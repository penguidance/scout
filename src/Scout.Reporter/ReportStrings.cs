using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Analyzer.Recommendations;
using Scout.Core.Models;

namespace Scout.Reporter;

/// <summary>
/// Every piece of user-facing report text lives here — nothing is inlined into
/// <see cref="ReportGenerator"/>'s HTML-building code — so adding a second language later means
/// adding a sibling of this class (or turning it into a culture-keyed lookup), not touching how
/// the report is assembled. Turkish only for now.
/// </summary>
/// <remarks>
/// Per-device "what to do" text is deliberately NOT here: it comes from
/// <see cref="CompatibilityEntry.Notes"/> in data/hardware-compatibility.json, so that text can
/// be fixed/improved by editing the database, not by changing code.
/// </remarks>
public static class ReportStrings
{
    public static string DocumentTitle(string machineLabel) => $"Linux Uyumluluk Raporu — {machineLabel}";

    // ---- Distribution recommendation — right under the verdict; one pick, one reason, one link -

    public const string DistributionSectionTitle = "Önerilen Dağıtım";

    public static string DistributionDownloadLabel(string distributionName) => $"{distributionName} indir";

    /// <summary>The report's one-sentence reasoning — composed here from the recommender's <see cref="DistributionRecommendationReason"/> enum, never read verbatim from data/distributions.json (see docs/schema/distributions-v0.1.md).</summary>
    public static string DistributionReason(DistributionRecommendationReason reason, string distributionName) => reason switch
    {
        DistributionRecommendationReason.WindowsFamiliarity =>
            $"{distributionName}, Windows'a benzer arayüzü sayesinde geçiş yapanlar için iyi bir ilk tercih.",
        DistributionRecommendationReason.LowMemory =>
            $"{distributionName}, bu bilgisayarın sınırlı belleğinde diğer seçeneklerden daha hafif ve akıcı çalışır.",
        DistributionRecommendationReason.NvidiaProprietaryDriver =>
            $"{distributionName}, ekran kartının kapalı kaynak sürücüsünü kurulum sırasında kolayca sunduğu için önerildi.",
        DistributionRecommendationReason.LimitedHardware =>
            $"Bu makinenin donanımı çoğu dağıtımın önerdiği minimumun altında; {distributionName} yine de denenebilecek en hafif seçenek.",
        _ => distributionName
    };

    public static string DistributionAlternative(string distributionName, string desktopEnvironment) =>
        $"Alternatif: {distributionName} ({desktopEnvironment}).";

    // ---- "Günlük hayatında ne değişir" — plain language, only the items that actually apply ----

    public const string DailyLifeSectionTitle = "Günlük Hayatında Ne Değişir?";

    public static string DailyLifePrinter(SupportLevel printerSupport) => printerSupport switch
    {
        SupportLevel.Native => "Yazıcın ek bir ayara gerek kalmadan çalışacak.",
        SupportLevel.FirmwareRequired => "Yazıcın çalışacak, ama kurulum sırasında ek bir dosya inmesi gerekebilir.",
        _ => "Yazıcının Linux'ta çalışıp çalışmayacağından emin değiliz; üreticinin Linux desteğini kontrol etmen iyi olur."
    };

    public const string DailyLifeGamingNative =
        "Steam kurulu; oyunların çoğu Proton uyumluluk katmanıyla çalışabilir, ama her oyun için garanti değil.";

    public const string DailyLifeGamingWine =
        "Kullandığın oyun launcher'ının resmi bir Linux sürümü yok; Wine/Lutris ile denenebilir ama bazı oyunlar çalışmayabilir.";

    public const string DailyLifeOffice =
        "Office yerine LibreOffice kullanman gerekecek; çoğu belge sorunsuz açılır ama karmaşık şablon/makrolar sorun çıkarabilir.";

    public const string DailyLifeWifiFirmware =
        "Kablosuz ağ kartın için kurulum sırasında ek bir dosya gerekebilir; o ana kadar kablolu bağlantı işini kolaylaştırır.";

