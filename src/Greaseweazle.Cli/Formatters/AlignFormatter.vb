Imports System.IO
Imports Greaseweazle.Actions
Imports Greaseweazle.Shared

Namespace Greaseweazle.Cli.Formatters

    ' Subscribes to AlignCommand events and renders the matching legacy
    ' lines:
    '   "Aligning T{c}.{h}, reading R times, revs=N"             (Started, single track)
    '   "Aligning Tc (alternating heads X,Y), reading R times,…" (Started, multi-track)
    '   "Format <name>"                                          (Started, optional)
    '   "Drive reports {N} hard sectors"                         (HardSectorsDetected)
    '   "{tspec}: {flux}"                                        (ReadCompleted, NoFormat)
    '   "{tspec}: {decoded} from {flux}"                         (ReadCompleted, Decoded)
    '   "{tspec}: WARNING: Out of range for format '{f}': …"     (ReadCompleted, OutOfRange)
    Public NotInheritable Class AlignFormatter
        Implements IDisposable

        Private ReadOnly _command As AlignCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _startedHandler As EventHandler(Of AlignStartedEventArgs)
        Private ReadOnly _hardSectorsHandler As EventHandler(Of AlignHardSectorsDetectedEventArgs)
        Private ReadOnly _readHandler As EventHandler(Of AlignReadCompletedEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As AlignCommand, output As TextWriter)
            _command = command
            _output = output
            _startedHandler = AddressOf OnStarted
            _hardSectorsHandler = AddressOf OnHardSectorsDetected
            _readHandler = AddressOf OnReadCompleted
            AddHandler _command.Started, _startedHandler
            AddHandler _command.HardSectorsDetected, _hardSectorsHandler
            AddHandler _command.ReadCompleted, _readHandler
        End Sub

        Private Sub OnStarted(sender As Object, e As AlignStartedEventArgs)
            If e.Tracks.Count = 1 Then
                Dim t = e.Tracks(0)
                _output.WriteLine(String.Format("Aligning {0}, reading {1} times, revs={2}",
                                                BuildTrackSpec(t), e.Reads, e.Revs))
            Else
                _output.WriteLine(String.Format("Aligning T{0} (alternating heads {1}), reading {2} times, revs={3}",
                                                e.Tracks(0).Cyl,
                                                String.Join(",", e.Tracks.Select(Function(t) t.Head)),
                                                e.Reads, e.Revs))
            End If
            If Not String.IsNullOrEmpty(e.FormatName) Then
                _output.WriteLine("Format " & e.FormatName)
            End If
        End Sub

        Private Sub OnHardSectorsDetected(sender As Object, e As AlignHardSectorsDetectedEventArgs)
            _output.WriteLine(String.Format("Drive reports {0} hard sectors", e.HardSectorCount))
        End Sub

        Private Sub OnReadCompleted(sender As Object, e As AlignReadCompletedEventArgs)
            Dim tspec = BuildTrackSpec(e.Track)
            Select Case e.Outcome
                Case AlignReadOutcome.NoFormat
                    _output.WriteLine(String.Format("{0}: {1}", tspec, e.FluxSummary))
                Case AlignReadOutcome.OutOfRange
                    _output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': No format conversion applied: {2}",
                                                    tspec, e.FormatName, e.FluxSummary))
                Case AlignReadOutcome.Decoded
                    _output.WriteLine(String.Format("{0}: {1} from {2}", tspec, e.DecodedSummary, e.FluxSummary))
            End Select
        End Sub

        ' Mirrors Greaseweazle.Tools.Align.BuildTrackSpec but lives in
        ' the CLI so the DLL doesn't need to expose the formatted text
        ' helper for this one purpose. (Same logic, byte-for-byte
        ' compatible with the parity probe.)
        Private Shared Function BuildTrackSpec(t As TrackIter) As String
            Dim spec = String.Format("T{0}.{1}", t.Cyl, t.Head)
            If t.PhysicalCyl <> t.Cyl OrElse t.PhysicalHead <> t.Head Then
                spec &= String.Format(" <- Drive {0}.{1}", t.PhysicalCyl, t.PhysicalHead)
            End If
            Return spec
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.Started, _startedHandler
            RemoveHandler _command.HardSectorsDetected, _hardSectorsHandler
            RemoveHandler _command.ReadCompleted, _readHandler
        End Sub

    End Class

End Namespace
