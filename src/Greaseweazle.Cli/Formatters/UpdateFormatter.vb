Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions
Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Cli.Formatters

    ' Renders Python's text for the Update command. Splits across two
    ' surfaces: an event-driven progress block (DownloadStarted +
    ' UpdateStarted) attached at construction, and a result-driven
    ' RenderSummary call once Run() completes; CmdError is rendered
    ' separately via RenderCmdError so the special OutOfFlash/OutOfSRAM
    ' messages can be inserted before the generic "Command Failed:".
    Public NotInheritable Class UpdateFormatter
        Implements IDisposable

        Private ReadOnly _command As UpdateCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _downloadHandler As EventHandler(Of UpdateDownloadStartedEventArgs)
        Private ReadOnly _updateHandler As EventHandler(Of UpdateStartedEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As UpdateCommand, output As TextWriter)
            _command = command
            _output = output
            _downloadHandler = AddressOf OnDownloadStarted
            _updateHandler = AddressOf OnUpdateStarted
            AddHandler _command.DownloadStarted, _downloadHandler
            AddHandler _command.UpdateStarted, _updateHandler
        End Sub

        Private Sub OnDownloadStarted(sender As Object, e As UpdateDownloadStartedEventArgs)
            _output.WriteLine("Downloading latest firmware: " & e.PayloadName)
        End Sub

        Private Sub OnUpdateStarted(sender As Object, e As UpdateStartedEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "Updating {0} to version {1}.{2}...",
                                            If(e.Target = UpdateTarget.Bootloader, "Bootloader", "Main Firmware"),
                                            e.Major, e.Minor))
        End Sub

        ' Renders the Skipped/Completed/Failed footer block. DryRun
        ' renders nothing, matching Python's --test path.
        Public Shared Sub RenderSummary(summary As UpdateSummary, output As TextWriter)
            If summary Is Nothing Then Return
            Select Case summary.Outcome
                Case UpdateOutcome.DryRun
                    ' --test: no output.
                Case UpdateOutcome.Skipped
                    output.WriteLine("** SKIPPING UPDATE:")
                    output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                                   "Device is already running version {0}.{1}.{2}Use --force to update anyway.",
                                                   summary.DeviceMajor, summary.DeviceMinor, vbLf))
                Case UpdateOutcome.Failed
                    If summary.Target = UpdateTarget.Bootloader Then
                        output.WriteLine("** UPDATE FAILED: Please retry immediately or your Weazle may need")
                        output.WriteLine("        full reflashing via a suitable programming adapter!")
                    Else
                        output.WriteLine("** UPDATE FAILED: Please retry!")
                    End If
                Case UpdateOutcome.Completed
                    output.WriteLine("Done.")
                    If summary.NeedsUnplug Then
                        output.WriteLine("** Unplug device and remove the Update Jumper")
                    End If
            End Select
        End Sub

        ' Maps a CmdError to the matching legacy line. The bootloader
        ' flag from the original options selects which ack code maps to
        ' which special message; everything else falls through to the
        ' generic "Command Failed: ..." line.
        Public Shared Sub RenderCmdError(ex As CmdError, bootloader As Boolean, output As TextWriter)
            If ex.Code = UsbProtocol.Ack.OutOfSRAM AndAlso bootloader Then
                output.WriteLine("ERROR: Bootloader update unsupported on this device (insufficient SRAM)")
            ElseIf ex.Code = UsbProtocol.Ack.OutOfFlash AndAlso Not bootloader Then
                output.WriteLine("ERROR: New firmware is too large for this device (insufficient Flash memory)")
            Else
                CommandFailedFormatter.Render(ex.Message, output)
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.DownloadStarted, _downloadHandler
            RemoveHandler _command.UpdateStarted, _updateHandler
        End Sub

    End Class

End Namespace
