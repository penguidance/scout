using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// A connected peripheral (keyboard, mouse, printer, audio endpoint, etc.) — same shape and
/// matching-key convention as <see cref="DeviceInfo"/> (not yet collected in this version; see
/// docs/schema/profile-v0.1.md), filtered to a different, interaction-focused set of PNP device
/// classes than Scout.Collector's device allow-list.
/// </summary>
public sealed class PeripheralInfo
{
    /// <summary>Normalized matching key. See <see cref="DeviceInfo.HardwareId"/> remarks.</summary>
    [JsonPropertyName("hardware_id")]
    public required string HardwareId { get; init; }

    /// <summary>The exact, untouched first HardwareID entry. See <see cref="DeviceInfo.HardwareIdRaw"/>.</summary>
    [JsonPropertyName("hardware_id_raw")]
    public required string HardwareIdRaw { get; init; }

    /// <summary>Prefer this + <see cref="DeviceId"/> for matching over any hardware_id string. See <see cref="DeviceInfo.VendorId"/>.</summary>
    [JsonPropertyName("vendor_id")]
    public string? VendorId { get; init; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("bus_type")]
    public required DeviceBusType BusType { get; init; }

    [JsonPropertyName("compatible_ids")]
    public required IReadOnlyList<string> CompatibleIds { get; init; }

    /// <summary>Display-only. Never used as a matching key.</summary>
    [JsonPropertyName("friendly_name")]
    public required string FriendlyName { get; init; }

    [JsonPropertyName("class")]
    public required string Class { get; init; }

    [JsonPropertyName("connection_type")]
    public required ConnectionType ConnectionType { get; init; }
}
