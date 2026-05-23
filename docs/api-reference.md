# Greaseweazle.dll — Public API Reference

This document catalogs the public surface of the Greaseweazle library
assembly (`Greaseweazle.dll`) as consumed by the CLI front-end and any
external host (GUI, automation script, test harness). Internal types
(those declared `Friend` or in the `Greaseweazle.Optimised`,
`Greaseweazle.Codecs`, `Greaseweazle.Images`, and platform helpers in
`Greaseweazle.Tools.WindowsPortDiscovery`) are excluded; consumers
should use the typed `Actions` façade instead of the lower-level
codec/image plumbing.

The library follows an explicit shape:

1. Construct a `GreaseweazleEngine`.
2. Pull a fresh per-action `*Command` from it.
3. Subscribe to that command's typed events.
4. Build the matching `*Options` DTO.
5. Call `cmd.Run(options, ct)` and read the typed `*Summary` /
   `*Result` it returns.

`Run` is synchronous and blocks the caller. Events are raised on the
calling thread; UI hosts should call `Run` from a worker (`Task.Run`)
and marshal handler bodies back to the UI thread.

---

## Namespaces at a glance

| Namespace                        | Purpose                                                                  |
| -------------------------------- | ------------------------------------------------------------------------ |
| `Greaseweazle.Actions`           | Engine façade, command classes, typed event args, result/summary DTOs    |
| `Greaseweazle.Tools`             | Per-action options DTOs, drive/port helpers, file-extension registry     |
| `Greaseweazle.Shared`            | `TrackSet` / `TrackIter` and the `OptionParser` helper                   |
| `Greaseweazle.Core`              | Diagnostics hub, host-version probe, fatal/argparse exception hierarchy  |
| `Greaseweazle.Infrastructure`    | USB protocol enums + `CmdError`                                          |
| `Greaseweazle.Codecs`            | `DiskDef`, `CodecRegistry` — only needed when wiring a custom format     |

---

## 1. `GreaseweazleEngine`

Top-level façade. The engine itself is stateless; each `*Command`
property returns a fresh, single-use command instance.

```vb
Public Class GreaseweazleEngine
    Public ReadOnly Property Info       As InfoCommand
    Public ReadOnly Property Read       As ReadCommand
    Public ReadOnly Property Write      As WriteCommand
    Public ReadOnly Property Convert    As ConvertCommand
    Public ReadOnly Property [Erase]    As EraseCommand
    Public ReadOnly Property Clean      As CleanCommand
    Public ReadOnly Property Seek       As SeekCommand
    Public ReadOnly Property Delays     As DelaysCommand
    Public ReadOnly Property Update     As UpdateCommand
    Public ReadOnly Property Pin        As PinCommand
    Public ReadOnly Property Reset      As ResetCommand
    Public ReadOnly Property Bandwidth  As BandwidthCommand
    Public ReadOnly Property Rpm        As RpmCommand
    Public ReadOnly Property Align      As AlignCommand
End Class
```

Commands inherit from `GwCommandBase`, an empty `MustInherit` base class
whose only protected method is `ThrowIfCancellationRequested(ct)`.

---

## 2. Command reference

Each section lists the command's events, its `Run` signature, the
options DTO it consumes, and the result/summary it returns. Recoverable
USB errors raise `Greaseweazle.Infrastructure.CmdError` from `Run`;
`OperationCanceledException` is raised when the supplied cancellation
token is signalled (or, internally, on Ctrl-C).

### 2.1 `InfoCommand`

One-shot device probe. No events.

```vb
Function Run(options As InfoOptions,
             Optional cancellationToken As CancellationToken = Nothing) As DeviceInfoResult
```

- **`InfoOptions`**
  - `Bootloader As Boolean` — probe the bootloader slot
  - `Live As Boolean = True` — `False` is `--test` dry-run
  - `Device As String` — explicit COM port; `Nothing` auto-discovers
- **Return: `DeviceInfoResult`** — see §3.1

Multi-device variant, for when the caller wants every connected
Greaseweazle rather than just the best-scoring one:

```vb
Function EnumerateDevices(Optional cancellationToken As CancellationToken = Nothing) As IReadOnlyList(Of DeviceInfoResult)
```

- Parameterless (beyond the cancellation token). Always probes every
  port whose `ToolOptions.ScorePort` is greater than zero.
- Each list entry is a regular `DeviceInfoResult` with
  `ConnectionState = Connected` (failed probes are silently skipped) and
  `Device.FirmwareUpdate = Nothing` (no network call is made — the GitHub
  `LatestFirmware` lookup is intentionally skipped here for speed and
  to keep `EnumerateDevices` offline-safe).
- List is sorted by `ScorePort` descending so
  `list.FirstOrDefault()?.Device` matches what `Run(...)` would have
  picked.
- Read-only by design: no mode switching is performed, so devices
  currently in bootloader mode are reported as `IsBootloader = True`
  rather than being switched into firmware mode just to be enumerated.
- Returns an empty list when no Greaseweazles are detected.
- For targeted single-device probing, for `--test` dry-run, for
  `--bootloader` mode switching, or for the firmware-update banner,
  use `Run(InfoOptions)` instead.

### 2.2 `ReadCommand`

Streaming command that reads each track in `ReadOptions.TrackSet`,
optionally retries, decodes via the chosen format codec, and writes to
the resolved output image.

