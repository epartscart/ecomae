<?php
/**
 * P1 #25 — Notification Center
 *
 * In-app notifications with email digest and webhook alert integration.
 * Schema: epc_notifications, epc_notification_prefs
 * Channels: in-app (DB), email (digest), webhook (via epc_events)
 */

if (!defined('EPC_NOTIFICATIONS_VERSION')) {
    define('EPC_NOTIFICATIONS_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_notifications_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_notifications` (
            `id`          BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `tenant_key`  VARCHAR(64)  NOT NULL DEFAULT '__platform__',
            `user_id`     INT UNSIGNED NOT NULL DEFAULT 0,
            `channel`     ENUM('in_app','email','webhook') NOT NULL DEFAULT 'in_app',
            `category`    VARCHAR(64)  NOT NULL DEFAULT 'system',
            `severity`    ENUM('info','warning','error','success') NOT NULL DEFAULT 'info',
            `title`       VARCHAR(255) NOT NULL,
            `body`        TEXT         NOT NULL,
            `action_url`  VARCHAR(512) NOT NULL DEFAULT '',
            `action_label` VARCHAR(64) NOT NULL DEFAULT '',
            `is_read`     TINYINT(1)   NOT NULL DEFAULT 0,
            `read_at`     DATETIME     NULL,
            `dismissed`   TINYINT(1)   NOT NULL DEFAULT 0,
            `metadata`    JSON         NULL,
            `created_at`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_tenant_user` (`tenant_key`, `user_id`, `is_read`),
            INDEX `idx_category` (`category`),
            INDEX `idx_created` (`created_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_notification_prefs` (
            `id`          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `tenant_key`  VARCHAR(64)  NOT NULL DEFAULT '__platform__',
            `user_id`     INT UNSIGNED NOT NULL DEFAULT 0,
            `category`    VARCHAR(64)  NOT NULL DEFAULT '*',
            `channel_in_app` TINYINT(1) NOT NULL DEFAULT 1,
            `channel_email`  TINYINT(1) NOT NULL DEFAULT 1,
            `channel_webhook` TINYINT(1) NOT NULL DEFAULT 0,
            `email_digest`   ENUM('instant','hourly','daily','off') NOT NULL DEFAULT 'daily',
            `updated_at`  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_pref` (`tenant_key`, `user_id`, `category`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── notification categories ─── */

function epc_notification_categories(): array
{
    return array(
        'system'       => array('label' => 'System Alerts',        'icon' => 'fa-cog',           'color' => '#6b7280'),
        'security'     => array('label' => 'Security',             'icon' => 'fa-shield',        'color' => '#ef4444'),
        'order'        => array('label' => 'Orders',               'icon' => 'fa-shopping-cart',  'color' => '#3b82f6'),
        'invoice'      => array('label' => 'Invoicing',            'icon' => 'fa-file-text-o',   'color' => '#10b981'),
        'inventory'    => array('label' => 'Inventory',            'icon' => 'fa-cubes',         'color' => '#f59e0b'),
        'erp'          => array('label' => 'ERP / Finance',        'icon' => 'fa-calculator',    'color' => '#8b5cf6'),
        'compliance'   => array('label' => 'Compliance',           'icon' => 'fa-balance-scale', 'color' => '#ec4899'),
        'tenant'       => array('label' => 'Tenant Updates',       'icon' => 'fa-building',      'color' => '#06b6d4'),
        'webhook'      => array('label' => 'Webhook Alerts',       'icon' => 'fa-plug',          'color' => '#84cc16'),
        'deploy'       => array('label' => 'Deploy / Maintenance', 'icon' => 'fa-rocket',        'color' => '#f97316'),
    );
}

/* ─── send notification ─── */

function epc_notification_send(PDO $pdo, array $data): int
{
    $tenantKey  = (string) ($data['tenant_key'] ?? '__platform__');
    $userId     = (int) ($data['user_id'] ?? 0);
    $category   = (string) ($data['category'] ?? 'system');
    $severity   = (string) ($data['severity'] ?? 'info');
    $title      = (string) ($data['title'] ?? '');
    $body       = (string) ($data['body'] ?? '');
    $actionUrl  = (string) ($data['action_url'] ?? '');
    $actionLabel = (string) ($data['action_label'] ?? '');
    $metadata   = isset($data['metadata']) ? json_encode($data['metadata']) : null;

    if ($title === '') {
        return 0;
    }

    $validSeverities = array('info', 'warning', 'error', 'success');
    if (!in_array($severity, $validSeverities, true)) {
        $severity = 'info';
    }

    epc_notifications_ensure_schema($pdo);

    $st = $pdo->prepare("
        INSERT INTO `epc_notifications`
            (`tenant_key`, `user_id`, `channel`, `category`, `severity`, `title`, `body`, `action_url`, `action_label`, `metadata`)
        VALUES (?, ?, 'in_app', ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array($tenantKey, $userId, $category, $severity, $title, $body, $actionUrl, $actionLabel, $metadata));
    $notifId = (int) $pdo->lastInsertId();

    // Fire event for webhook/email channels
    $eventsFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_events.php';
    if ($notifId > 0 && is_file($eventsFile)) {
        require_once $eventsFile;
        if (function_exists('epc_event_emit')) {
            epc_event_emit($pdo, array(
                'event_type' => 'notification.created',
                'tenant_key' => $tenantKey,
                'payload'    => array(
                    'notification_id' => $notifId,
                    'category'        => $category,
                    'severity'        => $severity,
                    'title'           => $title,
                    'user_id'         => $userId,
                ),
            ));
        }
    }

    return $notifId;
}

/* ─── broadcast to all users of a tenant ─── */

function epc_notification_broadcast(PDO $pdo, string $tenantKey, array $data): int
{
    $data['tenant_key'] = $tenantKey;
    $data['user_id'] = 0; // user_id=0 means "all users"
    return epc_notification_send($pdo, $data);
}

/* ─── list notifications ─── */

function epc_notifications_list(PDO $pdo, string $tenantKey, int $userId = 0, array $filters = array(), int $limit = 50, int $offset = 0): array
{
    epc_notifications_ensure_schema($pdo);

    $where = array('(`tenant_key` = ? AND (`user_id` = ? OR `user_id` = 0))');
    $params = array($tenantKey, $userId);

    if (!empty($filters['category'])) {
        $where[] = '`category` = ?';
        $params[] = (string) $filters['category'];
    }
    if (isset($filters['is_read'])) {
        $where[] = '`is_read` = ?';
        $params[] = (int) $filters['is_read'];
    }
    if (!empty($filters['severity'])) {
        $where[] = '`severity` = ?';
        $params[] = (string) $filters['severity'];
    }
    if (isset($filters['dismissed']) && $filters['dismissed'] === false) {
        $where[] = '`dismissed` = 0';
    }

    $sql = 'SELECT * FROM `epc_notifications` WHERE ' . implode(' AND ', $where)
         . ' ORDER BY `created_at` DESC LIMIT ' . (int) $limit . ' OFFSET ' . (int) $offset;

    $st = $pdo->prepare($sql);
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── unread count ─── */

function epc_notifications_unread_count(PDO $pdo, string $tenantKey, int $userId = 0): int
{
    epc_notifications_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT COUNT(*) FROM `epc_notifications`
        WHERE `tenant_key` = ? AND (`user_id` = ? OR `user_id` = 0) AND `is_read` = 0 AND `dismissed` = 0
    ");
    $st->execute(array($tenantKey, $userId));
    return (int) $st->fetchColumn();
}

/* ─── mark read ─── */

function epc_notifications_mark_read(PDO $pdo, array $ids, string $tenantKey): int
{
    if (empty($ids)) {
        return 0;
    }

    $placeholders = implode(',', array_fill(0, count($ids), '?'));
    $params = array_merge($ids, array($tenantKey));
    $st = $pdo->prepare("
        UPDATE `epc_notifications` SET `is_read` = 1, `read_at` = NOW()
        WHERE `id` IN ({$placeholders}) AND `tenant_key` = ?
    ");
    $st->execute($params);
    return $st->rowCount();
}

/* ─── mark all read ─── */

function epc_notifications_mark_all_read(PDO $pdo, string $tenantKey, int $userId = 0): int
{
    $st = $pdo->prepare("
        UPDATE `epc_notifications` SET `is_read` = 1, `read_at` = NOW()
        WHERE `tenant_key` = ? AND (`user_id` = ? OR `user_id` = 0) AND `is_read` = 0
    ");
    $st->execute(array($tenantKey, $userId));
    return $st->rowCount();
}

/* ─── dismiss ─── */

function epc_notifications_dismiss(PDO $pdo, int $id, string $tenantKey): bool
{
    $st = $pdo->prepare("
        UPDATE `epc_notifications` SET `dismissed` = 1 WHERE `id` = ? AND `tenant_key` = ?
    ");
    $st->execute(array($id, $tenantKey));
    return $st->rowCount() > 0;
}

/* ─── summary by category ─── */

function epc_notifications_summary(PDO $pdo, string $tenantKey, int $userId = 0): array
{
    epc_notifications_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT `category`,
               COUNT(*) AS `total`,
               SUM(CASE WHEN `is_read` = 0 THEN 1 ELSE 0 END) AS `unread`,
               MAX(`created_at`) AS `latest`
        FROM `epc_notifications`
        WHERE `tenant_key` = ? AND (`user_id` = ? OR `user_id` = 0) AND `dismissed` = 0
        GROUP BY `category`
        ORDER BY `unread` DESC, `latest` DESC
    ");
    $st->execute(array($tenantKey, $userId));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── preferences ─── */

function epc_notification_prefs_get(PDO $pdo, string $tenantKey, int $userId): array
{
    epc_notifications_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT * FROM `epc_notification_prefs`
        WHERE `tenant_key` = ? AND `user_id` = ?
    ");
    $st->execute(array($tenantKey, $userId));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $prefs = array();
    foreach ($rows as $row) {
        $prefs[$row['category']] = array(
            'in_app'       => (bool) $row['channel_in_app'],
            'email'        => (bool) $row['channel_email'],
            'webhook'      => (bool) $row['channel_webhook'],
            'email_digest' => $row['email_digest'],
        );
    }
    return $prefs;
}

function epc_notification_prefs_save(PDO $pdo, string $tenantKey, int $userId, string $category, array $channels): bool
{
    epc_notifications_ensure_schema($pdo);

    $st = $pdo->prepare("
        INSERT INTO `epc_notification_prefs`
            (`tenant_key`, `user_id`, `category`, `channel_in_app`, `channel_email`, `channel_webhook`, `email_digest`)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE
            `channel_in_app` = VALUES(`channel_in_app`),
            `channel_email` = VALUES(`channel_email`),
            `channel_webhook` = VALUES(`channel_webhook`),
            `email_digest` = VALUES(`email_digest`)
    ");
    return $st->execute(array(
        $tenantKey,
        $userId,
        $category,
        (int) ($channels['in_app'] ?? 1),
        (int) ($channels['email'] ?? 1),
        (int) ($channels['webhook'] ?? 0),
        (string) ($channels['email_digest'] ?? 'daily'),
    ));
}

/* ─── fleet notification stats (BOS) ─── */

function epc_notifications_fleet_stats(PDO $pdo): array
{
    epc_notifications_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `tenant_key`,
               COUNT(*) AS `total`,
               SUM(CASE WHEN `is_read` = 0 THEN 1 ELSE 0 END) AS `unread`,
               SUM(CASE WHEN `severity` = 'error' AND `is_read` = 0 THEN 1 ELSE 0 END) AS `errors`,
               MAX(`created_at`) AS `latest`
        FROM `epc_notifications`
        WHERE `dismissed` = 0
        GROUP BY `tenant_key`
        ORDER BY `errors` DESC, `unread` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── cleanup old notifications ─── */

function epc_notifications_cleanup(PDO $pdo, int $daysOld = 90): int
{
    $cutoff = date('Y-m-d H:i:s', time() - ($daysOld * 86400));
    $st = $pdo->prepare("DELETE FROM `epc_notifications` WHERE `created_at` < ? AND `is_read` = 1");
    $st->execute(array($cutoff));
    return $st->rowCount();
}

/* ─── email digest builder ─── */

function epc_notifications_pending_digest(PDO $pdo, string $digestType = 'daily'): array
{
    epc_notifications_ensure_schema($pdo);

    $since = ($digestType === 'hourly')
        ? date('Y-m-d H:i:s', time() - 3600)
        : date('Y-m-d H:i:s', time() - 86400);

    $st = $pdo->prepare("
        SELECT n.`tenant_key`, n.`user_id`, n.`category`, n.`severity`, n.`title`, n.`body`, n.`created_at`
        FROM `epc_notifications` n
        INNER JOIN `epc_notification_prefs` p
            ON p.`tenant_key` = n.`tenant_key` AND p.`user_id` = n.`user_id`
            AND (p.`category` = n.`category` OR p.`category` = '*')
        WHERE n.`is_read` = 0
          AND n.`created_at` >= ?
          AND p.`channel_email` = 1
          AND p.`email_digest` = ?
        ORDER BY n.`tenant_key`, n.`user_id`, n.`created_at` DESC
    ");
    $st->execute(array($since, $digestType));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $grouped = array();
    foreach ($rows as $row) {
        $key = $row['tenant_key'] . '::' . $row['user_id'];
        if (!isset($grouped[$key])) {
            $grouped[$key] = array(
                'tenant_key' => $row['tenant_key'],
                'user_id'    => (int) $row['user_id'],
                'items'      => array(),
            );
        }
        $grouped[$key]['items'][] = $row;
    }
    return array_values($grouped);
}
