#!/usr/bin/env python3
"""
TSV (UTF-8 no BOM) → grouped JSON (from 1 or more TSV inputs)

Each input TSV has headers including: name, * Description, itype1..itype7, group, ...
We group by the `group` column and collect row items with their `description`
and any non-empty `itype1`..`itype7` columns preserved (empty itype columns are
omitted from the JSON).

Deduplication: within each group, items are deduped by description using trim +
case-insensitive comparison. The first occurrence is kept and later duplicates
(from the same file or another file) are dropped.

Configured for:
  Inputs:
    C:\\z_GitHub\\00_DocTest\\src_2_1_2_gen\\MagicPrefix.txt
    C:\\z_GitHub\\00_DocTest\\src_2_1_2_gen\\MagicSuffix.txt
  Output:
    C:\\z_GitHub\\00_DocTest\\property_groups.json
"""

import csv
import json
from collections import OrderedDict
from pathlib import Path

# Paths (edit INPUT_PATHS/OUTPUT_PATH if your file paths differ)
INPUT_PATHS = [
    r"C:\\z_GitHub\\00_DocTest\\src_2_1_2_gen\\MagicPrefix.txt",
    r"C:\\z_GitHub\\00_DocTest\\src_2_1_2_gen\\MagicSuffix.txt",
]
OUTPUT_PATH = r"C:\\z_GitHub\\00_DocTest\\property_groups.json"

# Column names (case-insensitive match against headers)
GROUP_COLUMN = "group"
DESCRIPTION_COLUMN = "* Description"


def process_tsv_file(
    input_path: str,
    group_col: str,
    desc_col: str,
    grouped: OrderedDict,
    seen_desc_by_group: dict,
) -> None:
    """
    Read a single TSV file and merge its data into `grouped` / `seen_desc_by_group`.
    """
    in_path = Path(input_path)
    if not in_path.exists():
        raise FileNotFoundError(f"Input file not found: {in_path}")

    # utf-8-sig gracefully handles both no-BOM and any accidental BOM
    with in_path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter="\t")
        if not reader.fieldnames:
            raise ValueError(f"No header row detected in TSV: {in_path}")

        # Normalize headers for case-insensitive lookup, preserving originals
        original_headers = [h.strip() for h in reader.fieldnames]
        lower_map = {h.lower(): h for h in original_headers}

        def resolve(col_name: str) -> str:
            key = col_name.strip().lower()
            if key not in lower_map:
                raise ValueError(
                    f"Column '{col_name}' not found in {in_path}. "
                    f"Available headers: " + ", ".join(original_headers)
                )
            return lower_map[key]

        group_key = resolve(group_col)
        desc_key = resolve(desc_col)

        # Collect available itype column names (itype1..itype7) if present
        itype_keys = []
        for i in range(1, 8):
            k = f"itype{i}"
            if k in lower_map:
                itype_keys.append(lower_map[k])

        # Iterate rows in this file
        for row in reader:
            if row is None:
                continue
            group_val = (row.get(group_key, "") or "").strip()
            desc_val = (row.get(desc_key, "") or "").strip()

            # Skip rows missing either value
            if not group_val or not desc_val:
                continue

            # Keep types simple: try int group if numeric, else keep as string
            try:
                group_out = int(group_val)
            except ValueError:
                group_out = group_val

            # Dedupe within group by normalized description (trim done above, then lower)
            norm_desc_key = desc_val.lower()
            group_seen = seen_desc_by_group.setdefault(group_out, set())
            if norm_desc_key in group_seen:
                # Already seen (maybe in this file, maybe in a previous file)
                continue
            group_seen.add(norm_desc_key)

            # Build item payload and add to group
            item = {"description": desc_val}

            # Include only non-empty itype columns; omit empties
            for key in itype_keys:
                v = (row.get(key, "") or "").strip()
                if v:
                    item[key] = v

            grouped.setdefault(group_out, []).append(item)


def tsvs_to_grouped_json(
    input_paths: list[str],
    output_path: str,
    group_col: str,
    desc_col: str,
) -> None:
    grouped: OrderedDict = OrderedDict()
    seen_desc_by_group: dict = {}

    # Process each TSV and merge into grouped
    for p in input_paths:
        process_tsv_file(p, group_col, desc_col, grouped, seen_desc_by_group)

    # Build the final JSON shape
    output = [
        {"group": group, "items": items}
        for group, items in grouped.items()
    ]

    out_path = Path(output_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="") as out:
        json.dump(output, out, ensure_ascii=False, indent=2)

    total_items = sum(len(items) for items in grouped.values())
    print(f"Wrote {len(grouped)} groups / {total_items} items → {out_path}")


if __name__ == "__main__":
    tsvs_to_grouped_json(INPUT_PATHS, OUTPUT_PATH, GROUP_COLUMN, DESCRIPTION_COLUMN)
