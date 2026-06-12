<?php
/**
 * Advanced ERP — Projects & timesheets, Quality Control, Contracts.
 *
 * - Projects: tasks, timesheets, budget vs actual cost, billing (T&M / fixed),
 *   % complete.
 * - Quality control: inspection plans, inspections with pass/fail samples,
 *   AQL-style accept/reject decision.
 * - Contracts: contracts with value, milestones, renewal/expiry tracking.
 *
 * Additive: new epc_prj_*, epc_qc_*, epc_con_* tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_prj_ensure_schema')) {
    function epc_prj_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_prj_projects` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `billing_type` varchar(12) NOT NULL DEFAULT 'tm',
            `budget_cost` decimal(16,2) NOT NULL DEFAULT 0.00,
            `contract_value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `status` varchar(12) NOT NULL DEFAULT 'open',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Projects'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_prj_tasks` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `project_id` int(11) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `planned_hours` decimal(10,2) NOT NULL DEFAULT 0.00,
            `percent_complete` decimal(5,2) NOT NULL DEFAULT 0.00,
            `status` varchar(12) NOT NULL DEFAULT 'open',
            PRIMARY KEY (`id`),
            KEY `x_proj` (`project_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Project tasks'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_prj_timesheets` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `project_id` int(11) NOT NULL,
            `task_id` int(11) NOT NULL DEFAULT 0,
            `employee_id` int(11) NOT NULL DEFAULT 0,
            `work_date` int(11) NOT NULL DEFAULT 0,
            `hours` decimal(8,2) NOT NULL DEFAULT 0.00,
            `cost_rate` decimal(12,2) NOT NULL DEFAULT 0.00,
            `bill_rate` decimal(12,2) NOT NULL DEFAULT 0.00,
            `billable` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            KEY `x_proj` (`project_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Timesheets'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qc_inspections` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `reference` varchar(60) NOT NULL DEFAULT '',
            `entity_type` varchar(32) NOT NULL DEFAULT '',
            `entity_id` int(11) NOT NULL DEFAULT 0,
            `lot_size` int(11) NOT NULL DEFAULT 0,
            `sample_size` int(11) NOT NULL DEFAULT 0,
            `defects` int(11) NOT NULL DEFAULT 0,
            `accept_on` int(11) NOT NULL DEFAULT 0,
            `result` varchar(12) NOT NULL DEFAULT 'pending',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_entity` (`entity_type`,`entity_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='QC inspections'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_con_contracts` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `title` varchar(190) NOT NULL DEFAULT '',
            `party_type` varchar(12) NOT NULL DEFAULT 'customer',
            `party_id` int(11) NOT NULL DEFAULT 0,
            `value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `start_date` int(11) NOT NULL DEFAULT 0,
            `end_date` int(11) NOT NULL DEFAULT 0,
            `auto_renew` tinyint(1) NOT NULL DEFAULT 0,
            `status` varchar(12) NOT NULL DEFAULT 'active',
            `milestones` mediumtext,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Contracts'");
    }
}

/* ------------------------------ Projects ------------------------------ */

