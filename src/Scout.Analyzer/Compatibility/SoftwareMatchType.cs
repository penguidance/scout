namespace Scout.Analyzer.Compatibility;

/// <summary>
/// How a <see cref="SoftwareMatchRule.NamePattern"/> is compared against an installed program's
/// name. An exact full-name match is fragile (see <see cref="SoftwareMatchRule"/>), so most
/// database entries use <see cref="Prefix"/> or <see cref="Contains"/> instead.
/// </summary>
public enum SoftwareMatchType
{
    /// <summary>The whole name must equal the pattern (case-insensitive).</summary>
    Exact,

    /// <summary>The name must start with the pattern (case-insensitive).</summary>
    Prefix,

    /// <summary>The pattern must appear anywhere in the name (case-insensitive).</summary>
    Contains
}
