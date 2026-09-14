using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// The software compatibility database: a flat list of <see cref="SoftwareCompatibilityEntry"/>
/// rows, loaded from data/software-compatibility.json. Carries no matching logic itself — see
/// <see cref="SoftwareMatcher"/>. Mirrors <see cref="CompatibilityDatabase"/>'s shape/loading
/// convention exactly; kept as a separate type because the two databases describe unrelated
/// things (hardware vendor:device IDs vs. installed-program names) with unrelated schemas.
/// </summary>
public sealed class SoftwareCompatibilityDatabase
{
    private const string EmbeddedResourceName = "software-compatibility.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public SoftwareCompatibilityDatabase(IReadOnlyList<SoftwareCompatibilityEntry> entries)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public IReadOnlyList<SoftwareCompatibilityEntry> Entries { get; }

    /// <summary>Parses a database from a JSON string — e.g. a small fixture in a test.</summary>
    public static SoftwareCompatibilityDatabase FromJson(string json)
    {
        var entries = JsonSerializer.Deserialize<List<SoftwareCompatibilityEntry>>(json, JsonOptions)
            ?? throw new JsonException("Software compatibility database deserialized to null.");
        return new SoftwareCompatibilityDatabase(entries);
    }

    /// <summary>Loads the database embedded into this assembly from data/software-compatibility.json.</summary>
    public static SoftwareCompatibilityDatabase LoadEmbedded()
    {
        var assembly = typeof(SoftwareCompatibilityDatabase).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found in {assembly.FullName}.");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }
}
