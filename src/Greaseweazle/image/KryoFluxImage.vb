Imports Greaseweazle.Core
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/kryoflux.py::KryoFlux
    Public Class KryoFlux
        Inherits Image

        Private Const DefaultMasterClock As Double = 18432000.0 * 73.0 / 14.0 / 2.0
        Private Const DefaultSampleClock As Double = DefaultMasterClock / 2.0

        Private Const OpNop1 As Integer = 8
        Private Const OpNop2 As Integer = 9
        Private Const OpNop3 As Integer = 10
        Private Const OpOvl16 As Integer = 11
        Private Const OpFlux3 As Integer = 12
        Private Const OpOob As Integer = 13

        Private Const OobStreamInfo As Integer = 1
        Private Const OobIndex As Integer = 2
        Private Const OobStreamEnd As Integer = 3
        Private Const OobKfInfo As Integer = 4
        Private Const OobEof As Integer = 13

        Private ReadOnly _tracks As New Dictionary(Of Integer, Flux)()
        Private ReadOnly _baseName As String

        ' Python map: src/greaseweazle/image/kryoflux.py::KryoFlux.__init__
        Public Sub New(name As String)
            FileName = name
            _baseName = ParseBaseName(name)
            Options.WriteSettings.Add("sck")
            Options.WriteSettings.Add("revs")
        End Sub

        ' Python map: src/greaseweazle/image/kryoflux.py::KryoFlux.__enter__
        Public Overrides Function Enter() As Image
            Return MyBase.Enter()
        End Function

        ' Python map: src/greaseweazle/image/kryoflux.py::KryoFlux.__exit__
        Public Overrides Sub [Exit](exceptionType As Type, value As Exception)
            MyBase.Exit(exceptionType, value)
        End Sub

        ' Python map: src/greaseweazle/image/kryoflux.py::KryoFlux.from_file
        Public Shared Shadows Function FromFile(name As String) As KryoFlux
            Dim image = New KryoFlux(name)
            image.FileName = name
            image.FromBytes(File.ReadAllBytes(name))
            Return image
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration FromBytes)
        Public Overrides Sub FromBytes(data As Byte())
            Dim track = ParseTrackData(data)
            _tracks.Clear()
            _tracks(0) = track
        End Sub

        ' Python map: src/greaseweazle/image/kryoflux.py::KryoFlux.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = TrackKey(cyl, side)
            If _tracks.ContainsKey(key) Then
                Return _tracks(key)
            End If

            Dim name = TrackFileName(cyl, side)
            If Not File.Exists(name) Then
                Return Nothing
            End If

            Dim track = ParseTrackData(File.ReadAllBytes(name))
            _tracks(key) = track
            Return track
        End Function

        ' Python map: src/greaseweazle/image/kryoflux.py::KryoFlux.emit_track
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Dim flux = track.Flux()
            flux.CueAtIndex()
            Dim configuredRevs = ResolveConfiguredRevolutions()
            If configuredRevs.HasValue Then
                flux.SetNrRevs(configuredRevs.Value)
            End If
            _tracks(TrackKey(cyl, side)) = flux
            WriteTrackFile(cyl, side, flux)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            Return Array.Empty(Of Byte)()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration TrackFileName)
        Private Function TrackFileName(cyl As Integer, side As Integer) As String
            Return _baseName & cyl.ToString("00", CultureInfo.InvariantCulture) & "." &
                side.ToString(CultureInfo.InvariantCulture) & ".raw"
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration TrackKey)
        Private Shared Function TrackKey(cyl As Integer, side As Integer) As Integer
            Return cyl * 2 + side
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseBaseName)
        Private Shared Function ParseBaseName(name As String) As String
            Dim m = Regex.Match(name, "\d{2}\.[01]\.raw$", RegexOptions.IgnoreCase)
            ErrorHandling.Check(m.Success,
                                String.Format("Bad Kryoflux image name pattern '{0}'{1}Name pattern must be path/to/nameNN.N.raw (N is a digit)",
                                              name, Environment.NewLine))
            Return name.Substring(0, m.Index)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseTrackData)
        Private Shared Function ParseTrackData(data As Byte()) As Flux
            Dim dat = If(data, Array.Empty(Of Byte)())
            Dim index As New List(Of Integer)()
            Dim idx = 0
            Dim streamIdx = 0
            Dim sck = DefaultSampleClock

            While idx < dat.Length
                Dim op = CInt(dat(idx))
                If op = OpOob Then
                    ErrorHandling.Check(idx + 4 <= dat.Length, "KryoFlux: Truncated OOB header")
                    Dim oobOp = CInt(dat(idx + 1))
                    Dim oobSz = CInt(dat(idx + 2)) Or (CInt(dat(idx + 3)) << 8)
                    idx += 4
                    If oobOp = OobEof Then
                        Exit While
                    End If
                    ErrorHandling.Check(idx + oobSz <= dat.Length, "KryoFlux: Truncated OOB payload")
                    If oobOp = OobIndex AndAlso oobSz >= 4 Then
                        Dim pos = BitConverter.ToUInt32(dat, idx)
                        index.Add(CInt(pos))
                    ElseIf oobOp = OobKfInfo AndAlso oobSz > 0 Then
                        Dim info = Encoding.UTF8.GetString(dat, idx, Math.Max(0, oobSz - 1))
                        Dim sm = Regex.Match(info, "sck=([^,]+)")
                        If sm.Success Then
                            Dim parsed As Double
                            If Double.TryParse(sm.Groups(1).Value, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                                sck = parsed
                            End If
                        End If
                    End If
                    idx += oobSz
                ElseIf op = OpNop3 OrElse op = OpFlux3 Then
                    idx += 3
                    streamIdx += 3
                ElseIf op <= 7 OrElse op = OpNop2 Then
                    idx += 2
                    streamIdx += 2
                Else
                    idx += 1
                    streamIdx += 1
                End If
            End While

            Dim flux As New List(Of Integer)()
            Dim fluxList As New List(Of Double)()
            Dim indexList As New List(Of Double)()
            Dim val = 0
            Dim indexIdx = 0
            streamIdx = 0
            idx = 0

            While idx < dat.Length
                If indexIdx < index.Count AndAlso streamIdx >= index(indexIdx) Then
                    indexList.Add(flux.Sum())
                    fluxList.AddRange(flux.Select(Function(x) CDbl(x)))
                    flux.Clear()
                    indexIdx += 1
                End If

                Dim op = CInt(dat(idx))
                If op <= 7 Then
                    ErrorHandling.Check(idx + 2 <= dat.Length, "KryoFlux: Truncated Flux2 record")
                    val += (op << 8) + CInt(dat(idx + 1))
                    flux.Add(val)
                    val = 0
                    streamIdx += 2
                    idx += 2
                ElseIf op <= 10 Then
                    Dim nr = op - 7
                    streamIdx += nr
                    idx += nr
                ElseIf op = OpOvl16 Then
                    val += &H10000
                    streamIdx += 1
                    idx += 1
                ElseIf op = OpFlux3 Then
                    ErrorHandling.Check(idx + 3 <= dat.Length, "KryoFlux: Truncated Flux3 record")
                    val += (CInt(dat(idx + 1)) << 8) + CInt(dat(idx + 2))
                    flux.Add(val)
                    val = 0
                    streamIdx += 3
                    idx += 3
                ElseIf op = OpOob Then
                    ErrorHandling.Check(idx + 4 <= dat.Length, "KryoFlux: Truncated OOB header")
                    Dim oobOp = CInt(dat(idx + 1))
                    Dim oobSz = CInt(dat(idx + 2)) Or (CInt(dat(idx + 3)) << 8)
                    idx += 4
                    If oobOp = OobEof Then
                        Exit While
                    End If
                    ErrorHandling.Check(idx + oobSz <= dat.Length, "KryoFlux: Truncated OOB payload")
                    If (oobOp = OobStreamInfo OrElse oobOp = OobStreamEnd) AndAlso oobSz >= 4 Then
                        Dim pos = CInt(BitConverter.ToUInt32(dat, idx))
                        ErrorHandling.Check(pos = streamIdx, "Out-of-sync during KryoFlux stream read")
                    End If
                    idx += oobSz
                Else
                    val += op
                    flux.Add(val)
                    val = 0
                    streamIdx += 1
                    idx += 1
                End If
            End While
            fluxList.AddRange(flux.Select(Function(x) CDbl(x)))

            If indexList.Count > 1 Then
                Dim shortIndex = indexList(0)
                ' List(Of T).RemoveAt(0) is O(n) but uses Array.Copy under the
                ' hood; equivalent perf to Skip(1).ToList() but no extra LINQ
                ' allocation.
                indexList.RemoveAt(0)
                Dim seen = 0.0
                Dim startAt = 0
                While startAt < fluxList.Count AndAlso seen < shortIndex
                    seen += fluxList(startAt)
                    startAt += 1
                End While
                If startAt > 0 Then
                    ' GetRange is O(n) Array.Copy; replaces Skip(startAt).ToList()
                    ' which iterates from index 0.
                    fluxList = fluxList.GetRange(startAt, fluxList.Count - startAt)
                End If
            End If

            Return New Flux(indexList, fluxList, sck, indexCued:=True)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration WriteTrackFile)
        Private Sub WriteTrackFile(cyl As Integer, side As Integer, source As Flux)
            Dim flux = source
            Dim sck = ResolveSampleClock()

            Dim now = DateTime.Now
            Dim info = String.Format(CultureInfo.InvariantCulture,
                                     "name=Greaseweazle, version={0}, host_date={1}, host_time={2}, sck={3:F7}, ick={4:F7}",
                                     HostVersion.MajorMinor,
                                     now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture),
                                     now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                                     sck,
                                     sck / 8.0)

            Dim dat As New List(Of Byte)()
            AddOobHeader(dat, OobKfInfo, info.Length + 1)
            dat.AddRange(Encoding.UTF8.GetBytes(info))
            dat.Add(0)

            If flux.IndexCued Then
                AddOobIndex(dat, 0, 0, 0)
            End If

            Dim factor = sck / flux.SampleFreq
            ' Python: index = list(it.accumulate(map(lambda x: x*factor, flux.index_list)))
            ' We need a prefix sum of (index_list[i] * factor); the previous code only
            ' applied the multiply, which produced per-revolution durations rather than
            ' the cumulative stream-time the index OOB writer expects.
            Dim index As New List(Of Double)()
            Dim accum As Double = 0.0
            For Each x In flux.IndexList
                accum += x * factor
                index.Add(accum)
            Next
            Dim indexIdx = 0
            Dim streamIdx = 0
            Dim total = 0.0
            Dim remd = 0.0

            Dim checkIndex =
                Sub(prevFlux As Integer)
                    If indexIdx < index.Count AndAlso total >= index(indexIdx) Then
                        Dim delta = CInt(Math.Round(index(indexIdx) - total + prevFlux, MidpointRounding.ToEven))
                        Dim idxTime = CInt(Math.Round(index(indexIdx) / 8.0, MidpointRounding.ToEven))
                        AddOobIndex(dat, streamIdx, delta, idxTime)
                        indexIdx += 1
                    End If
                End Sub

            Dim emitFlux =
                Sub(f As Integer)
                    While f >= &H10000
                        dat.Add(CByte(OpOvl16))
                        streamIdx += 1
                        f -= &H10000
                        total += &H10000
                        checkIndex(&H10000)
                    End While

                    If f >= &H800 Then
                        dat.Add(CByte(OpFlux3))
                        dat.Add(CByte((f >> 8) And &HFF))
                        dat.Add(CByte(f And &HFF))
                        streamIdx += 3
                    ElseIf f > OpOob AndAlso f < &H100 Then
                        dat.Add(CByte(f))
                        streamIdx += 1
                    Else
                        dat.Add(CByte((f >> 8) And &HFF))
                        dat.Add(CByte(f And &HFF))
                        streamIdx += 2
                    End If
                    total += f
                    checkIndex(f)
                End Sub

            For Each x In flux.List
                Dim y = x * factor + remd
                Dim f = CInt(Math.Round(y, MidpointRounding.ToEven))
                remd = y - f
                emitFlux(Math.Max(1, f))
            Next

            If indexIdx < index.Count Then
                Dim pad = CInt(Math.Ceiling(index(indexIdx) - total)) + 1
                emitFlux(Math.Max(1, pad))
            End If
            emitFlux(CInt(Math.Round(sck * 12.0E-6, MidpointRounding.ToEven)))

            AddOobHeader(dat, OobStreamEnd, 8)
            AddUInt32LE(dat, streamIdx)
            AddUInt32LE(dat, 0)

            AddOobHeader(dat, OobEof, &H0D0D)

            Dim path = TrackFileName(cyl, side)
            Dim mode = If(NoClobber, FileMode.CreateNew, FileMode.Create)
            Using fs As New FileStream(path, mode, FileAccess.Write, FileShare.None)
                fs.Write(dat.ToArray(), 0, dat.Count)
            End Using
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveConfiguredRevolutions)
        Private Function ResolveConfiguredRevolutions() As Integer?
            Dim raw As String = Nothing
            If Not Options.Values.TryGetValue("revs", raw) Then
                Return Nothing
            End If
            Dim revs As Integer
            If Not Integer.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, revs) OrElse revs < 1 Then
                Throw New FatalException(String.Format("Kryoflux: Invalid revs: '{0}'", raw))
            End If
            Return revs
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveSampleClock)
        Private Function ResolveSampleClock() As Double
            Dim raw As String = Nothing
            If Not Options.Values.TryGetValue("sck", raw) Then
                Return DefaultSampleClock
            End If

            Dim work = raw
            Dim factor = 1.0
            If work.EndsWith("m", StringComparison.OrdinalIgnoreCase) Then
                work = work.Substring(0, work.Length - 1)
                factor = 1000000.0
            End If

            Dim parsed As Double
            If Not Double.TryParse(work, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                Throw New FatalException(String.Format("Kryoflux: Bad sck value: '{0}'", work))
            End If
            Return parsed * factor
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AddOobHeader)
        Private Shared Sub AddOobHeader(dat As List(Of Byte), oobOp As Integer, payloadSize As Integer)
            dat.Add(CByte(OpOob))
            dat.Add(CByte(oobOp))
            AddUInt16LE(dat, payloadSize)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AddOobIndex)
        Private Shared Sub AddOobIndex(dat As List(Of Byte), streamPos As Integer, delta As Integer, idxTime As Integer)
            AddOobHeader(dat, OobIndex, 12)
            AddUInt32LE(dat, streamPos)
            AddUInt32LE(dat, delta)
            AddUInt32LE(dat, idxTime)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AddUInt16LE)
        Private Shared Sub AddUInt16LE(dat As List(Of Byte), value As Integer)
            dat.Add(CByte(value And &HFF))
            dat.Add(CByte((value >> 8) And &HFF))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AddUInt32LE)
        Private Shared Sub AddUInt32LE(dat As List(Of Byte), value As Integer)
            dat.Add(CByte(value And &HFF))
            dat.Add(CByte((value >> 8) And &HFF))
            dat.Add(CByte((value >> 16) And &HFF))
            dat.Add(CByte((value >> 24) And &HFF))
        End Sub

    End Class

    ' Python map: src/greaseweazle/image/kryoflux.py::Op
    Public Enum Op
        Nop1 = 8
        Nop2 = 9
        Nop3 = 10
        Ovl16 = 11
        Flux3 = 12
        Oob = 13
    End Enum

    ' Python map: src/greaseweazle/image/kryoflux.py::OOB
    Public Enum Oob
        StreamInfo = 1
        Index = 2
        StreamEnd = 3
        KfInfo = 4
        Eof = 13
    End Enum

    ' Python map: src/greaseweazle/image/kryoflux.py::KFOpts
    Public Class KfOpts
        ' Python map: src/greaseweazle/image/kryoflux.py::KFOpts.__init__
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/image/kryoflux.py::KFOpts.sck
        Public Property Sck As String

        ' Python map: src/greaseweazle/image/kryoflux.py::KFOpts.revs
        Public Property Revs As String
    End Class

End Namespace
