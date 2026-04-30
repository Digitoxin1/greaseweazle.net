# Greaseweazle VB.NET

A VB.NET port of the Python [Greaseweazle](https://github.com/keirf/greaseweazle) host tools, split into a reusable class library and a command-line front-end. Targets .NET Framework 4.7.1.

The library is text-free and event-driven: any application (GUI, service, scripting host, parity harness, etc.) can drive a Greaseweazle V4 by referencing `Greaseweazle.dll` directly. The CLI (`gw-vb.exe`) is a thin shell that parses argv, drives the library, and renders output to match the Python `gw` reference.

The current build aligns with upstream Greaseweazle `1.23` and is verified at runtime parity against `python_source/` v1.23 across 24 hardware/file scenarios (info, bandwidth, reset, pin, delays, rpm, read IMG/SCP/HFE/raw, write, erase, convert, including the four user-reported regressions: `--gen-tg43` ordering, sparse-KryoFlux head-bang, `convert ::bitrate=N`, and HFE read without `--format`).

## Repository Layout

```
.
├── Directory.Build.props        Shared MSBuild props (TargetFramework=net471, Option Strict On, etc.)
├── Greaseweazle.VbNet.slnx      Solution file (open in Visual Studio or build via dotnet)
├── README.md
├── docs/                        Parity audit reports and symbol manifests
├── python_source/               Upstream Python greaseweazle source (read-only reference)
├── tools/                       Python utilities for parity / codegen audits
└── src/
    ├── Greaseweazle/            Class library -> Greaseweazle.dll
    │   ├── actions/             GreaseweazleEngine façade + per-action XCommand classes
    │   ├── codec/               Track codecs (ibm, amiga, apple2, c64, mac, hp, ...)
    │   ├── data/                Embedded diskdefs.xml resources
    │   ├── error/               Custom exceptions + LibraryDiagnostics event hub
    │   ├── flux/                Flux representation and conversions
    │   ├── image/               Image format readers/writers (HFE, SCP, IMG, IMD, KryoFlux, ...)
    │   ├── optimised/           Performance-critical helpers (PLL, CRC, MFM)
    │   ├── tools/               Algorithm bodies invoked by XCommand.Run
    │   ├── track/               Master-track types and track-set parsing
    │   └── usb/                 Serial/USB bootloader and device protocol
    └── Greaseweazle.Cli/        Console front-end -> gw-vb.exe
        ├── Driver.vb            Entry point: argv routing, action dispatch, exit codes
        ├── UsageText.vb         All --help / usage text lives here
        ├── Parsers/             One *OptionsParser.vb per action; argv -> XOptions DTO
        ├── Formatters/          One *Formatter.vb per action; XEventArgs/XSummary -> stdout
        └── Prompts/             ConsoleSeekPrompter (ISeekPrompter implementation)
```

## Architecture

### Library (`Greaseweazle.dll`)

All hardware and image-handling logic lives here. The library never writes to `Console.Out`/`Console.Error`; instead it raises events and throws strongly-typed exceptions.

- **`GreaseweazleEngine`** — façade exposing one command per action (`Info`, `Read`, `Write`, `Convert`, `Erase`, `Clean`, `Seek`, `Delays`, `Update`, `Pin`, `Reset`, `Bandwidth`, `Rpm`, `Align`).
- **Strongly-typed options** — every action takes an `XOptions` DTO (e.g. `ReadOptions`, `WriteOptions`, `ConvertOptions`).
- **Two result patterns:**
  - One-shot commands (`Info`, `Bandwidth`, `Reset`, `Pin`, `Delays`) return an `XSummary`/`XResult` object.
  - Streaming commands (`Read`, `Write`, `Convert`, `Erase`, `Clean`, `Seek`, `Update`, `Rpm`, `Align`) raise progress events (`XEventArgs`) and return a final summary.
- **`ISeekPrompter`** — interactive callback used by `Seek` for extreme-cylinder confirmation; the CLI implements it via `ConsoleSeekPrompter`.
- **`LibraryDiagnostics.MessageEmitted`** — global event for informational/warning text the library wants surfaced; the CLI wires it to `Console.Out`.
- **Errors** — `CmdError` for protocol/device failures and `FatalException` (with typed subclasses such as `UnrecognisedSuffixException`, `UnknownFormatException`, `Track0SeekMismatchException`, `CapsLibraryNotFoundException`, `DeviceInUpdateModeException`) for user-facing fatal errors. All carry structured data; the CLI's `FatalErrorFormatter` renders them.

### CLI (`gw-vb.exe`)

`Greaseweazle.Cli` is the only place that knows about argv parsing, usage text, console output, prompting, and exit codes.

- `Driver.vb` parses the action token, hands argv to the matching `*OptionsParser`, instantiates a fresh `GreaseweazleEngine` command, subscribes formatters to its events, and runs it.
- `Parsers/` builds `XOptions` DTOs from argv (validates `--format`, parses `--tracks`, splits `path::opt=val::opt2=val2`, etc.).
- `Formatters/` translates the library's structured events/summaries into the exact text Python `gw` produces.
- `UsageText.vb` holds every `--help` block (output stays in lock-step with Python `gw 1.23`).

## Building

Visual Studio: open `Greaseweazle.VbNet.slnx`.

Command line:

```bash
# Build everything (library + CLI):
dotnet build Greaseweazle.VbNet.slnx -c Debug

# Or just the CLI (pulls library by ProjectReference):
dotnet build src/Greaseweazle.Cli/Greaseweazle.Cli.vbproj -c Debug
```

Outputs:

- `src/Greaseweazle/bin/Debug/net471/Greaseweazle.dll`
- `src/Greaseweazle.Cli/bin/Debug/net471/gw-vb.exe`

## Running the CLI

```bash
gw-vb info
gw-vb read --drive 1 --tracks=c=0-82 --format=ibm.1440 disk.img
gw-vb write --drive 1 --format=ibm.1440 disk.img
gw-vb convert disk.img disk.hfe --format=ibm.1440
gw-vb convert disk.hfe disk.scp::bitrate=500
gw-vb erase --drive 1 --tracks=c=0-79
gw-vb rpm --drive 1
gw-vb pin get 2
```

Exit codes match Python `gw`: `0` on success, `1` on `FatalException`/`CmdError`, `130` on Ctrl-C.

## Consuming the library

The DLL targets .NET Framework 4.7.1, so any net471+ host (WinForms, WPF, console, service) can reference it directly. Minimum example:

```vbnet
Imports Greaseweazle.Actions
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Tools

Module Example
    Sub Main()
        Dim engine As New GreaseweazleEngine()

        ' One-shot: info command returns a typed DeviceInfoResult.
        Dim info = engine.Info.Run(New InfoOptions())
        Console.WriteLine($"Port {info.Device.Port}, firmware {info.Device.FirmwareMajor}.{info.Device.FirmwareMinor}")

        ' Streaming: subscribe to events before calling Run.
        AddHandler engine.Read.TrackProcessed,
            Sub(sender, e)
                Console.WriteLine($"T{e.Track.Cyl}.{e.Track.Head}: {e.FluxSummary}")
            End Sub

        engine.Read.Run(New ReadOptions With {
            .FileName = "disk.img",
            .Format = "ibm.1440",
            ' TrackSet is a TrackSetSpec carrying *user intent*; the engine
            ' folds it onto the format's default range at runtime.
            ' Pass Nothing to accept format defaults verbatim.
            .TrackSet = New TrackSetSpec("c=0-82"),
            .Drive = New DriveSpec With {.Bus = UsbProtocol.BusType.IBMPC, .UnitId = 1}
        })
    End Sub
End Module
```

Notes for embedders:

- Per-action options live in `Greaseweazle.Tools` (`InfoOptions`, `ReadOptions`, `WriteOptions`, ...); commands and result/event types live in `Greaseweazle.Actions`.
- For interactive flows such as `Seek`, supply an `ISeekPrompter` implementation (or a no-op stub for unattended hosts).
- Wire `LibraryDiagnostics.MessageEmitted` if you want to mirror Python's informational/warning lines (printer, no-IPF-DLL hint, etc.) into your own logger.
- All fatal errors surface as `FatalException` (or one of its subclasses); structured fields on each subclass (e.g. `UnrecognisedSuffixException.KnownSuffixes`) let you build your own UI without parsing strings.

### CAPS / IPF support

`.ipf` images are read through SPS's CAPSImg native library, which is not redistributed with this repo. To enable IPF reads:

1. Obtain `CAPSImg.dll` (e.g. from the upstream Greaseweazle Windows release at <https://github.com/keirf/greaseweazle/releases>) and drop it next to `gw-vb.exe`, or somewhere on the loader path.
2. Confirm that the DLL bitness matches the `gw-vb.exe` process (AnyCPU on a 64-bit OS resolves to a 64-bit process and needs the x64 build of `CAPSImg.dll`).
3. The library probes `CAPSImg_x64.dll` first and falls back to `CAPSImg.dll`; either filename works.

If the library is missing, the Greaseweazle DLL throws `CapsLibraryNotFoundException` (the CLI renders the wiki link plus a probe-error trace).

## Parity Tooling

The `tools/` Python scripts and `docs/` artifacts support ongoing parity auditing against `python_source/`. They read `python_source/src/greaseweazle/...` and the VB sources under `src/Greaseweazle/` and regenerate the manifests:

- `python tools/audit_conversion.py` — refreshes `docs/conversion-audit.{md,json}`, `docs/parity-symbol-manifest.{md,json}`, and `docs/parity-checklist.{md,json}`.
- `python tools/generate_symbol_inventory.py` — regenerates the layered symbol inventory.
- `python tools/refine_parity_checklist.py` — post-processes `docs/parity-checklist.json` with inferred mappings.
- `python tools/generate_diskdefs_xml.py` — converts upstream `python_source/src/greaseweazle/data/*.cfg` files into the embedded XML resources under `src/Greaseweazle/data/`.
- `python tools/generate_parity_fixtures.py` — produces fixtures for ad-hoc comparison runs.

For a behavioural / runtime-output parity status (what's been exercised against `gw.exe` v1.23 at runtime), see [`docs/runtime-test-checklist.md`](docs/runtime-test-checklist.md). The static `docs/parity-checklist.md` covers Python→VB symbol coverage; the runtime checklist tracks live-hardware tests, malformed-input sweeps, argparse-contract spot-checks, and remaining gaps.

## Versioning

`Directory.Build.props` pins `Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion` to mirror the upstream Greaseweazle release the port targets (currently `1.23`). `gw-vb info` reports both numbers:

```
Host Tools: 1.23     <- DLL InformationalVersion
CLI:        1.23     <- CLI InformationalVersion
Device:
  Port:     COM3
  Model:    Greaseweazle V4
  ...
```
