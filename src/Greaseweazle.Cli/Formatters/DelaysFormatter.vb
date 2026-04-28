Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Formatters

    ' Python map: src/greaseweazle/tools/delays.py — renders the result of
    ' DelaysCommand.Run as a column-aligned info block, identical to what
    ' Python's `print_info_line` produces in the unified exe. Library
    ' consumers in non-CLI hosts ignore this class and inspect the typed
    ' DelaysResult directly.
    Public NotInheritable Class DelaysFormatter

        Private Sub New()
        End Sub

        ' Render the supplied result. A null `result` means the algorithm
        ' was invoked in --test dry-run mode and nothing should be printed.
        Public Shared Sub Render(result As DelaysResult, output As TextWriter)
            If result Is Nothing Then Return
            output.WriteLine(FormatLine("Select Delay", result.SelectDelayMicros, "us"))
            output.WriteLine(FormatLine("Step Delay", result.StepDelayMicros, "us"))
            output.WriteLine(FormatLine("Settle Time", result.SettleTimeMillis, "ms"))
            output.WriteLine(FormatLine("Motor Delay", result.MotorDelayMillis, "ms"))
            output.WriteLine(FormatLine("Watchdog", result.WatchdogMillis, "ms"))
            If result.PreWriteMicros.HasValue Then
                output.WriteLine(FormatLine("Pre-Write", result.PreWriteMicros.Value, "us"))
            End If
            If result.PostWriteMicros.HasValue Then
                output.WriteLine(FormatLine("Post-Write", result.PostWriteMicros.Value, "us"))
            End If
            If result.IndexMaskMicros.HasValue Then
                output.WriteLine(FormatLine("Index Mask", result.IndexMaskMicros.Value, "us"))
            End If
        End Sub

        Private Shared Function FormatLine(name As String, value As Integer, unit As String) As String
            Return Delays.PrintInfoLine(name, value.ToString(CultureInfo.InvariantCulture) & unit)
        End Function

    End Class

End Namespace
