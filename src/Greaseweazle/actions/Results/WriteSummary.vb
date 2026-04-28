Namespace Greaseweazle.Actions

    ' Strongly-typed return value of WriteCommand.Run. DryRun is True
    ' when --test was passed (no USB activity, header events fire but
    ' no per-track work).
    Public NotInheritable Class WriteSummary

        Public Sub New(tracks As String,
                       formatName As String,
                       outcome As WriteVerifyOutcome,
                       verifiedCount As Integer,
                       notVerifiedCount As Integer,
                       dryRun As Boolean)
            Me.Tracks = tracks
            Me.FormatName = formatName
            Me.Outcome = outcome
            Me.VerifiedCount = verifiedCount
            Me.NotVerifiedCount = notVerifiedCount
            Me.DryRun = dryRun
        End Sub

        Public ReadOnly Property Tracks As String
        Public ReadOnly Property FormatName As String

        Public ReadOnly Property Outcome As WriteVerifyOutcome
        Public ReadOnly Property VerifiedCount As Integer
        Public ReadOnly Property NotVerifiedCount As Integer

        Public ReadOnly Property DryRun As Boolean

    End Class

End Namespace
