# Distribution Recommendation Database Schema — v0.1

Bu doküman, `data/distributions.json`'daki her girdinin (`DistributionEntry`) biçimini ve `DistributionRecommender`'ın (Scout.Analyzer) bunlardan tek bir öneri nasıl seçtiğini tanımlar. Hardware/software uyumluluk veritabanlarıyla aynı ailededir ama farklı bir soruya cevap verir: onlar "bu donanım/program Linux'ta çalışır mı" derken, bu doküman "bu makineye hangi Linux dağıtımını önermeliyim" sorusunu cevaplar — tek bir dağıtım, tek bir gerekçe, bir de alternatif.

## Neden tek öneri, bir liste değil

Rapor okuyan kişi zaten "taşınabilir" cümlesini gördü ve "peki şimdi ne yapmalıyım" sorusunu soruyor. Beş dağıtımın artı/eksilerini karşılaştıran bir tablo bu soruyu cevaplamaz, yeni bir araştırma ödevi verir. `DistributionRecommender` bu yüzden **her zaman tam olarak bir birincil öneri ve bir alternatif** döndürür — hiçbir zaman sıralı bir liste değil. Kullanıcı isterse alternatifi de görür, ama karar zaten verilmiştir.

## Alanlar

| Alan | Tip | Açıklama |
|---|---|---|
| `name` | string, zorunlu | Dağıtımın adı, örn. `"Linux Mint"`. |
| `version` | string, zorunlu | Bu girdinin yazıldığı sürüm, örn. `"22.1"`. Bilgilendirme amaçlıdır; `DistributionRecommender` sürüm numarasına göre bir mantık yürütmez. |
| `download_url` | string, zorunlu | Resmi indirme sayfası. Raporda doğrudan tıklanabilir link olarak gösterilir — bu, `Scout.Reporter`'ın "self-contained" (harici kaynak yüklemez) ilkesini ihlal etmez: hiçbir şey otomatik olarak getirilmez, okuyucu isterse tıklar. |
| `desktop_environment` | string, zorunlu | Öntanımlı masaüstü ortamı, örn. `"Cinnamon"`. |
| `minimum_feature_level` | enum, zorunlu | `v1` \| `v2` \| `v3` \| `v4` — bu dağıtımın gerektirdiği en düşük x86-64 mikromimari seviyesi (bkz. `cpu.x86_64_feature_level`, `profile-v0.1.md`). Makinenin seviyesi bunun altındaysa `DistributionRecommender` bu girdiyi tamamen eler. |
| `minimum_memory_bytes` | long?, opsiyonel | Bilinen en düşük RAM. `null` = bilinen bir alt sınır yok, asla bir adayı elemek için kullanılmaz. |
| `minimum_disk_bytes` | long?, opsiyonel | Sistem diskinde gereken en düşük boş alan. `null` = bilinen bir alt sınır yok. Kontrol, `VerdictEngine`'in de kullandığı aynı "önyükleme diskindeki bağlı bölümlerin boş alanı" hesabını (`SystemDiskSpace.FreeBytes`) kullanır. |
| `offers_proprietary_driver_installer` | bool, zorunlu | Kurulum sırasında (ya da hemen sonrasında) NVIDIA gibi kapalı kaynak bir GPU sürücüsünü kurmak için hazır bir grafik arayüzü sunuyor mu — bkz. "Neden `offers_proprietary_driver_installer`". |
| `lightweight` | bool, zorunlu | Düşük RAM'li makineler için tercih edilecek, hafif bir masaüstü ortamı sunan bir seçenek mi. |
| `is_default_choice` | bool, zorunlu | Başka hiçbir sinyal devreye girmediğinde önerilecek genel amaçlı varsayılan. **En fazla bir girdi** bunu `true` yapmalı — bkz. aşağıdaki "Eşleştirme mantığı". |
| `recommended_when` | string, zorunlu | Bu dağıtımın tipik kullanım senaryosunu anlatan, veri dosyasını düzenleyenler için kısa bir açıklama. **Raporda olduğu gibi gösterilmez** — bkz. "`recommended_when` neden raporda görünmüyor". |

## Eşleştirme mantığı (`DistributionRecommender`)

