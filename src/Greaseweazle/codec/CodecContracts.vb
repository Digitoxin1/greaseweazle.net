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

    ' Optional add-on contract a Codec can implement when its DecodeFlux
    ' pass produces structured per-track diagnostics that the action
    ' layer should surface as typed events (Read/Write/Convert
    ' UnexpectedSectorIgnored, etc.). Codecs that don't implement this
    ' simply emit no diagnostics. Action layer is expected to call Drain
    ' immediately after each DecodeFlux invocation so the buffer
    ' represents only that pass's findings (Python's diagnostics print
    ' inside decode_flux per-call; mirror semantics).
    '
    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.decode_flux
    '   (the per-call print loop for "Ignoring unexpected sector ..." is
    '   the canonical example -- in Python it's a fire-and-forget print;
    '   in VB the codec buffers and the action layer drains+dispatches.)
    Public Interface HasDecodeDiagnostics
        Function DrainDecodeDiagnostics() As IReadOnlyList(Of CodecDecodeDiagnostic)
    End Interface

    ' Base class for any structured diagnostic a codec produces during a
    ' single DecodeFlux pass. Subclasses are pure data carriers; the
    ' library never formats them. The CLI's per-command formatter
    ' (ReadFormatter / WriteFormatter / ConvertFormatter) is the only
    ' producer of human-readable text.
    '
    ' Python map: src/greaseweazle/codec/ibm/ibm.py::(no direct 1:1 symbol;
    '   Python uses ad-hoc print(...) calls inside decode_flux. VB lifts
    '   those into typed objects so non-CLI hosts can consume them.)
    Public MustInherit Class CodecDecodeDiagnostic
    End Class

    ' Emitted (one entry per unique (C,H,R,N) tuple, per DecodeFlux
    ' pass) when an IBM-fixed codec sees a sector header whose IDAM CRC
    ' is good but whose (cyl, head, sector_id, size_code) doesn't match
    ' any predeclared layout entry for the track. Mirrors Python's
    ' IBMTrack_Fixed.decode_flux "Ignoring unexpected sector ..." print
    ' but as structured data; the CLI formatter renders it with
    ' track-spec context.
    '
    ' Python map: src/greaseweazle/codec/ibm/ibm.py::IBMTrack_Fixed.decode_flux
    Public NotInheritable Class UnexpectedSectorDiagnostic
        Inherits CodecDecodeDiagnostic

        Public Sub New(c As Integer, h As Integer, r As Integer, n As Integer)
            Me.C = c
            Me.H = h
            Me.R = r
            Me.N = n
        End Sub

        ' Cylinder reported in the unexpected IDAM (the C field).
        Public ReadOnly Property C As Integer
        ' Head reported in the unexpected IDAM (the H field).
        Public ReadOnly Property H As Integer
        ' Sector-id (R) reported in the unexpected IDAM.
        Public ReadOnly Property R As Integer
        ' Size code (N) reported in the unexpected IDAM
        ' (sector size in bytes = 128 << N for valid IBM tracks).
        Public ReadOnly Property N As Integer

    End Class

End Namespace
