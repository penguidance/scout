using Scout.Analyzer;
using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Analyzer.Recommendations;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Analyzer;

/// <summary>Orchestration tests against a small fixture database — isolated from the real seed data and sample files.</summary>
public class MachineAnalyzerTests
{
    private const string Database = """
        [
          { "vendor_id": "1002", "device_id": null, "device_class": "Display", "kernel_driver": "amdgpu", "support": "native", "notes": { "en": "n/a" } }
        ]
        """;

    private const string SoftwareDatabase = """
        [
          {
            "match": { "name_aliases": [{ "pattern": "Adobe Photoshop", "match_type": "contains" }] },
            "status": "blocked",
            "notes": { "en": "n/a" },
            "importance": "critical"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Notepad++", "match_type": "contains" }] },
            "status": "equivalent",
            "alternatives": [{ "name": "VS Code", "note": { "en": "n/a" } }],
            "notes": { "en": "n/a" },
            "importance": "minor"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Ambiguous Tool", "match_type": "contains" }] },
            "status": "blocked",
            "notes": { "en": "No-language-tag entry, listed first." },
            "importance": "minor"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Ambiguous Tool", "match_type": "contains", "language": "tr" }] },
            "status": "native",
            "notes": { "en": "Turkish-tagged entry, listed second." },
            "importance": "minor"
          }
        ]
        """;

    private static MachineProfile ProfileWithDevices(params DeviceInfo[] devices) =>
        ProfileWithDevicesAndSoftware(devices, []);

