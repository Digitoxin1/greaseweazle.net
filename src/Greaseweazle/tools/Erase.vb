Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Shared

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `erase` action.
    Public Class EraseOptions
        Public Property Tracks As String
        Public Property TrackSet As TrackSet
        Public Property Revs As Integer
        Public Property Hfreq As Boolean
        Public Property FakeIndex As Double?
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/erase.py (direct command-algorithm parity mapping).
    Public NotInheritable Class [Erase]

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildEraseHeader)
        Public Shared Function BuildEraseHeader(tracks As String, revs As Integer) As String
            Return String.Format("Erasing {0}, revs={1}", tracks, revs)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeEraseTicks)
        Public Shared Function ComputeEraseTicks(driveTicks As Double) As Double
            Return driveTicks * 1.1
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeHighFrequencyFlux)
        Public Shared Function ComputeHighFrequencyFlux(driveTicks As Double) As List(Of Integer)
            Dim eraseTicks = ComputeEraseTicks(driveTicks)
            Return New List(Of Integer) From {CInt(Math.Round(eraseTicks))}
        End Function

        ' Python map: src/greaseweazle/tools/erase.py::erase
        '
        ' Performs the per-track erase loop. `onTrackStarting` is invoked
        ' just before each TrackIter is touched so the caller (the typed
        ' EraseCommand or any other library consumer) can render progress
        ' UI without this routine generating any text itself. Returns the
        ' number of tracks visited.
        Public Shared Function [Erase](usbClient As Unit,
                                       preview As EraseOptions,
                                       tracks As IReadOnlyList(Of TrackIter),
                                       onTrackStarting As Action(Of TrackIter)) As Integer
            Dim driveTicks As Double
            If preview.FakeIndex.HasValue Then
                driveTicks = preview.FakeIndex.Value * usbClient.SampleFreq
            Else
                driveTicks = usbClient.ReadTrack(2, 0).TicksPerRev
            End If

            Dim processed = 0
            For Each track In tracks
                If onTrackStarting IsNot Nothing Then
                    onTrackStarting(track)
                End If
                usbClient.Seek(track.PhysicalCyl, track.PhysicalHead)
                For rev = 0 To preview.Revs - 1
                    If preview.Hfreq Then
                        usbClient.WriteTrack(ComputeHighFrequencyFlux(driveTicks),
                                             terminateAtIndex:=False,
                                             cueAtIndex:=False)
                    Else
                        usbClient.EraseTrack(ComputeEraseTicks(driveTicks))
                    End If
                Next
                processed += 1
            Next
            Return processed
        End Function

    End Class

End Namespace
