using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects <see cref="FirmwareInfo"/> from Win32_BIOS, the Win32_Tpm WMI class, and (via the
/// StdRegProv WMI class, so the read still works remotely over WS-Man) the SecureBoot state
/// registry key — see docs/schema/profile-v0.1.md — "Kaynak eşlemesi".
/// </summary>
public sealed class FirmwareCollector : ISectionCollector<FirmwareInfo>
{
    private const uint HKeyLocalMachine = 0x80000002;
    private const string SecureBootStateKeyPath = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
    private const string SecureBootEnabledValueName = "UEFISecureBootEnabled";
    private const string SecureBootSource = @"StdRegProv.GetDWORDValue(SecureBoot\State\UEFISecureBootEnabled)";
    private const string TpmSpecVersionSource = "Win32_Tpm.SpecVersion";

    private readonly ICimQueryExecutor _cim;
    private readonly TimeProvider _clock;

    public FirmwareCollector(ICimQueryExecutor cim, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "firmware";

    public FirmwareInfo Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);

        var bios = CimInstanceFetcher.TryGetFirst(_cim, "root/cimv2", "Win32_BIOS", log);
        var (tpmStatus, tpm) = CimInstanceFetcher.TryGetOptionalSingleton(
            _cim, "root/cimv2/Security/MicrosoftTpm", "Win32_Tpm", log);
        var (bootMode, secureBootEnabled) = ReadBootMode(log);
        var (specVersion, manufacturerVersion, specVersionRaw) = ParseTpmSpecVersion(tpm, log);

        return new FirmwareInfo
        {
            BiosVendor = bios.RequireString("Manufacturer", "Win32_BIOS", log),
            BiosVersion = bios.RequireString("SMBIOSBIOSVersion", "Win32_BIOS", log),
            BiosReleaseDate = bios.GetDateOnly("ReleaseDate"),
            BootMode = bootMode,
            SecureBootEnabled = secureBootEnabled,
            Tpm = new TpmInfo
            {
                // Present is only ever false for a confirmed-empty query (no TPM chip). A query
                // failure (access denied, namespace missing, ...) maps to null, never false.
                Present = tpmStatus switch
                {
                    CimSingletonStatus.Present => true,
                    CimSingletonStatus.Absent => false,
                    _ => null
                },
                SpecVersion = specVersion,
                ManufacturerVersion = manufacturerVersion,
                SpecVersionRaw = specVersionRaw,
                Ready = CombineTpmReady(tpm)
            }
        };
    }

    private static bool? CombineTpmReady(CimInstance? tpm)
    {
        if (tpm is null) return null;

        var enabled = tpm.GetBool("IsEnabled_InitialValue");
        var activated = tpm.GetBool("IsActivated_InitialValue");
        if (enabled is null || activated is null) return null;

        return enabled.Value && activated.Value;
    }

    /// <summary>
    /// Splits the raw <c>Win32_Tpm.SpecVersion</c> value (e.g. "2.0, 0, 1.38") into spec version,
    /// (unnamed) revision, and manufacturer firmware version. Only the first and third
    /// components are surfaced as their own fields; the raw string is always kept in
    /// <c>spec_version_raw</c> — per the schema's general "never delete a raw value, only add a
    /// parsed one alongside it" principle — even when parsing fails.
    /// </summary>
    private static (string? SpecVersion, string? ManufacturerVersion, string? Raw) ParseTpmSpecVersion(
        CimInstance? tpm, CollectionErrorLog log)
    {
        var raw = tpm?.GetString("SpecVersion");
        if (raw is null) return (null, null, null);

        var parts = raw.Split(',');
        if (parts.Length == 3)
        {
            var specVersion = parts[0].Trim();
            var manufacturerVersion = parts[2].Trim();
            return (
                specVersion.Length > 0 ? specVersion : null,
                manufacturerVersion.Length > 0 ? manufacturerVersion : null,
                raw);
        }

        log.Add(TpmSpecVersionSource, $"Beklenmeyen biçim (\"sürüm, revizyon, üretici sürümü\" bekleniyordu): '{raw}'.");
        return (null, null, raw);
    }

    /// <summary>
    /// Reads the SecureBoot state via StdRegProv instead of the local Registry API, so the same
    /// call works against a remote target over the same CimSession/WS-Man connection.
    /// </summary>
    /// <remarks>
    /// Heuristic: <c>SYSTEM\CurrentControlSet\Control\SecureBoot\State\UEFISecureBootEnabled</c>
    /// only exists when the firmware booted in UEFI mode, so a "not found" result is read as
    /// Legacy/CSM boot. This cannot fully distinguish "Legacy" from "UEFI without a readable
    /// SecureBoot state key" — an edge case worth revisiting if it shows up in practice.
    /// </remarks>
    private (BootMode? Mode, bool? SecureBootEnabled) ReadBootMode(CollectionErrorLog log)
    {
        try
        {
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("hDefKey", HKeyLocalMachine, CimType.UInt32, CimFlags.In),
                CimMethodParameter.Create("sSubKeyName", SecureBootStateKeyPath, CimType.String, CimFlags.In),
                CimMethodParameter.Create("sValueName", SecureBootEnabledValueName, CimType.String, CimFlags.In)
            };

            var result = _cim.InvokeStaticMethod("root/cimv2", "StdRegProv", "GetDWORDValue", parameters);

            switch (result.ReturnCode)
            {
                case 0:
                    var outValue = result.GetOutParameter("uValue");
                    if (outValue is not null)
                    {
                        return (BootMode.UEFI, Convert.ToUInt32(outValue) == 1);
                    }

                    log.Add(SecureBootSource, "Anahtar okundu ancak 'uValue' değeri alınamadı.");
                    return (null, null);

                case 2: // ERROR_FILE_NOT_FOUND — key/value absent, heuristically Legacy boot.
                    return (BootMode.Legacy, null);

                default:
                    log.Add(SecureBootSource, $"Registry sorgusu {result.ReturnCode} dönüş koduyla başarısız oldu.");
                    return (null, null);
            }
        }
        catch (Exception ex)
        {
            log.Add(SecureBootSource, ex.Message);
            return (null, null);
        }
    }
}
