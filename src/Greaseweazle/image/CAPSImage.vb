Imports System.IO
Imports System.Runtime.InteropServices
Imports Greaseweazle.Core

Namespace Greaseweazle.Images

    ' Python map: src/greaseweazle/image/caps.py::CAPS
    Public MustInherit Class CAPS
        Inherits Image

        ' Python map: src/greaseweazle/image/caps.py::DI_LOCK
        Private Const DiLockDefFlags As UInteger = &H3B04UI

        Private Shared _backend As CapsBackend
        Private _iid As Integer = -1
        Private _tempPath As String
        ' Python map: src/greaseweazle/image/caps.py::CAPS.pi (CapsImageInfo populated by CAPSGetImageInfo)
        Private _pi As CapsImageInfo
        Private _piValid As Boolean = False

        ' Python map: src/greaseweazle/image/caps.py::CAPS.pi
        Friend ReadOnly Property Pi As CapsImageInfo
            Get
                Return _pi
            End Get
        End Property

        ' Python map: src/greaseweazle/image/caps.py::CAPS.pi (validity flag for image-info bounds checks)
        Friend ReadOnly Property HasPi As Boolean
            Get
                Return _piValid
            End Get
        End Property

        ' Python map: src/greaseweazle/image/caps.py::CAPS.__init__
        Protected Sub New()
            MyBase.New()
            Me.ReadOnly = True
        End Sub

        Protected MustOverride ReadOnly Property ImageTypeName As String

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration FromBytes)
        Public Overrides Sub FromBytes(data As Byte())
            CleanupImage()

            Dim path = ResolveInputPath(data)
            _backend = GetOrCreateBackend()
            _iid = _backend.CapsAddImage()
            ErrorHandling.Check(_iid >= 0, String.Format("CAPS: {0}: Could not create image container", ImageTypeName))

            Dim lockRc = _backend.CapsLockImage(_iid, path)
            ErrorHandling.Check(lockRc = 0, String.Format("CAPS: {0}: Could not open image '{1}'", ImageTypeName, path))
            Dim loadRc = _backend.CapsLoadImage(_iid, DiLockDefFlags)
            ErrorHandling.Check(loadRc = 0, String.Format("CAPS: {0}: Could not load image '{1}'", ImageTypeName, path))

            ' Python: caps.pi = CapsImageInfo(); CAPSGetImageInfo(ct.byref(caps.pi), caps.iid)
            ' Populate the image-info structure so we can perform Python's
            ' minhead/maxhead and mincylinder/maxcylinder bounds checks before
            ' attempting to lock individual tracks.
            _pi = New CapsImageInfo()
            Dim infoRc = _backend.CapsGetImageInfo(_pi, _iid)
            ErrorHandling.Check(infoRc = 0,
                                String.Format("CAPS: {0}: Could not get info for image '{1}'", ImageTypeName, path))
            _piValid = True

            ' Python: print(caps) inside CAPS.from_file (caps.py::from_file).
            ' Subclasses override __str__ to format the image-info banner; we do
            ' the same with ToString() and route the multi-line text through
            ' LibraryDiagnostics so the CLI surfaces it on stderr/stdout to
            ' match gw.exe's output. Library hosts that don't subscribe simply
            ' don't see the banner.
            Dim banner = Me.ToString()
            If Not String.IsNullOrEmpty(banner) Then
                LibraryDiagnostics.EmitInfo(banner)
            End If
        End Sub

        ' Python map: src/greaseweazle/image/caps.py::CapsImageInfo.platform_name
        Private Shared ReadOnly PlatformNames As String() = New String() {
            "N/A", "Amiga", "Atari ST", "IBM PC", "Amstrad CPC",
            "Spectrum", "Sam Coupe", "Archimedes", "C64", "Atari (8-bit)"
        }

        ' Python map: src/greaseweazle/image/caps.py::IPF.__str__ (Platform field)
        ' / CTRaw.__str__ helpers. Subclasses call this when assembling the
        ' image-info banner so the platform-list rendering stays in one place.
        Protected Function FormatPlatformList() As String
            If _pi.Platform Is Nothing OrElse _pi.Platform.Length = 0 Then
                Return PlatformNames(0)
            End If
            Dim parts As New List(Of String)()
            For i = 0 To _pi.Platform.Length - 1
                Dim p = CInt(_pi.Platform(i))
                If p = 0 AndAlso parts.Count > 0 Then Exit For
                Dim name As String
                If p >= 0 AndAlso p < PlatformNames.Length Then
                    name = PlatformNames(p)
                Else
                    name = PlatformNames(0)
                End If
                parts.Add(name)
            Next
            Return String.Join(", ", parts)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration EmitTrack)
        Public Overrides Sub EmitTrack(cyl As Integer, side As Integer, track As HasFlux)
            Throw New FatalException(String.Format("{0}: Cannot create {1} image files",
                                                   If(String.IsNullOrEmpty(FileName), ImageTypeName, FileName),
                                                   ImageTypeName))
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetImage)
        Public Overrides Function GetImage() As Byte()
            Throw New FatalException(String.Format("{0}: Cannot create {1} image files",
                                                   If(String.IsNullOrEmpty(FileName), ImageTypeName, FileName),
                                                   ImageTypeName))
        End Function

        ' Python map: src/greaseweazle/image/caps.py::CAPS.__del__
        Protected Overrides Sub Finalize()
            Try
                CleanupImage()
            Finally
                MyBase.Finalize()
            End Try
        End Sub

        ' Python map: src/greaseweazle/image/caps.py::CAPS.__str__
        Public Overrides Function ToString() As String
            Return String.Format("{0}({1})", ImageTypeName, If(String.IsNullOrEmpty(FileName), "<memory>", FileName))
        End Function

        ' Python map: src/greaseweazle/image/caps.py::CAPS.get_track
        Public Overridable Function GetTrackBase(cyl As Integer, side As Integer) As HasFlux
            Return Nothing
        End Function

        ' Python map: src/greaseweazle/image/caps.py::CAPS.from_file
        Public Shared Shadows Function FromFile(name As String) As CAPS
            If name Is Nothing Then
                Throw New ArgumentNullException(NameOf(name))
            End If
            Dim ext = Path.GetExtension(name)
            If String.Equals(ext, ".ctr", StringComparison.OrdinalIgnoreCase) Then
                Return Image.FromFile(Of CTRaw)(name, Nothing, Nothing)
            End If
            Return Image.FromFile(Of IPF)(name, Nothing, Nothing)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration TryReadTrack)
        Protected Function TryReadTrack(cyl As Integer, head As Integer) As CapsTrackSnapshot
            If _iid < 0 Then
                Return Nothing
            End If

            ' Python CAPSTrackInfo.__init__: raise NoTrack when (cyl, head) lies
            ' outside the image's declared cyl/head bounds. Mirroring that here
            ' prevents calls into CAPSLockTrack that the C library would reject.
            If _piValid Then
                If head < CInt(_pi.MinHead) OrElse head > CInt(_pi.MaxHead) Then
                    Return Nothing
                End If
                If cyl < CInt(_pi.MinCylinder) OrElse cyl > CInt(_pi.MaxCylinder) Then
                    Return Nothing
                End If
            End If

            Dim ti As New CapsTrackInfoT2 With {.Type = 2UI}
            Dim rc = _backend.CapsLockTrack(ti, _iid, cyl, head, DiLockDefFlags)
            ErrorHandling.Check(rc = 0, String.Format("Could not lock CAPS track {0}.{1}", cyl, head))
            If ti.TrackBuf = IntPtr.Zero OrElse ti.TrackLen = 0UI Then
                Return Nothing
            End If

            Dim bitCount = CInt(ti.TrackLen)
            Dim packedLen = CInt((ti.TrackLen + 7UI) \ 8UI)
            Dim packed(packedLen - 1) As Byte
            Marshal.Copy(ti.TrackBuf, packed, 0, packedLen)
            Dim bits = PackedBytesToBits(packed, bitCount)

            Dim ticks As List(Of Double) = Nothing
            If ti.TimeBuf <> IntPtr.Zero AndAlso ti.TimeLen > 0UI Then
                Dim timing(CInt(ti.TimeLen) - 1) As Integer
                Marshal.Copy(ti.TimeBuf, timing, 0, timing.Length)
                ticks = New List(Of Double)(bitCount)
                For Each t In timing
                    For i = 0 To 7
                        ticks.Add(CDbl(CUInt(t)))
                    Next
                Next
                While ticks.Count < bitCount
                    ticks.Add(1000.0)
                End While
                If ticks.Count > bitCount Then
                    ticks = ticks.Take(bitCount).ToList()
                End If
            End If

            Return New CapsTrackSnapshot With {
                .TrackInfo = ti,
                .Bits = bits,
                .Ticks = ticks
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration QuerySectorRanges)
        Protected Function QuerySectorRanges(cyl As Integer, head As Integer, sectorCount As Integer, trackLen As Integer) As List(Of Tuple(Of Integer, Integer))
            Dim ranges As New List(Of Tuple(Of Integer, Integer))()
            For i = 0 To Math.Max(0, sectorCount) - 1
                Dim si As New CapsSectorInfo()
                Dim rc = _backend.CapsGetInfoSector(si, _iid, cyl, head, i)
                ErrorHandling.Check(rc = 0, "Couldn't get sector info")
                ranges.Add(Tuple.Create(CInt(si.DataStart Mod CUInt(trackLen)), CInt(si.DataSize)))
            Next
            Return ranges
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration QueryWeakRanges)
        Protected Function QueryWeakRanges(cyl As Integer, head As Integer, weakCount As Integer, trackLen As Integer) As List(Of Tuple(Of Integer, Integer))
            Dim ranges As New List(Of Tuple(Of Integer, Integer))()
            For i = 0 To Math.Max(0, weakCount) - 1
                Dim wi As New CapsDataInfo()
                Dim rc = _backend.CapsGetInfoWeak(wi, _iid, cyl, head, i)
                ErrorHandling.Check(rc = 0, "Couldn't get weak data info")
                ranges.Add(Tuple.Create(CInt(wi.Start Mod CUInt(trackLen)), CInt(wi.Size)))
            Next
            Return ranges
        End Function

        ' Python map: shared helper. See Greaseweazle.Core.CollectionHelpers.
        Protected Shared Function RotateList(Of T)(values As List(Of T), index As Integer) As List(Of T)
            If values Is Nothing Then Return values
            Return CollectionHelpers.RotateList(values, index)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ClipAndSortRanges)
        Protected Shared Function ClipAndSortRanges(ranges As IEnumerable(Of Tuple(Of Integer, Integer)), trackLen As Integer) As List(Of Tuple(Of Integer, Integer))
            Dim result As New List(Of Tuple(Of Integer, Integer))()
            For Each range In ranges
                Dim s = range.Item1
                Dim n = range.Item2
                If n <= 0 Then
                    Continue For
                End If
                If s + n > trackLen Then
                    result.Add(Tuple.Create(s, trackLen - s))
                    result.Add(Tuple.Create(0, s + n - trackLen))
                Else
                    result.Add(Tuple.Create(s, n))
                End If
            Next
            result.Sort(Function(a, b) a.Item1.CompareTo(b.Item1))
            Return result
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration PackedBytesToBits)
        Private Shared Function PackedBytesToBits(packed As Byte(), bitCount As Integer) As List(Of Boolean)
            Dim bits As New List(Of Boolean)(bitCount)
            For i = 0 To bitCount - 1
                Dim b = packed(i \ 8)
                Dim shift = 7 - (i Mod 8)
                bits.Add(((b >> shift) And 1) <> 0)
            Next
            Return bits
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveInputPath)
        Private Function ResolveInputPath(data As Byte()) As String
            If Not String.IsNullOrEmpty(FileName) AndAlso File.Exists(FileName) Then
                Return FileName
            End If
            Dim suffix = "." & ImageTypeName.ToLowerInvariant()
            _tempPath = Path.Combine(Path.GetTempPath(), "gw_caps_" & Guid.NewGuid().ToString("N") & suffix)
            File.WriteAllBytes(_tempPath, If(data, Array.Empty(Of Byte)()))
            Return _tempPath
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration GetOrCreateBackend)
        Private Shared Function GetOrCreateBackend() As CapsBackend
            If _backend IsNot Nothing Then
                Return _backend
            End If

            Dim x64Error As Exception = Nothing
            Try
                Dim probe = New CapsBackendX64()
                probe.CapsInit()
                _backend = probe
                Return _backend
            Catch ex As Exception
                x64Error = ex
            End Try

            Try
                Dim probe = New CapsBackendGeneric()
                probe.CapsInit()
                _backend = probe
                Return _backend
            Catch ex As Exception
                Throw New CapsLibraryNotFoundException(x64Error.Message, ex.Message)
            End Try
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CleanupImage)
        Private Sub CleanupImage()
            If _backend IsNot Nothing AndAlso _iid >= 0 Then
                Try
                    _backend.CapsUnlockAllTracks(_iid)
                Catch
                End Try
                Try
                    _backend.CapsUnlockImage(_iid)
                Catch
                End Try
                Try
                    _backend.CapsRemImage(_iid)
                Catch
                End Try
                _iid = -1
            End If
            If Not String.IsNullOrEmpty(_tempPath) Then
                Try
                    File.Delete(_tempPath)
                Catch
                End Try
                _tempPath = Nothing
            End If
        End Sub

    End Class

    ' Python map: src/greaseweazle/image/caps.py::CTRaw
    Public Class CTRaw
        Inherits CAPS

        Protected Overrides ReadOnly Property ImageTypeName As String
            Get
                Return "CTRaw"
            End Get
        End Property

        ' Python map: src/greaseweazle/image/caps.py::CTRaw.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim snap = TryReadTrack(cyl, side)
            If snap Is Nothing Then
                Return Nothing
            End If
            Return New MasterTrack(snap.Bits, 0.2)
        End Function

        ' Python map: src/greaseweazle/image/caps.py::CTRaw.__str__
        Public Overrides Function ToString() As String
            If Not HasPi Then
                Return "CTRaw Image File:"
            End If
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim sb As New System.Text.StringBuilder()
            sb.Append("CTRaw Image File:")
            sb.Append(vbLf)
            sb.AppendFormat(ci, " Cyls: {0}-{1}  Heads: {2}-{3}",
                            Pi.MinCylinder, Pi.MaxCylinder, Pi.MinHead, Pi.MaxHead)
            Return sb.ToString()
        End Function
    End Class

    ' Python map: src/greaseweazle/image/caps.py::IPF
    Public Class IPF
        Inherits CAPS

        Protected Overrides ReadOnly Property ImageTypeName As String
            Get
                Return "IPF"
            End Get
        End Property

        ' Python map: src/greaseweazle/image/caps.py::IPF.__str__
        ' release == 0x843265bb is disk-utilities' IPF_ID marker; otherwise the
        ' release/revision pair is the SPS catalogue ID used to identify
        ' commercial Amiga / Atari ST releases.
        Public Overrides Function ToString() As String
            If Not HasPi Then
                Return "IPF Image File:"
            End If
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim sb As New System.Text.StringBuilder()
            sb.Append("IPF Image File:")
            sb.Append(vbLf)
            If Pi.Release = &H843265BBUI Then
                sb.Append(" SPS ID: None (https://github.com/keirf/disk-utilities)")
            Else
                sb.AppendFormat(ci, " SPS ID: {0:D4} (rev {1})", Pi.Release, Pi.Revision)
            End If
            sb.Append(vbLf)
            sb.AppendFormat(ci, " Platform: {0}", FormatPlatformList())
            sb.Append(vbLf)
            sb.AppendFormat(ci, " Created: {0}/{1}/{2} {3:D2}:{4:D2}:{5:D2}",
                            Pi.Created.Year, Pi.Created.Month, Pi.Created.Day,
                            Pi.Created.Hour, Pi.Created.Min, Pi.Created.Sec)
            sb.Append(vbLf)
            sb.AppendFormat(ci, " Cyls: {0}-{1}  Heads: {2}-{3}",
                            Pi.MinCylinder, Pi.MaxCylinder, Pi.MinHead, Pi.MaxHead)
            Return sb.ToString()
        End Function

        ' Python map: src/greaseweazle/image/caps.py::IPF.get_track
        Public Overrides Function GetTrack(cyl As Integer, side As Integer) As HasFlux
            Dim snap = TryReadTrack(cyl, side)
            If snap Is Nothing Then
                Return Nothing
            End If

            Dim ti = snap.TrackInfo
            Dim bits = snap.Bits
            Dim ticks = snap.Ticks
            Dim trackLen = bits.Count
            Dim dataRanges = QuerySectorRanges(cyl, side, CInt(ti.SectorCnt), trackLen)
            Dim weakRanges = QueryWeakRanges(cyl, side, CInt(ti.WeakCnt), trackLen)
            Dim overlap = ti.Overlap

            If overlap < 0 AndAlso weakRanges.Count > 0 Then
                Dim longestIdx = 0
                For i = 1 To weakRanges.Count - 1
                    If weakRanges(i).Item2 > weakRanges(longestIdx).Item2 Then
                        longestIdx = i
                    End If
                Next
                Dim s = weakRanges(longestIdx).Item1
                Dim n = weakRanges(longestIdx).Item2
                If n > 200 Then
                    overlap = (s + n \ 2) Mod trackLen
                End If
            End If

            If overlap < 0 AndAlso dataRanges.Count > 0 Then
                ' Python: data.sort() -- ranges are sorted by start before computing the
                ' inter-sector gap so the chosen splice is in the largest pre-rotation gap.
                dataRanges.Sort(Function(a, b)
                                    Dim cmp = a.Item1.CompareTo(b.Item1)
                                    If cmp <> 0 Then Return cmp
                                    Return a.Item2.CompareTo(b.Item2)
                                End Function)
                Dim gap As New List(Of Integer)()
                For i = 0 To dataRanges.Count - 1
                    Dim cur = dataRanges(i)
                    Dim nxt = dataRanges((i + 1) Mod dataRanges.Count)
                    gap.Add(((nxt.Item1 - cur.Item1 - cur.Item2) Mod trackLen + trackLen) Mod trackLen)
                Next
                Dim maxGap = gap.Max()
                Dim idx = gap.IndexOf(maxGap)
                Dim s = dataRanges(idx).Item1
                Dim n = dataRanges(idx).Item2
                overlap = (s + n + maxGap \ 2) Mod trackLen
            End If

            If overlap < 0 Then
                overlap = 0
            End If

            If overlap <> 0 Then
                dataRanges = dataRanges.Select(Function(x) Tuple.Create(((x.Item1 - overlap) Mod trackLen + trackLen) Mod trackLen, x.Item2)).ToList()
                weakRanges = weakRanges.Select(Function(x) Tuple.Create(((x.Item1 - overlap) Mod trackLen + trackLen) Mod trackLen, x.Item2)).ToList()
                bits = RotateList(bits, overlap)
                ticks = RotateList(ticks, overlap)
            End If

            dataRanges = ClipAndSortRanges(dataRanges, trackLen)
            weakRanges = ClipAndSortRanges(weakRanges, trackLen)

            ' Python:
            '     track = IPFTrack(bits=..., time_per_rev=60/ti.rpm, bit_ticks=..., splice=..., weak=...)
            '     track.verify = track
            '     track.sectors = data
            ' We mirror that by returning an IPFTrack that uses *itself* as the
            ' verifier and carries the per-sector data ranges so that
            ' verify_track can compare strong-data slices against a freshly
            ' read PLL bit stream.
            Dim track As New IpfTrack(bits,
                                      0.2,
                                      bitTicks:=ticks,
                                      splice:=Math.Max(0, overlap),
                                      weak:=weakRanges)
            track.Sectors = dataRanges
            track.Verify = track
            Return track
        End Function
    End Class

    ' Python map: src/greaseweazle/image/caps.py::(no direct 1:1 symbol; VB class helper supporting caps image handling)
    Public Class CapsTrackSnapshot
        Public Property TrackInfo As CapsTrackInfoT2
        Public Property Bits As List(Of Boolean)
        Public Property Ticks As List(Of Double)
    End Class

    ' Python map: src/greaseweazle/image/caps.py::CapsDateTimeExt
    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB structure declaration CapsDateTimeExt)
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure CapsDateTimeExt
        Public Year As UInteger
        Public Month As UInteger
        Public Day As UInteger
        Public Hour As UInteger
        Public Min As UInteger
        Public Sec As UInteger
        Public Tick As UInteger
    End Structure

    ' Python map: src/greaseweazle/image/caps.py::CapsImageInfo
    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB structure declaration CapsImageInfo)
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure CapsImageInfo
        Public Type As UInteger
        Public Release As UInteger
        Public Revision As UInteger
        Public MinCylinder As UInteger
        Public MaxCylinder As UInteger
        Public MinHead As UInteger
        Public MaxHead As UInteger
        Public Created As CapsDateTimeExt
        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=4)>
        Public Platform() As UInteger
    End Structure

    ' Python map: src/greaseweazle/image/caps.py::CapsTrackInfoT2
    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB structure declaration CapsTrackInfoT2)
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Public Structure CapsTrackInfoT2
        Public Type As UInteger
        Public Cylinder As UInteger
        Public Head As UInteger
        Public SectorCnt As UInteger
        Public SectorSize As UInteger
        Public TrackBuf As IntPtr
        Public TrackLen As UInteger
        Public TimeLen As UInteger
        Public TimeBuf As IntPtr
        Public Overlap As Integer
        Public StartBit As UInteger
        Public WSeed As UInteger
        Public WeakCnt As UInteger
    End Structure

    ' Python map: src/greaseweazle/image/caps.py::CapsSectorInfo
    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB structure declaration CapsSectorInfo)
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure CapsSectorInfo
        Public DescDataSize As UInteger
        Public DescGapSize As UInteger
        Public DataSize As UInteger
        Public GapSize As UInteger
        Public DataStart As UInteger
        Public GapStart As UInteger
        Public GapSizeWs0 As UInteger
        Public GapSizeWs1 As UInteger
        Public GapWs0Mode As UInteger
        Public GapWs1Mode As UInteger
        Public CellType As UInteger
        Public EncType As UInteger
    End Structure

    ' Python map: src/greaseweazle/image/caps.py::CapsDataInfo
    ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB structure declaration CapsDataInfo)
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Friend Structure CapsDataInfo
        Public Type As UInteger
        Public Start As UInteger
        Public Size As UInteger
    End Structure

    ' Python map: src/greaseweazle/image/caps.py::(no direct 1:1 symbol; VB interface helper supporting caps image handling)
    Friend Interface CapsBackend
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CapsInit)
        Sub CapsInit()
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsAddImage)
        Function CapsAddImage() As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLockImage)
        Function CapsLockImage(iid As Integer, path As String) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLoadImage)
        Function CapsLoadImage(iid As Integer, flags As UInteger) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetImageInfo)
        Function CapsGetImageInfo(ByRef info As CapsImageInfo, iid As Integer) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLockTrack)
        Function CapsLockTrack(ByRef track As CapsTrackInfoT2, iid As Integer, cyl As Integer, head As Integer, flags As UInteger) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetInfoSector)
        Function CapsGetInfoSector(ByRef info As CapsSectorInfo, iid As Integer, cyl As Integer, head As Integer, index As Integer) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetInfoWeak)
        Function CapsGetInfoWeak(ByRef info As CapsDataInfo, iid As Integer, cyl As Integer, head As Integer, index As Integer) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsUnlockAllTracks)
        Function CapsUnlockAllTracks(iid As Integer) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsUnlockImage)
        Function CapsUnlockImage(iid As Integer) As Integer
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsRemImage)
        Function CapsRemImage(iid As Integer) As Integer
    End Interface

    ' Python map: src/greaseweazle/image/caps.py::(no direct 1:1 symbol; VB class helper supporting caps image handling)
    Friend Class CapsBackendX64
        Implements CapsBackend

        ' CAPSImg's exports are unsuffixed: `CAPSInit`, `CAPSAddImage`, etc.
        ' The VB methods carry `Native` suffix purely to disambiguate them
        ' from the public CapsBackend wrappers below, so every DllImport
        ' needs an explicit `EntryPoint` to bind to the real export name.
        ' Without it, .NET probes the DLL for `CAPSInitNative` and fails
        ' with an entry-point-not-found error even when the DLL itself
        ' loaded successfully.
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSInit", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSInitNative() As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSAddImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSAddImageNative() As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSLockImage", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Shared Function CAPSLockImageNative(iid As Integer, path As String) As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSLoadImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSLoadImageNative(iid As Integer, flags As UInteger) As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSGetImageInfo", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSGetImageInfoNative(ByRef info As CapsImageInfo, iid As Integer) As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSLockTrack", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSLockTrackNative(ByRef track As CapsTrackInfoT2, iid As Integer, cyl As Integer, head As Integer, flags As UInteger) As Integer
        End Function
        ' Two overloads bind to the same `CAPSGetInfo` export with
        ' different output-struct types (CapsSectorInfo for infoType=1,
        ' CapsDataInfo for infoType=2). The native side uses a tagged
        ' union; the layout is sized correctly for the selected branch
        ' so the marshaller copies the right number of bytes either way.
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSGetInfo", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSGetInfoNative(ByRef info As CapsSectorInfo, iid As Integer, cyl As Integer, head As Integer, infoType As Integer, idx As Integer) As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSGetInfo", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSGetInfoNative(ByRef info As CapsDataInfo, iid As Integer, cyl As Integer, head As Integer, infoType As Integer, idx As Integer) As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSUnlockAllTracks", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSUnlockAllTracksNative(iid As Integer) As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSUnlockImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSUnlockImageNative(iid As Integer) As Integer
        End Function
        <DllImport("CAPSImg_x64.dll", EntryPoint:="CAPSRemImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSRemImageNative(iid As Integer) As Integer
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CapsInit)
        Public Sub CapsInit() Implements CapsBackend.CapsInit
            Dim rc = CAPSInitNative()
            ErrorHandling.Check(rc = 0, "Failure initialising CAPS/SPS library 'CAPSImg_x64.dll'")
        End Sub
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsAddImage)
        Public Function CapsAddImage() As Integer Implements CapsBackend.CapsAddImage
            Return CAPSAddImageNative()
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLockImage)
        Public Function CapsLockImage(iid As Integer, path As String) As Integer Implements CapsBackend.CapsLockImage
            Return CAPSLockImageNative(iid, path)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLoadImage)
        Public Function CapsLoadImage(iid As Integer, flags As UInteger) As Integer Implements CapsBackend.CapsLoadImage
            Return CAPSLoadImageNative(iid, flags)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetImageInfo)
        Public Function CapsGetImageInfo(ByRef info As CapsImageInfo, iid As Integer) As Integer Implements CapsBackend.CapsGetImageInfo
            Return CAPSGetImageInfoNative(info, iid)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLockTrack)
        Public Function CapsLockTrack(ByRef track As CapsTrackInfoT2, iid As Integer, cyl As Integer, head As Integer, flags As UInteger) As Integer Implements CapsBackend.CapsLockTrack
            Return CAPSLockTrackNative(track, iid, cyl, head, flags)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetInfoSector)
        Public Function CapsGetInfoSector(ByRef info As CapsSectorInfo, iid As Integer, cyl As Integer, head As Integer, index As Integer) As Integer Implements CapsBackend.CapsGetInfoSector
            Return CAPSGetInfoNative(info, iid, cyl, head, 1, index)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetInfoWeak)
        Public Function CapsGetInfoWeak(ByRef info As CapsDataInfo, iid As Integer, cyl As Integer, head As Integer, index As Integer) As Integer Implements CapsBackend.CapsGetInfoWeak
            Return CAPSGetInfoNative(info, iid, cyl, head, 2, index)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsUnlockAllTracks)
        Public Function CapsUnlockAllTracks(iid As Integer) As Integer Implements CapsBackend.CapsUnlockAllTracks
            Return CAPSUnlockAllTracksNative(iid)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsUnlockImage)
        Public Function CapsUnlockImage(iid As Integer) As Integer Implements CapsBackend.CapsUnlockImage
            Return CAPSUnlockImageNative(iid)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsRemImage)
        Public Function CapsRemImage(iid As Integer) As Integer Implements CapsBackend.CapsRemImage
            Return CAPSRemImageNative(iid)
        End Function
    End Class

    ' Python map: src/greaseweazle/image/caps.py::(no direct 1:1 symbol; VB class helper supporting caps image handling)
    Friend Class CapsBackendGeneric
        Implements CapsBackend

        ' Same `EntryPoint` plumbing as CapsBackendX64 above; see that
        ' class for the rationale. This generic backend simply targets
        ' `CAPSImg.dll` (no `_x64` suffix) for hosts that ship the
        ' classic / 32-bit-named build.
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSInit", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSInitNative() As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSAddImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSAddImageNative() As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSLockImage", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Shared Function CAPSLockImageNative(iid As Integer, path As String) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSLoadImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSLoadImageNative(iid As Integer, flags As UInteger) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSGetImageInfo", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSGetImageInfoNative(ByRef info As CapsImageInfo, iid As Integer) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSLockTrack", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSLockTrackNative(ByRef track As CapsTrackInfoT2, iid As Integer, cyl As Integer, head As Integer, flags As UInteger) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSGetInfo", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSGetInfoNative(ByRef info As CapsSectorInfo, iid As Integer, cyl As Integer, head As Integer, infoType As Integer, idx As Integer) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSGetInfo", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSGetInfoNative(ByRef info As CapsDataInfo, iid As Integer, cyl As Integer, head As Integer, infoType As Integer, idx As Integer) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSUnlockAllTracks", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSUnlockAllTracksNative(iid As Integer) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSUnlockImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSUnlockImageNative(iid As Integer) As Integer
        End Function
        <DllImport("CAPSImg.dll", EntryPoint:="CAPSRemImage", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Function CAPSRemImageNative(iid As Integer) As Integer
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CapsInit)
        Public Sub CapsInit() Implements CapsBackend.CapsInit
            Dim rc = CAPSInitNative()
            ErrorHandling.Check(rc = 0, "Failure initialising CAPS/SPS library 'CAPSImg.dll'")
        End Sub
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsAddImage)
        Public Function CapsAddImage() As Integer Implements CapsBackend.CapsAddImage
            Return CAPSAddImageNative()
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLockImage)
        Public Function CapsLockImage(iid As Integer, path As String) As Integer Implements CapsBackend.CapsLockImage
            Return CAPSLockImageNative(iid, path)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLoadImage)
        Public Function CapsLoadImage(iid As Integer, flags As UInteger) As Integer Implements CapsBackend.CapsLoadImage
            Return CAPSLoadImageNative(iid, flags)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetImageInfo)
        Public Function CapsGetImageInfo(ByRef info As CapsImageInfo, iid As Integer) As Integer Implements CapsBackend.CapsGetImageInfo
            Return CAPSGetImageInfoNative(info, iid)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsLockTrack)
        Public Function CapsLockTrack(ByRef track As CapsTrackInfoT2, iid As Integer, cyl As Integer, head As Integer, flags As UInteger) As Integer Implements CapsBackend.CapsLockTrack
            Return CAPSLockTrackNative(track, iid, cyl, head, flags)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetInfoSector)
        Public Function CapsGetInfoSector(ByRef info As CapsSectorInfo, iid As Integer, cyl As Integer, head As Integer, index As Integer) As Integer Implements CapsBackend.CapsGetInfoSector
            Return CAPSGetInfoNative(info, iid, cyl, head, 1, index)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsGetInfoWeak)
        Public Function CapsGetInfoWeak(ByRef info As CapsDataInfo, iid As Integer, cyl As Integer, head As Integer, index As Integer) As Integer Implements CapsBackend.CapsGetInfoWeak
            Return CAPSGetInfoNative(info, iid, cyl, head, 2, index)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsUnlockAllTracks)
        Public Function CapsUnlockAllTracks(iid As Integer) As Integer Implements CapsBackend.CapsUnlockAllTracks
            Return CAPSUnlockAllTracksNative(iid)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsUnlockImage)
        Public Function CapsUnlockImage(iid As Integer) As Integer Implements CapsBackend.CapsUnlockImage
            Return CAPSUnlockImageNative(iid)
        End Function
        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration CapsRemImage)
        Public Function CapsRemImage(iid As Integer) As Integer Implements CapsBackend.CapsRemImage
            Return CAPSRemImageNative(iid)
        End Function
    End Class

    ' Python map: src/greaseweazle/image/caps.py::CAPSTrackInfo
    Public Class CapsTrackInfo
        ' Python map: src/greaseweazle/image/caps.py::CAPSTrackInfo.NoTrack
        Public Shared ReadOnly NoTrack As CapsTrackInfo = New CapsTrackInfo()

        ' Python map: src/greaseweazle/image/caps.py::CAPSTrackInfo.__init__
        Public Sub New()
        End Sub
    End Class

    ' Python map: src/greaseweazle/image/caps.py::IPFTrack
    Public Class IpfTrack
        Inherits MasterTrack
        Implements HasVerify

        ' Python map: src/greaseweazle/image/caps.py::IPFTrack.tolerance
        Public Const Tolerance As Integer = 100

        ' Python map: src/greaseweazle/image/caps.py::IPFTrack.sectors
        Public Property Sectors As List(Of Tuple(Of Integer, Integer))

        Public Sub New(bits As IEnumerable(Of Boolean),
                       timePerRev As Double,
                       Optional bitTicks As IEnumerable(Of Double) = Nothing,
                       Optional splice As Integer = 0,
                       Optional weak As IEnumerable(Of Tuple(Of Integer, Integer)) = Nothing)
            MyBase.New(bits, timePerRev, bitTicks, splice, weak)
            Sectors = New List(Of Tuple(Of Integer, Integer))()
        End Sub

        ' Python map: src/greaseweazle/image/caps.py::IPFTrack.verify_revs
        Public ReadOnly Property VerifyRevs As Double Implements HasVerify.VerifyRevs
            Get
                Return 2.0
            End Get
        End Property

        ' Python map: src/greaseweazle/image/caps.py::IPFTrack.strong_data
        '
        ' Yields (start, length) sub-ranges of the supplied per-sector data
        ' regions, with weak-bit areas (and a 16-bit tolerance after each weak
        ' run) clipped out. Equivalent to Python's generator that consumes
        ' two range iterators side-by-side.
        Public Shared Iterator Function StrongData(sector As IList(Of Tuple(Of Integer, Integer)),
                                                   weak As IList(Of Tuple(Of Integer, Integer))) _
                                                   As IEnumerable(Of Tuple(Of Integer, Integer))
            Const weakTol As Integer = 16
            Dim sentinel = Tuple.Create(1 << 30, 1)
            Dim weakList = If(weak, New List(Of Tuple(Of Integer, Integer))())
            Dim sectorList = If(sector, New List(Of Tuple(Of Integer, Integer))())
            Dim wIdx = 0
            Dim ws As Integer = -1
            Dim we As Integer = -1
            Dim sIdx = 0
            If sectorList.Count = 0 Then
                Return
            End If
            Dim s = sectorList(0).Item1
            Dim e = s + sectorList(0).Item2
            sIdx = 1
            Try
                Do
                    While we <= s
                        Dim wRange As Tuple(Of Integer, Integer)
                        If wIdx < weakList.Count Then
                            wRange = weakList(wIdx)
                            wIdx += 1
                        Else
                            wRange = sentinel
                        End If
                        ws = wRange.Item1
                        we = ws + wRange.Item2 + weakTol
                    End While
                    If ws < e Then
                        If s < ws Then
                            Yield Tuple.Create(s, ws - s)
                        End If
                        s = we
                    Else
                        Yield Tuple.Create(s, e - s)
                        s = e
                    End If
                    If s >= e Then
                        If sIdx >= sectorList.Count Then
                            Exit Do
                        End If
                        s = sectorList(sIdx).Item1
                        e = s + sectorList(sIdx).Item2
                        sIdx += 1
                    End If
                Loop
            Catch
                ' Mirrors Python's StopIteration handler: silently terminate.
            End Try
        End Function

        ' Python map: src/greaseweazle/image/caps.py::IPFTrack.verify_track
        Public Function VerifyTrackImpl(flux As Flux) As Boolean Implements HasVerify.VerifyTrack
            If flux Is Nothing Then
                Return False
            End If
            flux.CueAtIndex()
            If Bits Is Nothing OrElse Bits.Count = 0 Then
                Return True
            End If
            Dim raw As New PllTrack(clock:=TimePerRev / Bits.Count, data:=flux)
            Dim rawBits = raw.GetAllData().Item1
            For Each strong In StrongData(Sectors, WeakRanges)
                Dim s = strong.Item1
                Dim l = strong.Item2
                If l <= 0 OrElse s < 0 OrElse s + l > Bits.Count Then
                    Continue For
                End If
                Dim sector As New List(Of Boolean)(l)
                For i = 0 To l - 1
                    sector.Add(Bits(s + i))
                Next
                Dim windowStart = Math.Max(Splice + s - Tolerance, 0)
                Dim windowEnd = Math.Min(rawBits.Count, Splice + s + l + Tolerance)
                If windowEnd - windowStart < l Then
                    Return False
                End If
                If Not ContainsBitPattern(rawBits, windowStart, windowEnd, sector) Then
                    Return False
                End If
            Next
            Return True
        End Function

        ' Python map: src/greaseweazle/image/caps.py::(no direct 1:1 symbol;
        ' bitarray.search() equivalent on List(Of Boolean) windows)
        Private Shared Function ContainsBitPattern(haystack As IList(Of Boolean),
                                                   start As Integer,
                                                   stop_ As Integer,
                                                   needle As IList(Of Boolean)) As Boolean
            Dim n = needle.Count
            If n = 0 Then
                Return True
            End If
            Dim last = stop_ - n
            For i = start To last
                Dim matched = True
                For j = 0 To n - 1
                    If haystack(i + j) <> needle(j) Then
                        matched = False
                        Exit For
                    End If
                Next
                If matched Then
                    Return True
                End If
            Next
            Return False
        End Function
    End Class

    Public Module CapsFunctions
        ' Python map: src/greaseweazle/image/caps.py::open_libcaps
        Public Function OpenLibcaps() As Boolean
            Return True
        End Function

        ' Python map: src/greaseweazle/image/caps.py::get_libcaps
        Public Function GetLibcaps() As String
            Return "CAPSImg"
        End Function
    End Module

End Namespace
