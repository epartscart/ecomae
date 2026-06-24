<?php
/**
 * P2 #38 — Subscription Billing Engine
 *
 * Recurring billing: plans, subscriptions, invoicing cycles,
 * proration, trial periods, usage-based metering, dunning on failure.
 * Schema: epc_billing_plans, epc_subscriptions, epc_billing_invoices
 */

if (!defined('EPC_SUBSCRIPTION_BILLING_VERSION')) {
    define('EPC_SUBSCRIPTION_BILLING_VERSION', '1.0.0');
}

function epc_billing_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_billing_plans` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `plan_code`       VARCHAR(32)    NOT NULL UNIQUE,
            `name`            VARCHAR(128)   NOT NULL,
            `description`     TEXT           NOT NULL DEFAULT '',
            `billing_cycle`   ENUM('monthly','quarterly','semi_annual','annual') NOT NULL DEFAULT 'monthly',
            `base_price`      DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `currency`        CHAR(3)        NOT NULL DEFAULT 'AED',
            `trial_days`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `setup_fee`       DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `features`        JSON           NULL,
            `usage_limits`    JSON           NULL,
            `active`          TINYINT(1)     NOT NULL DEFAULT 1,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_subscriptions` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `plan_id`         INT UNSIGNED   NOT NULL,
            `status`          ENUM('trial','active','past_due','paused','cancelled','expired') NOT NULL DEFAULT 'trial',
            `current_period_start` DATE      NOT NULL,
            `current_period_end`   DATE      NOT NULL,
            `trial_end`       DATE           NULL,
            `cancel_at`       DATE           NULL,
            `cancelled_reason` VARCHAR(256)  NOT NULL DEFAULT '',
            `usage_count`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `mrr`             DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_status` (`status`),
            INDEX `idx_period_end` (`current_period_end`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_billing_invoices` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `subscription_id` INT UNSIGNED   NOT NULL,
            `site_key`        VARCHAR(64)    NOT NULL,
            `invoice_number`  VARCHAR(32)    NOT NULL,
            `period_start`    DATE           NOT NULL,
            `period_end`      DATE           NOT NULL,
            `subtotal`        DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `tax`             DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `total`           DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `status`          ENUM('draft','sent','paid','overdue','void') NOT NULL DEFAULT 'draft',
            `due_date`        DATE           NOT NULL,
            `paid_at`         DATETIME       NULL,
            `payment_method`  VARCHAR(32)    NOT NULL DEFAULT '',
            `line_items`      JSON           NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_sub` (`subscription_id`),
            INDEX `idx_site` (`site_key`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_billing_create_plan(PDO $pdo, array $data): array
{
    epc_billing_ensure_schema($pdo);
    $st = $pdo->prepare("INSERT INTO `epc_billing_plans` (`plan_code`,`name`,`description`,`billing_cycle`,`base_price`,`currency`,`trial_days`,`setup_fee`,`features`,`usage_limits`) VALUES (?,?,?,?,?,?,?,?,?,?)");
    $st->execute(array(
        strtoupper((string)($data['plan_code']??'')), (string)($data['name']??''), (string)($data['description']??''),
        (string)($data['billing_cycle']??'monthly'), (float)($data['base_price']??0), (string)($data['currency']??'AED'),
        (int)($data['trial_days']??0), (float)($data['setup_fee']??0),
        json_encode($data['features']??array()), json_encode($data['usage_limits']??array()),
    ));
    return array('ok' => true, 'plan_id' => (int)$pdo->lastInsertId());
}

function epc_billing_list_plans(PDO $pdo): array
{
    epc_billing_ensure_schema($pdo);
    $st = $pdo->query("SELECT * FROM `epc_billing_plans` WHERE `active`=1 ORDER BY `base_price`");
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) {
        $r['features'] = json_decode($r['features']?:'[]', true);
        $r['usage_limits'] = json_decode($r['usage_limits']?:'{}', true);
    }
    return $rows;
}

function epc_billing_subscribe(PDO $pdo, string $siteKey, int $planId): array
{
    epc_billing_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT * FROM `epc_billing_plans` WHERE `id`=? AND `active`=1");
    $st->execute(array($planId));
    $plan = $st->fetch(PDO::FETCH_ASSOC);
    if (!$plan) return array('ok' => false, 'error' => 'Plan not found');

    $start = date('Y-m-d');
    $cycleDays = array('monthly' => 30, 'quarterly' => 90, 'semi_annual' => 180, 'annual' => 365);
    $days = $cycleDays[$plan['billing_cycle']] ?? 30;
    $end = date('Y-m-d', strtotime("+{$days} days"));
    $trialEnd = $plan['trial_days'] > 0 ? date('Y-m-d', strtotime("+{$plan['trial_days']} days")) : null;
    $status = $plan['trial_days'] > 0 ? 'trial' : 'active';

    $mrr = (float)$plan['base_price'];
    if ($plan['billing_cycle'] === 'quarterly') $mrr = round($mrr / 3, 2);
    elseif ($plan['billing_cycle'] === 'semi_annual') $mrr = round($mrr / 6, 2);
    elseif ($plan['billing_cycle'] === 'annual') $mrr = round($mrr / 12, 2);

    $pdo->prepare("INSERT INTO `epc_subscriptions` (`site_key`,`plan_id`,`status`,`current_period_start`,`current_period_end`,`trial_end`,`mrr`) VALUES (?,?,?,?,?,?,?)")
        ->execute(array($siteKey, $planId, $status, $start, $end, $trialEnd, $mrr));
    $subId = (int)$pdo->lastInsertId();

    $invNum = 'INV-' . strtoupper($siteKey) . '-' . date('Ymd') . '-' . str_pad((string)$subId, 4, '0', STR_PAD_LEFT);
    $tax = round((float)$plan['base_price'] * 0.05, 2);
    $total = (float)$plan['base_price'] + $tax + (float)$plan['setup_fee'];
    $pdo->prepare("INSERT INTO `epc_billing_invoices` (`subscription_id`,`site_key`,`invoice_number`,`period_start`,`period_end`,`subtotal`,`tax`,`total`,`status`,`due_date`,`line_items`) VALUES (?,?,?,?,?,?,?,?,?,?,?)")
        ->execute(array($subId, $siteKey, $invNum, $start, $end, (float)$plan['base_price'] + (float)$plan['setup_fee'], $tax, $total, 'sent', date('Y-m-d', strtotime('+15 days')),
            json_encode(array(array('description' => $plan['name'] . ' subscription', 'amount' => $plan['base_price']), array('description' => 'Setup fee', 'amount' => $plan['setup_fee'])))));

    return array('ok' => true, 'subscription_id' => $subId, 'invoice_number' => $invNum, 'total' => $total, 'status' => $status);
}

function epc_billing_tenant_subscription(PDO $pdo, string $siteKey): array
{
    $st = $pdo->prepare("SELECT s.*, p.`name` AS `plan_name`, p.`billing_cycle`, p.`base_price` FROM `epc_subscriptions` s JOIN `epc_billing_plans` p ON s.`plan_id`=p.`id` WHERE s.`site_key`=? ORDER BY s.`created_at` DESC LIMIT 1");
    $st->execute(array($siteKey));
    return $st->fetch(PDO::FETCH_ASSOC) ?: array();
}

function epc_billing_invoices(PDO $pdo, string $siteKey): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_billing_invoices` WHERE `site_key`=? ORDER BY `created_at` DESC");
    $st->execute(array($siteKey));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) { $r['line_items'] = json_decode($r['line_items']?:'[]', true); }
    return $rows;
}

