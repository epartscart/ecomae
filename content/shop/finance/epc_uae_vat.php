<?php
/**
 * UAE VAT (5%) — output on sales to UAE customers, input on UAE supplier purchases.
 * Net payable to FTA = output VAT − input VAT (recoverable).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../pricing/epc_pricing.php';

function epc_uae_vat_uae_country_codes(): array
{
	return array('AE', 'UAE', 'ARE', 'UNITED ARAB EMIRATES', 'U.A.E.', 'U.A.E');
}

function epc_uae_vat_normalize_country(?string $code): string
{
	$code = strtoupper(trim((string)$code));
	if ($code === '') {
		return 'AE';
	}
	if (in_array($code, array('UAE', 'ARE', 'UNITED ARAB EMIRATES', 'U.A.E.', 'U.A.E'), true)) {
		return 'AE';
	}
	return $code;
}

function epc_uae_vat_is_uae_country(?string $code): bool
{
	return epc_uae_vat_normalize_country($code) === 'AE';
}

function epc_uae_vat_rate_percent(PDO $db): float
{
	$rate = (float)epc_pricing_get_setting($db, 'vat_percent', '5.00');
	if ($rate < 0) {
		$rate = 0;
	}
	if ($rate > 100) {
		$rate = 100;
	}
	return round($rate, 2);
}

function epc_uae_vat_rate_decimal(PDO $db): float
{
	return epc_uae_vat_rate_percent($db) / 100;
}

function epc_uae_vat_multiplier(PDO $db): float
{
	return 1 + epc_uae_vat_rate_decimal($db);
}

/** UAE-only sales: output VAT when tenant is AE + VAT-registered (FTA). */
function epc_uae_vat_sales_enabled(PDO $db): bool
{
	$flag = epc_pricing_get_setting($db, 'vat_uae_sales_only', '1');
	if (!($flag === '1' || $flag === 1 || $flag === 'true' || $flag === '')) {
		return false;
	}
	$co = epc_uae_company_profile($db);
	return $co['country_code'] === 'AE' && !empty($co['vat_registered']);
}

/** Tenant may recover input VAT on UAE supplier purchases (AE + TRN + VAT registered). */
function epc_uae_vat_purchases_input_enabled(PDO $db): bool
{
	$co = epc_uae_company_profile($db);
	return !empty($co['fta_ready']);
}

function epc_uae_company_trn_valid(?string $trn): bool
{
	$trn = preg_replace('/\D/', '', (string)$trn);
	return $trn !== '' && strlen($trn) === 15;
}

/**
 * Tenant UAE registration (company country, TRN, VAT flag) — synced from e-invoice seller + price settings.
 */
function epc_uae_company_profile(PDO $db): array
{
	epc_uae_vat_ensure_settings($db);
	$country = epc_uae_vat_normalize_country((string)epc_pricing_get_setting($db, 'company_country_code', 'AE'));
	$trn = preg_replace('/\D/', '', (string)epc_pricing_get_setting($db, 'company_trn', ''));
	$vatRegRaw = epc_pricing_get_setting($db, 'company_vat_registered', '1');
	$vatRegistered = ($vatRegRaw === '1' || $vatRegRaw === 1 || $vatRegRaw === 'true' || $vatRegRaw === '');
	$legalName = trim((string)epc_pricing_get_setting($db, 'company_legal_name', ''));

	$einvoicePath = __DIR__ . '/epc_einvoice.php';
	if (is_readable($einvoicePath)) {
		require_once $einvoicePath;
		$sellerCountry = epc_uae_vat_normalize_country((string)epc_einvoice_get_setting($db, 'seller_country_code', 'AE'));
		if ($sellerCountry !== '') {
			$country = $sellerCountry;
		}
		$sellerTrn = preg_replace('/\D/', '', (string)epc_einvoice_get_setting($db, 'seller_trn', ''));
		if ($sellerTrn !== '') {
			$trn = $sellerTrn;
		}
		if ($legalName === '') {
			$legalName = trim((string)epc_einvoice_get_setting($db, 'seller_name', ''));
		}
	}

	$trnValid = epc_uae_company_trn_valid($trn);
	$ftaReady = ($country === 'AE' && $vatRegistered && $trnValid);

	return array(
		'country_code' => $country,
		'trn' => $trn,
		'trn_display' => $trn !== '' ? $trn : '—',
		'vat_registered' => $vatRegistered,
		'legal_name' => $legalName,
		'fta_ready' => $ftaReady,
		'fta_mode' => $country === 'AE',
	);
}

