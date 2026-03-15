"""
Compare UTF-8 no-BOM .txt files (TSV tables) between two directories
and output the differences.

Usage:
    python compare_tsv.py [new_dir] [old_dir] [--output FILE]

Defaults to the Warlock Patch CASC directories if no arguments are given.
"""

import os
import sys
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


def main():
    parser = argparse.ArgumentParser(description="Compare TSV .txt files between two directories.")
    parser.add_argument("new_dir", nargs="?", default=DEFAULT_NEW,
                        help="Path to the NEW directory (default: data\\global\\excel)")
    parser.add_argument("old_dir", nargs="?", default=DEFAULT_OLD,
                        help="Path to the OLD directory (default: data_old\\global\\excel)")
    parser.add_argument("--output", "-o", default=None,
                        help="Output file path (default: print to console)")
    args = parser.parse_args()

    new_dir = Path(args.new_dir)
    old_dir = Path(args.old_dir)

    if not new_dir.exists():
        print(f"ERROR: New directory not found: {new_dir}")
        sys.exit(1)
    if not old_dir.exists():
        print(f"ERROR: Old directory not found: {old_dir}")
        sys.exit(1)

    new_files = {f.name for f in new_dir.glob("*.txt")}
    old_files = {f.name for f in old_dir.glob("*.txt")}

    all_files = sorted(new_files | old_files)
    only_in_new = sorted(new_files - old_files)
    only_in_old = sorted(old_files - new_files)
    common_files = sorted(new_files & old_files)

    output_lines = []
    output_lines.append("=" * 70)
    output_lines.append("TSV File Comparison Report")
    output_lines.append(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    output_lines.append(f"NEW: {new_dir}")
    output_lines.append(f"OLD: {old_dir}")
    output_lines.append("=" * 70)
    output_lines.append("")

    # Files only in one directory
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

    # Compare common files
    identical_count = 0
    diff_count = 0

    output_lines.append("-" * 70)
    output_lines.append("FILE-BY-FILE DIFFERENCES")
    output_lines.append("-" * 70)
    output_lines.append("")

    for filename in common_files:
        new_path = new_dir / filename
        old_path = old_dir / filename

        # Quick binary comparison first
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
            diffs = compare_files(str(new_path), str(old_path))
            output_lines.extend(diffs)
        except Exception as e:
            output_lines.append(f"  ERROR comparing: {e}")

        output_lines.append("")

    # Summary
    output_lines.append("=" * 70)
    output_lines.append("SUMMARY")
    output_lines.append("=" * 70)
    output_lines.append(f"  Total .txt files examined: {len(all_files)}")
    output_lines.append(f"  Files only in NEW: {len(only_in_new)}")
    output_lines.append(f"  Files only in OLD: {len(only_in_old)}")
    output_lines.append(f"  Common files - identical: {identical_count}")
    output_lines.append(f"  Common files - different: {diff_count}")

    report = "\n".join(output_lines)

    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"Report written to: {args.output}")
    else:
        print(report)


if __name__ == "__main__":
    main()
