#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/notifications/send_notify.php (php-cli, display_errors off)
against a throwaway MariaDB schema per case. The mailer, translator, template, WhatsApp and curl are stubbed.
Needs ECOMAE_LOCAL_MARIADB_E2E_DSN (the password of ecomae@127.0.0.1:3306)."""
import json, os, secrets, shutil, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
password = os.environ["ECOMAE_LOCAL_MARIADB_E2E_DSN"]

STUBS = {
    "config.php": """<?php
class DP_Config {
    public $host = '127.0.0.1'; public $db; public $user = 'ecomae'; public $password;
    public $secret_succession = 'sek'; public $domain_path = 'http://127.0.0.1:9/';
    public function __construct() {
        $this->db = getenv('EPC_H_DB'); $this->password = getenv('EPC_H_PW');
        if (getenv('EPC_H_OSNS')) { $this->orders_statuses_notifications_settings = 1; }
    }
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
    "lib/DocpartMailer/docpart_mailer.php": """<?php
class DocpartMailer {
    public $Subject; public $Body; public $CharSet; public $SMTPDebug; private $to = array();
    public function addAddress($a, $n = '') { $this->to[] = $a; }
    public function IsSMTP() {} public function IsHTML($b) {} public function addAttachment($u, $n) {}
    public function Send() {
        file_put_contents(getenv('EPC_H_MAIL'), json_encode(array('to' => $this->to, 'subject' => $this->Subject, 'body' => $this->Body)) . "\\n", FILE_APPEND);
        foreach ($this->to as $t) { if (strpos($t, 'fail') !== false) { return false; } }
        return true;
    }
}
""",
    "content/notifications/template.php": "<?php\n",
    "content/notifications/epc_whatsapp_notify.php": "<?php\n",
    "stubs.php": """<?php
foreach (array('CURLOPT_URL', 'CURLOPT_RETURNTRANSFER', 'CURLOPT_POST', 'CURLOPT_POSTFIELDS', 'CURLOPT_SSL_VERIFYHOST', 'CURLOPT_SSL_VERIFYPEER') as $i => $c) { define($c, $i + 1); }
function curl_init() { return null; }
function curl_setopt($c, $o, $v) { return true; }
function curl_exec($c) { return false; }
function curl_close($c) {}
""",
    "run.php": """<?php
$case = json_decode(file_get_contents(getenv('EPC_H_CASE')), true);
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
$_POST = $case['post'];
include __DIR__ . '/content/notifications/send_notify.php';
""",
}


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
    shutil.copy(os.path.join(root, "content/notifications/send_notify.php"), os.path.join(docroot, "content/notifications/send_notify.php"))

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
                mail_file = os.path.join(work, "mail.log")
                json.dump(case, open(case_file, "w", encoding="utf-8"))
                open(mail_file, "w").close()
                env = dict(os.environ, EPC_H_DB=db, EPC_H_PW=password, EPC_H_CASE=case_file, EPC_H_MAIL=mail_file)
                if case.get("osns"):
                    env["EPC_H_OSNS"] = "1"
                run = subprocess.run(["php", "-d", "display_errors=0", "-d", "auto_prepend_file=" + os.path.join(docroot, "stubs.php"),
                                      os.path.join(docroot, "run.php")], env=env, capture_output=True, text=True)
                debug = [line.split("\t") for line in sql("SELECT name, IFNULL(status,'NULL') FROM debug_results ORDER BY id", db).splitlines()]
                mails = [json.loads(line) for line in open(mail_file, encoding="utf-8") if line.strip()]
                goldens[case["name"]] = {"body": run.stdout, "fatal": run.returncode != 0, "debug": debug,
                                         "mails": [{"to": m["to"], "subject": m["subject"], "body": m["body"]} for m in mails]}
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
