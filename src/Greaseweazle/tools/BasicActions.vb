Imports System.Globalization
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Ports
Imports System.Linq
Imports System.Threading
Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Images
Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this registry bootstrap aggregates command modules for VB startup.
    Public NotInheritable Class Actions

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CreateDefaultRegistry)
        Public Shared Function CreateDefaultRegistry() As ToolRegistry
            Dim registry As New ToolRegistry()
            registry.Register(New InfoAction())
            registry.Register(New ReadAction())
            registry.Register(New WriteAction())
            registry.Register(New ConvertAction())
            registry.Register(New EraseAction())
            registry.Register(New CleanAction())
            registry.Register(New SeekAction())
            registry.Register(New DelaysAction())
            registry.Register(New UpdateAction())
            registry.Register(New PinAction())
            registry.Register(New ResetAction())
            registry.Register(New BandwidthAction())
            registry.Register(New RpmAction())
            registry.Register(New AlignAction())
            Return registry
        End Function

    End Class

    ' Python map: no-1:1 with Python symbols; this base type centralizes shared action metadata/behavior.
    Public MustInherit Class StubActionBase
        Implements ToolAction

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Protected Sub New(name As String, description As String)
            Me.Name = name
            Me.Description = description
        End Sub

        Public ReadOnly Property Name As String Implements ToolAction.Name
        Public ReadOnly Property Description As String Implements ToolAction.Description

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overridable Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer Implements ToolAction.Execute
            Throw New FatalException(String.Format("{0}: Action is not implemented", Name))
        End Function
    End Class

    ' Python map: no-1:1 with Python symbols; this helper centralizes lightweight option parsing for parity probes.
    Public NotInheritable Class ActionArgs
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Parse)
        Public Shared Function Parse(args As IReadOnlyList(Of String)) As Dictionary(Of String, String)
            Dim output As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Dim i = 0
            While i < args.Count
                Dim token = args(i)
                If token.StartsWith("--", StringComparison.Ordinal) Then
                    Dim key = token.Substring(2)
                    Dim value As String = "true"
                    If i + 1 < args.Count AndAlso Not args(i + 1).StartsWith("--", StringComparison.Ordinal) Then
                        value = args(i + 1)
                        i += 1
                    End If
                    output(key) = value
                Else
                    output(String.Format("arg{0}", i)) = token
                End If
                i += 1
            End While
            Return output
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/info.py::main (direct command execution mapping).
    Public Class InfoAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("info", "Display information about the Greaseweazle setup.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-parse-tag") Then
                Dim tag = parsed("tag")
                Dim major = 0
                Dim minor = 0
                Dim matched = Info.TryParseFirmwareTag(tag, major, minor)
                context.Output.WriteLine(String.Format("matched={0}", If(matched, "1", "0")))
                If matched Then
                    context.Output.WriteLine(String.Format("version={0}.{1}", major, minor))
                End If
                Return 0
            End If
            If parsed.ContainsKey("parity-format-line") Then
                Dim name = parsed("name")
                Dim value = parsed("value")
                Dim tab = Integer.Parse(parsed("tab"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(Info.PrintInfoLine(name, value, tab))
                Return 0
            End If
            Dim preview = Info.BuildRuntimePreview(args)
            ' Python info.py:68-70 prints Host Tools / Device: unconditionally before
            ' attempting to open the device. Mirror that here regardless of test mode.
            ' Python uses `__version__` which is the setuptools_scm-derived version
            ' string (e.g. "1.23"). The assembly's InformationalVersion mirrors that
            ' format directly (1.23.0/1.23.0.0 contain trailing .0 components Python
            ' would not emit), so prefer it when present and only fall back to
            ' AssemblyVersion if for some reason the attribute is missing.
            Dim hostVersion As String = Nothing
            Dim infoAttr = TryCast(Reflection.CustomAttributeExtensions.GetCustomAttribute(Of Reflection.AssemblyInformationalVersionAttribute)(Reflection.Assembly.GetExecutingAssembly()), Reflection.AssemblyInformationalVersionAttribute)
            If infoAttr IsNot Nothing AndAlso Not String.IsNullOrEmpty(infoAttr.InformationalVersion) Then
                hostVersion = infoAttr.InformationalVersion
            Else
                hostVersion = Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()
            End If
            context.Output.WriteLine(Info.PrintInfoLine("Host Tools", hostVersion))
            context.Output.WriteLine("Device:")
            If preview.Live Then
                Dim usb As Unit = Nothing
                Try
                    Try
                        usb = ToolOptions.UsbOpen(preview.Device, modeCheck:=False)
                    Catch ex As IO.IOException
                        ' Python catches `serial.SerialException` which covers port-not-found
                        ' and port-in-use both; .NET splits these into IOException and
                        ' UnauthorizedAccessException, so accept either to preserve "Not
                        ' found" output parity rather than letting the worker dump a
                        ' FATAL ERROR.
                        context.Output.WriteLine("  Not found")
                        Return 0
                    Catch ex As UnauthorizedAccessException
                        context.Output.WriteLine("  Not found")
                        Return 0
                    Catch ex As Exception When TypeOf ex Is FatalException
                        context.Output.WriteLine("  Not found")
                        Return 0
                    End Try
                    Dim fw = usb.ReadFirmwareInfo()
                    Dim modeSwitched = usb.CanModeSwitch AndAlso usb.UpdateMode <> preview.Bootloader
                    If modeSwitched Then
                        usb = ToolOptions.UsbReopen(usb, isUpdate:=preview.Bootloader)
                    End If
                    If Not String.IsNullOrEmpty(usb.PortDevice) Then
                        context.Output.WriteLine(Info.PrintInfoLine("Port", usb.PortDevice, 2))
                    End If
                    Dim model = Info.ModelName(usb.HwModel, usb.HwSubmodel)
                    context.Output.WriteLine(Info.PrintInfoLine("Model", model, 2))
                    Dim mcuStrs As New List(Of String)()
                    Dim mcuName = Info.McuName(usb.McuId)
                    If Not String.IsNullOrEmpty(mcuName) Then
                        mcuStrs.Add(mcuName)
                    End If
                    If usb.McuMhz <> 0 Then
                        mcuStrs.Add(String.Format(CultureInfo.InvariantCulture, "{0}MHz", usb.McuMhz))
                    End If
                    If usb.McuSramKb <> 0 Then
                        mcuStrs.Add(String.Format(CultureInfo.InvariantCulture, "{0}kB SRAM", usb.McuSramKb))
                    End If
                    If mcuStrs.Count > 0 Then
                        context.Output.WriteLine(Info.PrintInfoLine("MCU", String.Join(", ", mcuStrs), 2))
                    End If
                    Dim fwver = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", fw.Major, fw.Minor)
                    If usb.UpdateMode Then
                        fwver &= " (Bootloader)"
                    End If
                    context.Output.WriteLine(Info.PrintInfoLine("Firmware", fwver, 2))
                    Dim serial = If(String.IsNullOrEmpty(usb.PortSerialNumber), "Unknown", usb.PortSerialNumber)
                    context.Output.WriteLine(Info.PrintInfoLine("Serial", serial, 2))
                    Dim usbStrs As New List(Of String)()
                    usbStrs.Add(Info.UsbSpeedName(usb.UsbSpeed))
                    If usb.UsbBufferKb <> 0 Then
                        usbStrs.Add(String.Format(CultureInfo.InvariantCulture, "{0}kB Buffer", usb.UsbBufferKb))
                    End If
                    context.Output.WriteLine(Info.PrintInfoLine("USB", String.Join(", ", usbStrs), 2))
                    Dim updateMode = usb.UpdateMode
                    Dim version = Tuple.Create(CInt(usb.Major), CInt(usb.Minor))
                    If modeSwitched Then
                        usb = ToolOptions.UsbReopen(usb, isUpdate:=Not preview.Bootloader)
                    End If
                    If Not updateMode Then
                        Try
                            Dim latest = Info.LatestFirmware()
                            If latest.Item1 > version.Item1 OrElse (latest.Item1 = version.Item1 AndAlso latest.Item2 > version.Item2) Then
                                context.Output.WriteLine("")
                                context.Output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                                                       "*** New firmware version {0}.{1} is available",
                                                                       latest.Item1, latest.Item2))
                                ' Python info.py:136: util.print_update_instructions(usb)
                                For Each line In ToolOptions.PrintUpdateInstructions(usb)
                                    context.Output.WriteLine(line)
                                Next
                            End If
                        Catch
                            ' Python prints exception traces; mirror by silently ignoring network failures.
                        End Try
                    End If
                Finally
                    If usb IsNot Nothing AndAlso usb.Serial IsNot Nothing Then
                        Try : usb.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/read.py::main (direct command execution mapping).
    Public Class ReadAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("read", "Read a disk to the specified image file.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-fake-index") Then
                Dim revs = Integer.Parse(parsed("revs"), Globalization.CultureInfo.InvariantCulture)
                Dim ticks = Integer.Parse(parsed("ticks"), Globalization.CultureInfo.InvariantCulture)
                Dim driveTicks = Integer.Parse(parsed("drive-ticks-per-rev"), Globalization.CultureInfo.InvariantCulture)
                Dim sampleFreq = Double.Parse(parsed("sample-freq"), Globalization.CultureInfo.InvariantCulture)
                Dim result = ReadWrite.BuildFakeIndexList(revs, ticks, driveTicks, sampleFreq)
                context.Output.WriteLine(String.Format("effective_ticks={0}", result.EffectiveTicks))
                context.Output.WriteLine(String.Format("index_list={0}", String.Join(",", result.IndexList)))
                Return 0
            End If
            Dim preview = ReadWrite.BuildReadRuntimePreview(args, CodecRegistry.GetFormats())
            context.Output.WriteLine(String.Format("Reading {0} revs={1}",
                                                   preview.Tracks,
                                                   If(preview.RevsDisplay, preview.Revs.ToString(Globalization.CultureInfo.InvariantCulture))))
            If Not String.IsNullOrEmpty(preview.Format) Then
                context.Output.WriteLine("Format " & preview.Format)
            End If
            If preview.Live Then
                Dim outSplit = ConvertAction.SplitImageFileOptions(preview.FileName)
                Dim outPath = outSplit.Item1
                Dim outOpts = outSplit.Item2
                Dim outExt = Path.GetExtension(outPath)
                Dim readOnlyType = ResolveReadOnlyImageTypeName(outExt)
                Dim writeScp = String.Equals(outExt, ".scp", StringComparison.OrdinalIgnoreCase)
                Dim writeSector = IsSectorImageExtension(outExt)
                Dim writeRaw = String.Equals(outExt, ".raw", StringComparison.OrdinalIgnoreCase)
                Dim writeD88 = String.Equals(outExt, ".d88", StringComparison.OrdinalIgnoreCase)
                Dim writeNsi = String.Equals(outExt, ".nsi", StringComparison.OrdinalIgnoreCase)
                Dim writeImd = String.Equals(outExt, ".imd", StringComparison.OrdinalIgnoreCase)
                Dim writeHfe = String.Equals(outExt, ".hfe", StringComparison.OrdinalIgnoreCase)
                ErrorHandling.Check(writeScp OrElse writeSector OrElse writeRaw OrElse writeD88 OrElse writeNsi OrElse writeImd OrElse writeHfe OrElse Not String.IsNullOrEmpty(readOnlyType),
                                    String.Format("{0}: Unrecognised file suffix '{1}'", outPath, Path.GetExtension(outPath)))
                If Not String.IsNullOrEmpty(readOnlyType) Then
                    Throw New FatalException(String.Format("{0}: Cannot create {1} image files", outPath, readOnlyType))
                End If
                Dim scpImage As Scp = Nothing
                Dim imgImage As Img = Nothing
                Dim rawImage As KryoFlux = Nothing
                Dim nsiImage As Nsi = Nothing
                Dim imdImage As Imd = Nothing
                Dim hfeImage As Hfe = Nothing
                Dim d88Image As D88 = Nothing
                Dim imgDisk As DiskDef = Nothing
                If writeScp Then
                    scpImage = New Scp()
                    scpImage.FileName = outPath
                    scpImage.ApplyWOpts(outOpts)
                ElseIf writeImd Then
                    Dim effectiveFormat = preview.Format
                    ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "IMD output requires --format")
                    imgDisk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    imdImage = New Imd()
                    imdImage.FileName = outPath
                    imdImage.ApplyWOpts(outOpts)
                ElseIf writeHfe Then
                    Dim effectiveFormat = preview.Format
                    ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "HFE output requires --format")
                    imgDisk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    hfeImage = New Hfe()
                    hfeImage.FileName = outPath
                    hfeImage.ApplyWOpts(outOpts)
                ElseIf writeSector Then
                    Dim effectiveFormat = preview.Format
                    If String.IsNullOrEmpty(effectiveFormat) Then
                        effectiveFormat = ImageDefaults.DefaultFormatForExtension(outExt)
                    End If
                    ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "IMG output requires --format")
                    imgDisk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    imgImage = New Img(imgDisk)
                    ConfigureSectorImageDefaults(imgImage, outExt)
                    imgImage.FileName = outPath
                    imgImage.ApplyWOpts(outOpts)
                ElseIf writeRaw Then
                    rawImage = New KryoFlux(outPath)
                ElseIf writeNsi Then
                    Dim effectiveFormat = preview.Format
                    ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "NSI output requires --format")
                    imgDisk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    nsiImage = New Nsi(imgDisk)
                    nsiImage.FileName = outPath
                    nsiImage.ApplyWOpts(outOpts)
                ElseIf writeD88 Then
                    Dim effectiveFormat = preview.Format
                    ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "D88 output requires --format")
                    imgDisk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    d88Image = New D88(imgDisk)
                    d88Image.FileName = outPath
                    d88Image.ApplyWOpts(outOpts)
                End If
                ' Python read.py:271 sets `args.fmt_cls = codec.get_diskdef(args.format, args.diskdefs)`
                ' regardless of the output image type. Resolve the format here so that
                ' --format always triggers decode + summary, even when paired with
                ' --raw or a flux-only output (e.g. .scp/.raw).
                If imgDisk Is Nothing AndAlso Not String.IsNullOrEmpty(preview.Format) Then
                    imgDisk = ResolveDiskDefinition(preview.Format, preview.DiskDefsPath)
                End If
                ' Python image/image.py::Image.__enter__ opens with mode="x" when --no-clobber
                ' is set, raising FileExistsError if the target already exists. KryoFlux
                ' is a directory-template name so we skip it (mirrors Python's KryoFlux class).
                If preview.NoClobber AndAlso Not writeRaw AndAlso File.Exists(outPath) Then
                    Throw New FatalException(String.Format("{0}: File exists", outPath))
                End If
                Dim summary As New Dictionary(Of Tuple(Of Integer, Integer), Codec)()
                Dim usbClient As Unit = Nothing
                Dim prevPin2 As Nullable(Of Boolean) = Nothing
                Dim cmdFailed = False
                Try
                    Try
                        usbClient = ToolOptions.UsbOpen(preview.Device)
                        usbClient.ReadFirmwareInfo()
                        If preview.Densel.HasValue OrElse preview.GenTg43 Then
                            prevPin2 = usbClient.GetPin(2)
                        End If
                        If preview.Densel.HasValue Then
                            usbClient.SetPin(2, preview.Densel.Value)
                        End If
                        ToolOptions.WithDriveSelected(
                            Sub()
                                ' Python read.py::read_to_image opening lines:
                                '   args.ticks, args.drive_ticks_per_rev = 0, None
                                Dim effectiveRevs = preview.Revs
                                Dim effectiveTicks = 0
                                Dim driveTicksPerRev As Nullable(Of Double) = Nothing
                                Dim effectiveHardSectors = preview.HardSectors
                                Dim hardSectorCount As Integer = 0

                                ' Python read.py:163-164:
                                '   if args.fake_index is not None:
                                '       args.drive_ticks_per_rev = args.fake_index * usb.sample_freq
                                If preview.FakeIndexPeriod.HasValue Then
                                    driveTicksPerRev = preview.FakeIndexPeriod.Value * usbClient.SampleFreq
                                    ' Python read.py:165-172:
                                    '   elif args.hard_sectors:
                                    '       flux = usb.read_track(revs=0, ticks=int(usb.sample_freq/2))
                                    '       flux.identify_hard_sectors()
                                    '       args.drive_ticks_per_rev = flux.ticks_per_rev
                                    '       args.hard_sectors = len(flux.sector_list[-1])
                                    '       print(f'Drive reports {args.hard_sectors} hard sectors')
                                ElseIf preview.HardSectors Then
                                    ' Python read.py:34 / align.py:50 / write.py:40:
                                    ' `flux = usb.read_track(revs=0, ticks=int(usb.sample_freq/2))`.
                                    ' int() truncates toward zero; CInt would use banker's
                                    ' rounding and diverge for odd sample frequencies.
                                    Dim probe = usbClient.ReadTrack(0, CInt(Math.Truncate(usbClient.SampleFreq / 2)))
                                    probe.IdentifyHardSectors()
                                    ErrorHandling.Check(probe.SectorList IsNot Nothing AndAlso probe.SectorList.Count > 0,
                                                       "Unable to identify hard sectors on this drive")
                                    driveTicksPerRev = probe.TicksPerRev
                                    effectiveHardSectors = True
                                    hardSectorCount = probe.SectorList(probe.SectorList.Count - 1).Count
                                    context.Output.WriteLine(String.Format("Drive reports {0} hard sectors", hardSectorCount))
                                End If

                                ' Python read.py:174-184:
                                '   if isinstance(args.revs, float):
                                '       if args.raw:           args.revs = 2
                                '       else:
                                '           if args.drive_ticks_per_rev is None:
                                '               args.drive_ticks_per_rev = usb.read_track(2).ticks_per_rev
                                '           args.ticks = int(args.drive_ticks_per_rev * args.revs)
                                '           args.revs  = 2
                                If preview.FractionalRevs.HasValue Then
                                    If preview.Raw Then
                                        effectiveRevs = 2
                                    Else
                                        If Not driveTicksPerRev.HasValue Then
                                            driveTicksPerRev = usbClient.ReadTrack(2, 0).TicksPerRev
                                        End If
                                        ' Python's int() truncates toward zero; mirror with Math.Truncate.
                                        effectiveTicks = CInt(Math.Truncate(driveTicksPerRev.Value * preview.FractionalRevs.Value))
                                        effectiveRevs = 2
                                    End If
                                End If

                                ' Python read.py:186-191:
                                '   if args.hard_sectors:
                                '       args.revs = (args.hard_sectors + 1) * (args.revs + 1)
                                '       args.ticks = 0
                                If effectiveHardSectors AndAlso hardSectorCount > 0 Then
                                    effectiveRevs = (hardSectorCount + 1) * (effectiveRevs + 1)
                                    effectiveTicks = 0
                                End If

                                Dim safeTracks = preview.TrackSet.IteratePhysical().ToList()
                                For Each track In safeTracks
                                    ' Python read.py:197 always passes args.fmt_cls into read_with_retry,
                                    ' so --format triggers decode/verification regardless of --raw.
                                    Dim readResult = ReadWrite.ReadWithRetry(usbClient,
                                                                             track,
                                                                             effectiveRevs,
                                                                             context.Output,
                                                                             imgDisk,
                                                                             preview.Format,
                                                                             preview.Raw,
                                                                             effectiveHardSectors,
                                                                             preview.Reverse,
                                                                             preview.AdjustSpeed,
                                                                             preview.FakeIndexPeriod,
                                                                             effectiveTicks,
                                                                             driveTicksPerRev,
                                                                             preview.Retries,
                                                                             preview.SeekRetries,
                                                                             preview.GenTg43,
                                                                             preview.PllProfiles)
                                    Dim flux = readResult.Item1
                                    Dim dat = readResult.Item2
                                    ' Python read.py:198-200: collect codec results for end-of-run summary.
                                    If imgDisk IsNot Nothing AndAlso TypeOf dat Is Codec Then
                                        summary(Tuple.Create(track.Cyl, track.Head)) = CType(dat, Codec)
                                    End If
                                    ' Python read.py:201-204: `if args.raw: image.emit_track(cyl,head,flux)`
                                    ' fires regardless of image type. The fall-through emits decoded
                                    ' data for codec-aware sector images.
                                    If preview.Raw Then
                                        If writeScp Then
                                            scpImage.EmitTrack(track.Cyl, track.Head, flux)
                                        ElseIf writeRaw Then
                                            rawImage.EmitTrack(track.Cyl, track.Head, flux)
                                        ElseIf writeNsi Then
                                            nsiImage.EmitTrack(track.Cyl, track.Head, flux)
                                        ElseIf writeImd Then
                                            imdImage.EmitTrack(track.Cyl, track.Head, flux)
                                        ElseIf writeHfe Then
                                            hfeImage.EmitTrack(track.Cyl, track.Head, flux)
                                        ElseIf writeSector Then
                                            imgImage.EmitTrack(track.Cyl, track.Head, flux)
                                        ElseIf writeD88 Then
                                            d88Image.EmitTrack(track.Cyl, track.Head, flux)
                                        End If
                                    ElseIf writeScp Then
                                        scpImage.EmitTrack(track.Cyl, track.Head, flux)
                                    ElseIf writeRaw Then
                                        rawImage.EmitTrack(track.Cyl, track.Head, flux)
                                    ElseIf writeNsi Then
                                        If dat IsNot Nothing Then
                                            nsiImage.EmitTrack(track.Cyl, track.Head, dat)
                                        End If
                                    ElseIf writeImd Then
                                        If dat IsNot Nothing Then
                                            imdImage.EmitTrack(track.Cyl, track.Head, dat)
                                        End If
                                    ElseIf writeHfe Then
                                        If dat IsNot Nothing Then
                                            hfeImage.EmitTrack(track.Cyl, track.Head, dat)
                                        End If
                                    ElseIf writeSector Then
                                        If dat IsNot Nothing Then
                                            imgImage.EmitTrack(track.Cyl, track.Head, dat)
                                        End If
                                    ElseIf writeD88 Then
                                        If dat IsNot Nothing Then
                                            d88Image.EmitTrack(track.Cyl, track.Head, dat)
                                        End If
                                    End If
                                Next
                                ' Python read.py:206-207: print_summary when --format was supplied.
                                If imgDisk IsNot Nothing Then
                                    ReadWrite.PrintSummary(preview.TrackSet, summary, context.Output)
                                End If
                            End Sub,
                            New UsbDriveControlAdapter(usbClient),
                            preview.Drive,
                            motor:=True)
                    Catch err As CmdError
                        ' Python read.py:301-302: `except USB.CmdError as err: print("Command Failed: %s" % err)`
                        context.Output.WriteLine(String.Format("Command Failed: {0}", err.Message))
                        cmdFailed = True
                    End Try
                Finally
                    If usbClient IsNot Nothing AndAlso (preview.Densel.HasValue OrElse preview.GenTg43) AndAlso prevPin2.HasValue Then
                        ' Tolerate SetPin errors so an in-flight Ctrl-C path
                        ' (which may have torn down the serial port) doesn't
                        ' mask the originating KeyboardInterruptException.
                        Try : usbClient.SetPin(2, prevPin2.Value) : Catch : End Try
                    End If
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
                If cmdFailed Then
                    Return 0
                End If
                If writeScp Then
                    File.WriteAllBytes(outPath, scpImage.GetImage())
                    context.Output.WriteLine(String.Format("Wrote {0}", outPath))
                ElseIf writeImd Then
                    File.WriteAllBytes(outPath, imdImage.GetImage())
                    context.Output.WriteLine(String.Format("Wrote {0}", outPath))
                ElseIf writeHfe Then
                    File.WriteAllBytes(outPath, hfeImage.GetImage())
                    context.Output.WriteLine(String.Format("Wrote {0}", outPath))
                ElseIf writeSector Then
                    File.WriteAllBytes(outPath, imgImage.GetImage())
                    context.Output.WriteLine(String.Format("Wrote {0}", outPath))
                ElseIf writeRaw Then
                    context.Output.WriteLine(String.Format("Wrote KryoFlux tracks using basename {0}", outPath))
                ElseIf writeNsi Then
                    File.WriteAllBytes(outPath, nsiImage.GetImage())
                    context.Output.WriteLine(String.Format("Wrote {0}", outPath))
                ElseIf writeD88 Then
                    File.WriteAllBytes(outPath, d88Image.GetImage())
                    context.Output.WriteLine(String.Format("Wrote {0}", outPath))
                End If
            End If
            Return 0
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDiskDefinition)
        Private Shared Function ResolveDiskDefinition(formatName As String, diskDefsPath As String) As DiskDef
            Dim path = If(String.IsNullOrEmpty(diskDefsPath), FindDiskDefsPath(), diskDefsPath)
            Dim disk = DiskDefParser.GetDiskdef(formatName, path)
            ErrorHandling.Check(disk IsNot Nothing, String.Format("Unknown format '{0}'", formatName))
            Return disk
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindDiskDefsPath)
        Private Shared Function FindDiskDefsPath() As String
            Return "diskdefs.xml"
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsSectorImageExtension)
        Private Shared Function IsSectorImageExtension(ext As String) As Boolean
            Return String.Equals(ext, ".img", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ima", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".st", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".dsk", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adf", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".dsd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ads", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adm", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adl", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".mgt", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".sf7", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".hdm", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".xdf", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".2d", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".do", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".po", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DefaultFormatForSectorExtension)
        Private Shared Function DefaultFormatForSectorExtension(ext As String) As String
            If String.Equals(ext, ".adf", StringComparison.OrdinalIgnoreCase) Then
                Return "amiga.amigados"
            End If
            If String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.dfs.ss"
            End If
            If String.Equals(ext, ".dsd", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.dfs.ds"
            End If
            If String.Equals(ext, ".ads", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.160"
            End If
            If String.Equals(ext, ".adm", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.320"
            End If
            If String.Equals(ext, ".adl", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.640"
            End If
            If String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) Then
                Return "thomson.1s320"
            End If
            If String.Equals(ext, ".mgt", StringComparison.OrdinalIgnoreCase) Then
                Return "ibm.800"
            End If
            If String.Equals(ext, ".sf7", StringComparison.OrdinalIgnoreCase) Then
                Return "sega.sf7000"
            End If
            If String.Equals(ext, ".hdm", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".xdf", StringComparison.OrdinalIgnoreCase) Then
                Return "pc98.2hd"
            End If
            If String.Equals(ext, ".2d", StringComparison.OrdinalIgnoreCase) Then
                Return "sharp.2d"
            End If
            If String.Equals(ext, ".do", StringComparison.OrdinalIgnoreCase) Then
                Return "apple2.appledos.140"
            End If
            If String.Equals(ext, ".po", StringComparison.OrdinalIgnoreCase) Then
                Return "apple2.prodos.140"
            End If
            If String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1571"
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1541"
            End If
            If String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd2000.dd"
            End If
            If String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd2000.hd"
            End If
            If String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd4000.ed"
            End If
            If String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1581"
            End If
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration InferDimFormat)
        Private Shared Function InferDimFormat(path As String) As String
            Dim data = File.ReadAllBytes(path)
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 256, "DIM: Not a DIM file.")
            Dim sig = System.Text.Encoding.ASCII.GetString(data, &HAB, &HD)
            ErrorHandling.Check(String.Equals(sig, "DIFC HEADER  ", StringComparison.Ordinal), "DIM: Not a DIM file.")
            Dim mediaByte = CInt(data(0))
            If mediaByte = 0 Then
                Return "pc98.2hd"
            End If
            If mediaByte = 1 Then
                Return "pc98.2hs"
            End If
            Throw New FatalException("DIM: Unsupported format.")
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveReadOnlyImageTypeName)
        Private Shared Function ResolveReadOnlyImageTypeName(ext As String) As String
            If String.Equals(ext, ".dim", StringComparison.OrdinalIgnoreCase) Then Return "DIM"
            If String.Equals(ext, ".dmk", StringComparison.OrdinalIgnoreCase) Then Return "DMK"
            If String.Equals(ext, ".fdi", StringComparison.OrdinalIgnoreCase) Then Return "FDI"
            If String.Equals(ext, ".nfd", StringComparison.OrdinalIgnoreCase) Then Return "NFD"
            If String.Equals(ext, ".td0", StringComparison.OrdinalIgnoreCase) Then Return "TD0"
            If String.Equals(ext, ".a2r", StringComparison.OrdinalIgnoreCase) Then Return "A2R"
            If String.Equals(ext, ".dcp", StringComparison.OrdinalIgnoreCase) Then Return "DCP"
            If String.Equals(ext, ".ctr", StringComparison.OrdinalIgnoreCase) Then Return "CTRaw"
            If String.Equals(ext, ".ipf", StringComparison.OrdinalIgnoreCase) Then Return "IPF"
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ConfigureSectorImageDefaults)
        Private Shared Sub ConfigureSectorImageDefaults(image As Img, ext As String)
            If String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) Then
                image.Sequential = True
                Return
            End If
            If String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                image.Sequential = True
                Return
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) Then
                image.MinCylinders = 35
                Return
            End If
            If String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) Then
                image.SidesSwapped = True
            End If
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/write.py::main (direct command execution mapping).
    Public Class WriteAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("write", "Write a disk from the specified image file.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-scale-flux") Then
                Dim factor = Double.Parse(parsed("factor"), Globalization.CultureInfo.InvariantCulture)
                ' Python's wflux.list is List[float]; the parity harness feeds
                ' integer literals via CLI, so widen to Double here so that
                ' ScaleWriteFlux receives the same float-typed iterable Python
                ' does at write.py:103-109.
                Dim flux = parsed("flux").Split(","c).Select(Function(x) Double.Parse(x, Globalization.CultureInfo.InvariantCulture))
                Dim result = ReadWrite.ScaleWriteFlux(flux, factor)
                ' Parity harness output: force InvariantCulture so the round-trip
                ' specifier `{0:R}` always emits `.` as the decimal separator and
                ' the integer flux list never picks up a current-culture group
                ' separator. Otherwise the parity fixture diff would fail on any
                ' non-English Windows locale.
                Dim ci = Globalization.CultureInfo.InvariantCulture
                context.Output.WriteLine(String.Format(ci, "scaled={0}",
                                                       String.Join(",", result.ScaledFlux.Select(Function(x) x.ToString(ci)))))
                context.Output.WriteLine(String.Format(ci, "remainder={0:R}", result.FinalRemainder))
                Return 0
            End If
            Dim preview = ReadWrite.BuildWriteRuntimePreview(args, CodecRegistry.GetFormats())
            If Not String.IsNullOrEmpty(preview.Format) Then
                context.Output.WriteLine("Format " & preview.Format)
            End If
            context.Output.WriteLine("Writing " & preview.Tracks)
            If Not String.IsNullOrEmpty(preview.Precomp) Then
                context.Output.WriteLine(preview.Precomp)
            End If
            If preview.Live Then
                Dim inSplit = ConvertAction.SplitImageFileOptions(preview.FileName)
                Dim inPath = inSplit.Item1
                Dim inOpts = inSplit.Item2
                Dim inExt = Path.GetExtension(inPath)
                Dim useScpInput = String.Equals(inExt, ".scp", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useD64Input = String.Equals(inExt, ".d64", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useD71Input = String.Equals(inExt, ".d71", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useSectorInput = IsSectorImageExtension(inExt) AndAlso
                                     File.Exists(inPath)
                Dim useRawInput = String.Equals(inExt, ".raw", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useDimInput = String.Equals(inExt, ".dim", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useD88Input = String.Equals(inExt, ".d88", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useEdskInput = String.Equals(inExt, ".dsk", StringComparison.OrdinalIgnoreCase) AndAlso
                                   File.Exists(inPath) AndAlso
                                   IsEdskFile(inPath)
                Dim useApridiskInput = String.Equals(inExt, ".dsk", StringComparison.OrdinalIgnoreCase) AndAlso
                                       File.Exists(inPath) AndAlso
                                       IsApridiskFile(inPath)
                Dim useDmkInput = String.Equals(inExt, ".dmk", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useTd0Input = String.Equals(inExt, ".td0", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useFdiInput = String.Equals(inExt, ".fdi", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useNfdInput = String.Equals(inExt, ".nfd", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useDcpInput = String.Equals(inExt, ".dcp", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useCtrInput = String.Equals(inExt, ".ctr", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useIpfInput = String.Equals(inExt, ".ipf", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useA2rInput = String.Equals(inExt, ".a2r", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useMsaInput = String.Equals(inExt, ".msa", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useNsiInput = String.Equals(inExt, ".nsi", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useImdInput = String.Equals(inExt, ".imd", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim useHfeInput = String.Equals(inExt, ".hfe", StringComparison.OrdinalIgnoreCase) AndAlso
                                  File.Exists(inPath)
                Dim scpInput As Scp = Nothing
                Dim imgInput As Img = Nothing
                Dim d64Input As D64 = Nothing
                Dim rawInput As KryoFlux = Nothing
                Dim d88Input As D88 = Nothing
                Dim edskInput As Edsk = Nothing
                Dim apridiskInput As Apridisk = Nothing
                Dim dmkInput As Dmk = Nothing
                Dim td0Input As Td0 = Nothing
                Dim fdiInput As Fdi = Nothing
                Dim nfdInput As Nfd = Nothing
                Dim dcpInput As Dcp = Nothing
                Dim ctrInput As CTRaw = Nothing
                Dim ipfInput As IPF = Nothing
                Dim a2rInput As A2R = Nothing
                Dim msaInput As Msa = Nothing
                Dim nsiInput As Nsi = Nothing
                Dim imdInput As Imd = Nothing
                Dim hfeInput As Hfe = Nothing
                If useScpInput Then
                    scpInput = New Scp()
                    scpInput.FileName = inPath
                    scpInput.ApplyROpts(inOpts)
                    scpInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useD88Input Then
                    d88Input = New D88()
                    d88Input.FileName = inPath
                    d88Input.ApplyROpts(inOpts)
                    d88Input.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useEdskInput Then
                    edskInput = New Edsk()
                    edskInput.FileName = inPath
                    edskInput.ApplyROpts(inOpts)
                    edskInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useApridiskInput Then
                    ' Python write.py:271 calls `image_class.from_file(args.file, args.fmt_cls, ...)`
                    ' with fmt_cls=None tolerated. The Apridisk class infers format internally
                    ' when unspecified, so we must not require --format up front.
                    Dim disk As DiskDef = Nothing
                    If Not String.IsNullOrEmpty(preview.Format) Then
                        disk = ResolveDiskDefinition(preview.Format, preview.DiskDefsPath)
                    End If
                    apridiskInput = New Apridisk(disk)
                    apridiskInput.FileName = inPath
                    apridiskInput.ApplyROpts(inOpts)
                    apridiskInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useDmkInput Then
                    dmkInput = New Dmk()
                    dmkInput.FileName = inPath
                    dmkInput.ApplyROpts(inOpts)
                    dmkInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useTd0Input Then
                    td0Input = New Td0()
                    td0Input.FileName = inPath
                    td0Input.ApplyROpts(inOpts)
                    td0Input.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useFdiInput Then
                    fdiInput = New Fdi()
                    fdiInput.FileName = inPath
                    fdiInput.ApplyROpts(inOpts)
                    fdiInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useNfdInput Then
                    nfdInput = New Nfd()
                    nfdInput.FileName = inPath
                    nfdInput.ApplyROpts(inOpts)
                    nfdInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useDcpInput Then
                    dcpInput = New Dcp()
                    dcpInput.FileName = inPath
                    dcpInput.ApplyROpts(inOpts)
                    dcpInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useCtrInput Then
                    ctrInput = New CTRaw()
                    ctrInput.FileName = inPath
                    ctrInput.ApplyROpts(inOpts)
                    ctrInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useIpfInput Then
                    ipfInput = New IPF()
                    ipfInput.FileName = inPath
                    ipfInput.ApplyROpts(inOpts)
                    ipfInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useA2rInput Then
                    a2rInput = New A2R()
                    a2rInput.FileName = inPath
                    a2rInput.ApplyROpts(inOpts)
                    a2rInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useMsaInput Then
                    msaInput = New Msa()
                    msaInput.FileName = inPath
                    msaInput.ApplyROpts(inOpts)
                    msaInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useNsiInput Then
                    Dim effectiveFormat = preview.Format
                    If String.IsNullOrEmpty(effectiveFormat) Then
                        Dim size = New FileInfo(inPath).Length
                        If size = 1L * 35L * 10L * 256L Then
                            effectiveFormat = "northstar.fm.ss"
                        ElseIf size = 1L * 35L * 10L * 512L Then
                            effectiveFormat = "northstar.mfm.ss"
                        ElseIf size = 2L * 35L * 10L * 512L Then
                            effectiveFormat = "northstar.mfm.ds"
                        Else
                            Throw New FatalException(String.Format("NSI: {0}: unrecognised file size", inPath))
                        End If
                    End If
                    Dim disk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    nsiInput = New Nsi(disk)
                    nsiInput.FileName = inPath
                    nsiInput.ApplyROpts(inOpts)
                    nsiInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useD64Input OrElse useD71Input Then
                    Dim effectiveFormat = preview.Format
                    If String.IsNullOrEmpty(effectiveFormat) Then
                        effectiveFormat = ImageDefaults.DefaultFormatForExtension(inExt)
                    End If
                    ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "D64 input requires --format")
                    Dim disk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    d64Input = New D64(disk, effectiveFormat)
                    d64Input.Sequential = String.Equals(inExt, ".d71", StringComparison.OrdinalIgnoreCase)
                    d64Input.MinCylinders = If(String.Equals(inExt, ".d64", StringComparison.OrdinalIgnoreCase), CType(35, Integer?), Nothing)
                    d64Input.FileName = inPath
                    d64Input.ApplyROpts(inOpts)
                    d64Input.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useImdInput Then
                    imdInput = New Imd()
                    imdInput.FileName = inPath
                    imdInput.ApplyROpts(inOpts)
                    imdInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useHfeInput Then
                    hfeInput = New Hfe()
                    hfeInput.FileName = inPath
                    hfeInput.ApplyROpts(inOpts)
                    hfeInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useDimInput Then
                    Dim effectiveFormat = preview.Format
                    If String.IsNullOrEmpty(effectiveFormat) Then
                        effectiveFormat = InferDimFormat(inPath)
                    End If
                    Dim imgDisk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    imgInput = New [Dim](imgDisk)
                    imgInput.FileName = inPath
                    imgInput.ApplyROpts(inOpts)
                    imgInput.FromBytes(File.ReadAllBytes(inPath))
                ElseIf useSectorInput Then
                    ' Python write.py:271-273:
                    '   image = open_image(args, image_class)
                    '   if args.fmt_cls is None and isinstance(image, IMG):
                    '       args.fmt_cls = image.fmt
                    ' i.e. IMG can self-resolve its format from the file when --format
                    ' is omitted; we mirror that by tolerating a missing format and
                    ' falling back to the loaded image's Format property post-load.
                    Dim effectiveFormat = preview.Format
                    If String.IsNullOrEmpty(effectiveFormat) Then
                        effectiveFormat = ImageDefaults.DefaultFormatForExtension(inExt)
                    End If
                    Dim imgDisk As DiskDef = Nothing
                    If Not String.IsNullOrEmpty(effectiveFormat) Then
                        imgDisk = ResolveDiskDefinition(effectiveFormat, preview.DiskDefsPath)
                    End If
                    imgInput = New Img(imgDisk)
                    ConfigureSectorImageDefaults(imgInput, inExt)
                    imgInput.FileName = inPath
                    imgInput.ApplyROpts(inOpts)
                    imgInput.FromBytes(File.ReadAllBytes(inPath))
                    If imgDisk Is Nothing AndAlso imgInput.Format IsNot Nothing Then
                        imgDisk = imgInput.Format
                    End If
                    ErrorHandling.Check(imgDisk IsNot Nothing, "IMG input requires --format")
                ElseIf useRawInput Then
                    rawInput = New KryoFlux(inPath)
                Else
                    Throw New FatalException(String.Format("{0}: Unrecognised file suffix '{1}'", inPath, Path.GetExtension(inPath)))
                End If
                Dim usbClient As Unit = Nothing
                Dim prevPin2 As Nullable(Of Boolean) = Nothing
                Dim cmdFailed = False
                Try
                    Try
                        usbClient = ToolOptions.UsbOpen(preview.Device)
                        usbClient.ReadFirmwareInfo()
                        If preview.Densel.HasValue OrElse preview.GenTg43 Then
                            prevPin2 = usbClient.GetPin(2)
                        End If
                        If preview.Densel.HasValue Then
                            usbClient.SetPin(2, preview.Densel.Value)
                        End If
                        ToolOptions.WithDriveSelected(
                        Sub()
                            Dim noIndex = preview.FakeIndexPeriod.HasValue
                            Dim driveTicksPerRev As Double
                            Dim hardSectorCount = 0
                            If preview.HardSectors Then
                                ' Python write.py:40 uses `int(usb.sample_freq/2)`; truncate
                                ' toward zero rather than banker's-round to keep parity for
                                ' odd sample frequencies.
                                Dim fluxProbe = usbClient.ReadTrack(0, CInt(Math.Truncate(usbClient.SampleFreq / 2)))
                                fluxProbe.IdentifyHardSectors()
                                ErrorHandling.Check(fluxProbe.SectorList IsNot Nothing AndAlso fluxProbe.SectorList.Count > 0,
                                                   "Unable to identify hard sectors on this drive")
                                hardSectorCount = fluxProbe.SectorList(fluxProbe.SectorList.Count - 1).Count
                                driveTicksPerRev = fluxProbe.TicksPerRev
                                context.Output.WriteLine(String.Format("Drive reports {0} hard sectors", hardSectorCount))
                            ElseIf noIndex Then
                                driveTicksPerRev = preview.FakeIndexPeriod.Value * usbClient.SampleFreq
                            Else
                                driveTicksPerRev = usbClient.ReadTrack(2, 0).TicksPerRev
                            End If
                            ' Python write.py:45: `hard_sector_ticks = int(drive_ticks_per_rev / args.hard_sectors)`
                            ' int() truncates toward zero; CInt uses banker's rounding and
                            ' would diverge whenever the quotient lands on .5.
                            Dim hardSectorTicks = If(preview.HardSectors AndAlso hardSectorCount > 0,
                                                     CInt(Math.Truncate(driveTicksPerRev / hardSectorCount)),
                                                     0)
                            Dim precompSpec As PrecompSpec = Nothing
                            If Not String.IsNullOrEmpty(preview.PrecompSpec) Then
                                precompSpec = New PrecompSpec(preview.PrecompSpec)
                            End If
                            Dim formatDef As DiskDef = Nothing
                            If Not String.IsNullOrEmpty(preview.Format) Then
                                formatDef = ResolveDiskDefinition(preview.Format, preview.DiskDefsPath)
                            End If
                            Dim verifiedCount = 0
                            Dim notVerifiedCount = 0
                            Dim safeTracks = preview.TrackSet.IteratePhysical().ToList()
                            For Each track In safeTracks
                                If preview.GenTg43 Then
                                    usbClient.SetPin(2, track.Cyl < 43)
                                End If
                                Dim tspec = String.Format("T{0}.{1}", track.Cyl, track.Head)
                                If track.PhysicalCyl <> track.Cyl OrElse track.PhysicalHead <> track.Head Then
                                    tspec &= String.Format(" -> Drive {0}.{1}", track.PhysicalCyl, track.PhysicalHead)
                                End If
                                Dim PrepareSourceTrack As Func(Of HasFlux, HasFlux) =
                                    Function(source As HasFlux) As HasFlux
                                        Dim prepared = source
                                        If formatDef IsNot Nothing AndAlso Not TypeOf prepared Is Codec Then
                                            Dim decoded = formatDef.DecodeFlux(track.Cyl, track.Head, prepared)
                                            If decoded Is Nothing Then
                                                context.Output.WriteLine(String.Format("{0}: WARNING: Out of range for format '{1}': Track skipped",
                                                                                       tspec,
                                                                                       preview.Format))
                                                Return Nothing
                                            End If
                                            ErrorHandling.Check(decoded.NrMissing() = 0,
                                                               String.Format("{0}: {1} missing sectors in input image",
                                                                             tspec,
                                                                             decoded.NrMissing()))
                                            prepared = decoded
                                        End If
                                        If TypeOf prepared Is Codec Then
                                            prepared = CType(prepared, Codec).MasterTrack()
                                        End If
                                        If TypeOf prepared Is MasterTrack Then
                                            Dim master = CType(prepared, MasterTrack)
                                            If preview.Reverse Then
                                                master.Reverse()
                                            End If
                                            If precompSpec IsNot Nothing Then
                                                master.Precomp = precompSpec.TrackPrecomp(track.Cyl)
                                            End If
                                            prepared = master
                                        ElseIf preview.Reverse Then
                                            Dim fluxTrack = prepared.Flux()
                                            fluxTrack.Reverse()
                                            prepared = fluxTrack
                                        End If
                                        Return prepared
                                    End Function
                                Dim WriteScaledTrack =
                                    Sub(scaledFlux As List(Of Integer),
                                        terminateAtIndex As Boolean,
                                        cueAtIndex As Boolean,
                                        writeSummary As String,
                                        sourceTrack As HasFlux)
                                        Dim verified = False
                                        For retry = 0 To preview.Retries
                                            If preview.PreErase Then
                                                context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                                usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                            End If
                                            Dim status = String.Format("{0}: Writing Track", tspec)
                                            ' Python write.py:117-122 always appends `(<wflux summary>)` on
                                            ' the first attempt and `(Verify Failure: Retry #N)` thereafter.
                                            If retry <> 0 Then
                                                status &= String.Format(" (Verify Failure: Retry #{0})", retry)
                                            Else
                                                status &= String.Format(" ({0})", If(writeSummary, String.Empty))
                                            End If
                                            context.Output.WriteLine(status)
                                            usbClient.WriteTrack(scaledFlux,
                                                                 terminateAtIndex:=terminateAtIndex,
                                                                 cueAtIndex:=cueAtIndex,
                                                                 hardSectorTicks:=hardSectorTicks)
                                            Dim verifySource As MasterTrack = Nothing
                                            If TypeOf sourceTrack Is MasterTrack Then
                                                verifySource = CType(sourceTrack, MasterTrack)
                                            ElseIf TypeOf sourceTrack Is Codec Then
                                                verifySource = CType(sourceTrack, Codec).MasterTrack()
                                            End If
                                            Dim verify As HasVerify = Nothing
                                            Dim noVerify = preview.NoVerify OrElse verifySource Is Nothing
                                            If Not noVerify Then
                                                verify = verifySource.Verify
                                                noVerify = (verify Is Nothing)
                                            End If
                                            If noVerify Then
                                                notVerifiedCount += 1
                                                verified = True
                                                Exit For
                                            End If
                                            Dim verifyRevs = verify.VerifyRevs
                                            Dim vTicks = 0
                                            Dim vRevs = CInt(Math.Truncate(verifyRevs))
                                            ' Python write.py:137-139: `if isinstance(v_revs, float): v_ticks = int(...)`.
                                            ' Python's int() truncates toward zero; mirror with Math.Truncate
                                            ' so banker's rounding from CInt() can't shift the tick budget.
                                            If Math.Abs(verifyRevs - Math.Truncate(verifyRevs)) > 0.0000001 Then
                                                vTicks = CInt(Math.Truncate(driveTicksPerRev * verifyRevs))
                                                vRevs = 2
                                            End If
                                            If preview.HardSectors Then
                                                vTicks = 0
                                                vRevs = (hardSectorCount + 1) * 2
                                            End If
                                            Dim vFlux As Flux
                                            If noIndex Then
                                                Dim driveTpr = CInt(Math.Truncate(driveTicksPerRev))
                                                Dim preIndex = CInt(Math.Truncate(usbClient.SampleFreq * 0.5E-3))
                                                If vTicks = 0 Then
                                                    vTicks = vRevs * driveTpr + 2 * preIndex
                                                End If
                                                vFlux = usbClient.ReadTrack(0, vTicks)
                                                Dim indexList As New List(Of Double) From {preIndex}
                                                Dim nrIndexes = (vTicks - preIndex) \ driveTpr
                                                For idx = 0 To nrIndexes - 1
                                                    indexList.Add(driveTpr)
                                                Next
                                                vFlux.IndexList = indexList
                                            Else
                                                vFlux = usbClient.ReadTrack(vRevs, vTicks)
                                            End If
                                            vFlux.TicksPerRev = driveTicksPerRev
                                            If preview.Reverse Then
                                                vFlux.Reverse()
                                            End If
                                            If preview.HardSectors Then
                                                vFlux.IdentifyHardSectors()
                                            End If
                                            verified = verify.VerifyTrack(vFlux)
                                            If verified Then
                                                verifiedCount += 1
                                                Exit For
                                            End If
                                        Next
                                        ErrorHandling.Check(verified,
                                                           String.Format("Failed to verify Track {0}.{1}",
                                                                         track.Cyl,
                                                                         track.Head))
                                    End Sub
                                usbClient.Seek(track.PhysicalCyl, track.PhysicalHead)
                                If useScpInput Then
                                    Dim inputTrack = scpInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useD88Input Then
                                    Dim inputTrack = d88Input.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useDmkInput Then
                                    Dim inputTrack = dmkInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useEdskInput Then
                                    Dim inputTrack = edskInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useApridiskInput Then
                                    Dim inputTrack = apridiskInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useTd0Input Then
                                    Dim inputTrack = td0Input.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useFdiInput Then
                                    Dim inputTrack = fdiInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useNfdInput Then
                                    Dim inputTrack = nfdInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useDcpInput Then
                                    Dim inputTrack = dcpInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useCtrInput Then
                                    Dim inputTrack = ctrInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useIpfInput Then
                                    Dim inputTrack = ipfInput.GetTrack(track.Cyl, track.Head)
                                    If inputTrack Is Nothing Then
                                        If preview.EraseEmpty Then
                                            context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                            usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                        End If
                                        Continue For
                                    End If
                                    Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                                    If source Is Nothing Then
                                        Continue For
                                    End If
                                    Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                                    Dim factor = driveTicksPerRev / wflux.TicksToIndex
                                    ' Python write.py:103-109 feeds wflux.list (float) directly
                                    ' into the residual-carrying scale loop; pre-rounding the
                                    ' floats to ints would discard the fractional input that
                                    ' the Bresenham residual depends on, so pass wflux.List
                                    ' (List(Of Double)) through unchanged.
                                    Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                                    WriteScaledTrack(scaled,
                                                         terminateAtIndex:=wflux.TerminateAtIndex,
                                                         cueAtIndex:=wflux.IndexCued,
                                                         writeSummary:=wflux.SummaryString(),
                                                         sourceTrack:=source)
                                ElseIf useA2rInput Then
                            Dim inputTrack = a2rInput.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        ElseIf useMsaInput Then
                            Dim inputTrack = msaInput.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        ElseIf useNsiInput Then
                            Dim inputTrack = nsiInput.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        ElseIf useD64Input OrElse useD71Input Then
                            Dim inputTrack = d64Input.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        ElseIf useImdInput Then
                            Dim inputTrack = imdInput.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        ElseIf useHfeInput Then
                            Dim inputTrack = hfeInput.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        ElseIf useSectorInput OrElse useDimInput Then
                            Dim inputTrack = imgInput.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        ElseIf useRawInput Then
                            Dim inputTrack = rawInput.GetTrack(track.Cyl, track.Head)
                            If inputTrack Is Nothing Then
                                If preview.EraseEmpty Then
                                    context.Output.WriteLine(String.Format("{0}: Erasing Track", tspec))
                                    usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                End If
                                Continue For
                            End If
                            Dim source = PrepareSourceTrack(CType(inputTrack, HasFlux))
                            If source Is Nothing Then
                                Continue For
                            End If
                            Dim wflux = source.FluxForWriteout(cueAtIndex:=Not noIndex)
                            Dim factor = driveTicksPerRev / wflux.TicksToIndex
                            ' See note above: feed wflux.List (List(Of Double)) directly so
                            ' the per-element residual loop in ScaleWriteFlux sees the
                            ' fractional inputs Python does at write.py:103-109.
                            Dim scaled = ReadWrite.ScaleWriteFlux(wflux.List, factor).ScaledFlux
                            WriteScaledTrack(scaled,
                                                 terminateAtIndex:=wflux.TerminateAtIndex,
                                                 cueAtIndex:=wflux.IndexCued,
                                                 writeSummary:=wflux.SummaryString(),
                                                 sourceTrack:=source)
                        End If
                            Next
                            If notVerifiedCount = 0 Then
                                context.Output.WriteLine("All tracks verified")
                            Else
                                If verifiedCount = 0 Then
                                    context.Output.Write("No tracks verified ")
                                Else
                                    context.Output.Write(String.Format("{0} tracks verified; {1} tracks *not* verified ",
                                                                       verifiedCount,
                                                                       notVerifiedCount))
                                End If
                                context.Output.WriteLine(String.Format("(Reason: Verify {0})",
                                                                       If(preview.NoVerify, "disabled", "unavailable")))
                            End If
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=True)
                    Catch err As CmdError
                        ' Python write.py:297-298: `except USB.CmdError as err: print("Command Failed: %s" % err)`
                        context.Output.WriteLine(String.Format("Command Failed: {0}", err.Message))
                        cmdFailed = True
                    End Try
                Finally
                    If usbClient IsNot Nothing AndAlso (preview.Densel.HasValue OrElse preview.GenTg43) AndAlso prevPin2.HasValue Then
                        ' Tolerate SetPin errors so an in-flight Ctrl-C path
                        ' (which may have torn down the serial port) doesn't
                        ' mask the originating KeyboardInterruptException.
                        Try : usbClient.SetPin(2, prevPin2.Value) : Catch : End Try
                    End If
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
                If cmdFailed Then
                    Return 0
                End If
            End If
            Return 0
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDiskDefinition)
        Private Shared Function ResolveDiskDefinition(formatName As String, diskDefsPath As String) As DiskDef
            Dim path = If(String.IsNullOrEmpty(diskDefsPath), FindDiskDefsPath(), diskDefsPath)
            Dim disk = DiskDefParser.GetDiskdef(formatName, path)
            ErrorHandling.Check(disk IsNot Nothing, String.Format("Unknown format '{0}'", formatName))
            Return disk
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindDiskDefsPath)
        Private Shared Function FindDiskDefsPath() As String
            Return "diskdefs.xml"
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsSectorImageExtension)
        Private Shared Function IsSectorImageExtension(ext As String) As Boolean
            Return String.Equals(ext, ".img", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ima", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".st", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".dsk", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adf", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".dsd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ads", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adm", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adl", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".mgt", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".sf7", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".hdm", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".xdf", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".2d", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".do", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".po", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DefaultFormatForSectorExtension)
        Private Shared Function DefaultFormatForSectorExtension(ext As String) As String
            If String.Equals(ext, ".adf", StringComparison.OrdinalIgnoreCase) Then
                Return "amiga.amigados"
            End If
            If String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.dfs.ss"
            End If
            If String.Equals(ext, ".dsd", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.dfs.ds"
            End If
            If String.Equals(ext, ".ads", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.160"
            End If
            If String.Equals(ext, ".adm", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.320"
            End If
            If String.Equals(ext, ".adl", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.640"
            End If
            If String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) Then
                Return "thomson.1s320"
            End If
            If String.Equals(ext, ".mgt", StringComparison.OrdinalIgnoreCase) Then
                Return "ibm.800"
            End If
            If String.Equals(ext, ".sf7", StringComparison.OrdinalIgnoreCase) Then
                Return "sega.sf7000"
            End If
            If String.Equals(ext, ".hdm", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".xdf", StringComparison.OrdinalIgnoreCase) Then
                Return "pc98.2hd"
            End If
            If String.Equals(ext, ".2d", StringComparison.OrdinalIgnoreCase) Then
                Return "sharp.2d"
            End If
            If String.Equals(ext, ".do", StringComparison.OrdinalIgnoreCase) Then
                Return "apple2.appledos.140"
            End If
            If String.Equals(ext, ".po", StringComparison.OrdinalIgnoreCase) Then
                Return "apple2.prodos.140"
            End If
            If String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1571"
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1541"
            End If
            If String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd2000.dd"
            End If
            If String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd2000.hd"
            End If
            If String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd4000.ed"
            End If
            If String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1581"
            End If
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration InferDimFormat)
        Private Shared Function InferDimFormat(path As String) As String
            Dim data = File.ReadAllBytes(path)
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 256, "DIM: Not a DIM file.")
            Dim sig = System.Text.Encoding.ASCII.GetString(data, &HAB, &HD)
            ErrorHandling.Check(String.Equals(sig, "DIFC HEADER  ", StringComparison.Ordinal), "DIM: Not a DIM file.")
            Dim mediaByte = CInt(data(0))
            If mediaByte = 0 Then
                Return "pc98.2hd"
            End If
            If mediaByte = 1 Then
                Return "pc98.2hs"
            End If
            Throw New FatalException("DIM: Unsupported format.")
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsEdskFile)
        Private Shared Function IsEdskFile(path As String) As Boolean
            If Not File.Exists(path) Then
                Return False
            End If
            Dim sig(33) As Byte
            Using fs = File.OpenRead(path)
                If fs.Read(sig, 0, sig.Length) <> sig.Length Then
                    Return False
                End If
            End Using
            Dim text = System.Text.Encoding.ASCII.GetString(sig)
            Return text.StartsWith("MV - CPC", StringComparison.Ordinal) OrElse
                   text.StartsWith("EXTENDED CPC DSK", StringComparison.Ordinal)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsApridiskFile)
        Private Shared Function IsApridiskFile(path As String) As Boolean
            If Not File.Exists(path) Then
                Return False
            End If
            Dim sig(23) As Byte
            Using fs = File.OpenRead(path)
                If fs.Read(sig, 0, sig.Length) <> sig.Length Then
                    Return False
                End If
            End Using
            Dim expected = System.Text.Encoding.ASCII.GetBytes("ACT Apricot disk image")
            For i = 0 To expected.Length - 1
                If sig(i) <> expected(i) Then
                    Return False
                End If
            Next
            Return sig(21) = &H1A AndAlso sig(22) = &H4
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ConfigureSectorImageDefaults)
        Private Shared Sub ConfigureSectorImageDefaults(image As Img, ext As String)
            If String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) Then
                image.Sequential = True
                Return
            End If
            If String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                image.Sequential = True
                Return
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) Then
                image.MinCylinders = 35
                Return
            End If
            If String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) Then
                image.SidesSwapped = True
            End If
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/convert.py::main (direct command execution mapping).
    Public Class ConvertAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("convert", "Convert between image formats.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-track-summary") Then
                Dim cyl = Integer.Parse(parsed("cyl"), CultureInfo.InvariantCulture)
                Dim head = Integer.Parse(parsed("head"), CultureInfo.InvariantCulture)
                Dim physicalCyl = Integer.Parse(parsed("physical-cyl"), CultureInfo.InvariantCulture)
                Dim physicalHead = Integer.Parse(parsed("physical-head"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(Convert.BuildTrackSummary(cyl, head, physicalCyl, physicalHead))
                Return 0
            End If
            If parsed.ContainsKey("parity-convert-header") Then
                context.Output.WriteLine(Convert.BuildConvertHeader(parsed("tracks"), parsed("out-tracks")))
                Return 0
            End If
            If parsed.ContainsKey("parity-resolve-format") Then
                Dim explicitFormat As String = Nothing
                Dim inputDefault As String = Nothing
                Dim outputDefault As String = Nothing
                parsed.TryGetValue("explicit-format", explicitFormat)
                parsed.TryGetValue("input-default", inputDefault)
                parsed.TryGetValue("output-default", outputDefault)
                Dim resolved = Convert.ResolveFormat(explicitFormat, inputDefault, outputDefault)
                context.Output.WriteLine(String.Format("format={0}", resolved))
                Return 0
            End If
            If parsed.ContainsKey("parity-resolve-tracks") Then
                Dim formatTracksSpec As String = Nothing
                Dim tracksSpec As String = Nothing
                Dim outTracksSpec As String = Nothing
                parsed.TryGetValue("format-tracks", formatTracksSpec)
                parsed.TryGetValue("tracks", tracksSpec)
                parsed.TryGetValue("out-tracks", outTracksSpec)
                Dim formatTracks = If(String.IsNullOrEmpty(formatTracksSpec), Nothing, New Greaseweazle.Shared.TrackSet(formatTracksSpec))
                Dim result = Convert.ResolveTrackSets(formatTracks, tracksSpec, outTracksSpec)
                context.Output.WriteLine(String.Format("tracks={0}", result.Item1.ToString()))
                context.Output.WriteLine(String.Format("out_tracks={0}", result.Item2.ToString()))
                Return 0
            End If
            If parsed.ContainsKey("parity-loop-sim") Then
                Dim outTracks As New List(Of ConvertOutTrackAddress)()
                Dim inTracks As New List(Of ConvertTrackAddress)()
                Dim availableTracks As New List(Of ConvertTrackAddress)()
                Dim outTokens As String = Nothing
                parsed.TryGetValue("out-tracks-map", outTokens)
                If Not String.IsNullOrEmpty(outTokens) Then
                    For Each token In outTokens.Split("|"c)
                        Dim parts = token.Split(">"c)
                        Dim logical = parts(0).Split("."c)
                        Dim physical = parts(1).Split("."c)
                        outTracks.Add(New ConvertOutTrackAddress With {
                            .Cyl = Integer.Parse(logical(0), CultureInfo.InvariantCulture),
                            .Head = Integer.Parse(logical(1), CultureInfo.InvariantCulture),
                            .PhysicalCyl = Integer.Parse(physical(0), CultureInfo.InvariantCulture),
                            .PhysicalHead = Integer.Parse(physical(1), CultureInfo.InvariantCulture)
                        })
                    Next
                End If
                Dim inTokens As String = Nothing
                parsed.TryGetValue("in-tracks", inTokens)
                If Not String.IsNullOrEmpty(inTokens) Then
                    For Each token In inTokens.Split("|"c)
                        Dim parts = token.Split("."c)
                        inTracks.Add(New ConvertTrackAddress With {
                            .Cyl = Integer.Parse(parts(0), CultureInfo.InvariantCulture),
                            .Head = Integer.Parse(parts(1), CultureInfo.InvariantCulture)
                        })
                    Next
                End If
                Dim availableTokens As String = Nothing
                parsed.TryGetValue("available-tracks", availableTokens)
                If Not String.IsNullOrEmpty(availableTokens) Then
                    For Each token In availableTokens.Split("|"c)
                        Dim parts = token.Split("."c)
                        availableTracks.Add(New ConvertTrackAddress With {
                            .Cyl = Integer.Parse(parts(0), CultureInfo.InvariantCulture),
                            .Head = Integer.Parse(parts(1), CultureInfo.InvariantCulture)
                        })
                    Next
                End If
                Dim cacheEnabled = Integer.Parse(parsed("cache"), CultureInfo.InvariantCulture) <> 0
                Dim result = Convert.SimulateConvertLoop(outTracks, inTracks, availableTracks, cacheEnabled)
                context.Output.WriteLine(String.Format("process={0}", String.Join(",", result.ProcessCalls)))
                context.Output.WriteLine(String.Format("emit={0}", String.Join(",", result.EmitTargets)))
                context.Output.WriteLine(String.Format("cache={0}", String.Join(",", result.CacheKeys)))
                Return 0
            End If
            Dim preview = Convert.BuildRuntimePreview(args, CodecRegistry.GetFormats())
            Dim inputPath = SplitImageFileOptions(preview.InputFile).Item1
            Dim outputPath = SplitImageFileOptions(preview.OutputFile).Item1
            ' Python convert.py:158-164 consults `image_class.default_format` for both
            ' input and output, falling back from explicit --format to input default to
            ' output default. Use the centralised ImageDefaults table so .fdi/.2d/.d?m
            ' are covered consistently with read/write.
            Dim inputDefaultFormat = ImageDefaults.DefaultFormatForFile(inputPath)
            Dim outputDefaultFormat = ImageDefaults.DefaultFormatForFile(outputPath)
            Dim effectiveFormat = Convert.ResolveFormat(preview.Format, inputDefaultFormat, outputDefaultFormat)

            If preview.NoClobber AndAlso File.Exists(outputPath) Then
                Throw New FatalException(String.Format("{0}: File exists", outputPath))
            End If

            ' Python convert.py opens the input image FIRST so the IMG.fmt fallback
            ' (lines 176-177) can populate args.fmt_cls before open_output_image is
            ' called with the now-resolved DiskDef. Mirror that ordering: input
            ' image -> resolve fmtCls (with IMG fallback) -> open output image.
            Dim inputImage = OpenImageForRead(preview.InputFile, effectiveFormat, preview.DiskDefsPath)
            Dim fmtCls As DiskDef = Nothing
            If Not String.IsNullOrEmpty(effectiveFormat) Then
                fmtCls = ResolveDiskDefinitionForConvert(effectiveFormat, preview.DiskDefsPath)
            ElseIf TypeOf inputImage Is Img Then
                fmtCls = CType(inputImage, Img).Format
            End If
            ' If the IMG.fmt fallback resolved a DiskDef, plumb its name back into
            ' effectiveFormat so OpenImageForWrite (which keys off the format string)
            ' picks up the same disk definition.
            If String.IsNullOrEmpty(effectiveFormat) AndAlso fmtCls IsNot Nothing Then
                effectiveFormat = fmtCls.Name
            End If
            Dim outputImage = OpenImageForWrite(preview.OutputFile, effectiveFormat, preview.DiskDefsPath)
            outputImage.NoClobber = preview.NoClobber
            Dim resolvedTracks = Convert.ResolveTrackSets(If(fmtCls IsNot Nothing, fmtCls.Tracks, Nothing),
                                                          preview.TracksSpec,
                                                          preview.OutTracksSpec)
            If Not String.IsNullOrEmpty(effectiveFormat) Then
                context.Output.WriteLine("Format " & effectiveFormat)
            End If
            context.Output.WriteLine(Convert.BuildConvertHeader(resolvedTracks.Item1.ToString(),
                                                                resolvedTracks.Item2.ToString()))
            ConvertFunctions.Convert(resolvedTracks.Item2.IteratePhysical().ToList(),
                                     resolvedTracks.Item1,
                                     inputImage,
                                     outputImage,
                                     context.Output,
                                     fmtCls,
                                     effectiveFormat,
                                     preview.Reverse,
                                     preview.HardSectors,
                                     preview.AdjustSpeed,
                                     preview.PllProfiles)
            Dim outExt = Path.GetExtension(outputPath)
            If IsSectorImageExtension(outExt) OrElse
               String.Equals(outExt, ".imd", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(outExt, ".hfe", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(outExt, ".d88", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(outExt, ".scp", StringComparison.OrdinalIgnoreCase) Then
                File.WriteAllBytes(outputPath, outputImage.GetImage())
            End If
            Return 0
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration OpenImageForRead)
        Private Shared Function OpenImageForRead(fileName As String, formatName As String, diskDefsPath As String) As Image
            Dim split = SplitImageFileOptions(fileName)
            Dim resolvedName = split.Item1
            Dim opts = split.Item2
            Dim ext = Path.GetExtension(resolvedName)
            If String.Equals(ext, ".hfe", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Hfe()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".imd", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Imd()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".d88", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New D88()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".dsk", StringComparison.OrdinalIgnoreCase) AndAlso IsEdskFile(resolvedName) Then
                Dim image As New Edsk()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".dsk", StringComparison.OrdinalIgnoreCase) AndAlso IsApridiskFile(resolvedName) Then
                ErrorHandling.Check(Not String.IsNullOrEmpty(formatName), "Apridisk input requires --format")
                Dim disk = ResolveDiskDefinitionForConvert(formatName, diskDefsPath)
                Dim image As New Apridisk(disk)
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".td0", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Td0()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".fdi", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Fdi()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".nfd", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Nfd()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".dcp", StringComparison.OrdinalIgnoreCase) Then
                ' Python: DCP inherits IMG_AutoFormat which auto-loads the diskdef
                ' from format_from_file(name) when --format is not supplied.
                Dim dcpFormat = If(String.IsNullOrEmpty(formatName), Dcp.FormatFromFile(resolvedName), formatName)
                Dim disk = ResolveDiskDefinitionForConvert(dcpFormat, diskDefsPath)
                Dim image As New Dcp(disk)
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".ctr", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New CTRaw()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".ipf", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New IPF()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".a2r", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New A2R()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".msa", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Msa()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".nsi", StringComparison.OrdinalIgnoreCase) Then
                Dim effectiveFormat = formatName
                If String.IsNullOrEmpty(effectiveFormat) Then
                    Dim size = New FileInfo(resolvedName).Length
                    If size = 1L * 35L * 10L * 256L Then
                        effectiveFormat = "northstar.fm.ss"
                    ElseIf size = 1L * 35L * 10L * 512L Then
                        effectiveFormat = "northstar.mfm.ss"
                    ElseIf size = 2L * 35L * 10L * 512L Then
                        effectiveFormat = "northstar.mfm.ds"
                    Else
                        Throw New FatalException(String.Format("NSI: {0}: unrecognised file size", resolvedName))
                    End If
                End If
                Dim disk = ResolveDiskDefinitionForConvert(effectiveFormat, diskDefsPath)
                Dim image As New Nsi(disk)
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                Dim effectiveFormat = formatName
                If String.IsNullOrEmpty(effectiveFormat) Then
                    effectiveFormat = DefaultFormatForSectorExtension(ext)
                End If
                ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "D64 input requires --format")
                Dim disk = ResolveDiskDefinitionForConvert(effectiveFormat, diskDefsPath)
                Dim image As New D64(disk, effectiveFormat)
                image.Sequential = String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase)
                image.MinCylinders = If(String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase), CType(35, Integer?), Nothing)
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".dmk", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Dmk()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".scp", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Scp()
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If String.Equals(ext, ".raw", StringComparison.OrdinalIgnoreCase) Then
                Return New KryoFlux(resolvedName)
            End If
            If String.Equals(ext, ".dim", StringComparison.OrdinalIgnoreCase) Then
                Dim effectiveFormat = formatName
                If String.IsNullOrEmpty(effectiveFormat) Then
                    effectiveFormat = InferDimFormat(resolvedName)
                End If
                Dim disk = ResolveDiskDefinitionForConvert(effectiveFormat, diskDefsPath)
                Dim image As New [Dim](disk)
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            If IsSectorImageExtension(ext) Then
                Dim effectiveFormat = formatName
                If String.IsNullOrEmpty(effectiveFormat) Then
                    effectiveFormat = DefaultFormatForSectorExtension(ext)
                End If
                ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "IMG input requires --format")
                Dim disk = ResolveDiskDefinitionForConvert(effectiveFormat, diskDefsPath)
                Dim image As New Img(disk)
                ConfigureSectorImageDefaults(image, ext)
                image.FileName = resolvedName
                image.ApplyROpts(opts)
                image.FromBytes(File.ReadAllBytes(resolvedName))
                Return image
            End If
            Throw New FatalException(String.Format("{0}: Unrecognised file suffix '{1}'", resolvedName, ext))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration OpenImageForWrite)
        Private Shared Function OpenImageForWrite(fileName As String, formatName As String, diskDefsPath As String) As Image
            Dim split = SplitImageFileOptions(fileName)
            Dim resolvedName = split.Item1
            Dim opts = split.Item2
            Dim ext = Path.GetExtension(resolvedName)
            Dim readOnlyType = ResolveReadOnlyImageTypeName(ext)
            If String.Equals(ext, ".hfe", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Hfe()
                image.FileName = resolvedName
                image.ApplyWOpts(opts)
                Return image
            End If
            If String.Equals(ext, ".imd", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Imd()
                image.FileName = resolvedName
                image.ApplyWOpts(opts)
                Return image
            End If
            If String.Equals(ext, ".msa", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Msa()
                image.FileName = resolvedName
                image.ApplyWOpts(opts)
                Return image
            End If
            If String.Equals(ext, ".nsi", StringComparison.OrdinalIgnoreCase) Then
                ErrorHandling.Check(Not String.IsNullOrEmpty(formatName), "NSI output requires --format")
                Dim disk = ResolveDiskDefinitionForConvert(formatName, diskDefsPath)
                Dim image As New Nsi(disk)
                image.FileName = resolvedName
                image.ApplyWOpts(opts)
                Return image
            End If
            If String.Equals(ext, ".d88", StringComparison.OrdinalIgnoreCase) Then
                ErrorHandling.Check(Not String.IsNullOrEmpty(formatName), "D88 output requires --format")
                Dim disk = ResolveDiskDefinitionForConvert(formatName, diskDefsPath)
                Dim image As New D88(disk)
                image.FileName = resolvedName
                image.ApplyWOpts(opts)
                Return image
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                Dim effectiveFormat = formatName
                If String.IsNullOrEmpty(effectiveFormat) Then
                    effectiveFormat = DefaultFormatForSectorExtension(ext)
                End If
                ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "D64 output requires --format")
                Dim disk = ResolveDiskDefinitionForConvert(effectiveFormat, diskDefsPath)
                Dim image As New D64(disk, effectiveFormat)
                image.Sequential = String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase)
                image.MinCylinders = If(String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase), CType(35, Integer?), Nothing)
                image.FileName = resolvedName
                image.ApplyWOpts(opts)
                Return image
            End If
            If Not String.IsNullOrEmpty(readOnlyType) Then
                Throw New FatalException(String.Format("{0}: Cannot create {1} image files", resolvedName, readOnlyType))
            End If
            If String.Equals(ext, ".scp", StringComparison.OrdinalIgnoreCase) Then
                Dim image As New Scp()
                image.FileName = resolvedName
                image.ApplyWOpts(opts)
                Return image
            End If
            If String.Equals(ext, ".raw", StringComparison.OrdinalIgnoreCase) Then
                Return New KryoFlux(resolvedName)
            End If
            If IsSectorImageExtension(ext) Then
                Dim effectiveFormat = formatName
                If String.IsNullOrEmpty(effectiveFormat) Then
                    effectiveFormat = DefaultFormatForSectorExtension(ext)
                End If
                ErrorHandling.Check(Not String.IsNullOrEmpty(effectiveFormat), "IMG output requires --format")
                Dim disk = ResolveDiskDefinitionForConvert(effectiveFormat, diskDefsPath)
                Dim image As New Img(disk) With {.FileName = resolvedName}
                ConfigureSectorImageDefaults(image, ext)
                image.ApplyWOpts(opts)
                Return image
            End If
            Throw New FatalException(String.Format("{0}: Unrecognised file suffix '{1}'", resolvedName, ext))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SplitImageFileOptions)
        Friend Shared Function SplitImageFileOptions(spec As String) As Tuple(Of String, Dictionary(Of String, String))
            Dim raw = If(spec, String.Empty)
            Dim parts = raw.Split(New String() {"::"}, StringSplitOptions.None)
            Dim name = parts(0)
            Dim opts As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For i = 1 To parts.Length - 1
                For Each token In parts(i).Split(":"c)
                    If String.IsNullOrEmpty(token) Then
                        Continue For
                    End If
                    Dim eq = token.IndexOf("="c)
                    Dim key As String
                    Dim value As String
                    If eq < 0 Then
                        key = token
                        value = "yes"
                    Else
                        key = token.Substring(0, eq)
                        value = token.Substring(eq + 1)
                    End If
                    If Not String.IsNullOrEmpty(key) Then
                        opts(key) = value
                    End If
                Next
            Next
            Return Tuple.Create(name, opts)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsSectorImageExtension)
        Private Shared Function IsSectorImageExtension(ext As String) As Boolean
            Return String.Equals(ext, ".img", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ima", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".st", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".dsk", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adf", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".dsd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".ads", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adm", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".adl", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".mgt", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".sf7", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".hdm", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".xdf", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".2d", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".do", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".po", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DefaultFormatForSectorExtension)
        Private Shared Function DefaultFormatForSectorExtension(ext As String) As String
            If String.Equals(ext, ".adf", StringComparison.OrdinalIgnoreCase) Then
                Return "amiga.amigados"
            End If
            If String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.dfs.ss"
            End If
            If String.Equals(ext, ".dsd", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.dfs.ds"
            End If
            If String.Equals(ext, ".ads", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.160"
            End If
            If String.Equals(ext, ".adm", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.320"
            End If
            If String.Equals(ext, ".adl", StringComparison.OrdinalIgnoreCase) Then
                Return "acorn.adfs.640"
            End If
            If String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) Then
                Return "thomson.1s320"
            End If
            If String.Equals(ext, ".mgt", StringComparison.OrdinalIgnoreCase) Then
                Return "ibm.800"
            End If
            If String.Equals(ext, ".sf7", StringComparison.OrdinalIgnoreCase) Then
                Return "sega.sf7000"
            End If
            If String.Equals(ext, ".hdm", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".xdf", StringComparison.OrdinalIgnoreCase) Then
                Return "pc98.2hd"
            End If
            If String.Equals(ext, ".2d", StringComparison.OrdinalIgnoreCase) Then
                Return "sharp.2d"
            End If
            If String.Equals(ext, ".do", StringComparison.OrdinalIgnoreCase) Then
                Return "apple2.appledos.140"
            End If
            If String.Equals(ext, ".po", StringComparison.OrdinalIgnoreCase) Then
                Return "apple2.prodos.140"
            End If
            If String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1571"
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1541"
            End If
            If String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd2000.dd"
            End If
            If String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd2000.hd"
            End If
            If String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.cmd.fd4000.ed"
            End If
            If String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase) Then
                Return "commodore.1581"
            End If
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ConfigureSectorImageDefaults)
        Private Shared Sub ConfigureSectorImageDefaults(image As Img, ext As String)
            If String.Equals(ext, ".ssd", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".fd", StringComparison.OrdinalIgnoreCase) Then
                image.Sequential = True
                Return
            End If
            If String.Equals(ext, ".d71", StringComparison.OrdinalIgnoreCase) Then
                image.Sequential = True
                Return
            End If
            If String.Equals(ext, ".d64", StringComparison.OrdinalIgnoreCase) Then
                image.MinCylinders = 35
                Return
            End If
            If String.Equals(ext, ".d81", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d1m", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d2m", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, ".d4m", StringComparison.OrdinalIgnoreCase) Then
                image.SidesSwapped = True
            End If
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration InferDimFormat)
        Private Shared Function InferDimFormat(path As String) As String
            Dim data = File.ReadAllBytes(path)
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 256, "DIM: Not a DIM file.")
            Dim sig = System.Text.Encoding.ASCII.GetString(data, &HAB, &HD)
            ErrorHandling.Check(String.Equals(sig, "DIFC HEADER  ", StringComparison.Ordinal), "DIM: Not a DIM file.")
            Dim mediaByte = CInt(data(0))
            If mediaByte = 0 Then
                Return "pc98.2hd"
            End If
            If mediaByte = 1 Then
                Return "pc98.2hs"
            End If
            Throw New FatalException("DIM: Unsupported format.")
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsEdskFile)
        Private Shared Function IsEdskFile(path As String) As Boolean
            If Not File.Exists(path) Then
                Return False
            End If
            Dim sig(33) As Byte
            Using fs = File.OpenRead(path)
                If fs.Read(sig, 0, sig.Length) <> sig.Length Then
                    Return False
                End If
            End Using
            Dim text = System.Text.Encoding.ASCII.GetString(sig)
            Return text.StartsWith("MV - CPC", StringComparison.Ordinal) OrElse
                   text.StartsWith("EXTENDED CPC DSK", StringComparison.Ordinal)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsApridiskFile)
        Private Shared Function IsApridiskFile(path As String) As Boolean
            If Not File.Exists(path) Then
                Return False
            End If
            Dim sig(23) As Byte
            Using fs = File.OpenRead(path)
                If fs.Read(sig, 0, sig.Length) <> sig.Length Then
                    Return False
                End If
            End Using
            Dim expected = System.Text.Encoding.ASCII.GetBytes("ACT Apricot disk image")
            For i = 0 To expected.Length - 1
                If sig(i) <> expected(i) Then
                    Return False
                End If
            Next
            Return sig(21) = &H1A AndAlso sig(22) = &H4
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveReadOnlyImageTypeName)
        Private Shared Function ResolveReadOnlyImageTypeName(ext As String) As String
            If String.Equals(ext, ".dim", StringComparison.OrdinalIgnoreCase) Then Return "DIM"
            If String.Equals(ext, ".dmk", StringComparison.OrdinalIgnoreCase) Then Return "DMK"
            If String.Equals(ext, ".fdi", StringComparison.OrdinalIgnoreCase) Then Return "FDI"
            If String.Equals(ext, ".nfd", StringComparison.OrdinalIgnoreCase) Then Return "NFD"
            If String.Equals(ext, ".td0", StringComparison.OrdinalIgnoreCase) Then Return "TD0"
            If String.Equals(ext, ".a2r", StringComparison.OrdinalIgnoreCase) Then Return "A2R"
            If String.Equals(ext, ".dcp", StringComparison.OrdinalIgnoreCase) Then Return "DCP"
            If String.Equals(ext, ".ctr", StringComparison.OrdinalIgnoreCase) Then Return "CTRaw"
            If String.Equals(ext, ".ipf", StringComparison.OrdinalIgnoreCase) Then Return "IPF"
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDiskDefinitionForConvert)
        Private Shared Function ResolveDiskDefinitionForConvert(formatName As String, diskDefsPath As String) As DiskDef
            Dim path = If(String.IsNullOrEmpty(diskDefsPath), FindDiskDefsPathForConvert(), diskDefsPath)
            Dim disk = DiskDefParser.GetDiskdef(formatName, path)
            ErrorHandling.Check(disk IsNot Nothing, String.Format("Unknown format '{0}'", formatName))
            Return disk
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindDiskDefsPathForConvert)
        Private Shared Function FindDiskDefsPathForConvert() As String
            Return "diskdefs.xml"
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/erase.py::main (direct command execution mapping).
    Public Class EraseAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("erase", "Erase a disk.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-header") Then
                Dim revs = Integer.Parse(parsed("revs"), CultureInfo.InvariantCulture)
                context.Output.WriteLine([Erase].BuildEraseHeader(parsed("tracks"), revs))
                Return 0
            End If
            If parsed.ContainsKey("parity-hfreq") Then
                Dim driveTicks = Double.Parse(parsed("drive-ticks"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(String.Format(CultureInfo.InvariantCulture, "erase_ticks={0:R}", [Erase].ComputeEraseTicks(driveTicks)))
                context.Output.WriteLine(String.Format("write_flux={0}", String.Join(",", [Erase].ComputeHighFrequencyFlux(driveTicks))))
                Return 0
            End If
            If parsed.ContainsKey("parity-resolve-tracks") Then
                Dim requested As String = Nothing
                parsed.TryGetValue("tracks", requested)
                Dim resolved = TrackResolution.ResolveDefaultTracks("c=0-81:h=0-1", requested)
                context.Output.WriteLine(String.Format("tracks={0}", resolved.ToString()))
                Return 0
            End If
            Dim preview = [Erase].BuildRuntimePreview(args)
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    context.Output.WriteLine([Erase].BuildEraseHeader(preview.Tracks, preview.Revs))
                    ToolOptions.WithDriveSelected(
                        Sub()
                            Dim safeTracks = preview.TrackSet.IteratePhysical().ToList()
                            [Erase].[Erase](usbClient, preview, safeTracks, context.Output)
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=True)
                Catch ex As CmdError
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            Else
                context.Output.WriteLine([Erase].BuildEraseHeader(preview.Tracks, preview.Revs))
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/clean.py::main (direct command execution mapping).
    Public Class CleanAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("clean", "Clean a drive in a zig-zag pattern using a cleaning disk.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-step") Then
                Dim cyls = Integer.Parse(parsed("cyls"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(String.Format("step={0}", Clean.ComputeStep(cyls)))
                Return 0
            End If
            If parsed.ContainsKey("parity-seek-target") Then
                Dim cyl = Integer.Parse(parsed("cylinder"), CultureInfo.InvariantCulture)
                Dim cyls = Integer.Parse(parsed("cyls"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(String.Format("seek_target={0}", Clean.ClampSeekCylinder(cyl, cyls)))
                Return 0
            End If
            If parsed.ContainsKey("parity-pattern") Then
                Dim cyls = Integer.Parse(parsed("cyls"), CultureInfo.InvariantCulture)
                Dim passes = Integer.Parse(parsed("passes"), CultureInfo.InvariantCulture)
                Dim sequences = Clean.BuildPassSequences(cyls, passes)
                For i = 0 To sequences.Count - 1
                    context.Output.WriteLine(String.Format("pass{0}={1}", i, String.Join(",", sequences(i))))
                Next
                Return 0
            End If
            Dim preview = Clean.BuildRuntimePreview(args)
            ' Python only prints "Pass p:" + cylinders during live clean. Skip pass-line preview unless dry-run mode.
            If Not preview.Live Then
                For Each line In preview.PassLines
                    context.Output.WriteLine(line)
                Next
            End If
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    ToolOptions.WithDriveSelected(
                        Sub()
                            Clean.Clean(usbClient, preview, context.Output)
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=True)
                Catch ex As CmdError
                    ' Python clean.py:57-58: `except USB.CmdError as error: print("Command Failed: %s" % error)`
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/seek.py::main (direct command execution mapping).
    Public Class SeekAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("seek", "Seek to the specified cylinder.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-extreme-check") Then
                Dim cyl = Integer.Parse(parsed("cylinder"), CultureInfo.InvariantCulture)
                Dim force = Integer.Parse(parsed("force"), CultureInfo.InvariantCulture) <> 0
                Dim prompt = Seek.ShouldPromptForExtremeCylinder(cyl, force)
                context.Output.WriteLine(String.Format("prompt={0}", If(prompt, 1, 0)))
                context.Output.WriteLine(Seek.GetExtremeCylinderPrompt(cyl))
                Return 0
            End If
            Dim preview = Seek.BuildRuntimePreview(args)
            If preview.PromptNeeded Then
                ' Python: input("...") and abort if answer != "Yes" (case sensitive).
                ' We only block on stdin when the input stream is genuinely
                ' interactive — running under the parity harness or with
                ' redirected stdin we just print the prompt and abort, which
                ' matches Python's behaviour when the user dismisses the
                ' prompt and lets the parity tests assert on the prompt text
                ' without deadlocking on stdin.
                context.Output.Write(preview.PromptText)
                Dim canPrompt As Boolean
                If context.Input IsNot Nothing AndAlso Not Object.ReferenceEquals(context.Input, Console.In) Then
                    ' Tool callers (parity harness, future GUI hosts) that
                    ' explicitly wire up an Input reader always get to answer.
                    canPrompt = True
                Else
                    ' Console.In: only read when stdin is an interactive TTY,
                    ' otherwise reading would block the parity harness which
                    ' captures stderr/stdout but doesn't supply stdin.
                    Try
                        canPrompt = Not Console.IsInputRedirected
                    Catch
                        canPrompt = False
                    End Try
                End If
                If canPrompt Then
                    Dim answer = context.Input.ReadLine()
                    If Not String.Equals(answer, "Yes", StringComparison.Ordinal) Then
                        Return 0
                    End If
                Else
                    Return 0
                End If
            End If
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    ToolOptions.WithDriveSelected(
                        Sub()
                            Seek.Seek(usbClient, preview.Cyl)
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=preview.MotorOn)
                Catch ex As CmdError
                    ' Python seek.py:52-53: `except USB.CmdError as err: print("Command Failed: %s" % err)`
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/delays.py::main (direct command execution mapping).
    Public Class DelaysAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("delays", "Display (and optionally modify) drive-delay parameters.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-format-line") Then
                Dim tab = Integer.Parse(parsed("tab"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(Delays.PrintInfoLine(parsed("name"), parsed("value"), tab))
                Return 0
            End If
            Dim preview = Delays.BuildRuntimePreview(args)
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)

                    Dim paramSize = 16
                    Dim dat As Byte() = Nothing
                    Do
                        Try
                            dat = usbClient.GetParams(UsbProtocol.Params.Delays, paramSize)
                            Exit Do
                        Catch ex As CmdError
                            If ex.Code = UsbProtocol.Ack.BadCommand AndAlso paramSize <> 10 Then
                                paramSize -= 2
                            Else
                                Throw
                            End If
                        End Try
                    Loop

                    Dim padded(15) As Byte
                    Array.Copy(dat, padded, Math.Min(dat.Length, padded.Length))
                    Dim values(7) As UShort
                    For i = 0 To 7
                        values(i) = BitConverter.ToUInt16(padded, i * 2)
                    Next

                    Dim optionToIndex As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
                        {"--select", 0},
                        {"--step", 1},
                        {"--settle", 2},
                        {"--motor", 3},
                        {"--watchdog", 4},
                        {"--pre-write", 5},
                        {"--post-write", 6},
                        {"--index-mask", 7}
                    }

                    For Each kvp In preview.Values
                        Dim key = kvp.Key
                        Dim idx = optionToIndex(key)
                        If String.Equals(key, "--pre-write", StringComparison.OrdinalIgnoreCase) AndAlso paramSize < 12 Then
                            Throw New FatalException("Option --pre-write requires updated firmware")
                        End If
                        If String.Equals(key, "--post-write", StringComparison.OrdinalIgnoreCase) AndAlso paramSize < 14 Then
                            Throw New FatalException("Option --post-write requires updated firmware")
                        End If
                        If String.Equals(key, "--index-mask", StringComparison.OrdinalIgnoreCase) AndAlso paramSize < 16 Then
                            Throw New FatalException("Option --index-mask requires updated firmware")
                        End If
                        values(idx) = CUShort(kvp.Value)
                    Next

                    If preview.Values.Count > 0 Then
                        Delays.Update(usbClient, paramSize, values)
                    End If

                    context.Output.WriteLine(Delays.PrintInfoLine("Select Delay", values(0).ToString(CultureInfo.InvariantCulture) & "us"))
                    context.Output.WriteLine(Delays.PrintInfoLine("Step Delay", values(1).ToString(CultureInfo.InvariantCulture) & "us"))
                    context.Output.WriteLine(Delays.PrintInfoLine("Settle Time", values(2).ToString(CultureInfo.InvariantCulture) & "ms"))
                    context.Output.WriteLine(Delays.PrintInfoLine("Motor Delay", values(3).ToString(CultureInfo.InvariantCulture) & "ms"))
                    context.Output.WriteLine(Delays.PrintInfoLine("Watchdog", values(4).ToString(CultureInfo.InvariantCulture) & "ms"))
                    If paramSize >= 12 Then
                        context.Output.WriteLine(Delays.PrintInfoLine("Pre-Write", values(5).ToString(CultureInfo.InvariantCulture) & "us"))
                    End If
                    If paramSize >= 14 Then
                        context.Output.WriteLine(Delays.PrintInfoLine("Post-Write", values(6).ToString(CultureInfo.InvariantCulture) & "us"))
                    End If
                    If paramSize >= 16 Then
                        context.Output.WriteLine(Delays.PrintInfoLine("Index Mask", values(7).ToString(CultureInfo.InvariantCulture) & "us"))
                    End If
                Catch ex As CmdError
                    ' Python delays.py:147-148: `except USB.CmdError as err: print("Command Failed: %s" % err)`
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/update.py::main (direct command execution mapping).
    Public Class UpdateAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("update", "Update the Greaseweazle device firmware to latest (or specified) version.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-validate-args") Then
                Dim fileValue As String = Nothing
                Dim tagValue As String = Nothing
                parsed.TryGetValue("file", fileValue)
                parsed.TryGetValue("tag", tagValue)
                Try
                    Update.ValidateTagFileExclusion(fileValue, tagValue)
                    context.Output.WriteLine("ok=1")
                Catch ex As FatalException
                    context.Output.WriteLine("ok=0")
                    context.Output.WriteLine(String.Format("error={0}", ex.Message))
                End Try
                Return 0
            End If

            Dim preview = Update.BuildRuntimePreview(args)
            If Not preview.Live Then
                Return 0
            End If

            ' Python update.py:103 emits "Downloading latest firmware: ..." BEFORE
            ' issuing the asset GET; pass our output writer so Update.Download can
            ' do likewise instead of waiting for the call to return.
            Dim updateFile = Update.ResolvePayload(preview, context.Output)
            Dim usbClient As Unit = Nothing
            Try
                usbClient = ToolOptions.UsbOpen(preview.Device, modeCheck:=False)
                ' UsbOpen already populated firmware capabilities via
                ' ReadFirmwareInfo + ApplyFirmwareInfo; ExtractUpdate only
                ' reads HwModel so synthesize a minimal info object rather
                ' than issuing a second Get-Info round-trip.
                Dim info = New FirmwareInfo() With {.HwModel = usbClient.HwModel}
                Dim extracted = Update.ExtractUpdate(info, updateFile, preview.Bootloader)

                context.Output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                                       "Updating {0} to version {1}.{2}...",
                                                       If(preview.Bootloader, "Bootloader", "Main Firmware"),
                                                       extracted.Major,
                                                       extracted.Minor))

                If Not preview.Force AndAlso (usbClient.CanModeSwitch OrElse preview.Bootloader = usbClient.UpdateMode) Then
                    If preview.Bootloader <> usbClient.UpdateMode Then
                        usbClient = ToolOptions.UsbReopen(usbClient, isUpdate:=preview.Bootloader)
                        ErrorHandling.Check(preview.Bootloader = usbClient.UpdateMode, "Device did not mode switch as requested")
                    End If

                    If usbClient.Major > extracted.Major OrElse
                       (usbClient.Major = extracted.Major AndAlso usbClient.Minor >= extracted.Minor) Then
                        If usbClient.UpdateMode AndAlso usbClient.CanModeSwitch Then
                            usbClient = ToolOptions.UsbReopen(usbClient, isUpdate:=False)
                        End If
                        ' Python update.py:173-176 builds this message via a
                        ' triple-quoted string so the embedded line break is a
                        ' bare `\n` (matched by textwrap.dedent at print time).
                        ' Use vbLf instead of Environment.NewLine so on Windows
                        ' we don't emit \r\n where Python emits \n.
                        Throw New SkipUpdate(String.Format(CultureInfo.InvariantCulture,
                                                           "Device is already running version {0}.{1}.{2}Use --force to update anyway.",
                                                           usbClient.Major,
                                                           usbClient.Minor,
                                                           vbLf))
                    End If
                End If

                usbClient = ToolOptions.UsbModeCheck(usbClient, isUpdate:=Not preview.Bootloader)
                Dim ack = Update.UpdateFirmware(usbClient, extracted.Payload, preview.Bootloader)
                If preview.Bootloader Then
                    If ack <> 0 Then
                        context.Output.WriteLine("** UPDATE FAILED: Please retry immediately or your Weazle may need")
                        context.Output.WriteLine("        full reflashing via a suitable programming adapter!")
                    Else
                        context.Output.WriteLine("Done.")
                    End If
                Else
                    If ack <> 0 Then
                        context.Output.WriteLine("** UPDATE FAILED: Please retry!")
                    Else
                        context.Output.WriteLine("Done.")
                        If Not usbClient.JumperlessUpdate Then
                            context.Output.WriteLine("** Unplug device and remove the Update Jumper")
                        End If
                    End If
                End If
                If usbClient.UpdateMode AndAlso usbClient.CanModeSwitch Then
                    usbClient = ToolOptions.UsbReopen(usbClient, isUpdate:=False)
                End If
                Return 0
            Catch ex As CmdError
                If ex.Code = UsbProtocol.Ack.OutOfSRAM AndAlso preview.Bootloader Then
                    context.Output.WriteLine("ERROR: Bootloader update unsupported on this device (insufficient SRAM)")
                ElseIf ex.Code = UsbProtocol.Ack.OutOfFlash AndAlso Not preview.Bootloader Then
                    context.Output.WriteLine("ERROR: New firmware is too large for this device (insufficient Flash memory)")
                Else
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                End If
                Return 0
            Catch ex As SkipUpdate
                context.Output.WriteLine("** SKIPPING UPDATE:")
                context.Output.WriteLine(ex.Message)
                Return 0
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/pin.py::main (direct command execution mapping).
    Public Class PinAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("pin", "Change the setting of a user-modifiable interface pin.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-usage") Then
                For Each line In Pin.Usage()
                    context.Output.WriteLine(line)
                Next
                Return 0
            End If
            If parsed.ContainsKey("parity-format-level") Then
                Dim pinNumber = Integer.Parse(parsed("pin"), CultureInfo.InvariantCulture)
                Dim level = Integer.Parse(parsed("level"), CultureInfo.InvariantCulture) <> 0
                context.Output.WriteLine(Pin.FormatPinLevelMessage(pinNumber, level))
                Return 0
            End If
            If parsed.ContainsKey("parity-dispatch") Then
                Dim parts = parsed("argv").Split("|"c)
                context.Output.WriteLine(Pin.DispatchPinSubcommand(parts))
                Return 0
            End If
            Dim preview = Pin.BuildRuntimePreview(args)
            If String.Equals(preview.Mode, "usage", StringComparison.Ordinal) Then
                For Each line In Pin.Usage()
                    context.Output.WriteLine(line)
                Next
                ' Python pin.py:62-65 calls sys.exit(1) from usage(); mirror that exit code.
                Return 1
            ElseIf String.Equals(preview.Mode, "set", StringComparison.Ordinal) Then
                If preview.Live Then
                    Dim usbClient As Unit = Nothing
                    Try
                        usbClient = ToolOptions.UsbOpen(preview.Device)
                        usbClient.SetPin(preview.Pin, preview.Level)
                        ' Python pin.py:30-31: print only AFTER set_pin succeeds.
                        ' On CmdError the success message must be replaced by
                        ' the "Command Failed" line below.
                        context.Output.WriteLine(preview.Message)
                    Catch ex As CmdError
                        ' Python pin.py:32-33: `except USB.CmdError as error: print("Command Failed: %s" % error)`
                        context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                    Finally
                        If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                            Try : usbClient.Serial.Close() : Catch : End Try
                        End If
                    End Try
                Else
                    ' Dry-run (--test): print the would-be success message
                    ' without contacting hardware so parity fixtures can
                    ' assert on it.
                    context.Output.WriteLine(preview.Message)
                End If
            ElseIf String.Equals(preview.Mode, "get", StringComparison.Ordinal) Then
                If preview.Live Then
                    Dim usbClient As Unit = Nothing
                    Try
                        usbClient = ToolOptions.UsbOpen(preview.Device)
                        ToolOptions.WithDriveSelected(
                            Sub()
                                context.Output.WriteLine(Pin.PinGet(usbClient, preview.Pin))
                            End Sub,
                            New UsbDriveControlAdapter(usbClient),
                            preview.Drive,
                            motor:=False)
                    Catch ex As CmdError
                        ' Python pin.py:59-60: `except USB.CmdError as error: print("Command Failed: %s" % error)`
                        context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                    Finally
                        If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                            Try : usbClient.Serial.Close() : Catch : End Try
                        End If
                    End Try
                End If
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/reset.py::main (direct command execution mapping).
    Public Class ResetAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("reset", "Reset the Greaseweazle device to power-on default state.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-delays-flag") Then
                Dim includeDelays = Integer.Parse(parsed("delays"), CultureInfo.InvariantCulture) <> 0
                Dim restore = Reset.ShouldRestoreDelays(includeDelays)
                context.Output.WriteLine(String.Format("restore_delays={0}", If(restore, 1, 0)))
                Return 0
            End If
            Dim preview = Reset.BuildRuntimePreview(args)
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    ' Python: capture current delay RAM via Delays(usb) before power-on reset.
                    Dim paramSize = 16
                    Dim dat As Byte() = Nothing
                    Do
                        Try
                            dat = usbClient.GetParams(UsbProtocol.Params.Delays, paramSize)
                            Exit Do
                        Catch ex As CmdError
                            If ex.Code = UsbProtocol.Ack.BadCommand AndAlso paramSize <> 10 Then
                                paramSize -= 2
                            Else
                                Throw
                            End If
                        End Try
                    Loop
                    Dim padded(15) As Byte
                    Array.Copy(dat, padded, Math.Min(dat.Length, padded.Length))
                    Dim values(7) As UShort
                    For i = 0 To 7
                        values(i) = BitConverter.ToUInt16(padded, i * 2)
                    Next
                    usbClient.PowerOnReset()
                    If Reset.ShouldRestoreDelays(preview.DelaysFlag) Then
                        Delays.Update(usbClient, paramSize, values)
                    End If
                Catch ex As CmdError
                    ' Python reset.py:34-35: `except USB.CmdError as error: print("Command Failed: %s" % error)`
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/bandwidth.py::main (direct command execution mapping).
    Public Class BandwidthAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("bandwidth", "Report the available USB bandwidth for the Greaseweazle device.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-buffer") Then
                Dim count = Integer.Parse(parsed("count"), CultureInfo.InvariantCulture)
                Dim seed = UInteger.Parse(parsed("seed"), NumberStyles.Integer, CultureInfo.InvariantCulture)
                Dim bytes = Bandwidth.GenerateRandomBuffer(count, seed)
                context.Output.WriteLine(String.Format("buffer={0}", String.Join(",", bytes.Select(Function(b) CInt(b)))))
                Return 0
            End If
            If parsed.ContainsKey("parity-estimate") Then
                Dim minRead = Double.Parse(parsed("min-read"), CultureInfo.InvariantCulture)
                Dim minWrite = Double.Parse(parsed("min-write"), CultureInfo.InvariantCulture)
                Dim estimated = Bandwidth.EstimateConsistentMinimumBandwidth(minRead, minWrite)
                context.Output.WriteLine(String.Format(CultureInfo.InvariantCulture, "estimated={0:R}", estimated))
                context.Output.WriteLine(Bandwidth.BuildBandwidthStatus(estimated))
                Return 0
            End If
            If parsed.ContainsKey("parity-required-min") Then
                context.Output.WriteLine(String.Format(CultureInfo.InvariantCulture, "required={0:R}", Bandwidth.ComputeRequiredMinimumBandwidth()))
                Return 0
            End If
            Dim preview = Bandwidth.BuildRuntimePreview(args)
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    Bandwidth.MeasureBandwidth(usbClient, context.Output)
                Catch ex As CmdError
                    ' Python bandwidth.py:86-87: `except USB.CmdError as error: print("Command Failed: %s" % error)`
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/rpm.py::main (direct command execution mapping).
    Public Class RpmAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("rpm", "Measure RPM of drive spindle.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-speed-line") Then
                Dim tpr = Double.Parse(parsed("tpr"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(Rpm.SpeedString(tpr))
                Return 0
            End If
            If parsed.ContainsKey("parity-summary") Then
                Dim samples = parsed("samples").Split(","c).Select(Function(x) Double.Parse(x, CultureInfo.InvariantCulture)).ToList()
                For Each line In Rpm.PrintRpm(samples)
                    context.Output.WriteLine(line)
                Next
                Return 0
            End If
            Dim preview = Rpm.BuildRuntimePreview(args)
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Dim samples As New List(Of Double)()
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    Dim sampleFreq = usbClient.SampleFreq
                    ToolOptions.WithDriveSelected(
                        Sub()
                            ' Python rpm.py:24-38: print_rpm uses try/finally so the
                            ' fastest/mean/median/slowest summary is emitted even
                            ' when read_track throws partway through, after which
                            ' the exception still propagates up to with_drive_selected
                            ' (motor-off + deselect) and then to main's CmdError
                            ' handler. Match that ordering exactly.
                            Try
                                For i = 1 To preview.Nr
                                    Dim flux = usbClient.ReadTrack(1, 0)
                                    Dim tpr = flux.IndexList.Last() / sampleFreq
                                    samples.Add(tpr)
                                    context.Output.WriteLine(Rpm.SpeedString(tpr))
                                Next
                            Finally
                                If samples.Count > 1 Then
                                    context.Output.WriteLine("***")
                                    For Each line In Rpm.PrintRpm(samples)
                                        context.Output.WriteLine(line)
                                    Next
                                End If
                            End Try
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=True)
                Catch ex As CmdError
                    ' Python rpm.py:58-59: `except USB.CmdError as err: print("Command Failed: %s" % err)`
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/align.py::main (direct command execution mapping).
    Public Class AlignAction
        Inherits StubActionBase
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New()
            MyBase.New("align", "Repeatedly read the same track for floppy drive alignment.")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration Execute)
        Public Overrides Function Execute(args As IReadOnlyList(Of String), context As ToolContext) As Integer
            Dim parsed = ActionArgs.Parse(args)
            If parsed.ContainsKey("parity-tspec") Then
                Dim cyl = Integer.Parse(parsed("cyl"), CultureInfo.InvariantCulture)
                Dim head = Integer.Parse(parsed("head"), CultureInfo.InvariantCulture)
                Dim physicalCyl = Integer.Parse(parsed("physical-cyl"), CultureInfo.InvariantCulture)
                Dim physicalHead = Integer.Parse(parsed("physical-head"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(Align.BuildTrackSpec(cyl, head, physicalCyl, physicalHead))
                Return 0
            End If
            If parsed.ContainsKey("parity-single-header") Then
                Dim reads = Integer.Parse(parsed("reads"), CultureInfo.InvariantCulture)
                Dim revs = Integer.Parse(parsed("revs"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(Align.BuildSingleTrackHeader(parsed("tspec"), reads, revs))
                Return 0
            End If
            If parsed.ContainsKey("parity-multi-header") Then
                Dim cyl = Integer.Parse(parsed("cyl"), CultureInfo.InvariantCulture)
                Dim reads = Integer.Parse(parsed("reads"), CultureInfo.InvariantCulture)
                Dim revs = Integer.Parse(parsed("revs"), CultureInfo.InvariantCulture)
                Dim heads = parsed("heads").Split(","c).Select(Function(x) Integer.Parse(x, CultureInfo.InvariantCulture)).ToList()
                context.Output.WriteLine(Align.BuildMultiTrackHeader(cyl, heads, reads, revs))
                Return 0
            End If
            If parsed.ContainsKey("parity-validate") Then
                Try
                    Dim pairs As New List(Of Tuple(Of Integer, Integer))()
                    Dim tracksText As String = Nothing
                    parsed.TryGetValue("tracks", tracksText)
                    If Not String.IsNullOrEmpty(tracksText) Then
                        For Each token In tracksText.Split("|"c)
                            Dim parts = token.Split(","c)
                            pairs.Add(Tuple.Create(
                                Integer.Parse(parts(0), CultureInfo.InvariantCulture),
                                Integer.Parse(parts(1), CultureInfo.InvariantCulture)))
                        Next
                    End If
                    Align.ValidateTrackCylinders(pairs)
                    context.Output.WriteLine("ok=1")
                Catch ex As FatalException
                    context.Output.WriteLine("ok=0")
                    context.Output.WriteLine(String.Format("error={0}", ex.Message))
                End Try
                Return 0
            End If
            If parsed.ContainsKey("parity-resolve-tracks") Then
                Dim formatTracksSpec As String = Nothing
                Dim requested As String = Nothing
                parsed.TryGetValue("format-tracks", formatTracksSpec)
                parsed.TryGetValue("tracks", requested)
                Dim formatTracks = If(String.IsNullOrEmpty(formatTracksSpec), Nothing, New Greaseweazle.Shared.TrackSet(formatTracksSpec))
                Dim resolved = TrackResolution.ResolveDefaultTracksFromFormat(formatTracks, requested)
                context.Output.WriteLine(String.Format("tracks={0}", resolved.ToString()))
                Return 0
            End If
            If parsed.ContainsKey("parity-alternation") Then
                Dim readNumber = Integer.Parse(parsed("read-num"), CultureInfo.InvariantCulture)
                Dim trackCount = Integer.Parse(parsed("track-count"), CultureInfo.InvariantCulture)
                context.Output.WriteLine(String.Format("index={0}", Align.ResolveAlternatingTrackIndex(readNumber, trackCount)))
                Return 0
            End If
            If parsed.ContainsKey("parity-hard-sectors") Then
                Dim hardSectors = Integer.Parse(parsed("hard-sectors"), CultureInfo.InvariantCulture)
                Dim revs = Integer.Parse(parsed("revs"), CultureInfo.InvariantCulture)
                Dim result = Align.ResolveHardSectorReadParams(hardSectors, revs)
                context.Output.WriteLine(String.Format("effective_revs={0}", result.Item1))
                context.Output.WriteLine(String.Format("effective_ticks={0}", result.Item2))
                Return 0
            End If
            Dim preview = Align.BuildRuntimePreview(args, CodecRegistry.GetFormats())
            ' Python align.py:100-107 prints "Aligning ..." (and the optional
            ' "Format ..." line) AFTER align_track has applied the fractional-
            ' revs collapse and hard-sectors multiplier — so the visible revs
            ' reflects the *effective* read count, not the parse-time value.
            ' In live mode we therefore defer those prints into AlignTrack itself
            ' so the post-adjustment values are used. In dry-run / --test mode we
            ' have no hardware to drive those adjustments, so we still print the
            ' parse-time preview header here (matches the earlier --test
            ' behaviour parity fixtures depend on).
            If Not preview.Live Then
                context.Output.WriteLine(preview.Header)
                If Not String.IsNullOrEmpty(preview.Format) Then
                    context.Output.WriteLine("Format " & preview.Format)
                End If
            End If
            If preview.Live Then
                Dim usbClient As Unit = Nothing
                Dim prevPin2 As Nullable(Of Boolean) = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    If preview.Densel.HasValue OrElse preview.GenTg43 Then
                        prevPin2 = usbClient.GetPin(2)
                    End If
                    If preview.Densel.HasValue Then
                        usbClient.SetPin(2, preview.Densel.Value)
                    End If

                    ToolOptions.WithDriveSelected(
                        Sub()
                            Dim effectiveRevs = preview.Revs
                            Dim effectiveTicks = preview.Ticks
                            Dim driveTicksPerRev As Nullable(Of Double) = Nothing
                            Dim hardSectorCount As Integer = 0
                            If preview.FakeIndexPeriod.HasValue Then
                                driveTicksPerRev = preview.FakeIndexPeriod.Value * usbClient.SampleFreq
                            ElseIf preview.HardSectors Then
                                ' Python align.py:50 uses `int(usb.sample_freq/2)`; truncate
                                ' toward zero rather than banker's-round.
                                Dim flux = usbClient.ReadTrack(0, CInt(Math.Truncate(usbClient.SampleFreq / 2)))
                                flux.IdentifyHardSectors()
                                ErrorHandling.Check(flux.SectorList IsNot Nothing AndAlso flux.SectorList.Count > 0,
                                                   "Unable to identify hard sectors on this drive")
                                driveTicksPerRev = flux.TicksPerRev
                                hardSectorCount = flux.SectorList(flux.SectorList.Count - 1).Count
                                context.Output.WriteLine(String.Format("Drive reports {0} hard sectors", hardSectorCount))
                            End If

                            ' Python align.py:64-71: collapse fractional revs to a
                            ' tick budget after measuring drive ticks-per-rev.
                            If preview.FractionalRevs.HasValue Then
                                If preview.Raw Then
                                    effectiveRevs = 2
                                Else
                                    If Not driveTicksPerRev.HasValue Then
                                        driveTicksPerRev = usbClient.ReadTrack(2, 0).TicksPerRev
                                    End If
                                    ' Python's int() truncates toward zero; use Math.Truncate.
                                    effectiveTicks = CInt(Math.Truncate(driveTicksPerRev.Value * preview.FractionalRevs.Value))
                                    effectiveRevs = 2
                                End If
                            End If

                            ' Python align.py:73-75: hard-sector revs/ticks
                            ' adjustment uses the post-fractional-collapse revs.
                            If preview.HardSectors AndAlso hardSectorCount > 0 Then
                                effectiveRevs = (hardSectorCount + 1) * (effectiveRevs + 1)
                                effectiveTicks = 0
                            End If

                            If preview.GenTg43 Then
                                Dim firstTrack = preview.TrackSet.IteratePhysical().First()
                                usbClient.SetPin(2, firstTrack.Cyl < 60)
                            End If

                            Align.AlignTrack(usbClient,
                                             preview.TrackSet.IteratePhysical().ToList(),
                                             preview.Reads,
                                             effectiveRevs,
                                             effectiveTicks,
                                             context.Output,
                                             preview.Reverse,
                                             preview.HardSectors,
                                             preview.Raw,
                                             preview.AdjustSpeed,
                                             driveTicksPerRev,
                                             preview.FakeIndexPeriod,
                                             preview.FormatDef,
                                             preview.Format,
                                             preview.PllProfiles,
                                             preview.Format)
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=True)
                Catch ex As CmdError
                    ' Python align.py:219-220: `except USB.CmdError as err: print("Command Failed: %s" % err)`
                    context.Output.WriteLine(String.Format("Command Failed: {0}", ex.Message))
                Finally
                    If usbClient IsNot Nothing AndAlso (preview.Densel.HasValue OrElse preview.GenTg43) AndAlso prevPin2.HasValue Then
                        ' Tolerate SetPin errors so an in-flight Ctrl-C path
                        ' (which may have torn down the serial port) doesn't
                        ' mask the originating KeyboardInterruptException.
                        Try : usbClient.SetPin(2, prevPin2.Value) : Catch : End Try
                    End If
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        ' Tolerate close errors so an in-flight Ctrl-C path
                        ' (which may have already torn down the serial port)
                        ' doesn't mask the originating KeyboardInterruptException.
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            Return 0
        End Function
    End Class

End Namespace
