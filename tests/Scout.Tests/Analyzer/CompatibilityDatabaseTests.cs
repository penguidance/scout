using Scout.Analyzer.Compatibility;
using Xunit;

namespace Scout.Tests.Analyzer;

public class CompatibilityDatabaseTests
{
    [Fact]
    public void LoadEmbedded_LoadsTheRealSeedDatabaseWithoutError()
    {
        var database = CompatibilityDatabase.LoadEmbedded();

        Assert.NotEmpty(database.Entries);
    }

    [Fact]
    public void LoadEmbedded_ContainsTheExplicitlyRequestedSeedEntries()
    {
        var entries = CompatibilityDatabase.LoadEmbedded().Entries;

        Assert.Contains(entries, e => e.VendorId == "1002" && e.KernelDriver == "amdgpu" && e.Support == SupportLevel.Native);
        Assert.Contains(entries, e => e.VendorId == "8086" && e.KernelDriver == "i915" && e.Support == SupportLevel.Native);
        Assert.Contains(entries, e => e.VendorId == "10DE" && e.Support == SupportLevel.Proprietary);
        Assert.Contains(entries, e => e.VendorId == "10EC" && e.DeviceId == "8168" && e.KernelDriver == "r8169" && e.Support == SupportLevel.Native);
        Assert.Contains(entries, e => e.VendorId == "10EC" && e.DeviceId is null && e.Support == SupportLevel.Native); // audio codecs
        Assert.Contains(entries, e => e.VendorId == "8086" && e.KernelDriver == "iwlwifi" && e.Support == SupportLevel.FirmwareRequired);
        Assert.Contains(entries, e => e.VendorId == "14E4" && e.Support is SupportLevel.FirmwareRequired or SupportLevel.Unsupported);
        Assert.Contains(entries, e => e.KernelDriver == "nvme" && e.Support == SupportLevel.Native);
        Assert.Contains(entries, e => e.KernelDriver == "xhci_hcd" && e.Support == SupportLevel.Native);
    }

    [Fact]
    public void LoadEmbedded_HasNoConflictingVendorWideRules()
    {
        // For any given vendor_id, there must be at most one class-less (device_id=null,
        // device_class=null) fallback rule, and at most one class-scoped fallback rule per
        // distinct class — otherwise CompatibilityMatcher's vendor-fallback lookup would be
        // ambiguous (silently picking whichever happens to come first). Range entries are
        // excluded here — they are matched separately (see the range-overlap test below) and
        // are not vendor-wide fallback candidates even though they also have device_id=null.
        var entries = CompatibilityDatabase.LoadEmbedded().Entries
            .Where(e => e.DeviceId is null && e.DeviceIdRangeStart is null);

        var groups = entries.GroupBy(e => (e.VendorId.ToUpperInvariant(), e.DeviceClass?.ToUpperInvariant()));

        foreach (var group in groups)
        {
            Assert.True(
                group.Count() == 1,
                $"Vendor '{group.Key.Item1}' class '{group.Key.Item2 ?? "(none)"}' has {group.Count()} conflicting vendor-wide rules.");
        }
    }

    [Fact]
    public void LoadEmbedded_HasNoOverlappingDeviceIdRangesPerVendor()
    {
        var ranges = CompatibilityDatabase.LoadEmbedded().Entries
            .Where(e => e.DeviceIdRangeStart is not null && e.DeviceIdRangeEnd is not null)
            .Select(e => (
                Vendor: e.VendorId.ToUpperInvariant(),
                Start: Convert.ToInt32(e.DeviceIdRangeStart, 16),
                End: Convert.ToInt32(e.DeviceIdRangeEnd!, 16)))
            .ToList();

        foreach (var vendorGroup in ranges.GroupBy(r => r.Vendor))
        {
            var sorted = vendorGroup.OrderBy(r => r.Start).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                Assert.True(
                    sorted[i].Start > sorted[i - 1].End,
                    $"Vendor '{vendorGroup.Key}' has overlapping device_id ranges: " +
                    $"{sorted[i - 1].Start:X4}-{sorted[i - 1].End:X4} and {sorted[i].Start:X4}-{sorted[i].End:X4}.");
            }
        }
    }

    [Fact]
    public void FromJson_ParsesMinimalEntry()
    {
        const string json = """
            [{ "vendor_id": "abcd", "kernel_driver": "test", "support": "unknown", "notes": "n/a" }]
            """;

        var database = CompatibilityDatabase.FromJson(json);

        var entry = Assert.Single(database.Entries);
        Assert.Equal("abcd", entry.VendorId);
        Assert.Null(entry.DeviceId);
        Assert.Equal(SupportLevel.Unknown, entry.Support);
    }
}
