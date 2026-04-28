Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFMDef
    Public Class HpMmfmDef
        Implements TrackDef

        Private _interleave As Integer = 1
        Private _cskew As Integer = 0
        Private _hskew As Integer = 0
        Private _secs As Integer?

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFMDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.1
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFMDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    _secs = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case "interleave", "cskew", "hskew"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 0 AndAlso n <= 255, String.Format("{0} out of range", key))
                    If key = "interleave" Then
                        _interleave = n
                    ElseIf key = "cskew" Then
                        _cskew = n
                    Else
                        _hskew = n
                    End If
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFMDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_secs.HasValue, "number of sectors not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFMDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New HpMmfm(cyl, head, _secs.GetValueOrDefault(), _interleave, _cskew, _hskew)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM
    Public Class HpMmfm
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.1
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Const TimePerRev As Double = 60.0 / 360.0
        Private Const Clock As Double = 1.0E-6
        Private Shared ReadOnly BadSector As Byte() =
            Enumerable.Repeat(System.Text.Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-"), 16).SelectMany(Function(x) x).ToArray()
        Private Shared ReadOnly SectorSyncBytes As Byte() = {&H55, &H55, &H2A, &H54}
        Private Shared ReadOnly DataSyncBytes As Byte() = {&H55, &H55, &H2A, &H44}
        Private Shared ReadOnly SectorSyncPattern As Boolean() = BytesToBits(SectorSyncBytes)
        Private Shared ReadOnly DataSyncPattern As Boolean() = BytesToBits(DataSyncBytes)
        Private Shared ReadOnly BitRevMap As Byte() = BuildBitRevMap()
        Private Shared ReadOnly MmfmList As Byte() = BuildMmfmList()

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _nsec As Integer
        Private ReadOnly _interleave As Integer
        Private ReadOnly _cskew As Integer
        Private ReadOnly _hskew As Integer
        Private ReadOnly _sectors As List(Of Byte())

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.__init__
        Public Sub New(cyl As Integer, head As Integer, nsec As Integer, interleave As Integer, cskew As Integer, hskew As Integer)
            _cyl = cyl
            _head = head
            _nsec = nsec
            _interleave = interleave
            _cskew = cskew
            _hskew = hskew
            _sectors = Enumerable.Repeat(Of Byte())(Nothing, nsec).ToList()
        End Sub

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _nsec
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _nsec AndAlso _sectors(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectors.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim output As New List(Of Byte)(_nsec * 256)
            For sec = 0 To _nsec - 1
                output.AddRange(If(_sectors(sec), BadSector))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = _nsec * 256
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For sec = 0 To _nsec - 1
                Dim data(255) As Byte
                Array.Copy(src, sec * 256, data, 0, 256)
                _sectors(sec) = data
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim raw As New PllTrack(timePerRev:=TimePerRev, clock:=Clock, data:=track, pll:=pll)
            Dim bits = raw.GetAllData().Item1

            For Each offs In FindPatternOffsets(bits, SectorSyncPattern)
                If NrMissing() = 0 Then Exit For
                ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.decode_flux
                ' Python decodes directly with no slip search; CRC must be valid as-read.
                Dim idamOffs = offs + 2 * 16
                If idamOffs + 4 * 16 > bits.Count Then Continue For
                Dim idam = DecodeDoubled(BitsToBytes(bits.Skip(idamOffs).Take(4 * 16).ToList()))
                If idam.Length <> 4 Then Continue For
                If ComputeCrcCcittFalse(idam) <> 0 Then Continue For
                Dim cyl = BitRev(idam(0))
                Dim secIdRaw = BitRev(idam(1))
                Dim head = ((secIdRaw And &H80) <> 0)
                Dim secId = secIdRaw And &H7F
                If cyl <> _cyl OrElse CInt(If(head, 1, 0)) <> _head OrElse secId > _nsec Then Continue For
                If secId < 0 OrElse secId >= _nsec OrElse HasSec(secId) Then Continue For

                Dim dataSearchOffs = idamOffs + 8 * 16
                Dim dataHits = FindPatternOffsets(bits.Skip(dataSearchOffs).Take(50 * 16).ToList(), DataSyncPattern).ToList()
                If dataHits.Count <> 1 Then Continue For
                Dim secOffs = dataSearchOffs + dataHits(0) + 2 * 16
                If secOffs + 258 * 16 > bits.Count Then Continue For
                Dim sec = DecodeDoubled(BitsToBytes(bits.Skip(secOffs).Take(258 * 16).ToList()))
                If sec.Length <> 258 Then Continue For
                If ComputeCrcCcittFalse(sec) <> 0 Then Continue For

                Dim payload = sec.Take(256).Select(Function(x) BitRev(x)).ToArray()
                payload = SwapWordEndian(payload)
                [Add](secId, payload)
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim encoded As New List(Of Byte)()
            encoded.AddRange(EncodeDoubled(New Byte(99) {}))

            For Each secId In BuildSectorMap(_nsec, _interleave, _cskew, _hskew, _cyl, _head)
                Dim data = If(_sectors(secId), BadSector)

                encoded.AddRange(EncodeDoubled(Enumerable.Repeat(CByte(&HFF), 3).ToArray()))
                encoded.AddRange(SectorSyncBytes)
                Dim idam = New Byte() {BitRev(CByte(_cyl And &HFF)), BitRev(CByte((secId And &H7F) Or ((_head And 1) << 7)))}
                Dim idamCrc = ComputeCrcCcittFalse(idam)
                Dim idamWithCrc = idam.Concat(New Byte() {CByte((idamCrc >> 8) And &HFF), CByte(idamCrc And &HFF)}).ToArray()
                encoded.AddRange(EncodeDoubled(idamWithCrc))
                encoded.AddRange(EncodeDoubled(New Byte(1 + 16 + 4 - 1) {}))

                encoded.AddRange(EncodeDoubled(Enumerable.Repeat(CByte(&HFF), 3).ToArray()))
                encoded.AddRange(DataSyncBytes)
                Dim swapped = SwapWordEndian(data)
                Dim bitRevData = swapped.Select(Function(x) BitRev(x)).ToArray()
                Dim dataCrc = ComputeCrcCcittFalse(bitRevData)
                Dim dataWithCrc = bitRevData.Concat(New Byte() {CByte((dataCrc >> 8) And &HFF), CByte(dataCrc And &HFF)}).ToArray()
                encoded.AddRange(EncodeDoubled(dataWithCrc))
                encoded.AddRange(EncodeDoubled(New Byte(1 + 34 + 4 - 1) {}))
            Next

            Dim tlen = CInt(Math.Floor(TimePerRev / Clock)) And Not 31
            Dim gapBytes = Math.Max(0, ((tlen \ 8) - encoded.Count) \ 2)
            If gapBytes > 0 Then
                encoded.AddRange(EncodeDoubled(New Byte(gapBytes - 1) {}))
            End If

            Dim mmfm = MmfmEncode(encoded.ToArray())
            Dim mt As New MasterTrack(BytesToBits(mmfm), TimePerRev)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("HP MMFM ({0}/{1} sectors)", _nsec - NrMissing(), _nsec)
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.verify_track
        Public Function VerifyTrack(flux As Flux) As Boolean
            Dim readback As New HpMmfm(_cyl, _head, _nsec, _interleave, _cskew, _hskew)
            readback.DecodeFlux(flux)
            Return readback.NrMissing() = 0 AndAlso _sectors.SequenceEqual(readback._sectors)
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::HPMMFM.add
        Private Sub [Add](secId As Integer, data As Byte())
            ErrorHandling.Check(Not HasSec(secId), "sector already exists")
            _sectors(secId) = data
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildSectorMap)
        Private Shared Function BuildSectorMap(nsec As Integer, interleave As Integer, cskew As Integer, hskew As Integer, cyl As Integer, head As Integer) As Integer()
            Dim map = Enumerable.Repeat(-1, nsec).ToArray()
            Dim pos = If(nsec = 0, 0, (cyl * cskew + head * hskew) Mod nsec)
            For i = 0 To nsec - 1
                While map(pos) <> -1
                    pos = (pos + 1) Mod nsec
                End While
                map(pos) = i
                pos = (pos + interleave) Mod nsec
            Next
            Return map
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildBitRevMap)
        Private Shared Function BuildBitRevMap() As Byte()
            Dim list(255) As Byte
            For x = 0 To 255
                Dim v = x
                Dim y = 0
                For i = 0 To 7
                    y = (y << 1) Or (v And 1)
                    v >>= 1
                Next
                list(x) = CByte(y)
            Next
            Return list
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::bitrev
        Private Shared Function BitRev(x As Byte) As Byte
            Return BitRevMap(x)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildMmfmList)
        Private Shared Function BuildMmfmList() As Byte()
            Dim list(511) As Byte
            For x = 0 To 511
                Dim v = x
                For Each i In New Integer() {7, 5, 3, 1}
                    If (v And (&HF << (i - 1))) = 0 Then
                        v = v Or (1 << i)
                    End If
                Next
                list(x) = CByte(v And &HFF)
            Next
            Return list
        End Function

        ' Python map: src/greaseweazle/codec/hp/hp_mmfm.py::mmfm_encode
        Private Shared Function MmfmEncode(data As Byte()) As Byte()
            Dim y = 0
            Dim out As New List(Of Byte)(data.Length)
            For Each x In data
                Dim cur = x
                If (cur And &HAA) = 0 Then
                    cur = MmfmList((y << 8) Or cur)
                End If
                out.Add(CByte(cur))
                y = If((cur And 3) = 0, 0, 1)
            Next
            Return out.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeDoubled)
        Private Shared Function EncodeDoubled(data As Byte()) As Byte()
            Dim out As New List(Of Byte)(data.Length * 2)
            For Each x In data
                Dim y As Integer = 0
                For i = 7 To 0 Step -1
                    y <<= 2
                    y = y Or ((x >> i) And 1)
                Next
                out.Add(CByte((y >> 8) And &HFF))
                out.Add(CByte(y And &HFF))
            Next
            Return out.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeDoubled)
        Private Shared Function DecodeDoubled(data As Byte()) As Byte()
            Dim out As New List(Of Byte)(data.Length \ 2)
            Dim pairs = data.Length \ 2
            For i = 0 To pairs - 1
                Dim word = (CInt(data(i * 2)) << 8) Or data(i * 2 + 1)
                Dim index = word And &H5555
                Dim y = (index + (index >> 1)) And &H3333
                y = (y + (y >> 2)) And &H0F0F
                y = (y + (y >> 4)) And &H00FF
                out.Add(CByte(y))
            Next
            Return out.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeCrcCcittFalse)
        Private Shared Function ComputeCrcCcittFalse(data As Byte()) As Integer
            Dim crc As UShort = &HFFFFUS
            For Each b In data
                crc = CUShort(crc Xor CUShort(CUShort(b) << 8))
                For i = 0 To 7
                    If (crc And &H8000US) <> 0US Then
                        crc = CUShort(((crc << 1) Xor &H1021US) And &HFFFFUS)
                    Else
                        crc = CUShort((crc << 1) And &HFFFFUS)
                    End If
                Next
            Next
            Return CInt(crc)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SwapWordEndian)
        Private Shared Function SwapWordEndian(data As Byte()) As Byte()
            Dim out(data.Length - 1) As Byte
            For i = 0 To data.Length - 1 Step 2
                out(i) = data(i + 1)
                out(i + 1) = data(i)
            Next
            Return out
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindPatternOffsets)
        Private Shared Iterator Function FindPatternOffsets(bits As List(Of Boolean), pattern As Boolean()) As IEnumerable(Of Integer)
            If bits.Count < pattern.Length Then Return
            For i = 0 To bits.Count - pattern.Length
                Dim ok = True
                For j = 0 To pattern.Length - 1
                    If bits(i + j) <> pattern(j) Then
                        ok = False
                        Exit For
                    End If
                Next
                If ok Then Yield i
            Next
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BytesToBits)
        Private Shared Function BytesToBits(data As Byte()) As Boolean()
            Dim bits As New List(Of Boolean)(data.Length * 8)
            For Each b In data
                For i = 7 To 0 Step -1
                    bits.Add(((b >> i) And 1) = 1)
                Next
            Next
            Return bits.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BitsToBytes)
        Private Shared Function BitsToBytes(bits As List(Of Boolean)) As Byte()
            Dim out As New List(Of Byte)(bits.Count \ 8)
            Dim n = bits.Count - (bits.Count Mod 8)
            For i = 0 To n - 1 Step 8
                Dim b As Integer = 0
                For j = 0 To 7
                    b = (b << 1) Or If(bits(i + j), 1, 0)
                Next
                out.Add(CByte(b))
            Next
            Return out.ToArray()
        End Function
    End Class

End Namespace
