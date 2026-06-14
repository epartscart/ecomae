<?php
/**
 * Project accounting depth — enterprise project budgets vs actuals,
 * cost/revenue transactions, work-in-progress (WIP), and revenue recognition
 * (percentage-of-completion, completed-contract, straight-line).
 *
 * Additive: new epc_prja_* tables; complements epc_erp_projects.php (which
 * holds the project master + timesheets). Pure compute helpers (PoC, WIP,
 * recognition) are separated from persistence for deterministic tests.
 * Multi-company aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_prja_ensure_schema')) {
    function epc_prja_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_prja_budget` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `project_id` int(11) NOT NULL DEFAULT 0,
            `category` varchar(60) NOT NULL DEFAULT 'general',
            `cost_budget` decimal(18,2) NOT NULL DEFAULT 0.00,
            `revenue_budget` decimal(18,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_project` (`project_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Project budgets by category'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_prja_txn` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `project_id` int(11) NOT NULL DEFAULT 0,
            `txn_type` varchar(12) NOT NULL DEFAULT 'cost',
            `category` varchar(60) NOT NULL DEFAULT 'general',
            `description` varchar(200) NOT NULL DEFAULT '',
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `txn_date` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_project` (`project_id`),
            KEY `x_type` (`txn_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Project cost/revenue/billing transactions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_prja_recognition` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `project_id` int(11) NOT NULL DEFAULT 0,
            `method` varchar(16) NOT NULL DEFAULT 'poc',
            `as_of` int(11) NOT NULL DEFAULT 0,
            `pct_complete` decimal(7,4) NOT NULL DEFAULT 0.0000,
            `recognized_revenue` decimal(18,2) NOT NULL DEFAULT 0.00,
            `recognized_cost` decimal(18,2) NOT NULL DEFAULT 0.00,
            `wip` decimal(18,2) NOT NULL DEFAULT 0.00,
            `detail_json` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_project` (`project_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Project revenue recognition runs'");
    }
}

/* ---------------- budgets ---------------- */

