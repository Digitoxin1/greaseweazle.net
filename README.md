# Greaseweazle VB.NET Migration Workspace

This workspace ports the Python `greaseweazle` package into a single VB.NET command-line project targeting .NET Framework 4.7.1, with parity verification kept in a separate test harness.

## Repository Layout

```
.
├── Directory.Build.props          MSBuild defaults shared by all projects
├── Greaseweazle.VbNet.slnx        Solution file (open in Visual Studio)
├── src/
│   ├── Greaseweazle/              Main VB.NET CLI -> 'gw-vb' executable
│   │   └── Greaseweazle.vbproj
│   └── Greaseweazle.Parity/       Fixture-driven parity test harness
│       └── Greaseweazle.Parity.vbproj
├── tools/                         Python utilities for parity / codegen
├── docs/                          Generated parity manifests and audit reports
└── python_source/                 Upstream Python 'greaseweazle' source tree
    └── src/greaseweazle/...
```

## Project Layout (VB.NET)

- `src/Greaseweazle/Greaseweazle.vbproj` - unified `gw-vb` executable. Folders mirror the Python package one-to-one for ease of porting:
  - `error` (`python_source/src/greaseweazle/error.py`)
  - `flux` (`python_source/src/greaseweazle/flux.py`)
  - `track` (`python_source/src/greaseweazle/track.py`)
  - `optimised` (`python_source/src/greaseweazle/optimised/*`)
  - `usb` (`python_source/src/greaseweazle/usb.py`)
  - `tools` (`python_source/src/greaseweazle/tools/*.py`)
  - `codec` (`python_source/src/greaseweazle/codec/**`)
  - `image` (`python_source/src/greaseweazle/image/**`)
  - `cli` (`python_source/src/greaseweazle/cli.py`)
- `src/Greaseweazle.Parity/Greaseweazle.Parity.vbproj` - fixture-driven parity checks used as strict migration gates before moving to higher dependency layers.

## Build and Gate

- Build unified CLI: `dotnet build src/Greaseweazle/Greaseweazle.vbproj`
- Run strict parity gate: `dotnet run --project src/Greaseweazle.Parity/Greaseweazle.Parity.vbproj`

## Parity Tooling

Python helpers under `tools/` regenerate the parity manifests and fixtures consumed by `Greaseweazle.Parity`. They read the upstream Python source from `python_source/` and the VB source from `src/Greaseweazle/`.

- `python tools/audit_conversion.py` - rewrites `docs/conversion-audit.json`, `docs/parity-symbol-manifest.{md,json}`, and `docs/parity-checklist.{md,json}`.
- `python tools/generate_symbol_inventory.py` - regenerates the layered symbol inventory.
- `python tools/refine_parity_checklist.py` - post-processes `docs/parity-checklist.json` with inferred mappings.
- `python tools/generate_diskdefs_xml.py` - converts `python_source/src/greaseweazle/data/*.cfg` into the embedded XML resources under `src/Greaseweazle/data/`.
- `python tools/generate_parity_fixtures.py` - regenerates the JSON fixtures under `src/Greaseweazle.Parity/Fixtures/`.
