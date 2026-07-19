<?php
/**
 * Order courier (customer-paid delivery) + destination VAT + ERP document map.
 *
 * Rules:
 * - Customer pays courier; fee is stored on how_get_json and billed on the tax invoice.
 * - Courier is taxable income in UAE (output VAT).
 * - Destination / buyer outside UAE → zero-rated (no VAT) on goods and courier.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_uae_customer_vat.php';

function epc_order_how_get_array($order_or_json): array
{
	if (is_array($order_or_json)) {
		if (isset($order_or_json['how_get_json'])) {
			$raw = $order_or_json['how_get_json'];
		} else {
			return $order_or_json;
		}
	} else {
		$raw = $order_or_json;
	}
	if (is_array($raw)) {
		return $raw;
	}
	if (!is_string($raw) || trim($raw) === '') {
		return array();
	}
	$decoded = json_decode($raw, true);
	return is_array($decoded) ? $decoded : array();
}

/** Shipping / buyer destination ISO country for VAT. */
function epc_order_destination_country(PDO $db, array $order, int $user_id = 0): string
{
	$how = epc_order_how_get_array($order);
	$ship = strtoupper(trim((string) ($how['country'] ?? $how['country_code'] ?? '')));
	if (strlen($ship) === 2) {
		return epc_uae_vat_normalize_country($ship);
	}

	if ($user_id <= 0) {
		$user_id = (int) ($order['user_id'] ?? 0);
	}
	$ctx = epc_uae_customer_vat_context($db, $user_id);
	$country = (string) ($ctx['country_code'] ?? 'AE');
	return epc_uae_vat_normalize_country($country !== '' ? $country : 'AE');
}

/**
 * Transaction flags for an order (export / zero-rate when destination ≠ AE).
 *
 * @return array<string,bool>
 */
function epc_order_vat_transaction_flags(PDO $db, array $order, int $user_id = 0, array $extra = array()): array
{
	$flags = is_array($extra) ? $extra : array();
	$dest = epc_order_destination_country($db, $order, $user_id);
	if (!epc_uae_vat_is_uae_country($dest)) {
		$flags['exports'] = true;
	}
	return $flags;
}

/**
 * Customer-paid courier fee from how_get_json (delivery_price or rate).
 *
 * @return array{amount:float,carrier:string,service:string,country:string,city:string,payer:string}
 */
function epc_order_courier_charge(array $order): array
{
	$how = epc_order_how_get_array($order);
	$amount = 0.0;
	if (isset($how['delivery_price']) && is_numeric($how['delivery_price'])) {
		$amount = (float) $how['delivery_price'];
	} elseif (isset($how['rate']) && is_numeric($how['rate'])) {
		$amount = (float) $how['rate'];
	}
	$amount = round(max(0, $amount), 2);

	return array(
		'amount' => $amount,
		'carrier' => trim((string) ($how['carrier'] ?? '')),
		'service' => trim((string) ($how['service'] ?? '')),
		'country' => strtoupper(trim((string) ($how['country'] ?? ''))),
		'city' => trim((string) ($how['city'] ?? '')),
		'payer' => 'customer',
	);
}

/**
 * VAT on courier fee. Quoted delivery_price is treated as ex-VAT income;
 * UAE supplies add output VAT; export / non-UAE destinations are zero-rated.
 *
 * @return array{unit_net:float,line_net:float,vat_amount:float,gross:float,tax_rate:float,tax_category:string,destination_country:string,vat_type:string,charges_customer:bool}
 */
function epc_order_courier_vat_amounts(PDO $db, array $order, int $user_id = 0, array $transaction_flags = array()): array
{
	if ($user_id <= 0) {
		$user_id = (int) ($order['user_id'] ?? 0);
	}
	$courier = epc_order_courier_charge($order);
	$dest = epc_order_destination_country($db, $order, $user_id);
	$flags = epc_order_vat_transaction_flags($db, $order, $user_id, $transaction_flags);

	$buyer = array(
		'country_code' => $dest,
		'buyer_country_code' => $dest,
	);
	$supply = epc_uae_vat_supply_tax_category($db, $buyer, $flags);
	$taxRate = (float) $supply['tax_rate'];
	$taxCat = (string) $supply['tax_category'];
	$net = (float) $courier['amount'];

	$vatAmt = 0.0;
	$gross = $net;
	if ($net > 0 && $taxRate > 0 && epc_uae_vat_sales_enabled($db)) {
		$vatAmt = round($net * ($taxRate / 100), 2);
		$gross = round($net + $vatAmt, 2);
	}

	$resolved = epc_uae_customer_vat_resolve($db, $user_id);

	return array(
		'unit_net' => $net,
		'line_net' => $net,
		'vat_amount' => $vatAmt,
		'gross' => $gross,
		'tax_rate' => ($net > 0 ? $taxRate : 0.0),
		'tax_category' => $taxCat,
		'destination_country' => $dest,
		'vat_type' => (string) ($resolved['vat_type'] ?? ''),
		'vat_type_label' => (string) ($resolved['vat_type_label'] ?? ''),
		'charges_customer' => true,
		'carrier' => $courier['carrier'],
		'service' => $courier['service'],
		'city' => $courier['city'],
	);
}

