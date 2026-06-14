<?php
/**
 * Financial depth — enterprise period management, foreign-currency
 * revaluation, ledger allocation rules, and accrual schemes.
 *
 * Additive: new epc_fin_* tables. Pure compute helpers (period generation,
 * allocation split, accrual schedule, FX revaluation delta) are separated from
 * persistence so they are deterministically testable. Multi-company aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_fin_adv_ensure_schema')) {
    function epc_fin_adv_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fin_periods` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `fy` int(11) NOT NULL DEFAULT 0,
            `period_no` int(11) NOT NULL DEFAULT 0,
            `start_date` int(11) NOT NULL DEFAULT 0,
            `end_date` int(11) NOT NULL DEFAULT 0,
            `status` varchar(12) NOT NULL DEFAULT 'open',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            UNIQUE KEY `x_period` (`company_id`,`fy`,`period_no`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Fiscal period calendar + status'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fin_alloc_rule` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `source_account` varchar(40) NOT NULL DEFAULT '',
            `basis` text,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Ledger allocation rules'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fin_alloc_run` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `rule_id` int(11) NOT NULL DEFAULT 0,
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `run_date` int(11) NOT NULL DEFAULT 0,
            `lines_json` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Allocation run results'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fin_accrual` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `description` varchar(200) NOT NULL DEFAULT '',
            `total_amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `periods` int(11) NOT NULL DEFAULT 1,
            `start_fy` int(11) NOT NULL DEFAULT 0,
            `start_period` int(11) NOT NULL DEFAULT 1,
            `status` varchar(12) NOT NULL DEFAULT 'open',
            `schedule_json` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Accrual schemes'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fin_fx_run` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `as_of` int(11) NOT NULL DEFAULT 0,
            `total_delta` decimal(18,2) NOT NULL DEFAULT 0.00,
            `lines_json` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='FX revaluation runs'");
    }
}

/* ---------------- period management ---------------- */

if (!function_exists('epc_fin_period_dates')) {
    /**
     * Pure: 12 monthly fiscal periods for a year given the fiscal start month
     * (1-12). Returns [['period_no','start_date','end_date'], ...].
     *
     * @return array<int,array{period_no:int,start_date:int,end_date:int}>
     */
    function epc_fin_period_dates(int $fy, int $startMonth = 1): array
    {
        if ($startMonth < 1 || $startMonth > 12) {
            $startMonth = 1;
        }
        $out = array();
        for ($p = 0; $p < 12; $p++) {
            $m = $startMonth + $p;
            $y = $fy + intdiv($m - 1, 12);
            $mm = (($m - 1) % 12) + 1;
            $start = mktime(0, 0, 0, $mm, 1, $y);
            $end = mktime(23, 59, 59, $mm, (int) date('t', $start), $y);
            $out[] = array('period_no' => $p + 1, 'start_date' => $start, 'end_date' => $end);
        }
        return $out;
    }
}

