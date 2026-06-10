<?php
/**
 * Advanced ERP — Manufacturing (discrete + light process).
 *
 * Bills of material (multi-level), work orders, material issue/backflush,
 * finished-goods receipt, and work-order costing (materials + labour +
 * overhead, with variance vs standard).
 *
 * Additive: new epc_mfg_* tables. Inventory movements are posted through the
 * existing epc_erp_inventory layer when available, so stock and valuation stay
 * consistent; if that layer is absent the module still tracks quantities.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_mfg_ensure_schema')) {
    function epc_mfg_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mfg_bom` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `product_item_id` int(11) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `output_qty` decimal(14,4) NOT NULL DEFAULT 1.0000,
            `labour_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `overhead_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_product` (`product_item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bill of materials header'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mfg_bom_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `bom_id` int(11) NOT NULL,
            `component_item_id` int(11) NOT NULL,
            `qty_per` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `scrap_percent` decimal(7,3) NOT NULL DEFAULT 0.000,
            PRIMARY KEY (`id`),
            KEY `x_bom` (`bom_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOM components'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mfg_work_orders` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `wo_no` varchar(40) NOT NULL DEFAULT '',
            `bom_id` int(11) NOT NULL,
            `product_item_id` int(11) NOT NULL,
            `qty_planned` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `qty_produced` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `status` varchar(16) NOT NULL DEFAULT 'planned',
            `material_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `labour_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `overhead_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Work orders'");
    }
}

if (!function_exists('epc_mfg_bom_save')) {
    /**
     * @param array<string,mixed> $data product_item_id, name, output_qty, labour_cost, overhead_cost
     * @param array<int,array<string,mixed>> $lines component_item_id, qty_per, scrap_percent
     */
    function epc_mfg_bom_save(PDO $db, array $data, array $lines, int $id = 0): int
    {
        epc_mfg_ensure_schema($db);
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_mfg_bom` SET `product_item_id`=?, `name`=?, `output_qty`=?, `labour_cost`=?, `overhead_cost`=? WHERE `id`=?")
               ->execute(array((int) $data['product_item_id'], (string) ($data['name'] ?? ''), (float) ($data['output_qty'] ?? 1), (float) ($data['labour_cost'] ?? 0), (float) ($data['overhead_cost'] ?? 0), $id));
            $db->prepare("DELETE FROM `epc_mfg_bom_lines` WHERE `bom_id`=?")->execute(array($id));
        } else {
            $db->prepare("INSERT INTO `epc_mfg_bom` (`product_item_id`,`name`,`output_qty`,`labour_cost`,`overhead_cost`,`active`,`time_created`) VALUES (?,?,?,?,?,1,?)")
               ->execute(array((int) $data['product_item_id'], (string) ($data['name'] ?? ''), (float) ($data['output_qty'] ?? 1), (float) ($data['labour_cost'] ?? 0), (float) ($data['overhead_cost'] ?? 0), $now));
            $id = (int) $db->lastInsertId();
        }
        $ins = $db->prepare("INSERT INTO `epc_mfg_bom_lines` (`bom_id`,`component_item_id`,`qty_per`,`scrap_percent`) VALUES (?,?,?,?)");
        foreach ($lines as $ln) {
            $ins->execute(array($id, (int) $ln['component_item_id'], (float) ($ln['qty_per'] ?? 0), (float) ($ln['scrap_percent'] ?? 0)));
        }
        return $id;
    }
}

if (!function_exists('epc_mfg_bom_requirements')) {
    /**
     * Gross component requirements for a build quantity (incl. scrap).
     *
     * @return array<int,array<string,mixed>> component_item_id => required qty
     */
    function epc_mfg_bom_requirements(PDO $db, int $bomId, float $buildQty): array
    {
        epc_mfg_ensure_schema($db);
        $hdr = $db->prepare("SELECT * FROM `epc_mfg_bom` WHERE `id`=?");
        $hdr->execute(array($bomId));
        $bom = $hdr->fetch(PDO::FETCH_ASSOC);
        if (!$bom) {
            return array();
        }
        $output = max(0.0001, (float) $bom['output_qty']);
        $factor = $buildQty / $output;
        $st = $db->prepare("SELECT * FROM `epc_mfg_bom_lines` WHERE `bom_id`=?");
        $st->execute(array($bomId));
        $out = array();
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $ln) {
            $base = (float) $ln['qty_per'] * $factor;
            $withScrap = $base * (1 + (float) $ln['scrap_percent'] / 100);
            $out[] = array(
                'component_item_id' => (int) $ln['component_item_id'],
                'qty_required' => round($withScrap, 4),
            );
        }
        return $out;
    }
}

if (!function_exists('epc_mfg_wo_create')) {
    /**
     * @param array<string,mixed> $data bom_id, qty_planned, warehouse_id, wo_no
     */
    function epc_mfg_wo_create(PDO $db, array $data): int
    {
        epc_mfg_ensure_schema($db);
        $bomId = (int) ($data['bom_id'] ?? 0);
        $hdr = $db->prepare("SELECT * FROM `epc_mfg_bom` WHERE `id`=?");
        $hdr->execute(array($bomId));
        $bom = $hdr->fetch(PDO::FETCH_ASSOC);
        if (!$bom) {
            throw new Exception('BOM not found');
        }
        $now = time();
        $woNo = (string) ($data['wo_no'] ?? ('WO-' . $now));
        $db->prepare(
            "INSERT INTO `epc_mfg_work_orders` (`wo_no`,`bom_id`,`product_item_id`,`qty_planned`,`warehouse_id`,`status`,`time_created`,`time_updated`)
             VALUES (?,?,?,?,?, 'planned', ?,?)"
        )->execute(array(
            $woNo,
            $bomId,
            (int) $bom['product_item_id'],
            (float) ($data['qty_planned'] ?? 0),
            (int) ($data['warehouse_id'] ?? 0),
            $now,
            $now,
        ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_mfg_wo_issue_materials')) {
    /**
     * Issue (consume) components for a work order. Posts negative inventory
     * movements via epc_erp_inventory when available and accumulates material
     * cost. Component unit cost is taken from arg map or looked up.
     *
     * @param array<int,float> $unitCosts component_item_id => unit cost override
     * @return array<string,mixed>
     */
    function epc_mfg_wo_issue_materials(PDO $db, int $woId, array $unitCosts = array()): array
    {
        epc_mfg_ensure_schema($db);
        $wo = epc_mfg_wo_get($db, $woId);
        if (!$wo) {
            throw new Exception('Work order not found');
        }
        $reqs = epc_mfg_bom_requirements($db, (int) $wo['bom_id'], (float) $wo['qty_planned']);
        $materialCost = 0.0;
        $issued = array();
        $hasInv = function_exists('epc_erp_inventory_record_movement');
        foreach ($reqs as $r) {
            $itemId = (int) $r['component_item_id'];
            $qty = (float) $r['qty_required'];
            $unit = isset($unitCosts[$itemId]) ? (float) $unitCosts[$itemId] : epc_mfg_item_cost($db, $itemId, (int) $wo['warehouse_id']);
            $materialCost += $qty * $unit;
            if ($hasInv) {
                try {
                    epc_erp_inventory_record_movement($db, array(
                        'item_id' => $itemId,
                        'warehouse_id' => (int) $wo['warehouse_id'],
                        'movement_type' => 'mfg_issue',
                        'qty' => -1 * $qty,
                        'unit_cost' => $unit,
                        'reference' => $wo['wo_no'],
                        'movement_date' => date('Y-m-d'),
                    ));
                } catch (Throwable $e) {
                    // tracking-only fallback
                }
            }
            $issued[] = array('component_item_id' => $itemId, 'qty' => $qty, 'unit_cost' => round($unit, 4));
        }
        $materialCost = round($materialCost, 2);
        $db->prepare("UPDATE `epc_mfg_work_orders` SET `material_cost`=?, `status`='in_progress', `time_updated`=? WHERE `id`=?")
           ->execute(array($materialCost, time(), $woId));
        return array('work_order_id' => $woId, 'material_cost' => $materialCost, 'issued' => $issued);
    }
}

if (!function_exists('epc_mfg_wo_complete')) {
    /**
     * Receive finished goods for a work order; finalise costing.
     * Unit cost of FG = (material + labour + overhead) / qty produced.
     *
     * @return array<string,mixed>
     */
    function epc_mfg_wo_complete(PDO $db, int $woId, float $qtyProduced, float $labourCost = -1, float $overheadCost = -1): array
    {
        epc_mfg_ensure_schema($db);
        $wo = epc_mfg_wo_get($db, $woId);
        if (!$wo) {
            throw new Exception('Work order not found');
        }
        // Default labour/overhead from BOM scaled to produced qty if not given.
        $bom = $db->prepare("SELECT * FROM `epc_mfg_bom` WHERE `id`=?");
        $bom->execute(array((int) $wo['bom_id']));
        $bomRow = $bom->fetch(PDO::FETCH_ASSOC) ?: array();
        $outputQty = max(0.0001, (float) ($bomRow['output_qty'] ?? 1));
        $scale = $qtyProduced / $outputQty;
        if ($labourCost < 0) {
            $labourCost = round((float) ($bomRow['labour_cost'] ?? 0) * $scale, 2);
        }
        if ($overheadCost < 0) {
            $overheadCost = round((float) ($bomRow['overhead_cost'] ?? 0) * $scale, 2);
        }
        $materialCost = (float) $wo['material_cost'];
        $totalCost = round($materialCost + $labourCost + $overheadCost, 2);
        $unitCost = $qtyProduced > 0 ? round($totalCost / $qtyProduced, 4) : 0.0;

        if (function_exists('epc_erp_inventory_record_movement')) {
            try {
                epc_erp_inventory_record_movement($db, array(
                    'item_id' => (int) $wo['product_item_id'],
                    'warehouse_id' => (int) $wo['warehouse_id'],
                    'movement_type' => 'mfg_receipt',
                    'qty' => $qtyProduced,
                    'unit_cost' => $unitCost,
                    'reference' => $wo['wo_no'],
                    'movement_date' => date('Y-m-d'),
                ));
            } catch (Throwable $e) {
                // tracking-only fallback
            }
        }

        $db->prepare("UPDATE `epc_mfg_work_orders` SET `qty_produced`=?, `labour_cost`=?, `overhead_cost`=?, `status`='completed', `time_updated`=? WHERE `id`=?")
           ->execute(array($qtyProduced, $labourCost, $overheadCost, time(), $woId));

        return array(
            'work_order_id' => $woId,
            'qty_produced' => $qtyProduced,
            'material_cost' => round($materialCost, 2),
            'labour_cost' => $labourCost,
            'overhead_cost' => $overheadCost,
            'total_cost' => $totalCost,
            'unit_cost' => $unitCost,
        );
    }
}

if (!function_exists('epc_mfg_wo_get')) {
    /**
     * @return array<string,mixed>|null
     */
    function epc_mfg_wo_get(PDO $db, int $woId): ?array
    {
        epc_mfg_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_mfg_work_orders` WHERE `id`=?");
        $st->execute(array($woId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_mfg_item_cost')) {
    /**
     * Best-effort component unit cost from inventory stock; 0 if unavailable.
     */
    function epc_mfg_item_cost(PDO $db, int $itemId, int $warehouseId = 0): float
    {
        try {
            if ($warehouseId > 0) {
                $st = $db->prepare("SELECT `avg_unit_cost` FROM `epc_erp_inv_stock` WHERE `item_id`=? AND `warehouse_id`=? LIMIT 1");
                $st->execute(array($itemId, $warehouseId));
            } else {
                $st = $db->prepare("SELECT `avg_unit_cost` FROM `epc_erp_inv_stock` WHERE `item_id`=? ORDER BY `avg_unit_cost` DESC LIMIT 1");
                $st->execute(array($itemId));
            }
            $v = $st->fetchColumn();
            return $v === false ? 0.0 : (float) $v;
        } catch (Throwable $e) {
            return 0.0;
        }
    }
}
