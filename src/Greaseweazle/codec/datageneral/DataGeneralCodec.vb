Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneralDef
    Public Class DataGeneralDef
        Implements TrackDef

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneralDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneralDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Throw New FatalException(String.Format("unrecognised track option {0}", key))
        End Sub

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneralDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
        End Sub

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneralDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New DataGeneral(cyl, head)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral
    Public Class DataGeneral
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Const SectorCountConst As Integer = 8
        Private Const Bps As Integer = 512
        Private Const TimePerRev As Double = 60.0 / 360.0
        Private Const Clock As Double = 2.0E-6
        Private Shared ReadOnly BadSector As Byte() = System.Text.Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-")
        Private Shared ReadOnly SyncWord As Byte() = {0, 1}
        Private Shared ReadOnly SyncPattern As Boolean() = BytesToBits(FmEncode(EncodeDoubled(SyncWord)))

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _sectors As List(Of Byte())

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.__init__
        Public Sub New(cyl As Integer, head As Integer)
            _cyl = cyl
            _head = head
            _sectors = Enumerable.Repeat(Of Byte())(Nothing, SectorCountConst).ToList()
        End Sub

        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return SectorCountConst
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < SectorCountConst AndAlso _sectors(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectors.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim output As New List(Of Byte)(SectorCountConst * Bps)
            For sec = 0 To SectorCountConst - 1
                If _sectors(sec) Is Nothing Then
                    output.AddRange(Enumerable.Repeat(BadSector, Bps \ 16).SelectMany(Function(x) x))
                Else
                    output.AddRange(_sectors(sec))
                End If
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = SectorCountConst * Bps
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For sec = 0 To SectorCountConst - 1
                Dim data(Bps - 1) As Byte
                Array.Copy(src, sec * Bps, data, 0, Bps)
                _sectors(sec) = data
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim flux = track.Flux()
            If flux.TimePerRev < TimePerRev / 2.0 Then
                flux.IdentifyHardSectors()
            End If
            flux.CueAtIndex()
            Dim raw As New PllTrack(timePerRev:=TimePerRev, clock:=Clock, data:=flux, pll:=pll)

            For rev = 0 To raw.Revolutions.Count - 1
                If NrMissing() = 0 Then Exit For
                Dim bits = raw.GetRevolution(rev).Item1
                Dim hardsectorBits As List(Of Integer)
                If raw.Revolutions(rev).HardsectorBits IsNot Nothing Then
                    hardsectorBits = New List(Of Integer)()
                    Dim sum = 0
                    For Each h In raw.Revolutions(rev).HardsectorBits
                        sum += h
                        hardsectorBits.Add(sum)
                    Next
                    If hardsectorBits.Count = 32 Then
                        Dim filtered As New List(Of Integer)()
                        For i = 3 To hardsectorBits.Count - 1 Step 4
                            filtered.Add(hardsectorBits(i))
                        Next
                        hardsectorBits = filtered
                    End If
                Else
                    hardsectorBits = Enumerable.Range(0, SectorCountConst).Select(Function(i) bits.Count * (i + 1) \ SectorCountConst).ToList()
                End If
                ErrorHandling.Check(hardsectorBits.Count = SectorCountConst,
                                    String.Format("Data General: Unexpected number of sectors: {0}", hardsectorBits.Count))
                hardsectorBits.Insert(0, 0)

                For hsecId = 0 To SectorCountConst - 1
                    Dim s = hardsectorBits(hsecId) + 352
                    Dim e = hardsectorBits(hsecId + 1)
                    If e <= s OrElse s < 0 OrElse e > bits.Count Then Continue For
                    ' bits is List(Of Boolean); GetRange is O(n) (Array.Copy)
                    ' vs Enumerable.Skip(N).Take(M).ToList() which is O(N+M).
                    Dim firstSync = FindPatternOffset(bits.GetRange(s, e - s), SyncPattern)
                    If firstSync < 0 Then Continue For
                    Dim off = firstSync + 2 * 16

                    If bits.Count - (s + off) < 2 * 16 Then Continue For
                    Dim preamble = DecodeDoubled(BitsToBytes(bits.GetRange(s + off, 2 * 16)))
                    If preamble.Length <> 2 Then Continue For
                    Dim cyl = preamble(0) And &H7F
                    Dim secId = preamble(1) >> 2
                    If cyl <> _cyl OrElse secId > SectorCountConst Then Continue For
                    If secId < 0 OrElse secId >= SectorCountConst Then Continue For
                    If HasSec(secId) Then Exit For

                    Dim dataSearchOff = off + 2 * 16 + 40
                    Dim dataSearchStart = s + dataSearchOff
                    If e <= dataSearchStart Then Continue For
                    Dim dataSyncRel = FindPatternOffset(bits.GetRange(dataSearchStart, e - dataSearchStart), SyncPattern)
                    If dataSyncRel < 0 Then Continue For
                    Dim dataOff = dataSearchOff + dataSyncRel + 2 * 16

                    If bits.Count - (s + dataOff) < 514 * 16 Then Continue For
                    Dim data = DecodeDoubled(BitsToBytes(bits.GetRange(s + dataOff, 514 * 16)))
                    If data.Length <> 514 Then Continue For
                    Dim readCsum = (CInt(data(512)) << 8) Or data(513)
                    Dim payload(511) As Byte
                    Array.Copy(data, 0, payload, 0, 512)
                    If DataGeneralChecksum(payload) = readCsum Then
                        [Add](secId, payload)
                    End If
                Next
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim t As New List(Of Byte)()
            Dim slen = CInt(Math.Floor(TimePerRev / Clock / SectorCountConst / 16))

            For secId = 0 To SectorCountConst - 1
                Dim s As New List(Of Byte)()
                s.AddRange(EncodeDoubled(New Byte(22 * 2 - 1) {}))
                s.AddRange(EncodeDoubled(SyncWord))
                s.AddRange(EncodeDoubled(New Byte() {CByte(_cyl And &H7F), CByte((secId << 2) And &HFF)}))
                s.AddRange(EncodeDoubled(New Byte(3) {}))
                s.AddRange(EncodeDoubled(SyncWord))
                Dim data = If(_sectors(secId), Enumerable.Repeat(BadSector, Bps \ 16).SelectMany(Function(x) x).ToArray())
                Dim csum = DataGeneralChecksum(data)
                s.AddRange(EncodeDoubled(data.Concat(New Byte() {CByte((csum >> 8) And &HFF), CByte(csum And &HFF)}).ToArray()))
                Dim fillLen = Math.Max(0, slen - (s.Count \ 2))
                If fillLen > 0 Then
                    s.AddRange(EncodeDoubled(New Byte(fillLen - 1) {}))
                End If
                t.AddRange(s)
            Next

            Dim fm = FmEncode(t.ToArray())
            Dim hard = Enumerable.Repeat(slen * 16, SectorCountConst).ToArray()
            Dim mt As New MasterTrack(BytesToBits(fm), TimePerRev, hardsectorBits:=hard)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("Data General 2F ({0}/{1} sectors)", SectorCountConst - NrMissing(), SectorCountConst)
        End Function

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.verify_track
        Public Function VerifyTrack(flux As Flux) As Boolean
            Dim readback As New DataGeneral(_cyl, _head)
            readback.DecodeFlux(flux)
            Return readback.NrMissing() = 0 AndAlso _sectors.SequenceEqual(readback._sectors)
        End Function

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::DataGeneral.add
        Private Sub [Add](secId As Integer, data As Byte())
            ErrorHandling.Check(Not HasSec(secId), "sector already exists")
            _sectors(secId) = data
        End Sub

        ' Python map: src/greaseweazle/codec/datageneral/datageneral.py::csum
        Private Shared Function DataGeneralChecksum(data As Byte()) As Integer
            Dim y As Integer = 0
            Dim withClock = data.Concat(New Byte() {0}).ToArray()
            For Each x In withClock
                y = ((y And &HFF) Xor (y >> 8)) Or ((((y And &HFF) Xor x) << 8) And &HFFFF)
            Next
            Return y And &HFFFF
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.DoubleBitCodec.
        Private Shared Function EncodeDoubled(data As Byte()) As Byte()
            Return DoubleBitCodec.Encode(data)
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.DoubleBitCodec.
        Private Shared Function DecodeDoubled(data As Byte()) As Byte()
            Return DoubleBitCodec.Decode(data)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FmEncode)
        Private Shared Function FmEncode(data As Byte()) As Byte()
            Dim out As New List(Of Byte)(data.Length)
            For Each x In data
                Dim y As Integer = x
                If (y And &HAA) = 0 Then
                    y = y Or &HAA
                End If
                out.Add(CByte(y))
            Next
            Return out.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindPatternOffset)
        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function FindPatternOffset(bits As List(Of Boolean), pattern As Boolean()) As Integer
            Return BitHelpers.FindPatternOffset(bits, pattern)
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
