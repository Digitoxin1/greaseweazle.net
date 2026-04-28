Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/dcp.py::DCP
    ' Python: class DCP(IMG_AutoFormat); read_only = True. Inherits IMG which provides
    ' track_list / sides_swapped / fmt-driven mk_track. We mirror that by inheriting
    ' our Img and reusing its track_list + Format infrastructure here.
    Public Class Dcp
        Inherits Img

        Public Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        Public Sub New(format As DiskDef)
            MyBase.New(format)
            Me.ReadOnly = True
        End Sub

        ' Python map: src/greaseweazle/image/dcp.py::DCP.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 162, "DCP: Corrupt header.")
            ErrorHandling.Check(Format IsNot Nothing, "DCP: A disk format must be supplied")

            Dim header = data.Take(162).ToArray()
            Dim pos = 162
            For Each pair In TrackList()
                Dim cyl = pair.Item1
                Dim head = pair.Item2
                If SidesSwapped Then
                    head = head Xor 1
                End If
                Dim track = Format.MkTrack(cyl, head)
                If track Is Nothing Then
                    Continue For
                End If
                ' Python: if cyl > 80: break
                If cyl > 80 Then
                    Exit For
                End If
                Dim marker = CInt(header(cyl * 2 + head))
                If marker = 1 Then
                    Dim remaining = If(pos < data.Length, data.Skip(pos).ToArray(), Array.Empty(Of Byte)())
                    pos += track.SetImgTrack(remaining)
                    SetTrack(cyl, head, track)
                ElseIf marker <> 0 Then
                    Throw New FatalException("DCP: Corrupt header.")
                End If
            Next
        End Sub

        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Throw New FatalException(String.Format("{0}: Cannot create DCP image files", If(String.IsNullOrEmpty(FileName), "DCP", FileName)))
        End Sub

        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create DCP image files", If(String.IsNullOrEmpty(FileName), "DCP", FileName)))
        End Function

        ' Python map: src/greaseweazle/image/dcp.py::DCP.format_from_file
        Public Overloads Shared Function FormatFromFile(name As String) As String
            ' Python: return 'pc98.2hd'
            Return "pc98.2hd"
        End Function

    End Class

End Namespace