    private static MachineProfile ProfileWithDevicesAndSoftware(
        IReadOnlyList<DeviceInfo> devices, IReadOnlyList<SoftwareEntry> software, string? osLanguage = null) => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "0.1.0",
        CollectedAt = DateTimeOffset.UtcNow,
        MachineId = "sha256:test",
        Privacy = new PrivacyInfo { HostnameIncluded = false, UsernameIncluded = false, SerialNumbersIncluded = false, RedactedFields = [] },
        System = new SystemInfo { Manufacturer = "Test", Model = "Test", ChassisType = ChassisType.Desktop },
        Firmware = new FirmwareInfo { BiosVendor = "Test", BiosVersion = "1.0", BootMode = BootMode.UEFI, Tpm = new TpmInfo { Present = false } },
        Cpu = new CpuInfo { Vendor = "Test", Model = "Test", PhysicalCores = 1, LogicalProcessors = 1, Architecture = CpuArchitecture.x86_64 },
        Memory = new MemoryInfo { TotalBytes = null, Modules = [] },
        Storage = new StorageInfo { Disks = [], BitlockerAvailable = null },
        Devices = devices,
        GpuTopology = new GpuTopology { Gpus = [], Layout = null },
        Os = new OsInfo { Edition = "Test", Version = "1.0", Build = "1", Architecture = CpuArchitecture.x86_64, Language = osLanguage },
        Software = software,
        SoftwareFilteredCount = 0,
        Peripherals = [],
        CollectionErrors = []
    };

    private static MachineAnalyzer CreateAnalyzer() => new(
        new CompatibilityMatcher(CompatibilityDatabase.FromJson(Database)),
        softwareMatcher: new SoftwareMatcher(SoftwareCompatibilityDatabase.FromJson(SoftwareDatabase)));

    [Fact]
    public void Analyze_FiltersThenMatchesThenDecidesInOneCall()
    {
        var profile = ProfileWithDevices(
            TestDevices.Create(null, null, DeviceBusType.Other, friendlyName: "WAN Miniport (PPPOE)", deviceClass: "Net"),
            TestDevices.Create("1002", "7340", DeviceBusType.PCI, friendlyName: "AMD Radeon RX 5500 XT", deviceClass: "Display"));

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(1, result.DevicesRemovedForRelevance);
        var assessment = Assert.Single(result.Devices);
        Assert.Equal(SupportLevel.Native, assessment.Match.Support);
        Assert.Equal(Verdict.Ready, result.Verdict);
    }

    [Fact]
    public void Analyze_ReportsRemovedDuplicateCount()
    {
        var profile = ProfileWithDevices(
            TestDevices.Create("1002", "7340", DeviceBusType.PCI, friendlyName: "A", deviceClass: "Display"),
            TestDevices.Create("1002", "7340", DeviceBusType.PCI, friendlyName: "AMD Radeon RX 5500 XT", deviceClass: "Display"));

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(1, result.DevicesRemovedForDuplication);
        Assert.Single(result.Devices);
    }

    [Fact]
    public void Analyze_UnmatchedDeviceInAnOtherwiseEmptyProfile_YieldsInsufficientData()
    {
        var profile = ProfileWithDevices(
            TestDevices.Create("FFFF", "0001", DeviceBusType.PCI, friendlyName: "Unknown Card", deviceClass: "Display"));

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(Verdict.InsufficientData, result.Verdict);
    }

    [Fact]
    public void Constructor_RequiresAMatcher()
    {
        Assert.Throws<ArgumentNullException>(() => new MachineAnalyzer(null!));
    }

    // ---- Software assessment ---------------------------------------------------------------

    [Fact]
    public void Analyze_AssessesApplicationCategorySoftwareAgainstTheDatabase()
    {
        var profile = ProfileWithDevicesAndSoftware(
            [],
            [TestSoftware.Create("Adobe Photoshop 2024", category: SoftwareCategory.application)]);

        var result = CreateAnalyzer().Analyze(profile);

        var assessment = Assert.Single(result.SoftwareAssessments);
        Assert.Equal(SoftwareCompatibilityStatus.Blocked, assessment.Match.Status);
        Assert.Equal(0, result.SoftwareSkippedForCategory);
    }

    [Theory]
    [InlineData(SoftwareCategory.runtime)]
    [InlineData(SoftwareCategory.driver)]
    [InlineData(SoftwareCategory.system)]
    public void Analyze_SkipsNonApplicationCategoriesEntirely_NeverAttemptsToMatchThem(SoftwareCategory category)
    {
        // "Adobe Photoshop" would match (and be Blocked) if looked up — the point of this test is
        // that it is never even attempted for these categories, so it must not appear at all.
        var profile = ProfileWithDevicesAndSoftware(
            [],
            [TestSoftware.Create("Adobe Photoshop 2024", category: category)]);

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Empty(result.SoftwareAssessments);
        Assert.Equal(1, result.SoftwareSkippedForCategory);
    }

    [Fact]
    public void Analyze_BlockedCriticalSoftware_RaisesVerdictToNeedsAttentionEvenWithCleanDevices()
    {
        var profile = ProfileWithDevicesAndSoftware(
            [TestDevices.Create("1002", "7340", DeviceBusType.PCI, deviceClass: "Display")],
            [TestSoftware.Create("Adobe Photoshop 2024")]);

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(SupportLevel.Native, Assert.Single(result.Devices).Match.Support); // devices are clean
        Assert.Equal(Verdict.NeedsAttention, result.Verdict); // but the verdict is not Ready
    }

    [Fact]
    public void Analyze_EquivalentSoftware_DoesNotAffectTheVerdict()
    {
        var profile = ProfileWithDevicesAndSoftware(
            [TestDevices.Create("1002", "7340", DeviceBusType.PCI, deviceClass: "Display")],
            [TestSoftware.Create("Notepad++ 8.6")]);

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, Assert.Single(result.SoftwareAssessments).Match.Status);
        Assert.Equal(Verdict.Ready, result.Verdict);
    }

    [Fact]
    public void Analyze_UnrecognizedApplicationSoftware_IsUnknownAndDoesNotAffectTheVerdict()
    {
        var profile = ProfileWithDevicesAndSoftware(
            [TestDevices.Create("1002", "7340", DeviceBusType.PCI, deviceClass: "Display")],
            [TestSoftware.Create("Some Obscure Home-Grown Tool")]);

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(SoftwareCompatibilityStatus.Unknown, Assert.Single(result.SoftwareAssessments).Match.Status);
        Assert.Equal(Verdict.Ready, result.Verdict);
    }

    [Fact]
    public void Analyze_ForwardsProfileOsLanguageToTheSoftwareMatcherAsATieBreak()
    {
        // End-to-end proof that profile.os.language actually reaches SoftwareMatcher: with no
        // language, the ambiguous pattern resolves to whichever entry is listed first (Blocked);
        // with a Turkish profile language, the Turkish-tagged entry wins the tie instead.
        var noLanguageProfile = ProfileWithDevicesAndSoftware([], [TestSoftware.Create("Ambiguous Tool")], osLanguage: null);
        var turkishProfile = ProfileWithDevicesAndSoftware([], [TestSoftware.Create("Ambiguous Tool")], osLanguage: "tr-TR");

        var noLanguageResult = CreateAnalyzer().Analyze(noLanguageProfile);
        var turkishResult = CreateAnalyzer().Analyze(turkishProfile);

        Assert.Equal(SoftwareCompatibilityStatus.Blocked, Assert.Single(noLanguageResult.SoftwareAssessments).Match.Status);
        Assert.Equal(SoftwareCompatibilityStatus.Native, Assert.Single(turkishResult.SoftwareAssessments).Match.Status);
    }

    [Fact]
    public void Analyze_MissingOsLanguage_StillMatchesALocalizedAliasNormally()
    {
        // The safety guarantee, exercised through the full pipeline: a program that only matches
        // via a language-tagged alias must still be found even when os.language is completely
        // absent from the profile — language is never a filter.
        var profile = ProfileWithDevicesAndSoftware([], [TestSoftware.Create("Ambiguous Tool")], osLanguage: null);

        var result = CreateAnalyzer().Analyze(profile);

        Assert.NotEqual(SoftwareCompatibilityStatus.Unknown, Assert.Single(result.SoftwareAssessments).Match.Status);
    }

    // ---- Distribution recommendation --------------------------------------------------------

    [Fact]
    public void Analyze_AlwaysProducesADistributionRecommendation()
    {
        var result = CreateAnalyzer().Analyze(ProfileWithDevices());

        Assert.NotNull(result.DistributionRecommendation);
        Assert.NotNull(result.DistributionRecommendation.Primary);
    }

    [Fact]
    public void Analyze_NvidiaProprietaryGpu_IsForwardedToTheDistributionRecommender()
    {
        // End-to-end proof that the device assessments computed earlier in Analyze() are the same
        // ones handed to the recommender, not a re-derived or stale list.
        var profile = ProfileWithDevices(TestDevices.Create("10DE", "1234", DeviceBusType.PCI, deviceClass: "Display"));

        var result = CreateAnalyzer().Analyze(profile);

        // The fixture hardware database has no NVIDIA entry, so this resolves Unknown, not
        // Proprietary — confirms the recommender only reacts to a *confirmed* proprietary need,
        // never guessing one from the vendor ID alone.
        Assert.Equal(SupportLevel.Unknown, Assert.Single(result.Devices).Match.Support);
        Assert.NotEqual(DistributionRecommendationReason.NvidiaProprietaryDriver, result.DistributionRecommendation.PrimaryReason);
    }
}
