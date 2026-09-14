using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects installed-software inventory: registry Uninstall keys (HKLM native, HKLM WOW6432Node,
/// HKCU), read via StdRegProv so the same code works against a remote target over the same
/// CIM/WS-Man session, plus, best-effort, Win32_InstalledStoreProgram for MSIX/Store apps. Never
/// Win32_Product — see docs/schema/profile-v0.1.md for why (silent MSI repair side effects, very
/// slow, MSI-only coverage).
/// </summary>
/// <remarks>
/// Inventory only, this iteration — no Linux-equivalent mapping happens here, that is a later
/// Analyzer concern. What this class does do is separate real noise (blank entries, Windows
/// update/hotfix entries, sub-components of an already-listed product, entries the registry
/// itself marks <c>SystemComponent</c>) from real software, without discarding runtime/driver
/// packages outright: those are kept but tagged via <see cref="SoftwareEntry.Category"/> so a
/// report can fold them away later without hiding them from someone who wants the full picture.
/// See <see cref="SoftwareFilterOptions"/> for the (configurable) rules.
/// </remarks>
public sealed class SoftwareCollector : ISectionCollector<SoftwareCollectionResult>
{
    private const uint HKeyLocalMachine = 0x80000002;
    private const uint HKeyCurrentUser = 0x80000001;
    private const string UninstallSubPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Wow6432UninstallSubPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string StoreProgramSource = "Win32_InstalledStoreProgram";

    private static readonly Regex KbHotfixNamePattern =
        new(@"^KB\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ICimQueryExecutor _cim;
    private readonly SoftwareFilterOptions _options;
    private readonly TimeProvider _clock;

    public SoftwareCollector(ICimQueryExecutor cim, SoftwareFilterOptions? options = null, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _options = options ?? new SoftwareFilterOptions();
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "software";

    public SoftwareCollectionResult Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);
        var entries = new List<SoftwareEntry>();
        var filteredCount = 0;

        foreach (var root in Roots())
        {
            filteredCount += CollectRoot(root, entries, log);
        }

        filteredCount += CollectStorePrograms(entries, log);

        return new SoftwareCollectionResult { Entries = entries, FilteredCount = filteredCount };
    }

    private readonly record struct UninstallRoot(uint HDefKey, string BasePath, RegistryView View, SoftwareSource Source, string HiveLabel);

    private static IEnumerable<UninstallRoot> Roots()
    {
        yield return new UninstallRoot(HKeyLocalMachine, UninstallSubPath, RegistryView.Native, SoftwareSource.HklmUninstallKey, "HKLM");
        yield return new UninstallRoot(HKeyLocalMachine, Wow6432UninstallSubPath, RegistryView.Wow6432Node, SoftwareSource.HklmUninstallKey, "HKLM");
        yield return new UninstallRoot(HKeyCurrentUser, UninstallSubPath, RegistryView.Native, SoftwareSource.HkcuUninstallKey, "HKCU");
    }

    private int CollectRoot(UninstallRoot root, List<SoftwareEntry> entries, CollectionErrorLog log)
    {
        var filtered = 0;

        foreach (var subKeyName in EnumSubKeys(root, log))
        {
            var keyPath = $@"{root.BasePath}\{subKeyName}";

            var displayName = ReadOptionalString(root.HDefKey, keyPath, "DisplayName", log);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                filtered++;
                continue;
            }

            if (ReadOptionalDword(root.HDefKey, keyPath, "SystemComponent", log) == 1)
            {
                filtered++;
                continue;
            }

            // A populated ParentKeyName/ParentDisplayName means this key describes a sub-component
            // of some other, already-listed product (e.g. one feature of a suite) — not a program
            // in its own right.
            var parentKeyName = ReadOptionalString(root.HDefKey, keyPath, "ParentKeyName", log);
            var parentDisplayName = ReadOptionalString(root.HDefKey, keyPath, "ParentDisplayName", log);
            if (!string.IsNullOrWhiteSpace(parentKeyName) || !string.IsNullOrWhiteSpace(parentDisplayName))
            {
                filtered++;
                continue;
            }

            if (IsWindowsUpdateName(displayName))
            {
                filtered++;
                continue;
            }

            var publisher = ReadOptionalString(root.HDefKey, keyPath, "Publisher", log);
            var installDateRaw = ReadOptionalString(root.HDefKey, keyPath, "InstallDate", log);
            var estimatedSizeKb = ReadOptionalDword(root.HDefKey, keyPath, "EstimatedSize", log);

            entries.Add(new SoftwareEntry
            {
                Name = displayName,
                Version = ReadOptionalString(root.HDefKey, keyPath, "DisplayVersion", log),
                Publisher = publisher,
                InstallDate = ParseInstallDate(installDateRaw, keyPath, log),
                EstimatedSizeBytes = estimatedSizeKb is null ? null : estimatedSizeKb.Value * 1024L,
                UninstallString = ReadOptionalString(root.HDefKey, keyPath, "UninstallString", log),
                RegistryView = root.View,
                // The subkey name itself — an MSI ProductCode GUID or an installer-chosen literal
                // name, set once at install time and never re-translated if the display language
                // changes later. See SoftwareEntry.RegistryKeyName.
                RegistryKeyName = subKeyName,
                InstallLocation = ReadOptionalString(root.HDefKey, keyPath, "InstallLocation", log),
                Source = root.Source,
                // SystemComponent==1 entries were already dropped above, so every entry that
                // reaches here is genuinely false — never "unknown" (the registry value's mere
                // absence, the overwhelmingly common case, is itself a definite "not flagged").
                SystemComponent = false,
                Category = Classify(displayName, publisher)
            });
        }

        return filtered;
    }

    private IReadOnlyList<string> EnumSubKeys(UninstallRoot root, CollectionErrorLog log)
    {
        var source = $@"StdRegProv.EnumKey({root.HiveLabel}\{root.BasePath})";
        try
        {
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("hDefKey", root.HDefKey, CimType.UInt32, CimFlags.In),
                CimMethodParameter.Create("sSubKeyName", root.BasePath, CimType.String, CimFlags.In)
            };

            var result = _cim.InvokeStaticMethod("root/cimv2", "StdRegProv", "EnumKey", parameters);
            switch (result.ReturnCode)
            {
                case 0:
                    return result.GetOutParameter("sNames") as string[] ?? [];
                case 2: // ERROR_FILE_NOT_FOUND — this root genuinely has no Uninstall key (e.g. a fresh HKCU profile) — not an error.
                    return [];
                default:
                    log.Add(source, $"Registry sorgusu {result.ReturnCode} dönüş koduyla başarısız oldu.");
                    return [];
            }
        }
        catch (Exception ex)
        {
            log.Add(source, ex.Message);
            return [];
        }
    }

    private string? ReadOptionalString(uint hDefKey, string keyPath, string valueName, CollectionErrorLog log)
    {
        var source = $@"StdRegProv.GetStringValue({keyPath}\{valueName})";
        try
        {
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("hDefKey", hDefKey, CimType.UInt32, CimFlags.In),
                CimMethodParameter.Create("sSubKeyName", keyPath, CimType.String, CimFlags.In),
                CimMethodParameter.Create("sValueName", valueName, CimType.String, CimFlags.In)
            };

            var result = _cim.InvokeStaticMethod("root/cimv2", "StdRegProv", "GetStringValue", parameters);
            if (result.ReturnCode == 0)
            {
                return result.GetOutParameter("sValue") as string;
            }

            if (IsValueNotPresent(result.ReturnCode))
            {
                return null;
            }

            log.Add(source, $"Registry sorgusu {result.ReturnCode} dönüş koduyla başarısız oldu.");
            return null;
        }
        catch (Exception ex)
        {
            log.Add(source, ex.Message);
            return null;
        }
    }

    private uint? ReadOptionalDword(uint hDefKey, string keyPath, string valueName, CollectionErrorLog log)
    {
        var source = $@"StdRegProv.GetDWORDValue({keyPath}\{valueName})";
        try
        {
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("hDefKey", hDefKey, CimType.UInt32, CimFlags.In),
                CimMethodParameter.Create("sSubKeyName", keyPath, CimType.String, CimFlags.In),
                CimMethodParameter.Create("sValueName", valueName, CimType.String, CimFlags.In)
            };

            var result = _cim.InvokeStaticMethod("root/cimv2", "StdRegProv", "GetDWORDValue", parameters);
            if (result.ReturnCode == 0)
            {
                var value = result.GetOutParameter("uValue");
                return value is null ? null : Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            }

            if (IsValueNotPresent(result.ReturnCode))
            {
                return null;
            }

            log.Add(source, $"Registry sorgusu {result.ReturnCode} dönüş koduyla başarısız oldu.");
            return null;
        }
        catch (Exception ex)
        {
            log.Add(source, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// StdRegProv's GetStringValue/GetDWORDValue return 2 (ERROR_FILE_NOT_FOUND) when the key
    /// itself does not exist, but — confirmed empirically against a real machine, since this
    /// particular case is not the one already documented/handled elsewhere in this codebase (see
    /// FirmwareCollector's SecureBoot read, which only ever hits the "whole key missing" case) —
    /// return 1 when the key exists but this specific value simply is not set on it. Both are the
    /// overwhelmingly common, entirely normal case for most of these optional Uninstall values
    /// (e.g. most apps never set EstimatedSize or ParentKeyName at all): neither is an error.
    /// </summary>
    private static bool IsValueNotPresent(uint returnCode) => returnCode is 1 or 2;

    /// <summary>Registry <c>InstallDate</c> is an unseparated "YYYYMMDD" string — see docs/schema/profile-v0.1.md.</summary>
    private static DateOnly? ParseInstallDate(string? raw, string keyPath, CollectionErrorLog log)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (DateOnly.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        log.Add($@"StdRegProv.GetStringValue({keyPath}\InstallDate)", $"Beklenmeyen biçim (\"YYYYMMDD\" bekleniyordu): '{raw}'.");
        return null;
    }

    private bool IsWindowsUpdateName(string displayName)
    {
        if (_options.TreatKbPrefixedNamesAsUpdates && KbHotfixNamePattern.IsMatch(displayName))
        {
            return true;
        }

        return ContainsAny(displayName, _options.UpdateNameSubstrings);
    }

    /// <summary>Driver, then runtime, then system — checked in this order since a name could plausibly match more than one list (e.g. a bundled runtime installer named "... Driver Package").</summary>
    private SoftwareCategory Classify(string name, string? publisher)
    {
        if (ContainsAny(name, _options.DriverNameKeywords)) return SoftwareCategory.driver;
        if (ContainsAny(name, _options.RuntimeNameKeywords)) return SoftwareCategory.runtime;
        if (ContainsAny(name, _options.SystemNameKeywords)) return SoftwareCategory.system;
        return SoftwareCategory.application;
    }

    private static bool ContainsAny(string haystack, IReadOnlyList<string> needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// MSIX/Store apps never appear in the registry Uninstall keys above at all — this is the only
    /// way to see them. Best-effort: this WMI class is undocumented and not guaranteed present on
    /// every Windows edition/build, so a failure here is logged, never fabricated.
    /// </summary>
    private int CollectStorePrograms(List<SoftwareEntry> entries, CollectionErrorLog log)
    {
        IReadOnlyList<CimInstance> instances;
        try
        {
            instances = _cim.QueryInstances("root/cimv2", StoreProgramSource);
        }
        catch (Exception ex)
        {
            log.Add(StoreProgramSource, ex.Message);
            return 0;
        }

        var filtered = 0;
        foreach (var instance in instances)
        {
            // Unlike a registry DisplayName, this class's "Name" is a package identifier (e.g.
            // "Microsoft.WindowsCalculator"), not a friendly display name — Windows does not
            // expose a nicer one through this class. Kept anyway (better than nothing entirely)
            // and marked with its own Source so a report can treat it differently later.
            var name = instance.GetString("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                filtered++;
                continue;
            }

            var publisher = instance.GetString("Vendor");

            entries.Add(new SoftwareEntry
            {
                Name = name,
                Version = instance.GetString("Version"),
                Publisher = publisher,
                InstallDate = instance.GetDateOnly("InstallDate"),
                EstimatedSizeBytes = null,
                UninstallString = null,
                RegistryView = null,
                Source = SoftwareSource.StorePackage,
                SystemComponent = false,
                Category = Classify(name, publisher)
            });
        }

        return filtered;
    }
}
