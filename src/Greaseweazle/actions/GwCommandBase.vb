Imports System.Threading

Namespace Greaseweazle.Actions

    ' Common base type for every Command class.
    '
    ' Every public command (one-shot or streaming) inherits from this
    ' class. Commands now expose typed events (per-action progress
    ' payloads) plus a typed Run() that returns a Result/Summary —
    ' there is no longer a generic Status event or TextWriter
    ' plumbing on this base class.
    Public MustInherit Class GwCommandBase

        ' Called by derived commands at points where cooperative
        ' cancellation is meaningful. Throws OperationCanceledException,
        ' which the CLI front-end treats identically to
        ' KeyboardInterruptException (silent return code 1).
        Protected Sub ThrowIfCancellationRequested(ct As CancellationToken)
            ct.ThrowIfCancellationRequested()
        End Sub

    End Class

End Namespace
