Imports Greaseweazle.Codecs
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli

    ' CLI help-text catalog: exposes the usage banner, per-action one-line
    ' descriptions, and the multi-line help blocks rendered when the user
    ' passes `-h`/`--help`. The text and dynamic enrichment (known
    ' formats / image suffixes from the library registries) live in the
    ' CLI project so the library DLL carries no console-bound text.
    '
    ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB module groups top-level globals/functions from module scope).
    Public Module UsageText

        ' Python map: src/greaseweazle/cli.py::actions
        Private ReadOnly SupportedActions As String() = {
            "info",
            "read",
            "write",
            "convert",
            "erase",
            "clean",
            "seek",
            "delays",
            "update",
            "pin",
            "reset",
            "bandwidth",
            "rpm",
            "align"
        }

        ' Per-action one-line description rendered by the top-level
        ' "Actions:" usage block. Mirrors the `description` constant at
        ' the top of each Python tool module (e.g.
        ' src/greaseweazle/tools/info.py::description).
        Private ReadOnly ActionDescriptions As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"info", "Display information about the Greaseweazle setup."},
            {"read", "Read a disk to the specified image file."},
            {"write", "Write a disk from the specified image file."},
            {"convert", "Convert between image formats."},
            {"erase", "Erase a disk."},
            {"clean", "Clean a drive in a zig-zag pattern using a cleaning disk."},
            {"seek", "Seek to the specified cylinder."},
            {"delays", "Display (and optionally modify) drive-delay parameters."},
            {"update", "Update the Greaseweazle device firmware to latest (or specified) version."},
            {"pin", "Change the setting of a user-modifiable interface pin."},
            {"reset", "Reset the Greaseweazle device to power-on default state."},
            {"bandwidth", "Report the available USB bandwidth for the Greaseweazle device."},
            {"rpm", "Measure RPM of drive spindle."},
            {"align", "Repeatedly read the same track for floppy drive alignment."}
        }

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper exposing one-line action description for usage rendering).
        Public Function GetActionDescription(actionName As String) As String
            Dim description As String = Nothing
            If ActionDescriptions.TryGetValue(actionName, description) Then
                Return description
            End If
            Return String.Empty
        End Function

        ' Python map: src/greaseweazle/cli.py::usage (top fixed usage/help text block)
        Private ReadOnly TopUsageLines As String() = {
            "Usage: gw-vb [--time] [action] [-h] ...",
            "  --time      Print elapsed time after action is executed",
            "  -h, --help  Show help message for specified action",
            "Actions:"
        }

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB captures expanded per-action help text that Python delegates to action modules).
        Private ReadOnly ActionHelpLines As New Dictionary(Of String, String())(StringComparer.OrdinalIgnoreCase) From {
            {"info", New String() {"usage: gw-vb info [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --bootloader          display bootloader info (F7 only)", "  --test                dry-run preview only (skip hardware access)"}},
            {"read", New String() {"usage: gw-vb read [options] <file>", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --drive <id>          drive selector", "  --diskdefs <path>     disk definitions file", "  --format <name>       disk format for decode/verify", "  --revs <n>            revolutions per track", "  --tracks <spec>       tracks to read", "  --raw                 output raw stream", "  --fake-index <speed>  fake index pulse speed", "  --hard-sectors        use hard-sectored decode", "  --adjust-speed <v>    scale track rotation speed", "  --retries <n>         retries per seek-retry", "  --seek-retries <n>    seek retries", "  -n, --no-clobber      do not overwrite output file", "  --pll <spec>          PLL override", "  --densel <H|L>, --dd <H|L>", "                        density select output", "  --gen-tg43            generate TG43 signal", "  --reverse             reverse track data", "  --test                dry-run preview only (skip hardware access)"}},
            {"write", New String() {"usage: gw-vb write [options] <file>", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --drive <id>          drive selector", "  --diskdefs <path>     disk definitions file", "  --format <name>       disk format", "  --tracks <spec>       tracks to write", "  --pre-erase           erase tracks before write", "  --erase-empty         erase empty tracks", "  --fake-index <speed>  fake index pulse speed", "  --hard-sectors        write hard-sectored data", "  --no-verify           disable verify", "  --retries <n>         verify retries", "  --precomp <spec>      write precompensation", "  --reverse             reverse track data", "  --densel <H|L>, --dd <H|L>", "                        density select output", "  --gen-tg43            generate TG43 signal", "  --test                dry-run preview only (skip hardware access)"}},
            {"convert", New String() {"usage: gw-vb convert [options] <input-file> <output-file>", "", "options:", "  -h, --help            show this help message and exit", "  --diskdefs <path>     disk definitions file", "  --format <name>       codec format for flux decode/re-encode", "  --tracks <spec>       input track set", "  --out-tracks <spec>   output track set", "  --adjust-speed <v>    scale track speed", "  -n, --no-clobber      do not overwrite output file", "  --pll <spec>          PLL override", "  --hard-sectors        hard-sector decode", "  --reverse             reverse input data"}},
            {"erase", New String() {"usage: gw-vb erase [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --drive <id>          drive selector", "  --revs <n>            erase revolutions", "  --tracks <spec>       tracks to erase", "  --hfreq               high-frequency erase mode", "  --fake-index <speed>  fake index pulse speed", "  --test                dry-run preview only (skip hardware access)"}},
            {"clean", New String() {"usage: gw-vb clean [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --drive <id>          drive selector", "  --cyls <n>            cleaning cylinder count", "  --passes <n>          zig-zag passes", "  --linger <ms>         settle time at seek endpoints", "  --test                dry-run preview only (skip hardware access)"}},
            {"seek", New String() {"usage: gw-vb seek [options] <cylinder>", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --drive <id>          drive selector", "  --force               permit extreme cylinders", "  --motor-on            keep spindle motor enabled", "  --test                dry-run preview only (skip hardware access)"}},
            {"delays", New String() {"usage: gw-vb delays [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --select <us>         select delay", "  --step <us>           step delay", "  --settle <ms>         settle delay", "  --motor <ms>          motor delay", "  --watchdog <ms>       watchdog timeout", "  --pre-write <us>      pre-write delay", "  --post-write <us>     post-write delay", "  --index-mask <us>     index mask delay", "  --test                dry-run preview only (skip hardware access)"}},
            {"update", New String() {"usage: gw-vb update [options]", "", "options:", "  -h, --help            show this help message and exit", "  --file <path>         local firmware/update bundle", "  --tag <version>       GitHub release tag", "  --device <COMx>       device name (COM/serial port)", "  --force               allow downgrade/same-version flash", "  --bootloader          update bootloader payload", "  --test                dry-run preview only (skip hardware access)", "", "Examples:", "  gw-vb update", "  gw-vb update --force --tag v1.0", "  gw-vb update --file greaseweazle-firmware-v1.0.upd"}},
            {"pin", New String() {"usage: gw-vb pin get|set [-h] ...", "  get|set  Get or set a pin"}},
            {"reset", New String() {"usage: gw-vb reset [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --delays              include default delay reprogram", "  --test                dry-run preview only (skip hardware access)"}},
            {"bandwidth", New String() {"usage: gw-vb bandwidth [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --test                dry-run preview only (skip hardware access)"}},
            {"rpm", New String() {"usage: gw-vb rpm [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --drive <id>          drive selector", "  --nr <n>              number of samples", "  --test                dry-run preview only (skip hardware access)"}},
            {"align", New String() {"usage: gw-vb align [options]", "", "options:", "  -h, --help            show this help message and exit", "  --device <COMx>       device name (COM/serial port)", "  --drive <id>          drive selector", "  --diskdefs <path>     disk definitions file", "  --format <name>       disk format for decoding", "  --revs <n>            revolutions per read", "  --tracks <spec>       tracks to sample", "  --reads <n>           repeated reads", "  --raw                 raw flux mode", "  --fake-index <speed>  fake index pulse speed", "  --hard-sectors        hard-sector decode", "  --adjust-speed <v>    scale track rotation speed", "  --pll <spec>          PLL override", "  --densel <H|L>, --dd <H|L>", "                        density select output", "  --gen-tg43            generate TG43 signal", "  --reverse             reverse track data", "  --test                dry-run preview only (skip hardware access)"}}
        }

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB shared help appendix for drive selection wording).
        Private ReadOnly DriveHelpLines As String() = {
            "",
            "DRIVE: Drive (and bus) identifier:",
            "  0 | 1 | 2 | 3       :: Shugart bus unit",
            "  A | B               :: IBM/PC bus unit"
        }

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB shared help appendix for SPEED token grammar).
        Private ReadOnly SpeedHelpLines As String() = {
            "",
            "SPEED: Track rotation time specified as:",
            "  <N>rpm | <N>ms | <N>us | <N>ns | <N>scp | <N>"
        }

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB shared help appendix for TrackSet syntax).
        Private ReadOnly TrackSpecHelpLines As String() = {
            "",
            "TSPEC: Colon-separated list of:",
            "  c=SET               :: Set of cylinders to access",
            "  h=SET               :: Set of heads (sides) to access",
            "  step=[0-9]          :: # physical head steps between cylinders",
            "  hswap               :: Swap physical drive heads",
            "  h[01].off=[+-][0-9] :: Physical cylinder offsets per head",
            "  SET is a comma-separated list of integers and integer ranges",
            "  e.g. 'c=0-7,9-12:h=0-1'"
        }

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB shared help appendix for PLL override syntax).
        Private ReadOnly PllSpecHelpLines As String() = {
            "",
            "PLLSPEC: Colon-separated list of:",
            "  period=PCT          :: Period adjustment as percentage of phase error",
            "  phase=PCT           :: Phase adjustment as percentage of phase error",
            "  lowpass=USEC        :: Filter flux periods shorter than USEC",
            "  Defaults: period=5:phase=60 (no lowpass filter)"
        }

        ' Python map: src/greaseweazle/cli.py::actions
        Public Function GetSupportedActions() As IReadOnlyList(Of String)
            Return SupportedActions
        End Function

        ' Python map: src/greaseweazle/cli.py::usage (header line block)
        Public Function GetTopUsageLines() As IReadOnlyList(Of String)
            Return TopUsageLines
        End Function

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB assembles static+dynamic action help instead of importing and deferring entirely to action modules).
        Public Function GetActionHelpLines(actionName As String) As IReadOnlyList(Of String)
            If ActionHelpLines.ContainsKey(actionName) Then
                Dim lines = New List(Of String)(ActionHelpLines(actionName))
                Select Case actionName.ToLowerInvariant()
                    Case "read"
                        lines.Add("positional arguments:")
                        lines.Add("  file                  output filename")
                        lines.AddRange(DriveHelpLines)
                        lines.AddRange(SpeedHelpLines)
                        lines.AddRange(TrackSpecHelpLines)
                        lines.AddRange(PllSpecHelpLines)
                        lines.Add("")
                        lines.Add("FORMAT options:")
                        lines.AddRange(ColumnFormatter.Columnify(GetKnownFormats()).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries))
                        lines.Add("")
                        lines.Add("Supported file suffixes:")
                        lines.AddRange(ColumnFormatter.Columnify(New ImageTypeRegistry().GetKnownSuffixes()).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries))
                    Case "write"
                        lines.Add("positional arguments:")
                        lines.Add("  file                  input filename")
                        lines.AddRange(DriveHelpLines)
                        lines.AddRange(SpeedHelpLines)
                        lines.AddRange(TrackSpecHelpLines)
                        lines.Add("")
                        lines.Add("FORMAT options:")
                        lines.AddRange(ColumnFormatter.Columnify(GetKnownFormats()).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries))
                        lines.Add("")
                        lines.Add("Supported file suffixes:")
                        lines.AddRange(ColumnFormatter.Columnify(New ImageTypeRegistry().GetKnownSuffixes()).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries))
                    Case "align"
                        lines.AddRange(DriveHelpLines)
                        lines.AddRange(SpeedHelpLines)
                        lines.AddRange(TrackSpecHelpLines)
                        lines.AddRange(PllSpecHelpLines)
                        lines.Add("")
                        lines.Add("FORMAT options:")
                        lines.AddRange(ColumnFormatter.Columnify(GetKnownFormats()).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries))
                        lines.Add("")
                        lines.Add("Note: TRACKS can specify one track (e.g., c=40:h=0) or multiple heads on same cylinder (e.g., c=40:h=0,1) to alternate between heads")
                    Case "convert"
                        lines.Add("positional arguments:")
                        lines.Add("  input-file            input filename")
                        lines.Add("  output-file           output filename")
                        lines.AddRange(SpeedHelpLines)
                        lines.AddRange(TrackSpecHelpLines)
                        lines.AddRange(PllSpecHelpLines)
                        lines.Add("")
                        lines.Add("FORMAT options:")
                        lines.AddRange(ColumnFormatter.Columnify(GetKnownFormats()).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries))
                        lines.Add("")
                        lines.Add("Supported file suffixes:")
                        lines.AddRange(ColumnFormatter.Columnify(New ImageTypeRegistry().GetKnownSuffixes()).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries))
                    Case "seek"
                        lines.Add("positional arguments:")
                        lines.Add("  cylinder              cylinder to seek")
                        lines.AddRange(DriveHelpLines)
                    Case "erase"
                        lines.AddRange(DriveHelpLines)
                        lines.AddRange(SpeedHelpLines)
                        lines.AddRange(TrackSpecHelpLines)
                    Case "clean", "rpm"
                        lines.AddRange(DriveHelpLines)
                    Case Else
                End Select
                If Not String.Equals(actionName, "pin", StringComparison.OrdinalIgnoreCase) Then
                    Dim description = GetActionDescription(actionName)
                    If Not String.IsNullOrEmpty(description) Then
                        Dim usageIndex = lines.FindIndex(Function(x) x.StartsWith("usage: ", StringComparison.OrdinalIgnoreCase))
                        If usageIndex >= 0 Then
                            lines.Insert(usageIndex + 1, "")
                            lines.Insert(usageIndex + 2, description)
                        End If
                    End If
                End If
                Return lines
            End If
            Return Array.Empty(Of String)()
        End Function

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper loads known formats for help text from diskdefs/registry).
        Private Function GetKnownFormats() As IEnumerable(Of String)
            Dim diskDefs = ResolveDiskDefsPath()
            Try
                Return DiskDefParser.GetAllFormats(diskDefs)
            Catch
                Return CodecRegistry.GetFormats().OrderBy(Function(x) x).ToList()
            End Try
        End Function

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper resolves repository-local diskdefs.cfg path for CLI help enrichment).
        Private Function ResolveDiskDefsPath() As String
            Return "diskdefs.xml"
        End Function

    End Module

End Namespace
