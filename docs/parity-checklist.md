# Python -> VB Parity Checklist

## Summary

- Total symbols: **786**
- Mapped exact (`Python map:` comment): **750**
- Mapped normalized (renamed, unique scoped match): **9**
- Name-only ambiguous (multiple candidates, no `Python map:`): **2**
- Truly missing: **25**
- By kind:
  - `class`: total 164 | exact 160 | normalized 0 | ambiguous 0 | missing 4
  - `enum`: total 4 | exact 4 | normalized 0 | ambiguous 0 | missing 0
  - `function`: total 96 | exact 80 | normalized 0 | ambiguous 0 | missing 16
  - `method`: total 522 | exact 506 | normalized 9 | ambiguous 2 | missing 5
- Events: Python AST scan found no event symbols (Python has no native event declaration construct).

Status taxonomy:
- `mapped-exact`: a `' Python map: <py_module>::<symbol>` comment binds the symbol to a specific VB declaration. Highest confidence.
- `mapped-normalized`: no `Python map:` comment, but exactly one VB declaration in the scoped VB file matches the normalized name (snake_case -> PascalCase, `__init__` -> `New`, `__str__` -> `ToString`). Medium confidence.
- `name-only-ambiguous`: multiple normalized matches exist; not uniquely bindable without a `Python map:` comment. Low confidence.
- `missing`: no exact and no normalized match.

## `src/greaseweazle/cli.py`

- [ ] `usage` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [ ] `main` | kind: `function` | mapped VB: _(none)_ | status: `missing`

## `src/greaseweazle/codec/amiga/amigados.py`

- [x] `AmigaDOS` | kind: `class` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::AmigaDos` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.exists` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::Exists` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `AmigaDOS.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `AmigaDOS_DD` | kind: `class` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::AmigaDosDd` (exact) | status: `mapped-exact`
- [x] `AmigaDOS_HD` | kind: `class` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::AmigaDosHd` (exact) | status: `mapped-exact`
- [x] `AmigaDOSDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::AmigaDosDef` (exact) | status: `mapped-exact`
- [x] `AmigaDOSDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `AmigaDOSDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `AmigaDOSDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `AmigaDOSDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::MkTrack` (exact) | status: `mapped-exact`
- [x] `encode` | kind: `function` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::Encode` (exact) | status: `mapped-exact`
- [x] `decode` | kind: `function` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::Decode` (exact) | status: `mapped-exact`
- [x] `checksum` | kind: `function` | mapped VB: `src/Greaseweazle/codec/amiga/AmigaDosCodec.vb::Checksum` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/apple2/apple2_gcr.py`

- [x] `Apple2GCR` | kind: `class` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::Apple2Gcr` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.set_vol_id` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::SetVolId` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.tracknr` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::TrackNr` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `Apple2GCR.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `Apple2GCRDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::Apple2GcrDef` (exact) | status: `mapped-exact`
- [x] `Apple2GCRDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `Apple2GCRDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `Apple2GCRDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `Apple2GCRDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/apple2/Apple2GcrCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/bitcell.py`

- [x] `BitcellTrack` | kind: `class` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::BitcellTrack` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.time_per_rev` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::TimePerRev` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `BitcellTrack.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `BitcellTrackDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::BitcellTrackDef` (exact) | status: `mapped-exact`
- [x] `BitcellTrackDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `BitcellTrackDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `BitcellTrackDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `BitcellTrackDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/BitcellCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/codec.py`

- [x] `Codec` | kind: `class` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::Codec` (exact) | status: `mapped-exact`
- [x] `Codec.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::Nsec` (exact); `src/Greaseweazle/codec/CodecContracts.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `Codec.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `Codec.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::HasSec` (exact); `src/Greaseweazle/codec/CodecContracts.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `Codec.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::NrMissing` (exact); `src/Greaseweazle/codec/CodecContracts.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `Codec.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::GetImgTrack` (exact); `src/Greaseweazle/codec/CodecContracts.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `Codec.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::SetImgTrack` (exact); `src/Greaseweazle/codec/CodecContracts.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `Codec.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::DecodeFlux` (exact); `src/Greaseweazle/codec/CodecContracts.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `Codec.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::MasterTrack` (exact); `src/Greaseweazle/codec/CodecContracts.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `Codec.flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::Flux` (exact) | status: `mapped-exact`
- [x] `Codec.flux_for_writeout` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::FluxForWriteout` (exact) | status: `mapped-exact`
- [x] `TrackDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::TrackDef` (exact) | status: `mapped-exact`
- [x] `TrackDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `TrackDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `TrackDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/CodecContracts.vb::MkTrack` (exact) | status: `mapped-exact`
- [x] `DiskDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::DiskDef` (exact) | status: `mapped-exact`
- [x] `DiskDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::New` (exact) | status: `mapped-exact`
- [x] `DiskDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `DiskDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `DiskDef.trackset` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::Trackset` (exact) | status: `mapped-exact`
- [x] `DiskDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::MkTrack` (exact) | status: `mapped-exact`
- [x] `DiskDef.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `DiskDef.default_revs` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::DefaultRevs` (exact) | status: `mapped-exact`
- [x] `DiskDef_File` | kind: `class` | mapped VB: `src/Greaseweazle/codec/DiskDefParser.vb::DiskDefFile` (exact) | status: `mapped-exact`
- [x] `DiskDef_File.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/DiskDefParser.vb::New` (exact) | status: `mapped-exact`
- [x] `mk_trackdef` | kind: `function` | mapped VB: `src/Greaseweazle/codec/DiskDefinitions.vb::MkTrackdef` (exact) | status: `mapped-exact`
- [x] `ParseMode` | kind: `class` | mapped VB: `src/Greaseweazle/codec/DiskDefParser.vb::ParseMode` (exact) | status: `mapped-exact`
- [x] `_get_diskdef` | kind: `function` | mapped VB: `src/Greaseweazle/codec/DiskDefParser.vb::GetDiskdefInner` (exact) | status: `mapped-exact`
- [x] `get_diskdef` | kind: `function` | mapped VB: `src/Greaseweazle/codec/DiskDefParser.vb::GetDiskdef` (exact) | status: `mapped-exact`
- [x] `get_all_formats` | kind: `function` | mapped VB: `src/Greaseweazle/codec/DiskDefParser.vb::CollectAllFormats` (exact) | status: `mapped-exact`
- [x] `print_formats` | kind: `function` | mapped VB: `src/Greaseweazle/codec/DiskDefParser.vb::GetAllFormats` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/commodore/c64_gcr.py`

- [x] `C64GCR` | kind: `class` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::C64Gcr` (exact) | status: `mapped-exact`
- [x] `C64GCR.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `C64GCR.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `C64GCR.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `C64GCR.set_disk_id` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::SetDiskId` (exact) | status: `mapped-exact`
- [x] `C64GCR.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `C64GCR.tracknr` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::TrackNr` (exact) | status: `mapped-exact`
- [x] `C64GCR.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `C64GCR.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `C64GCR.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `C64GCR.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `C64GCR.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `C64GCR.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `C64GCR.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `C64GCRDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::C64GcrDef` (exact) | status: `mapped-exact`
- [x] `C64GCRDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `C64GCRDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `C64GCRDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `C64GCRDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/commodore/C64GcrCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/datageneral/datageneral.py`

- [x] `csum` | kind: `function` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::DataGeneralChecksum` (exact) | status: `mapped-exact`
- [x] `DataGeneral` | kind: `class` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::DataGeneral` (exact) | status: `mapped-exact`
- [x] `DataGeneral.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `DataGeneral.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `DataGeneral.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `DataGeneral.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `DataGeneral.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `DataGeneral.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `DataGeneral.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `DataGeneral.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `DataGeneral.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `DataGeneral.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `DataGeneralDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::DataGeneralDef` (exact) | status: `mapped-exact`
- [x] `DataGeneralDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `DataGeneralDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `DataGeneralDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `DataGeneralDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/datageneral/DataGeneralCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/hp/hp_mmfm.py`

