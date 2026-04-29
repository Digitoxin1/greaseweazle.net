# Runtime / Integration Test Checklist

Tracks runtime-parity testing of `gw-vb.exe` against the Python reference
`gw.exe` v1.23 (`H:\gw\gw.exe`). The static symbol-mapping checklist lives
separately in [`parity-checklist.md`](parity-checklist.md); this file only
covers behavioural / output-parity testing.

Status legend:
- `[x]` — done & verified parity (or known-acceptable divergence documented)
- `[~]` — partial coverage; further test cases pending
- `[ ]` — not yet exercised

Hardware available for runtime tests:
- Drive 0 — 360K (5.25"), max safe range `--tracks=c=0-40`
- Drive 1 — 1.44M (3.5"), max safe range `--tracks=c=0-82`
- Drive 2 — 1.2M (5.25"), max safe range `--tracks=c=0-82`
- Test images: `H:\gw\images\*` (1440K.hfe, 1440K.ima, 1440K.scp, etc.)
- **Off-limits**: firmware update flashing.

---

## 1. Project structure

- [x] Library / CLI split — DLL is text-free, CLI does all argv parsing & rendering.
- [x] Strongly-typed `XOptions` per action; events for streaming, summaries for one-shots.
- [x] `ISeekPrompter` for interactive flows; `ConsoleSeekPrompter` in CLI.
- [x] `LibraryDiagnostics.MessageEmitted` plumbed for Python-style info/warning lines.
- [x] Typed exceptions (`UnrecognisedSuffixException`, `UnknownFormatException`,
  `Track0SeekMismatchException`, `CapsLibraryNotFoundException`,
  `DeviceInUpdateModeException`, `ArgparseException`, …) carrying structured data.

## 2. Argparse parity (CLI argv contract)

All argparse-contract failures use `ArgparseException` → exit code 2 +
two-line `usage:` / `<bin> action: error: ...` block on stderr (matches
Python argparse).

- [x] **Centralised** in `ParserHelpers.Argparse(action, message)`.
- [x] Per-action `usage:` banner table verified against `gw.exe <action> --nope`.
- [x] `convert` parser — missing positional, `--nope`, `-Z`, `--tracks=cyl=abc`,
  `--tracks=` (empty), `--out-tracks=`, bad `--pll`, bad `--densel`/`--dd`,
  inline-value on flag-only args.
- [x] `read` parser — missing positional, unknown args, `--tracks` parsing,
  `--hard-sectors`/`--fake-index` mutex, `--gen-tg43`/`--densel` mutex,
  `--pll`/`--densel`/`--dd` `ArgumentException` → argparse, `--drive` parsing.
- [x] `write` parser — same shape as `read` plus `--precomp`.
- [x] `info` parser — flag-only inline-value rejection, unknown args.
- [x] `erase` parser — `--tracks` parsing, signed-int `--revs` handling.
- [x] `seek` parser — missing/extra cylinder, non-numeric cylinder
  (`argument cylinder: invalid x value: '<v>'` lambda-quirk match).
- [x] `clean` parser — unknown args, flag-only inline-value rejection.
- [x] `delays` parser — value-bearing options table, unknown args.
- [x] `update` parser — `--file` / `--tag` exclusion still runs at parse time.
- [x] `pin` parser — **subcommand-aware** (`pin set` and `pin get` get their
  own `usage:` banner per Python argparse subparsers). Bad pin/level use the
  `argument pin: invalid x value: '<v>'` / `argument level: invalid pin level: '<v>'`
  wording verbatim.
- [x] `reset` parser.
- [x] `bandwidth` parser.
- [x] `rpm` parser.
- [x] `align` parser — `--tracks` required check switched to argparse-style.
- [x] Spot-check: 17 cross-action argparse cases all match Python byte-for-byte
  (modulo intentional `gw.exe`↔`gw-vb` binary-name swap).

## 3. Malformed-input parity sweep

Driver: `H:\gw\malformed\run.ps1`. Latest log:
`H:\gw\malformed\logs\run-20260429-020129.txt`. Result:
**Total: 28 — OK: 21 — Expected: 7 — Diverged: 0**.

### Group A — corrupted input files
- [x] A1 `convert <missing>.img out.hfe` — "Sector image requires a disk format to be specified".
- [x] A2 `convert empty.hfe out.img` — VB clean error; Python emits raw `struct.error` traceback (Python regression).
- [x] A3 `convert empty.scp out.img` — same Python regression.
- [x] A4 `convert truncated.hfe out.img` — same Python regression.
- [x] A5 `convert bad_magic.hfe out.img`.
- [x] A6 `convert --format ibm.1440 wrong_size.img out.hfe` — `HFE: Data bitrate detected: 500 kbit/s` line restored.
- [x] A7 `convert bad_magic.td0 out.img`.
- [x] A8 `convert garbage.scp out.img`.
- [x] A9 `convert --format ibm.1440 garbage.dsk out.img`.

### Group B — bad output paths
- [x] B1 `convert in.hfe missing-dir/out.img` — surfaces directory error after the "Converting …" header (Python order).
- [x] B2 `convert in.hfe <directory>` — extra `.2d` suffix in VB known-format list (intentional, unreleased upstream).

### Group C — argv contract
- [x] C1 `convert` (no args) — exit 2.
- [x] C2 `convert in.hfe` (one arg) — exit 2.
- [x] C3 `frobnicate` — VB lists `align` action (intentional, unreleased upstream).
- [x] C4 `convert --tracks=cyl=abc …` — exit 2, exact wording.
- [x] C5 `convert --tracks=c=0-99999 …` — out-of-range track surfaces as runtime error (exit 1).
- [x] C6 `convert --tracks= …` (empty) — exit 2.
- [x] C7 `convert --nope …` — exit 2.
- [x] C8 `convert -Z …` — exit 2.
- [x] C9 `convert --format no.such.format …` — VB lists extra forward-ported formats (intentional).
- [x] C10 `--help` — VB shows `align` action (intentional).

### Group D — image-option (`::opt=val`) parsing
- [x] D1 `convert in.hfe out.hfe::flubber=1` — fast-fail via `ImageBase.ValidateOptions()` hook.
- [x] D2 `convert in.hfe out.hfe::bitrate=` — eager validation in `HFEImage.ValidateOptions`.
- [x] D3 `convert in.hfe out.hfe::bitrate=fast` — eager validation.
- [x] D4 `convert in.hfe out.hfe::bitrate` — eager validation.
- [x] D5 `convert in.hfe out.hfe::` — empty options dict, completes successfully.

### Group E — `--no-clobber`
- [x] E1 — `--no-clobber existing.img` with format error: format-error fires
  before clobber check (Python's `convert.py` order).

### Group F — CAPS / IPF
- [x] F1 — `convert garbage.ipf out.img` with `CAPSImg.dll` present:
  both binaries emit `CAPS: IPF: Could not open image '…'` and exit 1.
  Driver auto-skips when the DLL is absent so CI on bare hosts isn't
  poisoned by a `CapsLibraryNotFoundException` divergence.

## 4. Runtime hardware tests (per session summary)

These were exercised against live drives in the prior session.

### General / device-state commands (no media required)
- [x] `info` — Host Tools, CLI, Device block (port, model, firmware versions).
- [x] `bandwidth` — USB bandwidth measurement.
- [x] `reset`.
- [x] `delays` (read-only).
- [x] `delays --select 100` (modify-then-read round-trip).

### Per-drive commands
- [x] Drive 0 — `rpm`, `read --drive 0 --tracks=c=0-40 --format=ibm.180 *.img`,
  `write --drive 0 *.img`, `erase --drive 0 --tracks=c=0-40`, `seek --drive 0 0`.
- [x] Drive 1 — `rpm`, `read --drive 1 --tracks=c=0-82 --format=ibm.1440 *.{img,hfe,scp,raw}`,
  `write --drive 1 *.{img,hfe,scp}`, `erase --drive 1 --tracks=c=0-82`,
  `seek --drive 1 0` / extreme cylinder confirm flow.
- [x] Drive 2 — `rpm`, `read --drive 2 --tracks=c=0-82 --format=ibm.1200 *.img`,
  `write --drive 2 *.img`, `erase --drive 2 --tracks=c=0-82`.
- [x] `convert` (file-only, no drive) — `.img↔.hfe`, `.hfe↔.scp`, raw KryoFlux folder I/O.
- [x] `pin get 2` — read pin level.
- [x] `pin set 2 H` / `pin set 2 L` — write pin level.

### Drive-error parity matrix

Setup: Drive 1 = 1.44M with **write-protected** disk; Drive 2 = 1.2M
with **no disk**. Every row below produces byte-for-byte identical
stdout + stderr (after binary-name + CRLF normalisation) and matching
exit code between `gw.exe` v1.23 and `gw-vb`. Logs: `H:\gw\out\drive-errors\`.

| # | Setup                       | Command                                                                 | Exit | Failure surface (stdout)                                |
|---|-----------------------------|-------------------------------------------------------------------------|------|---------------------------------------------------------|
| A1| Drive 1 (write-protected)   | `erase --drive 1 --tracks c=0`                                          | 0    | `Command Failed: EraseFlux: Disk is Write Protected`    |
| A2| Drive 1 (write-protected)   | `write --drive 1 --format=ibm.1440 --tracks c=0 1440K.ima`              | 0    | `T0.0: Writing Track …` then `Command Failed: WriteFlux: Disk is Write Protected` |
| B1| Drive 2 (no disk)           | `rpm --drive 2`                                                         | 0    | `Command Failed: GetFluxStatus: No Index`               |
| B2| Drive 2 (no disk)           | `read --drive 2 --tracks c=0 --format=ibm.1200 …`                       | 0    | `Command Failed: GetFluxStatus: No Index`               |
| B3| Drive 2 (no disk)           | `write --drive 2 --format=ibm.1200 --tracks c=0 1200K.ima`              | 0    | `Command Failed: GetFluxStatus: No Index`               |
| B4| Drive 2 (no disk)           | `erase --drive 2 --tracks c=0`                                          | 0    | `Command Failed: GetFluxStatus: No Index`               |
| B5| Drive 2 (no disk)           | `seek --drive 2 0`                                                      | 0    | (no error — seek-to-0 is just step pulses; matches Python) |

Note: Python's gw.exe exits **0** on a recovered `CmdError` (write
protect / index timeout / etc.). VB matches this exit-code contract
exactly, even though intuitively a hardware failure looks like it
should be exit 1. The `** FATAL ERROR:` family is reserved for
non-`CmdError` exceptions and does exit 1 (e.g. malformed sector image
without `--format`, both binaries verified).

#### Drive-off (powered-down drive)

Setup: all three drives **powered off** (Greaseweazle still connected
via USB). Same harness as above. 12/12 byte-equal between `gw.exe` and
`gw-vb` on stdout + stderr + exit code. Two distinct surfaces emerge
depending on whether the action pre-seeks Track 0 or jumps straight
into a flux operation:

| #  | Drive (off) | Command                                                | Exit | Failure surface                                                          |
|----|-------------|--------------------------------------------------------|------|--------------------------------------------------------------------------|
| C0 | 0 (360K)    | `rpm --drive 0`                                        | 0    | `Command Failed: GetFluxStatus: No Index` (~3s)                          |
| C1 | 0           | `read --drive 0 --tracks c=0 --format=ibm.180 …`       | 1    | `** FATAL ERROR: Track0 signal absent after seek to cylinder 0` (~1s)    |
| C2 | 0           | `erase --drive 0 --tracks c=0`                         | 0    | `Command Failed: GetFluxStatus: No Index`                                |
| C3 | 0           | `seek --drive 0 0`                                     | 1    | `** FATAL ERROR: Track0 signal absent after seek to cylinder 0` (<1s)    |
| D0 | 1 (1.44M)   | `rpm --drive 1`                                        | 0    | `Command Failed: GetFluxStatus: No Index`                                |
| D1 | 1           | `read --drive 1 --tracks c=0 --format=ibm.1440 …`      | 1    | `** FATAL ERROR: Track0 signal absent after seek to cylinder 0`          |
| D2 | 1           | `write --drive 1 --format=ibm.1440 --tracks c=0 …`     | 0    | `Command Failed: GetFluxStatus: No Index`                                |
| D3 | 1           | `erase --drive 1 --tracks c=0`                         | 0    | `Command Failed: GetFluxStatus: No Index`                                |
| D4 | 1           | `seek --drive 1 0`                                     | 1    | `** FATAL ERROR: Track0 signal absent after seek to cylinder 0`          |
| E0 | 2 (1.2M)    | `rpm --drive 2`                                        | 0    | `Command Failed: GetFluxStatus: No Index`                                |
| E1 | 2           | `read --drive 2 --tracks c=0 --format=ibm.1200 …`      | 1    | `** FATAL ERROR: Track0 signal absent after seek to cylinder 0`          |
| E2 | 2           | `seek --drive 2 0`                                     | 1    | `** FATAL ERROR: Track0 signal absent after seek to cylinder 0`          |

Two surfaces, two exit-code contracts:

- **`Track0` FATAL (exit 1)** — actions that pre-seek and verify the
  Track 0 sensor (`read`, `seek`). Fires fast (<1.3s) because the
  firmware checks the Track 0 signal immediately after issuing step
  pulses. Drive-off and "drive cabled but unresponsive" both surface
  here.
- **`GetFluxStatus: No Index` `CmdError` (exit 0)** — actions that
  start a flux read/write right away (`rpm`, `erase`, `write`). Takes
  ~3s because the firmware waits one revolution's worth of timeout
  for an index pulse before giving up.

Logs: `H:\gw\out\drive-off\<tag>.{py,vb}.{out,err}`.

#### Mechanical-limit (40-track drive, head over-stepped)

Setup: Drive 0 = 360K (5.25") **powered on with disk loaded**. Same
harness, with per-read flux counters normalised because each physical
read produces slightly different `(NNN flux in NN.NNms)` jitter. 3/3
byte-equal between `gw.exe` v1.23 and `gw-vb` on stdout + stderr +
exit code.

| #  | Command                                                         | Exit | Surface                                                                 |
|----|-----------------------------------------------------------------|------|-------------------------------------------------------------------------|
| F1 | `seek --drive 0 79`                                             | 0    | (silent) — firmware steps out 79 times; mechanical stop absorbs steps 41-79 |
| F2 | `seek --drive 0 0` immediately after F1                         | 0    | (silent) — firmware re-zeroes via Track 0 sensor; no mismatch surfaced  |
| F3 | `read --drive 0 --tracks c=79 --format=ibm.180 …`               | 0    | `T79.0: WARNING: Out of range for format 'ibm.180': No format conversion applied: Raw Flux (NNN flux in NN.NNms)` (stderr) |

Important finding: **`Track0SeekMismatchException` does not actually
fire on past-limit seek** in `gw.exe` v1.23. The Greaseweazle firmware
silently absorbs over-stepping (the stepper just clicks against the
mechanical stop), and re-zeroing on the way back is driven by the
Track 0 sensor, not by step-count accounting. So the only paths that
trigger `Track0` errors are the ones we already characterised in the
drive-off matrix (no Track 0 sensor signal at all). VB matches Python
on this behavioural quirk byte-for-byte.

Logs: `H:\gw\out\drive-mech-limit\<tag>.{py,vb}.{out,err}`.

This closes out the §8 drive-error wish-list. The only remaining
hardware-specific scenarios (write-protect surfacing mid-batch,
USB-disconnect mid-operation, drive-ID-not-cabled) are intrinsically
racy / hard to script reliably and are deferred unless they surface
during normal use.

### Targeted bug fixes verified at runtime
- [x] **`--gen-tg43` ordering** — option ordering vs density-select.
- [x] **Sparse KryoFlux head-bang** — `write` against a `.raw` set with 41
  tracks no longer head-bangs after track 41.
- [x] **`convert ::bitrate=N`** — recognised on output side for HFE.
- [x] **HFE input requires `--format`** — error raised correctly without
  needing a disk format on the input side.
- [x] **CAPS/IPF P/Invoke `EntryPoint`** — every `<DllImport>` in
  `CAPSImage.vb` was missing an `EntryPoint`, so .NET tried to resolve
  `CAPSInitNative` etc. The DLL exports `CAPSInit`, `CAPSAddImage`, …
  (no suffix). Added `EntryPoint:="CAPS<Name>"` to every binding so the
  CAPS path now actually loads on Windows hosts that have `CAPSImg.dll`
  next to `gw-vb.exe` (or on `PATH`). Verified end-to-end via the new
  `F1.malformed-ipf-input` sweep case.

## 5. Performance

- [x] **HFE `FromBytes` quadratic LINQ** — `data.Skip(d).Take(n)` inside the
  per-chunk loop made HFE input parsing O(N²). Replaced with `Array.Copy`
  into a pre-sized `List(Of Byte)`.
  - **Before**: `convert 1440K.hfe 1440K.hfe` ≈ 57 s.
  - **After**: ≈ 1.3 s. Python takes ≈ 0.18 s on the same input; VB is now
    in line with where it should be relative to CPython.

- [x] **LINQ sweep — Tier 1: `AmigaDosCodec.Checksum` O(N²)** — every
  `Step 4` iteration was calling `BytesToUInt32BE(data.Skip(i).Take(4).ToArray())`,
  re-walking the source iterator each time. For a 512-byte payload that's
  ~32K LINQ ops per checksum × thousands of sectors per Amiga write.
  Replaced with an indexed `BytesToUInt32BE(data, i)` overload (O(N) total).

- [x] **LINQ sweep — Tier 2: `bits.Skip(N).Take(M).ToList()` in codecs.**
  Replaced with `List(Of Boolean).GetRange(N, M)` (Array.Copy-backed, O(M))
  across the per-sector candidate loops in `AmigaDosCodec`, `MacGcrCodec`,
  `Apple2GcrCodec`, `DataGeneralCodec`, `IBMFixedCodec` (DEC RX-02),
  `NorthStarCodec`, `MicropolisCodec`, `HpMmfmCodec`, and `C64GcrCodec`.
  Same change applied to `TrackModel.GetRevolution`,
  `TrackModel.PLLTrack` rev-cells, and the `MasterTrack` splice/extension
  rebuilders (`Skip().Take().SelectMany().Concat().ToList()` → indexed
  pre-sized `List` build).

- [x] **LINQ sweep — Tier 3: per-sector / per-track byte-array slicing.**
  Replaced `Skip(N).Take(M).ToArray()` with `Array.Copy` into a pre-sized
  `Byte()` across `IBMFixedCodec`, `IBMScanCodec`, `MacGcrCodec`,
  `Apple2GcrCodec`, `DataGeneralCodec`, `MicropolisCodec`, `NorthStarCodec`,
  `HpMmfmCodec`, `C64GcrCodec`, `EDSKImage` (idam/dam tail builders),
  `D88Image`, `ApridiskImage`, `MSAImage`, `FDIImage`, `NFDImage`,
  `IMDImage`, `A2RImage`, `TD0Image` (LZSS unpack pattern emit), and
  `KryoFluxImage` (per-track flux skip). HFE per-block (256-byte) writers
  for both HFEv1 and HFEv3 now write into a reusable scratch buffer and
  pad in place. `RotateList` in `HFEImage` and `CAPSImage` rebuilt as
  pre-sized `List(Of T)` instead of `Skip(i).Concat(Take(i)).ToList()`.

- [x] **Parity preserved** — full byte-equality regression
  (`H:\gw\malformed\regression-perf.ps1`) re-verifies `ima → hfe`,
  `hfe → ima`, `hfe → hfe`, `td0 → hfe`, `td0 → ima`, `adf → hfe`,
  `ipf → img`, and `hfe → img` after the sweep; SHA-256 still matches
  `gw.exe` 1.23. Malformed-input sweep
  (`H:\gw\malformed\run.ps1`) still reports 21 OK / 7 expected / 0
  diverged.

- [x] **LINQ sweep — Tier 4: tail-end hot paths.** Cleaned up the
  remaining patterns flagged by the audit:
  - `FluxModel.SetNrRevs` synthesis path (called when SCP/KryoFlux output
    is forced to `--revs N≥3`) was cloning the entire flux list per
    iteration via `List.ToList()` and rebuilding via
    `Concat({…}).Concat(Skip(1)).ToList()`. Now scans `List` by index
    into a pre-sized `List(Of Double)` and uses `RemoveRange` /
    `InsertRange` (in-place `Array.Copy`) for prepend/truncate.
    Truncation path likewise switched from `Take(n).ToList()` to
    `RemoveRange`.
  - `FluxModel.Reverse` switched `IndexList.Skip(1).ToList()` to
    `IndexList.RemoveAt(0)`.
  - `EDSKImage.BuildMasterTrack` was emitting gap fills via
    `Enumerable.Repeat(gapByte, N).Select(Function(x) CByte(x)).ToArray()`
    per sector × per track (~10K calls per disk write), with a redundant
    `Select(CByte)` on already-byte input. Replaced with a `RepeatByte`
    helper using a single `Byte()` allocation + indexed fill (skipping
    the fill loop entirely when the value is zero, since `Byte()` is
    already zero-initialised).
  - `MasterTrack.BuildFlux` and `HFEv3_Generator..ctor` replaced
    `bitTicks.Sum()` (delegate-per-element on a ~100K-element
    `List(Of Double)`) with a direct indexed accumulator.
  - **Verified**: SCP roundtrip with `--revs ∈ {3, 5, 8}` produces
    SHA-256-equal IMA decode output across Python and VB, and SCP file
    sizes are byte-identical between binaries. `convert` parity matrix
    + malformed sweep both unchanged from prior pass.

- [x] **Post-LINQ audit — beyond LINQ.** Subsequent sweep targeted hot
  paths flagged by the audit that weren't LINQ chains:
  - **CRC-CCITT-FALSE table.** Four near-identical bit-by-bit
    implementations of `crc-ccitt-false` (`IBMFixedCodec`, `IBMScanCodec`,
    `EDSKImage`, `HpMmfmCodec`) consolidated into one
    `Greaseweazle.Codecs.Crc16Ccitt` with a 256-entry `UShort` lookup
    table. Per-byte cost drops from 8 inner iterations + branching to
    a single index + xor; ~4–8× faster on the per-sector verify path.
  - **`PllTrack` pre-sized bit / time arrays.** `BitArray` and `TimeArray`
    used to grow from default capacity, taking ~17 doublings per
    standard-density track during `OptimizedFlux.FluxToBitcells`. Now
    pre-sized from `time_per_rev / clock × revolutions × 1.1` (or 100K
    fallback). Avoids the entire reallocation chain on the inner decode
    loop.
  - **`OptimizedFlux.Read28Bit` inlined.** The 28-bit-payload decoder
    was a `Func(Of Integer)` closure capturing `pos` and `data`; the
    delegate `Invoke` + closure access was a non-trivial percentage
    of `DecodeFlux`. Now a `Private Shared Sub` taking `pos ByRef`.
  - **`BitsToBytes` / `BytesToBits` consolidation.** Eleven copies of
    near-identical MSB-first bit/byte unpackers (one per codec + EDSK,
    DMK) replaced with a single `Greaseweazle.Codecs.BitHelpers` module.
    The inner per-byte loop is unrolled (no `For j = 0 To 7` shifts, no
    `If(…, 1, 0)`); IBMFixed retains its pad-to-byte semantics via a
    dedicated `BitsToBytesPadded` overload.
  - **`FindPatternOffsets` word-level scan.** Eight identical iterator
    copies of "compare M bits at every offset, exit at first mismatch"
    replaced by one `BitHelpers.FindPatternOffsets` that packs the
    haystack into 64-bit words once per call and slides a single-XOR
    window. Patterns ≤ 64 bits (every codec's IDAM/DAM/sync) collapse
    from O(N·M) bit comparisons to O(N) word ops; ~10× faster on the
    typical 16-50 bit sync scans across ~100K bits per track. Patterns
    longer than 64 bits (DecMmfm only) fall back to a tight bit-level
    scan.
  - **Verified**: byte-equality regression
    (`H:\gw\malformed\regression-perf.ps1`) and full malformed sweep
    (`H:\gw\malformed\run.ps1`) re-run; SHA-256 still identical to
    `gw.exe` 1.23 across IMA / HFE / TD0 / ADF / IPF conversions, and
    21 OK / 7 expected / 0 diverged on the malformed contract suite.

## 6. CLI rendering parity

- [x] `info` output layout (`Host Tools` line, new `CLI` line, `Device:` block).
- [x] `--help` text per action mirrors Python (modulo unreleased `align` action).
- [x] Catalogue rendering on `Unknown format '…'` (CLI's `FatalErrorFormatter`).
- [x] Catalogue rendering on `Unrecognised file suffix '…'` (`Known suffixes:` list).
- [x] `Converting c=…:h=… -> c=…:h=…` header order vs output-image opening.
- [x] `HFE: Data bitrate detected: N kbit/s` informational line.
- [x] Line-ending normalisation in test runner (Python LF vs .NET CRLF).

## 7. Image-format coverage matrix

### 7a. Per-suffix grid (one row per file type)

Verdict legend:
- **BYTE-EQUAL** — SHA-256 of `gw.exe` output and `gw-vb` output match.
- **BYTE-EQUAL†** — match modulo SCP-footer timestamps + dependent header
  checksum (3 bytes when the two runs cross a wall-clock second; 0 when
  they don't). Python's gw.exe also produces non-deterministic SCP files
  for the same reason — true byte-for-byte SCP parity is impossible by
  design.
- **BYTE-EQUAL‡** — match modulo IMD comment-header timestamp (one
  `dd/mm/YYYY HH:MM:SS` substring; same caveat as SCP).
- **BOTH-FAIL (intrinsic)** — both binaries reject the conversion with
  identical error text and exit 1; no parity bug.
- **n/a** — pairing not exercised (e.g. flux→sector without `--format`,
  or no sample on disk yet).

Sources covered: every sample currently under `H:\gw\images\`. Targets
exercised per row: `.img`, `.do` (Apple-DOS only), `.hfe`, `.scp`, plus
`.imd` for IBM-compatible inputs. Hardware-only conversions (KryoFlux
`.raw` folder, Sydex `.dsk`) are noted but exercised in §4 rather than
this file-only matrix.

| Suffix  | System(s) / family                              | Default `--format`           | Sample(s) under `H:\gw\images\`                                          | Conversions tested (vs `gw.exe` v1.23)                                                                                                     | Notes                                                                                                          |
|---------|-------------------------------------------------|------------------------------|--------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| `.hfe`  | Universal flux container (HxC HFE, zoned bitcell) | suffix-detected             | `160K.hfe` / `180K.hfe` / `320K.hfe` / `360K.hfe` / `1200K.hfe` / `1440K.hfe` | `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL† · `→ .img` BOTH-FAIL (intrinsic — flux→sector requires `--format`)                              | HFEv1 round-trip; HFE input parser was the quadratic-LINQ hot spot fixed in §5.                                |
| `.ima`  | IBM PC (raw sector image)                       | `ibm.1440` / `ibm.360` / etc. | `1440K.ima` / `1200K.ima` / `360K.ima` / `320K.ima` / `180K.ima` / `160K.ima` | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL† · `→ .imd` BYTE-EQUAL‡                                                    | `.img` and `.ima` share the IBM raw codec; HFE output was the IAM-gap-presync fix in §11.                      |
| `.img`  | IBM PC (raw sector image, alias of `.ima`)      | `ibm.*` (same as `.ima`)     | (output-only here; covered as a target above)                            | covered as target on every IBM-compatible input row                                                                                        | Identical layout to `.ima`; suffix difference only.                                                            |
| `.adf`  | Amiga (DD MFM, AmigaDOS)                        | `amiga.amigados`             | `Demos06.adf`                                                            | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | Full Amiga MFM round-trip parity.                                                                              |
| `.adl`  | Acorn ADFS Large (640K, DD MFM)                  | `acorn.adfs.640` (suffix)    | `ZigZagTheRomansLocations.adl`                                           | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | Acorn-side suffix; codec is `acorn.adfs.640`. New sample, smoke-tested.                                        |
| `.ipf`  | Amiga / Atari ST / etc. (SPS / CAPS preservation flux) | `amiga.amigados` for our sample | `A-Train_HiRes.ipf` / `A-Train_LoRes.ipf`                            | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | Requires `CAPSImg.dll` next to `gw-vb.exe` (or on `PATH`). Banner parity fixed via `EntryPoint:` (§11).        |
| `.po`   | Apple II ProDOS-order sector image              | `apple2.prodos.140`          | `ProDOS 2.0.3.po`                                                        | `→ .img` BYTE-EQUAL · `→ .do` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                      | HFE output was the FM/GCR doubler `(0, b)` fix in §11.                                                         |
| `.do`   | Apple II DOS-order sector image                 | `apple2.appledos.140`        | (output-only; covered as a target on the `.po` row)                      | covered as target on `.po → .do`                                                                                                          | DOS-order ↔ ProDOS-order interleave.                                                                          |
| `.a2r`  | Apple II flux capture (`A2R` v3)                | (driven by codec)            | `Mind Forever Voyaging, A (USA) (Disk 1/2).a2r`                          | `→ .img` BOTH-FAIL (intrinsic — sample is `A2R2`, v1.23 only supports `A2R3`)                                                              | Both binaries emit `A2R: Invalid signature` and exit 1 — full parity, just nothing to encode against this rev. |
| `.d64`  | Commodore 1541 (5.25" SS DD GCR, zoned)         | `commodore.1541`             | `Heroes of the Lance (Disk A/B).d64`                                     | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | Was the zoned-bitrate (§11) and BAM `disk_id` byte-shift (§11) bugs. 21/19/18/17 sectors per zone now correct. |
| `.d71`  | Commodore 1571 (5.25" DS DD MFM)                | `commodore.1571`             | `deadline.d71`                                                           | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | New sample, smoke-tested.                                                                                      |
| `.d81`  | Commodore 1581 (3.5" DS DD MFM)                 | `commodore.1581`             | `Adventure Games 128 (19xx)(-).d81`                                      | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | New sample, smoke-tested.                                                                                      |
| `.d1m`  | Commodore CMD FD-2000 DD                        | `commodore.cmd.fd2000.dd`    | `empty-dd8.d1m`                                                          | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | New sample, smoke-tested.                                                                                      |
| `.d2m`  | Commodore CMD FD-2000 HD                        | `commodore.cmd.fd2000.hd`    | `empty-hd8.d2m`                                                          | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | New sample, smoke-tested.                                                                                      |
| `.d4m`  | Commodore CMD FD-4000 ED                        | `commodore.cmd.fd4000.ed`    | `empty-ed8.d4m`                                                          | `→ .img` BYTE-EQUAL · `→ .hfe` BOTH-FAIL (intrinsic — ED bitrate exceeds HFEv1 capacity) · `→ .scp` BYTE-EQUAL†                            | Both binaries emit `HFE: Track too long to fit in image! Are you trying to create an ED-rate image?` and exit 1. |
| `.imd`  | IBM-compatible (Sydex `ImageDisk`, self-describing) | `ibm.360` / `ibm.scan` etc.  | `King's Quest - Quest for the Crown … (PC Booter).imd`, `Zork III - The Dungeon Master … (PC Booter).imd` | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL† · `→ .imd` BYTE-EQUAL‡                                                    | IMD writer comment-header version is now `Greaseweazle 1.23` (was `1.23.0.0`).                                |
| `.td0`  | IBM-compatible (Sydex `TeleDisk`)               | suffix-detected              | `360k.td0` / `720k.td0`                                                  | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL† · `→ .imd` BOTH-FAIL (intrinsic — `T6.0: Sectors vary in size` rejected by IMD) | HFE output was the `from_config` oversize-track fix + double-rate FM detection (§11).                          |
| `.mgt`  | Sinclair Spectrum +D / Disciple / Sam Coupé / Acorn ADFS L (alt suffix) | `ibm.800` (suffix-detected) | `Agatha's Folly (1989)(Zenobi).mgt`                                      | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | New sample, smoke-tested. Same on-disk layout as `ibm.800`.                                                    |
| `.st`   | Atari ST (raw sector image)                     | `atarist.720`                | `Aladin Disk (19xx)(-).st`                                               | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | Sample size is 732672 (just under standard 80×2×9×512=737280 — partial last track); both binaries pad identically. |
| `.hdm`  | NEC PC-98 HD (1.25 MB MFM, 8 sectors of 1024)   | `pc98.2hd` (suffix-detected) | `PC-9821Xa12C8 Recovery FD 1.hdm`                                        | `→ .img` BYTE-EQUAL · `→ .hfe` BYTE-EQUAL · `→ .scp` BYTE-EQUAL†                                                                           | New sample, smoke-tested.                                                                                      |
| `.scp`  | Universal flux container (Supercard Pro)        | n/a                          | (no input sample on disk yet)                                            | covered as `→ .scp` target on every row above (writer side; reader side covered by `.scp → …` runtime sweeps in §4)                       | SCP writer now emits the real `Greaseweazle <ver>` provenance string + `time.time()` timestamps (was hard-coded to `Greaseweazle 0.0` + zero timestamps; SCP file size was 1 byte short across the board). |
| `.raw`  | KryoFlux raw flux folder                        | varies                       | exercised in §4 hardware sessions                                        | covered in §4 (`read --drive 0/1/2 *.raw`, `write --drive *.raw`); not file-to-file matrix-tested here                                     | Folder-of-files format; no fixture in `H:\gw\images\`.                                                         |
| `.dsk`  | Various (Sydex/Apple/CPC EDSK)                  | varies                       | exercised in §4 hardware sessions                                        | covered in §4; not file-to-file matrix-tested here                                                                                        | No fixture in `H:\gw\images\`.                                                                                 |
| `.ads`  | Acorn ADFS Small (160K, SD)                      | `acorn.adfs.160` etc.        | (no sample yet)                                                          | static parity only                                                                                                                         | Drop a `.ads` fixture and rerun the matrix.                                                                    |
| `.adm`  | Acorn ADFS Medium (320K)                         | `acorn.adfs.320` etc.        | (no sample yet)                                                          | static parity only                                                                                                                         | Drop a `.adm` fixture and rerun the matrix.                                                                    |
| `.ctr`  | CAPS raw container                               | (driven by CAPS lib)         | (no sample yet)                                                          | static parity only                                                                                                                         | Same `EntryPoint:` plumbing as `.ipf`; needs a `.ctr` fixture.                                                 |
| `.d88`  | NEC PC-88 / X1 / FM-7                            | `pc98.*` / vendor-specific   | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.dcp`  | DCP container                                    | varies                       | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.dim`  | DIM container (PC-98 region)                     | varies                       | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.dmk`  | TRS-80 / Coco DMK                                | varies                       | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.dsd`  | Acorn DFS double-sided                           | `acorn.dfs.*`                | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.edsk` | CPC Extended DSK                                 | `amstrad.cpc.*`              | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.fd`   | NEC PC-98 (alt suffix)                           | `pc98.*`                     | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.fdi`  | FDI image                                        | varies                       | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.msa`  | Atari ST `MSA`                                   | `atarist.*`                  | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.nfd`  | NFD container                                    | varies                       | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.nsi`  | Northstar                                        | `northstar.*`                | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.sf7`  | Sharp X1 / SF7                                   | varies                       | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.ssd`  | Acorn DFS single-sided                           | `acorn.dfs.*`                | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |
| `.xdf`  | XDF container                                    | varies                       | (no sample yet)                                                          | static parity only                                                                                                                         |                                                                                                                |

The grid above subsumes the previous "samples available" / "no sample
yet" split. Adding a fixture under `H:\gw\images\` and rerunning the
matrix runner will promote a row from "static parity only" to a real
verdict cell.

### 7b. Disk formats (`--format=…`)

This axis is the disk-codec namespace (`ibm.*`, `commodore.*`, etc.) and
is orthogonal to the suffix grid in §7a.

Runtime-verified at the drive level: `ibm.180`, `ibm.360`, `ibm.720`,
`ibm.1200`, `ibm.1440`. File-only convert verified via the §7a grid:
`amiga.amigados`, `apple2.prodos.140`, `apple2.appledos.140` (as a
target of `.po → .do`), `atarist.720`, `commodore.1541`,
`commodore.1571`, `commodore.1581`, `commodore.cmd.fd2000.dd/.hd`,
`commodore.cmd.fd4000.ed`, `pc98.2hd`, `acorn.adfs.640`. Not yet
exercised at all — needs hardware media or a suitable image:

- [ ] `mac.400` / `mac.800` (Mac GCR — distinct codec path)
- [ ] `atarist.800` (the 720K row in §7a only exercises `atarist.720`)
- [ ] The ~135 other diskdefs across `acorn.dfs.*`, `atari8.*`, `dec.*`,
  `mfm.*`, `sega.*`, `zx.*`, etc.

### 7c. Codecs

- [x] `IBM MFM` / `IBM FM` — exhaustively exercised at runtime and via
  the IBM-compatible rows of §7a (`.ima`, `.imd`, `.td0`, `.hdm`, `.st`,
  `.mgt`, `.adl`, `.d71`, `.d81`, `.d1m`, `.d2m`, `.d4m`).
- [x] `Amiga MFM` — convert-verified via `.adf` and `.ipf` rows.
- [x] `Apple2 GCR` — convert-verified via the `.po` row.
- [x] `Commodore GCR` — convert-verified via the `.d64` row (zoned
  21/19/18/17 layout, post BAM `disk_id` widen-shift fix in §11).
- [ ] `Mac GCR`, `HP MMFM`, `Northstar`, `Micropolis` — only static
  symbol-level audit; need samples for runtime exercise.

## 8. Areas **not** yet runtime-tested

- [ ] **`update` action live flash** — explicitly off-limits per project rules.
  Argv parsing & dry-run paths verified, but no firmware write performed.
- [ ] **`align` action live alignment loop** — argv parsing covered; needs a
  drive with deliberately mis-aligned heads to exercise the multi-revolution
  align flow end-to-end.
- [ ] **Long-running operations** under interrupt — Ctrl-C exit code 130 is
  documented but not stress-tested mid-`read`/`write`/`erase`.
- [x] **Drive-error paths** — see the deliberate matrix in §4
  ("Drive-error parity matrix"). Currently covered: write-protect on
  `erase` + `write`, no-disk on `rpm` / `read` / `write` / `erase` /
  `seek`, and **drive powered off** on every action across all three
  drive IDs (12 scenarios). Each row is byte-for-byte identical to
  Python's gw.exe v1.23 on stdout, stderr, and exit code. Two
  distinct exit-code contracts characterised:
  - `Track0` FATAL (exit 1) — `read`, `seek` (any pre-seek action).
  - `GetFluxStatus: No Index` `CmdError` (exit 0) — `rpm`, `write`, `erase`.
  - [x] `write` against write-protected disk — A1/A2.
  - [x] `read` with no disk in drive — B2 (B1/B3/B4 cover the same
    `GetFluxStatus: No Index` surface from `rpm`/`write`/`erase`).
  - [x] All actions against a powered-off drive — C0–C3, D0–D4, E0–E2.
  - [x] `seek` past mechanical limit on a 40-track drive — F1/F2/F3.
    **Behavioural finding**: `Track0SeekMismatchException` doesn't
    actually fire on this path in `gw.exe` v1.23 (the firmware
    silently absorbs over-stepping and re-zeroes on Track 0 sensor),
    and VB matches that quirk byte-for-byte. The `Track0` errors
    we *did* trigger come from the drive-off matrix (no Track 0
    sensor signal at all), not from past-limit seek.
- [~] **CAPS/IPF `.ipf`** — DLL-loading path verified, real IPF reads
  byte-equal with Python on `.ipf → .img` / `.hfe` / `.scp†` (see §7a).
  IPF-image-info banner now also matches gw.exe byte-for-byte (see §11).
  Remaining:
  - [ ] Byte-equal `Could not find SPS/CAPS library` hint block when
    the DLL is genuinely absent — the message text is currently
    VB-specific (`Probe errors: …`) and would need formatter-side
    work to match Python's installation-instructions block.
- [ ] **Hard-sectored media** (`--hard-sectors`) — distinct flux topology
  not yet exercised against real hardware.
- [ ] **`--fake-index`** — synthetic-index path on drives without an index
  pulse; no runtime exercise yet.

## 9. Tooling / harness

- [x] Malformed-input runner (`H:\gw\malformed\run.ps1`)
  - PS 5.1-compatible (`System.Diagnostics.ProcessStartInfo`, no inline-`if`).
  - 60-second per-test timeout.
  - Line-ending + binary-name normalisation.
  - `-Expected reason` parameter to mark known-acceptable divergences.
- [ ] **Per-action runtime sweep runner** — analogous to the malformed
  runner, but covering steady-state success paths for each action against
  live hardware. Currently runtime tests are ad-hoc; converting them into
  a scripted sweep would let us re-run after every refactor.
- [ ] **Convert round-trip matrix** — automated `gw.exe in.X out.Y && gw-vb in.X out.Y && fc /b` for every `.X → .Y` pair we care about. Would shake out
  format-conversion edge cases without needing hardware.

## 10. Known-acceptable divergences

These are tracked & expected; no fix planned:

- VB lists the unreleased upstream `align` action in `--help` and on
  unknown-action errors.
- VB exposes forward-ported diskdefs (`apricot.*`, `dec.rx50`, `rm.5in.*`,
  `rm.8in.sd`, `sharp.2d`) and the `.2d` file suffix that aren't in the
  v1.23 release.
- VB emits clean, structured error output for malformed HFE/SCP inputs
  where Python emits a raw `struct.error` traceback. VB is correct here;
  Python upstream has open issues for these regressions.
- Argparse banners use `gw-vb` instead of `gw.exe` — same wording, different
  binary name.

## 11. Known issues / divergences to fix

Surfaced by the §7a sample-driven matrix — these are real bugs in the
VB port relative to `gw.exe` v1.23, not parity-tooling artefacts:

- [x] **`.d64` zoned-bitrate decoding** — fixed in
  `DiskDefParser.ExpandTrackSpec`. The wildcard `*` in the disk
  definition was overwriting earlier explicit `tracks 0-16 / 17-23 /
  24-29` entries, collapsing the 4-zone 1541 layout to a flat
  17 sectors/track. Now mirrors Python's "fill remaining only" rule
  (`codec.py::_get_diskdef`: `if (c,hd) not in track_map`). Verified
  with `Heroes of the Lance (1989) (Disk A) [!].d64`: 768 / 768
  sectors decoded, text output identical to Python.
- [x] **`.td0 → .hfe` arithmetic overflow** — fixed in two places:
  - `HFEImage.ShouldUseDoubleRate` was using
    `summary.StartsWith("ibm.fm")` (which never matches the actual
    `IBM FM (...)` summary) plus a bitrate fallback that flagged
    every DD MFM disk as "double rate", emitting twice the bitcells
    per track and overflowing HFEv1's `2 * nrBytes` CUShort field.
    Now reads `IbmTrackFixed.Mode` directly (matching Python's
    `track.mode is ibm.Mode.FM`).
  - `TD0Image.FromBytes` constructed `IbmTrackFixed` with a fixed
    `clock = timePerRev / (rate * 400)`, bypassing Python's
    `IBMTrack_Fixed.from_config` oversize handling. For tracks with
    non-standard sectors (e.g. cyl 6 head 0 in the 360k sample has
    one n=6 8K sector + seven n=3 1K sectors as copy-protection),
    Python reduces gap3 to 0 and stretches `tracklen_bc` to fit; VB
    now does the same inline. Verified with `360k.td0`: same
    1043968-byte HFE produced.
  - HFEv1 `GetImage` is now wrapped in `Try / Catch OverflowException`
    so genuinely-too-long tracks (e.g. ED-rate) emit the same
    `HFE: Track too long to fit in image! Are you trying to create an
    ED-rate image?` fatal as Python's `struct.error` handler.
- [x] **IPF info banner missing** — fixed by overriding `ToString` on
  `IPF` and `CTRaw`, plus `CAPS.FromBytes` now emits the banner via
  `LibraryDiagnostics.EmitInfo` after `CAPSGetImageInfo` succeeds
  (mirrors Python's `print(caps)` in `CAPS.from_file`). Output now
  matches gw.exe byte-for-byte for IPF inputs:

  ```
  IPF Image File:
   SPS ID: 1010 (rev 1)
   Platform: Amiga
   Created: 2003/11/12 07:32:32
   Cyls: 0-83  Heads: 0-1
  ```
- [x] **HFE encoding nondeterminism (`.ima → .hfe`)** — fixed in
  `IbmTrackFixed.CreateMfmMasterTrack`. Python's
  `IBMTrack.mfm_master_track` emits a 12-byte gap-presync (zeros)
  before *every* sync, including the IAM. The earlier VB code emitted
  presync only before sectors, producing a systematic 24-encoded-byte
  shift across the rest of the track. After the fix, `.ima → .hfe`
  and `.td0 → .hfe` are byte-equal to gw.exe.
- [x] **HFE FM/GCR double-rate bit ordering (`.po → .hfe`)** — fixed
  in `HFEImage.PrepareMasterTrackForEmit`. HFE convention is that FM
  and Apple2 GCR are recorded at double rate. Python's `emit_track`
  runs `ibm.doubler` (= `ibm.encode`) over the master-track bytes,
  which interleaves a *zero clock cell* before each data bit:
  for each input bit `b` the output pair is `(0, b)` — e.g.
  `0xFF → 0x5555` (`0101010101010101`). The earlier VB code emitted
  `(b, b)` (`0xFF → 0xFFFF`), so the bit *count* matched but the
  *content* didn't. Apple2 GCR readback still worked because the
  `D5 AA 96 / D5 AA AD` sync bytes appear in the data cells either
  way, but HFE bytes diverged from `gw.exe`. After the fix,
  `.po → .hfe` is byte-equal to gw.exe. (Same code path covers IBM
  FM → HFE for any genuine FM-encoded input, though we don't have
  an FM sample on disk yet to confirm at runtime.)
- [x] **`.d64 → .hfe` BAM `disk_id` byte read** — fixed in
  `D64Image.ReadDiskIdFromBam`. Python:
  `disk_id, = struct.unpack('<H', dat[162:164])`. The VB port
  translated this as `data(162) Or (data(163) << 8)`, which looks
  innocuous but hits a VB-specific quirk: the `<<` operator on a
  `Byte` operand reduces the shift count modulo 8, so
  `Byte << 8 == Byte << 0` — the high-byte shift silently
  collapses to zero (VB Language Specification, "Shift Operators").
  Result: VB read `disk_id = 0x0047` instead of `0x4447` for the
  test sample, so every sector header carried the wrong upper
  `disk_id` byte and a corrupted header checksum, producing a
  3-byte diff per sector × 768 sectors = ~2304 byte-diffs.
  Fix: widen to `UShort` before shifting (matches the explicit
  cast pattern already used elsewhere in the codebase, e.g.
  `Apple2GcrCodec`, `MSAImage`, `KryoFluxImage`). Verified:
  `.d64 → .hfe` is now byte-equal to gw.exe.
- [x] **SCP / IMD writer provenance + timestamps** — fixed in
  `SCPImage.GetImage` and `IMDImage.GetImage` plus a new shared
  `Greaseweazle.Core.HostVersion` helper. The SCP footer's
  `app_name` was hard-coded to `Greaseweazle 0.0` (16 chars vs
  Python's 17-char `Greaseweazle 1.23`), making every SCP file
  exactly 1 byte short and corrupting the file-header checksum on
  top of that. The creation/modification timestamps were also
  zero, where Python writes `round(time.time())` for both. The
  IMD comment-header was reading `AssemblyName.Version.ToString()`,
  giving `Greaseweazle 1.23.0.0` vs Python's `Greaseweazle 1.23`.
  After the fix:
  - `.<X> → .scp` is byte-equal modulo at most 3 bytes (1 lowest
    byte each in `creation_time` / `modification_time` plus 1
    propagated byte of header checksum) when the two binaries
    cross a wall-clock second; identical when they don't. True
    byte-for-byte SCP parity is impossible by design (Python's own
    output also varies between runs).
  - `.<X> → .imd` is byte-equal modulo the IMD `dd/mm/YYYY HH:MM:SS`
    comment substring with the same wall-clock-second caveat.

### Convert parity matrix after fixes

The full per-source-suffix matrix is consolidated in §7a. The TL;DR is
that every fixture-driven `→ .img` / `→ .hfe` / `→ .imd` row is
BYTE-EQUAL to `gw.exe` v1.23, every `→ .scp` row is BYTE-EQUAL† (modulo
intrinsic timestamps), and the only BOTH-FAIL rows
(`.hfe / .td0 → .img` without `--format`, `.d4m → .hfe` ED-rate,
`.a2r → .img` for our `A2R2` sample, `.td0 → .imd` mixed-sector-size)
are intrinsic limitations Python rejects identically.

---

_Last updated: 2026-04-29. Source of truth for the malformed sweep is
`H:\gw\malformed\run.ps1`; latest run log under `H:\gw\malformed\logs\`.
The §7a image-format grid was last regenerated against the full sample
inventory in `H:\gw\images\` after the `SCPImage` / `IMDImage`
provenance + timestamp fixes._
