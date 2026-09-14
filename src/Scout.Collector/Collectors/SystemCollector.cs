using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects <see cref="SystemInfo"/> from Win32_ComputerSystem, Win32_SystemEnclosure and
/// Win32_BIOS (see docs/schema/profile-v0.1.md — "Kaynak eşlemesi").
/// </summary>
public sealed class SystemCollector : ISectionCollector<SystemInfo>
{
    private readonly ICimQueryExecutor _cim;
    private readonly bool _includeIdentifiers;
    private readonly TimeProvider _clock;

    /// <param name="includeIdentifiers">
    /// Privacy gate for <see cref="SystemInfo.Hostname"/> and <see cref="SystemInfo.SerialNumber"/>.
    /// Defaults to false (not collected) — the caller must opt in (e.g. via <c>--include-identifiers</c>)
    /// to have these read at all.
    /// </param>
    public SystemCollector(ICimQueryExecutor cim, bool includeIdentifiers = false, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _includeIdentifiers = includeIdentifiers;
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "system";

    public SystemInfo Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);

        var computerSystem = CimInstanceFetcher.TryGetFirst(_cim, "root/cimv2", "Win32_ComputerSystem", log);
        var enclosure = CimInstanceFetcher.TryGetFirst(_cim, "root/cimv2", "Win32_SystemEnclosure", log);
        var bios = CimInstanceFetcher.TryGetFirst(_cim, "root/cimv2", "Win32_BIOS", log);

        return new SystemInfo
        {
            Manufacturer = computerSystem.RequireString("Manufacturer", "Win32_ComputerSystem", log),
            Model = computerSystem.RequireString("Model", "Win32_ComputerSystem", log),
            Sku = computerSystem.GetString("SystemSKUNumber"),
            ChassisType = MapChassisType(enclosure, log),
            // Not read at all unless the caller opted in — see _includeIdentifiers.
            SerialNumber = _includeIdentifiers ? bios.GetString("SerialNumber") : null,
            Hostname = _includeIdentifiers ? computerSystem.GetString("DNSHostName") : null
        };
    }

    /// <summary>
    /// Normalizes the raw Win32_SystemEnclosure.ChassisTypes code into the schema's
    /// <see cref="ChassisType"/> category. This is a coding→label conversion, not a
    /// compatibility judgment — see docs/schema/profile-v0.1.md.
    /// </summary>
    /// <remarks>
    /// Null means the enclosure could not be read at all (no signal whatsoever).
    /// <see cref="ChassisType.Unknown"/> is a real, distinct observation: SMBIOS chassis type
    /// code 2 literally means "the firmware itself does not know the chassis type". Any other
    /// recognized-but-uncommon code (rack-mount subtypes, blades, ...) falls back to
    /// <see cref="ChassisType.Other"/> rather than being conflated with either of those.
    /// </remarks>
    private static ChassisType? MapChassisType(CimInstance? enclosure, CollectionErrorLog log)
    {
        if (enclosure is null) return null;

        var types = enclosure.GetUInt16Array("ChassisTypes");
        if (types is not { Length: > 0 })
        {
            log.Add("Win32_SystemEnclosure.ChassisTypes", "Kasa tipi kodu okunamadı.");
            return null;
        }

        // https://learn.microsoft.com/windows/win32/cimwin32prov/win32-systemenclosure — ChassisTypes.
        // Bit 7 is a "chassis lock present" flag, not part of the type code itself.
        var code = types[0] & 0x7F;
        return code switch
        {
            1 => ChassisType.Other,
            2 => ChassisType.Unknown, // SMBIOS's own "the firmware doesn't know" code.
            8 or 9 or 10 or 14 or 18 or 21 => ChassisType.Laptop,
            30 or 31 or 32 => ChassisType.Tablet,
            13 => ChassisType.AllInOne,
            17 or 23 or 28 => ChassisType.Server,
            3 or 4 or 5 or 6 or 7 or 15 or 16 => ChassisType.Desktop,
            _ => ChassisType.Other
        };
    }
}
