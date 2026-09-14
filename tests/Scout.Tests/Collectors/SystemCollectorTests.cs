using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class SystemCollectorTests
{
    private static FakeCimQueryExecutor CreateFakeWithAllSources()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_ComputerSystem", FakeCimQueryExecutor.Instance(
            "Win32_ComputerSystem",
            ("Manufacturer", "Contoso"),
            ("Model", "Latitude 9999"),
            ("SystemSKUNumber", "SKU-1"),
            ("DNSHostName", "Destiny")));
        cim.SetInstances("root/cimv2", "Win32_SystemEnclosure", FakeCimQueryExecutor.Instance(
            "Win32_SystemEnclosure",
            ("ChassisTypes", new ushort[] { 9 })));
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance(
            "Win32_BIOS",
            ("SerialNumber", "SN-123")));
        return cim;
    }

    [Fact]
    public void Collect_WithIdentifiersIncluded_MapsEveryField()
    {
        var cim = CreateFakeWithAllSources();

        var errors = new List<CollectionError>();
        var result = new SystemCollector(cim, includeIdentifiers: true).Collect(errors);

        Assert.Equal("Contoso", result.Manufacturer);
        Assert.Equal("Latitude 9999", result.Model);
        Assert.Equal("SKU-1", result.Sku);
        Assert.Equal(ChassisType.Laptop, result.ChassisType);
        Assert.Equal("SN-123", result.SerialNumber);
        Assert.Equal("Destiny", result.Hostname);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WithoutIncludeIdentifiers_LeavesHostnameAndSerialNumberNullEvenThoughSourceHasThem()
    {
        var cim = CreateFakeWithAllSources();

        var errors = new List<CollectionError>();
        // Default: includeIdentifiers not passed -> false.
        var result = new SystemCollector(cim).Collect(errors);

        Assert.Null(result.Hostname);
        Assert.Null(result.SerialNumber);
        // Not attempting the read at all is not a collection failure.
        Assert.Empty(errors);
        // Non-identifying fields are unaffected by the privacy gate.
        Assert.Equal("Contoso", result.Manufacturer);
    }

    [Fact]
    public void Collect_WhenComputerSystemQueryThrows_LeavesRequiredFieldsAtFallbackAndLogsOneError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstanceQueryFailure("root/cimv2", "Win32_ComputerSystem", new InvalidOperationException("WS-Man zaman aşımı"));
        cim.SetInstances("root/cimv2", "Win32_SystemEnclosure", FakeCimQueryExecutor.Instance(
            "Win32_SystemEnclosure", ("ChassisTypes", new ushort[] { 3 })));
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance("Win32_BIOS"));

        var errors = new List<CollectionError>();
        var result = new SystemCollector(cim, includeIdentifiers: true).Collect(errors);

        Assert.Equal("", result.Manufacturer);
        Assert.Equal("", result.Model);
        Assert.Null(result.Hostname);

        var error = Assert.Single(errors);
        Assert.Equal("system", error.Component);
        Assert.Equal("Win32_ComputerSystem", error.Source);
        Assert.Equal("WS-Man zaman aşımı", error.Message);
    }

    [Fact]
    public void Collect_WhenEnclosureReturnsNoInstances_LogsErrorAndFallsBackToUnknownChassis()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_ComputerSystem", FakeCimQueryExecutor.Instance(
            "Win32_ComputerSystem", ("Manufacturer", "Contoso"), ("Model", "X1")));
        // No Win32_SystemEnclosure instances registered -> empty result.
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance("Win32_BIOS"));

        var errors = new List<CollectionError>();
        var result = new SystemCollector(cim).Collect(errors);

        Assert.Null(result.ChassisType);
        Assert.Contains(errors, e => e.Component == "system" && e.Source == "Win32_SystemEnclosure");
    }

    [Fact]
    public void Collect_WhenOptionalFieldMissing_LeavesItNullWithoutLoggingAnError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_ComputerSystem", FakeCimQueryExecutor.Instance(
            "Win32_ComputerSystem", ("Manufacturer", "Contoso"), ("Model", "X1")));
        cim.SetInstances("root/cimv2", "Win32_SystemEnclosure", FakeCimQueryExecutor.Instance(
            "Win32_SystemEnclosure", ("ChassisTypes", new ushort[] { 3 })));
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance("Win32_BIOS"));

        var errors = new List<CollectionError>();
        var result = new SystemCollector(cim, includeIdentifiers: true).Collect(errors);

        Assert.Null(result.Sku);
        Assert.Null(result.SerialNumber);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(1, ChassisType.Other)] // SMBIOS "Other"
    [InlineData(2, ChassisType.Unknown)] // SMBIOS's own "the firmware doesn't know" code
    [InlineData(99, ChassisType.Other)] // recognized read, uncommon/unmapped code
    public void Collect_MapsChassisCode_DistinguishingUnknownCodeFromUnmappedCode(int code, ChassisType expected)
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_ComputerSystem", FakeCimQueryExecutor.Instance(
            "Win32_ComputerSystem", ("Manufacturer", "Contoso"), ("Model", "X1")));
        cim.SetInstances("root/cimv2", "Win32_SystemEnclosure", FakeCimQueryExecutor.Instance(
            "Win32_SystemEnclosure", ("ChassisTypes", new ushort[] { (ushort)code })));
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance("Win32_BIOS"));

        var errors = new List<CollectionError>();
        var result = new SystemCollector(cim).Collect(errors);

        Assert.Equal(expected, result.ChassisType);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WhenBiosReportsOemPlaceholderSerialNumber_NormalizesToNull()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_ComputerSystem", FakeCimQueryExecutor.Instance(
            "Win32_ComputerSystem", ("Manufacturer", "Contoso"), ("Model", "X1")));
        cim.SetInstances("root/cimv2", "Win32_SystemEnclosure", FakeCimQueryExecutor.Instance(
            "Win32_SystemEnclosure", ("ChassisTypes", new ushort[] { 3 })));
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance(
            "Win32_BIOS", ("SerialNumber", "  To Be Filled By O.E.M.  ")));

        var errors = new List<CollectionError>();
        var result = new SystemCollector(cim, includeIdentifiers: true).Collect(errors);

        Assert.Null(result.SerialNumber);
        Assert.Empty(errors);
    }
}
