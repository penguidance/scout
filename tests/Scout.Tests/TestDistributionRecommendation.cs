using Scout.Analyzer.Recommendations;
using Scout.Core.Models;

namespace Scout.Tests;

/// <summary>Minimal <see cref="DistributionRecommendation"/> fixture for tests that need a complete <see cref="Scout.Analyzer.Analysis.AnalysisResult"/> but do not care about the recommendation itself.</summary>
internal static class TestDistributionRecommendation
{
    public static DistributionRecommendation Default() => new()
    {
        Primary = Entry("Linux Mint", "Cinnamon"),
        PrimaryReason = DistributionRecommendationReason.WindowsFamiliarity,
        Secondary = Entry("Ubuntu", "GNOME")
    };

    public static DistributionEntry Entry(
        string name,
        string desktopEnvironment,
        string version = "1.0",
        string downloadUrl = "https://example.invalid/download",
        X86FeatureLevel minimumFeatureLevel = X86FeatureLevel.v1,
        long? minimumMemoryBytes = null,
        long? minimumDiskBytes = null,
        bool offersProprietaryDriverInstaller = false,
        bool lightweight = false,
        bool isDefaultChoice = false) => new()
    {
        Name = name,
        Version = version,
        DownloadUrl = downloadUrl,
        DesktopEnvironment = desktopEnvironment,
        MinimumFeatureLevel = minimumFeatureLevel,
        MinimumMemoryBytes = minimumMemoryBytes,
        MinimumDiskBytes = minimumDiskBytes,
        OffersProprietaryDriverInstaller = offersProprietaryDriverInstaller,
        Lightweight = lightweight,
        IsDefaultChoice = isDefaultChoice,
        RecommendedWhen = "test fixture"
    };
}
