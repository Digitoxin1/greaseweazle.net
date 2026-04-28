Imports Greaseweazle.Core
Imports Greaseweazle.Codecs
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Images
Imports Greaseweazle.Shared
Imports System.IO

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed read runtime state that Python keeps in argparse namespace/local variables.
    Public Class ReadRuntimePreview
        Public Property FileName As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property Tracks As String
        Public Property TrackSet As TrackSet
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
        Public Property Live As Boolean
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed write runtime state that Python keeps in argparse namespace/local variables.
    Public Class WriteRuntimePreview
        Public Property FileName As String
        Public Property Format As String
        Public Property DiskDefsPath As String
        Public Property Tracks As String
        Public Property Precomp As String
        Public Property PrecompSpec As String
        Public Property TrackSet As TrackSet
        Public Property PreErase As Boolean
        Public Property EraseEmpty As Boolean
        Public Property HardSectors As Boolean
        Public Property NoVerify As Boolean
        Public Property Reverse As Boolean
        Public Property GenTg43 As Boolean
        Public Property Densel As Nullable(Of Boolean)
        Public Property FakeIndexPeriod As Nullable(Of Double)
        Public Property Retries As Integer
        Public Property Live As Boolean
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
        Public Shared Function ReadWithRetry(usbClient As Unit,
                                             t As TrackIter,
                                             revs As Integer,
                                             output As TextWriter,
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
                                             Optional pllProfiles As IReadOnlyList(Of Pll) = Nothing) As Tuple(Of Flux, HasFlux)
            Dim tspec = BuildTrackSpec(t)
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
                output.WriteLine(String.Format("{0}: {1}", tspec, flux.SummaryString()))
                Return Tuple.Create(flux, CType(flux, HasFlux))
            End If

            Dim profiles = If(pllProfiles, Plls.Values)
            ' Python: args.fmt_cls.decode_flux(cyl, head, flux) implicitly reads
            ' plls[0]; with --pll the user's override has been inserted there.
            ' Pass profiles(0) explicitly so the first decode honours --pll.
            Dim firstPll As Pll = If(profiles.Count > 0, profiles(0), Nothing)
            Dim dat = fmtCls.DecodeFlux(t.Cyl, t.Head, flux, firstPll)
            If dat Is Nothing Then
                output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': No format conversion applied: {2}",
                                               tspec,
                                               If(formatName, String.Empty),
                                               flux.SummaryString()))
                Return Tuple.Create(flux, CType(Nothing, HasFlux))
            End If
            For i = 1 To profiles.Count - 1
                If dat.NrMissing() = 0 Then
                    Exit For
                End If
                dat.DecodeFlux(flux, profiles(i))
            Next

            Dim seekRetry = 0
            Dim retry = 0
            While True
                Dim line = String.Format("{0}: {1} from {2}",
                                         tspec,
                                         dat.SummaryString(),
                                         flux.SummaryString())
                If retry <> 0 Then
                    line &= String.Format(" (Retry #{0}.{1})", seekRetry, retry)
                End If
                output.WriteLine(line)
                If dat.NrMissing() = 0 Then
                    Exit While
                End If
                If retries = 0 OrElse (retry Mod retries) = 0 Then
                    If retries = 0 OrElse seekRetry > seekRetries Then
                        output.WriteLine(String.Format("{0}: Giving up: {1} sectors missing",
                                                       tspec,
                                                       dat.NrMissing()))
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
                Next
                If raw Then
                    flux.Append(retryFlux)
                Else
                    flux = retryFlux
                End If
            End While
            Return Tuple.Create(flux, CType(dat, HasFlux))
        End Function

        ' Python map: src/greaseweazle/tools/read.py::print_summary
        Public Shared Sub PrintSummary(tracks As TrackSet,
                                       summary As IDictionary(Of Tuple(Of Integer, Integer), Codec),
                                       output As TextWriter)
            If summary Is Nothing OrElse summary.Count = 0 Then
                Return
            End If

            Dim nsec = summary.Values.Select(Function(x) x.Nsec).DefaultIfEmpty(0).Max()
            If nsec <= 0 Then
                Return
            End If

            Dim tens As String = "Cyl-> "
            Dim p = -1
            For Each c In tracks.Cyls
                tens &= If(c \ 10 = p, " ", (c \ 10).ToString(Globalization.CultureInfo.InvariantCulture))
                p = c \ 10
            Next
            output.WriteLine(tens)

            Dim ones As String = "H. S: "
            For Each c In tracks.Cyls
                ones &= (c Mod 10).ToString(Globalization.CultureInfo.InvariantCulture)
            Next
            output.WriteLine(ones)

            Dim totSec = 0
            Dim goodSec = 0
            For Each head In tracks.Heads
                Dim headNsec = summary.Where(Function(kvp) kvp.Key.Item2 = head).
                    Select(Function(kvp) kvp.Value.Nsec).
                    DefaultIfEmpty(0).
                    Max()
                If headNsec = 0 Then
                    Continue For
                End If

                For sec = 0 To headNsec - 1
                    Dim line = String.Format(Globalization.CultureInfo.InvariantCulture, "{0}.{1,2}: ", head, sec)
                    For Each cyl In tracks.Cyls
                        Dim key = Tuple.Create(cyl, head)
                        If Not summary.ContainsKey(key) OrElse sec >= summary(key).Nsec Then
                            line &= " "
                        Else
                            totSec += 1
                            If summary(key).HasSec(sec) Then
                                goodSec += 1
                                line &= "."
                            Else
                                line &= "X"
                            End If
                        End If
                    Next
                    output.WriteLine(line)
                Next
            Next

            If totSec <> 0 Then
                output.WriteLine(String.Format(Globalization.CultureInfo.InvariantCulture,
                                               "Found {0} sectors of {1} ({2}%)",
                                               goodSec,
                                               totSec,
                                               (goodSec * 100) \ totSec))
            End If
        End Sub

        ' Python read.py::read_to_image lives inline in Greaseweazle.Tools.ReadAction.Execute
        ' (BasicActions.vb). The earlier standalone helper here has been removed because
        ' it had drifted (no fractional-revs handling, no fake-index/hard-sectors setup,
        ' no --raw branch, no CmdError catch) and would have to mirror Execute's full
        ' image-class fan-out to be useful. Keeping a single implementation prevents the
        ' two from diverging again.

        ' Python map: src/greaseweazle/tools/write.py::write_from_image
        Public Shared Sub WriteFromImage(usbClient As Unit,
                                         tracks As IEnumerable(Of TrackIter),
                                         image As Image,
                                         output As TextWriter,
                                         Optional eraseEmpty As Boolean = False,
                                         Optional revs As Integer = 1)
            Dim driveTicksPerRev = usbClient.ReadTrack(2, 0).TicksPerRev
            For Each t In tracks
                Dim tspec = String.Format("T{0}.{1}", t.Cyl, t.Head)
                If t.PhysicalCyl <> t.Cyl OrElse t.PhysicalHead <> t.Head Then
                    tspec &= String.Format(" -> Drive {0}.{1}", t.PhysicalCyl, t.PhysicalHead)
                End If

                usbClient.Seek(t.PhysicalCyl, t.PhysicalHead)
                Dim track = image.GetTrack(t.Cyl, t.Head)
                If track Is Nothing Then
                    If eraseEmpty Then
                        output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                        usbClient.EraseTrack(driveTicksPerRev * 1.1)
                    End If
                    Continue For
                End If

                Dim wflux = track.FluxForWriteout(cueAtIndex:=True)
                Dim factor = driveTicksPerRev / wflux.TicksToIndex
                ' Python write.py:103-109 passes wflux.list (float) into the
                ' residual loop. Pre-rounding to int would drop the fractional
                ' parts the Bresenham carry needs, so feed the List(Of Double)
                ' through unchanged.
                Dim scaled = ScaleWriteFlux(wflux.List, factor).ScaledFlux
                For i = 1 To revs
                    output.WriteLine(String.Format("{0}: Writing Track ({1})", tspec, wflux.SummaryString()))
                    usbClient.WriteTrack(scaled,
                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                         cueAtIndex:=wflux.IndexCued)
                Next
            Next
            output.WriteLine("All tracks verified")
        End Sub

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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildReadRuntimePreview)
        Public Shared Function BuildReadRuntimePreview(args As IReadOnlyList(Of String),
                                                       knownFormats As IEnumerable(Of String)) As ReadRuntimePreview
            Dim format As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim revs As Integer? = Nothing
            Dim raw = False
            Dim hardSectors = False
            Dim reverse = False
            Dim genTg43 = False
            Dim noClobber = False
            Dim adjustSpeed As Nullable(Of Double) = Nothing
            Dim fakeIndexPeriod As Nullable(Of Double) = Nothing
            Dim densel As Nullable(Of Boolean) = Nothing
            Dim retries = 3
            Dim seekRetries = 0
            Dim pllOverride As Pll = Nothing
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
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
                    Case "--revs"
                        revs = ParseUInt(TakeOptionValue(args, i, token, inlineValue), token)
                        ErrorHandling.Check(revs.Value >= 1, "--revs must be >= 1")
                    Case "--device", "--drive", "--diskdefs", "--fake-index", "--adjust-speed",
                         "--retries", "--seek-retries", "--pll", "--densel", "--dd"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        ElseIf String.Equals(token, "--drive", StringComparison.Ordinal) Then
                            driveToken = optionValue
                        ElseIf String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--fake-index", StringComparison.Ordinal) Then
                            fakeIndexPeriod = ToolOptions.Period(optionValue)
                        ElseIf String.Equals(token, "--adjust-speed", StringComparison.Ordinal) Then
                            adjustSpeed = ToolOptions.Period(optionValue)
                        ElseIf String.Equals(token, "--retries", StringComparison.Ordinal) Then
                            retries = ParseUInt(optionValue, token)
                        ElseIf String.Equals(token, "--seek-retries", StringComparison.Ordinal) Then
                            seekRetries = ParseUInt(optionValue, token)
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
                    Case "--raw", "--hard-sectors", "--no-clobber", "-n", "--gen-tg43", "--reverse"
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
                        ElseIf String.Equals(token, "--no-clobber", StringComparison.Ordinal) OrElse
                               String.Equals(token, "-n", StringComparison.Ordinal) Then
                            noClobber = True
                        End If
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ErrorHandling.Check(positionals.Count = 1, "read requires output file argument")
            ' Python read.py:231-234: --fake-index and --hard-sectors are mutex.
            If fakeIndexPeriod.HasValue AndAlso hardSectors Then
                Throw New FatalException("argument --hard-sectors: not allowed with argument --fake-index")
            End If
            ' Python read.py:247-251: --densel/--dd and --gen-tg43 are mutex.
            If densel.HasValue AndAlso genTg43 Then
                Throw New FatalException("argument --gen-tg43: not allowed with argument --densel")
            End If

            ' Python read.py:267-268: if not args.format: args.format = image_class.default_format
            ' Apply the per-image-class default before validating the format string.
            If String.IsNullOrEmpty(format) Then
                format = ImageDefaults.DefaultFormatForFile(positionals(0))
            End If
            ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)

            ' Python read.py:278-279: when --format is given, fmt_cls.default_revs becomes
            ' the default if --revs is omitted. Python preserves the float/int typing of
            ' default_revs so read_to_image can convert fractional values into a tick budget.
            Dim fractionalRevs As Nullable(Of Double) = Nothing
            If revs Is Nothing AndAlso Not String.IsNullOrEmpty(format) Then
                Try
                    Dim fmtCls = DiskDefParser.GetDiskdef(format, diskDefsPath)
                    If fmtCls IsNot Nothing Then
                        Dim defaultRevs = fmtCls.DefaultRevs
                        If defaultRevs > 0 Then
                            If defaultRevs <> Math.Floor(defaultRevs) Then
                                ' Python read.py:174-184 collapses fractional revs to 2
                                ' after measuring drive ticks-per-rev; do the integer
                                ' collapse here and stash the original float for runtime.
                                fractionalRevs = defaultRevs
                                revs = 2
                            Else
                                revs = CInt(defaultRevs)
                            End If
                        End If
                    End If
                Catch
                End Try
            End If

            Dim resolvedRevs = If(revs, 3)
            Dim revsDisplay As String
            If fractionalRevs.HasValue Then
                revsDisplay = fractionalRevs.Value.ToString(Globalization.CultureInfo.InvariantCulture)
            Else
                revsDisplay = resolvedRevs.ToString(Globalization.CultureInfo.InvariantCulture)
            End If

            ' Python read.py:275-281: when --format resolves a fmt_cls, the format's
            ' tracks become the default trackset (overlaid by --tracks if supplied).
            Dim tracks As TrackSet = Nothing
            If Not String.IsNullOrEmpty(format) Then
                Try
                    Dim fmtCls = DiskDefParser.GetDiskdef(format, diskDefsPath)
                    If fmtCls IsNot Nothing AndAlso fmtCls.Tracks IsNot Nothing Then
                        tracks = TrackResolution.ResolveDefaultTracksFromFormat(fmtCls.Tracks, tracksSpec)
                    End If
                Catch
                    ' Fall through to generic default below.
                End Try
            End If
            If tracks Is Nothing Then
                tracks = TrackResolution.ResolveDefaultTracks("c=0-81:h=0-1", tracksSpec)
            End If
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
            Return New ReadRuntimePreview With {
                .FileName = positionals(0),
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .Tracks = tracks.ToString(),
                .TrackSet = tracks,
                .Revs = resolvedRevs,
                .FractionalRevs = fractionalRevs,
                .RevsDisplay = revsDisplay,
                .Raw = raw,
                .HardSectors = hardSectors,
                .Reverse = reverse,
                .NoClobber = noClobber,
                .AdjustSpeed = adjustSpeed,
                .PllProfiles = pllProfiles,
                .FakeIndexPeriod = fakeIndexPeriod,
                .GenTg43 = genTg43,
                .Densel = densel,
                .Retries = retries,
                .SeekRetries = seekRetries,
                .Live = live,
                .Device = device,
                .Drive = drive
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildWriteRuntimePreview)
        Public Shared Function BuildWriteRuntimePreview(args As IReadOnlyList(Of String),
                                                        knownFormats As IEnumerable(Of String)) As WriteRuntimePreview
            Dim format As String = Nothing
            Dim diskDefsPath As String = Nothing
            Dim tracksSpec As String = Nothing
            Dim precompSpec As String = Nothing
            Dim preErase = False
            Dim eraseEmpty = False
            Dim hardSectors = False
            Dim noVerify = False
            Dim reverse = False
            Dim genTg43 = False
            Dim densel As Nullable(Of Boolean) = Nothing
            Dim fakeIndexPeriod As Nullable(Of Double) = Nothing
            Dim retries = 3
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
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
                    Case "--precomp"
                        precompSpec = TakeOptionValue(args, i, token, inlineValue)
                    Case "--device", "--drive", "--diskdefs", "--fake-index", "--retries", "--densel", "--dd"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        ElseIf String.Equals(token, "--drive", StringComparison.Ordinal) Then
                            driveToken = optionValue
                        ElseIf String.Equals(token, "--diskdefs", StringComparison.Ordinal) Then
                            diskDefsPath = optionValue
                        ElseIf String.Equals(token, "--fake-index", StringComparison.Ordinal) Then
                            fakeIndexPeriod = ToolOptions.Period(optionValue)
                        ElseIf String.Equals(token, "--retries", StringComparison.Ordinal) Then
                            retries = ParseUInt(optionValue, token)
                        ElseIf String.Equals(token, "--densel", StringComparison.Ordinal) OrElse
                               String.Equals(token, "--dd", StringComparison.Ordinal) Then
                            Try
                                densel = ToolOptions.Level(optionValue)
                            Catch ex As ArgumentException
                                Throw New FatalException(ex.Message)
                            End Try
                        End If
                    Case "--pre-erase", "--erase-empty", "--hard-sectors", "--no-verify", "--reverse", "--gen-tg43"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        If String.Equals(token, "--pre-erase", StringComparison.Ordinal) Then
                            preErase = True
                        ElseIf String.Equals(token, "--erase-empty", StringComparison.Ordinal) Then
                            eraseEmpty = True
                        ElseIf String.Equals(token, "--hard-sectors", StringComparison.Ordinal) Then
                            hardSectors = True
                        ElseIf String.Equals(token, "--no-verify", StringComparison.Ordinal) Then
                            noVerify = True
                        ElseIf String.Equals(token, "--reverse", StringComparison.Ordinal) Then
                            reverse = True
                        ElseIf String.Equals(token, "--gen-tg43", StringComparison.Ordinal) Then
                            genTg43 = True
                        End If
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ErrorHandling.Check(positionals.Count = 1, "write requires input file argument")

            ' Python write.py:231-234: --fake-index / --hard-sectors mutex.
            If fakeIndexPeriod.HasValue AndAlso hardSectors Then
                Throw New FatalException("argument --hard-sectors: not allowed with argument --fake-index")
            End If
            ' Python write.py:244-251: --densel/--dd / --gen-tg43 mutex.
            If densel.HasValue AndAlso genTg43 Then
                Throw New FatalException("argument --gen-tg43: not allowed with argument --densel")
            End If

            ' Python write.py:260-261: if not args.format: args.format = image_class.default_format
            If String.IsNullOrEmpty(format) Then
                format = ImageDefaults.DefaultFormatForFile(positionals(0))
            End If
            ValidateFormatIfSpecified(format, knownFormats, diskDefsPath)

            ' Python write.py:274-279: when --format resolves a fmt_cls, the format's
            ' tracks become the default trackset (overlaid by --tracks if supplied).
            Dim tracks As TrackSet = Nothing
            If Not String.IsNullOrEmpty(format) Then
                Try
                    Dim fmtCls = DiskDefParser.GetDiskdef(format, diskDefsPath)
                    If fmtCls IsNot Nothing AndAlso fmtCls.Tracks IsNot Nothing Then
                        tracks = TrackResolution.ResolveDefaultTracksFromFormat(fmtCls.Tracks, tracksSpec)
                    End If
                Catch
                    ' Fall through to generic default below.
                End Try
            End If
            If tracks Is Nothing Then
                tracks = TrackResolution.ResolveDefaultTracks("c=0-81:h=0-1", tracksSpec)
            End If

            Dim precompText As String = Nothing
            If Not String.IsNullOrEmpty(precompSpec) Then
                precompText = New PrecompSpec(precompSpec).ToString()
            End If
            Dim drive As DriveSpec
            Try
                drive = ToolOptions.Drive(driveToken)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try
            Return New WriteRuntimePreview With {
                .FileName = positionals(0),
                .Format = format,
                .DiskDefsPath = diskDefsPath,
                .Tracks = tracks.ToString(),
                .Precomp = precompText,
                .PrecompSpec = precompSpec,
                .TrackSet = tracks,
                .PreErase = preErase,
                .EraseEmpty = eraseEmpty,
                .HardSectors = hardSectors,
                .NoVerify = noVerify,
                .Reverse = reverse,
                .GenTg43 = genTg43,
                .Densel = densel,
                .FakeIndexPeriod = fakeIndexPeriod,
                .Retries = retries,
                .Live = live,
                .Device = device,
                .Drive = drive
            }
        End Function

        ' Python map: src/greaseweazle/tools/{read,write}.py::main (Unknown format error block)
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

            ' Python: error.Fatal("Unknown format '%s'\nKnown formats:\n%s" % (args.format, codec.print_formats(args.diskdefs)))
            ' codec.print_formats walks every `disk` entry in diskdefs.cfg (recursively via `import`)
            ' and columnifies the full sorted list. We must do the same here so the diagnostic
            ' enumerates real disk-definition names (e.g. "ibm.1440") rather than codec base
            ' types like "ibm.mfm".
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
                              Greaseweazle.Shared.ColumnFormatter.Columnify(formats)))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDiskDefsPath)
        Private Shared Function ResolveDiskDefsPath(diskDefsPath As String) As String
            If Not String.IsNullOrEmpty(diskDefsPath) Then
                Return diskDefsPath
            End If
            Return "diskdefs.xml"
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseUInt)
        Private Shared Function ParseUInt(value As String, optionName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid value for {0}: {1}", optionName, value))
            End If
            Return parsed
        End Function

        ' Python map: src/greaseweazle/tools/read.py::(no direct 1:1 symbol; VB helper to format track spec for read logging)
        Private Shared Function BuildTrackSpec(t As TrackIter) As String
            Dim tspec = String.Format("T{0}.{1}", t.Cyl, t.Head)
            If t.PhysicalCyl <> t.Cyl OrElse t.PhysicalHead <> t.Head Then
                tspec &= String.Format(" <- Drive {0}.{1}", t.PhysicalCyl, t.PhysicalHead)
            End If
            Return tspec
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
