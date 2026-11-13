#!/usr/bin/env python3
"""Import a supplier PDF catalog into the shared supplier-catalog store.

Per catalog it writes into <out-dir>/<id>/:
  catalog.json  — metadata + per-page index
  <file>.pdf    — the source PDF

Usage: import_supplier_catalog.py <pdf> <out-dir> [--supplier NAME] [--id ID]
"""
import argparse
import json
import os
import re
import subprocess
import sys
import unicodedata

PART_NO = re.compile(r"^[A-Z0-9][A-Z0-9\-\./]{4,14}$")
STOP = {
    "CATALOG", "CATALOGUE", "SERIES", "MODEL", "MODELS", "APPLICATION",
    "ENGINE", "YEAR", "FRONT", "REAR", "RIGHT", "LEFT", "UPPER", "LOWER",
    "SUPPLIER", "PRODUCT", "PRODUCTS", "GROUP", "GROUPS", "INDEX",
    "PAGE", "PART", "PARTS", "NUMBER", "NUMBERS", "TYPE", "TYPES",
    "JAPAN", "EUROPE", "KOREA", "CHINA", "AMERICA", "GENERAL", "MOTORS",
    "COMPRESSION", "HEIGHT", "LENGTH", "WIDTH", "INNER", "OUTER",
    "MANUAL", "AUTO", "DIESEL", "PETROL", "HYBRID", "ELECTRIC",
}
MAKES = [
    "TOYOTA", "LEXUS", "NISSAN", "INFINITI", "HONDA", "MAZDA", "MITSUBISHI",
    "SUBARU", "SUZUKI", "DAIHATSU", "ISUZU", "HINO", "UD", "FUSO", "HYUNDAI",
    "KIA", "SSANGYONG", "DAEWOO", "SAMSUNG", "PROTON", "PERODUA", "VOLKSWAGEN",
    "AUDI", "BMW", "MERCEDES", "BENZ", "OPEL", "RENAULT", "PEUGEOT", "CITROEN",
    "FIAT", "ALFA", "FORD", "CHEVROLET", "CHRYSLER", "JEEP", "DODGE", "GM",
    "VOLVO", "SAAB", "LAND", "ROVER", "JAGUAR", "PORSCHE", "SCANIA", "MAN",
    "DAF", "IVECO", "CATERPILLAR", "KOMATSU", "KUBOTA", "YANMAR", "BYD",
    "CHERY", "GEELY", "GREATWALL", "HAVAL", "MG", "JAC", "FOTON", "JCB",
    "CASE", "JOHNDEERE", "MASSEY", "KOBELCO", "HITACHI", "SUMITOMO",
]
MAKE_ALTS = {"VW": "VOLKSWAGEN", "MERC": "MERCEDES", "LANDROVER": "LAND ROVER"}
CAT_NO = re.compile(r"Cat\.?\s*No\.?\s*[:.]?\s*([0-9A-Z\-]+)\s*(Ver\.?\s*[0-9A-Z.]+)?", re.I)
VOL = re.compile(r"\bVol\.?\s*([0-9]+)\b", re.I)
YEAR_RE = re.compile(r"\b(19|20)\d{2}\b")


def clean(tok):
    t = unicodedata.normalize("NFKD", tok).strip().upper()
    t = t.strip(".,;:()[]<>*'\"!?")
    return t


def clean_tokens(text):
    out = []
    for raw in re.split(r"[\s\u00a0]+", text):
        t = clean(raw)
        if t:
            out.append(t)
    return out


def is_part_no(t):
    if t in STOP:
        return False
    if t.isdigit():
        return 5 <= len(t) <= 8
    if not PART_NO.match(t):
        return False
    # Plain words (ABSORBER, COMFORT) are not part numbers — require a digit
    # or a separator (-, /, .) that catalog numbers carry.
    return any(c.isdigit() for c in t) or any(c in "-/." for c in t)


