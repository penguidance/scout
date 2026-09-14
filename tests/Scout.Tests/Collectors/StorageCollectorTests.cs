using Scout.Collector.Cim;
using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class StorageCollectorTests
{
    private const string StorageNamespace = "root/Microsoft/Windows/Storage";
    private const string BitLockerNamespace = "root/CIMV2/Security/MicrosoftVolumeEncryption";

    private static FakeCimQueryExecutor CreateNvmeDiskWithTwoPartitions()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances(StorageNamespace, "MSFT_Disk", FakeCimQueryExecutor.Instance(
            "MSFT_Disk",
            ("Number", (uint)0),
            ("BusType", (ushort)17), // NVMe
            ("Size", (ulong)500_107_862_016),
            ("FriendlyName", "MSI M450 500GB"),
            ("IsBoot", true),
            ("PartitionStyle", (ushort)2))); // GPT
        cim.SetInstances(StorageNamespace, "MSFT_PhysicalDisk", FakeCimQueryExecutor.Instance(
            "MSFT_PhysicalDisk", ("DeviceId", "0"), ("MediaType", (ushort)4), ("FirmwareVersion", "11002111")));
        // DriveLetter is char16 on MSFT_Partition/MSFT_Volume (a boxed System.Char), not a
        // string — unlike Win32_EncryptableVolume.DriveLetter below, which really is a string.
        // A real, un-lettered partition reports '\0' (observed on real hardware), not a space.
        cim.SetInstances(StorageNamespace, "MSFT_Partition",
            FakeCimQueryExecutor.Instance(
                "MSFT_Partition", ("DiskNumber", (uint)0), ("DriveLetter", '\0'), ("Size", (ulong)104_857_600)),
            FakeCimQueryExecutor.Instance(
                "MSFT_Partition", ("DiskNumber", (uint)0), ("DriveLetter", 'C'), ("Size", (ulong)492_946_063_360)));
        cim.SetInstances(StorageNamespace, "MSFT_Volume", FakeCimQueryExecutor.Instance(
            "MSFT_Volume", ("DriveLetter", 'C'), ("FileSystem", "NTFS"), ("SizeRemaining", (ulong)200_000_000_000)));
        return cim;
    }

    [Fact]
    public void Collect_MapsDiskAndDistinguishesLetteredFromUnletteredPartitions()
    {
        var cim = CreateNvmeDiskWithTwoPartitions();
        cim.SetInstances(BitLockerNamespace, "Win32_EncryptableVolume", FakeCimQueryExecutor.Instance(
            "Win32_EncryptableVolume", ("DriveLetter", "C:")));
        cim.SetInstanceMethodHandler(BitLockerNamespace, "GetConversionStatus", _ => new CimStaticMethodResult
        {
            ReturnCode = 0,
            OutParameters = new Dictionary<string, object?> { ["ConversionStatus"] = (uint)1 } // FullyEncrypted
        });

        var errors = new List<CollectionError>();
        var result = new StorageCollector(cim).Collect(errors);

        var disk = Assert.Single(result.Disks);
        Assert.Equal(0, disk.DiskNumber);
        Assert.Equal(StorageMediaType.NVMe, disk.MediaType); // NVMe bus wins over MSFT_PhysicalDisk's "SSD"
        Assert.Equal("NVMe", disk.BusType);
        Assert.Equal(500_107_862_016, disk.SizeBytes);
        Assert.Equal("MSI M450 500GB", disk.Model);
        Assert.Equal("11002111", disk.FirmwareVersion);
        Assert.True(disk.IsBootDisk);
        Assert.Equal(PartitionStyle.GPT, disk.PartitionStyle);
        Assert.Equal(2, disk.Partitions.Count);

        var unlettered = disk.Partitions.Single(p => p.DriveLetter is null);
        Assert.Null(unlettered.Filesystem);
        Assert.Equal(BitLockerStatus.NotApplicable, unlettered.BitlockerStatus);
        Assert.Equal(104_857_600, unlettered.SizeBytes);

        var lettered = disk.Partitions.Single(p => p.DriveLetter == "C");
        Assert.Equal("NTFS", lettered.Filesystem);
        Assert.Equal(BitLockerStatus.FullyEncrypted, lettered.BitlockerStatus);
        Assert.Equal(200_000_000_000, lettered.FreeBytes);
        Assert.Null(unlettered.FreeBytes);

        Assert.True(result.BitlockerAvailable);
        Assert.Empty(errors);
    }

    [Fact]
    public void Collect_WhenVolumeHasNoSizeRemaining_LeavesFreeBytesNullNotZero()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances(StorageNamespace, "MSFT_Disk", FakeCimQueryExecutor.Instance(
            "MSFT_Disk", ("Number", (uint)0), ("BusType", (ushort)17), ("Size", (ulong)1), ("FriendlyName", "Disk")));
        cim.SetInstances(StorageNamespace, "MSFT_Partition", FakeCimQueryExecutor.Instance(
            "MSFT_Partition", ("DiskNumber", (uint)0), ("DriveLetter", 'C'), ("Size", (ulong)1)));
        // Volume exists (so Filesystem still resolves) but does not report SizeRemaining.
        cim.SetInstances(StorageNamespace, "MSFT_Volume", FakeCimQueryExecutor.Instance(
            "MSFT_Volume", ("DriveLetter", 'C'), ("FileSystem", "NTFS")));
        cim.SetInstances(BitLockerNamespace, "Win32_EncryptableVolume");

        var result = new StorageCollector(cim).Collect([]);

        var lettered = result.Disks[0].Partitions.Single(p => p.DriveLetter == "C");
        Assert.Equal("NTFS", lettered.Filesystem);
        Assert.Null(lettered.FreeBytes);
    }

    [Fact]
    public void Collect_WhenEncryptableVolumeQueryFails_LeavesBitlockerAvailableAndStatusNullNotFabricated()
    {
        var cim = CreateNvmeDiskWithTwoPartitions();
        cim.SetInstanceQueryFailure(
            BitLockerNamespace, "Win32_EncryptableVolume", new UnauthorizedAccessException("Access denied"));

        var errors = new List<CollectionError>();
        var result = new StorageCollector(cim).Collect(errors);

        Assert.Null(result.BitlockerAvailable);
        var lettered = result.Disks[0].Partitions.Single(p => p.DriveLetter == "C");
        Assert.Null(lettered.BitlockerStatus);
        Assert.Contains(errors, e => e.Component == "storage" && e.Source == "Win32_EncryptableVolume");
    }

    [Theory]
    [InlineData((ushort)11, (ushort)4, StorageMediaType.SSD)] // SATA bus, MSFT_PhysicalDisk says SSD
    [InlineData((ushort)11, (ushort)3, StorageMediaType.HDD)] // SATA bus, MSFT_PhysicalDisk says HDD
    public void Collect_DecodesMediaTypeFromPhysicalDiskWhenBusIsNotNvme(
        ushort busType, ushort physicalDiskMediaType, StorageMediaType expected)
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances(StorageNamespace, "MSFT_Disk", FakeCimQueryExecutor.Instance(
            "MSFT_Disk", ("Number", (uint)0), ("BusType", busType), ("Size", (ulong)1), ("FriendlyName", "Disk")));
        cim.SetInstances(StorageNamespace, "MSFT_PhysicalDisk", FakeCimQueryExecutor.Instance(
            "MSFT_PhysicalDisk", ("DeviceId", "0"), ("MediaType", physicalDiskMediaType)));
        cim.SetInstances(BitLockerNamespace, "Win32_EncryptableVolume");

        var result = new StorageCollector(cim).Collect([]);

        Assert.Equal(expected, result.Disks[0].MediaType);
    }

    [Fact]
    public void Collect_WhenDiskQueryThrows_ReturnsEmptyDisksAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstanceQueryFailure(StorageNamespace, "MSFT_Disk", new InvalidOperationException("bağlantı koptu"));
        cim.SetInstances(BitLockerNamespace, "Win32_EncryptableVolume");

        var errors = new List<CollectionError>();
        var result = new StorageCollector(cim).Collect(errors);

        Assert.Empty(result.Disks);
        Assert.Contains(errors, e => e.Component == "storage" && e.Source == "MSFT_Disk");
    }

    [Fact]
    public void Collect_WhenPartitionSizeUnreadable_LeavesItNullNotZero()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances(StorageNamespace, "MSFT_Disk", FakeCimQueryExecutor.Instance(
            "MSFT_Disk", ("Number", (uint)0), ("BusType", (ushort)17), ("Size", (ulong)1), ("FriendlyName", "Disk")));
        cim.SetInstances(StorageNamespace, "MSFT_Partition", FakeCimQueryExecutor.Instance(
            "MSFT_Partition", ("DiskNumber", (uint)0))); // no Size property at all
        cim.SetInstances(BitLockerNamespace, "Win32_EncryptableVolume");

        var errors = new List<CollectionError>();
        var result = new StorageCollector(cim).Collect(errors);

        Assert.Null(result.Disks[0].Partitions[0].SizeBytes);
        Assert.Contains(errors, e => e.Component == "storage" && e.Source == "MSFT_Partition");
    }

    [Fact]
    public void Collect_WhenGetConversionStatusReturnsNonZero_LeavesStatusNullAndLogsError()
    {
        var cim = CreateNvmeDiskWithTwoPartitions();
        cim.SetInstances(BitLockerNamespace, "Win32_EncryptableVolume", FakeCimQueryExecutor.Instance(
            "Win32_EncryptableVolume", ("DriveLetter", "C:")));
        cim.SetInstanceMethodHandler(BitLockerNamespace, "GetConversionStatus", _ => new CimStaticMethodResult
        {
            ReturnCode = 2,
            OutParameters = new Dictionary<string, object?>()
        });

        var errors = new List<CollectionError>();
        var result = new StorageCollector(cim).Collect(errors);

        var lettered = result.Disks[0].Partitions.Single(p => p.DriveLetter == "C");
        Assert.Null(lettered.BitlockerStatus);
        Assert.Contains(errors, e => e.Component == "storage" && e.Source == "Win32_EncryptableVolume.GetConversionStatus");
    }
}
