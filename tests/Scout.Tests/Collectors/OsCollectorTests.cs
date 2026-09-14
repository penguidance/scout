using Scout.Collector.Cim;
using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class OsCollectorTests
{
    private static void SetDisplayVersionHandler(FakeCimQueryExecutor cim, uint returnCode, string? sValue)
    {
        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetStringValue", _ => new CimStaticMethodResult
        {
            ReturnCode = returnCode,
            OutParameters = sValue is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["sValue"] = sValue }
        });
    }

    [Fact]
    public void Collect_WithAllSourcesAvailable_MapsEveryField()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_OperatingSystem", FakeCimQueryExecutor.Instance(
            "Win32_OperatingSystem",
            ("Caption", "Microsoft Windows 11 Pro"),
            ("Version", "10.0.26200"),
            ("BuildNumber", "26200"),
            ("OSArchitecture", "64 bit"),
            ("MUILanguages", new[] { "tr-TR", "en-US" }),
            ("InstallDate", new DateTime(2025, 1, 1, 3, 2, 59, DateTimeKind.Local))));
        SetDisplayVersionHandler(cim, returnCode: 0, sValue: "25H2");

        var errors = new List<CollectionError>();
        var result = new OsCollector(cim).Collect(errors);

        Assert.Equal("Microsoft Windows 11 Pro", result.Edition);
        Assert.Equal("10.0.26200", result.Version);
        Assert.Equal("26200", result.Build);
        Assert.Equal("25H2", result.DisplayVersion);
        Assert.Equal(CpuArchitecture.x86_64, result.Architecture);
        Assert.Equal("tr-TR", result.Language);
        Assert.NotNull(result.InstallDate);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("64 bit", CpuArchitecture.x86_64)]
    [InlineData("64-bit", CpuArchitecture.x86_64)]
    [InlineData("32 bit", CpuArchitecture.x86)]
    [InlineData("ARM 64-bit Processor", CpuArchitecture.arm64)]
    public void Collect_ParsesOsArchitectureStringVariants(string raw, CpuArchitecture expected)
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_OperatingSystem", FakeCimQueryExecutor.Instance(
            "Win32_OperatingSystem",
            ("Caption", "Windows"), ("Version", "1"), ("BuildNumber", "1"), ("OSArchitecture", raw)));
        SetDisplayVersionHandler(cim, returnCode: 2, sValue: null);

        var result = new OsCollector(cim).Collect([]);

        Assert.Equal(expected, result.Architecture);
    }

    [Fact]
    public void Collect_WhenOsQueryThrows_LeavesFieldsAtFallbackAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstanceQueryFailure("root/cimv2", "Win32_OperatingSystem", new InvalidOperationException("bağlantı koptu"));
        SetDisplayVersionHandler(cim, returnCode: 2, sValue: null);

        var errors = new List<CollectionError>();
        var result = new OsCollector(cim).Collect(errors);

        Assert.Equal("", result.Edition);
        Assert.Null(result.Architecture);
        Assert.Null(result.Language);
        Assert.Contains(errors, e => e.Component == "os" && e.Source == "Win32_OperatingSystem");
    }

    [Fact]
    public void Collect_WhenDisplayVersionRegistryReadFails_LeavesItNullAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_OperatingSystem", FakeCimQueryExecutor.Instance(
            "Win32_OperatingSystem", ("Caption", "Windows"), ("Version", "1"), ("BuildNumber", "1"), ("OSArchitecture", "64 bit")));
        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetStringValue", _ =>
            throw new InvalidOperationException("WS-Man bağlantısı reddedildi"));

        var errors = new List<CollectionError>();
        var result = new OsCollector(cim).Collect(errors);

        Assert.Null(result.DisplayVersion);
        Assert.Contains(errors, e => e.Component == "os" && e.Source.Contains("DisplayVersion"));
    }
}
