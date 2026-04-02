"""
Compare UTF-8 no-BOM .txt files (TSV tables) and .json files between two
directory trees and output the differences.

Usage:
    python compare_tsv.py [new_dir] [old_dir] [--output FILE]

Defaults to the Warlock Patch CASC directories if no arguments are given.
The script compares:
  - global/excel   (.txt TSV files)
  - global/ui/layouts (.json files)
  - hd/global/excel   (.json files)
"""

import os
import sys
import re
import json
import argparse
import csv
from pathlib import Path
from datetime import datetime


DEFAULT_NEW = r"C:\Users\lemin\Downloads\z_D2R Tools\zz_Warlock Patch\CASC Files\data\global\excel"
DEFAULT_OLD = r"C:\Users\lemin\Downloads\z_D2R Tools\zz_Warlock Patch\CASC Files\data_old\global\excel"


def read_tsv(filepath):
    """Read a TSV file and return header + list of row dicts, plus raw lines."""
    with open(filepath, "r", encoding="utf-8-sig") as f:
        content = f.read()
    lines = content.rstrip("\n").split("\n")
    if not lines:
        return [], [], []
    header = lines[0].split("\t")
    rows = []
    for line in lines[1:]:
        rows.append(line.split("\t"))
    return header, rows, lines


def compare_files(new_path, old_path):
    """Compare two TSV files and return a list of difference descriptions."""
    diffs = []

    old_header, old_rows, old_lines = read_tsv(old_path)
    new_header, new_rows, new_lines = read_tsv(new_path)

    # --- Header / column changes ---
    old_cols = set(old_header)
    new_cols = set(new_header)
    added_cols = new_cols - old_cols
    removed_cols = old_cols - new_cols

    if added_cols:
        diffs.append(f"  Columns ADDED: {', '.join(sorted(added_cols))}")
    if removed_cols:
        diffs.append(f"  Columns REMOVED: {', '.join(sorted(removed_cols))}")

    # Reordered columns (same set but different order)
    if old_header != new_header and not added_cols and not removed_cols:
        diffs.append(f"  Columns REORDERED")

    # --- Row count ---
    if len(old_rows) != len(new_rows):
        diffs.append(f"  Row count changed: {len(old_rows)} -> {len(new_rows)}")

    # --- Determine a key column for matching rows ---
    # Try common key column names, fall back to first column
    common_keys = ["code", "Key", "key", "Name", "name", "Skill", "skill", "Id", "id",
                   "Rune Name", "Patch Release"]
    key_col_idx_old = 0
    key_col_idx_new = 0
    key_col_name = old_header[0] if old_header else "col0"

    # Find columns common to both headers for comparison
    common_header = [c for c in new_header if c in old_cols]

    for kc in common_keys:
        if kc in old_cols and kc in new_cols:
            key_col_name = kc
            key_col_idx_old = old_header.index(kc)
            key_col_idx_new = new_header.index(kc)
            break

    # Build lookup dicts: key_value -> list of rows
    def build_row_map(header, rows, key_idx):
        row_map = {}
        for i, row in enumerate(rows):
            key_val = row[key_idx] if key_idx < len(row) else f"__row{i}"
            if key_val not in row_map:
                row_map[key_val] = []
            row_map[key_val].append((i, dict(zip(header, row + [""] * max(0, len(header) - len(row))))))
        return row_map

    old_map = build_row_map(old_header, old_rows, key_col_idx_old)
    new_map = build_row_map(new_header, new_rows, key_col_idx_new)

    all_keys = sorted(set(list(old_map.keys()) + list(new_map.keys())),
                       key=lambda k: (k == "", k))

    added_rows = []
    removed_rows = []
    changed_rows = []

    for key_val in all_keys:
        if not key_val and not key_val.strip():
            continue  # skip blank key rows

        old_entries = old_map.get(key_val, [])
        new_entries = new_map.get(key_val, [])

        if not old_entries and new_entries:
            added_rows.append(key_val)
            continue
        if old_entries and not new_entries:
            removed_rows.append(key_val)
            continue

        # Compare first matching pair on common columns
        old_dict = old_entries[0][1]
        new_dict = new_entries[0][1]

        cell_changes = []
        for col in common_header:
            old_val = old_dict.get(col, "")
            new_val = new_dict.get(col, "")
            if old_val != new_val:
                cell_changes.append((col, old_val, new_val))

        if cell_changes:
            changed_rows.append((key_val, cell_changes))

    if added_rows:
        if len(added_rows) <= 20:
            diffs.append(f"  Rows ADDED ({len(added_rows)}): {', '.join(added_rows)}")
        else:
            diffs.append(f"  Rows ADDED ({len(added_rows)}): {', '.join(added_rows[:20])}... and {len(added_rows)-20} more")

    if removed_rows:
        if len(removed_rows) <= 20:
            diffs.append(f"  Rows REMOVED ({len(removed_rows)}): {', '.join(removed_rows)}")
        else:
            diffs.append(f"  Rows REMOVED ({len(removed_rows)}): {', '.join(removed_rows[:20])}... and {len(removed_rows)-20} more")

    if changed_rows:
        diffs.append(f"  Rows with cell changes ({len(changed_rows)}):")
        for key_val, changes in changed_rows:
            label = key_val if key_val.strip() else "(blank key)"
            diffs.append(f"    [{key_col_name}={label}]")
            for col, old_v, new_v in changes:
                old_display = old_v if old_v else "(empty)"
                new_display = new_v if new_v else "(empty)"
                diffs.append(f"      {col}: {old_display} -> {new_display}")

    return diffs


