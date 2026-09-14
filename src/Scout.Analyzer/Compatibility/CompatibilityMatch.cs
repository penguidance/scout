namespace Scout.Analyzer.Compatibility;

/// <summary>Result of looking one device up in the <see cref="CompatibilityDatabase"/>.</summary>
public sealed class CompatibilityMatch
{
    public required MatchLevel Level { get; init; }

    /// <summary>The matched row; null when <see cref="Level"/> is <see cref="MatchLevel.None"/>.</summary>
    public CompatibilityEntry? Entry { get; init; }

    /// <summary>Convenience: <c>Entry?.Support ?? SupportLevel.Unknown</c>.</summary>
    public required SupportLevel Support { get; init; }
}
