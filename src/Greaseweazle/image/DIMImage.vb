Imports System.IO
Imports Greaseweazle.Core
Imports Greaseweazle.Codecs

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/dim.py::DIM (VB class: [Dim])
    Public Class [Dim]
        Inherits Img

        Public Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        Public Sub New(format As DiskDef)
            MyBase.New(format)
            Me.ReadOnly = True
        End Sub

        ' Python map: src/greaseweazle/image/dim.py::DIM.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 256, "DIM: Not a DIM file.")
            Dim sig = System.Text.Encoding.ASCII.GetString(data, &HAB, &HD)
            ErrorHandling.Check(String.Equals(sig, "DIFC HEADER  ", StringComparison.Ordinal), "DIM: Not a DIM file.")
            ErrorHandling.Check(Format IsNot Nothing, "DIM: A disk format must be supplied")

            ' Python: for t in fmt.tracks: cyl,head = t.cyl,t.head; if sides_swapped: head ^= 1; track = fmt.mk_track(cyl,head)
            Dim pos = 256
            For Each entry In Format.Tracks
                Dim cyl = entry.Cyl
                Dim head = entry.Head
                If SidesSwapped Then
                    head = head Xor 1
                End If
                Dim track = Format.MkTrack(cyl, head)
                If track Is Nothing Then
                    Continue For
                End If
                Dim remaining = If(pos < data.Length, data.Skip(pos).ToArray(), Array.Empty(Of Byte)())
                pos += track.SetImgTrack(remaining)
                SetTrack(cyl, head, track)
            Next
        End Sub

        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Throw New FatalException(String.Format("{0}: Cannot create DIM image files", If(String.IsNullOrEmpty(FileName), "DIM", FileName)))
        End Sub

        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create DIM image files", If(String.IsNullOrEmpty(FileName), "DIM", FileName)))
        End Function

        ' Python map: src/greaseweazle/image/dim.py::DIM.format_from_file
        Public Overloads Shared Function FormatFromFile(name As String) As String
            ' Python: opens the file, validates DIFC HEADER, reads media byte at offset 0:
            '   media_byte == 0 -> 'pc98.2hd'; media_byte == 1 -> 'pc98.2hs'; else fatal.
            Dim header(255) As Byte
            Using fs As New FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read)
                Dim read = 0
                While read < header.Length
                    Dim n = fs.Read(header, read, header.Length - read)
                    If n <= 0 Then Exit While
                    read += n
                End While
                ErrorHandling.Check(read = header.Length, "DIM: Not a DIM file.")
            End Using
            Dim sig = System.Text.Encoding.ASCII.GetString(header, &HAB, &HD)
            ErrorHandling.Check(String.Equals(sig, "DIFC HEADER  ", StringComparison.Ordinal), "DIM: Not a DIM file.")
            Dim mediaByte As Integer = header(0)
            If mediaByte = 0 Then
                Return "pc98.2hd"
            ElseIf mediaByte = 1 Then
                Return "pc98.2hs"
            End If
            Throw New FatalException("DIM: Unsupported format.")
        End Function
    End Class

End Namespace
