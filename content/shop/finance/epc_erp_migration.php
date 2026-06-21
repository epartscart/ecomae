<?php
/**
 * Advanced ERP — Data migration toolkit.
 *
 * Onboard a new client from their old system (Tally/QuickBooks/Zoho/SAP B1/
 * Excel) safely:
 *  - Migration batches (versioned, validate -> dry-run -> commit -> rollback).
 *  - Field mapping (source column -> ERP field) with reusable templates.
 *  - Validation (required/type/duplicate) with a per-row error report.
 *  - Opening-balance trial-balance check (Dr must equal Cr before commit).
 *  - Idempotent upsert by natural key; rollback by batch.
 *
 * Pure transform helpers + persisted batch metadata. The actual write into each
 * target module uses that module's own save functions (passed by the caller),
 * so migration never bypasses business rules.
 *
 * Additive: new epc_mig_* tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_mig_ensure_schema')) {
    function epc_mig_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mig_batches` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(50) NOT NULL DEFAULT '',
            `entity` varchar(40) NOT NULL DEFAULT '',
            `source_system` varchar(40) NOT NULL DEFAULT 'excel',
            `status` varchar(16) NOT NULL DEFAULT 'draft',
            `row_count` int(11) NOT NULL DEFAULT 0,
            `error_count` int(11) NOT NULL DEFAULT 0,
            `committed_count` int(11) NOT NULL DEFAULT 0,
            `created_by` varchar(80) DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_committed` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Migration batches'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mig_rows` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `batch_id` int(11) NOT NULL,
            `row_no` int(11) NOT NULL DEFAULT 0,
            `natural_key` varchar(120) DEFAULT NULL,
            `payload` mediumtext,
            `status` varchar(16) NOT NULL DEFAULT 'pending',
            `error` varchar(255) DEFAULT NULL,
            `target_id` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_batch` (`batch_id`),
            KEY `x_natkey` (`batch_id`,`natural_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Migration rows'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mig_mappings` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `source_system` varchar(40) NOT NULL DEFAULT '',
            `entity` varchar(40) NOT NULL DEFAULT '',
            `map_json` mediumtext,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_src_ent` (`source_system`,`entity`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Migration field maps'");
    }
}

/* --------------------------- Field mapping ---------------------------- */

