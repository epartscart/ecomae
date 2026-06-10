<?php
/**
 * Advanced ERP — Organization structure & voucher numbering.
 *
 * Org hierarchy: company (legal entity) -> business unit -> branch -> warehouse.
 * Transactions can be tagged to a branch/BU so reports and consolidation can
 * filter and roll up by them. Single-branch tenants simply never populate it.
 *
 * Voucher sequences: per document type, optionally per branch, with prefix +
 * year token + zero-padded running number, and a gapless guarantee (numbers
 * are allocated under a row lock so concurrent calls never collide).
 *
 * Additive: new epc_org_* tables only.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_org_ensure_schema')) {
    function epc_org_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_org_companies` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(20) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `base_currency` varchar(3) NOT NULL DEFAULT 'AED',
            `tax_id` varchar(60) DEFAULT NULL,
            `country` varchar(2) DEFAULT NULL,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Legal entities / companies'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_org_units` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `parent_id` int(11) NOT NULL DEFAULT 0,
            `type` varchar(12) NOT NULL DEFAULT 'branch',
            `code` varchar(30) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `base_currency` varchar(3) DEFAULT NULL,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`),
            KEY `x_company` (`company_id`),
            KEY `x_parent` (`parent_id`),
            KEY `x_type` (`type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Business units / branches / warehouses'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_org_sequences` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `doc_type` varchar(40) NOT NULL,
            `branch_id` int(11) NOT NULL DEFAULT 0,
            `prefix` varchar(24) NOT NULL DEFAULT '',
            `year_token` varchar(8) NOT NULL DEFAULT 'Y',
            `pad` tinyint(4) NOT NULL DEFAULT 5,
            `next_no` int(11) NOT NULL DEFAULT 1,
            `period_year` int(11) NOT NULL DEFAULT 0,
            `reset_yearly` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_seq` (`doc_type`,`branch_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Voucher/document numbering sequences'");
    }
}

/* --------------------------- Org structure --------------------------- */

