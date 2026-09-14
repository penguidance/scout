using System.Text.Json.Serialization;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// Matches a <see cref="Core.Models.SoftwareEntry.RegistryKeyName"/> — the Uninstall subkey's own
/// name, which is set once at install time and never re-translated when the display language
/// changes. The most reliable identity signal <see cref="SoftwareMatcher"/> has: for an
/// MSI-installed program this is a stable ProductCode GUID (e.g. many Microsoft Office ProductCodes
/// end in the hex sequence <c>"FF1CE"</c> — a well-known packaging convention, not a display string
/// anyone chose per-language), so it works regardless of what language <c>DisplayName</c> is in —
/// see docs/schema/software-compatibility-v0.1.md.
/// </summary>
public sealed class SoftwareRegistryKeyRule
{
    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("match_type")]
    public required SoftwareMatchType MatchType { get; init; }
}