    public const string DailyLifeHybridGpu =
        "Bu bilgisayarda hem entegre hem ayrık ekran kartı var; pil ömrü ile performans arasındaki geçiş Windows'takinden farklı çalışabilir.";

    // ---- "Sonraki adım" — only when there is a next step worth taking (verdict != Blocked) -----

    public const string NextStepsSectionTitle = "Sonraki Adım";

    public const string NextStepsIntro =
        "Hiçbir şeyi silmeden, mevcut Windows kurulumuna dokunmadan denemek istersen:";

    public static readonly IReadOnlyList<string> NextStepsList =
    [
        "Yukarıdaki linkten indirdiğin ISO dosyasını Rufus ya da balenaEtcher gibi bir araçla bir USB belleğe yaz (en az 8 GB'lık bir USB bellek yeterli).",
        "Bilgisayarı bu USB'den başlat — genelde açılışta F12, F2, Esc ya da Del tuşlarından biriyle açılan boot menüsünden USB'yi seçmen gerekir.",
        "Açılan menüden 'Dene' (Try) seçeneğini seç — hiçbir şey diskine kurulmaz ya da silinmez, her şey USB üzerinden çalışır.",
        "Beğenirsen masaüstündeki kurulum simgesinden gerçek kuruluma geçebilirsin; beğenmezsen bilgisayarı kapatıp USB'yi çıkarman yeterli, Windows kurulumun olduğu gibi kalır."
    ];

    public const string NextStepsLowDiskSpaceWarning =
        "Sistem diskinde kuruluma yetecek boş alan görünmüyor; USB'den denemek bunu etkilemez, ama gerçek kuruluma geçmeden önce yer açman gerekecek.";

    public const string NextStepsBitLockerWarning =
        "BitLocker şifrelemesi açık görünüyor; USB'den denemek bunu etkilemez, ama gerçek kuruluma geçmeden önce BitLocker kurtarma anahtarını bir yere not etmen iyi olur.";

    public static string VerdictHeadline(Verdict verdict) => verdict switch
    {
        Verdict.Ready => "Bu bilgisayar Linux'a taşınabilir.",
        Verdict.MinorIssues => "Taşınabilir, birkaç ayar gerekiyor.",
        Verdict.NeedsAttention => "Dikkat gerektiren donanım var.",
        Verdict.Blocked => "Bu bilgisayar şu an taşınmaya uygun değil.",
        Verdict.InsufficientData => "Yeterli veri toplanamadı.",
        _ => "Değerlendirme tamamlanamadı."
    };

    // Unknown-support devices never change the verdict on their own (see VerdictEngine) — so
    // when they do not affect the outcome, the reason sentence does not mention them at all.
    // Raising them here ("ama bu genel tabloyu değiştirmiyor" etc.) reads as a hedge and invites
    // doubt about a clean result; the honest, complete picture (including any Unknown devices)
    // still lives in the folded technical breakdown, where a reader who wants it can see it.
    public static string VerdictReason(Verdict verdict, SupportCounts counts) => verdict switch
    {
        Verdict.Ready =>
            $"İncelenen {counts.Native} donanım Linux çekirdeğinde hazır olarak bulunuyor, herhangi bir sorun tespit edilmedi.",
        Verdict.MinorIssues =>
            $"{counts.FirmwareRequired} donanım için ek bir dosya (firmware) kurulumu gerekiyor, bunun dışında bir sorun yok.",
        Verdict.NeedsAttention =>
            $"{counts.ProprietaryOrPartial} donanım için kapalı kaynak bir sürücü gerekiyor ya da destek eksik kalıyor.",
        Verdict.Blocked =>
            $"{counts.Unsupported} donanım Linux'ta çalışmıyor.",
        Verdict.InsufficientData when counts.Total == 0 =>
            "Değerlendirilecek anlamlı bir donanım bulunamadı.",
        Verdict.InsufficientData =>
            $"{counts.Unknown} donanım hakkında veritabanında hiç bilgi yok; sağlıklı bir değerlendirme yapılamıyor.",
        _ => ""
    };

