Imports System.Text
Imports Greaseweazle.Core
Imports Greaseweazle.Optimised

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCRDef
    Public Class MacGcrDef
        Implements TrackDef

        Private _secs As Integer?
        Private _clock As Double?
        Private _format As Integer?
        Private _interleave As Integer = 1

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCRDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.3
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCRDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    _secs = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case "clock"
                    _clock = Double.Parse(value, Globalization.CultureInfo.InvariantCulture) * 1.0E-6
                Case "format"
                    ' Python: int(val, base=0) -- accepts 0x/0o/0b prefixes plus decimal.
                    Dim n = ParseRadixZeroInt(value)
                    ErrorHandling.Check(n >= 0 AndAlso n <= 255, "format out of range")
                    _format = n
                Case "interleave"
                    _interleave = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCRDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_secs.HasValue, "number of sectors not specified")
            ErrorHandling.Check(_clock.HasValue, "clock period not specified")
            ErrorHandling.Check(_format.HasValue, "format byte not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCRDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New MacGcr(cyl, head, _secs.GetValueOrDefault(), _clock.GetValueOrDefault(), _format.GetValueOrDefault(), _interleave)
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::(no direct 1:1 symbol; VB helper for Python `int(val, base=0)` semantics)
        Private Shared Function ParseRadixZeroInt(value As String) As Integer
            If String.IsNullOrEmpty(value) Then
                Throw New FormatException("empty integer value")
            End If
            Dim s = value.Trim()
            Dim sign As Integer = 1
            If s.StartsWith("+", StringComparison.Ordinal) Then
                s = s.Substring(1)
            ElseIf s.StartsWith("-", StringComparison.Ordinal) Then
                sign = -1
                s = s.Substring(1)
            End If
            Dim mag As Integer
            If s.Length >= 2 AndAlso s(0) = "0"c AndAlso (s(1) = "x"c OrElse s(1) = "X"c) Then
                mag = Convert.ToInt32(s.Substring(2), 16)
            ElseIf s.Length >= 2 AndAlso s(0) = "0"c AndAlso (s(1) = "o"c OrElse s(1) = "O"c) Then
                mag = Convert.ToInt32(s.Substring(2), 8)
            ElseIf s.Length >= 2 AndAlso s(0) = "0"c AndAlso (s(1) = "b"c OrElse s(1) = "B"c) Then
                mag = Convert.ToInt32(s.Substring(2), 2)
            Else
                mag = Integer.Parse(s, Globalization.CultureInfo.InvariantCulture)
            End If
            Return sign * mag
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR
    Public Class MacGcr
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Const SectorLen As Integer = 524
        Private Const EncSectorLen As Integer = 703

        Private Shared ReadOnly SelfSyncBytes As Byte() = {&HFF, &H3F, &HCF, &HF3, &HFC, &HFF}
        Private Shared ReadOnly SectorSyncBytes As Byte() = {&HD5, &HAA, &H96}
        Private Shared ReadOnly DataSyncBytes As Byte() = {&HD5, &HAA, &HAD}
        Private Shared ReadOnly SectorSyncPattern As Boolean() = BytesToBits(SectorSyncBytes)
        Private Shared ReadOnly DataSyncPattern As Boolean() = BytesToBits(DataSyncBytes)
        Private Shared ReadOnly EncodeMap As Byte() = BuildEncodeMap()
        Private Shared ReadOnly DecodeMap As Integer() = BuildDecodeMap()
        Private Shared ReadOnly BadSector As Byte() =
            Enumerable.Repeat(Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-"), 32).SelectMany(Function(x) x).ToArray()

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _trackNr As Integer
        Private ReadOnly _secs As Integer
        Private ReadOnly _clock As Double
        Private ReadOnly _format As Integer
        Private ReadOnly _interleave As Integer
        Private ReadOnly _sectors As List(Of Byte())
        Private ReadOnly _secMap As List(Of Integer)

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.__init__
        Public Sub New(cyl As Integer, head As Integer, secs As Integer, clock As Double, formatByte As Integer, interleave As Integer)
            _cyl = cyl
            _head = head
            _trackNr = cyl * 2 + head
            _secs = secs
            _clock = clock
            _format = formatByte
            _interleave = interleave
            _sectors = Enumerable.Repeat(Of Byte())(Nothing, secs).ToList()
            _secMap = BuildSecMap(secs, interleave)
        End Sub

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _secs
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _secs AndAlso _sectors(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectors.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim output As New List(Of Byte)()
            For Each sec In _sectors
                If sec Is Nothing Then
                    output.AddRange(BadSector)
                Else
                    output.AddRange(sec.Skip(12))
                End If
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = _secs * 512
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For sec = 0 To _secs - 1
                Dim payload(523) As Byte
                Array.Copy(src, sec * 512, payload, 12, 512)
                _sectors(sec) = payload
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim raw As New PllTrack(timePerRev:=0.2, clock:=_clock, data:=track, pll:=pll)
            Dim bits = raw.GetAllData().Item1

            For Each offs In FindPatternOffsets(bits, SectorSyncPattern)
                If NrMissing() = 0 Then Exit For

                Dim headerOffs = offs + 24
                ' bits is List(Of Boolean); GetRange is O(n) (Array.Copy) vs
                ' Enumerable.Skip(N).Take(M).ToList() which is O(N+M).
                If bits.Count - headerOffs < 5 * 8 Then Continue For
                Dim headerGcrBits = bits.GetRange(headerOffs, 5 * 8)
                Dim headerDecoded = MacGcrDecode(BitsToBytes(headerGcrBits))
                If headerDecoded.Length <> 5 Then Continue For
                Dim sum = headerDecoded.Aggregate(0, Function(a, x) a Xor x)
                If sum <> 0 Then Continue For

                Dim cyl = CInt(headerDecoded(0))
                Dim secId = CInt(headerDecoded(1))
                Dim side = CInt(headerDecoded(2))
                cyl = cyl Or ((side And 1) << 6)
                side >>= 5
                If cyl <> _cyl OrElse side <> _head OrElse secId >= _secs OrElse HasSec(secId) Then
                    Continue For
                End If

                Dim searchStart = headerOffs + 40
                Dim searchLen = Math.Min(100 * 8, bits.Count - searchStart)
                If searchLen <= 0 Then Continue For
                Dim searchBits = bits.GetRange(searchStart, searchLen)
                Dim dataSyncHits = FindPatternOffsets(searchBits, DataSyncPattern).ToList()
                If dataSyncHits.Count <> 1 Then Continue For

                Dim dataOffs = searchStart + dataSyncHits(0) + 32
                If bits.Count - dataOffs < EncSectorLen * 8 Then Continue For
                Dim secBits = bits.GetRange(dataOffs, EncSectorLen * 8)
                Dim secDecodedGcr = MacGcrDecode(BitsToBytes(secBits))
                If secDecodedGcr.Length <> EncSectorLen Then Continue For
                Dim decoded = DecodeMacSector(secDecodedGcr)
                If decoded.Item2 <> 0 Then Continue For
                [Add](secId, decoded.Item1)
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim t As New List(Of Byte)()
            t.AddRange(Enumerable.Repeat(CByte(&H96), 64))
            For i = 1 To 20
                t.AddRange(SelfSyncBytes)
            Next

            For nr = 0 To _secs - 1
                Dim secId = _secMap(nr)
                Dim data = If(_sectors(secId), New Byte(11) {}.Concat(BadSector).ToArray())
                If data.Length <> SectorLen Then
                    Dim resized(SectorLen - 1) As Byte
                    Array.Copy(data, 0, resized, 0, Math.Min(data.Length, SectorLen))
                    data = resized
                End If

                Dim cyl = _cyl
                Dim side = _head
                side = (side << 5) Or (cyl >> 6)
                cyl = cyl And &H3F
                Dim hdr = New Byte() {CByte(cyl And &HFF), CByte(secId And &HFF), CByte(side And &HFF), CByte(_format And &HFF)}
                Dim hdrSum = hdr.Aggregate(0, Function(a, x) a Xor x)

                For i = 1 To 6
                    t.AddRange(SelfSyncBytes)
                Next
                t.AddRange(SectorSyncBytes)
                t.AddRange(MacGcrEncode(hdr.Concat(New Byte() {CByte(hdrSum And &HFF)}).ToArray()))
                t.AddRange(New Byte() {&HDE, &HAA, &HFF, &HFF})
                t.AddRange(SelfSyncBytes)
                t.AddRange(DataSyncBytes)
                t.AddRange(MacGcrEncode(New Byte() {CByte(secId And &HFF)}))
                t.AddRange(MacGcrEncode(EncodeMacSector(data)))
                t.AddRange(New Byte() {&HDE, &HAA, &HFF})
            Next

            t.AddRange(New Byte() {&HFF, &HFF, &HFF, &HFF})
            Dim tlen = CInt(Math.Floor(0.2 / _clock)) And Not 31
            Dim targetBytes = tlen \ 8
            If t.Count < targetBytes Then
                t.AddRange(Enumerable.Repeat(CByte(&H96), targetBytes - t.Count))
            End If

            Dim mt As New MasterTrack(BytesToBits(t.ToArray()), 0.2)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("Macintosh GCR ({0}/{1} sectors)", _secs - NrMissing(), _secs)
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.verify_track
        Public Function VerifyTrack(flux As Greaseweazle.Core.Flux) As Boolean
            Dim readback As New MacGcr(_cyl, _head, _secs, _clock, _format, _interleave)
            readback.DecodeFlux(flux)
            Return readback.NrMissing() = 0 AndAlso _sectors.SequenceEqual(readback._sectors)
        End Function

        ' Python map: src/greaseweazle/codec/macintosh/mac_gcr.py::MacGCR.add
        Private Sub [Add](secId As Integer, data As Byte())
            ErrorHandling.Check(Not HasSec(secId), "sector already exists")
            _sectors(secId) = data
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildSecMap)
        Private Shared Function BuildSecMap(nsec As Integer, interleave As Integer) As List(Of Integer)
            Dim secMap(nsec - 1) As Integer
            For i = 0 To secMap.Length - 1
                secMap(i) = -1
            Next
            Dim pos = 0
            For i = 0 To nsec - 1
                While secMap(pos) <> -1
                    pos = (pos + 1) Mod nsec
                End While
                secMap(pos) = i
                pos = (pos + interleave) Mod nsec
            Next
            Return secMap.ToList()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildEncodeMap)
        Private Shared Function BuildEncodeMap() As Byte()
            Return New Byte() {
                &H96, &H97, &H9A, &H9B, &H9D, &H9E, &H9F, &HA6,
                &HA7, &HAB, &HAC, &HAD, &HAE, &HAF, &HB2, &HB3,
                &HB4, &HB5, &HB6, &HB7, &HB9, &HBA, &HBB, &HBC,
                &HBD, &HBE, &HBF, &HCB, &HCD, &HCE, &HCF, &HD3,
                &HD6, &HD7, &HD9, &HDA, &HDB, &HDC, &HDD, &HDE,
                &HDF, &HE5, &HE6, &HE7, &HE9, &HEA, &HEB, &HEC,
                &HED, &HEE, &HEF, &HF2, &HF3, &HF4, &HF5, &HF6,
                &HF7, &HF9, &HFA, &HFB, &HFC, &HFD, &HFE, &HFF
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildDecodeMap)
        Private Shared Function BuildDecodeMap() As Integer()
            Dim map = Enumerable.Repeat(-1, 256).ToArray()
            For i = 0 To EncodeMap.Length - 1
                map(EncodeMap(i)) = i
            Next
            Return map
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration MacGcrDecode)
        Private Shared Function MacGcrDecode(input As Byte()) As Byte()
            Return AppleGcr62.DecodeBytes(input)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration MacGcrEncode)
        Private Shared Function MacGcrEncode(input As Byte()) As Byte()
            Return AppleGcr62.EncodeBytes(input)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeMacSector)
        Private Shared Function DecodeMacSector(input As Byte()) As Tuple(Of Byte(), Integer)
            Return Mac.DecodeSector(input)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeMacSector)
        Private Shared Function EncodeMacSector(input As Byte()) As Byte()
            Return Mac.EncodeSector(input)
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
            For i = 0 To bits.Count - 1 Step 8
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
