<?php
/**
 * Per-supplier order fulfillment pipeline (OMS).
 * Keyed by shop order # + supplier/warehouse.
 */
defined('_ASTEXE_') or die('No access');

function epc_order_supplier_fulfillment_stages(): array
{
	return array(
		'supplier_confirm' => 'Supplier confirm',
		'supplier_payment_done' => 'Supplier payment done',
		'supplier_ready_to_delivery' => 'Supplier ready to delivery',
		'delivered' => 'Delivered (from supplier)',
		'receipt_in_warehouse' => 'Receipt in our warehouse',
		'ready_to_customer' => 'Ready to customer',
		'packing' => 'Packing',
		'dispatch' => 'Dispatch',
		'deliver' => 'Deliver to customer',
		'complete' => 'Complete',
	);
}

function epc_order_supplier_fulfillment_stage_index(string $stage): int
{
	$stages = array_keys(epc_order_supplier_fulfillment_stages());
	$i = array_search($stage, $stages, true);
	return $i === false ? -1 : (int) $i;
}

function epc_order_supplier_fulfillment_ensure_schema(PDO $db): void
{
	$db->exec(
		"CREATE TABLE IF NOT EXISTS `epc_order_supplier_fulfillment` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`order_id` int(11) NOT NULL,
			`supplier_key` varchar(64) NOT NULL DEFAULT '',
			`supplier_id` int(11) NOT NULL DEFAULT 0,
			`storage_id` int(11) NOT NULL DEFAULT 0,
			`supplier_name` varchar(255) NOT NULL DEFAULT '',
			`stage` varchar(48) NOT NULL DEFAULT 'supplier_confirm',
			`po_id` int(11) NOT NULL DEFAULT 0,
			`notes` varchar(512) NOT NULL DEFAULT '',
			`time_updated` int(11) NOT NULL DEFAULT 0,
			`updated_by` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `u_order_supplier` (`order_id`, `supplier_key`),
			KEY `x_order` (`order_id`),
			KEY `x_stage` (`stage`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='OMS per-supplier fulfillment stages'"
	);
}

/**
 * Effective unit purchase for margin: details → APAI cost → storage data → t2_price_purchase.
 *
 * @return array{unit:float,source:string,stored:float,sell:float}
 */
function epc_order_item_effective_purchase(PDO $db, array $item): array
{
	$itemId = (int) ($item['id'] ?? 0);
	$sell = round((float) ($item['price'] ?? 0), 4);
	$stored = round((float) ($item['t2_price_purchase'] ?? 0), 4);
	$unit = $stored;
	$source = 't2_price_purchase';

	if ($itemId > 0) {
		try {
			$st = $db->prepare(
				'SELECT IFNULL(SUM(`price_purchase` * GREATEST(`count_reserved` + `count_issued`, 1)) /
					NULLIF(SUM(GREATEST(`count_reserved` + `count_issued`, 1)), 0), 0)
				 FROM `shop_orders_items_details` WHERE `order_item_id` = ? AND `price_purchase` > 0'
			);
			$st->execute(array($itemId));
			$avg = round((float) $st->fetchColumn(), 4);
			if ($avg > 0 && ($stored <= 0 || abs($stored - $sell) < 0.0001 || $avg < $stored - 0.0001)) {
				$unit = $avg;
				$source = 'order_item_details';
			}
		} catch (Throwable $e) {
		}
	}

	$meta = array();
	$json = (string) ($item['t2_json_params'] ?? '');
	if ($json !== '') {
		$decoded = json_decode($json, true);
		if (is_array($decoded)) {
			$meta = $decoded;
		}
	}
	$apaiCost = round((float) ($meta['apai_cost'] ?? $meta['import_warehouse_cost'] ?? 0), 4);
	if ($apaiCost > 0 && ($unit <= 0 || abs($unit - $sell) < 0.0001 || $apaiCost < $unit - 0.0001)) {
		$unit = $apaiCost;
		$source = 'apai_cost';
	}

	if (($unit <= 0 || abs($unit - $sell) < 0.0001)) {
		$storageId = (int) ($item['t2_storage_id'] ?? 0);
		$productId = (int) ($item['product_id'] ?? 0);
		try {
			if ($productId > 0) {
				$q = $db->prepare(
					'SELECT MAX(`price_purchase`) FROM `shop_storages_data`
					 WHERE `product_id` = ? AND `price_purchase` > 0'
					. ($storageId > 0 ? ' AND `storage_id` = ?' : '')
				);
				$q->execute($storageId > 0 ? array($productId, $storageId) : array($productId));
				$wh = round((float) $q->fetchColumn(), 4);
				if ($wh > 0) {
					$unit = $wh;
					$source = 'shop_storages_data';
				}
			}
		} catch (Throwable $e) {
		}
	}

	if ($unit <= 0) {
		$unit = $stored > 0 ? $stored : 0.0;
		$source = 't2_price_purchase';
	}

	return array(
		'unit' => round($unit, 4),
		'source' => $source,
		'stored' => $stored,
		'sell' => $sell,
	);
}