/** Persist company registration fields (price settings + mirror seller TRN/country when present). */
function epc_uae_company_save_profile(PDO $db, array $data): void
{
	epc_uae_vat_ensure_settings($db);
	$map = array(
		'company_country_code' => 'country_code',
		'company_trn' => 'trn',
		'company_vat_registered' => 'vat_registered',
		'company_legal_name' => 'legal_name',
	);
	$upd = $db->prepare(
		'INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?)
		ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)'
	);
	foreach ($map as $key => $field) {
		if (!array_key_exists($field, $data) && !array_key_exists($key, $data)) {
			continue;
		}
		$val = array_key_exists($key, $data) ? $data[$key] : $data[$field];
		if ($key === 'company_country_code') {
			$val = epc_uae_vat_normalize_country((string)$val);
		}
		if ($key === 'company_trn') {
			$val = preg_replace('/\D/', '', (string)$val);
		}
		if ($key === 'company_vat_registered') {
			$val = (!empty($val) && $val !== '0' && $val !== 'false') ? '1' : '0';
		}
		$upd->execute(array($key, trim((string)$val)));
	}
}

/** After e-invoice seller save — keep company_* aligned for FTA accounting. */
function epc_uae_company_sync_from_seller(PDO $db, array $seller): void
{
	$patch = array(
		'company_country_code' => epc_uae_vat_normalize_country($seller['seller_country_code'] ?? 'AE'),
		'company_trn' => preg_replace('/\D/', '', (string)($seller['seller_trn'] ?? '')),
		'company_legal_name' => trim((string)($seller['seller_name'] ?? '')),
	);
	epc_uae_company_save_profile($db, $patch);
}

/** Map supply reason → GL / purchase treatment tag. */
function epc_uae_vat_treatment_from_supply_reason(string $reason): string
{
	$map = array(
		'standard' => 'standard',
		'non_uae_buyer' => 'export',
		'export' => 'export',
		'margin_scheme' => 'exempt',
	);
	return $map[$reason] ?? 'standard';
}

/** Sales GL / order output VAT treatment for a customer. */
function epc_uae_vat_sales_treatment_for_order(PDO $db, int $user_id, array $transaction_flags = array()): string
{
	if (!epc_uae_vat_sales_enabled($db)) {
		return 'exempt';
	}
	$buyer = array('buyer_country_code' => 'AE', 'country_code' => 'AE');
	if ($user_id > 0) {
		$einvoicePath = __DIR__ . '/epc_einvoice.php';
		if (is_readable($einvoicePath)) {
			require_once $einvoicePath;
			$raw = epc_einvoice_buyer_profile($db, $user_id);
			if ($raw) {
				$buyer = array(
					'buyer_country_code' => $raw['country_code'] ?? 'AE',
					'country_code' => $raw['country_code'] ?? 'AE',
				);
			}
		}
	}
	require_once __DIR__ . '/epc_uae_tax_compliance.php';
	$supply = epc_uae_vat_supply_tax_category($db, $buyer, $transaction_flags);
	return epc_uae_vat_treatment_from_supply_reason((string)($supply['reason'] ?? 'standard'));
}

/** Input VAT purchase treatment from supplier master. */
function epc_uae_vat_purchase_treatment(PDO $db, int $supplier_id, string $requested = 'standard'): string
{
	$allowed = array('standard', 'zero_rated', 'exempt', 'reverse_charge', 'import_rc');
	$requested = in_array($requested, $allowed, true) ? $requested : 'standard';
	$supplier = epc_uae_vat_get_supplier($db, $supplier_id);
	if (!$supplier) {
		return 'exempt';
	}
	$country = epc_uae_vat_normalize_country($supplier['country_code'] ?? 'AE');
	if (!epc_uae_vat_is_uae_country($country)) {
		return 'exempt';
	}
	if (!epc_uae_vat_supplier_input_applicable($supplier)) {
		return 'zero_rated';
	}
	return $requested;
}

