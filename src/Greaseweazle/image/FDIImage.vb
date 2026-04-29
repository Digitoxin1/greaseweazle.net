Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/fdi.py::FDI
    Public Class Fdi
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), IbmTrackFixed)()

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        ' Python map: src/greaseweazle/image/fdi.py::FDI.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 32, "FDI: Not a FDI file.")

            Dim magic = BitConverter.ToUInt32(data, 0)
            Dim fddType = BitConverter.ToUInt32(data, 4)
            Dim headerSize = CInt(BitConverter.ToUInt32(data, 8))
            Dim sectorSize = CInt(BitConverter.ToUInt32(data, 16))
            Dim sectorsPerTrack = CInt(BitConverter.ToUInt32(data, 20))
            Dim sides = CInt(BitConverter.ToUInt32(data, 24))
            Dim tracks = CInt(BitConverter.ToUInt32(data, 28))

            ErrorHandling.Check(magic = 0UI, "FDI: Not a FDI file.")
            ErrorHandling.Check(fddType = &H90UI, "FDI: Unsupported format.")
            ErrorHandling.Check(sectorSize = 1024, "FDI: Unsupported sector size.")
            ErrorHandling.Check(sectorsPerTrack = 8, "FDI: Unsupported number of sectors per track.")
            ErrorHandling.Check(sides = 2, "FDI: Unsupported number of sides.")
            ErrorHandling.Check(tracks = 77, "FDI: Unsupported number of tracks.")
            ErrorHandling.Check(headerSize >= 32 AndAlso headerSize <= data.Length, "FDI: Not a FDI file.")

            Dim pos = headerSize
            For cyl = 0 To tracks - 1
                For head = 0 To sides - 1
                    Dim trackBytesLen = sectorsPerTrack * sectorSize
                    ErrorHandling.Check(pos + trackBytesLen <= data.Length, "FDI: Truncated image data.")
                    Dim payload(trackBytesLen - 1) As Byte
                    If trackBytesLen > 0 Then Array.Copy(data, pos, payload, 0, trackBytesLen)
                    pos += trackBytesLen
                    _tracks(Tuple.Create(cyl, head)) = BuildTrackCodec(cyl, head, payload)
                Next
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
            Throw New FatalException(String.Format("{0}: Cannot create FDI image files", If(String.IsNullOrEmpty(FileName), "FDI", FileName)))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create FDI image files", If(String.IsNullOrEmpty(FileName), "FDI", FileName)))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackCodec)
        Private Shared Function BuildTrackCodec(cyl As Integer, head As Integer, payload As Byte()) As IbmTrackFixed
            Dim sectorCount = 8
            Dim sectorSize = 1024
            Dim sectorNs = Enumerable.Repeat(3, sectorCount).ToList()
            Dim sectorSizes = Enumerable.Repeat(sectorSize, sectorCount).ToList()
            Dim sectorIds = Enumerable.Range(1, sectorCount).ToList()
            Dim timePerRev = 60.0 / 360.0
            Dim trackLenBc = CInt(Math.Max(1, Math.Floor(500.0 * 400.0 * 300.0 / 360.0)))
            Dim clock = timePerRev / trackLenBc
            Dim codec As New IbmTrackFixed("ibm.mfm",
                                           cyl,
                                           head,
                                           sectorSizes,
                                           sectorNs,
                                           sectorIds,
                                           head,
                                           imgBytesPerSector:=Nothing,
                                           timePerRev:=timePerRev,
                                           clock:=clock,
                                           emitIam:=True,
                                           gap1Override:=Nothing,
                                           gap2Override:=Nothing,
                                           gap3Override:=Nothing,
                                           gap4aOverride:=Nothing,
                                           gapByteOverride:=Nothing)
            codec.SetImgTrack(payload)
            Return codec
        End Function

    End Class

End Namespace
