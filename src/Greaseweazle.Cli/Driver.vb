Imports Greaseweazle.Actions
Imports Greaseweazle.Cli.Formatters
Imports Greaseweazle.Cli.Parsers
Imports Greaseweazle.Cli.Prompts
Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli

    ' Runtime entry-point for `gw-vb.exe`. The CLI front-end is a thin wrapper
    ' over the Greaseweazle class library: it parses argv, dispatches to the
    ' library's per-action commands, and translates events back into byte-for-
    ' byte parity output. The library carries the action algorithms, options,
    ' and typed events; the CLI's `UsageText` module owns the help/usage text
    ' rendered for `-h`/`--help` and the top-level `Usage:` block.
    Public Module Driver

        ' Python map: src/greaseweazle/cli.py::main
        Public Function Main(args As String()) As Integer
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

            ' Route library-internal informational/warning text to the CLI's
            ' redirected stdout (= stderr post-SetOut, matching Python's
            ' print(...) semantics). Library hosts that don't want these
            ' messages simply leave the event unsubscribed.
            AddHandler LibraryDiagnostics.MessageEmitted, AddressOf OnLibraryDiagnostic

            Dim backtrace = False
            Dim startTime As DateTime? = Nothing

            ' Python: print TEST/PRE-RELEASE banner before any other processing if version contains '+'.
            ' Use the entry assembly (gw-vb.exe) so the banner reflects the CLI
            ' build version, matching the behaviour of the previous unified exe.
            Dim entryAsm = Reflection.Assembly.GetEntryAssembly()
            If entryAsm Is Nothing Then
                entryAsm = Reflection.Assembly.GetExecutingAssembly()
            End If
            Dim hostVersion = entryAsm.GetName().Version.ToString()
            Dim infoVersion = TryCast(entryAsm.GetCustomAttributes(GetType(Reflection.AssemblyInformationalVersionAttribute), False).FirstOrDefault(),
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
                    Case Else
                        Return Usage()
                End Select
                remaining.RemoveAt(0)
            End While

            ' Python: case-sensitive `argv[1] not in actions` check.
            If remaining.Count = 0 OrElse Not UsageText.GetSupportedActions().Contains(remaining(0), StringComparer.Ordinal) Then
                Return Usage()
            End If

            Dim actionName = remaining(0)
            If remaining.Count >= 2 AndAlso IsHelpToken(remaining(1)) Then
                Return UsageForAction(actionName)
            End If
            Dim actionArgs = remaining.Skip(1).ToArray()
            Dim stdout As IO.TextWriter = Console.Out
            Dim stderr As IO.TextWriter = Console.Error
            Dim stdin As IO.TextReader = Console.In
            Dim result As Integer
            Try
                result = Dispatch(actionName, actionArgs, stdout, stdin)
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
            Catch argEx As ArgparseException
                ' argparse-style failures (unknown option, missing positional,
                ' bad --tracks, etc.) match Python's two-line stderr format:
                '   usage: gw.exe ACTION [options] in_file out_file
                '   gw.exe ACTION: error: MESSAGE
                ' and exit with code 2. Plain FatalException stays exit 1.
                If backtrace Then Throw
                stderr.WriteLine(argEx.UsageLine)
                stderr.WriteLine(String.Format("gw-vb {0}: error: {1}", argEx.Action, argEx.Message))
                result = 2
            Catch ex As FatalException
                ' Strongly-typed library failures (UnknownFormatException, the
                ' device-firmware-mode family, etc.) carry only a structured
                ' payload; the FatalErrorFormatter renders the catalogue /
                ' bullet list / hint block. Plain FatalException("...") sites
                ' fall through the formatter's default branch and are written
                ' verbatim, preserving the previous one-line behaviour.
                If backtrace Then Throw
                FatalErrorFormatter.Render(ex, stderr)
                result = 1
            Catch ex As Exception
                If backtrace Then Throw
                ' Python: assertion/index/type errors propagate; FATAL banner only for ordinary exceptions.
                If TypeOf ex Is FormatException OrElse TypeOf ex Is ArgumentException Then
                    Throw
                End If
                stderr.WriteLine("** FATAL ERROR:")
                stderr.WriteLine(ex.Message)
                result = 1
            End Try

            If startTime.HasValue Then
                Dim elapsed = DateTime.UtcNow - startTime.Value
                stderr.WriteLine(String.Format(Globalization.CultureInfo.InvariantCulture, "Time elapsed: {0:F2} seconds", elapsed.TotalSeconds))
            End If

            Return result
        End Function

        ' Dispatch the parsed action name to the typed Greaseweazle.Actions
        ' command surface. Each action is a class with a typed Run() that
        ' returns a Result/Summary or throws CmdError; per-action CLI
        ' formatters (Greaseweazle.Cli.Formatters.*) subscribe to the
        ' command's events and render Python-parity console output.
        ' Unknown action names are rejected upstream by GetSupportedActions
        ' before this function is reached, so the Case Else is purely
        ' defensive.
        Private Function Dispatch(actionName As String,
                                  actionArgs As String(),
                                  output As IO.TextWriter,
                                  input As IO.TextReader) As Integer
            Select Case actionName
                Case "reset"
                    Return RunReset(actionArgs, output)
                Case "pin"
                    Return RunPin(actionArgs, output)
                Case "delays"
                    Return RunDelays(actionArgs, output)
                Case "info"
                    Return RunInfo(actionArgs, output)
                Case "bandwidth"
                    Return RunBandwidth(actionArgs, output)
                Case "seek"
                    Return RunSeek(actionArgs, output, input)
                Case "erase"
                    Return RunErase(actionArgs, output)
                Case "clean"
                    Return RunClean(actionArgs, output)
                Case "rpm"
                    Return RunRpm(actionArgs, output)
                Case "update"
                    Return RunUpdate(actionArgs, output)
                Case "align"
                    Return RunAlign(actionArgs, output)
                Case "convert"
                    Return RunConvert(actionArgs, output)
                Case "read"
                    Return RunRead(actionArgs, output)
                Case "write"
                    Return RunWrite(actionArgs, output)
                Case Else
                    Throw New FatalException(String.Format("{0}: unknown action", actionName))
            End Select
        End Function

        ' `gw reset` — one-shot, no output on success. CmdError is the only
        ' user-visible failure mode and surfaces as Python's `Command Failed:
        ' %s` line.
        Private Function RunReset(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New ResetCommand()
                cmd.Run(ResetOptionsParser.Parse(args))
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw pin (get|set) ...` — one-shot. The command returns a typed
        ' PinResult; PinFormatter renders it (and its rc — UsageRequested
        ' returns 1, the others 0).
        Private Function RunPin(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New PinCommand()
                Dim result = cmd.Run(PinOptionsParser.Parse(args))
                Return PinFormatter.Render(result, output)
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw delays [--option value ...]` — one-shot. Returns a typed
        ' DelaysResult; the formatter renders it as the column-aligned info
        ' block. --test dry-run returns Nothing — the formatter writes
        ' nothing in that case.
        Private Function RunDelays(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New DelaysCommand()
                Dim result = cmd.Run(DelaysOptionsParser.Parse(args))
                DelaysFormatter.Render(result, output)
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw info [--bootloader] [--device=...]` — one-shot. Returns a
        ' typed DeviceInfoResult that captures host-tools version + the
        ' connection-state-tagged device block. InfoFormatter renders it as
        ' the Python-style "Host Tools: ..." / "Device:" indented block.
        Private Function RunInfo(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New InfoCommand()
                Dim result = cmd.Run(InfoOptionsParser.Parse(args))
                InfoFormatter.Render(result, output)
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw bandwidth [--device=...]` — one-shot. Performs the round-trip
        ' USB throughput measurement and returns a typed BandwidthResult.
        ' The formatter renders the legacy header/rows/summary block.
        Private Function RunBandwidth(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New BandwidthCommand()
                Dim result = cmd.Run(BandwidthOptionsParser.Parse(args))
                BandwidthFormatter.Render(result, output)
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw seek [--motor-on] [--force] [--test] CYL` — interactive.
        ' Builds a console-backed prompter so the algorithm can confirm
        ' "extreme" cylinder seeks; success/abort/dryrun all render no
        ' text (Python parity), only CmdError is rendered.
        Private Function RunSeek(args As String(), output As IO.TextWriter, input As IO.TextReader) As Integer
            Try
                Dim cmd As New SeekCommand() With {
                    .Prompter = New ConsoleSeekPrompter(output, input)
                }
                cmd.Run(SeekOptionsParser.Parse(args))
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw erase [--tracks ...] [--revs N] [--hfreq] [--test]` — streaming.
        ' EraseFormatter subscribes to Started + TrackStarted while the
        ' run is in flight, so each event renders the matching legacy
        ' line directly to ctx.Output.
        Private Function RunErase(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New EraseCommand()
                Using New EraseFormatter(cmd, output)
                    cmd.Run(EraseOptionsParser.Parse(args))
                End Using
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw clean [--cyls N] [--passes N] [--linger ms] [--test]` — streaming.
        ' CleanFormatter renders Python's "Pass N: 0 1 2 ... \n" lines
        ' incrementally as the algorithm fires PassStarted, per-cylinder
        ' CylinderSeeked, and PassCompleted events.
        Private Function RunClean(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New CleanCommand()
                Using New CleanFormatter(cmd, output)
                    cmd.Run(CleanOptionsParser.Parse(args))
                End Using
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw rpm [--nr N]` — streaming. RpmFormatter renders one speed
        ' line per measurement, then the four-line FASTEST/Mean/Median/
        ' SLOWEST block when Nr > 1. CmdError after a partial run still
        ' produces the summary because SummaryReady fires inside the
        ' per-sample-loop's Finally.
        Private Function RunRpm(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New RpmCommand()
                Using New RpmFormatter(cmd, output)
                    cmd.Run(RpmOptionsParser.Parse(args))
                End Using
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw update [--file FILE | --tag TAG] [--force] [--bootloader]`
        ' — streaming. UpdateFormatter renders the download line, the
        ' "Updating ... to version M.N..." line, and the
        ' Skipped/Completed/Failed footer block. CmdError is post-
        ' processed: OutOfFlash + Main Firmware, or OutOfSRAM +
        ' Bootloader, render specialised ERROR lines.
        Private Function RunUpdate(args As String(), output As IO.TextWriter) As Integer
            Dim opts = UpdateOptionsParser.Parse(args)
            Try
                Dim cmd As New UpdateCommand()
                Using New UpdateFormatter(cmd, output)
                    Dim summary = cmd.Run(opts)
                    UpdateFormatter.RenderSummary(summary, output)
                End Using
                Return 0
            Catch ex As CmdError
                UpdateFormatter.RenderCmdError(ex, opts.Bootloader, output)
                Return 0
            End Try
        End Function

        ' `gw align --tracks=... [--format FMT] [--reads N] [--revs R]
        '          [--hard-sectors] [--raw] [--reverse] [--test] ...`
        ' — streaming. AlignFormatter renders the header, optional Format
        ' line, optional hard-sector report, and one line per read pass.
        Private Function RunAlign(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New AlignCommand()
                Using New AlignFormatter(cmd, output)
                    cmd.Run(AlignOptionsParser.Parse(args, CodecRegistry.GetFormats()))
                End Using
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw convert IN OUT [--format ...] [--tracks ...] [--out-tracks ...]
        '              [--hard-sectors] [--reverse] [--no-clobber] [--pll PROFILE]
        '              [--adjust-speed PERIOD]` — streaming. ConvertFormatter
        ' renders the optional Format line, the "Converting … -> …" header,
        ' one line per processed track (NoFormat / Decoded / OutOfRange),
        ' optional "Converted to N hard sectors" line, and the post-decode
        ' "Cyl-> / H. S: / .X / Found N of M" sector grid.
        Private Function RunConvert(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New ConvertCommand()
                Using New ConvertFormatter(cmd, output)
                    cmd.Run(ConvertOptionsParser.Parse(args, CodecRegistry.GetFormats()))
                End Using
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw read OUT [--format FMT] [--tracks ...] [--revs N|F] [--raw]
        '          [--hard-sectors] [--reverse] [--no-clobber] [--retries N]
        '          [--seek-retries N] [--gen-tg43] [--densel ...] [--pll P]
        '          [--adjust-speed PER] [--fake-index PER] [--test]` —
        ' streaming. ReadFormatter renders the header, optional Format
        ' line, optional hard-sector report, one line per read attempt
        ' (initial / retry / give-up), and the post-decode "Cyl-> /
        ' H. S: / .X / Found N of M" sector grid (when --format).
        ' Mirrors Python's read.py — the image is written silently
        ' (no "Wrote ..." line). The resolved path is still surfaced
        ' on ReadSummary.OutputPath for library consumers.
        Private Function RunRead(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New ReadCommand()
                Using New ReadFormatter(cmd, output)
                    cmd.Run(ReadOptionsParser.Parse(args, CodecRegistry.GetFormats()))
                End Using
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' `gw write` — streaming command. Renders the run header (Format /
        ' Writing / precomp), an optional hard-sectors line, per-track
        ' Erasing / Out-of-range / Writing lines (with verify-failure
        ' retry suffix), and the final verify-tally footer. CmdError from
        ' the underlying USB layer is caught here and surfaced as
        ' Python's "Command Failed: %s" line.
        Private Function RunWrite(args As String(), output As IO.TextWriter) As Integer
            Try
                Dim cmd As New WriteCommand()
                Using New WriteFormatter(cmd, output)
                    cmd.Run(WriteOptionsParser.Parse(args, CodecRegistry.GetFormats()))
                End Using
                Return 0
            Catch ex As CmdError
                CommandFailedFormatter.Render(ex.Message, output)
                Return 0
            End Try
        End Function

        ' Python map: src/greaseweazle/cli.py::usage
        Private Function Usage() As Integer
            For Each line In UsageText.GetTopUsageLines()
                Console.Error.WriteLine(line)
            Next
            For Each name In UsageText.GetSupportedActions()
                Dim description = UsageText.GetActionDescription(name)
                If Not String.IsNullOrEmpty(description) Then
                    Console.Error.WriteLine(String.Format("  {0,-12}{1}", name, description))
                End If
            Next
            Return 1
        End Function

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper provides second-token -h/--help dispatch before action execution).
        Private Function UsageForAction(actionName As String) As Integer
            If Not UsageText.GetSupportedActions().Contains(actionName, StringComparer.Ordinal) Then
                Return Usage()
            End If

            Dim lines = UsageText.GetActionHelpLines(actionName)
            If lines.Count = 0 Then
                Console.Error.WriteLine(String.Format("usage: gw-vb {0} [options]", actionName))
                Dim description = UsageText.GetActionDescription(actionName)
                If Not String.IsNullOrEmpty(description) Then
                    Console.Error.WriteLine(description)
                End If
            Else
                For Each line In lines
                    Console.Error.WriteLine(line)
                Next
            End If
            Return 0
        End Function

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper renders LibraryDiagnostics events to the CLI's redirected stdout).
        '
        ' Python's tools call `print(...)` for the same messages this hook
        ' surfaces (e.g. "SCP: Imported legacy single-sided image",
        ' "T{c}.{h}: Ignoring unexpected sector ...") so emitting through
        ' Console.Out preserves byte-for-byte parity (Console.Out has been
        ' redirected to Console.Error in Main, matching cli.py's
        ' `sys.stdout = sys.stderr` redirect).
        Private Sub OnLibraryDiagnostic(sender As Object, e As LibraryDiagnosticEventArgs)
            Console.Out.WriteLine(e.Message)
        End Sub

        ' Python map: src/greaseweazle/cli.py::(no direct 1:1 symbol; VB helper extracted from inline token checks inside argument handling).
        Private Function IsHelpToken(token As String) As Boolean
            Return String.Equals(token, "-h", StringComparison.Ordinal) OrElse
                   String.Equals(token, "--help", StringComparison.Ordinal)
        End Function

    End Module

End Namespace
