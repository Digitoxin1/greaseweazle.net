Imports Greaseweazle.Core
Imports Greaseweazle.Codecs
Imports Greaseweazle.Images
Imports Greaseweazle.Shared
Imports System.IO
Imports System.Linq

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

    ' Python map: no-1:1 with Python symbols; this helper DTO captures simulated process/emit/cache traces for parity tests.
    Public Class ConvertLoopSimulationResult
        Public Property ProcessCalls As List(Of String)
        Public Property EmitTargets As List(Of String)
        Public Property CacheKeys As List(Of String)
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
        Public Shared Function ProcessInputTrack(t As TrackIdentity,
                                                 inImage As Image,
                                                 output As TextWriter,
                                                 Optional fmtCls As DiskDef = Nothing,
                                                 Optional formatName As String = Nothing,
                                                 Optional reverse As Boolean = False,
                                                 Optional hardSectors As Boolean = False,
                                                 Optional adjustSpeed As Nullable(Of Double) = Nothing,
                                                 Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing) As HasFlux
            Dim tspec = Greaseweazle.Tools.Convert.BuildTrackSummary(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)
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
                                   String.Format("{0}: Unable to identify hard sectors", tspec))
                output.WriteLine(String.Format("{0}: Converted to {1} hard sectors",
                                              tspec,
                                              flux.SectorList(flux.SectorList.Count - 1).Count))
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

            If fmtCls Is Nothing OrElse TypeOf track Is Codec Then
                output.WriteLine(String.Format("{0}: {1}", tspec, track.SummaryString()))
                Return track
            End If

            Dim profiles = If(pllProfiles, Plls.Values)
            ' Python: args.fmt_cls.decode_flux(cyl, head, track) implicitly uses
            ' plls[0], which has been mutated to the user --pll override (if any).
            ' Pass profiles(0) explicitly to mirror that behaviour without mutating
            ' the immutable Plls.Values list.
            Dim firstPll As Pll = If(profiles.Count > 0, profiles(0), Nothing)
            Dim dat = fmtCls.DecodeFlux(t.Cyl, t.Head, track, firstPll)
            If dat Is Nothing Then
                output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': Track skipped", tspec, If(formatName, "")))
                Return Nothing
            End If
            For i = 1 To profiles.Count - 1
                If dat.NrMissing() = 0 Then
                    Exit For
                End If
                dat.DecodeFlux(track, profiles(i))
            Next
            output.WriteLine(String.Format("{0}: {1} from {2}", tspec, dat.SummaryString(), track.SummaryString()))
            Return dat
        End Function

        ' Python map: src/greaseweazle/tools/convert.py::convert
        Public Shared Sub [Convert](outTracks As IEnumerable(Of TrackIter),
                                    tracks As TrackSet,
                                    inImage As Image,
                                    outImage As Image,
                                    output As TextWriter,
                                    Optional fmtCls As DiskDef = Nothing,
                                    Optional formatName As String = Nothing,
                                    Optional reverse As Boolean = False,
                                    Optional hardSectors As Boolean = False,
                                    Optional adjustSpeed As Nullable(Of Double) = Nothing,
                                    Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing)
            Dim summary As New Dictionary(Of Tuple(Of Integer, Integer), Codec)()
            For Each t In outTracks
                Dim key = Tuple.Create(t.Cyl, t.Head)
                Dim dat As HasFlux = Nothing
                If summary.ContainsKey(key) Then
                    dat = summary(key)
                ElseIf tracks.Contains(t.Cyl, t.Head) Then
                    dat = ProcessInputTrack(New TrackIdentity(tracks, t.Cyl, t.Head),
                                            inImage,
                                            output,
                                            fmtCls,
                                            formatName,
                                            reverse,
                                            hardSectors,
                                            adjustSpeed,
                                            pllProfiles)
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
            ReadWrite.PrintSummary(tracks, summary, output)
        End Sub
    End Class

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed convert runtime state.
    Public Class ConvertRuntimePreview
        Public Property InputFile As String
        Public Property OutputFile As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property TracksSpec As String
        Public Property OutTracksSpec As String
        Public Property Tracks As String
        Public Property OutTracks As String
        Public Property TrackSet As TrackSet
        Public Property OutTrackSet As TrackSet
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
        Public Shared Function ResolveTrackSets(formatTracks As TrackSet,
                                                tracksSpec As String,
                                                outTracksSpec As String) As Tuple(Of TrackSet, TrackSet)
            Dim baseSpec As String = If(formatTracks Is Nothing, "c=0-81:h=0-1", formatTracks.ToString())
            Dim defaultTracks As New TrackSet(baseSpec)
            Dim outDefaultTracks As New TrackSet(baseSpec)

            If Not String.IsNullOrEmpty(tracksSpec) Then
                defaultTracks.UpdateFromTrackspec(tracksSpec)
                ' Python copies only cyls/heads into out_def_tracks so step/hswap/h.off
                ' from --tracks do NOT bleed into the output trackset.
                outDefaultTracks.Cyls = New List(Of Integer)(defaultTracks.Cyls)
                outDefaultTracks.Heads = New List(Of Integer)(defaultTracks.Heads)
            End If

            If Not String.IsNullOrEmpty(outTracksSpec) Then
                outDefaultTracks.UpdateFromTrackspec(outTracksSpec)
            End If

            Return Tuple.Create(defaultTracks, outDefaultTracks)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SimulateConvertLoop)
        Public Shared Function SimulateConvertLoop(outTracks As IReadOnlyList(Of ConvertOutTrackAddress),
                                                   inTracks As IReadOnlyList(Of ConvertTrackAddress),
                                                   availableTracks As IReadOnlyList(Of ConvertTrackAddress),
                                                   cacheEnabled As Boolean) As ConvertLoopSimulationResult
            Dim processCalls As New List(Of String)()
            Dim emitTargets As New List(Of String)()
            Dim cacheKeys As New List(Of String)()
            Dim summary As New HashSet(Of String)(StringComparer.Ordinal)
            Dim inSet = New HashSet(Of String)(inTracks.Select(Function(t) TrackKey(t.Cyl, t.Head)), StringComparer.Ordinal)
            Dim availableSet = New HashSet(Of String)(availableTracks.Select(Function(t) TrackKey(t.Cyl, t.Head)), StringComparer.Ordinal)

            For Each t In outTracks
                Dim key = TrackKey(t.Cyl, t.Head)
                Dim shouldEmit = False
                If summary.Contains(key) Then
                    shouldEmit = True
                ElseIf inSet.Contains(key) Then
                    processCalls.Add(key)
                    If availableSet.Contains(key) Then
                        shouldEmit = True
                        If cacheEnabled Then
                            summary.Add(key)
                            cacheKeys.Add(key)
                        End If
                    End If
                End If

                If shouldEmit Then
                    emitTargets.Add(String.Format("{0}.{1}<={2}.{3}",
                                                 t.PhysicalCyl,
                                                 t.PhysicalHead,
                                                 t.Cyl,
                                                 t.Head))
                End If
            Next

            Return New ConvertLoopSimulationResult With {
                .ProcessCalls = processCalls,
                .EmitTargets = emitTargets,
                .CacheKeys = cacheKeys
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String),
                                                   knownFormats As IEnumerable(Of String)) As ConvertRuntimePreview
            Dim format As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim outTracksSpec As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim noClobber = False
            Dim hardSectors = False
            Dim reverse = False
            Dim adjustSpeed As Nullable(Of Double) = Nothing
            Dim pllOverride As Pll = Nothing
            Dim positionals As New List(Of String)()

            Dim i = 0
            While i < args.Count
                Dim rawToken = args(i)
                If String.Equals(rawToken, "--", StringComparison.Ordinal) Then
                    For j = i To args.Count - 1
                        positionals.Add(args(j))
                    Next
                    Exit While
                End If
                Dim token = rawToken
                Dim inlineValue As String = Nothing
                Dim equalsIndex = rawToken.IndexOf("="c)
                If rawToken.StartsWith("--", StringComparison.Ordinal) AndAlso equalsIndex > 2 Then
                    token = rawToken.Substring(0, equalsIndex)
                    inlineValue = rawToken.Substring(equalsIndex + 1)
                End If
                Select Case token
                    Case "--format"
                        format = TakeOptionValue(args, i, token, inlineValue)
                    Case "--tracks"
                        tracksSpec = TakeOptionValue(args, i, token, inlineValue)
                    Case "--out-tracks"
                        outTracksSpec = TakeOptionValue(args, i, token, inlineValue)
                    Case "--diskdefs", "--pll", "--adjust-speed"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--adjust-speed", StringComparison.Ordinal) Then
                            adjustSpeed = ToolOptions.Period(optionValue)
                        ElseIf String.Equals(token, "--pll", StringComparison.Ordinal) Then
                            Try
                                pllOverride = New Pll(optionValue)
                            Catch ex As ArgumentException
                                Throw New FatalException(ex.Message)
                            End Try
                        End If
                    Case "--hard-sectors", "--reverse", "--no-clobber", "-n"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        If String.Equals(token, "--no-clobber", StringComparison.Ordinal) OrElse
                           String.Equals(token, "-n", StringComparison.Ordinal) Then
                            noClobber = True
                        ElseIf String.Equals(token, "--hard-sectors", StringComparison.Ordinal) Then
                            hardSectors = True
                        ElseIf String.Equals(token, "--reverse", StringComparison.Ordinal) Then
                            reverse = True
                        End If
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ErrorHandling.Check(positionals.Count = 2, "convert requires input and output files")

            Dim inFile = ToolOptions.SplitOpts(positionals(0)).Item1
            Dim outFile = ToolOptions.SplitOpts(positionals(1)).Item1
            If String.IsNullOrEmpty(inFile) OrElse String.IsNullOrEmpty(outFile) Then
                Throw New FatalException("convert requires input and output files")
            End If

            If Not String.IsNullOrEmpty(format) Then
                Dim resolvedDiskDefsPath = ResolveDiskDefsPath(diskDefsPath)
                Dim parsed As DiskDef = Nothing
                Try
                    parsed = DiskDefParser.GetDiskdef(format, resolvedDiskDefsPath)
                Catch
                    parsed = Nothing
                End Try
                If parsed Is Nothing Then
                    ' Python convert.py:171 mirrors print_formats(diskdefs) which lists every
                    ' `disk` entry in diskdefs.cfg, not the codec base-type registry.
                    Dim formats As List(Of String)
                    Try
                        formats = DiskDefParser.GetAllFormats(resolvedDiskDefsPath)
                    Catch
                        formats = knownFormats.OrderBy(Function(x) x).ToList()
                    End Try
                    Throw New FatalException(
                        String.Format("Unknown format '{0}'{1}Known formats:{1}{2}",
                                      format,
                                      vbLf,
                                      ColumnFormatter.Columnify(formats)))
                End If
            End If

            Dim formatTracks As TrackSet = Nothing
            If Not String.IsNullOrEmpty(format) Then
                Dim resolvedDiskDefsPath = ResolveDiskDefsPath(diskDefsPath)
                Dim disk = DiskDefParser.GetDiskdef(format, resolvedDiskDefsPath)
                If disk IsNot Nothing Then
                    formatTracks = disk.Tracks
                End If
            End If

            Dim resolved = ResolveTrackSets(formatTracks, tracksSpec, outTracksSpec)
            Dim pllProfiles As New List(Of Pll)()
            If pllOverride IsNot Nothing Then
                pllProfiles.Add(pllOverride)
            End If
            pllProfiles.AddRange(Plls.Values)
            Return New ConvertRuntimePreview With {
                .InputFile = inFile,
                .OutputFile = outFile,
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .TracksSpec = tracksSpec,
                .OutTracksSpec = outTracksSpec,
                .Tracks = resolved.Item1.ToString(),
                .OutTracks = resolved.Item2.ToString(),
                .TrackSet = resolved.Item1,
                .OutTrackSet = resolved.Item2,
                .NoClobber = noClobber,
                .HardSectors = hardSectors,
                .Reverse = reverse,
                .AdjustSpeed = adjustSpeed,
                .PllProfiles = pllProfiles
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CloneTrackSet)
        Private Shared Function CloneTrackSet(value As TrackSet) As TrackSet
            Return New TrackSet(value.ToString())
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration TrackKey)
        Private Shared Function TrackKey(cyl As Integer, head As Integer) As String
            Return String.Format("{0}.{1}", cyl, head)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CheckOptionValue)
        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("--", StringComparison.Ordinal) Then
                    hasValue = False
                End If
            End If
            ErrorHandling.Check(hasValue, String.Format("missing value for option {0}", optionName))
        End Sub

        Private Shared Function TakeOptionValue(args As IReadOnlyList(Of String),
                                                ByRef index As Integer,
                                                optionName As String,
                                                inlineValue As String) As String
            If inlineValue IsNot Nothing Then
                Return inlineValue
            End If
            index += 1
            CheckOptionValue(args, index, optionName)
            Return args(index)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDiskDefsPath)
        Private Shared Function ResolveDiskDefsPath(diskDefsPath As String) As String
            If Not String.IsNullOrEmpty(diskDefsPath) Then
                Return diskDefsPath
            End If
            Return "diskdefs.xml"
        End Function

    End Class

End Namespace
