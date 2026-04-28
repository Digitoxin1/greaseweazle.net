#!/usr/bin/env python3
"""Generate a Python<->VB symbol inventory for parity tracking."""

from __future__ import annotations

import ast
import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Set


REPO_ROOT = Path(__file__).resolve().parents[1]
PY_ROOT = REPO_ROOT / "python_source" / "src" / "greaseweazle"
VB_ROOT = REPO_ROOT / "src"
DOCS_DIR = REPO_ROOT / "docs"
MANIFEST_JSON = DOCS_DIR / "parity-symbol-manifest.json"
MANIFEST_MD = DOCS_DIR / "parity-symbol-manifest.md"


LAYER_DEFS = [
    {
        "name": "Layer 0",
        "python_globs": ["error.py"],
        "vb_globs": ["Greaseweazle/error/ErrorHandling.vb"],
    },
    {
        "name": "Layer 1",
        "python_globs": ["flux.py", "optimised/*.py"],
        "vb_globs": ["Greaseweazle/flux/FluxModel.vb", "Greaseweazle/optimised/*.vb"],
    },
    {
        "name": "Layer 2",
        "python_globs": ["track.py"],
        "vb_globs": ["Greaseweazle/track/TrackModel.vb", "Greaseweazle/track/PllProfiles.vb"],
    },
    {
        "name": "Layer 3",
        "python_globs": ["usb.py"],
        "vb_globs": ["Greaseweazle/usb/UsbProtocol.vb", "Greaseweazle/usb/UsbUnitClient.vb"],
    },
    {
        "name": "Layer 4",
        "python_globs": ["tools/util.py"],
        "vb_globs": [
            "Greaseweazle/tools/TrackSet.vb",
            "Greaseweazle/tools/ColumnFormatter.vb",
            "Greaseweazle/tools/OptionParser.vb",
        ],
    },
    {
        "name": "Layer 5",
        "python_globs": ["codec/codec.py", "codec/**/*.py"],
        "vb_globs": ["Greaseweazle/codec/**/*.vb"],
    },
    {
        "name": "Layer 6",
        "python_globs": ["image/image.py", "image/**/*.py"],
        "vb_globs": ["Greaseweazle/image/*.vb"],
    },
    {
        "name": "Layer 7",
        "python_globs": ["tools/*.py"],
        "vb_globs": ["Greaseweazle/tools/*.vb"],
    },
    {
        "name": "Layer 8",
        "python_globs": ["cli.py"],
        "vb_globs": ["Greaseweazle/cli/Program.vb"],
    },
]


TYPE_OPEN_RE = re.compile(
    r"^\s*(?:Public|Friend|Private|Protected|Partial|MustInherit|NotInheritable|Shadows|Overloads|Overrides|Default|ReadOnly|WriteOnly|\s)+\s+"
    r"(Class|Module|Interface|Structure|Enum)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.IGNORECASE,
)
TYPE_CLOSE_RE = re.compile(r"^\s*End\s+(Class|Module|Interface|Structure|Enum)\b", re.IGNORECASE)
FUNC_RE = re.compile(
    r"^\s*(?:Public|Friend|Private|Protected|Shared|Static|Overrides|Overridable|MustOverride|NotOverridable|Overloads|Shadows|Default|ReadOnly|WriteOnly|Iterator|Async|\s)+\s+"
    r"(Function|Sub)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.IGNORECASE,
)
ENUM_MEMBER_RE = re.compile(r"^\s*([A-Za-z_][A-Za-z0-9_]*)(?:\s*=.*)?(?:'.*)?$")


@dataclass
class FileSymbols:
    path: str
    classes: List[str] = field(default_factory=list)
    enums: List[str] = field(default_factory=list)
    functions: List[str] = field(default_factory=list)
    methods: List[str] = field(default_factory=list)

    def all_symbols(self) -> Set[str]:
        return set(self.classes + self.enums + self.functions + self.methods)


def rel_py(path: Path) -> str:
    return path.relative_to(PY_ROOT).as_posix()


def rel_vb(path: Path) -> str:
    return path.relative_to(VB_ROOT).as_posix()


def _sort_unique(values: Iterable[str]) -> List[str]:
    return sorted(set(values), key=lambda s: s.lower())


