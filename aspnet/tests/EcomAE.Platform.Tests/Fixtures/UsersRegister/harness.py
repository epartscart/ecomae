#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/users/register.php (with the real dp_user.php, lang/dp_lang.php,
epc_registration_enhanced.php, epc_customer_trade.php, epc_einvoice.php and epc_admin_notifications.php) behind
`php -S`, so that KYC documents are genuine HTTP uploads. Only send_notify is stubbed: it records every call and answers
with the case's [status, contact status]. The PDO commit/rollBack ignore "no active transaction" (the e-invoice schema
DDL commits implicitly in MySQL, which makes PHP 8 throw at the final commit).

Each case records the redirect location or page output, the users, users_profiles, users_groups_bind, sessions and
epc_einvoice_buyer_profiles rows, the notify calls and the stored KYC files. Times are written as "T", random phone codes
and generated passwords as "R", the date stamp of a KYC file name as "_D.".
Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, re, secrets, shutil, socket, subprocess, sys, tempfile, time, urllib.error, urllib.parse, urllib.request

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]

DEFAULT_COOKIES = {"captcha": "2d429bb3ea4f162977dcc27e380578f4", "users_agreement": "yes"}
DEFAULT_CONFIG = {"site_name": "Parts", "domain_path": "http://shop.test/", "backend_dir": "cp", "secret_succession": "s3cr3t",
                  "from_email": "noreply@shop.test"}

NOTIFY_STUB = r"""<?php
function send_notify($name, $vars, $persons, $wait) {
    global $harness_notify, $harness_case;
    $simple = array();
    foreach ($persons as $p) {
        if (($p['type'] ?? '') === 'user_id') { $simple[] = array('user_id', (string) $p['user_id']); }
        else { $simple[] = array('direct', (string) ($p['contacts']['email']['value'] ?? ''), (string) ($p['contacts']['phone']['value'] ?? '')); }
    }
    ksort($vars);
    $harness_notify[] = array('name' => $name, 'vars' => array_map('strval', $vars), 'persons' => $simple, 'wait' => (bool) $wait);
    $answer = isset($harness_case['notify'][$name]) ? $harness_case['notify'][$name] : array(true, true);
    return array('status' => $answer[0], 'persons' => array(array('contacts' => array('email' => array('status' => $answer[1]), 'phone' => array('status' => $answer[1])))));
}
"""

