Imports System.Text.RegularExpressions
Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    Public Module IbmHelpers
        ' Python map: src/greaseweazle/codec/ibm/ibm.py::sync
        Public Function Sync(dat As Byte, Optional clk As Byte = &HC7) As Byte()
            Dim x As UShort = 0US
            For i = 0 To 7
                x = CUShort(x << 1)
                x = CUShort(x Or ((clk >> (7 - i)) And 1))
                x = CUShort(x << 1)
                x = CUShort(x Or ((dat >> (7 - i)) And 1))
            Next
            Return New Byte() {CByte((x >> 8) And &HFFUS), CByte(x And &HFFUS)}
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::fm_encode
        Public Function FmEncode(values As IEnumerable(Of Byte)) As Byte()
            Dim output As New List(Of Byte)()
            For Each x In values
                Dim y As Integer = x
                If (y And &HAA) = 0 Then
                    y = y Or &HAA
                End If
                output.Add(CByte(y And &HFF))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::mfm_encode
        Public Function MfmEncode(values As IEnumerable(Of Byte)) As Byte()
            Dim y As Integer = 0
            Dim output As New List(Of Byte)()
            For Each x In values
                y = (y << 8) Or x
                If (x And &HAA) = 0 Then
                    y = y Or (Not ((y >> 1) Or (y << 1)) And &HAAAA)
                End If
                y = y And &HFF
                output.Add(CByte(y))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::encode
        Public Function Encode(values As IEnumerable(Of Byte)) As Byte()
            Dim output As New List(Of Byte)()
            For Each value In values
                Dim word As UShort = 0US
                For i = 7 To 0 Step -1
                    word = CUShort((word << 2) Or ((value >> i) And 1))
                Next
                output.Add(CByte((word >> 8) And &HFF))
                output.Add(CByte(word And &HFF))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::decode
        Public Function Decode(values As IEnumerable(Of Byte)) As Byte()
            Dim bytes = values.ToArray()
            Dim outLen = bytes.Length \ 2
            If outLen <= 0 Then
                Return Array.Empty(Of Byte)()
            End If
            Dim output(outLen - 1) As Byte
            For i = 0 To outLen - 1
                Dim word As UShort = CUShort((CUShort(bytes(i * 2)) << 8) Or bytes(i * 2 + 1))
                Dim index As Integer = word And &H5555US
                Dim y As Integer = (index + (index >> 1)) And &H3333
                y = (y + (y >> 2)) And &HF0F
                y = (y + (y >> 4)) And &HFF
                output(i) = CByte(y And &HFF)
            Next
            Return output
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::sec_map
        Public Function SecMap(nsec As Integer,
                               interleave As Integer,
                               cskew As Integer,
                               hskew As Integer,
                               cyl As Integer,
                               head As Integer) As List(Of Integer)
            Dim secMapArray(nsec - 1) As Integer
            For i = 0 To secMapArray.Length - 1
                secMapArray(i) = -1
            Next
            Dim pos = 0
            If nsec <> 0 Then
                pos = (cyl * cskew + head * hskew) Mod nsec
            End If
            For i = 0 To nsec - 1
                While secMapArray(pos) <> -1
                    pos = (pos + 1) Mod nsec
                End While
                secMapArray(pos) = i
                pos = (pos + interleave) Mod nsec
            Next
            Return secMapArray.ToList()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::sec_sz
        Public Function SecSz(n As Integer) As Integer
            If n <= 7 Then
                Return 128 << n
            End If
            Return 128 << 8
        End Function
    End Module

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::Gaps
    Public Class Gaps
        Public Property Gap1 As Integer
        Public Property Gap2 As Integer
        Public Property Gap3 As Integer()
        Public Property Gap4a As Integer

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Gaps.__init__
        Public Sub New(gap1 As Integer, gap2 As Integer, gap3 As Integer(), gap4a As Integer)
            Me.Gap1 = gap1
            Me.Gap2 = gap2
            Me.Gap3 = gap3
            Me.Gap4a = gap4a
        End Sub
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::Mark
    Public NotInheritable Class Mark
        Public Const IAM As Byte = &HFC
        Public Const IDAM As Byte = &HFE
        Public Const DAM As Byte = &HFB
        Public Const DDAM As Byte = &HF8
        Public Const DAMDECMMFM As Byte = &HFD
        Public Const DDAMDECMMFM As Byte = &HF9
        Public Const DAMTRS80DIR As Byte = &HFA
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::Mode
    Public Enum IbmMode
        Fm = 0
        Mfm = 1
        DecRx02 = 2
    End Enum

    Public Module IbmModeExtensions
        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Mode.__str__
        <Runtime.CompilerServices.Extension()>
        Public Function ToPythonString(value As IbmMode) As String
            If value = IbmMode.Fm Then
                Return "IBM FM"
            End If
            If value = IbmMode.Mfm Then
                Return "IBM MFM"
            End If
            Return "DEC RX02"
        End Function
    End Module

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::TrackArea
    Public Class TrackArea
        Public Property Start As Integer
        Public Property [End] As Integer
        Public Property Crc As Integer?

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::TrackArea.__init__
        Public Sub New(start As Integer, [end] As Integer, Optional crc As Integer? = Nothing)
            Me.Start = start
            Me.End = [end]
            Me.Crc = crc
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::TrackArea.delta
        Public Overridable Sub Delta(value As Integer)
            Start -= value
            [End] -= value
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::TrackArea.__eq__
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(obj, TrackArea)
            If other Is Nothing Then
                Return False
            End If
            Return Math.Abs(Start - other.Start) < 1000 AndAlso
                   Math.Abs([End] - other.End) < 1000 AndAlso
                   Crc.Equals(other.Crc)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IDAM
    Public Class Idam
        Inherits TrackArea

        Public Property C As Integer
        Public Property H As Integer
        Public Property R As Integer
        Public Property N As Integer

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IDAM.__init__
        Public Sub New(start As Integer, [end] As Integer, crc As Integer, c As Integer, h As Integer, r As Integer, n As Integer)
            MyBase.New(start, [end], crc)
            Me.C = c
            Me.H = h
            Me.R = r
            Me.N = n
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IDAM.__str__
        Public Overrides Function ToString() As String
            Return String.Format("IDAM:{0,6}-{1,6} c={2:x2} h={3:x2} r={4:x2} n={5:x2} CRC:{6:x4}",
                                 Start, [End], C, H, R, N, Crc.GetValueOrDefault())
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IDAM.__eq__
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(obj, Idam)
            If other Is Nothing Then
                Return False
            End If
            Return MyBase.Equals(other) AndAlso
                   C = other.C AndAlso H = other.H AndAlso R = other.R AndAlso N = other.N
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IDAM.__copy__
        Public Function Copy() As Idam
            Return New Idam(Start, [End], Crc.GetValueOrDefault(), C, H, R, N)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::DAM
    Public Class Dam
        Inherits TrackArea

        Public Property MarkValue As Integer
        Public Property Data As Byte()

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::DAM.__init__
        Public Sub New(start As Integer, [end] As Integer, crc As Integer, mark As Integer, data As Byte())
            MyBase.New(start, [end], crc)
            Me.MarkValue = mark
            Me.Data = If(data, Array.Empty(Of Byte)())
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::DAM.__str__
        Public Overrides Function ToString() As String
            Return String.Format("DAM:{0,6}-{1,6} mark={2:x2} CRC:{3:x4} [{4} bytes]",
                                 Start, [End], MarkValue, Crc.GetValueOrDefault(), Data.Length)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::DAM.__eq__
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(obj, Dam)
            If other Is Nothing Then
                Return False
            End If
            Return MyBase.Equals(other) AndAlso
                   MarkValue = other.MarkValue AndAlso Data.SequenceEqual(other.Data)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::DAM.__copy__
        Public Function Copy() As Dam
            Return New Dam(Start, [End], Crc.GetValueOrDefault(), MarkValue, Data.ToArray())
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::Sector
    Public Class Sector
        Inherits TrackArea

        Public Property IdamValue As Idam
        Public Property DamValue As Dam

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Sector.__init__
        Public Sub New(idam As Idam, dam As Dam)
            ' Python: super().__init__(idam.start, dam.end, idam.crc | dam.crc)
            MyBase.New(If(idam IsNot Nothing, idam.Start, 0),
                       If(dam IsNot Nothing, dam.End, 0),
                       If(idam IsNot Nothing, idam.Crc.GetValueOrDefault(), 0) Or
                       If(dam IsNot Nothing, dam.Crc.GetValueOrDefault(), 0))
            IdamValue = idam
            DamValue = dam
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Sector.__str__
        Public Overrides Function ToString() As String
            Dim sb As New System.Text.StringBuilder()
            sb.AppendFormat("Sec: {0,6}-{1,6} CRC:{2:x4}", Start, [End], Crc.GetValueOrDefault())
            sb.Append(vbLf)
            sb.AppendFormat(" {0}", If(IdamValue Is Nothing, "IDAM:None", IdamValue.ToString()))
            sb.Append(vbLf)
            sb.AppendFormat(" {0}", If(DamValue Is Nothing, "DAM:None", DamValue.ToString()))
            Return sb.ToString()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Sector.delta
        Public Overrides Sub Delta(value As Integer)
            MyBase.Delta(value)
            If IdamValue IsNot Nothing Then
                IdamValue.Delta(value)
            End If
            If DamValue IsNot Nothing Then
                DamValue.Delta(value)
            End If
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Sector.__eq__
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(obj, Sector)
            If other Is Nothing Then
                Return False
            End If
            Return MyBase.Equals(other) AndAlso
                   Object.Equals(IdamValue, other.IdamValue) AndAlso
                   Object.Equals(DamValue, other.DamValue)
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return Start Xor [End]
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IAM
    Public Class Iam
        Inherits TrackArea

        Public Sub New(start As Integer, [end] As Integer, Optional crc As Integer? = Nothing)
            MyBase.New(start, [end], crc)
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IAM.__str__
        Public Overrides Function ToString() As String
            Return String.Format("IAM:{0,6}-{1,6}", Start, [End])
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IAM.__copy__
        Public Function Copy() As Iam
            Return New Iam(Start, [End], Crc)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::DEC_MMFM
    Public Class DecMmfm
        ' Python map: src/greaseweazle/codec/ibm/ibm.py::DEC_MMFM.__init__
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::DEC_MMFM.encode
        Public Function Encode(pre As Byte()) As Byte()
            Return IbmTrackFixed.DecMmfmEncode(pre)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::DEC_MMFM.decode
        Public Function Decode(bits As List(Of Boolean)) As Byte()
            Return IbmTrackFixed.DecMmfmDecode(bits)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_FixedDef
    Public Class IbmTrackFixedDef
        Implements TrackDef

        Private _defaultRevs As Double = 2.0
        Private _sectorCount As Integer
        Private _sectorSizes As New List(Of Integer)()
        Private _imgBytesPerSector As Integer?
        Private ReadOnly _formatName As String
        Private _idBase As Integer = 1
        Private _headOverride As Integer?
        Private _interleave As Integer = 1
        Private _cskew As Integer
        Private _hskew As Integer
        Private _rate As Integer
        Private _rpm As Integer = 300
        Private _iam As Boolean = True
        Private _gap1 As Integer?
        Private _gap2 As Integer?
        Private _gap3 As Integer?
        Private _gap4a As Integer?
        Private _gapByte As Byte?

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_FixedDef.__init__
        Public Sub New(formatName As String)
            _formatName = formatName
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return _defaultRevs
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_FixedDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 0 AndAlso n <= 256, "secs out of range")
                    _sectorCount = n
                Case "bps"
                    _sectorSizes = ParseBpsList(value)
                Case "img_bps"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 128 AndAlso n <= 8192, "img_bps out of range")
                    _imgBytesPerSector = n
                Case "id", "cskew", "hskew"
                    ' Python: int(val, base=0) -- accepts 0x/0o/0b prefixes plus decimal.
                    Dim n = ParseRadixZeroInt(value)
                    ErrorHandling.Check(n >= 0 AndAlso n <= 255, String.Format("{0} out of range", key))
                    Select Case key
                        Case "id" : _idBase = n
                        Case "cskew" : _cskew = n
                        Case "hskew" : _hskew = n
                    End Select
                Case "h", "gap1", "gap2", "gap3", "gap4a", "gapbyte"
                    Dim valueOpt As Integer? = Nothing
                    If Not String.Equals(value, "auto", StringComparison.OrdinalIgnoreCase) Then
                        Dim n = ParseRadixZeroInt(value)
                        ErrorHandling.Check(n >= 0 AndAlso n <= 255, String.Format("{0} out of range", key))
                        valueOpt = n
                    End If
                    Select Case key
                        Case "h" : _headOverride = valueOpt
                        Case "gap1" : _gap1 = valueOpt
                        Case "gap2" : _gap2 = valueOpt
                        Case "gap3" : _gap3 = valueOpt
                        Case "gap4a" : _gap4a = valueOpt
                        Case "gapbyte" : _gapByte = If(valueOpt.HasValue, CType(CByte(valueOpt.Value), Byte?), Nothing)
                    End Select
                Case "interleave"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 1 AndAlso n <= 255, "interleave out of range")
                    _interleave = n
                Case "rate"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 1 AndAlso n <= 2000, "rate out of range")
                    _rate = n
                Case "rpm"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 1 AndAlso n <= 2000, "rpm out of range")
                    _rpm = n
                Case "iam"
                    ErrorHandling.Check(value = "yes" OrElse value = "no", "bad iam value")
                    _iam = (value = "yes")
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::(no direct 1:1 symbol; VB helper for Python `int(val, base=0)`)
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

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_FixedDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ' Python evaluates iam/gap1, secs/sz, img_bps independently. Mirror that
            ' instead of returning early when secs == 0 (which would skip the
            ' iam/gap1 invariant).
            ErrorHandling.Check(_iam OrElse Not _gap1.HasValue, "gap1 specified but no iam")
            ErrorHandling.Check(_sectorCount = 0 OrElse _sectorSizes.Count > 0, "sector size not specified")
            If _sectorCount > 0 Then
                While _sectorSizes.Count < _sectorCount
                    _sectorSizes.Add(_sectorSizes.Last())
                End While
            End If
            If _imgBytesPerSector.HasValue Then
                Dim maxSize = If(_sectorSizes.Count > 0, _sectorSizes.Max(), 0)
                ErrorHandling.Check(_imgBytesPerSector.Value >= maxSize, "img_bps cannot be smaller than sector data size")
            End If
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_FixedDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            ' Python iterates sec_map(...) and computes idam.r=id0+sec, n=sec_n(sec).
            ' That means sector slot at physical position i carries logical-index sec_map[i],
            ' so both the ID list and the size list must be permuted by sec_map together.
            Dim secMapList As List(Of Integer)
            If _sectorCount <= 0 Then
                secMapList = New List(Of Integer)()
            Else
                secMapList = IbmHelpers.SecMap(_sectorCount, _interleave, _cskew, _hskew, cyl, head)
            End If

            Dim allSizes As New List(Of Integer)(_sectorSizes)
            ' sec_n(i) = config.sz[i] if i < len else config.sz[-1]: pad if too short.
            While allSizes.Count < _sectorCount AndAlso allSizes.Count > 0
                allSizes.Add(allSizes.Last())
            End While

            Dim physicalSizes = secMapList.Select(Function(s) allSizes(Math.Min(s, Math.Max(0, allSizes.Count - 1)))).ToList()
            Dim resolvedSizes As List(Of Integer)
            If String.Equals(_formatName, "dec.rx02", StringComparison.OrdinalIgnoreCase) Then
                resolvedSizes = physicalSizes.Select(Function(x) x * 2).ToList()
            Else
                resolvedSizes = physicalSizes.ToList()
            End If
            Dim headerNs = physicalSizes.Select(Function(x) HeaderBytesPerSectorToN(x)).ToList()
            Dim sectorIds = secMapList.Select(Function(s) _idBase + s).ToList()
            Dim isMfm = String.Equals(_formatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase)
            Dim isFm = String.Equals(_formatName, "ibm.fm", StringComparison.OrdinalIgnoreCase)
            Dim isDecRx02 = String.Equals(_formatName, "dec.rx02", StringComparison.OrdinalIgnoreCase)

            ' --- Python from_config gap & rate resolution ---
            ' Default gap tables (MFMGaps / FMGaps).
            Dim defaultGap1 = If(isMfm, 50, 26)
            Dim defaultGap2 = If(isMfm, 22, 11)
            Dim defaultGap4a = If(isMfm, 80, 40)
            Dim defaultGap3Table As Integer() = If(isMfm,
                New Integer() {32, 54, 84, 116, 255, 255, 255, 255},
                New Integer() {27, 42, 58, 138, 255, 255, 255, 255})
            Dim gapPresync = If(isMfm, 12, 6)
            Dim synclen = If(isMfm, 4, 1)

            Dim gap1 As Integer? = Nothing
            If _iam Then
                gap1 = If(_gap1.HasValue, _gap1.Value, defaultGap1)
            End If
            Dim gap2 As Integer = If(_gap2.HasValue, _gap2.Value, defaultGap2)
            Dim gap3 As Integer = If(_gap3.HasValue, _gap3.Value, 0)
            Dim gap4a As Integer = If(_gap4a.HasValue, _gap4a.Value, defaultGap4a)

            Dim nsec = _sectorCount
            Dim idxSz As Integer = gap4a
            If gap1.HasValue Then
                idxSz += gapPresync + synclen + gap1.Value
            End If
            Dim idamSz As Integer = gapPresync + synclen + 4 + 2 + gap2
            Dim damSzPre As Integer = gapPresync + synclen
            Dim damSzPost As Integer = 2 + gap3

            Dim trackLen As Integer = idxSz + (idamSz + damSzPre + damSzPost) * nsec
            For Each n In headerNs
                trackLen += 128 << n
            Next
            trackLen *= 16

            ' Auto-select rate when not specified.
            Dim rate = _rate
            If rate = 0 Then
                Dim rangeI As IEnumerable(Of Integer)
                If isMfm Then
                    rangeI = Enumerable.Range(1, 3) ' 1..3 -> DD/HD/ED
                Else
                    rangeI = Enumerable.Range(0, 2) ' 0..1 -> 125k / 250k
                End If
                Dim chosen = If(isMfm, 3, 1) ' fallback to highest
                For Each i In rangeI
                    Dim maxlen = (50000 * 300 \ _rpm) << i
                    maxlen += maxlen * 3 \ 100
                    If trackLen < maxlen Then
                        chosen = i
                        Exit For
                    End If
                Next
                rate = 125 << chosen
            End If

            ' MFM ED rate default GAP2 is 41 bytes.
            If isMfm AndAlso Not _gap2.HasValue AndAlso rate >= 1000 Then
                Dim newGap2 = 41
                idamSz += newGap2 - gap2
                trackLen += 16 * nsec * (newGap2 - gap2)
                gap2 = newGap2
            End If

            Dim trackLenBc As Integer = rate * 400 * 300 \ _rpm

            ' Auto-compute gap3 if not user-specified.
            If nsec <> 0 AndAlso Not _gap3.HasValue Then
                Dim space = Math.Max(0, trackLenBc - trackLen)
                Dim no = headerNs(0)
                Dim cap = If(no >= 0 AndAlso no < defaultGap3Table.Length, defaultGap3Table(no), 255)
                gap3 = Math.Min(space \ (16 * nsec), cap)
                damSzPost += gap3
                trackLen += 16 * nsec * gap3
            End If

            ' Allow at least 1% pre-index gap.
            Dim preIndexSz As Integer = trackLenBc \ 100
            If nsec <> 0 Then
                preIndexSz = Math.Max(0, preIndexSz - gap3 * 16)
            End If
            trackLen += preIndexSz

            ' Steal post-index gap if there is insufficient pre-index gap.
            If trackLen > trackLenBc AndAlso Not _gap4a.HasValue Then
                Dim newGap4a = gap4a \ 2
                idxSz -= gap4a - newGap4a
                trackLen -= gap4a - newGap4a
                gap4a = newGap4a
            End If

            Dim oversized As Boolean = False
            If trackLen > trackLenBc * 105 \ 100 Then
                oversized = True
                ' Python prints a warning here; suppress in VB build.
            End If
            trackLenBc = Math.Max(trackLenBc, trackLen)

            Dim timePerRev = 60.0 / _rpm
            Dim clock = timePerRev / trackLenBc

            Return New IbmTrackFixed(_formatName,
                                     cyl,
                                     head,
                                     resolvedSizes,
                                     headerNs,
                                     sectorIds,
                                     If(_headOverride.HasValue, _headOverride.Value, head),
                                     _imgBytesPerSector,
                                     timePerRev,
                                     clock,
                                     _iam,
                                     gap1,
                                     gap2,
                                     gap3,
                                     gap4a,
                                     _gapByte,
                                     oversized)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration HeaderBytesPerSectorToN)
        Private Shared Function HeaderBytesPerSectorToN(bytesPerSector As Integer) As Integer
            Dim n = 0
            Dim value = 128
            While value < bytesPerSector AndAlso n < 8
                value <<= 1
                n += 1
            End While
            Return n
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseBpsList)
        Private Shared Function ParseBpsList(value As String) As List(Of Integer)
            Dim sizes As New List(Of Integer)()
            For Each token In value.Split(","c)
                Dim trimmed = token.Trim()
                Dim repeat = 1
                Dim bytesPerSector = 0
                Dim m = Regex.Match(trimmed, "^(\d+)\*(\d+)$")
                If m.Success Then
                    bytesPerSector = Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                    repeat = Integer.Parse(m.Groups(2).Value, Globalization.CultureInfo.InvariantCulture)
                Else
                    bytesPerSector = Integer.Parse(trimmed, Globalization.CultureInfo.InvariantCulture)
                End If
                ErrorHandling.Check(New Integer() {128, 256, 512, 1024, 2048, 4096, 8192}.Contains(bytesPerSector), "bps value out of range")
                For i = 1 To repeat
                    sizes.Add(bytesPerSector)
                Next
            Next
            Return sizes
        End Function

    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed
    Public Class IbmTrackFixed
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private ReadOnly _formatName As String
        Private ReadOnly _sectorSizes As List(Of Integer)
        Private ReadOnly _sectorNs As List(Of Integer)
        Private ReadOnly _sectorIds As List(Of Integer)
        Private ReadOnly _logicalOrder As List(Of Integer)
        Private ReadOnly _expectedHeaderHead As Integer
        Private ReadOnly _imgBytesPerSector As Integer?
        Private ReadOnly _sectorData As List(Of Byte())
        Private ReadOnly _sectorValid As List(Of Boolean)
        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _timePerRev As Double
        Private ReadOnly _clock As Double
        Private ReadOnly _emitIam As Boolean
        Private ReadOnly _gap1Override As Integer?
        Private ReadOnly _gap2Override As Integer?
        Private ReadOnly _gap3Override As Integer?
        Private ReadOnly _gap4aOverride As Integer?
        Private ReadOnly _oversized As Boolean
        Private ReadOnly _gapByteOverride As Byte?
        Private ReadOnly _mfmSyncPattern As Boolean()
        Private ReadOnly _fmSyncPrefixPattern As Boolean()
        Private ReadOnly _decMmfmSyncPrefixPattern As Boolean()
        Private ReadOnly _fmIdamSyncPattern As Boolean()
        Private ReadOnly _fmDamSyncPattern As Boolean()
        Private ReadOnly _fmDdamSyncPattern As Boolean()
        Private ReadOnly _fmDamTrs80DirSyncPattern As Boolean()
        Private ReadOnly _fmDecDamSyncPattern As Boolean()
        Private ReadOnly _fmDecDdamSyncPattern As Boolean()
        Private Const MarkIdam As Byte = &HFE
        Private Const MarkDam As Byte = &HFB
        Private Const MarkDdam As Byte = &HF8
        Private Const MarkDamTrs80Dir As Byte = &HFA

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.__init__
        Public Sub New(formatName As String,
                       cyl As Integer,
                       head As Integer,
                       sectorSizes As List(Of Integer),
                       sectorHeaderNs As List(Of Integer),
                       sectorIds As List(Of Integer),
                       expectedHeaderHead As Integer,
                       imgBytesPerSector As Integer?,
                       timePerRev As Double,
                       clock As Double,
                       emitIam As Boolean,
                       gap1Override As Integer?,
                       gap2Override As Integer?,
                       gap3Override As Integer?,
                       gap4aOverride As Integer?,
                       gapByteOverride As Byte?,
                       Optional oversized As Boolean = False)
            _formatName = formatName
            _cyl = cyl
            _head = head
            _sectorSizes = sectorSizes
            _sectorNs = sectorHeaderNs
            _sectorIds = sectorIds
            _logicalOrder = Enumerable.Range(0, _sectorIds.Count).OrderBy(Function(i) _sectorIds(i)).ToList()
            _expectedHeaderHead = expectedHeaderHead
            _imgBytesPerSector = imgBytesPerSector
            _sectorData = sectorSizes.Select(Function(sz) New Byte(sz - 1) {}).ToList()
            _sectorValid = sectorSizes.Select(Function(dummy) False).ToList()
            _timePerRev = timePerRev
            _clock = clock
            _emitIam = emitIam
            _gap1Override = gap1Override
            _gap2Override = gap2Override
            _gap3Override = gap3Override
            _gap4aOverride = gap4aOverride
            _gapByteOverride = gapByteOverride
            _oversized = oversized
            _mfmSyncPattern = BytesToBits(New Byte() {&H44, &H89, &H44, &H89, &H44, &H89})
            _fmSyncPrefixPattern = BuildFmSyncPrefixPattern()
            _decMmfmSyncPrefixPattern = BuildDecMmfmSyncPrefixPattern()
            _fmIdamSyncPattern = SyncWordBits(&HFE, &HC7)
            _fmDamSyncPattern = SyncWordBits(&HFB, &HC7)
            _fmDdamSyncPattern = SyncWordBits(&HF8, &HC7)
            _fmDamTrs80DirSyncPattern = SyncWordBits(&HFA, &HC7)
            _fmDecDamSyncPattern = SyncWordBits(&HFD, &HC7)
            _fmDecDdamSyncPattern = SyncWordBits(&HF9, &HC7)
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.from_config
        Public Shared Function FromConfig(config As IbmTrackFixedDef, cyl As Integer, head As Integer) As IbmTrackFixed
            Return CType(config.MkTrack(cyl, head), IbmTrackFixed)
        End Function

        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _sectorSizes.Count
            End Get
        End Property

        Public ReadOnly Property FormatName As String
            Get
                Return _formatName
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.oversized
        Public ReadOnly Property Oversized As Boolean
            Get
                Return _oversized
            End Get
        End Property

        Public ReadOnly Property Cyl As Integer
            Get
                Return _cyl
            End Get
        End Property

        Public ReadOnly Property Head As Integer
            Get
                Return _head
            End Get
        End Property

        Public ReadOnly Property TimePerRevolution As Double
            Get
                Return _timePerRev
            End Get
        End Property

        Public ReadOnly Property Clock As Double
            Get
                Return _clock
            End Get
        End Property

        Public ReadOnly Property SectorIds As IReadOnlyList(Of Integer)
            Get
                Return _sectorIds
            End Get
        End Property

        Public ReadOnly Property SectorHeaderNs As IReadOnlyList(Of Integer)
            Get
                Return _sectorNs
            End Get
        End Property

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration HasSector)
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _sectorValid.Count AndAlso _sectorValid(sectorId)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration MissingSectorCount)
        Public Overrides Function NrMissing() As Integer
            Return _sectorValid.Where(Function(v) Not v).Count()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImageTrack)
        Public Overrides Function GetImgTrack() As Byte()
            Dim bytes As New List(Of Byte)()
            For Each i In _logicalOrder
                bytes.AddRange(_sectorData(i))
                If _imgBytesPerSector.HasValue Then
                    Dim pad = _imgBytesPerSector.Value - _sectorData(i).Length
                    If pad > 0 Then
                        bytes.AddRange(Enumerable.Repeat(CByte(0), pad))
                    End If
                End If
            Next
            Return bytes.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SetImageTrack)
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim src = If(trackData, Array.Empty(Of Byte)())
            Dim totalSize As Integer
            If _imgBytesPerSector.HasValue Then
                totalSize = _sectorData.Count * _imgBytesPerSector.Value
            Else
                totalSize = _sectorSizes.Sum()
            End If
            If src.Length < totalSize Then
                src = src.Concat(Enumerable.Repeat(CByte(0), totalSize - src.Length)).ToArray()
            End If

            Dim pos = 0
            For Each i In _logicalOrder
                Dim size = _sectorSizes(i)
                Dim sector(size - 1) As Byte
                Array.Copy(src, pos, sector, 0, size)
                _sectorData(i) = sector
                _sectorValid(i) = True
                If _imgBytesPerSector.HasValue Then
                    pos += _imgBytesPerSector.Value
                Else
                    pos += size
                End If
            Next
            Return totalSize
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.decode_flux
        '
        ' Faithful port of the Python second-pass reconcile (ibm.py:657-681).
        ' Python keeps `self.raw` (an IBMTrack of decoded raw sectors) and
        ' merges good IDAMs into the predeclared sector layout in `self.sectors`
        ' (each call accumulates - sectors are NOT cleared at the top of
        ' decode_flux, so multiple calls with different PLLs are cumulative and
        ' first-good-wins). Sectors with a good IDAM CRC whose (C,H,R,N) tuple
        ' does not match any predeclared layout entry are reported via:
        '     T<cyl>.<head>: Ignoring unexpected sector C:<c> H:<h> R:<r> N:<n>
        ' (one print per unique tuple).
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim mismatchOrder As New List(Of Tuple(Of Integer, Integer, Integer, Integer))
            Dim mismatchSeen As New HashSet(Of Tuple(Of Integer, Integer, Integer, Integer))
            If String.Equals(_formatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase) Then
                DecodeMfmFlux(track, pll, mismatchOrder, mismatchSeen)
            ElseIf String.Equals(_formatName, "ibm.fm", StringComparison.OrdinalIgnoreCase) Then
                DecodeFmFlux(track, pll, mismatchOrder, mismatchSeen)
            ElseIf String.Equals(_formatName, "dec.rx02", StringComparison.OrdinalIgnoreCase) Then
                DecodeDecRx02Flux(track, pll, mismatchOrder, mismatchSeen)
            Else
                DecodeMfmFlux(track, pll, mismatchOrder, mismatchSeen)
            End If
            For Each m In mismatchOrder
                LibraryDiagnostics.EmitInfo(String.Format("T{0}.{1}: Ignoring unexpected sector C:{2} H:{3} R:{4} N:{5}",
                                                          _cyl, _head, m.Item1, m.Item2, m.Item3, m.Item4))
            Next
        End Sub

        Private Sub RecordMismatch(c As Byte, h As Byte, r As Byte, n As Byte,
                                   mismatchOrder As List(Of Tuple(Of Integer, Integer, Integer, Integer)),
                                   mismatchSeen As HashSet(Of Tuple(Of Integer, Integer, Integer, Integer)))
            Dim key = Tuple.Create(CInt(c), CInt(h), CInt(r), CInt(n))
            If mismatchSeen.Add(key) Then
                mismatchOrder.Add(key)
            End If
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DecodeMfmFlux)
        Private Sub DecodeMfmFlux(track As HasFlux, pll As Pll,
                                  mismatchOrder As List(Of Tuple(Of Integer, Integer, Integer, Integer)),
                                  mismatchSeen As HashSet(Of Tuple(Of Integer, Integer, Integer, Integer)))
            Dim flux = track.Flux()
            flux.CueAtIndex()
            Dim raw As New PllTrack(clock:=_clock, data:=flux, timePerRev:=_timePerRev, pll:=pll)
            Dim bits = raw.GetAllData().Item1

            Dim pending As ParsedIdam = Nothing
            For Each offs In FindPatternOffsets(bits, _mfmSyncPattern)
                If bits.Count < offs + 64 Then
                    Continue For
                End If
                Dim mark = DecodeMfmByte(bits, offs + 48)
                If mark = MarkIdam Then
                    If bits.Count < offs + 160 Then
                        Continue For
                    End If
                    Dim idamBytes = DecodeMfmBytes(bits, offs, 10)
                    pending = New ParsedIdam With {
                        .EndOffset = offs + 160,
                        .C = idamBytes(4),
                        .H = idamBytes(5),
                        .R = idamBytes(6),
                        .N = idamBytes(7),
                        .Crc = ComputeCrcCcittFalse(idamBytes)
                    }
                    Continue For
                End If

                If mark <> MarkDam AndAlso mark <> MarkDdam Then
                    Continue For
                End If
                If pending Is Nothing OrElse offs - pending.EndOffset > 1000 Then
                    pending = Nothing
                    Continue For
                End If

                ' IDAM CRC gate parity: Python's mfm_decode_raw appends a Sector even with
                ' a non-zero IDAM CRC, but IBMTrack_Fixed.decode_flux subsequently skips
                ' those sectors (`if r.idam.crc != 0: continue`). Net behaviour at the
                ' fixed-track level is identical: a sector slot is only populated when the
                ' IDAM CRC is good. We keep the gate here for that exact reason.
                If pending.Crc <> 0 Then
                    pending = Nothing
                    Continue For
                End If

                ' Match against predeclared layout: (c,h,r,n) must all line up. A good-CRC
                ' IDAM that misses any field is an "unexpected sector" -> warning record.
                Dim idx = _sectorIds.IndexOf(pending.R)
                Dim isMatch = idx <> -1 AndAlso _
                              pending.C = _cyl AndAlso _
                              pending.H = _expectedHeaderHead AndAlso _
                              pending.N = _sectorNs(idx)
                If Not isMatch Then
                    RecordMismatch(pending.C, pending.H, pending.R, pending.N, mismatchOrder, mismatchSeen)
                    pending = Nothing
                    Continue For
                End If

                ' First-good-wins: Python only updates s when r.dam.crc==0 AND s.dam.crc!=0.
                ' Once a sector slot is valid we never overwrite it on subsequent decode
                ' passes (cumulative-across-calls reconcile).
                If _sectorValid(idx) Then
                    pending = Nothing
                    Continue For
                End If

                Dim size = _sectorSizes(idx)
                Dim decodedByteCount = 4 + size + 2
                Dim endOffset = offs + decodedByteCount * 16
                If bits.Count < endOffset Then
                    pending = Nothing
                    Continue For
                End If

                Dim sectorBytes = DecodeMfmBytes(bits, offs, decodedByteCount)
                If ComputeCrcCcittFalse(sectorBytes) <> 0 Then
                    pending = Nothing
                    Continue For
                End If

                Dim data = sectorBytes.Skip(4).Take(size).ToArray()
                _sectorData(idx) = data
                _sectorValid(idx) = True
                pending = Nothing
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DecodeFmFlux)
        Private Sub DecodeFmFlux(track As HasFlux, pll As Pll,
                                 mismatchOrder As List(Of Tuple(Of Integer, Integer, Integer, Integer)),
                                 mismatchSeen As HashSet(Of Tuple(Of Integer, Integer, Integer, Integer)))
            Dim flux = track.Flux()
            flux.CueAtIndex()
            Dim raw As New PllTrack(clock:=_clock, data:=flux, timePerRev:=_timePerRev, pll:=pll)
            Dim bits = raw.GetAllData().Item1

            Dim pending As ParsedIdam = Nothing
            Dim idamOffsets = FindPatternOffsets(bits, _fmIdamSyncPattern).ToList()
            Dim damOffsets = FindPatternOffsets(bits, _fmDamSyncPattern) _
                .Concat(FindPatternOffsets(bits, _fmDdamSyncPattern)) _
                .Concat(FindPatternOffsets(bits, _fmDamTrs80DirSyncPattern)) _
                .OrderBy(Function(x) x).ToList()
            Dim allOffsets = idamOffsets.Concat(damOffsets).Distinct().OrderBy(Function(x) x).ToList()

            For Each offs In allOffsets
                If bits.Count < offs + 16 Then
                    Continue For
                End If
                Dim mark = DecodeMfmByte(bits, offs)
                If mark = MarkIdam Then
                    If bits.Count < offs + 7 * 16 Then
                        Continue For
                    End If
                    Dim idamBytes = DecodeMfmBytes(bits, offs, 7)
                    pending = New ParsedIdam With {
                        .EndOffset = offs + 7 * 16,
                        .C = idamBytes(1),
                        .H = idamBytes(2),
                        .R = idamBytes(3),
                        .N = idamBytes(4),
                        .Crc = ComputeCrcCcittFalse(idamBytes)
                    }
                    Continue For
                End If

                ' Python (fm_decode_raw) accepts DAM, DDAM, DAM_TRS80_DIR, and (for DEC RX02
                ' only) DAM_DEC_MMFM/DDAM_DEC_MMFM via mmfm_raw. The DEC RX02 path is handled
                ' by DecodeDecRx02Flux; here we accept the three plain-FM data marks.
                If mark <> MarkDam AndAlso mark <> MarkDdam AndAlso mark <> MarkDamTrs80Dir Then
                    Continue For
                End If
                If pending Is Nothing OrElse offs - pending.EndOffset > 1000 Then
                    pending = Nothing
                    Continue For
                End If
                If pending.Crc <> 0 Then
                    pending = Nothing
                    Continue For
                End If

                Dim idx = _sectorIds.IndexOf(pending.R)
                Dim isMatch = idx <> -1 AndAlso _
                              pending.C = _cyl AndAlso _
                              pending.H = _expectedHeaderHead AndAlso _
                              pending.N = _sectorNs(idx)
                If Not isMatch Then
                    RecordMismatch(pending.C, pending.H, pending.R, pending.N, mismatchOrder, mismatchSeen)
                    pending = Nothing
                    Continue For
                End If

                If _sectorValid(idx) Then
                    pending = Nothing
                    Continue For
                End If

                Dim size = _sectorSizes(idx)
                Dim byteCount = 1 + size + 2
                Dim endOffset = offs + byteCount * 16
                If bits.Count < endOffset Then
                    pending = Nothing
                    Continue For
                End If
                Dim sectorBytes = DecodeMfmBytes(bits, offs, byteCount)
                If ComputeCrcCcittFalse(sectorBytes) <> 0 Then
                    pending = Nothing
                    Continue For
                End If
                _sectorData(idx) = sectorBytes.Skip(1).Take(size).ToArray()
                _sectorValid(idx) = True
                pending = Nothing
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DecodeDecRx02Flux)
        Private Sub DecodeDecRx02Flux(track As HasFlux, pll As Pll,
                                      mismatchOrder As List(Of Tuple(Of Integer, Integer, Integer, Integer)),
                                      mismatchSeen As HashSet(Of Tuple(Of Integer, Integer, Integer, Integer)))
            Dim flux = track.Flux()
            flux.CueAtIndex()
            Dim fmRaw As New PllTrack(clock:=_clock, data:=flux, timePerRev:=_timePerRev, pll:=pll)
            Dim mmfmRaw As New PllTrack(clock:=_clock / 2.0, data:=flux, timePerRev:=_timePerRev, pll:=pll)
            Dim fmData = fmRaw.GetAllData()
            Dim mmData = mmfmRaw.GetAllData()
            Dim bits = fmData.Item1
            Dim times = fmData.Item2
            Dim mmBits = mmData.Item1
            Dim mmTimes = mmData.Item2

            Dim mmSyncIter = FindPatternOffsets(mmBits, _decMmfmSyncPrefixPattern).GetEnumerator()
            Dim mmOff As Integer? = Nothing
            If mmSyncIter.MoveNext() Then
                mmOff = mmSyncIter.Current
            End If
            Dim fmTime As Double = 0
            Dim prevFmOff As Integer = 0
            Dim mmTime As Double = 0
            Dim prevMmOff As Integer = 0
            Dim pending As ParsedIdam = Nothing

            For Each offsPrefix In FindPatternOffsets(bits, _fmSyncPrefixPattern)
                fmTime += SumRange(times, prevFmOff, offsPrefix)
                prevFmOff = offsPrefix
                Dim delta As Double = 0
                While mmOff.HasValue
                    mmTime += SumRange(mmTimes, prevMmOff, mmOff.Value)
                    prevMmOff = mmOff.Value
                    delta = fmTime - mmTime
                    If delta < 1.0E-5 Then
                        Exit While
                    End If
                    If mmSyncIter.MoveNext() Then
                        mmOff = mmSyncIter.Current
                    Else
                        mmOff = Nothing
                    End If
                End While
                If Not mmOff.HasValue OrElse Math.Abs(delta) > 1.0E-5 Then
                    Continue For
                End If

                Dim offs = offsPrefix + 16
                If bits.Count < offs + 16 Then
                    Continue For
                End If
                Dim mark = DecodeMfmByte(bits, offs)
                Dim clock = DecodeMfmByte(bits, offs - 1)
                If clock <> &HC7 Then
                    Continue For
                End If

                If mark = MarkIdam Then
                    Dim s = offs
                    Dim e = offs + 7 * 16
                    If bits.Count < e Then
                        Continue For
                    End If
                    Dim idamBytes = DecodeMfmBytes(bits, s, 7)
                    pending = New ParsedIdam With {
                        .EndOffset = e,
                        .C = idamBytes(1),
                        .H = idamBytes(2),
                        .R = idamBytes(3),
                        .N = idamBytes(4),
                        .Crc = ComputeCrcCcittFalse(idamBytes)
                    }
                    Continue For
                End If

                Dim isSpecialDecDam = ((mark And &HFB) = &HF9)
                If mark <> MarkDam AndAlso mark <> MarkDdam AndAlso Not isSpecialDecDam Then
                    Continue For
                End If
                If pending Is Nothing OrElse offs - pending.EndOffset > 1000 Then
                    pending = Nothing
                    Continue For
                End If
                If pending.Crc <> 0 Then
                    pending = Nothing
                    Continue For
                End If

                Dim idx = _sectorIds.IndexOf(pending.R)
                Dim isMatch = idx <> -1 AndAlso _
                              pending.C = _cyl AndAlso _
                              pending.H = _expectedHeaderHead AndAlso _
                              pending.N = _sectorNs(idx)
                If Not isMatch Then
                    RecordMismatch(pending.C, pending.H, pending.R, pending.N, mismatchOrder, mismatchSeen)
                    pending = Nothing
                    Continue For
                End If

                If _sectorValid(idx) Then
                    pending = Nothing
                    Continue For
                End If

                Dim payload As Byte()
                If isSpecialDecDam Then
                    Dim size = 128 << pending.N
                    Dim ds = mmOff.Value + 64 + 1
                    Dim de = ds + (size * 2 + 2) * 16
                    If mmBits.Count < de Then
                        pending = Nothing
                        Continue For
                    End If
                    Dim mmSeg = mmBits.Skip(ds).Take(de - ds).ToList()
                    Dim dec = DecMmfmDecode(mmSeg)
                    payload = New Byte() {mark}.Concat(dec).ToArray()
                Else
                    Dim byteCount = 1 + _sectorSizes(idx) + 2
                    Dim e = offs + byteCount * 16
                    If bits.Count < e Then
                        pending = Nothing
                        Continue For
                    End If
                    payload = DecodeMfmBytes(bits, offs, byteCount)
                End If

                If ComputeCrcCcittFalse(payload) <> 0 Then
                    pending = Nothing
                    Continue For
                End If
                _sectorData(idx) = payload.Skip(1).Take(payload.Length - 3).ToArray()
                _sectorValid(idx) = True
                pending = Nothing
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CreateMasterTrack)
        Public Overrides Function MasterTrack() As MasterTrack
            Dim mt As MasterTrack
            If String.Equals(_formatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase) Then
                mt = CreateMfmMasterTrack()
            ElseIf String.Equals(_formatName, "dec.rx02", StringComparison.OrdinalIgnoreCase) Then
                mt = CreateDecRx02MasterTrack()
            ElseIf String.Equals(_formatName, "ibm.fm", StringComparison.OrdinalIgnoreCase) Then
                mt = CreateFmMasterTrack()
            Else
                mt = CreateMfmMasterTrack()
            End If
            ' Python: track.verify = self -- attach the codec as the verifier so
            ' downstream readback can validate against the originating sector layout.
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.mfm_master_track
        Public Function MfmMasterTrack() As MasterTrack
            Return CreateMfmMasterTrack()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.fm_master_track
        Public Function FmMasterTrack() As MasterTrack
            Return CreateFmMasterTrack()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.mfm_decode_raw
        Public Function MfmDecodeRaw(track As HasFlux, Optional pll As Pll = Nothing) As Boolean
            Dim mismatchOrder As New List(Of Tuple(Of Integer, Integer, Integer, Integer))
            Dim mismatchSeen As New HashSet(Of Tuple(Of Integer, Integer, Integer, Integer))
            DecodeMfmFlux(track, pll, mismatchOrder, mismatchSeen)
            Return True
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.fm_decode_raw
        Public Function FmDecodeRaw(track As HasFlux, Optional pll As Pll = Nothing) As Boolean
            Dim mismatchOrder As New List(Of Tuple(Of Integer, Integer, Integer, Integer))
            Dim mismatchSeen As New HashSet(Of Tuple(Of Integer, Integer, Integer, Integer))
            DecodeFmFlux(track, pll, mismatchOrder, mismatchSeen)
            Return True
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.decode_raw
        Public Function DecodeRaw(track As HasFlux, mode As IbmMode, Optional pll As Pll = Nothing) As Boolean
            If mode = IbmMode.Fm Then
                Return FmDecodeRaw(track, pll)
            End If
            If mode = IbmMode.DecRx02 Then
                Dim mismatchOrder As New List(Of Tuple(Of Integer, Integer, Integer, Integer))
                Dim mismatchSeen As New HashSet(Of Tuple(Of Integer, Integer, Integer, Integer))
                DecodeDecRx02Flux(track, pll, mismatchOrder, mismatchSeen)
                Return True
            End If
            Return MfmDecodeRaw(track, pll)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.verify_track
        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.verify_track
        ' Python compares full Sector objects: idam + dam (positions, c/h/r/n,
        ' mark, data) via Sector.__eq__ (which uses TrackArea.__eq__ tolerant
        ' positional compare). VB persists only payload bytes per slot together
        ' with a per-slot valid flag, plus the IDAM (c,h,r,n) implicitly via
        ' _sectorIds + _expectedHeaderHead + _cyl + _sectorNs which match by
        ' construction. So the equivalent check is: same set of valid slots and
        ' identical decoded payload bytes per slot, which is what we do below.
        Public Overridable Function VerifyTrack(flux As Flux) As Boolean
            Dim readback = CloneForVerify()
            readback.DecodeFlux(flux)
            If readback.NrMissing() <> 0 Then
                Return False
            End If
            For i = 0 To _sectorData.Count - 1
                If _sectorValid(i) <> readback._sectorValid(i) Then
                    Return False
                End If
                If _sectorValid(i) AndAlso Not _sectorData(i).SequenceEqual(readback._sectorData(i)) Then
                    Return False
                End If
                ' IDAM equivalence (Python: Sector.idam == readback.idam)
                If _sectorIds(i) <> readback._sectorIds(i) Then
                    Return False
                End If
                If _sectorNs(i) <> readback._sectorNs(i) Then
                    Return False
                End If
            Next
            Return True
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CreateMfmMasterTrack)
        Private Function CreateMfmMasterTrack() As MasterTrack

            Const defaultGap1 As Integer = 50
            Const defaultGap2 As Integer = 22
            Const defaultGap4a As Integer = 80
            Const gapPresync As Integer = 12

            Dim gap3ByN As Integer() = New Integer() {32, 54, 84, 116, 255, 255, 255, 255}

            Dim gapByte As Byte = If(_gapByteOverride, CByte(&H4E))
            Dim gap4a As Integer = If(_gap4aOverride, defaultGap4a)
            Dim gap1 As Integer = If(_gap1Override, defaultGap1)
            Dim gap2 As Integer = If(_gap2Override, defaultGap2)

            Dim encoded As New List(Of Byte)()
            AppendEncodedRepeated(encoded, gapByte, gap4a)
            If _emitIam Then
                AppendRawMfmIamSync(encoded)
                AppendEncodedByte(encoded, &HFC)
                AppendEncodedRepeated(encoded, gapByte, gap1)
            End If

            For i = 0 To _sectorData.Count - 1
                Dim n = _sectorNs(i)
                Dim gap3 = If(_gap3Override, If(n >= 0 AndAlso n < gap3ByN.Length, gap3ByN(n), 255))

                AppendEncodedRepeated(encoded, 0, gapPresync)
                AppendRawMfmSync(encoded)
                Dim idam = New List(Of Byte) From {
                    &HFE,
                    CByte(_cyl And &HFF),
                    CByte(_expectedHeaderHead And &HFF),
                    CByte(_sectorIds(i) And &HFF),
                    CByte(n And &HFF)
                }
                Dim idamCrc = ComputeCrcCcittFalse(New Byte() {&HA1, &HA1, &HA1}.Concat(idam).ToArray())
                idam.Add(CByte((idamCrc >> 8) And &HFF))
                idam.Add(CByte(idamCrc And &HFF))
                AppendEncodedBytes(encoded, idam)

                AppendEncodedRepeated(encoded, gapByte, gap2)
                AppendEncodedRepeated(encoded, 0, gapPresync)
                AppendRawMfmSync(encoded)
                Dim dam = New List(Of Byte) From {&HFB}
                dam.AddRange(_sectorData(i))
                Dim damCrc = ComputeCrcCcittFalse(New Byte() {&HA1, &HA1, &HA1}.Concat(dam).ToArray())
                dam.Add(CByte((damCrc >> 8) And &HFF))
                dam.Add(CByte(damCrc And &HFF))
                AppendEncodedBytes(encoded, dam)
                AppendEncodedRepeated(encoded, gapByte, gap3)
            Next

            Dim targetEncodedBytes = CInt(Math.Max(1, Math.Floor((_timePerRev / _clock) / 8.0)))
            If encoded.Count < targetEncodedBytes Then
                Dim remainingDecodedGapBytes = (targetEncodedBytes - encoded.Count) \ 2
                AppendEncodedRepeated(encoded, gapByte, remainingDecodedGapBytes)
            End If

            ' Apply final NRZI pass (Python: t = mfm_encode(t)) so that data
            ' bytes with no clock bits get the appropriate clock fill between
            ' adjacent zero-data bits. Without this, synthesised tracks lack
            ' the proper clock pattern in gap regions.
            Dim mfmEncoded = MfmEncode(encoded)
            Return New MasterTrack(BytesToBits(mfmEncoded), _timePerRev)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CreateDecRx02MasterTrack)
        Private Function CreateDecRx02MasterTrack() As MasterTrack
            Const defaultGap1 As Integer = 26
            Const defaultGap2 As Integer = 11
            Const gapPresync As Integer = 6
            Const decDamMark As Byte = &HFD

            Dim gap3ByN As Integer() = New Integer() {27, 42, 58, 138, 255, 255, 255, 255}

            Dim gapByte As Byte = If(_gapByteOverride, CByte(&HFF))
            Dim gap4a As Integer = If(_gap4aOverride, 40)
            Dim gap1 As Integer = If(_gap1Override, defaultGap1)
            Dim gap2 As Integer = If(_gap2Override, defaultGap2)

            Dim encoded As New List(Of Byte)()
            Dim mmfmAreas As New List(Of Tuple(Of Byte(), Integer))()

            AppendEncodedRepeated(encoded, gapByte, gap4a)
            If _emitIam Then
                AppendRawFmIamSync(encoded)
                AppendEncodedRepeated(encoded, gapByte, gap1)
            End If

            For i = 0 To _sectorData.Count - 1
                Dim n = _sectorNs(i)
                Dim gap3 = If(_gap3Override, If(n >= 0 AndAlso n < gap3ByN.Length, gap3ByN(n), 255))

                AppendEncodedRepeated(encoded, 0, gapPresync)
                AppendRawFmSync(encoded, &HFE, &HC7)
                Dim idam = New List(Of Byte) From {
                    CByte(_cyl And &HFF),
                    CByte(_expectedHeaderHead And &HFF),
                    CByte(_sectorIds(i) And &HFF),
                    CByte(n And &HFF)
                }
                Dim idamCrc = ComputeCrcCcittFalse(New Byte() {&HFE}.Concat(idam).ToArray())
                idam.Add(CByte((idamCrc >> 8) And &HFF))
                idam.Add(CByte(idamCrc And &HFF))
                AppendEncodedBytes(encoded, idam)

                AppendEncodedRepeated(encoded, gapByte, gap2)
                AppendEncodedRepeated(encoded, 0, gapPresync)
                AppendRawFmSync(encoded, decDamMark, &HC7)

                Dim damData As New List(Of Byte)(_sectorData(i))
                Dim damCrc = ComputeCrcCcittFalse(New Byte() {decDamMark}.Concat(damData).ToArray())
                damData.Add(CByte((damCrc >> 8) And &HFF))
                damData.Add(CByte(damCrc And &HFF))

                Dim mmfm = DecMmfmEncode(damData.ToArray())
                mmfmAreas.Add(Tuple.Create(mmfm, encoded.Count))
                AppendEncodedRepeated(encoded, gapByte, 128 + 2)
                AppendEncodedRepeated(encoded, gapByte, gap3)
            Next

            Dim targetEncodedBytes = CInt(Math.Max(1, Math.Floor((_timePerRev / _clock) / 8.0)))
            If encoded.Count < targetEncodedBytes Then
                Dim remainingDecodedGapBytes = (targetEncodedBytes - encoded.Count) \ 2
                AppendEncodedRepeated(encoded, gapByte, remainingDecodedGapBytes)
            End If

            Dim fmBytes = FmEncode(encoded.ToArray())
            Dim doubled = EncodeBytes(fmBytes).ToList()
            For Each area In mmfmAreas
                Dim src = area.Item1
                Dim offset = area.Item2 * 2
                For i = 0 To src.Length - 1
                    If offset + i < doubled.Count Then
                        doubled(offset + i) = src(i)
                    End If
                Next
            Next

            Dim bitList = BytesToBits(doubled.ToArray()).ToList()
            For Each area In mmfmAreas.OrderByDescending(Function(x) x.Item2)
                bitList.Insert(area.Item2 * 16, False)
            Next
            Return New MasterTrack(bitList, _timePerRev)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CreateFmMasterTrack)
        Private Function CreateFmMasterTrack() As MasterTrack
            Const defaultGap1 As Integer = 26
            Const defaultGap2 As Integer = 11
            Const gapPresync As Integer = 6

            Dim gap3ByN As Integer() = New Integer() {27, 42, 58, 138, 255, 255, 255, 255}

            Dim gapByte As Byte = If(_gapByteOverride, CByte(&HFF))
            Dim gap4a As Integer = If(_gap4aOverride, 40)
            Dim gap1 As Integer = If(_gap1Override, defaultGap1)
            Dim gap2 As Integer = If(_gap2Override, defaultGap2)

            Dim encoded As New List(Of Byte)()
            AppendEncodedRepeated(encoded, gapByte, gap4a)
            If _emitIam Then
                AppendRawFmIamSync(encoded)
                AppendEncodedRepeated(encoded, gapByte, gap1)
            End If

            For i = 0 To _sectorData.Count - 1
                Dim n = _sectorNs(i)
                Dim gap3 = If(_gap3Override, If(n >= 0 AndAlso n < gap3ByN.Length, gap3ByN(n), 255))

                AppendEncodedRepeated(encoded, 0, gapPresync)
                AppendRawFmSync(encoded, &HFE, &HC7)
                Dim idam = New List(Of Byte) From {
                    CByte(_cyl And &HFF),
                    CByte(_expectedHeaderHead And &HFF),
                    CByte(_sectorIds(i) And &HFF),
                    CByte(n And &HFF)
                }
                Dim idamCrc = ComputeCrcCcittFalse(New Byte() {&HFE}.Concat(idam).ToArray())
                idam.Add(CByte((idamCrc >> 8) And &HFF))
                idam.Add(CByte(idamCrc And &HFF))
                AppendEncodedBytes(encoded, idam)

                AppendEncodedRepeated(encoded, gapByte, gap2)
                AppendEncodedRepeated(encoded, 0, gapPresync)
                AppendRawFmSync(encoded, &HFB, &HC7)
                Dim dam As New List(Of Byte)(_sectorData(i))
                Dim damCrc = ComputeCrcCcittFalse(New Byte() {&HFB}.Concat(dam).ToArray())
                dam.Add(CByte((damCrc >> 8) And &HFF))
                dam.Add(CByte(damCrc And &HFF))
                AppendEncodedBytes(encoded, dam)
                AppendEncodedRepeated(encoded, gapByte, gap3)
            Next

            Dim targetEncodedBytes = CInt(Math.Max(1, Math.Floor((_timePerRev / _clock) / 8.0)))
            If encoded.Count < targetEncodedBytes Then
                Dim remainingDecodedGapBytes = (targetEncodedBytes - encoded.Count) \ 2
                AppendEncodedRepeated(encoded, gapByte, remainingDecodedGapBytes)
            End If

            ' Python fm_encode: set default FM clock bits unless a special sync already set.
            For i = 0 To encoded.Count - 1
                If (encoded(i) And &HAA) = 0 Then
                    encoded(i) = CByte(encoded(i) Or &HAA)
                End If
            Next

            Return New MasterTrack(BytesToBits(encoded.ToArray()), _timePerRev)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("{0} ({1}/{2} sectors)", ModeDisplayName(_formatName), Nsec - NrMissing(), Nsec)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::Mode.__str__
        Private Shared Function ModeDisplayName(formatName As String) As String
            If String.Equals(formatName, "ibm.mfm", StringComparison.OrdinalIgnoreCase) Then
                Return "IBM MFM"
            ElseIf String.Equals(formatName, "ibm.fm", StringComparison.OrdinalIgnoreCase) Then
                Return "IBM FM"
            ElseIf String.Equals(formatName, "dec.rx02", StringComparison.OrdinalIgnoreCase) Then
                Return "DEC RX02"
            End If
            Return formatName
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::(no direct 1:1 symbol; VB helper record for intermediate IDAM parse state)
        Private Class ParsedIdam
            Public Property EndOffset As Integer
            Public Property C As Byte
            Public Property H As Byte
            Public Property R As Byte
            Public Property N As Byte
            Public Property Crc As UShort
        End Class

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BytesPerSectorToN)
        Private Shared Function BytesPerSectorToN(bytesPerSector As Integer) As Integer
            Dim n = 0
            Dim value = 128
            While value < bytesPerSector AndAlso n < 8
                value <<= 1
                n += 1
            End While
            Return n
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindPatternOffsets)
        Private Shared Iterator Function FindPatternOffsets(bits As List(Of Boolean), pattern As Boolean()) As IEnumerable(Of Integer)
            If pattern.Length = 0 OrElse bits.Count < pattern.Length Then
                Return
            End If
            For i = 0 To bits.Count - pattern.Length
                Dim matched = True
                For j = 0 To pattern.Length - 1
                    If bits(i + j) <> pattern(j) Then
                        matched = False
                        Exit For
                    End If
                Next
                If matched Then
                    Yield i
                End If
            Next
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BytesToBits)
        Private Shared Function BytesToBits(bytes As Byte()) As Boolean()
            Dim output As New List(Of Boolean)(bytes.Length * 8)
            For Each b In bytes
                For i = 7 To 0 Step -1
                    output.Add(((b >> i) And 1) = 1)
                Next
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildFmSyncPrefixPattern)
        Private Shared Function BuildFmSyncPrefixPattern() As Boolean()
            Return BitsFrom01("10101010101010101111010101")
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildDecMmfmSyncPrefixPattern)
        Private Shared Function BuildDecMmfmSyncPrefixPattern() As Boolean()
            Return BitsFrom01("0100010001000100010001000100010001010101000100010001")
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BitsFrom01)
        Private Shared Function BitsFrom01(spec As String) As Boolean()
            Dim bits As New List(Of Boolean)(spec.Length)
            For Each ch In spec
                bits.Add(ch = "1"c)
            Next
            Return bits.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SumRange)
        Private Shared Function SumRange(values As List(Of Double), startIdx As Integer, endExclusive As Integer) As Double
            If endExclusive <= startIdx Then
                Return 0
            End If
            Dim acc As Double = 0
            For i = startIdx To endExclusive - 1
                acc += values(i)
            Next
            Return acc
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendEncodedRepeated)
        Private Shared Sub AppendEncodedRepeated(output As List(Of Byte), value As Byte, count As Integer)
            For i = 1 To count
                AppendEncodedByte(output, value)
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendEncodedBytes)
        Private Shared Sub AppendEncodedBytes(output As List(Of Byte), values As IEnumerable(Of Byte))
            For Each b In values
                AppendEncodedByte(output, b)
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendEncodedByte)
        Private Shared Sub AppendEncodedByte(output As List(Of Byte), value As Byte)
            Dim word As UShort = 0US
            For i = 7 To 0 Step -1
                word = CUShort((word << 2) Or ((value >> i) And 1))
            Next
            output.Add(CByte((word >> 8) And &HFF))
            output.Add(CByte(word And &HFF))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeBytes)
        Private Shared Function EncodeBytes(values As IEnumerable(Of Byte)) As Byte()
            Dim output As New List(Of Byte)()
            For Each b In values
                AppendEncodedByte(output, b)
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FmEncode)
        Private Shared Function FmEncode(values As IEnumerable(Of Byte)) As Byte()
            Dim bytes = values.ToArray()
            For i = 0 To bytes.Length - 1
                If (bytes(i) And &HAA) = 0 Then
                    bytes(i) = CByte(bytes(i) Or &HAA)
                End If
            Next
            Return bytes
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration MfmEncode)
        Private Shared Function MfmEncode(values As IEnumerable(Of Byte)) As Byte()
            Dim output As New List(Of Byte)()
            Dim y As Integer = 0
            For Each x In values
                y = ((y << 8) Or x) And &HFFFF
                If (x And &HAA) = 0 Then
                    y = y Or ((Not ((y >> 1) Or (y << 1))) And &HAAAA)
                End If
                y = y And &HFF
                output.Add(CByte(y))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendRawMfmSync)
        Private Shared Sub AppendRawMfmSync(output As List(Of Byte))
            output.AddRange(New Byte() {&H44, &H89, &H44, &H89, &H44, &H89})
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendRawMfmIamSync)
        Private Shared Sub AppendRawMfmIamSync(output As List(Of Byte))
            output.AddRange(New Byte() {&H52, &H24, &H52, &H24, &H52, &H24})
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendRawFmIamSync)
        Private Shared Sub AppendRawFmIamSync(output As List(Of Byte))
            AppendRawFmSync(output, &HFC, &HD7)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendRawFmSync)
        Private Shared Sub AppendRawFmSync(output As List(Of Byte), data As Byte, clock As Byte)
            Dim word = BuildSyncWord(data, clock)
            output.Add(CByte((word >> 8) And &HFF))
            output.Add(CByte(word And &HFF))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SyncWordBits)
        Private Shared Function SyncWordBits(data As Byte, clock As Byte) As Boolean()
            Dim word = BuildSyncWord(data, clock)
            Return BytesToBits(New Byte() {CByte((word >> 8) And &HFF), CByte(word And &HFF)})
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildSyncWord)
        Private Shared Function BuildSyncWord(data As Byte, clock As Byte) As UShort
            Dim word As UShort = 0US
            For i = 7 To 0 Step -1
                word = CUShort((word << 1) Or ((clock >> i) And 1))
                word = CUShort((word << 1) Or ((data >> i) And 1))
            Next
            Return word
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeMfmByte)
        Private Shared Function DecodeMfmByte(bits As List(Of Boolean), offset As Integer) As Byte
            Return DecodeMfmBytes(bits, offset, 1)(0)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeMfmBytes)
        Private Shared Function DecodeMfmBytes(bits As List(Of Boolean), offset As Integer, byteCount As Integer) As Byte()
            Dim output(byteCount - 1) As Byte
            For i = 0 To byteCount - 1
                Dim word As UShort = 0US
                For j = 0 To 15
                    word = CUShort((word << 1) Or If(bits(offset + i * 16 + j), 1US, 0US))
                Next
                output(i) = DecodeMfmWord(word)
            Next
            Return output
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecMmfmDecode)
        Friend Shared Function DecMmfmDecode(bits As List(Of Boolean)) As Byte()
            Dim work = bits.ToList()
            For i = 0 To work.Count - 3
                If i Mod 2 = 1 AndAlso Not work(i) AndAlso Not work(i + 1) AndAlso Not work(i + 2) Then
                    work(i) = True
                    work(i + 1) = False
                    work(i + 2) = True
                End If
            Next
            Return DecodeMfmBytes(work, 0, work.Count \ 16)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecMmfmEncode)
        Friend Shared Function DecMmfmEncode(pre As Byte()) As Byte()
            Dim preBits = BytesToBits(pre).ToList()
            Dim postBits = BytesToBits(MfmEncode(EncodeBytes(pre))).ToList()
            Dim searchPattern = BitsFrom01("011110")
            Dim replacement = BitsFrom01("01000100010")
            For Each x In FindPatternOffsets(preBits, searchPattern)
                Dim start = x * 2 + 1
                If start + replacement.Length <= postBits.Count Then
                    For i = 0 To replacement.Length - 1
                        postBits(start + i) = replacement(i)
                    Next
                End If
            Next
            Return BitsToBytes(postBits)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BitsToBytes)
        Private Shared Function BitsToBytes(bits As IEnumerable(Of Boolean)) As Byte()
            Dim list = bits.ToList()
            Dim pad = (8 - (list.Count Mod 8)) Mod 8
            For i = 1 To pad
                list.Add(False)
            Next
            Dim output As New List(Of Byte)(list.Count \ 8)
            For i = 0 To list.Count - 1 Step 8
                Dim b As Integer = 0
                For j = 0 To 7
                    b = (b << 1) Or If(list(i + j), 1, 0)
                Next
                output.Add(CByte(b))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeMfmWord)
        Private Shared Function DecodeMfmWord(word As UShort) As Byte
            Dim index As Integer = word And &H5555US
            Dim y = (index + (index >> 1)) And &H3333
            y = (y + (y >> 2)) And &HF0F
            y = (y + (y >> 4)) And &HFF
            Return CByte(y)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeCrcCcittFalse)
        Private Shared Function ComputeCrcCcittFalse(data As Byte()) As UShort
            Dim crc As UInteger = &HFFFFUI
            For Each b In data
                crc = crc Xor CUInt(b) << 8
                For i = 0 To 7
                    If (crc And &H8000UI) <> 0UI Then
                        crc = ((crc << 1) Xor &H1021UI) And &HFFFFUI
                    Else
                        crc = (crc << 1) And &HFFFFUI
                    End If
                Next
            Next
            Return CUShort(crc And &HFFFFUI)
        End Function

        Private Function CloneForVerify() As IbmTrackFixed
            Return New IbmTrackFixed(_formatName,
                                     _cyl,
                                     _head,
                                     _sectorSizes.ToList(),
                                     _sectorNs.ToList(),
                                     _sectorIds.ToList(),
                                     _expectedHeaderHead,
                                     _imgBytesPerSector,
                                     _timePerRev,
                                     _clock,
                                     _emitIam,
                                     _gap1Override,
                                     _gap2Override,
                                     _gap3Override,
                                     _gap4aOverride,
                                     _gapByteOverride,
                                     _oversized)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack
    Public Class IbmTrack
        Inherits IbmTrackFixed

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.__init__
        Public Sub New(formatName As String,
                       cyl As Integer,
                       head As Integer,
                       sectorSizes As List(Of Integer),
                       sectorHeaderNs As List(Of Integer),
                       sectorIds As List(Of Integer),
                       expectedHeaderHead As Integer,
                       imgBytesPerSector As Integer?,
                       timePerRev As Double,
                       clock As Double,
                       emitIam As Boolean,
                       gap1Override As Integer?,
                       gap2Override As Integer?,
                       gap3Override As Integer?,
                       gap4aOverride As Integer?,
                       gapByteOverride As Byte?)
            MyBase.New(formatName, cyl, head, sectorSizes, sectorHeaderNs, sectorIds, expectedHeaderHead, imgBytesPerSector, timePerRev, clock, emitIam, gap1Override, gap2Override, gap3Override, gap4aOverride, gapByteOverride)
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.nsec
        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return MyBase.Nsec
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.summary_string
        Public Overrides Function SummaryString() As String
            Return MyBase.SummaryString()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return MyBase.HasSec(sectorId)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return MyBase.NrMissing()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Return MyBase.SetImgTrack(trackData)
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Return MyBase.GetImgTrack()
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            MyBase.DecodeFlux(track, pll)
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Return MyBase.MasterTrack()
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Empty
    Public Class IbmTrackEmpty
        Inherits IbmTrack

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Empty.__init__
        Public Sub New()
            MyBase.New("ibm.mfm",
                       0,
                       0,
                       New List(Of Integer)(),
                       New List(Of Integer)(),
                       New List(Of Integer)(),
                       0,
                       Nothing,
                       0.2,
                       2.0E-6,
                       True,
                       Nothing,
                       Nothing,
                       Nothing,
                       Nothing,
                       Nothing)
        End Sub

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Empty.summary_string
        Public Overrides Function SummaryString() As String
            Return "Empty IBM Track"
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Empty.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Throw New FatalException("ibm.scan: Cannot handle IMG input data")
        End Function
    End Class

End Namespace