- [x] `bitrev` | kind: `function` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::BitRev` (exact) | status: `mapped-exact`
- [x] `mmfm_encode` | kind: `function` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::MmfmEncode` (exact) | status: `mapped-exact`
- [x] `HPMMFM` | kind: `class` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::HpMmfm` (exact) | status: `mapped-exact`
- [x] `HPMMFM.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `HPMMFM.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `HPMMFM.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `HPMMFM.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `HPMMFM.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `HPMMFM.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `HPMMFM.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `HPMMFM.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `HPMMFM.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::DecodeFlux` (exact); `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `HPMMFM.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `HPMMFM.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `HPMMFMDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::HpMmfmDef` (exact) | status: `mapped-exact`
- [x] `HPMMFMDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `HPMMFMDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `HPMMFMDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `HPMMFMDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/hp/HpMmfmCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/ibm/ibm.py`

- [x] `sync` | kind: `function` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Sync` (exact) | status: `mapped-exact`
- [x] `fm_encode` | kind: `function` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::FmEncode` (exact) | status: `mapped-exact`
- [x] `mfm_encode` | kind: `function` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::MfmEncode` (exact) | status: `mapped-exact`
- [x] `encode` | kind: `function` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Encode` (exact) | status: `mapped-exact`
- [x] `decode` | kind: `function` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Decode` (exact) | status: `mapped-exact`
- [x] `sec_map` | kind: `function` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::SecMap` (exact) | status: `mapped-exact`
- [x] `sec_sz` | kind: `function` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::SecSz` (exact) | status: `mapped-exact`
- [x] `Gaps` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Gaps` (exact) | status: `mapped-exact`
- [x] `Gaps.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `Mark` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Mark` (exact) | status: `mapped-exact`
- [x] `Mode` | kind: `enum` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::IbmMode` (exact) | status: `mapped-exact`
- [x] `Mode.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::ToPythonString` (exact); `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::ModeDisplayName` (exact) | status: `mapped-exact`
- [x] `TrackArea` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::TrackArea` (exact) | status: `mapped-exact`
- [x] `TrackArea.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `TrackArea.delta` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Delta` (exact) | status: `mapped-exact`
- [x] `TrackArea.__eq__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Equals` (exact) | status: `mapped-exact`
- [x] `IDAM` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Idam` (exact) | status: `mapped-exact`
- [x] `IDAM.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `IDAM.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::ToString` (exact) | status: `mapped-exact`
- [x] `IDAM.__eq__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Equals` (exact) | status: `mapped-exact`
- [x] `IDAM.__copy__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Copy` (exact) | status: `mapped-exact`
- [x] `DAM` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Dam` (exact) | status: `mapped-exact`
- [x] `DAM.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `DAM.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::ToString` (exact) | status: `mapped-exact`
- [x] `DAM.__eq__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Equals` (exact) | status: `mapped-exact`
- [x] `DAM.__copy__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Copy` (exact) | status: `mapped-exact`
- [x] `Sector` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Sector` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::ScanSector` (exact) | status: `mapped-exact`
- [x] `Sector.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `Sector.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::ToString` (exact) | status: `mapped-exact`
- [x] `Sector.delta` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Delta` (exact) | status: `mapped-exact`
- [x] `Sector.__eq__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Equals` (exact) | status: `mapped-exact`
- [x] `IAM` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Iam` (exact) | status: `mapped-exact`
- [x] `IAM.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::ToString` (exact) | status: `mapped-exact`
- [x] `IAM.__copy__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Copy` (exact) | status: `mapped-exact`
- [x] `DEC_MMFM` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::DecMmfm` (exact) | status: `mapped-exact`
- [x] `DEC_MMFM.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `DEC_MMFM.encode` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Encode` (exact) | status: `mapped-exact`
- [x] `DEC_MMFM.decode` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Decode` (exact) | status: `mapped-exact`
- [x] `IBMTrack` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::IbmTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `IBMTrack.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Nsec` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `IBMTrack.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::SummaryString` (exact); `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::SummaryString` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `IBMTrack.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::HasSec` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `IBMTrack.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::NrMissing` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `IBMTrack.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::SetImgTrack` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::GetImgTrack` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::VerifyTrack` (exact); `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::VerifyTrack` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::VerifyTrackInterface` (exact) | status: `mapped-exact`
- [x] `IBMTrack.mfm_master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::MfmMasterTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack.fm_master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::FmMasterTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack.mfm_decode_raw` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::MfmDecodeRaw` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::ParseMfm` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::NormaliseToRevolutions` (exact) | status: `mapped-exact`
- [x] `IBMTrack.fm_decode_raw` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::FmDecodeRaw` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::ParseFm` (exact) | status: `mapped-exact`
- [x] `IBMTrack.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `IBMTrack.decode_raw` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::DecodeRaw` (exact); `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::MergeScan` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Fixed` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::IbmTrackFixed` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Fixed.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Fixed.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/actions/Events/CommonEvents.vb::UnexpectedSectorEventArgs` (exact); `src/Greaseweazle/codec/CodecContracts.vb::HasDecodeDiagnostics` (exact); `src/Greaseweazle/codec/CodecContracts.vb::UnexpectedSectorDiagnostic` (exact); `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::DrainDecodeDiagnostics` (exact); `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::DecodeFlux` (exact); `src/Greaseweazle/tools/ReadWrite.vb::DrainUnexpectedSectors` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Fixed.from_config` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::FromConfig` (exact) | status: `mapped-exact`
- [x] `IBMTrack_FixedDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::IbmTrackFixedDef` (exact) | status: `mapped-exact`
- [x] `IBMTrack_FixedDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `IBMTrack_FixedDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `IBMTrack_FixedDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `IBMTrack_FixedDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::MkTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Empty` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::IbmTrackEmpty` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Empty.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Empty.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Empty.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMFixedCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Scan` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::IbmTrackScan` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Scan.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Scan.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::Nsec` (normalized) | status: `mapped-normalized`
- [x] `IBMTrack_Scan.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::SummaryString` (normalized) | status: `mapped-normalized`
- [x] `IBMTrack_Scan.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::HasSec` (normalized) | status: `mapped-normalized`
- [x] `IBMTrack_Scan.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::NrMissing` (normalized) | status: `mapped-normalized`
- [x] `IBMTrack_Scan.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `IBMTrack_Scan.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::SetImgTrack` (normalized) | status: `mapped-normalized`
- [x] `IBMTrack_Scan.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::GetImgTrack` (normalized) | status: `mapped-normalized`
- [x] `IBMTrack_Scan.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `IBMTrack_ScanDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::IbmTrackScanDef` (exact) | status: `mapped-exact`
- [x] `IBMTrack_ScanDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `IBMTrack_ScanDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `IBMTrack_ScanDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `IBMTrack_ScanDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/ibm/IBMScanCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/macintosh/mac_gcr.py`

- [x] `MacGCR` | kind: `class` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::MacGcr` (exact) | status: `mapped-exact`
- [x] `MacGCR.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `MacGCR.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `MacGCR.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `MacGCR.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `MacGCR.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `MacGCR.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `MacGCR.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `MacGCR.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `MacGCR.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `MacGCR.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `MacGCR.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `MacGCRDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::MacGcrDef` (exact) | status: `mapped-exact`
- [x] `MacGCRDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `MacGCRDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `MacGCRDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `MacGCRDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/macintosh/MacGcrCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/micropolis/micropolis.py`

- [x] `micropolis_csum` | kind: `function` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::MicropolisCsum` (exact) | status: `mapped-exact`
- [x] `Micropolis` | kind: `class` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::Micropolis` (exact) | status: `mapped-exact`
- [x] `Micropolis.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `Micropolis.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `Micropolis.img_bps` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::ImgBps` (exact) | status: `mapped-exact`
- [x] `Micropolis.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `Micropolis.bad_sector` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::BadSector` (exact) | status: `mapped-exact`
- [x] `Micropolis.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `Micropolis.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `Micropolis.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `Micropolis.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `Micropolis.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `Micropolis.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `Micropolis.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `Micropolis.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `MicropolisDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::MicropolisDef` (exact) | status: `mapped-exact`
- [x] `MicropolisDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `MicropolisDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `MicropolisDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `MicropolisDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/micropolis/MicropolisCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/codec/northstar/northstar.py`

