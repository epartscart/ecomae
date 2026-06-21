<?php
/**
 * Advanced ERP — Price lists, Promotions/Discounts, Loyalty.
 *
 * - Price lists: per-customer / per-currency / quantity-break pricing with
 *   effective dating; resolve the best applicable price for an item+qty+date.
 * - Promotions: percentage / fixed / buy-x-get-y, with validity window and
 *   minimum spend; apply to an order subtotal.
 * - Loyalty: points accrual on spend, redemption to value, balance tracking.
 *
 * Additive: new epc_pl_*, epc_promo_*, epc_loy_* tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_price_ensure_schema')) {
    function epc_price_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_pl_lists` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `priority` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Price lists'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_pl_prices` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `list_id` int(11) NOT NULL,
            `item_id` int(11) NOT NULL,
            `min_qty` decimal(16,3) NOT NULL DEFAULT 0.000,
            `price` decimal(16,4) NOT NULL DEFAULT 0.0000,
            `valid_from` int(11) NOT NULL DEFAULT 0,
            `valid_to` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_list_item` (`list_id`,`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Price list lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_promo_promotions` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `type` varchar(16) NOT NULL DEFAULT 'percent',
            `value` decimal(16,4) NOT NULL DEFAULT 0.0000,
            `min_spend` decimal(16,2) NOT NULL DEFAULT 0.00,
            `valid_from` int(11) NOT NULL DEFAULT 0,
            `valid_to` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Promotions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_loy_accounts` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `customer_id` int(11) NOT NULL,
            `points` decimal(16,2) NOT NULL DEFAULT 0.00,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_cust` (`customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Loyalty accounts'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_loy_ledger` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `customer_id` int(11) NOT NULL,
            `type` varchar(12) NOT NULL DEFAULT 'earn',
            `points` decimal(16,2) NOT NULL DEFAULT 0.00,
            `reference` varchar(60) DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_cust` (`customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Loyalty ledger'");
    }
}

/* ----------------------------- Price lists ---------------------------- */

