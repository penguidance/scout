using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Analyzer;

public class VerdictEngineTests
{
    private static CompatibilityMatch Match(SupportLevel support) => new()
    {
        Level = support == SupportLevel.Unknown ? MatchLevel.None : MatchLevel.Exact,
        Entry = null,
        Support = support
    };

    private static SystemConstraint Constraint(SystemConstraintSeverity severity) => new()
    {
        Kind = SystemConstraintKind.LegacyBootMode,
        Severity = severity
    };

    private static SoftwareAssessment SoftwareAssessment(SoftwareCompatibilityStatus status, SoftwareImportance importance) => new()
    {
        Software = TestSoftware.Create("Test App"),
        Match = new SoftwareMatch
        {
            Level = SoftwareMatchLevel.Contains,
            Entry = new SoftwareCompatibilityEntry
            {
                Match = new SoftwareMatchRule
                {
                    NameAliases = [new SoftwareNameAlias { Pattern = "Test", MatchType = SoftwareMatchType.Contains }]
                },
                Status = status,
                Notes = "n/a",
                Importance = importance
            },
            Status = status,
            Signals = [SoftwareMatchSignal.Name]
        }
    };

    [Fact]
    public void Decide_AllNative_ReturnsReady()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.Native) };

        Assert.Equal(Verdict.Ready, new VerdictEngine().Decide(matches));
    }

    [Fact]
    public void Decide_NoDevicesAtAll_ReturnsInsufficientData()
    {
        Assert.Equal(Verdict.InsufficientData, new VerdictEngine().Decide([]));
    }

    [Fact]
    public void Decide_AnyUnsupported_ReturnsBlockedRegardlessOfOtherDevices()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.Unsupported), Match(SupportLevel.Unknown) };

        Assert.Equal(Verdict.Blocked, new VerdictEngine().Decide(matches));
    }

    [Theory]
    [InlineData(SupportLevel.Proprietary)]
    [InlineData(SupportLevel.Partial)]
    public void Decide_ProprietaryOrPartialWithNoUnsupported_ReturnsNeedsAttention(SupportLevel support)
    {
        var matches = new[] { Match(SupportLevel.Native), Match(support) };

        Assert.Equal(Verdict.NeedsAttention, new VerdictEngine().Decide(matches));
    }

    [Fact]
    public void Decide_FirmwareRequiredOnly_ReturnsMinorIssues()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.FirmwareRequired) };

        Assert.Equal(Verdict.MinorIssues, new VerdictEngine().Decide(matches));
    }

    [Fact]
    public void Decide_MajorityUnknown_ReturnsInsufficientDataEvenWithoutAnyBadFinding()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.Unknown), Match(SupportLevel.Unknown) };

        // 2/3 unknown > default 0.5 threshold.
        Assert.Equal(Verdict.InsufficientData, new VerdictEngine().Decide(matches));
    }

    [Fact]
    public void Decide_MinorityUnknown_StillReturnsReadyWhenNothingElseIsWrong()
    {
        var matches = new[]
        {
            Match(SupportLevel.Native), Match(SupportLevel.Native), Match(SupportLevel.Native),
            Match(SupportLevel.Native), Match(SupportLevel.Unknown)
        };

        // 1/5 = 20% unknown, below the default 50% threshold.
        Assert.Equal(Verdict.Ready, new VerdictEngine().Decide(matches));
    }

    [Fact]
    public void Decide_UnsupportedTakesPriorityOverHighUnknownRatio()
    {
        var matches = new[] { Match(SupportLevel.Unsupported), Match(SupportLevel.Unknown), Match(SupportLevel.Unknown) };

        Assert.Equal(Verdict.Blocked, new VerdictEngine().Decide(matches));
    }

    [Fact]
    public void Decide_WithCustomThreshold_HonorsIt()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.Unknown) };

        // 50% unknown; with a strict 0.1 threshold that's already "too many".
        var engine = new VerdictEngine(insufficientDataUnknownRatio: 0.1);

        Assert.Equal(Verdict.InsufficientData, engine.Decide(matches));
    }

    [Fact]
    public void Constructor_RejectsOutOfRangeThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VerdictEngine(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VerdictEngine(1.1));
    }

    // ---- Decide(matches, systemConstraints): combining the two axes -------------------------

    [Fact]
    public void Decide_CriticalConstraint_FloorsAnOtherwiseReadyResultAtNeedsAttention()
    {
        var matches = new[] { Match(SupportLevel.Native) };
        var constraints = new[] { Constraint(SystemConstraintSeverity.Critical) };

        Assert.Equal(Verdict.NeedsAttention, new VerdictEngine().Decide(matches, constraints));
    }

    [Fact]
    public void Decide_SoftConstraint_FloorsAnOtherwiseReadyResultAtMinorIssues()
    {
        var matches = new[] { Match(SupportLevel.Native) };
        var constraints = new[] { Constraint(SystemConstraintSeverity.Soft) };

        Assert.Equal(Verdict.MinorIssues, new VerdictEngine().Decide(matches, constraints));
    }

    [Fact]
    public void Decide_NoConstraints_BehavesExactlyLikeTheDeviceOnlyOverload()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.FirmwareRequired) };

        Assert.Equal(new VerdictEngine().Decide(matches), new VerdictEngine().Decide(matches, []));
    }

    [Fact]
    public void Decide_SoftConstraint_NeverLowersAWorseDeviceVerdict()
    {
        var matches = new[] { Match(SupportLevel.Unsupported) };
        var constraints = new[] { Constraint(SystemConstraintSeverity.Soft) };

        Assert.Equal(Verdict.Blocked, new VerdictEngine().Decide(matches, constraints));
    }

    [Fact]
    public void Decide_CriticalConstraint_DoesNotDowngradeAWorseDeviceVerdict()
    {
        var matches = new[] { Match(SupportLevel.Unsupported) };
        var constraints = new[] { Constraint(SystemConstraintSeverity.Critical) };

        Assert.Equal(Verdict.Blocked, new VerdictEngine().Decide(matches, constraints));
    }

    [Fact]
    public void Decide_CriticalConstraint_DoesNotRaiseADeviceNeedsAttentionResult()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.Proprietary) };
        var constraints = new[] { Constraint(SystemConstraintSeverity.Critical) };

        // NeedsAttention already equals the critical floor — the combined result stays there,
        // it does not somehow escalate further just because both axes independently reached it.
        Assert.Equal(Verdict.NeedsAttention, new VerdictEngine().Decide(matches, constraints));
    }

    // ---- EvaluateSystemConstraints --------------------------------------------------------

    private static MachineProfile ProfileFor(
        X86FeatureLevel? featureLevel = X86FeatureLevel.v3,
        BootMode? bootMode = BootMode.UEFI,
        long? totalMemoryBytes = 16L * 1024 * 1024 * 1024,
        long? systemDiskFreeBytes = 100L * 1024 * 1024 * 1024,
        bool hasBootDisk = true) => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "test",
        CollectedAt = DateTimeOffset.UtcNow,
        MachineId = "sha256:test",
        Privacy = new PrivacyInfo { HostnameIncluded = false, UsernameIncluded = false, SerialNumbersIncluded = false, RedactedFields = [] },
        System = new SystemInfo { Manufacturer = "Test", Model = "Test", ChassisType = ChassisType.Desktop },
        Firmware = new FirmwareInfo { BiosVendor = "Test", BiosVersion = "1.0", BootMode = bootMode, Tpm = new TpmInfo { Present = false } },
        Cpu = new CpuInfo
        {
            Vendor = "Test", Model = "Test", PhysicalCores = 1, LogicalProcessors = 1,
            Architecture = CpuArchitecture.x86_64, X86_64FeatureLevel = featureLevel
        },
        Memory = new MemoryInfo { TotalBytes = totalMemoryBytes, Modules = [] },
        Storage = new StorageInfo
        {
            BitlockerAvailable = null,
            Disks = hasBootDisk
                ?
                [
                    new DiskInfo
                    {
                        DiskNumber = 0, MediaType = StorageMediaType.SSD, BusType = "SATA", SizeBytes = 500_000_000_000,
                        Model = "Test SSD", IsBootDisk = true, PartitionStyle = PartitionStyle.GPT,
                        Partitions = systemDiskFreeBytes is null
                            ? []
                            :
                            [
                                new PartitionInfo
                                {
                                    Filesystem = "NTFS", SizeBytes = 500_000_000_000, DriveLetter = "C",
                                    BitlockerStatus = BitLockerStatus.NotApplicable, FreeBytes = systemDiskFreeBytes
                                }
                            ]
                    }
                ]
                : []
        },
        Devices = [],
        GpuTopology = new GpuTopology { Gpus = [], Layout = null },
        Os = new OsInfo { Edition = "Test", Version = "1.0", Build = "1", Architecture = CpuArchitecture.x86_64 },
        Software = [],
        SoftwareFilteredCount = 0,
        Peripherals = [],
        CollectionErrors = []
    };

    [Fact]
    public void EvaluateSystemConstraints_CleanModernMachine_ReturnsNoConstraints()
    {
        var constraints = new VerdictEngine().EvaluateSystemConstraints(ProfileFor());

        Assert.Empty(constraints);
    }

    [Fact]
    public void EvaluateSystemConstraints_V2FeatureLevel_IsSoft()
    {
        var constraints = new VerdictEngine().EvaluateSystemConstraints(ProfileFor(featureLevel: X86FeatureLevel.v2));

        var constraint = Assert.Single(constraints);
        Assert.Equal(SystemConstraintKind.LowX86FeatureLevel, constraint.Kind);
        Assert.Equal(SystemConstraintSeverity.Soft, constraint.Severity);
        Assert.Equal(X86FeatureLevel.v2, constraint.ObservedFeatureLevel);
    }

    [Fact]
    public void EvaluateSystemConstraints_NullFeatureLevel_IsNeverGuessedAtAndProducesNoConstraint()
    {
        var constraints = new VerdictEngine().EvaluateSystemConstraints(ProfileFor(featureLevel: null));

        Assert.DoesNotContain(constraints, c => c.Kind == SystemConstraintKind.LowX86FeatureLevel);
    }

    [Fact]
    public void EvaluateSystemConstraints_LegacyBootMode_IsSoft()
    {
        var constraints = new VerdictEngine().EvaluateSystemConstraints(ProfileFor(bootMode: BootMode.Legacy));

        var constraint = Assert.Single(constraints);
        Assert.Equal(SystemConstraintKind.LegacyBootMode, constraint.Kind);
        Assert.Equal(SystemConstraintSeverity.Soft, constraint.Severity);
    }

    [Fact]
    public void EvaluateSystemConstraints_BelowMemoryThreshold_IsSoft()
    {
        var constraints = new VerdictEngine().EvaluateSystemConstraints(
            ProfileFor(totalMemoryBytes: 2L * 1024 * 1024 * 1024));

        var constraint = Assert.Single(constraints);
        Assert.Equal(SystemConstraintKind.LowMemory, constraint.Kind);
        Assert.Equal(SystemConstraintSeverity.Soft, constraint.Severity);
        Assert.Equal(2L * 1024 * 1024 * 1024, constraint.ObservedBytes);
    }

    [Fact]
    public void EvaluateSystemConstraints_BelowFreeDiskSpaceThreshold_IsCritical()
    {
        var constraints = new VerdictEngine().EvaluateSystemConstraints(
            ProfileFor(systemDiskFreeBytes: 5L * 1024 * 1024 * 1024));

        var constraint = Assert.Single(constraints);
        Assert.Equal(SystemConstraintKind.LowDiskSpace, constraint.Kind);
        Assert.Equal(SystemConstraintSeverity.Critical, constraint.Severity);
        Assert.Equal(5L * 1024 * 1024 * 1024, constraint.ObservedBytes);
    }

    [Fact]
    public void EvaluateSystemConstraints_NoBootDiskIdentified_NeverGuessesAtDiskSpace()
    {
        // No disk has IsBootDisk == true — we genuinely do not know which disk (if any) is the
        // system disk, so the check is skipped rather than guessing at the first disk in the list.
        var constraints = new VerdictEngine().EvaluateSystemConstraints(ProfileFor(hasBootDisk: false));

        Assert.DoesNotContain(constraints, c => c.Kind == SystemConstraintKind.LowDiskSpace);
    }

    [Fact]
    public void EvaluateSystemConstraints_BootDiskWithUnreadableFreeSpace_NeverGuessesAtDiskSpace()
    {
        var constraints = new VerdictEngine().EvaluateSystemConstraints(ProfileFor(systemDiskFreeBytes: null));

        Assert.DoesNotContain(constraints, c => c.Kind == SystemConstraintKind.LowDiskSpace);
    }

    [Fact]
    public void EvaluateSystemConstraints_MultipleFindingsAtOnce_AllAppear()
    {
        var profile = ProfileFor(
            featureLevel: X86FeatureLevel.v2,
            bootMode: BootMode.Legacy,
            totalMemoryBytes: 2L * 1024 * 1024 * 1024,
            systemDiskFreeBytes: 5L * 1024 * 1024 * 1024);

        var constraints = new VerdictEngine().EvaluateSystemConstraints(profile);

        Assert.Equal(4, constraints.Count);
    }

    [Fact]
    public void EvaluateSystemConstraints_ThresholdsAreConfigurable()
    {
        var strictEngine = new VerdictEngine(
            minimumRecommendedFeatureLevel: X86FeatureLevel.v4,
            minimumRecommendedMemoryBytes: 32L * 1024 * 1024 * 1024,
            minimumFreeSystemDiskBytes: 200L * 1024 * 1024 * 1024);

        // ProfileFor()'s defaults (v3, 16 GiB RAM, 100 GiB free) pass the engine's own defaults
        // but fail every one of these deliberately stricter thresholds.
        var constraints = strictEngine.EvaluateSystemConstraints(ProfileFor());

        Assert.Contains(constraints, c => c.Kind == SystemConstraintKind.LowX86FeatureLevel);
        Assert.Contains(constraints, c => c.Kind == SystemConstraintKind.LowMemory);
        Assert.Contains(constraints, c => c.Kind == SystemConstraintKind.LowDiskSpace);
    }

    [Fact]
    public void EvaluateSystemConstraints_ThrowsOnNullProfile()
    {
        Assert.Throws<ArgumentNullException>(() => new VerdictEngine().EvaluateSystemConstraints(null!));
    }

    // ---- Decide(matches, constraints, softwareAssessments): the software floor -------------

    [Fact]
    public void Decide_BlockedCriticalSoftware_FloorsAnOtherwiseReadyResultAtNeedsAttention()
    {
        var matches = new[] { Match(SupportLevel.Native) };
        var software = new[] { SoftwareAssessment(SoftwareCompatibilityStatus.Blocked, SoftwareImportance.Critical) };

        Assert.Equal(Verdict.NeedsAttention, new VerdictEngine().Decide(matches, [], software));
    }

    [Theory]
    [InlineData(SoftwareImportance.Normal)]
    [InlineData(SoftwareImportance.Minor)]
    public void Decide_BlockedNonCriticalSoftware_NeverChangesAnOtherwiseReadyResult(SoftwareImportance importance)
    {
        var matches = new[] { Match(SupportLevel.Native) };
        var software = new[] { SoftwareAssessment(SoftwareCompatibilityStatus.Blocked, importance) };

        Assert.Equal(Verdict.Ready, new VerdictEngine().Decide(matches, [], software));
    }

    [Theory]
    [InlineData(SoftwareCompatibilityStatus.Wine)]
    [InlineData(SoftwareCompatibilityStatus.Equivalent)]
    [InlineData(SoftwareCompatibilityStatus.Web)]
    [InlineData(SoftwareCompatibilityStatus.BuiltIn)]
    [InlineData(SoftwareCompatibilityStatus.Partial)]
    [InlineData(SoftwareCompatibilityStatus.Unknown)]
    public void Decide_CriticalImportanceButNotBlocked_NeverChangesAnOtherwiseReadyResult(SoftwareCompatibilityStatus status)
    {
        // Importance only matters for a Blocked verdict — critical-but-runs-via-Wine (or BuiltIn/
        // Partial) software is not, by itself, a reason to downgrade the machine's overall verdict.
        var matches = new[] { Match(SupportLevel.Native) };
        var software = new[] { SoftwareAssessment(status, SoftwareImportance.Critical) };

        Assert.Equal(Verdict.Ready, new VerdictEngine().Decide(matches, [], software));
    }

    [Fact]
    public void Decide_BlockedCriticalSoftware_NeverDowngradesAWorseDeviceVerdict()
    {
        var matches = new[] { Match(SupportLevel.Unsupported) };
        var software = new[] { SoftwareAssessment(SoftwareCompatibilityStatus.Blocked, SoftwareImportance.Critical) };

        Assert.Equal(Verdict.Blocked, new VerdictEngine().Decide(matches, [], software));
    }

    [Fact]
    public void Decide_NoSoftwareAssessments_BehavesExactlyLikeTheTwoArgumentOverload()
    {
        var matches = new[] { Match(SupportLevel.Native), Match(SupportLevel.FirmwareRequired) };
        var constraints = new[] { Constraint(SystemConstraintSeverity.Soft) };

        Assert.Equal(
            new VerdictEngine().Decide(matches, constraints),
            new VerdictEngine().Decide(matches, constraints, []));
    }

    [Fact]
    public void Decide_SystemConstraintAndCriticalBlockedSoftwareTogether_CombinesToTheHigherFloor()
    {
        var matches = new[] { Match(SupportLevel.Native) };
        var softConstraint = new[] { Constraint(SystemConstraintSeverity.Soft) }; // floor: MinorIssues
        var software = new[] { SoftwareAssessment(SoftwareCompatibilityStatus.Blocked, SoftwareImportance.Critical) }; // floor: NeedsAttention

        Assert.Equal(Verdict.NeedsAttention, new VerdictEngine().Decide(matches, softConstraint, software));
    }
}
