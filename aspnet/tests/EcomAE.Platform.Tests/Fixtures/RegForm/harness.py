#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/users/regform.php with php-cli for each case of cases.json.
Each case gets a temp docroot with copies of the page and its real includes (registration render half, countries,
auth layout, user agreement, e-mail code modal) and stubs for DP_User, the auth core, the login context and the
provider buttons. $db_link is a fake PDO that serves the case's reg_fields and reg_variants rows. Translations are the
case's map, else "T<id>" (ids listed in "missing" translate to null). A "quiet" case hides the PHP warning for a missing session row.
No database is used."""
import json, os, shutil, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))

RUN = r"""<?php
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = getenv('EPC_H_ROOT');
$case = json_decode(getenv('EPC_H_CASE'), true);
if (!empty($case['quiet'])) { error_reporting(0); }
function epc_h_t($key) {
    global $case;
    $key = (string) $key;
    if (in_array($key, $case['missing'] ?? array(), true)) { return null; }
    return array_key_exists($key, $case['strings'] ?? array()) ? $case['strings'][$key] : 'T' . $key;
}
function translate_str_by_id($key) { return epc_h_t($key); }
function translate_str_by_key($key) { return epc_h_t($key); }
if (array_key_exists('trade_name', $case)) {
    function epc_site_trade_name() { global $case; return $case['trade_name']; }
}
if (array_key_exists('site_profile', $case)) {
    function epc_portal_site_profile() { global $case; return $case['site_profile']; }
}
class EpcHStmt {
    private $rows; private $i = 0;
    function __construct($rows) { $this->rows = $rows; }
    function execute($p = array()) { return true; }
    function fetch() { return $this->i < count($this->rows) ? $this->rows[$this->i++] : false; }
    function fetchColumn() { return (string) count($this->rows); }
}
class EpcHDb {
    function prepare($sql) {
        global $case;
        if (strpos($sql, '`reg_variants`') !== false) { return new EpcHStmt($case['variants']); }
        $main = strpos($sql, '`main_flag` = ?') !== false ? 0 : 1;
        return new EpcHStmt(array_values(array_filter($case['fields'], function ($f) use ($main) { return (int) $f['main_flag'] === $main; })));
    }
}
class DP_User {
    static function getUserSession() { global $case; return $case['session']; }
    static function getUserId() { global $case; return $case['user_id']; }
    static function available_communications() { global $case; return $case['communications']; }
}
class DP_Config_H { public $min_password_len; public $domain_path; }
$DP_Config = new DP_Config_H();
$DP_Config->min_password_len = $case['min_password_len'];
$DP_Config->domain_path = $case['domain_path'];
$db_link = new EpcHDb();
$multilang_params = $case['multilang_params'];
ob_start();
require $_SERVER['DOCUMENT_ROOT'] . '/content/users/regform.php';
echo json_encode(ob_get_clean());
"""

SOCIAL = "<?php\nfunction epc_auth_login_context_for_ui($context) { global $case; return $case['ui']; }\n"
BUTTONS = "<?php\nfunction epc_oauth_buttons_render(array $opts) { global $case; return (string) $case['buttons']; }\n"


def docroot(case):
    work = tempfile.mkdtemp()
    users = os.path.join(work, "content/users")
    gp = os.path.join(work, "content/general_pages")
    os.makedirs(users)
    os.makedirs(gp)
    names = ["regform.php", "epc_countries.php", "epc_storefront_auth_layout.php", "users_agreement_module.php"]
    if case.get("enhanced", True):
        names.append("epc_registration_enhanced.php")
    for name in names:
        shutil.copy(os.path.join(root, "content/users", name), users)
    open(os.path.join(users, "dp_user.php"), "w").write("<?php\n")
    shutil.copy(os.path.join(root, "content/general_pages/epc_otp_modal.php"), gp)
    open(os.path.join(gp, "epc_auth_common.php"), "w").write("<?php\n")
    open(os.path.join(gp, "epc_auth_social.php"), "w").write(SOCIAL)
    if case.get("buttons") is not None:
        open(os.path.join(gp, "epc_oauth_buttons.php"), "w").write(BUTTONS)
    return work


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    goldens = {}
    for name, case in cases.items():
        work = docroot(case)
        run = os.path.join(work, "run.php")
        open(run, "w").write(RUN)
        env = dict(os.environ, EPC_H_ROOT=work, EPC_H_CASE=json.dumps(case))
        out = subprocess.run(["php", "-d", "display_errors=stderr", run], env=env, check=True, capture_output=True, text=True)
        shutil.rmtree(work)
        if out.stderr.strip():
            raise SystemExit(name + ": " + out.stderr)
        goldens[name] = json.loads(out.stdout)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