| Event                       | Args                              | When it fires                                              |
| --------------------------- | --------------------------------- | ---------------------------------------------------------- |
| `Started`                   | `ReadStartedEventArgs`            | Once after track-set/format resolution                     |
| `HardSectorsDetected`       | `HardSectorsDetectedEventArgs`    | Once when `--hard-sectors` auto-detection succeeds         |
| `TrackProcessed`            | `TrackProcessedEventArgs`         | Once per attempt (initial + each retry); exposes `DecodedSectorsFound`/`DecodedSectorsTotal` and (when the source is a raw Flux) `FluxSampleCount`/`FluxDurationMs` so hosts don't have to parse `DecodedSummary` / `FluxSummary` |
| `TrackGaveUp`               | `ReadTrackGaveUpEventArgs`        | Once when retry budget is exhausted                        |
| `SummaryReady`              | `SectorSummaryReadyEventArgs`     | Once after decode if codec produced a grid                 |
| `UnexpectedSectorIgnored`   | `UnexpectedSectorEventArgs`       | Per unique `(C,H,R,N)` sector header that doesn't match the format layout (may fire multiple times across retries) |

```vb
Function Run(options As ReadOptions,
             Optional cancellationToken As CancellationToken = Nothing) As ReadSummary
```

- **`ReadOptions`** (see §4.2 — superset of common track/drive fields)
- **Return: `ReadSummary`** — see §3.2

### 2.3 `WriteCommand`

Streaming command that reads each track from `WriteOptions.FileName`,
decodes via codec, scales flux to drive speed, and writes (with optional
verify + retries) to the unit.

| Event                       | Args                              | When it fires                                        |
| --------------------------- | --------------------------------- | ---------------------------------------------------- |
| `Started`                   | `WriteStartedEventArgs`           | Once at start                                        |
| `HardSectorsDetected`       | `HardSectorsDetectedEventArgs`    | Once when `--hard-sectors` succeeds                  |
| `TrackErasing`              | `WriteTrackErasingEventArgs`      | Per erased track (`--erase-empty` or `--pre-erase`)  |
| `TrackOutOfRange`           | `WriteTrackOutOfRangeEventArgs`   | When codec rejects an input track                    |
| `TrackWriting`              | `WriteTrackWritingEventArgs`      | Per write attempt (first + each verify retry)        |
| `VerifyCompleted`           | `WriteVerifyOutcomeEventArgs`     | Once at end with verify tally                        |
| `UnexpectedSectorIgnored`   | `UnexpectedSectorEventArgs`       | Per unique `(C,H,R,N)` sector header in the input image that doesn't match the format layout |

```vb
Function Run(options As WriteOptions,
             Optional cancellationToken As CancellationToken = Nothing) As WriteSummary
```

- **`WriteOptions`** — see §4.3
- **Return: `WriteSummary`** — see §3.3

### 2.4 `ConvertCommand`

Streaming image-to-image conversion (no USB activity).

| Event                       | Args                                | When it fires                                  |
| --------------------------- | ----------------------------------- | ---------------------------------------------- |
| `Started`                   | `ConvertStartedEventArgs`           | Once before per-track work                     |
| `HardSectorsApplied`        | `ConvertHardSectorsEventArgs`       | When `--hard-sectors` resolves a track's count |
| `TrackProcessed`            | `TrackProcessedEventArgs`           | Once per input track; exposes `DecodedSectorsFound`/`DecodedSectorsTotal` and (when the input is a raw Flux) `FluxSampleCount`/`FluxDurationMs` so hosts don't have to parse `DecodedSummary` / `FluxSummary` |
| `SummaryReady`              | `SectorSummaryReadyEventArgs`       | Once at end with the decoded sector grid       |
| `UnexpectedSectorIgnored`   | `UnexpectedSectorEventArgs`         | Per unique `(C,H,R,N)` sector header in the input image that doesn't match the format layout |

```vb
Function Run(options As ConvertOptions,
             Optional cancellationToken As CancellationToken = Nothing) As ConvertSummary
```

- **`ConvertOptions`** — see §4.4
- **Return: `ConvertSummary`** — see §3.4

### 2.5 `EraseCommand`