/**
 * Invoice line payload for courier (or null when fee is zero).
 *
 * @return array<string,mixed>|null
 */
function epc_order_courier_invoice_line(PDO $db, array $order, int $line_no, int $user_id = 0, array $transaction_flags = array()): ?array
{
	$calc = epc_order_courier_vat_amounts($db, $order, $user_id, $transaction_flags);
	if ((float) $calc['line_net'] <= 0) {
		return null;
	}
	$label = 'Courier / delivery (customer paid)';
	$parts = array();
	if ($calc['carrier'] !== '') {
		$parts[] = strtoupper((string) $calc['carrier']);
	}
	if ($calc['service'] !== '') {
		$parts[] = (string) $calc['service'];
	}
	if ($calc['destination_country'] !== '') {
		$parts[] = 'to ' . $calc['destination_country'];
	}
	$desc = $parts ? implode(' · ', $parts) : 'Shipping charge billed to customer';

	return array(
		'line_no' => $line_no,
		'item_name' => $label,
		'item_description' => $desc,
		'item_type' => 'S',
		'quantity' => 1,
		'uom_code' => 'C62',
		'unit_price' => (float) $calc['unit_net'],
		'line_net' => (float) $calc['line_net'],
		'tax_category' => (string) $calc['tax_category'],
		'tax_rate' => (float) $calc['tax_rate'],
		'tax_amount' => (float) $calc['vat_amount'],
		'gross_amount' => (float) $calc['gross'],
		'vat_line_aed' => (float) $calc['vat_amount'],
		'line_amount_aed' => (float) $calc['gross'],
		'is_courier' => 1,
	);
}

/**
 * Persist courier fee (+ optional destination) onto shop_orders.how_get_json.
 */
function epc_order_set_courier_charge(PDO $db, int $order_id, float $amount, array $extra = array()): array
{
	$st = $db->prepare('SELECT `how_get_json` FROM `shop_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($order_id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Order not found');
	}
	$how = epc_order_how_get_array($row);
	$how['delivery_price'] = round(max(0, $amount), 2);
	$how['rate'] = $how['delivery_price'];
	$how['courier_payer'] = 'customer';
	foreach (array('country', 'city', 'carrier', 'service', 'address', 'phone', 'weight_kg') as $k) {
		if (isset($extra[$k]) && $extra[$k] !== '' && $extra[$k] !== null) {
			$how[$k] = is_string($extra[$k]) ? trim((string) $extra[$k]) : $extra[$k];
		}
	}
	if (isset($how['country'])) {
		$how['country'] = strtoupper(substr((string) $how['country'], 0, 2));
	}
	$db->prepare('UPDATE `shop_orders` SET `how_get_json` = ? WHERE `id` = ?')
		->execute(array(json_encode($how, JSON_UNESCAPED_UNICODE), $order_id));
	return $how;
}

/**
 * OMS / ERP document map: Order ↔ VAT ↔ SO ↔ Supplier POs ↔ AR invoice ↔ AP bills.
 *
 * @return array<string,mixed>
 */
