using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// Reads/writes a <see cref="LocalizedText"/> as a plain JSON object mapping language code to
/// string, e.g. <c>{ "en": "...", "tr": "..." }</c>. A missing "en" key, or one whose value is
/// empty or whitespace-only, is a data error and throws a <see cref="JsonException"/> immediately
/// at load time — see <see cref="LocalizedText"/>'s remarks — rather than deserializing into
/// something that would later resolve to a blank string. This check is duplicated here (rather
/// than relying on <see cref="LocalizedText"/>'s own constructor validation propagating up) so the
/// failure is always a <see cref="JsonException"/> from this JSON-loading path specifically, not
/// whatever exception type the constructor happens to throw.
/// </summary>
public sealed class LocalizedTextJsonConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                "Expected a localized-text object, e.g. { \"en\": \"...\", \"tr\": \"...\" } — found " +
                $"{reader.TokenType} instead.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Malformed localized-text object.");
            }

            var language = reader.GetString()!;
            reader.Read();

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Localized text value for language '{language}' must be a string.");
            }

            values[language] = reader.GetString()!;
        }

        if (!values.TryGetValue("en", out var english) || string.IsNullOrWhiteSpace(english))
        {
            throw new JsonException(
                "Localized text is missing the required 'en' key (or its value is empty/whitespace) — " +
                "every localized-text field must provide a non-empty English value.");
        }

        return new LocalizedText(values);
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (language, text) in value.Values)
        {
            writer.WriteString(language, text);
        }

        writer.WriteEndObject();
    }
}
