Imports Greaseweazle.Codecs
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli

    ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB module groups top-level globals/functions from module scope).
    Public Module Program

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
                    Dim registry = Actions.CreateDefaultRegistry()
                    Dim action As ToolAction = Nothing
                    If registry.TryGetAction(actionName, action) Then
                        Dim usageIndex = lines.FindIndex(Function(x) x.StartsWith("usage: ", StringComparison.OrdinalIgnoreCase))
                        If usageIndex >= 0 Then
                            lines.Insert(usageIndex + 1, "")
                            lines.Insert(usageIndex + 2, action.Description)
                        End If
                    End If
                End If
                Return lines
            End If
            Return Array.Empty(Of String)()
        End Function

        ' Python map: src/greaseweazle/cli.py::main
        Function Main(args As String()) As Integer
            ' Force Unix-style line endings on stdout/stderr. .NET's default
            ' TextWriter.NewLine is `Environment.NewLine`, which is `\r\n` on
            ' Windows; Python's `print()` always writes `\n` regardless of OS.
            ' Without this override, every WriteLine emits an extra CR on
            ' Windows so byte-for-byte diffs against Python output (e.g. parity
            ' fixtures captured under Python, or `gw-vb ... | diff -` against
            ' `gw ...`) fail on every line. Set both before the first write
            ' (the TEST/PRE-RELEASE banner below would otherwise be the first
            ' divergent line).
            Try
                Console.Out.NewLine = vbLf
                Console.Error.NewLine = vbLf
            Catch
                ' If the streams are redirected to something that doesn't
                ' permit setting NewLine (rare), proceed with the default.
            End Try

            ' Mirror Python's `sys.stdout = sys.stderr` (cli.py:48): all
            ' logging/printing must go to stderr so stdout stays clean for any
            ' future machine-readable use. Without this, `gw info | grep ...`
            ' captures everything in VB but captures nothing in Python (because
            ' Python's print writes to stderr, which isn't piped). Redirect
            ' Console.Out to wrap Console.Error so every WriteLine through
            ' context.Output (= Console.Out) reaches stderr like Python.
            Try
                Console.SetOut(Console.Error)
            Catch
                ' If redirection is locked down (rare), proceed with default.
            End Try

            ' Install Ctrl-C handler before any USB activity so an aborted run can
            ' immediately stop the drive motor (mirrors Python's KeyboardInterrupt
            ' flow in tools/util.py::with_drive_selected). Without this, .NET's
            ' default Ctrl-C terminates the process without running our Finally
            ' blocks and the firmware keeps spinning the drive for several seconds.
            InterruptControl.Install()

            Dim backtrace = False
            Dim showTime = False
            Dim startTime As DateTime? = Nothing

            ' Python: print TEST/PRE-RELEASE banner before any other processing if version contains '+'.
            Dim hostVersion = Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()
            Dim infoVersion = TryCast(Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(GetType(Reflection.AssemblyInformationalVersionAttribute), False).FirstOrDefault(),
                                      Reflection.AssemblyInformationalVersionAttribute)
            Dim banner = If(infoVersion IsNot Nothing, infoVersion.InformationalVersion, hostVersion)
            If banner IsNot Nothing AndAlso banner.IndexOf("+"c) >= 0 Then
                Console.Error.WriteLine("*** TEST/PRE-RELEASE: " & banner)
                Console.Error.WriteLine("*** Use these tools ONLY for test and development!!")
            End If

            Dim remaining = New List(Of String)(args)
            While remaining.Count > 0 AndAlso remaining(0).StartsWith("--", StringComparison.Ordinal)
                Select Case remaining(0)
                    Case "--bt"
                        backtrace = True
                    Case "--time"
                        ' Python: start_time = time.time() — only set when --time is passed.
                        startTime = DateTime.UtcNow
                        showTime = True
                    Case Else
                        Return Usage()
                End Select
                remaining.RemoveAt(0)
            End While

            ' Python: case-sensitive `argv[1] not in actions` check.
            If remaining.Count = 0 OrElse Not SupportedActions.Contains(remaining(0), StringComparer.Ordinal) Then
                Return Usage()
            End If

            Dim actionName = remaining(0)
            If remaining.Count >= 2 AndAlso IsHelpToken(remaining(1)) Then
                Return UsageForAction(actionName)
            End If
            Dim actionArgs = remaining.Skip(1).ToArray()
            Dim registry = Actions.CreateDefaultRegistry()
            Dim action As ToolAction = Nothing
            registry.TryGetAction(actionName, action)

            Dim context As New ToolContext()
            Dim result As Integer
            Try
                result = action.Execute(actionArgs, context)
            Catch ex As KeyboardInterruptException
                ' Python: `except KeyboardInterrupt: sys.exit(1)` — silent return 1
                ' with no FATAL banner. Surfaced by WithDriveSelected when the
                ' Ctrl-C handler closes the serial port mid-read.
                If backtrace Then Throw
                result = 1
            Catch ex As OperationCanceledException
                ' Python: KeyboardInterrupt -> silent return 1 (no FATAL banner).
                If backtrace Then Throw
                result = 1
            Catch ex As IndexOutOfRangeException
                ' Python: IndexError/AssertionError/TypeError/KeyError/struct.error are re-raised.
                Throw
            Catch ex As KeyNotFoundException
                Throw
            Catch ex As InvalidCastException
                Throw
            Catch ex As NullReferenceException
                Throw
            Catch ex As Exception
                If backtrace Then Throw
                ' Python: assertion/index/type errors propagate; FATAL banner only for ordinary exceptions.
                If TypeOf ex Is FormatException OrElse TypeOf ex Is ArgumentException Then
                    Throw
                End If
                context.ErrorOutput.WriteLine("** FATAL ERROR:")
                context.ErrorOutput.WriteLine(ex.Message)
                result = 1
            End Try

            If startTime.HasValue Then
                Dim elapsed = DateTime.UtcNow - startTime.Value
                context.ErrorOutput.WriteLine(String.Format(Globalization.CultureInfo.InvariantCulture, "Time elapsed: {0:F2} seconds", elapsed.TotalSeconds))
            End If

            Return result
        End Function

        ' Python map: src/greaseweazle/cli.py::usage
        Private Function Usage() As Integer
            For Each line In TopUsageLines
                Console.Error.WriteLine(line)
            Next
            Dim registry = Actions.CreateDefaultRegistry()
            For Each name In SupportedActions
                Dim action As ToolAction = Nothing
                If registry.TryGetAction(name, action) Then
                    Console.Error.WriteLine(String.Format("  {0,-12}{1}", action.Name, action.Description))
                End If
            Next
            Return 1
        End Function

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper provides second-token -h/--help dispatch before action execution).
        Private Function UsageForAction(actionName As String) As Integer
            Dim registry = Actions.CreateDefaultRegistry()
            Dim action As ToolAction = Nothing
            If Not registry.TryGetAction(actionName, action) Then
                Return Usage()
            End If

            Dim lines = GetActionHelpLines(action.Name)
            If lines.Count = 0 Then
                Console.Error.WriteLine(String.Format("usage: gw-vb {0} [options]", action.Name))
                Console.Error.WriteLine(action.Description)
            Else
                For Each line In lines
                    Console.Error.WriteLine(line)
                Next
            End If
            Return 0
        End Function

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper extracted from inline token checks inside argument handling).
        Private Function IsHelpToken(token As String) As Boolean
            Return String.Equals(token, "-h", StringComparison.Ordinal) OrElse
                   String.Equals(token, "--help", StringComparison.Ordinal)
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
