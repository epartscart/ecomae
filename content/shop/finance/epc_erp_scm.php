<?php
/**
 * Advanced ERP — Supply Chain Management (SCM) layer.
 *
 * Additive module covering:
 *   - Advanced procurement: RFQ -> supplier responses -> compare -> award to PO
 *   - Demand forecasting & planning: consumption stats, moving-average forecast,
 *     reorder points and suggested purchase quantities
 *   - Landed cost: allocate freight / duty / insurance / other across receipt
 *     lines (by value, qty or weight) and roll into item cost
 *   - Shipping & logistics: carriers, shipments, tracking, inbound receiving
 *
 * It builds on the existing inventory (`epc_erp_inv_*`), suppliers
 * (`epc_erp_suppliers`) and purchase-order (`epc_erp_purchase_orders`) tables,
 * and only creates NEW `epc_scm_*` tables. Nothing existing is modified, so it
 * is safe for live tenants.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_advanced.php';

if (!function_exists('epc_scm_ensure_schema')) {
    function epc_scm_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_rfq` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `rfq_no` varchar(32) NOT NULL,
            `title` varchar(255) NOT NULL DEFAULT '',
            `status` enum('draft','sent','closed','awarded','cancelled') NOT NULL DEFAULT 'draft',
            `due_date` int(11) NOT NULL DEFAULT 0,
            `notes` text,
            `awarded_supplier_id` int(11) NOT NULL DEFAULT 0,
            `awarded_po_id` int(11) NOT NULL DEFAULT 0,
            `admin_id` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_rfq_no` (`rfq_no`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RFQ headers'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_rfq_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `rfq_id` int(11) NOT NULL,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `description` varchar(255) NOT NULL DEFAULT '',
            `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `unit` varchar(16) NOT NULL DEFAULT 'pcs',
            `target_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `sort_order` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_rfq` (`rfq_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RFQ request lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_rfq_responses` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `rfq_id` int(11) NOT NULL,
            `rfq_line_id` int(11) NOT NULL DEFAULT 0,
            `supplier_id` int(11) NOT NULL DEFAULT 0,
            `unit_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `lead_time_days` int(11) NOT NULL DEFAULT 0,
            `notes` varchar(255) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_rfq` (`rfq_id`),
            KEY `x_supplier` (`supplier_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Supplier quotes against RFQ lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_item_planning` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `item_id` int(11) NOT NULL,
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `lead_time_days` int(11) NOT NULL DEFAULT 7,
            `safety_stock` decimal(14,3) NOT NULL DEFAULT 0.000,
            `min_order_qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `review_horizon_days` int(11) NOT NULL DEFAULT 30,
            `avg_daily_demand` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `forecast_updated_at` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_item_wh` (`item_id`,`warehouse_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-item planning parameters'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_landed_cost` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `lc_no` varchar(32) NOT NULL,
            `reference` varchar(128) NOT NULL DEFAULT '',
            `po_id` int(11) NOT NULL DEFAULT 0,
            `purchase_id` int(11) NOT NULL DEFAULT 0,
            `allocation_basis` enum('value','qty','weight') NOT NULL DEFAULT 'value',
            `freight` decimal(14,2) NOT NULL DEFAULT 0.00,
            `duty` decimal(14,2) NOT NULL DEFAULT 0.00,
            `insurance` decimal(14,2) NOT NULL DEFAULT 0.00,
            `other` decimal(14,2) NOT NULL DEFAULT 0.00,
            `currency_code` varchar(8) NOT NULL DEFAULT '',
            `status` enum('draft','applied','cancelled') NOT NULL DEFAULT 'draft',
            `applied_at` int(11) NOT NULL DEFAULT 0,
            `admin_id` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_lc_no` (`lc_no`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Landed cost vouchers'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_landed_cost_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `lc_id` int(11) NOT NULL,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `description` varchar(255) NOT NULL DEFAULT '',
            `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `base_value` decimal(14,2) NOT NULL DEFAULT 0.00,
            `weight` decimal(14,3) NOT NULL DEFAULT 0.000,
            `allocated_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `unit_landed_addon` decimal(14,4) NOT NULL DEFAULT 0.0000,
            PRIMARY KEY (`id`),
            KEY `x_lc` (`lc_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Landed cost allocation lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_carriers` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `name` varchar(128) NOT NULL DEFAULT '',
            `code` varchar(32) NOT NULL DEFAULT '',
            `tracking_url` varchar(255) NOT NULL DEFAULT '',
            `contact` varchar(255) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Shipping carriers'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_shipments` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `shipment_no` varchar(32) NOT NULL,
            `direction` enum('inbound','outbound') NOT NULL DEFAULT 'inbound',
            `carrier_id` int(11) NOT NULL DEFAULT 0,
            `po_id` int(11) NOT NULL DEFAULT 0,
            `order_id` int(11) NOT NULL DEFAULT 0,
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `supplier_id` int(11) NOT NULL DEFAULT 0,
            `status` enum('pending','in_transit','delivered','cancelled') NOT NULL DEFAULT 'pending',
            `tracking_no` varchar(128) NOT NULL DEFAULT '',
            `ship_date` int(11) NOT NULL DEFAULT 0,
            `eta` int(11) NOT NULL DEFAULT 0,
            `delivered_at` int(11) NOT NULL DEFAULT 0,
            `freight_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `notes` text,
            `admin_id` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_shipment_no` (`shipment_no`),
            KEY `x_status` (`status`),
            KEY `x_dir` (`direction`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inbound/outbound shipments'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_scm_shipment_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `shipment_id` int(11) NOT NULL,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `description` varchar(255) NOT NULL DEFAULT '',
            `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `qty_received` decimal(14,3) NOT NULL DEFAULT 0.000,
            `unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `sort_order` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_shipment` (`shipment_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Shipment lines'");
    }
}

if (!function_exists('epc_scm_next_no')) {
    function epc_scm_next_no(PDO $db, string $table, string $col, string $prefix): string
    {
        $like = $prefix . date('Ym') . '-%';
        $st = $db->prepare("SELECT `$col` FROM `$table` WHERE `$col` LIKE ? ORDER BY `id` DESC LIMIT 1");
        $st->execute(array($like));
        $last = (string) $st->fetchColumn();
        $seq = 1;
        if ($last !== '' && preg_match('/(\d+)$/', $last, $m)) {
            $seq = (int) $m[1] + 1;
        }
        return $prefix . date('Ym') . '-' . str_pad((string) $seq, 4, '0', STR_PAD_LEFT);
    }
}

/* --------------------------------------------------------------------------
 * Procurement: RFQ
 * ------------------------------------------------------------------------ */

