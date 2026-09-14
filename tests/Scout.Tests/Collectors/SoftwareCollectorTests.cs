using Scout.Collector.Cim;
using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class SoftwareCollectorTests
{
    private const uint HKeyLocalMachine = 0x80000002;
    private const uint HKeyCurrentUser = 0x80000001;
    private const string NativePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Wow6432Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>
    /// Wires up a fake StdRegProv: <paramref name="subKeysByRoot"/> answers EnumKey for a given
    /// (hDefKey, base path); <paramref name="valuesByKeyPath"/> answers GetStringValue/GetDWORDValue
    /// for a given (full subkey path, value name). A root/key/value missing from its dictionary
    /// answers with return code 2 (ERROR_FILE_NOT_FOUND) — "not set", the common case.
    /// </summary>
    private static void SetUpRegistry(
        FakeCimQueryExecutor cim,
        IReadOnlyDictionary<(uint HDefKey, string BasePath), string[]> subKeysByRoot,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>? valuesByKeyPath = null)
    {
        valuesByKeyPath ??= new Dictionary<string, IReadOnlyDictionary<string, object>>();

        cim.SetMethodHandler("root/cimv2", "StdRegProv", "EnumKey", parameters =>
        {
            var hDefKey = Convert.ToUInt32(parameters["hDefKey"].Value);
            var basePath = (string)parameters["sSubKeyName"].Value!;

            return subKeysByRoot.TryGetValue((hDefKey, basePath), out var names)
                ? new CimStaticMethodResult { ReturnCode = 0, OutParameters = new Dictionary<string, object?> { ["sNames"] = names } }
                : new CimStaticMethodResult { ReturnCode = 2, OutParameters = new Dictionary<string, object?>() };
        });

        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetStringValue", parameters =>
        {
            var keyPath = (string)parameters["sSubKeyName"].Value!;
            var valueName = (string)parameters["sValueName"].Value!;

            if (valuesByKeyPath.TryGetValue(keyPath, out var values) && values.TryGetValue(valueName, out var value) && value is string s)
            {
                return new CimStaticMethodResult { ReturnCode = 0, OutParameters = new Dictionary<string, object?> { ["sValue"] = s } };
            }

            return new CimStaticMethodResult { ReturnCode = 2, OutParameters = new Dictionary<string, object?>() };
        });

        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetDWORDValue", parameters =>
        {
            var keyPath = (string)parameters["sSubKeyName"].Value!;
            var valueName = (string)parameters["sValueName"].Value!;

            if (valuesByKeyPath.TryGetValue(keyPath, out var values) && values.TryGetValue(valueName, out var value) && value is uint u)
            {
                return new CimStaticMethodResult { ReturnCode = 0, OutParameters = new Dictionary<string, object?> { ["uValue"] = u } };
            }

            return new CimStaticMethodResult { ReturnCode = 2, OutParameters = new Dictionary<string, object?>() };
        });

        // No Win32_InstalledStoreProgram instances unless a test registers its own.
        cim.SetInstances("root/cimv2", "Win32_InstalledStoreProgram");
    }

    private static Dictionary<string, IReadOnlyDictionary<string, object>> OneEntry(
        string keyPath, params (string Name, object Value)[] values) =>
        new() { [keyPath] = values.ToDictionary(v => v.Name, v => v.Value) };

    [Fact]
    public void Collect_MapsAllFieldsFromAnHklmNativeEntry()
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\MyApp";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["MyApp"] },
            OneEntry(keyPath,
                ("DisplayName", "My Application"),
                ("Publisher", "Contoso"),
                ("DisplayVersion", "1.2.3"),
                ("InstallDate", "20240115"),
                ("EstimatedSize", (uint)51200), // KiB
                ("UninstallString", @"msiexec /x {GUID}"),
                ("InstallLocation", @"C:\Program Files\MyApp")));

        var result = new SoftwareCollector(cim).Collect([]);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("My Application", entry.Name);
        Assert.Equal("Contoso", entry.Publisher);
        Assert.Equal("1.2.3", entry.Version);
        Assert.Equal(new DateOnly(2024, 1, 15), entry.InstallDate);
        Assert.Equal(51200L * 1024, entry.EstimatedSizeBytes);
        Assert.Equal(@"msiexec /x {GUID}", entry.UninstallString);
        Assert.Equal(RegistryView.Native, entry.RegistryView);
        Assert.Equal(SoftwareSource.HklmUninstallKey, entry.Source);
        Assert.False(entry.SystemComponent);
        Assert.Equal(SoftwareCategory.application, entry.Category);
        Assert.Equal(0, result.FilteredCount);
        // The Uninstall subkey's own name — a stable identity signal, never re-translated if the
        // display language changes later. See SoftwareEntry.RegistryKeyName.
        Assert.Equal("MyApp", entry.RegistryKeyName);
        Assert.Equal(@"C:\Program Files\MyApp", entry.InstallLocation);
    }

    [Fact]
    public void Collect_MsiStyleProductCodeKeyName_IsCapturedVerbatim()
    {
        var cim = new FakeCimQueryExecutor();
        const string productCode = "{90160000-008C-0409-1000-0000000FF1CE}";
        var keyPath = NativePath + @"\" + productCode;
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = [productCode] },
            OneEntry(keyPath, ("DisplayName", "Microsoft Office Professional Plus 2019")));

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Equal(productCode, Assert.Single(result.Entries).RegistryKeyName);
    }

    [Fact]
    public void Collect_WhenInstallLocationIsAbsent_LeavesItNullNotEmpty()
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\NoLocation";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["NoLocation"] },
            OneEntry(keyPath, ("DisplayName", "No Location App")));

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Null(Assert.Single(result.Entries).InstallLocation);
    }

    [Fact]
    public void Collect_ReadsAllThreeRegistryRoots_TaggingSourceAndRegistryViewCorrectly()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]>
            {
                [(HKeyLocalMachine, NativePath)] = ["Native64App"],
                [(HKeyLocalMachine, Wow6432Path)] = ["Wow32App"],
                [(HKeyCurrentUser, NativePath)] = ["PerUserApp"]
            },
            new Dictionary<string, IReadOnlyDictionary<string, object>>
            {
                [NativePath + @"\Native64App"] = new Dictionary<string, object> { ["DisplayName"] = "64-bit App" },
                [Wow6432Path + @"\Wow32App"] = new Dictionary<string, object> { ["DisplayName"] = "32-bit App" },
                [NativePath + @"\PerUserApp"] = new Dictionary<string, object> { ["DisplayName"] = "Per-User App" }
            });

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Equal(3, result.Entries.Count);

        var native = Assert.Single(result.Entries, e => e.Name == "64-bit App");
        Assert.Equal(RegistryView.Native, native.RegistryView);
        Assert.Equal(SoftwareSource.HklmUninstallKey, native.Source);

        var wow = Assert.Single(result.Entries, e => e.Name == "32-bit App");
        Assert.Equal(RegistryView.Wow6432Node, wow.RegistryView);
        Assert.Equal(SoftwareSource.HklmUninstallKey, wow.Source);

        var perUser = Assert.Single(result.Entries, e => e.Name == "Per-User App");
        Assert.Equal(RegistryView.Native, perUser.RegistryView);
        Assert.Equal(SoftwareSource.HkcuUninstallKey, perUser.Source);
    }

    [Fact]
    public void Collect_DropsEntryWithBlankDisplayName()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpRegistry(cim, new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["NoName"] });
        // DisplayName intentionally not registered -> GetStringValue answers "not found".

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.FilteredCount);
    }

    [Fact]
    public void Collect_DropsEntryFlaggedAsSystemComponent()
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\Comp";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["Comp"] },
            OneEntry(keyPath, ("DisplayName", "Some Component"), ("SystemComponent", (uint)1)));

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.FilteredCount);
    }

    [Theory]
    [InlineData("ParentKeyName", "{SomeParentKey}")]
    [InlineData("ParentDisplayName", "Some Suite")]
    public void Collect_DropsEntryThatIsASubComponentOfAnotherProduct(string parentField, string parentValue)
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\SubPart";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["SubPart"] },
            OneEntry(keyPath, ("DisplayName", "A Sub-Feature"), (parentField, parentValue)));

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.FilteredCount);
    }

    [Theory]
    [InlineData("KB5031354")]
    [InlineData("Update for Windows 10 for x64-based Systems (KB5031354)")]
    [InlineData("Security Update for Microsoft Office (KB1234567)")]
    public void Collect_DropsWindowsUpdateAndHotfixEntries(string updateName)
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\Upd";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["Upd"] },
            OneEntry(keyPath, ("DisplayName", updateName)));

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.FilteredCount);
    }

    [Theory]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64)", SoftwareCategory.runtime)]
    [InlineData("Microsoft .NET Runtime 8.0.1 (x64)", SoftwareCategory.runtime)]
    [InlineData("NVIDIA Graphics Driver", SoftwareCategory.driver)]
    [InlineData("Realtek High Definition Audio Driver", SoftwareCategory.driver)]
    [InlineData("Notepad++", SoftwareCategory.application)]
    public void Collect_CategorizesButNeverDropsRuntimesAndDrivers(string name, SoftwareCategory expectedCategory)
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\Item";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["Item"] },
            OneEntry(keyPath, ("DisplayName", name)));

        var result = new SoftwareCollector(cim).Collect([]);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(expectedCategory, entry.Category);
        Assert.Equal(0, result.FilteredCount);
    }

    [Fact]
    public void Collect_WhenAValueIsAbsentFromAnExistingKey_TreatsReturnCodeOneAsNotPresentWithoutLoggingAnError()
    {
        // Confirmed empirically against a real machine: StdRegProv's GetStringValue/GetDWORDValue
        // return 1 (not 2 — that is reserved for the whole key being missing) when the key itself
        // exists but a specific value on it was never set. This is the overwhelmingly common case
        // for most of these optional Uninstall values (e.g. ParentKeyName), so it must be silent.
        var cim = new FakeCimQueryExecutor();
        SetUpRegistry(cim, new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["App"] });
        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetStringValue", parameters =>
        {
            var valueName = (string)parameters["sValueName"].Value!;
            return valueName == "DisplayName"
                ? new CimStaticMethodResult { ReturnCode = 0, OutParameters = new Dictionary<string, object?> { ["sValue"] = "Real App" } }
                : new CimStaticMethodResult { ReturnCode = 1, OutParameters = new Dictionary<string, object?> { ["sValue"] = "" } };
        });
        cim.SetMethodHandler("root/cimv2", "StdRegProv", "GetDWORDValue", _ =>
            new CimStaticMethodResult { ReturnCode = 1, OutParameters = new Dictionary<string, object?> { ["uValue"] = "" } });

        var errors = new List<CollectionError>();
        var result = new SoftwareCollector(cim).Collect(errors);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Real App", entry.Name);
        Assert.Null(entry.Publisher);
        Assert.Null(entry.EstimatedSizeBytes);
        Assert.False(entry.SystemComponent);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WhenAnUninstallRootKeyDoesNotExist_ReturnsEmptyForItWithoutLoggingAnError()
    {
        var cim = new FakeCimQueryExecutor();
        // Only HKLM native is registered; HKCU (a very plausible "no per-user installs yet" case)
        // and Wow6432Node both fall through to EnumKey's "not found" (return code 2) response.
        SetUpRegistry(cim, new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = [] });

        var errors = new List<CollectionError>();
        var result = new SoftwareCollector(cim).Collect(errors);

        Assert.Empty(result.Entries);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WhenEnumKeyThrows_LogsErrorAndStillProcessesOtherRoots()
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\StillWorks";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyCurrentUser, NativePath)] = ["StillWorks"] },
            OneEntry(keyPath, ("DisplayName", "Still Works")));

        cim.SetMethodHandler("root/cimv2", "StdRegProv", "EnumKey", parameters =>
        {
            var hDefKey = Convert.ToUInt32(parameters["hDefKey"].Value);
            if (hDefKey == HKeyLocalMachine)
            {
                throw new InvalidOperationException("WS-Man bağlantısı reddedildi");
            }

            return new CimStaticMethodResult { ReturnCode = 0, OutParameters = new Dictionary<string, object?> { ["sNames"] = new[] { "StillWorks" } } };
        });

        var errors = new List<CollectionError>();
        var result = new SoftwareCollector(cim).Collect(errors);

        Assert.Contains(errors, e => e.Component == "software" && e.Message == "WS-Man bağlantısı reddedildi");
        // The HKCU root (processed independently of HKLM's two failed roots) still contributes its entry.
        Assert.Contains(result.Entries, e => e.Name == "Still Works");
    }

    [Fact]
    public void Collect_WhenInstallDateIsMalformed_LeavesItNullAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\BadDate";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["BadDate"] },
            OneEntry(keyPath, ("DisplayName", "Bad Date App"), ("InstallDate", "not-a-date")));

        var errors = new List<CollectionError>();
        var result = new SoftwareCollector(cim).Collect(errors);

        var entry = Assert.Single(result.Entries);
        Assert.Null(entry.InstallDate);
        Assert.Contains(errors, e => e.Component == "software" && e.Source.Contains("InstallDate"));
    }

    [Fact]
    public void Collect_WhenEstimatedSizeIsAbsent_LeavesItNullNotZero()
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\NoSize";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["NoSize"] },
            OneEntry(keyPath, ("DisplayName", "No Size App")));

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Null(Assert.Single(result.Entries).EstimatedSizeBytes);
    }

    [Fact]
    public void Collect_StorePrograms_AreTaggedWithStoreSourceAndNoRegistryView()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpRegistry(cim, new Dictionary<(uint, string), string[]>());
        cim.SetInstances("root/cimv2", "Win32_InstalledStoreProgram", FakeCimQueryExecutor.Instance(
            "Win32_InstalledStoreProgram",
            ("Name", "Microsoft.WindowsCalculator"),
            ("Vendor", "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"),
            ("Version", "10.1907.2712.0")));

        var result = new SoftwareCollector(cim).Collect([]);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Microsoft.WindowsCalculator", entry.Name);
        Assert.Equal("10.1907.2712.0", entry.Version);
        Assert.Equal(SoftwareSource.StorePackage, entry.Source);
        Assert.Null(entry.RegistryView);
        Assert.False(entry.SystemComponent);
        Assert.Null(entry.EstimatedSizeBytes);
        // No registry-key or install-location concept for a Store package.
        Assert.Null(entry.RegistryKeyName);
        Assert.Null(entry.InstallLocation);
    }

    [Fact]
    public void Collect_StorePrograms_WhenQueryFails_LogsErrorAndFabricatesNothing()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpRegistry(cim, new Dictionary<(uint, string), string[]>());
        cim.SetInstanceQueryFailure(
            "root/cimv2", "Win32_InstalledStoreProgram", new InvalidOperationException("Sınıf bulunamadı"));

        var errors = new List<CollectionError>();
        var result = new SoftwareCollector(cim).Collect(errors);

        Assert.Empty(result.Entries);
        Assert.Contains(errors, e => e.Component == "software" && e.Source == "Win32_InstalledStoreProgram");
    }

    [Fact]
    public void Collect_StorePrograms_WithBlankName_IsFiltered()
    {
        var cim = new FakeCimQueryExecutor();
        SetUpRegistry(cim, new Dictionary<(uint, string), string[]>());
        cim.SetInstances("root/cimv2", "Win32_InstalledStoreProgram", FakeCimQueryExecutor.Instance("Win32_InstalledStoreProgram"));

        var result = new SoftwareCollector(cim).Collect([]);

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.FilteredCount);
    }

    [Fact]
    public void Constructor_RequiresACimQueryExecutor()
    {
        Assert.Throws<ArgumentNullException>(() => new SoftwareCollector(null!));
    }

    [Fact]
    public void Collect_FilterListsAreConfigurable()
    {
        var cim = new FakeCimQueryExecutor();
        const string keyPath = NativePath + @"\Weird";
        SetUpRegistry(
            cim,
            new Dictionary<(uint, string), string[]> { [(HKeyLocalMachine, NativePath)] = ["Weird"] },
            OneEntry(keyPath, ("DisplayName", "Totally Normal App (Refresh Pack)")));

        var options = new SoftwareFilterOptions { UpdateNameSubstrings = ["Refresh Pack"] };
        var result = new SoftwareCollector(cim, options).Collect([]);

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.FilteredCount);
    }
}
