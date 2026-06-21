<?php
/**
 * Quality management — enterprise test plans, quality orders and
 * non-conformance (NCR).
 *
 * - Test plans: a named set of tests, each with a measurement type
 *   (quantitative with min/max range, or qualitative with an expected outcome).
 * - Quality orders: an inspection of a reference (PO/SO/production/item) against
 *   a test plan; results captured per test line; pure pass/fail evaluation
 *   (quantitative within range; qualitative equals expected) → overall verdict.
 * - Non-conformance (NCR): raised from a failed order or standalone, with
 *   severity, disposition (use-as-is / rework / scrap / return) and a
 *   corrective-action workflow (open → investigate → action → closed).
 *
 * Pure evaluation helpers are separated from persistence so they are
 * deterministic and unit-testable. Multi-company aware. Additive only.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_qm_ensure_schema')) {
    function epc_qm_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qm_plan` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Quality test plans'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qm_test` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `plan_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `test_type` varchar(12) NOT NULL DEFAULT 'quantitative',
            `unit` varchar(24) NOT NULL DEFAULT '',
            `min_val` decimal(18,4) DEFAULT NULL,
            `max_val` decimal(18,4) DEFAULT NULL,
            `expected` varchar(120) NOT NULL DEFAULT '',
            `sort` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_plan` (`plan_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Quality test plan lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qm_order` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `plan_id` int(11) NOT NULL DEFAULT 0,
            `ref_type` varchar(16) NOT NULL DEFAULT 'item',
            `ref_id` varchar(60) NOT NULL DEFAULT '',
            `item_id` int(11) NOT NULL DEFAULT 0,
            `qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `status` varchar(12) NOT NULL DEFAULT 'open',
            `verdict` varchar(12) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_plan` (`plan_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Quality orders'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qm_result` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `order_id` int(11) NOT NULL DEFAULT 0,
            `test_id` int(11) NOT NULL DEFAULT 0,
            `test_name` varchar(160) NOT NULL DEFAULT '',
            `value_num` decimal(18,4) DEFAULT NULL,
            `value_text` varchar(120) NOT NULL DEFAULT '',
            `result` varchar(8) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_order` (`order_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Quality order results'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qm_ncr` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `order_id` int(11) NOT NULL DEFAULT 0,
            `title` varchar(190) NOT NULL DEFAULT '',
            `severity` varchar(12) NOT NULL DEFAULT 'minor',
            `disposition` varchar(16) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'open',
            `corrective_action` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_closed` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Non-conformance reports'");
    }
}

/* ---------------- pure evaluation ---------------- */

if (!function_exists('epc_qm_eval_test')) {
    /**
     * Pure: evaluate one test result.
     *
     * @param array<string,mixed> $test test_type, min_val, max_val, expected
     * @param float|null $valueNum quantitative measurement
     * @param string $valueText qualitative outcome
     * @return string 'pass' | 'fail'
     */
    function epc_qm_eval_test(array $test, ?float $valueNum, string $valueText = ''): string
    {
        $type = (string) ($test['test_type'] ?? 'quantitative');
        if ($type === 'qualitative') {
            $exp = strtolower(trim((string) ($test['expected'] ?? '')));
            return $exp !== '' && strtolower(trim($valueText)) === $exp ? 'pass' : 'fail';
        }
        // quantitative: within [min,max] when bounds provided
        if ($valueNum === null) {
            return 'fail';
        }
        $min = $test['min_val'];
        $max = $test['max_val'];
        if ($min !== null && $valueNum < (float) $min - 1e-9) {
            return 'fail';
        }
        if ($max !== null && $valueNum > (float) $max + 1e-9) {
            return 'fail';
        }
        return 'pass';
    }
}

if (!function_exists('epc_qm_verdict')) {
    /**
     * Pure: overall verdict from a list of per-test results ('pass'/'fail').
     * Empty → '' (not yet evaluated); any fail → 'fail'; all pass → 'pass'.
     *
     * @param array<int,string> $results
     */
    function epc_qm_verdict(array $results): string
    {
        if (empty($results)) {
            return '';
        }
        foreach ($results as $r) {
            if ($r !== 'pass') {
                return 'fail';
            }
        }
        return 'pass';
    }
}

/* ---------------- test plans ---------------- */

