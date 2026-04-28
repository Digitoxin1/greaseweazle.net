Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `reset` action. Pure POCO -
    ' the CLI's ResetOptionsParser produces this from argv; library
    ' consumers may construct one directly.
    Public Class ResetOptions
        Public Property DelaysFlag As Boolean
        Public Property Live As Boolean = True
        Public Property Device As String
    End Class

    ' Python map: src/greaseweazle/tools/reset.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Reset

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ShouldRestoreDelays)
        Public Shared Function ShouldRestoreDelays(includeDelaysFlag As Boolean) As Boolean
            Return Not includeDelaysFlag
        End Function

    End Class

End Namespace
