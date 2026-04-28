Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Subscribes to CleanCommand events and renders Python's exact text
    '   "Pass {N}: {cyl} {cyl} ... \n"
    ' incrementally, one fragment per event so the user sees progress as
    ' the head moves. Identical text in both live and dry-run mode — the
    ' algorithm replays the planned cylinder sequence through the same
    ' events when --test is set.
    Public NotInheritable Class CleanFormatter
        Implements IDisposable

        Private ReadOnly _command As CleanCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _passStartedHandler As EventHandler(Of CleanPassStartedEventArgs)
        Private ReadOnly _cylinderHandler As EventHandler(Of CleanCylinderEventArgs)
        Private ReadOnly _passCompletedHandler As EventHandler(Of CleanPassCompletedEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As CleanCommand, output As TextWriter)
            _command = command
            _output = output
            _passStartedHandler = AddressOf OnPassStarted
            _cylinderHandler = AddressOf OnCylinderSeeked
            _passCompletedHandler = AddressOf OnPassCompleted
            AddHandler _command.PassStarted, _passStartedHandler
            AddHandler _command.CylinderSeeked, _cylinderHandler
            AddHandler _command.PassCompleted, _passCompletedHandler
        End Sub

        Private Sub OnPassStarted(sender As Object, e As CleanPassStartedEventArgs)
            _output.Write(String.Format(CultureInfo.InvariantCulture, "Pass {0}: ", e.PassIndex))
        End Sub

        Private Sub OnCylinderSeeked(sender As Object, e As CleanCylinderEventArgs)
            _output.Write(String.Format(CultureInfo.InvariantCulture, "{0} ", e.Cylinder))
        End Sub

        Private Sub OnPassCompleted(sender As Object, e As CleanPassCompletedEventArgs)
            _output.WriteLine()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.PassStarted, _passStartedHandler
            RemoveHandler _command.CylinderSeeked, _cylinderHandler
            RemoveHandler _command.PassCompleted, _passCompletedHandler
        End Sub

    End Class

End Namespace
