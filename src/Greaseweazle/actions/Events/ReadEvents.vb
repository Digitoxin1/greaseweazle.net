Imports Greaseweazle.Shared

Namespace Greaseweazle.Actions

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

    ' Raised once when the retry budget is exhausted for a track.
    ' Always preceded by at least one TrackProcessed event.
    Public NotInheritable Class ReadTrackGaveUpEventArgs
        Inherits EventArgs

        Public Sub New(track As TrackInfo, missingSectors As Integer)
            Me.Track = track
            Me.MissingSectors = missingSectors
        End Sub

        Public ReadOnly Property Track As TrackInfo
        Public ReadOnly Property MissingSectors As Integer

    End Class

    ' Raised per duplicate entry in ReadOptions.AdditionalFiles that was
    ' silently skipped because it normalised (Path.GetFullPath,
    ' OrdinalIgnoreCase) to the same absolute path as the primary sink or
    ' an earlier additional. Fires before any USB activity, during
    ' sink-list construction inside ReadAction.RunLive. Path is the
    ' caller-supplied raw string (including any `::opts` tail); MatchedPath
    ' is the already-accepted sink whose normalised form collided.
    Public NotInheritable Class ReadAdditionalOutputDedupedEventArgs
        Inherits EventArgs

        Public Sub New(path As String, matchedPath As String)
            Me.Path = path
            Me.MatchedPath = matchedPath
        End Sub

        Public ReadOnly Property Path As String
        Public ReadOnly Property MatchedPath As String

    End Class

End Namespace
