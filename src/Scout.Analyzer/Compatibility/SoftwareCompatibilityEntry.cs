using System.Text.Json.Serialization;
using Scout.Core.Models;

namespace Scout.Analyzer.Compatibility;

/// <summary>One row of the software compatibility database (data/software-compatibility.json).</summary>
public sealed class SoftwareCompatibilityEntry
{
    [JsonPropertyName("match")]
    public required SoftwareMatchRule Match { get; init; }

    [JsonPropertyName("status")]
    public required SoftwareCompatibilityStatus Status { get; init; }

    /// <summary>Suggested substitutes. Empty (not null) when there is nothing to suggest — e.g. always empty for <see cref="SoftwareCompatibilityStatus.Native"/> and <see cref="SoftwareCompatibilityStatus.BuiltIn"/>.</summary>
    [JsonPropertyName("alternatives")]
    public IReadOnlyList<SoftwareAlternative> Alternatives { get; init; } = [];

    /// <summary>One-sentence, user-facing explanation of the situation. The report shows this verbatim — never a string composed in code — so it can be corrected by editing the JSON. Localized (see <see cref="LocalizedText"/>); resolved against the profile's language when the report is generated.</summary>
    [JsonPropertyName("notes")]
    public required LocalizedText Notes { get; init; }

    /// <summary>Default severity if this ends up <see cref="SoftwareCompatibilityStatus.Blocked"/> — see <see cref="SoftwareImportance"/>.</summary>
    [JsonPropertyName("importance")]
    public required SoftwareImportance Importance { get; init; }
}
