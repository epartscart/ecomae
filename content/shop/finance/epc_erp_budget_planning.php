<?php
/**
 * Budgeting depth — budget planning & forecast positions.
 *
 * Adds the D365 budget-planning layer above the existing budget register:
 *   budget plan -> worksheet lines (per scenario/period) + forecast positions
 *   (planned headcount) -> staged workflow (draft -> review -> approved ->
 *   published). Publishing freezes the approved total for the budget register.
 *
 * Additive: new epc_bplan_* tables. Multi-company aware. No hard-coded amounts.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_bplan_ensure_schema')) {
    function epc_bplan_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_bplan_plan` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `fiscal_year` varchar(16) NOT NULL DEFAULT '',
            `stage` varchar(20) NOT NULL DEFAULT 'draft',
            `owner` varchar(160) NOT NULL DEFAULT '',
            `notes` text,
            `published_total` decimal(18,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_stage` (`stage`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Budget plans'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_bplan_line` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `plan_id` int(11) NOT NULL DEFAULT 0,
            `account` varchar(60) NOT NULL DEFAULT '',
            `dimension` varchar(120) NOT NULL DEFAULT '',
            `scenario` varchar(40) NOT NULL DEFAULT 'base',
            `period` varchar(20) NOT NULL DEFAULT '',
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            PRIMARY KEY (`id`),
            KEY `x_plan` (`plan_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Budget plan worksheet lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_bplan_position` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `plan_id` int(11) NOT NULL DEFAULT 0,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `title` varchar(160) NOT NULL DEFAULT '',
            `department` varchar(120) NOT NULL DEFAULT '',
            `headcount` int(11) NOT NULL DEFAULT 1,
            `annual_cost` decimal(18,2) NOT NULL DEFAULT 0.00,
            `start_period` varchar(20) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            KEY `x_plan` (`plan_id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Budget forecast positions'");
    }
}

if (!function_exists('epc_bplan_stages')) {
    /** @return array<int,string> ordered workflow stages */
    function epc_bplan_stages(): array
    {
        return array('draft', 'review', 'approved', 'published');
    }
}

