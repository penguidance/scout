namespace Scout.Analyzer.Compatibility;

/// <summary>
/// How well a piece of hardware is expected to work on Linux, per a
/// <see cref="CompatibilityEntry"/>. Serialized as snake_case (see
/// <see cref="CompatibilityDatabase"/>'s JSON options), so member names here stay PascalCase.
/// </summary>
public enum SupportLevel
{
    /// <summary>In the mainline kernel and works out of the box, no extra packages needed.</summary>
    Native,

    /// <summary>A kernel driver exists, but it needs a separate (often non-free) firmware package to function.</summary>
    FirmwareRequired,

    /// <summary>Full function requires a closed-source, out-of-tree driver.</summary>
    Proprietary,

    /// <summary>Works, but with reduced/incomplete functionality (e.g. an open-source fallback driver).</summary>
    Partial,

    /// <summary>Does not work on Linux.</summary>
    Unsupported,

    /// <summary>Not in the compatibility database — see <see cref="MatchLevel.None"/>. Never guessed at.</summary>
    Unknown
}