if (!function_exists('epc_org_company_save')) {
    /**
     * @param array<string,mixed> $data code, name, base_currency, tax_id, country
     */
    function epc_org_company_save(PDO $db, array $data, int $id = 0): int
    {
        epc_org_ensure_schema($db);
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_org_companies` SET `name`=?, `base_currency`=?, `tax_id`=?, `country`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (string) ($data['base_currency'] ?? 'AED'), (string) ($data['tax_id'] ?? ''), (string) ($data['country'] ?? ''), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_org_companies` (`code`,`name`,`base_currency`,`tax_id`,`country`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (string) ($data['base_currency'] ?? 'AED'), (string) ($data['tax_id'] ?? ''), (string) ($data['country'] ?? ''), $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_org_unit_save')) {
    /**
     * @param array<string,mixed> $data company_id, parent_id, type(bu|branch|warehouse), code, name, base_currency
     */
    function epc_org_unit_save(PDO $db, array $data, int $id = 0): int
    {
        epc_org_ensure_schema($db);
        $type = (string) ($data['type'] ?? 'branch');
        $valid = array('bu', 'branch', 'warehouse');
        if (!in_array($type, $valid, true)) {
            throw new Exception('Invalid org unit type');
        }
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_org_units` SET `company_id`=?, `parent_id`=?, `type`=?, `name`=?, `base_currency`=? WHERE `id`=?")
               ->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['parent_id'] ?? 0), $type, (string) ($data['name'] ?? ''), (string) ($data['base_currency'] ?? ''), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_org_units` (`company_id`,`parent_id`,`type`,`code`,`name`,`base_currency`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['parent_id'] ?? 0), $type, (string) $data['code'], (string) ($data['name'] ?? ''), (string) ($data['base_currency'] ?? ''), $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_org_units')) {
    /**
     * @return array<int,array<string,mixed>>
     */
    function epc_org_units(PDO $db, string $type = '', int $companyId = 0): array
    {
        epc_org_ensure_schema($db);
        $sql = "SELECT * FROM `epc_org_units` WHERE `active`=1";
        $args = array();
        if ($type !== '') {
            $sql .= " AND `type`=?";
            $args[] = $type;
        }
        if ($companyId > 0) {
            $sql .= " AND `company_id`=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY `type`,`code`";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_org_branch_tree')) {
    /**
     * Branches (and their warehouses) grouped under each company.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_org_branch_tree(PDO $db): array
    {
        epc_org_ensure_schema($db);
        $companies = $db->query("SELECT * FROM `epc_org_companies` WHERE `active`=1 ORDER BY `code`")->fetchAll(PDO::FETCH_ASSOC);
        $units = epc_org_units($db);
        $byCompany = array();
        foreach ($units as $u) {
            $byCompany[(int) $u['company_id']][] = $u;
        }
        $out = array();
        foreach ($companies as $c) {
            $out[] = array('company' => $c, 'units' => $byCompany[(int) $c['id']] ?? array());
        }
        return $out;
    }
}

/* ------------------------- Voucher numbering ------------------------- */

if (!function_exists('epc_org_sequence_config')) {
    /**
     * Define/update a numbering sequence for a doc type (+ optional branch).
     *
     * @param array<string,mixed> $cfg prefix, year_token, pad, next_no, reset_yearly
     */
    function epc_org_sequence_config(PDO $db, string $docType, int $branchId, array $cfg): void
    {
        epc_org_ensure_schema($db);
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_org_sequences` (`doc_type`,`branch_id`,`prefix`,`year_token`,`pad`,`next_no`,`reset_yearly`,`time_updated`)
             VALUES (?,?,?,?,?,?,?,?)
             ON DUPLICATE KEY UPDATE `prefix`=VALUES(`prefix`), `year_token`=VALUES(`year_token`), `pad`=VALUES(`pad`), `next_no`=VALUES(`next_no`), `reset_yearly`=VALUES(`reset_yearly`), `time_updated`=VALUES(`time_updated`)"
        )->execute(array(
            $docType,
            $branchId,
            (string) ($cfg['prefix'] ?? ''),
            (string) ($cfg['year_token'] ?? 'Y'),
            (int) ($cfg['pad'] ?? 5),
            (int) ($cfg['next_no'] ?? 1),
            isset($cfg['reset_yearly']) ? (int) (bool) $cfg['reset_yearly'] : 1,
            $now,
        ));
    }
}

if (!function_exists('epc_org_next_voucher')) {
    /**
     * Allocate the next gapless voucher number for a doc type (+ branch). The
     * row is locked + updated inside a transaction so concurrent allocations
     * cannot return the same number. Year reset is applied when the period year
     * rolls over (if reset_yearly).
     *
     * Format: PREFIX + [year] + zero-padded number, e.g. INV-2026-00042.
     *
     * @return array{number:string,sequence_no:int,year:int}
     */
    function epc_org_next_voucher(PDO $db, string $docType, int $branchId = 0, int $atTime = 0): array
    {
        epc_org_ensure_schema($db);
        $atTime = $atTime > 0 ? $atTime : time();
        $year = (int) date('Y', $atTime);

        $ownTxn = !$db->inTransaction();
        if ($ownTxn) {
            $db->beginTransaction();
        }
        try {
            $sel = $db->prepare("SELECT * FROM `epc_org_sequences` WHERE `doc_type`=? AND `branch_id`=? FOR UPDATE");
            $sel->execute(array($docType, $branchId));
            $row = $sel->fetch(PDO::FETCH_ASSOC);
            if (!$row) {
                // Auto-create a default sequence on first use.
                $db->prepare("INSERT INTO `epc_org_sequences` (`doc_type`,`branch_id`,`prefix`,`year_token`,`pad`,`next_no`,`period_year`,`reset_yearly`,`time_updated`) VALUES (?,?,?, 'Y', 5, 1, ?, 1, ?)")
                   ->execute(array($docType, $branchId, strtoupper(substr($docType, 0, 3)) . '-', $year, $atTime));
                $sel->execute(array($docType, $branchId));
                $row = $sel->fetch(PDO::FETCH_ASSOC);
            }

            $resetYearly = (int) $row['reset_yearly'] === 1;
            $periodYear = (int) $row['period_year'];
            $nextNo = (int) $row['next_no'];
            if ($resetYearly && $periodYear !== 0 && $periodYear !== $year) {
                $nextNo = 1; // new year -> restart
            }

            $seqNo = $nextNo;
            $pad = (int) $row['pad'];
            $prefix = (string) $row['prefix'];
            $yearToken = (string) $row['year_token'];
            $yearStr = '';
            if ($yearToken === 'Y') {
                $yearStr = (string) $year;
            } elseif ($yearToken === 'y') {
                $yearStr = date('y', $atTime);
            }
            $numStr = str_pad((string) $seqNo, max(1, $pad), '0', STR_PAD_LEFT);
            $voucher = $prefix . ($yearStr !== '' ? $yearStr . '-' : '') . $numStr;

            $db->prepare("UPDATE `epc_org_sequences` SET `next_no`=?, `period_year`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array($seqNo + 1, $year, $atTime, (int) $row['id']));

            if ($ownTxn) {
                $db->commit();
            }
            return array('number' => $voucher, 'sequence_no' => $seqNo, 'year' => $year);
        } catch (Throwable $e) {
            if ($ownTxn && $db->inTransaction()) {
                $db->rollBack();
            }
            throw $e;
        }
    }
}
