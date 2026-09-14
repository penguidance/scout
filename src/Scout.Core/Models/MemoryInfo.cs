using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>Physical memory facts, from Win32_PhysicalMemory / Win32_PhysicalMemoryArray.</summary>
public sealed class MemoryInfo
{
    /// <summary>Sum of installed module capacities. Null when this could not be determined.</summary>
    [JsonPropertyName("total_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required long? TotalBytes { get; init; }

    [JsonPropertyName("modules")]
    public required IReadOnlyList<MemoryModule> Modules { get; init; }
}

/// <summary>One populated physical memory module (DIMM/SODIMM), from Win32_PhysicalMemory.</summary>
public sealed class MemoryModule
{
    /// <summary>Null when this specific module's capacity could not be read.</summary>
    [JsonPropertyName("capacity_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required long? CapacityBytes { get; init; }

    /// <summary>The module's rated/JEDEC speed, from Win32_PhysicalMemory.Speed.</summary>
    [JsonPropertyName("rated_speed_mhz")]
    public int? RatedSpeedMhz { get; init; }

    /// <summary>The speed it is actually running at, from Win32_PhysicalMemory.ConfiguredClockSpeed (can be lower than rated, e.g. XMP/EXPO not enabled).</summary>
    [JsonPropertyName("configured_speed_mhz")]
    public int? ConfiguredSpeedMhz { get; init; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; init; }

    [JsonPropertyName("part_number")]
    public string? PartNumber { get; init; }

    /// <summary>
    /// A decoded label (e.g. "DDR4") for the SMBIOSMemoryType code, only when the code is one we
    /// recognize; null otherwise — the raw code is never silently presented as if it were a
    /// resolved label. See <see cref="MemoryTypeRaw"/>, which is kept either way.
    /// </summary>
    [JsonPropertyName("memory_type")]
    public string? MemoryType { get; init; }

    /// <summary>The raw SMBIOSMemoryType numeric code, as a string. Kept even when <see cref="MemoryType"/> is null.</summary>
    [JsonPropertyName("memory_type_raw")]
    public string? MemoryTypeRaw { get; init; }
}