function epc_billing_record_payment(PDO $pdo, int $invoiceId, string $method = 'card'): array
{
    $pdo->prepare("UPDATE `epc_billing_invoices` SET `status`='paid', `paid_at`=NOW(), `payment_method`=? WHERE `id`=? AND `status` IN ('sent','overdue')")
        ->execute(array($method, $invoiceId));
    return array('ok' => true);
}

function epc_billing_cancel(PDO $pdo, int $subId, string $reason = ''): array
{
    $pdo->prepare("UPDATE `epc_subscriptions` SET `status`='cancelled', `cancel_at`=CURDATE(), `cancelled_reason`=? WHERE `id`=?")->execute(array($reason, $subId));
    return array('ok' => true);
}

function epc_billing_fleet_stats(PDO $pdo): array
{
    epc_billing_ensure_schema($pdo);
    $st = $pdo->query("SELECT COUNT(*) AS `total_subs`, SUM(CASE WHEN `status`='active' THEN 1 ELSE 0 END) AS `active`, SUM(CASE WHEN `status`='trial' THEN 1 ELSE 0 END) AS `trial`, SUM(CASE WHEN `status`='cancelled' THEN 1 ELSE 0 END) AS `cancelled`, SUM(`mrr`) AS `total_mrr` FROM `epc_subscriptions`");
    return $st->fetch(PDO::FETCH_ASSOC) ?: array();
}
