Imports Greaseweazle.Core

Namespace Greaseweazle.Optimised

    ' Python map: src/greaseweazle/optimised/optimised.c::decode_flux
    Public NotInheritable Class OptimizedFlux

        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/optimised/optimised.c::flux_to_bitcells
        Public Shared Sub FluxToBitcells(bitArray As List(Of Boolean),
                                         timeArray As List(Of Double),
                                         revolutions As List(Of Integer),
                                         indexIter As IEnumerator(Of Double),
                                         fluxIter As IEnumerator(Of Double),
                                         freq As Double,
                                         clockCentre As Double,
                                         clockMin As Double,
                                         clockMax As Double,
                                         pllPeriodAdjust As Double,
                                         pllPhaseAdjust As Double)
            Dim nbits = 0
            Dim ticks = 0.0
            Dim clock = clockCentre
            If Not indexIter.MoveNext() Then
                Throw New InvalidOperationException("Missing index marks.")
            End If
            Dim toIndex = indexIter.Current

            While fluxIter.MoveNext()
                Dim x = fluxIter.Current
                ticks += x / freq
                If ticks < clock / 2.0 Then
                    Continue While
                End If

                Dim zeroes = 0
                While True
                    ticks -= clock
                    If ticks < clock / 2.0 Then
                        Exit While
                    End If
                    zeroes += 1
                    bitArray.Add(False)
                End While
                bitArray.Add(True)

                Dim newTicks = ticks * (1 - pllPhaseAdjust)
                Dim emittedClock = clock + (ticks - newTicks) / (zeroes + 1)
                For i = 0 To zeroes
                    toIndex -= emittedClock
                    If toIndex < 0 Then
                        revolutions.Add(nbits)
                        nbits = 0
                        If Not indexIter.MoveNext() Then
                            Throw New InvalidOperationException("Insufficient index values.")
                        End If
                        toIndex += indexIter.Current
                    End If
                    nbits += 1
                    timeArray.Add(emittedClock)
                Next

                If zeroes <= 3 Then
                    clock += ticks * pllPeriodAdjust
                Else
                    clock += (clockCentre - clock) * pllPeriodAdjust
                End If
                clock = Math.Min(Math.Max(clock, clockMin), clockMax)
                ticks = newTicks
            End While
        End Sub

        Public Shared Function DecodeFlux(data As Byte()) As Tuple(Of List(Of Double), List(Of Double))
            Dim flux As New List(Of Double)()
            Dim index As New List(Of Double)()
            If data Is Nothing OrElse data.Length = 0 OrElse data(data.Length - 1) <> 0 Then
                Throw New FatalException("Flux is not NUL-terminated")
            End If

            Dim pos As Integer = 0
            Dim l As Integer = data.Length - 1
            Dim ticks As Integer = 0
            Dim ticksSinceIndex As Integer = 0

            ' read28Bit was a Func(Of Integer) closure capturing pos+data; the
            ' delegate Invoke + closure access showed up in profiles. Inlined
            ' as a Sub call below; pos is updated via ByRef.

            While l <> 0
                Dim current = CInt(data(pos))
                pos += 1
                If current = &HFF Then
                    l -= 2
                    If l < 0 Then
                        Throw New FatalException("Unexpected end of flux")
                    End If

                    Dim opcode = CInt(data(pos))
                    pos += 1
                    Select Case opcode
                        Case 1 ' FluxOp.Index
                            l -= 4
                            If l < 0 Then
                                Throw New FatalException("Unexpected end of flux")
                            End If
                            Dim value = Read28Bit(data, pos)
                            index.Add(ticksSinceIndex + ticks + value)
                            ticksSinceIndex = -(ticks + value)
                        Case 2 ' FluxOp.Space
                            l -= 4
                            If l < 0 Then
                                Throw New FatalException("Unexpected end of flux")
                            End If
                            ticks += Read28Bit(data, pos)
                        Case Else
                            Throw New FatalException(String.Format("Bad opcode in flux stream ({0})", opcode))
                    End Select
                Else
                    Dim value As Integer
                    If current < 250 Then
                        l -= 1
                        value = current
                    Else
                        l -= 2
                        If l < 0 Then
                            Throw New FatalException("Unexpected end of flux")
                        End If
                        value = 250 + (current - 250) * 255
                        value += CInt(data(pos)) - 1
                        pos += 1
                    End If

                    ticks += value
                    flux.Add(ticks)
                    ticksSinceIndex += ticks
                    ticks = 0
                End If
            End While

            Return Tuple.Create(flux, index)
        End Function

        ' Reads 4 bytes at `pos`, packs them as a 28-bit integer (low 7 bits of
        ' each byte are payload, MSB is a stuffing flag), and advances `pos`.
        Private Shared Function Read28Bit(data As Byte(), ByRef pos As Integer) As Integer
            Dim value = ((data(pos) And &HFE) >> 1)
            value += ((data(pos + 1) And &HFE) << 6)
            value += ((data(pos + 2) And &HFE) << 13)
            value += ((data(pos + 3) And &HFE) << 20)
            pos += 4
            Return value
        End Function
    End Class
End Namespace
