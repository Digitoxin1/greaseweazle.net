Namespace Greaseweazle.Actions

    ' Strongly-typed return value of AlignCommand.Run. Library consumers
    ' use ReadsCompleted to know whether the Reads loop ran to completion
    ' (it can be cut short by Ctrl-C / CmdError), and DryRun to detect
    ' the --test path. The CLI does not currently render this object —
    ' Python's align command emits no terminal summary.
    Public NotInheritable Class AlignSummary

        Public Sub New(reads As Integer,
                       readsCompleted As Integer,
                       dryRun As Boolean)
            Me.Reads = reads
            Me.ReadsCompleted = readsCompleted
            Me.DryRun = dryRun
        End Sub

        ' Configured number of read passes (--reads).
        Public ReadOnly Property Reads As Integer

        ' Number of read passes that emitted a ReadCompleted event.
        ' Always 0 in dry-run mode.
        Public ReadOnly Property ReadsCompleted As Integer

        ' True when --test was set: USB was never opened.
        Public ReadOnly Property DryRun As Boolean

    End Class

End Namespace
