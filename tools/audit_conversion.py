#!/usr/bin/env python3
"""
Python/C -> VB.NET conversion audit.

Produces three artifacts:

  - docs/conversion-audit.json   (machine-readable companion)
  - docs/parity-symbol-manifest.md (regenerated, reconciled with audit taxonomy)
  - docs/parity-checklist.md       (regenerated, new 4-status taxonomy)

Audit dimensions:

  1. File-level structural mapping
       1:1            - one Python file mapped to exactly one VB file
       1:N            - one Python file split across multiple VB files
       N:1            - multiple Python files folded into a single VB file
       orphan-py      - Python file with no VB counterpart
       orphan-vb      - VB file with no Python counterpart (intentional or not)

  2. Symbol-level parity (per Python class / function / method)
       mapped-exact          - bound by a 'Python map:' comment to a VB declaration
       mapped-normalized     - no 'Python map:' but a unique normalized name match
                               exists in the scoped VB file (snake -> Pascal,
                               __init__ -> New, __str__ -> ToString)
       name-only-ambiguous   - normalized name exists but is not unique in scope
       missing               - neither

  3. Strict name match (legacy, kept for naming-convention drift detection)

Usage:
  python tools/audit_conversion.py
"""

from __future__ import annotations

import ast
import json
import re
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Set, Tuple


REPO_ROOT = Path(__file__).resolve().parents[1]
PY_TREE_ROOT = REPO_ROOT / "python_source"
PY_ROOT = PY_TREE_ROOT / "src" / "greaseweazle"
VB_ROOT = REPO_ROOT / "src" / "Greaseweazle"
DOCS_DIR = REPO_ROOT / "docs"

AUDIT_JSON = DOCS_DIR / "conversion-audit.json"
MANIFEST_MD = DOCS_DIR / "parity-symbol-manifest.md"
MANIFEST_JSON = DOCS_DIR / "parity-symbol-manifest.json"
CHECKLIST_MD = DOCS_DIR / "parity-checklist.md"
CHECKLIST_JSON = DOCS_DIR / "parity-checklist.json"


# ---------------------------------------------------------------------------
# Layer definitions (mirror tools/generate_symbol_inventory.py and the
# dependency catalog so the manifest stays comparable to its prior output).
# ---------------------------------------------------------------------------
LAYER_DEFS = [
    {
        "name": "Layer 0",
        "python_globs": ["error.py"],
        "vb_globs": ["error/ErrorHandling.vb"],
    },
    {
        "name": "Layer 1",
        "python_globs": ["flux.py", "optimised/*.py"],
        "vb_globs": ["flux/FluxModel.vb", "optimised/*.vb"],
    },
    {
        "name": "Layer 2",
        "python_globs": ["track.py"],
        "vb_globs": ["track/*.vb"],
    },
    {
        "name": "Layer 3",
        "python_globs": ["usb.py"],
        "vb_globs": ["usb/*.vb"],
    },
    {
        "name": "Layer 4",
        "python_globs": ["tools/util.py"],
        "vb_globs": [
            "tools/TrackSet.vb",
            "tools/ColumnFormatter.vb",
            "tools/OptionParser.vb",
            "tools/TrackResolution.vb",
        ],
    },
    {
        "name": "Layer 5",
        "python_globs": ["codec/codec.py", "codec/**/*.py"],
        "vb_globs": ["codec/**/*.vb"],
    },
    {
        "name": "Layer 6",
        "python_globs": ["image/image.py", "image/**/*.py"],
        "vb_globs": ["image/*.vb"],
    },
    {
        "name": "Layer 7",
        "python_globs": ["tools/*.py"],
        "vb_globs": ["tools/*.vb"],
    },
    {
        "name": "Layer 8",
        "python_globs": ["cli.py"],
        "vb_globs": ["cli/Program.vb"],
    },
]


# ---------------------------------------------------------------------------
# VB / Python parsers
# ---------------------------------------------------------------------------
TYPE_OPEN_RE = re.compile(
    r"^\s*(?:Public|Friend|Private|Protected|Partial|MustInherit|NotInheritable|Shadows|Overloads|Overrides|Default|ReadOnly|WriteOnly|\s)+\s+"
    r"(Class|Module|Interface|Structure|Enum)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.IGNORECASE,
)
TYPE_CLOSE_RE = re.compile(r"^\s*End\s+(Class|Module|Interface|Structure|Enum)\b", re.IGNORECASE)
DECL_RE = re.compile(
    r"^\s*(?:Public|Private|Protected|Friend)?(?:\s+(?:Shared|Static|Overrides|Overridable|MustOverride|NotOverridable|Overloads|Shadows|Default|ReadOnly|WriteOnly|Iterator|Async|Partial|MustInherit|NotInheritable))*\s+"
    r"(Class|Module|Interface|Structure|Enum|Function|Sub|Property|Event)\s+(\[?[A-Za-z_][A-Za-z0-9_]*\]?)",
    re.IGNORECASE,
)
ENUM_MEMBER_RE = re.compile(r"^\s*([A-Za-z_][A-Za-z0-9_]*)(?:\s*=.*)?(?:'.*)?$")
PYMAP_RE = re.compile(r"Python map:\s*(src/greaseweazle/[^:]+|src/greaseweazle/\.\.\.)::(.+)$")
PYMAP_FILE_ONLY_RE = re.compile(r"Python map:\s*(src/greaseweazle/[^:\s]+\.py)\b(?!::)")


