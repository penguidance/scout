using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// GPU adapters and their layout, derived from the "Display"-class entries already present in
/// <see cref="MachineProfile.Devices"/> — no separate WMI query (e.g. Win32_VideoController) is
/// made for this block in v0.1.
/// </summary>
public sealed class GpuTopology
{
    [JsonPropertyName("gpus")]
    public required IReadOnlyList<GpuInfo> Gpus { get; init; }

    /// <summary>Null when this could not be determined (0 GPUs, or an ambiguous 2+ GPU combination).</summary>
    [JsonPropertyName("layout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required GpuLayout? Layout { get; init; }
}

/// <summary>
/// One GPU adapter — a view over the corresponding <see cref="DeviceInfo"/> entry.
/// <see cref="HardwareId"/> cross-references that entry in <see cref="MachineProfile.Devices"/>;
/// matching itself should use <see cref="VendorId"/>/<see cref="DeviceId"/>, same as
/// <see cref="DeviceInfo"/>.
/// </summary>
public sealed class GpuInfo
{
    [JsonPropertyName("hardware_id")]
    public required string HardwareId { get; init; }

    [JsonPropertyName("vendor_id")]
    public string? VendorId { get; init; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("friendly_name")]
    public required string FriendlyName { get; init; }

    [JsonPropertyName("driver_version")]
    public string? DriverVersion { get; init; }
}
