using System.Text.Json;
using System.Text.Json.Serialization;
using Scout.Core.Models;

namespace Scout.Core.Serialization;

/// <summary>
/// Shared JSON serialization settings for <see cref="MachineProfile"/> documents, used by both
/// Scout.Collector (writing) and Scout.Analyzer (reading), so the two never drift apart on
/// casing/format conventions.
/// </summary>
public static class ProfileJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            // Property names are set explicitly per-member via [JsonPropertyName], so no
            // blanket naming policy is applied here.
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static string Serialize(MachineProfile profile) =>
        JsonSerializer.Serialize(profile, Options);

    public static MachineProfile Deserialize(string json) =>
        JsonSerializer.Deserialize<MachineProfile>(json, Options)
            ?? throw new JsonException("Machine profile document deserialized to null.");
}