- [x] `csum` | kind: `function` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::Csum` (exact) | status: `mapped-exact`
- [x] `Mode` | kind: `enum` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::Mode` (exact) | status: `mapped-exact`
- [x] `Mode.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::ToPythonString` (exact) | status: `mapped-exact`
- [x] `NorthStar` | kind: `class` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::NorthStar` (exact) | status: `mapped-exact`
- [x] `NorthStar.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `NorthStar.nsec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::Nsec` (exact) | status: `mapped-exact`
- [x] `NorthStar.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `NorthStar.add` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::Add` (exact) | status: `mapped-exact`
- [x] `NorthStar.has_sec` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::HasSec` (exact) | status: `mapped-exact`
- [x] `NorthStar.nr_missing` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::NrMissing` (exact) | status: `mapped-exact`
- [x] `NorthStar.get_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::GetImgTrack` (exact) | status: `mapped-exact`
- [x] `NorthStar.set_img_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::SetImgTrack` (exact) | status: `mapped-exact`
- [x] `NorthStar.decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::DecodeFlux` (exact) | status: `mapped-exact`
- [x] `NorthStar.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `NorthStar.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::VerifyTrackInterface` (exact); `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `NorthStarDef` | kind: `class` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::NorthStarDef` (exact) | status: `mapped-exact`
- [x] `NorthStarDef.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::New` (exact) | status: `mapped-exact`
- [x] `NorthStarDef.add_param` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::AddParam` (exact) | status: `mapped-exact`
- [x] `NorthStarDef.finalise` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::Finalise` (exact) | status: `mapped-exact`
- [x] `NorthStarDef.mk_track` | kind: `method` | mapped VB: `src/Greaseweazle/codec/northstar/NorthStarCodec.vb::MkTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/error.py`

- [x] `Fatal` | kind: `class` | mapped VB: `src/Greaseweazle/error/ErrorHandling.vb::Fatal` (exact) | status: `mapped-exact`
- [x] `check` | kind: `function` | mapped VB: `src/Greaseweazle/error/ErrorHandling.vb::Check` (exact) | status: `mapped-exact`

## `src/greaseweazle/flux.py`

- [x] `HasFlux` | kind: `class` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::HasFlux` (exact) | status: `mapped-exact`
- [x] `HasFlux.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `HasFlux.flux` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::Flux` (exact) | status: `mapped-exact`
- [x] `HasFlux.flux_for_writeout` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::FluxForWriteout` (exact) | status: `mapped-exact`
- [x] `Flux` | kind: `class` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::Flux` (exact) | status: `mapped-exact`
- [x] `Flux.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::New` (exact) | status: `mapped-exact`
- [x] `Flux.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::ToString` (exact) | status: `mapped-exact`
- [x] `Flux.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `Flux.identify_hard_sectors` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::IdentifyHardSectors` (exact) | status: `mapped-exact`
- [x] `Flux.append` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::Append` (exact) | status: `mapped-exact`
- [x] `Flux.cue_at_index` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::CueAtIndex` (exact) | status: `mapped-exact`
- [x] `Flux.reverse` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::Reverse` (exact) | status: `mapped-exact`
- [x] `Flux.set_nr_revs` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::SetNrRevs` (exact) | status: `mapped-exact`
- [x] `Flux.flux_for_writeout` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::FluxForWriteout` (exact) | status: `mapped-exact`
- [x] `Flux.flux` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::Flux` (exact) | status: `mapped-exact`
- [x] `Flux.scale` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::Scale` (exact) | status: `mapped-exact`
- [x] `Flux.ticks_per_rev` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::TicksPerRev` (exact) | status: `mapped-exact`
- [x] `Flux.time_per_rev` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::TimePerRev` (exact) | status: `mapped-exact`
- [x] `WriteoutFlux` | kind: `class` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::WriteoutFlux` (exact) | status: `mapped-exact`
- [x] `WriteoutFlux.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::New` (exact) | status: `mapped-exact`
- [x] `WriteoutFlux.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::ToString` (exact) | status: `mapped-exact`
- [x] `WriteoutFlux.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/flux/FluxModel.vb::SummaryString` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/a2r.py`

- [x] `A2RCapType` | kind: `class` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::A2RCapType` (exact) | status: `mapped-exact`
- [x] `A2RTrack` | kind: `class` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::A2RTrack` (exact) | status: `mapped-exact`
- [x] `A2RTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::New` (exact) | status: `mapped-exact`
- [x] `A2RTrack.add_cap` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::AddCap` (exact) | status: `mapped-exact`
- [x] `A2RTrack.best_cap` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::BestCap` (exact) | status: `mapped-exact`
- [x] `A2RTrack.flux` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::Flux` (exact) | status: `mapped-exact`
- [x] `A2R` | kind: `class` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::A2R` (exact) | status: `mapped-exact`
- [x] `A2R.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::New` (exact) | status: `mapped-exact`
- [x] `A2R.process_rwcp` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::ProcessRwcp` (exact) | status: `mapped-exact`
- [x] `A2R.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `A2R.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/A2RImage.vb::GetTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/acorn.py`

- [x] `SSD` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Ssd` (exact) | status: `mapped-exact`
- [x] `DSD` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Dsd` (exact) | status: `mapped-exact`
- [x] `ADS` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Ads` (exact) | status: `mapped-exact`
- [x] `ADM` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Adm` (exact) | status: `mapped-exact`
- [x] `ADL` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Adl` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/adf.py`

- [x] `ADF` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Adf` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/apple2.py`

- [x] `DO` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Po` (exact) | status: `mapped-exact`
- [x] `PO` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Po` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/apridisk.py`

- [x] `Apridisk` | kind: `class` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::Apridisk` (exact) | status: `mapped-exact`
- [x] `Apridisk.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `ApridiskRecord` | kind: `class` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::ApridiskRecord` (exact) | status: `mapped-exact`
- [x] `ApridiskRecord.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::New` (exact) | status: `mapped-exact`
- [x] `ApridiskRecord.split_apridisk_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::SplitApridiskFile` (exact) | status: `mapped-exact`
- [x] `ApridiskRecord.expand_record` | kind: `method` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::ExpandRecord` (exact) | status: `mapped-exact`
- [x] `ApridiskRecord._match_compression` | kind: `method` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::MatchCompression` (exact) | status: `mapped-exact`
- [x] `ApridiskRecord._match_pos` | kind: `method` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::MatchPos` (exact) | status: `mapped-exact`
- [x] `ApridiskSecType` | kind: `enum` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::ApridiskSecType` (exact) | status: `mapped-exact`
- [x] `ApridiskSecType.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/ApridiskImage.vb::FromBytes` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/caps.py`

- [x] `CapsDateTimeExt` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CapsDateTimeExt` (exact) | status: `mapped-exact`
- [x] `CapsImageInfo` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CapsImageInfo` (exact) | status: `mapped-exact`
- [x] `CapsTrackInfoT2` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CapsTrackInfoT2` (exact) | status: `mapped-exact`
- [x] `CapsSectorInfo` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CapsSectorInfo` (exact) | status: `mapped-exact`
- [x] `CapsDataInfo` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CapsDataInfo` (exact) | status: `mapped-exact`
- [x] `DI_LOCK` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::Pi` (exact) | status: `mapped-exact`
- [x] `CAPSTrackInfo` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CapsTrackInfo` (exact) | status: `mapped-exact`
- [x] `CAPSTrackInfo.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::New` (exact) | status: `mapped-exact`
- [x] `CAPS` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CAPS` (exact) | status: `mapped-exact`
- [x] `CAPS.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::New` (exact) | status: `mapped-exact`
- [x] `CAPS.__del__` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::Finalize` (exact) | status: `mapped-exact`
- [x] `CAPS.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::ToString` (exact) | status: `mapped-exact`
- [x] `CAPS.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::GetTrackBase` (exact) | status: `mapped-exact`
- [x] `CAPS.from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::FromFile` (exact) | status: `mapped-exact`
- [x] `CTRaw` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::CTRaw` (exact) | status: `mapped-exact`
- [x] `CTRaw.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::ToString` (exact) | status: `mapped-exact`
- [x] `CTRaw.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `IPFTrack` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::IpfTrack` (exact) | status: `mapped-exact`
- [x] `IPFTrack.strong_data` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::StrongData` (exact) | status: `mapped-exact`
- [x] `IPFTrack.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::VerifyTrackImpl` (exact) | status: `mapped-exact`
- [x] `IPF` | kind: `class` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::IPF` (exact) | status: `mapped-exact`
- [x] `IPF.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::FormatPlatformList` (exact); `src/Greaseweazle/image/CAPSImage.vb::ToString` (exact) | status: `mapped-exact`
- [x] `IPF.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `open_libcaps` | kind: `function` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::OpenLibcaps` (exact) | status: `mapped-exact`
- [x] `get_libcaps` | kind: `function` | mapped VB: `src/Greaseweazle/image/CAPSImage.vb::GetLibcaps` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/d64.py`

- [x] `D64` | kind: `class` | mapped VB: `src/Greaseweazle/image/D64Image.vb::D64` (exact) | status: `mapped-exact`
- [x] `D64.get_disk_id` | kind: `method` | mapped VB: `src/Greaseweazle/image/D64Image.vb::GetDiskId` (exact) | status: `mapped-exact`
- [x] `D64.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/D64Image.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `D71` | kind: `class` | mapped VB: `src/Greaseweazle/image/D64Image.vb::D71` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/d81.py`

- [x] `D81` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::D81` (exact) | status: `mapped-exact`
- [x] `D1M` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::D1M` (exact) | status: `mapped-exact`
- [x] `D2M` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::D2M` (exact) | status: `mapped-exact`
- [x] `D4M` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::D4M` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/d88.py`

