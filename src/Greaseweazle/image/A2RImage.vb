Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/a2r.py::A2RCapType
    Public Enum A2RCapType
        Timing = 1
        XTiming = 3
    End Enum

    ' Python map: src/greaseweazle/image/a2r.py::A2R
    Public Class A2R
        Inherits Image

        Private Const CapTypeTiming As Integer = 1
        Private Const CapTypeXTiming As Integer = 3

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), A2RTrack)()

        ' Python map: src/greaseweazle/image/a2r.py::A2R.__init__
        Public Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        ' Python map: src/greaseweazle/image/a2r.py::A2R.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 8, "A2R: Invalid signature")
            Dim signature = data.Take(8).ToArray()
            Dim expected = New Byte() {AscW("A"c), AscW("2"c), AscW("R"c), AscW("3"c), &HFF, &HA, &HD, &HA}
            ErrorHandling.Check(signature.SequenceEqual(expected), "A2R: Invalid signature")

            Dim pos = 8
            While pos + 8 <= data.Length
                Dim chunkId = System.Text.Encoding.ASCII.GetString(data, pos, 4)
                Dim size = CInt(BitConverter.ToUInt32(data, pos + 4))
                pos += 8
                ErrorHandling.Check(pos + size <= data.Length, "A2R: Corrupt chunk size")
                If chunkId = "RWCP" Then
                    Dim rwcp(size - 1) As Byte
                    If size > 0 Then Array.Copy(data, pos, rwcp, 0, size)
                    ProcessRwcp(rwcp)
                End If
                pos += size
            End While
        End Sub

        ' Python map: src/greaseweazle/image/a2r.py::A2R.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key).ToFlux()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EmitTrack)
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Throw New FatalException(String.Format("{0}: Cannot create A2R image files", If(String.IsNullOrEmpty(FileName), "A2R", FileName)))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create A2R image files", If(String.IsNullOrEmpty(FileName), "A2R", FileName)))
        End Function

        ' Python map: src/greaseweazle/image/a2r.py::A2R.process_rwcp
        Private Sub ProcessRwcp(chunk As Byte())
            ErrorHandling.Check(chunk.Length >= 16, "A2R: Invalid RWCP chunk")
            Dim psPerTick = CDbl(BitConverter.ToUInt32(chunk, 1))
            ErrorHandling.Check(psPerTick > 0, "A2R: Invalid RWCP sample timing")

            Dim i = 16
            While i < chunk.Length AndAlso chunk(i) = AscW("C"c)
                Dim start = i
                ErrorHandling.Check(i + 5 <= chunk.Length, "A2R: Invalid capture header")

                Dim capType = CInt(chunk(i + 1))
                Dim loc = CInt(BitConverter.ToUInt16(chunk, i + 2))
                Dim nidx = CInt(chunk(i + 4))
                Dim cyl = loc >> 1
                Dim head = loc And 1
                i += 5

                ErrorHandling.Check(i + nidx * 4 <= chunk.Length, "A2R: Invalid index data")
                i += nidx * 4
                ErrorHandling.Check(i + 4 <= chunk.Length, "A2R: Invalid flux length")
                Dim ncap = CInt(BitConverter.ToUInt32(chunk, i))
                i += 4
                ErrorHandling.Check(i + ncap <= chunk.Length, "A2R: Invalid flux data")
                i += ncap

                If capType <> CapTypeTiming AndAlso capType <> CapTypeXTiming Then
                    Continue While
                End If

                Dim key = Tuple.Create(cyl, head)
                Dim track As A2RTrack = Nothing
                If Not _tracks.TryGetValue(key, track) Then
                    track = New A2RTrack(cyl, head, psPerTick)
                    _tracks(key) = track
                End If
                Dim capLen = i - start
                Dim cap(capLen - 1) As Byte
                If capLen > 0 Then Array.Copy(chunk, start, cap, 0, capLen)
                track.AddCap(cap)
            End While
        End Sub

        ' Python map: src/greaseweazle/image/a2r.py::A2RTrack
        Public Class A2RTrack
            Private ReadOnly _captures As New List(Of Byte())()
            Private ReadOnly _sampleFreq As Double

            ' Python map: src/greaseweazle/image/a2r.py::A2RTrack.__init__
            Public Sub New(cyl As Integer, head As Integer, psPerTick As Double)
                _sampleFreq = 1.0E12 / psPerTick
            End Sub

            ' Python map: src/greaseweazle/image/a2r.py::A2RTrack.add_cap
            Public Sub AddCap(capture As Byte())
                _captures.Add(capture)
            End Sub

            ' Python map: src/greaseweazle/image/a2r.py::A2RTrack.flux
            Public Function Flux() As Flux
                Dim dat = BestCapture()
                Dim nidx = CInt(dat(4))
                Dim i = 5 + nidx * 4
                Dim idxRaw As New List(Of Double)()
                For j = 0 To nidx - 1
                    idxRaw.Add(CDbl(BitConverter.ToUInt32(dat, 5 + j * 4)))
                Next
                Dim indexList As New List(Of Double)()
                For j = 0 To idxRaw.Count - 1
                    If j = 0 Then
                        indexList.Add(idxRaw(j))
                    Else
                        indexList.Add(idxRaw(j) - idxRaw(j - 1))
                    End If
                Next

                ' Match Python's variable-reuse quirk in A2RTrack.flux():
                ' the loop variable overwrites the capture cursor.
                For j = indexList.Count - 1 To 1 Step -1
                    i = j
                Next

                Dim ncap = CInt(BitConverter.ToUInt32(dat, i))
                i += 4
                Dim fluxList As New List(Of Double)()
                Dim acc = 0
                For k = 0 To ncap - 1
                    Dim p = i + k
                    If p >= dat.Length Then
                        Exit For
                    End If
                    Dim f = CInt(dat(p))
                    acc += f
                    If f <> 255 Then
                        fluxList.Add(acc)
                        acc = 0
                    End If
                Next
                If acc <> 0 Then
                    fluxList.Add(acc)
                End If

                Return New Flux(indexList, fluxList, _sampleFreq)
            End Function

            Public Function ToFlux() As Flux
                Return Flux()
            End Function

            ' Python map: src/greaseweazle/image/a2r.py::A2RTrack.best_cap
            Public Function BestCap() As Byte()
                For Each cap In _captures
                    If cap.Length > 4 AndAlso cap(4) = 2 Then
                        Return cap
                    End If
                Next
                Return _captures(_captures.Count - 1)
            End Function

            Private Function BestCapture() As Byte()
                Return BestCap()
            End Function
        End Class

    End Class

End Namespace
