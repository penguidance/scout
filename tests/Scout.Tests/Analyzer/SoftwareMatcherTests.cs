using Scout.Analyzer.Compatibility;
using Xunit;

namespace Scout.Tests.Analyzer;

public class SoftwareMatcherTests
{
    private const string Database = """
        [
          {
            "match": { "name_aliases": [{ "pattern": "Adobe Photoshop", "match_type": "contains" }] },
            "status": "blocked",
            "alternatives": [{ "name": "GIMP", "note": "Ücretsiz." }],
            "notes": "Photoshop'un Linux sürümü yok.",
            "importance": "critical"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Google Chrome", "match_type": "contains" }], "publisher_pattern": "Google" },
            "status": "native",
            "notes": "Linux sürümü var.",
            "importance": "normal"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Origin", "match_type": "exact" }], "publisher_pattern": "Electronic Arts" },
            "status": "wine",
            "notes": "Wine ile çalışır.",
            "importance": "minor"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Visual Studio", "match_type": "contains" }] },
            "status": "equivalent",
            "alternatives": [{ "name": "JetBrains Rider", "note": "Ücretli." }],
            "notes": "Tam IDE'nin Linux sürümü yok.",
            "importance": "normal"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Visual Studio Code", "match_type": "contains" }] },
            "status": "native",
            "notes": "Linux sürümü var.",
            "importance": "normal"
          },
          {
            "match": {
              "registry_key": { "pattern": "CONTOSO-PRODCODE", "match_type": "contains" },
              "name_aliases": [
                { "pattern": "Contoso Suite", "match_type": "contains" },
                { "pattern": "Contoso Paketi", "match_type": "contains", "language": "tr" }
              ]
            },
            "status": "equivalent",
            "alternatives": [{ "name": "LibreContoso", "note": "n/a" }],
            "notes": "Contoso Suite'in Linux sürümü yok.",
            "importance": "normal"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Ambiguous Tool", "match_type": "contains" }] },
            "status": "blocked",
            "notes": "No-language-tag entry, listed first.",
            "importance": "minor"
          },
          {
            "match": { "name_aliases": [{ "pattern": "Ambiguous Tool", "match_type": "contains", "language": "tr" }] },
            "status": "native",
            "notes": "Turkish-tagged entry, listed second.",
            "importance": "minor"
          }
        ]
        """;

    private static SoftwareMatcher CreateMatcher() =>
        new(SoftwareCompatibilityDatabase.FromJson(Database));