    /// <summary>Short, jargon-free label for what a support level means — shown next to each attention-needing device.</summary>
    public static string SupportLevelLabel(SupportLevel level) => level switch
    {
        SupportLevel.Native => "Sorunsuz çalışır",
        SupportLevel.FirmwareRequired => "Ek dosya kurulumu gerekir",
        SupportLevel.Proprietary => "Kapalı kaynak sürücü gerekir",
        SupportLevel.Partial => "Kısıtlı/eksik çalışır",
        SupportLevel.Unsupported => "Çalışmaz",
        SupportLevel.Unknown => "Bilinmiyor",
        _ => "Bilinmiyor"
    };

    public static string MatchLevelLabel(MatchLevel level) => level switch
    {
        MatchLevel.Exact => "Tam eşleşme",
        MatchLevel.RangeMatch => "Donanım nesli bazlı kural",
        MatchLevel.VendorFallback => "Üretici bazlı genel kural",
        MatchLevel.None => "Eşleşme yok",
        _ => "-"
    };

    public const string NoNotesAvailable =
        "Bu donanım hakkında veritabanımızda bilgi yok. Vendor/Device ID'yi not alıp modelini ayrıca araştırman gerekebilir.";

    public const string AttentionSectionTitle = "Dikkat Gerektirenler";
    public const string AttentionColumnDevice = "Cihaz";
    public const string AttentionColumnWhatHappens = "Ne olacak?";
    public const string AttentionColumnWhatToDo = "Ne yapmalı?";

    // A separate section from AttentionSectionTitle above on purpose: those rows are about
    // specific hardware not working; these are about the machine as a whole (CPU baseline, boot
    // mode, memory, disk space) — mixing the two would make it unclear whether a row is about one
    // device or the whole move to Linux. See SystemConstraint/VerdictEngine.EvaluateSystemConstraints.
    public const string SystemConstraintsSectionTitle = "Sistem Genelinde Kısıtlamalar";
    public const string SystemConstraintsColumnTopic = "Konu";
    public const string SystemConstraintsColumnSeverity = "Etki";
    public const string SystemConstraintsColumnDetail = "Açıklama";

    public static string SystemConstraintSeverityLabel(SystemConstraintSeverity severity) => severity switch
    {
        SystemConstraintSeverity.Critical => "Kurulumu engelleyebilir",
        SystemConstraintSeverity.Soft => "Kısıtlı/ek adım gerekir",
        _ => Unknown
    };

    public static string SystemConstraintTopic(SystemConstraintKind kind) => kind switch
    {
        SystemConstraintKind.LowX86FeatureLevel => "İşlemci seviyesi",
        SystemConstraintKind.LegacyBootMode => "Önyükleme modu",
        SystemConstraintKind.LowMemory => "Bellek",
        SystemConstraintKind.LowDiskSpace => "Disk alanı",
        _ => Unknown
    };

    /// <summary>
    /// The per-constraint explanation text — composed here (unlike a device's "what to do", which
    /// comes verbatim from the compatibility database's <c>notes</c> field) because these are
    /// code-computed system facts with no equivalent database row to hold the text.
    /// </summary>
    public static string SystemConstraintDetail(SystemConstraint constraint) => constraint.Kind switch
    {
        SystemConstraintKind.LowX86FeatureLevel =>
            $"İşlemciniz x86-64-{constraint.ObservedFeatureLevel} mikromimari seviyesinde. Bazı güncel Linux " +
            "dağıtımları artık v3 tabanını şart koşuyor; bu makinede çalışmayabilirler, v2 uyumlu daha eski/hafif " +
            "bir dağıtım seçmeniz gerekebilir.",

        SystemConstraintKind.LegacyBootMode =>
            "Bu makine BIOS (Legacy) modunda önyükleniyor, UEFI değil. Çoğu güncel dağıtımın kurulum ortamı yine " +
            "de açılır, ama disk bölümleme ve önyükleyici kurulumu UEFI'dekinden farklı işler; bazı dağıtımlarda " +
            "kurulum sırasında ek bir adım gerekebilir.",

        SystemConstraintKind.LowMemory =>
            $"Toplam bellek {MemorySize(constraint.ObservedBytes)}, önerilen en az " +
            $"{MemorySize(constraint.ThresholdBytes)}'ın altında. GNOME/KDE gibi ağır masaüstü ortamları yerine " +
            "XFCE/LXQt gibi hafif bir masaüstü seçmeniz önerilir.",

        SystemConstraintKind.LowDiskSpace =>
            $"Sistem diskinde {MemorySize(constraint.ObservedBytes)} boş alan var, kurulum için önerilen en az " +
            $"{MemorySize(constraint.ThresholdBytes)}. Linux'u kurmadan önce yer açmanız gerekir.",

        _ => Unknown
    };

