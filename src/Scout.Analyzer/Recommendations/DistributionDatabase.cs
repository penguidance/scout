using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scout.Analyzer.Recommendations;

/// <summary>
/// The distribution database: a flat list of <see cref="DistributionEntry"/> rows, loaded from
/// data/distributions.json. Carries no recommendation logic itself — see
/// <see cref="DistributionRecommender"/>. Mirrors <see cref="Compatibility.CompatibilityDatabase"/>'s
/// shape/loading convention.
/// </summary>
public sealed class DistributionDatabase
{
    private const string EmbeddedResourceName = "distributions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public DistributionDatabase(IReadOnlyList<DistributionEntry> entries)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public IReadOnlyList<DistributionEntry> Entries { get; }

    /// <summary>Parses a database from a JSON string — e.g. a small fixture in a test.</summary>
    public static DistributionDatabase FromJson(string json)
    {
        var entries = JsonSerializer.Deserialize<List<DistributionEntry>>(json, JsonOptions)
            ?? throw new JsonException("Distribution database deserialized to null.");
        return new DistributionDatabase(entries);
    }

    /// <summary>Loads the database embedded into this assembly from data/distributions.json.</summary>
    public static DistributionDatabase LoadEmbedded()
    {
        var assembly = typeof(DistributionDatabase).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found in {assembly.FullName}.");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }
}
