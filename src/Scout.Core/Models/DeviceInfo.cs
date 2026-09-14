using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// One PnP device, from Win32_PnPEntity / Win32_PnPSignedDriver.
/// </summary>
/// <remarks>
/// Matching is always done via <see cref="VendorId"/> + <see cref="DeviceId"/> when both are
/// present (e.g. Linux kernel module databases key on exactly this vendor:device hex pair for
/// PCI/USB hardware) — never by string-comparing <see cref="HardwareId"/> or
/// <see cref="HardwareIdRaw"/>, and never by <see cref="FriendlyName"/>. For buses with no
/// vendor:device concept (ACPI's PNP IDs, e.g. "ACPI\PNP0303"), <see cref="VendorId"/>/
/// <see cref="DeviceId"/> are null and <see cref="HardwareId"/> is the only usable key.
/// </remarks>
public sealed class DeviceInfo
{
    /// <summary>
    /// Normalized matching key: for PCI/USB, the vendor+device pair only (e.g.
    /// "PCI\VEN_1002&amp;DEV_73FF", stripped of SUBSYS/REV); for everything else, identical to
    /// <see cref="HardwareIdRaw"/>. See <see cref="VendorId"/>/<see cref="DeviceId"/> remarks —
    /// prefer those for the actual match when they are present.
    /// </summary>
    [JsonPropertyName("hardware_id")]
    public required string HardwareId { get; init; }

    /// <summary>
    /// The exact, untouched first entry of Win32_PnPEntity's HardwareID array (most specific
    /// candidate), e.g. "PCI\VEN_1002&amp;DEV_73FF&amp;SUBSYS_0123458&amp;REV_C1". Kept verbatim
    /// per the schema's general "never delete a raw value" principle, even though
    /// <see cref="HardwareId"/>/<see cref="VendorId"/>/<see cref="DeviceId"/> are derived from it.
    /// </summary>
    [JsonPropertyName("hardware_id_raw")]
    public required string HardwareIdRaw { get; init; }

    /// <summary>Hex, uppercase, e.g. "1002". Null when not extractable (no VEN_/VID_ pattern in the hardware ID).</summary>
    [JsonPropertyName("vendor_id")]
    public string? VendorId { get; init; }

    /// <summary>Hex, uppercase, e.g. "73FF". Null when not extractable.</summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("bus_type")]
    public required DeviceBusType BusType { get; init; }

    /// <summary>The remaining (less specific) entries of HardwareID, verbatim, in Windows's own order.</summary>
    [JsonPropertyName("compatible_ids")]
    public required IReadOnlyList<string> CompatibleIds { get; init; }

    /// <summary>Display-only. Never used as a matching key.</summary>
    [JsonPropertyName("friendly_name")]
    public required string FriendlyName { get; init; }

    /// <summary>PnP device class, e.g. "Net", "Display", "HIDClass".</summary>
    [JsonPropertyName("class")]
    public required string Class { get; init; }

    [JsonPropertyName("driver_provider")]
    public string? DriverProvider { get; init; }

    [JsonPropertyName("driver_version")]
    public string? DriverVersion { get; init; }

    [JsonPropertyName("driver_date")]
    public DateOnly? DriverDate { get; init; }

    /// <summary>Null when the status code itself could not be read — see <see cref="DeviceStatus"/> remarks.</summary>
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required DeviceStatus? Status { get; init; }
}
