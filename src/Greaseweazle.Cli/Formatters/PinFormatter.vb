Imports System.IO
Imports Greaseweazle.Actions
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Formatters

    ' Renders PinCommand.Run() outcomes (typed PinResult, optional CmdError)
    ' to the supplied TextWriter using the Python pin.py wording. Library
    ' consumers in non-CLI hosts ignore this class and inspect the typed
    ' PinResult fields directly.
    Public NotInheritable Class PinFormatter

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/pin.py::usage (CLI-only help text).
        Private Shared ReadOnly UsageLines As String() = {
            "usage: gw pin get|set [-h] ...",
            "  get|set  Get or set a pin"
        }

        ' Renders a successful PinResult. Returns the CLI exit code that
        ' should be propagated (1 for UsageRequested, 0 otherwise) so the
        ' Driver can keep its existing rc translation pattern.
        Public Shared Function Render(result As PinResult, output As TextWriter) As Integer
            Select Case result.Kind
                Case PinResultKind.NoOp
                    Return 0
                Case PinResultKind.UsageRequested
                    For Each line In UsageLines
                        output.WriteLine(line)
                    Next
                    Return 1
                Case PinResultKind.PinSet
                    output.WriteLine(FormatPinSet(result.Pin, result.Level))
                    Return 0
                Case PinResultKind.PinValue
                    output.WriteLine(FormatPinValue(result.Pin, result.Level))
                    Return 0
                Case Else
                    Return 0
            End Select
        End Function

        ' Python pin.py:30-31 — `Pin %u is set %s`.
        Public Shared Function FormatPinSet(pin As Integer, level As Boolean) As String
            Return String.Format("Pin {0} is set {1}", pin, FormatLevel(level))
        End Function

        ' Python pin.py:55-56 — `Pin %u is %s`.
        Public Shared Function FormatPinValue(pin As Integer, level As Boolean) As String
            Return String.Format("Pin {0} is {1}", pin, FormatLevel(level))
        End Function

        ' Python pin.py:_level_str.
        Public Shared Function FormatLevel(level As Boolean) As String
            Return If(level, "High (5v)", "Low (0v)")
        End Function

    End Class

End Namespace
