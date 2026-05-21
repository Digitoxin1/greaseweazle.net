Imports System.Globalization
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions

Namespace Greaseweazle.Tools

    ' Windows-only port discovery. This is a faithful re-implementation of
    ' python_source/src/greaseweazle/tools/list_ports_windows.py — the
    ' Greaseweazle-vendored fork of pyserial PR #725 (chinaheyu/pyserial).
    '
    ' Enumerates COM ports via the Configuration Manager (CfgMgr32) APIs in
    ' exactly the same order Python does, then walks the parent chain on the
    ' device tree to find each port's parent USB hub. Real USB descriptors
    ' (iManufacturer / iProduct / iSerialNumber) are read by issuing
    ' DeviceIoControl IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION against
    ' the parent hub, matching pyserial.
    '
    ' Linux/macOS callers stay on the SerialPort.GetPortNames fallback in
    ' Tooling.EnumeratePorts; nothing in this file is portable.

    ' -------------------------------------------------------------------
    ' Public structs and DTOs (preserved as the existing public surface)
    ' -------------------------------------------------------------------

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::CM_POWER_DATA
    ' Public typed view of the CM_POWER_DATA buffer returned by
    ' DEVPKEY_Device_PowerData. The native layout is in NativeCmPowerData;
    ' this class is the consumer-facing one.
    Public Class CmPowerData
        Public Property PdSize As UInteger
        Public Property PdMostRecentPowerState As Integer
        Public Property PdCapabilities As UInteger
        Public Property PdD1Latency As UInteger
        Public Property PdD2Latency As UInteger
        Public Property PdD3Latency As UInteger
        Public Property PdPowerStateMapping As Integer()
        Public Property PdDeepestSystemWake As Integer
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

    ' -------------------------------------------------------------------
    ' DeviceNode / DeviceInterface
    ' Mirror python_source/.../list_ports_windows.py::DeviceNode + DeviceInterface
    ' (cached_property reads via CM_Get_*_PropertyW).
    ' -------------------------------------------------------------------

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::cached_property
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::cached_property.__init__
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::cached_property.__get__
    ' Python's cached_property decorator is replaced by VB's lazy-init
    ' property pattern: each reader stores the resolved value in a
    ' backing field and a paired _xxxLoaded flag, so the underlying
    ' CM_Get_*_PropertyW call only fires once per DeviceNode lifetime.
    Public Class DeviceNode
        Implements IComparable(Of DeviceNode), IEquatable(Of DeviceNode)

        Private ReadOnly _instanceHandle As UInteger

        ' Cached property storage (populated lazily, mirrors @cached_property).
        Private _instanceIdentifier As String
        Private _instanceIdentifierLoaded As Boolean
        Private _parent As DeviceNode
        Private _parentLoaded As Boolean
        Private _name As String
        Private _nameLoaded As Boolean
        Private _description As String
        Private _descriptionLoaded As Boolean
        Private _manufacturer As String
        Private _manufacturerLoaded As Boolean
        Private _address As Integer?
        Private _addressLoaded As Boolean
        Private _busReportedDeviceDescription As String
        Private _busReportedDeviceDescriptionLoaded As Boolean
        Private _friendlyName As String
        Private _friendlyNameLoaded As Boolean
        Private _locationPaths As List(Of String)
        Private _locationPathsLoaded As Boolean
        Private _portName As String
        Private _portNameLoaded As Boolean

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__init__
        Public Sub New(instanceHandle As UInteger)
            _instanceHandle = instanceHandle
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.instance_handle
        Public ReadOnly Property InstanceHandle As UInteger
            Get
                Return _instanceHandle
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.instance_identifier
        Public Overridable ReadOnly Property InstanceIdentifier As String
            Get
                If Not _instanceIdentifierLoaded Then
                    _instanceIdentifier = TryCast(GetProperty(NativeDevpropkeys.DEVPKEY_Device_InstanceId), String)
                    _instanceIdentifierLoaded = True
                End If
                Return _instanceIdentifier
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.parent
        Public ReadOnly Property Parent As DeviceNode
            Get
                If Not _parentLoaded Then
                    Dim parentHandle As UInteger
                    Dim cr = NativeMethods.CM_Get_Parent(parentHandle, _instanceHandle, 0UI)
                    If cr = NativeConstants.CR_SUCCESS Then
                        _parent = New DeviceNode(parentHandle)
                    Else
                        _parent = Nothing
                    End If
                    _parentLoaded = True
                End If
                Return _parent
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.name
        Public ReadOnly Property Name As String
            Get
                If Not _nameLoaded Then
                    _name = TryCast(GetProperty(NativeDevpropkeys.DEVPKEY_NAME), String)
                    _nameLoaded = True
                End If
                Return _name
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.description
        Public ReadOnly Property Description As String
            Get
                If Not _descriptionLoaded Then
                    _description = TryCast(GetProperty(NativeDevpropkeys.DEVPKEY_Device_DeviceDesc), String)
                    _descriptionLoaded = True
                End If
                Return _description
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.manufacturer
        Public ReadOnly Property Manufacturer As String
            Get
                If Not _manufacturerLoaded Then
                    _manufacturer = TryCast(GetProperty(NativeDevpropkeys.DEVPKEY_Device_Manufacturer), String)
                    _manufacturerLoaded = True
                End If
                Return _manufacturer
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.status
        ' Returns the (status, problem) pair from CM_Get_DevNode_Status.
        ' Greaseweazle's discovery flow does not consume this, but it is
        ' exposed so callers can inspect device-tree health for diagnostics
        ' (matches pyserial's @cached_property surface).
        Public ReadOnly Property Status As Tuple(Of UInteger, UInteger)
            Get
                Dim s As UInteger
                Dim p As UInteger
                Dim cr = NativeMethods.CM_Get_DevNode_Status(s, p, _instanceHandle, 0UI)
                If cr <> NativeConstants.CR_SUCCESS Then
                    Return Nothing
                End If
                Return Tuple.Create(s, p)
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.address
        ' DEVPKEY_Device_Address is a UInt32 — we expose it as Integer? so a
        ' missing property surfaces as Nothing without a sentinel value.
        Public ReadOnly Property Address As Integer?
            Get
                If Not _addressLoaded Then
                    Dim raw = GetProperty(NativeDevpropkeys.DEVPKEY_Device_Address)
                    If TypeOf raw Is UInteger Then
                        _address = CInt(CUInt(raw))
                    Else
                        _address = Nothing
                    End If
                    _addressLoaded = True
                End If
                Return _address
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.bus_reported_device_description
        Public ReadOnly Property BusReportedDeviceDescription As String
            Get
                If Not _busReportedDeviceDescriptionLoaded Then
                    _busReportedDeviceDescription = TryCast(GetProperty(NativeDevpropkeys.DEVPKEY_Device_BusReportedDeviceDesc), String)
                    _busReportedDeviceDescriptionLoaded = True
                End If
                Return _busReportedDeviceDescription
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.friendly_name
        Public ReadOnly Property FriendlyName As String
            Get
                If Not _friendlyNameLoaded Then
                    _friendlyName = TryCast(GetProperty(NativeDevpropkeys.DEVPKEY_Device_FriendlyName), String)
                    _friendlyNameLoaded = True
                End If
                Return _friendlyName
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.location_paths
        Public ReadOnly Property LocationPaths As List(Of String)
            Get
                If Not _locationPathsLoaded Then
                    _locationPaths = TryCast(GetProperty(NativeDevpropkeys.DEVPKEY_Device_LocationPaths), List(Of String))
                    _locationPathsLoaded = True
                End If
                Return _locationPaths
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.power_data
        ' Returns Nothing when the device doesn't expose CM_POWER_DATA. The
        ' typed CmPowerData wraps the underlying buffer for callers.
        Public ReadOnly Property PowerData As CmPowerData
            Get
                Dim raw = GetProperty(NativeDevpropkeys.DEVPKEY_Device_PowerData)
                Dim bytes = TryCast(raw, Byte())
                If bytes Is Nothing OrElse bytes.Length < Marshal.SizeOf(GetType(NativeCmPowerData)) Then
                    Return Nothing
                End If
                Dim native = NativeBuffers.ReadStruct(Of NativeCmPowerData)(bytes, 0)
                Return New CmPowerData With {
                    .PdSize = native.PdSize,
                    .PdMostRecentPowerState = native.PdMostRecentPowerState,
                    .PdCapabilities = native.PdCapabilities,
                    .PdD1Latency = native.PdD1Latency,
                    .PdD2Latency = native.PdD2Latency,
                    .PdD3Latency = native.PdD3Latency,
                    .PdPowerStateMapping = native.PdPowerStateMapping,
                    .PdDeepestSystemWake = native.PdDeepestSystemWake
                }
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.port_name
        ' Reads the "PortName" registry value under the device's hardware
        ' key. This is what gives us "COM3" for a USB CDC-ACM device whose
        ' instance identifier is the GUID-laden USB path.
        Public ReadOnly Property PortName As String
            Get
                If Not _portNameLoaded Then
                    _portName = ReadPortName()
                    _portNameLoaded = True
                End If
                Return _portName
            End Get
        End Property

        Private Function ReadPortName() As String
            Dim hKey As IntPtr = IntPtr.Zero
            Dim cr = NativeMethods.CM_Open_DevNode_Key(_instanceHandle,
                                                       NativeConstants.KEY_READ,
                                                       0UI,
                                                       NativeConstants.RegDisposition_OpenExisting,
                                                       hKey,
                                                       NativeConstants.CM_REGISTRY_HARDWARE)
            If cr <> NativeConstants.CR_SUCCESS OrElse hKey = IntPtr.Zero Then
                Return Nothing
            End If
            Try
                Dim bufferSize As UInteger = 0UI
                Dim status = NativeMethods.RegQueryValueExSize(hKey, "PortName", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, bufferSize)
                If status <> NativeConstants.ERROR_SUCCESS AndAlso status <> NativeConstants.ERROR_MORE_DATA Then
                    Return Nothing
                End If
                If bufferSize = 0UI Then
                    Return Nothing
                End If
                Dim data(CInt(bufferSize) - 1) As Byte
                Dim valueType As UInteger
                status = NativeMethods.RegQueryValueEx(hKey, "PortName", IntPtr.Zero, valueType, data, bufferSize)
                If status <> NativeConstants.ERROR_SUCCESS Then
                    Return Nothing
                End If
                Dim charCount As Integer = CInt(bufferSize \ 2UI)
                If charCount <= 0 Then
                    Return String.Empty
                End If
                Return System.Text.Encoding.Unicode.GetString(data, 0, charCount * 2).TrimEnd(ChrW(0))
            Finally
                NativeMethods.RegCloseKey(hKey)
            End Try
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.get_property
        ' Two-call pattern: first call with NULL buffer to discover the size,
        ' then allocate and fetch.
        Friend Overridable Function GetProperty(propertyKey As NativeDevpropkey) As Object
            Dim bufferSize As UInteger = 0UI
            Dim propertyType As UInteger
            Dim cr = NativeMethods.CM_Get_DevNode_PropertySize(_instanceHandle,
                                                               propertyKey,
                                                               propertyType,
                                                               IntPtr.Zero,
                                                               bufferSize,
                                                               0UI)
            If cr <> NativeConstants.CR_BUFFER_SMALL AndAlso cr <> NativeConstants.CR_SUCCESS Then
                Return Nothing
            End If
            If bufferSize = 0UI Then
                Return Nothing
            End If
            Dim buffer(CInt(bufferSize) - 1) As Byte
            cr = NativeMethods.CM_Get_DevNode_PropertyW(_instanceHandle,
                                                        propertyKey,
                                                        propertyType,
                                                        buffer,
                                                        bufferSize,
                                                        0UI)
            If cr <> NativeConstants.CR_SUCCESS Then
                Return Nothing
            End If
            Return NativeBuffers.ParseDeviceProperty(buffer, bufferSize, propertyType)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__str__
        Public Overrides Function ToString() As String
            Return Name
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__eq__
        Public Overloads Function Equals(other As DeviceNode) As Boolean Implements IEquatable(Of DeviceNode).Equals
            If other Is Nothing Then Return False
            Return _instanceHandle = other._instanceHandle
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return Equals(TryCast(obj, DeviceNode))
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__hash__
        Public Overrides Function GetHashCode() As Integer
            Return _instanceHandle.GetHashCode()
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceNode.__lt__
        Public Function CompareTo(other As DeviceNode) As Integer Implements IComparable(Of DeviceNode).CompareTo
            If other Is Nothing Then Return 1
            Return _instanceHandle.CompareTo(other._instanceHandle)
        End Function

        ' Best-effort wake-up — overridden by PortDevice / LegacyPortDevice.
        Public Overridable Sub WakeUpDevice()
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface
    Public Class DeviceInterface
        Inherits DeviceNode

        Private ReadOnly _interfacePath As String
        Private _instanceIdentifierOverride As String
        Private _instanceIdentifierOverrideLoaded As Boolean

        ' DeviceInterface needs its instance handle from CM_Locate_DevNodeW
        ' on the *interface property* DEVPKEY_Device_InstanceId. Resolve it
        ' once up front so MyBase.New can carry it.
        Private Shared Function ResolveInstanceHandle(interfacePath As String) As UInteger
            Dim instanceId As String = ResolveInterfaceInstanceId(interfacePath)
            If String.IsNullOrEmpty(instanceId) Then
                Return 0UI
            End If
            Dim handle As UInteger
            Dim cr = NativeMethods.CM_Locate_DevNodeW(handle, instanceId, NativeConstants.CM_LOCATE_DEVNODE_NORMAL)
            If cr <> NativeConstants.CR_SUCCESS Then
                Return 0UI
            End If
            Return handle
        End Function

        Private Shared Function ResolveInterfaceInstanceId(interfacePath As String) As String
            If String.IsNullOrEmpty(interfacePath) Then
                Return Nothing
            End If
            Dim bufferSize As UInteger = 0UI
            Dim propertyType As UInteger
            Dim key As NativeDevpropkey = NativeDevpropkeys.DEVPKEY_Device_InstanceId
            Dim cr = NativeMethods.CM_Get_Device_Interface_PropertySize(interfacePath,
                                                                        key,
                                                                        propertyType,
                                                                        IntPtr.Zero,
                                                                        bufferSize,
                                                                        0UI)
            If cr <> NativeConstants.CR_BUFFER_SMALL AndAlso cr <> NativeConstants.CR_SUCCESS Then
                Return Nothing
            End If
            If bufferSize = 0UI Then
                Return Nothing
            End If
            Dim buffer(CInt(bufferSize) - 1) As Byte
            cr = NativeMethods.CM_Get_Device_Interface_PropertyW(interfacePath,
                                                                 key,
                                                                 propertyType,
                                                                 buffer,
                                                                 bufferSize,
                                                                 0UI)
            If cr <> NativeConstants.CR_SUCCESS Then
                Return Nothing
            End If
            Return TryCast(NativeBuffers.ParseDeviceProperty(buffer, bufferSize, propertyType), String)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.__init__
        Public Sub New(interfacePath As String)
            MyBase.New(ResolveInstanceHandle(interfacePath))
            _interfacePath = interfacePath
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.interface
        Public ReadOnly Property [Interface] As String
            Get
                Return _interfacePath
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.instance_identifier
        ' Overrides the DeviceNode override because pyserial reads this
        ' specifically from the device interface side (not the devnode).
        Public Overrides ReadOnly Property InstanceIdentifier As String
            Get
                If Not _instanceIdentifierOverrideLoaded Then
                    _instanceIdentifierOverride = ResolveInterfaceInstanceId(_interfacePath)
                    _instanceIdentifierOverrideLoaded = True
                End If
                Return _instanceIdentifierOverride
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.get_interface_property
        Friend Function GetInterfaceProperty(propertyKey As NativeDevpropkey) As Object
            If String.IsNullOrEmpty(_interfacePath) Then
                Return Nothing
            End If
            Dim bufferSize As UInteger = 0UI
            Dim propertyType As UInteger
            Dim cr = NativeMethods.CM_Get_Device_Interface_PropertySize(_interfacePath,
                                                                        propertyKey,
                                                                        propertyType,
                                                                        IntPtr.Zero,
                                                                        bufferSize,
                                                                        0UI)
            If cr <> NativeConstants.CR_BUFFER_SMALL AndAlso cr <> NativeConstants.CR_SUCCESS Then
                Return Nothing
            End If
            If bufferSize = 0UI Then
                Return Nothing
            End If
            Dim buffer(CInt(bufferSize) - 1) As Byte
            cr = NativeMethods.CM_Get_Device_Interface_PropertyW(_interfacePath,
                                                                 propertyKey,
                                                                 propertyType,
                                                                 buffer,
                                                                 bufferSize,
                                                                 0UI)
            If cr <> NativeConstants.CR_SUCCESS Then
                Return Nothing
            End If
            Return NativeBuffers.ParseDeviceProperty(buffer, bufferSize, propertyType)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceInterface.enumerate_device
        Public Shared Iterator Function EnumerateInterfaces(possibleGuids As IEnumerable(Of Guid)) As IEnumerable(Of String)
            For Each guid In possibleGuids
                Dim listSize As UInteger = 0UI
                Dim guidLocal = guid
                Dim cr = NativeMethods.CM_Get_Device_Interface_List_SizeW(listSize,
                                                                          guidLocal,
                                                                          Nothing,
                                                                          NativeConstants.CM_GET_DEVICE_INTERFACE_LIST_PRESENT)
                If cr <> NativeConstants.CR_SUCCESS Then
                    Throw New System.ComponentModel.Win32Exception(CInt(NativeMethods.CM_MapCrToWin32Err(cr, 0UI)))
                End If
                If listSize <= 1UI Then
                    Continue For
                End If
                Dim buffer(CInt(listSize) - 1) As Char
                cr = NativeMethods.CM_Get_Device_Interface_ListW(guidLocal,
                                                                Nothing,
                                                                buffer,
                                                                listSize,
                                                                NativeConstants.CM_GET_DEVICE_INTERFACE_LIST_PRESENT)
                If cr <> NativeConstants.CR_SUCCESS Then
                    Throw New System.ComponentModel.Win32Exception(CInt(NativeMethods.CM_MapCrToWin32Err(cr, 0UI)))
                End If
                For Each path In NativeBuffers.ReadMultiSz(buffer, CInt(listSize))
                    Yield path
                Next
            Next
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::PortDevice
    Public Class PortDevice
        Inherits DeviceInterface

        Public Sub New(interfacePath As String)
            MyBase.New(interfacePath)
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::PortDevice.wake_up_device
        Public Overrides Sub WakeUpDevice()
            If String.IsNullOrEmpty([Interface]) Then
                Return
            End If
            Try
                Dim handle = NativeMethods.CreateFile([Interface],
                                                     NativeConstants.GENERIC_READ Or NativeConstants.GENERIC_WRITE,
                                                     0UI,
                                                     IntPtr.Zero,
                                                     NativeConstants.OPEN_EXISTING,
                                                     NativeConstants.FILE_ATTRIBUTE_NORMAL,
                                                     IntPtr.Zero)
                If handle <> NativeConstants.INVALID_HANDLE_VALUE Then
                    NativeMethods.CloseHandle(handle)
                End If
            Catch
                ' Best-effort wake-up only.
            End Try
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::PortDevice.enumerate_device
        Public Shared Iterator Function EnumerateDevice() As IEnumerable(Of PortDevice)
            Dim guids As Guid() = {NativeGuids.GUID_DEVINTERFACE_COMPORT, NativeGuids.GUID_DEVINTERFACE_MODEM}
            For Each path In DeviceInterface.EnumerateInterfaces(guids)
                Yield New PortDevice(path)
            Next
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::LegacyPortDevice
    Public Class LegacyPortDevice
        Inherits DeviceNode

        Public Sub New(instanceHandle As UInteger)
            MyBase.New(instanceHandle)
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::LegacyPortDevice.wake_up_device
        Public Overrides Sub WakeUpDevice()
            If String.IsNullOrEmpty(PortName) Then
                Return
            End If
            Dim path As String = "\\.\" & PortName
            Try
                Dim handle = NativeMethods.CreateFile(path,
                                                     NativeConstants.GENERIC_READ Or NativeConstants.GENERIC_WRITE,
                                                     0UI,
                                                     IntPtr.Zero,
                                                     NativeConstants.OPEN_EXISTING,
                                                     NativeConstants.FILE_ATTRIBUTE_NORMAL,
                                                     IntPtr.Zero)
                If handle <> NativeConstants.INVALID_HANDLE_VALUE Then
                    NativeMethods.CloseHandle(handle)
                End If
            Catch
                ' Best-effort wake-up only.
            End Try
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::LegacyPortDevice.enumerate_device
        Public Shared Iterator Function EnumerateDevice() As IEnumerable(Of LegacyPortDevice)
            Dim classGuids As Guid() = {NativeGuids.GUID_DEVCLASS_PORTS, NativeGuids.GUID_DEVCLASS_MODEM}
            For Each guid In classGuids
                Dim listSize As UInteger = 0UI
                Dim filter As String = guid.ToString("B")
                Dim flags As UInteger = NativeConstants.CM_GETIDLIST_FILTER_CLASS Or NativeConstants.CM_GETIDLIST_FILTER_PRESENT
                Dim cr = NativeMethods.CM_Get_Device_ID_List_SizeW(listSize, filter, flags)
                If cr <> NativeConstants.CR_SUCCESS Then
                    Throw New System.ComponentModel.Win32Exception(CInt(NativeMethods.CM_MapCrToWin32Err(cr, 0UI)))
                End If
                If listSize <= 1UI Then
                    Continue For
                End If
                Dim buffer(CInt(listSize) - 1) As Char
                cr = NativeMethods.CM_Get_Device_ID_ListW(filter, buffer, listSize, flags)
                If cr <> NativeConstants.CR_SUCCESS Then
                    Throw New System.ComponentModel.Win32Exception(CInt(NativeMethods.CM_MapCrToWin32Err(cr, 0UI)))
                End If
                For Each instanceId In NativeBuffers.ReadMultiSz(buffer, CInt(listSize))
                    Dim handle As UInteger
                    Dim crLocate = NativeMethods.CM_Locate_DevNodeW(handle, instanceId, NativeConstants.CM_LOCATE_DEVNODE_NORMAL)
                    If crLocate <> NativeConstants.CR_SUCCESS Then
                        Continue For
                    End If
                    Yield New LegacyPortDevice(handle)
                Next
            Next
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDevice
    Public Class UsbHubDevice
        Inherits DeviceInterface

        Public Sub New(interfacePath As String)
            MyBase.New(interfacePath)
        End Sub

        Public Shared Iterator Function EnumerateDevice() As IEnumerable(Of UsbHubDevice)
            For Each path In DeviceInterface.EnumerateInterfaces({NativeGuids.GUID_DEVINTERFACE_USB_HUB})
                Yield New UsbHubDevice(path)
            Next
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHostControllerDevice
    Public Class UsbHostControllerDevice
        Inherits DeviceInterface

        Public Sub New(interfacePath As String)
            MyBase.New(interfacePath)
        End Sub

        Public Shared Iterator Function EnumerateDevice() As IEnumerable(Of UsbHostControllerDevice)
            For Each path In DeviceInterface.EnumerateInterfaces({NativeGuids.GUID_DEVINTERFACE_USB_HOST_CONTROLLER})
                Yield New UsbHostControllerDevice(path)
            Next
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl
    ' Real DeviceIoControl implementation against the parent USB hub. The
    ' previous synthetic stub is gone; everything below issues real IOCTLs.
    ' Friend (assembly-local) because the methods deal in Native* structures
    ' that we don't want to leak as public surface.
    Friend Class UsbHubDeviceIOControl
        Implements IDisposable

        Private ReadOnly _devicePath As String
        Private _handle As IntPtr = NativeConstants.INVALID_HANDLE_VALUE

        Public Sub New(devicePath As String)
            _devicePath = devicePath
        End Sub

        Public ReadOnly Property DevicePath As String
            Get
                Return _devicePath
            End Get
        End Property

        Public ReadOnly Property IsOpen As Boolean
            Get
                Return _handle <> NativeConstants.INVALID_HANDLE_VALUE
            End Get
        End Property

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.open
        Public Sub Open()
            If String.IsNullOrEmpty(_devicePath) Then
                Return
            End If
            _handle = NativeMethods.CreateFile(_devicePath,
                                               NativeConstants.GENERIC_WRITE,
                                               NativeConstants.FILE_SHARE_WRITE,
                                               IntPtr.Zero,
                                               NativeConstants.OPEN_EXISTING,
                                               0UI,
                                               IntPtr.Zero)
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.close
        Public Sub Close()
            If _handle <> NativeConstants.INVALID_HANDLE_VALUE AndAlso _handle <> IntPtr.Zero Then
                NativeMethods.CloseHandle(_handle)
            End If
            _handle = NativeConstants.INVALID_HANDLE_VALUE
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.__enter__
        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.__exit__
        ' Python's context-manager protocol is replaced by .NET IDisposable
        ' + Using; callers wrap UsbHubDeviceIOControl in a Using block so
        ' Dispose runs in the same place __exit__ would.
        Public Sub Dispose() Implements IDisposable.Dispose
            Close()
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_supported_languages
        Public Function RequestSupportedLanguages(usbHubPort As Integer) As List(Of UShort)
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If

            Dim requestSize As Integer = Marshal.SizeOf(GetType(NativeUsbDescriptorRequest))
            Dim totalSize As Integer = requestSize + NativeConstants.MAX_USB_STRING_DESCRIPTOR_BYTES
            Dim buffer(totalSize - 1) As Byte
            Dim request As New NativeUsbDescriptorRequest With {
                .ConnectionIndex = CUInt(usbHubPort),
                .SetupPacket = New NativeSetupPacket With {
                    .WValue = CUShort(NativeConstants.USB_STRING_DESCRIPTOR_TYPE) << 8,
                    .WIndex = 0US,
                    .WLength = CUShort(NativeConstants.MAX_USB_STRING_DESCRIPTOR_BYTES)
                }
            }
            NativeBuffers.WriteStruct(buffer, 0, request)

            Dim returnedSize As UInteger
            Dim ok = NativeMethods.DeviceIoControl(_handle,
                                                   NativeConstants.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                                                   buffer,
                                                   CUInt(totalSize),
                                                   buffer,
                                                   CUInt(totalSize),
                                                   returnedSize,
                                                   IntPtr.Zero)
            If Not ok Then
                Return Nothing
            End If

            Dim descriptorBytes As Integer = CInt(returnedSize) - requestSize
            If descriptorBytes < 2 Then
                Return Nothing
            End If
            ' Header: bLength, bDescriptorType. Payload: array of UInt16
            ' language ids.
            Dim bLength As Integer = buffer(requestSize)
            If descriptorBytes <> bLength Then
                Return Nothing
            End If
            Dim languageCount As Integer = (bLength - 2) \ 2
            Dim languages As New List(Of UShort)(languageCount)
            For i = 0 To languageCount - 1
                Dim offset = requestSize + 2 + i * 2
                languages.Add(BitConverter.ToUInt16(buffer, offset))
            Next
            Return languages
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.suggest_language_id
        Public Function SuggestLanguageId(usbHubPort As Integer) As UShort
            Dim available = RequestSupportedLanguages(usbHubPort)
            If available Is Nothing OrElse available.Count = 0 Then
                Return NativeConstants.DEFAULT_LANG_ID
            End If
            Dim defaultLang = NativeMethods.GetUserDefaultLangID()
            If available.Contains(defaultLang) Then
                Return defaultLang
            End If
            Return available(0)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_string_description
        Public Function RequestUsbStringDescription(usbHubPort As Integer,
                                                    stringIndex As Integer,
                                                    Optional langId As UShort? = Nothing) As String
            If stringIndex = 0 OrElse Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If
            If Not langId.HasValue Then
                langId = SuggestLanguageId(usbHubPort)
            End If

            Dim requestSize As Integer = Marshal.SizeOf(GetType(NativeUsbDescriptorRequest))
            Dim totalSize As Integer = requestSize + NativeConstants.MAX_USB_STRING_DESCRIPTOR_BYTES
            Dim buffer(totalSize - 1) As Byte
            Dim request As New NativeUsbDescriptorRequest With {
                .ConnectionIndex = CUInt(usbHubPort),
                .SetupPacket = New NativeSetupPacket With {
                    .WValue = CUShort((CUShort(NativeConstants.USB_STRING_DESCRIPTOR_TYPE) << 8) Or CUShort(stringIndex And &HFF)),
                    .WIndex = langId.Value,
                    .WLength = CUShort(NativeConstants.MAX_USB_STRING_DESCRIPTOR_BYTES)
                }
            }
            NativeBuffers.WriteStruct(buffer, 0, request)

            Dim returnedSize As UInteger
            Dim ok = NativeMethods.DeviceIoControl(_handle,
                                                   NativeConstants.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                                                   buffer,
                                                   CUInt(totalSize),
                                                   buffer,
                                                   CUInt(totalSize),
                                                   returnedSize,
                                                   IntPtr.Zero)
            If Not ok Then
                Return Nothing
            End If

            Dim descriptorBytes As Integer = CInt(returnedSize) - requestSize
            If descriptorBytes < 2 Then
                Return Nothing
            End If
            Dim bLength As Integer = buffer(requestSize)
            If bLength <= 2 OrElse bLength > descriptorBytes Then
                Return Nothing
            End If
            ' Skip the 2-byte descriptor header; everything else is UTF-16.
            Dim payloadStart = requestSize + 2
            Dim payloadBytes = bLength - 2
            Dim raw As String = System.Text.Encoding.Unicode.GetString(buffer, payloadStart, payloadBytes)
            Return raw.TrimEnd(ChrW(0))
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_device_description
        Public Function RequestUsbDeviceDescription(usbHubPort As Integer) As NativeUsbDeviceDescriptor?
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If

            Dim requestSize As Integer = Marshal.SizeOf(GetType(NativeUsbDescriptorRequest))
            Dim descriptorSize As Integer = Marshal.SizeOf(GetType(NativeUsbDeviceDescriptor))
            Dim totalSize As Integer = requestSize + descriptorSize
            Dim buffer(totalSize - 1) As Byte
            Dim request As New NativeUsbDescriptorRequest With {
                .ConnectionIndex = CUInt(usbHubPort),
                .SetupPacket = New NativeSetupPacket With {
                    .BmRequest = &H80,
                    .BRequest = NativeConstants.USB_REQUEST_GET_DESCRIPTOR,
                    .WValue = CUShort(NativeConstants.USB_DEVICE_DESCRIPTOR_TYPE) << 8,
                    .WIndex = 0US,
                    .WLength = CUShort(descriptorSize)
                }
            }
            NativeBuffers.WriteStruct(buffer, 0, request)

            Dim returnedSize As UInteger
            Dim ok = NativeMethods.DeviceIoControl(_handle,
                                                   NativeConstants.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                                                   buffer,
                                                   CUInt(totalSize),
                                                   buffer,
                                                   CUInt(totalSize),
                                                   returnedSize,
                                                   IntPtr.Zero)
            If Not ok Then
                Return Nothing
            End If
            If CInt(returnedSize) - requestSize < descriptorSize Then
                Return Nothing
            End If
            Return NativeBuffers.ReadStruct(Of NativeUsbDeviceDescriptor)(buffer, requestSize)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_configuration_description
        Public Function RequestUsbConfigurationDescription(usbHubPort As Integer,
                                                           bConfigurationValue As Integer) As NativeUsbConfigurationDescriptor?
            If Not IsOpen OrElse usbHubPort <= 0 OrElse bConfigurationValue <= 0 Then
                Return Nothing
            End If

            Dim requestSize As Integer = Marshal.SizeOf(GetType(NativeUsbDescriptorRequest))
            Dim configSize As Integer = Marshal.SizeOf(GetType(NativeUsbConfigurationDescriptor))
            Dim totalSize As Integer = requestSize + configSize
            Dim buffer(totalSize - 1) As Byte
            Dim request As New NativeUsbDescriptorRequest With {
                .ConnectionIndex = CUInt(usbHubPort),
                .SetupPacket = New NativeSetupPacket With {
                    .BmRequest = &H80,
                    .BRequest = NativeConstants.USB_REQUEST_GET_DESCRIPTOR,
                    .WValue = CUShort((CUShort(NativeConstants.USB_CONFIGURATION_DESCRIPTOR_TYPE) << 8) Or CUShort((bConfigurationValue - 1) And &HFF)),
                    .WIndex = 0US,
                    .WLength = CUShort(configSize)
                }
            }
            NativeBuffers.WriteStruct(buffer, 0, request)

            Dim returnedSize As UInteger
            Dim ok = NativeMethods.DeviceIoControl(_handle,
                                                   NativeConstants.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                                                   buffer,
                                                   CUInt(totalSize),
                                                   buffer,
                                                   CUInt(totalSize),
                                                   returnedSize,
                                                   IntPtr.Zero)
            If Not ok Then
                Return Nothing
            End If
            If CInt(returnedSize) - requestSize < configSize Then
                Return Nothing
            End If
            Return NativeBuffers.ReadStruct(Of NativeUsbConfigurationDescriptor)(buffer, requestSize)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_interface_descriptions
        Public Function RequestUsbInterfaceDescriptions(usbHubPort As Integer,
                                                        configurationDescription As NativeUsbConfigurationDescriptor,
                                                        bInterfaceNumber As Integer?,
                                                        Optional bAlternateSetting As Integer = 0) As Tuple(Of NativeUsbInterfaceAssociationDescriptor?, NativeUsbInterfaceDescriptor?)
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If
            Dim resolvedInterface As Integer = If(bInterfaceNumber.HasValue, bInterfaceNumber.Value, 0)

            Dim requestSize As Integer = Marshal.SizeOf(GetType(NativeUsbDescriptorRequest))
            Dim totalSize As Integer = requestSize + CInt(configurationDescription.WTotalLength)
            Dim buffer(totalSize - 1) As Byte
            Dim request As New NativeUsbDescriptorRequest With {
                .ConnectionIndex = CUInt(usbHubPort),
                .SetupPacket = New NativeSetupPacket With {
                    .BmRequest = &H80,
                    .BRequest = NativeConstants.USB_REQUEST_GET_DESCRIPTOR,
                    .WValue = CUShort((CUShort(NativeConstants.USB_CONFIGURATION_DESCRIPTOR_TYPE) << 8) Or CUShort((configurationDescription.BConfigurationValue - 1) And &HFF)),
                    .WIndex = 0US,
                    .WLength = configurationDescription.WTotalLength
                }
            }
            NativeBuffers.WriteStruct(buffer, 0, request)

            Dim returnedSize As UInteger
            Dim ok = NativeMethods.DeviceIoControl(_handle,
                                                   NativeConstants.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION,
                                                   buffer,
                                                   CUInt(totalSize),
                                                   buffer,
                                                   CUInt(totalSize),
                                                   returnedSize,
                                                   IntPtr.Zero)
            If Not ok Then
                Return Nothing
            End If

            ' Walk the configuration buffer just like usbview/enum.c.
            Dim assocDesc As NativeUsbInterfaceAssociationDescriptor? = Nothing
            Dim ifaceDesc As NativeUsbInterfaceDescriptor? = Nothing
            Dim totalLength As Integer = CInt(configurationDescription.WTotalLength)
            Dim offset As Integer = 0
            Dim commonSize As Integer = 2 ' bLength + bDescriptorType
            Dim assocSize As Integer = Marshal.SizeOf(GetType(NativeUsbInterfaceAssociationDescriptor))
            Dim ifaceSize As Integer = Marshal.SizeOf(GetType(NativeUsbInterfaceDescriptor))

            While offset + commonSize <= totalLength
                Dim absolute = requestSize + offset
                Dim bLength As Integer = buffer(absolute)
                Dim bDescriptorType As Integer = buffer(absolute + 1)
                If bLength = 0 OrElse offset + bLength > totalLength Then
                    Exit While
                End If

                If bDescriptorType = NativeConstants.USB_INTERFACE_ASSOCIATION_DESCRIPTOR_TYPE Then
                    If bLength <> assocSize Then
                        Exit While
                    End If
                    Dim ad = NativeBuffers.ReadStruct(Of NativeUsbInterfaceAssociationDescriptor)(buffer, absolute)
                    Dim first As Integer = ad.BFirstInterface
                    Dim count As Integer = ad.BInterfaceCount
                    If first <= resolvedInterface AndAlso resolvedInterface <= first + count - 1 Then
                        assocDesc = ad
                    End If
                ElseIf bDescriptorType = NativeConstants.USB_INTERFACE_DESCRIPTOR_TYPE Then
                    If bLength <> ifaceSize Then
                        Exit While
                    End If
                    Dim id = NativeBuffers.ReadStruct(Of NativeUsbInterfaceDescriptor)(buffer, absolute)
                    If id.BInterfaceNumber = CByte(resolvedInterface) AndAlso id.BAlternateSetting = CByte(bAlternateSetting) Then
                        ifaceDesc = id
                    End If
                End If

                If ifaceDesc.HasValue Then
                    Exit While
                End If
                offset += bLength
            End While

            Return Tuple.Create(assocDesc, ifaceDesc)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::USBHubDeviceIOControl.request_usb_connection_info
        Public Function RequestUsbConnectionInfo(usbHubPort As Integer) As NativeUsbNodeConnectionInformationEx?
            If Not IsOpen OrElse usbHubPort <= 0 Then
                Return Nothing
            End If

            Dim size As Integer = Marshal.SizeOf(GetType(NativeUsbNodeConnectionInformationEx))
            Dim buffer(size - 1) As Byte
            ' First 4 bytes = ConnectionIndex.
            Dim header As New NativeUsbNodeConnectionInformationEx With {
                .ConnectionIndex = CUInt(usbHubPort)
            }
            NativeBuffers.WriteStruct(buffer, 0, header)

            Dim returnedSize As UInteger
            Dim ok = NativeMethods.DeviceIoControl(_handle,
                                                   NativeConstants.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX,
                                                   buffer,
                                                   CUInt(size),
                                                   buffer,
                                                   CUInt(size),
                                                   returnedSize,
                                                   IntPtr.Zero)
            If Not ok Then
                Return Nothing
            End If
            If CInt(returnedSize) < size Then
                Return Nothing
            End If
            Return NativeBuffers.ReadStruct(Of NativeUsbNodeConnectionInformationEx)(buffer, 0)
        End Function

    End Class

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry
    Public Class DeviceRegistry
        Private Shared ReadOnly UsbInfoCache As New Dictionary(Of String, UsbInfo)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _cacheUsbInfo As Boolean
        Private ReadOnly _allUsbHubs As List(Of UsbHubDevice)
        Private ReadOnly _allUsbHostControllers As List(Of UsbHostControllerDevice)

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.__init__
        Public Sub New(Optional cacheUsbInfo As Boolean = True)
            _cacheUsbInfo = cacheUsbInfo
            If Not cacheUsbInfo Then
                UsbInfoCache.Clear()
            End If
            _allUsbHubs = UsbHubDevice.EnumerateDevice().Distinct().OrderBy(Function(x) x).ToList()
            _allUsbHostControllers = UsbHostControllerDevice.EnumerateDevice().Distinct().OrderBy(Function(x) x).ToList()
        End Sub

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_cache_key
        Public Shared Function GetCacheKey(node As DeviceNode) As String
            If node Is Nothing OrElse String.IsNullOrEmpty(node.InstanceIdentifier) Then
                Return String.Empty
            End If
            Return node.InstanceIdentifier.ToLowerInvariant()
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_location_string
        Public Function GetLocationString(usbDevice As DeviceNode,
                                          usbHostController As UsbHostControllerDevice,
                                          Optional bConfigurationValue As Integer? = Nothing,
                                          Optional bInterfaceNumber As Integer? = Nothing) As String
            If usbDevice Is Nothing OrElse usbDevice.LocationPaths Is Nothing OrElse usbDevice.LocationPaths.Count = 0 Then
                Return Nothing
            End If
            Dim cfg As String = If(bConfigurationValue.HasValue,
                                   bConfigurationValue.Value.ToString(CultureInfo.InvariantCulture),
                                   "x")
            Dim parts As New List(Of String) From {GetBusNumber(usbHostController).ToString(CultureInfo.InvariantCulture)}
            Dim first As Boolean = True
            For Each m As Match In Regex.Matches(usbDevice.LocationPaths(0), "#USB\((\w+)\)", RegexOptions.IgnoreCase)
                If m.Success Then
                    parts.Add(If(first, "-", "."))
                    parts.Add(m.Groups(1).Value)
                    first = False
                End If
            Next
            If bInterfaceNumber.HasValue Then
                parts.Add(String.Format(CultureInfo.InvariantCulture, ":{0}.{1}", cfg, bInterfaceNumber.Value))
            End If
            Return String.Concat(parts)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_bus_number
        Public Function GetBusNumber(usbHostController As UsbHostControllerDevice) As Integer
            If usbHostController Is Nothing Then
                Return 0
            End If
            Dim idx = _allUsbHostControllers.FindIndex(Function(x) x.Equals(usbHostController))
            Return If(idx >= 0, idx + 1, 0)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.find_parent_hub_and_usb
        Public Function FindParentHubAndUsb(portDevice As DeviceNode) As Tuple(Of UsbHubDevice, DeviceNode, DeviceNode)
            If portDevice Is Nothing Then
                Return Nothing
            End If
            Dim usbDevice As DeviceNode = portDevice
            Dim usbInterfaceDevice As DeviceNode = portDevice
            While True
                Dim parentDevice = usbDevice.Parent
                If parentDevice Is Nothing Then
                    Return Nothing
                End If
                Dim hub = _allUsbHubs.FirstOrDefault(Function(x) x.Equals(parentDevice))
                If hub IsNot Nothing Then
                    Return Tuple.Create(hub, usbDevice, usbInterfaceDevice)
                End If
                usbInterfaceDevice = usbDevice
                usbDevice = parentDevice
            End While
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.find_parent_host_controller
        Public Function FindParentHostController(hubDevice As DeviceNode) As UsbHostControllerDevice
            Dim parent As DeviceNode = hubDevice
            While parent IsNot Nothing
                Dim host = _allUsbHostControllers.FirstOrDefault(Function(x) x.Equals(parent))
                If host IsNot Nothing Then
                    Return host
                End If
                parent = parent.Parent
            End While
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.find_parent_chain
        Public Function FindParentChain(portDevice As DeviceNode) As Tuple(Of UsbHostControllerDevice, UsbHubDevice, DeviceNode, DeviceNode)
            Dim parents = FindParentHubAndUsb(portDevice)
            If parents Is Nothing Then
                Return Nothing
            End If
            Dim hub = parents.Item1
            Dim usbDevice = parents.Item2
            Dim usbInterfaceDevice = parents.Item3
            Dim host = FindParentHostController(hub)
            If host Is Nothing Then
                Return Nothing
            End If
            Return Tuple.Create(host, hub, usbDevice, usbInterfaceDevice)
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.request_usb_info
        Public Shared Function RequestUsbInfo(hubDevice As UsbHubDevice,
                                              usbDevice As DeviceNode,
                                              usbInterfaceDevice As DeviceNode,
                                              portDevice As DeviceNode) As UsbInfo
            If hubDevice Is Nothing OrElse usbDevice Is Nothing OrElse usbInterfaceDevice Is Nothing Then
                Return Nothing
            End If

            Dim hubPort As Integer? = usbDevice.Address
            If Not hubPort.HasValue Then
                Return Nothing
            End If

            ' Wake the device if it has gone to sleep so the descriptor read
            ' returns valid data instead of an empty buffer.
            Dim power = usbDevice.PowerData
            If power IsNot Nothing AndAlso power.PdMostRecentPowerState <> 1 Then
                If portDevice IsNot Nothing Then
                    portDevice.WakeUpDevice()
                End If
            End If

            Using hubIo As New UsbHubDeviceIOControl(hubDevice.Interface)
                hubIo.Open()
                If Not hubIo.IsOpen Then
                    Return Nothing
                End If

                Dim connectionInfo As NativeUsbNodeConnectionInformationEx? = hubIo.RequestUsbConnectionInfo(hubPort.Value)
                Dim deviceDesc As NativeUsbDeviceDescriptor
                If connectionInfo.HasValue Then
                    deviceDesc = connectionInfo.Value.DeviceDescriptor
                Else
                    Dim fallback = hubIo.RequestUsbDeviceDescription(hubPort.Value)
                    If Not fallback.HasValue Then
                        Return Nothing
                    End If
                    deviceDesc = fallback.Value
                End If

                Dim languageId As UShort = hubIo.SuggestLanguageId(hubPort.Value)
                Dim productText = hubIo.RequestUsbStringDescription(hubPort.Value, deviceDesc.IProduct, languageId)
                Dim manufacturerText = hubIo.RequestUsbStringDescription(hubPort.Value, deviceDesc.IManufacturer, languageId)
                Dim serialText = hubIo.RequestUsbStringDescription(hubPort.Value, deviceDesc.ISerialNumber, languageId)

                Dim configValue As Integer? = Nothing
                If connectionInfo.HasValue Then
                    configValue = CInt(connectionInfo.Value.CurrentConfigurationValue)
                ElseIf deviceDesc.BNumConfigurations = 1 Then
                    configValue = 1
                End If

                Dim interfaceNumber As Integer? = ResolveInterfaceNumber(usbInterfaceDevice)

                Dim functionText As String = Nothing
                Dim interfaceText As String = Nothing
                If configValue.HasValue Then
                    Dim configDesc = hubIo.RequestUsbConfigurationDescription(hubPort.Value, configValue.Value)
                    If configDesc.HasValue Then
                        If deviceDesc.BNumConfigurations = 1 AndAlso configDesc.Value.BNumInterfaces = 1 Then
                            interfaceNumber = Nothing
                        End If
                        Dim ifaces = hubIo.RequestUsbInterfaceDescriptions(hubPort.Value, configDesc.Value, interfaceNumber)
                        If ifaces IsNot Nothing Then
                            If ifaces.Item1.HasValue Then
                                functionText = hubIo.RequestUsbStringDescription(hubPort.Value, ifaces.Item1.Value.IFunction, languageId)
                            End If
                            If ifaces.Item2.HasValue Then
                                interfaceText = hubIo.RequestUsbStringDescription(hubPort.Value, ifaces.Item2.Value.IInterface, languageId)
                            End If
                        End If
                    End If
                End If

                Return New UsbInfo With {
                    .Vid = CInt(deviceDesc.IdVendor),
                    .Pid = CInt(deviceDesc.IdProduct),
                    .Manufacturer = manufacturerText,
                    .Product = productText,
                    .SerialNumber = serialText,
                    .ConfigurationValue = configValue,
                    .InterfaceNumber = interfaceNumber,
                    .FunctionText = functionText,
                    .InterfaceText = interfaceText
                }
            End Using
        End Function

        Private Shared Function ResolveInterfaceNumber(usbInterfaceDevice As DeviceNode) As Integer?
            If usbInterfaceDevice Is Nothing Then
                Return Nothing
            End If
            Dim paths = usbInterfaceDevice.LocationPaths
            If paths IsNot Nothing AndAlso paths.Count > 0 Then
                For Each p In paths
                    Dim m = Regex.Match(If(p, String.Empty), "^.*?#USBMI\((\d+)\)$", RegexOptions.IgnoreCase)
                    If m.Success Then
                        Return Integer.Parse(m.Groups(1).Value, CultureInfo.InvariantCulture)
                    End If
                Next
                Return Nothing
            End If
            Dim id = usbInterfaceDevice.InstanceIdentifier
            If String.IsNullOrEmpty(id) Then
                Return Nothing
            End If
            Dim mid = Regex.Match(id, "^USB\\VID_[0-9A-Fa-f]{4}&PID_[0-9A-Fa-f]{4}&MI_(\d{2})\\.*?$", RegexOptions.IgnoreCase)
            If mid.Success Then
                Return Integer.Parse(mid.Groups(1).Value, CultureInfo.InvariantCulture)
            End If
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::DeviceRegistry.get_usb_info
        Public Function GetUsbInfo(portDevice As DeviceNode) As UsbInfo
            If portDevice Is Nothing Then
                Return Nothing
            End If
            Dim chain = FindParentChain(portDevice)
            If chain Is Nothing Then
                Return Nothing
            End If
            Dim host = chain.Item1
            Dim hub = chain.Item2
            Dim usbDevice = chain.Item3
            Dim usbInterfaceDevice = chain.Item4

            Dim cacheKey As String = Nothing
            If _cacheUsbInfo Then
                cacheKey = GetCacheKey(portDevice)
            End If
            Dim info As UsbInfo = Nothing
            If Not String.IsNullOrEmpty(cacheKey) AndAlso UsbInfoCache.TryGetValue(cacheKey, info) Then
                ' Use cached info but recompute Location below.
            Else
                info = RequestUsbInfo(hub, usbDevice, usbInterfaceDevice, portDevice)
                If info Is Nothing Then
                    Return Nothing
                End If
            End If

            info.Location = GetLocationString(usbDevice, host, info.ConfigurationValue, info.InterfaceNumber)

            If Not String.IsNullOrEmpty(cacheKey) Then
                UsbInfoCache(cacheKey) = info
            End If
            Return info
        End Function

    End Class

    ' Public façade. Tooling.EnumeratePorts calls Comports() to get
    ' List(Of PortDescriptor) for the scoring algorithm.
    Public Module WindowsPorts

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::iterate_comports
        Public Iterator Function IterateComports(Optional cacheUsbInfo As Boolean = True) As IEnumerable(Of PortDescriptor)
            Dim registry As New DeviceRegistry(cacheUsbInfo)
            Dim yielded As New HashSet(Of UInteger)()

            ' Build the iteration order Python uses: PortDevice (USB CDC-ACM
            ' interfaces) first, then LegacyPortDevice (raw COM/MODEM class
            ' nodes for legacy devices).
            Dim devices As New List(Of DeviceNode)()
            For Each pd In PortDevice.EnumerateDevice()
                devices.Add(pd)
            Next
            For Each lpd In LegacyPortDevice.EnumerateDevice()
                devices.Add(lpd)
            Next

            For Each node In devices
                If node.InstanceHandle = 0UI Then
                    Continue For
                End If
                If Not yielded.Add(node.InstanceHandle) Then
                    Continue For
                End If

                Dim portName = node.PortName
                If String.IsNullOrEmpty(portName) Then
                    Continue For
                End If
                If portName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase) Then
                    Continue For
                End If

                Dim usbInfo = registry.GetUsbInfo(node)
                Dim descriptor As New PortDescriptor With {.Device = portName}
                If usbInfo Is Nothing Then
                    descriptor.Manufacturer = If(node.Manufacturer, String.Empty)
                    descriptor.Product = If(node.Description, String.Empty)
                    descriptor.SerialNumber = String.Empty
                    descriptor.Location = String.Empty
                    descriptor.Vid = 0
                    descriptor.Pid = 0
                    descriptor.Interface = If(node.BusReportedDeviceDescription, String.Empty)
                Else
                    descriptor.Manufacturer = If(usbInfo.Manufacturer, String.Empty)
                    descriptor.Product = If(usbInfo.Product, String.Empty)
                    descriptor.SerialNumber = If(usbInfo.SerialNumber, String.Empty)
                    descriptor.Location = If(usbInfo.Location, String.Empty)
                    descriptor.Vid = usbInfo.Vid
                    descriptor.Pid = usbInfo.Pid
                    descriptor.Interface = If(usbInfo.InterfaceText, String.Empty)
                End If
                Yield descriptor
            Next
        End Function

        ' Python map: src/greaseweazle/tools/list_ports_windows.py::comports
        Public Function Comports(Optional includeLinks As Boolean = False) As List(Of PortDescriptor)
            Return IterateComports().ToList()
        End Function

    End Module

End Namespace
