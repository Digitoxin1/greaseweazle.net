Imports System.Text
Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOSDef
    Public Class AmigaDosDef
        Implements TrackDef

        Private _secs As Integer?

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOSDef.__init__
        Public Sub New(Optional formatName As String = Nothing)
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.1
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOSDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n = 11 OrElse n = 22, "secs out of range")
                    _secs = n
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOSDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_secs.HasValue, "number of sectors not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOSDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New AmigaDos(cyl, head, _secs.GetValueOrDefault())
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS
    Public Class AmigaDos
        Inherits CodecBase
        Implements HasVerify

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.verify_revs
        Public ReadOnly Property VerifyRevsValue As Double Implements HasVerify.VerifyRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.verify_track
        Public Function VerifyTrackInterface(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            Return VerifyTrack(flux)
        End Function

        Private Shared ReadOnly SyncPattern As Boolean() = BytesToBits(New Byte() {&H44, &H89, &H44, &H89})
        Private Shared ReadOnly BadSector As Byte() =
            Enumerable.Repeat(Encoding.ASCII.GetBytes("-=[BAD SECTOR]=-"), 32).
                SelectMany(Function(x) x).ToArray()

        Private ReadOnly _trackNumber As Integer
        Private ReadOnly _nsec As Integer
        Private ReadOnly _clock As Double
        Private ReadOnly _sectors As List(Of Tuple(Of Byte(), Byte()))
        Private ReadOnly _map As List(Of Integer?)

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.__init__
        Public Sub New(cyl As Integer, head As Integer, secs As Integer)
            _trackNumber = cyl * 2 + head
            _nsec = secs
            _clock = If(secs = 11, 14.0 / 7093790.0, (14.0 / 7093790.0) / 2.0)
            _sectors = Enumerable.Repeat(Of Tuple(Of Byte(), Byte()))(Nothing, _nsec).ToList()
            _map = Enumerable.Repeat(Of Integer?)(Nothing, _nsec).ToList()
        End Sub

        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return _nsec
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return sectorId >= 0 AndAlso sectorId < _nsec AndAlso _sectors(sectorId) IsNot Nothing
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return _sectors.Where(Function(x) x Is Nothing).Count()
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Dim bytes As New List(Of Byte)()
            For Each sec In _sectors
                If sec Is Nothing Then
                    bytes.AddRange(BadSector)
                Else
                    bytes.AddRange(sec.Item2)
                End If
            Next
            Return bytes.ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Dim total = _nsec * 512
            Dim src = If(trackData, Array.Empty(Of Byte)())
            If src.Length < total Then
                src = src.Concat(Enumerable.Repeat(CByte(0), total - src.Length)).ToArray()
            End If
            For i = 0 To _nsec - 1
                _map(i) = i
                Dim label(15) As Byte
                Dim data(511) As Byte
                Array.Copy(src, i * 512, data, 0, 512)
                _sectors(i) = Tuple.Create(label, data)
            Next
            Return total
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim raw As New PllTrack(timePerRev:=0.2, clock:=_clock, data:=track, pll:=pll)
            Dim bits = raw.GetAllData().Item1
            For Each offs In FindPatternOffsets(bits, SyncPattern)
                If NrMissing() = 0 Then Exit For
                Dim segBits = bits.Skip(offs).Take(544 * 16).ToList()
                If segBits.Count <> 544 * 16 Then Continue For
                Dim sec = BitsToBytes(segBits)
                If sec.Length <> 1088 Then Continue For

                Dim header = Decode(sec.Skip(4).Take(8).ToArray())
                If header.Length <> 4 Then Continue For
                Dim fmt = header(0)
                Dim trk = header(1)
                Dim secId = CInt(header(2))
                Dim togo = CInt(header(3))
                If fmt <> &HFF OrElse trk <> _trackNumber OrElse Not (secId < _nsec AndAlso togo > 0 AndAlso togo <= _nsec) OrElse Exists(secId, togo) Then
                    Continue For
                End If

                Dim label = Decode(sec.Skip(12).Take(32).ToArray())
                Dim hsumBytes = Decode(sec.Skip(44).Take(8).ToArray())
                If hsumBytes.Length <> 4 Then Continue For
                Dim hsum = BytesToUInt32BE(hsumBytes)
                If hsum <> Checksum(header.Concat(label).ToArray()) Then Continue For

                Dim dsumBytes = Decode(sec.Skip(52).Take(8).ToArray())
                If dsumBytes.Length <> 4 Then Continue For
                Dim dsum = BytesToUInt32BE(dsumBytes)
                Dim data = Decode(sec.Skip(60).Take(1024).ToArray())
                If data.Length <> 512 Then Continue For
                If dsum <> Checksum(data) Then Continue For

                [Add](secId, togo, label, data)
            Next
        End Sub

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            Dim missing = Enumerable.Range(0, _nsec).Where(Function(x) Not _map.Contains(x)).GetEnumerator()
            Dim fullMap As New List(Of Integer)(_nsec)
            For i = 0 To _nsec - 1
                If _map(i).HasValue Then
                    fullMap.Add(_map(i).Value)
                Else
                    missing.MoveNext()
                    fullMap.Add(missing.Current)
                End If
            Next

            Dim t As New List(Of Byte)()
            t.AddRange(Encode(New Byte(128 * (_nsec \ 11) - 1) {}))
            For nr = 0 To _nsec - 1
                Dim secId = fullMap(nr)
                Dim sector = _sectors(secId)
                Dim label As Byte()
                Dim data As Byte()
                If sector Is Nothing Then
                    label = New Byte(15) {}
                    data = BadSector
                Else
                    label = sector.Item1
                    data = sector.Item2
                End If
                Dim header As Byte() = {
                    &HFF,
                    CByte(_trackNumber And &HFF),
                    CByte(secId And &HFF),
                    CByte((_nsec - nr) And &HFF)
                }
                t.AddRange(New Byte() {&H44, &H89, &H44, &H89})
                t.AddRange(Encode(header))
                t.AddRange(Encode(label))
                t.AddRange(Encode(UInt32ToBytesBE(Checksum(header.Concat(label).ToArray()))))
                t.AddRange(Encode(UInt32ToBytesBE(Checksum(data))))
                t.AddRange(Encode(data))
                t.AddRange(Encode(New Byte(1) {}))
            Next

            Dim tlen = (CInt(Math.Floor(0.2 / _clock)) + 31) And Not 31
            Dim targetBytes = tlen \ 8
            If t.Count < targetBytes Then
                t.AddRange(Enumerable.Repeat(CByte(0), targetBytes - t.Count))
            End If
            Dim mt As New MasterTrack(BytesToBits(MfmEncode(t.ToArray())), 0.2)
            ' Python: track.verify = self
            mt.Verify = Me
            Return mt
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.summary_string
        Public Overrides Function SummaryString() As String
            Return String.Format("AmigaDOS ({0}/{1} sectors)", _nsec - NrMissing(), _nsec)
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.verify_track
        Public Function VerifyTrack(flux As Flux) As Boolean
            Dim cyl = _trackNumber \ 2
            Dim head = _trackNumber And 1
            Dim readback As New AmigaDos(cyl, head, _nsec)
            readback.DecodeFlux(flux)
            If readback.NrMissing() <> 0 Then
                Return False
            End If
            Return _sectors.SequenceEqual(readback._sectors)
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.exists
        Private Function Exists(secId As Integer, togo As Integer) As Boolean
            Return _sectors(secId) IsNot Nothing OrElse _map(_nsec - togo).HasValue
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS.add
        Private Sub [Add](secId As Integer, togo As Integer, label As Byte(), data As Byte())
            _sectors(secId) = Tuple.Create(label, data)
            _map(_nsec - togo) = secId
        End Sub

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::encode
        Public Shared Function Encode(data As Byte()) As Byte()
            Dim odd As New List(Of Byte)(data.Length)
            Dim even As New List(Of Byte)(data.Length)
            For Each b In data
                odd.Add(CByte((b >> 1) And &H55))
                even.Add(CByte(b And &H55))
            Next
            Return odd.Concat(even).ToArray()
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::decode
        Public Shared Function Decode(data As Byte()) As Byte()
            Dim n = data.Length \ 2
            Dim result(n - 1) As Byte
            For i = 0 To n - 1
                result(i) = CByte(((data(i) << 1) And &HAA) Or (data(i + n) And &H55))
            Next
            Return result
        End Function

        ' Python map: src/greaseweazle/codec/amiga/amigados.py::checksum
        Public Shared Function Checksum(data As Byte()) As UInteger
            Dim csum As UInteger = 0UI
            For i = 0 To data.Length - 1 Step 4
                csum = csum Xor BytesToUInt32BE(data.Skip(i).Take(4).ToArray())
            Next
            Return (csum Xor (csum >> 1)) And &H55555555UI
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BytesToUInt32BE)
        Private Shared Function BytesToUInt32BE(data As Byte()) As UInteger
            Return (CUInt(data(0)) << 24) Or (CUInt(data(1)) << 16) Or (CUInt(data(2)) << 8) Or CUInt(data(3))
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration UInt32ToBytesBE)
        Private Shared Function UInt32ToBytesBE(value As UInteger) As Byte()
            Return New Byte() {
                CByte((value >> 24) And &HFFUI),
                CByte((value >> 16) And &HFFUI),
                CByte((value >> 8) And &HFFUI),
                CByte(value And &HFFUI)
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration MfmEncode)
        Private Shared Function MfmEncode(values As Byte()) As Byte()
            Dim output As New List(Of Byte)(values.Length)
            Dim y As Integer = 0
            For Each x In values
                y = ((y << 8) Or x) And &HFFFF
                If (x And &HAA) = 0 Then
                    y = y Or ((Not ((y >> 1) Or (y << 1))) And &HAAAA)
                End If
                y = y And &HFF
                output.Add(CByte(y))
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FindPatternOffsets)
        Private Shared Iterator Function FindPatternOffsets(bits As List(Of Boolean), pattern As Boolean()) As IEnumerable(Of Integer)
            If pattern.Length = 0 OrElse bits.Count < pattern.Length Then Return
            For i = 0 To bits.Count - pattern.Length
                Dim ok = True
                For j = 0 To pattern.Length - 1
                    If bits(i + j) <> pattern(j) Then
                        ok = False
                        Exit For
                    End If
                Next
                If ok Then Yield i
            Next
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BytesToBits)
        Private Shared Function BytesToBits(data As Byte()) As Boolean()
            Dim bits As New List(Of Boolean)(data.Length * 8)
            For Each b In data
                For i = 7 To 0 Step -1
                    bits.Add(((b >> i) And 1) = 1)
                Next
            Next
            Return bits.ToArray()
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BitsToBytes)
        Private Shared Function BitsToBytes(bits As List(Of Boolean)) As Byte()
            Dim out As New List(Of Byte)(bits.Count \ 8)
            For i = 0 To bits.Count - 1 Step 8
                Dim b As Integer = 0
                For j = 0 To 7
                    b = (b << 1) Or If(bits(i + j), 1, 0)
                Next
                out.Add(CByte(b))
            Next
            Return out.ToArray()
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS_DD
    Public Class AmigaDosDd
        Inherits AmigaDos

        Public Sub New(cyl As Integer, head As Integer)
            MyBase.New(cyl, head, 11)
        End Sub
    End Class

    ' Python map: src/greaseweazle/codec/amiga/amigados.py::AmigaDOS_HD
    Public Class AmigaDosHd
        Inherits AmigaDos

        Public Sub New(cyl As Integer, head As Integer)
            MyBase.New(cyl, head, 22)
        End Sub
    End Class

End Namespace
