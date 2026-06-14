<?php
/**
 * Costing value-models — enterprise inventory valuation methods with
 * inventory recalculation / closing.
 *
 * Supports Standard, FIFO, LIFO and Moving-average costing. Pure compute
 * functions take a transaction list (receipt/issue) and return COGS, closing
 * quantity/value, layers and (for standard) purchase-price variance — separated
 * from persistence so they are deterministic and unit-testable.
 *
 * Additive: new epc_costm_* tables; does NOT change the live posting/valuation
 * path (existing inventory keeps weighted-average). This is an analysis/closing
 * layer per item with a configurable model. Multi-company aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_costm_ensure_schema')) {
    function epc_costm_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_costm_item` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `model` varchar(16) NOT NULL DEFAULT 'moving_avg',
            `std_cost` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_item` (`company_id`,`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-item costing model assignment'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_costm_txn` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `txn_type` varchar(8) NOT NULL DEFAULT 'receipt',
            `qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `unit_cost` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `txn_date` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_item` (`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Costing model transactions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_costm_close` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `model` varchar(16) NOT NULL DEFAULT 'moving_avg',
            `label` varchar(40) NOT NULL DEFAULT '',
            `cogs` decimal(18,2) NOT NULL DEFAULT 0.00,
            `closing_qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `closing_value` decimal(18,2) NOT NULL DEFAULT 0.00,
            `variance` decimal(18,2) NOT NULL DEFAULT 0.00,
            `detail_json` mediumtext,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_item` (`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inventory closing/recalculation runs'");
    }
}

if (!function_exists('epc_costm_models')) {
    /** @return array<int,string> */
    function epc_costm_models(): array
    {
        return array('standard', 'fifo', 'lifo', 'moving_avg');
    }
}

/* ---------------- pure compute ---------------- */

if (!function_exists('epc_costm_compute')) {
    /**
     * Pure: run a costing model over a chronologically-ordered transaction list.
     *
     * @param string $model standard|fifo|lifo|moving_avg
     * @param array<int,array{txn_type:string,qty:float,unit_cost:float}> $txns
     * @param float $stdCost standard cost (only used by 'standard')
     * @return array{model:string,cogs:float,closing_qty:float,closing_value:float,variance:float,issues:array<int,array<string,mixed>>,layers:array<int,array{qty:float,cost:float}>}
     */
    function epc_costm_compute(string $model, array $txns, float $stdCost = 0.0): array
    {
        if (!in_array($model, epc_costm_models(), true)) {
            $model = 'moving_avg';
        }
        $cogs = 0.0;
        $variance = 0.0;
        $issues = array();
        $layers = array(); // FIFO/LIFO: list of ['qty'=>, 'cost'=>]
        $avgQty = 0.0;
        $avgVal = 0.0;
        $lastCost = $stdCost;

        foreach ($txns as $t) {
            $type = (string) ($t['txn_type'] ?? 'receipt');
            $qty = (float) ($t['qty'] ?? 0);
            $unit = (float) ($t['unit_cost'] ?? 0);
            if ($qty <= 0) {
                continue;
            }
            if ($type === 'receipt') {
                $lastCost = $unit;
                if ($model === 'standard') {
                    $variance += round($qty * ($unit - $stdCost), 4);
                } elseif ($model === 'moving_avg') {
                    $avgQty += $qty;
                    $avgVal += $qty * $unit;
                } else {
                    $layers[] = array('qty' => $qty, 'cost' => $unit);
                }
                continue;
            }
            // issue
            $issueCost = 0.0;
            if ($model === 'standard') {
                $issueCost = round($qty * $stdCost, 4);
            } elseif ($model === 'moving_avg') {
                $avg = $avgQty > 0 ? $avgVal / $avgQty : $lastCost;
                $issueCost = round($qty * $avg, 4);
                $avgQty -= $qty;
                $avgVal -= $issueCost;
                if ($avgQty <= 0) {
                    $avgQty = 0.0;
                    $avgVal = 0.0;
                }
            } else { // fifo / lifo
                $need = $qty;
                while ($need > 0 && !empty($layers)) {
                    $idx = $model === 'fifo' ? 0 : count($layers) - 1;
                    $layer = &$layers[$idx];
                    $take = min($need, $layer['qty']);
                    $issueCost += $take * $layer['cost'];
                    $layer['qty'] -= $take;
                    $need -= $take;
                    if ($layer['qty'] <= 0.00001) {
                        unset($layers[$idx]);
                        $layers = array_values($layers);
                    }
                    unset($layer);
                }
                if ($need > 0) { // shortfall valued at last known cost
                    $issueCost += $need * $lastCost;
                }
                $issueCost = round($issueCost, 4);
            }
            $cogs += $issueCost;
            $issues[] = array('qty' => $qty, 'cost' => round($issueCost, 2), 'unit' => $qty > 0 ? round($issueCost / $qty, 4) : 0.0);
        }

        // closing position
        if ($model === 'standard') {
            $closingQty = 0.0;
            foreach ($txns as $t) {
                $q = (float) ($t['qty'] ?? 0);
                $closingQty += ((string) ($t['txn_type'] ?? 'receipt') === 'receipt') ? $q : -$q;
            }
            $closingValue = round($closingQty * $stdCost, 2);
        } elseif ($model === 'moving_avg') {
            $closingQty = $avgQty;
            $closingValue = round($avgVal, 2);
        } else {
            $closingQty = 0.0;
            $cv = 0.0;
            foreach ($layers as $l) {
                $closingQty += $l['qty'];
                $cv += $l['qty'] * $l['cost'];
            }
            $closingValue = round($cv, 2);
        }

        return array(
            'model' => $model,
            'cogs' => round($cogs, 2),
            'closing_qty' => round($closingQty, 4),
            'closing_value' => $closingValue,
            'variance' => round($variance, 2),
            'issues' => $issues,
            'layers' => array_values($layers),
        );
    }
}