if (!function_exists('epc_bplan_save')) {
    /** @param array<string,mixed> $data company_id, name, fiscal_year, owner, notes */
    function epc_bplan_save(PDO $db, array $data, int $id = 0): int
    {
        epc_bplan_ensure_schema($db);
        $name = trim((string) ($data['name'] ?? ''));
        if ($name === '') {
            throw new Exception('Plan name is required');
        }
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_bplan_plan` SET `name`=?, `fiscal_year`=?, `owner`=?, `notes`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array($name, (string) ($data['fiscal_year'] ?? ''), (string) ($data['owner'] ?? ''), (string) ($data['notes'] ?? ''), $now, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_bplan_plan` (`company_id`,`name`,`fiscal_year`,`stage`,`owner`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,'draft',?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $name, (string) ($data['fiscal_year'] ?? ''), (string) ($data['owner'] ?? ''), (string) ($data['notes'] ?? ''), $now, $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_bplan_get')) {
    /** @return array<string,mixed>|null */
    function epc_bplan_get(PDO $db, int $id): ?array
    {
        epc_bplan_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_bplan_plan` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_bplan_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_bplan_list(PDO $db, int $companyId, string $stage = ''): array
    {
        epc_bplan_ensure_schema($db);
        $sql = "SELECT * FROM `epc_bplan_plan` WHERE `company_id`=?";
        $args = array($companyId);
        if ($stage !== '') {
            $sql .= " AND `stage`=?";
            $args[] = $stage;
        }
        $sql .= " ORDER BY `id` DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_bplan_advance_stage')) {
    /** Advance a plan one stage along the ordered workflow (cannot skip or go back). */
    function epc_bplan_advance_stage(PDO $db, int $id): string
    {
        epc_bplan_ensure_schema($db);
        $plan = epc_bplan_get($db, $id);
        if (!$plan) {
            throw new Exception('Plan not found');
        }
        $stages = epc_bplan_stages();
        $idx = array_search($plan['stage'], $stages, true);
        if ($idx === false || $idx >= count($stages) - 1) {
            throw new Exception('Plan is already at the final stage');
        }
        $next = $stages[$idx + 1];
        if ($next === 'published') {
            return epc_bplan_publish($db, $id);
        }
        $db->prepare("UPDATE `epc_bplan_plan` SET `stage`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($next, time(), $id));
        return $next;
    }
}

if (!function_exists('epc_bplan_publish')) {
    /** Publish an approved plan: freeze the total (lines + positions) and lock stage. */
    function epc_bplan_publish(PDO $db, int $id): string
    {
        epc_bplan_ensure_schema($db);
        $plan = epc_bplan_get($db, $id);
        if (!$plan) {
            throw new Exception('Plan not found');
        }
        if ($plan['stage'] !== 'approved') {
            throw new Exception('Only an approved plan can be published');
        }
        $total = epc_bplan_total($db, $id) + epc_bplan_positions_cost($db, $id);
        $db->prepare("UPDATE `epc_bplan_plan` SET `stage`='published', `published_total`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($total, time(), $id));
        return 'published';
    }
}

if (!function_exists('epc_bplan_line_add')) {
    /** @param array<string,mixed> $data account, dimension, scenario, period, amount */
    function epc_bplan_line_add(PDO $db, int $planId, array $data): int
    {
        epc_bplan_ensure_schema($db);
        $plan = epc_bplan_get($db, $planId);
        if (!$plan) {
            throw new Exception('Plan not found');
        }
        if (!in_array($plan['stage'], array('draft', 'review'), true)) {
            throw new Exception('Lines can only be edited while the plan is in draft or review');
        }
        $db->prepare("INSERT INTO `epc_bplan_line` (`plan_id`,`account`,`dimension`,`scenario`,`period`,`amount`) VALUES (?,?,?,?,?,?)")
           ->execute(array($planId, (string) ($data['account'] ?? ''), (string) ($data['dimension'] ?? ''), (string) ($data['scenario'] ?? 'base'), (string) ($data['period'] ?? ''), (float) ($data['amount'] ?? 0)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_bplan_lines')) {
    /** @return array<int,array<string,mixed>> */
    function epc_bplan_lines(PDO $db, int $planId, string $scenario = ''): array
    {
        epc_bplan_ensure_schema($db);
        $sql = "SELECT * FROM `epc_bplan_line` WHERE `plan_id`=?";
        $args = array($planId);
        if ($scenario !== '') {
            $sql .= " AND `scenario`=?";
            $args[] = $scenario;
        }
        $sql .= " ORDER BY `id` ASC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_bplan_total')) {
    /** Sum of plan worksheet lines, optionally for one scenario. */
    function epc_bplan_total(PDO $db, int $planId, string $scenario = ''): float
    {
        epc_bplan_ensure_schema($db);
        $sql = "SELECT COALESCE(SUM(`amount`),0) FROM `epc_bplan_line` WHERE `plan_id`=?";
        $args = array($planId);
        if ($scenario !== '') {
            $sql .= " AND `scenario`=?";
            $args[] = $scenario;
        }
        $st = $db->prepare($sql);
        $st->execute($args);
        return (float) $st->fetchColumn();
    }
}

if (!function_exists('epc_bplan_position_add')) {
    /** @param array<string,mixed> $data title, department, headcount, annual_cost, start_period */
    function epc_bplan_position_add(PDO $db, int $planId, array $data): int
    {
        epc_bplan_ensure_schema($db);
        $plan = epc_bplan_get($db, $planId);
        if (!$plan) {
            throw new Exception('Plan not found');
        }
        if (!in_array($plan['stage'], array('draft', 'review'), true)) {
            throw new Exception('Positions can only be edited while the plan is in draft or review');
        }
        $title = trim((string) ($data['title'] ?? ''));
        if ($title === '') {
            throw new Exception('Position title is required');
        }
        $db->prepare("INSERT INTO `epc_bplan_position` (`plan_id`,`company_id`,`title`,`department`,`headcount`,`annual_cost`,`start_period`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array($planId, (int) $plan['company_id'], $title, (string) ($data['department'] ?? ''), max(1, (int) ($data['headcount'] ?? 1)), (float) ($data['annual_cost'] ?? 0), (string) ($data['start_period'] ?? '')));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_bplan_positions')) {
    /** @return array<int,array<string,mixed>> */
    function epc_bplan_positions(PDO $db, int $planId): array
    {
        epc_bplan_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_bplan_position` WHERE `plan_id`=? ORDER BY `id` ASC");
        $st->execute(array($planId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_bplan_positions_cost')) {
    /** Total planned position cost = sum(headcount * annual_cost). */
    function epc_bplan_positions_cost(PDO $db, int $planId): float
    {
        epc_bplan_ensure_schema($db);
        $st = $db->prepare("SELECT COALESCE(SUM(`headcount` * `annual_cost`),0) FROM `epc_bplan_position` WHERE `plan_id`=?");
        $st->execute(array($planId));
        return (float) $st->fetchColumn();
    }
}

if (!function_exists('epc_bplan_summary')) {
    /** @return array<string,mixed> */
    function epc_bplan_summary(PDO $db, int $companyId): array
    {
        epc_bplan_ensure_schema($db);
        $out = array('draft' => 0, 'review' => 0, 'approved' => 0, 'published' => 0, 'plans' => 0, 'published_total' => 0.0);
        $st = $db->prepare("SELECT `stage`, COUNT(*) c, COALESCE(SUM(`published_total`),0) v FROM `epc_bplan_plan` WHERE `company_id`=? GROUP BY `stage`");
        $st->execute(array($companyId));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $s = (string) $r['stage'];
            if (isset($out[$s])) {
                $out[$s] = (int) $r['c'];
            }
            $out['plans'] += (int) $r['c'];
            if ($s === 'published') {
                $out['published_total'] += (float) $r['v'];
            }
        }
        return $out;
    }
}
