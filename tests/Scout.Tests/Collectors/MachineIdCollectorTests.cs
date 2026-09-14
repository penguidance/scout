using System.Security.Cryptography;
using System.Text;
using Scout.Collector.Cim;
using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class MachineIdCollectorTests
{
    private const string RawGuid = "550e8400-e29b-41d4-a716-446655440000";

    private static void SetGuidHandler(FakeCimQueryExecutor cim, uint returnCode, string? sValue)
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
    public void Collect_WithReadableGuid_ReturnsSha256PrefixedDigestNotTheRawGuid()
    {
        var cim = new FakeCimQueryExecutor();
        SetGuidHandler(cim, returnCode: 0, sValue: RawGuid);

        var errors = new List<CollectionError>();
        var machineId = new MachineIdCollector(cim).Collect(errors);

        var expectedDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RawGuid))).ToLowerInvariant();
        Assert.Equal($"sha256:{expectedDigest}", machineId);
        Assert.DoesNotContain(RawGuid, machineId);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_IsDeterministic_SameGuidAlwaysProducesSameId()
    {
        var cim = new FakeCimQueryExecutor();
        SetGuidHandler(cim, returnCode: 0, sValue: RawGuid);

        var first = new MachineIdCollector(cim).Collect([]);
        var second = new MachineIdCollector(cim).Collect([]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Collect_WhenRegistryQueryFails_ReturnsEmptyAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        SetGuidHandler(cim, returnCode: 5, sValue: null); // ERROR_ACCESS_DENIED

        var errors = new List<CollectionError>();
        var machineId = new MachineIdCollector(cim).Collect(errors);

        Assert.Equal("", machineId);
        Assert.Contains(errors, e => e.Component == "machine_id" && e.Source.Contains("MachineGuid"));
    }

    [Fact]
    public void Collect_WhenGuidValueIsEmpty_ReturnsEmptyAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        SetGuidHandler(cim, returnCode: 0, sValue: "   ");

        var errors = new List<CollectionError>();
        var machineId = new MachineIdCollector(cim).Collect(errors);

        Assert.Equal("", machineId);
        Assert.Single(errors);
    }

    [Fact]
    public void Collect_WhenMethodInvocationThrows_ReturnsEmptyAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetStringValue", _ =>
            throw new InvalidOperationException("WS-Man bağlantısı reddedildi"));

        var errors = new List<CollectionError>();
        var machineId = new MachineIdCollector(cim).Collect(errors);

        Assert.Equal("", machineId);
        Assert.Contains(errors, e => e.Component == "machine_id" && e.Message == "WS-Man bağlantısı reddedildi");
    }
}
