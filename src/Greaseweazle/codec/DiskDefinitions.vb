Imports Greaseweazle.Core
Imports Greaseweazle.Shared

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/codec.py::DiskDef
    Public Class DiskDef

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.__init__
        Public Sub New()
            TrackMap = New Dictionary(Of Tuple(Of Integer, Integer), TrackDef)()
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.cyls
        Public Property Cyls As Integer?
        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.heads
        Public Property Heads As Integer?
        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.track_map
        Public Property TrackMap As Dictionary(Of Tuple(Of Integer, Integer), TrackDef)
        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.tracks
        Public Property Tracks As TrackSet
        ' Python map: no-1:1; VB-only annotation that records the format name the
        ' DiskDef was resolved from. Python carries the name implicitly via
        ' args.format alongside args.fmt_cls; we store it on the DiskDef so callers
        ' that received only the DiskDef can reconstruct the format name (e.g. when
        ' propagating IMG.fmt fallback into OpenImageForWrite).
        Public Property Name As String

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.add_param
        Public Sub AddParam(key As String, value As String)
            Select Case key
                Case "cyls"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 1 AndAlso n <= 255, String.Format("{0} out of range", key))
                    Cyls = n
                Case "heads"
                    Dim n = Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(n >= 1 AndAlso n <= 2, String.Format("{0} out of range", key))
                    Heads = n
                Case Else
                    Throw New FatalException(String.Format("unrecognised disk option: {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.finalise
        Public Sub Finalise()
            ErrorHandling.Check(Cyls.HasValue, "missing cyls")
            ErrorHandling.Check(Heads.HasValue, "missing heads")
            Tracks = New TrackSet(Trackset())
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.trackset
        Public Function Trackset() As String
            Dim value = "c=0"
            If Cyls.GetValueOrDefault() > 1 Then
                value &= "-" & (Cyls.Value - 1).ToString(Globalization.CultureInfo.InvariantCulture)
            End If
            value &= ":h=0"
            If Heads.GetValueOrDefault() > 1 Then
                value &= "-" & (Heads.Value - 1).ToString(Globalization.CultureInfo.InvariantCulture)
            End If
            Return value
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec
            Dim key = Tuple.Create(cyl, head)
            If Not TrackMap.ContainsKey(key) Then
                Return Nothing
            End If
            Return TrackMap(key).MkTrack(cyl, head)
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.decode_flux
        ' Python's read/convert/align tools insert the user's --pll override at
        ' plls[0] so the implicit first decode picks it up; VB's Plls.Values is
        ' immutable, so we accept the override as an explicit parameter instead.
        Public Function DecodeFlux(cyl As Integer, head As Integer, track As HasFlux, Optional pll As Pll = Nothing) As Codec
            Dim codec = MkTrack(cyl, head)
            If codec IsNot Nothing Then
                codec.DecodeFlux(track, pll)
            End If
            Return codec
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef.default_revs
        Public ReadOnly Property DefaultRevs As Double
            Get
                If TrackMap.Count = 0 Then
                    Return 0
                End If
                Return TrackMap.Values.Max(Function(x) x.DefaultRevs)
            End Get
        End Property

    End Class

    ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB registry abstraction replacing Python mk_trackdef dispatch function)
    Public NotInheritable Class CodecRegistry

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB in-memory dispatch table equivalent to mk_trackdef conditional routing)
        Private Shared ReadOnly _factories As New Dictionary(Of String, Func(Of String, TrackDef))(StringComparer.OrdinalIgnoreCase)

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB static initialiser for codec factory registration)
        Shared Sub New()
            Register("ibm.mfm", Function(formatName) New IbmTrackFixedDef(formatName))
            Register("ibm.fm", Function(formatName) New IbmTrackFixedDef(formatName))
            Register("dec.rx02", Function(formatName) New IbmTrackFixedDef(formatName))
            Register("ibm.scan", Function(formatName) New IbmTrackScanDef())
            Register("amiga.amigados", Function(formatName) New AmigaDosDef())
            Register("mac.gcr", Function(formatName) New MacGcrDef())
            Register("c64.gcr", Function(formatName) New C64GcrDef())
            Register("apple2.gcr", Function(formatName) New Apple2GcrDef())
            Register("northstar", Function(formatName) New NorthStarDef())
            Register("micropolis", Function(formatName) New MicropolisDef())
            Register("hp.mmfm", Function(formatName) New HpMmfmDef())
            Register("datageneral", Function(formatName) New DataGeneralDef())
            Register("bitcell", Function(formatName) New BitcellTrackDef())
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB non-instantiable utility class constructor)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB extension point for registering format factories)
        Public Shared Sub Register(formatName As String, factory As Func(Of String, TrackDef))
            _factories(formatName) = factory
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::mk_trackdef
        Public Shared Function MkTrackdef(formatName As String) As TrackDef
            If Not _factories.ContainsKey(formatName) Then
                Throw New FatalException(String.Format("unrecognised format name: {0}", formatName))
            End If
            Return _factories(formatName).Invoke(formatName)
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB helper exposing registered format names)
        Public Shared Function GetFormats() As IEnumerable(Of String)
            Return _factories.Keys.OrderBy(Function(x) x).ToArray()
        End Function
    End Class

End Namespace
