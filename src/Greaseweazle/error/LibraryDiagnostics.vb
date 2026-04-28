Namespace Greaseweazle.Core

    ' Severity classifier for messages routed via LibraryDiagnostics.
    ' Used by the CLI to choose stdout vs stderr (and lets non-CLI hosts
    ' apply their own routing/log-level mapping).
    Public Enum DiagnosticSeverity
        ' Informational status - mirrors Python's `print(...)` calls
        ' that just narrate progress (e.g. "SCP: Imported legacy ...").
        Info = 0
        ' Recoverable problem the library decided to continue past
        ' (e.g. "SCP: WARNING: Bad image checksum"). Python writes these
        ' to stdout too; the CLI mirrors that by default.
        Warning = 1
    End Enum

    Public Class LibraryDiagnosticEventArgs
        Inherits EventArgs

        Public ReadOnly Property Severity As DiagnosticSeverity
        Public ReadOnly Property Message As String

        Public Sub New(severity As DiagnosticSeverity, message As String)
            Me.Severity = severity
            Me.Message = If(message, String.Empty)
        End Sub
    End Class

    ' Static dispatch hub for library-internal informational/warning text.
    ' The library used to call Console.Out.WriteLine directly from deep
    ' inside codec/image parsing paths to mirror the Python tools'
    ' diagnostic printf-style notices. Direct Console writes break the
    ' "library produces no console output" guarantee for non-CLI hosts
    ' that consume the DLL, so the library now raises events here and the
    ' CLI front-end is the only subscriber that converts them to text on
    ' the user's terminal.
    '
    ' Library hosts that *don't* want the messages can simply leave the
    ' event unsubscribed - the Emit method silently no-ops in that case.
    Public NotInheritable Class LibraryDiagnostics

        Private Sub New()
        End Sub

        ' Raised whenever a library code path wants to surface an
        ' informational message or warning. Subscribers should treat
        ' the call as fire-and-forget; the library does not inspect or
        ' depend on the subscriber's behaviour.
        Public Shared Event MessageEmitted As EventHandler(Of LibraryDiagnosticEventArgs)

        Public Shared Sub EmitInfo(message As String)
            Emit(DiagnosticSeverity.Info, message)
        End Sub

        Public Shared Sub EmitWarning(message As String)
            Emit(DiagnosticSeverity.Warning, message)
        End Sub

        Private Shared Sub Emit(severity As DiagnosticSeverity, message As String)
            Dim handler = MessageEmittedEvent
            If handler Is Nothing Then
                Return
            End If
            handler.Invoke(Nothing, New LibraryDiagnosticEventArgs(severity, message))
        End Sub
    End Class

End Namespace
