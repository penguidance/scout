namespace Scout.Analyzer.Compatibility;

/// <summary>Result of looking one installed program up in the <see cref="SoftwareCompatibilityDatabase"/>.</summary>
public sealed class SoftwareMatch
{
    public required SoftwareMatchLevel Level { get; init; }

    /// <summary>The matched row; null when <see cref="Level"/> is <see cref="SoftwareMatchLevel.None"/>.</summary>
    public SoftwareCompatibilityEntry? Entry { get; init; }

    /// <summary>Convenience: <c>Entry?.Status ?? SoftwareCompatibilityStatus.Unknown</c>.</summary>
    public required SoftwareCompatibilityStatus Status { get; init; }

    /// <summary>
    /// Which evidence produced this match — see <see cref="SoftwareMatchSignal"/>. Empty when
    /// <see cref="Level"/> is <see cref="SoftwareMatchLevel.None"/>.
    /// </summary>
    public required IReadOnlyList<SoftwareMatchSignal> Signals { get; init; }
}
