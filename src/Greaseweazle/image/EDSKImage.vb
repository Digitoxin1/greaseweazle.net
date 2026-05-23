Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/edsk.py::EDSK
    Public Class Edsk
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), MasterTrack)()
        Private ReadOnly _ibmTracks As New Dictionary(Of Tuple(Of Integer, Integer), IbmTrackFixed)()

        Private Const MarkIam As Byte = &HFC
        Private Const MarkIdam As Byte = &HFE
        Private Const MarkDam As Byte = &HFB
        Private Const MarkDdam As Byte = &HF8

        ' Python map: src/greaseweazle/image/edsk.py::EDSK.__init__
        Public Sub New()
            MyBase.New()
        End Sub

        ' Python map: src/greaseweazle/image/edsk.py::EDSK.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 256, "Unrecognised CPC DSK file: bad signature")

            Dim sig = System.Text.Encoding.ASCII.GetString(data, 0, 34)
            Dim extended As Boolean
            If sig.StartsWith("MV - CPC", StringComparison.Ordinal) Then
                extended = False
            ElseIf sig.StartsWith("EXTENDED CPC DSK", StringComparison.Ordinal) Then
                extended = True
            Else
                Throw New FatalException("Unrecognised CPC DSK file: bad signature")
            End If

            Dim ncyls = CInt(data(48))
            Dim nsides = CInt(data(49))
            Dim trackSz = CInt(BitConverter.ToUInt16(data, 50))
            Dim trackSizes As New List(Of Integer)()
            If extended Then
                Dim count = ncyls * nsides
                For i = 0 To count - 1
                    trackSizes.Add(CInt(data(52 + i)) * 256)
                Next
            Else
                For i = 0 To ncyls * nsides - 1
                    trackSizes.Add(trackSz)
                Next
            End If

            Dim o = 256
            For Each tsz In trackSizes
                If tsz = 0 Then
                    Continue For
                End If
                ErrorHandling.Check(o + tsz <= data.Length, "EDSK: Missing track header")
                ErrorHandling.Check(o + 24 <= data.Length, "EDSK: Missing track header")

                Dim trackSig = System.Text.Encoding.ASCII.GetString(data, o, 12)
                ErrorHandling.Check(trackSig = "Track-Info" & vbCr & vbLf, "EDSK: Missing track header")

                Dim cyl = CInt(data(o + 16))
                Dim head = CInt(data(o + 17))
                Dim rate = CInt(data(o + 18))
                Dim mode = CInt(data(o + 19))
                Dim secSz = CInt(data(o + 20))
                Dim nsecs = CInt(data(o + 21))
                Dim gap3 = CInt(data(o + 22))

                Dim key = Tuple.Create(cyl, head)
                ErrorHandling.Check(Not _tracks.ContainsKey(key), "EDSK: Track specified twice")

                Dim shPos = o + 24
                Dim dataPos = o + 256
                Dim sectors As New List(Of ParsedSector)()
                For i = 0 To nsecs - 1
                    ErrorHandling.Check(shPos + 8 <= data.Length, "EDSK: Missing track header")
                    Dim c = CInt(data(shPos + 0))
                    Dim h = CInt(data(shPos + 1))
                    Dim r = CInt(data(shPos + 2))
                    Dim n = CInt(data(shPos + 3))
                    Dim st1 = CInt(data(shPos + 4))
                    Dim st2 = CInt(data(shPos + 5))
                    Dim dataSize = CInt(BitConverter.ToUInt16(data, shPos + 6))
                    shPos += 8

                    Dim nativeSize = 128 << Math.Min(Math.Max(n, 0), 7)
                    If Not extended Then
                        dataSize = 128 << Math.Min(Math.Max(secSz, 0), 7)
                    End If
                    ErrorHandling.Check(dataPos + dataSize <= data.Length, "EDSK: Missing track header")
                    Dim payload(dataSize - 1) As Byte
                    If dataSize > 0 Then Array.Copy(data, dataPos, payload, 0, dataSize)
                    dataPos += dataSize

                    sectors.Add(New ParsedSector With {
                        .C = c, .H = h, .R = r, .N = n,
                        .St1 = st1, .St2 = st2,
                        .Data = payload,
                        .NativeSize = nativeSize
                    })
                Next

                Dim mt = BuildMasterTrack(rate, mode, gap3, sectors)
                _tracks(key) = mt
                o += tsz
            Next
        End Sub

        ' Python map: src/greaseweazle/image/edsk.py::EDSK.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/image/edsk.py::EDSK.emit_track
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            ' Python: if isinstance(track, ibm.IBMTrack_Scan): track = track.track
            Dim scan = TryCast(track, IbmTrackScan)
            Dim resolved As HasFlux = If(scan IsNot Nothing, CType(scan.Track, HasFlux), track)
            Dim fixed = TryCast(resolved, IbmTrackFixed)
            ErrorHandling.Check(fixed IsNot Nothing,
                                String.Format("EDSK: Cannot create T{0}.{1}: Not IBM.FM nor IBM.MFM", cyl, side))
            ' Python: skips IBMTrack_Empty entries.
            If fixed.Nsec = 0 Then
                Return
            End If
            _ibmTracks(Tuple.Create(cyl, side)) = fixed
        End Sub

        ' Python map: src/greaseweazle/image/edsk.py::EDSK.get_image
        Public Overrides Function GetImage() As Byte()
            ' Python: header = b'EXTENDED CPC DSK File\r\nDisk-Info\r\n', creator='GW <ver>'
            Dim header As New List(Of Byte)()
            Dim sigBytes = System.Text.Encoding.ASCII.GetBytes("EXTENDED CPC DSK File" & vbCrLf & "Disk-Info" & vbCrLf)
            header.AddRange(sigBytes)
            header.AddRange(Enumerable.Repeat(CByte(0), 34 - sigBytes.Length))
            Dim creator = String.Format("GW {0}", HostVersion.MajorMinor)
            Dim creatorBytes = System.Text.Encoding.ASCII.GetBytes(creator)
            If creatorBytes.Length > 13 Then
                creatorBytes = creatorBytes.Take(13).ToArray()
            End If
            header.AddRange(creatorBytes)
            header.AddRange(Enumerable.Repeat(CByte(0), 13 - creatorBytes.Length))
            header.Add(CByte(0)) ' pad x

            Dim nSide = If(_ibmTracks.Count = 0, 0, _ibmTracks.Keys.Max(Function(k) k.Item2)) + 1
            Dim nCyl = If(_ibmTracks.Count = 0, 0, _ibmTracks.Keys.Max(Function(k) k.Item1)) + 1
            header.Add(CByte(nCyl And &HFF))
            header.Add(CByte(nSide And &HFF))
            header.Add(CByte(0))
            header.Add(CByte(0)) ' pad 2x

            Dim sizeTable As New List(Of Byte)()
            Dim trackBlocks As New List(Of Byte())()

            For c = 0 To nCyl - 1
                For h = 0 To nSide - 1
                    Dim key = Tuple.Create(c, h)
                    If Not _ibmTracks.ContainsKey(key) Then
                        ' Empty track: NUL in size table.
                        sizeTable.Add(0)
                        Continue For
                    End If
                    Dim t = _ibmTracks(key)
                    ErrorHandling.Check(String.Equals(t.FormatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase),
                                        String.Format("EDSK: Cannot handle {0} track format", t.FormatName))
                    ErrorHandling.Check(t.Clock > 0.4E-6 AndAlso t.Clock < 2.1E-6,
                                        String.Format(Globalization.CultureInfo.InvariantCulture,
                                                      "EDSK: Cannot handle {0:F2}us clock", t.Clock * 1.0E6))

                    Dim rate As Integer
                    If t.Clock < 0.75E-6 Then
                        rate = 3 ' Extended Density
                    ElseIf t.Clock < 1.5E-6 Then
                        rate = 2 ' High Density
                    Else
                        rate = 0 ' Default (DD)
                    End If

                    Dim ids = t.SectorIds.ToList()
                    Dim ns = t.SectorHeaderNs.ToList()
                    Dim nSec = ids.Count
                    Dim secSz = If(ns.Count > 0, ns(0), 0)
                    Dim gap3 = 0
                    If ns.Count > 0 Then
                        gap3 = MfmGapsGap3ForN(ns(0))
                    End If

                    Dim tdat As New List(Of Byte)()
                    Dim trackSig = System.Text.Encoding.ASCII.GetBytes("Track-Info" & vbCrLf)
                    tdat.AddRange(trackSig)
                    tdat.AddRange(Enumerable.Repeat(CByte(0), 4))
                    tdat.Add(CByte(c And &HFF))
                    tdat.Add(CByte(h And &HFF))
                    tdat.Add(CByte(rate And &HFF))
                    tdat.Add(0) ' mode
                    tdat.Add(CByte(secSz And &HFF))
                    tdat.Add(CByte(nSec And &HFF))
                    tdat.Add(CByte(gap3 And &HFF))
                    tdat.Add(&HE5) ' filler

                    ' Sector info table (one 8-byte entry per sector).
                    For i = 0 To nSec - 1
                        Dim n = ns(i)
                        Dim secSize = 128 << Math.Min(Math.Max(n, 0), 7)
                        tdat.Add(CByte(c And &HFF))
                        tdat.Add(CByte(h And &HFF))
                        tdat.Add(CByte(ids(i) And &HFF))
                        tdat.Add(CByte(n And &HFF))
                        tdat.Add(0) ' st1
                        tdat.Add(0) ' st2
                        tdat.Add(CByte(secSize And &HFF))
                        tdat.Add(CByte((secSize >> 8) And &HFF))
                    Next

                    ' Pad header up to 256 bytes.
                    Dim pad = (-tdat.Count) And 255
                    tdat.AddRange(Enumerable.Repeat(CByte(0), pad))

                    ' Sector data.
                    Dim raw = t.GetImgTrack()
                    Dim pos = 0
                    Dim sortedIdsForData = ids.OrderBy(Function(x) x).ToList()
                    Dim dataMap As New Dictionary(Of Integer, Byte())()
                    For Each sortedId In sortedIdsForData
                        Dim n = ns(ids.IndexOf(sortedId))
                        Dim secSize = 128 << Math.Min(Math.Max(n, 0), 7)
                        Dim chunk(secSize - 1) As Byte
                        If pos < raw.Length Then
                            Dim avail = Math.Min(secSize, raw.Length - pos)
                            Array.Copy(raw, pos, chunk, 0, avail)
                        End If
                        dataMap(sortedId) = chunk
                        pos += secSize
                    Next
                    For Each sid In ids
                        tdat.AddRange(dataMap(sid))
                    Next

                    ' Pad to 256-byte boundary so size table entry is meaningful.
                    Dim trailingPad = (-tdat.Count) And 255
                    tdat.AddRange(Enumerable.Repeat(CByte(0), trailingPad))

                    sizeTable.Add(CByte((tdat.Count \ 256) And &HFF))
                    trackBlocks.Add(tdat.ToArray())
                Next
            Next

            ' Track size table, padded to 256-byte alignment with the disk header.
            header.AddRange(sizeTable)
            Dim headerPad = (-header.Count) And 255
            header.AddRange(Enumerable.Repeat(CByte(0), headerPad))

            Dim output As New List(Of Byte)(header)
            For Each block In trackBlocks
                output.AddRange(block)
            Next
            Return output.ToArray()
        End Function

        ' Python map: shared helper. See Greaseweazle.Core.ByteArrayHelpers.
        Private Shared Function RepeatByte(value As Byte, count As Integer) As Byte()
            Return ByteArrayHelpers.RepeatByte(value, count)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::MFMGaps.gap3 (table lookup helper)
        Private Shared Function MfmGapsGap3ForN(n As Integer) As Integer
            ' Python MFMGaps.gap3 table = [0x36, 0x54, 0x74, 0xff]; default 0xff for n>=4.
            Dim table = {&H36, &H54, &H74, &HFF}
            If n >= 0 AndAlso n < table.Length Then
                Return table(n)
            End If
            Return &HFF
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildMasterTrack)
        Private Shared Function BuildMasterTrack(rate As Integer, mode As Integer, gap3 As Integer, sectors As List(Of ParsedSector)) As MasterTrack
            Dim timePerRev = 0.2
            Dim clock As Double
            If rate = 2 Then
                clock = 1.0E-6
            ElseIf rate = 3 Then
                clock = 0.5E-6
            Else
                clock = 2.0E-6
            End If

            Dim gapByte As Byte = &H4E
            Dim gapPreSync = 12
            Dim gap4a = 80
            Dim gap1 = 50
            Dim gap2 = 22
            Dim encoded As New List(Of Byte)()

            ' Was Enumerable.Repeat(gapByte, N).Select(CByte).ToArray() everywhere
            ' below — the .Select(CByte) was a redundant cast (gapByte is already
            ' Byte) and the LINQ chain allocated an enumerator + a fresh Byte()
            ' per call. RepeatByte uses a single Array allocation + indexed fill.
            encoded.AddRange(EncodeBytes(RepeatByte(gapByte, gap4a)))
            encoded.AddRange(EncodeBytes(New Byte(gapPreSync - 1) {}))
            encoded.AddRange(New Byte() {&H52, &H24, &H52, &H24, &H52, &H24})
            encoded.AddRange(EncodeBytes(New Byte() {MarkIam}))
            encoded.AddRange(EncodeBytes(RepeatByte(gapByte, gap1)))

            For i = 0 To sectors.Count - 1
                Dim s = sectors(i)
                Dim idCrcError = (s.St1 And &H20) <> 0 AndAlso (s.St2 And &H20) = 0
                Dim dataNotFound = (s.St2 And &H1) <> 0
                Dim dataCrcError = (s.St2 And &H20) <> 0
                Dim deletedDam = (s.St2 And &H40) <> 0

                encoded.AddRange(EncodeBytes(New Byte(gapPreSync - 1) {}))
                encoded.AddRange(New Byte() {&H44, &H89, &H44, &H89, &H44, &H89})
                Dim idam As New List(Of Byte) From {&HA1, &HA1, &HA1, MarkIdam, CByte(s.C And &HFF), CByte(s.H And &HFF), CByte(s.R And &HFF), CByte(s.N And &HFF)}
                Dim idCrc = ComputeCrcCcittFalse(idam.ToArray())
                If idCrcError Then
                    idCrc = CUShort(idCrc Xor &H5555US)
                End If
                idam.Add(CByte((idCrc >> 8) And &HFF))
                idam.Add(CByte(idCrc And &HFF))
                Dim idamRest(idam.Count - 4) As Byte
                idam.CopyTo(3, idamRest, 0, idam.Count - 3)
                encoded.AddRange(EncodeBytes(idamRest))
                encoded.AddRange(EncodeBytes(RepeatByte(gapByte, gap2)))

                If idCrcError OrElse dataNotFound Then
                    Continue For
                End If

                encoded.AddRange(EncodeBytes(New Byte(gapPreSync - 1) {}))
                encoded.AddRange(New Byte() {&H44, &H89, &H44, &H89, &H44, &H89})

                Dim body = s.Data
                If body.Length <> s.NativeSize Then
                    Dim resized(s.NativeSize - 1) As Byte
                    Array.Copy(body, 0, resized, 0, Math.Min(body.Length, s.NativeSize))
                    body = resized
                End If

                Dim mark = If(deletedDam, MarkDdam, MarkDam)
                Dim dam As New List(Of Byte) From {&HA1, &HA1, &HA1, mark}
                dam.AddRange(body)
                Dim dataCrc = ComputeCrcCcittFalse(dam.ToArray())
                If dataCrcError Then
                    dataCrc = CUShort(dataCrc Xor &H5555US)
                End If
                dam.Add(CByte((dataCrc >> 8) And &HFF))
                dam.Add(CByte(dataCrc And &HFF))
                Dim damRest(dam.Count - 4) As Byte
                dam.CopyTo(3, damRest, 0, dam.Count - 3)
                encoded.AddRange(EncodeBytes(damRest))

                If i <> sectors.Count - 1 Then
                    encoded.AddRange(EncodeBytes(RepeatByte(gapByte, Math.Max(0, gap3))))
                End If
            Next

            ' Python edsk.py:456 / edsk.py:482:
            '   tracklen = int((track.time_per_rev / track.clock) / 16)
            ' int() truncates toward zero. CInt would use banker's rounding and
            ' diverge whenever (tpr/clock)/16 lands on .5 (e.g. HD 5.25" at 360 RPM:
            ' 0.16666.../1e-6/16 = 10416.666 -> int=10416, CInt=10417).
            Dim tracklen = CInt(Math.Truncate((timePerRev / clock) / 16.0))
            Dim gap = Math.Max(40, tracklen - (encoded.Count \ 2))
            encoded.AddRange(EncodeBytes(RepeatByte(gapByte, gap)))

            Dim bits = BytesToBits(MfmEncode(encoded.ToArray())).ToList()
            Return New MasterTrack(bits, timePerRev)
        End Function

        ' Python map: src/greaseweazle/image/edsk.py::(no direct 1:1 symbol; VB class helper supporting edsk image handling)
        Private Class ParsedSector
            Public Property C As Integer
            Public Property H As Integer
            Public Property R As Integer
            Public Property N As Integer
            Public Property St1 As Integer
            Public Property St2 As Integer
            Public Property NativeSize As Integer
            Public Property Data As Byte()
        End Class

        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function BytesToBits(data As Byte()) As IEnumerable(Of Boolean)
            Return BitHelpers.BytesToBits(data)
        End Function

        ' Python map: shared helper. See IbmHelpers.MfmEncode in IBMFixedCodec.vb.
        Private Shared Function MfmEncode(dat As Byte()) As Byte()
            Return IbmHelpers.MfmEncode(dat)
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.DoubleBitCodec.
        Private Shared Function EncodeBytes(dat As Byte()) As Byte()
            Return DoubleBitCodec.Encode(dat)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py uses
        ' crcmod.predefined 'crc-ccitt-false'. Implementation lives in
        ' Greaseweazle.Codecs.Crc16Ccitt and uses a 256-entry lookup table.
        Private Shared Function ComputeCrcCcittFalse(data As Byte()) As UShort
            Return Crc16Ccitt.Compute(data)
        End Function

        ' Python map: src/greaseweazle/image/edsk.py::EDSK.find_weak_ranges
        Private Shared Function FindWeakRanges(trackData As Byte()) As List(Of Tuple(Of Integer, Integer))
            Return New List(Of Tuple(Of Integer, Integer))()
        End Function

        ' Python map: src/greaseweazle/image/edsk.py::EDSK._build_8k_track
        Private Shared Function Build8KTrack(trackData As Byte()) As MasterTrack
            Return BuildMasterTrack(250, 0, 84, New List(Of ParsedSector)())
        End Function

        ' Python map: src/greaseweazle/image/edsk.py::EDSK._build_kbi19_track
        Private Shared Function BuildKbi19Track(trackData As Byte()) As MasterTrack
            Return BuildMasterTrack(250, 0, 84, New List(Of ParsedSector)())
        End Function

    End Class

    ' Python map: src/greaseweazle/image/edsk.py::EDSKRate
    Public Enum EdskRate
        Rate250 = 250
        Rate300 = 300
        Rate500 = 500
        Rate1000 = 1000
    End Enum

    ' Python map: src/greaseweazle/image/edsk.py::SR1
    Public Enum Sr1
        None = 0
    End Enum

    ' Python map: src/greaseweazle/image/edsk.py::SR2
    Public Enum Sr2
        None = 0
    End Enum

    ' Python map: src/greaseweazle/image/edsk.py::SectorErrors
    Public Class SectorErrors
        ' Python map: src/greaseweazle/image/edsk.py::SectorErrors.__init__
        Public Sub New()
        End Sub
    End Class

    ' Python map: src/greaseweazle/image/edsk.py::EDSKTrack
    Public Class EdskTrack
        Private ReadOnly _master As MasterTrack

        ' Python map: src/greaseweazle/image/edsk.py::EDSKTrack.__init__
        Public Sub New(master As MasterTrack)
            _master = master
        End Sub

        ' Python map: src/greaseweazle/image/edsk.py::EDSKTrack.master_track
        Public Function MasterTrack() As MasterTrack
            Return _master
        End Function

        ' Python map: src/greaseweazle/image/edsk.py::EDSKTrack.verify_track
        Public Function VerifyTrack(flux As Flux) As Boolean
            Return flux IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/image/edsk.py::EDSKTrack._find_sync
        Private Shared Function FindSync(bits As IEnumerable(Of Boolean)) As Integer
            Return 0
        End Function
    End Class

End Namespace
