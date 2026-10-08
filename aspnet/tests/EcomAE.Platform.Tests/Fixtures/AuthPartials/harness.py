#!/usr/bin/env python3
"""Regenerates goldens.json by running the real content/users/users_agreement_module.php and
content/users/epc_storefront_auth_layout.php with php-cli. translate_str_by_id() is stubbed to return a marked string
with HTML and quotes, so the goldens show where PHP echoes translations unescaped. No database is used."""
import json, os, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))

RUN = r"""<?php
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = getenv('EPC_H_ROOT');
function translate_str_by_id($id) { return 'T' . $id . '<b>"\'&amp;'; }
$case = getenv('EPC_H_CASE');
ob_start();
if ($case === 'agreement') {
    $multilang_params = array('lang_href' => getenv('EPC_H_LANG'));
    require $_SERVER['DOCUMENT_ROOT'] . '/content/users/users_agreement_module.php';
} else {
    require $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_storefront_auth_layout.php';
    foreach (explode(',', getenv('EPC_H_STEPS')) as $step) {
        if ($step === 'css') { epc_storefront_auth_layout_css(); }
        elseif ($step === 'close') { epc_storefront_auth_layout_close(); }
        elseif ($step === 'open') { epc_storefront_auth_layout_open(); }
        else { epc_storefront_auth_layout_open(substr($step, 5)); }
        echo '|';
    }
}
echo json_encode(ob_get_clean());
"""

CASES = {
    "agreement_en": {"case": "agreement", "lang": "/en"},
    "agreement_root": {"case": "agreement", "lang": ""},
    "agreement_ar": {"case": "agreement", "lang": "/ar"},
    "layout_default_then_close": {"case": "layout", "steps": "open,close"},
    "layout_wide_twice": {"case": "layout", "steps": "open:wide,close,open:wide,close"},
    "layout_css_first": {"case": "layout", "steps": "css,css,open,close"},
    "layout_other_variant": {"case": "layout", "steps": "open:narrow,open:,close"},
}


def main():
    work = tempfile.mkdtemp()
    run = os.path.join(work, "run.php")
    open(run, "w").write(RUN)
    goldens = {}
    for name, case in CASES.items():
        env = dict(os.environ, EPC_H_ROOT=root, EPC_H_CASE=case["case"], EPC_H_LANG=case.get("lang", ""), EPC_H_STEPS=case.get("steps", ""))
        out = subprocess.run(["php", "-d", "display_errors=stderr", run], env=env, check=True, capture_output=True, text=True)
        if out.stderr.strip():
            raise SystemExit(name + ": " + out.stderr)
        goldens[name] = dict(case, html=json.loads(out.stdout))
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
