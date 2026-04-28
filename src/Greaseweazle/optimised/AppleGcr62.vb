Imports System.Linq

Namespace Greaseweazle.Optimised

    ' Python map: src/greaseweazle/optimised/apple_gcr_6a2.c::(no direct 1:1 symbol; VB helper class declaration)
    Public NotInheritable Class AppleGcr62

        Private Shared ReadOnly EncodeMap As Byte() = {
            &H96, &H97, &H9A, &H9B, &H9D, &H9E, &H9F, &HA6,
            &HA7, &HAB, &HAC, &HAD, &HAE, &HAF, &HB2, &HB3,
            &HB4, &HB5, &HB6, &HB7, &HB9, &HBA, &HBB, &HBC,
            &HBD, &HBE, &HBF, &HCB, &HCD, &HCE, &HCF, &HD3,
            &HD6, &HD7, &HD9, &HDA, &HDB, &HDC, &HDD, &HDE,
            &HDF, &HE5, &HE6, &HE7, &HE9, &HEA, &HEB, &HEC,
            &HED, &HEE, &HEF, &HF2, &HF3, &HF4, &HF5, &HF6,
            &HF7, &HF9, &HFA, &HFB, &HFC, &HFD, &HFE, &HFF
        }

        Private Shared ReadOnly DecodeMap As Integer() = BuildDecodeMap()

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/optimised/apple_gcr_6a2.c::apple_gcr_6a2_decode_byte
        Public Shared Function DecodeByte(gcr As Byte) As Integer
            Return DecodeMap(CInt(gcr))
        End Function

        ' Python map: src/greaseweazle/optimised/apple_gcr_6a2.c::apple_gcr_6a2_encode_byte
        Public Shared Function EncodeByte(value As Byte) As Integer
            If value >= EncodeMap.Length Then
                Return -1
            End If
            Return CInt(EncodeMap(value))
        End Function

        ' Python map: src/greaseweazle/optimised/apple_gcr_6a2.c::apple_gcr_6a2_decode_bytes
        Public Shared Function DecodeBytes(input As Byte()) As Byte()
            If input Is Nothing Then
                Return Array.Empty(Of Byte)()
            End If
            If input.Length = 0 Then
                Return Array.Empty(Of Byte)()
            End If

            Dim output(input.Length - 1) As Byte
            For i = 0 To input.Length - 1
                Dim decoded = DecodeMap(input(i))
                output(i) = CByte(decoded And &HFF)
            Next
            Return output
        End Function

        ' Python map: src/greaseweazle/optimised/apple_gcr_6a2.c::apple_gcr_6a2_encode_bytes
        Public Shared Function EncodeBytes(input As Byte()) As Byte()
            If input Is Nothing Then
                Return Array.Empty(Of Byte)()
            End If
            If input.Length = 0 Then
                Return Array.Empty(Of Byte)()
            End If

            Dim output(input.Length - 1) As Byte
            For i = 0 To input.Length - 1
                output(i) = CByte(EncodeByte(input(i)) And &HFF)
            Next
            Return output
        End Function

        Private Shared Function BuildDecodeMap() As Integer()
            Dim map = Enumerable.Repeat(-1, 256).ToArray()
            For i = 0 To EncodeMap.Length - 1
                map(EncodeMap(i)) = i
            Next
            Return map
        End Function
    End Class
End Namespace