    // ---- Software: inventory + Linux-equivalent mapping ------------------------------------
    //
    // Deliberately not one big table — a reader does not want to see 54 installed programs.
    // Three tiers instead: real problems get a full table, "there's a substitute" gets a short
    // table, everything else gets one summary sentence. The complete list (including runtime/
    // driver/system entries, which are never looked up at all) still lives in the folded
    // technical dump for anyone who wants it.

    public const string SoftwareProblemsSectionTitle = "Sorunlu Programlar";
    public const string SoftwareProblemsColumnProgram = "Program";
    public const string SoftwareProblemsColumnWhatHappens = "Ne olacak?";
    public const string SoftwareProblemsColumnWhatToDo = "Ne yapmalı?";
    public const string SoftwareProblemsColumnAlternative = "Muadil Önerisi";

    public const string SoftwareEquivalentsSectionTitle = "Muadili Olanlar";
    public const string SoftwareEquivalentsColumnProgram = "Program";
    public const string SoftwareEquivalentsColumnAlternative = "Muadil";

    public const string SoftwareWebAlternativeLabel = "Tarayıcı üzerinden kullanılabilir";
    public const string SoftwareNoKnownAlternative = "Bilinen bir muadili yok.";
    public const string SoftwareWineNoAlternativeNeeded = "Ek bir muadile gerek yok, Wine/Proton ile çalışıyor.";

    public static string SoftwareStatusLabel(SoftwareCompatibilityStatus status) => status switch
    {
        SoftwareCompatibilityStatus.Native => "Sorunsuz çalışır",
        SoftwareCompatibilityStatus.Equivalent => "Muadili var",
        SoftwareCompatibilityStatus.Wine => "Wine/Proton ile çalışır",
        SoftwareCompatibilityStatus.Web => "Tarayıcı üzerinden kullanılabilir",
        SoftwareCompatibilityStatus.Blocked => "Çalışmaz, muadili yok",
        SoftwareCompatibilityStatus.Unknown => Unknown,
        _ => Unknown
    };

    public static string SoftwareCategoryLabel(SoftwareCategory category) => category switch
    {
        SoftwareCategory.application => "Uygulama",
        SoftwareCategory.runtime => "Çalışma zamanı bileşeni",
        SoftwareCategory.driver => "Sürücü paketi",
        SoftwareCategory.system => "Sistem bileşeni",
        _ => Unknown
    };

    public static string SoftwareMatchLevelLabel(SoftwareMatchLevel level) => level switch
    {
        SoftwareMatchLevel.Exact => "Tam eşleşme",
        SoftwareMatchLevel.Prefix => "Önek eşleşmesi",
        SoftwareMatchLevel.Contains => "İçerik eşleşmesi",
        SoftwareMatchLevel.None => "Eşleşme yok",
        _ => "-"
    };

    /// <summary>Joined alternative names for the "Sorunlu Programlar" table's dedicated column, with a status-aware fallback when the database lists none.</summary>
    public static string SoftwareAlternativesSummary(SoftwareCompatibilityEntry? entry)
    {
        if (entry is { Alternatives.Count: > 0 })
        {
            return string.Join(", ", entry.Alternatives.Select(a => a.Name));
        }

        return entry?.Status == SoftwareCompatibilityStatus.Wine ? SoftwareWineNoAlternativeNeeded : SoftwareNoKnownAlternative;
    }

