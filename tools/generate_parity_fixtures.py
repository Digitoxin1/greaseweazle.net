import json
import os
import sys
import hashlib
import ast
import importlib
import time
import tempfile
import struct
import crcmod.predefined

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC_ROOT = os.path.join(REPO_ROOT, "python_source", "src")
sys.path.insert(0, SRC_ROOT)

from greaseweazle import error
from greaseweazle.flux import Flux
from greaseweazle.track import MasterTrack, PLLTrack, PLL
from greaseweazle.usb import Unit
from greaseweazle.tools import util
from greaseweazle.codec import codec
from greaseweazle.image.image import ImageOpts
from greaseweazle.image.img import IMG
from greaseweazle.tools.write import PrecompSpec
from pathlib import Path
import re

_gw_root = importlib.import_module("greaseweazle")
if not hasattr(_gw_root, "__version__"):
    _gw_root.__version__ = "0.0"

from greaseweazle.image.scp import SCP
from greaseweazle.image.kryoflux import KryoFlux
from greaseweazle.image.d88 import D88
from greaseweazle.image.dmk import DMK
from greaseweazle.image.edsk import EDSK
from greaseweazle.image.td0 import TD0, crc16 as td0_crc
from greaseweazle.image.nfd import NFD
from greaseweazle.image.dcp import DCP
from greaseweazle.image.a2r import A2R
from greaseweazle.image.msa import MSA
from greaseweazle.image.apridisk import Apridisk
from greaseweazle.image.imd import IMD
from greaseweazle.image.hfe import HFE
from greaseweazle.image.dim import DIM


class _FakeUnit(Unit):
    def __init__(self):
        self.sample_freq = 24000000


def _bools(bits):
    return [1 if b else 0 for b in bits]


def build_error_fixtures():
    cases = []
    try:
        error.check(False, "expected fatal")
    except Exception as ex:
        cases.append({"name": "check_false", "error_type": type(ex).__name__, "message": str(ex)})
    error.check(True, "ok")
    cases.append({"name": "check_true", "result": "pass"})
    return {"cases": cases}


def build_flux_fixtures():
    fixtures = {}

    f = Flux([30.0, 100.0, 100.0], [10.0, 10.0, 10.0, 20.0, 30.0, 40.0, 60.0], 1000.0, index_cued=False)
    f.cue_at_index()
    fixtures["cue_at_index"] = {"index_list": f.index_list, "flux_list": f.list}

    f = Flux([100.0, 100.0], [20.0, 30.0, 50.0, 20.0, 30.0, 50.0], 1000.0, index_cued=True)
    fixtures["summary"] = {"summary": f.summary_string(), "ticks_per_rev": f.ticks_per_rev}

    f = Flux([100.0, 100.0], [20.0, 30.0, 50.0, 20.0, 30.0, 50.0], 1000.0, index_cued=True)
    f.reverse()
    fixtures["reverse"] = {"index_list": f.index_list, "flux_list": f.list, "index_cued": f.index_cued}

    f = Flux([100.0, 100.0], [20.0, 30.0, 50.0, 20.0, 30.0, 50.0], 1000.0, index_cued=True)
    f.set_nr_revs(1)
    fixtures["set_nr_revs"] = {"index_list": f.index_list, "flux_list": f.list}

    f = Flux([100.0, 100.0], [20.0, 30.0, 50.0, 20.0, 30.0, 50.0], 1000.0, index_cued=True)
    wf = f.flux_for_writeout(cue_at_index=True)
    fixtures["writeout"] = {
        "ticks_to_index": wf.ticks_to_index,
        "list": wf.list,
        "index_cued": wf.index_cued,
        "terminate_at_index": wf.terminate_at_index,
        "summary": wf.summary_string(),
    }

    return fixtures


def build_track_fixtures():
    fixtures = {}
    bits = [True, False, True, False, True, False, False, True]
    mt = MasterTrack(bits, time_per_rev=0.2, splice=2)
    flux = mt.flux(revs=2)
    fixtures["master_flux"] = {
        "bitrate": mt.bitrate,
        "summary": mt.summary_string(),
        "flux_index_list": flux.index_list,
        "flux_list": flux.list,
        "flux_splice": flux.splice,
        "flux_sample_freq": flux.sample_freq,
    }

    mt2 = MasterTrack(bits, time_per_rev=0.2, splice=2)
    wf = mt2.flux_for_writeout(cue_at_index=True)
    fixtures["master_writeout"] = {
        "ticks_to_index": wf.ticks_to_index,
        "list": wf.list,
        "terminate_at_index": wf.terminate_at_index,
    }

    ptrack = PLLTrack(clock=2e-6, data=flux, pll=PLL("period=5:phase=60"))
    fixtures["pll_track"] = {
        "nr_revs": len(ptrack.revolutions),
        "rev_bits": [x.nr_bits for x in ptrack.revolutions],
        "total_bits": len(ptrack.bitarray),
        "total_times": len(ptrack.timearray),
    }
    return fixtures


def build_usb_fixtures():
    fixtures = {}
    fake = _FakeUnit()
    # Keep all pulses below the no-flux-area threshold so roundtrip decode
    # stays in the opcode subset accepted by _decode_flux.
    data = fake._encode_flux([100, 300, 1000, 2000])
    decoded_flux, decoded_index = fake._decode_flux(data)
    fixtures["encode_decode_basic"] = {
        "encoded": list(data),
        "decoded_flux": decoded_flux,
        "decoded_index": decoded_index,
    }

    astable_data = fake._encode_flux([100, 300, 1000, 50000])
    try:
        fake._decode_flux(astable_data)
        astable_error = None
    except Exception as ex:
        astable_error = str(ex)
    fixtures["decode_astable_failure"] = {
        "encoded": list(astable_data),
        "error": astable_error,
    }
    return fixtures


def build_tools_fixtures():
    period_inputs = ["300rpm", "200ms", "500us", "1000ns", "40000scp", "360"]
    split_inputs = ["foo::a=b:c=d", "bar::x:y=2", "image::opt"]
    column_input = [".a2r", ".adf", ".d64", ".d81", ".d88", ".dcp"]
    return {
        "period_cases": [{"input": x, "output": util.period(x)} for x in period_inputs],
        "split_opts_cases": [
            {
                "input": x,
                "name": util.split_opts(x)[0],
                "opts": [{"key": k, "value": v} for k, v in sorted(util.split_opts(x)[1].items())],
            }
            for x in split_inputs
        ],
        "columnify": {"input": column_input, "output": util.columnify(column_input)},
        "image_suffixes": sorted(list(util.image_types.keys())),
        "drive_cases": [
            {"input": "A", "bus": util.Drive()("A").bus.value, "unit": util.Drive()("A").unit_id},
            {"input": "B", "bus": util.Drive()("B").bus.value, "unit": util.Drive()("B").unit_id},
            {"input": "0", "bus": util.Drive()("0").bus.value, "unit": util.Drive()("0").unit_id},
            {"input": "3", "bus": util.Drive()("3").bus.value, "unit": util.Drive()("3").unit_id},
        ],
        "level_cases": [
            {"input": "H", "output": util.level("H")},
            {"input": "L", "output": util.level("L")},
            {"input": "h", "output": util.level("h")},
            {"input": "l", "output": util.level("l")},
        ],
        "score_port_cases": build_score_port_cases(),
        "find_port_cases": build_find_port_cases(),
        "valid_ser_id_cases": [
            {"input": "GW1234", "value": bool(util.valid_ser_id("GW1234"))},
            {"input": "gwabcd", "value": bool(util.valid_ser_id("gwabcd"))},
            {"input": "XX1234", "value": bool(util.valid_ser_id("XX1234"))},
            {"input": "", "value": bool(util.valid_ser_id(""))},
            {"input": None, "value": bool(util.valid_ser_id(None))},
        ],
        "with_drive_selected_cases": build_with_drive_selected_cases(),
        "range_str_cases": [
            {"input": [], "output": util.range_str([])},
            {"input": [0], "output": util.range_str([0])},
            {"input": [0, 1, 2, 4, 6, 7], "output": util.range_str([0, 1, 2, 4, 6, 7])},
            {"input": [5, 7, 8, 9], "output": util.range_str([5, 7, 8, 9])},
        ],
    }


def build_score_port_cases():
    class Port:
        def __init__(self, **kwargs):
            self.manufacturer = kwargs.get("manufacturer")
            self.product = kwargs.get("product")
            self.vid = kwargs.get("vid")
            self.pid = kwargs.get("pid")
            self.serial_number = kwargs.get("serial_number")
            self.location = kwargs.get("location")

    def as_dict(p):
        return {
            "manufacturer": p.manufacturer,
            "product": p.product,
            "vid": p.vid,
            "pid": p.pid,
            "serial_number": p.serial_number,
            "location": p.location,
        }

    p1 = Port(manufacturer="Keir Fraser", product="Greaseweazle", vid=0x1209, pid=0x4D69, serial_number="GW12345", location="A")
    p2 = Port(manufacturer="Other", product="gw-compat board", vid=0x1209, pid=0x0001, serial_number="GW54321", location="B")
    p3 = Port(manufacturer="Other", product="Random", vid=0x0000, pid=0x0000, serial_number=None, location=None)
    old = Port(manufacturer="Keir Fraser", product="Greaseweazle", vid=0x1209, pid=0x4D69, serial_number="GW12345", location="A")
    mismatch_old = Port(manufacturer="Keir Fraser", product="Greaseweazle", vid=0x1209, pid=0x4D69, serial_number="GW99999", location="A")

    cases = [
        {"port": as_dict(p1), "old_port": None, "score": util.score_port(p1, None)},
        {"port": as_dict(p1), "old_port": as_dict(old), "score": util.score_port(p1, old)},
        {"port": as_dict(p1), "old_port": as_dict(mismatch_old), "score": util.score_port(p1, mismatch_old)},
        {"port": as_dict(p2), "old_port": None, "score": util.score_port(p2, None)},
        {"port": as_dict(p3), "old_port": None, "score": util.score_port(p3, None)},
    ]
    return cases


def build_find_port_cases():
    class Port:
        def __init__(self, **kwargs):
            self.device = kwargs.get("device")
            self.manufacturer = kwargs.get("manufacturer")
            self.product = kwargs.get("product")
            self.vid = kwargs.get("vid")
            self.pid = kwargs.get("pid")
            self.serial_number = kwargs.get("serial_number")
            self.location = kwargs.get("location")

    def as_dict(p):
        return {
            "device": p.device,
            "manufacturer": p.manufacturer,
            "product": p.product,
            "vid": p.vid,
            "pid": p.pid,
            "serial_number": p.serial_number,
            "location": p.location,
        }

    ports = [
        Port(device="COM1", manufacturer="Other", product="Random", vid=0, pid=0, serial_number=None, location="X"),
        Port(device="COM2", manufacturer="Keir Fraser", product="Greaseweazle", vid=0x1209, pid=0x4D69, serial_number="GW111", location="A"),
        Port(device="COM3", manufacturer="Other", product="gw-compat board", vid=0x1209, pid=0x0001, serial_number="GW222", location="B"),
    ]
    old = Port(device="COM9", manufacturer="Keir Fraser", product="Greaseweazle", vid=0x1209, pid=0x4D69, serial_number="GW111", location="A")

    def pick(ps, old_port=None):
        best_score, best = 0, None
        for p in ps:
            s = util.score_port(p, old_port)
            if s > best_score:
                best_score, best = s, p
        return None if best is None else best.device

    return [
        {"ports": [as_dict(p) for p in ports], "old_port": None, "selected": pick(ports, None)},
        {"ports": [as_dict(p) for p in ports], "old_port": as_dict(old), "selected": pick(ports, old)},
        {"ports": [as_dict(ports[0])], "old_port": None, "selected": pick([ports[0]], None)},
    ]


def build_with_drive_selected_cases():
    class FakeUSB:
        def __init__(self):
            self.log = []

        def set_bus_type(self, bus):
            self.log.append(f"set_bus_type:{bus}")

        def drive_select(self, unit):
            self.log.append(f"drive_select:{unit}")

        def drive_motor(self, unit, state):
            self.log.append(f"drive_motor:{unit}:{int(state)}")

        def drive_deselect(self):
            self.log.append("drive_deselect")

        def reset(self):
            self.log.append("reset")

    cases = []

    usb = FakeUSB()
    drive = util.Drive()("A")

    def fn_success():
        usb.log.append("fn")

    util.with_drive_selected(fn_success, usb, drive, motor=True)
    cases.append({"name": "success", "raised": False, "log": usb.log})

    usb2 = FakeUSB()
    drive2 = util.Drive()("A")

    def fn_interrupt():
        raise KeyboardInterrupt()

    raised = False
    try:
        util.with_drive_selected(fn_interrupt, usb2, drive2, motor=True)
    except KeyboardInterrupt:
        raised = True
    cases.append({"name": "keyboard_interrupt", "raised": raised, "log": usb2.log})

    return cases