- [x] `D88Opts` | kind: `class` | mapped VB: `src/Greaseweazle/image/D88Image.vb::D88Opts` (exact) | status: `mapped-exact`
- [x] `D88Opts.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::New` (exact) | status: `mapped-exact`
- [x] `D88Opts.index` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::Index` (exact) | status: `mapped-exact`
- [x] `D88Opts.index` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::Index` (exact) | status: `mapped-exact`
- [x] `D88` | kind: `class` | mapped VB: `src/Greaseweazle/image/D88Image.vb::D88` (exact) | status: `mapped-exact`
- [x] `D88.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::New` (exact) | status: `mapped-exact`
- [x] `D88.remove_duplicate_sectors` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::RemoveDuplicateSectors` (exact) | status: `mapped-exact`
- [x] `D88.track_from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::TrackFromFile` (exact) | status: `mapped-exact`
- [x] `D88.disk_from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::DiskFromFile` (exact) | status: `mapped-exact`
- [x] `D88.from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::FromFile` (exact) | status: `mapped-exact`
- [x] `D88.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/D88Image.vb::GetTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/dcp.py`

- [x] `DCP` | kind: `class` | mapped VB: `src/Greaseweazle/image/DCPImage.vb::Dcp` (exact) | status: `mapped-exact`
- [x] `DCP.format_from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/DCPImage.vb::FormatFromFile` (exact) | status: `mapped-exact`
- [x] `DCP.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/DCPImage.vb::FromBytes` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/dim.py`

- [x] `DIM` | kind: `class` | mapped VB: `src/Greaseweazle/image/DIMImage.vb::New` (exact) | status: `mapped-exact`
- [x] `DIM.format_from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/DIMImage.vb::FormatFromFile` (exact) | status: `mapped-exact`
- [x] `DIM.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/DIMImage.vb::FromBytes` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/dmk.py`

- [x] `Encoding` | kind: `class` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::New` (exact) | status: `mapped-exact`
- [x] `Encoding.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::New` (exact) | status: `mapped-exact`
- [x] `Encoding.move_cursor` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::MoveCursor` (exact) | status: `mapped-exact`
- [x] `Encoding._off` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::ApplyOffAreas` (exact) | status: `mapped-exact`
- [x] `Encoding.mfm_off` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::MfmOff` (exact) | status: `mapped-exact`
- [x] `Encoding.fm_off` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::FmOff` (exact) | status: `mapped-exact`
- [x] `DMKTrack` | kind: `class` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::DmkTrack` (exact) | status: `mapped-exact`
- [x] `DMKTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::New` (exact) | status: `mapped-exact`
- [x] `DMKTrack.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `DMK` | kind: `class` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::Dmk` (exact) | status: `mapped-exact`
- [x] `DMK.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::New` (exact) | status: `mapped-exact`
- [x] `DMK.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `DMK.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/DMKImage.vb::GetTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/dsk.py`

- [x] `DSK` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Dsk` (exact) | status: `mapped-exact`
- [x] `DSK.from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::FromFile` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/edsk.py`

- [x] `EDSKRate` | kind: `class` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::EdskRate` (exact) | status: `mapped-exact`
- [x] `SR1` | kind: `class` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::Sr1` (exact) | status: `mapped-exact`
- [x] `SR2` | kind: `class` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::Sr2` (exact) | status: `mapped-exact`
- [x] `SectorErrors` | kind: `class` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::SectorErrors` (exact) | status: `mapped-exact`
- [x] `SectorErrors.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::New` (exact) | status: `mapped-exact`
- [x] `EDSKTrack` | kind: `class` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::EdskTrack` (exact) | status: `mapped-exact`
- [x] `EDSKTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::New` (exact) | status: `mapped-exact`
- [x] `EDSKTrack.master_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `EDSKTrack._find_sync` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::FindSync` (exact) | status: `mapped-exact`
- [x] `EDSKTrack.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `EDSK` | kind: `class` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::Edsk` (exact) | status: `mapped-exact`
- [x] `EDSK.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::New` (exact) | status: `mapped-exact`
- [x] `EDSK.find_weak_ranges` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::FindWeakRanges` (exact) | status: `mapped-exact`
- [x] `EDSK._build_8k_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::Build8KTrack` (exact) | status: `mapped-exact`
- [x] `EDSK._build_kbi19_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::BuildKbi19Track` (exact) | status: `mapped-exact`
- [x] `EDSK.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `EDSK.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `EDSK.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::EmitTrack` (exact) | status: `mapped-exact`
- [x] `EDSK.get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/EDSKImage.vb::GetImage` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/fd.py`

- [x] `FD` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Fd` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/fdi.py`

- [x] `FDI` | kind: `class` | mapped VB: `src/Greaseweazle/image/FDIImage.vb::Fdi` (exact) | status: `mapped-exact`
- [x] `FDI.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/FDIImage.vb::FromBytes` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/hdm.py`

- [x] `HDM` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Hdm` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/hfe.py`

- [x] `HFEOpts` | kind: `class` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::HfeOpts` (exact) | status: `mapped-exact`
- [x] `HFEOpts.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::New` (exact) | status: `mapped-exact`
- [x] `HFEOpts.bitrate` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Bitrate` (exact) | status: `mapped-exact`
- [x] `HFEOpts.bitrate` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Bitrate` (exact) | status: `mapped-exact`
- [x] `HFEOpts.version` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Version` (exact) | status: `mapped-exact`
- [x] `HFEOpts.version` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Version` (exact) | status: `mapped-exact`
- [x] `HFEOpts.interface` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Interface` (exact) | status: `mapped-exact`
- [x] `HFEOpts.interface` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Interface` (exact) | status: `mapped-exact`
- [x] `HFEOpts.encoding` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Encoding` (exact) | status: `mapped-exact`
- [x] `HFEOpts.encoding` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Encoding` (exact) | status: `mapped-exact`
- [ ] `HFETrack` | kind: `class` | mapped VB: _(none)_ | status: `missing`
- [ ] `HFETrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::New` (ambiguous); `src/Greaseweazle/image/HFEImage.vb::New` (ambiguous); `src/Greaseweazle/image/HFEImage.vb::New` (ambiguous); `src/Greaseweazle/image/HFEImage.vb::New` (ambiguous); `src/Greaseweazle/image/HFEImage.vb::New` (ambiguous); `src/Greaseweazle/image/HFEImage.vb::New` (ambiguous) | status: `name-only-ambiguous`
- [ ] `HFETrack.from_bitarray` | kind: `method` | mapped VB: _(none)_ | status: `missing`
- [ ] `HFETrack.from_hfe_bytes` | kind: `method` | mapped VB: _(none)_ | status: `missing`
- [ ] `HFETrack.to_hfe_bytes` | kind: `method` | mapped VB: _(none)_ | status: `missing`
- [x] `HFE` | kind: `class` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Hfe` (exact) | status: `mapped-exact`
- [x] `HFE.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::New` (exact) | status: `mapped-exact`
- [x] `HFE.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `HFE.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `HFE.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::EmitTrack` (exact); `src/Greaseweazle/image/HFEImage.vb::ShouldUseDoubleRate` (exact) | status: `mapped-exact`
- [x] `HFE.hfev1_get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::BuildHfev1Image` (exact) | status: `mapped-exact`
- [x] `HFE.get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::GetImage` (exact) | status: `mapped-exact`
- [x] `HFEv3_Op` | kind: `class` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Hfev3Op` (exact) | status: `mapped-exact`
- [x] `HFEv3_Range` | kind: `class` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Hfev3Range` (exact) | status: `mapped-exact`
- [x] `HFEv3_Range.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::New` (exact) | status: `mapped-exact`
- [x] `HFEv3_Range.e` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::E` (exact) | status: `mapped-exact`
- [ ] `hfev3_mk_track` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `HFEv3_Chunk` | kind: `class` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Hfev3Chunk` (exact) | status: `mapped-exact`
- [x] `HFEv3_Chunk.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::New` (exact) | status: `mapped-exact`
- [x] `HFEv3_Chunk.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::ToString` (exact) | status: `mapped-exact`
- [x] `HFEv3_Generator` | kind: `class` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::Hfev3Generator` (exact) | status: `mapped-exact`
- [x] `HFEv3_Generator.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::New` (exact) | status: `mapped-exact`
- [x] `HFEv3_Generator.next_chunk` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::NextChunk` (exact) | status: `mapped-exact`
- [x] `HFEv3_Generator.increment_position` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::IncrementPosition` (exact) | status: `mapped-exact`
- [x] `HFEv3_Generator.raw_hfe_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::RawHfeBytes` (exact) | status: `mapped-exact`
- [x] `HFEv3_Generator.empty` | kind: `method` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::CreateEmpty` (exact) | status: `mapped-exact`
- [x] `hfev3_get_image` | kind: `function` | mapped VB: `src/Greaseweazle/image/HFEImage.vb::GetImageV3` (exact); `src/Greaseweazle/image/HFEImage.vb::Hfev3GetImage` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/image.py`

- [x] `ImageOpts` | kind: `class` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::ImageOpts` (exact) | status: `mapped-exact`
- [x] `ImageOpts._set` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::Set` (exact) | status: `mapped-exact`
- [x] `ImageOpts.r_set` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::RSet` (exact) | status: `mapped-exact`
- [x] `ImageOpts.w_set` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::WSet` (exact) | status: `mapped-exact`
- [x] `Image` | kind: `class` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::Image` (exact) | status: `mapped-exact`
- [x] `Image.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::New` (exact) | status: `mapped-exact`
- [x] `Image.apply_r_opts` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::ApplyROpts` (exact) | status: `mapped-exact`
- [x] `Image.apply_w_opts` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::ApplyWOpts` (exact) | status: `mapped-exact`
- [x] `Image.__enter__` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::Enter` (exact) | status: `mapped-exact`
- [x] `Image.__exit__` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::Exit` (exact) | status: `mapped-exact`
- [x] `Image.to_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::ToFile` (exact) | status: `mapped-exact`
- [x] `Image.from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::FromFile` (exact); `src/Greaseweazle/image/ImageBase.vb::AssignFormat` (exact) | status: `mapped-exact`
- [x] `Image.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `Image.max_cylinder` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::MaxCylinder` (exact) | status: `mapped-exact`
- [x] `Image.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `Image.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::EmitTrack` (exact); `src/Greaseweazle/image/TD0Image.vb::EmitTrack` (exact) | status: `mapped-exact`
- [x] `Image.get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/ImageBase.vb::GetImage` (exact); `src/Greaseweazle/image/TD0Image.vb::GetImage` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/imd.py`

