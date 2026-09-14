using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>BIOS/UEFI firmware facts, from Win32_BIOS and the SecureBoot/TPM registry and WMI surfaces.</summary>
public sealed class FirmwareInfo
{
    [JsonPropertyName("bios_vendor")]
    public required string BiosVendor { get; init; }

    [JsonPropertyName("bios_version")]
    public required string BiosVersion { get; init; }

    [JsonPropertyName("bios_release_date")]
    public DateOnly? BiosReleaseDate { get; init; }

    /// <summary>Null when the boot mode could not be determined at all (e.g. the registry read failed).</summary>
    [JsonPropertyName("boot_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required BootMode? BootMode { get; init; }

    /// <summary>Null when the machine boots Legacy and the setting does not apply.</summary>
    [JsonPropertyName("secure_boot_enabled")]
    public bool? SecureBootEnabled { get; init; }

    [JsonPropertyName("tpm")]
    public required TpmInfo Tpm { get; init; }
}
