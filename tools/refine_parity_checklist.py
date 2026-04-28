#!/usr/bin/env python3
"""
Refine parity-checklist status by inferring likely VB mappings
when exact Python-map comments are missing.
"""

from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Tuple


RE_DECL = re.compile(
    r"^\s*(?:Public|Private|Protected|Friend)?(?:\s+\w+)*\s+"
    r"(Class|Interface|Enum|Function|Sub|Property)\s+(\[?[A-Za-z_][A-Za-z0-9_]*\]?)\b",
    re.IGNORECASE,
)
RE_PYMAP = re.compile(r"Python map:\s*(src/greaseweazle/[^:]+)::(.+)$")


def snake_to_pascal(name: str) -> str:
    parts = [p for p in name.split("_") if p]
    if not parts:
        return name
    return "".join(p[:1].upper() + p[1:] for p in parts)


def normalize_expected_member(member: str) -> List[str]:
    special = {
        "__init__": ["New"],
        "__str__": ["ToString"],
        "__contains__": ["Contains"],
        "__iter__": ["GetEnumerator"],
    }
    if member in special:
        return special[member]
    return [snake_to_pascal(member)]


def normalize_expected_type(name: str) -> str:
    return snake_to_pascal(name)


def clean_decl_name(name: str) -> str:
    return name.strip("[]")


def parse_vb_declarations(vb_root: Path, repo_root: Path) -> Tuple[Dict[str, List[dict]], Dict[str, List[dict]]]:
    by_file: Dict[str, List[dict]] = defaultdict(list)
    by_name: Dict[str, List[dict]] = defaultdict(list)
    for vb_file in vb_root.rglob("*.vb"):
        rel = vb_file.relative_to(repo_root).as_posix()
        lines = vb_file.read_text(encoding="utf-8", errors="ignore").splitlines()
        for i, line in enumerate(lines, start=1):
            m = RE_DECL.match(line)
            if not m:
                continue
            kind = m.group(1).lower()
            name = clean_decl_name(m.group(2))
            entry = {
                "vb_file": rel,
                "vb_declaration": name,
                "vb_declaration_kind": kind,
                "vb_declaration_line": i,
            }
            by_file[rel].append(entry)
            by_name[name.lower()].append(entry)
    return by_file, by_name


def parse_exact_symbol_mappings(
    vb_root: Path,
    repo_root: Path,
    by_file: Dict[str, List[dict]],
) -> Dict[str, List[dict]]:
    """
    Parse exact Python->VB mappings from 'Python map:' comments and bind
    each mapping to the next VB declaration in the same file.
    """
    out: Dict[str, List[dict]] = defaultdict(list)

    for vb_file in vb_root.rglob("*.vb"):
        rel = vb_file.relative_to(repo_root).as_posix()
        decls = sorted(by_file.get(rel, []), key=lambda d: d["vb_declaration_line"])
        if not decls:
            continue

        lines = vb_file.read_text(encoding="utf-8", errors="ignore").splitlines()
        for i, line in enumerate(lines, start=1):
            m = RE_PYMAP.search(line)
            if not m:
                continue

            py_path = m.group(1).strip()
            py_symbol_raw = m.group(2).strip()
            if py_path.startswith("src/greaseweazle/..."):
                continue
            if py_symbol_raw.startswith("(no direct 1:1 symbol"):
                continue

            py_symbol = py_symbol_raw.split(" (", 1)[0].strip()
            if not py_symbol:
                continue

            target_decl = None
            for d in decls:
                if d["vb_declaration_line"] > i:
                    target_decl = d
                    break
            if target_decl is None:
                continue

            key = f"{py_path}::{py_symbol}"
            out[key].append(
                {
                    "vb_file": rel,
                    "vb_declaration": target_decl["vb_declaration"],
                    "vb_declaration_kind": target_decl["vb_declaration_kind"],
                    "vb_declaration_line": target_decl["vb_declaration_line"],
                }
            )

    # De-duplicate
    deduped: Dict[str, List[dict]] = {}
    for k, vals in out.items():
        uniq = {}
        for d in vals:
            uniq[(d["vb_file"], d["vb_declaration"], d["vb_declaration_line"])] = d
        deduped[k] = list(uniq.values())
    return deduped


