<?php
/**
 * P2 #35 — Config Sandbox
 *
 * Test configuration changes before go-live: snapshot current config,
 * apply changes in sandbox, preview diff, promote or discard.
 * Schema: epc_config_snapshots, epc_sandbox_changes
 */

if (!defined('EPC_CONFIG_SANDBOX_VERSION')) {
    define('EPC_CONFIG_SANDBOX_VERSION', '1.0.0');
}

function epc_sandbox_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_config_snapshots` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `snapshot_name`   VARCHAR(128)   NOT NULL,
            `config_data`     LONGTEXT       NOT NULL,
            `status`          ENUM('active','promoted','discarded') NOT NULL DEFAULT 'active',
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `promoted_at`     DATETIME       NULL,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_sandbox_changes` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `snapshot_id`     INT UNSIGNED   NOT NULL,
            `change_key`      VARCHAR(128)   NOT NULL,
            `old_value`       TEXT           NULL,
            `new_value`       TEXT           NULL,
            `change_type`     ENUM('add','modify','delete') NOT NULL DEFAULT 'modify',
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_snapshot` (`snapshot_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_sandbox_create(PDO $pdo, string $siteKey, string $name, array $configData, int $userId = 0): array
{
    epc_sandbox_ensure_schema($pdo);
    $st = $pdo->prepare("INSERT INTO `epc_config_snapshots` (`site_key`,`snapshot_name`,`config_data`,`created_by`) VALUES (?,?,?,?)");
    $st->execute(array($siteKey, $name, json_encode($configData), $userId));
    return array('ok' => true, 'snapshot_id' => (int)$pdo->lastInsertId());
}

function epc_sandbox_apply_change(PDO $pdo, int $snapshotId, string $key, string $oldVal, string $newVal, string $type = 'modify'): array
{
    $pdo->prepare("INSERT INTO `epc_sandbox_changes` (`snapshot_id`,`change_key`,`old_value`,`new_value`,`change_type`) VALUES (?,?,?,?,?)")
        ->execute(array($snapshotId, $key, $oldVal, $newVal, $type));
    return array('ok' => true);
}

function epc_sandbox_diff(PDO $pdo, int $snapshotId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_sandbox_changes` WHERE `snapshot_id`=? ORDER BY `created_at`");
    $st->execute(array($snapshotId));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_sandbox_promote(PDO $pdo, int $snapshotId): array
{
    $pdo->prepare("UPDATE `epc_config_snapshots` SET `status`='promoted', `promoted_at`=NOW() WHERE `id`=? AND `status`='active'")
        ->execute(array($snapshotId));
    return array('ok' => true);
}

function epc_sandbox_discard(PDO $pdo, int $snapshotId): array
{
    $pdo->prepare("UPDATE `epc_config_snapshots` SET `status`='discarded' WHERE `id`=? AND `status`='active'")
        ->execute(array($snapshotId));
    return array('ok' => true);
}

function epc_sandbox_list(PDO $pdo, string $siteKey): array
{
    epc_sandbox_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT s.*, (SELECT COUNT(*) FROM `epc_sandbox_changes` c WHERE c.`snapshot_id`=s.`id`) AS `change_count` FROM `epc_config_snapshots` s WHERE `site_key`=? ORDER BY `created_at` DESC");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_sandbox_fleet_stats(PDO $pdo): array
{
    epc_sandbox_ensure_schema($pdo);
    $st = $pdo->query("SELECT `site_key`, COUNT(*) AS `snapshots`, SUM(CASE WHEN `status`='active' THEN 1 ELSE 0 END) AS `active`, SUM(CASE WHEN `status`='promoted' THEN 1 ELSE 0 END) AS `promoted` FROM `epc_config_snapshots` GROUP BY `site_key`");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── Snapshot Comparison ─── */

function epc_sandbox_compare(PDO $pdo, int $snapshotA, int $snapshotB): array
{
    epc_sandbox_ensure_schema($pdo);
    $stA = $pdo->prepare("SELECT `config_data` FROM `epc_config_snapshots` WHERE `id`=?");
    $stA->execute(array($snapshotA));
    $dataA = json_decode((string) $stA->fetchColumn(), true) ?: array();

    $stB = $pdo->prepare("SELECT `config_data` FROM `epc_config_snapshots` WHERE `id`=?");
    $stB->execute(array($snapshotB));
    $dataB = json_decode((string) $stB->fetchColumn(), true) ?: array();

    $added = array_diff_key($dataB, $dataA);
    $removed = array_diff_key($dataA, $dataB);
    $changed = array();
    foreach (array_intersect_key($dataA, $dataB) as $key => $valA) {
        if (json_encode($valA) !== json_encode($dataB[$key])) {
            $changed[$key] = array('from' => $valA, 'to' => $dataB[$key]);
        }
    }

    return array('added' => $added, 'removed' => $removed, 'changed' => $changed, 'total_diffs' => count($added) + count($removed) + count($changed));
}

function epc_sandbox_rollback(PDO $pdo, int $snapshotId): array
{
    epc_sandbox_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT `config_data`, `site_key` FROM `epc_config_snapshots` WHERE `id`=? AND `status`='promoted'");
    $st->execute(array($snapshotId));
    $row = $st->fetch(PDO::FETCH_ASSOC);
    if (!$row) {
        return array('ok' => false, 'error' => 'Snapshot not found or not promoted');
    }
    $newId = epc_sandbox_create($pdo, $row['site_key'], 'Rollback of snapshot #' . $snapshotId, json_decode($row['config_data'], true) ?: array());
    epc_sandbox_promote($pdo, $newId['snapshot_id']);
    return array('ok' => true, 'new_snapshot_id' => $newId['snapshot_id']);
}

function epc_sandbox_export(PDO $pdo, int $snapshotId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_config_snapshots` WHERE `id`=?");
    $st->execute(array($snapshotId));
    $row = $st->fetch(PDO::FETCH_ASSOC);
    if (!$row) return array('ok' => false, 'error' => 'Not found');
    $changes = epc_sandbox_diff($pdo, $snapshotId);
    return array('ok' => true, 'snapshot' => $row, 'changes' => $changes, 'exported_at' => date('c'));
}

function epc_sandbox_import(PDO $pdo, string $siteKey, array $exportData, int $userId = 0): array
{
    if (empty($exportData['snapshot']['config_data'])) {
        return array('ok' => false, 'error' => 'Invalid export data');
    }
    $config = json_decode($exportData['snapshot']['config_data'], true) ?: array();
    return epc_sandbox_create($pdo, $siteKey, 'Imported snapshot', $config, $userId);
}
