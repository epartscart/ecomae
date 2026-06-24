<?php
/**
 * P2 #27 — Collections & Dunning Automation
 *
 * Automated overdue invoice tracking, dunning letter sequences,
 * payment reminders, escalation rules, and collection status.
 * Schema: epc_dunning_profiles, epc_dunning_queue, epc_dunning_log
 */

if (!defined('EPC_COLLECTIONS_VERSION')) {
    define('EPC_COLLECTIONS_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_dunning_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_dunning_profiles` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `name`            VARCHAR(128)   NOT NULL,
            `steps`           JSON           NOT NULL,
            `active`          TINYINT(1)     NOT NULL DEFAULT 1,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_dunning_queue` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `customer_id`     INT UNSIGNED   NOT NULL,
            `customer_name`   VARCHAR(128)   NOT NULL DEFAULT '',
            `invoice_ref`     VARCHAR(64)    NOT NULL,
            `invoice_amount`  DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `amount_due`      DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `due_date`        DATE           NOT NULL,
            `days_overdue`    INT            NOT NULL DEFAULT 0,
            `dunning_step`    TINYINT UNSIGNED NOT NULL DEFAULT 0,
            `profile_id`      INT UNSIGNED   NULL,
            `status`          ENUM('open','in_progress','promised','partial','paid','written_off','disputed') NOT NULL DEFAULT 'open',
            `next_action_date`DATE           NULL,
            `assigned_to`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `notes`           TEXT           NULL,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX `idx_site_status` (`site_key`, `status`),
            INDEX `idx_overdue` (`days_overdue`),
            INDEX `idx_next_action` (`next_action_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_dunning_log` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `queue_id`        BIGINT UNSIGNED NOT NULL,
            `action_type`     ENUM('email','sms','call','letter','escalation','note','payment','write_off') NOT NULL,
            `details`         TEXT           NOT NULL,
            `performed_by`    INT UNSIGNED   NOT NULL DEFAULT 0,
            `performed_at`    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_queue` (`queue_id`),
            INDEX `idx_type` (`action_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── default dunning profile ─── */

function epc_dunning_default_steps(): array
{
    return array(
        array('day' => 1,  'action' => 'email', 'template' => 'friendly_reminder', 'subject' => 'Friendly Payment Reminder'),
        array('day' => 7,  'action' => 'email', 'template' => 'first_notice',      'subject' => 'Payment Notice — Invoice Overdue'),
        array('day' => 14, 'action' => 'email', 'template' => 'second_notice',     'subject' => 'Second Payment Notice — Urgent'),
        array('day' => 21, 'action' => 'call',  'template' => 'phone_followup',    'subject' => 'Phone Follow-Up Required'),
        array('day' => 30, 'action' => 'letter','template' => 'formal_demand',     'subject' => 'Formal Demand for Payment'),
        array('day' => 45, 'action' => 'escalation', 'template' => 'escalation',   'subject' => 'Account Escalated to Collections'),
        array('day' => 60, 'action' => 'letter','template' => 'final_notice',      'subject' => 'Final Notice Before Legal Action'),
    );
}

/* ─── profile CRUD ─── */

function epc_dunning_profile_create(PDO $pdo, string $siteKey, string $name, array $steps = array()): array
{
    epc_dunning_ensure_schema($pdo);
    if (empty($steps)) {
        $steps = epc_dunning_default_steps();
    }

    $st = $pdo->prepare("INSERT INTO `epc_dunning_profiles` (`site_key`, `name`, `steps`) VALUES (?, ?, ?)");
    $st->execute(array($siteKey, $name, json_encode($steps)));
    return array('ok' => true, 'profile_id' => (int) $pdo->lastInsertId());
}

function epc_dunning_profile_list(PDO $pdo, string $siteKey): array
{
    epc_dunning_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT * FROM `epc_dunning_profiles` WHERE `site_key` = ? ORDER BY `name`");
    $st->execute(array($siteKey));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) {
        $r['steps'] = json_decode($r['steps'] ?: '[]', true);
    }
    return $rows;
}

/* ─── queue management ─── */

function epc_dunning_add_invoice(PDO $pdo, string $siteKey, array $invoice): array
{
    epc_dunning_ensure_schema($pdo);

    $dueDate = (string) ($invoice['due_date'] ?? date('Y-m-d'));
    $daysOverdue = max(0, (int) ((time() - strtotime($dueDate)) / 86400));

    $st = $pdo->prepare("
        INSERT INTO `epc_dunning_queue`
            (`site_key`, `customer_id`, `customer_name`, `invoice_ref`, `invoice_amount`,
             `amount_due`, `due_date`, `days_overdue`, `profile_id`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (int) ($invoice['customer_id'] ?? 0),
        (string) ($invoice['customer_name'] ?? ''),
        (string) ($invoice['invoice_ref'] ?? ''),
        (float) ($invoice['invoice_amount'] ?? 0),
        (float) ($invoice['amount_due'] ?? $invoice['invoice_amount'] ?? 0),
        $dueDate,
        $daysOverdue,
        (int) ($invoice['profile_id'] ?? 0) ?: null,
    ));

    return array('ok' => true, 'queue_id' => (int) $pdo->lastInsertId(), 'days_overdue' => $daysOverdue);
}

function epc_dunning_queue_list(PDO $pdo, string $siteKey, array $filters = array()): array
{
    epc_dunning_ensure_schema($pdo);

    $where = array('`site_key` = ?');
    $params = array($siteKey);

    if (!empty($filters['status'])) {
        $where[] = '`status` = ?';
        $params[] = $filters['status'];
    }
    if (!empty($filters['min_days'])) {
        $where[] = '`days_overdue` >= ?';
        $params[] = (int) $filters['min_days'];
    }

    $st = $pdo->prepare("SELECT * FROM `epc_dunning_queue` WHERE " . implode(' AND ', $where) . " ORDER BY `days_overdue` DESC");
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_dunning_update_status(PDO $pdo, int $queueId, string $status, string $notes = '', int $userId = 0): array
{
    $pdo->prepare("UPDATE `epc_dunning_queue` SET `status` = ?, `notes` = ? WHERE `id` = ?")->execute(array($status, $notes, $queueId));
    $pdo->prepare("INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`, `performed_by`) VALUES (?, 'note', ?, ?)")->execute(array($queueId, 'Status → ' . $status . ': ' . $notes, $userId));
    return array('ok' => true);
}

function epc_dunning_record_payment(PDO $pdo, int $queueId, float $amount, int $userId = 0): array
{
    $st = $pdo->prepare("SELECT `amount_due` FROM `epc_dunning_queue` WHERE `id` = ?");
    $st->execute(array($queueId));
    $due = (float) $st->fetchColumn();

    $newDue = max(0, $due - $amount);
    $newStatus = $newDue <= 0 ? 'paid' : 'partial';

    $pdo->prepare("UPDATE `epc_dunning_queue` SET `amount_due` = ?, `status` = ? WHERE `id` = ?")->execute(array($newDue, $newStatus, $queueId));
    $pdo->prepare("INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`, `performed_by`) VALUES (?, 'payment', ?, ?)")->execute(array($queueId, 'Payment received: ' . number_format($amount, 2), $userId));

    return array('ok' => true, 'remaining' => $newDue, 'status' => $newStatus);
}

/* ─── process dunning (advance steps) ─── */

function epc_dunning_process(PDO $pdo, string $siteKey): array
{
    epc_dunning_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT q.*, p.`steps`
        FROM `epc_dunning_queue` q
        LEFT JOIN `epc_dunning_profiles` p ON q.`profile_id` = p.`id`
        WHERE q.`site_key` = ? AND q.`status` IN ('open', 'in_progress')
    ");
    $st->execute(array($siteKey));
    $items = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $daysOverdue = max(0, (int) ((time() - strtotime(date('Y-m-d'))) / 86400));
    $actioned = 0;

    foreach ($items as $item) {
        $steps = json_decode($item['steps'] ?: '[]', true);
        if (empty($steps)) {
            $steps = epc_dunning_default_steps();
        }

        $itemDays = max(0, (int) ((time() - strtotime($item['due_date'])) / 86400));
        $pdo->prepare("UPDATE `epc_dunning_queue` SET `days_overdue` = ? WHERE `id` = ?")->execute(array($itemDays, $item['id']));

        $currentStep = (int) $item['dunning_step'];
        if ($currentStep < count($steps) && $itemDays >= $steps[$currentStep]['day']) {
            $step = $steps[$currentStep];
            $pdo->prepare("UPDATE `epc_dunning_queue` SET `dunning_step` = ?, `status` = 'in_progress' WHERE `id` = ?")->execute(array($currentStep + 1, $item['id']));
            $pdo->prepare("INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`) VALUES (?, ?, ?)")->execute(array($item['id'], $step['action'], 'Step ' . ($currentStep + 1) . ': ' . $step['subject']));
            $actioned++;
        }
    }

    return array('ok' => true, 'processed' => count($items), 'actioned' => $actioned);
}

/* ─── aging report ─── */

function epc_dunning_aging(PDO $pdo, string $siteKey): array
{
    epc_dunning_ensure_schema($pdo);

    $buckets = array('current' => 0, '1_30' => 0, '31_60' => 0, '61_90' => 0, 'over_90' => 0);
    $st = $pdo->prepare("SELECT `days_overdue`, `amount_due` FROM `epc_dunning_queue` WHERE `site_key` = ? AND `status` NOT IN ('paid', 'written_off')");
    $st->execute(array($siteKey));

    foreach ($st->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
        $d = (int) $row['days_overdue'];
        $a = (float) $row['amount_due'];
        if ($d <= 0) $buckets['current'] += $a;
        elseif ($d <= 30) $buckets['1_30'] += $a;
        elseif ($d <= 60) $buckets['31_60'] += $a;
        elseif ($d <= 90) $buckets['61_90'] += $a;
        else $buckets['over_90'] += $a;
    }

    return $buckets;
}

/* ─── fleet stats (BOS) ─── */

function epc_dunning_fleet_stats(PDO $pdo): array
{
    epc_dunning_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(*) AS `total_items`,
               SUM(CASE WHEN `status` IN ('open','in_progress') THEN 1 ELSE 0 END) AS `active`,
               SUM(CASE WHEN `status` = 'paid' THEN 1 ELSE 0 END) AS `collected`,
               SUM(`amount_due`) AS `outstanding`,
               AVG(`days_overdue`) AS `avg_days_overdue`
        FROM `epc_dunning_queue`
        GROUP BY `site_key`
        ORDER BY `outstanding` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