1. **Eleme.** `minimum_feature_level`, `minimum_memory_bytes`, `minimum_disk_bytes`'tan herhangi biri makinenin bilinen değerini aşan her girdi listeden çıkarılır. Bir alan `null`/bilinmiyorsa o kritere göre **hiçbir eleme yapılmaz** — "okunamadı" hiçbir zaman "yetersiz" ile karıştırılmaz, profil şemasının genel ilkesiyle aynı. Hiçbir girdi hayatta kalmazsa (makine her adayın konforlu minimumunun altında), en az talepkâr olan tek girdi yine de "en iyi çaba" önerisi olarak seçilir (`LimitedHardware` gerekçesiyle).
2. **Öncelik sırasıyla birincil seçim** (bkz. "`DistributionRecommendationReason`"):
   - Toplam RAM, `DistributionRecommender`'ın yapılandırılabilir eşiğinin (varsayılan 4 GiB, `VerdictEngine`'in kendi varsayılanıyla aynı) altındaysa: hayatta kalan `lightweight` girdiler arasından, mevcut RAM'e göre **en iyi uyanı** (minimum RAM gereksinimi en yüksek ama yine de karşılanan) seçilir — her zaman en hafifi değil, "az kaynağı boşuna hafif bir seçenekle harcama" mantığıyla.
   - Yoksa, ilgili donanım değerlendirmesinde bir ekran kartı `Proprietary` destek seviyesindeyse (bkz. `Scout.Analyzer.Compatibility.SupportLevel`): hayatta kalanlar arasından `offers_proprietary_driver_installer: true` olan ilk girdi seçilir.
   - Yoksa: `is_default_choice: true` olan girdi seçilir — bu profil zaten her zaman bir Windows makinesinden geldiği için "Windows'a benzerlik" varsayılan gerekçedir.
3. **İkincil öneri**, sabit kodlanmış (veri dosyasında bir alan değil) genel bir tercih sırasına göre, birincil olmayan ilk hayatta kalan girdidir.

**Önyükleme modu (UEFI/Legacy) kasıtlı olarak bir seçim kriteri değildir**: veritabanındaki her dağıtım hem UEFI hem Legacy/BIOS önyüklemeyi destekler, bu yüzden aralarında gerçek bir ayrım yoktur; makinenin önyükleme modu zaten ayrı olarak `SystemConstraintKind.LegacyBootMode` üzerinden gösteriliyor, burada tekrarlanmasına gerek yok.

## Neden `offers_proprietary_driver_installer`

Bir NVIDIA ekran kartı `Proprietary` destek seviyesinde çıktığında, dağıtımlar arasında pratik bir fark var: Linux Mint ve Ubuntu (ve Ubuntu türevleri Xubuntu/Lubuntu) kurulumda ya da kurulumdan hemen sonra grafik bir araçla ("Driver Manager" / "Additional Drivers") kapalı kaynak sürücüyü tek tıkla sunar; Fedora ve Debian bunu varsayılan olarak **sunmaz** — kullanıcının RPM Fusion (Fedora) ya da `non-free` deposunu (Debian) elle eklemesi gerekir. Bu, "hangi dağıtım bu kullanıcı için daha az sürtünmeli" sorusuna doğrudan etki eden, gerçek bir fark.

## `recommended_when` neden raporda görünmüyor

Diğer iki veritabanının aksine (`hardware-compatibility.json`'daki `notes`, `software-compatibility.json`'daki `notes` — ikisi de raporda **olduğu gibi** gösterilir), buradaki tek cümlelik gerekçe raporda `recommended_when`'den değil, `Scout.Reporter.ReportStrings.DistributionReason`'dan gelir; bu metot `DistributionRecommendationReason` enum'una (`WindowsFamiliarity`/`LowMemory`/`NvidiaProprietaryDriver`/`LimitedHardware`) bakar. Fark şu: hardware/software notu belirli bir donanım/programın **kendi durumunu** anlatır (o satıra özgü bir olgu); buradaki gerekçeyse **hangi genel kuralın devreye girdiğini** anlatır (RAM mi, sürücü mü, varsayılan mı) — bu, veriden değil, karar mantığından kaynaklanan bir bilgidir, bu yüzden kod tarafında (ve kullanıcı dilinde, `ReportStrings` üzerinden) üretilir. `recommended_when` yalnızca veri dosyasını düzenleyen biri için dokümantasyon amaçlıdır.

## Sürüm notları

- **v0.1** — İlk sürüm: 6 kürasyonlu girdi (Linux Mint, Ubuntu, Fedora, Debian, Xubuntu, Lubuntu). RAM/disk eşikleri ve `offers_proprietary_driver_installer` değerleri, her dağıtımın kendi resmi belgelerine ve iyi bilinen kurulum davranışına (Mint/Ubuntu ailesinin sürücü aracı, Fedora/Debian'ın manuel adımı) dayanır; benchmark edilmiş kesin rakamlar değil, pratik yönlendirme değerleridir.