# ---------------------------------------------------------------------------
# JSON comparison helpers
# ---------------------------------------------------------------------------

def _flatten_json(obj, prefix=""):
    """Flatten a nested JSON object into a dict of dotted-path -> value."""
    items = {}
    if isinstance(obj, dict):
        for k, v in obj.items():
            new_key = f"{prefix}.{k}" if prefix else k
            items.update(_flatten_json(v, new_key))
    elif isinstance(obj, list):
        for i, v in enumerate(obj):
            new_key = f"{prefix}[{i}]"
            items.update(_flatten_json(v, new_key))
    else:
        items[prefix] = obj
    return items


def _strip_trailing_commas(text):
    """Remove trailing commas before } or ] so json.loads can parse the data."""
    return re.sub(r",\s*([}\]])", r"\1", text)


def _load_json_lenient(filepath):
    """Load a JSON file, tolerating trailing commas."""
    with open(filepath, "r", encoding="utf-8-sig") as f:
        raw = f.read()
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return json.loads(_strip_trailing_commas(raw))


def compare_json_files(new_path, old_path):
    """Compare two JSON files and return a list of difference descriptions."""
    diffs = []

    old_data = _load_json_lenient(old_path)
    new_data = _load_json_lenient(new_path)

    old_flat = _flatten_json(old_data)
    new_flat = _flatten_json(new_data)

    old_keys = set(old_flat.keys())
    new_keys = set(new_flat.keys())

    added_keys = sorted(new_keys - old_keys)
    removed_keys = sorted(old_keys - new_keys)
    common_keys = sorted(old_keys & new_keys)

    if added_keys:
        if len(added_keys) <= 20:
            diffs.append(f"  Keys ADDED ({len(added_keys)}): {', '.join(added_keys)}")
        else:
            diffs.append(f"  Keys ADDED ({len(added_keys)}): {', '.join(added_keys[:20])}... and {len(added_keys)-20} more")

    if removed_keys:
        if len(removed_keys) <= 20:
            diffs.append(f"  Keys REMOVED ({len(removed_keys)}): {', '.join(removed_keys)}")
        else:
            diffs.append(f"  Keys REMOVED ({len(removed_keys)}): {', '.join(removed_keys[:20])}... and {len(removed_keys)-20} more")

    changed = []
    for k in common_keys:
        if old_flat[k] != new_flat[k]:
            changed.append(k)

    if changed:
        diffs.append(f"  Values changed ({len(changed)}):")
        for k in changed:
            old_display = old_flat[k] if old_flat[k] not in (None, "") else "(empty)"
            new_display = new_flat[k] if new_flat[k] not in (None, "") else "(empty)"
            diffs.append(f"    {k}: {old_display} -> {new_display}")

    return diffs


# ---------------------------------------------------------------------------
# Generic directory comparison
# ---------------------------------------------------------------------------