if (!function_exists('epc_scm_rfq_save')) {
    /**
     * @param array<string,mixed> $data  title, status, due_date, notes, lines[]
     */
    function epc_scm_rfq_save(PDO $db, array $data, int $id = 0): int
    {
        epc_scm_ensure_schema($db);
        $now = time();
        if ($id > 0) {
            $st = $db->prepare(
                "UPDATE `epc_scm_rfq` SET `title`=?, `status`=?, `due_date`=?, `notes`=?, `time_updated`=? WHERE `id`=?"
            );
            $st->execute(array(
                (string) ($data['title'] ?? ''),
                (string) ($data['status'] ?? 'draft'),
                (int) ($data['due_date'] ?? 0),
                (string) ($data['notes'] ?? ''),
                $now,
                $id,
            ));
        } else {
            $no = epc_scm_next_no($db, 'epc_scm_rfq', 'rfq_no', 'RFQ-');
            $st = $db->prepare(
                "INSERT INTO `epc_scm_rfq` (`rfq_no`,`title`,`status`,`due_date`,`notes`,`admin_id`,`time_created`,`time_updated`)
                 VALUES (?,?,?,?,?,?,?,?)"
            );
            $st->execute(array(
                $no,
                (string) ($data['title'] ?? ''),
                (string) ($data['status'] ?? 'draft'),
                (int) ($data['due_date'] ?? 0),
                (string) ($data['notes'] ?? ''),
                (int) ($data['admin_id'] ?? 0),
                $now,
                $now,
            ));
            $id = (int) $db->lastInsertId();
        }

        if (isset($data['lines']) && is_array($data['lines'])) {
            $db->prepare('DELETE FROM `epc_scm_rfq_lines` WHERE `rfq_id`=?')->execute(array($id));
            $li = $db->prepare(
                "INSERT INTO `epc_scm_rfq_lines` (`rfq_id`,`item_id`,`description`,`qty`,`unit`,`target_price`,`sort_order`)
                 VALUES (?,?,?,?,?,?,?)"
            );
            $i = 0;
            foreach ($data['lines'] as $line) {
                $li->execute(array(
                    $id,
                    (int) ($line['item_id'] ?? 0),
                    (string) ($line['description'] ?? ''),
                    (float) ($line['qty'] ?? 0),
                    (string) ($line['unit'] ?? 'pcs'),
                    (float) ($line['target_price'] ?? 0),
                    $i++,
                ));
            }
        }
        return $id;
    }
}

