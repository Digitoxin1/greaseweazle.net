# Layered Parity Findings (Layer 0 - Layer 8)

This document aggregates the per-layer behavioural parity scan results for every Python/C symbol against its VB.NET counterpart. Each finding is tagged:

- **FIX** = real divergence to address in this pass
- **DEFER-ARCH** = intentional architectural difference (VB.NET .NET conventions vs Python idioms); document only
- **DEFER-COSMETIC** = labelling/print-only difference, not behavioural
- **CLARIFIED** = closer reading shows behaviours are equivalent

Status legend: H = HIGH, M = MEDIUM, L = LOW, C = COSMETIC.

## Layer 0 — `error.py`

CLEAN: `Fatal` / `FatalException` / `Check` all parity-aligned.

## Layer 1 — `flux.py` + `optimised/*`

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| H | `Flux.__init__` | VB copies index/flux lists; Python aliases. | **DEFER-ARCH** (VB immutability is desirable; no fixture asserts caller mutation) |
| M | `decode_flux:read_28bit` | C uses `p[0] >> 1`; VB uses `(b & 0xFE) >> 1`. | **CLARIFIED** — extra mask is defensive; bit 0 of first byte is always 0 by encoder design. Add comment. |
| M | `decode_flux` exceptions | C `ValueError` vs VB `FatalException`. | **DEFER-ARCH** |
| M | `Flux.reverse` | `assert` -> `AssertionError` vs `InvalidOperationException`. | **DEFER-ARCH** |
| M | `c64_gcr decode/encode` | Python rejects when `len % 5 != 0` / `len % 4 != 0`. | **FIX** — add modulus guards |
| M | `mac_sector decode/encode` | Python rejects too-short input. | **FIX** — add length guards |
| L | `Flux.ticks_per_rev` getter | bare except vs Catch. | DEFER-COSMETIC |
| L | `optimised/__init__.py` `GW_OPT` | No VB analogue. | DEFER-ARCH |

## Layer 2 — `track.py`

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| **H** | `MasterTrack._flux` (negative `splice`) | Python `sum(bit_ticks[:splice])` for negative splice yields prefix-sum-minus-tail; VB `Take(Splice)` returns 0 elements. | **FIX** — implement Python-equivalent slice |
| M | `MasterTrack._flux` weak-range bounds | Python `assert 0 <= s < e <= bitlen`; VB silently corrupts. | **FIX** — add validation |
| M | `MasterTrack._flux` writeout assert | Python `assert for_writeout` on read-style fall-through. | **FIX** — add Debug.Assert |
| M | `PLLTrack.import_flux_data` fallback | Python falls back to managed `flux_to_bitcells` if optimised unavailable. | DEFER-ARCH (VB always uses managed) |
| L | `PLL.__init__` exception | `ValueError` vs `ArgumentException`. | DEFER-ARCH |

## Layer 3 — `usb.py`

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| H | `Unit.__init__` | Python ctor reads firmware too; VB requires explicit ReadFirmwareInfo+ApplyFirmwareInfo. | DEFER-ARCH (well-documented in callers) |
| M | `Unit.__init__` Reset Try/Catch | VB swallows; Python propagates. | **FIX** — let exceptions propagate |
| L/M | `_decode_flux` truncation | VB matches strict C path, not lenient Python fallback. | DEFER-ARCH (matches C semantics) |
| C | `CmdError` ctor / various string tables | Cosmetic. | DEFER-COSMETIC |

## Layer 4 — `tools/util.py` + `list_ports_windows.py`

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| Critical | Port discovery (CfgMgr32+IOCTL stack) | Stub. | DEFER-ARCH (uses .NET SerialPort) |
| Critical | `get_image_class` returns string vs class | VB string-keyed registry. | DEFER-ARCH (consistent across VB) |
| H | `score_port`/`find_port` enumeration timing | VB caches single snapshot. | DEFER-ARCH |
| **M** | `with_drive_selected` KeyboardInterrupt | Python prints empty line then resets and re-raises. | **FIX** — add newline emit |
| M | Exception types (argparse vs ArgumentException, SerialException vs IOException) | Architectural. | DEFER-ARCH |
| L | `columnify` empty input | Python raises; VB returns empty string. | DEFER-COSMETIC |
| L | `TrackSet.ch_to_pch` `c // -step` | Floor vs truncation for negative cyl. | **FIX** — use FloorDiv |
| L | `period` parsing strictness | VB EndsWith vs Python re.match. | **FIX** — tighten parsing |

