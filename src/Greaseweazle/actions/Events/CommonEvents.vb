Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Actions

    ' Track address as it appears to a Read/Write/Convert subscriber.
    ' PhysicalCyl/PhysicalHead reflect the physical address of the
    ' source/sink — the drive's physical address for Read/Write, or
    ' the input image's geometry for Convert. The CLI formatters
    ' encode the per-context wording (Read renders
    ' "{cyl}.{head} <- Drive {pcyl}.{phead}", Convert renders
    ' "<- Image", and Write renders "-> Drive") — the type itself is
    ' role-agnostic.
    Public NotInheritable Class TrackInfo

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

        Public Shared Function FromTrackIter(t As TrackIter) As TrackInfo
            Return New TrackInfo(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)
        End Function

        Public Shared Function FromTrackIdentity(t As TrackIdentity) As TrackInfo
            Return New TrackInfo(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)
        End Function

    End Class

    ' Raised once per (C, H, R, N) tuple whose IDAM CRC was good but the
    ' tuple does not match any predeclared sector entry for the track's
    ' format layout. Mirrors Python's
    '   "T<cyl>.<head>: Ignoring unexpected sector C:<c> H:<h> R:<r> N:<n>"
    ' line, but as structured data — the CLI formatter renders the line
    ' (with track-spec context) and non-CLI hosts can consume the typed
    ' fields directly. May fire multiple times per track when retry
    ' attempts re-decode the flux and re-encounter the same anomaly.
    '
    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.decode_flux
    Public NotInheritable Class UnexpectedSectorEventArgs
        Inherits EventArgs

        Public Sub New(track As TrackInfo, c As Integer, h As Integer, r As Integer, n As Integer)
            Me.Track = track
            Me.C = c
            Me.H = h
            Me.R = r
            Me.N = n
        End Sub

        ' Track address as it appears to the subscriber.
        Public ReadOnly Property Track As TrackInfo
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

    ' How a single track decode pass was interpreted. Shared by Read,
    ' Convert, and Align flows so subscribers that handle more than one
    ' command can dispatch on a single enum.
    Public Enum TrackDecodeOutcome
        ' --format wasn't supplied (or the input track is already a
        ' Codec) — formatters render the flux summary directly.
        NoFormat = 0

        ' Format codec successfully decoded the flux. DecodedSummary on
        ' the carrying event-args is populated.
        Decoded = 1

        ' Format codec rejected the flux: the track is skipped and the
        ' WARNING line is emitted by formatters.
        OutOfRange = 2
    End Enum

    ' Raised once when --hard-sectors auto-detects the count from the
    ' drive at the start of a Read/Write/Align run. CLI formatters
    ' render Python's "Drive reports N hard sectors" line.
    Public NotInheritable Class HardSectorsDetectedEventArgs
        Inherits EventArgs

        Public Sub New(hardSectorCount As Integer)
            Me.HardSectorCount = hardSectorCount
        End Sub

        Public ReadOnly Property HardSectorCount As Integer

    End Class

    ' Raised once at the end of a Read/Convert run when --format produced
    ' a non-empty decode dict. Subscribers render Python's "Cyl-> / H. S:
    ' / .X" grid + "Found N sectors of M" tally — or surface the typed
    ' grid in a UI.
    Public NotInheritable Class SectorSummaryReadyEventArgs
        Inherits EventArgs

        Public Sub New(grid As SectorSummaryGrid)
            Me.Grid = grid
        End Sub

        Public ReadOnly Property Grid As SectorSummaryGrid

    End Class

    ' Raised after each track decode pass — once per attempt for Read
    ' (initial + each retry, with SeekRetry/Retry > 0 on retries), once
    ' per input track for Convert, once per read pass for Align. CLI
    ' formatters render one of three legacy lines per Outcome:
    '   NoFormat:   "{tspec}: {fluxSummary}"
    '   Decoded:    "{tspec}: {decodedSummary} from {fluxSummary}"
    '               (Read appends " (Retry #SeekRetry.Retry)" when
    '               Retry <> 0)
    '   OutOfRange: "{tspec}: WARNING: Out of range for format
    '               '{formatName}': ..."
    Public NotInheritable Class TrackProcessedEventArgs
        Inherits EventArgs

        Public Sub New(track As TrackInfo,
                       outcome As TrackDecodeOutcome,
                       fluxSummary As String,
                       decodedSummary As String,
                       formatName As String,
                       decodedSectorsFound As Integer,
                       decodedSectorsTotal As Integer,
                       fluxSampleCount As Nullable(Of Integer),
                       fluxDurationMs As Nullable(Of Double),
                       Optional seekRetry As Integer = 0,
                       Optional retry As Integer = 0)
            Me.Track = track
            Me.Outcome = outcome
            Me.FluxSummary = fluxSummary
            Me.DecodedSummary = decodedSummary
            Me.FormatName = formatName
            Me.DecodedSectorsFound = decodedSectorsFound
            Me.DecodedSectorsTotal = decodedSectorsTotal
            Me.FluxSampleCount = fluxSampleCount
            Me.FluxDurationMs = fluxDurationMs
            Me.SeekRetry = seekRetry
            Me.Retry = retry
        End Sub

        Public ReadOnly Property Track As TrackInfo
        Public ReadOnly Property Outcome As TrackDecodeOutcome

        ' Always populated. Comes from the underlying track's
        ' SummaryString() in the DLL's flux/codec layer.
        Public ReadOnly Property FluxSummary As String

        ' Populated only when Outcome = Decoded.
        Public ReadOnly Property DecodedSummary As String

        ' Populated only when Outcome = OutOfRange.
        Public ReadOnly Property FormatName As String

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

        ' Raw sample count of the captured/source flux (Flux.List.Count).
        ' Mirrors the "(N flux ...)" prefix in FluxSummary; populated
        ' whenever the underlying source is a raw Flux. Nothing when
        ' the source isn't a raw Flux (e.g. MasterTrack or Codec).
        Public ReadOnly Property FluxSampleCount As Nullable(Of Integer)

        ' Total wall-clock duration of the captured/source flux in
        ' milliseconds (List.Sum() * 1000.0 / SampleFreq). Mirrors the
        ' "... in M.MMms)" suffix in FluxSummary; populated whenever
        ' the underlying source is a raw Flux. Nothing otherwise.
        Public ReadOnly Property FluxDurationMs As Nullable(Of Double)

        ' Whole-disk seek retry counter. Always 0 for Convert/Align
        ' (no retry loop). For Read: 0 on the first attempt; > 0 on
        ' subsequent whole-disk seek-retry passes.
        Public ReadOnly Property SeekRetry As Integer

        ' Per-track read attempt index. Always 0 for Convert/Align
        ' (no retry loop). For Read: 0 on the first attempt; > 0 on
        ' subsequent retries within the current SeekRetry pass. The
        ' CLI appends " (Retry #SeekRetry.Retry)" when Retry <> 0.
        Public ReadOnly Property Retry As Integer

    End Class

End Namespace
