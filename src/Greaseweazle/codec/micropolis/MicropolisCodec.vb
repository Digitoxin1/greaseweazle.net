Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::MicropolisDef
    Public Class MicropolisDef
        Implements TrackDef

        Private _secs As Integer?
        Private _imgBytesPerSector As Integer?

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::MicropolisDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::MicropolisDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    _secs = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case "img_bps"
                    Dim parsed = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(parsed = 256 OrElse parsed = 275, String.Format("bad img_bps {0}", parsed))
                    _imgBytesPerSector = parsed
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::MicropolisDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_secs.HasValue, "number of sectors not specified")
            ErrorHandling.Check(_imgBytesPerSector.HasValue, "img_bps not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::MicropolisDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New Micropolis(cyl, head, _secs.GetValueOrDefault(), _imgBytesPerSector.GetValueOrDefault())
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis
    Public Class Micropolis
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Const TimePerRev As Double = 0.2
        Private Shared ReadOnly BadSectorMarker As Byte() = System.Text.Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-")
        Private Shared ReadOnly SyncPattern As Boolean() = BytesToBits(MfmEncode(EncodeBits(New Byte() {0, 0, 0, &HFF})))

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _nsec As Integer
        Private ReadOnly _imgBps As Integer
        Private ReadOnly _clock As Double = 2.0E-6
        Private ReadOnly _preSyncBytes As Integer = 40
        Private ReadOnly _sectorData As List(Of Byte())

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.__init__
        Public Sub New(cyl As Integer, head As Integer, nsec As Integer, imgBps As Integer)
            _cyl = cyl
            _head = head
            _nsec = nsec
            _imgBps = imgBps
            _sectorData = Enumerable.Repeat(Of Byte())(Nothing, nsec).ToList()
        End Sub

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _nsec
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.img_bps
        Public ReadOnly Property ImgBps As Integer
            Get
                Return _imgBps
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _nsec AndAlso _sectorData(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectorData.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim output As New List(Of Byte)(_nsec * _imgBps)
            For sec = 0 To _nsec - 1
                Dim data = _sectorData(sec)
                output.AddRange(If(data, BadSector()))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = _nsec * _imgBps
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For sec = 0 To _nsec - 1
                Dim data(_imgBps - 1) As Byte
                Array.Copy(src, sec * _imgBps, data, 0, _imgBps)
                _sectorData(sec) = data
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim flux = track.Flux()
            If flux.TimePerRev < TimePerRev / 2.0 Then
                flux.IdentifyHardSectors()
            End If
            flux.CueAtIndex()
            Dim raw As New PllTrack(timePerRev:=TimePerRev, clock:=_clock, data:=flux, pll:=pll)

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
                Else
                    hardsectorBits = Enumerable.Range(0, _nsec).Select(Function(i) bits.Count * (i + 1) \ _nsec).ToList()
                End If
                ErrorHandling.Check(hardsectorBits.Count = _nsec,
                                    String.Format("North Star: Unexpected number of sectors: {0}", hardsectorBits.Count))
                hardsectorBits.Insert(0, 0)

                For hsecId = 0 To _nsec - 1
                    Dim s = hardsectorBits(hsecId) + 50
                    Dim e = hardsectorBits(hsecId + 1)
                    If e <= s OrElse s < 0 OrElse e > bits.Count Then Continue For
                    ' bits is List(Of Boolean); GetRange is O(n) Array.Copy.
                    Dim window = bits.GetRange(s, e - s)
                    For Each syncOff In FindPatternOffsets(window, SyncPattern)
                        Dim off = syncOff + 3 * 16
                        Dim segLen = 275 * 16
                        If bits.Count - (s + off) < segLen Then Continue For
                        Dim dat = DecodeBits(BitsToBytes(bits.GetRange(s + off, segLen)))
                        If dat.Length <> 275 Then Continue For
                        Dim cyl = dat(1)
                        Dim secId = dat(2)
                        If cyl <> _cyl OrElse secId > _nsec Then Continue For
                        If secId < 0 OrElse secId >= _nsec OrElse HasSec(secId) Then Continue For
                        Dim csumLen = dat.Length - 7
                        Dim csumBuf(csumLen - 1) As Byte
                        Array.Copy(dat, 1, csumBuf, 0, csumLen)
                        If MicropolisCsum(csumBuf) = dat(dat.Length - 6) Then
                            If _imgBps = 256 Then
                                Dim payload(255) As Byte
                                Array.Copy(dat, 13, payload, 0, 256)
                                [Add](secId, payload)
                            Else
                                [Add](secId, dat)
                            End If
                            Exit For
                        End If
                    Next
                Next
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim encodedTrack As New List(Of Byte)()
            Dim slen = CInt(Math.Floor(TimePerRev / _clock / _nsec / 16))

            For secId = 0 To _nsec - 1
                Dim s As New List(Of Byte)()
                s.AddRange(EncodeBits(New Byte(_preSyncBytes - 1) {}))
                Dim sector = If(_sectorData(secId), BadSector())
                Dim dat As Byte()
                If sector.Length = 256 Then
                    Dim header = New List(Of Byte) From {&HFF, CByte(_cyl And &HFF), CByte(secId And &HFF)}
                    header.AddRange(New Byte(9) {})
                    header.AddRange(sector)
                    header.Add(MicropolisCsum(header.Skip(1).ToArray()))
                    dat = header.ToArray()
                Else
                    dat = sector.ToArray()
                End If
                s.AddRange(EncodeBits(dat))
                Dim fillLen = Math.Max(0, slen - (s.Count \ 2))
                If fillLen > 0 Then
                    s.AddRange(EncodeBits(New Byte(fillLen - 1) {}))
                End If
                encodedTrack.AddRange(s)
            Next

            Dim fluxBytes = MfmEncode(encodedTrack.ToArray())
            Dim hardsector = Enumerable.Repeat(slen * 16, _nsec).ToArray()
            Dim mt As New MasterTrack(BytesToBits(fluxBytes), TimePerRev, hardsectorBits:=hardsector)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("Micropolis ({0}/{1} sectors)", _nsec - NrMissing(), _nsec)
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.verify_track
        Public Function VerifyTrack(flux As Flux) As Boolean
            Dim readback As New Micropolis(_cyl, _head, _nsec, _imgBps)
            readback.DecodeFlux(flux)
            Return readback.NrMissing() = 0 AndAlso _sectorData.SequenceEqual(readback._sectorData)
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.bad_sector
        Private Function BadSector() As Byte()
            If _imgBps = 256 Then
                Return Enumerable.Repeat(BadSectorMarker, 16).SelectMany(Function(x) x).ToArray()
            End If
            Dim output As New List(Of Byte) From {&HFF}
            output.AddRange(New Byte(11) {})
            output.AddRange(Enumerable.Repeat(BadSectorMarker, 16).SelectMany(Function(x) x))
            output.AddRange(New Byte(5) {})
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::micropolis_csum
        Public Shared Function MicropolisCsum(data As Byte()) As Byte
            Dim y As Integer = 0
            For Each x In data
                If y > 255 Then y -= 255
                y += x
            Next
            Return CByte(y And &HFF)
        End Function

        ' Python map: src/greaseweazle/codec/micropolis/micropolis.py::Micropolis.add
        Private Sub [Add](secId As Integer, data As Byte())
            ErrorHandling.Check(Not HasSec(secId), "sector already exists")
            _sectorData(secId) = data
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeBits)
        Private Shared Function EncodeBits(data As Byte()) As Byte()
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeBits)
        Private Shared Function DecodeBits(data As Byte()) As Byte()
            Dim out As New List(Of Byte)(data.Length \ 2)
            Dim pairCount = data.Length \ 2
            For i = 0 To pairCount - 1
                Dim word = (CInt(data(i * 2)) << 8) Or data(i * 2 + 1)
                Dim index = word And &H5555
                Dim y = (index + (index >> 1)) And &H3333
                y = (y + (y >> 2)) And &H0F0F
                y = (y + (y >> 4)) And &H00FF
                out.Add(CByte(y))
            Next
            Return out.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration MfmEncode)
        Private Shared Function MfmEncode(data As Byte()) As Byte()
            Dim y As Integer = 0
            Dim out As New List(Of Byte)(data.Length)
            For Each x In data
                y = (y << 8) Or x
                If (x And &HAA) = 0 Then
                    y = y Or (Not ((y >> 1) Or (y << 1)) And &HAAAA)
                End If
                y = y And &HFF
                out.Add(CByte(y))
            Next
            Return out.ToArray()
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
