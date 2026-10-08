#!/usr/bin/env python3
"""Regenerates goldens.json from the Syncron policy PHP of PR #8 (branch devin/1782038638-missing-pr4-modules,
content/shop/finance/epc_erp_syncron_policy.php). That module never reached main; the owner accepted it as an ERP
enhancement, so its PHP is the behaviour reference for the pure policy maths: the service-level z table, safety
stock, reorder point and exponential smoothing (fed a daily series through a fake PDO). Results are printed with
4 decimals, the precision the C# side and the DECIMAL(14,4) columns keep."""
import json, os, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
BRANCH = "origin/devin/1782038638-missing-pr4-modules"
SOURCE = "content/shop/finance/epc_erp_syncron_policy.php"

RUN = r"""<?php
define('_ASTEXE_', 1);
$case = json_decode(getenv('EPC_H_CASE'), true);
class FakeStmt {
    private $rows = array();
    function __construct($rows) { $this->rows = $rows; }
    function execute($params = array()) { return true; }
    function fetchAll($mode = null) { return $this->rows; }
}
class FakePdo extends PDO {
    function __construct() {}
    #[\ReturnTypeWillChange]
    function prepare($sql, $options = array()) {
        global $case; $rows = array();
        foreach ($case['series'] ?? array() as $i => $q) { $rows[] = array('d' => 'day' . $i, 'qty' => (string) $q); }
        return new FakeStmt($rows);
    }
}
require getenv('EPC_H_SOURCE');
$f = function ($v) { return number_format((float) $v, 4, '.', ''); };
$z = epc_syncron_service_level_z((float) $case['service']);
$safety = epc_syncron_calculate_safety_stock((float) $case['daily'], (int) $case['lead'], $z);
echo json_encode(array(
    'z' => $f($z),
    'safety' => $f($safety),
    'rop' => $f(epc_syncron_calculate_reorder_point((float) $case['daily'], (int) $case['lead'], $safety)),
    'exponential' => $f(epc_syncron_demand_exponential(new FakePdo(), 1, 1, (float) $case['alpha'], 90)),
));
"""


def main():
    work = tempfile.mkdtemp()
    source = os.environ.get("EPC_SYNCRON_PHP")
    if not source:
        source = os.path.join(work, "syncron.php")
        php = subprocess.run(["git", "-C", root, "show", BRANCH + ":" + SOURCE], check=True, capture_output=True, text=True).stdout
        open(source, "w", encoding="utf-8").write(php)
    run = os.path.join(work, "run.php")
    open(run, "w").write(RUN)
    cases = json.load(open(os.path.join(here, "cases.json"), encoding="utf-8"))
    goldens = {}
    for name, case in cases.items():
        env = dict(os.environ, EPC_H_SOURCE=source, EPC_H_CASE=json.dumps(case))
        out = subprocess.run(["php", "-d", "display_errors=stderr", "-d", "error_reporting=E_ALL", run], env=env, capture_output=True, text=True)
        if out.returncode != 0 or out.stderr.strip():
            raise SystemExit(name + ": " + out.stderr + out.stdout)
        goldens[name] = json.loads(out.stdout)
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
