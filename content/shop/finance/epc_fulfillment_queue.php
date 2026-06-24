<?php
/**
 * P1 #16 — Fulfillment Queue
 *
 * Pick/pack/ship workflow with warehouse assignment, wave picking,
 * packing slip generation, and shipping label integration.
 * Schema: epc_fulfillment_orders, epc_fulfillment_items
 */

if (!defined('EPC_FULFILLMENT_VERSION')) {
    define('EPC_FULFILLMENT_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_fulfillment_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_fulfillment_orders` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `order_id`        INT UNSIGNED   NOT NULL,
            `order_number`    VARCHAR(64)    NOT NULL DEFAULT '',
            `customer_name`   VARCHAR(255)   NOT NULL DEFAULT '',
            `status`          ENUM('queued','picking','picked','packing','packed','shipping','shipped','delivered','cancelled') NOT NULL DEFAULT 'queued',
            `priority`        ENUM('low','normal','high','urgent') NOT NULL DEFAULT 'normal',
            `warehouse`       VARCHAR(64)    NOT NULL DEFAULT 'main',
            `assigned_to`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `assigned_name`   VARCHAR(128)   NOT NULL DEFAULT '',
            `wave_id`         INT UNSIGNED   NOT NULL DEFAULT 0,
            `pick_started_at` DATETIME       NULL,
            `pick_completed_at` DATETIME     NULL,
            `pack_completed_at` DATETIME     NULL,
            `ship_date`       DATETIME       NULL,
            `carrier`         VARCHAR(64)    NOT NULL DEFAULT '',
            `tracking_number` VARCHAR(128)   NOT NULL DEFAULT '',
            `shipping_method` VARCHAR(64)    NOT NULL DEFAULT 'standard',
            `total_items`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `total_weight`    DECIMAL(10,2)  NOT NULL DEFAULT 0.00,
            `ship_address`    JSON           NULL,
            `notes`           TEXT           NOT NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX `idx_site_status` (`site_key`, `status`),
            INDEX `idx_order` (`order_id`),
            INDEX `idx_assigned` (`assigned_to`),
            INDEX `idx_wave` (`wave_id`),
            INDEX `idx_priority` (`priority`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_fulfillment_items` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `fulfillment_id`  BIGINT UNSIGNED NOT NULL,
            `sku`             VARCHAR(64)    NOT NULL,
            `product_name`    VARCHAR(255)   NOT NULL DEFAULT '',
            `qty_ordered`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `qty_picked`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `qty_packed`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `bin_location`    VARCHAR(32)    NOT NULL DEFAULT '',
            `weight`          DECIMAL(8,2)   NOT NULL DEFAULT 0.00,
            `pick_status`     ENUM('pending','picked','short','substituted') NOT NULL DEFAULT 'pending',
            `notes`           VARCHAR(255)   NOT NULL DEFAULT '',
            INDEX `idx_fulfillment` (`fulfillment_id`),
            INDEX `idx_sku` (`sku`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── queue order for fulfillment ─── */

function epc_fulfillment_queue(PDO $pdo, string $siteKey, array $order): array
{
    epc_fulfillment_ensure_schema($pdo);

    $st = $pdo->prepare("
        INSERT INTO `epc_fulfillment_orders`
            (`site_key`, `order_id`, `order_number`, `customer_name`, `priority`,
             `warehouse`, `total_items`, `total_weight`, `ship_address`, `notes`, `shipping_method`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (int) ($order['order_id'] ?? 0),
        (string) ($order['order_number'] ?? ''),
        (string) ($order['customer_name'] ?? ''),
        (string) ($order['priority'] ?? 'normal'),
        (string) ($order['warehouse'] ?? 'main'),
        (int) ($order['total_items'] ?? 0),
        (float) ($order['total_weight'] ?? 0),
        json_encode($order['ship_address'] ?? array()),
        (string) ($order['notes'] ?? ''),
        (string) ($order['shipping_method'] ?? 'standard'),
    ));

    $fId = (int) $pdo->lastInsertId();

    $items = $order['items'] ?? array();
    foreach ($items as $item) {
        $st = $pdo->prepare("
            INSERT INTO `epc_fulfillment_items`
                (`fulfillment_id`, `sku`, `product_name`, `qty_ordered`, `bin_location`, `weight`)
            VALUES (?, ?, ?, ?, ?, ?)
        ");
        $st->execute(array(
            $fId,
            (string) ($item['sku'] ?? ''),
            (string) ($item['product_name'] ?? ''),
            (int) ($item['qty'] ?? $item['quantity'] ?? 1),
            (string) ($item['bin_location'] ?? ''),
            (float) ($item['weight'] ?? 0),
        ));
    }

    return array('ok' => true, 'fulfillment_id' => $fId, 'status' => 'queued');
}

/* ─── transition status ─── */

function epc_fulfillment_transition(PDO $pdo, int $fId, string $newStatus, array $opts = array()): array
{
    $validTransitions = array(
        'queued'   => array('picking', 'cancelled'),
        'picking'  => array('picked', 'queued'),
        'picked'   => array('packing'),
        'packing'  => array('packed', 'picked'),
        'packed'   => array('shipping'),
        'shipping' => array('shipped'),
        'shipped'  => array('delivered'),
    );

    $st = $pdo->prepare("SELECT `status` FROM `epc_fulfillment_orders` WHERE `id` = ?");
    $st->execute(array($fId));
    $current = $st->fetchColumn();

    if (!$current) {
        return array('ok' => false, 'error' => 'Fulfillment order not found');
    }

    $allowed = $validTransitions[$current] ?? array();
    if (!in_array($newStatus, $allowed, true)) {
        return array('ok' => false, 'error' => 'Invalid transition: ' . $current . ' → ' . $newStatus);
    }

    $updates = array('`status` = ?');
    $params = array($newStatus);

    switch ($newStatus) {
        case 'picking':
            $updates[] = '`pick_started_at` = NOW()';
            if (!empty($opts['assigned_to'])) {
                $updates[] = '`assigned_to` = ?';
                $params[] = (int) $opts['assigned_to'];
                $updates[] = '`assigned_name` = ?';
                $params[] = (string) ($opts['assigned_name'] ?? '');
            }
            break;
        case 'picked':
            $updates[] = '`pick_completed_at` = NOW()';
            break;
        case 'packed':
            $updates[] = '`pack_completed_at` = NOW()';
            break;
        case 'shipping':
            if (!empty($opts['carrier'])) {
                $updates[] = '`carrier` = ?';
                $params[] = (string) $opts['carrier'];
            }
            if (!empty($opts['tracking_number'])) {
                $updates[] = '`tracking_number` = ?';
                $params[] = (string) $opts['tracking_number'];
            }
            break;
        case 'shipped':
            $updates[] = '`ship_date` = NOW()';
            break;
    }

    $params[] = $fId;
    $sql = 'UPDATE `epc_fulfillment_orders` SET ' . implode(', ', $updates) . ' WHERE `id` = ?';
    $pdo->prepare($sql)->execute($params);

    return array('ok' => true, 'status' => $newStatus);
}

/* ─── assign picker ─── */

function epc_fulfillment_assign(PDO $pdo, int $fId, int $userId, string $userName = ''): array
{
    $st = $pdo->prepare("UPDATE `epc_fulfillment_orders` SET `assigned_to` = ?, `assigned_name` = ? WHERE `id` = ?");
    $st->execute(array($userId, $userName, $fId));
    return array('ok' => true);
}

/* ─── pick item ─── */

function epc_fulfillment_pick_item(PDO $pdo, int $itemId, int $qtyPicked, string $status = 'picked'): array
{
    $validStatuses = array('picked', 'short', 'substituted');
    if (!in_array($status, $validStatuses, true)) {
        $status = 'picked';
    }
    $st = $pdo->prepare("UPDATE `epc_fulfillment_items` SET `qty_picked` = ?, `pick_status` = ? WHERE `id` = ?");
    $st->execute(array($qtyPicked, $status, $itemId));
    return array('ok' => true);
}

/* ─── pack item ─── */

function epc_fulfillment_pack_item(PDO $pdo, int $itemId, int $qtyPacked): array
{
    $st = $pdo->prepare("UPDATE `epc_fulfillment_items` SET `qty_packed` = ? WHERE `id` = ?");
    $st->execute(array($qtyPacked, $itemId));
    return array('ok' => true);
}

/* ─── get fulfillment with items ─── */

function epc_fulfillment_get(PDO $pdo, int $fId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_fulfillment_orders` WHERE `id` = ?");
    $st->execute(array($fId));
    $row = $st->fetch(PDO::FETCH_ASSOC);
    if (!$row) {
        return array();
    }

    $st = $pdo->prepare("SELECT * FROM `epc_fulfillment_items` WHERE `fulfillment_id` = ? ORDER BY `sku`");
    $st->execute(array($fId));
    $row['items'] = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    $row['ship_address'] = json_decode($row['ship_address'] ?: '{}', true);

    return $row;
}

/* ─── list fulfillment queue ─── */

function epc_fulfillment_list(PDO $pdo, string $siteKey, array $filters = array(), int $limit = 50): array
{
    epc_fulfillment_ensure_schema($pdo);

    $where = array('`site_key` = ?');
    $params = array($siteKey);

    if (!empty($filters['status'])) {
        $where[] = '`status` = ?';
        $params[] = (string) $filters['status'];
    }
    if (!empty($filters['warehouse'])) {
        $where[] = '`warehouse` = ?';
        $params[] = (string) $filters['warehouse'];
    }
    if (!empty($filters['assigned_to'])) {
        $where[] = '`assigned_to` = ?';
        $params[] = (int) $filters['assigned_to'];
    }

    $sql = 'SELECT * FROM `epc_fulfillment_orders` WHERE ' . implode(' AND ', $where)
         . ' ORDER BY FIELD(`priority`, "urgent", "high", "normal", "low"), `created_at` ASC LIMIT ' . (int) $limit;
    $st = $pdo->prepare($sql);
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── wave picking ─── */

function epc_fulfillment_create_wave(PDO $pdo, string $siteKey, array $fulfillmentIds): array
{
    if (empty($fulfillmentIds)) {
        return array('ok' => false, 'error' => 'No fulfillment IDs provided');
    }

    $waveId = (int) date('ymdHi');
    $placeholders = implode(',', array_fill(0, count($fulfillmentIds), '?'));
    $params = array_merge(array($waveId, $siteKey), array_map('intval', $fulfillmentIds));

    $st = $pdo->prepare("
        UPDATE `epc_fulfillment_orders`
        SET `wave_id` = ?
        WHERE `site_key` = ? AND `id` IN ({$placeholders}) AND `status` = 'queued'
    ");
    $st->execute($params);

    return array('ok' => true, 'wave_id' => $waveId, 'count' => $st->rowCount());
}

/* ─── packing slip data ─── */

function epc_fulfillment_packing_slip(PDO $pdo, int $fId): array
{
    $f = epc_fulfillment_get($pdo, $fId);
    if (empty($f)) {
        return array('ok' => false, 'error' => 'Not found');
    }

    return array(
        'ok'              => true,
        'order_number'    => $f['order_number'],
        'customer_name'   => $f['customer_name'],
        'ship_address'    => $f['ship_address'],
        'carrier'         => $f['carrier'],
        'tracking_number' => $f['tracking_number'],
        'items'           => array_map(function ($item) {
            return array(
                'sku'          => $item['sku'],
                'product_name' => $item['product_name'],
                'qty'          => $item['qty_packed'] ?: $item['qty_picked'] ?: $item['qty_ordered'],
                'bin'          => $item['bin_location'],
            );
        }, $f['items']),
        'packed_at'       => $f['pack_completed_at'],
    );
}

/* ─── fleet stats (BOS) ─── */

function epc_fulfillment_fleet_stats(PDO $pdo): array
{
    epc_fulfillment_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(*) AS `total`,
               SUM(CASE WHEN `status` = 'queued' THEN 1 ELSE 0 END) AS `queued`,
               SUM(CASE WHEN `status` IN ('picking','picked','packing','packed') THEN 1 ELSE 0 END) AS `in_progress`,
               SUM(CASE WHEN `status` = 'shipped' THEN 1 ELSE 0 END) AS `shipped`,
               SUM(CASE WHEN `status` = 'delivered' THEN 1 ELSE 0 END) AS `delivered`,
               AVG(TIMESTAMPDIFF(HOUR, `created_at`, COALESCE(`ship_date`, NOW()))) AS `avg_fulfillment_hrs`
        FROM `epc_fulfillment_orders`
        GROUP BY `site_key`
        ORDER BY `queued` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── SLA check ─── */

function epc_fulfillment_sla_breaches(PDO $pdo, string $siteKey, int $maxHours = 48): array
{
    epc_fulfillment_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT * FROM `epc_fulfillment_orders`
        WHERE `site_key` = ? AND `status` NOT IN ('shipped','delivered','cancelled')
          AND `created_at` < DATE_SUB(NOW(), INTERVAL ? HOUR)
        ORDER BY `created_at` ASC
    ");
    $st->execute(array($siteKey, $maxHours));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
