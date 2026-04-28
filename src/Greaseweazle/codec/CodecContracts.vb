Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/codec.py::Codec
    Public Interface Codec
        Inherits HasFlux

        ' Python map: src/greaseweazle/codec/codec.py::Codec.nsec
        ReadOnly Property Nsec As Integer
        ' Python map: src/greaseweazle/codec/codec.py::Codec.has_sec
        Function HasSec(sectorId As Integer) As Boolean
        ' Python map: src/greaseweazle/codec/codec.py::Codec.nr_missing
        Function NrMissing() As Integer
        ' Python map: src/greaseweazle/codec/codec.py::Codec.get_img_track
        Function GetImgTrack() As Byte()
        ' Python map: src/greaseweazle/codec/codec.py::Codec.set_img_track
        Function SetImgTrack(trackData As Byte()) As Integer
        ' Python map: src/greaseweazle/codec/codec.py::Codec.decode_flux
        Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing)
        ' Python map: src/greaseweazle/codec/codec.py::Codec.master_track
        Function MasterTrack() As MasterTrack
    End Interface

    ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB abstract base centralising shared Codec implementation members)
    Public MustInherit Class CodecBase
        Implements Codec

        ' Python map: src/greaseweazle/codec/codec.py::Codec.nsec
        Public MustOverride ReadOnly Property Nsec As Integer Implements Codec.Nsec
        ' Python map: src/greaseweazle/codec/codec.py::Codec.has_sec
        Public MustOverride Function HasSec(sectorId As Integer) As Boolean Implements Codec.HasSec
        ' Python map: src/greaseweazle/codec/codec.py::Codec.nr_missing
        Public MustOverride Function NrMissing() As Integer Implements Codec.NrMissing
        ' Python map: src/greaseweazle/codec/codec.py::Codec.get_img_track
        Public MustOverride Function GetImgTrack() As Byte() Implements Codec.GetImgTrack
        ' Python map: src/greaseweazle/codec/codec.py::Codec.set_img_track
        Public MustOverride Function SetImgTrack(trackData As Byte()) As Integer Implements Codec.SetImgTrack
        ' Python map: src/greaseweazle/codec/codec.py::Codec.decode_flux
        Public MustOverride Sub DecodeFlux(track As HasFlux, Optional pll As Pll = Nothing) Implements Codec.DecodeFlux
        ' Python map: src/greaseweazle/codec/codec.py::Codec.master_track
        Public MustOverride Function MasterTrack() As MasterTrack Implements Codec.MasterTrack

        ' Python map: src/greaseweazle/codec/codec.py::Codec.flux
        Public Function Flux() As Flux Implements HasFlux.Flux
            Return MasterTrack().Flux()
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::Codec.flux_for_writeout
        Public Function FluxForWriteout(cueAtIndex As Boolean) As WriteoutFlux Implements HasFlux.FluxForWriteout
            Return MasterTrack().FluxForWriteout(cueAtIndex)
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::Codec.summary_string
        Public MustOverride Function SummaryString() As String Implements HasFlux.SummaryString
    End Class

    ' Python map: src/greaseweazle/codec/codec.py::TrackDef
    Public Interface TrackDef
        ' Python map: src/greaseweazle/codec/codec.py::TrackDef.default_revs
        ReadOnly Property DefaultRevs As Double
        ' Python map: src/greaseweazle/codec/codec.py::TrackDef.add_param
        Sub AddParam(key As String, value As String)
        ' Python map: src/greaseweazle/codec/codec.py::TrackDef.finalise
        Sub Finalise()
        ' Python map: src/greaseweazle/codec/codec.py::TrackDef.mk_track
        Function MkTrack(cyl As Integer, head As Integer) As Codec
    End Interface

End Namespace
