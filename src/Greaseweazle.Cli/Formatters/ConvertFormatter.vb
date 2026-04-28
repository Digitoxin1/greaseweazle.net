Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Subscribes to ConvertCommand events and renders the matching
    ' legacy lines:
    '   "Format <name>"                                        (Started, optional)
    '   "Converting {tracks} -> {outTracks}"                   (Started)
    '   "{tspec}: Converted to {N} hard sectors"               (HardSectorsApplied)
    '   "{tspec}: {flux}"                                      (TrackProcessed, NoFormat)
    '   "{tspec}: {decoded} from {flux}"                       (TrackProcessed, Decoded)
    '   "{tspec}: WARNING: Out of range for format '{f}': …"   (TrackProcessed, OutOfRange)
    '   "Cyl-> / H. S: / 0. 0: / Found N of M"                 (SummaryReady)
    Public NotInheritable Class ConvertFormatter
        Implements IDisposable

        Private ReadOnly _command As ConvertCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _startedHandler As EventHandler(Of ConvertStartedEventArgs)
        Private ReadOnly _hardSectorsHandler As EventHandler(Of ConvertHardSectorsEventArgs)
        Private ReadOnly _trackHandler As EventHandler(Of ConvertTrackProcessedEventArgs)
        Private ReadOnly _summaryHandler As EventHandler(Of ConvertSummaryReadyEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As ConvertCommand, output As TextWriter)
            _command = command
            _output = output
            _startedHandler = AddressOf OnStarted
            _hardSectorsHandler = AddressOf OnHardSectorsApplied
            _trackHandler = AddressOf OnTrackProcessed
            _summaryHandler = AddressOf OnSummaryReady
            AddHandler _command.Started, _startedHandler
            AddHandler _command.HardSectorsApplied, _hardSectorsHandler
            AddHandler _command.TrackProcessed, _trackHandler
            AddHandler _command.SummaryReady, _summaryHandler
        End Sub

        Private Sub OnStarted(sender As Object, e As ConvertStartedEventArgs)
            If Not String.IsNullOrEmpty(e.FormatName) Then
                _output.WriteLine("Format " & e.FormatName)
            End If
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "Converting {0} -> {1}", e.InTracks, e.OutTracks))
        End Sub

        Private Sub OnHardSectorsApplied(sender As Object, e As ConvertHardSectorsEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "{0}: Converted to {1} hard sectors",
                                            BuildTrackSpec(e.Track), e.HardSectorCount))
        End Sub

        Private Sub OnTrackProcessed(sender As Object, e As ConvertTrackProcessedEventArgs)
            Dim tspec = BuildTrackSpec(e.Track)
            Select Case e.Outcome
                Case ConvertTrackOutcome.NoFormat
                    _output.WriteLine(String.Format("{0}: {1}", tspec, e.FluxSummary))
                Case ConvertTrackOutcome.OutOfRange
                    _output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': Track skipped",
                                                    tspec, If(e.FormatName, "")))
                Case ConvertTrackOutcome.Decoded
                    _output.WriteLine(String.Format("{0}: {1} from {2}", tspec, e.DecodedSummary, e.FluxSummary))
            End Select
        End Sub

        Private Sub OnSummaryReady(sender As Object, e As ConvertSummaryReadyEventArgs)
            SectorSummaryFormatter.Render(e.Grid, _output)
        End Sub

        ' "T{c}.{h}[ <- Image {pc}.{ph}]"
        Private Shared Function BuildTrackSpec(t As ConvertTrackInfo) As String
            Dim spec = String.Format(CultureInfo.InvariantCulture, "T{0}.{1}", t.Cyl, t.Head)
            If t.PhysicalCyl <> t.Cyl OrElse t.PhysicalHead <> t.Head Then
                spec &= String.Format(CultureInfo.InvariantCulture, " <- Image {0}.{1}", t.PhysicalCyl, t.PhysicalHead)
            End If
            Return spec
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.Started, _startedHandler
            RemoveHandler _command.HardSectorsApplied, _hardSectorsHandler
            RemoveHandler _command.TrackProcessed, _trackHandler
            RemoveHandler _command.SummaryReady, _summaryHandler
        End Sub

    End Class

End Namespace
