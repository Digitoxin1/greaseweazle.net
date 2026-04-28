Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Subscribes to EraseCommand events and renders Python's exact text:
    '   "Erasing {tracks}, revs={N}"          (Started)
    '   "T{cyl}.{head}: Erasing Track"        (TrackStarted, live only)
    '
    ' Implemented as IDisposable so the Driver can scope the subscription
    ' to a single Run() call and unsubscribe on completion.
    Public NotInheritable Class EraseFormatter
        Implements IDisposable

        Private ReadOnly _command As EraseCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _startedHandler As EventHandler(Of EraseStartedEventArgs)
        Private ReadOnly _trackHandler As EventHandler(Of EraseTrackEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As EraseCommand, output As TextWriter)
            _command = command
            _output = output
            _startedHandler = AddressOf OnStarted
            _trackHandler = AddressOf OnTrackStarted
            AddHandler _command.Started, _startedHandler
            AddHandler _command.TrackStarted, _trackHandler
        End Sub

        Private Sub OnStarted(sender As Object, e As EraseStartedEventArgs)
            _output.WriteLine(String.Format("Erasing {0}, revs={1}", e.Tracks, e.Revs))
        End Sub

        Private Sub OnTrackStarted(sender As Object, e As EraseTrackEventArgs)
            _output.WriteLine(String.Format("T{0}.{1}: Erasing Track", e.Cyl, e.Head))
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.Started, _startedHandler
            RemoveHandler _command.TrackStarted, _trackHandler
        End Sub

    End Class

End Namespace
