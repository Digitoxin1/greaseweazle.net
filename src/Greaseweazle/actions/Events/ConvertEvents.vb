Imports Greaseweazle.Shared

Namespace Greaseweazle.Actions

    ' How a single Convert input track was interpreted.
    Public Enum ConvertTrackOutcome
        ' --format wasn't supplied (or the input track is already a
        ' Codec) — the formatter renders Python's "{tspec}: {summary}".
        NoFormat = 0

        ' Format codec successfully decoded the flux. DecodedSummary is
        ' populated.
        Decoded = 1

        ' Format codec rejected the flux: track is skipped in the
        ' output. FormatName is populated for the WARNING line.
        OutOfRange = 2
    End Enum

    ' Track address as it appears to a Convert subscriber. PhysicalCyl/
    ' PhysicalHead reflect the input image's geometry — Convert renders
    ' "{cyl}.{head} <- Image {pcyl}.{phead}" when they differ.
    Public NotInheritable Class ConvertTrackInfo

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

    End Class

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

        Public Sub New(track As ConvertTrackInfo, hardSectorCount As Integer)
            Me.Track = track
            Me.HardSectorCount = hardSectorCount
        End Sub

        Public ReadOnly Property Track As ConvertTrackInfo
        Public ReadOnly Property HardSectorCount As Integer

    End Class

    ' Raised after each input track is processed (decoded or skipped).
    ' One of three legacy lines is rendered per Outcome — see
    ' ConvertTrackOutcome.
    Public NotInheritable Class ConvertTrackProcessedEventArgs
        Inherits EventArgs

        Public Sub New(track As ConvertTrackInfo,
                       outcome As ConvertTrackOutcome,
                       fluxSummary As String,
                       decodedSummary As String,
                       formatName As String)
            Me.Track = track
            Me.Outcome = outcome
            Me.FluxSummary = fluxSummary
            Me.DecodedSummary = decodedSummary
            Me.FormatName = formatName
        End Sub

        Public ReadOnly Property Track As ConvertTrackInfo
        Public ReadOnly Property Outcome As ConvertTrackOutcome

        ' Always populated. Comes from track.SummaryString() in the
        ' DLL's flux/codec layer.
        Public ReadOnly Property FluxSummary As String

        ' Populated only when Outcome = Decoded.
        Public ReadOnly Property DecodedSummary As String

        ' Populated only when Outcome = OutOfRange.
        Public ReadOnly Property FormatName As String

    End Class

    ' Raised once at the end of a Convert run with the completed sector
    ' grid (cyls × heads × sectors, with per-cell verdict). Subscribers
    ' render Python's "Cyl->", "H. S:", per-row, and "Found N of M"
    ' tally — or surface the typed grid in a UI.
    Public NotInheritable Class ConvertSummaryReadyEventArgs
        Inherits EventArgs

        Public Sub New(grid As SectorSummaryGrid)
            Me.Grid = grid
        End Sub

        Public ReadOnly Property Grid As SectorSummaryGrid

    End Class

End Namespace
