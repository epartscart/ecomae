#!/usr/bin/env python3
"""Regenerates goldens.json by calling the real epc_otp_modal_render() (content/general_pages/epc_otp_modal.php) with
php-cli for each case of cases.json. A case is a list of configs rendered in order on one page (joined with "|"), so
the goldens show the stylesheet printed once. No database is used."""
import json, os, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))

RUN = r"""<?php
$_SERVER['DOCUMENT_ROOT'] = getenv('EPC_H_ROOT');
require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_otp_modal.php';
$out = array();
foreach (json_decode(getenv('EPC_H_CFGS'), true) as $cfg) {
    $out[] = epc_otp_modal_render($cfg);
}
echo json_encode(implode('|', $out));
"""


def main():
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    work = tempfile.mkdtemp()
    run = os.path.join(work, "run.php")
    open(run, "w").write(RUN)
    goldens = {}
    for name, cfgs in cases.items():
        env = dict(os.environ, EPC_H_ROOT=root, EPC_H_CFGS=json.dumps(cfgs))
        out = subprocess.run(["php", "-d", "display_errors=stderr", run], env=env, check=True, capture_output=True, text=True)
        if out.stderr.strip():
            raise SystemExit(name + ": " + out.stderr)
        goldens[name] = json.loads(out.stdout)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
