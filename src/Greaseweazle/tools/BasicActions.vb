Imports System.Globalization
Imports System.IO
Imports System.Threading
Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Images
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Shared

Namespace Greaseweazle.Tools

    ' Python map: src/greaseweazle/tools/info.py::main (direct command execution mapping).
    Public NotInheritable Class InfoAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/info.py::main (post-parser algorithm body).
        ' Returns a typed DeviceInfoResult. Port-open failures surface as
        ' ConnectionState=NotFound (not as exceptions) so callers can
        ' distinguish "device absent" from "transport error". Other USB
        ' failures throw CmdError up.
        Public Shared Function RunFromOptions(preview As InfoOptions) As Greaseweazle.Actions.DeviceInfoResult
            Dim hostVersion As String = Nothing
            Dim infoAttr = TryCast(Reflection.CustomAttributeExtensions.GetCustomAttribute(Of Reflection.AssemblyInformationalVersionAttribute)(Reflection.Assembly.GetExecutingAssembly()), Reflection.AssemblyInformationalVersionAttribute)
            If infoAttr IsNot Nothing AndAlso Not String.IsNullOrEmpty(infoAttr.InformationalVersion) Then
                hostVersion = infoAttr.InformationalVersion
            Else
                hostVersion = Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()
            End If

            If Not preview.Live Then
                Return New Greaseweazle.Actions.DeviceInfoResult(
                    hostVersion, Greaseweazle.Actions.DeviceConnectionState.TestMode, Nothing)
            End If

            Dim usb As Unit = Nothing
            Try
                Try
                    usb = ToolOptions.UsbOpen(preview.Device, modeCheck:=False)
                Catch ex As IO.IOException
                    ' Python catches `serial.SerialException` which covers
                    ' port-not-found and port-in-use both; .NET splits these
                    ' into IOException and UnauthorizedAccessException, so
                    ' accept either to preserve "Not found" parity.
                    Return New Greaseweazle.Actions.DeviceInfoResult(
                        hostVersion, Greaseweazle.Actions.DeviceConnectionState.NotFound, Nothing)
                Catch ex As UnauthorizedAccessException
                    Return New Greaseweazle.Actions.DeviceInfoResult(
                        hostVersion, Greaseweazle.Actions.DeviceConnectionState.NotFound, Nothing)
                Catch ex As Exception When TypeOf ex Is FatalException
                    Return New Greaseweazle.Actions.DeviceInfoResult(
                        hostVersion, Greaseweazle.Actions.DeviceConnectionState.NotFound, Nothing)
                End Try

                Dim fw = usb.ReadFirmwareInfo()
                Dim modeSwitched = usb.CanModeSwitch AndAlso usb.UpdateMode <> preview.Bootloader
                If modeSwitched Then
                    usb = ToolOptions.UsbReopen(usb, isUpdate:=preview.Bootloader)
                End If

                Dim updateMode = usb.UpdateMode
                Dim version = Tuple.Create(CInt(usb.Major), CInt(usb.Minor))
                Dim port = If(usb.PortDevice, String.Empty)
                Dim hwModel = usb.HwModel
                Dim hwSubmodel = usb.HwSubmodel
                Dim mcuId = usb.McuId
                Dim mcuMhz = usb.McuMhz
                Dim mcuSramKb = usb.McuSramKb
                Dim firmwareMajor = fw.Major
                Dim firmwareMinor = fw.Minor
                Dim isBootloader = usb.UpdateMode
                Dim serialNumber = If(usb.PortSerialNumber, String.Empty)
                Dim usbSpeedRaw = usb.UsbSpeed
                Dim usbBufferKb = usb.UsbBufferKb
                Dim jumperlessUpdate = usb.JumperlessUpdate

                If modeSwitched Then
                    usb = ToolOptions.UsbReopen(usb, isUpdate:=Not preview.Bootloader)
                End If

                Dim firmwareUpdate As Greaseweazle.Actions.FirmwareUpdateInfo = Nothing
                If Not updateMode Then
                    Try
                        Dim latest = Info.LatestFirmware()
                        If latest.Item1 > version.Item1 OrElse
                           (latest.Item1 = version.Item1 AndAlso latest.Item2 > version.Item2) Then
                            firmwareUpdate = New Greaseweazle.Actions.FirmwareUpdateInfo(
                                latest.Item1, latest.Item2)
                        End If
                    Catch
                        ' Python silently swallows network failures here.
                    End Try
                End If

                Dim block As New Greaseweazle.Actions.DeviceInfoBlock(
                    port, hwModel, hwSubmodel, mcuId, mcuMhz, mcuSramKb,
                    firmwareMajor, firmwareMinor, isBootloader, serialNumber,
                    usbSpeedRaw, usbBufferKb, jumperlessUpdate, firmwareUpdate)

                Return New Greaseweazle.Actions.DeviceInfoResult(
                    hostVersion, Greaseweazle.Actions.DeviceConnectionState.Connected, block)
            Finally
                If usb IsNot Nothing AndAlso usb.Serial IsNot Nothing Then
                    Try : usb.Serial.Close() : Catch : End Try
                End If
            End Try
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/read.py::main (direct command execution mapping).
    Public NotInheritable Class ReadAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/read.py::main (post-parser algorithm body).
        '
        ' Pure-logic body — no console output. Streams progress via
        ' ReadCommand events and returns a typed ReadSummary.
        Public Shared Function RunFromOptions(preview As ReadOptions,
                                              cmd As Greaseweazle.Actions.ReadCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.ReadSummary
            Dim revsDisplay = If(preview.RevsDisplay, preview.Revs.ToString(Globalization.CultureInfo.InvariantCulture))
            If cmd IsNot Nothing Then
                cmd.OnStarted(New Greaseweazle.Actions.ReadStartedEventArgs(preview.Tracks, revsDisplay, preview.Format))
            End If
            If Not preview.Live Then
                Return New Greaseweazle.Actions.ReadSummary(preview.Tracks, revsDisplay, 0,
                                                            preview.Format, Nothing,
                                                            Nothing, dryRun:=True)
            End If
            Return RunLive(preview, cmd, ct, revsDisplay)
        End Function

        ' Live-mode body for Read. Pure logic — all output flows
        ' through ReadCommand events.
        Private Shared Function RunLive(preview As ReadOptions,
                                        cmd As Greaseweazle.Actions.ReadCommand,
                                        ct As CancellationToken,
                                        revsDisplay As String) As Greaseweazle.Actions.ReadSummary
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

            Dim summaryDict As New Dictionary(Of Tuple(Of Integer, Integer), Codec)()
            Dim usbClient As Unit = Nothing
            Dim prevPin2 As Nullable(Of Boolean) = Nothing
            Dim tracksProcessed = 0
            Dim trackProcessedCallback As Action(Of Greaseweazle.Actions.ReadTrackProcessedEventArgs) = Nothing
            Dim trackGaveUpCallback As Action(Of Greaseweazle.Actions.ReadTrackGaveUpEventArgs) = Nothing
            If cmd IsNot Nothing Then
                trackProcessedCallback = Sub(args)
                                             ct.ThrowIfCancellationRequested()
                                             cmd.OnTrackProcessed(args)
                                         End Sub
                trackGaveUpCallback = Sub(args)
                                          ct.ThrowIfCancellationRequested()
                                          cmd.OnTrackGaveUp(args)
                                      End Sub
            End If

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
                            If cmd IsNot Nothing Then
                                cmd.OnHardSectorsDetected(New Greaseweazle.Actions.ReadHardSectorsEventArgs(hardSectorCount))
                            End If
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
                            ct.ThrowIfCancellationRequested()
                            ' Python read.py:197 always passes args.fmt_cls into read_with_retry,
                            ' so --format triggers decode/verification regardless of --raw.
                            Dim readResult = ReadWrite.ReadWithRetry(usbClient,
                                                                     track,
                                                                     effectiveRevs,
                                                                     trackProcessedCallback,
                                                                     trackGaveUpCallback,
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
                            tracksProcessed += 1
                            ' Python read.py:198-200: collect codec results for end-of-run summary.
                            If imgDisk IsNot Nothing AndAlso TypeOf dat Is Codec Then
                                summaryDict(Tuple.Create(track.Cyl, track.Head)) = CType(dat, Codec)
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
                    End Sub,
                    New UsbDriveControlAdapter(usbClient),
                    preview.Drive,
                    motor:=True)
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

            ' Python read.py:206-207: print_summary when --format was supplied.
            Dim grid As Greaseweazle.Actions.SectorSummaryGrid = Nothing
            If imgDisk IsNot Nothing Then
                grid = ReadWrite.BuildSectorSummary(preview.TrackSet, summaryDict)
                If cmd IsNot Nothing Then
                    cmd.OnSummaryReady(New Greaseweazle.Actions.ReadSummaryReadyEventArgs(grid))
                End If
            End If

            ' Python read.py writes the image silently inside its
            ' `with open_image(...)` context manager — no "Wrote ..."
            ' line. Mirror that: just emit the bytes (KryoFlux is a
            ' directory template, so it has no single-file body to
            ' write here).
            If writeScp Then
                File.WriteAllBytes(outPath, scpImage.GetImage())
            ElseIf writeImd Then
                File.WriteAllBytes(outPath, imdImage.GetImage())
            ElseIf writeHfe Then
                File.WriteAllBytes(outPath, hfeImage.GetImage())
            ElseIf writeSector Then
                File.WriteAllBytes(outPath, imgImage.GetImage())
            ElseIf writeNsi Then
                File.WriteAllBytes(outPath, nsiImage.GetImage())
            ElseIf writeD88 Then
                File.WriteAllBytes(outPath, d88Image.GetImage())
            End If

            Return New Greaseweazle.Actions.ReadSummary(preview.Tracks, revsDisplay,
                                                        tracksProcessed, preview.Format,
                                                        outPath, grid, dryRun:=False)
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
    Public NotInheritable Class WriteAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/write.py::main (post-parser algorithm body).
        '
        ' Pure-logic body — no console output. Streams progress via
        ' WriteCommand events and returns a typed WriteSummary.
        Public Shared Function RunFromOptions(preview As WriteOptions,
                                              cmd As Greaseweazle.Actions.WriteCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.WriteSummary
            If cmd IsNot Nothing Then
                cmd.OnStarted(New Greaseweazle.Actions.WriteStartedEventArgs(preview.Format, preview.Tracks, preview.Precomp))
            End If
            If Not preview.Live Then
                ' --test parity (VB-only): the dry-run path emits only
                ' the header lines via OnStarted; no per-track work runs
                ' and the verify-summary footer is intentionally
                ' suppressed (Python has no --test mode but the existing
                ' fixtures expect just the header echo).
                Return New Greaseweazle.Actions.WriteSummary(preview.Tracks,
                                                              preview.Format,
                                                              Greaseweazle.Actions.WriteVerifyOutcome.AllVerified,
                                                              0,
                                                              0,
                                                              dryRun:=True)
            End If
            Return RunWriteLive(preview, cmd, ct)
        End Function

        ' Live-mode body for Write. Pure logic — all output flows through
        ' WriteCommand events. Returns the final WriteSummary.
        Private Shared Function RunWriteLive(preview As WriteOptions,
                                             cmd As Greaseweazle.Actions.WriteCommand,
                                             ct As CancellationToken) As Greaseweazle.Actions.WriteSummary
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
                Dim runVerifiedCount As Integer = 0
                Dim runNotVerifiedCount As Integer = 0
                Dim runOutcome As Greaseweazle.Actions.WriteVerifyOutcome = Greaseweazle.Actions.WriteVerifyOutcome.AllVerified
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
                                If cmd IsNot Nothing Then
                                    cmd.OnHardSectorsDetected(New Greaseweazle.Actions.WriteHardSectorsEventArgs(hardSectorCount))
                                End If
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
                            ' Verify-tally counters live in the enclosing
                            ' scope (runVerifiedCount/runNotVerifiedCount) so
                            ' RunWriteLive can construct the final
                            ' WriteSummary after the with-drive-selected
                            ' lambda returns. The lambda mutates them via
                            ' closure capture.
                            Dim safeTracks = preview.TrackSet.IteratePhysical().ToList()
                            For Each track In safeTracks
                                ct.ThrowIfCancellationRequested()
                                If preview.GenTg43 Then
                                    usbClient.SetPin(2, track.Cyl < 43)
                                End If
                                Dim trackInfo = New Greaseweazle.Actions.WriteTrackInfo(track.Cyl, track.Head, track.PhysicalCyl, track.PhysicalHead)
                                Dim PrepareSourceTrack As Func(Of HasFlux, HasFlux) =
                                    Function(source As HasFlux) As HasFlux
                                        Dim prepared = source
                                        If formatDef IsNot Nothing AndAlso Not TypeOf prepared Is Codec Then
                                            Dim decoded = formatDef.DecodeFlux(track.Cyl, track.Head, prepared)
                                            If decoded Is Nothing Then
                                                If cmd IsNot Nothing Then
                                                    cmd.OnTrackOutOfRange(New Greaseweazle.Actions.WriteTrackOutOfRangeEventArgs(trackInfo, preview.Format))
                                                End If
                                                Return Nothing
                                            End If
                                            ErrorHandling.Check(decoded.NrMissing() = 0,
                                                               String.Format("T{0}.{1}: {2} missing sectors in input image",
                                                                             track.Cyl, track.Head,
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
                                            ct.ThrowIfCancellationRequested()
                                            If preview.PreErase Then
                                                If cmd IsNot Nothing Then
                                                    cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.PreErase))
                                                End If
                                                usbClient.EraseTrack(driveTicksPerRev * 1.1)
                                            End If
                                            ' Python write.py:117-122 always appends `(<wflux summary>)` on
                                            ' the first attempt and `(Verify Failure: Retry #N)` thereafter.
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackWriting(New Greaseweazle.Actions.WriteTrackWritingEventArgs(
                                                    trackInfo,
                                                    If(retry = 0, writeSummary, Nothing),
                                                    retry))
                                            End If
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
                                                runNotVerifiedCount += 1
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
                                                runVerifiedCount += 1
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                            If cmd IsNot Nothing Then
                                                cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                            End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                                    If cmd IsNot Nothing Then
                                        cmd.OnTrackErasing(New Greaseweazle.Actions.WriteTrackErasingEventArgs(trackInfo, Greaseweazle.Actions.WriteEraseReason.EmptyTrack))
                                    End If
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
                            ' Python write.py:158-167 footer: pick the verdict
                            ' based on the verified vs not-verified tallies and
                            ' fire WriteVerifyOutcome so subscribers can render
                            ' the final summary line. CmdError thrown earlier
                            ' propagates out of this lambda — we mirror Python
                            ' which skips the footer in that case.
                            If runNotVerifiedCount = 0 Then
                                runOutcome = Greaseweazle.Actions.WriteVerifyOutcome.AllVerified
                            ElseIf preview.NoVerify Then
                                runOutcome = Greaseweazle.Actions.WriteVerifyOutcome.VerifyDisabled
                            Else
                                runOutcome = Greaseweazle.Actions.WriteVerifyOutcome.VerifyUnavailable
                            End If
                            If cmd IsNot Nothing Then
                                cmd.OnVerifyCompleted(New Greaseweazle.Actions.WriteVerifyOutcomeEventArgs(
                                    runOutcome, runVerifiedCount, runNotVerifiedCount))
                            End If
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=True)
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
            Return New Greaseweazle.Actions.WriteSummary(preview.Tracks,
                                                          preview.Format,
                                                          runOutcome,
                                                          runVerifiedCount,
                                                          runNotVerifiedCount,
                                                          dryRun:=False)
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
    Public NotInheritable Class ConvertAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/convert.py::main (post-parser algorithm body).
        '
        ' Pure-logic body — no console output. Streams progress via
        ' ConvertCommand events and returns a typed ConvertSummary.
        Public Shared Function RunFromOptions(preview As ConvertOptions,
                                              cmd As Greaseweazle.Actions.ConvertCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.ConvertSummary
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
            Dim inSpec = resolvedTracks.Item1.ToString()
            Dim outSpec = resolvedTracks.Item2.ToString()

            If cmd IsNot Nothing Then
                cmd.OnStarted(New Greaseweazle.Actions.ConvertStartedEventArgs(effectiveFormat, inSpec, outSpec))
            End If

            Dim hardSectorsCallback As Action(Of Greaseweazle.Actions.ConvertHardSectorsEventArgs) = Nothing
            Dim trackProcessedCallback As Action(Of Greaseweazle.Actions.ConvertTrackProcessedEventArgs) = Nothing
            If cmd IsNot Nothing Then
                hardSectorsCallback = Sub(args)
                                          ct.ThrowIfCancellationRequested()
                                          cmd.OnHardSectorsApplied(args)
                                      End Sub
                trackProcessedCallback = Sub(args)
                                             ct.ThrowIfCancellationRequested()
                                             cmd.OnTrackProcessed(args)
                                         End Sub
            End If

            Dim processedCount = 0
            Dim summaryDict = ConvertFunctions.Convert(resolvedTracks.Item2.IteratePhysical().ToList(),
                                                     resolvedTracks.Item1,
                                                     inputImage,
                                                     outputImage,
                                                     hardSectorsCallback,
                                                     Sub(args)
                                                         If trackProcessedCallback IsNot Nothing Then trackProcessedCallback(args)
                                                         If args.Outcome <> Greaseweazle.Actions.ConvertTrackOutcome.OutOfRange Then
                                                             processedCount += 1
                                                         End If
                                                     End Sub,
                                                     fmtCls,
                                                     effectiveFormat,
                                                     preview.Reverse,
                                                     preview.HardSectors,
                                                     preview.AdjustSpeed,
                                                     preview.PllProfiles)
            Dim grid = ReadWrite.BuildSectorSummary(resolvedTracks.Item1, summaryDict)
            If cmd IsNot Nothing Then
                cmd.OnSummaryReady(New Greaseweazle.Actions.ConvertSummaryReadyEventArgs(grid))
            End If

            Dim outExt = Path.GetExtension(outputPath)
            If IsSectorImageExtension(outExt) OrElse
               String.Equals(outExt, ".imd", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(outExt, ".hfe", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(outExt, ".d88", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(outExt, ".scp", StringComparison.OrdinalIgnoreCase) Then
                File.WriteAllBytes(outputPath, outputImage.GetImage())
            End If

            Return New Greaseweazle.Actions.ConvertSummary(inSpec, outSpec, processedCount, effectiveFormat, grid)
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
    Public NotInheritable Class EraseAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/erase.py::main.
        ' Raises EraseCommand.Started exactly once, then (in live mode)
        ' raises EraseCommand.TrackStarted before each track is touched.
        ' Returns a typed EraseSummary; CmdError propagates.
        Public Shared Function RunFromOptions(preview As EraseOptions,
                                              cmd As Greaseweazle.Actions.EraseCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.EraseSummary
            If cmd IsNot Nothing Then
                cmd.OnStarted(New Greaseweazle.Actions.EraseStartedEventArgs(preview.Tracks, preview.Revs))
            End If

            If Not preview.Live Then
                Return New Greaseweazle.Actions.EraseSummary(preview.Tracks, preview.Revs, 0, dryRun:=True)
            End If

            Dim processed = 0
            Dim usbClient As Unit = Nothing
            Try
                usbClient = ToolOptions.UsbOpen(preview.Device)
                ToolOptions.WithDriveSelected(
                    Sub()
                        Dim safeTracks = preview.TrackSet.IteratePhysical().ToList()
                        processed = [Erase].[Erase](usbClient, preview, safeTracks,
                            Sub(track As TrackIter)
                                ct.ThrowIfCancellationRequested()
                                If cmd IsNot Nothing Then
                                    cmd.OnTrackStarted(New Greaseweazle.Actions.EraseTrackEventArgs(track.Cyl, track.Head))
                                End If
                            End Sub)
                    End Sub,
                    New UsbDriveControlAdapter(usbClient),
                    preview.Drive,
                    motor:=True)
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    ' Tolerate close errors so an in-flight Ctrl-C path
                    ' (which may have already torn down the serial port)
                    ' doesn't mask the originating KeyboardInterruptException.
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
            Return New Greaseweazle.Actions.EraseSummary(preview.Tracks, preview.Revs, processed, dryRun:=False)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/clean.py::main (direct command execution mapping).
    Public NotInheritable Class CleanAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/clean.py::main.
        ' Raises PassStarted/CylinderSeeked/PassCompleted in pass order;
        ' in dry-run mode the events fire synchronously over Sequences
        ' (no USB), in live mode they fire alongside Clean.Clean's seeks.
        ' Returns a CleanSummary; CmdError propagates.
        Public Shared Function RunFromOptions(preview As CleanOptions,
                                              cmd As Greaseweazle.Actions.CleanCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.CleanSummary
            If Not preview.Live Then
                ' Python's --test path emits the same "Pass N: ..." lines
                ' the live path streams; replay them via the same events
                ' so subscribers don't need a separate "preview-mode"
                ' code path.
                Dim visited = 0
                For p = 0 To preview.Sequences.Count - 1
                    ct.ThrowIfCancellationRequested()
                    If cmd IsNot Nothing Then cmd.OnPassStarted(New Greaseweazle.Actions.CleanPassStartedEventArgs(p))
                    For Each cyl In preview.Sequences(p)
                        If cmd IsNot Nothing Then cmd.OnCylinderSeeked(New Greaseweazle.Actions.CleanCylinderEventArgs(p, cyl))
                        visited += 1
                    Next
                    If cmd IsNot Nothing Then cmd.OnPassCompleted(New Greaseweazle.Actions.CleanPassCompletedEventArgs(p))
                Next
                Return New Greaseweazle.Actions.CleanSummary(preview.Cyls, preview.Sequences.Count, visited, dryRun:=True)
            End If

            Dim cylindersVisited = 0
            Dim usbClient As Unit = Nothing
            Try
                usbClient = ToolOptions.UsbOpen(preview.Device)
                ToolOptions.WithDriveSelected(
                    Sub()
                        cylindersVisited = Clean.Clean(usbClient, preview,
                            Sub(passIndex As Integer)
                                ct.ThrowIfCancellationRequested()
                                If cmd IsNot Nothing Then cmd.OnPassStarted(New Greaseweazle.Actions.CleanPassStartedEventArgs(passIndex))
                            End Sub,
                            Sub(passIndex As Integer, clamped As Integer)
                                ct.ThrowIfCancellationRequested()
                                If cmd IsNot Nothing Then cmd.OnCylinderSeeked(New Greaseweazle.Actions.CleanCylinderEventArgs(passIndex, clamped))
                            End Sub,
                            Sub(passIndex As Integer)
                                If cmd IsNot Nothing Then cmd.OnPassCompleted(New Greaseweazle.Actions.CleanPassCompletedEventArgs(passIndex))
                            End Sub)
                    End Sub,
                    New UsbDriveControlAdapter(usbClient),
                    preview.Drive,
                    motor:=True)
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    ' Tolerate close errors so an in-flight Ctrl-C path
                    ' (which may have already torn down the serial port)
                    ' doesn't mask the originating KeyboardInterruptException.
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
            Return New Greaseweazle.Actions.CleanSummary(preview.Cyls, preview.Sequences.Count, cylindersVisited, dryRun:=False)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/seek.py::main (direct command execution mapping).
    Public NotInheritable Class SeekAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/seek.py::main.
        ' Returns a typed SeekResult; throws CmdError on a recoverable USB
        ' error so the caller (CLI or library consumer) decides how to
        ' surface the failure. Emits no text — the prompter is the only
        ' interactive surface, and even it is supplied by the caller.
        Public Shared Function RunFromOptions(preview As SeekOptions,
                                              prompter As Greaseweazle.Actions.ISeekPrompter) As Greaseweazle.Actions.SeekResult
            If preview.PromptNeeded Then
                ' Default-deny: a caller that ignored the Prompter
                ' property and lands here unprompted aborts rather than
                ' silently risking a head-crash.
                If prompter Is Nothing OrElse Not prompter.Confirm(preview.PromptText) Then
                    Return New Greaseweazle.Actions.SeekResult(
                        Greaseweazle.Actions.SeekOutcome.Aborted, preview.Cyl)
                End If
            End If

            If Not preview.Live Then
                Return New Greaseweazle.Actions.SeekResult(
                    Greaseweazle.Actions.SeekOutcome.DryRun, preview.Cyl)
            End If

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
                Return New Greaseweazle.Actions.SeekResult(
                    Greaseweazle.Actions.SeekOutcome.Seeked, preview.Cyl)
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    ' Tolerate close errors so an in-flight Ctrl-C path
                    ' (which may have already torn down the serial port)
                    ' doesn't mask the originating KeyboardInterruptException.
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/delays.py::main (direct command execution mapping).
    Public NotInheritable Class DelaysAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/delays.py::main (post-parser algorithm body).
        ' Returns a typed DelaysResult; throws CmdError or FatalException on
        ' recoverable failures (the CLI catches CmdError into "Command
        ' Failed: ..."; FatalException already carries its own banner). For
        ' --test dry-run mode returns Nothing — the CLI renders no output.
        Public Shared Function RunFromOptions(preview As DelaysOptions) As Greaseweazle.Actions.DelaysResult
            If Not preview.Live Then Return Nothing

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

                Return New Greaseweazle.Actions.DelaysResult(
                    selectMicros:=values(0),
                    stepMicros:=values(1),
                    settleMillis:=values(2),
                    motorMillis:=values(3),
                    watchdogMillis:=values(4),
                    preWriteMicros:=If(paramSize >= 12, CType(values(5), Integer?), Nothing),
                    postWriteMicros:=If(paramSize >= 14, CType(values(6), Integer?), Nothing),
                    indexMaskMicros:=If(paramSize >= 16, CType(values(7), Integer?), Nothing))
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    ' Tolerate close errors so an in-flight Ctrl-C path
                    ' (which may have already torn down the serial port)
                    ' doesn't mask the originating KeyboardInterruptException.
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/update.py::main (direct command execution mapping).
    Public NotInheritable Class UpdateAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/update.py::main.
        ' Resolves the firmware payload, validates it against the
        ' connected device, and flashes it. Raises DownloadStarted +
        ' UpdateStarted at the points where Python prints the
        ' corresponding lines, then returns a typed UpdateSummary
        ' describing the outcome. CmdError propagates for unrecoverable
        ' USB errors so the caller can surface ack-specific messages.
        Public Shared Function RunFromOptions(preview As UpdateOptions,
                                              cmd As Greaseweazle.Actions.UpdateCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.UpdateSummary
            Dim target = If(preview.Bootloader,
                            Greaseweazle.Actions.UpdateTarget.Bootloader,
                            Greaseweazle.Actions.UpdateTarget.MainFirmware)

            If Not preview.Live Then
                Return Greaseweazle.Actions.UpdateSummary.ForDryRun(target)
            End If

            Dim updateFile = Update.ResolvePayload(preview,
                Sub(name As String)
                    If cmd IsNot Nothing Then
                        cmd.OnDownloadStarted(New Greaseweazle.Actions.UpdateDownloadStartedEventArgs(name))
                    End If
                End Sub)

            Dim usbClient As Unit = Nothing
            Try
                usbClient = ToolOptions.UsbOpen(preview.Device, modeCheck:=False)
                ' UsbOpen already populated firmware capabilities via
                ' ReadFirmwareInfo + ApplyFirmwareInfo; ExtractUpdate only
                ' reads HwModel so synthesize a minimal info object rather
                ' than issuing a second Get-Info round-trip.
                Dim info = New FirmwareInfo() With {.HwModel = usbClient.HwModel}
                Dim extracted = Update.ExtractUpdate(info, updateFile, preview.Bootloader)

                If cmd IsNot Nothing Then
                    cmd.OnUpdateStarted(New Greaseweazle.Actions.UpdateStartedEventArgs(target, extracted.Major, extracted.Minor))
                End If

                If Not preview.Force AndAlso (usbClient.CanModeSwitch OrElse preview.Bootloader = usbClient.UpdateMode) Then
                    If preview.Bootloader <> usbClient.UpdateMode Then
                        usbClient = ToolOptions.UsbReopen(usbClient, isUpdate:=preview.Bootloader)
                        ErrorHandling.Check(preview.Bootloader = usbClient.UpdateMode, "Device did not mode switch as requested")
                    End If

                    If usbClient.Major > extracted.Major OrElse
                       (usbClient.Major = extracted.Major AndAlso usbClient.Minor >= extracted.Minor) Then
                        Dim deviceMajor = usbClient.Major
                        Dim deviceMinor = usbClient.Minor
                        If usbClient.UpdateMode AndAlso usbClient.CanModeSwitch Then
                            usbClient = ToolOptions.UsbReopen(usbClient, isUpdate:=False)
                        End If
                        Return Greaseweazle.Actions.UpdateSummary.ForSkipped(target,
                            extracted.Major, extracted.Minor, deviceMajor, deviceMinor)
                    End If
                End If

                usbClient = ToolOptions.UsbModeCheck(usbClient, isUpdate:=Not preview.Bootloader)
                Dim ack = Update.UpdateFirmware(usbClient, extracted.Payload, preview.Bootloader)

                Dim summary As Greaseweazle.Actions.UpdateSummary
                If ack <> 0 Then
                    summary = Greaseweazle.Actions.UpdateSummary.ForFailed(target, extracted.Major, extracted.Minor, ack)
                Else
                    Dim needsUnplug = (Not preview.Bootloader) AndAlso (Not usbClient.JumperlessUpdate)
                    summary = Greaseweazle.Actions.UpdateSummary.ForCompleted(target, extracted.Major, extracted.Minor, needsUnplug)
                End If

                If usbClient.UpdateMode AndAlso usbClient.CanModeSwitch Then
                    usbClient = ToolOptions.UsbReopen(usbClient, isUpdate:=False)
                End If
                Return summary
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/pin.py::main (direct command execution mapping).
    Public NotInheritable Class PinAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/pin.py::main (post-parser algorithm body).
        ' Returns a typed PinResult; throws CmdError for recoverable USB errors.
        ' Emits no text — formatting is the CLI front-end's responsibility.
        Public Shared Function RunFromOptions(preview As PinOptions) As Greaseweazle.Actions.PinResult
            If String.Equals(preview.Mode, "usage", StringComparison.Ordinal) Then
                ' Python pin.py:62-65 calls sys.exit(1) from usage(); the CLI
                ' maps PinResultKind.UsageRequested to rc=1.
                Return Greaseweazle.Actions.PinResult.Usage()
            ElseIf String.Equals(preview.Mode, "set", StringComparison.Ordinal) Then
                If Not preview.Live Then
                    ' --test dry-run: skip hardware contact and surface the
                    ' would-be success result so callers can assert on it.
                    Return Greaseweazle.Actions.PinResult.Set(preview.Pin, preview.Level)
                End If
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    ' Python pin.py:30-31 returns the success result AFTER
                    ' set_pin returns; on CmdError that path is replaced by
                    ' the thrown exception which the caller renders as the
                    ' "Command Failed" line.
                    usbClient.SetPin(preview.Pin, preview.Level)
                    Return Greaseweazle.Actions.PinResult.Set(preview.Pin, preview.Level)
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            ElseIf String.Equals(preview.Mode, "get", StringComparison.Ordinal) Then
                If Not preview.Live Then
                    ' Python pin.py:51 only invokes pin_get when args.live is
                    ' true; --test is therefore a no-op. We don't fabricate a
                    ' fake level, so the caller renders nothing for this
                    ' result.
                    Return Greaseweazle.Actions.PinResult.NoOp()
                End If
                Dim usbClient As Unit = Nothing
                Try
                    usbClient = ToolOptions.UsbOpen(preview.Device)
                    Dim level As Boolean = False
                    ToolOptions.WithDriveSelected(
                        Sub()
                            level = Pin.PinGetInner(usbClient, preview.Pin)
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=False)
                    Return Greaseweazle.Actions.PinResult.Value(preview.Pin, level)
                Finally
                    If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                        Try : usbClient.Serial.Close() : Catch : End Try
                    End If
                End Try
            End If
            ' Defensive: an unrecognised Mode means BuildRuntimePreview added
            ' a new value the algorithm hasn't been taught — surface it as
            ' UsageRequested so the CLI prints usage rather than crashing.
            Return Greaseweazle.Actions.PinResult.Usage()
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/reset.py::main (direct command execution mapping).
    Public NotInheritable Class ResetAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/reset.py::main (post-parser algorithm body).
        ' Throws CmdError on a recoverable USB error so the caller (CLI or
        ' library consumer) decides how to surface the failure. Emits no text.
        Public Shared Sub RunFromOptions(preview As ResetOptions)
            If Not preview.Live Then Return
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
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    ' Tolerate close errors so an in-flight Ctrl-C path
                    ' (which may have already torn down the serial port)
                    ' doesn't mask the originating KeyboardInterruptException.
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/bandwidth.py::main (direct command execution mapping).
    Public NotInheritable Class BandwidthAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/bandwidth.py::main.
        ' Throws CmdError on a recoverable USB error; returns Nothing for
        ' --test dry-run mode.
        Public Shared Function RunFromOptions(preview As BandwidthOptions) As Greaseweazle.Actions.BandwidthResult
            If Not preview.Live Then Return Nothing
            Dim usbClient As Unit = Nothing
            Try
                usbClient = ToolOptions.UsbOpen(preview.Device)
                Return Bandwidth.Measure(usbClient)
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    ' Tolerate close errors so an in-flight Ctrl-C path
                    ' (which may have already torn down the serial port)
                    ' doesn't mask the originating KeyboardInterruptException.
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/rpm.py::main (direct command execution mapping).
    Public NotInheritable Class RpmAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/rpm.py::main.
        ' Reads Nr index periods, raising SampleMeasured per measurement
        ' and SummaryReady (in the loop's Finally) when at least 2
        ' samples were collected. Returns RpmSummary on success;
        ' propagates CmdError after raising SummaryReady so a partial
        ' run still surfaces the stats it managed to collect.
        Public Shared Function RunFromOptions(preview As RpmOptions,
                                              cmd As Greaseweazle.Actions.RpmCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.RpmSummary
            If Not preview.Live Then
                Return Greaseweazle.Actions.RpmSummary.ForDryRun()
            End If

            Dim samples As New List(Of Double)()
            Dim completed = False
            Dim usbClient As Unit = Nothing
            Try
                usbClient = ToolOptions.UsbOpen(preview.Device)
                Dim sampleFreq = usbClient.SampleFreq
                Try
                    ToolOptions.WithDriveSelected(
                        Sub()
                            For i = 1 To preview.Nr
                                ct.ThrowIfCancellationRequested()
                                Dim flux = usbClient.ReadTrack(1, 0)
                                Dim tpr = flux.IndexList.Last() / sampleFreq
                                samples.Add(tpr)
                                If cmd IsNot Nothing Then
                                    cmd.OnSampleMeasured(New Greaseweazle.Actions.RpmSampleMeasuredEventArgs(i, tpr))
                                End If
                            Next
                            completed = True
                        End Sub,
                        New UsbDriveControlAdapter(usbClient),
                        preview.Drive,
                        motor:=True)
                Finally
                    ' Python rpm.py:24-38: emit the FASTEST/Mean/Median/
                    ' SLOWEST block in a try/finally so a partial run
                    ' still gets a summary before any exception keeps
                    ' propagating. Mirror that ordering.
                    If samples.Count > 1 Then
                        Dim partialSummary = Greaseweazle.Actions.RpmSummary.FromSamples(samples, completed)
                        If cmd IsNot Nothing AndAlso partialSummary IsNot Nothing Then
                            cmd.OnSummaryReady(New Greaseweazle.Actions.RpmSummaryReadyEventArgs(partialSummary))
                        End If
                    End If
                End Try
            Finally
                If usbClient IsNot Nothing AndAlso usbClient.Serial IsNot Nothing Then
                    ' Tolerate close errors so an in-flight Ctrl-C path
                    ' (which may have already torn down the serial port)
                    ' doesn't mask the originating KeyboardInterruptException.
                    Try : usbClient.Serial.Close() : Catch : End Try
                End If
            End Try

            ' Successful path: return the (possibly Nothing) summary so
            ' library consumers can branch on Completed/Samples.Count.
            If samples.Count > 1 Then
                Return Greaseweazle.Actions.RpmSummary.FromSamples(samples, completed)
            End If
            Return New Greaseweazle.Actions.RpmSummary(
                samples,
                fastest:=0, slowest:=0, mean:=0, median:=0,
                completed:=completed, dryRun:=False)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/align.py::main (direct command execution mapping).
    Public NotInheritable Class AlignAction

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/align.py::main.
        ' --test mode: raises Started once with the parse-time header.
        ' Live mode: applies fractional-revs / hard-sector adjustments,
        ' then drives Align.AlignTrack which raises Started + ReadCompleted
        ' through the supplied AlignCommand. CmdError propagates.
        Public Shared Function RunFromOptions(preview As AlignOptions,
                                              cmd As Greaseweazle.Actions.AlignCommand,
                                              ct As CancellationToken) As Greaseweazle.Actions.AlignSummary
            If Not preview.Live Then
                If cmd IsNot Nothing Then
                    cmd.OnStarted(New Greaseweazle.Actions.AlignStartedEventArgs(
                        preview.TrackSet.IteratePhysical().ToList(),
                        preview.Reads,
                        preview.Revs,
                        preview.Format))
                End If
                Return New Greaseweazle.Actions.AlignSummary(preview.Reads, 0, dryRun:=True)
            End If

            Dim readsCompleted = 0
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
                            If cmd IsNot Nothing Then
                                cmd.OnHardSectorsDetected(New Greaseweazle.Actions.AlignHardSectorsDetectedEventArgs(hardSectorCount))
                            End If
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

                        readsCompleted = Align.AlignTrack(
                            usbClient,
                            preview.TrackSet.IteratePhysical().ToList(),
                            preview.Reads,
                            effectiveRevs,
                            effectiveTicks,
                            preview.Reverse,
                            preview.HardSectors,
                            preview.Raw,
                            preview.AdjustSpeed,
                            driveTicksPerRev,
                            Sub(tracks As IReadOnlyList(Of TrackIter), reads As Integer, revs As Integer, formatName As String)
                                If cmd IsNot Nothing Then
                                    cmd.OnStarted(New Greaseweazle.Actions.AlignStartedEventArgs(tracks, reads, revs, formatName))
                                End If
                            End Sub,
                            Sub(args As Greaseweazle.Actions.AlignReadCompletedEventArgs)
                                ct.ThrowIfCancellationRequested()
                                If cmd IsNot Nothing Then cmd.OnReadCompleted(args)
                            End Sub,
                            preview.FakeIndexPeriod,
                            preview.FormatDef,
                            preview.Format,
                            preview.PllProfiles)
                    End Sub,
                    New UsbDriveControlAdapter(usbClient),
                    preview.Drive,
                    motor:=True)
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
            Return New Greaseweazle.Actions.AlignSummary(preview.Reads, readsCompleted, dryRun:=False)
        End Function
    End Class

End Namespace
