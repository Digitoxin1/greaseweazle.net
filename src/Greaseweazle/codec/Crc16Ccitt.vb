Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' CRC-16/CCITT-FALSE: poly = 0x1021, init = 0xFFFF, refin = false,
    ' refout = false, xorout = 0x0000.
    '
    ' Python map: src/greaseweazle/codec/ibm/ibm.py uses crcmod.predefined
    ' 'crc-ccitt-false' on every IBM/EDSK/HP MMFM IDAM/DAM/sector buffer.
    '
    ' This replaces four near-identical bit-by-bit implementations that lived
    ' in IBMFixedCodec, IBMScanCodec, EDSKImage, and HpMmfmCodec. The bit-loop
    ' versions cost 8 inner iterations per byte (compare + branch + shift +
    ' xor + mask). With a 256-entry UShort table the inner loop collapses to
    ' a single index + xor, ~4-8x faster on the per-sector hot path.
    Public NotInheritable Class Crc16Ccitt

        Private Sub New()
        End Sub

        Private Const Polynomial As UShort = &H1021US

        Private Shared ReadOnly _table As UShort() = BuildTable()

        Private Shared Function BuildTable() As UShort()
            Dim tbl(255) As UShort
            For i = 0 To 255
                Dim crc As UInteger = CUInt(i) << 8
                For bit = 0 To 7
                    If (crc And &H8000UI) <> 0UI Then
                        crc = ((crc << 1) Xor Polynomial) And &HFFFFUI
                    Else
                        crc = (crc << 1) And &HFFFFUI
                    End If
                Next
                tbl(i) = CUShort(crc)
            Next
            Return tbl
        End Function

        Public Shared Function Compute(data As Byte()) As UShort
            Return Compute(data, 0, data.Length, &HFFFFUS)
        End Function

        Public Shared Function Compute(data As Byte(), seed As UShort) As UShort
            Return Compute(data, 0, data.Length, seed)
        End Function

        Public Shared Function Compute(data As Byte(), offset As Integer, length As Integer, seed As UShort) As UShort
            Dim crc As UInteger = seed
            Dim [end] = offset + length
            For i = offset To [end] - 1
                Dim idx As Integer = CInt((crc >> 8) Xor data(i)) And &HFF
                crc = ((crc << 8) Xor _table(idx)) And &HFFFFUI
            Next
            Return CUShort(crc)
        End Function

    End Class

End Namespace
