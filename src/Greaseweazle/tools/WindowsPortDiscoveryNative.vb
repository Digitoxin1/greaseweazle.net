Imports System.Runtime.InteropServices

Namespace Greaseweazle.Tools

    ' Native (P/Invoke) layer for the Windows port discovery code in
    ' WindowsPortDiscovery.vb. Mirrors the ctypes block at the top of
    ' python_source/src/greaseweazle/tools/list_ports_windows.py:185-407.
    '
    ' Anything calling into setupapi/cfgmgr32/kernel32/advapi32 lives here so
    ' the orchestration file can stay focused on the class hierarchy. Win 7
    ' is supported: every API and DEVPROPKEY referenced below is available on
    ' Vista/Win 7 or later (cfgmgr32.dll has shipped these since Vista).

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::DEVPROPKEY
    ' (struct layout, packed for native interop with CM_Get_DevNode_PropertyW).
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeDevpropkey
        Public Fmtid As Guid
        Public Pid As UInteger
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_DEVICE_DESCRIPTOR
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeUsbDeviceDescriptor
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

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_CONFIGURATION_DESCRIPTOR
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeUsbConfigurationDescriptor
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
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeUsbInterfaceDescriptor
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
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeUsbInterfaceAssociationDescriptor
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
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeUsbNodeConnectionInformationEx
        Public ConnectionIndex As UInteger
        Public DeviceDescriptor As NativeUsbDeviceDescriptor
        Public CurrentConfigurationValue As Byte
        Public Speed As Byte
        Public DeviceIsHub As Byte
        Public DeviceAddress As UShort
        Public NumberOfOpenPipes As UInteger
        Public ConnectionStatus As UInteger
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::SetupPacket
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeSetupPacket
        Public BmRequest As Byte
        Public BRequest As Byte
        Public WValue As UShort
        Public WIndex As UShort
        Public WLength As UShort
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_DESCRIPTOR_REQUEST
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure NativeUsbDescriptorRequest
        Public ConnectionIndex As UInteger
        Public SetupPacket As NativeSetupPacket
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::CM_POWER_DATA
    <StructLayout(LayoutKind.Sequential)>
    Friend Structure NativeCmPowerData
        Public PdSize As UInteger
        Public PdMostRecentPowerState As Integer
        Public PdCapabilities As UInteger
        Public PdD1Latency As UInteger
        Public PdD2Latency As UInteger
        Public PdD3Latency As UInteger
        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=7)>
        Public PdPowerStateMapping As Integer()
        Public PdDeepestSystemWake As Integer
    End Structure

    ' Python map: src/greaseweazle/tools/list_ports_windows.py (the const block at lines 337-391).
    Friend Module NativeConstants

        ' ConfigMgr / SetupAPI return codes.
        Public Const CR_SUCCESS As UInteger = 0UI
        Public Const CR_BUFFER_SMALL As UInteger = 26UI

        ' Win32 error codes used by the registry path.
        Public Const ERROR_SUCCESS As Integer = 0
        Public Const ERROR_MORE_DATA As Integer = 234

        ' CM_Get_Device_ID_List flags.
        Public Const CM_GETIDLIST_FILTER_PRESENT As UInteger = &H100UI
        Public Const CM_GETIDLIST_FILTER_CLASS As UInteger = &H200UI

        ' CM_Get_Device_Interface_List flags.
        Public Const CM_GET_DEVICE_INTERFACE_LIST_PRESENT As UInteger = 0UI

        ' CM_Locate_DevNode flags.
        Public Const CM_LOCATE_DEVNODE_NORMAL As UInteger = 0UI

        ' Win32 file API.
        Public Const GENERIC_READ As UInteger = &H80000000UI
        Public Const GENERIC_WRITE As UInteger = &H40000000UI
        Public Const FILE_SHARE_READ As UInteger = 1UI
        Public Const FILE_SHARE_WRITE As UInteger = 2UI
        Public Const OPEN_EXISTING As UInteger = 3UI
        Public Const FILE_ATTRIBUTE_NORMAL As UInteger = &H80UI

        ' INVALID_HANDLE_VALUE = (HANDLE)-1.
        Public ReadOnly INVALID_HANDLE_VALUE As IntPtr = New IntPtr(-1)

        ' Registry access constants for CM_Open_DevNode_Key.
        Public Const KEY_READ As UInteger = &H20019UI
        Public Const RegDisposition_OpenExisting As UInteger = 1UI
        Public Const CM_REGISTRY_HARDWARE As UInteger = 0UI

        ' DEVPROPTYPE values relevant to the properties we read. The list
        ' parsing helper below covers the same subset Python's
        ' parse_device_property does.
        Public Const DEVPROP_TYPEMOD_ARRAY As UInteger = &H1000UI
        Public Const DEVPROP_TYPEMOD_LIST As UInteger = &H2000UI
        Public Const DEVPROP_TYPE_BYTE As UInteger = &H3UI
        Public Const DEVPROP_TYPE_UINT32 As UInteger = &H7UI
        Public Const DEVPROP_TYPE_STRING As UInteger = &H12UI
        Public Const DEVPROP_TYPE_BINARY As UInteger = (DEVPROP_TYPE_BYTE Or DEVPROP_TYPEMOD_ARRAY)
        Public Const DEVPROP_TYPE_STRING_LIST As UInteger = (DEVPROP_TYPE_STRING Or DEVPROP_TYPEMOD_LIST)

        ' USB descriptor types.
        Public Const USB_REQUEST_GET_DESCRIPTOR As Byte = 6
        Public Const USB_DEVICE_DESCRIPTOR_TYPE As Byte = 1
        Public Const USB_CONFIGURATION_DESCRIPTOR_TYPE As Byte = 2
        Public Const USB_STRING_DESCRIPTOR_TYPE As Byte = 3
        Public Const USB_INTERFACE_DESCRIPTOR_TYPE As Byte = 4
        Public Const USB_INTERFACE_ASSOCIATION_DESCRIPTOR_TYPE As Byte = &HB

        ' USB hub IOCTLs. Hard-coded values (CTL_CODE math) match
        ' python_source/src/greaseweazle/tools/list_ports_windows.py:382-385.
        Public Const IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION As UInteger = 2229264UI
        Public Const IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX As UInteger = 2229320UI
        Public Const IOCTL_USB_GET_ROOT_HUB_NAME As UInteger = 2229256UI
        Public Const IOCTL_USB_GET_NODE_CONNECTION_NAME As UInteger = 2229268UI

        ' English (United States) is the universal-fallback language id.
        Public Const DEFAULT_LANG_ID As UShort = &H409US

        ' Maximum bytes we'll attempt to read for a USB string descriptor.
        ' Matches the sizeof(USB_STRING_DESCRIPTOR) Python uses (2-byte
        ' header + 254 wide chars).
        Public Const MAX_USB_STRING_DESCRIPTOR_BYTES As Integer = 256

    End Module

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::GUID
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::GUID.__str__
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::GUID.__eq__
    ' Python's GUID/__str__/__eq__ wrap a 128-bit identifier. .NET ships
    ' System.Guid with the same semantics (ToString, Equals), so we use it
    ' directly and only declare the well-known instance values here.
    Friend Module NativeGuids
        Public ReadOnly GUID_DEVINTERFACE_USB_HUB As Guid =
            New Guid("F18A0E88-C30C-11D0-8815-00A0C906BED8")
        Public ReadOnly GUID_DEVINTERFACE_COMPORT As Guid =
            New Guid("86E0D1E0-8089-11D0-9CE4-08003E301F73")
        Public ReadOnly GUID_DEVINTERFACE_MODEM As Guid =
            New Guid("2C7089AA-2E0E-11D1-B114-00C04FC2AAE4")
        Public ReadOnly GUID_DEVINTERFACE_USB_HOST_CONTROLLER As Guid =
            New Guid("3ABF6F2D-71C4-462A-8A92-1E6861E6AF27")
        Public ReadOnly GUID_DEVCLASS_PORTS As Guid =
            New Guid("4D36E978-E325-11CE-BFC1-08002BE10318")
        Public ReadOnly GUID_DEVCLASS_MODEM As Guid =
            New Guid("4D36E96D-E325-11CE-BFC1-08002BE10318")
    End Module

    ' Python map: src/greaseweazle/tools/list_ports_windows.py (DEVPROPKEY block at lines 354-362).
    Friend Module NativeDevpropkeys

        Private Function MakeKey(fmtid As String, pid As UInteger) As NativeDevpropkey
            Return New NativeDevpropkey With {
                .Fmtid = New Guid(fmtid),
                .Pid = pid
            }
        End Function

        Public ReadOnly DEVPKEY_NAME As NativeDevpropkey =
            MakeKey("B725F130-47EF-101A-A5F1-02608C9EEBAC", 10UI)
        Public ReadOnly DEVPKEY_Device_DeviceDesc As NativeDevpropkey =
            MakeKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 2UI)
        Public ReadOnly DEVPKEY_Device_Manufacturer As NativeDevpropkey =
            MakeKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 13UI)
        Public ReadOnly DEVPKEY_Device_FriendlyName As NativeDevpropkey =
            MakeKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 14UI)
        Public ReadOnly DEVPKEY_Device_PowerData As NativeDevpropkey =
            MakeKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 32UI)
        Public ReadOnly DEVPKEY_Device_LocationPaths As NativeDevpropkey =
            MakeKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 37UI)
        Public ReadOnly DEVPKEY_Device_Address As NativeDevpropkey =
            MakeKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 30UI)
        Public ReadOnly DEVPKEY_Device_InstanceId As NativeDevpropkey =
            MakeKey("78C34FC8-104A-4ACA-9EA4-524D52996E57", 256UI)
        Public ReadOnly DEVPKEY_Device_BusReportedDeviceDesc As NativeDevpropkey =
            MakeKey("540B947E-8B40-45BC-A8A2-6A0B894CBDA2", 4UI)

    End Module

    ' Python map: src/greaseweazle/tools/list_ports_windows.py (P/Invoke block at lines 185-335).
    Friend Module NativeMethods

        ' kernel32.dll
        <DllImport("kernel32.dll", SetLastError:=True, CharSet:=CharSet.Unicode, EntryPoint:="CreateFileW")>
        Public Function CreateFile(lpFileName As String,
                                   dwDesiredAccess As UInteger,
                                   dwShareMode As UInteger,
                                   lpSecurityAttributes As IntPtr,
                                   dwCreationDisposition As UInteger,
                                   dwFlagsAndAttributes As UInteger,
                                   hTemplateFile As IntPtr) As IntPtr
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Public Function CloseHandle(hObject As IntPtr) As Boolean
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Public Function DeviceIoControl(hDevice As IntPtr,
                                        dwIoControlCode As UInteger,
                                        <[In](), [Out]()> lpInBuffer As Byte(),
                                        nInBufferSize As UInteger,
                                        <[In](), [Out]()> lpOutBuffer As Byte(),
                                        nOutBufferSize As UInteger,
                                        ByRef lpBytesReturned As UInteger,
                                        lpOverlapped As IntPtr) As Boolean
        End Function

        <DllImport("kernel32.dll")>
        Public Function GetUserDefaultLangID() As UShort
        End Function

        ' advapi32.dll (registry).
        <DllImport("advapi32.dll", SetLastError:=True, CharSet:=CharSet.Unicode, EntryPoint:="RegQueryValueExW")>
        Public Function RegQueryValueEx(hKey As IntPtr,
                                        lpValueName As String,
                                        lpReserved As IntPtr,
                                        ByRef lpType As UInteger,
                                        lpData As Byte(),
                                        ByRef lpcbData As UInteger) As Integer
        End Function

        <DllImport("advapi32.dll", SetLastError:=True, CharSet:=CharSet.Unicode, EntryPoint:="RegQueryValueExW")>
        Public Function RegQueryValueExSize(hKey As IntPtr,
                                            lpValueName As String,
                                            lpReserved As IntPtr,
                                            lpType As IntPtr,
                                            lpData As IntPtr,
                                            ByRef lpcbData As UInteger) As Integer
        End Function

        <DllImport("advapi32.dll", SetLastError:=True)>
        Public Function RegCloseKey(hKey As IntPtr) As Integer
        End Function

        ' cfgmgr32.dll (Configuration Manager).
        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Get_Device_Interface_List_SizeW(ByRef pulLen As UInteger,
                                                           ByRef interfaceClassGuid As Guid,
                                                           pDeviceID As String,
                                                           ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Get_Device_Interface_ListW(ByRef interfaceClassGuid As Guid,
                                                      pDeviceID As String,
                                                      buffer As Char(),
                                                      bufferLen As UInteger,
                                                      ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Locate_DevNodeW(ByRef pdnDevInst As UInteger,
                                           pDeviceID As String,
                                           ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Get_DevNode_PropertyW(dnDevInst As UInteger,
                                                 ByRef propertyKey As NativeDevpropkey,
                                                 ByRef propertyType As UInteger,
                                                 propertyBuffer As Byte(),
                                                 ByRef propertyBufferSize As UInteger,
                                                 ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode, EntryPoint:="CM_Get_DevNode_PropertyW")>
        Public Function CM_Get_DevNode_PropertySize(dnDevInst As UInteger,
                                                    ByRef propertyKey As NativeDevpropkey,
                                                    ByRef propertyType As UInteger,
                                                    propertyBuffer As IntPtr,
                                                    ByRef propertyBufferSize As UInteger,
                                                    ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Get_Device_Interface_PropertyW(pszDeviceInterface As String,
                                                          ByRef propertyKey As NativeDevpropkey,
                                                          ByRef propertyType As UInteger,
                                                          propertyBuffer As Byte(),
                                                          ByRef propertyBufferSize As UInteger,
                                                          ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode, EntryPoint:="CM_Get_Device_Interface_PropertyW")>
        Public Function CM_Get_Device_Interface_PropertySize(pszDeviceInterface As String,
                                                             ByRef propertyKey As NativeDevpropkey,
                                                             ByRef propertyType As UInteger,
                                                             propertyBuffer As IntPtr,
                                                             ByRef propertyBufferSize As UInteger,
                                                             ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True)>
        Public Function CM_Get_Parent(ByRef pdnDevInst As UInteger,
                                      dnDevInst As UInteger,
                                      ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True)>
        Public Function CM_Get_DevNode_Status(ByRef pulStatus As UInteger,
                                              ByRef pulProblemNumber As UInteger,
                                              dnDevInst As UInteger,
                                              ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Open_DevNode_Key(dnDevNode As UInteger,
                                            samDesired As UInteger,
                                            ulHardwareProfile As UInteger,
                                            disposition As UInteger,
                                            ByRef phkDevice As IntPtr,
                                            ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Get_Device_ID_List_SizeW(ByRef pulLen As UInteger,
                                                    pszFilter As String,
                                                    ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Public Function CM_Get_Device_ID_ListW(pszFilter As String,
                                               buffer As Char(),
                                               bufferLen As UInteger,
                                               ulFlags As UInteger) As UInteger
        End Function

        <DllImport("cfgmgr32.dll")>
        Public Function CM_MapCrToWin32Err(cmReturnCode As UInteger,
                                           defaultError As UInteger) As UInteger
        End Function

    End Module

    ' Python map: src/greaseweazle/tools/list_ports_windows.py::parse_device_property
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::find_from_iterable
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_STRING_DESCRIPTOR
    ' Python map: src/greaseweazle/tools/list_ports_windows.py::USB_COMMON_DESCRIPTOR
    ' (multi-string split helper used by both Get_Device_Interface_List and
    ' Get_Device_ID_List; LINQ FirstOrDefault subsumes find_from_iterable;
    ' USB_STRING_DESCRIPTOR / USB_COMMON_DESCRIPTOR are read inline as
    ' bytes since we never need to round-trip them through Marshal — see
    ' the byte-walking loops in UsbHubDeviceIOControl).
    Friend Module NativeBuffers

        ' Decodes a property buffer returned by CM_Get_*_PropertyW, exactly
        ' matching python parse_device_property() for the four property
        ' types the discovery code consumes (STRING, UINT32, STRING_LIST,
        ' BINARY). Anything else returns Nothing — pyserial raises
        ' NotImplementedError for that case but the .NET callers all check
        ' For Nothing so a soft-fail keeps us robust against future driver
        ' changes.
        Public Function ParseDeviceProperty(buffer As Byte(),
                                            byteSize As UInteger,
                                            propertyType As UInteger) As Object
            If buffer Is Nothing OrElse byteSize = 0UI Then
                Return Nothing
            End If

            Select Case propertyType
                Case NativeConstants.DEVPROP_TYPE_STRING
                    ' UTF-16LE null-terminated string. byteSize is in bytes;
                    ' divide by 2 for chars and strip the final null.
                    Dim charCount As Integer = CInt(byteSize \ 2UI)
                    If charCount <= 0 Then
                        Return String.Empty
                    End If
                    Dim raw As String = System.Text.Encoding.Unicode.GetString(buffer, 0, charCount * 2)
                    Return raw.TrimEnd(ChrW(0))

                Case NativeConstants.DEVPROP_TYPE_UINT32
                    If byteSize < 4UI Then
                        Return Nothing
                    End If
                    Return BitConverter.ToUInt32(buffer, 0)

                Case NativeConstants.DEVPROP_TYPE_STRING_LIST
                    ' Multi-string (sequence of UTF-16LE null-terminated
                    ' strings, terminated by an extra null). byteSize is
                    ' in bytes.
                    Dim charCount As Integer = CInt(byteSize \ 2UI)
                    If charCount <= 0 Then
                        Return New List(Of String)()
                    End If
                    Dim raw As String = System.Text.Encoding.Unicode.GetString(buffer, 0, charCount * 2)
                    Return SplitMultiString(raw)

                Case NativeConstants.DEVPROP_TYPE_BINARY
                    Dim bytes(CInt(byteSize) - 1) As Byte
                    System.Buffer.BlockCopy(buffer, 0, bytes, 0, CInt(byteSize))
                    Return bytes

                Case Else
                    Return Nothing
            End Select
        End Function

        ' Reads a CM_Get_Device_Interface_ListW / CM_Get_Device_ID_ListW
        ' result (multi-string) into a List(Of String).
        Public Function ReadMultiSz(buffer As Char(), charCount As Integer) As List(Of String)
            If buffer Is Nothing OrElse charCount <= 0 Then
                Return New List(Of String)()
            End If
            Dim raw As String = New String(buffer, 0, charCount)
            Return SplitMultiString(raw)
        End Function

        Private Function SplitMultiString(raw As String) As List(Of String)
            Dim result As New List(Of String)()
            If String.IsNullOrEmpty(raw) Then
                Return result
            End If
            ' The caller may have included the trailing null terminator;
            ' drop trailing nulls, then split on '\0'.
            Dim trimmed As String = raw.TrimEnd(ChrW(0))
            If String.IsNullOrEmpty(trimmed) Then
                Return result
            End If
            For Each entry In trimmed.Split(ChrW(0))
                If Not String.IsNullOrEmpty(entry) Then
                    result.Add(entry)
                End If
            Next
            Return result
        End Function

        ' Helper for IOCTL request buffers. Writes a structure into a byte
        ' buffer at the given offset using Marshal.StructureToPtr against a
        ' pinned managed array.
        Public Sub WriteStruct(Of T As Structure)(buffer As Byte(), offset As Integer, value As T)
            Dim handle = GCHandle.Alloc(buffer, GCHandleType.Pinned)
            Try
                Dim ptr As IntPtr = IntPtr.Add(handle.AddrOfPinnedObject(), offset)
                Marshal.StructureToPtr(value, ptr, False)
            Finally
                handle.Free()
            End Try
        End Sub

        ' Helper for IOCTL response buffers. Reads a structure out of a
        ' byte buffer at the given offset.
        Public Function ReadStruct(Of T As Structure)(buffer As Byte(), offset As Integer) As T
            Dim handle = GCHandle.Alloc(buffer, GCHandleType.Pinned)
            Try
                Dim ptr As IntPtr = IntPtr.Add(handle.AddrOfPinnedObject(), offset)
                Return DirectCast(Marshal.PtrToStructure(ptr, GetType(T)), T)
            Finally
                handle.Free()
            End Try
        End Function

    End Module

End Namespace
