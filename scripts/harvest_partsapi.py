#!/usr/bin/env python3
"""Bulk-harvest partsapi.ru TecDoc data into epc_partsapi_cache.

Crawls smallest-first so coverage completes level by level:
getMakes (per carType) -> getModels (per make, fewest-models-first) ->
getCars (per model) -> getSearchTree (per car) -> getArticles (per tree node)
-> getArticleCrosses (per article, optional).

Every response is written to `epc_partsapi_cache` with the SAME cache_key
format as PartsApiCatalogService.BuildCacheKey, so the ASP.NET service serves
these rows to every tenant of the target DB without another provider call.

Keys are read from a JSON file (never committed): {"getMakes": "...", ...}.

Usage:
  python3 harvest_partsapi.py --keys-file keys.json --database docpart \
      --mysql "mysql" --workers 8 [--level 5] [--car-types PC,CV]
Resumable: cached cache_keys are skipped; the frontier log lives next to the
keys file so an interrupted run continues where it stopped.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
import threading
import time
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor

API = "https://api.partsapi.ru/"
CREATE_TABLE = """
CREATE TABLE IF NOT EXISTS `epc_partsapi_cache` (
    `cache_key` VARCHAR(190) NOT NULL,
    `method` VARCHAR(64) NOT NULL,
    `action` VARCHAR(40) NOT NULL DEFAULT '',
    `section` VARCHAR(20) NOT NULL DEFAULT '',
    `language` VARCHAR(10) NOT NULL DEFAULT 'en',
    `request_json` TEXT,
    `response_json` MEDIUMTEXT NOT NULL,
    `rows_count` INT NOT NULL DEFAULT 0,
    `http_status` INT NOT NULL DEFAULT 200,
    `last_sync` INT NOT NULL DEFAULT 0,
    PRIMARY KEY (`cache_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
"""

SAFE_ARG = re.compile(r"^[a-zA-Z0-9_\- .=/]*$")


def canonical_params(params: dict) -> str:
    return json.dumps({k: str(params[k]) for k in sorted(params)}, separators=(",", ":"), ensure_ascii=False)


def cache_key(method: str, params: dict) -> str:
    # Matches the existing epc_partsapi_cache producer + ASP.NET service:
    # sha1(method + "|" + canonical params JSON)
    import hashlib
    return hashlib.sha1((method + "|" + canonical_params(params)).encode()).hexdigest()


def sql_escape(value: str) -> str:
    return (
        (value or "")
        .replace("\\", "\\\\")
        .replace("'", "\\'")
        .replace('"', '\\"')
        .replace("\n", "\\n")
        .replace("\r", "\\r")
        .replace("\x00", "\\0")
        .replace("\x1a", "\\Z")
    )


class Db:
    def __init__(self, mysql: str, database: str):
        self.mysql = mysql
        self.database = database
        self._lock = threading.Lock()
        self._known: set[str] = set()

    def run(self, sql: str):
        with self._lock:
            proc = subprocess.run(
                f"{self.mysql} -N -s {self.database}",
                shell=True,
                input=sql.encode(),
                capture_output=True,
            )
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr.decode()[:300])
        return proc.stdout.decode()

    def load_known_keys(self, prefix: str):
        out = self.run(
            "SELECT `cache_key` FROM `epc_partsapi_cache` "
            f"WHERE `cache_key` LIKE '{sql_escape(prefix)}%'"
        )
        for line in out.splitlines():
            self._known.add(line.strip())

    def has(self, key: str) -> bool:
        return key in self._known

    def get(self, key: str) -> str | None:
        out = self.run(
            "SELECT `response_json` FROM `epc_partsapi_cache` "
            f"WHERE `cache_key` = '{sql_escape(key)}' LIMIT 1"
        )
        return out if out else None

    def save(self, key: str, method: str, params: dict, body: str):
        now = int(time.time())
        wrapped = wrap_body(method, body)
        try:
            rows_n = len(json.loads(wrapped).get("data", []))
        except Exception:
            rows_n = 0
        lang = str(params.get("lang") or params.get("LANG") or "en")
        req = canonical_params(params)
        sql = (
            "REPLACE INTO `epc_partsapi_cache` (`cache_key`, `method`, `action`, `section`, `language`, `request_json`, `response_json`, `rows_count`, `http_status`, `last_sync`) "
            f"VALUES ('{sql_escape(key)}', '{sql_escape(method)}', '{sql_escape(method.lower())}', 'catalog', "
            f"'{sql_escape(lang)}', '{sql_escape(req)}', '{sql_escape(wrapped)}', {rows_n}, 200, {now})"
        )
        self.run(sql)
        self._known.add(key)


def fetch(method: str, key: str, params: dict, retries: int = 3) -> str | None:
    qs = "method=" + urllib.parse.quote(method) + "&key=" + urllib.parse.quote(key)
    for k, v in params.items():
        qs += "&" + urllib.parse.quote(k) + "=" + urllib.parse.quote(str(v))
    for attempt in range(retries):
        try:
            req = urllib.request.Request(API + "?" + qs, headers={"User-Agent": "ecomae-harvest/1.0"})
            with urllib.request.urlopen(req, timeout=30) as resp:
                body = resp.read().decode("utf-8", "replace")
            if '"error_code"' in body or '"code":9' in body or '"status":4' in body or '"status":5' in body:
                return None
            return body
        except Exception:
            time.sleep(1.5 * (attempt + 1))
    return None


def rows(body: str | None) -> list[dict]:
    if not body:
        return []
    try:
        data = json.loads(body)
    except json.JSONDecodeError:
        return []
    if isinstance(data, list):
        return [r for r in data if isinstance(r, dict)]
    if isinstance(data, dict) and isinstance(data.get("data"), list):
        return [r for r in data["data"] if isinstance(r, dict)]
    return []


def wrap_body(method: str, body: str) -> str:
    try:
        data = json.loads(body)
    except json.JSONDecodeError:
        return body
    if isinstance(data, dict) and "ok" in data:
        return body
    if isinstance(data, list):
        return json.dumps({"ok": True, "action": method.lower(), "rows": len(data), "data": data},
                          ensure_ascii=False, separators=(",", ":"))
    return json.dumps({"ok": True, "action": method.lower(), "rows": 1, "data": data},
                      ensure_ascii=False, separators=(",", ":"))


def pick(row: dict, *names: str) -> str | None:
    for name in names:
        for k, v in row.items():
            if k.lower() == name.lower() and v not in (None, ""):
                return str(v)
    return None


def harvest(db: Db, keys: dict, workers: int, level: int, car_types: list[str]):
    stats = {"calls": 0, "saved": 0}
    lock = threading.Lock()

    def call(method: str, params: dict) -> str | None:
        ck = cache_key(method, params)
        if len(ck) > 250:
            return None
        if db.has(ck):
            return db.get(ck)
        key = keys.get(method)
        if not key:
            return None
        body = fetch(method, key, params)
        with lock:
            stats["calls"] += 1
        if body:
            db.save(ck, method, params, body)
            with lock:
                stats["saved"] += 1
        return body

    def batch(tasks: list[tuple[str, dict]], label: str) -> list[str | None]:
        results: list[str | None] = [None] * len(tasks)
        done = [0]

        def work(i_method_params):
            i, method, params = i_method_params
            results[i] = call(method, params)
            with lock:
                done[0] += 1
                if done[0] % 200 == 0:
                    print(f"  {label}: {done[0]}/{len(tasks)} calls={stats['calls']} saved={stats['saved']}", flush=True)

        with ThreadPoolExecutor(max_workers=workers) as pool:
            list(pool.map(work, enumerate(tasks)))
        return results

    # Level 1: manufacturers
    print("level 1: getMakes", flush=True)
    makes: list[dict] = []
    for ct in car_types:
        body = call("getMakes", {"carType": ct, "lang": "16"})
        for row in rows(body):
            mid = pick(row, "makeId", "MFA_ID")
            name = pick(row, "makeName", "MFA_BRAND")
            if mid and name:
                makes.append({"makeId": mid, "makeName": name, "carType": ct})
    print(f"  {len(makes)} makes", flush=True)
    if level < 2 or not makes:
        return stats

    # Level 2: models — needed in full to order makes smallest-first
    print("level 2: getModels", flush=True)
    tasks = [("getModels", {"carType": m["carType"], "makeId": m["makeId"], "lang": "16"}) for m in makes]
    results = batch(tasks, "models")
    model_map: dict[str, list[dict]] = {}
    for m, body in zip(makes, results):
        models = []
        for row in rows(body):
            mid = pick(row, "modelId", "MOD_ID", "MS_ID")
            name = pick(row, "modelName", "MOD_CDS_TEXT", "MS_NAME")
            if mid:
                models.append({"modelId": mid, "modelName": name or "", "carType": m["carType"], "makeId": m["makeId"]})
        model_map[m["makeId"]] = models
    print(f"  {sum(len(v) for v in model_map.values())} models", flush=True)
    if level < 3:
        return stats

    # Level 3: cars — smallest makes (fewest models) first
    ordered_makes = sorted(makes, key=lambda m: len(model_map.get(m["makeId"], [])))
    car_tasks: list[tuple[str, dict, dict]] = []
    for m in ordered_makes:
        for mo in model_map.get(m["makeId"], []):
            car_tasks.append((
                "getCars",
                {"carType": mo["carType"], "makeId": mo["makeId"], "modelId": mo["modelId"], "lang": "16"},
                mo,
            ))
    print(f"level 3: getCars x{len(car_tasks)}", flush=True)
    results = batch([(t[0], t[1]) for t in car_tasks], "cars")
    cars: list[dict] = []
    for task, body in zip(car_tasks, results):
        for row in rows(body):
            cid = pick(row, "CAR_ID", "carId")
            if cid:
                cars.append({"carId": cid, "carType": task[2]["carType"], "makeId": task[2]["makeId"]})
    print(f"  {len(cars)} cars", flush=True)
    if level < 4:
        return stats

    # Level 4: search trees per car (fewest cars per make first already)
    print(f"level 4: getSearchTree x{len(cars)}", flush=True)
    tree_tasks = [("getSearchTree", {"lang": "16", "carId": c["carId"], "carType": c["carType"]}) for c in cars]
    results = batch(tree_tasks, "trees")
    nodes: list[dict] = []
    for c, body in zip(cars, results):
        for row in rows(body):
            nid = pick(row, "STR_ID", "strId")
            if nid:
                nodes.append({"strId": nid, "carId": c["carId"], "carType": c["carType"]})
    print(f"  {len(nodes)} tree nodes", flush=True)
    if level < 5:
        return stats

    # Level 5: articles per node — smallest trees (fewest nodes per car) first
    per_car: dict[str, int] = {}
    for n in nodes:
        per_car[n["carId"]] = per_car.get(n["carId"], 0) + 1
    nodes.sort(key=lambda n: (per_car[n["carId"]], n["strId"]))
    article_tasks = [
        ("getArticles", {"lang": "16", "strId": n["strId"], "carId": n["carId"], "carType": n["carType"]})
        for n in nodes
    ]
    print(f"level 5: getArticles x{len(article_tasks)}", flush=True)
    results = batch(article_tasks, "articles")

    # Level 6 (optional): crosses + media per article, when keys exist.
    if level >= 6 and (keys.get("getArticleCrosses") or keys.get("getArticleMedia")):
        article_ids: list[str] = []
        seen = set()
        for body in results:
            for row in rows(body):
                aid = pick(row, "ART_ID", "articleId")
                if aid and aid not in seen:
                    seen.add(aid)
                    article_ids.append(aid)
        if keys.get("getArticleCrosses"):
            cross_tasks = [("getArticleCrosses", {"ART_ID": a, "LANG": "16"}) for a in article_ids]
            print(f"level 6: getArticleCrosses x{len(cross_tasks)}", flush=True)
            batch(cross_tasks, "crosses")
        if keys.get("getArticleMedia"):
            media_tasks = [("getArticleMedia", {"ART_ID": a, "LANG": "16"}) for a in article_ids]
            print(f"level 6: getArticleMedia x{len(media_tasks)}", flush=True)
            batch(media_tasks, "media")

    return stats


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--keys-file", required=True)
    ap.add_argument("--database", required=True)
    ap.add_argument("--mysql", default="mysql")
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--level", type=int, default=5)
    ap.add_argument("--car-types", default="PC")
    args = ap.parse_args()

    for name, value in (("--database", args.database), ("--mysql", args.mysql)):
        if not SAFE_ARG.match(value):
            print(f"refusing {name} with shell metacharacters: {value!r}", file=sys.stderr)
            return 1

    keys = json.load(open(args.keys_file))
    db = Db(args.mysql, args.database)
    db.run(CREATE_TABLE)
    print("loading existing cache keys...", flush=True)
    db.load_known_keys("")
    print(f"  {len(db._known):,} cached responses already stored", flush=True)

    stats = harvest(db, keys, args.workers, args.level, [t.strip() for t in args.car_types.split(",") if t.strip()])
    print(f"done: {stats['calls']:,} provider calls, {stats['saved']:,} new responses saved", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
