#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/users/check_reg_contact.php with the real stop_csrf.php and
dp_user.php (php-cli, display_errors off) against a throwaway MariaDB schema per case. Only config.php and the
translator are stubbed. Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, secrets, shutil, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]

STUBS = {
    "config.php": """<?php
class DP_Config {
    public $host = '127.0.0.1'; public $db; public $user = 'ecomae'; public $password;
    public $backend_dir = 'cp'; public $domain_path = 'http://127.0.0.1:9/';
    public function __construct() { $this->db = getenv('EPC_H_DB'); $this->password = getenv('EPC_H_PW'); }
}
""",
    "lang/dp_lang.php": """<?php
function multilang_init() { return array('lang' => 'en'); }
function translate_str_by_id($id) {
    global $db_link;
    $q = $db_link->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?');
    $q->execute(array((string)$id, 'en'));
    $v = $q->fetchColumn();
    return $v === false ? null : $v;
}
""",
    "run.php": """<?php
$case = json_decode(file_get_contents(getenv('EPC_H_CASE')), true);
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
$_SERVER['REQUEST_METHOD'] = 'POST';
$_SERVER['REQUEST_URI'] = '/content/users/check_reg_contact.php';
$_SERVER['HTTP_HOST'] = '127.0.0.1';
if (isset($case['referer'])) { $_SERVER['HTTP_REFERER'] = $case['referer']; }
$_GET = isset($case['query']) ? $case['query'] : array();
$_POST = $case['post'];
$_COOKIE = isset($case['cookies']) ? $case['cookies'] : array();
include __DIR__ . '/content/users/check_reg_contact.php';
""",
}

COPIED = ["content/users/check_reg_contact.php", "content/users/stop_csrf.php", "content/users/dp_user.php"]


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
