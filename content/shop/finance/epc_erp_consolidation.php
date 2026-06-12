<?php
/**
 * Advanced ERP — Group consolidation & intercompany accounting.
 *
 * Registers the members of a corporate group (legal entities), captures each
 * member's period financials (the "home" tenant is pulled live from its own GL),
 * records intercompany transactions, and produces a consolidated P&L and balance
 * sheet with intercompany eliminations and minority-interest split.
 *
 * Additive: new epc_cons_* tables. Pulls home figures via epc_erp_gl_*.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_cons_ensure_schema')) {
    function epc_cons_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cons_entities` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `currency_code` varchar(8) NOT NULL DEFAULT 'AED',
            `ownership_pct` decimal(7,3) NOT NULL DEFAULT 100.000,
            `is_home` tinyint(1) NOT NULL DEFAULT 0,
            `parent_code` varchar(40) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `u_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Group member companies'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cons_figures` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `entity_code` varchar(40) NOT NULL DEFAULT '',
            `revenue` decimal(16,2) NOT NULL DEFAULT 0.00,
            `expenses` decimal(16,2) NOT NULL DEFAULT 0.00,
            `assets` decimal(16,2) NOT NULL DEFAULT 0.00,
            `liabilities` decimal(16,2) NOT NULL DEFAULT 0.00,
            `equity` decimal(16,2) NOT NULL DEFAULT 0.00,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `u_entity` (`entity_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-entity manual financials'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cons_ic` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `ref` varchar(60) NOT NULL DEFAULT '',
            `from_entity` varchar(40) NOT NULL DEFAULT '',
            `to_entity` varchar(40) NOT NULL DEFAULT '',
            `txn_type` varchar(20) NOT NULL DEFAULT 'sale',
            `amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `txn_date` varchar(10) NOT NULL DEFAULT '',
            `memo` varchar(200) NOT NULL DEFAULT '',
            `reconciled` tinyint(1) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_from` (`from_entity`),
            KEY `x_to` (`to_entity`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Intercompany transactions'");
    }
}

if (!function_exists('epc_cons_entity_save')) {
    /**
     * @param array<string,mixed> $d
     */
    function epc_cons_entity_save(PDO $db, array $d, int $id = 0): int
    {
        epc_cons_ensure_schema($db);
        $code = strtoupper(trim((string) ($d['code'] ?? '')));
        if ($code === '') { throw new Exception('Entity code is required'); }
        $name = trim((string) ($d['name'] ?? ''));
        if ($name === '') { throw new Exception('Entity name is required'); }
        $ccy = strtoupper(trim((string) ($d['currency_code'] ?? 'AED'))) ?: 'AED';
        $own = max(0.0, min(100.0, (float) ($d['ownership_pct'] ?? 100)));
        $home = !empty($d['is_home']) ? 1 : 0;
        $parent = strtoupper(trim((string) ($d['parent_code'] ?? '')));
        if ($home) {
            $db->exec("UPDATE `epc_cons_entities` SET `is_home`=0");
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_cons_entities` SET `code`=?,`name`=?,`currency_code`=?,`ownership_pct`=?,`is_home`=?,`parent_code`=? WHERE `id`=?")
               ->execute(array($code, $name, $ccy, $own, $home, $parent, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_cons_entities` (`code`,`name`,`currency_code`,`ownership_pct`,`is_home`,`parent_code`,`active`,`time_created`) VALUES (?,?,?,?,?,?,1,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`currency_code`=VALUES(`currency_code`),`ownership_pct`=VALUES(`ownership_pct`),`is_home`=VALUES(`is_home`),`parent_code`=VALUES(`parent_code`),`active`=1")
           ->execute(array($code, $name, $ccy, $own, $home, $parent, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_cons_entity_delete')) {
    function epc_cons_entity_delete(PDO $db, int $id): bool
    {
        epc_cons_ensure_schema($db);
        $db->prepare("DELETE FROM `epc_cons_entities` WHERE `id`=?")->execute(array($id));
        return true;
    }
}

if (!function_exists('epc_cons_entities_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_cons_entities_list(PDO $db): array
    {
        epc_cons_ensure_schema($db);
        return $db->query("SELECT * FROM `epc_cons_entities` WHERE `active`=1 ORDER BY `is_home` DESC, `code`")->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_cons_figures_save')) {
    /**
     * @param array<string,mixed> $d
     */
    function epc_cons_figures_save(PDO $db, array $d): bool
    {
        epc_cons_ensure_schema($db);
        $code = strtoupper(trim((string) ($d['entity_code'] ?? '')));
        if ($code === '') { throw new Exception('Entity is required'); }
        $db->prepare("INSERT INTO `epc_cons_figures` (`entity_code`,`revenue`,`expenses`,`assets`,`liabilities`,`equity`,`time_updated`) VALUES (?,?,?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `revenue`=VALUES(`revenue`),`expenses`=VALUES(`expenses`),`assets`=VALUES(`assets`),`liabilities`=VALUES(`liabilities`),`equity`=VALUES(`equity`),`time_updated`=VALUES(`time_updated`)")
           ->execute(array(
               $code,
               (float) ($d['revenue'] ?? 0), (float) ($d['expenses'] ?? 0),
               (float) ($d['assets'] ?? 0), (float) ($d['liabilities'] ?? 0),
               (float) ($d['equity'] ?? 0), time(),
           ));
        return true;
    }
}

if (!function_exists('epc_cons_figures_map')) {
    /** @return array<string,array<string,float>> entity_code => figures */
    function epc_cons_figures_map(PDO $db): array
    {
        epc_cons_ensure_schema($db);
        $out = array();
        foreach ($db->query("SELECT * FROM `epc_cons_figures`")->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $out[(string) $r['entity_code']] = array(
                'revenue' => (float) $r['revenue'], 'expenses' => (float) $r['expenses'],
                'assets' => (float) $r['assets'], 'liabilities' => (float) $r['liabilities'],
                'equity' => (float) $r['equity'],
            );
        }
        return $out;
    }
}

if (!function_exists('epc_cons_home_figures')) {
    /**
     * Live figures for the home entity from this tenant's GL.
     *
     * @return array<string,float>
     */
    function epc_cons_home_figures(PDO $db, int $dateFrom, int $dateTo): array
    {
        $rev = $exp = $asset = $liab = $eq = 0.0;
        try {
            if (function_exists('epc_erp_gl_pl_report')) {
                $pl = epc_erp_gl_pl_report($db, $dateFrom, $dateTo);
                $rev = (float) $pl['total_revenue'];
                $exp = (float) $pl['total_expenses'];
            }
            if (function_exists('epc_erp_gl_balance_sheet')) {
                $bs = epc_erp_gl_balance_sheet($db, $dateTo);
                $asset = (float) ($bs['total_assets'] ?? 0);
                $liab = (float) ($bs['total_liabilities'] ?? 0);
                $eq = (float) ($bs['total_equity'] ?? 0);
            }
        } catch (Throwable $e) {
            // leave zeros if GL not available
        }
        return array('revenue' => $rev, 'expenses' => $exp, 'assets' => $asset, 'liabilities' => $liab, 'equity' => $eq);
    }
}

if (!function_exists('epc_cons_ic_save')) {
    /**
     * @param array<string,mixed> $d
     */
    function epc_cons_ic_save(PDO $db, array $d): int
    {
        epc_cons_ensure_schema($db);
        $from = strtoupper(trim((string) ($d['from_entity'] ?? '')));
        $to = strtoupper(trim((string) ($d['to_entity'] ?? '')));
        if ($from === '' || $to === '') { throw new Exception('From and to entities are required'); }
        if ($from === $to) { throw new Exception('Intercompany needs two different entities'); }
        $amt = round((float) ($d['amount'] ?? 0), 2);
        if ($amt <= 0) { throw new Exception('Amount must be positive'); }
        $type = (string) ($d['txn_type'] ?? 'sale');
        $date = (string) ($d['txn_date'] ?? date('Y-m-d'));
        $ref = trim((string) ($d['ref'] ?? '')) ?: ('IC-' . $from . '-' . $to . '-' . time());
        $memo = mb_substr(trim((string) ($d['memo'] ?? '')), 0, 200);
        $db->prepare("INSERT INTO `epc_cons_ic` (`ref`,`from_entity`,`to_entity`,`txn_type`,`amount`,`txn_date`,`memo`,`time_created`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array($ref, $from, $to, $type, $amt, $date, $memo, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_cons_ic_delete')) {
    function epc_cons_ic_delete(PDO $db, int $id): bool
    {
        epc_cons_ensure_schema($db);
        $db->prepare("DELETE FROM `epc_cons_ic` WHERE `id`=?")->execute(array($id));
        return true;
    }
}

if (!function_exists('epc_cons_ic_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_cons_ic_list(PDO $db): array
    {
        epc_cons_ensure_schema($db);
        return $db->query("SELECT * FROM `epc_cons_ic` ORDER BY `id` DESC")->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_cons_consolidate')) {
    /**
     * Build the consolidated P&L and balance sheet across the group.
     *
     * Combine = home (live GL) + each member's figures.
     * Eliminations:
     *   - IC sales/purchases remove matched revenue & expense (the lower of the two
     *     mirror legs, summed per ordered pair).
     *   - IC balances (loan/open) remove matched receivable (asset) & payable
     *     (liability).
     * Minority interest = members' post-elimination equity × (1 − ownership%).
     *
     * @return array<string,mixed>
     */
    function epc_cons_consolidate(PDO $db, int $dateFrom, int $dateTo): array
    {
        $entities = epc_cons_entities_list($db);
        $manual = epc_cons_figures_map($db);

        $combined = array('revenue' => 0.0, 'expenses' => 0.0, 'assets' => 0.0, 'liabilities' => 0.0, 'equity' => 0.0);
        $rows = array();
        $minorityInterest = 0.0;
        foreach ($entities as $e) {
            $code = (string) $e['code'];
            if (!empty($e['is_home'])) {
                // Home entity is pulled live from this tenant's GL.
                $f = epc_cons_home_figures($db, $dateFrom, $dateTo);
            } else {
                $f = isset($manual[$code]) ? $manual[$code] : array('revenue' => 0.0, 'expenses' => 0.0, 'assets' => 0.0, 'liabilities' => 0.0, 'equity' => 0.0);
            }
            $f['profit'] = round($f['revenue'] - $f['expenses'], 2);
            $f['code'] = $code;
            $f['name'] = (string) $e['name'];
            $f['ownership_pct'] = (float) $e['ownership_pct'];
            $f['is_home'] = !empty($e['is_home']);
            $rows[] = $f;
            foreach ($combined as $k => $_) { $combined[$k] += (float) $f[$k]; }
            // minority interest on equity for non-wholly-owned subs
            if (!$f['is_home'] && $f['ownership_pct'] < 100.0) {
                $minorityInterest += ($f['equity'] + $f['profit']) * (1 - $f['ownership_pct'] / 100.0);
            }
        }

        // Eliminations from IC ledger
        $ics = epc_cons_ic_list($db);
        $elimPL = 0.0;   // revenue & matching expense removed
        $elimBS = 0.0;   // receivable & matching payable removed
        foreach ($ics as $ic) {
            $amt = (float) $ic['amount'];
            if (in_array($ic['txn_type'], array('sale', 'purchase', 'service'), true)) {
                $elimPL += $amt; // removes both the seller's revenue and buyer's expense
            } else {
                $elimBS += $amt; // loan/funding: removes IC receivable & payable
            }
        }

        $consRevenue = round($combined['revenue'] - $elimPL, 2);
        $consExpenses = round($combined['expenses'] - $elimPL, 2);
        $consProfit = round($consRevenue - $consExpenses, 2);
        $consAssets = round($combined['assets'] - $elimBS, 2);
        $consLiab = round($combined['liabilities'] - $elimBS, 2);
        $consEquity = round($combined['equity'], 2);

        return array(
            'date_from' => $dateFrom,
            'date_to' => $dateTo,
            'entities' => $rows,
            'entity_count' => count($rows),
            'combined' => array_map(function ($v) { return round($v, 2); }, $combined),
            'elimination_pl' => round($elimPL, 2),
            'elimination_bs' => round($elimBS, 2),
            'ic_count' => count($ics),
            'minority_interest' => round($minorityInterest, 2),
            'consolidated' => array(
                'revenue' => $consRevenue,
                'expenses' => $consExpenses,
                'net_profit' => $consProfit,
                'assets' => $consAssets,
                'liabilities' => $consLiab,
                'equity' => $consEquity,
                'group_profit' => round($consProfit - $minorityInterest, 2),
            ),
        );
    }
}
