using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class MemoryCollectorTests
{
    [Fact]
    public void Collect_WithTwoModules_SumsCapacityAndDecodesMemoryType()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PhysicalMemory",
            FakeCimQueryExecutor.Instance(
                "Win32_PhysicalMemory",
                ("Capacity", (ulong)8_589_934_592),
                ("Speed", (uint)3200),
                ("ConfiguredClockSpeed", (uint)2933), // XMP/EXPO off -> running below rated speed
                ("Manufacturer", "Corsair"),
                ("PartNumber", "CMK16GX4M2B3200C16"),
                ("SMBIOSMemoryType", (uint)26)),
            FakeCimQueryExecutor.Instance(
                "Win32_PhysicalMemory",
                ("Capacity", (ulong)8_589_934_592),
                ("Speed", (uint)3200),
                ("ConfiguredClockSpeed", (uint)2933),
                ("Manufacturer", "Unknown"), // OEM placeholder -> should normalize to null
                ("PartNumber", "CMK16GX4M2B3200C16"),
                ("SMBIOSMemoryType", (uint)26)));

        var errors = new List<CollectionError>();
        var result = new MemoryCollector(cim).Collect(errors);

        Assert.Equal(2, result.Modules.Count);
        Assert.Equal(17_179_869_184, result.TotalBytes);
        Assert.Equal(8_589_934_592, result.Modules[0].CapacityBytes);
        Assert.Equal(3200, result.Modules[0].RatedSpeedMhz);
        Assert.Equal(2933, result.Modules[0].ConfiguredSpeedMhz);
        Assert.Equal("Corsair", result.Modules[0].Manufacturer);
        Assert.Equal("DDR4", result.Modules[0].MemoryType);
        Assert.Equal("26", result.Modules[0].MemoryTypeRaw);
        Assert.Null(result.Modules[1].Manufacturer);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WithUnrecognizedMemoryTypeCode_LeavesTypeNullButKeepsRawCode()
    {
        // Modern Windows: the older MemoryType property is unreliable and commonly just reports
        // 0 regardless of the real module type — this must never be presented as a resolved "0"
        // label; it belongs only in memory_type_raw.
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PhysicalMemory", FakeCimQueryExecutor.Instance(
            "Win32_PhysicalMemory", ("Capacity", (ulong)1024), ("MemoryType", (uint)0), ("SMBIOSMemoryType", (uint)0)));

        var result = new MemoryCollector(cim).Collect([]);

        Assert.Null(result.Modules[0].MemoryType);
        Assert.Equal("0", result.Modules[0].MemoryTypeRaw);
    }

    [Fact]
    public void Collect_ReadsSmbiosMemoryTypeNotTheLegacyMemoryTypeProperty()
    {
        // MemoryType says something recognizable (DDR3, 24) while SMBIOSMemoryType says DDR4
        // (26) — the real, more reliable field must win.
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PhysicalMemory", FakeCimQueryExecutor.Instance(
            "Win32_PhysicalMemory", ("Capacity", (ulong)1024), ("MemoryType", (uint)24), ("SMBIOSMemoryType", (uint)26)));

        var result = new MemoryCollector(cim).Collect([]);

        Assert.Equal("DDR4", result.Modules[0].MemoryType);
        Assert.Equal("26", result.Modules[0].MemoryTypeRaw);
    }

    [Fact]
    public void Collect_WhenQueryThrows_LeavesTotalBytesNullNotZeroAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstanceQueryFailure("root/cimv2", "Win32_PhysicalMemory", new InvalidOperationException("erişim reddedildi"));

        var errors = new List<CollectionError>();
        var result = new MemoryCollector(cim).Collect(errors);

        Assert.Null(result.TotalBytes);
        Assert.Empty(result.Modules);
        Assert.Contains(errors, e => e.Component == "memory" && e.Source == "Win32_PhysicalMemory");
    }

    [Fact]
    public void Collect_WhenNoModulesButArrayPresent_LogsVirtualizationHint()
    {
        var cim = new FakeCimQueryExecutor();
        // No Win32_PhysicalMemory instances, but the array class resolves fine.
        cim.SetInstances("root/cimv2", "Win32_PhysicalMemoryArray", FakeCimQueryExecutor.Instance(
            "Win32_PhysicalMemoryArray", ("Use", (ushort)3)));

        var errors = new List<CollectionError>();
        var result = new MemoryCollector(cim).Collect(errors);

        Assert.Null(result.TotalBytes);
        Assert.Contains(errors, e =>
            e.Component == "memory" && e.Source == "Win32_PhysicalMemory" && e.Message.Contains("sanallaştırılmış"));
    }

    [Fact]
    public void Collect_WhenOneModuleCapacityUnreadable_SumsOnlyReadableModulesAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PhysicalMemory",
            FakeCimQueryExecutor.Instance("Win32_PhysicalMemory", ("Capacity", (ulong)4_294_967_296)),
            FakeCimQueryExecutor.Instance("Win32_PhysicalMemory")); // no Capacity property at all

        var errors = new List<CollectionError>();
        var result = new MemoryCollector(cim).Collect(errors);

        Assert.Equal(4_294_967_296, result.TotalBytes);
        Assert.Null(result.Modules[1].CapacityBytes);
        Assert.Contains(errors, e => e.Component == "memory" && e.Source == "Win32_PhysicalMemory.Capacity");
    }
}