    [Fact]
    public void Match_ContainsPattern_MatchesRegardlessOfSurroundingText()
    {
        var software = TestSoftware.Create("Adobe Photoshop 2021");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareMatchLevel.Contains, result.Level);
        Assert.Equal(SoftwareCompatibilityStatus.Blocked, result.Status);
        Assert.Equal("GIMP", result.Entry!.Alternatives[0].Name);
        Assert.Equal([SoftwareMatchSignal.Name], result.Signals);
    }

    [Fact]
    public void Match_YearSuffixDoesNotBreakTheMatch()
    {
        // The whole reason for prefix/contains matching over exact: a full product name changes
        // every release ("Adobe Photoshop 2021", "...2024", ...).
        var result2021 = CreateMatcher().Match(TestSoftware.Create("Adobe Photoshop 2021"));
        var result2024 = CreateMatcher().Match(TestSoftware.Create("Adobe Photoshop 2024"));

        Assert.Equal(SoftwareCompatibilityStatus.Blocked, result2021.Status);
        Assert.Equal(SoftwareCompatibilityStatus.Blocked, result2024.Status);
    }

    [Fact]
    public void Match_PublisherRequired_DoesNotMatchWhenPublisherDiffers()
    {
        // Plain "Origin" is an ordinary English word — the publisher check exists precisely to
        // stop it from matching unrelated software that happens to share the name.
        var unrelated = TestSoftware.Create("Origin", publisher: "Some Other Company");

        var result = CreateMatcher().Match(unrelated);

        Assert.Equal(SoftwareMatchLevel.None, result.Level);
        Assert.Equal(SoftwareCompatibilityStatus.Unknown, result.Status);
    }

    [Fact]
    public void Match_PublisherRequired_MatchesWhenPublisherAlsoSatisfiesTheRule()
    {
        var eaOrigin = TestSoftware.Create("Origin", publisher: "Electronic Arts");

        var result = CreateMatcher().Match(eaOrigin);

        Assert.Equal(SoftwareCompatibilityStatus.Wine, result.Status);
        // Publisher was required and satisfied, alongside the primary name pattern.
        Assert.Equal([SoftwareMatchSignal.Name, SoftwareMatchSignal.Publisher], result.Signals);
    }

    [Fact]
    public void Match_PublisherRequired_DoesNotMatchWhenPublisherIsUnknown()
    {
        // A null publisher can never satisfy a publisher requirement — "cannot confirm" is not
        // the same as "assume it matches".
        var noPublisher = TestSoftware.Create("Origin", publisher: null);

        var result = CreateMatcher().Match(noPublisher);

        Assert.Equal(SoftwareMatchLevel.None, result.Level);
    }

    [Fact]
    public void Match_LongerContainsPatternWinsOverAShorterOneThatAlsoMatches()
    {
        // "Microsoft Visual Studio Code" contains both "Visual Studio" and "Visual Studio Code" —
        // the more specific (longer) pattern must win regardless of database row order.
        var vscode = TestSoftware.Create("Microsoft Visual Studio Code");

        var result = CreateMatcher().Match(vscode);

        Assert.Equal(SoftwareCompatibilityStatus.Native, result.Status);
    }

    [Fact]
    public void Match_ShorterPatternStillWinsWhenTheLongerOneDoesNotApply()
    {
        var fullIde = TestSoftware.Create("Microsoft Visual Studio Professional 2022");

        var result = CreateMatcher().Match(fullIde);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.Equal("JetBrains Rider", result.Entry!.Alternatives[0].Name);
    }

    [Fact]
    public void Match_ExactType_RequiresTheWholeNameNotJustASubstring()
    {
        var justOrigin = TestSoftware.Create("Origin", publisher: "Electronic Arts");
        var somethingElse = TestSoftware.Create("Origin Launcher Updater", publisher: "Electronic Arts");

        Assert.Equal(SoftwareCompatibilityStatus.Wine, CreateMatcher().Match(justOrigin).Status);
        Assert.Equal(SoftwareMatchLevel.None, CreateMatcher().Match(somethingElse).Level);
    }

    [Fact]
    public void Match_NoEntryMatches_ReturnsUnknownNeverGuessed()
    {
        var software = TestSoftware.Create("Some Obscure Utility Nobody Has Heard Of");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareMatchLevel.None, result.Level);
        Assert.Equal(SoftwareCompatibilityStatus.Unknown, result.Status);
        Assert.Null(result.Entry);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void Match_IsCaseInsensitiveForBothNameAndPublisher()
    {
        var lowercase = TestSoftware.Create("google chrome", publisher: "google llc");

        var result = CreateMatcher().Match(lowercase);

        Assert.Equal(SoftwareCompatibilityStatus.Native, result.Status);
    }

    [Fact]
    public void Constructor_RequiresADatabase()
    {
        Assert.Throws<ArgumentNullException>(() => new SoftwareMatcher(null!));
    }

    [Fact]
    public void Match_ThrowsOnNullSoftware()
    {
        Assert.Throws<ArgumentNullException>(() => CreateMatcher().Match(null!));
    }

    // ---- Registry key: the most reliable signal, checked first, never re-translated ---------

    [Fact]
    public void Match_RegistryKey_IdentifiesTheProgramEvenWithACompletelyUnrecognizedLocalizedName()
    {
        // The whole point of this signal: DisplayName can be in any language and the registry key
        // still resolves it correctly.
        var software = TestSoftware.Create(
            "Une Application Complètement Inconnue", registryKeyName: "{XXXX-CONTOSO-PRODCODE-XXXX}");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.Equal("LibreContoso", result.Entry!.Alternatives[0].Name);
        Assert.Equal([SoftwareMatchSignal.RegistryKey], result.Signals);
    }

    [Fact]
    public void Match_RegistryKey_TakesPriorityOverANameThatWouldMatchADifferentEntry()
    {
        // Name alone says "Visual Studio" (which has its own, different database entry) — but the
        // registry key says this is actually Contoso Suite. The registry key must win.
        var software = TestSoftware.Create("Visual Studio", registryKeyName: "{XXXX-CONTOSO-PRODCODE-XXXX}");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.Equal("LibreContoso", result.Entry!.Alternatives[0].Name);
        Assert.Equal([SoftwareMatchSignal.RegistryKey], result.Signals);
    }

    [Fact]
    public void Match_NoRegistryKeyKnown_FallsBackToNameMatching()
    {
        var software = TestSoftware.Create("Adobe Photoshop 2024", registryKeyName: null);

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Blocked, result.Status);
        Assert.Equal([SoftwareMatchSignal.Name], result.Signals);
    }

    [Fact]
    public void Match_RegistryKeyPresentButMatchesNoEntry_FallsBackToNameMatching()
    {
        var software = TestSoftware.Create("Adobe Photoshop 2024", registryKeyName: "{SOME-UNRELATED-GUID}");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Blocked, result.Status);
        Assert.Equal([SoftwareMatchSignal.Name], result.Signals);
    }

    // ---- Name aliases: localized spellings, always tried regardless of language -------------

    [Fact]
    public void Match_PrimaryNameAlias_ReportsNameSignal()
    {
        var result = CreateMatcher().Match(TestSoftware.Create("Contoso Suite 2024"));

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.Equal([SoftwareMatchSignal.Name], result.Signals);
    }

    [Fact]
    public void Match_LocalizedAlias_ReportsAliasSignalNotName()
    {
        var result = CreateMatcher().Match(TestSoftware.Create("Contoso Paketi 2024"));

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.Equal([SoftwareMatchSignal.Alias], result.Signals);
    }

    [Fact]
    public void Match_LocalizedAlias_MatchesEvenWithNoProfileLanguageGiven()
    {
        // The critical guarantee: a missing language hint must never cause a real, localized
        // match to be skipped — every alias is always attempted.
        var result = CreateMatcher().Match(TestSoftware.Create("Contoso Paketi 2024"), profileLanguage: null);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
    }

    [Fact]
    public void Match_LocalizedAlias_MatchesEvenWithAWrongProfileLanguageGiven()
    {
        // Equally critical: a language hint that does not match the alias's tag must not gate
        // matching either — it is a tie-break preference only, never a filter.
        var result = CreateMatcher().Match(TestSoftware.Create("Contoso Paketi 2024"), profileLanguage: "de-DE");

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
    }

    [Fact]
    public void Match_AmbiguousPatternAcrossTwoEntries_WithNoLanguageHint_PrefersTheFirstListedEntry()
    {
        // Deterministic, not arbitrary: absent a language hint, ties resolve to database order.
        var result = CreateMatcher().Match(TestSoftware.Create("The Ambiguous Tool for Everyone"), profileLanguage: null);

        Assert.Equal(SoftwareCompatibilityStatus.Blocked, result.Status);
    }

    [Fact]
    public void Match_AmbiguousPatternAcrossTwoEntries_WithMatchingLanguageHint_PrefersTheTaggedEntry()
    {
        // Only a tie-break — the language-tagged alias is preferred purely because both candidates
        // are otherwise equally specific (same match type, same pattern length).
        var result = CreateMatcher().Match(TestSoftware.Create("The Ambiguous Tool for Everyone"), profileLanguage: "tr-TR");

        Assert.Equal(SoftwareCompatibilityStatus.Native, result.Status);
    }
}
