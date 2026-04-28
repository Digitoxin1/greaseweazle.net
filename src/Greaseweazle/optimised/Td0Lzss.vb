Namespace Greaseweazle.Optimised

    ' Python map: src/greaseweazle/optimised/td0_lzss.c::td0_unpack
    Public NotInheritable Class Td0Lzss

        Private Sub New()
        End Sub

        Public Shared Function Unpack(packedData As Byte()) As Byte()
            Dim decoder As New Td0LzssDecoder()
            Return decoder.Unpack(packedData)
        End Function

        Private NotInheritable Class Td0LzssDecoder
            Private Const Sbsize As Integer = 4096
            Private Const Lasize As Integer = 60
            Private Const Threshold As Integer = 2
            Private Const NChar As Integer = (256 - Threshold + Lasize)
            Private Const TSize As Integer = NChar * 2 - 1
            Private Const Root As Integer = TSize - 1
            Private Const MaxFreq As Integer = &H8000

            Private Shared ReadOnly DCodeLzss As Byte() = {
                &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0,
                &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0, &H0,
                &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1, &H1,
                &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2, &H2,
                &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3, &H3,
                &H4, &H4, &H4, &H4, &H4, &H4, &H4, &H4, &H5, &H5, &H5, &H5, &H5, &H5, &H5, &H5,
                &H6, &H6, &H6, &H6, &H6, &H6, &H6, &H6, &H7, &H7, &H7, &H7, &H7, &H7, &H7, &H7,
                &H8, &H8, &H8, &H8, &H8, &H8, &H8, &H8, &H9, &H9, &H9, &H9, &H9, &H9, &H9, &H9,
                &HA, &HA, &HA, &HA, &HA, &HA, &HA, &HA, &HB, &HB, &HB, &HB, &HB, &HB, &HB, &HB,
                &HC, &HC, &HC, &HC, &HD, &HD, &HD, &HD, &HE, &HE, &HE, &HE, &HF, &HF, &HF, &HF,
                &H10, &H10, &H10, &H10, &H11, &H11, &H11, &H11, &H12, &H12, &H12, &H12, &H13, &H13, &H13, &H13,
                &H14, &H14, &H14, &H14, &H15, &H15, &H15, &H15, &H16, &H16, &H16, &H16, &H17, &H17, &H17, &H17,
                &H18, &H18, &H19, &H19, &H1A, &H1A, &H1B, &H1B, &H1C, &H1C, &H1D, &H1D, &H1E, &H1E, &H1F, &H1F,
                &H20, &H20, &H21, &H21, &H22, &H22, &H23, &H23, &H24, &H24, &H25, &H25, &H26, &H26, &H27, &H27,
                &H28, &H28, &H29, &H29, &H2A, &H2A, &H2B, &H2B, &H2C, &H2C, &H2D, &H2D, &H2E, &H2E, &H2F, &H2F,
                &H30, &H31, &H32, &H33, &H34, &H35, &H36, &H37, &H38, &H39, &H3A, &H3B, &H3C, &H3D, &H3E, &H3F
            }

            Private Shared ReadOnly DLenLzss As Byte() = {2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7}

            Private ReadOnly _parent(TSize + NChar - 1) As Integer
            Private ReadOnly _son(TSize - 1) As Integer
            Private ReadOnly _freq(TSize) As Integer
            Private ReadOnly _ringBuffer(Sbsize + Lasize - 2) As Byte

            Private _bits As Integer
            Private _bitBuffer As Integer
            Private _gbState As Integer
            Private _gbr As Integer
            Private _gbi As Integer
            Private _gbj As Integer
            Private _gbk As Integer
            Private _eof As Boolean
            Private _bufferOffset As Integer
            Private _bufferSize As Integer
            Private _buffer As Byte()

            Public Function Unpack(packedData As Byte()) As Byte()
                If packedData Is Nothing OrElse packedData.Length = 0 Then
                    Return Array.Empty(Of Byte)()
                End If

                InitDecompress()
                _buffer = packedData
                _bufferSize = packedData.Length
                _bufferOffset = 0

                Dim output As New List(Of Byte)(512)
                While Not _eof
                    Do
                        Dim v = GetByteDecoded()
                        If v < 0 Then
                            Exit Do
                        End If
                        output.Add(CByte(v))
                    Loop While ((output.Count And 511) <> 0) AndAlso Not _eof
                End While

                Return output.ToArray()
            End Function

            Private Sub InitDecompress()
                Array.Clear(_parent, 0, _parent.Length)
                Array.Clear(_son, 0, _son.Length)
                Array.Clear(_freq, 0, _freq.Length)
                _bits = 0
                _bitBuffer = 0
                _gbr = 0
                _gbi = 0
                _gbj = 0
                _gbk = 0
                _gbState = 0
                _eof = False
                Array.Clear(_ringBuffer, 0, _ringBuffer.Length)

                Dim i = 0
                Dim j = 0
                While i < NChar
                    _freq(i) = 1
                    _son(i) = i + TSize
                    _parent(i + TSize) = i
                    i += 1
                End While

                While i <= Root
                    _freq(i) = _freq(j) + _freq(j + 1)
                    _son(i) = j
                    _parent(j) = i
                    _parent(j + 1) = i
                    i += 1
                    j += 2
                End While

                For k = 0 To _ringBuffer.Length - 1
                    _ringBuffer(k) = CByte(AscW(" "c))
                Next

                _freq(TSize) = &HFFFF
                _parent(Root) = 0
                _bits = 0
                _bitBuffer = 0
                _gbr = Sbsize - Lasize
            End Sub

            Private Function GetChar() As Integer
                If _bufferSize = 0 Then
                    _eof = True
                    Return 0
                End If

                Dim c = CInt(_buffer(_bufferOffset)) And &HFF
                _bufferOffset += 1
                If _bufferOffset >= _bufferSize Then
                    _bufferOffset = _bufferSize - 1
                    c = 0
                    _eof = True
                End If
                Return c
            End Function

            Private Function GetBit() As Integer
                If _bits = 0 Then
                    _bitBuffer = _bitBuffer Or (GetChar() << 8)
                    _bits = 7
                Else
                    _bits -= 1
                End If

                Dim t = (_bitBuffer >> 15) And 1
                _bitBuffer = (_bitBuffer << 1) And &HFFFF
                Return t
            End Function

            Private Function GetByteUnaligned() As Integer
                If _bits < 8 Then
                    _bitBuffer = _bitBuffer Or (GetChar() << (8 - _bits))
                Else
                    _bits -= 8
                End If

                Dim t = (_bitBuffer >> 8) And &HFF
                _bitBuffer = (_bitBuffer << 8) And &HFFFF
                Return t
            End Function

            Private Function DecodeChar() As Integer
                Dim c = Root
                Do
                    c = _son(c)
                    If c >= TSize Then
                        Exit Do
                    End If
                    c += GetBit()
                Loop

                c -= TSize
                UpdateTree(c)
                Return c
            End Function

            Private Function DecodePosition() As Integer
                Dim i = GetByteUnaligned()
                Dim c = (CInt(DCodeLzss(i)) << 6)
                Dim j = CInt(DLenLzss(i >> 4))

                While True
                    j -= 1
                    If j <= 0 Then
                        Exit While
                    End If
                    i = ((i << 1) Or GetBit()) And &HFF
                End While

                Return (i And &H3F) Or c
            End Function

            Private Sub UpdateTree(code As Integer)
                If _freq(Root) = MaxFreq Then
                    Dim i = 0
                    Dim j = 0
                    While i < TSize
                        If _son(i) >= TSize Then
                            _freq(j) = (_freq(i) + 1) \ 2
                            _son(j) = _son(i)
                            j += 1
                        End If
                        i += 1
                    End While

                    i = 0
                    Dim node = NChar
                    While node < TSize
                        Dim k = i + 1
                        Dim f = _freq(i) + _freq(k)
                        _freq(node) = f
                        k = node - 1
                        While f < _freq(k)
                            k -= 1
                        End While
                        k += 1

                        Dim l = node - k
                        If l > 0 Then
                            Array.Copy(_freq, k, _freq, k + 1, l)
                            Array.Copy(_son, k, _son, k + 1, l)
                        End If

                        _freq(k) = f
                        _son(k) = i
                        i += 2
                        node += 1
                    End While

                    For p = 0 To TSize - 1
                        Dim k = _son(p)
                        If k >= TSize Then
                            _parent(k) = p
                        Else
                            _parent(k) = p
                            _parent(k + 1) = p
                        End If
                    Next
                End If

                Dim c = _parent(code + TSize)
                Do
                    Dim k = _freq(c) + 1
                    _freq(c) = k
                    Dim l = c + 1
                    If k > _freq(l) Then
                        Do
                            l += 1
                        Loop While k > _freq(l)
                        l -= 1

                        _freq(c) = _freq(l)
                        _freq(l) = k

                        Dim i = _son(c)
                        _parent(i) = l
                        If i < TSize Then
                            _parent(i + 1) = l
                        End If

                        Dim j = _son(l)
                        _parent(j) = c
                        _son(l) = i
                        If j < TSize Then
                            _parent(j + 1) = c
                        End If
                        _son(c) = j
                        c = l
                    End If
                    c = _parent(c)
                Loop While c <> 0
            End Sub

            Private Function GetByteDecoded() As Integer
                While True
                    If _eof Then
                        Return -1
                    End If

                    If _gbState = 0 Then
                        Dim c = DecodeChar()
                        If c < 256 Then
                            _ringBuffer(_gbr) = CByte(c)
                            _gbr = (_gbr + 1) And (Sbsize - 1)
                            Return c
                        End If

                        _gbState = 255
                        _gbi = (_gbr - DecodePosition() - 1) And (Sbsize - 1)
                        _gbj = c - 255 + Threshold
                        _gbk = 0
                    End If

                    If _gbk < _gbj Then
                        _ringBuffer(_gbr) = _ringBuffer((_gbk + _gbi) And (Sbsize - 1))
                        _gbk += 1
                        Dim c = CInt(_ringBuffer(_gbr)) And &HFF
                        _gbr = (_gbr + 1) And (Sbsize - 1)
                        Return c
                    End If

                    _gbState = 0
                End While

                Return -1
            End Function
        End Class
    End Class
End Namespace
