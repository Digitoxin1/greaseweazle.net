Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Shared "spread each input bit into 2 output bits with the high bit
    ' acting as a clock-zero" transform used by HpMmfm and DataGeneral.
    '
    ' Encoding pattern:
    '   input bit b at position i (MSB-first within the input byte)
    '       -> output bits [0, b] at positions (2*i, 2*i+1) of the output
    '
    ' One input byte expands to two output bytes (16 output bits per 8 input
    ' bits). Decoding inverts the transform by extracting the data bits
    ' (every other bit, starting at the LSB of each pair) and packing them
    ' back to a single byte.
    '
    ' Was duplicated as private helpers `EncodeDoubled`/`DecodeDoubled` in
    ' both HpMmfmCodec and DataGeneralCodec; the implementations were
    ' byte-for-byte identical.
    Public NotInheritable Class DoubleBitCodec

        Private Sub New()
        End Sub

        Private Shared ReadOnly _encodeTable As UShort() = BuildEncodeTable()

        ' Shared 256-entry table: byte -> 16-bit "spread" value.
        ' Replaces the per-file BuildEncodeList tables in EDSK and DMK.
        Public Shared ReadOnly Property EncodeTable As UShort()
            Get
                Return _encodeTable
            End Get
        End Property

        Private Shared Function BuildEncodeTable() As UShort()
            Dim table(255) As UShort
            For x = 0 To 255
                Dim y As Integer = 0
                For i = 7 To 0 Step -1
                    y <<= 2
                    y = y Or ((x >> i) And 1)
                Next
                table(x) = CUShort(y And &HFFFF)
            Next
            Return table
        End Function

        Public Shared Function Encode(data As Byte()) As Byte()
            Dim out(data.Length * 2 - 1) As Byte
            Dim p As Integer = 0
            For Each x In data
                Dim y As Integer = _encodeTable(x)
                out(p) = CByte((y >> 8) And &HFF) : p += 1
                out(p) = CByte(y And &HFF) : p += 1
            Next
            Return out
        End Function

        Public Shared Function Decode(data As Byte()) As Byte()
            Dim pairs = data.Length \ 2
            Dim out(pairs - 1) As Byte
            For i = 0 To pairs - 1
                Dim word = (CInt(data(i * 2)) << 8) Or data(i * 2 + 1)
                Dim index = word And &H5555
                Dim y = (index + (index >> 1)) And &H3333
                y = (y + (y >> 2)) And &H0F0F
                y = (y + (y >> 4)) And &H00FF
                out(i) = CByte(y)
            Next
            Return out
        End Function

    End Class

End Namespace
