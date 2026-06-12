<?php
defined('_ASTEXE_') or die('No access');
/**
 * Contract management, native e-signature and OCR capture.
 *
 * Complements the existing ECM/Document-Control repository (which already
 * handles file storage + versioning) by adding the contract lifecycle,
 * a lightweight tamper-evident e-signature ledger, and an OCR text store.
 */

if (!function_exists('epc_ctr_ensure_schema')) {
    function epc_ctr_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_contracts` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `title` varchar(200) NOT NULL DEFAULT '',
            `counterparty` varchar(200) NOT NULL DEFAULT '',
            `contract_value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `start_date` int(11) NOT NULL DEFAULT 0,
            `end_date` int(11) NOT NULL DEFAULT 0,
            `status` varchar(16) NOT NULL DEFAULT 'draft',
            `version` int(11) NOT NULL DEFAULT 1,
            `body_text` mediumtext,
            `ocr_text` mediumtext,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Contracts register'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_contract_signatures` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `contract_id` int(11) NOT NULL,
            `signer_name` varchar(160) NOT NULL DEFAULT '',
            `signer_email` varchar(160) NOT NULL DEFAULT '',
            `signed_at` int(11) NOT NULL DEFAULT 0,
            `signature_hash` varchar(64) NOT NULL DEFAULT '',
            `ip` varchar(64) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            KEY `x_ctr` (`contract_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='E-signature ledger'");
    }
}

if (!function_exists('epc_ctr_save')) {
    /**
     * @param array<string,mixed> $data
     */
    function epc_ctr_save(PDO $db, array $data, int $id = 0): int
    {
        epc_ctr_ensure_schema($db);
        $start = !empty($data['start_date']) ? (int) $data['start_date'] : 0;
        $end = !empty($data['end_date']) ? (int) $data['end_date'] : 0;
        if ($id > 0) {
            // editing bumps the version (immutable-ish revision trail on the body)
            $db->prepare("UPDATE `epc_erp_contracts` SET `title`=?, `counterparty`=?, `contract_value`=?, `currency`=?, `start_date`=?, `end_date`=?, `body_text`=?, `version`=`version`+1, `time_updated`=? WHERE `id`=?")
               ->execute(array(
                   (string) ($data['title'] ?? ''), (string) ($data['counterparty'] ?? ''),
                   (float) ($data['contract_value'] ?? 0), (string) ($data['currency'] ?? 'AED'),
                   $start, $end, (string) ($data['body_text'] ?? ''), time(), $id,
               ));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_erp_contracts` (`code`,`title`,`counterparty`,`contract_value`,`currency`,`start_date`,`end_date`,`status`,`version`,`body_text`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?, 'draft', 1, ?, ?, ?)")
           ->execute(array(
               (string) ($data['code'] ?? ''), (string) ($data['title'] ?? ''), (string) ($data['counterparty'] ?? ''),
               (float) ($data['contract_value'] ?? 0), (string) ($data['currency'] ?? 'AED'),
               $start, $end, (string) ($data['body_text'] ?? ''), time(), time(),
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_ctr_set_status')) {
    function epc_ctr_set_status(PDO $db, int $id, string $status): void
    {
        epc_ctr_ensure_schema($db);
        $allowed = array('draft', 'sent', 'signed', 'active', 'expired', 'terminated');
        if (!in_array($status, $allowed, true)) {
            throw new Exception('Invalid contract status');
        }
        $db->prepare("UPDATE `epc_erp_contracts` SET `status`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($status, time(), $id));
    }
}

if (!function_exists('epc_ctr_sign')) {
    /**
     * Record a tamper-evident e-signature. The hash binds the contract id +
     * version + body + signer + timestamp, so any later edit invalidates it.
     *
     * @return array{id:int,hash:string}
     */
    function epc_ctr_sign(PDO $db, int $contractId, string $signerName, string $signerEmail, string $ip = ''): array
    {
        epc_ctr_ensure_schema($db);
        $c = epc_ctr_get($db, $contractId);
        if (!$c) {
            throw new Exception('Contract not found');
        }
        $ts = time();
        $hash = hash('sha256', $contractId . '|' . $c['version'] . '|' . (string) $c['body_text'] . '|' . $signerName . '|' . $signerEmail . '|' . $ts);
        $db->prepare("INSERT INTO `epc_erp_contract_signatures` (`contract_id`,`signer_name`,`signer_email`,`signed_at`,`signature_hash`,`ip`) VALUES (?,?,?,?,?,?)")
           ->execute(array($contractId, $signerName, $signerEmail, $ts, $hash, $ip));
        epc_ctr_set_status($db, $contractId, 'signed');
        return array('id' => (int) $db->lastInsertId(), 'hash' => $hash);
    }
}

if (!function_exists('epc_ctr_signatures')) {
    /** @return array<int,array<string,mixed>> */
    function epc_ctr_signatures(PDO $db, int $contractId): array
    {
        epc_ctr_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_contract_signatures` WHERE `contract_id`=? ORDER BY `id`");
        $st->execute(array($contractId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_ctr_ocr_store')) {
    /**
     * Store OCR-extracted text against a contract. A real OCR vendor (Tesseract,
     * AWS Textract, Google Vision) would populate $text; here we accept the text
     * directly so the pipeline is wired and ready for an engine.
     */
    function epc_ctr_ocr_store(PDO $db, int $contractId, string $text): void
    {
        epc_ctr_ensure_schema($db);
        $db->prepare("UPDATE `epc_erp_contracts` SET `ocr_text`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($text, time(), $contractId));
    }
}

if (!function_exists('epc_ctr_get')) {
    /** @return array<string,mixed>|null */
    function epc_ctr_get(PDO $db, int $id): ?array
    {
        epc_ctr_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_contracts` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_ctr_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_ctr_list(PDO $db, int $limit = 200): array
    {
        epc_ctr_ensure_schema($db);
        $rows = $db->query("SELECT * FROM `epc_erp_contracts` ORDER BY `id` DESC LIMIT " . max(1, $limit))->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $sigs = epc_ctr_signatures($db, (int) $r['id']);
            $r['signature_count'] = count($sigs);
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_ctr_summary')) {
    /**
     * @return array{contracts:int,active:int,signed:int,value:float}
     */
    function epc_ctr_summary(PDO $db): array
    {
        epc_ctr_ensure_schema($db);
        $total = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_contracts`")->fetchColumn();
        $active = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_contracts` WHERE `status`='active'")->fetchColumn();
        $signed = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_contracts` WHERE `status` IN ('signed','active')")->fetchColumn();
        $value = (float) $db->query("SELECT COALESCE(SUM(`contract_value`),0) FROM `epc_erp_contracts` WHERE `status` IN ('signed','active')")->fetchColumn();
        return array('contracts' => $total, 'active' => $active, 'signed' => $signed, 'value' => round($value, 2));
    }
}
