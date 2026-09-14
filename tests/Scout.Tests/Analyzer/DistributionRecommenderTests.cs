using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Analyzer.Recommendations;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Analyzer;

public class DistributionRecommenderTests
{
    private const string Database = """
        [
          {
            "name": "Default Pick",
            "version": "1.0",
            "download_url": "https://example.invalid/default",
            "desktop_environment": "DefaultDE",
            "minimum_feature_level": "v1",
            "minimum_memory_bytes": 2147483648,
            "minimum_disk_bytes": 10737418240,
            "offers_proprietary_driver_installer": false,
            "lightweight": false,
            "is_default_choice": true,
            "recommended_when": "n/a"
          },
          {
            "name": "Driver Friendly",
            "version": "1.0",
            "download_url": "https://example.invalid/driver-friendly",
            "desktop_environment": "DriverDE",
            "minimum_feature_level": "v1",
            "minimum_memory_bytes": 2147483648,
            "minimum_disk_bytes": 10737418240,
            "offers_proprietary_driver_installer": true,
            "lightweight": false,
            "is_default_choice": false,
            "recommended_when": "n/a"
          },
          {
            "name": "Lightweight Pick",
            "version": "1.0",
            "download_url": "https://example.invalid/lightweight",
            "desktop_environment": "LightDE",
            "minimum_feature_level": "v1",
            "minimum_memory_bytes": 1073741824,
            "minimum_disk_bytes": 5368709120,
            "offers_proprietary_driver_installer": false,
            "lightweight": true,
            "is_default_choice": false,
            "recommended_when": "n/a"
          },
          {
            "name": "Bleeding Edge",
            "version": "1.0",
            "download_url": "https://example.invalid/bleeding-edge",
            "desktop_environment": "EdgeDE",
            "minimum_feature_level": "v3",
            "minimum_memory_bytes": 4294967296,
            "minimum_disk_bytes": 10737418240,
            "offers_proprietary_driver_installer": false,
            "lightweight": false,
            "is_default_choice": false,
            "recommended_when": "n/a"
          }
        ]
        """;

    private static DistributionRecommender CreateRecommender(long lowMemoryThresholdBytes = 4L * 1024 * 1024 * 1024) =>
        new(DistributionDatabase.FromJson(Database), lowMemoryThresholdBytes);

    private static MachineProfile ProfileFor(
        X86FeatureLevel? featureLevel = X86FeatureLevel.v3,
        long? totalMemoryBytes = 16L * 1024 * 1024 * 1024,
        long? systemDiskFreeBytes = 100L * 1024 * 1024 * 1024) => new()
    {
        SchemaVersion = "0.1",
        CollectorVersion = "test",
        CollectedAt = DateTimeOffset.UtcNow,
        MachineId = "sha256:test",
        Privacy = new PrivacyInfo { HostnameIncluded = false, UsernameIncluded = false, SerialNumbersIncluded = false, RedactedFields = [] },
        System = new SystemInfo { Manufacturer = "Test", Model = "Test", ChassisType = ChassisType.Desktop },
        Firmware = new FirmwareInfo { BiosVendor = "Test", BiosVersion = "1.0", BootMode = BootMode.UEFI, Tpm = new TpmInfo { Present = false } },
        Cpu = new CpuInfo { Vendor = "Test", Model = "Test", PhysicalCores = 1, LogicalProcessors = 1, Architecture = CpuArchitecture.x86_64, X86_64FeatureLevel = featureLevel },
        Memory = new MemoryInfo { TotalBytes = totalMemoryBytes, Modules = [] },
        Storage = new StorageInfo
        {
            BitlockerAvailable = null,
            Disks = systemDiskFreeBytes is null
                ? []
                :
                [
                    new DiskInfo
                    {
                        DiskNumber = 0, MediaType = StorageMediaType.SSD, BusType = "SATA", SizeBytes = 500_000_000_000,
                        Model = "Test SSD", IsBootDisk = true, PartitionStyle = PartitionStyle.GPT,
                        Partitions = [new PartitionInfo { Filesystem = "NTFS", SizeBytes = 500_000_000_000, DriveLetter = "C", BitlockerStatus = BitLockerStatus.NotApplicable, FreeBytes = systemDiskFreeBytes }]
                    }
                ]
        },
        Devices = [],
        GpuTopology = new GpuTopology { Gpus = [], Layout = null },
        Os = new OsInfo { Edition = "Test", Version = "1.0", Build = "1", Architecture = CpuArchitecture.x86_64 },
        Software = [],
        Peripherals = [],
        CollectionErrors = []
    };

    private static DeviceAssessment DisplayDevice(SupportLevel support) => new()
    {
        Device = new DeviceInfo
        {
            HardwareId = @"PCI\VEN_10DE&DEV_0000", HardwareIdRaw = @"PCI\VEN_10DE&DEV_0000",
            VendorId = "10DE", DeviceId = "0000", BusType = DeviceBusType.PCI, CompatibleIds = [],
            FriendlyName = "Test GPU", Class = "Display", Status = DeviceStatus.OK
        },
        Match = new CompatibilityMatch { Level = MatchLevel.Exact, Entry = null, Support = support }
    };