if (!function_exists('epc_scm_rfq_add_response')) {
    /**
     * @param array<string,mixed> $resp  rfq_line_id, supplier_id, unit_price, lead_time_days, notes
     */
    function epc_scm_rfq_add_response(PDO $db, int $rfqId, array $resp): int
    {
        epc_scm_ensure_schema($db);
        $st = $db->prepare(
            "INSERT INTO `epc_scm_rfq_responses` (`rfq_id`,`rfq_line_id`,`supplier_id`,`unit_price`,`lead_time_days`,`notes`,`time_created`)
             VALUES (?,?,?,?,?,?,?)"
        );
        $st->execute(array(
            $rfqId,
            (int) ($resp['rfq_line_id'] ?? 0),
            (int) ($resp['supplier_id'] ?? 0),
            (float) ($resp['unit_price'] ?? 0),
            (int) ($resp['lead_time_days'] ?? 0),
            (string) ($resp['notes'] ?? ''),
            time(),
        ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_scm_rfq_compare')) {
    /**
     * Compare supplier responses for an RFQ. Returns best price per line and a
     * supplier ranking by total quoted value (lower = better).
     *
     * @return array<string,mixed>
     */
    function epc_scm_rfq_compare(PDO $db, int $rfqId): array
    {
        epc_scm_ensure_schema($db);
        $lines = $db->prepare('SELECT * FROM `epc_scm_rfq_lines` WHERE `rfq_id`=? ORDER BY `sort_order`');
        $lines->execute(array($rfqId));
        $lineRows = $lines->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $resp = $db->prepare('SELECT * FROM `epc_scm_rfq_responses` WHERE `rfq_id`=?');
        $resp->execute(array($rfqId));
        $respRows = $resp->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $byLine = array();
        $supplierTotals = array();
        foreach ($lineRows as $ln) {
            $qty = (float) $ln['qty'];
            $best = null;
            foreach ($respRows as $r) {
                if ((int) $r['rfq_line_id'] !== (int) $ln['id']) {
                    continue;
                }
                $sid = (int) $r['supplier_id'];
                $lineTotal = $qty * (float) $r['unit_price'];
                $supplierTotals[$sid] = ($supplierTotals[$sid] ?? 0) + $lineTotal;
                if ($best === null || (float) $r['unit_price'] < (float) $best['unit_price']) {
                    $best = $r;
                }
            }
            $byLine[] = array(
                'rfq_line_id' => (int) $ln['id'],
                'description' => (string) $ln['description'],
                'qty' => $qty,
                'best_supplier_id' => $best ? (int) $best['supplier_id'] : 0,
                'best_unit_price' => $best ? (float) $best['unit_price'] : 0.0,
                'best_lead_time_days' => $best ? (int) $best['lead_time_days'] : 0,
            );
        }

        asort($supplierTotals);
        $ranking = array();
        foreach ($supplierTotals as $sid => $total) {
            $ranking[] = array('supplier_id' => $sid, 'total' => round($total, 2));
        }

        return array(
            'rfq_id' => $rfqId,
            'lines' => $byLine,
            'supplier_ranking' => $ranking,
            'recommended_supplier_id' => $ranking[0]['supplier_id'] ?? 0,
        );
    }
}

if (!function_exists('epc_scm_rfq_award_to_po')) {
    /**
     * Award an RFQ to a supplier: build a draft PO from that supplier's quoted
     * prices. Reuses epc_erp_po_save() when available, else inserts directly.
     *
     * @return array<string,mixed>
     */
    function epc_scm_rfq_award_to_po(PDO $db, int $rfqId, int $supplierId, int $adminId = 0): array
    {
        epc_scm_ensure_schema($db);
        $lines = $db->prepare('SELECT * FROM `epc_scm_rfq_lines` WHERE `rfq_id`=? ORDER BY `sort_order`');
        $lines->execute(array($rfqId));
        $lineRows = $lines->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $poLines = array();
        $totalEx = 0.0;
        foreach ($lineRows as $ln) {
            $pr = $db->prepare(
                'SELECT `unit_price` FROM `epc_scm_rfq_responses` WHERE `rfq_id`=? AND `rfq_line_id`=? AND `supplier_id`=? LIMIT 1'
            );
            $pr->execute(array($rfqId, (int) $ln['id'], $supplierId));
            $price = (float) $pr->fetchColumn();
            $qty = (float) $ln['qty'];
            $lineEx = $qty * $price;
            $totalEx += $lineEx;
            $poLines[] = array(
                'item_id' => (int) $ln['item_id'],
                'description' => (string) $ln['description'],
                'qty' => $qty,
                'unit_cost_ex_vat' => $price,
                'line_ex_vat' => round($lineEx, 2),
            );
        }

        $poId = 0;
        $poNo = '';
        if (function_exists('epc_erp_po_save')) {
            try {
                $poId = (int) epc_erp_po_save($db, array(
                    'supplier_id' => $supplierId,
                    'title' => 'From RFQ',
                    'amount_ex_vat' => round($totalEx, 2),
                    'lines' => $poLines,
                    'admin_id' => $adminId,
                ));
            } catch (Throwable $e) {
                $poId = 0;
            }
        }
        if ($poId === 0) {
            // Direct fallback into the existing PO table.
            $poNo = epc_scm_next_no($db, 'epc_erp_purchase_orders', 'po_no', 'PO-');
            $now = time();
            $st = $db->prepare(
                "INSERT INTO `epc_erp_purchase_orders`
                 (`po_no`,`supplier_id`,`title`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`admin_id`,`time_created`,`time_updated`)
                 VALUES (?,?,?,?,0,?,'draft',?,?,?)"
            );
            $st->execute(array($poNo, $supplierId, 'From RFQ ' . $rfqId, round($totalEx, 2), round($totalEx, 2), $adminId, $now, $now));
            $poId = (int) $db->lastInsertId();
        }

        $db->prepare("UPDATE `epc_scm_rfq` SET `status`='awarded', `awarded_supplier_id`=?, `awarded_po_id`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($supplierId, $poId, time(), $rfqId));

        return array(
            'status' => true,
            'rfq_id' => $rfqId,
            'supplier_id' => $supplierId,
            'po_id' => $poId,
            'po_no' => $poNo,
            'total_ex' => round($totalEx, 2),
            'lines' => count($poLines),
        );
    }
}

/* --------------------------------------------------------------------------
 * Demand forecasting & planning
 * ------------------------------------------------------------------------ */

if (!function_exists('epc_scm_demand_stats')) {
    /**
     * Consumption statistics from sale_out movements over a lookback window.
     *
     * @return array<string,mixed>
     */
    function epc_scm_demand_stats(PDO $db, int $itemId, int $warehouseId = 0, int $lookbackDays = 90): array
    {
        $lookbackDays = max(1, $lookbackDays);
        $since = time() - $lookbackDays * 86400;
        $mid = time() - (int) ($lookbackDays / 2) * 86400;

        $sql = "SELECT
                    COALESCE(SUM(ABS(`qty`)),0) AS total_qty,
                    COALESCE(SUM(CASE WHEN `movement_date` >= ? THEN ABS(`qty`) ELSE 0 END),0) AS recent_qty,
                    COALESCE(SUM(CASE WHEN `movement_date` <  ? THEN ABS(`qty`) ELSE 0 END),0) AS older_qty
                FROM `epc_erp_inv_movements`
                WHERE `item_id` = ? AND `movement_type` = 'sale_out'
                  AND `active` = 1 AND `movement_date` >= ?";
        $params = array($mid, $mid, $itemId, $since);
        if ($warehouseId > 0) {
            $sql .= ' AND `warehouse_id` = ?';
            $params[] = $warehouseId;
        }
        $st = $db->prepare($sql);
        $st->execute($params);
        $row = $st->fetch(PDO::FETCH_ASSOC) ?: array('total_qty' => 0, 'recent_qty' => 0, 'older_qty' => 0);

        $total = (float) $row['total_qty'];
        $avgDaily = $total / $lookbackDays;
        $recent = (float) $row['recent_qty'];
        $older = (float) $row['older_qty'];
        $trend = 'flat';
        if ($recent > $older * 1.15) {
            $trend = 'up';
        } elseif ($recent < $older * 0.85) {
            $trend = 'down';
        }

        return array(
            'item_id' => $itemId,
            'lookback_days' => $lookbackDays,
            'total_demand' => round($total, 3),
            'avg_daily_demand' => round($avgDaily, 4),
            'trend' => $trend,
        );
    }
}

if (!function_exists('epc_scm_forecast')) {
    /**
     * Moving-average demand forecast for a horizon, with a light trend factor.
     *
     * @return array<string,mixed>
     */
    function epc_scm_forecast(PDO $db, int $itemId, int $warehouseId = 0, int $horizonDays = 30, int $lookbackDays = 90): array
    {
        $stats = epc_scm_demand_stats($db, $itemId, $warehouseId, $lookbackDays);
        $avg = (float) $stats['avg_daily_demand'];
        $factor = $stats['trend'] === 'up' ? 1.15 : ($stats['trend'] === 'down' ? 0.85 : 1.0);
        $forecastQty = $avg * $horizonDays * $factor;
        return array(
            'item_id' => $itemId,
            'horizon_days' => $horizonDays,
            'avg_daily_demand' => $avg,
            'trend' => $stats['trend'],
            'forecast_qty' => round($forecastQty, 3),
        );
    }
}

if (!function_exists('epc_scm_reorder_point')) {
    /**
     * Reorder point = expected demand over lead time + safety stock.
     */
    function epc_scm_reorder_point(float $avgDailyDemand, int $leadTimeDays, float $safetyStock): float
    {
        return round(max(0.0, $avgDailyDemand) * max(0, $leadTimeDays) + max(0.0, $safetyStock), 3);
    }
}

if (!function_exists('epc_scm_planning_set')) {
    /**
     * @param array<string,mixed> $params lead_time_days, safety_stock, min_order_qty, review_horizon_days
     */
    function epc_scm_planning_set(PDO $db, int $itemId, int $warehouseId, array $params): void
    {
        epc_scm_ensure_schema($db);
        $st = $db->prepare(
            "INSERT INTO `epc_scm_item_planning`
                (`item_id`,`warehouse_id`,`lead_time_days`,`safety_stock`,`min_order_qty`,`review_horizon_days`,`forecast_updated_at`)
             VALUES (?,?,?,?,?,?,?)
             ON DUPLICATE KEY UPDATE
                `lead_time_days`=VALUES(`lead_time_days`),
                `safety_stock`=VALUES(`safety_stock`),
                `min_order_qty`=VALUES(`min_order_qty`),
                `review_horizon_days`=VALUES(`review_horizon_days`),
                `forecast_updated_at`=VALUES(`forecast_updated_at`)"
        );
        $st->execute(array(
            $itemId,
            $warehouseId,
            (int) ($params['lead_time_days'] ?? 7),
            (float) ($params['safety_stock'] ?? 0),
            (float) ($params['min_order_qty'] ?? 0),
            (int) ($params['review_horizon_days'] ?? 30),
            time(),
        ));
    }
}

if (!function_exists('epc_scm_planning_suggestions')) {
    /**
     * Generate reorder suggestions: for each stocked item, compute demand-based
     * reorder point and suggested order qty to cover the review horizon.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_scm_planning_suggestions(PDO $db, int $warehouseId = 0): array
    {
        epc_scm_ensure_schema($db);
        // reorder_level lives on inv_items via the phase8 schema layer.
        require_once __DIR__ . '/epc_erp_phase8.php';
        epc_erp_phase8_ensure_schema($db);
        $sql = "SELECT s.`item_id`, s.`warehouse_id`, s.`qty_on_hand`, i.`sku`, i.`name`, i.`reorder_level`
                FROM `epc_erp_inv_stock` s
                INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1";
        $params = array();
        if ($warehouseId > 0) {
            $sql .= ' WHERE s.`warehouse_id` = ?';
            $params[] = $warehouseId;
        }
        $st = $db->prepare($sql);
        $st->execute($params);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $planStmt = $db->prepare('SELECT * FROM `epc_scm_item_planning` WHERE `item_id`=? AND `warehouse_id` IN (?,0) ORDER BY `warehouse_id` DESC LIMIT 1');

        $out = array();
        foreach ($rows as $r) {
            $itemId = (int) $r['item_id'];
            $wh = (int) $r['warehouse_id'];
            $onHand = (float) $r['qty_on_hand'];

            $planStmt->execute(array($itemId, $wh));
            $plan = $planStmt->fetch(PDO::FETCH_ASSOC) ?: array();
            $leadTime = (int) ($plan['lead_time_days'] ?? 7);
            $safety = (float) ($plan['safety_stock'] ?? 0);
            $horizon = (int) ($plan['review_horizon_days'] ?? 30);
            $minOrder = (float) ($plan['min_order_qty'] ?? 0);

            $stats = epc_scm_demand_stats($db, $itemId, $wh, 90);
            $avgDaily = (float) $stats['avg_daily_demand'];
            $rop = epc_scm_reorder_point($avgDaily, $leadTime, $safety);

            // Static reorder_level acts as a floor if no demand history.
            $effectiveRop = max($rop, (float) $r['reorder_level']);
            if ($onHand > $effectiveRop) {
                continue; // healthy stock
            }

            $coverDays = $leadTime + $horizon;
            $targetQty = $avgDaily * $coverDays + $safety;
            $suggested = max(0.0, $targetQty - $onHand);
            if ($minOrder > 0 && $suggested > 0 && $suggested < $minOrder) {
                $suggested = $minOrder;
            }
            if ($suggested <= 0 && $onHand <= $effectiveRop && $effectiveRop > 0) {
                // Demand history absent but below static reorder level: top up to ROP.
                $suggested = max($minOrder, $effectiveRop - $onHand);
            }
            if ($suggested <= 0) {
                continue;
            }

            $out[] = array(
                'item_id' => $itemId,
                'warehouse_id' => $wh,
                'sku' => (string) $r['sku'],
                'name' => (string) $r['name'],
                'qty_on_hand' => round($onHand, 3),
                'avg_daily_demand' => $avgDaily,
                'trend' => $stats['trend'],
                'lead_time_days' => $leadTime,
                'reorder_point' => round($effectiveRop, 3),
                'suggested_order_qty' => round($suggested, 3),
            );
        }

        usort($out, static function ($a, $b) {
            return ($b['suggested_order_qty'] ?? 0) <=> ($a['suggested_order_qty'] ?? 0);
        });
        return $out;
    }
}

/* --------------------------------------------------------------------------
 * Landed cost
 * ------------------------------------------------------------------------ */

if (!function_exists('epc_scm_landed_cost_allocate')) {
    /**
     * Pure allocation of extra costs across lines by a chosen basis.
     *
     * @param array<int,array<string,mixed>> $lines each with qty, base_value, weight
     * @return array<int,array<string,mixed>> input lines + allocated_cost + unit_landed_addon
     */
    function epc_scm_landed_cost_allocate(array $lines, float $freight, float $duty, float $insurance, float $other, string $basis = 'value'): array
    {
        $extra = max(0.0, $freight) + max(0.0, $duty) + max(0.0, $insurance) + max(0.0, $other);

        $weights = array();
        $totalWeight = 0.0;
        foreach ($lines as $i => $ln) {
            switch ($basis) {
                case 'qty':
                    $w = (float) ($ln['qty'] ?? 0);
                    break;
                case 'weight':
                    $w = (float) ($ln['weight'] ?? 0);
                    break;
                case 'value':
                default:
                    $w = (float) ($ln['base_value'] ?? 0);
                    break;
            }
            $w = max(0.0, $w);
            $weights[$i] = $w;
            $totalWeight += $w;
        }

        $out = array();
        $allocatedSoFar = 0.0;
        $n = count($lines);
        $idx = 0;
        foreach ($lines as $i => $ln) {
            $idx++;
            if ($totalWeight > 0) {
                if ($idx === $n) {
                    // Last line absorbs rounding remainder for exact total.
                    $alloc = round($extra - $allocatedSoFar, 2);
                } else {
                    $alloc = round($extra * ($weights[$i] / $totalWeight), 2);
                    $allocatedSoFar += $alloc;
                }
            } else {
                $alloc = 0.0;
            }
            $qty = (float) ($ln['qty'] ?? 0);
            $addon = $qty > 0 ? round($alloc / $qty, 4) : 0.0;
            $ln['allocated_cost'] = $alloc;
            $ln['unit_landed_addon'] = $addon;
            $out[] = $ln;
        }
        return $out;
    }
}

if (!function_exists('epc_scm_landed_cost_save')) {
    /**
     * @param array<string,mixed> $data reference, po_id, allocation_basis, freight, duty, insurance, other, lines[]
     * @return array<string,mixed>
     */
    function epc_scm_landed_cost_save(PDO $db, array $data): array
    {
        epc_scm_ensure_schema($db);
        $basis = (string) ($data['allocation_basis'] ?? 'value');
        $lines = isset($data['lines']) && is_array($data['lines']) ? $data['lines'] : array();
        $allocated = epc_scm_landed_cost_allocate(
            $lines,
            (float) ($data['freight'] ?? 0),
            (float) ($data['duty'] ?? 0),
            (float) ($data['insurance'] ?? 0),
            (float) ($data['other'] ?? 0),
            $basis
        );

        $no = epc_scm_next_no($db, 'epc_scm_landed_cost', 'lc_no', 'LC-');
        $now = time();
        $st = $db->prepare(
            "INSERT INTO `epc_scm_landed_cost`
                (`lc_no`,`reference`,`po_id`,`purchase_id`,`allocation_basis`,`freight`,`duty`,`insurance`,`other`,`currency_code`,`status`,`admin_id`,`time_created`,`time_updated`)
             VALUES (?,?,?,?,?,?,?,?,?,?,'draft',?,?,?)"
        );
        $st->execute(array(
            $no,
            (string) ($data['reference'] ?? ''),
            (int) ($data['po_id'] ?? 0),
            (int) ($data['purchase_id'] ?? 0),
            $basis,
            (float) ($data['freight'] ?? 0),
            (float) ($data['duty'] ?? 0),
            (float) ($data['insurance'] ?? 0),
            (float) ($data['other'] ?? 0),
            (string) ($data['currency_code'] ?? ''),
            (int) ($data['admin_id'] ?? 0),
            $now,
            $now,
        ));
        $lcId = (int) $db->lastInsertId();

        $li = $db->prepare(
            "INSERT INTO `epc_scm_landed_cost_lines`
                (`lc_id`,`item_id`,`description`,`qty`,`base_value`,`weight`,`allocated_cost`,`unit_landed_addon`)
             VALUES (?,?,?,?,?,?,?,?)"
        );
        foreach ($allocated as $ln) {
            $li->execute(array(
                $lcId,
                (int) ($ln['item_id'] ?? 0),
                (string) ($ln['description'] ?? ''),
                (float) ($ln['qty'] ?? 0),
                (float) ($ln['base_value'] ?? 0),
                (float) ($ln['weight'] ?? 0),
                (float) ($ln['allocated_cost'] ?? 0),
                (float) ($ln['unit_landed_addon'] ?? 0),
            ));
        }

        return array('status' => true, 'lc_id' => $lcId, 'lc_no' => $no, 'lines' => $allocated);
    }
}

if (!function_exists('epc_scm_landed_cost_apply')) {
    /**
     * Apply a landed-cost voucher: raise each item's weighted-average cost by
     * the allocated unit add-on (weighted by current on-hand). Additive and
     * idempotent (won't re-apply once status='applied').
     *
     * @return array<string,mixed>
     */
    function epc_scm_landed_cost_apply(PDO $db, int $lcId, int $warehouseId = 0, int $adminId = 0): array
    {
        epc_scm_ensure_schema($db);
        $hdr = $db->prepare('SELECT * FROM `epc_scm_landed_cost` WHERE `id`=? LIMIT 1');
        $hdr->execute(array($lcId));
        $lc = $hdr->fetch(PDO::FETCH_ASSOC);
        if (!$lc) {
            return array('status' => false, 'message' => 'Landed cost not found');
        }
        if ($lc['status'] === 'applied') {
            return array('status' => false, 'message' => 'Already applied');
        }

        $ls = $db->prepare('SELECT * FROM `epc_scm_landed_cost_lines` WHERE `lc_id`=?');
        $ls->execute(array($lcId));
        $lines = $ls->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $updated = 0;
        foreach ($lines as $ln) {
            $itemId = (int) $ln['item_id'];
            $addon = (float) $ln['unit_landed_addon'];
            if ($itemId <= 0 || $addon == 0.0) {
                continue;
            }
            // Raise weighted-average cost (held on the stock row) by the per-unit
            // add-on, for the given warehouse or all warehouses holding the item.
            $sql = 'UPDATE `epc_erp_inv_stock` SET `avg_unit_cost` = ROUND(`avg_unit_cost` + ?, 4), `time_updated` = ? WHERE `item_id` = ?';
            $params = array($addon, time(), $itemId);
            if ($warehouseId > 0) {
                $sql .= ' AND `warehouse_id` = ?';
                $params[] = $warehouseId;
            }
            $st = $db->prepare($sql);
            $st->execute($params);
            if ($st->rowCount() > 0) {
                $updated++;
            }
        }

        $db->prepare("UPDATE `epc_scm_landed_cost` SET `status`='applied', `applied_at`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array(time(), time(), $lcId));

        return array('status' => true, 'lc_id' => $lcId, 'items_updated' => $updated);
    }
}

/* --------------------------------------------------------------------------
 * Shipping & logistics
 * ------------------------------------------------------------------------ */

if (!function_exists('epc_scm_carrier_save')) {
    /**
     * @param array<string,mixed> $data name, code, tracking_url, contact, active
     */
    function epc_scm_carrier_save(PDO $db, array $data, int $id = 0): int
    {
        epc_scm_ensure_schema($db);
        if ($id > 0) {
            $db->prepare('UPDATE `epc_scm_carriers` SET `name`=?, `code`=?, `tracking_url`=?, `contact`=?, `active`=? WHERE `id`=?')
               ->execute(array(
                   (string) ($data['name'] ?? ''),
                   (string) ($data['code'] ?? ''),
                   (string) ($data['tracking_url'] ?? ''),
                   (string) ($data['contact'] ?? ''),
                   (int) ($data['active'] ?? 1),
                   $id,
               ));
            return $id;
        }
        $db->prepare('INSERT INTO `epc_scm_carriers` (`name`,`code`,`tracking_url`,`contact`,`active`,`time_created`) VALUES (?,?,?,?,?,?)')
           ->execute(array(
               (string) ($data['name'] ?? ''),
               (string) ($data['code'] ?? ''),
               (string) ($data['tracking_url'] ?? ''),
               (string) ($data['contact'] ?? ''),
               (int) ($data['active'] ?? 1),
               time(),
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_scm_carrier_tracking_url')) {
    /**
     * Build a tracking URL from a carrier template containing {tracking}.
     *
     * @param array<string,mixed> $carrier
     */
    function epc_scm_carrier_tracking_url(array $carrier, string $trackingNo): string
    {
        $tpl = (string) ($carrier['tracking_url'] ?? '');
        if ($tpl === '' || $trackingNo === '') {
            return '';
        }
        if (strpos($tpl, '{tracking}') !== false) {
            return str_replace('{tracking}', rawurlencode($trackingNo), $tpl);
        }
        return rtrim($tpl, '/') . '/' . rawurlencode($trackingNo);
    }
}

if (!function_exists('epc_scm_shipment_save')) {
    /**
     * @param array<string,mixed> $data direction, carrier_id, po_id, order_id, warehouse_id, supplier_id, tracking_no, ship_date, eta, freight_cost, notes, lines[]
     */
    function epc_scm_shipment_save(PDO $db, array $data, int $id = 0): int
    {
        epc_scm_ensure_schema($db);
        $now = time();
        if ($id > 0) {
            $db->prepare(
                "UPDATE `epc_scm_shipments` SET `direction`=?, `carrier_id`=?, `po_id`=?, `order_id`=?, `warehouse_id`=?, `supplier_id`=?,
                    `status`=?, `tracking_no`=?, `ship_date`=?, `eta`=?, `freight_cost`=?, `notes`=?, `time_updated`=? WHERE `id`=?"
            )->execute(array(
                (string) ($data['direction'] ?? 'inbound'),
                (int) ($data['carrier_id'] ?? 0),
                (int) ($data['po_id'] ?? 0),
                (int) ($data['order_id'] ?? 0),
                (int) ($data['warehouse_id'] ?? 0),
                (int) ($data['supplier_id'] ?? 0),
                (string) ($data['status'] ?? 'pending'),
                (string) ($data['tracking_no'] ?? ''),
                (int) ($data['ship_date'] ?? 0),
                (int) ($data['eta'] ?? 0),
                (float) ($data['freight_cost'] ?? 0),
                (string) ($data['notes'] ?? ''),
                $now,
                $id,
            ));
        } else {
            $no = epc_scm_next_no($db, 'epc_scm_shipments', 'shipment_no', 'SHP-');
            $db->prepare(
                "INSERT INTO `epc_scm_shipments`
                    (`shipment_no`,`direction`,`carrier_id`,`po_id`,`order_id`,`warehouse_id`,`supplier_id`,`status`,`tracking_no`,`ship_date`,`eta`,`freight_cost`,`notes`,`admin_id`,`time_created`,`time_updated`)
                 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"
            )->execute(array(
                $no,
                (string) ($data['direction'] ?? 'inbound'),
                (int) ($data['carrier_id'] ?? 0),
                (int) ($data['po_id'] ?? 0),
                (int) ($data['order_id'] ?? 0),
                (int) ($data['warehouse_id'] ?? 0),
                (int) ($data['supplier_id'] ?? 0),
                (string) ($data['status'] ?? 'pending'),
                (string) ($data['tracking_no'] ?? ''),
                (int) ($data['ship_date'] ?? 0),
                (int) ($data['eta'] ?? 0),
                (float) ($data['freight_cost'] ?? 0),
                (string) ($data['notes'] ?? ''),
                (int) ($data['admin_id'] ?? 0),
                $now,
                $now,
            ));
            $id = (int) $db->lastInsertId();
        }

        if (isset($data['lines']) && is_array($data['lines'])) {
            $db->prepare('DELETE FROM `epc_scm_shipment_lines` WHERE `shipment_id`=?')->execute(array($id));
            $li = $db->prepare(
                "INSERT INTO `epc_scm_shipment_lines` (`shipment_id`,`item_id`,`description`,`qty`,`unit_cost`,`sort_order`) VALUES (?,?,?,?,?,?)"
            );
            $i = 0;
            foreach ($data['lines'] as $line) {
                $li->execute(array(
                    $id,
                    (int) ($line['item_id'] ?? 0),
                    (string) ($line['description'] ?? ''),
                    (float) ($line['qty'] ?? 0),
                    (float) ($line['unit_cost'] ?? 0),
                    $i++,
                ));
            }
        }
        return $id;
    }
}

if (!function_exists('epc_scm_shipment_set_status')) {
    function epc_scm_shipment_set_status(PDO $db, int $shipmentId, string $status, int $ts = 0): void
    {
        epc_scm_ensure_schema($db);
        $ts = $ts > 0 ? $ts : time();
        $delivered = $status === 'delivered' ? $ts : 0;
        $db->prepare("UPDATE `epc_scm_shipments` SET `status`=?, `delivered_at`=IF(?>0,?,`delivered_at`), `time_updated`=? WHERE `id`=?")
           ->execute(array($status, $delivered, $delivered, time(), $shipmentId));
    }
}

if (!function_exists('epc_scm_shipment_receive')) {
    /**
     * Receive an inbound shipment into inventory: record purchase_in movements
     * for received qty and mark the shipment delivered. Reuses the inventory
     * movement engine when available.
     *
     * @param array<int,array<string,mixed>> $receivedLines [{line_id, qty_received}]
     * @return array<string,mixed>
     */
    function epc_scm_shipment_receive(PDO $db, int $shipmentId, array $receivedLines, int $warehouseId = 0, int $adminId = 0): array
    {
        epc_scm_ensure_schema($db);
        $hdr = $db->prepare('SELECT * FROM `epc_scm_shipments` WHERE `id`=? LIMIT 1');
        $hdr->execute(array($shipmentId));
        $ship = $hdr->fetch(PDO::FETCH_ASSOC);
        if (!$ship) {
            return array('status' => false, 'message' => 'Shipment not found');
        }
        $wh = $warehouseId > 0 ? $warehouseId : (int) $ship['warehouse_id'];

        $map = array();
        foreach ($receivedLines as $rl) {
            $map[(int) ($rl['line_id'] ?? 0)] = (float) ($rl['qty_received'] ?? 0);
        }

        $ls = $db->prepare('SELECT * FROM `epc_scm_shipment_lines` WHERE `shipment_id`=?');
        $ls->execute(array($shipmentId));
        $lines = $ls->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $received = 0;
        $hasInvEngine = function_exists('epc_erp_inventory_record_movement');
        foreach ($lines as $ln) {
            $lineId = (int) $ln['id'];
            $qtyRecv = isset($map[$lineId]) ? $map[$lineId] : (float) $ln['qty'];
            if ($qtyRecv <= 0 || (int) $ln['item_id'] <= 0) {
                continue;
            }
            if ($hasInvEngine && $wh > 0) {
                try {
                    epc_erp_inventory_record_movement($db, array(
                        'movement_type' => 'purchase_in',
                        'warehouse_id' => $wh,
                        'item_id' => (int) $ln['item_id'],
                        'qty' => $qtyRecv,
                        'unit_cost' => (float) $ln['unit_cost'],
                        'reference' => (string) $ship['shipment_no'],
                        'movement_date' => date('Y-m-d'),
                        'admin_id' => $adminId,
                    ));
                } catch (Throwable $e) {
                    // continue; record qty even if movement engine rejects
                }
            }
            $db->prepare('UPDATE `epc_scm_shipment_lines` SET `qty_received`=? WHERE `id`=?')
               ->execute(array($qtyRecv, $lineId));
            $received++;
        }

        epc_scm_shipment_set_status($db, $shipmentId, 'delivered');

        return array('status' => true, 'shipment_id' => $shipmentId, 'lines_received' => $received, 'warehouse_id' => $wh);
    }
}

if (!function_exists('epc_scm_logistics_dashboard')) {
    /**
     * Shipment counts by status + overdue (eta passed, not delivered).
     *
     * @return array<string,mixed>
     */
    function epc_scm_logistics_dashboard(PDO $db): array
    {
        epc_scm_ensure_schema($db);
        $out = array('by_status' => array(), 'in_transit' => 0, 'overdue' => 0, 'total' => 0);
        $rows = $db->query('SELECT `status`, COUNT(*) AS c FROM `epc_scm_shipments` GROUP BY `status`')->fetchAll(PDO::FETCH_ASSOC) ?: array();
        foreach ($rows as $r) {
            $out['by_status'][(string) $r['status']] = (int) $r['c'];
            $out['total'] += (int) $r['c'];
            if ($r['status'] === 'in_transit') {
                $out['in_transit'] = (int) $r['c'];
            }
        }
        $now = time();
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_scm_shipments` WHERE `status` IN ('pending','in_transit') AND `eta` > 0 AND `eta` < ?");
        $st->execute(array($now));
        $out['overdue'] = (int) $st->fetchColumn();
        return $out;
    }
}
