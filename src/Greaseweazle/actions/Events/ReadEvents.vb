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

End Namespace
