using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects <see cref="StorageInfo"/> from the root/Microsoft/Windows/Storage namespace
/// (MSFT_Disk, MSFT_Partition, MSFT_Volume) plus MSFT_PhysicalDisk in the same namespace (needed
/// to tell SSD from HDD — MSFT_Disk itself does not carry a media type), and BitLocker status
/// from Win32_EncryptableVolume in root/CIMV2/Security/MicrosoftVolumeEncryption — see
/// docs/schema/profile-v0.1.md — "Kaynak eşlemesi".
/// </summary>
public sealed class StorageCollector : ISectionCollector<StorageInfo>
{
    private const string StorageNamespace = "root/Microsoft/Windows/Storage";
    private const string BitLockerNamespace = "root/CIMV2/Security/MicrosoftVolumeEncryption";
    private const string DiskSource = "MSFT_Disk";
    private const string PhysicalDiskSource = "MSFT_PhysicalDisk";
    private const string PartitionSource = "MSFT_Partition";
    private const string VolumeSource = "MSFT_Volume";
    private const string EncryptableVolumeSource = "Win32_EncryptableVolume";

    // STORAGE_BUS_TYPE, https://learn.microsoft.com/windows/win32/api/winioctl/ne-winioctl-storage_bus_type
    private static readonly Dictionary<uint, string> BusTypeNames = new()
    {
        [1] = "SCSI",
        [2] = "ATAPI",
        [3] = "ATA",
        [4] = "1394",
        [5] = "SSA",
        [6] = "Fibre Channel",
        [7] = "USB",
        [8] = "RAID",
        [9] = "iSCSI",
        [10] = "SAS",
        [11] = "SATA",
        [12] = "SD",
        [13] = "MMC",
        [15] = "File Backed Virtual",
        [16] = "Storage Spaces",
        [17] = "NVMe"
    };

    private readonly ICimQueryExecutor _cim;
    private readonly TimeProvider _clock;

    public StorageCollector(ICimQueryExecutor cim, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "storage";

    public StorageInfo Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);

        var disks = FetchInstances(StorageNamespace, DiskSource, log);
        var physicalDisks = FetchInstances(StorageNamespace, PhysicalDiskSource, log);
        var partitions = FetchInstances(StorageNamespace, PartitionSource, log);
        var volumes = FetchInstances(StorageNamespace, VolumeSource, log);
        var (bitlockerAvailable, encryptableVolumesByDriveLetter) = FetchEncryptableVolumes(log);

        var physicalDiskFacts = BuildPhysicalDiskLookup(physicalDisks);
        var volumesByDriveLetter = BuildDriveLetterLookup(volumes);
        var partitionsByDiskNumber = BuildPartitionGrouping(partitions);

        var diskInfos = disks
            .Select(disk => BuildDisk(
                disk, physicalDiskFacts, partitionsByDiskNumber, volumesByDriveLetter,
                encryptableVolumesByDriveLetter, log))
            .ToList();