/* ---------------- per-item model ---------------- */

if (!function_exists('epc_costm_item_set')) {
    function epc_costm_item_set(PDO $db, int $companyId, int $itemId, string $model, float $stdCost = 0.0): void
    {
        epc_costm_ensure_schema($db);
        if (!in_array($model, epc_costm_models(), true)) {
            throw new Exception('Invalid costing model');
        }
        $db->prepare("INSERT INTO `epc_costm_item` (`company_id`,`item_id`,`model`,`std_cost`,`time_updated`) VALUES (?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `model`=VALUES(`model`), `std_cost`=VALUES(`std_cost`), `time_updated`=VALUES(`time_updated`)")
           ->execute(array($companyId, $itemId, $model, $stdCost, time()));
    }
}

if (!function_exists('epc_costm_item_get')) {
    /** @return array{item_id:int,model:string,std_cost:float} */
    function epc_costm_item_get(PDO $db, int $companyId, int $itemId): array
    {
        epc_costm_ensure_schema($db);
        $st = $db->prepare("SELECT model, std_cost FROM `epc_costm_item` WHERE company_id=? AND item_id=?");
        $st->execute(array($companyId, $itemId));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return array(
            'item_id' => $itemId,
            'model' => $r ? (string) $r['model'] : 'moving_avg',
            'std_cost' => $r ? (float) $r['std_cost'] : 0.0,
        );
    }
}

