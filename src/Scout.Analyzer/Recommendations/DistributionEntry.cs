using System.Text.Json.Serialization;
using Scout.Core.Models;

namespace Scout.Analyzer.Recommendations;

/// <summary>One row of the distribution database (data/distributions.json).</summary>
public sealed class DistributionEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("download_url")]
    public required string DownloadUrl { get; init; }

    [JsonPropertyName("desktop_environment")]
    public required string DesktopEnvironment { get; init; }

    /// <summary>Below this, <see cref="DistributionRecommender"/> excludes this distribution — see docs/schema/distributions-v0.1.md.</summary>
    [JsonPropertyName("minimum_feature_level")]
    public required X86FeatureLevel MinimumFeatureLevel { get; init; }

    /// <summary>Null means no known minimum — never used to exclude a candidate when null.</summary>
    [JsonPropertyName("minimum_memory_bytes")]
    public long? MinimumMemoryBytes { get; init; }

    /// <summary>Null means no known minimum — never used to exclude a candidate when null.</summary>
    [JsonPropertyName("minimum_disk_bytes")]
    public long? MinimumDiskBytes { get; init; }

    /// <summary>Whether the installer offers a built-in, GUI way to install a proprietary GPU driver (e.g. NVIDIA) — see "Neden `offers_proprietary_driver_installer`" in the schema doc.</summary>
    [JsonPropertyName("offers_proprietary_driver_installer")]
    public required bool OffersProprietaryDriverInstaller { get; init; }

    /// <summary>Marks this as a low-resource-oriented pick, preferred when the machine has limited RAM.</summary>
    [JsonPropertyName("lightweight")]
    public required bool Lightweight { get; init; }

    /// <summary>
    /// The general-purpose default recommendation when no other signal (low memory, proprietary
    /// GPU driver) applies — see <see cref="DistributionRecommendationReason.WindowsFamiliarity"/>.
    /// At most one entry should set this.
    /// </summary>
    [JsonPropertyName("is_default_choice")]
    public required bool IsDefaultChoice { get; init; }

    /// <summary>
    /// Short, human-readable description of this distribution's typical use case — documentation
    /// for whoever edits this file. Not shown verbatim in the report: the report's one-sentence
    /// reason is composed by Scout.Reporter from <see cref="DistributionRecommendationReason"/>,
    /// so it stays in the user's language independent of how this field is worded.
    /// </summary>
    [JsonPropertyName("recommended_when")]
    public required string RecommendedWhen { get; init; }
}
