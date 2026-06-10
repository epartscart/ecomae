<?php
/**
 * Advanced ERP — Cost accounting, rebate management, enterprise asset
 * maintenance (EAM).
 *
 * Cost accounting: cost centers + allocation of shared/overhead costs across
 * centers by driver weights (headcount, area, usage…), with exact-reconciling
 * rounding.
 *
 * Rebates: supplier/customer rebate agreements (volume/value tiers) and accrual
 * of the earned rebate for a given turnover.
 *
 * EAM: maintenance assets, scheduled maintenance plans (interval based) and
 * work orders, with next-due computation.
 *
 * Additive: new epc_cc_*, epc_rbt_*, epc_eam_* tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

/* =========================== Cost accounting =========================== */

if (!function_exists('epc_cc_ensure_schema')) {
    function epc_cc_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cc_centers` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(30) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `branch_id` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cost centers'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cc_allocations` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `run_label` varchar(80) NOT NULL DEFAULT '',
            `source_cost` decimal(16,2) NOT NULL DEFAULT 0.00,
            `cost_center_id` int(11) NOT NULL DEFAULT 0,
            `amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_run` (`run_label`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cost allocation results'");
    }
}

if (!function_exists('epc_cc_center_save')) {
    /**
     * @param array<string,mixed> $data code, name, branch_id
     */
    function epc_cc_center_save(PDO $db, array $data, int $id = 0): int
    {
        epc_cc_ensure_schema($db);
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_cc_centers` SET `name`=?, `branch_id`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (int) ($data['branch_id'] ?? 0), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_cc_centers` (`code`,`name`,`branch_id`,`time_created`) VALUES (?,?,?,?)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (int) ($data['branch_id'] ?? 0), $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_cc_allocate')) {
    /**
     * Allocate a shared cost across cost centers by driver weights. The largest
     * weight absorbs any rounding remainder so allocations sum exactly to the
     * source cost.
     *
     * @param array<string|int,float> $weights centerKey => driver weight
     * @return array<string|int,float> centerKey => allocated amount
     */
    function epc_cc_allocate(float $cost, array $weights): array
    {
        $sum = array_sum($weights);
        if ($sum <= 0) {
            return array();
        }
        $out = array();
        $allocated = 0.0;
        $maxKey = null;
        $maxVal = -INF;
        foreach ($weights as $k => $w) {
            $amt = round($cost * ($w / $sum), 2);
            $out[$k] = $amt;
            $allocated = round($allocated + $amt, 2);
            if ($w > $maxVal) {
                $maxVal = $w;
                $maxKey = $k;
            }
        }
        $remainder = round($cost - $allocated, 2);
        if ($maxKey !== null && abs($remainder) >= 0.01) {
            $out[$maxKey] = round($out[$maxKey] + $remainder, 2);
        }
        return $out;
    }
}

if (!function_exists('epc_cc_post_allocation')) {
    /**
     * Persist an allocation run.
     *
     * @param array<int,float> $weightsByCenterId cost_center_id => weight
     * @return array<int,float> cost_center_id => amount
     */
    function epc_cc_post_allocation(PDO $db, string $runLabel, float $cost, array $weightsByCenterId): array
    {
        epc_cc_ensure_schema($db);
        $alloc = epc_cc_allocate($cost, $weightsByCenterId);
        $now = time();
        $ins = $db->prepare("INSERT INTO `epc_cc_allocations` (`run_label`,`source_cost`,`cost_center_id`,`amount`,`time_created`) VALUES (?,?,?,?,?)");
        foreach ($alloc as $ccId => $amt) {
            $ins->execute(array($runLabel, $cost, (int) $ccId, $amt, $now));
        }
        return $alloc;
    }
}

/* ============================== Rebates =============================== */

if (!function_exists('epc_rbt_ensure_schema')) {
    function epc_rbt_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbt_agreements` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `party_type` varchar(10) NOT NULL DEFAULT 'customer',
            `party_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `basis` varchar(10) NOT NULL DEFAULT 'value',
            `tiers_json` text,
            `start_date` int(11) NOT NULL DEFAULT 0,
            `end_date` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_party` (`party_type`,`party_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Rebate agreements'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbt_accruals` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `agreement_id` int(11) NOT NULL,
            `period_label` varchar(40) NOT NULL DEFAULT '',
            `turnover` decimal(16,2) NOT NULL DEFAULT 0.00,
            `rebate_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `rate_applied` decimal(7,3) NOT NULL DEFAULT 0.000,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_agreement` (`agreement_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Rebate accruals'");
    }
}

if (!function_exists('epc_rbt_agreement_save')) {
    /**
     * @param array<string,mixed> $data party_type, party_id, name, basis(value|qty), start_date, end_date
     * @param array<int,array{threshold:float,percent:float}> $tiers ascending by threshold
     */
    function epc_rbt_agreement_save(PDO $db, array $data, array $tiers, int $id = 0): int
    {
        epc_rbt_ensure_schema($db);
        usort($tiers, static function ($a, $b) {
            return $a['threshold'] <=> $b['threshold'];
        });
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_rbt_agreements` SET `name`=?, `basis`=?, `tiers_json`=?, `start_date`=?, `end_date`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (string) ($data['basis'] ?? 'value'), json_encode($tiers), (int) ($data['start_date'] ?? 0), (int) ($data['end_date'] ?? 0), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_rbt_agreements` (`party_type`,`party_id`,`name`,`basis`,`tiers_json`,`start_date`,`end_date`,`time_created`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array((string) ($data['party_type'] ?? 'customer'), (int) ($data['party_id'] ?? 0), (string) ($data['name'] ?? ''), (string) ($data['basis'] ?? 'value'), json_encode($tiers), (int) ($data['start_date'] ?? 0), (int) ($data['end_date'] ?? 0), $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_rbt_tier_rate')) {
    /**
     * Highest tier percent whose threshold is met by $turnover.
     *
     * @param array<int,array{threshold:float,percent:float}> $tiers
     */
    function epc_rbt_tier_rate(array $tiers, float $turnover): float
    {
        usort($tiers, static function ($a, $b) {
            return $a['threshold'] <=> $b['threshold'];
        });
        $rate = 0.0;
        foreach ($tiers as $t) {
            if ($turnover >= (float) $t['threshold']) {
                $rate = (float) $t['percent'];
            }
        }
        return $rate;
    }
}

if (!function_exists('epc_rbt_accrue')) {
    /**
     * Compute + persist a rebate accrual for a period turnover.
     *
     * @return array<string,mixed>
     */
    function epc_rbt_accrue(PDO $db, int $agreementId, string $periodLabel, float $turnover): array
    {
        epc_rbt_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_rbt_agreements` WHERE `id`=?");
        $st->execute(array($agreementId));
        $ag = $st->fetch(PDO::FETCH_ASSOC);
        if (!$ag) {
            throw new Exception('Rebate agreement not found');
        }
        $tiers = json_decode((string) $ag['tiers_json'], true) ?: array();
        $rate = epc_rbt_tier_rate($tiers, $turnover);
        $amount = round($turnover * $rate / 100, 2);
        $now = time();
        $db->prepare("INSERT INTO `epc_rbt_accruals` (`agreement_id`,`period_label`,`turnover`,`rebate_amount`,`rate_applied`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array($agreementId, $periodLabel, $turnover, $amount, $rate, $now));
        return array('agreement_id' => $agreementId, 'period' => $periodLabel, 'turnover' => round($turnover, 2), 'rate' => $rate, 'rebate_amount' => $amount);
    }
}

/* ============================ EAM (maintenance) ======================= */

if (!function_exists('epc_eam_ensure_schema')) {
    function epc_eam_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_eam_assets` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `location` varchar(160) DEFAULT NULL,
            `branch_id` int(11) NOT NULL DEFAULT 0,
            `status` varchar(16) NOT NULL DEFAULT 'active',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Maintainable assets'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_eam_plans` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `asset_id` int(11) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `interval_days` int(11) NOT NULL DEFAULT 0,
            `last_done` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            KEY `x_asset` (`asset_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Preventive maintenance plans'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_eam_work_orders` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `asset_id` int(11) NOT NULL,
            `plan_id` int(11) NOT NULL DEFAULT 0,
            `type` varchar(16) NOT NULL DEFAULT 'corrective',
            `description` text,
            `status` varchar(16) NOT NULL DEFAULT 'open',
            `cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `scheduled_for` int(11) NOT NULL DEFAULT 0,
            `completed_at` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_asset` (`asset_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Maintenance work orders'");
    }
}

if (!function_exists('epc_eam_asset_save')) {
    /**
     * @param array<string,mixed> $data code, name, location, branch_id
     */
    function epc_eam_asset_save(PDO $db, array $data, int $id = 0): int
    {
        epc_eam_ensure_schema($db);
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_eam_assets` SET `name`=?, `location`=?, `branch_id`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (string) ($data['location'] ?? ''), (int) ($data['branch_id'] ?? 0), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_eam_assets` (`code`,`name`,`location`,`branch_id`,`time_created`) VALUES (?,?,?,?,?)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (string) ($data['location'] ?? ''), (int) ($data['branch_id'] ?? 0), $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_eam_plan_save')) {
    /**
     * @param array<string,mixed> $data asset_id, name, interval_days, last_done
     */
    function epc_eam_plan_save(PDO $db, array $data, int $id = 0): int
    {
        epc_eam_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_eam_plans` SET `name`=?, `interval_days`=?, `last_done`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (int) ($data['interval_days'] ?? 0), (int) ($data['last_done'] ?? 0), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_eam_plans` (`asset_id`,`name`,`interval_days`,`last_done`,`active`) VALUES (?,?,?,?,1)")
           ->execute(array((int) $data['asset_id'], (string) ($data['name'] ?? ''), (int) ($data['interval_days'] ?? 0), (int) ($data['last_done'] ?? 0)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_eam_next_due')) {
    /**
     * Next-due date for a plan = last_done + interval_days. Returns due flag
     * relative to $asOf.
     *
     * @return array<string,mixed>
     */
    function epc_eam_next_due(PDO $db, int $planId, int $asOf = 0): array
    {
        epc_eam_ensure_schema($db);
        $asOf = $asOf > 0 ? $asOf : time();
        $st = $db->prepare("SELECT * FROM `epc_eam_plans` WHERE `id`=?");
        $st->execute(array($planId));
        $plan = $st->fetch(PDO::FETCH_ASSOC);
        if (!$plan) {
            throw new Exception('Plan not found');
        }
        $base = (int) $plan['last_done'] > 0 ? (int) $plan['last_done'] : (int) $asOf;
        $nextDue = $base + ((int) $plan['interval_days'] * 86400);
        return array(
            'plan_id' => $planId,
            'next_due' => $nextDue,
            'is_due' => $nextDue <= $asOf,
            'days_until' => (int) ceil(($nextDue - $asOf) / 86400),
        );
    }
}

if (!function_exists('epc_eam_wo_create')) {
    /**
     * @param array<string,mixed> $data asset_id, plan_id, type(preventive|corrective), description, scheduled_for
     */
    function epc_eam_wo_create(PDO $db, array $data): int
    {
        epc_eam_ensure_schema($db);
        $now = time();
        $db->prepare("INSERT INTO `epc_eam_work_orders` (`asset_id`,`plan_id`,`type`,`description`,`status`,`scheduled_for`,`time_created`) VALUES (?,?,?,?, 'open', ?, ?)")
           ->execute(array((int) $data['asset_id'], (int) ($data['plan_id'] ?? 0), (string) ($data['type'] ?? 'corrective'), (string) ($data['description'] ?? ''), (int) ($data['scheduled_for'] ?? 0), $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_eam_wo_complete')) {
    /**
     * Complete a maintenance WO; stamps the linked plan's last_done so the next
     * due date rolls forward.
     */
    function epc_eam_wo_complete(PDO $db, int $woId, float $cost = 0.0, int $completedAt = 0): void
    {
        epc_eam_ensure_schema($db);
        $completedAt = $completedAt > 0 ? $completedAt : time();
        $st = $db->prepare("SELECT * FROM `epc_eam_work_orders` WHERE `id`=?");
        $st->execute(array($woId));
        $wo = $st->fetch(PDO::FETCH_ASSOC);
        if (!$wo) {
            throw new Exception('Work order not found');
        }
        $db->prepare("UPDATE `epc_eam_work_orders` SET `status`='completed', `cost`=?, `completed_at`=? WHERE `id`=?")
           ->execute(array(round($cost, 2), $completedAt, $woId));
        if ((int) $wo['plan_id'] > 0) {
            $db->prepare("UPDATE `epc_eam_plans` SET `last_done`=? WHERE `id`=?")->execute(array($completedAt, (int) $wo['plan_id']));
        }
    }
}
