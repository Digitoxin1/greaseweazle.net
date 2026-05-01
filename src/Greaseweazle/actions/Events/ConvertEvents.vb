Imports Greaseweazle.Shared

Namespace Greaseweazle.Actions

    ' Raised once at the start of a Convert run after both images are
    ' opened and track sets resolved. The CLI renders Python's optional
    ' "Format <name>" line plus the "Converting {tracks} -> {outTracks}"
    ' header.
    Public NotInheritable Class ConvertStartedEventArgs
        Inherits EventArgs

        Public Sub New(formatName As String, inTracks As String, outTracks As String)
            Me.FormatName = formatName
            Me.InTracks = inTracks
            Me.OutTracks = outTracks
        End Sub

        ' --format value (or auto-resolved IMG default); Nothing when no
        ' format applies. Subscribers render "Format <name>" only when
        ' this is non-empty.
        Public ReadOnly Property FormatName As String

        ' Compact track-set spec being read from the input image.
        Public ReadOnly Property InTracks As String

        ' Compact track-set spec being written to the output image.
        Public ReadOnly Property OutTracks As String

    End Class

    ' Raised when --hard-sectors detection succeeds for a track. The CLI
    ' renders Python's "{tspec}: Converted to {N} hard sectors" line.
    Public NotInheritable Class ConvertHardSectorsEventArgs
        Inherits EventArgs

        Public Sub New(track As TrackInfo, hardSectorCount As Integer)
            Me.Track = track
            Me.HardSectorCount = hardSectorCount
        End Sub

        Public ReadOnly Property Track As TrackInfo
        Public ReadOnly Property HardSectorCount As Integer

    End Class

End Namespace
