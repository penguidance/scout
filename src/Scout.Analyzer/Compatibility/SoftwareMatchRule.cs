using System.Text.Json.Serialization;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// How a <see cref="SoftwareCompatibilityEntry"/> recognizes an installed program, from most to
/// least reliable signal:
/// <list type="number">
/// <item><see cref="RegistryKey"/> — never re-translated, checked first by <see cref="SoftwareMatcher"/>; sufficient on its own.</item>
/// <item><see cref="NameAliases"/> — one or more observed <c>DisplayName</c> spellings (see <see cref="SoftwareNameAlias"/> for why more than one), each optionally narrowed by <see cref="PublisherPattern"/>.</item>
/// </list>
/// A full exact name is fragile even setting localization aside — e.g. "Adobe Photoshop 2021"
/// changes every year — so most aliases use a prefix/contains pattern rather than an exact one.
/// </summary>
public sealed class SoftwareMatchRule
{
    /// <summary>
    /// Optional, checked before any name alias. When set and it matches, this alone identifies
    /// the program — name/publisher are not additionally required.
    /// </summary>
    [JsonPropertyName("registry_key")]
    public SoftwareRegistryKeyRule? RegistryKey { get; init; }

    /// <summary>
    /// Observed <c>DisplayName</c> spellings for this program — see <see cref="SoftwareNameAlias"/>.
    /// The first entry is the one this database row was originally written against; later entries
    /// are alternate spellings (typically localized) added as they were confirmed on a real
    /// machine. Empty only for a row that matches solely via <see cref="RegistryKey"/>.
    /// </summary>
    [JsonPropertyName("name_aliases")]
    public IReadOnlyList<SoftwareNameAlias> NameAliases { get; init; } = [];

    /// <summary>
    /// Optional secondary criterion checked alongside a name-alias match: when set,
    /// <see cref="Core.Models.SoftwareEntry.Publisher"/> must also contain this (case-insensitive)
    /// — never sufficient by itself, and not consulted at all for a <see cref="RegistryKey"/> match
    /// (e.g. plain "Origin" the English word needs this set to "Electronic Arts" to avoid matching
    /// something unrelated).
    /// </summary>
    [JsonPropertyName("publisher_pattern")]
    public string? PublisherPattern { get; init; }
}
