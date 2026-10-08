#!/usr/bin/env python3
"""Regenerates goldens.json by serving the real epc-auth-verify-code.php (and its content/general_pages shim) with
`php -S`, with the real auth, portal and trade code, against a throwaway MariaDB schema per case that holds both the
platform registry and the tenant tables (seed.sql). Each golden keeps the status, headers, the body with the handoff
token unpacked, the cookies set, and the rows left behind. Session tokens and times are reduced to stable markers.
Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import base64, hashlib, json, os, re, secrets, shutil, socket, subprocess, tempfile, time, urllib.error, urllib.parse, urllib.request

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]
SECRET = "epartscart-deploy-2026"

OTP_DDL = """CREATE TABLE IF NOT EXISTS `epc_auth_otp_requests` (
 `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY, `email` VARCHAR(120) NOT NULL, `code_hash` VARCHAR(64) NOT NULL,
 `tenant_key` VARCHAR(64) NOT NULL DEFAULT '', `context_json` TEXT NULL, `expires_at` INT NOT NULL,
 `ip_address` VARCHAR(45) NOT NULL DEFAULT '', `created_at` INT NOT NULL DEFAULT 0,
 INDEX `email_created` (`email`, `created_at`), INDEX `expires_at` (`expires_at`)) ENGINE=InnoDB DEFAULT CHARSET=utf8"""

HANDOFF = re.compile(r"p=([A-Za-z0-9_\-%]+)&s=([0-9a-f]+)")


def php_str(value):
    return "'" + str(value).replace("\\", "\\\\").replace("'", "\\'") + "'"


def config_php(db):
    return "\n".join(["<?php", "class DP_Config {", "public $host = '127.0.0.1';", "public $db = %s;" % php_str(db),
                      "public $user = 'ecomae';", "public $password = %s;" % php_str(password), "public $backend_dir = 'cp';",
                      "public $secret_succession = 'unused';", "public $domain_path = '';", "}"]) + "\n"


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def unpack(match):
    raw = urllib.parse.unquote(match.group(1))
    data = json.loads(base64.urlsafe_b64decode(raw + "=" * (-len(raw) % 4)))
    data["sess"] = "S32" if re.fullmatch(r"[0-9a-f]{32}", data.get("sess", "")) else data.get("sess")
    data["exp"] = "E120" if 110 <= int(data["exp"]) - int(time.time()) <= 121 else data["exp"]
    return data


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    seed = open(os.path.join(here, "seed.sql"), encoding="utf-8").read()
    old = os.umask(0o077)
    work = tempfile.mkdtemp()
    cnf = os.path.join(work, "client.cnf")
    with open(cnf, "w") as f:
        f.write("[client]\nuser=ecomae\nhost=127.0.0.1\nport=3306\npassword=%s\n" % password)
    os.umask(old)

    def sql(statement, db="mysql"):
        return subprocess.run(["mysql", "--defaults-extra-file=" + cnf, "-N", "-B", db, "-e", statement],
                              check=True, capture_output=True, text=True).stdout

    docroot = os.path.join(work, "root")
    os.makedirs(docroot)
    for rel in ["content", "lib"]:
        os.symlink(os.path.join(root, rel), os.path.join(docroot, rel))
    for rel in ["epc-auth-verify-code.php", "epc_deploy_auth.php"]:
        shutil.copy(os.path.join(root, rel), os.path.join(docroot, rel))

    port = free_port()
    env = {k: v for k, v in os.environ.items() if k != "EPC_DEPLOY_TOKEN"}
    log = os.path.join(work, "php.log")
    server = subprocess.Popen(["php", "-d", "display_errors=0", "-d", "log_errors=1", "-d", "error_log=" + log,
                               "-S", "127.0.0.1:%d" % port, "-t", docroot], env=env,
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(0.8)
    goldens = {}
    try:
        for case in cases:
            db = "ecomae_cpw_" + secrets.token_hex(6)
            sql("CREATE DATABASE `%s`" % db)
            try:
                sql(seed.replace("@DB@", db).replace("@PW@", password), db)
                sql(OTP_DDL, db)
                for statement in case.get("setup", []):
                    sql(statement, db)
                otp = case.get("otp")
                if otp:
                    digest = hashlib.sha256((otp["code"] + "|" + SECRET).encode()).hexdigest()
                    sql("INSERT INTO epc_auth_otp_requests (email, code_hash, tenant_key, expires_at, ip_address, created_at) "
                        "VALUES ('%s','%s','%s',UNIX_TIMESTAMP()+%d,'10.0.0.9',UNIX_TIMESTAMP()-10)"
                        % (otp["email"], digest, otp["tenant_key"], otp.get("expires_in", 300)), db)
                open(os.path.join(docroot, "config.php"), "w").write(config_php(db))

                headers = {"Host": case.get("host", "localhost"), "User-Agent": "golden-agent"}
                if case.get("https"):
                    headers["X-Forwarded-Proto"] = "https"
                if "cookie" in case:
                    headers["Cookie"] = case["cookie"]
                if "form" in case:
                    data = urllib.parse.urlencode(case["form"]).encode()
                    headers["Content-Type"] = "application/x-www-form-urlencoded"
                else:
                    data = json.dumps(case["json"]).encode()
                    headers["Content-Type"] = "application/json"
                request = urllib.request.Request("http://127.0.0.1:%d%s" % (port, case["path"]), data=data, headers=headers)
                try:
                    response = urllib.request.urlopen(request)
                except urllib.error.HTTPError as e:
                    response = e
                body = response.read().decode()
                handoff = [unpack(m) for m in HANDOFF.finditer(body)]
                cookies = sorted(
                    re.sub(r"=[0-9a-f]{32}$", "=S32", c.split(";", 1)[0])
                    for c in (response.headers.get_all("Set-Cookie") or []))

                def rows(query):
                    return [line.split("\t") for line in sql(query, db).splitlines()]

                goldens[case["name"]] = {
                    "status": response.status,
                    "type": response.headers.get("Content-Type"),
                    "cache": response.headers.get("Cache-Control"),
                    "body": HANDOFF.sub("p=P&s=S", body),
                    "handoff": handoff,
                    "cookies": cookies,
                    "otp_rows": int(sql("SELECT COUNT(*) FROM epc_auth_otp_requests", db).strip()),
                    "users": rows("SELECT user_id, email, email_confirmed, unlocked, reg_variant, time_registered > 0, "
                                  "time_last_visit > 0, admin_created FROM users ORDER BY user_id"),
                    "profiles": rows("SELECT user_id, data_key, IF(data_value REGEXP '^[0-9]{9,}$', 'T', IFNULL(data_value, 'NULL')) "
                                     "FROM users_profiles ORDER BY user_id, data_key"),
                    "binds": rows("SELECT user_id, group_id FROM users_groups_bind ORDER BY user_id, group_id"),
                    "sessions": rows("SELECT IF(session REGEXP '^[0-9a-f]{32}$', 'S32', session), user_id, time > 1, type, contact_type, "
                                     "LENGTH(csrf_guard_key), last_activiti_time > 0 FROM sessions ORDER BY id"),
                    "carts": rows("SELECT id, user_id, session_id FROM shop_carts ORDER BY id"),
                }
            finally:
                sql("DROP DATABASE `%s`" % db)
    finally:
        server.terminate()
        server.wait()
        if os.path.exists(log):
            text = open(log).read()
            if "Fatal" in text or "Warning" in text:
                print(text[-4000:])
        shutil.rmtree(work)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
