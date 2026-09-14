using Scout.Core.Models;

namespace Scout.Analyzer.Analysis;

/// <summary>
/// Shared "how much free space is on the system disk" calculation — used by
/// <see cref="VerdictEngine"/> (for the <see cref="SystemConstraintKind.LowDiskSpace"/> check) and
/// by <see cref="Recommendations.DistributionRecommender"/> (to filter out distributions that
/// would not fit), so the two never disagree on what counts as "the system disk".
/// </summary>
public static class SystemDiskSpace
{
    /// <summary>
    /// Sums <see cref="PartitionInfo.FreeBytes"/> across the boot disk's mounted (drive-lettered)
    /// partitions. Null — never guessed as 0 — when there is no confirmed boot disk, or when none
    /// of its mounted partitions report a free-space figure, matching the rest of the profile's
    /// "could not read" convention.
    /// </summary>
    public static long? FreeBytes(MachineProfile profile)
    {
        var bootDisk = profile.Storage.Disks.FirstOrDefault(d => d.IsBootDisk == true);
        if (bootDisk is null)
        {
            return null;
        }

        var readings = bootDisk.Partitions
            .Where(p => p.DriveLetter is not null && p.FreeBytes is not null)
            .Select(p => p.FreeBytes!.Value)
            .ToList();

        return readings.Count == 0 ? null : readings.Sum();
    }
}
