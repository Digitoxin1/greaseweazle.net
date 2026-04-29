Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_ScanDef
    Public Class IbmTrackScanDef
        Implements TrackDef

        Private _rate As Integer?
        Private _rpm As Integer?

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_ScanDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 2.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_ScanDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "rate", "rpm"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 1 AndAlso n <= 2000, String.Format("{0} out of range", key))
                    If key = "rate" Then
                        _rate = n
                    Else
                        _rpm = n
                    End If
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_ScanDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_ScanDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New IbmTrackScan(cyl, head, _rate, _rpm)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan
    Public Class IbmTrackScan
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.verify_track (delegated to scan-built fixed track)
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Dim built = BuildScanTrack()
            If built Is Nothing Then
                Return False
            End If
            built.SetImgTrack(GetImgTrack())
            Return built.VerifyTrack(flux)
        End Function

        Private Shared ReadOnly ProbeRates As Integer() = {125, 250, 500}
        Private Shared ReadOnly ProbeRpms As Integer() = {300, 360}
        Private Shared BestGuess As Tuple(Of Double, Double, String)

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _rate As Integer?
        Private ReadOnly _rpm As Integer?
        Private _sectors As New SortedDictionary(Of Integer, Byte())()
        Private _sectorNs As New SortedDictionary(Of Integer, Integer)()
        Private _summary As String = "IBM Empty"
        Private _bestFormatName As String = "ibm.mfm"
        Private _bestTimePerRev As Double = 0.2
        Private _bestClock As Double = 2.0E-6
        ' Total IDAMs seen by best scan pass (Python: t.nsec). May differ from
        ' _sectors.Count when some sectors have bad data CRC (Python: nr_missing > 0).
        Private _totalIdamSlots As Integer = 0

        Private Shared ReadOnly MfmSyncPattern As Boolean() = BitsFrom01("010001001000100101000100100010010100010010001001")
        Private Shared ReadOnly FmSyncPrefixPattern As Boolean() = BitsFrom01("10101010101010101111010101")
        Private Shared ReadOnly FmIdamPattern As Boolean() = SyncWordBits(&HFE, &HC7)
        Private Shared ReadOnly FmDamPattern As Boolean() = SyncWordBits(&HFB, &HC7)
        Private Shared ReadOnly FmDdamPattern As Boolean() = SyncWordBits(&HF8, &HC7)

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.__init__
        Public Sub New(cyl As Integer, head As Integer, rate As Integer?, rpm As Integer?)
            _cyl = cyl
            _head = head
            _rate = rate
            _rpm = rpm
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                ' Python: IBMTrack.nsec = len(self.sectors), where self.sectors
                ' contains every IDAM-bearing slot, even those with bad data CRC.
                Return Math.Max(_totalIdamSlots, _sectors.Count)
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return _sectors.ContainsKey(sectorId)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.nr_missing
        Public Overrides Function NrMissing() As Integer
            ' Python: count of IDAM slots whose data CRC is non-zero.
            ' VB tracks decoded sectors in _sectors and total IDAMs in _totalIdamSlots.
            Return Math.Max(0, _totalIdamSlots - _sectors.Count)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim bytes As New List(Of Byte)()
            For Each kv In _sectors
                bytes.AddRange(kv.Value)
            Next
            Return bytes.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            If _sectors.Count = 0 Then
                Throw New FatalException("ibm.scan: Cannot handle IMG input data")
            End If
            Dim src = If(trackData, Array.Empty(Of Byte)())
            Dim pos = 0
            Dim total = 0
            For Each id In _sectors.Keys.ToList()
                Dim size = _sectors(id).Length
                total += size
                Dim sector(size - 1) As Byte
                If pos < src.Length Then
                    Dim avail = Math.Min(size, src.Length - pos)
                    Array.Copy(src, pos, sector, 0, avail)
                End If
                _sectors(id) = sector
                pos += size
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim flux = track.Flux()
            flux.CueAtIndex()

            ' Python: "Add more data to an existing track instance" -- if a previous
            ' DecodeFlux pass already produced sectors, re-decode at the locked-in
            ' best-guess settings and only fill slots that are still missing.
            If _sectors.Count > 0 Then
                Dim raw As New PllTrack(clock:=_bestClock, data:=flux, timePerRev:=_bestTimePerRev, pll:=pll)
                Dim bits = raw.GetAllData().Item1
                Dim parsed As ScanResult
                If String.Equals(_bestFormatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase) Then
                    parsed = ParseMfm(bits)
                Else
                    parsed = ParseFm(bits)
                End If
                MergeScan(parsed)
                Return
            End If

            Dim best As ScanResult = Nothing
            Dim bestTimePerRev As Double = _bestTimePerRev
            Dim bestClock As Double = _bestClock
            Dim bestFormat As String = _bestFormatName

            ' Best-guess mode: try the previous winning settings first. Mirror
            ' Python's perfect-match short-circuit: "t.nsec != 0 AND nr_missing == 0".
            If BestGuess IsNot Nothing Then
                Dim mode = BestGuess.Item3
                Dim raw As New PllTrack(clock:=BestGuess.Item2, data:=flux, timePerRev:=BestGuess.Item1, pll:=pll)
                Dim guess = If(mode = "mfm", ParseMfm(raw.GetAllData().Item1), ParseFm(raw.GetAllData().Item1))
                If guess.Sectors.Count > 0 AndAlso guess.Sectors.Count = guess.TotalIdamSlots Then
                    _sectors = guess.Sectors
                    _sectorNs = guess.SectorNs
                    _totalIdamSlots = guess.TotalIdamSlots
                    _summary = guess.Summary
                    _bestTimePerRev = BestGuess.Item1
                    _bestClock = BestGuess.Item2
                    _bestFormatName = If(mode = "mfm", "ibm.mfm", "ibm.fm")
                    Return
                End If
            End If

            Dim rates = If(_rate.HasValue, New Integer() {_rate.Value}, ProbeRates)
            Dim rpms = If(_rpm.HasValue, New Integer() {_rpm.Value}, ProbeRpms)
            For Each rpm In rpms
                Dim timePerRev = 60.0 / rpm
                For Each probeRate As Integer In rates
                    Dim clock = 0.0005 / probeRate
                    Dim raw As New PllTrack(clock:=clock, data:=flux, timePerRev:=timePerRev, pll:=pll)
                    Dim bits = raw.GetAllData().Item1

                    Dim mfm = ParseMfm(bits)
                    If best Is Nothing OrElse mfm.Score > best.Score Then
                        best = mfm
                        bestTimePerRev = timePerRev
                        bestClock = clock
                        bestFormat = "ibm.mfm"
                    End If

                    Dim fm = ParseFm(bits)
                    If best Is Nothing OrElse fm.Score > best.Score Then
                        best = fm
                        bestTimePerRev = timePerRev
                        bestClock = clock
                        bestFormat = "ibm.fm"
                    End If
                Next
            Next

            If best IsNot Nothing AndAlso best.Sectors.Count > 0 Then
                _sectors = best.Sectors
                _sectorNs = best.SectorNs
                _totalIdamSlots = best.TotalIdamSlots
                _summary = best.Summary
                _bestTimePerRev = bestTimePerRev
                _bestClock = bestClock
                _bestFormatName = bestFormat
                ' Python: BEST_GUESS is updated only once, after probing finishes,
                ' using the winning track's settings. Mirror that here.
                BestGuess = Tuple.Create(bestTimePerRev, bestClock,
                                         If(String.Equals(bestFormat, "ibm.mfm", StringComparison.OrdinalIgnoreCase), "mfm", "fm"))
            End If
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.decode_flux (re-call merge)
        Private Sub MergeScan(parsed As ScanResult)
            ' Only fill slots that don't already have valid data; never overwrite
            ' a previously-decoded sector. Mirrors Python's per-slot "is None" guard.
            For Each kv In parsed.Sectors
                If Not _sectors.ContainsKey(kv.Key) Then
                    _sectors(kv.Key) = kv.Value
                End If
            Next
            For Each kv In parsed.SectorNs
                If Not _sectorNs.ContainsKey(kv.Key) Then
                    _sectorNs(kv.Key) = kv.Value
                End If
            Next
            _totalIdamSlots = Math.Max(_totalIdamSlots, parsed.TotalIdamSlots)
            _summary = String.Format("{0} ({1}/{2} sectors)",
                                     If(String.Equals(_bestFormatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase), "IBM MFM", "IBM FM"),
                                     _sectors.Count, _totalIdamSlots)
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim scanTrack = BuildScanTrack()
            Dim mt As MasterTrack
            If scanTrack Is Nothing Then
                mt = New IbmTrackEmpty().MasterTrack()
            Else
                scanTrack.SetImgTrack(GetImgTrack())
                mt = scanTrack.MasterTrack()
            End If
            ' Python: IBMTrack_Scan.master_track delegates to inner IBMTrack which sets
            ' track.verify = self. Make sure verify still points at the *Scan* codec so
            ' the verifier holds the same sector map we'll re-decode against.
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.summary_string
        Public Overrides Function SummaryString() As String
            Return _summary
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::(no direct 1:1 symbol; VB helper container for scan pass results)
        Private Class ScanResult
            Public Property Sectors As SortedDictionary(Of Integer, Byte())
            Public Property SectorNs As SortedDictionary(Of Integer, Integer)
            ' Total number of IDAM-bearing slots discovered (Python: len(track.sectors)).
            Public Property TotalIdamSlots As Integer
            Public Property Summary As String
            Public Property FormatName As String
            Public Property TimePerRev As Double
            Public Property Clock As Double

            Public ReadOnly Property Score As Integer
                Get
                    ' Python's choose-track scoring: t.nsec - t.nr_missing(),
                    ' i.e. the number of decoded (good-CRC) sectors.
                    Return Sectors.Count
                End Get
            End Property
        End Class

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseMfm)
        Private Function ParseMfm(bits As List(Of Boolean)) As ScanResult
            Dim sectors As New SortedDictionary(Of Integer, Byte())()
            Dim sectorNs As New SortedDictionary(Of Integer, Integer)()
            Dim seenIds As New HashSet(Of Integer)()
            Dim pending As ParsedIdam = Nothing
            For Each offs In FindPatternOffsets(bits, MfmSyncPattern)
                If bits.Count < offs + 64 Then Continue For
                Dim mark = DecodeWord(bits, offs + 48)
                If mark = &HFE Then
                    If bits.Count < offs + 160 Then Continue For
                    Dim idamBytes = DecodeWords(bits, offs, 10)
                    If ComputeCrcCcittFalse(idamBytes) <> 0 Then Continue For
                    pending = New ParsedIdam With {
                        .EndOffset = offs + 160,
                        .R = idamBytes(6),
                        .N = idamBytes(7)
                    }
                    seenIds.Add(pending.R)
                    Continue For
                End If
                If mark <> &HFB AndAlso mark <> &HF8 Then Continue For
                If pending Is Nothing OrElse offs - pending.EndOffset > 1000 Then
                    pending = Nothing
                    Continue For
                End If
                Dim size = 128 << pending.N
                Dim byteCount = 4 + size + 2
                Dim e = offs + byteCount * 16
                If bits.Count < e Then Continue For
                Dim payload = DecodeWords(bits, offs, byteCount)
                If ComputeCrcCcittFalse(payload) <> 0 Then
                    pending = Nothing
                    Continue For
                End If
                Dim mfmData(size - 1) As Byte
                Array.Copy(payload, 4, mfmData, 0, size)
                sectors(pending.R) = mfmData
                sectorNs(pending.R) = pending.N
                pending = Nothing
            Next
            Dim totalSlots = Math.Max(seenIds.Count, sectors.Count)
            Return New ScanResult With {
                .Sectors = sectors,
                .SectorNs = sectorNs,
                .TotalIdamSlots = totalSlots,
                .Summary = String.Format("IBM MFM ({0}/{1} sectors)", sectors.Count, totalSlots),
                .FormatName = "ibm.mfm"
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseFm)
        Private Function ParseFm(bits As List(Of Boolean)) As ScanResult
            Dim sectors As New SortedDictionary(Of Integer, Byte())()
            Dim sectorNs As New SortedDictionary(Of Integer, Integer)()
            Dim seenIds As New HashSet(Of Integer)()
            Dim pending As ParsedIdam = Nothing
            Dim offsets = FindPatternOffsets(bits, FmIdamPattern).
                Concat(FindPatternOffsets(bits, FmDamPattern)).
                Concat(FindPatternOffsets(bits, FmDdamPattern)).
                Distinct().
                OrderBy(Function(x) x)
            For Each offs In offsets
                Dim mark = DecodeWord(bits, offs)
                If mark = &HFE Then
                    Dim endIdam = offs + 7 * 16
                    If bits.Count < endIdam Then Continue For
                    Dim idamBytes = DecodeWords(bits, offs, 7)
                    If ComputeCrcCcittFalse(idamBytes) <> 0 Then Continue For
                    pending = New ParsedIdam With {.EndOffset = endIdam, .R = idamBytes(3), .N = idamBytes(4)}
                    seenIds.Add(pending.R)
                    Continue For
                End If
                If mark <> &HFB AndAlso mark <> &HF8 Then Continue For
                If pending Is Nothing OrElse offs - pending.EndOffset > 1000 Then
                    pending = Nothing
                    Continue For
                End If
                Dim size = 128 << pending.N
                Dim byteCount = 1 + size + 2
                Dim endDam = offs + byteCount * 16
                If bits.Count < endDam Then Continue For
                Dim payload = DecodeWords(bits, offs, byteCount)
                If ComputeCrcCcittFalse(payload) <> 0 Then
                    pending = Nothing
                    Continue For
                End If
                Dim fmData(size - 1) As Byte
                Array.Copy(payload, 1, fmData, 0, size)
                sectors(pending.R) = fmData
                sectorNs(pending.R) = pending.N
                pending = Nothing
            Next
            Dim totalSlots = Math.Max(seenIds.Count, sectors.Count)
            Return New ScanResult With {
                .Sectors = sectors,
                .SectorNs = sectorNs,
                .TotalIdamSlots = totalSlots,
                .Summary = String.Format("IBM FM ({0}/{1} sectors)", sectors.Count, totalSlots),
                .FormatName = "ibm.fm"
            }
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::(no direct 1:1 symbol; VB helper record for intermediate IDAM parse state)
        Private Class ParsedIdam
            Public Property EndOffset As Integer
            Public Property R As Integer
            Public Property N As Integer
        End Class

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.track
        ' Python exposes `self.track` as the underlying IBMTrack_Fixed once a scan has
        ' settled. Mirror that for callers (e.g. IMD.emit_track) that need to unwrap.
        Public ReadOnly Property Track As IbmTrackFixed
            Get
                Dim built = BuildScanTrack()
                If built Is Nothing Then
                    Return Nothing
                End If
                built.SetImgTrack(GetImgTrack())
                Return built
            End Get
        End Property

        Private Function BuildScanTrack() As IbmTrackFixed
            If _sectors.Count = 0 Then
                Return Nothing
            End If

            Dim sectorIds = _sectors.Keys.ToList()
            Dim sectorSizes As New List(Of Integer)(sectorIds.Count)
            Dim sectorHeaderNs As New List(Of Integer)(sectorIds.Count)
            For Each id In sectorIds
                Dim size = _sectors(id).Length
                sectorSizes.Add(size)
                Dim n As Integer
                If _sectorNs.ContainsKey(id) Then
                    n = _sectorNs(id)
                Else
                    n = InferSectorN(size)
                End If
                ErrorHandling.Check(n >= 0, String.Format("ibm.scan: Unsupported sector size {0}", size))
                sectorHeaderNs.Add(n)
            Next

            Return New IbmTrackFixed(_bestFormatName,
                                     _cyl,
                                     _head,
                                     sectorSizes,
                                     sectorHeaderNs,
                                     sectorIds,
                                     _head,
                                     Nothing,
                                     _bestTimePerRev,
                                     _bestClock,
                                     emitIam:=True,
                                     gap1Override:=Nothing,
                                     gap2Override:=Nothing,
                                     gap3Override:=Nothing,
                                     gap4aOverride:=Nothing,
                                     gapByteOverride:=Nothing)
        End Function

        Private Shared Function InferSectorN(size As Integer) As Integer
            For n = 0 To 7
                If (128 << n) = size Then
                    Return n
                End If
            Next
            Return -1
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function FindPatternOffsets(bits As List(Of Boolean), pattern As Boolean()) As IEnumerable(Of Integer)
            Return BitHelpers.FindPatternOffsets(bits, pattern)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeWord)
        Private Shared Function DecodeWord(bits As List(Of Boolean), offset As Integer) As Byte
            Return DecodeWords(bits, offset, 1)(0)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeWords)
        Private Shared Function DecodeWords(bits As List(Of Boolean), offset As Integer, byteCount As Integer) As Byte()
            Dim output(byteCount - 1) As Byte
            For i = 0 To byteCount - 1
                Dim word As UShort = 0US
                For j = 0 To 15
                    word = CUShort((word << 1) Or If(bits(offset + i * 16 + j), 1US, 0US))
                Next
                Dim index As Integer = word And &H5555US
                Dim y = (index + (index >> 1)) And &H3333
                y = (y + (y >> 2)) And &HF0F
                y = (y + (y >> 4)) And &HFF
                output(i) = CByte(y)
            Next
            Return output
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py uses
        ' crcmod.predefined 'crc-ccitt-false'. Implementation lives in
        ' Greaseweazle.Codecs.Crc16Ccitt and uses a 256-entry lookup table.
        Private Shared Function ComputeCrcCcittFalse(data As Byte()) As UShort
            Return Crc16Ccitt.Compute(data)
        End Function

        ' Python map: shared helper. See Greaseweazle.Codecs.BitHelpers.
        Private Shared Function BitsFrom01(spec As String) As Boolean()
            Return BitHelpers.BitsFrom01(spec)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SyncWordBits)
        Private Shared Function SyncWordBits(data As Byte, clock As Byte) As Boolean()
            Dim word As UShort = 0US
            For i = 7 To 0 Step -1
                word = CUShort((word << 1) Or ((clock >> i) And 1))
                word = CUShort((word << 1) Or ((data >> i) And 1))
            Next
            Dim syncBytes As Byte() = {CByte((word >> 8) And &HFF), CByte(word And &HFF)}
            Dim outputBits As New List(Of Boolean)(16)
            For Each b In syncBytes
                For i = 7 To 0 Step -1
                    outputBits.Add(((b >> i) And 1) = 1)
                Next
            Next
            Return outputBits.ToArray()
        End Function
    End Class

End Namespace
