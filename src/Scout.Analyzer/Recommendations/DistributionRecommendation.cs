namespace Scout.Analyzer.Recommendations;

/// <summary>
/// Result of <see cref="DistributionRecommender.Recommend"/>: exactly one primary suggestion with
/// one reason, plus one alternative — deliberately not a ranked list. See
/// docs/schema/distributions-v0.1.md for why a single recommendation, not a menu.
/// </summary>
public sealed class DistributionRecommendation
{
    public required DistributionEntry Primary { get; init; }

    public required DistributionRecommendationReason PrimaryReason { get; init; }

    /// <summary>A second, genuinely different option — null only if the database has a single entry.</summary>
    public DistributionEntry? Secondary { get; init; }
}
