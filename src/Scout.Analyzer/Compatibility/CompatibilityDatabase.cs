using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// The hardware compatibility database: a flat list of <see cref="CompatibilityEntry"/> rows,
/// loaded from data/hardware-compatibility.json. Carries no matching logic itself — see
/// <see cref="CompatibilityMatcher"/>.
/// </summary>
public sealed class CompatibilityDatabase
{
    private const string EmbeddedResourceName = "hardware-compatibility.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public CompatibilityDatabase(IReadOnlyList<CompatibilityEntry> entries)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public IReadOnlyList<CompatibilityEntry> Entries { get; }

    /// <summary>Parses a database from a JSON string — e.g. a small fixture in a test.</summary>
    public static CompatibilityDatabase FromJson(string json)
    {
        var entries = JsonSerializer.Deserialize<List<CompatibilityEntry>>(json, JsonOptions)
            ?? throw new JsonException("Hardware compatibility database deserialized to null.");
        return new CompatibilityDatabase(entries);
    }

    /// <summary>Loads the database embedded into this assembly from data/hardware-compatibility.json.</summary>
    public static CompatibilityDatabase LoadEmbedded()
    {
        var assembly = typeof(CompatibilityDatabase).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found in {assembly.FullName}.");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }
}
