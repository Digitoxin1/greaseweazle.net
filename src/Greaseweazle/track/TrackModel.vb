Imports System.Diagnostics
Imports System.Text

Namespace Greaseweazle.Core

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration Pll)
    ' Python map: src/greaseweazle/track.py::PLL
    Public Class Pll

        ' Python map: src/greaseweazle/track.py::PLL.__init__
        Public Sub New(pllSpec As String)
            PeriodAdjPct = 5
            PhaseAdjPct = 60
            LowpassThresh = Nothing

            For Each part In pllSpec.Split(":"c)
                Dim kv = part.Split("="c)
                If kv.Length <> 2 Then
                    Throw New ArgumentException("Invalid PLL spec segment.")
                End If
                Select Case kv(0)
                    Case "period"
                        PeriodAdjPct = Integer.Parse(kv(1), Globalization.CultureInfo.InvariantCulture)
                    Case "phase"
                        PhaseAdjPct = Integer.Parse(kv(1), Globalization.CultureInfo.InvariantCulture)
                    Case "lowpass"
                        LowpassThresh = Double.Parse(kv(1), Globalization.CultureInfo.InvariantCulture) / 1000000.0
                    Case Else
                        Throw New ArgumentException("Unknown PLL key.")
                End Select
            Next
        End Sub

        Public Property PeriodAdjPct As Integer
        Public Property PhaseAdjPct As Integer
        Public Property LowpassThresh As Nullable(Of Double)

        ' Python map: src/greaseweazle/track.py::PLL.__str__
        Public Overrides Function ToString() As String
            ' Python's f-string `f'{x:.2f}'` always emits the C-locale decimal
            ' separator; force InvariantCulture so non-English Windows locales
            ' don't render `lowpass_thresh=1,50` instead of `1.50`.
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim s = String.Format(ci, "PLL: period_adj={0}% phase_adj={1}%",
                                  PeriodAdjPct, PhaseAdjPct)
            If LowpassThresh.HasValue Then
                s &= String.Format(ci, " lowpass_thresh={0:F2}", LowpassThresh.Value * 1000000.0)
            End If
            Return s
        End Function
    End Class

    ' Python map: src/greaseweazle/track.py::plls
    Public NotInheritable Class Plls

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB default profile holder constructor)
        Private Sub New()
        End Sub

        Public Shared ReadOnly Property Values As IReadOnlyList(Of Pll) =
            New List(Of Pll) From {
                New Pll("period=5:phase=60"),
                New Pll("period=1:phase=10")
            }

    End Class

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration PrecompType)
    Public NotInheritable Class PrecompType
        Public Const Mfm As Integer = 0
        Public Const Fm As Integer = 1
        Public Const Gcr As Integer = 2
    End Class

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration Precomp)
    ' Python map: src/greaseweazle/track.py::Precomp
    Public Class Precomp

        Private Shared ReadOnly TypeString As String() = {"MFM", "FM", "GCR"}

        ' Python map: src/greaseweazle/track.py::Precomp.__init__
        Public Sub New(typeValue As Integer, ns As Double)
            Me.Type = typeValue
            Me.Ns = ns
        End Sub

        Public Property [Type] As Integer
        Public Property Ns As Double

        ' Python map: src/greaseweazle/track.py::Precomp.__str__
        Public Overrides Function ToString() As String
            ' Python uses "%d" % self.ns which truncates toward zero (like int()).
            Return String.Format("Precomp: {0}, {1}ns", TypeString([Type]), CInt(Fix(Ns)))
        End Function

        ' Python map: src/greaseweazle/track.py::Precomp.apply
        Public Sub Apply(bits As List(Of Boolean), bitTicks As List(Of Double), scale As Double)
            Dim adjustment = Ns * scale
            If [Type] = PrecompType.Mfm Then
                For Each i In BitPatternSearch(bits, "10100")
                    bitTicks(i + 2) -= adjustment
                    bitTicks(i + 3) += adjustment
                Next
                For Each i In BitPatternSearch(bits, "00101")
                    bitTicks(i + 2) += adjustment
                    bitTicks(i + 3) -= adjustment
                Next
            End If

            For Each i In BitPatternSearch(bits, "110")
                bitTicks(i + 1) -= adjustment
                bitTicks(i + 2) += adjustment
            Next
            For Each i In BitPatternSearch(bits, "011")
                bitTicks(i + 1) += adjustment
                bitTicks(i + 2) -= adjustment
            Next
        End Sub

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper for precomp pattern matching)
        Private Shared Function BitPatternSearch(bits As List(Of Boolean), pattern As String) As IEnumerable(Of Integer)
            Dim p = pattern.Select(Function(ch) ch = "1"c).ToArray()
            Dim result As New List(Of Integer)()
            If bits.Count < p.Length Then
                Return result
            End If
            For i = 0 To bits.Count - p.Length
                Dim matched = True
                For j = 0 To p.Length - 1
                    If bits(i + j) <> p(j) Then
                        matched = False
                        Exit For
                    End If
                Next
                If matched Then
                    result.Add(i)
                End If
            Next
            Return result
        End Function
    End Class

    ' Python map: src/greaseweazle/track.py::HasVerify
    Public Interface HasVerify
        ' Python map: src/greaseweazle/track.py::HasVerify.verify_revs
        ReadOnly Property VerifyRevs As Double
        ' Python map: src/greaseweazle/track.py::HasVerify.verify_track
        Function VerifyTrack(flux As Flux) As Boolean
    End Interface

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB interface declaration FluxDecoder)
    Public Interface FluxDecoder
        ' Python map: src/greaseweazle/track.py::flux_to_bitcells
        Sub FluxToBitcells(bitArray As List(Of Boolean),
                           timeArray As List(Of Double),
                           revolutions As List(Of Integer),
                           indexIter As IEnumerator(Of Double),
                           fluxIter As IEnumerator(Of Double),
                           freq As Double,
                           clockCentre As Double,
                           clockMin As Double,
                           clockMax As Double,
                           pllPeriodAdjust As Double,
                           pllPhaseAdjust As Double)
    End Interface

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration ManagedFluxDecoder)
    Public Class ManagedFluxDecoder
        Implements FluxDecoder

        ' Python map: src/greaseweazle/track.py::flux_to_bitcells
        Public Sub FluxToBitcells(bitArray As List(Of Boolean),
                                  timeArray As List(Of Double),
                                  revolutions As List(Of Integer),
                                  indexIter As IEnumerator(Of Double),
                                  fluxIter As IEnumerator(Of Double),
                                  freq As Double,
                                  clockCentre As Double,
                                  clockMin As Double,
                                  clockMax As Double,
                                  pllPeriodAdjust As Double,
                                  pllPhaseAdjust As Double) Implements FluxDecoder.FluxToBitcells
            Greaseweazle.Optimised.OptimizedFlux.FluxToBitcells(bitArray, timeArray, revolutions, indexIter, fluxIter,
                                                                 freq, clockCentre, clockMin, clockMax,
                                                                 pllPeriodAdjust, pllPhaseAdjust)
        End Sub
    End Class

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration MasterTrack)
    ' Python map: src/greaseweazle/track.py::MasterTrack
    Public Class MasterTrack
        Implements HasFlux

        ' Python map: src/greaseweazle/track.py::MasterTrack.__init__
        Public Sub New(bits As IEnumerable(Of Boolean),
                       timePerRev As Double,
                       Optional bitTicks As IEnumerable(Of Double) = Nothing,
                       Optional splice As Integer = 0,
                       Optional weak As IEnumerable(Of Tuple(Of Integer, Integer)) = Nothing,
                       Optional hardsectorBits As IEnumerable(Of Integer) = Nothing)
            Me.Bits = New List(Of Boolean)(bits)
            Me.TimePerRev = timePerRev
            Me.BitTicks = If(bitTicks Is Nothing, Nothing, New List(Of Double)(bitTicks))
            Me.Splice = splice
            Me.WeakRanges = If(weak Is Nothing,
                               New List(Of Tuple(Of Integer, Integer))(),
                               New List(Of Tuple(Of Integer, Integer))(weak))
            Me.HardsectorBits = If(hardsectorBits Is Nothing,
                                   Nothing,
                                   New List(Of Integer)(hardsectorBits))
            ForceRandomWeak = True
        End Sub

        ' Python map: src/greaseweazle/track.py::MasterTrack.__init__
        ' Overload for raw byte input (Python accepts Union[bitarray, bytes]).
        Public Sub New(bytes As Byte(),
                       timePerRev As Double,
                       Optional bitTicks As IEnumerable(Of Double) = Nothing,
                       Optional splice As Integer = 0,
                       Optional weak As IEnumerable(Of Tuple(Of Integer, Integer)) = Nothing,
                       Optional hardsectorBits As IEnumerable(Of Integer) = Nothing)
            Me.New(BytesToBoolList(bytes), timePerRev, bitTicks, splice, weak, hardsectorBits)
        End Sub

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper for bytes->bits expansion)
        Private Shared Function BytesToBoolList(bytes As Byte()) As IEnumerable(Of Boolean)
            Dim count = If(bytes Is Nothing, 0, bytes.Length)
            Dim result As New List(Of Boolean)(count * 8)
            If bytes IsNot Nothing Then
                For Each b In bytes
                    For i = 7 To 0 Step -1
                        result.Add(((b >> i) And 1) = 1)
                    Next
                Next
            End If
            Return result
        End Function

        Public Property Verify As HasVerify
        Public Property Bits As List(Of Boolean)
        Public Property TimePerRev As Double
        Public Property BitTicks As List(Of Double)
        Public Property Splice As Integer
        Public Property WeakRanges As List(Of Tuple(Of Integer, Integer))
        Public Property Precomp As Precomp
        Public Property ForceRandomWeak As Boolean
        Public Property HardsectorBits As List(Of Integer)

        ' Python map: src/greaseweazle/track.py::MasterTrack.bitrate
        Public ReadOnly Property Bitrate As Double
            Get
                Return Bits.Count / TimePerRev
            End Get
        End Property

        ' Python map: src/greaseweazle/track.py::MasterTrack.scale
        Public Sub Scale(factor As Double)
            TimePerRev *= factor
        End Sub

        ' Python map: src/greaseweazle/track.py::MasterTrack.__str__
        Public Overrides Function ToString() As String
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim sb As New StringBuilder()
            sb.Append(vbLf)
            sb.Append(String.Format(ci, "Master Track: splice @ {0}", Splice))
            sb.Append(vbLf)
            sb.Append(String.Format(ci, " {0} bits, {1:F1} kbit/s", Bits.Count, Bitrate / 1000.0))
            If BitTicks IsNot Nothing Then
                sb.Append(" (variable)")
            End If
            sb.Append(vbLf)
            sb.Append(String.Format(ci, " {0:F1} ms / rev ({1:F1} rpm)", TimePerRev * 1000.0, 60.0 / TimePerRev))
            If WeakRanges.Count > 0 Then
                sb.Append(vbLf)
                sb.Append(String.Format(ci, " {0} weak range{1}: {2} bits",
                                        WeakRanges.Count,
                                        If(WeakRanges.Count > 1, "s", String.Empty),
                                        String.Join(", ", WeakRanges.Select(Function(x) x.Item2.ToString(ci)))))
            End If
            Return sb.ToString()
        End Function

        ' Python map: src/greaseweazle/track.py::MasterTrack.summary_string
        Public Function SummaryString() As String Implements HasFlux.SummaryString
            ' Force InvariantCulture so {n:F1} matches Python's locale-independent
            ' `%.1f` output regardless of the host Windows locale.
            Dim s = String.Format(Globalization.CultureInfo.InvariantCulture,
                                  "Bitcells ({0} bits, {1:F1} kbit/s, {2:F1} rpm",
                                  Bits.Count, Bitrate / 1000.0, 60.0 / TimePerRev)
            If BitTicks IsNot Nothing Then
                s &= ", variable"
            End If
            If WeakRanges.Count > 0 Then
                s &= ", weak"
            End If
            s &= ")"
            Return s
        End Function

        ' Python map: src/greaseweazle/track.py::MasterTrack.reverse
        Public Sub Reverse()
            Dim bitLength = Bits.Count
            If bitLength = 0 Then
                Return
            End If
            Bits.Reverse()
            If BitTicks IsNot Nothing Then
                BitTicks.Reverse()
            End If
            Splice = ((-Splice Mod bitLength) + bitLength) Mod bitLength
            WeakRanges = WeakRanges.Select(Function(x)
                                               Dim start = ((-x.Item1 Mod bitLength) + bitLength) Mod bitLength
                                               Return Tuple.Create(start, x.Item2)
                                           End Function).ToList()
            If HardsectorBits IsNot Nothing Then
                HardsectorBits.Reverse()
            End If
        End Sub

        ' Python map: src/greaseweazle/track.py::MasterTrack.flux
        Public Function Flux() As Flux Implements HasFlux.Flux
            Dim output = BuildFlux(forWriteout:=False, cueAtIndex:=True, revs:=Nothing)
            Return output.Item1
        End Function

        ' Python map: src/greaseweazle/track.py::MasterTrack.flux
        Public Function Flux(Optional revs As Nullable(Of Integer) = Nothing) As Flux
            Dim output = BuildFlux(forWriteout:=False, cueAtIndex:=True, revs:=revs)
            Return output.Item1
        End Function

        ' Python map: src/greaseweazle/track.py::MasterTrack.flux_for_writeout
        Public Function FluxForWriteout(cueAtIndex As Boolean) As WriteoutFlux Implements HasFlux.FluxForWriteout
            Dim output = BuildFlux(forWriteout:=True, cueAtIndex:=cueAtIndex, revs:=Nothing)
            Return output.Item2
        End Function

        ' Python map: src/greaseweazle/track.py::MasterTrack._flux
        Private Function BuildFlux(forWriteout As Boolean,
                                   cueAtIndex As Boolean,
                                   revs As Nullable(Of Integer)) As Tuple(Of Flux, WriteoutFlux)
            ' Python: assert for_writeout when not cue_at_index. Internal misuse only.
            Debug.Assert(forWriteout OrElse cueAtIndex,
                         "MasterTrack.BuildFlux requires for_writeout when not cue_at_index")
            Dim bits = New List(Of Boolean)(Me.Bits)
            Dim bitLength = bits.Count
            Dim bitTicks = If(Me.BitTicks Is Nothing, Enumerable.Repeat(1.0, bitLength).ToList(), New List(Of Double)(Me.BitTicks))
            Dim ticksToIndex = bitTicks.Sum()

            For Each range In WeakRanges
                Dim s = range.Item1
                Dim n = range.Item2
                If n < 2 Then
                    Continue For
                End If
                Dim e = s + n
                ' Python: assert 0 <= s < e <= bitlen on each weak range.
                ErrorHandling.Check(s >= 0 AndAlso s < e AndAlso e <= bitLength,
                                    String.Format("MasterTrack: weak range [{0},{1}) out of bounds (bitlen={2})", s, e, bitLength))
                Dim pattern As List(Of Boolean)
                If n < 400 OrElse ForceRandomWeak Then
                    pattern = ByteToBits(&H80).Concat(ByteToBits(&H00)).Concat(ByteToBits(&H00)).Concat(ByteToBits(&H00)).ToList()
                    Dim rep = RepeatPattern(pattern, n)
                    For i = 0 To n - 1
                        bits(s + i) = rep(i)
                    Next
                Else
                    pattern = ByteToBits(&H12).Concat(ByteToBits(&HA5)).ToList()
                    Dim rep = RepeatPattern(pattern, n)
                    For i = 0 To n - 1
                        bits(s + i) = rep(i)
                    Next
                    For i = 0 To n - 11 Step 16
                        Dim x = bitTicks(s + i + 10)
                        Dim y = bitTicks(s + i + 11)
                        bitTicks(s + i + 10) = x + y * 0.5
                        bitTicks(s + i + 11) = y * 0.5
                    Next
                End If

                bits(s) = Not bits((s - 1 + bitLength) Mod bitLength)
                bits(e - 1) = Not (bits(e - 2) Or bits(e Mod bitLength))
            Next

            Dim spliceAtIndex As Boolean
            If cueAtIndex Then
                Dim index = ((-Splice Mod bitLength) + bitLength) Mod bitLength
                If index <> 0 Then
                    bits = Rotate(bits, index)
                    bitTicks = Rotate(bitTicks, index)
                End If
                spliceAtIndex = (index < 4 OrElse bitLength - index < 4)
            Else
                spliceAtIndex = False
            End If

            If Not forWriteout Then
                ' no extension
            ElseIf Not cueAtIndex Then
                Dim pos = 4
                Dim rep = bitLength \ (10 * 32)
                bitTicks = bitTicks.Skip(pos).Take(32).SelectMany(Function(x) Enumerable.Repeat(x, rep)).Concat(bitTicks.Skip(pos)).ToList()
                bits = bits.Skip(pos).Take(32).SelectMany(Function(x) Enumerable.Repeat(x, rep)).Concat(bits.Skip(pos)).ToList()
            ElseIf spliceAtIndex Then
                Dim pos = ((Splice - 4) Mod bitLength + bitLength) Mod bitLength
                Dim rep = bitLength \ (10 * 32)
                bitTicks = bitTicks.Take(pos).Concat(bitTicks.Skip(Math.Max(pos - 32, 0)).Take(32).SelectMany(Function(x) Enumerable.Repeat(x, rep))).ToList()
                bits = bits.Take(pos).Concat(bits.Skip(Math.Max(pos - 32, 0)).Take(32).SelectMany(Function(x) Enumerable.Repeat(x, rep))).ToList()
            Else
                ' Python: bits += bits[:self.splice-4]
                ' When splice<4, splice-4 is negative and Python's slice [:k] with k<0
                ' returns all-but-last-|k| elements; VB Take() returns 0 for k<0.
                Dim bitTicksLen = bitTicks.Count
                Dim bitsLen = bits.Count
                Dim takeAmount As Integer
                If Splice - 4 >= 0 Then
                    takeAmount = Math.Min(Splice - 4, bitsLen)
                Else
                    takeAmount = Math.Max(bitsLen + (Splice - 4), 0)
                End If
                bitTicks = bitTicks.Concat(bitTicks.Take(Math.Min(takeAmount, bitTicksLen))).ToList()
                bits = bits.Concat(bits.Take(takeAmount)).ToList()
                Dim pos = Splice + 4
                Dim fillPattern = bits.Skip(pos).Take(32).ToList()
                While pos >= 32
                    pos -= 32
                    For i = 0 To Math.Min(31, fillPattern.Count - 1)
                        bits(pos + i) = fillPattern(i)
                    Next
                End While
            End If

            If forWriteout AndAlso Precomp IsNot Nothing Then
                Precomp.Apply(bits, bitTicks, ticksToIndex / (TimePerRev * 1000000000.0))
            End If

            Dim fluxList As New List(Of Double)()
            Dim fluxTicks As Double = 0
            For i = 0 To bits.Count - 1
                fluxTicks += bitTicks(i)
                If bits(i) Then
                    fluxList.Add(fluxTicks)
                    fluxTicks = 0
                End If
            Next

            If forWriteout Then
                If fluxTicks <> 0 Then
                    fluxList.Add(fluxTicks)
                End If
                Dim w = New WriteoutFlux(ticksToIndex, fluxList, ticksToIndex / TimePerRev, cueAtIndex, spliceAtIndex)
                Return Tuple.Create(Of Flux, WriteoutFlux)(Nothing, w)
            End If

            Dim indexList As New List(Of Double) From {ticksToIndex}
            Dim resolvedRevs = If(revs.HasValue, revs.Value, If(spliceAtIndex, 1, 2))
            If resolvedRevs > 1 Then
                Dim original = fluxList.ToList()
                For i = 0 To resolvedRevs - 2
                    fluxList = original.Concat({fluxTicks + fluxList(0)}).Concat(fluxList.Skip(1)).ToList()
                Next
                indexList = Enumerable.Repeat(ticksToIndex, resolvedRevs).ToList()
            End If

            Dim flux = New Flux(indexList, fluxList, ticksToIndex / TimePerRev, True)
            ' Python: flux.splice = sum(bit_ticks[:self.splice]) -- uses Python negative-slice
            ' semantics for negative splice (returns all-but-last-|splice| elements).
            flux.Splice = SumPythonSlicePrefix(bitTicks, Splice)
            Return Tuple.Create(flux, CType(Nothing, WriteoutFlux))
        End Function

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper for Python `sum(seq[:k])` semantics)
        Private Shared Function SumPythonSlicePrefix(values As IList(Of Double), k As Integer) As Double
            Dim count = values.Count
            Dim takeCount As Integer
            If k >= 0 Then
                takeCount = Math.Min(k, count)
            Else
                takeCount = Math.Max(0, count + k)
            End If
            Dim total As Double = 0.0
            For i = 0 To takeCount - 1
                total += values(i)
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper for rotation)
        Private Shared Function Rotate(Of T)(values As List(Of T), index As Integer) As List(Of T)
            Return values.Skip(index).Concat(values.Take(index)).ToList()
        End Function

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper for weak-pattern repeat)
        Private Shared Function RepeatPattern(pattern As List(Of Boolean), length As Integer) As List(Of Boolean)
            Dim output As New List(Of Boolean)(length)
            While output.Count < length
                output.AddRange(pattern)
            End While
            If output.Count > length Then
                output = output.Take(length).ToList()
            End If
            Return output
        End Function

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper for bit expansion)
        Private Shared Function ByteToBits(value As Byte) As IEnumerable(Of Boolean)
            Dim bits As New List(Of Boolean)(8)
            For i = 7 To 0 Step -1
                bits.Add(((value >> i) And 1) = 1)
            Next
            Return bits
        End Function
    End Class

    ' Python map: src/greaseweazle/track.py::PLLRevolution
    Public Class PllRevolution
        ' Python map: src/greaseweazle/track.py::PLLRevolution.__init__
        Public Sub New(nrBits As Integer, Optional hardsectorBits As IEnumerable(Of Integer) = Nothing)
            Me.NrBits = nrBits
            Me.HardsectorBits = If(hardsectorBits Is Nothing, Nothing, New List(Of Integer)(hardsectorBits))
        End Sub

        ' Python map: src/greaseweazle/track.py::PLLRevolution.nr_bits
        Public Property NrBits As Integer
        ' Python map: src/greaseweazle/track.py::PLLRevolution.hardsector_bits
        Public Property HardsectorBits As List(Of Integer)
    End Class

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration PllTrack)
    ' Python map: src/greaseweazle/track.py::PLLTrack
    Public Class PllTrack

        ' Python map: src/greaseweazle/track.py::PLLTrack.__init__
        Public Sub New(clock As Double,
                       data As HasFlux,
                       Optional timePerRev As Nullable(Of Double) = Nothing,
                       Optional pll As Pll = Nothing,
                       Optional lowpassThresh As Nullable(Of Double) = Nothing)
            Me.Clock = clock
            Me.TimePerRev = timePerRev
            ClockMaxAdj = 0.1

            Dim activePll = If(pll, Plls.Values(0))
            PllPeriodAdj = activePll.PeriodAdjPct / 100.0
            PllPhaseAdj = activePll.PhaseAdjPct / 100.0
            Me.LowpassThresh = If(activePll.LowpassThresh.HasValue, activePll.LowpassThresh, lowpassThresh)

            BitArray = New List(Of Boolean)()
            TimeArray = New List(Of Double)()
            Revolutions = New List(Of PllRevolution)()
            ImportFluxData(data)
        End Sub

        Public Property Clock As Double
        Public Property TimePerRev As Nullable(Of Double)
        Public Property ClockMaxAdj As Double
        Public Property PllPeriodAdj As Double
        Public Property PllPhaseAdj As Double
        Public Property LowpassThresh As Nullable(Of Double)
        Public Property BitArray As List(Of Boolean)
        Public Property TimeArray As List(Of Double)
        Public Property Revolutions As List(Of PllRevolution)

        ' Python map: src/greaseweazle/track.py::PLLTrack.__str__
        Public Overrides Function ToString() As String
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim sb As New StringBuilder()
            sb.Append(vbLf)
            sb.Append(String.Format(ci, "Raw Track: {0} revolutions", Revolutions.Count))
            sb.Append(vbLf)
            For rev = 0 To Revolutions.Count - 1
                Dim revolution = GetRevolution(rev).Item1
                sb.Append(String.Format(ci, "Revolution {0} ({1} bits): ", rev, revolution.Count))
                sb.Append(BitsToHexLiteral(revolution))
                sb.Append(vbLf)
            Next

            Dim tailStart = Revolutions.Sum(Function(x) x.NrBits)
            Dim tail = BitArray.Skip(tailStart).ToList()
            sb.Append(String.Format(ci, "Tail ({0} bits): ", tail.Count))
            sb.Append(BitsToHexLiteral(tail))
            ' Python returns s[:-1] which strips the trailing newline left by the tail line.
            Return sb.ToString()
        End Function

        ' Python map: src/greaseweazle/track.py::PLLTrack.get_revolution
        Public Function GetRevolution(index As Integer) As Tuple(Of List(Of Boolean), List(Of Double))
            Dim start = Revolutions.Take(index).Sum(Function(x) x.NrBits)
            Dim count = Revolutions(index).NrBits
            Return Tuple.Create(BitArray.Skip(start).Take(count).ToList(),
                                TimeArray.Skip(start).Take(count).ToList())
        End Function

        ' Python map: src/greaseweazle/track.py::PLLTrack.get_all_data
        ' Python returns the live bitarray and timearray references (not copies).
        ' Callers may mutate the result and have changes reflected in the track.
        Public Function GetAllData() As Tuple(Of List(Of Boolean), List(Of Double))
            Return Tuple.Create(BitArray, TimeArray)
        End Function

        ' Python map: src/greaseweazle/track.py::PLLTrack.import_flux_data
        Public Sub ImportFluxData(data As HasFlux)
            Dim flux = data.Flux()
            Dim freq = flux.SampleFreq
            If TimePerRev.HasValue Then
                freq *= flux.TimePerRev / TimePerRev.Value
            End If

            Dim clockCentre = Clock
            Dim clockMin = Clock * (1 - ClockMaxAdj)
            Dim clockMax = Clock * (1 + ClockMaxAdj)

            Dim indexValues = flux.IndexList.Select(Function(x) x / freq).Concat({Double.PositiveInfinity}).ToList()
            Dim filteredFlux As List(Of Double)

            If LowpassThresh.HasValue Then
                filteredFlux = New List(Of Double) From {0.0}
                Dim i = 0
                While i < flux.List.Count
                    Dim x = flux.List(i)
                    If x / freq <= LowpassThresh.Value Then
                        Dim y = If(i + 1 < flux.List.Count, flux.List(i + 1), 0.0)
                        If y <= x Then
                            Dim z = If(i + 2 < flux.List.Count, flux.List(i + 2), 0.0)
                            filteredFlux.Add(x + y + z)
                            i += 3
                        Else
                            filteredFlux(filteredFlux.Count - 1) += x + y
                            i += 2
                        End If
                    Else
                        filteredFlux.Add(x)
                        i += 1
                    End If
                End While
            Else
                filteredFlux = flux.List.ToList()
            End If

            Dim tail = Math.Max(0.0, flux.IndexList.Sum() - filteredFlux.Sum() + clockCentre * freq * 2)
            filteredFlux.Add(tail)

            Dim revolutionsRaw As New List(Of Integer)()
            Dim decoder As New ManagedFluxDecoder()
            decoder.FluxToBitcells(BitArray, TimeArray, revolutionsRaw,
                                   indexValues.GetEnumerator(),
                                   filteredFlux.GetEnumerator(),
                                   freq, clockCentre, clockMin, clockMax,
                                   PllPeriodAdj, PllPhaseAdj)

            For i = 0 To revolutionsRaw.Count - 1
                Dim hardsector As List(Of Integer) = Nothing
                If flux.SectorList IsNot Nothing AndAlso i < flux.SectorList.Count Then
                    Dim start = revolutionsRaw.Take(i).Sum()
                    Dim revCells = TimeArray.Skip(start).Take(revolutionsRaw(i)).ToList()
                    hardsector = New List(Of Integer)()
                    Dim sectorEnds = Accumulate(flux.SectorList(i).Select(Function(x) x / freq))
                    For Each sectorEnd In sectorEnds
                        Dim nbits = 0
                        Dim cellSum = 0.0
                        For Each t In revCells
                            cellSum += t
                            If cellSum < sectorEnd Then
                                nbits += 1
                            Else
                                nbits += 1
                                Exit For
                            End If
                        Next
                        hardsector.Add(nbits)
                    Next
                End If
                Revolutions.Add(New PllRevolution(revolutionsRaw(i), hardsector))
            Next
        End Sub

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper equivalent to cumulative sector endpoints)
        Private Shared Iterator Function Accumulate(values As IEnumerable(Of Double)) As IEnumerable(Of Double)
            Dim sum = 0.0
            For Each v In values
                sum += v
                Yield sum
            Next
        End Function

        ' Python map: src/greaseweazle/track.py::(no direct 1:1 symbol; VB helper for bitarray to hex literal formatting used in __str__)
        Private Shared Function BitsToHexLiteral(bits As IEnumerable(Of Boolean)) As String
            Dim bitList = bits.ToList()
            If bitList.Count = 0 Then
                Return "b''"
            End If

            Dim byteCount = CInt(Math.Ceiling(bitList.Count / 8.0))
            Dim bytes(byteCount - 1) As Byte
            For i = 0 To bitList.Count - 1
                If bitList(i) Then
                    Dim byteIndex = i \ 8
                    Dim bitIndex = 7 - (i Mod 8)
                    bytes(byteIndex) = CByte(bytes(byteIndex) Or (1 << bitIndex))
                End If
            Next

            Dim hex = BitConverter.ToString(bytes).Replace("-", String.Empty).ToLowerInvariant()
            Return String.Format("b'{0}'", hex)
        End Function
    End Class

End Namespace
