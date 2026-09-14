using Scout.Analyzer.Compatibility;
using Scout.Core.Models;

namespace Scout.Analyzer.Analysis;

/// <summary>
/// Turns a set of per-device <see cref="CompatibilityMatch"/> results, plus a set of system-wide
/// <see cref="SystemConstraint"/> findings, into one machine-wide <see cref="Analysis.Verdict"/>.
/// Kept separate from <see cref="MachineAnalyzer"/> and from the matcher/filter so the decision
/// rule itself is easy to unit test in isolation.
/// </summary>
/// <remarks>
/// The two inputs are deliberately evaluated independently and only combined at the very end:
/// device compatibility answers "will this specific hardware work", system constraints answer
/// "does moving this machine to Linux at all come with limitations" (CPU baseline, boot mode,
/// memory, disk space) — a device-perfect machine can still be far from a clean Ready call, and a
/// device with real problems should never have those masked by an otherwise-fine system profile.
/// See <see cref="EvaluateSystemConstraints"/> for how the latter list is built.
/// </remarks>
public sealed class VerdictEngine
{
    private readonly double _insufficientDataUnknownRatio;
    private readonly X86FeatureLevel _minimumRecommendedFeatureLevel;
    private readonly long _minimumRecommendedMemoryBytes;
    private readonly long _minimumFreeSystemDiskBytes;

    /// <param name="insufficientDataUnknownRatio">
    /// If the fraction of relevant devices with <see cref="SupportLevel.Unknown"/> support
    /// exceeds this (default 0.5, i.e. a majority), the verdict is
    /// <see cref="Verdict.InsufficientData"/> regardless of what else was found — with that
    /// little confirmed data, reporting a more specific verdict would overstate what is actually
    /// known. A minority of unknowns alongside otherwise-clean results does not, by itself,
    /// block a <see cref="Verdict.Ready"/> call.
    /// </param>
    /// <param name="minimumRecommendedFeatureLevel">
    /// Below this <see cref="X86FeatureLevel"/> (default <see cref="X86FeatureLevel.v3"/>), the
    /// CPU is flagged as a soft <see cref="SystemConstraint"/> — some current distributions build
    /// for a v3 baseline and refuse to run (or run unaccelerated) on older CPUs.
    /// </param>
    /// <param name="minimumRecommendedMemoryBytes">
    /// Below this much total RAM (default 4 GiB), memory is flagged as a soft
    /// <see cref="SystemConstraint"/> — a full desktop environment becomes impractical.
    /// </param>
    /// <param name="minimumFreeSystemDiskBytes">
    /// Below this much free space on the system (boot) disk (default 25 GiB), storage is flagged
    /// as a critical <see cref="SystemConstraint"/> — there may not be room to install at all.
    /// </param>
    public VerdictEngine(
        double insufficientDataUnknownRatio = 0.5,
        X86FeatureLevel minimumRecommendedFeatureLevel = X86FeatureLevel.v3,
        long minimumRecommendedMemoryBytes = 4L * 1024 * 1024 * 1024,
        long minimumFreeSystemDiskBytes = 25L * 1024 * 1024 * 1024)
    {
        if (insufficientDataUnknownRatio is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(insufficientDataUnknownRatio), "Must be between 0 and 1.");
        }

