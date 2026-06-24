<?php
/**
 * P1 #12 — PO Approval Workflow
 *
 * 3-tier purchase order approval: requester → manager → finance.
 * Schema: epc_po_requests, epc_po_approval_steps
 * Supports: threshold-based auto-approve, delegation, escalation.
 */

if (!defined('EPC_PO_APPROVAL_VERSION')) {
    define('EPC_PO_APPROVAL_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_po_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_po_requests` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `po_number`       VARCHAR(64)    NOT NULL,
            `requester_id`    INT UNSIGNED   NOT NULL,
            `vendor_id`       INT UNSIGNED   NOT NULL DEFAULT 0,
            `vendor_name`     VARCHAR(255)   NOT NULL DEFAULT '',
            `description`     TEXT           NOT NULL,
            `currency`        CHAR(3)        NOT NULL DEFAULT 'AED',
            `subtotal`        DECIMAL(15,2)  NOT NULL DEFAULT 0.00,
            `tax`             DECIMAL(15,2)  NOT NULL DEFAULT 0.00,
            `total`           DECIMAL(15,2)  NOT NULL DEFAULT 0.00,
            `status`          ENUM('draft','pending','approved','rejected','cancelled') NOT NULL DEFAULT 'draft',
            `current_tier`    TINYINT UNSIGNED NOT NULL DEFAULT 0,
            `priority`        ENUM('low','normal','high','urgent') NOT NULL DEFAULT 'normal',
            `items`           JSON           NULL,
            `attachments`     JSON           NULL,
            `notes`           TEXT           NOT NULL,
            `approved_at`     DATETIME       NULL,
            `rejected_at`     DATETIME       NULL,
            `rejection_reason` VARCHAR(512)  NOT NULL DEFAULT '',
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_po` (`site_key`, `po_number`),
            INDEX `idx_status` (`status`),
            INDEX `idx_requester` (`requester_id`),
            INDEX `idx_tier` (`current_tier`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_po_approval_steps` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `po_id`           INT UNSIGNED   NOT NULL,
            `tier`            TINYINT UNSIGNED NOT NULL,
            `tier_label`      VARCHAR(64)    NOT NULL DEFAULT '',
            `approver_id`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `approver_name`   VARCHAR(128)   NOT NULL DEFAULT '',
            `decision`        ENUM('pending','approved','rejected','skipped','auto_approved') NOT NULL DEFAULT 'pending',
            `comment`         TEXT           NOT NULL,
            `decided_at`      DATETIME       NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_po` (`po_id`),
            INDEX `idx_approver` (`approver_id`),
            INDEX `idx_decision` (`decision`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── approval tiers ─── */

function epc_po_approval_tiers(): array
{
    return array(
        1 => array('label' => 'Manager Approval',  'role' => 'manager',  'threshold' => 0),
        2 => array('label' => 'Finance Approval',  'role' => 'finance',  'threshold' => 5000),
        3 => array('label' => 'Director Approval',  'role' => 'director', 'threshold' => 50000),
    );
}

function epc_po_required_tiers(float $total): array
{
    $tiers = epc_po_approval_tiers();
    $required = array();
    foreach ($tiers as $num => $tier) {
        if ($total >= $tier['threshold']) {
            $required[$num] = $tier;
        }
    }
    return $required;
}

/* ─── create PO request ─── */

function epc_po_create(PDO $pdo, string $siteKey, array $data): array
{
    epc_po_ensure_schema($pdo);

    $poNumber = 'PO-' . strtoupper($siteKey) . '-' . date('Ymd') . '-' . str_pad(mt_rand(1, 9999), 4, '0', STR_PAD_LEFT);
    $total = (float) ($data['total'] ?? 0);

    $st = $pdo->prepare("
        INSERT INTO `epc_po_requests`
            (`site_key`, `po_number`, `requester_id`, `vendor_id`, `vendor_name`, `description`,
             `currency`, `subtotal`, `tax`, `total`, `status`, `current_tier`, `priority`, `items`, `notes`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'pending', 1, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        $poNumber,
        (int) ($data['requester_id'] ?? 0),
        (int) ($data['vendor_id'] ?? 0),
        (string) ($data['vendor_name'] ?? ''),
        (string) ($data['description'] ?? ''),
        (string) ($data['currency'] ?? 'AED'),
        (float) ($data['subtotal'] ?? $total),
        (float) ($data['tax'] ?? 0),
        $total,
        (string) ($data['priority'] ?? 'normal'),
        json_encode($data['items'] ?? array()),
        (string) ($data['notes'] ?? ''),
    ));

    $poId = (int) $pdo->lastInsertId();

    $requiredTiers = epc_po_required_tiers($total);
    foreach ($requiredTiers as $num => $tier) {
        $st = $pdo->prepare("
            INSERT INTO `epc_po_approval_steps` (`po_id`, `tier`, `tier_label`, `comment`)
            VALUES (?, ?, ?, '')
        ");
        $st->execute(array($poId, $num, $tier['label']));
    }

    return array('ok' => true, 'po_id' => $poId, 'po_number' => $poNumber, 'tiers_required' => count($requiredTiers));
}

/* ─── get PO with approval steps ─── */

function epc_po_get(PDO $pdo, int $poId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_po_requests` WHERE `id` = ?");
    $st->execute(array($poId));
    $po = $st->fetch(PDO::FETCH_ASSOC);
    if (!$po) {
        return array();
    }

    $st = $pdo->prepare("SELECT * FROM `epc_po_approval_steps` WHERE `po_id` = ? ORDER BY `tier`");
    $st->execute(array($poId));
    $po['approval_steps'] = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    $po['items'] = json_decode($po['items'] ?: '[]', true);
    $po['attachments'] = json_decode($po['attachments'] ?: '[]', true);

    return $po;
}

/* ─── approve a tier ─── */

function epc_po_approve(PDO $pdo, int $poId, int $tier, int $approverId, string $comment = ''): array
{
    $po = epc_po_get($pdo, $poId);
    if (empty($po)) {
        return array('ok' => false, 'error' => 'PO not found');
    }
    if ($po['status'] !== 'pending') {
        return array('ok' => false, 'error' => 'PO is not pending approval');
    }
    if ((int) $po['current_tier'] !== $tier) {
        return array('ok' => false, 'error' => 'Not the current approval tier');
    }

    $st = $pdo->prepare("
        UPDATE `epc_po_approval_steps`
        SET `decision` = 'approved', `approver_id` = ?, `comment` = ?, `decided_at` = NOW()
        WHERE `po_id` = ? AND `tier` = ? AND `decision` = 'pending'
    ");
    $st->execute(array($approverId, $comment, $poId, $tier));

    $nextTier = $tier + 1;
    $stNext = $pdo->prepare("SELECT COUNT(*) FROM `epc_po_approval_steps` WHERE `po_id` = ? AND `tier` = ?");
    $stNext->execute(array($poId, $nextTier));
    $hasNext = (int) $stNext->fetchColumn() > 0;

    if ($hasNext) {
        $pdo->prepare("UPDATE `epc_po_requests` SET `current_tier` = ? WHERE `id` = ?")->execute(array($nextTier, $poId));
        return array('ok' => true, 'status' => 'pending', 'next_tier' => $nextTier);
    }

    $pdo->prepare("UPDATE `epc_po_requests` SET `status` = 'approved', `approved_at` = NOW() WHERE `id` = ?")->execute(array($poId));
    return array('ok' => true, 'status' => 'approved', 'message' => 'PO fully approved');
}

/* ─── reject ─── */

function epc_po_reject(PDO $pdo, int $poId, int $tier, int $approverId, string $reason = ''): array
{
    $po = epc_po_get($pdo, $poId);
    if (empty($po)) {
        return array('ok' => false, 'error' => 'PO not found');
    }

    $st = $pdo->prepare("
        UPDATE `epc_po_approval_steps`
        SET `decision` = 'rejected', `approver_id` = ?, `comment` = ?, `decided_at` = NOW()
        WHERE `po_id` = ? AND `tier` = ?
    ");
    $st->execute(array($approverId, $reason, $poId, $tier));

    $pdo->prepare("UPDATE `epc_po_requests` SET `status` = 'rejected', `rejected_at` = NOW(), `rejection_reason` = ? WHERE `id` = ?")->execute(array($reason, $poId));

    return array('ok' => true, 'status' => 'rejected', 'reason' => $reason);
}

/* ─── list POs ─── */

function epc_po_list(PDO $pdo, string $siteKey, array $filters = array(), int $limit = 50, int $offset = 0): array
{
    epc_po_ensure_schema($pdo);

    $where = array('`site_key` = ?');
    $params = array($siteKey);

    if (!empty($filters['status'])) {
        $where[] = '`status` = ?';
        $params[] = (string) $filters['status'];
    }
    if (!empty($filters['requester_id'])) {
        $where[] = '`requester_id` = ?';
        $params[] = (int) $filters['requester_id'];
    }
    if (!empty($filters['priority'])) {
        $where[] = '`priority` = ?';
        $params[] = (string) $filters['priority'];
    }

    $sql = 'SELECT * FROM `epc_po_requests` WHERE ' . implode(' AND ', $where)
         . ' ORDER BY `created_at` DESC LIMIT ' . (int) $limit . ' OFFSET ' . (int) $offset;
    $st = $pdo->prepare($sql);
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── pending approvals for a user ─── */

function epc_po_pending_for_approver(PDO $pdo, string $siteKey, string $role): array
{
    epc_po_ensure_schema($pdo);

    $tiers = epc_po_approval_tiers();
    $tierNums = array();
    foreach ($tiers as $num => $tier) {
        if ($tier['role'] === $role) {
            $tierNums[] = $num;
        }
    }

    if (empty($tierNums)) {
        return array();
    }

    $placeholders = implode(',', array_fill(0, count($tierNums), '?'));
    $params = array_merge(array($siteKey), $tierNums);

    $st = $pdo->prepare("
        SELECT p.* FROM `epc_po_requests` p
        WHERE p.`site_key` = ? AND p.`status` = 'pending' AND p.`current_tier` IN ({$placeholders})
        ORDER BY p.`priority` DESC, p.`created_at` ASC
    ");
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── auto-approve below threshold ─── */

function epc_po_auto_approve_check(PDO $pdo, int $poId): array
{
    $po = epc_po_get($pdo, $poId);
    if (empty($po) || $po['status'] !== 'pending') {
        return array('auto_approved' => false);
    }

    $autoApproveThreshold = 500.00;
    if ((float) $po['total'] <= $autoApproveThreshold) {
        foreach ($po['approval_steps'] as $step) {
            if ($step['decision'] === 'pending') {
                $st = $pdo->prepare("
                    UPDATE `epc_po_approval_steps`
                    SET `decision` = 'auto_approved', `approver_name` = 'System', `comment` = 'Auto-approved: below threshold', `decided_at` = NOW()
                    WHERE `id` = ?
                ");
                $st->execute(array($step['id']));
            }
        }
        $pdo->prepare("UPDATE `epc_po_requests` SET `status` = 'approved', `approved_at` = NOW() WHERE `id` = ?")->execute(array($poId));
        return array('auto_approved' => true, 'threshold' => $autoApproveThreshold);
    }

    return array('auto_approved' => false);
}

/* ─── cancel PO ─── */

function epc_po_cancel(PDO $pdo, int $poId, int $userId): array
{
    $po = epc_po_get($pdo, $poId);
    if (empty($po)) {
        return array('ok' => false, 'error' => 'PO not found');
    }
    if ($po['status'] === 'approved') {
        return array('ok' => false, 'error' => 'Cannot cancel approved PO');
    }

    $pdo->prepare("UPDATE `epc_po_requests` SET `status` = 'cancelled' WHERE `id` = ?")->execute(array($poId));
    return array('ok' => true);
}

/* ─── fleet PO stats (BOS) ─── */

function epc_po_fleet_stats(PDO $pdo): array
{
    epc_po_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(*) AS `total_pos`,
               SUM(CASE WHEN `status` = 'pending' THEN 1 ELSE 0 END) AS `pending`,
               SUM(CASE WHEN `status` = 'approved' THEN 1 ELSE 0 END) AS `approved`,
               SUM(CASE WHEN `status` = 'rejected' THEN 1 ELSE 0 END) AS `rejected`,
               SUM(`total`) AS `total_value`,
               AVG(TIMESTAMPDIFF(HOUR, `created_at`, COALESCE(`approved_at`, `rejected_at`, NOW()))) AS `avg_turnaround_hrs`
        FROM `epc_po_requests`
        GROUP BY `site_key`
        ORDER BY `pending` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