if (!function_exists('epc_pl_list_save')) {
    /** @param array<string,mixed> $data */
    function epc_pl_list_save(PDO $db, array $data, int $id = 0): int
    {
        epc_price_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_pl_lists` SET `name`=?, `currency`=?, `customer_id`=?, `priority`=?, `active`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (string) ($data['currency'] ?? 'AED'), (int) ($data['customer_id'] ?? 0), (int) ($data['priority'] ?? 0), (int) ($data['active'] ?? 1), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_pl_lists` (`code`,`name`,`currency`,`customer_id`,`priority`,`active`) VALUES (?,?,?,?,?,1)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (string) ($data['currency'] ?? 'AED'), (int) ($data['customer_id'] ?? 0), (int) ($data['priority'] ?? 0)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_pl_price_set')) {
    /** @param array<string,mixed> $data list_id, item_id, min_qty, price, valid_from, valid_to */
    function epc_pl_price_set(PDO $db, array $data): int
    {
        epc_price_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_pl_prices` (`list_id`,`item_id`,`min_qty`,`price`,`valid_from`,`valid_to`) VALUES (?,?,?,?,?,?)")
           ->execute(array((int) $data['list_id'], (int) $data['item_id'], (float) ($data['min_qty'] ?? 0), (float) ($data['price'] ?? 0), (int) ($data['valid_from'] ?? 0), (int) ($data['valid_to'] ?? 0)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_pl_resolve_price')) {
    /**
     * Resolve the applicable unit price for an item+qty+date+customer.
     * Prefers a customer-specific list (higher priority), then the qty-break
     * with the highest min_qty <= qty, valid at the date.
     *
     * @return array<string,mixed>|null {price, list_id, min_qty}
     */
    function epc_pl_resolve_price(PDO $db, int $itemId, float $qty, int $customerId = 0, int $atDate = 0): ?array
    {
        epc_price_ensure_schema($db);
        $atDate = $atDate > 0 ? $atDate : time();
        $sql = "SELECT p.`price`, p.`list_id`, p.`min_qty`, l.`priority`, l.`customer_id`
                FROM `epc_pl_prices` p
                JOIN `epc_pl_lists` l ON l.`id`=p.`list_id`
                WHERE p.`item_id`=? AND l.`active`=1
                  AND (l.`customer_id`=0 OR l.`customer_id`=?)
                  AND p.`min_qty`<=?
                  AND (p.`valid_from`=0 OR p.`valid_from`<=?)
                  AND (p.`valid_to`=0 OR p.`valid_to`>=?)
                ORDER BY (l.`customer_id`=?) DESC, l.`priority` DESC, p.`min_qty` DESC
                LIMIT 1";
        $st = $db->prepare($sql);
        $st->execute(array($itemId, $customerId, $qty, $atDate, $atDate, $customerId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return null;
        }
        return array('price' => round((float) $row['price'], 4), 'list_id' => (int) $row['list_id'], 'min_qty' => (float) $row['min_qty']);
    }
}

/* ----------------------------- Promotions ----------------------------- */

if (!function_exists('epc_promo_save')) {
    /** @param array<string,mixed> $data */
    function epc_promo_save(PDO $db, array $data, int $id = 0): int
    {
        epc_price_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_promo_promotions` SET `name`=?, `type`=?, `value`=?, `min_spend`=?, `valid_from`=?, `valid_to`=?, `active`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (string) ($data['type'] ?? 'percent'), (float) ($data['value'] ?? 0), (float) ($data['min_spend'] ?? 0), (int) ($data['valid_from'] ?? 0), (int) ($data['valid_to'] ?? 0), (int) ($data['active'] ?? 1), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_promo_promotions` (`code`,`name`,`type`,`value`,`min_spend`,`valid_from`,`valid_to`,`active`) VALUES (?,?,?,?,?,?,?,1)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (string) ($data['type'] ?? 'percent'), (float) ($data['value'] ?? 0), (float) ($data['min_spend'] ?? 0), (int) ($data['valid_from'] ?? 0), (int) ($data['valid_to'] ?? 0)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_promo_apply')) {
    /**
     * Apply a promotion code to a subtotal at a date. Returns discount + net.
     * Honours validity window, active flag, and minimum spend.
     *
     * @return array<string,mixed> {applied, reason, discount, net}
     */
    function epc_promo_apply(PDO $db, string $code, float $subtotal, int $atDate = 0): array
    {
        epc_price_ensure_schema($db);
        $atDate = $atDate > 0 ? $atDate : time();
        $st = $db->prepare("SELECT * FROM `epc_promo_promotions` WHERE `code`=?");
        $st->execute(array($code));
        $p = $st->fetch(PDO::FETCH_ASSOC);
        if (!$p) {
            return array('applied' => false, 'reason' => 'not_found', 'discount' => 0.0, 'net' => round($subtotal, 2));
        }
        if ((int) $p['active'] !== 1) {
            return array('applied' => false, 'reason' => 'inactive', 'discount' => 0.0, 'net' => round($subtotal, 2));
        }
        if (((int) $p['valid_from'] > 0 && $atDate < (int) $p['valid_from']) || ((int) $p['valid_to'] > 0 && $atDate > (int) $p['valid_to'])) {
            return array('applied' => false, 'reason' => 'expired', 'discount' => 0.0, 'net' => round($subtotal, 2));
        }
        if ($subtotal < (float) $p['min_spend']) {
            return array('applied' => false, 'reason' => 'below_min_spend', 'discount' => 0.0, 'net' => round($subtotal, 2));
        }
        $discount = 0.0;
        if ((string) $p['type'] === 'percent') {
            $discount = round($subtotal * (float) $p['value'] / 100, 2);
        } elseif ((string) $p['type'] === 'fixed') {
            $discount = round(min((float) $p['value'], $subtotal), 2);
        }
        return array('applied' => true, 'reason' => 'ok', 'discount' => $discount, 'net' => round($subtotal - $discount, 2));
    }
}

/* ------------------------------- Loyalty ------------------------------ */

if (!function_exists('epc_loy_earn')) {
    /**
     * Accrue points for spend. pointsPerUnit = points earned per 1 of spend.
     *
     * @return float new balance
     */
    function epc_loy_earn(PDO $db, int $customerId, float $spend, float $pointsPerUnit = 1.0, string $reference = ''): float
    {
        epc_price_ensure_schema($db);
        $points = round($spend * $pointsPerUnit, 2);
        $db->prepare("INSERT INTO `epc_loy_accounts` (`customer_id`,`points`) VALUES (?,?) ON DUPLICATE KEY UPDATE `points`=`points`+VALUES(`points`)")
           ->execute(array($customerId, $points));
        $db->prepare("INSERT INTO `epc_loy_ledger` (`customer_id`,`type`,`points`,`reference`,`time_created`) VALUES (?, 'earn', ?, ?, ?)")
           ->execute(array($customerId, $points, $reference, time()));
        return epc_loy_balance($db, $customerId);
    }
}

if (!function_exists('epc_loy_redeem')) {
    /**
     * Redeem points to a money value. valuePerPoint = currency per point.
     * Caps redemption at the available balance.
     *
     * @return array<string,mixed> {redeemed_points, value, balance}
     */
    function epc_loy_redeem(PDO $db, int $customerId, float $points, float $valuePerPoint = 0.05, string $reference = ''): array
    {
        epc_price_ensure_schema($db);
        $bal = epc_loy_balance($db, $customerId);
        $redeem = round(min($points, $bal), 2);
        if ($redeem > 0) {
            $db->prepare("UPDATE `epc_loy_accounts` SET `points`=`points`-? WHERE `customer_id`=?")->execute(array($redeem, $customerId));
            $db->prepare("INSERT INTO `epc_loy_ledger` (`customer_id`,`type`,`points`,`reference`,`time_created`) VALUES (?, 'redeem', ?, ?, ?)")
               ->execute(array($customerId, $redeem, $reference, time()));
        }
        return array('redeemed_points' => $redeem, 'value' => round($redeem * $valuePerPoint, 2), 'balance' => epc_loy_balance($db, $customerId));
    }
}

if (!function_exists('epc_loy_balance')) {
    function epc_loy_balance(PDO $db, int $customerId): float
    {
        epc_price_ensure_schema($db);
        $st = $db->prepare("SELECT `points` FROM `epc_loy_accounts` WHERE `customer_id`=?");
        $st->execute(array($customerId));
        $v = $st->fetchColumn();
        return $v === false ? 0.0 : round((float) $v, 2);
    }
}
