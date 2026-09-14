using Scout.Analyzer.Compatibility;
using Xunit;

namespace Scout.Tests.Analyzer;

public class SoftwareCompatibilityDatabaseTests
{
    [Fact]
    public void LoadEmbedded_LoadsTheRealSeedDatabaseWithoutError()
    {
        var database = SoftwareCompatibilityDatabase.LoadEmbedded();

        Assert.NotEmpty(database.Entries);
    }

    [Fact]
    public void LoadEmbedded_HasAtLeast60Entries()
    {
        // Not a hard schema requirement — just a sanity check catching an accidental truncation
        // or a broken write, not a ceiling on legitimate growth as real machines turn up more
        // confirmed-unknown programs worth adding.
        var count = SoftwareCompatibilityDatabase.LoadEmbedded().Entries.Count;

        Assert.True(count >= 60, $"Expected at least 60 entries, found {count}.");
    }

    private static bool AnyAliasContains(SoftwareCompatibilityEntry entry, string substring) =>
        entry.Match.NameAliases.Any(a => a.Pattern.Contains(substring, StringComparison.Ordinal));

    [Fact]
    public void LoadEmbedded_ContainsTheExplicitlyRequestedSeedEntries()
    {
        var entries = SoftwareCompatibilityDatabase.LoadEmbedded().Entries;

        Assert.Contains(entries, e => AnyAliasContains(e, "Chrome") && e.Status == SoftwareCompatibilityStatus.Native);
        Assert.Contains(entries, e => AnyAliasContains(e, "Photoshop") && e.Status == SoftwareCompatibilityStatus.Blocked);
        Assert.Contains(entries, e => AnyAliasContains(e, "Office") && e.Status == SoftwareCompatibilityStatus.Equivalent);
        Assert.Contains(entries, e => AnyAliasContains(e, "Steam") && e.Status == SoftwareCompatibilityStatus.Native);
    }

    [Fact]
    public void LoadEmbedded_EveryEntryHasARegistryKeyOrAtLeastOneNameAlias()
    {
        // A row that matches nothing at all would be a dead entry — every row must offer some way
        // to actually be found.
        var entries = SoftwareCompatibilityDatabase.LoadEmbedded().Entries;

        foreach (var entry in entries)
        {
            Assert.True(
                entry.Match.RegistryKey is not null || entry.Match.NameAliases.Count > 0,
                "An entry has neither a registry_key nor any name_aliases.");
        }
    }

    [Fact]
    public void LoadEmbedded_TheOfficeRegistryKeyHeuristicIsPresent()
    {
        // Confirmed by convention, not by our own machine (Office is not installed there) — see
        // docs/schema/software-compatibility-v0.1.md for the honest caveat.
        var entries = SoftwareCompatibilityDatabase.LoadEmbedded().Entries;

        Assert.Contains(entries, e => AnyAliasContains(e, "Microsoft Office") && e.Match.RegistryKey is not null);
    }

    [Fact]
    public void LoadEmbedded_BuildToolsCarriesItsConfirmedTurkishAliasOnTheSameEntry()
    {
        var entries = SoftwareCompatibilityDatabase.LoadEmbedded().Entries;

        var buildTools = Assert.Single(entries, e => AnyAliasContains(e, "Visual Studio Build Tools"));
        Assert.Contains(buildTools.Match.NameAliases, a => a.Language == "tr");
    }

    [Fact]
    public void LoadEmbedded_NeverAssignsUnknownStatusToASeedEntry()
    {
        // Unknown only ever exists as SoftwareMatcher's own "nothing matched" fallback — the
        // database itself must never claim it as a real, curated answer for a program.
        var entries = SoftwareCompatibilityDatabase.LoadEmbedded().Entries;

        Assert.DoesNotContain(entries, e => e.Status == SoftwareCompatibilityStatus.Unknown);
    }

    [Fact]
    public void LoadEmbedded_EveryAlternativeHasANameAndANote()
    {
        var entries = SoftwareCompatibilityDatabase.LoadEmbedded().Entries;

        foreach (var entry in entries)
        {
            foreach (var alternative in entry.Alternatives)
            {
                Assert.False(string.IsNullOrWhiteSpace(alternative.Name));
                Assert.False(string.IsNullOrWhiteSpace(alternative.Note));
            }
        }
    }

    [Fact]
    public void FromJson_ParsesMinimalEntry()
    {
        const string json = """
            [{
              "match": { "name_aliases": [{ "pattern": "Test App", "match_type": "contains" }] },
              "status": "blocked",
              "notes": "n/a",
              "importance": "minor"
            }]
            """;

        var database = SoftwareCompatibilityDatabase.FromJson(json);

        var entry = Assert.Single(database.Entries);
        var alias = Assert.Single(entry.Match.NameAliases);
        Assert.Equal("Test App", alias.Pattern);
        Assert.Equal(SoftwareMatchType.Contains, alias.MatchType);
        Assert.Null(alias.Language);
        Assert.Null(entry.Match.RegistryKey);
        Assert.Null(entry.Match.PublisherPattern);
        Assert.Equal(SoftwareCompatibilityStatus.Blocked, entry.Status);
        Assert.Equal(SoftwareImportance.Minor, entry.Importance);
        Assert.Empty(entry.Alternatives);
    }

    [Theory]
    [InlineData("built_in", SoftwareCompatibilityStatus.BuiltIn)]
    [InlineData("partial", SoftwareCompatibilityStatus.Partial)]
    public void FromJson_ParsesTheNewStatusValuesAsSnakeCase(string jsonValue, SoftwareCompatibilityStatus expected)
    {
        var json = $$"""
            [{
              "match": { "name_aliases": [{ "pattern": "Test App", "match_type": "contains" }] },
              "status": "{{jsonValue}}",
              "notes": "n/a",
              "importance": "minor"
            }]
            """;

        var entry = Assert.Single(SoftwareCompatibilityDatabase.FromJson(json).Entries);

        Assert.Equal(expected, entry.Status);
    }

    [Fact]
    public void FromJson_ParsesRegistryKeyAndMultipleAliases()
    {
        const string json = """
            [{
              "match": {
                "registry_key": { "pattern": "FF1CE", "match_type": "contains" },
                "name_aliases": [
                  { "pattern": "Test App", "match_type": "contains" },
                  { "pattern": "Test Uygulaması", "match_type": "contains", "language": "tr" }
                ],
                "publisher_pattern": "Contoso"
              },
              "status": "equivalent",
              "notes": "n/a",
              "importance": "normal"
            }]
            """;

        var entry = Assert.Single(SoftwareCompatibilityDatabase.FromJson(json).Entries);

        Assert.Equal("FF1CE", entry.Match.RegistryKey!.Pattern);
        Assert.Equal(2, entry.Match.NameAliases.Count);
        Assert.Null(entry.Match.NameAliases[0].Language);
        Assert.Equal("tr", entry.Match.NameAliases[1].Language);
        Assert.Equal("Contoso", entry.Match.PublisherPattern);
    }
}