if (!function_exists('epc_prj_save')) {
    /** @param array<string,mixed> $data */
    function epc_prj_save(PDO $db, array $data, int $id = 0): int
    {
        epc_prj_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_prj_projects` SET `name`=?, `customer_id`=?, `billing_type`=?, `budget_cost`=?, `contract_value`=?, `status`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (int) ($data['customer_id'] ?? 0), (string) ($data['billing_type'] ?? 'tm'), (float) ($data['budget_cost'] ?? 0), (float) ($data['contract_value'] ?? 0), (string) ($data['status'] ?? 'open'), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_prj_projects` (`code`,`name`,`customer_id`,`billing_type`,`budget_cost`,`contract_value`,`status`,`time_created`) VALUES (?,?,?,?,?,?, 'open', ?)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (int) ($data['customer_id'] ?? 0), (string) ($data['billing_type'] ?? 'tm'), (float) ($data['budget_cost'] ?? 0), (float) ($data['contract_value'] ?? 0), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_prj_task_save')) {
    /** @param array<string,mixed> $data */
    function epc_prj_task_save(PDO $db, int $projectId, array $data, int $id = 0): int
    {
        epc_prj_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_prj_tasks` SET `name`=?, `planned_hours`=?, `percent_complete`=?, `status`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (float) ($data['planned_hours'] ?? 0), (float) ($data['percent_complete'] ?? 0), (string) ($data['status'] ?? 'open'), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_prj_tasks` (`project_id`,`name`,`planned_hours`,`percent_complete`,`status`) VALUES (?,?,?,?, 'open')")
           ->execute(array($projectId, (string) ($data['name'] ?? ''), (float) ($data['planned_hours'] ?? 0), (float) ($data['percent_complete'] ?? 0)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_prj_log_time')) {
    /** @param array<string,mixed> $data */
    function epc_prj_log_time(PDO $db, int $projectId, array $data): int
    {
        epc_prj_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_prj_timesheets` (`project_id`,`task_id`,`employee_id`,`work_date`,`hours`,`cost_rate`,`bill_rate`,`billable`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array($projectId, (int) ($data['task_id'] ?? 0), (int) ($data['employee_id'] ?? 0), (int) ($data['work_date'] ?? time()), (float) ($data['hours'] ?? 0), (float) ($data['cost_rate'] ?? 0), (float) ($data['bill_rate'] ?? 0), (int) ($data['billable'] ?? 1)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_prj_summary')) {
    /**
     * Cost vs budget, billable value, margin, % complete (avg of tasks).
     *
     * @return array<string,mixed>
     */
    function epc_prj_summary(PDO $db, int $projectId): array
    {
        epc_prj_ensure_schema($db);
        $p = $db->prepare("SELECT * FROM `epc_prj_projects` WHERE `id`=?");
        $p->execute(array($projectId));
        $proj = $p->fetch(PDO::FETCH_ASSOC);
        if (!$proj) {
            throw new Exception('Project not found');
        }
        $ts = $db->prepare("SELECT COALESCE(SUM(`hours`),0) hrs,
                                   COALESCE(SUM(`hours`*`cost_rate`),0) cost,
                                   COALESCE(SUM(CASE WHEN `billable`=1 THEN `hours`*`bill_rate` ELSE 0 END),0) bill
                            FROM `epc_prj_timesheets` WHERE `project_id`=?");
        $ts->execute(array($projectId));
        $agg = $ts->fetch(PDO::FETCH_ASSOC);
        $pc = $db->prepare("SELECT COALESCE(AVG(`percent_complete`),0) FROM `epc_prj_tasks` WHERE `project_id`=?");
        $pc->execute(array($projectId));
        $percent = round((float) $pc->fetchColumn(), 2);

        $cost = round((float) $agg['cost'], 2);
        $billable = round((float) $agg['bill'], 2);
        $value = (string) $proj['billing_type'] === 'fixed' ? (float) $proj['contract_value'] : $billable;
        return array(
            'project_id' => $projectId,
            'hours' => round((float) $agg['hrs'], 2),
            'cost' => $cost,
            'budget_cost' => round((float) $proj['budget_cost'], 2),
            'cost_variance' => round((float) $proj['budget_cost'] - $cost, 2),
            'over_budget' => $cost > (float) $proj['budget_cost'],
            'billable_value' => $billable,
            'revenue_recognized' => round($value * $percent / 100, 2),
            'margin' => round($value - $cost, 2),
            'percent_complete' => $percent,
        );
    }
}

if (!function_exists('epc_prj_list')) {
    /**
     * Projects newest-first, each with its live summary (cost/budget/margin/%).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_prj_list(PDO $db, int $limit = 200): array
    {
        epc_prj_ensure_schema($db);
        $rows = $db->query("SELECT * FROM `epc_prj_projects` ORDER BY `id` DESC LIMIT " . max(1, $limit))->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            try {
                $r['summary'] = epc_prj_summary($db, (int) $r['id']);
            } catch (Throwable $e) {
                $r['summary'] = array('cost' => 0, 'billable_value' => 0, 'margin' => 0, 'percent_complete' => 0, 'hours' => 0, 'over_budget' => false, 'revenue_recognized' => 0, 'cost_variance' => 0, 'budget_cost' => (float) $r['budget_cost']);
            }
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_prj_get')) {
    /** @return array<string,mixed>|null */
    function epc_prj_get(PDO $db, int $id): ?array
    {
        epc_prj_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_prj_projects` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_prj_tasks_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_prj_tasks_list(PDO $db, int $projectId): array
    {
        epc_prj_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_prj_tasks` WHERE `project_id`=? ORDER BY `id`");
        $st->execute(array($projectId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_prj_timesheets_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_prj_timesheets_list(PDO $db, int $projectId, int $limit = 100): array
    {
        epc_prj_ensure_schema($db);
        $st = $db->prepare("SELECT t.*, k.`name` AS task_name FROM `epc_prj_timesheets` t
                            LEFT JOIN `epc_prj_tasks` k ON k.`id`=t.`task_id`
                            WHERE t.`project_id`=? ORDER BY t.`id` DESC LIMIT " . max(1, $limit));
        $st->execute(array($projectId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_prj_portfolio')) {
    /**
     * Portfolio headline totals across all projects.
     *
     * @return array{projects:int,open:int,cost:float,billable:float,margin:float}
     */
    function epc_prj_portfolio(PDO $db): array
    {
        $rows = epc_prj_list($db, 500);
        $cost = $bill = $margin = 0.0;
        $open = 0;
        foreach ($rows as $r) {
            $cost += (float) $r['summary']['cost'];
            $bill += (float) $r['summary']['billable_value'];
            $margin += (float) $r['summary']['margin'];
            if ((string) $r['status'] === 'open') { $open++; }
        }
        return array('projects' => count($rows), 'open' => $open, 'cost' => round($cost, 2), 'billable' => round($bill, 2), 'margin' => round($margin, 2));
    }
}

/* --------------------------- Quality control -------------------------- */

if (!function_exists('epc_qc_inspect')) {
    /**
     * Record an inspection. Accept if defects <= accept_on (acceptance number).
     *
     * @param array<string,mixed> $data reference, entity_type, entity_id, lot_size, sample_size, defects, accept_on
     * @return array<string,mixed>
     */
    function epc_qc_inspect(PDO $db, array $data): array
    {
        epc_prj_ensure_schema($db);
        $defects = (int) ($data['defects'] ?? 0);
        $acceptOn = (int) ($data['accept_on'] ?? 0);
        $result = $defects <= $acceptOn ? 'accepted' : 'rejected';
        $db->prepare("INSERT INTO `epc_qc_inspections` (`reference`,`entity_type`,`entity_id`,`lot_size`,`sample_size`,`defects`,`accept_on`,`result`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)")
           ->execute(array((string) ($data['reference'] ?? ''), (string) ($data['entity_type'] ?? ''), (int) ($data['entity_id'] ?? 0), (int) ($data['lot_size'] ?? 0), (int) ($data['sample_size'] ?? 0), $defects, $acceptOn, $result, time()));
        return array('id' => (int) $db->lastInsertId(), 'result' => $result, 'defects' => $defects, 'accept_on' => $acceptOn);
    }
}

/* ------------------------------ Contracts ----------------------------- */

if (!function_exists('epc_con_save')) {
    /**
     * @param array<string,mixed> $data
     * @param array<int,array<string,mixed>> $milestones
     */
    function epc_con_save(PDO $db, array $data, array $milestones = array(), int $id = 0): int
    {
        epc_prj_ensure_schema($db);
        $ms = json_encode($milestones);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_con_contracts` SET `title`=?, `party_type`=?, `party_id`=?, `value`=?, `start_date`=?, `end_date`=?, `auto_renew`=?, `status`=?, `milestones`=? WHERE `id`=?")
               ->execute(array((string) ($data['title'] ?? ''), (string) ($data['party_type'] ?? 'customer'), (int) ($data['party_id'] ?? 0), (float) ($data['value'] ?? 0), (int) ($data['start_date'] ?? 0), (int) ($data['end_date'] ?? 0), (int) ($data['auto_renew'] ?? 0), (string) ($data['status'] ?? 'active'), $ms, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_con_contracts` (`code`,`title`,`party_type`,`party_id`,`value`,`start_date`,`end_date`,`auto_renew`,`status`,`milestones`,`time_created`) VALUES (?,?,?,?,?,?,?,?, 'active', ?, ?)")
           ->execute(array((string) $data['code'], (string) ($data['title'] ?? ''), (string) ($data['party_type'] ?? 'customer'), (int) ($data['party_id'] ?? 0), (float) ($data['value'] ?? 0), (int) ($data['start_date'] ?? 0), (int) ($data['end_date'] ?? 0), (int) ($data['auto_renew'] ?? 0), $ms, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_con_expiring')) {
    /**
     * Contracts expiring within $withinDays of $asOf (active only).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_con_expiring(PDO $db, int $withinDays = 30, int $asOf = 0): array
    {
        epc_prj_ensure_schema($db);
        $asOf = $asOf > 0 ? $asOf : time();
        $limit = $asOf + $withinDays * 86400;
        $st = $db->prepare("SELECT * FROM `epc_con_contracts` WHERE `status`='active' AND `end_date`>0 AND `end_date`<=? ORDER BY `end_date` ASC");
        $st->execute(array($limit));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}
