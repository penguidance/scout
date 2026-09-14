using System.Text.Json;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Core;

/// <summary>
/// <see cref="LocalizedText"/> is the wrapper behind every user-facing multi-language field in the
/// compatibility databases (<c>notes</c>, <c>display_name</c>, <c>alternatives[].note</c>) — see
/// docs/schema/software-compatibility-v0.1.md and hardware-compatibility-v0.1.md. These tests
/// cover the two guarantees those docs promise: a missing "en" is a loud data error, never a
/// silent empty string, and resolution always falls back to "en" rather than returning nothing.
/// </summary>
public class LocalizedTextTests
{
    [Fact]
    public void Constructor_MissingEnglishKey_Throws()
    {
        var values = new Dictionary<string, string> { ["tr"] = "Merhaba" };

        Assert.Throws<ArgumentException>(() => new LocalizedText(values));
    }

    [Fact]
    public void Constructor_EmptyEnglishValue_Throws()
    {
        var values = new Dictionary<string, string> { ["en"] = "", ["tr"] = "Merhaba" };

        Assert.Throws<ArgumentException>(() => new LocalizedText(values));
    }

    [Fact]
    public void Constructor_WhitespaceOnlyEnglishValue_Throws()
    {
        var values = new Dictionary<string, string> { ["en"] = "   ", ["tr"] = "Merhaba" };

        Assert.Throws<ArgumentException>(() => new LocalizedText(values));
    }

    [Fact]
    public void FromJson_MissingEnglishKey_ThrowsAtLoadTime()
    {
        const string json = """{ "tr": "Merhaba" }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LocalizedText>(json));
    }

    [Fact]
    public void FromJson_EmptyEnglishValue_ThrowsAtLoadTime()
    {
        const string json = """{ "en": "", "tr": "Merhaba" }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LocalizedText>(json));
    }

    [Fact]
    public void FromJson_WhitespaceOnlyEnglishValue_ThrowsAtLoadTime()
    {
        const string json = """{ "en": "   ", "tr": "Merhaba" }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LocalizedText>(json));
    }

    [Fact]
    public void FromJson_EmptyObject_ThrowsAtLoadTime()
    {
        const string json = "{}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LocalizedText>(json));
    }

    [Fact]
    public void FromJson_NotAnObject_Throws()
    {
        const string json = "\"just a string\"";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LocalizedText>(json));
    }

    [Fact]
    public void Resolve_RequestedLanguagePresent_ReturnsIt()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["en"] = "Hello", ["tr"] = "Merhaba" });

        Assert.Equal("Merhaba", text.Resolve("tr"));
    }

    [Fact]
    public void Resolve_RequestedLanguageMissing_FallsBackToEnglish()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["en"] = "Hello", ["tr"] = "Merhaba" });

        Assert.Equal("Hello", text.Resolve("de"));
    }

    [Fact]
    public void Resolve_NullLanguage_ReturnsEnglish()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["en"] = "Hello", ["tr"] = "Merhaba" });

        Assert.Equal("Hello", text.Resolve(null));
    }

    [Fact]
    public void Resolve_EnglishOnlyValue_AlwaysReturnsEnglishRegardlessOfRequestedLanguage()
    {
        var text = LocalizedText.FromEnglish("Hello");

        Assert.Equal("Hello", text.Resolve("tr"));
        Assert.Equal("Hello", text.Resolve(null));
    }

    [Fact]
    public void Resolve_FullBcp47ProfileTag_MatchesAShortLanguageKey()
    {
        // Consistent with SoftwareMatcher's own language tie-break: a profile's full os.language
        // (e.g. "tr-TR") must still resolve against a short "tr" key in the data file.
        var text = new LocalizedText(new Dictionary<string, string> { ["en"] = "Hello", ["tr"] = "Merhaba" });

        Assert.Equal("Merhaba", text.Resolve("tr-TR"));
    }

    [Fact]
    public void Resolve_LanguageKeyLookupIsCaseInsensitive()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["en"] = "Hello", ["tr"] = "Merhaba" });

        Assert.Equal("Merhaba", text.Resolve("TR"));
        Assert.Equal("Merhaba", text.Resolve("TR-tr"));
    }

    [Fact]
    public void RoundTrip_SerializeThenDeserialize_PreservesAllLanguages()
    {
        var original = new LocalizedText(new Dictionary<string, string> { ["en"] = "Hello", ["tr"] = "Merhaba" });

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<LocalizedText>(json)!;

        Assert.Equal("Hello", roundTripped.Resolve("en"));
        Assert.Equal("Merhaba", roundTripped.Resolve("tr"));
    }
}