    [Fact]
    public void Recommend_NoSpecialSignals_PicksTheDefaultChoice()
    {
        var result = CreateRecommender().Recommend(ProfileFor(), []);

        Assert.Equal("Default Pick", result.Primary.Name);
        Assert.Equal(DistributionRecommendationReason.WindowsFamiliarity, result.PrimaryReason);
    }

    [Fact]
    public void Recommend_LowMemory_PicksTheLightweightOption()
    {
        var result = CreateRecommender().Recommend(ProfileFor(totalMemoryBytes: 2L * 1024 * 1024 * 1024), []);

        Assert.Equal("Lightweight Pick", result.Primary.Name);
        Assert.Equal(DistributionRecommendationReason.LowMemory, result.PrimaryReason);
    }

    [Fact]
    public void Recommend_ProprietaryGpuDriverNeeded_PicksTheDriverFriendlyOption()
    {
        var devices = new[] { DisplayDevice(SupportLevel.Proprietary) };

        var result = CreateRecommender().Recommend(ProfileFor(), devices);

        Assert.Equal("Driver Friendly", result.Primary.Name);
        Assert.Equal(DistributionRecommendationReason.NvidiaProprietaryDriver, result.PrimaryReason);
    }

    [Fact]
    public void Recommend_LowMemoryTakesPriorityOverProprietaryDriverNeed()
    {
        // Both signals present — a machine that cannot comfortably run a full desktop at all is a
        // more urgent constraint than which desktop offers a driver installer.
        var devices = new[] { DisplayDevice(SupportLevel.Proprietary) };

        var result = CreateRecommender().Recommend(ProfileFor(totalMemoryBytes: 2L * 1024 * 1024 * 1024), devices);

        Assert.Equal(DistributionRecommendationReason.LowMemory, result.PrimaryReason);
    }

    [Fact]
    public void Recommend_NonProprietaryGpu_DoesNotTriggerTheDriverSignal()
    {
        var devices = new[] { DisplayDevice(SupportLevel.Native) };

        var result = CreateRecommender().Recommend(ProfileFor(), devices);

        Assert.Equal(DistributionRecommendationReason.WindowsFamiliarity, result.PrimaryReason);
    }

    [Fact]
    public void Recommend_ExcludesADistributionRequiringAHigherFeatureLevel()
    {
        // "Bleeding Edge" requires v3 — a v2 machine must never see it recommended at all.
        var result = CreateRecommender().Recommend(ProfileFor(featureLevel: X86FeatureLevel.v2), []);

        Assert.NotEqual("Bleeding Edge", result.Primary.Name);
        Assert.NotEqual("Bleeding Edge", result.Secondary?.Name);
    }

    [Fact]
    public void Recommend_UnknownFeatureLevel_NeverExcludesACandidate()
    {
        // "Could not determine" must not be treated as "definitely too old" — the null case is
        // skipped, not treated as a failure, consistent with the rest of the profile schema.
        var result = CreateRecommender().Recommend(ProfileFor(featureLevel: null), []);

        Assert.Equal("Default Pick", result.Primary.Name);
    }

    [Fact]
    public void Recommend_UnknownMemory_NeverExcludesACandidateOnMemoryAlone()
    {
        var result = CreateRecommender().Recommend(ProfileFor(totalMemoryBytes: null), []);

        Assert.Equal("Default Pick", result.Primary.Name);
    }

    [Fact]
    public void Recommend_BelowEveryCandidatesMinimum_StillPicksTheLeastDemandingOption()
    {
        var result = CreateRecommender().Recommend(ProfileFor(totalMemoryBytes: 200L * 1024 * 1024), []);

        Assert.Equal(DistributionRecommendationReason.LimitedHardware, result.PrimaryReason);
        Assert.Equal("Lightweight Pick", result.Primary.Name); // the lowest minimum-memory entry
    }

    [Fact]
    public void Recommend_AlwaysReturnsASecondaryDifferentFromPrimary_WhenMoreThanOneEntryExists()
    {
        var result = CreateRecommender().Recommend(ProfileFor(), []);

        Assert.NotNull(result.Secondary);
        Assert.NotEqual(result.Primary.Name, result.Secondary!.Name);
    }

    [Fact]
    public void Recommend_SingleEntryDatabase_HasNoSecondary()
    {
        const string singleEntryDb = """
            [{
              "name": "Only Option", "version": "1.0", "download_url": "https://example.invalid/only",
              "desktop_environment": "OnlyDE", "minimum_feature_level": "v1",
              "offers_proprietary_driver_installer": false, "lightweight": false,
              "is_default_choice": true, "recommended_when": "n/a"
            }]
            """;
        var recommender = new DistributionRecommender(DistributionDatabase.FromJson(singleEntryDb));

        var result = recommender.Recommend(ProfileFor(), []);

        Assert.Null(result.Secondary);
    }

    [Fact]
    public void Constructor_RequiresADatabase()
    {
        Assert.Throws<ArgumentNullException>(() => new DistributionRecommender(null!));
    }

    [Fact]
    public void Recommend_ThrowsOnNullArguments()
    {
        var recommender = CreateRecommender();

        Assert.Throws<ArgumentNullException>(() => recommender.Recommend(null!, []));
        Assert.Throws<ArgumentNullException>(() => recommender.Recommend(ProfileFor(), null!));
    }
}
