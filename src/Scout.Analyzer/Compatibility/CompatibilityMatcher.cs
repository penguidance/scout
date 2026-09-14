using System.Globalization;
using Scout.Core.Models;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// Looks a device up in a <see cref="CompatibilityDatabase"/>: exact vendor_id+device_id, then a
/// device_id-generation range, then a vendor-wide fallback, then <see cref="MatchLevel.None"/>.
/// Never guesses — a miss is reported as <see cref="MatchLevel.None"/> /
/// <see cref="SupportLevel.Unknown"/>, not inferred from a similar device or a partial ID match.
/// </summary>
public sealed class CompatibilityMatcher
{
    private readonly CompatibilityDatabase _database;

    public CompatibilityMatcher(CompatibilityDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public CompatibilityMatch Match(DeviceInfo device)
    {
        if (device.VendorId is null)
        {
            // No vendor ID at all (e.g. an ACPI PNP ID) — nothing to look up.
            return None();
        }

        var exact = device.DeviceId is not null ? FindExact(device.VendorId, device.DeviceId) : null;
        if (exact is not null)
        {
            return new CompatibilityMatch { Level = MatchLevel.Exact, Entry = exact, Support = exact.Support };
        }

        var ranged = device.DeviceId is not null ? FindInRange(device.VendorId, device.DeviceId) : null;
        if (ranged is not null)
        {
            return new CompatibilityMatch { Level = MatchLevel.RangeMatch, Entry = ranged, Support = ranged.Support };
        }

        var vendorWide = FindVendorWide(device.VendorId, device.Class);
        if (vendorWide is not null)
        {
            return new CompatibilityMatch { Level = MatchLevel.VendorFallback, Entry = vendorWide, Support = vendorWide.Support };
        }

        return None();
    }

    private CompatibilityEntry? FindExact(string vendorId, string deviceId)
    {
        foreach (var entry in _database.Entries)
        {
            if (entry.DeviceId is not null &&
                string.Equals(entry.VendorId, vendorId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks device_id-range entries for this vendor — see
    /// <see cref="CompatibilityEntry.DeviceIdRangeStart"/> for why these exist (a vendor's
    /// device IDs clustering by hardware generation, where one blanket vendor-wide rule would be
    /// wrong for part of the range). A malformed device_id (not valid hex) or range bound simply
    /// cannot match, rather than throwing — the caller falls through to the next tier.
    /// </summary>
    private CompatibilityEntry? FindInRange(string vendorId, string deviceId)
    {
        if (!TryParseHex(deviceId, out var deviceValue))
        {
            return null;
        }

        foreach (var entry in _database.Entries)
        {
            if (entry.DeviceIdRangeStart is null || entry.DeviceIdRangeEnd is null)
            {
                continue;
            }

            if (!string.Equals(entry.VendorId, vendorId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryParseHex(entry.DeviceIdRangeStart, out var start) || !TryParseHex(entry.DeviceIdRangeEnd, out var end))
            {
                continue;
            }

            if (deviceValue >= start && deviceValue <= end)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Among a vendor's device_id-less rows (excluding range entries, which are handled
    /// separately by <see cref="FindInRange"/>), prefers one scoped to the device's own class
    /// (e.g. "Display") over a class-less one, so a vendor with several class-scoped rules
    /// (Intel: Display + Net + USB + SCSIAdapter) is not accidentally matched by the wrong one.
    /// </summary>
    private CompatibilityEntry? FindVendorWide(string vendorId, string? deviceClass)
    {
        CompatibilityEntry? classScoped = null;
        CompatibilityEntry? classless = null;

        foreach (var entry in _database.Entries)
        {
            var isVendorWideRow = entry.DeviceId is null && entry.DeviceIdRangeStart is null;
            if (!isVendorWideRow || !string.Equals(entry.VendorId, vendorId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.DeviceClass is null)
            {
                classless ??= entry;
            }
            else if (deviceClass is not null && string.Equals(entry.DeviceClass, deviceClass, StringComparison.OrdinalIgnoreCase))
            {
                classScoped ??= entry;
            }
        }

        return classScoped ?? classless;
    }

    private static bool TryParseHex(string value, out int result) =>
        int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);

    private static CompatibilityMatch None() =>
        new() { Level = MatchLevel.None, Entry = null, Support = SupportLevel.Unknown };
}
