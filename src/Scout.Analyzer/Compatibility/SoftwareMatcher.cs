using Scout.Core.Models;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// Looks an installed program up in a <see cref="SoftwareCompatibilityDatabase"/>, preferring the
/// most reliable available signal — see <see cref="SoftwareMatchRule"/> and
/// <see cref="SoftwareMatchSignal"/>. Never guesses — a miss is reported as
/// <see cref="SoftwareMatchLevel.None"/> / <see cref="SoftwareCompatibilityStatus.Unknown"/>, not
/// inferred from a similar name.
/// </summary>
/// <remarks>
/// Callers should only invoke this for entries meaningful to a Linux migration — see
/// <see cref="MachineAnalyzer"/>, which skips <see cref="SoftwareCategory.runtime"/>/
/// <see cref="SoftwareCategory.driver"/>/<see cref="SoftwareCategory.system"/> entries entirely
/// before ever calling <see cref="Match"/>: a bundled VC++ Redistributable or a display driver
/// package has no meaning as a standalone Linux program.
/// </remarks>
public sealed class SoftwareMatcher
{
    private readonly SoftwareCompatibilityDatabase _database;

    public SoftwareMatcher(SoftwareCompatibilityDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <param name="software">The inventory entry to identify.</param>
    /// <param name="profileLanguage">
    /// The collecting machine's <c>os.language</c> (e.g. <c>"tr-TR"</c>), if known. Never gates or
    /// filters which name aliases are tried — every alias is always attempted regardless of this
    /// value, so a missing or wrong language never causes a real match to be skipped. Used only as
    /// a last-resort tie-break: when two aliases would otherwise match equally well, the one
    /// tagged for this language is preferred. See <see cref="SoftwareNameAlias.Language"/>.
    /// </param>
    public SoftwareMatch Match(SoftwareEntry software, string? profileLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(software);

        return MatchByRegistryKey(software) ?? MatchByNameAlias(software, profileLanguage) ?? None();
    }

    // ---- Signal 1 (most reliable): the Uninstall subkey's own name, never re-translated -------

    private SoftwareMatch? MatchByRegistryKey(SoftwareEntry software)
    {
        if (software.RegistryKeyName is null)
        {
            return null;
        }

        var candidates = _database.Entries
            .Where(entry => entry.Match.RegistryKey is not null &&
                             IsPatternMatch(entry.Match.RegistryKey.Pattern, entry.Match.RegistryKey.MatchType, software.RegistryKeyName))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .OrderByDescending(entry => entry.Match.RegistryKey!.MatchType == SoftwareMatchType.Exact)
            .ThenByDescending(entry => entry.Match.RegistryKey!.Pattern.Length)
            .First();

        return new SoftwareMatch
        {
            Level = ToLevel(best.Match.RegistryKey!.MatchType),
            Entry = best,
            Status = best.Status,
            Signals = [SoftwareMatchSignal.RegistryKey]
        };
    }

    // ---- Signal 2: DisplayName, against one or more known (often localized) spellings ---------

    private readonly record struct AliasCandidate(SoftwareCompatibilityEntry Entry, SoftwareNameAlias Alias, bool IsPrimary);

    private SoftwareMatch? MatchByNameAlias(SoftwareEntry software, string? profileLanguage)
    {
        // Publisher never identifies a program by itself, so it is applied as a per-entry gate
        // here rather than being its own candidate pool — an entry whose publisher requirement
        // fails contributes no alias candidates at all, regardless of how well its name matches.
        var candidates = new List<AliasCandidate>();
        foreach (var entry in _database.Entries)
        {
            if (entry.Match.PublisherPattern is not null && !IsPublisherMatch(entry.Match.PublisherPattern, software.Publisher))
            {
                continue;
            }

            for (var i = 0; i < entry.Match.NameAliases.Count; i++)
            {
                var alias = entry.Match.NameAliases[i];
                if (IsPatternMatch(alias.Pattern, alias.MatchType, software.Name))
                {
                    candidates.Add(new AliasCandidate(entry, alias, IsPrimary: i == 0));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // Real installer-generated names collide more than hardware IDs do (e.g. "Visual Studio
        // Code" vs. plain "Visual Studio" both legitimately using Contains) — an Exact rule always
        // wins, then the longer/more specific pattern, then (only as a final tie-break, never a
        // filter) a pattern tagged for the profile's own language — regardless of the JSON file's
        // row order.
        var best = candidates
            .OrderByDescending(c => c.Alias.MatchType == SoftwareMatchType.Exact)
            .ThenByDescending(c => c.Alias.Pattern.Length)
            .ThenByDescending(c => LanguageMatches(profileLanguage, c.Alias.Language))
            .First();

        var signals = new List<SoftwareMatchSignal> { best.IsPrimary ? SoftwareMatchSignal.Name : SoftwareMatchSignal.Alias };
        if (best.Entry.Match.PublisherPattern is not null)
        {
            signals.Add(SoftwareMatchSignal.Publisher);
        }

        return new SoftwareMatch
        {
            Level = ToLevel(best.Alias.MatchType),
            Entry = best.Entry,
            Status = best.Entry.Status,
            Signals = signals
        };
    }

    private static SoftwareMatch None() => new()
    {
        Level = SoftwareMatchLevel.None,
        Entry = null,
        Status = SoftwareCompatibilityStatus.Unknown,
        Signals = []
    };

    private static bool IsPatternMatch(string pattern, SoftwareMatchType matchType, string value) => matchType switch
    {
        SoftwareMatchType.Exact => string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase),
        SoftwareMatchType.Prefix => value.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
        SoftwareMatchType.Contains => value.Contains(pattern, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    /// <summary>A null <see cref="SoftwareEntry.Publisher"/> can never satisfy a publisher requirement — treated as "cannot confirm", not "assume it matches".</summary>
    private static bool IsPublisherMatch(string publisherPattern, string? publisher) =>
        publisher is not null && publisher.Contains(publisherPattern, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Purely a tie-break preference (see <see cref="Match"/>'s <c>profileLanguage</c> doc) — never
    /// a filter. Tolerates a full BCP-47 profile tag (e.g. "tr-TR") against a short alias tag
    /// ("tr").
    /// </summary>
    private static bool LanguageMatches(string? profileLanguage, string? aliasLanguage)
    {
        if (profileLanguage is null || aliasLanguage is null)
        {
            return false;
        }

        return profileLanguage.Equals(aliasLanguage, StringComparison.OrdinalIgnoreCase) ||
               profileLanguage.StartsWith(aliasLanguage + "-", StringComparison.OrdinalIgnoreCase);
    }

    private static SoftwareMatchLevel ToLevel(SoftwareMatchType matchType) => matchType switch
    {
        SoftwareMatchType.Exact => SoftwareMatchLevel.Exact,
        SoftwareMatchType.Prefix => SoftwareMatchLevel.Prefix,
        SoftwareMatchType.Contains => SoftwareMatchLevel.Contains,
        _ => SoftwareMatchLevel.None
    };
}
