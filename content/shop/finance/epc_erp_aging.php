<?php
/**
 * AR / AP / Inventory aging analysis.
 *
 * Aging bucket boundaries are tenant-configurable (Accounting setup →
 * setting key `aging_buckets`, default "30,60,90"); nothing is hard-coded
 * as a global constant. Each function returns real per-document data pulled
 * from this tenant's own database.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_extended.php';

/**
 * Read the three aging boundaries (in days) from tenant settings.
 * Returns a sorted array of three positive ints, e.g. array(30,60,90).
 */
function epc_erp_aging_boundaries(PDO $db): array
{
	$raw = trim((string) epc_erp_platform_setting_get($db, 'aging_buckets', '30,60,90'));
	$parts = array_values(array_filter(array_map('intval', explode(',', $raw)), function ($n) {
		return $n > 0;
	}));
	if (count($parts) < 3) {
		$parts = array(30, 60, 90);
	}
	$parts = array_slice($parts, 0, 3);
	sort($parts);
	return $parts;
}

/**
 * Human labels for the five buckets given boundaries [b1,b2,b3].
 * @param bool $overdue  true => AR/AP style ("Not due" + overdue ranges),
 *                       false => inventory style (absolute stock age).
 */
function epc_erp_aging_bucket_labels(array $b, bool $overdue = true): array
{
	if ($overdue) {
		return array(
			'Not due',
			'1-' . $b[0],
			($b[0] + 1) . '-' . $b[1],
			($b[1] + 1) . '-' . $b[2],
			$b[2] . '+',
		);
	}
	return array(
		'0-' . $b[0],
		($b[0] + 1) . '-' . $b[1],
		($b[1] + 1) . '-' . $b[2],
		($b[2] + 1) . '-' . ($b[2] * 2),
		($b[2] * 2) . '+',
	);
}

/**
 * Map a day count to a bucket index 0..4.
 * @param bool $overdue  current(<=0) lands in bucket 0 when true; otherwise
 *                       absolute age where bucket 0 is the youngest range.
 */
function epc_erp_aging_bucket_index(int $days, array $b, bool $overdue = true): int
{
	if ($overdue) {
		if ($days <= 0) {
			return 0;
		}
		if ($days <= $b[0]) {
			return 1;
		}
		if ($days <= $b[1]) {
			return 2;
		}
		if ($days <= $b[2]) {
			return 3;
		}
		return 4;
	}
	if ($days <= $b[0]) {
		return 0;
	}
	if ($days <= $b[1]) {
		return 1;
	}
	if ($days <= $b[2]) {
		return 2;
	}
	if ($days <= $b[2] * 2) {
		return 3;
	}
	return 4;
}

function epc_erp_aging_empty_buckets(): array
{
	return array(0.0, 0.0, 0.0, 0.0, 0.0);
}

/**
 * Receivables aging by customer, sourced from outstanding sales invoices
 * (epc_einvoice_documents). Outstanding = total incl VAT − paid amount,
 * aged from the payment due date (falls back to issue date).
 *
 * @return array{boundaries:array,labels:array,rows:array,totals:array,grand:float}
 */
function epc_erp_ar_aging(PDO $db): array
{
	$b = epc_erp_aging_boundaries($db);
	$labels = epc_erp_aging_bucket_labels($b, true);
	$now = time();
	$rows = array();
	$totals = epc_erp_aging_empty_buckets();
	$grand = 0.0;

	$hasTable = $db->query("SHOW TABLES LIKE 'epc_einvoice_documents'")->fetchColumn();
	if ($hasTable) {
		$sql = "SELECT d.`user_id`, d.`issue_date`, d.`payment_due_date`,
				d.`total_incl_vat`, d.`paid_amount`, u.`email`
			FROM `epc_einvoice_documents` d
			LEFT JOIN `users` u ON u.`user_id` = d.`user_id`
			WHERE d.`active` = 1 AND d.`status` <> 'cancelled'
			  AND d.`doc_category` IN ('tax_invoice','commercial_invoice')";
		foreach ($db->query($sql) as $r) {
			$outstanding = round((float) $r['total_incl_vat'] - (float) $r['paid_amount'], 2);
			if ($outstanding <= 0.005) {
				continue;
			}
			$due = (int) $r['payment_due_date'] > 0 ? (int) $r['payment_due_date'] : (int) $r['issue_date'];
			$days = $due > 0 ? (int) floor(($now - $due) / 86400) : 0;
			$idx = epc_erp_aging_bucket_index($days, $b, true);
			$key = (int) $r['user_id'];
			if (!isset($rows[$key])) {
				$rows[$key] = array(
					'name' => $r['email'] ?: ('User #' . $key),
					'buckets' => epc_erp_aging_empty_buckets(),
					'total' => 0.0,
				);
			}
			$rows[$key]['buckets'][$idx] += $outstanding;
			$rows[$key]['total'] += $outstanding;
			$totals[$idx] += $outstanding;
			$grand += $outstanding;
		}
	}
	usort($rows, function ($a, $z) {
		return $z['total'] <=> $a['total'];
	});
	return array('boundaries' => $b, 'labels' => $labels, 'rows' => array_values($rows), 'totals' => $totals, 'grand' => round($grand, 2));
}

