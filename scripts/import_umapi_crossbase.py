#!/usr/bin/env python3
"""Import the umapi.ru cross-reference CSV into shop_docpart_articles_analogs_list.

Usage (on the production box or any host with mysql client + DB access):

    python3 scripts/import_umapi_crossbase.py \\
        --csv /path/to/cross-base.csv \\
        --database auto247 \\
        [--mysql "mysql -u root"] \\
        [--chunk-lines 5000000] \\
        [--keep-work]

CSV columns: ART_NUMBER,ART_BRAND,CROSS_NUMBER,CROSS_BRAND
Table mapping: article/_search, manufacturer_article, analog/_search, manufacturer_analog.

Streams the CSV, normalizes each article (same rule as
StorefrontPhpCatalogueDemand.NormalizeArticle: strip [\\s\\-_/`'"\\\\.,#\\r\\n\\t],
uppercase), writes chunked TSV files, then LOAD DATA LOCAL INFILE ... IGNORE into the
analog table — the UNIQUE key on
(article, manufacturer_article, analog, manufacturer_analog) dedups re-runs, so the
import is repeatable when umapi ships refreshed files.
"""

from __future__ import annotations

import argparse
import csv
import os
import re
import subprocess
import sys
import tempfile

NORMALIZE_STRIP = re.compile(r"[\s\-_/`'\"\\.,#\r\n\t]")


def norm(value: str) -> str:
    return NORMALIZE_STRIP.sub("", value or "").upper()[:64]


def mysql_cmd(base: str, database: str, sql: str) -> None:
    proc = subprocess.run(
        f"{base} --local-infile=1 {database}",
        shell=True,
        input=sql,
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        raise RuntimeError(f"mysql failed: {proc.stderr.strip()[:400]}")
    if proc.stdout.strip():
        print(proc.stdout.strip()[-400:])


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--csv", required=True, help="Path to the umapi cross-base CSV")
    parser.add_argument("--database", required=True, help="Tenant database (e.g. auto247, docpart)")
    parser.add_argument("--mysql", default="mysql", help="mysql client prefix, e.g. 'mysql -u root -pPASS'")
    parser.add_argument("--chunk-lines", type=int, default=5_000_000, help="Rows per LOAD DATA batch")
    parser.add_argument("--keep-work", action="store_true", help="Keep intermediate TSV files")
    args = parser.parse_args()

    if not os.path.isfile(args.csv):
        print(f"csv not found: {args.csv}", file=sys.stderr)
        return 1

    mysql = args.mysql
    total = 0
    chunk_files: list[str] = []

    with tempfile.TemporaryDirectory(prefix="umapi_cross_") as workdir:
        # 1) Stream CSV -> normalized TSV chunks
        with open(args.csv, newline="", encoding="utf-8-sig") as src:
            reader = csv.reader(src)
            header = next(reader, [])
            idx = {name.strip().upper(): i for i, name in enumerate(header)}
            try:
                c_art = idx["ART_NUMBER"]
                c_art_brand = idx["ART_BRAND"]
                c_cross = idx["CROSS_NUMBER"]
                c_cross_brand = idx["CROSS_BRAND"]
            except KeyError as missing:
                print(f"CSV missing column {missing}; header={header}", file=sys.stderr)
                return 1

            chunk_no = 0
            out = None
            count_in_chunk = 0

            def open_chunk() -> object:
                nonlocal chunk_no, count_in_chunk
                path = os.path.join(workdir, f"chunk_{chunk_no:04d}.tsv")
                chunk_files.append(path)
                chunk_no += 1
                count_in_chunk = 0
                return open(path, "w", encoding="utf-8", newline="")

            for row in reader:
                if len(row) <= max(c_art, c_art_brand, c_cross, c_cross_brand):
                    continue
                art = row[c_art].strip()
                cross = row[c_cross].strip()
                if not art or not cross:
                    continue
                if out is None or count_in_chunk >= args.chunk_lines:
                    if out is not None:
                        out.close()
                    out = open_chunk()
                mfr = row[c_art_brand].strip()
                mfr_cross = row[c_cross_brand].strip()
                out.write("\t".join([art, norm(art), mfr, cross, norm(cross), mfr_cross]) + "\n")
                count_in_chunk += 1
                total += 1
            if out is not None:
                out.close()

        print(f"normalized {total:,} rows into {len(chunk_files)} chunk(s)")

        # 2) LOAD DATA each chunk (IGNORE => re-runs are deduped by the UNIQUE key)
        for i, path in enumerate(chunk_files, 1):
            esc = path.replace("\\", "\\\\").replace("'", "\\'")
            sql = (
                "LOAD DATA LOCAL INFILE '" + esc + "' INTO TABLE `shop_docpart_articles_analogs_list` "
                "FIELDS TERMINATED BY '\\t' LINES TERMINATED BY '\\n' "
                "(`article`, `article_search`, `manufacturer_article`, `analog`, `analog_search`, `manufacturer_analog`)"
            )
            mysql_cmd(mysql, args.database, sql)
            print(f"loaded chunk {i}/{len(chunk_files)}")
            if not args.keep_work:
                os.remove(path)

        # 3) Report
        mysql_cmd(
            mysql,
            args.database,
            "SELECT COUNT(*) AS total_rows FROM `shop_docpart_articles_analogs_list`",
        )
        print(f"done: {total:,} source rows processed into {args.database}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