- [x] `IMDMode` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMDImage.vb::ImdMode` (exact) | status: `mapped-exact`
- [x] `IMD` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMDImage.vb::Imd` (exact) | status: `mapped-exact`
- [x] `IMD.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMDImage.vb::New` (exact) | status: `mapped-exact`
- [x] `IMD.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMDImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `IMD.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMDImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `IMD.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMDImage.vb::EmitTrack` (exact) | status: `mapped-exact`
- [x] `IMD.get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMDImage.vb::GetImage` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/img.py`

- [x] `IMG` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Img` (exact) | status: `mapped-exact`
- [x] `IMG.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::New` (exact); `src/Greaseweazle/image/IMGImage.vb::New` (exact); `src/Greaseweazle/image/NSIImage.vb::New` (exact) | status: `mapped-exact`
- [x] `IMG.track_list` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::TrackList` (exact) | status: `mapped-exact`
- [x] `IMG.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::FromBytes` (exact); `src/Greaseweazle/image/NSIImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `IMG.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::GetTrack` (exact); `src/Greaseweazle/image/NSIImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `IMG.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::EmitTrack` (exact); `src/Greaseweazle/image/NSIImage.vb::EmitTrack` (exact) | status: `mapped-exact`
- [x] `IMG.get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::GetImage` (exact); `src/Greaseweazle/image/NSIImage.vb::GetImage` (exact) | status: `mapped-exact`
- [x] `IMG_AutoFormat` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::ImgAutoFormat` (exact) | status: `mapped-exact`
- [x] `IMG_AutoFormat.format_from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::FormatFromFile` (exact) | status: `mapped-exact`
- [x] `IMG_AutoFormat.from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::FromFile` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/kryoflux.py`

- [x] `Op` | kind: `class` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Op` (exact) | status: `mapped-exact`
- [x] `OOB` | kind: `class` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Oob` (exact) | status: `mapped-exact`
- [x] `KFOpts` | kind: `class` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::KfOpts` (exact) | status: `mapped-exact`
- [x] `KFOpts.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::New` (exact) | status: `mapped-exact`
- [x] `KFOpts.sck` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Sck` (exact) | status: `mapped-exact`
- [x] `KFOpts.sck` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Sck` (exact) | status: `mapped-exact`
- [x] `KFOpts.revs` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Revs` (exact) | status: `mapped-exact`
- [x] `KFOpts.revs` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Revs` (exact) | status: `mapped-exact`
- [x] `KryoFlux` | kind: `class` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::KryoFlux` (exact) | status: `mapped-exact`
- [x] `KryoFlux.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::New` (exact) | status: `mapped-exact`
- [x] `KryoFlux.from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::FromFile` (exact) | status: `mapped-exact`
- [x] `KryoFlux.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `KryoFlux.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::EmitTrack` (exact) | status: `mapped-exact`
- [x] `KryoFlux.__enter__` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Enter` (exact) | status: `mapped-exact`
- [x] `KryoFlux.__exit__` | kind: `method` | mapped VB: `src/Greaseweazle/image/KryoFluxImage.vb::Exit` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/mgt.py`

- [x] `MGT` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Mgt` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/msa.py`

- [x] `MSA` | kind: `class` | mapped VB: `src/Greaseweazle/image/MSAImage.vb::Msa` (exact) | status: `mapped-exact`
- [x] `MSA.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/MSAImage.vb::New` (exact) | status: `mapped-exact`
- [x] `MSA.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/MSAImage.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `MSA.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/MSAImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `MSA.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/MSAImage.vb::EmitTrack` (exact) | status: `mapped-exact`
- [x] `MSA.get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/MSAImage.vb::GetImage` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/nfd.py`

- [x] `NFD` | kind: `class` | mapped VB: `src/Greaseweazle/image/NFDImage.vb::Nfd` (exact) | status: `mapped-exact`
- [x] `NFD.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/NFDImage.vb::New` (exact) | status: `mapped-exact`
- [x] `NFD.from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/NFDImage.vb::FromFile` (exact) | status: `mapped-exact`
- [x] `NFD.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/NFDImage.vb::GetTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/nsi.py`

- [x] `NSI` | kind: `class` | mapped VB: `src/Greaseweazle/image/NSIImage.vb::Nsi` (exact) | status: `mapped-exact`
- [x] `NSI.format_from_file` | kind: `method` | mapped VB: `src/Greaseweazle/image/NSIImage.vb::FormatFromFile` (exact) | status: `mapped-exact`
- [x] `NSI.track_list` | kind: `method` | mapped VB: `src/Greaseweazle/image/NSIImage.vb::TrackList` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/scp.py`

- [x] `SCPHeaderFlags` | kind: `class` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::ScpHeaderFlags` (exact) | status: `mapped-exact`
- [x] `SCPOpts` | kind: `class` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::ScpOpts` (exact) | status: `mapped-exact`
- [x] `SCPOpts.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::New` (exact) | status: `mapped-exact`
- [x] `SCPOpts.disktype` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::Disktype` (exact) | status: `mapped-exact`
- [x] `SCPOpts.disktype` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::Disktype` (exact) | status: `mapped-exact`
- [x] `SCPOpts.revs` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::Revs` (exact) | status: `mapped-exact`
- [x] `SCPOpts.revs` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::Revs` (exact) | status: `mapped-exact`
- [x] `SCPTrack` | kind: `class` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::ScpTrack` (exact) | status: `mapped-exact`
- [x] `SCPTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::New` (exact) | status: `mapped-exact`
- [x] `SCP` | kind: `class` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::Scp` (exact) | status: `mapped-exact`
- [x] `SCP.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::New` (exact) | status: `mapped-exact`
- [x] `SCP.side_count` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::SideCount` (exact) | status: `mapped-exact`
- [x] `SCP.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::FromBytes` (exact); `src/Greaseweazle/image/SCPImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `SCP.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::GetTrack` (exact) | status: `mapped-exact`
- [x] `SCP.emit_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::EmitTrack` (exact) | status: `mapped-exact`
- [x] `SCP.get_image` | kind: `method` | mapped VB: `src/Greaseweazle/image/SCPImage.vb::GetImage` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/sf7.py`

- [x] `SF7` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Sf7` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/sharp2d.py`

- [x] `SHARP2D` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Sharp2d` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/td0.py`

- [x] `TD0` | kind: `class` | mapped VB: `src/Greaseweazle/image/TD0Image.vb::Td0` (exact) | status: `mapped-exact`
- [x] `TD0.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/image/TD0Image.vb::New` (exact) | status: `mapped-exact`
- [x] `TD0.from_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/image/TD0Image.vb::FromBytes` (exact) | status: `mapped-exact`
- [x] `TD0.get_track` | kind: `method` | mapped VB: `src/Greaseweazle/image/TD0Image.vb::GetTrack` (exact) | status: `mapped-exact`

## `src/greaseweazle/image/xdf.py`

- [x] `XDF` | kind: `class` | mapped VB: `src/Greaseweazle/image/IMGImage.vb::Xdf` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/align.py`

