using Scout.Analyzer.Recommendations;
using Xunit;

namespace Scout.Tests.Analyzer;

public class DistributionDatabaseTests
{
    [Fact]
    public void LoadEmbedded_LoadsTheRealSeedDatabaseWithoutError()
    {
        var database = DistributionDatabase.LoadEmbedded();

        Assert.NotEmpty(database.Entries);
    }

    [Fact]
    public void LoadEmbedded_HasBetween5And6Entries()
    {
        var count = DistributionDatabase.LoadEmbedded().Entries.Count;

        Assert.InRange(count, 5, 6);
    }

    [Fact]
    public void LoadEmbedded_ContainsTheRequestedDistributions()
    {
        var names = DistributionDatabase.LoadEmbedded().Entries.Select(e => e.Name).ToList();

        Assert.Contains("Linux Mint", names);
        Assert.Contains("Ubuntu", names);
        Assert.Contains("Fedora", names);
        Assert.Contains("Debian", names);
        Assert.Contains(names, n => n is "Xubuntu" or "Lubuntu");
    }

    [Fact]
    public void LoadEmbedded_HasExactlyOneDefaultChoice()
    {
        var entries = DistributionDatabase.LoadEmbedded().Entries;

        Assert.Single(entries, e => e.IsDefaultChoice);
    }

    [Fact]
    public void LoadEmbedded_HasAtLeastOneLightweightOption()
    {
        var entries = DistributionDatabase.LoadEmbedded().Entries;

        Assert.Contains(entries, e => e.Lightweight);
    }

    [Fact]
    public void LoadEmbedded_EveryEntryHasARealDownloadUrl()
    {
        var entries = DistributionDatabase.LoadEmbedded().Entries;

        foreach (var entry in entries)
        {
            Assert.StartsWith("https://", entry.DownloadUrl);
        }
    }

    [Fact]
    public void FromJson_ParsesMinimalEntry()
    {
        const string json = """
            [{
              "name": "Test Distro",
              "version": "1.0",
              "download_url": "https://example.invalid/download",
              "desktop_environment": "TestDE",
              "minimum_feature_level": "v1",
              "offers_proprietary_driver_installer": false,
              "lightweight": false,
              "is_default_choice": false,
              "recommended_when": "n/a"
            }]
            """;

        var entry = Assert.Single(DistributionDatabase.FromJson(json).Entries);

        Assert.Equal("Test Distro", entry.Name);
        Assert.Null(entry.MinimumMemoryBytes);
        Assert.Null(entry.MinimumDiskBytes);
    }
}