/** ERP banner: FTA mode active or advisory with setup link. */
function epc_uae_fta_erp_banner_html(PDO $db, string $erpSetupUrl = ''): string
{
	$co = epc_uae_company_profile($db);
	$einvUrl = $erpSetupUrl !== '' ? $erpSetupUrl . (strpos($erpSetupUrl, '?') !== false ? '&' : '?') . 'tab=einvoice&einv_section=seller' : '';
	$taxUrl = $erpSetupUrl !== '' ? $erpSetupUrl . (strpos($erpSetupUrl, '?') !== false ? '&' : '?') . 'tab=tax_compliance&area=finance' : '';
	if (!empty($co['fta_ready'])) {
		$trn = htmlspecialchars($co['trn_display'], ENT_QUOTES, 'UTF-8');
		$name = htmlspecialchars($co['legal_name'] !== '' ? $co['legal_name'] : 'UAE tenant', ENT_QUOTES, 'UTF-8');
		return '<div class="alert alert-success" style="margin-bottom:12px;"><strong>UAE FTA mode</strong> — company registered in AE'
			. ($name !== '' ? ' (' . $name . ')' : '')
			. ' · TRN: <code>' . $trn . '</code>. Sales output VAT and UAE supplier input VAT follow FTA rules.</div>';
	}
	$issues = array();
	if ($co['country_code'] !== 'AE') {
		$issues[] = 'company country is not AE';
	}
	if (empty($co['vat_registered'])) {
		$issues[] = 'VAT registration flag is off';
	}
	if (!$co['fta_ready'] && $co['country_code'] === 'AE') {
		$issues[] = 'seller TRN missing or invalid (15 digits required)';
	}
	$detail = $issues ? implode('; ', $issues) : 'complete E-invoicing seller profile';
	$links = array();
	if ($einvUrl !== '') {
		$links[] = '<a href="' . htmlspecialchars($einvUrl, ENT_QUOTES, 'UTF-8') . '">E-invoicing seller setup</a>';
	}
	if ($taxUrl !== '') {
		$links[] = '<a href="' . htmlspecialchars($taxUrl, ENT_QUOTES, 'UTF-8') . '">Tax compliance</a>';
	}
	$linkHtml = $links ? (' — ' . implode(' · ', $links)) : '';
	return '<div class="alert alert-warning" style="margin-bottom:12px;"><strong>UAE FTA accounting advisory</strong> — '
		. htmlspecialchars($detail, ENT_QUOTES, 'UTF-8')
		. '. Tax invoices and input VAT recovery require AE country + valid TRN.'
		. $linkHtml . '</div>';
}

function epc_uae_vat_calc_on_exclusive(float $amount_ex, PDO $db): array
{
	$amount_ex = round($amount_ex, 2);
	if (!epc_uae_vat_sales_enabled($db)) {
		return array(
			'amount_ex_vat' => $amount_ex,
			'vat_amount' => 0.0,
			'total_incl_vat' => $amount_ex,
			'vat_rate' => 0.0,
			'vat_applicable' => false,
		);
	}
	$rate = epc_uae_vat_rate_percent($db);
	$vat = round($amount_ex * ($rate / 100), 2);
	return array(
		'amount_ex_vat' => $amount_ex,
		'vat_amount' => $vat,
		'total_incl_vat' => round($amount_ex + $vat, 2),
		'vat_rate' => $rate,
		'vat_applicable' => $vat > 0,
	);
}

