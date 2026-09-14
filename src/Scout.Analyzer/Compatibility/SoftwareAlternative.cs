using System.Text.Json.Serialization;

namespace Scout.Analyzer.Compatibility;

/// <summary>One suggested substitute program for a <see cref="SoftwareCompatibilityEntry"/> that is not <see cref="SoftwareCompatibilityStatus.Native"/>.</summary>
public sealed class SoftwareAlternative
{
    /// <summary>
    /// The name shown in the report — must be something an ordinary end user would recognize as
    /// a product (e.g. "OneDrive Client for Linux"), never a raw package or GitHub repository
    /// identifier (e.g. not "abraunegg/onedrive"). See <see cref="TechnicalName"/> for where the
    /// exact technical identifier belongs instead.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>One short, user-facing sentence — e.g. whether it is free, or has a different learning curve. Distinct from the entry-level <see cref="SoftwareCompatibilityEntry.Notes"/>, which describes the original program's situation, not this specific alternative.</summary>
    [JsonPropertyName("note")]
    public required string Note { get; init; }

    /// <summary>
    /// Optional exact package/binary/repository identifier (e.g. "onedrive (abraunegg)"), kept
    /// for maintainers editing this file — never rendered in the report. Only worth setting when
    /// <see cref="Name"/> had to be a descriptive/friendly label rather than the tool's own exact
    /// name, so the precise thing being referred to is not lost.
    /// </summary>
    [JsonPropertyName("technical_name")]
    public string? TechnicalName { get; init; }
}
