<?php
/**
 * P1 #21 — Credit Limit Engine
 *
 * Per-customer credit management: limits, utilization, hold/release,
 * aging-based auto-hold, and credit review workflow.
 * Schema: epc_credit_limits, epc_credit_transactions
 */

if (!defined('EPC_CREDIT_LIMIT_VERSION')) {
    define('EPC_CREDIT_LIMIT_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_credit_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_credit_limits` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `customer_id`     INT UNSIGNED   NOT NULL,
            `credit_limit`    DECIMAL(15,2)  NOT NULL DEFAULT 0.00,
            `balance_used`    DECIMAL(15,2)  NOT NULL DEFAULT 0.00,
            `currency`        CHAR(3)        NOT NULL DEFAULT 'AED',
            `status`          ENUM('active','on_hold','suspended','review') NOT NULL DEFAULT 'active',
            `hold_reason`     VARCHAR(255)   NOT NULL DEFAULT '',
            `risk_score`      TINYINT UNSIGNED NOT NULL DEFAULT 50,
            `payment_terms`   VARCHAR(32)    NOT NULL DEFAULT 'net30',
            `last_review`     DATE           NULL,
            `next_review`     DATE           NULL,
            `approved_by`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `notes`           TEXT           NOT NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_tenant_customer` (`site_key`, `customer_id`),
            INDEX `idx_status` (`status`),
            INDEX `idx_risk` (`risk_score`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_credit_transactions` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `customer_id`     INT UNSIGNED   NOT NULL,
            `txn_type`        ENUM('invoice','payment','adjustment','write_off','credit_note') NOT NULL,
            `reference_type`  VARCHAR(32)    NOT NULL DEFAULT '',
            `reference_id`    VARCHAR(64)    NOT NULL DEFAULT '',
            `amount`          DECIMAL(15,2)  NOT NULL DEFAULT 0.00,
            `running_balance` DECIMAL(15,2)  NOT NULL DEFAULT 0.00,
            `description`     VARCHAR(255)   NOT NULL DEFAULT '',
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_customer` (`site_key`, `customer_id`),
            INDEX `idx_type` (`txn_type`),
            INDEX `idx_created` (`created_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── get or create credit record ─── */

function epc_credit_get(PDO $pdo, string $siteKey, int $customerId): array
{
    epc_credit_ensure_schema($pdo);

    $st = $pdo->prepare("SELECT * FROM `epc_credit_limits` WHERE `site_key` = ? AND `customer_id` = ?");
    $st->execute(array($siteKey, $customerId));
    $row = $st->fetch(PDO::FETCH_ASSOC);

    if (!$row) {
        return array(
            'customer_id'  => $customerId,
            'credit_limit' => 0.00,
            'balance_used' => 0.00,
            'available'    => 0.00,
            'utilization'  => 0,
            'status'       => 'active',
            'risk_score'   => 50,
            'payment_terms' => 'net30',
            'exists'       => false,
        );
    }

    $available = (float) $row['credit_limit'] - (float) $row['balance_used'];
    $utilization = $row['credit_limit'] > 0
        ? round(($row['balance_used'] / $row['credit_limit']) * 100, 1)
        : 0;

    $row['available'] = $available;
    $row['utilization'] = $utilization;
    $row['exists'] = true;
    return $row;
}

/* ─── set credit limit ─── */

function epc_credit_set_limit(PDO $pdo, string $siteKey, int $customerId, float $limit, array $opts = array()): array
{
    epc_credit_ensure_schema($pdo);

    $currency     = (string) ($opts['currency'] ?? 'AED');
    $paymentTerms = (string) ($opts['payment_terms'] ?? 'net30');
    $approvedBy   = (int) ($opts['approved_by'] ?? 0);
    $notes        = (string) ($opts['notes'] ?? '');
    $nextReview   = (string) ($opts['next_review'] ?? date('Y-m-d', strtotime('+90 days')));

    $st = $pdo->prepare("
        INSERT INTO `epc_credit_limits`
            (`site_key`, `customer_id`, `credit_limit`, `currency`, `payment_terms`, `approved_by`, `notes`, `last_review`, `next_review`)
        VALUES (?, ?, ?, ?, ?, ?, ?, CURDATE(), ?)
        ON DUPLICATE KEY UPDATE
            `credit_limit` = VALUES(`credit_limit`),
            `currency` = VALUES(`currency`),
            `payment_terms` = VALUES(`payment_terms`),
            `approved_by` = VALUES(`approved_by`),
            `notes` = VALUES(`notes`),
            `last_review` = CURDATE(),
            `next_review` = VALUES(`next_review`)
    ");
    $st->execute(array($siteKey, $customerId, $limit, $currency, $paymentTerms, $approvedBy, $notes, $nextReview));

    return epc_credit_get($pdo, $siteKey, $customerId);
}

/* ─── check if order can proceed ─── */

function epc_credit_check_order(PDO $pdo, string $siteKey, int $customerId, float $orderTotal): array
{
    $credit = epc_credit_get($pdo, $siteKey, $customerId);

    if (!$credit['exists']) {
        return array('allowed' => true, 'reason' => 'No credit limit configured — order allowed');
    }

    if ($credit['status'] === 'on_hold' || $credit['status'] === 'suspended') {
        return array(
            'allowed' => false,
            'reason'  => 'Account is ' . $credit['status'] . ': ' . ($credit['hold_reason'] ?: 'Contact finance'),
            'credit'  => $credit,
        );
    }

    if ($credit['credit_limit'] <= 0) {
        return array('allowed' => true, 'reason' => 'No limit set — order allowed', 'credit' => $credit);
    }

    $newBalance = (float) $credit['balance_used'] + $orderTotal;
    if ($newBalance > (float) $credit['credit_limit']) {
        return array(
            'allowed'   => false,
            'reason'    => sprintf(
                'Order %.2f exceeds available credit %.2f (limit %.2f, used %.2f)',
                $orderTotal, $credit['available'], $credit['credit_limit'], $credit['balance_used']
            ),
            'credit'    => $credit,
            'shortfall' => $newBalance - (float) $credit['credit_limit'],
        );
    }

    return array('allowed' => true, 'reason' => 'Within credit limit', 'credit' => $credit);
}

/* ─── record transaction ─── */

function epc_credit_record_txn(PDO $pdo, string $siteKey, int $customerId, string $type, float $amount, array $opts = array()): array
{
    epc_credit_ensure_schema($pdo);

    $validTypes = array('invoice', 'payment', 'adjustment', 'write_off', 'credit_note');
    if (!in_array($type, $validTypes, true)) {
        return array('ok' => false, 'error' => 'Invalid transaction type');
    }

    $pdo->beginTransaction();
    try {
        // Update balance
        $delta = in_array($type, array('payment', 'credit_note', 'write_off'), true) ? -$amount : $amount;

        $st = $pdo->prepare("
            UPDATE `epc_credit_limits`
            SET `balance_used` = GREATEST(0, `balance_used` + ?)
            WHERE `site_key` = ? AND `customer_id` = ?
        ");
        $st->execute(array($delta, $siteKey, $customerId));

        // Get running balance
        $credit = epc_credit_get($pdo, $siteKey, $customerId);

        // Record transaction
        $st = $pdo->prepare("
            INSERT INTO `epc_credit_transactions`
                (`site_key`, `customer_id`, `txn_type`, `reference_type`, `reference_id`, `amount`, `running_balance`, `description`, `created_by`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        ");
        $st->execute(array(
            $siteKey, $customerId, $type,
            (string) ($opts['reference_type'] ?? ''),
            (string) ($opts['reference_id'] ?? ''),
            $amount,
            (float) $credit['balance_used'],
            (string) ($opts['description'] ?? ''),
            (int) ($opts['created_by'] ?? 0),
        ));

        $pdo->commit();
        return array('ok' => true, 'credit' => $credit);
    } catch (\Exception $e) {
        $pdo->rollBack();
        return array('ok' => false, 'error' => $e->getMessage());
    }
}

/* ─── hold / release ─── */

function epc_credit_hold(PDO $pdo, string $siteKey, int $customerId, string $reason = ''): bool
{
    $st = $pdo->prepare("
        UPDATE `epc_credit_limits` SET `status` = 'on_hold', `hold_reason` = ?
        WHERE `site_key` = ? AND `customer_id` = ?
    ");
    return $st->execute(array($reason, $siteKey, $customerId));
}

function epc_credit_release(PDO $pdo, string $siteKey, int $customerId): bool
{
    $st = $pdo->prepare("
        UPDATE `epc_credit_limits` SET `status` = 'active', `hold_reason` = ''
        WHERE `site_key` = ? AND `customer_id` = ?
    ");
    return $st->execute(array($siteKey, $customerId));
}

/* ─── auto-hold based on aging ─── */

function epc_credit_aging_auto_hold(PDO $pdo, string $siteKey, int $overdueDays = 90): array
{
    epc_credit_ensure_schema($pdo);

    $held = array();

    $st = $pdo->prepare("
        SELECT cl.`customer_id`, cl.`balance_used`, cl.`credit_limit`
        FROM `epc_credit_limits` cl
        WHERE cl.`site_key` = ? AND cl.`status` = 'active' AND cl.`balance_used` > 0
    ");
    $st->execute(array($siteKey));
    $customers = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    foreach ($customers as $c) {
        $stOldest = $pdo->prepare("
            SELECT MIN(`created_at`) AS oldest
            FROM `epc_credit_transactions`
            WHERE `site_key` = ? AND `customer_id` = ? AND `txn_type` = 'invoice'
              AND `created_at` < DATE_SUB(CURDATE(), INTERVAL ? DAY)
        ");
        $stOldest->execute(array($siteKey, $c['customer_id'], $overdueDays));
        $oldest = $stOldest->fetchColumn();

        if ($oldest) {
            epc_credit_hold($pdo, $siteKey, (int) $c['customer_id'], 'Auto-hold: invoice overdue > ' . $overdueDays . ' days');
            $held[] = (int) $c['customer_id'];
        }
    }

    return array('ok' => true, 'held_count' => count($held), 'customer_ids' => $held);
}

/* ─── transaction history ─── */

function epc_credit_history(PDO $pdo, string $siteKey, int $customerId, int $limit = 50): array
{
    epc_credit_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT * FROM `epc_credit_transactions`
        WHERE `site_key` = ? AND `customer_id` = ?
        ORDER BY `created_at` DESC
        LIMIT ?
    ");
    $st->execute(array($siteKey, $customerId, $limit));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── fleet credit summary (BOS) ─── */

function epc_credit_fleet_summary(PDO $pdo): array
{
    epc_credit_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(*) AS `customers`,
               SUM(`credit_limit`) AS `total_limit`,
               SUM(`balance_used`) AS `total_used`,
               SUM(CASE WHEN `status` = 'on_hold' THEN 1 ELSE 0 END) AS `on_hold`,
               SUM(CASE WHEN `status` = 'suspended' THEN 1 ELSE 0 END) AS `suspended`,
               AVG(`risk_score`) AS `avg_risk`
        FROM `epc_credit_limits`
        GROUP BY `site_key`
        ORDER BY `total_used` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── customers due for review ─── */

function epc_credit_due_for_review(PDO $pdo, string $siteKey): array
{
    epc_credit_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT * FROM `epc_credit_limits`
        WHERE `site_key` = ? AND `next_review` <= CURDATE()
        ORDER BY `next_review` ASC
    ");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── risk score update ─── */

function epc_credit_update_risk(PDO $pdo, string $siteKey, int $customerId, int $score): bool
{
    $score = max(0, min(100, $score));
    $st = $pdo->prepare("UPDATE `epc_credit_limits` SET `risk_score` = ? WHERE `site_key` = ? AND `customer_id` = ?");
    return $st->execute(array($score, $siteKey, $customerId));
}
