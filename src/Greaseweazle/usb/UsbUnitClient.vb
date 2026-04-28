Imports Greaseweazle.Core

Namespace Greaseweazle.Infrastructure

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB interface declaration SerialTransport)
    Public Interface SerialTransport
        Property BaudRate As Integer
        ReadOnly Property BytesAvailable As Integer
        ' Python map: src/greaseweazle/usb.py::Unit._send_cmd
        Sub Write(data As Byte())
        ' Python map: src/greaseweazle/usb.py::Unit._send_cmd
        Function Read(count As Integer) As Byte()
        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Sub ResetInputBuffer()
        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Sub ResetOutputBuffer()
        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Sub Open()
        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Sub Close()
    End Interface

    ' Python map: src/greaseweazle/usb.py::Unit
    Public Class Unit

        ' Python map: src/greaseweazle/usb.py::Unit.__init__
        Public Sub New(serial As SerialTransport)
            Me.Serial = serial
            ' Python's Unit.__init__ unconditionally calls self.reset() to put
            ' the serial line into a known state before any commands are sent,
            ' and lets any failure propagate from the constructor. We mirror that
            ' here so transport faults during initialisation surface immediately
            ' instead of being absorbed.
            If serial IsNot Nothing Then
                Reset()
            End If
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.ser
        Public Property Serial As SerialTransport
        ' Python map: src/greaseweazle/usb.py::Unit.sample_freq
        Public Property SampleFreq As Double
        ' Python map: src/greaseweazle/tools/util.py::usb_open (persisted selected device path)
        Public Property PortDevice As String
        ' Python map: src/greaseweazle/tools/util.py::port_info.serial_number (stored for reopen scoring)
        Public Property PortSerialNumber As String
        ' Python map: src/greaseweazle/tools/util.py::port_info.location (stored for reopen scoring)
        Public Property PortLocation As String
        ' Python map: src/greaseweazle/tools/util.py::usb_open (computed capability flag)
        Public Property JumperlessUpdate As Boolean
        ' Python map: src/greaseweazle/tools/util.py::usb_open (computed capability flag)
        Public Property CanModeSwitch As Boolean
        ' Python map: src/greaseweazle/usb.py::Unit.major
        Public Property Major As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.minor
        Public Property Minor As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.version
        Public Property Version As Tuple(Of Integer, Integer)
        ' Python map: src/greaseweazle/usb.py::Unit.max_cmd
        Public Property MaxCmd As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.hw_model
        Public Property HwModel As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.hw_submodel
        Public Property HwSubmodel As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.usb_speed
        Public Property UsbSpeed As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.mcu_id
        Public Property McuId As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.mcu_mhz
        Public Property McuMhz As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.mcu_sram_kb
        Public Property McuSramKb As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.usb_buf_kb
        Public Property UsbBufferKb As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.update_mode
        Public Property UpdateMode As Boolean
        ' Python map: src/greaseweazle/usb.py::Unit.update_needed
        Public Property UpdateNeeded As Boolean
        ' Python map: src/greaseweazle/usb.py::Unit.update_jumpered
        Public Property UpdateJumpered As Boolean

        ' Python map: src/greaseweazle/usb.py::Unit.__init__
        Public Function ReadFirmwareInfo() As FirmwareInfo
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetInfo), 3, CByte(UsbProtocol.GetInfo.Firmware)})
            Dim rsp = ReadExact(32)
            Dim major = rsp(0)
            Dim minor = rsp(1)
            Dim isMainFirmware = rsp(2)
            Dim maxCmd = rsp(3)
            Dim sampleFreq = BitConverter.ToUInt32(rsp, 4)
            Dim hwModel = rsp(8)
            Dim hwSubmodel = rsp(9)
            Dim usbSpeed = rsp(10)
            Dim mcuId = rsp(11)
            Dim mcuMhz = BitConverter.ToUInt16(rsp, 12)
            Dim mcuSramKb = BitConverter.ToUInt16(rsp, 14)
            Dim usbBufKb = BitConverter.ToUInt16(rsp, 16)
            If hwModel = 0 Then
                hwModel = 1
            End If
            Dim updateMode = (isMainFirmware = 0)
            Dim updateNeeded = Not updateMode AndAlso
                               ((major < UsbProtocol.EarliestSupportedFirmwareMajor) OrElse
                                (major = UsbProtocol.EarliestSupportedFirmwareMajor AndAlso
                                 minor < UsbProtocol.EarliestSupportedFirmwareMinor))
            ' Python: when running update firmware (`update_mode == True`) the
            ' sample_freq field is repurposed for the update-jumpered bit and
            ' is then explicitly `del`d from the unit. Mirror that by zeroing
            ' SampleFreq so callers can't accidentally use stale ticks.
            If updateMode Then
                Me.SampleFreq = 0.0
            Else
                Me.SampleFreq = CDbl(sampleFreq)
            End If
            Return New FirmwareInfo With {
                .Major = major,
                .Minor = minor,
                .IsMainFirmware = (isMainFirmware <> 0),
                .Version = Tuple.Create(CInt(major), CInt(minor)),
                .MaxCmd = maxCmd,
                .SampleFreq = If(updateMode, 0.0, CDbl(sampleFreq)),
                .HwModel = hwModel,
                .HwSubmodel = hwSubmodel,
                .UsbSpeed = usbSpeed,
                .McuId = mcuId,
                .McuMhz = mcuMhz,
                .McuSramKb = mcuSramKb,
                .UsbBufferKb = usbBufKb,
                .UpdateMode = updateMode,
                .UpdateNeeded = updateNeeded,
                .UpdateJumpered = If(updateMode, (CInt(sampleFreq) And 1) <> 0, False)
            }
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (copy unpacked firmware info to unit fields)
        Public Sub ApplyFirmwareInfo(info As FirmwareInfo)
            If info Is Nothing Then
                Return
            End If
            Major = info.Major
            Minor = info.Minor
            Version = info.Version
            MaxCmd = info.MaxCmd
            SampleFreq = info.SampleFreq
            HwModel = info.HwModel
            HwSubmodel = info.HwSubmodel
            UsbSpeed = info.UsbSpeed
            McuId = info.McuId
            McuMhz = info.McuMhz
            McuSramKb = info.McuSramKb
            UsbBufferKb = info.UsbBufferKb
            UpdateMode = info.UpdateMode
            UpdateNeeded = info.UpdateNeeded
            UpdateJumpered = info.UpdateJumpered
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.source_bytes
        Public Function SourceBytes(nr As Integer, seed As UInteger) As Byte()
            Dim cmd As New List(Of Byte) From {
                CByte(UsbProtocol.Cmd.SourceBytes),
                10
            }
            cmd.AddRange(BitConverter.GetBytes(CUInt(nr)))
            cmd.AddRange(BitConverter.GetBytes(seed))
            SendCmd(cmd.ToArray())
            Return ReadExact(nr)
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.sink_bytes
        Public Function SinkBytes(dat As Byte(), seed As UInteger) As Integer
            Dim cmd As New List(Of Byte) From {
                CByte(UsbProtocol.Cmd.SinkBytes),
                10
            }
            cmd.AddRange(BitConverter.GetBytes(CUInt(dat.Length)))
            cmd.AddRange(BitConverter.GetBytes(seed))
            SendCmd(cmd.ToArray())
            Serial.Write(dat)
            Return ReadExact(1)(0)
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.bw_stats
        Public Function BwStats() As Tuple(Of Double, Double)
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetInfo), 3, CByte(UsbProtocol.GetInfo.BandwidthStats)})
            Dim rsp = ReadExact(32)
            Dim minBytes = BitConverter.ToUInt32(rsp, 0)
            Dim minUsecs = BitConverter.ToUInt32(rsp, 4)
            Dim maxBytes = BitConverter.ToUInt32(rsp, 8)
            Dim maxUsecs = BitConverter.ToUInt32(rsp, 12)
            Dim minBw = (8.0 * minBytes) / minUsecs
            Dim maxBw = (8.0 * maxBytes) / maxUsecs
            Return Tuple.Create(minBw, maxBw)
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.get_params
        Public Function GetParams(idx As UsbProtocol.Params, nr As Integer) As Byte()
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetParams), 4, CByte(idx), CByte(nr)})
            Return ReadExact(nr)
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.set_params
        Public Sub SetParams(idx As UsbProtocol.Params, dat As Byte())
            Dim cmd As New List(Of Byte) From {
                CByte(UsbProtocol.Cmd.SetParams),
                CByte(3 + dat.Length),
                CByte(idx)
            }
            cmd.AddRange(dat)
            SendCmd(cmd.ToArray())
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.get_current_drive_info
        Public Function GetCurrentDriveInfo() As DriveInfo
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetInfo), 3, CByte(UsbProtocol.GetInfo.CurrentDrive)})
            Return New DriveInfo(ReadExact(32))
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Public Sub Reset()
            ' Ensure the port is open before issuing the baudrate-magic
            ' firmware reset. On the Ctrl-C abort path the InterruptControl
            ' handler closes the port from a non-worker thread to unblock the
            ' worker's pending Read; calling Open() here is a no-op when the
            ' port is already open and reopens it otherwise so the
            ' baudrate-magic + drive_motor(False) + drive_deselect() cleanup
            ' performed by with_drive_selected can actually be delivered to
            ' the firmware. Python doesn't need this step because
            ' KeyboardInterrupt is raised on the same thread as the in-flight
            ' Read, so the port is never closed externally.
            Serial.Open()
            Serial.ResetOutputBuffer()
            Serial.BaudRate = CInt(UsbProtocol.ControlCmd.ClearComms)
            Serial.BaudRate = CInt(UsbProtocol.ControlCmd.Normal)
            Serial.ResetInputBuffer()
            Serial.Close()
            Serial.Open()
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.power_on_reset
        Public Sub PowerOnReset()
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.Reset), 2})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.read_track
        Public Function ReadTrack(revs As Integer,
                                  Optional ticks As Integer = 0,
                                  Optional nrRetries As Integer = 5) As Flux
            Dim retry = 0
            Dim stream As Byte() = Nothing
            While True
                Try
                    stream = ReadTrackRaw(revs, ticks)
                Catch ex As CmdError
                    If ex.Code = UsbProtocol.Ack.FluxOverflow AndAlso retry < nrRetries Then
                        retry += 1
                    Else
                        Throw
                    End If
                End Try
                If stream IsNot Nothing Then
                    Exit While
                End If
            End While

            Dim decoded = DecodeFluxData(stream)
            Return New Flux(decoded.Item2, decoded.Item1, SampleFreq, indexCued:=False)
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.write_track
        Public Sub WriteTrack(fluxList As IEnumerable(Of Integer),
                              terminateAtIndex As Boolean,
                              Optional cueAtIndex As Boolean = True,
                              Optional nrRetries As Integer = 5,
                              Optional hardSectorTicks As Integer = 0)
            Dim encoded = EncodeFluxData(fluxList)
            Dim retry = 0
            While True
                Try
                    If hardSectorTicks <> 0 Then
                        Dim cmd As New List(Of Byte) From {
                            CByte(UsbProtocol.Cmd.WriteFlux),
                            8,
                            CByte(If(cueAtIndex, 1, 0)),
                            CByte(If(terminateAtIndex, 1, 0))
                        }
                        cmd.AddRange(BitConverter.GetBytes(CUInt(hardSectorTicks)))
                        SendCmd(cmd.ToArray())
                    Else
                        SendCmd(New Byte() {
                                    CByte(UsbProtocol.Cmd.WriteFlux),
                                    4,
                                    CByte(If(cueAtIndex, 1, 0)),
                                    CByte(If(terminateAtIndex, 1, 0))
                                   })
                    End If
                    Serial.Write(encoded)
                    Dim syncByte = Serial.Read(1)
                    SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetFluxStatus), 2})
                    Exit While
                Catch ex As CmdError
                    If ex.Code = UsbProtocol.Ack.FluxUnderflow AndAlso retry < nrRetries Then
                        retry += 1
                    Else
                        Throw
                    End If
                End Try
            End While
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.erase_track
        Public Sub EraseTrack(ticks As Double)
            Dim command As New List(Of Byte) From {
                CByte(UsbProtocol.Cmd.EraseFlux),
                6
            }
            command.AddRange(BitConverter.GetBytes(CUInt(Math.Truncate(ticks))))
            SendCmd(command.ToArray())
            Dim syncByte = Serial.Read(1)
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetFluxStatus), 2})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.seek
        Public Sub Seek(cyl As Integer, head As Integer)
            Dim cmd = BuildSeekCommand(cyl)
            SendCmd(cmd)
            ' Python: trk0 = not self.get_pin(26)
            Dim trk0 = Not GetPin(26)
            If cyl = 0 AndAlso Not trk0 Then
                ' Some flippy-modded Panasonic drives may not assert /TRK0 when
                ' stepping inward from cyl -1. Try a NoClickStep to nudge it.
                Try
                    Dim info = GetCurrentDriveInfo()
                    If info.IsFlippy Then
                        SendCmd(New Byte() {CByte(UsbProtocol.Cmd.NoClickStep), 2})
                    End If
                Catch ex As CmdError
                    ' GetInfo.CurrentDrive / NoClickStep are best-effort: older
                    ' firmware may not support them.
                End Try
                trk0 = Not GetPin(26)
            End If
            ErrorHandling.Check(cyl < 0 OrElse (cyl = 0) = trk0,
                                String.Format(
                                    "Track0 signal {0} after seek to cylinder {1}" & vbLf &
                                    " 1. Try ""gw reset"" to re-calibrate the drive-head position" & vbLf &
                                    " 2. If the error persists try slowing down seek operations" & vbLf &
                                    "     eg. ""gw delays --step 20000"" for 20ms per step",
                                    If(trk0, "asserted", "absent"), cyl))
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.Head), 3, CByte(head)})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.set_bus_type
        Public Sub SetBusType(busType As Integer)
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.SetBusType), 3, CByte(busType)})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.set_pin
        Public Sub SetPin(pin As Integer, level As Boolean)
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.SetPin), 4, CByte(pin), CByte(If(level, 1, 0))})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.get_pin
        Public Function GetPin(pin As Integer) As Boolean
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetPin), 3, CByte(pin)})
            Dim value = ReadExact(1)(0)
            Return value <> 0
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.drive_select
        Public Sub DriveSelect(unit As Integer)
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.[Select]), 3, CByte(unit)})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.drive_deselect
        Public Sub DriveDeselect()
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.Deselect), 2})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.drive_motor
        Public Sub DriveMotor(unit As Integer, state As Boolean)
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.Motor), 4, CByte(unit), CByte(If(state, 1, 0))})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.switch_fw_mode
        Public Sub SwitchFwMode(mode As Integer)
            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.SwitchFwMode), 3, CByte(mode And &HFF)})
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.update_main_firmware
        Public Function UpdateMainFirmware(data As Byte()) As Integer
            Dim cmd As New List(Of Byte) From {
                CByte(UsbProtocol.Cmd.Update),
                6
            }
            cmd.AddRange(BitConverter.GetBytes(CUInt(data.Length)))
            SendCmd(cmd.ToArray())
            Serial.Write(data)
            Return CInt(ReadExact(1)(0))
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.update_bootloader
        Public Function UpdateBootloader(data As Byte()) As Integer
            Dim cmd As New List(Of Byte) From {
                CByte(UsbProtocol.Cmd.Update),
                10
            }
            cmd.AddRange(BitConverter.GetBytes(CUInt(data.Length)))
            cmd.AddRange(BitConverter.GetBytes(CUInt(&HDEAFBEE3UI)))
            SendCmd(cmd.ToArray())
            Serial.Write(data)
            Return CInt(ReadExact(1)(0))
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit._send_cmd
        Public Sub SendCmd(command As Byte())
            Serial.Write(command)
            Dim ack = ReadExact(2)
            ErrorHandling.Check(ack.Length = 2, "Command acknowledgement is incomplete.")
            ErrorHandling.Check(ack(0) = command(0), String.Format("Command returned garbage ({0:X2} != {1:X2})", ack(0), command(0)))
            If ack(1) <> 0 Then
                Throw New CmdError(command, CType(ack(1), UsbProtocol.Ack))
            End If
        End Sub

        ' Python map: src/greaseweazle/usb.py::(no direct 1:1 symbol; VB helper for exact-length reads)
        Private Function ReadExact(count As Integer) As Byte()
            Dim data As New List(Of Byte)(count)
            While data.Count < count
                Dim chunk = Serial.Read(count - data.Count)
                If chunk Is Nothing OrElse chunk.Length = 0 Then
                    Throw New FatalException("Device did not return expected response bytes.")
                End If
                data.AddRange(chunk)
            End While
            Return data.ToArray()
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit._read_track
        Private Function ReadTrackRaw(revs As Integer, ticks As Integer) As Byte()
            Dim request = BuildReadFluxCommand(revs, ticks)
            SendCmd(request)
            Dim stream As New List(Of Byte)()
            Do
                stream.AddRange(Serial.Read(1))
                Dim pending = Serial.BytesAvailable
                If pending > 0 Then
                    stream.AddRange(Serial.Read(pending))
                End If
            Loop While stream.Count = 0 OrElse stream(stream.Count - 1) <> 0

            SendCmd(New Byte() {CByte(UsbProtocol.Cmd.GetFluxStatus), 2})
            Return stream.ToArray()
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit._read_track
        Private Shared Function BuildReadFluxCommand(revs As Integer, ticks As Integer) As Byte()
            Dim bytes As New List(Of Byte) From {
                CByte(UsbProtocol.Cmd.ReadFlux),
                8
            }
            bytes.AddRange(BitConverter.GetBytes(CUInt(ticks)))
            bytes.AddRange(BitConverter.GetBytes(CUShort(If(revs = 0, 0, revs + 1))))
            Return bytes.ToArray()
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.seek
        Private Shared Function BuildSeekCommand(cyl As Integer) As Byte()
            If cyl >= -128 AndAlso cyl <= 127 Then
                Return New Byte() {CByte(UsbProtocol.Cmd.Seek), 3, CByte(cyl And &HFF)}
            End If
            If cyl >= -32768 AndAlso cyl <= 32767 Then
                Dim list As New List(Of Byte) From {
                    CByte(UsbProtocol.Cmd.Seek),
                    4
                }
                list.AddRange(BitConverter.GetBytes(CShort(cyl)))
                Return list.ToArray()
            End If
            Throw New FatalException(String.Format("Seek: Invalid cylinder {0}", cyl))
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit._decode_flux
        Private Function DecodeFluxData(data As Byte()) As Tuple(Of List(Of Double), List(Of Double))
            Return UsbProtocol.DecodeFlux(data)
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit._encode_flux
        Private Function EncodeFluxData(fluxValues As IEnumerable(Of Integer)) As Byte()
            Return UsbProtocol.EncodeFlux(fluxValues, SampleFreq)
        End Function

    End Class

    ' Python map: src/greaseweazle/usb.py::DriveInfo
    Public Class DriveInfo

        ' Python map: src/greaseweazle/usb.py::DriveInfo.FLAG_CYL_VALID
        Public Const FlagCylValid As UInteger = 1UI
        ' Python map: src/greaseweazle/usb.py::DriveInfo.FLAG_MOTOR_ON
        Public Const FlagMotorOn As UInteger = 2UI
        ' Python map: src/greaseweazle/usb.py::DriveInfo.FLAG_IS_FLIPPY
        Public Const FlagIsFlippy As UInteger = 4UI

        ' Python map: src/greaseweazle/usb.py::DriveInfo.__init__
        Public Sub New(response As Byte())
            ErrorHandling.Check(response IsNot Nothing AndAlso response.Length >= 8, "DriveInfo response too short")
            Dim flags = BitConverter.ToUInt32(response, 0)
            Dim cylValue = BitConverter.ToInt32(response, 4)
            Me.Cyl = If((flags And FlagCylValid) <> 0UI, CType(cylValue, Integer?), CType(Nothing, Integer?))
            MotorOn = (flags And FlagMotorOn) <> 0UI
            IsFlippy = (flags And FlagIsFlippy) <> 0UI
        End Sub

        ' Python map: src/greaseweazle/usb.py::DriveInfo.cyl
        Public Property Cyl As Integer?
        ' Python map: src/greaseweazle/usb.py::DriveInfo.motor_on
        Public Property MotorOn As Boolean
        ' Python map: src/greaseweazle/usb.py::DriveInfo.is_flippy
        Public Property IsFlippy As Boolean

        ' Python map: src/greaseweazle/usb.py::DriveInfo.__str__
        Public Overrides Function ToString() As String
            Dim text = "Cyl: " & If(Cyl.HasValue, Cyl.Value.ToString(Globalization.CultureInfo.InvariantCulture), "Unknown")
            If MotorOn Then
                text &= "; Motor-On"
            End If
            If IsFlippy Then
                text &= "; Is-Flippy"
            End If
            Return text
        End Function
    End Class

    ' Python map: src/greaseweazle/usb.py::Unit.__init__ (firmware info unpack fields)
    Public Class FirmwareInfo
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked major field)
        Public Property Major As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked minor field)
        Public Property Minor As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked is-main-firmware flag)
        Public Property IsMainFirmware As Boolean
        ' Python map: src/greaseweazle/usb.py::Unit.version
        Public Property Version As Tuple(Of Integer, Integer)
        ' Python map: src/greaseweazle/usb.py::Unit.max_cmd
        Public Property MaxCmd As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.sample_freq
        Public Property SampleFreq As Double
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked hardware model)
        Public Property HwModel As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked hardware submodel)
        Public Property HwSubmodel As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked usb speed)
        Public Property UsbSpeed As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked MCU id)
        Public Property McuId As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked MCU MHz)
        Public Property McuMhz As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked MCU SRAM KB)
        Public Property McuSramKb As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.__init__ (no direct 1:1 symbol; unpacked USB buffer KB)
        Public Property UsbBufferKb As Integer
        ' Python map: src/greaseweazle/usb.py::Unit.update_mode
        Public Property UpdateMode As Boolean
        ' Python map: src/greaseweazle/usb.py::Unit.update_needed
        Public Property UpdateNeeded As Boolean
        ' Python map: src/greaseweazle/usb.py::Unit.update_jumpered
        Public Property UpdateJumpered As Boolean
    End Class

End Namespace
