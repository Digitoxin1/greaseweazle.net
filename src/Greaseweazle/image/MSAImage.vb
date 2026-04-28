Imports System.Linq
Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/msa.py::MSA
    Public Class Msa
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), IbmTrackFixed)()

        ' Python map: src/greaseweazle/image/msa.py::MSA.__init__
        Public Sub New()
            MyBase.New()
        End Sub

        ' Python map: src/greaseweazle/image/msa.py::MSA.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 10, "MSA: Unrecognised signature")

            ErrorHandling.Check(data(0) = &HE AndAlso data(1) = &HF, "MSA: Unrecognised signature")
            Dim spt = CInt(ReadUInt16BE(data, 2))
            Dim nsides = CInt(ReadUInt16BE(data, 4)) + 1
            Dim st = CInt(ReadUInt16BE(data, 6))
            Dim et = CInt(ReadUInt16BE(data, 8))
            ErrorHandling.Check(nsides >= 1 AndAlso nsides <= 2, String.Format("MSA: Bad number of sides: {0}", nsides))

            Dim idx = 10
            For cyl = st To et
                For head = 0 To nsides - 1
                    ErrorHandling.Check(idx + 2 <= data.Length, "MSA: Truncated track length")
                    Dim nbytes = CInt(ReadUInt16BE(data, idx))
                    idx += 2
                    ErrorHandling.Check(nbytes <= spt * 512, "MSA: Track data too long")
                    ErrorHandling.Check(idx + nbytes <= data.Length, "MSA: Truncated track data")
                    Dim td = data.Skip(idx).Take(nbytes).ToArray()
                    idx += nbytes

                    Dim trackData As Byte()
                    If nbytes = spt * 512 Then
                        trackData = td
                    Else
                        trackData = ExpandTrackData(td, spt * 512)
                    End If

                    Dim track = BuildTrackCodec(cyl, head, spt)
                    track.SetImgTrack(trackData)
                    _tracks(Tuple.Create(cyl, head)) = track
                Next
            Next
        End Sub

        ' Python map: src/greaseweazle/image/msa.py::MSA.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/image/msa.py::MSA.emit_track
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            ErrorHandling.Check(TypeOf track Is IbmTrackFixed,
                                String.Format("MSA: Track {0}.{1} is not an IBM track: Maybe missing --format= option?", cyl, side))
            _tracks(Tuple.Create(cyl, side)) = CType(track, IbmTrackFixed)
        End Sub

        ' Python map: src/greaseweazle/image/msa.py::MSA.get_image
        Public Overrides Function GetImage() As Byte()
            Dim nSide = If(_tracks.Count = 0, 0, _tracks.Keys.Max(Function(k) k.Item2))
            nSide += 1
            Dim st = If(_tracks.Count = 0, 0, _tracks.Keys.Min(Function(k) k.Item1))
            Dim et = If(_tracks.Count = 0, 0, _tracks.Keys.Max(Function(k) k.Item1))

            Dim outData As New List(Of Byte)()
            Dim spt As Integer? = Nothing
            For c = st To et
                For h = 0 To nSide - 1
                    Dim key = Tuple.Create(c, h)
                    ErrorHandling.Check(_tracks.ContainsKey(key), String.Format("MSA: Missing track {0}.{1} in output", c, h))
                    Dim track = _tracks(key)
                    Dim tdat = track.GetImgTrack()
                    Dim thisSpt = track.Nsec
                    If Not spt.HasValue Then
                        spt = thisSpt
                    End If
                    ErrorHandling.Check(spt.Value = thisSpt,
                                        String.Format("MSA: Track {0}.{1} has incorrect sectors per track ({2} != {3})",
                                                      c, h, spt.Value, thisSpt))

                    Dim compressed = CompressTrackData(tdat)
                    If compressed.Length < tdat.Length Then
                        outData.AddRange(ToUInt16BE(compressed.Length))
                        outData.AddRange(compressed)
                    Else
                        outData.AddRange(ToUInt16BE(tdat.Length))
                        outData.AddRange(tdat)
                    End If
                Next
            Next

            Dim header As New List(Of Byte)()
            header.Add(&HE)
            header.Add(&HF)
            header.AddRange(ToUInt16BE(If(spt.HasValue, spt.Value, 0)))
            header.AddRange(ToUInt16BE(Math.Max(0, nSide - 1)))
            header.AddRange(ToUInt16BE(st))
            header.AddRange(ToUInt16BE(et))
            Return header.Concat(outData).ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackCodec)
        Private Shared Function BuildTrackCodec(cyl As Integer, head As Integer, spt As Integer) As IbmTrackFixed
            ' Mirror Python `track = ibm.IBMTrack_FixedDef('ibm.mfm'); track.iam=False; track.rate=...
            ' track.rpm=300; track.secs=spt; track.sz=[2]; gap3/cskew/hskew per spt; finalise; mk_track`.
            Dim def As New IbmTrackFixedDef("ibm.mfm")
            def.AddParam("iam", "no")
            def.AddParam("secs", spt.ToString(Globalization.CultureInfo.InvariantCulture))
            def.AddParam("bps", "512")
            def.AddParam("rpm", "300")
            If spt <= 9 Then
                def.AddParam("rate", "250")
                def.AddParam("gap3", "84")
                def.AddParam("cskew", "4")
                def.AddParam("hskew", "2")
            ElseIf spt = 10 Then
                def.AddParam("rate", "250")
                def.AddParam("gap3", "30")
            Else
                def.AddParam("rate", "261")
                def.AddParam("gap3", "3")
            End If
            def.Finalise()
            Return CType(def.MkTrack(cyl, head), IbmTrackFixed)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ExpandTrackData)
        Private Shared Function ExpandTrackData(data As Byte(), expectedLen As Integer) As Byte()
            Dim out As New List(Of Byte)()
            Dim i = 0
            While i < data.Length
                Dim b = data(i)
                i += 1
                If b = &HE5 Then
                    ErrorHandling.Check(i + 3 <= data.Length, "MSA: Bad track compressed data")
                    Dim runByte = data(i)
                    Dim runLen = CInt(ReadUInt16BE(data, i + 1))
                    i += 3
                    out.AddRange(Enumerable.Repeat(runByte, runLen))
                Else
                    out.Add(b)
                End If
            End While
            ErrorHandling.Check(out.Count = expectedLen, "MSA: Bad track compressed data")
            Return out.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CompressTrackData)
        Private Shared Function CompressTrackData(data As Byte()) As Byte()
            Dim td As New List(Of Byte)()
            Dim idx = 0
            Dim runLen = 0
            Dim runByte As Byte = 0
            While idx < data.Length
                Dim b = data(idx)
                idx += 1
                If b <> runByte Then
                    FlushRun(td, runByte, runLen)
                    runLen = 0
                End If
                runByte = b
                runLen += 1
            End While
            FlushRun(td, runByte, runLen)
            Return td.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration FlushRun)
        Private Shared Sub FlushRun(output As List(Of Byte), runByte As Byte, runLen As Integer)
            If runLen <= 0 Then
                Return
            End If
            If runLen < 4 AndAlso runByte <> &HE5 Then
                output.AddRange(Enumerable.Repeat(runByte, runLen))
            Else
                output.Add(&HE5)
                output.Add(runByte)
                output.AddRange(ToUInt16BE(runLen))
            End If
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ReadUInt16BE)
        Private Shared Function ReadUInt16BE(data As Byte(), offset As Integer) As UShort
            Return CUShort((CInt(data(offset)) << 8) Or CInt(data(offset + 1)))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ToUInt16BE)
        Private Shared Function ToUInt16BE(value As Integer) As Byte()
            Return {CByte((value >> 8) And &HFF), CByte(value And &HFF)}
        End Function

    End Class

End Namespace
