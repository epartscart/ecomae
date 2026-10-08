#!/usr/bin/env python3
"""Regenerates goldens.json from the real content/users/epc_countries.php (php-cli): the full lists, and the dial
prefix, address rules and code normalisation for each input of inputs.json."""
import json, os, subprocess, sys

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))

SCRIPT = r"""
define('_ASTEXE_', 1);
require $argv[1];
$inputs = json_decode(file_get_contents($argv[2]), true);
$pairs = function ($a) { $o = array(); foreach ($a as $k => $v) { $o[] = array((string) $k, $v); } return $o; };
$out = array(
    'iso3166' => $pairs(epc_countries_iso3166_alpha2()),
    'registration' => $pairs(epc_countries_registration_options()),
    'dial_codes' => $pairs(epc_countries_dial_codes()),
    'emirates' => epc_countries_uae_emirates(),
    'inputs' => array(),
);
foreach ($inputs as $in) {
    $out['inputs'][] = array('in' => $in, 'dial' => epc_countries_dial_prefix($in), 'meta' => epc_countries_address_meta($in), 'normalize' => epc_countries_normalize_code($in));
}
echo json_encode($out, JSON_UNESCAPED_UNICODE);
"""


def main():
    run = subprocess.run(["php", "-r", SCRIPT, os.path.join(root, "content/users/epc_countries.php"), os.path.join(here, "inputs.json")],
                         check=True, capture_output=True, text=True)
    out = os.path.join(here, "goldens.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(json.loads(run.stdout), f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("wrote", out)


if __name__ == "__main__":
    sys.exit(main())
