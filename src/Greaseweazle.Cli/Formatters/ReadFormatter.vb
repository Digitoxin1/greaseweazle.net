Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Subscribes to ReadCommand events and renders the matching legacy
    ' lines (Python parity):
    '   "Reading {tracks} revs={n}"                              (Started)
    '   "Format <name>"                                          (Started, optional)
    '   "Drive reports {N} hard sectors"                         (HardSectorsDetected)
    '   "{tspec}: Ignoring unexpected sector C:.. H:.. R:.. N:.."(UnexpectedSectorIgnored)
    '   "{tspec}: {flux}"                                        (TrackProcessed, NoFormat)
    '   "{tspec}: {decoded} from {flux}[ (Retry #X.Y)]"          (TrackProcessed, Decoded)
    '   "{tspec}: WARNING: Out of range for format '{f}': …"     (TrackProcessed, OutOfRange)
    '   "{tspec}: Giving up: {N} sectors missing"                (TrackGaveUp)
    '   "Cyl-> / H. S: / 0. 0: / Found N of M"                   (SummaryReady)
    Public NotInheritable Class ReadFormatter
        Implements IDisposable

        Private ReadOnly _command As ReadCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _startedHandler As EventHandler(Of ReadStartedEventArgs)
        Private ReadOnly _hardSectorsHandler As EventHandler(Of HardSectorsDetectedEventArgs)
        Private ReadOnly _trackHandler As EventHandler(Of TrackProcessedEventArgs)
        Private ReadOnly _gaveUpHandler As EventHandler(Of ReadTrackGaveUpEventArgs)
        Private ReadOnly _summaryHandler As EventHandler(Of SectorSummaryReadyEventArgs)
        Private ReadOnly _unexpectedSectorHandler As EventHandler(Of UnexpectedSectorEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As ReadCommand, output As TextWriter)
            _command = command
            _output = output
            _startedHandler = AddressOf OnStarted
            _hardSectorsHandler = AddressOf OnHardSectorsDetected
            _trackHandler = AddressOf OnTrackProcessed
            _gaveUpHandler = AddressOf OnTrackGaveUp
            _summaryHandler = AddressOf OnSummaryReady
            _unexpectedSectorHandler = AddressOf OnUnexpectedSectorIgnored
            AddHandler _command.Started, _startedHandler
            AddHandler _command.HardSectorsDetected, _hardSectorsHandler
            AddHandler _command.TrackProcessed, _trackHandler
            AddHandler _command.TrackGaveUp, _gaveUpHandler
            AddHandler _command.SummaryReady, _summaryHandler
            AddHandler _command.UnexpectedSectorIgnored, _unexpectedSectorHandler
        End Sub

        Private Sub OnStarted(sender As Object, e As ReadStartedEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "Reading {0} revs={1}", e.Tracks, e.RevsDisplay))
            If Not String.IsNullOrEmpty(e.FormatName) Then
                _output.WriteLine("Format " & e.FormatName)
            End If
        End Sub

        Private Sub OnHardSectorsDetected(sender As Object, e As HardSectorsDetectedEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "Drive reports {0} hard sectors", e.HardSectorCount))
        End Sub

        Private Sub OnTrackProcessed(sender As Object, e As TrackProcessedEventArgs)
            Dim tspec = BuildTrackSpec(e.Track)
            Select Case e.Outcome
                Case TrackDecodeOutcome.NoFormat
                    _output.WriteLine(String.Format("{0}: {1}", tspec, e.FluxSummary))
                Case TrackDecodeOutcome.OutOfRange
                    _output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': No format conversion applied: {2}",
                                                    tspec, If(e.FormatName, ""), e.FluxSummary))
                Case TrackDecodeOutcome.Decoded
                    Dim line = String.Format("{0}: {1} from {2}", tspec, e.DecodedSummary, e.FluxSummary)
                    If e.Retry <> 0 Then
                        line &= String.Format(" (Retry #{0}.{1})", e.SeekRetry, e.Retry)
                    End If
                    _output.WriteLine(line)
            End Select
        End Sub

        Private Sub OnTrackGaveUp(sender As Object, e As ReadTrackGaveUpEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "{0}: Giving up: {1} sectors missing",
                                            BuildTrackSpec(e.Track), e.MissingSectors))
        End Sub

        Private Sub OnSummaryReady(sender As Object, e As SectorSummaryReadyEventArgs)
            SectorSummaryFormatter.Render(e.Grid, _output)
        End Sub

        ' Mirrors Python's ibm.py "Ignoring unexpected sector ..." print
        ' (one line per unique (C, H, R, N) tuple per DecodeFlux pass).
        Private Sub OnUnexpectedSectorIgnored(sender As Object, e As UnexpectedSectorEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "{0}: Ignoring unexpected sector C:{1} H:{2} R:{3} N:{4}",
                                            BuildTrackSpec(e.Track), e.C, e.H, e.R, e.N))
        End Sub

        ' "T{c}.{h}[ <- Drive {pc}.{ph}]"
        Private Shared Function BuildTrackSpec(t As TrackInfo) As String
            Dim spec = String.Format(CultureInfo.InvariantCulture, "T{0}.{1}", t.Cyl, t.Head)
            If t.PhysicalCyl <> t.Cyl OrElse t.PhysicalHead <> t.Head Then
                spec &= String.Format(CultureInfo.InvariantCulture, " <- Drive {0}.{1}", t.PhysicalCyl, t.PhysicalHead)
            End If
            Return spec
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.Started, _startedHandler
            RemoveHandler _command.HardSectorsDetected, _hardSectorsHandler
            RemoveHandler _command.TrackProcessed, _trackHandler
            RemoveHandler _command.TrackGaveUp, _gaveUpHandler
            RemoveHandler _command.SummaryReady, _summaryHandler
            RemoveHandler _command.UnexpectedSectorIgnored, _unexpectedSectorHandler
        End Sub

    End Class

End Namespace