function epc_uae_vat_get_supplier(PDO $db, int $supplier_id): ?array
{
	if ($supplier_id <= 0) {
		return null;
	}
	$st = $db->prepare('SELECT * FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($supplier_id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_uae_vat_supplier_input_applicable(?array $supplier): bool
{
	if (!$supplier) {
		return false;
	}
	if (isset($supplier['vat_registered']) && (int)$supplier['vat_registered'] !== 1) {
		return false;
	}
	$country = isset($supplier['country_code']) ? $supplier['country_code'] : 'AE';
	if (!epc_uae_vat_is_uae_country($country)) {
		return false;
	}
	$trn = preg_replace('/\D/', '', (string)($supplier['trn'] ?? ''));
	return epc_uae_company_trn_valid($trn);
}

function epc_uae_vat_purchase_amounts(PDO $db, int $supplier_id, float $amount_ex): array
{
	$amount_ex = round($amount_ex, 2);
	if (!epc_uae_vat_purchases_input_enabled($db)) {
		$supplier = epc_uae_vat_get_supplier($db, $supplier_id);
		return array(
			'amount_ex_vat' => $amount_ex,
			'vat_amount' => 0.0,
			'total_amount' => $amount_ex,
			'vat_rate' => 0.0,
			'vat_applicable' => false,
			'supplier_country' => $supplier ? epc_uae_vat_normalize_country($supplier['country_code'] ?? 'AE') : '',
		);
	}
	$supplier = epc_uae_vat_get_supplier($db, $supplier_id);
	$applicable = epc_uae_vat_supplier_input_applicable($supplier);
	if (!$applicable || $amount_ex <= 0) {
		return array(
			'amount_ex_vat' => $amount_ex,
			'vat_amount' => 0.0,
			'total_amount' => $amount_ex,
			'vat_rate' => 0.0,
			'vat_applicable' => false,
			'supplier_country' => $supplier ? epc_uae_vat_normalize_country($supplier['country_code'] ?? 'AE') : '',
		);
	}
	$rate = epc_uae_vat_rate_percent($db);
	$vat = round($amount_ex * ($rate / 100), 2);
	return array(
		'amount_ex_vat' => $amount_ex,
		'vat_amount' => $vat,
		'total_amount' => round($amount_ex + $vat, 2),
		'vat_rate' => $rate,
		'vat_applicable' => true,
		'supplier_country' => epc_uae_vat_normalize_country($supplier['country_code'] ?? 'AE'),
	);
}

function epc_uae_vat_invoice_totals(PDO $db, float $amount_ex): array
{
	$calc = epc_uae_vat_calc_on_exclusive($amount_ex, $db);
	return array(
		'exclusive_mode' => epc_uae_vat_sales_enabled($db),
		'amount_ex_vat' => $calc['amount_ex_vat'],
		'vat_amount' => $calc['vat_amount'],
		'total_incl_vat' => $calc['total_incl_vat'],
		'vat_rate' => $calc['vat_rate'],
	);
}

/**
 * VAT return summary for a period (completed orders + purchase invoices).
 */
function epc_uae_vat_return_report(PDO $db, int $date_from, int $date_to): array
{
	require_once __DIR__ . '/epc_erp_helpers.php';
	require_once __DIR__ . '/epc_uae_tax_compliance.php';
	epc_uae_tax_compliance_ensure_schema($db);
	$sql = epc_erp_order_sum_sql($db);
	$rate = epc_uae_vat_rate_decimal($db);

	$q = $db->prepare(
		'SELECT
			IFNULL(SUM(' . $sql['sale'] . '), 0) AS sales_ex_vat,
			IFNULL(SUM(' . $sql['purchase'] . '), 0) AS cogs_ex_vat
		FROM `shop_orders`
		WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?'
	);
	$q->execute(array($date_from, $date_to));
	$row = $q->fetch(PDO::FETCH_ASSOC) ?: array();

	$sales_ex = (float)($row['sales_ex_vat'] ?? 0);
	$output_vat = epc_uae_vat_sales_enabled($db) ? round($sales_ex * $rate, 2) : 0.0;
	$sales_incl = round($sales_ex + $output_vat, 2);

	$pq = $db->prepare(
		'SELECT
			IFNULL(SUM(p.`amount_ex_vat`), 0) AS purchase_ex,
			IFNULL(SUM(p.`vat_amount`), 0) AS input_vat,
			IFNULL(SUM(p.`total_amount`), 0) AS purchase_total
		FROM `epc_erp_purchases` p
		WHERE p.`active` = 1 AND p.`purchase_date` >= ? AND p.`purchase_date` <= ?'
	);
	$pq->execute(array($date_from, $date_to));
	$prow = $pq->fetch(PDO::FETCH_ASSOC) ?: array();

	$input_vat = (float)($prow['input_vat'] ?? 0);
	$purchase_ex = (float)($prow['purchase_ex'] ?? 0);
	$net = round($output_vat - $input_vat, 2);

	$gl_output = 0.0;
	$gl_input = 0.0;
	if (function_exists('epc_erp_gl_coa_by_code')) {
		require_once __DIR__ . '/epc_erp_gl.php';
		epc_erp_gl_ensure_schema($db);
		$outCoa = epc_erp_gl_coa_by_code($db, '2100');
		$inCoa = epc_erp_gl_coa_by_code($db, '1150');
		if ($outCoa) {
			$act = epc_erp_gl_coa_activity($db, (int)$outCoa['id'], $date_from, $date_to);
			$gl_output = round((float)$act['credits'] - (float)$act['debits'], 2);
		}
		if ($inCoa) {
			$act = epc_erp_gl_coa_activity($db, (int)$inCoa['id'], $date_from, $date_to);
			$gl_input = round((float)$act['debits'] - (float)$act['credits'], 2);
		}
	}

	$advance = epc_uae_vat_advance_period_summary($db, $date_from, $date_to);
	$expInput = epc_uae_vat_input_expenses_report($db, $date_from, $date_to);

	return array(
		'date_from' => $date_from,
		'date_to' => $date_to,
		'vat_rate_percent' => epc_uae_vat_rate_percent($db),
		'sales_ex_vat' => $sales_ex,
		'output_vat' => $output_vat,
		'sales_incl_vat' => $sales_incl,
		'purchase_ex_vat' => $purchase_ex,
		'input_vat' => $input_vat,
		'purchase_incl_vat' => (float)($prow['purchase_total'] ?? 0),
		'net_vat_payable' => $net,
		'net_status' => $net >= 0 ? 'payable_to_fta' : 'recoverable_from_fta',
		'gl_output_vat_period' => $gl_output,
		'gl_input_vat_period' => $gl_input,
		'cogs_ex_vat' => (float)($row['cogs_ex_vat'] ?? 0),
		'output_vat_on_advances' => $advance['output_vat_on_advances'],
		'advance_vat_credited_on_invoices' => $advance['advance_vat_credited_on_invoices'],
		'unadjusted_advance_vat' => $advance['unadjusted_advance_vat'],
		'advance_payment_count' => $advance['advance_payment_count'],
		'input_vat_expense_lines' => $expInput['lines'],
		'input_vat_recoverable_expenses' => $expInput['totals']['recoverable_vat'],
		'input_vat_blocked_expenses' => $expInput['totals']['blocked_vat'],
		'input_vat_expense_gross' => $expInput['totals']['gross_amount'],
	);
}

function epc_uae_vat_ensure_settings(PDO $db): void
{
	$defaults = array(
		'vat_percent' => '5.00',
		'vat_uae_sales_only' => '1',
		'company_country_code' => 'AE',
		'company_trn' => '',
		'company_vat_registered' => '1',
		'company_legal_name' => '',
	);
	foreach ($defaults as $key => $val) {
		$st = $db->prepare('SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1');
		$st->execute(array($key));
		if ($st->fetchColumn() === false) {
			$db->prepare('INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?)')->execute(array($key, $val));
		}
	}
}

function epc_uae_vat_recalc_purchases(PDO $db): int
{
	require_once __DIR__ . '/epc_erp_schema.php';
	epc_erp_ensure_schema($db);
	$rows = $db->query('SELECT `id`, `supplier_id`, `amount_ex_vat` FROM `epc_erp_purchases` WHERE `active` = 1')->fetchAll(PDO::FETCH_ASSOC);
	$upd = $db->prepare(
		'UPDATE `epc_erp_purchases` SET `vat_amount` = ?, `total_amount` = ?, `vat_applicable` = ?, `vat_rate` = ? WHERE `id` = ?'
	);
	$n = 0;
	foreach ($rows as $r) {
		$calc = epc_uae_vat_purchase_amounts($db, (int)$r['supplier_id'], (float)$r['amount_ex_vat']);
		$upd->execute(array(
			$calc['vat_amount'],
			$calc['total_amount'],
			$calc['vat_applicable'] ? 1 : 0,
			$calc['vat_rate'],
			(int)$r['id'],
		));
		$n++;
	}
	return $n;
}