- [x] `read_and_normalise` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Align.vb::ReadAndNormalise` (exact) | status: `mapped-exact`
- [x] `align_track` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Align.vb::AlignTrack` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::AlignAction` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/bandwidth.py`

- [x] `generate_random_buffer` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Bandwidth.vb::GenerateRandomBuffer` (exact) | status: `mapped-exact`
- [ ] `measure_bandwidth` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::BandwidthAction` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/clean.py`

- [x] `seek` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Clean.vb::Seek` (exact) | status: `mapped-exact`
- [x] `clean` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Clean.vb::Clean` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::CleanAction` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/convert.py`

- [x] `open_input_image` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Convert.vb::OpenInputImage` (exact) | status: `mapped-exact`
- [x] `open_output_image` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Convert.vb::OpenOutputImage` (exact) | status: `mapped-exact`
- [x] `TrackIdentity` | kind: `class` | mapped VB: `src/Greaseweazle/tools/Convert.vb::TrackIdentity` (exact) | status: `mapped-exact`
- [x] `TrackIdentity.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Convert.vb::New` (exact) | status: `mapped-exact`
- [x] `process_input_track` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Convert.vb::ProcessInputTrack` (exact) | status: `mapped-exact`
- [x] `convert` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Convert.vb::Convert` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::ConvertAction` (exact); `src/Greaseweazle/tools/BasicActions.vb::RunFromOptions` (exact); `src/Greaseweazle/tools/Convert.vb::ResolveTrackSets` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/delays.py`

- [x] `Delays` | kind: `class` | mapped VB: `src/Greaseweazle/tools/Delays.vb::Delays` (exact) | status: `mapped-exact`
- [x] `Delays.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Delays.vb::New` (exact) | status: `mapped-exact`
- [x] `Delays.update` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Delays.vb::Update` (exact) | status: `mapped-exact`
- [x] `print_info_line` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Delays.vb::PrintInfoLine` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::DelaysAction` (exact); `src/Greaseweazle/tools/BasicActions.vb::RunFromOptions` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/erase.py`

- [x] `erase` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Erase.vb::Erase` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::EraseAction` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/info.py`

- [x] `print_info_line` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Info.vb::PrintInfoLine` (exact) | status: `mapped-exact`
- [x] `latest_firmware` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Info.vb::LatestFirmware` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::InfoAction` (exact); `src/Greaseweazle/tools/BasicActions.vb::RunFromOptions` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/list_ports_windows.py`

- [x] `GUID` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeGuids` (exact) | status: `mapped-exact`
- [x] `GUID.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeGuids` (exact) | status: `mapped-exact`
- [x] `GUID.__eq__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeGuids` (exact) | status: `mapped-exact`
- [x] `DEVPROPKEY` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeDevpropkey` (exact) | status: `mapped-exact`
- [x] `USB_DEVICE_DESCRIPTOR` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeUsbDeviceDescriptor` (exact) | status: `mapped-exact`
- [x] `USB_STRING_DESCRIPTOR` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeBuffers` (exact) | status: `mapped-exact`
- [x] `USB_COMMON_DESCRIPTOR` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeBuffers` (exact) | status: `mapped-exact`
- [x] `USB_CONFIGURATION_DESCRIPTOR` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeUsbConfigurationDescriptor` (exact) | status: `mapped-exact`
- [x] `USB_INTERFACE_DESCRIPTOR` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeUsbInterfaceDescriptor` (exact) | status: `mapped-exact`
- [x] `USB_INTERFACE_ASSOCIATION_DESCRIPTOR` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeUsbInterfaceAssociationDescriptor` (exact) | status: `mapped-exact`
- [x] `USB_NODE_CONNECTION_INFORMATION_EX` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeUsbNodeConnectionInformationEx` (exact) | status: `mapped-exact`
- [x] `SetupPacket` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeSetupPacket` (exact) | status: `mapped-exact`
- [x] `USB_DESCRIPTOR_REQUEST` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeUsbDescriptorRequest` (exact) | status: `mapped-exact`
- [x] `CM_POWER_DATA` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::CmPowerData` (exact); `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeCmPowerData` (exact) | status: `mapped-exact`
- [x] `find_from_iterable` | kind: `function` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeBuffers` (exact) | status: `mapped-exact`
- [x] `parse_device_property` | kind: `function` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscoveryNative.vb::NativeBuffers` (exact) | status: `mapped-exact`
- [x] `cached_property` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::DeviceNode` (exact) | status: `mapped-exact`
- [x] `cached_property.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::DeviceNode` (exact) | status: `mapped-exact`
- [x] `cached_property.__get__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::DeviceNode` (exact) | status: `mapped-exact`
- [x] `DeviceNode` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::DeviceNode` (exact) | status: `mapped-exact`
- [x] `DeviceNode.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::New` (exact) | status: `mapped-exact`
- [x] `DeviceNode.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::ToString` (exact) | status: `mapped-exact`
- [x] `DeviceNode.__eq__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Equals` (exact) | status: `mapped-exact`
- [x] `DeviceNode.__lt__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::CompareTo` (exact) | status: `mapped-exact`
- [x] `DeviceNode.__hash__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::GetHashCode` (exact) | status: `mapped-exact`
- [x] `DeviceNode.instance_handle` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::InstanceHandle` (exact) | status: `mapped-exact`
- [x] `DeviceNode.instance_identifier` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::InstanceIdentifier` (exact) | status: `mapped-exact`
- [x] `DeviceNode.status` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Status` (exact) | status: `mapped-exact`
- [x] `DeviceNode.parent` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Parent` (exact) | status: `mapped-exact`
- [x] `DeviceNode.name` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Name` (exact) | status: `mapped-exact`
- [x] `DeviceNode.description` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Description` (exact) | status: `mapped-exact`
- [x] `DeviceNode.manufacturer` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Manufacturer` (exact) | status: `mapped-exact`
- [x] `DeviceNode.address` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Address` (exact) | status: `mapped-exact`
- [x] `DeviceNode.bus_reported_device_description` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::BusReportedDeviceDescription` (exact) | status: `mapped-exact`
- [x] `DeviceNode.friendly_name` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::FriendlyName` (exact) | status: `mapped-exact`
- [x] `DeviceNode.location_paths` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::LocationPaths` (exact) | status: `mapped-exact`
- [x] `DeviceNode.power_data` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::PowerData` (exact) | status: `mapped-exact`
- [x] `DeviceNode.port_name` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::PortName` (exact) | status: `mapped-exact`
- [x] `DeviceNode.get_property` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::GetProperty` (exact) | status: `mapped-exact`
- [x] `DeviceInterface` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::DeviceInterface` (exact) | status: `mapped-exact`
- [x] `DeviceInterface.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::New` (exact) | status: `mapped-exact`
- [x] `DeviceInterface.enumerate_device` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::EnumerateInterfaces` (exact) | status: `mapped-exact`
- [x] `DeviceInterface.interface` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Interface` (exact) | status: `mapped-exact`
- [x] `DeviceInterface.instance_identifier` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::InstanceIdentifier` (exact) | status: `mapped-exact`
- [x] `DeviceInterface.instance_handle` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::InstanceHandle` (normalized) | status: `mapped-normalized`
- [x] `DeviceInterface.get_interface_property` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::GetInterfaceProperty` (exact) | status: `mapped-exact`
- [x] `PortDevice` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::PortDevice` (exact) | status: `mapped-exact`
- [x] `PortDevice.wake_up_device` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::WakeUpDevice` (exact) | status: `mapped-exact`
- [x] `LegacyPortDevice` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::LegacyPortDevice` (exact) | status: `mapped-exact`
- [x] `LegacyPortDevice.wake_up_device` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::WakeUpDevice` (exact) | status: `mapped-exact`
- [x] `LegacyPortDevice.enumerate_device` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::EnumerateDevice` (exact) | status: `mapped-exact`
- [x] `USBHostControllerDevice` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::UsbHostControllerDevice` (exact) | status: `mapped-exact`
- [x] `USBHubDevice` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::UsbHubDevice` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::DeviceRegistry` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::New` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.get_cache_key` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::GetCacheKey` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.get_location_string` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::GetLocationString` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.get_bus_number` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::GetBusNumber` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.find_parent_hub_and_usb` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::FindParentHubAndUsb` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.find_parent_host_controller` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::FindParentHostController` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.find_parent_chain` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::FindParentChain` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.request_usb_info` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::RequestUsbInfo` (exact) | status: `mapped-exact`
- [x] `DeviceRegistry.get_usb_info` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::GetUsbInfo` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::UsbHubDeviceIOControl` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::New` (normalized) | status: `mapped-normalized`
- [x] `USBHubDeviceIOControl.__enter__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Dispose` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.__exit__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Dispose` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.is_open` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::IsOpen` (normalized) | status: `mapped-normalized`
- [x] `USBHubDeviceIOControl.open` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Open` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.close` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Close` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.request_supported_languages` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::RequestSupportedLanguages` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.suggest_language_id` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::SuggestLanguageId` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.request_usb_string_description` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::RequestUsbStringDescription` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.request_usb_device_description` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::RequestUsbDeviceDescription` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.request_usb_configuration_description` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::RequestUsbConfigurationDescription` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.request_usb_interface_descriptions` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::RequestUsbInterfaceDescriptions` (exact) | status: `mapped-exact`
- [x] `USBHubDeviceIOControl.request_usb_connection_info` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::RequestUsbConnectionInfo` (exact) | status: `mapped-exact`
- [x] `USBInfo` | kind: `class` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::UsbInfo` (exact) | status: `mapped-exact`
- [x] `USBInfo.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::New` (exact) | status: `mapped-exact`
- [x] `iterate_comports` | kind: `function` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::IterateComports` (exact) | status: `mapped-exact`
- [x] `comports` | kind: `function` | mapped VB: `src/Greaseweazle/tools/WindowsPortDiscovery.vb::Comports` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/pin.py`

