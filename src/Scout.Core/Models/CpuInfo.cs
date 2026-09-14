using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>CPU facts, from Win32_Processor plus a vendor/family/model-derived feature level.</summary>
public sealed class CpuInfo
{
    [JsonPropertyName("vendor")]
    public required string Vendor { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>Null when unreadable — never fabricated as 0.</summary>
    [JsonPropertyName("physical_cores")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int? PhysicalCores { get; init; }

    /// <summary>Null when unreadable — never fabricated as 0.</summary>
    [JsonPropertyName("logical_processors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int? LogicalProcessors { get; init; }

    /// <summary>
    /// Null when the architecture could not be determined at all (source unreadable).
    /// <see cref="CpuArchitecture.Unknown"/> is reserved for "we read a real code, but it is not
    /// one we recognize" — a different, genuine observation.
    /// </summary>
    [JsonPropertyName("architecture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required CpuArchitecture? Architecture { get; init; }

    /// <summary>Null when <see cref="Architecture"/> is not x86_64, or when it could not be classified.</summary>
    [JsonPropertyName("x86_64_feature_level")]
    public X86FeatureLevel? X86_64FeatureLevel { get; init; }

    [JsonPropertyName("virtualization_firmware_enabled")]
    public bool? VirtualizationFirmwareEnabled { get; init; }
}