def build_codec_fixtures():
    diskdef = codec.get_diskdef("ibm.1440")
    all_formats = codec.get_all_formats("", codec.DiskDef_File(name=None))
    all_formats.sort()
    printed = codec.print_formats()
    digest = hashlib.sha256(printed.encode("utf-8")).hexdigest()

    track = diskdef.mk_track(0, 0)
    data = bytes(((i * 7) & 0xFF) for i in range(10000))
    consumed = track.set_img_track(data)
    img_track = bytes(track.get_img_track())

    track_short = diskdef.mk_track(0, 0)
    consumed_short = track_short.set_img_track(bytes([1, 2]))
    img_track_short = bytes(track_short.get_img_track())

    source_track = diskdef.mk_track(0, 0)
    source_data = bytes(((i * 11 + 3) & 0xFF) for i in range(source_track.nsec * 512))
    source_track.set_img_track(source_data)
    source_flux = source_track.flux()
    decoded_track = diskdef.mk_track(0, 0)
    decoded_track.decode_flux(source_flux)
    decoded_img = bytes(decoded_track.get_img_track())

    emit_track = diskdef.mk_track(0, 0)
    emit_data = bytes(((i * 13 + 9) & 0xFF) for i in range(emit_track.nsec * 512))
    emit_track.set_img_track(emit_data)
    emit_flux = emit_track.flux()
    emit_decoded = diskdef.mk_track(0, 0)
    emit_decoded.decode_flux(emit_flux)
    emit_decoded_img = bytes(emit_decoded.get_img_track())

    bitcell_diskdef = codec.get_diskdef("raw.250")
    bitcell_track = bitcell_diskdef.mk_track(0, 0)
    bitcell_track.decode_flux(emit_flux)
    bitcell_master = bitcell_track.master_track()
    bitcell_empty = bitcell_diskdef.mk_track(0, 0)
    bitcell_empty_master = bitcell_empty.master_track()

    bitcell_125_diskdef = codec.get_diskdef("raw.125")
    bitcell_125_track = bitcell_125_diskdef.mk_track(0, 0)
    bitcell_125_track.decode_flux(emit_flux)
    bitcell_125_master = bitcell_125_track.master_track()
    bitcell_125_empty = bitcell_125_diskdef.mk_track(0, 0)
    bitcell_125_empty_master = bitcell_125_empty.master_track()

    bitcell_500_diskdef = codec.get_diskdef("raw.500")
    bitcell_500_track = bitcell_500_diskdef.mk_track(0, 0)
    bitcell_500_track.decode_flux(emit_flux)
    bitcell_500_master = bitcell_500_track.master_track()
    bitcell_500_empty = bitcell_500_diskdef.mk_track(0, 0)
    bitcell_500_empty_master = bitcell_500_empty.master_track()

    ibm_scan_diskdef = codec.get_diskdef("ibm.scan")
    ibm_scan_source_diskdef = codec.get_diskdef("ibm.1440")
    ibm_scan_source_track = ibm_scan_source_diskdef.mk_track(0, 0)
    ibm_scan_source_data = bytes(((i * 61 + 5) & 0xFF) for i in range(ibm_scan_source_track.nsec * 512))
    ibm_scan_source_track.set_img_track(ibm_scan_source_data)
    ibm_scan_flux = ibm_scan_source_track.flux()
    ibm_scan_decoded = ibm_scan_diskdef.mk_track(0, 0)
    ibm_scan_decoded.decode_flux(ibm_scan_flux)
    ibm_scan_decoded_img = bytes(ibm_scan_decoded.get_img_track())

    ibm_scan_fm_source_diskdef = codec.get_diskdef("atari.90")
    ibm_scan_fm_source_track = ibm_scan_fm_source_diskdef.mk_track(0, 0)
    ibm_scan_fm_source_data = bytes(((i * 67 + 9) & 0xFF) for i in range(ibm_scan_fm_source_track.nsec * 128))
    ibm_scan_fm_source_track.set_img_track(ibm_scan_fm_source_data)
    ibm_scan_fm_flux = ibm_scan_fm_source_track.flux()
    ibm_scan_fm_decoded = ibm_scan_diskdef.mk_track(0, 0)
    ibm_scan_fm_decoded.decode_flux(ibm_scan_fm_flux)
    ibm_scan_fm_decoded_img = bytes(ibm_scan_fm_decoded.get_img_track())
    ibm_scan_write_error = None
    try:
        ibm_scan_diskdef.mk_track(0, 0).set_img_track(bytes(512))
    except Exception as ex:
        ibm_scan_write_error = str(ex)

    fm_diskdef = codec.get_diskdef("atari.90")
    fm_track = fm_diskdef.mk_track(0, 0)
    fm_data = bytes(((i * 5 + 1) & 0xFF) for i in range(fm_track.nsec * 128))
    fm_track.set_img_track(fm_data)
    fm_flux = fm_track.flux()
    fm_decoded = fm_diskdef.mk_track(0, 0)
    fm_decoded.decode_flux(fm_flux)
    fm_decoded_img = bytes(fm_decoded.get_img_track())

    rx_diskdef = codec.get_diskdef("dec.rx02")
    rx_track = rx_diskdef.mk_track(0, 0)
    rx_data = bytes(((i * 17 + 7) & 0xFF) for i in range(rx_track.nsec * 256))
    rx_track.set_img_track(rx_data)
    rx_flux = rx_track.flux()
    rx_decoded = rx_diskdef.mk_track(0, 0)
    rx_decoded.decode_flux(rx_flux)
    rx_decoded_img = bytes(rx_decoded.get_img_track())

    ibm_360_diskdef = codec.get_diskdef("ibm.360")
    ibm_360_track = ibm_360_diskdef.mk_track(0, 0)
    ibm_360_data = bytes(((i * 71 + 3) & 0xFF) for i in range(ibm_360_track.nsec * 512))
    ibm_360_track.set_img_track(ibm_360_data)
    ibm_360_flux = ibm_360_track.flux()
    ibm_360_decoded = ibm_360_diskdef.mk_track(0, 0)
    ibm_360_decoded.decode_flux(ibm_360_flux)
    ibm_360_decoded_img = bytes(ibm_360_decoded.get_img_track())

    ibm_1200_diskdef = codec.get_diskdef("ibm.1200")
    ibm_1200_track = ibm_1200_diskdef.mk_track(0, 0)
    ibm_1200_data = bytes(((i * 73 + 5) & 0xFF) for i in range(ibm_1200_track.nsec * 512))
    ibm_1200_track.set_img_track(ibm_1200_data)
    ibm_1200_flux = ibm_1200_track.flux()
    ibm_1200_decoded = ibm_1200_diskdef.mk_track(0, 0)
    ibm_1200_decoded.decode_flux(ibm_1200_flux)
    ibm_1200_decoded_img = bytes(ibm_1200_decoded.get_img_track())

    amiga_diskdef = codec.get_diskdef("amiga.amigados")
    amiga_track = amiga_diskdef.mk_track(0, 0)
    amiga_data = bytes(((i * 19 + 5) & 0xFF) for i in range(amiga_track.nsec * 512))
    amiga_track.set_img_track(amiga_data)
    amiga_flux = amiga_track.flux()
    amiga_decoded = amiga_diskdef.mk_track(0, 0)
    amiga_decoded.decode_flux(amiga_flux)
    amiga_decoded_img = bytes(amiga_decoded.get_img_track())

    amiga_head1_track = amiga_diskdef.mk_track(0, 1)
    amiga_head1_data = bytes(((i * 73 + 19) & 0xFF) for i in range(amiga_head1_track.nsec * 512))
    amiga_head1_track.set_img_track(amiga_head1_data)
    amiga_head1_flux = amiga_head1_track.flux()
    amiga_head1_decoded = amiga_diskdef.mk_track(0, 1)
    amiga_head1_decoded.decode_flux(amiga_head1_flux)
    amiga_head1_decoded_img = bytes(amiga_head1_decoded.get_img_track())

    amiga_hd_diskdef = codec.get_diskdef("amiga.amigados_hd")
    amiga_hd_track = amiga_hd_diskdef.mk_track(0, 0)
    amiga_hd_data = bytes(((i * 83 + 27) & 0xFF) for i in range(amiga_hd_track.nsec * 512))
    amiga_hd_track.set_img_track(amiga_hd_data)
    amiga_hd_flux = amiga_hd_track.flux()
    amiga_hd_decoded = amiga_hd_diskdef.mk_track(0, 0)
    amiga_hd_decoded.decode_flux(amiga_hd_flux)
    amiga_hd_decoded_img = bytes(amiga_hd_decoded.get_img_track())

    c64_case = None
    try:
        c64_diskdef = codec.get_diskdef("commodore.1541")
        c64_track = c64_diskdef.mk_track(0, 0)
        c64_data = bytes(((i * 97 + 43) & 0xFF) for i in range(c64_track.nsec * 256))
        c64_track.set_img_track(c64_data)
        c64_flux = c64_track.flux()
        c64_decoded = c64_diskdef.mk_track(0, 0)
        c64_decoded.decode_flux(c64_flux)
        c64_decoded_img = bytes(c64_decoded.get_img_track())
        c64_case = {
            "source_img_sha256": hashlib.sha256(c64_data).hexdigest(),
            "decoded_missing": c64_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(c64_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": c64_decoded_img[:16].hex(),
        }
    except Exception:
        # c64.gcr requires the python optimised extension in this environment.
        c64_case = None

    northstar_diskdef = codec.get_diskdef("northstar.fm.ss")
    northstar_track = northstar_diskdef.mk_track(0, 0)
    northstar_data = bytes(((i * 31 + 3) & 0xFF) for i in range(northstar_track.nsec * 256))
    northstar_track.set_img_track(northstar_data)
    northstar_flux = northstar_track.flux()
    northstar_decoded = northstar_diskdef.mk_track(0, 0)
    northstar_decoded.decode_flux(northstar_flux)
    northstar_decoded_img = bytes(northstar_decoded.get_img_track())

    northstar_mfm_diskdef = codec.get_diskdef("northstar.mfm.ss")
    northstar_mfm_track = northstar_mfm_diskdef.mk_track(0, 0)
    northstar_mfm_data = bytes(((i * 47 + 13) & 0xFF) for i in range(northstar_mfm_track.nsec * 512))
    northstar_mfm_track.set_img_track(northstar_mfm_data)
    northstar_mfm_flux = northstar_mfm_track.flux()
    northstar_mfm_decoded = northstar_mfm_diskdef.mk_track(0, 0)
    northstar_mfm_decoded.decode_flux(northstar_mfm_flux)
    northstar_mfm_decoded_img = bytes(northstar_mfm_decoded.get_img_track())

    micropolis_diskdef = codec.get_diskdef("micropolis.48tpi.ss")
    micropolis_track = micropolis_diskdef.mk_track(0, 0)
    micropolis_data = bytes(((i * 37 + 9) & 0xFF) for i in range(micropolis_track.nsec * 256))
    micropolis_track.set_img_track(micropolis_data)
    micropolis_flux = micropolis_track.flux()
    micropolis_decoded = micropolis_diskdef.mk_track(0, 0)
    micropolis_decoded.decode_flux(micropolis_flux)
    micropolis_decoded_img = bytes(micropolis_decoded.get_img_track())

    micropolis_275_diskdef = codec.get_diskdef("micropolis.48tpi.ss.275")
    micropolis_275_track = micropolis_275_diskdef.mk_track(0, 0)
    micropolis_275_data = bytearray(((i * 53 + 25) & 0xFF) for i in range(micropolis_275_track.nsec * 275))
    for sec in range(micropolis_275_track.nsec):
        base = sec * 275
        micropolis_275_data[base] = 0xFF
        micropolis_275_data[base + 1] = 0
        micropolis_275_data[base + 2] = sec & 0xFF
        for i in range(3, 269):
            micropolis_275_data[base + i] = (sec * 67 + i * 53 + 25) & 0xFF
        csum = 0
        for x in micropolis_275_data[base + 1:base + 269]:
            if csum > 255:
                csum -= 255
            csum += x
        micropolis_275_data[base + 269] = csum & 0xFF
        for i in range(270, 275):
            micropolis_275_data[base + i] = (sec * 29 + i * 11) & 0xFF
    micropolis_275_data = bytes(micropolis_275_data)
    micropolis_275_track.set_img_track(micropolis_275_data)
    micropolis_275_flux = micropolis_275_track.flux()
    micropolis_275_decoded = micropolis_275_diskdef.mk_track(0, 0)
    micropolis_275_decoded.decode_flux(micropolis_275_flux)
    micropolis_275_decoded_img = bytes(micropolis_275_decoded.get_img_track())

    hp_mmfm_case = None
    hp_mmfm_diskdef = codec.get_diskdef("hp.mmfm.9885")
    hp_mmfm_track = hp_mmfm_diskdef.mk_track(0, 0)
    hp_mmfm_data = bytes(((i * 41 + 17) & 0xFF) for i in range(hp_mmfm_track.nsec * 256))
    hp_mmfm_track.set_img_track(hp_mmfm_data)
    hp_mmfm_flux = hp_mmfm_track.flux()
    hp_mmfm_decoded = hp_mmfm_diskdef.mk_track(0, 0)
    hp_mmfm_decoded.decode_flux(hp_mmfm_flux)
    hp_mmfm_decoded_img = bytes(hp_mmfm_decoded.get_img_track())
    hp_mmfm_case = {
        "source_img_sha256": hashlib.sha256(hp_mmfm_data).hexdigest(),
        "decoded_missing": hp_mmfm_decoded.nr_missing(),
        "decoded_img_sha256": hashlib.sha256(hp_mmfm_decoded_img).hexdigest(),
        "decoded_img_prefix_hex": hp_mmfm_decoded_img[:16].hex(),
    }

    hp_mmfm_head1_diskdef = codec.get_diskdef("hp.mmfm.9895")
    hp_mmfm_head1_track = hp_mmfm_head1_diskdef.mk_track(0, 1)
    hp_mmfm_head1_data = bytes(((i * 59 + 7) & 0xFF) for i in range(hp_mmfm_head1_track.nsec * 256))
    hp_mmfm_head1_track.set_img_track(hp_mmfm_head1_data)
    hp_mmfm_head1_flux = hp_mmfm_head1_track.flux()
    hp_mmfm_head1_decoded = hp_mmfm_head1_diskdef.mk_track(0, 1)
    hp_mmfm_head1_decoded.decode_flux(hp_mmfm_head1_flux)
    hp_mmfm_head1_decoded_img = bytes(hp_mmfm_head1_decoded.get_img_track())

    datageneral_diskdef = codec.get_diskdef("datageneral.2f")
    datageneral_track = datageneral_diskdef.mk_track(0, 0)
    datageneral_data = bytes(((i * 43 + 21) & 0xFF) for i in range(datageneral_track.nsec * 512))
    datageneral_track.set_img_track(datageneral_data)
    datageneral_flux = datageneral_track.flux()
    datageneral_decoded = datageneral_diskdef.mk_track(0, 0)
    datageneral_decoded.decode_flux(datageneral_flux)
    datageneral_decoded_img = bytes(datageneral_decoded.get_img_track())

    mac_case = None
    mac_400_case = None
    try:
        mac_diskdef = codec.get_diskdef("mac.800")
        mac_track = mac_diskdef.mk_track(0, 0)
        mac_data = bytes(((i * 23 + 11) & 0xFF) for i in range(mac_track.nsec * 512))
        mac_track.set_img_track(mac_data)
        mac_flux = mac_track.flux()
        mac_decoded = mac_diskdef.mk_track(0, 0)
        mac_decoded.decode_flux(mac_flux)
        mac_decoded_img = bytes(mac_decoded.get_img_track())
        mac_case = {
            "source_img_sha256": hashlib.sha256(mac_data).hexdigest(),
            "decoded_missing": mac_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(mac_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": mac_decoded_img[:16].hex(),
        }
        mac_head1_track = mac_diskdef.mk_track(0, 1)
        mac_head1_data = bytes(((i * 79 + 17) & 0xFF) for i in range(mac_head1_track.nsec * 512))
        mac_head1_track.set_img_track(mac_head1_data)
        mac_head1_flux = mac_head1_track.flux()
        mac_head1_decoded = mac_diskdef.mk_track(0, 1)
        mac_head1_decoded.decode_flux(mac_head1_flux)
        mac_head1_decoded_img = bytes(mac_head1_decoded.get_img_track())
        mac_head1_case = {
            "source_img_sha256": hashlib.sha256(mac_head1_data).hexdigest(),
            "decoded_missing": mac_head1_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(mac_head1_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": mac_head1_decoded_img[:16].hex(),
        }

        mac_400_diskdef = codec.get_diskdef("mac.400")
        mac_400_track = mac_400_diskdef.mk_track(0, 0)
        mac_400_data = bytes(((i * 89 + 31) & 0xFF) for i in range(mac_400_track.nsec * 512))
        mac_400_track.set_img_track(mac_400_data)
        mac_400_flux = mac_400_track.flux()
        mac_400_decoded = mac_400_diskdef.mk_track(0, 0)
        mac_400_decoded.decode_flux(mac_400_flux)
        mac_400_decoded_img = bytes(mac_400_decoded.get_img_track())
        mac_400_case = {
            "source_img_sha256": hashlib.sha256(mac_400_data).hexdigest(),
            "decoded_missing": mac_400_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(mac_400_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": mac_400_decoded_img[:16].hex(),
        }
    except Exception:
        # mac.gcr requires the python optimised extension in this environment.
        mac_case = None
        mac_head1_case = None
        mac_400_case = None

    apple2_case = None
    try:
        apple2_diskdef = codec.get_diskdef("apple2.nofs.140")
        apple2_track = apple2_diskdef.mk_track(0, 0)
        apple2_data = bytes(((i * 29 + 7) & 0xFF) for i in range(apple2_track.nsec * 256))
        apple2_track.set_img_track(apple2_data)
        apple2_flux = apple2_track.flux()
        apple2_decoded = apple2_diskdef.mk_track(0, 0)
        apple2_decoded.decode_flux(apple2_flux)
        apple2_decoded_img = bytes(apple2_decoded.get_img_track())
        apple2_case = {
            "source_img_sha256": hashlib.sha256(apple2_data).hexdigest(),
            "decoded_missing": apple2_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(apple2_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": apple2_decoded_img[:16].hex(),
        }
    except Exception:
        # apple2.gcr requires the python optimised extension in this environment.
        apple2_case = None


    return {
        "diskdef_ibm_1440": {
            "cyls": diskdef.cyls,
            "heads": diskdef.heads,
            "trackset": diskdef.trackset(),
            "default_revs": diskdef.default_revs,
            "track_map_size": len(diskdef.track_map),
        },
        "formats": {
            "count": len(all_formats),
            "first10": all_formats[:10],
            "last10": all_formats[-10:],
            "print_sha256": digest,
        },
        "ibm_track_img_cases": {
            "consumed": consumed,
            "img_len": len(img_track),
            "img_sha256": hashlib.sha256(img_track).hexdigest(),
            "missing": track.nr_missing(),
            "has_sec0": bool(track.has_sec(0)),
            "has_last": bool(track.has_sec(track.nsec - 1)),
            "consumed_short": consumed_short,
            "img_short_len": len(img_track_short),
            "img_short_sha256": hashlib.sha256(img_track_short).hexdigest(),
            "img_short_prefix_hex": img_track_short[:16].hex(),
            "missing_short": track_short.nr_missing(),
        },
        "ibm_decode_case": {
            "index_list": [float(x) for x in source_flux.index_list],
            "flux_list": [float(x) for x in source_flux.list],
            "sample_freq": float(source_flux.sample_freq),
            "decoded_missing": decoded_track.nr_missing(),
            "decoded_img_len": len(decoded_img),
            "decoded_img_sha256": hashlib.sha256(decoded_img).hexdigest(),
            "decoded_img_prefix_hex": decoded_img[:16].hex(),
        },
        "ibm_emit_case": {
            "source_img_sha256": hashlib.sha256(emit_data).hexdigest(),
            "decoded_missing": emit_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(emit_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": emit_decoded_img[:16].hex(),
        },
        "bitcell_case": {
            "decoded_summary": bitcell_track.summary_string(),
            "decoded_bit_count": len(bitcell_master.bits),
            "decoded_time_per_rev": float(bitcell_master.time_per_rev),
            "empty_summary": bitcell_empty.summary_string(),
            "empty_bit_count": len(bitcell_empty_master.bits),
            "empty_weak_count": len(bitcell_empty_master.weak),
        },
        "bitcell_125_case": {
            "decoded_summary": bitcell_125_track.summary_string(),
            "decoded_time_per_rev": float(bitcell_125_master.time_per_rev),
            "empty_summary": bitcell_125_empty.summary_string(),
            "empty_bit_count": len(bitcell_125_empty_master.bits),
            "empty_weak_count": len(bitcell_125_empty_master.weak),
        },
        "bitcell_500_case": {
            "decoded_summary": bitcell_500_track.summary_string(),
            "decoded_time_per_rev": float(bitcell_500_master.time_per_rev),
            "empty_summary": bitcell_500_empty.summary_string(),
            "empty_bit_count": len(bitcell_500_empty_master.bits),
            "empty_weak_count": len(bitcell_500_empty_master.weak),
        },
        "ibm_scan_case": {
            "decoded_summary": ibm_scan_decoded.summary_string(),
            "decoded_missing": ibm_scan_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(ibm_scan_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": ibm_scan_decoded_img[:16].hex(),
            "write_error": ibm_scan_write_error,
        },
        "ibm_scan_fm_case": {
            "decoded_summary": ibm_scan_fm_decoded.summary_string(),
            "decoded_missing": ibm_scan_fm_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(ibm_scan_fm_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": ibm_scan_fm_decoded_img[:16].hex(),
        },
        "ibm_fm_case": {
            "source_img_sha256": hashlib.sha256(fm_data).hexdigest(),
            "decoded_missing": fm_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(fm_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": fm_decoded_img[:16].hex(),
        },
        "dec_rx02_case": {
            "source_img_sha256": hashlib.sha256(rx_data).hexdigest(),
            "decoded_missing": rx_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(rx_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": rx_decoded_img[:16].hex(),
        },
        "ibm_360_case": {
            "source_img_sha256": hashlib.sha256(ibm_360_data).hexdigest(),
            "decoded_missing": ibm_360_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(ibm_360_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": ibm_360_decoded_img[:16].hex(),
        },
        "ibm_1200_case": {
            "source_img_sha256": hashlib.sha256(ibm_1200_data).hexdigest(),
            "decoded_missing": ibm_1200_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(ibm_1200_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": ibm_1200_decoded_img[:16].hex(),
        },
        "amiga_case": {
            "source_img_sha256": hashlib.sha256(amiga_data).hexdigest(),
            "decoded_missing": amiga_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(amiga_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": amiga_decoded_img[:16].hex(),
        },
        "amiga_head1_case": {
            "source_img_sha256": hashlib.sha256(amiga_head1_data).hexdigest(),
            "decoded_missing": amiga_head1_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(amiga_head1_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": amiga_head1_decoded_img[:16].hex(),
        },
        "amiga_hd_case": {
            "source_img_sha256": hashlib.sha256(amiga_hd_data).hexdigest(),
            "decoded_missing": amiga_hd_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(amiga_hd_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": amiga_hd_decoded_img[:16].hex(),
        },
        "c64_case": c64_case,
        "mac_case": mac_case,
        "mac_head1_case": mac_head1_case,
        "mac_400_case": mac_400_case,
        "apple2_case": apple2_case,
        "northstar_case": {
            "source_img_sha256": hashlib.sha256(northstar_data).hexdigest(),
            "decoded_missing": northstar_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(northstar_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": northstar_decoded_img[:16].hex(),
        },
        "northstar_mfm_case": {
            "source_img_sha256": hashlib.sha256(northstar_mfm_data).hexdigest(),
            "decoded_missing": northstar_mfm_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(northstar_mfm_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": northstar_mfm_decoded_img[:16].hex(),
        },
        "micropolis_case": {
            "source_img_sha256": hashlib.sha256(micropolis_data).hexdigest(),
            "decoded_missing": micropolis_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(micropolis_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": micropolis_decoded_img[:16].hex(),
        },
        "micropolis_275_case": {
            "source_img_sha256": hashlib.sha256(micropolis_275_data).hexdigest(),
            "decoded_missing": micropolis_275_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(micropolis_275_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": micropolis_275_decoded_img[:16].hex(),
        },
        "hp_mmfm_case": hp_mmfm_case,
        "hp_mmfm_head1_case": {
            "source_img_sha256": hashlib.sha256(hp_mmfm_head1_data).hexdigest(),
            "decoded_missing": hp_mmfm_head1_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(hp_mmfm_head1_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": hp_mmfm_head1_decoded_img[:16].hex(),
        },
        "datageneral_case": {
            "source_img_sha256": hashlib.sha256(datageneral_data).hexdigest(),
            "decoded_missing": datageneral_decoded.nr_missing(),
            "decoded_img_sha256": hashlib.sha256(datageneral_decoded_img).hexdigest(),
            "decoded_img_prefix_hex": datageneral_decoded_img[:16].hex(),
        },
    }


def build_image_fixtures():
    class FakeTrack:
        def __init__(self, length):
            self.length = length
            self._data = bytes([0] * length)
            self.nsec = 1

        def set_img_track(self, dat):
            chunk = bytes(dat[: self.length])
            if len(chunk) < self.length:
                chunk += bytes(self.length - len(chunk))
            self._data = chunk
            return self.length

        def get_img_track(self):
            return self._data

        def nr_missing(self):
            return 0 if any(self._data) else self.nsec

    class FakeTrackSet:
        def __init__(self, cyls, heads):
            self.cyls = cyls
            self.heads = heads

    class FakeFmt:
        def __init__(self, lengths, cyls=None, heads=None):
            if cyls is None:
                cyls = [0, 1]
            if heads is None:
                heads = [0, 1]
            self.tracks = FakeTrackSet(cyls, heads)
            self._lengths = lengths

        def mk_track(self, cyl, head):
            key = (cyl, head)
            if key not in self._lengths:
                return None
            return FakeTrack(self._lengths[key])

    opts = ImageOpts()
    try:
        opts.r_set("x.img", "foo", "bar")
    except Exception as ex:
        invalid_msg = str(ex)
    else:
        invalid_msg = ""
    try:
        hfe_opt_probe = HFE("probe.hfe", None)
        hfe_opt_probe.opts.bitrate = "0"
    except Exception as ex:
        hfe_invalid_bitrate_msg = str(ex)
    else:
        hfe_invalid_bitrate_msg = ""
    try:
        hfe_opt_probe = HFE("probe.hfe", None)
        hfe_opt_probe.opts.interface = "bad_mode"
    except Exception as ex:
        hfe_invalid_interface_msg = str(ex).splitlines()[0] if str(ex) else ""
    else:
        hfe_invalid_interface_msg = ""
    try:
        hfe_opt_probe = HFE("probe.hfe", None)
        hfe_opt_probe.opts.encoding = "bad_encoding"
    except Exception as ex:
        hfe_invalid_encoding_msg = str(ex).splitlines()[0] if str(ex) else ""
    else:
        hfe_invalid_encoding_msg = ""
    try:
        hfe_opt_probe = HFE("probe.hfe", None)
        hfe_opt_probe.opts.version = "2"
    except Exception as ex:
        hfe_invalid_version_msg = str(ex)
    else:
        hfe_invalid_version_msg = ""
    try:
        hfe_flux_probe = HFE("probe.hfe", None)
        hfe_flux_probe.emit_track(
            0, 0,
            Flux([200000.0], [1000.0] * 200, 40000000, index_cued=True)
        )
    except Exception as ex:
        hfe_flux_requires_bitrate_msg = str(ex)
    else:
        hfe_flux_requires_bitrate_msg = ""
    lengths = {(0, 0): 3, (0, 1): 3, (1, 0): 3, (1, 1): 3}
    dat = bytes([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12])

    img_default = IMG("default.img", FakeFmt(lengths))
    img_default.from_bytes(dat)
    mapping_default = []
    for (c, h) in [(0, 0), (0, 1), (1, 0), (1, 1)]:
        t = img_default.get_track(c, h)
        mapping_default.append({"cyl": c, "head": h, "data_hex": t.get_img_track().hex()})
    default_image_hex = img_default.get_image().hex()

    img_swapped = IMG("swapped.img", FakeFmt(lengths))
    img_swapped.sides_swapped = True
    img_swapped.from_bytes(dat)
    mapping_swapped = []
    for (c, h) in [(0, 0), (0, 1), (1, 0), (1, 1)]:
        t = img_swapped.get_track(c, h)
        mapping_swapped.append({"cyl": c, "head": h, "data_hex": t.get_img_track().hex()})

    img_seq = IMG("seq.img", FakeFmt(lengths))
    img_seq.sequential = True
    img_seq.from_bytes(dat)
    sequential_image_hex = img_seq.get_image().hex()

    lengths_min = {(c, h): 3 for c in [0, 1, 2] for h in [0, 1]}
    dat_min = bytes([0] * (3 * 6))
    img_min = IMG("min.img", FakeFmt(lengths_min, cyls=[0, 1, 2], heads=[0, 1]))
    img_min.min_cyls = 2
    img_min.from_bytes(dat_min)
    min_no_extend_len = len(img_min.get_image())
    t = img_min.get_track(2, 1)
    t.set_img_track(bytes([1, 0, 0]))
    min_extend_len = len(img_min.get_image())

    with tempfile.TemporaryDirectory() as tmp:
        raw_name = os.path.join(tmp, "rawcase00.0.raw")
        raw = KryoFlux(raw_name, None)
        raw_input_index = [100000.0]
        raw_input_flux = [2000.0, 2500.0, 3000.0, 92500.0, 2100.0, 2600.0, 3100.0]
        raw_input_sample = 40000000.0
        raw.emit_track(0, 0, Flux(raw_input_index, raw_input_flux, raw_input_sample, index_cued=True))
        raw_decoded = raw.get_track(0, 0)
        raw_file_size = os.path.getsize(raw_name)

        raw_track11_name = os.path.join(tmp, "rawcase11.1.raw")
        raw_multi = KryoFlux(raw_track11_name, None)
        raw_multi_input_index = [95000.0]
        raw_multi_input_flux = [1800.0, 2000.0, 2300.0, 2600.0, 3000.0, 83300.0]
        raw_multi_input_sample = 36000000.0
        raw_multi.emit_track(11, 1, Flux(raw_multi_input_index, raw_multi_input_flux, raw_multi_input_sample, index_cued=True))
        raw_multi_decoded = raw_multi.get_track(11, 1)
        raw_track11_file_size = os.path.getsize(raw_track11_name)

        raw_revs_name = os.path.join(tmp, "rawrevs00.0.raw")
        raw_revs = KryoFlux(raw_revs_name, None)
        raw_revs.opts.revs = "1"
        raw_revs_input_index = [92000.0, 93000.0]
        raw_revs_input_flux = [2000.0, 2300.0, 2500.0, 85000.0, 1800.0, 2200.0, 2600.0, 84000.0]
        raw_revs_input_sample = 40000000.0
        raw_revs.emit_track(0, 0, Flux(raw_revs_input_index, raw_revs_input_flux, raw_revs_input_sample, index_cued=True))
        raw_revs_decoded = raw_revs.get_track(0, 0)
        raw_revs_file_size = os.path.getsize(raw_revs_name)

        raw_sck_name = os.path.join(tmp, "rawsck00.0.raw")
        raw_sck = KryoFlux(raw_sck_name, None)
        raw_sck.opts.sck = "72m"
        raw_sck_input_index = [88000.0]
        raw_sck_input_flux = [1700.0, 2100.0, 2600.0, 81600.0]
        raw_sck_input_sample = 40000000.0
        raw_sck.emit_track(0, 0, Flux(raw_sck_input_index, raw_sck_input_flux, raw_sck_input_sample, index_cued=True))
        raw_sck_decoded = raw_sck.get_track(0, 0)
        raw_sck_file_size = os.path.getsize(raw_sck_name)

        def build_d88_disk(track_sectors, media_flag=0x00):
            # track_sectors: list of dict(c,h,r,n,mfm_flag,data)
            num = len(track_sectors)
            track_blob = bytearray()
            for s in track_sectors:
                c = s["c"] & 0xFF
                h = s["h"] & 0xFF
                r = s["r"] & 0xFF
                n = s["n"] & 0xFF
                mfm_flag = s["mfm_flag"] & 0xFF
                data = bytes(s["data"])
                track_blob += struct.pack("<BBBBHBBB5xH", c, h, r, n, num, mfm_flag, 0, 0, len(data))
                track_blob += data

            track_offset = 32 + 640
            disk_size = track_offset + len(track_blob)
            header = struct.pack("<16sB9xBBL", b"GW-D88-FIXTURE\0\0\0", 0, 0, media_flag, disk_size)
            table = [0] * 160
            table[0] = track_offset
            table_bytes = struct.pack("<160L", *table)
            return header + table_bytes + bytes(track_blob)

        d88_mfm_sectors = []
        for sec_id in [1, 2]:
            payload = bytes(((sec_id * 37 + i * 11 + 5) & 0xFF) for i in range(512))
            d88_mfm_sectors.append({"c": 0, "h": 0, "r": sec_id, "n": 2, "mfm_flag": 0x00, "data": payload})
        d88_mfm_bytes = build_d88_disk(d88_mfm_sectors, media_flag=0x00)
        d88_mfm_name = os.path.join(tmp, "mfm.d88")
        with open(d88_mfm_name, "wb") as f:
            f.write(d88_mfm_bytes)
        d88_mfm_img = D88.from_file(d88_mfm_name, None, {})
        d88_mfm_track = d88_mfm_img.get_track(0, 0)

        d88_fm_sectors = []
        for sec_id in [1, 2]:
            payload = bytes(((sec_id * 19 + i * 7 + 3) & 0xFF) for i in range(256))
            d88_fm_sectors.append({"c": 0, "h": 0, "r": sec_id, "n": 1, "mfm_flag": 0x40, "data": payload})
        d88_fm_bytes = build_d88_disk(d88_fm_sectors, media_flag=0x00)
        d88_fm_name = os.path.join(tmp, "fm.d88")
        with open(d88_fm_name, "wb") as f:
            f.write(d88_fm_bytes)
        d88_fm_img = D88.from_file(d88_fm_name, None, {})
        d88_fm_track = d88_fm_img.get_track(0, 0)

        d88_multi_bytes = d88_mfm_bytes + d88_fm_bytes
        d88_multi_name = os.path.join(tmp, "multi.d88")
        with open(d88_multi_name, "wb") as f:
            f.write(d88_multi_bytes)
        d88_multi_index1_img = D88.from_file(d88_multi_name, None, {"index": "1"})
        d88_multi_index1_track = d88_multi_index1_img.get_track(0, 0)

        d88_index_error_cases = []
        for idx in ["-1", "abc", "2"]:
            try:
                D88.from_file(d88_multi_name, None, {"index": idx})
            except Exception as ex:
                d88_index_error_cases.append(
                    {
                        "index": idx,
                        "error": str(ex),
                    }
                )

        dmk_track_data = bytearray([0x4E] * 6250)
        dmk_off = 200
        dmk_track_data[dmk_off + 0:dmk_off + 8] = b"\xa1\xa1\xa1\xfe\x00\x00\x01\x02"
        dmk_track_data[dmk_off + 22:dmk_off + 25] = b"\xa1\xa1\xa1"
        dmk_track_data[dmk_off + 25] = 0xFB
        dmk_idam_table = [0] * 64
        dmk_idam_table[0] = 0x8000 | (dmk_off + 128)
        dmk_tlen = 128 + len(dmk_track_data)
        dmk_header = struct.pack("<2BHB11x", 0, 1, dmk_tlen, 0x10)
        dmk_bytes = dmk_header + struct.pack("<64H", *dmk_idam_table) + bytes(dmk_track_data)
        dmk_img = DMK("mfm.dmk", None)
        dmk_img.from_bytes(dmk_bytes)
        dmk_track = dmk_img.get_track(0, 0)

        edsk_sector_a = bytes(((0x11 + i * 3) & 0xFF) for i in range(512))
        edsk_sector_b = bytes(((0xA7 + i * 5) & 0xFF) for i in range(512))
        edsk_track_header = struct.pack(
            "<12s4x8B",
            b"Track-Info\r\n",
            0, 0, 0, 0, 2, 2, 0x2A, 0xE5
        )
        edsk_sector_info = (
            struct.pack("<6BH", 0, 0, 1, 2, 0, 0, len(edsk_sector_a)) +
            struct.pack("<6BH", 0, 0, 2, 2, 0, 0, len(edsk_sector_b))
        )
        edsk_track_block = (edsk_track_header + edsk_sector_info).ljust(256, b"\x00") + edsk_sector_a + edsk_sector_b
        edsk_track_size = len(edsk_track_block) // 256
        edsk_disk_header = struct.pack(
            "<34s14s2BH",
            b"EXTENDED CPC DSK File\r\nDisk-Info\r\n",
            b"GW-FIXTURE-EDSK",
            1, 1, 0
        )
        edsk_size_table = bytes([edsk_track_size]) + bytes(203)
        edsk_bytes = (edsk_disk_header + edsk_size_table).ljust(256, b"\x00") + edsk_track_block
        edsk_img = EDSK("fixture.dsk", None)
        edsk_img.from_bytes(edsk_bytes)
        edsk_track = edsk_img.get_track(0, 0)

        dim_header = bytearray(256)
        dim_header[0] = 0  # pc98.2hd
        dim_header[0xAB:0xB8] = b"DIFC HEADER  "
        dim_body = bytes(((0x5D + i * 7) & 0xFF) for i in range(18 * 1024))
        dim_bytes = bytes(dim_header) + dim_body
        dim_name = os.path.join(tmp, "fixture.dim")
        with open(dim_name, "wb") as f:
            f.write(dim_bytes)
        dim_img = DIM.from_file(dim_name, None, {})
        dim_track = dim_img.get_track(0, 0)

        td0_rate = 0
        td0_header_no_crc = struct.pack("<2s2x2BxBxB", b"TD", 0x21, td0_rate, 0, 1)
        td0_header_crc = td0_crc.new(td0_header_no_crc).crcValue
        td0_header = td0_header_no_crc + struct.pack("<H", td0_header_crc)
        td0_sector_a = bytes(((0x21 + i * 7) & 0xFF) for i in range(512))
        td0_sector_b = bytes(((0x97 + i * 9) & 0xFF) for i in range(512))
        td0_track_no_crc = bytes([2, 0, 0])
        td0_track_crc = td0_crc.new(td0_track_no_crc).crcValue & 0xFF
        td0_track_header = td0_track_no_crc + bytes([td0_track_crc])
        td0_sec_a_crc = td0_crc.new(td0_sector_a).crcValue & 0xFF
        td0_sec_b_crc = td0_crc.new(td0_sector_b).crcValue & 0xFF
        td0_sec_a = struct.pack("<6BHB", 0, 0, 1, 2, 0, td0_sec_a_crc, len(td0_sector_a) + 1, 0) + td0_sector_a
        td0_sec_b = struct.pack("<6BHB", 0, 0, 2, 2, 0, td0_sec_b_crc, len(td0_sector_b) + 1, 0) + td0_sector_b
        td0_bytes = td0_header + td0_track_header + td0_sec_a + td0_sec_b + b"\xFF"
        td0_img = TD0("fixture.td0", None)
        td0_img.from_bytes(td0_bytes)
        td0_track = td0_img.get_track(0, 0)

        nfd_sector_a = bytes(((0x41 + i * 13) & 0xFF) for i in range(512))
        nfd_sector_b = bytes(((0xB3 + i * 17) & 0xFF) for i in range(512))
        nfd_header_size = 288 + (163 * 26 * 16)
        nfd_header = struct.pack(
            "<15sx256sLBB10x",
            b"T98FDDIMAGE.R0\0",
            b"GW-NFD-FIXTURE".ljust(256, b"\0"),
            nfd_header_size,
            0,
            2,
        )
        nfd_track_headers = bytearray([0xFF] * (163 * 26 * 16))
        nfd_track_headers[0:16] = struct.pack("<11B5x", 0, 0, 1, 2, 1, 0, 0, 0, 0, 0, 0x90)
        nfd_track_headers[16:32] = struct.pack("<11B5x", 0, 0, 2, 2, 1, 0, 0, 0, 0, 0, 0x90)
        nfd_bytes = nfd_header + bytes(nfd_track_headers) + nfd_sector_a + nfd_sector_b
        nfd_name = os.path.join(tmp, "fixture.nfd")
        with open(nfd_name, "wb") as f:
            f.write(nfd_bytes)
        nfd_img = NFD.from_file(nfd_name, None, {})
        nfd_track = nfd_img.get_track(0, 0)

        dcp_header = bytearray(162)
        dcp_header[0] = 1
        dcp_track = bytes(((0x29 + i * 19) & 0xFF) for i in range(8 * 1024))
        dcp_bytes = bytes(dcp_header) + dcp_track
        dcp_name = os.path.join(tmp, "fixture.dcp")
        with open(dcp_name, "wb") as f:
            f.write(dcp_bytes)
        dcp_img = DCP.from_file(dcp_name, None, {})
        dcp_track0 = dcp_img.get_track(0, 0)

        a2r_rwcp = bytearray(16)
        a2r_rwcp[0] = 1
        a2r_rwcp[1:5] = struct.pack("<I", 25000)  # 40MHz sample clock
        a2r_capture_flux = bytes([20, 30, 40, 255, 12, 18, 22, 28, 34, 36])
        a2r_capture = (
            b"C" +
            struct.pack("<BHB", 3, 0, 2) +
            struct.pack("<II", 100000, 200000) +
            struct.pack("<I", len(a2r_capture_flux)) +
            a2r_capture_flux
        )
        a2r_rwcp += a2r_capture
        a2r_rwcp += b"\x00"
        a2r_bytes = b"A2R3\xff\x0a\x0d\x0a" + struct.pack("<4sI", b"RWCP", len(a2r_rwcp)) + bytes(a2r_rwcp)
        a2r_img = A2R("fixture.a2r", None)
        a2r_img.from_bytes(a2r_bytes)
        a2r_track = a2r_img.get_track(0, 0)

        msa_track_data = bytes(((0x31 + i * 7) & 0xFF) for i in range(9 * 512))
        msa_bytes = (
            struct.pack(">2s4H", b"\x0e\x0f", 9, 0, 0, 0) +
            struct.pack(">H", len(msa_track_data)) +
            msa_track_data
        )
        msa_img = MSA("fixture.msa", None)
        msa_img.from_bytes(msa_bytes)
        msa_track0 = msa_img.get_track(0, 0)

        apridisk_header = bytearray(128)
        apridisk_sig = b"ACT Apricot disk image\x1a\x04"
        apridisk_header[:len(apridisk_sig)] = apridisk_sig
        apridisk_payload = bytearray()
        for sec in range(1, 10):
            sec_data = bytes(((0x55 + sec * 11 + i * 5) & 0xFF) for i in range(512))
            rec = bytearray(16)
            rec[0:4] = (0xE31D0001).to_bytes(4, byteorder="little")
            rec[4:6] = (0x9E90).to_bytes(2, byteorder="little")
            rec[6:8] = (16).to_bytes(2, byteorder="little")
            rec[8:12] = (len(sec_data)).to_bytes(4, byteorder="little")
            rec[12] = 0
            rec[13] = sec
            rec[14:16] = (0).to_bytes(2, byteorder="little")
            apridisk_payload += rec + sec_data
        apridisk_bytes = bytes(apridisk_header) + bytes(apridisk_payload)
        apridisk_fmt = codec.get_diskdef("ibm.720")
        apridisk_img = Apridisk("fixture_apri.dsk", apridisk_fmt)
        apridisk_img.from_bytes(apridisk_bytes)
        apridisk_track0 = apridisk_img.get_track(0, 0)



        imd_mfm_src = codec.get_diskdef("ibm.720").mk_track(0, 0)
        imd_mfm_payload = bytes(((13 + i * 5) & 0xFF) for i in range(imd_mfm_src.nsec * 512))
        imd_mfm_src.set_img_track(imd_mfm_payload)
        imd_mfm = IMD("mfm.imd", None)
        imd_mfm.emit_track(0, 0, imd_mfm_src)
        imd_mfm_bytes = imd_mfm.get_image()
        imd_mfm_roundtrip = IMD("mfm_roundtrip.imd", None)
        imd_mfm_roundtrip.from_bytes(imd_mfm_bytes)
        imd_mfm_track = imd_mfm_roundtrip.get_track(0, 0)

        imd_fm_src = codec.get_diskdef("atari.90").mk_track(0, 0)
        imd_fm_payload = bytes(((7 + i * 3) & 0xFF) for i in range(imd_fm_src.nsec * 128))
        imd_fm_src.set_img_track(imd_fm_payload)
        imd_fm = IMD("fm.imd", None)
        imd_fm.emit_track(0, 0, imd_fm_src)
        imd_fm_bytes = imd_fm.get_image()
        imd_fm_roundtrip = IMD("fm_roundtrip.imd", None)
        imd_fm_roundtrip.from_bytes(imd_fm_bytes)
        imd_fm_track = imd_fm_roundtrip.get_track(0, 0)

        hfe_mfm_src = codec.get_diskdef("ibm.720").mk_track(0, 0)
        hfe_mfm_payload = bytes(((31 + i * 9) & 0xFF) for i in range(hfe_mfm_src.nsec * 512))
        hfe_mfm_src.set_img_track(hfe_mfm_payload)
        hfe_mfm = HFE("mfm.hfe", None)
        hfe_mfm.emit_track(0, 0, hfe_mfm_src)
        hfe_mfm_bytes = hfe_mfm.get_image()
        hfe_mfm_roundtrip = HFE("mfm_roundtrip.hfe", None)
        hfe_mfm_roundtrip.from_bytes(hfe_mfm_bytes)
        hfe_mfm_track = hfe_mfm_roundtrip.get_track(0, 0)

        hfe_mfm_bitrate = HFE("mfm_bitrate.hfe", None)
        hfe_mfm_bitrate.opts.bitrate = "300"
        hfe_mfm_bitrate.emit_track(0, 0, hfe_mfm_src)
        hfe_mfm_bitrate_bytes = hfe_mfm_bitrate.get_image()
        hfe_mfm_bitrate_roundtrip = HFE("mfm_bitrate_roundtrip.hfe", None)
        hfe_mfm_bitrate_roundtrip.from_bytes(hfe_mfm_bitrate_bytes)
        hfe_mfm_bitrate_track = hfe_mfm_bitrate_roundtrip.get_track(0, 0)

        hfe_mfm_double_step = HFE("mfm_double_step.hfe", None)
        hfe_mfm_double_step.opts.double_step = "yes"
        hfe_mfm_double_step.emit_track(0, 0, hfe_mfm_src)
        hfe_mfm_double_step_bytes = hfe_mfm_double_step.get_image()
        hfe_mfm_double_step_roundtrip = HFE("mfm_double_step_roundtrip.hfe", None)
        hfe_mfm_double_step_roundtrip.from_bytes(hfe_mfm_double_step_bytes)
        hfe_mfm_double_step_track = hfe_mfm_double_step_roundtrip.get_track(0, 0)

        hfe_mfm_header_opts = HFE("mfm_header_opts.hfe", None)
        hfe_mfm_header_opts.opts.interface = "AMIGA_HD"
        hfe_mfm_header_opts.opts.encoding = "AMIGA_MFM"
        hfe_mfm_header_opts.opts.double_step = "1"
        hfe_mfm_header_opts.emit_track(0, 0, hfe_mfm_src)
        hfe_mfm_header_opts_bytes = hfe_mfm_header_opts.get_image()
        hfe_mfm_header_opts_roundtrip = HFE("mfm_header_opts_roundtrip.hfe", None)
        hfe_mfm_header_opts_roundtrip.from_bytes(hfe_mfm_header_opts_bytes)
        hfe_mfm_header_opts_track = hfe_mfm_header_opts_roundtrip.get_track(0, 0)

        hfe_mfm_header_opts_numeric = HFE("mfm_header_opts_numeric.hfe", None)
        hfe_mfm_header_opts_numeric.opts.interface = "0xA5"
        hfe_mfm_header_opts_numeric.opts.encoding = "0x5A"
        hfe_mfm_header_opts_numeric.emit_track(0, 0, hfe_mfm_src)
        hfe_mfm_header_opts_numeric_bytes = hfe_mfm_header_opts_numeric.get_image()
        hfe_mfm_header_opts_numeric_roundtrip = HFE("mfm_header_opts_numeric_roundtrip.hfe", None)
        hfe_mfm_header_opts_numeric_roundtrip.from_bytes(hfe_mfm_header_opts_numeric_bytes)
        hfe_mfm_header_opts_numeric_track = hfe_mfm_header_opts_numeric_roundtrip.get_track(0, 0)

        hfe_flux_with_bitrate = HFE("flux_with_bitrate.hfe", None)
        hfe_flux_with_bitrate.opts.bitrate = "250"
        hfe_flux_with_bitrate.emit_track(
            0, 0,
            Flux([200000.0], [1000.0] * 200, 40000000, index_cued=True)
        )
        hfe_flux_with_bitrate_bytes = hfe_flux_with_bitrate.get_image()
        hfe_flux_with_bitrate_roundtrip = HFE("flux_with_bitrate_roundtrip.hfe", None)
        hfe_flux_with_bitrate_roundtrip.from_bytes(hfe_flux_with_bitrate_bytes)
        hfe_flux_with_bitrate_track = hfe_flux_with_bitrate_roundtrip.get_track(0, 0)

        hfe_fm_src = codec.get_diskdef("atari.90").mk_track(0, 0)
        hfe_fm_payload = bytes(((17 + i * 5) & 0xFF) for i in range(hfe_fm_src.nsec * 128))
        hfe_fm_src.set_img_track(hfe_fm_payload)
        hfe_fm = HFE("fm.hfe", None)
        hfe_fm.emit_track(0, 0, hfe_fm_src)
        hfe_fm_bytes = hfe_fm.get_image()
        hfe_fm_roundtrip = HFE("fm_roundtrip.hfe", None)
        hfe_fm_roundtrip.from_bytes(hfe_fm_bytes)
        hfe_fm_track = hfe_fm_roundtrip.get_track(0, 0)

        hfe_v3_mfm = HFE("mfm_v3.hfe", None)
        hfe_v3_mfm.opts.version = 3
        hfe_v3_mfm.emit_track(0, 0, hfe_mfm_src)
        hfe_v3_mfm_bytes = hfe_v3_mfm.get_image()
        hfe_v3_mfm_roundtrip = HFE("mfm_v3_roundtrip.hfe", None)
        hfe_v3_mfm_roundtrip.from_bytes(hfe_v3_mfm_bytes)
        hfe_v3_mfm_track = hfe_v3_mfm_roundtrip.get_track(0, 0)

        hfe_v3_uniform_mfm = HFE("mfm_v3_uniform.hfe", None)
        hfe_v3_uniform_mfm.opts.version = 3
        hfe_v3_uniform_mfm.opts.uniform = "yes"
        hfe_v3_uniform_mfm.emit_track(0, 0, hfe_mfm_src)
        hfe_v3_uniform_mfm_bytes = hfe_v3_uniform_mfm.get_image()
        hfe_v3_uniform_mfm_roundtrip = HFE("mfm_v3_uniform_roundtrip.hfe", None)
        hfe_v3_uniform_mfm_roundtrip.from_bytes(hfe_v3_uniform_mfm_bytes)
        hfe_v3_uniform_mfm_track = hfe_v3_uniform_mfm_roundtrip.get_track(0, 0)

        hfe_v3_fm = HFE("fm_v3.hfe", None)
        hfe_v3_fm.opts.version = 3
        hfe_v3_fm.emit_track(0, 0, hfe_fm_src)
        hfe_v3_fm_bytes = hfe_v3_fm.get_image()
        hfe_v3_fm_roundtrip = HFE("fm_v3_roundtrip.hfe", None)
        hfe_v3_fm_roundtrip.from_bytes(hfe_v3_fm_bytes)
        hfe_v3_fm_track = hfe_v3_fm_roundtrip.get_track(0, 0)

    return {
        "invalid_option_message": invalid_msg,
        "hfe_invalid_bitrate_message": hfe_invalid_bitrate_msg,
        "hfe_invalid_interface_message": hfe_invalid_interface_msg,
        "hfe_invalid_encoding_message": hfe_invalid_encoding_msg,
        "hfe_invalid_version_message": hfe_invalid_version_msg,
        "hfe_flux_requires_bitrate_message": hfe_flux_requires_bitrate_msg,
        "img_cases": {
            "input_hex": dat.hex(),
            "default_mapping": mapping_default,
            "default_image_hex": default_image_hex,
            "swapped_mapping": mapping_swapped,
            "sequential_image_hex": sequential_image_hex,
            "min_cyls_no_extend_len": min_no_extend_len,
            "min_cyls_extend_len": min_extend_len,
        },
        "raw_cases": {
            "emit_parse": {
                "input_index_list": [float(x) for x in raw_input_index],
                "input_flux_list": [float(x) for x in raw_input_flux],
                "input_sample_freq": float(raw_input_sample),
                "decoded_index_list": [float(x) for x in raw_decoded.index_list],
                "decoded_flux_prefix": [float(x) for x in raw_decoded.list[:16]],
                "decoded_flux_count": len(raw_decoded.list),
                "decoded_sample_freq": float(raw_decoded.sample_freq),
                "file_size": raw_file_size,
            },
            "multi_track_emit": {
                "cyl": 11,
                "head": 1,
                "input_index_list": [float(x) for x in raw_multi_input_index],
                "input_flux_list": [float(x) for x in raw_multi_input_flux],
                "input_sample_freq": float(raw_multi_input_sample),
                "decoded_index_list": [float(x) for x in raw_multi_decoded.index_list],
                "decoded_flux_prefix": [float(x) for x in raw_multi_decoded.list[:16]],
                "decoded_flux_count": len(raw_multi_decoded.list),
                "decoded_sample_freq": float(raw_multi_decoded.sample_freq),
                "file_size": raw_track11_file_size,
            },
            "revs_emit_parse": {
                "input_revs": 1,
                "input_index_list": [float(x) for x in raw_revs_input_index],
                "input_flux_list": [float(x) for x in raw_revs_input_flux],
                "input_sample_freq": float(raw_revs_input_sample),
                "decoded_index_list": [float(x) for x in raw_revs_decoded.index_list],
                "decoded_flux_prefix": [float(x) for x in raw_revs_decoded.list[:16]],
                "decoded_flux_count": len(raw_revs_decoded.list),
                "decoded_sample_freq": float(raw_revs_decoded.sample_freq),
                "file_size": raw_revs_file_size,
            },
            "sck_emit_parse": {
                "input_sck": "72m",
                "input_index_list": [float(x) for x in raw_sck_input_index],
                "input_flux_list": [float(x) for x in raw_sck_input_flux],
                "input_sample_freq": float(raw_sck_input_sample),
                "decoded_index_list": [float(x) for x in raw_sck_decoded.index_list],
                "decoded_flux_prefix": [float(x) for x in raw_sck_decoded.list[:16]],
                "decoded_flux_count": len(raw_sck_decoded.list),
                "decoded_sample_freq": float(raw_sck_decoded.sample_freq),
                "file_size": raw_sck_file_size,
            },
        },
        "d88_cases": {
            "mfm": {
                "file_hex": d88_mfm_bytes.hex(),
                "track_present": d88_mfm_track is not None,
                "track_summary": d88_mfm_track.summary_string() if d88_mfm_track is not None else "",
                "track_sector_count": d88_mfm_track.nsec if d88_mfm_track is not None else 0,
                "track_missing": d88_mfm_track.nr_missing() if d88_mfm_track is not None else 0,
                "track_img_sha256": hashlib.sha256(bytes(d88_mfm_track.get_img_track())).hexdigest() if d88_mfm_track is not None else "",
                "track_img_prefix_hex": bytes(d88_mfm_track.get_img_track())[:16].hex() if d88_mfm_track is not None else "",
                "track01_present": d88_mfm_img.get_track(0, 1) is not None,
            },
            "fm": {
                "file_hex": d88_fm_bytes.hex(),
                "track_present": d88_fm_track is not None,
                "track_summary": d88_fm_track.summary_string() if d88_fm_track is not None else "",
                "track_sector_count": d88_fm_track.nsec if d88_fm_track is not None else 0,
                "track_missing": d88_fm_track.nr_missing() if d88_fm_track is not None else 0,
                "track_img_sha256": hashlib.sha256(bytes(d88_fm_track.get_img_track())).hexdigest() if d88_fm_track is not None else "",
                "track_img_prefix_hex": bytes(d88_fm_track.get_img_track())[:16].hex() if d88_fm_track is not None else "",
                "track01_present": d88_fm_img.get_track(0, 1) is not None,
            },
            "multi_index1": {
                "file_hex": d88_multi_bytes.hex(),
                "track_present": d88_multi_index1_track is not None,
                "track_summary": d88_multi_index1_track.summary_string() if d88_multi_index1_track is not None else "",
                "track_sector_count": d88_multi_index1_track.nsec if d88_multi_index1_track is not None else 0,
                "track_missing": d88_multi_index1_track.nr_missing() if d88_multi_index1_track is not None else 0,
                "track_img_sha256": hashlib.sha256(bytes(d88_multi_index1_track.get_img_track())).hexdigest() if d88_multi_index1_track is not None else "",
                "track_img_prefix_hex": bytes(d88_multi_index1_track.get_img_track())[:16].hex() if d88_multi_index1_track is not None else "",
                "track01_present": d88_multi_index1_img.get_track(0, 1) is not None,
            },
            "index_error_cases": d88_index_error_cases,
        },
        "dmk_case": {
            "file_hex": dmk_bytes.hex(),
            "track_present": dmk_track is not None,
            "bit_length": len(dmk_track.bits) if dmk_track is not None else 0,
            "time_per_rev": float(dmk_track.time_per_rev) if dmk_track is not None else 0.0,
            "bitrate": float(dmk_track.bitrate) if dmk_track is not None else 0.0,
            "bits_sha256": hashlib.sha256(dmk_track.bits.tobytes()).hexdigest() if dmk_track is not None else "",
            "bits_prefix_hex": dmk_track.bits.tobytes()[:16].hex() if dmk_track is not None else "",
            "track01_present": dmk_img.get_track(0, 1) is not None,
        },
        "edsk_case": {
            "file_hex": edsk_bytes.hex(),
            "track_present": edsk_track is not None,
            "bit_length": len(edsk_track.bits) if edsk_track is not None else 0,
            "time_per_rev": float(edsk_track.time_per_rev) if edsk_track is not None else 0.0,
            "bitrate": float(edsk_track.bitrate) if edsk_track is not None else 0.0,
            "bits_sha256": hashlib.sha256(edsk_track.bits.tobytes()).hexdigest() if edsk_track is not None else "",
            "bits_prefix_hex": edsk_track.bits.tobytes()[:16].hex() if edsk_track is not None else "",
            "track01_present": edsk_img.get_track(0, 1) is not None,
        },
        "dim_case": {
            "file_hex": dim_bytes.hex(),
            "track_present": dim_track is not None,
            "track_summary": dim_track.summary_string() if dim_track is not None else "",
            "track_sector_count": dim_track.nsec if dim_track is not None else 0,
            "track_missing": dim_track.nr_missing() if dim_track is not None else 0,
            "track_img_sha256": hashlib.sha256(bytes(dim_track.get_img_track())).hexdigest() if dim_track is not None else "",
            "track_img_prefix_hex": bytes(dim_track.get_img_track())[:16].hex() if dim_track is not None else "",
            "track01_present": dim_img.get_track(0, 1) is not None,
        },
        "td0_case": {
            "file_hex": td0_bytes.hex(),
            "track_present": td0_track is not None,
            "track_summary": td0_track.summary_string() if td0_track is not None else "",
            "track_sector_count": td0_track.nsec if td0_track is not None else 0,
            "track_missing": td0_track.nr_missing() if td0_track is not None else 0,
            "track_img_sha256": hashlib.sha256(bytes(td0_track.get_img_track())).hexdigest() if td0_track is not None else "",
            "track_img_prefix_hex": bytes(td0_track.get_img_track())[:16].hex() if td0_track is not None else "",
            "track01_present": td0_img.get_track(0, 1) is not None,
        },
        "nfd_case": {
            "file_hex": nfd_bytes.hex(),
            "track_present": nfd_track is not None,
            "track_summary": nfd_track.summary_string() if nfd_track is not None else "",
            "track_sector_count": nfd_track.nsec if nfd_track is not None else 0,
            "track_missing": nfd_track.nr_missing() if nfd_track is not None else 0,
            "track_img_sha256": hashlib.sha256(bytes(nfd_track.get_img_track())).hexdigest() if nfd_track is not None else "",
            "track_img_prefix_hex": bytes(nfd_track.get_img_track())[:16].hex() if nfd_track is not None else "",
            "track01_present": nfd_img.get_track(0, 1) is not None,
        },
        "dcp_case": {
            "file_hex": dcp_bytes.hex(),
            "track_present": dcp_track0 is not None,
            "track_summary": dcp_track0.summary_string() if dcp_track0 is not None else "",
            "track_sector_count": dcp_track0.nsec if dcp_track0 is not None else 0,
            "track_missing": dcp_track0.nr_missing() if dcp_track0 is not None else 0,
            "track_img_sha256": hashlib.sha256(bytes(dcp_track0.get_img_track())).hexdigest() if dcp_track0 is not None else "",
            "track_img_prefix_hex": bytes(dcp_track0.get_img_track())[:16].hex() if dcp_track0 is not None else "",
            "track01_present": dcp_img.get_track(0, 1) is not None,
        },
        "a2r_case": {
            "file_hex": a2r_bytes.hex(),
            "track_present": a2r_track is not None,
            "index_count": len(a2r_track.index_list) if a2r_track is not None else 0,
            "flux_count": len(a2r_track.list) if a2r_track is not None else 0,
            "flux_total": float(sum(a2r_track.list)) if a2r_track is not None else 0.0,
            "flux_prefix": [float(x) for x in (a2r_track.list[:16] if a2r_track is not None else [])],
            "track01_present": a2r_img.get_track(0, 1) is not None,
        },
        "msa_case": {
            "file_hex": msa_bytes.hex(),
            "track_present": msa_track0 is not None,
            "track_summary": msa_track0.summary_string() if msa_track0 is not None else "",
            "track_sector_count": msa_track0.nsec if msa_track0 is not None else 0,
            "track_missing": msa_track0.nr_missing() if msa_track0 is not None else 0,
            "track_img_sha256": hashlib.sha256(bytes(msa_track0.get_img_track())).hexdigest() if msa_track0 is not None else "",
            "track_img_prefix_hex": bytes(msa_track0.get_img_track())[:16].hex() if msa_track0 is not None else "",
            "track01_present": msa_img.get_track(0, 1) is not None,
        },
        "apridisk_case": {
            "file_hex": apridisk_bytes.hex(),
            "track_present": apridisk_track0 is not None,
            "track_summary": apridisk_track0.summary_string() if apridisk_track0 is not None else "",
            "track_sector_count": apridisk_track0.nsec if apridisk_track0 is not None else 0,
            "track_missing": apridisk_track0.nr_missing() if apridisk_track0 is not None else 0,
            "track_img_sha256": hashlib.sha256(bytes(apridisk_track0.get_img_track())).hexdigest() if apridisk_track0 is not None else "",
            "track_img_prefix_hex": bytes(apridisk_track0.get_img_track())[:16].hex() if apridisk_track0 is not None else "",
            "track01_present": apridisk_img.get_track(0, 1) is not None,
        },
        "imd_cases": {
            "mfm": {
                "file_hex": imd_mfm_bytes.hex(),
                "track_present": imd_mfm_track is not None,
                "track_summary": imd_mfm_track.summary_string() if imd_mfm_track is not None else "",
                "track_sector_count": imd_mfm_track.nsec if imd_mfm_track is not None else 0,
                "track_missing": imd_mfm_track.nr_missing() if imd_mfm_track is not None else 0,
                "track_img_sha256": hashlib.sha256(bytes(imd_mfm_track.get_img_track())).hexdigest() if imd_mfm_track is not None else "",
                "track_img_prefix_hex": bytes(imd_mfm_track.get_img_track())[:16].hex() if imd_mfm_track is not None else "",
                "track01_present": imd_mfm_roundtrip.get_track(0, 1) is not None,
            },
            "fm": {
                "file_hex": imd_fm_bytes.hex(),
                "track_present": imd_fm_track is not None,
                "track_summary": imd_fm_track.summary_string() if imd_fm_track is not None else "",
                "track_sector_count": imd_fm_track.nsec if imd_fm_track is not None else 0,
                "track_missing": imd_fm_track.nr_missing() if imd_fm_track is not None else 0,
                "track_img_sha256": hashlib.sha256(bytes(imd_fm_track.get_img_track())).hexdigest() if imd_fm_track is not None else "",
                "track_img_prefix_hex": bytes(imd_fm_track.get_img_track())[:16].hex() if imd_fm_track is not None else "",
                "track01_present": imd_fm_roundtrip.get_track(0, 1) is not None,
            },
        },
        "hfe_cases": {
            "mfm": {
                "file_hex": hfe_mfm_bytes.hex(),
                "track_present": hfe_mfm_track is not None,
                "bit_length": len(hfe_mfm_track.bits) if hfe_mfm_track is not None else 0,
                "time_per_rev": float(hfe_mfm_track.time_per_rev) if hfe_mfm_track is not None else 0.0,
                "bitrate": float(hfe_mfm_track.bitrate) if hfe_mfm_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_mfm_track.bits.tobytes()).hexdigest() if hfe_mfm_track is not None else "",
                "bits_prefix_hex": hfe_mfm_track.bits.tobytes()[:16].hex() if hfe_mfm_track is not None else "",
                "track01_present": hfe_mfm_roundtrip.get_track(0, 1) is not None,
            },
            "mfm_bitrate_300": {
                "file_hex": hfe_mfm_bitrate_bytes.hex(),
                "track_present": hfe_mfm_bitrate_track is not None,
                "bit_length": len(hfe_mfm_bitrate_track.bits) if hfe_mfm_bitrate_track is not None else 0,
                "time_per_rev": float(hfe_mfm_bitrate_track.time_per_rev) if hfe_mfm_bitrate_track is not None else 0.0,
                "bitrate": float(hfe_mfm_bitrate_track.bitrate) if hfe_mfm_bitrate_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_mfm_bitrate_track.bits.tobytes()).hexdigest() if hfe_mfm_bitrate_track is not None else "",
                "bits_prefix_hex": hfe_mfm_bitrate_track.bits.tobytes()[:16].hex() if hfe_mfm_bitrate_track is not None else "",
                "track01_present": hfe_mfm_bitrate_roundtrip.get_track(0, 1) is not None,
            },
            "mfm_double_step": {
                "file_hex": hfe_mfm_double_step_bytes.hex(),
                "track_present": hfe_mfm_double_step_track is not None,
                "bit_length": len(hfe_mfm_double_step_track.bits) if hfe_mfm_double_step_track is not None else 0,
                "time_per_rev": float(hfe_mfm_double_step_track.time_per_rev) if hfe_mfm_double_step_track is not None else 0.0,
                "bitrate": float(hfe_mfm_double_step_track.bitrate) if hfe_mfm_double_step_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_mfm_double_step_track.bits.tobytes()).hexdigest() if hfe_mfm_double_step_track is not None else "",
                "bits_prefix_hex": hfe_mfm_double_step_track.bits.tobytes()[:16].hex() if hfe_mfm_double_step_track is not None else "",
                "track01_present": hfe_mfm_double_step_roundtrip.get_track(0, 1) is not None,
            },
            "mfm_header_opts": {
                "file_hex": hfe_mfm_header_opts_bytes.hex(),
                "track_present": hfe_mfm_header_opts_track is not None,
                "bit_length": len(hfe_mfm_header_opts_track.bits) if hfe_mfm_header_opts_track is not None else 0,
                "time_per_rev": float(hfe_mfm_header_opts_track.time_per_rev) if hfe_mfm_header_opts_track is not None else 0.0,
                "bitrate": float(hfe_mfm_header_opts_track.bitrate) if hfe_mfm_header_opts_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_mfm_header_opts_track.bits.tobytes()).hexdigest() if hfe_mfm_header_opts_track is not None else "",
                "bits_prefix_hex": hfe_mfm_header_opts_track.bits.tobytes()[:16].hex() if hfe_mfm_header_opts_track is not None else "",
                "track01_present": hfe_mfm_header_opts_roundtrip.get_track(0, 1) is not None,
                "expected_encoding_byte": hfe_mfm_header_opts_bytes[11],
                "expected_interface_byte": hfe_mfm_header_opts_bytes[16],
                "expected_double_step_byte": hfe_mfm_header_opts_bytes[19],
            },
            "mfm_header_opts_numeric": {
                "file_hex": hfe_mfm_header_opts_numeric_bytes.hex(),
                "track_present": hfe_mfm_header_opts_numeric_track is not None,
                "bit_length": len(hfe_mfm_header_opts_numeric_track.bits) if hfe_mfm_header_opts_numeric_track is not None else 0,
                "time_per_rev": float(hfe_mfm_header_opts_numeric_track.time_per_rev) if hfe_mfm_header_opts_numeric_track is not None else 0.0,
                "bitrate": float(hfe_mfm_header_opts_numeric_track.bitrate) if hfe_mfm_header_opts_numeric_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_mfm_header_opts_numeric_track.bits.tobytes()).hexdigest() if hfe_mfm_header_opts_numeric_track is not None else "",
                "bits_prefix_hex": hfe_mfm_header_opts_numeric_track.bits.tobytes()[:16].hex() if hfe_mfm_header_opts_numeric_track is not None else "",
                "track01_present": hfe_mfm_header_opts_numeric_roundtrip.get_track(0, 1) is not None,
                "expected_encoding_byte": hfe_mfm_header_opts_numeric_bytes[11],
                "expected_interface_byte": hfe_mfm_header_opts_numeric_bytes[16],
                "expected_double_step_byte": hfe_mfm_header_opts_numeric_bytes[19],
            },
            "flux_bitrate_250": {
                "file_hex": hfe_flux_with_bitrate_bytes.hex(),
                "track_present": hfe_flux_with_bitrate_track is not None,
                "bit_length": len(hfe_flux_with_bitrate_track.bits) if hfe_flux_with_bitrate_track is not None else 0,
                "time_per_rev": float(hfe_flux_with_bitrate_track.time_per_rev) if hfe_flux_with_bitrate_track is not None else 0.0,
                "bitrate": float(hfe_flux_with_bitrate_track.bitrate) if hfe_flux_with_bitrate_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_flux_with_bitrate_track.bits.tobytes()).hexdigest() if hfe_flux_with_bitrate_track is not None else "",
                "bits_prefix_hex": hfe_flux_with_bitrate_track.bits.tobytes()[:16].hex() if hfe_flux_with_bitrate_track is not None else "",
                "track01_present": hfe_flux_with_bitrate_roundtrip.get_track(0, 1) is not None,
            },
            "fm": {
                "file_hex": hfe_fm_bytes.hex(),
                "track_present": hfe_fm_track is not None,
                "bit_length": len(hfe_fm_track.bits) if hfe_fm_track is not None else 0,
                "time_per_rev": float(hfe_fm_track.time_per_rev) if hfe_fm_track is not None else 0.0,
                "bitrate": float(hfe_fm_track.bitrate) if hfe_fm_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_fm_track.bits.tobytes()).hexdigest() if hfe_fm_track is not None else "",
                "bits_prefix_hex": hfe_fm_track.bits.tobytes()[:16].hex() if hfe_fm_track is not None else "",
                "track01_present": hfe_fm_roundtrip.get_track(0, 1) is not None,
            },
        },
        "hfe_v3_cases": {
            "mfm": {
                "file_hex": hfe_v3_mfm_bytes.hex(),
                "track_present": hfe_v3_mfm_track is not None,
                "bit_length": len(hfe_v3_mfm_track.bits) if hfe_v3_mfm_track is not None else 0,
                "time_per_rev": float(hfe_v3_mfm_track.time_per_rev) if hfe_v3_mfm_track is not None else 0.0,
                "bitrate": float(hfe_v3_mfm_track.bitrate) if hfe_v3_mfm_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_v3_mfm_track.bits.tobytes()).hexdigest() if hfe_v3_mfm_track is not None else "",
                "bits_prefix_hex": hfe_v3_mfm_track.bits.tobytes()[:16].hex() if hfe_v3_mfm_track is not None else "",
                "track01_present": hfe_v3_mfm_roundtrip.get_track(0, 1) is not None,
            },
            "mfm_uniform": {
                "file_hex": hfe_v3_uniform_mfm_bytes.hex(),
                "track_present": hfe_v3_uniform_mfm_track is not None,
                "bit_length": len(hfe_v3_uniform_mfm_track.bits) if hfe_v3_uniform_mfm_track is not None else 0,
                "time_per_rev": float(hfe_v3_uniform_mfm_track.time_per_rev) if hfe_v3_uniform_mfm_track is not None else 0.0,
                "bitrate": float(hfe_v3_uniform_mfm_track.bitrate) if hfe_v3_uniform_mfm_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_v3_uniform_mfm_track.bits.tobytes()).hexdigest() if hfe_v3_uniform_mfm_track is not None else "",
                "bits_prefix_hex": hfe_v3_uniform_mfm_track.bits.tobytes()[:16].hex() if hfe_v3_uniform_mfm_track is not None else "",
                "track01_present": hfe_v3_uniform_mfm_roundtrip.get_track(0, 1) is not None,
            },
            "fm": {
                "file_hex": hfe_v3_fm_bytes.hex(),
                "track_present": hfe_v3_fm_track is not None,
                "bit_length": len(hfe_v3_fm_track.bits) if hfe_v3_fm_track is not None else 0,
                "time_per_rev": float(hfe_v3_fm_track.time_per_rev) if hfe_v3_fm_track is not None else 0.0,
                "bitrate": float(hfe_v3_fm_track.bitrate) if hfe_v3_fm_track is not None else 0.0,
                "bits_sha256": hashlib.sha256(hfe_v3_fm_track.bits.tobytes()).hexdigest() if hfe_v3_fm_track is not None else "",
                "bits_prefix_hex": hfe_v3_fm_track.bits.tobytes()[:16].hex() if hfe_v3_fm_track is not None else "",
                "track01_present": hfe_v3_fm_roundtrip.get_track(0, 1) is not None,
            },
        },
    }


def build_scp_fixtures():
    decode_cases = []
    emit_cases = []
    layout_cases = []
    invalid_disktype_message = ""

    try:
        probe = SCP("invalid_disktype.scp", None)
        probe.opts.disktype = "bad_type"
    except Exception as ex:
        invalid_disktype_message = str(ex).splitlines()[0] if str(ex) else ""

    decode_src = SCP("decode_fixture.scp", None)
    decode_flux = Flux([100000.0], [2000.0, 2500.0, 3000.0, 3500.0, 4000.0], SCP.sample_freq, index_cued=True)
    decode_src.emit_track(3, 0, decode_flux)
    decode_bytes = decode_src.get_image()
    decode_roundtrip = SCP("decode_roundtrip.scp", None)
    decode_roundtrip.from_bytes(decode_bytes)
    decode_track = decode_roundtrip.get_track(3, 0)
    decode_cases.append(
        {
            "name": "single_rev_track",
            "cyl": 3,
            "head": 0,
            "image_bytes": list(decode_bytes),
            "expected_index_list": [float(x) for x in decode_track.index_list],
            "expected_flux_count": len(decode_track.list),
            "expected_flux_prefix": [float(x) for x in decode_track.list[:16]],
            "expected_splice": None,
        }
    )

    decode_src2 = SCP("decode_fixture_long.scp", None)
    decode_flux2 = Flux([120000.0], [70000.0, 80000.0, 90000.0, 100000.0], SCP.sample_freq, index_cued=True)
    decode_src2.emit_track(4, 1, decode_flux2)
    decode_bytes2 = decode_src2.get_image()
    decode_roundtrip2 = SCP("decode_roundtrip_long.scp", None)
    decode_roundtrip2.from_bytes(decode_bytes2)
    decode_track2 = decode_roundtrip2.get_track(4, 1)
    decode_cases.append(
        {
            "name": "long_pulse_track",
            "cyl": 4,
            "head": 1,
            "image_bytes": list(decode_bytes2),
            "expected_index_list": [float(x) for x in decode_track2.index_list],
            "expected_flux_count": len(decode_track2.list),
            "expected_flux_prefix": [float(x) for x in decode_track2.list[:16]],
            "expected_splice": None,
        }
    )

    decode_src3 = SCP("decode_fixture_wrsp.scp", None)
    decode_flux3 = Flux([95000.0], [1800.0, 2200.0, 2600.0, 3000.0], SCP.sample_freq, index_cued=True)
    decode_flux3.splice = 12345.0
    decode_src3.emit_track(5, 0, decode_flux3)
    decode_bytes3 = decode_src3.get_image()
    decode_roundtrip3 = SCP("decode_roundtrip_wrsp.scp", None)
    decode_roundtrip3.from_bytes(decode_bytes3)
    decode_track3 = decode_roundtrip3.get_track(5, 0)
    decode_cases.append(
        {
            "name": "wrsp_splice_track",
            "cyl": 5,
            "head": 0,
            "image_bytes": list(decode_bytes3),
            "expected_index_list": [float(x) for x in decode_track3.index_list],
            "expected_flux_count": len(decode_track3.list),
            "expected_flux_prefix": [float(x) for x in decode_track3.list[:16]],
            "expected_splice": float(decode_track3.splice) if decode_track3.splice is not None else None,
        }
    )

    emit_src = SCP("emit_fixture.scp", None)
    emit_flux = Flux([120000.0], [1000.0, 1100.0, 1300.0, 1700.0, 1900.0, 2100.0], SCP.sample_freq, index_cued=True)
    emit_src.emit_track(6, 1, emit_flux)
    emit_bytes = emit_src.get_image()
    emit_roundtrip = SCP("emit_roundtrip.scp", None)
    emit_roundtrip.from_bytes(emit_bytes)
    emit_track = emit_roundtrip.get_track(6, 1)
    emit_cases.append(
        {
            "name": "emit_single_track",
            "cyl": 6,
            "head": 1,
            "input_index_list": [120000.0],
            "input_flux_list": [1000.0, 1100.0, 1300.0, 1700.0, 1900.0, 2100.0],
            "input_splice": None,
            "expected_index_list": [float(x) for x in emit_track.index_list],
            "expected_flux_count": len(emit_track.list),
            "expected_flux_prefix": [float(x) for x in emit_track.list[:16]],
            "expected_splice": None,
        }
    )

    emit_src2 = SCP("emit_fixture_multi.scp", None)
    emit_flux2 = Flux(
        [90000.0, 91000.0],
        [1200.0, 1300.0, 2000.0, 87000.0, 1400.0, 1500.0, 1600.0],
        SCP.sample_freq,
        index_cued=True,
    )
    emit_src2.emit_track(9, 0, emit_flux2)
    emit_bytes2 = emit_src2.get_image()
    emit_roundtrip2 = SCP("emit_roundtrip_multi.scp", None)
    emit_roundtrip2.from_bytes(emit_bytes2)
    emit_track2 = emit_roundtrip2.get_track(9, 0)
    emit_cases.append(
        {
            "name": "emit_multi_rev_track",
            "cyl": 9,
            "head": 0,
            "input_index_list": [90000.0, 91000.0],
            "input_flux_list": [1200.0, 1300.0, 2000.0, 87000.0, 1400.0, 1500.0, 1600.0],
            "input_splice": None,
            "expected_index_list": [float(x) for x in emit_track2.index_list],
            "expected_flux_count": len(emit_track2.list),
            "expected_flux_prefix": [float(x) for x in emit_track2.list[:16]],
            "expected_splice": None,
        }
    )

    emit_src3 = SCP("emit_fixture_wrsp.scp", None)
    emit_flux3 = Flux([88000.0], [900.0, 1000.0, 1300.0, 1700.0], SCP.sample_freq, index_cued=True)
    emit_flux3.splice = 2222.0
    emit_src3.emit_track(10, 1, emit_flux3)
    emit_bytes3 = emit_src3.get_image()
    emit_roundtrip3 = SCP("emit_roundtrip_wrsp.scp", None)
    emit_roundtrip3.from_bytes(emit_bytes3)
    emit_track3 = emit_roundtrip3.get_track(10, 1)
    emit_cases.append(
        {
            "name": "emit_splice_track",
            "cyl": 10,
            "head": 1,
            "input_index_list": [88000.0],
            "input_flux_list": [900.0, 1000.0, 1300.0, 1700.0],
            "input_splice": 2222.0,
            "expected_index_list": [float(x) for x in emit_track3.index_list],
            "expected_flux_count": len(emit_track3.list),
            "expected_flux_prefix": [float(x) for x in emit_track3.list[:16]],
            "expected_splice": float(emit_track3.splice) if emit_track3.splice is not None else None,
        }
    )

    def build_layout_case(name, tracks, opts=None):
        img = SCP(f"{name}.scp", None)
        if opts:
            for key, value in opts.items():
                setattr(img.opts, key, value)
        for t in tracks:
            flux = Flux(t["index_list"], t["flux_list"], SCP.sample_freq, index_cued=True)
            if "splice" in t and t["splice"] is not None:
                flux.splice = t["splice"]
            img.emit_track(t["cyl"], t["head"], flux)
        old_time = time.time
        try:
            time.time = lambda: 0
            payload = img.get_image()
        finally:
            time.time = old_time
        trk_offs = [int.from_bytes(payload[16 + i * 4 : 20 + i * 4], "little") for i in range(168)]
        nonzero = [i for i, off in enumerate(trk_offs) if off != 0]
        output_revs = payload[5]
        tdh_tracks = []
        data_prefix_tracks = []
        data_sha_tracks = []
        for trk in nonzero:
            off = trk_offs[trk]
            entries = []
            tdh_start = off + 4
            for rev in range(output_revs):
                eoff = tdh_start + rev * 12
                ticks = int.from_bytes(payload[eoff : eoff + 4], "little")
                words = int.from_bytes(payload[eoff + 4 : eoff + 8], "little")
                rel = int.from_bytes(payload[eoff + 8 : eoff + 12], "little")
                entries.append({"ticks": ticks, "words": words, "offset": rel})
            tdh_tracks.append({"track": trk, "entries": entries})
            first_rel = entries[0]["offset"]
            first_words = entries[0]["words"]
            dat_abs = off + first_rel
            dat_len = first_words * 2
            prefix_len = min(32, dat_len)
            data_prefix_tracks.append(
                {
                    "track": trk,
                    "data_prefix": list(payload[dat_abs : dat_abs + prefix_len]),
                }
            )
            rel_start = min(e["offset"] for e in entries)
            rel_end = max(e["offset"] + e["words"] * 2 for e in entries)
            full_dat = payload[off + rel_start : off + rel_end]
            data_sha_tracks.append(
                {
                    "track": trk,
                    "data_len": len(full_dat),
                    "data_sha256": hashlib.sha256(full_dat).hexdigest(),
                }
            )
        zero_samples = [i for i in [0, 1, 2, 5, 80, 81, 82] if i not in nonzero]
        checksum = int.from_bytes(payload[12:16], "little")
        app_name_marker = b"Greaseweazle "
        app_name_pos = payload.rfind(app_name_marker)
        if app_name_pos < 2:
            raise RuntimeError("Could not locate SCP footer app name")
        footer_offset = app_name_pos - 2
        app_name_len = int.from_bytes(payload[footer_offset : footer_offset + 2], "little")
        app_name = payload[footer_offset + 2 : footer_offset + 2 + app_name_len].decode("ascii")
        footer_struct_offset = footer_offset + 2 + app_name_len + 1
        app_name_offset = int.from_bytes(payload[footer_struct_offset + 16 : footer_struct_offset + 20], "little")
        exts_offset = 0x2B0 if len(payload) >= 0x2B8 and payload[0x2B0:0x2B4] == b"EXTS" else 0
        wrsp_offset = 0x2B8 if exts_offset != 0 else 0
        first_track_offset = min((off for off in trk_offs if off != 0), default=0)
        wrsp_present = False
        ext_len = 0
        wrsp_chunk_len = 0
        wrsp_flags = 0
        wrsp_sig = None
        wrsp_nonzero_entries = []
        wrsp_zero_samples = []
        wrsp_table = []
        if len(payload) >= 0x2C4 and payload[0x2B0:0x2B4] == b"EXTS":
            wrsp_present = True
            ext_len = int.from_bytes(payload[0x2B4:0x2B8], "little")
            wrsp_sig = payload[0x2B8:0x2BC].decode("ascii")
            wrsp_chunk_len = int.from_bytes(payload[0x2BC:0x2C0], "little")
            wrsp_flags = int.from_bytes(payload[0x2C0:0x2C4], "little")
            wrsp_table = [int.from_bytes(payload[0x2C4 + i * 4 : 0x2C8 + i * 4], "little") for i in range(168)]
            wrsp_nonzero_entries = [{"track": i, "value": v} for i, v in enumerate(wrsp_table) if v != 0]
            wrsp_zero_samples = [i for i in [0, 1, 2, 5, 80, 81, 82] if wrsp_table[i] == 0]
        return {
            "name": name,
            "input_tracks": tracks,
            "input_disktype": opts.get("disktype") if opts else None,
            "input_legacy_ss": bool(opts and "legacy_ss" in opts),
            "expected_disk_type": payload[4],
            "expected_single_sided": payload[10],
            "expected_end_track": payload[7],
            "expected_flags": payload[8],
            "expected_checksum": checksum,
            "expected_footer_magic": [payload[-4], payload[-3], payload[-2], payload[-1]],
            "expected_footer_offset": footer_offset,
            "expected_app_name_offset": app_name_offset,
            "expected_app_name": app_name,
            "expected_wrsp_present": wrsp_present,
            "expected_ext_len": ext_len,
            "expected_wrsp_sig": wrsp_sig,
            "expected_wrsp_chunk_len": wrsp_chunk_len,
            "expected_wrsp_flags": wrsp_flags,
            "expected_wrsp_table": wrsp_table,
            "expected_wrsp_nonzero_entries": wrsp_nonzero_entries,
            "expected_wrsp_zero_samples": wrsp_zero_samples,
            "expected_exts_offset": exts_offset,
            "expected_wrsp_offset": wrsp_offset,
            "expected_first_track_offset": first_track_offset,
            "expected_output_revs": output_revs,
            "expected_tdh_tracks": tdh_tracks,
            "expected_data_prefix_tracks": data_prefix_tracks,
            "expected_data_sha_tracks": data_sha_tracks,
            "expected_nonzero_tracks": nonzero,
            "expected_zero_samples": zero_samples,
        }

    layout_cases.append(
        build_layout_case(
            "layout_side1_sparse",
            [
                {
                    "cyl": 2,
                    "head": 1,
                    "index_list": [100000.0],
                    "flux_list": [2000.0, 3000.0, 4000.0, 5000.0],
                }
            ],
        )
    )
    layout_cases.append(
        build_layout_case(
            "layout_mixed_noncontiguous",
            [
                {
                    "cyl": 0,
                    "head": 0,
                    "index_list": [90000.0],
                    "flux_list": [1200.0, 1400.0, 1600.0, 1800.0],
                },
                {
                    "cyl": 40,
                    "head": 1,
                    "index_list": [110000.0],
                    "flux_list": [2100.0, 2200.0, 2300.0, 2400.0],
                },
            ],
        )
    )
    layout_cases.append(
        build_layout_case(
            "layout_wrsp_splice",
            [
                {
                    "cyl": 3,
                    "head": 0,
                    "index_list": [100000.0],
                    "flux_list": [1500.0, 1600.0, 1700.0, 1800.0],
                    "splice": 3333.0,
                }
            ],
        )
    )
    layout_cases.append(
        build_layout_case(
            "layout_wrsp_multi_splice",
            [
                {
                    "cyl": 6,
                    "head": 0,
                    "index_list": [98000.0],
                    "flux_list": [1600.0, 1700.0, 1800.0, 1900.0],
                    "splice": 1111.0,
                },
                {
                    "cyl": 7,
                    "head": 1,
                    "index_list": [99000.0],
                    "flux_list": [2100.0, 2200.0, 2300.0, 2400.0],
                    "splice": 4444.0,
                },
            ],
        )
    )
    layout_cases.append(
        build_layout_case(
            "layout_legacy_single_sided_disktype",
            [
                {
                    "cyl": 4,
                    "head": 0,
                    "index_list": [100000.0],
                    "flux_list": [1800.0, 1900.0, 2100.0, 2300.0],
                }
            ],
            opts={"legacy_ss": "yes", "disktype": "ibmpc-1m44"},
        )
    )
    layout_cases.append(
        build_layout_case(
            "layout_disktype_hex",
            [
                {
                    "cyl": 1,
                    "head": 0,
                    "index_list": [96000.0],
                    "flux_list": [1400.0, 1500.0, 1700.0, 1800.0],
                }
            ],
            opts={"disktype": "0x33"},
        )
    )

    return {
        "invalid_disktype_message": invalid_disktype_message,
        "decode_cases": decode_cases,
        "emit_cases": emit_cases,
        "layout_cases": layout_cases,
    }


def build_cli_fixtures():
    cli_path = os.path.join(SRC_ROOT, "greaseweazle", "cli.py")
    with open(cli_path, "r", encoding="utf-8") as f:
        text = f.read()
    marker = "actions = ["
    start = text.index(marker) + len(marker)
    end = text.index("]", start)
    body = text[start:end]
    actions = []
    for line in body.splitlines():
        line = line.strip()
        if not line:
            continue
        line = line.rstrip(",")
        if line.startswith("'") and line.endswith("'"):
            actions.append(line[1:-1])
    import io
    import greaseweazle.cli as py_cli

    def capture_lines(callable_obj):
        buf = io.StringIO()
        out, err = sys.stdout, sys.stderr
        sys.stdout = sys.stderr = buf
        try:
            try:
                callable_obj()
            except SystemExit:
                pass
        finally:
            sys.stdout, sys.stderr = out, err
        lines = [ln for ln in buf.getvalue().splitlines() if not ln.startswith("*** WARNING: Optimised data routines not found")]
        return lines

    top_usage = capture_lines(lambda: py_cli.usage(["gw"]))
    if len(top_usage) >= 4:
        top_usage = top_usage[:4]

    section_headers = [
        "options:",
        "positional arguments:",
        "DRIVE: Drive (and bus) identifier:",
        "SPEED: Track rotation time specified as:",
        "TSPEC: Colon-separated list of:",
        "PLLSPEC: Colon-separated list of:",
        "FORMAT options:",
        "Supported file suffixes:",
        "Examples:",
        "Note: TRACKS can specify one track (e.g., c=40:h=0) or multiple heads on same cylinder (e.g., c=40:h=0,1) to alternate between heads",
    ]
    help_cases = []
    for action in actions:
        mod = importlib.import_module("greaseweazle.tools." + action)
        lines = capture_lines(lambda mod=mod, action=action: mod.main(["gw", action, "--help"]))
        usage_line = next((ln for ln in lines if ln.lower().startswith("usage: ")), "")
        description_line = ""
        if usage_line:
            try:
                usage_idx = lines.index(usage_line)
                for ln in lines[usage_idx + 1 :]:
                    stripped = ln.strip()
                    if not stripped:
                        continue
                    if stripped in section_headers:
                        break
                    if stripped.lower().startswith("usage: "):
                        continue
                    description_line = stripped
                    break
            except ValueError:
                pass
        sections = [s for s in section_headers if s in lines]
        option_tokens = []
        seen = set()
        for ln in lines:
            stripped = ln.lstrip()
            if not stripped.startswith("-"):
                continue
            option_part = stripped
            if "  " in option_part:
                option_part = option_part.split("  ", 1)[0]
            for token in re.findall(r"(?<!\w)(?:--[a-z0-9-]+|-n)(?!\w)", option_part):
                if token not in seen:
                    seen.add(token)
                    option_tokens.append(token)
        positional_lines = []
        if "positional arguments:" in lines:
            start = lines.index("positional arguments:") + 1
            for ln in lines[start:]:
                if not ln.strip():
                    continue
                if ln in section_headers or ln.lower().startswith("options:"):
                    break
                positional_lines.append(ln)

        example_lines = []
        if "Examples:" in lines:
            start = lines.index("Examples:") + 1
            for ln in lines[start:]:
                if not ln.strip():
                    continue
                if ln in section_headers:
                    break
                example_lines.append(ln)

        help_cases.append(
            {
                "action": action,
                "usage_line": usage_line,
                "description_line": description_line,
                "sections": sections,
                "option_tokens": option_tokens,
                "positional_lines": positional_lines,
                "example_lines": example_lines,
            }
        )

    return {"actions": actions, "top_usage": top_usage, "help_cases": help_cases}


def build_trackset_fixtures():
    specs = [
        "c=0-3:h=0-1",
        "c=0-4/2,7:h=1:step=2:h0.off=+1:hswap",
        "c=1-3:h=0:h1.off=-1:step=1/2",
    ]
    cases = []
    for spec in specs:
        ts = util.TrackSet(spec)
        iter_rows = []
        for row in ts:
            iter_rows.append(
                {
                    "physical_cyl": row.physical_cyl,
                    "physical_head": row.physical_head,
                    "cyl": row.cyl,
                    "head": row.head,
                }
            )
        contains = [{"key": [0, 0], "value": ((0, 0) in ts)}, {"key": [3, 1], "value": ((3, 1) in ts)}]
        cases.append(
            {
                "spec": spec,
                "to_string": str(ts),
                "cyls": ts.cyls,
                "heads": ts.heads,
                "h_off": ts.h_off,
                "step": ts.step,
                "hswap": ts.hswap,
                "iter": iter_rows,
                "contains": contains,
            }
        )
    return {"cases": cases}


def build_action_description_fixtures():
    tools_dir = Path(SRC_ROOT) / "greaseweazle" / "tools"
    actions = {}
    for path in tools_dir.glob("*.py"):
        name = path.stem
        if name.startswith("_") or name in ("util", "list_ports_windows"):
            continue
        text = path.read_text(encoding="utf-8")
        tree = ast.parse(text)
        for node in tree.body:
            if isinstance(node, ast.Assign):
                for target in node.targets:
                    if isinstance(target, ast.Name) and target.id == "description":
                        expr = ast.Expression(node.value)
                        compiled = compile(expr, filename=str(path), mode="eval")
                        actions[name] = eval(compiled, {"__builtins__": {}}, {})
    ordered = ["info", "read", "write", "convert", "erase", "clean", "seek", "delays", "update", "pin", "reset", "bandwidth", "rpm", "align"]
    return {"descriptions": {k: actions[k] for k in ordered if k in actions}}


def build_precomp_fixtures():
    specs = ["type=mfm:0=50:40=125", "type=gcr:10=80", "0=125:60=250"]
    out = []
    for spec in specs:
        p = PrecompSpec(spec)
        track_samples = [0, 10, 40, 61]
        resolved = []
        for cyl in track_samples:
            t = p.track_precomp(cyl)
            resolved.append(None if t is None else {"type": t.type, "ns": t.ns})
        out.append(
            {
                "spec": spec,
                "repr": str(p),
                "list": [{"cyl": c, "ns": ns} for (c, ns) in p.list],
                "type": p.type,
                "resolved": resolved,
            }
        )
    return {"cases": out}


def build_info_fixtures():
    import re

    tags = ["v1.2", "v12.34", "v0.31-beta", "v7.7"]
    cases = []
    for tag in tags:
        m = re.match(r"v(\d+)\.(\d+)", tag)
        cases.append(
            {
                "tag": tag,
                "major": int(m.group(1)) if m else None,
                "minor": int(m.group(2)) if m else None,
                "matched": m is not None,
            }
        )
    line_cases = []
    for name, value, tab in [("Host Tools", "1.0", 0), ("Port", "COM5", 2), ("Firmware", "1.2", 4)]:
        output = "".ljust(tab) + (name + ":").ljust(12 - tab) + value
        line_cases.append({"name": name, "value": value, "tab": tab, "output": output})

    runtime_cases = [
        {"args": [], "ok": True, "lines": []},
        {"args": ["--bootloader"], "ok": True, "lines": []},
        {"args": ["--device", "COM3"], "ok": True, "lines": []},
        {"args": ["--weird"], "ok": False, "error_prefix": "unrecognized option: --weird"},
        {"args": ["extra"], "ok": False, "error_prefix": "info does not take positional arguments"},
    ]

    return {"tag_cases": cases, "line_cases": line_cases, "runtime_cases": runtime_cases}


def build_update_fixtures():
    # Mirrors update.py mutual exclusion check:
    # error.check(args.tag is None or args.file is None, "File and tag both specified. Only one is allowed.")
    from greaseweazle.tools import update as update_tool

    class _Args:
        def __init__(self, file_name, bootloader):
            self.file = file_name
            self.bootloader = bootloader

    class _Usb:
        def __init__(self, hw_model):
            self.hw_model = hw_model

    def crc16_ccitt_append(data):
        crc16 = crcmod.predefined.Crc("crc-ccitt-false")
        crc16.update(data)
        return data + struct.pack(">H", crc16.crcValue)

    def make_update_entry(update_type, major, minor, footer_hw_model, body_seed):
        # Keep body length multiple-of-four so full update entry is 4-byte aligned.
        body = bytes(((body_seed + i * 7) & 0xFF) for i in range(8))
        payload = body + update_type + bytes([major & 0xFF, minor & 0xFF]) + struct.pack("<H", footer_hw_model)
        payload = crc16_ccitt_append(payload)
        return payload

    def make_upd(entries):
        # entries: list of (catalog_hw_model, payload_bytes)
        catalog = b""
        for catalog_hw, payload in entries:
            catalog += struct.pack("<2H", len(payload), catalog_hw)
            catalog += payload
        out = b"GWUP" + catalog
        crc32 = crcmod.predefined.Crc("crc-32-mpeg")
        crc32.update(out)
        out += struct.pack(">I", crc32.crcValue)
        return out

    def make_extract_case(name, upd_bytes, hw_model, bootloader):
        args = _Args("fixture.upd", bootloader)
        usb = _Usb(hw_model)
        try:
            version, payload = update_tool.extract_update(usb, upd_bytes, args)
            return {
                "name": name,
                "upd_hex": upd_bytes.hex(),
                "hw_model": hw_model,
                "bootloader": bootloader,
                "ok": True,
                "version_major": version[0],
                "version_minor": version[1],
                "payload_len": len(payload),
                "payload_sha256": hashlib.sha256(payload).hexdigest(),
            }
        except Exception as ex:
            return {
                "name": name,
                "upd_hex": upd_bytes.hex(),
                "hw_model": hw_model,
                "bootloader": bootloader,
                "ok": False,
                "error_prefix": str(ex),
            }

    valid_main_entry = make_update_entry(b"GW", 3, 9, 7, body_seed=0x10)
    valid_boot_entry = make_update_entry(b"BL", 4, 2, 7, body_seed=0x40)
    decoy_entry = make_update_entry(b"GW", 1, 1, 6, body_seed=0x70)

    valid_catalog = make_upd(
        [
            (6, decoy_entry),
            (7, valid_main_entry),
            (7, valid_boot_entry),
        ]
    )

    bad_outer_crc = bytearray(valid_catalog)
    bad_outer_crc[-1] ^= 0x01

    bad_footer_entry = make_update_entry(b"GW", 3, 9, 8, body_seed=0x10)
    bad_footer_catalog = make_upd([(7, bad_footer_entry)])

    bad_entry_crc = bytearray(make_update_entry(b"GW", 3, 9, 7, body_seed=0x10))
    bad_entry_crc[-1] ^= 0x01
    bad_entry_crc_catalog = make_upd([(7, bytes(bad_entry_crc))])

    extract_cases = [
        make_extract_case("valid_main", valid_catalog, hw_model=7, bootloader=False),
        make_extract_case("valid_bootloader", valid_catalog, hw_model=7, bootloader=True),
        make_extract_case("not_found", make_upd([(7, valid_main_entry)]), hw_model=7, bootloader=True),
        make_extract_case("bad_header", b"NOTUPD", hw_model=7, bootloader=False),
        make_extract_case("bad_outer_crc", bytes(bad_outer_crc), hw_model=7, bootloader=False),
        make_extract_case("bad_footer", bad_footer_catalog, hw_model=7, bootloader=False),
        make_extract_case("bad_entry_crc", bad_entry_crc_catalog, hw_model=7, bootloader=False),
    ]

    return {
        "mutual_exclusion_cases": [
            {"file": None, "tag": None, "ok": True, "error": None},
            {"file": "firmware.upd", "tag": None, "ok": True, "error": None},
            {"file": None, "tag": "v1.0", "ok": True, "error": None},
            {"file": "firmware.upd", "tag": "v1.0", "ok": False, "error": "File and tag both specified. Only one is allowed."},
        ],
        "runtime_cases": [
            {"args": ["--force", "--bootloader"], "ok": True, "lines": []},
            {"args": ["--file", "firmware.upd"], "ok": True, "lines": []},
            {"args": ["--tag", "v1.0"], "ok": True, "lines": []},
            {"args": ["--file", "firmware.upd", "--tag", "v1.0"], "ok": False, "error_prefix": "File and tag both specified. Only one is allowed."},
            {"args": ["--weird"], "ok": False, "error_prefix": "unrecognized option: --weird"},
            {"args": ["firmware.upd"], "ok": False, "error_prefix": "update does not take positional arguments"},
        ],
        "extract_cases": extract_cases,
    }


def build_pin_fixtures():
    return {
        "usage_lines": [
            "usage: gw pin get|set [-h] ...",
            "  get|set  Get or set a pin",
        ],
        "pin_value_messages": [
            {"pin": 2, "level": False, "message": "Pin 2 is Low (0v)"},
            {"pin": 2, "level": True, "message": "Pin 2 is High (5v)"},
        ],
        "dispatch_cases": [
            {"argv": ["gw", "pin", "get"], "action": "get"},
            {"argv": ["gw", "pin", "set"], "action": "set"},
            {"argv": ["gw", "pin", "other"], "action": "usage"},
            {"argv": ["gw", "pin"], "action": "usage"},
        ],
        "runtime_cases": [
            {"args": [], "ok": True, "lines": ["usage: gw pin get|set [-h] ...", "  get|set  Get or set a pin"]},
            {"args": ["set", "2", "H"], "ok": True, "lines": ["Pin 2 is set High (5v)"]},
            {"args": ["get", "2"], "ok": True, "lines": []},
            {"args": ["set", "2"], "ok": False, "error_prefix": "pin set requires <pin> <level>"},
            {"args": ["get"], "ok": False, "error_prefix": "pin get requires <pin>"},
            {"args": ["set", "2", "X"], "ok": False, "error_prefix": "invalid pin level: 'X'"},
            {"args": ["set", "-1", "H"], "ok": False, "error_prefix": "unrecognized option: -1"},
            {"args": ["nope"], "ok": True, "lines": ["usage: gw pin get|set [-h] ...", "  get|set  Get or set a pin"]},
        ],
    }


def build_reset_fixtures():
    return {
        "delays_flag_cases": [
            {"delays": False, "should_restore_delays": True},
            {"delays": True, "should_restore_delays": False},
        ],
        "runtime_cases": [
            {"args": [], "ok": True, "lines": []},
            {"args": ["--delays"], "ok": True, "lines": []},
            {"args": ["--device", "COM5"], "ok": True, "lines": []},
            {"args": ["--weird"], "ok": False, "error_prefix": "unrecognized option: --weird"},
            {"args": ["input"], "ok": False, "error_prefix": "reset does not take positional arguments"},
        ],
    }


def build_seek_fixtures():
    def prompt_needed(cylinder, force):
        return not (0 <= cylinder <= 83) and (not force)

    cases = []
    for cylinder, force in [(-1, False), (0, False), (83, False), (84, False), (120, True)]:
        cases.append({
            "cylinder": cylinder,
            "force": force,
            "prompt_needed": prompt_needed(cylinder, force),
            "prompt_text": f"Seek to extreme cylinder {cylinder}, Yes/No? ",
        })
    runtime_cases = [
        {"args": ["40"], "ok": True, "lines": []},
        {"args": ["--motor-on", "83"], "ok": True, "lines": []},
        {"args": ["84"], "ok": True, "lines": ["Seek to extreme cylinder 84, Yes/No? "]},
        {"args": ["--force", "84"], "ok": True, "lines": []},
        {"args": [], "ok": False, "error_prefix": "seek requires cylinder argument"},
        {"args": ["1", "2"], "ok": False, "error_prefix": "seek takes exactly one cylinder argument"},
        {"args": ["--weird", "40"], "ok": False, "error_prefix": "unrecognized option: --weird"},
        {"args": ["-1"], "ok": False, "error_prefix": "unrecognized option: -1"},
    ]
    return {"cases": cases, "runtime_cases": runtime_cases}


def build_delays_fixtures():
    def print_info_line(name, value, tab=0):
        return ''.ljust(tab) + (name + ':').ljust(14 - tab) + value

    return {
        "print_info_cases": [
            {"name": "Select Delay", "value": "250us", "tab": 0, "line": print_info_line("Select Delay", "250us")},
            {"name": "Step Delay", "value": "10us", "tab": 2, "line": print_info_line("Step Delay", "10us", 2)},
        ],
        "runtime_cases": [
            {"args": [], "ok": True, "lines": []},
            {"args": ["--step", "10", "--watchdog", "500"], "ok": True, "lines": []},
            {"args": ["--step", "-1"], "ok": False, "error_prefix": "invalid value for --step: -1"},
            {"args": ["--weird"], "ok": False, "error_prefix": "unrecognized option: --weird"},
            {"args": ["extra"], "ok": False, "error_prefix": "delays does not take positional arguments"},
        ],
    }


def build_clean_fixtures():
    def seek_target(cyl, cyls):
        return min(cyl, cyls - 1)

    def clean_pattern(cyls, passes):
        step = max(cyls // 8, 2)
        seq = []
        for _ in range(passes):
            pass_seq = []
            for cyl in range(0, cyls, step):
                pass_seq.append(seek_target(cyl + step - 1, cyls))
                pass_seq.append(seek_target(cyl, cyls))
            seq.append(pass_seq)
        return step, seq

    pattern_cases = []
    for cyls, passes in [(80, 1), (9, 2), (16, 1)]:
        step, seq = clean_pattern(cyls, passes)
        pattern_cases.append(
            {
                "cyls": cyls,
                "passes": passes,
                "step": step,
                "pass_sequences": seq,
            }
        )

    seek_cases = []
    for cyl, cyls in [(10, 80), (85, 80), (0, 1), (3, 3)]:
        seek_cases.append(
            {
                "cylinder": cyl,
                "cyls": cyls,
                "seek_target": seek_target(cyl, cyls),
            }
        )

    runtime_cases = [
        {
            "args": ["--cyls", "8", "--passes", "2"],
            "ok": True,
            "lines": [
                "Pass 0: 1 0 3 2 5 4 7 6 ",
                "Pass 1: 1 0 3 2 5 4 7 6 ",
            ],
        },
        {
            "args": ["--passes", "0"],
            "ok": True,
            "lines": [],
        },
        {
            "args": ["--weird"],
            "ok": False,
            "error_prefix": "unrecognized option: --weird",
        },
        {
            "args": ["--cyls", "-1"],
            "ok": False,
            "error_prefix": "--cyls must be >= 0",
        },
        {
            "args": ["file.img"],
            "ok": False,
            "error_prefix": "clean does not take positional arguments",
        },
    ]

    return {"pattern_cases": pattern_cases, "seek_cases": seek_cases, "runtime_cases": runtime_cases}


def build_convert_fixtures():
    from greaseweazle.tools import convert as convert_tool

    def track_summary(cyl, head, pcyl, phead):
        s = f"T{cyl}.{head}"
        if pcyl != cyl or phead != head:
            s += f" <- Image {pcyl}.{phead}"
        return s

    def run_convert(argv):
        try:
            convert_tool.main(["gw", "convert"] + argv)
        except SystemExit as ex:
            code = ex.code if isinstance(ex.code, int) else 0
            if code != 0:
                raise

    def try_run_convert(argv):
        try:
            run_convert(argv)
            return True
        except Exception:
            return False

    def scp_track_stats(path):
        img = SCP("stats.scp", None)
        with open(path, "rb") as f:
            img.from_bytes(f.read())
        trk = img.get_track(0, 0)
        if trk is None:
            return {
                "index_count": 0,
                "flux_count": 0,
                "flux_total": 0.0,
                "flux_prefix": [],
            }
        return {
            "index_count": len(trk.index_list),
            "flux_count": len(trk.list),
            "flux_total": float(sum(trk.list)),
            "flux_prefix": [float(x) for x in trk.list[:8]],
        }

    flow_cases = []
    for explicit, in_default, out_default in [
        (None, "ibm.1440", "amiga.amigados"),
        (None, None, "amiga.amigados"),
        ("c64.gcr", "ibm.1440", "amiga.amigados"),
    ]:
        fmt = explicit
        if not fmt:
            fmt = in_default
        if not fmt:
            fmt = out_default
        flow_cases.append(
            {
                "explicit_format": explicit,
                "input_default": in_default,
                "output_default": out_default,
                "resolved_format": fmt,
            }
        )

    track_cases = []
    for format_tracks, tracks, out_tracks in [
        ("c=0-79:h=0-1", None, None),
        ("c=0-79:h=0-1", "c=0-39:h=0", None),
        ("c=0-79:h=0-1", "c=0-39:h=0", "c=40-79:h=1"),
        (None, "c=10-12:h=1", "c=8-9:h=0"),
    ]:
        if format_tracks is None:
            def_tracks = util.TrackSet("c=0-81:h=0-1")
        else:
            def_tracks = util.TrackSet(format_tracks)
        out_def_tracks = util.TrackSet(str(def_tracks))
        if tracks is not None:
            def_tracks.update_from_trackspec(tracks)
            out_def_tracks.cyls = def_tracks.cyls.copy()
            out_def_tracks.heads = def_tracks.heads.copy()
        if out_tracks is not None:
            out_def_tracks.update_from_trackspec(out_tracks)
        track_cases.append(
            {
                "format_tracks": format_tracks,
                "tracks": tracks,
                "out_tracks": out_tracks,
                "resolved_tracks": str(def_tracks),
                "resolved_out_tracks": str(out_def_tracks),
            }
        )

    loop_cases = []
    for out_tracks, in_tracks, available, cache_enabled in [
        (
            [
                {"cyl": 0, "head": 0, "physical_cyl": 0, "physical_head": 0},
                {"cyl": 1, "head": 0, "physical_cyl": 1, "physical_head": 0},
            ],
            [(0, 0), (1, 0)],
            [(0, 0), (1, 0)],
            False,
        ),
        (
            [
                {"cyl": 2, "head": 1, "physical_cyl": 2, "physical_head": 1},
                {"cyl": 2, "head": 1, "physical_cyl": 5, "physical_head": 0},
            ],
            [(2, 1)],
            [(2, 1)],
            True,
        ),
        (
            [
                {"cyl": 3, "head": 0, "physical_cyl": 3, "physical_head": 0},
                {"cyl": 4, "head": 0, "physical_cyl": 4, "physical_head": 0},
            ],
            [(3, 0)],
            [(3, 0)],
            True,
        ),
        (
            [
                {"cyl": 6, "head": 1, "physical_cyl": 6, "physical_head": 1},
            ],
            [(6, 1)],
            [],
            True,
        ),
    ]:
        summary = {}
        process_calls = []
        emit = []
        cache_keys = []
        in_set = set(in_tracks)
        available_set = set(available)
        for t in out_tracks:
            key = (t["cyl"], t["head"])
            if key in summary:
                dat = summary[key]
            elif key in in_set:
                process_calls.append(f"{key[0]}.{key[1]}")
                if key not in available_set:
                    continue
                dat = "ok"
                if cache_enabled:
                    summary[key] = dat
                    cache_keys.append(f"{key[0]}.{key[1]}")
            else:
                continue
            emit.append(f'{t["physical_cyl"]}.{t["physical_head"]}<={t["cyl"]}.{t["head"]}')
        loop_cases.append(
            {
                "out_tracks": out_tracks,
                "in_tracks": [{"cyl": c, "head": h} for c, h in in_tracks],
                "available_tracks": [{"cyl": c, "head": h} for c, h in available],
                "cache_enabled": cache_enabled,
                "process_calls": process_calls,
                "emit_targets": emit,
                "cache_keys": cache_keys,
            }
        )

    runtime_cases = [
        {
            "args": ["--format", "ibm.mfm", "--tracks", "c=0-3:h=0", "in.scp", "out.img"],
            "ok": True,
            "lines": ["Format ibm.mfm", "Converting c=0-3:h=0 -> c=0-3:h=0"],
        },
        {
            "args": ["--tracks", "c=40:h=0,1", "--out-tracks", "c=40:h=1", "in.scp", "out.scp"],
            "ok": True,
            "lines": ["Converting c=40:h=0-1 -> c=40:h=1"],
        },
        {
            "args": ["--format", "bad.format", "in.scp", "out.img"],
            "ok": False,
            "error_prefix": "Unknown format 'bad.format'",
        },
        {
            "args": ["--weird", "in.scp", "out.img"],
            "ok": False,
            "error_prefix": "unrecognized option: --weird",
        },
        {
            "args": ["--format", "ibm.mfm", "in.scp"],
            "ok": False,
            "error_prefix": "convert requires input and output files",
        },
    ]

    scp_track_cases = {}
    with tempfile.TemporaryDirectory() as tmp:
        in_scp = os.path.join(tmp, "in.scp")
        in_dmk = os.path.join(tmp, "in.dmk")
        in_td0 = os.path.join(tmp, "in.td0")
        in_fdi = os.path.join(tmp, "in.fdi")
        in_nfd = os.path.join(tmp, "in.nfd")
        in_dcp = os.path.join(tmp, "in.dcp")
        in_a2r = os.path.join(tmp, "in.a2r")
        in_msa = os.path.join(tmp, "in.msa")
        in_apridisk = os.path.join(tmp, "in-apridisk.dsk")
        in_nsi = os.path.join(tmp, "in.nsi")
        out_raw = os.path.join(tmp, "out00.0.raw")
        out_scp = os.path.join(tmp, "roundtrip.scp")
        out_scp_from_dmk = os.path.join(tmp, "roundtrip-from-dmk.scp")
        out_scp_from_td0 = os.path.join(tmp, "roundtrip-from-td0.scp")
        out_scp_from_fdi = os.path.join(tmp, "roundtrip-from-fdi.scp")
        out_scp_from_nfd = os.path.join(tmp, "roundtrip-from-nfd.scp")
        out_scp_from_dcp = os.path.join(tmp, "roundtrip-from-dcp.scp")
        out_scp_from_a2r = os.path.join(tmp, "roundtrip-from-a2r.scp")
        out_scp_from_msa = os.path.join(tmp, "roundtrip-from-msa.scp")
        out_scp_from_apridisk = os.path.join(tmp, "roundtrip-from-apridisk.scp")
        out_scp_from_nsi = os.path.join(tmp, "roundtrip-from-nsi.scp")
        out_adf = os.path.join(tmp, "roundtrip.adf")
        out_d81 = os.path.join(tmp, "roundtrip.d81")
        out_d64 = os.path.join(tmp, "roundtrip.d64")
        out_d71 = os.path.join(tmp, "roundtrip.d71")
        out_d1m = os.path.join(tmp, "roundtrip.d1m")
        out_d2m = os.path.join(tmp, "roundtrip.d2m")
        out_d4m = os.path.join(tmp, "roundtrip.d4m")
        out_do = os.path.join(tmp, "roundtrip.do")
        out_po = os.path.join(tmp, "roundtrip.po")
        out_st = os.path.join(tmp, "roundtrip.st")
        out_ima = os.path.join(tmp, "roundtrip.ima")
        out_scp_from_adf = os.path.join(tmp, "roundtrip-from-adf.scp")
        out_scp_from_d81 = os.path.join(tmp, "roundtrip-from-d81.scp")
        out_scp_from_dim = os.path.join(tmp, "roundtrip-from-dim.scp")
        out_scp_from_d64 = os.path.join(tmp, "roundtrip-from-d64.scp")
        out_scp_from_d71 = os.path.join(tmp, "roundtrip-from-d71.scp")
        out_scp_from_d1m = os.path.join(tmp, "roundtrip-from-d1m.scp")
        out_scp_from_d2m = os.path.join(tmp, "roundtrip-from-d2m.scp")
        out_scp_from_d4m = os.path.join(tmp, "roundtrip-from-d4m.scp")
        out_scp_from_do = os.path.join(tmp, "roundtrip-from-do.scp")
        out_scp_from_po = os.path.join(tmp, "roundtrip-from-po.scp")
        out_scp_from_st = os.path.join(tmp, "roundtrip-from-st.scp")
        out_scp_from_ima = os.path.join(tmp, "roundtrip-from-ima.scp")
        out_imd = os.path.join(tmp, "roundtrip.imd")
        out_hfe = os.path.join(tmp, "roundtrip.hfe")
        out_scp_from_imd = os.path.join(tmp, "roundtrip-from-imd.scp")
        out_scp_from_hfe = os.path.join(tmp, "roundtrip-from-hfe.scp")
        out_imd_from_hfe = os.path.join(tmp, "roundtrip-from-hfe.imd")
        out_hfe_from_imd = os.path.join(tmp, "roundtrip-from-imd.hfe")
        out_raw_from_imd = os.path.join(tmp, "roundtrip-from-imd.00.0.raw")
        out_raw_from_hfe = os.path.join(tmp, "roundtrip-from-hfe.00.0.raw")
        out_scp_from_imd_from_raw = os.path.join(tmp, "roundtrip-from-imd-from-raw.scp")
        out_scp_from_hfe_from_raw = os.path.join(tmp, "roundtrip-from-hfe-from-raw.scp")
        out_scp_from_imd_from_hfe = os.path.join(tmp, "roundtrip-from-hfe-from-imd.scp")
        out_scp_from_hfe_from_imd = os.path.join(tmp, "roundtrip-from-imd-from-hfe.scp")

        src = SCP("in.scp", None)
        src.emit_track(0, 0, Flux([100000.0], [2000.0, 2500.0, 3000.0, 3500.0, 91000.0], SCP.sample_freq, index_cued=True))
        with open(in_scp, "wb") as f:
            f.write(src.get_image())

        dmk_track_data = bytearray([0x4E] * 6250)
        dmk_off = 200
        dmk_track_data[dmk_off + 0:dmk_off + 8] = b"\xa1\xa1\xa1\xfe\x00\x00\x01\x02"
        dmk_track_data[dmk_off + 22:dmk_off + 25] = b"\xa1\xa1\xa1"
        dmk_track_data[dmk_off + 25] = 0xFB
        dmk_idam_table = [0] * 64
        dmk_idam_table[0] = 0x8000 | (dmk_off + 128)
        dmk_tlen = 128 + len(dmk_track_data)
        dmk_header = struct.pack("<2BHB11x", 0, 1, dmk_tlen, 0x10)
        dmk_bytes = dmk_header + struct.pack("<64H", *dmk_idam_table) + bytes(dmk_track_data)
        with open(in_dmk, "wb") as f:
            f.write(dmk_bytes)

        td0_header_no_crc = struct.pack("<2s2x2BxBxB", b"TD", 0x21, 0, 0, 1)
        td0_header_crc = td0_crc.new(td0_header_no_crc).crcValue
        td0_header = td0_header_no_crc + struct.pack("<H", td0_header_crc)
        td0_sector_a = bytes(((0x21 + i * 7) & 0xFF) for i in range(512))
        td0_sector_b = bytes(((0x97 + i * 9) & 0xFF) for i in range(512))
        td0_track_no_crc = bytes([2, 0, 0])
        td0_track_crc = td0_crc.new(td0_track_no_crc).crcValue & 0xFF
        td0_track_header = td0_track_no_crc + bytes([td0_track_crc])
        td0_sec_a_crc = td0_crc.new(td0_sector_a).crcValue & 0xFF
        td0_sec_b_crc = td0_crc.new(td0_sector_b).crcValue & 0xFF
        td0_sec_a = struct.pack("<6BHB", 0, 0, 1, 2, 0, td0_sec_a_crc, len(td0_sector_a) + 1, 0) + td0_sector_a
        td0_sec_b = struct.pack("<6BHB", 0, 0, 2, 2, 0, td0_sec_b_crc, len(td0_sector_b) + 1, 0) + td0_sector_b
        td0_bytes = td0_header + td0_track_header + td0_sec_a + td0_sec_b + b"\xFF"
        with open(in_td0, "wb") as f:
            f.write(td0_bytes)

        fdi_header = struct.pack("<LLL4xLLLL", 0, 0x90, 32, 1024, 8, 2, 77)
        fdi_track0 = bytes(((0x33 + i * 11) & 0xFF) for i in range(8 * 1024))
        fdi_track_blank = bytes(8 * 1024)
        fdi_payload = bytearray()
        for cyl in range(77):
            for head in range(2):
                fdi_payload += fdi_track0 if (cyl == 0 and head == 0) else fdi_track_blank
        with open(in_fdi, "wb") as f:
            f.write(fdi_header + bytes(fdi_payload))

        nfd_sector_a = bytes(((0x41 + i * 13) & 0xFF) for i in range(512))
        nfd_sector_b = bytes(((0xB3 + i * 17) & 0xFF) for i in range(512))
        nfd_header_size = 288 + (163 * 26 * 16)
        nfd_header = struct.pack(
            "<15sx256sLBB10x",
            b"T98FDDIMAGE.R0\0",
            b"GW-NFD-FIXTURE".ljust(256, b"\0"),
            nfd_header_size,
            0,
            2,
        )
        nfd_track_headers = bytearray([0xFF] * (163 * 26 * 16))
        nfd_track_headers[0:16] = struct.pack("<11B5x", 0, 0, 1, 2, 1, 0, 0, 0, 0, 0, 0x90)
        nfd_track_headers[16:32] = struct.pack("<11B5x", 0, 0, 2, 2, 1, 0, 0, 0, 0, 0, 0x90)
        with open(in_nfd, "wb") as f:
            f.write(nfd_header + bytes(nfd_track_headers) + nfd_sector_a + nfd_sector_b)

        dcp_header = bytearray(162)
        dcp_header[0] = 1
        dcp_track0 = bytes(((0x29 + i * 19) & 0xFF) for i in range(8 * 1024))
        with open(in_dcp, "wb") as f:
            f.write(bytes(dcp_header) + dcp_track0)

        a2r_rwcp = bytearray(16)
        a2r_rwcp[0] = 1
        a2r_rwcp[1:5] = struct.pack("<I", 25000)
        a2r_capture_flux = bytes([20, 30, 40, 255, 12, 18, 22, 28, 34, 36])
        a2r_capture = (
            b"C" +
            struct.pack("<BHB", 3, 0, 2) +
            struct.pack("<II", 100000, 200000) +
            struct.pack("<I", len(a2r_capture_flux)) +
            a2r_capture_flux
        )
        a2r_rwcp += a2r_capture
        a2r_rwcp += b"\x00"
        a2r_bytes = b"A2R3\xff\x0a\x0d\x0a" + struct.pack("<4sI", b"RWCP", len(a2r_rwcp)) + bytes(a2r_rwcp)
        with open(in_a2r, "wb") as f:
            f.write(a2r_bytes)

        msa_track_data = bytes(((0x31 + i * 7) & 0xFF) for i in range(9 * 512))
        msa_bytes = (
            struct.pack(">2s4H", b"\x0e\x0f", 9, 0, 0, 0) +
            struct.pack(">H", len(msa_track_data)) +
            msa_track_data
        )
        with open(in_msa, "wb") as f:
            f.write(msa_bytes)

        apridisk_header = bytearray(128)
        apridisk_sig = b"ACT Apricot disk image\x1a\x04"
        apridisk_header[:len(apridisk_sig)] = apridisk_sig
        apridisk_payload = bytearray()
        for sec in range(1, 10):
            sec_data = bytes(((0x55 + sec * 11 + i * 5) & 0xFF) for i in range(512))
            rec = bytearray(16)
            rec[0:4] = (0xE31D0001).to_bytes(4, byteorder="little")
            rec[4:6] = (0x9E90).to_bytes(2, byteorder="little")
            rec[6:8] = (16).to_bytes(2, byteorder="little")
            rec[8:12] = (len(sec_data)).to_bytes(4, byteorder="little")
            rec[12] = 0
            rec[13] = sec
            rec[14:16] = (0).to_bytes(2, byteorder="little")
            apridisk_payload += rec + sec_data
        with open(in_apridisk, "wb") as f:
            f.write(bytes(apridisk_header) + bytes(apridisk_payload))

        nsi_bytes = bytes(((0x63 + i * 9) & 0xFF) for i in range(35 * 10 * 512))
        with open(in_nsi, "wb") as f:
            f.write(nsi_bytes)


        run_convert(["--tracks", "c=0:h=0", in_scp, out_raw])
        run_convert(["--tracks", "c=0:h=0", out_raw, out_scp])
        run_convert(["--tracks", "c=0:h=0", in_dmk, out_scp_from_dmk])
        run_convert(["--tracks", "c=0:h=0", in_td0, out_scp_from_td0])
        run_convert(["--tracks", "c=0:h=0", in_fdi, out_scp_from_fdi])
        run_convert(["--tracks", "c=0:h=0", in_nfd, out_scp_from_nfd])
        run_convert(["--tracks", "c=0:h=0", in_dcp, out_scp_from_dcp])
        run_convert(["--tracks", "c=0:h=0", in_a2r, out_scp_from_a2r])
        run_convert(["--tracks", "c=0:h=0", in_msa, out_scp_from_msa])
        run_convert(["--format", "ibm.720", "--tracks", "c=0:h=0", in_apridisk, out_scp_from_apridisk])
        run_convert(["--tracks", "c=0:h=0", in_nsi, out_scp_from_nsi])

        run_convert(["--tracks", "c=0:h=0", in_scp, out_adf])
        run_convert(["--tracks", "c=0:h=0", out_adf, out_scp_from_adf])
        run_convert(["--tracks", "c=0:h=0", in_scp, out_d81])
        run_convert(["--tracks", "c=0:h=0", out_d81, out_scp_from_d81])

        commodore_outputs = []
        if try_run_convert(["--tracks", "c=0:h=0", in_scp, out_d64]) and try_run_convert(["--tracks", "c=0:h=0", out_d64, out_scp_from_d64]):
            commodore_outputs.append(("from_d64", out_scp_from_d64))
        if try_run_convert(["--tracks", "c=0:h=0", in_scp, out_d71]) and try_run_convert(["--tracks", "c=0:h=0", out_d71, out_scp_from_d71]):
            commodore_outputs.append(("from_d71", out_scp_from_d71))
        if try_run_convert(["--tracks", "c=0:h=0", in_scp, out_d1m]) and try_run_convert(["--tracks", "c=0:h=0", out_d1m, out_scp_from_d1m]):
            commodore_outputs.append(("from_d1m", out_scp_from_d1m))
        if try_run_convert(["--tracks", "c=0:h=0", in_scp, out_d2m]) and try_run_convert(["--tracks", "c=0:h=0", out_d2m, out_scp_from_d2m]):
            commodore_outputs.append(("from_d2m", out_scp_from_d2m))
        if try_run_convert(["--tracks", "c=0:h=0", in_scp, out_d4m]) and try_run_convert(["--tracks", "c=0:h=0", out_d4m, out_scp_from_d4m]):
            commodore_outputs.append(("from_d4m", out_scp_from_d4m))
        if try_run_convert(["--tracks", "c=0:h=0", in_scp, out_do]) and try_run_convert(["--tracks", "c=0:h=0", out_do, out_scp_from_do]):
            commodore_outputs.append(("from_do", out_scp_from_do))
        if try_run_convert(["--tracks", "c=0:h=0", in_scp, out_po]) and try_run_convert(["--tracks", "c=0:h=0", out_po, out_scp_from_po]):
            commodore_outputs.append(("from_po", out_scp_from_po))
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", in_scp, out_st])
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_st, out_scp_from_st])
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", in_scp, out_ima])
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_ima, out_scp_from_ima])

        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", in_scp, out_imd])
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_imd, out_scp_from_imd])

        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", in_scp, out_hfe])
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_hfe, out_scp_from_hfe])

        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_hfe, out_imd_from_hfe])
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_imd_from_hfe, out_scp_from_imd_from_hfe])

        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_imd, out_hfe_from_imd])
        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_hfe_from_imd, out_scp_from_hfe_from_imd])

        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_imd, out_raw_from_imd])
        run_convert(["--tracks", "c=0:h=0", out_raw_from_imd, out_scp_from_imd_from_raw])

        run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", out_hfe, out_raw_from_hfe])
        run_convert(["--tracks", "c=0:h=0", out_raw_from_hfe, out_scp_from_hfe_from_raw])

        explicit_img_exts = [".ssd", ".dsd", ".ads", ".adm", ".adl", ".fd", ".mgt", ".sf7", ".hdm", ".xdf", ".2d", ".dsk", ".imd"]
        explicit_scp_outputs = {}
        for ext in explicit_img_exts:
            ext_name = ext.lstrip(".")
            out_ext_img = os.path.join(tmp, "roundtrip" + ext)
            out_ext_scp = os.path.join(tmp, "roundtrip-from-" + ext_name + ".scp")
            if try_run_convert(["--format", "ibm.1440", "--tracks", "c=0:h=0", in_scp, out_ext_img]) and try_run_convert(
                ["--format", "ibm.1440", "--tracks", "c=0:h=0", out_ext_img, out_ext_scp]
            ):
                explicit_scp_outputs[ext_name] = out_ext_scp

        out_hdm = os.path.join(tmp, "roundtrip.hdm")
        if os.path.exists(out_hdm):
            out_dim = os.path.join(tmp, "roundtrip-from-hdm.dim")
            dim_header = bytearray(256)
            dim_header[0] = 0
            dim_header[0xAB:0xB8] = b"DIFC HEADER  "
            with open(out_hdm, "rb") as f:
                dim_body = f.read()
            with open(out_dim, "wb") as f:
                f.write(bytes(dim_header) + dim_body)
            run_convert(["--tracks", "c=0:h=0", out_dim, out_scp_from_dim])

        for name, path in [
            ("from_raw", out_scp),
            ("from_dmk", out_scp_from_dmk),
            ("from_td0", out_scp_from_td0),
            ("from_fdi", out_scp_from_fdi),
            ("from_nfd", out_scp_from_nfd),
            ("from_dcp", out_scp_from_dcp),
            ("from_a2r", out_scp_from_a2r),
            ("from_msa", out_scp_from_msa),
            ("from_apridisk", out_scp_from_apridisk),
            ("from_nsi", out_scp_from_nsi),
            ("from_adf", out_scp_from_adf),
            ("from_d81", out_scp_from_d81),
            ("from_dim", out_scp_from_dim),
            ("from_st", out_scp_from_st),
            ("from_ima", out_scp_from_ima),
            ("from_imd", out_scp_from_imd),
            ("from_hfe", out_scp_from_hfe),
            ("from_imd_raw", out_scp_from_imd_from_raw),
            ("from_hfe_raw", out_scp_from_hfe_from_raw),
            ("from_hfe_imd", out_scp_from_imd_from_hfe),
            ("from_imd_hfe", out_scp_from_hfe_from_imd),
        ]:
            stats = scp_track_stats(path)
            stats["name"] = name
            scp_track_cases[name] = stats

        for name, path in commodore_outputs:
            stats = scp_track_stats(path)
            stats["name"] = name
            scp_track_cases[name] = stats

        for ext_name, out_ext_scp in explicit_scp_outputs.items():
            name = "from_" + ext_name
            if name in scp_track_cases:
                continue
            stats = scp_track_stats(out_ext_scp)
            stats["name"] = name
            scp_track_cases[name] = stats

    return {
        "track_summary_cases": [
            {"cyl": 0, "head": 0, "physical_cyl": 0, "physical_head": 0, "summary": track_summary(0, 0, 0, 0)},
            {"cyl": 40, "head": 1, "physical_cyl": 39, "physical_head": 0, "summary": track_summary(40, 1, 39, 0)},
        ],
        "convert_header_cases": [
            {"tracks": "c=0-81:h=0-1", "out_tracks": "c=0-81:h=0-1", "line": "Converting c=0-81:h=0-1 -> c=0-81:h=0-1"},
            {"tracks": "c=0-39:h=0", "out_tracks": "c=0-79:h=0-1", "line": "Converting c=0-39:h=0 -> c=0-79:h=0-1"},
        ],
        "format_resolution_cases": flow_cases,
        "track_resolution_cases": track_cases,
        "loop_cases": loop_cases,
        "runtime_cases": runtime_cases,
        "scp_track_cases": list(scp_track_cases.values()),
    }