function epc_order_supplier_fulfillment_resolve_groups(PDO $db, int $orderId): array
{
	require_once __DIR__ . '/epc_erp_order_fulfillment.php';
	$items = epc_erp_order_fulfillment_load_items($db, $orderId);
	$groups = array();
	foreach ($items as $item) {
		$resolved = epc_erp_order_fulfillment_resolve_line_supplier($db, $item);
		$storageId = (int) $resolved['storage_id'];
		$supplierId = (int) $resolved['supplier_id'];
		if ($supplierId <= 0 && $storageId <= 0) {
			$key = 'unassigned';
		} else {
			$key = $supplierId > 0 ? ('s' . $supplierId) : ('w' . $storageId);
		}
		if (!isset($groups[$key])) {
			$groups[$key] = array(
				'supplier_key' => $key,
				'supplier_id' => $supplierId,
				'storage_id' => $storageId,
				'supplier_name' => (string) ($resolved['supplier_name'] !== '' ? $resolved['supplier_name'] : ($item['t2_storage'] ?? 'Supplier')),
				'item_ids' => array(),
				'lines' => 0,
				'sell_sum' => 0.0,
				'purchase_sum' => 0.0,
			);
		}
		$qty = max(1, (int) ($item['count_need'] ?? 1));
		$eff = epc_order_item_effective_purchase($db, $item);
		$groups[$key]['item_ids'][] = (int) $item['id'];
		$groups[$key]['lines']++;
		$groups[$key]['sell_sum'] += round((float) $item['price'] * $qty, 2);
		$groups[$key]['purchase_sum'] += round((float) $eff['unit'] * $qty, 2);
		if ($groups[$key]['supplier_name'] === '' || $groups[$key]['supplier_name'] === 'Unassigned supplier') {
			$label = trim((string) ($item['t2_storage'] ?? ''));
			if ($label !== '') {
				$groups[$key]['supplier_name'] = $label;
			}
		}
	}
	return array_values($groups);
}

function epc_order_supplier_fulfillment_bootstrap(PDO $db, int $orderId, int $adminId = 0): array
{
	epc_order_supplier_fulfillment_ensure_schema($db);
	$groups = epc_order_supplier_fulfillment_resolve_groups($db, $orderId);
	$now = time();
	$poBySupplier = array();
	try {
		require_once __DIR__ . '/epc_erp_order_fulfillment.php';
		$st = $db->prepare(
			'SELECT `id`, `supplier_id` FROM `epc_erp_purchase_orders`
			 WHERE `order_id` = ? AND `status` != \'cancelled\' ORDER BY `id`'
		);
		$st->execute(array($orderId));
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$sid = (int) $row['supplier_id'];
			if ($sid > 0 && !isset($poBySupplier[$sid])) {
				$poBySupplier[$sid] = (int) $row['id'];
			}
		}
	} catch (Throwable $e) {
	}

	foreach ($groups as $g) {
		$key = (string) $g['supplier_key'];
		$poId = 0;
		if (!empty($g['supplier_id']) && isset($poBySupplier[(int) $g['supplier_id']])) {
			$poId = (int) $poBySupplier[(int) $g['supplier_id']];
		}
		$db->prepare(
			'INSERT INTO `epc_order_supplier_fulfillment`
			(`order_id`, `supplier_key`, `supplier_id`, `storage_id`, `supplier_name`, `stage`, `po_id`, `time_updated`, `updated_by`)
			VALUES (?, ?, ?, ?, ?, \'supplier_confirm\', ?, ?, ?)
			ON DUPLICATE KEY UPDATE
			`supplier_id` = VALUES(`supplier_id`),
			`storage_id` = VALUES(`storage_id`),
			`supplier_name` = VALUES(`supplier_name`),
			`po_id` = IF(VALUES(`po_id`) > 0, VALUES(`po_id`), `po_id`),
			`time_updated` = VALUES(`time_updated`)'
		)->execute(array(
			$orderId,
			$key,
			(int) $g['supplier_id'],
			(int) $g['storage_id'],
			(string) $g['supplier_name'],
			$poId,
			$now,
			$adminId,
		));
	}
	return epc_order_supplier_fulfillment_status($db, $orderId);
}

function epc_order_supplier_fulfillment_set_stage(
	PDO $db,
	int $orderId,
	string $supplierKey,
	string $stage,
	int $adminId = 0,
	string $notes = ''
): array {
	epc_order_supplier_fulfillment_ensure_schema($db);
	$stages = epc_order_supplier_fulfillment_stages();
	if (!isset($stages[$stage])) {
		throw new Exception('Unknown fulfillment stage');
	}
	epc_order_supplier_fulfillment_bootstrap($db, $orderId, $adminId);
	$st = $db->prepare(
		'UPDATE `epc_order_supplier_fulfillment`
		 SET `stage` = ?, `notes` = ?, `time_updated` = ?, `updated_by` = ?
		 WHERE `order_id` = ? AND `supplier_key` = ?'
	);
	$st->execute(array($stage, mb_substr($notes, 0, 512), time(), $adminId, $orderId, $supplierKey));
	if ($st->rowCount() < 1) {
		throw new Exception('Supplier fulfillment row not found for this order');
	}
	return epc_order_supplier_fulfillment_status($db, $orderId);
}

