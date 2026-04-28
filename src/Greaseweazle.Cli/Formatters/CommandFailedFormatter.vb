Imports System.IO

Namespace Greaseweazle.Cli.Formatters

    ' Single source of truth for the Python `Command Failed: %s` line that
    ' every action's CmdError catch printed in the unified exe. Library code
    ' throws CmdError untransformed; the CLI front-end's per-action dispatch
    ' wraps the call in a try/catch and routes the message through here so
    ' the literal prefix lives in exactly one place.
    '
    ' Mirrors the prints in:
    '   python_source/src/greaseweazle/tools/{reset.py:34, pin.py:32, pin.py:59,
    '   delays.py, info.py, bandwidth.py, seek.py, rpm.py, erase.py, clean.py,
    '   update.py, convert.py, align.py, read.py, write.py}
    Public NotInheritable Class CommandFailedFormatter

        Private Sub New()
        End Sub

        Public Shared Sub Render(message As String, output As TextWriter)
            output.WriteLine(String.Format("Command Failed: {0}", message))
        End Sub

    End Class

End Namespace