- [ ] `pin_set` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `_pin_get` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Pin.vb::PinGetInner` (exact) | status: `mapped-exact`
- [ ] `pin_get` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [ ] `usage` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::PinAction` (exact); `src/Greaseweazle/tools/BasicActions.vb::RunFromOptions` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/read.py`

- [x] `open_image` | kind: `function` | mapped VB: `src/Greaseweazle/tools/ReadWrite.vb::OpenImage` (exact) | status: `mapped-exact`
- [x] `read_and_normalise` | kind: `function` | mapped VB: `src/Greaseweazle/tools/ReadWrite.vb::ReadAndNormalise` (exact) | status: `mapped-exact`
- [x] `read_with_retry` | kind: `function` | mapped VB: `src/Greaseweazle/tools/ReadWrite.vb::ReadWithRetry` (exact) | status: `mapped-exact`
- [x] `print_summary` | kind: `function` | mapped VB: `src/Greaseweazle/tools/ReadWrite.vb::BuildSectorSummary` (exact) | status: `mapped-exact`
- [ ] `read_to_image` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::ReadAction` (exact); `src/Greaseweazle/tools/BasicActions.vb::RunFromOptions` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/reset.py`

- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::ResetAction` (exact); `src/Greaseweazle/tools/BasicActions.vb::RunFromOptions` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/rpm.py`

- [x] `speed_str` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Rpm.vb::SpeedString` (exact) | status: `mapped-exact`
- [x] `print_rpm` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Rpm.vb::PrintRpm` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::RpmAction` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/seek.py`

- [x] `seek` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Seek.vb::Seek` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::SeekAction` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/update.py`

- [x] `SkipUpdate` | kind: `class` | mapped VB: `src/Greaseweazle/tools/Update.vb::SkipUpdate` (exact) | status: `mapped-exact`
- [x] `update_firmware` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Update.vb::UpdateFirmware` (exact) | status: `mapped-exact`
- [x] `extract_update` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Update.vb::ExtractUpdate` (exact) | status: `mapped-exact`
- [x] `gh_request_get` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Update.vb::GhRequestGet` (exact) | status: `mapped-exact`
- [x] `download` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Update.vb::Download` (exact) | status: `mapped-exact`
- [x] `download_by_tag` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Update.vb::DownloadByTag` (exact) | status: `mapped-exact`
- [x] `download_latest` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Update.vb::DownloadLatest` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::UpdateAction` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/util.py`

- [ ] `columnify` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [ ] `CmdlineHelpFormatter` | kind: `class` | mapped VB: _(none)_ | status: `missing`
- [ ] `CmdlineHelpFormatter._get_help_string` | kind: `method` | mapped VB: _(none)_ | status: `missing`
- [ ] `ArgumentParser` | kind: `class` | mapped VB: _(none)_ | status: `missing`
- [ ] `ArgumentParser.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/OptionParser.vb::New` (ambiguous); `src/Greaseweazle/tools/Tooling.vb::New` (ambiguous); `src/Greaseweazle/tools/Tooling.vb::New` (ambiguous); `src/Greaseweazle/tools/Tooling.vb::New` (ambiguous); `src/Greaseweazle/tools/Tooling.vb::New` (ambiguous); `src/Greaseweazle/tools/TrackSet.vb::New` (ambiguous); `src/Greaseweazle/tools/TrackSet.vb::New` (ambiguous); `src/Greaseweazle/tools/TrackSet.vb::New` (ambiguous); `src/Greaseweazle/tools/TrackSetSpec.vb::New` (ambiguous); `src/Greaseweazle/tools/TrackSetSpec.vb::New` (ambiguous); `src/Greaseweazle/usb/UsbUnitClient.vb::New` (ambiguous); `src/Greaseweazle/usb/UsbUnitClient.vb::New` (ambiguous) | status: `name-only-ambiguous`
- [ ] `min_int` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [ ] `level` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [ ] `period` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [ ] `Drive` | kind: `class` | mapped VB: _(none)_ | status: `missing`
- [ ] `Drive.__call__` | kind: `method` | mapped VB: _(none)_ | status: `missing`
- [x] `range_str` | kind: `function` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::RangeToString` (exact) | status: `mapped-exact`
- [x] `TrackSet` | kind: `class` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::TrackSet` (exact); `src/Greaseweazle/tools/TrackSetSpec.vb::TrackSetSpec` (exact) | status: `mapped-exact`
- [x] `TrackSet.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::New` (exact) | status: `mapped-exact`
- [x] `TrackSet.ch_to_pch` | kind: `method` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::ChToPch` (exact) | status: `mapped-exact`
- [x] `TrackSet.update_from_trackspec` | kind: `method` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::UpdateFromTrackspec` (exact); `src/Greaseweazle/tools/TrackSet.vb::ParseCylinderSet` (exact); `src/Greaseweazle/tools/TrackSet.vb::ParseHeadSet` (exact); `src/Greaseweazle/tools/TrackSetSpec.vb::UpdateFromTrackspec` (exact) | status: `mapped-exact`
- [x] `TrackSet.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::ToString` (exact) | status: `mapped-exact`
- [x] `TrackSet.__iter__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::GetEnumerator` (exact); `src/Greaseweazle/tools/TrackSet.vb::GetEnumeratorNonGeneric` (exact) | status: `mapped-exact`
- [x] `TrackSet.__contains__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/TrackSet.vb::Contains` (exact); `src/Greaseweazle/tools/TrackSet.vb::Contains` (exact) | status: `mapped-exact`
- [x] `split_opts` | kind: `function` | mapped VB: `src/Greaseweazle/tools/OptionParser.vb::SplitOpts` (exact) | status: `mapped-exact`
- [ ] `get_image_class` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `with_drive_selected` | kind: `function` | mapped VB: `src/Greaseweazle/tools/InterruptControl.vb::Register` (exact); `src/Greaseweazle/tools/InterruptControl.vb::Unregister` (exact); `src/Greaseweazle/tools/Tooling.vb::WithDriveSelected` (exact) | status: `mapped-exact`
- [ ] `valid_ser_id` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `score_port` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::ScorePort` (exact) | status: `mapped-exact`
- [x] `find_port` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::FindPort` (exact) | status: `mapped-exact`
- [x] `port_info` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::PortInfo` (exact) | status: `mapped-exact`
- [x] `usb_reopen` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::UsbReopen` (exact) | status: `mapped-exact`
- [ ] `print_update_instructions` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `usb_mode_check` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::UsbModeCheck` (exact) | status: `mapped-exact`
- [x] `usb_open` | kind: `function` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::UsbOpen` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::PortDevice` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::JumperlessUpdate` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::CanModeSwitch` (exact) | status: `mapped-exact`

## `src/greaseweazle/tools/write.py`

- [x] `open_image` | kind: `function` | mapped VB: `src/Greaseweazle/tools/ReadWrite.vb::OpenImage` (exact) | status: `mapped-exact`
- [ ] `write_from_image` | kind: `function` | mapped VB: _(none)_ | status: `missing`
- [x] `PrecompSpec` | kind: `class` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::PrecompSpec` (exact) | status: `mapped-exact`
- [x] `PrecompSpec.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::ToString` (exact) | status: `mapped-exact`
- [x] `PrecompSpec.track_precomp` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::TrackPrecomp` (exact) | status: `mapped-exact`
- [x] `PrecompSpec.importspec` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::ImportSpec` (exact) | status: `mapped-exact`
- [x] `PrecompSpec.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::New` (exact) | status: `mapped-exact`
- [x] `main` | kind: `function` | mapped VB: `src/Greaseweazle/tools/BasicActions.vb::WriteAction` (exact); `src/Greaseweazle/tools/BasicActions.vb::RunFromOptions` (exact) | status: `mapped-exact`

