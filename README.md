# Scout

**Find out whether your PC can run Linux — before you touch the disk.**

Scout scans a Windows machine and answers one question: if you switched this computer to Linux, what would work, what would need extra steps, and what would break?

It reads. It does not write. No partitioning, no bootloader changes, no installer. The output is a single HTML file you can read, email, or hand to whoever asked.

```
> Scout.Collector.exe --report report.html

Verdict: ready
12 devices examined, 10 supported natively.
Report written to report.html
```

The report is currently rendered in Turkish; English is planned. All user-facing strings live in one file, so adding a language is a translation task rather than a code change.

## Why

Windows 10 reached end of support in October 2025. A large number of machines that still work fine cannot install Windows 11 — no TPM 2.0, a CPU that is not on Microsoft's supported list, or firmware that cannot do UEFI Secure Boot.

For those machines Linux is a real option, but finding out whether it is a *good* option currently means hours of reading forum threads about one specific Wi-Fi chipset. Scout does that lookup for you, against the actual hardware in the machine rather than the model name on the box.

## What it reports

**The verdict** — one sentence at the top, colour-coded. Ready, minor issues, needs attention, blocked, or not enough data.

**What needs attention** — only the hardware that does not work out of the box, in plain language. Not "iwlwifi requires firmware" but "your Wi-Fi card works, but you may need a wired connection during installation."

**The technical breakdown** — collapsed by default. Every relevant device with its PCI/USB vendor and device ID, the Linux kernel driver that claims it, and how the match was made.

**System summary** — CPU, microarchitecture level, RAM, storage, boot mode, TPM.

## How matching works

Device names are unreliable. The string `Intel(R) Wireless-AC 9560` changes with driver version and system language; `PCI\VEN_8086&DEV_9DF0` does not. Every compatibility lookup goes through the vendor and device ID pair, never the friendly name.

Matching happens in four stages, most specific first:

1. **Exact** — this vendor and this device ID.
2. **Range** — this vendor, device ID within a known range. Needed because a single vendor rule is often wrong: AMD cards in the `6780`–`683F` range default to the `radeon` driver, while everything newer uses `amdgpu`.
3. **Vendor-wide, class-scoped** — this vendor, this device class. Intel ships GPUs, Wi-Fi cards, USB controllers and storage controllers under one vendor ID, so class scoping keeps an Intel Wi-Fi card from being matched to `i915`.
4. **None** — reported as unknown.

Scout does not guess. A device it has no entry for is reported as unknown, and unknown is never rendered as "works."

## The collector never interprets

The scan produces a JSON machine profile. Deciding what that profile means is the analyzer's job, and the two are deliberately separate:

- A profile collected today can be re-analyzed later against an updated compatibility database.
- The analyzer can be tested against synthetic profiles without needing the physical hardware.
- "Could not read" and "not present" stay distinguishable. A TPM query that fails with access denied yields `null`, not `false` — a machine with an unreadable TPM must not be reported as a machine without one.

Raw values are never discarded when they are parsed. `SpecVersion` arrives as `2.0, 0, 1.38`; the profile stores the parsed `spec_version` and `manufacturer_version` alongside the original in `spec_version_raw`.

## Privacy

Hostname and serial numbers are **not** collected unless you pass `--include-identifiers`. The machine identifier in the profile is a SHA-256 digest of the machine GUID; the raw GUID never reaches the output. Fields that were deliberately skipped are listed in the profile so it is clear what is missing and why.

Nothing is uploaded. Scout makes no network connections.

## Usage

```
Scout.Collector.exe                              # print profile as JSON to stdout
Scout.Collector.exe --output profile.json        # write the profile to a file
Scout.Collector.exe --report report.html         # scan, analyze, write an HTML report
Scout.Collector.exe --include-identifiers        # include hostname and serial numbers
```

Run elevated to read TPM state and BitLocker status. Without elevation those fields come back as unknown rather than wrong.

Analyzing a profile collected elsewhere:

```
Scout.Reporter.exe profile.json --output report.html
```

## Building

Requires the .NET 8 SDK.

```
git clone https://github.com/MrrKamall/winlin-scout.git
cd winlin-scout
dotnet build
dotnet test
```

## Project layout

| Project | Role |
|---|---|
| `Scout.Core` | Profile models and JSON serialization |
| `Scout.Collector` | Reads the machine, writes a profile |
| `Scout.Analyzer` | Matches devices against the compatibility database, produces a verdict |
| `Scout.Reporter` | Renders an analysis as a single HTML file |

Documentation lives in `docs/schema/`. Sample profiles and their reports are in `docs/samples/`, including synthetic profiles for hardware we do not own — hybrid Nvidia graphics, Broadcom Wi-Fi, pre-GCN AMD cards.

## Contributing hardware data

The most useful contribution is a machine profile from hardware we have never seen:

```
Scout.Collector.exe --output my-profile.json
```

The profile contains no identifying information by default. Open an issue and attach it.

Compatibility entries live in `data/hardware-compatibility.json`. If Scout reports one of your devices incorrectly, that file is where the fix goes — the format is documented in `docs/schema/hardware-compatibility-v0.1.md`.

## Status

Early. The collector is working and the analyzer produces reports. Installed-software inventory and its Linux-equivalent mapping (`data/software-compatibility.json`) are implemented, but both compatibility databases are still small starter sets. Treat the verdict as a strong hint, not a guarantee.

## License

GPL-3.0. See [LICENSE](LICENSE).
