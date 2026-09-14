using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>Operating system facts, from Win32_OperatingSystem plus the CurrentVersion registry key.</summary>
public sealed class OsInfo
{
    [JsonPropertyName("edition")]
    public required string Edition { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("build")]
    public required string Build { get; init; }

    /// <summary>e.g. "24H2". Only available from the registry, not WMI.</summary>
    [JsonPropertyName("display_version")]
    public string? DisplayVersion { get; init; }

    [JsonPropertyName("install_date")]
    public DateTimeOffset? InstallDate { get; init; }

    /// <summary>Null when this could not be determined at all — see <see cref="CpuInfo.Architecture"/> remarks.</summary>
    [JsonPropertyName("architecture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required CpuArchitecture? Architecture { get; init; }

    /// <summary>BCP-47 tag, e.g. "tr-TR".</summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }
}
