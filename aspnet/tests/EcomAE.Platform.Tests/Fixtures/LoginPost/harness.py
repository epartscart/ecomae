#!/usr/bin/env python3
"""Regenerates goldens.json by POSTing each case of cases.json to the real plugins/authentication/plugin.php, served by
`php -S` through a small router that builds $db_link and $DP_Config the way the storefront core does and prints
$DP_Template->html after the plugin. The real content/users (dp_user, stop_csrf, password upgrade), UAE VAT, anti-crawl
and epc_deploy_auth.php code run against a throwaway MariaDB schema per case (seed.sql); the staff notice is a stub
that records its arguments. Each golden keeps the status, Location, body, cookies, the notices and the rows left
behind, with session tokens reduced to S32. Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, re, secrets, shutil, socket, subprocess, tempfile, time, urllib.error, urllib.parse, urllib.request

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]
SECRET = "golden-secret"
HOST = "acme.example"

ROUTER = r"""<?php
define('_ASTEXE_', 1);
require $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$db_link->query('SET NAMES utf8');
function translate_str_by_id($id) { return 'T' . $id; }
function getPageUrl() { return 'http://' . $_SERVER['HTTP_HOST'] . $_SERVER['REQUEST_URI']; }
class EpcHTemplate { public $html = ''; public $id = 0; }
$DP_Template = new EpcHTemplate();
require $_SERVER['DOCUMENT_ROOT'] . '/plugins/authentication/plugin.php';
echo 'HTML:' . $DP_Template->html;
"""

NOTIFY = r"""<?php
function epc_build_auth_event_html($event, $user_id, $contact, $extra) { return 'EV|' . $event . '|' . $user_id . '|' . $contact; }
function epc_staff_send_notify($name, $vars, $customer_id = 0, $office_id = 0, $extra = array(), $wait = true) {
    file_put_contents(getenv('EPC_H_NOTIFY'), json_encode(array($name, $vars, $customer_id, $office_id, $extra, $wait)) . "\n", FILE_APPEND);
}
"""


def php_str(value):
    return "'" + str(value).replace("\\", "\\\\").replace("'", "\\'") + "'"


def config_php(db):
    return "\n".join(["<?php", "class DP_Config {", "public $host = '127.0.0.1';", "public $db = %s;" % php_str(db),
                      "public $user = 'ecomae';", "public $password = %s;" % php_str(password), "public $backend_dir = 'cp';",
                      "public $secret_succession = %s;" % php_str(SECRET), "public $domain_path = 'http://%s/';" % HOST, "}"]) + "\n"


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
    for rel in ["content/shop/usefull", "plugins/authentication"]:
        os.makedirs(os.path.join(docroot, rel))
    for rel in ["content/users", "content/shop/finance", "content/shop/pricing", "content/shop/docpart"]:
        os.symlink(os.path.join(root, rel), os.path.join(docroot, rel))
    shutil.copy(os.path.join(root, "plugins/authentication/plugin.php"), os.path.join(docroot, "plugins/authentication"))
    shutil.copy(os.path.join(root, "epc_deploy_auth.php"), docroot)
    open(os.path.join(docroot, "content/shop/usefull/epc_admin_notifications.php"), "w").write(NOTIFY)
    router = os.path.join(docroot, "router.php")
    open(router, "w").write(ROUTER)
    notify = os.path.join(work, "notify.log")

    port = free_port()
    env = {k: v for k, v in os.environ.items() if k != "EPC_DEPLOY_TOKEN"}
    env["EPC_H_NOTIFY"] = notify
    log = os.path.join(work, "php.log")
    server = subprocess.Popen(["php", "-d", "display_errors=0", "-d", "log_errors=1", "-d", "error_log=" + log,
                               "-S", "127.0.0.1:%d" % port, "-t", docroot, router], env=env,
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(0.8)

    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, *args, **kwargs):
            return None

    opener = urllib.request.build_opener(NoRedirect)
    goldens = {}
    try:
        for case in cases:
            db = "ecomae_cpw_" + secrets.token_hex(6)
            sql("CREATE DATABASE `%s`" % db)
            try:
                sql(seed, db)
                for statement in case.get("setup", []):
                    sql(statement, db)
                open(os.path.join(docroot, "config.php"), "w").write(config_php(db))
                if os.path.exists(notify):
                    os.remove(notify)

                headers = {"Host": HOST, "User-Agent": case.get("agent", "Mozilla/5.0 golden"),
                           "Content-Type": "application/x-www-form-urlencoded"}
                if "cookie" in case:
                    headers["Cookie"] = case["cookie"]
                path = case.get("path", "/en/users/login") + case.get("query", "")
                request = urllib.request.Request("http://127.0.0.1:%d%s" % (port, path),
                                                 data=urllib.parse.urlencode(case["form"]).encode(), headers=headers)
                try:
                    response = opener.open(request)
                except urllib.error.HTTPError as e:
                    response = e
                body = response.read().decode()
                cookies = sorted(
                    re.sub(r"=[0-9a-f]{32}$", "=S32", c.split(";", 1)[0]) + ("|expires" if "expires=" in c.lower() else "|session")
                    for c in (response.headers.get_all("Set-Cookie") or []))
                notices = [json.loads(line) for line in open(notify)] if os.path.exists(notify) else []

                def rows(query):
                    return [line.split("\t") for line in sql(query, db).splitlines()]

                goldens[case["name"]] = {
                    "status": response.status,
                    "location": response.headers.get("Location"),
                    "body": body,
                    "cookies": cookies,
                    "notices": notices,
                    "users": rows("SELECT user_id, LEFT(password, 4), time_last_visit > 0 FROM users ORDER BY user_id"),
                    "sessions": rows("SELECT IF(session REGEXP '^[0-9a-f]{32}$', 'S32', session), user_id, time > 1, LENGTH(csrf_guard_key), "
                                     "last_activiti_time > 0, IFNULL(`2fa_attempts`, 'NULL') FROM sessions ORDER BY id"),
                    "options": rows("SELECT id, session_id FROM users_options ORDER BY id"),
                    "profiles": rows("SELECT user_id, data_key, IFNULL(data_value, 'NULL') FROM users_profiles ORDER BY user_id, data_key"),
                    "carts": rows("SELECT id, user_id, session_id FROM shop_carts ORDER BY id"),
                }
            finally:
                sql("DROP DATABASE `%s`" % db)
    finally:
        server.terminate()
        server.wait()
        if os.path.exists(log):
            text = open(log).read()
            if "Fatal" in text or "Warning" in text or "Exception" in text:
                print(text[-4000:])
        shutil.rmtree(work)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
