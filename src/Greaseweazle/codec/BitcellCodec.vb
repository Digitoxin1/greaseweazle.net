Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrackDef
    Public Class BitcellTrackDef
        Implements TrackDef

        Private _clock As Double?
        Private _timePerRev As Double?

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrackDef.__init__
        Public Sub New()
        End Sub

        Public ReadOnly Property DefaultRevs As Double Implements TrackDef.DefaultRevs
            Get
                Return 1.0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrackDef.add_param
        Public Sub AddParam(key As String, value As String) Implements TrackDef.AddParam
            Select Case key
                Case "secs"
                    ' Accepted for parity with python track-def parser, unused here.
                    Integer.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case "clock"
                    _clock = Double.Parse(value, Globalization.CultureInfo.InvariantCulture) * 1.0E-6
                Case "time_per_rev"
                    _timePerRev = Double.Parse(value, Globalization.CultureInfo.InvariantCulture)
                Case Else
                    Throw New FatalException(String.Format("unrecognised track option {0}", key))
            End Select
        End Sub

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrackDef.finalise
        Public Sub Finalise() Implements TrackDef.Finalise
            ErrorHandling.Check(_clock.HasValue, "clock period not specified")
        End Sub

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrackDef.mk_track
        Public Function MkTrack(cyl As Integer, head As Integer) As Codec Implements TrackDef.MkTrack
            Return New BitcellTrack(cyl, head, _clock.GetValueOrDefault(), _timePerRev)
        End Function
    End Class

    ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack
    Public Class BitcellTrack
        Inherits CodecBase

        Private ReadOnly _cyl As Integer
        Private ReadOnly _head As Integer
        Private ReadOnly _clock As Double
        Private ReadOnly _configuredTimePerRev As Double?
        Private _raw As PllTrack

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.__init__
        Public Sub New(cyl As Integer, head As Integer, clock As Double, configuredTimePerRev As Double?)
            _cyl = cyl
            _head = head
            _clock = clock
            _configuredTimePerRev = configuredTimePerRev
        End Sub

        Public Overrides ReadOnly Property Nsec As Integer
            Get
                Return 0
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.time_per_rev
        Public ReadOnly Property TimePerRev As Double
            Get
                Return EffectiveTimePerRev()
            End Get
        End Property

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.has_sec
        Public Overrides Function HasSec(sectorId As Integer) As Boolean
            Return False
        End Function

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.nr_missing
        Public Overrides Function NrMissing() As Integer
            Return 0
        End Function

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.get_img_track
        Public Overrides Function GetImgTrack() As Byte()
            Return Array.Empty(Of Byte)()
        End Function

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.set_img_track
        Public Overrides Function SetImgTrack(trackData As Byte()) As Integer
            Return 0
        End Function

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.decode_flux
        Public Overrides Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
            Dim flux = track.Flux()
            flux.CueAtIndex()
            Dim tpr = If(_configuredTimePerRev.HasValue, _configuredTimePerRev.Value, flux.TimePerRev)
            _raw = New PllTrack(timePerRev:=tpr, clock:=_clock, data:=flux, pll:=pll)
        End Sub

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.master_track
        Public Overrides Function MasterTrack() As MasterTrack
            If _raw Is Nothing Then
                Dim tpr = EffectiveTimePerRev()
                Dim nbits = CInt(Math.Floor(tpr / _clock / 8.0)) * 8
                Dim weakRanges = New List(Of Tuple(Of Integer, Integer)) From {Tuple.Create(0, nbits)}
                Dim track = New MasterTrack(Enumerable.Repeat(False, nbits), tpr, weak:=weakRanges)
                track.ForceRandomWeak = True
                Return track
            End If
            Dim bits = _raw.GetRevolution(0).Item1
            Return New MasterTrack(bits, EffectiveTimePerRev())
        End Function

        ' Python map: src/greaseweazle/codec/bitcell.py::BitcellTrack.summary_string
        Public Overrides Function SummaryString() As String
            If _raw Is Nothing Then
                Return "Raw Bitcell (empty)"
            End If
            Dim bits = _raw.GetRevolution(0).Item1
            ' Python's `f'{x:.2f}'` is locale-independent; force InvariantCulture
            ' so non-English Windows hosts don't render `1,50ms`.
            Return String.Format(Globalization.CultureInfo.InvariantCulture,
                                 "Raw Bitcell ({0} bits, {1:F2}ms)", bits.Count, _raw.TimePerRev * 1000.0)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EffectiveTimePerRev)
        Private Function EffectiveTimePerRev() As Double
            If _raw IsNot Nothing Then
                Return _raw.TimePerRev.GetValueOrDefault(0.2)
            End If
            If _configuredTimePerRev.HasValue Then
                Return _configuredTimePerRev.Value
            End If
            Return 0.2
        End Function
    End Class

End Namespace
