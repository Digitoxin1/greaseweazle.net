Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Subscribes to WriteCommand events and renders the matching legacy
    ' lines (Python parity):
    '   "Format <name>"                                          (Started, optional)
    '   "Writing <tracks>"                                       (Started)
    '   "<precomp summary>"                                      (Started, optional)
    '   "Drive reports {N} hard sectors"                         (HardSectorsDetected)
    '   "{tspec}: Ignoring unexpected sector C:.. H:.. R:.. N:.."(UnexpectedSectorIgnored)
    '   "{tspec}: Erasing Track"                                 (TrackErasing)
    '   "{tspec}: WARNING: Out of range for format '{f}': …"     (TrackOutOfRange)
    '   "{tspec}: Writing Track ({summary})"                     (TrackWriting, retry=0)
    '   "{tspec}: Writing Track (Verify Failure: Retry #N)"      (TrackWriting, retry>0)
    '   "All tracks verified"                                    (VerifyCompleted, AllVerified)
    '   "{V} tracks verified; {N} tracks *not* verified (Reason: Verify <r>)"
    '   "No tracks verified (Reason: Verify <r>)"                (VerifyCompleted)
    Public NotInheritable Class WriteFormatter
        Implements IDisposable

        Private ReadOnly _command As WriteCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _startedHandler As EventHandler(Of WriteStartedEventArgs)
        Private ReadOnly _hardSectorsHandler As EventHandler(Of HardSectorsDetectedEventArgs)
        Private ReadOnly _erasingHandler As EventHandler(Of WriteTrackErasingEventArgs)
        Private ReadOnly _outOfRangeHandler As EventHandler(Of WriteTrackOutOfRangeEventArgs)
        Private ReadOnly _writingHandler As EventHandler(Of WriteTrackWritingEventArgs)
        Private ReadOnly _verifyHandler As EventHandler(Of WriteVerifyOutcomeEventArgs)
        Private ReadOnly _unexpectedSectorHandler As EventHandler(Of UnexpectedSectorEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As WriteCommand, output As TextWriter)
            _command = command
            _output = output
            _startedHandler = AddressOf OnStarted
            _hardSectorsHandler = AddressOf OnHardSectorsDetected
            _erasingHandler = AddressOf OnTrackErasing
            _outOfRangeHandler = AddressOf OnTrackOutOfRange
            _writingHandler = AddressOf OnTrackWriting
            _verifyHandler = AddressOf OnVerifyCompleted
            _unexpectedSectorHandler = AddressOf OnUnexpectedSectorIgnored
            AddHandler _command.Started, _startedHandler
            AddHandler _command.HardSectorsDetected, _hardSectorsHandler
            AddHandler _command.TrackErasing, _erasingHandler
            AddHandler _command.TrackOutOfRange, _outOfRangeHandler
            AddHandler _command.TrackWriting, _writingHandler
            AddHandler _command.VerifyCompleted, _verifyHandler
            AddHandler _command.UnexpectedSectorIgnored, _unexpectedSectorHandler
        End Sub

        Private Sub OnStarted(sender As Object, e As WriteStartedEventArgs)
            If Not String.IsNullOrEmpty(e.FormatName) Then
                _output.WriteLine("Format " & e.FormatName)
            End If
            _output.WriteLine("Writing " & e.Tracks)
            If Not String.IsNullOrEmpty(e.PrecompSummary) Then
                _output.WriteLine(e.PrecompSummary)
            End If
        End Sub

        Private Sub OnHardSectorsDetected(sender As Object, e As HardSectorsDetectedEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "Drive reports {0} hard sectors", e.HardSectorCount))
        End Sub

        Private Sub OnTrackErasing(sender As Object, e As WriteTrackErasingEventArgs)
            _output.WriteLine(String.Format("{0}: Erasing Track", BuildTrackSpec(e.Track)))
        End Sub

        Private Sub OnTrackOutOfRange(sender As Object, e As WriteTrackOutOfRangeEventArgs)
            _output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': Track skipped",
                                            BuildTrackSpec(e.Track), If(e.FormatName, "")))
        End Sub

        Private Sub OnTrackWriting(sender As Object, e As WriteTrackWritingEventArgs)
            Dim line = String.Format("{0}: Writing Track", BuildTrackSpec(e.Track))
            If e.RetryNumber <> 0 Then
                line &= String.Format(CultureInfo.InvariantCulture, " (Verify Failure: Retry #{0})", e.RetryNumber)
            Else
                line &= String.Format(" ({0})", If(e.FluxSummary, ""))
            End If
            _output.WriteLine(line)
        End Sub

        Private Sub OnVerifyCompleted(sender As Object, e As WriteVerifyOutcomeEventArgs)
            Select Case e.Outcome
                Case WriteVerifyOutcome.AllVerified
                    _output.WriteLine("All tracks verified")
                Case Else
                    Dim head As String
                    If e.VerifiedCount = 0 Then
                        head = "No tracks verified "
                    Else
                        head = String.Format(CultureInfo.InvariantCulture,
                                             "{0} tracks verified; {1} tracks *not* verified ",
                                             e.VerifiedCount, e.NotVerifiedCount)
                    End If
                    Dim reason = If(e.Outcome = WriteVerifyOutcome.VerifyDisabled, "disabled", "unavailable")
                    _output.WriteLine(head & String.Format(CultureInfo.InvariantCulture,
                                                          "(Reason: Verify {0})", reason))
            End Select
        End Sub

        ' Mirrors Python's ibm.py "Ignoring unexpected sector ..." print
        ' (one line per unique (C, H, R, N) tuple per DecodeFlux pass).
        Private Sub OnUnexpectedSectorIgnored(sender As Object, e As UnexpectedSectorEventArgs)
            _output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                            "{0}: Ignoring unexpected sector C:{1} H:{2} R:{3} N:{4}",
                                            BuildTrackSpec(e.Track), e.C, e.H, e.R, e.N))
        End Sub

        ' "T{c}.{h}[ -> Drive {pc}.{ph}]" (note arrow direction is the
        ' opposite of Read/Convert).
        Private Shared Function BuildTrackSpec(t As TrackInfo) As String
            Dim spec = String.Format(CultureInfo.InvariantCulture, "T{0}.{1}", t.Cyl, t.Head)
            If t.PhysicalCyl <> t.Cyl OrElse t.PhysicalHead <> t.Head Then
                spec &= String.Format(CultureInfo.InvariantCulture, " -> Drive {0}.{1}", t.PhysicalCyl, t.PhysicalHead)
            End If
            Return spec
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.Started, _startedHandler
            RemoveHandler _command.HardSectorsDetected, _hardSectorsHandler
            RemoveHandler _command.TrackErasing, _erasingHandler
            RemoveHandler _command.TrackOutOfRange, _outOfRangeHandler
            RemoveHandler _command.TrackWriting, _writingHandler
            RemoveHandler _command.VerifyCompleted, _verifyHandler
            RemoveHandler _command.UnexpectedSectorIgnored, _unexpectedSectorHandler
        End Sub

    End Class

End Namespace