if (!function_exists('epc_mig_save_mapping')) {
    /**
     * Save a reusable source->ERP field map for (system, entity).
     *
     * @param array<string,string> $map source_column => erp_field
     */
    function epc_mig_save_mapping(PDO $db, string $sourceSystem, string $entity, array $map): int
    {
        epc_mig_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_mig_mappings` (`source_system`,`entity`,`map_json`) VALUES (?,?,?)
                      ON DUPLICATE KEY UPDATE `map_json`=VALUES(`map_json`)")
           ->execute(array($sourceSystem, $entity, json_encode($map)));
        $row = $db->prepare("SELECT `id` FROM `epc_mig_mappings` WHERE `source_system`=? AND `entity`=?");
        $row->execute(array($sourceSystem, $entity));
        return (int) $row->fetchColumn();
    }
}

if (!function_exists('epc_mig_apply_mapping')) {
    /**
     * Transform a source row into an ERP payload using the field map. Unmapped
     * source columns are dropped.
     *
     * @param array<string,string> $map source_column => erp_field
     * @param array<string,mixed> $sourceRow
     * @return array<string,mixed>
     */
    function epc_mig_apply_mapping(array $map, array $sourceRow): array
    {
        $out = array();
        foreach ($map as $src => $erpField) {
            if (array_key_exists($src, $sourceRow)) {
                $out[$erpField] = $sourceRow[$src];
            }
        }
        return $out;
    }
}

/* ----------------------------- Validation ----------------------------- */

if (!function_exists('epc_mig_validate_rows')) {
    /**
     * Validate mapped rows against a spec. Spec:
     *  {required: [fields], numeric: [fields], natural_key: field}
     * Flags missing-required, non-numeric, and duplicate natural keys within
     * the batch.
     *
     * @param array<int,array<string,mixed>> $rows mapped ERP payloads
     * @param array<string,mixed> $spec
     * @return array<string,mixed> {valid, errors:[{row_no, error}], seen_keys}
     */
    function epc_mig_validate_rows(array $rows, array $spec): array
    {
        $required = $spec['required'] ?? array();
        $numeric = $spec['numeric'] ?? array();
        $natKeyField = $spec['natural_key'] ?? '';
        $errors = array();
        $seen = array();
        foreach ($rows as $i => $row) {
            $rowNo = $i + 1;
            foreach ($required as $f) {
                if (!isset($row[$f]) || $row[$f] === '' || $row[$f] === null) {
                    $errors[] = array('row_no' => $rowNo, 'error' => "missing required field '$f'");
                }
            }
            foreach ($numeric as $f) {
                if (isset($row[$f]) && $row[$f] !== '' && !is_numeric($row[$f])) {
                    $errors[] = array('row_no' => $rowNo, 'error' => "field '$f' must be numeric");
                }
            }
            if ($natKeyField !== '' && isset($row[$natKeyField]) && $row[$natKeyField] !== '') {
                $k = (string) $row[$natKeyField];
                if (isset($seen[$k])) {
                    $errors[] = array('row_no' => $rowNo, 'error' => "duplicate natural key '$k' (also row {$seen[$k]})");
                } else {
                    $seen[$k] = $rowNo;
                }
            }
        }
        return array('valid' => count($errors) === 0, 'errors' => $errors, 'seen_keys' => array_keys($seen));
    }
}

if (!function_exists('epc_mig_opening_balance_check')) {
    /**
     * Opening-balance trial-balance check: total debit must equal total credit.
     *
     * @param array<int,array<string,mixed>> $glRows each {debit, credit}
     * @return array<string,mixed> {total_debit, total_credit, difference, balanced}
     */
    function epc_mig_opening_balance_check(array $glRows): array
    {
        $dr = 0.0;
        $cr = 0.0;
        foreach ($glRows as $r) {
            $dr = round($dr + (float) ($r['debit'] ?? 0), 2);
            $cr = round($cr + (float) ($r['credit'] ?? 0), 2);
        }
        return array('total_debit' => $dr, 'total_credit' => $cr, 'difference' => round($dr - $cr, 2), 'balanced' => abs($dr - $cr) < 0.01);
    }
}

/* ------------------------------- Batches ------------------------------ */

if (!function_exists('epc_mig_batch_create')) {
    /**
     * Create a batch and stage its rows (validated, not yet committed).
     *
     * @param array<int,array<string,mixed>> $mappedRows
     * @param array<string,mixed> $spec validation spec (natural_key etc.)
     * @return array<string,mixed> {batch_id, row_count, error_count, valid}
     */
    function epc_mig_batch_create(PDO $db, string $code, string $entity, string $sourceSystem, array $mappedRows, array $spec = array(), string $by = ''): array
    {
        epc_mig_ensure_schema($db);
        $validation = epc_mig_validate_rows($mappedRows, $spec);
        $errByRow = array();
        foreach ($validation['errors'] as $e) {
            $errByRow[$e['row_no']][] = $e['error'];
        }
        $db->prepare("INSERT INTO `epc_mig_batches` (`code`,`entity`,`source_system`,`status`,`row_count`,`error_count`,`created_by`,`time_created`) VALUES (?,?,?, ?, ?, ?, ?, ?)")
           ->execute(array($code, $entity, $sourceSystem, $validation['valid'] ? 'validated' : 'error', count($mappedRows), count($validation['errors']), $by, time()));
        $batchId = (int) $db->lastInsertId();
        $natKeyField = (string) ($spec['natural_key'] ?? '');
        $ins = $db->prepare("INSERT INTO `epc_mig_rows` (`batch_id`,`row_no`,`natural_key`,`payload`,`status`,`error`) VALUES (?,?,?,?,?,?)");
        foreach ($mappedRows as $i => $row) {
            $rowNo = $i + 1;
            $natKey = $natKeyField !== '' ? (string) ($row[$natKeyField] ?? '') : '';
            $err = isset($errByRow[$rowNo]) ? implode('; ', $errByRow[$rowNo]) : null;
            $ins->execute(array($batchId, $rowNo, $natKey, json_encode($row), $err === null ? 'valid' : 'error', $err));
        }
        return array('batch_id' => $batchId, 'row_count' => count($mappedRows), 'error_count' => count($validation['errors']), 'valid' => $validation['valid']);
    }
}

if (!function_exists('epc_mig_batch_commit')) {
    /**
     * Commit a validated batch by calling $writer($payload, $naturalKey) for
     * each valid row. $writer returns the created/updated target id (idempotent
     * upsert is the writer's responsibility, keyed by natural key). Refuses to
     * commit a batch with errors.
     *
     * @param callable $writer fn(array $payload, string $naturalKey): int
     * @return array<string,mixed> {committed, status, skipped}
     */
    function epc_mig_batch_commit(PDO $db, int $batchId, callable $writer): array
    {
        epc_mig_ensure_schema($db);
        $b = $db->prepare("SELECT * FROM `epc_mig_batches` WHERE `id`=?");
        $b->execute(array($batchId));
        $batch = $b->fetch(PDO::FETCH_ASSOC);
        if (!$batch) {
            throw new Exception('Batch not found');
        }
        if ((int) $batch['error_count'] > 0) {
            return array('committed' => 0, 'status' => 'blocked', 'skipped' => (int) $batch['row_count'], 'reason' => 'validation_errors');
        }
        if ((string) $batch['status'] === 'committed') {
            return array('committed' => (int) $batch['committed_count'], 'status' => 'already_committed', 'skipped' => 0);
        }
        $rows = $db->prepare("SELECT * FROM `epc_mig_rows` WHERE `batch_id`=? AND `status`='valid' ORDER BY `row_no`");
        $rows->execute(array($batchId));
        $committed = 0;
        $upd = $db->prepare("UPDATE `epc_mig_rows` SET `status`='committed', `target_id`=? WHERE `id`=?");
        foreach ($rows->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $payload = json_decode((string) $r['payload'], true) ?: array();
            $targetId = (int) $writer($payload, (string) $r['natural_key']);
            $upd->execute(array($targetId, (int) $r['id']));
            $committed++;
        }
        $db->prepare("UPDATE `epc_mig_batches` SET `status`='committed', `committed_count`=?, `time_committed`=? WHERE `id`=?")
           ->execute(array($committed, time(), $batchId));
        return array('committed' => $committed, 'status' => 'committed', 'skipped' => 0);
    }
}

if (!function_exists('epc_mig_batch_rollback')) {
    /**
     * Roll back a committed batch by calling $remover($targetId) for each
     * committed row, then mark the batch rolled_back. Safe/idempotent.
     *
     * @param callable $remover fn(int $targetId): void
     * @return array<string,mixed> {rolled_back, status}
     */
    function epc_mig_batch_rollback(PDO $db, int $batchId, callable $remover): array
    {
        epc_mig_ensure_schema($db);
        $rows = $db->prepare("SELECT * FROM `epc_mig_rows` WHERE `batch_id`=? AND `status`='committed' AND `target_id`>0");
        $rows->execute(array($batchId));
        $count = 0;
        $upd = $db->prepare("UPDATE `epc_mig_rows` SET `status`='rolled_back' WHERE `id`=?");
        foreach ($rows->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $remover((int) $r['target_id']);
            $upd->execute(array((int) $r['id']));
            $count++;
        }
        $db->prepare("UPDATE `epc_mig_batches` SET `status`='rolled_back' WHERE `id`=?")->execute(array($batchId));
        return array('rolled_back' => $count, 'status' => 'rolled_back');
    }
}
