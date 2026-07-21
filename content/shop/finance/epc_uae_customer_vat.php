<?php
/**
 * UAE customer VAT — storefront display (incl/excl), customer_vat_type on login/register,
 * checkout and e-invoice line treatment (Amazon / Noon style B2C inclusive pricing).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_uae_vat.php';
require_once __DIR__ . '/epc_uae_tax_compliance.php';
require_once __DIR__ . '/../pricing/epc_pricing.php';

function epc_uae_customer_vat_types(): array
{
	return array(
		'local_b2c' => 'UAE retail (B2C) — prices incl. VAT',
		'local_b2b' => 'UAE business (B2B) — prices excl. VAT',
		'gcc' => 'GCC buyer — export / zero-rated',
		'export' => 'Export — zero-rated',
		'tax_exempt' => 'Tax-exempt certificate',
	);
}

function epc_uae_customer_vat_gcc_codes(): array
{
	return array('SA', 'BH', 'OM', 'KW', 'QA');
}

function epc_uae_customer_vat_is_gcc(?string $code): bool
{
	$code = strtoupper(trim((string)$code));
	return $code !== '' && $code !== 'AE' && in_array($code, epc_uae_customer_vat_gcc_codes(), true);
}

function epc_uae_customer_vat_profile_key(): string
{
	return 'customer_vat_type';
}

function epc_uae_customer_vat_get_stored(PDO $db, int $user_id): string
{
	if ($user_id <= 0) {
		return '';
	}
	require_once __DIR__ . '/../pricing/epc_customer_trade.php';
	return epc_trade_profile_get($db, $user_id, epc_uae_customer_vat_profile_key(), '');
}

function epc_uae_customer_vat_save(PDO $db, int $user_id, string $vat_type): void
{
	if ($user_id <= 0) {
		return;
	}
	require_once __DIR__ . '/../pricing/epc_customer_trade.php';
	epc_trade_profile_set($db, $user_id, epc_uae_customer_vat_profile_key(), $vat_type);
}

/** Infer customer country, TRN, trade type from profiles + e-invoice buyer. */
function epc_uae_customer_vat_context(PDO $db, int $user_id): array
{
	require_once __DIR__ . '/../pricing/epc_customer_trade.php';

	$country = 'AE';
	$trn = '';
	$customer_type = 'retail';
	$tax_exempt = false;

	if ($user_id > 0) {
		$customer_type = epc_trade_profile_get($db, $user_id, 'epc_customer_type', 'retail');
		if ($customer_type === '') {
			$customer_type = 'retail';
		}
		$country = strtoupper(trim(epc_trade_profile_get($db, $user_id, 'epc_reg_country', 'AE')));
		if ($country === '') {
			$country = 'AE';
		}
		$trn = preg_replace('/\D/', '', epc_trade_profile_get($db, $user_id, 'epc_reg_trn', ''));
		if ($trn === '') {
			$trnRaw = epc_trade_profile_get($db, $user_id, 'epc_reg_trn', '');
			if ($trnRaw !== '' && !ctype_digit($trnRaw)) {
				$trn = trim($trnRaw);
			}
		}
		$taxStatus = epc_trade_profile_get($db, $user_id, 'epc_tax_exempt_cert_status', '');
		$tax_exempt = ($customer_type === 'wholesale' && $taxStatus === 'approved');

		$einvoicePath = __DIR__ . '/epc_einvoice.php';
		if (is_readable($einvoicePath)) {
			require_once $einvoicePath;
			$buyer = epc_einvoice_buyer_profile($db, $user_id);
			if (!empty($buyer['country_code'])) {
				$country = epc_uae_vat_normalize_country($buyer['country_code']);
			}
			if ($trn === '' && !empty($buyer['trn'])) {
				$trn = preg_replace('/\D/', '', (string)$buyer['trn']);
			}
		}
	}

	$country = epc_uae_vat_normalize_country($country);
	$trnDigits = preg_replace('/\D/', '', $trn);
	$trnValid = epc_uae_company_trn_valid($trnDigits);

	return array(
		'user_id' => $user_id,
		'country_code' => $country,
		'customer_type' => $customer_type,
		'trn' => $trnDigits !== '' ? $trnDigits : trim($trn),
		'trn_valid' => $trnValid,
		'tax_exempt' => $tax_exempt,
		'buyer_country_code' => $country,
	);
}