if (!function_exists('epc_fin_periods_generate')) {
    /** Generate/refresh a fiscal year's periods (idempotent: keeps existing status). */
    function epc_fin_periods_generate(PDO $db, int $companyId, int $fy, int $startMonth = 1): int
    {
        epc_fin_adv_ensure_schema($db);
        $count = 0;
        $ins = $db->prepare("INSERT INTO `epc_fin_periods` (`company_id`,`fy`,`period_no`,`start_date`,`end_date`,`status`,`time_created`)
                             VALUES (?,?,?,?,?,'open',?) ON DUPLICATE KEY UPDATE `start_date`=VALUES(`start_date`), `end_date`=VALUES(`end_date`)");
        foreach (epc_fin_period_dates($fy, $startMonth) as $pd) {
            $ins->execute(array($companyId, $fy, $pd['period_no'], $pd['start_date'], $pd['end_date'], time()));
            $count++;
        }
        return $count;
    }
}

if (!function_exists('epc_fin_period_set_status')) {
    function epc_fin_period_set_status(PDO $db, int $companyId, int $fy, int $periodNo, string $status): void
    {
        epc_fin_adv_ensure_schema($db);
        $valid = array('open', 'on_hold', 'closed');
        if (!in_array($status, $valid, true)) {
            throw new Exception('Invalid period status');
        }
        $db->prepare("UPDATE `epc_fin_periods` SET `status`=? WHERE `company_id`=? AND `fy`=? AND `period_no`=?")
           ->execute(array($status, $companyId, $fy, $periodNo));
    }
}

if (!function_exists('epc_fin_period_for_date')) {
    /** @return array<string,mixed>|null */
    function epc_fin_period_for_date(PDO $db, int $companyId, int $ts): ?array
    {
        epc_fin_adv_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_fin_periods` WHERE `company_id`=? AND `start_date`<=? AND `end_date`>=? LIMIT 1");
        $st->execute(array($companyId, $ts, $ts));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

if (!function_exists('epc_fin_posting_allowed')) {
    /** True if a posting date falls in an open period (unknown dates allowed). */
    function epc_fin_posting_allowed(PDO $db, int $companyId, int $ts): bool
    {
        $p = epc_fin_period_for_date($db, $companyId, $ts);
        if ($p === null) {
            return true;
        }
        return (string) $p['status'] === 'open';
    }
}

if (!function_exists('epc_fin_periods_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_fin_periods_list(PDO $db, int $companyId, int $fy = 0): array
    {
        epc_fin_adv_ensure_schema($db);
        $sql = "SELECT * FROM `epc_fin_periods` WHERE `company_id`=?";
        $args = array($companyId);
        if ($fy > 0) {
            $sql .= " AND `fy`=?";
            $args[] = $fy;
        }
        $sql .= " ORDER BY `fy`, `period_no`";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- allocations ---------------- */

if (!function_exists('epc_fin_alloc_split')) {
    /**
     * Pure: split an amount across destinations by weight. Rounds to 2dp and
     * forces the destination amounts to sum exactly to the source (remainder
     * onto the largest weight) so allocation journals always balance.
     *
     * @param array<string,float> $weights destination => weight
     * @return array<string,float> destination => allocated amount
     */
    function epc_fin_alloc_split(float $amount, array $weights): array
    {
        $total = array_sum($weights);
        if ($total <= 0) {
            return array();
        }
        $out = array();
        $running = 0.0;
        foreach ($weights as $dest => $w) {
            $a = round($amount * ($w / $total), 2);
            $out[$dest] = $a;
            $running += $a;
        }
        $diff = round($amount - $running, 2);
        if (abs($diff) >= 0.01) {
            $maxDest = null;
            $maxW = -INF;
            foreach ($weights as $dest => $w) {
                if ($w > $maxW) {
                    $maxW = $w;
                    $maxDest = $dest;
                }
            }
            if ($maxDest !== null) {
                $out[$maxDest] = round($out[$maxDest] + $diff, 2);
            }
        }
        return $out;
    }
}

if (!function_exists('epc_fin_alloc_rule_save')) {
    /** @param array<string,mixed> $data code, name, source_account, basis(array dest=>weight) */
    function epc_fin_alloc_rule_save(PDO $db, array $data, int $id = 0): int
    {
        epc_fin_adv_ensure_schema($db);
        $basis = $data['basis'] ?? array();
        $basisJson = json_encode(is_array($basis) ? $basis : array());
        if ($id > 0) {
            $db->prepare("UPDATE `epc_fin_alloc_rule` SET `code`=?, `name`=?, `source_account`=?, `basis`=?, `active`=? WHERE `id`=?")
               ->execute(array((string) ($data['code'] ?? ''), (string) ($data['name'] ?? ''), (string) ($data['source_account'] ?? ''), $basisJson, (int) ($data['active'] ?? 1), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_fin_alloc_rule` (`company_id`,`code`,`name`,`source_account`,`basis`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), (string) ($data['code'] ?? ''), (string) ($data['name'] ?? ''), (string) ($data['source_account'] ?? ''), $basisJson, (int) ($data['active'] ?? 1), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_fin_alloc_rules')) {
    /** @return array<int,array<string,mixed>> */
    function epc_fin_alloc_rules(PDO $db, int $companyId = 0): array
    {
        epc_fin_adv_ensure_schema($db);
        $sql = "SELECT * FROM `epc_fin_alloc_rule` WHERE 1=1";
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
            $r['basis_arr'] = json_decode((string) $r['basis'], true) ?: array();
        }
        return $rows;
    }
}

if (!function_exists('epc_fin_alloc_run')) {
    /** Execute an allocation rule for an amount; persists + returns the split lines. */
    function epc_fin_alloc_run(PDO $db, int $ruleId, float $amount): array
    {
        epc_fin_adv_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_fin_alloc_rule` WHERE id=?");
        $st->execute(array($ruleId));
        $rule = $st->fetch(PDO::FETCH_ASSOC);
        if (!$rule) {
            throw new Exception('Allocation rule not found');
        }
        $weights = json_decode((string) $rule['basis'], true) ?: array();
        $lines = epc_fin_alloc_split($amount, $weights);
        $db->prepare("INSERT INTO `epc_fin_alloc_run` (`company_id`,`rule_id`,`amount`,`run_date`,`lines_json`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array((int) $rule['company_id'], $ruleId, $amount, time(), json_encode($lines), time()));
        return $lines;
    }
}

/* ---------------- accruals ---------------- */

if (!function_exists('epc_fin_accrual_schedule')) {
    /**
     * Pure: straight-line accrual schedule of a total over n periods. Rounds to
     * 2dp; the rounding remainder goes onto the last period so the schedule
     * sums exactly to the total.
     *
     * @return array<int,float>
     */
    function epc_fin_accrual_schedule(float $total, int $periods): array
    {
        if ($periods < 1) {
            $periods = 1;
        }
        $per = round($total / $periods, 2);
        $out = array_fill(0, $periods, $per);
        $sum = round($per * $periods, 2);
        $diff = round($total - $sum, 2);
        if (abs($diff) >= 0.01) {
            $out[$periods - 1] = round($out[$periods - 1] + $diff, 2);
        }
        return $out;
    }
}

if (!function_exists('epc_fin_accrual_save')) {
    /** @param array<string,mixed> $data code, description, total_amount, periods, start_fy, start_period */
    function epc_fin_accrual_save(PDO $db, array $data, int $id = 0): int
    {
        epc_fin_adv_ensure_schema($db);
        $periods = max(1, (int) ($data['periods'] ?? 1));
        $sched = epc_fin_accrual_schedule((float) ($data['total_amount'] ?? 0), $periods);
        $startFy = (int) ($data['start_fy'] ?? 0);
        $startPeriod = max(1, (int) ($data['start_period'] ?? 1));
        $rows = array();
        for ($i = 0; $i < $periods; $i++) {
            $m = $startPeriod + $i;
            $fy = $startFy + intdiv($m - 1, 12);
            $pno = (($m - 1) % 12) + 1;
            $rows[] = array('seq' => $i + 1, 'fy' => $fy, 'period_no' => $pno, 'amount' => $sched[$i], 'reversed' => 0);
        }
        $schedJson = json_encode($rows);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_fin_accrual` SET `code`=?, `description`=?, `total_amount`=?, `periods`=?, `start_fy`=?, `start_period`=?, `schedule_json`=? WHERE `id`=?")
               ->execute(array((string) ($data['code'] ?? ''), (string) ($data['description'] ?? ''), (float) ($data['total_amount'] ?? 0), $periods, $startFy, $startPeriod, $schedJson, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_fin_accrual` (`company_id`,`code`,`description`,`total_amount`,`periods`,`start_fy`,`start_period`,`status`,`schedule_json`,`time_created`) VALUES (?,?,?,?,?,?,?,'open',?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), (string) ($data['code'] ?? ''), (string) ($data['description'] ?? ''), (float) ($data['total_amount'] ?? 0), $periods, $startFy, $startPeriod, $schedJson, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_fin_accruals')) {
    /** @return array<int,array<string,mixed>> */
    function epc_fin_accruals(PDO $db, int $companyId = 0): array
    {
        epc_fin_adv_ensure_schema($db);
        $sql = "SELECT * FROM `epc_fin_accrual` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['schedule'] = json_decode((string) $r['schedule_json'], true) ?: array();
        }
        return $rows;
    }
}

/* ---------------- FX revaluation ---------------- */

if (!function_exists('epc_fin_fx_reval_delta')) {
    /**
     * Pure: revaluation delta for one foreign-currency balance.
     *  revalued_lc = fcAmount * currentRate ; delta = revalued_lc - bookLc
     * Positive delta = unrealized gain (for an asset), negative = loss.
     *
     * @return array{revalued_lc:float,delta:float}
     */
    function epc_fin_fx_reval_delta(float $fcAmount, float $bookLc, float $currentRate): array
    {
        $reval = round($fcAmount * $currentRate, 2);
        return array('revalued_lc' => $reval, 'delta' => round($reval - $bookLc, 2));
    }
}

if (!function_exists('epc_fin_fx_revalue')) {
    /**
     * Run FX revaluation over a set of open FC balances and persist the run.
     *
     * @param array<int,array{account:string,currency:string,fc_amount:float,book_lc:float,rate:float}> $balances
     * @return array{run_id:int,total_delta:float,lines:array<int,array<string,mixed>>}
     */
    function epc_fin_fx_revalue(PDO $db, int $companyId, array $balances, int $asOf = 0): array
    {
        epc_fin_adv_ensure_schema($db);
        $lines = array();
        $totalDelta = 0.0;
        foreach ($balances as $b) {
            $d = epc_fin_fx_reval_delta((float) $b['fc_amount'], (float) $b['book_lc'], (float) $b['rate']);
            $line = array(
                'account' => (string) ($b['account'] ?? ''),
                'currency' => (string) ($b['currency'] ?? ''),
                'fc_amount' => (float) $b['fc_amount'],
                'book_lc' => (float) $b['book_lc'],
                'rate' => (float) $b['rate'],
                'revalued_lc' => $d['revalued_lc'],
                'delta' => $d['delta'],
                'effect' => $d['delta'] >= 0 ? 'gain' : 'loss',
            );
            $lines[] = $line;
            $totalDelta = round($totalDelta + $d['delta'], 2);
        }
        $asOf = $asOf > 0 ? $asOf : time();
        $db->prepare("INSERT INTO `epc_fin_fx_run` (`company_id`,`as_of`,`total_delta`,`lines_json`,`time_created`) VALUES (?,?,?,?,?)")
           ->execute(array($companyId, $asOf, $totalDelta, json_encode($lines), time()));
        return array('run_id' => (int) $db->lastInsertId(), 'total_delta' => $totalDelta, 'lines' => $lines);
    }
}

if (!function_exists('epc_fin_fx_runs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_fin_fx_runs(PDO $db, int $companyId = 0): array
    {
        epc_fin_adv_ensure_schema($db);
        $sql = "SELECT * FROM `epc_fin_fx_run` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['lines'] = json_decode((string) $r['lines_json'], true) ?: array();
        }
        return $rows;
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_fin_adv_summary')) {
    /** @return array{open_periods:int,closed_periods:int,alloc_rules:int,accruals:int,fx_runs:int} */
    function epc_fin_adv_summary(PDO $db, int $companyId = 0): array
    {
        epc_fin_adv_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        return array(
            'open_periods' => (int) $db->query("SELECT COUNT(*) FROM `epc_fin_periods` WHERE status='open'" . $wa)->fetchColumn(),
            'closed_periods' => (int) $db->query("SELECT COUNT(*) FROM `epc_fin_periods` WHERE status='closed'" . $wa)->fetchColumn(),
            'alloc_rules' => (int) $db->query("SELECT COUNT(*) FROM `epc_fin_alloc_rule` WHERE 1=1" . $wa)->fetchColumn(),
            'accruals' => (int) $db->query("SELECT COUNT(*) FROM `epc_fin_accrual` WHERE 1=1" . $wa)->fetchColumn(),
            'fx_runs' => (int) $db->query("SELECT COUNT(*) FROM `epc_fin_fx_run` WHERE 1=1" . $wa)->fetchColumn(),
        );
    }
}
