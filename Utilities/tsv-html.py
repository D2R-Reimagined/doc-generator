#!/usr/bin/env python3
# python tsv-html.py input/path/item.txt output/path/item.html --h2 optional header row --h3 optional header row

#This is intended to simplify the process of converting TSV files to HTML table snippets for inclusion in the wiki.

import csv
import html
import argparse
from pathlib import Path


def tsv_to_html_snippet(
    input_path: Path,
    output_path: Path,
    h2_title: str | None = None,
    h3_title: str | None = None,
):
    # Read TSV
    with input_path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.reader(f, delimiter="\t")
        rows = list(reader)

    if not rows:
        raise ValueError("Input TSV file is empty.")

    headers = rows[0]
    data_rows = rows[1:]

    lines: list[str] = []

    # Optional H2 / H3 headings
    if h2_title:
        lines.append(f"<h2>{html.escape(h2_title)}</h2>")
    if h3_title:
        lines.append(f"<h3>{html.escape(h3_title)}</h3>")

    # Start figure/table
    lines.append('<figure class="table">')
    lines.append("  <table>")
    lines.append("    <thead>")
    lines.append("      <tr>")

    # Header row: <th><mark class="pen-green">Name</mark></th>
    for h in headers:
        text = html.escape(h)
        lines.append(
            f'        <th><mark class="pen-green">{text}</mark></th>'
        )

    lines.append("      </tr>")
    lines.append("    </thead>")
    lines.append("    <tbody>")

    # Body rows
    for row in data_rows:
        # Pad row to same length as headers
        padded = row + [""] * (len(headers) - len(row))
        lines.append("      <tr>")

        for idx, cell in enumerate(padded):
            # Convert empty string to &nbsp;
            cell_text = "&nbsp;" if cell == "" else html.escape(cell)

            # First column as <th>, rest as <td>
            if idx == 0:
                lines.append(f"        <th>{cell_text}</th>")
            else:
                lines.append(f"        <td>{cell_text}</td>")

        lines.append("      </tr>")

    lines.append("    </tbody>")
    lines.append("  </table>")
    lines.append("</figure>")

    # Join and write
    html_snippet = "\n".join(lines)
    output_path.write_text(html_snippet, encoding="utf-8")
    print(f"Wrote HTML snippet to {output_path}")


def main():
    parser = argparse.ArgumentParser(
        description="Convert a UTF-8 TSV file with headers to a HTML table snippet."
    )
    parser.add_argument("input_tsv", help="Path to input .tsv file")
    parser.add_argument("output_html", help="Path to output .html file")
    parser.add_argument("--h2", help="Optional <h2> heading text", default=None)
    parser.add_argument("--h3", help="Optional <h3> heading text", default=None)
    args = parser.parse_args()

    tsv_to_html_snippet(
        Path(args.input_tsv),
        Path(args.output_html),
        h2_title=args.h2,
        h3_title=args.h3,
    )


if __name__ == "__main__":
    main()
