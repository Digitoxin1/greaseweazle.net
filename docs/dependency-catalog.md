# Dependency Catalog: Python -> VB.NET

This catalog is the execution order for conversion and integration.
The unified executable project is `src/Greaseweazle/Greaseweazle.vbproj`
and compiles all mapped layer files below. The companion behavioural parity
runner is `src/Greaseweazle.Parity/Greaseweazle.Parity.vbproj`.

VB namespace mapping (kept stable across layers):

- `Greaseweazle.Core`           -> `error/`, `flux/`, `track/`
- `Greaseweazle.Optimised`      -> `optimised/`
- `Greaseweazle.Infrastructure` -> `usb/`
- `Greaseweazle.Shared`         -> `tools/TrackSet.vb`, `tools/ColumnFormatter.vb`, `tools/OptionParser.vb`
- `Greaseweazle.Codecs`         -> `codec/**/*.vb`
- `Greaseweazle.Images`         -> `image/*.vb`
- `Greaseweazle.Tools`          -> all other `tools/*.vb`
- `Greaseweazle.Cli`            -> `cli/Program.vb`

## Layer 0
- Python: `src/greaseweazle/error.py`
- VB.NET: `src/Greaseweazle/error/ErrorHandling.vb`
- Depends on: none

## Layer 1
- Python: `src/greaseweazle/flux.py`, `src/greaseweazle/optimised/*`
- VB.NET: `src/Greaseweazle/flux/FluxModel.vb`, `src/Greaseweazle/optimised/*.vb`
- Depends on: Layer 0

## Layer 2
- Python: `src/greaseweazle/track.py`
- VB.NET: `src/Greaseweazle/track/TrackModel.vb` (PLL profiles, `PLLTrack`, `MasterTrack`, `Precomp`, `ManagedFluxDecoder` are all folded into this single file).
- Depends on: Layers 0-1

## Layer 3
- Python: `src/greaseweazle/usb.py`
- VB.NET:
  - `src/Greaseweazle/usb/UsbProtocol.vb`     (Cmd/Ack/FluxOp/BusType enums, framing, `CmdError`, `DriveInfo`)
  - `src/Greaseweazle/usb/UsbUnitClient.vb`   (`Unit` client + helpers)
  - `src/Greaseweazle/usb/SerialPortTransport.vb` (low-level serial transport)
- Depends on: Layers 0-2

## Layer 4 (Shared extraction from `tools.util`)
- Python source points:
  - `src/greaseweazle/tools/util.py` (`TrackSet`, `columnify`, option splitting, `find_port`/`get_unit` helpers)
- VB.NET (Shared / pure):
  - `src/Greaseweazle/tools/TrackSet.vb`
  - `src/Greaseweazle/tools/ColumnFormatter.vb`
  - `src/Greaseweazle/tools/OptionParser.vb`
- VB.NET (additional `util.py` consumers, also referenced by `Python map:` comments):
  - `src/Greaseweazle/tools/Tooling.vb`        (`ScorePort`/`FindBestPort`/`FindPortDevice` from `util.find_port`)
  - `src/Greaseweazle/usb/UsbUnitClient.vb`    (`get_unit`-style probing)
- Depends on: Layer 0

## Layer 5
- Python: `src/greaseweazle/codec/codec.py` (+ codec subpackages)
- VB.NET:
  - `src/Greaseweazle/codec/CodecContracts.vb`
  - `src/Greaseweazle/codec/DiskDefinitions.vb`
  - `src/Greaseweazle/codec/DiskDefParser.vb`
  - `src/Greaseweazle/codec/BitcellCodec.vb`
  - `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb`
  - `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb`
  - `src/Greaseweazle/codec/commodore/C64GcrCodec.vb`
  - `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb`
  - `src/Greaseweazle/codec/hp/HpMmfmCodec.vb`
  - `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb`
  - `src/Greaseweazle/codec/ibm/IBMScanCodec.vb`
  - `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb`
  - `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb`
  - `src/Greaseweazle/codec/northstar/NorthStarCodec.vb`
- Depends on: Layers 0-4

## Layer 6
- Python: `src/greaseweazle/image/image.py` (+ image modules)
- VB.NET:
  - `src/Greaseweazle/image/ImageBase.vb`
  - `src/Greaseweazle/image/*.vb`
- Depends on: Layers 0-5

## Layer 7
- Python: `src/greaseweazle/tools/*.py`
- VB.NET:
  - `src/Greaseweazle/tools/Tooling.vb`           (shared option parsing, port discovery wiring)
  - `src/Greaseweazle/tools/BasicActions.vb`      (per-tool action / runtime-state DTOs)
  - `src/Greaseweazle/tools/Align.vb` / `Bandwidth.vb` / `Clean.vb` / `Convert.vb` / `Delays.vb` / `Erase.vb` / `Info.vb` / `Pin.vb` / `ReadWrite.vb` / `Reset.vb` / `Rpm.vb` / `Seek.vb` / `Update.vb`  (per-tool front-ends)
  - `src/Greaseweazle/tools/WindowsPortDiscovery.vb` (managed replacement for `tools/list_ports_windows.py`)
  - `src/Greaseweazle/tools/TrackResolution.vb`   (VB-only helper, no Python 1:1 - centralizes default-track resolution shared by read/write/convert/erase/align)
- Depends on: Layers 0-6

## Layer 8
- Python: `src/greaseweazle/cli.py`
- VB.NET:
  - `src/Greaseweazle/cli/Program.vb`
- Depends on: Layer 7

## Parity Gate
- VB.NET runner: `src/Greaseweazle.Parity/Program.vb`
- Symbol inventory: `docs/parity-symbol-manifest.md` and `docs/parity-symbol-manifest.json`
- Per-symbol checklist: `docs/parity-checklist.md` and `docs/parity-checklist.json`
- Conversion audit: `docs/conversion-audit.md` and `docs/conversion-audit.json`
- Fixtures (29 suites):
  - `src/Greaseweazle.Parity/Fixtures/error-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/flux-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/track-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/usb-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/codec-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/image-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/scp-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/cli-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/tools-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/trackset-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/actions-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/precomp-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/readwrite-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/info-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/update-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/pin-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/reset-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/seek-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/delays-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/clean-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/convert-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/erase-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/bandwidth-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/rpm-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/align-fixtures.json`
  - `src/Greaseweazle.Parity/Fixtures/track-resolution-fixtures.json`
- Gate rule: Layer `N+1` progresses only after layer `N` parity checks pass.

## Audit refresh
Regenerate manifests, checklist, and audit JSON:

```
python tools/generate_symbol_inventory.py    # legacy strict view
python tools/refine_parity_checklist.py      # legacy refined view
python tools/audit_conversion.py             # unified audit (current)
```

The current source of truth for parity status is `tools/audit_conversion.py`,
which folds the strict and refined views together and emits the new four-status
taxonomy (`mapped-exact`, `mapped-normalized`, `name-only-ambiguous`, `missing`).
