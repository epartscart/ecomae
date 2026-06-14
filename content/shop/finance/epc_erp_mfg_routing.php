<?php
/**
 * Manufacturing depth — enterprise routes/operations, work centers
 * (resources), finite/infinite capacity scheduling, and a regenerative,
 * multi-level MRP run with level-by-level netting against on-hand.
 *
 * Additive: new epc_mfg_wc / epc_mfg_route* / epc_mfg_planned tables. Reuses
 * the existing BOM tables (epc_mfg_bom / epc_mfg_bom_lines) for explosion so
 * MRP and the BOM module stay consistent. Multi-company aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_manufacturing.php';

if (!function_exists('epc_mfgr_ensure_schema')) {
    function epc_mfgr_ensure_schema(PDO $db): void
    {
        epc_mfg_ensure_schema($db);

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mfg_wc` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `capacity_min_per_day` int(11) NOT NULL DEFAULT 480,
            `cost_per_hour` decimal(14,2) NOT NULL DEFAULT 0.00,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            UNIQUE KEY `x_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Work centers / resources'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mfg_route` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `product_item_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_product` (`product_item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Production routes'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mfg_route_op` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `route_id` int(11) NOT NULL DEFAULT 0,
            `op_no` int(11) NOT NULL DEFAULT 10,
            `workcenter_id` int(11) NOT NULL DEFAULT 0,
            `description` varchar(200) NOT NULL DEFAULT '',
            `setup_min` decimal(12,2) NOT NULL DEFAULT 0.00,
            `run_min_per_unit` decimal(12,4) NOT NULL DEFAULT 0.0000,
            PRIMARY KEY (`id`),
            KEY `x_route` (`route_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Route operations'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mfg_planned` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `order_type` varchar(16) NOT NULL DEFAULT 'production',
            `qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `level` int(11) NOT NULL DEFAULT 0,
            `due_date` int(11) NOT NULL DEFAULT 0,
            `source` varchar(16) NOT NULL DEFAULT 'mrp',
            `status` varchar(16) NOT NULL DEFAULT 'planned',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_item` (`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='MRP planned orders'");
    }
}

/* ---------------- work centers ---------------- */

if (!function_exists('epc_mfgr_wc_save')) {
    /** @param array<string,mixed> $data */
    function epc_mfgr_wc_save(PDO $db, array $data, int $id = 0): int
    {
        epc_mfgr_ensure_schema($db);
        $code = strtoupper(trim((string) ($data['code'] ?? '')));
        if ($code === '') {
            throw new Exception('Work center code is required');
        }
        $cap = (int) ($data['capacity_min_per_day'] ?? 480);
        if ($cap < 0) {
            $cap = 0;
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_mfg_wc` SET `code`=?, `name`=?, `capacity_min_per_day`=?, `cost_per_hour`=?, `active`=? WHERE `id`=?")
               ->execute(array($code, (string) ($data['name'] ?? ''), $cap, (float) ($data['cost_per_hour'] ?? 0), (int) ($data['active'] ?? 1), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_mfg_wc` (`company_id`,`code`,`name`,`capacity_min_per_day`,`cost_per_hour`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $code, (string) ($data['name'] ?? ''), $cap, (float) ($data['cost_per_hour'] ?? 0), (int) ($data['active'] ?? 1), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_mfgr_wc_get')) {
    /** @return array<string,mixed>|null */
    function epc_mfgr_wc_get(PDO $db, int $id): ?array
    {
        epc_mfgr_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_mfg_wc` WHERE `id`=?");
        $st->execute(array($id));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

if (!function_exists('epc_mfgr_wc_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_mfgr_wc_list(PDO $db, int $companyId = 0): array
    {
        epc_mfgr_ensure_schema($db);
        $sql = "SELECT * FROM `epc_mfg_wc` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY code";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- routes ---------------- */

if (!function_exists('epc_mfgr_route_save')) {
    /**
     * @param array<string,mixed> $data product_item_id, name
     * @param array<int,array<string,mixed>> $ops op_no, workcenter_id, description, setup_min, run_min_per_unit
     */
    function epc_mfgr_route_save(PDO $db, array $data, array $ops, int $id = 0): int
    {
        epc_mfgr_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_mfg_route` SET `product_item_id`=?, `name`=?, `active`=? WHERE `id`=?")
               ->execute(array((int) ($data['product_item_id'] ?? 0), (string) ($data['name'] ?? ''), (int) ($data['active'] ?? 1), $id));
            $db->prepare("DELETE FROM `epc_mfg_route_op` WHERE `route_id`=?")->execute(array($id));
        } else {
            $db->prepare("INSERT INTO `epc_mfg_route` (`company_id`,`product_item_id`,`name`,`active`,`time_created`) VALUES (?,?,?,?,?)")
               ->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['product_item_id'] ?? 0), (string) ($data['name'] ?? ''), (int) ($data['active'] ?? 1), time()));
            $id = (int) $db->lastInsertId();
        }
        $ins = $db->prepare("INSERT INTO `epc_mfg_route_op` (`route_id`,`op_no`,`workcenter_id`,`description`,`setup_min`,`run_min_per_unit`) VALUES (?,?,?,?,?,?)");
        foreach ($ops as $op) {
            $ins->execute(array($id, (int) ($op['op_no'] ?? 10), (int) ($op['workcenter_id'] ?? 0), (string) ($op['description'] ?? ''), (float) ($op['setup_min'] ?? 0), (float) ($op['run_min_per_unit'] ?? 0)));
        }
        return $id;
    }
}

