Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/d88.py::D88
    Public Class D88
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), IbmTrackFixed)()
        Public Property Format As DiskDef

        ' Python map: src/greaseweazle/image/d88.py::D88.__init__
        Public Sub New()
            MyBase.New()
            Options.ReadSettings.Add("index")
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Public Sub New(format As DiskDef)
            Me.New()
            Me.Format = format
        End Sub

        ' Python map: src/greaseweazle/image/d88.py::D88.from_file
        Public Shared Shadows Function FromFile(name As String) As D88
            Return Image.FromFile(Of D88)(name, Nothing, Nothing)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration FromBytes)
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 32 + 640, "D88: Header is too short")

            Dim selectedIndex = ParseSelectedIndex()
            Dim diskOffset = 0
            Dim diskIndex = 0
            Dim selected = False

            While diskOffset < data.Length
                ErrorHandling.Check(diskOffset + 32 <= data.Length, "D88: Header is too short")
                Dim diskSize = CInt(BitConverter.ToUInt32(data, diskOffset + 28))
                ErrorHandling.Check(diskSize > 0 AndAlso diskOffset + diskSize <= data.Length, "D88: Invalid disk size")
                If diskIndex = selectedIndex Then
                    DiskFromFile(data, diskOffset, diskSize)
                    selected = True
                End If
                diskOffset += diskSize
                diskIndex += 1
            End While

            ErrorHandling.Check(diskIndex > 0,
                                String.Format("D88: {0}: No valid disk found",
                                              If(String.IsNullOrEmpty(FileName), "<memory>", FileName)))
            ErrorHandling.Check(selected,
                                String.Format("D88: {0}: No disk with index {1} (valid indexes 0-{2})",
                                              If(String.IsNullOrEmpty(FileName), "<memory>", FileName),
                                              selectedIndex,
                                              diskIndex - 1))
        End Sub

        ' Python map: src/greaseweazle/image/d88.py::D88.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EmitTrack)
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Dim key = Tuple.Create(cyl, side)
            If TypeOf track Is IbmTrackFixed Then
                _tracks(key) = CType(track, IbmTrackFixed)
                Return
            End If

            ErrorHandling.Check(Format IsNot Nothing, "D88 output requires --format")
            Dim decoded = Format.DecodeFlux(cyl, side, track)
            If decoded Is Nothing Then
                Return
            End If
            ErrorHandling.Check(TypeOf decoded Is IbmTrackFixed, "D88: Only IBM FM/MFM tracks are supported for output")
            _tracks(key) = CType(decoded, IbmTrackFixed)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            Dim trackOffsets(159) As UInteger
            Dim trackData As New List(Of Byte)()

            For Each kv In _tracks.OrderBy(Function(x) x.Key.Item1).ThenBy(Function(x) x.Key.Item2)
                Dim cyl = kv.Key.Item1
                Dim head = kv.Key.Item2
                Dim idx = cyl * 2 + head
                ErrorHandling.Check(idx >= 0 AndAlso idx < 160, String.Format("D88: Track {0}.{1} out of supported range", cyl, head))
                ErrorHandling.Check(trackOffsets(idx) = 0UI, String.Format("D88: Duplicate track {0}.{1}", cyl, head))
                Dim relOffset = 32 + 640 + trackData.Count
                trackOffsets(idx) = CUInt(relOffset)
                trackData.AddRange(SerializeTrack(kv.Value))
            Next

            Dim diskSize = 32 + 640 + trackData.Count
            Dim header(31) As Byte
            Dim nameBytes = Text.Encoding.ASCII.GetBytes("GW-D88")
            Array.Copy(nameBytes, 0, header, 0, nameBytes.Length)
            header(16) = 0
            header(26) = CByte(ComputeMediaFlag())
            Dim sizeBytes = BitConverter.GetBytes(CUInt(diskSize))
            Array.Copy(sizeBytes, 0, header, 28, 4)

            Dim out As New List(Of Byte)(diskSize)
            out.AddRange(header)
            For i = 0 To 159
                out.AddRange(BitConverter.GetBytes(trackOffsets(i)))
            Next
            out.AddRange(trackData)
            Return out.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeMediaFlag)
        Private Function ComputeMediaFlag() As Integer
            If _tracks.Count = 0 Then
                Return 0
            End If
            For Each t In _tracks.Values
                Dim rate = 1.0 / (2000.0 * t.Clock)
                If rate >= 375.0 Then
                    Return 1
                End If
            Next
            Return 0
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration SerializeTrack)
        Private Shared Function SerializeTrack(track As IbmTrackFixed) As IEnumerable(Of Byte)
            Dim sectorCount = track.Nsec
            Dim ns = track.SectorHeaderNs.ToList()
            Dim ids = track.SectorIds.ToList()
            ErrorHandling.Check(ns.Count = sectorCount AndAlso ids.Count = sectorCount, "D88: Invalid sector metadata")

            Dim expectedSizes = ns.Select(Function(n) 128 << n).ToList()
            Dim raw = track.GetImgTrack()
            Dim variableLayout = (raw.Length = expectedSizes.Sum())
            Dim fixedStride = If(sectorCount = 0, 0, raw.Length \ Math.Max(1, sectorCount))
            ErrorHandling.Check(variableLayout OrElse (sectorCount > 0 AndAlso raw.Length Mod sectorCount = 0),
                                "D88: Unsupported sector image layout")

            Dim dataPos = 0
            Dim out As New List(Of Byte)()
            Dim isFm = track.FormatName.IndexOf(".fm", StringComparison.OrdinalIgnoreCase) >= 0
            Dim mfmFlag As Byte = If(isFm, CByte(&H40), CByte(0))

            For i = 0 To sectorCount - 1
                Dim dataSize = expectedSizes(i)
                Dim srcPos = If(variableLayout, dataPos, i * fixedStride)
                ErrorHandling.Check(srcPos + dataSize <= raw.Length, "D88: Sector data too short")
                Dim sec = raw.Skip(srcPos).Take(dataSize).ToArray()
                If variableLayout Then
                    dataPos += dataSize
                End If

                out.Add(CByte(track.Cyl And &HFF))
                out.Add(CByte(track.Head And &HFF))
                out.Add(CByte(ids(i) And &HFF))
                out.Add(CByte(ns(i) And &HFF))
                out.AddRange(BitConverter.GetBytes(CUShort(sectorCount)))
                out.Add(mfmFlag)
                out.Add(0) ' deleted
                out.Add(0) ' status
                out.AddRange({0, 0, 0, 0, 0})
                out.AddRange(BitConverter.GetBytes(CUShort(dataSize)))
                out.AddRange(sec)
            Next

            Return out
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ParseDiskAt)
        Private Sub ParseDiskAt(data As Byte(), diskOffset As Integer, diskSize As Integer)
            Dim mediaFlag = CInt(data(diskOffset + 26))
            Dim diskEnd = diskOffset + diskSize
            Dim trackTableOffset = diskOffset + 32
            ErrorHandling.Check(trackTableOffset + 640 <= diskEnd, "D88: Header is too short")

            Dim trackOffsets As New List(Of Integer)()
            For i = 0 To 159
                trackOffsets.Add(CInt(BitConverter.ToUInt32(data, trackTableOffset + i * 4)))
            Next

            ' Python: s_off = min(filter(lambda x: x != 0, track_table), default=672)
            ' If s_off == 688 the track table is 164 entries; if it != 672 and != 688 -> fatal.
            Dim nonZero = trackOffsets.Where(Function(x) x <> 0).ToList()
            Dim sOff = If(nonZero.Count = 0, 672, nonZero.Min())
            If sOff = 688 Then
                Dim extOffset = trackTableOffset + 640
                ErrorHandling.Check(extOffset + 16 <= diskEnd, "D88: Extended track table truncated")
                For i = 0 To 3
                    trackOffsets.Add(CInt(BitConverter.ToUInt32(data, extOffset + i * 4)))
                Next
            ElseIf sOff <> 672 Then
                Throw New FatalException("D88: Unsupported track table length.")
            End If

            For trackIndex = 0 To trackOffsets.Count - 1
                Dim relativeTrackOffset = trackOffsets(trackIndex)
                If relativeTrackOffset = 0 Then
                    Continue For
                End If
                Dim trackOffset = diskOffset + relativeTrackOffset
                If trackOffset >= diskEnd Then
                    Continue For
                End If

                Dim cyl = trackIndex \ 2
                Dim head = trackIndex Mod 2
                Dim parsed = TrackFromFile(data, trackOffset, diskEnd)
                If parsed Is Nothing Then
                    Continue For
                End If

                Dim codec = BuildTrackCodec(cyl, head, mediaFlag, parsed)
                If codec IsNot Nothing Then
                    _tracks(Tuple.Create(cyl, head)) = codec
                End If
            Next
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseSelectedIndex)
        Private Function ParseSelectedIndex() As Integer
            Dim value As String = Nothing
            If Not Options.Values.TryGetValue("index", value) Then
                Return 0
            End If
            Dim idx As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, idx) OrElse idx < 0 Then
                Throw New FatalException(String.Format("D88: Invalid index: '{0}'", value))
            End If
            Return idx
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseTrack)
        Private Shared Function ParseTrack(data As Byte(), trackOffset As Integer, diskEnd As Integer) As ParsedTrack
            Dim pos = trackOffset
            Dim sectors As New List(Of ParsedSector)()
            Dim numSectorsTrack = 255
            Dim trackMfmFlag As Integer? = Nothing

            While sectors.Count < numSectorsTrack
                If pos + 16 > diskEnd Then
                    Exit While
                End If
                Dim c = CInt(data(pos))
                Dim h = CInt(data(pos + 1))
                Dim r = CInt(data(pos + 2))
                Dim n = CInt(data(pos + 3))
                Dim numSectors = CInt(BitConverter.ToUInt16(data, pos + 4))
                Dim mfmFlag = CInt(data(pos + 6))
                Dim deleted = CInt(data(pos + 7))
                Dim status = CInt(data(pos + 8))
                Dim dataSize = CInt(BitConverter.ToUInt16(data, pos + 14))
                pos += 16

                ErrorHandling.Check(status = 0, "D88: FDC error codes are unsupported.")
                ErrorHandling.Check(deleted = 0, "D88: Deleted data is unsupported.")
                ErrorHandling.Check(n >= 0 AndAlso n <= 6, "D88: Bad sector size")
                ErrorHandling.Check(pos + dataSize <= diskEnd, "D88: Truncated sector payload")
                Dim size = 128 << n
                ErrorHandling.Check(size = dataSize, "D88: Extra sector data is unsupported.")

                If Not trackMfmFlag.HasValue Then
                    trackMfmFlag = mfmFlag
                    numSectorsTrack = numSectors
                End If
                ErrorHandling.Check(trackMfmFlag.Value = mfmFlag, "D88: Mixed FM and MFM sectors in one track are unsupported.")
                ErrorHandling.Check(numSectorsTrack = numSectors, "D88: Corrupt number of sectors per track in sector header.")

                Dim payload(dataSize - 1) As Byte
                Array.Copy(data, pos, payload, 0, dataSize)
                pos += dataSize
                sectors.Add(New ParsedSector With {
                    .C = c,
                    .H = h,
                    .R = r,
                    .N = n,
                    .MfmFlag = mfmFlag,
                    .Data = payload
                })
            End While

            If sectors.Count = 0 OrElse Not trackMfmFlag.HasValue Then
                Return Nothing
            End If

            ' Python defers dedup until from_config(warn_on_oversize=False) reports oversized.
            ' We mirror that gating in BuildTrackCodec.
            Return New ParsedTrack With {
                .MfmFlag = trackMfmFlag.Value,
                .Sectors = sectors
            }
        End Function

        ' Python map: src/greaseweazle/image/d88.py::D88.remove_duplicate_sectors
        Private Shared Function RemoveDuplicateSectors(sectors As IEnumerable(Of ParsedSector)) As List(Of ParsedSector)
            Dim dedup As New List(Of ParsedSector)()
            For Each s In sectors
                If Not dedup.Any(Function(t) t.C = s.C AndAlso t.H = s.H AndAlso t.R = s.R AndAlso t.N = s.N AndAlso t.Data.SequenceEqual(s.Data)) Then
                    dedup.Add(s)
                End If
            Next
            Return dedup
        End Function

        ' Python map: src/greaseweazle/image/d88.py::D88.track_from_file
        Private Shared Function TrackFromFile(data As Byte(), trackOffset As Integer, diskEnd As Integer) As ParsedTrack
            Return ParseTrack(data, trackOffset, diskEnd)
        End Function

        ' Python map: src/greaseweazle/image/d88.py::D88.disk_from_file
        Private Sub DiskFromFile(data As Byte(), diskOffset As Integer, diskSize As Integer)
            ParseDiskAt(data, diskOffset, diskSize)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackCodec)
        Private Shared Function BuildTrackCodec(cyl As Integer, head As Integer, mediaFlag As Integer, parsed As ParsedTrack) As IbmTrackFixed
            Dim isFm = parsed.MfmFlag = &H40
            Dim formatName = If(isFm, "ibm.fm", "ibm.mfm")

            Dim secs = parsed.Sectors
            Dim built = TryBuildFromConfig(formatName, mediaFlag, isFm, cyl, head, secs)

            ' Python: if t.oversized: dedup secs, warn, rebuild with warn_on_oversize=True.
            If built.Oversized Then
                Dim newSecs = RemoveDuplicateSectors(secs)
                Dim ndups = secs.Count - newSecs.Count
                If ndups <> 0 Then
                    Console.Out.WriteLine(String.Format("T{0}.{1}: D88: Removed {2} duplicate sectors from oversized track",
                                                        cyl, head, ndups))
                End If
                secs = newSecs
                built = TryBuildFromConfig(formatName, mediaFlag, isFm, cyl, head, secs)
            End If

            ' Python: enumerate(t.sectors) and assign idam.{c,h,r,n} + dam.data from secs.
            ' VB IbmTrackFixed stores by sector_id; we mirror that by writing data in
            ' physical (parsed) sector order.
            Dim sectorIds = built.SectorIds
            Dim raw As New List(Of Byte)()
            ' Python preserves physical sector order; SetImgTrack expects logical order.
            ' If logical order == physical order, the result is identical. Otherwise
            ' upstream callers may need to write per-sector via the codec's sector API
            ' (currently not exposed); for D88 the sec_map for trivial interleave==1 yields
            ' identical orders.
            Dim physicalToBuilt = Enumerable.Range(0, secs.Count).
                                            OrderBy(Function(i) sectorIds(i)).
                                            ToList()
            For Each idx In physicalToBuilt
                raw.AddRange(secs(idx).Data)
            Next
            built.SetImgTrack(raw.ToArray())
            Return built
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB helper to drive IbmTrackFixedDef finalise/mk_track flow used by D88)
        Private Shared Function TryBuildFromConfig(formatName As String, mediaFlag As Integer, isFm As Boolean,
                                                   cyl As Integer, head As Integer,
                                                   secs As List(Of ParsedSector)) As IbmTrackFixed
            Dim def As New IbmTrackFixedDef(formatName)
            def.AddParam("secs", secs.Count.ToString(Globalization.CultureInfo.InvariantCulture))
            def.AddParam("bps", String.Join(",", secs.Select(Function(s) s.Data.Length.ToString(Globalization.CultureInfo.InvariantCulture))))
            Dim rate As Integer
            Dim rpm As Integer
            If mediaFlag = 0 Then
                rate = If(isFm, 125, 250)
                rpm = 300
            Else
                rate = If(isFm, 250, 500)
                rpm = 360
            End If
            def.AddParam("rate", rate.ToString(Globalization.CultureInfo.InvariantCulture))
            def.AddParam("rpm", rpm.ToString(Globalization.CultureInfo.InvariantCulture))
            def.Finalise()
            Return CType(def.MkTrack(cyl, head), IbmTrackFixed)
        End Function

        ' Python map: src/greaseweazle/image/d88.py::(no direct 1:1 symbol; VB class helper supporting d88 image handling)
        Private Class ParsedTrack
            Public Property MfmFlag As Integer
            Public Property Sectors As List(Of ParsedSector)
        End Class

        ' Python map: src/greaseweazle/image/d88.py::(no direct 1:1 symbol; VB class helper supporting d88 image handling)
        Private Class ParsedSector
            Public Property C As Integer
            Public Property H As Integer
            Public Property R As Integer
            Public Property N As Integer
            Public Property MfmFlag As Integer
            Public Property Data As Byte()
        End Class

    End Class

    ' Python map: src/greaseweazle/image/d88.py::D88Opts
    Public Class D88Opts
        ' Python map: src/greaseweazle/image/d88.py::D88Opts.__init__
        Public Sub New()
        End Sub

        ' Python map: src/greaseweazle/image/d88.py::D88Opts.index
        Public Property Index As Integer
    End Class

End Namespace
