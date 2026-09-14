using System.Text.Json.Serialization;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// One name pattern a <see cref="SoftwareCompatibilityEntry"/> recognizes a program by.
/// <c>DisplayName</c> is set by the installer in whatever language Windows was running when the
/// program was installed — the same product can show up as "Visual Studio Build Tools" on an
/// English install and "Visual Studio Derleme Araçları" on a Turkish one, as an entirely
/// different string, not just a translated label. An entry lists one alias per observed spelling
/// instead of trying to guess every possible translation.
/// </summary>
public sealed class SoftwareNameAlias
{
    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("match_type")]
    public required SoftwareMatchType MatchType { get; init; }

    /// <summary>
    /// Optional BCP-47-ish language tag this spelling is known to have been observed under (e.g.
    /// <c>"tr"</c>). Never used to filter or gate matching — every alias is always tried
    /// regardless of the profile's <c>os.language</c>, since a missing/wrong language tag must
    /// never cause a real match to be silently skipped. Purely informational metadata (shown in
    /// the report's technical dump) plus a minor tie-break: when two aliases match equally well,
    /// the one tagged with the profile's own language is preferred. Null means "the original
    /// spelling this entry was written against" — conventionally English, but not asserted as
    /// such; not "unknown language".
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }
}
