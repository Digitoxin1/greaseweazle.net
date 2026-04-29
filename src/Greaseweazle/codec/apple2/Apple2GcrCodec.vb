Imports Greaseweazle.Core
Imports Greaseweazle.Optimised

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCRDef
    Public Class Apple2GcrDef
        Implements TrackDef

        Private ReadOnly _sectors As New List(Of Integer)()
        Private _clock As Double?

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCRDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.1
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCRDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    _sectors.Clear()
                    For Each token In value.Split(","c)
                        _sectors.Add(Integer.Parse(token.Trim(), Globalization.CultureInfo.InvariantCulture))
                    Next
                    Dim reverseMap(_sectors.Count - 1) As Integer
                    For i = 0 To reverseMap.Length - 1
                        reverseMap(i) = -1
                    Next
                    For i = 0 To _sectors.Count - 1
                        Dim s = _sectors(i)
                        ErrorHandling.Check(s < reverseMap.Length, String.Format("sector {0} is out of range", s))
                        ErrorHandling.Check(reverseMap(s) = -1, String.Format("sector {0} is repeated", s))
                        reverseMap(s) = i
                    Next
                Case "clock"
                    _clock = Double.Parse(value, Globalization.CultureInfo.InvariantCulture) * 1.0E-6
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCRDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_sectors.Count > 0, "sector list not specified")
            ErrorHandling.Check(_clock.HasValue, "clock period not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCRDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New Apple2Gcr(cyl, head, _sectors.ToList(), _clock.GetValueOrDefault())
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR
    Public Class Apple2Gcr
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Const SectorLength As Integer = 256
        Private Const EncodedSectorLength As Integer = 342

        Private Shared ReadOnly AddrSyncBytes As Byte() = {&HD5, &HAA, &H96}
        Private Shared ReadOnly DataSyncBytes As Byte() = {&HD5, &HAA, &HAD}
        Private Shared ReadOnly TrailerBytes As Byte() = {&HDE, &HAA, &HEB}
        Private Shared ReadOnly AddrSyncPattern As Boolean() = BytesToBits(AddrSyncBytes)
        Private Shared ReadOnly DataSyncPattern As Boolean() = BytesToBits(DataSyncBytes)
        Private Shared ReadOnly EncodeMap As Byte() = BuildEncodeMap()
        Private Shared ReadOnly DecodeMap As Integer() = BuildDecodeMap()
        Private Shared ReadOnly BadSector As Byte() =
            Enumerable.Repeat(System.Text.Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-"), 16).SelectMany(Function(x) x).ToArray()

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _clock As Double
        Private ReadOnly _sectorOrder As List(Of Integer)
        Private ReadOnly _sectors As List(Of Byte())
        Private _volumeId As Integer?

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.__init__
        Public Sub New(cyl As Integer, head As Integer, sectorOrder As List(Of Integer), clock As Double)
            _cyl = cyl
            _head = head
            _sectorOrder = sectorOrder
            _clock = clock
            _sectors = Enumerable.Repeat(Of Byte())(Nothing, sectorOrder.Count).ToList()
        End Sub

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _sectorOrder.Count
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.set_vol_id
        Public Sub SetVolId(volId As Integer)
            ErrorHandling.Check(Not _volumeId.HasValue, "volume id is already set")
            _volumeId = volId
        End Sub

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _sectors.Count AndAlso _sectors(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectors.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim output As New List(Of Byte)()
            For Each s In _sectorOrder
                Dim sec = _sectors(s)
                If sec Is Nothing Then
                    output.AddRange(BadSector)
                Else
                    output.AddRange(sec)
                End If
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = _sectorOrder.Count * SectorLength
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For i = 0 To _sectorOrder.Count - 1
                Dim sec = _sectorOrder(i)
                Dim dat(SectorLength - 1) As Byte
                Array.Copy(src, i * SectorLength, dat, 0, SectorLength)
                _sectors(sec) = dat
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim raw As New PllTrack(timePerRev:=0.2, clock:=_clock, data:=track, pll:=pll, lowpassThresh:=2.5E-6)
            Dim bits = raw.GetAllData().Item1

            For Each offs In FindPatternOffsets(bits, AddrSyncPattern)
                If NrMissing() = 0 Then Exit For

                Dim hdrOffs = offs + 24
                ' bits is List(Of Boolean); GetRange is O(n) (Array.Copy) vs
                ' Enumerable.Skip(N).Take(M).ToList() which is O(N+M).
                If bits.Count - hdrOffs < 8 * 8 Then Continue For
                Dim hdrBits = bits.GetRange(hdrOffs, 8 * 8)
                Dim hdrBytes = BitsToBytes(hdrBits)
                Dim hdrVals As New List(Of Integer)
                For i = 0 To 3
                    Dim x = CUShort((CUShort(hdrBytes(i * 2)) << 8) Or hdrBytes(i * 2 + 1))
                    hdrVals.Add((x And (x >> 7)) And &HFF)
                Next
                Dim volId = hdrVals(0)
                Dim trkId = hdrVals(1)
                Dim secId = hdrVals(2)
                Dim csum = hdrVals(3)
                If csum <> (volId Xor trkId Xor secId) Then Continue For
                If trkId <> TrackNr() OrElse secId >= _sectors.Count Then Continue For
                If Not _volumeId.HasValue Then
                    _volumeId = volId
                ElseIf _volumeId.Value <> volId Then
                    Continue For
                End If
                If HasSec(secId) Then Continue For

                Dim dataSearchStart = hdrOffs + 8 * 8
                Dim dataSearchLen = Math.Min(100 * 8, bits.Count - dataSearchStart)
                If dataSearchLen <= 0 Then Continue For
                Dim dataWindow = bits.GetRange(dataSearchStart, dataSearchLen)
                Dim dataHits = FindPatternOffsets(dataWindow, DataSyncPattern).ToList()
                If dataHits.Count <> 1 Then Continue For
                Dim encStart = dataSearchStart + dataHits(0) + 3 * 8
                Dim encLen = Math.Min(400 * 8, bits.Count - encStart)
                If encLen <= 0 Then Continue For
                Dim encBits = bits.GetRange(encStart, encLen)
                Dim decoded = DecodeApple2Sector(BitsToBytes(encBits))
                If decoded.Item2 <> 0 Then Continue For
                [Add](secId, decoded.Item1)
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim bits As New List(Of Boolean)()
            bits.AddRange(Enumerable.Repeat(True, 300))

            Dim volId = If(_volumeId.HasValue, _volumeId.Value, 254)
            Dim trkId = TrackNr()
            For secId = 0 To _sectors.Count - 1
                Dim data = If(_sectors(secId), BadSector)
                If data.Length <> SectorLength Then
                    Dim resized(SectorLength - 1) As Byte
                    Array.Copy(data, 0, resized, 0, Math.Min(data.Length, SectorLength))
                    data = resized
                End If

                bits.AddRange(RepeatFf40(18))
                bits.AddRange(Enumerable.Repeat(True, 8))
                bits.AddRange(AddrSyncPattern)
                Dim hdr = New Integer() {volId, trkId, secId, volId Xor trkId Xor secId}
                For Each x In hdr
                    bits.AddRange(ByteToBits(CByte(((x >> 1) Or &HAA) And &HFF)))
                    bits.AddRange(ByteToBits(CByte((x Or &HAA) And &HFF)))
                Next
                bits.AddRange(BytesToBits(TrailerBytes))
                bits.AddRange(RepeatFf40(6))
                bits.AddRange(DataSyncPattern)
                bits.AddRange(BytesToBits(EncodeApple2Sector(data)))
                bits.AddRange(BytesToBits(TrailerBytes))
            Next

            Dim tlen = CInt(Math.Floor(0.2 / _clock))
            If bits.Count < tlen Then
                bits.AddRange(Enumerable.Repeat(True, tlen - bits.Count))
            End If
            Dim mt As New MasterTrack(bits, 0.2)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("Apple2 GCR ({0}/{1} sectors)", _sectors.Count - NrMissing(), _sectors.Count)
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.verify_track
        Public Function VerifyTrack(flux As Greaseweazle.Core.Flux) As Boolean
            Dim readback As New Apple2Gcr(_cyl, _head, _sectorOrder.ToList(), _clock)
            readback.DecodeFlux(flux)
            Return readback.NrMissing() = 0 AndAlso _sectors.SequenceEqual(readback._sectors)
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.tracknr
        Private Function TrackNr() As Integer
            Return _cyl
        End Function

        ' Python map: src/greaseweazle/codec/apple2/apple2_gcr.py::Apple2GCR.add
        Private Sub [Add](secId As Integer, data As Byte())
            ErrorHandling.Check(Not HasSec(secId), "sector already exists")
            _sectors(secId) = data
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration RepeatFf40)
        Private Shared Function RepeatFf40(count As Integer) As IEnumerable(Of Boolean)
            Dim output As New List(Of Boolean)(count * 10)
            For i = 1 To count
                output.AddRange(Enumerable.Repeat(True, 8))
                output.Add(False)
                output.Add(False)
            Next
            Return output
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
            Dim out = Enumerable.Repeat(-1, 256).ToArray()
            For i = 0 To EncodeMap.Length - 1
                out(EncodeMap(i)) = i
            Next
            Return out
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeApple2Sector)
        Private Shared Function DecodeApple2Sector(input As Byte()) As Tuple(Of Byte(), Integer)
            Return Apple2.DecodeSector(input)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeApple2Sector)
        Private Shared Function EncodeApple2Sector(input As Byte()) As Byte()
            Return Apple2.EncodeSector(input)
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function FindPatternOffsets(bits As List(Of Boolean), pattern As Boolean()) As IEnumerable(Of Integer)
            Return BitHelpers.FindPatternOffsets(bits, pattern)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ByteToBits)
        Private Shared Function ByteToBits(value As Byte) As IEnumerable(Of Boolean)
            Dim bits As New List(Of Boolean)(8)
            For i = 7 To 0 Step -1
                bits.Add(((value >> i) And 1) = 1)
            Next
            Return bits
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function BytesToBits(data As Byte()) As Boolean()
            Return BitHelpers.BytesToBits(data)
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function BitsToBytes(bits As List(Of Boolean)) As Byte()
            Return BitHelpers.BitsToBytes(bits)
        End Function
    End Class

End Namespace
