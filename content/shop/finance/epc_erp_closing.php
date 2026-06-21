<?php
/**
 * Advanced ERP — Fiscal periods, year-end closing & consolidation.
 *
 * - Fiscal years + periods with status (open / closed / locked). Posting guards
 *   can call epc_fy_is_open() to block entries into a closed period.
 * - Year-end close: compute net P&L for the year, post a closing entry that
 *   moves it to retained earnings, carry balance-sheet balances forward as the
 *   new year's opening, and lock the year.
 * - Consolidation: roll up GL balances across branches / business units /
 *   companies into one P&L + balance sheet, with inter-branch elimination and
 *   FX translation of foreign-entity balances to the group's base currency.
 *
 * Additive: new epc_fy_* tables. Reads existing GL via epc_erp_gl_* when present
 * but never alters GL structures.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_fy_ensure_schema')) {
    function epc_fy_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fy_years` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `label` varchar(40) NOT NULL,
            `start_date` int(11) NOT NULL DEFAULT 0,
            `end_date` int(11) NOT NULL DEFAULT 0,
            `status` varchar(10) NOT NULL DEFAULT 'open',
            `closed_at` int(11) NOT NULL DEFAULT 0,
            `retained_pl` decimal(16,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_label` (`label`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Fiscal years'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fy_periods` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `year_id` int(11) NOT NULL,
            `period_no` int(11) NOT NULL DEFAULT 0,
            `start_date` int(11) NOT NULL DEFAULT 0,
            `end_date` int(11) NOT NULL DEFAULT 0,
            `status` varchar(10) NOT NULL DEFAULT 'open',
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_period` (`year_id`,`period_no`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Fiscal periods'");
    }
}

if (!function_exists('epc_fy_create_year')) {
    /**
     * Create a fiscal year and optionally its monthly periods.
     */
    function epc_fy_create_year(PDO $db, string $label, int $startDate, int $endDate, bool $monthlyPeriods = true): int
    {
        epc_fy_ensure_schema($db);
        $now = time();
        $db->prepare("INSERT INTO `epc_fy_years` (`label`,`start_date`,`end_date`,`status`,`time_created`) VALUES (?,?,?, 'open', ?)
                      ON DUPLICATE KEY UPDATE `start_date`=VALUES(`start_date`), `end_date`=VALUES(`end_date`)")
           ->execute(array($label, $startDate, $endDate, $now));
        $yearId = (int) $db->lastInsertId();
        if ($yearId === 0) {
            $st = $db->prepare("SELECT `id` FROM `epc_fy_years` WHERE `label`=?");
            $st->execute(array($label));
            $yearId = (int) $st->fetchColumn();
        }
        if ($monthlyPeriods) {
            $cursor = $startDate;
            $p = 1;
            while ($cursor < $endDate && $p <= 12) {
                $pEnd = strtotime('+1 month', $cursor) - 1;
                if ($pEnd > $endDate) {
                    $pEnd = $endDate;
                }
                $db->prepare("INSERT INTO `epc_fy_periods` (`year_id`,`period_no`,`start_date`,`end_date`,`status`) VALUES (?,?,?,?, 'open')
                              ON DUPLICATE KEY UPDATE `start_date`=VALUES(`start_date`), `end_date`=VALUES(`end_date`)")
                   ->execute(array($yearId, $p, $cursor, $pEnd));
                $cursor = strtotime('+1 month', $cursor);
                $p++;
            }
        }
        return $yearId;
    }
}

if (!function_exists('epc_fy_is_open')) {
    /**
     * Is the fiscal period covering $date open for posting? If no fiscal year
     * is defined at all, returns true (period control not in use).
     */
    function epc_fy_is_open(PDO $db, int $date): bool
    {
        epc_fy_ensure_schema($db);
        $cnt = (int) $db->query("SELECT COUNT(*) FROM `epc_fy_years`")->fetchColumn();
        if ($cnt === 0) {
            return true;
        }
        $st = $db->prepare("SELECT `status` FROM `epc_fy_years` WHERE `start_date`<=? AND `end_date`>=? LIMIT 1");
        $st->execute(array($date, $date));
        $yStatus = $st->fetchColumn();
        if ($yStatus === false) {
            return false; // date falls outside any defined year -> block
        }
        if ($yStatus !== 'open') {
            return false;
        }
        // Period-level lock (if periods defined).
        $ps = $db->prepare("SELECT p.`status` FROM `epc_fy_periods` p JOIN `epc_fy_years` y ON y.id=p.year_id WHERE p.`start_date`<=? AND p.`end_date`>=? LIMIT 1");
        $ps->execute(array($date, $date));
        $pStatus = $ps->fetchColumn();
        if ($pStatus !== false && $pStatus !== 'open') {
            return false;
        }
        return true;
    }
}

if (!function_exists('epc_fy_set_period_status')) {
    function epc_fy_set_period_status(PDO $db, int $yearId, int $periodNo, string $status): void
    {
        epc_fy_ensure_schema($db);
        $valid = array('open', 'closed', 'locked');
        if (!in_array($status, $valid, true)) {
            throw new Exception('Invalid period status');
        }
        $db->prepare("UPDATE `epc_fy_periods` SET `status`=? WHERE `year_id`=? AND `period_no`=?")->execute(array($status, $yearId, $periodNo));
    }
}

if (!function_exists('epc_fy_close_year')) {
    /**
     * Year-end close. Computes net P&L for the year (preferring the GL P&L
     * report when available, else the supplied $netPL), records it as the
     * year's retained P&L, marks the year closed, and returns the closing
     * entry intent (debit/credit) so the caller can post it to the GL.
     *
     * @return array<string,mixed>
     */
    function epc_fy_close_year(PDO $db, int $yearId, float $netPL = null, string $retainedAccount = '3200', string $plClearing = '3900'): array
    {
        epc_fy_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_fy_years` WHERE `id`=?");
        $st->execute(array($yearId));
        $year = $st->fetch(PDO::FETCH_ASSOC);
        if (!$year) {
            throw new Exception('Fiscal year not found');
        }
        if ($year['status'] !== 'open') {
            throw new Exception('Year is not open');
        }

        if ($netPL === null) {
            $netPL = 0.0;
            if (function_exists('epc_erp_gl_pl_report')) {
                try {
                    $pl = epc_erp_gl_pl_report($db, $year['start_date'], $year['end_date']);
                    $netPL = (float) ($pl['net_profit'] ?? (((float) ($pl['total_revenue'] ?? 0)) - ((float) ($pl['total_expenses'] ?? 0))));
                } catch (Throwable $e) {
                    $netPL = 0.0;
                }
            }
        }
        $netPL = round($netPL, 2);

        // Closing entry: move net P&L to retained earnings.
        // Profit (net>0): debit P&L clearing, credit retained earnings.
        $entry = array(
            'date' => (int) $year['end_date'],
            'memo' => 'Year-end close ' . $year['label'],
            'lines' => $netPL >= 0
                ? array(
                    array('account' => $plClearing, 'debit' => abs($netPL), 'credit' => 0.0),
                    array('account' => $retainedAccount, 'debit' => 0.0, 'credit' => abs($netPL)),
                )
                : array(
                    array('account' => $retainedAccount, 'debit' => abs($netPL), 'credit' => 0.0),
                    array('account' => $plClearing, 'debit' => 0.0, 'credit' => abs($netPL)),
                ),
        );

        $db->prepare("UPDATE `epc_fy_years` SET `status`='closed', `closed_at`=?, `retained_pl`=? WHERE `id`=?")
           ->execute(array(time(), $netPL, $yearId));
        $db->prepare("UPDATE `epc_fy_periods` SET `status`='closed' WHERE `year_id`=? AND `status`='open'")->execute(array($yearId));

        return array(
            'year_id' => $yearId,
            'label' => $year['label'],
            'net_pl' => $netPL,
            'result' => $netPL >= 0 ? 'profit' : 'loss',
            'closing_entry' => $entry,
        );
    }
}

if (!function_exists('epc_fy_reopen_year')) {
    function epc_fy_reopen_year(PDO $db, int $yearId): void
    {
        epc_fy_ensure_schema($db);
        $db->prepare("UPDATE `epc_fy_years` SET `status`='open', `closed_at`=0 WHERE `id`=? AND `status`='closed'")->execute(array($yearId));
    }
}

if (!function_exists('epc_fy_carry_forward')) {
    /**
     * Compute next-year opening balances from a set of closing balances.
     * Balance-sheet accounts (assets/liabilities/equity) carry forward; P&L
     * accounts (income/expense) reset to zero (their net is already in retained
     * earnings via the closing entry).
     *
     * @param array<int,array{account:string,type:string,balance:float}> $closing
     * @return array<int,array{account:string,opening:float}>
     */
    function epc_fy_carry_forward(array $closing): array
    {
        $bsTypes = array('asset', 'liability', 'equity');
        $out = array();
        foreach ($closing as $row) {
            $type = strtolower((string) ($row['type'] ?? ''));
            if (in_array($type, $bsTypes, true)) {
                $out[] = array('account' => (string) $row['account'], 'opening' => round((float) $row['balance'], 2));
            }
        }
        return $out;
    }
}

/* --------------------------- Consolidation --------------------------- */

if (!function_exists('epc_consol_rollup')) {
    /**
     * Consolidate balances from multiple entities (branches/BUs/companies).
     *
     * Each entity provides: key, currency, rate (entity-currency -> group base),
     * and accounts[] of {account, type, balance} in the entity's own currency.
     * Balances are translated to group base, summed per account, and totalled
     * into P&L (income - expense) and balance-sheet groups.
     *
     * @param array<int,array<string,mixed>> $entities
     * @param string $groupCurrency
     * @return array<string,mixed>
     */
    function epc_consol_rollup(array $entities, string $groupCurrency = 'AED'): array
    {
        $accounts = array(); // account => ['type'=>, 'balance'=>]
        $byEntity = array();
        foreach ($entities as $ent) {
            $rate = (float) ($ent['rate'] ?? 1.0);
            $key = (string) ($ent['key'] ?? '');
            $entTotal = 0.0;
            foreach ((array) ($ent['accounts'] ?? array()) as $a) {
                $acc = (string) $a['account'];
                $type = strtolower((string) ($a['type'] ?? ''));
                $bal = round((float) $a['balance'] * $rate, 2);
                if (!isset($accounts[$acc])) {
                    $accounts[$acc] = array('type' => $type, 'balance' => 0.0);
                }
                $accounts[$acc]['balance'] = round($accounts[$acc]['balance'] + $bal, 2);
                $entTotal = round($entTotal + $bal, 2);
            }
            $byEntity[$key] = $entTotal;
        }

        $income = 0.0;
        $expense = 0.0;
        $assets = 0.0;
        $liabilities = 0.0;
        $equity = 0.0;
        foreach ($accounts as $acc => $info) {
            $bal = (float) $info['balance'];
            switch ($info['type']) {
                case 'income':
                case 'revenue':
                    $income = round($income + $bal, 2);
                    break;
                case 'expense':
                    $expense = round($expense + $bal, 2);
                    break;
                case 'asset':
                    $assets = round($assets + $bal, 2);
                    break;
                case 'liability':
                    $liabilities = round($liabilities + $bal, 2);
                    break;
                case 'equity':
                    $equity = round($equity + $bal, 2);
                    break;
            }
        }

        return array(
            'group_currency' => $groupCurrency,
            'accounts' => $accounts,
            'by_entity' => $byEntity,
            'net_profit' => round($income - $expense, 2),
            'total_income' => $income,
            'total_expense' => $expense,
            'total_assets' => $assets,
            'total_liabilities' => $liabilities,
            'total_equity' => $equity,
        );
    }
}

if (!function_exists('epc_consol_eliminate')) {
    /**
     * Apply inter-company/inter-branch eliminations to a consolidated result.
     * Each elimination reduces an account's consolidated balance (e.g. remove
     * inter-branch sales/receivables that net to zero at group level).
     *
     * @param array<string,mixed> $consol output of epc_consol_rollup()
     * @param array<int,array{account:string,amount:float}> $eliminations
     * @return array<string,mixed>
     */
    function epc_consol_eliminate(array $consol, array $eliminations): array
    {
        foreach ($eliminations as $el) {
            $acc = (string) $el['account'];
            $amt = round((float) $el['amount'], 2);
            if (isset($consol['accounts'][$acc])) {
                $consol['accounts'][$acc]['balance'] = round($consol['accounts'][$acc]['balance'] - $amt, 2);
                $type = $consol['accounts'][$acc]['type'];
                switch ($type) {
                    case 'income':
                    case 'revenue':
                        $consol['total_income'] = round($consol['total_income'] - $amt, 2);
                        break;
                    case 'expense':
                        $consol['total_expense'] = round($consol['total_expense'] - $amt, 2);
                        break;
                    case 'asset':
                        $consol['total_assets'] = round($consol['total_assets'] - $amt, 2);
                        break;
                    case 'liability':
                        $consol['total_liabilities'] = round($consol['total_liabilities'] - $amt, 2);
                        break;
                    case 'equity':
                        $consol['total_equity'] = round($consol['total_equity'] - $amt, 2);
                        break;
                }
            }
        }
        $consol['net_profit'] = round($consol['total_income'] - $consol['total_expense'], 2);
        $consol['eliminations_applied'] = count($eliminations);
        return $consol;
    }
}