RUN_PHP = r"""<?php
define('_ASTEXE_', 1);
date_default_timezone_set('Asia/Dubai');
$harness_case = json_decode(file_get_contents($_GET['case']), true);
$harness_notify = array();
class DP_Config {}
$DP_Config = new DP_Config();
foreach ($harness_case['config_all'] as $k => $v) { $DP_Config->$k = $v; }
class HarnessPdo extends PDO {
    public function commit(): bool { try { return parent::commit(); } catch (PDOException $e) { if (strpos($e->getMessage(), 'no active transaction') === false) { throw $e; } return true; } }
    public function rollBack(): bool { try { return parent::rollBack(); } catch (PDOException $e) { if (strpos($e->getMessage(), 'no active transaction') === false) { throw $e; } return true; } }
}
$db_link = new HarnessPdo('mysql:host=127.0.0.1;dbname=' . $_GET['db'], 'ecomae', getenv('EPC_H_PW'));
$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
$db_link->query('SET NAMES utf8mb4;');
$DP_Lang = 'en';
$multilang_params = array('lang' => 'en', 'lang_href' => '/en', 'lang_href_no_slash' => 'en');
require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
function harness_rows($db_link, $sql) {
    try {
        return array_map(function ($r) { return array_map(function ($v) { return $v === null ? null : (string) $v; }, $r); },
            $db_link->query($sql)->fetchAll(PDO::FETCH_NUM));
    } catch (Throwable $e) { return null; }
}
function harness_dump() {
    global $db_link, $harness_notify;
    $out = ob_get_clean();
    $err = error_get_last();
    $location = null;
    if (preg_match('/location="([^"]*)";/', (string) $out, $m)) { $location = $m[1]; $out = ''; }
    $users = harness_rows($db_link, 'SELECT user_id, email, phone, reg_variant, password, email_code, phone_code, email_code_expired, phone_code_expired, time_registered, unlocked, email_confirmed, phone_confirmed, ip_address FROM users WHERE user_id > 7 ORDER BY user_id');
    $profiles = harness_rows($db_link, 'SELECT user_id, data_key, data_value FROM users_profiles ORDER BY user_id, id');
    foreach ((array) $profiles as $i => $p) { if ($p[2] !== null) { $profiles[$i][2] = preg_replace('/_\d{8}_\d{6}\./', '_D.', $p[2]); } }
    $buyers = harness_rows($db_link, 'SELECT user_id, buyer_name, trn, tin, legal_reg_no, country_code, emirate, city, email, phone, peppol_endpoint, buyer_onboarded FROM epc_einvoice_buyer_profiles ORDER BY user_id');
    $files = array();
    $kyc = $_SERVER['DOCUMENT_ROOT'] . '/content/files/kyc';
    if (is_dir($kyc)) {
        $it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($kyc, FilesystemIterator::SKIP_DOTS));
        foreach ($it as $f) { $files[] = array(preg_replace('/_\d{8}_\d{6}\./', '_D.', substr($f->getPathname(), strlen($_SERVER['DOCUMENT_ROOT']))), $f->getSize()); }
        sort($files);
    }
    echo "\n@@HARNESS@@" . json_encode(array(
        'location' => $location,
        'html' => (string) $out,
        'fatal' => ($err && in_array($err['type'], array(E_ERROR, E_CORE_ERROR, E_COMPILE_ERROR, E_USER_ERROR), true)) ? $err['message'] : null,
        'users' => $users,
        'profiles' => $profiles,
        'binds' => harness_rows($db_link, 'SELECT user_id, group_id FROM users_groups_bind ORDER BY user_id, id'),
        'sessions' => harness_rows($db_link, 'SELECT session, `2fa_attempts` FROM sessions ORDER BY id'),
        'buyers' => $buyers,
        'notify' => $harness_notify,
        'files' => $files,
    ), JSON_UNESCAPED_UNICODE);
}
register_shutdown_function('harness_dump');
ob_start();
include $_SERVER['DOCUMENT_ROOT'] . '/content/users/register.php';
"""


def multipart(fields, files):
    boundary = "----epcharness" + secrets.token_hex(8)
    parts = []
    for key, value in fields.items():
        parts.append(("--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n" % (boundary, key)).encode() + value.encode("utf-8") + b"\r\n")
    for key, (name, size) in files.items():
        head = "--%s\r\nContent-Disposition: form-data; name=\"%s\"; filename=\"%s\"\r\nContent-Type: application/octet-stream\r\n\r\n" % (boundary, key, name)
        parts.append(head.encode() + b"x" * size + b"\r\n")
    parts.append(("--%s--\r\n" % boundary).encode())
    return b"".join(parts), "multipart/form-data; boundary=" + boundary


