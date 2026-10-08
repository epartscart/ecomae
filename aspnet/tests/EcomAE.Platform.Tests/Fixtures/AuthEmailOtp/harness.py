#!/usr/bin/env python3
"""Regenerates goldens.json by serving the real epc-auth-send-code.php and epc-auth-otp-verify-only.php (and their
content/general_pages shims) with `php -S`, with the real auth, SMTP-precheck and portal code, against a throwaway
MariaDB schema per case. Only config.php (and config.epc-smtp.php when a case sets it) are written for the case; no
mail is sent because every case stops at the SMTP precheck. Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of
ecomae@127.0.0.1:3306)."""
import hashlib, json, os, secrets, shutil, socket, subprocess, sys, tempfile, time, urllib.error, urllib.parse, urllib.request

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]
SECRET = "epartscart-deploy-2026"

OTP_DDL = """CREATE TABLE IF NOT EXISTS `epc_auth_otp_requests` (
 `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY, `email` VARCHAR(120) NOT NULL, `code_hash` VARCHAR(64) NOT NULL,
 `tenant_key` VARCHAR(64) NOT NULL DEFAULT '', `context_json` TEXT NULL, `expires_at` INT NOT NULL,
 `ip_address` VARCHAR(45) NOT NULL DEFAULT '', `created_at` INT NOT NULL DEFAULT 0,
 INDEX `email_created` (`email`, `created_at`), INDEX `expires_at` (`expires_at`)) ENGINE=InnoDB DEFAULT CHARSET=utf8"""

DEFAULT_CONFIG = {"smtp_mode": "0", "smtp_host": "", "smtp_port": "465", "smtp_encryption": "ssl", "smtp_username": "",
                  "smtp_password": "", "from_email": "shop@example.com", "from_name": "Shop"}


def php_str(value):
    return "'" + str(value).replace("\\", "\\\\").replace("'", "\\'") + "'"


def config_php(db, values):
    props = dict(DEFAULT_CONFIG, **values)
    lines = ["<?php", "class DP_Config {", "public $host = '127.0.0.1';", "public $db = %s;" % php_str(db),
             "public $user = 'ecomae';", "public $password = %s;" % php_str(password), "public $backend_dir = 'cp';",
             "public $secret_succession = 'unused';", "public $domain_path = 'http://localhost/';"]
    lines += ["public $%s = %s;" % (k, php_str(v)) for k, v in props.items()]
    lines.append("}")
    return "\n".join(lines) + "\n"


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


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
    for rel in ["epc-auth-send-code.php", "epc-auth-otp-verify-only.php", "epc_deploy_auth.php"]:
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
                for i in range(case.get("setup_ip_rows", 0)):
                    sql("INSERT INTO epc_auth_otp_requests (email, code_hash, tenant_key, expires_at, ip_address, created_at) "
                        "VALUES ('ip%d@example.com','x','acme',UNIX_TIMESTAMP()+600,'127.0.0.1',UNIX_TIMESTAMP()-30)" % i, db)
                otp = case.get("otp")
                if otp:
                    digest = hashlib.sha256((otp["code"] + "|" + SECRET).encode()).hexdigest()
                    sql("INSERT INTO epc_auth_otp_requests (email, code_hash, tenant_key, expires_at, ip_address, created_at) "
                        "VALUES ('%s','%s','%s',UNIX_TIMESTAMP()+%d,'10.0.0.9',UNIX_TIMESTAMP()-10)"
                        % (otp["email"], digest, otp["tenant_key"], otp["expires_in"]), db)
                open(os.path.join(docroot, "config.php"), "w").write(config_php(db, case.get("config", {})))
                smtp_file = os.path.join(docroot, "config.epc-smtp.php")
                if "smtp_file" in case:
                    open(smtp_file, "w").write("<?php\nreturn array(" + ", ".join(
                        "%s => %s" % (php_str(k), php_str(v)) for k, v in case["smtp_file"].items()) + ");\n")
                elif os.path.exists(smtp_file):
                    os.remove(smtp_file)

                headers = {"Host": case.get("host", "localhost")}
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
                rows = []
                for line in sql("SELECT email, tenant_key, LENGTH(code_hash), IFNULL(context_json,''), expires_at - created_at, "
                                "ip_address, created_at > UNIX_TIMESTAMP() - 60 FROM epc_auth_otp_requests ORDER BY id", db).splitlines():
                    email, tenant, hash_len, context, ttl, ip, fresh = line.split("\t")
                    ctx = json.loads(context) if context else {}
                    operator = ctx.get("_operator_otp", "") if isinstance(ctx, dict) else ""
                    rows.append({"email": email, "tenant_key": tenant, "hash_len": int(hash_len), "ttl": int(ttl), "ip": ip,
                                 "fresh": fresh == "1", "auth_mode": ctx.get("auth_mode", "") if isinstance(ctx, dict) else "",
                                 "operator_otp": "D6" if len(operator) == 6 and operator.isdigit() else operator})
                goldens[case["name"]] = {
                    "status": response.status,
                    "type": response.headers.get("Content-Type"),
                    "cache": response.headers.get("Cache-Control"),
                    "body": body,
                    "rows": rows,
                }
            finally:
                sql("DROP DATABASE `%s`" % db)
    finally:
        server.terminate()
        server.wait()
        if os.path.exists(log):
            text = open(log).read()
            if "Fatal" in text or "epc-auth-send-code:" in text:
                print(text[-3000:])
        shutil.rmtree(work)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
