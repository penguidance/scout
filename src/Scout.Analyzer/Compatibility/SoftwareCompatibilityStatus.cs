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

    /// <summary>
    /// The job this program does is already a built-in feature of Linux itself — there is nothing
    /// missing to substitute for, so no <see cref="SoftwareCompatibilityEntry.Alternatives"/> are
    /// ever listed (same convention as <see cref="Native"/>). Example: MSYS2 (brings Unix tools to
    /// Windows; Linux already has them) or Visual Studio Installer (package management; Linux has
    /// its distribution's own package manager). Never counts as a problem, regardless of
    /// <see cref="SoftwareCompatibilityEntry.Importance"/> — see <c>VerdictEngine.Decide</c>.
    /// </summary>
    BuiltIn,

    /// <summary>
    /// The core function (usually hardware) works on Linux, but with real feature loss compared to
    /// Windows — lighter than <see cref="Blocked"/>, since the program is not simply unusable.
    /// Example: SteelSeries GG (the keyboard/mouse works as a plain Linux input device; only
    /// RGB/macro configuration software is missing). Never raises the overall verdict by itself,
    /// even when <see cref="SoftwareCompatibilityEntry.Importance"/> is Critical — but a Critical
    /// entry is still always shown in the report.
    /// </summary>
    Partial,

    /// <summary>Does not work on Linux, and no real substitute exists either.</summary>
    Blocked,

    /// <summary>
    /// Not in the compatibility database — see <see cref="SoftwareMatchLevel.None"/>. Never
    /// appears in data/software-compatibility.json itself; only assigned by
    /// <see cref="SoftwareMatcher"/> when no entry matches.
    /// </summary>
    Unknown
}
