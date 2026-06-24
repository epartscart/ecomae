<?php
/**
 * P2 #34 — Promotions / Pricing Rules Engine
 *
 * Rule-based promotions: percentage/fixed/BOGO/free-shipping/bundle,
 * coupon codes, date-range validity, min-order, customer segments,
 * stackable rules, priority, and usage limits.
 * Schema: epc_promotions, epc_promotion_usage
 */

if (!defined('EPC_PROMOTIONS_VERSION')) {
    define('EPC_PROMOTIONS_VERSION', '1.0.0');
}

function epc_promo_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_promotions` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `name`            VARCHAR(128)   NOT NULL,
            `code`            VARCHAR(32)    NOT NULL DEFAULT '',
            `type`            ENUM('percentage','fixed','bogo','free_shipping','bundle','tiered') NOT NULL DEFAULT 'percentage',
            `value`           DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `min_order`       DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `max_discount`    DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `start_date`      DATETIME       NOT NULL,
            `end_date`        DATETIME       NOT NULL,
            `usage_limit`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `per_customer`    INT UNSIGNED   NOT NULL DEFAULT 0,
            `used_count`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `stackable`       TINYINT(1)     NOT NULL DEFAULT 0,
            `priority`        INT            NOT NULL DEFAULT 0,
            `conditions`      JSON           NULL,
            `applies_to`      JSON           NULL,
            `customer_segments` JSON         NULL,
            `active`          TINYINT(1)     NOT NULL DEFAULT 1,
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_code` (`code`),
            INDEX `idx_dates` (`start_date`, `end_date`),
            INDEX `idx_active` (`active`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_promotion_usage` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `promotion_id`    INT UNSIGNED   NOT NULL,
            `site_key`        VARCHAR(64)    NOT NULL,
            `customer_id`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `order_ref`       VARCHAR(64)    NOT NULL DEFAULT '',
            `discount_amount` DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `used_at`         DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_promo` (`promotion_id`),
            INDEX `idx_customer` (`customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_promo_create(PDO $pdo, string $siteKey, array $data): array
{
    epc_promo_ensure_schema($pdo);
    $st = $pdo->prepare("
        INSERT INTO `epc_promotions` (`site_key`,`name`,`code`,`type`,`value`,`min_order`,`max_discount`,`start_date`,`end_date`,`usage_limit`,`per_customer`,`stackable`,`priority`,`conditions`,`applies_to`,`customer_segments`,`created_by`)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
    ");
    $st->execute(array(
        $siteKey, (string)($data['name']??''), strtoupper((string)($data['code']??'')),
        (string)($data['type']??'percentage'), (float)($data['value']??0), (float)($data['min_order']??0), (float)($data['max_discount']??0),
        (string)($data['start_date']??date('Y-m-d')), (string)($data['end_date']??date('Y-m-d', strtotime('+30 days'))),
        (int)($data['usage_limit']??0), (int)($data['per_customer']??0), (int)($data['stackable']??0), (int)($data['priority']??0),
        json_encode($data['conditions']??array()), json_encode($data['applies_to']??array()), json_encode($data['customer_segments']??array()),
        (int)($data['created_by']??0),
    ));
    return array('ok' => true, 'promotion_id' => (int)$pdo->lastInsertId());
}

function epc_promo_list(PDO $pdo, string $siteKey, bool $activeOnly = false): array
{
    epc_promo_ensure_schema($pdo);
    $where = '`site_key` = ?';
    $params = array($siteKey);
    if ($activeOnly) { $where .= ' AND `active` = 1 AND NOW() BETWEEN `start_date` AND `end_date`'; }
    $st = $pdo->prepare("SELECT * FROM `epc_promotions` WHERE {$where} ORDER BY `priority` DESC, `created_at` DESC");
    $st->execute($params);
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) {
        $r['conditions'] = json_decode($r['conditions']?:'[]', true);
        $r['applies_to'] = json_decode($r['applies_to']?:'[]', true);
        $r['customer_segments'] = json_decode($r['customer_segments']?:'[]', true);
    }
    return $rows;
}

function epc_promo_apply(PDO $pdo, string $siteKey, string $code, float $orderTotal, int $customerId = 0): array
{
    epc_promo_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT * FROM `epc_promotions` WHERE `site_key`=? AND `code`=? AND `active`=1 AND NOW() BETWEEN `start_date` AND `end_date`");
    $st->execute(array($siteKey, strtoupper($code)));
    $promo = $st->fetch(PDO::FETCH_ASSOC);
    if (!$promo) return array('ok' => false, 'error' => 'Invalid or expired promotion code');

    if ($promo['min_order'] > 0 && $orderTotal < (float)$promo['min_order']) {
        return array('ok' => false, 'error' => 'Minimum order ' . number_format((float)$promo['min_order'], 2) . ' not met');
    }
    if ($promo['usage_limit'] > 0 && (int)$promo['used_count'] >= (int)$promo['usage_limit']) {
        return array('ok' => false, 'error' => 'Promotion usage limit reached');
    }
    if ($promo['per_customer'] > 0 && $customerId > 0) {
        $st2 = $pdo->prepare("SELECT COUNT(*) FROM `epc_promotion_usage` WHERE `promotion_id`=? AND `customer_id`=?");
        $st2->execute(array($promo['id'], $customerId));
        if ((int)$st2->fetchColumn() >= (int)$promo['per_customer']) {
            return array('ok' => false, 'error' => 'Per-customer usage limit reached');
        }
    }

    $discount = 0;
    switch ($promo['type']) {
        case 'percentage': $discount = round($orderTotal * (float)$promo['value'] / 100, 2); break;
        case 'fixed': $discount = (float)$promo['value']; break;
        case 'free_shipping': $discount = 0; break;
        case 'bogo': $discount = round($orderTotal * 0.5, 2); break;
        default: $discount = round($orderTotal * (float)$promo['value'] / 100, 2);
    }

    if ($promo['max_discount'] > 0) { $discount = min($discount, (float)$promo['max_discount']); }
    $discount = min($discount, $orderTotal);

    return array(
        'ok' => true, 'promotion_id' => (int)$promo['id'], 'name' => $promo['name'],
        'type' => $promo['type'], 'discount' => $discount,
        'new_total' => round($orderTotal - $discount, 2),
        'free_shipping' => ($promo['type'] === 'free_shipping'),
    );
}

function epc_promo_record_usage(PDO $pdo, int $promoId, string $siteKey, int $customerId, string $orderRef, float $discount): array
{
    $pdo->prepare("INSERT INTO `epc_promotion_usage` (`promotion_id`,`site_key`,`customer_id`,`order_ref`,`discount_amount`) VALUES (?,?,?,?,?)")
        ->execute(array($promoId, $siteKey, $customerId, $orderRef, $discount));
    $pdo->prepare("UPDATE `epc_promotions` SET `used_count`=`used_count`+1 WHERE `id`=?")->execute(array($promoId));
    return array('ok' => true);
}

function epc_promo_fleet_stats(PDO $pdo): array
{
    epc_promo_ensure_schema($pdo);
    $st = $pdo->query("SELECT `site_key`, COUNT(*) AS `promotions`, SUM(CASE WHEN `active`=1 AND NOW() BETWEEN `start_date` AND `end_date` THEN 1 ELSE 0 END) AS `active`, SUM(`used_count`) AS `total_usage` FROM `epc_promotions` GROUP BY `site_key`");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
