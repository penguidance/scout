namespace Scout.Analyzer.Recommendations;

/// <summary>
/// Which signal decided the primary pick in a <see cref="DistributionRecommendation"/> — kept as
/// an enum, not a pre-rendered string, so Scout.Reporter composes the actual one-sentence
/// explanation in the user's language (see docs/schema/distributions-v0.1.md).
/// </summary>
public enum DistributionRecommendationReason
{
    /// <summary>No stronger signal applied — the general-purpose default, chosen for its Windows-like interface.</summary>
    WindowsFamiliarity,

    /// <summary>Total memory is below the recommender's low-memory threshold — a lightweight desktop environment was preferred.</summary>
    LowMemory,

    /// <summary>A display device needs a proprietary driver (e.g. NVIDIA) — a distribution with a built-in GUI installer for it was preferred.</summary>
    NvidiaProprietaryDriver,

    /// <summary>The machine falls below every candidate's comfortable minimum — the lightest available option was picked anyway, as a best effort.</summary>
    LimitedHardware
}
