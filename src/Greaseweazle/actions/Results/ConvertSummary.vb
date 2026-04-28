Namespace Greaseweazle.Actions

    ' Strongly-typed return value of ConvertCommand.Run.
    Public NotInheritable Class ConvertSummary

        Public Sub New(inTracks As String,
                       outTracks As String,
                       tracksProcessed As Integer,
                       formatName As String,
                       grid As SectorSummaryGrid)
            Me.InTracks = inTracks
            Me.OutTracks = outTracks
            Me.TracksProcessed = tracksProcessed
            Me.FormatName = formatName
            Me.Grid = grid
        End Sub

        Public ReadOnly Property InTracks As String
        Public ReadOnly Property OutTracks As String

        ' Number of input tracks that produced output (excludes skipped
        ' OutOfRange tracks and missing source tracks).
        Public ReadOnly Property TracksProcessed As Integer

        ' Effective format name (or Nothing). Mirrors the resolved
        ' format that produced the run.
        Public ReadOnly Property FormatName As String

        ' Same grid that was carried in the SummaryReady event. May be
        ' empty when no codec decoded any sectors.
        Public ReadOnly Property Grid As SectorSummaryGrid

    End Class

End Namespace
