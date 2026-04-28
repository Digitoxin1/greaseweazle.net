Namespace Greaseweazle.Actions

    ' Discriminator for the device-probe outcome. Any one DeviceInfoResult
    ' is in exactly one of these states; library consumers branch on it
    ' to decide which fields are meaningful.
    Public Enum DeviceConnectionState
        ' --test dry-run: the algorithm did not attempt to open a serial
        ' port. Only HostToolsVersion is populated; Device is Nothing.
        TestMode = 0

        ' Probe attempted but the serial port could not be opened (port
        ' missing, in use, or rejected). Device is Nothing; the CLI
        ' renders "  Not found" under "Device:".
        NotFound = 1

        ' Probe succeeded — Device carries the device-side fields.
        Connected = 2
    End Enum

    ' Strongly-typed return value of InfoCommand.Run.
    '
    ' Every value the legacy `gw info` exe printed has a corresponding
    ' field somewhere in this object graph. The CLI front-end's
    ' InfoFormatter renders it back to the Python `print_info_line` shape;
    ' library consumers (GUIs, automation) inspect fields directly.
    Public NotInheritable Class DeviceInfoResult

        Public Sub New(hostToolsVersion As String,
                       connectionState As DeviceConnectionState,
                       device As DeviceInfoBlock)
            Me.HostToolsVersion = hostToolsVersion
            Me.ConnectionState = connectionState
            Me.Device = device
        End Sub

        Public ReadOnly Property HostToolsVersion As String
        Public ReadOnly Property ConnectionState As DeviceConnectionState

        ' Populated only when ConnectionState = Connected. Nothing in the
        ' TestMode and NotFound states.
        Public ReadOnly Property Device As DeviceInfoBlock

    End Class

    ' Per-device fields surfaced when ConnectionState = Connected. The
    ' algorithm exposes raw numeric IDs (HwModel, HwSubmodel, McuId,
    ' UsbSpeedRaw) rather than pre-formatted display strings so non-CLI
    ' consumers can render them however they like; the CLI looks the names
    ' up via Info.ModelName / McuName / UsbSpeedName helpers when rendering.
    Public NotInheritable Class DeviceInfoBlock

        Public Sub New(port As String,
                       hwModel As Integer,
                       hwSubmodel As Integer,
                       mcuId As Integer,
                       mcuMhz As Integer,
                       mcuSramKb As Integer,
                       firmwareMajor As Integer,
                       firmwareMinor As Integer,
                       isBootloader As Boolean,
                       serialNumber As String,
                       usbSpeedRaw As Integer,
                       usbBufferKb As Integer,
                       jumperlessUpdate As Boolean,
                       firmwareUpdate As FirmwareUpdateInfo)
            Me.Port = port
            Me.HwModel = hwModel
            Me.HwSubmodel = hwSubmodel
            Me.McuId = mcuId
            Me.McuMhz = mcuMhz
            Me.McuSramKb = mcuSramKb
            Me.FirmwareMajor = firmwareMajor
            Me.FirmwareMinor = firmwareMinor
            Me.IsBootloader = isBootloader
            Me.SerialNumber = serialNumber
            Me.UsbSpeedRaw = usbSpeedRaw
            Me.UsbBufferKb = usbBufferKb
            Me.JumperlessUpdate = jumperlessUpdate
            Me.FirmwareUpdate = firmwareUpdate
        End Sub

        Public ReadOnly Property Port As String
        Public ReadOnly Property HwModel As Integer
        Public ReadOnly Property HwSubmodel As Integer
        Public ReadOnly Property McuId As Integer
        Public ReadOnly Property McuMhz As Integer
        Public ReadOnly Property McuSramKb As Integer
        Public ReadOnly Property FirmwareMajor As Integer
        Public ReadOnly Property FirmwareMinor As Integer
        Public ReadOnly Property IsBootloader As Boolean
        Public ReadOnly Property SerialNumber As String
        Public ReadOnly Property UsbSpeedRaw As Integer
        Public ReadOnly Property UsbBufferKb As Integer
        Public ReadOnly Property JumperlessUpdate As Boolean

        ' Nothing when no newer release was discovered (or the GitHub probe
        ' silently failed). Populated when a newer release is published.
        Public ReadOnly Property FirmwareUpdate As FirmwareUpdateInfo

    End Class

    ' Marker for the case where Info.LatestFirmware() reports a newer
    ' release than the currently-running firmware. The CLI front-end
    ' renders the "*** New firmware version X.Y is available" banner plus
    ' the per-device update instructions; library consumers can pop their
    ' own UI without going through the CLI's wording.
    Public NotInheritable Class FirmwareUpdateInfo

        Public Sub New(latestMajor As Integer, latestMinor As Integer)
            Me.LatestMajor = latestMajor
            Me.LatestMinor = latestMinor
        End Sub

        Public ReadOnly Property LatestMajor As Integer
        Public ReadOnly Property LatestMinor As Integer

    End Class

End Namespace