## Layer 5 — IBM codec + base

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| **H** | `DiskDef_File` Outer parser | Python rejects non-disk/non-import lines; VB silently skips. | **FIX** — emit syntax error |
| **H** | `IBMTrack_Scan.BEST_GUESS` | Updated only after winning track in Python; VB updates inside probes. | **FIX** — assign once after best wins |
| **H** | `IBMTrack_Scan.decode_flux` re-call | Python decodes additional flux into existing track; VB has no merge path. | **FIX** — implement merge path |
| **M-H** | `IBMTrack.master_track` -> `MasterTrack.Verify = self` | Not wired in VB. | **FIX** — wire Verify across all IBM master_track functions |
| M | `IBMTrack_FixedDef.add_param` radix | `int(val, base=0)` not supported in VB. | **FIX** — accept `0x`, `0o`, `0b`, decimal |
| M | `IBMTrack_FixedDef.finalise` early return | VB returns when sector_count=0, skipping iam/gap1 checks. | **FIX** — match Python guard order |
| M | `IBMTrack_Fixed.decode_flux` raw+merge | Python populates raw track first, then merges. | DEFER-ARCH (covered by Scan path) |
| M | `IBMTrack.verify_track` tolerant region compare | VB does shorter check. | **FIX** — extend per-Sector equality with TrackArea.delta |
| L | `_get_diskdef` exception wrapping | Loses original type. | DEFER-COSMETIC |
| C | `Codec`/`HasFlux` coupling | Architectural. | DEFER-ARCH |

## Layer 5 — Other codecs (Amiga / Apple2 / C64 / Mac / DG / HP / Micropolis / NorthStar)

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| H | optimised.enabled gates | Python errors if C ext absent; VB always has managed impl. | DEFER-ARCH |
| **H** | `HPMMFM.decode_flux` slip search | VB has ±64 bit slip; Python decodes at exact offset. | **FIX** — remove slip for parity |
| **M** | `master_track.verify = self` | Missing across Amiga, Apple2, C64, DG, HP, Mac, Micropolis, NorthStar. | **FIX** — wire Verify |
| M | `master_track` padding | Python may throw on negative pad; VB silently no-ops. | **FIX** — match Python (allow only nonneg) |
| **M** | `C64GCR.set_disk_id` | Python asserts not already set; VB allows overwrite. | **FIX** — add check |
| M | `MacGCRDef.add_param` radix | `int(val, base=0)` not supported. | **FIX** — same as IBM |
| M | Decode-flux print warnings | Python prints; VB silent. | DEFER-COSMETIC |
| L | `NorthStarDef.add_param` case-sensitivity | Python exact `'fm'`/`'mfm'`; VB case-insensitive. | **FIX** — make ordinal |

## Layer 6 — Flux images (HFE / SCP / KryoFlux / A2R / CAPS)

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| **H** | `KryoFlux.emit_track` | Python uses `accumulate` (prefix sum) for index timing; VB uses raw values. | **FIX** — prefix-sum index list |
| **H** | `SCP.emit_track` | Python honours `revs` and codec branching; VB uses fixed Flux(2). | **FIX** — port revs/codec/MasterTrack branches |
| **H** | `SCP.get_image` (WRSP splice) | Python scales splice by `factor`; VB uses raw. | **FIX** — scale splice |
| **H** | `SCP.from_bytes` recovery logic | Missing TLUT truncation, dummy TDH skip, first-rev clip, empty trailing rev strip, C64 half-track / single-sided remap. | **FIX** — port all branches |
| **M** | `IPF.get_track` sector sort | Python sorts by start before splice/gap calculation. | **FIX** — sort sectors |
| M | `hfev3 rate clamp` | Python writes raw byte; VB clamps 0-255. | DEFER-ARCH (Python implicit truncation matches VB clamp on legal values) |
| L | `HFETrack.from_hfe_bytes` | VB legacy func diverges from `DecodeHfeBits`; appears unused. | **FIX** — align or remove |
| L | `SCP` footer `creation_time`/`app_name` | Hard-coded vs Python live values. | DEFER-COSMETIC |
| L | `SCP` checksum validation | Python warns; VB skips. | **FIX** — add warning |

