Namespace Greaseweazle.Actions

    ' Strongly-typed return value of ReadCommand.Run. DryRun is True
    ' when --test was passed (no USB activity, no output image).
    Public NotInheritable Class ReadSummary

        ' Legacy single-sink constructor. Preserved for source-level
        ' compatibility with callers that don't use AdditionalFiles.
        ' AdditionalOutputPaths is exposed as an empty read-only list so
        ' downstream `For Each` / `.Count` consumers never null-deref.
        Public Sub New(tracks As String,
                       revsDisplay As String,
                       tracksProcessed As Integer,
                       formatName As String,
                       outputPath As String,
                       grid As SectorSummaryGrid,
                       dryRun As Boolean)
            Me.New(tracks,
                   revsDisplay,
                   tracksProcessed,
                   formatName,
                   outputPath,
                   CType(Array.Empty(Of String)(), IReadOnlyList(Of String)),
                   grid,
                   dryRun)
        End Sub

        ' Multi-sink constructor. Used when ReadOptions.AdditionalFiles
        ' produced one or more additional sinks alongside the primary.
        ' additionalOutputPaths is assumed non-null (caller passes
        ' Array.Empty(Of String)() for a single-sink run that wants to
        ' use this overload explicitly).
        Public Sub New(tracks As String,
                       revsDisplay As String,
                       tracksProcessed As Integer,
                       formatName As String,
                       outputPath As String,
                       additionalOutputPaths As IReadOnlyList(Of String),
                       grid As SectorSummaryGrid,
                       dryRun As Boolean)
            Me.Tracks = tracks
            Me.RevsDisplay = revsDisplay
            Me.TracksProcessed = tracksProcessed
            Me.FormatName = formatName
            Me.OutputPath = outputPath
            Me.AdditionalOutputPaths = If(additionalOutputPaths,
                                          CType(Array.Empty(Of String)(), IReadOnlyList(Of String)))
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

        ' Resolved paths of any additional sinks produced in the same
        ' read pass (ReadOptions.AdditionalFiles). Empty (never null)
        ' when no additionals were requested or after --test. Entries
        ' are in caller-supplied order, minus any silently-deduped
        ' duplicates — the corresponding AdditionalOutputDeduped events
        ' fire during the run for subscribers that want to observe
        ' dedup decisions.
        Public ReadOnly Property AdditionalOutputPaths As IReadOnlyList(Of String)

        ' Same grid that was carried in the SummaryReady event. May be
        ' Nothing when no codec decoded any sectors.
        Public ReadOnly Property Grid As SectorSummaryGrid

        Public ReadOnly Property DryRun As Boolean

    End Class

End Namespace
