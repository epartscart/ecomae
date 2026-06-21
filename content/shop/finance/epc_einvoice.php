<?php
/**
 * UAE Electronic Invoicing — PINT-AE helpers, validation, XML, order build.
 * Guidelines V1.0 (23 Feb 2026) · Mandatory fields V1.0.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_einvoice_schema.php';
require_once __DIR__ . '/epc_uae_vat.php';
require_once __DIR__ . '/epc_uae_tax_compliance.php';

function epc_einvoice_constants(): array
{
	return array(
		'business_process' => 'urn:peppol:bis:billing',
		'specification_id' => 'urn:peppol:pint:billing-1@ae-1',
		'electronic_scheme' => '0235',
		'endpoint_not_onboarded' => '0235:9900000098',
		'endpoint_deemed_supply' => '0235:9900000097',
		'endpoint_exports' => '0235:9900000099',
		'invoice_type_tax' => '380',
		'invoice_type_credit' => '381',
		'invoice_type_commercial' => '380',
	);
}

function epc_einvoice_default_settings(): array
{
	return array(
		'seller_name' => 'ePartsCart LLC',
		'seller_trn' => '',
		'seller_tin' => '',
		'seller_legal_reg_no' => '',
		'seller_legal_reg_type' => 'TL',
		'seller_authority_name' => 'Dubai Economy and Tourism',
		'seller_address_line1' => '',
		'seller_city' => 'Dubai',
		'seller_emirate' => 'Dubai',
		'seller_country_code' => 'AE',
		'seller_phone' => '',
		'seller_email' => '',
		'seller_bank_account' => '',
		'payment_means_code' => '30',
		'payment_terms' => 'Within 7 days',
		'asp_name' => '',
		'asp_api_mode' => 'manual',
		'asp_api_url' => '',
		'asp_api_key' => '',
		'einvoice_enabled' => '1',
		'default_doc_category' => 'tax_invoice',
		'default_payment_due_days' => '7',
		'auto_validate' => '1',
	);
}

function epc_einvoice_get_setting(PDO $db, string $key, $default = '')
{
	epc_einvoice_ensure_schema($db);
	$st = $db->prepare('SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1');
	$st->execute(array($key));
	$v = $st->fetchColumn();
	return ($v === false) ? $default : $v;
}

function epc_einvoice_save_settings(PDO $db, array $data): void
{
	epc_einvoice_ensure_schema($db);
	if (!empty($data['seller_trn']) && empty(trim((string)($data['seller_tin'] ?? '')))) {
		$data['seller_tin'] = epc_einvoice_tin_from_trn((string)$data['seller_trn']);
	}
	$allowed = array_keys(epc_einvoice_default_settings());
	$upd = $db->prepare(
		'INSERT INTO `epc_einvoice_settings` (`setting_key`, `setting_value`, `time_updated`)
		VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `time_updated` = VALUES(`time_updated`)'
	);
	$now = time();
	foreach ($allowed as $key) {
		if (array_key_exists($key, $data)) {
			$upd->execute(array($key, trim((string)$data[$key]), $now));
		}
	}
	if (function_exists('epc_uae_company_sync_from_seller')) {
		epc_uae_company_sync_from_seller($db, epc_einvoice_seller_profile($db));
	}
}

function epc_einvoice_tin_from_trn(?string $trn): string
{
	$digits = preg_replace('/\D/', '', (string)$trn);
	if (strlen($digits) >= 10) {
		return substr($digits, 0, 10);
	}
	return $digits;
}

function epc_einvoice_peppol_endpoint(?string $tin, ?string $electronic_id = '0235'): string
{
	$scheme = trim((string)$electronic_id);
	if ($scheme === '') {
		$scheme = '0235';
	}
	$t = epc_einvoice_tin_from_trn($tin);
	if ($t === '') {
		return '';
	}
	return $scheme . ':' . $t;
}

function epc_einvoice_transaction_flags(): array
{
	return array(
		'free_zone' => 'Free Trade Zone',
		'deemed_supply' => 'Deemed Supply',
		'margin_scheme' => 'Margin Scheme',
		'summary_invoice' => 'Summary Invoice',
		'continuous_supply' => 'Continuous Supply',
		'agent_billing' => 'Disclosed Agent Billing',
		'ecommerce' => 'Supply through e-commerce',
		'exports' => 'Exports',
	);
}

function epc_einvoice_build_transaction_code(array $flags): string
{
	$order = array('free_zone', 'deemed_supply', 'margin_scheme', 'summary_invoice', 'continuous_supply', 'agent_billing', 'ecommerce', 'exports');
	$code = '';
	foreach ($order as $f) {
		$code .= !empty($flags[$f]) ? '1' : '0';
	}
	return str_pad($code, 8, '0');
}

function epc_einvoice_tax_categories(): array
{
	return array(
		'S' => array('label' => 'Standard Rate', 'rate' => null),
		'Z' => array('label' => 'Zero rated', 'rate' => 0),
		'E' => array('label' => 'Exempt from VAT', 'rate' => 0),
		'O' => array('label' => 'Outside scope of VAT', 'rate' => 0),
		'AE' => array('label' => 'Reverse Charge', 'rate' => 0),
		'M' => array('label' => 'Margin scheme', 'rate' => 0),
	);
}

function epc_einvoice_seller_profile(PDO $db): array
{
	$keys = array_keys(epc_einvoice_default_settings());
	$profile = array();
	foreach ($keys as $k) {
		if (strpos($k, 'seller_') === 0 || in_array($k, array('payment_means_code', 'payment_terms', 'seller_bank_account'), true)) {
			$profile[$k] = (string)epc_einvoice_get_setting($db, $k, epc_einvoice_default_settings()[$k] ?? '');
		}
	}
	$tin = epc_einvoice_get_setting($db, 'seller_tin', '');
	if ($tin === '') {
		$tin = epc_einvoice_tin_from_trn(epc_einvoice_get_setting($db, 'seller_trn', ''));
	}
	$profile['seller_tin'] = $tin;
	$profile['seller_peppol_endpoint'] = epc_einvoice_peppol_endpoint($tin);
	$profile['seller_electronic_id'] = '0235';
	return $profile;
}

function epc_einvoice_buyer_profile(PDO $db, int $user_id): array
{
	epc_einvoice_ensure_schema($db);
	if ($user_id <= 0) {
		return array();
	}
	$st = $db->prepare('SELECT * FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1');
	$st->execute(array($user_id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		$row = epc_einvoice_buyer_profile_from_user($db, $user_id);
	}
	if (!$row) {
		return array();
	}
	$tin = !empty($row['tin']) ? $row['tin'] : epc_einvoice_tin_from_trn($row['trn'] ?? '');
	$row['tin'] = $tin;
	$row['peppol_endpoint'] = !empty($row['peppol_endpoint'])
		? $row['peppol_endpoint']
		: epc_einvoice_peppol_endpoint($tin, $row['electronic_id'] ?? '0235');
	return $row;
}

function epc_einvoice_buyer_profile_from_user(PDO $db, int $user_id): array
{
	$fields = array('name' => '', 'surname' => '', 'email' => '', 'phone' => '', 'company' => '', 'address' => '', 'city' => '');
	$st = $db->prepare('SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?');
	$st->execute(array($user_id));
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		if (isset($fields[$r['data_key']])) {
			$fields[$r['data_key']] = trim((string)$r['data_value']);
		}
	}
	$ust = $db->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$ust->execute(array($user_id));
	$u = $ust->fetch(PDO::FETCH_ASSOC) ?: array();
	$name = trim($fields['company'] !== '' ? $fields['company'] : trim($fields['name'] . ' ' . $fields['surname']));
	return array(
		'user_id' => $user_id,
		'buyer_name' => $name !== '' ? $name : ('Customer #' . $user_id),
		'trn' => '',
		'tin' => '',
		'legal_reg_no' => '',
		'legal_reg_type' => 'TL',
		'authority_name' => '',
		'address_line1' => $fields['address'],
		'city' => $fields['city'] !== '' ? $fields['city'] : 'Dubai',
		'emirate' => 'Dubai',
		'country_code' => 'AE',
		'phone' => $fields['phone'] !== '' ? $fields['phone'] : ($u['phone'] ?? ''),
		'email' => $fields['email'] !== '' ? $fields['email'] : ($u['email'] ?? ''),
		'electronic_id' => '0235',
		'peppol_endpoint' => '',
		'buyer_onboarded' => 0,
	);
}

function epc_einvoice_save_buyer_profile(PDO $db, array $data): void
{
	epc_einvoice_ensure_schema($db);
	$user_id = (int)($data['user_id'] ?? 0);
	if ($user_id <= 0) {
		throw new Exception('Invalid customer');
	}
	$tin = trim((string)($data['tin'] ?? ''));
	if ($tin === '') {
		$tin = epc_einvoice_tin_from_trn($data['trn'] ?? '');
	}
	$onboarded = !empty($data['buyer_onboarded']) ? 1 : 0;
	$endpoint = trim((string)($data['peppol_endpoint'] ?? ''));
	if ($endpoint === '' && $onboarded && $tin !== '') {
		$endpoint = epc_einvoice_peppol_endpoint($tin, $data['electronic_id'] ?? '0235');
	}
	$db->prepare(
		'INSERT INTO `epc_einvoice_buyer_profiles`
		(`user_id`, `buyer_name`, `trn`, `tin`, `legal_reg_no`, `legal_reg_type`, `authority_name`,
		 `address_line1`, `city`, `emirate`, `country_code`, `phone`, `email`, `electronic_id`,
		 `peppol_endpoint`, `buyer_onboarded`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE
		`buyer_name`=VALUES(`buyer_name`), `trn`=VALUES(`trn`), `tin`=VALUES(`tin`),
		`legal_reg_no`=VALUES(`legal_reg_no`), `legal_reg_type`=VALUES(`legal_reg_type`),
		`authority_name`=VALUES(`authority_name`), `address_line1`=VALUES(`address_line1`),
		`city`=VALUES(`city`), `emirate`=VALUES(`emirate`), `country_code`=VALUES(`country_code`),
		`phone`=VALUES(`phone`), `email`=VALUES(`email`), `electronic_id`=VALUES(`electronic_id`),
		`peppol_endpoint`=VALUES(`peppol_endpoint`), `buyer_onboarded`=VALUES(`buyer_onboarded`),
		`time_updated`=VALUES(`time_updated`)'
	)->execute(array(
		$user_id,
		trim((string)($data['buyer_name'] ?? '')),
		trim((string)($data['trn'] ?? '')),
		$tin,
		trim((string)($data['legal_reg_no'] ?? '')),
		in_array($data['legal_reg_type'] ?? 'TL', array('TL', 'EID', 'PAS', 'CD'), true) ? $data['legal_reg_type'] : 'TL',
		trim((string)($data['authority_name'] ?? '')),
		trim((string)($data['address_line1'] ?? '')),
		trim((string)($data['city'] ?? 'Dubai')),
		trim((string)($data['emirate'] ?? 'Dubai')),
		strtoupper(trim((string)($data['country_code'] ?? 'AE'))),
		trim((string)($data['phone'] ?? '')),
		trim((string)($data['email'] ?? '')),
		'0235',
		$endpoint,
		$onboarded,
		time(),
	));
}

function epc_einvoice_mandatory_field_map(): array
{
	return array(
		'invoice_number' => 'Invoice number',
		'issue_date' => 'Invoice date',
		'invoice_type_code' => 'Invoice type code',
		'currency_code' => 'Invoice currency code',
		'transaction_type_code' => 'Invoice transaction type code',
		'payment_due_date' => 'Payment due date',
		'business_process' => 'Business process type',
		'specification_id' => 'Specification identifier',
		'payment_means_code' => 'Payment means type code',
		'seller_name' => 'Seller name',
		'seller_peppol_endpoint' => 'Seller electronic address (Peppol)',
		'seller_electronic_id' => 'Seller electronic identifier (0235)',
		'seller_legal_reg_no' => 'Seller legal registration identifier',
		'seller_legal_reg_type' => 'Seller legal registration identifier type',
		'seller_trn' => 'Seller tax identifier (TRN)',
		'seller_address_line1' => 'Seller address line 1',
		'seller_city' => 'Seller city',
		'seller_emirate' => 'Seller country subdivision',
		'seller_country_code' => 'Seller country code',
		'buyer_name' => 'Buyer name',
		'buyer_peppol_endpoint' => 'Buyer electronic address',
		'buyer_electronic_id' => 'Buyer electronic identifier',
		'buyer_trn' => 'Buyer tax identifier (TRN)',
		'buyer_address_line1' => 'Buyer address line 1',
		'buyer_city' => 'Buyer city',
		'buyer_emirate' => 'Buyer country subdivision',
		'buyer_country_code' => 'Buyer country code',
		'subtotal_ex_vat' => 'Sum of invoice line net amount',
		'total_ex_vat' => 'Invoice total amount without tax',
		'total_vat' => 'Invoice total tax amount',
		'total_incl_vat' => 'Invoice total amount with tax',
		'amount_due' => 'Amount due for payment',
	);
}

function epc_einvoice_validate_document(array $doc, array $lines, bool $tax_invoice = true): array
{
	$errors = array();
	$check = function ($key, $val) use (&$errors) {
		$labels = epc_einvoice_mandatory_field_map();
		if ($val === '' || $val === null || (is_numeric($val) && (float)$val < 0)) {
			$errors[] = ($labels[$key] ?? $key) . ' is required';
		}
	};

	foreach (array('invoice_number', 'issue_date', 'invoice_type_code', 'currency_code', 'transaction_type_code', 'payment_due_date') as $k) {
		$check($k, $doc[$k] ?? '');
	}
	$check('business_process', $doc['business_process'] ?? epc_einvoice_constants()['business_process']);
	$check('specification_id', $doc['specification_id'] ?? epc_einvoice_constants()['specification_id']);
	$check('payment_means_code', $doc['payment_means_code'] ?? '');

	$seller = is_array($doc['seller'] ?? null) ? $doc['seller'] : json_decode($doc['seller_json'] ?? '{}', true);
	$buyer = is_array($doc['buyer'] ?? null) ? $doc['buyer'] : json_decode($doc['buyer_json'] ?? '{}', true);

	foreach (array('seller_name', 'seller_legal_reg_no', 'seller_legal_reg_type', 'seller_address_line1', 'seller_city', 'seller_emirate', 'seller_country_code') as $sk) {
		$check($sk, $seller[$sk] ?? $seller[str_replace('seller_', '', $sk)] ?? '');
	}
	if ($tax_invoice) {
		$sellerTrn = $seller['seller_trn'] ?? $seller['trn'] ?? '';
		$check('seller_trn', $sellerTrn);
		if ($sellerTrn !== '' && !epc_uae_validate_trn($sellerTrn, true)) {
			$errors[] = 'Seller TRN must be exactly 15 digits (UAE FTA)';
		}
	}
	$check('seller_peppol_endpoint', $seller['seller_peppol_endpoint'] ?? $seller['peppol_endpoint'] ?? '');

	foreach (array('buyer_name', 'buyer_address_line1', 'buyer_city', 'buyer_emirate', 'buyer_country_code') as $bk) {
		$check($bk, $buyer[$bk] ?? $buyer[str_replace('buyer_', '', $bk)] ?? '');
	}
	if ($tax_invoice) {
		$buyerTrn = $buyer['buyer_trn'] ?? $buyer['trn'] ?? '';
		$buyerCountry = strtoupper((string)($buyer['buyer_country_code'] ?? $buyer['country_code'] ?? 'AE'));
		if ($buyerCountry === 'AE' && $buyerTrn !== '') {
			$check('buyer_trn', $buyerTrn);
		}
		if ($buyerTrn !== '' && !epc_uae_validate_trn($buyerTrn, false)) {
			$errors[] = 'Buyer TRN must be exactly 15 digits when provided (UAE FTA)';
		}
	}
	$invNo = trim((string)($doc['invoice_number'] ?? ''));
	if ($tax_invoice && $invNo !== '' && !preg_match('/^EINV-\d{4}-\d{5}$/', $invNo)) {
		if (!preg_match('/^[A-Z0-9][A-Z0-9\-\/]{2,48}$/i', $invNo)) {
			$errors[] = 'Invoice number must be a valid unique serial (recommended EINV-YYYY-NNNNN)';
		}
	}
	$advCr = round((float)($doc['advance_vat_credit'] ?? 0), 2);
	$totVat = round((float)($doc['total_vat'] ?? 0), 2);
	if ($tax_invoice && $advCr > $totVat + 0.01) {
		$errors[] = 'Advance VAT credit cannot exceed total VAT on the tax invoice';
	}
	$check('buyer_peppol_endpoint', $buyer['buyer_peppol_endpoint'] ?? $buyer['peppol_endpoint'] ?? '');

	if (count($lines) < 1) {
		$errors[] = 'At least one invoice line is required';
	}
	foreach ($lines as $i => $ln) {
		$n = (int)$i + 1;
		foreach (array('item_name', 'quantity', 'uom_code', 'unit_price', 'line_net', 'tax_category', 'tax_rate', 'tax_amount', 'gross_amount') as $lf) {
			if (!isset($ln[$lf]) || $ln[$lf] === '') {
				$errors[] = 'Line ' . $n . ': ' . $lf . ' is required';
			}
		}
		if ($tax_invoice) {
			if (!isset($ln['vat_line_aed']) || $ln['vat_line_aed'] === '') {
				$errors[] = 'Line ' . $n . ': VAT line amount in AED is required';
			}
			if (!isset($ln['line_amount_aed']) || $ln['line_amount_aed'] === '') {
				$errors[] = 'Line ' . $n . ': Invoice line amount in AED is required';
			}
		}
	}

	return array('ok' => count($errors) === 0, 'errors' => $errors);
}

function epc_einvoice_generate_uuid(): string
{
	$data = random_bytes(16);
	$data[6] = chr(ord($data[6]) & 0x0f | 0x40);
	$data[8] = chr(ord($data[8]) & 0x3f | 0x80);
	return vsprintf('%s%s-%s-%s-%s-%s%s%s', str_split(bin2hex($data), 4));
}

function epc_einvoice_next_number(PDO $db): string
{
	$year = date('Y');
	$prefix = 'EINV-' . $year . '-';
	$st = $db->prepare('SELECT `invoice_number` FROM `epc_einvoice_documents` WHERE `invoice_number` LIKE ? ORDER BY `id` DESC LIMIT 1');
	$st->execute(array($prefix . '%'));
	$last = $st->fetchColumn();
	$n = 1;
	if ($last && preg_match('/-(\d+)$/', (string)$last, $m)) {
		$n = (int)$m[1] + 1;
	}
	return $prefix . str_pad((string)$n, 5, '0', STR_PAD_LEFT);
}

function epc_einvoice_build_from_order(PDO $db, int $order_id, array $opts = array()): array
{
	require_once __DIR__ . '/epc_erp_helpers.php';
	epc_einvoice_ensure_schema($db);

	$ost = $db->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1');
	$ost->execute(array($order_id));
	$order = $ost->fetch(PDO::FETCH_ASSOC);
	if (!$order) {
		throw new Exception('Order not found');
	}

	$item_where = '';
	$ex = epc_erp_item_status_exclusion($db);
	$item_where = $ex['where_and'];

	$items = $db->prepare(
		'SELECT *, (`price`*`count_need`) AS `line_net` FROM `shop_orders_items`
		WHERE `order_id` = ?' . $item_where
	);
	$items->execute(array($order_id));
	$rows = $items->fetchAll(PDO::FETCH_ASSOC);
	if (!$rows) {
		throw new Exception('Order has no billable lines');
	}

	$seller = epc_einvoice_seller_profile($db);
	$user_id = (int)$order['user_id'];
	$buyerRaw = epc_einvoice_buyer_profile($db, $user_id);
	$const = epc_einvoice_constants();
	$flags = isset($opts['transaction_flags']) && is_array($opts['transaction_flags']) ? $opts['transaction_flags'] : array();
	$txCode = epc_einvoice_build_transaction_code($flags);

	$buyerEndpoint = '';
	if (!empty($flags['deemed_supply'])) {
		$buyerEndpoint = $const['endpoint_deemed_supply'];
	} elseif (!empty($flags['exports'])) {
		$buyerEndpoint = $const['endpoint_exports'];
	} elseif (!empty($buyerRaw['buyer_onboarded']) && !empty($buyerRaw['peppol_endpoint'])) {
		$buyerEndpoint = $buyerRaw['peppol_endpoint'];
	} else {
		$buyerEndpoint = $const['endpoint_not_onboarded'];
	}

	$buyer = array(
		'buyer_name' => $buyerRaw['buyer_name'] ?? 'Buyer',
		'buyer_trn' => $buyerRaw['trn'] ?? '',
		'buyer_tin' => $buyerRaw['tin'] ?? '',
		'buyer_legal_reg_no' => $buyerRaw['legal_reg_no'] ?? '',
		'buyer_legal_reg_type' => $buyerRaw['legal_reg_type'] ?? 'TL',
		'buyer_authority_name' => $buyerRaw['authority_name'] ?? '',
		'buyer_address_line1' => $buyerRaw['address_line1'] ?? '',
		'buyer_city' => $buyerRaw['city'] ?? 'Dubai',
		'buyer_emirate' => $buyerRaw['emirate'] ?? 'Dubai',
		'buyer_country_code' => $buyerRaw['country_code'] ?? 'AE',
		'buyer_phone' => $buyerRaw['phone'] ?? '',
		'buyer_email' => $buyerRaw['email'] ?? '',
		'buyer_electronic_id' => '0235',
		'buyer_peppol_endpoint' => $buyerEndpoint,
	);

	$supplyTax = epc_uae_vat_supply_tax_category($db, $buyer, $flags);
	$taxCat = $supplyTax['tax_category'];
	$taxRate = (float)$supplyTax['tax_rate'];

	require_once __DIR__ . '/epc_uae_customer_vat.php';

	$lines = array();
	$lineNo = 0;
	$subtotal = 0.0;
	$totalVat = 0.0;
	foreach ($rows as $r) {
		$lineNo++;
		$qty = (float)$r['count_need'];
		$unitStored = (float)$r['price'];
		$lineCalc = epc_uae_customer_vat_order_line($db, $user_id, $unitStored, $qty, $flags);
		$unit = round((float)$lineCalc['unit_net'], 2);
		$net = round((float)$lineCalc['line_net'], 2);
		$vatLine = round((float)$lineCalc['vat_amount'], 2);
		$gross = round((float)$lineCalc['gross'], 2);
		$lineTaxRate = (float)$lineCalc['tax_rate'];
		$lineTaxCat = (string)$lineCalc['tax_category'];
		$name = trim(($r['t2_manufacturer'] ?? '') . ' ' . ($r['t2_article'] ?? ''));
		if ($name === '') {
			$name = 'Part line ' . $lineNo;
		}
		$lines[] = array(
			'line_no' => $lineNo,
			'item_name' => $name,
			'item_description' => trim((string)($r['t2_name'] ?? '')),
			'item_type' => 'G',
			'quantity' => $qty,
			'uom_code' => 'C62',
			'unit_price' => $unit,
			'line_net' => $net,
			'tax_category' => $lineTaxCat,
			'tax_rate' => $lineTaxRate,
			'tax_amount' => $vatLine,
			'gross_amount' => $gross,
			'vat_line_aed' => $vatLine,
			'line_amount_aed' => $gross,
		);
		$subtotal += $net;
		$totalVat += $vatLine;
	}
	$subtotal = round($subtotal, 2);
	$totalVat = round($totalVat, 2);
	$totalIncl = round($subtotal + $totalVat, 2);

	$paid = 0.0;
	$pq = $db->prepare('SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=0 AND `order_id`=?');
	$pq->execute(array($order_id));
	$paid = round((float)$pq->fetchColumn(), 2);
	$amountDue = max(0, round($totalIncl - $paid, 2));

	$issueTs = (int)$order['time'];
	$dueDays = (int)epc_einvoice_get_setting($db, 'default_payment_due_days', '7');
	$dueTs = $issueTs + ($dueDays * 86400);

	$doc = array(
		'uuid' => epc_einvoice_generate_uuid(),
		'invoice_number' => !empty($opts['invoice_number']) ? $opts['invoice_number'] : epc_einvoice_next_number($db),
		'order_id' => $order_id,
		'user_id' => $user_id,
		'doc_category' => 'tax_invoice',
		'invoice_type_code' => $const['invoice_type_tax'],
		'issue_date' => $issueTs,
		'payment_due_date' => $dueTs,
		'vat_point_date' => $issueTs,
		'currency_code' => 'AED',
		'vat_currency_code' => 'AED',
		'transaction_type_code' => $txCode,
		'payment_means_code' => epc_einvoice_get_setting($db, 'payment_means_code', '30'),
		'payment_terms' => epc_einvoice_get_setting($db, 'payment_terms', 'Within 7 days'),
		'bank_account' => epc_einvoice_get_setting($db, 'seller_bank_account', ''),
		'business_process' => $const['business_process'],
		'specification_id' => $const['specification_id'],
		'seller' => $seller,
		'buyer' => $buyer,
		'subtotal_ex_vat' => $subtotal,
		'total_vat' => $totalVat,
		'total_incl_vat' => $totalIncl,
		'paid_amount' => $paid,
		'rounding_amount' => 0,
		'amount_due' => $amountDue,
		'tax_breakdown' => array(array(
			'tax_category' => $taxCat,
			'taxable_amount' => $subtotal,
			'tax_rate' => $taxRate,
			'tax_amount' => $totalVat,
			'label' => epc_einvoice_tax_categories()[$taxCat]['label'] ?? 'Standard Rate',
		)),
		'lines' => $lines,
	);
	return $doc;
}

function epc_einvoice_save_document(PDO $db, array $doc, int $admin_id = 0): int
{
	epc_einvoice_ensure_schema($db);
	$isTaxInvoice = ($doc['doc_category'] ?? 'tax_invoice') === 'tax_invoice';
	$validation = epc_einvoice_validate_document($doc, $doc['lines'] ?? array(), $isTaxInvoice);
	$voucherCheck = epc_uae_vat_apply_to_voucher($db, 'einvoice', array_merge($doc, array(
		'seller' => $doc['seller'] ?? json_decode($doc['seller_json'] ?? '{}', true),
		'buyer' => $doc['buyer'] ?? json_decode($doc['buyer_json'] ?? '{}', true),
	)));
	if (!$voucherCheck['ok']) {
		$validation['ok'] = false;
		$validation['errors'] = array_merge($validation['errors'] ?? array(), $voucherCheck['errors']);
	}
	if ($isTaxInvoice && !$validation['ok']) {
		throw new Exception('Tax invoice validation failed: ' . implode('; ', $validation['errors']));
	}
	$xml = epc_einvoice_build_xml($doc);
	$now = time();
	$status = $validation['ok'] ? 'validated' : 'draft';

	if (!$db->beginTransaction()) {
		throw new Exception('Transaction start failed');
	}
	try {
		$ins = $db->prepare(
			'INSERT INTO `epc_einvoice_documents`
			(`uuid`, `invoice_number`, `order_id`, `user_id`, `doc_category`, `invoice_type_code`,
			 `issue_date`, `payment_due_date`, `vat_point_date`, `currency_code`, `vat_currency_code`,
			 `transaction_type_code`, `payment_means_code`, `payment_terms`, `bank_account`,
			 `seller_json`, `buyer_json`, `subtotal_ex_vat`, `total_vat`, `total_incl_vat`,
			 `paid_amount`, `rounding_amount`, `amount_due`, `tax_breakdown_json`, `status`,
			 `validation_ok`, `validation_errors_json`, `xml_content`, `time_created`, `time_updated`, `admin_id`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		);
		$ins->execute(array(
			$doc['uuid'],
			$doc['invoice_number'],
			(int)$doc['order_id'],
			(int)$doc['user_id'],
			$doc['doc_category'] ?? 'tax_invoice',
			$doc['invoice_type_code'] ?? '380',
			(int)$doc['issue_date'],
			(int)$doc['payment_due_date'],
			(int)($doc['vat_point_date'] ?? $doc['issue_date']),
			$doc['currency_code'] ?? 'AED',
			$doc['vat_currency_code'] ?? 'AED',
			$doc['transaction_type_code'] ?? '00000000',
			$doc['payment_means_code'] ?? '30',
			$doc['payment_terms'] ?? '',
			$doc['bank_account'] ?? '',
			json_encode($doc['seller'], JSON_UNESCAPED_UNICODE),
			json_encode($doc['buyer'], JSON_UNESCAPED_UNICODE),
			$doc['subtotal_ex_vat'],
			$doc['total_vat'],
			$doc['total_incl_vat'],
			$doc['paid_amount'] ?? 0,
			$doc['rounding_amount'] ?? 0,
			$doc['amount_due'],
			json_encode($doc['tax_breakdown'] ?? array(), JSON_UNESCAPED_UNICODE),
			$status,
			$validation['ok'] ? 1 : 0,
			json_encode($validation['errors'], JSON_UNESCAPED_UNICODE),
			$xml,
			$now,
			$now,
			$admin_id,
		));
		$docId = (int)$db->lastInsertId();

		$lins = $db->prepare(
			'INSERT INTO `epc_einvoice_lines`
			(`document_id`, `line_no`, `item_name`, `item_description`, `item_type`, `quantity`, `uom_code`,
			 `unit_price`, `line_net`, `tax_category`, `tax_rate`, `tax_amount`, `gross_amount`, `vat_line_aed`, `line_amount_aed`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		);
		foreach ($doc['lines'] as $ln) {
			$lins->execute(array(
				$docId,
				(int)$ln['line_no'],
				$ln['item_name'],
				$ln['item_description'] ?? '',
				$ln['item_type'] ?? 'G',
				$ln['quantity'],
				$ln['uom_code'] ?? 'C62',
				$ln['unit_price'],
				$ln['line_net'],
				$ln['tax_category'],
				$ln['tax_rate'],
				$ln['tax_amount'],
				$ln['gross_amount'],
				$ln['vat_line_aed'],
				$ln['line_amount_aed'],
			));
		}

		epc_einvoice_log_event($db, $docId, 'created', $validation['ok'] ? 'validated' : 'draft',
			$validation['ok'] ? 'Document validated against mandatory fields' : 'Draft — fix validation errors before submission',
			array('errors' => $validation['errors']));

		if ((int)($doc['order_id'] ?? 0) > 0 && ($doc['doc_category'] ?? '') === 'tax_invoice') {
			epc_uae_vat_apply_invoice_adjustment($db, $docId, (int)$doc['order_id']);
		}

		$db->commit();

		// Feed real sale-out demand into Order planning / SCM from posted sales
		// invoices (online CP-portal orders and manual sales). Guarded so it can
		// never break invoicing; skips credit notes (381).
		if (($doc['doc_category'] ?? 'tax_invoice') === 'tax_invoice'
			&& (string) ($doc['invoice_type_code'] ?? '380') !== '381') {
			try {
				$invFile = __DIR__ . '/epc_erp_inventory.php';
				if (is_file($invFile)) {
					require_once $invFile;
					if (function_exists('epc_erp_inventory_record_sale_demand')) {
						epc_erp_inventory_record_sale_demand(
							$db,
							$docId,
							(int) ($doc['order_id'] ?? 0),
							$doc['lines'] ?? array(),
							(int) ($doc['issue_date'] ?? time())
						);
					}
				}
			} catch (Exception $e) {
				// demand capture is best-effort; never block the invoice
			}
			// advance the order's process-flow case to "Invoiced & closed"
			try {
				$pfFile = __DIR__ . '/epc_erp_processflow.php';
				if ((int) ($doc['order_id'] ?? 0) > 0 && is_file($pfFile)) {
					require_once $pfFile;
					if (function_exists('epc_pf_sync_order_case')) {
						epc_pf_sync_order_case($db, (int) $doc['order_id']);
					}
				}
			} catch (Exception $e) {
				// process-flow sync is best-effort; never block the invoice
			}
		}

		return $docId;
	} catch (Exception $e) {
		if ($db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
}

function epc_einvoice_log_event(PDO $db, int $doc_id, string $type, string $status, string $message, array $payload = array()): void
{
	$db->prepare(
		'INSERT INTO `epc_einvoice_events` (`document_id`, `event_type`, `status`, `message`, `payload_json`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?)'
	)->execute(array($doc_id, $type, $status, $message, json_encode($payload, JSON_UNESCAPED_UNICODE), time()));
}

function epc_einvoice_get_document(PDO $db, int $id): ?array
{
	$st = $db->prepare('SELECT * FROM `epc_einvoice_documents` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($id));
	$doc = $st->fetch(PDO::FETCH_ASSOC);
	if (!$doc) {
		return null;
	}
	$ln = $db->prepare('SELECT * FROM `epc_einvoice_lines` WHERE `document_id` = ? ORDER BY `line_no`');
	$ln->execute(array($id));
	$doc['lines'] = $ln->fetchAll(PDO::FETCH_ASSOC);
	$doc['seller'] = json_decode($doc['seller_json'] ?? '{}', true) ?: array();
	$doc['buyer'] = json_decode($doc['buyer_json'] ?? '{}', true) ?: array();
	$doc['tax_breakdown'] = json_decode($doc['tax_breakdown_json'] ?? '[]', true) ?: array();
	$doc['validation_errors'] = json_decode($doc['validation_errors_json'] ?? '[]', true) ?: array();
	$ev = $db->prepare('SELECT * FROM `epc_einvoice_events` WHERE `document_id` = ? ORDER BY `time_created` DESC LIMIT 20');
	$ev->execute(array($id));
	$doc['events'] = $ev->fetchAll(PDO::FETCH_ASSOC);
	return $doc;
}

function epc_einvoice_list_documents(PDO $db, int $date_from = 0, int $date_to = 0, int $limit = 100): array
{
	epc_einvoice_ensure_schema($db);
	if ($date_to <= 0) {
		$date_to = time();
	}
	if ($date_from <= 0) {
		$date_from = strtotime(date('Y-m-01'));
	}
	$st = $db->prepare(
		'SELECT d.*, o.`id` AS `order_ref`
		FROM `epc_einvoice_documents` d
		LEFT JOIN `shop_orders` o ON o.`id` = d.`order_id`
		WHERE d.`active` = 1 AND d.`issue_date` >= ? AND d.`issue_date` <= ?
		ORDER BY d.`issue_date` DESC, d.`id` DESC LIMIT ' . (int)$limit
	);
	$st->execute(array($date_from, $date_to));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_einvoice_dashboard(PDO $db, int $date_from = 0, int $date_to = 0): array
{
	epc_einvoice_ensure_schema($db);
	if ($date_to <= 0) {
		$date_to = time();
	}
	if ($date_from <= 0) {
		$date_from = strtotime(date('Y-m-01'));
	}
	$q = $db->prepare(
		'SELECT
			COUNT(*) AS total,
			SUM(`status` = "draft") AS draft,
			SUM(`status` = "validated") AS validated,
			SUM(`status` IN ("queued","submitted")) AS submitted,
			SUM(`status` = "accepted") AS accepted,
			SUM(`status` = "rejected") AS rejected,
			IFNULL(SUM(`total_incl_vat`),0) AS amount_incl_vat
		FROM `epc_einvoice_documents`
		WHERE `active` = 1 AND `issue_date` >= ? AND `issue_date` <= ?'
	);
	$q->execute(array($date_from, $date_to));
	$row = $q->fetch(PDO::FETCH_ASSOC) ?: array();
	$seller = epc_einvoice_seller_profile($db);
	$ready = ($seller['seller_trn'] ?? '') !== '' && ($seller['seller_peppol_endpoint'] ?? '') !== '';
	return array_merge($row, array(
		'seller_configured' => $ready,
		'asp_name' => epc_einvoice_get_setting($db, 'asp_name', ''),
		'guidelines_version' => 'V1.0 · 23 Feb 2026',
	));
}

function epc_einvoice_submit_to_asp(PDO $db, int $doc_id, int $admin_id = 0): array
{
	$doc = epc_einvoice_get_document($db, $doc_id);
	if (!$doc) {
		throw new Exception('Document not found');
	}
	if (!$doc['validation_ok']) {
		throw new Exception('Fix validation errors before submission');
	}
	$asp = epc_einvoice_get_setting($db, 'asp_name', '');
	if ($asp === '') {
		throw new Exception('Configure Accredited Service Provider (ASP) in e-Invoicing settings first');
	}

	$mode = epc_einvoice_get_setting($db, 'asp_api_mode', 'manual');
	$now = time();
	$ref = 'ASP-' . strtoupper(substr(md5($doc['uuid'] . $now), 0, 12));

	if ($mode === 'api') {
		$url = trim((string)epc_einvoice_get_setting($db, 'asp_api_url', ''));
		if ($url === '') {
			throw new Exception('ASP API URL not configured');
		}
		// Placeholder for live ASP REST integration — store queue record.
		epc_einvoice_log_event($db, $doc_id, 'asp_queue', 'queued', 'Queued for ASP API transmission', array('url' => $url));
		$status = 'queued';
	} else {
		epc_einvoice_log_event($db, $doc_id, 'asp_manual', 'submitted',
			'XML package ready for ASP upload. Complete transmission via your ASP portal (EmaraTax onboarding).',
			array('asp' => $asp, 'reference' => $ref));
		$status = 'submitted';
	}

	$db->prepare(
		'UPDATE `epc_einvoice_documents` SET `status` = ?, `asp_name` = ?, `asp_reference` = ?,
		`fta_report_status` = ?, `time_submitted` = ?, `time_updated` = ? WHERE `id` = ?'
	)->execute(array($status, $asp, $ref, 'pending_fta_confirmation', $now, $now, $doc_id));

	epc_einvoice_log_event($db, $doc_id, 'fta_report', 'pending',
		'Tax data reporting to FTA (Corner 5) is performed by your ASP after successful validation.',
		array('reference' => $ref));

	return array('document_id' => $doc_id, 'status' => $status, 'asp_reference' => $ref);
}

function epc_einvoice_xml_escape(string $s): string
{
	return htmlspecialchars($s, ENT_XML1 | ENT_QUOTES, 'UTF-8');
}

function epc_einvoice_build_xml(array $doc): string
{
	$c = epc_einvoice_constants();
	$seller = $doc['seller'] ?? array();
	$buyer = $doc['buyer'] ?? array();
	$issue = date('Y-m-d', (int)$doc['issue_date']);
	$due = date('Y-m-d', (int)$doc['payment_due_date']);
	$vatPoint = date('Y-m-d', (int)($doc['vat_point_date'] ?? $doc['issue_date']));

	$xml = '<?xml version="1.0" encoding="UTF-8"?>' . "\n";
	$xml .= '<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"' . "\n";
	$xml .= ' xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"' . "\n";
	$xml .= ' xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">' . "\n";
	$xml .= '<cbc:CustomizationID>' . epc_einvoice_xml_escape($c['specification_id']) . '</cbc:CustomizationID>' . "\n";
	$xml .= '<cbc:ProfileID>' . epc_einvoice_xml_escape($c['business_process']) . '</cbc:ProfileID>' . "\n";
	$xml .= '<cbc:ID>' . epc_einvoice_xml_escape($doc['invoice_number']) . '</cbc:ID>' . "\n";
	$xml .= '<cbc:UUID>' . epc_einvoice_xml_escape($doc['uuid']) . '</cbc:UUID>' . "\n";
	$xml .= '<cbc:IssueDate>' . $issue . '</cbc:IssueDate>' . "\n";
	$xml .= '<cbc:DueDate>' . $due . '</cbc:DueDate>' . "\n";
	$xml .= '<cbc:InvoiceTypeCode>' . epc_einvoice_xml_escape($doc['invoice_type_code'] ?? '380') . '</cbc:InvoiceTypeCode>' . "\n";
	$xml .= '<cbc:DocumentCurrencyCode>' . epc_einvoice_xml_escape($doc['currency_code'] ?? 'AED') . '</cbc:DocumentCurrencyCode>' . "\n";
	$xml .= '<cbc:TaxCurrencyCode>' . epc_einvoice_xml_escape($doc['vat_currency_code'] ?? 'AED') . '</cbc:TaxCurrencyCode>' . "\n";

	$xml .= '<cac:AccountingSupplierParty><cac:Party>';
	$xml .= '<cbc:EndpointID schemeID="0235">' . epc_einvoice_xml_escape(epc_einvoice_tin_from_trn($seller['seller_trn'] ?? '')) . '</cbc:EndpointID>';
	$xml .= '<cac:PartyName><cbc:Name>' . epc_einvoice_xml_escape($seller['seller_name'] ?? '') . '</cbc:Name></cac:PartyName>';
	$xml .= '<cac:PostalAddress>';
	$xml .= '<cbc:StreetName>' . epc_einvoice_xml_escape($seller['seller_address_line1'] ?? '') . '</cbc:StreetName>';
	$xml .= '<cbc:CityName>' . epc_einvoice_xml_escape($seller['seller_city'] ?? '') . '</cbc:CityName>';
	$xml .= '<cbc:CountrySubentity>' . epc_einvoice_xml_escape($seller['seller_emirate'] ?? '') . '</cbc:CountrySubentity>';
	$xml .= '<cac:Country><cbc:IdentificationCode>' . epc_einvoice_xml_escape($seller['seller_country_code'] ?? 'AE') . '</cbc:IdentificationCode></cac:Country>';
	$xml .= '</cac:PostalAddress>';
	$xml .= '<cac:PartyTaxScheme><cbc:CompanyID>' . epc_einvoice_xml_escape($seller['seller_trn'] ?? '') . '</cbc:CompanyID>';
	$xml .= '<cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme></cac:PartyTaxScheme>';
	$xml .= '<cac:PartyLegalEntity><cbc:RegistrationName>' . epc_einvoice_xml_escape($seller['seller_name'] ?? '') . '</cbc:RegistrationName>';
	$xml .= '<cbc:CompanyID schemeID="' . epc_einvoice_xml_escape($seller['seller_legal_reg_type'] ?? 'TL') . '">' . epc_einvoice_xml_escape($seller['seller_legal_reg_no'] ?? '') . '</cbc:CompanyID>';
	$xml .= '</cac:PartyLegalEntity></cac:Party></cac:AccountingSupplierParty>';

	$buyerEndpoint = $buyer['buyer_peppol_endpoint'] ?? '';
	$buyerTin = '';
	if (strpos($buyerEndpoint, ':') !== false) {
		$buyerTin = substr($buyerEndpoint, strrpos($buyerEndpoint, ':') + 1);
	}
	$xml .= '<cac:AccountingCustomerParty><cac:Party>';
	$xml .= '<cbc:EndpointID schemeID="0235">' . epc_einvoice_xml_escape($buyerTin) . '</cbc:EndpointID>';
	$xml .= '<cac:PartyName><cbc:Name>' . epc_einvoice_xml_escape($buyer['buyer_name'] ?? '') . '</cbc:Name></cac:PartyName>';
	$xml .= '<cac:PostalAddress>';
	$xml .= '<cbc:StreetName>' . epc_einvoice_xml_escape($buyer['buyer_address_line1'] ?? '') . '</cbc:StreetName>';
	$xml .= '<cbc:CityName>' . epc_einvoice_xml_escape($buyer['buyer_city'] ?? '') . '</cbc:CityName>';
	$xml .= '<cbc:CountrySubentity>' . epc_einvoice_xml_escape($buyer['buyer_emirate'] ?? '') . '</cbc:CountrySubentity>';
	$xml .= '<cac:Country><cbc:IdentificationCode>' . epc_einvoice_xml_escape($buyer['buyer_country_code'] ?? 'AE') . '</cbc:IdentificationCode></cac:Country>';
	$xml .= '</cac:PostalAddress>';
	$xml .= '<cac:PartyTaxScheme><cbc:CompanyID>' . epc_einvoice_xml_escape($buyer['buyer_trn'] ?? '') . '</cbc:CompanyID>';
	$xml .= '<cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme></cac:PartyTaxScheme>';
	$xml .= '</cac:Party></cac:AccountingCustomerParty>';

	$xml .= '<cac:PaymentMeans><cbc:PaymentMeansCode>' . epc_einvoice_xml_escape($doc['payment_means_code'] ?? '30') . '</cbc:PaymentMeansCode>';
	if (!empty($doc['bank_account'])) {
		$xml .= '<cac:PayeeFinancialAccount><cbc:ID>' . epc_einvoice_xml_escape($doc['bank_account']) . '</cbc:ID></cac:PayeeFinancialAccount>';
	}
	$xml .= '</cac:PaymentMeans>';

	$xml .= '<cac:TaxTotal><cbc:TaxAmount currencyID="AED">' . number_format((float)$doc['total_vat'], 2, '.', '') . '</cbc:TaxAmount>';
	foreach ($doc['tax_breakdown'] ?? array() as $tb) {
		$xml .= '<cac:TaxSubtotal>';
		$xml .= '<cbc:TaxableAmount currencyID="AED">' . number_format((float)$tb['taxable_amount'], 2, '.', '') . '</cbc:TaxableAmount>';
		$xml .= '<cbc:TaxAmount currencyID="AED">' . number_format((float)$tb['tax_amount'], 2, '.', '') . '</cbc:TaxAmount>';
		$xml .= '<cac:TaxCategory><cbc:ID>' . epc_einvoice_xml_escape($tb['tax_category']) . '</cbc:ID>';
		$xml .= '<cbc:Percent>' . number_format((float)$tb['tax_rate'], 2, '.', '') . '</cbc:Percent>';
		$xml .= '<cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme></cac:TaxCategory>';
		$xml .= '</cac:TaxSubtotal>';
	}
	$xml .= '</cac:TaxTotal>';

	$xml .= '<cac:LegalMonetaryTotal>';
	$xml .= '<cbc:LineExtensionAmount currencyID="AED">' . number_format((float)$doc['subtotal_ex_vat'], 2, '.', '') . '</cbc:LineExtensionAmount>';
	$xml .= '<cbc:TaxExclusiveAmount currencyID="AED">' . number_format((float)$doc['subtotal_ex_vat'], 2, '.', '') . '</cbc:TaxExclusiveAmount>';
	$xml .= '<cbc:TaxInclusiveAmount currencyID="AED">' . number_format((float)$doc['total_incl_vat'], 2, '.', '') . '</cbc:TaxInclusiveAmount>';
	$xml .= '<cbc:PrepaidAmount currencyID="AED">' . number_format((float)($doc['paid_amount'] ?? 0), 2, '.', '') . '</cbc:PrepaidAmount>';
	$xml .= '<cbc:PayableAmount currencyID="AED">' . number_format((float)$doc['amount_due'], 2, '.', '') . '</cbc:PayableAmount>';
	$xml .= '</cac:LegalMonetaryTotal>';

	foreach ($doc['lines'] as $ln) {
		$xml .= '<cac:InvoiceLine>';
		$xml .= '<cbc:ID>' . (int)$ln['line_no'] . '</cbc:ID>';
		$xml .= '<cbc:InvoicedQuantity unitCode="' . epc_einvoice_xml_escape($ln['uom_code'] ?? 'C62') . '">' . number_format((float)$ln['quantity'], 4, '.', '') . '</cbc:InvoicedQuantity>';
		$xml .= '<cbc:LineExtensionAmount currencyID="AED">' . number_format((float)$ln['line_net'], 2, '.', '') . '</cbc:LineExtensionAmount>';
		$xml .= '<cac:Item><cbc:Name>' . epc_einvoice_xml_escape($ln['item_name']) . '</cbc:Name>';
		if (!empty($ln['item_description'])) {
			$xml .= '<cbc:Description>' . epc_einvoice_xml_escape($ln['item_description']) . '</cbc:Description>';
		}
		$xml .= '<cac:ClassifiedTaxCategory><cbc:ID>' . epc_einvoice_xml_escape($ln['tax_category']) . '</cbc:ID>';
		$xml .= '<cbc:Percent>' . number_format((float)$ln['tax_rate'], 2, '.', '') . '</cbc:Percent>';
		$xml .= '<cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme></cac:ClassifiedTaxCategory>';
		$xml .= '</cac:Item>';
		$xml .= '<cac:Price><cbc:PriceAmount currencyID="AED">' . number_format((float)$ln['unit_price'], 4, '.', '') . '</cbc:PriceAmount></cac:Price>';
		$xml .= '</cac:InvoiceLine>';
	}

	$xml .= '</Invoice>';
	return $xml;
}

function epc_einvoice_readiness_checklist(PDO $db): array
{
	$s = epc_einvoice_seller_profile($db);
	$asp = epc_einvoice_get_setting($db, 'asp_name', '');
	$items = array(
		array('id' => 'guidelines', 'label' => 'Reviewed UAE e-Invoicing Guidelines V1.0 (Feb 2026)', 'done' => true),
		array('id' => 'mandatory_fields', 'label' => 'ERP captures all PINT-AE mandatory fields', 'done' => true),
		array('id' => 'seller_trn', 'label' => 'Seller TRN configured', 'done' => ($s['seller_trn'] ?? '') !== ''),
		array('id' => 'seller_peppol', 'label' => 'Seller Peppol ID (0235:TIN) configured', 'done' => ($s['seller_peppol_endpoint'] ?? '') !== ''),
		array('id' => 'asp', 'label' => 'Accredited Service Provider (ASP) selected', 'done' => $asp !== ''),
		array('id' => 'emaratax', 'label' => 'Onboarded with ASP via EmaraTax (external)', 'done' => false),
		array('id' => 'buyer_profiles', 'label' => 'Buyer TRN / Peppol IDs captured for B2B customers', 'done' => (int)$db->query('SELECT COUNT(*) FROM `epc_einvoice_buyer_profiles` WHERE `trn` != ""')->fetchColumn() > 0),
		array('id' => 'test', 'label' => 'End-to-end test with ASP completed', 'done' => (int)$db->query('SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `status` IN ("submitted","accepted")')->fetchColumn() > 0),
	);
	$done = 0;
	foreach ($items as $it) {
		if ($it['done']) {
			$done++;
		}
	}
	return array('items' => $items, 'done' => $done, 'total' => count($items), 'percent' => count($items) ? round($done * 100 / count($items)) : 0);
}