Streaming bulk-erase. See [Erase example](#erase-example) for end-to-end
wiring; full reference below.

| Event          | Args                       | When it fires                                       |
| -------------- | -------------------------- | --------------------------------------------------- |
| `Started`      | `EraseStartedEventArgs`    | Once before any USB activity (live and `--test`)    |
| `TrackStarted` | `EraseTrackEventArgs`      | Before each physical track is erased (live only)    |

```vb
Function Run(options As EraseOptions,
             Optional cancellationToken As CancellationToken = Nothing) As EraseSummary
```

- **`EraseOptions`** — see §4.5
- **Return: `EraseSummary`** — see §3.5

### 2.6 `CleanCommand`

Streaming head-clean schedule (no codec work).

| Event           | Args                              | When it fires                       |
| --------------- | --------------------------------- | ----------------------------------- |
| `PassStarted`   | `CleanPassStartedEventArgs`       | Once at start of each pass          |
| `CylinderSeeked`| `CleanCylinderEventArgs`          | After each cylinder is reached      |
| `PassCompleted` | `CleanPassCompletedEventArgs`     | Once at end of each pass            |

```vb
Function Run(options As CleanOptions,
             Optional cancellationToken As CancellationToken = Nothing) As CleanSummary
```

- **`CleanOptions`** — see §4.6
- **Return: `CleanSummary`** — see §3.6

### 2.7 `SeekCommand`

Interactive command. Calls `ISeekPrompter.ConfirmExtremeCylinder` for
cyl < 0 or cyl > 83 unless `--force` is set; without a prompter the
algorithm aborts (default-deny).

```vb
Public Property Prompter As ISeekPrompter

Function Run(options As SeekOptions,
             Optional cancellationToken As CancellationToken = Nothing) As SeekResult
```

- **`SeekOptions`** — see §4.7
- **Return: `SeekResult`** — see §3.7
- **`ISeekPrompter`** — see §5.3

### 2.8 `DelaysCommand`

One-shot read (and optional write) of the device delay block. No events.

```vb
Function Run(options As DelaysOptions,
             Optional cancellationToken As CancellationToken = Nothing) As DelaysResult
```

Returns `Nothing` for `--test` dry-run mode.

- **`DelaysOptions`** — see §4.8
- **Return: `DelaysResult`** — see §3.8

### 2.9 `UpdateCommand`

Streaming firmware updater.

| Event             | Args                              | When it fires                          |
| ----------------- | --------------------------------- | -------------------------------------- |
| `DownloadStarted` | `UpdateDownloadStartedEventArgs`  | Before GitHub asset GET / file load    |
| `UpdateStarted`   | `UpdateStartedEventArgs`          | Once after payload is selected         |

```vb
Function Run(options As UpdateOptions,
             Optional cancellationToken As CancellationToken = Nothing) As UpdateSummary
```

- **`UpdateOptions`** — see §4.9
- **Return: `UpdateSummary`** — see §3.9

### 2.10 `PinCommand`

One-shot get/set of a device pin. No events.

```vb
Function Run(options As PinOptions,
             Optional cancellationToken As CancellationToken = Nothing) As PinResult
```

- **`PinOptions`** — see §4.10
- **Return: `PinResult`** — see §3.10

### 2.11 `ResetCommand`

One-shot device reset. No events; returns nothing.

```vb
Sub Run(options As ResetOptions,
        Optional cancellationToken As CancellationToken = Nothing)
```

- **`ResetOptions`** — see §4.11

### 2.12 `BandwidthCommand`

One-shot bidirectional USB throughput probe. No events.

```vb
Function Run(options As BandwidthOptions,
             Optional cancellationToken As CancellationToken = Nothing) As BandwidthResult
```

Returns `Nothing` in `--test` mode.

- **`BandwidthOptions`** — see §4.12
- **Return: `BandwidthResult`** — see §3.11

### 2.13 `RpmCommand`

Streaming spindle-period sampler.

| Event           | Args                              | When it fires                                    |
| --------------- | --------------------------------- | ------------------------------------------------ |
| `SampleMeasured`| `RpmSampleMeasuredEventArgs`      | Once per `Nr` loop iteration                     |
| `SummaryReady`  | `RpmSummaryReadyEventArgs`        | Once when at least 2 samples were collected      |

```vb
Function Run(options As RpmOptions,
             Optional cancellationToken As CancellationToken = Nothing) As RpmSummary
```

- **`RpmOptions`** — see §4.13
- **Return: `RpmSummary`** — see §3.12

### 2.14 `AlignCommand`

Streaming repeated-read for drive alignment.

| Event                | Args                                 | When it fires                                |
| -------------------- | ------------------------------------ | -------------------------------------------- |
| `Started`            | `AlignStartedEventArgs`              | Once after live-mode adjustments             |
| `HardSectorsDetected`| `HardSectorsDetectedEventArgs`       | When `--hard-sectors` succeeds               |
| `ReadCompleted`      | `TrackProcessedEventArgs`            | Per read pass; exposes `DecodedSectorsFound`/`DecodedSectorsTotal` and `FluxSampleCount`/`FluxDurationMs` so hosts don't have to parse `DecodedSummary` / `FluxSummary` |

```vb
Function Run(options As AlignOptions,
             Optional cancellationToken As CancellationToken = Nothing) As AlignSummary
```

- **`AlignOptions`** — see §4.14
- **Return: `AlignSummary`** — see §3.13

---

## 3. Result / summary types (`Greaseweazle.Actions`)

Every type below is `NotInheritable` (i.e. `sealed`). All properties are
read-only.

### 3.1 `DeviceInfoResult`

```vb
Public ReadOnly Property HostToolsVersion  As String
Public ReadOnly Property ConnectionState   As DeviceConnectionState
Public ReadOnly Property Device            As DeviceInfoBlock      ' nullable
```

`DeviceConnectionState` enum:

| Value      | Meaning                                       |
| ---------- | --------------------------------------------- |
| `TestMode` | `--test` dry-run; only `HostToolsVersion` set |
| `NotFound` | Probe attempted but port unavailable          |
| `Connected`| `Device` is populated                         |

`DeviceInfoBlock` (populated when `Connected`):

```vb
Port              As String
HwModel           As Integer
HwSubmodel        As Integer
McuId             As Integer
McuMhz            As Integer
McuSramKb         As Integer
FirmwareMajor     As Integer
FirmwareMinor     As Integer
IsBootloader      As Boolean
SerialNumber      As String
UsbSpeedRaw       As Integer
UsbBufferKb       As Integer
JumperlessUpdate  As Boolean
FirmwareUpdate    As FirmwareUpdateInfo   ' nullable
```

`FirmwareUpdateInfo` (populated when a newer GitHub release is found):

```vb
LatestMajor As Integer
LatestMinor As Integer
```

### 3.2 `ReadSummary`

```vb
Tracks            As String
RevsDisplay       As String
TracksProcessed   As Integer
FormatName        As String              ' nullable
OutputPath        As String              ' nullable in --test
Grid              As SectorSummaryGrid   ' nullable
DryRun            As Boolean
```

### 3.3 `WriteSummary`

```vb
Tracks            As String
FormatName        As String              ' nullable
Outcome           As WriteVerifyOutcome
VerifiedCount     As Integer
NotVerifiedCount  As Integer
DryRun            As Boolean
```

`WriteVerifyOutcome` enum: `AllVerified`, `VerifyDisabled`, `VerifyUnavailable`.

### 3.4 `ConvertSummary`

```vb
InTracks         As String
OutTracks        As String
TracksProcessed  As Integer
FormatName       As String               ' nullable
Grid             As SectorSummaryGrid    ' may be empty
```

### 3.5 `EraseSummary`

```vb
Tracks           As String
Revs             As Integer
TracksProcessed  As Integer   ' 0 in dry-run
DryRun           As Boolean
```

### 3.6 `CleanSummary`

```vb
Cyls              As Integer
Passes            As Integer
CylindersVisited  As Integer  ' 0 in dry-run
DryRun            As Boolean
```

### 3.7 `SeekResult`

```vb
Outcome   As SeekOutcome
Cylinder  As Integer
```

`SeekOutcome` enum: `DryRun`, `Aborted`, `Seeked`.

### 3.8 `DelaysResult`

```vb
SelectDelayMicros  As Integer
StepDelayMicros    As Integer
SettleTimeMillis   As Integer
MotorDelayMillis   As Integer
WatchdogMillis     As Integer
PreWriteMicros     As Integer?   ' firmware-gated
PostWriteMicros    As Integer?   ' firmware-gated
IndexMaskMicros    As Integer?   ' firmware-gated
```

`Run` returns `Nothing` for `--test` dry-run.

### 3.9 `UpdateSummary`

```vb
Target         As UpdateTarget
Outcome        As UpdateOutcome
AppliedMajor   As Integer
AppliedMinor   As Integer
DeviceMajor    As Integer       ' meaningful when Outcome = Skipped
DeviceMinor    As Integer
AckCode        As Integer       ' meaningful when Outcome = Failed
NeedsUnplug    As Boolean
```

`UpdateTarget` enum: `MainFirmware`, `Bootloader`.
`UpdateOutcome` enum: `DryRun`, `Completed`, `Failed`, `Skipped`.

### 3.10 `PinResult`

```vb
Kind   As PinResultKind
Pin    As Integer    ' meaningful for PinSet/PinValue
Level  As Boolean    ' meaningful for PinSet/PinValue
```

`PinResultKind` enum: `NoOp`, `UsageRequested`, `PinSet`, `PinValue`.

### 3.11 `BandwidthResult`

```vb
WriteRow      As BandwidthRow
WriteGarbled  As Boolean
ReadRow       As BandwidthRow      ' Nothing if WriteGarbled
ReadGarbled   As Boolean
Summary       As BandwidthSummary  ' Nothing unless both directions OK
```

`BandwidthRow` (Mbps): `Min`, `Mean`, `Max` — all `Double`.

`BandwidthSummary`:

```vb
EstimatedMinMbps   As Double
BelowRequirement   As Boolean
RequiredMinMbps    As Double   ' meaningful when BelowRequirement
MaxFluxRateMsps    As Double   ' meaningful when not BelowRequirement
MinAvgFluxUs       As Double   ' meaningful when not BelowRequirement
```

`Run` returns `Nothing` in `--test` dry-run.

### 3.12 `RpmSummary`

```vb
Samples    As IReadOnlyList(Of Double)   ' seconds per rev
Fastest    As Double
Slowest    As Double
Mean       As Double
Median     As Double
Completed  As Boolean
DryRun     As Boolean
```

Convert seconds-per-rev to RPM with `60.0 / value`.

### 3.13 `AlignSummary`

```vb
Reads           As Integer
ReadsCompleted  As Integer   ' 0 in dry-run
DryRun          As Boolean
```

### 3.14 `SectorSummaryGrid`

Carried by `SectorSummaryReadyEventArgs` and on `Read`/`Convert` summaries.

```vb
Cyls          As IReadOnlyList(Of Integer)
Heads         As IReadOnlyList(Of Integer)
Rows          As IReadOnlyList(Of SectorSummaryRow)
TotalSectors  As Integer
GoodSectors   As Integer
HasContent    As Boolean   ' Rows.Count > 0
```

`SectorSummaryRow`: `Head`, `Sector`, `Cells As IReadOnlyList(Of SectorSummaryCell)`.
`SectorSummaryCell` enum: `Empty`, `Good`, `Bad`.

---

## 4. Options DTOs (`Greaseweazle.Tools`)

Every options class is a plain mutable POCO; populate via property
initializers. Every command that talks to a Greaseweazle device exposes
the same trio of host fields:

- `Live As Boolean = True` — set `False` for dry-run (no USB).
- `Device As String` — explicit COM port, or `Nothing` to auto-discover.
- `Drive As DriveSpec` — bus + unit selector (see §5.1).

Every read/write/erase/align/convert command accepts a track set via:

- `TrackSet As TrackSetSpec` — the user's **partial intent** (e.g. only
  the keys named in `--tracks`). Pass `Nothing` to accept format
  defaults verbatim, or build one with `New TrackSetSpec("c=0-1")` /
  `New TrackSetSpec()` + property setters.

