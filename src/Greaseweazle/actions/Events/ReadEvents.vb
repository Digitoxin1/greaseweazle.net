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
                       retry As Integer,
                       decodedSectorsFound As Integer,
                       decodedSectorsTotal As Integer,
                       fluxSampleCount As Nullable(Of Integer),
                       fluxDurationMs As Nullable(Of Double))
            Me.Track = track
            Me.Outcome = outcome
            Me.FluxSummary = fluxSummary
            Me.DecodedSummary = decodedSummary
            Me.FormatName = formatName
            Me.SeekRetry = seekRetry
            Me.Retry = retry
            Me.DecodedSectorsFound = decodedSectorsFound
            Me.DecodedSectorsTotal = decodedSectorsTotal
            Me.FluxSampleCount = fluxSampleCount
            Me.FluxDurationMs = fluxDurationMs
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

        ' Number of sectors successfully decoded for the track
        ' (Codec.Nsec - Codec.NrMissing). 0 when Outcome != Decoded
        ' or when the codec has no sector concept (raw Bitcell etc.).
        ' Hosts that need the count without parsing DecodedSummary
        ' read this directly.
        Public ReadOnly Property DecodedSectorsFound As Integer

        ' Total sectors the codec's layout expects for the track
        ' (Codec.Nsec). 0 when Outcome != Decoded or when the codec
        ' has no sector concept.
        Public ReadOnly Property DecodedSectorsTotal As Integer

        ' Raw sample count of the captured flux (Flux.List.Count).
        ' Mirrors the "(N flux ...)" prefix in FluxSummary; populated
        ' whenever the underlying source is a raw Flux. Nothing when
        ' the source isn't a raw Flux (e.g. MasterTrack or Codec).
        Public ReadOnly Property FluxSampleCount As Nullable(Of Integer)

        ' Total wall-clock duration of the captured flux in
        ' milliseconds (List.Sum() * 1000.0 / SampleFreq). Mirrors the
        ' "... in M.MMms)" suffix in FluxSummary; populated whenever
        ' the underlying source is a raw Flux. Nothing otherwise.
        Public ReadOnly Property FluxDurationMs As Nullable(Of Double)

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

    ' Raised once per (C, H, R, N) tuple whose IDAM CRC was good but the
    ' tuple does not match any predeclared sector entry for the track's
    ' format layout. Mirrors Python read.py's
    '   "T<cyl>.<head>: Ignoring unexpected sector C:<c> H:<h> R:<r> N:<n>"
    ' line, but as structured data: the CLI formatter renders the line
    ' (with track-spec context) and non-CLI hosts can consume the typed
    ' fields directly. May fire multiple times per track when retry
    ' attempts re-decode the flux and re-encounter the same anomaly.
    '
    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.decode_flux
    Public NotInheritable Class ReadUnexpectedSectorEventArgs
        Inherits EventArgs

        Public Sub New(track As ReadTrackInfo, c As Integer, h As Integer, r As Integer, n As Integer)
            Me.Track = track
            Me.C = c
            Me.H = h
            Me.R = r
            Me.N = n
        End Sub

        ' Track address as it appears to a Read subscriber.
        Public ReadOnly Property Track As ReadTrackInfo
        ' Cylinder reported in the unexpected IDAM (the C field).
        Public ReadOnly Property C As Integer
        ' Head reported in the unexpected IDAM (the H field).
        Public ReadOnly Property H As Integer
        ' Sector-id (R) reported in the unexpected IDAM.
        Public ReadOnly Property R As Integer
        ' Size code (N) reported in the unexpected IDAM
        ' (sector size in bytes = 128 << N for valid IBM tracks).
        Public ReadOnly Property N As Integer

    End Class

End Namespace
