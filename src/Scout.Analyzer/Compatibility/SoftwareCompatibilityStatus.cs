namespace Scout.Analyzer.Compatibility;

/// <summary>
/// How well an installed Windows program is expected to translate to a Linux machine, per a
/// <see cref="SoftwareCompatibilityEntry"/>. Serialized as snake_case (see
/// <see cref="SoftwareCompatibilityDatabase"/>'s JSON options), so member names here stay PascalCase.
/// </summary>
public enum SoftwareCompatibilityStatus
{
    /// <summary>The vendor ships a Linux version of this exact program.</summary>
    Native,

    /// <summary>No Linux version, but a genuinely different program covers the same job — see <see cref="SoftwareCompatibilityEntry.Alternatives"/>.</summary>
    Equivalent,

    /// <summary>No Linux version, but it is known to run under Wine/Proton (to varying reliability).</summary>
    Wine,

    /// <summary>No Linux desktop app, but the same product/service works through a browser.</summary>
    Web,

    /// <summary>Does not work on Linux, and no real substitute exists either.</summary>
    Blocked,

    /// <summary>
    /// Not in the compatibility database — see <see cref="SoftwareMatchLevel.None"/>. Never
    /// appears in data/software-compatibility.json itself; only assigned by
    /// <see cref="SoftwareMatcher"/> when no entry matches.
    /// </summary>
    Unknown
}
