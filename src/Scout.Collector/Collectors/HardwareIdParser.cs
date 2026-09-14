using System.Text.RegularExpressions;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>Result of parsing one raw PnP hardware ID string.</summary>
internal readonly record struct ParsedHardwareId(string NormalizedId, string? VendorId, string? DeviceId, DeviceBusType BusType);

/// <summary>
/// Parses a raw Win32_PnPEntity hardware ID (e.g. "PCI\VEN_1002&amp;DEV_73FF&amp;SUBSYS_...&amp;REV_C1")
/// into a bus type plus, when the pattern is present, a vendor:device hex pair — the canonical
/// matching key this schema uses (see <see cref="DeviceInfo"/> remarks). Never throws: an
/// unparsable or unexpected format simply yields null vendor/device IDs and an unchanged
/// normalized form.
/// </summary>
internal static partial class HardwareIdParser
{
    /// <summary>
    /// Pseudo vendor_id assigned to USB root hub PnP nodes ("USB\ROOT_HUB30", "USB\ROOT_HUB20",
    /// the legacy "USB\ROOT_HUB", ...). These are not physical devices with their own USB
    /// descriptor — Windows creates this node for the root hub function built into a USB host
    /// controller silicon — so they never carry a real VID_/PID_. Deliberately not 4 hex digits,
    /// so it can never collide with a genuine PCI/USB vendor ID. See
    /// data/hardware-compatibility.json's "USBROOTHUB" entry and docs/schema/profile-v0.1.md.
    /// </summary>
    public const string UsbRootHubVendorId = "USBROOTHUB";

    public static ParsedHardwareId Parse(string raw)
    {
        var busType = DetermineBusType(raw);
        var prefix = CanonicalPrefix(busType, raw);

        if (raw.Contains("ROOT_HUB", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedHardwareId(raw, UsbRootHubVendorId, null, busType);
        }

        var venDev = VenDevPattern().Match(raw);
        if (venDev.Success)
        {
            var vendor = venDev.Groups[1].Value.ToUpperInvariant();
            var device = venDev.Groups[2].Value.ToUpperInvariant();
            return new ParsedHardwareId($@"{prefix}\VEN_{vendor}&DEV_{device}", vendor, device, busType);
        }

        var vidPid = VidPidPattern().Match(raw);
        if (vidPid.Success)
        {
            var vendor = vidPid.Groups[1].Value.ToUpperInvariant();
            var device = vidPid.Groups[2].Value.ToUpperInvariant();
            return new ParsedHardwareId($@"{prefix}\VID_{vendor}&PID_{device}", vendor, device, busType);
        }

        // No recognized vendor:device pattern (e.g. a plain ACPI PNP ID like "ACPI\PNP0303", or
        // an unexpected/malformed string) — keep the raw form as-is, no vendor/device IDs.
        return new ParsedHardwareId(raw, null, null, busType);
    }

    private static DeviceBusType DetermineBusType(string raw)
    {
        var separatorIndex = raw.IndexOf('\\');
        var busPrefix = separatorIndex > 0 ? raw[..separatorIndex] : raw;
        return busPrefix.ToUpperInvariant() switch
        {
            "PCI" => DeviceBusType.PCI,
            "USB" => DeviceBusType.USB,
            "ACPI" => DeviceBusType.ACPI,
            _ => DeviceBusType.Other
        };
    }

    /// <summary>The canonical (uppercase) bus prefix for a recognized bus; the original prefix text otherwise.</summary>
    private static string CanonicalPrefix(DeviceBusType busType, string raw)
    {
        if (busType != DeviceBusType.Other)
        {
            return busType.ToString().ToUpperInvariant();
        }

        var separatorIndex = raw.IndexOf('\\');
        return separatorIndex > 0 ? raw[..separatorIndex] : raw;
    }

    [GeneratedRegex(@"VEN_([0-9A-Fa-f]{4})&DEV_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VenDevPattern();

    [GeneratedRegex(@"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidPattern();
}
