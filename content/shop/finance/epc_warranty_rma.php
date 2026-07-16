<?php
/**
 * P2 #28 — Warranty / RMA Tracking
 *
 * Warranty registration, RMA (Return Merchandise Authorization) requests,
 * claim processing, repair tracking, and replacement management.
 * Schema: epc_warranties, epc_rma_requests, epc_rma_items
 */

if (!defined('EPC_WARRANTY_RMA_VERSION')) {
    define('EPC_WARRANTY_RMA_VERSION', '1.0.0');
}

function epc_warranty_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_warranties` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `product_sku`     VARCHAR(64)    NOT NULL,
            `product_name`    VARCHAR(255)   NOT NULL DEFAULT '',
            `serial_number`   VARCHAR(64)    NOT NULL DEFAULT '',
            `customer_id`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `customer_name`   VARCHAR(128)   NOT NULL DEFAULT '',
            `order_ref`       VARCHAR(64)    NOT NULL DEFAULT '',
            `purchase_date`   DATE           NOT NULL,
            `warranty_months` INT UNSIGNED   NOT NULL DEFAULT 12,
            `expiry_date`     DATE           NOT NULL,
            `warranty_type`   ENUM('standard','extended','lifetime') NOT NULL DEFAULT 'standard',
            `status`          ENUM('active','expired','claimed','voided') NOT NULL DEFAULT 'active',
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_serial` (`serial_number`),
            INDEX `idx_expiry` (`expiry_date`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_rma_requests` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `rma_number`      VARCHAR(32)    NOT NULL,
            `warranty_id`     INT UNSIGNED   NULL,
            `customer_id`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `customer_name`   VARCHAR(128)   NOT NULL DEFAULT '',
            `reason`          ENUM('defective','wrong_item','damaged','not_as_described','warranty_claim','other') NOT NULL DEFAULT 'defective',
            `description`     TEXT           NOT NULL,
            `status`          ENUM('pending','approved','received','inspecting','repair','replacement','refund','completed','rejected','cancelled') NOT NULL DEFAULT 'pending',
            `resolution_type` ENUM('repair','replace','refund','credit','none') NOT NULL DEFAULT 'none',
            `resolution_notes`TEXT           NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            `completed_at`    DATETIME       NULL,
            UNIQUE KEY `uk_rma` (`rma_number`),
            INDEX `idx_site` (`site_key`),
            INDEX `idx_status` (`status`),
            INDEX `idx_warranty` (`warranty_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_rma_items` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `rma_id`          INT UNSIGNED   NOT NULL,
            `product_sku`     VARCHAR(64)    NOT NULL,
            `product_name`    VARCHAR(255)   NOT NULL DEFAULT '',
            `qty`             INT UNSIGNED   NOT NULL DEFAULT 1,
            `unit_price`      DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `condition_received` ENUM('unopened','good','damaged','defective') NULL,
            `inspection_notes`TEXT           NULL,
            INDEX `idx_rma` (`rma_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_warranty_register(PDO $pdo, string $siteKey, array $data): array
{
    epc_warranty_ensure_schema($pdo);
    $purchaseDate = (string) ($data['purchase_date'] ?? date('Y-m-d'));
    $months = (int) ($data['warranty_months'] ?? 12);
    $expiry = date('Y-m-d', strtotime($purchaseDate . ' + ' . $months . ' months'));

    $st = $pdo->prepare("
        INSERT INTO `epc_warranties` (`site_key`, `product_sku`, `product_name`, `serial_number`, `customer_id`, `customer_name`, `order_ref`, `purchase_date`, `warranty_months`, `expiry_date`, `warranty_type`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array($siteKey, (string)($data['product_sku']??''), (string)($data['product_name']??''), (string)($data['serial_number']??''), (int)($data['customer_id']??0), (string)($data['customer_name']??''), (string)($data['order_ref']??''), $purchaseDate, $months, $expiry, (string)($data['warranty_type']??'standard')));
    return array('ok' => true, 'warranty_id' => (int) $pdo->lastInsertId(), 'expiry_date' => $expiry);
}

function epc_warranty_check(PDO $pdo, string $serialNumber): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_warranties` WHERE `serial_number` = ? ORDER BY `expiry_date` DESC LIMIT 1");
    $st->execute(array($serialNumber));
    $w = $st->fetch(PDO::FETCH_ASSOC);
    if (!$w) return array('ok' => false, 'error' => 'No warranty found');
    $w['is_valid'] = ($w['status'] === 'active' && $w['expiry_date'] >= date('Y-m-d'));
    return array('ok' => true, 'warranty' => $w);
}

function epc_warranty_list(PDO $pdo, string $siteKey, string $status = ''): array
{
    epc_warranty_ensure_schema($pdo);
    $where = array('`site_key` = ?');
    $params = array($siteKey);
    if ($status !== '') { $where[] = '`status` = ?'; $params[] = $status; }
    $st = $pdo->prepare("SELECT * FROM `epc_warranties` WHERE " . implode(' AND ', $where) . " ORDER BY `expiry_date` DESC");
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_rma_create(PDO $pdo, string $siteKey, array $data): array
{
    epc_warranty_ensure_schema($pdo);
    $rmaNumber = 'RMA-' . strtoupper($siteKey) . '-' . date('Ymd') . '-' . str_pad((string)random_int(1, 9999), 4, '0', STR_PAD_LEFT);

    $st = $pdo->prepare("
        INSERT INTO `epc_rma_requests` (`site_key`, `rma_number`, `warranty_id`, `customer_id`, `customer_name`, `reason`, `description`)
        VALUES (?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array($siteKey, $rmaNumber, (int)($data['warranty_id']??0) ?: null, (int)($data['customer_id']??0), (string)($data['customer_name']??''), (string)($data['reason']??'defective'), (string)($data['description']??'')));
    $rmaId = (int) $pdo->lastInsertId();

    foreach (($data['items'] ?? array()) as $item) {
        $pdo->prepare("INSERT INTO `epc_rma_items` (`rma_id`, `product_sku`, `product_name`, `qty`, `unit_price`) VALUES (?, ?, ?, ?, ?)")
            ->execute(array($rmaId, (string)($item['product_sku']??''), (string)($item['product_name']??''), (int)($item['qty']??1), (float)($item['unit_price']??0)));
    }

    // Blockchain BOS: anchor warranty RMA create (best-effort).
    try {
        $bcFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
        if (is_file($bcFile)) {
            require_once $bcFile;
            epc_bc_bos_maybe_record_document(
                'rma',
                $rmaNumber,
                array(
                    'rma_id' => $rmaId,
                    'rma_number' => $rmaNumber,
                    'warranty_id' => (int) ($data['warranty_id'] ?? 0),
                    'customer_id' => (int) ($data['customer_id'] ?? 0),
                    'customer_name' => (string) ($data['customer_name'] ?? ''),
                    'reason' => (string) ($data['reason'] ?? 'defective'),
                    'item_count' => count($data['items'] ?? array()),
                    'channel' => 'warranty',
                ),
                array('tenant_key' => $siteKey)
            );
        }
    } catch (Throwable $e) {
        // best-effort
    }

    return array('ok' => true, 'rma_id' => $rmaId, 'rma_number' => $rmaNumber);
}

function epc_rma_transition(PDO $pdo, int $rmaId, string $newStatus, string $notes = ''): array
{
    $valid = array(
        'pending' => array('approved','rejected','cancelled'),
        'approved' => array('received','cancelled'),
        'received' => array('inspecting'),
        'inspecting' => array('repair','replacement','refund','rejected'),
        'repair' => array('completed'),
        'replacement' => array('completed'),
        'refund' => array('completed'),
    );

    $st = $pdo->prepare("SELECT `status` FROM `epc_rma_requests` WHERE `id` = ?");
    $st->execute(array($rmaId));
    $current = $st->fetchColumn();
    if (!$current) return array('ok' => false, 'error' => 'RMA not found');
    if (!isset($valid[$current]) || !in_array($newStatus, $valid[$current], true)) {
        return array('ok' => false, 'error' => 'Invalid transition: ' . $current . ' → ' . $newStatus);
    }

    $completedAt = ($newStatus === 'completed') ? date('Y-m-d H:i:s') : null;
    $pdo->prepare("UPDATE `epc_rma_requests` SET `status` = ?, `resolution_notes` = CONCAT(IFNULL(`resolution_notes`,''), ?), `completed_at` = COALESCE(?, `completed_at`) WHERE `id` = ?")
        ->execute(array($newStatus, ($notes !== '' ? "\n" . $notes : ''), $completedAt, $rmaId));

    return array('ok' => true, 'from' => $current, 'to' => $newStatus);
}

function epc_rma_list(PDO $pdo, string $siteKey, array $filters = array()): array
{
    epc_warranty_ensure_schema($pdo);
    $where = array('`site_key` = ?');
    $params = array($siteKey);
    if (!empty($filters['status'])) { $where[] = '`status` = ?'; $params[] = $filters['status']; }
    $st = $pdo->prepare("SELECT * FROM `epc_rma_requests` WHERE " . implode(' AND ', $where) . " ORDER BY `created_at` DESC");
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_rma_detail(PDO $pdo, int $rmaId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_rma_requests` WHERE `id` = ?");
    $st->execute(array($rmaId));
    $rma = $st->fetch(PDO::FETCH_ASSOC);
    if (!$rma) return array();
    $st = $pdo->prepare("SELECT * FROM `epc_rma_items` WHERE `rma_id` = ?");
    $st->execute(array($rmaId));
    $rma['items'] = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    return $rma;
}

function epc_warranty_fleet_stats(PDO $pdo): array
{
    epc_warranty_ensure_schema($pdo);
    $st = $pdo->query("
        SELECT w.`site_key`,
               COUNT(DISTINCT w.`id`) AS `warranties`,
               SUM(CASE WHEN w.`status` = 'active' AND w.`expiry_date` >= CURDATE() THEN 1 ELSE 0 END) AS `active`,
               SUM(CASE WHEN w.`status` = 'expired' OR w.`expiry_date` < CURDATE() THEN 1 ELSE 0 END) AS `expired`,
               (SELECT COUNT(*) FROM `epc_rma_requests` r WHERE r.`site_key` = w.`site_key`) AS `total_rma`,
               (SELECT COUNT(*) FROM `epc_rma_requests` r2 WHERE r2.`site_key` = w.`site_key` AND r2.`status` NOT IN ('completed','rejected','cancelled')) AS `open_rma`
        FROM `epc_warranties` w
        GROUP BY w.`site_key`
        ORDER BY `open_rma` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── Warranty Lookup & Lifecycle ─── */

function epc_warranty_lookup(PDO $pdo, string $siteKey, string $serialOrSku): array
{
    epc_warranty_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT * FROM `epc_warranties` WHERE `site_key`=? AND (`serial_number`=? OR `product_sku`=?) ORDER BY `created_at` DESC");
    $st->execute(array($siteKey, $serialOrSku, $serialOrSku));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_warranty_check_valid(PDO $pdo, int $warrantyId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_warranties` WHERE `id`=?");
    $st->execute(array($warrantyId));
    $w = $st->fetch(PDO::FETCH_ASSOC);
    if (!$w) return array('valid' => false, 'error' => 'Warranty not found');
    $expired = strtotime($w['expiry_date']) < time();
    return array('valid' => !$expired && $w['status'] === 'active', 'warranty' => $w, 'expired' => $expired, 'days_remaining' => $expired ? 0 : (int)((strtotime($w['expiry_date']) - time()) / 86400));
}

function epc_warranty_extend(PDO $pdo, int $warrantyId, int $months, string $reason = ''): array
{
    $st = $pdo->prepare("SELECT `expiry_date` FROM `epc_warranties` WHERE `id`=?");
    $st->execute(array($warrantyId));
    $current = $st->fetchColumn();
    if (!$current) return array('ok' => false, 'error' => 'Not found');
    $newExpiry = date('Y-m-d', strtotime($current . ' + ' . $months . ' months'));
    $pdo->prepare("UPDATE `epc_warranties` SET `expiry_date`=?, `status`='active' WHERE `id`=?")->execute(array($newExpiry, $warrantyId));
    return array('ok' => true, 'new_expiry' => $newExpiry);
}

function epc_warranty_void(PDO $pdo, int $warrantyId, string $reason = ''): array
{
    $pdo->prepare("UPDATE `epc_warranties` SET `status`='voided' WHERE `id`=?")->execute(array($warrantyId));
    return array('ok' => true);
}

/* ─── RMA Resolution & Tracking ─── */

function epc_rma_add_item(PDO $pdo, int $rmaId, array $item): array
{
    $pdo->prepare("INSERT INTO `epc_rma_items` (`rma_id`,`product_sku`,`serial_number`,`quantity`,`reason`,`condition_received`) VALUES (?,?,?,?,?,?)")
        ->execute(array($rmaId, (string)($item['product_sku']??''), (string)($item['serial_number']??''), (int)($item['quantity']??1), (string)($item['reason']??''), (string)($item['condition']??'unknown')));
    return array('ok' => true);
}

function epc_rma_assign(PDO $pdo, int $rmaId, int $technicianId): array
{
    $pdo->prepare("UPDATE `epc_rma_requests` SET `assigned_to`=? WHERE `id`=?")->execute(array($technicianId, $rmaId));
    return array('ok' => true);
}

function epc_rma_stats(PDO $pdo, string $siteKey): array
{
    epc_warranty_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT `status`, COUNT(*) AS `count` FROM `epc_rma_requests` WHERE `site_key`=? GROUP BY `status`");
    $st->execute(array($siteKey));
    $byStatus = $st->fetchAll(PDO::FETCH_KEY_PAIR) ?: array();
    $avgDays = $pdo->prepare("SELECT AVG(DATEDIFF(`completed_at`, `created_at`)) FROM `epc_rma_requests` WHERE `site_key`=? AND `completed_at` IS NOT NULL");
    $avgDays->execute(array($siteKey));
    return array('by_status' => $byStatus, 'avg_resolution_days' => round((float)$avgDays->fetchColumn(), 1));
}
