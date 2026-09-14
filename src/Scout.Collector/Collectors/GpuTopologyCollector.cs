using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Builds <see cref="GpuTopology"/> purely from the "Display"-class entries already present in
/// the already-collected <see cref="DeviceInfo"/> list — no CIM query of its own, hence no
/// <see cref="Scout.Collector.Cim.ICimQueryExecutor"/> dependency. Layout classification is a heuristic (see
/// <see cref="GpuClassifier"/>) and deliberately returns null rather than guessing when it is
/// not confident.
/// </summary>
public sealed class GpuTopologyCollector : ISectionCollector<GpuTopology>
{
    private const string DisplayClass = "Display";

    private readonly IReadOnlyList<DeviceInfo> _devices;

    public GpuTopologyCollector(IReadOnlyList<DeviceInfo> devices)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
    }

    public string ComponentName => "gpu_topology";

    public GpuTopology Collect(ICollection<CollectionError> errors)
    {
        var gpus = _devices
            .Where(d => string.Equals(d.Class, DisplayClass, StringComparison.OrdinalIgnoreCase))
            .Select(d => new GpuInfo
            {
                HardwareId = d.HardwareId,
                VendorId = d.VendorId,
                DeviceId = d.DeviceId,
                FriendlyName = d.FriendlyName,
                DriverVersion = d.DriverVersion
            })
            .ToList();

        return new GpuTopology { Gpus = gpus, Layout = DetermineLayout(gpus) };
    }

    private static GpuLayout? DetermineLayout(IReadOnlyList<GpuInfo> gpus)
    {
        if (gpus.Count == 0) return null;
        if (gpus.Count == 1) return GpuLayout.Single;

        var integratedCount = gpus.Count(GpuClassifier.IsLikelyIntegrated);
        var discreteCount = gpus.Count(GpuClassifier.IsLikelyDiscrete);

        if (integratedCount >= 1 && discreteCount >= 1) return GpuLayout.Hybrid;
        if (discreteCount >= 2 && integratedCount == 0) return GpuLayout.MultiDiscrete;

        // 2+ GPUs but the heuristic could not confidently classify enough of them — do not guess.
        return null;
    }
}
