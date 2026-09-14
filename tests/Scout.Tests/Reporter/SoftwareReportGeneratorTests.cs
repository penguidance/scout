using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Models;
using Scout.Reporter;
using Xunit;

namespace Scout.Tests.Reporter;

/// <summary>
/// Report rendering for the software analysis blocks — separate from
/// <see cref="ReportGeneratorTests"/> since these exercise a different axis (installed programs,
/// not hardware) with its own three-tier design (problems table / equivalents table+summary /
/// folded full inventory).
/// </summary>
public class SoftwareReportGeneratorTests
{
    private static MachineProfile ProfileWithSoftware(IReadOnlyList<SoftwareEntry> software, int? filteredCount = 0) => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "0.1.0",
        CollectedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        MachineId = "sha256:test",
        Privacy = new PrivacyInfo { HostnameIncluded = false, UsernameIncluded = false, SerialNumbersIncluded = false, RedactedFields = [] },
        System = new SystemInfo { Manufacturer = "Contoso", Model = "Latitude 9999", ChassisType = ChassisType.Desktop },
        Firmware = new FirmwareInfo { BiosVendor = "Contoso", BiosVersion = "1.0", BootMode = BootMode.UEFI, Tpm = new TpmInfo { Present = false } },
        Cpu = new CpuInfo { Vendor = "Test", Model = "Test", PhysicalCores = 1, LogicalProcessors = 1, Architecture = CpuArchitecture.x86_64 },
        Memory = new MemoryInfo { TotalBytes = null, Modules = [] },
        Storage = new StorageInfo { Disks = [], BitlockerAvailable = null },
        Devices = [],
        GpuTopology = new GpuTopology { Gpus = [], Layout = null },
        Os = new OsInfo { Edition = "Windows 11 Pro", Version = "10.0.26200", Build = "26200", Architecture = CpuArchitecture.x86_64 },
        Software = software,
        SoftwareFilteredCount = filteredCount,
        Peripherals = [],
        CollectionErrors = []
    };

    private static SoftwareEntry Software(string name, string? publisher = null, SoftwareCategory category = SoftwareCategory.application) => new()
    {
        Name = name,
        Publisher = publisher,
        Source = SoftwareSource.HklmUninstallKey,
        Category = category,
        RegistryView = RegistryView.Native,
        SystemComponent = false
    };

    private static SoftwareCompatibilityEntry DbEntry(
        SoftwareCompatibilityStatus status,
        string notes = "test notes",
        SoftwareImportance importance = SoftwareImportance.Normal,
        params (string Name, string Note)[] alternatives) => new()
    {
        Match = new SoftwareMatchRule
        {
            NameAliases = [new SoftwareNameAlias { Pattern = "test", MatchType = SoftwareMatchType.Contains }]
        },
        Status = status,
        Alternatives = alternatives.Select(a => new SoftwareAlternative { Name = a.Name, Note = LocalizedText.FromEnglish(a.Note) }).ToList(),
        Notes = LocalizedText.FromEnglish(notes),
        Importance = importance
    };

    private static SoftwareAssessment Assessed(
        SoftwareEntry software,
        SoftwareCompatibilityEntry? entry,
        SoftwareMatchLevel level = SoftwareMatchLevel.Contains,
        SoftwareMatchSignal signal = SoftwareMatchSignal.Name) =>
        new()
        {
            Software = software,
            Match = new SoftwareMatch
            {
                Level = entry is null ? SoftwareMatchLevel.None : level,
                Entry = entry,
                Status = entry?.Status ?? SoftwareCompatibilityStatus.Unknown,
                Signals = entry is null ? [] : [signal]
            }
        };

    private static AnalysisResult Result(IReadOnlyList<SoftwareAssessment> softwareAssessments, int softwareSkipped = 0) => new()
    {
        Devices = [],
        Verdict = Verdict.Ready,
        SystemConstraints = [],
        DevicesRemovedForRelevance = 0,
        DevicesRemovedForDuplication = 0,
        SoftwareAssessments = softwareAssessments,
        SoftwareSkippedForCategory = softwareSkipped,
        DistributionRecommendation = TestDistributionRecommendation.Default()
    };

    // ---- Problems section --------------------------------------------------------------

    [Fact]
    public void Generate_BlockedSoftware_AppearsInProblemsSectionWithAlternative()
    {
        var photoshop = Software("Adobe Photoshop 2024");
        var entry = DbEntry(SoftwareCompatibilityStatus.Blocked, notes: "Photoshop'un Linux sürümü yok.", importance: SoftwareImportance.Normal, ("GIMP", "Ücretsiz."));
        var analysis = Result([Assessed(photoshop, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([photoshop]), analysis);

        Assert.Contains(ReportStrings.SoftwareProblemsSectionTitle, html);
        Assert.Contains("Adobe Photoshop 2024", html);
        Assert.Contains("Photoshop'un Linux sürümü yok.", html);
        Assert.Contains("GIMP", html);
    }

    [Fact]
    public void Generate_WineSoftware_AppearsInProblemsSectionNotEquivalentsSection()
    {
        var battleNet = Software("Battle.net");
        var entry = DbEntry(SoftwareCompatibilityStatus.Wine, notes: "Wine/Lutris ile çalışır.");
        var analysis = Result([Assessed(battleNet, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([battleNet]), analysis);

        Assert.Contains(ReportStrings.SoftwareProblemsSectionTitle, html);
        Assert.DoesNotContain(ReportStrings.SoftwareEquivalentsSectionTitle, html);
    }

    [Fact]
    public void Generate_BlockedSoftwareWithNoAlternatives_ShowsTheGenericFallback()
    {
        var software = Software("Obscure Blocked Thing");
        var entry = DbEntry(SoftwareCompatibilityStatus.Blocked, notes: "n/a");
        var analysis = Result([Assessed(software, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([software]), analysis);

        Assert.Contains(ReportStrings.SoftwareNoKnownAlternative, html);
    }

    [Fact]
    public void Generate_NoProblemSoftware_OmitsTheSectionEntirely()
    {
        var chrome = Software("Google Chrome");
        var entry = DbEntry(SoftwareCompatibilityStatus.Native);
        var analysis = Result([Assessed(chrome, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([chrome]), analysis);

        Assert.DoesNotContain(ReportStrings.SoftwareProblemsSectionTitle, html);
    }

    [Fact]
    public void Generate_PartialSoftware_AppearsInProblemsSectionWithFeatureLossNote()
    {
        var steelSeries = Software("SteelSeries GG");
        var entry = DbEntry(SoftwareCompatibilityStatus.Partial, notes: "Klavye/fare çalışır, RGB/makro yapılandırması yok.");
        var analysis = Result([Assessed(steelSeries, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([steelSeries]), analysis);

        Assert.Contains(ReportStrings.SoftwareProblemsSectionTitle, html);
        Assert.Contains("SteelSeries GG", html);
        Assert.Contains("Klavye/fare çalışır, RGB/makro yapılandırması yok.", html);
        Assert.Contains(ReportStrings.SoftwareStatusLabel(SoftwareCompatibilityStatus.Partial), html);
    }

    [Fact]
    public void Generate_PartialSoftwareWithNoAlternatives_ShowsThePartialSpecificFallback()
    {
        var software = Software("Some Partial Tool");
        var entry = DbEntry(SoftwareCompatibilityStatus.Partial, notes: "n/a");
        var analysis = Result([Assessed(software, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([software]), analysis);

        Assert.Contains(ReportStrings.SoftwarePartialNoAlternativeNeeded, html);
    }

    [Fact]
    public void Generate_BuiltInSoftware_NeverAppearsInTheProblemsSection()
    {
        var msys2 = Software("MSYS2");
        var entry = DbEntry(SoftwareCompatibilityStatus.BuiltIn, notes: "Linux'ta zaten terminal ve paket yöneticisi var.");
        var analysis = Result([Assessed(msys2, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([msys2]), analysis);

        Assert.DoesNotContain(ReportStrings.SoftwareProblemsSectionTitle, html);
    }

    // ---- Equivalents + native-summary section --------------------------------------------

    [Fact]
    public void Generate_EquivalentSoftware_AppearsInEquivalentsTableWithAlternativeNames()
    {
        var office = Software("Microsoft Office Professional Plus 2019");
        var entry = DbEntry(SoftwareCompatibilityStatus.Equivalent, "test notes", SoftwareImportance.Normal, ("LibreOffice", "Ücretsiz."), ("OnlyOffice", "Ücretsiz."));
        var analysis = Result([Assessed(office, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([office]), analysis);

        Assert.Contains(ReportStrings.SoftwareEquivalentsSectionTitle, html);
        Assert.Contains("Microsoft Office Professional Plus 2019", html);
        Assert.Contains("LibreOffice", html);
        Assert.Contains("OnlyOffice", html);
    }

    [Fact]
    public void Generate_WebSoftware_AppearsInEquivalentsTableWithBrowserNote()
    {
        var teams = Software("Microsoft Teams");
        var entry = DbEntry(SoftwareCompatibilityStatus.Web, notes: "Tarayıcıdan kullan.");
        var analysis = Result([Assessed(teams, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([teams]), analysis);

        Assert.Contains(ReportStrings.SoftwareEquivalentsSectionTitle, html);
        Assert.Contains("Microsoft Teams", html);
        Assert.Contains(ReportStrings.SoftwareWebAlternativeLabel, html);
    }

    [Fact]
    public void Generate_NativeSoftware_ProducesTheOneLineSummaryNotATable()
    {
        var chrome = Software("Google Chrome");
        var vlc = Software("VLC media player");
        var entry = DbEntry(SoftwareCompatibilityStatus.Native);
        var analysis = Result([Assessed(chrome, entry), Assessed(vlc, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([chrome, vlc]), analysis);

        Assert.Contains(ReportStrings.SoftwareEquivalentsSectionTitle, html);
        Assert.Contains("Linux'ta doğrudan çalışıyor.", html);
        Assert.Contains("Google Chrome", html);
        Assert.Contains("VLC media player", html);
    }

    [Fact]
    public void Generate_ManyNativePrograms_SummarizesWithACountInsteadOfListingAll()
    {
        var names = Enumerable.Range(1, 34).Select(i => $"App {i}").ToList();
        var softwareList = names.Select(n => Software(n)).ToList();
        var entry = DbEntry(SoftwareCompatibilityStatus.Native);
        var analysis = Result(softwareList.Select(s => Assessed(s, entry)).ToList());

        var html = new ReportGenerator().Generate(ProfileWithSoftware(softwareList), analysis);

        // Only the first three (alphabetically first by construction) plus a "ve N program" tail —
        // not all 34 names dumped into the summary sentence.
        Assert.Contains("ve 31 program", html);
        Assert.DoesNotContain("App 34", html.Split("Tam Yazılım Envanteri")[0]); // not before the folded dump
    }

    [Fact]
    public void Generate_BuiltInSoftware_ProducesItsOwnOneLineSummaryDistinctFromNative()
    {
        var msys2 = Software("MSYS2");
        var entry = DbEntry(SoftwareCompatibilityStatus.BuiltIn);
        var analysis = Result([Assessed(msys2, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([msys2]), analysis);

        Assert.Contains(ReportStrings.SoftwareEquivalentsSectionTitle, html);
        Assert.Contains("MSYS2", html);
        Assert.Contains("muadil aramaya gerek yok", html);
        Assert.DoesNotContain("Linux'ta doğrudan çalışıyor.", html); // that is Native's wording, not BuiltIn's
    }

    [Fact]
    public void Generate_NativeAndBuiltInTogether_ProduceTwoSeparateSummarySentences()
    {
        var chrome = Software("Google Chrome");
        var msys2 = Software("MSYS2");
        var analysis = Result([
            Assessed(chrome, DbEntry(SoftwareCompatibilityStatus.Native)),
            Assessed(msys2, DbEntry(SoftwareCompatibilityStatus.BuiltIn))
        ]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([chrome, msys2]), analysis);

        Assert.Contains("Google Chrome", html);
        Assert.Contains("Linux'ta doğrudan çalışıyor.", html);
        Assert.Contains("MSYS2", html);
        Assert.Contains("muadil aramaya gerek yok", html);
    }

    [Fact]
    public void Generate_UnknownSoftware_DoesNotAppearInEitherShortSection()
    {
        var mystery = Software("Some Home-Grown Tool Nobody Recognizes");
        var analysis = Result([Assessed(mystery, entry: null)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([mystery]), analysis);

        Assert.DoesNotContain(ReportStrings.SoftwareProblemsSectionTitle, html);
        Assert.DoesNotContain(ReportStrings.SoftwareEquivalentsSectionTitle, html);
        // Still visible in the folded full inventory.
        Assert.Contains("Some Home-Grown Tool Nobody Recognizes", html);
    }

    [Fact]
    public void Generate_NoSoftwareAtAll_OmitsAllSoftwareSectionsIncludingTheFoldedDump()
    {
        var html = new ReportGenerator().Generate(ProfileWithSoftware([]), Result([]));

        Assert.DoesNotContain(ReportStrings.SoftwareProblemsSectionTitle, html);
        Assert.DoesNotContain(ReportStrings.SoftwareEquivalentsSectionTitle, html);
        Assert.DoesNotContain(ReportStrings.SoftwareTechnicalDetailsSummary, html);
    }

    // ---- Folded full inventory ------------------------------------------------------------

    [Fact]
    public void Generate_FoldedInventory_IncludesRuntimeDriverSystemEntriesNeverAssessed()
    {
        var redistributable = Software("Microsoft Visual C++ 2015-2022 Redistributable (x64)", category: SoftwareCategory.runtime);
        var analysis = Result([], softwareSkipped: 1);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([redistributable], filteredCount: 5), analysis);

        Assert.Contains(ReportStrings.SoftwareTechnicalDetailsSummary, html);
        Assert.Contains("Microsoft Visual C++ 2015-2022 Redistributable (x64)", html);
        Assert.Contains(ReportStrings.SoftwareNotAssessed, html);
        Assert.Contains(ReportStrings.SoftwareCategoryLabel(SoftwareCategory.runtime), html);
    }

    [Theory]
    [InlineData(SoftwareMatchSignal.RegistryKey)]
    [InlineData(SoftwareMatchSignal.Name)]
    [InlineData(SoftwareMatchSignal.Alias)]
    public void Generate_FoldedInventory_ShowsWhichSignalIdentifiedTheProgram(SoftwareMatchSignal signal)
    {
        var software = Software("Some App");
        var analysis = Result([Assessed(software, DbEntry(SoftwareCompatibilityStatus.Native), signal: signal)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([software]), analysis);

        Assert.Contains(ReportStrings.SoftwareMatchSignalLabel(signal), html);
    }

    [Fact]
    public void Generate_FoldedInventory_CombinedSignals_ShowsBoth()
    {
        var software = Software("Origin", publisher: "Electronic Arts");
        var entry = DbEntry(SoftwareCompatibilityStatus.Wine);
        var assessment = new SoftwareAssessment
        {
            Software = software,
            Match = new SoftwareMatch
            {
                Level = SoftwareMatchLevel.Exact,
                Entry = entry,
                Status = entry.Status,
                Signals = [SoftwareMatchSignal.Name, SoftwareMatchSignal.Publisher]
            }
        };

        var html = new ReportGenerator().Generate(ProfileWithSoftware([software]), Result([assessment]));

        Assert.Contains(
            ReportStrings.SoftwareMatchSignalsSummary([SoftwareMatchSignal.Name, SoftwareMatchSignal.Publisher]),
            html);
    }

    [Fact]
    public void Generate_FoldedInventory_IsFoldedByDefault()
    {
        var software = Software("Some App");
        var entry = DbEntry(SoftwareCompatibilityStatus.Native);
        var analysis = Result([Assessed(software, entry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([software]), analysis);

        var section = html.Split(ReportStrings.SoftwareTechnicalDetailsSummary)[0];
        Assert.DoesNotContain("<details open>", html);
        Assert.NotEmpty(section); // sanity: the split actually found the section
    }

    [Fact]
    public void Generate_FoldedInventory_ReportsFilteredAndSkippedCounts()
    {
        var software = Software("Some App");
        var analysis = Result([Assessed(software, DbEntry(SoftwareCompatibilityStatus.Native))], softwareSkipped: 2);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([software], filteredCount: 7), analysis);

        Assert.Contains(ReportStrings.SoftwareNoiseRemovedNote(7, 2), html);
    }

    [Fact]
    public void Generate_FoldedInventory_PutsKnownStatusProgramsBeforeUnknownOnesRegardlessOfAlphabeticalOrder()
    {
        // "Aardvark Tool" would sort first alphabetically, but it is Unknown — a program with a
        // real answer (even one starting with "Z") must still appear before it.
        var aardvark = Software("Aardvark Tool");
        var zChrome = Software("Zzz Chrome-like Thing");
        var nativeEntry = DbEntry(SoftwareCompatibilityStatus.Native);
        var analysis = Result([Assessed(aardvark, entry: null), Assessed(zChrome, nativeEntry)]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([aardvark, zChrome]), analysis);

        var dump = html.Split(ReportStrings.SoftwareTechnicalDetailsSummary)[1];
        Assert.True(dump.IndexOf("Zzz Chrome-like Thing", StringComparison.Ordinal) < dump.IndexOf("Aardvark Tool", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_FoldedInventory_ShowsTheUnknownGroupIntroOnlyOnceRightBeforeUnknownRows()
    {
        var known = Software("Google Chrome");
        var unknown1 = Software("Aardvark Tool");
        var unknown2 = Software("Zzz Mystery Tool");
        var analysis = Result([
            Assessed(known, DbEntry(SoftwareCompatibilityStatus.Native)),
            Assessed(unknown1, entry: null),
            Assessed(unknown2, entry: null)
        ]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([known, unknown1, unknown2]), analysis);

        var dump = html.Split(ReportStrings.SoftwareTechnicalDetailsSummary)[1];
        var introIndex = dump.IndexOf(ReportStrings.SoftwareUnknownGroupIntro, StringComparison.Ordinal);
        Assert.True(introIndex >= 0);
        Assert.True(introIndex < dump.IndexOf("Aardvark Tool", StringComparison.Ordinal));
        Assert.True(introIndex < dump.IndexOf("Zzz Mystery Tool", StringComparison.Ordinal));
        Assert.True(introIndex > dump.IndexOf("Google Chrome", StringComparison.Ordinal));
        // Shown once, not once per unknown row.
        Assert.Equal(1, CountOccurrences(dump, ReportStrings.SoftwareUnknownGroupIntro));
    }

    [Fact]
    public void Generate_FoldedInventory_NoUnknownEntries_NeverShowsTheIntroLine()
    {
        var known = Software("Google Chrome");
        var analysis = Result([Assessed(known, DbEntry(SoftwareCompatibilityStatus.Native))]);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([known]), analysis);

        Assert.DoesNotContain(ReportStrings.SoftwareUnknownGroupIntro, html);
    }

    [Fact]
    public void Generate_FoldedInventory_SkippedCategoryRowsCountAsKnownNotUnknown()
    {
        // A runtime/driver/system entry was never assessed at all — that is a different, more
        // informative situation than a genuine database miss, so it must not be lumped in with
        // (or sorted after) the Unknown group.
        var redistributable = Software("Microsoft Visual C++ 2015-2022 Redistributable (x64)", category: SoftwareCategory.runtime);
        var unknown = Software("Aardvark Mystery Tool");
        var analysis = Result([Assessed(unknown, entry: null)], softwareSkipped: 1);

        var html = new ReportGenerator().Generate(ProfileWithSoftware([redistributable, unknown]), analysis);

        var dump = html.Split(ReportStrings.SoftwareTechnicalDetailsSummary)[1];
        var introIndex = dump.IndexOf(ReportStrings.SoftwareUnknownGroupIntro, StringComparison.Ordinal);
        Assert.True(introIndex < dump.IndexOf("Aardvark Mystery Tool", StringComparison.Ordinal));
        Assert.True(introIndex > dump.IndexOf("Microsoft Visual C++ 2015-2022 Redistributable (x64)", StringComparison.Ordinal));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
