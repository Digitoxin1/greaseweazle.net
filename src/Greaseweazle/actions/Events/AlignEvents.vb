Imports Greaseweazle.Shared

Namespace Greaseweazle.Actions

    ' How a single Align read pass interpreted the recovered flux.
    Public Enum AlignReadOutcome
        ' --format wasn't specified; the formatter renders the
        ' flux summary directly.
        NoFormat = 0

        ' The format codec successfully decoded (or partially decoded)
        ' the flux. DecodedSummary is populated.
        Decoded = 1

        ' The format codec rejected the flux as out of range for the
        ' selected format (decoded == Nothing in Python). FormatName is
        ' populated so the formatter can render the WARNING line.
        OutOfRange = 2
    End Enum

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

    ' Raised once when --hard-sectors is enabled and the head-stop
    ' detection round identifies the sector count. Mirrors Python's
    ' "Drive reports {N} hard sectors" line. Never fires in --test
    ' mode (no USB) or when --hard-sectors is off.
    Public NotInheritable Class AlignHardSectorsDetectedEventArgs
        Inherits EventArgs

        Public Sub New(hardSectorCount As Integer)
            Me.HardSectorCount = hardSectorCount
        End Sub

        Public ReadOnly Property HardSectorCount As Integer

    End Class

    ' Raised after each read pass completes — once per read in the
    ' Reads loop. The CLI formatter renders one of three legacy lines
    ' depending on Outcome:
    '   NoFormat:   "{tspec}: {fluxSummary}"
    '   Decoded:    "{tspec}: {decodedSummary} from {fluxSummary}"
    '   OutOfRange: "{tspec}: WARNING: Out of range for format
    '               '{formatName}': No format conversion applied:
    '               {fluxSummary}"
    Public NotInheritable Class AlignReadCompletedEventArgs
        Inherits EventArgs

        Public Sub New(track As TrackIter,
                       outcome As AlignReadOutcome,
                       fluxSummary As String,
                       decodedSummary As String,
                       formatName As String,
                       decodedSectorsFound As Integer,
                       decodedSectorsTotal As Integer,
                       fluxSampleCount As Nullable(Of Integer),
                       fluxDurationMs As Nullable(Of Double))
            Me.Track = track
            Me.Outcome = outcome
            Me.FluxSummary = fluxSummary
            Me.DecodedSummary = decodedSummary
            Me.FormatName = formatName
            Me.DecodedSectorsFound = decodedSectorsFound
            Me.DecodedSectorsTotal = decodedSectorsTotal
            Me.FluxSampleCount = fluxSampleCount
            Me.FluxDurationMs = fluxDurationMs
        End Sub

        Public ReadOnly Property Track As TrackIter
        Public ReadOnly Property Outcome As AlignReadOutcome

        ' Always populated. Comes from Flux.SummaryString() in the
        ' DLL's flux/codec layer (a domain-specific helper, not a
        ' rendering decision).
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

        ' Raw sample count of the captured flux (Flux.List.Count).
        ' Mirrors the "(N flux ...)" prefix in FluxSummary; populated
        ' whenever the underlying source is a raw Flux. Nothing
        ' otherwise.
        Public ReadOnly Property FluxSampleCount As Nullable(Of Integer)

        ' Total wall-clock duration of the captured flux in
        ' milliseconds (List.Sum() * 1000.0 / SampleFreq). Mirrors
        ' the "... in M.MMms)" suffix in FluxSummary; populated
        ' whenever the underlying source is a raw Flux. Nothing
        ' otherwise.
        Public ReadOnly Property FluxDurationMs As Nullable(Of Double)

    End Class

End Namespace
