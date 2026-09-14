# Machine Profile Schema — v0.1

Bu doküman, **Scout.Collector**'ın ürettiği, **Scout.Analyzer**'ın tükettiği makine profili JSON'unun şemasını tanımlar.

## Tasarım ilkeleri

- **Collector yorum yapmaz.** Bu dosyada tanımlanan her alan ham, gözlemlenmiş bir değerdir (bir WMI sorgusunun veya registry okumasının sonucu). "Bu sürücü Linux'ta çalışır mı", "bu disk düzeni desteklenir mi" gibi hiçbir uyumluluk kararı ya da türetilmiş yargı bu şemada yer almaz — bunlar Analyzer'ın ürettiği ayrı bir rapor şemasına aittir (`report-v0.1`, ileride).
- **Eşleştirme anahtarları kararlıdır, dostane isimler değildir.** PCI/USB donanımı için eşleştirme **daima `vendor_id` + `device_id` çifti üzerinden** yapılır — bu, Linux çekirdek modül veritabanlarının (`modules.pcimap`, `modules.usbmap`) ve PCI ID veritabanının da kullandığı aynı hex vendor:device çiftidir. `hardware_id`/`hardware_id_raw` yalnızca çapraz referans/teşhis içindir; ikisi de vendor:device çiftini içeren normalize/ham dizeler olsa da, Analyzer'ın birincil eşleştirme mantığı ayrıştırılmış `vendor_id`/`device_id` alanları üzerinden kurulmalıdır (bkz. `devices[]`). ACPI gibi vendor:device kavramı olmayan veri yollarında bu alanlar `null`'dur ve `hardware_id` tek kullanılabilir anahtar olur. `friendly_name` yalnızca raporda insana gösterim içindir; hiçbir eşleştirme mantığı `friendly_name` üzerinden kurulmamalıdır çünkü bu alan yerelleştirilmiş, sürücü sürümüne göre değişken veya eksik olabilir. Bir `vendor_id`+`device_id` çifti tek başına her zaman yeterli olmayabilir — aynı vendor'un device_id'leri donanım nesline göre kümelenip farklı sürücü gerektirebilir (örn. eski AMD GPU'lar `amdgpu` değil `radeon` ister) veya bazı Windows PnP düğümlerinin (USB kök hub gibi) hiç gerçek bir vendor:device çifti olmayabilir; Analyzer tarafındaki bu iki durumun nasıl ele alındığı için bkz. [hardware-compatibility-v0.1.md](hardware-compatibility-v0.1.md).
- **Şema sürümlenir.** `schema_version` alanı `major.minor` biçimindedir. Analyzer, desteklemediği bir `schema_version` gördüğünde veriyi reddetmeli, tahmin yürütmemelidir.
- **Gizlilik tasarım gereğidir.** `privacy` bloğu, hangi kişisel/tanımlayıcı alanların toplandığını/redakte edildiğini açıkça belirtir; bu, sonradan eklenen bir özellik değil şemanın parçasıdır. Kimlik niteliğindeki alanlar (`system.hostname`, `system.serial_number`) **varsayılan olarak toplanmaz** — yalnızca Collector açıkça `--include-identifiers` ile çalıştırılırsa toplanır.
- **"Okunamadı" hiçbir zaman "yok"/"false"/"0" ile karıştırılmaz.** Bu kural `bool` alanlarla sınırlı değildir: şemadaki her `bool`, sayısal ve enum alan, okunamayan bir değer için `false`/`0`/kodun kendi "Unknown" üyesine değil **`null`'a** düşer (bkz. `firmware.tpm.present`, `storage.bitlocker_available`, `memory.total_bytes`, `cpu.physical_cores`, `cpu.architecture`, `system.chassis_type`, `firmware.boot_mode`, `gpu_topology.layout`, `storage.disks[].*`). Bir enum'ın kendi "Unknown" üyesi (varsa) farklı, kendi başına anlamlı bir gözlemi temsil eder — "gerçek bir kod okundu ama tanımadık" — "hiçbir sinyal alamadık" (`null`) ile karıştırılmaz. Örnek: `system.chassis_type`'da SMBIOS kodu `2` gerçekten "Unknown" anlamına gelir (donanımın kendisi bunu söyler); enclosure hiç okunamazsa alan `null` olur. **JSON'da anahtar varlığı garantisi:** yukarıda sayılan zorunlu-ama-nullable alanların anahtarı çıktıda **her zaman bulunur** (değeri `null` olsa bile) — tamamen isteğe bağlı alanlardan (örn. `sku`, `driver_provider`) farklı olarak, onlar `null` olduğunda anahtarları JSON'dan tamamen düşer. Bu ayrım, `required` C# alanlarının .NET'in System.Text.Json'ında deserialize sırasında anahtarın var olmasını zorunlu kılmasından kaynaklanır; bir JSON tüketicisi (Analyzer dahil) zorunlu alanların anahtarını her zaman bulmayı bekleyebilir, isteğe bağlı olanlarınkini bekleyemez.
- **String alanlar normalize edilmiş olarak gelir.** Collector her string alanı ortak bir normalizasyon adımından geçirir: baştaki/sondaki boşluklar (ve `\0` gibi dolgu karakterleri) kırpılır, ve "To Be Filled By O.E.M.", "Default string", "System Serial Number", "Not Specified", "None", "N/A" gibi bilinen SMBIOS/OEM placeholder değerleri (büyük/küçük harf duyarsız) `null`'a çevrilir. Bu değerler donanımdan gelen gerçek veri değil, üreticinin hiç doldurmadığı alanlardır.
- **Ayrıştırılmış bir alanın ham hali asla silinmez.** Collector bir alanı yorumlayıp/ayrıştırıp (örn. `Win32_Tpm.SpecVersion`'ı `spec_version`+`manufacturer_version`'a bölmek, ya da bir SMBIOS/enum kodunu insan-okunur bir etikete çevirmek) yeni, türetilmiş bir alan ürettiğinde, ham değeri de **`*_raw`** adlı ayrı bir alanda saklar (bkz. `firmware.tpm.spec_version_raw`). Ayrıştırma başarısız olursa türetilmiş alan(lar) `null` olur ve bir `collection_errors` kaydı düşülür, ama `*_raw` yine de (okunabildiyse) korunur. Bu sayede ayrıştırma mantığı ileride değişirse/düzelirse, eski profiller ham veri kaybedilmeden yeniden işlenebilir.

## Üst düzey yapı

```json
{
  "schema_version": "0.1",
  "collector_version": "0.1.0",
  "collected_at": "2026-09-12T08:30:00Z",
  "machine_id": "b1946ac92492d2347c6235b4d2611184",
  "privacy": { },
  "system": { },
  "firmware": { },
  "cpu": { },
  "memory": { },
  "storage": { },
  "devices": [ ],
  "gpu_topology": { },
  "os": { },
  "software": [ ],
  "software_filtered_count": 0,
  "peripherals": [ ],
  "collection_errors": [ ]
}
```

### `schema_version` (string, zorunlu)
Bu dokümanın sürümü. Şu an sabit değer: `"0.1"`.

### `collector_version` (string, zorunlu)
Profili üreten Scout.Collector derlemesinin semver'i (örn. `"0.1.0"`). Analyzer, bilinen collector hatalarını sürüme göre telafi edebilmek için bunu saklar.

### `collected_at` (string, zorunlu)
Toplamanın başladığı an, ISO-8601 UTC (`YYYY-MM-DDTHH:mm:ssZ`).

### `machine_id` (string, zorunlu)
Windows makine GUID'inin (`HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`) **SHA-256 özeti**, `sha256:` önekiyle (örn. `"sha256:9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"`), hex tamamı küçük harf. Ham GUID hiçbir zaman saklanmaz/gönderilmez, hatta bir `collection_errors` mesajına bile yazılmaz — bu alan yalnızca "aynı makineden gelen iki profili eşleştir" amaçlı sözde-anonim bir kimliktir, geri döndürülemez. Registry StdRegProv WMI sınıfı üzerinden okunur (bkz. Kaynak eşlemesi), böylece uzak bir hedefte de CIM/WS-Man üzerinden çalışır. Okunamazsa `""` (boş dize) yazılır ve `collection_errors`'a kayıt düşülür.

### `privacy` (object, zorunlu)
Toplama sırasında hangi kişisel/tanımlayıcı bilgilerin dahil edildiğini belirtir.

**Varsayılan davranış "toplama":** `system.hostname` ve `system.serial_number` **varsayılan olarak toplanmaz**. Collector bu alanları yalnızca `--include-identifiers` bayrağı verildiğinde okumaya çalışır; bayrak verilmezse alanlar hiç sorgulanmaz (bu bir koleksiyon hatası değildir) ve karşılık gelen yollar `redacted_fields`'a yazılır.

| Alan | Tip | Açıklama |
|---|---|---|
| `hostname_included` | bool | `system.hostname` bu çalıştırmada gerçekten dolduruldu mu (bayrak verilmemişse veya okuma başarısız olsa da `false`) |
| `username_included` | bool | Oturum açan kullanıcı adı toplandı mı (v0.1'de hiç toplanmaz, alan gelecek için ayrılmıştır) |
| `serial_numbers_included` | bool | `system.serial_number` bu çalıştırmada gerçekten dolduruldu mu |
| `redacted_fields` | string[] | `--include-identifiers` verilmediği için bilinçli olarak hiç denenmeyen alan yolları (v0.1'de: `"system.hostname"`, `"system.serial_number"`) |

### `system` (object, zorunlu)
| Alan | Tip | Açıklama |
|---|---|---|
| `manufacturer` | string | Sistem üreticisi |
| `model` | string | Sistem modeli |
| `chassis_type` | string? (enum) | `Desktop`, `Laptop`, `Tablet`, `Server`, `AllInOne`, `Other`, `Unknown`, veya `null`. `null` = enclosure hiç okunamadı (sinyal yok); `Unknown` = SMBIOS kodu **2**'nin kendisi ("firmware kasa tipini bilmiyor") — gerçek, farklı bir gözlem; `Other` = tanınan ama eşleme tablosunda karşılığı olmayan bir kod |
| `sku` | string? | Üretici SKU numarası |
| `serial_number` | string? | `privacy.serial_numbers_included=false` ise `null` |
| `hostname` | string? | `privacy.hostname_included=false` ise `null` |

### `firmware` (object, zorunlu)
| Alan | Tip | Açıklama |
|---|---|---|
| `bios_vendor` | string | BIOS/UEFI üreticisi |
| `bios_version` | string | BIOS sürüm dizesi |
| `bios_release_date` | string? | ISO-8601 tarih |
| `boot_mode` | string? (enum) | `UEFI`, `Legacy`, veya `null` (belirlenemedi). Bu enum'da ayrı bir "Unknown" üyesi yok: UEFI/Legacy'nin ikisi de kendi başına pozitif bir tespittir, üçüncü bir "tanındı ama kategorize edilemedi" durumu yoktur |
| `secure_boot_enabled` | bool? | `null` = sorgulanamadı, ya da Legacy modda anlamsız |
| `tpm` | object | `{ "present": bool?, "spec_version": string?, "manufacturer_version": string?, "spec_version_raw": string?, "ready": bool? }` — `present`: `true`/`false` sorgu başarıyla yanıtlandığında (`false` = TPM sınıfı 0 örnekle döndü, yani donanımda gerçekten TPM yok); sorgunun kendisi başarısız olduysa (örn. erişim reddedildi) `null`. "Okunamadı" hiçbir zaman `false`'a düşürülmez. `spec_version`/`manufacturer_version`, ham `Win32_Tpm.SpecVersion` değerinin (örn. `"2.0, 0, 1.38"`) virgülle ayrılmış 1. ve 3. bileşenleridir (2. bileşen — revizyon — ayrı bir alan olarak yüzeye çıkarılmaz, yalnızca `spec_version_raw` içinde durur); biçim beklenmedikse ikisi de `null` olur ama `spec_version_raw` yine de ham değeri korur + `collection_errors`'a kayıt düşülür. |

### `cpu` (object, zorunlu)
| Alan | Tip | Açıklama |
|---|---|---|
| `vendor` | string | `GenuineIntel`, `AuthenticAMD` vb. |
| `model` | string | CPU marka dizesi (brand string) |
| `physical_cores` | int? | `null` = okunamadı (asla `0`'a düşürülmez) |
| `logical_processors` | int? | `null` = okunamadı |
| `architecture` | string? (enum) | `x86_64`, `arm64`, `x86`, `Unknown`, veya `null`. `null` = kaynak hiç okunamadı; `Unknown` = gerçek bir mimari kodu okundu ama tanımadık (bkz. genel ilke) |
| `x86_64_feature_level` | string? (enum) | `v2`, `v3`, `null` (mimari `x86_64` değilse, ya da vendor+family+model bilinen eşleme tablosunda yoksa) — CPU'nun desteklediği [x86-64 mikromimari seviyesi](https://en.wikipedia.org/wiki/X86-64#Microarchitecture_levels) (glibc/Linux dağıtımlarının minimum CPU gereksinimi olarak kullandığı sınıflandırma). CPUID probing'e değil, `Win32_Processor`'dan okunan vendor + CPUID family/model'e dayanan **kürasyonlu bir eşleme tablosuna** dayanır (bkz. aşağıdaki kaynak eşlemesi); bu sayede uzak bir hedefte de CIM/WS-Man üzerinden belirlenebilir. Tabloda olmayan bir vendor/family/model kombinasyonu için `null` yazılır ve `collection_errors`'a kayıt düşülür — tahmin yürütülmez. Ham bir gözlemdir — desteklenen dağıtımın gerektirdiği seviyeyle karşılaştırma Analyzer'ın işidir. |
| `virtualization_firmware_enabled` | bool? | VT-x/AMD-V firmware'de açık mı |

### `memory` (object, zorunlu)
| Alan | Tip | Açıklama |
|---|---|---|
| `total_bytes` | long? | Okunabilen modüllerin kapasitelerinin toplamı; hiçbir modül okunamadıysa `null` (asla `0`'a düşürülmez) |
| `modules` | array | Her modül: `{ "capacity_bytes": long?, "rated_speed_mhz": int?, "configured_speed_mhz": int?, "manufacturer": string?, "part_number": string?, "memory_type": string?, "memory_type_raw": string? }` |

`rated_speed_mhz` modülün JEDEC/rated hızıdır (`Speed`); `configured_speed_mhz` fiilen çalıştığı hızdır (`ConfiguredClockSpeed`) — XMP/EXPO açık değilse ikisi farklı olabilir, bu yüzden ayrı iki alan olarak tutulur. `memory_type`, `SMBIOSMemoryType` kodu (`Win32_PhysicalMemory.MemoryType` **değil** — o alan modern Windows'ta güvenilmezdir ve genelde her zaman `0` döner) bilinen bir SMBIOS Type 17 değeriyse (`"DDR4"`, `"DDR5"` vb.) etikete çevrilir; tanınmıyorsa `memory_type` `null` olur ama ham kod `memory_type_raw`'da (metin olarak, örn. `"0"`) her zaman kalır — ham kod asla çözümlenmiş bir etiketmiş gibi `memory_type`'a yazılmaz (bkz. genel "okunamadı ≠ 0" ilkesi).

### `storage` (object, zorunlu)
| Alan | Tip | Açıklama |
|---|---|---|
| `disks` | array | Aşağıya bakınız |
| `bitlocker_available` | bool? | BitLocker yönetim API'si (Win32_EncryptableVolume) sorgulanabildi mi; sorgu başarısız olduysa (örn. erişim reddedildi) `null` — v0.1'de yalnızca `true`/`null` üretilir, kesin bir `false` yolu henüz yok |

`disks[]` öğesi:
| Alan | Tip | Açıklama |
|---|---|---|
| `disk_number` | int? | Windows disk numarası; okunamadıysa `null` |
| `media_type` | string? (enum) | `SSD`, `HDD`, `NVMe`, `Unknown`, veya `null` (sinyal yok). NVMe veri yolu, medya tipinden bağımsız olarak her zaman `NVMe` kabul edilir |
| `bus_type` | string | Ham `MSFT_Disk.BusType` kodundan çevrilen etiket (`NVMe`, `SATA`, `USB`, `SAS`, vb.); tanınmayan bir kod gelirse ham sayısal kodun kendisi metin olarak yazılır |
| `size_bytes` | long? | `null` = okunamadı |
| `model` | string | |
| `firmware_version` | string? | |
| `is_boot_disk` | bool? | `null` = okunamadı (asla `false`'a düşürülmez) |
| `partition_style` | string? (enum) | `GPT`, `MBR`, `Unknown` (kod `0`, "Windows'un kendisi tanımıyor"), veya `null` (sinyal yok) |
| `partitions` | array | Her biri: `{ "filesystem": string?, "size_bytes": long?, "drive_letter": string?, "bitlocker_status": string?, "free_bytes": long? }` |

`partitions[].drive_letter` yoksa (EFI System, Recovery, MSR gibi bölümler) `filesystem` `null` ve `bitlocker_status` `NotApplicable` olur — okunamadıkları için değil, gerçekten bağlı bir birim olmadığı için. Sürücü harfi *varsa* ama BitLocker durumu okunamadıysa `bitlocker_status` `null` olur (`NotApplicable` değil).

`bitlocker_status` değerleri: `FullyEncrypted`, `EncryptionInProgress` (duraklatılmış şifreleme dahil), `DecryptionInProgress` (duraklatılmış şifre çözme dahil), `FullyDecrypted`, `Unknown`, `NotApplicable`, veya `null` (belirlenemedi).

`partitions[].free_bytes`, bağlı birimdeki boş alan (`MSFT_Volume.SizeRemaining`). Sürücü harfi yoksa (bağlı bir birim yoksa — `filesystem` ile aynı gerekçe) `null`; sürücü harfi *var* ama bu alan okunamadıysa da `null`. `size_bytes`'tan farklı olarak "zorunlu-ama-nullable" değildir: burada gerçekten "kavram geçerli değil" (bağlı birim yok) durumu var, salt "okunamadı" değil — bu yüzden anahtar `null` olduğunda JSON'dan düşer.

### `devices[]` (array, zorunlu)
Her öğe, Linux uyumluluğu açısından anlamlı bir PNPClass'a ait bir PnP aygıtını temsil eder — makinedeki **tüm** PnP aygıtları değil (bir Windows makinesi binlerce PnP girdisi döndürebilir). Hangi sınıfların dahil edildiği sabit bir liste değil, Collector içinde kolayca genişletilebilir bir yapılandırmadır (v0.1 varsayılanı: `Display`, `Net`, `Media`, `Bluetooth`, `USB`, `SCSIAdapter`, `Biometric`, `Camera`, `PrintQueue`, `HIDClass`, `SmartCardReader`).

| Alan | Tip | Açıklama |
|---|---|---|
| `hardware_id` | string | Normalize edilmiş biçim: PCI/USB için yalnızca vendor+device çifti (örn. `PCI\VEN_1002&DEV_73FF`, `SUBSYS_.../REV_..` atılmış); başka bir yol için (örn. ACPI) `hardware_id_raw` ile aynıdır. **Asıl eşleştirme anahtarı bu değil, `vendor_id`+`device_id` çiftidir** — bkz. "Tasarım ilkeleri". |
| `hardware_id_raw` | string | `Win32_PnPEntity.HardwareID` dizisinin ham, dokunulmamış ilk (en özgül) elemanı, örn. `PCI\VEN_1002&DEV_73FF&SUBSYS_0123458&REV_C1`. `hardware_id`/`vendor_id`/`device_id` bundan türetilse de, ham değer hiçbir zaman silinmez (genel "*_raw" ilkesi). |
| `vendor_id` | string? | Hex, büyük harf, örn. `"1002"`. `hardware_id_raw` içinde `VEN_xxxx`/`VID_xxxx` deseni yoksa `null`. |
| `device_id` | string? | Hex, büyük harf, örn. `"73FF"`. Aynı şekilde `null` olabilir. |
| `bus_type` | string (enum) | `PCI`, `USB`, `ACPI`, `Other` — `hardware_id_raw`'ın `\` öncesi önekinden belirlenir |
| `compatible_ids` | string[] | `HardwareID` dizisinin geri kalanı (ilk/en özgül eleman hariç), Windows'un verdiği sırayla, ham |
| `friendly_name` | string | **Yalnızca gösterim içindir, eşleştirmede kullanılmaz.** Kullanıcıya dönük aygıt adı (`Win32_PnPEntity.Name`). |
| `class` | string | PnP aygıt sınıfı (`Win32_PnPEntity.PNPClass`) — `Net`, `Display`, `HIDClass`, `Bluetooth`, vb. |
| `driver_provider` | string? | |
| `driver_version` | string? | |
| `driver_date` | string? | ISO-8601 tarih |
| `status` | string? (enum) | `OK`, `Error`, `Degraded`, `Unknown`, veya `null` (kod hiç okunamadı). `Win32_PnPEntity.ConfigManagerErrorCode`'dan türetilir (0 → `OK`, başka her kod → `Error` — v0.1'de ~30 belgeli CM_PROB_* kodunun tamamı ayrıştırılmaz, `Degraded`/`Unknown` şimdilik üretilmez ama gelecekte daha ince bir eşleme için ayrılmıştır) |

Bir aygıtın `HardwareID` dizisi tamamen boşsa (kullanılabilir hiçbir eşleştirme anahtarı yoksa), o aygıt tamamen atlanır — kaç aygıtın bu şekilde atlandığı bir `collection_errors` kaydında sayılır.

### `gpu_topology` (object, zorunlu)
Ayrı bir WMI sorgusu (örn. `Win32_VideoController`) yapılmaz — bu blok tamamen `devices[]` içindeki `class = "Display"` girdilerinden türetilir.

| Alan | Tip | Açıklama |
|---|---|---|
| `gpus` | array | Her biri: `{ "hardware_id": string, "vendor_id": string?, "device_id": string?, "friendly_name": string, "driver_version": string? }` — karşılık gelen `devices[]` girdisinin bir görünümü; `hardware_id` oraya çapraz referanstır, asıl eşleştirme `vendor_id`+`device_id` ile yapılır |
| `layout` | string? (enum) | `Single`, `Hybrid` (entegre+ayrık bir arada), `MultiDiscrete` (2+ ayrık GPU), veya `null`. 0 GPU'da, ya da 2+ GPU'nun entegre/ayrık olarak güvenle sınıflandırılamadığı durumlarda `null` — **tahmin yürütülmez.** |

**Hibrit/çoklu-ayrık tespiti bir sezgiseldir, kesin bir WMI sinyali değildir**: Windows, bir GPU'nun entegre mi ayrık mı olduğunu doğrudan raporlamaz. Collector, `vendor_id` + `friendly_name` içindeki bilinen kalıplara bakar (örn. NVIDIA her zaman ayrıktır; Intel `vendor_id=8086` genelde entegredir ama adı "Arc" içeriyorsa ayrıktır; AMD'de "Radeon(TM) Graphics"/"Vega Graphics" entegre, "Radeon RX/HD/PRO" ayrık kabul edilir). Bilinen kör noktalar vardır (örn. tanınmayan bir vendor_id, ya da adı kalıplara uymayan bir kart) — bu durumlarda `layout` `null` kalır.

### `os` (object, zorunlu)
| Alan | Tip | Açıklama |
|---|---|---|
| `edition` | string | örn. `Windows 11 Pro` |
| `version` | string | örn. `10.0.26200` |
| `build` | string | örn. `26200` |
| `display_version` | string? | örn. `25H2`. Yalnızca registry'de bulunur (WMI'da karşılığı yok); registry okunamazsa `null` |
| `install_date` | string? | ISO-8601, saat dilimi ofsetiyle (örn. `2025-01-01T03:02:59+03:00`) |
| `architecture` | string? (enum) | `x86_64`, `arm64`, `x86`, `Unknown`, veya `null` — bkz. `cpu.architecture`'daki null/Unknown ayrımı. `Win32_OperatingSystem.OSArchitecture` serbest metin bir alandır (gözlemlenen değerler: `"64 bit"`/`"32 bit"`, ARM Windows'ta `"ARM 64-bit Processor"`), sayısal bir kod değil — bu yüzden alt dize eşleştirmesiyle yorumlanır |
| `language` | string? | BCP-47, `MUILanguages` dizisinin ilk (birincil) öğesi, örn. `tr-TR` |

### `software[]` (array, zorunlu)
Kurulu yazılım envanteri — bu şemanın (Collector'ın ürettiği profilin) parçası olarak **sadece envanterdir**; her girdinin Linux'ta bir muadili olup olmadığı (`native`/`equivalent`/`wine`/`web`/`blocked`) bu envanterin kendisinde tutulmaz, Analyzer'ın `data/software-compatibility.json`'a karşı çalıştırdığı ayrı bir eşleştirme adımıdır — bkz. [software-compatibility-v0.1.md](software-compatibility-v0.1.md). İki kaynaktan doldurulur (bkz. Kaynak eşlemesi):

1. Registry `Uninstall` anahtarları (`HKLM` native, `HKLM\WOW6432Node`, `HKCU`) — `Win32_Product` **kesinlikle kullanılmaz**, bkz. "Neden `Win32_Product` kullanılmıyor".
2. `Win32_InstalledStoreProgram` — MSIX/Store paketleri, hiçbir zaman `Uninstall` anahtarlarında görünmez.

**Gürültü filtresi Collector'da uygulanır, Analyzer'da değil** — ham, filtrelenmemiş bir liste Analyzer'a bırakılmaz, çünkü `Uninstall` anahtarları çok kirlidir (bkz. aşağıdaki filtre listesi). Bu, "Collector yorum yapmaz" ilkesine görünüşte aykırı gibi durabilir, ama filtrelenen girdiler bir *uyumluluk yargısı* değil — "bu bir program mı, yoksa bir alt bileşen/güncelleme kaydı mı" sorusuna verilen, kararlı ve yeniden üretilebilir bir cevaptır; bu yüzden Collector'ın sorumluluğu sayılır. Filtre listesi yapılandırılabilir (`SoftwareFilterOptions`), sabit kodlanmamıştır.

| Alan | Tip | Açıklama |
|---|---|---|
| `name` | string | `DisplayName` (registry) ya da `Name` (Store — bu durumda bir paket kimliğidir, örn. `"Microsoft.WindowsCalculator"`, kullanıcı dostu bir görünen ad değil; Windows bu sınıf üzerinden daha iyisini sunmuyor) |
| `version` | string? | `DisplayVersion` / `Version` |
| `publisher` | string? | `Publisher` / `Vendor` (Store girdisinde ham bir X.500 sertifika konusu dizesi olabilir, örn. `"CN=Microsoft Corporation, O=..., C=US"`) |
| `install_date` | string? | ISO-8601 (varsa `InstallDate` YYYYMMDD'den çevrilir) |
| `estimated_size_bytes` | long? | `EstimatedSize` (KiB) bayta çevrilir. Çoğu yükleyici bu değeri hiç yazmadığı için `null` çok yaygın ve normaldir — bir okuma hatası değildir. Store girdilerinde her zaman `null` (bu sınıfta karşılığı yok) |
| `uninstall_string` | string? | Yalnızca teşhis amaçlı saklanır, hiçbir zaman çalıştırılmaz. Store girdilerinde her zaman `null` |
| `registry_view` | string? (enum) | `Wow6432Node` (32-bit) veya `Native` (64-bit) — hangi registry görünümünden okunduğu. Registry dışı bir kaynaktan (Store) gelen girdilerde `null` — "okunamadı" değil, kavramın kendisi geçerli değil, bu yüzden anahtar JSON'dan düşer |
| `source` | string (enum) | `HklmUninstallKey`, `HkcuUninstallKey`, veya `StorePackage` — hangi kökten geldiği. `registry_view`'dan ayrı bir alan: o yalnızca 32/64-bit ayrımını taşır, `source` ise hive'ı (HKLM/HKCU) ve registry-dışı kaynağı ayırt eder |
| `registry_key_name` | string? | Uninstall alt anahtarının kendi adı — MSI ile kurulmuş bir program için ProductCode GUID'i (örn. `"{90160000-008C-0409-1000-0000000FF1CE}"`), EXE tabanlı çoğu yükleyici için yükleyicinin seçtiği bir ad. `name`'in aksine bu, kurulum anında bir kez yazılır ve Windows'un görüntüleme dili sonradan değişse bile **yeniden çevrilmez** — `name` lokalize olduğunda (bkz. `docs/schema/software-compatibility-v0.1.md`) Analyzer'ın kullanabileceği çok daha kararlı bir kimlik sinyalidir. Registry dışı bir kaynaktan (Store) gelen girdilerde `null` |
| `install_location` | string? | Registry'nin `InstallLocation` değeri (bir kurulum klasörü yolu), yükleyici bunu yazdıysa. Çoğu yükleyici hiç yazmaz, bu normal ve yaygın bir durumdur — okuma hatası değildir. Bir yol da genelde görüntüleme diline göre yeniden çevrilmez (örn. "Program Files" NTFS'teki gerçek klasör adı olarak Windows arayüz dilinden bağımsız sabit kalır), ama bu sürümde Analyzer tarafından henüz eşleştirme için kullanılmıyor — ileride kullanılmak ve profili elle inceleyen biri için saklanıyor |
| `category` | string (enum) | `application`, `runtime`, `driver`, veya `system` — Collector'ın (yapılandırılabilir) anahtar kelime kurallarından. Varsayılan `application`'dır; bir makinede onlarca VC++ Redistributable/İ.NET Runtime/sürücü paketi görülmesi normaldir, bunlar silinmez ama raporun bu kategorileri katlayabilmesi için ayrı etiketlenir |
| `system_component` | bool | Registry'nin ham `SystemComponent` değerini yansıtır. `SystemComponent=1` olan girdiler Collector tarafından **tamamen elenir** (aşağıya bakınız), bu yüzden profile ulaşan her girdide bu alan her zaman `false`'dur — alan yine de tutulur ki zaten toplanmış bir profil, ham registry verisine geri dönmeden yeniden değerlendirilebilsin. Store girdilerinde her zaman `false` (karşılığı yok) |

**Elenen girdiler (profile hiç yansımaz):**

| Kural | Gerekçe |
|---|---|
| `DisplayName` boş/yok | Gösterilecek bir ad olmadan anlamlı bir envanter satırı değil |
| `SystemComponent = 1` | Registry'nin kendisi bunu bağımsız bir program değil bir alt bileşen olarak işaretliyor |
| `ParentKeyName` veya `ParentDisplayName` dolu | Başka bir (zaten listelenmiş) ürünün bir parçası, ayrı bir program değil |
| Ad `KB` + rakamla başlıyor, ya da "Update for" / "Security Update" / "Hotfix for" içeriyor (büyük/küçük harf duyarsız) | Windows güncellemesi/hotfix — kullanıcının "bu makinedeki bir program" olarak tanımayacağı bir kayıt |

Sürücü paketleri ve runtime/redistributable bileşenleri bu listede **yoktur** — silinmezler, yalnızca `category` ile etiketlenirler (yukarıya bakınız).

Eleme sayısı `collection_errors`'a değil, ayrı bir sayaca yazılır — bkz. `software_filtered_count`. Bu rutin, beklenen bir filtreleme adımıdır, bir toplama hatası değil.

### `software_filtered_count` (int?, zorunlu değil)
Collector'ın incelediği ama yukarıdaki kurallardan biri yüzünden `software[]`'e dahil etmediği registry/Store girdisi sayısı. Bu alan bu şemaya sonradan eklendi: bu alandan önceki bir collector sürümüyle toplanmış bir profilde anahtar hiç yoktur (`null` olarak okunur) — bu "okunamadı" değil, "bu profili üreten collector sürümü bunu hiç hesaplamadı" anlamına gelir. Bu veya daha yeni bir collector'ın ürettiği her profilde gerçek (sıfır dahil) bir sayı olarak bulunur.

### `peripherals[]` (array, zorunlu — henüz toplanmıyor)
Bağlı çevre birimleri (klavye, fare, yazıcı, ses aygıtları vb.) — `devices[]` ile **birebir aynı alan kümesini ve eşleştirme kuralını** paylaşır (`hardware_id`/`hardware_id_raw`/`vendor_id`/`device_id`/`bus_type`/`compatible_ids`/`friendly_name`/`class`), yalnızca `class` yerine kullanıcı etkileşim sınıflarına odaklanan farklı bir filtre kullanır ve ek olarak bir `connection_type` taşır. v0.1'de collector'ı henüz yazılmadı (bkz. `collection_errors`).

| Alan | Tip | Açıklama |
|---|---|---|
| `hardware_id`, `hardware_id_raw`, `vendor_id`, `device_id`, `bus_type`, `compatible_ids`, `friendly_name`, `class` | — | `devices[]` ile aynı anlam — bkz. yukarısı |
| `connection_type` | string (enum) | `USB`, `Bluetooth`, `PS2`, `Internal`, `Unknown` |

### `collection_errors[]` (array, zorunlu)
Collector bir bölümü toplayamadığında burada **ham hata** kaydı bırakır; hiçbir yorum/öneri içermez.

| Alan | Tip | Açıklama |
|---|---|---|
| `component` | string | Hatanın oluştuğu üst düzey profil bileşeni (örn. `"storage"`) |
| `source` | string | Sorgulanan WMI sınıfı veya registry anahtarı |
| `message` | string | Ham istisna/hata mesajı |
| `occurred_at` | string | ISO-8601 UTC |

---

## Kaynak eşlemesi

| Şema alanı | Kaynak | Not |
|---|---|---|
| `machine_id` | Registry (StdRegProv WMI sınıfı üzerinden — WS-Man uyumlu): `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` | Okunduktan hemen sonra SHA-256 ile hash'lenir, ham GUID hiçbir yere (çıktı, log, `collection_errors` mesajı) yazılmaz |
| `system.manufacturer`, `system.model`, `system.sku` | WMI: `Win32_ComputerSystem` (`Manufacturer`, `Model`, `SystemSKUNumber`) | |
| `system.chassis_type` | WMI: `Win32_SystemEnclosure.ChassisTypes[0]` | Sayısal kod → enum eşlemesi Collector'da normalize edilir (ham kod değil, standart bir kategori adı yazılır — bu normalize etme yorum değildir, sadece kodlamadan insan/anahtar okunur biçime çeviridir) |
| `system.hostname` | WMI: `Win32_ComputerSystem.DNSHostName` | `--include-identifiers` verilmediyse hiç sorgulanmaz (bkz. `privacy`) |
| `system.serial_number` | WMI: `Win32_BIOS.SerialNumber` | `--include-identifiers` verilmediyse hiç sorgulanmaz (bkz. `privacy`) |
| `firmware.bios_vendor/version/release_date` | WMI: `Win32_BIOS` (`Manufacturer`, `SMBIOSBIOSVersion`, `ReleaseDate`) | |
| `firmware.boot_mode` | Registry: `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State\UEFISecureBootProgram` varlığı + `PEFirmwareType` (`HKLM\SYSTEM\CurrentControlSet\Control` altında `firmware environment` API'si) | Win32 API `GetFirmwareEnvironmentVariable` başarı/hata koduyla da doğrulanır |
| `firmware.secure_boot_enabled` | Registry: `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State\UEFISecureBootEnabled` | Legacy boot modunda anahtar yok → `null` |
| `firmware.tpm` | WMI: `root\CIMV2\Security\MicrosoftTpm` sınıfı `Win32_Tpm` (`IsEnabled_InitialValue`, `IsActivated_InitialValue`, `SpecVersion`) | `present`: sorgu 0 örnekle başarıyla dönerse `false` (gerçekten yok), sorgunun kendisi başarısız olursa (örn. erişim reddedildi) `null`. `spec_version`/`manufacturer_version`, ham `SpecVersion` değerinin (`"sürüm, revizyon, üretici sürümü"`) virgülle ayrıştırılmasıyla türetilir; ham değer her zaman `spec_version_raw`'da kalır (bkz. genel "*_raw" ilkesi) |
| `cpu.vendor/model` | WMI: `Win32_Processor` (`Manufacturer`, `Name`) | |
| `cpu.physical_cores/logical_processors` | WMI: `Win32_Processor` (`NumberOfCores`, `NumberOfLogicalProcessors`) | |
| `cpu.x86_64_feature_level` | WMI: `Win32_Processor` (`Manufacturer`, `Description`) | CPUID probing YOK. `Description` alanındaki `"... Family <F> Model <M> Stepping <S>"` metninden ham CPUID family/model ayrıştırılır (`Win32_Processor.Family` **kullanılmaz** — o alan CPUID family'si değil, WMI'ın kendi "well-known value" pazarlama numaralandırmasıdır); vendor+family+model, kürasyonlu bir eşleme tablosundan (AMD Zen [0x17] ve sonrası → v3, Intel Haswell [family 6, model ≥ 0x3C ailesi] ve sonrası → v3, daha eski bilinen aileler → v2, bilinmeyenler → `null` + `collection_errors`) geçirilir |
| `memory.total_bytes`, `memory.modules[]` | WMI: `Win32_PhysicalMemory` (`Capacity`, `Speed`, `ConfiguredClockSpeed`, `Manufacturer`, `PartNumber`, `SMBIOSMemoryType`) | `total_bytes` = okunabilen modül kapasitelerinin toplamı (bkz. yukarıdaki nullable not). `MemoryType` **kullanılmaz** — modern Windows'ta güvenilmez (genelde `0` döner); onun yerine `SMBIOSMemoryType` okunur. `Win32_PhysicalMemoryArray` de sorgulanır, ama yalnızca tanı amaçlı: `Win32_PhysicalMemory` 0 örnekle dönerse (bazı sanal makinelerde/BIOS'larda görülen bilinen bir SMBIOS Type 17 sınırlaması) bunun gerçek bir hata mı yoksa bu ortamın sınırlaması mı olduğunu ayırt etmek için kullanılır; şemada kendi alanı yoktur |
| `storage.disks[]` | WMI (`root\Microsoft\Windows\Storage`): `MSFT_Disk` (`Number`, `BusType`, `Size`, `FriendlyName`, `IsBoot`, `PartitionStyle`) + `MSFT_PhysicalDisk` (`DeviceId`, `MediaType`, `FirmwareVersion`) | `media_type`/`firmware_version`, `MSFT_Disk.Number`'a eşit `MSFT_PhysicalDisk.DeviceId` üzerinden eşleştirilir — `MSFT_Disk`'in kendisinde medya tipi/firmware sürümü yoktur |
| `storage.disks[].partitions[]` | WMI: `MSFT_Partition` (`DiskNumber`, `DriveLetter`, `Size`) + `MSFT_Volume` (`DriveLetter`, `FileSystem`, `SizeRemaining`) | `filesystem`/`free_bytes`, sürücü harfi üzerinden `MSFT_Partition` ↔ `MSFT_Volume` eşleştirmesiyle bulunur (birim-eşleme association sınıfları yerine bilinçli olarak basitleştirilmiş bir yaklaşım — v0.1 için yeterli). `DriveLetter` her iki sınıfta da `char16` (tek karakter) tipindedir, `string` değil; atanmamış bir bölümde ham değer boşluk değil `'\0'` olabilir — normalizasyon adımı ikisini de `null`'a çevirir |
| `storage.disks[].partitions[].bitlocker_status` | WMI: `root\CIMV2\Security\MicrosoftVolumeEncryption` → `Win32_EncryptableVolume.GetConversionStatus()` | Sürücü harfi üzerinden eşleştirilir (`Win32_EncryptableVolume.DriveLetter` burada gerçekten `string`, örn. `"C:"`). Yöntem "duraklatılmış" iki durum daha döndürür (EncryptionPaused/DecryptionPaused); şemada ayrı bir karşılığı olmadığından sırasıyla `EncryptionInProgress`/`DecryptionInProgress`'e eşlenir |
| `devices[]` | WMI: `Win32_PnPEntity` (`PNPClass`, `HardwareID[]`, `Name`, `DeviceID`, `ConfigManagerErrorCode`) + `Win32_PnPSignedDriver` (`DeviceID`, `DriverProvider`, `DriverVersion`, `DriverDate`) | `HardwareID[0]` → `hardware_id_raw`; regex ile `VEN_xxxx&DEV_yyyy` / `VID_xxxx&PID_yyyy` deseni aranır → `vendor_id`/`device_id` + normalize `hardware_id`. Sürücü bilgisi `Win32_PnPSignedDriver.DeviceID` = `Win32_PnPEntity.DeviceID` eşleşmesiyle birleştirilir (tek sorguda tüm sürücüler çekilip DeviceID'ye göre indekslenir — aygıt başına ayrı sorgu yok) |
| `gpu_topology.gpus[]` | — (ayrı bir WMI sorgusu yok) | `devices[]`'teki `class = "Display"` girdilerinden bire bir türetilir |
| `os.*` | WMI: `Win32_OperatingSystem` (`Caption`, `Version`, `BuildNumber`, `InstallDate`, `OSArchitecture`, `MUILanguages`) + Registry (StdRegProv üzerinden — WS-Man uyumlu): `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\DisplayVersion` | `display_version`'ın WMI'da karşılığı yoktur, bu yüzden `firmware.boot_mode` için kullanılan aynı StdRegProv yaklaşımıyla ayrıca okunur |
| `software[]` | Registry (StdRegProv üzerinden — WS-Man uyumlu): `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*`, `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*`, `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*` altındaki her alt anahtar (anahtarın kendi adı → `registry_key_name`; `DisplayName`, `DisplayVersion`, `Publisher`, `InstallDate`, `EstimatedSize`, `UninstallString`, `InstallLocation`, `SystemComponent`, `ParentKeyName`, `ParentDisplayName`) + WMI: `Win32_InstalledStoreProgram` (`Name`, `Version`, `Vendor`, `InstallDate`) | **`Win32_Product` kesinlikle kullanılmaz** — aşağıya bakınız. `Win32_InstalledStoreProgram` belgelenmemiş/gayriresmî bir sınıftır ve her Windows sürümünde bulunmayabilir; sorgu başarısız olursa `collection_errors`'a kayıt düşülür, hiçbir Store verisi uydurulmaz |
| `software_filtered_count` | — (Collector'ın kendi filtre mantığı) | `software[]`'e dahil edilirken elenen girdi sayısı — dış bir kaynak değil, Collector'ın kendi hesabı |
| `peripherals[]` | WMI: `Win32_PnPEntity` (`PNPClass` ∈ `{Keyboard, Mouse, HIDClass, Bluetooth, MEDIA, Printer, AudioEndpoint}` ile filtrelenmiş alt küme) | `devices[]` ile aynı kaynak/mantık, farklı sınıf filtresi — v0.1'de collector'ı henüz yazılmadı |
| `collection_errors[]` | — | Collector'ın kendi try/catch blokları; dış kaynak yok |

### Neden `Win32_Product` kullanılmıyor

`Win32_Product`, WMI sınıfı olarak listelenen **her** MSI paketini numaralandırırken arka planda her paket için bir **Windows Installer doğrulama/onarım (`MsiEnumProducts` + `MSICONFIGVALIDATE`, yani sessiz bir consistency-check/repair)** tetikler. Bunun sonuçları:

1. **Çok yavaştır** — her MSI paketi için ayrı bir doğrulama geçişi çalıştırır; onlarca yazılım kurulu bir makinede sorgu dakikalar sürebilir.
2. **Yan etkilidir** — bazı paketlerde bu "doğrulama" gerçek bir **onarım (repair)** işlemini tetikleyip dosyaları/registry anahtarlarını yeniden yazabilir; bu da Collector'ın "asla yorum yapmaz / asla değiştirmez, sadece okur" ilkesini doğrudan ihlal eder.
3. **Yalnızca MSI ile kurulmuş yazılımı görür** — EXE tabanlı yükleyicilerle (çoğu modern uygulama: tarayıcılar, IDE'ler, oyunlar) kurulan yazılımı kaçırır; envanter zaten eksik olurdu.
4. Bu davranış Microsoft tarafından resmen belgelenmiştir (KB2905638 / Win32_Product WMI class dokümantasyonu): *"Win32_Product... is not query optimized... triggers a consistency check... This behavior can cause a significant performance and reliability impact"*.

Bunun yerine registry `Uninstall` anahtarları **salt okunur** taranır, hiçbir Windows Installer API'si çağrılmaz; hem MSI hem EXE tabanlı çoğu kurulum bu anahtarları yazdığı için envanter de daha eksiksiz olur.

---

## Sürüm notları

- **v0.1 (revizyon 6)** — `software[].registry_key_name` ve `software[].install_location` eklendi. Gerekçe: `DisplayName` yükleyicinin çalıştığı Windows görüntüleme diline göre değişir — aynı program İngilizce bir Windows'ta bambaşka bir Türkçe/Almanca/Fransızca dizeyle görünebilir (`docs/schema/software-compatibility-v0.1.md`'de belgelenen gerçek bir örnek: "Visual Studio Build Tools" ↔ "Visual Studio Derleme Araçları"). `registry_key_name` (Uninstall alt anahtarının kendi adı) kurulum anında bir kez yazılır ve asla yeniden çevrilmez — Analyzer'ın artık kullanabildiği çok daha kararlı bir kimlik sinyali. `install_location` benzer bir gerekçeyle toplanıyor (yol adları da genelde çevrilmez) ama bu sürümde Analyzer tarafından henüz eşleştirmede kullanılmıyor.
- **v0.1** — İlk taslak. Alan kümesi bu proje için gerçek toplama koduna geçmeden önce dondurulmuştur; toplama sırasında eksik/yanlış çıkan varsayımlar `v0.2`'de netleştirilecektir.
- **v0.1 (revizyon)** — `system`/`firmware`/`cpu` toplama mantığı yazılırken ortaya çıkan düzeltmeler: `collection_errors[].section` → `collection_errors[].component` (kod ile şema tek isimde birleştirildi); `firmware.tpm.present` ve `storage.bitlocker_available` `bool` → `bool?` (okunamayan bir değer artık `false`'a düşmüyor); gizlilik varsayılanı tersine çevrildi (`system.hostname`/`system.serial_number` artık varsayılan olarak toplanmıyor, yalnızca `--include-identifiers` ile); `cpu.x86_64_feature_level` artık CPUID probing değil, `Win32_Processor` vendor+family/model'den kürasyonlu bir tablo ile türetiliyor; tüm string alanlar ortak bir normalizasyon adımından (trim + SMBIOS/OEM placeholder → `null`) geçiyor.
- **v0.1 (revizyon 2)** — `memory`, `os`, `storage` blokları ve `machine_id` uygulandı. Genel "hiçbir zaman sahte sentinel değer yok" ilkesi tüm sayısal/enum alanlara genişletildi (`memory.total_bytes`, `cpu.physical_cores`/`logical_processors`, `cpu.architecture`, `system.chassis_type`, `firmware.boot_mode`, `storage.disks[].*`, `gpu_topology.layout`) — hepsi artık nullable, "okunamadı" (`null`) ile "gerçek bir kod okundu ama tanınmadı" (enum'un kendi `Unknown`/`Other` üyesi, varsa) birbirinden ayrıldı. `firmware.boot_mode`'daki ayrı `Unknown` üyesi bu yüzden tamamen kaldırıldı (yerini `null` aldı). Yeni genel ilke: ayrıştırılan bir alanın ham hali `*_raw`'da korunur — ilk uygulaması `firmware.tpm.spec_version`/`manufacturer_version`/`spec_version_raw`. `memory.modules[].memory_type` ve `storage.disks[].bus_type` de aynı ruhla: tanınan kodlar etikete çevrilir, tanınmayanlar ham kod olarak kalır.
- **v0.1 (revizyon 5)** — `software[]` uygulandı (yalnızca envanter — Linux-muadili eşlemesi yok). Registry `Uninstall` anahtarları (HKLM native/WOW6432Node, HKCU) + `Win32_InstalledStoreProgram` (MSIX/Store) kaynak alındı. Yeni alanlar: `estimated_size_bytes`, `source`, `category`; `registry_view` artık `null` olabiliyor (registry-dışı kaynaklar için — "okunamadı" değil, kavram geçerli değil). Gürültü filtresi (boş ad, `SystemComponent=1`, alt bileşenler, Windows güncellemeleri/hotfix) Collector'da uygulanıyor ve elenen sayısı yeni, zorunlu olmayan `software_filtered_count` alanına yazılıyor — `collection_errors`'a değil, çünkü bu rutin bir filtreleme, bir toplama hatası değil.
- **v0.1 (revizyon 4)** — `storage.disks[].partitions[].free_bytes` eklendi (`MSFT_Volume.SizeRemaining`) — sistem geneli kısıtlamaların (bkz. Analyzer `VerdictEngine`) sistem diskinde kuruluma yetecek boş alan olup olmadığını değerlendirebilmesi için. `filesystem` ile aynı null kuralını izler: bağlı birim yoksa `null` (kavram geçerli değil), okunamadıysa da `null` (ayrım şu an yapılmıyor, ikisi de aynı sinyali paylaşıyor).
- **v0.1 (revizyon 3)** — `devices` ve `gpu_topology` uygulandı; ikisi de kritik blok olarak ele alındı. `devices[]` artık yapılandırılabilir bir `PNPClass` izin listesiyle filtreleniyor (sabit değil, kolayca genişletilebilir); eşleştirme anahtarı `hardware_id` string'inden `vendor_id`+`device_id` hex çiftine kaydırıldı (bkz. "Tasarım ilkeleri"), ham `HardwareID[0]` `hardware_id_raw`'da korunuyor, kalan adaylar `compatible_ids`'e taşındı, `status` `Win32_PnPEntity.Status` yerine `ConfigManagerErrorCode`'dan türetiliyor. `gpu_topology` artık ayrı bir WMI sorgusu yapmıyor, tamamen `devices[]`'in `Display` sınıfı girdilerinden türetiliyor; `layout` sezgisel bir entegre/ayrık sınıflandırmasına dayanıyor ve belirsiz durumda `null` kalıyor (bilinen kör noktalarıyla birlikte belgelenmiştir). İki hızlı düzeltme de bu turda yapıldı: `memory.modules[].memory_type`, güvenilmez `MemoryType` yerine `SMBIOSMemoryType`'tan okunuyor ve artık tanınmayan bir kodu asla çözümlenmiş gibi göstermiyor (yalnızca `memory_type_raw`'da kalıyor); `speed_mhz` belirsizliği `rated_speed_mhz`/`configured_speed_mhz` olarak ikiye ayrıldı.
