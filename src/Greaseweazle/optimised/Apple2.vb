Namespace Greaseweazle.Optimised

    ' Python map: src/greaseweazle/optimised/apple2.c::(no direct 1:1 symbol; VB helper class declaration)
    Public NotInheritable Class Apple2

        Private Const SectorLength As Integer = 256
        Private Const EncodedSectorLength As Integer = 342
        Private Const TwoBitCount As Integer = &H56

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/optimised/apple2.c::decode_apple2_sector
        Public Shared Function DecodeSector(input As Byte()) As Tuple(Of Byte(), Integer)
            If input Is Nothing OrElse input.Length = 0 Then
                Return Tuple.Create(Array.Empty(Of Byte)(), 1)
            End If

            Dim scratch(EncodedSectorLength) As Byte
            Dim j = 0
            Dim result As Byte = 0

            For i = 0 To input.Length - 1
                Dim x = input(i)
                For k = 0 To 7
                    result = CByte((result << 1) Or ((x >> 7) And 1))
                    x = CByte((x << 1) And &HFF)
                    If (result And &H80) <> 0 Then
                        scratch(j) = result
                        j += 1
                        If j = EncodedSectorLength + 1 Then
                            GoTo Found
                        End If
                        result = 0
                    End If
                Next
            Next

            Return Tuple.Create(Array.Empty(Of Byte)(), 1)

Found:
            Dim output(SectorLength - 1) As Byte
            Dim checksum As Integer = 0
            For i = 0 To EncodedSectorLength - 1
                Dim dec = AppleGcr62.DecodeByte(scratch(i))
                checksum = (checksum Xor dec) And &HFF
                If i >= 86 Then
                    output(i - 86) = CByte(output(i - 86) Or ((checksum And &H3F) << 2))
                Else
                    output(i) = CByte(((checksum >> 1) And 1) Or ((checksum << 1) And 2))
                    output(i + 86) = CByte(((checksum >> 3) And 1) Or ((checksum >> 1) And 2))
                    If i + 172 < SectorLength Then
                        output(i + 172) = CByte(((checksum >> 5) And 1) Or ((checksum >> 3) And 2))
                    End If
                End If
            Next

            checksum = checksum And &H3F
            Dim trailing = AppleGcr62.DecodeByte(scratch(EncodedSectorLength))
            Return Tuple.Create(output, If(checksum <> trailing, 1, 0))
        End Function

        ' Python map: src/greaseweazle/optimised/apple2.c::encode_apple2_sector
        Public Shared Function EncodeSector(input As Byte()) As Byte()
            If input Is Nothing OrElse input.Length < SectorLength Then
                Return Array.Empty(Of Byte)()
            End If

            Dim output(EncodedSectorLength) As Byte
            Dim checksum As Integer = 0

            For i = 0 To EncodedSectorLength - 1
                Dim value As Integer
                If i >= TwoBitCount Then
                    value = (input(i - TwoBitCount) >> 2) And &H3F
                Else
                    Dim tmp = input(i)
                    value = ((tmp And 1) << 1) Or ((tmp And 2) >> 1)

                    tmp = input(i + TwoBitCount)
                    value = value Or ((tmp And 1) << 3) Or ((tmp And 2) << 1)

                    If i + 2 * TwoBitCount < SectorLength Then
                        tmp = input(i + 2 * TwoBitCount)
                        value = value Or ((tmp And 1) << 5) Or ((tmp And 2) << 3)
                    End If
                End If

                checksum = checksum Xor value
                output(i) = CByte(AppleGcr62.EncodeByte(CByte(checksum And &H3F)))
                checksum = value
            Next

            output(EncodedSectorLength) = CByte(AppleGcr62.EncodeByte(CByte(checksum And &H3F)))
            Return output
        End Function
    End Class
End Namespace