function epc_order_erp_document_map(PDO $db, int $order_id): array
{
	require_once __DIR__ . '/epc_erp_order_fulfillment.php';

	$ost = $db->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1');
	$ost->execute(array($order_id));
	$order = $ost->fetch(PDO::FETCH_ASSOC);
	if (!$order) {
		throw new Exception('Order not found');
	}

	$user_id = (int) ($order['user_id'] ?? 0);
	$fulfillment = epc_erp_order_fulfillment_status($db, $order_id);
	$courier = epc_order_courier_vat_amounts($db, $order, $user_id);
	$vatResolved = epc_uae_customer_vat_resolve($db, $user_id);
	$dest = (string) $courier['destination_country'];
	$zeroRated = !epc_uae_vat_is_uae_country($dest);

	$chain = array(
		array(
			'key' => 'shop_order',
			'label' => 'Shop order',
			'ref' => '#' . $order_id,
			'id' => $order_id,
			'status' => ((int) ($order['paid'] ?? 0) === 1) ? 'paid' : 'open',
		),
		array(
			'key' => 'vat',
			'label' => 'VAT treatment',
			'ref' => $zeroRated ? ('Zero-rated export (' . $dest . ')') : ('UAE standard (' . ($courier['tax_rate'] ?: 5) . '%)'),
			'id' => 0,
			'status' => (string) ($vatResolved['vat_type'] ?? ''),
			'detail' => (string) ($vatResolved['vat_type_label'] ?? ''),
		),
		array(
			'key' => 'sales_order',
			'label' => 'ERP sales order',
			'ref' => !empty($fulfillment['sales_order']['so_no'])
				? (string) $fulfillment['sales_order']['so_no']
				: (!empty($fulfillment['sales_order']['id']) ? ('SO #' . (int) $fulfillment['sales_order']['id']) : '—'),
			'id' => (int) ($fulfillment['sales_order']['id'] ?? 0),
			'status' => (string) ($fulfillment['sales_order']['status'] ?? ($fulfillment['fulfillment_status'] ?? 'none')),
		),
		array(
			'key' => 'purchase_orders',
			'label' => 'Supplier POs',
			'ref' => (string) count($fulfillment['purchase_orders'] ?? array()) . ' PO(s)',
			'id' => 0,
			'status' => (string) ($fulfillment['fulfillment_status'] ?? ''),
			'items' => array_map(function ($po) {
				return array(
					'id' => (int) ($po['id'] ?? 0),
					'ref' => (string) ($po['po_no'] ?? ('PO #' . (int) ($po['id'] ?? 0))),
					'supplier' => (string) ($po['supplier_name'] ?? ''),
					'status' => (string) ($po['status'] ?? ''),
					'purchase_id' => (int) ($po['purchase_id'] ?? 0),
				);
			}, $fulfillment['purchase_orders'] ?? array()),
		),
		array(
			'key' => 'ap_bills',
			'label' => 'AP bills (supplier invoices)',
			'ref' => (string) count($fulfillment['purchase_invoices'] ?? array()) . ' bill(s)',
			'id' => 0,
			'status' => !empty($fulfillment['accounting']['cost_posted']) ? 'posted' : 'pending',
			'items' => array_map(function ($pi) {
				return array(
					'id' => (int) ($pi['id'] ?? 0),
					'ref' => (string) ($pi['invoice_number'] ?? ('Bill #' . (int) ($pi['id'] ?? 0))),
					'total' => (float) ($pi['total_amount'] ?? 0),
					'status' => (string) ($pi['status'] ?? ''),
					'po_id' => (int) ($pi['po_id'] ?? 0),
				);
			}, $fulfillment['purchase_invoices'] ?? array()),
		),
		array(
			'key' => 'ar_invoice',
			'label' => 'AR tax invoice (customer)',
			'ref' => !empty($fulfillment['sales_invoice']['invoice_number'])
				? (string) $fulfillment['sales_invoice']['invoice_number']
				: '—',
			'id' => (int) ($fulfillment['sales_invoice']['id'] ?? 0),
			'status' => !empty($fulfillment['sales_invoice']['validation_ok']) ? 'validated' : (!empty($fulfillment['sales_invoice']) ? 'draft' : 'none'),
		),
		array(
			'key' => 'courier',
			'label' => 'Courier (customer pays)',
			'ref' => number_format((float) $courier['line_net'], 2, '.', '') . ' + VAT '
				. number_format((float) $courier['vat_amount'], 2, '.', '')
				. ' = ' . number_format((float) $courier['gross'], 2, '.', '') . ' AED',
			'id' => 0,
			'status' => ((float) $courier['line_net'] > 0) ? 'billed' : 'none',
			'detail' => $zeroRated
				? 'Outside UAE — no VAT on courier'
				: 'UAE — VAT on courier is output tax (income)',
		),
	);

	return array(
		'shop_order_id' => $order_id,
		'user_id' => $user_id,
		'destination_country' => $dest,
		'vat' => array(
			'type' => (string) ($vatResolved['vat_type'] ?? ''),
			'label' => (string) ($vatResolved['vat_type_label'] ?? ''),
			'zero_rated' => $zeroRated,
			'documentation' => $zeroRated
				? 'Export / non-UAE supply: zero-rated (tax category Z). Keep shipping docs, commercial invoice, and buyer country evidence for FTA.'
				: 'UAE domestic supply: standard rate on goods and courier. Tax invoice (PINT-AE) required; keep TRN and invoice XML/PDF.',
		),
		'courier' => $courier,
		'fulfillment' => $fulfillment,
		'chain' => $chain,
	);
}
