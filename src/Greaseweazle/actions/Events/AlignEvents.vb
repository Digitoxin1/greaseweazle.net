Imports Greaseweazle.Shared

Namespace Greaseweazle.Actions

    ' Raised once at the start of an Align run, after live-mode
    ' adjustments (fractional revs collapse, hard-sector multiplier)
    ' have been applied. The CLI formatter renders Python's
    '   "Aligning T{c}.{h}, reading R times, revs=N"
    '   "Aligning Tcyl (alternating heads X,Y), reading R times, revs=N"
    ' from these typed fields. FormatName is non-Nothing iff --format
    ' was supplied — the formatter then renders an additional
    ' "Format <name>" line right after the header.
    Public NotInheritable Class AlignStartedEventArgs
        Inherits EventArgs

        Public Sub New(tracks As IReadOnlyList(Of TrackIter),
                       reads As Integer,
                       revs As Integer,
                       formatName As String)
            Me.Tracks = tracks
            Me.Reads = reads
            Me.Revs = revs
            Me.FormatName = formatName
        End Sub

        Public ReadOnly Property Tracks As IReadOnlyList(Of TrackIter)
        Public ReadOnly Property Reads As Integer
        Public ReadOnly Property Revs As Integer
        Public ReadOnly Property FormatName As String

    End Class

End Namespace
