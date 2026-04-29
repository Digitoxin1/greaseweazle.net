Imports Greaseweazle.Codecs
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/imd.py::IMDMode
    Public Enum ImdMode
        Fm500 = 0
        Fm300 = 1
        Fm250 = 2
        Mfm500 = 3
        Mfm300 = 4
        Mfm250 = 5
    End Enum

    ' Python map: src/greaseweazle/image/imd.py::IMD
    Public Class Imd
        Inherits Image

        Private ReadOnly _tracks As New Dictionary(Of Tuple(Of Integer, Integer), IbmTrackFixed)()
        ' Python: IMD writer comment carries 'IMD 1.17: dd/mm/YYYY HH:MM:SS\r\nGreaseweazle <ver>\r\n\x1a'.
        ' Pulled from the shared HostVersion helper so the IMD comment header
        ' tracks Python's `__version__` (e.g. "1.23") instead of the
        ' four-part AssemblyName.Version ("1.23.0.0").
        Private Shared ReadOnly ImdHostVersion As String = Greaseweazle.Core.HostVersion.Value

        ' Python map: src/greaseweazle/image/imd.py::IMD.__init__
        Public Sub New()
            MyBase.New()
            Me.ReadOnly = False
        End Sub

        ' Python map: src/greaseweazle/image/imd.py::IMD.from_bytes
        Public Overrides Sub FromBytes(data As Byte())
            _tracks.Clear()
            ErrorHandling.Check(data IsNot Nothing AndAlso data.Length >= 5, "Unrecognised IMD file: bad signature")
            ErrorHandling.Check(data(0) = AscW("I"c) AndAlso data(1) = AscW("M"c) AndAlso data(2) = AscW("D"c) AndAlso data(3) = AscW(" "c),
                                "Unrecognised IMD file: bad signature")

            Dim pos = -1
            For i = 0 To data.Length - 1
                If data(i) = &H1A Then
                    pos = i + 1
                    Exit For
                End If
            Next
            ErrorHandling.Check(pos <> -1, "IMD: No comment terminator found")

            Dim rpm = 300
            While pos < data.Length - 5
                Dim mode = CInt(data(pos))
                Dim cyl = CInt(data(pos + 1))
                Dim headRaw = CInt(data(pos + 2))
                Dim nsec = CInt(data(pos + 3))
                Dim secN = CInt(data(pos + 4))
                pos += 5

                ErrorHandling.Check(secN >= 0 AndAlso secN <= 6, String.Format("IMD: Bad sector size {0:x}", secN))
                Dim secSize = 128 << secN
                Dim hasCylMap = (headRaw And &H80) <> 0
                Dim hasHeadMap = (headRaw And &H40) <> 0
                Dim head = headRaw And &H3F
                ErrorHandling.Check(head >= 0 AndAlso head <= 1, String.Format("IMD: Bad head value {0:x}", head))

                Dim formatName As String
                Dim rate As Integer
                Select Case mode
                    Case 0, 1, 2
                        formatName = "ibm.fm"
                        If mode = 0 Then
                            If nsec = 26 Then
                                rpm = 360
                            End If
                            rate = 250
                        Else
                            rate = 125
                        End If
                    Case 3, 4, 5
                        formatName = "ibm.mfm"
                        If mode = 3 Then
                            If nsec = 26 Then
                                rpm = 360
                            End If
                            rate = 500
                        Else
                            rate = 250
                        End If
                    Case Else
                        Throw New FatalException(String.Format("IMD: Unrecognised track mode {0:x}", mode))
                End Select

                ErrorHandling.Check(pos + nsec <= data.Length, "IMD: Truncated rmap")
                Dim rmap(nsec - 1) As Integer
                For i = 0 To nsec - 1
                    rmap(i) = CInt(data(pos + i))
                Next
                pos += nsec

                Dim cmap As Integer() = Nothing
                If hasCylMap Then
                    ErrorHandling.Check(pos + nsec <= data.Length, "IMD: Truncated cmap")
                    cmap = New Integer(nsec - 1) {}
                    For i = 0 To nsec - 1
                        cmap(i) = CInt(data(pos + i))
                    Next
                    pos += nsec
                End If
                Dim hmap As Integer() = Nothing
                If hasHeadMap Then
                    ErrorHandling.Check(pos + nsec <= data.Length, "IMD: Truncated hmap")
                    hmap = New Integer(nsec - 1) {}
                    For i = 0 To nsec - 1
                        hmap(i) = CInt(data(pos + i))
                    Next
                    pos += nsec
                End If

                Dim sectors As New List(Of ParsedSector)()
                For i = 0 To nsec - 1
                    ErrorHandling.Check(pos < data.Length, "IMD: Truncated sector code")
                    Dim rec = CInt(data(pos))
                    pos += 1
                    ErrorHandling.Check(rec >= 0 AndAlso rec <= 8, String.Format("IMD: Unexpected sector code {0:x}", rec))

                    Dim payload(secSize - 1) As Byte
                    If rec <> 0 Then
                        Dim x = rec - 1
                        If (x And 1) <> 0 Then
                            ErrorHandling.Check(pos < data.Length, "IMD: Truncated compressed sector")
                            For p = 0 To payload.Length - 1
                                payload(p) = data(pos)
                            Next
                            pos += 1
                        Else
                            ErrorHandling.Check(pos + secSize <= data.Length, "IMD: Truncated sector payload")
                            Array.Copy(data, pos, payload, 0, secSize)
                            pos += secSize
                        End If
                    End If

                    sectors.Add(New ParsedSector With {
                        .C = If(cmap Is Nothing, cyl, cmap(i)),
                        .H = If(hmap Is Nothing, head, hmap(i)),
                        .R = rmap(i),
                        .N = secN,
                        .Data = payload
                    })
                Next

                Dim trackCodec = BuildTrackCodec(cyl, head, formatName, rate, rpm, sectors)
                _tracks(Tuple.Create(cyl, head)) = trackCodec
            End While
        End Sub

        ' Python map: src/greaseweazle/image/imd.py::IMD.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim key = Tuple.Create(cyl, side)
            If Not _tracks.ContainsKey(key) Then
                Return Nothing
            End If
            Return _tracks(key)
        End Function

        ' Python map: src/greaseweazle/image/imd.py::IMD.emit_track
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            ' Python: if isinstance(track, ibm.IBMTrack_Scan): track = track.track
            Dim scan = TryCast(track, IbmTrackScan)
            Dim resolved As HasFlux = If(scan IsNot Nothing, CType(scan.Track, HasFlux), track)

            Dim fixed = TryCast(resolved, IbmTrackFixed)
            ErrorHandling.Check(fixed IsNot Nothing,
                                String.Format("IMD: Cannot create T{0}.{1}: Not IBM.FM nor IBM.MFM", cyl, side))
            ' Python: skips IBMTrack_Empty entries by `if not isinstance(track, ibm.IBMTrack_Empty)`.
            ' Mirror that with the empty-track shortcut (Nsec == 0).
            If fixed.Nsec = 0 Then
                Return
            End If
            _tracks(Tuple.Create(cyl, side)) = fixed
        End Sub

        ' Python map: src/greaseweazle/image/imd.py::IMD.get_image
        Public Overrides Function GetImage() As Byte()
            Dim output As New List(Of Byte)()
            ' Python: 'IMD 1.17: %s\r\nGreaseweazle %s\r\n\x1a' % (now, __version__)
            Dim now = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
            Dim sig = String.Format("IMD 1.17: {0}{1}Greaseweazle {2}{1}", now, vbCrLf, ImdHostVersion)
            output.AddRange(System.Text.Encoding.ASCII.GetBytes(sig))
            output.Add(&H1A)

            For Each pair In _tracks.OrderBy(Function(x) x.Key.Item1).ThenBy(Function(x) x.Key.Item2)
                Dim cyl = pair.Key.Item1
                Dim head = pair.Key.Item2
                Dim track = pair.Value
                Dim mode = ResolveMode(track)

                ' Python iterates t.sectors in physical order (post sec_map),
                ' not sorted by sector id. Mirror by using the codec's existing
                ' SectorIds order (which already reflects physical layout).
                Dim ids = track.SectorIds.ToList()
                Dim nsec = ids.Count
                Dim secN = If(track.SectorHeaderNs.Count = 0, 0, track.SectorHeaderNs(0))
                Dim secSize = 128 << secN
                Dim trackHead = head

                ' Python: per-sector idam.n must be uniform; otherwise raise. We don't
                ' have per-sector IDAMs exposed here, but VB's IbmTrackFixed already
                ' enforces matching n via SectorHeaderNs from sec_map; assert it.
                If track.SectorHeaderNs.Any(Function(n) n <> secN) Then
                    Throw New FatalException(String.Format("IMD: Cannot create T{0}.{1}: Sectors vary in size", cyl, head))
                End If

                output.Add(CByte(mode))
                output.Add(CByte(cyl And &HFF))
                output.Add(CByte(trackHead And &HFF))
                output.Add(CByte(nsec And &HFF))
                output.Add(CByte(secN And &HFF))

                For Each sid In ids
                    output.Add(CByte(sid And &HFF))
                Next

                ' Python emits sector data in t.sectors order; since GetImgTrack lays
                ' bytes out by logical (id-sorted) order, we re-permute back to the
                ' physical order represented by `ids` here.
                Dim sortedIds = ids.OrderBy(Function(x) x).ToList()
                Dim trackData = track.GetImgTrack()
                Dim sectorBytes As New List(Of Byte())()
                Dim pos = 0
                For Each unused In sortedIds
                    Dim sector(secSize - 1) As Byte
                    Dim copyLen = Math.Min(secSize, Math.Max(0, trackData.Length - pos))
                    If copyLen > 0 Then Array.Copy(trackData, pos, sector, 0, copyLen)
                    sectorBytes.Add(sector)
                    pos += secSize
                Next
                ' Re-key by sector id so we can emit in physical order.
                Dim sectorById As New Dictionary(Of Integer, Byte())()
                For i = 0 To sortedIds.Count - 1
                    sectorById(sortedIds(i)) = sectorBytes(i)
                Next

                For Each sid In ids
                    Dim sector = sectorById(sid)
                    Dim uniform = sector.Length > 0 AndAlso sector.All(Function(b) b = sector(0))
                    Dim rec = If(uniform, 1, 0)
                    ' Python: rec |= 2 if dam.mark == DDAM; rec |= 4 if dam.crc != 0.
                    ' We don't track per-sector DAM/CRC state in this codec, so emit
                    ' only the compression bit.
                    output.Add(CByte(rec + 1))
                    If uniform Then
                        output.Add(sector(0))
                    Else
                        output.AddRange(sector)
                    End If
                Next
            Next

            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveMode)
        Private Shared Function ResolveMode(track As IbmTrackFixed) As Integer
            Dim isFm = String.Equals(track.FormatName, "ibm.fm", StringComparison.OrdinalIgnoreCase)
            If isFm Then
                If track.Clock < 3.0E-6 Then
                    Return 0
                End If
                Return 2
            End If

            If track.Clock < 1.5E-6 Then
                Return 3
            End If
            Return 5
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildTrackCodec)
        Private Shared Function BuildTrackCodec(cyl As Integer,
                                                head As Integer,
                                                formatName As String,
                                                rate As Integer,
                                                rpm As Integer,
                                                sectors As List(Of ParsedSector)) As IbmTrackFixed
            Dim timePerRev = 60.0 / rpm
            Dim trackLenBc = CInt(Math.Max(1, Math.Floor(rate * 400.0 * 300.0 / rpm)))
            Dim clock = timePerRev / trackLenBc

            Dim sectorSizes = sectors.Select(Function(s) s.Data.Length).ToList()
            Dim sectorNs = sectors.Select(Function(s) s.N).ToList()
            Dim sectorIds = sectors.Select(Function(s) s.R).ToList()
            Dim codec As New IbmTrackFixed(formatName,
                                           cyl,
                                           head,
                                           sectorSizes,
                                           sectorNs,
                                           sectorIds,
                                           head,
                                           imgBytesPerSector:=Nothing,
                                           timePerRev:=timePerRev,
                                           clock:=clock,
                                           emitIam:=True,
                                           gap1Override:=Nothing,
                                           gap2Override:=Nothing,
                                           gap3Override:=Nothing,
                                           gap4aOverride:=Nothing,
                                           gapByteOverride:=Nothing)
            Dim logicalOrder = Enumerable.Range(0, sectorIds.Count).OrderBy(Function(i) sectorIds(i)).ToList()
            Dim bytes As New List(Of Byte)()
            For Each idx In logicalOrder
                bytes.AddRange(sectors(idx).Data)
            Next
            codec.SetImgTrack(bytes.ToArray())
            Return codec
        End Function

        ' Python map: src/greaseweazle/image/imd.py::(no direct 1:1 symbol; VB class helper supporting imd image handling)
        Private Class ParsedSector
            Public Property C As Integer
            Public Property H As Integer
            Public Property R As Integer
            Public Property N As Integer
            Public Property Data As Byte()
        End Class

    End Class

End Namespace
