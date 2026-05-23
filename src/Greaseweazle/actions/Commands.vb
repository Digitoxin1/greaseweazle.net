Imports System.Threading
Imports Greaseweazle.Tools

Namespace Greaseweazle.Actions

    ' Top-level façade for the strongly-typed Greaseweazle library API.
    ' Consumers (the CLI front-end and any external app) instantiate the
    ' engine once and then invoke per-action commands. Each property returns
    ' a fresh command instance because commands are stateful (they raise
    ' events) and a single instance is meant to drive a single Run().
    Public Class GreaseweazleEngine

        Public ReadOnly Property Info As New InfoCommand()
        Public ReadOnly Property Read As New ReadCommand()
        Public ReadOnly Property Write As New WriteCommand()
        Public ReadOnly Property Convert As New ConvertCommand()
        Public ReadOnly Property [Erase] As New EraseCommand()
        Public ReadOnly Property Clean As New CleanCommand()
        Public ReadOnly Property Seek As New SeekCommand()
        Public ReadOnly Property Delays As New DelaysCommand()
        Public ReadOnly Property Update As New UpdateCommand()
        Public ReadOnly Property Pin As New PinCommand()
        Public ReadOnly Property Reset As New ResetCommand()
        Public ReadOnly Property Bandwidth As New BandwidthCommand()
        Public ReadOnly Property Rpm As New RpmCommand()
        Public ReadOnly Property Align As New AlignCommand()

    End Class

    ' Each *Command class below offers a strongly-typed Run(options, [ct])
    ' entry point and inherits the common `Status` event from GwCommandBase.
    ' For the initial split, Run() reuses the validated algorithm bodies in
    ' tools/*.vb via XAction.RunFromOptions(opts, ctx) — this guarantees
    ' byte-for-byte parity with the previous unified exe. Subsequent work can
    ' add specialised per-event types (e.g. TrackRead, FlashProgress) on top
    ' of the same Status pipeline without breaking the public surface.
    ' One-shot command. Reads device info (model, MCU, firmware version,
    ' USB speed) and optionally checks GitHub for a newer firmware release.
    ' Returns a typed DeviceInfoResult; CmdError can be thrown by the
    ' underlying USB layer (rare; usually the algorithm catches port-open
    ' failures itself and surfaces them via DeviceConnectionState.NotFound).
    Public Class InfoCommand
        Inherits GwCommandBase

        Public Function Run(options As InfoOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As DeviceInfoResult
            ThrowIfCancellationRequested(cancellationToken)
            Return InfoAction.RunFromOptions(options)
        End Function

        ' Enumerates every Greaseweazle currently connected. Each device is
        ' opened, probed read-only, and closed; ports that fail to open are
        ' silently skipped. The returned list is sorted by ScorePort
        ' descending so `list.FirstOrDefault()` matches the device that
        ' Run(InfoOptions) would have picked. Returns an empty list when no
        ' devices are detected.
        '
        ' No network calls are made: every entry's Device.FirmwareUpdate is
        ' Nothing. For targeted single-device probing (or for --test,
        ' --bootloader, or firmware-update banner support), use
        ' Run(InfoOptions) instead.
        Public Function EnumerateDevices(Optional cancellationToken As CancellationToken = Nothing) As IReadOnlyList(Of DeviceInfoResult)
            ThrowIfCancellationRequested(cancellationToken)
            Return InfoAction.EnumerateDevices()
        End Function

    End Class

    ' Streaming command. Reads each track in ReadOptions.TrackSet,
    ' optionally retries on missing sectors, decodes via --format
    ' codec, and writes to the resolved output image. Subscribers
    ' receive structured events for the run header, hard-sector
    ' detection, each per-track read attempt, retry give-ups, and
    ' the final sector grid. Returns a ReadSummary on completion;
    ' CmdError is raised by the underlying USB layer.
    Public Class ReadCommand
        Inherits GwCommandBase

        ' Raised once at the start, with resolved tracks/revs/format —
        ' formatter prints "Reading <tracks> revs=<n>" + "Format <name>".
        Public Event Started As EventHandler(Of ReadStartedEventArgs)

        ' Raised when --hard-sectors auto-detection succeeds.
        Public Event HardSectorsDetected As EventHandler(Of HardSectorsDetectedEventArgs)

        ' Raised once per read attempt — initial reads have Retry=0,
        ' retries have Retry>0 (and SeekRetry counts whole-disk seeks).
        Public Event TrackProcessed As EventHandler(Of TrackProcessedEventArgs)

        ' Raised when the retry budget is exhausted with sectors
        ' still missing.
        Public Event TrackGaveUp As EventHandler(Of ReadTrackGaveUpEventArgs)

        ' Raised once after all tracks decode, when --format produced
        ' a non-empty grid. Carries the typed SectorSummaryGrid.
        Public Event SummaryReady As EventHandler(Of SectorSummaryReadyEventArgs)

        ' Raised once per (C, H, R, N) tuple whose IDAM CRC was good
        ' but the tuple does not match any predeclared sector entry
        ' for the track's format layout. May fire multiple times for
        ' the same anomaly when retry attempts re-decode the flux.
        ' Mirrors Python read.py's "Ignoring unexpected sector ..."
        ' print, but the CLI formatter renders the line so the
        ' library produces no console output.
        Public Event UnexpectedSectorIgnored As EventHandler(Of UnexpectedSectorEventArgs)

        ' Raised per duplicate entry in ReadOptions.AdditionalFiles that
        ' was silently skipped at sink-build time. Fires before any USB
        ' activity. Subscribers that don't care about dedup behaviour can
        ' ignore this event — the run continues with the non-duplicate
        ' sinks exactly as if the caller had pre-filtered the list.
        Public Event AdditionalOutputDeduped As EventHandler(Of ReadAdditionalOutputDedupedEventArgs)

        Public Function Run(options As ReadOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As ReadSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return ReadAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnStarted(args As ReadStartedEventArgs)
            RaiseEvent Started(Me, args)
        End Sub

        Friend Sub OnHardSectorsDetected(args As HardSectorsDetectedEventArgs)
            RaiseEvent HardSectorsDetected(Me, args)
        End Sub

        Friend Sub OnTrackProcessed(args As TrackProcessedEventArgs)
            RaiseEvent TrackProcessed(Me, args)
        End Sub

        Friend Sub OnTrackGaveUp(args As ReadTrackGaveUpEventArgs)
            RaiseEvent TrackGaveUp(Me, args)
        End Sub

        Friend Sub OnSummaryReady(args As SectorSummaryReadyEventArgs)
            RaiseEvent SummaryReady(Me, args)
        End Sub

        Friend Sub OnUnexpectedSectorIgnored(args As UnexpectedSectorEventArgs)
            RaiseEvent UnexpectedSectorIgnored(Me, args)
        End Sub

        Friend Sub OnAdditionalOutputDeduped(args As ReadAdditionalOutputDedupedEventArgs)
            RaiseEvent AdditionalOutputDeduped(Me, args)
        End Sub

    End Class

    ' Streaming command. Reads each track from WriteOptions.FileName,
    ' decodes via --format codec (when supplied), scales the flux to
    ' the drive's measured speed, and writes (with optional verify +
    ' retries) to the connected unit. Subscribers receive structured
    ' events for the run header, hard-sector probe, per-track erase,
    ' decode-out-of-range, write attempts (initial + verify retries),
    ' and the final verify outcome. Returns a WriteSummary on
    ' completion; CmdError is raised by the underlying USB layer.
    Public Class WriteCommand
        Inherits GwCommandBase

        ' Raised once at the start, with resolved tracks/format/precomp —
        ' formatter prints the optional Format line, "Writing <tracks>",
        ' and the optional precomp summary line.
        Public Event Started As EventHandler(Of WriteStartedEventArgs)

        ' Raised when --hard-sectors auto-detection succeeds.
        Public Event HardSectorsDetected As EventHandler(Of HardSectorsDetectedEventArgs)

        ' Raised when a track is erased (empty source + --erase-empty,
        ' or before each write attempt with --pre-erase).
        Public Event TrackErasing As EventHandler(Of WriteTrackErasingEventArgs)

        ' Raised when a format codec rejects an input track. The track
        ' is skipped — no TrackWriting event follows.
        Public Event TrackOutOfRange As EventHandler(Of WriteTrackOutOfRangeEventArgs)

        ' Raised once per write attempt — first attempt (RetryNumber=0)
        ' and then once per verify-failure retry.
        Public Event TrackWriting As EventHandler(Of WriteTrackWritingEventArgs)

        ' Raised once at the end of the run with the verify tally.
        Public Event VerifyCompleted As EventHandler(Of WriteVerifyOutcomeEventArgs)

        ' Raised once per (C, H, R, N) tuple whose IDAM CRC was good
        ' but the tuple does not match any predeclared sector entry
        ' for the track's format layout, encountered while decoding
        ' the input image (PrepareSourceTrack). The CLI formatter
        ' renders the per-track line; the library produces no
        ' console output.
        Public Event UnexpectedSectorIgnored As EventHandler(Of UnexpectedSectorEventArgs)

        Public Function Run(options As WriteOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As WriteSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return WriteAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnStarted(args As WriteStartedEventArgs)
            RaiseEvent Started(Me, args)
        End Sub

        Friend Sub OnHardSectorsDetected(args As HardSectorsDetectedEventArgs)
            RaiseEvent HardSectorsDetected(Me, args)
        End Sub

        Friend Sub OnTrackErasing(args As WriteTrackErasingEventArgs)
            RaiseEvent TrackErasing(Me, args)
        End Sub

        Friend Sub OnTrackOutOfRange(args As WriteTrackOutOfRangeEventArgs)
            RaiseEvent TrackOutOfRange(Me, args)
        End Sub

        Friend Sub OnTrackWriting(args As WriteTrackWritingEventArgs)
            RaiseEvent TrackWriting(Me, args)
        End Sub

        Friend Sub OnVerifyCompleted(args As WriteVerifyOutcomeEventArgs)
            RaiseEvent VerifyCompleted(Me, args)
        End Sub

        Friend Sub OnUnexpectedSectorIgnored(args As UnexpectedSectorEventArgs)
            RaiseEvent UnexpectedSectorIgnored(Me, args)
        End Sub

    End Class

    ' Streaming command. Walks the resolved output trackset, decoding
    ' each input track via the matching codec, and emits structured
    ' events the CLI formatter (or any subscriber) translates back
    ' into Python's per-track lines. Final SummaryReady event carries
    ' a typed SectorSummaryGrid; the same grid is also returned on
    ' the ConvertSummary.
    Public Class ConvertCommand
        Inherits GwCommandBase

        ' Raised once before any per-track work — formatter prints
        ' the "Format <name>" + "Converting <in> -> <out>" header.
        Public Event Started As EventHandler(Of ConvertStartedEventArgs)

        ' Raised when --hard-sectors successfully identifies the
        ' hard-sector count for a track.
        Public Event HardSectorsApplied As EventHandler(Of ConvertHardSectorsEventArgs)

        ' Raised once per processed input track (NoFormat / Decoded /
        ' OutOfRange) so the formatter can stream Python's
        ' per-track text.
        Public Event TrackProcessed As EventHandler(Of TrackProcessedEventArgs)

        ' Raised at the end of the run with the post-decode sector
        ' grid. Subscribers render the "Cyl-> / H. S: / .X" table.
        Public Event SummaryReady As EventHandler(Of SectorSummaryReadyEventArgs)

        ' Raised once per (C, H, R, N) tuple whose IDAM CRC was good
        ' but the tuple does not match any predeclared sector entry
        ' for the track's format layout, encountered while decoding
        ' the input image. The CLI formatter renders the per-track
        ' line; the library produces no console output.
        Public Event UnexpectedSectorIgnored As EventHandler(Of UnexpectedSectorEventArgs)

        Public Function Run(options As ConvertOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As ConvertSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return ConvertAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnStarted(args As ConvertStartedEventArgs)
            RaiseEvent Started(Me, args)
        End Sub

        Friend Sub OnHardSectorsApplied(args As ConvertHardSectorsEventArgs)
            RaiseEvent HardSectorsApplied(Me, args)
        End Sub

        Friend Sub OnTrackProcessed(args As TrackProcessedEventArgs)
            RaiseEvent TrackProcessed(Me, args)
        End Sub

        Friend Sub OnSummaryReady(args As SectorSummaryReadyEventArgs)
            RaiseEvent SummaryReady(Me, args)
        End Sub

        Friend Sub OnUnexpectedSectorIgnored(args As UnexpectedSectorEventArgs)
            RaiseEvent UnexpectedSectorIgnored(Me, args)
        End Sub

    End Class

    ' Streaming command. Erases each track in EraseOptions.TrackSet and
    ' raises a typed event before each track so subscribers can render
    ' progress. Returns an EraseSummary when the run completes; throws
    ' CmdError on a recoverable USB error (CLI catches and prints
    ' "Command Failed: %s") and OperationCanceledException on Ctrl-C.
    Public Class EraseCommand
        Inherits GwCommandBase

        ' Raised once before any USB activity, with the resolved track-set
        ' string and revs count. Always fires (live and dry-run).
        Public Event Started As EventHandler(Of EraseStartedEventArgs)

        ' Raised before each physical track is erased. Only fires in the
        ' live path (--test stops after Started).
        Public Event TrackStarted As EventHandler(Of EraseTrackEventArgs)

        Public Function Run(options As EraseOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As EraseSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return EraseAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnStarted(args As EraseStartedEventArgs)
            RaiseEvent Started(Me, args)
        End Sub

        Friend Sub OnTrackStarted(args As EraseTrackEventArgs)
            RaiseEvent TrackStarted(Me, args)
        End Sub

    End Class

    ' Streaming command. Walks each clean pass, raising structured events
    ' before/during/after each pass so the CLI (or any subscriber) can
    ' render Python's "Pass {N}: {cyls...} " lines incrementally as the
    ' drive head moves. Returns a CleanSummary on completion; throws
    ' CmdError on a recoverable USB error.
    Public Class CleanCommand
        Inherits GwCommandBase

        ' Raised at the start of every pass — formatter writes "Pass N: ".
        Public Event PassStarted As EventHandler(Of CleanPassStartedEventArgs)

        ' Raised after each cylinder is reached (clamped value) so the
        ' formatter can append "{cyl} " to the in-progress pass line.
        Public Event CylinderSeeked As EventHandler(Of CleanCylinderEventArgs)

        ' Raised at the end of every pass — formatter writes a newline.
        Public Event PassCompleted As EventHandler(Of CleanPassCompletedEventArgs)

        Public Function Run(options As CleanOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As CleanSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return CleanAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnPassStarted(args As CleanPassStartedEventArgs)
            RaiseEvent PassStarted(Me, args)
        End Sub

        Friend Sub OnCylinderSeeked(args As CleanCylinderEventArgs)
            RaiseEvent CylinderSeeked(Me, args)
        End Sub

        Friend Sub OnPassCompleted(args As CleanPassCompletedEventArgs)
            RaiseEvent PassCompleted(Me, args)
        End Sub

    End Class

    ' Interactive command. When the requested cylinder is "extreme" (cyl < 0
    ' OR cyl > 83) and --force is not set, the algorithm calls the supplied
    ' ISeekPrompter to confirm. If no prompter is wired up the algorithm
    ' aborts (default-deny) — callers that always want to proceed can
    ' install a prompter that returns True unconditionally.
    Public Class SeekCommand
        Inherits GwCommandBase

        ' UI plug-point. Set to a ConsoleSeekPrompter (CLI default) or any
        ' custom ISeekPrompter implementation for non-interactive hosts.
        Public Property Prompter As ISeekPrompter

        Public Function Run(options As SeekOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As SeekResult
            ThrowIfCancellationRequested(cancellationToken)
            Return SeekAction.RunFromOptions(options, Prompter)
        End Function

    End Class

    ' One-shot. The algorithm reads the current delay block (and optionally
    ' applies --select/--step/... overrides before re-reading), then returns
    ' a typed DelaysResult. Returns `Nothing` for --test dry-run mode — the
    ' CLI renders no output for a Nothing result.
    Public Class DelaysCommand
        Inherits GwCommandBase

        Public Function Run(options As DelaysOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As DelaysResult
            ThrowIfCancellationRequested(cancellationToken)
            Return DelaysAction.RunFromOptions(options)
        End Function

    End Class

    ' Streaming command. Downloads (or loads from disk) a firmware
    ' payload, validates it against the connected device, and flashes
    ' the relevant slot. Two events fire during the run:
    '   DownloadStarted before the GitHub asset GET
    '   UpdateStarted   before the device is flashed
    ' On completion Run returns an UpdateSummary describing the outcome
    ' (Completed/Failed/Skipped/DryRun); CmdError propagates for
    ' unrecoverable USB errors so the CLI can format ack-specific
    ' messages from ex.Code.
    Public Class UpdateCommand
        Inherits GwCommandBase

        Public Event DownloadStarted As EventHandler(Of UpdateDownloadStartedEventArgs)
        Public Event UpdateStarted As EventHandler(Of UpdateStartedEventArgs)

        Public Function Run(options As UpdateOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As UpdateSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return UpdateAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnDownloadStarted(args As UpdateDownloadStartedEventArgs)
            RaiseEvent DownloadStarted(Me, args)
        End Sub

        Friend Sub OnUpdateStarted(args As UpdateStartedEventArgs)
            RaiseEvent UpdateStarted(Me, args)
        End Sub

    End Class

    ' One-shot command. The algorithm performs at most one USB round-trip
    ' (set or get) and returns a typed PinResult; the recoverable USB error
    ' path throws CmdError instead of rendering "Command Failed: ..." text.
    Public Class PinCommand
        Inherits GwCommandBase

        Public Function Run(options As PinOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As PinResult
            ThrowIfCancellationRequested(cancellationToken)
            Return PinAction.RunFromOptions(options)
        End Function

    End Class

    ' One-shot command. No progress to report, so no events: the algorithm
    ' either runs to completion or throws (CmdError on a recoverable USB
    ' protocol error, OperationCanceledException / KeyboardInterruptException
    ' on Ctrl-C). Library consumers wrap Run() in their own try/catch; the
    ' CLI front-end translates CmdError to the `Command Failed: ...` line.
    Public Class ResetCommand
        Inherits GwCommandBase

        Public Sub Run(options As ResetOptions,
                       Optional cancellationToken As CancellationToken = Nothing)
            ThrowIfCancellationRequested(cancellationToken)
            ResetAction.RunFromOptions(options)
        End Sub

    End Class

    ' One-shot. Performs a single bidirectional USB throughput measurement
    ' (1 MB host->device + 1 MB device->host) and returns a typed
    ' BandwidthResult. Returns Nothing for --test dry-run mode.
    Public Class BandwidthCommand
        Inherits GwCommandBase

        Public Function Run(options As BandwidthOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As BandwidthResult
            ThrowIfCancellationRequested(cancellationToken)
            Return BandwidthAction.RunFromOptions(options)
        End Function

    End Class

    ' Streaming command. Reads the spindle index period N times, raising
    ' SampleMeasured per sample so subscribers can render Python's
    ' "Rate: ... rpm ; Period: ... ms" lines incrementally. Once the loop
    ' terminates (success or partial), if at least 2 samples were
    ' collected SummaryReady fires with the FASTEST/Mean/Median/SLOWEST
    ' stats. Run() then returns the same RpmSummary on success; on a
    ' recoverable USB error CmdError propagates AFTER SummaryReady, so
    ' the CLI prints the summary and then the "Command Failed: ..." line.
    Public Class RpmCommand
        Inherits GwCommandBase

        ' Raised once per Nr loop iteration with the measured period.
        Public Event SampleMeasured As EventHandler(Of RpmSampleMeasuredEventArgs)

        ' Raised in the Finally of the per-sample loop when there are
        ' at least 2 samples — a single sample produces no summary in
        ' Python and we mirror that here.
        Public Event SummaryReady As EventHandler(Of RpmSummaryReadyEventArgs)

        Public Function Run(options As RpmOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As RpmSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return RpmAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnSampleMeasured(args As RpmSampleMeasuredEventArgs)
            RaiseEvent SampleMeasured(Me, args)
        End Sub

        Friend Sub OnSummaryReady(args As RpmSummaryReadyEventArgs)
            RaiseEvent SummaryReady(Me, args)
        End Sub

    End Class

    ' Streaming command. Repeatedly reads the same set of tracks for
    ' drive alignment work. Three events fire during a run:
    '   Started               (header — once, after live-mode adjustments)
    '   HardSectorsDetected   (optional — once, when --hard-sectors active)
    '   ReadCompleted         (per read pass)
    ' Run returns an AlignSummary on completion; CmdError propagates so
    ' the CLI surfaces the standard "Command Failed: ..." line.
    Public Class AlignCommand
        Inherits GwCommandBase

        Public Event Started As EventHandler(Of AlignStartedEventArgs)
        Public Event HardSectorsDetected As EventHandler(Of HardSectorsDetectedEventArgs)
        Public Event ReadCompleted As EventHandler(Of TrackProcessedEventArgs)

        Public Function Run(options As AlignOptions,
                            Optional cancellationToken As CancellationToken = Nothing) As AlignSummary
            ThrowIfCancellationRequested(cancellationToken)
            Return AlignAction.RunFromOptions(options, Me, cancellationToken)
        End Function

        Friend Sub OnStarted(args As AlignStartedEventArgs)
            RaiseEvent Started(Me, args)
        End Sub

        Friend Sub OnHardSectorsDetected(args As HardSectorsDetectedEventArgs)
            RaiseEvent HardSectorsDetected(Me, args)
        End Sub

        Friend Sub OnReadCompleted(args As TrackProcessedEventArgs)
            RaiseEvent ReadCompleted(Me, args)
        End Sub

    End Class

End Namespace
