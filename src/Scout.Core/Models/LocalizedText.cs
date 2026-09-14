using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// A user-facing text field that carries more than one language, e.g. <c>{ "en": "...", "tr": "..." }</c>
/// in a database JSON file. Used wherever a compatibility-database entry's text is shown directly
/// in the report (see <c>Scout.Analyzer.Compatibility.CompatibilityEntry.Notes</c>,
/// <c>CompatibilityEntry.DisplayName</c>, <c>SoftwareCompatibilityEntry.Notes</c> and
/// <c>SoftwareAlternative.Note</c>).
/// </summary>
/// <remarks>
/// "en" is the one required key — every other language is optional and only ever improves the
/// result, never replaces the guaranteed fallback. This mirrors the project's general "never
/// silently produce nothing" rule: a database entry missing "en", or whose "en" value is empty or
/// whitespace-only, is a data error and must fail loudly at load time (see
/// <see cref="LocalizedTextJsonConverter"/>), not resolve to a blank string at report time.
/// </remarks>
[JsonConverter(typeof(LocalizedTextJsonConverter))]
public sealed class LocalizedText
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public LocalizedText(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!values.TryGetValue("en", out var english) || string.IsNullOrWhiteSpace(english))
        {
            throw new ArgumentException("A LocalizedText value must contain a non-empty 'en' entry.", nameof(values));
        }

        // Copied into a case-insensitive dictionary so a lookup for "TR" or "tr-TR" behaves the
        // same as one for "tr" regardless of how the JSON file happened to spell the key.
        _values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The raw language → text map, in the case it was constructed with. Used only by <see cref="LocalizedTextJsonConverter"/> to write the value back out.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Convenience constructor for an English-only value (e.g. in tests or code-generated defaults).</summary>
    public static LocalizedText FromEnglish(string text) => new(new Dictionary<string, string> { ["en"] = text });

    /// <summary>
    /// Resolves the text for <paramref name="language"/>, falling back to "en" when that language
    /// is missing. <paramref name="language"/> may be a full BCP-47 tag (e.g. "tr-TR") — an exact
    /// key match is tried first, then just the primary subtag ("tr"), consistent with how
    /// <c>SoftwareMatcher</c> already tolerates a full profile tag against a short alias tag. A
    /// null or unrecognized language always resolves to "en", never to an empty string — "en" is
    /// guaranteed present by the constructor.
    /// </summary>
    public string Resolve(string? language)
    {
        if (language is not null)
        {
            if (_values.TryGetValue(language, out var exact))
            {
                return exact;
            }

            var dashIndex = language.IndexOf('-');
            if (dashIndex > 0 && _values.TryGetValue(language[..dashIndex], out var byPrimarySubtag))
            {
                return byPrimarySubtag;
            }
        }

        return _values["en"];
    }
}
