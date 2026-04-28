Namespace Greaseweazle.Optimised

    ' Python map: src/greaseweazle/optimised/mac.c::(no direct 1:1 symbol; VB helper class declaration)
    Public NotInheritable Class Mac

        Private Const SectorLength As Integer = 524
        Private Const EncodedSectorLength As Integer = 703
        Private Const LookupLen As Integer = SectorLength \ 3

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/optimised/mac.c::decode_mac_sector
        Public Shared Function DecodeSector(input As Byte()) As Tuple(Of Byte(), Integer)
            ' Mirror Python optimised.c py_decode_mac_sector: in.len < MAC_ENCODED_SECTOR_LENGTH -> fail.
            If input Is Nothing OrElse input.Length < EncodedSectorLength Then
                Return Tuple.Create(Array.Empty(Of Byte)(), 1)
            End If
            Dim b1(LookupLen) As Byte
            Dim b2(LookupLen) As Byte
            Dim b3(LookupLen) As Byte
            Dim idx = 0

            For i = 0 To LookupLen
                Dim w4 = input(idx) : idx += 1
                Dim w1 = input(idx) : idx += 1
                Dim w2 = input(idx) : idx += 1
                Dim w3 As Byte = 0
                If i <> LookupLen Then
                    w3 = input(idx) : idx += 1
                End If

                b1(i) = CByte((w1 And &H3F) Or ((w4 << 2) And &HC0))
                b2(i) = CByte((w2 And &H3F) Or ((w4 << 4) And &HC0))
                b3(i) = CByte((w3 And &H3F) Or ((w4 << 6) And &HC0))
            Next

            Dim output(SectorLength - 1) As Byte
            Dim c1 As UInteger = 0UI, c2 As UInteger = 0UI, c3 As UInteger = 0UI
            Dim count = 0
            Dim outPos = 0

            Do
                c1 = (c1 And &HFFUI) << 1
                If (c1 And &H100UI) <> 0UI Then c1 += 1UI

                Dim v As Byte = CByte(b1(count) Xor (c1 And &HFFUI))
                c3 += v
                If (c1 And &H100UI) <> 0UI Then
                    c3 += 1UI
                    c1 = c1 And &HFFUI
                End If
                output(outPos) = v : outPos += 1

                v = CByte(b2(count) Xor (c3 And &HFFUI))
                c2 += v
                If c3 > &HFFUI Then
                    c2 += 1UI
                    c3 = c3 And &HFFUI
                End If
                output(outPos) = v : outPos += 1

                If outPos = SectorLength Then Exit Do

                v = CByte(b3(count) Xor (c2 And &HFFUI))
                c1 += v
                If c2 > &HFFUI Then
                    c1 += 1UI
                    c2 = c2 And &HFFUI
                End If
                output(outPos) = v : outPos += 1
                count += 1
            Loop

            Dim c4 As Byte = CByte(((c1 And &HC0UI) >> 6) Or ((c2 And &HC0UI) >> 4) Or ((c3 And &HC0UI) >> 2))
            c1 = c1 And &H3FUI
            c2 = c2 And &H3FUI
            c3 = c3 And &H3FUI
            c4 = CByte(c4 And &H3F)

            Dim status = If(input(idx) = c4 AndAlso
                            input(idx + 1) = CByte(c3) AndAlso
                            input(idx + 2) = CByte(c2) AndAlso
                            input(idx + 3) = CByte(c1), 0, 1)
            Return Tuple.Create(output, status)
        End Function

        ' Python map: src/greaseweazle/optimised/mac.c::encode_mac_sector
        Public Shared Function EncodeSector(input As Byte()) As Byte()
            ' Mirror Python optimised.c py_encode_mac_sector: in.len < MAC_SECTOR_LENGTH -> fail.
            If input Is Nothing OrElse input.Length < SectorLength Then
                Return Array.Empty(Of Byte)()
            End If
            Dim b1(LookupLen) As Byte
            Dim b2(LookupLen) As Byte
            Dim b3(LookupLen) As Byte
            Dim c1 As UInteger = 0UI, c2 As UInteger = 0UI, c3 As UInteger = 0UI
            Dim pos = 0
            Dim j = 0

            While True
                c1 = (c1 And &HFFUI) << 1
                If (c1 And &H100UI) <> 0UI Then c1 += 1UI

                Dim v = input(pos) : pos += 1
                c3 += v
                If (c1 And &H100UI) <> 0UI Then
                    c3 += 1UI
                    c1 = c1 And &HFFUI
                End If
                b1(j) = CByte((v Xor c1) And &HFFUI)

                v = input(pos) : pos += 1
                c2 += v
                If c3 > &HFFUI Then
                    c2 += 1UI
                    c3 = c3 And &HFFUI
                End If
                b2(j) = CByte((v Xor c3) And &HFFUI)

                If pos = SectorLength Then Exit While

                v = input(pos) : pos += 1
                c1 += v
                If c2 > &HFFUI Then
                    c1 += 1UI
                    c2 = c2 And &HFFUI
                End If
                b3(j) = CByte((v Xor c2) And &HFFUI)
                j += 1
            End While

            Dim c4 As UInteger = ((c1 And &HC0UI) >> 6) Or ((c2 And &HC0UI) >> 4) Or ((c3 And &HC0UI) >> 2)
            b3(LookupLen) = 0

            Dim output As New List(Of Byte)(EncodedSectorLength)
            For i = 0 To LookupLen
                Dim w1 = b1(i) And &H3F
                Dim w2 = b2(i) And &H3F
                Dim w3 = b3(i) And &H3F
                Dim w4 = ((b1(i) And &HC0) >> 2) Or ((b2(i) And &HC0) >> 4) Or ((b3(i) And &HC0) >> 6)
                output.Add(CByte(w4))
                output.Add(CByte(w1))
                output.Add(CByte(w2))
                If i <> LookupLen Then output.Add(CByte(w3))
            Next
            output.Add(CByte(c4 And &H3FUI))
            output.Add(CByte(c3 And &H3FUI))
            output.Add(CByte(c2 And &H3FUI))
            output.Add(CByte(c1 And &H3FUI))
            Return output.ToArray()
        End Function
    End Class
End Namespace
