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

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.sectors
        ' Python stores Sector instances in physical decode order (sorted by
        ' a.start). Each entry carries its own IDAM (c/h/r/n) and DAM (data)
        ' plus a combined CRC indicator (s.crc = idam.crc | dam.crc). Bad-CRC
        ' sectors are kept in the list with crc != 0 -- they still count
        ' toward nsec but are reported as "missing" by nr_missing().
        Private _sectors As New List(Of ScanSector)()
        Private _summary As String = "IBM Empty"
        Private _bestFormatName As String = "ibm.mfm"
        Private _bestTimePerRev As Double = 0.2
        Private _bestClock As Double = 2.0E-6

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

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                ' Python: nsec = len(self.sectors). The list contains every
                ' IDAM+DAM pair found, including bad-CRC ones.
                Return _sectors.Count
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            ' Python: self.sectors[sec_id].crc == 0. Treats sec_id as a
            ' positional index into the (physical-order) sector list, not as
            ' an IDAM R value.
            Return sectorId >= 0 _
                AndAlso sectorId < _sectors.Count _
                AndAlso _sectors(sectorId).GoodCrc
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.nr_missing
        Public Overrides Function NrMissing() As Integer
            ' Python: count of sectors whose combined CRC is non-zero.
            Return _sectors.Where(Function(s) Not s.GoodCrc).Count()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            ' Python sorts sectors by idam.r and concatenates dam.data for
            ' every sector (good and bad CRC alike).
            Dim sorted = _sectors.OrderBy(Function(s) s.R).ToList()
            Dim bytes As New List(Of Byte)()
            For Each s In sorted
                bytes.AddRange(s.Data)
            Next
            Return bytes.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            If _sectors.Count = 0 Then
                Throw New FatalException("ibm.scan: Cannot handle IMG input data")
            End If
            ' Python sorts the in-memory list by idam.r, fills in dam.data
            ' from the supplied buffer, then resorts back to start order.
            Dim sorted = _sectors.OrderBy(Function(s) s.R).ToList()
            Dim totalSize = sorted.Sum(Function(s) s.Data.Length)
            Dim src = If(trackData, Array.Empty(Of Byte)())
            Dim padded(totalSize - 1) As Byte
            If src.Length > 0 Then
                Dim copy = Math.Min(src.Length, totalSize)
                Array.Copy(src, padded, copy)
            End If
            Dim pos = 0
            For Each s In sorted
                Dim size = s.Data.Length
                Dim newData(size - 1) As Byte
                Array.Copy(padded, pos, newData, 0, size)
                s.Data = newData
                ' Python: s.crc = s.idam.crc = s.dam.crc = 0 -- writing image
                ' bytes is treated as having reconstructed a perfect sector.
                s.GoodCrc = True
                pos += size
            Next
            Return totalSize
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Scan.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim flux = track.Flux()
            flux.CueAtIndex()

            ' Python: "Add more data to an existing track instance" -- if a
            ' previous DecodeFlux pass already produced sectors, re-decode at
            ' the locked-in best-guess settings and merge new sectors with
            ' the existing list (replacing bad-CRC slots with good ones,
            ' otherwise leaving the existing entry alone).
            If _sectors.Count > 0 Then
                Dim raw As New PllTrack(clock:=_bestClock, data:=flux, timePerRev:=_bestTimePerRev, pll:=pll)
                Dim parsed As ScanResult
                If String.Equals(_bestFormatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase) Then
                    parsed = ParseMfm(raw)
                Else
                    parsed = ParseFm(raw)
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
                Dim guess = If(mode = "mfm", ParseMfm(raw), ParseFm(raw))
                Dim total = guess.Sectors.Count
                Dim good = guess.Sectors.Where(Function(s) s.GoodCrc).Count()
                If total > 0 AndAlso good = total Then
                    AdoptResult(guess, BestGuess.Item1, BestGuess.Item2,
                                If(mode = "mfm", "ibm.mfm", "ibm.fm"))
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

                    Dim mfm = ParseMfm(raw)
                    If best Is Nothing OrElse mfm.Score > best.Score Then
                        best = mfm
                        bestTimePerRev = timePerRev
                        bestClock = clock
                        bestFormat = "ibm.mfm"
                    End If

                    Dim fm = ParseFm(raw)
                    If best Is Nothing OrElse fm.Score > best.Score Then
                        best = fm
                        bestTimePerRev = timePerRev
                        bestClock = clock
                        bestFormat = "ibm.fm"
                    End If
                Next
            Next

            If best IsNot Nothing AndAlso best.Sectors.Count > 0 Then
                AdoptResult(best, bestTimePerRev, bestClock, bestFormat)
                ' Python: BEST_GUESS is updated only once, after probing finishes,
                ' using the winning track's settings. Mirror that here.
                BestGuess = Tuple.Create(bestTimePerRev, bestClock,
                                         If(String.Equals(bestFormat, "ibm.mfm", StringComparison.OrdinalIgnoreCase), "mfm", "fm"))
            End If
        End Sub

        Private Sub AdoptResult(result As ScanResult,
                                timePerRev As Double,
                                clock As Double,
                                formatName As String)
            _sectors = result.Sectors
            _bestTimePerRev = timePerRev
            _bestClock = clock
            _bestFormatName = formatName
            _summary = BuildSummaryString()
        End Sub

        Private Function BuildSummaryString() As String
            Dim mode = If(String.Equals(_bestFormatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase), "IBM MFM", "IBM FM")
            Dim total = _sectors.Count
            Dim good = total - NrMissing()
            Return String.Format("{0} ({1}/{2} sectors)", mode, good, total)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.decode_raw (dedup pass)
        Private Sub MergeScan(parsed As ScanResult)
            ' Python: for each newly-decoded Sector, look for an existing
            ' entry with abs(start - a.start) < 1000. If a dupe is found, the
            ' new sector replaces the old one only when the existing entry
            ' has bad CRC and the new one has good CRC. Otherwise the new
            ' entry is appended. Finally the list is resorted by start.
            For Each newSec In parsed.Sectors
                Dim foundDupe = False
                For i = 0 To _sectors.Count - 1
                    If Math.Abs(_sectors(i).Start - newSec.Start) < 1000 Then
                        foundDupe = True
                        If Not _sectors(i).GoodCrc AndAlso newSec.GoodCrc Then
                            _sectors(i) = newSec
                        End If
                        Exit For
                    End If
                Next
                If Not foundDupe Then
                    _sectors.Add(newSec)
                End If
            Next
            _sectors.Sort(Function(a, b) a.Start.CompareTo(b.Start))
            _summary = BuildSummaryString()
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

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.summary_string
        Public Overrides Function SummaryString() As String
            Return _summary
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Sector
        Private Class ScanSector
            Public Property Start As Integer
            Public Property R As Integer
            Public Property N As Integer
            Public Property Data As Byte()
            Public Property GoodCrc As Boolean
        End Class

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB helper container for scan pass results)
        Private Class ScanResult
            Public Property Sectors As List(Of ScanSector)
            Public Property FormatName As String

            Public ReadOnly Property Score As Integer
                Get
                    ' Python's choose-track scoring: t.nsec - t.nr_missing(),
                    ' i.e. the number of decoded (good-CRC) sectors.
                    Return Sectors.Where(Function(s) s.GoodCrc).Count()
                End Get
            End Property
        End Class

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.mfm_decode_raw
        ' Mirrors Python's behaviour of always remembering the most recent
        ' IDAM (good or bad CRC) and pairing it with the next DAM/DDAM that
        ' appears within ~1000 bits, even when either CRC is bad. Bad-CRC
        ' sectors are still added to the result with GoodCrc = False.
        ' After collection, sector start offsets are normalised to within
        ' a single revolution (Python: a.delta(p) loop) so duplicates seen
        ' across revolutions land at the same Start and the dedup pass
        ' (mirror of decode_raw's per-Sector dupe check) collapses them.
        Private Function ParseMfm(rawTrack As PllTrack) As ScanResult
            Dim bits = rawTrack.GetAllData().Item1
            Dim sectors As New List(Of ScanSector)()
            Dim pending As ParsedIdam = Nothing
            For Each offs In FindPatternOffsets(bits, MfmSyncPattern)
                If bits.Count < offs + 64 Then Continue For
                Dim mark = DecodeWord(bits, offs + 48)
                If mark = &HFE Then
                    If bits.Count < offs + 160 Then Continue For
                    Dim idamBytes = DecodeWords(bits, offs, 10)
                    Dim idamCrc = ComputeCrcCcittFalse(idamBytes)
                    pending = New ParsedIdam With {
                        .Start = offs,
                        .EndOffset = offs + 160,
                        .R = idamBytes(6),
                        .N = idamBytes(7),
                        .CrcGood = (idamCrc = 0)
                    }
                    Continue For
                End If
                If mark <> &HFB AndAlso mark <> &HF8 Then Continue For
                If pending Is Nothing OrElse offs - pending.EndOffset > 1000 Then
                    pending = Nothing
                    Continue For
                End If
                Dim size = SafeSectorSize(pending.N)
                If size <= 0 Then
                    pending = Nothing
                    Continue For
                End If
                Dim byteCount = 4 + size + 2
                Dim e = offs + byteCount * 16
                If bits.Count < e Then
                    pending = Nothing
                    Continue For
                End If
                Dim payload = DecodeWords(bits, offs, byteCount)
                Dim damCrcGood = (ComputeCrcCcittFalse(payload) = 0)
                Dim mfmData(size - 1) As Byte
                Array.Copy(payload, 4, mfmData, 0, size)
                sectors.Add(New ScanSector With {
                    .Start = pending.Start,
                    .R = pending.R,
                    .N = pending.N,
                    .Data = mfmData,
                    .GoodCrc = pending.CrcGood AndAlso damCrcGood
                })
                pending = Nothing
            Next
            Return BuildScanResult(sectors, rawTrack, "ibm.mfm")
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.fm_decode_raw
        ' Same bad-CRC-aware + per-revolution-normalised semantics as ParseMfm.
        Private Function ParseFm(rawTrack As PllTrack) As ScanResult
            Dim bits = rawTrack.GetAllData().Item1
            Dim sectors As New List(Of ScanSector)()
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
                    Dim idamCrc = ComputeCrcCcittFalse(idamBytes)
                    pending = New ParsedIdam With {
                        .Start = offs,
                        .EndOffset = endIdam,
                        .R = idamBytes(3),
                        .N = idamBytes(4),
                        .CrcGood = (idamCrc = 0)
                    }
                    Continue For
                End If
                If mark <> &HFB AndAlso mark <> &HF8 Then Continue For
                If pending Is Nothing OrElse offs - pending.EndOffset > 1000 Then
                    pending = Nothing
                    Continue For
                End If
                Dim size = SafeSectorSize(pending.N)
                If size <= 0 Then
                    pending = Nothing
                    Continue For
                End If
                Dim byteCount = 1 + size + 2
                Dim endDam = offs + byteCount * 16
                If bits.Count < endDam Then
                    pending = Nothing
                    Continue For
                End If
                Dim payload = DecodeWords(bits, offs, byteCount)
                Dim damCrcGood = (ComputeCrcCcittFalse(payload) = 0)
                Dim fmData(size - 1) As Byte
                Array.Copy(payload, 1, fmData, 0, size)
                sectors.Add(New ScanSector With {
                    .Start = pending.Start,
                    .R = pending.R,
                    .N = pending.N,
                    .Data = fmData,
                    .GoodCrc = pending.CrcGood AndAlso damCrcGood
                })
                pending = Nothing
            Next
            Return BuildScanResult(sectors, rawTrack, "ibm.fm")
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.mfm_decode_raw +
        ' decode_raw (the "Convert to offsets within track" loop and the
        ' isinstance(a, Sector) dedup that follows). Combined here because
        ' Python first normalises area starts via raw.revolutions, then
        ' decode_raw collapses per-revolution duplicates into the per-track
        ' sector list.
        Private Shared Function BuildScanResult(sectors As List(Of ScanSector),
                                                rawTrack As PllTrack,
                                                formatName As String) As ScanResult
            NormaliseToRevolutions(sectors, rawTrack)
            sectors.Sort(Function(a, b) a.Start.CompareTo(b.Start))
            Dim deduped As New List(Of ScanSector)()
            For Each sec In sectors
                Dim foundDupe = False
                For i = 0 To deduped.Count - 1
                    If Math.Abs(deduped(i).Start - sec.Start) < 1000 Then
                        foundDupe = True
                        If Not deduped(i).GoodCrc AndAlso sec.GoodCrc Then
                            deduped(i) = sec
                        End If
                        Exit For
                    End If
                Next
                If Not foundDupe Then
                    deduped.Add(sec)
                End If
            Next
            deduped.Sort(Function(a, b) a.Start.CompareTo(b.Start))
            Return New ScanResult With {
                .Sectors = deduped,
                .FormatName = formatName
            }
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.mfm_decode_raw
        '   "Convert to offsets within track" loop (lines 492-504). Walks
        '   start-sorted areas alongside an iterator over revolution bit
        '   counts; each time an area's start crosses the cumulative end of
        '   the current revolution we set p=n (start of the next rev) and
        '   advance n by the next revolution's NrBits, then subtract p from
        '   each subsequent area's start. That yields per-revolution offsets
        '   so duplicates from different revolutions collapse on dedup.
        Private Shared Sub NormaliseToRevolutions(sectors As List(Of ScanSector),
                                                  rawTrack As PllTrack)
            If rawTrack Is Nothing OrElse rawTrack.Revolutions Is Nothing OrElse rawTrack.Revolutions.Count = 0 Then
                Return
            End If
            sectors.Sort(Function(a, b) a.Start.CompareTo(b.Start))
            Dim revs = rawTrack.Revolutions
            Dim revIdx = 0
            Dim p As Long = 0
            Dim n As Long = revs(0).NrBits
            For Each sec In sectors
                If sec.Start >= n Then
                    p = n
                    revIdx += 1
                    If revIdx < revs.Count Then
                        n += revs(revIdx).NrBits
                    Else
                        n = Long.MaxValue
                    End If
                End If
                sec.Start = CInt(sec.Start - p)
            Next
        End Sub

        ' Python uses arbitrary-precision integers for `128 << idam.n` and
        ' relies on len(bits) bounds to skip impossibly large reads. .NET
        ' Int32 wraps on shifts >= 25, so reject any N outside the IBM-valid
        ' 0..7 range here -- the corresponding Python branch always fails
        ' the len(bits) check anyway, so the resulting sector is dropped.
        Private Shared Function SafeSectorSize(n As Integer) As Integer
            If n < 0 OrElse n > 7 Then Return 0
            Return 128 << n
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::(no direct 1:1 symbol; VB helper record for intermediate IDAM parse state)
        Private Class ParsedIdam
            Public Property Start As Integer
            Public Property EndOffset As Integer
            Public Property R As Integer
            Public Property N As Integer
            Public Property CrcGood As Boolean
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

            ' Pass sectors in physical order; IbmTrackFixed re-sorts by sector
            ' ID internally for image I/O via its _logicalOrder map.
            Dim sectorIds As New List(Of Integer)(_sectors.Count)
            Dim sectorSizes As New List(Of Integer)(_sectors.Count)
            Dim sectorHeaderNs As New List(Of Integer)(_sectors.Count)
            For Each s In _sectors
                sectorIds.Add(s.R)
                sectorSizes.Add(s.Data.Length)
                Dim n = s.N
                If n < 0 OrElse n > 7 Then
                    n = InferSectorN(s.Data.Length)
                End If
                ErrorHandling.Check(n >= 0, String.Format("ibm.scan: Unsupported sector size {0}", s.Data.Length))
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
