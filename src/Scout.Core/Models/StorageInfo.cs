using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>Storage subsystem facts, from the MSFT_Disk/MSFT_Partition/MSFT_Volume storage WMI namespace.</summary>
public sealed class StorageInfo
{
    [JsonPropertyName("disks")]
    public required IReadOnlyList<DiskInfo> Disks { get; init; }

    /// <summary>Null when this could not be determined (including: not collected yet).</summary>
    [JsonPropertyName("bitlocker_available")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required bool? BitlockerAvailable { get; init; }
}

/// <summary>One physical disk, from MSFT_Disk.</summary>
public sealed class DiskInfo
{
    /// <summary>Null when unreadable — never fabricated as 0.</summary>
    [JsonPropertyName("disk_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int? DiskNumber { get; init; }

    /// <summary>Null when unreadable. See <see cref="ChassisType"/> remarks for the null-vs-Unknown convention.</summary>
    [JsonPropertyName("media_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required StorageMediaType? MediaType { get; init; }

    /// <summary>A decoded label (e.g. "NVMe") for the raw MSFT_Disk.BusType code, or the raw code as a string if unrecognized.</summary>
    [JsonPropertyName("bus_type")]
    public required string BusType { get; init; }

    /// <summary>Null when unreadable — never fabricated as 0.</summary>
    [JsonPropertyName("size_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required long? SizeBytes { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("firmware_version")]
    public string? FirmwareVersion { get; init; }

    /// <summary>Null when unreadable — never fabricated as false.</summary>
    [JsonPropertyName("is_boot_disk")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required bool? IsBootDisk { get; init; }

    /// <summary>Null when unreadable.</summary>
    [JsonPropertyName("partition_style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required PartitionStyle? PartitionStyle { get; init; }

    [JsonPropertyName("partitions")]
    public required IReadOnlyList<PartitionInfo> Partitions { get; init; }
}

/// <summary>One partition on a disk, from MSFT_Partition, joined to MSFT_Volume by drive letter when possible.</summary>
public sealed class PartitionInfo
{
    /// <summary>
    /// Null for a partition with no mounted volume/drive letter (e.g. EFI System, Recovery) —
    /// there is no volume to read a filesystem from, which is a real fact, not a read failure.
    /// </summary>
    [JsonPropertyName("filesystem")]
    public string? Filesystem { get; init; }

    /// <summary>Null when unreadable — never fabricated as 0.</summary>
    [JsonPropertyName("size_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required long? SizeBytes { get; init; }

    [JsonPropertyName("drive_letter")]
    public string? DriveLetter { get; init; }

    /// <summary>
    /// <see cref="Core.Models.BitLockerStatus.NotApplicable"/> for a partition with no mounted
    /// volume (nothing to encrypt/query); null only when a volume exists but its status could
    /// not be read.
    /// </summary>
    [JsonPropertyName("bitlocker_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required BitLockerStatus? BitlockerStatus { get; init; }

    /// <summary>
    /// Free space on the mounted volume (<c>MSFT_Volume.SizeRemaining</c>), in bytes. Null for a
    /// partition with no mounted volume (nothing to measure — same reasoning as
    /// <see cref="Filesystem"/>) or when a volume exists but this could not be read. Not a
    /// "required-but-nullable" field: unlike <see cref="SizeBytes"/>, there is a real case (no
    /// volume) where the concept itself does not apply, not only "could not read".
    /// </summary>
    [JsonPropertyName("free_bytes")]
    public long? FreeBytes { get; init; }
}
