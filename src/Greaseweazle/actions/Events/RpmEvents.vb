Namespace Greaseweazle.Actions

    ' Raised once per spindle revolution measurement (matches Python's
    ' "Rate: ... rpm ; Period: ... ms" line). The CLI formatter uses
    ' SampleIndex (1-based) only for traceability — Python doesn't
    ' include it in the rendered text — and renders TimePerRev itself.
    Public NotInheritable Class RpmSampleMeasuredEventArgs
        Inherits EventArgs

        Public Sub New(sampleIndex As Integer, timePerRev As Double)
            Me.SampleIndex = sampleIndex
            Me.TimePerRev = timePerRev
        End Sub

        ' 1-based index of the sample (1..Nr).
        Public ReadOnly Property SampleIndex As Integer

        ' Measured period of one revolution, in seconds.
        Public ReadOnly Property TimePerRev As Double

    End Class

    ' Raised after the per-sample loop terminates (success or partial).
    ' Only fires when at least 2 samples were collected — mirrors
    ' Python's `len(time_per_rev) > 1` guard around the FASTEST/Mean/
    ' Median/SLOWEST block. Fires from within a Finally so a partial
    ' run that throws CmdError still surfaces a summary first.
    Public NotInheritable Class RpmSummaryReadyEventArgs
        Inherits EventArgs

        Public Sub New(summary As RpmSummary)
            Me.Summary = summary
        End Sub

        Public ReadOnly Property Summary As RpmSummary

    End Class

End Namespace