@dataclass
class PySymbol:
    module: str  # e.g. 'src/greaseweazle/flux.py'
    name: str    # e.g. 'Flux.__init__' or 'usage' or 'Flux'
    kind: str    # 'class' | 'enum' | 'function' | 'method' | 'interface'


@dataclass
class VbDecl:
    file: str        # repo-relative posix path
    name: str
    kind: str        # 'class' | 'module' | 'interface' | 'structure' | 'enum' | 'function' | 'sub' | 'property' | 'event' | 'enum_member'
    line: int
    parent: Optional[str] = None  # enclosing type, if any


@dataclass
class PyMapBinding:
    py_module: str
    py_symbol_raw: str       # raw text after '::'
    py_symbol: str           # cleaned (split on ' (')
    is_no_direct: bool       # ::(no direct 1:1 symbol; ...)
    vb_file: str
    vb_line: int             # line of the comment
    vb_decl: Optional[VbDecl] = None  # the next declaration after the comment


@dataclass
class PyFile:
    path: str
    symbols: List[PySymbol] = field(default_factory=list)
    classes: Set[str] = field(default_factory=set)


@dataclass
class VbFile:
    path: str
    decls: List[VbDecl] = field(default_factory=list)
    map_bindings: List[PyMapBinding] = field(default_factory=list)
    file_level_py_refs: Set[str] = field(default_factory=set)


# ---------------------------------------------------------------------------
# Parsers
# ---------------------------------------------------------------------------
def rel_py(path: Path) -> str:
    # Returned as 'src/greaseweazle/...' so it matches the literal text used
    # in VB 'Python map:' comments (which were authored relative to the
    # original Python package root, now housed under python_source/).
    return path.relative_to(PY_TREE_ROOT).as_posix()


def rel_vb(path: Path) -> str:
    return path.relative_to(REPO_ROOT).as_posix()