def build_erase_fixtures():
    runtime_cases = [
        {
            "args": ["--tracks", "c=40:h=0", "--revs", "3"],
            "ok": True,
            "lines": ["Erasing c=40:h=0, revs=3"],
        },
        {
            "args": ["--hfreq", "--fake-index", "300rpm"],
            "ok": True,
            "lines": ["Erasing c=0-81:h=0-1, revs=1"],
        },
        {
            "args": ["--revs", "0"],
            "ok": False,
            "error_prefix": "--revs must be >= 1",
        },
        {
            "args": ["--weird"],
            "ok": False,
            "error_prefix": "unrecognized option: --weird",
        },
        {
            "args": ["in.scp"],
            "ok": False,
            "error_prefix": "erase does not take positional arguments",
        },
    ]

    return {
        "header_cases": [
            {"tracks": "c=0-81:h=0-1", "revs": 1, "line": "Erasing c=0-81:h=0-1, revs=1"},
            {"tracks": "c=40:h=0", "revs": 3, "line": "Erasing c=40:h=0, revs=3"},
        ],
        "hfreq_cases": [
            {"drive_ticks": 200000, "erase_ticks": 220000.0, "write_flux": [220000]},
            {"drive_ticks": 12345.6, "erase_ticks": 13580.16, "write_flux": [13580]},
        ],
        "runtime_cases": runtime_cases,
    }


