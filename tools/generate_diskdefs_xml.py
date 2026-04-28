#!/usr/bin/env python3
"""Convert Python diskdefs CFG files to XML resources."""

from __future__ import annotations

import re
from pathlib import Path
import xml.etree.ElementTree as ET


REPO_ROOT = Path(__file__).resolve().parents[1]
SRC_DIR = REPO_ROOT / "python_source" / "src" / "greaseweazle" / "data"
DST_DIR = REPO_ROOT / "src" / "Greaseweazle" / "data"


def strip_comment(line: str) -> str:
    idx = line.find("#")
    if idx >= 0:
        line = line[:idx]
    return line.strip()


def split_line_and_comment(line: str) -> tuple[str, str]:
    idx = line.find("#")
    if idx < 0:
        return line.rstrip(), ""
    return line[:idx].rstrip(), line[idx + 1 :].strip()


def sanitize_comment(text: str) -> str:
    safe = re.sub(r"^#+\s*", "", text.strip())
    safe = safe.replace("--", "- -")
    if safe.endswith("-"):
        safe = safe + " "
    return safe


def add_comment(parent: ET.Element | None, text: str) -> None:
    if parent is None:
        return
    cleaned = sanitize_comment(text)
    if cleaned:
        parent.append(ET.Comment(cleaned))


def convert_cfg_file(path: Path) -> ET.ElementTree:
    lines = path.read_text(encoding="utf-8").splitlines()
    root = ET.Element("diskdefs")
    mode = "outer"
    disk_node = None
    track_node = None

    for raw in lines:
        code_part, comment_part = split_line_and_comment(raw)

        pref = re.match(r"^\s*#\s*prefix:\s*([^\s]+)\s*$", raw)
        if pref is not None:
            root.set("prefix", pref.group(1))
            add_comment(root, comment_part or raw.lstrip()[1:].strip())
            continue

        line = code_part.strip()
        if not line:
            target = root if mode == "outer" else track_node if mode == "track" else disk_node
            add_comment(target, comment_part)
            continue

        if mode == "outer":
            m = re.match(r"^disk\s+([\w,.-]+)$", line, re.IGNORECASE)
            if m:
                disk_node = ET.SubElement(root, "disk", {"name": m.group(1)})
                add_comment(root, comment_part)
                mode = "disk"
                continue
            m = re.match(r'^import\s+([\w,.-]*)\s*"([^"]+)"$', line, re.IGNORECASE)
            if m:
                import_file = m.group(2)
                if import_file.endswith(".cfg"):
                    import_file = import_file[:-4] + ".xml"
                ET.SubElement(
                    root,
                    "import",
                    {"prefix": m.group(1), "file": import_file},
                )
                add_comment(root, comment_part)
                continue
            add_comment(root, comment_part)
            continue

        if mode == "disk":
            if line.lower() == "end":
                add_comment(disk_node, comment_part)
                mode = "outer"
                disk_node = None
                continue
            m = re.match(r"^tracks\s+([0-9,.*-]+)\s+([\w,.-]+)$", line, re.IGNORECASE)
            if m:
                track_node = ET.SubElement(
                    disk_node,
                    "tracks",
                    {"spec": m.group(1), "format": m.group(2)},
                )
                add_comment(disk_node, comment_part)
                mode = "track"
                continue
            m = re.match(r"^([a-zA-Z0-9:,._-]+)\s*=\s*([a-zA-Z0-9:,._*-]+)$", line)
            if m:
                ET.SubElement(disk_node, "option", {"key": m.group(1), "value": m.group(2)})
                add_comment(disk_node, comment_part)
                continue
            add_comment(disk_node, comment_part)
            continue

        if mode == "track":
            if line.lower() == "end":
                add_comment(track_node, comment_part)
                mode = "disk"
                track_node = None
                continue
            m = re.match(r"^([a-zA-Z0-9:,._-]+)\s*=\s*([a-zA-Z0-9:,._*-]+)$", line)
            if m:
                ET.SubElement(track_node, "option", {"key": m.group(1), "value": m.group(2)})
                add_comment(track_node, comment_part)
                continue
            add_comment(track_node, comment_part)

    return ET.ElementTree(root)


def indent(elem: ET.Element, level: int = 0) -> None:
    i = "\n" + level * "  "
    if len(elem):
        if not elem.text or not elem.text.strip():
            elem.text = i + "  "
        for child in elem:
            indent(child, level + 1)
        if not child.tail or not child.tail.strip():
            child.tail = i
    if level and (not elem.tail or not elem.tail.strip()):
        elem.tail = i


def main() -> None:
    DST_DIR.mkdir(parents=True, exist_ok=True)
    for cfg in sorted(SRC_DIR.glob("diskdefs*.cfg")):
        tree = convert_cfg_file(cfg)
        indent(tree.getroot())
        out = DST_DIR / (cfg.stem + ".xml")
        tree.write(out, encoding="utf-8", xml_declaration=True)
    print(f"Wrote XML files to {DST_DIR}")


if __name__ == "__main__":
    main()
