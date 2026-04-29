Imports Greaseweazle.Core
Imports System.Text

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/scp.py::SCP
    Public Class Scp
        Inherits Image

        Public Const SampleFrequency As Double = 40000000.0

        Private ReadOnly _tracks As New Dictionary(Of Integer, Flux)()
        ' Python: SCP.nr_revs -- minimum revolutions seen across emitted tracks.
        Private _nrRevs As Integer? = Nothing
        ' Python: SCP.index_cued -- becomes False if any emitted flux is not index-cued.
        Private _indexCued As Boolean = True
        Private Shared ReadOnly DiskTypes As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
            {"c64", &H0},
            {"amiga", &H4},
            {"amigahd", &H8},
            {"atari800-sd", &H10},
            {"atari800-dd", &H11},
            {"atari800-ed", &H12},
            {"atarist-ss", &H14},
            {"atarist-ds", &H15},
            {"appleii", &H20},
            {"appleiipro", &H21},
            {"apple-400k", &H24},
            {"apple-800k", &H25},
            {"apple-1m44", &H26},
            {"ibmpc-360k", &H30},
            {"ibmpc-720k", &H31},
            {"ibmpc-1m2", &H32},
            {"ibmpc-1m44", &H33},
            {"trs80_sssd", &H40},
            {"trs80_ssdd", &H41},
            {"trs80_dssd", &H42},
            {"trs80_dsdd", &H43},
            {"ti-99/4a", &H50},
            {"roland-d20", &H60},
            {"amstrad-cpc", &H70},
            {"other-320k", &H80},
            {"other-1m2", &H81},
            {"other-720k", &H84},
            {"other-1m44", &H85},
            {"tape-gcr1", &HE0},
            {"tape-gcr2", &HE1},
            {"tape-mfm", &HE2},
            {"hdd-mfm", &HF0},
            {"hdd-rll", &HF1}
        }

        ' Python map: src/greaseweazle/image/scp.py::SCP.__init__
        Public Sub New()
            Options.WriteSettings.Add("disktype")
            Options.WriteSettings.Add("legacy_ss")
            Options.WriteSettings.Add("revs")
        End Sub

        ' Python map: src/greaseweazle/image/scp.py::SCP.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 16, "SCP: Header is too short")
            ErrorHandling.Check(data(0) = AscW("S"c) AndAlso data(1) = AscW("C"c) AndAlso data(2) = AscW("P"c),
                                "SCP: Bad signature")

            Dim diskType = CInt(data(4))
            Dim nrRevs = CInt(data(5))
            Dim flagsByte = CInt(data(8))
            Dim singleSided = CInt(data(10))
            Dim storedChecksum As UInteger = BitConverter.ToUInt32(data, 12)
            ErrorHandling.Check(nrRevs >= 0, "SCP: Invalid revolution count")

            ' Python map: src/greaseweazle/image/scp.py::SCP.from_bytes (checksum warning)
            ' Python: if sum(dat[16:]) & 0xffffffff != checksum: print('SCP: WARNING: Bad image checksum')
            Dim computedChecksum As UInteger = 0
            For i = 16 To data.Length - 1
                computedChecksum = CUInt((CULng(computedChecksum) + CULng(data(i))) And &HFFFFFFFFUL)
            Next
            If computedChecksum <> storedChecksum Then
                LibraryDiagnostics.EmitWarning("SCP: WARNING: Bad image checksum")
            End If

            ' Python: index_cued = (flags & 1) == 1 or nr_revs == 1
            Dim indexCued = ((flagsByte And 1) = 1) OrElse nrRevs = 1

            Dim trkOffsets(167) As Integer
            For i = 0 To 167
                trkOffsets(i) = CInt(BitConverter.ToUInt32(data, 16 + i * 4))
            Next

            ' Python truncates the TLUT at the first track-data header (any offset >= 0x2b0).
            ' Some tools generate a short TLUT; clip on first such marker.
            Dim tlutLimit = trkOffsets.Length
            For i = 0 To trkOffsets.Length - 1
                Dim off = trkOffsets(i)
                If off = 0 OrElse off >= &H2B0 Then
                    Continue For
                End If
                Dim sliced = (off \ 4) - 4
                ErrorHandling.Check(sliced >= 0, "SCP: Bad Track Table")
                If sliced < tlutLimit Then
                    tlutLimit = sliced
                End If
            Next

            Dim splices As Integer() = Nothing
            Dim minTrackOffset = trkOffsets.Take(tlutLimit).Where(Function(x) x > 0).DefaultIfEmpty(Integer.MaxValue).Min()
            If data.Length >= &H2B8 Then
                Dim extSig = Encoding.ASCII.GetString(data, &H2B0, 4)
                Dim extLen = CInt(BitConverter.ToUInt32(data, &H2B4))
                If String.Equals(extSig, "EXTS", StringComparison.Ordinal) AndAlso (&H2B8 + extLen) <= minTrackOffset Then
                    Dim pos = &H2B8
                    Dim [end] = &H2B8 + extLen
                    While ([end] - pos) >= 8
                        Dim chunkSig = Encoding.ASCII.GetString(data, pos, 4)
                        Dim chunkLen = CInt(BitConverter.ToUInt32(data, pos + 4))
                        pos += 8
                        If String.Equals(chunkSig, "WRSP", StringComparison.Ordinal) AndAlso chunkLen >= 169 * 4 AndAlso (pos + chunkLen) <= data.Length Then
                            Dim parsed(167) As Integer
                            For i = 0 To 167
                                parsed(i) = CInt(BitConverter.ToUInt32(data, pos + 4 + i * 4))
                            Next
                            splices = parsed
                        End If
                        pos += chunkLen
                    End While
                End If
            End If

            For trackNumber = 0 To tlutLimit - 1
                Dim trackOffset = trkOffsets(trackNumber)
                If trackOffset <= 0 OrElse trackOffset + 4 > data.Length Then
                    Continue For
                End If
                ErrorHandling.Check(data(trackOffset) = AscW("T"c) AndAlso
                                    data(trackOffset + 1) = AscW("R"c) AndAlso
                                    data(trackOffset + 2) = AscW("K"c),
                                    "SCP: Missing track signature")
                ErrorHandling.Check(CInt(data(trackOffset + 3)) = trackNumber, "SCP: Wrong track number in header")

                Dim tdhStart = trackOffset + 4
                Dim tdhLength = nrRevs * 12
                ErrorHandling.Check(tdhStart + tdhLength <= data.Length, "SCP: Truncated track header")

                ' Strip empty trailing revolutions (old versions of FluxEngine).
                Dim revCount = nrRevs
                While revCount > 0
                    Dim tail = tdhStart + (revCount - 1) * 12
                    Dim eTicks = BitConverter.ToUInt32(data, tail)
                    Dim eNr = BitConverter.ToUInt32(data, tail + 4)
                    If eNr <> 0UI AndAlso eTicks <> 0UI Then
                        Exit While
                    End If
                    revCount -= 1
                End While
                If revCount = 0 Then
                    Continue For
                End If

                ' Clip the first revolution if not flagged as index-cued and >1 rev remain.
                Dim firstRev = 0
                If Not indexCued AndAlso revCount > 1 Then
                    firstRev = 1
                End If

                Dim indexList As New List(Of Double)()
                Dim dataStart = Integer.MaxValue
                Dim dataEnd = 0
                For r = firstRev To revCount - 1
                    Dim entryOffset = tdhStart + r * 12
                    Dim ticks = CInt(BitConverter.ToUInt32(data, entryOffset))
                    Dim words = CInt(BitConverter.ToUInt32(data, entryOffset + 4))
                    Dim relOffset = CInt(BitConverter.ToUInt32(data, entryOffset + 8))
                    If ticks = 0 OrElse words = 0 Then
                        Continue For
                    End If
                    indexList.Add(ticks)
                    dataStart = Math.Min(dataStart, relOffset)
                    dataEnd = Math.Max(dataEnd, relOffset + words * 2)
                Next

                If indexList.Count = 0 OrElse dataStart = Integer.MaxValue Then
                    Continue For
                End If

                ' FluxEngine creates dummy TDHs for empty tracks - skip if start == end.
                If dataStart = dataEnd Then
                    Continue For
                End If

                Dim fluxAbsStart = trackOffset + dataStart
                Dim fluxAbsEnd = trackOffset + dataEnd
                ErrorHandling.Check(fluxAbsStart >= 0 AndAlso fluxAbsEnd <= data.Length AndAlso fluxAbsEnd >= fluxAbsStart,
                                    "SCP: Track data is out of bounds")

                Dim fluxList As New List(Of Double)()
                Dim carry As Integer = 0
                Dim p = fluxAbsStart
                While p + 1 < fluxAbsEnd
                    Dim x = CInt(data(p)) * 256 + CInt(data(p + 1))
                    If x = 0 Then
                        carry += 65536
                    Else
                        fluxList.Add(carry + x)
                        carry = 0
                    End If
                    p += 2
                End While

                Dim flux = New Flux(indexList, fluxList, SampleFrequency, indexCued:=indexCued)
                If splices IsNot Nothing AndAlso trackNumber < splices.Length AndAlso splices(trackNumber) <> 0 Then
                    flux.Splice = splices(trackNumber)
                End If
                _tracks(trackNumber) = flux
            Next

            ' Python: SCP single-sided fixups (C64 halftrack heuristic + legacy SS remap).
            Dim sideCounts As Integer() = New Integer() {0, 0}
            For Each tnr In _tracks.Keys
                sideCounts(tnr And 1) += 1
            Next
            If singleSided = 0 AndAlso diskType = 0 AndAlso sideCounts(1) > 0 _
                AndAlso sideCounts(0) = sideCounts(1) + 1 AndAlso sideCounts(0) < 42 Then
                singleSided = 1
                LibraryDiagnostics.EmitInfo("SCP: Importing C64 image with halftracks")
            End If
            If singleSided <> 0 AndAlso sideCounts(0) > 0 AndAlso sideCounts(1) > 0 Then
                Dim remap As New Dictionary(Of Integer, Flux)()
                For Each kvp In _tracks
                    remap(kvp.Key * 2 + singleSided - 1) = kvp.Value
                Next
                _tracks.Clear()
                For Each kvp In remap
                    _tracks(kvp.Key) = kvp.Value
                Next
                LibraryDiagnostics.EmitInfo("SCP: Imported legacy single-sided image")
            End If
        End Sub

        ' Python map: src/greaseweazle/image/scp.py::SCP.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = cyl * 2 + side
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/image/scp.py::SCP.emit_track
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Dim optsRevs As Integer? = ResolveOptsRevs()
            Dim flux As Flux
            If TypeOf track Is Greaseweazle.Codecs.Codec Then
                Dim mt = DirectCast(track, Greaseweazle.Codecs.Codec).MasterTrack()
                ' Python: mt_revs = 2 if self.opts.revs is None else self.opts.revs
                Dim mtRevs = If(optsRevs.HasValue, optsRevs.Value, 2)
                flux = mt.Flux(mtRevs)
            ElseIf TypeOf track Is MasterTrack Then
                Dim mtRevs = If(optsRevs.HasValue, optsRevs.Value, 2)
                flux = DirectCast(track, MasterTrack).Flux(mtRevs)
            Else
                flux = track.Flux()
            End If

            flux.CueAtIndex()

            If optsRevs.HasValue Then
                flux.SetNrRevs(optsRevs.Value)
            End If

            If Not flux.IndexCued Then
                _indexCued = False
            End If

            ' Python: nr_revs = len(flux.index_list); track minimum across all emit calls.
            Dim revsHere = flux.IndexList.Count
            If Not _nrRevs.HasValue Then
                _nrRevs = revsHere
            Else
                _nrRevs = Math.Min(_nrRevs.Value, revsHere)
            End If

            _tracks(cyl * 2 + side) = flux
        End Sub

        ' Python map: src/greaseweazle/image/scp.py::(no direct 1:1 symbol; VB helper to resolve SCP.opts.revs)
        Private Function ResolveOptsRevs() As Integer?
            Dim raw As String = Nothing
            If Not Options.Values.TryGetValue("revs", raw) OrElse String.IsNullOrEmpty(raw) Then
                Return Nothing
            End If
            Dim parsed As Integer
            If Not Integer.TryParse(raw, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 1 Then
                Throw New FatalException(String.Format("Kryoflux: Invalid revs: '{0}'", raw))
            End If
            Return parsed
        End Function

        ' Python map: src/greaseweazle/image/scp.py::SCP.get_image
        Public Overrides Function GetImage() As Byte()
            Dim singleSided = ComputeSingleSidedCode()
            Dim useLegacySingleSided = ResolveLegacySingleSided(singleSided)
            Dim diskType = ResolveDiskType()
            Dim tracksToWrite = _tracks
            If useLegacySingleSided Then
                tracksToWrite = New Dictionary(Of Integer, Flux)()
                For Each kvp In _tracks
                    tracksToWrite(kvp.Key \ 2) = kvp.Value
                Next
            End If

            Dim maxTrack = If(tracksToWrite.Count = 0, 0, tracksToWrite.Keys.Max())
            Dim ntracks = maxTrack + 1
            Dim outputRevs = ResolveOutputRevolutionCount()

            Dim tlut As New List(Of Byte)()
            Dim wrsp As New List(Of Byte)()
            Dim trackData As New List(Of Byte)()
            Dim wrspLen = 0
            If tracksToWrite.Values.Any(Function(f) f.Splice.HasValue) Then
                wrspLen = (2 + 2 + 169) * 4
                wrsp.AddRange(Encoding.ASCII.GetBytes("EXTS"))
                wrsp.AddRange(BitConverter.GetBytes(CUInt(wrspLen - 8)))
                wrsp.AddRange(Encoding.ASCII.GetBytes("WRSP"))
                wrsp.AddRange(BitConverter.GetBytes(CUInt(wrspLen - 16)))
                wrsp.AddRange(BitConverter.GetBytes(CUInt(0))) ' WRSP flags
            End If
            Dim trackStart = 16 + &H2A0 + wrspLen

            For trackNumber = 0 To ntracks - 1
                If tracksToWrite.ContainsKey(trackNumber) Then
                    tlut.AddRange(BitConverter.GetBytes(CUInt(trackStart + trackData.Count)))
                    Dim encoded = EncodeTrack(trackNumber, tracksToWrite(trackNumber), outputRevs)
                    trackData.AddRange(encoded)
                    If wrspLen <> 0 Then
                        Dim splice = 0UI
                        Dim trackFlux = tracksToWrite(trackNumber)
                        If trackFlux.Splice.HasValue Then
                            ' Python: splice = round(flux.splice * factor)
                            ' factor = SCP.sample_freq / flux.sample_freq
                            Dim factor = SampleFrequency / trackFlux.SampleFreq
                            splice = CUInt(Math.Max(0, CInt(Math.Round(trackFlux.Splice.Value * factor, MidpointRounding.ToEven))))
                        End If
                        wrsp.AddRange(BitConverter.GetBytes(splice))
                    End If
                Else
                    tlut.AddRange(BitConverter.GetBytes(CUInt(0)))
                    If wrspLen <> 0 Then
                        wrsp.AddRange(BitConverter.GetBytes(CUInt(0)))
                    End If
                End If
            Next

            While tlut.Count < &H2A0
                tlut.Add(0)
            End While
            ErrorHandling.Check(tlut.Count = &H2A0, "SCP: Too many tracks")
            If wrspLen <> 0 Then
                While wrsp.Count < wrspLen
                    wrsp.Add(0)
                End While
            End If

            Dim footerOffset = 16 + &H2A0 + wrspLen + trackData.Count
            Dim footer As New List(Of Byte)()
            ' Python: app_name = f'Greaseweazle {__version__}'.encode().
            ' Pull the version from AssemblyInformationalVersionAttribute so
            ' the app_name length and contents track Python's `__version__`
            ' verbatim instead of being hard-coded to a stale "0.0".
            Dim appName = Encoding.ASCII.GetBytes("Greaseweazle " & Greaseweazle.Core.HostVersion.Value)
            footer.AddRange(BitConverter.GetBytes(CUShort(appName.Length)))
            footer.AddRange(appName)
            footer.Add(0)

            ' Python: creation_time = round(time.time()); same value used for
            ' both creation and modification timestamps. Mirror that here so
            ' the SCP footer carries a real timestamp instead of zeros.
            Dim creationTime As ULong = CULng(Math.Round((DateTime.UtcNow - New DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds))

            footer.AddRange(BitConverter.GetBytes(CUInt(0))) ' drive manufacturer
            footer.AddRange(BitConverter.GetBytes(CUInt(0))) ' drive model
            footer.AddRange(BitConverter.GetBytes(CUInt(0))) ' drive serial
            footer.AddRange(BitConverter.GetBytes(CUInt(0))) ' creator name
            footer.AddRange(BitConverter.GetBytes(CUInt(footerOffset))) ' application name offset
            footer.AddRange(BitConverter.GetBytes(CUInt(0))) ' comments
            footer.AddRange(BitConverter.GetBytes(creationTime)) ' creation time
            footer.AddRange(BitConverter.GetBytes(creationTime)) ' modification time
            footer.Add(0) ' application version
            footer.Add(0) ' hardware version
            footer.Add(0) ' firmware version
            footer.Add(&H24) ' format version
            footer.Add(CByte(AscW("F"c)))
            footer.Add(CByte(AscW("P"c)))
            footer.Add(CByte(AscW("C"c)))
            footer.Add(CByte(AscW("S"c)))

            Dim payload = tlut.Concat(wrsp).Concat(trackData).Concat(footer).ToArray()
            Dim checksum As UInteger = CUInt(payload.Aggregate(0UI, Function(acc, b) acc + b))

            Dim flags As Byte = &H2 ' 96 TPI
            ' Python: SCP_FLAG_INDEXED is only set when self.index_cued (i.e. all
            ' emitted tracks reported flux.index_cued=True).
            If _indexCued Then
                flags = CByte(flags Or 1)
            End If
            flags = CByte(flags Or &H20) ' footer present
            Dim header As New List(Of Byte)()
            header.AddRange(New Byte() {
                CByte(AscW("S"c)), CByte(AscW("C"c)), CByte(AscW("P"c)),
                0,          ' version
                CByte(diskType And &HFF),
                CByte(outputRevs),
                0,          ' start track
                CByte(Math.Max(0, ntracks - 1)),
                flags,
                0,          ' cell width
                CByte(singleSided),
                0           ' 25ns capture
            })
            header.AddRange(BitConverter.GetBytes(checksum))

            Return header.Concat(payload).ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveLegacySingleSided)
        Private Function ResolveLegacySingleSided(singleSided As Integer) As Boolean
            If singleSided = 0 Then
                Return False
            End If
            Dim legacyRaw As String = Nothing
            Return Options.Values.TryGetValue("legacy_ss", legacyRaw)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDiskType)
        Private Function ResolveDiskType() As Integer
            Dim raw As String = Nothing
            If Not Options.Values.TryGetValue("disktype", raw) Then
                Return &H80
            End If
            If String.IsNullOrWhiteSpace(raw) Then
                Throw New FatalException("Bad SCP disktype: ''")
            End If

            Dim mapped As Integer
            If DiskTypes.TryGetValue(raw, mapped) Then
                Return mapped
            End If

            Dim parsed As Integer
            If raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) Then
                If Integer.TryParse(raw.Substring(2), Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, parsed) Then
                    Return parsed And &HFF
                End If
            ElseIf Integer.TryParse(raw, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) Then
                Return parsed And &HFF
            End If

            Throw New FatalException(String.Format("Bad SCP disktype: '{0}'", raw))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveOutputRevolutionCount)
        Private Function ResolveOutputRevolutionCount() As Integer
            ' Python: header uses self.nr_revs which is updated incrementally on emit.
            If _nrRevs.HasValue AndAlso _nrRevs.Value > 0 Then
                Return _nrRevs.Value
            End If
            Dim configuredRevs As String = Nothing
            If Options.Values.TryGetValue("revs", configuredRevs) Then
                Dim parsed As Integer
                ErrorHandling.Check(Integer.TryParse(configuredRevs, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) AndAlso parsed >= 1,
                                    String.Format("SCP: Invalid revs: '{0}'", configuredRevs))
                Return parsed
            End If
            Dim counts = _tracks.Values.Select(Function(f) f.IndexList.Count).Where(Function(c) c > 0).ToList()
            If counts.Count = 0 Then
                Return 1
            End If
            Return counts.Min()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeSingleSidedCode)
        Private Function ComputeSingleSidedCode() As Integer
            Dim side0 = _tracks.Keys.Where(Function(k) (k And 1) = 0).Count()
            Dim side1 = _tracks.Keys.Where(Function(k) (k And 1) = 1).Count()
            If side0 > 0 AndAlso side1 > 0 Then
                Return 0
            End If
            If side0 > 0 Then
                Return 1
            End If
            Return 2
        End Function

        ' Python map: src/greaseweazle/image/scp.py::SCP.side_count
        Public ReadOnly Property SideCount As Integer
            Get
                Dim code = ComputeSingleSidedCode()
                If code = 0 Then
                    Return 2
                End If
                Return 1
            End Get
        End Property

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EncodeTrack)
        Private Function EncodeTrack(trackNumber As Integer, flux As Flux, outputRevs As Integer) As IEnumerable(Of Byte)
            Dim indexList = flux.IndexList.Take(outputRevs).ToList()
            ErrorHandling.Check(indexList.Count = outputRevs, "SCP: Track does not have enough revolutions")

            Dim scale = SampleFrequency / flux.SampleFreq
            Dim tdh As New List(Of Byte)()
            Dim dat As New List(Of Byte)()
            Dim rev = 0
            Dim toIndex = indexList(0)
            Dim lenAtIndex = 0
            Dim remainder = 0.0

            For Each raw In flux.List
                While toIndex < raw AndAlso rev < outputRevs
                    Dim ticks = CUInt(Math.Round(indexList(rev) * scale))
                    Dim words = CUInt((dat.Count - lenAtIndex) \ 2)
                    Dim relOffset = CUInt(4 + outputRevs * 12 + lenAtIndex)
                    tdh.AddRange(BitConverter.GetBytes(ticks))
                    tdh.AddRange(BitConverter.GetBytes(words))
                    tdh.AddRange(BitConverter.GetBytes(relOffset))
                    lenAtIndex = dat.Count
                    rev += 1
                    If rev >= outputRevs Then
                        Exit While
                    End If
                    toIndex += indexList(rev)
                End While
                If rev >= outputRevs Then
                    Exit For
                End If

                toIndex -= raw
                Dim y = raw * scale + remainder
                Dim value = CInt(Math.Round(y, MidpointRounding.ToEven))
                If value <= 0 Then
                    Continue For
                End If
                If (value And &HFFFF) = 0 Then
                    value += 1
                End If
                remainder = y - value

                While value >= 65536
                    dat.Add(0)
                    dat.Add(0)
                    value -= 65536
                End While
                dat.Add(CByte((value >> 8) And &HFF))
                dat.Add(CByte(value And &HFF))
            Next

            While rev < outputRevs
                Dim ticks = CUInt(Math.Round(indexList(rev) * scale))
                Dim words = CUInt((dat.Count - lenAtIndex) \ 2)
                Dim relOffset = CUInt(4 + outputRevs * 12 + lenAtIndex)
                tdh.AddRange(BitConverter.GetBytes(ticks))
                tdh.AddRange(BitConverter.GetBytes(words))
                tdh.AddRange(BitConverter.GetBytes(relOffset))
                lenAtIndex = dat.Count
                rev += 1
            End While

            Dim output As New List(Of Byte)()
            output.Add(CByte(AscW("T"c)))
            output.Add(CByte(AscW("R"c)))
            output.Add(CByte(AscW("K"c)))
            output.Add(CByte(trackNumber))
            output.AddRange(tdh)
            output.AddRange(dat)
            Return output
        End Function

    End Class

    ' Python map: src/greaseweazle/image/scp.py::SCPHeaderFlags
    <Flags>
    Public Enum ScpHeaderFlags As Byte
        Indexed = 1
        Tpi96 = 2
        FooterPresent = &H20
    End Enum

    ' Python map: src/greaseweazle/image/scp.py::SCPOpts
    Public Class ScpOpts
        ' Python map: src/greaseweazle/image/scp.py::SCPOpts.__init__
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/image/scp.py::SCPOpts.disktype
        Public Property Disktype As String

        ' Python map: src/greaseweazle/image/scp.py::SCPOpts.revs
        Public Property Revs As Integer
    End Class

    ' Python map: src/greaseweazle/image/scp.py::SCPTrack
    Public Class ScpTrack
        Public ReadOnly Property FluxData As Flux

        ' Python map: src/greaseweazle/image/scp.py::SCPTrack.__init__
        Public Sub New(flux As Flux)
            FluxData = flux
        End Sub
    End Class

End Namespace