/** Compute customer_vat_type from country + TRN + trade profile. */
function epc_uae_customer_vat_resolve_type(PDO $db, int $user_id): string
{
	$ctx = epc_uae_customer_vat_context($db, $user_id);

	if (!empty($ctx['tax_exempt'])) {
		return 'tax_exempt';
	}

	$country = $ctx['country_code'];
	if (!epc_uae_vat_is_uae_country($country)) {
		if (epc_uae_customer_vat_is_gcc($country)) {
			return 'gcc';
		}
		return 'export';
	}

	if ($ctx['customer_type'] === 'wholesale') {
		return 'local_b2b';
	}

	return 'local_b2c';
}

function epc_uae_customer_vat_sync(PDO $db, int $user_id): string
{
	$type = epc_uae_customer_vat_resolve_type($db, $user_id);
	if ($user_id > 0) {
		epc_uae_customer_vat_save($db, $user_id, $type);
	}
	return $type;
}

function epc_uae_customer_vat_display_mode(string $vat_type): string
{
	$map = array(
		'local_b2c' => 'inclusive',
		'local_b2b' => 'exclusive',
		'gcc' => 'exclusive',
		'export' => 'exclusive',
		'tax_exempt' => 'exclusive',
	);
	return $map[$vat_type] ?? 'inclusive';
}

function epc_uae_customer_vat_price_label(string $vat_type, PDO $db): string
{
	if (!epc_uae_vat_sales_enabled($db)) {
		return '';
	}
	switch ($vat_type) {
		case 'local_b2c':
			return 'incl. VAT';
		case 'local_b2b':
			return 'excl. VAT';
		case 'gcc':
		case 'export':
			return 'Export';
		case 'tax_exempt':
			return 'Tax exempt';
		default:
			return 'incl. VAT';
	}
}

/**
 * Resolve VAT context for current or given user (guest → UAE B2C inclusive).
 *
 * @return array{vat_type:string,display_mode:string,price_label:string,vat_rate:float,vat_applicable:bool,context:array}
 */
function epc_uae_customer_vat_resolve(PDO $db, ?int $user_id = null): array
{
	if ($user_id === null) {
		if (!class_exists('DP_User')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		}
		$user_id = (int)DP_User::getUserId();
	}

	$stored = ($user_id > 0) ? epc_uae_customer_vat_get_stored($db, $user_id) : '';
	$types = epc_uae_customer_vat_types();
	if ($stored === '' || !isset($types[$stored])) {
		$stored = epc_uae_customer_vat_resolve_type($db, $user_id);
		if ($user_id > 0) {
			epc_uae_customer_vat_save($db, $user_id, $stored);
		}
	}

	$ctx = epc_uae_customer_vat_context($db, $user_id);
	$supply = epc_uae_vat_supply_tax_category($db, $ctx, array());
	$display = epc_uae_customer_vat_display_mode($stored);

	return array(
		'vat_type' => $stored,
		'vat_type_label' => $types[$stored] ?? $stored,
		'display_mode' => $display,
		'price_label' => epc_uae_customer_vat_price_label($stored, $db),
		'vat_rate' => (float)$supply['tax_rate'],
		'vat_applicable' => ((float)$supply['tax_rate'] > 0 && $display === 'inclusive'),
		'tax_category' => (string)$supply['tax_category'],
		'context' => $ctx,
	);
}

/** Apply storefront display price (ex-VAT base → inclusive when required). */
function epc_uae_customer_vat_apply_display_price(PDO $db, float $price_ex, ?int $user_id = null): array
{
	$price_ex = round(max(0, $price_ex), 2);
	$resolved = epc_uae_customer_vat_resolve($db, $user_id);

	if ($resolved['display_mode'] !== 'inclusive' || $resolved['vat_rate'] <= 0 || !epc_uae_vat_sales_enabled($db)) {
		return array(
			'display_price' => $price_ex,
			'price_ex_vat' => $price_ex,
			'vat_amount' => 0.0,
			'display_mode' => $resolved['display_mode'],
			'price_label' => $resolved['price_label'],
			'vat_type' => $resolved['vat_type'],
		);
	}

	$mult = epc_uae_vat_multiplier($db);
	$display = round($price_ex * $mult, 2);

	return array(
		'display_price' => $display,
		'price_ex_vat' => $price_ex,
		'vat_amount' => round($display - $price_ex, 2),
		'display_mode' => 'inclusive',
		'price_label' => $resolved['price_label'],
		'vat_type' => $resolved['vat_type'],
	);
}

