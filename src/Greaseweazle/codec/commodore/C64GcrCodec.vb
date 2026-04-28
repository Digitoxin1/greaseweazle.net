Imports System.Text
Imports Greaseweazle.Core
Imports Greaseweazle.Optimised

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCRDef
    Public Class C64GcrDef
        Implements TrackDef

        Private _secs As Integer?
        Private _clock As Double?

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCRDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.1
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCRDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    _secs = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case "clock"
                    _clock = Double.Parse(value, Globalization.CultureInfo.InvariantCulture) * 1.0E-6
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCRDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_secs.HasValue, "number of sectors not specified")
            ErrorHandling.Check(_clock.HasValue, "clock period not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCRDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New C64Gcr(cyl, head, _secs.GetValueOrDefault(), _clock.GetValueOrDefault())
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR
    Public Class C64Gcr
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Shared ReadOnly SectorSyncPattern As Boolean() = BitsFrom01("11111111110101001010")
        Private Shared ReadOnly DataSyncPattern As Boolean() = BitsFrom01("11111111110101010111")
        Private Shared ReadOnly EncodeNibble As Byte() = {&HA, &HB, &H12, &H13, &HE, &HF, &H16, &H17, &H9, &H19, &H1A, &H1B, &HD, &H1D, &H1E, &H15}
        Private Shared ReadOnly DecodeNibble As Integer() = BuildDecodeNibble()
        Private Shared ReadOnly BadSector As Byte() = Enumerable.Repeat(Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-"), 16).SelectMany(Function(x) x).ToArray()

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _secs As Integer
        Private ReadOnly _clock As Double
        Private ReadOnly _sectors As List(Of Byte())
        Private _diskId As UShort?

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.__init__
        Public Sub New(cyl As Integer, head As Integer, secs As Integer, clock As Double)
            _cyl = cyl
            _head = head
            _secs = secs
            _clock = clock
            _sectors = Enumerable.Repeat(Of Byte())(Nothing, secs).ToList()
        End Sub

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _secs
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _secs AndAlso _sectors(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectors.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim output As New List(Of Byte)()
            For Each sec In _sectors
                If sec Is Nothing Then
                    output.AddRange(BadSector)
                Else
                    output.AddRange(sec)
                End If
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = _secs * 256
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For i = 0 To _secs - 1
                Dim sec(255) As Byte
                Array.Copy(src, i * 256, sec, 0, 256)
                _sectors(i) = sec
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim raw As New PllTrack(timePerRev:=0.2, clock:=_clock, data:=track, pll:=pll, lowpassThresh:=2.5E-6)
            Dim bits = raw.GetAllData().Item1

            For Each offs In FindPatternOffsets(bits, SectorSyncPattern)
                If NrMissing() = 0 Then Exit For

                Dim hdrOffs = offs + 10
                Dim hdrBits = bits.Skip(hdrOffs).Take(10 * 8).ToList()
                If hdrBits.Count <> 80 Then Continue For
                Dim hdr = DecodeC64Gcr(BitsToBytes(hdrBits), 8)
                If hdr.Length <> 8 Then Continue For
                Dim csum = 0
                For i = 1 To 5
                    csum = csum Xor hdr(i)
                Next
                If csum <> 0 Then Continue For
                Dim secId = CInt(hdr(2))
                Dim cyl = CInt(hdr(3))
                Dim diskId = CUShort((CUInt(hdr(4)) << 8) Or hdr(5))
                If cyl <> TrackNr() OrElse secId >= _secs Then Continue For
                If Not _diskId.HasValue Then
                    _diskId = diskId
                ElseIf _diskId.Value <> diskId Then
                    Continue For
                End If
                If HasSec(secId) Then Continue For

                Dim dataSearchOffs = hdrOffs + 8 * 8
                Dim dataSearchBits = bits.Skip(dataSearchOffs).Take(100 * 8).ToList()
                Dim dataHits = FindPatternOffsets(dataSearchBits, DataSyncPattern).ToList()
                If dataHits.Count <> 1 Then Continue For
                Dim dataOffs = dataSearchOffs + dataHits(0) + 10
                Dim secBits = bits.Skip(dataOffs).Take(260 * 10).ToList()
                If secBits.Count <> 2600 Then Continue For
                Dim secDecoded = DecodeC64Gcr(BitsToBytes(secBits), 260)
                If secDecoded.Length <> 260 Then Continue For
                Dim dsum = 0
                For i = 1 To 257
                    dsum = dsum Xor secDecoded(i)
                Next
                If dsum <> 0 Then Continue For
                [Add](secId, secDecoded.Skip(1).Take(256).ToArray())
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim t As New List(Of Byte)()
            t.AddRange(Enumerable.Repeat(CByte(&H55), 10))

            For secId = 0 To _secs - 1
                Dim data = If(_sectors(secId), BadSector)
                If data.Length < 256 Then
                    data = data.Concat(Enumerable.Repeat(CByte(0), 256 - data.Length)).ToArray()
                End If
                data = data.Take(256).ToArray()

                Dim diskId = If(_diskId.HasValue, _diskId.Value, CUShort(0))
                Dim hdr = New Byte() {
                    CByte(secId And &HFF),
                    CByte(TrackNr() And &HFF),
                    CByte((diskId >> 8) And &HFF),
                    CByte(diskId And &HFF)
                }
                Dim hsum = hdr.Aggregate(0, Function(a, x) a Xor x)
                Dim hdrBlock = New Byte() {&H8, CByte(hsum)}.Concat(hdr).Concat(New Byte() {&HF, &HF}).ToArray()

                t.AddRange(Enumerable.Repeat(CByte(&HFF), 5))
                t.AddRange(EncodeC64Gcr(hdrBlock))
                t.AddRange(Enumerable.Repeat(CByte(&H55), 9))

                Dim dsum = data.Aggregate(0, Function(a, x) a Xor x)
                Dim dataBlock = New Byte() {&H7}.Concat(data).Concat(New Byte() {CByte(dsum), &HF, &HF}).ToArray()
                t.AddRange(Enumerable.Repeat(CByte(&HFF), 5))
                t.AddRange(EncodeC64Gcr(dataBlock))
                t.AddRange(Enumerable.Repeat(CByte(&H55), 9))
            Next

            Dim tlen = CInt(Math.Floor(0.2 / _clock)) And Not 31
            Dim targetBytes = tlen \ 8
            If t.Count < targetBytes Then
                t.AddRange(Enumerable.Repeat(CByte(&H55), targetBytes - t.Count))
            End If
            Dim mt As New MasterTrack(BytesToBits(t.ToArray()), 0.2)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("Commodore GCR ({0}/{1} sectors)", _secs - NrMissing(), _secs)
        End Function

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.verify_track
        Public Function VerifyTrack(flux As Greaseweazle.Core.Flux) As Boolean
            Dim readback As New C64Gcr(_cyl, _head, _secs, _clock)
            readback.DecodeFlux(flux)
            Return readback.NrMissing() = 0 AndAlso _sectors.SequenceEqual(readback._sectors)
        End Function

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.set_disk_id
        Public Sub SetDiskId(diskId As UShort)
            ' Python: assert self.disk_id is None
            ErrorHandling.Check(Not _diskId.HasValue, "C64GCR: disk id already set")
            _diskId = diskId
        End Sub

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.add
        Private Sub [Add](secId As Integer, data As Byte())
            ErrorHandling.Check(Not HasSec(secId), "sector already exists")
            _sectors(secId) = data
        End Sub

        ' Python map: src/greaseweazle/codec/commodore/c64_gcr.py::C64GCR.tracknr
        Private Function TrackNr() As Integer
            Return _head * 35 + _cyl + 1
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildDecodeNibble)
        Private Shared Function BuildDecodeNibble() As Integer()
            Dim out = Enumerable.Repeat(-1, 32).ToArray()
            For i = 0 To EncodeNibble.Length - 1
                out(EncodeNibble(i)) = i
            Next
            Return out
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeC64Gcr)
        Private Shared Function DecodeC64Gcr(input As Byte(), outLen As Integer) As Byte()
            Return C64.DecodeGcr(input, outLen)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeC64Gcr)
        Private Shared Function EncodeC64Gcr(input As Byte()) As Byte()
            Return C64.EncodeGcr(input)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BitsFrom01)
        Private Shared Function BitsFrom01(spec As String) As Boolean()
            Dim bits As New List(Of Boolean)(spec.Length)
            For Each ch In spec
                bits.Add(ch = "1"c)
            Next
            Return bits.ToArray()
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
