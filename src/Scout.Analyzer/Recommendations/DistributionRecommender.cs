using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Models;

namespace Scout.Analyzer.Recommendations;

/// <summary>
/// Picks exactly one recommended Linux distribution for a machine, plus one alternative — never a
/// ranked list (see docs/schema/distributions-v0.1.md for why). Decides using, in priority order:
/// available RAM (a lightweight desktop environment below the configured threshold), whether a
/// display device needs a proprietary driver (prefer a distribution with a built-in GUI installer
/// for it), and otherwise the general-purpose default (chosen for Windows-like familiarity, since
/// every profile this runs against is, by definition, a Windows machine). CPU feature level and
/// free disk space are used to exclude candidates that would not actually work, not to choose
/// between ones that do.
/// </summary>
/// <remarks>
/// Firmware boot mode is deliberately not used to choose between candidates: every distribution in
/// the curated database supports both UEFI and Legacy/BIOS boot equally, so there is no real
/// distinction to base a pick on here — the machine's boot mode is already surfaced separately via
/// <see cref="SystemConstraintKind.LegacyBootMode"/>, so nothing is lost by not repeating it here.
/// </remarks>
public sealed class DistributionRecommender
{
    /// <summary>
    /// A plain, hardcoded fallback order for picking the alternative suggestion — not a database
    /// field, since it only matters when two candidates are otherwise equally good and does not
    /// need to be user-configurable data.
    /// </summary>
    private static readonly string[] GeneralPreferenceOrder =
        ["Linux Mint", "Ubuntu", "Xubuntu", "Fedora", "Debian", "Lubuntu"];

    private readonly DistributionDatabase _database;
    private readonly long _lowMemoryThresholdBytes;

    /// <param name="lowMemoryThresholdBytes">
    /// Below this much total RAM (default 4 GiB, matching <see cref="VerdictEngine"/>'s own
    /// default), a <see cref="DistributionEntry.Lightweight"/> distribution is preferred.
    /// </param>
    public DistributionRecommender(DistributionDatabase database, long lowMemoryThresholdBytes = 4L * 1024 * 1024 * 1024)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _lowMemoryThresholdBytes = lowMemoryThresholdBytes;
    }

    /// <param name="profile">The machine profile.</param>
    /// <param name="deviceAssessments">Already-matched device assessments (see <see cref="MachineAnalyzer"/>) — used to detect a proprietary-driver GPU.</param>
    public DistributionRecommendation Recommend(MachineProfile profile, IReadOnlyList<DeviceAssessment> deviceAssessments)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(deviceAssessments);

        var eligible = _database.Entries.Where(d => IsEligible(d, profile)).ToList();
        var reason = DistributionRecommendationReason.LimitedHardware;

        if (eligible.Count == 0)
        {
            // Nothing meets its comfortable minimum — still offer the least demanding option
            // rather than nothing at all; the report is expected to caveat this via the machine's
            // own low-memory/low-disk system constraints, already shown elsewhere.
            eligible = _database.Entries
                .OrderBy(d => d.MinimumMemoryBytes ?? long.MaxValue)
                .Take(1)
                .ToList();
        }
        else if (profile.Memory.TotalBytes is { } totalMemory && totalMemory < _lowMemoryThresholdBytes)
        {
            reason = DistributionRecommendationReason.LowMemory;
        }
        else if (NeedsProprietaryGpuDriver(deviceAssessments))
        {
            reason = DistributionRecommendationReason.NvidiaProprietaryDriver;
        }
        else
        {
            reason = DistributionRecommendationReason.WindowsFamiliarity;
        }

        var primary = PickPrimary(eligible, reason);
        var secondary = PickSecondary(eligible, primary);

        return new DistributionRecommendation { Primary = primary, PrimaryReason = reason, Secondary = secondary };
    }

    private static DistributionEntry PickPrimary(List<DistributionEntry> eligible, DistributionRecommendationReason reason)
    {
        switch (reason)
        {
            case DistributionRecommendationReason.LowMemory:
                // Among lightweight options, prefer the one using the most of what little RAM is
                // available (a better desktop experience) rather than always picking the very
                // lightest — that is only a fallback for when even that does not fit.
                var lightweight = eligible.Where(d => d.Lightweight).OrderByDescending(d => d.MinimumMemoryBytes ?? 0).FirstOrDefault();
                return lightweight ?? eligible.OrderBy(d => d.MinimumMemoryBytes ?? long.MaxValue).First();

            case DistributionRecommendationReason.NvidiaProprietaryDriver:
                return eligible.FirstOrDefault(d => d.OffersProprietaryDriverInstaller) ?? eligible.First();

            case DistributionRecommendationReason.LimitedHardware:
                return eligible.First();

            case DistributionRecommendationReason.WindowsFamiliarity:
            default:
                return eligible.FirstOrDefault(d => d.IsDefaultChoice) ?? eligible.First();
        }
    }

    private static DistributionEntry? PickSecondary(List<DistributionEntry> eligible, DistributionEntry primary) =>
        GeneralPreferenceOrder
            .Select(name => eligible.FirstOrDefault(d => d.Name == name))
            .FirstOrDefault(d => d is not null && d != primary)
        ?? eligible.FirstOrDefault(d => d != primary);

    private static bool IsEligible(DistributionEntry distro, MachineProfile profile)
    {
        if (profile.Cpu.X86_64FeatureLevel is { } level && level < distro.MinimumFeatureLevel)
        {
            return false;
        }

        if (profile.Memory.TotalBytes is { } totalMemory && distro.MinimumMemoryBytes is { } minMemory && totalMemory < minMemory)
        {
            return false;
        }

        if (SystemDiskSpace.FreeBytes(profile) is { } freeBytes && distro.MinimumDiskBytes is { } minDisk && freeBytes < minDisk)
        {
            return false;
        }

        return true;
    }

    private static bool NeedsProprietaryGpuDriver(IReadOnlyList<DeviceAssessment> deviceAssessments) =>
        deviceAssessments.Any(a =>
            string.Equals(a.Device.Class, "Display", StringComparison.OrdinalIgnoreCase) &&
            a.Match.Support == SupportLevel.Proprietary);
}