def parse_python_file(path: Path) -> FileSymbols:
    source = path.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(path))
    file_symbols = FileSymbols(path=rel_py(path))

    for node in tree.body:
        if isinstance(node, ast.ClassDef):
            file_symbols.classes.append(node.name)
            is_enum = any(
                isinstance(base, ast.Name) and base.id == "Enum"
                or isinstance(base, ast.Attribute) and base.attr == "Enum"
                for base in node.bases
            )
            if is_enum:
                file_symbols.enums.append(node.name)
            for sub in node.body:
                if isinstance(sub, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    file_symbols.methods.append(f"{node.name}.{sub.name}")
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            file_symbols.functions.append(node.name)

    file_symbols.classes = _sort_unique(file_symbols.classes)
    file_symbols.enums = _sort_unique(file_symbols.enums)
    file_symbols.functions = _sort_unique(file_symbols.functions)
    file_symbols.methods = _sort_unique(file_symbols.methods)
    return file_symbols


def parse_vb_file(path: Path) -> FileSymbols:
    lines = path.read_text(encoding="utf-8").splitlines()
    file_symbols = FileSymbols(path=rel_vb(path))
    type_stack: List[str] = []
    enum_depth = 0

    for line in lines:
        open_match = TYPE_OPEN_RE.match(line)
        if open_match:
            type_kind = open_match.group(1).lower()
            type_name = open_match.group(2)
            type_stack.append(type_name)
            if type_kind == "enum":
                enum_depth += 1
                file_symbols.enums.append(type_name)
            else:
                file_symbols.classes.append(type_name)
            continue

        close_match = TYPE_CLOSE_RE.match(line)
        if close_match and type_stack:
            closing_kind = close_match.group(1).lower()
            type_stack.pop()
            if closing_kind == "enum" and enum_depth > 0:
                enum_depth -= 1
            continue

        fn_match = FUNC_RE.match(line)
        if fn_match:
            fn_name = fn_match.group(2)
            if type_stack:
                file_symbols.methods.append(f"{type_stack[-1]}.{fn_name}")
            else:
                file_symbols.functions.append(fn_name)
            continue

        if enum_depth > 0 and type_stack:
            member_match = ENUM_MEMBER_RE.match(line)
            if member_match and not line.strip().startswith("'"):
                member_name = member_match.group(1)
                if member_name.lower() not in {"public", "private", "friend", "protected"}:
                    file_symbols.methods.append(f"{type_stack[-1]}.{member_name}")

    file_symbols.classes = _sort_unique(file_symbols.classes)
    file_symbols.enums = _sort_unique(file_symbols.enums)
    file_symbols.functions = _sort_unique(file_symbols.functions)
    file_symbols.methods = _sort_unique(file_symbols.methods)
    return file_symbols


def expand_patterns(base_dir: Path, patterns: List[str]) -> List[Path]:
    files: List[Path] = []
    for pattern in patterns:
        files.extend(base_dir.glob(pattern))
    return sorted({p for p in files if p.is_file()})


def flatten_symbol_set(file_symbols: Iterable[FileSymbols]) -> Set[str]:
    result: Set[str] = set()
    for item in file_symbols:
        result.update(item.all_symbols())
    return {s.lower() for s in result}


def build_manifest() -> Dict[str, object]:
    python_index: Dict[str, FileSymbols] = {}
    for py_file in sorted(PY_ROOT.rglob("*.py")):
        python_index[rel_py(py_file)] = parse_python_file(py_file)

    vb_index: Dict[str, FileSymbols] = {}
    for vb_file in sorted(VB_ROOT.rglob("*.vb")):
        if "/obj/" in vb_file.as_posix():
            continue
        vb_index[rel_vb(vb_file)] = parse_vb_file(vb_file)

    layers_output: List[Dict[str, object]] = []

    for layer in LAYER_DEFS:
        py_paths = [rel_py(p) for p in expand_patterns(PY_ROOT, layer["python_globs"])]
        vb_paths = [rel_vb(p) for p in expand_patterns(VB_ROOT, layer["vb_globs"])]

        py_symbols = [python_index[path] for path in py_paths if path in python_index]
        vb_symbols = [vb_index[path] for path in vb_paths if path in vb_index]

        py_flat = flatten_symbol_set(py_symbols)
        vb_flat = flatten_symbol_set(vb_symbols)

        missing = sorted(py_flat - vb_flat)
        matching = sorted(py_flat & vb_flat)

        layers_output.append(
            {
                "name": layer["name"],
                "python_files": py_paths,
                "vb_files": vb_paths,
                "python_symbol_count": len(py_flat),
                "vb_symbol_count": len(vb_flat),
                "matching_symbol_count": len(matching),
                "missing_in_vb": missing,
                "sample_matches": matching[:40],
            }
        )

    native_touchpoints = {
        "python_optimised_exports": [
            "flux_to_bitcells",
            "decode_flux",
            "decode_mac_gcr",
            "encode_mac_gcr",
            "decode_mac_sector",
            "encode_mac_sector",
            "decode_c64_gcr",
            "encode_c64_gcr",
            "decode_apple2_sector",
            "encode_apple2_sector",
            "td0_unpack",
        ],
        "python_native_modules": [
            "image/caps.py",
            "tools/list_ports_windows.py",
            "optimised/optimised.c",
            "optimised/apple_gcr_6a2.c",
            "optimised/apple2.c",
            "optimised/c64.c",
            "optimised/mac.c",
            "optimised/td0_lzss.c",
        ],
        "vb_native_replacement_files": [
            "Greaseweazle/optimised/OptimizedFlux.vb",
            "Greaseweazle/optimised/Td0Lzss.vb",
            "Greaseweazle/optimised/Apple2.vb",
            "Greaseweazle/optimised/C64.vb",
            "Greaseweazle/optimised/Mac.vb",
            "Greaseweazle/optimised/AppleGcr62.vb",
            "Greaseweazle/image/CAPSImage.vb",
            "Greaseweazle/usb/SerialPortTransport.vb",
            "Greaseweazle/usb/UsbProtocol.vb",
        ],
    }

    return {
        "generated_by": "tools/generate_symbol_inventory.py",
        "layers": layers_output,
        "native_touchpoints": native_touchpoints,
        "python_files_total": len(python_index),
        "vb_files_total": len(vb_index),
    }


def render_markdown(manifest: Dict[str, object]) -> str:
    lines: List[str] = []
    lines.append("# Python to VB Symbol Parity Manifest")
    lines.append("")
    lines.append("Generated from repository sources for strict layer gating.")
    lines.append("")
    lines.append(
        f"- Python files indexed: `{manifest['python_files_total']}`"
    )
    lines.append(
        f"- VB files indexed: `{manifest['vb_files_total']}`"
    )
    lines.append("")

    for layer in manifest["layers"]:
        layer_name = layer["name"]
        lines.append(f"## {layer_name}")
        lines.append("")
        lines.append(f"- Python files: `{len(layer['python_files'])}`")
        lines.append(f"- VB files: `{len(layer['vb_files'])}`")
        lines.append(f"- Python symbols: `{layer['python_symbol_count']}`")
        lines.append(f"- VB symbols: `{layer['vb_symbol_count']}`")
        lines.append(f"- Matching names: `{layer['matching_symbol_count']}`")
        lines.append("")
        lines.append("### Python files")
        for path in layer["python_files"]:
            lines.append(f"- `{path}`")
        lines.append("")
        lines.append("### VB files")
        for path in layer["vb_files"]:
            lines.append(f"- `{path}`")
        lines.append("")
        lines.append("### Missing in VB (name-level)")
        missing = layer["missing_in_vb"]
        if missing:
            for symbol in missing[:120]:
                lines.append(f"- `{symbol}`")
            if len(missing) > 120:
                lines.append(f"- `... +{len(missing) - 120} more`")
        else:
            lines.append("- None")
        lines.append("")

    lines.append("## Native touchpoints")
    lines.append("")
    lines.append("### Python optimised exports")
    for name in manifest["native_touchpoints"]["python_optimised_exports"]:
        lines.append(f"- `{name}`")
    lines.append("")
    lines.append("### Python native modules/files")
    for name in manifest["native_touchpoints"]["python_native_modules"]:
        lines.append(f"- `{name}`")
    lines.append("")
    lines.append("### VB replacement files")
    for name in manifest["native_touchpoints"]["vb_native_replacement_files"]:
        lines.append(f"- `{name}`")
    lines.append("")
    return "\n".join(lines)


def main() -> None:
    manifest = build_manifest()
    DOCS_DIR.mkdir(parents=True, exist_ok=True)
    MANIFEST_JSON.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    MANIFEST_MD.write_text(render_markdown(manifest), encoding="utf-8")
    print(f"Wrote {MANIFEST_JSON}")
    print(f"Wrote {MANIFEST_MD}")


if __name__ == "__main__":
    main()