function epc_order_supplier_fulfillment_advance(
	PDO $db,
	int $orderId,
	string $supplierKey,
	int $adminId = 0
): array {
	epc_order_supplier_fulfillment_ensure_schema($db);
	epc_order_supplier_fulfillment_bootstrap($db, $orderId, $adminId);
	$st = $db->prepare(
		'SELECT `stage` FROM `epc_order_supplier_fulfillment` WHERE `order_id` = ? AND `supplier_key` = ? LIMIT 1'
	);
	$st->execute(array($orderId, $supplierKey));
	$cur = (string) $st->fetchColumn();
	$keys = array_keys(epc_order_supplier_fulfillment_stages());
	$idx = epc_order_supplier_fulfillment_stage_index($cur !== '' ? $cur : 'supplier_confirm');
	if ($idx < 0) {
		$idx = 0;
	}
	if ($idx >= count($keys) - 1) {
		return epc_order_supplier_fulfillment_status($db, $orderId);
	}
	return epc_order_supplier_fulfillment_set_stage($db, $orderId, $supplierKey, $keys[$idx + 1], $adminId);
}

/**
 * @return array{order_id:int,suppliers:array,stages:array,rollup:string}
 */
function epc_order_supplier_fulfillment_status(PDO $db, int $orderId): array
{
	epc_order_supplier_fulfillment_ensure_schema($db);
	$groups = epc_order_supplier_fulfillment_resolve_groups($db, $orderId);
	$existing = array();
	$st = $db->prepare('SELECT * FROM `epc_order_supplier_fulfillment` WHERE `order_id` = ?');
	$st->execute(array($orderId));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$existing[(string) $row['supplier_key']] = $row;
	}

	$stages = epc_order_supplier_fulfillment_stages();
	$suppliers = array();
	$minIdx = null;
	foreach ($groups as $g) {
		$key = (string) $g['supplier_key'];
		$row = $existing[$key] ?? null;
		$stage = $row ? (string) $row['stage'] : 'supplier_confirm';
		if (!isset($stages[$stage])) {
			$stage = 'supplier_confirm';
		}
		$idx = epc_order_supplier_fulfillment_stage_index($stage);
		if ($minIdx === null || $idx < $minIdx) {
			$minIdx = $idx;
		}
		$keys = array_keys($stages);
		$next = ($idx >= 0 && $idx < count($keys) - 1) ? $keys[$idx + 1] : '';
		$suppliers[] = array(
			'supplier_key' => $key,
			'supplier_id' => (int) $g['supplier_id'],
			'storage_id' => (int) $g['storage_id'],
			'supplier_name' => (string) $g['supplier_name'],
			'lines' => (int) $g['lines'],
			'item_ids' => $g['item_ids'],
			'sell_sum' => round((float) $g['sell_sum'], 2),
			'purchase_sum' => round((float) $g['purchase_sum'], 2),
			'margin' => round((float) $g['sell_sum'] - (float) $g['purchase_sum'], 2),
			'stage' => $stage,
			'stage_label' => $stages[$stage],
			'stage_index' => $idx,
			'next_stage' => $next,
			'next_label' => $next !== '' ? $stages[$next] : '',
			'po_id' => (int) ($row['po_id'] ?? 0),
			'time_updated' => (int) ($row['time_updated'] ?? 0),
			'pipeline' => (function () use ($stages, $idx) {
				$pipe = array();
				$i = 0;
				foreach ($stages as $sk => $label) {
					$pipe[] = array(
						'key' => $sk,
						'label' => $label,
						'done' => ($idx >= $i),
						'current' => ($idx === $i),
					);
					$i++;
				}
				return $pipe;
			})(),
		);
	}

	$rollup = 'none';
	if ($suppliers) {
		$allComplete = true;
		$anyStarted = false;
		foreach ($suppliers as $s) {
			if ($s['stage'] !== 'complete') {
				$allComplete = false;
			}
			if ($s['stage_index'] > 0) {
				$anyStarted = true;
			}
		}
		if ($allComplete) {
			$rollup = 'complete';
		} elseif ($anyStarted) {
			$rollup = 'in_progress';
		} else {
			$rollup = 'awaiting_confirm';
		}
	}

	return array(
		'order_id' => $orderId,
		'suppliers' => $suppliers,
		'stages' => $stages,
		'rollup' => $rollup,
		'supplier_count' => count($suppliers),
	);
}
