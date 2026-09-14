namespace Scout.Core.Models;

// Enum member names in this file are deliberately spelled to match the wire-format strings in
// docs/schema/profile-v0.1.md exactly (via the default System.Text.Json JsonStringEnumConverter,
// configured in ProfileJsonSerializer). Where the schema uses non-PascalCase text (acronyms,
// "x86_64", feature-level tags), the member name follows the schema instead of C# naming
// conventions, so the enum is a literal, greppable mirror of the schema — not a re-encoding of it.

/// <summary>
/// System enclosure category, normalized from <c>Win32_SystemEnclosure.ChassisTypes</c>.
/// </summary>
public enum ChassisType
{
    Unknown,
    Desktop,
    Laptop,
    Tablet,
    Server,
    AllInOne,
    Other
}

/// <summary>
/// Firmware boot mode. No "Unknown" member: "could not determine" is represented by a null
/// <see cref="FirmwareInfo.BootMode"/>, since every value this enum could take (UEFI or Legacy)
/// is itself a positive determination — there is no third "recognized but uncategorized" case.
/// </summary>
public enum BootMode
{
    UEFI,
    Legacy
}

/// <summary>CPU instruction set architecture.</summary>
public enum CpuArchitecture
{
    Unknown,
    x86,
    x86_64,
    arm64
}

/// <summary>
/// x86-64 microarchitecture feature level (v1-v4), as used by glibc/Linux distributions
/// to describe minimum CPU requirements. See profile-v0.1.md for the source of this value.
/// </summary>
public enum X86FeatureLevel
{
    v1,
    v2,
    v3,
    v4
}

/// <summary>Physical storage media type.</summary>
public enum StorageMediaType
{
    Unknown,
    HDD,
    SSD,
    NVMe
}

/// <summary>Disk partition table style.</summary>
public enum PartitionStyle
{
    Unknown,
    GPT,
    MBR,
    RAW
}

/// <summary>BitLocker conversion status for a volume, from <c>Win32_EncryptableVolume.GetConversionStatus</c>.</summary>
public enum BitLockerStatus
{
    Unknown,
    NotApplicable,
    FullyDecrypted,
    FullyEncrypted,
    EncryptionInProgress,
    DecryptionInProgress
}

/// <summary>
/// PnP device status, derived from <c>Win32_PnPEntity.ConfigManagerErrorCode</c> (0 = OK, any
/// other code = Error in this version — see <c>DeviceCollector</c>). <c>Unknown</c>/<c>Degraded</c>
/// are reserved for a future, finer-grained mapping of the ~30 documented CM_PROB_* codes; the
/// current collector does not produce them. "Could not read the code at all" is a null
/// <see cref="DeviceInfo.Status"/>, not <c>Unknown</c>.
/// </summary>
public enum DeviceStatus
{
    Unknown,
    OK,
    Error,
    Degraded
}

/// <summary>
/// GPU topology layout, derived from the "Display"-class entries in <c>devices[]</c> — see
/// <c>GpuTopologyCollector</c>. No "Unknown" member: "could not determine" (0 GPUs, or 2+ GPUs
/// that could not be confidently classified as integrated/discrete) is a null
/// <see cref="GpuTopology.Layout"/>.
/// </summary>
public enum GpuLayout
{
    Single,
    Hybrid,
    MultiDiscrete
}

/// <summary>Physical/logical bus a PnP device is enumerated on, from the prefix of its hardware ID (e.g. "PCI\...").</summary>
public enum DeviceBusType
{
    PCI,
    USB,
    ACPI,
    Other
}

/// <summary>Which registry view (32-bit vs. native) a software inventory entry was read from.</summary>
public enum RegistryView
{
    Native,
    Wow6432Node
}

/// <summary>
/// Where a <see cref="SoftwareEntry"/> was read from. Separate from <see cref="RegistryView"/>
/// (which only describes 32-bit vs. 64-bit registry redirection): this instead distinguishes the
/// registry hive from the non-registry MSIX/Store source, which has no registry view at all.
/// </summary>
public enum SoftwareSource
{
    /// <summary>A per-machine Uninstall key (<c>HKLM\...\Uninstall</c> or its <c>WOW6432Node</c> counterpart).</summary>
    HklmUninstallKey,

    /// <summary>A per-user Uninstall key (<c>HKCU\...\Uninstall</c>) — visible only for the collecting user's own installs.</summary>
    HkcuUninstallKey,

    /// <summary>An MSIX/Store package, from <c>Win32_InstalledStoreProgram</c> — never appears in the registry Uninstall keys at all.</summary>
    StorePackage
}

/// <summary>
/// Coarse classification of a <see cref="SoftwareEntry"/>, so a report can fold away the
/// less-interesting categories (a machine easily has a dozen VC++ Redistributable entries) without
/// discarding them outright — see <c>SoftwareCollector</c>'s (configurable) keyword lists. Lowercase
/// member names to match the wire-format strings, same convention as <see cref="X86FeatureLevel"/>.
/// </summary>
public enum SoftwareCategory
{
    /// <summary>The default: a normal, user-installed program. Nothing else matched.</summary>
    application,

    /// <summary>A shared runtime/redistributable (VC++ Redistributable, .NET Runtime, DirectX, ...), not an application in its own right.</summary>
    runtime,

    /// <summary>A device driver package.</summary>
    driver,

    /// <summary>A Microsoft-published system utility that is neither a typical application nor a runtime/driver.</summary>
    system
}

/// <summary>Physical/logical connection type for a peripheral.</summary>
public enum ConnectionType
{
    Unknown,
    USB,
    Bluetooth,
    PS2,
    Internal
}