def module_scope_candidates(python_module: str, vb_files: List[str]) -> List[str]:
    mod_path = python_module.lower()
    stem = Path(mod_path).stem
    parts = [p for p in Path(mod_path).parts if p not in {"src", "greaseweazle"}]
    keys = set(parts + [stem])
    out = []
    for f in vb_files:
        fl = f.lower()
        if any(k and k in fl for k in keys):
            out.append(f)
    return out


def find_unique_decl(
    expected_names: List[str],
    scoped_files: List[str],
    by_file: Dict[str, List[dict]],
    by_name: Dict[str, List[dict]],
    allowed_kinds: set[str],
) -> dict | None:
    scoped_hits: List[dict] = []
    scoped_set = set(scoped_files)
    for exp in expected_names:
        for d in by_name.get(exp.lower(), []):
            if d["vb_declaration_kind"] in allowed_kinds and (not scoped_set or d["vb_file"] in scoped_set):
                scoped_hits.append(d)
    uniq = {(d["vb_file"], d["vb_declaration"], d["vb_declaration_line"]): d for d in scoped_hits}
    if len(uniq) == 1:
        return next(iter(uniq.values()))
    return None


def generate_md(data: dict, md_path: Path) -> None:
    syms = data["symbols"]
    grouped: Dict[str, List[dict]] = defaultdict(list)
    for s in syms:
        grouped[s["python_module"]].append(s)

    lines: List[str] = []
    summary = data["summary"]
    lines.append("# Python -> VB Parity Checklist")
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- Total symbols: **{summary['total_symbols']}**")
    lines.append(f"- Exact-implemented: **{summary['exact_implemented']}**")
    lines.append(f"- Refined-implemented: **{summary['refined_implemented']}**")
    lines.append(f"- Missing: **{summary['refined_missing']}**")
    lines.append("- By kind (refined):")
    for k in sorted(summary["by_kind_refined"]):
        d = summary["by_kind_refined"][k]
        lines.append(f"  - `{k}`: total {d['total']}, implemented {d['implemented']}, missing {d['missing']}")
    lines.append("- Events: Python symbol extraction found no event symbols.")
    lines.append("")
    lines.append("Status derivation:")
    lines.append("- `implemented-exact`: exact `Python map:` symbol key match.")
    lines.append("- `implemented-inferred`: no exact key, but unique VB declaration inferred by symbol-normalization + scoped file matching.")
    lines.append("- `missing`: no exact or inferred mapping.")
    lines.append("")

    for module in sorted(grouped):
        lines.append(f"## `{module}`")
        lines.append("")
        for s in grouped[module]:
            status = s["status_refined"]
            checked = "x" if status.startswith("implemented") else " "
            mapped = s["mapped_vb_symbols"]
            if mapped:
                vb_text = "; ".join(
                    f"`{m['vb_file']}::{m['vb_declaration']}`"
                    + (" (inferred)" if m.get("inferred") else "")
                    for m in mapped
                )
            else:
                vb_text = "_(none)_"
            lines.append(
                f"- [{checked}] `{s['python_symbol']}` | kind: `{s['kind']}` | "
                f"mapped VB: {vb_text} | status: `{status}`"
            )
        lines.append("")

    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    repo = Path(__file__).resolve().parents[1]
    json_path = repo / "docs/parity-checklist.json"
    md_path = repo / "docs/parity-checklist.md"
    vb_root = repo / "src/Greaseweazle"

    data = json.loads(json_path.read_text(encoding="utf-8"))
    symbols: List[dict] = data["symbols"]

    by_file, by_name = parse_vb_declarations(vb_root, repo)
    exact_map = parse_exact_symbol_mappings(vb_root, repo, by_file)
    all_vb_files = sorted(by_file.keys())

    class_file_map: Dict[Tuple[str, str], List[str]] = defaultdict(list)
    for s in symbols:
        if s["kind"] in {"class", "interface", "enum"} and s.get("mapped_vb_symbols"):
            key = (s["python_module"], s["python_symbol"])
            class_file_map[key] = [m["vb_file"] for m in s["mapped_vb_symbols"]]

    repo_prefix = repo.as_posix().lower().rstrip("/") + "/"

    def normalize_vb_path(p: str) -> str:
        p_norm = p.replace("\\", "/")
        p_low = p_norm.lower()
        if p_low.startswith(repo_prefix):
            return p_norm[len(repo_prefix):]
        return p_norm

    exact_implemented = 0
    refined_implemented = 0

    for s in symbols:
        key = f"{s['python_module']}::{s['python_symbol']}"
        base_mapped = []
        for m in exact_map.get(key, []):
            m2 = dict(m)
            if "vb_file" in m2:
                m2["vb_file"] = normalize_vb_path(m2["vb_file"])
            base_mapped.append(m2)
        s["mapped_vb_symbols"] = base_mapped

        exact = bool(base_mapped)
        if exact:
            exact_implemented += 1
            s["status_exact"] = "implemented-exact"
            s["status_refined"] = "implemented-exact"
            refined_implemented += 1
            continue

        py_mod = s["python_module"]
        py_sym = s["python_symbol"]
        kind = s["kind"]

        scoped_files: List[str] = []
        expected_names: List[str] = []
        allowed_kinds = {"function", "sub", "property"}

        if kind in {"class", "interface", "enum"}:
            expected_names = [normalize_expected_type(py_sym)]
            allowed_kinds = {"class", "interface", "enum"}
            scoped_files = module_scope_candidates(py_mod, all_vb_files)
        elif "." in py_sym and kind == "method":
            cls, member = py_sym.split(".", 1)
            expected_names = normalize_expected_member(member)
            scoped_files = class_file_map.get((py_mod, cls), []) or module_scope_candidates(py_mod, all_vb_files)
            if member == "__init__":
                allowed_kinds = {"sub"}
        else:
            expected_names = normalize_expected_member(py_sym)
            scoped_files = module_scope_candidates(py_mod, all_vb_files)

        inferred = find_unique_decl(expected_names, scoped_files, by_file, by_name, allowed_kinds)
        s["status_exact"] = "missing"
        if inferred is not None:
            inferred["inferred"] = True
            s["mapped_vb_symbols"] = [inferred]
            s["status_refined"] = "implemented-inferred"
            refined_implemented += 1
        else:
            s["status_refined"] = "missing"

    total = len(symbols)
    refined_missing = total - refined_implemented

    by_kind_refined: Dict[str, dict] = {}
    for k in sorted({s["kind"] for s in symbols}):
        sub = [s for s in symbols if s["kind"] == k]
        imp = sum(1 for s in sub if s["status_refined"].startswith("implemented"))
        by_kind_refined[k] = {"total": len(sub), "implemented": imp, "missing": len(sub) - imp}

    data["summary"] = {
        "total_symbols": total,
        "exact_implemented": exact_implemented,
        "refined_implemented": refined_implemented,
        "refined_missing": refined_missing,
        "by_kind_refined": by_kind_refined,
        "events_note": "Python AST scan found no event symbols (Python has no native event declaration construct).",
        "status_derivation": {
            "implemented-exact": "At least one VB mapping entry found for exact Python symbol key '<python module path>::<python symbol>'.",
            "implemented-inferred": "No exact key match, but a unique declaration was inferred from normalized symbol name and scoped VB file matching.",
            "missing": "No exact or inferred VB mapping found.",
        },
    }

    data["symbols"] = symbols
    json_path.write_text(json.dumps(data, indent=2), encoding="utf-8")
    generate_md(data, md_path)

    print(f"total={total}")
    print(f"exact_implemented={exact_implemented}")
    print(f"refined_implemented={refined_implemented}")
    print(f"refined_missing={refined_missing}")


if __name__ == "__main__":
    main()

