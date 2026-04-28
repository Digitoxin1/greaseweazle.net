Namespace Greaseweazle.Actions

    ' Strongly-typed return value of EraseCommand.Run. The CLI does not
    ' currently render this object — Python's erase command emits no
    ' summary line — but library consumers can read it to know whether
    ' the run actually touched the drive or stopped after the header
    ' (--test mode), and how much work was done.
    Public NotInheritable Class EraseSummary

        Public Sub New(tracks As String,
                       revs As Integer,
                       tracksProcessed As Integer,
                       dryRun As Boolean)
            Me.Tracks = tracks
            Me.Revs = revs
            Me.TracksProcessed = tracksProcessed
            Me.DryRun = dryRun
        End Sub

        ' Compact track-set spec from EraseOptions.Tracks.
        Public ReadOnly Property Tracks As String

        ' Number of erase revolutions per track requested by --revs.
        Public ReadOnly Property Revs As Integer

        ' Number of physical tracks visited. Always 0 in dry-run mode.
        Public ReadOnly Property TracksProcessed As Integer

        ' True when --test was set: USB was never opened, no track was erased.
        Public ReadOnly Property DryRun As Boolean

    End Class

End Namespace