def parse_python_file(path: Path) -> PyFile:
    source = path.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(path))
    pf = PyFile(path=rel_py(path))

    for node in tree.body:
        if isinstance(node, ast.ClassDef):
            is_enum = any(
                (isinstance(base, ast.Name) and base.id == "Enum")
                or (isinstance(base, ast.Attribute) and base.attr == "Enum")
                for base in node.bases
            )
            kind = "enum" if is_enum else "class"
            pf.symbols.append(PySymbol(pf.path, node.name, kind))
            pf.classes.add(node.name)
            for sub in node.body:
                if isinstance(sub, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    pf.symbols.append(PySymbol(pf.path, f"{node.name}.{sub.name}", "method"))
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            pf.symbols.append(PySymbol(pf.path, node.name, "function"))

    return pf


def clean_decl_name(name: str) -> str:
    return name.strip("[]")


def parse_vb_file(path: Path) -> VbFile:
    rel = rel_vb(path)
    text = path.read_text(encoding="utf-8", errors="ignore")
    lines = text.splitlines()

    vf = VbFile(path=rel)
    type_stack: List[Tuple[str, str]] = []  # (kind, name)
    enum_depth = 0

    pending_pymap_lines: List[Tuple[int, str, str, bool]] = []
    # Each entry: (line_no, py_module, py_symbol_clean, is_no_direct)

    for i, line in enumerate(lines, start=1):
        m_pymap = PYMAP_RE.search(line)
        if m_pymap:
            py_path = m_pymap.group(1).strip()
            py_symbol_raw = m_pymap.group(2).strip()
            is_no_direct = py_symbol_raw.startswith("(no direct 1:1 symbol")
            py_symbol = py_symbol_raw.split(" (", 1)[0].strip()
            pending_pymap_lines.append((i, py_path, py_symbol, is_no_direct))
        else:
            m_file_only = PYMAP_FILE_ONLY_RE.search(line)
            if m_file_only:
                py_path = m_file_only.group(1).strip()
                if not py_path.endswith("..."):
                    vf.file_level_py_refs.add(py_path)

        m_open = TYPE_OPEN_RE.match(line)
        if m_open:
            kind = m_open.group(1).lower()
            name = m_open.group(2)
            decl = VbDecl(rel, name, kind, i, parent=None)
            vf.decls.append(decl)
            for line_no, py_path, py_symbol, is_no_direct in pending_pymap_lines:
                vf.map_bindings.append(
                    PyMapBinding(
                        py_module=py_path,
                        py_symbol_raw=py_symbol,
                        py_symbol=py_symbol,
                        is_no_direct=is_no_direct,
                        vb_file=rel,
                        vb_line=line_no,
                        vb_decl=decl,
                    )
                )
            pending_pymap_lines = []
            type_stack.append((kind, name))
            if kind == "enum":
                enum_depth += 1
            continue

        m_close = TYPE_CLOSE_RE.match(line)
        if m_close and type_stack:
            type_stack.pop()
            if m_close.group(1).lower() == "enum" and enum_depth > 0:
                enum_depth -= 1
            continue

        m_decl = DECL_RE.match(line)
        if m_decl:
            kind = m_decl.group(1).lower()
            if kind in ("class", "module", "interface", "structure", "enum"):
                # Already handled by TYPE_OPEN_RE
                continue
            name = clean_decl_name(m_decl.group(2))
            parent = type_stack[-1][1] if type_stack else None
            decl = VbDecl(rel, name, kind, i, parent=parent)
            vf.decls.append(decl)
            for line_no, py_path, py_symbol, is_no_direct in pending_pymap_lines:
                vf.map_bindings.append(
                    PyMapBinding(
                        py_module=py_path,
                        py_symbol_raw=py_symbol,
                        py_symbol=py_symbol,
                        is_no_direct=is_no_direct,
                        vb_file=rel,
                        vb_line=line_no,
                        vb_decl=decl,
                    )
                )
            pending_pymap_lines = []
            continue

        if enum_depth > 0 and type_stack:
            mm = ENUM_MEMBER_RE.match(line)
            stripped = line.strip()
            if mm and stripped and not stripped.startswith("'") and stripped.lower() not in {
                "public", "private", "friend", "protected"
            }:
                name = mm.group(1)
                if name.lower() not in {"public", "private", "friend", "protected", "end"}:
                    parent = type_stack[-1][1]
                    vf.decls.append(VbDecl(rel, name, "enum_member", i, parent=parent))

    return vf


# ---------------------------------------------------------------------------
# Index builders
# ---------------------------------------------------------------------------
def build_python_index() -> Dict[str, PyFile]:
    out: Dict[str, PyFile] = {}
    for p in sorted(PY_ROOT.rglob("*.py")):
        if p.name == "__init__.py" and p.read_text(encoding="utf-8").strip() == "":
            # Empty __init__.py - record path but no symbols.
            out[rel_py(p)] = PyFile(path=rel_py(p))
            continue
        out[rel_py(p)] = parse_python_file(p)
    return out


def build_vb_index() -> Dict[str, VbFile]:
    out: Dict[str, VbFile] = {}
    for p in sorted(VB_ROOT.rglob("*.vb")):
        if "/obj/" in p.as_posix() or "/bin/" in p.as_posix():
            continue
        out[rel_vb(p)] = parse_vb_file(p)
    return out


# ---------------------------------------------------------------------------
# Mapping helpers
# ---------------------------------------------------------------------------
SPECIAL_MEMBERS = {
    "__init__": ["New"],
    "__str__": ["ToString"],
    "__contains__": ["Contains"],
    "__iter__": ["GetEnumerator"],
    "__len__": ["Count", "Length"],
}


def snake_to_pascal(name: str) -> str:
    parts = [p for p in name.split("_") if p]
    if not parts:
        return name
    return "".join(p[:1].upper() + p[1:] for p in parts)


def normalize_member(name: str) -> List[str]:
    if name in SPECIAL_MEMBERS:
        return SPECIAL_MEMBERS[name]
    if name.startswith("_") and not name.startswith("__"):
        # Private-by-convention, normalize without leading underscore too.
        cleaned = name.lstrip("_")
        return [snake_to_pascal(cleaned)]
    return [snake_to_pascal(name)]


def normalize_type(name: str) -> str:
    return snake_to_pascal(name)


def expand_globs(base: Path, patterns: List[str]) -> List[Path]:
    files: List[Path] = []
    for pat in patterns:
        files.extend(base.glob(pat))
    return sorted({p for p in files if p.is_file()})


# ---------------------------------------------------------------------------
# Audit construction
# ---------------------------------------------------------------------------
@dataclass
class FileMapEntry:
    py_file: str
    vb_files: List[str]
    relation: str  # 1:1 | 1:N | orphan-py
    via_pymap: bool


@dataclass
class SymbolAuditEntry:
    python_module: str
    python_symbol: str
    kind: str
    status: str  # mapped-exact | mapped-normalized | name-only-ambiguous | missing
    mapped_vb: List[Dict[str, object]]
    notes: str = ""


def collect_pymap_index(vb_index: Dict[str, VbFile]) -> Dict[str, List[PyMapBinding]]:
    out: Dict[str, List[PyMapBinding]] = defaultdict(list)
    for vf in vb_index.values():
        for b in vf.map_bindings:
            if b.is_no_direct:
                continue
            key = f"{b.py_module}::{b.py_symbol}"
            out[key].append(b)
    return out


def collect_vb_decls_by_name(vb_index: Dict[str, VbFile]) -> Dict[str, List[VbDecl]]:
    out: Dict[str, List[VbDecl]] = defaultdict(list)
    for vf in vb_index.values():
        for d in vf.decls:
            out[d.name.lower()].append(d)
    return out


def file_level_audit(
    py_index: Dict[str, PyFile],
    vb_index: Dict[str, VbFile],
) -> Tuple[List[FileMapEntry], List[str]]:
    """
    For each Python file, find all VB files that reference it via 'Python map:'.
    Returns (per-py entries, orphan-vb list).
    """
    py_to_vb_files: Dict[str, Set[str]] = defaultdict(set)
    referenced_vb_files: Set[str] = set()
    vb_file_level_only: Set[str] = set()
    for vf in vb_index.values():
        for b in vf.map_bindings:
            if b.py_module.endswith("...") or b.py_module == "src/greaseweazle/...":
                # Pure VB scaffolding marker - doesn't count as a real Py->VB binding.
                continue
            py_to_vb_files[b.py_module].add(vf.path)
            referenced_vb_files.add(vf.path)
        for fp in vf.file_level_py_refs:
            py_to_vb_files[fp].add(vf.path)
            referenced_vb_files.add(vf.path)
            vb_file_level_only.add(vf.path)

    entries: List[FileMapEntry] = []
    for py_path, py_file in sorted(py_index.items()):
        vb_files = sorted(py_to_vb_files.get(py_path, set()))
        if not vb_files:
            relation = "orphan-py"
            # Empty __init__.py is intentionally orphan.
            if py_path.endswith("__init__.py") and not py_file.symbols:
                relation = "orphan-py-empty-init"
        elif len(vb_files) == 1:
            relation = "1:1"
        else:
            relation = "1:N"
        entries.append(
            FileMapEntry(
                py_file=py_path,
                vb_files=vb_files,
                relation=relation,
                via_pymap=bool(vb_files),
            )
        )

    # Detect orphan VB files (no Python map references to a real Python file).
    all_vb_files = {vf.path for vf in vb_index.values()}
    orphan_vb = sorted(all_vb_files - referenced_vb_files)
    return entries, orphan_vb


def find_decl_in_class(
    vb_index: Dict[str, VbFile],
    vb_files: List[str],
    class_name_candidates: List[str],
    member_candidates: List[str],
    allowed_kinds: Set[str],
) -> List[VbDecl]:
    out: List[VbDecl] = []
    members_l = {m.lower() for m in member_candidates}
    classes_l = {c.lower() for c in class_name_candidates}
    for vbf in vb_files:
        vf = vb_index.get(vbf)
        if vf is None:
            continue
        for d in vf.decls:
            if d.kind not in allowed_kinds:
                continue
            if d.name.lower() not in members_l:
                continue
            if class_name_candidates and (d.parent is None or d.parent.lower() not in classes_l):
                continue
            out.append(d)
    return out


def find_decl_in_files(
    vb_index: Dict[str, VbFile],
    vb_files: List[str],
    name_candidates: List[str],
    allowed_kinds: Set[str],
    class_filter: Optional[str] = None,
) -> List[VbDecl]:
    out: List[VbDecl] = []
    names_l = {n.lower() for n in name_candidates}
    for vbf in vb_files:
        vf = vb_index.get(vbf)
        if vf is None:
            continue
        for d in vf.decls:
            if d.kind not in allowed_kinds:
                continue
            if d.name.lower() not in names_l:
                continue
            if class_filter is not None:
                if d.parent and d.parent.lower() != class_filter.lower():
                    continue
            out.append(d)
    return out


def symbol_audit(
    py_index: Dict[str, PyFile],
    vb_index: Dict[str, VbFile],
    pymap_index: Dict[str, List[PyMapBinding]],
    file_map: Dict[str, List[str]],
) -> List[SymbolAuditEntry]:
    """
    Classify every Python symbol with the new 4-status taxonomy.
    """
    # Build a quick lookup: which VB classes are bound to a given (py_module, py_class)?
    class_to_vb: Dict[Tuple[str, str], List[str]] = defaultdict(list)
    for key, bindings in pymap_index.items():
        py_mod, py_sym = key.split("::", 1)
        if "." in py_sym:
            continue
        for b in bindings:
            if b.vb_decl and b.vb_decl.kind in {"class", "module", "interface", "structure", "enum"}:
                class_to_vb[(py_mod, py_sym)].append(b.vb_decl.name)

    out: List[SymbolAuditEntry] = []
    for py_path, py_file in sorted(py_index.items()):
        vb_files_for_py = file_map.get(py_path, [])
        for sym in py_file.symbols:
            key = f"{py_path}::{sym.name}"
            mapped: List[Dict[str, object]] = []
            status = "missing"
            notes = ""

            # 1) Exact: Python map: comment binds this exact key.
            if key in pymap_index:
                for b in pymap_index[key]:
                    if b.vb_decl is None:
                        continue
                    mapped.append(
                        {
                            "vb_file": b.vb_file,
                            "vb_declaration": b.vb_decl.name,
                            "vb_declaration_kind": b.vb_decl.kind,
                            "vb_declaration_line": b.vb_decl.line,
                            "vb_parent": b.vb_decl.parent,
                            "match": "exact",
                        }
                    )
                if mapped:
                    status = "mapped-exact"

            if status == "missing":
                # 2) Normalized: snake -> Pascal etc., looked up in scoped VB files.
                if sym.kind in {"class", "interface", "enum"}:
                    candidates = [normalize_type(sym.name)]
                    allowed = {"class", "module", "interface", "structure", "enum"}
                    hits = find_decl_in_files(vb_index, vb_files_for_py, candidates, allowed)
                elif sym.kind == "method" and "." in sym.name:
                    cls, member = sym.name.split(".", 1)
                    member_candidates = normalize_member(member)
                    allowed = {"function", "sub", "property", "event"}
                    if member == "__init__":
                        allowed = {"sub"}
                    # Prefer scoped to mapped class names.
                    vb_class_names = class_to_vb.get((py_path, cls), [normalize_type(cls)])
                    hits = find_decl_in_class(
                        vb_index, vb_files_for_py, vb_class_names, member_candidates, allowed
                    )
                    if not hits:
                        hits = find_decl_in_files(
                            vb_index, vb_files_for_py, member_candidates, allowed
                        )
                else:  # module-level function
                    candidates = normalize_member(sym.name)
                    allowed = {"function", "sub"}
                    hits = find_decl_in_files(vb_index, vb_files_for_py, candidates, allowed)

                # Deduplicate
                uniq: Dict[Tuple[str, str, int], VbDecl] = {}
                for d in hits:
                    uniq[(d.file, d.name, d.line)] = d

                if len(uniq) == 1:
                    d = next(iter(uniq.values()))
                    mapped.append(
                        {
                            "vb_file": d.file,
                            "vb_declaration": d.name,
                            "vb_declaration_kind": d.kind,
                            "vb_declaration_line": d.line,
                            "vb_parent": d.parent,
                            "match": "normalized",
                        }
                    )
                    status = "mapped-normalized"
                elif len(uniq) > 1:
                    status = "name-only-ambiguous"
                    notes = f"{len(uniq)} candidate VB declarations match by normalized name"
                    for d in uniq.values():
                        mapped.append(
                            {
                                "vb_file": d.file,
                                "vb_declaration": d.name,
                                "vb_declaration_kind": d.kind,
                                "vb_declaration_line": d.line,
                                "vb_parent": d.parent,
                                "match": "ambiguous",
                            }
                        )

            out.append(
                SymbolAuditEntry(
                    python_module=py_path,
                    python_symbol=sym.name,
                    kind=sym.kind,
                    status=status,
                    mapped_vb=mapped,
                    notes=notes,
                )
            )
    return out


def strict_name_match_per_layer(
    py_index: Dict[str, PyFile],
    vb_index: Dict[str, VbFile],
) -> List[Dict[str, object]]:
    layers_out: List[Dict[str, object]] = []
    for layer in LAYER_DEFS:
        py_paths = sorted(
            {rel_py(p) for p in expand_globs(PY_ROOT, layer["python_globs"])}
        )
        vb_paths = sorted(
            {rel_vb(p) for p in expand_globs(VB_ROOT, layer["vb_globs"])}
        )

        py_symbols: Set[str] = set()
        for p in py_paths:
            pf = py_index.get(p)
            if pf is None:
                continue
            for sym in pf.symbols:
                py_symbols.add(sym.name.lower())

        vb_symbols: Set[str] = set()
        for v in vb_paths:
            vf = vb_index.get(v)
            if vf is None:
                continue
            for d in vf.decls:
                if d.parent and d.kind != "enum_member":
                    vb_symbols.add(f"{d.parent}.{d.name}".lower())
                    vb_symbols.add(d.name.lower())
                elif d.kind == "enum_member" and d.parent:
                    vb_symbols.add(f"{d.parent}.{d.name}".lower())
                else:
                    vb_symbols.add(d.name.lower())

        missing_strict = sorted(py_symbols - vb_symbols)
        matching_strict = sorted(py_symbols & vb_symbols)

        layers_out.append(
            {
                "name": layer["name"],
                "python_files": py_paths,
                "vb_files": vb_paths,
                "python_symbol_count": len(py_symbols),
                "vb_symbol_count": len(vb_symbols),
                "matching_strict": len(matching_strict),
                "missing_strict": missing_strict,
                "sample_matches": matching_strict[:40],
            }
        )
    return layers_out


def reconcile_strict_with_audit(
    layers: List[Dict[str, object]],
    audit_entries: List[SymbolAuditEntry],
) -> List[Dict[str, object]]:
    """
    For each layer's strict-missing list, classify whether each missing
    name is actually mapped via the audit (naming-convention-only) or truly missing.
    """
    by_module: Dict[str, Dict[str, SymbolAuditEntry]] = defaultdict(dict)
    for e in audit_entries:
        by_module[e.python_module][e.python_symbol.lower()] = e

    for layer in layers:
        true_missing: List[str] = []
        naming_only: List[Dict[str, str]] = []
        for missing in layer["missing_strict"]:
            resolved = False
            for py_path in layer["python_files"]:
                e = by_module.get(py_path, {}).get(missing)
                if e is None:
                    continue
                if e.status in {"mapped-exact", "mapped-normalized"}:
                    naming_only.append(
                        {
                            "python_symbol": e.python_symbol,
                            "vb_target": (
                                f"{e.mapped_vb[0]['vb_file']}::{e.mapped_vb[0]['vb_declaration']}"
                                if e.mapped_vb
                                else ""
                            ),
                            "match": e.status,
                        }
                    )
                    resolved = True
                    break
            if not resolved:
                true_missing.append(missing)
        layer["naming_only_resolved"] = naming_only
        layer["truly_missing"] = true_missing
    return layers


# ---------------------------------------------------------------------------
# Output writers
# ---------------------------------------------------------------------------
def write_audit_json(
    file_entries: List[FileMapEntry],
    orphan_vb: List[str],
    audit_entries: List[SymbolAuditEntry],
    layers: List[Dict[str, object]],
    parity_runner_output: Optional[str],
) -> None:
    by_status = defaultdict(int)
    by_kind_status = defaultdict(lambda: defaultdict(int))
    for e in audit_entries:
        by_status[e.status] += 1
        by_kind_status[e.kind][e.status] += 1

    by_relation = defaultdict(int)
    for f in file_entries:
        by_relation[f.relation] += 1

    payload = {
        "generated_by": "tools/audit_conversion.py",
        "summary": {
            "python_files": len(file_entries),
            "vb_files_total": sum(1 for _ in VB_ROOT.rglob("*.vb")),
            "file_relation_counts": dict(by_relation),
            "orphan_vb_count": len(orphan_vb),
            "symbol_total": len(audit_entries),
            "symbol_status_counts": dict(by_status),
            "symbol_status_by_kind": {
                k: dict(v) for k, v in by_kind_status.items()
            },
        },
        "file_mapping": [
            {
                "python_file": f.py_file,
                "vb_files": f.vb_files,
                "relation": f.relation,
            }
            for f in file_entries
        ],
        "orphan_vb_files": orphan_vb,
        "symbols": [
            {
                "python_module": e.python_module,
                "python_symbol": e.python_symbol,
                "kind": e.kind,
                "status": e.status,
                "mapped_vb": e.mapped_vb,
                "notes": e.notes,
            }
            for e in audit_entries
        ],
        "layers": layers,
        "parity_runner_output": parity_runner_output or "",
    }

    AUDIT_JSON.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_manifest_md(
    file_entries: List[FileMapEntry],
    orphan_vb: List[str],
    layers: List[Dict[str, object]],
) -> None:
    lines: List[str] = []
    lines.append("# Python -> VB Symbol Parity Manifest")
    lines.append("")
    lines.append(
        "Generated by `tools/audit_conversion.py`. The legacy strict "
        "name match is preserved for naming-convention drift, but each "
        "missing-strict entry is now classified as **naming-only-resolved** "
        "(actually mapped, just renamed snake_case -> PascalCase / dunder -> "
        "VB equivalent) or **truly missing**."
    )
    lines.append("")
    py_count = len(file_entries)
    vb_count = sum(1 for _ in VB_ROOT.rglob("*.vb"))
    lines.append(f"- Python files indexed: `{py_count}`")
    lines.append(f"- VB files indexed: `{vb_count}`")
    by_rel = defaultdict(int)
    for f in file_entries:
        by_rel[f.relation] += 1
    rel_summary = ", ".join(f"{k}: {v}" for k, v in sorted(by_rel.items()))
    lines.append(f"- File-level relations: {rel_summary}")
    lines.append("")

    for layer in layers:
        lines.append(f"## {layer['name']}")
        lines.append("")
        lines.append(f"- Python files: `{len(layer['python_files'])}`")
        lines.append(f"- VB files: `{len(layer['vb_files'])}`")
        lines.append(f"- Python symbols (lowercased): `{layer['python_symbol_count']}`")
        lines.append(f"- VB symbols (lowercased): `{layer['vb_symbol_count']}`")
        lines.append(f"- Strict name matches: `{layer['matching_strict']}`")
        lines.append(f"- Naming-only resolved: `{len(layer['naming_only_resolved'])}`")
        lines.append(f"- Truly missing: `{len(layer['truly_missing'])}`")
        lines.append("")
        lines.append("### Python files")
        for path in layer["python_files"]:
            lines.append(f"- `{path}`")
        lines.append("")
        lines.append("### VB files")
        for path in layer["vb_files"]:
            lines.append(f"- `{path}`")
        lines.append("")
        if layer["naming_only_resolved"]:
            lines.append("### Naming-only resolved (renamed, behaviorally mapped)")
            for entry in layer["naming_only_resolved"][:200]:
                lines.append(
                    f"- `{entry['python_symbol']}` -> `{entry['vb_target']}` "
                    f"({entry['match']})"
                )
            if len(layer["naming_only_resolved"]) > 200:
                lines.append(
                    f"- `... +{len(layer['naming_only_resolved']) - 200} more`"
                )
            lines.append("")
        lines.append("### Truly missing in VB")
        if layer["truly_missing"]:
            for sym in layer["truly_missing"][:200]:
                lines.append(f"- `{sym}`")
            if len(layer["truly_missing"]) > 200:
                lines.append(f"- `... +{len(layer['truly_missing']) - 200} more`")
        else:
            lines.append("- None")
        lines.append("")

    lines.append("## Orphan VB files (no `Python map:` reference to any real Python module)")
    lines.append("")
    if orphan_vb:
        for p in orphan_vb:
            lines.append(f"- `{p}`")
    else:
        lines.append("- None")
    lines.append("")

    MANIFEST_MD.write_text("\n".join(lines), encoding="utf-8")


def write_manifest_json(
    file_entries: List[FileMapEntry],
    orphan_vb: List[str],
    layers: List[Dict[str, object]],
) -> None:
    by_rel = defaultdict(int)
    for f in file_entries:
        by_rel[f.relation] += 1
    payload = {
        "generated_by": "tools/audit_conversion.py",
        "python_files_total": len(file_entries),
        "vb_files_total": sum(1 for _ in VB_ROOT.rglob("*.vb")),
        "file_relation_counts": dict(by_rel),
        "orphan_vb_files": orphan_vb,
        "layers": [
            {
                "name": layer["name"],
                "python_files": layer["python_files"],
                "vb_files": layer["vb_files"],
                "python_symbol_count": layer["python_symbol_count"],
                "vb_symbol_count": layer["vb_symbol_count"],
                "matching_strict": layer["matching_strict"],
                "missing_strict": layer["missing_strict"],
                "naming_only_resolved": layer["naming_only_resolved"],
                "truly_missing": layer["truly_missing"],
                "sample_matches": layer["sample_matches"],
            }
            for layer in layers
        ],
    }
    MANIFEST_JSON.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_checklist(audit_entries: List[SymbolAuditEntry]) -> None:
    by_status = defaultdict(int)
    by_kind = defaultdict(lambda: {"total": 0, "by_status": defaultdict(int)})
    for e in audit_entries:
        by_status[e.status] += 1
        by_kind[e.kind]["total"] += 1
        by_kind[e.kind]["by_status"][e.status] += 1

    grouped: Dict[str, List[SymbolAuditEntry]] = defaultdict(list)
    for e in audit_entries:
        grouped[e.python_module].append(e)

    total = len(audit_entries)
    md_lines: List[str] = []
    md_lines.append("# Python -> VB Parity Checklist")
    md_lines.append("")
    md_lines.append("## Summary")
    md_lines.append("")
    md_lines.append(f"- Total symbols: **{total}**")
    md_lines.append(f"- Mapped exact (`Python map:` comment): **{by_status['mapped-exact']}**")
    md_lines.append(f"- Mapped normalized (renamed, unique scoped match): **{by_status['mapped-normalized']}**")
    md_lines.append(f"- Name-only ambiguous (multiple candidates, no `Python map:`): **{by_status['name-only-ambiguous']}**")
    md_lines.append(f"- Truly missing: **{by_status['missing']}**")
    md_lines.append("- By kind:")
    for k in sorted(by_kind):
        d = by_kind[k]
        ex = d["by_status"]["mapped-exact"]
        nr = d["by_status"]["mapped-normalized"]
        amb = d["by_status"]["name-only-ambiguous"]
        miss = d["by_status"]["missing"]
        md_lines.append(
            f"  - `{k}`: total {d['total']} | exact {ex} | normalized {nr} | ambiguous {amb} | missing {miss}"
        )
    md_lines.append("- Events: Python AST scan found no event symbols (Python has no native event declaration construct).")
    md_lines.append("")
    md_lines.append("Status taxonomy:")
    md_lines.append(
        "- `mapped-exact`: a `' Python map: <py_module>::<symbol>` comment "
        "binds the symbol to a specific VB declaration. Highest confidence."
    )
    md_lines.append(
        "- `mapped-normalized`: no `Python map:` comment, but exactly one "
        "VB declaration in the scoped VB file matches the normalized name "
        "(snake_case -> PascalCase, `__init__` -> `New`, `__str__` -> "
        "`ToString`). Medium confidence."
    )
    md_lines.append(
        "- `name-only-ambiguous`: multiple normalized matches exist; not "
        "uniquely bindable without a `Python map:` comment. Low confidence."
    )
    md_lines.append("- `missing`: no exact and no normalized match.")
    md_lines.append("")

    json_symbols: List[Dict[str, object]] = []

    for module in sorted(grouped):
        md_lines.append(f"## `{module}`")
        md_lines.append("")
        for s in grouped[module]:
            checked = "x" if s.status.startswith("mapped") else " "
            if s.mapped_vb:
                vb_text = "; ".join(
                    f"`{m['vb_file']}::{m['vb_declaration']}` ({m['match']})"
                    for m in s.mapped_vb
                )
            else:
                vb_text = "_(none)_"
            md_lines.append(
                f"- [{checked}] `{s.python_symbol}` | kind: `{s.kind}` | "
                f"mapped VB: {vb_text} | status: `{s.status}`"
            )
            json_symbols.append(
                {
                    "python_module": s.python_module,
                    "python_symbol": s.python_symbol,
                    "kind": s.kind,
                    "status": s.status,
                    "mapped_vb_symbols": s.mapped_vb,
                    "notes": s.notes,
                }
            )
        md_lines.append("")

    CHECKLIST_MD.write_text("\n".join(md_lines) + "\n", encoding="utf-8")

    json_payload = {
        "generated_by": "tools/audit_conversion.py",
        "summary": {
            "total_symbols": total,
            "by_status": dict(by_status),
            "by_kind": {
                k: {"total": v["total"], "by_status": dict(v["by_status"])}
                for k, v in by_kind.items()
            },
            "status_derivation": {
                "mapped-exact": "A 'Python map: <py_module>::<symbol>' comment in VB binds this symbol to a VB declaration.",
                "mapped-normalized": "No 'Python map:' comment for this symbol, but a unique VB declaration in the scoped file(s) matches the normalized name.",
                "name-only-ambiguous": "Multiple VB declarations match the normalized name; cannot bind uniquely.",
                "missing": "No exact or normalized VB match found.",
            },
        },
        "symbols": json_symbols,
    }
    CHECKLIST_JSON.write_text(json.dumps(json_payload, indent=2), encoding="utf-8")


# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------
def main() -> None:
    py_index = build_python_index()
    vb_index = build_vb_index()
    pymap_index = collect_pymap_index(vb_index)

    file_entries, orphan_vb = file_level_audit(py_index, vb_index)
    file_map = {fe.py_file: fe.vb_files for fe in file_entries}

    audit_entries = symbol_audit(py_index, vb_index, pymap_index, file_map)
    layers = strict_name_match_per_layer(py_index, vb_index)
    layers = reconcile_strict_with_audit(layers, audit_entries)

    runner_output_path = DOCS_DIR / "parity-runner-output.txt"
    runner_text = runner_output_path.read_text(encoding="utf-8") if runner_output_path.exists() else None

    DOCS_DIR.mkdir(parents=True, exist_ok=True)
    write_audit_json(file_entries, orphan_vb, audit_entries, layers, runner_text)
    write_manifest_md(file_entries, orphan_vb, layers)
    write_manifest_json(file_entries, orphan_vb, layers)
    write_checklist(audit_entries)

    by_status = defaultdict(int)
    for e in audit_entries:
        by_status[e.status] += 1
    by_rel = defaultdict(int)
    for f in file_entries:
        by_rel[f.relation] += 1

    print(f"python_files={len(file_entries)} vb_files_total={sum(1 for _ in VB_ROOT.rglob('*.vb'))}")
    print("file_relations:", dict(by_rel))
    print("orphan_vb_count:", len(orphan_vb))
    print("symbol_total:", len(audit_entries))
    print("symbol_status:", dict(by_status))
    print(f"wrote: {AUDIT_JSON}")
    print(f"wrote: {MANIFEST_MD}")
    print(f"wrote: {MANIFEST_JSON}")
    print(f"wrote: {CHECKLIST_MD}")
    print(f"wrote: {CHECKLIST_JSON}")


if __name__ == "__main__":
    main()
