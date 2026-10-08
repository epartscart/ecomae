#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/shop/pricing/epc_customer_trade.php and epc_currency.php
(php-cli) against a throwaway MariaDB schema per case. Each case is a list of operations; the result of every operation
and the final users_profiles, users_groups_bind and shop_currencies rows are recorded. Only dp_user.php is stubbed (the
signed-in user of a "selected" operation). Unix times in *_at profile values are written as "T".
Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, secrets, shutil, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]

STUBS = {
    "content/users/dp_user.php": """<?php
class DP_User { public static function getUserId() { return (int) $GLOBALS['epc_h_user']; } }
""",
    "run.php": """<?php
define('_ASTEXE_', 1);
$case = json_decode(file_get_contents(getenv('EPC_H_CASE')), true);
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
$GLOBALS['epc_h_user'] = 0;
require_once __DIR__ . '/content/users/dp_user.php';
require_once __DIR__ . '/content/shop/pricing/epc_currency.php';
require_once __DIR__ . '/content/shop/pricing/epc_customer_trade.php';
$db_link = new PDO('mysql:host=127.0.0.1;dbname=' . getenv('EPC_H_DB'), 'ecomae', getenv('EPC_H_PW'));
$db_link->query('SET NAMES utf8mb4;');
function cfg($shop) { $c = new stdClass; $c->shop_currency = $shop; return $c; }
function strs($row) { return array_map(function ($v) { return $v === null ? null : (string) $v; }, $row); }
function recs($records) {
    $out = array();
    foreach ($records as $iso => $r) {
        $out[] = array((string) $iso, (string) $r['iso_name'], (string) $r['caption_short'], (string) $r['sign'], (string) (float) $r['rate']);
    }
    return $out;
}
$results = array();
foreach ($case['ops'] as $op) {
    $a = $op;
    switch ($op[0]) {
        case 'save': epc_trade_save_registration($db_link, $a[1], $a[2]); $results[] = null; break;
        case 'approve': $results[] = epc_trade_approve_customer($db_link, $a[1], $a[2], $a[3], $a[4]); break;
        case 'reject': epc_trade_reject_customer($db_link, $a[1], $a[2], $a[3]); $results[] = null; break;
        case 'request_change': epc_trade_request_currency_change($db_link, $a[1], $a[2], $a[3]); $results[] = null; break;
        case 'status': $results[] = epc_trade_approval_status($db_link, $a[1]); break;
        case 'can_order': $results[] = epc_trade_can_place_order($db_link, $a[1]); break;
        case 'block': $results[] = epc_trade_checkout_block_message($db_link, $a[1]); break;
        case 'currency_iso': $results[] = epc_trade_user_currency_iso($db_link, $a[1]); break;
        case 'locked': $results[] = epc_trade_currency_locked($db_link, $a[1]); break;
        case 'label': $results[] = epc_trade_customer_type_label($a[1]); break;
        case 'normalize': $results[] = epc_trade_normalize_customer_type($a[1]); break;
        case 'group_id': $results[] = epc_trade_price_profile_group_id($db_link, $a[1]); break;
        case 'assign': $results[] = epc_trade_assign_price_profile($db_link, $a[1], $a[2]); break;
        case 'get': $results[] = epc_trade_profile_get($db_link, $a[1], $a[2], $a[3]); break;
        case 'default_retail': $results[] = epc_trade_default_retail_currency_iso($db_link); break;
        case 'pending': $results[] = array_map('strs', epc_trade_pending_customers($db_link)); break;
        case 'records': $results[] = recs(epc_trade_currency_options($db_link, cfg($a[1]))); break;
        case 'selected':
            $GLOBALS['epc_h_user'] = $a[2];
            $_COOKIE = array();
            if ($a[3] !== null) { $_COOKIE['epc_currency'] = $a[3]; }
            if ($a[4] !== null) { $_COOKIE['epc_country'] = $a[4]; }
            $results[] = (string) epc_currency_selected_iso(epc_currency_records($db_link, cfg($a[1])), cfg($a[1]), $db_link);
            break;
        case 'format': $results[] = epc_currency_format_amount($a[1], epc_currency_records($db_link, cfg($a[2])), $a[3], $a[4]); break;
        default: $results[] = 'unknown op';
    }
}
function rows($db_link, $sql) {
    try { return array_map('strs', $db_link->query($sql)->fetchAll(PDO::FETCH_NUM)); } catch (Throwable $e) { return null; }
}
$profiles = rows($db_link, 'SELECT user_id, data_key, data_value FROM users_profiles ORDER BY user_id, data_key, id');
foreach ((array) $profiles as $i => $p) {
    if (substr($p[1], -3) === '_at' && $p[2] !== null && ctype_digit($p[2]) && abs((int) $p[2] - time()) < 600) { $profiles[$i][2] = 'T'; }
}
echo json_encode(array(
    'results' => $results,
    'profiles' => $profiles,
    'binds' => rows($db_link, 'SELECT user_id, group_id FROM users_groups_bind ORDER BY user_id, group_id'),
    'currencies' => rows($db_link, 'SELECT iso_code, iso_name, caption_short, sign, rate, available, `order` FROM shop_currencies ORDER BY iso_code, id'),
), JSON_UNESCAPED_UNICODE);
""",
}

COPIED = ["content/shop/pricing/epc_customer_trade.php", "content/shop/pricing/epc_currency.php"]


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    seed = open(os.path.join(here, "seed.sql"), encoding="utf-8").read()
    old = os.umask(0o077)
    work = tempfile.mkdtemp()
    cnf = os.path.join(work, "client.cnf")
    with open(cnf, "w") as f:
        f.write("[client]\nuser=ecomae\nhost=127.0.0.1\nport=3306\npassword=%s\ndefault-character-set=utf8mb4\n" % password)
    os.umask(old)

    def sql(statement, db="mysql"):
        return subprocess.run(["mysql", "--defaults-extra-file=" + cnf, "-N", "-B", db, "-e", statement],
                              check=True, capture_output=True, text=True).stdout

    docroot = os.path.join(work, "root")
    for rel, text in STUBS.items():
        os.makedirs(os.path.dirname(os.path.join(docroot, rel)), exist_ok=True)
        open(os.path.join(docroot, rel), "w", encoding="utf-8").write(text)
    for rel in COPIED:
        os.makedirs(os.path.dirname(os.path.join(docroot, rel)), exist_ok=True)
        shutil.copy(os.path.join(root, rel), os.path.join(docroot, rel))

    goldens = {}
    try:
        for case in cases:
            db = "ecomae_cpw_" + secrets.token_hex(6)
            sql("CREATE DATABASE `%s` DEFAULT CHARACTER SET utf8mb4" % db)
            try:
                sql(seed, db)
                for statement in case.get("setup", []):
                    sql(statement, db)
                case_file = os.path.join(work, "case.json")
                json.dump(case, open(case_file, "w", encoding="utf-8"))
                env = dict(os.environ, EPC_H_DB=db, EPC_H_PW=password, EPC_H_CASE=case_file)
                run = subprocess.run(["php", "-d", "display_errors=stderr", os.path.join(docroot, "run.php")],
                                     env=env, capture_output=True, text=True)
                if run.returncode != 0 or run.stderr.strip():
                    print(case["name"], run.stderr, file=sys.stderr)
                goldens[case["name"]] = json.loads(run.stdout)
            finally:
                sql("DROP DATABASE IF EXISTS `%s`" % db)
    finally:
        shutil.rmtree(work, ignore_errors=True)

    out = os.path.join(here, "goldens.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(goldens, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("wrote", out)


if __name__ == "__main__":
    sys.exit(main())
