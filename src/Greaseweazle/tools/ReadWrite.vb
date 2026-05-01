Imports Greaseweazle.Core
Imports Greaseweazle.Codecs
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Images
Imports Greaseweazle.Shared
Imports System.IO

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `read` action.
    '
    ' TrackSet is a TrackSetSpec (partial / user intent): the parser captures
    ' only the keys the user named in --tracks, and the engine folds them
    ' against the format defaults inside RunFromOptions via
    ' TrackResolution.ResolveSpec. Hosts can leave it Nothing to accept
    ' format defaults verbatim, or build one with `New TrackSetSpec("c=0-1")`.
    Public Class ReadOptions
        Public Property FileName As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property TrackSet As TrackSetSpec
        Public Property Revs As Integer
        ' Python read.py:174-184 keeps a fractional default_revs as a float and converts
        ' it to a tick budget at runtime (after measuring drive ticks-per-rev). When the
        ' format provides a non-integral default_revs, this property holds the raw float
        ' so the live path can reproduce Python's "ticks = drive_tpr * fractional_revs;
        ' revs = 2" collapse.
        Public Property FractionalRevs As Nullable(Of Double)
        ' Python prints `str(args.revs)` directly, which preserves the float form for
        ' fractional defaults (e.g. "revs=1.1"). We capture the formatted string here so
        ' the printer doesn't have to re-derive it.
        Public Property RevsDisplay As String
        Public Property Raw As Boolean
        Public Property HardSectors As Boolean
        Public Property Reverse As Boolean
        Public Property NoClobber As Boolean
        Public Property AdjustSpeed As Nullable(Of Double)
        Public Property PllProfiles As IReadOnlyList(Of Pll)
        Public Property FakeIndexPeriod As Nullable(Of Double)
        Public Property GenTg43 As Boolean
        Public Property Densel As Nullable(Of Boolean)
        Public Property Retries As Integer
        Public Property SeekRetries As Integer
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Strongly-typed options for the `write` action.
    '
    ' TrackSet is a TrackSetSpec (partial / user intent) — see ReadOptions
    ' for the rationale. The engine resolves it against format defaults
    ' inside RunFromOptions.
    Public Class WriteOptions
        Public Property FileName As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property Precomp As String
        Public Property PrecompSpec As String
        Public Property TrackSet As TrackSetSpec
        Public Property PreErase As Boolean
        Public Property EraseEmpty As Boolean
        Public Property HardSectors As Boolean
        Public Property NoVerify As Boolean
        Public Property Reverse As Boolean
        Public Property GenTg43 As Boolean
        Public Property Densel As Nullable(Of Boolean)
        Public Property FakeIndexPeriod As Nullable(Of Double)
        Public Property Retries As Integer
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/read.py + src/greaseweazle/tools/write.py (direct algorithm parity for argument/runtime shaping).
    Public NotInheritable Class ReadWrite

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/read.py::open_image
        ' Python map: src/greaseweazle/tools/write.py::open_image
        Public Shared Function OpenImage(Of T As {Image, New})(fileName As String,
                                                               format As DiskDef,
                                                               noClobber As Boolean,
                                                               fileOpts As IDictionary(Of String, String)) As T
            Dim imageObj = Greaseweazle.Images.Image.ToFile(Of T)(fileName, format, noClobber, fileOpts)
            imageObj.WriteOnCtrlC = True
            Return imageObj
        End Function

        ' Python map: src/greaseweazle/tools/read.py::read_and_normalise
        Public Shared Function ReadAndNormalise(usbClient As Unit,
                                                revs As Integer,
                                                ticks As Integer,
                                                driveTicksPerRev As Nullable(Of Double),
                                                reverse As Boolean,
                                                hardSectors As Boolean,
                                                raw As Boolean,
                                                adjustSpeed As Nullable(Of Double),
                                                Optional fakeIndexPeriod As Nullable(Of Double) = Nothing) As Flux
            Return Align.ReadAndNormalise(usbClient,
                                          revs,
                                          ticks,
                                          driveTicksPerRev,
                                          reverse,
                                          hardSectors,
                                          raw,
                                          adjustSpeed,
                                          fakeIndexPeriod)
        End Function

        ' Python map: src/greaseweazle/tools/read.py::read_with_retry
        '
        ' Reads (and optionally decodes/retries) one track. Two
        ' callbacks expose progress without the algorithm producing
        ' any text:
        '   onTrackProcessed - fires once on the initial read AND
        '                      once per retry attempt
        '   onTrackGaveUp    - fires only when the retry budget is
        '                      exhausted with sectors still missing
        '   onUnexpectedSector - fires once per unique (C,H,R,N) tuple
        '                      drained from a HasDecodeDiagnostics codec
        '                      after each DecodeFlux pass; mirrors the
        '                      Python "Ignoring unexpected sector ..."
        '                      print but as structured data.
        ' Returns (flux, decoded-or-Nothing) so the caller can decide
        ' which artefact to emit to its output image.
        Public Shared Function ReadWithRetry(usbClient As Unit,
                                             t As TrackIter,
                                             revs As Integer,
                                             onTrackProcessed As Action(Of Greaseweazle.Actions.TrackProcessedEventArgs),
                                             onTrackGaveUp As Action(Of Greaseweazle.Actions.ReadTrackGaveUpEventArgs),
                                             Optional fmtCls As DiskDef = Nothing,
                                             Optional formatName As String = Nothing,
                                             Optional raw As Boolean = False,
                                             Optional hardSectors As Boolean = False,
                                             Optional reverse As Boolean = False,
                                             Optional adjustSpeed As Nullable(Of Double) = Nothing,
                                             Optional fakeIndexPeriod As Nullable(Of Double) = Nothing,
                                             Optional ticks As Integer = 0,
                                             Optional driveTicksPerRev As Nullable(Of Double) = Nothing,
                                             Optional retries As Integer = 3,
                                             Optional seekRetries As Integer = 0,
                                             Optional genTg43 As Boolean = False,
                                             Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing,
                                             Optional onUnexpectedSector As Action(Of Greaseweazle.Actions.UnexpectedSectorEventArgs) = Nothing) As Tuple(Of Flux, HasFlux)
            Dim trackInfo = Greaseweazle.Actions.TrackInfo.FromTrackIter(t)
            usbClient.Seek(t.PhysicalCyl, t.PhysicalHead)
            If genTg43 Then
                usbClient.SetPin(2, t.Cyl < 60)
            End If
            Dim flux = ReadAndNormalise(usbClient,
                                        revs:=revs,
                                        ticks:=ticks,
                                        driveTicksPerRev:=driveTicksPerRev,
                                        reverse:=reverse,
                                        hardSectors:=hardSectors,
                                        raw:=raw,
                                        adjustSpeed:=adjustSpeed,
                                        fakeIndexPeriod:=fakeIndexPeriod)
            If fmtCls Is Nothing Then
                If onTrackProcessed IsNot Nothing Then
                    onTrackProcessed(New Greaseweazle.Actions.TrackProcessedEventArgs(
                        trackInfo,
                        Greaseweazle.Actions.TrackDecodeOutcome.NoFormat,
                        flux.SummaryString(), Nothing, Nothing, 0, 0,
                        flux.List.Count,
                        flux.List.Sum() * 1000.0 / flux.SampleFreq))
                End If
                Return Tuple.Create(flux, CType(flux, HasFlux))
            End If

            Dim profiles = If(pllProfiles, Plls.Values)
            ' Python: args.fmt_cls.decode_flux(cyl, head, flux) implicitly reads
            ' plls[0]; with --pll the user's override has been inserted there.
            ' Pass profiles(0) explicitly so the first decode honours --pll.
            Dim firstPll As Pll = If(profiles.Count > 0, profiles(0), Nothing)
            Dim dat = fmtCls.DecodeFlux(t.Cyl, t.Head, flux, firstPll)
            If dat Is Nothing Then
                If onTrackProcessed IsNot Nothing Then
                    onTrackProcessed(New Greaseweazle.Actions.TrackProcessedEventArgs(
                        trackInfo,
                        Greaseweazle.Actions.TrackDecodeOutcome.OutOfRange,
                        flux.SummaryString(), Nothing, If(formatName, String.Empty), 0, 0,
                        flux.List.Count,
                        flux.List.Sum() * 1000.0 / flux.SampleFreq))
                End If
                Return Tuple.Create(flux, CType(Nothing, HasFlux))
            End If
            DrainUnexpectedSectors(dat, trackInfo, onUnexpectedSector)
            For i = 1 To profiles.Count - 1
                If dat.NrMissing() = 0 Then
                    Exit For
                End If
                dat.DecodeFlux(flux, profiles(i))
                DrainUnexpectedSectors(dat, trackInfo, onUnexpectedSector)
            Next

            Dim seekRetry = 0
            Dim retry = 0
            While True
                If onTrackProcessed IsNot Nothing Then
                    onTrackProcessed(New Greaseweazle.Actions.TrackProcessedEventArgs(
                        trackInfo,
                        Greaseweazle.Actions.TrackDecodeOutcome.Decoded,
                        flux.SummaryString(),
                        dat.SummaryString(),
                        Nothing,
                        dat.Nsec - dat.NrMissing(),
                        dat.Nsec,
                        flux.List.Count,
                        flux.List.Sum() * 1000.0 / flux.SampleFreq,
                        seekRetry, retry))
                End If
                If dat.NrMissing() = 0 Then
                    Exit While
                End If
                If retries = 0 OrElse (retry Mod retries) = 0 Then
                    If retries = 0 OrElse seekRetry > seekRetries Then
                        If onTrackGaveUp IsNot Nothing Then
                            onTrackGaveUp(New Greaseweazle.Actions.ReadTrackGaveUpEventArgs(trackInfo, dat.NrMissing()))
                        End If
                        Exit While
                    End If
                    If retry <> 0 Then
                        usbClient.Seek(0, 0)
                        usbClient.Seek(t.PhysicalCyl, t.PhysicalHead)
                        If genTg43 Then
                            usbClient.SetPin(2, t.Cyl < 60)
                        End If
                    End If
                    seekRetry += 1
                    retry = 0
                End If
                retry += 1

                Dim retryFlux = ReadAndNormalise(usbClient,
                                                 revs:=Math.Max(revs, 3),
                                                 ticks:=ticks,
                                                 driveTicksPerRev:=driveTicksPerRev,
                                                 reverse:=reverse,
                                                 hardSectors:=hardSectors,
                                                 raw:=raw,
                                                 adjustSpeed:=adjustSpeed,
                                                 fakeIndexPeriod:=fakeIndexPeriod)
                For Each pll In profiles
                    If dat.NrMissing() = 0 Then
                        Exit For
                    End If
                    dat.DecodeFlux(retryFlux, pll)
                    DrainUnexpectedSectors(dat, trackInfo, onUnexpectedSector)
                Next
                If raw Then
                    flux.Append(retryFlux)
                Else
                    flux = retryFlux
                End If
            End While
            Return Tuple.Create(flux, CType(dat, HasFlux))
        End Function

        ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.decode_flux
        '   (the inline `for m in mismatches: print(...)` loop). VB lifts
        '   that print into a structured drain: codecs that implement
        '   HasDecodeDiagnostics buffer per-pass findings, and we call
        '   this helper after each DecodeFlux invocation to translate
        '   them into typed events. Shared by Read/Write/Convert flows
        '   (the originating command supplies its own typed callback).
        '   No-op when the codec doesn't implement HasDecodeDiagnostics
        '   or when no callback is wired.
        Public Shared Sub DrainUnexpectedSectors(dat As Object,
                                                 trackInfo As Greaseweazle.Actions.TrackInfo,
                                                 onUnexpectedSector As Action(Of Greaseweazle.Actions.UnexpectedSectorEventArgs))
            If onUnexpectedSector Is Nothing Then Return
            Dim hd = TryCast(dat, HasDecodeDiagnostics)
            If hd Is Nothing Then Return
            For Each d In hd.DrainDecodeDiagnostics()
                Dim us = TryCast(d, UnexpectedSectorDiagnostic)
                If us IsNot Nothing Then
                    onUnexpectedSector(New Greaseweazle.Actions.UnexpectedSectorEventArgs(
                        trackInfo, us.C, us.H, us.R, us.N))
                End If
            Next
        End Sub

        ' Python map: src/greaseweazle/tools/read.py::print_summary
        ' Builds a typed sector grid from the (cyls × heads) decode dict.
        ' Returns Nothing when there are no decoded tracks (matches
        ' Python's "skip the table" guard); callers should treat a
        ' Nothing return as "no summary block to emit".
        Public Shared Function BuildSectorSummary(tracks As TrackSet,
                                                  summary As IDictionary(Of Tuple(Of Integer, Integer), Codec)) As Greaseweazle.Actions.SectorSummaryGrid
            If summary Is Nothing OrElse summary.Count = 0 Then Return Nothing
            Dim nsec = summary.Values.Select(Function(x) x.Nsec).DefaultIfEmpty(0).Max()
            If nsec <= 0 Then Return Nothing

            Dim cyls = New List(Of Integer)(tracks.Cyls)
            Dim heads = New List(Of Integer)(tracks.Heads)
            Dim rows As New List(Of Greaseweazle.Actions.SectorSummaryRow)()
            Dim totSec = 0
            Dim goodSec = 0
            For Each head In heads
                Dim headNsec = summary.Where(Function(kvp) kvp.Key.Item2 = head).
                    Select(Function(kvp) kvp.Value.Nsec).
                    DefaultIfEmpty(0).
                    Max()
                If headNsec = 0 Then Continue For
                For sec = 0 To headNsec - 1
                    Dim cells As New List(Of Greaseweazle.Actions.SectorSummaryCell)(cyls.Count)
                    For Each cyl In cyls
                        Dim key = Tuple.Create(cyl, head)
                        If Not summary.ContainsKey(key) OrElse sec >= summary(key).Nsec Then
                            cells.Add(Greaseweazle.Actions.SectorSummaryCell.Empty)
                        Else
                            totSec += 1
                            If summary(key).HasSec(sec) Then
                                goodSec += 1
                                cells.Add(Greaseweazle.Actions.SectorSummaryCell.Good)
                            Else
                                cells.Add(Greaseweazle.Actions.SectorSummaryCell.Bad)
                            End If
                        End If
                    Next
                    rows.Add(New Greaseweazle.Actions.SectorSummaryRow(head, sec, cells))
                Next
            Next
            Return New Greaseweazle.Actions.SectorSummaryGrid(cyls, heads, rows, totSec, goodSec)
        End Function

        ' Python read.py::read_to_image lives inline in
        ' Greaseweazle.Tools.ReadAction.RunFromOptions/RunLive
        ' (BasicActions.vb). Per-track flux/decode emission flows
        ' through the typed ReadCommand events; sector-summary text
        ' is produced by Greaseweazle.Cli.Formatters.ReadFormatter.

        ' Python write.py::write_from_image lives inline in
        ' Greaseweazle.Tools.WriteAction.RunFromOptions/RunWriteLive
        ' (BasicActions.vb). Per-track erase/write/verify emission
        ' flows through the typed WriteCommand events; the verify
        ' summary footer is produced by
        ' Greaseweazle.Cli.Formatters.WriteFormatter.

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ScaleWriteFlux)
        ' Python map: src/greaseweazle/tools/write.py:103-109
        '   rem = 0.0
        '   wflux_list = []
        '   for x in wflux.list:
        '       y = x * factor + rem
        '       val = round(y)
        '       rem = y - val
        '       wflux_list.append(val)
        '
        ' Note: parameter is IEnumerable(Of Double). Python's wflux.list contains
        ' float ticks (track.py:276 declares `flux_ticks: float = 0`), so each
        ' element's fractional part is what feeds the Bresenham residual carry.
        ' Pre-rounding the input values would drop that fractional component
        ' before the residual loop sees it and distort every subsequent flux
        ' interval; callers must pass the raw List(Of Double) untouched.
        Public Shared Function ScaleWriteFlux(fluxList As IEnumerable(Of Double),
                                              factor As Double) As WriteScaleResult
            Dim remainder = 0.0
            Dim scaled As New List(Of Integer)()
            For Each value In fluxList
                Dim y = value * factor + remainder
                ' Python `round` is banker's (round-half-to-even) in Py3, which
                ' is .NET's default Math.Round MidpointRounding.
                Dim rounded = CInt(Math.Round(y, MidpointRounding.ToEven))
                remainder = y - rounded
                scaled.Add(rounded)
            Next
            Return New WriteScaleResult With {
                .ScaledFlux = scaled,
                .FinalRemainder = remainder
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildFakeIndexList)
        Public Shared Function BuildFakeIndexList(revolutions As Integer,
                                                  ticks As Integer,
                                                  driveTicksPerRev As Integer,
                                                  sampleFrequency As Double) As FakeIndexResult
            ' Python read.py:37 / align.py:29: `pre_index = int(usb.sample_freq * 0.5e-3)`.
            ' int() truncates toward zero. CInt's banker's rounding can diverge
            ' for odd sample frequencies (e.g. 72_000_001 * 5e-4 = 36000.0005).
            Dim preIndex = CInt(Math.Truncate(sampleFrequency * 0.5E-3))
            Dim effectiveTicks = ticks
            If effectiveTicks = 0 Then
                effectiveTicks = revolutions * driveTicksPerRev + 2 * preIndex
            End If
            Dim count = (effectiveTicks - preIndex) \ driveTicksPerRev
            Dim indexList As New List(Of Integer) From {preIndex}
            For i = 0 To count - 1
                indexList.Add(driveTicksPerRev)
            Next
            Return New FakeIndexResult With {
                .EffectiveTicks = effectiveTicks,
                .IndexList = indexList
            }
        End Function


    End Class

    ' Python map: no-1:1 with Python symbols; this helper DTO returns composite write scaling results from managed routines.
    Public Class WriteScaleResult
        Public Property ScaledFlux As List(Of Integer)
        Public Property FinalRemainder As Double
    End Class

    ' Python map: no-1:1 with Python symbols; this helper DTO returns derived fake-index timing results.
    Public Class FakeIndexResult
        Public Property EffectiveTicks As Integer
        Public Property IndexList As List(Of Integer)
    End Class

End Namespace
