using Scout.Core.Models;
using Scout.Core.Serialization;
using Xunit;

namespace Scout.Tests;

/// <summary>
/// Smoke tests for the MachineProfile model + serializer scaffold. No collection logic is
/// exercised here — these just confirm the schema-v0.1 model shape round-trips through JSON
/// with the expected wire-format casing (see docs/schema/profile-v0.1.md).
/// </summary>
public class ProfileJsonSerializerTests
{
    private static MachineProfile CreateMinimalProfile() => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "0.1.0",
        CollectedAt = new DateTimeOffset(2026, 9, 12, 8, 30, 0, TimeSpan.Zero),
        MachineId = new string('a', 64),
        Privacy = new PrivacyInfo
        {
            HostnameIncluded = false,
            UsernameIncluded = false,
            SerialNumbersIncluded = false,
            RedactedFields = ["system.serial_number", "system.hostname"]
        },
        System = new SystemInfo
        {
            Manufacturer = "Contoso",
            Model = "Latitude 9999",
            ChassisType = ChassisType.Laptop
        },
        Firmware = new FirmwareInfo
        {
            BiosVendor = "Contoso",
            BiosVersion = "1.2.3",
            BootMode = BootMode.UEFI,
            SecureBootEnabled = true,
            Tpm = new TpmInfo
            {
                Present = true,
                SpecVersion = "2.0",
                ManufacturerVersion = "1.16",
                SpecVersionRaw = "2.0, 0, 1.16",
                Ready = true
            }
        },
        Cpu = new CpuInfo
        {
            Vendor = "GenuineIntel",
            Model = "Intel(R) Core(TM) i7",
            PhysicalCores = 8,
            LogicalProcessors = 16,
            Architecture = CpuArchitecture.x86_64,
            X86_64FeatureLevel = X86FeatureLevel.v3
        },
        Memory = new MemoryInfo
        {
            TotalBytes = 34_359_738_368,
            Modules = [new MemoryModule { CapacityBytes = 17_179_869_184, RatedSpeedMhz = 3200, ConfiguredSpeedMhz = 3200 }]
        },
        Storage = new StorageInfo
        {
            BitlockerAvailable = true,
            Disks =
            [
                new DiskInfo
                {
                    DiskNumber = 0,
                    MediaType = StorageMediaType.NVMe,
                    BusType = "NVMe",
                    SizeBytes = 1_000_204_886_016,
                    Model = "Contoso NVMe SSD",
                    IsBootDisk = true,
                    PartitionStyle = PartitionStyle.GPT,
                    Partitions =
                    [
                        new PartitionInfo
                        {
                            Filesystem = "NTFS",
                            SizeBytes = 999_999_999_999,
                            DriveLetter = "C",
                            BitlockerStatus = BitLockerStatus.FullyEncrypted
                        }
                    ]
                }
            ]
        },
        Devices =
        [
            new DeviceInfo
            {
                HardwareId = @"PCI\VEN_8086&DEV_24FD",
                HardwareIdRaw = @"PCI\VEN_8086&DEV_24FD&SUBSYS_00108086&REV_29",
                VendorId = "8086",
                DeviceId = "24FD",
                BusType = DeviceBusType.PCI,
                CompatibleIds = [@"PCI\VEN_8086&DEV_24FD"],
                FriendlyName = "Intel(R) Wireless-AC 9560",
                Class = "Net",
                Status = DeviceStatus.OK
            }
        ],
        GpuTopology = new GpuTopology
        {
            Layout = GpuLayout.Single,
            Gpus = [new GpuInfo { HardwareId = @"PCI\VEN_8086&DEV_9BC4", VendorId = "8086", DeviceId = "9BC4", FriendlyName = "Intel(R) UHD Graphics 630" }]
        },
        Os = new OsInfo
        {
            Edition = "Windows 11 Pro",
            Version = "10.0.26200",
            Build = "26200",
            DisplayVersion = "24H2",
            Architecture = CpuArchitecture.x86_64
        },
        Software =
        [
            new SoftwareEntry
            {
                Name = "7-Zip",
                Version = "23.01",
                RegistryView = RegistryView.Native,
                Source = SoftwareSource.HklmUninstallKey,
                Category = SoftwareCategory.application,
                SystemComponent = false
            }
        ],
        SoftwareFilteredCount = 3,
        Peripherals =
        [
            new PeripheralInfo
            {
                HardwareId = @"USB\VID_046D&PID_C52B",
                HardwareIdRaw = @"USB\VID_046D&PID_C52B&REV_0110",
                VendorId = "046D",
                DeviceId = "C52B",
                BusType = DeviceBusType.USB,
                CompatibleIds = [@"USB\VID_046D&PID_C52B"],
                FriendlyName = "Logitech USB Receiver",
                Class = "HIDClass",
                ConnectionType = ConnectionType.USB
            }
        ],
        CollectionErrors = []
    };

    [Fact]
    public void Serialize_ProducesExpectedWireFormatCasingForEnums()
    {
        var json = ProfileJsonSerializer.Serialize(CreateMinimalProfile());

        Assert.Contains("\"boot_mode\": \"UEFI\"", json);
        Assert.Contains("\"architecture\": \"x86_64\"", json);
        Assert.Contains("\"x86_64_feature_level\": \"v3\"", json);
        Assert.Contains("\"media_type\": \"NVMe\"", json);
        Assert.Contains("\"partition_style\": \"GPT\"", json);
        Assert.Contains("\"status\": \"OK\"", json);
        Assert.Contains("\"connection_type\": \"USB\"", json);
    }

    [Fact]
    public void RoundTrip_PreservesMatchingKeysNotFriendlyNames()
    {
        var original = CreateMinimalProfile();

        var json = ProfileJsonSerializer.Serialize(original);
        var roundTripped = ProfileJsonSerializer.Deserialize(json);

        Assert.Equal(original.Devices[0].HardwareId, roundTripped.Devices[0].HardwareId);
        Assert.Equal(original.Devices[0].FriendlyName, roundTripped.Devices[0].FriendlyName);
        Assert.Equal(original.GpuTopology.Gpus[0].HardwareId, roundTripped.GpuTopology.Gpus[0].HardwareId);
        Assert.Equal(original.SchemaVersion, roundTripped.SchemaVersion);
    }

    /// <summary>
    /// Regression test: a `required` C# member forces System.Text.Json's deserializer to demand
    /// the JSON key be present, even as `null` — but ProfileJsonSerializer's global
    /// DefaultIgnoreCondition = WhenWritingNull would otherwise omit that same key when the value
    /// is null, so Collector's own output became unreadable by its own deserializer for every
    /// `required <T>?` property once its value was legitimately null (exactly the common case
    /// this schema's "never fabricate a sentinel" principle produces). Caught via a real
    /// non-admin sample profile, not by the pre-existing round-trip test (which only ever used
    /// non-null values) — this test exercises the null case for every such property so a
    /// regression here fails loudly instead of only showing up against a real captured profile.
    /// </summary>
    [Fact]
    public void RoundTrip_RequiredNullableFieldsSurviveWhenTheirValueIsNull()
    {
        var original = CreateProfileWithAllRequiredNullableFieldsSetToNull();

        var json = ProfileJsonSerializer.Serialize(original);
        var roundTripped = ProfileJsonSerializer.Deserialize(json);

        Assert.Null(roundTripped.Firmware.Tpm.Present);
        Assert.Null(roundTripped.Storage.BitlockerAvailable);
        Assert.Null(roundTripped.Cpu.PhysicalCores);
        Assert.Null(roundTripped.Cpu.LogicalProcessors);
        Assert.Null(roundTripped.Cpu.Architecture);
        Assert.Null(roundTripped.System.ChassisType);
        Assert.Null(roundTripped.Firmware.BootMode);
        Assert.Null(roundTripped.Memory.TotalBytes);
        Assert.Null(roundTripped.Memory.Modules[0].CapacityBytes);
        Assert.Null(roundTripped.Os.Architecture);
        Assert.Null(roundTripped.GpuTopology.Layout);
        Assert.Null(roundTripped.Storage.Disks[0].DiskNumber);
        Assert.Null(roundTripped.Storage.Disks[0].MediaType);
        Assert.Null(roundTripped.Storage.Disks[0].SizeBytes);
        Assert.Null(roundTripped.Storage.Disks[0].IsBootDisk);
        Assert.Null(roundTripped.Storage.Disks[0].PartitionStyle);
        Assert.Null(roundTripped.Storage.Disks[0].Partitions[0].SizeBytes);
        Assert.Null(roundTripped.Storage.Disks[0].Partitions[0].BitlockerStatus);
        Assert.Null(roundTripped.Devices[0].Status);
    }

    // Models are plain classes (not records), so this cannot reuse CreateMinimalProfile via a
    // `with` expression — it duplicates the same shape, swapping in null for every property
    // covered by the JsonIgnore(Never) fix.
    private static MachineProfile CreateProfileWithAllRequiredNullableFieldsSetToNull() => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "0.1.0",
        CollectedAt = new DateTimeOffset(2026, 9, 12, 8, 30, 0, TimeSpan.Zero),
        MachineId = new string('a', 64),
        Privacy = new PrivacyInfo
        {
            HostnameIncluded = false,
            UsernameIncluded = false,
            SerialNumbersIncluded = false,
            RedactedFields = ["system.serial_number", "system.hostname"]
        },
        System = new SystemInfo
        {
            Manufacturer = "Contoso",
            Model = "Latitude 9999",
            ChassisType = null
        },
        Firmware = new FirmwareInfo
        {
            BiosVendor = "Contoso",
            BiosVersion = "1.2.3",
            BootMode = null,
            Tpm = new TpmInfo { Present = null }
        },
        Cpu = new CpuInfo
        {
            Vendor = "GenuineIntel",
            Model = "Intel(R) Core(TM) i7",
            PhysicalCores = null,
            LogicalProcessors = null,
            Architecture = null
        },
        Memory = new MemoryInfo
        {
            TotalBytes = null,
            Modules = [new MemoryModule { CapacityBytes = null }]
        },
        Storage = new StorageInfo
        {
            BitlockerAvailable = null,
            Disks =
            [
                new DiskInfo
                {
                    DiskNumber = null,
                    MediaType = null,
                    BusType = "NVMe",
                    SizeBytes = null,
                    Model = "Contoso NVMe SSD",
                    IsBootDisk = null,
                    PartitionStyle = null,
                    Partitions =
                    [
                        new PartitionInfo
                        {
                            SizeBytes = null,
                            BitlockerStatus = null
                        }
                    ]
                }
            ]
        },
        Devices =
        [
            new DeviceInfo
            {
                HardwareId = @"PCI\VEN_8086&DEV_24FD",
                HardwareIdRaw = @"PCI\VEN_8086&DEV_24FD&SUBSYS_00108086&REV_29",
                BusType = DeviceBusType.PCI,
                CompatibleIds = [],
                FriendlyName = "Intel(R) Wireless-AC 9560",
                Class = "Net",
                Status = null
            }
        ],
        GpuTopology = new GpuTopology { Gpus = [], Layout = null },
        Os = new OsInfo
        {
            Edition = "Windows 11 Pro",
            Version = "10.0.26200",
            Build = "26200",
            Architecture = null
        },
        Software = [],
        SoftwareFilteredCount = 0,
        Peripherals = [],
        CollectionErrors = []
    };
}
