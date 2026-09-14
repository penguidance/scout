# Hardware Compatibility Database Schema — v0.1

Bu doküman, `data/hardware-compatibility.json`'daki her girdinin (`CompatibilityEntry`) biçimini ve `CompatibilityMatcher`'ın (Scout.Analyzer) bunları nasıl eşleştirdiğini tanımlar. Makine profili şeması için bkz. [profile-v0.1.md](profile-v0.1.md) — bu, ayrı ama ilişkili bir şemadır: profil şeması Collector'ın *gözlemlediği* donanımı tanımlar, bu doküman Analyzer'ın o donanımı *değerlendirirken* kullandığı referans veritabanını tanımlar.

## Alanlar

| Alan | Tip | Açıklama |
|---|---|---|
| `vendor_id` | string, zorunlu | PCI/USB vendor ID, hex, örn. `"1002"`. Büyük/küçük harf duyarsız eşleşir. Ayrıca, gerçek bir vendor kimliği olmayan bazı genel USB altyapı düğümleri için **sentetik (4 hex hane olmayan) bir kimlik** olarak da kullanılır — bkz. aşağıdaki "Sentetik vendor_id" bölümü. |
| `device_id` | string?, opsiyonel | PCI/USB device ID, hex, örn. `"73FF"`. `null` = bu girdi tüm vendor için geçerli ("vendor-geneli" kural) — bkz. `device_class`. `device_id_range_start`/`device_id_range_end` doluysa yok sayılır. |
| `device_id_range_start`, `device_id_range_end` | string?, opsiyonel (birlikte) | Kapsayıcı (inclusive) hex device_id aralığı. Bir üreticinin device_id'leri donanım nesline göre kümelendiğinde ve tek bir vendor-geneli kuralın aralığın bir kısmı için yanlış cevap vereceği durumlar için — bkz. aşağıdaki "Neden device_id aralıkları var". |
| `device_class` | string?, opsiyonel | Vendor-geneli bir kural (`device_id` boş) için PNPClass benzeri kapsam (örn. `"Display"`, `"Net"`). Intel gibi tek vendor_id altında GPU/ağ/WiFi/USB gibi tamamen farklı donanım satan üreticiler için, tek bir kapsamsız kural yanıltıcı olurdu; sınıf bazlı kapsam aynı vendor için birden çok vendor-geneli kuralın çakışmadan bir arada durmasını sağlar. `device_id` doluysa yok sayılır. |
| `kernel_driver` | string, zorunlu | Linux çekirdek modülü/sürücü adı, örn. `"amdgpu"`, `"r8169"`. |
| `display_name` | LocalizedText?, opsiyonel | `kernel_driver`'ın ham adı kullanıcıyı tereddüde düşürebileceği durumlarda onun yerine gösterilecek sade bir etiket (örn. `"snd_hda_intel"` yerine `{ "en": "HDMI/DisplayPort audio output", "tr": "HDMI/DisplayPort ses çıkışı" }`). Boşsa raporda `kernel_driver` gösterilir. Bkz. aşağıdaki "`LocalizedText`: çok dilli metin alanları". |
| `support` | enum, zorunlu | `native` \| `firmware_required` \| `proprietary` \| `partial` \| `unsupported` \| `unknown` |
| `min_kernel` | string?, opsiyonel | Bilinen en düşük çalışır çekirdek sürümü, örn. `"5.4"`. En iyi çaba tahminidir, kesin garanti değildir. |
| `notes` | LocalizedText, zorunlu | Kullanıcıya gösterilecek kısa, sade dilde açıklama — Scout.Reporter'ın "Ne yapmalı?" sütunu, profilin diline göre çözümlenmiş **bu metni doğrudan** kullanır, kod içinde ayrıca bir çeviri/özet üretilmez. Bkz. aşağıdaki "`LocalizedText`: çok dilli metin alanları". |

## `LocalizedText`: çok dilli metin alanları

`notes` ve `display_name`, düz bir string değil, dil koduna göre birden fazla metin taşıyan bir nesnedir — `data/software-compatibility.json` ile paylaşılan aynı `Scout.Core.Models.LocalizedText` tipi:

```json
"notes": { "en": "Your USB ports work with no extra installation.", "tr": "USB bağlantı noktaların ek bir kurulum yapmadan çalışır." }
```

- **`en` zorunludur.** Diğer her dil (şu an yalnızca `tr`) opsiyoneldir; yalnızca sonucu iyileştirir, garanti edilen `en` düşüşünün yerini asla almaz.
- **`en` eksikse bu bir veri hatasıdır** — veritabanı yüklenirken (`CompatibilityDatabase.FromJson`/`LoadEmbedded`) `LocalizedTextJsonConverter` hemen bir `JsonException` fırlatır; sessizce boş bir string'e düşülmez.
- **Çözümleme**, `Scout.Reporter`'da rapor üretilirken profilin `os.language` alanına göre yapılır: tam eşleşme, yoksa (bir BCP-47 etiketiyse, örn. `"tr-TR"`) yalnızca birincil alt etiket, yoksa `en`. Ayrıntılı kural ve örnekler için bkz. [software-compatibility-v0.1.md](software-compatibility-v0.1.md)'deki aynı başlıklı bölüm — iki şema de aynı tipi ve aynı çözümleme kuralını kullanır.

## Eşleştirme sırası (`CompatibilityMatcher`)

1. **Tam eşleşme** (`MatchLevel.Exact`) — `vendor_id` + `device_id` birebir aynı.
2. **Nesil aralığı eşleşmesi** (`MatchLevel.RangeMatch`) — device_id, bu vendor için tanımlı bir `device_id_range_start..device_id_range_end` aralığına düşüyor.
3. **Vendor-geneli yedek** (`MatchLevel.VendorFallback`) — `device_id` boş bir girdi; cihazın `class`'ıyla eşleşen bir `device_class` kısıtlı girdi varsa o, yoksa `device_class` de boş (kapsamsız) bir girdi.
4. **Hiçbiri** (`MatchLevel.None`) — `support: unknown`. Asla tahmin yürütülmez.