The engine folds the spec against the format's default track range
inside `RunFromOptions`, so callers no longer need to call
`TrackResolution.ResolveDefaultTracks*` themselves. See §5.4 for the
`TrackSetSpec` reference and §5.5 for `TrackResolution.ResolveSpec`,
the engine-side helper.

### 4.1 `InfoOptions`
```vb
Bootloader  As Boolean
Live        As Boolean = True
Device      As String
```

### 4.2 `ReadOptions`
```vb
FileName         As String                ' output image path
Format           As String                ' codec format name (nullable)
DiskDefsPath     As String                ' optional override for diskdefs.xml
TrackSet         As TrackSetSpec          ' --tracks user intent (partial)
Revs             As Integer
FractionalRevs   As Double?               ' format-default revs (e.g. 1.1)
RevsDisplay      As String                ' formatted revs string
Raw              As Boolean
HardSectors      As Boolean
Reverse          As Boolean
NoClobber        As Boolean
AdjustSpeed      As Double?
PllProfiles      As IReadOnlyList(Of Pll)
FakeIndexPeriod  As Double?
GenTg43          As Boolean
Densel           As Boolean?
Retries          As Integer
SeekRetries      As Integer
Live, Device, Drive (common)
```

### 4.3 `WriteOptions`
```vb
FileName         As String                ' input image path
Format           As String
DiskDefsPath     As String
Precomp          As String                ' raw spec text
PrecompSpec      As String                ' resolved spec object reference
TrackSet         As TrackSetSpec          ' --tracks user intent (partial)
PreErase         As Boolean
EraseEmpty       As Boolean
HardSectors      As Boolean
NoVerify         As Boolean
Reverse          As Boolean
GenTg43          As Boolean
Densel           As Boolean?
FakeIndexPeriod  As Double?
Retries          As Integer
Live, Device, Drive (common)
```

