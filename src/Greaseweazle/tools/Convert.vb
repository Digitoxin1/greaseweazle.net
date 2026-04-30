Imports Greaseweazle.Core
Imports Greaseweazle.Codecs
Imports Greaseweazle.Images
Imports Greaseweazle.Shared

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this DTO replaces tuple/dict track-address carriers in convert flow.
    Public Class ConvertTrackAddress
        Public Property Cyl As Integer
        Public Property Head As Integer
    End Class

    ' Python map: no-1:1 with Python symbols; this DTO extends track-address data for output remapping.
    Public Class ConvertOutTrackAddress
        Inherits ConvertTrackAddress
        Public Property PhysicalCyl As Integer
        Public Property PhysicalHead As Integer
    End Class

    ' Python map: src/greaseweazle/tools/convert.py::TrackIdentity
    Public Class TrackIdentity
        Public Property Cyl As Integer
        Public Property Head As Integer
        Public Property PhysicalCyl As Integer
        Public Property PhysicalHead As Integer

        ' Python map: src/greaseweazle/tools/convert.py::TrackIdentity.__init__
        Public Sub New(ts As TrackSet, cyl As Integer, head As Integer)
            Me.Cyl = cyl
            Me.Head = head
            Dim mapped = ts.ChToPch(cyl, head)
            Me.PhysicalCyl = mapped.Item1
            Me.PhysicalHead = mapped.Item2
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/convert.py::(no direct 1:1 symbol; VB helper container for converted function-style members)
    Public NotInheritable Class ConvertFunctions
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/convert.py::open_input_image
        Public Shared Function OpenInputImage(Of T As {Image, New})(inFile As String,
                                                                    fmtCls As DiskDef,
                                                                    inFileOpts As IDictionary(Of String, String)) As T
            Return Image.FromFile(Of T)(inFile, fmtCls, inFileOpts)
        End Function

        ' Python map: src/greaseweazle/tools/convert.py::open_output_image
        Public Shared Function OpenOutputImage(Of T As {Image, New})(outFile As String,
                                                                     fmtCls As DiskDef,
                                                                     noClobber As Boolean,
                                                                     outFileOpts As IDictionary(Of String, String)) As T
            Return Image.ToFile(Of T)(outFile, fmtCls, noClobber, outFileOpts)
        End Function

        ' Python map: src/greaseweazle/tools/convert.py::process_input_track
        '
        ' Performs the per-track decode/preprocess pipeline. Three
        ' callbacks expose progress without the algorithm producing
        ' any text:
        '   onHardSectorsApplied (only when --hard-sectors is set
        '                         and detection succeeds)
        '   onTrackProcessed     (always, with the typed outcome)
        '   onUnexpectedSector   (once per (C,H,R,N) tuple drained
        '                         from a HasDecodeDiagnostics codec
        '                         after each DecodeFlux pass)
        ' Returns the final HasFlux to be emitted to the output image
        ' (Nothing when the track was skipped).
        Public Shared Function ProcessInputTrack(t As TrackIdentity,
                                                 inImage As Image,
                                                 onHardSectorsApplied As Action(Of Greaseweazle.Actions.ConvertHardSectorsEventArgs),
                                                 onTrackProcessed As Action(Of Greaseweazle.Actions.ConvertTrackProcessedEventArgs),
                                                 Optional fmtCls As DiskDef = Nothing,
                                                 Optional formatName As String = Nothing,
                                                 Optional reverse As Boolean = False,
                                                 Optional hardSectors As Boolean = False,
                                                 Optional adjustSpeed As Nullable(Of Double) = Nothing,
                                                 Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing,
                                                 Optional onUnexpectedSector As Action(Of Greaseweazle.Actions.ConvertUnexpectedSectorEventArgs) = Nothing) As HasFlux
            Dim trackInfo = New Greaseweazle.Actions.ConvertTrackInfo(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)
            Dim track = inImage.GetTrack(t.PhysicalCyl, t.PhysicalHead)
            If track Is Nothing Then
                Return Nothing
            End If

            If reverse Then
                Dim flux = track.Flux()
                flux.Reverse()
                track = flux
            End If

            If hardSectors Then
                Dim flux = track.Flux()
                flux.IdentifyHardSectors()
                track = flux
                ErrorHandling.Check(flux.SectorList IsNot Nothing AndAlso flux.SectorList.Count > 0,
                                   String.Format("{0}: Unable to identify hard sectors",
                                                 Greaseweazle.Tools.Convert.BuildTrackSummary(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)))
                Dim count = flux.SectorList(flux.SectorList.Count - 1).Count
                If onHardSectorsApplied IsNot Nothing Then
                    onHardSectorsApplied(New Greaseweazle.Actions.ConvertHardSectorsEventArgs(trackInfo, count))
                End If
            End If

            If adjustSpeed.HasValue Then
                If TypeOf track Is Codec Then
                    track = CType(track, Codec).MasterTrack()
                End If
                If TypeOf track Is MasterTrack Then
                    Dim master = CType(track, MasterTrack)
                    master.Scale(adjustSpeed.Value / master.TimePerRev)
                    track = master
                Else
                    Dim flux = track.Flux()
                    flux.Scale(adjustSpeed.Value / flux.TimePerRev)
                    track = flux
                End If
            End If

            ' Snapshot the raw-flux summary fields once. When the source
            ' isn't a raw Flux (e.g. an IMG/HFE/IMD input that surfaces a
            ' Codec or MasterTrack), both come back Nothing.
            Dim trackAsFlux = TryCast(track, Greaseweazle.Core.Flux)
            Dim fluxSampleCount As Nullable(Of Integer) = Nothing
            Dim fluxDurationMs As Nullable(Of Double) = Nothing
            If trackAsFlux IsNot Nothing Then
                fluxSampleCount = trackAsFlux.List.Count
                fluxDurationMs = trackAsFlux.List.Sum() * 1000.0 / trackAsFlux.SampleFreq
            End If

            If fmtCls Is Nothing OrElse TypeOf track Is Codec Then
                If onTrackProcessed IsNot Nothing Then
                    onTrackProcessed(New Greaseweazle.Actions.ConvertTrackProcessedEventArgs(
                        trackInfo,
                        Greaseweazle.Actions.ConvertTrackOutcome.NoFormat,
                        track.SummaryString(),
                        Nothing,
                        Nothing,
                        0,
                        0,
                        fluxSampleCount,
                        fluxDurationMs))
                End If
                Return track
            End If

            Dim profiles = If(pllProfiles, Plls.Values)
            Dim firstPll As Pll = If(profiles.Count > 0, profiles(0), Nothing)
            Dim dat = fmtCls.DecodeFlux(t.Cyl, t.Head, track, firstPll)
            If dat Is Nothing Then
                If onTrackProcessed IsNot Nothing Then
                    onTrackProcessed(New Greaseweazle.Actions.ConvertTrackProcessedEventArgs(
                        trackInfo,
                        Greaseweazle.Actions.ConvertTrackOutcome.OutOfRange,
                        track.SummaryString(),
                        Nothing,
                        If(formatName, ""),
                        0,
                        0,
                        fluxSampleCount,
                        fluxDurationMs))
                End If
                Return Nothing
            End If
            ReadWrite.DrainUnexpectedSectorsForConvert(dat, trackInfo, onUnexpectedSector)
            For i = 1 To profiles.Count - 1
                If dat.NrMissing() = 0 Then
                    Exit For
                End If
                dat.DecodeFlux(track, profiles(i))
                ReadWrite.DrainUnexpectedSectorsForConvert(dat, trackInfo, onUnexpectedSector)
            Next
            If onTrackProcessed IsNot Nothing Then
                onTrackProcessed(New Greaseweazle.Actions.ConvertTrackProcessedEventArgs(
                    trackInfo,
                    Greaseweazle.Actions.ConvertTrackOutcome.Decoded,
                    track.SummaryString(),
                    dat.SummaryString(),
                    Nothing,
                    dat.Nsec - dat.NrMissing(),
                    dat.Nsec,
                    fluxSampleCount,
                    fluxDurationMs))
            End If
            Return dat
        End Function

        ' Python map: src/greaseweazle/tools/convert.py::convert
        '
        ' Walks the output track set and decodes/emits each one.
        ' Returns the post-decode summary dict so the caller can
        ' surface a typed SectorSummaryGrid via SummaryReady.
        Public Shared Function [Convert](outTracks As IEnumerable(Of TrackIter),
                                         tracks As TrackSet,
                                         inImage As Image,
                                         outImage As Image,
                                         onHardSectorsApplied As Action(Of Greaseweazle.Actions.ConvertHardSectorsEventArgs),
                                         onTrackProcessed As Action(Of Greaseweazle.Actions.ConvertTrackProcessedEventArgs),
                                         Optional fmtCls As DiskDef = Nothing,
                                         Optional formatName As String = Nothing,
                                         Optional reverse As Boolean = False,
                                         Optional hardSectors As Boolean = False,
                                         Optional adjustSpeed As Nullable(Of Double) = Nothing,
                                         Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing,
                                         Optional onUnexpectedSector As Action(Of Greaseweazle.Actions.ConvertUnexpectedSectorEventArgs) = Nothing) As IDictionary(Of Tuple(Of Integer, Integer), Codec)
            Dim summary As New Dictionary(Of Tuple(Of Integer, Integer), Codec)()
            For Each t In outTracks
                Dim key = Tuple.Create(t.Cyl, t.Head)
                Dim dat As HasFlux = Nothing
                If summary.ContainsKey(key) Then
                    dat = summary(key)
                ElseIf tracks.Contains(t.Cyl, t.Head) Then
                    dat = ProcessInputTrack(New TrackIdentity(tracks, t.Cyl, t.Head),
                                            inImage,
                                            onHardSectorsApplied,
                                            onTrackProcessed,
                                            fmtCls,
                                            formatName,
                                            reverse,
                                            hardSectors,
                                            adjustSpeed,
                                            pllProfiles,
                                            onUnexpectedSector)
                    If dat Is Nothing Then
                        Continue For
                    End If
                    If fmtCls IsNot Nothing AndAlso TypeOf dat Is Codec Then
                        summary(key) = CType(dat, Codec)
                    End If
                Else
                    Continue For
                End If
                outImage.EmitTrack(t.PhysicalCyl, t.PhysicalHead, dat)
            Next
            Return summary
        End Function
    End Class

    ' Strongly-typed options for the `convert` action.
    '
    ' TrackSet / OutTrackSet are TrackSetSpec rather than TrackSet because
    ' Convert can't fold format defaults at parse time — the format is
    ' auto-detected from the input image inside RunFromOptions. The DTO
    ' carries the *user's intent* (which keys they specified); the engine
    ' folds that intent against format defaults via Convert.ResolveTrackSets.
    Public Class ConvertOptions
        Public Property InputFile As String
        Public Property OutputFile As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property TrackSet As TrackSetSpec
        Public Property OutTrackSet As TrackSetSpec
        Public Property NoClobber As Boolean
        Public Property HardSectors As Boolean
        Public Property Reverse As Boolean
        Public Property AdjustSpeed As Nullable(Of Double)
        Public Property PllProfiles As IReadOnlyList(Of Pll)
    End Class

    ' Python map: src/greaseweazle/tools/convert.py (direct algorithm parity for convert option/track resolution logic).
    Public NotInheritable Class Convert

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackSummary)
        Public Shared Function BuildTrackSummary(cyl As Integer, head As Integer, physicalCyl As Integer, physicalHead As Integer) As String
            Dim tspec = String.Format("T{0}.{1}", cyl, head)
            If physicalCyl <> cyl OrElse physicalHead <> head Then
                tspec &= String.Format(" <- Image {0}.{1}", physicalCyl, physicalHead)
            End If
            Return tspec
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildConvertHeader)
        Public Shared Function BuildConvertHeader(tracks As String, outTracks As String) As String
            Return String.Format("Converting {0} -> {1}", tracks, outTracks)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveFormat)
        Public Shared Function ResolveFormat(explicitFormat As String,
                                             inputDefaultFormat As String,
                                             outputDefaultFormat As String) As String
            Dim value = explicitFormat
            If String.IsNullOrEmpty(value) Then
                value = inputDefaultFormat
            End If
            If String.IsNullOrEmpty(value) Then
                value = outputDefaultFormat
            End If
            Return value
        End Function

        ' Python map: src/greaseweazle/tools/convert.py::main (track resolution block,
        ' lines 178-190).
        '
        ' Python clones the *format-default* TrackSet first, then folds --tracks into
        ' the input set, copies ONLY cyls/heads (not step/hswap/h.off) into the output
        ' set, and finally folds --out-tracks into the output set. This means
        ' step=2/hswap/h.off specified on --tracks affect the input traversal only;
        ' the output set keeps the format-default geometry unless --out-tracks
        ' overrides it explicitly.
        '
        ' VB inputs are typed (TrackSetSpec) rather than spec strings: the
        ' user's --tracks / --out-tracks are parsed into partials at parse
        ' time and passed through ConvertOptions. The fold against format
        ' defaults happens here using TrackSetSpec.Resolve(formatTracks).
        Public Shared Function ResolveTrackSets(formatTracks As TrackSet,
                                                userIn As TrackSetSpec,
                                                userOut As TrackSetSpec) As Tuple(Of TrackSet, TrackSet)
            Dim defaults As TrackSet = If(formatTracks Is Nothing, New TrackSet("c=0-81:h=0-1"), formatTracks)

            ' Input set: format defaults overlaid with --tracks intent.
            Dim resolvedIn As TrackSet =
                If(userIn Is Nothing, New TrackSet(defaults.ToString()), userIn.Resolve(defaults))

            ' Output base: format defaults with cyls/heads from the resolved
            ' input — Python copies only those two fields so step/hswap/h.off
            ' from --tracks do NOT bleed into the output trackset.
            Dim outBase As New TrackSet(defaults.ToString())
            outBase.Cyls = New List(Of Integer)(resolvedIn.Cyls)
            outBase.Heads = New List(Of Integer)(resolvedIn.Heads)

            ' Output set: that base overlaid with --out-tracks intent.
            Dim resolvedOut As TrackSet =
                If(userOut Is Nothing, outBase, userOut.Resolve(outBase))

            Return Tuple.Create(resolvedIn, resolvedOut)
        End Function

    End Class

End Namespace
