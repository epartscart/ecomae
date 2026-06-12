<?php
defined('_ASTEXE_') or die('No access');
/**
 * Order Planning — demand-driven replenishment recommendations and an item
 * inventory-analytics worksheet.
 *
 * For each stocked item × warehouse it derives, natively from the inventory
 * ledger:
 *   - demand history (sale_out movements) → average daily demand + variability
 *   - demand classification (smooth / erratic / intermittent / lumpy)
 *   - safety / buffer stock from a service-level Z-factor
 *   - order level (reorder point) = lead-time demand + safety stock
 *   - recommended order qty (ROQ) up to a target stock, honouring min/multiple
 *   - coverage at due (days of cover) and order value
 *   - ABC value class
 *
 * Planners confirm / reject recommendations; per-item planning parameters
 * (lead time, target service level, review period, min/multiple, supplier)
 * are editable and persisted.
 */

if (!function_exists('epc_opl_ensure_schema')) {
    function epc_opl_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_planning_params` (
            `item_id` int(11) NOT NULL,
            `warehouse_id` int(11) NOT NULL,
            `lead_time_days` int(11) NOT NULL DEFAULT 30,
            `target_service_level` decimal(5,2) NOT NULL DEFAULT 90.00,
            `review_period_days` int(11) NOT NULL DEFAULT 30,
            `min_order_qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `order_multiple` decimal(14,3) NOT NULL DEFAULT 0.000,
            `manual_buffer` decimal(14,3) NOT NULL DEFAULT 0.000,
            `supplier` varchar(160) NOT NULL DEFAULT '',
            `stocked` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`item_id`,`warehouse_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per item/warehouse planning parameters'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_order_recommendations` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `item_id` int(11) NOT NULL,
            `warehouse_id` int(11) NOT NULL,
            `roq` decimal(14,3) NOT NULL DEFAULT 0.000,
            `order_value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `status` varchar(12) NOT NULL DEFAULT 'pending',
            `supplier` varchar(160) NOT NULL DEFAULT '',
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_item_wh` (`item_id`,`warehouse_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Replenishment recommendation status'");
        // Links a confirmed recommendation to the draft PO raised from it, so
        // re-running "Create draft POs" never double-orders the same line.
        try {
            $col = $db->query("SHOW COLUMNS FROM `epc_erp_order_recommendations` LIKE 'ordered_po_id'")->fetch();
            if (!$col) {
                $db->exec("ALTER TABLE `epc_erp_order_recommendations` ADD `ordered_po_id` int(11) NOT NULL DEFAULT 0");
            }
        } catch (Exception $e) {
        }
    }
}

if (!function_exists('epc_opl_z_factor')) {
    /** Inverse-normal Z for a target service level percentage. */
    function epc_opl_z_factor(float $servicePct): float
    {
        $sl = $servicePct / 100.0;
        // Parallel arrays — PHP truncates float array keys to int, so a
        // float-keyed map would collapse to a single bucket.
        $levels = array(0.50, 0.75, 0.80, 0.85, 0.90, 0.925, 0.95, 0.975, 0.98, 0.99, 0.995, 0.999);
        $zs     = array(0.00, 0.67, 0.84, 1.04, 1.28, 1.44, 1.65, 1.96, 2.05, 2.33, 2.58, 3.09);
        $best = 1.28;
        $bestDiff = 1e9;
        foreach ($levels as $i => $p) {
            $d = abs($p - $sl);
            if ($d < $bestDiff) {
                $bestDiff = $d;
                $best = $zs[$i];
            }
        }
        return $best;
    }
}

if (!function_exists('epc_opl_demand_series')) {
    /**
     * Monthly sale-out demand for the last $months months (oldest→newest).
     *
     * @return array<int,float>
     */
    function epc_opl_demand_series(PDO $db, int $itemId, int $warehouseId, int $months = 12): array
    {
        $since = strtotime('-' . $months . ' months');
        $sql = "SELECT FROM_UNIXTIME(`movement_date`, '%Y-%m') AS ym, SUM(`qty`) AS q
                FROM `epc_erp_inv_movements`
                WHERE `movement_type` IN ('sale_out','transfer_out') AND `active`=1
                  AND `item_id`=? AND `movement_date`>=?";
        $args = array($itemId, $since);
        if ($warehouseId > 0) {
            $sql .= " AND `warehouse_id`=?";
            $args[] = $warehouseId;
        }
        $sql .= " GROUP BY ym";
        $st = $db->prepare($sql);
        $st->execute($args);
        $byMonth = array();
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $byMonth[(string) $r['ym']] = abs((float) $r['q']);
        }
        $series = array();
        for ($i = $months - 1; $i >= 0; $i--) {
            $key = date('Y-m', strtotime('-' . $i . ' months'));
            $series[] = isset($byMonth[$key]) ? $byMonth[$key] : 0.0;
        }
        return $series;
    }
}

if (!function_exists('epc_opl_classify_demand')) {
    /**
     * Syed-Shenstone-style classification using ADI and CV² of the demand sizes.
     *
     * @param array<int,float> $series
     * @return array{class:string,adi:float,cv2:float}
     */
    function epc_opl_classify_demand(array $series): array
    {
        $n = count($series);
        $nonzero = array_values(array_filter($series, static function ($v) {
            return $v > 0;
        }));
        $k = count($nonzero);
        if ($k === 0) {
            return array('class' => 'no demand', 'adi' => 0.0, 'cv2' => 0.0);
        }
        $adi = $n / $k; // average interval between demand occurrences
        $mean = array_sum($nonzero) / $k;
        $var = 0.0;
        foreach ($nonzero as $v) {
            $var += ($v - $mean) * ($v - $mean);
        }
        $var = $k > 1 ? $var / $k : 0.0;
        $cv2 = $mean > 0 ? ($var / ($mean * $mean)) : 0.0;
        if ($adi < 1.32 && $cv2 < 0.49) {
            $class = 'smooth';
        } elseif ($adi < 1.32 && $cv2 >= 0.49) {
            $class = 'erratic';
        } elseif ($adi >= 1.32 && $cv2 < 0.49) {
            $class = 'intermittent';
        } else {
            $class = 'lumpy';
        }
        return array('class' => $class, 'adi' => round($adi, 2), 'cv2' => round($cv2, 2));
    }
}

if (!function_exists('epc_opl_params_get')) {
    /** @return array<string,mixed> */
    function epc_opl_params_get(PDO $db, int $itemId, int $warehouseId): array
    {
        epc_opl_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_planning_params` WHERE `item_id`=? AND `warehouse_id`=?");
        $st->execute(array($itemId, $warehouseId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if ($row) {
            return $row;
        }
        return array(
            'item_id' => $itemId, 'warehouse_id' => $warehouseId,
            'lead_time_days' => 30, 'target_service_level' => 90.0, 'review_period_days' => 30,
            'min_order_qty' => 0.0, 'order_multiple' => 0.0, 'manual_buffer' => 0.0,
            'supplier' => '', 'stocked' => 1,
        );
    }
}

if (!function_exists('epc_opl_params_save')) {
    /** @param array<string,mixed> $data */
    function epc_opl_params_save(PDO $db, int $itemId, int $warehouseId, array $data): void
    {
        epc_opl_ensure_schema($db);
        $st = $db->prepare("INSERT INTO `epc_erp_planning_params`
            (`item_id`,`warehouse_id`,`lead_time_days`,`target_service_level`,`review_period_days`,`min_order_qty`,`order_multiple`,`manual_buffer`,`supplier`,`stocked`,`time_updated`)
            VALUES (?,?,?,?,?,?,?,?,?,?,?)
            ON DUPLICATE KEY UPDATE `lead_time_days`=VALUES(`lead_time_days`), `target_service_level`=VALUES(`target_service_level`),
            `review_period_days`=VALUES(`review_period_days`), `min_order_qty`=VALUES(`min_order_qty`), `order_multiple`=VALUES(`order_multiple`),
            `manual_buffer`=VALUES(`manual_buffer`), `supplier`=VALUES(`supplier`), `stocked`=VALUES(`stocked`), `time_updated`=VALUES(`time_updated`)");
        $st->execute(array(
            $itemId, $warehouseId,
            max(1, (int) ($data['lead_time_days'] ?? 30)),
            max(0, min(99.9, (float) ($data['target_service_level'] ?? 90))),
            max(1, (int) ($data['review_period_days'] ?? 30)),
            max(0, (float) ($data['min_order_qty'] ?? 0)),
            max(0, (float) ($data['order_multiple'] ?? 0)),
            max(0, (float) ($data['manual_buffer'] ?? 0)),
            (string) ($data['supplier'] ?? ''),
            !empty($data['stocked']) ? 1 : 0,
            time(),
        ));
    }
}

if (!function_exists('epc_opl_round_to_multiple')) {
    function epc_opl_round_to_multiple(float $qty, float $minQty, float $multiple): float
    {
        if ($qty <= 0) {
            return 0.0;
        }
        if ($minQty > 0 && $qty < $minQty) {
            $qty = $minQty;
        }
        if ($multiple > 0) {
            $qty = ceil($qty / $multiple) * $multiple;
        } else {
            $qty = ceil($qty);
        }
        return $qty;
    }
}

if (!function_exists('epc_opl_compute')) {
    /**
     * Full replenishment metrics for one item × warehouse stock row.
     *
     * @param array<string,mixed> $stockRow joined stock+item row (qty_on_hand, avg_unit_cost, sku, name, item_id, warehouse_id)
     * @return array<string,mixed>
     */
    function epc_opl_compute(PDO $db, array $stockRow): array
    {
        $itemId = (int) $stockRow['item_id'];
        $warehouseId = (int) $stockRow['warehouse_id'];
        $params = epc_opl_params_get($db, $itemId, $warehouseId);
        $series = epc_opl_demand_series($db, $itemId, $warehouseId, 12);
        $cls = epc_opl_classify_demand($series);

        $months = count($series);
        $totalDemand = array_sum($series);
        $monthlyMean = $months > 0 ? $totalDemand / $months : 0.0;
        $add = $monthlyMean / 30.4375; // average daily demand

        // monthly std dev (population) → daily sigma
        $var = 0.0;
        foreach ($series as $v) {
            $var += ($v - $monthlyMean) * ($v - $monthlyMean);
        }
        $var = $months > 0 ? $var / $months : 0.0;
        $sigmaMonthly = sqrt($var);
        $sigmaDaily = $sigmaMonthly / sqrt(30.4375);

        $lead = max(1, (int) $params['lead_time_days']);
        $sl = (float) $params['target_service_level'];
        $z = epc_opl_z_factor($sl);

        $leadTimeDemand = $add * $lead;
        $safety = $z * $sigmaDaily * sqrt($lead);
        if ((float) $params['manual_buffer'] > $safety) {
            $safety = (float) $params['manual_buffer'];
        }
        $orderLevel = $leadTimeDemand + $safety; // reorder point

        $review = max(1, (int) $params['review_period_days']);
        $targetStock = $orderLevel + ($add * $review); // order-up-to level

        $onHand = (float) $stockRow['qty_on_hand'];
        // stock in transit = open recommendations confirmed (proxy) — kept 0 unless tracked
        $effectiveStock = $onHand;

        $shortfall = max(0.0, $orderLevel - $effectiveStock);
        $excess = max(0.0, $effectiveStock - $targetStock);

        $rawRoq = 0.0;
        if ($effectiveStock <= $orderLevel) {
            $rawRoq = $targetStock - $effectiveStock;
        }
        $roq = epc_opl_round_to_multiple($rawRoq, (float) $params['min_order_qty'], (float) $params['order_multiple']);

        $unitCost = (float) $stockRow['avg_unit_cost'];
        $value = round($roq * $unitCost, 2);
        $coverageDays = $add > 0 ? round($effectiveStock / $add, 1) : null;
        $annualValue = round($monthlyMean * 12 * $unitCost, 2);

        return array(
            'item_id' => $itemId,
            'warehouse_id' => $warehouseId,
            'sku' => (string) ($stockRow['sku'] ?? ''),
            'name' => (string) ($stockRow['name'] ?? ''),
            'warehouse_name' => (string) ($stockRow['warehouse_name'] ?? ''),
            'unit' => (string) ($stockRow['unit'] ?? 'pcs'),
            'on_hand' => round($onHand, 3),
            'effective_stock' => round($effectiveStock, 3),
            'monthly_demand' => round($monthlyMean, 2),
            'avg_daily_demand' => round($add, 3),
            'forecast_next' => round($monthlyMean, 2),
            'sigma_monthly' => round($sigmaMonthly, 2),
            'lead_time_days' => $lead,
            'lead_time_demand' => round($leadTimeDemand, 2),
            'service_level' => $sl,
            'z' => $z,
            'safety_stock' => round($safety, 2),
            'order_level' => round($orderLevel, 2),
            'target_stock' => round($targetStock, 2),
            'shortfall' => round($shortfall, 2),
            'excess' => round($excess, 2),
            'roq' => round($roq, 3),
            'unit_cost' => round($unitCost, 4),
            'value' => $value,
            'coverage_days' => $coverageDays,
            'annual_value' => $annualValue,
            'demand_class' => $cls['class'],
            'adi' => $cls['adi'],
            'cv2' => $cls['cv2'],
            'supplier' => (string) $params['supplier'],
            'series' => $series,
        );
    }
}

if (!function_exists('epc_opl_recommendations')) {
    /**
     * Replenishment recommendation lines across stocked items.
     *
     * @param array{warehouse_id?:int,only_due?:bool,status?:string,search?:string} $filters
     * @return array<int,array<string,mixed>>
     */
    function epc_opl_recommendations(PDO $db, array $filters = array()): array
    {
        epc_opl_ensure_schema($db);
        if (!function_exists('epc_erp_inventory_stock_report')) {
            require_once __DIR__ . '/epc_erp_inventory.php';
        }
        $warehouseId = (int) ($filters['warehouse_id'] ?? 0);
        $stock = epc_erp_inventory_stock_report($db, $warehouseId);

        // aggregate batches into a single item×warehouse position
        $agg = array();
        foreach ($stock as $s) {
            $key = (int) $s['item_id'] . ':' . (int) $s['warehouse_id'];
            if (!isset($agg[$key])) {
                $agg[$key] = $s;
                $agg[$key]['qty_on_hand'] = 0.0;
                $agg[$key]['_costqty'] = 0.0;
                $agg[$key]['_cost'] = 0.0;
            }
            $q = (float) $s['qty_on_hand'];
            $agg[$key]['qty_on_hand'] += $q;
            $agg[$key]['_costqty'] += $q;
            $agg[$key]['_cost'] += $q * (float) $s['avg_unit_cost'];
        }

        // status map
        $statusMap = array();
        foreach ($db->query("SELECT item_id, warehouse_id, status FROM `epc_erp_order_recommendations`")->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $statusMap[(int) $r['item_id'] . ':' . (int) $r['warehouse_id']] = (string) $r['status'];
        }

        $search = strtolower((string) ($filters['search'] ?? ''));
        $onlyDue = !empty($filters['only_due']);
        $statusFilter = (string) ($filters['status'] ?? '');

        $out = array();
        foreach ($agg as $key => $s) {
            if ($s['_costqty'] > 0) {
                $s['avg_unit_cost'] = $s['_cost'] / $s['_costqty'];
            }
            if ($search !== '' && strpos(strtolower((string) $s['sku'] . ' ' . (string) $s['name']), $search) === false) {
                continue;
            }
            $m = epc_opl_compute($db, $s);
            $m['status'] = $statusMap[$key] ?? 'pending';
            if ($onlyDue && $m['roq'] <= 0) {
                continue;
            }
            if ($statusFilter !== '' && $m['status'] !== $statusFilter) {
                continue;
            }
            $out[] = $m;
        }
        // due lines first, then by order value desc
        usort($out, static function ($a, $b) {
            if (($a['roq'] > 0) !== ($b['roq'] > 0)) {
                return $a['roq'] > 0 ? -1 : 1;
            }
            return $b['value'] <=> $a['value'];
        });
        return $out;
    }
}

if (!function_exists('epc_opl_set_status')) {
    function epc_opl_set_status(PDO $db, int $itemId, int $warehouseId, string $status, float $roq = 0.0, float $value = 0.0, string $supplier = ''): void
    {
        epc_opl_ensure_schema($db);
        $allowed = array('pending', 'confirmed', 'rejected');
        if (!in_array($status, $allowed, true)) {
            throw new Exception('Invalid recommendation status');
        }
        $st = $db->prepare("INSERT INTO `epc_erp_order_recommendations`
            (`item_id`,`warehouse_id`,`roq`,`order_value`,`status`,`supplier`,`time_updated`)
            VALUES (?,?,?,?,?,?,?)
            ON DUPLICATE KEY UPDATE `roq`=VALUES(`roq`), `order_value`=VALUES(`order_value`), `status`=VALUES(`status`), `supplier`=VALUES(`supplier`), `time_updated`=VALUES(`time_updated`)");
        $st->execute(array($itemId, $warehouseId, $roq, $value, $status, $supplier, time()));
    }
}

if (!function_exists('epc_opl_confirm_all')) {
    /** Confirm every currently-due recommendation. Returns count confirmed. */
    function epc_opl_confirm_all(PDO $db, int $warehouseId = 0): int
    {
        $recs = epc_opl_recommendations($db, array('warehouse_id' => $warehouseId, 'only_due' => true, 'status' => 'pending'));
        $n = 0;
        foreach ($recs as $r) {
            epc_opl_set_status($db, (int) $r['item_id'], (int) $r['warehouse_id'], 'confirmed', (float) $r['roq'], (float) $r['value'], (string) $r['supplier']);
            $n++;
        }
        return $n;
    }
}

if (!function_exists('epc_opl_create_draft_pos')) {
    /**
     * Raise draft purchase orders from confirmed recommendation lines, grouped
     * by supplier. Each confirmed line is matched to a supplier (by the planning
     * supplier name; unmatched lines fall under one "supplier to assign" draft
     * against the first active supplier) and rolled into one draft PO per
     * supplier, ready for review in Purchasing before sending. Lines already
     * linked to a PO are skipped so the action is safely re-runnable.
     *
     * @return array{pos:int,lines:int,value:float,skipped:int,assign:int,message:string}
     */
    function epc_opl_create_draft_pos(PDO $db, int $warehouseId = 0): array
    {
        epc_opl_ensure_schema($db);
        require_once __DIR__ . '/epc_erp_extended.php';

        // active suppliers, name -> id (case/space-insensitive)
        $supById = array();
        $supByName = array();
        foreach ($db->query("SELECT `id`, `name` FROM `epc_erp_suppliers` WHERE `active` = 1 ORDER BY `id`")->fetchAll(PDO::FETCH_ASSOC) as $s) {
            $sid = (int) $s['id'];
            $supById[$sid] = (string) $s['name'];
            $supByName[strtolower(trim((string) $s['name']))] = $sid;
        }
        if (empty($supById)) {
            return array('pos' => 0, 'lines' => 0, 'value' => 0.0, 'skipped' => 0, 'assign' => 0,
                'message' => 'No suppliers defined yet — add a supplier first, then raise POs.');
        }
        $fallbackSid = (int) array_key_first($supById);

        // recommendations already linked to a PO (avoid double-ordering)
        $orderedMap = array();
        foreach ($db->query("SELECT `item_id`, `warehouse_id`, `ordered_po_id` FROM `epc_erp_order_recommendations`")->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $orderedMap[(int) $r['item_id'] . ':' . (int) $r['warehouse_id']] = (int) $r['ordered_po_id'];
        }

        $recs = epc_opl_recommendations($db, array('warehouse_id' => $warehouseId, 'status' => 'confirmed'));
        $groups = array();   // supplier_id => array of lines
        $assignCount = 0;
        foreach ($recs as $r) {
            if ((float) $r['roq'] <= 0) {
                continue;
            }
            $key = (int) $r['item_id'] . ':' . (int) $r['warehouse_id'];
            if (!empty($orderedMap[$key])) {
                continue; // already raised
            }
            $supName = strtolower(trim((string) $r['supplier']));
            if ($supName !== '' && isset($supByName[$supName])) {
                $sid = $supByName[$supName];
            } else {
                $sid = $fallbackSid;
                $assignCount++;
            }
            if (!isset($groups[$sid])) {
                $groups[$sid] = array();
            }
            $groups[$sid][] = $r;
        }

        $posCreated = 0;
        $linesTotal = 0;
        $valueTotal = 0.0;
        $today = date('Y-m-d');
        foreach ($groups as $sid => $lines) {
            $hasUnassigned = false;
            $sumEx = 0.0;
            $noteLines = array();
            foreach ($lines as $r) {
                $supName = strtolower(trim((string) $r['supplier']));
                if ($supName === '' || !isset($supByName[$supName])) {
                    $hasUnassigned = true;
                }
                $qty = (float) $r['roq'];
                $val = (float) $r['value'];
                $sumEx += $val;
                $noteLines[] = sprintf(
                    '%s %s — qty %s @ %s = %s%s',
                    (string) $r['sku'] !== '' ? (string) $r['sku'] : ('#' . (int) $r['item_id']),
                    (string) $r['name'],
                    rtrim(rtrim(number_format($qty, 3, '.', ''), '0'), '.'),
                    number_format((float) $r['unit_cost'], 2),
                    number_format($val, 2),
                    !empty($r['warehouse_name']) ? (' [' . (string) $r['warehouse_name'] . ']') : ''
                );
            }
            $supLabel = $supById[$sid] ?? ('Supplier #' . $sid);
            $title = ($hasUnassigned && count($groups) === 1 && $sid === $fallbackSid)
                ? ('Replenishment ' . $today . ' (supplier to assign)')
                : ('Replenishment ' . $today . ' — ' . $supLabel);
            $notes = "Auto-drafted from Order planning confirmed recommendations.\n";
            if ($hasUnassigned) {
                $notes .= "NOTE: some lines had no supplier set on the item worksheet — please verify/assign before sending.\n";
            }
            $notes .= "\n" . implode("\n", $noteLines);

            $poId = (int) epc_erp_po_save($db, array(
                'supplier_id' => $sid,
                'title' => $title,
                'amount_ex_vat' => round($sumEx, 2),
                'notes' => $notes,
                'status' => 'draft',
            ));
            if ($poId > 0) {
                $posCreated++;
                $upd = $db->prepare("UPDATE `epc_erp_order_recommendations` SET `ordered_po_id` = ? WHERE `item_id` = ? AND `warehouse_id` = ?");
                foreach ($lines as $r) {
                    $upd->execute(array($poId, (int) $r['item_id'], (int) $r['warehouse_id']));
                    $linesTotal++;
                    $valueTotal += (float) $r['value'];
                }
            }
        }

        if ($posCreated === 0) {
            return array('pos' => 0, 'lines' => 0, 'value' => 0.0, 'skipped' => 0, 'assign' => 0,
                'message' => 'No new confirmed lines to order — confirm recommendations first (or they are already on a draft PO).');
        }
        $msg = sprintf('Created %d draft PO%s covering %d line%s (%s AED). Review them in Purchasing → Purchase orders before sending.',
            $posCreated, $posCreated === 1 ? '' : 's', $linesTotal, $linesTotal === 1 ? '' : 's', number_format($valueTotal, 2));
        if ($assignCount > 0) {
            $msg .= sprintf(' %d line%s had no supplier set — grouped into a "supplier to assign" draft.', $assignCount, $assignCount === 1 ? '' : 's');
        }
        return array('pos' => $posCreated, 'lines' => $linesTotal, 'value' => round($valueTotal, 2),
            'skipped' => 0, 'assign' => $assignCount, 'message' => $msg);
    }
}

if (!function_exists('epc_opl_abc_xyz')) {
    /**
     * Classify each position: ABC by cumulative annual demand value (80/15/5),
     * XYZ by demand variability (CV²), and a recommended class service level.
     *
     * @param array<int,array<string,mixed>> $rows output of epc_opl_recommendations
     * @return array<int,array<string,mixed>> rows enriched with abc, xyz, class_service_level
     */
    function epc_opl_abc_xyz(array $rows): array
    {
        $total = 0.0;
        foreach ($rows as $r) {
            $total += (float) $r['annual_value'];
        }
        // sort copy by annual value desc to assign ABC
        usort($rows, static function ($a, $b) {
            return $b['annual_value'] <=> $a['annual_value'];
        });
        $cum = 0.0;
        foreach ($rows as &$r) {
            $cum += (float) $r['annual_value'];
            $pct = $total > 0 ? ($cum / $total) : 1.0;
            if ($pct <= 0.80) {
                $abc = 'A';
            } elseif ($pct <= 0.95) {
                $abc = 'B';
            } else {
                $abc = 'C';
            }
            if ((float) $r['annual_value'] <= 0) {
                $abc = 'C';
            }
            $cv2 = (float) $r['cv2'];
            if ($cv2 < 0.25) {
                $xyz = 'X';
            } elseif ($cv2 < 1.0) {
                $xyz = 'Y';
            } else {
                $xyz = 'Z';
            }
            // higher service level for high-value (A) and stable (X) items
            $base = ($abc === 'A') ? 95.0 : (($abc === 'B') ? 92.5 : 90.0);
            if ($xyz === 'Z') {
                $base -= 2.5; // erratic items: relax target to avoid over-stocking
            }
            $r['abc'] = $abc;
            $r['xyz'] = $xyz;
            $r['class'] = $abc . $xyz;
            $r['class_service_level'] = round(max(85.0, min(99.0, $base)), 1);
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_opl_redistribution')) {
    /**
     * Suggest stock transfers: move excess of an item in one warehouse to
     * cover a shortfall of the same item in another warehouse.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_opl_redistribution(PDO $db): array
    {
        $rows = epc_opl_recommendations($db, array());
        $byItem = array();
        foreach ($rows as $r) {
            $byItem[(int) $r['item_id']][] = $r;
        }
        $out = array();
        foreach ($byItem as $itemId => $positions) {
            if (count($positions) < 2) {
                continue;
            }
            $sources = array(); // excess
            $dests = array();    // shortfall
            foreach ($positions as $p) {
                if ((float) $p['excess'] > 0) {
                    $sources[] = $p;
                } elseif ((float) $p['shortfall'] > 0) {
                    $dests[] = $p;
                }
            }
            foreach ($dests as $d) {
                $need = (float) $d['shortfall'];
                foreach ($sources as &$s) {
                    if ($need <= 0) {
                        break;
                    }
                    $avail = (float) $s['excess'];
                    if ($avail <= 0) {
                        continue;
                    }
                    $move = min($need, $avail);
                    if ($move < 1) {
                        continue;
                    }
                    $out[] = array(
                        'item_id' => $itemId,
                        'sku' => $d['sku'],
                        'name' => $d['name'],
                        'from_wh' => $s['warehouse_name'],
                        'to_wh' => $d['warehouse_name'],
                        'qty' => round($move, 2),
                        'value' => round($move * (float) $d['unit_cost'], 2),
                        'from_excess' => round((float) $s['excess'], 2),
                        'to_shortfall' => round((float) $d['shortfall'], 2),
                    );
                    $s['excess'] = $avail - $move;
                    $need -= $move;
                }
                unset($s);
            }
        }
        usort($out, static function ($a, $b) {
            return $b['value'] <=> $a['value'];
        });
        return $out;
    }
}

if (!function_exists('epc_opl_exceptions')) {
    /**
     * Exception / alert list across positions.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_opl_exceptions(PDO $db, int $warehouseId = 0): array
    {
        $rows = epc_opl_recommendations($db, array('warehouse_id' => $warehouseId));
        $out = array();
        foreach ($rows as $r) {
            $alerts = array();
            if ($r['coverage_days'] !== null && $r['coverage_days'] < $r['lead_time_days'] && $r['monthly_demand'] > 0) {
                $alerts[] = array('type' => 'Stock-out risk', 'sev' => 'danger', 'detail' => 'Cover ' . $r['coverage_days'] . 'd < lead time ' . $r['lead_time_days'] . 'd');
            }
            if ($r['effective_stock'] < $r['safety_stock'] && $r['safety_stock'] > 0) {
                $alerts[] = array('type' => 'Below safety stock', 'sev' => 'warning', 'detail' => 'On hand ' . $r['effective_stock'] . ' < safety ' . $r['safety_stock']);
            }
            if ($r['monthly_demand'] <= 0 && $r['on_hand'] > 0) {
                $alerts[] = array('type' => 'Dead stock (no demand)', 'sev' => 'default', 'detail' => $r['on_hand'] . ' units, no demand in 12 months');
            }
            if ($r['excess'] > 0 && $r['monthly_demand'] > 0 && $r['excess'] > $r['monthly_demand']) {
                $alerts[] = array('type' => 'Excess stock', 'sev' => 'info', 'detail' => 'Excess ' . $r['excess'] . ' (> 1 month demand)');
            }
            foreach ($alerts as $a) {
                $out[] = array_merge(array(
                    'item_id' => $r['item_id'], 'warehouse_id' => $r['warehouse_id'],
                    'sku' => $r['sku'], 'name' => $r['name'], 'warehouse_name' => $r['warehouse_name'],
                ), $a);
            }
        }
        $sevRank = array('danger' => 0, 'warning' => 1, 'info' => 2, 'default' => 3);
        usort($out, static function ($a, $b) use ($sevRank) {
            return ($sevRank[$a['sev']] ?? 9) <=> ($sevRank[$b['sev']] ?? 9);
        });
        return $out;
    }
}

if (!function_exists('epc_opl_kpis')) {
    /**
     * Stock-analysis KPIs across positions.
     *
     * @return array<string,mixed>
     */
    function epc_opl_kpis(PDO $db, int $warehouseId = 0): array
    {
        $rows = epc_opl_abc_xyz(epc_opl_recommendations($db, array('warehouse_id' => $warehouseId)));
        $invValue = 0.0;
        $excessValue = 0.0;
        $orderValue = 0.0;
        $annualDemandValue = 0.0;
        $coverSum = 0.0;
        $coverN = 0;
        $risk = 0;
        $due = 0;
        $classCount = array();
        foreach ($rows as $r) {
            $invValue += (float) $r['on_hand'] * (float) $r['unit_cost'];
            $excessValue += (float) $r['excess'] * (float) $r['unit_cost'];
            $orderValue += (float) $r['value'];
            $annualDemandValue += (float) $r['annual_value'];
            if ($r['coverage_days'] !== null && $r['monthly_demand'] > 0) {
                $coverSum += (float) $r['coverage_days'];
                $coverN++;
            }
            if ($r['coverage_days'] !== null && $r['coverage_days'] < $r['lead_time_days'] && $r['monthly_demand'] > 0) {
                $risk++;
            }
            if ($r['roq'] > 0) {
                $due++;
            }
            $cls = (string) $r['abc'];
            $classCount[$cls] = ($classCount[$cls] ?? 0) + 1;
        }
        $cogs = $annualDemandValue; // annual demand at cost ≈ COGS
        $turns = $invValue > 0 ? ($cogs / $invValue) : 0.0;
        $activeLines = count($rows);
        $fillRate = $activeLines > 0 ? (100.0 * ($activeLines - $risk) / $activeLines) : 100.0;
        return array(
            'lines' => $activeLines,
            'inventory_value' => round($invValue, 2),
            'excess_value' => round($excessValue, 2),
            'suggested_order_value' => round($orderValue, 2),
            'annual_demand_value' => round($annualDemandValue, 2),
            'inventory_turns' => round($turns, 2),
            'avg_cover_days' => $coverN > 0 ? round($coverSum / $coverN, 1) : 0.0,
            'stockout_risk' => $risk,
            'due' => $due,
            'fill_rate' => round($fillRate, 1),
            'class_count' => $classCount,
        );
    }
}

if (!function_exists('epc_opl_seed_demo_demand')) {
    /**
     * Seed realistic 12-month sale-out demand history across stocked items so
     * the planning engine has data to forecast from. Idempotent: existing
     * DEMO-DEMAND rows are cleared first. Current on-hand is left untouched
     * (rows are written straight to the ledger, not via stock-decrementing
     * movements) so the recommendation mix stays meaningful.
     *
     * Demand pattern is assigned deterministically per item so re-runs are
     * stable: a slice of items get heavy demand (will fall below reorder
     * point → recommend an order), the rest get light/intermittent demand.
     *
     * @return array{items:int,movements:int}
     */
    function epc_opl_seed_demo_demand(PDO $db, int $months = 12, int $warehouseId = 0): array
    {
        epc_opl_ensure_schema($db);
        if (!function_exists('epc_erp_inventory_stock_report')) {
            require_once __DIR__ . '/epc_erp_inventory.php';
        }
        $db->exec("DELETE FROM `epc_erp_inv_movements` WHERE `reference`='DEMO-DEMAND'");

        $stock = epc_erp_inventory_stock_report($db, $warehouseId);
        // aggregate to item × warehouse
        $pos = array();
        foreach ($stock as $s) {
            $key = (int) $s['item_id'] . ':' . (int) $s['warehouse_id'];
            if (!isset($pos[$key])) {
                $pos[$key] = array('item_id' => (int) $s['item_id'], 'warehouse_id' => (int) $s['warehouse_id'], 'on_hand' => 0.0, 'cost' => (float) $s['avg_unit_cost']);
            }
            $pos[$key]['on_hand'] += (float) $s['qty_on_hand'];
            if ((float) $s['avg_unit_cost'] > 0) {
                $pos[$key]['cost'] = (float) $s['avg_unit_cost'];
            }
        }

        $ins = $db->prepare("INSERT INTO `epc_erp_inv_movements`
            (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`reference`,`note`,`movement_date`,`active`)
            VALUES ('sale_out',?,?,?,?,?,'DEMO-DEMAND','Seeded demand',?,1)");

        $patterns = array('smooth', 'erratic', 'intermittent', 'lumpy');
        $itemCount = 0;
        $moveCount = 0;
        $idx = 0;
        foreach ($pos as $p) {
            $idx++;
            mt_srand($p['item_id'] * 7919 + $p['warehouse_id']);
            $onHand = max(1.0, $p['on_hand']);
            $cost = $p['cost'] > 0 ? $p['cost'] : 1.0;
            $pattern = $patterns[$idx % 4];
            // roughly half the items should need an order: heavy demand vs on-hand
            $needsOrder = ($idx % 2 === 0);
            $base = $needsOrder
                ? $onHand * (0.7 + (mt_rand(0, 60) / 100.0))   // 0.7–1.3 × on-hand / month
                : $onHand * (0.08 + (mt_rand(0, 18) / 100.0));  // 0.08–0.26 × on-hand / month
            $base = max(1.0, $base);

            for ($i = $months - 1; $i >= 0; $i--) {
                $monthTs = strtotime('-' . $i . ' months');
                $qty = 0.0;
                switch ($pattern) {
                    case 'smooth':
                        $qty = $base * (0.85 + mt_rand(0, 30) / 100.0);
                        break;
                    case 'erratic':
                        $qty = $base * (0.3 + mt_rand(0, 170) / 100.0);
                        break;
                    case 'intermittent':
                        $qty = (mt_rand(0, 100) < 55) ? $base * (0.8 + mt_rand(0, 60) / 100.0) : 0.0;
                        break;
                    case 'lumpy':
                        $qty = (mt_rand(0, 100) < 40) ? $base * (1.0 + mt_rand(0, 250) / 100.0) : 0.0;
                        break;
                }
                $qty = round($qty, ($qty < 5 ? 2 : 0));
                if ($qty <= 0) {
                    continue;
                }
                // mid-month timestamp
                $dt = mktime(12, 0, 0, (int) date('n', $monthTs), min(28, 10 + mt_rand(0, 15)), (int) date('Y', $monthTs));
                $ins->execute(array(
                    $p['warehouse_id'], $p['item_id'], $qty, $cost, round($qty * $cost, 2), $dt,
                ));
                $moveCount++;
            }
            $itemCount++;
        }
        mt_srand();
        return array('items' => $itemCount, 'movements' => $moveCount);
    }
}

if (!function_exists('epc_opl_clear_demo_demand')) {
    function epc_opl_clear_demo_demand(PDO $db): int
    {
        $db->exec("DELETE FROM `epc_erp_order_recommendations`");
        return (int) $db->exec("DELETE FROM `epc_erp_inv_movements` WHERE `reference`='DEMO-DEMAND'");
    }
}

if (!function_exists('epc_opl_summary')) {
    /**
     * @return array{lines:int,due:int,order_value:float,confirmed_value:float,stockout_risk:int}
     */
    function epc_opl_summary(PDO $db, int $warehouseId = 0): array
    {
        $recs = epc_opl_recommendations($db, array('warehouse_id' => $warehouseId));
        $due = 0;
        $orderValue = 0.0;
        $confirmedValue = 0.0;
        $risk = 0;
        foreach ($recs as $r) {
            if ($r['roq'] > 0) {
                $due++;
                $orderValue += (float) $r['value'];
            }
            if ($r['status'] === 'confirmed') {
                $confirmedValue += (float) $r['value'];
            }
            if ($r['coverage_days'] !== null && $r['coverage_days'] < $r['lead_time_days']) {
                $risk++;
            }
        }
        return array(
            'lines' => count($recs),
            'due' => $due,
            'order_value' => round($orderValue, 2),
            'confirmed_value' => round($confirmedValue, 2),
            'stockout_risk' => $risk,
        );
    }
}
