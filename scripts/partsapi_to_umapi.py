#!/usr/bin/env python3
"""Translate partsapi.ru (TecDoc) cache rows into the umapi-shaped tables the
storefront already reads, so a partsapi subscription keeps the same drill-down
working: epc_umapi_manufacturers/models/modifications/brands + epc_umapi_cache.

Reads epc_partsapi_cache from --source-db, writes the umapi tables in
--target-db (may be the same DB). Idempotent REPLACE INTO — safe to re-run
as the partsapi cache grows.

Sections: PC -> passenger, CV -> commercial, Motorcycle -> motorbike.
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
import time

SAFE_ARG = re.compile(r"^[a-zA-Z0-9_\- .=/]*$")

SECTION = {"PC": "passenger", "CV": "commercial", "Motorcycle": "motorbike"}


def sql_escape(v):
    return (str(v) if v is not None else "").replace("\\", "\\\\").replace("'", "\\'")\
        .replace("\n", "\\n").replace("\r", "\\r").replace("\x00", "\\0").replace("\x1a", "\\Z")


class Db:
    def __init__(self, mysql, database):
        self.mysql, self.database = mysql, database

    def run(self, sql, out=False):
        proc = subprocess.run(
            f"{self.mysql} -N -s {self.database}", shell=True,
            input=sql.encode(), capture_output=True)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr.decode()[:300])
        return proc.stdout.decode() if out else ""

    def read_rows(self, sql):
        return self.run(sql, out=True)


def pick(row, *names):
    for n in names:
        for k, v in row.items():
            if k.lower() == n.lower() and v not in (None, ""):
                return str(v)
    return None


def data_rows(response_json):
    try:
        d = json.loads(response_json)
    except Exception:
        return []
    for _ in range(4):
        if isinstance(d, list):
            return [r for r in d if isinstance(r, dict)]
        if isinstance(d, dict) and isinstance(d.get("data"), (list, dict)):
            d = d["data"]
            continue
        return []
    return []


def umapi_cache_key(action, section, language, region, request_json):
    return hashlib.sha1(
        f"{action}|{section}|{language}|{region}|{request_json}".encode()).hexdigest()


def umapi_cache_write(db, action, section, request_json, items, language="en", region="WWW"):
    key = umapi_cache_key(action, section, language, region, request_json)
    body = json.dumps({"data": items}, ensure_ascii=False, separators=(",", ":"))
    now = int(time.time())
    db.run(
        "REPLACE INTO `epc_umapi_cache` (`cache_key`,`action`,`section`,`language`,`region`,"
        "`request_json`,`response_json`,`rows_count`,`http_status`,`last_sync`) VALUES ("
        f"'{key}','{sql_escape(action)}','{sql_escape(section)}','{language}','{region}',"
        f"'{sql_escape(request_json)}','{sql_escape(body)}',{len(items)},200,{now})")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source-db", required=True)
    ap.add_argument("--target-db", required=True)
    ap.add_argument("--mysql", default="mysql")
    args = ap.parse_args()
    for n, v in (("--source-db", args.source_db), ("--target-db", args.target_db), ("--mysql", args.mysql)):
        if not SAFE_ARG.match(v):
            print(f"refusing {n}: {v!r}", file=sys.stderr)
            return 1

    src = Db(args.mysql, args.source_db)
    dst = Db(args.mysql, args.target_db)
    now = int(time.time())

    for ddl in (
        "CREATE TABLE IF NOT EXISTS `epc_umapi_manufacturers` (`section` varchar(20) NOT NULL,`mfa_id` int NOT NULL,`manufacturer` varchar(255) NOT NULL,`manufacturer_ru` varchar(255) DEFAULT NULL,`type` varchar(255) DEFAULT NULL,`country` varchar(120) DEFAULT NULL,`popular` tinyint NOT NULL DEFAULT '0',`is_logo` tinyint NOT NULL DEFAULT '0',`raw_json` text,`updated_at` int NOT NULL DEFAULT '0',PRIMARY KEY (`section`,`mfa_id`),KEY `manufacturer` (`manufacturer`),KEY `popular` (`popular`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3",
        "CREATE TABLE IF NOT EXISTS `epc_umapi_models` (`section` varchar(20) NOT NULL,`mfa_id` int NOT NULL,`ms_id` int NOT NULL,`model_series` varchar(255) NOT NULL,`year_from` varchar(20) DEFAULT NULL,`year_to` varchar(20) DEFAULT NULL,`raw_json` text,`updated_at` int NOT NULL DEFAULT '0',PRIMARY KEY (`section`,`ms_id`),KEY `mfa_id` (`mfa_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3",
        "CREATE TABLE IF NOT EXISTS `epc_umapi_modifications` (`section` varchar(20) NOT NULL,`ms_id` int NOT NULL,`modification_id` int NOT NULL,`title` varchar(255) NOT NULL,`year_from` varchar(20) DEFAULT NULL,`year_to` varchar(20) DEFAULT NULL,`power_kw` varchar(50) DEFAULT NULL,`capacity_lt` varchar(50) DEFAULT NULL,`fuel_type` varchar(120) DEFAULT NULL,`raw_json` text,`updated_at` int NOT NULL DEFAULT '0',PRIMARY KEY (`section`,`modification_id`),KEY `ms_id` (`ms_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3",
        "CREATE TABLE IF NOT EXISTS `epc_umapi_brands` (`sup_id` int NOT NULL,`brand` varchar(255) NOT NULL,`full_name` varchar(255) DEFAULT NULL,`raw_json` text,`updated_at` int NOT NULL DEFAULT '0',PRIMARY KEY (`sup_id`),KEY `brand` (`brand`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3",
        "CREATE TABLE IF NOT EXISTS `epc_umapi_cache` (`cache_key` varchar(64) NOT NULL,`action` varchar(40) NOT NULL,`section` varchar(20) NOT NULL,`language` varchar(10) NOT NULL DEFAULT 'en',`region` varchar(10) NOT NULL DEFAULT 'WWW',`request_json` text,`response_json` mediumtext,`rows_count` int NOT NULL DEFAULT 0,`http_status` int NOT NULL DEFAULT 0,`last_sync` int NOT NULL DEFAULT 0,PRIMARY KEY (`cache_key`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3",
    ):
        dst.run(ddl)

    # Load every catalog row once.
    rows = src.read_rows(
        "SELECT `method`, `request_json`, `response_json` FROM `epc_partsapi_cache` WHERE `section`='catalog'")
    stats = {"makes": 0, "models": 0, "mods": 0, "brands": 0}
    brands = {}  # sup_id -> (brand, full)
    makes_by_section = {}
    for line in rows.splitlines():
        parts = line.split("\t")
        if len(parts) < 3:
            continue
        method, req, resp = parts[0], parts[1], parts[2]
        try:
            params = json.loads(req)
        except Exception:
            params = {}
        section = SECTION.get(params.get("carType") or params.get("carType".upper()) or "", "passenger")
        items = data_rows(resp)
        if method == "getMakes":
            out = []
            for r in items:
                mid, name = pick(r, "makeId", "MFA_ID"), pick(r, "makeName", "MFA_BRAND")
                if not mid or not name:
                    continue
                rec = {"MFA_ID": mid, "MANUFACTURER": name, "MANUFACTURER_RU": name,
                       "TYPE": section, "COUNTRY": "", "IS_LOGO": 1}
                out.append(rec)
                # partsapi carries no country/popular — preserve any values the
                # umapi data already set instead of blanking them on re-runs.
                dst.run(
                    "INSERT INTO `epc_umapi_manufacturers` (`section`,`mfa_id`,`manufacturer`,`manufacturer_ru`,`type`,`country`,`popular`,`is_logo`,`raw_json`,`updated_at`) VALUES ("
                    f"'{section}',{int(float(mid))},'{sql_escape(name)}','{sql_escape(name)}','{section}','',0,1,"
                    f"'{sql_escape(json.dumps(rec, ensure_ascii=False))}',{now})"
                    " ON DUPLICATE KEY UPDATE `manufacturer`=VALUES(`manufacturer`),`type`=VALUES(`type`),"
                    "`is_logo`=VALUES(`is_logo`),`raw_json`=VALUES(`raw_json`),`updated_at`=VALUES(`updated_at`)")
                stats["makes"] += 1
            if out:
                # Cache row is rebuilt from the table so it inherits preserved
                # country/popular values, sorted popular-first.
                lines = dst.read_rows(
                    "SELECT mfa_id,manufacturer,COALESCE(manufacturer_ru,manufacturer),COALESCE(country,''),"
                    "popular,is_logo FROM `epc_umapi_manufacturers` WHERE `section`='" + section
                    + "' ORDER BY popular DESC,manufacturer").splitlines()
                items = [
                    {"MFA_ID": m, "MANUFACTURER": n, "MANUFACTURER_RU": r, "TYPE": section,
                     "COUNTRY": c, "POPULAR": p, "IS_LOGO": l}
                    for m, n, r, c, p, l in (l.split("\t") for l in lines)
                ]
                reqj = json.dumps({"action": "manufacturers", "section": section}, separators=(",", ":"))
                umapi_cache_write(dst, "manufacturers", section, reqj, items)
        elif method == "getModels":
            mfa = params.get("makeId")
            out = []
            for r in items:
                msid = pick(r, "modelId", "MOD_ID", "MS_ID")
                name = pick(r, "modelName", "MOD_CDS_TEXT", "MS_NAME")
                yf = (pick(r, "yearsFrom") or "")[4:16]
                yt = (pick(r, "yearsTo") or "")[4:16]
                if not msid:
                    continue
                rec = {"MS_ID": msid, "MODEL_SERIES": name or "", "CI_FROM": yf, "CI_TO": yt}
                out.append(rec)
                dst.run(
                    "REPLACE INTO `epc_umapi_models` (`section`,`mfa_id`,`ms_id`,`model_series`,`year_from`,`year_to`,`raw_json`,`updated_at`) VALUES ("
                    f"'{section}',{int(float(mfa or 0))},{int(float(msid))},'{sql_escape(name)}','{sql_escape(yf)}','{sql_escape(yt)}',"
                    f"'{sql_escape(json.dumps(rec, ensure_ascii=False))}',{now})")
                stats["models"] += 1
            if out:
                reqj = json.dumps({"action": "models", "section": section}, separators=(",", ":"))
                umapi_cache_write(dst, "models", section, reqj, out)
        elif method == "getCars":
            ms = params.get("modelId")
            out = []
            for r in items:
                cid = pick(r, "carId", "CAR_ID", "PC_ID", "CV_ID", "MTB_ID", "ID")
                title = pick(r, "carName", "CAR_NAME", "PASSENGER_CAR", "COMMERCIAL_VEHICLE")
                yf = (pick(r, "yearsFrom", "yearFrom") or "")[4:16]
                yt = (pick(r, "yearsTo", "yearTo") or "")[4:16]
                kw = pick(r, "powerKw", "POWER_KW", "kw") or ""
                cc = pick(r, "capacityLt", "CAPACITY_LT", "capacityTech") or ""
                fuel = pick(r, "fuelType", "FUEL_TYPE") or ""
                if not cid:
                    continue
                rec = {"PC_ID": cid, "PASSENGER_CAR": title or "", "CI_FROM": yf, "CI_TO": yt,
                       "POWER_KW": kw, "CAPACITY_LT": cc, "FUEL_TYPE": fuel}
                out.append(rec)
                dst.run(
                    "REPLACE INTO `epc_umapi_modifications` (`section`,`ms_id`,`modification_id`,`title`,`year_from`,`year_to`,`power_kw`,`capacity_lt`,`fuel_type`,`raw_json`,`updated_at`) VALUES ("
                    f"'{section}',{int(float(ms or 0))},{int(float(cid))},'{sql_escape(title)}','{sql_escape(yf)}','{sql_escape(yt)}',"
                    f"'{sql_escape(kw)}','{sql_escape(cc)}','{sql_escape(fuel)}','{sql_escape(json.dumps(rec, ensure_ascii=False))}',{now})")
                stats["mods"] += 1
            if out:
                reqj = json.dumps({"action": "modifications", "section": section}, separators=(",", ":"))
                umapi_cache_write(dst, "modifications", section, reqj, out)
        elif method == "getArticles":
            for r in items:
                sid = pick(r, "SUP_ID", "ART_SUP_ID")
                br = pick(r, "SUP_BRAND", "ART_SUP_BRAND")
                full = pick(r, "SUP_FULL_NAME") or br
                if sid and br:
                    brands[sid] = (br, full)
    for sid, (br, full) in brands.items():
        rec = {"SUP_ID": sid, "SUP_BRAND": br, "SUP_FULL_NAME": full}
        dst.run(
            "REPLACE INTO `epc_umapi_brands` (`sup_id`,`brand`,`full_name`,`raw_json`,`updated_at`) VALUES ("
            f"{int(float(sid))},'{sql_escape(br)}','{sql_escape(full)}','{sql_escape(json.dumps(rec, ensure_ascii=False))}',{now})")
        stats["brands"] += 1
    if brands:
        reqj = json.dumps({"action": "suppliers", "section": "aftermarket"}, separators=(",", ":"))
        umapi_cache_write(dst, "suppliers", "aftermarket", reqj,
                          [{"SUP_ID": s, "SUP_BRAND": b[0], "SUP_FULL_NAME": b[1]} for s, b in brands.items()])

    print(f"translated: {stats}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
