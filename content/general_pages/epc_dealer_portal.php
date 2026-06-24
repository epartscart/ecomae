<?php
/**
 * P2 #29 — Dealer / Distributor Portal
 *
 * B2B dealer management: dealer registration, tiered pricing,
 * territory assignment, order quotas, and performance dashboards.
 * Schema: epc_dealers, epc_dealer_territories, epc_dealer_orders
 */

if (!defined('EPC_DEALER_PORTAL_VERSION')) {
    define('EPC_DEALER_PORTAL_VERSION', '1.0.0');
}

function epc_dealer_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_dealers` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `dealer_code`     VARCHAR(32)    NOT NULL,
            `company_name`    VARCHAR(128)   NOT NULL,
            `contact_name`    VARCHAR(128)   NOT NULL DEFAULT '',
            `email`           VARCHAR(128)   NOT NULL DEFAULT '',
            `phone`           VARCHAR(32)    NOT NULL DEFAULT '',
            `tier`            ENUM('bronze','silver','gold','platinum') NOT NULL DEFAULT 'bronze',
            `discount_pct`    DECIMAL(5,2)   NOT NULL DEFAULT 0.00,
            `credit_limit`    DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `territory`       VARCHAR(64)    NOT NULL DEFAULT '',
            `status`          ENUM('pending','active','suspended','terminated') NOT NULL DEFAULT 'pending',
            `ytd_revenue`     DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `order_count`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_dealer` (`site_key`, `dealer_code`),
            INDEX `idx_tier` (`tier`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_dealer_orders` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `dealer_id`       INT UNSIGNED   NOT NULL,
            `order_ref`       VARCHAR(64)    NOT NULL,
            `order_total`     DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `discount_applied`DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `net_total`       DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `status`          ENUM('draft','submitted','confirmed','shipped','delivered','cancelled') NOT NULL DEFAULT 'draft',
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_dealer` (`dealer_id`),
            INDEX `idx_site` (`site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_dealer_tier_discounts(): array
{
    return array(
        'bronze'   => array('discount' => 5,  'min_revenue' => 0,      'label' => 'Bronze'),
        'silver'   => array('discount' => 10, 'min_revenue' => 50000,  'label' => 'Silver'),
        'gold'     => array('discount' => 15, 'min_revenue' => 200000, 'label' => 'Gold'),
        'platinum' => array('discount' => 20, 'min_revenue' => 500000, 'label' => 'Platinum'),
    );
}

function epc_dealer_register(PDO $pdo, string $siteKey, array $data): array
{
    epc_dealer_ensure_schema($pdo);
    $code = strtoupper((string) ($data['dealer_code'] ?? 'DLR-' . str_pad((string)random_int(1,9999), 4, '0', STR_PAD_LEFT)));
    $tier = (string) ($data['tier'] ?? 'bronze');
    $tiers = epc_dealer_tier_discounts();
    $discount = $tiers[$tier]['discount'] ?? 5;

    $st = $pdo->prepare("
        INSERT INTO `epc_dealers` (`site_key`, `dealer_code`, `company_name`, `contact_name`, `email`, `phone`, `tier`, `discount_pct`, `credit_limit`, `territory`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array($siteKey, $code, (string)($data['company_name']??''), (string)($data['contact_name']??''), (string)($data['email']??''), (string)($data['phone']??''), $tier, $discount, (float)($data['credit_limit']??0), (string)($data['territory']??'')));
    return array('ok' => true, 'dealer_id' => (int) $pdo->lastInsertId(), 'dealer_code' => $code);
}

function epc_dealer_list(PDO $pdo, string $siteKey, array $filters = array()): array
{
    epc_dealer_ensure_schema($pdo);
    $where = array('`site_key` = ?'); $params = array($siteKey);
    if (!empty($filters['tier'])) { $where[] = '`tier` = ?'; $params[] = $filters['tier']; }
    if (!empty($filters['status'])) { $where[] = '`status` = ?'; $params[] = $filters['status']; }
    $st = $pdo->prepare("SELECT * FROM `epc_dealers` WHERE " . implode(' AND ', $where) . " ORDER BY `ytd_revenue` DESC");
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_dealer_place_order(PDO $pdo, string $siteKey, int $dealerId, float $orderTotal): array
{
    $st = $pdo->prepare("SELECT `discount_pct`, `status` FROM `epc_dealers` WHERE `id` = ? AND `site_key` = ?");
    $st->execute(array($dealerId, $siteKey));
    $dealer = $st->fetch(PDO::FETCH_ASSOC);
    if (!$dealer) return array('ok' => false, 'error' => 'Dealer not found');
    if ($dealer['status'] !== 'active') return array('ok' => false, 'error' => 'Dealer not active');

    $discount = round($orderTotal * (float) $dealer['discount_pct'] / 100, 2);
    $net = $orderTotal - $discount;
    $ref = 'DO-' . date('Ymd') . '-' . str_pad((string)random_int(1,9999), 4, '0', STR_PAD_LEFT);

    $pdo->prepare("INSERT INTO `epc_dealer_orders` (`site_key`, `dealer_id`, `order_ref`, `order_total`, `discount_applied`, `net_total`, `status`) VALUES (?, ?, ?, ?, ?, ?, 'submitted')")
        ->execute(array($siteKey, $dealerId, $ref, $orderTotal, $discount, $net));

    $pdo->prepare("UPDATE `epc_dealers` SET `ytd_revenue` = `ytd_revenue` + ?, `order_count` = `order_count` + 1 WHERE `id` = ?")
        ->execute(array($net, $dealerId));

    return array('ok' => true, 'order_ref' => $ref, 'discount' => $discount, 'net_total' => $net);
}

function epc_dealer_auto_tier(PDO $pdo, int $dealerId): array
{
    $st = $pdo->prepare("SELECT `ytd_revenue`, `tier` FROM `epc_dealers` WHERE `id` = ?");
    $st->execute(array($dealerId));
    $d = $st->fetch(PDO::FETCH_ASSOC);
    if (!$d) return array('ok' => false, 'error' => 'Dealer not found');

    $rev = (float) $d['ytd_revenue'];
    $tiers = epc_dealer_tier_discounts();
    $newTier = 'bronze';
    foreach (array_reverse(array_keys($tiers)) as $t) {
        if ($rev >= $tiers[$t]['min_revenue']) { $newTier = $t; break; }
    }

    if ($newTier !== $d['tier']) {
        $pdo->prepare("UPDATE `epc_dealers` SET `tier` = ?, `discount_pct` = ? WHERE `id` = ?")
            ->execute(array($newTier, $tiers[$newTier]['discount'], $dealerId));
    }

    return array('ok' => true, 'old_tier' => $d['tier'], 'new_tier' => $newTier, 'ytd_revenue' => $rev);
}

function epc_dealer_fleet_stats(PDO $pdo): array
{
    epc_dealer_ensure_schema($pdo);
    $st = $pdo->query("
        SELECT `site_key`, COUNT(*) AS `total_dealers`,
               SUM(CASE WHEN `status`='active' THEN 1 ELSE 0 END) AS `active`,
               SUM(`ytd_revenue`) AS `total_revenue`, SUM(`order_count`) AS `total_orders`
        FROM `epc_dealers` GROUP BY `site_key` ORDER BY `total_revenue` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── Dealer Management ─── */

function epc_dealer_get(PDO $pdo, int $dealerId): ?array
{
    $st = $pdo->prepare("SELECT * FROM `epc_dealers` WHERE `id`=?");
    $st->execute(array($dealerId));
    return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_dealer_update(PDO $pdo, int $dealerId, array $data): array
{
    $fields = array();
    $params = array();
    $allowed = array('company_name', 'contact_email', 'phone', 'tier', 'discount_pct', 'status', 'credit_limit', 'payment_terms_days');
    foreach ($allowed as $f) {
        if (array_key_exists($f, $data)) {
            $fields[] = "`{$f}` = ?";
            $params[] = $data[$f];
        }
    }
    if (empty($fields)) return array('ok' => false, 'error' => 'No fields to update');
    $params[] = $dealerId;
    $pdo->prepare("UPDATE `epc_dealers` SET " . implode(', ', $fields) . " WHERE `id`=?")->execute($params);
    return array('ok' => true);
}

function epc_dealer_orders(PDO $pdo, int $dealerId, int $limit = 50): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_dealer_orders` WHERE `dealer_id`=? ORDER BY `created_at` DESC LIMIT ?");
    $st->execute(array($dealerId, $limit));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_dealer_suspend(PDO $pdo, int $dealerId, string $reason = ''): array
{
    $pdo->prepare("UPDATE `epc_dealers` SET `status`='suspended' WHERE `id`=?")->execute(array($dealerId));
    return array('ok' => true, 'message' => 'Dealer suspended');
}

function epc_dealer_activate(PDO $pdo, int $dealerId): array
{
    $pdo->prepare("UPDATE `epc_dealers` SET `status`='active' WHERE `id`=?")->execute(array($dealerId));
    return array('ok' => true);
}

function epc_dealer_performance_report(PDO $pdo, string $siteKey): array
{
    epc_dealer_ensure_schema($pdo);
    $st = $pdo->prepare("
        SELECT d.`id`, d.`company_name`, d.`tier`, d.`ytd_revenue`, d.`order_count`, d.`discount_pct`,
               (SELECT MAX(o.`created_at`) FROM `epc_dealer_orders` o WHERE o.`dealer_id` = d.`id`) AS `last_order`
        FROM `epc_dealers` d WHERE d.`site_key`=? AND d.`status`='active'
        ORDER BY d.`ytd_revenue` DESC
    ");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
