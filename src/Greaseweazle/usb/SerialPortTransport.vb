Imports System.IO.Ports

Namespace Greaseweazle.Infrastructure

    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration SerialPortTransport)
    Public Class SerialPortTransport
        Implements SerialTransport

        Private ReadOnly _port As SerialPort

        ' Python map: src/greaseweazle/usb.py::Unit.__init__
        Public Sub New(portName As String,
                       Optional baudRate As Integer = 9600,
                       Optional readTimeoutMs As Integer = 2000)
            _port = New SerialPort(portName, baudRate, Parity.None, 8, StopBits.One) With {
                .Handshake = Handshake.None,
                .ReadTimeout = readTimeoutMs,
                .WriteTimeout = readTimeoutMs
            }
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.reset (self.ser.baudrate assignments for clear-comms/normal transport speed switching).
        Public Property BaudRate As Integer Implements SerialTransport.BaudRate
            Get
                Return _port.BaudRate
            End Get
            Set(value As Integer)
                _port.BaudRate = value
            End Set
        End Property

        ' Python map: src/greaseweazle/usb.py::Unit (self.ser.in_waiting reads to drain pending serial bytes).
        Public ReadOnly Property BytesAvailable As Integer Implements SerialTransport.BytesAvailable
            Get
                Return _port.BytesToRead
            End Get
        End Property

        ' Python map: src/greaseweazle/usb.py::Unit._send_cmd
        Public Sub Write(data As Byte()) Implements SerialTransport.Write
            _port.Write(data, 0, data.Length)
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit._send_cmd
        Public Function Read(count As Integer) As Byte() Implements SerialTransport.Read
            If count <= 0 Then
                Return Array.Empty(Of Byte)()
            End If
            Dim buffer(count - 1) As Byte
            Dim offset = 0
            While offset < count
                Dim n = _port.Read(buffer, offset, count - offset)
                If n <= 0 Then
                    Exit While
                End If
                offset += n
            End While
            If offset = count Then
                Return buffer
            End If
            Dim partialBuffer(offset - 1) As Byte
            If offset > 0 Then
                Array.Copy(buffer, partialBuffer, offset)
            End If
            Return partialBuffer
        End Function

        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Public Sub ResetInputBuffer() Implements SerialTransport.ResetInputBuffer
            _port.DiscardInBuffer()
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Public Sub ResetOutputBuffer() Implements SerialTransport.ResetOutputBuffer
            _port.DiscardOutBuffer()
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Public Sub Open() Implements SerialTransport.Open
            If Not _port.IsOpen Then
                _port.Open()
            End If
        End Sub

        ' Python map: src/greaseweazle/usb.py::Unit.reset
        Public Sub Close() Implements SerialTransport.Close
            If Not _port.IsOpen Then Return

            ' Workaround for a long-standing .NET Framework System.IO.Ports bug:
            ' SerialStream.Dispose(disposing:=True) closes the underlying
            ' SafeFileHandle but fails to call GC.SuppressFinalize on itself.
            ' At process exit the SerialStream finalizer therefore runs a
            ' second cleanup pass which calls SetCommMask on the now-closed
            ' handle, throwing ObjectDisposedException from Finalize().
            '
            ' This is especially likely to fire when Close() is invoked from a
            ' thread other than the one that opened the port (our Ctrl-C
            ' handler does exactly that), so suppress the finalizer ourselves
            ' before tearing the stream down. Errors here are non-fatal: if
            ' BaseStream has already been disposed the finalizer is harmless.
            Try
                Dim baseStream = _port.BaseStream
                If baseStream IsNot Nothing Then
                    GC.SuppressFinalize(baseStream)
                End If
            Catch
            End Try

            Try
                _port.Close()
            Catch
            End Try
        End Sub

    End Class

End Namespace
