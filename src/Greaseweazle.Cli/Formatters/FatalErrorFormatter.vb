Imports System.IO
Imports Greaseweazle.Core

Namespace Greaseweazle.Cli.Formatters

    ' Single source of truth for the Python `** FATAL ERROR:` banner +
    ' multi-line body that legacy throw sites used to bake into their
    ' FatalException messages.
    '
    ' The library now throws strongly-typed FatalException subclasses
    ' (UnknownFormatException, UnrecognisedSuffixException,
    ' DeviceInUpdateModeException, DeviceNotInUpdateModeException,
    ' DeviceFirmwareUnsupportedException, DeviceNotFoundAfterModeSwitch-
    ' Exception) carrying just the structured payload (format name, list
    ' of known formats, hardware model, jumper state, etc). This formatter
    ' renders the page-of-details that the CLI presents to the user:
    '   * columnified catalogues for unknown formats / suffixes
    '   * bullet-list "To perform an Update" instructions for the device-
    '     firmware-mode error family
    '   * the "If you are connected via a USB hub..." hint after a failed
    '     mode-switch reopen
    '
    ' Untyped FatalException (and the various plain Throw New
    ' FatalException("...") sites scattered across the library) fall
    ' through to a default branch that just writes ex.Message verbatim,
    ' preserving the previous one-line behaviour.
    Public NotInheritable Class FatalErrorFormatter

        Private Sub New()
        End Sub

        Public Shared Sub Render(ex As FatalException, output As TextWriter)
            output.WriteLine("** FATAL ERROR:")
            Dim typedLines = TryBuildTypedBody(ex)
            If typedLines IsNot Nothing Then
                For Each line In typedLines
                    output.WriteLine(line)
                Next
            Else
                output.WriteLine(ex.Message)
            End If
        End Sub

        Private Shared Function TryBuildTypedBody(ex As FatalException) As IEnumerable(Of String)
            Dim ufe = TryCast(ex, UnknownFormatException)
            If ufe IsNot Nothing Then
                Dim lines As New List(Of String) From {ufe.Message}
                If ufe.KnownFormats IsNot Nothing AndAlso ufe.KnownFormats.Count > 0 Then
                    lines.Add("Known formats:")
                    AddColumnified(lines, ufe.KnownFormats)
                End If
                Return lines
            End If

            Dim use = TryCast(ex, UnrecognisedSuffixException)
            If use IsNot Nothing Then
                Dim lines As New List(Of String) From {use.Message}
                If use.KnownSuffixes IsNot Nothing AndAlso use.KnownSuffixes.Count > 0 Then
                    lines.Add("Known suffixes:")
                    AddColumnified(lines, use.KnownSuffixes)
                End If
                Return lines
            End If

            Dim diue = TryCast(ex, DeviceInUpdateModeException)
            If diue IsNot Nothing Then
                Dim lines As New List(Of String) From {
                    "ERROR: Device is in Firmware Update Mode",
                    " - The only available action is ""gw update"""
                }
                If diue.UpdateJumpered Then
                    Dim pins = If(diue.HwModel <> 1, "RXI-TXO", "DCLK-GND")
                    lines.Add(String.Format(" - For normal operation disconnect from USB and remove the Update Jumper at pins {0}", pins))
                Else
                    lines.Add(" - Main firmware is erased: You *must* perform an update!")
                End If
                Return lines
            End If

            Dim dnium = TryCast(ex, DeviceNotInUpdateModeException)
            If dnium IsNot Nothing Then
                Dim lines As New List(Of String) From {"ERROR: Device is not in Firmware Update Mode"}
                lines.AddRange(BuildUpdateInstructions(dnium.JumperlessUpdate, dnium.HwModel))
                Return lines
            End If

            Dim dfu = TryCast(ex, DeviceFirmwareUnsupportedException)
            If dfu IsNot Nothing Then
                Dim lines As New List(Of String) From {
                    String.Format(Globalization.CultureInfo.InvariantCulture,
                                  "ERROR: Device firmware version {0}.{1} is unsupported",
                                  dfu.Major, dfu.Minor)
                }
                lines.AddRange(BuildUpdateInstructions(dfu.JumperlessUpdate, dfu.HwModel))
                Return lines
            End If

            Dim dnfa = TryCast(ex, DeviceNotFoundAfterModeSwitchException)
            If dnfa IsNot Nothing Then
                Return New String() {
                    "Could not find the Greaseweazle device after switching firmware mode.",
                    "If you are connected via a USB hub, instead try connecting directly."
                }
            End If

            Dim caps = TryCast(ex, CapsLibraryNotFoundException)
            If caps IsNot Nothing Then
                Dim lines As New List(Of String) From {
                    caps.Message,
                    "For installation instructions please read the wiki:",
                    "https://github.com/keirf/greaseweazle/wiki/IPF-Images"
                }
                If Not String.IsNullOrEmpty(caps.X64Error) OrElse Not String.IsNullOrEmpty(caps.GenericError) Then
                    lines.Add(String.Format("Probe errors: {0}; {1}", caps.X64Error, caps.GenericError))
                End If
                Return lines
            End If

            Dim t0sm = TryCast(ex, Track0SeekMismatchException)
            If t0sm IsNot Nothing Then
                Return New String() {
                    t0sm.Message,
                    " 1. Try ""gw reset"" to re-calibrate the drive-head position",
                    " 2. If the error persists try slowing down seek operations",
                    "     eg. ""gw delays --step 20000"" for 20ms per step"
                }
            End If

            Return Nothing
        End Function

        ' Mirrors the legacy Tooling.PrintUpdateInstructions output: empty
        ' instruction list when the firmware supports a jumperless update,
        ' otherwise the three-step "Disconnect / Install Jumper / Reconnect"
        ' preamble before the standard "Run gw update" line.
        Private Shared Function BuildUpdateInstructions(jumperlessUpdate As Boolean, hwModel As Integer) As IEnumerable(Of String)
            Dim lines As New List(Of String) From {"To perform an Update:"}
            If Not jumperlessUpdate Then
                lines.Add(" - Disconnect from USB")
                Dim pins = If(hwModel <> 1, "RXI-TXO", "DCLK-GND")
                lines.Add(String.Format(" - Install the Update Jumper at pins {0}", pins))
                lines.Add(" - Reconnect to USB")
            End If
            lines.Add(" - Run ""gw update"" to download and install latest firmware")
            Return lines
        End Function

        Private Shared Sub AddColumnified(lines As List(Of String), values As IEnumerable(Of String))
            Dim text = ColumnFormatter.Columnify(values)
            For Each line In text.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                lines.Add(line)
            Next
        End Sub

    End Class

End Namespace
