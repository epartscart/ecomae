#!/usr/bin/env python3
"""Regenerates goldens.json from the PIM custom fields PHP of PR #8 (branch devin/1782038638-missing-pr4-modules,
content/shop/finance/epc_erp_pim_custom_fields.php). That module never reached main; the owner accepted it as an
ERP enhancement, so its PHP is the behaviour reference. Each case runs php-cli against a fake PDO that serves the
case's fields, options and item values, and records the form render, the display table render and the field
codes that the field save derives from the names."""
import json, os, subprocess, tempfile

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, "../../../../.."))
BRANCH = "origin/devin/1782038638-missing-pr4-modules"
SOURCE = "content/shop/finance/epc_erp_pim_custom_fields.php"

RUN = r"""<?php
define('_ASTEXE_', 1);
$case = json_decode(getenv('EPC_H_CASE'), true);

class FakeStmt {
    private $sql; private $rows = array();
    function __construct($sql) { $this->sql = $sql; }
    function execute($params = array()) { $this->rows = FakePdo::route($this->sql, $params); return true; }
    function fetchAll($mode = null) {
        if ($mode === PDO::FETCH_COLUMN) { return array_map(function ($r) { return reset($r); }, $this->rows); }
        return $this->rows;
    }
    function fetch($mode = null) { return $this->rows ? $this->rows[0] : false; }
    function fetchColumn() { return $this->rows ? reset($this->rows[0]) : false; }
}

class FakePdo extends PDO {
    public static $inserted = array();
    function __construct() {}
    #[\ReturnTypeWillChange]
    function prepare($sql, $options = array()) { return new FakeStmt($sql); }
    #[\ReturnTypeWillChange]
    function query($sql, $mode = null, ...$args) { $s = new FakeStmt($sql); $s->execute(); return $s; }
    #[\ReturnTypeWillChange]
    function exec($sql) { return 0; }
    #[\ReturnTypeWillChange]
    function lastInsertId($name = null) { return (string) count(self::$inserted); }

    static function fieldsById() {
        global $case; $m = array();
        foreach ($case['fields'] as $f) { $m[(int) $f['id']] = $f; }
        return $m;
    }
    static function route($sql, $params) {
        global $case;
        if (strpos($sql, 'INSERT INTO `epc_pim_fields`') !== false) { self::$inserted[] = $params; return array(); }
        if (strpos($sql, 'FROM `epc_pim_fields` f WHERE f.`active` = 1') !== false) {
            $out = array();
            foreach ($case['fields'] as $f) {
                foreach (array('inventory', 'sales', 'purchase') as $mod) {
                    if (strpos($sql, 'show_' . $mod . '` = 1') !== false && !(int) $f['show_' . $mod]) { continue 2; }
                }
                $f['option_count'] = count($case['options'][(string) $f['id']] ?? array());
                $out[] = $f;
            }
            return $out;
        }
        if (strpos($sql, 'SELECT * FROM `epc_pim_field_options` WHERE `field_id` = ?') !== false) {
            return $case['options'][(string) (int) $params[0]] ?? array();
        }
        if (strpos($sql, 'FROM `epc_pim_item_values` iv') !== false) {
            $byId = self::fieldsById(); $out = array();
            foreach ($case['fields'] as $f) {
                foreach ($case['values'] as $v) {
                    if ((int) $v['field_id'] !== (int) $f['id']) { continue; }
                    $out[] = array_merge($v, array('item_id' => $case['itemId'], 'field_name' => $f['name'], 'field_code' => $f['code'],
                        'field_type' => $f['field_type'], 'required' => $f['required'], 'show_inventory' => $f['show_inventory'],
                        'show_sales' => $f['show_sales'], 'show_purchase' => $f['show_purchase']));
                }
            }
            return $out;
        }
        if (strpos($sql, 'SELECT `label` FROM `epc_pim_field_options` WHERE `id` IN') !== false) {
            $out = array(); $ids = array_map('intval', $params);
            foreach ($case['options'] as $opts) {
                foreach ($opts as $o) { if (in_array((int) $o['id'], $ids, true)) { $out[] = $o; } }
            }
            usort($out, function ($a, $b) { return (int) $a['position'] - (int) $b['position']; });
            return array_map(function ($o) { return array('label' => $o['label']); }, $out);
        }
        throw new Exception('unrouted SQL: ' . $sql);
    }
}

require getenv('EPC_H_SOURCE');
$db = new FakePdo();
$codes = array();
foreach ($case['names'] ?? array() as $name) {
    epc_pim_field_save($db, array('name' => $name));
    $last = end(FakePdo::$inserted);
    $codes[] = $last[1];
}
echo json_encode(array(
    'form' => epc_pim_render_form_fields($db, $case['module'], (int) $case['itemId']),
    'table' => epc_pim_render_display_table($db, (int) $case['itemId'], $case['module']),
    'codes' => $codes,
));
"""


def main():
    source = os.environ.get("EPC_PIM_PHP")
    work = tempfile.mkdtemp()
    if not source:
        source = os.path.join(work, "pim.php")
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
    json.dump(goldens, open(os.path.join(here, "goldens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("wrote goldens.json")


if __name__ == "__main__":
    main()
