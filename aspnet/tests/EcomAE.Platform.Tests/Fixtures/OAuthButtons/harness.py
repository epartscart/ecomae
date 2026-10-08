#!/usr/bin/env python3
"""Regenerates goldens.json by calling the real epc_oauth_buttons_render() (content/general_pages/epc_oauth_buttons.php)
with php-cli for each case of cases.json. A temp docroot holds a copy of the buttons file and a providers stub made of the
real epc_oauth_provider_defs() and epc_auth_normalize_mode() source, with the enabled providers taken from the case.
A case is a list of configs rendered on one page, joined with "|". The random root id is replaced by XXXXXXXX."""
import json, os, re, shutil, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))


def function_source(path, name):
    src = open(os.path.join(root, path), encoding="utf-8").read()
    start = src.index("function " + name + "(")
    depth, i = 0, src.index("{", start)
    while True:
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0:
                return src[start:i + 1]
        i += 1


RUN = r"""<?php
$case = json_decode(getenv('EPC_H_CASE'), true);
$_COOKIE = $case['cookies'] ?? array();
require getenv('EPC_H_ROOT') . '/content/general_pages/epc_oauth_buttons.php';
$out = array();
foreach ($case['configs'] as $cfg) {
    $out[] = epc_oauth_buttons_render($cfg);
}
echo json_encode(preg_replace('/epc_social_[0-9a-f]{8}/', 'epc_social_XXXXXXXX', implode('|', $out)));
"""


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    stub = "<?php\nfunction epc_oauth_enabled_providers(): array { global $case; return $case['enabled']; }\n" \
        + function_source("content/general_pages/epc_oauth_providers.php", "epc_oauth_provider_defs") + "\n" \
        + function_source("content/general_pages/epc_auth_common.php", "epc_auth_normalize_mode") + "\n"
    goldens = {}
    for name, case in cases.items():
        work = tempfile.mkdtemp()
        gp = os.path.join(work, "content/general_pages")
        os.makedirs(gp)
        shutil.copy(os.path.join(root, "content/general_pages/epc_oauth_buttons.php"), gp)
        open(os.path.join(gp, "epc_oauth_providers.php"), "w").write(stub)
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
