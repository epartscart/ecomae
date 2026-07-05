<?php
/**
 * E-commerce Integration Module — connect external platforms (Shopify, Magento, WooCommerce).
 * Sync orders, products, inventory, customers between ERP and external stores.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_ecom_int_ensure_schema')) {
    function epc_ecom_int_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ecom_connections` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `platform` varchar(30) NOT NULL DEFAULT '' COMMENT 'shopify,magento,woocommerce,bigcommerce,prestashop',
            `store_name` varchar(200) NOT NULL DEFAULT '',
            `store_url` varchar(500) NOT NULL DEFAULT '',
            `api_key` varchar(300) NOT NULL DEFAULT '',
            `api_secret` varchar(300) NOT NULL DEFAULT '',
            `access_token` varchar(500) NOT NULL DEFAULT '',
            `webhook_secret` varchar(200) NOT NULL DEFAULT '',
            `sync_products` tinyint(1) NOT NULL DEFAULT 1,
            `sync_orders` tinyint(1) NOT NULL DEFAULT 1,
            `sync_inventory` tinyint(1) NOT NULL DEFAULT 1,
            `sync_customers` tinyint(1) NOT NULL DEFAULT 1,
            `sync_interval_min` int(11) NOT NULL DEFAULT 15,
            `last_sync_at` datetime DEFAULT NULL,
            `status` enum('active','paused','error','disconnected') NOT NULL DEFAULT 'active',
            `error_message` varchar(500) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_platform` (`platform`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='E-commerce platform connections'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ecom_sync_log` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `connection_id` int(11) NOT NULL DEFAULT 0,
            `direction` enum('pull','push') NOT NULL DEFAULT 'pull',
            `entity_type` varchar(30) NOT NULL DEFAULT '' COMMENT 'order,product,inventory,customer',
            `records_processed` int(11) NOT NULL DEFAULT 0,
            `records_created` int(11) NOT NULL DEFAULT 0,
            `records_updated` int(11) NOT NULL DEFAULT 0,
            `records_failed` int(11) NOT NULL DEFAULT 0,
            `error_details` text,
            `duration_ms` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_connection` (`connection_id`),
            KEY `x_time` (`time_created`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Sync operation log'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ecom_product_map` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `connection_id` int(11) NOT NULL DEFAULT 0,
            `external_id` varchar(100) NOT NULL DEFAULT '',
            `external_sku` varchar(100) NOT NULL DEFAULT '',
            `internal_product_id` int(11) NOT NULL DEFAULT 0,
            `internal_sku` varchar(100) NOT NULL DEFAULT '',
            `last_synced_at` datetime DEFAULT NULL,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_conn_ext` (`connection_id`, `external_id`),
            KEY `x_internal` (`internal_product_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Product ID mapping between platforms'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ecom_order_map` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `connection_id` int(11) NOT NULL DEFAULT 0,
            `external_order_id` varchar(100) NOT NULL DEFAULT '',
            `external_order_no` varchar(100) NOT NULL DEFAULT '',
            `internal_order_id` int(11) NOT NULL DEFAULT 0,
            `internal_invoice_id` int(11) NOT NULL DEFAULT 0,
            `status` varchar(30) NOT NULL DEFAULT '',
            `last_synced_at` datetime DEFAULT NULL,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_conn_order` (`connection_id`, `external_order_id`),
            KEY `x_internal` (`internal_order_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Order ID mapping between platforms'");
    }

    function epc_ecom_int_connect(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_ecom_connections` (`company_id`,`platform`,`store_name`,`store_url`,`api_key`,`api_secret`,`access_token`,`sync_products`,`sync_orders`,`sync_inventory`,`sync_customers`,`sync_interval_min`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['platform'] ?? '', $data['store_name'] ?? '', $data['store_url'] ?? '', $data['api_key'] ?? '', $data['api_secret'] ?? '', $data['access_token'] ?? '', $data['sync_products'] ?? 1, $data['sync_orders'] ?? 1, $data['sync_inventory'] ?? 1, $data['sync_customers'] ?? 1, $data['sync_interval_min'] ?? 15, 'active', time()]);
        return (int) $db->lastInsertId();
    }

    function epc_ecom_int_sync_orders_shopify(PDO $db, int $connectionId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_ecom_connections` WHERE `id` = ?");
        $stmt->execute([$connectionId]);
        $conn = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$conn) return ['ok' => false, 'error' => 'Connection not found'];

        $url = rtrim($conn['store_url'], '/') . '/admin/api/2024-01/orders.json?status=any&limit=50';
        $ctx = stream_context_create(['http' => [
            'header' => "X-Shopify-Access-Token: " . $conn['access_token'] . "\r\nContent-Type: application/json\r\n",
            'timeout' => 30
        ]]);
        $response = @file_get_contents($url, false, $ctx);
        if ($response === false) return ['ok' => false, 'error' => 'Shopify API request failed'];

        $json = json_decode($response, true);
        $orders = $json['orders'] ?? [];
        $created = 0; $updated = 0;

        foreach ($orders as $order) {
            $existing = $db->prepare("SELECT `id` FROM `epc_ecom_order_map` WHERE `connection_id` = ? AND `external_order_id` = ?");
            $existing->execute([$connectionId, (string) $order['id']]);
            if ($existing->fetchColumn()) {
                $updated++;
            } else {
                $db->prepare("INSERT INTO `epc_ecom_order_map` (`connection_id`,`external_order_id`,`external_order_no`,`status`,`last_synced_at`) VALUES (?,?,?,?,NOW())")
                    ->execute([$connectionId, (string) $order['id'], $order['name'] ?? '', $order['financial_status'] ?? '']);
                $created++;
            }
        }

        $db->prepare("INSERT INTO `epc_ecom_sync_log` (`connection_id`,`direction`,`entity_type`,`records_processed`,`records_created`,`records_updated`,`time_created`) VALUES (?,?,?,?,?,?,?)")
            ->execute([$connectionId, 'pull', 'order', count($orders), $created, $updated, time()]);

        return ['ok' => true, 'processed' => count($orders), 'created' => $created, 'updated' => $updated];
    }

    function epc_ecom_int_list_connections(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_ecom_connections` WHERE `company_id` = ? ORDER BY `time_created` DESC");
        $stmt->execute([$companyId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