/** Mutate product row (ajax) — price + optional VAT meta. */
function epc_uae_customer_vat_apply_product_row(PDO $db, array &$product, ?int $user_id = null): void
{
	if (!isset($product['price']) || (float)$product['price'] <= 0) {
		return;
	}
	$base = (float)$product['price'];
	$applied = epc_uae_customer_vat_apply_display_price($db, $base, $user_id);
	$product['price'] = $applied['display_price'];
	$product['price_ex_vat'] = $applied['price_ex_vat'];
	$product['vat_display_mode'] = $applied['display_mode'];
	$product['vat_price_label'] = $applied['price_label'];
	$product['vat_type'] = $applied['vat_type'];

	if (!empty($product['groups_price']) && is_array($product['groups_price'])) {
		foreach ($product['groups_price'] as $gid => $gp) {
			$gApplied = epc_uae_customer_vat_apply_display_price($db, (float)$gp, $user_id);
			$product['groups_price'][$gid] = $gApplied['display_price'];
		}
	}
}

/**
 * Order / e-invoice line amounts from stored unit price (what customer saw in cart).
 *
 * @return array{unit_net:float,line_net:float,vat_amount:float,gross:float,tax_rate:float,tax_category:string,prices_inclusive:bool}
 */
function epc_uae_customer_vat_order_line(PDO $db, int $user_id, float $unit_price, float $qty, array $transaction_flags = array()): array
{
	$qty = max(0, (float)$qty);
	$unit_price = round(max(0, (float)$unit_price), 2);
	$ctx = epc_uae_customer_vat_context($db, $user_id);
	$supply = epc_uae_vat_supply_tax_category($db, $ctx, $transaction_flags);
	$taxRate = (float)$supply['tax_rate'];
	$taxCat = (string)$supply['tax_category'];

	$resolved = epc_uae_customer_vat_resolve($db, $user_id);
	$inclusive = ($resolved['display_mode'] === 'inclusive' && $taxRate > 0);

	$lineGrossStored = round($unit_price * $qty, 2);

	if ($taxRate <= 0 || !epc_uae_vat_sales_enabled($db)) {
		return array(
			'unit_net' => $unit_price,
			'line_net' => $lineGrossStored,
			'vat_amount' => 0.0,
			'gross' => $lineGrossStored,
			'tax_rate' => 0.0,
			'tax_category' => $taxCat,
			'prices_inclusive' => $inclusive,
		);
	}

	if ($inclusive) {
		$split = epc_uae_vat_split_inclusive($lineGrossStored, $db);
		$lineNet = $split['amount_ex_vat'];
		$vatAmt = $split['vat_amount'];
		$unitNet = $qty > 0 ? round($lineNet / $qty, 4) : 0.0;
		return array(
			'unit_net' => $unitNet,
			'line_net' => $lineNet,
			'vat_amount' => $vatAmt,
			'gross' => $lineGrossStored,
			'tax_rate' => $taxRate,
			'tax_category' => $taxCat,
			'prices_inclusive' => true,
		);
	}

	$lineNet = $lineGrossStored;
	$vatAmt = round($lineNet * ($taxRate / 100), 2);
	return array(
		'unit_net' => $unit_price,
		'line_net' => $lineNet,
		'vat_amount' => $vatAmt,
		'gross' => round($lineNet + $vatAmt, 2),
		'tax_rate' => $taxRate,
		'tax_category' => $taxCat,
		'prices_inclusive' => false,
	);
}

function epc_uae_customer_vat_type_label(string $vat_type): string
{
	$types = epc_uae_customer_vat_types();
	return $types[$vat_type] ?? $vat_type;
}

