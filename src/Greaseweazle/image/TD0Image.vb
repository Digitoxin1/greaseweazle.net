Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Optimised

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/td0.py::TD0
    Public Class Td0
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), IbmTrackFixed)()

        ' Python map: src/greaseweazle/image/td0.py::TD0.__init__
        Public Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        ' Python map: src/greaseweazle/image/td0.py::TD0.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 12, "TD0: bad file signature")

            Dim sig0 = data(0)
            Dim sig1 = data(1)
            ErrorHandling.Check((sig0 = AscW("T"c) AndAlso sig1 = AscW("D"c)) OrElse (sig0 = AscW("t"c) AndAlso sig1 = AscW("d"c)),
                                "TD0: bad file signature")
            ' Python: error.check(crc16.new(dat[:10]).crcValue == crc, 'TD0: bad file header crc')
            Dim fileHeaderCrc = CInt(BitConverter.ToUInt16(data, 10))
            ErrorHandling.Check(Crc16Teledisk(data, 0, 10) = fileHeaderCrc, "TD0: bad file header crc")
            If sig0 = AscW("t"c) AndAlso sig1 = AscW("d"c) Then
                data = data.Take(12).Concat(Td0Lzss.Unpack(data.Skip(12).ToArray())).ToArray()
            End If

            Dim tdVer = CInt(data(4))
            Dim dataRate = CInt(data(5))
            Dim stepping = CInt(data(7))
            Dim globalIsFm = (dataRate >> 7) = 1
            dataRate = dataRate And &H7F
            ErrorHandling.Check(dataRate <= 2, String.Format("TD0: bad data rate {0}", dataRate))
            Dim baseRate = New Integer() {250, 300, 500}(dataRate)

            Dim off = 12
            If (stepping And &H80) <> 0 Then
                ErrorHandling.Check(off + 10 <= data.Length, "TD0: bad comment header crc")
                Dim commentCrc = CInt(BitConverter.ToUInt16(data, off))
                Dim dlen = CInt(BitConverter.ToUInt16(data, off + 2))
                ErrorHandling.Check(off + 10 + dlen <= data.Length, "TD0: bad comment header crc")
                ' Python: crc16.new(dat[off+2:off+10+dlen]).crcValue == crc
                ErrorHandling.Check(Crc16Teledisk(data, off + 2, 8 + dlen) = commentCrc,
                                    "TD0: bad comment header crc")
                off += 10 + dlen
            End If

            While off < data.Length AndAlso data(off) <> &HFF
                ErrorHandling.Check(off + 4 <= data.Length, "TD0: bad track header crc")
                Dim nSec = CInt(data(off + 0))
                Dim cyl = CInt(data(off + 1))
                Dim headRaw = CInt(data(off + 2))
                Dim trackHeaderCrc = CInt(data(off + 3))
                ' Python: crc16.new(dat[off:off+3]).crcValue & 0xff == crc
                ErrorHandling.Check((Crc16Teledisk(data, off, 3) And &HFF) = trackHeaderCrc,
                                    "TD0: bad track header crc")
                off += 4

                Dim trackIsFm = (headRaw And &H80) = &H80 OrElse globalIsFm
                Dim head = headRaw And &H7F
                Dim formatName = If(trackIsFm, "ibm.fm", "ibm.mfm")
                Dim rate = If(trackIsFm, baseRate \ 2, baseRate)

                Dim sectorSizes As New List(Of Integer)()
                Dim sectorNs As New List(Of Integer)()
                Dim sectorIds As New List(Of Integer)()
                Dim sectorPayloads As New List(Of Byte())()
                Dim sectorDdamFlags As New List(Of Boolean)()

                For i = 0 To nSec - 1
                    ErrorHandling.Check(off + 6 <= data.Length, "TD0: bad sector data crc")
                    Dim idC = CInt(data(off + 0))
                    Dim idH = CInt(data(off + 1))
                    Dim idR = CInt(data(off + 2))
                    Dim idN = CInt(data(off + 3))
                    Dim flags = CInt(data(off + 4))
                    Dim sectorCrc = CInt(data(off + 5))
                    off += 6

                    Dim nativeSize = SectorSizeFromN(idN)
                    Dim blk As Byte()
                    If (flags And &H30) = 0 Then
                        ErrorHandling.Check(off + 3 <= data.Length, "TD0: bad sector data crc")
                        Dim dlen = CInt(BitConverter.ToUInt16(data, off))
                        Dim enc = CInt(data(off + 2))
                        off += 3
                        dlen -= 1
                        ErrorHandling.Check(dlen >= 0 AndAlso off + dlen <= data.Length, "TD0: bad sector data crc")
                        Dim packed(dlen - 1) As Byte
                        If dlen > 0 Then Array.Copy(data, off, packed, 0, dlen)
                        off += dlen
                        blk = DecodeSectorPayload(packed, enc)
                        ' Python: assert len(blk) == ibm.sec_sz(id_n);
                        '         error.check(crc16.new(blk).crcValue & 0xff == crc, 'TD0: bad sector data crc')
                        ErrorHandling.Check(blk.Length = nativeSize, "TD0: bad sector data crc")
                        ErrorHandling.Check((Crc16Teledisk(blk, 0, blk.Length) And &HFF) = sectorCrc,
                                            "TD0: bad sector data crc")
                    Else
                        blk = New Byte(nativeSize - 1) {}
                    End If

                    If blk.Length < nativeSize Then
                        blk = blk.Concat(Enumerable.Repeat(CByte(0), nativeSize - blk.Length)).ToArray()
                    ElseIf blk.Length > nativeSize Then
                        blk = blk.Take(nativeSize).ToArray()
                    End If

                    sectorNs.Add(idN)
                    sectorIds.Add(idR)
                    sectorSizes.Add(blk.Length)
                    sectorPayloads.Add(blk)
                    ' Python: if flags & 4: s.dam.mark = ibm.Mark.DDAM
                    sectorDdamFlags.Add((flags And 4) <> 0)
                Next

                ' Mirror Python's `IBMTrack_Fixed.from_config` oversize handling so
                ' tracks with non-standard / oversized sectors (e.g. copy-protected
                ' TD0s with a single n=6 8K sector) get a reduced gap3 and an
                ' extended track-length in bitcells. Without this, fixed-clock
                ' encoding overflows HFEv1's 16-bit per-track length field on
                ' tracks larger than ~32K encoded bytes per side.
                Dim timePerRev = 0.2
                Dim rpm = 300
                Const defaultGap1 As Integer = 50
                Const defaultGap2 As Integer = 22
                Const defaultGap4a As Integer = 80
                Const gapPresync As Integer = 12
                Dim mfmGap3Table = New Integer() {32, 54, 84, 116, 255, 255, 255, 255}
                Dim fmGap3Table = New Integer() {27, 42, 58, 138, 255, 255, 255, 255}
                Dim gap3Table = If(trackIsFm, fmGap3Table, mfmGap3Table)
                Dim baseGap1 = If(trackIsFm, 26, defaultGap1)
                Dim baseGap2 = If(trackIsFm, 11, defaultGap2)
                Dim baseGap4a = If(trackIsFm, 40, defaultGap4a)
                Dim basePresync = If(trackIsFm, 6, gapPresync)
                Dim synclen = If(trackIsFm, 1, 4)

                Dim gap1 = baseGap1
                Dim gap2 = baseGap2
                Dim gap4a = baseGap4a
                Dim idxSz = gap4a + basePresync + synclen + gap1
                Dim idamSz = basePresync + synclen + 4 + 2 + gap2
                Dim damSzPre = basePresync + synclen
                Dim damSzPostNoGap3 = 2

                Dim trackLenDecoded = idxSz + (idamSz + damSzPre + damSzPostNoGap3) * sectorIds.Count
                For Each n In sectorNs
                    trackLenDecoded += 128 << n
                Next
                Dim trackLenBitcells = trackLenDecoded * 16
                Dim trackLenBc = rate * 400 * 300 \ rpm

                Dim resolvedGap3 As Integer = 0
                If sectorIds.Count > 0 Then
                    Dim space = Math.Max(0, trackLenBc - trackLenBitcells)
                    Dim n0 = sectorNs(0)
                    Dim cap = If(n0 >= 0 AndAlso n0 < gap3Table.Length, gap3Table(n0), 255)
                    resolvedGap3 = Math.Min(space \ (16 * sectorIds.Count), cap)
                    trackLenBitcells += 16 * sectorIds.Count * resolvedGap3
                End If

                Dim preIndexSz = trackLenBc \ 100
                If sectorIds.Count > 0 Then
                    preIndexSz = Math.Max(0, preIndexSz - resolvedGap3 * 16)
                End If
                trackLenBitcells += preIndexSz

                If trackLenBitcells > trackLenBc Then
                    Dim newGap4a = gap4a \ 2
                    idxSz -= gap4a - newGap4a
                    trackLenBitcells -= gap4a - newGap4a
                    gap4a = newGap4a
                End If

                trackLenBc = Math.Max(trackLenBc, trackLenBitcells)
                Dim clock = timePerRev / trackLenBc

                Dim codec As New IbmTrackFixed(formatName,
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
                                               gap1Override:=gap1,
                                               gap2Override:=gap2,
                                               gap3Override:=resolvedGap3,
                                               gap4aOverride:=gap4a,
                                               gapByteOverride:=Nothing)

                Dim logicalOrder = Enumerable.Range(0, sectorIds.Count).OrderBy(Function(x) sectorIds(x)).ToList()
                Dim imageData As New List(Of Byte)()
                For Each idx In logicalOrder
                    imageData.AddRange(sectorPayloads(idx))
                Next
                codec.SetImgTrack(imageData.ToArray())
                _tracks(Tuple.Create(cyl, head)) = codec
            End While
        End Sub

        ' Python map: src/greaseweazle/image/td0.py::TD0.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/image/image.py::Image.emit_track (TD0 is read-only, so this override always errors)
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Throw New FatalException(String.Format("{0}: Cannot create TD0 image files", If(String.IsNullOrEmpty(FileName), "TD0", FileName)))
        End Sub

        ' Python map: src/greaseweazle/image/image.py::Image.get_image (TD0 is read-only, so this override always errors)
        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create TD0 image files", If(String.IsNullOrEmpty(FileName), "TD0", FileName)))
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeSectorPayload)
        Private Shared Function DecodeSectorPayload(packed As Byte(), enc As Integer) As Byte()
            If enc = 0 Then
                Return packed
            End If
            If enc = 1 Then
                Dim o = 0
                Dim out As New List(Of Byte)()
                While o + 4 <= packed.Length
                    Dim count = CInt(BitConverter.ToUInt16(packed, o))
                    Dim b0 = packed(o + 2)
                    Dim b1 = packed(o + 3)
                    For i = 1 To count
                        out.Add(b0)
                        out.Add(b1)
                    Next
                    o += 4
                End While
                Return out.ToArray()
            End If
            If enc = 2 Then
                Dim o = 0
                Dim out As New List(Of Byte)()
                While o + 2 <= packed.Length
                    Dim c = CInt(packed(o))
                    Dim n = CInt(packed(o + 1))
                    o += 2
                    If c = 0 Then
                        ErrorHandling.Check(o + n <= packed.Length, "TD0: bad sector data crc")
                        For i = 0 To n - 1
                            out.Add(packed(o + i))
                        Next
                        o += n
                    Else
                        ErrorHandling.Check(o + c * 2 <= packed.Length, "TD0: bad sector data crc")
                        Dim patternLen = c * 2
                        Dim pattern(patternLen - 1) As Byte
                        Array.Copy(packed, o, pattern, 0, patternLen)
                        o += patternLen
                        For i = 1 To n
                            out.AddRange(pattern)
                        Next
                    End If
                End While
                Return out.ToArray()
            End If
            Throw New FatalException(String.Format("TD0: Unsupported sector encoding {0}", enc))
        End Function

        ' Python map: src/greaseweazle/image/td0.py::(no direct 1:1 symbol; VB helper for crcmod 'crc-16-teledisk')
        ' CRC-16/TELEDISK: poly=0xA097, init=0, refin=false, refout=false, xorout=0.
        Private Shared ReadOnly TeledidskCrcTable As UShort() = BuildTeledidskTable()
        Private Shared Function BuildTeledidskTable() As UShort()
            Dim table(255) As UShort
            Const poly As UShort = &HA097US
            For i = 0 To 255
                Dim r As UShort = CUShort(i << 8)
                For j = 0 To 7
                    If (r And &H8000US) <> 0US Then
                        r = CUShort(((r << 1) Xor poly) And &HFFFF)
                    Else
                        r = CUShort((r << 1) And &HFFFF)
                    End If
                Next
                table(i) = r
            Next
            Return table
        End Function

        Private Shared Function Crc16Teledisk(buffer As Byte(), offset As Integer, length As Integer) As Integer
            Dim crc As UShort = 0
            For i = offset To offset + length - 1
                crc = CUShort((TeledidskCrcTable((CInt(crc >> 8) Xor buffer(i)) And &HFF)) Xor CUShort((crc << 8) And &HFFFF))
            Next
            Return crc
        End Function

    End Class

End Namespace
