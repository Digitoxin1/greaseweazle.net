Namespace Greaseweazle.Actions

    ' Outcome of SeekCommand.Run. Library consumers branch on this to know
    ' whether the drive head actually moved.
    Public Enum SeekOutcome
        ' --test dry-run mode: no USB activity took place. The cylinder
        ' field still reflects the parsed argument so a caller could
        ' replay the same options live.
        DryRun = 0

        ' Algorithm called the supplied prompter (or had none) and the
        ' user declined the extreme-cylinder seek; the head was not
        ' moved.
        Aborted = 1

        ' Drive head was moved to the requested cylinder.
        Seeked = 2
    End Enum

    ' Strongly-typed return value of SeekCommand.Run.
    Public NotInheritable Class SeekResult

        Public Sub New(outcome As SeekOutcome, cylinder As Integer)
            Me.Outcome = outcome
            Me.Cylinder = cylinder
        End Sub

        Public ReadOnly Property Outcome As SeekOutcome
        Public ReadOnly Property Cylinder As Integer

    End Class

    ' Pluggable prompt strategy used by SeekCommand when the requested
    ' cylinder is "extreme" (cyl < 0 OR cyl > 83) and the user did not
    ' opt out of the safety check. The library only passes structured
    ' data (the cylinder number); the prompter implementation owns the
    ' user-facing text. The CLI front-end ships a ConsoleSeekPrompter
    ' that writes "Seek to extreme cylinder N, Yes/No? " to stdout and
    ' reads an answer from stdin; a GUI consumer would implement
    ' ConfirmExtremeCylinder() to show a Yes/No dialog formatted however
    ' it likes.
    '
    ' If SeekCommand.Prompter is Nothing when the algorithm needs a
    ' confirmation, the algorithm aborts (Outcome = Aborted) — the
    ' default-deny stance keeps a non-interactive caller from
    ' accidentally damaging a drive by stepping past its end stop.
    Public Interface ISeekPrompter

        Function ConfirmExtremeCylinder(cyl As Integer) As Boolean

    End Interface

End Namespace
