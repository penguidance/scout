using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// Root of a machine profile document, as produced by Scout.Collector and consumed by
/// Scout.Analyzer. Mirrors docs/schema/profile-v0.1.md field-for-field.
///
/// This model is a plain data container: it carries no compatibility logic or interpretation.
/// Collector fills it with observed values only; Analyzer reads it to produce a separate report.
/// </summary>
public sealed class MachineProfile
{
    /// <summary>Schema version of this document, e.g. "0.1".</summary>
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    /// <summary>Semver of the Scout.Collector build that produced this document.</summary>
    [JsonPropertyName("collector_version")]
    public required string CollectorVersion { get; init; }

    /// <summary>UTC instant the collection run started.</summary>
    [JsonPropertyName("collected_at")]
    public required DateTimeOffset CollectedAt { get; init; }

    /// <summary>SHA-256 hex digest of the Windows machine GUID. Never the raw GUID.</summary>
    [JsonPropertyName("machine_id")]
    public required string MachineId { get; init; }

    [JsonPropertyName("privacy")]
    public required PrivacyInfo Privacy { get; init; }

    [JsonPropertyName("system")]
    public required SystemInfo System { get; init; }

    [JsonPropertyName("firmware")]
    public required FirmwareInfo Firmware { get; init; }

    [JsonPropertyName("cpu")]
    public required CpuInfo Cpu { get; init; }

    [JsonPropertyName("memory")]
    public required MemoryInfo Memory { get; init; }

    [JsonPropertyName("storage")]
    public required StorageInfo Storage { get; init; }

    /// <summary>
    /// PnP devices. The matching key for every entry is <see cref="DeviceInfo.HardwareId"/>,
    /// never <see cref="DeviceInfo.FriendlyName"/>.
    /// </summary>
    [JsonPropertyName("devices")]
    public required IReadOnlyList<DeviceInfo> Devices { get; init; }

    [JsonPropertyName("gpu_topology")]
    public required GpuTopology GpuTopology { get; init; }

    [JsonPropertyName("os")]
    public required OsInfo Os { get; init; }

    /// <summary>
    /// Installed software inventory, read from registry Uninstall keys plus, best-effort,
    /// Win32_InstalledStoreProgram for MSIX/Store apps (never Win32_Product). Already had the
    /// obvious noise (blank entries, Windows updates/hotfixes, SystemComponent-flagged entries,
    /// sub-components of an already-listed product) removed by the collector — see
    /// <see cref="SoftwareFilteredCount"/> for how many.
    /// </summary>
    [JsonPropertyName("software")]
    public required IReadOnlyList<SoftwareEntry> Software { get; init; }

    /// <summary>
    /// How many registry Uninstall/Store entries the collector examined but left out of
    /// <see cref="Software"/> as noise (blank <c>DisplayName</c>, <c>SystemComponent=1</c>, a
    /// sub-component of an already-listed product, or a Windows update/hotfix). Deliberately a
    /// plain counter here rather than a <see cref="CollectionError"/> per entry — this is expected,
    /// routine filtering, not a collection failure.
    /// </summary>
    /// <remarks>
    /// Not <c>required</c>: added after schema-0.1's initial field set, so a profile collected
    /// before this field existed simply omits the key — null here means "this profile's collector
    /// version never computed this", not a read failure. A profile from this or a later collector
    /// always populates it with a real (possibly zero) count.
    /// </remarks>
    [JsonPropertyName("software_filtered_count")]
    public int? SoftwareFilteredCount { get; init; }

    [JsonPropertyName("peripherals")]
    public required IReadOnlyList<PeripheralInfo> Peripherals { get; init; }

    [JsonPropertyName("collection_errors")]
    public required IReadOnlyList<CollectionError> CollectionErrors { get; init; }
}
