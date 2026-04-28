Namespace Greaseweazle.Actions

    ' Strongly-typed return value of ReadCommand.Run. DryRun is True
    ' when --test was passed (no USB activity, no output image).
    Public NotInheritable Class ReadSummary

        Public Sub New(tracks As String,
                       revsDisplay As String,
                       tracksProcessed As Integer,
                       formatName As String,
                       outputPath As String,
                       grid As SectorSummaryGrid,
                       dryRun As Boolean)
            Me.Tracks = tracks
            Me.RevsDisplay = revsDisplay
            Me.TracksProcessed = tracksProcessed
            Me.FormatName = formatName
            Me.OutputPath = outputPath
            Me.Grid = grid
            Me.DryRun = dryRun
        End Sub

        Public ReadOnly Property Tracks As String
        Public ReadOnly Property RevsDisplay As String
        Public ReadOnly Property TracksProcessed As Integer
        Public ReadOnly Property FormatName As String

        ' Resolved file (or KryoFlux basename) the algorithm wrote.
        ' Nothing when --test produces no output. Library consumers
        ' (e.g. UI front-ends) can surface this without any text
        ' formatting.
        Public ReadOnly Property OutputPath As String

        ' Same grid that was carried in the SummaryReady event. May be
        ' Nothing when no codec decoded any sectors.
        Public ReadOnly Property Grid As SectorSummaryGrid

        Public ReadOnly Property DryRun As Boolean

    End Class

End Namespace
