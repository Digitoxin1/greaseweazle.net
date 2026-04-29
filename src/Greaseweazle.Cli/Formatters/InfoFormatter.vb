Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports Greaseweazle.Actions
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Formatters

    ' Python map: src/greaseweazle/tools/info.py — renders DeviceInfoResult
    ' in the same byte sequence as the legacy unified exe. Library
    ' consumers in non-CLI hosts ignore this class.
    Public NotInheritable Class InfoFormatter

        Private Sub New()
        End Sub

        Public Shared Sub Render(result As DeviceInfoResult, output As TextWriter)
            output.WriteLine(Info.PrintInfoLine("Host Tools", result.HostToolsVersion))
            output.WriteLine(Info.PrintInfoLine("CLI", GetCliVersion()))
            output.WriteLine("Device:")
            Select Case result.ConnectionState
                Case DeviceConnectionState.TestMode
                    Return
                Case DeviceConnectionState.NotFound
                    output.WriteLine("  Not found")
                    Return
            End Select

            Dim dev = result.Device
            If Not String.IsNullOrEmpty(dev.Port) Then
                output.WriteLine(Info.PrintInfoLine("Port", dev.Port, 2))
            End If
            output.WriteLine(Info.PrintInfoLine("Model", Info.ModelName(dev.HwModel, dev.HwSubmodel), 2))

            Dim mcuStrs As New List(Of String)()
            Dim mcuName = Info.McuName(dev.McuId)
            If Not String.IsNullOrEmpty(mcuName) Then mcuStrs.Add(mcuName)
            If dev.McuMhz <> 0 Then mcuStrs.Add(String.Format(CultureInfo.InvariantCulture, "{0}MHz", dev.McuMhz))
            If dev.McuSramKb <> 0 Then mcuStrs.Add(String.Format(CultureInfo.InvariantCulture, "{0}kB SRAM", dev.McuSramKb))
            If mcuStrs.Count > 0 Then
                output.WriteLine(Info.PrintInfoLine("MCU", String.Join(", ", mcuStrs), 2))
            End If

            Dim fwver = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", dev.FirmwareMajor, dev.FirmwareMinor)
            If dev.IsBootloader Then fwver &= " (Bootloader)"
            output.WriteLine(Info.PrintInfoLine("Firmware", fwver, 2))

            output.WriteLine(Info.PrintInfoLine("Serial",
                                                If(String.IsNullOrEmpty(dev.SerialNumber), "Unknown", dev.SerialNumber),
                                                2))

            Dim usbStrs As New List(Of String)() From {Info.UsbSpeedName(dev.UsbSpeedRaw)}
            If dev.UsbBufferKb <> 0 Then
                usbStrs.Add(String.Format(CultureInfo.InvariantCulture, "{0}kB Buffer", dev.UsbBufferKb))
            End If
            output.WriteLine(Info.PrintInfoLine("USB", String.Join(", ", usbStrs), 2))

            If dev.FirmwareUpdate IsNot Nothing Then
                output.WriteLine("")
                output.WriteLine(String.Format(CultureInfo.InvariantCulture,
                                               "*** New firmware version {0}.{1} is available",
                                               dev.FirmwareUpdate.LatestMajor,
                                               dev.FirmwareUpdate.LatestMinor))
                For Each line In BuildUpdateInstructionLines(dev.JumperlessUpdate, dev.HwModel)
                    output.WriteLine(line)
                Next
            End If
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::print_update_instructions.
        ' Mirrors the helper in InfoAction; lives here too because the Cli
        ' formatter is the canonical source once the legacy shim is deleted.
        Public Shared Function BuildUpdateInstructionLines(jumperlessUpdate As Boolean,
                                                            hwModel As Integer) As List(Of String)
            Dim lines As New List(Of String) From {"To perform an Update:"}
            If Not jumperlessUpdate Then
                lines.Add(" - Disconnect from USB")
                Dim pins = If(hwModel <> 1, "RXI-TXO", "DCLK-GND")
                lines.Add(String.Format(" - Install the Update Jumper at pins {0}", pins))
                lines.Add(" - Reconnect to USB")
            End If
            lines.Add(" - Run ""gw update"" to download and install latest firmware")
            Return lines
        End Function

        ' Returns the CLI assembly's version. Mirrors the library-side
        ' `Host Tools` resolution in InfoAction: prefer
        ' AssemblyInformationalVersionAttribute (set when the build embeds a
        ' git-describe / pre-release tag), fall back to AssemblyName.Version.
        ' Reflects this assembly (Greaseweazle.Cli) rather than the entry
        ' assembly so library hosts that wrap the CLI still report the
        ' formatter's own build.
        Private Shared Function GetCliVersion() As String
            Dim asm = Assembly.GetExecutingAssembly()
            Dim infoAttr = TryCast(CustomAttributeExtensions.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)(asm),
                                   AssemblyInformationalVersionAttribute)
            If infoAttr IsNot Nothing AndAlso Not String.IsNullOrEmpty(infoAttr.InformationalVersion) Then
                Return infoAttr.InformationalVersion
            End If
            Return asm.GetName().Version.ToString()
        End Function

    End Class

End Namespace
