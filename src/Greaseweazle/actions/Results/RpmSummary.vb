Namespace Greaseweazle.Actions

    ' Strongly-typed return value of RpmCommand.Run, also the payload
    ' carried in RpmSummaryReadyEventArgs. The CLI formatter renders
    ' the FASTEST/Ar.Mean/Median/SLOWEST block from these fields; library
    ' consumers can surface the same statistics in a UI without
    ' reformatting Python's text.
    '
    ' All time fields are seconds-per-revolution. Convert to RPM with
    ' `60.0 / value`, to milliseconds with `value * 1000.0`.
    Public NotInheritable Class RpmSummary

        Public Sub New(samples As IReadOnlyList(Of Double),
                       fastest As Double,
                       slowest As Double,
                       mean As Double,
                       median As Double,
                       completed As Boolean,
                       dryRun As Boolean)
            Me.Samples = samples
            Me.Fastest = fastest
            Me.Slowest = slowest
            Me.Mean = mean
            Me.Median = median
            Me.Completed = completed
            Me.DryRun = dryRun
        End Sub

        ' Raw seconds-per-rev measurements, in measurement order.
        Public ReadOnly Property Samples As IReadOnlyList(Of Double)

        ' Smallest seconds-per-rev (highest RPM).
        Public ReadOnly Property Fastest As Double

        ' Largest seconds-per-rev (lowest RPM).
        Public ReadOnly Property Slowest As Double

        ' Arithmetic mean of Samples.
        Public ReadOnly Property Mean As Double

        ' Python uses `sorted[len/2]` (lower-of-two for even counts), not
        ' the average — this property follows the same convention so
        ' the CLI output matches byte-for-byte.
        Public ReadOnly Property Median As Double

        ' True when the requested Nr samples were all collected. False
        ' when the run was cut short (e.g. CmdError partway through).
        Public ReadOnly Property Completed As Boolean

        ' True when --test was set: USB was never opened, no samples
        ' collected, all stat fields are 0.
        Public ReadOnly Property DryRun As Boolean

        ' Builds a summary from a partial-or-full sample list. Returns
        ' Nothing when the list has fewer than 2 entries (matches
        ' Python's `len(time_per_rev) > 1` guard); callers should treat
        ' a Nothing return as "no summary block to emit".
        Friend Shared Function FromSamples(samples As IReadOnlyList(Of Double),
                                           completed As Boolean) As RpmSummary
            If samples Is Nothing OrElse samples.Count <= 1 Then Return Nothing
            Dim sortedSamples = samples.OrderBy(Function(x) x).ToList()
            Return New RpmSummary(
                samples,
                fastest:=samples.Min(),
                slowest:=samples.Max(),
                mean:=samples.Average(),
                median:=sortedSamples(sortedSamples.Count \ 2),
                completed:=completed,
                dryRun:=False)
        End Function

        ' Sentinel for --test mode.
        Friend Shared Function ForDryRun() As RpmSummary
            Return New RpmSummary(New List(Of Double)(),
                                  fastest:=0,
                                  slowest:=0,
                                  mean:=0,
                                  median:=0,
                                  completed:=False,
                                  dryRun:=True)
        End Function

    End Class

End Namespace