def build_bandwidth_fixtures():
    def generate_random_buffer(nr, seed):
        dat = bytearray()
        r = seed
        for _ in range(nr):
            dat.append(r & 255)
            if r & 1:
                r = (r >> 1) ^ 0x80000062
            else:
                r >>= 1
        return list(dat)

    twobyte_us = 249 / 72
    req_min_bw = 16 / twobyte_us
    est_cases = []
    for min_r, min_w in [(8.5, 9.5), (3.0, 5.0)]:
        est = 0.9 * min(min_r, min_w)
        if req_min_bw > est:
            status = f"warning={req_min_bw:.3f}"
        else:
            max_flux_rate = ((est * 0.9) * 1e6) / 8
            status = f"max_flux={max_flux_rate / 1e6:.3f};min_ave_flux={1e6 / max_flux_rate:.3f}"
        est_cases.append(
            {
                "min_read": min_r,
                "min_write": min_w,
                "estimated": est,
                "status": status,
            }
        )

    return {
        "buffer_cases": [
            {"nr": 8, "seed": 0x12345678, "buffer": generate_random_buffer(8, 0x12345678)},
            {"nr": 6, "seed": 1, "buffer": generate_random_buffer(6, 1)},
        ],
        "required_min_bw": req_min_bw,
        "estimate_cases": est_cases,
        "runtime_cases": [
            {"args": [], "ok": True, "lines": []},
            {"args": ["--device", "COM3"], "ok": True, "lines": []},
            {"args": ["--weird"], "ok": False, "error_prefix": "unrecognized option: --weird"},
            {"args": ["extra"], "ok": False, "error_prefix": "bandwidth does not take positional arguments"},
        ],
    }