def compare_directory_pair(new_dir, old_dir, glob_pattern, compare_func, section_title, output_lines):
    """Compare files matching *glob_pattern* between two directories.

    Appends results to *output_lines* and returns
    (total_files, only_new, only_old, identical, different) counts.
    """
    if not new_dir.exists() and not old_dir.exists():
        return 0, 0, 0, 0, 0

    new_files = {f.name for f in new_dir.glob(glob_pattern)} if new_dir.exists() else set()
    old_files = {f.name for f in old_dir.glob(glob_pattern)} if old_dir.exists() else set()

    all_files = sorted(new_files | old_files)
    only_in_new = sorted(new_files - old_files)
    only_in_old = sorted(old_files - new_files)
    common_files = sorted(new_files & old_files)

    output_lines.append("")
    output_lines.append("=" * 70)
    output_lines.append(section_title)
    output_lines.append(f"NEW: {new_dir}")
    output_lines.append(f"OLD: {old_dir}")
    output_lines.append("=" * 70)
    output_lines.append("")

    if only_in_new:
        output_lines.append(f"Files ONLY in NEW ({len(only_in_new)}):")
        for f in only_in_new:
            output_lines.append(f"  + {f}")
        output_lines.append("")

    if only_in_old:
        output_lines.append(f"Files ONLY in OLD ({len(only_in_old)}):")
        for f in only_in_old:
            output_lines.append(f"  - {f}")
        output_lines.append("")

    identical_count = 0
    diff_count = 0

    output_lines.append("-" * 70)
    output_lines.append("FILE-BY-FILE DIFFERENCES")
    output_lines.append("-" * 70)
    output_lines.append("")

    for filename in common_files:
        new_path = new_dir / filename
        old_path = old_dir / filename

        with open(new_path, "rb") as f:
            new_bytes = f.read()
        with open(old_path, "rb") as f:
            old_bytes = f.read()

        if new_bytes == old_bytes:
            identical_count += 1
            continue

        diff_count += 1
        output_lines.append(f">>> {filename}")

        try:
            diffs = compare_func(str(new_path), str(old_path))
            output_lines.extend(diffs)
        except Exception as e:
            output_lines.append(f"  ERROR comparing: {e}")

        output_lines.append("")

    return len(all_files), len(only_in_new), len(only_in_old), identical_count, diff_count


def main():
    parser = argparse.ArgumentParser(
        description="Compare TSV .txt and .json files between two CASC data directories.")
    parser.add_argument("new_dir", nargs="?", default=DEFAULT_NEW,
                        help="Path to the NEW global/excel directory (default: data\\global\\excel)")
    parser.add_argument("old_dir", nargs="?", default=DEFAULT_OLD,
                        help="Path to the OLD global/excel directory (default: data_old\\global\\excel)")
    script_dir = Path(__file__).resolve().parent
    default_output = str(script_dir / "compare_output.txt")
    parser.add_argument("--output", "-o", default=default_output,
                        help="Output file path (default: compare_output.txt next to this script)")
    args = parser.parse_args()

    new_dir = Path(args.new_dir)
    old_dir = Path(args.old_dir)

    if not new_dir.exists():
        print(f"ERROR: New directory not found: {new_dir}")
        sys.exit(1)
    if not old_dir.exists():
        print(f"ERROR: Old directory not found: {old_dir}")
        sys.exit(1)

    # Derive the base data roots from the global/excel paths so we can locate
    # the sibling folders (global/ui/layouts and hd/global/excel).
    # Expected structure: <root>/data/global/excel  ->  <root>/data
    new_data_root = new_dir.parent.parent  # …/data
    old_data_root = old_dir.parent.parent  # …/data_old

    # Define all directory pairs to compare
    dir_pairs = [
        # (new_dir, old_dir, glob, compare_func, section_title)
        (new_dir, old_dir, "*.txt", compare_files,
         "TSV File Comparison — global/excel"),
        (new_data_root / "global" / "ui" / "layouts",
         old_data_root / "global" / "ui" / "layouts",
         "*.json", compare_json_files,
         "JSON File Comparison — global/ui/layouts"),
        (new_data_root / "hd" / "global" / "excel",
         old_data_root / "hd" / "global" / "excel",
         "*.json", compare_json_files,
         "JSON File Comparison — hd/global/excel"),
    ]

    output_lines = []
    output_lines.append("=" * 70)
    output_lines.append("File Comparison Report")
    output_lines.append(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    output_lines.append("=" * 70)

    summary_rows = []

    for pair_new, pair_old, glob, func, title in dir_pairs:
        total, only_new, only_old, identical, different = compare_directory_pair(
            pair_new, pair_old, glob, func, title, output_lines)
        if total > 0:
            summary_rows.append((title, total, only_new, only_old, identical, different))

    # Summary
    output_lines.append("")
    output_lines.append("=" * 70)
    output_lines.append("SUMMARY")
    output_lines.append("=" * 70)
    for title, total, only_new, only_old, identical, different in summary_rows:
        output_lines.append(f"  [{title}]")
        output_lines.append(f"    Total files examined: {total}")
        output_lines.append(f"    Files only in NEW: {only_new}")
        output_lines.append(f"    Files only in OLD: {only_old}")
        output_lines.append(f"    Common files - identical: {identical}")
        output_lines.append(f"    Common files - different: {different}")

    report = "\n".join(output_lines)

    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"Report written to: {args.output}")
    else:
        print(report)


if __name__ == "__main__":
    main()
