#!/usr/bin/env python3
"""Replays accounts_steps.json against PHP ajax_epc_free_tools.php (php -S on a throwaway MariaDB database)
and writes accounts_golden.json: every response (status + body, tokens masked) and the final table state.

Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=<password> python3 accounts_driver.py <repo_root>
"""
import json
import os
import re
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = "/content/general_pages/ajax_epc_free_tools.php"

STATE_SQL = [
    ("accounts",
     "SELECT id, email, company, country, use_count, login_count, LEFT(pass_hash, 7), reset_code_hash IS NULL, "
     "reset_code_expires IS NULL, del_code_hash IS NULL, time_created > 1000 FROM epc_free_tool_accounts ORDER BY id"),
    ("saves", "SELECT id, account_id, tool, country, title, payload, time_created > 1000 FROM epc_free_tool_saves ORDER BY id"),
    ("settings", "SELECT name, val FROM epc_free_tool_settings ORDER BY name"),
    ("columns",
     "SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, IFNULL(COLUMN_DEFAULT, 'NULL') FROM information_schema.columns "
     "WHERE table_schema = DATABASE() AND TABLE_NAME LIKE 'epc\\_free\\_tool\\_%' ORDER BY TABLE_NAME, ORDINAL_POSITION"),
]


def mysql(db, sql, password):
    out = subprocess.run(
        ["mysql", "-h127.0.0.1", "-uecomae", "-p" + password, "--default-character-set=utf8mb4", "-N", "-B", db, "-e", sql],
        check=True, capture_output=True, text=True)
    return [line.split("\t") for line in out.stdout.splitlines()]


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def main():
    root = sys.argv[1]
    password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]
    db = "ecomae_cpw_" + uuid.uuid4().hex[:12]
    mysql("mysql", "CREATE DATABASE `%s`" % db, password)
    docroot = tempfile.mkdtemp(prefix="ft-docroot-")
    os.symlink(os.path.join(root, "content"), os.path.join(docroot, "content"))
    with open(os.path.join(docroot, "config.php"), "w") as f:
        f.write("<?php\nclass DP_Config {\npublic $host = '127.0.0.1';\npublic $db = '%s';\npublic $user = 'ecomae';\n"
                "public $password = '%s';\n}\n" % (db, password.replace("\\", "\\\\").replace("'", "\\'")))
    port = free_port()
    server = subprocess.Popen(["php", "-S", "127.0.0.1:%d" % port, "-t", docroot], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        time.sleep(0.8)
        steps = json.load(open(os.path.join(HERE, "accounts_steps.json")))
        tokens = {}
        results = []
        def dump():
            state = {name: mysql(db, sql, password) for name, sql in STATE_SQL}
            for rows in state.values():
                for row in rows:
                    for i, cell in enumerate(row):
                        for name, value in tokens.items():
                            row[i] = row[i].replace(value, "{" + name + "}")
            return state

        for step in steps:
            if "dump" in step:
                results.append({"state": dump()})
                continue
            if "sql" in step:
                mysql(db, step["sql"], password)
                results.append({"sql": True})
                continue
            body = step["body"]
            for name, value in tokens.items():
                body = body.replace("{" + name + "U}", value.upper()).replace("{" + name + "}", value)
            req = urllib.request.Request(
                "http://127.0.0.1:%d%s" % (port, PATH),
                data=None if step.get("method") == "GET" else body.encode("utf-8"),
                method=step.get("method", "POST"),
                headers={"Content-Type": step.get("content_type", "application/json")})
            try:
                with urllib.request.urlopen(req) as resp:
                    status, text = resp.status, resp.read().decode("utf-8")
            except urllib.error.HTTPError as e:
                status, text = e.code, e.read().decode("utf-8")
            if "capture" in step:
                tokens[step["capture"]] = json.loads(text)["token"]
            for name, value in tokens.items():
                text = text.replace(value, "{" + name + "}")
            text = re.sub(r'"time_created":[0-9]+', '"time_created":"*"', text)
            results.append({"status": status, "body": text})
        json.dump({"results": results, "state": dump()}, open(os.path.join(HERE, "accounts_golden.json"), "w"),
                  indent=1, ensure_ascii=False)
        print("steps:", len(results))
    finally:
        server.terminate()
        mysql("mysql", "DROP DATABASE `%s`" % db, password)


if __name__ == "__main__":
    main()
