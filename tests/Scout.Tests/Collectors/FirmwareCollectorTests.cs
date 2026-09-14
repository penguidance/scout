using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class FirmwareCollectorTests
{
    private static void SetUpBiosAndTpm(FakeCimQueryExecutor cim, string specVersion = "2.0, 0, 1.16")
    {
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance(
            "Win32_BIOS",
            ("Manufacturer", "American Megatrends International, LLC."),
            ("SMBIOSBIOSVersion", "B.M0"),
            ("ReleaseDate", new DateTime(2024, 7, 25, 0, 0, 0, DateTimeKind.Utc))));
        cim.SetInstances("root/cimv2/Security/MicrosoftTpm", "Win32_Tpm", FakeCimQueryExecutor.Instance(
            "Win32_Tpm",
            ("SpecVersion", specVersion),
            ("IsEnabled_InitialValue", true),
            ("IsActivated_InitialValue", true)));
    }

    private static void SetSecureBootHandler(FakeCimQueryExecutor cim, uint returnCode, uint? uValue)
    {
        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetDWORDValue", _ => new CimStaticMethodResult
        {
            ReturnCode = returnCode,
            OutParameters = uValue is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["uValue"] = uValue.Value }
        });
    }

    [Fact]
    public void Collect_WithAllSourcesAvailable_MapsEveryField()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpBiosAndTpm(cim);
        SetSecureBootHandler(cim, returnCode: 0, uValue: 1);

        var errors = new List<CollectionError>();
        var result = new FirmwareCollector(cim).Collect(errors);

        Assert.Equal("American Megatrends International, LLC.", result.BiosVendor);
        Assert.Equal("B.M0", result.BiosVersion);
        Assert.Equal(new DateOnly(2024, 7, 25), result.BiosReleaseDate);
        Assert.Equal(BootMode.UEFI, result.BootMode);
        Assert.True(result.SecureBootEnabled);
        Assert.True(result.Tpm.Present);
        Assert.Equal("2.0", result.Tpm.SpecVersion);
        Assert.Equal("1.16", result.Tpm.ManufacturerVersion);
        Assert.Equal("2.0, 0, 1.16", result.Tpm.SpecVersionRaw);
        Assert.True(result.Tpm.Ready);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WhenTpmSpecVersionIsMalformed_KeepsRawButLeavesParsedFieldsNullAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpBiosAndTpm(cim, specVersion: "garbled-not-three-parts");
        SetSecureBootHandler(cim, returnCode: 2, uValue: null);

        var errors = new List<CollectionError>();
        var result = new FirmwareCollector(cim).Collect(errors);

        Assert.Null(result.Tpm.SpecVersion);
        Assert.Null(result.Tpm.ManufacturerVersion);
        Assert.Equal("garbled-not-three-parts", result.Tpm.SpecVersionRaw);
        Assert.Contains(errors, e => e.Component == "firmware" && e.Source == "Win32_Tpm.SpecVersion");
    }

    [Fact]
    public void Collect_WhenSecureBootKeyMissing_HeuristicallyReportsLegacyWithoutLoggingAnError()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpBiosAndTpm(cim);
        SetSecureBootHandler(cim, returnCode: 2, uValue: null); // ERROR_FILE_NOT_FOUND

        var errors = new List<CollectionError>();
        var result = new FirmwareCollector(cim).Collect(errors);

        Assert.Equal(BootMode.Legacy, result.BootMode);
        Assert.Null(result.SecureBootEnabled);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WhenSecureBootMethodThrows_LeavesBootModeNullNotAFakeEnumValueAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpBiosAndTpm(cim);
        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetDWORDValue", _ =>
            throw new InvalidOperationException("WS-Man bağlantısı reddedildi"));

        var errors = new List<CollectionError>();
        var result = new FirmwareCollector(cim).Collect(errors);

        Assert.Null(result.BootMode);
        Assert.Null(result.SecureBootEnabled);
        Assert.Contains(errors, e =>
            e.Component == "firmware" &&
            e.Source.Contains("StdRegProv") &&
            e.Message == "WS-Man bağlantısı reddedildi");
    }

    [Fact]
    public void Collect_WhenTpmQueryFails_ReportsPresentAsNullNotFalseAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance(
            "Win32_BIOS", ("Manufacturer", "Contoso"), ("SMBIOSBIOSVersion", "1.0")));
        cim.SetInstanceQueryFailure(
            "root/cimv2/Security/MicrosoftTpm", "Win32_Tpm", new UnauthorizedAccessException("Access denied"));
        SetSecureBootHandler(cim, returnCode: 2, uValue: null);

        var errors = new List<CollectionError>();
        var result = new FirmwareCollector(cim).Collect(errors);

        // "Could not read" must never collapse into "confirmed false".
        Assert.Null(result.Tpm.Present);
        Assert.Null(result.Tpm.SpecVersion);
        Assert.Null(result.Tpm.SpecVersionRaw);
        Assert.Null(result.Tpm.Ready);
        Assert.Contains(errors, e => e.Component == "firmware" && e.Source == "Win32_Tpm");
    }

    [Fact]
    public void Collect_WhenTpmQuerySucceedsWithNoInstances_ReportsConfirmedAbsentWithoutLoggingAnError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance(
            "Win32_BIOS", ("Manufacturer", "Contoso"), ("SMBIOSBIOSVersion", "1.0")));
        // No Win32_Tpm instances registered -> the query itself succeeds with 0 results, which is
        // a real, confirmable fact on hardware with no TPM chip, not a collection failure.
        SetSecureBootHandler(cim, returnCode: 2, uValue: null);

        var errors = new List<CollectionError>();
        var result = new FirmwareCollector(cim).Collect(errors);

        Assert.False(result.Tpm.Present);
        Assert.Null(result.Tpm.SpecVersionRaw);
        Assert.Null(result.Tpm.Ready);
        Assert.DoesNotContain(errors, e => e.Component == "firmware" && e.Source == "Win32_Tpm");
    }

    [Fact]
    public void Collect_WhenBiosVendorIsOemPlaceholder_NormalizesToFallbackAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_BIOS", FakeCimQueryExecutor.Instance(
            "Win32_BIOS", ("Manufacturer", "Default string"), ("SMBIOSBIOSVersion", "1.0")));
        cim.SetInstances("root/cimv2/Security/MicrosoftTpm", "Win32_Tpm");
        SetSecureBootHandler(cim, returnCode: 2, uValue: null);

        var errors = new List<CollectionError>();
        var result = new FirmwareCollector(cim).Collect(errors);

        Assert.Equal("", result.BiosVendor);
        Assert.Contains(errors, e => e.Component == "firmware" && e.Source == "Win32_BIOS");
    }
}