if (!function_exists('epc_mfgr_route_ops')) {
    /** @return array<int,array<string,mixed>> */
    function epc_mfgr_route_ops(PDO $db, int $routeId): array
    {
        epc_mfgr_ensure_schema($db);
        $st = $db->prepare("SELECT o.*, w.code AS wc_code, w.name AS wc_name, w.capacity_min_per_day, w.cost_per_hour
                            FROM `epc_mfg_route_op` o LEFT JOIN `epc_mfg_wc` w ON w.id=o.workcenter_id
                            WHERE o.route_id=? ORDER BY o.op_no");
        $st->execute(array($routeId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_mfgr_route_for_item')) {
    /** @return array<string,mixed>|null */
    function epc_mfgr_route_for_item(PDO $db, int $companyId, int $itemId): ?array
    {
        epc_mfgr_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_mfg_route` WHERE company_id=? AND product_item_id=? AND active=1 ORDER BY id DESC LIMIT 1");
        $st->execute(array($companyId, $itemId));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

if (!function_exists('epc_mfgr_routes')) {
    /** @return array<int,array<string,mixed>> */
    function epc_mfgr_routes(PDO $db, int $companyId = 0): array
    {
        epc_mfgr_ensure_schema($db);
        $sql = "SELECT r.*, (SELECT COUNT(*) FROM `epc_mfg_route_op` o WHERE o.route_id=r.id) AS op_count FROM `epc_mfg_route` r WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND r.company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY r.id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- capacity scheduling (pure) ---------------- */

if (!function_exists('epc_mfgr_op_load_min')) {
    /** Load minutes for one operation at a build qty: setup + run*qty. */
    function epc_mfgr_op_load_min(float $setupMin, float $runMinPerUnit, float $qty): float
    {
        return round($setupMin + ($runMinPerUnit * $qty), 4);
    }
}

if (!function_exists('epc_mfgr_schedule')) {
    /**
     * Forward, sequential, finite-capacity schedule of route operations for a
     * build qty. Each operation starts when the previous finishes; minutes are
     * laid onto each work center's available minutes/day, so an operation that
     * exceeds one day's capacity spills onto following days.
     *
     * @param array<int,array<string,mixed>> $ops each with setup_min, run_min_per_unit, workcenter_id, capacity_min_per_day, op_no
     * @return array{operations:array<int,array<string,mixed>>,total_min:float,total_days:int,finite:bool}
     */
    function epc_mfgr_schedule(array $ops, float $qty, int $startTs, bool $finite = true): array
    {
        $cursorMin = 0.0;       // running minutes from start (sequential)
        $out = array();
        $maxDays = 0;
        foreach ($ops as $op) {
            $load = epc_mfgr_op_load_min((float) ($op['setup_min'] ?? 0), (float) ($op['run_min_per_unit'] ?? 0), $qty);
            $cap = (int) ($op['capacity_min_per_day'] ?? 480);
            if ($cap <= 0) {
                $cap = 480;
            }
            $startMin = $cursorMin;
            // working days this op spans against its work center capacity
            $opDays = $finite ? (int) max(1, (int) ceil($load / $cap)) : 1;
            // elapsed minutes for this op: finite => spread over opDays of cap; infinite => raw load
            $elapsed = $finite ? ($opDays * $cap >= $load ? $load : $opDays * $cap) : $load;
            $endMin = $startMin + ($finite ? max($load, 0) : $load);
            $startDay = (int) floor($startMin / $cap);
            $endDay = $startDay + $opDays;
            if ($endDay > $maxDays) {
                $maxDays = $endDay;
            }
            $out[] = array(
                'op_no' => (int) ($op['op_no'] ?? 0),
                'workcenter_id' => (int) ($op['workcenter_id'] ?? 0),
                'wc_code' => (string) ($op['wc_code'] ?? ''),
                'description' => (string) ($op['description'] ?? ''),
                'load_min' => $load,
                'days' => $opDays,
                'start_min' => round($startMin, 2),
                'end_min' => round($endMin, 2),
                'start_ts' => $startTs + (int) round($startMin * 60),
                'end_ts' => $startTs + (int) round($endMin * 60),
                'capacity_ok' => $finite ? ($load <= $cap * $opDays) : true,
            );
            $cursorMin = $endMin;
        }
        return array(
            'operations' => $out,
            'total_min' => round($cursorMin, 2),
            'total_days' => $maxDays > 0 ? $maxDays : 0,
            'finite' => $finite,
        );
    }
}

if (!function_exists('epc_mfgr_schedule_route')) {
    /** Convenience: schedule a saved route for a qty. */
    function epc_mfgr_schedule_route(PDO $db, int $routeId, float $qty, int $startTs, bool $finite = true): array
    {
        $ops = epc_mfgr_route_ops($db, $routeId);
        return epc_mfgr_schedule($ops, $qty, $startTs, $finite);
    }
}

/* ---------------- BOM helpers for MRP ---------------- */

if (!function_exists('epc_mfgr_bom_for_item')) {
    /** @return array<string,mixed>|null active BOM header for a product item */
    function epc_mfgr_bom_for_item(PDO $db, int $itemId): ?array
    {
        epc_mfgr_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_mfg_bom` WHERE `product_item_id`=? AND `active`=1 ORDER BY id DESC LIMIT 1");
        $st->execute(array($itemId));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

/* ---------------- regenerative multi-level MRP ---------------- */

if (!function_exists('epc_mfgr_mrp_net_explode')) {
    /**
     * Level-by-level net explosion. Nets requiredQty against on-hand at each
     * node (consuming on-hand), then for the net qty emits a planned order
     * (production if the item has a BOM, else purchase) and recurses into the
     * BOM components. Pure given $onhand (passed by reference, mutated as
     * on-hand is consumed) and a BOM resolver closure.
     *
     * @param array<int,float> $onhand item_id => qty (mutated)
     * @param callable $bomResolver fn(int $itemId): ?array{output_qty:float,lines:array<int,array{component_item_id:int,qty_per:float,scrap_percent:float}>}
     * @param array<int,array<string,mixed>> $orders accumulator (by reference)
     */
    function epc_mfgr_mrp_net_explode(int $itemId, float $requiredQty, array &$onhand, callable $bomResolver, array &$orders, int $level = 0, int $maxLevel = 20): void
    {
        if ($requiredQty <= 0 || $level > $maxLevel) {
            return;
        }
        $avail = $onhand[$itemId] ?? 0.0;
        $consume = min($avail, $requiredQty);
        $onhand[$itemId] = $avail - $consume;
        $net = round($requiredQty - $consume, 4);
        if ($net <= 0) {
            return;
        }
        $bom = $bomResolver($itemId);
        if ($bom && !empty($bom['lines'])) {
            $orders[] = array('item_id' => $itemId, 'order_type' => 'production', 'qty' => $net, 'level' => $level);
            $outQty = (float) ($bom['output_qty'] ?? 1);
            if ($outQty <= 0) {
                $outQty = 1.0;
            }
            $batch = $net / $outQty;
            foreach ($bom['lines'] as $ln) {
                $childReq = round($batch * (float) $ln['qty_per'] * (1 + ((float) ($ln['scrap_percent'] ?? 0) / 100)), 4);
                epc_mfgr_mrp_net_explode((int) $ln['component_item_id'], $childReq, $onhand, $bomResolver, $orders, $level + 1, $maxLevel);
            }
        } else {
            $orders[] = array('item_id' => $itemId, 'order_type' => 'purchase', 'qty' => $net, 'level' => $level);
        }
    }
}

if (!function_exists('epc_mfgr_db_bom_resolver')) {
    /** Build a BOM resolver closure backed by the DB BOM tables. */
    function epc_mfgr_db_bom_resolver(PDO $db): callable
    {
        epc_mfgr_ensure_schema($db);
        return static function (int $itemId) use ($db): ?array {
            $bom = epc_mfgr_bom_for_item($db, $itemId);
            if (!$bom) {
                return null;
            }
            $st = $db->prepare("SELECT component_item_id, qty_per, scrap_percent FROM `epc_mfg_bom_lines` WHERE bom_id=?");
            $st->execute(array((int) $bom['id']));
            return array(
                'output_qty' => (float) $bom['output_qty'],
                'lines' => $st->fetchAll(PDO::FETCH_ASSOC),
            );
        };
    }
}

if (!function_exists('epc_mfgr_mrp_run')) {
    /**
     * Regenerative MRP: clears prior MRP-sourced planned orders for the company,
     * then nets demand against on-hand level-by-level and writes planned
     * production/purchase orders. Aggregates duplicate items.
     *
     * @param array<int,float> $demand item_id => required qty
     * @param array<int,float> $onhand item_id => on-hand qty
     * @return array<int,array<string,mixed>> persisted planned orders
     */
    function epc_mfgr_mrp_run(PDO $db, int $companyId, array $demand, array $onhand, int $dueTs = 0): array
    {
        epc_mfgr_ensure_schema($db);
        $resolver = epc_mfgr_db_bom_resolver($db);
        $orders = array();
        $oh = $onhand;
        foreach ($demand as $itemId => $qty) {
            epc_mfgr_mrp_net_explode((int) $itemId, (float) $qty, $oh, $resolver, $orders);
        }
        // aggregate by item+type, keep deepest level (lowest-level code drives sequencing)
        $agg = array();
        foreach ($orders as $o) {
            $k = $o['item_id'] . ':' . $o['order_type'];
            if (!isset($agg[$k])) {
                $agg[$k] = $o;
            } else {
                $agg[$k]['qty'] = round($agg[$k]['qty'] + $o['qty'], 4);
                $agg[$k]['level'] = max($agg[$k]['level'], $o['level']);
            }
        }
        // regenerate
        $db->prepare("DELETE FROM `epc_mfg_planned` WHERE company_id=? AND source='mrp'")->execute(array($companyId));
        $ins = $db->prepare("INSERT INTO `epc_mfg_planned` (`company_id`,`item_id`,`order_type`,`qty`,`level`,`due_date`,`source`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)");
        $now = time();
        $result = array();
        // order by level desc so lowest-level (raw materials) come first, like low-level coding
        usort($agg, static function ($a, $b) { return $b['level'] <=> $a['level']; });
        foreach ($agg as $o) {
            $ins->execute(array($companyId, (int) $o['item_id'], (string) $o['order_type'], (float) $o['qty'], (int) $o['level'], $dueTs, 'mrp', 'planned', $now));
            $o['id'] = (int) $db->lastInsertId();
            $result[] = $o;
        }
        return $result;
    }
}

if (!function_exists('epc_mfgr_planned_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_mfgr_planned_list(PDO $db, int $companyId = 0): array
    {
        epc_mfgr_ensure_schema($db);
        $sql = "SELECT * FROM `epc_mfg_planned` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY level DESC, item_id";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_mfgr_planned_firm')) {
    /** Firm a planned order (mark released). */
    function epc_mfgr_planned_firm(PDO $db, int $id): void
    {
        epc_mfgr_ensure_schema($db);
        $db->prepare("UPDATE `epc_mfg_planned` SET `status`='firmed' WHERE `id`=?")->execute(array($id));
    }
}

if (!function_exists('epc_mfgr_summary')) {
    /** @return array{work_centers:int,routes:int,planned:int,planned_production:int,planned_purchase:int,capacity_min:int} */
    function epc_mfgr_summary(PDO $db, int $companyId = 0): array
    {
        epc_mfgr_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        $w = $companyId > 0 ? " WHERE company_id=" . $companyId : "";
        return array(
            'work_centers' => (int) $db->query("SELECT COUNT(*) FROM `epc_mfg_wc`" . $w)->fetchColumn(),
            'routes' => (int) $db->query("SELECT COUNT(*) FROM `epc_mfg_route`" . $w)->fetchColumn(),
            'planned' => (int) $db->query("SELECT COUNT(*) FROM `epc_mfg_planned` WHERE source='mrp'" . $wa)->fetchColumn(),
            'planned_production' => (int) $db->query("SELECT COUNT(*) FROM `epc_mfg_planned` WHERE source='mrp' AND order_type='production'" . $wa)->fetchColumn(),
            'planned_purchase' => (int) $db->query("SELECT COUNT(*) FROM `epc_mfg_planned` WHERE source='mrp' AND order_type='purchase'" . $wa)->fetchColumn(),
            'capacity_min' => (int) $db->query("SELECT COALESCE(SUM(capacity_min_per_day),0) FROM `epc_mfg_wc` WHERE active=1" . $wa)->fetchColumn(),
        );
    }
}
