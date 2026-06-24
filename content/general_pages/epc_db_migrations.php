<?php
/**
 * P1 #24 — DB Migration Versioning Tool
 *
 * Track and apply schema migrations across all tenants.
 * Schema: epc_migrations (auto-created per database)
 * Pattern: sequential versioned migrations with up/down, rollback support.
 */

if (!defined('EPC_MIGRATIONS_VERSION')) {
    define('EPC_MIGRATIONS_VERSION', '1.0.0');
}

/* ─── migration registry ─── */

function epc_migrations_registry(): array
{
    return array(
        array(
            'version'     => '001',
            'name'        => 'create_epc_settings',
            'description' => 'Core platform settings table',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_settings` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `setting_key` VARCHAR(128) NOT NULL,
                `setting_value` TEXT NOT NULL,
                `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY `uk_key` (`setting_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_settings`",
        ),
        array(
            'version'     => '002',
            'name'        => 'create_epc_events',
            'description' => 'Platform event bus table',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_events` (
                `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `event_type` VARCHAR(128) NOT NULL,
                `tenant_key` VARCHAR(64) NOT NULL DEFAULT '__platform__',
                `payload` JSON NULL,
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_type` (`event_type`),
                INDEX `idx_tenant` (`tenant_key`),
                INDEX `idx_created` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_events`",
        ),
        array(
            'version'     => '003',
            'name'        => 'create_epc_webhooks',
            'description' => 'Webhook endpoints with HMAC signing',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_webhooks` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `tenant_key` VARCHAR(64) NOT NULL,
                `url` VARCHAR(512) NOT NULL,
                `secret` VARCHAR(128) NOT NULL,
                `events` JSON NOT NULL,
                `active` TINYINT(1) NOT NULL DEFAULT 1,
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_tenant` (`tenant_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_webhooks`",
        ),
        array(
            'version'     => '004',
            'name'        => 'create_epc_webhook_deliveries',
            'description' => 'Webhook delivery log with retry tracking',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_webhook_deliveries` (
                `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `webhook_id` INT UNSIGNED NOT NULL,
                `event_id` BIGINT UNSIGNED NOT NULL,
                `status` ENUM('pending','delivered','failed','dead') NOT NULL DEFAULT 'pending',
                `attempts` TINYINT UNSIGNED NOT NULL DEFAULT 0,
                `last_attempt` DATETIME NULL,
                `response_code` SMALLINT NULL,
                `response_body` TEXT NULL,
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_webhook` (`webhook_id`),
                INDEX `idx_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_webhook_deliveries`",
        ),
        array(
            'version'     => '005',
            'name'        => 'create_epc_notifications',
            'description' => 'In-app notification center',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_notifications` (
                `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `tenant_key` VARCHAR(64) NOT NULL DEFAULT '__platform__',
                `user_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `channel` ENUM('in_app','email','webhook') NOT NULL DEFAULT 'in_app',
                `category` VARCHAR(64) NOT NULL DEFAULT 'system',
                `severity` ENUM('info','warning','error','success') NOT NULL DEFAULT 'info',
                `title` VARCHAR(255) NOT NULL,
                `body` TEXT NOT NULL,
                `action_url` VARCHAR(512) NOT NULL DEFAULT '',
                `action_label` VARCHAR(64) NOT NULL DEFAULT '',
                `is_read` TINYINT(1) NOT NULL DEFAULT 0,
                `read_at` DATETIME NULL,
                `dismissed` TINYINT(1) NOT NULL DEFAULT 0,
                `metadata` JSON NULL,
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_tenant_user` (`tenant_key`, `user_id`, `is_read`),
                INDEX `idx_category` (`category`),
                INDEX `idx_created` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_notifications`",
        ),
        array(
            'version'     => '006',
            'name'        => 'create_epc_notification_prefs',
            'description' => 'Per-user notification channel preferences',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_notification_prefs` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `tenant_key` VARCHAR(64) NOT NULL DEFAULT '__platform__',
                `user_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `category` VARCHAR(64) NOT NULL DEFAULT '*',
                `channel_in_app` TINYINT(1) NOT NULL DEFAULT 1,
                `channel_email` TINYINT(1) NOT NULL DEFAULT 1,
                `channel_webhook` TINYINT(1) NOT NULL DEFAULT 0,
                `email_digest` ENUM('instant','hourly','daily','off') NOT NULL DEFAULT 'daily',
                `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY `uk_pref` (`tenant_key`, `user_id`, `category`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_notification_prefs`",
        ),
        array(
            'version'     => '007',
            'name'        => 'create_epc_mfa_tokens',
            'description' => 'MFA TOTP enrollment tokens',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_mfa_tokens` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `user_id` INT UNSIGNED NOT NULL,
                `secret` VARCHAR(64) NOT NULL,
                `backup_codes` JSON NULL,
                `verified` TINYINT(1) NOT NULL DEFAULT 0,
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY `uk_user` (`user_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_mfa_tokens`",
        ),
        array(
            'version'     => '008',
            'name'        => 'create_epc_audit_log',
            'description' => 'Security audit log for admin actions',
            'up'          => "CREATE TABLE IF NOT EXISTS `epc_audit_log` (
                `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `tenant_key` VARCHAR(64) NOT NULL DEFAULT '__platform__',
                `user_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `action` VARCHAR(128) NOT NULL,
                `entity_type` VARCHAR(64) NOT NULL DEFAULT '',
                `entity_id` VARCHAR(128) NOT NULL DEFAULT '',
                `details` JSON NULL,
                `ip_address` VARCHAR(45) NOT NULL DEFAULT '',
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_tenant` (`tenant_key`),
                INDEX `idx_user` (`user_id`),
                INDEX `idx_action` (`action`),
                INDEX `idx_created` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            'down'        => "DROP TABLE IF EXISTS `epc_audit_log`",
        ),
    );
}

/* ─── ensure migrations table ─── */

function epc_migrations_ensure_table(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_migrations` (
            `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `version` VARCHAR(10) NOT NULL,
            `name` VARCHAR(128) NOT NULL,
            `applied_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `checksum` VARCHAR(64) NOT NULL DEFAULT '',
            UNIQUE KEY `uk_version` (`version`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── get applied migrations ─── */

function epc_migrations_applied(PDO $pdo): array
{
    epc_migrations_ensure_table($pdo);
    $st = $pdo->query("SELECT `version`, `name`, `applied_at`, `checksum` FROM `epc_migrations` ORDER BY `version`");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── get pending migrations ─── */

function epc_migrations_pending(PDO $pdo): array
{
    $applied = array_column(epc_migrations_applied($pdo), 'version');
    $registry = epc_migrations_registry();
    $pending = array();
    foreach ($registry as $m) {
        if (!in_array($m['version'], $applied, true)) {
            $pending[] = $m;
        }
    }
    return $pending;
}

/* ─── apply single migration ─── */

function epc_migration_apply(PDO $pdo, array $migration): array
{
    epc_migrations_ensure_table($pdo);

    try {
        $pdo->beginTransaction();
        $pdo->exec($migration['up']);
        $checksum = md5($migration['up']);
        $st = $pdo->prepare("INSERT INTO `epc_migrations` (`version`, `name`, `checksum`) VALUES (?, ?, ?)");
        $st->execute(array($migration['version'], $migration['name'], $checksum));
        $pdo->commit();
        return array('ok' => true, 'version' => $migration['version'], 'name' => $migration['name']);
    } catch (\Exception $e) {
        if ($pdo->inTransaction()) {
            $pdo->rollBack();
        }
        return array('ok' => false, 'version' => $migration['version'], 'error' => $e->getMessage());
    }
}

/* ─── rollback single migration ─── */

function epc_migration_rollback(PDO $pdo, string $version): array
{
    $registry = epc_migrations_registry();
    $migration = null;
    foreach ($registry as $m) {
        if ($m['version'] === $version) {
            $migration = $m;
            break;
        }
    }
    if (!$migration) {
        return array('ok' => false, 'error' => 'Migration not found: ' . $version);
    }

    try {
        $pdo->beginTransaction();
        $pdo->exec($migration['down']);
        $st = $pdo->prepare("DELETE FROM `epc_migrations` WHERE `version` = ?");
        $st->execute(array($version));
        $pdo->commit();
        return array('ok' => true, 'version' => $version, 'rolled_back' => $migration['name']);
    } catch (\Exception $e) {
        if ($pdo->inTransaction()) {
            $pdo->rollBack();
        }
        return array('ok' => false, 'version' => $version, 'error' => $e->getMessage());
    }
}

/* ─── run all pending migrations ─── */

function epc_migrations_run_all(PDO $pdo): array
{
    $pending = epc_migrations_pending($pdo);
    if (empty($pending)) {
        return array('ok' => true, 'applied' => 0, 'message' => 'No pending migrations');
    }

    $results = array();
    $applied = 0;
    foreach ($pending as $m) {
        $result = epc_migration_apply($pdo, $m);
        $results[] = $result;
        if ($result['ok']) {
            $applied++;
        } else {
            break;
        }
    }

    return array(
        'ok'      => ($applied === count($pending)),
        'applied' => $applied,
        'total'   => count($pending),
        'results' => $results,
    );
}

/* ─── migration status ─── */

function epc_migrations_status(PDO $pdo): array
{
    $applied = epc_migrations_applied($pdo);
    $pending = epc_migrations_pending($pdo);
    $registry = epc_migrations_registry();

    return array(
        'total'        => count($registry),
        'applied'      => count($applied),
        'pending'      => count($pending),
        'current_version' => !empty($applied) ? end($applied)['version'] : '000',
        'latest_version'  => !empty($registry) ? end($registry)['version'] : '000',
        'applied_list' => $applied,
        'pending_list' => array_map(function ($m) {
            return array('version' => $m['version'], 'name' => $m['name'], 'description' => $m['description']);
        }, $pending),
    );
}

/* ─── verify integrity ─── */

function epc_migrations_verify(PDO $pdo): array
{
    $applied = epc_migrations_applied($pdo);
    $registry = epc_migrations_registry();
    $issues = array();

    foreach ($applied as $a) {
        $found = false;
        foreach ($registry as $m) {
            if ($m['version'] === $a['version']) {
                $found = true;
                $expectedChecksum = md5($m['up']);
                if ($a['checksum'] !== '' && $a['checksum'] !== $expectedChecksum) {
                    $issues[] = array(
                        'version' => $a['version'],
                        'issue'   => 'checksum_mismatch',
                        'detail'  => 'Migration SQL has changed since it was applied',
                    );
                }
                break;
            }
        }
        if (!$found) {
            $issues[] = array(
                'version' => $a['version'],
                'issue'   => 'orphaned',
                'detail'  => 'Applied migration not found in registry',
            );
        }
    }

    return array(
        'ok'     => empty($issues),
        'issues' => $issues,
    );
}

/* ─── dry-run (show SQL without executing) ─── */

function epc_migrations_dry_run(PDO $pdo): array
{
    $pending = epc_migrations_pending($pdo);
    $statements = array();
    foreach ($pending as $m) {
        $statements[] = array(
            'version'     => $m['version'],
            'name'        => $m['name'],
            'description' => $m['description'],
            'sql'         => $m['up'],
        );
    }
    return array('ok' => true, 'count' => count($statements), 'statements' => $statements);
}