if (!function_exists('epc_costm_items')) {
    /** @return array<int,array<string,mixed>> */
    function epc_costm_items(PDO $db, int $companyId = 0): array
    {
        epc_costm_ensure_schema($db);
        $sql = "SELECT * FROM `epc_costm_item` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY item_id";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- transactions ---------------- */

if (!function_exists('epc_costm_txn_add')) {
    function epc_costm_txn_add(PDO $db, int $companyId, int $itemId, string $type, float $qty, float $unitCost, int $date = 0): int
    {
        epc_costm_ensure_schema($db);
        if (!in_array($type, array('receipt', 'issue'), true)) {
            throw new Exception('Invalid transaction type');
        }
        $db->prepare("INSERT INTO `epc_costm_txn` (`company_id`,`item_id`,`txn_type`,`qty`,`unit_cost`,`txn_date`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array($companyId, $itemId, $type, $qty, $unitCost, $date > 0 ? $date : time(), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_costm_txns')) {
    /** @return array<int,array<string,mixed>> chronological */
    function epc_costm_txns(PDO $db, int $companyId, int $itemId): array
    {
        epc_costm_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_costm_txn` WHERE company_id=? AND item_id=? ORDER BY txn_date ASC, id ASC");
        $st->execute(array($companyId, $itemId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- closing / recalculation ---------------- */

if (!function_exists('epc_costm_close_run')) {
    /**
     * Run inventory closing/recalculation for an item: reads its model + txns,
     * computes the value-model result and persists a closing record.
     *
     * @return array<string,mixed>
     */
    function epc_costm_close_run(PDO $db, int $companyId, int $itemId, string $label = ''): array
    {
        epc_costm_ensure_schema($db);
        $item = epc_costm_item_get($db, $companyId, $itemId);
        $txns = array();
        foreach (epc_costm_txns($db, $companyId, $itemId) as $t) {
            $txns[] = array('txn_type' => (string) $t['txn_type'], 'qty' => (float) $t['qty'], 'unit_cost' => (float) $t['unit_cost']);
        }
        $res = epc_costm_compute($item['model'], $txns, $item['std_cost']);
        $db->prepare("INSERT INTO `epc_costm_close` (`company_id`,`item_id`,`model`,`label`,`cogs`,`closing_qty`,`closing_value`,`variance`,`detail_json`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)")
           ->execute(array($companyId, $itemId, $res['model'], $label !== '' ? $label : date('Y-m'), $res['cogs'], $res['closing_qty'], $res['closing_value'], $res['variance'], json_encode($res), time()));
        $res['id'] = (int) $db->lastInsertId();
        return $res;
    }
}

if (!function_exists('epc_costm_closes')) {
    /** @return array<int,array<string,mixed>> */
    function epc_costm_closes(PDO $db, int $companyId = 0, int $itemId = 0): array
    {
        epc_costm_ensure_schema($db);
        $sql = "SELECT * FROM `epc_costm_close` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        if ($itemId > 0) {
            $sql .= " AND item_id=?";
            $args[] = $itemId;
        }
        $sql .= " ORDER BY id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- compare + summary ---------------- */

if (!function_exists('epc_costm_compare')) {
    /**
     * Run all four models over the same transactions for side-by-side analysis.
     *
     * @param array<int,array{txn_type:string,qty:float,unit_cost:float}> $txns
     * @return array<string,array<string,mixed>>
     */
    function epc_costm_compare(array $txns, float $stdCost = 0.0): array
    {
        $out = array();
        foreach (epc_costm_models() as $m) {
            $out[$m] = epc_costm_compute($m, $txns, $stdCost);
        }
        return $out;
    }
}

if (!function_exists('epc_costm_summary')) {
    /** @return array{items:int,closings:int,closing_value:float} */
    function epc_costm_summary(PDO $db, int $companyId = 0): array
    {
        epc_costm_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        $cv = (float) $db->query("SELECT COALESCE(SUM(closing_value),0) FROM `epc_costm_close` c WHERE c.id IN (SELECT MAX(id) FROM `epc_costm_close` GROUP BY item_id)" . ($companyId > 0 ? " AND c.company_id=" . $companyId : ""))->fetchColumn();
        return array(
            'items' => (int) $db->query("SELECT COUNT(*) FROM `epc_costm_item` WHERE 1=1" . $wa)->fetchColumn(),
            'closings' => (int) $db->query("SELECT COUNT(*) FROM `epc_costm_close` WHERE 1=1" . $wa)->fetchColumn(),
            'closing_value' => round($cv, 2),
        );
    }
}
