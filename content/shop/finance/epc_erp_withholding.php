<?php
/**
 * Tax depth — withholding tax.
 *
 * Withholding tax codes (rate + GL account) -> apply on vendor payments
 * (base -> computed withheld amount) -> issue withholding certificates ->
 * settle to the authority. Multi-company, additive epc_wht_* tables.
 *
 * Rates are configured per tenant (not hard-coded to any country) in line with
 * the country-driven compliance rule.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_wht_ensure_schema')) {
    function epc_wht_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_wht_code` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `rate` decimal(8,4) NOT NULL DEFAULT 0.0000,
            `account` varchar(60) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Withholding tax codes'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_wht_txn` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code_id` int(11) NOT NULL DEFAULT 0,
            `vendor` varchar(180) NOT NULL DEFAULT '',
            `doc_ref` varchar(80) NOT NULL DEFAULT '',
            `txn_date` varchar(16) NOT NULL DEFAULT '',
            `base_amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `wht_amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `rate` decimal(8,4) NOT NULL DEFAULT 0.0000,
            `certificate_no` varchar(60) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'accrued',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_code` (`code_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Withholding tax transactions'");
    }
}

if (!function_exists('epc_wht_code_save')) {
    /** @param array<string,mixed> $data company_id, code, name, rate, account, active */
    function epc_wht_code_save(PDO $db, array $data, int $id = 0): int
    {
        epc_wht_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        $name = trim((string) ($data['name'] ?? ''));
        if ($code === '' || $name === '') {
            throw new Exception('Code and name are required');
        }
        $rate = (float) ($data['rate'] ?? 0);
        if ($rate < 0 || $rate > 100) {
            throw new Exception('Rate must be a percentage between 0 and 100');
        }
        $active = isset($data['active']) ? (int) ((int) $data['active'] === 1) : 1;
        if ($id > 0) {
            $db->prepare("UPDATE `epc_wht_code` SET `code`=?, `name`=?, `rate`=?, `account`=?, `active`=? WHERE `id`=?")
               ->execute(array($code, $name, $rate, (string) ($data['account'] ?? ''), $active, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_wht_code` (`company_id`,`code`,`name`,`rate`,`account`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $code, $name, $rate, (string) ($data['account'] ?? ''), $active, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_wht_code_get')) {
    /** @return array<string,mixed>|null */
    function epc_wht_code_get(PDO $db, int $id): ?array
    {
        epc_wht_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_wht_code` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_wht_codes')) {
    /** @return array<int,array<string,mixed>> */
    function epc_wht_codes(PDO $db, int $companyId, bool $activeOnly = false): array
    {
        epc_wht_ensure_schema($db);
        $sql = "SELECT * FROM `epc_wht_code` WHERE `company_id`=?";
        if ($activeOnly) {
            $sql .= " AND `active`=1";
        }
        $sql .= " ORDER BY `code` ASC";
        $st = $db->prepare($sql);
        $st->execute(array($companyId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_wht_calc')) {
    /** Compute withheld amount for a base using a code's rate. */
    function epc_wht_calc(PDO $db, int $codeId, float $base): float
    {
        $code = epc_wht_code_get($db, $codeId);
        if (!$code) {
            throw new Exception('Withholding code not found');
        }
        return round($base * ((float) $code['rate'] / 100.0), 2);
    }
}

if (!function_exists('epc_wht_record')) {
    /** @param array<string,mixed> $data company_id, code_id, vendor, doc_ref, txn_date, base_amount */
    function epc_wht_record(PDO $db, array $data): int
    {
        epc_wht_ensure_schema($db);
        $codeId = (int) ($data['code_id'] ?? 0);
        $code = epc_wht_code_get($db, $codeId);
        if (!$code) {
            throw new Exception('Withholding code not found');
        }
        $base = (float) ($data['base_amount'] ?? 0);
        if ($base <= 0) {
            throw new Exception('Base amount must be positive');
        }
        $wht = epc_wht_calc($db, $codeId, $base);
        $db->prepare("INSERT INTO `epc_wht_txn` (`company_id`,`code_id`,`vendor`,`doc_ref`,`txn_date`,`base_amount`,`wht_amount`,`rate`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,'accrued',?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $codeId, (string) ($data['vendor'] ?? ''), (string) ($data['doc_ref'] ?? ''), (string) ($data['txn_date'] ?? ''), $base, $wht, (float) $code['rate'], time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_wht_txn_get')) {
    /** @return array<string,mixed>|null */
    function epc_wht_txn_get(PDO $db, int $id): ?array
    {
        epc_wht_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_wht_txn` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_wht_txns')) {
    /** @return array<int,array<string,mixed>> */
    function epc_wht_txns(PDO $db, int $companyId, string $status = ''): array
    {
        epc_wht_ensure_schema($db);
        $sql = "SELECT * FROM `epc_wht_txn` WHERE `company_id`=?";
        $args = array($companyId);
        if ($status !== '') {
            $sql .= " AND `status`=?";
            $args[] = $status;
        }
        $sql .= " ORDER BY `id` DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_wht_certificate_issue')) {
    /** Issue a withholding certificate number for an accrued transaction. */
    function epc_wht_certificate_issue(PDO $db, int $txnId, string $certNo = ''): string
    {
        epc_wht_ensure_schema($db);
        $txn = epc_wht_txn_get($db, $txnId);
        if (!$txn) {
            throw new Exception('Transaction not found');
        }
        if ($txn['certificate_no'] !== '') {
            throw new Exception('Certificate already issued');
        }
        if ($certNo === '') {
            $certNo = 'WHT-' . date('Y') . '-' . str_pad((string) $txnId, 5, '0', STR_PAD_LEFT);
        }
        $db->prepare("UPDATE `epc_wht_txn` SET `certificate_no`=? WHERE `id`=?")->execute(array($certNo, $txnId));
        return $certNo;
    }
}

if (!function_exists('epc_wht_settle')) {
    /** Mark an accrued transaction as settled to the authority. */
    function epc_wht_settle(PDO $db, int $txnId): void
    {
        epc_wht_ensure_schema($db);
        $txn = epc_wht_txn_get($db, $txnId);
        if (!$txn) {
            throw new Exception('Transaction not found');
        }
        if ($txn['status'] === 'settled') {
            throw new Exception('Already settled');
        }
        $db->prepare("UPDATE `epc_wht_txn` SET `status`='settled' WHERE `id`=?")->execute(array($txnId));
    }
}

if (!function_exists('epc_wht_summary')) {
    /** @return array<string,mixed> */
    function epc_wht_summary(PDO $db, int $companyId): array
    {
        epc_wht_ensure_schema($db);
        $out = array('codes' => 0, 'txns' => 0, 'accrued' => 0.0, 'settled' => 0.0, 'total_withheld' => 0.0);
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_wht_code` WHERE `company_id`=?");
        $st->execute(array($companyId));
        $out['codes'] = (int) $st->fetchColumn();
        $st = $db->prepare("SELECT `status`, COUNT(*) c, COALESCE(SUM(`wht_amount`),0) v FROM `epc_wht_txn` WHERE `company_id`=? GROUP BY `status`");
        $st->execute(array($companyId));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $out['txns'] += (int) $r['c'];
            $out['total_withheld'] += (float) $r['v'];
            if ($r['status'] === 'accrued') {
                $out['accrued'] = (float) $r['v'];
            } elseif ($r['status'] === 'settled') {
                $out['settled'] = (float) $r['v'];
            }
        }
        return $out;
    }
}