### 4.4 `ConvertOptions`
```vb
InputFile        As String
OutputFile       As String
Format           As String                ' explicit --format (nullable)
DiskDefsPath     As String
TrackSet         As TrackSetSpec          ' --tracks user intent (partial)
OutTrackSet      As TrackSetSpec          ' --out-tracks user intent (partial)
NoClobber        As Boolean
HardSectors      As Boolean
Reverse          As Boolean
AdjustSpeed      As Double?
PllProfiles      As IReadOnlyList(Of Pll)
```

`TrackSet` and `OutTrackSet` are `TrackSetSpec` (see §5.5) — partial,
user-intent objects whose Cyls/Heads (empty list) and `Step` / `Hswap`
/ `HOff` (Nothing) record exactly which keys the user named. The
engine merges these against format defaults inside
`RunFromOptions` once the input image has been opened.

Construct from a spec string:

```vb
Dim opts As New ConvertOptions With {
    .InputFile = "in.scp",
    .OutputFile = "out.img",
    .TrackSet    = New TrackSetSpec("c=0-2:h=0"),   ' partial
    .OutTrackSet = New TrackSetSpec(""),            ' empty = pure format defaults
    .PllProfiles = New List(Of Pll)()
}
```

Or leave both `Nothing` to accept format defaults verbatim.

### 4.5 `EraseOptions`
```vb
TrackSet     As TrackSetSpec  ' --tracks user intent (partial)
Revs         As Integer
Hfreq        As Boolean       ' high-frequency flux pattern instead of EraseTrack opcode
FakeIndex    As Double?
Live         As Boolean = True
Device       As String
Drive        As DriveSpec
```

### 4.6 `CleanOptions`
```vb
Cyls       As Integer
PassLines  As List(Of String)
Sequences  As List(Of List(Of Integer))
LingerMs   As Integer
Live, Device, Drive (common)
```

### 4.7 `SeekOptions`
```vb
Cyl            As Integer
MotorOn        As Boolean
Force          As Boolean
PromptNeeded   As Boolean
Live, Device, Drive (common)
```

### 4.8 `DelaysOptions`
```vb
Values   As Dictionary(Of String, Integer)   ' "step" / "settle" / "pre-write" / ...
Live     As Boolean = True
Device   As String
```

### 4.9 `UpdateOptions`
```vb
FileValue    As String   ' --file local payload
TagValue     As String   ' --tag GitHub release tag
Force        As Boolean
Bootloader   As Boolean
Live         As Boolean = True
Device       As String
```

Supporting DTOs in the `Greaseweazle.Tools` namespace used during Update:

- `UpdatePayload { Name, Data }` — selected firmware bytes
- `ExtractedUpdate { Major, Minor, Payload }` — validated payload metadata
- `SkipUpdate(message)` — exception raised when no update is needed

### 4.10 `PinOptions`
```vb
Mode    As String       ' "set" / "get"
Live    As Boolean = True
Device  As String
Drive   As DriveSpec
Pin     As Integer
Level   As Boolean
```

### 4.11 `ResetOptions`
```vb
DelaysFlag  As Boolean   ' --delays: also restores delay block
Live        As Boolean = True
Device      As String
```

### 4.12 `BandwidthOptions`
```vb
Live    As Boolean = True
Device  As String
```

### 4.13 `RpmOptions`
```vb
Nr      As Integer
Live, Device, Drive (common)
```

### 4.14 `AlignOptions`
```vb
Format           As String
DiskDefsPath     As String
FormatDef        As DiskDef
Reads            As Integer
Revs             As Integer
FractionalRevs   As Double?
Ticks            As Integer
TrackSet         As TrackSetSpec          ' --tracks user intent (partial)
Raw              As Boolean
HardSectors      As Boolean
Reverse          As Boolean
AdjustSpeed      As Double?
PllProfiles      As IReadOnlyList(Of Pll)
FakeIndexPeriod  As Double?
GenTg43          As Boolean
Densel           As Boolean?
Live, Device, Drive (common)
```

The engine resolves `TrackSet` against `FormatDef.Tracks` (or
`"c=0-81:h=0-1"` when no format applies) and runs
`Align.ValidateTrackCylinders` on the resolved set inside
`RunFromOptions`. The single-track-vs-multi-track header banner that
the CLI prints is built from the resolved set inside
`AlignFormatter`, not stored on the DTO.

---

## 5. Supporting types (`Greaseweazle.Tools` / `Greaseweazle.Shared`)

### 5.1 `DriveSpec`

```vb
Public Class DriveSpec
    Public Property Bus     As UsbProtocol.BusType   ' Invalid | IBMPC | Shugart
    Public Property UnitId  As Integer
End Class
```

CLI mapping (replicate these in your host UI as needed):

