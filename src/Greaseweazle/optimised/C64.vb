Namespace Greaseweazle.Optimised

    ' Python map: src/greaseweazle/optimised/c64.c::(no direct 1:1 symbol; VB helper class declaration)
    Public NotInheritable Class C64

        Private Shared ReadOnly EncodeNibble As Byte() = {&HA, &HB, &H12, &H13, &HE, &HF, &H16, &H17, &H9, &H19, &H1A, &H1B, &HD, &H1D, &H1E, &H15}
        Private Shared ReadOnly DecodeNibble As Integer() = BuildDecodeNibble()

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/optimised/c64.c::decode_c64_gcr
        Public Shared Function DecodeGcr(input As Byte(), outLen As Integer) As Byte()
            If input Is Nothing OrElse outLen <= 0 Then
                Return Array.Empty(Of Byte)()
            End If
            ' Mirror Python optimised.c py_decode_c64_gcr: in.len % 5 must be 0; out_len = (in.len/5)*4.
            If (input.Length Mod 5) <> 0 Then
                Return Array.Empty(Of Byte)()
            End If
            If (input.Length \ 5) * 4 <> outLen Then
                Return Array.Empty(Of Byte)()
            End If

            Dim output(outLen - 1) As Byte
            Dim inPos = 0
            Dim acc As UInteger = &H10000UI
            For k = 0 To outLen - 1
                Dim enc As UShort = 0US
                For i = 0 To 9
                    If (acc And &H10000UI) <> 0UI Then
                        If inPos >= input.Length Then
                            Return Array.Empty(Of Byte)()
                        End If
                        acc = CUInt(input(inPos)) Or &H100UI
                        inPos += 1
                    End If
                    acc <<= 1
                    enc = CUShort((enc << 1) Or ((acc >> 8) And 1UI))
                Next

                Dim hi = DecodeNibble((enc >> 5) And &H1F)
                Dim lo = DecodeNibble(enc And &H1F)
                output(k) = CByte(((hi << 4) Or lo) And &HFF)
            Next
            Return output
        End Function

        ' Python map: src/greaseweazle/optimised/c64.c::encode_c64_gcr
        Public Shared Function EncodeGcr(input As Byte()) As Byte()
            If input Is Nothing OrElse input.Length = 0 Then
                Return Array.Empty(Of Byte)()
            End If
            ' Mirror Python optimised.c py_encode_c64_gcr: in.len % 4 must be 0.
            If (input.Length Mod 4) <> 0 Then
                Return Array.Empty(Of Byte)()
            End If

            Dim output As New List(Of Byte)()
            Dim acc As Integer = 1
            For Each b In input
                Dim enc = (CInt(EncodeNibble((b >> 4) And &HF)) << 5) Or EncodeNibble(b And &HF)
                For i = 0 To 9
                    acc = (acc << 1) Or ((enc >> (9 - i)) And 1)
                    If (acc And &H100) <> 0 Then
                        output.Add(CByte(acc And &HFF))
                        acc = 1
                    End If
                Next
            Next
            Return output.ToArray()
        End Function

        Private Shared Function BuildDecodeNibble() As Integer()
            Dim map = Enumerable.Repeat(-1, 32).ToArray()
            For i = 0 To EncodeNibble.Length - 1
                map(EncodeNibble(i)) = i
            Next
            Return map
        End Function
    End Class
End Namespace
