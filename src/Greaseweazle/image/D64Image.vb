Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/d64.py::D64
    Public Class D64
        Inherits Image

        Public Property Format As DiskDef
        Public Property Sequential As Boolean
        Public Property MinCylinders As Integer?

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), Codec)()
        Private ReadOnly _supportedFormat As String

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New(format As DiskDef, supportedFormat As String)
            MyBase.New()
            Me.Format = format
            _supportedFormat = supportedFormat
        End Sub

        ' Python map: src/greaseweazle/image/d64.py::D64.from_bytes
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

            Dim diskId = GetDiskId()
            For Each track In _tracks.Values
                ErrorHandling.Check(TypeOf track Is C64Gcr,
                                    String.Format("{0}: Only {1} format is supported", Me.GetType().Name, _supportedFormat))
                If diskId.HasValue Then
                    CType(track, C64Gcr).SetDiskId(diskId.Value)
                End If
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetTrack)
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EmitTrack)
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            EnsureFormat()
            Dim bytes As New List(Of Byte)()

            Dim maxCyl As Integer? = Nothing
            If MinCylinders.HasValue Then
                maxCyl = MinCylinders.Value - 1
                For Each pair In BuildTrackList()
                    Dim cyl = pair.Item1
                    Dim head = pair.Item2
                    Dim key = Tuple.Create(cyl, head)
                    If cyl > maxCyl.Value AndAlso _tracks.ContainsKey(key) Then
                        Dim track = _tracks(key)
                        If track.NrMissing() < track.Nsec Then
                            maxCyl = cyl
                        End If
                    End If
                Next
            End If

            For Each pair In BuildTrackList()
                Dim cyl = pair.Item1
                Dim head = pair.Item2
                If maxCyl.HasValue AndAlso cyl > maxCyl.Value Then
                    Exit For
                End If
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
            If Sequential Then
                For Each head In Format.Tracks.Heads
                    For Each cyl In Format.Tracks.Cyls
                        result.Add(Tuple.Create(cyl, head))
                    Next
                Next
            Else
                For Each cyl In Format.Tracks.Cyls
                    For Each head In Format.Tracks.Heads
                        result.Add(Tuple.Create(cyl, head))
                    Next
                Next
            End If
            Return result
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ReadDiskIdFromBam)
        Private Function ReadDiskIdFromBam() As UShort?
            Dim bam = TryCast(GetTrack(17, 0), C64Gcr)
            If bam Is Nothing Then
                Return Nothing
            End If
            Dim data = bam.GetImgTrack()
            If data.Length < 164 Then
                Return Nothing
            End If
            Return CUShort(data(162) Or (data(163) << 8))
        End Function

        ' Python map: src/greaseweazle/image/d64.py::D64.get_disk_id
        Public Function GetDiskId() As UShort?
            Return ReadDiskIdFromBam()
        End Function

    End Class

    ' Python map: src/greaseweazle/image/d64.py::D71
    Public Class D71
        Inherits D64

        Public Sub New(format As DiskDef, supportedFormat As String)
            MyBase.New(format, supportedFormat)
            Sequential = True
            MinCylinders = Nothing
        End Sub
    End Class

End Namespace
