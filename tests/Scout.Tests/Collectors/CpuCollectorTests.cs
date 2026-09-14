using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class CpuCollectorTests
{
    private static FakeCimQueryExecutor CreateFake(
        string manufacturer,
        string description,
        ushort architectureCode = 9,
        bool? virtualizationFirmwareEnabled = true)
    {
        var cim = new FakeCimQueryExecutor();
        var properties = new List<(string, object?)>
        {
            ("Manufacturer", manufacturer),
            ("Name", "Test CPU"),
            ("Description", description),
            ("NumberOfCores", (uint)6),
            ("NumberOfLogicalProcessors", (uint)12),
            ("Architecture", architectureCode)
        };
        if (virtualizationFirmwareEnabled is not null)
        {
            properties.Add(("VirtualizationFirmwareEnabled", virtualizationFirmwareEnabled.Value));
        }

        cim.SetInstances("root/cimv2", "Win32_Processor", FakeCimQueryExecutor.Instance(
            "Win32_Processor", properties.ToArray()));
        return cim;
    }

    [Fact]
    public void Collect_WithAmdZen3Description_DerivesFeatureLevelV3WithoutError()
    {
        // Real Win32_Processor.Description format for a Ryzen 5 5600X (Zen 3, family 25/0x19).
        var cim = CreateFake("AuthenticAMD", "AMD64 Family 25 Model 33 Stepping 2");

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Equal(CpuArchitecture.x86_64, result.Architecture);
        Assert.Equal(X86FeatureLevel.v3, result.X86_64FeatureLevel);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WithOlderAmdBulldozerFamily_DerivesFeatureLevelV2()
    {
        var cim = CreateFake("AuthenticAMD", "AMD64 Family 21 Model 2 Stepping 0"); // Bulldozer (0x15)

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Equal(X86FeatureLevel.v2, result.X86_64FeatureLevel);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WithIntelHaswellDescription_DerivesFeatureLevelV3()
    {
        var cim = CreateFake("GenuineIntel", "Intel64 Family 6 Model 60 Stepping 3"); // Haswell (0x3C)

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Equal(X86FeatureLevel.v3, result.X86_64FeatureLevel);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WithOlderIntelSandyBridgeDescription_DerivesFeatureLevelV2()
    {
        var cim = CreateFake("GenuineIntel", "Intel64 Family 6 Model 42 Stepping 7"); // Sandy Bridge (0x2A)

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Equal(X86FeatureLevel.v2, result.X86_64FeatureLevel);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WithUnknownFamilyModelCombination_LeavesFeatureLevelNullAndLogsError()
    {
        var cim = CreateFake("GenuineIntel", "Intel64 Family 6 Model 250 Stepping 1"); // not in our table

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Null(result.X86_64FeatureLevel);
        Assert.Contains(errors, e =>
            e.Component == "cpu" && e.Source == "Win32_Processor.Description" && e.Message.Contains("family=6"));
    }

    [Fact]
    public void Collect_WithUnparsableDescription_LeavesFeatureLevelNullAndLogsError()
    {
        var cim = CreateFake("GenuineIntel", "garbled/unexpected description string");

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Null(result.X86_64FeatureLevel);
        Assert.Contains(errors, e => e.Component == "cpu" && e.Source == "Win32_Processor.Description");
    }

    [Fact]
    public void Collect_WhenArchitectureIsNotX64_LeavesFeatureLevelNullWithoutAttemptingDerivation()
    {
        // arm64 (code 12) — Description would be irrelevant/absent on such a machine.
        var cim = CreateFake("Qualcomm", "ARMv8 (64-bit) Family 1 Model 1", architectureCode: 12);

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Equal(CpuArchitecture.arm64, result.Architecture);
        Assert.Null(result.X86_64FeatureLevel);
        Assert.DoesNotContain(errors, e => e.Source == "Win32_Processor.Description");
    }

    [Fact]
    public void Collect_WhenProcessorQueryThrows_LeavesRequiredFieldsAtFallbackAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstanceQueryFailure("root/cimv2", "Win32_Processor", new InvalidOperationException("bağlantı koptu"));

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Equal("", result.Vendor);
        Assert.Equal("", result.Model);
        Assert.Null(result.PhysicalCores);
        Assert.Null(result.LogicalProcessors);
        Assert.Null(result.Architecture);
        Assert.Null(result.X86_64FeatureLevel);
        Assert.Null(result.VirtualizationFirmwareEnabled);

        Assert.Contains(errors, e =>
            e.Component == "cpu" && e.Source == "Win32_Processor" && e.Message == "bağlantı koptu");
    }

    [Fact]
    public void Collect_WithUnrecognizedArchitectureCode_MapsToUnknown()
    {
        var cim = CreateFake("GenuineIntel", "IA64 Family 1 Model 1", architectureCode: 6); // ia64

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Equal(CpuArchitecture.Unknown, result.Architecture);
        Assert.Null(result.X86_64FeatureLevel);
    }

    [Fact]
    public void Collect_WhenVirtualizationFlagMissing_LeavesItNullWithoutExtraError()
    {
        var cim = CreateFake(
            "GenuineIntel", "Intel64 Family 6 Model 60 Stepping 3", virtualizationFirmwareEnabled: null);

        var errors = new List<CollectionError>();
        var result = new CpuCollector(cim).Collect(errors);

        Assert.Null(result.VirtualizationFirmwareEnabled);
        Assert.Empty(errors);
    }
}