## `src/greaseweazle/track.py`

- [x] `PLL` | kind: `class` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::Pll` (exact) | status: `mapped-exact`
- [x] `PLL.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::New` (exact) | status: `mapped-exact`
- [x] `PLL.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::ToString` (exact) | status: `mapped-exact`
- [x] `Precomp` | kind: `class` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::Precomp` (exact) | status: `mapped-exact`
- [x] `Precomp.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::ToString` (exact) | status: `mapped-exact`
- [x] `Precomp.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::New` (exact) | status: `mapped-exact`
- [x] `Precomp.apply` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::Apply` (exact) | status: `mapped-exact`
- [x] `HasVerify` | kind: `class` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::HasVerify` (exact) | status: `mapped-exact`
- [x] `HasVerify.verify_track` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::VerifyTrack` (exact) | status: `mapped-exact`
- [x] `MasterTrack` | kind: `class` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::MasterTrack` (exact) | status: `mapped-exact`
- [x] `MasterTrack.bitrate` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::Bitrate` (exact) | status: `mapped-exact`
- [x] `MasterTrack.scale` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::Scale` (exact) | status: `mapped-exact`
- [x] `MasterTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::New` (exact); `src/Greaseweazle/track/TrackModel.vb::New` (exact) | status: `mapped-exact`
- [x] `MasterTrack.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::ToString` (exact) | status: `mapped-exact`
- [x] `MasterTrack.summary_string` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::SummaryString` (exact) | status: `mapped-exact`
- [x] `MasterTrack.reverse` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::Reverse` (exact) | status: `mapped-exact`
- [x] `MasterTrack.flux` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::Flux` (exact); `src/Greaseweazle/track/TrackModel.vb::Flux` (exact) | status: `mapped-exact`
- [x] `MasterTrack.flux_for_writeout` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::FluxForWriteout` (exact) | status: `mapped-exact`
- [x] `MasterTrack._flux` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::BuildFlux` (exact) | status: `mapped-exact`
- [x] `PLLRevolution` | kind: `class` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::PllRevolution` (exact) | status: `mapped-exact`
- [x] `PLLRevolution.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::New` (exact) | status: `mapped-exact`
- [x] `PLLTrack` | kind: `class` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::PllTrack` (exact) | status: `mapped-exact`
- [x] `PLLTrack.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::New` (exact) | status: `mapped-exact`
- [x] `PLLTrack.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::ToString` (exact) | status: `mapped-exact`
- [x] `PLLTrack.get_revolution` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::GetRevolution` (exact) | status: `mapped-exact`
- [x] `PLLTrack.get_all_data` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::GetAllData` (exact) | status: `mapped-exact`
- [x] `PLLTrack.import_flux_data` | kind: `method` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::ImportFluxData` (exact) | status: `mapped-exact`
- [x] `flux_to_bitcells` | kind: `function` | mapped VB: `src/Greaseweazle/track/TrackModel.vb::FluxToBitcells` (exact); `src/Greaseweazle/track/TrackModel.vb::FluxToBitcells` (exact) | status: `mapped-exact`

## `src/greaseweazle/usb.py`

- [x] `ControlCmd` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::ControlCmd` (exact) | status: `mapped-exact`
- [x] `Cmd` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::Cmd` (exact) | status: `mapped-exact`
- [x] `Ack` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::Ack` (exact) | status: `mapped-exact`
- [x] `GetInfo` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::GetInfo` (exact) | status: `mapped-exact`
- [x] `Params` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::Params` (exact) | status: `mapped-exact`
- [x] `BusType` | kind: `enum` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::BusType` (exact) | status: `mapped-exact`
- [x] `FluxOp` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::FluxOp` (exact) | status: `mapped-exact`
- [x] `DriveInfo` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::DriveInfo` (exact) | status: `mapped-exact`
- [x] `DriveInfo.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::New` (exact) | status: `mapped-exact`
- [x] `DriveInfo.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::ToString` (exact) | status: `mapped-exact`
- [x] `CmdError` | kind: `class` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::CmdError` (exact) | status: `mapped-exact`
- [x] `CmdError.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::New` (exact) | status: `mapped-exact`
- [x] `CmdError.cmd_str` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::CmdStr` (exact) | status: `mapped-exact`
- [x] `CmdError.errcode_str` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::ErrcodeStr` (exact) | status: `mapped-exact`
- [x] `CmdError.__str__` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbProtocol.vb::ToString` (exact) | status: `mapped-exact`
- [x] `Unit` | kind: `class` | mapped VB: `src/Greaseweazle/usb/SerialPortTransport.vb::BytesAvailable` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Unit` (exact) | status: `mapped-exact`
- [x] `Unit.__init__` | kind: `method` | mapped VB: `src/Greaseweazle/usb/SerialPortTransport.vb::New` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::New` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::ReadFirmwareInfo` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::ApplyFirmwareInfo` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::FirmwareInfo` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Major` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Minor` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::IsMainFirmware` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::HwModel` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::HwSubmodel` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::UsbSpeed` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::McuId` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::McuMhz` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::McuSramKb` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::UsbBufferKb` (exact) | status: `mapped-exact`
- [x] `Unit.reset` | kind: `method` | mapped VB: `src/Greaseweazle/tools/Tooling.vb::Reset` (exact); `src/Greaseweazle/usb/SerialPortTransport.vb::BaudRate` (exact); `src/Greaseweazle/usb/SerialPortTransport.vb::ResetInputBuffer` (exact); `src/Greaseweazle/usb/SerialPortTransport.vb::ResetOutputBuffer` (exact); `src/Greaseweazle/usb/SerialPortTransport.vb::Open` (exact); `src/Greaseweazle/usb/SerialPortTransport.vb::Close` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::ResetInputBuffer` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::ResetOutputBuffer` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Open` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Close` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Reset` (exact) | status: `mapped-exact`
- [x] `Unit._send_cmd` | kind: `method` | mapped VB: `src/Greaseweazle/usb/SerialPortTransport.vb::Write` (exact); `src/Greaseweazle/usb/SerialPortTransport.vb::Read` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Write` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::Read` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::SendCmd` (exact) | status: `mapped-exact`
- [x] `Unit.get_current_drive_info` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::GetCurrentDriveInfo` (exact) | status: `mapped-exact`
- [x] `Unit.get_params` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::GetParams` (exact) | status: `mapped-exact`
- [x] `Unit.set_params` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::SetParams` (exact) | status: `mapped-exact`
- [x] `Unit.seek` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::Seek` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::BuildSeekCommand` (exact) | status: `mapped-exact`
- [x] `Unit.set_bus_type` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::SetBusType` (exact) | status: `mapped-exact`
- [x] `Unit.set_pin` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::SetPin` (exact) | status: `mapped-exact`
- [x] `Unit.get_pin` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::GetPin` (exact) | status: `mapped-exact`
- [x] `Unit.power_on_reset` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::PowerOnReset` (exact) | status: `mapped-exact`
- [x] `Unit.drive_select` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::DriveSelect` (exact) | status: `mapped-exact`
- [x] `Unit.drive_deselect` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::DriveDeselect` (exact) | status: `mapped-exact`
- [x] `Unit.drive_motor` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::DriveMotor` (exact) | status: `mapped-exact`
- [x] `Unit.switch_fw_mode` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::SwitchFwMode` (exact) | status: `mapped-exact`
- [x] `Unit.update_main_firmware` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::UpdateMainFirmware` (exact) | status: `mapped-exact`
- [x] `Unit.update_bootloader` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::UpdateBootloader` (exact) | status: `mapped-exact`
- [x] `Unit._decode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::DecodeFluxData` (exact) | status: `mapped-exact`
- [x] `Unit._encode_flux` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::EncodeFluxData` (exact) | status: `mapped-exact`
- [x] `Unit._read_track` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::ReadTrackRaw` (exact); `src/Greaseweazle/usb/UsbUnitClient.vb::BuildReadFluxCommand` (exact) | status: `mapped-exact`
- [x] `Unit.read_track` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::ReadTrack` (exact) | status: `mapped-exact`
- [x] `Unit.write_track` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::WriteTrack` (exact) | status: `mapped-exact`
- [x] `Unit.erase_track` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::EraseTrack` (exact) | status: `mapped-exact`
- [x] `Unit.source_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::SourceBytes` (exact) | status: `mapped-exact`
- [x] `Unit.sink_bytes` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::SinkBytes` (exact) | status: `mapped-exact`
- [x] `Unit.bw_stats` | kind: `method` | mapped VB: `src/Greaseweazle/usb/UsbUnitClient.vb::BwStats` (exact) | status: `mapped-exact`

