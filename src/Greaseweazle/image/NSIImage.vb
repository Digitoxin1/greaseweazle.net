Imports System.IO
Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/nsi.py::NSI
    Public Class Nsi
        Inherits Image

        Public Property Format As DiskDef

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), Codec)()

        ' Python map: src/greaseweazle/image/img.py::IMG.__init__
        Public Sub New(format As DiskDef)
            MyBase.New()
            Me.Format = format
        End Sub

        ' Python map: src/greaseweazle/image/nsi.py::NSI.format_from_file
        Public Shared Function FormatFromFile(name As String) As String
            Dim size = New FileInfo(name).Length
            If size = 1L * 35L * 10L * 256L Then
                Return "northstar.fm.ss"
            End If
            If size = 1L * 35L * 10L * 512L Then
                Return "northstar.mfm.ss"
            End If
            If size = 2L * 35L * 10L * 512L Then
                Return "northstar.mfm.ds"
            End If
            Throw New FatalException(String.Format("NSI: {0}: unrecognised file size", name))
        End Function

        ' Python map: src/greaseweazle/image/img.py::IMG.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            EnsureFormat()
            _tracks.Clear()
            Dim position = 0
            For Each pair In BuildTrackList()
                Dim cyl = pair.Item1
                Dim head = pair.Item2
                Dim track = Format.MkTrack(cyl, head)
                If track Is Nothing Then
                    Continue For
                End If
                Dim remaining = Array.Empty(Of Byte)()
                If position < data.Length Then
                    remaining = data.Skip(position).ToArray()
                End If
                position += track.SetImgTrack(remaining)
                _tracks(Tuple.Create(cyl, head)) = track
            Next
        End Sub

        ' Python map: src/greaseweazle/image/img.py::IMG.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/image/img.py::IMG.emit_track
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            If TypeOf track Is Codec Then
                _tracks(Tuple.Create(cyl, side)) = CType(track, Codec)
                Return
            End If
            EnsureFormat()
            Dim decoded = Format.DecodeFlux(cyl, side, track)
            If decoded IsNot Nothing Then
                _tracks(Tuple.Create(cyl, side)) = decoded
            End If
        End Sub

        ' Python map: src/greaseweazle/image/img.py::IMG.get_image
        Public Overrides Function GetImage() As Byte()
            EnsureFormat()
            Dim bytes As New List(Of Byte)()
            For Each pair In BuildTrackList()
                Dim cyl = pair.Item1
                Dim head = pair.Item2
                Dim key = Tuple.Create(cyl, head)
                Dim track As Codec = Nothing
                If _tracks.ContainsKey(key) Then
                    track = _tracks(key)
                Else
                    track = Format.MkTrack(cyl, head)
                    ErrorHandling.Check(track IsNot Nothing, String.Format("missing track definition for C{0}H{1}", cyl, head))
                End If
                bytes.AddRange(track.GetImgTrack())
            Next
            Return bytes.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EnsureFormat)
        Private Sub EnsureFormat()
            ErrorHandling.Check(Format IsNot Nothing, "Sector image requires a disk format to be specified")
            ErrorHandling.Check(Format.Tracks IsNot Nothing, "Disk format has no track map")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackList)
        Private Function BuildTrackList() As List(Of Tuple(Of Integer, Integer))
            EnsureFormat()
            Dim result As New List(Of Tuple(Of Integer, Integer))()
            For Each head In Format.Tracks.Heads
                Dim sideTracks = Format.Tracks.Cyls.Select(Function(cyl) Tuple.Create(cyl, head)).ToList()
                If (head And 1) = 1 Then
                    sideTracks.Reverse()
                End If
                result.AddRange(sideTracks)
            Next
            Return result
        End Function

        ' Python map: src/greaseweazle/image/nsi.py::NSI.track_list
        Public Function TrackList() As List(Of Tuple(Of Integer, Integer))
            Return BuildTrackList()
        End Function

    End Class

End Namespace