if (!function_exists('epc_prja_budget_save')) {
    /** @param array<string,mixed> $data project_id, category, cost_budget, revenue_budget */
    function epc_prja_budget_save(PDO $db, array $data, int $id = 0): int
    {
        epc_prja_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_prja_budget` SET `project_id`=?, `category`=?, `cost_budget`=?, `revenue_budget`=? WHERE `id`=?")
               ->execute(array((int) ($data['project_id'] ?? 0), (string) ($data['category'] ?? 'general'), (float) ($data['cost_budget'] ?? 0), (float) ($data['revenue_budget'] ?? 0), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_prja_budget` (`company_id`,`project_id`,`category`,`cost_budget`,`revenue_budget`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['project_id'] ?? 0), (string) ($data['category'] ?? 'general'), (float) ($data['cost_budget'] ?? 0), (float) ($data['revenue_budget'] ?? 0), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_prja_budgets')) {
    /** @return array<int,array<string,mixed>> */
    function epc_prja_budgets(PDO $db, int $projectId): array
    {
        epc_prja_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_prja_budget` WHERE project_id=? ORDER BY category");
        $st->execute(array($projectId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_prja_budget_totals')) {
    /** @return array{cost_budget:float,revenue_budget:float} */
    function epc_prja_budget_totals(PDO $db, int $projectId): array
    {
        epc_prja_ensure_schema($db);
        $st = $db->prepare("SELECT COALESCE(SUM(cost_budget),0) c, COALESCE(SUM(revenue_budget),0) r FROM `epc_prja_budget` WHERE project_id=?");
        $st->execute(array($projectId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return array('cost_budget' => round((float) $row['c'], 2), 'revenue_budget' => round((float) $row['r'], 2));
    }
}

/* ---------------- transactions ---------------- */

if (!function_exists('epc_prja_txn_types')) {
    /** @return array<int,string> */
    function epc_prja_txn_types(): array
    {
        return array('cost', 'revenue', 'billing');
    }
}

if (!function_exists('epc_prja_txn_add')) {
    /** @param array<string,mixed> $data project_id, txn_type, category, description, amount, txn_date */
    function epc_prja_txn_add(PDO $db, array $data): int
    {
        epc_prja_ensure_schema($db);
        $type = (string) ($data['txn_type'] ?? 'cost');
        if (!in_array($type, epc_prja_txn_types(), true)) {
            throw new Exception('Invalid project transaction type');
        }
        $db->prepare("INSERT INTO `epc_prja_txn` (`company_id`,`project_id`,`txn_type`,`category`,`description`,`amount`,`txn_date`,`time_created`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['project_id'] ?? 0), $type, (string) ($data['category'] ?? 'general'), (string) ($data['description'] ?? ''), (float) ($data['amount'] ?? 0), (int) ($data['txn_date'] ?? time()), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_prja_txns')) {
    /** @return array<int,array<string,mixed>> */
    function epc_prja_txns(PDO $db, int $projectId): array
    {
        epc_prja_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_prja_txn` WHERE project_id=? ORDER BY txn_date DESC, id DESC");
        $st->execute(array($projectId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_prja_actuals')) {
    /** @return array{cost:float,revenue:float,billing:float} */
    function epc_prja_actuals(PDO $db, int $projectId): array
    {
        epc_prja_ensure_schema($db);
        $st = $db->prepare("SELECT txn_type, COALESCE(SUM(amount),0) amt FROM `epc_prja_txn` WHERE project_id=? GROUP BY txn_type");
        $st->execute(array($projectId));
        $out = array('cost' => 0.0, 'revenue' => 0.0, 'billing' => 0.0);
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $out[(string) $r['txn_type']] = round((float) $r['amt'], 2);
        }
        return $out;
    }
}

/* ---------------- pure: percent complete, recognition, WIP ---------------- */

if (!function_exists('epc_prja_pct_complete')) {
    /**
     * Pure: cost-to-cost percent complete = cost incurred / total estimated cost
     * (clamped to 0..1). estTotalCost falls back to cost incurred when unknown.
     */
    function epc_prja_pct_complete(float $costIncurred, float $estTotalCost): float
    {
        if ($estTotalCost <= 0) {
            return $costIncurred > 0 ? 1.0 : 0.0;
        }
        $p = $costIncurred / $estTotalCost;
        if ($p < 0) {
            $p = 0.0;
        }
        if ($p > 1) {
            $p = 1.0;
        }
        return round($p, 4);
    }
}

if (!function_exists('epc_prja_recognize')) {
    /**
     * Pure: revenue recognition for a project snapshot.
     *  - poc (percentage-of-completion): revenue = contract * pct; cost = incurred
     *  - completed: recognize nothing until pct>=1, then full contract & cost
     *  - straight_line: revenue = contract * (elapsed/duration)
     * WIP (asset) = recognized_revenue - billed (positive = unbilled/over-earned;
     * negative = billed in advance/deferred).
     *
     * @return array{method:string,pct_complete:float,recognized_revenue:float,recognized_cost:float,wip:float}
     */
    function epc_prja_recognize(string $method, float $contractValue, float $costIncurred, float $estTotalCost, float $billed, float $fraction = 0.0): array
    {
        $pct = epc_prja_pct_complete($costIncurred, $estTotalCost);
        $recRev = 0.0;
        $recCost = 0.0;
        switch ($method) {
            case 'completed':
                if ($pct >= 1.0) {
                    $recRev = $contractValue;
                    $recCost = $costIncurred;
                }
                break;
            case 'straight_line':
                $f = $fraction;
                if ($f < 0) {
                    $f = 0.0;
                }
                if ($f > 1) {
                    $f = 1.0;
                }
                $recRev = round($contractValue * $f, 2);
                $recCost = $costIncurred;
                break;
            case 'poc':
            default:
                $method = 'poc';
                $recRev = round($contractValue * $pct, 2);
                $recCost = $costIncurred;
                break;
        }
        return array(
            'method' => $method,
            'pct_complete' => $pct,
            'recognized_revenue' => round($recRev, 2),
            'recognized_cost' => round($recCost, 2),
            'wip' => round($recRev - $billed, 2),
        );
    }
}

if (!function_exists('epc_prja_recognition_run')) {
    /**
     * Run + persist a recognition snapshot for a project. Pulls contract value
     * from revenue budget and actuals from transactions; estTotalCost defaults
     * to the cost budget.
     *
     * @return array<string,mixed>
     */
    function epc_prja_recognition_run(PDO $db, int $companyId, int $projectId, string $method = 'poc', float $fraction = 0.0, int $asOf = 0): array
    {
        epc_prja_ensure_schema($db);
        $budget = epc_prja_budget_totals($db, $projectId);
        $actual = epc_prja_actuals($db, $projectId);
        $rec = epc_prja_recognize($method, $budget['revenue_budget'], $actual['cost'], $budget['cost_budget'], $actual['billing'], $fraction);
        $asOf = $asOf > 0 ? $asOf : time();
        $detail = array('budget' => $budget, 'actual' => $actual);
        $db->prepare("INSERT INTO `epc_prja_recognition` (`company_id`,`project_id`,`method`,`as_of`,`pct_complete`,`recognized_revenue`,`recognized_cost`,`wip`,`detail_json`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)")
           ->execute(array($companyId, $projectId, $rec['method'], $asOf, $rec['pct_complete'], $rec['recognized_revenue'], $rec['recognized_cost'], $rec['wip'], json_encode($detail), time()));
        $rec['id'] = (int) $db->lastInsertId();
        $rec['budget'] = $budget;
        $rec['actual'] = $actual;
        return $rec;
    }
}

if (!function_exists('epc_prja_recognitions')) {
    /** @return array<int,array<string,mixed>> */
    function epc_prja_recognitions(PDO $db, int $projectId): array
    {
        epc_prja_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_prja_recognition` WHERE project_id=? ORDER BY id DESC");
        $st->execute(array($projectId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- project P&L / variance ---------------- */

if (!function_exists('epc_prja_pnl')) {
    /**
     * Budget vs actual with variance + forecast margin.
     *
     * @return array<string,mixed>
     */
    function epc_prja_pnl(PDO $db, int $projectId): array
    {
        $budget = epc_prja_budget_totals($db, $projectId);
        $actual = epc_prja_actuals($db, $projectId);
        $pct = epc_prja_pct_complete($actual['cost'], $budget['cost_budget']);
        $budgetMargin = round($budget['revenue_budget'] - $budget['cost_budget'], 2);
        $actualMargin = round($actual['revenue'] - $actual['cost'], 2);
        return array(
            'project_id' => $projectId,
            'cost_budget' => $budget['cost_budget'],
            'revenue_budget' => $budget['revenue_budget'],
            'cost_actual' => $actual['cost'],
            'revenue_actual' => $actual['revenue'],
            'billed' => $actual['billing'],
            'cost_variance' => round($budget['cost_budget'] - $actual['cost'], 2),
            'budget_margin' => $budgetMargin,
            'actual_margin' => $actualMargin,
            'pct_complete' => $pct,
            'over_budget' => $budget['cost_budget'] > 0 && $actual['cost'] > $budget['cost_budget'],
        );
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_prja_summary')) {
    /** @return array{projects_with_budget:int,cost_budget:float,revenue_budget:float,cost_actual:float,wip:float} */
    function epc_prja_summary(PDO $db, int $companyId = 0): array
    {
        epc_prja_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        $wip = (float) $db->query("SELECT COALESCE(SUM(wip),0) FROM `epc_prja_recognition` r WHERE r.id IN (SELECT MAX(id) FROM `epc_prja_recognition` GROUP BY project_id)" . ($companyId > 0 ? " AND r.company_id=" . $companyId : ""))->fetchColumn();
        return array(
            'projects_with_budget' => (int) $db->query("SELECT COUNT(DISTINCT project_id) FROM `epc_prja_budget` WHERE 1=1" . $wa)->fetchColumn(),
            'cost_budget' => (float) $db->query("SELECT COALESCE(SUM(cost_budget),0) FROM `epc_prja_budget` WHERE 1=1" . $wa)->fetchColumn(),
            'revenue_budget' => (float) $db->query("SELECT COALESCE(SUM(revenue_budget),0) FROM `epc_prja_budget` WHERE 1=1" . $wa)->fetchColumn(),
            'cost_actual' => (float) $db->query("SELECT COALESCE(SUM(amount),0) FROM `epc_prja_txn` WHERE txn_type='cost'" . $wa)->fetchColumn(),
            'wip' => round($wip, 2),
        );
    }
}
