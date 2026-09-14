using Scout.Core.Models;

namespace Scout.Analyzer.Analysis;

/// <summary>
/// Which system-wide (not per-device) property a <see cref="SystemConstraint"/> is about. These
/// come from <see cref="MachineProfile"/> fields outside <c>devices[]</c> — CPU, firmware,
/// memory, storage — and are evaluated independently of device compatibility (see
/// <see cref="VerdictEngine.EvaluateSystemConstraints"/>) so the two never get mixed into one list.
/// </summary>
public enum SystemConstraintKind
{
    /// <summary>
    /// The CPU's x86-64 microarchitecture level is below the engine's configured baseline — some
    /// current distributions require a higher level (glibc/kernel build targets v3+).
    /// </summary>
    LowX86FeatureLevel,

    /// <summary>Firmware boots Legacy (BIOS), not UEFI — distro/installer media choice is constrained.</summary>
    LegacyBootMode,

    /// <summary>Total physical memory is below the configured threshold — full desktop environments become impractical.</summary>
    LowMemory,

    /// <summary>Free space on the system (boot) disk is below the configured threshold — there may not be room to install at all.</summary>
    LowDiskSpace
}

/// <summary>
/// How strongly a <see cref="SystemConstraint"/> should pull the overall <see cref="Verdict"/>.
/// See <see cref="VerdictEngine"/> for the floor each severity imposes on the final verdict.
/// </summary>
public enum SystemConstraintSeverity
{
    /// <summary>The machine still works, but distro/desktop choice or install steps are constrained.</summary>
    Soft,

    /// <summary>Seriously complicates or blocks installation as-is (e.g. no room left on disk).</summary>
    Critical
}

/// <summary>
/// One system-wide (as opposed to per-device) finding from
/// <see cref="VerdictEngine.EvaluateSystemConstraints"/>. Kept as structured data — not a
/// pre-rendered string — so all presentation, including the user-facing Turkish text, stays in
/// Scout.Reporter, the same way per-device findings work via <see cref="Compatibility.CompatibilityEntry.Notes"/>.
/// </summary>
public sealed class SystemConstraint
{
    public required SystemConstraintKind Kind { get; init; }
    public required SystemConstraintSeverity Severity { get; init; }

    /// <summary>Set only for <see cref="SystemConstraintKind.LowX86FeatureLevel"/>: the CPU's actual observed level.</summary>
    public X86FeatureLevel? ObservedFeatureLevel { get; init; }

    /// <summary>
    /// Set only for <see cref="SystemConstraintKind.LowMemory"/> and
    /// <see cref="SystemConstraintKind.LowDiskSpace"/>: the observed value in bytes, so the
    /// report can state the actual figure alongside the threshold.
    /// </summary>
    public long? ObservedBytes { get; init; }

    /// <summary>The configured threshold the observed value fell short of, in bytes. Same population rule as <see cref="ObservedBytes"/>.</summary>
    public long? ThresholdBytes { get; init; }
}