def detect_meta(pdf_text_first, pdf_text_last, filename, supplier):
    cat_no, ver, edition = "", "", ""
    m = CAT_NO.search(pdf_text_last)
    if m:
        cat_no = m.group(1)
        ver = (m.group(2) or "").replace(" ", "")
    vm = VOL.search(pdf_text_first) or VOL.search(filename)
    if vm and not cat_no:
        cat_no = "Vol. " + vm.group(1)
    ym = YEAR_RE.search(filename)
    if not ym:
        ym = YEAR_RE.search(pdf_text_first)
    if ym:
        edition = ym.group(0)
    return cat_no, ver, edition


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("pdf")
    ap.add_argument("out_dir")
    ap.add_argument("--supplier", default="")
    ap.add_argument("--id", default="")
    a = ap.parse_args()

    base = os.path.basename(a.pdf)
    cid = a.id or re.sub(r"[^a-z0-9]+", "-", os.path.splitext(base)[0].lower()).strip("-")
    if not cid:
        sys.exit("catalog id resolved empty — pass --id explicitly (filename has no latin chars)")
    supplier = a.supplier or re.split(r"[_\-\s]", os.path.splitext(base)[0])[0].upper()
    dest = os.path.join(a.out_dir, cid)
    tmp = dest + ".importing"
    # Build the whole catalog in a temp dir first; only swap into place once
    # the import succeeded so a bad PDF never destroys the published one.
    subprocess.run(["rm", "-rf", tmp], check=True)
    os.makedirs(tmp, exist_ok=True)
    shutil_pdf = os.path.join(tmp, "catalog.pdf")
    subprocess.run(["cp", a.pdf, shutil_pdf], check=True)

    n_pages = int(re.search(r"Pages:\s*(\d+)",
                            subprocess.run(["pdfinfo", a.pdf], capture_output=True, text=True).stdout).group(1))
    all_text_pages = []
    for start in range(1, n_pages + 1, 50):
        end = min(start + 49, n_pages)
        txt = subprocess.run(
            ["pdftotext", "-f", str(start), "-l", str(end), a.pdf, "-"],
            capture_output=True, text=True).stdout
        parts = txt.split("\f")
        if parts and parts[-1].strip() == "":
            parts.pop()  # trailing form-feed of the last page
        all_text_pages.extend(parts)
    all_text_pages = all_text_pages[:n_pages]

    cat_no, ver, edition = detect_meta(
        all_text_pages[0] + (all_text_pages[1] if len(all_text_pages) > 1 else ""),
        all_text_pages[-1] + (all_text_pages[-2] if len(all_text_pages) > 1 else ""),
        base, supplier)

    pages = []
    for i, txt in enumerate(all_text_pages, start=1):
        refs, oems, makes = [], [], set()
        for t in clean_tokens(txt):
            if t in MAKE_ALTS:
                makes.add(MAKE_ALTS[t])
            elif t in MAKES:
                makes.add(t)
            elif is_part_no(t):
                refs.append(t)
        for m in re.finditer(r"(?:O\.?\s?E\.?\s?M\.?|OEN|O\.E\.)\s*(?:No\.?|N[°ºo]?\.?)?\s*[:\-]?\s*([A-Z0-9\-/\.]{5,20})",
                             txt, re.I):
            oems.append(clean(m.group(1)))
        pages.append({
            "n": i,
            "refs": sorted(set(refs)),
            "oems": sorted(set(oems)),
            "makes": sorted(makes),
            "excerpt": re.sub(r"\s+", " ", txt).strip()[:400],
        })

    meta = {
        "id": cid,
        "supplier": supplier,
        "title": os.path.splitext(base)[0].replace("_", " "),
        "file": f"{cid}/catalog.pdf",
        "pages": n_pages,
        "catNo": cat_no,
        "version": ver,
        "edition": edition,
        "itemCount": sum(len(p["refs"]) for p in pages),
        # Single-version policy: re-importing a supplier's newer edition into
        # the same <id> replaces the whole directory — only the latest
        # catalog is ever kept. To add a new brand, pick a new --id.
        "imported": __import__("datetime").datetime.now(
            __import__("datetime").timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
    }
    with open(os.path.join(tmp, "catalog.json"), "w") as f:
        json.dump({"meta": meta, "pages": pages}, f, ensure_ascii=False)
    # Best-effort cover thumbnail (first page) for the search-bar results.
    try:
        subprocess.run(
            ["pdftoppm", "-png", "-f", "1", "-singlefile", "-scale-to", "320",
             a.pdf, os.path.join(tmp, "cover")],
            check=False, capture_output=True, timeout=60)
    except Exception:
        pass
    subprocess.run(["rm", "-rf", dest], check=True)
    os.replace(tmp, dest)
    print(json.dumps(meta))


if __name__ == "__main__":
    main()