    /// <summary>The "Muadili Olanlar" table's per-row alternative cell: named alternatives for Equivalent, the browser note for Web.</summary>
    public static string SoftwareEquivalentCell(SoftwareCompatibilityEntry entry) =>
        entry.Status == SoftwareCompatibilityStatus.Web
            ? SoftwareWebAlternativeLabel
            : string.Join(", ", entry.Alternatives.Select(a => a.Name));

    /// <summary>
    /// The one-line summary for programs that need no attention at all — e.g. "Chrome, Spotify,
    /// VLC ve 31 program Linux'ta doğrudan çalışıyor." Turkish-joins the example names with "ve"
    /// before the last one; omits the "ve N program daha" clause entirely when there is nothing
    /// left to count.
    /// </summary>
    public static string NativeSoftwareSummary(IReadOnlyList<string> exampleNames, int remainingCount)
    {
        var namesPart = JoinTurkish(exampleNames);
        var suffix = remainingCount > 0 ? $" ve {remainingCount} program" : "";
        return $"{namesPart}{suffix} Linux'ta doğrudan çalışıyor.";
    }

    private static string JoinTurkish(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " ve " + items[^1]
    };

    public const string SoftwareTechnicalDetailsSummary = "Tam Yazılım Envanteri";
    public const string SoftwareTechnicalColumnProgram = "Program";
    public const string SoftwareTechnicalColumnPublisher = "Yayıncı";
    public const string SoftwareTechnicalColumnCategory = "Kategori";
    public const string SoftwareTechnicalColumnMatch = "Eşleşme";
    public const string SoftwareTechnicalColumnSignal = "Sinyal";
    public const string SoftwareTechnicalColumnStatus = "Durum";
    public const string SoftwareNotAssessed = "Değerlendirilmedi";

    /// <summary>Which evidence identified the program — shown in the technical dump specifically to make a wrong match diagnosable (see <see cref="SoftwareMatchSignal"/>).</summary>
    public static string SoftwareMatchSignalLabel(SoftwareMatchSignal signal) => signal switch
    {
        SoftwareMatchSignal.RegistryKey => "Registry anahtarı",
        SoftwareMatchSignal.Name => "Ad",
        SoftwareMatchSignal.Alias => "Takma ad",
        SoftwareMatchSignal.Publisher => "Yayıncı",
        _ => Unknown
    };

    public static string SoftwareMatchSignalsSummary(IReadOnlyList<SoftwareMatchSignal> signals) =>
        signals.Count == 0 ? "—" : string.Join(" + ", signals.Select(SoftwareMatchSignalLabel));

    public static string SoftwareNoiseRemovedNote(int filteredAtCollection, int skippedForCategory) =>
        $"Ayrıca {filteredAtCollection} kayıt toplama sırasında gürültü olarak elendi " +
        "(boş/anlamsız ad, Windows güncellemesi, ya da bir ana ürünün alt bileşeni), " +
        $"ve {skippedForCategory} kayıt (çalışma zamanı/sürücü/sistem bileşeni) Linux karşılığı açısından anlamsız olduğu için hiç değerlendirilmedi.";

    /// <summary>
    /// Separator row shown once, right before the first Unknown-status program in the folded
    /// inventory — explains that "no database entry" is a gap in what was checked, not a claim
    /// that the program does not work, before dumping a list of names that would otherwise read
    /// as low-value noise sitting alphabetically among programs with a real answer.
    /// </summary>
    public const string SoftwareUnknownGroupIntro =
        "Aşağıdaki programlar veritabanımızda kayıtlı değil — bu, Linux'ta çalışmayacakları anlamına gelmez, sadece haklarında bilgimiz olmadığı anlamına gelir; tahmin yürütülmedi.";

    public const string TechnicalDetailsSummary = "Tam Teknik Döküm";
    public const string TechnicalColumnDevice = "Cihaz";
    public const string TechnicalColumnVendorDevice = "Vendor:Device";
    public const string TechnicalColumnClass = "Sınıf";
    public const string TechnicalColumnDriver = "Linux Sürücüsü";
    public const string TechnicalColumnMatch = "Eşleşme";
    public const string TechnicalColumnSupport = "Destek";