/**
 * Aggregate FTA-style order VAT totals from stored cart/order unit prices.
 * Matches e-invoice: unit net, line net, VAT amount, gross — without double VAT.
 *
 * @param list<array{price?:float|int|string,count_need?:float|int|string,qty?:float|int|string}> $items
 * @return array{
 *   line_net:float,vat_amount:float,gross:float,tax_rate:float,prices_inclusive:bool,
 *   lines:list<array>,courier_net:float,courier_vat:float,courier_gross:float,amount_due_base:float
 * }
 */
function epc_uae_customer_vat_order_totals(PDO $db, int $user_id, array $items, array $transaction_flags = array(), array $courier = array()): array
{
	$lineNet = 0.0;
	$vatAmt = 0.0;
	$gross = 0.0;
	$taxRate = 0.0;
	$inclusive = false;
	$lineOut = array();
	foreach ($items as $item) {
		$unit = (float) ($item['price'] ?? 0);
		$qty = (float) ($item['count_need'] ?? ($item['qty'] ?? 0));
		$line = epc_uae_customer_vat_order_line($db, $user_id, $unit, $qty, $transaction_flags);
		$lineNet += (float) $line['line_net'];
		$vatAmt += (float) $line['vat_amount'];
		$gross += (float) $line['gross'];
		$inclusive = $inclusive || !empty($line['prices_inclusive']);
		if ((float) $line['tax_rate'] > 0) {
			$taxRate = (float) $line['tax_rate'];
		}
		$lineOut[] = $line;
	}
	$courierNet = round((float) ($courier['line_net'] ?? 0), 2);
	$courierVat = round((float) ($courier['vat_amount'] ?? 0), 2);
	$courierGross = round((float) ($courier['gross'] ?? ($courierNet + $courierVat)), 2);
	$goodsGross = round($gross, 2);
	return array(
		'line_net' => round($lineNet, 2),
		'vat_amount' => round($vatAmt, 2),
		'gross' => $goodsGross,
		'tax_rate' => $taxRate,
		'prices_inclusive' => $inclusive,
		'lines' => $lineOut,
		'courier_net' => $courierNet,
		'courier_vat' => $courierVat,
		'courier_gross' => $courierGross,
		'amount_due_base' => round($goodsGross + $courierGross, 2),
	);
}

/**
 * Load shop order items and compute FTA VAT totals (goods + courier).
 *
 * @return array<string,mixed>
 */
function epc_uae_customer_vat_shop_order_totals(PDO $db, int $order_id): array
{
	$order_id = (int) $order_id;
	$empty = array(
		'line_net' => 0.0,
		'vat_amount' => 0.0,
		'gross' => 0.0,
		'tax_rate' => 0.0,
		'prices_inclusive' => false,
		'lines' => array(),
		'courier_net' => 0.0,
		'courier_vat' => 0.0,
		'courier_gross' => 0.0,
		'amount_due_base' => 0.0,
		'user_id' => 0,
	);
	if ($order_id <= 0) {
		return $empty;
	}
	$oq = $db->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? LIMIT 1');
	$oq->execute(array($order_id));
	$order = $oq->fetch(PDO::FETCH_ASSOC);
	if (!$order) {
		return $empty;
	}
	$userId = (int) ($order['user_id'] ?? 0);
	$iq = $db->prepare('SELECT `price`, `count_need` FROM `shop_orders_items` WHERE `order_id` = ?');
	$iq->execute(array($order_id));
	$items = $iq->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$flags = array();
	$courier = array();
	$courierLib = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_courier_vat.php';
	if (is_file($courierLib)) {
		require_once $courierLib;
		if (function_exists('epc_order_vat_transaction_flags')) {
			$flags = epc_order_vat_transaction_flags($db, $order, $userId);
		}
		if (function_exists('epc_order_courier_vat_amounts')) {
			$courier = epc_order_courier_vat_amounts($db, $order, $userId, $flags);
		}
	}
	$totals = epc_uae_customer_vat_order_totals($db, $userId, $items, $flags, $courier);
	$totals['user_id'] = $userId;
	return $totals;
}

function epc_uae_customer_vat_styles(): string
{
	return '<style>'
		. '.epc-vat-price-label{display:block;font-size:11px;color:#64748b;font-weight:500;line-height:1.2;margin-top:2px}'
		. '.td_price .epc-vat-price-label{font-size:10px}'
		. '</style>';
}
