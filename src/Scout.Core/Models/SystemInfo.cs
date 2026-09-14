using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>System-level identification, from Win32_ComputerSystem / Win32_SystemEnclosure / Win32_BIOS.</summary>
public sealed class SystemInfo
{
    [JsonPropertyName("manufacturer")]
    public required string Manufacturer { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Null when the enclosure could not be read at all. <see cref="ChassisType.Unknown"/> and
    /// <see cref="ChassisType.Other"/> are reserved for "we read a real SMBIOS chassis code" —
    /// respectively the code's own literal "2 = Unknown" and a recognized-but-uncommon code.
    /// </summary>
    [JsonPropertyName("chassis_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required ChassisType? ChassisType { get; init; }

    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    /// <summary>Null when <see cref="PrivacyInfo.SerialNumbersIncluded"/> is false.</summary>
    [JsonPropertyName("serial_number")]
    public string? SerialNumber { get; init; }

    /// <summary>Null when <see cref="PrivacyInfo.HostnameIncluded"/> is false.</summary>
    [JsonPropertyName("hostname")]
    public string? Hostname { get; init; }
}
