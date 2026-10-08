#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/users/check_user_access.php with the real dp_user.php and
lang/dp_lang.php (php-cli, display_errors off) as a Control Panel ajax request, against a throwaway MariaDB schema per
case. Only config.php is stubbed; "ALLOWED" is echoed when the include lets the script go on.
Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, secrets, shutil, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]

STUBS = {
    "config.php": """<?php
class DP_Config {
    public $host = '127.0.0.1'; public $db; public $user = 'ecomae'; public $password;
    public $backend_dir = 'cp'; public $domain_path = 'http://127.0.0.1:9/'; public $multilang; public $backend_ui_lang;
    public function __construct() {
        $this->db = getenv('EPC_H_DB'); $this->password = getenv('EPC_H_PW');
        $case = json_decode(file_get_contents(getenv('EPC_H_CASE')), true);
        $this->multilang = !empty($case['multilang']) ? 1 : 0;
        $this->backend_ui_lang = isset($case['backend_ui_lang']) ? $case['backend_ui_lang'] : '';
    }
}
""",
    "run.php": """<?php
$case = json_decode(file_get_contents(getenv('EPC_H_CASE')), true);
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
$_SERVER['REQUEST_METHOD'] = 'POST';
$_SERVER['REQUEST_URI'] = '/cp/content/lang/ajax_get_string_translation.php';
$_SERVER['HTTP_HOST'] = '127.0.0.1';
$_SERVER['HTTP_REFERER'] = 'http://127.0.0.1:9/cp/lang/editor';
$_COOKIE = array();
if (!empty($case['admin'])) { $_COOKIE['admin_session'] = 'sa' . $case['admin']; $_COOKIE['admin_u_id'] = (string) $case['admin']; }
if (!empty($case['user'])) { $_COOKIE['session'] = 'su' . $case['user']; $_COOKIE['u_id'] = (string) $case['user']; }
if (isset($case['lang_cp'])) { $_COOKIE['lang_cp'] = $case['lang_cp']; }
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
$db_link->query('SET NAMES utf8;');
$pages_to_check = $case['pages'];
require_once __DIR__ . '/content/users/check_user_access.php';
echo 'ALLOWED';
""",
}

COPIED = ["content/users/check_user_access.php", "content/users/dp_user.php", "lang/dp_lang.php"]


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
            sql("CREATE DATABASE `%s`" % db)
            try:
                sql(seed, db)
                for statement in case.get("setup", []):
                    sql(statement, db)
                case_file = os.path.join(work, "case.json")
                json.dump(case, open(case_file, "w", encoding="utf-8"))
                env = dict(os.environ, EPC_H_DB=db, EPC_H_PW=password, EPC_H_CASE=case_file)
                run = subprocess.run(["php", "-d", "display_errors=0", os.path.join(docroot, "run.php")],
                                     env=env, capture_output=True, text=True)
                goldens[case["name"]] = {"body": run.stdout, "fatal": run.returncode != 0}
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
