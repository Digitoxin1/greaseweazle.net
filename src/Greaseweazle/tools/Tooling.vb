Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports Greaseweazle.Shared
Imports System.IO
Imports System.IO.Ports
Imports System.Threading

Namespace Greaseweazle.Tools

    ' Python map: src/greaseweazle/tools/util.py::CmdlineHelpFormatter
    Public Class CmdlineHelpFormatter
        ' Python map: src/greaseweazle/tools/util.py::CmdlineHelpFormatter._get_help_string
        Public Function GetHelpString(help As String, defaultValue As Object) As String
            If help Is Nothing Then
                Return String.Empty
            End If
            If help.IndexOf("%no_default", StringComparison.Ordinal) >= 0 Then
                Return help.Replace("%no_default", String.Empty)
            End If
            If help.IndexOf("%(default)", StringComparison.Ordinal) >= 0 OrElse
               defaultValue Is Nothing OrElse
               (TypeOf defaultValue Is Boolean AndAlso Not CBool(defaultValue)) Then
                Return help
            End If
            Return help & " (default: %(default)s)"
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/util.py::ArgumentParser
    Public Class ArgumentParser
        Public ReadOnly Property Formatter As CmdlineHelpFormatter

        ' Python map: src/greaseweazle/tools/util.py::ArgumentParser.__init__
        Public Sub New(Optional formatter As CmdlineHelpFormatter = Nothing)
            Me.Formatter = If(formatter, New CmdlineHelpFormatter())
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/util.py::Drive
    Public Class Drive
        ' Python map: src/greaseweazle/tools/util.py::Drive.__init__
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::Drive.__call__
        Public Function [Call](token As String) As DriveSpec
            Return ToolOptions.Drive(token)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/util.py (direct helper parity for option/drive parsing and port selection).
    Public NotInheritable Class ToolOptions

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/util.py::period
        Public Shared Function Period(arg As String) As Double
            ' Python uses re.match (start-anchored). Mirror that with a regex anchored
            ' at the beginning of the string and look at each suffix in priority order.
            Dim m = System.Text.RegularExpressions.Regex.Match(arg, "^(\d*\.\d+|\d+)rpm")
            If m.Success Then
                Return 60.0 / Double.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
            End If
            m = System.Text.RegularExpressions.Regex.Match(arg, "^(\d*\.\d+|\d+)ms")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture) / 1000.0
            End If
            m = System.Text.RegularExpressions.Regex.Match(arg, "^(\d*\.\d+|\d+)us")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture) / 1000000.0
            End If
            m = System.Text.RegularExpressions.Regex.Match(arg, "^(\d*\.\d+|\d+)ns")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture) / 1000000000.0
            End If
            m = System.Text.RegularExpressions.Regex.Match(arg, "^(\d*\.\d+|\d+)scp")
            If m.Success Then
                Return Double.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture) / 40000000.0
            End If
            Return 60.0 / Double.Parse(arg, Globalization.CultureInfo.InvariantCulture)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::split_opts
        Public Shared Function SplitOpts(input As String) As Tuple(Of String, Dictionary(Of String, String))
            Return OptionParser.SplitOpts(input)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::Drive.__call__
        Public Shared Function Drive(token As String) As DriveSpec
            Dim map As New Dictionary(Of String, Tuple(Of UsbProtocol.BusType, Integer))(StringComparer.OrdinalIgnoreCase) From {
                {"A", Tuple.Create(UsbProtocol.BusType.IBMPC, 0)},
                {"B", Tuple.Create(UsbProtocol.BusType.IBMPC, 1)},
                {"0", Tuple.Create(UsbProtocol.BusType.Shugart, 0)},
                {"1", Tuple.Create(UsbProtocol.BusType.Shugart, 1)},
                {"2", Tuple.Create(UsbProtocol.BusType.Shugart, 2)},
                {"3", Tuple.Create(UsbProtocol.BusType.Shugart, 3)}
            }
            If Not map.ContainsKey(token) Then
                Throw New ArgumentException(String.Format("invalid drive letter: '{0}'", token))
            End If
            Dim mapped = map(token)
            Return New DriveSpec With {.Bus = mapped.Item1, .UnitId = mapped.Item2}
        End Function

        ' Python map: src/greaseweazle/tools/util.py::level
        Public Shared Function Level(token As String) As Boolean
            Dim map As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase) From {
                {"H", True},
                {"L", False}
            }
            If Not map.ContainsKey(token) Then
                Throw New ArgumentException(String.Format("invalid pin level: '{0}'", token))
            End If
            Return map(token)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::min_int
        Public Shared Function MinInt(minimum As Integer) As Func(Of String, Integer)
            Return Function(value As String)
                       Dim parsed = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                       If parsed < minimum Then
                           Throw New ArgumentException(String.Format("must be {0} or greater", minimum))
                       End If
                       Return parsed
                   End Function
        End Function

        ' Python map: src/greaseweazle/tools/util.py::range_str
        Public Shared Function RangeStr(values As IEnumerable(Of Integer)) As String
            Dim items = values.ToList()
            If items.Count = 0 Then
                Return "<none>"
            End If
            Dim result As New Text.StringBuilder()
            Dim currentStart As Nullable(Of Integer) = Nothing
            Dim currentEnd As Nullable(Of Integer) = Nothing
            For Each i In items
                If currentEnd.HasValue AndAlso i = currentEnd.Value + 1 Then
                    currentEnd = i
                    Continue For
                End If
                If currentStart.HasValue Then
                    If currentStart.Value = currentEnd.Value Then
                        result.AppendFormat(Globalization.CultureInfo.InvariantCulture, "{0},", currentStart.Value)
                    Else
                        result.AppendFormat(Globalization.CultureInfo.InvariantCulture, "{0}-{1},", currentStart.Value, currentEnd.Value)
                    End If
                End If
                currentStart = i
                currentEnd = i
            Next

            If currentStart.HasValue Then
                If currentStart.Value = currentEnd.Value Then
                    result.AppendFormat(Globalization.CultureInfo.InvariantCulture, "{0}", currentStart.Value)
                Else
                    result.AppendFormat(Globalization.CultureInfo.InvariantCulture, "{0}-{1}", currentStart.Value, currentEnd.Value)
                End If
            End If

            Return result.ToString()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ValidSerialId)
        Public Shared Function ValidSerialId(serialId As String) As Boolean
            Return Not String.IsNullOrEmpty(serialId) AndAlso serialId.ToUpperInvariant().StartsWith("GW", StringComparison.Ordinal)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::get_image_class
        Public Shared Function GetImageClass(name As String) As String
            Return New ImageTypeRegistry().ResolveType(name).Item1
        End Function

        ' Python map: src/greaseweazle/tools/util.py::valid_ser_id
        Public Shared Function ValidSerId(serialId As String) As Boolean
            Return ValidSerialId(serialId)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::score_port
        Public Shared Function ScorePort(port As PortDescriptor,
                                         Optional oldPort As PortDescriptor = Nothing) As Integer
            Dim score = 0
            If port.Manufacturer = "Keir Fraser" AndAlso port.Product = "Greaseweazle" Then
                score = 20
            ElseIf port.Vid = &H1209 AndAlso port.Pid = &H4D69 Then
                score = 20
            ElseIf Not String.IsNullOrEmpty(port.Product) AndAlso port.Product.ToLowerInvariant().Contains("gw-compat") Then
                score = 19
            ElseIf port.Vid = &H1209 AndAlso port.Pid = &H1 Then
                score = 10
            End If

            If score > 0 AndAlso ValidSerialId(port.SerialNumber) Then
                If oldPort Is Nothing OrElse Not ValidSerialId(oldPort.SerialNumber) Then
                    score = 20
                ElseIf String.Equals(port.SerialNumber, oldPort.SerialNumber, StringComparison.Ordinal) Then
                    score = 30
                Else
                    score = 0
                End If
            End If

            If oldPort IsNot Nothing AndAlso Not String.IsNullOrEmpty(oldPort.Location) Then
                If String.IsNullOrEmpty(port.Location) OrElse Not String.Equals(port.Location, oldPort.Location, StringComparison.Ordinal) Then
                    score = 0
                End If
            End If

            Return score
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindBestPort)
        Public Shared Function FindBestPort(ports As IEnumerable(Of PortDescriptor),
                                            Optional oldPort As PortDescriptor = Nothing) As PortDescriptor
            Dim bestScore = 0
            Dim best As PortDescriptor = Nothing
            For Each p In ports
                Dim score = ScorePort(p, oldPort)
                If score > bestScore Then
                    bestScore = score
                    best = p
                End If
            Next
            Return best
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindPortDevice)
        Public Shared Function FindPortDevice(ports As IEnumerable(Of PortDescriptor),
                                              Optional oldPort As PortDescriptor = Nothing) As String
            Dim best = FindBestPort(ports, oldPort)
            If best IsNot Nothing AndAlso Not String.IsNullOrEmpty(best.Device) Then
                Return best.Device
            End If
            ' Python throws `serial.SerialException` here (util.py:397). The
            ' info action catches that to render "  Not found" with exit code
            ' 0; other actions let it propagate to a generic FATAL ERROR with
            ' exit 1. The closest .NET analogue is IOException (the same type
            ' SerialPort.Open() raises for port-not-found), which the info
            ' action already catches alongside UnauthorizedAccessException.
            ' Throwing InvalidOperationException would skip those handlers and
            ' surface a confusing FATAL ERROR even though the device is simply
            ' absent.
            Throw New IOException("Cannot find the Greaseweazle device")
        End Function

        ' Python map: src/greaseweazle/tools/util.py::find_port
        Public Shared Function FindPort(ports As IEnumerable(Of PortDescriptor),
                                        Optional oldPort As PortDescriptor = Nothing) As String
            Return FindPortDevice(ports, oldPort)
        End Function

        ' Python map: src/greaseweazle/tools/util.py::port_info
        Public Shared Function PortInfo(devname As String, ports As IEnumerable(Of PortDescriptor)) As PortDescriptor
            For Each p In ports
                If String.Equals(p.Device, devname, StringComparison.Ordinal) Then
                    Return p
                End If
            Next
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/tools/util.py::usb_reopen
        Public Shared Function UsbReopen(usb As Unit, isUpdate As Boolean) As Unit
            Dim mode = If(isUpdate, 0, 1)
            Try
                usb.SwitchFwMode(mode)
            Catch ex As Exception
                ' Mode switch may drop the serial connection before ack is observed.
            End Try

            usb.Serial.Close()
            Dim oldPort As PortDescriptor = Nothing
            If Not String.IsNullOrEmpty(usb.PortDevice) OrElse
               Not String.IsNullOrEmpty(usb.PortSerialNumber) OrElse
               Not String.IsNullOrEmpty(usb.PortLocation) Then
                oldPort = New PortDescriptor With {
                    .Device = usb.PortDevice,
                    .SerialNumber = usb.PortSerialNumber,
                    .Location = usb.PortLocation
                }
            End If

            For i = 0 To 9
                Thread.Sleep(500)
                Try
                    Dim ports = EnumeratePorts()
                    Dim deviceName = FindPort(ports, oldPort)
                    Dim transport As New SerialPortTransport(deviceName)
                    transport.Open()
                    Dim reopened As New Unit(transport)
                    Dim info = reopened.ReadFirmwareInfo()
                    reopened.ApplyFirmwareInfo(info)
                    Dim descriptor = PortInfo(deviceName, ports)
                    reopened.PortDevice = deviceName
                    reopened.PortSerialNumber = If(descriptor Is Nothing, Nothing, descriptor.SerialNumber)
                    reopened.PortLocation = If(descriptor Is Nothing, Nothing, descriptor.Location)
                    reopened.JumperlessUpdate = usb.JumperlessUpdate
                    reopened.CanModeSwitch = usb.CanModeSwitch
                    Return reopened
                Catch ex As Exception
                    ' Device not yet available; keep polling.
                End Try
            Next

            Throw New IOException("Could not find the Greaseweazle device after switching firmware mode." &
                                  Environment.NewLine &
                                  "If you are connected via a USB hub, instead try connecting directly.")
        End Function

        ' Python map: src/greaseweazle/tools/util.py::print_update_instructions
        Public Shared Function PrintUpdateInstructions(usb As Unit) As List(Of String)
            Dim lines As New List(Of String) From {"To perform an Update:"}
            If Not usb.JumperlessUpdate Then
                lines.Add(" - Disconnect from USB")
                Dim pins = If(usb.HwModel <> 1, "RXI-TXO", "DCLK-GND")
                lines.Add(String.Format(" - Install the Update Jumper at pins {0}", pins))
                lines.Add(" - Reconnect to USB")
            End If
            lines.Add(" - Run ""gw update"" to download and install latest firmware")
            Return lines
        End Function

        ' Python map: src/greaseweazle/tools/util.py::usb_mode_check
        Public Shared Function UsbModeCheck(usb As Unit, isUpdate As Boolean) As Unit
            If usb.UpdateMode AndAlso Not isUpdate Then
                If usb.CanModeSwitch Then
                    usb = UsbReopen(usb, isUpdate)
                    If Not usb.UpdateMode Then
                        Return usb
                    End If
                End If
                Dim message As New List(Of String) From {
                    "ERROR: Device is in Firmware Update Mode",
                    " - The only available action is ""gw update"""
                }
                If usb.UpdateJumpered Then
                    Dim pins = If(usb.HwModel <> 1, "RXI-TXO", "DCLK-GND")
                    message.Add(String.Format(" - For normal operation disconnect from USB and remove the Update Jumper at pins {0}", pins))
                Else
                    message.Add(" - Main firmware is erased: You *must* perform an update!")
                End If
                Throw New FatalException(String.Join(Environment.NewLine, message))
            End If

            If isUpdate AndAlso Not usb.UpdateMode Then
                If usb.CanModeSwitch Then
                    usb = UsbReopen(usb, isUpdate)
                    ErrorHandling.Check(usb.UpdateMode,
"Device did not change to Firmware Update Mode as requested." & Environment.NewLine &
"If the problem persists, install the Update Jumper at pins RXI-TXO.")
                    Return usb
                End If
                Dim lines As New List(Of String) From {"ERROR: Device is not in Firmware Update Mode"}
                lines.AddRange(PrintUpdateInstructions(usb))
                Throw New FatalException(String.Join(Environment.NewLine, lines))
            End If

            If Not usb.UpdateMode AndAlso usb.UpdateNeeded Then
                Dim lines As New List(Of String) From {
                    String.Format(Globalization.CultureInfo.InvariantCulture,
                                  "ERROR: Device firmware version {0}.{1} is unsupported",
                                  usb.Major,
                                  usb.Minor)
                }
                lines.AddRange(PrintUpdateInstructions(usb))
                Throw New FatalException(String.Join(Environment.NewLine, lines))
            End If

            Return usb
        End Function

        ' Python map: src/greaseweazle/tools/util.py::usb_open
        Public Shared Function UsbOpen(deviceName As String, Optional isUpdate As Boolean = False, Optional modeCheck As Boolean = True) As Unit
            Dim ports = EnumeratePorts()
            If String.IsNullOrEmpty(deviceName) Then
                deviceName = FindPort(ports)
            End If
            Dim transport As New SerialPortTransport(deviceName)
            transport.Open()
            Dim usb As New Unit(transport)
            Dim info = usb.ReadFirmwareInfo()
            usb.ApplyFirmwareInfo(info)
            Dim descriptor = PortInfo(deviceName, ports)
            usb.PortDevice = deviceName
            usb.PortSerialNumber = If(descriptor Is Nothing, Nothing, descriptor.SerialNumber)
            usb.PortLocation = If(descriptor Is Nothing, Nothing, descriptor.Location)
            Dim version = Environment.OSVersion.Version
            Dim isWin7 = version.Major = 6 AndAlso version.Minor = 1
            usb.JumperlessUpdate = (usb.HwModel <> 1 OrElse usb.HwSubmodel <> 0) AndAlso Not isWin7
            usb.CanModeSwitch = usb.JumperlessUpdate AndAlso Not (usb.UpdateMode AndAlso usb.UpdateJumpered)
            If modeCheck Then
                usb = UsbModeCheck(usb, isUpdate)
            End If
            Return usb
        End Function

        ' Python map: src/greaseweazle/tools/util.py::comports (platform-specific serial-port enumeration source)
        Private Shared Function EnumeratePorts() As List(Of PortDescriptor)
            Try
                If Environment.OSVersion.Platform = PlatformID.Win32NT Then
                    Dim winPorts = WindowsPorts.Comports()
                    If winPorts IsNot Nothing AndAlso winPorts.Count <> 0 Then
                        Return winPorts
                    End If
                End If
            Catch
                ' Fall back to basic serial port enumeration.
            End Try

            Return SerialPort.GetPortNames().
                OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
                Select(Function(name) New PortDescriptor With {.Device = name}).
                ToList()
        End Function

        ' Python map: src/greaseweazle/tools/util.py::with_drive_selected
        Public Shared Sub WithDriveSelected(action As Action,
                                            usb As UsbDriveControl,
                                            drive As DriveSpec,
                                            Optional motor As Boolean = True)
            Try
                usb.SetBusType(CInt(drive.Bus))
            Catch ex As CmdError When ex.Code = UsbProtocol.Ack.BadCommand
                Throw New FatalException("Device does not support " & drive.Bus.ToString())
            End Try
            ' Register the active Unit + DriveSpec with the Ctrl-C handler so an
            ' aborted operation immediately unblocks the worker thread (the
            ' handler closes the serial port; the worker — i.e. THIS thread —
            ' then runs the cleanup below, mirroring Python's KeyboardInterrupt
            ' flow on the main thread).
            Dim adapter = TryCast(usb, UsbDriveControlAdapter)
            If adapter IsNot Nothing Then
                InterruptControl.Register(adapter.Client, drive)
            End If
            Try
                usb.DriveSelect(drive.UnitId)
                usb.DriveMotor(drive.UnitId, motor)
                action()
            Catch ex As KeyboardInterruptException
                ' Parity-test path: tests directly throw KeyboardInterruptException
                ' to exercise the same cleanup. Python prints a bare newline
                ' before reset() so any in-flight progress line ends cleanly.
                Console.Error.WriteLine()
                Try : usb.Reset() : Catch : End Try
                Throw
            Catch ex As Exception When InterruptControl.Aborted
                ' Real Ctrl-C path: the handler closed the serial port to unblock
                ' our pending Read, so the action()'s in-flight USB call surfaced
                ' as IOException/ObjectDisposedException/etc. Reopen the port via
                ' Reset() so the Finally below can issue motor-off + deselect on a
                ' healthy transport, and translate the failure into a clean
                ' KeyboardInterruptException for Program.Main to handle silently.
                '
                ' Wait for the handler thread's still-in-progress Close() to
                ' finish releasing the OS handle before we attempt to reopen.
                ' Without this, the worker races the handler and Open() fails
                ' with UnauthorizedAccessException ("Access to the port
                ' 'COMx' is denied") so the subsequent motor-off command can't
                ' be delivered and the drive keeps spinning.
                Console.Error.WriteLine()
                InterruptControl.WaitForCloseComplete()
                Try : usb.Reset() : Catch : End Try
                Throw New KeyboardInterruptException()
            Finally
                Try
                    ' Cleanup must not mask the original exception. If the
                    ' Ctrl-C handler closed the port externally and we exited
                    ' normally (e.g. action() finished just as Ctrl-C fired)
                    ' the Catch When Aborted block didn't run, so the port
                    ' may still be closed. Run Reset() here too in that case
                    ' so the motor-off + deselect commands below can actually
                    ' be delivered to the firmware.
                    If InterruptControl.Aborted Then
                        ' Same wait-for-close rationale as in the Catch above:
                        ' guarantees the handler's Close() has finished
                        ' releasing the COM port before we reopen it.
                        InterruptControl.WaitForCloseComplete()
                        Try : usb.Reset() : Catch : End Try
                    End If
                    Try : usb.DriveMotor(drive.UnitId, False) : Catch : End Try
                    Try : usb.DriveDeselect() : Catch : End Try
                Finally
                    If adapter IsNot Nothing Then
                        InterruptControl.Unregister()
                    End If
                End Try
            End Try
        End Sub
    End Class

    ' Python map: no-1:1 with Python symbols; this typed DTO replaces tuple-based drive descriptors used by tools modules.
    Public Class DriveSpec
        Public Property Bus As UsbProtocol.BusType
        Public Property UnitId As Integer
    End Class

    ' Python map: no-1:1 with Python symbols; this typed DTO models serial port metadata gathered in list-ports flows.
    Public Class PortDescriptor
        Public Property Device As String
        Public Property Manufacturer As String
        Public Property Product As String
        Public Property Vid As Integer
        Public Property Pid As Integer
        Public Property SerialNumber As String
        Public Property Location As String
        Public Property [Interface] As String
    End Class

    ' Python map: no-1:1 with Python symbols; this interface isolates USB drive operations for VB dependency inversion.
    Public Interface UsbDriveControl
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration SetBusType)
        Sub SetBusType(bus As Integer)
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DriveSelect)
        Sub DriveSelect(unit As Integer)
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DriveMotor)
        Sub DriveMotor(unit As Integer, enabled As Boolean)
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DriveDeselect)
        Sub DriveDeselect()
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration Reset)
        Sub Reset()
    End Interface

    ' Python map: no-1:1 with Python symbols; this adapter bridges Unit APIs to UsbDriveControl.
    Public Class UsbDriveControlAdapter
        Implements UsbDriveControl

        Private ReadOnly _client As Unit

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New(client As Unit)
            _client = client
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration SetBusType)
        Public Sub SetBusType(bus As Integer) Implements UsbDriveControl.SetBusType
            _client.SetBusType(bus)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DriveSelect)
        Public Sub DriveSelect(unit As Integer) Implements UsbDriveControl.DriveSelect
            _client.DriveSelect(unit)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DriveMotor)
        Public Sub DriveMotor(unit As Integer, enabled As Boolean) Implements UsbDriveControl.DriveMotor
            _client.DriveMotor(unit, enabled)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration DriveDeselect)
        Public Sub DriveDeselect() Implements UsbDriveControl.DriveDeselect
            _client.DriveDeselect()
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.reset (transport-level baudrate-magic
        ' reset used by with_drive_selected to break in-flight commands on KeyboardInterrupt).
        Public Sub Reset() Implements UsbDriveControl.Reset
            _client.Reset()
        End Sub

        ' Python map: no-1:1 with Python symbols; VB-only accessor that lets WithDriveSelected
        ' surface the underlying Unit so Ctrl-C handling can run motor-off/deselect commands.
        Public ReadOnly Property Client As Unit
            Get
                Return _client
            End Get
        End Property
    End Class

    ' Python map: no-1:1 with Python symbols; this declaration mirrors KeyboardInterrupt semantics in managed code.
    Public Class KeyboardInterruptException
        Inherits Exception

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New(Optional message As String = "Keyboard interrupt")
            MyBase.New(message)
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/write.py::PrecompSpec (direct precomp specification parser parity).
    Public Class PrecompSpec

        ' Python map: src/greaseweazle/tools/write.py::PrecompSpec.__init__
        Public Sub New(spec As String)
            Type = PrecompType.Mfm
            Entries = New List(Of Tuple(Of Integer, Integer))()
            ImportSpec(spec)
        End Sub

        Public Property Type As Integer
        Public Property Entries As List(Of Tuple(Of Integer, Integer))

        ' Python map: src/greaseweazle/tools/write.py::PrecompSpec.importspec
        Public Sub ImportSpec(spec As String)
            Entries.Clear()
            Type = PrecompType.Mfm
            For Each token In spec.Split(":"c)
                Dim kv = token.Split("="c)
                If kv.Length <> 2 Then
                    Throw New ArgumentException("Invalid precomp specification.")
                End If
                Dim key = kv(0)
                Dim value = kv(1)
                If String.Equals(key, "type", StringComparison.OrdinalIgnoreCase) Then
                    Dim upper = value.ToUpperInvariant()
                    If upper = "MFM" Then
                        Type = PrecompType.Mfm
                    ElseIf upper = "FM" Then
                        Type = PrecompType.Fm
                    ElseIf upper = "GCR" Then
                        Type = PrecompType.Gcr
                    Else
                        Throw New ArgumentException("Unknown precomp type.")
                    End If
                Else
                    Entries.Add(Tuple.Create(Integer.Parse(key, Globalization.CultureInfo.InvariantCulture),
                                             Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)))
                End If
            Next
            Entries = Entries.OrderBy(Function(x) x.Item1).ToList()
        End Sub

        ' Python map: src/greaseweazle/tools/write.py::PrecompSpec.track_precomp
        Public Function TrackPrecomp(cyl As Integer) As Precomp
            For i = Entries.Count - 1 To 0 Step -1
                Dim entry = Entries(i)
                If cyl >= entry.Item1 Then
                    Return New Precomp(Type, entry.Item2)
                End If
            Next
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/tools/write.py::PrecompSpec.__str__
        Public Overrides Function ToString() As String
            Dim typeNames As String() = {"MFM", "FM", "GCR"}
            Dim s = "Precomp " & typeNames(Type)
            For Each entry In Entries
                s &= String.Format(", {0}-:{1}ns", entry.Item1, entry.Item2)
            Next
            Return s
        End Function
    End Class

    ' Python map: no-1:1 with Python symbols; this declaration centralizes extension-to-image lookup used across multiple tools modules.
    Public Class ImageTypeRegistry

        Private ReadOnly _types As New Dictionary(Of String, Tuple(Of String, String))(StringComparer.OrdinalIgnoreCase) From {
            {".2d", Tuple.Create("SHARP2D", "sharp2d")},
            {".a2r", Tuple.Create("A2R", "a2r")},
            {".adf", Tuple.Create("ADF", "adf")},
            {".ads", Tuple.Create("ADS", "acorn")},
            {".adm", Tuple.Create("ADM", "acorn")},
            {".adl", Tuple.Create("ADL", "acorn")},
            {".ctr", Tuple.Create("CTRaw", "caps")},
            {".d1m", Tuple.Create("D1M", "d81")},
            {".d2m", Tuple.Create("D2M", "d81")},
            {".d4m", Tuple.Create("D4M", "d81")},
            {".d64", Tuple.Create("D64", "d64")},
            {".d71", Tuple.Create("D71", "d64")},
            {".d81", Tuple.Create("D81", "d81")},
            {".d88", Tuple.Create("D88", "d88")},
            {".dcp", Tuple.Create("DCP", "dcp")},
            {".dim", Tuple.Create("DIM", "dim")},
            {".dmk", Tuple.Create("DMK", "dmk")},
            {".do", Tuple.Create("DO", "apple2")},
            {".dsd", Tuple.Create("DSD", "acorn")},
            {".dsk", Tuple.Create("DSK", "dsk")},
            {".edsk", Tuple.Create("EDSK", "edsk")},
            {".fd", Tuple.Create("FD", "fd")},
            {".fdi", Tuple.Create("FDI", "fdi")},
            {".hdm", Tuple.Create("HDM", "hdm")},
            {".hfe", Tuple.Create("HFE", "hfe")},
            {".ima", Tuple.Create("IMG", "img")},
            {".img", Tuple.Create("IMG", "img")},
            {".imd", Tuple.Create("IMD", "imd")},
            {".ipf", Tuple.Create("IPF", "caps")},
            {".mgt", Tuple.Create("MGT", "mgt")},
            {".msa", Tuple.Create("MSA", "msa")},
            {".nfd", Tuple.Create("NFD", "nfd")},
            {".nsi", Tuple.Create("NSI", "nsi")},
            {".po", Tuple.Create("PO", "apple2")},
            {".raw", Tuple.Create("KryoFlux", "kryoflux")},
            {".sf7", Tuple.Create("SF7", "sf7")},
            {".scp", Tuple.Create("SCP", "scp")},
            {".ssd", Tuple.Create("SSD", "acorn")},
            {".st", Tuple.Create("IMG", "img")},
            {".td0", Tuple.Create("TD0", "td0")},
            {".xdf", Tuple.Create("XDF", "xdf")}
        }

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetKnownSuffixes)
        Public Function GetKnownSuffixes() As IEnumerable(Of String)
            Return _types.Keys.OrderBy(Function(x) x).ToList()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveType)
        Public Function ResolveType(fileName As String) As Tuple(Of String, String)
            Dim ext = IO.Path.GetExtension(fileName)
            ErrorHandling.Check(_types.ContainsKey(ext),
                                String.Format("{0}: Unrecognised file suffix '{1}'{2}Known suffixes:{2}{3}",
                                              fileName,
                                              ext,
                                              Environment.NewLine,
                                              ColumnFormatter.Columnify(GetKnownSuffixes())))
            Return _types(ext)
        End Function
    End Class

End Namespace
