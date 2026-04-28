Imports Greaseweazle.Core
Imports Greaseweazle.Codecs
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Shared
Imports System.IO

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed align runtime state.
    Public Class AlignRuntimePreview
        Public Property Header As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property FormatDef As DiskDef
        Public Property Reads As Integer
        Public Property Revs As Integer
        ' Python align.py:64-71 keeps a fractional default_revs as a float and
        ' converts it to a tick budget at runtime (after measuring drive
        ' ticks-per-rev). When the format provides a non-integral default_revs,
        ' this property holds the raw float so the live path can reproduce
        ' Python's `ticks = drive_tpr * fractional_revs; revs = 2` collapse.
        Public Property FractionalRevs As Nullable(Of Double)
        Public Property Ticks As Integer
        Public Property TrackSet As TrackSet
        Public Property Raw As Boolean
        Public Property HardSectors As Boolean
        Public Property Reverse As Boolean
        Public Property AdjustSpeed As Nullable(Of Double)
        Public Property PllProfiles As IReadOnlyList(Of Pll)
        Public Property FakeIndexPeriod As Nullable(Of Double)
        Public Property GenTg43 As Boolean
        Public Property Densel As Nullable(Of Boolean)
        Public Property Live As Boolean
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/align.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Align

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackSpec)
        Public Shared Function BuildTrackSpec(cyl As Integer, head As Integer, physicalCyl As Integer, physicalHead As Integer) As String
            Dim tspec = String.Format("T{0}.{1}", cyl, head)
            If physicalCyl <> cyl OrElse physicalHead <> head Then
                tspec &= String.Format(" <- Drive {0}.{1}", physicalCyl, physicalHead)
            End If
            Return tspec
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildSingleTrackHeader)
        Public Shared Function BuildSingleTrackHeader(tspec As String, reads As Integer, revs As Integer) As String
            Return String.Format("Aligning {0}, reading {1} times, revs={2}", tspec, reads, revs)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildMultiTrackHeader)
        Public Shared Function BuildMultiTrackHeader(cyl As Integer, heads As IReadOnlyList(Of Integer), reads As Integer, revs As Integer) As String
            Return String.Format("Aligning T{0} (alternating heads {1}), reading {2} times, revs={3}",
                                 cyl,
                                 String.Join(",", heads),
                                 reads,
                                 revs)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ValidateTrackCylinders)
        Public Shared Sub ValidateTrackCylinders(trackCylinderHeads As IReadOnlyList(Of Tuple(Of Integer, Integer)))
            If trackCylinderHeads Is Nothing OrElse trackCylinderHeads.Count = 0 Then
                Throw New FatalException("Align command requires at least one track (e.g., c=40:h=0)")
            End If
            Dim cyl = trackCylinderHeads(0).Item1
            For i = 1 To trackCylinderHeads.Count - 1
                If trackCylinderHeads(i).Item1 <> cyl Then
                    Throw New FatalException("All tracks must be on the same cylinder for alignment")
                End If
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveAlternatingTrackIndex)
        Public Shared Function ResolveAlternatingTrackIndex(readNumber As Integer, trackCount As Integer) As Integer
            Return (readNumber - 1) Mod trackCount
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveHardSectorReadParams)
        Public Shared Function ResolveHardSectorReadParams(hardSectors As Integer, revs As Integer) As Tuple(Of Integer, Integer)
            Return Tuple.Create((hardSectors + 1) * (revs + 1), 0)
        End Function

        ' Python map: src/greaseweazle/tools/align.py::read_and_normalise
        Public Shared Function ReadAndNormalise(usbClient As Unit,
                                                revs As Integer,
                                                ticks As Integer,
                                                driveTicksPerRev As Nullable(Of Double),
                                                reverse As Boolean,
                                                hardSectors As Boolean,
                                                raw As Boolean,
                                                adjustSpeed As Nullable(Of Double),
                                                Optional fakeIndexPeriod As Nullable(Of Double) = Nothing) As Flux
            Dim flux As Flux
            If fakeIndexPeriod.HasValue Then
                ErrorHandling.Check(driveTicksPerRev.HasValue, "fake-index requires drive ticks-per-rev")
                ' Python read.py:36 / align.py:28: `drive_tpr = int(args.drive_ticks_per_rev)`.
                ' int() truncates toward zero; Math.Round here would use banker's
                ' rounding and diverge from Python whenever the fractional part is
                ' >0.5 (or exactly .5 with an odd integer part).
                Dim fakeResult = ReadWrite.BuildFakeIndexList(revs,
                                                              ticks,
                                                              CInt(Math.Truncate(driveTicksPerRev.Value)),
                                                              usbClient.SampleFreq)
                flux = usbClient.ReadTrack(0, fakeResult.EffectiveTicks)
                flux.IndexList = fakeResult.IndexList.Select(Function(x) CDbl(x)).ToList()
            Else
                flux = usbClient.ReadTrack(revs, ticks)
            End If

            ' Python read.py:45 unconditionally assigns:
            '   flux._ticks_per_rev = args.drive_ticks_per_rev
            ' which clears the cached value to None when no explicit measurement is
            ' supplied. VB's `Flux.TicksPerRev` getter prefers the index-list-derived
            ' value over the cached field, so a missing override is automatically
            ' equivalent to Python's None case; we only assign when we have an actual
            ' override to apply.
            If driveTicksPerRev.HasValue Then
                flux.TicksPerRev = driveTicksPerRev.Value
            End If
            If reverse Then
                flux.Reverse()
            End If
            If hardSectors AndAlso Not raw Then
                flux.IdentifyHardSectors()
            End If
            If adjustSpeed.HasValue Then
                flux.Scale(adjustSpeed.Value / flux.TimePerRev)
            End If
            Return flux
        End Function

        ' Python map: src/greaseweazle/tools/align.py::align_track
        Public Shared Sub AlignTrack(usbClient As Unit,
                                     tracks As IReadOnlyList(Of TrackIter),
                                     reads As Integer,
                                     revs As Integer,
                                     ticks As Integer,
                                     output As TextWriter,
                                     reverse As Boolean,
                                     hardSectors As Boolean,
                                     raw As Boolean,
                                     adjustSpeed As Nullable(Of Double),
                                     driveTicksPerRev As Nullable(Of Double),
                                     Optional fakeIndexPeriod As Nullable(Of Double) = Nothing,
                                     Optional formatDef As DiskDef = Nothing,
                                     Optional formatName As String = Nothing,
                                     Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing,
                                     Optional formatLine As String = Nothing)
            Dim trackList = tracks.ToList()
            Dim pairs = trackList.Select(Function(t) Tuple.Create(t.Cyl, t.Head)).ToList()
            ValidateTrackCylinders(pairs)

            Dim cyl = trackList(0).Cyl
            ' Python prints the "Aligning ..." header AFTER all revs adjustments
            ' (fractional collapse + hard-sectors multiplier), so the displayed
            ' revs reflects the *effective* read count, not the parse-time value.
            ' AlignAction defers this print to here in live mode for that reason;
            ' the caller-supplied formatLine (if any) is then emitted before the
            ' read loop, mirroring align.py:106-107.
            If trackList.Count = 1 Then
                Dim t = trackList(0)
                output.WriteLine(BuildSingleTrackHeader(BuildTrackSpec(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead), reads, revs))
            Else
                output.WriteLine(BuildMultiTrackHeader(cyl, trackList.Select(Function(t) t.Head).ToList(), reads, revs))
            End If
            If Not String.IsNullOrEmpty(formatLine) Then
                output.WriteLine("Format " & formatLine)
            End If

            For readNum = 1 To reads
                Dim t = trackList(ResolveAlternatingTrackIndex(readNum, trackList.Count))
                Dim tspec = BuildTrackSpec(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)
                usbClient.Seek(t.PhysicalCyl, t.PhysicalHead)
                Dim flux = ReadAndNormalise(usbClient, revs, ticks, driveTicksPerRev, reverse, hardSectors, raw, adjustSpeed, fakeIndexPeriod)
                If formatDef Is Nothing Then
                    output.WriteLine(String.Format("{0}: {1}", tspec, flux.SummaryString()))
                Else
                    ' Python (align.py:126-135): `dat = fmt_cls.decode_flux(cyl, head, flux)`
                    ' creates the codec instance and runs the first decode pass; each retry
                    ' calls `dat.decode_flux(flux, pll)` on the SAME instance so subsequent
                    ' PLL profiles cumulatively merge any newly-recovered sectors via the
                    ' IBMTrack_Fixed.decode_flux raw-reconcile pass (first-good-wins).
                    Dim profiles = If(pllProfiles, Plls.Values)
                    ' Python's first decode reads plls[0]; --pll has mutated that to the
                    ' user override. Pass profiles(0) explicitly because VB's Plls.Values
                    ' is immutable.
                    Dim firstPll As Pll = If(profiles.Count > 0, profiles(0), Nothing)
                    Dim decoded = formatDef.DecodeFlux(t.Cyl, t.Head, flux, firstPll)
                    If decoded Is Nothing Then
                        output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': No format conversion applied: {2}",
                                                       tspec,
                                                       If(formatName, String.Empty),
                                                       flux.SummaryString()))
                    Else
                        Dim nr = 1
                        While decoded.NrMissing() <> 0 AndAlso nr < profiles.Count
                            decoded.DecodeFlux(flux, profiles(nr))
                            nr += 1
                        End While
                        output.WriteLine(String.Format("{0}: {1} from {2}",
                                                       tspec,
                                                       decoded.SummaryString(),
                                                       flux.SummaryString()))
                    End If
                End If
                If readNum < reads Then
                    Threading.Thread.Sleep(100)
                End If
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String),
                                                   knownFormats As IEnumerable(Of String)) As AlignRuntimePreview
            Dim format As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim raw = False
            Dim hardSectors = False
            Dim reverse = False
            Dim genTg43 = False
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
            Dim pllOverride As Pll = Nothing
            Dim adjustSpeed As Nullable(Of Double) = Nothing
            Dim fakeIndexPeriod As Nullable(Of Double) = Nothing
            Dim densel As Nullable(Of Boolean) = Nothing
            Dim reads = 10
            Dim revs As Integer? = Nothing
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
                    Case "--reads"
                        reads = ParseUInt(TakeOptionValue(args, i, token, inlineValue), token)
                        ErrorHandling.Check(reads >= 1, "--reads must be >= 1")
                    Case "--revs"
                        revs = ParseUInt(TakeOptionValue(args, i, token, inlineValue), token)
                        ErrorHandling.Check(revs.Value >= 1, "--revs must be >= 1")
                    Case "--device", "--drive", "--diskdefs", "--pll", "--adjust-speed", "--densel", "--dd", "--fake-index"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        ElseIf String.Equals(token, "--drive", StringComparison.Ordinal) Then
                            driveToken = optionValue
                        ElseIf String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--adjust-speed", StringComparison.Ordinal) Then
                            adjustSpeed = ToolOptions.Period(optionValue)
                        ElseIf String.Equals(token, "--fake-index", StringComparison.Ordinal) Then
                            fakeIndexPeriod = ToolOptions.Period(optionValue)
                        ElseIf String.Equals(token, "--pll", StringComparison.Ordinal) Then
                            Try
                                pllOverride = New Pll(optionValue)
                            Catch ex As ArgumentException
                                Throw New FatalException(ex.Message)
                            End Try
                        ElseIf String.Equals(token, "--densel", StringComparison.Ordinal) OrElse
                               String.Equals(token, "--dd", StringComparison.Ordinal) Then
                            Try
                                densel = ToolOptions.Level(optionValue)
                            Catch ex As ArgumentException
                                Throw New FatalException(ex.Message)
                            End Try
                        End If
                    Case "--raw", "--hard-sectors", "--gen-tg43", "--reverse", "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        If String.Equals(token, "--raw", StringComparison.Ordinal) Then
                            raw = True
                        ElseIf String.Equals(token, "--hard-sectors", StringComparison.Ordinal) Then
                            hardSectors = True
                        ElseIf String.Equals(token, "--gen-tg43", StringComparison.Ordinal) Then
                            genTg43 = True
                        ElseIf String.Equals(token, "--reverse", StringComparison.Ordinal) Then
                            reverse = True
                        ElseIf String.Equals(token, "--test", StringComparison.Ordinal) Then
                            live = False
                        End If
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            If positionals.Count > 0 Then
                Throw New FatalException(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals)))
            End If
            ErrorHandling.Check(Not String.IsNullOrEmpty(tracksSpec), "align requires --tracks")
            ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)
            Dim formatDef As DiskDef = Nothing
            ' Python: load DiskDef whenever --format is supplied, not only when running live.
            ' Track-only specs (e.g. "ibm.mfm") may not resolve to a DiskDef; tolerate
            ' a missing entry here so callers using --format for codec hints continue
            ' to work in --test (preview) mode.
            Dim fractionalRevs As Nullable(Of Double) = Nothing
            If Not String.IsNullOrEmpty(format) Then
                Try
                    formatDef = ResolveDiskDefinition(format, diskDefsPath)
                Catch ex As FatalException
                    formatDef = Nothing
                End Try
                ' Python align.py:201: `if args.revs is None: args.revs = fmt_cls.default_revs`
                ' Python keeps the float typing of default_revs and collapses to
                ' integer revs=2 inside align_track at runtime (see
                ' align.py:64-71). Preserve the float for the runtime collapse
                ' rather than rounding here, which would lose tick fidelity
                ' for fractional values like 1.1.
                If revs Is Nothing AndAlso formatDef IsNot Nothing AndAlso formatDef.DefaultRevs > 0 Then
                    Dim defaultRevs = formatDef.DefaultRevs
                    If defaultRevs <> Math.Floor(defaultRevs) Then
                        ' Fractional: stash float, set integer revs=2 (Python's
                        ' unconditional collapse target).
                        fractionalRevs = defaultRevs
                        revs = 2
                    Else
                        revs = CInt(defaultRevs)
                    End If
                End If
            End If
            Dim resolvedRevs = If(revs, 3)

            Dim trackSet = TrackResolution.ResolveDefaultTracksFromFormat(If(formatDef Is Nothing, Nothing, formatDef.Tracks), tracksSpec)
            Dim trackList = trackSet.IteratePhysical().ToList()
            Dim pairs = trackList.Select(Function(t) Tuple.Create(t.Cyl, t.Head)).ToList()
            ValidateTrackCylinders(pairs)

            Dim drive As DriveSpec
            Try
                drive = ToolOptions.Drive(driveToken)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try
            Dim pllProfiles As New List(Of Pll)()
            If pllOverride IsNot Nothing Then
                pllProfiles.Add(pllOverride)
            End If
            pllProfiles.AddRange(Plls.Values)

            Dim header As String
            If trackList.Count = 1 Then
                Dim t = trackList(0)
                header = BuildSingleTrackHeader(
                    BuildTrackSpec(t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead),
                    reads,
                    resolvedRevs)
            Else
                Dim heads = trackList.Select(Function(t) t.Head).ToList()
                header = BuildMultiTrackHeader(trackList(0).Cyl, heads, reads, resolvedRevs)
            End If

            Return New AlignRuntimePreview With {
                .Header = header,
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .FormatDef = formatDef,
                .Reads = reads,
                .Revs = resolvedRevs,
                .FractionalRevs = fractionalRevs,
                .Ticks = 0,
                .TrackSet = trackSet,
                .Raw = raw,
                .HardSectors = hardSectors,
                .Reverse = reverse,
                .AdjustSpeed = adjustSpeed,
                .PllProfiles = pllProfiles,
                .FakeIndexPeriod = fakeIndexPeriod,
                .GenTg43 = genTg43,
                .Densel = densel,
                .Live = live,
                .Device = device,
                .Drive = drive
            }
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

        Private Shared Function ParseUInt(value As String, optionName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid value for {0}: {1}", optionName, value))
            End If
            Return parsed
        End Function

        Private Shared Function ResolveDiskDefsPath(diskDefsPath As String) As String
            If Not String.IsNullOrEmpty(diskDefsPath) Then
                Return diskDefsPath
            End If
            Return "diskdefs.xml"
        End Function

        Private Shared Function ResolveDiskDefinition(formatName As String, diskDefsPath As String) As DiskDef
            Dim path = ResolveDiskDefsPath(diskDefsPath)
            Dim disk = DiskDefParser.GetDiskdef(formatName, path)
            ErrorHandling.Check(disk IsNot Nothing, String.Format("Unknown format '{0}'", formatName))
            Return disk
        End Function

        ' Python map: src/greaseweazle/tools/align.py::main (Unknown format error block)
        Private Shared Sub ValidateFormatIfSpecified(format As String,
                                                     knownFormats As IEnumerable(Of String),
                                                     diskDefsPath As String)
            If String.IsNullOrEmpty(format) Then
                Return
            End If

            Dim resolvedDiskDefsPath = ResolveDiskDefsPath(diskDefsPath)
            Try
                Dim parsed = DiskDefParser.GetDiskdef(format, resolvedDiskDefsPath)
                If parsed IsNot Nothing Then
                    Return
                End If
            Catch
                ' Keep fallback unknown-format output below.
            End Try

            ' Python align.py:196 mirrors codec.print_formats(args.diskdefs) which lists every
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
        End Sub

    End Class

End Namespace
