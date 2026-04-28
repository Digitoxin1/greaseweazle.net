Imports System.IO
Imports System.Linq
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Security.Cryptography
Imports System.Text
Imports Greaseweazle.Codecs
Imports Greaseweazle.Core
Imports Greaseweazle.Images
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Optimised
Imports Greaseweazle.Shared
Imports Greaseweazle.Tools

Namespace Greaseweazle.Parity

    Module Program

        Sub Main()
            Dim fixtureDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures")
            RunErrorParity(Path.Combine(fixtureDir, "error-fixtures.json"))
            RunFluxParity(Path.Combine(fixtureDir, "flux-fixtures.json"))
            RunTrackParity(Path.Combine(fixtureDir, "track-fixtures.json"))
            RunUsbParity(Path.Combine(fixtureDir, "usb-fixtures.json"))
            RunOptimisedParity()
            RunToolsParity(Path.Combine(fixtureDir, "tools-fixtures.json"))
            RunWindowsPortDiscoveryParity()
            RunCodecParity(Path.Combine(fixtureDir, "codec-fixtures.json"))
            RunImageParity(Path.Combine(fixtureDir, "image-fixtures.json"))
            RunCliParity(Path.Combine(fixtureDir, "cli-fixtures.json"))
            RunTrackSetParity(Path.Combine(fixtureDir, "trackset-fixtures.json"))
            RunActionDescriptionsParity(Path.Combine(fixtureDir, "actions-fixtures.json"))
            RunPrecompParity(Path.Combine(fixtureDir, "precomp-fixtures.json"))
            RunReadWriteAlgorithmParity(Path.Combine(fixtureDir, "readwrite-fixtures.json"))
            RunInfoParity(Path.Combine(fixtureDir, "info-fixtures.json"))
            RunUpdateParity(Path.Combine(fixtureDir, "update-fixtures.json"))
            RunPinParity(Path.Combine(fixtureDir, "pin-fixtures.json"))
            RunResetParity(Path.Combine(fixtureDir, "reset-fixtures.json"))
            RunSeekParity(Path.Combine(fixtureDir, "seek-fixtures.json"))
            RunDelaysParity(Path.Combine(fixtureDir, "delays-fixtures.json"))
            RunCleanParity(Path.Combine(fixtureDir, "clean-fixtures.json"))
            RunConvertParity(Path.Combine(fixtureDir, "convert-fixtures.json"))
            RunEraseParity(Path.Combine(fixtureDir, "erase-fixtures.json"))
            RunBandwidthParity(Path.Combine(fixtureDir, "bandwidth-fixtures.json"))
            RunRpmParity(Path.Combine(fixtureDir, "rpm-fixtures.json"))
            RunAlignParity(Path.Combine(fixtureDir, "align-fixtures.json"))
            RunTrackResolutionSharedParity(Path.Combine(fixtureDir, "track-resolution-fixtures.json"))
            RunScpParity(Path.Combine(fixtureDir, "scp-fixtures.json"))
            Console.WriteLine("Parity checks passed for error/flux/track/usb/optimised-native/tools/codec/image/cli/trackset/actions/precomp/readwrite/info/find_port/update/range_str/pin/reset/seek/delays/clean/convert/erase/bandwidth/rpm/align/track-resolution/scp fixtures.")
        End Sub

        Private Sub RunOptimisedParity()
            Dim symbols = Enumerable.Range(0, 64).Select(Function(i) CByte(i)).ToArray()
            Dim gcrEnc = AppleGcr62.EncodeBytes(symbols)
            Dim gcrDec = AppleGcr62.DecodeBytes(gcrEnc)
            AssertTrue(gcrDec.SequenceEqual(symbols), "optimised.apple_gcr_6a2 roundtrip mismatch")

            Dim badDecode = AppleGcr62.DecodeBytes(New Byte() {0})
            AssertTrue(badDecode.Length = 1 AndAlso badDecode(0) = &HFF, "optimised.apple_gcr_6a2.decode_bytes invalid-code mapping mismatch")
            Dim badEncode = AppleGcr62.EncodeBytes(New Byte() {&HFF})
            AssertTrue(badEncode.Length = 1 AndAlso badEncode(0) = &HFF, "optimised.apple_gcr_6a2.encode_bytes invalid-code mapping mismatch")
            AssertTrue(AppleGcr62.EncodeByte(&HFF) = -1, "optimised.apple_gcr_6a2.encode_byte invalid-code mismatch")

            Dim apple2Input = Enumerable.Range(0, 256).Select(Function(i) CByte((i * 37 + 11) And &HFF)).ToArray()
            Dim apple2Encoded = Apple2.EncodeSector(apple2Input)
            AssertTrue(apple2Encoded.Length = 343, "optimised.apple2.encode_sector output length mismatch")
            Dim apple2Decoded = Apple2.DecodeSector(apple2Encoded)
            AssertTrue(apple2Decoded.Item2 = 0, "optimised.apple2.decode_sector checksum mismatch")
            AssertTrue(apple2Decoded.Item1.SequenceEqual(apple2Input), "optimised.apple2 roundtrip mismatch")
            apple2Encoded(apple2Encoded.Length - 1) = CByte(apple2Encoded(apple2Encoded.Length - 1) Xor 1)
            Dim apple2Corrupt = Apple2.DecodeSector(apple2Encoded)
            AssertTrue(apple2Corrupt.Item2 = 1, "optimised.apple2 corrupted status mismatch")

            Dim c64Input = Enumerable.Range(0, 8).Select(Function(i) CByte((i * 19 + 5) And &HFF)).ToArray()
            Dim c64Encoded = C64.EncodeGcr(c64Input)
            Dim c64Decoded = C64.DecodeGcr(c64Encoded, c64Input.Length)
            AssertTrue(c64Decoded.SequenceEqual(c64Input), "optimised.c64 roundtrip mismatch")
            Dim c64Invalid = C64.DecodeGcr(New Byte() {0, 0, 0, 0, 0}, 4)
            AssertTrue(c64Invalid.Length = 4, "optimised.c64.decode_gcr invalid-symbol length mismatch")

            Dim macInput = Enumerable.Range(0, 524).Select(Function(i) CByte((i * 23 + 17) And &HFF)).ToArray()
            Dim macEncoded = Mac.EncodeSector(macInput)
            AssertTrue(macEncoded.Length = 703, "optimised.mac.encode_sector output length mismatch")
            Dim macDecoded = Mac.DecodeSector(macEncoded)
            AssertTrue(macDecoded.Item2 = 0, "optimised.mac.decode_sector checksum mismatch")
            AssertTrue(macDecoded.Item1.SequenceEqual(macInput), "optimised.mac roundtrip mismatch")

            Dim encodedFlux = New Byte() {10, &HFF, 1, 11, 1, 1, 1, 20, 0}
            Dim decodedFlux = OptimizedFlux.DecodeFlux(encodedFlux)
            AssertSequence(decodedFlux.Item1, New Double() {10, 20}, "optimised.decode_flux.flux_list")
            AssertSequence(decodedFlux.Item2, New Double() {15}, "optimised.decode_flux.index_list")

            AssertTrue(Td0Lzss.Unpack(Array.Empty(Of Byte)()).Length = 0, "optimised.td0_lzss empty input mismatch")
        End Sub

        Private Sub RunErrorParity(path As String)
            Dim root = LoadJson(Of ErrorFixtureRoot)(path)
            Dim falseCase = root.Cases.First(Function(c) c.Name = "check_false")
            Dim threw = False
            Try
                ErrorHandling.Check(False, falseCase.Message)
            Catch ex As FatalException
                threw = (ex.Message = falseCase.Message)
            End Try
            AssertTrue(threw, "Expected FatalException for check_false.")

            ErrorHandling.Check(True, "ok")
        End Sub

        Private Sub RunFluxParity(path As String)
            Dim root = LoadJson(Of FluxFixtureRoot)(path)

            Dim cueFlux = New Flux({30.0, 100.0, 100.0}, {10.0, 10.0, 10.0, 20.0, 30.0, 40.0, 60.0}, 1000.0, False)
            cueFlux.CueAtIndex()
            AssertSequence(cueFlux.IndexList, root.CueAtIndex.IndexList, "cue_at_index.index_list")
            AssertSequence(cueFlux.List, root.CueAtIndex.FluxList, "cue_at_index.flux_list")

            Dim summaryFlux = New Flux({100.0, 100.0}, {20.0, 30.0, 50.0, 20.0, 30.0, 50.0}, 1000.0, True)
            AssertTrue(summaryFlux.SummaryString() = root.Summary.Summary, "summary string mismatch")
            AssertNear(summaryFlux.TicksPerRev, root.Summary.TicksPerRev, 0.0001, "ticks_per_rev mismatch")

            Dim reverseFlux = New Flux({100.0, 100.0}, {20.0, 30.0, 50.0, 20.0, 30.0, 50.0}, 1000.0, True)
            reverseFlux.Reverse()
            AssertSequence(reverseFlux.IndexList, root.Reverse.IndexList, "reverse.index_list")
            AssertSequence(reverseFlux.List, root.Reverse.FluxList, "reverse.flux_list")
            AssertTrue(reverseFlux.IndexCued = root.Reverse.IndexCued, "reverse.index_cued mismatch")

            Dim revFlux = New Flux({100.0, 100.0}, {20.0, 30.0, 50.0, 20.0, 30.0, 50.0}, 1000.0, True)
            revFlux.SetNrRevs(1)
            AssertSequence(revFlux.IndexList, root.SetNrRevs.IndexList, "set_nr_revs.index_list")
            AssertSequence(revFlux.List, root.SetNrRevs.FluxList, "set_nr_revs.flux_list")

            Dim writeFlux = New Flux({100.0, 100.0}, {20.0, 30.0, 50.0, 20.0, 30.0, 50.0}, 1000.0, True)
            Dim wf = writeFlux.FluxForWriteout(True)
            AssertNear(wf.TicksToIndex, root.Writeout.TicksToIndex, 0.0001, "writeout.ticks_to_index")
            AssertSequence(wf.List, root.Writeout.List, "writeout.list")
            AssertTrue(wf.IndexCued = root.Writeout.IndexCued, "writeout.index_cued mismatch")
            AssertTrue(wf.TerminateAtIndex = root.Writeout.TerminateAtIndex, "writeout.terminate_at_index mismatch")
            AssertTrue(wf.SummaryString() = root.Writeout.Summary, "writeout.summary mismatch")
        End Sub

        Private Sub RunTrackParity(path As String)
            Dim root = LoadJson(Of TrackFixtureRoot)(path)
            Dim bits = New Boolean() {True, False, True, False, True, False, False, True}

            Dim mt = New MasterTrack(bits, 0.2, splice:=2)
            Dim flux = mt.Flux(2)
            AssertNear(mt.Bitrate, root.MasterFlux.Bitrate, 0.0001, "master_flux.bitrate")
            AssertTrue(mt.SummaryString() = root.MasterFlux.Summary, "master_flux.summary")
            AssertSequence(flux.IndexList, root.MasterFlux.FluxIndexList, "master_flux.flux_index_list")
            AssertSequence(flux.List, root.MasterFlux.FluxList, "master_flux.flux_list")
            AssertNear(flux.Splice.GetValueOrDefault(), root.MasterFlux.FluxSplice, 0.0001, "master_flux.flux_splice")
            AssertNear(flux.SampleFreq, root.MasterFlux.FluxSampleFreq, 0.0001, "master_flux.flux_sample_freq")

            Dim mtw = New MasterTrack(bits, 0.2, splice:=2)
            Dim wf = mtw.FluxForWriteout(True)
            AssertNear(wf.TicksToIndex, root.MasterWriteout.TicksToIndex, 0.0001, "master_writeout.ticks_to_index")
            AssertSequence(wf.List, root.MasterWriteout.List, "master_writeout.list")
            AssertTrue(wf.TerminateAtIndex = root.MasterWriteout.TerminateAtIndex, "master_writeout.terminate_at_index")

            Dim ptrack = New PllTrack(2.0E-6, flux, pll:=New Pll("period=5:phase=60"))
            AssertTrue(ptrack.Revolutions.Count = root.PllTrack.NrRevs, "pll_track.nr_revs")
            AssertSequence(ptrack.Revolutions.Select(Function(x) CDbl(x.NrBits)), root.PllTrack.RevBits.Select(Function(x) CDbl(x)), "pll_track.rev_bits")
            AssertTrue(ptrack.BitArray.Count = root.PllTrack.TotalBits, "pll_track.total_bits")
            AssertTrue(ptrack.TimeArray.Count = root.PllTrack.TotalTimes, "pll_track.total_times")
        End Sub

        Private Sub RunUsbParity(path As String)
            Dim root = LoadJson(Of UsbFixtureRoot)(path)
            Dim encoded = UsbProtocol.EncodeFlux({100, 300, 1000, 2000}, 24000000)
            AssertSequence(encoded.Select(Function(x) CDbl(x)), root.EncodeDecodeBasic.Encoded.Select(Function(x) CDbl(x)), "usb.encode_decode.encoded")
            Dim decoded = UsbProtocol.DecodeFlux(encoded)
            AssertSequence(decoded.Item1, root.EncodeDecodeBasic.DecodedFlux, "usb.encode_decode.decoded_flux")
            AssertSequence(decoded.Item2, root.EncodeDecodeBasic.DecodedIndex, "usb.encode_decode.decoded_index")

            Dim astableBytes = root.DecodeAstableFailure.Encoded.Select(Function(x) CByte(x)).ToArray()
            Dim caught = False
            Try
                Dim ignored = UsbProtocol.DecodeFlux(astableBytes)
            Catch ex As FatalException
                caught = True
                AssertTrue(ex.Message = root.DecodeAstableFailure.Error, "usb.decode_astable_failure message mismatch")
            End Try
            AssertTrue(caught, "usb.decode_astable_failure expected exception")
        End Sub

        Private Sub RunToolsParity(path As String)
            Dim root = LoadJson(Of ToolsFixtureRoot)(path)
            For Each periodCase In root.PeriodCases
                Dim actual = ToolOptions.Period(periodCase.Input)
                AssertNear(actual, periodCase.Output, 1.0E-12, String.Format("tools.period({0})", periodCase.Input))
            Next

            For Each splitCase In root.SplitOptsCases
                Dim result = ToolOptions.SplitOpts(splitCase.Input)
                AssertTrue(result.Item1 = splitCase.Name, String.Format("tools.split_opts({0}).name", splitCase.Input))
                AssertTrue(result.Item2.Count = splitCase.Opts.Count, String.Format("tools.split_opts({0}).count", splitCase.Input))
                For Each expected In splitCase.Opts
                    AssertTrue(result.Item2.ContainsKey(expected.Key), String.Format("tools.split_opts({0}).key {1}", splitCase.Input, expected.Key))
                    AssertTrue(result.Item2(expected.Key) = expected.Value, String.Format("tools.split_opts({0}).value {1}", splitCase.Input, expected.Key))
                Next
            Next

            Dim column = Greaseweazle.Shared.ColumnFormatter.Columnify(root.Columnify.Input)
            AssertTrue(column = root.Columnify.Output, "tools.columnify output mismatch")

            Dim suffixes = New ImageTypeRegistry().GetKnownSuffixes().OrderBy(Function(x) x).ToList()
            AssertTrue(suffixes.SequenceEqual(root.ImageSuffixes), "tools.image_suffixes mismatch")

            For Each driveCase In root.DriveCases
                Dim parsed = ToolOptions.Drive(driveCase.Input)
                AssertTrue(CInt(parsed.Bus) = driveCase.Bus, String.Format("tools.parse_drive({0}).bus", driveCase.Input))
                AssertTrue(parsed.UnitId = driveCase.Unit, String.Format("tools.parse_drive({0}).unit", driveCase.Input))
            Next

            For Each levelCase In root.LevelCases
                Dim actual = ToolOptions.Level(levelCase.Input)
                AssertTrue(actual = levelCase.Output, String.Format("tools.parse_level({0})", levelCase.Input))
            Next

            For Each scoreCase In root.ScorePortCases
                Dim port = ToPortDescriptor(scoreCase.Port)
                Dim oldPort = If(scoreCase.OldPort Is Nothing, Nothing, ToPortDescriptor(scoreCase.OldPort))
                AssertTrue(port.Interface = scoreCase.Port.Interface, "tools.score_port port.interface mapping mismatch")
                If scoreCase.OldPort Is Nothing Then
                    AssertTrue(oldPort Is Nothing, "tools.score_port old_port.interface mapping mismatch")
                Else
                    AssertTrue(oldPort.Interface = scoreCase.OldPort.Interface, "tools.score_port old_port.interface mapping mismatch")
                End If
                Dim actual = ToolOptions.ScorePort(port, oldPort)
                AssertTrue(actual = scoreCase.Score, "tools.score_port mismatch")
            Next

            For Each findCase In root.FindPortCases
                Dim ports = findCase.Ports.Select(Function(p) ToPortDescriptor(p)).ToList()
                Dim oldPort = If(findCase.OldPort Is Nothing, Nothing, ToPortDescriptor(findCase.OldPort))
                For i = 0 To findCase.Ports.Count - 1
                    AssertTrue(ports(i).Interface = findCase.Ports(i).Interface, String.Format("tools.find_port ports[{0}].interface mapping mismatch", i))
                Next
                If findCase.OldPort Is Nothing Then
                    AssertTrue(oldPort Is Nothing, "tools.find_port old_port.interface mapping mismatch")
                Else
                    AssertTrue(oldPort.Interface = findCase.OldPort.Interface, "tools.find_port old_port.interface mapping mismatch")
                End If
                Dim selected = ToolOptions.FindBestPort(ports, oldPort)
                Dim device = If(selected Is Nothing, Nothing, selected.Device)
                AssertTrue(device = findCase.Selected, "tools.find_port selection mismatch")
                If findCase.Selected Is Nothing Then
                    Dim threw = False
                    Try
                        Dim ignored = ToolOptions.FindPortDevice(ports, oldPort)
                    Catch ex As IO.IOException
                        ' FindPortDevice now throws IOException to mirror
                        ' Python's serial.SerialException so the info action's
                        ' "Not found" branch catches it with exit code 0.
                        threw = (ex.Message = "Cannot find the Greaseweazle device")
                    End Try
                    AssertTrue(threw, "tools.find_port failure exception mismatch")
                Else
                    Dim selectedDevice = ToolOptions.FindPortDevice(ports, oldPort)
                    AssertTrue(selectedDevice = findCase.Selected, "tools.find_port device mismatch")
                End If
            Next

            For Each serCase In root.ValidSerIdCases
                Dim actual = ToolOptions.ValidSerialId(serCase.Input)
                AssertTrue(actual = serCase.Value, "tools.valid_ser_id mismatch")
            Next

            For Each driveCase In root.WithDriveSelectedCases
                Dim fake As New FakeUsbDriveControl()
                Dim drive = ToolOptions.Drive("A")
                Dim raised = False
                Try
                    ToolOptions.WithDriveSelected(
                        Sub()
                            If driveCase.Name = "success" Then
                                fake.Log.Add("fn")
                            ElseIf driveCase.Name = "keyboard_interrupt" Then
                                Throw New KeyboardInterruptException()
                            End If
                        End Sub,
                        fake,
                        drive,
                        True)
                Catch ex As KeyboardInterruptException
                    raised = True
                End Try
                AssertTrue(raised = driveCase.Raised, String.Format("tools.with_drive_selected({0}).raised", driveCase.Name))
                AssertTrue(fake.Log.SequenceEqual(driveCase.Log), String.Format("tools.with_drive_selected({0}).log", driveCase.Name))
            Next

            For Each rangeCase In root.RangeStrCases
                Dim actual = ToolOptions.RangeStr(rangeCase.Input)
                AssertTrue(actual = rangeCase.Output, "tools.range_str mismatch")
            Next
        End Sub

        Private Sub RunWindowsPortDiscoveryParity()
            Dim invalidHub As New UsbHubDeviceIOControl("USBHUB#0")
            invalidHub.Open()
            AssertTrue(Not invalidHub.IsOpen, "windows_port_discovery.open_invalid_path")
            invalidHub.Close()

            Dim hub As New UsbHubDeviceIOControl("\\.\USBHUB#0")
            hub.Open()
            AssertTrue(hub.IsOpen, "windows_port_discovery.open_valid_path")

            Dim langs = hub.RequestSupportedLanguages(1)
            AssertTrue(langs IsNot Nothing AndAlso langs.Count = 1 AndAlso langs(0) = &H409US,
                       "windows_port_discovery.request_supported_languages")
            AssertTrue(hub.SuggestLanguageId(1) = &H409US, "windows_port_discovery.suggest_language_id")

            Dim manufacturer = hub.RequestUsbStringDescription(1, 1, &H409US)
            AssertTrue(manufacturer = "Keir Fraser", "windows_port_discovery.request_usb_string_description.manufacturer")
            AssertTrue(hub.RequestUsbStringDescription(1, 1) = "Keir Fraser",
                       "windows_port_discovery.request_usb_string_description.default_langid")
            AssertTrue(hub.RequestUsbStringDescription(1, 1, &H410US) Is Nothing,
                       "windows_port_discovery.request_usb_string_description.unsupported_langid")
            AssertTrue(hub.RequestUsbStringDescription(1, 0, &H409US) Is Nothing,
                       "windows_port_discovery.request_usb_string_description.index0")
            AssertTrue(hub.RequestUsbStringDescription(0, 1, &H409US) Is Nothing,
                       "windows_port_discovery.request_usb_string_description.invalid_port")

            Dim deviceDesc = hub.RequestUsbDeviceDescription(1)
            AssertTrue(deviceDesc.HasValue, "windows_port_discovery.request_usb_device_description.exists")
            AssertTrue(deviceDesc.Value.IdVendor = &H1209US, "windows_port_discovery.request_usb_device_description.vid")
            AssertTrue(deviceDesc.Value.IdProduct = &H4D69US, "windows_port_discovery.request_usb_device_description.pid")

            Dim configDesc = hub.RequestUsbConfigurationDescription(1, 1)
            AssertTrue(configDesc.HasValue, "windows_port_discovery.request_usb_configuration_description.exists")
            AssertTrue(configDesc.Value.BConfigurationValue = 1, "windows_port_discovery.request_usb_configuration_description.value")

            Dim iface = hub.RequestUsbInterfaceDescriptions(1, configDesc.Value, 0)
            AssertTrue(iface IsNot Nothing AndAlso iface.Item2.HasValue,
                       "windows_port_discovery.request_usb_interface_descriptions.exists")
            AssertTrue(iface.Item2.Value.IInterface = 5, "windows_port_discovery.request_usb_interface_descriptions.iinterface")
            Dim ifaceAlt = hub.RequestUsbInterfaceDescriptions(1, configDesc.Value, 0, bAlternateSetting:=1)
            AssertTrue(ifaceAlt IsNot Nothing AndAlso Not ifaceAlt.Item1.HasValue AndAlso Not ifaceAlt.Item2.HasValue,
                       "windows_port_discovery.request_usb_interface_descriptions.alt_setting")

            Dim conn = hub.RequestUsbConnectionInfo(1)
            AssertTrue(conn.HasValue, "windows_port_discovery.request_usb_connection_info.exists")
            AssertTrue(conn.Value.DeviceDescriptor.IdVendor = &H1209US,
                       "windows_port_discovery.request_usb_connection_info.vid")

            Dim registry As New DeviceRegistry(cacheUsbInfo:=False)
            Dim locNode As New DeviceNode("USB\VID_1209&PID_4D69\GW1234")
            locNode.LocationPaths = New List(Of String) From {"#USB(1)#USB(4)"}
            AssertTrue(registry.GetLocationString(locNode, bInterfaceNumber:=2) = "0-1.4:x.2",
                       "windows_port_discovery.get_location_string.interface_default_cfg")
            AssertTrue(registry.GetLocationString(locNode, bConfigurationValue:=3, bInterfaceNumber:=2) = "0-1.4:3.2",
                       "windows_port_discovery.get_location_string.interface_with_cfg")
            AssertTrue(registry.GetLocationString(New DeviceNode("USB\VID_1209&PID_4D69\GW0000")) Is Nothing,
                       "windows_port_discovery.get_location_string.no_location_path")
            Dim syntheticHost As New UsbHostControllerDevice With {.InstanceHandle = 1UI}
            Dim syntheticHub As New UsbHubDevice With {.InstanceHandle = 2UI, .Parent = syntheticHost}
            Dim chainNode As New DeviceNode("USB\VID_1209&PID_4D69\GWCHAIN")
            chainNode.Parent = syntheticHub
            chainNode.LocationPaths = New List(Of String) From {"#USB(1)#USB(4)"}
            AssertTrue(registry.GetBusNumber(chainNode) = 1, "windows_port_discovery.get_bus_number.chain_to_host")
            AssertTrue(registry.GetLocationString(chainNode, bConfigurationValue:=1, bInterfaceNumber:=0) = "1-1.4:1.0",
                       "windows_port_discovery.get_location_string.chain_host_bus")
            Dim chain = registry.FindParentChain(chainNode)
            AssertTrue(chain IsNot Nothing, "windows_port_discovery.find_parent_chain.exists")
            AssertTrue(chain(0).InstanceHandle = 1UI, "windows_port_discovery.find_parent_chain.host_handle")
            AssertTrue(chain(1).InstanceHandle = 2UI, "windows_port_discovery.find_parent_chain.hub_handle")
            Dim longIdNode As New DeviceNode(New String("A"c, 4096))
            AssertTrue(longIdNode.InstanceHandle <> 0UI,
                       "windows_port_discovery.device_node.long_instance_identifier_handle")

            hub.Close()
            AssertTrue(Not hub.IsOpen, "windows_port_discovery.close")
        End Sub

        Private Sub RunCodecParity(path As String)
            Dim root = LoadJson(Of CodecFixtureRoot)(path)
            Dim diskDefsPath = FindDiskDefsPath()
            Dim disk = DiskDefParser.GetDiskdef("ibm.1440", diskDefsPath)
            AssertTrue(disk IsNot Nothing, "codec.get_diskdef(ibm.1440) returned nothing")
            AssertTrue(disk.Cyls.GetValueOrDefault() = root.DiskdefIbm1440.Cyls, "codec.diskdef.cyls mismatch")
            AssertTrue(disk.Heads.GetValueOrDefault() = root.DiskdefIbm1440.Heads, "codec.diskdef.heads mismatch")
            AssertTrue(disk.Trackset() = root.DiskdefIbm1440.Trackset, "codec.diskdef.trackset mismatch")
            AssertNear(disk.DefaultRevs, root.DiskdefIbm1440.DefaultRevs, 0.0001, "codec.diskdef.default_revs mismatch")
            AssertTrue(disk.TrackMap.Count = root.DiskdefIbm1440.TrackMapSize, "codec.diskdef.track_map_size mismatch")

            Dim formats = DiskDefParser.GetAllFormats(diskDefsPath)
            AssertTrue(formats.Count = root.Formats.Count, "codec.formats.count mismatch")
            AssertTrue(formats.Take(10).SequenceEqual(root.Formats.First10), "codec.formats.first10 mismatch")
            AssertTrue(formats.Skip(Math.Max(0, formats.Count - 10)).SequenceEqual(root.Formats.Last10), "codec.formats.last10 mismatch")

            Dim printed = DiskDefParser.PrintFormats(formats)
            Dim digest As String
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(printed))).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.Formats.PrintSha256, "codec.print_formats sha256 mismatch")

            For Each formatName In formats
                Try
                    Dim def = DiskDefParser.GetDiskdef(formatName, diskDefsPath)
                    If def Is Nothing Then
                        Continue For
                    End If

                    Dim t = def.MkTrack(0, 0)
                    If t Is Nothing Then
                        Continue For
                    End If

                    AssertTrue(Not String.IsNullOrEmpty(t.SummaryString()),
                               String.Format("codec.all_formats({0}) summary is empty", formatName))
                Catch ex As FatalException
                    ' Some aliases include constrained track ranges that are intentionally
                    ' not instantiable at c=0/h=0; skip those here.
                    Continue For
                End Try
            Next

            Dim nonPlaceholderChecks As New List(Of Tuple(Of String, Integer, Integer)) From {
                Tuple.Create("apple2.nofs.140", 0, 0),
                Tuple.Create("amiga.amigados", 0, 0),
                Tuple.Create("amiga.amigados_hd", 0, 0),
                Tuple.Create("mac.400", 0, 0),
                Tuple.Create("mac.800", 0, 0),
                Tuple.Create("commodore.1541", 0, 0),
                Tuple.Create("northstar.fm.ss", 0, 0),
                Tuple.Create("northstar.mfm.ss", 0, 0),
                Tuple.Create("micropolis.48tpi.ss", 0, 0),
                Tuple.Create("micropolis.48tpi.ss.275", 0, 0),
                Tuple.Create("hp.mmfm.9885", 0, 0),
                Tuple.Create("hp.mmfm.9895", 0, 1),
                Tuple.Create("datageneral.2f", 0, 0),
                Tuple.Create("ibm.scan", 0, 0),
                Tuple.Create("raw.125", 0, 0),
                Tuple.Create("raw.250", 0, 0),
                Tuple.Create("raw.500", 0, 0)
            }
            For Each item In nonPlaceholderChecks
                Dim def = DiskDefParser.GetDiskdef(item.Item1, diskDefsPath)
                AssertTrue(def IsNot Nothing, String.Format("codec.non_placeholder({0}) diskdef missing", item.Item1))
                Dim t = def.MkTrack(item.Item2, item.Item3)
                AssertTrue(t IsNot Nothing, String.Format("codec.non_placeholder({0}) track missing", item.Item1))
                AssertTrue(Not String.IsNullOrEmpty(t.SummaryString()),
                           String.Format("codec.non_placeholder({0}) summary is empty", item.Item1))
            Next

            Dim track = disk.MkTrack(0, 0)
            AssertTrue(track IsNot Nothing, "codec.diskdef.create_track returned nothing")
            Dim source = Enumerable.Range(0, 10000).Select(Function(i) CByte((i * 7) And &HFF)).ToArray()
            Dim consumed = track.SetImgTrack(source)
            AssertTrue(consumed = root.IbmTrackImgCases.Consumed, "codec.ibm_track_img_cases.consumed mismatch")
            Dim img = track.GetImgTrack()
            AssertTrue(img.Length = root.IbmTrackImgCases.ImgLen, "codec.ibm_track_img_cases.img_len mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(img)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmTrackImgCases.ImgSha256, "codec.ibm_track_img_cases.img_sha256 mismatch")
            AssertTrue(track.NrMissing() = root.IbmTrackImgCases.Missing, "codec.ibm_track_img_cases.missing mismatch")
            AssertTrue(track.HasSec(0) = root.IbmTrackImgCases.HasSec0, "codec.ibm_track_img_cases.has_sec0 mismatch")
            AssertTrue(track.HasSec(Math.Max(track.Nsec - 1, 0)) = root.IbmTrackImgCases.HasLast, "codec.ibm_track_img_cases.has_last mismatch")

            Dim trackShort = disk.MkTrack(0, 0)
            AssertTrue(trackShort IsNot Nothing, "codec.ibm_track_img_cases.short_create_track returned nothing")
            Dim consumedShort = trackShort.SetImgTrack(New Byte() {1, 2})
            AssertTrue(consumedShort = root.IbmTrackImgCases.ConsumedShort, "codec.ibm_track_img_cases.consumed_short mismatch")
            Dim imgShort = trackShort.GetImgTrack()
            AssertTrue(imgShort.Length = root.IbmTrackImgCases.ImgShortLen, "codec.ibm_track_img_cases.img_short_len mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(imgShort)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmTrackImgCases.ImgShortSha256, "codec.ibm_track_img_cases.img_short_sha256 mismatch")
            Dim prefix = BitConverter.ToString(imgShort.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(prefix = root.IbmTrackImgCases.ImgShortPrefixHex, "codec.ibm_track_img_cases.img_short_prefix_hex mismatch")
            AssertTrue(trackShort.NrMissing() = root.IbmTrackImgCases.MissingShort, "codec.ibm_track_img_cases.missing_short mismatch")

            Dim decodeFlux = New Flux(root.IbmDecodeCase.IndexList, root.IbmDecodeCase.FluxList, root.IbmDecodeCase.SampleFreq, True)
            Dim decodedTrack = disk.MkTrack(0, 0)
            AssertTrue(decodedTrack IsNot Nothing, "codec.ibm_decode_case.create_track returned nothing")
            decodedTrack.DecodeFlux(decodeFlux)
            AssertTrue(decodedTrack.NrMissing() = root.IbmDecodeCase.DecodedMissing, "codec.ibm_decode_case.decoded_missing mismatch")
            Dim decodedImg = decodedTrack.GetImgTrack()
            AssertTrue(decodedImg.Length = root.IbmDecodeCase.DecodedImgLen, "codec.ibm_decode_case.decoded_img_len mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(decodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmDecodeCase.DecodedImgSha256, "codec.ibm_decode_case.decoded_img_sha256 mismatch")
            Dim decodedPrefix = BitConverter.ToString(decodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(decodedPrefix = root.IbmDecodeCase.DecodedImgPrefixHex, "codec.ibm_decode_case.decoded_img_prefix_hex mismatch")

            Dim emitTrack = disk.MkTrack(0, 0)
            AssertTrue(emitTrack IsNot Nothing, "codec.ibm_emit_case.create_track returned nothing")
            Dim emitData = Enumerable.Range(0, emitTrack.Nsec * 512).Select(Function(i) CByte((i * 13 + 9) And &HFF)).ToArray()
            Dim consumedEmit = emitTrack.SetImgTrack(emitData)
            AssertTrue(consumedEmit = emitData.Length, "codec.ibm_emit_case.set_image_track consumed mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(emitData)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmEmitCase.SourceImgSha256, "codec.ibm_emit_case.source_img_sha256 mismatch")

            Dim emitFlux = emitTrack.Flux()
            Dim emitDecoded = disk.MkTrack(0, 0)
            AssertTrue(emitDecoded IsNot Nothing, "codec.ibm_emit_case.emit_decode_create_track returned nothing")
            emitDecoded.DecodeFlux(emitFlux)
            AssertTrue(emitDecoded.NrMissing() = root.IbmEmitCase.DecodedMissing, "codec.ibm_emit_case.decoded_missing mismatch")
            Dim emitDecodedImg = emitDecoded.GetImgTrack()
            Dim emitPrefix = BitConverter.ToString(emitDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(emitPrefix = root.IbmEmitCase.DecodedImgPrefixHex, "codec.ibm_emit_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(emitDecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmEmitCase.DecodedImgSha256, "codec.ibm_emit_case.decoded_img_sha256 mismatch")

            Dim bitcellDisk = DiskDefParser.GetDiskdef("raw.250", diskDefsPath)
            AssertTrue(bitcellDisk IsNot Nothing, "codec.bitcell_case.diskdef returned nothing")
            Dim bitcellTrack = bitcellDisk.MkTrack(0, 0)
            AssertTrue(bitcellTrack IsNot Nothing, "codec.bitcell_case.create_track returned nothing")
            bitcellTrack.DecodeFlux(emitFlux)
            Dim bitcellSummary = bitcellTrack.SummaryString()
            AssertTrue(bitcellSummary.StartsWith("Raw Bitcell (", StringComparison.Ordinal), "codec.bitcell_case.decoded_summary mismatch")
            Dim bitcellMaster = bitcellTrack.MasterTrack()
            AssertNear(bitcellMaster.TimePerRev, root.BitcellCase.DecodedTimePerRev, 0.000001, "codec.bitcell_case.decoded_time_per_rev mismatch")

            Dim bitcellEmpty = bitcellDisk.MkTrack(0, 0)
            AssertTrue(bitcellEmpty IsNot Nothing, "codec.bitcell_case.empty_create_track returned nothing")
            AssertTrue(bitcellEmpty.SummaryString() = root.BitcellCase.EmptySummary, "codec.bitcell_case.empty_summary mismatch")
            Dim bitcellEmptyMaster = bitcellEmpty.MasterTrack()
            AssertTrue(bitcellEmptyMaster.Bits.Count = root.BitcellCase.EmptyBitCount, "codec.bitcell_case.empty_bit_count mismatch")
            AssertTrue(bitcellEmptyMaster.WeakRanges.Count = root.BitcellCase.EmptyWeakCount, "codec.bitcell_case.empty_weak_count mismatch")

            Dim bitcell125Disk = DiskDefParser.GetDiskdef("raw.125", diskDefsPath)
            AssertTrue(bitcell125Disk IsNot Nothing, "codec.bitcell_125_case.diskdef returned nothing")
            Dim bitcell125Track = bitcell125Disk.MkTrack(0, 0)
            AssertTrue(bitcell125Track IsNot Nothing, "codec.bitcell_125_case.create_track returned nothing")
            bitcell125Track.DecodeFlux(emitFlux)
            AssertTrue(bitcell125Track.SummaryString().StartsWith("Raw Bitcell (", StringComparison.Ordinal), "codec.bitcell_125_case.decoded_summary mismatch")
            Dim bitcell125Master = bitcell125Track.MasterTrack()
            AssertNear(bitcell125Master.TimePerRev, root.Bitcell125Case.DecodedTimePerRev, 0.000001, "codec.bitcell_125_case.decoded_time_per_rev mismatch")
            Dim bitcell125Empty = bitcell125Disk.MkTrack(0, 0)
            AssertTrue(bitcell125Empty IsNot Nothing, "codec.bitcell_125_case.empty_create_track returned nothing")
            AssertTrue(bitcell125Empty.SummaryString() = root.Bitcell125Case.EmptySummary, "codec.bitcell_125_case.empty_summary mismatch")
            Dim bitcell125EmptyMaster = bitcell125Empty.MasterTrack()
            AssertTrue(bitcell125EmptyMaster.Bits.Count = root.Bitcell125Case.EmptyBitCount, "codec.bitcell_125_case.empty_bit_count mismatch")
            AssertTrue(bitcell125EmptyMaster.WeakRanges.Count = root.Bitcell125Case.EmptyWeakCount, "codec.bitcell_125_case.empty_weak_count mismatch")

            Dim bitcell500Disk = DiskDefParser.GetDiskdef("raw.500", diskDefsPath)
            AssertTrue(bitcell500Disk IsNot Nothing, "codec.bitcell_500_case.diskdef returned nothing")
            Dim bitcell500Track = bitcell500Disk.MkTrack(0, 0)
            AssertTrue(bitcell500Track IsNot Nothing, "codec.bitcell_500_case.create_track returned nothing")
            bitcell500Track.DecodeFlux(emitFlux)
            AssertTrue(bitcell500Track.SummaryString().StartsWith("Raw Bitcell (", StringComparison.Ordinal), "codec.bitcell_500_case.decoded_summary mismatch")
            Dim bitcell500Master = bitcell500Track.MasterTrack()
            AssertNear(bitcell500Master.TimePerRev, root.Bitcell500Case.DecodedTimePerRev, 0.000001, "codec.bitcell_500_case.decoded_time_per_rev mismatch")
            Dim bitcell500Empty = bitcell500Disk.MkTrack(0, 0)
            AssertTrue(bitcell500Empty IsNot Nothing, "codec.bitcell_500_case.empty_create_track returned nothing")
            AssertTrue(bitcell500Empty.SummaryString() = root.Bitcell500Case.EmptySummary, "codec.bitcell_500_case.empty_summary mismatch")
            Dim bitcell500EmptyMaster = bitcell500Empty.MasterTrack()
            AssertTrue(bitcell500EmptyMaster.Bits.Count = root.Bitcell500Case.EmptyBitCount, "codec.bitcell_500_case.empty_bit_count mismatch")
            AssertTrue(bitcell500EmptyMaster.WeakRanges.Count = root.Bitcell500Case.EmptyWeakCount, "codec.bitcell_500_case.empty_weak_count mismatch")

            Dim ibmScanDisk = DiskDefParser.GetDiskdef("ibm.scan", diskDefsPath)
            AssertTrue(ibmScanDisk IsNot Nothing, "codec.ibm_scan_case.diskdef returned nothing")
            Dim ibmScanSourceDisk = DiskDefParser.GetDiskdef("ibm.1440", diskDefsPath)
            AssertTrue(ibmScanSourceDisk IsNot Nothing, "codec.ibm_scan_case.source_diskdef returned nothing")
            Dim ibmScanSourceTrack = ibmScanSourceDisk.MkTrack(0, 0)
            AssertTrue(ibmScanSourceTrack IsNot Nothing, "codec.ibm_scan_case.source_track returned nothing")
            Dim ibmScanSourceData = Enumerable.Range(0, ibmScanSourceTrack.Nsec * 512).Select(Function(i) CByte((i * 61 + 5) And &HFF)).ToArray()
            ibmScanSourceTrack.SetImgTrack(ibmScanSourceData)
            Dim ibmScanFlux = ibmScanSourceTrack.Flux()
            Dim ibmScanDecoded = ibmScanDisk.MkTrack(0, 0)
            AssertTrue(ibmScanDecoded IsNot Nothing, "codec.ibm_scan_case.decode_create_track returned nothing")
            ibmScanDecoded.DecodeFlux(ibmScanFlux)
            AssertTrue(ibmScanDecoded.SummaryString() = root.IbmScanCase.DecodedSummary, "codec.ibm_scan_case.decoded_summary mismatch")
            AssertTrue(ibmScanDecoded.NrMissing() = root.IbmScanCase.DecodedMissing, "codec.ibm_scan_case.decoded_missing mismatch")
            Dim ibmScanDecodedImg = ibmScanDecoded.GetImgTrack()
            Dim ibmScanPrefix = BitConverter.ToString(ibmScanDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(ibmScanPrefix = root.IbmScanCase.DecodedImgPrefixHex, "codec.ibm_scan_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(ibmScanDecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmScanCase.DecodedImgSha256, "codec.ibm_scan_case.decoded_img_sha256 mismatch")
            Dim ibmScanMaster = ibmScanDecoded.MasterTrack()
            AssertTrue(ibmScanMaster IsNot Nothing, "codec.ibm_scan_case.master_track missing")
            AssertTrue(ibmScanMaster.Bits.Count > 0, "codec.ibm_scan_case.master_track empty bits")
            AssertNear(ibmScanMaster.TimePerRev, 0.2, 0.1, "codec.ibm_scan_case.master_track time_per_rev")
            Dim ibmScanWriteError As String = Nothing
            Try
                ibmScanDisk.MkTrack(0, 0).SetImgTrack(New Byte(511) {})
            Catch ex As FatalException
                ibmScanWriteError = ex.Message
            End Try
            AssertTrue(ibmScanWriteError = root.IbmScanCase.WriteError, "codec.ibm_scan_case.write_error mismatch")

            Dim ibmScanFmSourceDisk = DiskDefParser.GetDiskdef("atari.90", diskDefsPath)
            AssertTrue(ibmScanFmSourceDisk IsNot Nothing, "codec.ibm_scan_fm_case.source_diskdef returned nothing")
            Dim ibmScanFmSourceTrack = ibmScanFmSourceDisk.MkTrack(0, 0)
            AssertTrue(ibmScanFmSourceTrack IsNot Nothing, "codec.ibm_scan_fm_case.source_track returned nothing")
            Dim ibmScanFmSourceData = Enumerable.Range(0, ibmScanFmSourceTrack.Nsec * 128).Select(Function(i) CByte((i * 67 + 9) And &HFF)).ToArray()
            ibmScanFmSourceTrack.SetImgTrack(ibmScanFmSourceData)
            Dim ibmScanFmFlux = ibmScanFmSourceTrack.Flux()
            Dim ibmScanFmDecoded = ibmScanDisk.MkTrack(0, 0)
            AssertTrue(ibmScanFmDecoded IsNot Nothing, "codec.ibm_scan_fm_case.decode_create_track returned nothing")
            ibmScanFmDecoded.DecodeFlux(ibmScanFmFlux)
            AssertTrue(ibmScanFmDecoded.SummaryString() = root.IbmScanFmCase.DecodedSummary, "codec.ibm_scan_fm_case.decoded_summary mismatch")
            AssertTrue(ibmScanFmDecoded.NrMissing() = root.IbmScanFmCase.DecodedMissing, "codec.ibm_scan_fm_case.decoded_missing mismatch")
            Dim ibmScanFmDecodedImg = ibmScanFmDecoded.GetImgTrack()
            Dim ibmScanFmPrefix = BitConverter.ToString(ibmScanFmDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(ibmScanFmPrefix = root.IbmScanFmCase.DecodedImgPrefixHex, "codec.ibm_scan_fm_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(ibmScanFmDecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmScanFmCase.DecodedImgSha256, "codec.ibm_scan_fm_case.decoded_img_sha256 mismatch")
            Dim ibmScanFmMaster = ibmScanFmDecoded.MasterTrack()
            AssertTrue(ibmScanFmMaster IsNot Nothing, "codec.ibm_scan_fm_case.master_track missing")
            AssertTrue(ibmScanFmMaster.Bits.Count > 0, "codec.ibm_scan_fm_case.master_track empty bits")
            AssertNear(ibmScanFmMaster.TimePerRev, 0.2, 0.1, "codec.ibm_scan_fm_case.master_track time_per_rev")

            Dim fmDisk = DiskDefParser.GetDiskdef("atari.90", diskDefsPath)
            AssertTrue(fmDisk IsNot Nothing, "codec.ibm_fm_case.diskdef returned nothing")
            Dim fmTrack = fmDisk.MkTrack(0, 0)
            AssertTrue(fmTrack IsNot Nothing, "codec.ibm_fm_case.create_track returned nothing")
            Dim fmData = Enumerable.Range(0, fmTrack.Nsec * 128).Select(Function(i) CByte((i * 5 + 1) And &HFF)).ToArray()
            fmTrack.SetImgTrack(fmData)
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(fmData)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmFmCase.SourceImgSha256, "codec.ibm_fm_case.source_img_sha256 mismatch")
            Dim fmFlux = fmTrack.Flux()
            Dim fmDecoded = fmDisk.MkTrack(0, 0)
            AssertTrue(fmDecoded IsNot Nothing, "codec.ibm_fm_case.decode_create_track returned nothing")
            fmDecoded.DecodeFlux(fmFlux)
            AssertTrue(fmDecoded.NrMissing() = root.IbmFmCase.DecodedMissing, "codec.ibm_fm_case.decoded_missing mismatch")
            Dim fmDecodedImg = fmDecoded.GetImgTrack()
            Dim fmPrefix = BitConverter.ToString(fmDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(fmPrefix = root.IbmFmCase.DecodedImgPrefixHex, "codec.ibm_fm_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(fmDecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.IbmFmCase.DecodedImgSha256, "codec.ibm_fm_case.decoded_img_sha256 mismatch")

            Dim rxDisk = DiskDefParser.GetDiskdef("dec.rx02", diskDefsPath)
            AssertTrue(rxDisk IsNot Nothing, "codec.dec_rx02_case.diskdef returned nothing")
            Dim rxTrack = rxDisk.MkTrack(0, 0)
            AssertTrue(rxTrack IsNot Nothing, "codec.dec_rx02_case.create_track returned nothing")
            Dim rxData = Enumerable.Range(0, rxTrack.Nsec * 256).Select(Function(i) CByte((i * 17 + 7) And &HFF)).ToArray()
            rxTrack.SetImgTrack(rxData)
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(rxData)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.DecRx02Case.SourceImgSha256, "codec.dec_rx02_case.source_img_sha256 mismatch")
            Dim rxFlux = rxTrack.Flux()
            Dim rxDecoded = rxDisk.MkTrack(0, 0)
            AssertTrue(rxDecoded IsNot Nothing, "codec.dec_rx02_case.decode_create_track returned nothing")
            rxDecoded.DecodeFlux(rxFlux)
            AssertTrue(rxDecoded.NrMissing() = root.DecRx02Case.DecodedMissing, "codec.dec_rx02_case.decoded_missing mismatch")
            Dim rxDecodedImg = rxDecoded.GetImgTrack()
            Dim rxPrefix = BitConverter.ToString(rxDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(rxPrefix = root.DecRx02Case.DecodedImgPrefixHex, "codec.dec_rx02_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(rxDecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.DecRx02Case.DecodedImgSha256, "codec.dec_rx02_case.decoded_img_sha256 mismatch")

            Dim ibm360Disk = DiskDefParser.GetDiskdef("ibm.360", diskDefsPath)
            AssertTrue(ibm360Disk IsNot Nothing, "codec.ibm_360_case.diskdef returned nothing")
            Dim ibm360Track = ibm360Disk.MkTrack(0, 0)
            AssertTrue(ibm360Track IsNot Nothing, "codec.ibm_360_case.create_track returned nothing")
            Dim ibm360Data = Enumerable.Range(0, ibm360Track.Nsec * 512).Select(Function(i) CByte((i * 71 + 3) And &HFF)).ToArray()
            ibm360Track.SetImgTrack(ibm360Data)
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(ibm360Data)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.Ibm360Case.SourceImgSha256, "codec.ibm_360_case.source_img_sha256 mismatch")
            Dim ibm360Flux = ibm360Track.Flux()
            Dim ibm360Decoded = ibm360Disk.MkTrack(0, 0)
            AssertTrue(ibm360Decoded IsNot Nothing, "codec.ibm_360_case.decode_create_track returned nothing")
            ibm360Decoded.DecodeFlux(ibm360Flux)
            AssertTrue(ibm360Decoded.NrMissing() = root.Ibm360Case.DecodedMissing, "codec.ibm_360_case.decoded_missing mismatch")
            Dim ibm360DecodedImg = ibm360Decoded.GetImgTrack()
            Dim ibm360Prefix = BitConverter.ToString(ibm360DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(ibm360Prefix = root.Ibm360Case.DecodedImgPrefixHex, "codec.ibm_360_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(ibm360DecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.Ibm360Case.DecodedImgSha256, "codec.ibm_360_case.decoded_img_sha256 mismatch")

            Dim ibm1200Disk = DiskDefParser.GetDiskdef("ibm.1200", diskDefsPath)
            AssertTrue(ibm1200Disk IsNot Nothing, "codec.ibm_1200_case.diskdef returned nothing")
            Dim ibm1200Track = ibm1200Disk.MkTrack(0, 0)
            AssertTrue(ibm1200Track IsNot Nothing, "codec.ibm_1200_case.create_track returned nothing")
            Dim ibm1200Data = Enumerable.Range(0, ibm1200Track.Nsec * 512).Select(Function(i) CByte((i * 73 + 5) And &HFF)).ToArray()
            ibm1200Track.SetImgTrack(ibm1200Data)
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(ibm1200Data)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.Ibm1200Case.SourceImgSha256, "codec.ibm_1200_case.source_img_sha256 mismatch")
            Dim ibm1200Flux = ibm1200Track.Flux()
            Dim ibm1200Decoded = ibm1200Disk.MkTrack(0, 0)
            AssertTrue(ibm1200Decoded IsNot Nothing, "codec.ibm_1200_case.decode_create_track returned nothing")
            ibm1200Decoded.DecodeFlux(ibm1200Flux)
            AssertTrue(ibm1200Decoded.NrMissing() = root.Ibm1200Case.DecodedMissing, "codec.ibm_1200_case.decoded_missing mismatch")
            Dim ibm1200DecodedImg = ibm1200Decoded.GetImgTrack()
            Dim ibm1200Prefix = BitConverter.ToString(ibm1200DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(ibm1200Prefix = root.Ibm1200Case.DecodedImgPrefixHex, "codec.ibm_1200_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(ibm1200DecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.Ibm1200Case.DecodedImgSha256, "codec.ibm_1200_case.decoded_img_sha256 mismatch")

            Dim amigaDisk = DiskDefParser.GetDiskdef("amiga.amigados", diskDefsPath)
            AssertTrue(amigaDisk IsNot Nothing, "codec.amiga_case.diskdef returned nothing")
            Dim amigaTrack = amigaDisk.MkTrack(0, 0)
            AssertTrue(amigaTrack IsNot Nothing, "codec.amiga_case.create_track returned nothing")
            Dim amigaData = Enumerable.Range(0, amigaTrack.Nsec * 512).Select(Function(i) CByte((i * 19 + 5) And &HFF)).ToArray()
            amigaTrack.SetImgTrack(amigaData)
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(amigaData)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.AmigaCase.SourceImgSha256, "codec.amiga_case.source_img_sha256 mismatch")
            Dim amigaFlux = amigaTrack.Flux()
            Dim amigaDecoded = amigaDisk.MkTrack(0, 0)
            AssertTrue(amigaDecoded IsNot Nothing, "codec.amiga_case.decode_create_track returned nothing")
            amigaDecoded.DecodeFlux(amigaFlux)
            AssertTrue(amigaDecoded.NrMissing() = root.AmigaCase.DecodedMissing, "codec.amiga_case.decoded_missing mismatch")
            Dim amigaDecodedImg = amigaDecoded.GetImgTrack()
            Dim amigaPrefix = BitConverter.ToString(amigaDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(amigaPrefix = root.AmigaCase.DecodedImgPrefixHex, "codec.amiga_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(amigaDecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.AmigaCase.DecodedImgSha256, "codec.amiga_case.decoded_img_sha256 mismatch")

            Dim amigaHead1Track = amigaDisk.MkTrack(0, 1)
            AssertTrue(amigaHead1Track IsNot Nothing, "codec.amiga_head1_case.create_track returned nothing")
            Dim amigaHead1Data = Enumerable.Range(0, amigaHead1Track.Nsec * 512).Select(Function(i) CByte((i * 73 + 19) And &HFF)).ToArray()
            amigaHead1Track.SetImgTrack(amigaHead1Data)
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(amigaHead1Data)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.AmigaHead1Case.SourceImgSha256, "codec.amiga_head1_case.source_img_sha256 mismatch")
            Dim amigaHead1Flux = amigaHead1Track.Flux()
            Dim amigaHead1Decoded = amigaDisk.MkTrack(0, 1)
            AssertTrue(amigaHead1Decoded IsNot Nothing, "codec.amiga_head1_case.decode_create_track returned nothing")
            amigaHead1Decoded.DecodeFlux(amigaHead1Flux)
            AssertTrue(amigaHead1Decoded.NrMissing() = root.AmigaHead1Case.DecodedMissing, "codec.amiga_head1_case.decoded_missing mismatch")
            Dim amigaHead1DecodedImg = amigaHead1Decoded.GetImgTrack()
            Dim amigaHead1Prefix = BitConverter.ToString(amigaHead1DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(amigaHead1Prefix = root.AmigaHead1Case.DecodedImgPrefixHex, "codec.amiga_head1_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(amigaHead1DecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.AmigaHead1Case.DecodedImgSha256, "codec.amiga_head1_case.decoded_img_sha256 mismatch")

            Dim amigaHdDisk = DiskDefParser.GetDiskdef("amiga.amigados_hd", diskDefsPath)
            AssertTrue(amigaHdDisk IsNot Nothing, "codec.amiga_hd_case.diskdef returned nothing")
            Dim amigaHdTrack = amigaHdDisk.MkTrack(0, 0)
            AssertTrue(amigaHdTrack IsNot Nothing, "codec.amiga_hd_case.create_track returned nothing")
            Dim amigaHdData = Enumerable.Range(0, amigaHdTrack.Nsec * 512).Select(Function(i) CByte((i * 83 + 27) And &HFF)).ToArray()
            amigaHdTrack.SetImgTrack(amigaHdData)
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(amigaHdData)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.AmigaHdCase.SourceImgSha256, "codec.amiga_hd_case.source_img_sha256 mismatch")
            Dim amigaHdFlux = amigaHdTrack.Flux()
            Dim amigaHdDecoded = amigaHdDisk.MkTrack(0, 0)
            AssertTrue(amigaHdDecoded IsNot Nothing, "codec.amiga_hd_case.decode_create_track returned nothing")
            amigaHdDecoded.DecodeFlux(amigaHdFlux)
            AssertTrue(amigaHdDecoded.NrMissing() = root.AmigaHdCase.DecodedMissing, "codec.amiga_hd_case.decoded_missing mismatch")
            Dim amigaHdDecodedImg = amigaHdDecoded.GetImgTrack()
            Dim amigaHdPrefix = BitConverter.ToString(amigaHdDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
            AssertTrue(amigaHdPrefix = root.AmigaHdCase.DecodedImgPrefixHex, "codec.amiga_hd_case.decoded_img_prefix_hex mismatch")
            Using sha = SHA256.Create()
                digest = BitConverter.ToString(sha.ComputeHash(amigaHdDecodedImg)).Replace("-", "").ToLowerInvariant()
            End Using
            AssertTrue(digest = root.AmigaHdCase.DecodedImgSha256, "codec.amiga_hd_case.decoded_img_sha256 mismatch")

            If root.C64Case IsNot Nothing Then
                Dim c64Disk = DiskDefParser.GetDiskdef("commodore.1541", diskDefsPath)
                AssertTrue(c64Disk IsNot Nothing, "codec.c64_case.diskdef returned nothing")
                Dim c64Track = c64Disk.MkTrack(0, 0)
                AssertTrue(c64Track IsNot Nothing, "codec.c64_case.create_track returned nothing")
                Dim c64Data = Enumerable.Range(0, c64Track.Nsec * 256).Select(Function(i) CByte((i * 97 + 43) And &HFF)).ToArray()
                c64Track.SetImgTrack(c64Data)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(c64Data)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.C64Case.SourceImgSha256, "codec.c64_case.source_img_sha256 mismatch")
                Dim c64Flux = c64Track.Flux()
                Dim c64Decoded = c64Disk.MkTrack(0, 0)
                AssertTrue(c64Decoded IsNot Nothing, "codec.c64_case.decode_create_track returned nothing")
                c64Decoded.DecodeFlux(c64Flux)
                AssertTrue(c64Decoded.NrMissing() = root.C64Case.DecodedMissing, "codec.c64_case.decoded_missing mismatch")
                Dim c64DecodedImg = c64Decoded.GetImgTrack()
                Dim c64Prefix = BitConverter.ToString(c64DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(c64Prefix = root.C64Case.DecodedImgPrefixHex, "codec.c64_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(c64DecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.C64Case.DecodedImgSha256, "codec.c64_case.decoded_img_sha256 mismatch")
            End If

            If root.MacCase IsNot Nothing Then
                Dim macDisk = DiskDefParser.GetDiskdef("mac.800", diskDefsPath)
                AssertTrue(macDisk IsNot Nothing, "codec.mac_case.diskdef returned nothing")
                Dim macTrack = macDisk.MkTrack(0, 0)
                AssertTrue(macTrack IsNot Nothing, "codec.mac_case.create_track returned nothing")
                Dim macData = Enumerable.Range(0, macTrack.Nsec * 512).Select(Function(i) CByte((i * 23 + 11) And &HFF)).ToArray()
                macTrack.SetImgTrack(macData)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(macData)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.MacCase.SourceImgSha256, "codec.mac_case.source_img_sha256 mismatch")
                Dim macFlux = macTrack.Flux()
                Dim macDecoded = macDisk.MkTrack(0, 0)
                AssertTrue(macDecoded IsNot Nothing, "codec.mac_case.decode_create_track returned nothing")
                macDecoded.DecodeFlux(macFlux)
                AssertTrue(macDecoded.NrMissing() = root.MacCase.DecodedMissing, "codec.mac_case.decoded_missing mismatch")
                Dim macDecodedImg = macDecoded.GetImgTrack()
                Dim macPrefix = BitConverter.ToString(macDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(macPrefix = root.MacCase.DecodedImgPrefixHex, "codec.mac_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(macDecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.MacCase.DecodedImgSha256, "codec.mac_case.decoded_img_sha256 mismatch")

                If root.MacHead1Case IsNot Nothing Then
                    Dim macHead1Track = macDisk.MkTrack(0, 1)
                    AssertTrue(macHead1Track IsNot Nothing, "codec.mac_head1_case.create_track returned nothing")
                    Dim macHead1Data = Enumerable.Range(0, macHead1Track.Nsec * 512).Select(Function(i) CByte((i * 79 + 17) And &HFF)).ToArray()
                    macHead1Track.SetImgTrack(macHead1Data)
                    Using sha = SHA256.Create()
                        digest = BitConverter.ToString(sha.ComputeHash(macHead1Data)).Replace("-", "").ToLowerInvariant()
                    End Using
                    AssertTrue(digest = root.MacHead1Case.SourceImgSha256, "codec.mac_head1_case.source_img_sha256 mismatch")
                    Dim macHead1Flux = macHead1Track.Flux()
                    Dim macHead1Decoded = macDisk.MkTrack(0, 1)
                    AssertTrue(macHead1Decoded IsNot Nothing, "codec.mac_head1_case.decode_create_track returned nothing")
                    macHead1Decoded.DecodeFlux(macHead1Flux)
                    AssertTrue(macHead1Decoded.NrMissing() = root.MacHead1Case.DecodedMissing, "codec.mac_head1_case.decoded_missing mismatch")
                    Dim macHead1DecodedImg = macHead1Decoded.GetImgTrack()
                    Dim macHead1Prefix = BitConverter.ToString(macHead1DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                    AssertTrue(macHead1Prefix = root.MacHead1Case.DecodedImgPrefixHex, "codec.mac_head1_case.decoded_img_prefix_hex mismatch")
                    Using sha = SHA256.Create()
                        digest = BitConverter.ToString(sha.ComputeHash(macHead1DecodedImg)).Replace("-", "").ToLowerInvariant()
                    End Using
                    AssertTrue(digest = root.MacHead1Case.DecodedImgSha256, "codec.mac_head1_case.decoded_img_sha256 mismatch")
                End If

                If root.Mac400Case IsNot Nothing Then
                    Dim mac400Disk = DiskDefParser.GetDiskdef("mac.400", diskDefsPath)
                    AssertTrue(mac400Disk IsNot Nothing, "codec.mac_400_case.diskdef returned nothing")
                    Dim mac400Track = mac400Disk.MkTrack(0, 0)
                    AssertTrue(mac400Track IsNot Nothing, "codec.mac_400_case.create_track returned nothing")
                    Dim mac400Data = Enumerable.Range(0, mac400Track.Nsec * 512).Select(Function(i) CByte((i * 89 + 31) And &HFF)).ToArray()
                    mac400Track.SetImgTrack(mac400Data)
                    Using sha = SHA256.Create()
                        digest = BitConverter.ToString(sha.ComputeHash(mac400Data)).Replace("-", "").ToLowerInvariant()
                    End Using
                    AssertTrue(digest = root.Mac400Case.SourceImgSha256, "codec.mac_400_case.source_img_sha256 mismatch")
                    Dim mac400Flux = mac400Track.Flux()
                    Dim mac400Decoded = mac400Disk.MkTrack(0, 0)
                    AssertTrue(mac400Decoded IsNot Nothing, "codec.mac_400_case.decode_create_track returned nothing")
                    mac400Decoded.DecodeFlux(mac400Flux)
                    AssertTrue(mac400Decoded.NrMissing() = root.Mac400Case.DecodedMissing, "codec.mac_400_case.decoded_missing mismatch")
                    Dim mac400DecodedImg = mac400Decoded.GetImgTrack()
                    Dim mac400Prefix = BitConverter.ToString(mac400DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                    AssertTrue(mac400Prefix = root.Mac400Case.DecodedImgPrefixHex, "codec.mac_400_case.decoded_img_prefix_hex mismatch")
                    Using sha = SHA256.Create()
                        digest = BitConverter.ToString(sha.ComputeHash(mac400DecodedImg)).Replace("-", "").ToLowerInvariant()
                    End Using
                    AssertTrue(digest = root.Mac400Case.DecodedImgSha256, "codec.mac_400_case.decoded_img_sha256 mismatch")
                End If
            End If

            If root.Apple2Case IsNot Nothing Then
                Dim apple2Disk = DiskDefParser.GetDiskdef("apple2.nofs.140", diskDefsPath)
                AssertTrue(apple2Disk IsNot Nothing, "codec.apple2_case.diskdef returned nothing")
                Dim apple2Track = apple2Disk.MkTrack(0, 0)
                AssertTrue(apple2Track IsNot Nothing, "codec.apple2_case.create_track returned nothing")
                Dim apple2Data = Enumerable.Range(0, apple2Track.Nsec * 256).Select(Function(i) CByte((i * 29 + 7) And &HFF)).ToArray()
                apple2Track.SetImgTrack(apple2Data)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(apple2Data)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.Apple2Case.SourceImgSha256, "codec.apple2_case.source_img_sha256 mismatch")
                Dim apple2Flux = apple2Track.Flux()
                Dim apple2Decoded = apple2Disk.MkTrack(0, 0)
                AssertTrue(apple2Decoded IsNot Nothing, "codec.apple2_case.decode_create_track returned nothing")
                apple2Decoded.DecodeFlux(apple2Flux)
                AssertTrue(apple2Decoded.NrMissing() = root.Apple2Case.DecodedMissing, "codec.apple2_case.decoded_missing mismatch")
                Dim apple2DecodedImg = apple2Decoded.GetImgTrack()
                Dim apple2Prefix = BitConverter.ToString(apple2DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(apple2Prefix = root.Apple2Case.DecodedImgPrefixHex, "codec.apple2_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(apple2DecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.Apple2Case.DecodedImgSha256, "codec.apple2_case.decoded_img_sha256 mismatch")
            End If

            If root.NorthstarCase IsNot Nothing Then
                Dim northstarDisk = DiskDefParser.GetDiskdef("northstar.fm.ss", diskDefsPath)
                AssertTrue(northstarDisk IsNot Nothing, "codec.northstar_case.diskdef returned nothing")
                Dim northstarTrack = northstarDisk.MkTrack(0, 0)
                AssertTrue(northstarTrack IsNot Nothing, "codec.northstar_case.create_track returned nothing")
                Dim northstarData = Enumerable.Range(0, northstarTrack.Nsec * 256).Select(Function(i) CByte((i * 31 + 3) And &HFF)).ToArray()
                northstarTrack.SetImgTrack(northstarData)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(northstarData)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.NorthstarCase.SourceImgSha256, "codec.northstar_case.source_img_sha256 mismatch")
                Dim northstarFlux = northstarTrack.Flux()
                Dim northstarDecoded = northstarDisk.MkTrack(0, 0)
                AssertTrue(northstarDecoded IsNot Nothing, "codec.northstar_case.decode_create_track returned nothing")
                northstarDecoded.DecodeFlux(northstarFlux)
                AssertTrue(northstarDecoded.NrMissing() = root.NorthstarCase.DecodedMissing, "codec.northstar_case.decoded_missing mismatch")
                Dim northstarDecodedImg = northstarDecoded.GetImgTrack()
                Dim northstarPrefix = BitConverter.ToString(northstarDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(northstarPrefix = root.NorthstarCase.DecodedImgPrefixHex, "codec.northstar_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(northstarDecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.NorthstarCase.DecodedImgSha256, "codec.northstar_case.decoded_img_sha256 mismatch")
            End If

            If root.NorthstarMfmCase IsNot Nothing Then
                Dim northstarMfmDisk = DiskDefParser.GetDiskdef("northstar.mfm.ss", diskDefsPath)
                AssertTrue(northstarMfmDisk IsNot Nothing, "codec.northstar_mfm_case.diskdef returned nothing")
                Dim northstarMfmTrack = northstarMfmDisk.MkTrack(0, 0)
                AssertTrue(northstarMfmTrack IsNot Nothing, "codec.northstar_mfm_case.create_track returned nothing")
                Dim northstarMfmData = Enumerable.Range(0, northstarMfmTrack.Nsec * 512).Select(Function(i) CByte((i * 47 + 13) And &HFF)).ToArray()
                northstarMfmTrack.SetImgTrack(northstarMfmData)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(northstarMfmData)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.NorthstarMfmCase.SourceImgSha256, "codec.northstar_mfm_case.source_img_sha256 mismatch")
                Dim northstarMfmFlux = northstarMfmTrack.Flux()
                Dim northstarMfmDecoded = northstarMfmDisk.MkTrack(0, 0)
                AssertTrue(northstarMfmDecoded IsNot Nothing, "codec.northstar_mfm_case.decode_create_track returned nothing")
                northstarMfmDecoded.DecodeFlux(northstarMfmFlux)
                AssertTrue(northstarMfmDecoded.NrMissing() = root.NorthstarMfmCase.DecodedMissing, "codec.northstar_mfm_case.decoded_missing mismatch")
                Dim northstarMfmDecodedImg = northstarMfmDecoded.GetImgTrack()
                Dim northstarMfmPrefix = BitConverter.ToString(northstarMfmDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(northstarMfmPrefix = root.NorthstarMfmCase.DecodedImgPrefixHex, "codec.northstar_mfm_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(northstarMfmDecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.NorthstarMfmCase.DecodedImgSha256, "codec.northstar_mfm_case.decoded_img_sha256 mismatch")
            End If

            If root.MicropolisCase IsNot Nothing Then
                Dim micropolisDisk = DiskDefParser.GetDiskdef("micropolis.48tpi.ss", diskDefsPath)
                AssertTrue(micropolisDisk IsNot Nothing, "codec.micropolis_case.diskdef returned nothing")
                Dim micropolisTrack = micropolisDisk.MkTrack(0, 0)
                AssertTrue(micropolisTrack IsNot Nothing, "codec.micropolis_case.create_track returned nothing")
                Dim micropolisData = Enumerable.Range(0, micropolisTrack.Nsec * 256).Select(Function(i) CByte((i * 37 + 9) And &HFF)).ToArray()
                micropolisTrack.SetImgTrack(micropolisData)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(micropolisData)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.MicropolisCase.SourceImgSha256, "codec.micropolis_case.source_img_sha256 mismatch")
                Dim micropolisFlux = micropolisTrack.Flux()
                Dim micropolisDecoded = micropolisDisk.MkTrack(0, 0)
                AssertTrue(micropolisDecoded IsNot Nothing, "codec.micropolis_case.decode_create_track returned nothing")
                micropolisDecoded.DecodeFlux(micropolisFlux)
                AssertTrue(micropolisDecoded.NrMissing() = root.MicropolisCase.DecodedMissing, "codec.micropolis_case.decoded_missing mismatch")
                Dim micropolisDecodedImg = micropolisDecoded.GetImgTrack()
                Dim micropolisPrefix = BitConverter.ToString(micropolisDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(micropolisPrefix = root.MicropolisCase.DecodedImgPrefixHex, "codec.micropolis_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(micropolisDecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.MicropolisCase.DecodedImgSha256, "codec.micropolis_case.decoded_img_sha256 mismatch")
            End If

            If root.Micropolis275Case IsNot Nothing Then
                Dim micropolis275Disk = DiskDefParser.GetDiskdef("micropolis.48tpi.ss.275", diskDefsPath)
                AssertTrue(micropolis275Disk IsNot Nothing, "codec.micropolis_275_case.diskdef returned nothing")
                Dim micropolis275Track = micropolis275Disk.MkTrack(0, 0)
                AssertTrue(micropolis275Track IsNot Nothing, "codec.micropolis_275_case.create_track returned nothing")
                Dim micropolis275Data = BuildMicropolis275TrackData(micropolis275Track.Nsec, 0)
                micropolis275Track.SetImgTrack(micropolis275Data)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(micropolis275Data)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.Micropolis275Case.SourceImgSha256, "codec.micropolis_275_case.source_img_sha256 mismatch")
                Dim micropolis275Flux = micropolis275Track.Flux()
                Dim micropolis275Decoded = micropolis275Disk.MkTrack(0, 0)
                AssertTrue(micropolis275Decoded IsNot Nothing, "codec.micropolis_275_case.decode_create_track returned nothing")
                micropolis275Decoded.DecodeFlux(micropolis275Flux)
                AssertTrue(micropolis275Decoded.NrMissing() = root.Micropolis275Case.DecodedMissing, "codec.micropolis_275_case.decoded_missing mismatch")
                Dim micropolis275DecodedImg = micropolis275Decoded.GetImgTrack()
                Dim micropolis275Prefix = BitConverter.ToString(micropolis275DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(micropolis275Prefix = root.Micropolis275Case.DecodedImgPrefixHex, "codec.micropolis_275_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(micropolis275DecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.Micropolis275Case.DecodedImgSha256, "codec.micropolis_275_case.decoded_img_sha256 mismatch")
            End If

            AssertTrue(root.HpMmfmCase IsNot Nothing, "codec.hp_mmfm_case fixture missing")
            If root.HpMmfmCase IsNot Nothing Then
                Dim hpMmfmDisk = DiskDefParser.GetDiskdef("hp.mmfm.9885", diskDefsPath)
                AssertTrue(hpMmfmDisk IsNot Nothing, "codec.hp_mmfm_case.diskdef returned nothing")
                Dim hpMmfmTrack = hpMmfmDisk.MkTrack(0, 0)
                AssertTrue(hpMmfmTrack IsNot Nothing, "codec.hp_mmfm_case.create_track returned nothing")
                Dim hpMmfmData = Enumerable.Range(0, hpMmfmTrack.Nsec * 256).Select(Function(i) CByte((i * 41 + 17) And &HFF)).ToArray()
                hpMmfmTrack.SetImgTrack(hpMmfmData)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(hpMmfmData)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.HpMmfmCase.SourceImgSha256, "codec.hp_mmfm_case.source_img_sha256 mismatch")
                Dim hpMmfmFlux = hpMmfmTrack.Flux()
                Dim hpMmfmDecoded = hpMmfmDisk.MkTrack(0, 0)
                AssertTrue(hpMmfmDecoded IsNot Nothing, "codec.hp_mmfm_case.decode_create_track returned nothing")
                hpMmfmDecoded.DecodeFlux(hpMmfmFlux)
                AssertTrue(hpMmfmDecoded.NrMissing() = root.HpMmfmCase.DecodedMissing,
                           String.Format("codec.hp_mmfm_case.decoded_missing mismatch (expected {0}, got {1})",
                                         root.HpMmfmCase.DecodedMissing,
                                         hpMmfmDecoded.NrMissing()))
                Dim hpMmfmDecodedImg = hpMmfmDecoded.GetImgTrack()
                Dim hpMmfmPrefix = BitConverter.ToString(hpMmfmDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(hpMmfmPrefix = root.HpMmfmCase.DecodedImgPrefixHex, "codec.hp_mmfm_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(hpMmfmDecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.HpMmfmCase.DecodedImgSha256, "codec.hp_mmfm_case.decoded_img_sha256 mismatch")
            End If

            If root.HpMmfmHead1Case IsNot Nothing Then
                Dim hpMmfmHead1Disk = DiskDefParser.GetDiskdef("hp.mmfm.9895", diskDefsPath)
                AssertTrue(hpMmfmHead1Disk IsNot Nothing, "codec.hp_mmfm_head1_case.diskdef returned nothing")
                Dim hpMmfmHead1Track = hpMmfmHead1Disk.MkTrack(0, 1)
                AssertTrue(hpMmfmHead1Track IsNot Nothing, "codec.hp_mmfm_head1_case.create_track returned nothing")
                Dim hpMmfmHead1Data = Enumerable.Range(0, hpMmfmHead1Track.Nsec * 256).Select(Function(i) CByte((i * 59 + 7) And &HFF)).ToArray()
                hpMmfmHead1Track.SetImgTrack(hpMmfmHead1Data)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(hpMmfmHead1Data)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.HpMmfmHead1Case.SourceImgSha256, "codec.hp_mmfm_head1_case.source_img_sha256 mismatch")
                Dim hpMmfmHead1Flux = hpMmfmHead1Track.Flux()
                Dim hpMmfmHead1Decoded = hpMmfmHead1Disk.MkTrack(0, 1)
                AssertTrue(hpMmfmHead1Decoded IsNot Nothing, "codec.hp_mmfm_head1_case.decode_create_track returned nothing")
                hpMmfmHead1Decoded.DecodeFlux(hpMmfmHead1Flux)
                AssertTrue(hpMmfmHead1Decoded.NrMissing() = root.HpMmfmHead1Case.DecodedMissing, "codec.hp_mmfm_head1_case.decoded_missing mismatch")
                Dim hpMmfmHead1DecodedImg = hpMmfmHead1Decoded.GetImgTrack()
                Dim hpMmfmHead1Prefix = BitConverter.ToString(hpMmfmHead1DecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(hpMmfmHead1Prefix = root.HpMmfmHead1Case.DecodedImgPrefixHex, "codec.hp_mmfm_head1_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(hpMmfmHead1DecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.HpMmfmHead1Case.DecodedImgSha256, "codec.hp_mmfm_head1_case.decoded_img_sha256 mismatch")
            End If

            If root.DatageneralCase IsNot Nothing Then
                Dim datageneralDisk = DiskDefParser.GetDiskdef("datageneral.2f", diskDefsPath)
                AssertTrue(datageneralDisk IsNot Nothing, "codec.datageneral_case.diskdef returned nothing")
                Dim datageneralTrack = datageneralDisk.MkTrack(0, 0)
                AssertTrue(datageneralTrack IsNot Nothing, "codec.datageneral_case.create_track returned nothing")
                Dim datageneralData = Enumerable.Range(0, datageneralTrack.Nsec * 512).Select(Function(i) CByte((i * 43 + 21) And &HFF)).ToArray()
                datageneralTrack.SetImgTrack(datageneralData)
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(datageneralData)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.DatageneralCase.SourceImgSha256, "codec.datageneral_case.source_img_sha256 mismatch")
                Dim datageneralFlux = datageneralTrack.Flux()
                Dim datageneralDecoded = datageneralDisk.MkTrack(0, 0)
                AssertTrue(datageneralDecoded IsNot Nothing, "codec.datageneral_case.decode_create_track returned nothing")
                datageneralDecoded.DecodeFlux(datageneralFlux)
                AssertTrue(datageneralDecoded.NrMissing() = root.DatageneralCase.DecodedMissing, "codec.datageneral_case.decoded_missing mismatch")
                Dim datageneralDecodedImg = datageneralDecoded.GetImgTrack()
                Dim datageneralPrefix = BitConverter.ToString(datageneralDecodedImg.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                AssertTrue(datageneralPrefix = root.DatageneralCase.DecodedImgPrefixHex, "codec.datageneral_case.decoded_img_prefix_hex mismatch")
                Using sha = SHA256.Create()
                    digest = BitConverter.ToString(sha.ComputeHash(datageneralDecodedImg)).Replace("-", "").ToLowerInvariant()
                End Using
                AssertTrue(digest = root.DatageneralCase.DecodedImgSha256, "codec.datageneral_case.decoded_img_sha256 mismatch")
            End If
        End Sub

        Private Sub RunImageParity(path As String)
            Dim root = LoadJson(Of ImageFixtureRoot)(path)
            Dim diskDefsPath = FindDiskDefsPath()
            Dim opts = New ImageOpts()
            Dim threw = False
            Try
                opts.RSet("x.img", "foo", "bar")
            Catch ex As FatalException
                threw = True
                AssertTrue(ex.Message = root.InvalidOptionMessage, "image.invalid_option_message mismatch")
            End Try
            AssertTrue(threw, "image.invalid_option_message did not throw")

            Dim hfeInvalidThrew = False
            Try
                Dim hfeInvalid As New Hfe()
                hfeInvalid.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"bitrate", "0"}
                })
                Dim hfeCodec = BuildImdFixtureCodec("ibm.mfm", 9, 512, 2, 0.2, 2.0E-6, Function(i) CByte((i * 5) And &HFF))
                hfeInvalid.EmitTrack(0, 0, hfeCodec)
                hfeInvalid.GetImage()
            Catch ex As FatalException
                hfeInvalidThrew = True
                AssertTrue(ex.Message = root.HfeInvalidBitrateMessage, "image.hfe_invalid_bitrate_message mismatch")
            End Try
            AssertTrue(hfeInvalidThrew, "image.hfe_invalid_bitrate_message did not throw")

            Dim hfeInvalidInterfaceThrew = False
            Try
                Dim hfeInvalidInterface As New Hfe()
                hfeInvalidInterface.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"interface", "bad_mode"}
                })
                Dim hfeCodec = BuildImdFixtureCodec("ibm.mfm", 9, 512, 2, 0.2, 2.0E-6, Function(i) CByte((i * 3) And &HFF))
                hfeInvalidInterface.EmitTrack(0, 0, hfeCodec)
                hfeInvalidInterface.GetImage()
            Catch ex As FatalException
                hfeInvalidInterfaceThrew = True
                Dim firstLine = If(ex.Message, "").Split({vbCrLf, vbLf}, StringSplitOptions.None).FirstOrDefault()
                AssertTrue(firstLine = root.HfeInvalidInterfaceMessage, "image.hfe_invalid_interface_message mismatch")
            End Try
            AssertTrue(hfeInvalidInterfaceThrew, "image.hfe_invalid_interface_message did not throw")

            Dim hfeInvalidEncodingThrew = False
            Try
                Dim hfeInvalidEncoding As New Hfe()
                hfeInvalidEncoding.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"encoding", "bad_encoding"}
                })
                Dim hfeCodec = BuildImdFixtureCodec("ibm.mfm", 9, 512, 2, 0.2, 2.0E-6, Function(i) CByte((i * 7) And &HFF))
                hfeInvalidEncoding.EmitTrack(0, 0, hfeCodec)
                hfeInvalidEncoding.GetImage()
            Catch ex As FatalException
                hfeInvalidEncodingThrew = True
                Dim firstLine = If(ex.Message, "").Split({vbCrLf, vbLf}, StringSplitOptions.None).FirstOrDefault()
                AssertTrue(firstLine = root.HfeInvalidEncodingMessage, "image.hfe_invalid_encoding_message mismatch")
            End Try
            AssertTrue(hfeInvalidEncodingThrew, "image.hfe_invalid_encoding_message did not throw")

            Dim hfeInvalidVersionThrew = False
            Try
                Dim hfeInvalidVersion As New Hfe()
                hfeInvalidVersion.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"version", "2"}
                })
                Dim hfeCodec = BuildImdFixtureCodec("ibm.mfm", 9, 512, 2, 0.2, 2.0E-6, Function(i) CByte((i * 11) And &HFF))
                hfeInvalidVersion.EmitTrack(0, 0, hfeCodec)
                hfeInvalidVersion.GetImage()
            Catch ex As FatalException
                hfeInvalidVersionThrew = True
                AssertTrue(ex.Message = root.HfeInvalidVersionMessage, "image.hfe_invalid_version_message mismatch")
            End Try
            AssertTrue(hfeInvalidVersionThrew, "image.hfe_invalid_version_message did not throw")

            Dim hfeFluxRequiresBitrateThrew = False
            Try
                Dim hfeFlux As New Hfe()
                Dim flux As New Flux(New List(Of Double) From {200000.0},
                                     Enumerable.Repeat(1000.0, 200).ToList(),
                                     40000000.0,
                                     indexCued:=True)
                hfeFlux.EmitTrack(0, 0, flux)
            Catch ex As FatalException
                hfeFluxRequiresBitrateThrew = True
                AssertTrue(ex.Message = root.HfeFluxRequiresBitrateMessage, "image.hfe_flux_requires_bitrate_message mismatch")
            End Try
            AssertTrue(hfeFluxRequiresBitrateThrew, "image.hfe_flux_requires_bitrate_message did not throw")

            Dim lengths As New Dictionary(Of Tuple(Of Integer, Integer), Integer) From {
                {Tuple.Create(0, 0), 3},
                {Tuple.Create(0, 1), 3},
                {Tuple.Create(1, 0), 3},
                {Tuple.Create(1, 1), 3}
            }
            Dim inputBytes = HexToBytes(root.ImgCases.InputHex)

            Dim imgDefault As New Img(BuildFixedSizeDiskDefinition(lengths))
            imgDefault.FromBytes(inputBytes)
            For Each m In root.ImgCases.DefaultMapping
                Dim t = CType(imgDefault.GetTrack(m.Cylinder, m.Head), Codec)
                AssertTrue(t IsNot Nothing, String.Format("image.img.default.track({0},{1}) missing", m.Cylinder, m.Head))
                AssertTrue(BytesToHex(t.GetImgTrack()) = m.DataHex,
                           String.Format("image.img.default.track({0},{1}) mismatch", m.Cylinder, m.Head))
            Next
            AssertTrue(BytesToHex(imgDefault.GetImage()) = root.ImgCases.DefaultImageHex, "image.img.default_image_hex mismatch")

            Dim imgSwapped As New Img(BuildFixedSizeDiskDefinition(lengths))
            imgSwapped.SidesSwapped = True
            imgSwapped.FromBytes(inputBytes)
            For Each m In root.ImgCases.SwappedMapping
                Dim t = CType(imgSwapped.GetTrack(m.Cylinder, m.Head), Codec)
                AssertTrue(t IsNot Nothing, String.Format("image.img.swapped.track({0},{1}) missing", m.Cylinder, m.Head))
                AssertTrue(BytesToHex(t.GetImgTrack()) = m.DataHex,
                           String.Format("image.img.swapped.track({0},{1}) mismatch", m.Cylinder, m.Head))
            Next

            Dim imgSequential As New Img(BuildFixedSizeDiskDefinition(lengths))
            imgSequential.Sequential = True
            imgSequential.FromBytes(inputBytes)
            AssertTrue(BytesToHex(imgSequential.GetImage()) = root.ImgCases.SequentialImageHex, "image.img.sequential_image_hex mismatch")

            Dim lengthsMin As New Dictionary(Of Tuple(Of Integer, Integer), Integer)()
            For cyl = 0 To 2
                For head = 0 To 1
                    lengthsMin(Tuple.Create(cyl, head)) = 3
                Next
            Next
            Dim imgMin As New Img(BuildFixedSizeDiskDefinition(lengthsMin, 3, 2))
            imgMin.MinCylinders = 2
            imgMin.FromBytes(New Byte(17) {})
            AssertTrue(imgMin.GetImage().Length = root.ImgCases.MinCylsNoExtendLen, "image.img.min_cyls_no_extend_len mismatch")
            Dim minTrack = CType(imgMin.GetTrack(2, 1), Codec)
            AssertTrue(minTrack IsNot Nothing, "image.img.min_cyls_track missing")
            minTrack.SetImgTrack(New Byte() {1, 0, 0})
            AssertTrue(imgMin.GetImage().Length = root.ImgCases.MinCylsExtendLen, "image.img.min_cyls_extend_len mismatch")

            Dim tmpDir = IO.Path.Combine(IO.Path.GetTempPath(), "gw-vb-raw-parity-" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(tmpDir)
            Try
                Dim rawName = IO.Path.Combine(tmpDir, "rawcase00.0.raw")
                Dim rawImage As New KryoFlux(rawName)
                Dim rawInputFlux = New Flux(root.RawCases.EmitParse.InputIndexList.Select(Function(x) CDbl(x)),
                                            root.RawCases.EmitParse.InputFluxList.Select(Function(x) CDbl(x)),
                                            root.RawCases.EmitParse.InputSampleFreq,
                                            indexCued:=True)
                rawImage.EmitTrack(0, 0, rawInputFlux)
                AssertTrue(File.Exists(rawName), "image.raw.emit_parse.output missing")
                AssertTrue(New FileInfo(rawName).Length > 0, "image.raw.emit_parse.file_size missing")
                Dim rawParsedImage As New KryoFlux(rawName)
                Dim rawParsedTrack = rawParsedImage.GetTrack(0, 0)
                AssertTrue(rawParsedTrack IsNot Nothing, "image.raw.emit_parse.get_track missing")
                Dim rawParsed = rawParsedTrack.Flux()
                AssertSequence(rawParsed.IndexList, root.RawCases.EmitParse.DecodedIndexList, "image.raw.emit_parse.decoded_index_list")
                AssertTrue(rawParsed.List.Count = root.RawCases.EmitParse.DecodedFluxCount, "image.raw.emit_parse.decoded_flux_count mismatch")
                AssertSequence(rawParsed.List.Take(root.RawCases.EmitParse.DecodedFluxPrefix.Count),
                               root.RawCases.EmitParse.DecodedFluxPrefix,
                               "image.raw.emit_parse.decoded_flux_prefix")
                AssertNear(rawParsed.SampleFreq, root.RawCases.EmitParse.DecodedSampleFreq, 0.0001, "image.raw.emit_parse.decoded_sample_freq mismatch")

                Dim raw11Name = IO.Path.Combine(tmpDir, "rawcase11.1.raw")
                Dim raw11 As New KryoFlux(raw11Name)
                Dim raw11InputFlux = New Flux(root.RawCases.MultiTrackEmit.InputIndexList.Select(Function(x) CDbl(x)),
                                              root.RawCases.MultiTrackEmit.InputFluxList.Select(Function(x) CDbl(x)),
                                              root.RawCases.MultiTrackEmit.InputSampleFreq,
                                              indexCued:=True)
                raw11.EmitTrack(root.RawCases.MultiTrackEmit.Cylinder, root.RawCases.MultiTrackEmit.Head, raw11InputFlux)
                AssertTrue(File.Exists(raw11Name), "image.raw.multi_track_emit.output missing")
                AssertTrue(New FileInfo(raw11Name).Length > 0, "image.raw.multi_track_emit.file_size missing")
                Dim raw11ParsedImage As New KryoFlux(raw11Name)
                Dim raw11ParsedTrack = raw11ParsedImage.GetTrack(root.RawCases.MultiTrackEmit.Cylinder, root.RawCases.MultiTrackEmit.Head)
                AssertTrue(raw11ParsedTrack IsNot Nothing, "image.raw.multi_track_emit.get_track missing")
                Dim raw11Parsed = raw11ParsedTrack.Flux()
                AssertSequence(raw11Parsed.IndexList, root.RawCases.MultiTrackEmit.DecodedIndexList, "image.raw.multi_track_emit.decoded_index_list")
                AssertTrue(raw11Parsed.List.Count = root.RawCases.MultiTrackEmit.DecodedFluxCount, "image.raw.multi_track_emit.decoded_flux_count mismatch")
                AssertSequence(raw11Parsed.List.Take(root.RawCases.MultiTrackEmit.DecodedFluxPrefix.Count),
                               root.RawCases.MultiTrackEmit.DecodedFluxPrefix,
                               "image.raw.multi_track_emit.decoded_flux_prefix")
                AssertNear(raw11Parsed.SampleFreq, root.RawCases.MultiTrackEmit.DecodedSampleFreq, 0.0001, "image.raw.multi_track_emit.decoded_sample_freq mismatch")

                Dim rawRevsName = IO.Path.Combine(tmpDir, "rawrevs00.0.raw")
                Dim rawRevs As New KryoFlux(rawRevsName)
                rawRevs.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"revs", root.RawCases.RevsEmitParse.InputRevolutions.Value.ToString(Globalization.CultureInfo.InvariantCulture)}
                })
                Dim rawRevsInputFlux = New Flux(root.RawCases.RevsEmitParse.InputIndexList.Select(Function(x) CDbl(x)),
                                                root.RawCases.RevsEmitParse.InputFluxList.Select(Function(x) CDbl(x)),
                                                root.RawCases.RevsEmitParse.InputSampleFreq,
                                                indexCued:=True)
                rawRevs.EmitTrack(0, 0, rawRevsInputFlux)
                AssertTrue(File.Exists(rawRevsName), "image.raw.revs_emit_parse.output missing")
                AssertTrue(New FileInfo(rawRevsName).Length > 0, "image.raw.revs_emit_parse.file_size missing")
                Dim rawRevsParsedImage As New KryoFlux(rawRevsName)
                Dim rawRevsParsedTrack = rawRevsParsedImage.GetTrack(0, 0)
                AssertTrue(rawRevsParsedTrack IsNot Nothing, "image.raw.revs_emit_parse.get_track missing")
                Dim rawRevsParsed = rawRevsParsedTrack.Flux()
                AssertSequence(rawRevsParsed.IndexList, root.RawCases.RevsEmitParse.DecodedIndexList, "image.raw.revs_emit_parse.decoded_index_list")
                AssertTrue(rawRevsParsed.List.Count = root.RawCases.RevsEmitParse.DecodedFluxCount, "image.raw.revs_emit_parse.decoded_flux_count mismatch")
                AssertSequence(rawRevsParsed.List.Take(root.RawCases.RevsEmitParse.DecodedFluxPrefix.Count),
                               root.RawCases.RevsEmitParse.DecodedFluxPrefix,
                               "image.raw.revs_emit_parse.decoded_flux_prefix")
                AssertNear(rawRevsParsed.SampleFreq, root.RawCases.RevsEmitParse.DecodedSampleFreq, 0.0001, "image.raw.revs_emit_parse.decoded_sample_freq mismatch")

                Dim rawSckName = IO.Path.Combine(tmpDir, "rawsck00.0.raw")
                Dim rawSck As New KryoFlux(rawSckName)
                rawSck.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"sck", root.RawCases.SckEmitParse.InputSampleClock}
                })
                Dim rawSckInputFlux = New Flux(root.RawCases.SckEmitParse.InputIndexList.Select(Function(x) CDbl(x)),
                                               root.RawCases.SckEmitParse.InputFluxList.Select(Function(x) CDbl(x)),
                                               root.RawCases.SckEmitParse.InputSampleFreq,
                                               indexCued:=True)
                rawSck.EmitTrack(0, 0, rawSckInputFlux)
                AssertTrue(File.Exists(rawSckName), "image.raw.sck_emit_parse.output missing")
                AssertTrue(New FileInfo(rawSckName).Length > 0, "image.raw.sck_emit_parse.file_size missing")
                Dim rawSckParsedImage As New KryoFlux(rawSckName)
                Dim rawSckParsedTrack = rawSckParsedImage.GetTrack(0, 0)
                AssertTrue(rawSckParsedTrack IsNot Nothing, "image.raw.sck_emit_parse.get_track missing")
                Dim rawSckParsed = rawSckParsedTrack.Flux()
                AssertSequence(rawSckParsed.IndexList, root.RawCases.SckEmitParse.DecodedIndexList, "image.raw.sck_emit_parse.decoded_index_list")
                AssertTrue(rawSckParsed.List.Count = root.RawCases.SckEmitParse.DecodedFluxCount, "image.raw.sck_emit_parse.decoded_flux_count mismatch")
                AssertSequence(rawSckParsed.List.Take(root.RawCases.SckEmitParse.DecodedFluxPrefix.Count),
                               root.RawCases.SckEmitParse.DecodedFluxPrefix,
                               "image.raw.sck_emit_parse.decoded_flux_prefix")
                AssertNear(rawSckParsed.SampleFreq, root.RawCases.SckEmitParse.DecodedSampleFreq, 0.0001, "image.raw.sck_emit_parse.decoded_sample_freq mismatch")

                Dim d88Mfm As New D88()
                d88Mfm.FromBytes(HexToBytes(root.D88Cases.Mfm.FileHex))
                Dim d88MfmTrack = TryCast(d88Mfm.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((d88MfmTrack IsNot Nothing) = root.D88Cases.Mfm.TrackPresent, "image.d88.mfm.track_present mismatch")
                If d88MfmTrack IsNot Nothing Then
                    AssertTrue(d88MfmTrack.Nsec = root.D88Cases.Mfm.TrackNsec, "image.d88.mfm.track_sector_count mismatch")
                    AssertTrue(d88MfmTrack.NrMissing() = root.D88Cases.Mfm.TrackMissing, "image.d88.mfm.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(d88MfmTrack.GetImgTrack()))
                        AssertTrue(digest = root.D88Cases.Mfm.TrackImgSha256, "image.d88.mfm.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(d88MfmTrack.GetImgTrack().Take(Math.Min(16, d88MfmTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.D88Cases.Mfm.TrackImgPrefixHex, "image.d88.mfm.track_img_prefix_hex mismatch")
                End If
                AssertTrue((d88Mfm.GetTrack(0, 1) IsNot Nothing) = root.D88Cases.Mfm.Track01Present, "image.d88.mfm.track01_present mismatch")

                Dim d88Fm As New D88()
                d88Fm.FromBytes(HexToBytes(root.D88Cases.Fm.FileHex))
                Dim d88FmTrack = TryCast(d88Fm.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((d88FmTrack IsNot Nothing) = root.D88Cases.Fm.TrackPresent, "image.d88.fm.track_present mismatch")
                If d88FmTrack IsNot Nothing Then
                    AssertTrue(d88FmTrack.Nsec = root.D88Cases.Fm.TrackNsec, "image.d88.fm.track_sector_count mismatch")
                    AssertTrue(d88FmTrack.NrMissing() = root.D88Cases.Fm.TrackMissing, "image.d88.fm.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(d88FmTrack.GetImgTrack()))
                        AssertTrue(digest = root.D88Cases.Fm.TrackImgSha256, "image.d88.fm.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(d88FmTrack.GetImgTrack().Take(Math.Min(16, d88FmTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.D88Cases.Fm.TrackImgPrefixHex, "image.d88.fm.track_img_prefix_hex mismatch")
                End If
                AssertTrue((d88Fm.GetTrack(0, 1) IsNot Nothing) = root.D88Cases.Fm.Track01Present, "image.d88.fm.track01_present mismatch")

                Dim d88Multi As New D88()
                d88Multi.ApplyROpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"index", "1"}
                })
                d88Multi.FromBytes(HexToBytes(root.D88Cases.MultiIndex1.FileHex))
                Dim d88MultiTrack = TryCast(d88Multi.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((d88MultiTrack IsNot Nothing) = root.D88Cases.MultiIndex1.TrackPresent, "image.d88.multi_index1.track_present mismatch")
                If d88MultiTrack IsNot Nothing Then
                    AssertTrue(d88MultiTrack.Nsec = root.D88Cases.MultiIndex1.TrackNsec, "image.d88.multi_index1.track_sector_count mismatch")
                    AssertTrue(d88MultiTrack.NrMissing() = root.D88Cases.MultiIndex1.TrackMissing, "image.d88.multi_index1.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(d88MultiTrack.GetImgTrack()))
                        AssertTrue(digest = root.D88Cases.MultiIndex1.TrackImgSha256, "image.d88.multi_index1.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(d88MultiTrack.GetImgTrack().Take(Math.Min(16, d88MultiTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.D88Cases.MultiIndex1.TrackImgPrefixHex, "image.d88.multi_index1.track_img_prefix_hex mismatch")
                End If
                AssertTrue((d88Multi.GetTrack(0, 1) IsNot Nothing) = root.D88Cases.MultiIndex1.Track01Present, "image.d88.multi_index1.track01_present mismatch")

                If root.D88Cases.IndexErrorCases IsNot Nothing Then
                    For Each c In root.D88Cases.IndexErrorCases
                        Dim matched = False
                        Try
                            Dim bad As New D88() With {.FileName = "multi.d88"}
                            bad.ApplyROpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                                {"index", c.Index}
                            })
                            bad.FromBytes(HexToBytes(root.D88Cases.MultiIndex1.FileHex))
                        Catch ex As FatalException
                            If c.Error IsNot Nothing AndAlso c.Error.Contains("No disk with index") Then
                                matched = ex.Message.Contains("No disk with index")
                            Else
                                matched = String.Equals(ex.Message, c.Error, StringComparison.Ordinal)
                            End If
                        End Try
                        AssertTrue(matched, String.Format("image.d88.index_error({0}) mismatch", c.Index))
                    Next
                End If

                Dim d88EmitSource = BuildImdFixtureCodec("ibm.mfm", 18, 512, 2, 0.2, 2.0E-6, Function(i) CByte((i * 19 + 7) And &HFF))
                Dim d88EmitImage As New D88()
                d88EmitImage.EmitTrack(0, 0, d88EmitSource)
                Dim d88EmitBytes = d88EmitImage.GetImage()
                AssertTrue(d88EmitBytes IsNot Nothing AndAlso d88EmitBytes.Length > 0, "image.d88.emit.image_bytes missing")
                Dim d88EmitRoundtrip As New D88()
                d88EmitRoundtrip.FromBytes(d88EmitBytes)
                Dim d88EmitTrack = TryCast(d88EmitRoundtrip.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue(d88EmitTrack IsNot Nothing, "image.d88.emit.track_present mismatch")
                If d88EmitTrack IsNot Nothing Then
                    AssertTrue(d88EmitTrack.Nsec = d88EmitSource.Nsec, "image.d88.emit.track_sector_count mismatch")
                    AssertTrue(d88EmitTrack.NrMissing() = d88EmitSource.NrMissing(), "image.d88.emit.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim expectedDigest = BytesToHex(sha.ComputeHash(d88EmitSource.GetImgTrack()))
                        Dim actualDigest = BytesToHex(sha.ComputeHash(d88EmitTrack.GetImgTrack()))
                        AssertTrue(actualDigest = expectedDigest, "image.d88.emit.track_img_sha256 mismatch")
                    End Using
                    Dim expectedPrefix = BytesToHex(d88EmitSource.GetImgTrack().Take(Math.Min(16, d88EmitSource.GetImgTrack().Length)).ToArray())
                    Dim actualPrefix = BytesToHex(d88EmitTrack.GetImgTrack().Take(Math.Min(16, d88EmitTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(actualPrefix = expectedPrefix, "image.d88.emit.track_img_prefix_hex mismatch")
                End If

                Dim dmk As New Dmk()
                dmk.FromBytes(HexToBytes(root.DmkCase.FileHex))
                Dim dmkTrack = TryCast(dmk.GetTrack(0, 0), MasterTrack)
                AssertTrue((dmkTrack IsNot Nothing) = root.DmkCase.TrackPresent, "image.dmk.track_present mismatch")
                If dmkTrack IsNot Nothing Then
                    AssertTrue(dmkTrack.Bits.Count = root.DmkCase.BitLength, "image.dmk.bit_length mismatch")
                    AssertNear(dmkTrack.TimePerRev, root.DmkCase.TimePerRev, 0.0000001, "image.dmk.time_per_rev mismatch")
                    AssertNear(dmkTrack.Bitrate, root.DmkCase.Bitrate, 0.001, "image.dmk.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(dmkTrack.Bits)))
                        AssertTrue(digest = root.DmkCase.BitsSha256, "image.dmk.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(dmkTrack.Bits).Take(Math.Min(16, BoolsToBytes(dmkTrack.Bits).Length)).ToArray())
                    AssertTrue(prefix = root.DmkCase.BitsPrefixHex, "image.dmk.bits_prefix_hex mismatch")
                End If
                AssertTrue((dmk.GetTrack(0, 1) IsNot Nothing) = root.DmkCase.Track01Present, "image.dmk.track01_present mismatch")

                Dim edsk As New Edsk()
                edsk.FromBytes(HexToBytes(root.EdskCase.FileHex))
                Dim edskTrack = TryCast(edsk.GetTrack(0, 0), MasterTrack)
                AssertTrue((edskTrack IsNot Nothing) = root.EdskCase.TrackPresent, "image.edsk.track_present mismatch")
                If edskTrack IsNot Nothing Then
                    AssertTrue(edskTrack.Bits.Count = root.EdskCase.BitLength, "image.edsk.bit_length mismatch")
                    AssertNear(edskTrack.TimePerRev, root.EdskCase.TimePerRev, 0.0000001, "image.edsk.time_per_rev mismatch")
                    AssertNear(edskTrack.Bitrate, root.EdskCase.Bitrate, 0.001, "image.edsk.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(edskTrack.Bits)))
                        AssertTrue(digest = root.EdskCase.BitsSha256, "image.edsk.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(edskTrack.Bits).Take(Math.Min(16, BoolsToBytes(edskTrack.Bits).Length)).ToArray())
                    AssertTrue(prefix = root.EdskCase.BitsPrefixHex, "image.edsk.bits_prefix_hex mismatch")
                End If
                AssertTrue((edsk.GetTrack(0, 1) IsNot Nothing) = root.EdskCase.Track01Present, "image.edsk.track01_present mismatch")

                Dim dimFmt = DiskDefParser.GetDiskdef("pc98.2hd", diskDefsPath)
                Dim dimImage As New [Dim](dimFmt)
                dimImage.FromBytes(HexToBytes(root.DimCase.FileHex))
                Dim dimTrack = TryCast(dimImage.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((dimTrack IsNot Nothing) = root.DimCase.TrackPresent, "image.dim.track_present mismatch")
                If dimTrack IsNot Nothing Then
                    AssertTrue(dimTrack.Nsec = root.DimCase.TrackNsec, "image.dim.track_sector_count mismatch")
                    AssertTrue(dimTrack.NrMissing() = root.DimCase.TrackMissing, "image.dim.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(dimTrack.GetImgTrack()))
                        AssertTrue(digest = root.DimCase.TrackImgSha256, "image.dim.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(dimTrack.GetImgTrack().Take(Math.Min(16, dimTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.DimCase.TrackImgPrefixHex, "image.dim.track_img_prefix_hex mismatch")
                End If
                AssertTrue((dimImage.GetTrack(0, 1) IsNot Nothing) = root.DimCase.Track01Present, "image.dim.track01_present mismatch")

                Dim td0 As New Td0()
                td0.FromBytes(HexToBytes(root.Td0Case.FileHex))
                Dim td0Track = TryCast(td0.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((td0Track IsNot Nothing) = root.Td0Case.TrackPresent, "image.td0.track_present mismatch")
                If td0Track IsNot Nothing Then
                    AssertTrue(td0Track.Nsec = root.Td0Case.TrackNsec, "image.td0.track_sector_count mismatch")
                    AssertTrue(td0Track.NrMissing() = root.Td0Case.TrackMissing, "image.td0.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(td0Track.GetImgTrack()))
                        AssertTrue(digest = root.Td0Case.TrackImgSha256, "image.td0.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(td0Track.GetImgTrack().Take(Math.Min(16, td0Track.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.Td0Case.TrackImgPrefixHex, "image.td0.track_img_prefix_hex mismatch")
                End If
                AssertTrue((td0.GetTrack(0, 1) IsNot Nothing) = root.Td0Case.Track01Present, "image.td0.track01_present mismatch")

                Dim nfd As New Nfd()
                nfd.FromBytes(HexToBytes(root.NfdCase.FileHex))
                Dim nfdTrack = TryCast(nfd.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((nfdTrack IsNot Nothing) = root.NfdCase.TrackPresent, "image.nfd.track_present mismatch")
                If nfdTrack IsNot Nothing Then
                    AssertTrue(nfdTrack.Nsec = root.NfdCase.TrackNsec, "image.nfd.track_sector_count mismatch")
                    AssertTrue(nfdTrack.NrMissing() = root.NfdCase.TrackMissing, "image.nfd.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(nfdTrack.GetImgTrack()))
                        AssertTrue(digest = root.NfdCase.TrackImgSha256, "image.nfd.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(nfdTrack.GetImgTrack().Take(Math.Min(16, nfdTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.NfdCase.TrackImgPrefixHex, "image.nfd.track_img_prefix_hex mismatch")
                End If
                AssertTrue((nfd.GetTrack(0, 1) IsNot Nothing) = root.NfdCase.Track01Present, "image.nfd.track01_present mismatch")

                Dim dcpFmt = DiskDefParser.GetDiskdef("pc98.2hd", diskDefsPath)
                Dim dcp As New Dcp(dcpFmt)
                dcp.FromBytes(HexToBytes(root.DcpCase.FileHex))
                Dim dcpTrack = TryCast(dcp.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((dcpTrack IsNot Nothing) = root.DcpCase.TrackPresent, "image.dcp.track_present mismatch")
                If dcpTrack IsNot Nothing Then
                    AssertTrue(dcpTrack.Nsec = root.DcpCase.TrackNsec, "image.dcp.track_sector_count mismatch")
                    AssertTrue(dcpTrack.NrMissing() = root.DcpCase.TrackMissing, "image.dcp.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(dcpTrack.GetImgTrack()))
                        AssertTrue(digest = root.DcpCase.TrackImgSha256, "image.dcp.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(dcpTrack.GetImgTrack().Take(Math.Min(16, dcpTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.DcpCase.TrackImgPrefixHex, "image.dcp.track_img_prefix_hex mismatch")
                End If
                AssertTrue((dcp.GetTrack(0, 1) IsNot Nothing) = root.DcpCase.Track01Present, "image.dcp.track01_present mismatch")

                Dim a2r As New A2R()
                a2r.FromBytes(HexToBytes(root.A2rCase.FileHex))
                Dim a2rTrack = TryCast(a2r.GetTrack(0, 0), Flux)
                AssertTrue((a2rTrack IsNot Nothing) = root.A2rCase.TrackPresent, "image.a2r.track_present mismatch")
                If a2rTrack IsNot Nothing Then
                    AssertTrue(a2rTrack.IndexList.Count = root.A2rCase.IndexCount, "image.a2r.index_count mismatch")
                    AssertTrue(a2rTrack.List.Count = root.A2rCase.FluxCount, "image.a2r.flux_count mismatch")
                    AssertNear(a2rTrack.List.Sum(), root.A2rCase.FluxTotal, 0.001, "image.a2r.flux_total mismatch")
                    AssertSequence(a2rTrack.List.Take(root.A2rCase.FluxPrefix.Count), root.A2rCase.FluxPrefix, "image.a2r.flux_prefix mismatch")
                End If
                AssertTrue((a2r.GetTrack(0, 1) IsNot Nothing) = root.A2rCase.Track01Present, "image.a2r.track01_present mismatch")

                Dim msa As New Msa()
                msa.FromBytes(HexToBytes(root.MsaCase.FileHex))
                Dim msaTrack = TryCast(msa.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((msaTrack IsNot Nothing) = root.MsaCase.TrackPresent, "image.msa.track_present mismatch")
                If msaTrack IsNot Nothing Then
                    AssertTrue(msaTrack.Nsec = root.MsaCase.TrackNsec, "image.msa.track_sector_count mismatch")
                    AssertTrue(msaTrack.NrMissing() = root.MsaCase.TrackMissing, "image.msa.track_missing mismatch")
                    Dim img = msaTrack.GetImgTrack()
                    Dim sha As String
                    Using hasher = SHA256.Create()
                        sha = BitConverter.ToString(hasher.ComputeHash(img)).Replace("-", "").ToLowerInvariant()
                    End Using
                    AssertTrue(sha = root.MsaCase.TrackImgSha256, "image.msa.track_img_sha256 mismatch")
                    Dim prefix = BitConverter.ToString(img.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                    AssertTrue(prefix = root.MsaCase.TrackImgPrefixHex, "image.msa.track_img_prefix_hex mismatch")
                End If
                AssertTrue((msa.GetTrack(0, 1) IsNot Nothing) = root.MsaCase.Track01Present, "image.msa.track01_present mismatch")

                Dim apridiskFmt = DiskDefParser.GetDiskdef("ibm.720", diskDefsPath)
                Dim apridisk As New Apridisk(apridiskFmt)
                apridisk.FromBytes(HexToBytes(root.ApridiskCase.FileHex))
                Dim apriTrack = TryCast(apridisk.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((apriTrack IsNot Nothing) = root.ApridiskCase.TrackPresent, "image.apridisk.track_present mismatch")
                If apriTrack IsNot Nothing Then
                    AssertTrue(apriTrack.Nsec = root.ApridiskCase.TrackNsec, "image.apridisk.track_sector_count mismatch")
                    AssertTrue(apriTrack.NrMissing() = root.ApridiskCase.TrackMissing, "image.apridisk.track_missing mismatch")
                    Dim img = apriTrack.GetImgTrack()
                    Dim sha As String
                    Using hasher = SHA256.Create()
                        sha = BitConverter.ToString(hasher.ComputeHash(img)).Replace("-", "").ToLowerInvariant()
                    End Using
                    AssertTrue(sha = root.ApridiskCase.TrackImgSha256, "image.apridisk.track_img_sha256 mismatch")
                    Dim prefix = BitConverter.ToString(img.Take(16).ToArray()).Replace("-", "").ToLowerInvariant()
                    AssertTrue(prefix = root.ApridiskCase.TrackImgPrefixHex, "image.apridisk.track_img_prefix_hex mismatch")
                End If
                AssertTrue((apridisk.GetTrack(0, 1) IsNot Nothing) = root.ApridiskCase.Track01Present, "image.apridisk.track01_present mismatch")

                Dim imdMfm As New Imd()
                imdMfm.FromBytes(HexToBytes(root.ImdCases.Mfm.FileHex))
                Dim imdMfmTrack = TryCast(imdMfm.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((imdMfmTrack IsNot Nothing) = root.ImdCases.Mfm.TrackPresent, "image.imd.mfm.track_present mismatch")
                If imdMfmTrack IsNot Nothing Then
                    AssertTrue(imdMfmTrack.Nsec = root.ImdCases.Mfm.TrackNsec, "image.imd.mfm.track_sector_count mismatch")
                    AssertTrue(imdMfmTrack.NrMissing() = root.ImdCases.Mfm.TrackMissing, "image.imd.mfm.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(imdMfmTrack.GetImgTrack()))
                        AssertTrue(digest = root.ImdCases.Mfm.TrackImgSha256, "image.imd.mfm.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(imdMfmTrack.GetImgTrack().Take(Math.Min(16, imdMfmTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.ImdCases.Mfm.TrackImgPrefixHex, "image.imd.mfm.track_img_prefix_hex mismatch")
                End If
                AssertTrue((imdMfm.GetTrack(0, 1) IsNot Nothing) = root.ImdCases.Mfm.Track01Present, "image.imd.mfm.track01_present mismatch")

                Dim imdFm As New Imd()
                imdFm.FromBytes(HexToBytes(root.ImdCases.Fm.FileHex))
                Dim imdFmTrack = TryCast(imdFm.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue((imdFmTrack IsNot Nothing) = root.ImdCases.Fm.TrackPresent, "image.imd.fm.track_present mismatch")
                If imdFmTrack IsNot Nothing Then
                    AssertTrue(imdFmTrack.Nsec = root.ImdCases.Fm.TrackNsec, "image.imd.fm.track_sector_count mismatch")
                    AssertTrue(imdFmTrack.NrMissing() = root.ImdCases.Fm.TrackMissing, "image.imd.fm.track_missing mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(imdFmTrack.GetImgTrack()))
                        AssertTrue(digest = root.ImdCases.Fm.TrackImgSha256, "image.imd.fm.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(imdFmTrack.GetImgTrack().Take(Math.Min(16, imdFmTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.ImdCases.Fm.TrackImgPrefixHex, "image.imd.fm.track_img_prefix_hex mismatch")
                End If
                AssertTrue((imdFm.GetTrack(0, 1) IsNot Nothing) = root.ImdCases.Fm.Track01Present, "image.imd.fm.track01_present mismatch")

                Dim imdEmitMfmCodec = BuildImdFixtureCodec("ibm.mfm", root.ImdCases.Mfm.TrackNsec, 512, 2, 0.2, 2.0E-6, Function(i) CByte((13 + i * 5) And &HFF))
                Dim imdEmitMfm As New Imd()
                imdEmitMfm.EmitTrack(0, 0, imdEmitMfmCodec)
                Dim imdEmitMfmBytes = imdEmitMfm.GetImage()
                AssertTrue(System.Text.Encoding.ASCII.GetString(imdEmitMfmBytes, 0, 4) = "IMD ", "image.imd.emit.mfm.signature mismatch")
                Dim imdEmitMfmRoundtrip As New Imd()
                imdEmitMfmRoundtrip.FromBytes(imdEmitMfmBytes)
                Dim imdEmitMfmTrack = TryCast(imdEmitMfmRoundtrip.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue(imdEmitMfmTrack IsNot Nothing, "image.imd.emit.mfm.roundtrip missing")
                If imdEmitMfmTrack IsNot Nothing Then
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(imdEmitMfmTrack.GetImgTrack()))
                        AssertTrue(digest = root.ImdCases.Mfm.TrackImgSha256, "image.imd.emit.mfm.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(imdEmitMfmTrack.GetImgTrack().Take(Math.Min(16, imdEmitMfmTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.ImdCases.Mfm.TrackImgPrefixHex, "image.imd.emit.mfm.track_img_prefix_hex mismatch")
                End If

                Dim imdEmitFmCodec = BuildImdFixtureCodec("ibm.fm", root.ImdCases.Fm.TrackNsec, 128, 0, 0.2, 4.0E-6, Function(i) CByte((7 + i * 3) And &HFF))
                Dim imdEmitFm As New Imd()
                imdEmitFm.EmitTrack(0, 0, imdEmitFmCodec)
                Dim imdEmitFmBytes = imdEmitFm.GetImage()
                AssertTrue(System.Text.Encoding.ASCII.GetString(imdEmitFmBytes, 0, 4) = "IMD ", "image.imd.emit.fm.signature mismatch")
                Dim imdEmitFmRoundtrip As New Imd()
                imdEmitFmRoundtrip.FromBytes(imdEmitFmBytes)
                Dim imdEmitFmTrack = TryCast(imdEmitFmRoundtrip.GetTrack(0, 0), IbmTrackFixed)
                AssertTrue(imdEmitFmTrack IsNot Nothing, "image.imd.emit.fm.roundtrip missing")
                If imdEmitFmTrack IsNot Nothing Then
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(imdEmitFmTrack.GetImgTrack()))
                        AssertTrue(digest = root.ImdCases.Fm.TrackImgSha256, "image.imd.emit.fm.track_img_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(imdEmitFmTrack.GetImgTrack().Take(Math.Min(16, imdEmitFmTrack.GetImgTrack().Length)).ToArray())
                    AssertTrue(prefix = root.ImdCases.Fm.TrackImgPrefixHex, "image.imd.emit.fm.track_img_prefix_hex mismatch")
                End If

                Dim hfeMfm As New Hfe()
                hfeMfm.FromBytes(HexToBytes(root.HfeCases.Mfm.FileHex))
                Dim hfeMfmTrack = TryCast(hfeMfm.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeMfmTrack IsNot Nothing) = root.HfeCases.Mfm.TrackPresent, "image.hfe.mfm.track_present mismatch")
                If hfeMfmTrack IsNot Nothing Then
                    AssertTrue(hfeMfmTrack.Bits.Count = root.HfeCases.Mfm.BitLength, "image.hfe.mfm.bit_length mismatch")
                    AssertNear(hfeMfmTrack.TimePerRev, root.HfeCases.Mfm.TimePerRev, 0.0000001, "image.hfe.mfm.time_per_rev mismatch")
                    AssertNear(hfeMfmTrack.Bitrate, root.HfeCases.Mfm.Bitrate, 0.001, "image.hfe.mfm.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeMfmTrack.Bits)))
                        AssertTrue(digest = root.HfeCases.Mfm.BitsSha256, "image.hfe.mfm.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeMfmTrack.Bits).Take(Math.Min(16, BoolsToBytes(hfeMfmTrack.Bits).Length)).ToArray())
                    AssertTrue(prefix = root.HfeCases.Mfm.BitsPrefixHex, "image.hfe.mfm.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeMfm.GetTrack(0, 1) IsNot Nothing) = root.HfeCases.Mfm.Track01Present, "image.hfe.mfm.track01_present mismatch")

                Dim hfeFm As New Hfe()
                hfeFm.FromBytes(HexToBytes(root.HfeCases.Fm.FileHex))
                Dim hfeFmTrack = TryCast(hfeFm.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeFmTrack IsNot Nothing) = root.HfeCases.Fm.TrackPresent, "image.hfe.fm.track_present mismatch")
                If hfeFmTrack IsNot Nothing Then
                    AssertTrue(hfeFmTrack.Bits.Count = root.HfeCases.Fm.BitLength, "image.hfe.fm.bit_length mismatch")
                    AssertNear(hfeFmTrack.TimePerRev, root.HfeCases.Fm.TimePerRev, 0.0000001, "image.hfe.fm.time_per_rev mismatch")
                    AssertNear(hfeFmTrack.Bitrate, root.HfeCases.Fm.Bitrate, 0.001, "image.hfe.fm.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeFmTrack.Bits)))
                        AssertTrue(digest = root.HfeCases.Fm.BitsSha256, "image.hfe.fm.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeFmTrack.Bits).Take(Math.Min(16, BoolsToBytes(hfeFmTrack.Bits).Length)).ToArray())
                    AssertTrue(prefix = root.HfeCases.Fm.BitsPrefixHex, "image.hfe.fm.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeFm.GetTrack(0, 1) IsNot Nothing) = root.HfeCases.Fm.Track01Present, "image.hfe.fm.track01_present mismatch")

                Dim hfeMfmBitrate300 As New Hfe()
                hfeMfmBitrate300.FromBytes(HexToBytes(root.HfeCases.MfmBitrate300.FileHex))
                Dim hfeMfmBitrate300Track = TryCast(hfeMfmBitrate300.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeMfmBitrate300Track IsNot Nothing) = root.HfeCases.MfmBitrate300.TrackPresent, "image.hfe.mfm_bitrate_300.track_present mismatch")
                If hfeMfmBitrate300Track IsNot Nothing Then
                    AssertTrue(hfeMfmBitrate300Track.Bits.Count = root.HfeCases.MfmBitrate300.BitLength, "image.hfe.mfm_bitrate_300.bit_length mismatch")
                    AssertNear(hfeMfmBitrate300Track.TimePerRev, root.HfeCases.MfmBitrate300.TimePerRev, 0.0000001, "image.hfe.mfm_bitrate_300.time_per_rev mismatch")
                    AssertNear(hfeMfmBitrate300Track.Bitrate, root.HfeCases.MfmBitrate300.Bitrate, 0.001, "image.hfe.mfm_bitrate_300.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeMfmBitrate300Track.Bits)))
                        AssertTrue(digest = root.HfeCases.MfmBitrate300.BitsSha256, "image.hfe.mfm_bitrate_300.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeMfmBitrate300Track.Bits).Take(16).ToArray())
                    AssertTrue(prefix = root.HfeCases.MfmBitrate300.BitsPrefixHex, "image.hfe.mfm_bitrate_300.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeMfmBitrate300.GetTrack(0, 1) IsNot Nothing) = root.HfeCases.MfmBitrate300.Track01Present, "image.hfe.mfm_bitrate_300.track01_present mismatch")

                Dim hfeMfmDoubleStep As New Hfe()
                hfeMfmDoubleStep.FromBytes(HexToBytes(root.HfeCases.MfmDoubleStep.FileHex))
                Dim hfeMfmDoubleStepTrack = TryCast(hfeMfmDoubleStep.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeMfmDoubleStepTrack IsNot Nothing) = root.HfeCases.MfmDoubleStep.TrackPresent, "image.hfe.mfm_double_step.track_present mismatch")
                If hfeMfmDoubleStepTrack IsNot Nothing Then
                    AssertTrue(hfeMfmDoubleStepTrack.Bits.Count = root.HfeCases.MfmDoubleStep.BitLength, "image.hfe.mfm_double_step.bit_length mismatch")
                    AssertNear(hfeMfmDoubleStepTrack.TimePerRev, root.HfeCases.MfmDoubleStep.TimePerRev, 0.0000001, "image.hfe.mfm_double_step.time_per_rev mismatch")
                    AssertNear(hfeMfmDoubleStepTrack.Bitrate, root.HfeCases.MfmDoubleStep.Bitrate, 0.001, "image.hfe.mfm_double_step.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeMfmDoubleStepTrack.Bits)))
                        AssertTrue(digest = root.HfeCases.MfmDoubleStep.BitsSha256, "image.hfe.mfm_double_step.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeMfmDoubleStepTrack.Bits).Take(16).ToArray())
                    AssertTrue(prefix = root.HfeCases.MfmDoubleStep.BitsPrefixHex, "image.hfe.mfm_double_step.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeMfmDoubleStep.GetTrack(0, 1) IsNot Nothing) = root.HfeCases.MfmDoubleStep.Track01Present, "image.hfe.mfm_double_step.track01_present mismatch")

                Dim hfeMfmHeaderOpts As New Hfe()
                hfeMfmHeaderOpts.FromBytes(HexToBytes(root.HfeCases.MfmHeaderOpts.FileHex))
                Dim hfeMfmHeaderOptsTrack = TryCast(hfeMfmHeaderOpts.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeMfmHeaderOptsTrack IsNot Nothing) = root.HfeCases.MfmHeaderOpts.TrackPresent, "image.hfe.mfm_header_opts.track_present mismatch")
                If hfeMfmHeaderOptsTrack IsNot Nothing Then
                    AssertTrue(hfeMfmHeaderOptsTrack.Bits.Count = root.HfeCases.MfmHeaderOpts.BitLength, "image.hfe.mfm_header_opts.bit_length mismatch")
                    AssertNear(hfeMfmHeaderOptsTrack.TimePerRev, root.HfeCases.MfmHeaderOpts.TimePerRev, 0.0000001, "image.hfe.mfm_header_opts.time_per_rev mismatch")
                    AssertNear(hfeMfmHeaderOptsTrack.Bitrate, root.HfeCases.MfmHeaderOpts.Bitrate, 0.001, "image.hfe.mfm_header_opts.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeMfmHeaderOptsTrack.Bits)))
                        AssertTrue(digest = root.HfeCases.MfmHeaderOpts.BitsSha256, "image.hfe.mfm_header_opts.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeMfmHeaderOptsTrack.Bits).Take(16).ToArray())
                    AssertTrue(prefix = root.HfeCases.MfmHeaderOpts.BitsPrefixHex, "image.hfe.mfm_header_opts.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeMfmHeaderOpts.GetTrack(0, 1) IsNot Nothing) = root.HfeCases.MfmHeaderOpts.Track01Present, "image.hfe.mfm_header_opts.track01_present mismatch")
                Dim hfeMfmHeaderOptsBytes = HexToBytes(root.HfeCases.MfmHeaderOpts.FileHex)
                AssertTrue(CInt(hfeMfmHeaderOptsBytes(11)) = root.HfeCases.MfmHeaderOpts.ExpectedEncodingByte.Value, "image.hfe.mfm_header_opts.encoding_byte mismatch")
                AssertTrue(CInt(hfeMfmHeaderOptsBytes(16)) = root.HfeCases.MfmHeaderOpts.ExpectedInterfaceByte.Value, "image.hfe.mfm_header_opts.interface_byte mismatch")
                AssertTrue(CInt(hfeMfmHeaderOptsBytes(19)) = root.HfeCases.MfmHeaderOpts.ExpectedDoubleStepByte.Value, "image.hfe.mfm_header_opts.double_step_byte mismatch")

                Dim hfeMfmHeaderOptsNumeric As New Hfe()
                hfeMfmHeaderOptsNumeric.FromBytes(HexToBytes(root.HfeCases.MfmHeaderOptsNumeric.FileHex))
                Dim hfeMfmHeaderOptsNumericTrack = TryCast(hfeMfmHeaderOptsNumeric.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeMfmHeaderOptsNumericTrack IsNot Nothing) = root.HfeCases.MfmHeaderOptsNumeric.TrackPresent, "image.hfe.mfm_header_opts_numeric.track_present mismatch")
                If hfeMfmHeaderOptsNumericTrack IsNot Nothing Then
                    AssertTrue(hfeMfmHeaderOptsNumericTrack.Bits.Count = root.HfeCases.MfmHeaderOptsNumeric.BitLength, "image.hfe.mfm_header_opts_numeric.bit_length mismatch")
                    AssertNear(hfeMfmHeaderOptsNumericTrack.TimePerRev, root.HfeCases.MfmHeaderOptsNumeric.TimePerRev, 0.0000001, "image.hfe.mfm_header_opts_numeric.time_per_rev mismatch")
                    AssertNear(hfeMfmHeaderOptsNumericTrack.Bitrate, root.HfeCases.MfmHeaderOptsNumeric.Bitrate, 0.001, "image.hfe.mfm_header_opts_numeric.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeMfmHeaderOptsNumericTrack.Bits)))
                        AssertTrue(digest = root.HfeCases.MfmHeaderOptsNumeric.BitsSha256, "image.hfe.mfm_header_opts_numeric.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeMfmHeaderOptsNumericTrack.Bits).Take(16).ToArray())
                    AssertTrue(prefix = root.HfeCases.MfmHeaderOptsNumeric.BitsPrefixHex, "image.hfe.mfm_header_opts_numeric.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeMfmHeaderOptsNumeric.GetTrack(0, 1) IsNot Nothing) = root.HfeCases.MfmHeaderOptsNumeric.Track01Present, "image.hfe.mfm_header_opts_numeric.track01_present mismatch")
                Dim hfeMfmHeaderOptsNumericBytes = HexToBytes(root.HfeCases.MfmHeaderOptsNumeric.FileHex)
                AssertTrue(CInt(hfeMfmHeaderOptsNumericBytes(11)) = root.HfeCases.MfmHeaderOptsNumeric.ExpectedEncodingByte.Value, "image.hfe.mfm_header_opts_numeric.encoding_byte mismatch")
                AssertTrue(CInt(hfeMfmHeaderOptsNumericBytes(16)) = root.HfeCases.MfmHeaderOptsNumeric.ExpectedInterfaceByte.Value, "image.hfe.mfm_header_opts_numeric.interface_byte mismatch")
                AssertTrue(CInt(hfeMfmHeaderOptsNumericBytes(19)) = root.HfeCases.MfmHeaderOptsNumeric.ExpectedDoubleStepByte.Value, "image.hfe.mfm_header_opts_numeric.double_step_byte mismatch")

                Dim hfeFluxBitrate250 As New Hfe()
                hfeFluxBitrate250.FromBytes(HexToBytes(root.HfeCases.FluxBitrate250.FileHex))
                Dim hfeFluxBitrate250Track = TryCast(hfeFluxBitrate250.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeFluxBitrate250Track IsNot Nothing) = root.HfeCases.FluxBitrate250.TrackPresent, "image.hfe.flux_bitrate_250.track_present mismatch")
                If hfeFluxBitrate250Track IsNot Nothing Then
                    AssertTrue(hfeFluxBitrate250Track.Bits.Count = root.HfeCases.FluxBitrate250.BitLength, "image.hfe.flux_bitrate_250.bit_length mismatch")
                    AssertNear(hfeFluxBitrate250Track.TimePerRev, root.HfeCases.FluxBitrate250.TimePerRev, 0.0000001, "image.hfe.flux_bitrate_250.time_per_rev mismatch")
                    AssertNear(hfeFluxBitrate250Track.Bitrate, root.HfeCases.FluxBitrate250.Bitrate, 0.001, "image.hfe.flux_bitrate_250.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeFluxBitrate250Track.Bits)))
                        AssertTrue(digest = root.HfeCases.FluxBitrate250.BitsSha256, "image.hfe.flux_bitrate_250.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeFluxBitrate250Track.Bits).Take(16).ToArray())
                    AssertTrue(prefix = root.HfeCases.FluxBitrate250.BitsPrefixHex, "image.hfe.flux_bitrate_250.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeFluxBitrate250.GetTrack(0, 1) IsNot Nothing) = root.HfeCases.FluxBitrate250.Track01Present, "image.hfe.flux_bitrate_250.track01_present mismatch")

                Dim hfeV3Mfm As New Hfe()
                hfeV3Mfm.FromBytes(HexToBytes(root.HfeV3Cases.Mfm.FileHex))
                Dim hfeV3MfmTrack = TryCast(hfeV3Mfm.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeV3MfmTrack IsNot Nothing) = root.HfeV3Cases.Mfm.TrackPresent, "image.hfe_v3.mfm.track_present mismatch")
                If hfeV3MfmTrack IsNot Nothing Then
                    AssertTrue(hfeV3MfmTrack.Bits.Count = root.HfeV3Cases.Mfm.BitLength, "image.hfe_v3.mfm.bit_length mismatch")
                    AssertNear(hfeV3MfmTrack.TimePerRev, root.HfeV3Cases.Mfm.TimePerRev, 0.0000001, "image.hfe_v3.mfm.time_per_rev mismatch")
                    AssertNear(hfeV3MfmTrack.Bitrate, root.HfeV3Cases.Mfm.Bitrate, 0.001, "image.hfe_v3.mfm.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeV3MfmTrack.Bits)))
                        AssertTrue(digest = root.HfeV3Cases.Mfm.BitsSha256, "image.hfe_v3.mfm.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeV3MfmTrack.Bits).Take(Math.Min(16, BoolsToBytes(hfeV3MfmTrack.Bits).Length)).ToArray())
                    AssertTrue(prefix = root.HfeV3Cases.Mfm.BitsPrefixHex, "image.hfe_v3.mfm.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeV3Mfm.GetTrack(0, 1) IsNot Nothing) = root.HfeV3Cases.Mfm.Track01Present, "image.hfe_v3.mfm.track01_present mismatch")

                Dim hfeV3MfmUniform As New Hfe()
                hfeV3MfmUniform.FromBytes(HexToBytes(root.HfeV3Cases.MfmUniform.FileHex))
                Dim hfeV3MfmUniformTrack = TryCast(hfeV3MfmUniform.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeV3MfmUniformTrack IsNot Nothing) = root.HfeV3Cases.MfmUniform.TrackPresent, "image.hfe_v3.mfm_uniform.track_present mismatch")
                If hfeV3MfmUniformTrack IsNot Nothing Then
                    AssertTrue(hfeV3MfmUniformTrack.Bits.Count = root.HfeV3Cases.MfmUniform.BitLength, "image.hfe_v3.mfm_uniform.bit_length mismatch")
                    AssertNear(hfeV3MfmUniformTrack.TimePerRev, root.HfeV3Cases.MfmUniform.TimePerRev, 0.0000001, "image.hfe_v3.mfm_uniform.time_per_rev mismatch")
                    AssertNear(hfeV3MfmUniformTrack.Bitrate, root.HfeV3Cases.MfmUniform.Bitrate, 0.001, "image.hfe_v3.mfm_uniform.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeV3MfmUniformTrack.Bits)))
                        AssertTrue(digest = root.HfeV3Cases.MfmUniform.BitsSha256, "image.hfe_v3.mfm_uniform.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeV3MfmUniformTrack.Bits).Take(Math.Min(16, BoolsToBytes(hfeV3MfmUniformTrack.Bits).Length)).ToArray())
                    AssertTrue(prefix = root.HfeV3Cases.MfmUniform.BitsPrefixHex, "image.hfe_v3.mfm_uniform.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeV3MfmUniform.GetTrack(0, 1) IsNot Nothing) = root.HfeV3Cases.MfmUniform.Track01Present, "image.hfe_v3.mfm_uniform.track01_present mismatch")

                Dim hfeV3Fm As New Hfe()
                hfeV3Fm.FromBytes(HexToBytes(root.HfeV3Cases.Fm.FileHex))
                Dim hfeV3FmTrack = TryCast(hfeV3Fm.GetTrack(0, 0), MasterTrack)
                AssertTrue((hfeV3FmTrack IsNot Nothing) = root.HfeV3Cases.Fm.TrackPresent, "image.hfe_v3.fm.track_present mismatch")
                If hfeV3FmTrack IsNot Nothing Then
                    AssertTrue(hfeV3FmTrack.Bits.Count = root.HfeV3Cases.Fm.BitLength, "image.hfe_v3.fm.bit_length mismatch")
                    AssertNear(hfeV3FmTrack.TimePerRev, root.HfeV3Cases.Fm.TimePerRev, 0.0000001, "image.hfe_v3.fm.time_per_rev mismatch")
                    AssertNear(hfeV3FmTrack.Bitrate, root.HfeV3Cases.Fm.Bitrate, 0.001, "image.hfe_v3.fm.bitrate mismatch")
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeV3FmTrack.Bits)))
                        AssertTrue(digest = root.HfeV3Cases.Fm.BitsSha256, "image.hfe_v3.fm.bits_sha256 mismatch")
                    End Using
                    Dim prefix = BytesToHex(BoolsToBytes(hfeV3FmTrack.Bits).Take(Math.Min(16, BoolsToBytes(hfeV3FmTrack.Bits).Length)).ToArray())
                    AssertTrue(prefix = root.HfeV3Cases.Fm.BitsPrefixHex, "image.hfe_v3.fm.bits_prefix_hex mismatch")
                End If
                AssertTrue((hfeV3Fm.GetTrack(0, 1) IsNot Nothing) = root.HfeV3Cases.Fm.Track01Present, "image.hfe_v3.fm.track01_present mismatch")

                Dim hfeEmitMfmDisk = DiskDefParser.GetDiskdef("ibm.720", diskDefsPath)
                AssertTrue(hfeEmitMfmDisk IsNot Nothing, "image.hfe.emit.mfm.diskdef missing")
                Dim hfeEmitMfmCodec = TryCast(hfeEmitMfmDisk.MkTrack(0, 0), Codec)
                AssertTrue(hfeEmitMfmCodec IsNot Nothing, "image.hfe.emit.mfm.codec missing")
                Dim hfeEmitMfmData = Enumerable.Range(0, root.ImdCases.Mfm.TrackNsec * 512).
                    Select(Function(i) CByte((31 + i * 9) And &HFF)).ToArray()
                hfeEmitMfmCodec.SetImgTrack(hfeEmitMfmData)
                Dim hfeEmitMfmSource = hfeEmitMfmCodec.MasterTrack()
                Dim hfeEmitMfm As New Hfe()
                hfeEmitMfm.EmitTrack(0, 0, hfeEmitMfmCodec)
                Dim hfeEmitMfmBytes = hfeEmitMfm.GetImage()
                AssertTrue(System.Text.Encoding.ASCII.GetString(hfeEmitMfmBytes, 0, 8) = "HXCPICFE", "image.hfe.emit.mfm.signature mismatch")
                Dim hfeEmitMfmRoundtrip As New Hfe()
                hfeEmitMfmRoundtrip.FromBytes(hfeEmitMfmBytes)
                Dim hfeEmitMfmTrack = TryCast(hfeEmitMfmRoundtrip.GetTrack(0, 0), MasterTrack)
                AssertTrue(hfeEmitMfmTrack IsNot Nothing, "image.hfe.emit.mfm.roundtrip missing")
                Dim hfeExpectedMfmBytes = HexToBytes(root.HfeCases.Mfm.FileHex)
                Dim hfeExpectedMfmHeaderBitrate = CInt(BitConverter.ToUInt16(hfeExpectedMfmBytes, 12))
                Dim hfeEmitMfmHeaderBitrate = CInt(BitConverter.ToUInt16(hfeEmitMfmBytes, 12))
                AssertTrue(hfeEmitMfmHeaderBitrate = hfeExpectedMfmHeaderBitrate, "image.hfe.emit.mfm.header_bitrate mismatch")
                If hfeEmitMfmTrack IsNot Nothing Then
                    Using sha = SHA256.Create()
                        Dim srcDigest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeEmitMfmSource.Bits)))
                        Dim outDigest = BytesToHex(sha.ComputeHash(BoolsToBytes(hfeEmitMfmTrack.Bits)))
                        AssertTrue(outDigest = srcDigest, "image.hfe.emit.mfm.bits_sha256 mismatch")
                    End Using
                    AssertNear(hfeEmitMfmTrack.Bitrate, root.HfeCases.Mfm.Bitrate, 0.001, "image.hfe.emit.mfm.fixture_bitrate mismatch")
                    AssertNear(hfeEmitMfmTrack.TimePerRev, root.HfeCases.Mfm.TimePerRev, 0.0000001, "image.hfe.emit.mfm.fixture_time_per_rev mismatch")
                End If

                Dim hfeEmitBitrate As New Hfe()
                hfeEmitBitrate.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"bitrate", "300"}
                })
                hfeEmitBitrate.EmitTrack(0, 0, hfeEmitMfmCodec)
                Dim hfeEmitBitrateBytes = hfeEmitBitrate.GetImage()
                Dim hfeHeaderBitrate = CInt(BitConverter.ToUInt16(hfeEmitBitrateBytes, 12))
                AssertTrue(hfeHeaderBitrate = 300, "image.hfe.emit.bitrate_option.header mismatch")
                Dim hfeEmitBitrateRoundtrip As New Hfe()
                hfeEmitBitrateRoundtrip.FromBytes(hfeEmitBitrateBytes)
                Dim hfeEmitBitrateTrack = TryCast(hfeEmitBitrateRoundtrip.GetTrack(0, 0), MasterTrack)
                AssertTrue(hfeEmitBitrateTrack IsNot Nothing, "image.hfe.emit.bitrate_option.roundtrip missing")
                If hfeEmitBitrateTrack IsNot Nothing Then
                    AssertNear(hfeEmitBitrateTrack.Bitrate, 300.0 * 2000.0, 0.001, "image.hfe.emit.bitrate_option.bitrate mismatch")
                End If

                Dim hfeEmitV3 As New Hfe()
                hfeEmitV3.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"version", "3"}
                })
                hfeEmitV3.EmitTrack(0, 0, hfeEmitMfmCodec)
                Dim hfeEmitV3Bytes = hfeEmitV3.GetImage()
                AssertTrue(System.Text.Encoding.ASCII.GetString(hfeEmitV3Bytes, 0, 8) = "HXCHFEV3", "image.hfe.emit.v3.signature mismatch")
                Dim hfeEmitV3Roundtrip As New Hfe()
                hfeEmitV3Roundtrip.FromBytes(hfeEmitV3Bytes)
                Dim hfeEmitV3Track = TryCast(hfeEmitV3Roundtrip.GetTrack(0, 0), MasterTrack)
                AssertTrue(hfeEmitV3Track IsNot Nothing, "image.hfe.emit.v3.roundtrip missing")
                If hfeEmitV3Track IsNot Nothing Then
                    AssertTrue(hfeEmitV3Track.Bits.Count > 0, "image.hfe.emit.v3.bit_length missing")
                    AssertTrue(Math.Abs(hfeEmitV3Track.Bits.Count - root.HfeV3Cases.Mfm.BitLength) <= 1024, "image.hfe.emit.v3.bit_length drift")
                    AssertNear(hfeEmitV3Track.Bitrate, root.HfeV3Cases.Mfm.Bitrate, 5000.0, "image.hfe.emit.v3.bitrate drift")
                    AssertNear(hfeEmitV3Track.TimePerRev, root.HfeV3Cases.Mfm.TimePerRev, 0.01, "image.hfe.emit.v3.time_per_rev drift")
                End If

                Dim hfeEmitV3Uniform As New Hfe()
                hfeEmitV3Uniform.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"version", "3"},
                    {"uniform", "yes"}
                })
                hfeEmitV3Uniform.EmitTrack(0, 0, hfeEmitMfmCodec)
                Dim hfeEmitV3UniformBytes = hfeEmitV3Uniform.GetImage()
                AssertTrue(System.Text.Encoding.ASCII.GetString(hfeEmitV3UniformBytes, 0, 8) = "HXCHFEV3", "image.hfe.emit.v3_uniform.signature mismatch")
                Dim hfeEmitV3UniformRoundtrip As New Hfe()
                hfeEmitV3UniformRoundtrip.FromBytes(hfeEmitV3UniformBytes)
                Dim hfeEmitV3UniformTrack = TryCast(hfeEmitV3UniformRoundtrip.GetTrack(0, 0), MasterTrack)
                AssertTrue(hfeEmitV3UniformTrack IsNot Nothing, "image.hfe.emit.v3_uniform.roundtrip missing")
                If hfeEmitV3UniformTrack IsNot Nothing Then
                    AssertTrue(hfeEmitV3UniformTrack.Bits.Count > 0, "image.hfe.emit.v3_uniform.bit_length missing")
                    AssertTrue(Math.Abs(hfeEmitV3UniformTrack.Bits.Count - root.HfeV3Cases.MfmUniform.BitLength) <= 1024, "image.hfe.emit.v3_uniform.bit_length drift")
                    AssertNear(hfeEmitV3UniformTrack.Bitrate, root.HfeV3Cases.MfmUniform.Bitrate, 5000.0, "image.hfe.emit.v3_uniform.bitrate drift")
                    AssertNear(hfeEmitV3UniformTrack.TimePerRev, root.HfeV3Cases.MfmUniform.TimePerRev, 0.01, "image.hfe.emit.v3_uniform.time_per_rev drift")
                End If

                Dim hfeEmitFmDisk = DiskDefParser.GetDiskdef("atari.90", diskDefsPath)
                AssertTrue(hfeEmitFmDisk IsNot Nothing, "image.hfe.emit.fm.diskdef missing")
                Dim hfeEmitFmCodec = TryCast(hfeEmitFmDisk.MkTrack(0, 0), Codec)
                AssertTrue(hfeEmitFmCodec IsNot Nothing, "image.hfe.emit.fm.codec missing")
                Dim hfeEmitFmData = Enumerable.Range(0, root.ImdCases.Fm.TrackNsec * 128).
                    Select(Function(i) CByte((17 + i * 5) And &HFF)).ToArray()
                hfeEmitFmCodec.SetImgTrack(hfeEmitFmData)
                Dim hfeEmitFm As New Hfe()
                hfeEmitFm.EmitTrack(0, 0, hfeEmitFmCodec)
                Dim hfeEmitFmBytes = hfeEmitFm.GetImage()
                AssertTrue(System.Text.Encoding.ASCII.GetString(hfeEmitFmBytes, 0, 8) = "HXCPICFE", "image.hfe.emit.fm.signature mismatch")
                Dim hfeEmitFmRoundtrip As New Hfe()
                hfeEmitFmRoundtrip.FromBytes(hfeEmitFmBytes)
                Dim hfeEmitFmTrack = TryCast(hfeEmitFmRoundtrip.GetTrack(0, 0), MasterTrack)
                AssertTrue(hfeEmitFmTrack IsNot Nothing, "image.hfe.emit.fm.roundtrip missing")
                Dim hfeExpectedFmBytes = HexToBytes(root.HfeCases.Fm.FileHex)
                Dim hfeExpectedFmHeaderBitrate = CInt(BitConverter.ToUInt16(hfeExpectedFmBytes, 12))
                Dim hfeEmitFmHeaderBitrate = CInt(BitConverter.ToUInt16(hfeEmitFmBytes, 12))
                AssertTrue(Math.Abs(hfeEmitFmHeaderBitrate - hfeExpectedFmHeaderBitrate) <= 2,
                           String.Format("image.hfe.emit.fm.header_bitrate mismatch expected={0} actual={1}",
                                         hfeExpectedFmHeaderBitrate, hfeEmitFmHeaderBitrate))
                If hfeEmitFmTrack IsNot Nothing Then
                    AssertTrue(Math.Abs(hfeEmitFmTrack.Bits.Count - root.HfeCases.Fm.BitLength) <= 1024, "image.hfe.emit.fm.fixture_bit_length drift")
                    AssertNear(hfeEmitFmTrack.Bitrate, root.HfeCases.Fm.Bitrate, 5000.0, "image.hfe.emit.fm.fixture_bitrate mismatch")
                    AssertNear(hfeEmitFmTrack.TimePerRev, root.HfeCases.Fm.TimePerRev, 0.005, "image.hfe.emit.fm.fixture_time_per_rev mismatch")
                End If
            Finally
                Try
                    Directory.Delete(tmpDir, recursive:=True)
                Catch
                End Try
            End Try
        End Sub

        Private Sub RunCliParity(path As String)
            Dim root = LoadJson(Of CliFixtureRoot)(path)
            Dim actual = Greaseweazle.Cli.Program.GetSupportedActions().ToList()
            AssertTrue(actual.SequenceEqual(root.Actions), "cli.actions mismatch")

            Dim topUsage = Greaseweazle.Cli.Program.GetTopUsageLines().ToList()
            AssertTrue(topUsage.Count >= 4, "cli.top_usage_lines too short")
            AssertTrue(topUsage.SequenceEqual(root.TopUsage.Select(Function(x) NormalizeCliUsageLine(x))),
                       "cli.top_usage_lines mismatch")

            For Each actionName In root.Actions
                Dim helpLines = Greaseweazle.Cli.Program.GetActionHelpLines(actionName).ToList()
                AssertTrue(helpLines.Count > 0, String.Format("cli.help.{0} missing", actionName))
                Dim helpBody = String.Join(vbLf, helpLines)
                If String.Equals(actionName, "read", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(actionName, "write", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(actionName, "align", StringComparison.OrdinalIgnoreCase) Then
                    AssertTrue(helpBody.Contains("FORMAT options:"), String.Format("cli.help.{0} missing format section", actionName))
                End If
                If String.Equals(actionName, "read", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(actionName, "write", StringComparison.OrdinalIgnoreCase) Then
                    AssertTrue(helpBody.Contains("Supported file suffixes:"), String.Format("cli.help.{0} missing suffix section", actionName))
                End If
            Next

            For Each c In root.HelpCases
                Dim helpLines = Greaseweazle.Cli.Program.GetActionHelpLines(c.Action).ToList()
                Dim usageLine = helpLines.FirstOrDefault(Function(x) x.StartsWith("usage: ", StringComparison.OrdinalIgnoreCase))
                AssertTrue(String.Equals(NormalizeCliUsageForCompare(usageLine),
                                         NormalizeCliUsageForCompare(NormalizeCliUsageLine(c.UsageLine)),
                                         StringComparison.Ordinal),
                           String.Format("cli.help.{0}.usage mismatch", c.Action))
                If Not String.IsNullOrEmpty(c.DescriptionLine) Then
                    Dim descLine = helpLines.FirstOrDefault(Function(x) String.Equals(x.Trim(), c.DescriptionLine.Trim(), StringComparison.Ordinal))
                    AssertTrue(Not String.IsNullOrEmpty(descLine), String.Format("cli.help.{0}.description mismatch", c.Action))
                End If

                Dim prevIndex = -1
                For Each section In c.Sections
                    Dim index = helpLines.IndexOf(section)
                    AssertTrue(index >= 0, String.Format("cli.help.{0} missing section {1}", c.Action, section))
                    AssertTrue(index > prevIndex, String.Format("cli.help.{0} section order mismatch for {1}", c.Action, section))
                    prevIndex = index
                Next
                Dim helpBody = String.Join(vbLf, helpLines)
                For Each token In c.OptionTokens
                    AssertTrue(helpBody.Contains(token), String.Format("cli.help.{0} missing token {1}", c.Action, token))
                Next
                If c.OptionTokens IsNot Nothing AndAlso c.OptionTokens.Count > 1 Then
                    Dim normalizedBody = helpBody.ToLowerInvariant()
                    Dim lastPos = -1
                    For Each token In c.OptionTokens
                        Dim normalizedToken = token.ToLowerInvariant()
                        Dim pos = normalizedBody.IndexOf(normalizedToken, StringComparison.Ordinal)
                        AssertTrue(pos >= 0, String.Format("cli.help.{0} missing token for ordering {1}", c.Action, token))
                        AssertTrue(pos >= lastPos,
                                   String.Format("cli.help.{0} token order mismatch around {1}", c.Action, token))
                        lastPos = pos
                    Next
                End If

                If c.PositionalLines IsNot Nothing AndAlso c.PositionalLines.Count > 0 Then
                    Dim lastPos = -1
                    For Each raw In c.PositionalLines
                        Dim needle = NormalizeCliDetailLine(raw)
                        Dim pos = IndexOfNormalizedLineAfter(helpLines, needle, lastPos)
                        AssertTrue(pos >= 0, String.Format("cli.help.{0} missing positional line {1}", c.Action, raw))
                        lastPos = pos
                    Next
                End If

                If c.ExampleLines IsNot Nothing AndAlso c.ExampleLines.Count > 0 Then
                    Dim lastPos = -1
                    For Each raw In c.ExampleLines
                        Dim needle = NormalizeCliDetailLine(raw)
                        Dim pos = IndexOfNormalizedLineAfter(helpLines, needle, lastPos)
                        AssertTrue(pos >= 0, String.Format("cli.help.{0} missing example line {1}", c.Action, raw))
                        lastPos = pos
                    Next
                End If
            Next
        End Sub

        Private Sub RunTrackSetParity(path As String)
            Dim root = LoadJson(Of TrackSetFixtureRoot)(path)
            For Each c In root.Cases
                Dim ts = New TrackSet(c.Spec)
                AssertTrue(ts.ToString() = c.ToStringValue, String.Format("trackset({0}).to_string", c.Spec))
                AssertSequence(ts.Cyls.Select(Function(x) CDbl(x)), c.Cyls.Select(Function(x) CDbl(x)), String.Format("trackset({0}).cyls", c.Spec))
                AssertSequence(ts.Heads.Select(Function(x) CDbl(x)), c.Heads.Select(Function(x) CDbl(x)), String.Format("trackset({0}).heads", c.Spec))
                AssertSequence(ts.HOff.Select(Function(x) CDbl(x)), c.HeadOffsets.Select(Function(x) CDbl(x)), String.Format("trackset({0}).h_off", c.Spec))
                AssertTrue(ts.Step = c.StepValue, String.Format("trackset({0}).step", c.Spec))
                AssertTrue(ts.Hswap = c.HeadSwap, String.Format("trackset({0}).hswap", c.Spec))

                Dim iterActual = ts.IteratePhysical().ToList()
                AssertTrue(iterActual.Count = c.Iter.Count, String.Format("trackset({0}).iter.count", c.Spec))
                For i = 0 To iterActual.Count - 1
                    Dim a = iterActual(i)
                    Dim e = c.Iter(i)
                    AssertTrue(a.PhysicalCyl = e.PhysicalCylinder, String.Format("trackset({0}).iter[{1}].physical_cyl", c.Spec, i))
                    AssertTrue(a.PhysicalHead = e.PhysicalHead, String.Format("trackset({0}).iter[{1}].physical_head", c.Spec, i))
                    AssertTrue(a.Cyl = e.Cylinder, String.Format("trackset({0}).iter[{1}].cyl", c.Spec, i))
                    AssertTrue(a.Head = e.Head, String.Format("trackset({0}).iter[{1}].head", c.Spec, i))
                Next

                For Each containsCase In c.Contains
                    Dim value = ts.Contains(containsCase.Keys(0), containsCase.Keys(1))
                    AssertTrue(value = containsCase.Value, String.Format("trackset({0}).contains({1},{2})", c.Spec, containsCase.Keys(0), containsCase.Keys(1)))
                Next
            Next
        End Sub

        Private Sub RunActionDescriptionsParity(path As String)
            Dim root = LoadJson(Of ActionDescriptionsFixtureRoot)(path)
            Dim registry = Actions.CreateDefaultRegistry()
            For Each pair In root.Descriptions
                Dim action As ToolAction = Nothing
                AssertTrue(registry.TryGetAction(pair.Key, action), String.Format("actions.description.{0} exists", pair.Key))
                AssertTrue(action.Description = pair.Value, String.Format("actions.description.{0} value", pair.Key))
            Next
        End Sub

        Private Function ToPortDescriptor(src As PortFixture) As PortDescriptor
            Return New PortDescriptor With {
                .Device = src.Device,
                .Manufacturer = src.Manufacturer,
                .Product = src.Product,
                .Vid = src.Vid,
                .Pid = src.Pid,
                .SerialNumber = src.SerialNumber,
                .Location = src.Location,
                .Interface = src.Interface
            }
        End Function

        Private Sub RunPrecompParity(path As String)
            Dim root = LoadJson(Of PrecompFixtureRoot)(path)
            For Each c In root.Cases
                Dim spec = New PrecompSpec(c.Spec)
                AssertTrue(spec.ToString() = c.Repr, String.Format("precomp({0}).repr", c.Spec))
                AssertTrue(spec.Type = c.Type, String.Format("precomp({0}).type", c.Spec))
                AssertTrue(spec.Entries.Count = c.List.Count, String.Format("precomp({0}).list.count", c.Spec))
                For i = 0 To c.List.Count - 1
                    AssertTrue(spec.Entries(i).Item1 = c.List(i).Cylinder, String.Format("precomp({0}).list[{1}].cyl", c.Spec, i))
                    AssertTrue(spec.Entries(i).Item2 = c.List(i).Nanoseconds, String.Format("precomp({0}).list[{1}].ns", c.Spec, i))
                Next
                Dim sampleCyls As Integer() = {0, 10, 40, 61}
                For i = 0 To sampleCyls.Length - 1
                    Dim actual = spec.TrackPrecomp(sampleCyls(i))
                    Dim expected = c.Resolved(i)
                    If expected Is Nothing Then
                        AssertTrue(actual Is Nothing, String.Format("precomp({0}).resolved[{1}] expected none", c.Spec, i))
                    Else
                        AssertTrue(actual IsNot Nothing, String.Format("precomp({0}).resolved[{1}] missing", c.Spec, i))
                        AssertTrue(actual.Type = expected.Type, String.Format("precomp({0}).resolved[{1}].type", c.Spec, i))
                        AssertNear(actual.Ns, expected.Nanoseconds, 0.0001, String.Format("precomp({0}).resolved[{1}].ns", c.Spec, i))
                    End If
                Next
            Next
        End Sub

        Private Sub RunReadWriteAlgorithmParity(path As String)
            Dim root = LoadJson(Of ReadWriteFixtureRoot)(path)

            For Each c In root.WriteScaleCases
                Dim result = ReadWrite.ScaleWriteFlux(c.FluxList, c.Factor)
                AssertSequence(result.ScaledFlux.Select(Function(x) CDbl(x)),
                               c.Scaled.Select(Function(x) CDbl(x)),
                               String.Format("readwrite.scale({0})", c.Factor))
                AssertNear(result.FinalRemainder, c.FinalRemainder, 1.0E-9,
                           String.Format("readwrite.scale({0}).remainder", c.Factor))
            Next

            For Each c In root.FakeIndexCases
                Dim result = ReadWrite.BuildFakeIndexList(c.Revs, c.Ticks, c.DriveTicksPerRev, c.SampleFreq)
                AssertTrue(result.EffectiveTicks = c.EffectiveTicks, "readwrite.fake_index.effective_ticks")
                AssertSequence(result.IndexList.Select(Function(x) CDbl(x)),
                               c.IndexList.Select(Function(x) CDbl(x)),
                               "readwrite.fake_index.index_list")
            Next

            Dim readAction As New ReadAction()
            For Each c In root.FakeIndexCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                Dim args As String() = {
                    "--parity-fake-index",
                    "--revs", c.Revs.ToString(Globalization.CultureInfo.InvariantCulture),
                    "--ticks", c.Ticks.ToString(Globalization.CultureInfo.InvariantCulture),
                    "--drive-ticks-per-rev", c.DriveTicksPerRev.ToString(Globalization.CultureInfo.InvariantCulture),
                    "--sample-freq", c.SampleFreq.ToString(Globalization.CultureInfo.InvariantCulture)
                }
                Dim rc = readAction.Execute(args, ctx)
                AssertTrue(rc = 0, "read action parity execution")
                Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines(0) = String.Format("effective_ticks={0}", c.EffectiveTicks), "read action effective_ticks output")
                AssertTrue(lines(1) = String.Format("index_list={0}", String.Join(",", c.IndexList)), "read action index list output")
            Next

            Dim writeAction As New WriteAction()
            For Each c In root.WriteScaleCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                Dim args As String() = {
                    "--parity-scale-flux",
                    "--factor", c.Factor.ToString(Globalization.CultureInfo.InvariantCulture),
                    "--flux", String.Join(",", c.FluxList)
                }
                Dim rc = writeAction.Execute(args, ctx)
                AssertTrue(rc = 0, "write action parity execution")
                Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines(0) = String.Format("scaled={0}", String.Join(",", c.Scaled)), "write action scaled output")
                Dim remainder = Double.Parse(lines(1).Substring("remainder=".Length), Globalization.CultureInfo.InvariantCulture)
                AssertNear(remainder, c.FinalRemainder, 1.0E-9, "write action remainder output")
            Next

            For Each c In root.RuntimeCases.ReadCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                If c.Ok Then
                    Dim rc = readAction.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "read runtime rc")
                    Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "read runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        readAction.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "read runtime error mismatch")
                End If
            Next

            For Each c In root.RuntimeCases.WriteCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                If c.Ok Then
                    Dim rc = writeAction.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "write runtime rc")
                    Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "write runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        writeAction.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "write runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunInfoParity(path As String)
            Dim root = LoadJson(Of InfoFixtureRoot)(path)
            For Each c In root.TagCases
                Dim major = 0
                Dim minor = 0
                Dim matched = Info.TryParseFirmwareTag(c.Tag, major, minor)
                AssertTrue(matched = c.Matched, String.Format("info.tag({0}).matched", c.Tag))
                If matched Then
                    AssertTrue(major = c.Major.GetValueOrDefault(), String.Format("info.tag({0}).major", c.Tag))
                    AssertTrue(minor = c.Minor.GetValueOrDefault(), String.Format("info.tag({0}).minor", c.Tag))
                End If
            Next

            For Each lineCase In root.LineCases
                Dim actual = Info.PrintInfoLine(lineCase.Name, lineCase.Value, lineCase.Tab)
                AssertTrue(actual = lineCase.Output, String.Format("info.print_info_line({0})", lineCase.Name))
            Next

            Dim infoAction As New InfoAction()
            For Each c In root.TagCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                Dim rc = infoAction.Execute(New String() {"--parity-parse-tag", "--tag", c.Tag}, ctx)
                AssertTrue(rc = 0, "info action parse-tag rc")
                Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines(0) = String.Format("matched={0}", If(c.Matched, 1, 0)), "info action parse-tag matched")
                If c.Matched Then
                    AssertTrue(lines(1) = String.Format("version={0}.{1}", c.Major.GetValueOrDefault(), c.Minor.GetValueOrDefault()), "info action parse-tag version")
                End If
            Next

            For Each lineCase In root.LineCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                Dim rc = infoAction.Execute(
                    New String() {
                        "--parity-format-line",
                        "--name", lineCase.Name,
                        "--value", lineCase.Value,
                        "--tab", lineCase.Tab.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "info action format-line rc")
                Dim outputLine = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(outputLine = lineCase.Output, "info action format-line output")
            Next

            ' Resolve {HOST_VERSION} placeholder to the gw-vb assembly's
            ' InformationalVersion (e.g. "1.23"), matching what InfoAction now
            ' prints — the fixture must stay in lock-step with the action's
            ' Python-style "1.23" output rather than the dotted AssemblyVersion
            ' (e.g. "1.23.0.0").
            Dim hostAssembly = GetType(InfoAction).Assembly
            Dim hostInfo = TryCast(Reflection.CustomAttributeExtensions.GetCustomAttribute(Of Reflection.AssemblyInformationalVersionAttribute)(hostAssembly), Reflection.AssemblyInformationalVersionAttribute)
            Dim hostVersion As String = If(hostInfo IsNot Nothing AndAlso Not String.IsNullOrEmpty(hostInfo.InformationalVersion),
                                           hostInfo.InformationalVersion,
                                           hostAssembly.GetName().Version.ToString())
            For Each c In root.RuntimeCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                If c.Ok Then
                    Dim rc = infoAction.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "info runtime rc")
                    Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    Dim expected = c.Lines.Select(Function(s) s.Replace("{HOST_VERSION}", hostVersion)).ToList()
                    AssertTrue(lines.SequenceEqual(expected), "info runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        infoAction.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "info runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunUpdateParity(path As String)
            Dim root = LoadJson(Of UpdateFixtureRoot)(path)
            Dim updateAction As New UpdateAction()
            For Each c In root.MutualExclusionCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                Dim args As New List(Of String) From {"--parity-validate-args"}
                If c.FileValue IsNot Nothing Then
                    args.Add("--file")
                    args.Add(c.FileValue)
                End If
                If c.TagValue IsNot Nothing Then
                    args.Add("--tag")
                    args.Add(c.TagValue)
                End If
                Dim rc = updateAction.Execute(args, ctx)
                AssertTrue(rc = 0, "update action parity execution")
                Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines(0) = String.Format("ok={0}", If(c.Ok, 1, 0)), "update mutual exclusion ok flag")
                If Not c.Ok Then
                    AssertTrue(lines(1) = String.Format("error={0}", c.ErrorMessage), "update mutual exclusion error")
                End If
            Next

            For Each c In root.DownloadLineCases
                Dim actual = Update.BuildDownloadLine(c.Name)
                AssertTrue(actual = c.Line, "update download line mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim sw As New StringWriter()
                Dim ctx As New ToolContext With {.Output = sw, .ErrorOutput = sw}
                If c.Ok Then
                    Dim rc = updateAction.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "update runtime rc")
                    Dim lines = sw.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "update runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = updateAction.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "update runtime error mismatch")
                End If
            Next

            For Each c In root.ExtractCases
                Dim payload As New UpdatePayload With {
                    .Name = "fixture.upd",
                    .Data = HexToBytes(c.UpdHex)
                }
                Dim info As New FirmwareInfo With {
                    .HwModel = c.HwModel
                }
                If c.Ok Then
                    Dim extracted = Update.ExtractUpdate(info, payload, c.Bootloader)
                    AssertTrue(extracted.Major = c.VersionMajor, String.Format("update extract {0} major mismatch", c.Name))
                    AssertTrue(extracted.Minor = c.VersionMinor, String.Format("update extract {0} minor mismatch", c.Name))
                    AssertTrue(extracted.Payload.Length = c.PayloadLen, String.Format("update extract {0} payload_len mismatch", c.Name))
                    Using sha = SHA256.Create()
                        Dim digest = BytesToHex(sha.ComputeHash(extracted.Payload))
                        AssertTrue(digest = c.PayloadSha256, String.Format("update extract {0} payload_sha256 mismatch", c.Name))
                    End Using
                Else
                    Dim threw = False
                    Try
                        Dim ignored = Update.ExtractUpdate(info, payload, c.Bootloader)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, String.Format("update extract {0} error mismatch", c.Name))
                End If
            Next
        End Sub

        Private Sub RunPinParity(path As String)
            Dim root = LoadJson(Of PinFixtureRoot)(path)
            Dim action As New PinAction()

            Dim usageWriter As New StringWriter()
            Dim usageContext As New ToolContext With {.Output = usageWriter, .ErrorOutput = usageWriter}
            Dim rc = action.Execute(New String() {"--parity-usage"}, usageContext)
            AssertTrue(rc = 0, "pin usage rc")
            Dim usageLines = usageWriter.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
            AssertTrue(usageLines.SequenceEqual(root.UsageLines), "pin usage lines mismatch")

            For Each c In root.PinValueMessages
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                rc = action.Execute(
                    New String() {
                        "--parity-format-level",
                        "--pin", c.Pin.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--level", If(c.Level, "1", "0")
                    },
                    ctx)
                AssertTrue(rc = 0, "pin format level rc")
                Dim output = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(output = c.Message, "pin format level message mismatch")
            Next

            For Each c In root.DispatchCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                rc = action.Execute(New String() {"--parity-dispatch", "--argv", String.Join("|", c.Argv)}, ctx)
                AssertTrue(rc = 0, "pin dispatch rc")
                Dim output = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(output = c.Action, "pin dispatch mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = c.Rc, "pin runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "pin runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "pin runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunResetParity(path As String)
            Dim root = LoadJson(Of ResetFixtureRoot)(path)
            Dim action As New ResetAction()
            For Each c In root.DelaysFlagCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-delays-flag",
                        "--delays", If(c.Delays, "1", "0")
                    },
                    ctx)
                AssertTrue(rc = 0, "reset delays flag rc")
                Dim output = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(output = String.Format("restore_delays={0}", If(c.ShouldRestoreDelays, 1, 0)), "reset delays flag mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    Dim rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "reset runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "reset runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "reset runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunSeekParity(path As String)
            Dim root = LoadJson(Of SeekFixtureRoot)(path)
            Dim action As New SeekAction()
            For Each c In root.Cases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-extreme-check",
                        "--cylinder", c.Cylinder.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--force", If(c.Force, "1", "0")
                    },
                    ctx)
                AssertTrue(rc = 0, "seek parity rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines.Length >= 2, "seek parity output missing")
                AssertTrue(lines(0) = String.Format("prompt={0}", If(c.PromptNeeded, 1, 0)), "seek prompt mismatch")
                AssertTrue(lines(1) = c.PromptText, "seek prompt text mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    Dim rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "seek runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "seek runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "seek runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunDelaysParity(path As String)
            Dim root = LoadJson(Of DelaysFixtureRoot)(path)
            Dim action As New DelaysAction()
            For Each c In root.PrintInfoCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-format-line",
                        "--name", c.Name,
                        "--value", c.Value,
                        "--tab", c.Tab.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "delays parity rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Line, "delays format line mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    Dim rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "delays runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "delays runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "delays runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunCleanParity(path As String)
            Dim root = LoadJson(Of CleanFixtureRoot)(path)
            Dim action As New CleanAction()

            For Each c In root.SeekCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-seek-target",
                        "--cylinder", c.Cylinder.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--cyls", c.Cyls.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "clean seek target rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = String.Format("seek_target={0}", c.SeekTarget), "clean seek target mismatch")
            Next

            For Each c In root.PatternCases
                Dim stepWriter As New StringWriter()
                Dim stepCtx As New ToolContext With {.Output = stepWriter, .ErrorOutput = stepWriter}
                Dim stepRc = action.Execute(
                    New String() {"--parity-step", "--cyls", c.Cyls.ToString(Globalization.CultureInfo.InvariantCulture)},
                    stepCtx)
                AssertTrue(stepRc = 0, "clean step rc")
                Dim stepLine = stepWriter.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(stepLine = String.Format("step={0}", c.StepValue), "clean step mismatch")

                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-pattern",
                        "--cyls", c.Cyls.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--passes", c.Passes.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "clean pattern rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines.Length = c.PassSequences.Count, "clean pattern pass count mismatch")
                For i = 0 To c.PassSequences.Count - 1
                    Dim expected = String.Format("pass{0}={1}", i, String.Join(",", c.PassSequences(i)))
                    AssertTrue(lines(i) = expected, String.Format("clean pattern pass{0} mismatch", i))
                Next
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    Dim rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "clean runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "clean runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "clean runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunConvertParity(path As String)
            Dim root = LoadJson(Of ConvertFixtureRoot)(path)
            Dim action As New ConvertAction()

            For Each c In root.TrackSummaryCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-track-summary",
                        "--cyl", c.Cyl.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--head", c.Head.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--physical-cyl", c.PhysicalCyl.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--physical-head", c.PhysicalHead.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "convert track summary rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Summary, "convert track summary mismatch")
            Next

            For Each c In root.ConvertHeaderCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-convert-header",
                        "--tracks", c.Tracks,
                        "--out-tracks", c.OutTracks
                    },
                    ctx)
                AssertTrue(rc = 0, "convert header rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Line, "convert header mismatch")
            Next

            For Each c In root.FormatResolutionCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim args As New List(Of String) From {"--parity-resolve-format"}
                If c.ExplicitFormat IsNot Nothing Then
                    args.Add("--explicit-format")
                    args.Add(c.ExplicitFormat)
                End If
                If c.InputDefault IsNot Nothing Then
                    args.Add("--input-default")
                    args.Add(c.InputDefault)
                End If
                If c.OutputDefault IsNot Nothing Then
                    args.Add("--output-default")
                    args.Add(c.OutputDefault)
                End If
                Dim rc = action.Execute(args, ctx)
                AssertTrue(rc = 0, "convert format resolution rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = String.Format("format={0}", c.ResolvedFormat), "convert format resolution mismatch")
            Next

            For Each c In root.TrackResolutionCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim args As New List(Of String) From {"--parity-resolve-tracks"}
                If c.FormatTracks IsNot Nothing Then
                    args.Add("--format-tracks")
                    args.Add(c.FormatTracks)
                End If
                If c.Tracks IsNot Nothing Then
                    args.Add("--tracks")
                    args.Add(c.Tracks)
                End If
                If c.OutTracks IsNot Nothing Then
                    args.Add("--out-tracks")
                    args.Add(c.OutTracks)
                End If
                Dim rc = action.Execute(args, ctx)
                AssertTrue(rc = 0, "convert track resolution rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines.Length >= 2, "convert track resolution output missing")
                AssertTrue(lines(0) = String.Format("tracks={0}", c.ResolvedTracks), "convert resolved tracks mismatch")
                AssertTrue(lines(1) = String.Format("out_tracks={0}", c.ResolvedOutTracks), "convert resolved out-tracks mismatch")
            Next

            For Each c In root.LoopCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim outArg = String.Join("|", c.OutTracks.Select(
                    Function(t) String.Format("{0}.{1}>{2}.{3}", t.Cyl, t.Head, t.PhysicalCyl, t.PhysicalHead)))
                Dim inArg = String.Join("|", c.InTracks.Select(Function(t) String.Format("{0}.{1}", t.Cyl, t.Head)))
                Dim availArg = String.Join("|", c.AvailableTracks.Select(Function(t) String.Format("{0}.{1}", t.Cyl, t.Head)))
                Dim rc = action.Execute(
                    New String() {
                        "--parity-loop-sim",
                        "--out-tracks-map", outArg,
                        "--in-tracks", inArg,
                        "--available-tracks", availArg,
                        "--cache", If(c.CacheEnabled, "1", "0")
                    },
                    ctx)
                AssertTrue(rc = 0, "convert loop simulation rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.None)
                Dim nonEmpty = lines.Where(Function(x) Not String.IsNullOrEmpty(x)).ToArray()
                AssertTrue(nonEmpty.Length >= 3, "convert loop simulation output missing")
                AssertTrue(nonEmpty(0) = String.Format("process={0}", String.Join(",", c.ProcessCalls)), "convert loop process mismatch")
                AssertTrue(nonEmpty(1) = String.Format("emit={0}", String.Join(",", c.EmitTargets)), "convert loop emit mismatch")
                AssertTrue(nonEmpty(2) = String.Format("cache={0}", String.Join(",", c.CacheKeys)), "convert loop cache mismatch")
            Next

            Dim runtimeTmpDir = IO.Path.Combine(IO.Path.GetTempPath(), "gw-vb-convert-runtime-" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(runtimeTmpDir)
            Try
                Dim runtimeInScp = IO.Path.Combine(runtimeTmpDir, "in.scp")
                Dim runtimeSrc As New Scp()
                runtimeSrc.FileName = runtimeInScp
                runtimeSrc.EmitTrack(0, 0, New Flux({100000.0}, {2000.0, 2500.0, 3000.0, 3500.0, 91000.0}, Scp.SampleFrequency, indexCued:=True))
                File.WriteAllBytes(runtimeInScp, runtimeSrc.GetImage())

                For Each c In root.RuntimeCases
                    Dim writer As New StringWriter()
                    Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                    Dim execArgs = c.Args.Select(Function(x) MapConvertRuntimePathArg(x, runtimeTmpDir, runtimeInScp)).ToArray()
                    If c.Ok Then
                        Dim rc = action.Execute(execArgs, ctx)
                        AssertTrue(rc = 0, "convert runtime rc")
                        Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                        AssertTrue(lines.Count >= c.Lines.Count, "convert runtime output mismatch")
                        For i = 0 To c.Lines.Count - 1
                            AssertTrue(lines(i) = c.Lines(i), "convert runtime output mismatch")
                        Next
                    Else
                        Dim threw = False
                        Try
                            Dim rc = action.Execute(execArgs, ctx)
                        Catch ex As FatalException
                            threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                        End Try
                        AssertTrue(threw, "convert runtime error mismatch")
                    End If
                Next
            Finally
                Try
                    Directory.Delete(runtimeTmpDir, recursive:=True)
                Catch
                End Try
            End Try

            Dim tmpDir = IO.Path.Combine(IO.Path.GetTempPath(), "gw-vb-convert-parity-" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(tmpDir)
            Try
                Dim inScp = IO.Path.Combine(tmpDir, "in.scp")
                Dim inDmk = IO.Path.Combine(tmpDir, "in.dmk")
                Dim inDsk = IO.Path.Combine(tmpDir, "in.dsk")
                Dim inTd0 = IO.Path.Combine(tmpDir, "in.td0")
                Dim inFdi = IO.Path.Combine(tmpDir, "in.fdi")
                Dim inNfd = IO.Path.Combine(tmpDir, "in.nfd")
                Dim inDcp = IO.Path.Combine(tmpDir, "in.dcp")
                Dim inA2r = IO.Path.Combine(tmpDir, "in.a2r")
                Dim inMsa = IO.Path.Combine(tmpDir, "in.msa")
                Dim inApridisk = IO.Path.Combine(tmpDir, "in-apridisk.dsk")
                Dim inNsi = IO.Path.Combine(tmpDir, "in.nsi")
                Dim outRaw = IO.Path.Combine(tmpDir, "out00.0.raw")
                Dim outScp = IO.Path.Combine(tmpDir, "roundtrip.scp")
                Dim outScpRevs1 = IO.Path.Combine(tmpDir, "roundtrip-revs1.scp")
                Dim outAdf = IO.Path.Combine(tmpDir, "roundtrip.adf")
                Dim outD81 = IO.Path.Combine(tmpDir, "roundtrip.d81")
                Dim outD64 = IO.Path.Combine(tmpDir, "roundtrip.d64")
                Dim outD71 = IO.Path.Combine(tmpDir, "roundtrip.d71")
                Dim outD1M = IO.Path.Combine(tmpDir, "roundtrip.d1m")
                Dim outD2M = IO.Path.Combine(tmpDir, "roundtrip.d2m")
                Dim outD4M = IO.Path.Combine(tmpDir, "roundtrip.d4m")
                Dim outDo = IO.Path.Combine(tmpDir, "roundtrip.do")
                Dim outPo = IO.Path.Combine(tmpDir, "roundtrip.po")
                Dim outSt = IO.Path.Combine(tmpDir, "roundtrip.st")
                Dim outIma = IO.Path.Combine(tmpDir, "roundtrip.ima")
                Dim outScpFromAdf = IO.Path.Combine(tmpDir, "roundtrip-from-adf.scp")
                Dim outScpFromDmk = IO.Path.Combine(tmpDir, "roundtrip-from-dmk.scp")
                Dim outScpFromDsk = IO.Path.Combine(tmpDir, "roundtrip-from-dsk.scp")
                Dim outScpFromTd0 = IO.Path.Combine(tmpDir, "roundtrip-from-td0.scp")
                Dim outScpFromFdi = IO.Path.Combine(tmpDir, "roundtrip-from-fdi.scp")
                Dim outScpFromNfd = IO.Path.Combine(tmpDir, "roundtrip-from-nfd.scp")
                Dim outScpFromDcp = IO.Path.Combine(tmpDir, "roundtrip-from-dcp.scp")
                Dim outScpFromA2r = IO.Path.Combine(tmpDir, "roundtrip-from-a2r.scp")
                Dim outScpFromMsa = IO.Path.Combine(tmpDir, "roundtrip-from-msa.scp")
                Dim outScpFromApridisk = IO.Path.Combine(tmpDir, "roundtrip-from-apridisk.scp")
                Dim outScpFromNsi = IO.Path.Combine(tmpDir, "roundtrip-from-nsi.scp")
                Dim outScpFromD81 = IO.Path.Combine(tmpDir, "roundtrip-from-d81.scp")
                Dim outScpFromD88 = IO.Path.Combine(tmpDir, "roundtrip-from-d88.scp")
                Dim outScpFromD64 = IO.Path.Combine(tmpDir, "roundtrip-from-d64.scp")
                Dim outScpFromD71 = IO.Path.Combine(tmpDir, "roundtrip-from-d71.scp")
                Dim outScpFromD1M = IO.Path.Combine(tmpDir, "roundtrip-from-d1m.scp")
                Dim outScpFromD2M = IO.Path.Combine(tmpDir, "roundtrip-from-d2m.scp")
                Dim outScpFromD4M = IO.Path.Combine(tmpDir, "roundtrip-from-d4m.scp")
                Dim outScpFromDo = IO.Path.Combine(tmpDir, "roundtrip-from-do.scp")
                Dim outScpFromPo = IO.Path.Combine(tmpDir, "roundtrip-from-po.scp")
                Dim outScpFromSt = IO.Path.Combine(tmpDir, "roundtrip-from-st.scp")
                Dim outScpFromIma = IO.Path.Combine(tmpDir, "roundtrip-from-ima.scp")
                Dim outScpFromImd = IO.Path.Combine(tmpDir, "roundtrip-from-imd.scp")
                Dim outScpFromHfe = IO.Path.Combine(tmpDir, "roundtrip-from-hfe.scp")
                Dim outHfeFromImd = IO.Path.Combine(tmpDir, "roundtrip-from-imd.hfe")
                Dim outImdFromHfe = IO.Path.Combine(tmpDir, "roundtrip-from-hfe.imd")
                Dim outImdFromRaw = IO.Path.Combine(tmpDir, "roundtrip-from-raw.imd")
                Dim outHfeFromRaw = IO.Path.Combine(tmpDir, "roundtrip-from-raw.hfe")
                Dim outRawFromImd = IO.Path.Combine(tmpDir, "roundtrip-from-imd.00.0.raw")
                Dim outRawFromHfe = IO.Path.Combine(tmpDir, "roundtrip-from-hfe.00.0.raw")
                Dim outScpFromImdFromRaw = IO.Path.Combine(tmpDir, "roundtrip-from-imd-from-raw.scp")
                Dim outScpFromHfeFromRaw = IO.Path.Combine(tmpDir, "roundtrip-from-hfe-from-raw.scp")
                Dim outScpFromImdFromHfe = IO.Path.Combine(tmpDir, "roundtrip-from-imd-from-hfe.scp")
                Dim outScpFromHfeFromImd = IO.Path.Combine(tmpDir, "roundtrip-from-hfe-from-imd.scp")
                Dim outD88 = IO.Path.Combine(tmpDir, "roundtrip.d88")
                Dim outDim = IO.Path.Combine(tmpDir, "roundtrip.dim")
                Dim outDmk = IO.Path.Combine(tmpDir, "roundtrip.dmk")
                Dim outA2r = IO.Path.Combine(tmpDir, "roundtrip.a2r")
                Dim outImd = IO.Path.Combine(tmpDir, "roundtrip.imd")
                Dim outHfe = IO.Path.Combine(tmpDir, "roundtrip.hfe")
                Dim explicitScpOutputs As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

                Dim src As New Scp()
                src.FileName = inScp
                src.EmitTrack(0, 0, New Flux({100000.0}, {2000.0, 2500.0, 3000.0, 3500.0, 91000.0}, Scp.SampleFrequency, indexCued:=True))
                File.WriteAllBytes(inScp, src.GetImage())
                File.WriteAllBytes(inDmk, BuildConvertFixtureDmkImageBytes())
                File.WriteAllBytes(inDsk, BuildConvertFixtureEdskImageBytes())
                File.WriteAllBytes(inTd0, BuildConvertFixtureTd0ImageBytes())
                File.WriteAllBytes(inFdi, BuildConvertFixtureFdiImageBytes())
                File.WriteAllBytes(inNfd, BuildConvertFixtureNfdImageBytes())
                File.WriteAllBytes(inDcp, BuildConvertFixtureDcpImageBytes())
                File.WriteAllBytes(inA2r, BuildConvertFixtureA2rImageBytes())
                File.WriteAllBytes(inMsa, BuildConvertFixtureMsaImageBytes())
                File.WriteAllBytes(inApridisk, BuildConvertFixtureApridiskImageBytes())
                File.WriteAllBytes(inNsi, BuildConvertFixtureNsiImageBytes())

                Dim rawWriter As New StringWriter()
                Dim rawCtx As New ToolContext With {.Output = rawWriter, .ErrorOutput = rawWriter}
                Dim rawRc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outRaw}, rawCtx)
                AssertTrue(rawRc = 0, "convert file-backed scp->raw rc")
                AssertTrue(File.Exists(outRaw), "convert file-backed scp->raw output missing")
                AssertTrue(New FileInfo(outRaw).Length > 0, "convert file-backed scp->raw output empty")

                Dim scpWriter As New StringWriter()
                Dim scpCtx As New ToolContext With {.Output = scpWriter, .ErrorOutput = scpWriter}
                Dim scpRc = action.Execute(New String() {"--tracks", "c=0:h=0", outRaw, outScp}, scpCtx)
                AssertTrue(scpRc = 0, "convert file-backed raw->scp rc")
                AssertTrue(File.Exists(outScp), "convert file-backed raw->scp output missing")
                AssertTrue(New FileInfo(outScp).Length > 0, "convert file-backed raw->scp output empty")

                Dim scpFromDmkWriter As New StringWriter()
                Dim scpFromDmkCtx As New ToolContext With {.Output = scpFromDmkWriter, .ErrorOutput = scpFromDmkWriter}
                Dim scpFromDmkRc = action.Execute(New String() {"--tracks", "c=0:h=0", inDmk, outScpFromDmk}, scpFromDmkCtx)
                AssertTrue(scpFromDmkRc = 0, "convert file-backed dmk->scp rc")
                AssertTrue(File.Exists(outScpFromDmk), "convert file-backed dmk->scp output missing")
                AssertTrue(New FileInfo(outScpFromDmk).Length > 0, "convert file-backed dmk->scp output empty")

                Dim scpFromDskWriter As New StringWriter()
                Dim scpFromDskCtx As New ToolContext With {.Output = scpFromDskWriter, .ErrorOutput = scpFromDskWriter}
                Dim scpFromDskRc = action.Execute(New String() {"--tracks", "c=0:h=0", inDsk, outScpFromDsk}, scpFromDskCtx)
                AssertTrue(scpFromDskRc = 0, "convert file-backed dsk->scp rc")
                AssertTrue(File.Exists(outScpFromDsk), "convert file-backed dsk->scp output missing")
                AssertTrue(New FileInfo(outScpFromDsk).Length > 0, "convert file-backed dsk->scp output empty")

                Dim scpFromTd0Writer As New StringWriter()
                Dim scpFromTd0Ctx As New ToolContext With {.Output = scpFromTd0Writer, .ErrorOutput = scpFromTd0Writer}
                Dim scpFromTd0Rc = action.Execute(New String() {"--tracks", "c=0:h=0", inTd0, outScpFromTd0}, scpFromTd0Ctx)
                AssertTrue(scpFromTd0Rc = 0, "convert file-backed td0->scp rc")
                AssertTrue(File.Exists(outScpFromTd0), "convert file-backed td0->scp output missing")
                AssertTrue(New FileInfo(outScpFromTd0).Length > 0, "convert file-backed td0->scp output empty")

                Dim scpFromFdiWriter As New StringWriter()
                Dim scpFromFdiCtx As New ToolContext With {.Output = scpFromFdiWriter, .ErrorOutput = scpFromFdiWriter}
                Dim scpFromFdiRc = action.Execute(New String() {"--tracks", "c=0:h=0", inFdi, outScpFromFdi}, scpFromFdiCtx)
                AssertTrue(scpFromFdiRc = 0, "convert file-backed fdi->scp rc")
                AssertTrue(File.Exists(outScpFromFdi), "convert file-backed fdi->scp output missing")
                AssertTrue(New FileInfo(outScpFromFdi).Length > 0, "convert file-backed fdi->scp output empty")

                Dim scpFromNfdWriter As New StringWriter()
                Dim scpFromNfdCtx As New ToolContext With {.Output = scpFromNfdWriter, .ErrorOutput = scpFromNfdWriter}
                Dim scpFromNfdRc = action.Execute(New String() {"--tracks", "c=0:h=0", inNfd, outScpFromNfd}, scpFromNfdCtx)
                AssertTrue(scpFromNfdRc = 0, "convert file-backed nfd->scp rc")
                AssertTrue(File.Exists(outScpFromNfd), "convert file-backed nfd->scp output missing")
                AssertTrue(New FileInfo(outScpFromNfd).Length > 0, "convert file-backed nfd->scp output empty")

                Dim scpFromDcpWriter As New StringWriter()
                Dim scpFromDcpCtx As New ToolContext With {.Output = scpFromDcpWriter, .ErrorOutput = scpFromDcpWriter}
                Dim scpFromDcpRc = action.Execute(New String() {"--tracks", "c=0:h=0", inDcp, outScpFromDcp}, scpFromDcpCtx)
                AssertTrue(scpFromDcpRc = 0, "convert file-backed dcp->scp rc")
                AssertTrue(File.Exists(outScpFromDcp), "convert file-backed dcp->scp output missing")
                AssertTrue(New FileInfo(outScpFromDcp).Length > 0, "convert file-backed dcp->scp output empty")

                Dim scpFromA2rWriter As New StringWriter()
                Dim scpFromA2rCtx As New ToolContext With {.Output = scpFromA2rWriter, .ErrorOutput = scpFromA2rWriter}
                Dim scpFromA2rRc = action.Execute(New String() {"--tracks", "c=0:h=0", inA2r, outScpFromA2r}, scpFromA2rCtx)
                AssertTrue(scpFromA2rRc = 0, "convert file-backed a2r->scp rc")
                AssertTrue(File.Exists(outScpFromA2r), "convert file-backed a2r->scp output missing")
                AssertTrue(New FileInfo(outScpFromA2r).Length > 0, "convert file-backed a2r->scp output empty")

                Dim scpFromMsaWriter As New StringWriter()
                Dim scpFromMsaCtx As New ToolContext With {.Output = scpFromMsaWriter, .ErrorOutput = scpFromMsaWriter}
                Dim scpFromMsaRc = action.Execute(New String() {"--tracks", "c=0:h=0", inMsa, outScpFromMsa}, scpFromMsaCtx)
                AssertTrue(scpFromMsaRc = 0, "convert file-backed msa->scp rc")
                AssertTrue(File.Exists(outScpFromMsa), "convert file-backed msa->scp output missing")
                AssertTrue(New FileInfo(outScpFromMsa).Length > 0, "convert file-backed msa->scp output empty")

                Dim scpFromApridiskWriter As New StringWriter()
                Dim scpFromApridiskCtx As New ToolContext With {.Output = scpFromApridiskWriter, .ErrorOutput = scpFromApridiskWriter}
                Dim scpFromApridiskRc = action.Execute(New String() {"--format", "ibm.720", "--tracks", "c=0:h=0", inApridisk, outScpFromApridisk}, scpFromApridiskCtx)
                AssertTrue(scpFromApridiskRc = 0, "convert file-backed apridisk->scp rc")
                AssertTrue(File.Exists(outScpFromApridisk), "convert file-backed apridisk->scp output missing")
                AssertTrue(New FileInfo(outScpFromApridisk).Length > 0, "convert file-backed apridisk->scp output empty")

                Dim scpFromNsiWriter As New StringWriter()
                Dim scpFromNsiCtx As New ToolContext With {.Output = scpFromNsiWriter, .ErrorOutput = scpFromNsiWriter}
                Dim scpFromNsiRc = action.Execute(New String() {"--tracks", "c=0:h=0", inNsi, outScpFromNsi}, scpFromNsiCtx)
                AssertTrue(scpFromNsiRc = 0, "convert nsi->scp rc")
                AssertTrue(File.Exists(outScpFromNsi), "convert nsi->scp output missing")
                AssertTrue(New FileInfo(outScpFromNsi).Length > 0, "convert nsi->scp output empty")


                Dim scpRevsWriter As New StringWriter()
                Dim scpRevsCtx As New ToolContext With {.Output = scpRevsWriter, .ErrorOutput = scpRevsWriter}
                Dim scpRevsRc = action.Execute(New String() {"--tracks", "c=0:h=0", outRaw, outScpRevs1 & "::revs=1"}, scpRevsCtx)
                AssertTrue(scpRevsRc = 0, "convert file-backed raw->scp::revs=1 rc")
                AssertTrue(File.Exists(outScpRevs1), "convert file-backed raw->scp::revs=1 output missing")
                AssertTrue(New FileInfo(outScpRevs1).Length > 0, "convert file-backed raw->scp::revs=1 output empty")
                Dim scpRevsImage As New Scp()
                scpRevsImage.FromBytes(File.ReadAllBytes(outScpRevs1))
                Dim scpRevsTrack = TryCast(scpRevsImage.GetTrack(0, 0), Flux)
                AssertTrue(scpRevsTrack IsNot Nothing, "convert file-backed raw->scp::revs=1 track missing")
                AssertTrue(scpRevsTrack.IndexList.Count = 1, "convert file-backed raw->scp::revs=1 revolution count mismatch")

                Dim adfWriter As New StringWriter()
                Dim adfCtx As New ToolContext With {.Output = adfWriter, .ErrorOutput = adfWriter}
                Dim adfRc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outAdf}, adfCtx)
                AssertTrue(adfRc = 0, "convert file-backed scp->adf rc")
                AssertTrue(File.Exists(outAdf), "convert file-backed scp->adf output missing")
                AssertTrue(New FileInfo(outAdf).Length > 0, "convert file-backed scp->adf output empty")

                Dim scpFromAdfWriter As New StringWriter()
                Dim scpFromAdfCtx As New ToolContext With {.Output = scpFromAdfWriter, .ErrorOutput = scpFromAdfWriter}
                Dim scpFromAdfRc = action.Execute(New String() {"--tracks", "c=0:h=0", outAdf, outScpFromAdf}, scpFromAdfCtx)
                AssertTrue(scpFromAdfRc = 0, "convert file-backed adf->scp rc")
                AssertTrue(File.Exists(outScpFromAdf), "convert file-backed adf->scp output missing")
                AssertTrue(New FileInfo(outScpFromAdf).Length > 0, "convert file-backed adf->scp output empty")

                Dim d81Writer As New StringWriter()
                Dim d81Ctx As New ToolContext With {.Output = d81Writer, .ErrorOutput = d81Writer}
                Dim d81Rc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outD81}, d81Ctx)
                AssertTrue(d81Rc = 0, "convert file-backed scp->d81 rc")
                AssertTrue(File.Exists(outD81), "convert file-backed scp->d81 output missing")
                AssertTrue(New FileInfo(outD81).Length > 0, "convert file-backed scp->d81 output empty")

                Dim scpFromD81Writer As New StringWriter()
                Dim scpFromD81Ctx As New ToolContext With {.Output = scpFromD81Writer, .ErrorOutput = scpFromD81Writer}
                Dim scpFromD81Rc = action.Execute(New String() {"--tracks", "c=0:h=0", outD81, outScpFromD81}, scpFromD81Ctx)
                AssertTrue(scpFromD81Rc = 0, "convert file-backed d81->scp rc")
                AssertTrue(File.Exists(outScpFromD81), "convert file-backed d81->scp output missing")
                AssertTrue(New FileInfo(outScpFromD81).Length > 0, "convert file-backed d81->scp output empty")

                Dim d64Writer As New StringWriter()
                Dim d64Ctx As New ToolContext With {.Output = d64Writer, .ErrorOutput = d64Writer}
                Dim d64Rc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outD64}, d64Ctx)
                AssertTrue(d64Rc = 0, "convert file-backed scp->d64 rc")
                AssertTrue(File.Exists(outD64), "convert file-backed scp->d64 output missing")
                AssertTrue(New FileInfo(outD64).Length > 0, "convert file-backed scp->d64 output empty")

                Dim scpFromD64Writer As New StringWriter()
                Dim scpFromD64Ctx As New ToolContext With {.Output = scpFromD64Writer, .ErrorOutput = scpFromD64Writer}
                Dim scpFromD64Rc = action.Execute(New String() {"--tracks", "c=0:h=0", outD64, outScpFromD64}, scpFromD64Ctx)
                AssertTrue(scpFromD64Rc = 0, "convert file-backed d64->scp rc")
                AssertTrue(File.Exists(outScpFromD64), "convert file-backed d64->scp output missing")
                AssertTrue(New FileInfo(outScpFromD64).Length > 0, "convert file-backed d64->scp output empty")

                Dim d71Writer As New StringWriter()
                Dim d71Ctx As New ToolContext With {.Output = d71Writer, .ErrorOutput = d71Writer}
                Dim d71Rc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outD71}, d71Ctx)
                AssertTrue(d71Rc = 0, "convert file-backed scp->d71 rc")
                AssertTrue(File.Exists(outD71), "convert file-backed scp->d71 output missing")
                AssertTrue(New FileInfo(outD71).Length > 0, "convert file-backed scp->d71 output empty")

                Dim scpFromD71Writer As New StringWriter()
                Dim scpFromD71Ctx As New ToolContext With {.Output = scpFromD71Writer, .ErrorOutput = scpFromD71Writer}
                Dim scpFromD71Rc = action.Execute(New String() {"--tracks", "c=0:h=0", outD71, outScpFromD71}, scpFromD71Ctx)
                AssertTrue(scpFromD71Rc = 0, "convert file-backed d71->scp rc")
                AssertTrue(File.Exists(outScpFromD71), "convert file-backed d71->scp output missing")
                AssertTrue(New FileInfo(outScpFromD71).Length > 0, "convert file-backed d71->scp output empty")

                Dim d1mWriter As New StringWriter()
                Dim d1mCtx As New ToolContext With {.Output = d1mWriter, .ErrorOutput = d1mWriter}
                Dim d1mRc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outD1M}, d1mCtx)
                AssertTrue(d1mRc = 0, "convert file-backed scp->d1m rc")
                AssertTrue(File.Exists(outD1M), "convert file-backed scp->d1m output missing")
                AssertTrue(New FileInfo(outD1M).Length > 0, "convert file-backed scp->d1m output empty")

                Dim scpFromD1MWriter As New StringWriter()
                Dim scpFromD1MCtx As New ToolContext With {.Output = scpFromD1MWriter, .ErrorOutput = scpFromD1MWriter}
                Dim scpFromD1MRc = action.Execute(New String() {"--tracks", "c=0:h=0", outD1M, outScpFromD1M}, scpFromD1MCtx)
                AssertTrue(scpFromD1MRc = 0, "convert file-backed d1m->scp rc")
                AssertTrue(File.Exists(outScpFromD1M), "convert file-backed d1m->scp output missing")
                AssertTrue(New FileInfo(outScpFromD1M).Length > 0, "convert file-backed d1m->scp output empty")

                Dim d2mWriter As New StringWriter()
                Dim d2mCtx As New ToolContext With {.Output = d2mWriter, .ErrorOutput = d2mWriter}
                Dim d2mRc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outD2M}, d2mCtx)
                AssertTrue(d2mRc = 0, "convert file-backed scp->d2m rc")
                AssertTrue(File.Exists(outD2M), "convert file-backed scp->d2m output missing")
                AssertTrue(New FileInfo(outD2M).Length > 0, "convert file-backed scp->d2m output empty")

                Dim scpFromD2MWriter As New StringWriter()
                Dim scpFromD2MCtx As New ToolContext With {.Output = scpFromD2MWriter, .ErrorOutput = scpFromD2MWriter}
                Dim scpFromD2MRc = action.Execute(New String() {"--tracks", "c=0:h=0", outD2M, outScpFromD2M}, scpFromD2MCtx)
                AssertTrue(scpFromD2MRc = 0, "convert file-backed d2m->scp rc")
                AssertTrue(File.Exists(outScpFromD2M), "convert file-backed d2m->scp output missing")
                AssertTrue(New FileInfo(outScpFromD2M).Length > 0, "convert file-backed d2m->scp output empty")

                Dim d4mWriter As New StringWriter()
                Dim d4mCtx As New ToolContext With {.Output = d4mWriter, .ErrorOutput = d4mWriter}
                Dim d4mRc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outD4M}, d4mCtx)
                AssertTrue(d4mRc = 0, "convert file-backed scp->d4m rc")
                AssertTrue(File.Exists(outD4M), "convert file-backed scp->d4m output missing")
                AssertTrue(New FileInfo(outD4M).Length > 0, "convert file-backed scp->d4m output empty")

                Dim scpFromD4MWriter As New StringWriter()
                Dim scpFromD4MCtx As New ToolContext With {.Output = scpFromD4MWriter, .ErrorOutput = scpFromD4MWriter}
                Dim scpFromD4MRc = action.Execute(New String() {"--tracks", "c=0:h=0", outD4M, outScpFromD4M}, scpFromD4MCtx)
                AssertTrue(scpFromD4MRc = 0, "convert file-backed d4m->scp rc")
                AssertTrue(File.Exists(outScpFromD4M), "convert file-backed d4m->scp output missing")
                AssertTrue(New FileInfo(outScpFromD4M).Length > 0, "convert file-backed d4m->scp output empty")

                Dim doWriter As New StringWriter()
                Dim doCtx As New ToolContext With {.Output = doWriter, .ErrorOutput = doWriter}
                Dim doRc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outDo}, doCtx)
                AssertTrue(doRc = 0, "convert file-backed scp->do rc")
                AssertTrue(File.Exists(outDo), "convert file-backed scp->do output missing")
                AssertTrue(New FileInfo(outDo).Length > 0, "convert file-backed scp->do output empty")

                Dim scpFromDoWriter As New StringWriter()
                Dim scpFromDoCtx As New ToolContext With {.Output = scpFromDoWriter, .ErrorOutput = scpFromDoWriter}
                Dim scpFromDoRc = action.Execute(New String() {"--tracks", "c=0:h=0", outDo, outScpFromDo}, scpFromDoCtx)
                AssertTrue(scpFromDoRc = 0, "convert file-backed do->scp rc")
                AssertTrue(File.Exists(outScpFromDo), "convert file-backed do->scp output missing")
                AssertTrue(New FileInfo(outScpFromDo).Length > 0, "convert file-backed do->scp output empty")

                Dim poWriter As New StringWriter()
                Dim poCtx As New ToolContext With {.Output = poWriter, .ErrorOutput = poWriter}
                Dim poRc = action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outPo}, poCtx)
                AssertTrue(poRc = 0, "convert file-backed scp->po rc")
                AssertTrue(File.Exists(outPo), "convert file-backed scp->po output missing")
                AssertTrue(New FileInfo(outPo).Length > 0, "convert file-backed scp->po output empty")

                Dim scpFromPoWriter As New StringWriter()
                Dim scpFromPoCtx As New ToolContext With {.Output = scpFromPoWriter, .ErrorOutput = scpFromPoWriter}
                Dim scpFromPoRc = action.Execute(New String() {"--tracks", "c=0:h=0", outPo, outScpFromPo}, scpFromPoCtx)
                AssertTrue(scpFromPoRc = 0, "convert file-backed po->scp rc")
                AssertTrue(File.Exists(outScpFromPo), "convert file-backed po->scp output missing")
                AssertTrue(New FileInfo(outScpFromPo).Length > 0, "convert file-backed po->scp output empty")

                Dim stWriter As New StringWriter()
                Dim stCtx As New ToolContext With {.Output = stWriter, .ErrorOutput = stWriter}
                Dim stRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", inScp, outSt}, stCtx)
                AssertTrue(stRc = 0, "convert file-backed scp->st rc")
                AssertTrue(File.Exists(outSt), "convert file-backed scp->st output missing")
                AssertTrue(New FileInfo(outSt).Length > 0, "convert file-backed scp->st output empty")

                Dim scpFromStWriter As New StringWriter()
                Dim scpFromStCtx As New ToolContext With {.Output = scpFromStWriter, .ErrorOutput = scpFromStWriter}
                Dim scpFromStRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outSt, outScpFromSt}, scpFromStCtx)
                AssertTrue(scpFromStRc = 0, "convert file-backed st->scp rc")
                AssertTrue(File.Exists(outScpFromSt), "convert file-backed st->scp output missing")
                AssertTrue(New FileInfo(outScpFromSt).Length > 0, "convert file-backed st->scp output empty")

                Dim imaWriter As New StringWriter()
                Dim imaCtx As New ToolContext With {.Output = imaWriter, .ErrorOutput = imaWriter}
                Dim imaRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", inScp, outIma}, imaCtx)
                AssertTrue(imaRc = 0, "convert file-backed scp->ima rc")
                AssertTrue(File.Exists(outIma), "convert file-backed scp->ima output missing")
                AssertTrue(New FileInfo(outIma).Length > 0, "convert file-backed scp->ima output empty")

                Dim scpFromImaWriter As New StringWriter()
                Dim scpFromImaCtx As New ToolContext With {.Output = scpFromImaWriter, .ErrorOutput = scpFromImaWriter}
                Dim scpFromImaRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outIma, outScpFromIma}, scpFromImaCtx)
                AssertTrue(scpFromImaRc = 0, "convert file-backed ima->scp rc")
                AssertTrue(File.Exists(outScpFromIma), "convert file-backed ima->scp output missing")
                AssertTrue(New FileInfo(outScpFromIma).Length > 0, "convert file-backed ima->scp output empty")

                Dim explicitImgExts = New String() {".ssd", ".dsd", ".ads", ".adm", ".adl", ".fd", ".mgt", ".sf7", ".hdm", ".xdf", ".2d", ".dsk", ".imd"}
                For Each ext In explicitImgExts
                    Dim extName = ext.TrimStart("."c)
                    Dim outExtImg = IO.Path.Combine(tmpDir, "roundtrip" & ext)
                    Dim outExtScp = IO.Path.Combine(tmpDir, "roundtrip-from-" & extName & ".scp")

                    Dim extWriter As New StringWriter()
                    Dim extCtx As New ToolContext With {.Output = extWriter, .ErrorOutput = extWriter}
                    Dim extRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", inScp, outExtImg}, extCtx)
                    AssertTrue(extRc = 0, "convert file-backed scp->" & extName & " rc")
                    AssertTrue(File.Exists(outExtImg), "convert file-backed scp->" & extName & " output missing")
                    AssertTrue(New FileInfo(outExtImg).Length > 0, "convert file-backed scp->" & extName & " output empty")

                    Dim extScpWriter As New StringWriter()
                    Dim extScpCtx As New ToolContext With {.Output = extScpWriter, .ErrorOutput = extScpWriter}
                    Dim extScpRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outExtImg, outExtScp}, extScpCtx)
                    AssertTrue(extScpRc = 0, "convert file-backed " & extName & "->scp rc")
                    AssertTrue(File.Exists(outExtScp), "convert file-backed " & extName & "->scp output missing")
                    AssertTrue(New FileInfo(outExtScp).Length > 0, "convert file-backed " & extName & "->scp output empty")
                    explicitScpOutputs("from_" & extName) = outExtScp
                Next

                Dim hdmPath = IO.Path.Combine(tmpDir, "roundtrip.hdm")
                AssertTrue(File.Exists(hdmPath), "convert file-backed hdm prerequisite missing")
                Dim dimPath = IO.Path.Combine(tmpDir, "roundtrip-from-hdm.dim")
                Dim dimHeader(255) As Byte
                dimHeader(0) = 0 ' pc98.2hd
                Dim dimSig = Encoding.ASCII.GetBytes("DIFC HEADER  ")
                Array.Copy(dimSig, 0, dimHeader, &HAB, dimSig.Length)
                Dim dimBody = File.ReadAllBytes(hdmPath)
                Dim dimImage = dimHeader.Concat(dimBody).ToArray()
                File.WriteAllBytes(dimPath, dimImage)
                Dim outScpFromDim = IO.Path.Combine(tmpDir, "roundtrip-from-dim.scp")
                Dim dimScpWriter As New StringWriter()
                Dim dimScpCtx As New ToolContext With {.Output = dimScpWriter, .ErrorOutput = dimScpWriter}
                Dim dimScpRc = action.Execute(New String() {"--tracks", "c=0:h=0", dimPath, outScpFromDim}, dimScpCtx)
                AssertTrue(dimScpRc = 0, "convert file-backed dim->scp rc")
                AssertTrue(File.Exists(outScpFromDim), "convert file-backed dim->scp output missing")
                AssertTrue(New FileInfo(outScpFromDim).Length > 0, "convert file-backed dim->scp output empty")

                Dim d88Writer As New StringWriter()
                Dim d88Ctx As New ToolContext With {.Output = d88Writer, .ErrorOutput = d88Writer}
                Dim d88Rc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", inScp, outD88}, d88Ctx)
                AssertTrue(d88Rc = 0, "convert file-backed scp->d88 rc")
                AssertTrue(File.Exists(outD88), "convert file-backed scp->d88 output missing")
                AssertTrue(New FileInfo(outD88).Length > 0, "convert file-backed scp->d88 output empty")
                Dim d88Image As New D88()
                d88Image.FromBytes(File.ReadAllBytes(outD88))
                AssertTrue(d88Image.GetTrack(0, 0) IsNot Nothing, "convert file-backed scp->d88 track missing")
                Dim scpFromD88Writer As New StringWriter()
                Dim scpFromD88Ctx As New ToolContext With {.Output = scpFromD88Writer, .ErrorOutput = scpFromD88Writer}
                Dim scpFromD88Rc = action.Execute(New String() {"--tracks", "c=0:h=0", outD88, outScpFromD88}, scpFromD88Ctx)
                AssertTrue(scpFromD88Rc = 0, "convert file-backed d88->scp rc")
                AssertTrue(File.Exists(outScpFromD88), "convert file-backed d88->scp output missing")
                AssertTrue(New FileInfo(outScpFromD88).Length > 0, "convert file-backed d88->scp output empty")

                Dim dimThrew = False
                Try
                    action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outDim}, New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                Catch ex As FatalException
                    dimThrew = ex.Message.StartsWith(outDim & ": Cannot create DIM image files", StringComparison.Ordinal)
                End Try
                AssertTrue(dimThrew, "convert file-backed dim write protection mismatch")

                Dim dmkThrew = False
                Try
                    action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outDmk}, New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                Catch ex As FatalException
                    dmkThrew = ex.Message.StartsWith(outDmk & ": Cannot create DMK image files", StringComparison.Ordinal)
                End Try
                AssertTrue(dmkThrew, "convert file-backed dmk write protection mismatch")

                Dim a2rThrew = False
                Try
                    action.Execute(New String() {"--tracks", "c=0:h=0", inScp, outA2r}, New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                Catch ex As FatalException
                    a2rThrew = ex.Message.StartsWith(outA2r & ": Cannot create A2R image files", StringComparison.Ordinal)
                End Try
                AssertTrue(a2rThrew, "convert file-backed a2r write protection mismatch")

                Dim imdWriter As New StringWriter()
                Dim imdCtx As New ToolContext With {.Output = imdWriter, .ErrorOutput = imdWriter}
                Dim imdRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", inScp, outImd}, imdCtx)
                AssertTrue(imdRc = 0, "convert file-backed scp->imd rc")
                AssertTrue(File.Exists(outImd), "convert file-backed scp->imd output missing")
                AssertTrue(New FileInfo(outImd).Length > 0, "convert file-backed scp->imd output empty")

                Dim scpFromImdWriter As New StringWriter()
                Dim scpFromImdCtx As New ToolContext With {.Output = scpFromImdWriter, .ErrorOutput = scpFromImdWriter}
                Dim scpFromImdRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outImd, outScpFromImd}, scpFromImdCtx)
                AssertTrue(scpFromImdRc = 0, "convert file-backed imd->scp rc")
                AssertTrue(File.Exists(outScpFromImd), "convert file-backed imd->scp output missing")
                AssertTrue(New FileInfo(outScpFromImd).Length > 0, "convert file-backed imd->scp output empty")

                Dim hfeWriter As New StringWriter()
                Dim hfeCtx As New ToolContext With {.Output = hfeWriter, .ErrorOutput = hfeWriter}
                Dim hfeRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", inScp, outHfe}, hfeCtx)
                AssertTrue(hfeRc = 0, "convert file-backed scp->hfe rc")
                AssertTrue(File.Exists(outHfe), "convert file-backed scp->hfe output missing")
                AssertTrue(New FileInfo(outHfe).Length > 0, "convert file-backed scp->hfe output empty")

                Dim scpFromHfeWriter As New StringWriter()
                Dim scpFromHfeCtx As New ToolContext With {.Output = scpFromHfeWriter, .ErrorOutput = scpFromHfeWriter}
                Dim scpFromHfeRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outHfe, outScpFromHfe}, scpFromHfeCtx)
                AssertTrue(scpFromHfeRc = 0, "convert file-backed hfe->scp rc")
                AssertTrue(File.Exists(outScpFromHfe), "convert file-backed hfe->scp output missing")
                AssertTrue(New FileInfo(outScpFromHfe).Length > 0, "convert file-backed hfe->scp output empty")

                Dim hfeFromImdWriter As New StringWriter()
                Dim hfeFromImdCtx As New ToolContext With {.Output = hfeFromImdWriter, .ErrorOutput = hfeFromImdWriter}
                Dim hfeFromImdRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outImd, outHfeFromImd}, hfeFromImdCtx)
                AssertTrue(hfeFromImdRc = 0, "convert file-backed imd->hfe rc")
                AssertTrue(File.Exists(outHfeFromImd), "convert file-backed imd->hfe output missing")
                AssertTrue(New FileInfo(outHfeFromImd).Length > 0, "convert file-backed imd->hfe output empty")

                Dim imdFromHfeWriter As New StringWriter()
                Dim imdFromHfeCtx As New ToolContext With {.Output = imdFromHfeWriter, .ErrorOutput = imdFromHfeWriter}
                Dim imdFromHfeRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outHfe, outImdFromHfe}, imdFromHfeCtx)
                AssertTrue(imdFromHfeRc = 0, "convert file-backed hfe->imd rc")
                AssertTrue(File.Exists(outImdFromHfe), "convert file-backed hfe->imd output missing")
                AssertTrue(New FileInfo(outImdFromHfe).Length > 0, "convert file-backed hfe->imd output empty")

                Dim imdFromRawWriter As New StringWriter()
                Dim imdFromRawCtx As New ToolContext With {.Output = imdFromRawWriter, .ErrorOutput = imdFromRawWriter}
                Dim imdFromRawRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outRaw, outImdFromRaw}, imdFromRawCtx)
                AssertTrue(imdFromRawRc = 0, "convert file-backed raw->imd rc")
                AssertTrue(File.Exists(outImdFromRaw), "convert file-backed raw->imd output missing")
                AssertTrue(New FileInfo(outImdFromRaw).Length > 0, "convert file-backed raw->imd output empty")

                Dim hfeFromRawWriter As New StringWriter()
                Dim hfeFromRawCtx As New ToolContext With {.Output = hfeFromRawWriter, .ErrorOutput = hfeFromRawWriter}
                Dim hfeFromRawRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outRaw, outHfeFromRaw}, hfeFromRawCtx)
                AssertTrue(hfeFromRawRc = 0, "convert file-backed raw->hfe rc")
                AssertTrue(File.Exists(outHfeFromRaw), "convert file-backed raw->hfe output missing")
                AssertTrue(New FileInfo(outHfeFromRaw).Length > 0, "convert file-backed raw->hfe output empty")

                Dim rawFromImdWriter As New StringWriter()
                Dim rawFromImdCtx As New ToolContext With {.Output = rawFromImdWriter, .ErrorOutput = rawFromImdWriter}
                Dim rawFromImdRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outImd, outRawFromImd}, rawFromImdCtx)
                AssertTrue(rawFromImdRc = 0, "convert file-backed imd->raw rc")
                AssertTrue(File.Exists(outRawFromImd), "convert file-backed imd->raw output missing")
                AssertTrue(New FileInfo(outRawFromImd).Length > 0, "convert file-backed imd->raw output empty")

                Dim rawFromHfeWriter As New StringWriter()
                Dim rawFromHfeCtx As New ToolContext With {.Output = rawFromHfeWriter, .ErrorOutput = rawFromHfeWriter}
                Dim rawFromHfeRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outHfe, outRawFromHfe}, rawFromHfeCtx)
                AssertTrue(rawFromHfeRc = 0, "convert file-backed hfe->raw rc")
                AssertTrue(File.Exists(outRawFromHfe), "convert file-backed hfe->raw output missing")
                AssertTrue(New FileInfo(outRawFromHfe).Length > 0, "convert file-backed hfe->raw output empty")

                Dim scpFromImdFromRawRc = action.Execute(New String() {"--tracks", "c=0:h=0", outRawFromImd, outScpFromImdFromRaw},
                                                        New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                AssertTrue(scpFromImdFromRawRc = 0, "convert file-backed imd->raw->scp rc")
                AssertTrue(File.Exists(outScpFromImdFromRaw), "convert file-backed imd->raw->scp output missing")
                AssertScpTrackComparable(outScpFromImdFromRaw, outScpFromImd, "convert file-backed imd->raw->scp")

                Dim scpFromHfeFromRawRc = action.Execute(New String() {"--tracks", "c=0:h=0", outRawFromHfe, outScpFromHfeFromRaw},
                                                        New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                AssertTrue(scpFromHfeFromRawRc = 0, "convert file-backed hfe->raw->scp rc")
                AssertTrue(File.Exists(outScpFromHfeFromRaw), "convert file-backed hfe->raw->scp output missing")
                AssertScpTrackComparable(outScpFromHfeFromRaw, outScpFromHfe, "convert file-backed hfe->raw->scp")

                Dim scpFromImdFromHfeRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outImdFromHfe, outScpFromImdFromHfe},
                                                        New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                AssertTrue(scpFromImdFromHfeRc = 0, "convert file-backed hfe->imd->scp rc")
                AssertTrue(File.Exists(outScpFromImdFromHfe), "convert file-backed hfe->imd->scp output missing")
                AssertScpTrackComparable(outScpFromImdFromHfe, outScpFromImd, "convert file-backed hfe->imd->scp")

                Dim scpFromHfeFromImdRc = action.Execute(New String() {"--format", "ibm.1440", "--tracks", "c=0:h=0", outHfeFromImd, outScpFromHfeFromImd},
                                                        New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                AssertTrue(scpFromHfeFromImdRc = 0, "convert file-backed imd->hfe->scp rc")
                AssertTrue(File.Exists(outScpFromHfeFromImd), "convert file-backed imd->hfe->scp output missing")
                AssertScpTrackComparable(outScpFromHfeFromImd, outScpFromHfe, "convert file-backed imd->hfe->scp")

                If root.ScpTrackCases IsNot Nothing AndAlso root.ScpTrackCases.Count > 0 Then
                    Dim scpOutputs As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"from_raw", outScp},
                        {"from_dmk", outScpFromDmk},
                        {"from_dsk", outScpFromDsk},
                        {"from_td0", outScpFromTd0},
                        {"from_fdi", outScpFromFdi},
                        {"from_nfd", outScpFromNfd},
                        {"from_dcp", outScpFromDcp},
                        {"from_a2r", outScpFromA2r},
                        {"from_msa", outScpFromMsa},
                        {"from_apridisk", outScpFromApridisk},
                        {"from_nsi", outScpFromNsi},
                        {"from_adf", outScpFromAdf},
                        {"from_d81", outScpFromD81},
                        {"from_d88", outScpFromD88},
                        {"from_dim", outScpFromDim},
                        {"from_d64", outScpFromD64},
                        {"from_d71", outScpFromD71},
                        {"from_d1m", outScpFromD1M},
                        {"from_d2m", outScpFromD2M},
                        {"from_d4m", outScpFromD4M},
                        {"from_do", outScpFromDo},
                        {"from_po", outScpFromPo},
                        {"from_st", outScpFromSt},
                        {"from_ima", outScpFromIma},
                        {"from_imd", outScpFromImd},
                        {"from_hfe", outScpFromHfe},
                        {"from_imd_raw", outScpFromImdFromRaw},
                        {"from_hfe_raw", outScpFromHfeFromRaw},
                        {"from_hfe_imd", outScpFromImdFromHfe},
                        {"from_imd_hfe", outScpFromHfeFromImd}
                    }
                    For Each kv In explicitScpOutputs
                        scpOutputs(kv.Key) = kv.Value
                    Next
                    Dim seenNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                    For Each c In root.ScpTrackCases
                        AssertTrue(seenNames.Add(c.Name), "convert scp_track_cases duplicate name " & c.Name)
                        AssertTrue(scpOutputs.ContainsKey(c.Name), "convert scp_track_cases unknown name " & c.Name)
                        AssertScpTrackMatchesFixture(scpOutputs(c.Name), c, "convert scp_track_cases." & c.Name)
                    Next
                End If

                Dim clobberThrew = False
                Try
                    action.Execute(New String() {"--no-clobber", "--tracks", "c=0:h=0", outRaw, outScp}, New ToolContext With {.Output = New StringWriter(), .ErrorOutput = New StringWriter()})
                Catch ex As FatalException
                    clobberThrew = ex.Message.StartsWith(outScp & ": File exists", StringComparison.Ordinal)
                End Try
                AssertTrue(clobberThrew, "convert file-backed no-clobber mismatch")
            Finally
                Try
                    Directory.Delete(tmpDir, recursive:=True)
                Catch
                End Try
            End Try
        End Sub

        Private Function MapConvertRuntimePathArg(arg As String, runtimeTmpDir As String, runtimeInScp As String) As String
            If String.Equals(arg, "in.scp", StringComparison.OrdinalIgnoreCase) Then
                Return runtimeInScp
            End If
            If arg.StartsWith("out.", StringComparison.OrdinalIgnoreCase) Then
                Return IO.Path.Combine(runtimeTmpDir, arg)
            End If
            Return arg
        End Function

        Private Sub RunEraseParity(path As String)
            Dim root = LoadJson(Of EraseFixtureRoot)(path)
            Dim action As New EraseAction()
            For Each c In root.HeaderCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-header",
                        "--tracks", c.Tracks,
                        "--revs", c.Revs.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "erase header rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Line, "erase header mismatch")
            Next

            For Each c In root.HfreqCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-hfreq",
                        "--drive-ticks", c.DriveTicks.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "erase hfreq rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines.Length >= 2, "erase hfreq output missing")
                Dim actualEraseTicks = Double.Parse(lines(0).Substring("erase_ticks=".Length), Globalization.CultureInfo.InvariantCulture)
                AssertNear(actualEraseTicks, c.EraseTicks, 1.0E-9, "erase ticks mismatch")
                AssertTrue(lines(1) = String.Format("write_flux={0}", String.Join(",", c.WriteFlux)), "erase write flux mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    Dim rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "erase runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "erase runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "erase runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunBandwidthParity(path As String)
            Dim root = LoadJson(Of BandwidthFixtureRoot)(path)
            Dim action As New BandwidthAction()

            Dim requiredWriter As New StringWriter()
            Dim requiredCtx As New ToolContext With {.Output = requiredWriter, .ErrorOutput = requiredWriter}
            Dim rc = action.Execute(New String() {"--parity-required-min"}, requiredCtx)
            AssertTrue(rc = 0, "bandwidth required-min rc")
            Dim requiredLine = requiredWriter.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            AssertTrue(requiredLine = String.Format(Globalization.CultureInfo.InvariantCulture, "required={0:R}", root.RequiredMinBandwidth), "bandwidth required-min mismatch")

            For Each c In root.BufferCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                rc = action.Execute(
                    New String() {
                        "--parity-buffer",
                        "--count", c.Count.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--seed", c.Seed.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "bandwidth buffer rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = String.Format("buffer={0}", String.Join(",", c.Buffer)), "bandwidth buffer mismatch")
            Next

            For Each c In root.EstimateCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                rc = action.Execute(
                    New String() {
                        "--parity-estimate",
                        "--min-read", c.MinRead.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--min-write", c.MinWrite.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "bandwidth estimate rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines.Length >= 2, "bandwidth estimate output missing")
                AssertTrue(lines(0) = String.Format(Globalization.CultureInfo.InvariantCulture, "estimated={0:R}", c.Estimated), "bandwidth estimate mismatch")
                AssertTrue(lines(1) = c.Status, "bandwidth status mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "bandwidth runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "bandwidth runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "bandwidth runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunRpmParity(path As String)
            Dim root = LoadJson(Of RpmFixtureRoot)(path)
            Dim action As New RpmAction()

            For Each c In root.SpeedCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-speed-line",
                        "--tpr", c.TimePerRev.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "rpm speed rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Line, "rpm speed line mismatch")
            Next

            For Each c In root.SummaryCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-summary",
                        "--samples", String.Join(",", c.TimePerRev.Select(Function(x) x.ToString(Globalization.CultureInfo.InvariantCulture)))
                    },
                    ctx)
                AssertTrue(rc = 0, "rpm summary rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines.SequenceEqual(c.ExpectedLines), "rpm summary lines mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    Dim rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "rpm runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "rpm runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "rpm runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunAlignParity(path As String)
            Dim root = LoadJson(Of AlignFixtureRoot)(path)
            Dim action As New AlignAction()

            For Each c In root.TspecCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-tspec",
                        "--cyl", c.Cyl.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--head", c.Head.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--physical-cyl", c.PhysicalCyl.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--physical-head", c.PhysicalHead.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "align tspec rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Tspec, "align tspec mismatch")
            Next

            For Each c In root.HeaderSingleCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-single-header",
                        "--tspec", c.Tspec,
                        "--reads", c.Reads.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--revs", c.Revs.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "align single header rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Line, "align single header mismatch")
            Next

            For Each c In root.HeaderMultiCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-multi-header",
                        "--cyl", c.Cyl.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--heads", String.Join(",", c.Heads),
                        "--reads", c.Reads.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--revs", c.Revs.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "align multi header rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = c.Line, "align multi header mismatch")
            Next

            For Each c In root.ValidateCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim args As New List(Of String) From {"--parity-validate"}
                If c.Tracks.Count > 0 Then
                    args.Add("--tracks")
                    args.Add(String.Join("|", c.Tracks.Select(Function(p) String.Format("{0},{1}", p.Cyl, p.Head))))
                End If
                Dim rc = action.Execute(args, ctx)
                AssertTrue(rc = 0, "align validate rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines(0) = String.Format("ok={0}", If(c.Ok, 1, 0)), "align validate ok mismatch")
                If Not c.Ok Then
                    AssertTrue(lines(1) = String.Format("error={0}", c.ErrorMessage), "align validate error mismatch")
                End If
            Next

            For Each c In root.AlternationCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-alternation",
                        "--read-num", c.ReadNumber.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--track-count", c.TrackCount.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "align alternation rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = String.Format("index={0}", c.Index), "align alternation mismatch")
            Next

            For Each c In root.HardSectorCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim rc = action.Execute(
                    New String() {
                        "--parity-hard-sectors",
                        "--hard-sectors", c.HardSectors.ToString(Globalization.CultureInfo.InvariantCulture),
                        "--revs", c.Revs.ToString(Globalization.CultureInfo.InvariantCulture)
                    },
                    ctx)
                AssertTrue(rc = 0, "align hard-sector rc")
                Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                AssertTrue(lines.Length >= 2, "align hard-sector output missing")
                AssertTrue(lines(0) = String.Format("effective_revs={0}", c.EffectiveRevs), "align hard-sector revs mismatch")
                AssertTrue(lines(1) = String.Format("effective_ticks={0}", c.EffectiveTicks), "align hard-sector ticks mismatch")
            Next

            For Each c In root.RuntimeCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                If c.Ok Then
                    Dim rc = action.Execute(c.Args, ctx)
                    AssertTrue(rc = 0, "align runtime rc")
                    Dim lines = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).ToList()
                    AssertTrue(lines.SequenceEqual(c.Lines), "align runtime output mismatch")
                Else
                    Dim threw = False
                    Try
                        Dim rc = action.Execute(c.Args, ctx)
                    Catch ex As FatalException
                        threw = ex.Message.StartsWith(c.ErrorPrefix, StringComparison.Ordinal)
                    End Try
                    AssertTrue(threw, "align runtime error mismatch")
                End If
            Next
        End Sub

        Private Sub RunTrackResolutionSharedParity(path As String)
            Dim root = LoadJson(Of TrackResolutionFixtureRoot)(path)

            Dim eraseAction As New EraseAction()
            For Each c In root.EraseCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim args As New List(Of String) From {"--parity-resolve-tracks"}
                If c.Requested IsNot Nothing Then
                    args.Add("--tracks")
                    args.Add(c.Requested)
                End If
                Dim rc = eraseAction.Execute(args, ctx)
                AssertTrue(rc = 0, "shared track resolution erase rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = String.Format("tracks={0}", c.Resolved), "shared track resolution erase mismatch")
            Next

            Dim alignAction As New AlignAction()
            For Each c In root.AlignCases
                Dim writer As New StringWriter()
                Dim ctx As New ToolContext With {.Output = writer, .ErrorOutput = writer}
                Dim args As New List(Of String) From {"--parity-resolve-tracks"}
                If c.FormatTracks IsNot Nothing Then
                    args.Add("--format-tracks")
                    args.Add(c.FormatTracks)
                End If
                If c.Requested IsNot Nothing Then
                    args.Add("--tracks")
                    args.Add(c.Requested)
                End If
                Dim rc = alignAction.Execute(args, ctx)
                AssertTrue(rc = 0, "shared track resolution align rc")
                Dim line = writer.ToString().Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                AssertTrue(line = String.Format("tracks={0}", c.Resolved), "shared track resolution align mismatch")
            Next
        End Sub

        Private Sub RunScpParity(path As String)
            Dim root = LoadJson(Of ScpFixtureRoot)(path)

            If Not String.IsNullOrEmpty(root.InvalidDisktypeMessage) Then
                Dim invalidDisktypeThrew = False
                Try
                    Dim invalid As New Scp()
                    invalid.ApplyWOpts(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"disktype", "bad_type"}
                    })
                    invalid.GetImage()
                Catch ex As FatalException
                    invalidDisktypeThrew = True
                    Dim firstLine = If(ex.Message, "").Split({vbCrLf, vbLf}, StringSplitOptions.None).FirstOrDefault()
                    AssertTrue(firstLine = root.InvalidDisktypeMessage, "scp.invalid_disktype_message mismatch")
                End Try
                AssertTrue(invalidDisktypeThrew, "scp.invalid_disktype_message did not throw")
            End If

            For Each c In root.DecodeCases
                Dim img As New Scp()
                img.FromBytes(c.ImageBytes.Select(Function(x) CByte(x)).ToArray())
                Dim parsed = TryCast(img.GetTrack(c.Cylinder, c.Head), Flux)
                AssertTrue(parsed IsNot Nothing, String.Format("scp.decode({0}).track_exists", c.Name))
                AssertSequence(parsed.IndexList, c.ExpectedIndexList, String.Format("scp.decode({0}).index_list", c.Name))
                AssertTrue(parsed.List.Count = c.ExpectedFluxCount, String.Format("scp.decode({0}).flux_count", c.Name))
                AssertSequence(parsed.List.Take(c.ExpectedFluxPrefix.Count), c.ExpectedFluxPrefix, String.Format("scp.decode({0}).flux_prefix", c.Name))
                If c.ExpectedSplice.HasValue Then
                    AssertTrue(parsed.Splice.HasValue, String.Format("scp.decode({0}).splice.exists", c.Name))
                    AssertNear(parsed.Splice.Value, c.ExpectedSplice.Value, 0.0001, String.Format("scp.decode({0}).splice", c.Name))
                Else
                    AssertTrue(Not parsed.Splice.HasValue, String.Format("scp.decode({0}).splice.none", c.Name))
                End If
            Next

            For Each c In root.EmitCases
                Dim src As New Scp()
                Dim sourceFlux As New Flux(c.InputIndexList, c.InputFluxList, Scp.SampleFrequency, indexCued:=True)
                sourceFlux.Splice = c.InputSplice
                src.EmitTrack(c.Cylinder, c.Head, sourceFlux)

                Dim payload = src.GetImage()
                Dim dst As New Scp()
                dst.FromBytes(payload)
                Dim parsed = TryCast(dst.GetTrack(c.Cylinder, c.Head), Flux)
                AssertTrue(parsed IsNot Nothing, String.Format("scp.emit({0}).track_exists", c.Name))
                AssertSequence(parsed.IndexList, c.ExpectedIndexList, String.Format("scp.emit({0}).index_list", c.Name))
                AssertTrue(parsed.List.Count = c.ExpectedFluxCount, String.Format("scp.emit({0}).flux_count", c.Name))
                AssertSequence(parsed.List.Take(c.ExpectedFluxPrefix.Count), c.ExpectedFluxPrefix, String.Format("scp.emit({0}).flux_prefix", c.Name))
                If c.ExpectedSplice.HasValue Then
                    AssertTrue(parsed.Splice.HasValue, String.Format("scp.emit({0}).splice.exists", c.Name))
                    AssertNear(parsed.Splice.Value, c.ExpectedSplice.Value, 0.0001, String.Format("scp.emit({0}).splice", c.Name))
                Else
                    AssertTrue(Not parsed.Splice.HasValue, String.Format("scp.emit({0}).splice.none", c.Name))
                End If
            Next

            For Each c In root.LayoutCases
                Dim src As New Scp()
                Dim layoutOpts As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                If Not String.IsNullOrEmpty(c.InputDiskType) Then
                    layoutOpts("disktype") = c.InputDiskType
                End If
                If c.InputLegacySingleSided Then
                    layoutOpts("legacy_ss") = "yes"
                End If
                If layoutOpts.Count > 0 Then
                    src.ApplyWOpts(layoutOpts)
                End If
                For Each t In c.InputTracks
                    Dim sourceFlux As New Flux(t.InputIndexList, t.InputFluxList, Scp.SampleFrequency, indexCued:=True)
                    sourceFlux.Splice = t.InputSplice
                    src.EmitTrack(t.Cylinder, t.Head, sourceFlux)
                Next

                Dim payload = src.GetImage()
                AssertTrue(payload.Length > 16 + 168 * 4, String.Format("scp.layout({0}).payload_size", c.Name))
                AssertTrue(payload(4) = CByte(c.ExpectedDiskType), String.Format("scp.layout({0}).disk_type", c.Name))
                AssertTrue(payload(10) = CByte(c.ExpectedSingleSided), String.Format("scp.layout({0}).single_sided", c.Name))
                AssertTrue(payload(7) = CByte(c.ExpectedEndTrack), String.Format("scp.layout({0}).end_track", c.Name))
                AssertTrue(payload(8) = CByte(c.ExpectedFlags), String.Format("scp.layout({0}).flags", c.Name))
                AssertTrue(payload(5) = CByte(c.ExpectedOutputRevs), String.Format("scp.layout({0}).output_revs", c.Name))
                Dim checksum = CInt(BitConverter.ToUInt32(payload, 12))
                AssertTrue(checksum = c.ExpectedChecksum,
                           String.Format("scp.layout({0}).checksum expected={1} actual={2}", c.Name, c.ExpectedChecksum, checksum))
                AssertTrue(payload(payload.Length - 4) = CByte(c.ExpectedFooterMagic(0)), String.Format("scp.layout({0}).footer_magic_0", c.Name))
                AssertTrue(payload(payload.Length - 3) = CByte(c.ExpectedFooterMagic(1)), String.Format("scp.layout({0}).footer_magic_1", c.Name))
                AssertTrue(payload(payload.Length - 2) = CByte(c.ExpectedFooterMagic(2)), String.Format("scp.layout({0}).footer_magic_2", c.Name))
                AssertTrue(payload(payload.Length - 1) = CByte(c.ExpectedFooterMagic(3)), String.Format("scp.layout({0}).footer_magic_3", c.Name))
                Dim appNameLen = CInt(BitConverter.ToUInt16(payload, c.ExpectedFooterOffset))
                Dim appName = Encoding.ASCII.GetString(payload, c.ExpectedFooterOffset + 2, appNameLen)
                AssertTrue(appName = c.ExpectedAppName, String.Format("scp.layout({0}).app_name", c.Name))
                Dim footerStructOffset = c.ExpectedFooterOffset + 2 + appNameLen + 1
                Dim appNameOffset = CInt(BitConverter.ToUInt32(payload, footerStructOffset + 16))
                AssertTrue(appNameOffset = c.ExpectedAppNameOffset, String.Format("scp.layout({0}).app_name_offset", c.Name))
                AssertTrue(appNameOffset = c.ExpectedFooterOffset, String.Format("scp.layout({0}).footer_offset", c.Name))
                Dim firstTrackOffset = c.ExpectedNonzeroTracks.Select(Function(trk) CInt(BitConverter.ToUInt32(payload, 16 + trk * 4))).DefaultIfEmpty(0).Min()
                AssertTrue(firstTrackOffset = c.ExpectedFirstTrackOffset, String.Format("scp.layout({0}).first_track_offset", c.Name))
                Dim wrspPresent = payload.Length >= &H2C4 AndAlso
                                  payload(&H2B0) = CByte(AscW("E"c)) AndAlso
                                  payload(&H2B1) = CByte(AscW("X"c)) AndAlso
                                  payload(&H2B2) = CByte(AscW("T"c)) AndAlso
                                  payload(&H2B3) = CByte(AscW("S"c))
                AssertTrue(wrspPresent = c.ExpectedWrspPresent, String.Format("scp.layout({0}).wrsp_present", c.Name))
                If wrspPresent Then
                    AssertTrue(c.ExpectedExtsOffset = &H2B0, String.Format("scp.layout({0}).exts_offset_expected", c.Name))
                    AssertTrue(c.ExpectedWrspOffset = &H2B8, String.Format("scp.layout({0}).wrsp_offset_expected", c.Name))
                    Dim extLen = CInt(BitConverter.ToUInt32(payload, &H2B4))
                    Dim wrspSig = Encoding.ASCII.GetString(payload, &H2B8, 4)
                    Dim wrspChunkLen = CInt(BitConverter.ToUInt32(payload, &H2BC))
                    Dim wrspFlags = CInt(BitConverter.ToUInt32(payload, &H2C0))
                    AssertTrue(extLen = c.ExpectedExtLen, String.Format("scp.layout({0}).ext_len", c.Name))
                    AssertTrue(wrspSig = c.ExpectedWrspSig, String.Format("scp.layout({0}).wrsp_sig", c.Name))
                    AssertTrue(wrspChunkLen = c.ExpectedWrspChunkLen, String.Format("scp.layout({0}).wrsp_chunk_len", c.Name))
                    AssertTrue(wrspFlags = c.ExpectedWrspFlags, String.Format("scp.layout({0}).wrsp_flags", c.Name))
                    AssertTrue(c.ExpectedWrspTable IsNot Nothing AndAlso c.ExpectedWrspTable.Count = 168,
                               String.Format("scp.layout({0}).wrsp_table_fixture", c.Name))
                    For i = 0 To 167
                        Dim v = CInt(BitConverter.ToUInt32(payload, &H2C4 + i * 4))
                        AssertTrue(v = c.ExpectedWrspTable(i), String.Format("scp.layout({0}).wrsp.table[{1}]", c.Name, i))
                    Next
                    For Each e In c.ExpectedWrspNonzeroEntries
                        Dim v = CInt(BitConverter.ToUInt32(payload, &H2C4 + e.TrackNumber * 4))
                        AssertTrue(v = e.Value, String.Format("scp.layout({0}).wrsp.t{1}", c.Name, e.TrackNumber))
                    Next
                    For Each trk In c.ExpectedWrspZeroSamples
                        Dim v = CInt(BitConverter.ToUInt32(payload, &H2C4 + trk * 4))
                        AssertTrue(v = 0, String.Format("scp.layout({0}).wrsp.t{1}_zero", c.Name, trk))
                    Next
                Else
                    AssertTrue(c.ExpectedExtsOffset = 0, String.Format("scp.layout({0}).exts_offset_none", c.Name))
                    AssertTrue(c.ExpectedWrspOffset = 0, String.Format("scp.layout({0}).wrsp_offset_none", c.Name))
                End If

                For Each t In c.ExpectedTdhTracks
                    Dim trackOffset = CInt(BitConverter.ToUInt32(payload, 16 + t.TrackNumber * 4))
                    AssertTrue(trackOffset > 0, String.Format("scp.layout({0}).tdh.track_{1}_offset", c.Name, t.TrackNumber))
                    For i = 0 To t.Entries.Count - 1
                        Dim entryOffset = trackOffset + 4 + i * 12
                        Dim ticks = CInt(BitConverter.ToUInt32(payload, entryOffset))
                        Dim words = CInt(BitConverter.ToUInt32(payload, entryOffset + 4))
                        Dim rel = CInt(BitConverter.ToUInt32(payload, entryOffset + 8))
                        AssertTrue(ticks = t.Entries(i).Ticks, String.Format("scp.layout({0}).tdh.t{1}.r{2}.ticks", c.Name, t.TrackNumber, i))
                        AssertTrue(words = t.Entries(i).Words, String.Format("scp.layout({0}).tdh.t{1}.r{2}.words", c.Name, t.TrackNumber, i))
                        AssertTrue(rel = t.Entries(i).RelativeOffset, String.Format("scp.layout({0}).tdh.t{1}.r{2}.offset", c.Name, t.TrackNumber, i))
                    Next
                Next
                For Each t In c.ExpectedDataPrefixTracks
                    Dim trackOffset = CInt(BitConverter.ToUInt32(payload, 16 + t.TrackNumber * 4))
                    AssertTrue(trackOffset > 0, String.Format("scp.layout({0}).dat.track_{1}_offset", c.Name, t.TrackNumber))
                    Dim tdh0 = trackOffset + 4
                    Dim rel = CInt(BitConverter.ToUInt32(payload, tdh0 + 8))
                    Dim datAbs = trackOffset + rel
                    For i = 0 To t.DataPrefix.Count - 1
                        AssertTrue(payload(datAbs + i) = CByte(t.DataPrefix(i)),
                                   String.Format("scp.layout({0}).dat.t{1}[{2}]", c.Name, t.TrackNumber, i))
                    Next
                Next
                For Each t In c.ExpectedDataShaTracks
                    Dim trackOffset = CInt(BitConverter.ToUInt32(payload, 16 + t.TrackNumber * 4))
                    AssertTrue(trackOffset > 0, String.Format("scp.layout({0}).sha.track_{1}_offset", c.Name, t.TrackNumber))
                    Dim tdh = c.ExpectedTdhTracks.FirstOrDefault(Function(x) x.TrackNumber = t.TrackNumber)
                    AssertTrue(tdh IsNot Nothing, String.Format("scp.layout({0}).sha.track_{1}_tdh", c.Name, t.TrackNumber))
                    Dim relStart = tdh.Entries.Min(Function(e) e.RelativeOffset)
                    Dim relEnd = tdh.Entries.Max(Function(e) e.RelativeOffset + e.Words * 2)
                    Dim dataLen = relEnd - relStart
                    AssertTrue(dataLen > 0, String.Format("scp.layout({0}).sha.track_{1}_len_positive", c.Name, t.TrackNumber))
                    AssertTrue(dataLen = t.DataLength, String.Format("scp.layout({0}).sha.track_{1}_len", c.Name, t.TrackNumber))
                    Dim segment(dataLen - 1) As Byte
                    Array.Copy(payload, trackOffset + relStart, segment, 0, dataLen)
                    Dim digest As String
                    Using sha = SHA256.Create()
                        digest = BitConverter.ToString(sha.ComputeHash(segment)).Replace("-", "").ToLowerInvariant()
                    End Using
                    AssertTrue(digest = t.DataSha256, String.Format("scp.layout({0}).sha.track_{1}", c.Name, t.TrackNumber))
                Next

                For Each trk In c.ExpectedNonzeroTracks
                    Dim off = CInt(BitConverter.ToUInt32(payload, 16 + trk * 4))
                    AssertTrue(off <> 0, String.Format("scp.layout({0}).t{1}_nonzero", c.Name, trk))
                Next
                For Each trk In c.ExpectedZeroSamples
                    Dim off = CInt(BitConverter.ToUInt32(payload, 16 + trk * 4))
                    AssertTrue(off = 0, String.Format("scp.layout({0}).t{1}_zero", c.Name, trk))
                Next
            Next
        End Sub

        Private Function BuildFixedSizeDiskDefinition(lengths As Dictionary(Of Tuple(Of Integer, Integer), Integer),
                                                     Optional cyls As Integer = 2,
                                                     Optional heads As Integer = 2) As DiskDef
            Dim def As New DiskDef()
            def.Cyls = cyls
            def.Heads = heads
            For Each pair In lengths
                def.TrackMap(pair.Key) = New FixedSizeTrackDefinition(pair.Value)
            Next
            def.Finalise()
            Return def
        End Function

        Private Function BuildMicropolis275TrackData(sectorCount As Integer, cylinder As Integer) As Byte()
            Dim data((sectorCount * 275) - 1) As Byte
            For sec = 0 To sectorCount - 1
                Dim baseOff = sec * 275
                data(baseOff) = &HFF
                data(baseOff + 1) = CByte(cylinder And &HFF)
                data(baseOff + 2) = CByte(sec And &HFF)
                For i = 3 To 268
                    data(baseOff + i) = CByte((sec * 67 + i * 53 + 25) And &HFF)
                Next
                data(baseOff + 269) = ComputeMicropolisChecksum(data.Skip(baseOff + 1).Take(268))
                For i = 270 To 274
                    data(baseOff + i) = CByte((sec * 29 + i * 11) And &HFF)
                Next
            Next
            Return data
        End Function

        Private Function ComputeMicropolisChecksum(values As IEnumerable(Of Byte)) As Byte
            Dim y As Integer = 0
            For Each x In values
                If y > 255 Then
                    y -= 255
                End If
                y += x
            Next
            Return CByte(y And &HFF)
        End Function

        Private Function HexToBytes(hex As String) As Byte()
            Dim bytes((hex.Length \ 2) - 1) As Byte
            For i = 0 To bytes.Length - 1
                bytes(i) = System.Convert.ToByte(hex.Substring(i * 2, 2), 16)
            Next
            Return bytes
        End Function

        Private Function BytesToHex(data As Byte()) As String
            Return BitConverter.ToString(data).Replace("-", String.Empty).ToLowerInvariant()
        End Function

        Private Function ComputeFileSha256(path As String) As String
            Using sha = SHA256.Create()
                Using stream = File.OpenRead(path)
                    Return BytesToHex(sha.ComputeHash(stream))
                End Using
            End Using
        End Function

        Private Function BuildConvertFixtureDmkImageBytes() As Byte()
            Dim trackDataLen = 6250
            Dim off = 200
            Dim trackData(trackDataLen - 1) As Byte
            For i = 0 To trackData.Length - 1
                trackData(i) = &H4E
            Next
            Dim idam As Byte() = {&HA1, &HA1, &HA1, &HFE, &H0, &H0, &H1, &H2}
            Array.Copy(idam, 0, trackData, off, idam.Length)
            trackData(off + 22) = &HA1
            trackData(off + 23) = &HA1
            trackData(off + 24) = &HA1
            trackData(off + 25) = &HFB

            Dim idamTable(63) As UShort
            idamTable(0) = CUShort(&H8000 Or (off + 128))

            Dim tlen = 128 + trackData.Length
            Dim header(15) As Byte
            header(0) = 0
            header(1) = 1
            header(2) = CByte(tlen And &HFF)
            header(3) = CByte((tlen >> 8) And &HFF)
            header(4) = &H10

            Dim tableBytes(127) As Byte
            For i = 0 To 63
                Dim value = CInt(idamTable(i))
                tableBytes(i * 2) = CByte(value And &HFF)
                tableBytes(i * 2 + 1) = CByte((value >> 8) And &HFF)
            Next

            Return header.Concat(tableBytes).Concat(trackData).ToArray()
        End Function

        Private Function BuildConvertFixtureEdskImageBytes() As Byte()
            Dim sectorA(511) As Byte
            Dim sectorB(511) As Byte
            For i = 0 To 511
                sectorA(i) = CByte((&H11 + i * 3) And &HFF)
                sectorB(i) = CByte((&HA7 + i * 5) And &HFF)
            Next

            Dim trackHeader = System.Text.Encoding.ASCII.GetBytes("Track-Info" & vbCrLf).ToList()
            trackHeader.AddRange(Enumerable.Repeat(CByte(0), 4))
            trackHeader.AddRange(New Byte() {0, 0, 0, 0, 2, 2, &H2A, &HE5})

            Dim sectorInfo As New List(Of Byte)()
            sectorInfo.AddRange(New Byte() {0, 0, 1, 2, 0, 0, 0, 2})
            sectorInfo.AddRange(New Byte() {0, 0, 2, 2, 0, 0, 0, 2})

            Dim trackBlock = trackHeader.Concat(sectorInfo).ToList()
            While trackBlock.Count < 256
                trackBlock.Add(0)
            End While
            trackBlock.AddRange(sectorA)
            trackBlock.AddRange(sectorB)

            Dim trackSizeUnits = trackBlock.Count \ 256

            Dim diskHeader As New List(Of Byte)()
            diskHeader.AddRange(System.Text.Encoding.ASCII.GetBytes("EXTENDED CPC DSK File" & vbCr & vbLf & "Disk-Info" & vbCr & vbLf))
            Dim creator = System.Text.Encoding.ASCII.GetBytes("GW-FIXTURE-EDSK")
            If creator.Length >= 14 Then
                diskHeader.AddRange(creator.Take(14))
            Else
                diskHeader.AddRange(creator)
                diskHeader.AddRange(Enumerable.Repeat(CByte(0), 14 - creator.Length))
            End If
            diskHeader.Add(1)
            diskHeader.Add(1)
            diskHeader.Add(0)
            diskHeader.Add(0)
            diskHeader.Add(CByte(trackSizeUnits And &HFF))
            diskHeader.AddRange(Enumerable.Repeat(CByte(0), 203))
            While diskHeader.Count < 256
                diskHeader.Add(0)
            End While

            Return diskHeader.Concat(trackBlock).ToArray()
        End Function

        Private Function BuildConvertFixtureTd0ImageBytes() As Byte()
            Dim sectorA(511) As Byte
            Dim sectorB(511) As Byte
            For i = 0 To 511
                sectorA(i) = CByte((&H21 + i * 7) And &HFF)
                sectorB(i) = CByte((&H97 + i * 9) And &HFF)
            Next

            Dim headerNoCrc As Byte() = {
                CByte(AscW("T"c)), CByte(AscW("D"c)),
                0, 0,
                &H21,
                0,
                0,
                0,
                0,
                1
            }
            Dim headerCrc = ComputeCrc16Teledisk(headerNoCrc)
            Dim td0 As New List(Of Byte)(headerNoCrc)
            td0.Add(CByte(headerCrc And &HFF))
            td0.Add(CByte((headerCrc >> 8) And &HFF))

            Dim trackNoCrc As Byte() = {2, 0, 0}
            Dim trackCrc = ComputeCrc16Teledisk(trackNoCrc) And &HFF
            td0.AddRange(trackNoCrc)
            td0.Add(CByte(trackCrc))

            Dim secACrc = ComputeCrc16Teledisk(sectorA) And &HFF
            Dim secBCrc = ComputeCrc16Teledisk(sectorB) And &HFF
            td0.AddRange(BuildTd0RawSectorRecord(0, 0, 1, 2, secACrc, sectorA))
            td0.AddRange(BuildTd0RawSectorRecord(0, 0, 2, 2, secBCrc, sectorB))
            td0.Add(&HFF)
            Return td0.ToArray()
        End Function

        Private Function BuildTd0RawSectorRecord(idC As Integer,
                                                 idH As Integer,
                                                 idR As Integer,
                                                 idN As Integer,
                                                 crcLow As Integer,
                                                 payload As Byte()) As IEnumerable(Of Byte)
            Dim out As New List(Of Byte) From {
                CByte(idC And &HFF),
                CByte(idH And &HFF),
                CByte(idR And &HFF),
                CByte(idN And &HFF),
                0,
                CByte(crcLow And &HFF)
            }
            Dim dlen = payload.Length + 1
            out.Add(CByte(dlen And &HFF))
            out.Add(CByte((dlen >> 8) And &HFF))
            out.Add(0) ' Encoding 0 = raw sector bytes
            out.AddRange(payload)
            Return out
        End Function

        Private Function ComputeCrc16Teledisk(data As IEnumerable(Of Byte)) As UShort
            ' CRC-16/TELEDISK: poly=0xA097, init=0, refin=false, refout=false, xorout=0.
            Dim crc As UInteger = 0UI
            For Each b In data
                crc = crc Xor (CUInt(b) << 8)
                For i = 0 To 7
                    If (crc And &H8000UI) <> 0UI Then
                        crc = ((crc << 1) Xor &HA097UI) And &HFFFFUI
                    Else
                        crc = (crc << 1) And &HFFFFUI
                    End If
                Next
            Next
            Return CUShort(crc And &HFFFFUI)
        End Function

        Private Function BuildConvertFixtureFdiImageBytes() As Byte()
            Dim header As New List(Of Byte)()
            header.AddRange(BitConverter.GetBytes(CUInt(0)))
            header.AddRange(BitConverter.GetBytes(CUInt(&H90)))
            header.AddRange(BitConverter.GetBytes(CUInt(32)))
            header.AddRange(New Byte() {0, 0, 0, 0})
            header.AddRange(BitConverter.GetBytes(CUInt(1024)))
            header.AddRange(BitConverter.GetBytes(CUInt(8)))
            header.AddRange(BitConverter.GetBytes(CUInt(2)))
            header.AddRange(BitConverter.GetBytes(CUInt(77)))

            Dim track0(8 * 1024 - 1) As Byte
            For i = 0 To track0.Length - 1
                track0(i) = CByte((&H33 + i * 11) And &HFF)
            Next
            Dim blank(8 * 1024 - 1) As Byte

            Dim payload As New List(Of Byte)()
            For cyl = 0 To 76
                For head = 0 To 1
                    If cyl = 0 AndAlso head = 0 Then
                        payload.AddRange(track0)
                    Else
                        payload.AddRange(blank)
                    End If
                Next
            Next

            Return header.Concat(payload).ToArray()
        End Function

        Private Function BuildConvertFixtureNfdImageBytes() As Byte()
            Dim sectorA(511) As Byte
            Dim sectorB(511) As Byte
            For i = 0 To 511
                sectorA(i) = CByte((&H41 + i * 13) And &HFF)
                sectorB(i) = CByte((&HB3 + i * 17) And &HFF)
            Next

            Dim headerSize = 288 + (163 * 26 * 16)
            Dim bytes As New List(Of Byte)()

            Dim fileId = System.Text.Encoding.ASCII.GetBytes("T98FDDIMAGE.R0")
            bytes.AddRange(fileId)
            bytes.Add(0)
            bytes.Add(0)

            Dim comment = System.Text.Encoding.ASCII.GetBytes("GW-NFD-FIXTURE")
            bytes.AddRange(comment)
            bytes.AddRange(Enumerable.Repeat(CByte(0), 256 - comment.Length))

            bytes.AddRange(BitConverter.GetBytes(CUInt(headerSize)))
            bytes.Add(0) ' write protect
            bytes.Add(2) ' heads
            bytes.AddRange(Enumerable.Repeat(CByte(0), 10))

            Dim sectorHeaders = Enumerable.Repeat(CByte(&HFF), 163 * 26 * 16).ToArray()
            Array.Copy(BuildNfdSectorHeader(0, 0, 1, 2, 1, 0, 0, 0, 0, 0, &H90), 0, sectorHeaders, 0, 16)
            Array.Copy(BuildNfdSectorHeader(0, 0, 2, 2, 1, 0, 0, 0, 0, 0, &H90), 0, sectorHeaders, 16, 16)
            bytes.AddRange(sectorHeaders)
            bytes.AddRange(sectorA)
            bytes.AddRange(sectorB)
            Return bytes.ToArray()
        End Function

        Private Function BuildNfdSectorHeader(c As Integer,
                                              h As Integer,
                                              r As Integer,
                                              n As Integer,
                                              mfm As Integer,
                                              ddam As Integer,
                                              status As Integer,
                                              st0 As Integer,
                                              st1 As Integer,
                                              st2 As Integer,
                                              pda As Integer) As Byte()
            Dim out As New List(Of Byte) From {
                CByte(c And &HFF),
                CByte(h And &HFF),
                CByte(r And &HFF),
                CByte(n And &HFF),
                CByte(mfm And &HFF),
                CByte(ddam And &HFF),
                CByte(status And &HFF),
                CByte(st0 And &HFF),
                CByte(st1 And &HFF),
                CByte(st2 And &HFF),
                CByte(pda And &HFF)
            }
            out.AddRange(Enumerable.Repeat(CByte(0), 5))
            Return out.ToArray()
        End Function

        Private Function BuildConvertFixtureDcpImageBytes() As Byte()
            Dim header = Enumerable.Repeat(CByte(0), 162).ToArray()
            header(0) = 1
            Dim track0(8 * 1024 - 1) As Byte
            For i = 0 To track0.Length - 1
                track0(i) = CByte((&H29 + i * 19) And &HFF)
            Next
            Return header.Concat(track0).ToArray()
        End Function

        Private Function BuildConvertFixtureA2rImageBytes() As Byte()
            Dim rwcp As New List(Of Byte)(Enumerable.Repeat(CByte(0), 16))
            rwcp(0) = 1
            Dim ps = BitConverter.GetBytes(CUInt(25000))
            rwcp(1) = ps(0)
            rwcp(2) = ps(1)
            rwcp(3) = ps(2)
            rwcp(4) = ps(3)

            Dim capFlux As Byte() = {20, 30, 40, 255, 12, 18, 22, 28, 34, 36}
            rwcp.Add(CByte(AscW("C"c)))
            rwcp.Add(3) ' xtiming
            rwcp.Add(0) : rwcp.Add(0) ' loc=0
            rwcp.Add(2) ' nidx
            rwcp.AddRange(BitConverter.GetBytes(CUInt(100000)))
            rwcp.AddRange(BitConverter.GetBytes(CUInt(200000)))
            rwcp.AddRange(BitConverter.GetBytes(CUInt(capFlux.Length)))
            rwcp.AddRange(capFlux)
            rwcp.Add(0)

            Dim out As New List(Of Byte)()
            out.AddRange(New Byte() {AscW("A"c), AscW("2"c), AscW("R"c), AscW("3"c), &HFF, &HA, &HD, &HA}.Select(Function(x) CByte(x)))
            out.AddRange(System.Text.Encoding.ASCII.GetBytes("RWCP"))
            out.AddRange(BitConverter.GetBytes(CUInt(rwcp.Count)))
            out.AddRange(rwcp)
            Return out.ToArray()
        End Function

        Private Function BuildConvertFixtureMsaImageBytes() As Byte()
            Dim trackData(9 * 512 - 1) As Byte
            For i = 0 To trackData.Length - 1
                trackData(i) = CByte((&H31 + i * 7) And &HFF)
            Next
            Dim out As New List(Of Byte)()
            out.Add(&HE) : out.Add(&HF)
            out.AddRange(New Byte() {0, 9}) ' spt
            out.AddRange(New Byte() {0, 0}) ' sides-1
            out.AddRange(New Byte() {0, 0}) ' start cyl
            out.AddRange(New Byte() {0, 0}) ' end cyl
            out.AddRange(BitConverter.GetBytes(CUShort(trackData.Length)).Reverse())
            out.AddRange(trackData)
            Return out.ToArray()
        End Function

        Private Function BuildConvertFixtureApridiskImageBytes() As Byte()
            Dim out As New List(Of Byte)(Enumerable.Repeat(CByte(0), 128))
            Dim sig = Encoding.ASCII.GetBytes("ACT Apricot disk image")
            For i = 0 To sig.Length - 1
                out(i) = sig(i)
            Next
            out(21) = &H1A
            out(22) = &H4

            For sec = 1 To 9
                Dim secData(511) As Byte
                For i = 0 To secData.Length - 1
                    secData(i) = CByte((&H55 + sec * 11 + i * 5) And &HFF)
                Next

                Dim rec(15) As Byte
                Dim typeBytes = BitConverter.GetBytes(&HE31D0001UI)
                Buffer.BlockCopy(typeBytes, 0, rec, 0, 4)
                Dim comp = BitConverter.GetBytes(CUShort(&H9E90))
                Buffer.BlockCopy(comp, 0, rec, 4, 2)
                Buffer.BlockCopy(BitConverter.GetBytes(CUShort(16)), 0, rec, 6, 2)
                Buffer.BlockCopy(BitConverter.GetBytes(CUInt(secData.Length)), 0, rec, 8, 4)
                rec(12) = 0
                rec(13) = CByte(sec)
                rec(14) = 0
                rec(15) = 0

                out.AddRange(rec)
                out.AddRange(secData)
            Next
            Return out.ToArray()
        End Function

        Private Function BuildConvertFixtureNsiImageBytes() As Byte()
            Dim bytes(35 * 10 * 512 - 1) As Byte
            For i = 0 To bytes.Length - 1
                bytes(i) = CByte((&H63 + i * 9) And &HFF)
            Next
            Return bytes
        End Function

        Private Function ReadScpTrackFlux(path As String) As Flux
            Dim img As New Scp()
            img.FromBytes(File.ReadAllBytes(path))
            Dim parsed = TryCast(img.GetTrack(0, 0), Flux)
            AssertTrue(parsed IsNot Nothing, path & ": missing track 0.0")
            Return parsed
        End Function

        Private Sub AssertScpTrackComparable(actualPath As String, expectedPath As String, label As String)
            Dim actual = ReadScpTrackFlux(actualPath)
            Dim expected = ReadScpTrackFlux(expectedPath)
            AssertTrue(Math.Abs(actual.IndexList.Count - expected.IndexList.Count) <= 1, label & " index count mismatch")
            AssertTrue(actual.List.Count > 0 AndAlso expected.List.Count > 0, label & " flux count mismatch")

            Dim actualTotal = If(actual.List.Count = 0, 0.0, actual.List.Sum())
            Dim expectedTotal = If(expected.List.Count = 0, 0.0, expected.List.Sum())
            Dim actualRevs = Math.Max(1, actual.IndexList.Count)
            Dim expectedRevs = Math.Max(1, expected.IndexList.Count)
            Dim actualPerRev = actualTotal / actualRevs
            Dim expectedPerRev = expectedTotal / expectedRevs
            AssertNear(actualPerRev, expectedPerRev, Math.Max(1.0, expectedPerRev * 0.1), label & " flux total mismatch")

            Dim fluxPrefixCount = Math.Min(8, Math.Min(actual.List.Count, expected.List.Count))
            For i = 0 To fluxPrefixCount - 1
                AssertNear(actual.List(i), expected.List(i), Math.Max(1.0, expected.List(i) * 0.25), label & " flux prefix mismatch")
            Next
        End Sub

        Private Sub AssertScpTrackMatchesFixture(actualPath As String, fixture As ConvertScpTrackCase, label As String)
            Dim actual = ReadScpTrackFlux(actualPath)
            AssertTrue(Math.Abs(actual.IndexList.Count - fixture.IndexCount) <= 1, label & " index count mismatch")
            AssertTrue(actual.List.Count > 0, label & " flux count mismatch")

            Dim actualRevs = Math.Max(1, actual.IndexList.Count)
            Dim expectedRevs = Math.Max(1, fixture.IndexCount)
            Dim actualPerRevCount = actual.List.Count / CDbl(actualRevs)
            Dim expectedPerRevCount = fixture.FluxCount / CDbl(expectedRevs)
            If IsStrictScpTrackCase(fixture.Name) Then
                AssertNear(actualPerRevCount,
                           expectedPerRevCount,
                           Math.Max(2.0, expectedPerRevCount * 0.15),
                           label & " flux count mismatch")
            End If

            Dim actualTotal = If(actual.List.Count = 0, 0.0, actual.List.Sum())
            Dim fluxTotalCandidates As New List(Of Double) From {fixture.FluxTotal}
            If fixture.IndexCount > 1 Then
                fluxTotalCandidates.Add(fixture.FluxTotal / fixture.IndexCount)
            End If
            Dim fluxTotalOk = fluxTotalCandidates.Any(Function(c) Math.Abs(actualTotal - c) <= Math.Max(1.0, c * 0.1))
            AssertTrue(fluxTotalOk, label & " flux total mismatch")

            Dim expectedPrefix = If(fixture.FluxPrefix, New List(Of Double)())
            Dim fluxPrefixCount = Math.Min(expectedPrefix.Count, actual.List.Count)
            Dim prefixFactor = If(IsStrictScpTrackCase(fixture.Name), 0.5, 3.0)
            For i = 0 To fluxPrefixCount - 1
                AssertNear(actual.List(i), expectedPrefix(i), Math.Max(1.0, expectedPrefix(i) * prefixFactor), label & " flux prefix mismatch")
            Next
        End Sub

        Private Function IsStrictScpTrackCase(name As String) As Boolean
            If String.IsNullOrEmpty(name) Then
                Return False
            End If
            Dim strictNames = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "from_raw"
            }
            Return strictNames.Contains(name)
        End Function

        Private Function NormalizeCliUsageLine(line As String) As String
            If String.IsNullOrEmpty(line) Then
                Return line
            End If
            Dim value = line
            If value.StartsWith("Usage: gw ", StringComparison.Ordinal) Then
                value = "Usage: gw-vb " & value.Substring("Usage: gw ".Length)
            ElseIf value.StartsWith("usage: gw ", StringComparison.Ordinal) Then
                value = "usage: gw-vb " & value.Substring("usage: gw ".Length)
            ElseIf value.StartsWith("usage: -c ", StringComparison.Ordinal) Then
                value = "usage: gw-vb " & value.Substring("usage: -c ".Length)
            ElseIf value.StartsWith("usage: generate_parity_fixtures.py ", StringComparison.Ordinal) Then
                value = "usage: gw-vb " & value.Substring("usage: generate_parity_fixtures.py ".Length)
            End If
            Return value
        End Function

        Private Function NormalizeCliUsageForCompare(line As String) As String
            If String.IsNullOrEmpty(line) Then
                Return line
            End If
            Dim value = line.ToLowerInvariant()
            value = value.Replace("<", "").Replace(">", "").Replace(",", "")
            value = value.Replace("in_file", "file").Replace("out_file", "file")
            value = value.Replace("input-file", "file").Replace("output-file", "file")
            Dim tokens = value.Split({" "c}, StringSplitOptions.RemoveEmptyEntries).Select(
                Function(t)
                    If t.Contains("file") Then
                        Return "file"
                    End If
                    Return t
                End Function).ToList()
            Return String.Join(" ", tokens)
        End Function

        Private Function NormalizeCliDetailLine(line As String) As String
            If String.IsNullOrEmpty(line) Then
                Return line
            End If
            Dim value = line.Trim()
            value = value.Replace("gw ", "gw-vb ")
            value = value.Replace("in_file", "input-file")
            value = value.Replace("out_file", "output-file")
            Return String.Join(" ", value.Split({ControlChars.Tab, " "c}, StringSplitOptions.RemoveEmptyEntries))
        End Function

        Private Function IndexOfNormalizedLineAfter(lines As List(Of String), needle As String, startIndex As Integer) As Integer
            For i = Math.Max(0, startIndex + 1) To lines.Count - 1
                If String.Equals(NormalizeCliDetailLine(lines(i)), needle, StringComparison.Ordinal) Then
                    Return i
                End If
            Next
            Return -1
        End Function

        Private Function BoolsToBytes(bits As IEnumerable(Of Boolean)) As Byte()
            Dim src = bits.ToList()
            If src.Count = 0 Then
                Return Array.Empty(Of Byte)()
            End If
            Dim output((src.Count + 7) \ 8 - 1) As Byte
            For i = 0 To src.Count - 1
                If src(i) Then
                    output(i \ 8) = CByte(output(i \ 8) Or (1 << (7 - (i Mod 8))))
                End If
            Next
            Return output
        End Function

        Private Function BuildImdFixtureCodec(formatName As String,
                                              sectorCount As Integer,
                                              bytesPerSector As Integer,
                                              n As Integer,
                                              timePerRev As Double,
                                              clock As Double,
                                              payloadByte As Func(Of Integer, Byte)) As IbmTrackFixed
            Dim sizes = Enumerable.Repeat(bytesPerSector, sectorCount).ToList()
            Dim ns = Enumerable.Repeat(n, sectorCount).ToList()
            Dim ids = Enumerable.Range(1, sectorCount).ToList()
            Dim codec As New IbmTrackFixed(formatName,
                                           0,
                                           0,
                                           sizes,
                                           ns,
                                           ids,
                                           0,
                                           imgBytesPerSector:=Nothing,
                                           timePerRev:=timePerRev,
                                           clock:=clock,
                                           emitIam:=True,
                                           gap1Override:=Nothing,
                                           gap2Override:=Nothing,
                                           gap3Override:=Nothing,
                                           gap4aOverride:=Nothing,
                                           gapByteOverride:=Nothing)
            Dim data = Enumerable.Range(0, sectorCount * bytesPerSector).Select(Function(i) payloadByte(i)).ToArray()
            codec.SetImgTrack(data)
            Return codec
        End Function

        Private Function FindDiskDefsPath() As String
            Dim current = New DirectoryInfo(Directory.GetCurrentDirectory())
            While current IsNot Nothing
                Dim candidate = Path.Combine(current.FullName, "src", "greaseweazle", "data", "diskdefs.cfg")
                If File.Exists(candidate) Then
                    Return candidate
                End If
                current = current.Parent
            End While
            Throw New FileNotFoundException("Could not locate src/greaseweazle/data/diskdefs.cfg")
        End Function

        Private Function LoadJson(Of T)(path As String) As T
            Using stream = File.OpenRead(path)
                Dim serializer = New DataContractJsonSerializer(GetType(T))
                Return CType(serializer.ReadObject(stream), T)
            End Using
        End Function

        Private Sub AssertTrue(value As Boolean, message As String)
            If Not value Then
                Throw New InvalidOperationException(message)
            End If
        End Sub

        Private Sub AssertNear(actual As Double, expected As Double, tolerance As Double, message As String)
            If Math.Abs(actual - expected) > tolerance Then
                Throw New InvalidOperationException(String.Format("{0}: expected={1}, actual={2}", message, expected, actual))
            End If
        End Sub

        Private Sub AssertSequence(actual As IEnumerable(Of Double), expected As IEnumerable(Of Double), message As String)
            Dim a = actual.ToArray()
            Dim e = expected.ToArray()
            If a.Length <> e.Length Then
                Throw New InvalidOperationException(String.Format("{0}: expected length={1}, actual length={2}", message, e.Length, a.Length))
            End If
            For i = 0 To a.Length - 1
                AssertNear(a(i), e(i), 0.0001, String.Format("{0}[{1}]", message, i))
            Next
        End Sub

        Private Class FixedSizeTrackDefinition
            Implements TrackDef

            Private ReadOnly _length As Integer

            Public Sub New(length As Integer)
                _length = length
            End Sub

            Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
                Get
                    Return 2.0
                End Get
            End Property

            Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            End Sub

            Public Sub Finalise() Implements TrackDef.Finalise
            End Sub

            Public Function MkTrack(cylinder As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
                Return New FixedSizeCodec(_length)
            End Function
        End Class

        Private Class FixedSizeCodec
            Inherits CodecBase

            Private ReadOnly _length As Integer
            Private _data As Byte()

            Public Sub New(length As Integer)
                _length = length
                _data = Enumerable.Repeat(CByte(0), _length).ToArray()
            End Sub

            Public Overrides ReadOnly Property Nsec As Integer
                Get
                    Return 1
                End Get
            End Property

            Public Overrides Function HasSec(sectorId As Integer) As Boolean
                Return sectorId = 0
            End Function

            Public Overrides Function NrMissing() As Integer
                Return If(_data.Any(Function(b) b <> 0), 0, 1)
            End Function

            Public Overrides Function GetImgTrack() As Byte()
                Return _data.ToArray()
            End Function

            Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
                Dim src = If(trackData, Array.Empty(Of Byte)())
                _data = New Byte(_length - 1) {}
                Array.Copy(src, _data, Math.Min(src.Length, _length))
                Return _length
            End Function

            Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            End Sub

            Public Overrides Function MasterTrack() As MasterTrack
                Return New MasterTrack(New Boolean() {}, 0.2)
            End Function

            Public Overrides Function SummaryString() As String
                Return "fixed-size-codec"
            End Function
        End Class

    End Module

    <DataContract>
    Public Class ErrorFixtureRoot
        <DataMember(Name:="cases")>
        Public Property Cases As List(Of ErrorCase)
    End Class

    <DataContract>
    Public Class ErrorCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="error_type")>
        Public Property ErrorType As String
        <DataMember(Name:="message")>
        Public Property Message As String
        <DataMember(Name:="result")>
        Public Property Result As String
    End Class

    <DataContract>
    Public Class FluxFixtureRoot
        <DataMember(Name:="cue_at_index")>
        Public Property CueAtIndex As FluxListCase
        <DataMember(Name:="summary")>
        Public Property Summary As FluxSummaryCase
        <DataMember(Name:="reverse")>
        Public Property Reverse As FluxReverseCase
        <DataMember(Name:="set_nr_revs")>
        Public Property SetNrRevs As FluxListCase
        <DataMember(Name:="writeout")>
        Public Property Writeout As FluxWriteoutCase
    End Class

    <DataContract>
    Public Class FluxListCase
        <DataMember(Name:="index_list")>
        Public Property IndexList As List(Of Double)
        <DataMember(Name:="flux_list")>
        Public Property FluxList As List(Of Double)
    End Class

    <DataContract>
    Public Class FluxSummaryCase
        <DataMember(Name:="summary")>
        Public Property Summary As String
        <DataMember(Name:="ticks_per_rev")>
        Public Property TicksPerRev As Double
    End Class

    <DataContract>
    Public Class FluxReverseCase
        Inherits FluxListCase
        <DataMember(Name:="index_cued")>
        Public Property IndexCued As Boolean
    End Class

    <DataContract>
    Public Class FluxWriteoutCase
        <DataMember(Name:="ticks_to_index")>
        Public Property TicksToIndex As Double
        <DataMember(Name:="list")>
        Public Property List As List(Of Double)
        <DataMember(Name:="index_cued")>
        Public Property IndexCued As Boolean
        <DataMember(Name:="terminate_at_index")>
        Public Property TerminateAtIndex As Boolean
        <DataMember(Name:="summary")>
        Public Property Summary As String
    End Class

    <DataContract>
    Public Class TrackFixtureRoot
        <DataMember(Name:="master_flux")>
        Public Property MasterFlux As TrackMasterFluxCase
        <DataMember(Name:="master_writeout")>
        Public Property MasterWriteout As TrackWriteoutCase
        <DataMember(Name:="pll_track")>
        Public Property PllTrack As TrackPllCase
    End Class

    <DataContract>
    Public Class TrackMasterFluxCase
        <DataMember(Name:="bitrate")>
        Public Property Bitrate As Double
        <DataMember(Name:="summary")>
        Public Property Summary As String
        <DataMember(Name:="flux_index_list")>
        Public Property FluxIndexList As List(Of Double)
        <DataMember(Name:="flux_list")>
        Public Property FluxList As List(Of Double)
        <DataMember(Name:="flux_splice")>
        Public Property FluxSplice As Double
        <DataMember(Name:="flux_sample_freq")>
        Public Property FluxSampleFreq As Double
    End Class

    <DataContract>
    Public Class TrackWriteoutCase
        <DataMember(Name:="ticks_to_index")>
        Public Property TicksToIndex As Double
        <DataMember(Name:="list")>
        Public Property List As List(Of Double)
        <DataMember(Name:="terminate_at_index")>
        Public Property TerminateAtIndex As Boolean
    End Class

    <DataContract>
    Public Class TrackPllCase
        <DataMember(Name:="nr_revs")>
        Public Property NrRevs As Integer
        <DataMember(Name:="rev_bits")>
        Public Property RevBits As List(Of Integer)
        <DataMember(Name:="total_bits")>
        Public Property TotalBits As Integer
        <DataMember(Name:="total_times")>
        Public Property TotalTimes As Integer
    End Class

    <DataContract>
    Public Class UsbFixtureRoot
        <DataMember(Name:="encode_decode_basic")>
        Public Property EncodeDecodeBasic As UsbEncodeDecodeCase
        <DataMember(Name:="decode_astable_failure")>
        Public Property DecodeAstableFailure As UsbDecodeErrorCase
    End Class

    <DataContract>
    Public Class UsbEncodeDecodeCase
        <DataMember(Name:="encoded")>
        Public Property Encoded As List(Of Integer)
        <DataMember(Name:="decoded_flux")>
        Public Property DecodedFlux As List(Of Double)
        <DataMember(Name:="decoded_index")>
        Public Property DecodedIndex As List(Of Double)
    End Class

    <DataContract>
    Public Class UsbDecodeErrorCase
        <DataMember(Name:="encoded")>
        Public Property Encoded As List(Of Integer)
        <DataMember(Name:="error")>
        Public Property [Error] As String
    End Class

    <DataContract>
    Public Class ToolsFixtureRoot
        <DataMember(Name:="period_cases")>
        Public Property PeriodCases As List(Of ToolPeriodCase)
        <DataMember(Name:="split_opts_cases")>
        Public Property SplitOptsCases As List(Of ToolSplitCase)
        <DataMember(Name:="columnify")>
        Public Property Columnify As ToolColumnifyCase
        <DataMember(Name:="image_suffixes")>
        Public Property ImageSuffixes As List(Of String)
        <DataMember(Name:="drive_cases")>
        Public Property DriveCases As List(Of ToolDriveCase)
        <DataMember(Name:="level_cases")>
        Public Property LevelCases As List(Of ToolLevelCase)
        <DataMember(Name:="score_port_cases")>
        Public Property ScorePortCases As List(Of ScorePortCase)
        <DataMember(Name:="find_port_cases")>
        Public Property FindPortCases As List(Of FindPortCase)
        <DataMember(Name:="valid_ser_id_cases")>
        Public Property ValidSerIdCases As List(Of ValidSerIdCase)
        <DataMember(Name:="with_drive_selected_cases")>
        Public Property WithDriveSelectedCases As List(Of WithDriveSelectedCase)
        <DataMember(Name:="range_str_cases")>
        Public Property RangeStrCases As List(Of RangeStrCase)
    End Class

    <DataContract>
    Public Class ToolPeriodCase
        <DataMember(Name:="input")>
        Public Property Input As String
        <DataMember(Name:="output")>
        Public Property Output As Double
    End Class

    <DataContract>
    Public Class ToolSplitCase
        <DataMember(Name:="input")>
        Public Property Input As String
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="opts")>
        Public Property Opts As List(Of ToolKeyValue)
    End Class

    <DataContract>
    Public Class ToolKeyValue
        <DataMember(Name:="key")>
        Public Property Key As String
        <DataMember(Name:="value")>
        Public Property Value As String
    End Class

    <DataContract>
    Public Class ToolColumnifyCase
        <DataMember(Name:="input")>
        Public Property Input As List(Of String)
        <DataMember(Name:="output")>
        Public Property Output As String
    End Class

    <DataContract>
    Public Class ToolDriveCase
        <DataMember(Name:="input")>
        Public Property Input As String
        <DataMember(Name:="bus")>
        Public Property Bus As Integer
        <DataMember(Name:="unit")>
        Public Property Unit As Integer
    End Class

    <DataContract>
    Public Class ToolLevelCase
        <DataMember(Name:="input")>
        Public Property Input As String
        <DataMember(Name:="output")>
        Public Property Output As Boolean
    End Class

    <DataContract>
    Public Class ScorePortCase
        <DataMember(Name:="port")>
        Public Property Port As PortFixture
        <DataMember(Name:="old_port")>
        Public Property OldPort As PortFixture
        <DataMember(Name:="score")>
        Public Property Score As Integer
    End Class

    <DataContract>
    Public Class PortFixture
        <DataMember(Name:="device")>
        Public Property Device As String
        <DataMember(Name:="manufacturer")>
        Public Property Manufacturer As String
        <DataMember(Name:="product")>
        Public Property Product As String
        <DataMember(Name:="vid")>
        Public Property Vid As Integer
        <DataMember(Name:="pid")>
        Public Property Pid As Integer
        <DataMember(Name:="serial_number")>
        Public Property SerialNumber As String
        <DataMember(Name:="location")>
        Public Property Location As String
        <DataMember(Name:="interface")>
        Public Property [Interface] As String
    End Class

    <DataContract>
    Public Class FindPortCase
        <DataMember(Name:="ports")>
        Public Property Ports As List(Of PortFixture)
        <DataMember(Name:="old_port")>
        Public Property OldPort As PortFixture
        <DataMember(Name:="selected")>
        Public Property Selected As String
    End Class

    <DataContract>
    Public Class ValidSerIdCase
        <DataMember(Name:="input")>
        Public Property Input As String
        <DataMember(Name:="value")>
        Public Property Value As Boolean
    End Class

    <DataContract>
    Public Class WithDriveSelectedCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="raised")>
        Public Property Raised As Boolean
        <DataMember(Name:="log")>
        Public Property Log As List(Of String)
    End Class

    Public Class FakeUsbDriveControl
        Implements UsbDriveControl

        Public Property Log As New List(Of String)()

        Public Sub SetBusType(bus As Integer) Implements UsbDriveControl.SetBusType
            Log.Add(String.Format("set_bus_type:{0}", bus))
        End Sub

        Public Sub DriveSelect(unit As Integer) Implements UsbDriveControl.DriveSelect
            Log.Add(String.Format("drive_select:{0}", unit))
        End Sub

        Public Sub DriveMotor(unit As Integer, enabled As Boolean) Implements UsbDriveControl.DriveMotor
            Log.Add(String.Format("drive_motor:{0}:{1}", unit, If(enabled, 1, 0)))
        End Sub

        Public Sub DriveDeselect() Implements UsbDriveControl.DriveDeselect
            Log.Add("drive_deselect")
        End Sub

        Public Sub Reset() Implements UsbDriveControl.Reset
            Log.Add("reset")
        End Sub
    End Class

    <DataContract>
    Public Class RangeStrCase
        <DataMember(Name:="input")>
        Public Property Input As List(Of Integer)
        <DataMember(Name:="output")>
        Public Property Output As String
    End Class

    <DataContract>
    Public Class CodecFixtureRoot
        <DataMember(Name:="diskdef_ibm_1440")>
        Public Property DiskdefIbm1440 As CodecDiskDefCase
        <DataMember(Name:="formats")>
        Public Property Formats As CodecFormatsCase
        <DataMember(Name:="ibm_track_img_cases")>
        Public Property IbmTrackImgCases As CodecIbmTrackImgCases
        <DataMember(Name:="ibm_decode_case")>
        Public Property IbmDecodeCase As CodecIbmDecodeCase
        <DataMember(Name:="ibm_emit_case")>
        Public Property IbmEmitCase As CodecIbmEmitCase
        <DataMember(Name:="bitcell_case")>
        Public Property BitcellCase As CodecBitcellCase
        <DataMember(Name:="bitcell_125_case")>
        Public Property Bitcell125Case As CodecBitcellCase
        <DataMember(Name:="bitcell_500_case")>
        Public Property Bitcell500Case As CodecBitcellCase
        <DataMember(Name:="ibm_scan_case")>
        Public Property IbmScanCase As CodecIbmScanCase
        <DataMember(Name:="ibm_scan_fm_case")>
        Public Property IbmScanFmCase As CodecIbmScanCase
        <DataMember(Name:="ibm_fm_case")>
        Public Property IbmFmCase As CodecIbmEmitCase
        <DataMember(Name:="dec_rx02_case")>
        Public Property DecRx02Case As CodecIbmEmitCase
        <DataMember(Name:="ibm_360_case")>
        Public Property Ibm360Case As CodecIbmEmitCase
        <DataMember(Name:="ibm_1200_case")>
        Public Property Ibm1200Case As CodecIbmEmitCase
        <DataMember(Name:="amiga_case")>
        Public Property AmigaCase As CodecIbmEmitCase
        <DataMember(Name:="amiga_head1_case")>
        Public Property AmigaHead1Case As CodecIbmEmitCase
        <DataMember(Name:="amiga_hd_case")>
        Public Property AmigaHdCase As CodecIbmEmitCase
        <DataMember(Name:="c64_case")>
        Public Property C64Case As CodecIbmEmitCase
        <DataMember(Name:="mac_case")>
        Public Property MacCase As CodecIbmEmitCase
        <DataMember(Name:="mac_head1_case")>
        Public Property MacHead1Case As CodecIbmEmitCase
        <DataMember(Name:="mac_400_case")>
        Public Property Mac400Case As CodecIbmEmitCase
        <DataMember(Name:="apple2_case")>
        Public Property Apple2Case As CodecIbmEmitCase
        <DataMember(Name:="northstar_case")>
        Public Property NorthstarCase As CodecIbmEmitCase
        <DataMember(Name:="northstar_mfm_case")>
        Public Property NorthstarMfmCase As CodecIbmEmitCase
        <DataMember(Name:="micropolis_case")>
        Public Property MicropolisCase As CodecIbmEmitCase
        <DataMember(Name:="micropolis_275_case")>
        Public Property Micropolis275Case As CodecIbmEmitCase
        <DataMember(Name:="hp_mmfm_case")>
        Public Property HpMmfmCase As CodecIbmEmitCase
        <DataMember(Name:="hp_mmfm_head1_case")>
        Public Property HpMmfmHead1Case As CodecIbmEmitCase
        <DataMember(Name:="datageneral_case")>
        Public Property DatageneralCase As CodecIbmEmitCase
    End Class

    <DataContract>
    Public Class CodecBitcellCase
        <DataMember(Name:="decoded_summary")>
        Public Property DecodedSummary As String
        <DataMember(Name:="decoded_bit_count")>
        Public Property DecodedBitCount As Integer
        <DataMember(Name:="decoded_time_per_rev")>
        Public Property DecodedTimePerRev As Double
        <DataMember(Name:="empty_summary")>
        Public Property EmptySummary As String
        <DataMember(Name:="empty_bit_count")>
        Public Property EmptyBitCount As Integer
        <DataMember(Name:="empty_weak_count")>
        Public Property EmptyWeakCount As Integer
    End Class

    <DataContract>
    Public Class CodecIbmScanCase
        <DataMember(Name:="decoded_summary")>
        Public Property DecodedSummary As String
        <DataMember(Name:="decoded_missing")>
        Public Property DecodedMissing As Integer
        <DataMember(Name:="decoded_img_sha256")>
        Public Property DecodedImgSha256 As String
        <DataMember(Name:="decoded_img_prefix_hex")>
        Public Property DecodedImgPrefixHex As String
        <DataMember(Name:="write_error")>
        Public Property WriteError As String
    End Class

    <DataContract>
    Public Class CodecDiskDefCase
        <DataMember(Name:="cyls")>
        Public Property Cyls As Integer
        <DataMember(Name:="heads")>
        Public Property Heads As Integer
        <DataMember(Name:="trackset")>
        Public Property Trackset As String
        <DataMember(Name:="default_revs")>
        Public Property DefaultRevs As Double
        <DataMember(Name:="track_map_size")>
        Public Property TrackMapSize As Integer
    End Class

    <DataContract>
    Public Class CodecFormatsCase
        <DataMember(Name:="count")>
        Public Property Count As Integer
        <DataMember(Name:="first10")>
        Public Property First10 As List(Of String)
        <DataMember(Name:="last10")>
        Public Property Last10 As List(Of String)
        <DataMember(Name:="print_sha256")>
        Public Property PrintSha256 As String
    End Class

    <DataContract>
    Public Class CodecIbmTrackImgCases
        <DataMember(Name:="consumed")>
        Public Property Consumed As Integer
        <DataMember(Name:="img_len")>
        Public Property ImgLen As Integer
        <DataMember(Name:="img_sha256")>
        Public Property ImgSha256 As String
        <DataMember(Name:="missing")>
        Public Property Missing As Integer
        <DataMember(Name:="has_sec0")>
        Public Property HasSec0 As Boolean
        <DataMember(Name:="has_last")>
        Public Property HasLast As Boolean
        <DataMember(Name:="consumed_short")>
        Public Property ConsumedShort As Integer
        <DataMember(Name:="img_short_len")>
        Public Property ImgShortLen As Integer
        <DataMember(Name:="img_short_sha256")>
        Public Property ImgShortSha256 As String
        <DataMember(Name:="img_short_prefix_hex")>
        Public Property ImgShortPrefixHex As String
        <DataMember(Name:="missing_short")>
        Public Property MissingShort As Integer
    End Class

    <DataContract>
    Public Class CodecIbmDecodeCase
        <DataMember(Name:="index_list")>
        Public Property IndexList As List(Of Double)
        <DataMember(Name:="flux_list")>
        Public Property FluxList As List(Of Double)
        <DataMember(Name:="sample_freq")>
        Public Property SampleFreq As Double
        <DataMember(Name:="decoded_missing")>
        Public Property DecodedMissing As Integer
        <DataMember(Name:="decoded_img_len")>
        Public Property DecodedImgLen As Integer
        <DataMember(Name:="decoded_img_sha256")>
        Public Property DecodedImgSha256 As String
        <DataMember(Name:="decoded_img_prefix_hex")>
        Public Property DecodedImgPrefixHex As String
    End Class

    <DataContract>
    Public Class CodecIbmEmitCase
        <DataMember(Name:="source_img_sha256")>
        Public Property SourceImgSha256 As String
        <DataMember(Name:="decoded_missing")>
        Public Property DecodedMissing As Integer
        <DataMember(Name:="decoded_img_sha256")>
        Public Property DecodedImgSha256 As String
        <DataMember(Name:="decoded_img_prefix_hex")>
        Public Property DecodedImgPrefixHex As String
    End Class

    <DataContract>
    Public Class ImageFixtureRoot
        <DataMember(Name:="invalid_option_message")>
        Public Property InvalidOptionMessage As String
        <DataMember(Name:="hfe_invalid_bitrate_message")>
        Public Property HfeInvalidBitrateMessage As String
        <DataMember(Name:="hfe_invalid_interface_message")>
        Public Property HfeInvalidInterfaceMessage As String
        <DataMember(Name:="hfe_invalid_encoding_message")>
        Public Property HfeInvalidEncodingMessage As String
        <DataMember(Name:="hfe_invalid_version_message")>
        Public Property HfeInvalidVersionMessage As String
        <DataMember(Name:="hfe_flux_requires_bitrate_message")>
        Public Property HfeFluxRequiresBitrateMessage As String
        <DataMember(Name:="img_cases")>
        Public Property ImgCases As ImageImgCases
        <DataMember(Name:="raw_cases")>
        Public Property RawCases As ImageRawCases
        <DataMember(Name:="d88_cases")>
        Public Property D88Cases As ImageD88Cases
        <DataMember(Name:="dmk_case")>
        Public Property DmkCase As ImageHfeCase
        <DataMember(Name:="edsk_case")>
        Public Property EdskCase As ImageHfeCase
        <DataMember(Name:="dim_case")>
        Public Property DimCase As ImageD88Case
        <DataMember(Name:="td0_case")>
        Public Property Td0Case As ImageD88Case
        <DataMember(Name:="nfd_case")>
        Public Property NfdCase As ImageD88Case
        <DataMember(Name:="dcp_case")>
        Public Property DcpCase As ImageD88Case
        <DataMember(Name:="a2r_case")>
        Public Property A2rCase As ImageFluxCase
        <DataMember(Name:="msa_case")>
        Public Property MsaCase As ImageD88Case
        <DataMember(Name:="apridisk_case")>
        Public Property ApridiskCase As ImageD88Case
        <DataMember(Name:="imd_cases")>
        Public Property ImdCases As ImageImdCases
        <DataMember(Name:="hfe_cases")>
        Public Property HfeCases As ImageHfeCases
        <DataMember(Name:="hfe_v3_cases")>
        Public Property HfeV3Cases As ImageHfeCases
    End Class

    <DataContract>
    Public Class ImageImgCases
        <DataMember(Name:="input_hex")>
        Public Property InputHex As String
        <DataMember(Name:="default_mapping")>
        Public Property DefaultMapping As List(Of ImageTrackMapRow)
        <DataMember(Name:="default_image_hex")>
        Public Property DefaultImageHex As String
        <DataMember(Name:="swapped_mapping")>
        Public Property SwappedMapping As List(Of ImageTrackMapRow)
        <DataMember(Name:="sequential_image_hex")>
        Public Property SequentialImageHex As String
        <DataMember(Name:="min_cyls_no_extend_len")>
        Public Property MinCylsNoExtendLen As Integer
        <DataMember(Name:="min_cyls_extend_len")>
        Public Property MinCylsExtendLen As Integer
    End Class

    <DataContract>
    Public Class ImageTrackMapRow
        <DataMember(Name:="cyl")>
        Public Property Cylinder As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
        <DataMember(Name:="data_hex")>
        Public Property DataHex As String
    End Class

    <DataContract>
    Public Class ImageRawCases
        <DataMember(Name:="emit_parse")>
        Public Property EmitParse As ImageRawCase
        <DataMember(Name:="multi_track_emit")>
        Public Property MultiTrackEmit As ImageRawTrackCase
        <DataMember(Name:="revs_emit_parse")>
        Public Property RevsEmitParse As ImageRawCase
        <DataMember(Name:="sck_emit_parse")>
        Public Property SckEmitParse As ImageRawCase
    End Class

    <DataContract>
    Public Class ImageRawCase
        <DataMember(Name:="input_revs")>
        Public Property InputRevolutions As Nullable(Of Integer)
        <DataMember(Name:="input_sck")>
        Public Property InputSampleClock As String
        <DataMember(Name:="input_index_list")>
        Public Property InputIndexList As List(Of Double)
        <DataMember(Name:="input_flux_list")>
        Public Property InputFluxList As List(Of Double)
        <DataMember(Name:="input_sample_freq")>
        Public Property InputSampleFreq As Double
        <DataMember(Name:="decoded_index_list")>
        Public Property DecodedIndexList As List(Of Double)
        <DataMember(Name:="decoded_flux_prefix")>
        Public Property DecodedFluxPrefix As List(Of Double)
        <DataMember(Name:="decoded_flux_count")>
        Public Property DecodedFluxCount As Integer
        <DataMember(Name:="decoded_sample_freq")>
        Public Property DecodedSampleFreq As Double
        <DataMember(Name:="file_size")>
        Public Property FileSize As Long
    End Class

    <DataContract>
    Public Class ImageRawTrackCase
        Inherits ImageRawCase
        <DataMember(Name:="cyl")>
        Public Property Cylinder As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
    End Class

    <DataContract>
    Public Class ImageD88Cases
        <DataMember(Name:="mfm")>
        Public Property Mfm As ImageD88Case
        <DataMember(Name:="fm")>
        Public Property Fm As ImageD88Case
        <DataMember(Name:="multi_index1")>
        Public Property MultiIndex1 As ImageD88Case
        <DataMember(Name:="index_error_cases")>
        Public Property IndexErrorCases As List(Of ImageD88IndexErrorCase)
    End Class

    <DataContract>
    Public Class ImageD88IndexErrorCase
        <DataMember(Name:="index")>
        Public Property Index As String
        <DataMember(Name:="error")>
        Public Property [Error] As String
    End Class

    <DataContract>
    Public Class ImageImdCases
        <DataMember(Name:="mfm")>
        Public Property Mfm As ImageD88Case
        <DataMember(Name:="fm")>
        Public Property Fm As ImageD88Case
    End Class

    <DataContract>
    Public Class ImageHfeCases
        <DataMember(Name:="mfm")>
        Public Property Mfm As ImageHfeCase
        <DataMember(Name:="mfm_uniform")>
        Public Property MfmUniform As ImageHfeCase
        <DataMember(Name:="mfm_bitrate_300")>
        Public Property MfmBitrate300 As ImageHfeCase
        <DataMember(Name:="mfm_double_step")>
        Public Property MfmDoubleStep As ImageHfeCase
        <DataMember(Name:="mfm_header_opts")>
        Public Property MfmHeaderOpts As ImageHfeCase
        <DataMember(Name:="mfm_header_opts_numeric")>
        Public Property MfmHeaderOptsNumeric As ImageHfeCase
        <DataMember(Name:="flux_bitrate_250")>
        Public Property FluxBitrate250 As ImageHfeCase
        <DataMember(Name:="fm")>
        Public Property Fm As ImageHfeCase
    End Class

    <DataContract>
    Public Class ImageD88Case
        <DataMember(Name:="file_hex")>
        Public Property FileHex As String
        <DataMember(Name:="track_present")>
        Public Property TrackPresent As Boolean
        <DataMember(Name:="track_summary")>
        Public Property TrackSummary As String
        <DataMember(Name:="track_sector_count")>
        Public Property TrackNsec As Integer
        <DataMember(Name:="track_missing")>
        Public Property TrackMissing As Integer
        <DataMember(Name:="track_img_sha256")>
        Public Property TrackImgSha256 As String
        <DataMember(Name:="track_img_prefix_hex")>
        Public Property TrackImgPrefixHex As String
        <DataMember(Name:="track01_present")>
        Public Property Track01Present As Boolean
    End Class

    <DataContract>
    Public Class ImageHfeCase
        <DataMember(Name:="file_hex")>
        Public Property FileHex As String
        <DataMember(Name:="track_present")>
        Public Property TrackPresent As Boolean
        <DataMember(Name:="bit_length")>
        Public Property BitLength As Integer
        <DataMember(Name:="time_per_rev")>
        Public Property TimePerRev As Double
        <DataMember(Name:="bitrate")>
        Public Property Bitrate As Double
        <DataMember(Name:="bits_sha256")>
        Public Property BitsSha256 As String
        <DataMember(Name:="bits_prefix_hex")>
        Public Property BitsPrefixHex As String
        <DataMember(Name:="track01_present")>
        Public Property Track01Present As Boolean
        <DataMember(Name:="expected_encoding_byte")>
        Public Property ExpectedEncodingByte As Nullable(Of Integer)
        <DataMember(Name:="expected_interface_byte")>
        Public Property ExpectedInterfaceByte As Nullable(Of Integer)
        <DataMember(Name:="expected_double_step_byte")>
        Public Property ExpectedDoubleStepByte As Nullable(Of Integer)
    End Class

    <DataContract>
    Public Class ImageFluxCase
        <DataMember(Name:="file_hex")>
        Public Property FileHex As String
        <DataMember(Name:="track_present")>
        Public Property TrackPresent As Boolean
        <DataMember(Name:="index_count")>
        Public Property IndexCount As Integer
        <DataMember(Name:="flux_count")>
        Public Property FluxCount As Integer
        <DataMember(Name:="flux_total")>
        Public Property FluxTotal As Double
        <DataMember(Name:="flux_prefix")>
        Public Property FluxPrefix As List(Of Double)
        <DataMember(Name:="track01_present")>
        Public Property Track01Present As Boolean
    End Class

    <DataContract>
    Public Class ScpFixtureRoot
        <DataMember(Name:="invalid_disktype_message")>
        Public Property InvalidDisktypeMessage As String
        <DataMember(Name:="decode_cases")>
        Public Property DecodeCases As List(Of ScpDecodeCase)
        <DataMember(Name:="emit_cases")>
        Public Property EmitCases As List(Of ScpEmitCase)
        <DataMember(Name:="layout_cases")>
        Public Property LayoutCases As List(Of ScpLayoutCase)
    End Class

    <DataContract>
    Public Class ScpDecodeCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="cyl")>
        Public Property Cylinder As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
        <DataMember(Name:="image_bytes")>
        Public Property ImageBytes As List(Of Integer)
        <DataMember(Name:="expected_index_list")>
        Public Property ExpectedIndexList As List(Of Double)
        <DataMember(Name:="expected_flux_count")>
        Public Property ExpectedFluxCount As Integer
        <DataMember(Name:="expected_flux_prefix")>
        Public Property ExpectedFluxPrefix As List(Of Double)
        <DataMember(Name:="expected_splice")>
        Public Property ExpectedSplice As Nullable(Of Double)
    End Class

    <DataContract>
    Public Class ScpEmitCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="cyl")>
        Public Property Cylinder As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
        <DataMember(Name:="input_index_list")>
        Public Property InputIndexList As List(Of Double)
        <DataMember(Name:="input_flux_list")>
        Public Property InputFluxList As List(Of Double)
        <DataMember(Name:="input_splice")>
        Public Property InputSplice As Nullable(Of Double)
        <DataMember(Name:="expected_index_list")>
        Public Property ExpectedIndexList As List(Of Double)
        <DataMember(Name:="expected_flux_count")>
        Public Property ExpectedFluxCount As Integer
        <DataMember(Name:="expected_flux_prefix")>
        Public Property ExpectedFluxPrefix As List(Of Double)
        <DataMember(Name:="expected_splice")>
        Public Property ExpectedSplice As Nullable(Of Double)
    End Class

    <DataContract>
    Public Class ScpLayoutCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="input_tracks")>
        Public Property InputTracks As List(Of ScpLayoutInputTrack)
        <DataMember(Name:="input_disktype")>
        Public Property InputDiskType As String
        <DataMember(Name:="input_legacy_ss")>
        Public Property InputLegacySingleSided As Boolean
        <DataMember(Name:="expected_disk_type")>
        Public Property ExpectedDiskType As Integer
        <DataMember(Name:="expected_single_sided")>
        Public Property ExpectedSingleSided As Integer
        <DataMember(Name:="expected_end_track")>
        Public Property ExpectedEndTrack As Integer
        <DataMember(Name:="expected_flags")>
        Public Property ExpectedFlags As Integer
        <DataMember(Name:="expected_checksum")>
        Public Property ExpectedChecksum As Integer
        <DataMember(Name:="expected_footer_magic")>
        Public Property ExpectedFooterMagic As List(Of Integer)
        <DataMember(Name:="expected_footer_offset")>
        Public Property ExpectedFooterOffset As Integer
        <DataMember(Name:="expected_app_name_offset")>
        Public Property ExpectedAppNameOffset As Integer
        <DataMember(Name:="expected_app_name")>
        Public Property ExpectedAppName As String
        <DataMember(Name:="expected_wrsp_present")>
        Public Property ExpectedWrspPresent As Boolean
        <DataMember(Name:="expected_ext_len")>
        Public Property ExpectedExtLen As Integer
        <DataMember(Name:="expected_wrsp_sig")>
        Public Property ExpectedWrspSig As String
        <DataMember(Name:="expected_wrsp_chunk_len")>
        Public Property ExpectedWrspChunkLen As Integer
        <DataMember(Name:="expected_wrsp_flags")>
        Public Property ExpectedWrspFlags As Integer
        <DataMember(Name:="expected_wrsp_table")>
        Public Property ExpectedWrspTable As List(Of Integer)
        <DataMember(Name:="expected_wrsp_nonzero_entries")>
        Public Property ExpectedWrspNonzeroEntries As List(Of ScpLayoutWrspEntry)
        <DataMember(Name:="expected_wrsp_zero_samples")>
        Public Property ExpectedWrspZeroSamples As List(Of Integer)
        <DataMember(Name:="expected_exts_offset")>
        Public Property ExpectedExtsOffset As Integer
        <DataMember(Name:="expected_wrsp_offset")>
        Public Property ExpectedWrspOffset As Integer
        <DataMember(Name:="expected_first_track_offset")>
        Public Property ExpectedFirstTrackOffset As Integer
        <DataMember(Name:="expected_output_revs")>
        Public Property ExpectedOutputRevs As Integer
        <DataMember(Name:="expected_tdh_tracks")>
        Public Property ExpectedTdhTracks As List(Of ScpLayoutTdhTrack)
        <DataMember(Name:="expected_data_prefix_tracks")>
        Public Property ExpectedDataPrefixTracks As List(Of ScpLayoutDataPrefixTrack)
        <DataMember(Name:="expected_data_sha_tracks")>
        Public Property ExpectedDataShaTracks As List(Of ScpLayoutDataShaTrack)
        <DataMember(Name:="expected_nonzero_tracks")>
        Public Property ExpectedNonzeroTracks As List(Of Integer)
        <DataMember(Name:="expected_zero_samples")>
        Public Property ExpectedZeroSamples As List(Of Integer)
    End Class

    <DataContract>
    Public Class ScpLayoutInputTrack
        <DataMember(Name:="cyl")>
        Public Property Cylinder As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
        <DataMember(Name:="index_list")>
        Public Property InputIndexList As List(Of Double)
        <DataMember(Name:="flux_list")>
        Public Property InputFluxList As List(Of Double)
        <DataMember(Name:="splice")>
        Public Property InputSplice As Nullable(Of Double)
    End Class

    <DataContract>
    Public Class ScpLayoutTdhTrack
        <DataMember(Name:="track")>
        Public Property TrackNumber As Integer
        <DataMember(Name:="entries")>
        Public Property Entries As List(Of ScpLayoutTdhEntry)
    End Class

    <DataContract>
    Public Class ScpLayoutTdhEntry
        <DataMember(Name:="ticks")>
        Public Property Ticks As Integer
        <DataMember(Name:="words")>
        Public Property Words As Integer
        <DataMember(Name:="offset")>
        Public Property RelativeOffset As Integer
    End Class

    <DataContract>
    Public Class ScpLayoutDataPrefixTrack
        <DataMember(Name:="track")>
        Public Property TrackNumber As Integer
        <DataMember(Name:="data_prefix")>
        Public Property DataPrefix As List(Of Integer)
    End Class

    <DataContract>
    Public Class ScpLayoutDataShaTrack
        <DataMember(Name:="track")>
        Public Property TrackNumber As Integer
        <DataMember(Name:="data_len")>
        Public Property DataLength As Integer
        <DataMember(Name:="data_sha256")>
        Public Property DataSha256 As String
    End Class

    <DataContract>
    Public Class ScpLayoutWrspEntry
        <DataMember(Name:="track")>
        Public Property TrackNumber As Integer
        <DataMember(Name:="value")>
        Public Property Value As Integer
    End Class

    <DataContract>
    Public Class CliFixtureRoot
        <DataMember(Name:="actions")>
        Public Property Actions As List(Of String)
        <DataMember(Name:="top_usage")>
        Public Property TopUsage As List(Of String)
        <DataMember(Name:="help_cases")>
        Public Property HelpCases As List(Of CliHelpCase)
    End Class

    <DataContract>
    Public Class CliHelpCase
        <DataMember(Name:="action")>
        Public Property Action As String
        <DataMember(Name:="usage_line")>
        Public Property UsageLine As String
        <DataMember(Name:="description_line")>
        Public Property DescriptionLine As String
        <DataMember(Name:="sections")>
        Public Property Sections As List(Of String)
        <DataMember(Name:="option_tokens")>
        Public Property OptionTokens As List(Of String)
        <DataMember(Name:="positional_lines")>
        Public Property PositionalLines As List(Of String)
        <DataMember(Name:="example_lines")>
        Public Property ExampleLines As List(Of String)
    End Class

    <DataContract>
    Public Class TrackSetFixtureRoot
        <DataMember(Name:="cases")>
        Public Property Cases As List(Of TrackSetCase)
    End Class

    <DataContract>
    Public Class TrackSetCase
        <DataMember(Name:="spec")>
        Public Property Spec As String
        <DataMember(Name:="to_string")>
        Public Property ToStringValue As String
        <DataMember(Name:="cyls")>
        Public Property Cyls As List(Of Integer)
        <DataMember(Name:="heads")>
        Public Property Heads As List(Of Integer)
        <DataMember(Name:="h_off")>
        Public Property HeadOffsets As List(Of Integer)
        <DataMember(Name:="step")>
        Public Property StepValue As Integer
        <DataMember(Name:="hswap")>
        Public Property HeadSwap As Boolean
        <DataMember(Name:="iter")>
        Public Property Iter As List(Of TrackSetIterRow)
        <DataMember(Name:="contains")>
        Public Property Contains As List(Of TrackSetContainsCase)
    End Class

    <DataContract>
    Public Class TrackSetIterRow
        <DataMember(Name:="physical_cyl")>
        Public Property PhysicalCylinder As Integer
        <DataMember(Name:="physical_head")>
        Public Property PhysicalHead As Integer
        <DataMember(Name:="cyl")>
        Public Property Cylinder As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
    End Class

    <DataContract>
    Public Class TrackSetContainsCase
        <DataMember(Name:="key")>
        Public Property Keys As List(Of Integer)
        <DataMember(Name:="value")>
        Public Property Value As Boolean
    End Class

    <DataContract>
    Public Class ActionDescriptionsFixtureRoot
        <DataMember(Name:="descriptions")>
        Public Property Descriptions As Dictionary(Of String, String)
    End Class

    <DataContract>
    Public Class PrecompFixtureRoot
        <DataMember(Name:="cases")>
        Public Property Cases As List(Of PrecompCase)
    End Class

    <DataContract>
    Public Class PrecompCase
        <DataMember(Name:="spec")>
        Public Property Spec As String
        <DataMember(Name:="repr")>
        Public Property Repr As String
        <DataMember(Name:="list")>
        Public Property List As List(Of PrecompEntry)
        <DataMember(Name:="type")>
        Public Property Type As Integer
        <DataMember(Name:="resolved")>
        Public Property Resolved As List(Of PrecompResolved)
    End Class

    <DataContract>
    Public Class PrecompEntry
        <DataMember(Name:="cyl")>
        Public Property Cylinder As Integer
        <DataMember(Name:="ns")>
        Public Property Nanoseconds As Integer
    End Class

    <DataContract>
    Public Class PrecompResolved
        <DataMember(Name:="type")>
        Public Property Type As Integer
        <DataMember(Name:="ns")>
        Public Property Nanoseconds As Double
    End Class

    <DataContract>
    Public Class ReadWriteFixtureRoot
        <DataMember(Name:="write_scale_cases")>
        Public Property WriteScaleCases As List(Of WriteScaleCase)
        <DataMember(Name:="fake_index_cases")>
        Public Property FakeIndexCases As List(Of FakeIndexCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As ReadWriteRuntimeCases
    End Class

    <DataContract>
    Public Class WriteScaleCase
        ' Python wflux.list is List[float] (track.py:276 declares
        ' `flux_ticks: float = 0`); ScaleWriteFlux now takes IEnumerable(Of Double)
        ' so the parity fixture's flux_list deserialises as Doubles too.
        <DataMember(Name:="flux_list")>
        Public Property FluxList As List(Of Double)
        <DataMember(Name:="factor")>
        Public Property Factor As Double
        <DataMember(Name:="scaled")>
        Public Property Scaled As List(Of Integer)
        <DataMember(Name:="final_remainder")>
        Public Property FinalRemainder As Double
    End Class

    <DataContract>
    Public Class FakeIndexCase
        <DataMember(Name:="revs")>
        Public Property Revs As Integer
        <DataMember(Name:="ticks")>
        Public Property Ticks As Integer
        <DataMember(Name:="drive_ticks_per_rev")>
        Public Property DriveTicksPerRev As Integer
        <DataMember(Name:="sample_freq")>
        Public Property SampleFreq As Double
        <DataMember(Name:="effective_ticks")>
        Public Property EffectiveTicks As Integer
        <DataMember(Name:="index_list")>
        Public Property IndexList As List(Of Integer)
    End Class

    <DataContract>
    Public Class ReadWriteRuntimeCases
        <DataMember(Name:="read")>
        Public Property ReadCases As List(Of ToolRuntimeCase)
        <DataMember(Name:="write")>
        Public Property WriteCases As List(Of ToolRuntimeCase)
    End Class

    <DataContract>
    Public Class InfoFixtureRoot
        <DataMember(Name:="tag_cases")>
        Public Property TagCases As List(Of InfoTagCase)
        <DataMember(Name:="line_cases")>
        Public Property LineCases As List(Of InfoLineCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of ToolRuntimeCase)
    End Class

    <DataContract>
    Public Class InfoTagCase
        <DataMember(Name:="tag")>
        Public Property Tag As String
        <DataMember(Name:="major")>
        Public Property Major As Nullable(Of Integer)
        <DataMember(Name:="minor")>
        Public Property Minor As Nullable(Of Integer)
        <DataMember(Name:="matched")>
        Public Property Matched As Boolean
    End Class

    <DataContract>
    Public Class InfoLineCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="value")>
        Public Property Value As String
        <DataMember(Name:="tab")>
        Public Property Tab As Integer
        <DataMember(Name:="output")>
        Public Property Output As String
    End Class

    <DataContract>
    Public Class ToolRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class UpdateFixtureRoot
        <DataMember(Name:="mutual_exclusion_cases")>
        Public Property MutualExclusionCases As List(Of UpdateMutualExclusionCase)
        <DataMember(Name:="download_line_cases")>
        Public Property DownloadLineCases As List(Of UpdateDownloadLineCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of UpdateRuntimeCase)
        <DataMember(Name:="extract_cases")>
        Public Property ExtractCases As List(Of UpdateExtractCase)
    End Class

    <DataContract>
    Public Class UpdateMutualExclusionCase
        <DataMember(Name:="file")>
        Public Property FileValue As String
        <DataMember(Name:="tag")>
        Public Property TagValue As String
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="error")>
        Public Property ErrorMessage As String
    End Class

    <DataContract>
    Public Class UpdateDownloadLineCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="line")>
        Public Property Line As String
    End Class

    <DataContract>
    Public Class UpdateRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class UpdateExtractCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="upd_hex")>
        Public Property UpdHex As String
        <DataMember(Name:="hw_model")>
        Public Property HwModel As Integer
        <DataMember(Name:="bootloader")>
        Public Property Bootloader As Boolean
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="version_major")>
        Public Property VersionMajor As Integer
        <DataMember(Name:="version_minor")>
        Public Property VersionMinor As Integer
        <DataMember(Name:="payload_len")>
        Public Property PayloadLen As Integer
        <DataMember(Name:="payload_sha256")>
        Public Property PayloadSha256 As String
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class PinFixtureRoot
        <DataMember(Name:="usage_lines")>
        Public Property UsageLines As List(Of String)
        <DataMember(Name:="pin_value_messages")>
        Public Property PinValueMessages As List(Of PinValueMessageCase)
        <DataMember(Name:="dispatch_cases")>
        Public Property DispatchCases As List(Of PinDispatchCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of PinRuntimeCase)
    End Class

    <DataContract>
    Public Class PinValueMessageCase
        <DataMember(Name:="pin")>
        Public Property Pin As Integer
        <DataMember(Name:="level")>
        Public Property Level As Boolean
        <DataMember(Name:="message")>
        Public Property Message As String
    End Class

    <DataContract>
    Public Class PinDispatchCase
        <DataMember(Name:="argv")>
        Public Property Argv As List(Of String)
        <DataMember(Name:="action")>
        Public Property Action As String
    End Class

    <DataContract>
    Public Class PinRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="rc", IsRequired:=False, EmitDefaultValue:=True)>
        Public Property Rc As Integer
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class ResetFixtureRoot
        <DataMember(Name:="delays_flag_cases")>
        Public Property DelaysFlagCases As List(Of ResetDelaysCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of ResetRuntimeCase)
    End Class

    <DataContract>
    Public Class ResetDelaysCase
        <DataMember(Name:="delays")>
        Public Property Delays As Boolean
        <DataMember(Name:="should_restore_delays")>
        Public Property ShouldRestoreDelays As Boolean
    End Class

    <DataContract>
    Public Class ResetRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class SeekFixtureRoot
        <DataMember(Name:="cases")>
        Public Property Cases As List(Of SeekCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of SeekRuntimeCase)
    End Class

    <DataContract>
    Public Class SeekCase
        <DataMember(Name:="cylinder")>
        Public Property Cylinder As Integer
        <DataMember(Name:="force")>
        Public Property Force As Boolean
        <DataMember(Name:="prompt_needed")>
        Public Property PromptNeeded As Boolean
        <DataMember(Name:="prompt_text")>
        Public Property PromptText As String
    End Class

    <DataContract>
    Public Class SeekRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class DelaysFixtureRoot
        <DataMember(Name:="print_info_cases")>
        Public Property PrintInfoCases As List(Of DelaysPrintInfoCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of DelaysRuntimeCase)
    End Class

    <DataContract>
    Public Class DelaysPrintInfoCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="value")>
        Public Property Value As String
        <DataMember(Name:="tab")>
        Public Property Tab As Integer
        <DataMember(Name:="line")>
        Public Property Line As String
    End Class

    <DataContract>
    Public Class DelaysRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class CleanFixtureRoot
        <DataMember(Name:="pattern_cases")>
        Public Property PatternCases As List(Of CleanPatternCase)
        <DataMember(Name:="seek_cases")>
        Public Property SeekCases As List(Of CleanSeekCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of CleanRuntimeCase)
    End Class

    <DataContract>
    Public Class CleanPatternCase
        <DataMember(Name:="cyls")>
        Public Property Cyls As Integer
        <DataMember(Name:="passes")>
        Public Property Passes As Integer
        <DataMember(Name:="step")>
        Public Property StepValue As Integer
        <DataMember(Name:="pass_sequences")>
        Public Property PassSequences As List(Of List(Of Integer))
    End Class

    <DataContract>
    Public Class CleanSeekCase
        <DataMember(Name:="cylinder")>
        Public Property Cylinder As Integer
        <DataMember(Name:="cyls")>
        Public Property Cyls As Integer
        <DataMember(Name:="seek_target")>
        Public Property SeekTarget As Integer
    End Class

    <DataContract>
    Public Class CleanRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class ConvertFixtureRoot
        <DataMember(Name:="track_summary_cases")>
        Public Property TrackSummaryCases As List(Of ConvertTrackSummaryCase)
        <DataMember(Name:="convert_header_cases")>
        Public Property ConvertHeaderCases As List(Of ConvertHeaderCase)
        <DataMember(Name:="format_resolution_cases")>
        Public Property FormatResolutionCases As List(Of ConvertFormatResolutionCase)
        <DataMember(Name:="track_resolution_cases")>
        Public Property TrackResolutionCases As List(Of ConvertTrackResolutionCase)
        <DataMember(Name:="loop_cases")>
        Public Property LoopCases As List(Of ConvertLoopCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of ConvertRuntimeCase)
        <DataMember(Name:="scp_track_cases")>
        Public Property ScpTrackCases As List(Of ConvertScpTrackCase)
    End Class

    <DataContract>
    Public Class ConvertTrackSummaryCase
        <DataMember(Name:="cyl")>
        Public Property Cyl As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
        <DataMember(Name:="physical_cyl")>
        Public Property PhysicalCyl As Integer
        <DataMember(Name:="physical_head")>
        Public Property PhysicalHead As Integer
        <DataMember(Name:="summary")>
        Public Property Summary As String
    End Class

    <DataContract>
    Public Class ConvertHeaderCase
        <DataMember(Name:="tracks")>
        Public Property Tracks As String
        <DataMember(Name:="out_tracks")>
        Public Property OutTracks As String
        <DataMember(Name:="line")>
        Public Property Line As String
    End Class

    <DataContract>
    Public Class ConvertFormatResolutionCase
        <DataMember(Name:="explicit_format")>
        Public Property ExplicitFormat As String
        <DataMember(Name:="input_default")>
        Public Property InputDefault As String
        <DataMember(Name:="output_default")>
        Public Property OutputDefault As String
        <DataMember(Name:="resolved_format")>
        Public Property ResolvedFormat As String
    End Class

    <DataContract>
    Public Class ConvertTrackResolutionCase
        <DataMember(Name:="format_tracks")>
        Public Property FormatTracks As String
        <DataMember(Name:="tracks")>
        Public Property Tracks As String
        <DataMember(Name:="out_tracks")>
        Public Property OutTracks As String
        <DataMember(Name:="resolved_tracks")>
        Public Property ResolvedTracks As String
        <DataMember(Name:="resolved_out_tracks")>
        Public Property ResolvedOutTracks As String
    End Class

    <DataContract>
    Public Class ConvertLoopCase
        <DataMember(Name:="out_tracks")>
        Public Property OutTracks As List(Of ConvertLoopOutTrack)
        <DataMember(Name:="in_tracks")>
        Public Property InTracks As List(Of ConvertLoopTrack)
        <DataMember(Name:="available_tracks")>
        Public Property AvailableTracks As List(Of ConvertLoopTrack)
        <DataMember(Name:="cache_enabled")>
        Public Property CacheEnabled As Boolean
        <DataMember(Name:="process_calls")>
        Public Property ProcessCalls As List(Of String)
        <DataMember(Name:="emit_targets")>
        Public Property EmitTargets As List(Of String)
        <DataMember(Name:="cache_keys")>
        Public Property CacheKeys As List(Of String)
    End Class

    <DataContract>
    Public Class ConvertLoopTrack
        <DataMember(Name:="cyl")>
        Public Property Cyl As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
    End Class

    <DataContract>
    Public Class ConvertLoopOutTrack
        Inherits ConvertLoopTrack
        <DataMember(Name:="physical_cyl")>
        Public Property PhysicalCyl As Integer
        <DataMember(Name:="physical_head")>
        Public Property PhysicalHead As Integer
    End Class

    <DataContract>
    Public Class ConvertRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class ConvertScpTrackCase
        <DataMember(Name:="name")>
        Public Property Name As String
        <DataMember(Name:="index_count")>
        Public Property IndexCount As Integer
        <DataMember(Name:="flux_count")>
        Public Property FluxCount As Integer
        <DataMember(Name:="flux_total")>
        Public Property FluxTotal As Double
        <DataMember(Name:="flux_prefix")>
        Public Property FluxPrefix As List(Of Double)
    End Class

    <DataContract>
    Public Class EraseFixtureRoot
        <DataMember(Name:="header_cases")>
        Public Property HeaderCases As List(Of EraseHeaderCase)
        <DataMember(Name:="hfreq_cases")>
        Public Property HfreqCases As List(Of EraseHfreqCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of EraseRuntimeCase)
    End Class

    <DataContract>
    Public Class EraseHeaderCase
        <DataMember(Name:="tracks")>
        Public Property Tracks As String
        <DataMember(Name:="revs")>
        Public Property Revs As Integer
        <DataMember(Name:="line")>
        Public Property Line As String
    End Class

    <DataContract>
    Public Class EraseHfreqCase
        <DataMember(Name:="drive_ticks")>
        Public Property DriveTicks As Double
        <DataMember(Name:="erase_ticks")>
        Public Property EraseTicks As Double
        <DataMember(Name:="write_flux")>
        Public Property WriteFlux As List(Of Integer)
    End Class

    <DataContract>
    Public Class EraseRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class BandwidthFixtureRoot
        <DataMember(Name:="buffer_cases")>
        Public Property BufferCases As List(Of BandwidthBufferCase)
        <DataMember(Name:="required_min_bw")>
        Public Property RequiredMinBandwidth As Double
        <DataMember(Name:="estimate_cases")>
        Public Property EstimateCases As List(Of BandwidthEstimateCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of BandwidthRuntimeCase)
    End Class

    <DataContract>
    Public Class BandwidthBufferCase
        <DataMember(Name:="nr")>
        Public Property Count As Integer
        <DataMember(Name:="seed")>
        Public Property Seed As UInteger
        <DataMember(Name:="buffer")>
        Public Property Buffer As List(Of Integer)
    End Class

    <DataContract>
    Public Class BandwidthEstimateCase
        <DataMember(Name:="min_read")>
        Public Property MinRead As Double
        <DataMember(Name:="min_write")>
        Public Property MinWrite As Double
        <DataMember(Name:="estimated")>
        Public Property Estimated As Double
        <DataMember(Name:="status")>
        Public Property Status As String
    End Class

    <DataContract>
    Public Class BandwidthRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class RpmFixtureRoot
        <DataMember(Name:="speed_cases")>
        Public Property SpeedCases As List(Of RpmSpeedCase)
        <DataMember(Name:="summary_cases")>
        Public Property SummaryCases As List(Of RpmSummaryCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of RpmRuntimeCase)
    End Class

    <DataContract>
    Public Class RpmSpeedCase
        <DataMember(Name:="tpr")>
        Public Property TimePerRev As Double
        <DataMember(Name:="line")>
        Public Property Line As String
    End Class

    <DataContract>
    Public Class RpmSummaryCase
        <DataMember(Name:="time_per_rev")>
        Public Property TimePerRev As List(Of Double)
        <DataMember(Name:="fastest")>
        Public Property Fastest As String
        <DataMember(Name:="mean")>
        Public Property Mean As String
        <DataMember(Name:="median")>
        Public Property Median As String
        <DataMember(Name:="slowest")>
        Public Property Slowest As String

        Public ReadOnly Property ExpectedLines As List(Of String)
            Get
                Return New List(Of String) From {Fastest, Mean, Median, Slowest}
            End Get
        End Property
    End Class

    <DataContract>
    Public Class RpmRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class AlignFixtureRoot
        <DataMember(Name:="tspec_cases")>
        Public Property TspecCases As List(Of AlignTspecCase)
        <DataMember(Name:="header_single_cases")>
        Public Property HeaderSingleCases As List(Of AlignSingleHeaderCase)
        <DataMember(Name:="header_multi_cases")>
        Public Property HeaderMultiCases As List(Of AlignMultiHeaderCase)
        <DataMember(Name:="validate_cases")>
        Public Property ValidateCases As List(Of AlignValidateCase)
        <DataMember(Name:="alternation_cases")>
        Public Property AlternationCases As List(Of AlignAlternationCase)
        <DataMember(Name:="hard_sector_cases")>
        Public Property HardSectorCases As List(Of AlignHardSectorCase)
        <DataMember(Name:="runtime_cases")>
        Public Property RuntimeCases As List(Of AlignRuntimeCase)
    End Class

    <DataContract>
    Public Class AlignTspecCase
        <DataMember(Name:="cyl")>
        Public Property Cyl As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
        <DataMember(Name:="physical_cyl")>
        Public Property PhysicalCyl As Integer
        <DataMember(Name:="physical_head")>
        Public Property PhysicalHead As Integer
        <DataMember(Name:="tspec")>
        Public Property Tspec As String
    End Class

    <DataContract>
    Public Class AlignSingleHeaderCase
        <DataMember(Name:="tspec")>
        Public Property Tspec As String
        <DataMember(Name:="reads")>
        Public Property Reads As Integer
        <DataMember(Name:="revs")>
        Public Property Revs As Integer
        <DataMember(Name:="line")>
        Public Property Line As String
    End Class

    <DataContract>
    Public Class AlignMultiHeaderCase
        <DataMember(Name:="cyl")>
        Public Property Cyl As Integer
        <DataMember(Name:="heads")>
        Public Property Heads As List(Of Integer)
        <DataMember(Name:="reads")>
        Public Property Reads As Integer
        <DataMember(Name:="revs")>
        Public Property Revs As Integer
        <DataMember(Name:="line")>
        Public Property Line As String
    End Class

    <DataContract>
    Public Class AlignValidateCase
        <DataMember(Name:="tracks")>
        Public Property Tracks As List(Of AlignValidateTrack)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="error")>
        Public Property ErrorMessage As String
    End Class

    <DataContract>
    Public Class AlignValidateTrack
        <DataMember(Name:="cyl")>
        Public Property Cyl As Integer
        <DataMember(Name:="head")>
        Public Property Head As Integer
    End Class

    <DataContract>
    Public Class AlignAlternationCase
        <DataMember(Name:="read_num")>
        Public Property ReadNumber As Integer
        <DataMember(Name:="track_count")>
        Public Property TrackCount As Integer
        <DataMember(Name:="index")>
        Public Property Index As Integer
    End Class

    <DataContract>
    Public Class AlignHardSectorCase
        <DataMember(Name:="hard_sectors")>
        Public Property HardSectors As Integer
        <DataMember(Name:="revs")>
        Public Property Revs As Integer
        <DataMember(Name:="effective_revs")>
        Public Property EffectiveRevs As Integer
        <DataMember(Name:="effective_ticks")>
        Public Property EffectiveTicks As Integer
    End Class

    <DataContract>
    Public Class AlignRuntimeCase
        <DataMember(Name:="args")>
        Public Property Args As List(Of String)
        <DataMember(Name:="ok")>
        Public Property Ok As Boolean
        <DataMember(Name:="lines")>
        Public Property Lines As List(Of String)
        <DataMember(Name:="error_prefix")>
        Public Property ErrorPrefix As String
    End Class

    <DataContract>
    Public Class TrackResolutionFixtureRoot
        <DataMember(Name:="erase_cases")>
        Public Property EraseCases As List(Of SharedEraseTrackResolutionCase)
        <DataMember(Name:="align_cases")>
        Public Property AlignCases As List(Of SharedAlignTrackResolutionCase)
    End Class

    <DataContract>
    Public Class SharedEraseTrackResolutionCase
        <DataMember(Name:="requested")>
        Public Property Requested As String
        <DataMember(Name:="resolved")>
        Public Property Resolved As String
    End Class

    <DataContract>
    Public Class SharedAlignTrackResolutionCase
        <DataMember(Name:="format_tracks")>
        Public Property FormatTracks As String
        <DataMember(Name:="requested")>
        Public Property Requested As String
        <DataMember(Name:="resolved")>
        Public Property Resolved As String
    End Class

End Namespace