Her aşama bir öncekinde eşleşme bulunamazsa denenir; tam eşleşme her zaman en yüksek önceliklidir.

## Neden `device_id` aralıkları var

**Motivasyon örneği:** AMD/ATI vendor kimliği `1002` için "amdgpu native" vendor-geneli kuralı genel olarak doğrudur (2014/GCN 1.2 ve sonrası kartlar için) — ama **AMD Radeon HD 7750** gibi 2012 nesli "Southern Islands" (GCN 1.0 — Tahiti/Pitcairn/Cape Verde ailesi) kartlarında **yanlıştır**: bu nesil için Linux'ta varsayılan ve önerilen sürücü daha eski `radeon`'dur, `amdgpu` bu kartları yalnızca deneysel bir önyükleme parametresiyle (varsayılan kapalı) destekler. Basit bir vendor-geneli kural bu farkı ayırt edemez ve HD 7750'yi yanlışlıkla "amdgpu, native" olarak raporlardı — destek seviyesi tesadüfen doğru (native) olsa bile, gösterilen sürücü adı yanlış olurdu.

**Çözüm:** `data/hardware-compatibility.json`'da `1002` için `device_id_range_start: "6780"`, `device_id_range_end: "683F"` olan ayrı bir girdi var (`kernel_driver: "radeon"`), bu girdi `CompatibilityMatcher`'da vendor-geneli `amdgpu` kuralından **önce** kontrol edilir. Radeon HD 7750 (`683F`) bu aralığa düştüğü için doğru sürücüyle (`radeon`) eşleşir; modern kartlar (örn. RX 5500 XT, `7340`) aralığın dışında kaldığı için etkilenmeden `amdgpu` vendor-geneli kuralına düşmeye devam eder.

Bu aralık şu an yalnızca Southern Islands ailesini kapsayacak şekilde **dar tutulmuştur** — AMD'nin device_id tahsisinin tamamını iddia etmez; karşılaşıldıkça (örn. Sea Islands / GCN 1.1 için de benzer bir ayrım gerekirse) yeni aralık girdileriyle genişletilmelidir. Bkz. [`tests/Scout.Tests/Analyzer/RealDatabaseBehaviorTests.cs`](../../tests/Scout.Tests/Analyzer/RealDatabaseBehaviorTests.cs) ve [`tests/Scout.Tests/Analyzer/SyntheticProfileVerdictTests.cs`](../../tests/Scout.Tests/Analyzer/SyntheticProfileVerdictTests.cs) (`synthetic-old-radeon.json`) — bu ayrımı doğrulayan regresyon testleri.

## Sentetik `vendor_id`: genel USB altyapısı

Bazı Windows PnP düğümleri (en yaygını: **USB kök hub'lar**, `USB\ROOT_HUB30`, `USB\ROOT_HUB20`, eski `USB\ROOT_HUB`) fiziksel, kendi USB tanımlayıcısına (VID_/PID_) sahip bir aygıt değildir — bunlar, bir USB denetleyicisi yongasının kök hub işlevi için Windows'un oluşturduğu sanal bir düğümdür. `hardware_id`'lerinde hiçbir zaman gerçek bir vendor:device çifti yoktur, bu yüzden normal eşleştirme mekanizması onları her zaman `unknown` bırakırdı — oysa bunlar Linux'un `usbcore` alt sistemiyle her zaman ve kayıtsız şartsız native çalışır, gerçek bir belirsizlik yoktur.

Bunu çözmek için Scout.Collector'ın `HardwareIdParser`'ı, ham `hardware_id`'sinde `"ROOT_HUB"` geçen cihazlara gerçek olmayan (4 hex hane değil, asla gerçek bir PCI/USB vendor kimliğiyle çakışmayacak şekilde) bir **sentetik `vendor_id`** — `"USBROOTHUB"` — atar. `data/hardware-compatibility.json`'da bu vendor_id için tek bir vendor-geneli girdi (`kernel_driver: "usbcore"`, `support: "native"`) bulunur ve tüm `ROOT_HUB` türevlerini (HUB20, HUB30, sürüm eki olmayan eski biçim) tek girdiyle kapsar.

## Sürüm notları

- **v0.1** — İlk sürüm: temel `vendor_id`/`device_id`/`device_class` alanları ve üç seviyeli eşleştirme (exact/vendor_fallback/none).
- **v0.1 (revizyon)** — `device_id_range_start`/`device_id_range_end` (dördüncü eşleştirme seviyesi: `RangeMatch`) ve `display_name` eklendi; `HardwareIdParser`'a USB kök hub'lar için sentetik `vendor_id` ataması eklendi.
- **v0.1 (revizyon 2)** — `notes` ve `display_name` düz string'den `LocalizedText`'e çevrildi (bkz. yukarıdaki "`LocalizedText`: çok dilli metin alanları") — `{ "en": "...", "tr": "..." }`, `en` zorunlu diğer diller opsiyonel. 20 `notes` ve 5 `display_name` değerinin tamamının mevcut Türkçe metni `tr`'ye taşındı, her biri için taslak bir İngilizce çeviri eklendi. Scout.Reporter artık raporu `profile.os.language`'a göre çözümlüyor (`SoftwareMatcher`'ın takma ad dil yeğlemesiyle aynı tolerans kuralı); `en` eksik bir girdi veritabanı yüklenirken hemen `JsonException` ile başarısız olur.
