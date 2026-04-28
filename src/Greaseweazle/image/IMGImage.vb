Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/img.py::IMG
    Public Class Img
        Inherits Image

        Public Property SidesSwapped As Boolean
        Public Property Sequential As Boolean
        Public Property MinCylinders As Integer?
        Public Property Format As DiskDef

        Protected ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), Codec)()

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB protected helper to set per-track codec)
        Protected Sub SetTrack(cyl As Integer, head As Integer, track As Codec)
            _tracks(Tuple.Create(cyl, head)) = track
        End Sub

        ' Python map: src/greaseweazle/image/img.py::IMG.__init__
        Public Sub New()
            MyBase.New()
        End Sub

        ' Python map: src/greaseweazle/image/img.py::IMG.__init__
        Public Sub New(format As DiskDef)
            Me.New()
            Me.Format = format
        End Sub

        ' Python map: src/greaseweazle/image/img.py::IMG.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            Dim position = 0
            For Each pair In BuildTrackList()
                Dim cyl = pair.Item1
                Dim head = pair.Item2
                If SidesSwapped Then
                    head = head Xor 1
                End If

                Dim track = Format.MkTrack(cyl, head)
                If track Is Nothing Then
                    Continue For
                End If

                Dim remaining = Array.Empty(Of Byte)()
                If position < data.Length Then
                    remaining = data.Skip(position).ToArray()
                End If
                Dim consumed = track.SetImgTrack(remaining)
                position += consumed
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
                If SidesSwapped Then
                    head = head Xor 1
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

        ' Python map: src/greaseweazle/image/img.py::IMG.track_list
        Public Function TrackList() As List(Of Tuple(Of Integer, Integer))
            Return BuildTrackList()
        End Function

    End Class

    ' Python map: src/greaseweazle/image/img.py::IMG_AutoFormat
    Public MustInherit Class ImgAutoFormat
        Inherits Img

        Protected Sub New()
            MyBase.New()
        End Sub

        Protected Sub New(format As DiskDef)
            MyBase.New(format)
        End Sub

        ' Python map: src/greaseweazle/image/img.py::IMG_AutoFormat.format_from_file
        Public Shared Function FormatFromFile(name As String) As String
            Throw New FatalException(String.Format("{0}: {1}.FormatFromFile must be implemented by a concrete image type",
                                                   name,
                                                   NameOf(ImgAutoFormat)))
        End Function

        ' Python map: src/greaseweazle/image/img.py::IMG_AutoFormat.from_file
        Public Shared Shadows Function FromFile(name As String) As ImgAutoFormat
            Throw New FatalException(String.Format("{0}: {1}.FromFile must be invoked on a concrete auto-format image class",
                                                   name,
                                                   NameOf(ImgAutoFormat)))
        End Function
    End Class

    ' Python map: src/greaseweazle/image/apple2.py::DO
    Public Class [Do]
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/apple2.py::PO
    Public Class Po
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/d81.py::D81
    Public Class D81
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/d81.py::D1M
    Public Class D1M
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/d81.py::D2M
    Public Class D2M
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/d81.py::D4M
    Public Class D4M
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/acorn.py::SSD
    Public Class Ssd
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/acorn.py::DSD
    Public Class Dsd
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/acorn.py::ADS
    Public Class Ads
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/acorn.py::ADM
    Public Class Adm
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/acorn.py::ADL
    Public Class Adl
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/adf.py::ADF
    Public Class Adf
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/fd.py::FD
    Public Class Fd
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/hdm.py::HDM
    Public Class Hdm
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/mgt.py::MGT
    Public Class Mgt
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/sf7.py::SF7
    Public Class Sf7
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/sharp2d.py::SHARP2D
    Public Class Sharp2d
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/xdf.py::XDF
    Public Class Xdf
        Inherits Img
    End Class

    ' Python map: src/greaseweazle/image/dsk.py::DSK
    Public Class Dsk
        Inherits Img

        ' Python map: src/greaseweazle/image/dsk.py::DSK.from_file
        Public Shared Shadows Function FromFile(name As String) As Dsk
            Return Image.FromFile(Of Dsk)(name, Nothing, Nothing)
        End Function
    End Class

End Namespace
