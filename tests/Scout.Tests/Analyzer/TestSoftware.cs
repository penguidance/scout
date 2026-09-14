using Scout.Core.Models;

namespace Scout.Tests.Analyzer;

/// <summary>Small factory for building minimal <see cref="SoftwareEntry"/> fixtures in tests.</summary>
internal static class TestSoftware
{
    public static SoftwareEntry Create(
        string name,
        string? publisher = null,
        SoftwareCategory category = SoftwareCategory.application,
        SoftwareSource source = SoftwareSource.HklmUninstallKey,
        string? registryKeyName = null) =>
        new()
        {
            Name = name,
            Publisher = publisher,
            Source = source,
            Category = category,
            RegistryView = RegistryView.Native,
            RegistryKeyName = registryKeyName,
            SystemComponent = false
        };
}
