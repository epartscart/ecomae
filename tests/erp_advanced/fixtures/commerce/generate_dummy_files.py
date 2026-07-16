#!/usr/bin/env python3
"""Generate dummy commerce Excel/CSV fixtures for S/P/L ingest tests."""
from __future__ import annotations

import csv
import zipfile
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parent

SALES_ROWS = [
    ["Brand", "Article", "Name", "Qty", "Price"],
    ["BOSCH", "OC47", "Oil filter", "2", "12.50"],
    ["BOSCH", "OC47", "Oil filter HQ", "3", "18.90"],  # higher → wins
    ["MANN", "W71945", "Oil filter Mann", "5", "22.00"],
    ["MANN", "W71945", "Oil filter Mann", "2", "19.50"],  # lower ignored for price
    ["NGK", "BKR6E", "Spark plug", "10", "4.75"],
    ["HELLA", "8GS009", "Glow plug", "0", "15.00"],
]

PURCHASE_ROWS = [
    ["Brand", "Article", "Name", "Qty", "Cost", "Supplier"],
    ["BOSCH", "OC47", "Oil filter", "20", "8.00", "ACME Parts"],
    ["BOSCH", "OC47", "Oil filter", "10", "9.50", "ACME Parts"],  # higher cost ignored
    ["BOSCH", "OC47", "Oil filter", "15", "7.50", "BETA Supply"],
    ["MANN", "W71945", "Oil filter Mann", "30", "11.00", "ACME Parts"],
    ["NGK", "BKR6E", "Spark plug", "100", "1.80", "BETA Supply"],
    ["HELLA", "8GS009", "Glow plug", "25", "9.20", "GAMMA Auto"],
]

INVENTORY_ROWS = [
    ["Brand", "Article", "Name", "Stock", "Cost"],
    ["BOSCH", "OC47", "Oil filter stock", "12", "9.00"],
    ["BOSCH", "OC47", "Oil filter stock", "8", "9.00"],  # qty summed
    ["MANN", "W71945", "Mann local", "40", "12.00"],
    ["NGK", "BKR6E", "Spark plug local", "200", "2.00"],
    ["VALEO", "VF123", "Cabin filter", "6", "14.50"],
]


def write_csv(path: Path, rows: list[list[str]]) -> None:
    with path.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.writer(fh)
        writer.writerows(rows)
    print(f"wrote {path.name} ({len(rows) - 1} data rows)")


def sheet_xml(rows: list[list[str]]) -> str:
    """Minimal SpreadsheetML worksheet."""
    lines = [
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">',
        "<sheetData>",
    ]
    for r_idx, row in enumerate(rows, start=1):
        lines.append(f'<row r="{r_idx}">')
        for c_idx, value in enumerate(row):
            col = ""
            n = c_idx
            while True:
                col = chr(ord("A") + (n % 26)) + col
                n = n // 26 - 1
                if n < 0:
                    break
            cell_ref = f"{col}{r_idx}"
            # treat numeric-looking as number except header row
            if r_idx > 1 and _is_number(value):
                lines.append(f'<c r="{cell_ref}"><v>{escape(value)}</v></c>')
            else:
                lines.append(
                    f'<c r="{cell_ref}" t="inlineStr"><is><t>{escape(value)}</t></is></c>'
                )
        lines.append("</row>")
    lines.extend(["</sheetData>", "</worksheet>"])
    return "\n".join(lines)


def _is_number(value: str) -> bool:
    try:
        float(value)
        return True
    except ValueError:
        return False


def write_xlsx(path: Path, rows: list[list[str]]) -> None:
    content_types = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
</Types>"""
    rels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>"""
    workbook = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets><sheet name="Data" sheetId="1" r:id="rId1"/></sheets>
</workbook>"""
    workbook_rels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
</Relationships>"""
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        zf.writestr("[Content_Types].xml", content_types)
        zf.writestr("_rels/.rels", rels)
        zf.writestr("xl/workbook.xml", workbook)
        zf.writestr("xl/_rels/workbook.xml.rels", workbook_rels)
        zf.writestr("xl/worksheets/sheet1.xml", sheet_xml(rows))
    print(f"wrote {path.name} ({len(rows) - 1} data rows)")


def main() -> None:
    write_csv(ROOT / "sales_dummy.csv", SALES_ROWS)
    write_csv(ROOT / "purchase_dummy.csv", PURCHASE_ROWS)
    write_csv(ROOT / "inventory_dummy.csv", INVENTORY_ROWS)
    write_xlsx(ROOT / "sales_dummy.xlsx", SALES_ROWS)
    write_xlsx(ROOT / "purchase_dummy.xlsx", PURCHASE_ROWS)
    write_xlsx(ROOT / "inventory_dummy.xlsx", INVENTORY_ROWS)
    print("done")


if __name__ == "__main__":
    main()
