Imports Greaseweazle.Core
Imports Greaseweazle.Optimised

Namespace Greaseweazle.Infrastructure

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration UsbProtocol)
    Public NotInheritable Class UsbProtocol

        ' Python map: src/greaseweazle/usb.py::(no direct 1:1 symbol; VB static protocol holder constructor)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/usb.py::EARLIEST_SUPPORTED_FIRMWARE
        Public Const EarliestSupportedFirmwareMajor As Integer = 0
        ' Python map: src/greaseweazle/usb.py::EARLIEST_SUPPORTED_FIRMWARE
        Public Const EarliestSupportedFirmwareMinor As Integer = 31

        ' Python map: src/greaseweazle/usb.py::ControlCmd
        Public Enum ControlCmd
            ClearComms = 10000
            Normal = 9600
        End Enum

        ' Python map: src/greaseweazle/usb.py::Cmd
        Public Enum Cmd As Byte
            GetInfo = 0
            Update = 1
            Seek = 2
            Head = 3
            SetParams = 4
            GetParams = 5
            Motor = 6
            ReadFlux = 7
            WriteFlux = 8
            GetFluxStatus = 9
            GetIndexTimes = 10
            SwitchFwMode = 11
            [Select] = 12
            Deselect = 13
            SetBusType = 14
            SetPin = 15
            Reset = 16
            EraseFlux = 17
            SourceBytes = 18
            SinkBytes = 19
            GetPin = 20
            TestMode = 21
            NoClickStep = 22
        End Enum

        ' Python map: src/greaseweazle/usb.py::GetInfo
        Public Enum GetInfo As Byte
            Firmware = 0
            BandwidthStats = 1
            CurrentDrive = 7
        End Enum

        ' Python map: src/greaseweazle/usb.py::Params
        Public Enum Params As Byte
            Delays = 0
        End Enum

        ' Python map: src/greaseweazle/usb.py::Ack
        Public Enum Ack As Byte
            Okay = 0
            BadCommand = 1
            NoIndex = 2
            NoTrk0 = 3
            FluxOverflow = 4
            FluxUnderflow = 5
            Wrprot = 6
            NoUnit = 7
            NoBus = 8
            BadUnit = 9
            BadPin = 10
            BadCylinder = 11
            OutOfSRAM = 12
            OutOfFlash = 13
        End Enum

        ' Python map: src/greaseweazle/usb.py::FluxOp
        Public Enum FluxOp As Byte
            Index = 1
            Space = 2
            Astable = 3
        End Enum

        ' Python map: src/greaseweazle/usb.py::BusType
        Public Enum BusType As Byte
            Invalid = 0
            IBMPC = 1
            Shugart = 2
        End Enum

        ' Python map: src/greaseweazle/usb.py::_decode_flux
        Public Shared Function DecodeFlux(data As Byte()) As Tuple(Of List(Of Double), List(Of Double))
            Return Greaseweazle.Optimised.OptimizedFlux.DecodeFlux(data)
        End Function

        ' Python map: src/greaseweazle/usb.py::_encode_flux
        Public Shared Function EncodeFlux(fluxValues As IEnumerable(Of Integer), sampleFrequency As Double) As Byte()
            Dim nfaThreshold = CInt(Math.Round(150.0E-6 * sampleFrequency))
            Dim nfaPeriod = CInt(Math.Round(1.25E-6 * sampleFrequency))
            Dim data As New List(Of Byte)()

            Dim write28 As Action(Of Integer) =
                Sub(value As Integer)
                    data.Add(CByte(1 Or ((value << 1) And &HFF)))
                    data.Add(CByte(1 Or ((value >> 6) And &HFF)))
                    data.Add(CByte(1 Or ((value >> 13) And &HFF)))
                    data.Add(CByte(1 Or ((value >> 20) And &HFF)))
                End Sub

            Dim dummyFlux = CInt(Math.Round(100.0E-6 * sampleFrequency))
            For Each value In fluxValues.Concat({dummyFlux})
                If value = 0 Then
                    Continue For
                End If

                If value < 250 Then
                    data.Add(CByte(value))
                ElseIf value > nfaThreshold Then
                    data.Add(&HFF)
                    data.Add(CByte(FluxOp.Space))
                    write28.Invoke(value)
                    data.Add(&HFF)
                    data.Add(CByte(FluxOp.Astable))
                    write28.Invoke(nfaPeriod)
                Else
                    Dim high = (value - 250) \ 255
                    If high < 5 Then
                        data.Add(CByte(250 + high))
                        data.Add(CByte(1 + (value - 250) Mod 255))
                    Else
                        data.Add(&HFF)
                        data.Add(CByte(FluxOp.Space))
                        write28.Invoke(value - 249)
                        data.Add(249)
                    End If
                End If
            Next

            data.Add(0)
            Return data.ToArray()
        End Function

    End Class

    ' Python map: src/greaseweazle/usb.py::CmdError
    Public Class CmdError
        Inherits Exception

        ' Python map: src/greaseweazle/usb.py::CmdError.__init__
        Public Sub New(cmd As Byte(), code As UsbProtocol.Ack)
            MyBase.New(String.Format("{0}: {1}", CmdStrInner(cmd), ErrcodeStrInner(code, cmd)))
            Me.Cmd = cmd
            Me.Code = code
        End Sub

        ' Python map: src/greaseweazle/usb.py::CmdError.cmd
        Public Property Cmd As Byte()
        ' Python map: src/greaseweazle/usb.py::CmdError.code
        Public Property Code As UsbProtocol.Ack

        ' Python map: src/greaseweazle/usb.py::CmdError.cmd_str
        Public Function CmdStr() As String
            Return CmdStrInner(Cmd)
        End Function

        ' Python map: src/greaseweazle/usb.py::(no direct 1:1 symbol; VB helper for cmd_str with explicit command input)
        Private Shared Function CmdStrInner(cmd As Byte()) As String
            If cmd Is Nothing OrElse cmd.Length = 0 Then
                Return "UnknownCmd"
            End If
            Return CmdName(cmd(0))
        End Function

        ' Python map: src/greaseweazle/usb.py::(no direct 1:1 symbol; VB helper to map command id to command name text)
        Private Shared Function CmdName(commandId As Byte) As String
            Dim value = CType(commandId, UsbProtocol.Cmd)
            Return If([Enum].GetName(GetType(UsbProtocol.Cmd), value), "UnknownCmd")
        End Function

        ' Python map: src/greaseweazle/usb.py::CmdError.errcode_str
        Public Function ErrcodeStr() As String
            Return ErrcodeStrInner(Code, Cmd)
        End Function

        ' Python map: src/greaseweazle/usb.py::(no direct 1:1 symbol; VB helper for errcode_str with explicit code/cmd input)
        Private Shared Function ErrcodeStrInner(code As UsbProtocol.Ack, cmd As Byte()) As String
            If code = UsbProtocol.Ack.BadCylinder AndAlso cmd(0) = CByte(UsbProtocol.Cmd.Seek) Then
                Dim cyl As Integer
                If cmd.Length = 3 Then
                    cyl = CInt(CSByte(cmd(2)))
                ElseIf cmd.Length >= 4 Then
                    cyl = BitConverter.ToInt16(cmd, 2)
                Else
                    cyl = 0
                End If
                Return String.Format("{0} {1}", AckStr(code), cyl)
            End If
            Return AckStr(code)
        End Function

        ' Python map: src/greaseweazle/usb.py::CmdError.__str__
        Public Overrides Function ToString() As String
            Return String.Format("{0}: {1}", CmdStr(), ErrcodeStr())
        End Function

        ' Python map: src/greaseweazle/usb.py::Ack.str
        Private Shared Function AckStr(code As UsbProtocol.Ack) As String
            Select Case code
                Case UsbProtocol.Ack.Okay : Return "Okay"
                Case UsbProtocol.Ack.BadCommand : Return "Bad Command"
                Case UsbProtocol.Ack.NoIndex : Return "No Index"
                Case UsbProtocol.Ack.NoTrk0 : Return "Track 0 not found"
                Case UsbProtocol.Ack.FluxOverflow : Return "Flux Overflow"
                Case UsbProtocol.Ack.FluxUnderflow : Return "Flux Underflow"
                Case UsbProtocol.Ack.Wrprot : Return "Disk is Write Protected"
                Case UsbProtocol.Ack.NoUnit : Return "No drive unit selected"
                Case UsbProtocol.Ack.NoBus : Return "No bus type (eg. Shugart, IBM/PC) specified"
                Case UsbProtocol.Ack.BadUnit : Return "Invalid unit number"
                Case UsbProtocol.Ack.BadPin : Return "Invalid pin"
                Case UsbProtocol.Ack.BadCylinder : Return "Invalid cylinder"
                Case UsbProtocol.Ack.OutOfSRAM : Return "Out of SRAM"
                Case UsbProtocol.Ack.OutOfFlash : Return "Out of Flash"
                Case Else : Return String.Format("Unknown Error ({0})", CInt(code))
            End Select
        End Function
    End Class

End Namespace