    public static string NoiseRemovedNote(int removedForRelevance, int removedForDuplication) =>
        $"Ayrıca {removedForRelevance + removedForDuplication} cihaz bu değerlendirme dışında bırakıldı " +
        $"({removedForRelevance} Linux uyumluluğu açısından anlamsız, {removedForDuplication} aynı donanımın tekrar eden kaydı).";

    public const string SystemSummaryTitle = "Sistem Özeti";
    public const string LabelCpu = "İşlemci";
    public const string LabelArchitecture = "Mimari";
    public const string LabelMemory = "Bellek";
    public const string LabelDisks = "Disk";
    public const string LabelBootMode = "Önyükleme Modu";
    public const string LabelTpm = "TPM";
    public const string LabelSecureBoot = "Secure Boot";

    /// <summary>
    /// Shown only when TPM and/or BitLocker actually came back unreadable (see
    /// <see cref="ReportGenerator"/>) — explains why, rather than leaving the reader to guess
    /// whether "Bilinmiyor" means the collector is broken.
    /// </summary>
    public const string ElevationNote =
        "TPM ve BitLocker gibi bazı alanlar, bu profil yönetici (Administrator) izniyle çalıştırılmadan toplandığı için 'Bilinmiyor' görünüyor — bu, donanımda böyle bir özellik olmadığı anlamına gelmez. Scout.Collector'ı yönetici olarak çalıştırırsan bu bilgiler okunabilir hale gelir.";

    public const string Unknown = "Bilinmiyor";
    public const string Yes = "Var";
    public const string No = "Yok";

    public static string CoresAndThreads(int? cores, int? threads) =>
        cores is null && threads is null
            ? Unknown
            : $"{FormatCountOrUnknown(cores)} çekirdek / {FormatCountOrUnknown(threads)} iş parçacığı";

    public static string ArchitectureLabel(CpuArchitecture? architecture) => architecture switch
    {
        CpuArchitecture.x86_64 => "x86_64 (64-bit)",
        CpuArchitecture.x86 => "x86 (32-bit)",
        CpuArchitecture.arm64 => "ARM64",
        _ => Unknown
    };

    public static string ArchitectureAndFeatureLevel(CpuArchitecture? architecture, X86FeatureLevel? featureLevel) =>
        featureLevel is null
            ? ArchitectureLabel(architecture)
            : $"{ArchitectureLabel(architecture)} — mikromimari seviyesi {featureLevel}";

    public static string BootModeLabel(BootMode? mode) => mode switch
    {
        BootMode.UEFI => "UEFI",
        BootMode.Legacy => "Legacy (BIOS)",
        _ => Unknown
    };

    public static string MediaTypeLabel(StorageMediaType? mediaType) => mediaType switch
    {
        StorageMediaType.NVMe => "NVMe SSD",
        StorageMediaType.SSD => "SSD",
        StorageMediaType.HDD => "HDD",
        _ => Unknown
    };

    public static string MemorySize(long? totalBytes) =>
        totalBytes is null ? Unknown : $"{totalBytes.Value / 1024d / 1024 / 1024:0.#} GB";

    public static string DiskSummary(string model, long? sizeBytes, StorageMediaType? mediaType) =>
        $"{model} — {(sizeBytes is null ? Unknown : $"{sizeBytes.Value / 1024d / 1024 / 1024:0} GB")} ({MediaTypeLabel(mediaType)})";

    public static string TpmSummary(bool? present, string? specVersion) => present switch
    {
        true when specVersion is not null => $"{Yes} (TPM {specVersion})",
        true => Yes,
        false => No,
        null => Unknown
    };

    public static string GeneratedFooter(DateTimeOffset generatedAt) =>
        $"Scout Analyzer tarafından {generatedAt:yyyy-MM-dd HH:mm} UTC'de üretildi.";

    private static string FormatCountOrUnknown(int? value) => value?.ToString() ?? "?";
}
