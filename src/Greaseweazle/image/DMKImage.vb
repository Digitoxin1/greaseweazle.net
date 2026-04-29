Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/dmk.py::DMK
    Public Class Dmk
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), MasterTrack)()
        Private Shared ReadOnly EncodeList As UShort() = DoubleBitCodec.EncodeTable

        ' Python map: src/greaseweazle/image/dmk.py::DMK.__init__
        Public Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        ' Python map: src/greaseweazle/image/dmk.py::DMK.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 16, "DMK: Header is too short")

            Dim ncyl = CInt(data(1))
            Dim tlen = CInt(BitConverter.ToUInt16(data, 2))
            Dim flags = CInt(data(4))
            ErrorHandling.Check((flags And &H2F) = 0,
                                String.Format("DMK: Unrecognised flags value 0x{0:X2}", flags))

            Dim nside = If((flags And &H10) <> 0, 1, 2)
            Dim fmStep = If((flags And &HC0) <> 0, 1, 2)

            Dim o = 16
            For cyl = 0 To ncyl - 1
                For head = 0 To nside - 1
                    ErrorHandling.Check(tlen >= 128, "DMK: Invalid track length")
                    ErrorHandling.Check(o + tlen <= data.Length, "DMK: Truncated track data")

                    Dim offsets As New List(Of Tuple(Of Boolean, Integer))()
                    For i = 0 To 63
                        Dim entry = CInt(BitConverter.ToUInt16(data, o + i * 2))
                        If entry = 0 Then
                            Continue For
                        End If
                        Dim isMfm = (entry And &H8000) = &H8000
                        Dim off = (entry And &H3FFF) - 128
                        offsets.Add(Tuple.Create(isMfm, off))
                    Next

                    Dim prev = -1
                    For i = 0 To offsets.Count - 1
                        Dim off = offsets(i).Item2
                        If off <= prev Then
                            offsets = offsets.Take(i).ToList()
                            Exit For
                        End If
                        prev = off
                    Next

                    Dim trackDataLen = tlen - 128
                    Dim trackData(trackDataLen - 1) As Byte
                    Array.Copy(data, o + 128, trackData, 0, trackDataLen)
                    o += tlen

                    If offsets.Count = 0 Then
                        Continue For
                    End If

                    Dim enc As New [Encoding](trackData.Length, offsets(0).Item1, fmStep)
                    For Each entry In offsets
                        Dim isMfm = entry.Item1
                        Dim off = entry.Item2
                        If isMfm Then
                            enc.MfmOff(trackData, off)
                            Dim dam = FindPattern(trackData, off + 8, off + 64, &HA1, &HA1, &HA1)
                            ErrorHandling.Check(dam <> -1, "DMK: No MFM DAM sync found")
                            enc.MoveCursor(dam + 8)
                            enc.Clock(dam + 0) = &HA
                            enc.Clock(dam + 1) = &HA
                            enc.Clock(dam + 2) = &HA
                        Else
                            Dim stepSize = enc.FmStep
                            off = off And Not (stepSize - 1)
                            ErrorHandling.Check(off >= 0 AndAlso off < trackData.Length, "DMK: Invalid FM IDAM offset")
                            If trackData(off) = 0 Then
                                off += stepSize
                            End If
                            ErrorHandling.Check(off >= 0 AndAlso off < trackData.Length, "DMK: Invalid FM IDAM offset")

                            enc.FmOff(trackData, off)
                            enc.Clock(off) = &HC7

                            ' Python:
                            '   for dam in range(off+8*step, off+64*step, step):
                            '       if (data[dam-step] == 0 and data[dam] & 0xf0 == 0xf0): break
                            ' Python's loop variable retains its last assigned value when the loop
                            ' falls through, so failure to match yields dam == off + 63*step.
                            Dim damStart = off + 8 * stepSize
                            Dim damStop = off + 64 * stepSize
                            Dim dam As Integer = damStart
                            Dim probe = damStart
                            While probe < damStop
                                dam = probe
                                If probe - stepSize >= 0 AndAlso probe - stepSize < trackData.Length AndAlso
                                   probe < trackData.Length AndAlso
                                   trackData(probe - stepSize) = 0 AndAlso (trackData(probe) And &HF0) = &HF0 Then
                                    Exit While
                                End If
                                probe += stepSize
                            End While
                            enc.MoveCursor(dam + 8 * stepSize)
                            ErrorHandling.Check(dam >= 0 AndAlso dam < enc.Clock.Length, "DMK: FM DAM offset out of range")
                            enc.Clock(dam) = &HC7
                        End If
                    Next
                    enc.MoveCursor(trackData.Length)

                    Dim encoded As New List(Of Byte)()
                    Dim fmMask = enc.FmStep - 1
                    For i = 0 To trackData.Length - 1
                        Dim isMfm = enc.Mfm(i)
                        If Not isMfm AndAlso (i And fmMask) <> 0 Then
                            Continue For
                        End If

                        Dim d = CInt(EncodeList(trackData(i)))
                        Dim c = CInt(EncodeList(enc.Clock(i)))
                        Dim word = ((c << 1) Or d) And &HFFFF
                        Dim pair = New Byte() {CByte((word >> 8) And &HFF), CByte(word And &HFF)}
                        If isMfm Then
                            encoded.AddRange(MfmEncode(pair))
                        Else
                            encoded.AddRange(EncodeBytes(FmEncode(pair)))
                        End If
                    Next

                    Dim mt As New MasterTrack(BytesToBits(encoded.ToArray()), ResolveTimePerRev(encoded.Count))
                    _tracks(Tuple.Create(cyl, head)) = mt
                Next
            Next
        End Sub

        ' Python map: src/greaseweazle/image/dmk.py::DMK.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EmitTrack)
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Throw New FatalException(String.Format("{0}: Cannot create DMK image files", If(String.IsNullOrEmpty(FileName), "DMK", FileName)))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create DMK image files", If(String.IsNullOrEmpty(FileName), "DMK", FileName)))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveTimePerRev)
        Private Shared Function ResolveTimePerRev(byteLen As Integer) As Double
            Dim timePerRev = 0.2
            Dim dlen = (byteLen * 8) \ 1000
            If (dlen >= 80 AndAlso dlen <= 85) OrElse (dlen >= 160 AndAlso dlen <= 170) Then
                timePerRev *= 5.0 / 6.0
            End If
            Return timePerRev
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindPattern)
        Private Shared Function FindPattern(data As Byte(),
                                            startInclusive As Integer,
                                            endExclusive As Integer,
                                            b0 As Byte,
                                            b1 As Byte,
                                            b2 As Byte) As Integer
            Dim startIdx = Math.Max(0, startInclusive)
            Dim endIdx = Math.Min(Math.Max(0, endExclusive), data.Length)
            For i = startIdx To endIdx - 3
                If data(i) = b0 AndAlso data(i + 1) = b1 AndAlso data(i + 2) = b2 Then
                    Return i
                End If
            Next
            Return -1
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function BytesToBits(data As Byte()) As IEnumerable(Of Boolean)
            Return BitHelpers.BytesToBits(data)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FmEncode)
        Private Shared Function FmEncode(dat As Byte()) As Byte()
            Dim out As Byte() = CType(dat.Clone(), Byte())
            For i = 0 To out.Length - 1
                If (out(i) And &HAA) = 0 Then
                    out(i) = CByte(out(i) Or &HAA)
                End If
            Next
            Return out
        End Function

        ' Python map: shared helper. See IbmHelpers.MfmEncode in IBMFixedCodec.vb.
        Private Shared Function MfmEncode(dat As Byte()) As Byte()
            Return IbmHelpers.MfmEncode(dat)
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.DoubleBitCodec.
        Private Shared Function EncodeBytes(dat As Byte()) As Byte()
            Return DoubleBitCodec.Encode(dat)
        End Function

        ' Python map: src/greaseweazle/image/dmk.py::Encoding
        Private NotInheritable Class [Encoding]
            ' Python map: src/greaseweazle/image/dmk.py::Encoding.__init__
            Public Sub New(length As Integer, initialMfm As Boolean, fmStep As Integer)
                Me.FmStep = fmStep
                ReDim Mfm(length - 1)
                ReDim Clock(length - 1)
                Cursor = 0
                PrevMfm = initialMfm
            End Sub

            Public ReadOnly Property FmStep As Integer
            Public ReadOnly Property Mfm As Boolean()
            Public ReadOnly Property Clock As Byte()

            Private Property Cursor As Integer
            Private Property PrevMfm As Boolean

            ' Python map: src/greaseweazle/image/dmk.py::Encoding.move_cursor
            Public Sub MoveCursor(offset As Integer)
                Dim target = Math.Max(0, Math.Min(offset, Mfm.Length))
                While Cursor < target
                    Mfm(Cursor) = PrevMfm
                    Cursor += 1
                End While
            End Sub

            ' Python map: src/greaseweazle/image/dmk.py::Encoding.mfm_off
            Public Sub MfmOff(data As Byte(), off As Integer)
                Dim areas = {
                    Tuple.Create(3, CByte(&HA1), CByte(&HA)),
                    Tuple.Create(12, CByte(&H0), CByte(&H0)),
                    Tuple.Create(512, CByte(&H4E), CByte(&H0)),
                    Tuple.Create(1, CByte(&HFC), CByte(&H0)),
                    Tuple.Create(3, CByte(&HC2), CByte(&H14)),
                    Tuple.Create(12, CByte(&H0), CByte(&H0)),
                    Tuple.Create(512, CByte(&H4E), CByte(&H0))
                }
                ApplyOffAreas(areas, data, off, 1)
                PrevMfm = True
            End Sub

            ' Python map: src/greaseweazle/image/dmk.py::Encoding.fm_off
            Public Sub FmOff(data As Byte(), off As Integer)
                Dim areas = {
                    Tuple.Create(6, CByte(&H0), CByte(&H0)),
                    Tuple.Create(256, CByte(&HFF), CByte(&H0)),
                    Tuple.Create(1, CByte(&HFC), CByte(&HD7)),
                    Tuple.Create(6, CByte(&H0), CByte(&H0))
                }
                ApplyOffAreas(areas, data, off, FmStep)
                PrevMfm = False
            End Sub

            ' Python map: src/greaseweazle/image/dmk.py::Encoding._off
            Private Sub ApplyOffAreas(areas As IEnumerable(Of Tuple(Of Integer, Byte, Byte)),
                                      data As Byte(),
                                      off As Integer,
                                      stepSize As Integer)
                ' Python does not clamp `off`; it relies on caller-provided offsets being
                ' inside `data`. Mirror that and only protect against malformed images
                ' via explicit bounds checks before reads/writes.
                Dim work = off
                For Each a In areas
                    Dim minOff = Math.Max(work - a.Item1 * stepSize, 0)
                    While work > minOff AndAlso work - stepSize >= 0 AndAlso work - stepSize < data.Length AndAlso data(work - stepSize) = a.Item2
                        work -= stepSize
                        If work >= 0 AndAlso work < Clock.Length Then
                            Clock(work) = a.Item3
                        End If
                    End While
                Next
                MoveCursor(work)
            End Sub
        End Class

    End Class

    ' Python map: src/greaseweazle/image/dmk.py::DMKTrack
    Public Class DmkTrack
        Public ReadOnly Property Track As MasterTrack

        ' Python map: src/greaseweazle/image/dmk.py::DMKTrack.__init__
        Public Sub New(track As MasterTrack)
            Me.Track = track
        End Sub

        ' Python map: src/greaseweazle/image/dmk.py::DMKTrack.master_track
        Public Function MasterTrack() As MasterTrack
            Return Track
        End Function
    End Class

End Namespace
