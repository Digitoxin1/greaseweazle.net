Namespace Greaseweazle.Actions

    ' Track address as it appears to a Write subscriber.
    ' PhysicalCyl/PhysicalHead reflect the drive's physical address —
    ' Write renders "{cyl}.{head} -> Drive {pcyl}.{phead}" when they
    ' differ (note the arrow direction is the inverse of Read/Convert).
    Public NotInheritable Class WriteTrackInfo

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

    ' Why a track is being erased before/instead of being written.
    Public Enum WriteEraseReason
        ' Input image had no track at this (cyl, head) AND --erase-empty
        ' was passed. The track is erased then skipped — no Writing
        ' Track event follows.
        EmptyTrack = 0

        ' --pre-erase was passed; the track is erased before each
        ' write attempt and is followed by a Writing Track event.
        PreErase = 1
    End Enum

    ' Final verify outcome for the run. Renders one of the three
    ' Python footer lines:
    '   "All tracks verified"
    '   "No tracks verified (Reason: Verify {disabled|unavailable})"
    '   "{V} tracks verified; {N} tracks *not* verified (Reason: …)"
    Public Enum WriteVerifyOutcome
        ' Every track that was written verified successfully (no track
        ' fell through to the not-verified path). Renders "All tracks
        ' verified".
        AllVerified = 0

        ' At least one track skipped verification because the user
        ' passed --no-verify. NotVerifiedCount > 0 reflects how many.
        VerifyDisabled = 1

        ' At least one track skipped verification because the codec
        ' for the source track did not provide a verifier (i.e.
        ' MasterTrack.verify is None). NotVerifiedCount > 0.
        VerifyUnavailable = 2
    End Enum

    ' Raised once at the start of a Write run, after the input image
    ' is opened and the track-set/format are resolved. The CLI prints
    ' Python's three header lines: optional "Format <name>",
    ' "Writing <tracks>", and an optional precomp summary line.
    Public NotInheritable Class WriteStartedEventArgs
        Inherits EventArgs

        Public Sub New(formatName As String, tracks As String, precompSummary As String)
            Me.FormatName = formatName
            Me.Tracks = tracks
            Me.PrecompSummary = precompSummary
        End Sub

        ' --format value (or auto-resolved IMG default); Nothing when
        ' no format applies. Subscribers render "Format <name>" only
        ' when this is non-empty.
        Public ReadOnly Property FormatName As String

        ' Compact track-set spec being written.
        Public ReadOnly Property Tracks As String

        ' Pre-formatted Python `__str__` of the PrecompSpec (e.g.
        ' "Precomp MFM, 0-:140ns, 50-:160ns") or Nothing when --precomp
        ' was not supplied. Library consumers can ignore; the CLI
        ' simply echoes the line.
        Public ReadOnly Property PrecompSummary As String

    End Class

    ' Raised when --hard-sectors auto-detection succeeds. CLI prints
    ' "Drive reports {N} hard sectors".
    Public NotInheritable Class WriteHardSectorsEventArgs
        Inherits EventArgs

        Public Sub New(hardSectorCount As Integer)
            Me.HardSectorCount = hardSectorCount
        End Sub

        Public ReadOnly Property HardSectorCount As Integer

    End Class

    ' Raised when a track is erased — either because the input image
    ' didn't have data for it (--erase-empty) or because --pre-erase
    ' was passed before each write attempt. Renders "{tspec}: Erasing
    ' Track".
    Public NotInheritable Class WriteTrackErasingEventArgs
        Inherits EventArgs

        Public Sub New(track As WriteTrackInfo, reason As WriteEraseReason)
            Me.Track = track
            Me.Reason = reason
        End Sub

        Public ReadOnly Property Track As WriteTrackInfo
        Public ReadOnly Property Reason As WriteEraseReason

    End Class

    ' Raised when a format codec rejects an input track. Renders
    ' "{tspec}: WARNING: Out of range for format '{f}': Track skipped".
    Public NotInheritable Class WriteTrackOutOfRangeEventArgs
        Inherits EventArgs

        Public Sub New(track As WriteTrackInfo, formatName As String)
            Me.Track = track
            Me.FormatName = formatName
        End Sub

        Public ReadOnly Property Track As WriteTrackInfo
        Public ReadOnly Property FormatName As String

    End Class

    ' Raised once per write attempt. RetryNumber = 0 on the first
    ' attempt (FluxSummary is populated); RetryNumber > 0 on a verify-
    ' failure retry (FluxSummary is Nothing — the CLI substitutes the
    ' "Verify Failure: Retry #N" suffix).
    Public NotInheritable Class WriteTrackWritingEventArgs
        Inherits EventArgs

        Public Sub New(track As WriteTrackInfo, fluxSummary As String, retryNumber As Integer)
            Me.Track = track
            Me.FluxSummary = fluxSummary
            Me.RetryNumber = retryNumber
        End Sub

        Public ReadOnly Property Track As WriteTrackInfo
        Public ReadOnly Property FluxSummary As String
        Public ReadOnly Property RetryNumber As Integer

    End Class

    ' Raised once at the end of a Write run with the verify tally.
    ' Always fires (live and dry-run) so subscribers can render the
    ' final footer line without having to re-derive the verdict.
    Public NotInheritable Class WriteVerifyOutcomeEventArgs
        Inherits EventArgs

        Public Sub New(outcome As WriteVerifyOutcome, verifiedCount As Integer, notVerifiedCount As Integer)
            Me.Outcome = outcome
            Me.VerifiedCount = verifiedCount
            Me.NotVerifiedCount = notVerifiedCount
        End Sub

        Public ReadOnly Property Outcome As WriteVerifyOutcome
        Public ReadOnly Property VerifiedCount As Integer
        Public ReadOnly Property NotVerifiedCount As Integer

    End Class

End Namespace