        _insufficientDataUnknownRatio = insufficientDataUnknownRatio;
        _minimumRecommendedFeatureLevel = minimumRecommendedFeatureLevel;
        _minimumRecommendedMemoryBytes = minimumRecommendedMemoryBytes;
        _minimumFreeSystemDiskBytes = minimumFreeSystemDiskBytes;
    }

    /// <summary>Device compatibility only, no system constraints or software findings — equivalent to calling the full overload with two empty lists.</summary>
    public Verdict Decide(IReadOnlyList<CompatibilityMatch> matches) => Decide(matches, [], []);

    /// <summary>Device compatibility plus system constraints, no software findings — equivalent to calling the full overload with an empty software list.</summary>
    public Verdict Decide(IReadOnlyList<CompatibilityMatch> matches, IReadOnlyList<SystemConstraint> systemConstraints) =>
        Decide(matches, systemConstraints, []);

    /// <summary>
    /// Combines the device-compatibility verdict with the floors imposed by
    /// <paramref name="systemConstraints"/> and <paramref name="softwareAssessments"/>: any
    /// <see cref="SystemConstraintSeverity.Critical"/> constraint raises the result to at least
    /// <see cref="Verdict.NeedsAttention"/>; otherwise any <see cref="SystemConstraintSeverity.Soft"/>
    /// constraint raises it to at least <see cref="Verdict.MinorIssues"/>. Separately, a
    /// <see cref="SoftwareCompatibilityStatus.Blocked"/> program whose database entry has
    /// <see cref="SoftwareImportance.Critical"/> also raises the result to at least
    /// <see cref="Verdict.NeedsAttention"/>. Every floor only ever raises the verdict, never
    /// lowers one a worse device finding already produced.
    /// </summary>
    public Verdict Decide(
        IReadOnlyList<CompatibilityMatch> matches,
        IReadOnlyList<SystemConstraint> systemConstraints,
        IReadOnlyList<SoftwareAssessment> softwareAssessments)
    {
        var deviceVerdict = DecideFromDevices(matches);
        var constraintFloor = FloorFromConstraints(systemConstraints);
        var softwareFloor = FloorFromSoftware(softwareAssessments);

        var floor = SeverityRank(softwareFloor) > SeverityRank(constraintFloor) ? softwareFloor : constraintFloor;
        return SeverityRank(floor) > SeverityRank(deviceVerdict) ? floor : deviceVerdict;
    }

    private Verdict DecideFromDevices(IReadOnlyList<CompatibilityMatch> matches)
    {
        if (matches.Count == 0)
        {
            return Verdict.InsufficientData;
        }

        var unsupported = 0;
        var proprietaryOrPartial = 0;
        var firmwareRequired = 0;
        var unknown = 0;

        foreach (var match in matches)
        {
            switch (match.Support)
            {
                case SupportLevel.Unsupported:
                    unsupported++;
                    break;
                case SupportLevel.Proprietary:
                case SupportLevel.Partial:
                    proprietaryOrPartial++;
                    break;
                case SupportLevel.FirmwareRequired:
                    firmwareRequired++;
                    break;
                case SupportLevel.Unknown:
                    unknown++;
                    break;
                case SupportLevel.Native:
                default:
                    break;
            }
        }

        // A confirmed-unsupported device is the strongest, most actionable signal there is —
        // report it even if plenty of other devices are also unknown.
        if (unsupported > 0)
        {
            return Verdict.Blocked;
        }

        if (unknown / (double)matches.Count > _insufficientDataUnknownRatio)
        {
            return Verdict.InsufficientData;
        }

        if (proprietaryOrPartial > 0)
        {
            return Verdict.NeedsAttention;
        }

        if (firmwareRequired > 0)
        {
            return Verdict.MinorIssues;
        }

        return Verdict.Ready;
    }

    private static Verdict FloorFromConstraints(IReadOnlyList<SystemConstraint> constraints)
    {
        if (constraints.Any(c => c.Severity == SystemConstraintSeverity.Critical))
        {
            return Verdict.NeedsAttention;
        }

        return constraints.Count > 0 ? Verdict.MinorIssues : Verdict.Ready;
    }

    /// <summary>
    /// A blocked-with-no-real-substitute program only matters to the overall verdict when it is
    /// also flagged <see cref="SoftwareImportance.Critical"/> — a blocked minor utility is real
    /// information (still visible per-entry in the report) but should not by itself downgrade an
    /// otherwise-clean machine, the same way a single Unknown device does not.
    /// <see cref="SoftwareCompatibilityStatus.BuiltIn"/> never reaches this check at all — it is
    /// not a shortfall, so importance is irrelevant to it. <see cref="SoftwareCompatibilityStatus.Partial"/>
    /// is deliberately lighter than <see cref="SoftwareCompatibilityStatus.Blocked"/>: the core
    /// function still works, so it never floors the verdict by itself even when Critical — a
    /// Critical Partial finding is instead surfaced by staying visible in the report.
    /// </summary>
    private static Verdict FloorFromSoftware(IReadOnlyList<SoftwareAssessment> assessments) =>
        assessments.Any(a =>
            a.Match.Status == SoftwareCompatibilityStatus.Blocked &&
            a.Match.Entry?.Importance == SoftwareImportance.Critical)
            ? Verdict.NeedsAttention
            : Verdict.Ready;

    /// <summary>
    /// Priority order used only to combine a device verdict with a system-constraint floor (the
    /// higher-ranked of the two wins). Mirrors the early-return priority already implicit in
    /// <see cref="DecideFromDevices"/>: an unsupported device (<see cref="Verdict.Blocked"/>)
    /// outranks everything, and a majority-unknown result (<see cref="Verdict.InsufficientData"/>)
    /// outranks a specific-but-lesser device finding.
    /// </summary>
    private static int SeverityRank(Verdict verdict) => verdict switch
    {
        Verdict.Ready => 0,
        Verdict.MinorIssues => 1,
        Verdict.NeedsAttention => 2,
        Verdict.InsufficientData => 3,
        Verdict.Blocked => 4,
        _ => 0
    };

    /// <summary>
    /// Evaluates the machine-wide (non-device) fields of <paramref name="profile"/> against this
    /// engine's configured thresholds. Each check is independent — a machine can accumulate
    /// several constraints at once (e.g. an old CPU AND a full disk). Never guesses: a field the
    /// collector could not read (<c>null</c>) is skipped, not treated as either "fine" or "a
    /// problem" — consistent with the profile schema's own null-means-unknown convention.
    /// </summary>
    public IReadOnlyList<SystemConstraint> EvaluateSystemConstraints(MachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var constraints = new List<SystemConstraint>();

        if (profile.Cpu.X86_64FeatureLevel is { } featureLevel && featureLevel < _minimumRecommendedFeatureLevel)
        {
            constraints.Add(new SystemConstraint
            {
                Kind = SystemConstraintKind.LowX86FeatureLevel,
                Severity = SystemConstraintSeverity.Soft,
                ObservedFeatureLevel = featureLevel
            });
        }

        if (profile.Firmware.BootMode == BootMode.Legacy)
        {
            constraints.Add(new SystemConstraint
            {
                Kind = SystemConstraintKind.LegacyBootMode,
                Severity = SystemConstraintSeverity.Soft
            });
        }

        if (profile.Memory.TotalBytes is { } totalMemoryBytes && totalMemoryBytes < _minimumRecommendedMemoryBytes)
        {
            constraints.Add(new SystemConstraint
            {
                Kind = SystemConstraintKind.LowMemory,
                Severity = SystemConstraintSeverity.Soft,
                ObservedBytes = totalMemoryBytes,
                ThresholdBytes = _minimumRecommendedMemoryBytes
            });
        }

        if (SystemDiskSpace.FreeBytes(profile) is { } freeBytes && freeBytes < _minimumFreeSystemDiskBytes)
        {
            constraints.Add(new SystemConstraint
            {
                Kind = SystemConstraintKind.LowDiskSpace,
                Severity = SystemConstraintSeverity.Critical,
                ObservedBytes = freeBytes,
                ThresholdBytes = _minimumFreeSystemDiskBytes
            });
        }

        return constraints;
    }

}
