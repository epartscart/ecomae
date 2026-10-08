#!/usr/bin/env python3
"""Regenerates goldens.json by calling the real render functions of content/users/epc_registration_enhanced.php with
php-cli for each case of cases.json. Each case gets its own temp docroot holding copies of the registration and country
files plus stubs for the auth core, the storefront login context and the provider buttons. No database is used."""
import json, os, shutil, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))

RUN = r"""<?php
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = getenv('EPC_H_ROOT');
$case = json_decode(getenv('EPC_H_CASE'), true);
if (array_key_exists('trade_name', $case)) {
    function epc_site_trade_name() { global $case; return $case['trade_name']; }
}
require $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_enhanced.php';
ob_start();
switch ($case['fn']) {
    case 'social':
        epc_reg_render_social_block($case['params']);
        break;
    case 'country':
        call_user_func_array('epc_reg_render_country_select', $case['args']);
        break;
    case 'tabs':
        epc_reg_render_account_tabs();
        break;
    case 'uae':
        epc_reg_render_uae_panel();
        break;
}
echo json_encode(ob_get_clean());
"""

SOCIAL = r"""<?php
function epc_auth_login_context_for_ui($context) { return json_decode(getenv('EPC_H_UI'), true); }
"""

BUTTONS = r"""<?php
function epc_oauth_buttons_render(array $opts) { return (string) getenv('EPC_H_BUTTONS'); }
"""


def docroot(case):
    work = tempfile.mkdtemp()
    os.makedirs(os.path.join(work, "content/users"))
    os.makedirs(os.path.join(work, "content/general_pages"))
    for name in ("epc_registration_enhanced.php", "epc_countries.php"):
        shutil.copy(os.path.join(root, "content/users", name), os.path.join(work, "content/users", name))
    gp = os.path.join(work, "content/general_pages")
    if case.get("auth_available", True):
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
        env = dict(os.environ, EPC_H_ROOT=work, EPC_H_CASE=json.dumps(case), EPC_H_UI=json.dumps(case.get("ui", {})),
                   EPC_H_BUTTONS=case.get("buttons") or "")
        out = subprocess.run(["php", "-d", "display_errors=stderr", run], env=env, check=True, capture_output=True, text=True)
        shutil.rmtree(work)
        if out.stderr.strip():
            raise SystemExit(name + ": " + out.stderr)
        goldens[name] = json.loads(out.stdout)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
