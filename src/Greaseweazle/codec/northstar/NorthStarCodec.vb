Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStarDef
    Public Class NorthStarDef
        Implements TrackDef

        Private _mode As Mode?
        Private _secs As Integer?

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStarDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStarDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    _secs = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case "mode"
                    ' Python: `val == 'fm'` / `val == 'mfm'` -- case sensitive comparison.
                    If String.Equals(value, "fm", StringComparison.Ordinal) Then
                        _mode = Mode.Fm
                    ElseIf String.Equals(value, "mfm", StringComparison.Ordinal) Then
                        _mode = Mode.Mfm
                    Else
                        Throw New FatalException(String.Format("unrecognised mode {0}", value))
                    End If
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStarDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_secs.HasValue, "number of sectors not specified")
            ErrorHandling.Check(_mode.HasValue, "mode not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStarDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New NorthStar(cyl, head, _mode.GetValueOrDefault(), _secs.GetValueOrDefault())
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/northstar/northstar.py::Mode
    Public Enum Mode
        Fm = 0
        Mfm = 1
    End Enum

    Public Module ModeExtensions
        ' Python map: src/greaseweazle/codec/northstar/northstar.py::Mode.__str__
        <Runtime.CompilerServices.Extension()>
        Public Function ToPythonString(value As Mode) As String
            If value = Mode.Mfm Then
                Return "North Star MFM"
            End If
            Return "North Star FM"
        End Function
    End Module

    ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar
    Public Class NorthStar
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                ' Python: default_revs = 1
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Const TimePerRev As Double = 0.2
        Private Shared ReadOnly BadSector As Byte() = System.Text.Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-")
        Private Shared ReadOnly FmSync As Boolean() = BytesToBits(FmEncode(EncodeBits(New Byte() {0, &HFB})))
        Private Shared ReadOnly MfmSync As Boolean() = BytesToBits(MfmEncode(EncodeBits(New Byte() {0, &HFB})))

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _mode As Mode
        Private ReadOnly _nsec As Integer
        Private ReadOnly _clock As Double
        Private ReadOnly _bps As Integer
        Private ReadOnly _syncPattern As Boolean()
        Private ReadOnly _preSyncBytes As Integer
        Private ReadOnly _syncBytes As Integer
        Private ReadOnly _sectorData As List(Of Byte())

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.__init__
        Public Sub New(cyl As Integer, head As Integer, mode As Mode, nsec As Integer)
            _cyl = cyl
            _head = head
            _mode = mode
            _nsec = nsec
            If mode = Mode.Fm Then
                _clock = 4.0E-6
                _bps = 256
                _syncPattern = FmSync
                _preSyncBytes = 17
                _syncBytes = 1
            Else
                _clock = 2.0E-6
                _bps = 512
                _syncPattern = MfmSync
                _preSyncBytes = 34
                _syncBytes = 2
            End If
            _sectorData = Enumerable.Repeat(Of Byte())(Nothing, _nsec).ToList()
        End Sub

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _nsec
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _nsec AndAlso _sectorData(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectorData.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim output As New List(Of Byte)(_nsec * _bps)
            For sec = 0 To _nsec - 1
                Dim data = _sectorData(sec)
                If data Is Nothing Then
                    output.AddRange(Enumerable.Repeat(BadSector, _bps \ 16).SelectMany(Function(x) x))
                Else
                    output.AddRange(data)
                End If
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = _nsec * _bps
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For sec = 0 To _nsec - 1
                Dim data(_bps - 1) As Byte
                Array.Copy(src, sec * _bps, data, 0, _bps)
                _sectorData(sec) = data
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.decode_flux
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

                For secId = 0 To _nsec - 1
                    If HasSec(secId) Then Continue For
                    Dim s = hardsectorBits(secId)
                    Dim e = hardsectorBits(secId + 1)
                    If e <= s OrElse s < 0 OrElse e > bits.Count Then Continue For
                    ' bits is List(Of Boolean); GetRange is O(n) Array.Copy.
                    Dim window = bits.GetRange(s, e - s)
                    Dim firstSync = FindPatternOffset(window, _syncPattern)
                    If firstSync < 0 Then Continue For
                    Dim off = firstSync + (1 + _syncBytes) * 16
                    Dim encLen = (_bps + 1) * 16
                    If bits.Count - (s + off) < encLen Then Continue For
                    Dim encodedBytes = BitsToBytes(bits.GetRange(s + off, encLen))
                    Dim decoded = DecodeBits(encodedBytes)
                    If decoded.Length < _bps + 1 Then Continue For
                    Dim data(_bps - 1) As Byte
                    Array.Copy(decoded, 0, data, 0, _bps)
                    Dim check = decoded(_bps)
                    If Csum(data) = check Then
                        [Add](secId, data)
                    End If
                Next
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim encodedTrack As New List(Of Byte)()
            Dim slen = CInt(Math.Floor(TimePerRev / _clock / _nsec / 16))
            For secId = 0 To _nsec - 1
                Dim s As New List(Of Byte)()
                s.AddRange(EncodeBits(New Byte(_preSyncBytes - 1) {}))
                s.AddRange(EncodeBits(Enumerable.Repeat(CByte(&HFB), _syncBytes).ToArray()))
                Dim data = _sectorData(secId)
                If data Is Nothing Then
                    data = Enumerable.Repeat(BadSector, _bps \ 16).SelectMany(Function(x) x).ToArray()
                End If
                s.AddRange(EncodeBits(data.Concat({Csum(data)}).ToArray()))
                ' Python: encode(bytes(slen - len(s)//2)) -- a negative argument raises in
                ' Python. We surface that as an explicit error to keep parity instead of
                ' silently clamping (which would corrupt the track layout).
                Dim fillLen = slen - (s.Count \ 2)
                ErrorHandling.Check(fillLen >= 0,
                                    String.Format("North Star: sector data overruns slot ({0} bytes)", -fillLen))
                If fillLen > 0 Then
                    s.AddRange(EncodeBits(New Byte(fillLen - 1) {}))
                End If
                encodedTrack.AddRange(s)
            Next

            Dim fluxBytes = If(_mode = Mode.Mfm, MfmEncode(encodedTrack.ToArray()), FmEncode(encodedTrack.ToArray()))
            Dim hardsector = Enumerable.Repeat(slen * 16, _nsec).ToArray()
            Dim mt As New MasterTrack(BytesToBits(fluxBytes), TimePerRev, hardsectorBits:=hardsector)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.summary_string
        Public Overrides Function SummaryString() As String
            Dim modeName = _mode.ToPythonString()
            Return String.Format("{0} ({1}/{2} sectors)", modeName, _nsec - NrMissing(), _nsec)
        End Function

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::csum
        Public Shared Function Csum(data As Byte()) As Byte
            Dim y As Integer = 0
            For Each x In data
                y = y Xor x
                y = ((y << 1) Or (y >> 7)) And &HFF
            Next
            Return CByte(y)
        End Function

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.add
        Private Sub [Add](secId As Integer, data As Byte())
            ErrorHandling.Check(Not HasSec(secId), "sector already exists")
            _sectorData(secId) = data
        End Sub

        ' Python map: src/greaseweazle/codec/northstar/northstar.py::NorthStar.verify_track
        Public Function VerifyTrack(flux As Flux) As Boolean
            Dim readback As New NorthStar(_cyl, _head, _mode, _nsec)
            readback.DecodeFlux(flux)
            Return readback.NrMissing() = 0 AndAlso _sectorData.SequenceEqual(readback._sectorData)
        End Function

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
