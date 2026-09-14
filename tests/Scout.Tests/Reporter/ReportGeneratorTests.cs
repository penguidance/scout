using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Models;
using Scout.Reporter;
using Xunit;

namespace Scout.Tests.Reporter;

public class ReportGeneratorTests
{
    private static MachineProfile MinimalProfile(
        ChassisType? chassisType = ChassisType.Desktop,
        BootMode? bootMode = BootMode.UEFI,
        bool? tpmPresent = true,
        string? tpmSpecVersion = "2.0",
        bool? bitlockerAvailable = true,
        string? osLanguage = null) => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "0.1.0",
        CollectedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        MachineId = "sha256:test",
        Privacy = new PrivacyInfo { HostnameIncluded = true, UsernameIncluded = false, SerialNumbersIncluded = false, RedactedFields = [] },
        System = new SystemInfo { Manufacturer = "Contoso", Model = "Latitude 9999", ChassisType = chassisType, Hostname = "TEST-PC" },
        Firmware = new FirmwareInfo
        {
            BiosVendor = "Contoso",
            BiosVersion = "1.0",
            BootMode = bootMode,
            SecureBootEnabled = true,
            Tpm = new TpmInfo { Present = tpmPresent, SpecVersion = tpmSpecVersion }
        },
        Cpu = new CpuInfo
        {
            Vendor = "AuthenticAMD",
            Model = "AMD Ryzen 5 5600X",
            PhysicalCores = 6,
            LogicalProcessors = 12,
            Architecture = CpuArchitecture.x86_64,
            X86_64FeatureLevel = X86FeatureLevel.v3
        },
        Memory = new MemoryInfo { TotalBytes = 34_359_738_368, Modules = [] },
        Storage = new StorageInfo
        {
            BitlockerAvailable = bitlockerAvailable,
            Disks = [new DiskInfo { DiskNumber = 0, MediaType = StorageMediaType.NVMe, BusType = "NVMe", SizeBytes = 500_107_862_016, Model = "Test SSD", IsBootDisk = true, PartitionStyle = PartitionStyle.GPT, Partitions = [] }]
        },
        Devices = [],
        GpuTopology = new GpuTopology { Gpus = [], Layout = null },
        Os = new OsInfo { Edition = "Windows 11 Pro", Version = "10.0.26200", Build = "26200", Architecture = CpuArchitecture.x86_64, Language = osLanguage },
        Software = [],
        SoftwareFilteredCount = 0,
        Peripherals = [],
        CollectionErrors = []
    };

    private static DeviceInfo Device(string friendlyName, string vendorId = "1002", string deviceId = "7340", string deviceClass = "Display") => new()
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

    private static CompatibilityEntry Entry(SupportLevel support, string driver = "amdgpu", string notes = "test notes", string? notesTr = null) => new()
    {
        VendorId = "1002",
        KernelDriver = driver,
        Support = support,
        Notes = notesTr is null
            ? LocalizedText.FromEnglish(notes)
            : new LocalizedText(new Dictionary<string, string> { ["en"] = notes, ["tr"] = notesTr })
    };

    private static AnalysisResult SingleDeviceResult(DeviceInfo device, SupportLevel support, MatchLevel level = MatchLevel.Exact, CompatibilityEntry? entry = null, Verdict verdict = Verdict.Ready)
    {
        var match = new CompatibilityMatch { Level = level, Entry = entry ?? (level == MatchLevel.None ? null : Entry(support)), Support = support };
        return new AnalysisResult
        {
            Devices = [new DeviceAssessment { Device = device, Match = match }],
            Verdict = verdict,
            SystemConstraints = [],
            DevicesRemovedForRelevance = 3,
            DevicesRemovedForDuplication = 2,
            SoftwareAssessments = [],
            SoftwareSkippedForCategory = 0,
            DistributionRecommendation = TestDistributionRecommendation.Default()
        };
    }

    [Fact]
    public void Generate_ProducesASingleSelfContainedHtmlDocumentWithNoExternalResources()
    {
        // "Self-contained" means the document needs no network access to render correctly (no
        // external script/stylesheet/font/image) — it does not mean the document can never
        // mention a URL. The distribution recommendation's download link (a plain <a href>, only
        // ever followed if the reader chooses to click it) is a deliberate, intentional exception:
        // nothing is fetched to display the report itself.
        var html = new ReportGenerator().Generate(MinimalProfile(), SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<style>", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_AllNativeDevices_OmitsTheAttentionSectionEntirely()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.DoesNotContain(ReportStrings.AttentionSectionTitle, html);
    }

    [Fact]
    public void Generate_WithANonNativeDevice_ShowsAttentionSectionUsingDatabaseNotesVerbatim()
    {
        var device = Device("NVIDIA GeForce RTX 3060", vendorId: "10DE", deviceId: "2504");
        var entry = Entry(SupportLevel.Proprietary, driver: "nvidia", notes: "Bu tam olarak veritabanından gelen benzersiz bir not metnidir.");
        var result = SingleDeviceResult(device, SupportLevel.Proprietary, entry: entry, verdict: Verdict.NeedsAttention);

        var html = new ReportGenerator().Generate(MinimalProfile(), result);

        Assert.Contains(ReportStrings.AttentionSectionTitle, html);
        Assert.Contains("Bu tam olarak veritabanından gelen benzersiz bir not metnidir.", html);
        Assert.Contains("NVIDIA GeForce RTX 3060", html);
    }

    [Fact]
    public void Generate_ProfileLanguageMatchesADatabaseTranslation_UsesThatLanguage()
    {
        var device = Device("NVIDIA GeForce RTX 3060", vendorId: "10DE", deviceId: "2504");
        var entry = Entry(SupportLevel.Proprietary, driver: "nvidia", notes: "English-only note.", notesTr: "Türkçe not.");
        var result = SingleDeviceResult(device, SupportLevel.Proprietary, entry: entry, verdict: Verdict.NeedsAttention);

        var html = new ReportGenerator().Generate(MinimalProfile(osLanguage: "tr-TR"), result);

        Assert.Contains("Türkçe not.", html);
        Assert.DoesNotContain("English-only note.", html);
    }

    [Fact]
    public void Generate_ProfileLanguageHasNoTranslation_FallsBackToEnglish()
    {
        var device = Device("NVIDIA GeForce RTX 3060", vendorId: "10DE", deviceId: "2504");
        var entry = Entry(SupportLevel.Proprietary, driver: "nvidia", notes: "English-only note.", notesTr: "Türkçe not.");
        var result = SingleDeviceResult(device, SupportLevel.Proprietary, entry: entry, verdict: Verdict.NeedsAttention);

        // German is not one of this entry's languages — must fall back to "en", never throw and
        // never show an empty cell.
        var html = new ReportGenerator().Generate(MinimalProfile(osLanguage: "de-DE"), result);

        Assert.Contains("English-only note.", html);
        Assert.DoesNotContain("Türkçe not.", html);
    }

    [Fact]
    public void Generate_UnknownDevice_DoesNotAppearInAttentionButStillAppearsInTechnicalDump()
    {
        // An Unknown device (no database entry at all, e.g. a USB root hub with no vendor_id) is
        // a gap in what we could check, not a known problem — showing "go research this
        // yourself" for it in the headline attention block is noise, not a finding.
        var device = Device("Gizemli Kart", vendorId: "FFFF", deviceId: "0001");
        var result = SingleDeviceResult(device, SupportLevel.Unknown, level: MatchLevel.None, verdict: Verdict.InsufficientData);

        var html = new ReportGenerator().Generate(MinimalProfile(), result);

        Assert.DoesNotContain(ReportStrings.AttentionSectionTitle, html);
        Assert.Contains("Gizemli Kart", html); // still visible in the folded technical breakdown
    }

    [Fact]
    public void Generate_AttentionWorthyDeviceWithNoEntry_FallsBackToTheGenericNote()
    {
        // Defensive path in ReportGenerator itself: CompatibilityMatcher never actually produces
        // a non-Unknown support level with a null Entry, but the renderer must not throw or show
        // a raw null if that invariant is ever violated.
        var device = Device("Edge Case Card", vendorId: "1234", deviceId: "5678");
        var match = new CompatibilityMatch { Level = MatchLevel.Exact, Entry = null, Support = SupportLevel.Proprietary };
        var result = new AnalysisResult
        {
            Devices = [new DeviceAssessment { Device = device, Match = match }],
            Verdict = Verdict.NeedsAttention,
            SystemConstraints = [],
            DevicesRemovedForRelevance = 0,
            DevicesRemovedForDuplication = 0,
            SoftwareAssessments = [],
            SoftwareSkippedForCategory = 0,
            DistributionRecommendation = TestDistributionRecommendation.Default()
        };

        var html = new ReportGenerator().Generate(MinimalProfile(), result);

        Assert.Contains(ReportStrings.AttentionSectionTitle, html);
        Assert.Contains(ReportStrings.NoNotesAvailable, html);
    }

    [Theory]
    [InlineData(Verdict.Ready, "verdict-good")]
    [InlineData(Verdict.MinorIssues, "verdict-warn")]
    [InlineData(Verdict.NeedsAttention, "verdict-attention")]
    [InlineData(Verdict.Blocked, "verdict-bad")]
    [InlineData(Verdict.InsufficientData, "verdict-unknown")]
    public void Generate_UsesTheExpectedColorClassPerVerdict(Verdict verdict, string expectedClass)
    {
        var result = new AnalysisResult
        {
            Devices = [], Verdict = verdict, SystemConstraints = [], DevicesRemovedForRelevance = 0, DevicesRemovedForDuplication = 0,
            SoftwareAssessments = [], SoftwareSkippedForCategory = 0,
            DistributionRecommendation = TestDistributionRecommendation.Default()
        };

        var html = new ReportGenerator().Generate(MinimalProfile(), result);

        Assert.Contains(expectedClass, html);
        Assert.Contains(ReportStrings.VerdictHeadline(verdict), html);
    }

    [Fact]
    public void Generate_TechnicalDetailsAreFoldedByDefault()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        // <details> must NOT carry the `open` attribute — folded away until the reader clicks it.
        Assert.Matches(@"<details>\s*<summary>", html);
        Assert.DoesNotContain("<details open>", html);
    }

    [Fact]
    public void Generate_TechnicalDetails_ReportsRemovedNoiseDeviceCounts()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.Contains(ReportStrings.NoiseRemovedNote(3, 2), html);
    }

    [Fact]
    public void Generate_EscapesHtmlSpecialCharactersInDeviceNamesAndNotes()
    {
        var device = Device("Card <Special> & \"Weird\"", vendorId: "10DE", deviceId: "2504");
        var entry = Entry(SupportLevel.Proprietary, notes: "Not <b>bold değil</b> & tırnak \" testi.");
        var result = SingleDeviceResult(device, SupportLevel.Proprietary, entry: entry, verdict: Verdict.NeedsAttention);

        var html = new ReportGenerator().Generate(MinimalProfile(), result);

        Assert.DoesNotContain("<Special>", html);
        Assert.Contains("&lt;Special&gt;", html);
        Assert.DoesNotContain("<b>bold değil</b>", html);
    }

    [Fact]
    public void Generate_SystemSummary_IncludesCpuMemoryDiskBootModeAndTpm()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.Contains("AMD Ryzen 5 5600X", html);
        Assert.Contains("6 çekirdek", html);
        Assert.Contains("32", html); // 34_359_738_368 bytes ~ 32 GB
        Assert.Contains("Test SSD", html);
        Assert.Contains("UEFI", html);
        Assert.Contains("TPM 2.0", html);
    }

    [Fact]
    public void Generate_WhenFieldsAreNull_FallsBackToUnknownRatherThanThrowing()
    {
        var profile = MinimalProfile(chassisType: null, bootMode: null, tpmPresent: null, tpmSpecVersion: null);

        var html = new ReportGenerator().Generate(profile, SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.Contains(ReportStrings.Unknown, html);
    }

    [Fact]
    public void Generate_AdminProfileWithEverythingReadable_OmitsTheElevationNote()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.DoesNotContain(ReportStrings.ElevationNote, html);
    }

    [Fact]
    public void Generate_UnreadableTpm_ShowsTheElevationNote()
    {
        var profile = MinimalProfile(tpmPresent: null, tpmSpecVersion: null);

        var html = new ReportGenerator().Generate(profile, SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.Contains(ReportStrings.ElevationNote, html);
    }

    [Fact]
    public void Generate_UnreadableBitlockerAvailability_ShowsTheElevationNoteEvenWhenTpmIsReadable()
    {
        var profile = MinimalProfile(bitlockerAvailable: null);

        var html = new ReportGenerator().Generate(profile, SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.Contains(ReportStrings.ElevationNote, html);
    }

    [Fact]
    public void Generate_TitleIncludesManufacturerModelAndHostname()
    {
        var html = new ReportGenerator().Generate(MinimalProfile(), SingleDeviceResult(Device("GPU"), SupportLevel.Native));

        Assert.Contains("Contoso Latitude 9999 (TEST-PC)", html);
    }
}