| Token | Bus     | UnitId |
| ----- | ------- | ------ |
| `A`   | IBMPC   | 0      |
| `B`   | IBMPC   | 1      |
| `0`   | Shugart | 0      |
| `1`   | Shugart | 1      |
| `2`   | Shugart | 2      |
| `3`   | Shugart | 3      |

### 5.2 `PortDescriptor`

```vb
Device, Manufacturer, Product As String
Vid, Pid As Integer
SerialNumber, Location, [Interface] As String
```

Returned indirectly by `ToolOptions` helpers when enumerating devices.

### 5.3 `ISeekPrompter`

```vb
Public Interface ISeekPrompter
    Function ConfirmExtremeCylinder(cyl As Integer) As Boolean
End Interface
```

Set on `SeekCommand.Prompter` to handle the cyl < 0 / cyl > 83 safety
prompt. Return `True` to allow the seek; `False` aborts the run with
`SeekOutcome.Aborted`. If `Prompter` is `Nothing` and a prompt is
needed, the algorithm aborts (default-deny).

### 5.4 `TrackSet`, `TrackSetSpec`, and `TrackIter` (`Greaseweazle.Shared`)

`TrackSet` is the **fully-resolved** track set: every field carries a
concrete value (`Step` defaults to `1`, `Hswap` to `False`, `HOff` to
`{0, 0}`). It is what the engine iterates. Construct directly with a
spec string (`New TrackSet("c=0-81:h=0-1")`) or via
`TrackResolution.ResolveDefaultTracks(default, requested)` (see §5.5)
which mirrors the CLI's defaulting behaviour.

```vb
New(trackSpecValue As String)
Cyls       As List(Of Integer)
Heads      As List(Of Integer)
HOff       As Integer()         ' length 2
[Step]     As Integer
Hswap      As Boolean
Trackspec  As String
Function ChToPch(cyl, head)  As Tuple(Of Integer, Integer)
Sub UpdateFromTrackspec(spec As String)
Function Contains(cyl, head)  As Boolean
Function Contains(key)         As Boolean      ' Tuple(Of Integer, Integer)
Iterator Function IteratePhysical() As IEnumerable(Of TrackIter)
Implements IEnumerable(Of TrackIter)
Overrides Function ToString() As String
```

`TrackIter`:

```vb
PhysicalCyl, PhysicalHead, Cyl, Head As Integer
```

`TrackSetSpec` is the **partial / user-intent** companion to
`TrackSet`. Every action's `*Options.TrackSet` (and
`ConvertOptions.OutTrackSet`) is a `TrackSetSpec`: the parser captures
only the keys the user named in `--tracks` (Cyls/Heads/Step/HOff/Hswap)
and the engine folds them against format defaults inside
`RunFromOptions` via `TrackResolution.ResolveSpec` (Read/Write/Erase/
Align) or `Convert.ResolveTrackSets` (Convert).

```vb
New()
New(spec As String)             ' parses spec, only fields it names get set
Cyls       As List(Of Integer)  ' Count = 0  ⇒ user did not specify
Heads      As List(Of Integer)  ' Count = 0  ⇒ user did not specify
HOff       As Integer?()        ' length 2; per-slot Nothing ⇒ unspecified
[Step]     As Integer?          ' Nothing ⇒ unspecified
Hswap      As Boolean?          ' Nothing ⇒ unspecified
Trackspec  As String            ' raw spec accumulated via UpdateFromTrackspec
Sub UpdateFromTrackspec(spec As String)
Function Resolve(defaults As TrackSet) As TrackSet
Overrides Function ToString() As String      ' emits only set fields
```

`Resolve(defaults)` returns a fully-resolved `TrackSet` where every
field is taken from `Me` if set, otherwise from `defaults`.
`ToString()` omits unspecified keys (a freshly-constructed
`TrackSetSpec()` renders as `""`).

### 5.5 `TrackResolution`

```vb
Public Shared Function ResolveSpec(spec             As TrackSetSpec,
                                   formatDefaults   As TrackSet,
                                   fallbackBaseSpec As String) As TrackSet
Public Shared Function ResolveDefaultTracks(defaultTrackSpec  As String,
                                            requestedTrackSpec As String) As TrackSet
Public Shared Function ResolveDefaultTracksFromFormat(formatTracks As TrackSet,
                                                      requestedTrackSpec As String) As TrackSet
```

`ResolveSpec` is the engine-time helper used by every
`*Action.RunFromOptions`. It folds `spec` (user intent — may be
`Nothing` or a partial `TrackSetSpec`) against `formatDefaults` (codec
defaults, may be `Nothing`) and falls back to `fallbackBaseSpec`
(typically `"c=0-81:h=0-1"`) when neither side supplies cyls/heads.

`ResolveDefaultTracks` / `ResolveDefaultTracksFromFormat` are
convenience wrappers that take spec strings instead of typed objects;
hosts that already hold a fully-resolved `TrackSet` can use them to
construct alternative track sets without round-tripping through
`TrackSetSpec`. They are not used by the engine itself anymore.

### 5.6 `ImageTypeRegistry`

```vb
Function GetKnownSuffixes()           As IEnumerable(Of String)
Function ResolveType(fileName)        As Tuple(Of String, String)   ' (className, factoryKey)
```

Throws `UnrecognisedSuffixException` for unknown extensions.

### 5.7 `ImageDefaults`

```vb
Public Shared Function DefaultFormatForFile(fileName As String) As String
Public Shared Function DefaultFormatForExtension(ext As String) As String
```

Returns the codec format name a Greaseweazle CLI host would auto-pick
for a given file (`.adf` → `"amiga.amigados"`, `.d64` → `"commodore.1541"`,
etc.); `Nothing` for extensions with no implicit format.

### 5.8 `OptionParser` (`Greaseweazle.Shared`)

