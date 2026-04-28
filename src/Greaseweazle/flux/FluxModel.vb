Imports System.Text

Namespace Greaseweazle.Core

    ' Python map: src/greaseweazle/flux.py::HasFlux
    Public Interface HasFlux
        ' Python map: src/greaseweazle/flux.py::HasFlux.summary_string
        Function SummaryString() As String
        ' Python map: src/greaseweazle/flux.py::HasFlux.flux
        Function Flux() As Flux
        ' Python map: src/greaseweazle/flux.py::HasFlux.flux_for_writeout
        Function FluxForWriteout(cueAtIndex As Boolean) As WriteoutFlux
    End Interface

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration Flux)
    ' Python map: src/greaseweazle/flux.py::Flux
    Public Class Flux
        Implements HasFlux

        Private _ticksPerRev As Double

        ' Python map: src/greaseweazle/flux.py::Flux.__init__
        Public Sub New(indexList As IEnumerable(Of Double),
                       fluxList As IEnumerable(Of Double),
                       sampleFreq As Double,
                       Optional indexCued As Boolean = True)
            Me.IndexList = New List(Of Double)(indexList)
            Me.List = New List(Of Double)(fluxList)
            Me.SampleFreq = sampleFreq
            Me.IndexCued = indexCued
        End Sub

        Public Property IndexList As List(Of Double)
        Public Property SectorList As List(Of List(Of Double))
        Public Property List As List(Of Double)
        Public Property SampleFreq As Double
        Public Property Splice As Nullable(Of Double)
        Public Property IndexCued As Boolean

        ' Python map: src/greaseweazle/flux.py::Flux.__str__
        Public Overrides Function ToString() As String
            ' Python's f-string `f'{x:.2f}'` always emits the C-locale decimal
            ' separator. .NET's String.Format defaults to CurrentCulture which
            ' on (e.g.) German locales emits a comma. Force InvariantCulture so
            ' the rendered text matches Python's output byte-for-byte.
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim sb As New StringBuilder()
            sb.AppendLine(String.Empty)
            sb.AppendLine(String.Format(ci, "Flux: {0:F2} MHz{1}",
                                        SampleFreq * 1.0E-6,
                                        If(IndexCued, ", Index-Cued", String.Empty)))
            sb.AppendLine(String.Format(ci, " Total: {0} samples, {1:F2}ms",
                                        List.Count,
                                        List.Sum() * 1000.0 / SampleFreq))
            For rev = 0 To IndexList.Count - 1
                sb.AppendLine(String.Format(ci, " Revolution {0}: {1:F2}ms",
                                            rev,
                                            IndexList(rev) * 1000.0 / SampleFreq))
                If SectorList IsNot Nothing AndAlso rev < SectorList.Count Then
                    For sec = 0 To SectorList(rev).Count - 1
                        sb.AppendLine(String.Format(ci, "    Sector {0}: {1:F2}ms",
                                                    sec,
                                                    SectorList(rev)(sec) * 1000.0 / SampleFreq))
                    Next
                End If
            Next

            Return sb.ToString().TrimEnd()
        End Function

        ' Python map: src/greaseweazle/flux.py::Flux.summary_string
        Public Function SummaryString() As String Implements HasFlux.SummaryString
            Return String.Format(Globalization.CultureInfo.InvariantCulture,
                                 "Raw Flux ({0} flux in {1:F2}ms)",
                                 List.Count,
                                 List.Sum() * 1000.0 / SampleFreq)
        End Function

        ' Python map: src/greaseweazle/flux.py::Flux.identify_hard_sectors
        Public Sub IdentifyHardSectors()
            If SectorList IsNot Nothing Then
                Return
            End If

            ErrorHandling.Check(IndexList.Count > 3,
                                "Not enough index marks for a hard-sectored track")
            CueAtIndex()

            Dim sortedTwo As New List(Of Double) From {IndexList(0), IndexList(2)}
            sortedTwo.Sort()
            Dim threshold = sortedTwo(1) * 3.0 / 4.0
            Dim ticksToIndex As Double = 0
            Dim shortTicks As Double = 0
            Dim sectors As New List(Of Double)()
            Dim original = IndexList.ToList()
            IndexList = New List(Of Double)()
            SectorList = New List(Of List(Of Double))()
            Dim shortCount = 0

            For Each t In original
                Dim isShort = (t < threshold)
                If isShort Then
                    shortTicks += t
                    shortCount += 1
                End If

                If shortCount <> 0 AndAlso (shortCount > 1 OrElse Not isShort) Then
                    ticksToIndex += shortTicks
                    sectors.Add(shortTicks)
                    IndexList.Add(ticksToIndex)
                    SectorList.Add(New List(Of Double)(sectors))
                    sectors.Clear()
                    shortTicks = 0
                    ticksToIndex = 0
                    shortCount = 0
                End If

                If Not isShort Then
                    ticksToIndex += t
                    sectors.Add(t)
                End If
            Next

            ErrorHandling.Check(IndexList.Count > 0, "No hard-sector index mark found")
            IndexCued = (IndexList.Count >= 2) AndAlso
                        (SectorList(0).Count = SectorList(1).Count)
        End Sub

        ' Python map: src/greaseweazle/flux.py::Flux.append
        Public Sub Append(other As Flux)
            Dim appendFlux As List(Of Double)
            Dim appendIndex As List(Of Double)

            If SampleFreq = other.SampleFreq Then
                appendFlux = other.List.ToList()
                appendIndex = other.IndexList.ToList()
            Else
                Dim factor = SampleFreq / other.SampleFreq
                appendFlux = other.List.Select(Function(x) x * factor).ToList()
                appendIndex = other.IndexList.Select(Function(x) x * factor).ToList()
            End If

            Dim rev0 = appendIndex(0) + List.Sum() - IndexList.Sum()
            IndexList.Add(rev0)
            If appendIndex.Count > 1 Then
                IndexList.AddRange(appendIndex.Skip(1))
            End If
            List.AddRange(appendFlux)
            SectorList = Nothing
        End Sub

        ' Python map: src/greaseweazle/flux.py::Flux.cue_at_index
        Public Sub CueAtIndex()
            If IndexCued Then
                Return
            End If

            ' Python prints `\n` regardless of host OS (no Environment.NewLine
            ' equivalent in Python's textwrap-style messages); use vbLf to keep
            ' the embedded line break locale/host-independent and match Python
            ' byte-for-byte.
            ErrorHandling.Check(IndexList.Count >= 2,
                                "Not enough revolutions of flux data to cue at index." &
                                vbLf &
                                "Try dumping more revolutions (larger --revs value).")

            Dim toIndex = IndexList(0)
            Dim cut = -1
            For i = 0 To List.Count - 1
                toIndex -= List(i)
                If toIndex < 0 Then
                    cut = i
                    Exit For
                End If
            Next

            If toIndex < 0 Then
                Dim clipped As New List(Of Double) From {-toIndex}
                If cut + 1 < List.Count Then
                    clipped.AddRange(List.Skip(cut + 1))
                End If
                List = clipped
            Else
                List = New List(Of Double)()
            End If

            IndexList = IndexList.Skip(1).ToList()
            IndexCued = True
            If SectorList IsNot Nothing Then
                SectorList = SectorList.Skip(1).ToList()
            End If
        End Sub

        ' Python map: src/greaseweazle/flux.py::Flux.reverse
        Public Sub Reverse()
            If SectorList IsNot Nothing Then
                Throw New InvalidOperationException("Cannot reverse hard-sectored flux")
            End If

            Dim wasIndexCued = IndexCued
            Dim fluxSum = List.Sum()

            IndexCued = False
            List.Reverse()
            IndexList.Reverse()

            Dim toIndex = fluxSum - IndexList.Sum()
            If toIndex <= 0 Then
                If toIndex < 0 Then
                    List.Insert(0, -toIndex)
                    fluxSum += -toIndex
                End If
                IndexList = IndexList.Skip(1).ToList()
                IndexCued = True
            Else
                Dim newIndexes As New List(Of Double) From {toIndex}
                If IndexList.Count > 1 Then
                    newIndexes.AddRange(IndexList.Take(IndexList.Count - 1))
                End If
                IndexList = newIndexes
            End If

            If wasIndexCued Then
                IndexList.Add(fluxSum - IndexList.Sum())
            End If
        End Sub

        ' Python map: src/greaseweazle/flux.py::Flux.set_nr_revs
        Public Sub SetNrRevs(revs As Integer)
            CueAtIndex()
            ErrorHandling.Check(IndexList.Count > 0, "Need at least one revolution to adjust # revolutions")

            If IndexList.Count > revs Then
                IndexList = IndexList.Take(revs).ToList()
                If SectorList IsNot Nothing Then
                    SectorList = SectorList.Take(revs).ToList()
                End If

                Dim toIndex = IndexList.Sum()
                For i = 0 To List.Count - 1
                    toIndex -= List(i)
                    If toIndex < 0 Then
                        List = List.Take(i).ToList()
                        Exit For
                    End If
                Next
            End If

            While IndexList.Count < revs
                Dim nr = Math.Min(revs - IndexList.Count, IndexList.Count)
                Dim toIndex = IndexList.Take(nr).Sum()
                Dim l = List.ToList()
                For i = 0 To l.Count - 1
                    toIndex -= l(i)
                    If toIndex < 0 Then
                        toIndex += l(i)
                        l = l.Take(i).ToList()
                        Exit For
                    End If
                Next
                If List.Count > 0 Then
                    List = l.Concat({toIndex + List(0)}).Concat(List.Skip(1)).ToList()
                End If
                IndexList = IndexList.Take(nr).Concat(IndexList).ToList()
                If SectorList IsNot Nothing Then
                    SectorList = SectorList.Take(nr).Concat(SectorList).ToList()
                End If
            End While
        End Sub

        ' Python map: src/greaseweazle/flux.py::Flux.flux_for_writeout
        Public Function FluxForWriteout(cueAtIndex As Boolean) As WriteoutFlux Implements HasFlux.FluxForWriteout
            Dim spliceValue As Double = If(Me.Splice, 0.0)
            ErrorHandling.Check(IndexCued, "Cannot write non-index-cued raw flux")
            ErrorHandling.Check(spliceValue = 0 OrElse IndexList.Count > 1, "Cannot write single-revolution unaligned raw flux")

            Dim spliceAtIndex = (spliceValue = 0)
            Dim fluxList As New List(Of Double)()
            Dim toIndex = IndexList(0)
            Dim remain = toIndex + spliceValue

            For Each f In List
                If f > remain Then
                    Exit For
                End If
                fluxList.Add(f)
                remain -= f
            Next

            If Not cueAtIndex Then
                If remain > 0 Then
                    fluxList.Add(remain)
                End If
                Dim prepend = Math.Max(CInt(Math.Round(toIndex / 10.0 - spliceValue)), 0)
                If prepend <> 0 Then
                    Dim fourUs = Math.Max(SampleFreq * 4.0E-6, 1)
                    Dim repeats = CInt(Math.Round(prepend / fourUs))
                    Dim safe = Enumerable.Repeat(fourUs, repeats).ToList()
                    safe.AddRange(fluxList)
                    fluxList = safe
                End If
                spliceAtIndex = False
            ElseIf spliceAtIndex Then
                Dim fourUs = Math.Max(SampleFreq * 4.0E-6, 1)
                If remain > fourUs Then
                    fluxList.Add(remain)
                End If
                Dim repeats = CInt(Math.Round(toIndex / (10.0 * fourUs)))
                For i = 0 To repeats - 1
                    fluxList.Add(fourUs)
                Next
            ElseIf remain > 0 Then
                fluxList.Add(remain)
            End If

            Return New WriteoutFlux(toIndex, fluxList, SampleFreq, cueAtIndex, spliceAtIndex)
        End Function

        ' Python map: src/greaseweazle/flux.py::Flux.flux
        Public Function Flux() As Flux Implements HasFlux.Flux
            Return Me
        End Function

        ' Python map: src/greaseweazle/flux.py::Flux.scale
        Public Sub Scale(factor As Double)
            SampleFreq /= factor
        End Sub

        ' Python map: src/greaseweazle/flux.py::Flux.ticks_per_rev
        Public Property TicksPerRev As Double
            Get
                Try
                    Dim indexValues = IndexList
                    If Not IndexCued Then
                        indexValues = IndexList.Skip(1).ToList()
                    End If
                    Return indexValues.Sum() / indexValues.Count
                Catch
                    Return _ticksPerRev
                End Try
            End Get
            Set(value As Double)
                _ticksPerRev = value
            End Set
        End Property

        ' Python map: src/greaseweazle/flux.py::Flux.time_per_rev
        Public ReadOnly Property TimePerRev As Double
            Get
                Return TicksPerRev / SampleFreq
            End Get
        End Property

    End Class

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration WriteoutFlux)
    ' Python map: src/greaseweazle/flux.py::WriteoutFlux
    Public Class WriteoutFlux
        Implements HasFlux

        ' Python map: src/greaseweazle/flux.py::WriteoutFlux.__init__
        Public Sub New(ticksToIndex As Double,
                       fluxList As IEnumerable(Of Double),
                       sampleFreq As Double,
                       indexCued As Boolean,
                       terminateAtIndex As Boolean)
            Me.TicksToIndex = ticksToIndex
            Me.List = New List(Of Double)(fluxList)
            Me.SampleFreq = sampleFreq
            Me.IndexCued = indexCued
            Me.TerminateAtIndex = terminateAtIndex
        End Sub

        Public Property TicksToIndex As Double
        Public Property List As List(Of Double)
        Public Property SampleFreq As Double
        Public Property IndexCued As Boolean
        Public Property TerminateAtIndex As Boolean

        ' Python map: src/greaseweazle/flux.py::WriteoutFlux.__str__
        Public Overrides Function ToString() As String
            ' Python's __str__ embeds bare `\n` (flux.py:296). Use vbLf rather
            ' than Environment.NewLine so the rendered string is host-OS-
            ' independent and matches Python on Windows where NewLine is \r\n.
            ' Force InvariantCulture so {n:Fk} matches Python's locale-independent f-strings.
            Return String.Format(Globalization.CultureInfo.InvariantCulture,
                                 "{0}WriteoutFlux: {1:F2} MHz, {2:F2}ms to index, {3}{0} Total: {4} samples, {5:F2}ms",
                                 vbLf,
                                 SampleFreq * 1.0E-6,
                                 TicksToIndex * 1000.0 / SampleFreq,
                                 If(TerminateAtIndex, "Terminate at index", "Write all"),
                                 List.Count,
                                 List.Sum() * 1000.0 / SampleFreq)
        End Function

        ' Python map: src/greaseweazle/flux.py::WriteoutFlux.summary_string
        Public Function SummaryString() As String Implements HasFlux.SummaryString
            Return String.Format(Globalization.CultureInfo.InvariantCulture,
                                 "Flux: {0:F1}ms period, {1:F1} ms total, {2}",
                                 TicksToIndex * 1000.0 / SampleFreq,
                                 List.Sum() * 1000.0 / SampleFreq,
                                 If(TerminateAtIndex, "Terminate at index", "Write all"))
        End Function

        ' Python map: src/greaseweazle/flux.py::(no direct 1:1 symbol; VB guard for unsupported conversion)
        Public Function Flux() As Flux Implements HasFlux.Flux
            Throw New NotSupportedException("WriteoutFlux cannot be converted back to Flux directly.")
        End Function

        ' Python map: src/greaseweazle/flux.py::(no direct 1:1 symbol; passthrough helper for interface compliance)
        Public Function FluxForWriteout(cueAtIndex As Boolean) As WriteoutFlux Implements HasFlux.FluxForWriteout
            Return Me
        End Function
    End Class

End Namespace