def build_rpm_fixtures():
    def speed_str(tpr):
        return "Rate: %.3f rpm ; Period: %.3f ms" % (60 / tpr, tpr * 1e3)

    summary_cases = []
    for values in [[0.2, 0.201, 0.198], [0.1666667, 0.166, 0.167, 0.168]]:
        mean = sum(values) / len(values)
        median = sorted(values)[len(values) // 2]
        summary_cases.append(
            {
                "time_per_rev": values,
                "fastest": "FASTEST:  " + speed_str(min(values)),
                "mean": "Ar.Mean:  " + speed_str(mean),
                "median": "Median:   " + speed_str(median),
                "slowest": "SLOWEST:  " + speed_str(max(values)),
            }
        )

    return {
        "speed_cases": [
            {"tpr": 0.2, "line": speed_str(0.2)},
            {"tpr": 0.1666667, "line": speed_str(0.1666667)},
        ],
        "summary_cases": summary_cases,
        "runtime_cases": [
            {"args": [], "ok": True, "lines": []},
            {"args": ["--nr", "5", "--drive", "A"], "ok": True, "lines": []},
            {"args": ["--nr", "-1"], "ok": False, "error_prefix": "invalid value for --nr: -1"},
            {"args": ["--weird"], "ok": False, "error_prefix": "unrecognized option: --weird"},
            {"args": ["extra"], "ok": False, "error_prefix": "rpm does not take positional arguments"},
        ],
    }


def build_align_fixtures():
    def tspec(cyl, head, pcyl, phead):
        s = f"T{cyl}.{head}"
        if pcyl != cyl or phead != head:
            s += f" <- Drive {pcyl}.{phead}"
        return s

    runtime_cases = [
        {
            "args": ["--tracks", "c=40:h=0", "--reads", "4", "--revs", "2"],
            "ok": True,
            "lines": ["Aligning T40.0, reading 4 times, revs=2"],
        },
        {
            "args": ["--tracks", "c=40:h=0,1", "--reads", "3"],
            "ok": True,
            "lines": ["Aligning T40 (alternating heads 0,1), reading 3 times, revs=3"],
        },
        {
            "args": ["--tracks", "c=40:h=0", "--format", "ibm.mfm"],
            "ok": True,
            "lines": ["Aligning T40.0, reading 10 times, revs=3", "Format ibm.mfm"],
        },
        {
            "args": ["--format", "ibm.mfm"],
            "ok": False,
            "error_prefix": "align requires --tracks",
        },
        {
            "args": ["--tracks", "c=40:h=0", "--format", "bad.format"],
            "ok": False,
            "error_prefix": "Unknown format 'bad.format'",
        },
        {
            "args": ["--tracks", "c=40:h=0", "--weird"],
            "ok": False,
            "error_prefix": "unrecognized option: --weird",
        },
    ]

    return {
        "tspec_cases": [
            {"cyl": 40, "head": 0, "physical_cyl": 40, "physical_head": 0, "tspec": tspec(40, 0, 40, 0)},
            {"cyl": 40, "head": 1, "physical_cyl": 39, "physical_head": 0, "tspec": tspec(40, 1, 39, 0)},
        ],
        "header_single_cases": [
            {"tspec": tspec(40, 0, 40, 0), "reads": 10, "revs": 3, "line": "Aligning T40.0, reading 10 times, revs=3"},
            {"tspec": tspec(40, 1, 39, 0), "reads": 5, "revs": 2, "line": "Aligning T40.1 <- Drive 39.0, reading 5 times, revs=2"},
        ],
        "header_multi_cases": [
            {"cyl": 40, "heads": [0, 1], "reads": 10, "revs": 3, "line": "Aligning T40 (alternating heads 0,1), reading 10 times, revs=3"},
        ],
        "validate_cases": [
            {"tracks": [], "ok": False, "error": "Align command requires at least one track (e.g., c=40:h=0)"},
            {"tracks": [{"cyl": 40, "head": 0}, {"cyl": 40, "head": 1}], "ok": True, "error": None},
            {"tracks": [{"cyl": 40, "head": 0}, {"cyl": 41, "head": 1}], "ok": False, "error": "All tracks must be on the same cylinder for alignment"},
        ],
        "alternation_cases": [
            {"read_num": 1, "track_count": 2, "index": (1 - 1) % 2},
            {"read_num": 2, "track_count": 2, "index": (2 - 1) % 2},
            {"read_num": 5, "track_count": 3, "index": (5 - 1) % 3},
        ],
        "hard_sector_cases": [
            {"hard_sectors": 8, "revs": 3, "effective_revs": (8 + 1) * (3 + 1), "effective_ticks": 0},
            {"hard_sectors": 16, "revs": 1, "effective_revs": (16 + 1) * (1 + 1), "effective_ticks": 0},
        ],
        "runtime_cases": runtime_cases,
    }


def build_track_resolution_shared_fixtures():
    erase_cases = []
    for requested in [None, "c=0-39:h=0", "c=10-12:h=1:step=2"]:
        ts = util.TrackSet("c=0-81:h=0-1")
        if requested is not None:
            ts.update_from_trackspec(requested)
        erase_cases.append(
            {
                "requested": requested,
                "resolved": str(ts),
            }
        )

    align_cases = []
    for format_tracks, requested in [
        (None, None),
        ("c=0-79:h=0-1", None),
        ("c=0-79:h=0-1", "c=40:h=0,1"),
        (None, "c=40:h=0"),
    ]:
        if format_tracks is None:
            ts = util.TrackSet("c=0-81:h=0-1")
        else:
            ts = util.TrackSet(format_tracks)
        if requested is not None:
            ts.update_from_trackspec(requested)
        align_cases.append(
            {
                "format_tracks": format_tracks,
                "requested": requested,
                "resolved": str(ts),
            }
        )

    return {
        "erase_cases": erase_cases,
        "align_cases": align_cases,
    }


def build_read_write_algo_fixtures():
    write_cases = []
    for flux_list, factor in [
        ([100, 101, 99, 250], 1.25),
        ([7, 7, 7, 7, 7], 0.9),
        ([333, 666, 999], 0.3333),
    ]:
        rem = 0.0
        scaled = []
        for x in flux_list:
            y = x * factor + rem
            val = round(y)
            rem = y - val
            scaled.append(val)
        write_cases.append(
            {
                "flux_list": flux_list,
                "factor": factor,
                "scaled": scaled,
                "final_remainder": rem,
            }
        )

    fake_index_cases = []
    for revs, ticks, drive_tpr, sample_freq in [
        (2, 0, 100000, 24000000),
        (3, 450000, 120000, 20000000),
        (1, 0, 80000, 16000000),
    ]:
        pre_index = int(sample_freq * 0.5e-3)
        effective_ticks = ticks
        if effective_ticks == 0:
            effective_ticks = revs * drive_tpr + 2 * pre_index
        index_list = [pre_index] + [drive_tpr] * ((effective_ticks - pre_index) // drive_tpr)
        fake_index_cases.append(
            {
                "revs": revs,
                "ticks": ticks,
                "drive_ticks_per_rev": drive_tpr,
                "sample_freq": sample_freq,
                "effective_ticks": effective_ticks,
                "index_list": index_list,
            }
        )

    runtime_cases = {
        "read": [
            {"args": ["out.scp"], "ok": True, "lines": ["Reading c=0-81:h=0-1 revs=3"]},
            {"args": ["--tracks", "c=40:h=0", "--revs", "2", "--format", "ibm.mfm", "out.scp"], "ok": True, "lines": ["Reading c=40:h=0 revs=2", "Format ibm.mfm"]},
            {"args": ["--live", "out.dim"], "ok": False, "error_prefix": "out.dim: Cannot create DIM image files"},
            {"args": ["--live", "out.dmk"], "ok": False, "error_prefix": "out.dmk: Cannot create DMK image files"},
            {"args": ["--live", "out.a2r"], "ok": False, "error_prefix": "out.a2r: Cannot create A2R image files"},
            {"args": ["--revs", "0", "out.scp"], "ok": False, "error_prefix": "--revs must be >= 1"},
            {"args": ["--weird", "out.scp"], "ok": False, "error_prefix": "unrecognized option: --weird"},
            {"args": [], "ok": False, "error_prefix": "read requires output file argument"},
        ],
        "write": [
            {"args": ["in.scp"], "ok": True, "lines": ["Writing c=0-81:h=0-1"]},
            {"args": ["--tracks", "c=40:h=1", "--format", "ibm.mfm", "in.scp"], "ok": True, "lines": ["Format ibm.mfm", "Writing c=40:h=1"]},
            {"args": ["--precomp", "type=mfm:0=50", "in.scp"], "ok": True, "lines": ["Writing c=0-81:h=0-1", "Precomp MFM, 0-:50ns"]},
            {"args": ["--format", "bad.format", "in.scp"], "ok": False, "error_prefix": "Unknown format 'bad.format'"},
            {"args": ["--weird", "in.scp"], "ok": False, "error_prefix": "unrecognized option: --weird"},
            {"args": [], "ok": False, "error_prefix": "write requires input file argument"},
        ],
    }

    return {
        "write_scale_cases": write_cases,
        "fake_index_cases": fake_index_cases,
        "runtime_cases": runtime_cases,
    }


def main():
    out_dir = os.path.join(REPO_ROOT, "src", "Greaseweazle.Parity", "Fixtures")
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, "error-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_error_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "flux-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_flux_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "track-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_track_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "usb-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_usb_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "tools-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_tools_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "codec-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_codec_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "image-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_image_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "scp-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_scp_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "cli-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_cli_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "trackset-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_trackset_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "actions-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_action_description_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "precomp-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_precomp_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "readwrite-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_read_write_algo_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "info-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_info_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "update-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_update_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "pin-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_pin_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "reset-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_reset_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "seek-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_seek_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "delays-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_delays_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "clean-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_clean_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "convert-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_convert_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "erase-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_erase_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "bandwidth-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_bandwidth_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "rpm-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_rpm_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "align-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_align_fixtures(), f, indent=2)
    with open(os.path.join(out_dir, "track-resolution-fixtures.json"), "w", encoding="utf-8") as f:
        json.dump(build_track_resolution_shared_fixtures(), f, indent=2)
    print("Generated parity fixtures.")


if __name__ == "__main__":
    main()