```vb
Public Shared Function SplitOpts(sequence As String) As Tuple(Of String, Dictionary(Of String, String))
```

Helper that splits the `name::key=value:key2=value2` sub-option syntax
used by `--format` and a handful of other options.

### 5.9 `PrecompSpec`

```vb
Public Sub New(spec As String)
Public Property Type     As Integer    ' PrecompType.{Mfm, Fm, Gcr}
Public Property Entries  As List(Of Tuple(Of Integer, Integer))
Public Sub ImportSpec(spec As String)
Public Function TrackPrecomp(cyl As Integer) As Precomp
Public Overrides Function ToString() As String
```

Used by `WriteOptions.PrecompSpec` (string field). `Type` is an enum
exposed via `PrecompType` (`Mfm`, `Fm`, `Gcr`).

### 5.10 `ToolOptions`

Static helper class for COM-port discovery and drive-selection
scaffolding. Most consumers won't need it directly because the command
algorithms call into it internally. Notable shared members:

```vb
Shared Function ScorePort(port[, oldPort])              As Integer
Shared Function FindBestPort(ports[, oldPort])          As PortDescriptor
Shared Function FindPort(ports[, oldPort])              As String
Shared Function PortInfo(devname, ports)                As PortDescriptor
Shared Function UsbOpen(deviceName[, isUpdate, modeCheck]) As Unit
Shared Function UsbReopen(usb, isUpdate)                As Unit
Shared Function UsbModeCheck(usb, isUpdate)             As Unit
Shared Sub WithDriveSelected(action, usb, drive[, motor])
```

`Unit` here is the `Greaseweazle.Infrastructure.Unit` USB client; treat
it as opaque from external apps unless you're writing a custom
algorithm.

---

## 6. Diagnostics (`Greaseweazle.Core`)

### 6.1 `LibraryDiagnostics`

The library never writes to `Console`. Codecs/image readers raise
informational and warning messages through this static hub instead:

```vb
Public Shared Event MessageEmitted As EventHandler(Of LibraryDiagnosticEventArgs)
```

`LibraryDiagnosticEventArgs`:

```vb
Severity  As DiagnosticSeverity   ' Info | Warning
Message   As String
```

Subscribe once at app startup if you want to surface these messages in
your UI/log:

```vb
AddHandler LibraryDiagnostics.MessageEmitted,
    Sub(sender, e)
        Console.WriteLine($"[{e.Severity}] {e.Message}")
    End Sub
```

Messages are dropped silently when no subscriber is attached.

### 6.2 `HostVersion`

```vb
Public Shared ReadOnly Property Value As String
```

Returns the assembly's `InformationalVersion` (or its 4-part fallback).
Used by image writers that stamp their files with a "Greaseweazle <ver>"
provenance line; you can read it for an About box.

### 6.3 `ErrorHandling`

```vb
Public Shared Sub Check(predicate As Boolean, description As String)
```

Throws `FatalException` if the predicate is false. Library consumers
typically catch `FatalException` (or its subclasses) at the boundary.

---

## 7. Exception hierarchy

All application-level exceptions live in `Greaseweazle.Core`; the USB
protocol error lives in `Greaseweazle.Infrastructure`.

| Exception                                | Base                | Surfaces                                                                   |
| ---------------------------------------- | ------------------- | -------------------------------------------------------------------------- |
| `FatalException`                         | `Exception`         | Generic library-level fatal error                                          |
| `Fatal`                                  | `FatalException`    | Compatibility alias                                                        |
| `ArgparseException`                      | `FatalException`    | CLI-style argv error (exposes `Action`, `UsageLine`)                       |
| `UnknownFormatException`                 | `FatalException`    | Codec format not registered; carries `FormatName`, `KnownFormats`          |
| `UnrecognisedSuffixException`            | `FatalException`    | File extension not in `ImageTypeRegistry`                                  |
| `DeviceInUpdateModeException`            | `FatalException`    | Device in update mode but action isn't `update`                            |
| `DeviceNotInUpdateModeException`         | `FatalException`    | Update requested but device isn't in update mode                           |
| `DeviceFirmwareUnsupportedException`     | `FatalException`    | Connected firmware too old (carries `Major`, `Minor`)                      |
| `DeviceNotFoundAfterModeSwitchException` | `FatalException`    | Mode switch succeeded but device didn't reappear                           |
| `CapsLibraryNotFoundException`           | `FatalException`    | SPS/CAPS native library missing                                            |
| `Track0SeekMismatchException`            | `FatalException`    | Drive's TRK0 disagrees with commanded cylinder                             |
| `WriteVerifyFailedException`             | `FatalException`    | Verify failed for a Write track after retries; carries `Cyl`, `Head`        |
| `WriteMissingSectorsException`           | `FatalException`    | Input-image track has missing sectors; carries `Cyl`, `Head`, `MissingCount` |
| `KeyboardInterruptException`             | `Exception`         | Mirrors Python `KeyboardInterrupt`; library cancellation path              |
| `CmdError`                               | `Exception`         | Recoverable USB protocol error (`Cmd` byte + `Code` of type `UsbProtocol.Ack`) |
| `SkipUpdate`                             | `Exception`         | Update skipped (`-Force` was not set)                                      |

`CmdError` is the one you'll most often catch at the `Run` boundary:

```vb
Try
    summary = engine.Erase.Run(opts)
Catch ex As CmdError
    Log($"USB error: {ex.Message} (cmd={ex.CmdStr()}, code={ex.ErrcodeStr()})")
End Try
```

---

## 8. USB protocol surface (`Greaseweazle.Infrastructure`)

Most consumers only touch this namespace for `UsbProtocol.BusType`
(when populating `DriveSpec.Bus`) and for catching `CmdError`. The full
public set of enums:

