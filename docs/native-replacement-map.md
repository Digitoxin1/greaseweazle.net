# Native Replacement Map (Python -> VB.NET)

This document records the required Python-native touchpoints (C extension and
ctypes/Win32 modules) and their managed .NET replacement paths in the unified
`gw-vb` project. All replacements compile under `src/Greaseweazle/Greaseweazle.vbproj`
and are exercised by the parity runner at
`src/Greaseweazle.Parity/Greaseweazle.Parity.vbproj`.

## Optimised extension exports

Python C extension: `src/greaseweazle/optimised/optimised.c`
Python C sources also covered: `apple_gcr_6a2.c`, `apple2.c`, `c64.c`, `mac.c`, `td0_lzss.c`.

VB implementation files (managed ports, no native dependency):
- `src/Greaseweazle/track/TrackModel.vb`        (`Greaseweazle.Core`)
- `src/Greaseweazle/optimised/OptimizedFlux.vb` (`Greaseweazle.Optimised`)
- `src/Greaseweazle/optimised/Td0Lzss.vb`       (`Greaseweazle.Optimised`)
- `src/Greaseweazle/optimised/Apple2.vb`        (`Greaseweazle.Optimised`)
- `src/Greaseweazle/optimised/C64.vb`           (`Greaseweazle.Optimised`)
- `src/Greaseweazle/optimised/Mac.vb`           (`Greaseweazle.Optimised`)
- `src/Greaseweazle/optimised/AppleGcr62.vb`    (`Greaseweazle.Optimised`)

Symbol mapping (Python C export -> VB managed implementation):

| Python C export | VB managed target |
|-----------------|-------------------|
| `flux_to_bitcells` | `Greaseweazle.Core.ManagedFluxDecoder.FluxToBitcellsManaged` (defined in `track/TrackModel.vb`) |
| `decode_flux`      | `Greaseweazle.Infrastructure.UsbProtocol.DecodeFlux` (delegates to `Greaseweazle.Optimised.OptimizedFlux.DecodeFlux`) |
| `decode_mac_gcr` / `encode_mac_gcr`       | `Greaseweazle.Optimised.Mac.DecodeBytes` / `Greaseweazle.Optimised.Mac.EncodeBytes` (consumed by `Greaseweazle.Codecs.MacGcr`) |
| `decode_mac_sector` / `encode_mac_sector` | `Greaseweazle.Optimised.Mac.DecodeSector` / `Greaseweazle.Optimised.Mac.EncodeSector` |
| `decode_c64_gcr` / `encode_c64_gcr`       | `Greaseweazle.Optimised.C64.DecodeGcr` / `Greaseweazle.Optimised.C64.EncodeGcr` |
| `decode_apple2_sector` / `encode_apple2_sector` | `Greaseweazle.Optimised.Apple2.DecodeSector` / `Greaseweazle.Optimised.Apple2.EncodeSector` |
| `td0_unpack` | `Greaseweazle.Optimised.Td0Lzss` (decoder used by `Greaseweazle.Images.TD0Image`) |

Layer 5 codec implementations that consume the optimised exports:
- `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb`     (`Greaseweazle.Codecs.MacGcr`)
- `src/Greaseweazle/codec/commodore/C64GcrCodec.vb`     (`Greaseweazle.Codecs.C64Gcr`)
- `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb`     (`Greaseweazle.Codecs.Apple2Gcr`)

## ctypes / Win32 native integrations

| Python module | VB.NET replacement |
|---------------|--------------------|
| `src/greaseweazle/image/caps.py` (CAPS/IPF DLL via `ctypes`) | `src/Greaseweazle/image/CAPSImage.vb` (`Greaseweazle.Images`); P/Invoke backends `CapsBackendX64` / `CapsBackendGeneric` |
| `src/greaseweazle/tools/list_ports_windows.py` (Windows SetupAPI / cfgmgr32 / kernel32 via `ctypes`) | `src/Greaseweazle/tools/WindowsPortDiscovery.vb` (`Greaseweazle.Tools`); `BasicActions.vb` and `Tooling.vb` provide port-scoring helpers (`ToolOptions.ScorePort` / `FindBestPort` / `FindPortDevice`) |

USB protocol/client implementation files (Python `usb.py` had no native dependency
but used `pyserial`; the VB port uses `System.IO.Ports.SerialPort`):
- `src/Greaseweazle/usb/UsbProtocol.vb`       (`Greaseweazle.Infrastructure`)
- `src/Greaseweazle/usb/UsbUnitClient.vb`     (`Greaseweazle.Infrastructure`)
- `src/Greaseweazle/usb/SerialPortTransport.vb` (`Greaseweazle.Infrastructure`)

## Gate evidence

- Fixture generator oracle: `tools/generate_parity_fixtures.py`
- Parity runner: `src/Greaseweazle.Parity/Program.vb`
- Run command:
  - `dotnet run --project src/Greaseweazle.Parity/Greaseweazle.Parity.vbproj`
- The runner exercises the optimised native ports via the `RunOptimisedParity` block (Apple GCR 6+2, Apple II sector encode/decode, C64 GCR, Mac GCR, MFM flux decoding, TD0 LZSS) plus dedicated fixture suites for `optimised-native`, `find_port`, and `range_str`.
- Expected output: `Parity checks passed for error/flux/track/usb/optimised-native/tools/codec/image/cli/trackset/actions/precomp/readwrite/info/find_port/update/range_str/pin/reset/seek/delays/clean/convert/erase/bandwidth/rpm/align/track-resolution/scp fixtures.`

## Audit refresh

The native-replacement coverage is also surfaced in:

- `docs/conversion-audit.md`           (executive summary, regenerable)
- `docs/conversion-audit.json`         (machine-readable companion)
- `docs/parity-symbol-manifest.md`     (per-layer name parity, reconciled)
- `docs/parity-checklist.md`           (per-symbol four-status taxonomy)

Regenerate via `python tools/audit_conversion.py`.
