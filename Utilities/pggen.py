#!/usr/bin/env python3
"""
TSV (UTF-8 no BOM) → grouped JSON

Input TSV has headers including: name, * Description, itype1..itype7, group, ...
We group by the `group` column and collect row items with their `description` and any non-empty
`itype1`..`itype7` columns preserved (empty itype columns are omitted from the JSON).

Deduplication: within each group, items are deduped by description using trim + case-insensitive
comparison. The first occurrence is kept and later duplicates are dropped.

Configured for:
  Input:  C:\\z_GitHub\\00_DocTest\\src_2_1_2_gen\\MagicPrefix.txt
  Output: C:\\z_GitHub\\00_DocTest\\json\\PropertyGroups.json

Adjust INPUT_PATH if your file name/path differs.

Used to create initial propertygroups.json from magic prefix/suffix TSV sources
Leaving for reference, but further updates of that file should be done by hand when adding new properties unless a completely overhaul of them is needed.
"""

import csv
import json
from collections import OrderedDict
from pathlib import Path

# Paths (edit INPUT_PATH if your file path differs)
INPUT_PATH = r"C:\\z_GitHub\\00_DocTest\\src_2_1_2_gen\\MagicPrefix.txt"
OUTPUT_PATH = r"C:\\z_GitHub\\00_DocTest\\PropertyGroups.json"

# Column names (case-insensitive match against headers)
GROUP_COLUMN = "group"
DESCRIPTION_COLUMN = "* Description"


def tsv_to_grouped_json(input_path: str, output_path: str,
                        group_col: str, desc_col: str) -> None:
    in_path = Path(input_path)
    if not in_path.exists():
        raise FileNotFoundError(f"Input file not found: {in_path}")

    # utf-8-sig gracefully handles both no-BOM and any accidental BOM
    with in_path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter="\t")
        if not reader.fieldnames:
            raise ValueError("No header row detected in TSV")

        # Normalize headers for case-insensitive lookup, preserving originals
        original_headers = [h.strip() for h in reader.fieldnames]
        lower_map = {h.lower(): h for h in original_headers}

        def resolve(col_name: str) -> str:
            key = col_name.strip().lower()
            if key not in lower_map:
                raise ValueError(
                    f"Column '{col_name}' not found. Available headers: "
                    + ", ".join(original_headers)
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

        # group -> list of item dicts: { description: str, itype1..itype7?: str }
        grouped = OrderedDict()
        # Track per-group seen description keys for dedupe (lowercased)
        seen_desc_by_group = {}

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
                continue
            group_seen.add(norm_desc_key)

            # Build item payload for this row
            item = {"description": desc_val}

            # Include only non-empty itype columns; omit empties as requested
            for key in itype_keys:
                v = (row.get(key, "") or "").strip()
                if v:
                    item[key] = v

            grouped.setdefault(group_out, []).append(item)

    # Build the requested shape: list of objects, one per group
    output = [
        {"group": group, "items": items}
        for group, items in grouped.items()
    ]

    out_path = Path(output_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="") as out:
        json.dump(output, out, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    tsv_to_grouped_json(INPUT_PATH, OUTPUT_PATH, GROUP_COLUMN, DESCRIPTION_COLUMN)