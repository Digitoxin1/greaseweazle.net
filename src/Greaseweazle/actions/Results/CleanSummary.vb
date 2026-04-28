Namespace Greaseweazle.Actions

    ' Strongly-typed return value of CleanCommand.Run. Library consumers
    ' use this to confirm how many passes/cylinders were actually
    ' visited; the CLI does not currently render this object — Python's
    ' clean command emits no terminal summary line.
    Public NotInheritable Class CleanSummary

        Public Sub New(cyls As Integer,
                       passes As Integer,
                       cylindersVisited As Integer,
                       dryRun As Boolean)
            Me.Cyls = cyls
            Me.Passes = passes
            Me.CylindersVisited = cylindersVisited
            Me.DryRun = dryRun
        End Sub

        ' Number of cylinders the drive is configured for (--cyls).
        Public ReadOnly Property Cyls As Integer

        ' Number of passes (--passes).
        Public ReadOnly Property Passes As Integer

        ' Total clamped cylinder visits across every pass. 0 in dry-run
        ' mode — events still fire, but no USB seek occurs.
        Public ReadOnly Property CylindersVisited As Integer

        ' True when --test was set: USB was never opened.
        Public ReadOnly Property DryRun As Boolean

    End Class

End Namespace