        return new StorageInfo { Disks = diskInfos, BitlockerAvailable = bitlockerAvailable };
    }

    private IReadOnlyList<CimInstance> FetchInstances(string namespaceName, string className, CollectionErrorLog log)
    {
        try
        {
            return _cim.QueryInstances(namespaceName, className);
        }
        catch (Exception ex)
        {
            log.Add(className, ex.Message);
            return [];
        }
    }

    /// <summary>
    /// True once we get any answer at all from Win32_EncryptableVolume (BitLocker's management
    /// API is present and responding), null if the query itself failed (access denied, missing
    /// namespace, ...) — there is currently no code path that reports a confirmed false.
    /// </summary>
    private (bool? Available, IReadOnlyDictionary<string, CimInstance>? ByDriveLetter) FetchEncryptableVolumes(
        CollectionErrorLog log)
    {
        try
        {
            var instances = _cim.QueryInstances(BitLockerNamespace, EncryptableVolumeSource);
            return (true, BuildDriveLetterLookup(instances));
        }
        catch (Exception ex)
        {
            log.Add(EncryptableVolumeSource, ex.Message);
            return (null, null);
        }
    }

    private static IReadOnlyDictionary<string, CimInstance> BuildDriveLetterLookup(IReadOnlyList<CimInstance> instances) =>
        instances
            .Select(i => (Letter: NormalizeDriveLetter(i.GetString("DriveLetter")), Instance: i))
            .Where(x => x.Letter is not null)
            .ToDictionary(x => x.Letter!, x => x.Instance, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<uint, List<CimInstance>> BuildPartitionGrouping(IReadOnlyList<CimInstance> partitions)
    {
        var result = new Dictionary<uint, List<CimInstance>>();
        foreach (var partition in partitions)
        {
            var diskNumber = partition.GetUInt32("DiskNumber");
            if (diskNumber is null) continue;

            if (!result.TryGetValue(diskNumber.Value, out var list))
            {
                list = [];
                result[diskNumber.Value] = list;
            }

            list.Add(partition);
        }

        return result;
    }

    private sealed record PhysicalDiskFacts(uint? MediaTypeCode, string? FirmwareVersion);

    /// <summary>Keyed by MSFT_PhysicalDisk.DeviceId parsed as an integer, matched against MSFT_Disk.Number.</summary>
    private static IReadOnlyDictionary<uint, PhysicalDiskFacts> BuildPhysicalDiskLookup(
        IReadOnlyList<CimInstance> physicalDisks)
    {
        var result = new Dictionary<uint, PhysicalDiskFacts>();
        foreach (var physicalDisk in physicalDisks)
        {
            if (uint.TryParse(physicalDisk.GetString("DeviceId"), out var id))
            {
                result[id] = new PhysicalDiskFacts(
                    physicalDisk.GetUInt32("MediaType"), physicalDisk.GetString("FirmwareVersion"));
            }
        }

        return result;
    }

    private DiskInfo BuildDisk(
        CimInstance disk,
        IReadOnlyDictionary<uint, PhysicalDiskFacts> physicalDiskFacts,
        IReadOnlyDictionary<uint, List<CimInstance>> partitionsByDiskNumber,
        IReadOnlyDictionary<string, CimInstance> volumesByDriveLetter,
        IReadOnlyDictionary<string, CimInstance>? encryptableVolumesByDriveLetter,
        CollectionErrorLog log)
    {
        var number = disk.GetUInt32("Number");
        if (number is null)
        {
            log.Add($"{DiskSource}.Number", "Disk numarası okunamadı.");
        }

        var busTypeCode = disk.GetUInt32("BusType");
        var facts = number is not null && physicalDiskFacts.TryGetValue(number.Value, out var f) ? f : null;

        var partitions = number is not null && partitionsByDiskNumber.TryGetValue(number.Value, out var parts)
            ? parts.Select(p => BuildPartition(p, volumesByDriveLetter, encryptableVolumesByDriveLetter, log)).ToList()
            : [];

        return new DiskInfo
        {
            DiskNumber = number is null ? null : (int)number.Value,
            MediaType = DecodeMediaType(busTypeCode, facts?.MediaTypeCode),
            BusType = DecodeBusType(busTypeCode, log),
            SizeBytes = disk.RequireInt64("Size", DiskSource, log),
            Model = disk.RequireString("FriendlyName", DiskSource, log),
            FirmwareVersion = facts?.FirmwareVersion,
            IsBootDisk = disk.GetBool("IsBoot"),
            PartitionStyle = DecodePartitionStyle(disk.GetUInt32("PartitionStyle")),
            Partitions = partitions
        };
    }

    private PartitionInfo BuildPartition(
        CimInstance partition,
        IReadOnlyDictionary<string, CimInstance> volumesByDriveLetter,
        IReadOnlyDictionary<string, CimInstance>? encryptableVolumesByDriveLetter,
        CollectionErrorLog log)
    {
        var driveLetter = NormalizeDriveLetter(partition.GetString("DriveLetter"));
        var sizeBytes = partition.RequireInt64("Size", PartitionSource, log);

        if (driveLetter is null)
        {
            // EFI System / Recovery / MSR partitions typically have no drive letter and no
            // mounted volume — there is genuinely nothing to read a filesystem or BitLocker
            // status from, which is a real fact, not a collection failure.
            return new PartitionInfo
            {
                Filesystem = null,
                SizeBytes = sizeBytes,
                DriveLetter = null,
                BitlockerStatus = BitLockerStatus.NotApplicable
            };
        }

        var hasVolume = volumesByDriveLetter.TryGetValue(driveLetter, out var volume);
        var filesystem = hasVolume ? volume.GetString("FileSystem") : null;
        var freeBytes = hasVolume ? volume.GetInt64("SizeRemaining") : null;

        return new PartitionInfo
        {
            Filesystem = filesystem,
            SizeBytes = sizeBytes,
            DriveLetter = driveLetter,
            BitlockerStatus = ResolveBitlockerStatus(driveLetter, encryptableVolumesByDriveLetter, log),
            FreeBytes = freeBytes
        };
    }

    private BitLockerStatus? ResolveBitlockerStatus(
        string driveLetter,
        IReadOnlyDictionary<string, CimInstance>? encryptableVolumesByDriveLetter,
        CollectionErrorLog log)
    {
        // The whole Win32_EncryptableVolume query already failed — already logged once at the
        // top level in FetchEncryptableVolumes; do not repeat that per partition.
        if (encryptableVolumesByDriveLetter is null) return null;

        if (!encryptableVolumesByDriveLetter.TryGetValue(driveLetter, out var volume))
        {
            log.Add(EncryptableVolumeSource, $"'{driveLetter}:' için BitLocker durumu bulunamadı.");
            return null;
        }

        return InvokeGetConversionStatus(volume, log);
    }

    private BitLockerStatus? InvokeGetConversionStatus(CimInstance volume, CollectionErrorLog log)
    {
        const string source = $"{EncryptableVolumeSource}.GetConversionStatus";

        try
        {
            var result = _cim.InvokeInstanceMethod(
                BitLockerNamespace, volume, "GetConversionStatus", new CimMethodParametersCollection());

            if (result.ReturnCode != 0)
            {
                log.Add(source, $"Yöntem {result.ReturnCode} dönüş koduyla başarısız oldu.");
                return null;
            }

            var conversionStatus = result.GetOutParameter("ConversionStatus");
            if (conversionStatus is null)
            {
                log.Add(source, "'ConversionStatus' değeri alınamadı.");
                return null;
            }

            // https://learn.microsoft.com/windows/win32/secprov/getconversionstatus-win32-encryptablevolume
            // The schema has no separate "paused" state, so paused-encrypting/decrypting (4/5)
            // collapse into the corresponding in-progress state.
            return Convert.ToUInt32(conversionStatus) switch
            {
                0 => BitLockerStatus.FullyDecrypted,
                1 => BitLockerStatus.FullyEncrypted,
                2 or 4 => BitLockerStatus.EncryptionInProgress,
                3 or 5 => BitLockerStatus.DecryptionInProgress,
                _ => BitLockerStatus.Unknown
            };
        }
        catch (Exception ex)
        {
            log.Add(source, ex.Message);
            return null;
        }
    }

    private static string? NormalizeDriveLetter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().TrimEnd(':').ToUpperInvariant();
    }

    /// <summary>
    /// NVMe is treated as its own media type ahead of MSFT_PhysicalDisk's SSD/HDD classification
    /// (an NVMe drive's MSFT_PhysicalDisk.MediaType is just "SSD" — this schema wants NVMe
    /// called out specifically, since bus and media are conflated for this one case).
    /// </summary>
    private static StorageMediaType? DecodeMediaType(uint? busTypeCode, uint? physicalDiskMediaTypeCode)
    {
        if (busTypeCode == 17) return StorageMediaType.NVMe;
        return physicalDiskMediaTypeCode switch
        {
            null => null,
            3 => StorageMediaType.HDD,
            4 => StorageMediaType.SSD,
            _ => StorageMediaType.Unknown
        };
    }

    private static string DecodeBusType(uint? code, CollectionErrorLog log)
    {
        if (code is null)
        {
            log.Add($"{DiskSource}.BusType", "Alan okunamadı.");
            return "";
        }

        return BusTypeNames.TryGetValue(code.Value, out var name) ? name : code.Value.ToString();
    }

    private static PartitionStyle? DecodePartitionStyle(uint? code) => code switch
    {
        null => null,
        1 => PartitionStyle.MBR,
        2 => PartitionStyle.GPT,
        _ => PartitionStyle.Unknown
    };
}
