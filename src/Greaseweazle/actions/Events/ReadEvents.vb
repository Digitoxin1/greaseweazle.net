Imports Greaseweazle.Shared

Namespace Greaseweazle.Actions

    ' How a single Read track was interpreted. Mirrors the parallel
    ' ConvertTrackOutcome enum so subscribers that handle both can
    ' share the same render logic.
    Public Enum ReadTrackOutcome
        ' --format wasn't supplied — formatter renders Python's
        ' "{tspec}: {flux.SummaryString()}".
        NoFormat = 0

        ' Format codec successfully decoded the flux. DecodedSummary is
        ' populated. SeekRetry/Retry track the retry index when > 0.
        Decoded = 1

        ' Format codec rejected the flux: the WARNING line is emitted
        ' and the track is skipped.
        OutOfRange = 2
    End Enum

    ' Track address as it appears to a Read subscriber. PhysicalCyl/
    ' PhysicalHead reflect the drive's physical address — Read renders
    ' "{cyl}.{head} <- Drive {pcyl}.{phead}" when they differ.
    Public NotInheritable Class ReadTrackInfo

        Public Sub New(cyl As Integer, head As Integer, physicalCyl As Integer, physicalHead As Integer)
            Me.Cyl = cyl
            Me.Head = head
            Me.PhysicalCyl = physicalCyl
            Me.PhysicalHead = physicalHead
        End Sub

        Public ReadOnly Property Cyl As Integer
        Public ReadOnly Property Head As Integer
        Public ReadOnly Property PhysicalCyl As Integer
        Public ReadOnly Property PhysicalHead As Integer

        Public Shared Function FromTrackIter(t As TrackIter) As ReadTrackInfo
            Return New ReadTrackInfo(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)
        End Function

    End Class

    ' Raised once at the start of a Read run, after track-set/format
    ' resolution but before any USB activity. RevsDisplay preserves
    ' the textual revs value (Python prints `str(args.revs)` directly,
    ' which preserves fractional defaults like "1.1").
    Public NotInheritable Class ReadStartedEventArgs
        Inherits EventArgs

        Public Sub New(tracks As String, revsDisplay As String, formatName As String)
            Me.Tracks = tracks
            Me.RevsDisplay = revsDisplay
            Me.FormatName = formatName
        End Sub

        Public ReadOnly Property Tracks As String
        Public ReadOnly Property RevsDisplay As String
        Public ReadOnly Property FormatName As String

    End Class

    ' Raised once when --hard-sectors auto-detects the count from
    ' the drive. CLI renders Python's "Drive reports N hard sectors"
    ' line.
    Public NotInheritable Class ReadHardSectorsEventArgs
        Inherits EventArgs

        Public Sub New(hardSectorCount As Integer)
            Me.HardSectorCount = hardSectorCount
        End Sub

        Public ReadOnly Property HardSectorCount As Integer

    End Class

    ' Raised after each successful read attempt for a track (the
    ' first attempt + any retry attempts). NoFormat / Decoded /
    ' OutOfRange map to the three legacy lines; SeekRetry/Retry are
    ' both 0 on the first attempt.
    Public NotInheritable Class ReadTrackProcessedEventArgs
        Inherits EventArgs

        Public Sub New(track As ReadTrackInfo,
                       outcome As ReadTrackOutcome,
                       fluxSummary As String,
                       decodedSummary As String,
                       formatName As String,
                       seekRetry As Integer,
                       retry As Integer)
            Me.Track = track
            Me.Outcome = outcome
            Me.FluxSummary = fluxSummary
            Me.DecodedSummary = decodedSummary
            Me.FormatName = formatName
            Me.SeekRetry = seekRetry
            Me.Retry = retry
        End Sub

        Public ReadOnly Property Track As ReadTrackInfo
        Public ReadOnly Property Outcome As ReadTrackOutcome
        Public ReadOnly Property FluxSummary As String

        ' Populated only when Outcome = Decoded.
        Public ReadOnly Property DecodedSummary As String

        ' Populated only when Outcome = OutOfRange.
        Public ReadOnly Property FormatName As String

        ' SeekRetry/Retry are 0 for the first read attempt; > 0 for
        ' subsequent retries. The CLI appends " (Retry #X.Y)" when
        ' Retry <> 0.
        Public ReadOnly Property SeekRetry As Integer
        Public ReadOnly Property Retry As Integer

    End Class

    ' Raised once when the retry budget is exhausted for a track.
    ' Always preceded by at least one ReadTrackProcessed event.
    Public NotInheritable Class ReadTrackGaveUpEventArgs
        Inherits EventArgs

        Public Sub New(track As ReadTrackInfo, missingSectors As Integer)
            Me.Track = track
            Me.MissingSectors = missingSectors
        End Sub

        Public ReadOnly Property Track As ReadTrackInfo
        Public ReadOnly Property MissingSectors As Integer

    End Class

    ' Raised once at the end of a run when --format produced a
    ' non-empty decode dict. CLI renders the "Cyl-> / H. S: / .X"
    ' grid + "Found N sectors of M" tally.
    Public NotInheritable Class ReadSummaryReadyEventArgs
        Inherits EventArgs

        Public Sub New(grid As SectorSummaryGrid)
            Me.Grid = grid
        End Sub

        Public ReadOnly Property Grid As SectorSummaryGrid

    End Class

End Namespace