def normalise(result, case):
    phone = case["post"].get("reg_contact_type") == "phone"
    simple = "simple_register" in case["post"]
    for row in result["users"] or []:
        row[9] = "T"
        if row[7] != "0":
            row[7] = "T"
        if row[8] != "0":
            row[8] = "T"
        if phone:
            row[6] = "R" if row[6] else row[6]
        if simple:
            row[4] = "R"
    for row in result["profiles"] or []:
        if row[1] in ("epc_trade_registered_at", "epc_trade_approved_at"):
            row[2] = "T"
    for call in result["notify"]:
        if "phone_confirm_code" in call["vars"]:
            call["vars"]["phone_confirm_code"] = "R"
        for key, value in call["vars"].items():
            call["vars"][key] = re.sub(r"Time: \d{2}\.\d{2}\.\d{4} \d{2}:\d{2}", "Time: T", value)
    return result


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    seed = open(os.path.join(here, "seed.sql"), encoding="utf-8").read()
    old = os.umask(0o077)
    work = tempfile.mkdtemp()
    cnf = os.path.join(work, "client.cnf")
    with open(cnf, "w") as f:
        f.write("[client]\nuser=ecomae\nhost=127.0.0.1\nport=3306\npassword=%s\ndefault-character-set=utf8mb4\n" % password)
    os.umask(old)
    os.chmod(work, 0o755)

    def sql(statement, db="mysql"):
        return subprocess.run(["mysql", "--defaults-extra-file=" + cnf, "-N", "-B", db, "-e", statement],
                              check=True, capture_output=True, text=True).stdout

    docroot = os.path.join(work, "root")
    os.makedirs(os.path.join(docroot, "content", "files"))
    os.makedirs(os.path.join(docroot, "content", "notifications"))
    for name in os.listdir(os.path.join(root, "content")):
        if name not in ("files", "notifications"):
            os.symlink(os.path.join(root, "content", name), os.path.join(docroot, "content", name))
    os.symlink(os.path.join(root, "lang"), os.path.join(docroot, "lang"))
    open(os.path.join(docroot, "content", "notifications", "notify_helper.php"), "w", encoding="utf-8").write(NOTIFY_STUB)
    open(os.path.join(docroot, "run.php"), "w", encoding="utf-8").write(RUN_PHP)

    sock = socket.socket()
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
    sock.close()
    server = subprocess.Popen(["php", "-d", "upload_max_filesize=20M", "-d", "post_max_size=40M", "-d", "display_errors=0", "-d", "log_errors=1", "-d", "error_log=" + os.path.join(work, "php.log"),
                               "-S", "127.0.0.1:%d" % port, "-t", docroot],
                              env=dict(os.environ, EPC_H_PW=password), stdout=subprocess.DEVNULL, stderr=open(os.path.join(work, "server.log"), "w"))
    time.sleep(0.8)

    goldens = {}
    try:
        for case in cases:
            db = "ecomae_cpw_" + secrets.token_hex(6)
            sql("CREATE DATABASE `%s` DEFAULT CHARACTER SET utf8mb4" % db)
            try:
                sql(seed, db)
                for statement in case.get("setup", []):
                    sql(statement, db)
                shutil.rmtree(os.path.join(docroot, "content", "files", "kyc"), ignore_errors=True)
                case_file = os.path.join(work, "case.json")
                json.dump(dict(case, config_all=dict(DEFAULT_CONFIG, **case.get("config", {}))), open(case_file, "w", encoding="utf-8"))
                body, ctype = multipart(case.get("post", {}), case.get("files", {}))
                url = "http://127.0.0.1:%d/run.php?%s" % (port, urllib.parse.urlencode({"case": case_file, "db": db}))
                cookies = case.get("cookies", DEFAULT_COOKIES)
                headers = {"Content-Type": ctype, "Host": "shop.test", "User-Agent": "EpcHarness/1.0",
                           "Cookie": "; ".join("%s=%s" % (k, urllib.parse.quote(v)) for k, v in cookies.items())}
                req = urllib.request.Request(url, data=body, headers=headers)
                try:
                    text = urllib.request.urlopen(req, timeout=60).read().decode("utf-8")
                except urllib.error.HTTPError as e:
                    text = e.read().decode("utf-8")
                marker = text.rfind("\n@@HARNESS@@")
                if marker < 0:
                    print(case["name"], text[:3000], file=sys.stderr)
                    raise SystemExit(1)
                goldens[case["name"]] = normalise(json.loads(text[marker + len("\n@@HARNESS@@"):]), case)
            finally:
                sql("DROP DATABASE IF EXISTS `%s`" % db)
    finally:
        server.terminate()
        server.wait()
        log = open(os.path.join(work, "server.log")).read() + (open(os.path.join(work, "php.log")).read() if os.path.exists(os.path.join(work, "php.log")) else "")
        if any(w in log for w in ("Fatal", "Deprecated")):
            print(log[-6000:], file=sys.stderr)
        shutil.rmtree(work, ignore_errors=True)

    out = os.path.join(here, "goldens.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(goldens, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("wrote", out)


if __name__ == "__main__":
    sys.exit(main())