- `UsbProtocol.ControlCmd { ClearComms, Normal }`
- `UsbProtocol.Cmd` — every firmware command opcode
- `UsbProtocol.GetInfo { Firmware, BandwidthStats, CurrentDrive }`
- `UsbProtocol.Params { Delays }`
- `UsbProtocol.Ack` — every firmware ack code (`Okay`, `BadCommand`,
  `NoIndex`, `NoTrk0`, `FluxOverflow`, `FluxUnderflow`, `Wrprot`,
  `NoUnit`, `NoBus`, `BadUnit`, `BadPin`, `BadCylinder`, `OutOfSRAM`,
  `OutOfFlash`)
- `UsbProtocol.FluxOp { Index, Space, Astable }`
- `UsbProtocol.BusType { Invalid, IBMPC, Shugart }`
- Constants: `EarliestSupportedFirmwareMajor` (0), `EarliestSupportedFirmwareMinor` (31)
- Static helpers: `DecodeFlux(data)`, `EncodeFlux(values, sampleFrequency)`

`SerialPortTransport` (`Greaseweazle.Infrastructure`) is the underlying
transport class but is normally consumed via `ToolOptions.UsbOpen`.

---

## 9. Codec extension hooks (`Greaseweazle.Codecs`)

Most apps don't need this section. It's only relevant if you're
registering a custom on-disk format.

### 9.1 `DiskDef`

```vb
Cyls         As Integer?
Heads        As Integer?
TrackMap     As Dictionary(Of Tuple(Of Integer, Integer), TrackDef)
Tracks       As TrackSet
Name         As String
DefaultRevs  As Double                 ' read-only
Sub  AddParam(key As String, value As String)
Sub  Finalise()
Function Trackset()                              As String
Function MkTrack(cyl, head)                      As Codec
Function DecodeFlux(cyl, head, track[, pll])     As Codec
```

### 9.2 `CodecRegistry`

```vb
Shared Sub Register(formatName As String, factory As Func(Of String, TrackDef))
Shared Function MkTrackdef(formatName As String) As TrackDef
Shared Function GetFormats()                     As IEnumerable(Of String)
```

### 9.3 Contracts

- `Codec` (interface) — single-track codec abstraction
- `CodecBase` (`MustInherit`) — partial implementation
- `TrackDef` (interface) — factory + per-track metadata
- `HasDecodeDiagnostics` (interface) — opt-in. A codec that produces
  structured per-track diagnostics during `DecodeFlux` (e.g. IBM-fixed's
  "unexpected sector" findings) can implement this. Action layer calls
  `DrainDecodeDiagnostics` after each `DecodeFlux` to translate
  `CodecDecodeDiagnostic` subclasses into typed Read/Write/Convert
  events. The codec produces no console text — `Greaseweazle.Cli`'s
  formatters render the matching lines from the typed events. Today
  the only concrete subclass is `UnexpectedSectorDiagnostic(c, h, r, n)`.

These are deeper-level extension points; refer to the in-source comments
in `src/Greaseweazle/codec/CodecContracts.vb` if you need to plug in a
new format.

---

## 10. Threading and lifecycle

- All `Run` calls are synchronous. Wrap in `Task.Run` for UI hosts.
- Events fire on the thread that invoked `Run`. Marshal to UI threads
  inside your handlers.
- A command instance is single-use: pull a fresh one from the engine
  for each operation. `EngineFacade.Erase` (etc.) returns a per-call
  fresh instance via the property getter.
- The command's events stay subscribed for the life of that instance.
  Either let it go out of scope after `Run` (event handlers still
  referenced via captured locals are GC-collected with it) or wrap the
  subscription in an `IDisposable` adapter (the CLI front-end does this
  via `*Formatter` classes — see `src/Greaseweazle.Cli/Formatters/`).
- Cancellation: pass a `CancellationToken` to `Run`. The algorithms
  check the token at meaningful boundaries (per-track for streaming
  commands, per-sample for RPM, etc.). A signalled token raises
  `OperationCanceledException`.

---

## 11. Erase example (end-to-end)

VB.NET host wiring all of `Started` + `TrackStarted` plus the typed
return:

```vb
Imports System.Threading
Imports Greaseweazle.Actions
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Tools

Module EraseDemo
    Sub Main()
        Dim engine As New GreaseweazleEngine()
        Dim cmd = engine.Erase

        AddHandler cmd.Started,
            Sub(s, e) Console.WriteLine($"Erasing {e.Tracks}, revs={e.Revs}")
        AddHandler cmd.TrackStarted,
            Sub(s, e) Console.WriteLine($"T{e.Cyl}.{e.Head}: erasing")

        Dim opts As New EraseOptions With {
            .TrackSet = New TrackSetSpec("c=0-1:h=0"),
            .Revs     = 3,
            .Hfreq    = False,
            .Live     = True,
            .Drive    = New DriveSpec With {
                .Bus    = UsbProtocol.BusType.IBMPC,
                .UnitId = 0
            }
        }

        Try
            Using cts As New CancellationTokenSource()
                Dim summary = cmd.Run(opts, cts.Token)
                Console.WriteLine(
                    $"Done. processed={summary.TracksProcessed} dryRun={summary.DryRun}")
            End Using
        Catch ex As CmdError
            Console.WriteLine($"Command Failed: {ex.Message}")
        Catch ex As OperationCanceledException
            Console.WriteLine("Cancelled.")
        End Try
    End Sub
End Module
```

For more complete patterns, the CLI front-end's `Formatters/*.vb`
(under `src/Greaseweazle.Cli/Formatters/`) is the canonical reference
for how each command's events are wired to output.
