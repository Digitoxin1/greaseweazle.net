Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/nfd.py::NFD
    Public Class Nfd
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), IbmTrackFixed)()

        ' Python map: src/greaseweazle/image/nfd.py::NFD.__init__
        Public Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration FromBytes)
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 288, "NFD: not a NFD image")

            Dim fileId = data.Take(15).ToArray()
            Dim r0 = System.Text.Encoding.ASCII.GetBytes("T98FDDIMAGE.R0" & ChrW(0))
            Dim r1 = System.Text.Encoding.ASCII.GetBytes("T98FDDIMAGE.R1" & ChrW(0))
            If fileId.SequenceEqual(r1) Then
                Throw New FatalException("NFD: r1 format images not supported")
            End If
            ErrorHandling.Check(fileId.SequenceEqual(r0), "NFD: not a NFD image")

            Dim headerSize = CInt(BitConverter.ToUInt32(data, 272))
            Dim heads = CInt(data(277))
            ErrorHandling.Check(heads = 2, "NFD: heads != 2 not supported")
            ErrorHandling.Check(headerSize >= 288 AndAlso headerSize <= data.Length, "NFD: not a NFD image")

            Dim trackSectorHeaders As New List(Of TrackDesc)()
            Dim pos = 288
            For physicalTrack = 0 To 162
                Dim sectors As New List(Of SectorDesc)()
                Dim trackMfm As Integer? = Nothing
                For i = 1 To 26
                    ErrorHandling.Check(pos + 16 <= data.Length, "NFD: not a NFD image")
                    Dim c = CInt(data(pos + 0))
                    Dim h = CInt(data(pos + 1))
                    Dim r = CInt(data(pos + 2))
                    Dim n = CInt(data(pos + 3))
                    Dim mfm = CInt(data(pos + 4))
                    Dim ddam = CInt(data(pos + 5))
                    Dim status = CInt(data(pos + 6))
                    Dim st0 = CInt(data(pos + 7))
                    Dim st1 = CInt(data(pos + 8))
                    Dim st2 = CInt(data(pos + 9))
                    Dim pda = CInt(data(pos + 10))
                    pos += 16

                    If c = &HFF Then
                        Continue For
                    End If
                    If Not trackMfm.HasValue Then
                        trackMfm = mfm
                    ElseIf trackMfm.Value <> mfm Then
                        Throw New FatalException("NFD: mixed FM and MFM tracks not supported")
                    End If
                    ErrorHandling.Check(ddam = 0, "NFD: DDAM not supported")
                    ErrorHandling.Check(status = 0, String.Format("NFD: Status {0:x2} not supported", status))
                    ErrorHandling.Check(h = 0 OrElse h = 1, String.Format("NFD: Invalid head value {0}", h))
                    If h = 0 Then
                        ErrorHandling.Check(st0 = 0, String.Format("NFD: ST0 {0:x2} not supported", st0))
                    Else
                        ErrorHandling.Check(st0 = 4, String.Format("NFD: ST0 {0:x2} not supported", st0))
                    End If
                    ErrorHandling.Check(st1 = 0, String.Format("NFD: ST1 {0:x2} not supported", st1))
                    ErrorHandling.Check(st2 = 0, String.Format("NFD: ST2 {0:x2} not supported", st2))
                    ErrorHandling.Check(pda = &H90, String.Format("NFD: PDA {0:x2} not supported", pda))

                    sectors.Add(New SectorDesc With {.C = c, .H = h, .R = r, .N = n})
                Next
                trackSectorHeaders.Add(New TrackDesc With {
                    .Cyl = physicalTrack \ 2,
                    .Head = physicalTrack Mod 2,
                    .Mfm = If(trackMfm, 1),
                    .Sectors = sectors
                })
            Next

            Dim dataPos = headerSize
            For Each td In trackSectorHeaders
                If td.Sectors.Count = 0 Then
                    Continue For
                End If
                Dim sectorNs As New List(Of Integer)()
                Dim sectorSizes As New List(Of Integer)()
                Dim sectorIds As New List(Of Integer)()
                Dim payload As New List(Of Byte)()
                For Each sec In td.Sectors
                    Dim size = SectorSizeFromN(sec.N)
                    ErrorHandling.Check(dataPos + size <= data.Length, "NFD: not a NFD image")
                    payload.AddRange(data.Skip(dataPos).Take(size))
                    dataPos += size
                    sectorNs.Add(sec.N)
                    sectorSizes.Add(size)
                    sectorIds.Add(sec.R)
                Next

                Dim formatName = If(td.Mfm <> 0, "ibm.mfm", "ibm.fm")
                Dim baseRate = If(td.Mfm <> 0, 500.0, 250.0)
                Dim rpm = 360.0
                Dim timePerRev = 60.0 / rpm
                Dim trackLenBc = CInt(Math.Max(1, Math.Floor(baseRate * 400.0 * 300.0 / rpm)))
                Dim clock = timePerRev / trackLenBc
                Dim codec As New IbmTrackFixed(formatName,
                                               td.Cyl,
                                               td.Head,
                                               sectorSizes,
                                               sectorNs,
                                               sectorIds,
                                               td.Head,
                                               imgBytesPerSector:=Nothing,
                                               timePerRev:=timePerRev,
                                               clock:=clock,
                                               emitIam:=True,
                                               gap1Override:=Nothing,
                                               gap2Override:=Nothing,
                                               gap3Override:=Nothing,
                                               gap4aOverride:=Nothing,
                                               gapByteOverride:=Nothing)
                codec.SetImgTrack(payload.ToArray())
                _tracks(Tuple.Create(td.Cyl, td.Head)) = codec
            Next
        End Sub

        ' Python map: src/greaseweazle/image/nfd.py::NFD.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EmitTrack)
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Throw New FatalException(String.Format("{0}: Cannot create NFD image files", If(String.IsNullOrEmpty(FileName), "NFD", FileName)))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create NFD image files", If(String.IsNullOrEmpty(FileName), "NFD", FileName)))
        End Function

        ' Python map: src/greaseweazle/image/nfd.py::NFD.from_file
        Public Shared Shadows Function FromFile(name As String) As Nfd
            Return Image.FromFile(Of Nfd)(name, Nothing, Nothing)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SectorSizeFromN)
        Private Shared Function SectorSizeFromN(n As Integer) As Integer
            If n < 0 Then
                Return 128
            End If
            If n > 7 Then
                Return 128 << 8
            End If
            Return 128 << n
        End Function

        ' Python map: src/greaseweazle/image/nfd.py::(no direct 1:1 symbol; VB class helper supporting nfd image handling)
        Private Class SectorDesc
            Public Property C As Integer
            Public Property H As Integer
            Public Property R As Integer
            Public Property N As Integer
        End Class

        ' Python map: src/greaseweazle/image/nfd.py::(no direct 1:1 symbol; VB class helper supporting nfd image handling)
        Private Class TrackDesc
            Public Property Cyl As Integer
            Public Property Head As Integer
            Public Property Mfm As Integer
            Public Property Sectors As List(Of SectorDesc)
        End Class

    End Class

End Namespace
