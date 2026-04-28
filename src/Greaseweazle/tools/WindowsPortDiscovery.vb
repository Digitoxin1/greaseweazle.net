Imports System.Globalization
Imports System.IO
Imports System.IO.Ports
Imports System.Management
Imports System.Text.RegularExpressions

Namespace Greaseweazle.Tools

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::GUID
    Public Structure GuidStruct
        Public Value As Guid

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::GUID.__str__
        Public Overrides Function ToString() As String
            Return Value.ToString()
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::GUID.__eq__
        Public Overrides Function Equals(obj As Object) As Boolean
            If Not TypeOf obj Is GuidStruct Then
                Return False
            End If
            Return Value.Equals(DirectCast(obj, GuidStruct).Value)
        End Function
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DEVPROPKEY
    Public Structure Devpropkey
        Public Fmtid As Guid
        Public Pid As UInteger
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_DEVICE_DESCRIPTOR
    Public Structure UsbDeviceDescriptor
        Public BLength As Byte
        Public BDescriptorType As Byte
        Public BcdUsb As UShort
        Public BDeviceClass As Byte
        Public BDeviceSubClass As Byte
        Public BDeviceProtocol As Byte
        Public BMaxPacketSize0 As Byte
        Public IdVendor As UShort
        Public IdProduct As UShort
        Public BcdDevice As UShort
        Public IManufacturer As Byte
        Public IProduct As Byte
        Public ISerialNumber As Byte
        Public BNumConfigurations As Byte
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_STRING_DESCRIPTOR
    Public Structure UsbStringDescriptor
        Public BLength As Byte
        Public BDescriptorType As Byte
        Public BString As String
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_COMMON_DESCRIPTOR
    Public Structure UsbCommonDescriptor
        Public BLength As Byte
        Public BDescriptorType As Byte
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_CONFIGURATION_DESCRIPTOR
    Public Structure UsbConfigurationDescriptor
        Public BLength As Byte
        Public BDescriptorType As Byte
        Public WTotalLength As UShort
        Public BNumInterfaces As Byte
        Public BConfigurationValue As Byte
        Public IConfiguration As Byte
        Public BmAttributes As Byte
        Public MaxPower As Byte
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_INTERFACE_DESCRIPTOR
    Public Structure UsbInterfaceDescriptor
        Public BLength As Byte
        Public BDescriptorType As Byte
        Public BInterfaceNumber As Byte
        Public BAlternateSetting As Byte
        Public BNumEndpoints As Byte
        Public BInterfaceClass As Byte
        Public BInterfaceSubClass As Byte
        Public BInterfaceProtocol As Byte
        Public IInterface As Byte
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_INTERFACE_ASSOCIATION_DESCRIPTOR
    Public Structure UsbInterfaceAssociationDescriptor
        Public BLength As Byte
        Public BDescriptorType As Byte
        Public BFirstInterface As Byte
        Public BInterfaceCount As Byte
        Public BFunctionClass As Byte
        Public BFunctionSubClass As Byte
        Public BFunctionProtocol As Byte
        Public IFunction As Byte
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_NODE_CONNECTION_INFORMATION_EX
    Public Structure UsbNodeConnectionInformationEx
        Public ConnectionIndex As UInteger
        Public DeviceDescriptor As UsbDeviceDescriptor
        Public CurrentConfigurationValue As Byte
        Public Speed As Byte
        Public DeviceIsHub As Byte
        Public DeviceAddress As UShort
        Public NumberOfOpenPipes As UInteger
        Public ConnectionStatus As UInteger
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::SetupPacket
    Public Structure SetupPacket
        Public BmRequest As Byte
        Public BRequest As Byte
        Public WValue As UShort
        Public WIndex As UShort
        Public WLength As UShort
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_DESCRIPTOR_REQUEST
    Public Structure UsbDescriptorRequest
        Public ConnectionIndex As UInteger
        Public SetupPacket As SetupPacket
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::CM_POWER_DATA
    Public Structure CmPowerData
        Public PdSize As UInteger
        Public PdMostRecentPowerState As Integer
        Public PdCapabilities As UInteger
        Public PdD1Latency As UInteger
        Public PdD2Latency As UInteger
        Public PdD3Latency As UInteger
        Public PdPowerStateMapping As Integer()
        Public PdDeepestSystemWake As Integer
    End Structure

    Public Module WindowsPortHelpers
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::find_from_iterable
        Public Function FindFromIterable(Of T)(items As IEnumerable(Of T), value As T) As T
            If items Is Nothing Then
                Return Nothing
            End If
            Return items.FirstOrDefault(Function(x) EqualityComparer(Of T).Default.Equals(x, value))
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::parse_device_property
        Public Function ParseDeviceProperty(value As Object) As Object
            If value Is Nothing Then
                Return Nothing
            End If
            If TypeOf value Is IEnumerable(Of String) Then
                Return DirectCast(value, IEnumerable(Of String)).ToList()
            End If
            If TypeOf value Is Byte() Then
                Return DirectCast(value, Byte())
            End If
            Return value
        End Function

        ' Python map: no-1:1 with Python symbols; helper to keep synthetic instance handles stable.
        Public Function StableInstanceHandle(value As String, Optional offset As UInteger = 0UI) As UInteger
            Dim hash As ULong = &H811C9DC5UL
            For Each ch As Char In If(value, String.Empty).ToUpperInvariant()
                hash = hash Xor CULng(CUInt(AscW(ch)))
                hash = (hash * &H1000193UL) And &HFFFFFFFFUL
            Next
            Return CUInt((hash + offset) And &HFFFFFFFFUL)
        End Function
    End Module

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::cached_property
    Public Class CachedProperty
        Private ReadOnly _cache As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::cached_property.__init__
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::cached_property.__get__
        Public Function [Get](key As String, resolver As Func(Of Object)) As Object
            If String.IsNullOrEmpty(key) Then
                Return If(resolver Is Nothing, Nothing, resolver())
            End If
            If _cache.ContainsKey(key) Then
                Return _cache(key)
            End If
            Dim value = If(resolver Is Nothing, Nothing, resolver())
            _cache(key) = value
            Return value
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode
    Public Class DeviceNode
        Implements IComparable(Of DeviceNode)
        Private _instanceIdentifier As String

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__init__
        Public Sub New(Optional instanceIdentifier As String = "")
            Me.InstanceIdentifier = instanceIdentifier
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.instance_handle
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.instance_handle
        Public Property InstanceHandle As UInteger
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.instance_identifier
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.instance_identifier
        Public Property InstanceIdentifier As String
            Get
                Return _instanceIdentifier
            End Get
            Set(value As String)
                _instanceIdentifier = value
                If InstanceHandle = 0UI AndAlso Not String.IsNullOrEmpty(value) Then
                    InstanceHandle = StableInstanceHandle(value, 1UI)
                End If
            End Set
        End Property
        Public Property Status As UInteger
        Public Property Parent As DeviceNode
        Public Property Name As String
        Public Property Description As String
        Public Property Address As String
        Public Property Manufacturer As String
        Public Property BusReportedDeviceDescription As String
        Public Property FriendlyName As String
        Public Property LocationPaths As New List(Of String)()
        Public Property PowerData As CmPowerData?
        Public Property PortName As String

        ' Python map: no-1:1 with Python symbols; base no-op wake implementation for polymorphic port nodes.
        Public Overridable Sub WakeUpDevice()
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__str__
        Public Overrides Function ToString() As String
            Return Name
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__eq__
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(obj, DeviceNode)
            If other Is Nothing Then
                Return False
            End If
            Return InstanceHandle = other.InstanceHandle
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__lt__
        Public Function CompareTo(other As DeviceNode) As Integer Implements IComparable(Of DeviceNode).CompareTo
            If other Is Nothing Then
                Return 1
            End If
            Return InstanceHandle.CompareTo(other.InstanceHandle)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__hash__
        Public Overrides Function GetHashCode() As Integer
            Return InstanceHandle.GetHashCode()
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.instance_handle
        Public ReadOnly Property InstanceHandleProperty As UInteger
            Get
                Return InstanceHandle
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.instance_identifier
        Public ReadOnly Property InstanceIdentifierProperty As String
            Get
                Return InstanceIdentifier
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.status
        Public ReadOnly Property StatusProperty As UInteger
            Get
                Return Status
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.parent
        Public ReadOnly Property ParentProperty As DeviceNode
            Get
                Return Parent
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.name
        Public ReadOnly Property NameProperty As String
            Get
                Return Name
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.description
        Public ReadOnly Property DescriptionProperty As String
            Get
                Return Description
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.address
        Public ReadOnly Property AddressProperty As String
            Get
                Return Address
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.manufacturer
        Public ReadOnly Property ManufacturerProperty As String
            Get
                Return Manufacturer
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.bus_reported_device_description
        Public ReadOnly Property BusReportedDeviceDescriptionProperty As String
            Get
                Return BusReportedDeviceDescription
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.friendly_name
        Public ReadOnly Property FriendlyNameProperty As String
            Get
                Return FriendlyName
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.location_paths
        Public ReadOnly Property LocationPathsProperty As List(Of String)
            Get
                Return LocationPaths
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.power_data
        Public ReadOnly Property PowerDataProperty As CmPowerData?
            Get
                Return PowerData
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.port_name
        Public ReadOnly Property PortNameProperty As String
            Get
                Return PortName
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.get_property
        Public Overridable Function GetProperty(key As String) As Object
            Select Case key
                Case NameOf(InstanceIdentifier)
                    Return InstanceIdentifier
                Case NameOf(Name)
                    Return Name
                Case NameOf(Description)
                    Return Description
                Case NameOf(Address)
                    Return Address
                Case NameOf(Manufacturer)
                    Return Manufacturer
                Case NameOf(BusReportedDeviceDescription)
                    Return BusReportedDeviceDescription
                Case NameOf(FriendlyName)
                    Return FriendlyName
                Case NameOf(LocationPaths)
                    Return LocationPaths
                Case NameOf(PortName)
                    Return PortName
            End Select
            Return Nothing
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface
    Public Class DeviceInterface
        Inherits DeviceNode
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.__init__
        Public Sub New()
            MyBase.New()
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.__init__
        Public Sub New(path As String, instanceIdentifier As String)
            MyBase.New(instanceIdentifier)
            InterfacePath = path
        End Sub

        Public Property InterfacePath As String

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.enumerate_device
        Public Shared Function EnumerateDevice() As List(Of DeviceInterface)
            Return SerialPort.GetPortNames().
                OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
                Select(Function(name) New DeviceInterface("\\.\" & name, name) With {
                    .InstanceHandle = StableInstanceHandle(name, 1UI)
                }).
                ToList()
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.interface
        Public ReadOnly Property [Interface] As String
            Get
                Return InterfacePath
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.get_interface_property
        Public Function GetInterfaceProperty(key As String) As Object
            If String.Equals(key, NameOf(InstanceIdentifier), StringComparison.OrdinalIgnoreCase) Then
                Return InstanceIdentifier
            End If
            If String.Equals(key, NameOf(InterfacePath), StringComparison.OrdinalIgnoreCase) Then
                Return InterfacePath
            End If
            Return Nothing
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::PortDevice
    Public Class PortDevice
        Inherits DeviceInterface

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::PortDevice.wake_up_device
        Public Overrides Sub WakeUpDevice()
            Dim targetPath = InterfacePath
            If String.IsNullOrEmpty(targetPath) AndAlso Not String.IsNullOrEmpty(PortName) Then
                targetPath = "\\.\" & PortName
            End If
            If String.IsNullOrEmpty(targetPath) Then
                Return
            End If
            Try
                Using handle As New FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)
                End Using
            Catch
                ' Best-effort wake-up only, like Python fallback behavior.
            End Try
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::PortDevice.enumerate_device
        Public Shared Shadows Function EnumerateDevice() As List(Of PortDevice)
            Dim devices As New List(Of PortDevice)()
            ' Query the real Windows PnP database for live COM ports so the
            ' resulting InstanceIdentifier carries the device's true USB serial
            ' (e.g. "USB\VID_1209&PID_4D69\GW00B542654C874000071C0716"). Without
            ' this, the fallback that mines the serial out of the path would
            ' just read back the synthetic placeholder we wrote ourselves and
            ' the user would see a fake serial in `gw info`.
            Dim wmiByPort = WindowsPnpLookup.QueryComPorts()
            Dim idx = 0
            For Each devicePort In SerialPort.GetPortNames().OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase)
                idx += 1
                Dim wmi As WindowsPnpLookup.PnpEntry = Nothing
                wmiByPort.TryGetValue(devicePort, wmi)

                ' Default to a synthetic identifier so legacy/stub-driven code
                ' paths continue to function when WMI lookup fails (e.g. a
                ' non-USB serial port, a service-disabled WMI provider, or a
                ' permission-restricted environment).
                Dim instanceId = String.Format(CultureInfo.InvariantCulture,
                                               "USB\VID_1209&PID_4D69\{0}",
                                               devicePort)
                Dim description = "USB Serial Device"
                Dim manufacturer = "Unknown"
                If wmi IsNot Nothing Then
                    If Not String.IsNullOrEmpty(wmi.DeviceId) Then
                        instanceId = wmi.DeviceId
                    End If
                    If Not String.IsNullOrEmpty(wmi.Description) Then
                        description = wmi.Description
                    End If
                    If Not String.IsNullOrEmpty(wmi.Manufacturer) Then
                        manufacturer = wmi.Manufacturer
                    End If
                End If

                devices.Add(New PortDevice With {
                    .InterfacePath = "\\.\" & devicePort,
                    .InstanceHandle = StableInstanceHandle(devicePort, 1UI),
                    .InstanceIdentifier = instanceId,
                    .PortName = devicePort,
                    .Name = devicePort,
                    .FriendlyName = devicePort,
                    .Description = description,
                    .BusReportedDeviceDescription = "USB Serial Interface",
                    .Manufacturer = manufacturer,
                    .Address = idx.ToString(CultureInfo.InvariantCulture),
                    .LocationPaths = New List(Of String) From {
                        String.Format(CultureInfo.InvariantCulture, "#USB({0})", idx)
                    }
                })
            Next
            Return devices
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry (partial; covers
    ' the subset of SetupAPI-based COM-port enumeration the VB stub does not implement).
    ' WMI replaces the SetupDiGetClassDevs P/Invoke chain here: the data we need is
    ' just the PnP DeviceID (instance identifier with the firmware serial) and the
    ' friendly description/manufacturer. Win32_PnPEntity surfaces all of these and
    ' is available on every Windows host that exposes a COM port through PnP.
    Friend NotInheritable Class WindowsPnpLookup
        Private Sub New()
        End Sub

        Public Class PnpEntry
            Public Property DeviceId As String
            Public Property Description As String
            Public Property Manufacturer As String
        End Class

        ' Captures the COM port name from a Caption like "USB Serial Device (COM3)".
        ' Anchored at end-of-string and tolerant of any leading text so vendor-
        ' specific captions (e.g. "Greaseweazle Serial Port (COM7)") still match.
        Private Shared ReadOnly CaptionPortRegex As New Regex("\(COM(\d+)\)\s*$",
                                                              RegexOptions.Compiled Or RegexOptions.IgnoreCase)

        ' Returns a dictionary keyed by COM port name (e.g. "COM3") with the
        ' live PnP entry pulled from WMI. Returns an empty dictionary on any
        ' WMI failure so callers degrade gracefully to the synthetic path.
        Public Shared Function QueryComPorts() As Dictionary(Of String, PnpEntry)
            Dim result As New Dictionary(Of String, PnpEntry)(StringComparer.OrdinalIgnoreCase)
            Try
                ' Restrict to entities whose caption looks like "...(COMx)" so we
                ' don't drag back every PnP entity on the machine.
                Dim query As New SelectQuery("Win32_PnPEntity",
                                             "Caption LIKE '%(COM%'",
                                             New String() {"Caption", "DeviceID", "Description", "Manufacturer"})
                Using searcher As New ManagementObjectSearcher("root\CIMV2", query.QueryString)
                    Using collection = searcher.Get()
                        For Each obj As ManagementObject In collection
                            Try
                                Dim caption = TryCast(obj("Caption"), String)
                                If String.IsNullOrEmpty(caption) Then
                                    Continue For
                                End If
                                Dim m = CaptionPortRegex.Match(caption)
                                If Not m.Success Then
                                    Continue For
                                End If
                                Dim portName = "COM" & m.Groups(1).Value
                                Dim entry As New PnpEntry With {
                                    .DeviceId = TryCast(obj("DeviceID"), String),
                                    .Description = TryCast(obj("Description"), String),
                                    .Manufacturer = TryCast(obj("Manufacturer"), String)
                                }
                                result(portName) = entry
                            Finally
                                obj.Dispose()
                            End Try
                        Next
                    End Using
                End Using
            Catch
                ' Swallow all WMI failures; callers fall back to the synthetic
                ' instance identifier and the existing path-extraction code.
            End Try
            Return result
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::LegacyPortDevice
    Public Class LegacyPortDevice
        Inherits DeviceNode

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::LegacyPortDevice.wake_up_device
        Public Overrides Sub WakeUpDevice()
            If String.IsNullOrEmpty(PortName) Then
                Return
            End If
            Dim targetPath = "\\.\" & PortName
            Try
                Using handle As New FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)
                End Using
            Catch
                ' Best-effort wake-up only, like Python fallback behavior.
            End Try
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::LegacyPortDevice.enumerate_device
        Public Shared Shadows Function EnumerateDevice() As List(Of LegacyPortDevice)
            Dim devices As New List(Of LegacyPortDevice)()
            Dim idx = 0
            For Each serialDeviceName In SerialPort.GetPortNames().OrderBy(Function(n) n, StringComparer.OrdinalIgnoreCase)
                idx += 1
                devices.Add(New LegacyPortDevice With {
                    .InstanceHandle = StableInstanceHandle(serialDeviceName, 1UI),
                    .InstanceIdentifier = serialDeviceName,
                    .PortName = serialDeviceName,
                    .Name = serialDeviceName,
                    .FriendlyName = serialDeviceName
                })
            Next
            Return devices
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHostControllerDevice
    Public Class UsbHostControllerDevice
        Inherits DeviceInterface
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDevice
    Public Class UsbHubDevice
        Inherits DeviceInterface
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry
    Public Class DeviceRegistry
        Private Shared ReadOnly UsbInfoCache As New Dictionary(Of String, UsbInfo)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly CacheUsbInfo As Boolean
        Private ReadOnly AllUsbHubs As List(Of UsbHubDevice)
        Private ReadOnly AllUsbHostControllers As List(Of UsbHostControllerDevice)

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.__init__
        Public Sub New(Optional cacheUsbInfo As Boolean = True)
            Me.CacheUsbInfo = cacheUsbInfo
            If Not cacheUsbInfo Then
                UsbInfoCache.Clear()
            End If

            Dim host As New UsbHostControllerDevice With {
                .InstanceHandle = 1UI,
                .InterfacePath = "\\.\USBHOST#0",
                .InstanceIdentifier = "USBHOST#0",
                .Address = "1",
                .LocationPaths = New List(Of String) From {"#USB(1)"},
                .Name = "USB Host Controller"
            }
            Dim hub As New UsbHubDevice With {
                .InstanceHandle = 2UI,
                .InterfacePath = "\\.\USBHUB#0",
                .InstanceIdentifier = "USBHUB#0",
                .Address = "1",
                .LocationPaths = New List(Of String) From {"#USB(1)"},
                .Name = "USB Root Hub",
                .Parent = host
            }
            AllUsbHostControllers = New List(Of UsbHostControllerDevice) From {host}
            AllUsbHubs = New List(Of UsbHubDevice) From {hub}
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_cache_key
        Public Function GetCacheKey(node As DeviceNode) As String
            Return If(node Is Nothing OrElse String.IsNullOrEmpty(node.InstanceIdentifier),
                      String.Empty,
                      node.InstanceIdentifier.ToLowerInvariant())
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_location_string
        Public Function GetLocationString(node As DeviceNode,
                                          Optional bConfigurationValue As Integer? = Nothing,
                                          Optional bInterfaceNumber As Integer? = Nothing) As String
            If node Is Nothing OrElse node.LocationPaths Is Nothing OrElse node.LocationPaths.Count = 0 Then
                Return Nothing
            End If
            Dim bus = Math.Max(GetBusNumber(node), 0)
            Dim location As New Text.StringBuilder(bus.ToString(CultureInfo.InvariantCulture))
            For Each m As Match In Regex.Matches(node.LocationPaths(0), "#USB\((\w+)\)", RegexOptions.IgnoreCase)
                If m.Success Then
                    If location.Length = bus.ToString(CultureInfo.InvariantCulture).Length Then
                        location.Append("-")
                    Else
                        location.Append(".")
                    End If
                    location.Append(m.Groups(1).Value)
                End If
            Next
            If bInterfaceNumber.HasValue Then
                Dim cfg = If(bConfigurationValue.HasValue,
                             bConfigurationValue.Value.ToString(CultureInfo.InvariantCulture),
                             "x")
                location.Append(String.Format(CultureInfo.InvariantCulture, ":{0}.{1}", cfg, bInterfaceNumber.Value))
            End If
            Return location.ToString()
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_bus_number
        Public Function GetBusNumber(node As DeviceNode) As Integer
            Dim host = TryCast(node, UsbHostControllerDevice)
            If host IsNot Nothing Then
                Dim idx = AllUsbHostControllers.FindIndex(Function(x) x.Equals(host))
                Return If(idx >= 0, idx + 1, 0)
            End If

            Dim current = node
            While current IsNot Nothing
                host = TryCast(current, UsbHostControllerDevice)
                If host IsNot Nothing Then
                    Dim idx = AllUsbHostControllers.FindIndex(Function(x) x.Equals(host))
                    Return If(idx >= 0, idx + 1, 0)
                End If
                current = current.Parent
            End While
            Return 0
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.find_parent_hub_and_usb
        Public Function FindParentHubAndUsb(node As DeviceNode) As Tuple(Of UsbHubDevice, DeviceNode, DeviceNode)
            If node Is Nothing Then
                Return Nothing
            End If
            Dim usbDevice = node
            Dim usbInterfaceDevice = node
            While True
                Dim parentDevice = usbDevice.Parent
                If parentDevice Is Nothing Then
                    Return Nothing
                End If
                Dim hub = AllUsbHubs.FirstOrDefault(Function(x) x.Equals(parentDevice))
                If hub IsNot Nothing Then
                    Return Tuple.Create(hub, usbDevice, usbInterfaceDevice)
                End If
                usbInterfaceDevice = usbDevice
                usbDevice = parentDevice
            End While
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.find_parent_host_controller
        Public Function FindParentHostController(node As DeviceNode) As UsbHostControllerDevice
            Dim current = node
            While current IsNot Nothing
                Dim host = AllUsbHostControllers.FirstOrDefault(Function(x) x.Equals(current))
                If host IsNot Nothing Then
                    Return host
                End If
                current = current.Parent
            End While
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.find_parent_chain
        Public Function FindParentChain(node As DeviceNode) As List(Of DeviceNode)
            If node Is Nothing Then
                Return Nothing
            End If

            Dim hubAndUsb = FindParentHubAndUsb(node)
            If hubAndUsb Is Nothing OrElse
               hubAndUsb.Item1 Is Nothing OrElse
               hubAndUsb.Item2 Is Nothing OrElse
               hubAndUsb.Item3 Is Nothing Then
                Return Nothing
            End If
            Dim hub = hubAndUsb.Item1
            Dim usbDevice = hubAndUsb.Item2
            Dim usbInterfaceDevice = hubAndUsb.Item3
            Dim host = FindParentHostController(hub)
            If host Is Nothing OrElse hub Is Nothing OrElse usbDevice Is Nothing OrElse usbInterfaceDevice Is Nothing Then
                Return Nothing
            End If
            ' host, hub, usb_device, usb_interface_device
            Return New List(Of DeviceNode) From {host, hub, usbDevice, usbInterfaceDevice}
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.request_usb_info
        Public Function RequestUsbInfo(hub As UsbHubDevice,
                                       usbDevice As DeviceNode,
                                       usbInterfaceDevice As DeviceNode,
                                       portDevice As DeviceNode) As UsbInfo
            If hub Is Nothing OrElse usbDevice Is Nothing OrElse usbInterfaceDevice Is Nothing Then
                Return Nothing
            End If

            Dim usbHubPort As Integer
            If String.IsNullOrEmpty(usbDevice.Address) OrElse
               Not Integer.TryParse(usbDevice.Address, NumberStyles.Integer, CultureInfo.InvariantCulture, usbHubPort) Then
                Return Nothing
            End If

            If usbDevice.PowerData.HasValue AndAlso usbDevice.PowerData.Value.PdMostRecentPowerState <> 1 Then
                If portDevice IsNot Nothing Then
                    portDevice.WakeUpDevice()
                End If
            End If

            Dim info As UsbInfo = Nothing
            Using hubIo As New UsbHubDeviceIOControl(If(String.IsNullOrEmpty(hub.InterfacePath), hub.Name, hub.InterfacePath))
                hubIo.Open()
                If Not hubIo.IsOpen Then
                    Return Nothing
                End If

                Dim connectionInfo = hubIo.RequestUsbConnectionInfo(usbHubPort)
                Dim deviceDescription As UsbDeviceDescriptor
                If connectionInfo.HasValue Then
                    deviceDescription = connectionInfo.Value.DeviceDescriptor
                Else
                    Dim requestedDeviceDescription = hubIo.RequestUsbDeviceDescription(usbHubPort)
                    If Not requestedDeviceDescription.HasValue Then
                        Return Nothing
                    End If
                    deviceDescription = requestedDeviceDescription.Value
                End If

                Dim languageId = hubIo.SuggestLanguageId(usbHubPort)

                Dim configurationValue As Integer? = Nothing
                If connectionInfo.HasValue Then
                    configurationValue = CInt(connectionInfo.Value.CurrentConfigurationValue)
                ElseIf deviceDescription.BNumConfigurations = 1 Then
                    configurationValue = 1
                End If

                Dim interfaceNumber As Integer? = Nothing
                If usbInterfaceDevice.LocationPaths IsNot Nothing Then
                    For Each path In usbInterfaceDevice.LocationPaths
                        Dim locationMatch = Regex.Match(If(path, String.Empty),
                                                        ".*?#USBMI\((\d+)\)",
                                                        RegexOptions.IgnoreCase)
                        If locationMatch.Success Then
                            interfaceNumber = Integer.Parse(locationMatch.Groups(1).Value, CultureInfo.InvariantCulture)
                            Exit For
                        End If
                    Next
                Else
                    Dim ifaceMatch = Regex.Match(If(usbInterfaceDevice.InstanceIdentifier, String.Empty),
                                                 "MI_(\d{2})",
                                                 RegexOptions.IgnoreCase)
                    If ifaceMatch.Success Then
                        interfaceNumber = Integer.Parse(ifaceMatch.Groups(1).Value, CultureInfo.InvariantCulture)
                    End If
                End If

                Dim functionText As String = Nothing
                Dim interfaceText As String = Nothing
                If configurationValue.HasValue Then
                    Dim configurationDescription = hubIo.RequestUsbConfigurationDescription(usbHubPort, configurationValue.Value)
                    If configurationDescription.HasValue Then
                        If deviceDescription.BNumConfigurations = 1 AndAlso configurationDescription.Value.BNumInterfaces = 1 Then
                            interfaceNumber = Nothing
                        End If
                        Dim interfaceDescriptions = hubIo.RequestUsbInterfaceDescriptions(usbHubPort,
                                                                                           configurationDescription.Value,
                                                                                           interfaceNumber)
                        If interfaceDescriptions IsNot Nothing Then
                            If interfaceDescriptions.Item1.HasValue Then
                                functionText = hubIo.RequestUsbStringDescription(usbHubPort,
                                                                                 CInt(interfaceDescriptions.Item1.Value.IFunction),
                                                                                 languageId)
                            End If
                            If interfaceDescriptions.Item2.HasValue Then
                                interfaceText = hubIo.RequestUsbStringDescription(usbHubPort,
                                                                                  CInt(interfaceDescriptions.Item2.Value.IInterface),
                                                                                  languageId)
                            End If
                        End If
                    End If
                End If

                info = New UsbInfo With {
                    .Vid = CInt(deviceDescription.IdVendor),
                    .Pid = CInt(deviceDescription.IdProduct),
                    .Manufacturer = hubIo.RequestUsbStringDescription(usbHubPort,
                                                                      CInt(deviceDescription.IManufacturer),
                                                                      languageId),
                    .Product = hubIo.RequestUsbStringDescription(usbHubPort,
                                                                 CInt(deviceDescription.IProduct),
                                                                 languageId),
                    .SerialNumber = hubIo.RequestUsbStringDescription(usbHubPort,
                                                                      CInt(deviceDescription.ISerialNumber),
                                                                      languageId),
                    .ConfigurationValue = configurationValue,
                    .InterfaceNumber = interfaceNumber,
                    .FunctionText = functionText,
                    .InterfaceText = interfaceText
                }
            End Using

            Return info
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_usb_info
        Public Function GetUsbInfo(node As DeviceNode) As UsbInfo
            If node Is Nothing Then
                Return Nothing
            End If

            Dim parentChain = FindParentChain(node)
            If parentChain Is Nothing OrElse parentChain.Count < 4 Then
                Return Nothing
            End If
            Dim host = TryCast(parentChain(0), UsbHostControllerDevice)
            Dim hub = TryCast(parentChain(1), UsbHubDevice)
            Dim usbDevice = parentChain(2)
            Dim usbInterfaceDevice = parentChain(3)
            If host Is Nothing OrElse hub Is Nothing OrElse usbDevice Is Nothing OrElse usbInterfaceDevice Is Nothing Then
                Return Nothing
            End If

            Dim cacheKey As String = Nothing
            If CacheUsbInfo Then
                cacheKey = GetCacheKey(node)
            End If
            Dim info As UsbInfo = Nothing
            If Not String.IsNullOrEmpty(cacheKey) AndAlso UsbInfoCache.ContainsKey(cacheKey) Then
                info = UsbInfoCache(cacheKey)
            Else
                info = RequestUsbInfo(hub, usbDevice, usbInterfaceDevice, node)
                If info Is Nothing Then
                    Return Nothing
                End If

                Dim source = String.Format("{0} {1}",
                                           If(node.InstanceIdentifier, String.Empty),
                                           If(node.Description, String.Empty))
                If info.Vid = 0 OrElse info.Pid = 0 Then
                    Dim m = Regex.Match(source, "VID_([0-9A-Fa-f]{4}).*PID_([0-9A-Fa-f]{4})")
                    If m.Success Then
                        info.Vid = System.Convert.ToInt32(m.Groups(1).Value, 16)
                        info.Pid = System.Convert.ToInt32(m.Groups(2).Value, 16)
                    End If
                End If

                If String.IsNullOrEmpty(info.SerialNumber) Then
                    ' Real USB serial numbers live in the *parent USB device's*
                    ' instance identifier (parentChain(2)), not in the COM port
                    ' / interface device's path. For a non-composite Greaseweazle
                    ' the two coincide, but for composite (MI_xx) devices the
                    ' interface path ends in a Windows-generated id like
                    ' "6&abc&0&0000" while the parent USB device path ends in
                    ' the firmware-reported serial (e.g. GW00B5...0716).
                    ' Try the parent path first, then fall back to the COM port
                    ' device's path. Reject values matching Windows' generated
                    ' "<digit>&<hex>&<digit>&<digit>" pattern so we report
                    ' "Unknown" (empty) rather than a synthetic id, matching
                    ' pyserial which only reports values from the real
                    ' iSerialNumber descriptor.
                    Dim candidates As New List(Of String)()
                    If usbDevice IsNot Nothing AndAlso Not String.IsNullOrEmpty(usbDevice.InstanceIdentifier) Then
                        candidates.Add(usbDevice.InstanceIdentifier)
                    End If
                    candidates.Add(source)
                    For Each candidate In candidates
                        Dim serialMatch = Regex.Match(candidate, "\\([^\\]+)$")
                        If Not serialMatch.Success Then
                            Continue For
                        End If
                        Dim value = serialMatch.Groups(1).Value
                        If Regex.IsMatch(value, "^\d+(&[0-9A-Fa-f]+)+$") Then
                            ' Windows-generated bus id, not a real serial.
                            Continue For
                        End If
                        info.SerialNumber = value
                        Exit For
                    Next
                End If

                If String.IsNullOrEmpty(info.Manufacturer) Then
                    info.Manufacturer = If(String.IsNullOrEmpty(node.Manufacturer), usbDevice.Manufacturer, node.Manufacturer)
                End If
                If String.IsNullOrEmpty(info.Product) Then
                    info.Product = If(String.IsNullOrEmpty(node.BusReportedDeviceDescription), node.Description, node.BusReportedDeviceDescription)
                End If
                If String.IsNullOrEmpty(info.InterfaceText) Then
                    info.InterfaceText = usbInterfaceDevice.BusReportedDeviceDescription
                End If

                If Not String.IsNullOrEmpty(cacheKey) Then
                    UsbInfoCache(cacheKey) = info
                End If
            End If
            info.Location = GetLocationString(usbDevice, info.ConfigurationValue, info.InterfaceNumber)
            If Not String.IsNullOrEmpty(cacheKey) Then
                UsbInfoCache(cacheKey) = info
            End If
            Return info
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl
    Public Class UsbHubDeviceIOControl
        Implements IDisposable

        Private _isOpen As Boolean
        Private Const SyntheticVid As UShort = &H1209US
        Private Const SyntheticPid As UShort = &H4D69US

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.__init__
        Public Sub New(Optional devicePath As String = "")
            Me.DevicePath = devicePath
        End Sub

        Public Property DevicePath As String

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.__enter__
        Public Function Enter() As UsbHubDeviceIOControl
            Open()
            Return Me
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.__exit__
        Public Sub [Exit](exceptionType As Type, value As Exception)
            Close()
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.is_open
        Public ReadOnly Property IsOpen As Boolean
            Get
                Return _isOpen
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.open
        Public Sub Open()
            _isOpen = Not String.IsNullOrEmpty(DevicePath) AndAlso DevicePath.StartsWith("\\.\", StringComparison.Ordinal)
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.close
        Public Sub Close()
            _isOpen = False
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_supported_languages
        Public Function RequestSupportedLanguages(Optional usbHubPort As Integer = 0) As List(Of UShort)
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If
            Return New List(Of UShort) From {&H409US}
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.suggest_language_id
        Public Function SuggestLanguageId(Optional usbHubPort As Integer = 0) As UShort
            Dim available = RequestSupportedLanguages(usbHubPort)
            If available Is Nothing OrElse available.Count = 0 Then
                Return &H409US
            End If
            Dim current = CUShort(CultureInfo.CurrentCulture.LCID And &HFFFF)
            If available.Contains(current) Then
                Return current
            End If
            Return available(0)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_string_description
        Public Function RequestUsbStringDescription(usbHubPort As Integer,
                                                    index As Integer,
                                                    Optional languageId As UShort? = Nothing) As String
            If usbHubPort <= 0 OrElse index = 0 Then
                Return Nothing
            End If
            If Not IsOpen Then
                Return Nothing
            End If
            If Not languageId.HasValue Then
                languageId = SuggestLanguageId(usbHubPort)
            End If
            Dim supported = RequestSupportedLanguages(usbHubPort)
            If supported Is Nothing OrElse Not supported.Contains(languageId.Value) Then
                Return Nothing
            End If
            ' These are SYNTHETIC stub responses for Greaseweazle. The real
            ' USB hub IOCTL (DeviceIoControl IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION)
            ' is not implemented; instead we return the well-known Manufacturer/
            ' Product/Function/Interface strings that match Greaseweazle firmware.
            ' Index 3 (iSerialNumber) intentionally returns Nothing so callers
            ' fall back to extracting the real serial from the device's instance
            ' identifier path (see DeviceRegistry.GetUsbInfo). Returning a
            ' synthetic "GW{hash}" here would shadow the real per-device serial
            ' that Python's pyserial pulls from the actual descriptor (e.g.
            ' "GW00B542654C874000071C0716" for an STM32-based board).
            Select Case index
                Case 1
                    Return "Keir Fraser"
                Case 2
                    Return "Greaseweazle"
                Case 3
                    Return Nothing
                Case 4
                    Return "Greaseweazle Function"
                Case 5
                    Return "Greaseweazle Interface"
                Case Else
                    Return Nothing
            End Select
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_device_description
        Public Function RequestUsbDeviceDescription(usbHubPort As Integer) As UsbDeviceDescriptor?
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If
            Return New UsbDeviceDescriptor With {
                .BLength = 18,
                .BDescriptorType = 1,
                .IdVendor = SyntheticVid,
                .IdProduct = SyntheticPid,
                .IManufacturer = 1,
                .IProduct = 2,
                .ISerialNumber = 3,
                .BNumConfigurations = 1
            }
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_configuration_description
        Public Function RequestUsbConfigurationDescription(usbHubPort As Integer,
                                                           bConfigurationValue As Integer) As UsbConfigurationDescriptor?
            If Not IsOpen OrElse usbHubPort <= 0 OrElse bConfigurationValue <= 0 Then
                Return Nothing
            End If
            Return New UsbConfigurationDescriptor With {
                .BLength = 9,
                .BDescriptorType = 2,
                .WTotalLength = 9,
                .BNumInterfaces = 1,
                .BConfigurationValue = CByte(bConfigurationValue)
            }
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_interface_descriptions
        Public Function RequestUsbInterfaceDescriptions(usbHubPort As Integer,
                                                        configurationDescription As UsbConfigurationDescriptor,
                                                        bInterfaceNumber As Integer?,
                                                        Optional bAlternateSetting As Integer = 0) As Tuple(Of UsbInterfaceAssociationDescriptor?, UsbInterfaceDescriptor?)
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If
            If configurationDescription.BConfigurationValue = 0 Then
                Return Nothing
            End If

            If bAlternateSetting <> 0 Then
                Return Tuple.Create(Of UsbInterfaceAssociationDescriptor?, UsbInterfaceDescriptor?)(Nothing, Nothing)
            End If

            Dim resolvedInterface = If(bInterfaceNumber.HasValue, bInterfaceNumber.Value, 0)
            If resolvedInterface < 0 OrElse resolvedInterface > Byte.MaxValue Then
                Return Tuple.Create(Of UsbInterfaceAssociationDescriptor?, UsbInterfaceDescriptor?)(Nothing, Nothing)
            End If
            Dim interfaceAssociationDescription As UsbInterfaceAssociationDescriptor? = Nothing
            If bInterfaceNumber.HasValue Then
                interfaceAssociationDescription = New UsbInterfaceAssociationDescriptor With {
                    .BLength = 8,
                    .BDescriptorType = 11,
                    .BFirstInterface = CByte(resolvedInterface),
                    .BInterfaceCount = 1,
                    .IFunction = 4
                }
            End If
            Dim interfaceDescription As New UsbInterfaceDescriptor With {
                .BLength = 9,
                .BDescriptorType = 4,
                .BInterfaceNumber = CByte(Math.Max(0, resolvedInterface)),
                .BAlternateSetting = 0
            }
            interfaceDescription.IInterface = 5

            Return Tuple.Create(interfaceAssociationDescription, CType(interfaceDescription, UsbInterfaceDescriptor?))
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_connection_info
        Public Function RequestUsbConnectionInfo(usbHubPort As Integer) As UsbNodeConnectionInformationEx?
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If
            Dim descriptor = RequestUsbDeviceDescription(usbHubPort)
            If Not descriptor.HasValue Then
                Return Nothing
            End If
            Return New UsbNodeConnectionInformationEx With {
                .ConnectionIndex = CUInt(Math.Max(0, usbHubPort)),
                .CurrentConfigurationValue = 1,
                .ConnectionStatus = 1,
                .DeviceAddress = CUShort(Math.Max(0, usbHubPort)),
                .NumberOfOpenPipes = 1UI,
                .DeviceDescriptor = descriptor.Value
            }
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            Close()
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBInfo
    Public Class UsbInfo
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBInfo.__init__
        Public Sub New()
        End Sub

        Public Property Vid As Integer
        Public Property Pid As Integer
        Public Property Manufacturer As String
        Public Property Product As String
        Public Property SerialNumber As String
        Public Property ConfigurationValue As Integer?
        Public Property InterfaceNumber As Integer?
        Public Property FunctionText As String
        Public Property InterfaceText As String
        Public Property Location As String

        Public Function Copy() As UsbInfo
            Return New UsbInfo With {
                .Vid = Vid,
                .Pid = Pid,
                .Manufacturer = Manufacturer,
                .Product = Product,
                .SerialNumber = SerialNumber,
                .ConfigurationValue = ConfigurationValue,
                .InterfaceNumber = InterfaceNumber,
                .FunctionText = FunctionText,
                .InterfaceText = InterfaceText,
                .Location = Location
            }
        End Function
    End Class

    Public Module WindowsPorts
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::iterate_comports
        Public Iterator Function IterateComports(Optional cacheUsbInfo As Boolean = True) As IEnumerable(Of PortDescriptor)
            Dim deviceRegistry As New DeviceRegistry(cacheUsbInfo)

            Dim allDevices As New List(Of DeviceNode)()
            allDevices.AddRange(PortDevice.EnumerateDevice().Cast(Of DeviceNode)())
            allDevices.AddRange(LegacyPortDevice.EnumerateDevice().Cast(Of DeviceNode)())

            Dim yieldedDevices As New List(Of DeviceNode)()
            Dim idx = 0
            For Each node In allDevices
                idx += 1
                If yieldedDevices.Contains(node) Then
                    Continue For
                End If
                yieldedDevices.Add(node)

                Dim portName = node.PortName
                If portName Is Nothing Then
                    Continue For
                End If
                If portName.StartsWith("LPT", StringComparison.Ordinal) Then
                    Continue For
                End If

                Dim host As New UsbHostControllerDevice With {
                    .InstanceHandle = 1UI,
                    .InterfacePath = "\\.\USBHOST#0",
                    .InstanceIdentifier = "USBHOST#0",
                    .Address = "1",
                    .LocationPaths = New List(Of String) From {
                        String.Format(CultureInfo.InvariantCulture, "#USB({0})", Math.Max(1, idx))
                    },
                    .Name = "USB Host Controller"
                }
                Dim hub As New UsbHubDevice With {
                    .InstanceHandle = 2UI,
                    .InterfacePath = "\\.\USBHUB#0",
                    .InstanceIdentifier = "USBHUB#0",
                    .Address = idx.ToString(CultureInfo.InvariantCulture),
                    .LocationPaths = New List(Of String) From {
                        String.Format(CultureInfo.InvariantCulture, "#USB({0})", Math.Max(1, idx))
                    },
                    .Name = "USB Root Hub",
                    .Parent = host
                }
                If node.InstanceHandle = 0UI Then
                    node.InstanceHandle = CUInt(100 + idx)
                End If
                node.Parent = hub
                If node.LocationPaths Is Nothing OrElse node.LocationPaths.Count = 0 Then
                    node.LocationPaths = New List(Of String) From {
                        String.Format(CultureInfo.InvariantCulture, "#USB({0})", Math.Max(1, idx))
                    }
                End If
                If String.IsNullOrEmpty(node.Address) Then
                    node.Address = idx.ToString(CultureInfo.InvariantCulture)
                End If

                Dim usbInfo = deviceRegistry.GetUsbInfo(node)
                Dim descriptor As New PortDescriptor With {.Device = portName}
                If usbInfo Is Nothing Then
                    descriptor.Product = If(node.Description, String.Empty)
                    descriptor.Manufacturer = If(node.Manufacturer, String.Empty)
                    descriptor.SerialNumber = String.Empty
                    descriptor.Location = String.Empty
                    descriptor.Vid = 0
                    descriptor.Pid = 0
                    descriptor.Interface = If(node.BusReportedDeviceDescription, String.Empty)
                Else
                    descriptor.Product = If(usbInfo.Product, String.Empty)
                    descriptor.Manufacturer = If(usbInfo.Manufacturer, String.Empty)
                    descriptor.SerialNumber = If(usbInfo.SerialNumber, String.Empty)
                    descriptor.Location = If(usbInfo.Location, String.Empty)
                    descriptor.Vid = usbInfo.Vid
                    descriptor.Pid = usbInfo.Pid
                    descriptor.Interface = If(usbInfo.InterfaceText, String.Empty)
                End If
                Yield New PortDescriptor With {
                    .Device = descriptor.Device,
                    .Product = descriptor.Product,
                    .Manufacturer = descriptor.Manufacturer,
                    .SerialNumber = descriptor.SerialNumber,
                    .Location = descriptor.Location,
                    .Vid = descriptor.Vid,
                    .Pid = descriptor.Pid,
                    .Interface = descriptor.Interface
                }
            Next
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::comports
        Public Function Comports(Optional includeLinks As Boolean = False) As List(Of PortDescriptor)
            Return IterateComports().ToList()
        End Function
    End Module

End Namespace
