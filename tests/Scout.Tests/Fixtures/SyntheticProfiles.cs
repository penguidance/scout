using Scout.Collector.Collectors;
using Scout.Core.Models;

namespace Scout.Tests.Fixtures;

/// <summary>
/// Hand-authored (not collected from a real machine) but schema-perfect machine profiles used as
/// Analyzer/Reporter test fixtures and written out to docs/samples/synthetic-*.json — each one
/// exercises a specific, real-world compatibility scenario. "Schema-perfect" here means: built as
/// real <see cref="MachineProfile"/> objects and only ever turned into JSON via the actual
/// <c>ProfileJsonSerializer</c>, the same serializer Scout.Collector uses — never hand-typed JSON
/// that could silently drift from the schema.
/// </summary>
internal static class SyntheticProfiles
{
    /// <summary>
    /// 2019 gaming/workstation laptop with NVIDIA Optimus-style hybrid graphics (Intel iGPU +
    /// discrete NVIDIA GTX 1650), Intel AX200 WiFi, and a Synaptics fingerprint reader.
    /// Expected verdict: NeedsAttention — driven by the NVIDIA GPU needing a proprietary driver
    /// for full function; the WiFi firmware requirement and unrecognized fingerprint reader are
    /// both lesser/unrelated findings.
    /// </summary>
    public static MachineProfile OptimusLaptop() => BuildProfile(
        machineIdSuffix: "optimus-laptop",
        manufacturer: "Dell Inc.",
        model: "G5 5590",
        hostname: "DESK-OPTIMUS",
        chassisType: ChassisType.Laptop,
        biosVendor: "Dell Inc.",
        biosVersion: "1.14.0",
        bootMode: BootMode.UEFI,
        secureBootEnabled: true,
        tpmPresent: true,
        tpmSpecVersion: "2.0",
        cpuVendor: "GenuineIntel",
        cpuModel: "Intel(R) Core(TM) i7-9750H CPU @ 2.60GHz",
        physicalCores: 6,
        logicalProcessors: 12,
        featureLevel: X86FeatureLevel.v3,
        totalMemoryBytes: 17_179_869_184, // 16 GB
        diskModel: "WD PC SN730 SDBPNTY-512G",
        diskSizeBytes: 512_110_190_592,
        diskMediaType: StorageMediaType.NVMe,
        osEdition: "Windows 10 Pro",
        osVersion: "10.0.19045",
        osBuild: "19045",
        osDisplayVersion: "22H2",
        devices:
        [
            Device(@"PCI\VEN_8086&DEV_3E9B&SUBSYS_09821028&REV_02", "Intel(R) UHD Graphics 630", "Display"),
            Device(@"PCI\VEN_10DE&DEV_1F91&SUBSYS_09821028&REV_A1", "NVIDIA GeForce GTX 1650", "Display"),
            Device(@"PCI\VEN_8086&DEV_2723&SUBSYS_00108086&REV_1A", "Intel(R) Wi-Fi 6 AX200 160MHz", "Net"),
            Device(@"USB\VID_06CB&PID_00BD&REV_0101", "Synaptics FP Sensors (WBDI)", "Biometric"),
            Device(@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0899&SUBSYS_10280899", "Realtek(R) Audio", "MEDIA"),
            Device(@"PCI\VEN_8086&DEV_A36D&SUBSYS_09821028&REV_F0", "Intel(R) USB 3.1 eXtensible Host Controller", "USB"),
            Device(@"PCI\VEN_8086&DEV_A37F&SUBSYS_09821028&REV_00", "Standard NVM Express Controller", "SCSIAdapter")
        ]);

    /// <summary>
    /// 2015-era MacBook Pro running Windows via Boot Camp: Broadcom BCM43602 WiFi/Bluetooth
    /// combo (notoriously needs MacBook-specific firmware not in the standard linux-firmware
    /// package), a plain Intel iGPU, and otherwise unremarkable Intel-chipset hardware — no T2
    /// chip or Apple Silicon complexity in scope. Expected verdict: NeedsAttention — the
    /// Broadcom chip is classified "partial" (works, but the firmware situation is harder than a
    /// generic Broadcom card), nothing is flatly unsupported.
    /// </summary>
    public static MachineProfile BroadcomMacBook() => BuildProfile(
        machineIdSuffix: "broadcom-macbook",
        manufacturer: "Apple Inc.",
        model: "MacBookPro12,1",
        hostname: "DESK-MACBOOK",
        chassisType: ChassisType.Laptop,
        biosVendor: "Apple Inc.",
        biosVersion: "MBP121.88Z.0167.B00.1810221759",
        bootMode: BootMode.UEFI,
        secureBootEnabled: false,
        tpmPresent: false, // pre-T2 Mac: no hardware TPM at all, not "unreadable"
        tpmSpecVersion: null,
        cpuVendor: "GenuineIntel",
        cpuModel: "Intel(R) Core(TM) i5-5257U CPU @ 2.70GHz",
        physicalCores: 2,
        logicalProcessors: 4,
        featureLevel: X86FeatureLevel.v3, // Broadwell
        totalMemoryBytes: 8_589_934_592, // 8 GB
        diskModel: "APPLE SSD SM0256G",
        diskSizeBytes: 251_000_193_024,
        diskMediaType: StorageMediaType.SSD,
        osEdition: "Windows 10 Home",
        osVersion: "10.0.19045",
        osBuild: "19045",
        osDisplayVersion: "22H2",
        devices:
        [
            Device(@"PCI\VEN_14E4&DEV_43BA&SUBSYS_92A114E4&REV_03", "Broadcom 802.11ac Network Adapter", "Net"),
            Device(@"PCI\VEN_8086&DEV_1626&SUBSYS_00001028&REV_09", "Intel(R) HD Graphics 6000", "Display"),
            Device(@"PCI\VEN_8086&DEV_9CB1&SUBSYS_00001028&REV_03", "Intel(R) USB 3.0 eXtensible Host Controller", "USB"),
            Device(@"PCI\VEN_8086&DEV_9C03&SUBSYS_00001028&REV_03", "Standard NVM Express Controller", "SCSIAdapter"),
            Device(@"USB\VID_05AC&PID_0273&REV_0224", "Apple Multitouch Trackpad", "HIDClass")
        ]);

    /// <summary>
    /// 2012 AMD-everything desktop: an AMD Radeon HD 7750 (Cape Verde, GCN 1.0 / "Southern
    /// Islands"), BIOS (Legacy) boot, no TPM. Exists specifically to prove the AMD vendor-wide
    /// "amdgpu" rule does NOT swallow this generation — see the device_id-range entry in
    /// data/hardware-compatibility.json and docs/schema/profile-v0.1.md. Every device here
    /// resolves natively (the regression check is that the matched driver is specifically
    /// "radeon", not "amdgpu"), but the machine itself is not a clean Ready: its CPU is
    /// x86-64-v2 (pre-AVX2 Piledriver) and it boots Legacy/BIOS, both soft
    /// <see cref="Scout.Analyzer.Analysis.SystemConstraint"/> findings — so the expected overall
    /// verdict is MinorIssues, floored there by those system-level constraints rather than by
    /// anything device-related.
    /// </summary>
    public static MachineProfile OldRadeonDesktop() => BuildProfile(
        machineIdSuffix: "old-radeon",
        manufacturer: "Gigabyte Technology Co., Ltd.",
        model: "GA-970A-UD3",
        hostname: "DESK-OLDAMD",
        chassisType: ChassisType.Desktop,
        biosVendor: "American Megatrends Inc.",
        biosVersion: "F9",
        bootMode: BootMode.Legacy,
        secureBootEnabled: null, // not applicable in Legacy mode
        tpmPresent: false, // confirmed absent — this board has no TPM header populated
        tpmSpecVersion: null,
        cpuVendor: "AuthenticAMD",
        cpuModel: "AMD FX-8350 Eight-Core Processor",
        physicalCores: 8,
        logicalProcessors: 8,
        featureLevel: X86FeatureLevel.v2, // Piledriver — pre-AVX2/BMI2
        totalMemoryBytes: 17_179_869_184, // 16 GB
        diskModel: "WDC WD10EZEX-08WN4A0",
        diskSizeBytes: 1_000_204_886_016,
        diskMediaType: StorageMediaType.HDD,
        osEdition: "Windows 10 Pro",
        osVersion: "10.0.19045",
        osBuild: "19045",
        osDisplayVersion: "22H2",
        devices:
        [
            Device(@"PCI\VEN_1002&DEV_683F&SUBSYS_20521458&REV_00", "AMD Radeon HD 7750", "Display"),
            Device(@"PCI\VEN_10EC&DEV_8168&SUBSYS_E0001458&REV_06", "Realtek PCIe GbE Family Controller", "Net"),
            Device(@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0662&SUBSYS_14583602", "Realtek High Definition Audio", "MEDIA"),
            Device(@"PCI\VEN_8086&DEV_1E31&SUBSYS_20521458&REV_04", "Intel(R) USB 3.0 eXtensible Host Controller", "USB")
        ]);

    private static DeviceInfo Device(string rawHardwareId, string friendlyName, string deviceClass)
    {
        var parsed = HardwareIdParser.Parse(rawHardwareId);
        return new DeviceInfo
        {
            HardwareId = parsed.NormalizedId,
            HardwareIdRaw = rawHardwareId,
            VendorId = parsed.VendorId,
            DeviceId = parsed.DeviceId,
            BusType = parsed.BusType,
            CompatibleIds = [],
            FriendlyName = friendlyName,
            Class = deviceClass,
            Status = DeviceStatus.OK
        };
    }

    private static MachineProfile BuildProfile(
        string machineIdSuffix,
        string manufacturer,
        string model,
        string hostname,
        ChassisType chassisType,
        string biosVendor,
        string biosVersion,
        BootMode bootMode,
        bool? secureBootEnabled,
        bool tpmPresent,
        string? tpmSpecVersion,
        string cpuVendor,
        string cpuModel,
        int physicalCores,
        int logicalProcessors,
        X86FeatureLevel featureLevel,
        long totalMemoryBytes,
        string diskModel,
        long diskSizeBytes,
        StorageMediaType diskMediaType,
        string osEdition,
        string osVersion,
        string osBuild,
        string osDisplayVersion,
        IReadOnlyList<DeviceInfo> devices)
    {
        var gpus = devices
            .Where(d => d.Class == "Display")
            .Select(d => new GpuInfo
            {
                HardwareId = d.HardwareId,
                VendorId = d.VendorId,
                DeviceId = d.DeviceId,
                FriendlyName = d.FriendlyName
            })
            .ToList();

        return new MachineProfile
        {
            SchemaVersion = "0.1",
            CollectorVersion = "synthetic",
            CollectedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            MachineId = $"sha256:synthetic-{machineIdSuffix}",
            Privacy = new PrivacyInfo
            {
                HostnameIncluded = true,
                UsernameIncluded = false,
                SerialNumbersIncluded = false,
                RedactedFields = ["system.serial_number"]
            },
            System = new SystemInfo
            {
                Manufacturer = manufacturer,
                Model = model,
                ChassisType = chassisType,
                Hostname = hostname
            },
            Firmware = new FirmwareInfo
            {
                BiosVendor = biosVendor,
                BiosVersion = biosVersion,
                BootMode = bootMode,
                SecureBootEnabled = secureBootEnabled,
                Tpm = new TpmInfo { Present = tpmPresent, SpecVersion = tpmSpecVersion }
            },
            Cpu = new CpuInfo
            {
                Vendor = cpuVendor,
                Model = cpuModel,
                PhysicalCores = physicalCores,
                LogicalProcessors = logicalProcessors,
                Architecture = CpuArchitecture.x86_64,
                X86_64FeatureLevel = featureLevel
            },
            Memory = new MemoryInfo
            {
                TotalBytes = totalMemoryBytes,
                Modules = []
            },
            Storage = new StorageInfo
            {
                BitlockerAvailable = null,
                Disks =
                [
                    new DiskInfo
                    {
                        DiskNumber = 0,
                        MediaType = diskMediaType,
                        BusType = diskMediaType == StorageMediaType.NVMe ? "NVMe" : "SATA",
                        SizeBytes = diskSizeBytes,
                        Model = diskModel,
                        IsBootDisk = true,
                        PartitionStyle = PartitionStyle.GPT,
                        Partitions =
                        [
                            new PartitionInfo
                            {
                                Filesystem = "NTFS",
                                SizeBytes = diskSizeBytes - 500_000_000,
                                DriveLetter = "C",
                                BitlockerStatus = BitLockerStatus.NotApplicable,
                                // Comfortably above VerdictEngine's default 25 GiB free-space
                                // threshold for every profile built here — none of these fixtures
                                // exists to exercise the low-disk-space constraint.
                                FreeBytes = (diskSizeBytes - 500_000_000) / 2
                            }
                        ]
                    }
                ]
            },
            Devices = devices,
            GpuTopology = new GpuTopology
            {
                Gpus = gpus,
                Layout = gpus.Count switch
                {
                    0 => null,
                    1 => GpuLayout.Single,
                    _ => GpuLayout.Hybrid
                }
            },
            Os = new OsInfo
            {
                Edition = osEdition,
                Version = osVersion,
                Build = osBuild,
                DisplayVersion = osDisplayVersion,
                Architecture = CpuArchitecture.x86_64,
                Language = "tr-TR"
            },
            Software = [],
            SoftwareFilteredCount = 0,
            Peripherals = [],
            CollectionErrors = []
        };
    }
}
