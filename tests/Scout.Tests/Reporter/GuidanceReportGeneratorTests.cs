using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Analyzer.Recommendations;
using Scout.Core.Models;
using Scout.Reporter;
using Xunit;

namespace Scout.Tests.Reporter;

/// <summary>
/// The three "so what do I do now" report sections — distribution recommendation, daily-life
/// impact, and next steps — separate from the other report test files since these read from
/// several parts of <see cref="AnalysisResult"/>/<see cref="MachineProfile"/> at once rather than
/// one specific axis.
/// </summary>
public class GuidanceReportGeneratorTests
{
    private static MachineProfile MinimalProfile(
        IReadOnlyList<DeviceInfo>? devices = null,
        GpuLayout? gpuLayout = null,
        BitLockerStatus? partitionBitlockerStatus = BitLockerStatus.NotApplicable) => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "0.1.0",
        CollectedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        MachineId = "sha256:test",
        Privacy = new PrivacyInfo { HostnameIncluded = false, UsernameIncluded = false, SerialNumbersIncluded = false, RedactedFields = [] },
        System = new SystemInfo { Manufacturer = "Contoso", Model = "Latitude 9999", ChassisType = ChassisType.Desktop },
        Firmware = new FirmwareInfo { BiosVendor = "Contoso", BiosVersion = "1.0", BootMode = BootMode.UEFI, Tpm = new TpmInfo { Present = true } },
        Cpu = new CpuInfo { Vendor = "Test", Model = "Test", PhysicalCores = 1, LogicalProcessors = 1, Architecture = CpuArchitecture.x86_64, X86_64FeatureLevel = X86FeatureLevel.v3 },
        Memory = new MemoryInfo { TotalBytes = 16L * 1024 * 1024 * 1024, Modules = [] },
        Storage = new StorageInfo
        {
            BitlockerAvailable = true,
            Disks =
            [
                new DiskInfo
                {
                    DiskNumber = 0, MediaType = StorageMediaType.NVMe, BusType = "NVMe", SizeBytes = 500_000_000_000,
                    Model = "Test SSD", IsBootDisk = true, PartitionStyle = PartitionStyle.GPT,
                    Partitions = partitionBitlockerStatus is null
                        ? []
                        : [new PartitionInfo { Filesystem = "NTFS", SizeBytes = 490_000_000_000, DriveLetter = "C", BitlockerStatus = partitionBitlockerStatus, FreeBytes = 100_000_000_000 }]
                }
            ]
        },
        Devices = devices ?? [],
        GpuTopology = new GpuTopology { Gpus = [], Layout = gpuLayout },
        Os = new OsInfo { Edition = "Windows 11 Pro", Version = "10.0.26200", Build = "26200", Architecture = CpuArchitecture.x86_64 },
        Software = [],
        SoftwareFilteredCount = 0,
        Peripherals = [],
        CollectionErrors = []
    };

    private static DeviceInfo Device(string friendlyName, string deviceClass, string vendorId = "1002", string deviceId = "0000") => new()
    {
        HardwareId = $@"PCI\VEN_{vendorId}&DEV_{deviceId}",
        HardwareIdRaw = $@"PCI\VEN_{vendorId}&DEV_{deviceId}&REV_00",
        VendorId = vendorId,
        DeviceId = deviceId,
        BusType = DeviceBusType.PCI,
        CompatibleIds = [],
        FriendlyName = friendlyName,
        Class = deviceClass,
        Status = DeviceStatus.OK
    };

    private static DeviceAssessment Assessed(DeviceInfo device, SupportLevel support) => new()
    {
        Device = device,
        Match = new CompatibilityMatch { Level = MatchLevel.Exact, Entry = null, Support = support }
    };

    private static SoftwareEntry Software(string name) => new()
    {
        Name = name,
        Source = SoftwareSource.HklmUninstallKey,
        Category = SoftwareCategory.application,
        RegistryView = RegistryView.Native,
        SystemComponent = false
    };

    private static SoftwareAssessment SoftwareAssessed(string name, SoftwareCompatibilityStatus status) => new()
    {
        Software = Software(name),
        Match = new SoftwareMatch
        {
            Level = SoftwareMatchLevel.Contains,
            Entry = new SoftwareCompatibilityEntry
            {
                Match = new SoftwareMatchRule { NameAliases = [new SoftwareNameAlias { Pattern = name, MatchType = SoftwareMatchType.Contains }] },
                Status = status,
                Notes = LocalizedText.FromEnglish("n/a"),
                Importance = SoftwareImportance.Normal
            },
            Status = status,
            Signals = [SoftwareMatchSignal.Name]
        }
    };

    private static AnalysisResult Result(
        IReadOnlyList<DeviceAssessment>? devices = null,
        IReadOnlyList<SoftwareAssessment>? software = null,
        IReadOnlyList<SystemConstraint>? systemConstraints = null,
        Verdict verdict = Verdict.Ready) => new()
    {
        Devices = devices ?? [],
        Verdict = verdict,
        SystemConstraints = systemConstraints ?? [],
        DevicesRemovedForRelevance = 0,
        DevicesRemovedForDuplication = 0,
        SoftwareAssessments = software ?? [],
        SoftwareSkippedForCategory = 0,
        DistributionRecommendation = TestDistributionRecommendation.Default()
    };

    // ---- Distribution recommendation --------------------------------------------------------

    [Fact]
    public void Generate_ShowsThePrimaryDistributionNameReasonAndDownloadLink()
    {
        var recommendation = new DistributionRecommendation
        {
            Primary = TestDistributionRecommendation.Entry("Linux Mint", "Cinnamon", downloadUrl: "https://linuxmint.com/download.php"),
            PrimaryReason = DistributionRecommendationReason.WindowsFamiliarity,
            Secondary = TestDistributionRecommendation.Entry("Ubuntu", "GNOME")
        };
        var withRecommendation = new AnalysisResult
        {
            Devices = [], Verdict = Verdict.Ready, SystemConstraints = [], DevicesRemovedForRelevance = 0,
            DevicesRemovedForDuplication = 0, SoftwareAssessments = [], SoftwareSkippedForCategory = 0,
            DistributionRecommendation = recommendation
        };

        var html = new ReportGenerator().Generate(MinimalProfile(), withRecommendation);

        Assert.Contains(ReportStrings.DistributionSectionTitle, html);
        Assert.Contains("Linux Mint", html);
        Assert.Contains(ReportStrings.DistributionReason(DistributionRecommendationReason.WindowsFamiliarity, "Linux Mint"), html);
        Assert.Contains("https://linuxmint.com/download.php", html);
        Assert.Contains(ReportStrings.DistributionAlternative("Ubuntu", "GNOME"), html);
    }

    [Fact]
    public void Generate_DistributionSection_AppearsRightAfterTheVerdictBanner()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), Result());

        var verdictEnd = html.IndexOf("</header>", StringComparison.Ordinal);
        var distroIndex = html.IndexOf(ReportStrings.DistributionSectionTitle, StringComparison.Ordinal);
        var attentionOrNextIndex = html.IndexOf(ReportStrings.SystemSummaryTitle, StringComparison.Ordinal);

        Assert.True(verdictEnd < distroIndex);
        Assert.True(distroIndex < attentionOrNextIndex);
    }

    [Fact]
    public void Generate_NoSecondary_OmitsTheAlternativeLine()
    {
        var recommendation = new DistributionRecommendation
        {
            Primary = TestDistributionRecommendation.Entry("Only Option", "SomeDE"),
            PrimaryReason = DistributionRecommendationReason.WindowsFamiliarity,
            Secondary = null
        };
        var analysis = new AnalysisResult
        {
            Devices = [], Verdict = Verdict.Ready, SystemConstraints = [], DevicesRemovedForRelevance = 0,
            DevicesRemovedForDuplication = 0, SoftwareAssessments = [], SoftwareSkippedForCategory = 0,
            DistributionRecommendation = recommendation
        };

        var html = new ReportGenerator().Generate(MinimalProfile(), analysis);

        Assert.DoesNotContain("Alternatif:", html);
    }

    // ---- Daily-life impact -------------------------------------------------------------------

    [Fact]
    public void Generate_NoApplicableSignals_OmitsTheDailyLifeSectionEntirely()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), Result());

        Assert.DoesNotContain(ReportStrings.DailyLifeSectionTitle, html);
    }

    [Fact]
    public void Generate_NativePrinter_ShowsTheWorksFineBullet()
    {
        var printer = Device("HP LaserJet", "PrintQueue");
        var profile = MinimalProfile(devices: [printer]);
        var analysis = Result(devices: [Assessed(printer, SupportLevel.Native)]);

        var html = new ReportGenerator().Generate(profile, analysis);

        Assert.Contains(ReportStrings.DailyLifeSectionTitle, html);
        Assert.Contains(ReportStrings.DailyLifePrinter(SupportLevel.Native), html);
    }

    [Fact]
    public void Generate_FirmwareRequiredPrinter_ShowsTheCaveatBullet()
    {
        var printer = Device("Canon Printer", "PrintQueue");
        var profile = MinimalProfile(devices: [printer]);
        var analysis = Result(devices: [Assessed(printer, SupportLevel.FirmwareRequired)]);

        var html = new ReportGenerator().Generate(profile, analysis);

        Assert.Contains(ReportStrings.DailyLifePrinter(SupportLevel.FirmwareRequired), html);
    }

    [Fact]
    public void Generate_SteamInstalled_ShowsTheProtonBullet()
    {
        var analysis = Result(software: [SoftwareAssessed("Steam", SoftwareCompatibilityStatus.Native)]);

        var html = new ReportGenerator().Generate(MinimalProfile(), analysis);

        Assert.Contains(ReportStrings.DailyLifeGamingNative, html);
        Assert.DoesNotContain(ReportStrings.DailyLifeGamingWine, html);
    }

    [Fact]
    public void Generate_BattleNetInstalled_ShowsTheWineBullet()
    {
        var analysis = Result(software: [SoftwareAssessed("Battle.net", SoftwareCompatibilityStatus.Wine)]);

        var html = new ReportGenerator().Generate(MinimalProfile(), analysis);

        Assert.Contains(ReportStrings.DailyLifeGamingWine, html);
        Assert.DoesNotContain(ReportStrings.DailyLifeGamingNative, html);
    }

    [Fact]
    public void Generate_OfficeInstalled_ShowsTheLibreOfficeBullet()
    {
        var analysis = Result(software: [SoftwareAssessed("Microsoft Office Professional Plus 2019", SoftwareCompatibilityStatus.Equivalent)]);

        var html = new ReportGenerator().Generate(MinimalProfile(), analysis);

        Assert.Contains(ReportStrings.DailyLifeOffice, html);
    }

    [Fact]
    public void Generate_WifiFirmwareRequired_ShowsTheCableBullet()
    {
        var wifi = Device("Intel Wi-Fi 6", "Net");
        var profile = MinimalProfile(devices: [wifi]);
        var analysis = Result(devices: [Assessed(wifi, SupportLevel.FirmwareRequired)]);

        var html = new ReportGenerator().Generate(profile, analysis);

        Assert.Contains(ReportStrings.DailyLifeWifiFirmware, html);
    }

    [Fact]
    public void Generate_NativeEthernet_DoesNotShowTheWifiFirmwareBullet()
    {
        var nic = Device("Realtek Ethernet", "Net");
        var profile = MinimalProfile(devices: [nic]);
        var analysis = Result(devices: [Assessed(nic, SupportLevel.Native)]);

        var html = new ReportGenerator().Generate(profile, analysis);

        Assert.DoesNotContain(ReportStrings.DailyLifeWifiFirmware, html);
    }

    [Fact]
    public void Generate_HybridGpu_ShowsTheBatteryLifeBullet()
    {
        var profile = MinimalProfile(gpuLayout: GpuLayout.Hybrid);

        var html = new ReportGenerator().Generate(profile, Result());

        Assert.Contains(ReportStrings.DailyLifeHybridGpu, html);
    }

    [Fact]
    public void Generate_SingleGpu_DoesNotShowTheHybridBullet()
    {
        var profile = MinimalProfile(gpuLayout: GpuLayout.Single);

        var html = new ReportGenerator().Generate(profile, Result());

        Assert.DoesNotContain(ReportStrings.DailyLifeHybridGpu, html);
    }

    [Fact]
    public void Generate_MultipleApplicableSignals_ShowsAllOfThem()
    {
        var printer = Device("HP LaserJet", "PrintQueue");
        var profile = MinimalProfile(devices: [printer], gpuLayout: GpuLayout.Hybrid);
        var analysis = Result(
            devices: [Assessed(printer, SupportLevel.Native)],
            software: [SoftwareAssessed("Steam", SoftwareCompatibilityStatus.Native)]);

        var html = new ReportGenerator().Generate(profile, analysis);

        Assert.Contains(ReportStrings.DailyLifePrinter(SupportLevel.Native), html);
        Assert.Contains(ReportStrings.DailyLifeGamingNative, html);
        Assert.Contains(ReportStrings.DailyLifeHybridGpu, html);
    }

    // ---- Next steps ---------------------------------------------------------------------------

    [Theory]
    [InlineData(Verdict.Ready)]
    [InlineData(Verdict.MinorIssues)]
    [InlineData(Verdict.NeedsAttention)]
    [InlineData(Verdict.InsufficientData)]
    public void Generate_NonBlockedVerdicts_ShowTheNextStepsSection(Verdict verdict)
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), Result(verdict: verdict));

        Assert.Contains(ReportStrings.NextStepsSectionTitle, html);
        foreach (var step in ReportStrings.NextStepsList)
        {
            Assert.Contains(step, html);
        }
    }

    [Fact]
    public void Generate_BlockedVerdict_OmitsTheNextStepsSectionEntirely()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), Result(verdict: Verdict.Blocked));

        Assert.DoesNotContain(ReportStrings.NextStepsSectionTitle, html);
    }

    [Fact]
    public void Generate_LowDiskSpaceConstraint_ShowsTheDiskSpaceWarningInNextSteps()
    {
        var constraint = new SystemConstraint { Kind = SystemConstraintKind.LowDiskSpace, Severity = SystemConstraintSeverity.Critical, ObservedBytes = 1, ThresholdBytes = 2 };
        var analysis = Result(systemConstraints: [constraint]);

        var html = new ReportGenerator().Generate(MinimalProfile(), analysis);

        Assert.Contains(ReportStrings.NextStepsLowDiskSpaceWarning, html);
    }

    [Fact]
    public void Generate_NoLowDiskSpaceConstraint_OmitsTheDiskSpaceWarning()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), Result());

        Assert.DoesNotContain(ReportStrings.NextStepsLowDiskSpaceWarning, html);
    }

    [Theory]
    [InlineData(BitLockerStatus.FullyEncrypted)]
    [InlineData(BitLockerStatus.EncryptionInProgress)]
    public void Generate_BitLockerEnabled_ShowsTheBitLockerWarning(BitLockerStatus status)
    {
        var profile = MinimalProfile(partitionBitlockerStatus: status);

        var html = new ReportGenerator().Generate(profile, Result());

        Assert.Contains(ReportStrings.NextStepsBitLockerWarning, html);
    }

    [Theory]
    [InlineData(BitLockerStatus.FullyDecrypted)]
    [InlineData(BitLockerStatus.NotApplicable)]
    public void Generate_BitLockerNotEnabled_OmitsTheBitLockerWarning(BitLockerStatus status)
    {
        var profile = MinimalProfile(partitionBitlockerStatus: status);

        var html = new ReportGenerator().Generate(profile, Result());

        Assert.DoesNotContain(ReportStrings.NextStepsBitLockerWarning, html);
    }
}
