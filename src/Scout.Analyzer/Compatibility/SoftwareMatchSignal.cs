namespace Scout.Analyzer.Compatibility;

/// <summary>
/// Which evidence contributed to a <see cref="SoftwareMatch"/> — reported (as a list — a match can
/// have more than one) in the profile/report specifically to make a wrong match diagnosable: if a
/// program is misclassified, knowing it matched via, say, a loose <see cref="Name"/> pattern
/// rather than <see cref="RegistryKey"/> immediately narrows down why.
/// </summary>
public enum SoftwareMatchSignal
{
    /// <summary>Matched via <see cref="SoftwareMatchRule.RegistryKey"/> — the most reliable signal, never re-translated.</summary>
    RegistryKey,

    /// <summary>Matched via the first (original/default) entry in <see cref="SoftwareMatchRule.NameAliases"/>.</summary>
    Name,

    /// <summary>Matched via a later entry in <see cref="SoftwareMatchRule.NameAliases"/> — typically a localized spelling confirmed on a real machine.</summary>
    Alias,

    /// <summary>
    /// <see cref="SoftwareMatchRule.PublisherPattern"/> was checked and required to confirm this
    /// match, alongside a <see cref="Name"/>/<see cref="Alias"/> signal — publisher alone never
    /// identifies a specific program in this database.
    /// </summary>
    Publisher
}
