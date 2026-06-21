<?php
/**
 * Demo sales seeder.
 *
 * Creates realistic completed sales orders across the last N months so the
 * executive dashboard / KPI engine (revenue, gross margin, trend) has data to
 * report. Orders are tagged with email_not_auth = 'DEMO-SALE' so they can be
 * cleared again without touching real storefront orders.
 */

if (!function_exists('epc_demo_sales_finish_status')) {
    function epc_demo_sales_finish_status(PDO $db): array
    {
        $orderStatus = (int) $db->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC LIMIT 1')->fetchColumn();
        // prefer a counting (count_flag != 0) finish item status
        $itemStatus = (int) $db->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 AND `count_flag` != 0 ORDER BY `order` ASC LIMIT 1')->fetchColumn();
        if ($itemStatus <= 0) {
            $itemStatus = (int) $db->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC LIMIT 1')->fetchColumn();
        }
        return array('order' => $orderStatus, 'item' => $itemStatus);
    }
}

if (!function_exists('epc_demo_clear_sales')) {
    /**
     * Remove previously-seeded demo sales orders.
     *
     * @return int number of orders removed
     */
    function epc_demo_clear_sales(PDO $db): int
    {
        $ids = $db->query("SELECT `id` FROM `shop_orders` WHERE `email_not_auth` = 'DEMO-SALE'")->fetchAll(PDO::FETCH_COLUMN);
        if (empty($ids)) {
            return 0;
        }
        $in = implode(',', array_map('intval', $ids));
        foreach (array(
            "DELETE FROM `shop_orders_items_details` WHERE `order_id` IN ($in)",
            "DELETE FROM `shop_orders_items` WHERE `order_id` IN ($in)",
            "DELETE FROM `shop_orders_logs` WHERE `order_id` IN ($in)",
            "DELETE FROM `shop_orders` WHERE `id` IN ($in)",
        ) as $sql) {
            try {
                $db->exec($sql);
            } catch (Throwable $e) {
                // table may not exist in some deployments; continue
            }
        }
        return count($ids);
    }
}

if (!function_exists('epc_demo_seed_sales')) {
    /**
     * Seed completed sales orders for the last N months.
     *
     * @return array{orders:int,lines:int,revenue:float}
     */
    function epc_demo_seed_sales(PDO $db, int $months = 6): array
    {
        if (!function_exists('epc_erp_inventory_stock_report')) {
            require_once __DIR__ . '/epc_erp_inventory.php';
        }
        $st = epc_demo_sales_finish_status($db);
        if ($st['order'] <= 0 || $st['item'] <= 0) {
            throw new Exception('No finished-order status configured in this store');
        }
        epc_demo_clear_sales($db);

        $officeId = (int) $db->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
        $storageId = (int) $db->query('SELECT `id` FROM `shop_storages` ORDER BY `id` ASC LIMIT 1')->fetchColumn();

        // product basis from inventory (cost → realistic sell price)
        $stock = epc_erp_inventory_stock_report($db, 0);
        $items = array();
        foreach ($stock as $s) {
            $cost = (float) $s['avg_unit_cost'];
            if ($cost <= 0) {
                continue;
            }
            $items[(int) $s['item_id']] = array(
                'cost' => $cost,
                'sku' => (string) ($s['sku'] ?? ''),
                'name' => (string) ($s['name'] ?? ''),
            );
        }
        $items = array_values($items);
        if (empty($items)) {
            throw new Exception('No costed inventory items to base sample sales on');
        }

        $insOrder = $db->prepare(
            "INSERT INTO `shop_orders`
            (`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`, `office_id`)
            VALUES (0, 0, ?, 1, ?, 1, 1, '{}', '', 'DEMO-SALE', ?)"
        );
        $insItem = $db->prepare(
            "INSERT INTO `shop_orders_items`
            (`order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`,
             `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`, `t2_time_to_exe`, `t2_time_to_exe_guaranteed`,
             `t2_storage`, `t2_min_order`, `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`, `sao_state`, `sao_robot`, `t2_json_params`)
            VALUES (?, 2, ?, ?, 0, ?, 'DEMO', ?, ?, ?, 10, 1, 1, '', 1, 100, 0, ?, ?, ?, 0, 0, '')"
        );

        mt_srand(20260601);
        $orderCount = 0;
        $lineCount = 0;
        $revenue = 0.0;
        for ($m = $months - 1; $m >= 0; $m--) {
            $monthTs = strtotime('-' . $m . ' months');
            $year = (int) date('Y', $monthTs);
            $mon = (int) date('n', $monthTs);
            // a gentle upward trend toward recent months
            $ordersThisMonth = 8 + (int) round(($months - $m) * 1.5) + mt_rand(0, 4);
            for ($o = 0; $o < $ordersThisMonth; $o++) {
                $day = min(28, 1 + mt_rand(0, 27));
                $ts = mktime(mt_rand(9, 18), mt_rand(0, 59), 0, $mon, $day, $year);
                $insOrder->execute(array($ts, $st['order'], $officeId));
                $orderId = (int) $db->lastInsertId();
                $nLines = 1 + mt_rand(0, 2);
                for ($l = 0; $l < $nLines; $l++) {
                    $it = $items[mt_rand(0, count($items) - 1)];
                    $cost = $it['cost'];
                    $markup = 1.2 + (mt_rand(0, 50) / 100.0); // 1.2–1.7×
                    $price = round($cost * $markup, 2);
                    $qty = 1 + mt_rand(0, 5);
                    $article = $it['sku'] !== '' ? $it['sku'] : ('ITEM-' . $l);
                    $name = $it['name'] !== '' ? $it['name'] : $article;
                    $insItem->execute(array(
                        $orderId, $price, $qty, $st['item'],
                        $article, $article, mb_substr($name, 0, 255),
                        round($cost, 2), $officeId, $storageId,
                    ));
                    $lineCount++;
                    $revenue += $price * $qty;
                }
                $orderCount++;
            }
        }
        mt_srand();
        return array('orders' => $orderCount, 'lines' => $lineCount, 'revenue' => round($revenue, 2));
    }
}
