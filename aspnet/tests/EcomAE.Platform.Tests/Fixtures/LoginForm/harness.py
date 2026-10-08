#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/users/loginform.php with php-cli for each case of cases.json.
Each case gets a temp docroot with copies of the page and its real includes (auth links, auth layout,
modules/login/login_form_general.php, the pass and epc_code tabs, the e-mail code modal) and stubs for DP_User, the
auth core, the login context and the provider buttons. $DP_Template->id is the case's template id. Translations are
the case's map, else "T<id>". No database is used."""
import json, os, shutil, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))

RUN = r"""<?php
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = getenv('EPC_H_ROOT');
$case = json_decode(getenv('EPC_H_CASE'), true);
function translate_str_by_id($key) {
    global $case;
    $key = (string) $key;
    return array_key_exists($key, $case['strings'] ?? array()) ? $case['strings'][$key] : 'T' . $key;
}
if (array_key_exists('site_profile', $case)) {
    function epc_portal_site_profile() { global $case; return $case['site_profile']; }
}
class DP_User {
    static function getUserSession() { global $case; return $case['session']; }
    static function getUserId() { global $case; return $case['user_id']; }
    static function available_communications() { global $case; return $case['communications']; }
}
class DP_Config_H { public $simple_register_available = false; }
class DP_Template_H { public $id; }
$DP_Config = new DP_Config_H();
$DP_Template = new DP_Template_H();
$DP_Template->id = $case['template_id'];
$multilang_params = $case['multilang_params'];
ob_start();
if (isset($case['general'])) {
    if (array_key_exists('postfix', $case['general'])) { $login_form_postfix = $case['general']['postfix']; }
    if (array_key_exists('target', $case['general'])) { $login_form_target = $case['general']['target']; }
    foreach (range(1, $case['general']['times'] ?? 1) as $i) {
        require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
        echo '|';
    }
} else {
    require $_SERVER['DOCUMENT_ROOT'] . '/content/users/loginform.php';
}
echo json_encode(ob_get_clean());
"""

SOCIAL = "<?php\nfunction epc_auth_login_context_for_ui($context) { global $case; return $case['ui']; }\n"
BUTTONS = "<?php\nfunction epc_oauth_buttons_render(array $opts) { global $case; return (string) $case['buttons']; }\n"


def docroot(case):
    work = tempfile.mkdtemp()
    users = os.path.join(work, "content/users")
    gp = os.path.join(work, "content/general_pages")
    login = os.path.join(work, "modules/login")
    for d in (users, gp, os.path.join(login, "pass"), os.path.join(login, "epc_code")):
        os.makedirs(d)
    for name in ("loginform.php", "epc_storefront_auth_layout.php"):
        shutil.copy(os.path.join(root, "content/users", name), users)
    open(os.path.join(users, "dp_user.php"), "w").write("<?php\n")
    for name in ("epc_storefront_auth_links.php", "epc_otp_modal.php"):
        shutil.copy(os.path.join(root, "content/general_pages", name), gp)
    open(os.path.join(gp, "epc_auth_common.php"), "w").write("<?php\n")
    open(os.path.join(gp, "epc_auth_social.php"), "w").write(SOCIAL)
    if case.get("buttons") is not None:
        open(os.path.join(gp, "epc_oauth_buttons.php"), "w").write(BUTTONS)
    shutil.copy(os.path.join(root, "modules/login/login_form_general.php"), login)
    for tab in ("pass", "epc_code"):
        shutil.copy(os.path.join(root, "modules/login", tab, "app.php"), os.path.join(login, tab))
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