/**
 * Payables aging by supplier, sourced from outstanding purchase invoices
 * (epc_erp_purchases). Outstanding = invoice total − payments recorded
 * against it in the supplier ledger, aged from the purchase (invoice) date.
 */
function epc_erp_ap_aging(PDO $db): array
{
	$b = epc_erp_aging_boundaries($db);
	$labels = epc_erp_aging_bucket_labels($b, true);
	$now = time();
	$rows = array();
	$totals = epc_erp_aging_empty_buckets();
	$grand = 0.0;

	$hasTable = $db->query("SHOW TABLES LIKE 'epc_erp_purchases'")->fetchColumn();
	if ($hasTable) {
		$sql = "SELECT p.`id`, p.`supplier_id`, p.`purchase_date`, p.`total_amount`, s.`name`,
				IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a
					WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0) AS paid
			FROM `epc_erp_purchases` p
			LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`
			WHERE p.`active` = 1 AND p.`status` <> 'draft'";
		foreach ($db->query($sql) as $r) {
			$outstanding = round((float) $r['total_amount'] - (float) $r['paid'], 2);
			if ($outstanding <= 0.005) {
				continue;
			}
			$d = (int) $r['purchase_date'];
			$days = $d > 0 ? (int) floor(($now - $d) / 86400) : 0;
			$idx = epc_erp_aging_bucket_index($days, $b, true);
			$key = (int) $r['supplier_id'];
			if (!isset($rows[$key])) {
				$rows[$key] = array(
					'name' => $r['name'] ?: ('Supplier #' . $key),
					'buckets' => epc_erp_aging_empty_buckets(),
					'total' => 0.0,
				);
			}
			$rows[$key]['buckets'][$idx] += $outstanding;
			$rows[$key]['total'] += $outstanding;
			$totals[$idx] += $outstanding;
			$grand += $outstanding;
		}
	}
	usort($rows, function ($a, $z) {
		return $z['total'] <=> $a['total'];
	});
	return array('boundaries' => $b, 'labels' => $labels, 'rows' => array_values($rows), 'totals' => $totals, 'grand' => round($grand, 2));
}

/**
 * Inventory aging by item — how long on-hand stock value has been sitting.
 * Age is measured from the most recent inbound movement for each item
 * (purchase/opening/transfer/return in), falling back to the stock row's
 * last update. Value = qty on hand × average unit cost.
 */
function epc_erp_inventory_aging(PDO $db): array
{
	$b = epc_erp_aging_boundaries($db);
	$labels = epc_erp_aging_bucket_labels($b, false);
	$now = time();
	$rows = array();
	$totals = epc_erp_aging_empty_buckets();
	$grand = 0.0;

	$hasTable = $db->query("SHOW TABLES LIKE 'epc_erp_inv_stock'")->fetchColumn();
	if ($hasTable) {
		$sql = "SELECT st.`item_id`, st.`qty_on_hand`, st.`avg_unit_cost`, st.`time_updated`, it.`name`, it.`sku`,
				(SELECT MAX(m.`movement_date`) FROM `epc_erp_inv_movements` m
					WHERE m.`item_id` = st.`item_id` AND m.`active` = 1
					  AND m.`movement_type` IN ('opening','purchase_in','transfer_in','return_in')) AS last_in
			FROM `epc_erp_inv_stock` st
			LEFT JOIN `epc_erp_inv_items` it ON it.`id` = st.`item_id`
			WHERE st.`qty_on_hand` > 0";
		foreach ($db->query($sql) as $r) {
			$value = round((float) $r['qty_on_hand'] * (float) $r['avg_unit_cost'], 2);
			if ($value <= 0.005) {
				continue;
			}
			$ref = (int) $r['last_in'] > 0 ? (int) $r['last_in'] : (int) $r['time_updated'];
			$days = $ref > 0 ? (int) floor(($now - $ref) / 86400) : 0;
			$idx = epc_erp_aging_bucket_index($days, $b, false);
			$key = (int) $r['item_id'];
			if (!isset($rows[$key])) {
				$rows[$key] = array(
					'name' => trim(($r['sku'] ? $r['sku'] . ' — ' : '') . ($r['name'] ?: ('Item #' . $key))),
					'buckets' => epc_erp_aging_empty_buckets(),
					'total' => 0.0,
				);
			}
			$rows[$key]['buckets'][$idx] += $value;
			$rows[$key]['total'] += $value;
			$totals[$idx] += $value;
			$grand += $value;
		}
	}
	usort($rows, function ($a, $z) {
		return $z['total'] <=> $a['total'];
	});
	return array('boundaries' => $b, 'labels' => $labels, 'rows' => array_values($rows), 'totals' => $totals, 'grand' => round($grand, 2));
}
