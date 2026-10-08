#!/usr/bin/env python3
"""Regenerates goldens.json by requesting the real content/users/profileform.php for each case of cases.json, served by
`php -S` through a small router that builds $db_link, $DP_Config and $multilang_params the way the storefront core does
and prints the page output. The real content/users (dp_user, stop_csrf), the trade, currency and UAE VAT libraries and
the e-invoice schema run against a throwaway MariaDB schema per case (seed.sql). Translations are "T<id>". The profile
html and staff persons of the admin notice and send_notify are stubs that record their arguments. Each golden keeps the
body, the notices and the rows left behind. Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, secrets, shutil, socket, subprocess, tempfile, time, urllib.error, urllib.parse, urllib.request

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]
HOST = "acme.example"

ROUTER = r"""<?php
define('_ASTEXE_', 1);
require $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$db_link->query('SET NAMES utf8');
function translate_str_by_id($id) { return 'T' . $id; }
$multilang_params = array('lang_href' => $EPC_H_LANG);
require $_SERVER['DOCUMENT_ROOT'] . '/content/users/profileform.php';
"""

ADMIN = r"""<?php
function epc_build_customer_profile_html($customer_id, $order = null) { return 'PROFILE|' . $customer_id; }
function epc_staff_notify_persons($customer_id, $office_id = 0, $extra = array()) { return array('P|' . $customer_id); }
"""

NOTIFY = r"""<?php
function send_notify($notify_name, $notify_vars, $persons, $to_wait = true, $files = array()) {
    file_put_contents(getenv('EPC_H_NOTIFY'), json_encode(array($notify_name, $notify_vars, $persons, $to_wait)) . "\n", FILE_APPEND);
}
"""

BASE_CONFIG = {"shop_currency": "784", "show_phone_mask": 0, "country_phone_mask": "ru", "backend_dir": "cp",
               "domain_path": "http://%s/" % HOST, "from_name": "", "from_email": "", "smtp_mode": "", "smtp_encryption": "",
               "smtp_host": "", "smtp_port": "", "smtp_username": "", "smtp_password": ""}


def php_value(value):
    if isinstance(value, int):
        return str(value)
    return "'" + str(value).replace("\\", "\\\\").replace("'", "\\'") + "'"


def config_php(db, case):
    config = dict(BASE_CONFIG, **case.get("config", {}))
    lines = ["<?php", "class DP_Config {", "public $host = '127.0.0.1';", "public $db = %s;" % php_value(db),
             "public $user = 'ecomae';", "public $password = %s;" % php_value(password)]
    lines += ["public $%s = %s;" % (k, php_value(v)) for k, v in config.items()]
    lines += ["}", "$EPC_H_LANG = %s;" % php_value(case.get("lang", "/en"))]
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
    for rel in ["content/shop/usefull", "content/notifications"]:
        os.makedirs(os.path.join(docroot, rel))
    for rel in ["content/users", "content/shop/finance", "content/shop/pricing"]:
        os.symlink(os.path.join(root, rel), os.path.join(docroot, rel))
    open(os.path.join(docroot, "content/shop/usefull/epc_admin_notifications.php"), "w").write(ADMIN)
    open(os.path.join(docroot, "content/notifications/notify_helper.php"), "w").write(NOTIFY)
    router = os.path.join(docroot, "router.php")
    open(router, "w").write(ROUTER)
    notify = os.path.join(work, "notify.log")

    port = free_port()
    env = dict(os.environ)
    env["EPC_H_NOTIFY"] = notify
    log = os.path.join(work, "php.log")
    server = subprocess.Popen(["php", "-d", "display_errors=0", "-d", "log_errors=1", "-d", "error_log=" + log,
                               "-S", "127.0.0.1:%d" % port, "-t", docroot, router], env=env,
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(0.8)
    goldens = {}
    try:
        for case in cases:
            db = "ecomae_cpw_" + secrets.token_hex(6)
            sql("CREATE DATABASE `%s`" % db)
            try:
                sql(seed, db)
                for statement in case.get("setup", []):
                    sql(statement, db)
                open(os.path.join(docroot, "config.php"), "w").write(config_php(db, case))
                if os.path.exists(notify):
                    os.remove(notify)

                headers = {"Host": HOST, "User-Agent": "Mozilla/5.0 golden"}
                cookie = case.get("cookie", "session=sess41; u_id=41")
                if cookie:
                    headers["Cookie"] = cookie
                data = None
                if case.get("method") == "POST":
                    headers["Content-Type"] = "application/x-www-form-urlencoded"
                    data = urllib.parse.urlencode(case["form"]).encode()
                url = "http://127.0.0.1:%d/en/users/profile%s" % (port, case.get("query", ""))
                try:
                    response = urllib.request.urlopen(urllib.request.Request(url, data=data, headers=headers))
                except urllib.error.HTTPError as e:
                    response = e
                body = response.read().decode()
                notices = [json.loads(line) for line in open(notify)] if os.path.exists(notify) else []

                def rows(query):
                    return [line.split("\t") for line in sql(query, db).splitlines()]

                goldens[case["name"]] = {
                    "status": response.status,
                    "body": body,
                    "notices": notices,
                    "profiles": rows("SELECT user_id, data_key, IFNULL(data_value, 'NULL') FROM users_profiles ORDER BY user_id, data_key"),
                    "settings": rows("SELECT setting_key, IFNULL(setting_value, 'NULL') FROM epc_price_settings ORDER BY setting_key"),
                    "currencies": rows("SELECT iso_code, iso_name, caption_short, sign, available, `order` FROM shop_currencies ORDER BY iso_code"),
                    "tables": rows("SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() ORDER BY table_name"),
                }
            finally:
                sql("DROP DATABASE `%s`" % db)
    finally:
        server.terminate()
        server.wait()
        if os.path.exists(log):
            text = open(log).read()
            if "Fatal" in text or "Exception" in text:
                print(text[-4000:])
        shutil.rmtree(work)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
