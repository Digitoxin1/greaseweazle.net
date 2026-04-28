Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/hfe.py::HFE
    Public Class Hfe
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), MasterTrack)()
        Private _bitrateKbps As Integer? = Nothing
        Private Shared ReadOnly InterfaceModes As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
            {"IBMPC_DD", &H0},
            {"IBMPC_HD", &H1},
            {"ATARIST_DD", &H2},
            {"ATARIST_HD", &H3},
            {"AMIGA_DD", &H4},
            {"AMIGA_HD", &H5},
            {"CPC_DD", &H6},
            {"GENERIC_SHUGART_DD", &H7},
            {"IBMPC_ED", &H8},
            {"MSX2_DD", &H9},
            {"C64_DD", &HA},
            {"EMU_SHUGART", &HB},
            {"S950_DD", &HC},
            {"S950_HD", &HD},
            {"S950_DD_HD", &HE},
            {"IBMPC_DD_HD", &HF},
            {"QUICKDISK", &H10},
            {"UNKNOWN", &HFF}
        }
        Private Shared ReadOnly EncodingTypes As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
            {"ISOIBM_MFM", &H0},
            {"AMIGA_MFM", &H1},
            {"ISOIBM_FM", &H2},
            {"EMU_FM", &H3},
            {"TYCOM_FM", &H4},
            {"MEMBRAIN_MFM", &H5},
            {"APPLEII_GCR1", &H6},
            {"APPLEII_GCR2", &H7},
            {"APPLEII_HDDD_A2_GCR1", &H8},
            {"APPLEII_HDDD_A2_GCR2", &H9},
            {"ARBURGDAT", &HA},
            {"ARBURGSYS", &HB},
            {"AED6200P_MFM", &HC},
            {"NORTHSTAR_HS_MFM", &HD},
            {"HEATHKIT_HS_FM", &HE},
            {"DEC_RX02_M2FM", &HF},
            {"APPLEMAC_GCR", &H10},
            {"QD_MO5", &H11},
            {"C64_GCR", &H12},
            {"VICTOR9K_GCR", &H13},
            {"MICRALN_HS_FM", &H14},
            {"UNKNOWN", &HFF}
        }

        ' Python map: src/greaseweazle/image/hfe.py::HFE.__init__
        Public Sub New()
            MyBase.New()
            Me.ReadOnly = False
            Options.WriteSettings.Add("bitrate")
            Options.WriteSettings.Add("version")
            Options.WriteSettings.Add("interface")
            Options.WriteSettings.Add("encoding")
            Options.WriteSettings.Add("double_step")
            Options.WriteSettings.Add("uniform")
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFE.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 20, "Not a valid HFE file")

            Dim sig = System.Text.Encoding.ASCII.GetString(data, 0, 8)
            Dim version As Integer
            If String.Equals(sig, "HXCHFEV3", StringComparison.Ordinal) Then
                version = 3
            Else
                ErrorHandling.Check(String.Equals(sig, "HXCPICFE", StringComparison.Ordinal), "Not a valid HFE file")
                version = 1
            End If

            Dim fileRev = CInt(data(8))
            Dim nCyl = CInt(data(9))
            Dim nSide = CInt(data(10))
            Dim bitrate = CInt(BitConverter.ToUInt16(data, 12))
            Dim tlutBase = CInt(BitConverter.ToUInt16(data, 18))

            ErrorHandling.Check(fileRev <= 1, "Not a valid HFE file")
            ErrorHandling.Check(nCyl > 0, "HFE: Invalid #cyls")
            ErrorHandling.Check(nSide > 0 AndAlso nSide < 3, "HFE: Invalid #sides")
            ErrorHandling.Check(bitrate > 0, "HFE: Invalid bitrate")
            _bitrateKbps = bitrate

            Dim tlutStart = tlutBase * 512
            Dim tlutLen = nCyl * 4
            ErrorHandling.Check(tlutStart >= 0 AndAlso (tlutStart + tlutLen) <= data.Length, "HFE: Truncated track table")

            For cyl = 0 To nCyl - 1
                Dim offset = CInt(BitConverter.ToUInt16(data, tlutStart + cyl * 4))
                Dim length = CInt(BitConverter.ToUInt16(data, tlutStart + cyl * 4 + 2))
                Dim todo = length \ 2
                If todo <= 0 Then
                    Continue For
                End If

                For side = 0 To nSide - 1
                    Dim tdat As New List(Of Byte)()
                    Dim walkOffset = offset
                    Dim remaining = todo
                    While remaining > 0
                        Dim dOff = walkOffset * 512 + side * 256
                        Dim dNr = Math.Min(256, remaining)
                        ErrorHandling.Check(dOff >= 0 AndAlso (dOff + dNr) <= data.Length, "HFE: Truncated track data")
                        tdat.AddRange(data.Skip(dOff).Take(dNr))
                        remaining -= dNr
                        walkOffset += 1
                    End While

                    If version = 3 Then
                        _tracks(Tuple.Create(cyl, side)) = DecodeHfeV3Track(cyl, side, tdat.ToArray())
                    Else
                        Dim bits = DecodeHfeBits(tdat.ToArray())
                        Dim timePerRev = bits.Count / (2000.0 * bitrate)
                        _tracks(Tuple.Create(cyl, side)) = New MasterTrack(bits, timePerRev)
                    End If
                Next
            Next
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFE.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/image/hfe.py::HFE.emit_track
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Dim master As MasterTrack = Nothing
            Dim codec = TryCast(track, Codec)
            If codec IsNot Nothing Then
                master = codec.MasterTrack()
            Else
                master = TryCast(track, MasterTrack)
            End If
            If master Is Nothing Then
                Dim sourceFlux = TryCast(track, Flux)
                If sourceFlux Is Nothing Then
                    sourceFlux = track.Flux()
                End If
                If sourceFlux IsNot Nothing Then
                    Dim bitrate = ResolveFluxEmitBitrate()
                    sourceFlux.CueAtIndex()
                    Dim pll As New PllTrack(5.0E-4 / bitrate, sourceFlux)
                    ErrorHandling.Check(pll.Revolutions.Count > 0,
                                        String.Format("HFE: Cannot create T{0}.{1}: No PLL revolutions decoded", cyl, side))
                    Dim rev0 = pll.GetRevolution(0)
                    Dim bitTicks = If(IsUniformEnabled(), Nothing, rev0.Item2)
                    master = New MasterTrack(rev0.Item1,
                                             sourceFlux.TimePerRev,
                                             bitTicks:=bitTicks,
                                             hardsectorBits:=pll.Revolutions(0).HardsectorBits)
                End If
            End If
            ErrorHandling.Check(master IsNot Nothing,
                                String.Format("HFE: Cannot create T{0}.{1}: Unsupported track type", cyl, side))
            Dim useDoubleRate = ShouldUseDoubleRate(track, codec, master)
            Dim autoBitrateKbps As Integer? = Nothing
            If Not _bitrateKbps.HasValue Then
                autoBitrateKbps = Math.Max(1, CInt(Math.Round(master.Bitrate / 2000.0)))
                If useDoubleRate Then
                    autoBitrateKbps = autoBitrateKbps.Value * 2
                End If
            End If
            master = PrepareMasterTrackForEmit(master, useDoubleRate)
            ' Python's hfe.opts.double_step does NOT physically double bits.
            ' It only flips a header byte so HxC firmware steps the heads twice
            ' per cylinder. The bit data on disk is identical either way.
            _tracks(Tuple.Create(cyl, side)) = master
            If autoBitrateKbps.HasValue Then
                _bitrateKbps = autoBitrateKbps.Value
            End If
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFE.get_image
        Public Overrides Function GetImage() As Byte()
            Dim bitrate = ResolveOutputBitrate()
            Dim version = ResolveOutputVersion()
            Dim interfaceMode = ResolveInterfaceMode()
            Dim encodingType = ResolveEncodingType()
            Dim doubleStepByte = ResolveDoubleStepByte()
            If version = 3 Then
                Return GetImageV3(bitrate, interfaceMode, encodingType, doubleStepByte)
            End If

            Dim nSide = 1
            Dim nCyl = If(_tracks.Count = 0, 1, _tracks.Keys.Max(Function(k) k.Item1) + 1)
            Dim tlut As New List(Of Byte)()
            Dim tdat As New List(Of Byte)()

            For cyl = 0 To nCyl - 1
                Dim s0 = GetMasterTrackOrNothing(cyl, 0)
                Dim s1 = GetMasterTrackOrNothing(cyl, 1)
                If s0 Is Nothing AndAlso s1 Is Nothing Then
                    Dim emptyNrBytes = 100 * bitrate
                    tlut.AddRange(BitConverter.GetBytes(CUShort((tdat.Count \ 512) + 2)))
                    tlut.AddRange(BitConverter.GetBytes(CUShort(emptyNrBytes)))
                    Dim padLen = (emptyNrBytes + &H1FF) And Not &H1FF
                    tdat.AddRange(Enumerable.Repeat(CByte(&H88), padLen))
                    Continue For
                End If

                If s1 IsNot Nothing Then
                    nSide = 2
                End If

                Dim b0 = If(s0 Is Nothing, Array.Empty(Of Byte)(), EncodeHfeBytes(s0.Bits))
                Dim b1 = If(s1 Is Nothing, Array.Empty(Of Byte)(), EncodeHfeBytes(s1.Bits))
                Dim nrBytes = Math.Max(b0.Length, b1.Length)
                Dim nrBlocks = (nrBytes + &HFF) \ &H100
                tlut.AddRange(BitConverter.GetBytes(CUShort((tdat.Count \ 512) + 2)))
                tlut.AddRange(BitConverter.GetBytes(CUShort(2 * nrBytes)))

                For b = 0 To nrBlocks - 1
                    Dim s0Slice = b0.Skip(b * 256).Take(256).ToArray()
                    Dim s1Slice = b1.Skip(b * 256).Take(256).ToArray()
                    If s0Slice.Length < 256 Then
                        s0Slice = s0Slice.Concat(Enumerable.Repeat(CByte(&H88), 256 - s0Slice.Length)).ToArray()
                    End If
                    If s1Slice.Length < 256 Then
                        s1Slice = s1Slice.Concat(Enumerable.Repeat(CByte(&H88), 256 - s1Slice.Length)).ToArray()
                    End If
                    tdat.AddRange(s0Slice)
                    tdat.AddRange(s1Slice)
                Next
            Next

            Dim header As New List(Of Byte)()
            header.AddRange(System.Text.Encoding.ASCII.GetBytes("HXCPICFE"))
            header.Add(0) ' f_rev
            header.Add(CByte(Math.Min(255, nCyl)))
            header.Add(CByte(nSide))
            header.Add(CByte(encodingType And &HFF))
            header.AddRange(BitConverter.GetBytes(CUShort(bitrate)))
            header.AddRange(BitConverter.GetBytes(CUShort(0))) ' rpm unused
            header.Add(CByte(interfaceMode And &HFF))
            header.Add(1)    ' reserved
            header.AddRange(BitConverter.GetBytes(CUShort(1))) ' tlut block index
            header.Add(&HFF) ' write allowed
            header.Add(doubleStepByte)

            While header.Count < 512
                header.Add(&HFF)
            End While
            While tlut.Count < 512
                tlut.Add(&HFF)
            End While

            Return header.Concat(tlut).Concat(tdat).ToArray()
        End Function

        ' Python map: src/greaseweazle/image/hfe.py::hfev3_get_image
        ' Dual-head HFEv3 generator that interleaves both heads' bitcells while
        ' tracking timing skew and bitrate drift, mirroring Python exactly.
        Private Function GetImageV3(bitrate As Integer,
                                    interfaceMode As Integer,
                                    encodingType As Integer,
                                    doubleStepByte As Byte) As Byte()
            Dim nSide = 1
            Dim nCyl = If(_tracks.Count = 0, 1, _tracks.Keys.Max(Function(k) k.Item1) + 1)
            Dim tlut As New List(Of Byte)()
            Dim tdat As New List(Of Byte)()
            Dim defaultTimePerRev = 0.2
            If _tracks.Count > 0 Then
                defaultTimePerRev = _tracks.Values.First().TimePerRev
            End If

            For cyl = 0 To nCyl - 1
                Dim s0Track = GetMasterTrackOrNothing(cyl, 0)
                Dim s1Track = GetMasterTrackOrNothing(cyl, 1)
                Dim timePerRev = defaultTimePerRev
                If s0Track IsNot Nothing Then
                    timePerRev = s0Track.TimePerRev
                ElseIf s1Track IsNot Nothing Then
                    timePerRev = s1Track.TimePerRev
                End If
                If s1Track IsNot Nothing Then
                    nSide = 2
                End If

                Dim s = New Hfev3Generator() {
                    If(s0Track IsNot Nothing,
                       New Hfev3Generator(s0Track),
                       Hfev3Generator.CreateEmpty(timePerRev, bitrate)),
                    If(s1Track IsNot Nothing,
                       New Hfev3Generator(s1Track),
                       Hfev3Generator.CreateEmpty(timePerRev, bitrate))
                }

                Const RateDistance As Integer = 64

                Do
                    ' Pick the side with shortest output and remaining work.
                    Dim x = s(0)
                    Dim y = s(1)
                    Dim c = x.Chunk
                    If c Is Nothing OrElse (y.Out.Count < x.Out.Count AndAlso y.Chunk IsNot Nothing) Then
                        x = s(1)
                        y = s(0)
                        c = x.Chunk
                        If c Is Nothing Then Exit Do
                    End If

                    Dim diff As Integer = CInt(Math.Round((x.Time - y.Time) / c.TimePerBit)) +
                                           (y.Out.Count - x.Out.Count) * 8

                    Dim tpb = c.TimePerBit + (x.Time - x.HfeTime) / (RateDistance * 8)

                    If c.EmitIndex Then
                        c.EmitIndex = False
                        x.Out.Add(Hfev3Op.Index)
                        diff -= 8
                    End If

                    Dim rate = CInt(Math.Round(tpb * 36000000.0))
                    If rate <> x.Rate AndAlso
                       (Math.Abs(rate - x.Rate) > 1 OrElse diff >= 16 OrElse
                        (x.Out.Count - x.RateChangePos) > RateDistance) Then
                        x.Out.Add(Hfev3Op.Bitrate)
                        x.Out.Add(CByte(Math.Max(0, Math.Min(255, rate))))
                        x.Rate = rate
                        x.RateChangePos = x.Out.Count
                        diff -= 16
                    End If

                    If diff >= 8 Then
                        x.Out.Add(Hfev3Op.Nop)
                    End If

                    Dim n = Math.Min(c.NBits, 8)
                    If n < 8 Then
                        x.Out.Add(Hfev3Op.SkipBits)
                        x.Out.Add(CByte(8 - n))
                    End If

                    If c.IsRandom Then
                        x.Out.Add(Hfev3Op.Rand)
                    Else
                        Dim b = ReadBitsAsByte(x.Track.Bits, x.Pos, n) << (8 - n)
                        b = b And &HFF
                        Dim emit = b >> (8 - n)
                        ' If looks like an opcode, drop a bit.
                        If (emit And &HF0) = &HF0 Then
                            n = 7
                            emit >>= 1
                            x.Out.Add(Hfev3Op.SkipBits)
                            x.Out.Add(CByte(8 - n))
                        End If
                        x.Out.Add(CByte(emit And &HFF))
                    End If

                    x.IncrementPosition(n)
                Loop

                Dim nrBytes = Math.Max(s(0).Out.Count, s(1).Out.Count)
                ErrorHandling.Check(nrBytes < 32768, "HFEv3: Track too long to fit in image")
                Dim nrBlocks = (nrBytes + &HFF) \ &H100
                For Each gen In s
                    Dim padCount = nrBlocks * &H100 - gen.Out.Count
                    For i = 1 To padCount
                        gen.Out.Add(Hfev3Op.Nop)
                    Next
                Next

                tlut.AddRange(BitConverter.GetBytes(CUShort((tdat.Count \ 512) + 2)))
                tlut.AddRange(BitConverter.GetBytes(CUShort(2 * nrBytes)))

                Dim raw0 = s(0).RawHfeBytes()
                Dim raw1 = s(1).RawHfeBytes()
                For b = 0 To nrBlocks - 1
                    tdat.AddRange(raw0.Skip(b * 256).Take(256))
                    tdat.AddRange(raw1.Skip(b * 256).Take(256))
                Next
            Next

            Dim header As New List(Of Byte)()
            header.AddRange(System.Text.Encoding.ASCII.GetBytes("HXCHFEV3"))
            header.Add(0) ' f_rev
            header.Add(CByte(Math.Min(255, nCyl)))
            header.Add(CByte(nSide))
            header.Add(CByte(encodingType And &HFF))
            header.AddRange(BitConverter.GetBytes(CUShort(bitrate)))
            header.AddRange(BitConverter.GetBytes(CUShort(0))) ' rpm unused
            header.Add(CByte(interfaceMode And &HFF))
            header.Add(1)    ' reserved
            header.AddRange(BitConverter.GetBytes(CUShort(1))) ' tlut block index
            header.Add(&HFF) ' write allowed
            header.Add(doubleStepByte)

            While header.Count < 512
                header.Add(&HFF)
            End While
            While tlut.Count < 512
                tlut.Add(&HFF)
            End While

            Return header.Concat(tlut).Concat(tdat).ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ReadBitsAsByte)
        Private Shared Function ReadBitsAsByte(bits As IList(Of Boolean), start As Integer, count As Integer) As Integer
            Dim value = 0
            For i = 0 To count - 1
                value <<= 1
                If start + i < bits.Count AndAlso bits(start + i) Then
                    value = value Or 1
                End If
            Next
            Return value
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveOutputBitrate)
        Private Function ResolveOutputBitrate() As Integer
            Dim configured As String = Nothing
            If Options.Values.TryGetValue("bitrate", configured) Then
                Dim parsed As Integer
                ErrorHandling.Check(Integer.TryParse(configured, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) AndAlso parsed > 0,
                                    String.Format("HFE: Invalid bitrate: '{0}'", configured))
                Return parsed
            End If
            If _bitrateKbps.HasValue Then
                Return _bitrateKbps.Value
            End If
            Return 250
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveOutputVersion)
        Private Function ResolveOutputVersion() As Integer
            Dim configured As String = Nothing
            If Not Options.Values.TryGetValue("version", configured) Then
                Return 1
            End If
            Dim parsed As Integer
            If Integer.TryParse(configured, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) AndAlso
                (parsed = 1 OrElse parsed = 3) Then
                Return parsed
            End If
            Throw New FatalException(String.Format("HFE: Invalid version: '{0}'", configured))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveInterfaceMode)
        Private Function ResolveInterfaceMode() As Integer
            Return ResolveNamedOrNumericOption("interface", InterfaceModes, "Bad HFE interface mode: '{0}'")
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveEncodingType)
        Private Function ResolveEncodingType() As Integer
            Return ResolveNamedOrNumericOption("encoding", EncodingTypes, "Bad HFE encoding type: '{0}'")
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDoubleStepByte)
        Private Function ResolveDoubleStepByte() As Byte
            Dim raw As String = Nothing
            If Not Options.Values.TryGetValue("double_step", raw) Then
                Return &HFF
            End If
            If String.IsNullOrEmpty(raw) Then
                Return &HFF
            End If
            Return 0
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsDoubleStepEnabled)
        Private Function IsDoubleStepEnabled() As Boolean
            Dim raw As String = Nothing
            Return Options.Values.TryGetValue("double_step", raw) AndAlso Not String.IsNullOrEmpty(raw)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration IsUniformEnabled)
        Private Function IsUniformEnabled() As Boolean
            Dim raw As String = Nothing
            Return Options.Values.TryGetValue("uniform", raw) AndAlso Not String.IsNullOrEmpty(raw)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveFluxEmitBitrate)
        Private Function ResolveFluxEmitBitrate() As Integer
            Dim configured As String = Nothing
            If Options.Values.TryGetValue("bitrate", configured) Then
                Dim parsed As Integer
                ErrorHandling.Check(Integer.TryParse(configured, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) AndAlso parsed > 0,
                                    String.Format("HFE: Invalid bitrate: '{0}'", configured))
                Return parsed
            End If
            ErrorHandling.Check(_bitrateKbps.HasValue,
                                "HFE: Requires bitrate to be specified (eg. filename.hfe::bitrate=500)")
            Return _bitrateKbps.Value
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration PrepareMasterTrackForEmit)
        Private Shared Function PrepareMasterTrackForEmit(source As MasterTrack, useDoubleRate As Boolean) As MasterTrack
            Dim bitCount = source.Bits.Count
            If bitCount = 0 Then
                Return source
            End If

            Dim splice = ((source.Splice Mod bitCount) + bitCount) Mod bitCount
            Dim index = ((-splice Mod bitCount) + bitCount) Mod bitCount
            Dim rotatedBits = RotateList(source.Bits, index)
            Dim rotatedTicks As List(Of Double) = Nothing
            If source.BitTicks IsNot Nothing Then
                rotatedTicks = RotateList(source.BitTicks, index)
            End If

            Dim rotatedWeak As New List(Of Tuple(Of Integer, Integer))()
            For Each w In source.WeakRanges
                Dim s = w.Item1 - splice
                Dim n = w.Item2
                If s < 0 Then
                    If s + n > 0 Then
                        rotatedWeak.Add(Tuple.Create(((s Mod bitCount) + bitCount) Mod bitCount, -s))
                        rotatedWeak.Add(Tuple.Create(0, s + n))
                    Else
                        rotatedWeak.Add(Tuple.Create(((s Mod bitCount) + bitCount) Mod bitCount, n))
                    End If
                Else
                    rotatedWeak.Add(Tuple.Create(s, n))
                End If
            Next

            Dim hardsectorBits = If(source.HardsectorBits Is Nothing, Nothing, source.HardsectorBits.ToList())

            If useDoubleRate Then
                rotatedBits = rotatedBits.SelectMany(Function(b) New Boolean() {b, b}).ToList()
                If rotatedTicks IsNot Nothing Then
                    rotatedTicks = rotatedTicks.SelectMany(Function(t) New Double() {t, t}).ToList()
                End If
                rotatedWeak = rotatedWeak.Select(Function(w) Tuple.Create(w.Item1 * 2, w.Item2 * 2)).ToList()
                If hardsectorBits IsNot Nothing Then
                    hardsectorBits = hardsectorBits.Select(Function(x) x * 2).ToList()
                End If
            End If

            Dim result As New MasterTrack(rotatedBits,
                                          source.TimePerRev,
                                          bitTicks:=rotatedTicks,
                                          splice:=0,
                                          weak:=rotatedWeak,
                                          hardsectorBits:=hardsectorBits)
            result.ForceRandomWeak = source.ForceRandomWeak
            result.Precomp = source.Precomp
            Return result
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration RotateList)
        Private Shared Function RotateList(Of T)(values As IList(Of T), index As Integer) As List(Of T)
            If values Is Nothing OrElse values.Count = 0 Then
                Return New List(Of T)()
            End If
            Dim wrapped = ((index Mod values.Count) + values.Count) Mod values.Count
            Return values.Skip(wrapped).Concat(values.Take(wrapped)).ToList()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ShouldUseDoubleRate)
        Private Shared Function ShouldUseDoubleRate(track As HasFlux, codec As Codec, master As MasterTrack) As Boolean
            If TypeOf track Is Apple2Gcr OrElse TypeOf codec Is Apple2Gcr Then
                Return True
            End If
            If TypeOf codec Is IbmTrackFixed Then
                Dim summary = codec.SummaryString()
                If summary.StartsWith("ibm.fm", StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
                If master IsNot Nothing AndAlso master.Bitrate < 400000.0 Then
                    Return True
                End If
            End If
            Return False
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveNamedOrNumericOption)
        Private Function ResolveNamedOrNumericOption(optionName As String,
                                                     namedValues As Dictionary(Of String, Integer),
                                                     invalidTemplate As String) As Integer
            Dim raw As String = Nothing
            If Not Options.Values.TryGetValue(optionName, raw) Then
                Return &HFF
            End If

            Dim mapped As Integer
            If namedValues.TryGetValue(raw, mapped) Then
                Return mapped
            End If

            Dim parsed As Integer
            If TryParseIntegerLiteral(raw, parsed) Then
                Return parsed And &HFF
            End If

            Throw New FatalException(String.Format(invalidTemplate, raw))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration TryParseIntegerLiteral)
        Private Shared Function TryParseIntegerLiteral(raw As String, ByRef value As Integer) As Boolean
            If String.IsNullOrEmpty(raw) Then
                value = 0
                Return False
            End If

            If raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) Then
                Return Integer.TryParse(raw.Substring(2), Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, value)
            End If

            Return Integer.TryParse(raw, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, value)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetMasterTrackOrNothing)
        Private Function GetMasterTrackOrNothing(cyl As Integer, side As Integer) As MasterTrack
            Dim key = Tuple.Create(cyl, side)
            If _tracks.ContainsKey(key) Then
                Return _tracks(key)
            End If
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeHfeBits)
        Private Shared Function DecodeHfeBits(raw As Byte()) As List(Of Boolean)
            Dim result As New List(Of Boolean)(raw.Length * 8)
            For Each b In raw
                Dim r = ReverseByte(b)
                For bit = 7 To 0 Step -1
                    result.Add(((r >> bit) And 1) <> 0)
                Next
            Next
            Return result
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeHfeBytes)
        Private Shared Function EncodeHfeBytes(bits As IEnumerable(Of Boolean)) As Byte()
            Dim src = bits.ToList()
            If src.Count = 0 Then
                Return Array.Empty(Of Byte)()
            End If
            Dim bytes((src.Count + 7) \ 8 - 1) As Byte
            For i = 0 To src.Count - 1
                If src(i) Then
                    bytes(i \ 8) = CByte(bytes(i \ 8) Or (1 << (7 - (i Mod 8))))
                End If
            Next
            For i = 0 To bytes.Length - 1
                bytes(i) = ReverseByte(bytes(i))
            Next
            Return bytes
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DecodeHfeV3Track)
        Private Shared Function DecodeHfeV3Track(cyl As Integer, side As Integer, raw As Byte()) As MasterTrack
            Dim tdat = raw.Select(Function(b) ReverseByte(b)).ToArray()
            Dim bits As New List(Of Boolean)()
            Dim ticks As New List(Of Integer)()
            Dim weak As New List(Of Tuple(Of Integer, Integer))()
            Dim index As New List(Of Integer)()

            Dim i = 0
            Dim rate = 0
            While i < tdat.Length
                Dim x = CInt(tdat(i))
                i += 1
                Select Case x
                    Case &HF0 ' Nop
                    Case &HF1 ' Index
                        index.Add(bits.Count)
                    Case &HF2 ' Bitrate
                        If i + 1 > tdat.Length Then
                            Exit While
                        End If
                        rate = CInt(tdat(i))
                        i += 1
                    Case &HF3 ' SkipBits
                        ErrorHandling.Check(i + 2 <= tdat.Length,
                                            String.Format("T{0}.{1}: HFEv3: Truncated skipbits opcode", cyl, side))
                        Dim nr = CInt(tdat(i))
                        Dim skipVal = CInt(tdat(i + 1))
                        i += 2
                        ErrorHandling.Check(nr > 0 AndAlso nr < 8,
                                            String.Format("T{0}.{1}: HFEv3: Bad skipbits value: {2}", cyl, side, nr))
                        If skipVal = &HF4 Then
                            AddWeakRange(weak, bits.Count, 8 - nr)
                            skipVal = 0
                        End If
                        ErrorHandling.Check((skipVal << nr) <= 255,
                                            String.Format("T{0}.{1}: HFEv3: Bad skipbits: 0x{2:x2}<<{3} = 0x{4:x4}",
                                                          cyl, side, skipVal, nr, (skipVal << nr)))
                        AppendByteBits(bits, CByte((skipVal << nr) And &HFF))
                        For cut = 1 To nr
                            bits.RemoveAt(bits.Count - 1)
                        Next
                        For t = 1 To (8 - nr)
                            ticks.Add(rate)
                        Next
                    Case &HF4 ' Rand
                        AddWeakRange(weak, bits.Count, 8)
                        AppendByteBits(bits, 0)
                        For t = 1 To 8
                            ticks.Add(rate)
                        Next
                    Case Else
                        ErrorHandling.Check((x And &HF0) <> &HF0,
                                            String.Format("T{0}.{1}: HFEv3: unrecognised opcode {2:x2}", cyl, side, x))
                        AppendByteBits(bits, CByte(x))
                        For t = 1 To 8
                            ticks.Add(rate)
                        Next
                End Select
            End While

            ErrorHandling.Check(rate <> 0, "HFEv3: Bitrate was never set in track")
            For t = 0 To ticks.Count - 1
                If ticks(t) <> 0 Then
                    Exit For
                End If
                ticks(t) = rate
            Next

            Dim hardsectorBits As List(Of Integer) = Nothing
            If index.Count > 1 Then
                hardsectorBits = New List(Of Integer)()
                Dim pos = 0
                For Each x In index.Skip(1)
                    hardsectorBits.Add(x - pos)
                    pos = x
                Next
                If hardsectorBits.Count > 0 Then
                    hardsectorBits(hardsectorBits.Count - 1) += bits.Count - pos
                End If
            End If

            Dim timePerRev = ticks.Select(Function(t) CDbl(t)).Sum() / 36000000.0
            Return New MasterTrack(bits,
                                   timePerRev,
                                   bitTicks:=ticks.Select(Function(t) CDbl(t)).ToList(),
                                   weak:=weak,
                                   hardsectorBits:=hardsectorBits)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AddWeakRange)
        Private Shared Sub AddWeakRange(ranges As List(Of Tuple(Of Integer, Integer)), start As Integer, count As Integer)
            If ranges.Count > 0 Then
                Dim last = ranges(ranges.Count - 1)
                If (last.Item1 + last.Item2) = start Then
                    ranges(ranges.Count - 1) = Tuple.Create(last.Item1, last.Item2 + count)
                    Return
                End If
            End If
            ranges.Add(Tuple.Create(start, count))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration AppendByteBits)
        Private Shared Sub AppendByteBits(bits As List(Of Boolean), value As Byte)
            For bit = 7 To 0 Step -1
                bits.Add(((value >> bit) And 1) <> 0)
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ReverseByte)
        Private Shared Function ReverseByte(value As Byte) As Byte
            Dim x = CInt(value)
            Dim r = ((x And &H1) << 7) Or
                    ((x And &H2) << 5) Or
                    ((x And &H4) << 3) Or
                    ((x And &H8) << 1) Or
                    ((x And &H10) >> 1) Or
                    ((x And &H20) >> 3) Or
                    ((x And &H40) >> 5) Or
                    ((x And &H80) >> 7)
            Return CByte(r)
        End Function

    End Class

    ' Python map: src/greaseweazle/image/hfe.py::HFEOpts
    Public Class HfeOpts
        ' Python map: src/greaseweazle/image/hfe.py::HFEOpts.__init__
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFEOpts.bitrate
        Public Property Bitrate As Integer

        ' Python map: src/greaseweazle/image/hfe.py::HFEOpts.version
        Public Property Version As Integer

        ' Python map: src/greaseweazle/image/hfe.py::HFEOpts.interface
        Public Property [Interface] As Integer

        ' Python map: src/greaseweazle/image/hfe.py::HFEOpts.encoding
        Public Property Encoding As Integer
    End Class

    ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Op
    Public NotInheritable Class Hfev3Op
        Private Sub New()
        End Sub
        Public Const Nop As Byte = &HF0
        Public Const Index As Byte = &HF1
        Public Const Bitrate As Byte = &HF2
        Public Const SkipBits As Byte = &HF3
        Public Const Rand As Byte = &HF4
    End Class

    ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Range
    Public Class Hfev3Range
        Public Property S As Integer
        Public Property N As Integer
        Public Property Val As Double

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Range.__init__
        Public Sub New(s As Integer, n As Integer, Optional val As Double = 0.0)
            Me.S = s
            Me.N = n
            Me.Val = val
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Range.e
        Public ReadOnly Property E As Integer
            Get
                Return S + N
            End Get
        End Property
    End Class

    ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Chunk
    Public Class Hfev3Chunk
        Public Property NBits As Integer
        Public Property TimePerBit As Double
        Public Property IsRandom As Boolean
        Public Property EmitIndex As Boolean

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Chunk.__init__
        Public Sub New(nBits As Integer, timePerBit As Double, isRandom As Boolean, emitIndex As Boolean)
            Me.NBits = nBits
            Me.TimePerBit = timePerBit
            Me.IsRandom = isRandom
            Me.EmitIndex = emitIndex
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Chunk.__str__
        Public Overrides Function ToString() As String
            ' Python's `f'{x:.4f}'` is locale-independent; force InvariantCulture
            ' so non-English Windows hosts don't render `0,5000us`.
            Dim s = String.Format(Globalization.CultureInfo.InvariantCulture,
                                  "{0} bits, {1:F4}us per bit", NBits, TimePerBit * 1000000.0)
            If IsRandom Then s &= ", random"
            Return s
        End Function
    End Class

    ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Generator
    ' Mirrors the Python state machine: walks a MasterTrack and emits HFEv3
    ' opcode chunks (Nop / Index / Bitrate / SkipBits / Rand / data) honouring
    ' weak ranges, hard sectors, and per-cell timing.
    Public Class Hfev3Generator
        Public Property Track As MasterTrack
        Public Property TicksPerRev As Double
        Public Property TimePerTick As Double
        Public Property IndexPositions As List(Of Integer)
        Public Property Ticks As List(Of Hfev3Range)
        Public Property TickIndex As Integer
        Public Property TickCur As Hfev3Range
        Public Property WeakRanges As List(Of Hfev3Range)
        Public Property WeakIndex As Integer
        Public Property WeakCur As Hfev3Range
        Public Property Out As List(Of Byte)
        Public Property Time As Double
        Public Property HfeTime As Double
        Public Property Pos As Integer
        Public Property Rate As Integer
        Public Property RateChangePos As Integer
        Public Property Sec As Integer
        Public Property Chunk As Hfev3Chunk

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Generator.__init__
        Public Sub New(track As MasterTrack)
            Me.Track = track

            Dim bitTicks = track.BitTicks
            If bitTicks IsNot Nothing Then
                TicksPerRev = bitTicks.Sum()
            Else
                TicksPerRev = track.Bits.Count
            End If
            TimePerTick = If(TicksPerRev > 0, track.TimePerRev / TicksPerRev, 0.0)

            ' index_positions: bit positions of each index pulse + final track end
            Dim hsb As List(Of Integer)
            If track.HardsectorBits Is Nothing OrElse track.HardsectorBits.Count = 0 Then
                hsb = New List(Of Integer) From {0}
            Else
                hsb = New List(Of Integer)(track.HardsectorBits)
                hsb(hsb.Count - 1) = hsb(hsb.Count - 1) \ 2
                Dim acc As New List(Of Integer)()
                Dim run As Integer = 0
                acc.Add(0)
                For Each x In hsb
                    run += x
                    acc.Add(run)
                Next
                hsb = acc
            End If
            hsb.Add(track.Bits.Count)
            IndexPositions = hsb

            ' ticks: list of (start, length, val) ranges of identical timing
            Ticks = New List(Of Hfev3Range)()
            If bitTicks Is Nothing Then
                Ticks.Add(New Hfev3Range(0, track.Bits.Count, 1))
            Else
                If bitTicks.Count > 0 Then
                    Dim c = bitTicks(0)
                    Dim start = 0
                    For i = 0 To bitTicks.Count - 1
                        If bitTicks(i) <> c Then
                            Ticks.Add(New Hfev3Range(start, i - start, c))
                            c = bitTicks(i)
                            start = i
                        End If
                    Next
                    Ticks.Add(New Hfev3Range(start, bitTicks.Count - start, c))
                End If
            End If
            ' Sentinel terminator (matches Python's chained final entry).
            Ticks.Add(New Hfev3Range(track.Bits.Count, 0, 0))
            TickIndex = 0
            TickCur = Ticks(0)

            WeakRanges = New List(Of Hfev3Range)()
            If track.WeakRanges IsNot Nothing Then
                For Each w In track.WeakRanges
                    WeakRanges.Add(New Hfev3Range(w.Item1, w.Item2))
                Next
            End If
            WeakRanges.Add(New Hfev3Range(track.Bits.Count, 0))
            WeakIndex = 0
            WeakCur = WeakRanges(0)

            Out = New List(Of Byte)()
            Time = 0.0
            HfeTime = 0.0
            Pos = 0
            Rate = -1
            RateChangePos = 0
            Sec = 0
            Chunk = NextChunk()
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Generator.empty
        Public Shared Function CreateEmpty(timePerRev As Double, bitrate As Double) As Hfev3Generator
            Dim nbits = CInt(Math.Round(2000.0 * bitrate * timePerRev))
            Dim bits = New List(Of Boolean)(nbits)
            For i = 1 To nbits
                bits.Add(False)
            Next
            Dim weak = New List(Of Tuple(Of Integer, Integer)) From {Tuple.Create(0, nbits)}
            Dim mt = New MasterTrack(bits, timePerRev, weak:=weak)
            Return New Hfev3Generator(mt)
        End Function

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Generator.next_chunk
        Public Function NextChunk() As Hfev3Chunk
            If Pos >= Track.Bits.Count Then
                Return Nothing
            End If

            Dim emitIndex As Boolean = False
            Dim n As Integer
            While True
                n = IndexPositions(Sec) - Pos
                If n > 0 Then Exit While
                Sec += 1
                emitIndex = True
            End While

            While Pos >= TickCur.E
                TickIndex += 1
                TickCur = Ticks(TickIndex)
            End While
            n = Math.Min(n, TickCur.E - Pos)

            While Pos >= WeakCur.E
                WeakIndex += 1
                WeakCur = WeakRanges(WeakIndex)
            End While
            Dim isRandom As Boolean
            If Pos < WeakCur.S Then
                n = Math.Min(n, WeakCur.S - Pos)
                isRandom = False
            Else
                n = Math.Min(n, WeakCur.E - Pos)
                isRandom = True
            End If

            Return New Hfev3Chunk(n, TimePerTick * TickCur.Val, isRandom, emitIndex)
        End Function

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Generator.increment_position
        Public Sub IncrementPosition(n As Integer)
            Dim c = Chunk
            Pos += n
            Time += n * c.TimePerBit
            HfeTime += n * Rate / 36000000.0
            c.NBits -= n
            If c.NBits = 0 Then
                Chunk = NextChunk()
            End If
        End Sub

        ' Python map: src/greaseweazle/image/hfe.py::HFEv3_Generator.raw_hfe_bytes
        Public Function RawHfeBytes() As Byte()
            ' Python: bytereverse() on the bitarray-loaded bytes.
            Dim result(Out.Count - 1) As Byte
            For i = 0 To Out.Count - 1
                result(i) = ReverseByte(Out(i))
            Next
            Return result
        End Function

        Private Shared Function ReverseByte(value As Byte) As Byte
            Dim x As Integer = value
            x = (((x And &HF0) >> 4) Or ((x And &HF) << 4)) And &HFF
            x = (((x And &HCC) >> 2) Or ((x And &H33) << 2)) And &HFF
            x = (((x And &HAA) >> 1) Or ((x And &H55) << 1)) And &HFF
            Return CByte(x)
        End Function
    End Class

    Public Module HfeFunctions
        ' Python map: src/greaseweazle/image/hfe.py::hfev3_get_image
        Public Function Hfev3GetImage(image As Hfe) As Byte()
            If image Is Nothing Then
                Return Array.Empty(Of Byte)()
            End If
            Return image.GetImage()
        End Function
    End Module

End Namespace
