using Scout.Analyzer.Compatibility;
using Xunit;

namespace Scout.Tests.Analyzer;

/// <summary>
/// Behavior tests against the real, committed seed database (not a small fixture) — these lock in
/// specific, previously-wrong outcomes that were fixed by hand after being observed for real. See
/// <see cref="Analyzer.RealDatabaseBehaviorTests"/> for the hardware-side equivalent.
/// </summary>
public class RealSoftwareDatabaseBehaviorTests
{
    private static SoftwareMatcher CreateMatcher() => new(SoftwareCompatibilityDatabase.LoadEmbedded());

    [Fact]
    public void Match_VisualStudioBuildTools_GetsCompilerAlternativesNotAnIdeAlternative()
    {
        // The generic "Visual Studio" (contains) rule used to swallow this — Build Tools is a
        // compiler/toolchain, not an IDE, so suggesting JetBrains Rider for it was wrong.
        var software = TestSoftware.Create("Microsoft Visual Studio Build Tools 2022");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.DoesNotContain(result.Entry!.Alternatives, a => a.Name.Contains("Rider"));
        Assert.Contains(result.Entry.Alternatives, a => a.Name.Contains("SDK") || a.Name.Contains("GCC"));
    }

    [Fact]
    public void Match_VisualStudioInstaller_IsNotTreatedAsTheIde()
    {
        var software = TestSoftware.Create("Visual Studio Installer");

        var result = CreateMatcher().Match(software);

        Assert.DoesNotContain(result.Entry!.Alternatives, a => a.Name.Contains("Rider"));
        Assert.Contains("paket yöneticisi", result.Entry.Notes.Resolve("tr"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Match_TurkishLocalizedBuildTools_AlsoGetsCompilerAlternativesNotAnIdeAlternative()
    {
        // Confirmed against a real Turkish-locale Windows install: the registry DisplayName
        // itself is localized ("Visual Studio Derleme Araçları 2026"), not just a UI label — the
        // English-only pattern alone would miss it and let this fall through to the generic
        // "Visual Studio" rule, reproducing the exact bug the English alias was meant to fix.
        var software = TestSoftware.Create("Visual Studio Derleme Araçları 2026", publisher: "Microsoft Corporation");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.DoesNotContain(result.Entry!.Alternatives, a => a.Name.Contains("Rider"));
    }

    [Fact]
    public void Match_TurkishLocalizedBuildTools_ReportsAliasSignalNotName()
    {
        // The Turkish spelling lives as a secondary alias on the same entry (not its own database
        // row) — confirms it is correctly recognized as the non-primary alias.
        var software = TestSoftware.Create("Visual Studio Derleme Araçları 2019", publisher: "Microsoft Corporation");

        var result = CreateMatcher().Match(software, profileLanguage: "tr-TR");

        Assert.Contains(SoftwareMatchSignal.Alias, result.Signals);
    }

    [Fact]
    public void Match_OfficeByRegistryKey_ResolvesEvenWithATotallyUnrecognizedLocalizedName()
    {
        // Simulates what a non-English Windows install would actually give us: a DisplayName in
        // some other language we have no alias for, but the classic MSI ProductCode GUID still
        // ends in the well-known "FF1CE" convention regardless of display language.
        var software = TestSoftware.Create(
            "Ein völlig unbekannter Name", registryKeyName: "{90160000-008C-0409-1000-0000000FF1CE}");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.Contains(result.Entry!.Alternatives, a => a.Name == "LibreOffice");
        Assert.Equal([SoftwareMatchSignal.RegistryKey], result.Signals);
    }

    [Fact]
    public void Match_ThePlainIde_StillFallsBackToTheGenericVisualStudioRule()
    {
        // Regression guard for the fix above: a real full-IDE install must still resolve to the
        // original Rider/VS Code guidance, not accidentally to Build Tools or Installer.
        var software = TestSoftware.Create("Microsoft Visual Studio Community 2022", publisher: "Microsoft Corporation");

        var result = CreateMatcher().Match(software);

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
        Assert.Contains(result.Entry!.Alternatives, a => a.Name.Contains("Rider"));
    }

    [Fact]
    public void Match_OneDrive_NeverExposesARawRepositoryNameAsAnAlternative()
    {
        var software = TestSoftware.Create("Microsoft OneDrive");

        var result = CreateMatcher().Match(software);

        Assert.DoesNotContain(result.Entry!.Alternatives, a => a.Name.Contains('/'));
        Assert.Contains(result.Entry.Alternatives, a => a.Name == "OneDrive Client for Linux");
    }

    [Theory]
    [InlineData("PowerShell 7.6.6.0-x64")]
    [InlineData("Python 3.14.3")]
    [InlineData("Node.js")]
    [InlineData("Audacity 3.7.7")]
    public void Match_ConfirmedNativePrograms_ResolveToNative(string name)
    {
        var result = CreateMatcher().Match(TestSoftware.Create(name));

        Assert.Equal(SoftwareCompatibilityStatus.Native, result.Status);
    }

    [Theory]
    [InlineData("µTorrent")]
    [InlineData("uTorrent")]
    public void Match_UTorrentVariants_ResolveToEquivalent(string name)
    {
        var result = CreateMatcher().Match(TestSoftware.Create(name));

        Assert.Equal(SoftwareCompatibilityStatus.Equivalent, result.Status);
    }

    [Fact]
    public void LoadEmbedded_NoAlternativeNameLooksLikeAGitHubRepositorySlug()
    {
        // General audit, not just the one reported OneDrive case. Deliberately narrow: a real
        // "org/repo" slug is all-lowercase with no spaces (e.g. "abraunegg/onedrive") — unlike a
        // legitimate "either of these two" listing such as "Ark/File Roller" (capitalized, has a
        // space), which is a normal, recognizable way to present two real product names.
        var repoSlugPattern = new System.Text.RegularExpressions.Regex(@"^[a-z0-9][a-z0-9_.-]*/[a-z0-9][a-z0-9_.-]*$");
        var entries = SoftwareCompatibilityDatabase.LoadEmbedded().Entries;

        foreach (var entry in entries)
        {
            foreach (var alternative in entry.Alternatives)
            {
                Assert.False(
                    repoSlugPattern.IsMatch(alternative.Name),
                    $"'{alternative.Name}' looks like a raw repository slug, not a recognizable product name.");
            }
        }
    }
}
