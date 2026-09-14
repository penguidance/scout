namespace Scout.Analyzer.Compatibility;

/// <summary>
/// Default importance of a <see cref="SoftwareCompatibilityEntry"/> if it turns out
/// <see cref="SoftwareCompatibilityStatus.Blocked"/> — a starting assumption, not a fact about any
/// specific machine. Photoshop is <see cref="Critical"/> for a graphic designer and irrelevant for
/// someone who never opens it; this field reflects how serious being blocked typically is *for
/// someone who has the program installed at all*, and a user can always override it later.
/// </summary>
public enum SoftwareImportance
{
    /// <summary>Losing this is a serious problem for most people who have it installed (professional tools, accounting/tax software).</summary>
    Critical,

    /// <summary>The default: matters, but rarely irreplaceable or urgent.</summary>
    Normal,

    /// <summary>Losing this is a minor inconvenience (utilities, games, casual tools).</summary>
    Minor
}