## Layer 6 — Sector images (IMG/D64/D88/DCP/DIM/DMK/FDI/IMD/MSA/NFD/NSI/TD0/EDSK/Apridisk)

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| H | `Image.__exit__` KeyboardInterrupt | VB uses `KeyboardInterruptException` not surfaced by .NET hosts. | DEFER-ARCH |
| **M** | `ImageOpts._set` case-sensitivity | Python case-sensitive option keys; VB ignore case. | **FIX** — use ordinal compare |
| **M** | `Image.to_file` format constructor | Python passes fmt to ctor; VB uses reflection AssignFormat. | DEFER-ARCH (functional outcome equivalent) |
| L | `IMG.__init__` fmt None check | Python errors; VB defers. | DEFER-COSMETIC |
| **H** | `IMG_AutoFormat.from_file` | Stubbed in VB. | **FIX** — implement format-from-file flow |
| **H** | `DIM.format_from_file` | VB always returns `ibm.1440` instead of parsing magic + media_byte. | **FIX** — port pc98.2hd/2hs detection |
| M | `DIM.from_bytes` track ordering | Python uses `TrackIter` physical; VB uses logical. | **FIX** — use physical iteration |
| **H** | `DCP.format_from_file` | VB returns `pc98.1232` (does not exist); Python returns `pc98.2hd`. | **FIX** — return correct name |
| M | `DCP.from_bytes` sides_swapped | VB ignores. | **FIX** — XOR head |
| H | `D88` read-only | VB implements full write paths; Python is read-only. | DEFER-ARCH (extension acceptable) |
| **H** | `D88.remove_duplicate_sectors` | Always dedup in VB; Python only when oversized. | **FIX** — gate on oversize |
| **H** | `D88` track-table extension | Python reads 4 extra dwords when `s_off==688`; VB always 160 entries. | **FIX** — port extension |
| M | `DMK.fm_off` clamp | VB clamps `work` to data.Length-1 prematurely. | **FIX** — match Python |
| M | `DMK.from_bytes` FM DAM search | Loop `dam` placement differs. | **FIX** — match Python loop semantics |
| L | `FDI.from_bytes` track_list | VB uses cylinder-major; Python uses sequential helper. | DEFER-COSMETIC (default sequential=False both sides) |
| **H** | `IMD.emit_track` scan unwrap | Python accepts IBMTrack_Scan/Empty; VB only IbmTrackFixed. | **FIX** — unwrap scan + skip empty |
| **H** | `IMD.get_image` rmap/cmap/hmap + rec flags | Not implemented. | **FIX** — port writer |
| M | `MSA` skew | Python sets cskew=4/hskew=2 for spt<=9. | **FIX** — pass skew |
| **H** | `TD0.from_bytes` CRC checks | Three crc-16-teledisk regions skipped. | **FIX** — add CRC validation |
| **H** | `TD0.from_bytes` DDAM | `flags & 4` -> `Mark.DDAM` not applied. | **FIX** — apply DDAM |
| M | `TD0` advanced compression gate | Python errors if optimised unavailable. | DEFER-ARCH |
| H | `EDSK` weak-bits/special tracks/write | Stubs only. | **FIX** — port full builder + writer |
| **H** | `Apridisk` SecType validation | VB silently skips unknown; Python raises Fatal. | **FIX** — add validation |
| M | `Apridisk.expand_record` slicing | Off-by-one possible if header_size != record_size - payload. | **FIX** — slice exactly `data[header_size:record_size]` |

## Layer 7 — Tools (read/write/info/seek/...)

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| **H** | `reset` no-delays-restore | Python re-applies delay RAM after `power_on_reset`. | **FIX** — restore delays after reset |
| H | `info --live` output | VB emits stub lines only. | **FIX** — port full info output |
| H | `delays` display unconditional | Python gates Pre-Write/Post-Write/Index Mask on FW param size. | **FIX** — conditional display |
| **H** | `seek` Yes/No prompt | Python prompts for extreme cyl; VB skips. | **FIX** — implement prompt |
| **H** | `pin get` print gating | Python always prints; VB only with --live. | **FIX** — always print |
| H | `read --format` default revs | Python pulls fmt_cls.default_revs; VB uses fixed 3. | **FIX** — apply fmt default_revs |
| M | `align --format` defaults | Same as read for revs. | **FIX** — apply fmt default_revs |
| M | `align.align_track` first decode | VB uses MkTrack vs Python `decode_flux` entry. | **FIX** — use codec.DecodeFlux for first pass |
| M | `bandwidth` strict-vs-soft on error | VB throws; Python prints+returns. | **FIX** — soft-fail |
| M | `clean` preview lines | VB prints PassLines unconditionally. | **FIX** — gate on dry-run |
| M | `pin usage` exit code | Python exits 1; VB returns 0. | **FIX** — return 1 |

## Layer 8 — `cli`

| Severity | Symbol | Finding | Disposition |
|----------|--------|---------|-------------|
| **H** | `KeyboardInterrupt` handling | Python prints nothing, exits 1; VB shows FATAL banner. | **FIX** — silent exit on KeyboardInterrupt |
| **H** | Exception pass-through | Python re-raises programmer errors (`IndexError`, `AssertionError`, `TypeError`, `KeyError`, `struct.error`) even without --bt. | **FIX** — rethrow these CLR types |
| M | Subcommand case-sensitivity | Python rejects `Read`; VB accepts. | **FIX** — ordinal compare |
| M | Pre-release version banner | `+` in version triggers banner; VB has none. | **FIX** — add banner |
| L | `--time` start timing | VB starts at Main entry; Python starts at flag consumption. | **FIX** — set start when --time consumed |
| L | `gw <action> -h` short-circuit | VB returns synthesized usage; Python delegates to argparse. | DEFER-ARCH |
| C | stderr line-buffering | Python explicit; VB implicit. | DEFER-COSMETIC |

## Aggregate Totals

- **Items to FIX**: ~50 across layers (HIGH ~25, MEDIUM ~20, LOW ~5)
- **DEFER-ARCH**: ~15 (Python C-extension gates, VB SerialPort vs ctypes, exception type families)
- **DEFER-COSMETIC**: ~10 (print/log strings, footer metadata)
- **CLARIFIED**: 1 (`read_28bit` defensive mask)