if (!function_exists('epc_qm_plan_save')) {
    /** @param array<string,mixed> $data */
    function epc_qm_plan_save(PDO $db, int $companyId, array $data, int $id = 0): int
    {
        epc_qm_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Plan code is required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_qm_plan` SET `name`=?, `active`=?, `time_updated`=? WHERE id=? AND company_id=?")
               ->execute(array((string) ($data['name'] ?? ''), !empty($data['active']) ? 1 : 0, time(), $id, $companyId));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_qm_plan` (`company_id`,`code`,`name`,`active`,`time_updated`) VALUES (?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `active`=VALUES(`active`), `time_updated`=VALUES(`time_updated`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? ''), !empty($data['active']) ? 1 : 0, time()));
        $st = $db->prepare("SELECT id FROM `epc_qm_plan` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_qm_test_add')) {
    /** @param array<string,mixed> $data */
    function epc_qm_test_add(PDO $db, int $planId, array $data): int
    {
        epc_qm_ensure_schema($db);
        $type = (string) ($data['test_type'] ?? 'quantitative');
        if (!in_array($type, array('quantitative', 'qualitative'), true)) {
            throw new Exception('Invalid test type');
        }
        $min = ($data['min_val'] ?? '') === '' ? null : (float) $data['min_val'];
        $max = ($data['max_val'] ?? '') === '' ? null : (float) $data['max_val'];
        $db->prepare("INSERT INTO `epc_qm_test` (`plan_id`,`name`,`test_type`,`unit`,`min_val`,`max_val`,`expected`,`sort`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array($planId, (string) ($data['name'] ?? ''), $type, (string) ($data['unit'] ?? ''), $min, $max, (string) ($data['expected'] ?? ''), (int) ($data['sort'] ?? 0)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_qm_plan_tests')) {
    /** @return array<int,array<string,mixed>> */
    function epc_qm_plan_tests(PDO $db, int $planId): array
    {
        epc_qm_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_qm_test` WHERE plan_id=? ORDER BY sort ASC, id ASC");
        $st->execute(array($planId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_qm_plans')) {
    /** @return array<int,array<string,mixed>> */
    function epc_qm_plans(PDO $db, int $companyId = 0): array
    {
        epc_qm_ensure_schema($db);
        $sql = "SELECT * FROM `epc_qm_plan` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY code";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['test_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_qm_test` WHERE plan_id=" . (int) $r['id'])->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

/* ---------------- quality orders ---------------- */

if (!function_exists('epc_qm_order_create')) {
    /** @param array<string,mixed> $data plan_id, ref_type, ref_id, item_id, qty */
    function epc_qm_order_create(PDO $db, int $companyId, array $data): int
    {
        epc_qm_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_qm_order` (`company_id`,`plan_id`,`ref_type`,`ref_id`,`item_id`,`qty`,`status`,`verdict`,`time_created`) VALUES (?,?,?,?,?,?, 'open', '', ?)")
           ->execute(array($companyId, (int) ($data['plan_id'] ?? 0), (string) ($data['ref_type'] ?? 'item'), (string) ($data['ref_id'] ?? ''), (int) ($data['item_id'] ?? 0), (float) ($data['qty'] ?? 0), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_qm_order_record')) {
    /**
     * Record results for a quality order against its plan's tests, evaluate each
     * (pure) and set the overall verdict. Re-recording replaces prior results.
     *
     * @param array<int,array{value_num?:mixed,value_text?:string}> $values keyed by test_id
     * @return array{verdict:string,results:array<int,array<string,mixed>>}
     */
    function epc_qm_order_record(PDO $db, int $orderId, array $values): array
    {
        epc_qm_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_qm_order` WHERE id=?");
        $st->execute(array($orderId));
        $order = $st->fetch(PDO::FETCH_ASSOC);
        if (!$order) {
            throw new Exception('Quality order not found');
        }
        $tests = epc_qm_plan_tests($db, (int) $order['plan_id']);
        $db->prepare("DELETE FROM `epc_qm_result` WHERE order_id=?")->execute(array($orderId));
        $ins = $db->prepare("INSERT INTO `epc_qm_result` (`order_id`,`test_id`,`test_name`,`value_num`,`value_text`,`result`,`time_created`) VALUES (?,?,?,?,?,?,?)");
        $results = array();
        $verdictParts = array();
        foreach ($tests as $t) {
            $tid = (int) $t['id'];
            $v = $values[$tid] ?? array();
            $valNum = (isset($v['value_num']) && $v['value_num'] !== '') ? (float) $v['value_num'] : null;
            $valText = (string) ($v['value_text'] ?? '');
            $res = epc_qm_eval_test($t, $valNum, $valText);
            $verdictParts[] = $res;
            $ins->execute(array($orderId, $tid, (string) $t['name'], $valNum, $valText, $res, time()));
            $results[] = array('test_id' => $tid, 'test_name' => (string) $t['name'], 'result' => $res);
        }
        $verdict = epc_qm_verdict($verdictParts);
        $db->prepare("UPDATE `epc_qm_order` SET `status`='completed', `verdict`=? WHERE id=?")->execute(array($verdict, $orderId));
        return array('verdict' => $verdict, 'results' => $results);
    }
}

if (!function_exists('epc_qm_order_results')) {
    /** @return array<int,array<string,mixed>> */
    function epc_qm_order_results(PDO $db, int $orderId): array
    {
        epc_qm_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_qm_result` WHERE order_id=? ORDER BY id ASC");
        $st->execute(array($orderId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_qm_orders')) {
    /** @return array<int,array<string,mixed>> */
    function epc_qm_orders(PDO $db, int $companyId = 0, int $limit = 200): array
    {
        epc_qm_ensure_schema($db);
        $sql = "SELECT o.*, p.code AS plan_code, p.name AS plan_name FROM `epc_qm_order` o LEFT JOIN `epc_qm_plan` p ON p.id=o.plan_id WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND o.company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY o.id DESC LIMIT " . max(1, $limit);
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- non-conformance ---------------- */

if (!function_exists('epc_qm_ncr_create')) {
    /** @param array<string,mixed> $data order_id, title, severity, disposition */
    function epc_qm_ncr_create(PDO $db, int $companyId, array $data): int
    {
        epc_qm_ensure_schema($db);
        $sev = (string) ($data['severity'] ?? 'minor');
        if (!in_array($sev, array('minor', 'major', 'critical'), true)) {
            throw new Exception('Invalid severity');
        }
        $disp = (string) ($data['disposition'] ?? '');
        if ($disp !== '' && !in_array($disp, array('use_as_is', 'rework', 'scrap', 'return'), true)) {
            throw new Exception('Invalid disposition');
        }
        $db->prepare("INSERT INTO `epc_qm_ncr` (`company_id`,`order_id`,`title`,`severity`,`disposition`,`status`,`corrective_action`,`time_created`) VALUES (?,?,?,?,?, 'open', '', ?)")
           ->execute(array($companyId, (int) ($data['order_id'] ?? 0), (string) ($data['title'] ?? ''), $sev, $disp, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_qm_ncr_update')) {
    /** @param array<string,mixed> $data status, disposition, corrective_action */
    function epc_qm_ncr_update(PDO $db, int $id, array $data): void
    {
        epc_qm_ensure_schema($db);
        $status = (string) ($data['status'] ?? 'open');
        if (!in_array($status, array('open', 'investigate', 'action', 'closed'), true)) {
            throw new Exception('Invalid status');
        }
        $disp = (string) ($data['disposition'] ?? '');
        if ($disp !== '' && !in_array($disp, array('use_as_is', 'rework', 'scrap', 'return'), true)) {
            throw new Exception('Invalid disposition');
        }
        $closed = $status === 'closed' ? time() : 0;
        $db->prepare("UPDATE `epc_qm_ncr` SET `status`=?, `disposition`=?, `corrective_action`=?, `time_closed`=? WHERE id=?")
           ->execute(array($status, $disp, (string) ($data['corrective_action'] ?? ''), $closed, $id));
    }
}

if (!function_exists('epc_qm_ncrs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_qm_ncrs(PDO $db, int $companyId = 0): array
    {
        epc_qm_ensure_schema($db);
        $sql = "SELECT * FROM `epc_qm_ncr` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_qm_summary')) {
    /** @return array{plans:int,orders:int,passed:int,failed:int,open_ncr:int} */
    function epc_qm_summary(PDO $db, int $companyId = 0): array
    {
        epc_qm_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        return array(
            'plans' => (int) $db->query("SELECT COUNT(*) FROM `epc_qm_plan` WHERE 1=1" . $wa)->fetchColumn(),
            'orders' => (int) $db->query("SELECT COUNT(*) FROM `epc_qm_order` WHERE 1=1" . $wa)->fetchColumn(),
            'passed' => (int) $db->query("SELECT COUNT(*) FROM `epc_qm_order` WHERE verdict='pass'" . $wa)->fetchColumn(),
            'failed' => (int) $db->query("SELECT COUNT(*) FROM `epc_qm_order` WHERE verdict='fail'" . $wa)->fetchColumn(),
            'open_ncr' => (int) $db->query("SELECT COUNT(*) FROM `epc_qm_ncr` WHERE status!='closed'" . $wa)->fetchColumn(),
        );
    }
}
