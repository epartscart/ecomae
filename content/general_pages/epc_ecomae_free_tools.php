<?php
/**
 * ecomae.com — Free Tools tier.
 *
 * Public, self-serve business tools that anyone can use for their OWN company
 * after a lightweight free registration (email + company + country). Each tool
 * is country-driven: rates, currency, tax label and e-invoice scheme all resolve
 * from the chosen country via epc_country_profile() — never hard-coded to one
 * jurisdiction. Tools are a lead magnet into the full ECOM AE platform.
 *
 * Layers:
 *  - epc_free_tools_catalog()         — tool registry (key, name, icon, blurb).
 *  - epc_free_tools_compute()         — pure compute dispatcher (no DB).
 *  - epc_ecomae_platform_page_free_tools() — hub + per-tool page renderer.
 *  - account/save helpers             — the only DB-backed part (free accounts).
 */

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_law.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_law.php';
}

/*
 * Shared ERP engines reused by the free tools (the "sync rule"): improving a
 * module's engine automatically improves its free tool. Loaded defensively so
 * the public page never fatals if a file is absent.
 */
foreach (array(
	'/content/shop/finance/epc_erp_dataio.php',      // pure CSV parser
	'/content/shop/finance/epc_erp_doc_expiry.php',  // days-left / status / reminder thresholds
	'/content/shop/finance/epc_erp_customs.php',     // CIF / duty / import VAT compute
	'/content/shop/finance/epc_erp_insurance.php',   // classes + country-recommended cover
) as $epcFtEngine) {
	if (is_file($_SERVER['DOCUMENT_ROOT'] . $epcFtEngine)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . $epcFtEngine;
	}
}

if (!function_exists('epc_free_tools_h')) {
	function epc_free_tools_h($v): string
	{
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	}
}

if (!function_exists('epc_free_tools_countries')) {
	/** Countries offered in the picker (covered by the localization packs + generic). */
	function epc_free_tools_countries(): array
	{
		$codes = array('AE', 'SA', 'QA', 'OM', 'BH', 'KW', 'PK', 'IN', 'GB', 'US', 'EG', 'BD', 'NG', 'ZA', 'TR');
		$out = array();
		foreach ($codes as $c) {
			$p = epc_country_profile($c);
			$out[$c] = $p['name'];
		}
		$out['XX'] = 'Other / generic';
		return $out;
	}
}

if (!function_exists('epc_free_tools_cit_rate')) {
	/**
	 * Headline corporate income tax rate + small-profit relief by country (pure).
	 * Country-driven; generic fallback 0%. These are headline references — the
	 * full ECOM AE Tax Toolkit carries date-effective, jurisdiction-complete kits.
	 *
	 * @return array{rate:float,threshold:float,note:string}
	 */
	function epc_free_tools_cit_rate(string $country): array
	{
		$c = strtoupper(trim($country));
		$map = array(
			'AE' => array(9.0, 375000.0, '0% on first AED 375,000; 9% above (UAE CT).'),
			'SA' => array(20.0, 0.0, '20% corporate income tax (Zakat may apply to GCC-owned).'),
			'QA' => array(10.0, 0.0, '10% standard corporate tax.'),
			'OM' => array(15.0, 0.0, '15% standard corporate tax.'),
			'BH' => array(0.0, 0.0, 'No general corporate income tax.'),
			'KW' => array(15.0, 0.0, '15% on foreign corporate bodies.'),
			'PK' => array(29.0, 0.0, '29% corporate tax (standard).'),
			'IN' => array(25.0, 0.0, '~25% (domestic, turnover-linked); 22% under new regime.'),
			'GB' => array(25.0, 50000.0, '19% small-profits up to GBP 50k, 25% main rate.'),
			'US' => array(21.0, 0.0, '21% federal corporate tax (states add their own).'),
			'EG' => array(22.5, 0.0, '22.5% standard corporate tax.'),
			'BD' => array(27.5, 0.0, '27.5% non-listed company rate.'),
			'NG' => array(30.0, 25000000.0, '0% small co (<NGN 25m), 30% large.'),
			'ZA' => array(27.0, 0.0, '27% corporate income tax.'),
			'TR' => array(25.0, 0.0, '25% corporate tax.'),
		);
		if (isset($map[$c])) {
			return array('rate' => $map[$c][0], 'threshold' => $map[$c][1], 'note' => $map[$c][2]);
		}
		return array('rate' => 0.0, 'threshold' => 0.0, 'note' => 'No corporate tax reference for this country — set 0%. Confirm with a local advisor.');
	}
}

if (!function_exists('epc_free_tools_catalog')) {
	/** Registry of free tools. */
	function epc_free_tools_catalog(): array
	{
		return array(
			'vat' => array(
				'name' => 'VAT / GST Return',
				'icon' => 'fa-file-text-o',
				'tag' => 'Tax',
				'blurb' => 'Work out output tax, input tax and net VAT/GST payable for a period — at your country rate.',
			),
			'ct' => array(
				'name' => 'Corporate Tax',
				'icon' => 'fa-balance-scale',
				'tag' => 'Tax',
				'blurb' => 'Estimate corporate income tax on taxable profit using your country rate and small-profit relief.',
			),
			'payroll' => array(
				'name' => 'Payroll & Gratuity',
				'icon' => 'fa-users',
				'tag' => 'People',
				'blurb' => 'Compute net pay and end-of-service gratuity per your country labour law.',
			),
			'ifrs' => array(
				'name' => 'IFRS Financials',
				'icon' => 'fa-line-chart',
				'tag' => 'Finance',
				'blurb' => 'Turn a trial balance into an IFRS-style Income Statement and Balance Sheet.',
			),
			'einvoice' => array(
				'name' => 'Electronic Invoicing',
				'icon' => 'fa-qrcode',
				'tag' => 'Compliance',
				'blurb' => 'Create a compliant tax invoice with your country VAT, e-invoice scheme and Peppol/network endpoint.',
			),
			'extreport' => array(
				'name' => 'External Reporting',
				'icon' => 'fa-institution',
				'tag' => 'Tax',
				'blurb' => 'Generate a filing-ready VAT return and corporate tax return for your country authority — upload a CSV to auto-fill.',
			),
			'customs' => array(
				'name' => 'Customs & Logistics',
				'icon' => 'fa-ship',
				'tag' => 'Trade',
				'blurb' => 'Work out CIF, customs duty, import VAT and landed cost per line — upload an invoice CSV.',
			),
			'insurance' => array(
				'name' => 'Insurance',
				'icon' => 'fa-shield',
				'tag' => 'Risk',
				'blurb' => 'See the compulsory/recommended cover for your country and track a policy renewal with reminder lead times.',
			),
			'docexpiry' => array(
				'name' => 'Document Expiry',
				'icon' => 'fa-calendar-check-o',
				'tag' => 'Risk',
				'blurb' => 'Track licences, visas and contracts — get days-left, status and reminder thresholds. Upload a CSV of documents.',
			),
			'valuation' => array(
				'name' => 'Business Valuation',
				'icon' => 'fa-balance-scale',
				'tag' => 'Finance',
				'blurb' => 'Value your company by DCF, EBITDA-multiple and revenue-multiple, with an equity-value bridge.',
			),
			'finmodel' => array(
				'name' => 'Financial Model',
				'icon' => 'fa-area-chart',
				'tag' => 'Finance',
				'blurb' => 'Project a multi-year P&L (revenue, gross profit, EBITDA, net profit) and download it as CSV.',
			),
			'taxkit' => array(
				'name' => 'Tax Worldwide Kit',
				'icon' => 'fa-globe',
				'tag' => 'Tax',
				'blurb' => 'One-screen tax snapshot for your country: VAT/GST, corporate tax + relief, e-invoice scheme and filing authority.',
			),
			'hrcompliance' => array(
				'name' => 'HR Compliance Worldwide',
				'icon' => 'fa-id-card-o',
				'tag' => 'HR',
				'blurb' => 'Country labour-law card: end-of-service, leave, notice, probation, WPS/social security and the official authority link.',
			),
			'workflow' => array(
				'name' => 'Approval Workflow',
				'icon' => 'fa-sitemap',
				'tag' => 'Operations',
				'blurb' => 'Design a spend-approval workflow with thresholds and approver tiers you can adopt today.',
			),
		);
	}
}

if (!function_exists('epc_free_tools_round')) {
	function epc_free_tools_round($v): float
	{
		return round((float) $v, 2);
	}
}

if (!function_exists('epc_free_tools_num')) {
	/** Parse a possibly-formatted number ("1,234.50", "(500)", "AED 10") to float. */
	function epc_free_tools_num($v): float
	{
		$s = trim((string) $v);
		if ($s === '') {
			return 0.0;
		}
		$neg = (strpos($s, '(') !== false && strpos($s, ')') !== false) || strpos($s, '-') === 0;
		$s = preg_replace('/[^0-9.]/', '', $s);
		$f = $s === '' ? 0.0 : (float) $s;
		return $neg ? -$f : $f;
	}
}

if (!function_exists('epc_free_tools_authority')) {
	/**
	 * Country tax authority + return name reference (country-driven; generic
	 * fallback). Used by the External Reporting tool so the filing pack names
	 * the right body and form for the registered country.
	 *
	 * @return array{authority:string,vat_return:string,ct_return:string}
	 */
	function epc_free_tools_authority(string $country): array
	{
		$c = strtoupper(trim($country));
		$map = array(
			'AE' => array('Federal Tax Authority (FTA)', 'VAT 201', 'CT return'),
			'SA' => array('Zakat, Tax and Customs Authority (ZATCA)', 'VAT return', 'Corporate income tax / Zakat'),
			'QA' => array('General Tax Authority (GTA)', 'VAT return', 'Corporate income tax'),
			'OM' => array('Oman Tax Authority', 'VAT return', 'Corporate income tax'),
			'BH' => array('National Bureau for Revenue (NBR)', 'VAT return', 'No general corporate tax'),
			'KW' => array('Ministry of Finance', 'No VAT', 'Corporate income tax (foreign)'),
			'PK' => array('Federal Board of Revenue (FBR)', 'Sales tax return', 'Income tax return'),
			'IN' => array('GSTN / Income Tax Dept', 'GSTR-3B', 'ITR'),
			'GB' => array('HM Revenue & Customs (HMRC)', 'VAT return (MTD)', 'CT600'),
			'US' => array('IRS / State authorities', 'State sales tax', 'Form 1120'),
			'EG' => array('Egyptian Tax Authority (ETA)', 'VAT return', 'Corporate income tax'),
			'BD' => array('National Board of Revenue (NBR)', 'VAT return (Mushak)', 'Income tax return'),
			'NG' => array('Federal Inland Revenue Service (FIRS)', 'VAT return', 'Company income tax'),
			'ZA' => array('South African Revenue Service (SARS)', 'VAT201', 'ITR14'),
			'TR' => array('Revenue Administration (GIB)', 'KDV beyannamesi', 'Kurumlar vergisi'),
		);
		if (isset($map[$c])) {
			return array('authority' => $map[$c][0], 'vat_return' => $map[$c][1], 'ct_return' => $map[$c][2]);
		}
		return array('authority' => 'Your national tax authority', 'vat_return' => 'VAT/GST return', 'ct_return' => 'Corporate tax return');
	}
}

if (!function_exists('epc_free_tools_parse_csv')) {
	/**
	 * Parse CSV text into header-keyed assoc rows (lowercased, trimmed headers).
	 * Reuses the ERP's pure CSV parser when present (sync rule).
	 *
	 * @return array<int,array<string,string>>
	 */
	function epc_free_tools_parse_csv(string $text): array
	{
		$text = trim($text);
		if ($text === '') {
			return array();
		}
		if (function_exists('epc_dataio_parse_csv')) {
			$rows = epc_dataio_parse_csv($text);
		} else {
			$rows = array();
			foreach (preg_split('/\r\n|\r|\n/', $text) as $line) {
				if (trim($line) === '') {
					continue;
				}
				$rows[] = str_getcsv($line);
			}
		}
		if (!$rows) {
			return array();
		}
		$header = array();
		foreach ((array) array_shift($rows) as $h) {
			$header[] = strtolower(trim((string) $h));
		}
		$out = array();
		foreach ($rows as $r) {
			if (!is_array($r)) {
				continue;
			}
			$blank = true;
			foreach ($r as $v) {
				if (trim((string) $v) !== '') {
					$blank = false;
					break;
				}
			}
			if ($blank) {
				continue;
			}
			$assoc = array();
			foreach ($header as $i => $key) {
				if ($key === '') {
					continue;
				}
				$assoc[$key] = isset($r[$i]) ? trim((string) $r[$i]) : '';
			}
			$out[] = $assoc;
		}
		return $out;
	}
}

if (!function_exists('epc_free_tools_finding')) {
	/** Build a compliance finding row. level ∈ pass|warn|fail. */
	function epc_free_tools_finding(string $level, string $message): array
	{
		return array('level' => $level, 'message' => $message);
	}
}

if (!function_exists('epc_free_tools_compute')) {
	/**
	 * Pure compute dispatcher. Returns array with 'ok' + result payload.
	 *
	 * @param string $tool
	 * @param string $country ISO-2
	 * @param array<string,mixed> $in
	 * @return array<string,mixed>
	 */
	function epc_free_tools_compute(string $tool, string $country, array $in): array
	{
		$prof = epc_country_profile($country);
		switch ($tool) {
			case 'vat':
				return epc_free_tools_compute_vat($prof, $in);
			case 'ct':
				return epc_free_tools_compute_ct($prof, $in);
			case 'payroll':
				return epc_free_tools_compute_payroll($prof, $in);
			case 'ifrs':
				return epc_free_tools_compute_ifrs($prof, $in);
			case 'einvoice':
				return epc_free_tools_compute_einvoice($prof, $in);
			case 'extreport':
				return epc_free_tools_compute_extreport($prof, $in);
			case 'customs':
				return epc_free_tools_compute_customs($prof, $in);
			case 'insurance':
				return epc_free_tools_compute_insurance($prof, $in);
			case 'docexpiry':
				return epc_free_tools_compute_docexpiry($prof, $in);
			case 'valuation':
				return epc_free_tools_compute_valuation($prof, $in);
			case 'finmodel':
				return epc_free_tools_compute_finmodel($prof, $in);
			case 'taxkit':
				return epc_free_tools_compute_taxkit($prof, $in);
			case 'hrcompliance':
				return epc_free_tools_compute_hrcompliance($prof, $in);
			case 'workflow':
				return epc_free_tools_compute_workflow($prof, $in);
		}
		return array('ok' => false, 'message' => 'Unknown tool');
	}
}

if (!function_exists('epc_free_tools_vat_from_csv')) {
	/**
	 * Aggregate a sales/purchase ledger CSV into VAT buckets + compliance.
	 * Expected columns (any order, case-insensitive):
	 *   type (sale|sales|output | purchase|purchases|input), category
	 *   (standard|zero|exempt — defaults standard), amount, trn (optional).
	 *
	 * @return array{buckets:array<string,float>,findings:array,count:int}
	 */
	function epc_free_tools_vat_from_csv(array $rows): array
	{
		$b = array('standard_sales' => 0.0, 'zero_sales' => 0.0, 'exempt_sales' => 0.0, 'standard_purchases' => 0.0, 'import_vat' => 0.0);
		$findings = array();
		$missingTrn = 0;
		$negatives = 0;
		$unknownCat = 0;
		$n = 0;
		foreach ($rows as $i => $r) {
			$type = strtolower((string) ($r['type'] ?? ''));
			$cat = strtolower((string) ($r['category'] ?? 'standard'));
			$amt = epc_free_tools_num($r['amount'] ?? ($r['net'] ?? ($r['value'] ?? 0)));
			if ($amt < 0) {
				$negatives++;
			}
			$isPurchase = (strpos($type, 'purchase') !== false || strpos($type, 'input') !== false || strpos($type, 'expense') !== false);
			$isSale = (strpos($type, 'sale') !== false || strpos($type, 'output') !== false || strpos($type, 'revenue') !== false);
			if (!$isPurchase && !$isSale) {
				$isSale = true; // default unspecified rows to sales
			}
			if ($isSale) {
				if (strpos($cat, 'zero') !== false) {
					$b['zero_sales'] += $amt;
				} elseif (strpos($cat, 'exempt') !== false) {
					$b['exempt_sales'] += $amt;
				} elseif (strpos($cat, 'standard') !== false || $cat === '' || $cat === 'std') {
					$b['standard_sales'] += $amt;
				} else {
					$b['standard_sales'] += $amt;
					$unknownCat++;
				}
				if (trim((string) ($r['trn'] ?? ($r['tax_reg'] ?? ($r['vat_no'] ?? '')))) === '') {
					$missingTrn++;
				}
			} else {
				if (strpos($cat, 'import') !== false) {
					$b['import_vat'] += $amt; // already a tax amount
				} else {
					$b['standard_purchases'] += $amt;
				}
			}
			$n++;
		}
		if ($missingTrn > 0) {
			$findings[] = epc_free_tools_finding('warn', $missingTrn . ' sales row(s) have no tax registration number (TRN) — required on standard-rated tax invoices.');
		}
		if ($negatives > 0) {
			$findings[] = epc_free_tools_finding('warn', $negatives . ' row(s) have a negative amount — confirm these are credit notes, not data errors.');
		}
		if ($unknownCat > 0) {
			$findings[] = epc_free_tools_finding('warn', $unknownCat . ' row(s) had an unrecognised category and were treated as standard-rated.');
		}
		return array('buckets' => $b, 'findings' => $findings, 'count' => $n);
	}
}

if (!function_exists('epc_free_tools_compute_vat')) {
	function epc_free_tools_compute_vat(array $prof, array $in): array
	{
		$rate = (float) $prof['tax_rate'];
		$findings = array();
		$rowCount = 0;
		$csv = trim((string) ($in['csv'] ?? ''));
		if ($csv !== '') {
			$rows = epc_free_tools_parse_csv($csv);
			if (!$rows) {
				$findings[] = epc_free_tools_finding('fail', 'Could not read any data rows from the CSV. Include a header row (type, category, amount, trn).');
			} else {
				$agg = epc_free_tools_vat_from_csv($rows);
				$standardSales = $agg['buckets']['standard_sales'];
				$zeroSales = $agg['buckets']['zero_sales'];
				$exemptSales = $agg['buckets']['exempt_sales'];
				$standardPurch = $agg['buckets']['standard_purchases'];
				$importVat = $agg['buckets']['import_vat'];
				$findings = array_merge($findings, $agg['findings']);
				$rowCount = $agg['count'];
			}
		}
		if ($csv === '' || !isset($standardSales)) {
			$standardSales = epc_free_tools_num($in['standard_sales'] ?? 0);
			$zeroSales = epc_free_tools_num($in['zero_sales'] ?? 0);
			$exemptSales = epc_free_tools_num($in['exempt_sales'] ?? 0);
			$standardPurch = epc_free_tools_num($in['standard_purchases'] ?? 0);
			$importVat = epc_free_tools_num($in['import_vat'] ?? 0);
		}
		$outputTax = $standardSales * $rate / 100.0;
		$inputTax = $standardPurch * $rate / 100.0 + $importVat;
		$net = $outputTax - $inputTax;
		if ($rate <= 0) {
			$findings[] = epc_free_tools_finding('warn', 'No standard VAT/GST rate is configured for this country — output tax is 0.');
		} else {
			$findings[] = epc_free_tools_finding('pass', 'Output and input ' . $prof['tax_label'] . ' computed at the ' . $rate . '% country rate.');
		}
		if ($rowCount > 0) {
			$findings[] = epc_free_tools_finding('pass', $rowCount . ' ledger line(s) classified and totalled.');
		}
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'label' => $prof['tax_label'],
			'rate' => $rate,
			'rows' => array(
				array('Standard-rated sales', epc_free_tools_round($standardSales)),
				array('Zero-rated sales', epc_free_tools_round($zeroSales)),
				array('Exempt sales', epc_free_tools_round($exemptSales)),
				array('Output ' . $prof['tax_label'] . ' (' . $rate . '%)', epc_free_tools_round($outputTax)),
				array('Standard-rated purchases', epc_free_tools_round($standardPurch)),
				array('Import ' . $prof['tax_label'], epc_free_tools_round($importVat)),
				array('Recoverable input ' . $prof['tax_label'], epc_free_tools_round($inputTax)),
			),
			'net' => epc_free_tools_round($net),
			'net_label' => $net >= 0 ? ($prof['tax_label'] . ' payable') : ($prof['tax_label'] . ' refundable'),
			'scheme' => $prof['einvoice'],
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_ct_from_csv')) {
	/**
	 * Aggregate a P&L / trial-balance CSV into revenue / expenses / adjustments.
	 * Columns: type (revenue|income|sales | expense|cost | adjustment|addback|
	 * disallowed), amount. Add-backs/disallowed expenses become positive
	 * adjustments (they increase taxable profit).
	 *
	 * @return array{revenue:float,expenses:float,adjustments:float,findings:array,count:int}
	 */
	function epc_free_tools_ct_from_csv(array $rows): array
	{
		$revenue = 0.0;
		$expenses = 0.0;
		$adjustments = 0.0;
		$unknown = 0;
		$n = 0;
		foreach ($rows as $r) {
			$type = strtolower((string) ($r['type'] ?? ($r['category'] ?? '')));
			$amt = epc_free_tools_num($r['amount'] ?? ($r['value'] ?? 0));
			if (strpos($type, 'revenue') !== false || strpos($type, 'income') !== false || strpos($type, 'sales') !== false || strpos($type, 'turnover') !== false) {
				$revenue += $amt;
			} elseif (strpos($type, 'adjust') !== false || strpos($type, 'addback') !== false || strpos($type, 'add-back') !== false || strpos($type, 'disallow') !== false) {
				$adjustments += $amt;
			} elseif (strpos($type, 'expense') !== false || strpos($type, 'cost') !== false || strpos($type, 'opex') !== false || strpos($type, 'cogs') !== false) {
				$expenses += $amt;
			} else {
				$unknown++;
			}
			$n++;
		}
		$findings = array();
		if ($unknown > 0) {
			$findings[] = epc_free_tools_finding('warn', $unknown . ' row(s) had an unrecognised type and were ignored — use revenue / expense / adjustment.');
		}
		return array('revenue' => $revenue, 'expenses' => $expenses, 'adjustments' => $adjustments, 'findings' => $findings, 'count' => $n);
	}
}

if (!function_exists('epc_free_tools_compute_ct')) {
	function epc_free_tools_compute_ct(array $prof, array $in): array
	{
		$cit = epc_free_tools_cit_rate($prof['country']);
		$findings = array();
		$rowCount = 0;
		$csv = trim((string) ($in['csv'] ?? ''));
		if ($csv !== '') {
			$rows = epc_free_tools_parse_csv($csv);
			if (!$rows) {
				$findings[] = epc_free_tools_finding('fail', 'Could not read any data rows from the CSV. Include a header row (type, amount).');
				$revenue = $expenses = $adjustments = 0.0;
			} else {
				$agg = epc_free_tools_ct_from_csv($rows);
				$revenue = $agg['revenue'];
				$expenses = $agg['expenses'];
				$adjustments = $agg['adjustments'];
				$findings = array_merge($findings, $agg['findings']);
				$rowCount = $agg['count'];
			}
		} else {
			$revenue = epc_free_tools_num($in['revenue'] ?? 0);
			$expenses = epc_free_tools_num($in['expenses'] ?? 0);
			$adjustments = epc_free_tools_num($in['adjustments'] ?? 0);
		}
		$profit = $revenue - $expenses + $adjustments;
		$taxable = max(0.0, $profit);
		$threshold = (float) $cit['threshold'];
		$rate = (float) $cit['rate'];
		$reliefApplied = 0.0;
		if ($threshold > 0 && $taxable <= $threshold) {
			$tax = 0.0;
			$reliefApplied = $taxable;
		} elseif ($threshold > 0) {
			$tax = ($taxable - $threshold) * $rate / 100.0;
			$reliefApplied = $threshold;
		} else {
			$tax = $taxable * $rate / 100.0;
		}
		if ($profit < 0) {
			$findings[] = epc_free_tools_finding('warn', 'A tax loss was computed — no tax is due; the loss may be carried forward subject to local rules.');
		}
		if ($reliefApplied > 0) {
			$findings[] = epc_free_tools_finding('pass', 'Small-business / relief band applied at 0% up to ' . epc_free_tools_round($threshold) . '.');
		}
		if ($rowCount > 0) {
			$findings[] = epc_free_tools_finding('pass', $rowCount . ' ledger line(s) classified into revenue / expenses / adjustments.');
		}
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'rate' => $rate,
			'rows' => array(
				array('Revenue', epc_free_tools_round($revenue)),
				array('Deductible expenses', epc_free_tools_round($expenses)),
				array('Tax adjustments', epc_free_tools_round($adjustments)),
				array('Accounting / taxable profit', epc_free_tools_round($profit)),
				array('Relief (0%) band', epc_free_tools_round($reliefApplied)),
				array('Taxable above relief', epc_free_tools_round(max(0.0, $taxable - $reliefApplied))),
			),
			'net' => epc_free_tools_round($tax),
			'net_label' => 'Estimated corporate tax (' . $rate . '%)',
			'note' => $cit['note'],
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_payroll')) {
	function epc_free_tools_compute_payroll(array $prof, array $in): array
	{
		$basic = (float) ($in['basic'] ?? 0);
		$allowances = (float) ($in['allowances'] ?? 0);
		$deductions = (float) ($in['deductions'] ?? 0);
		$years = (float) ($in['years'] ?? 0);
		$gross = $basic + $allowances;
		$net = $gross - $deductions;
		$grat = array('amount' => 0.0, 'days' => 0.0, 'notes' => 'Gratuity engine unavailable.');
		if (function_exists('epc_hr_gratuity')) {
			$g = epc_hr_gratuity($prof['hr_country'], $basic, $years);
			$grat = array('amount' => (float) $g['amount'], 'days' => (float) $g['days'], 'notes' => (string) $g['notes']);
		}
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'rows' => array(
				array('Basic salary (monthly)', epc_free_tools_round($basic)),
				array('Allowances', epc_free_tools_round($allowances)),
				array('Gross pay', epc_free_tools_round($gross)),
				array('Deductions', epc_free_tools_round($deductions)),
				array('Net pay', epc_free_tools_round($net)),
				array('Years of service', round($years, 2)),
				array('Gratuity days', round($grat['days'], 2)),
				array('End-of-service gratuity', epc_free_tools_round($grat['amount'])),
			),
			'net' => epc_free_tools_round($net),
			'net_label' => 'Net monthly pay',
			'note' => $grat['notes'],
		);
	}
}

if (!function_exists('epc_free_tools_ifrs_from_csv')) {
	/**
	 * Aggregate a trial-balance CSV into IFRS buckets. Each row carries an
	 * account name + debit/credit (or a signed amount) + a classification
	 * (revenue|cogs|opex|other_income|non_current_assets|current_assets|equity|
	 * non_current_liabilities|current_liabilities). Returns the buckets, the
	 * raw debit/credit totals (for the balance check) and compliance findings.
	 *
	 * @return array{buckets:array<string,float>,debit:float,credit:float,findings:array,count:int}
	 */
	function epc_free_tools_ifrs_from_csv(array $rows): array
	{
		$keys = array('revenue', 'cogs', 'opex', 'other_income', 'non_current_assets', 'current_assets', 'equity', 'non_current_liabilities', 'current_liabilities');
		$b = array_fill_keys($keys, 0.0);
		$debit = 0.0;
		$credit = 0.0;
		$unknown = 0;
		$n = 0;
		$alias = array(
			'sales' => 'revenue', 'turnover' => 'revenue', 'income' => 'revenue',
			'cost_of_sales' => 'cogs', 'costofsales' => 'cogs', 'cos' => 'cogs',
			'expenses' => 'opex', 'operating_expenses' => 'opex', 'admin' => 'opex', 'overheads' => 'opex',
			'other' => 'other_income', 'otherincome' => 'other_income',
			'fixed_assets' => 'non_current_assets', 'ppe' => 'non_current_assets', 'nca' => 'non_current_assets',
			'ca' => 'current_assets', 'receivables' => 'current_assets', 'cash' => 'current_assets', 'inventory' => 'current_assets',
			'capital' => 'equity', 'reserves' => 'equity',
			'ncl' => 'non_current_liabilities', 'loans' => 'non_current_liabilities',
			'cl' => 'current_liabilities', 'payables' => 'current_liabilities',
		);
		foreach ($rows as $r) {
			$cls = strtolower(str_replace(array(' ', '-'), '_', (string) ($r['classification'] ?? ($r['class'] ?? ($r['type'] ?? '')))));
			if (isset($alias[$cls])) {
				$cls = $alias[$cls];
			}
			$dr = epc_free_tools_num($r['debit'] ?? 0);
			$cr = epc_free_tools_num($r['credit'] ?? 0);
			if (($r['debit'] ?? '') === '' && ($r['credit'] ?? '') === '') {
				$amt = epc_free_tools_num($r['amount'] ?? ($r['balance'] ?? 0));
				if ($amt >= 0) {
					$dr = $amt;
				} else {
					$cr = -$amt;
				}
			}
			$debit += $dr;
			$credit += $cr;
			$signed = $dr - $cr; // debit-positive
			if (!in_array($cls, $keys, true)) {
				$unknown++;
				$n++;
				continue;
			}
			// Revenue/other income & equity/liabilities are credit-natured.
			if (in_array($cls, array('revenue', 'other_income', 'equity', 'non_current_liabilities', 'current_liabilities'), true)) {
				$b[$cls] += ($cr - $dr);
			} else {
				$b[$cls] += $signed;
			}
			$n++;
		}
		$findings = array();
		if ($unknown > 0) {
			$findings[] = epc_free_tools_finding('warn', $unknown . ' row(s) had no recognised classification and were skipped — add a classification column.');
		}
		return array('buckets' => $b, 'debit' => $debit, 'credit' => $credit, 'findings' => $findings, 'count' => $n);
	}
}

if (!function_exists('epc_free_tools_compute_ifrs')) {
	function epc_free_tools_compute_ifrs(array $prof, array $in): array
	{
		$findings = array();
		$rowCount = 0;
		$tbCheck = null;
		$csv = trim((string) ($in['csv'] ?? ''));
		$buckets = null;
		if ($csv !== '') {
			$rows = epc_free_tools_parse_csv($csv);
			if (!$rows) {
				$findings[] = epc_free_tools_finding('fail', 'Could not read any data rows from the CSV. Include a header row (account, debit, credit, classification).');
			} else {
				$agg = epc_free_tools_ifrs_from_csv($rows);
				$buckets = $agg['buckets'];
				$findings = array_merge($findings, $agg['findings']);
				$rowCount = $agg['count'];
				$tbDiff = round($agg['debit'] - $agg['credit'], 2);
				$tbCheck = abs($tbDiff) < 0.01;
				if ($tbCheck) {
					$findings[] = epc_free_tools_finding('pass', 'Trial balance is in balance (total debits = total credits = ' . epc_free_tools_round($agg['debit']) . ').');
				} else {
					$findings[] = epc_free_tools_finding('fail', 'Trial balance is OUT by ' . epc_free_tools_round($tbDiff) . ' (debits ' . epc_free_tools_round($agg['debit']) . ' vs credits ' . epc_free_tools_round($agg['credit']) . ').');
				}
			}
		}
		$g = function ($k) use ($in, $buckets) {
			if (is_array($buckets) && array_key_exists($k, $buckets)) {
				return (float) $buckets[$k];
			}
			return epc_free_tools_num($in[$k] ?? 0);
		};
		$revenue = $g('revenue');
		$cogs = $g('cogs');
		$opex = $g('opex');
		$other = $g('other_income');
		$grossProfit = $revenue - $cogs;
		$operatingProfit = $grossProfit - $opex + $other;
		// Balance sheet
		$nonCurrentAssets = $g('non_current_assets');
		$currentAssets = $g('current_assets');
		$equity = $g('equity');
		$nonCurrentLiab = $g('non_current_liabilities');
		$currentLiab = $g('current_liabilities');
		$totalAssets = $nonCurrentAssets + $currentAssets;
		$totalEL = $equity + $nonCurrentLiab + $currentLiab;
		$balanced = abs($totalAssets - $totalEL) < 0.01;
		if ($balanced) {
			$findings[] = epc_free_tools_finding('pass', 'Statement of financial position balances (assets = equity + liabilities).');
		} else {
			$findings[] = epc_free_tools_finding('warn', 'Assets (' . epc_free_tools_round($totalAssets) . ') do not equal equity + liabilities (' . epc_free_tools_round($totalEL) . ') — check inputs.');
		}
		if ($rowCount > 0) {
			$findings[] = epc_free_tools_finding('pass', $rowCount . ' trial-balance line(s) mapped to the IFRS statements.');
		}
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'compliance' => $findings,
			'income' => array(
				array('Revenue', epc_free_tools_round($revenue)),
				array('Cost of sales', epc_free_tools_round(-$cogs)),
				array('Gross profit', epc_free_tools_round($grossProfit)),
				array('Operating expenses', epc_free_tools_round(-$opex)),
				array('Other income', epc_free_tools_round($other)),
				array('Operating profit', epc_free_tools_round($operatingProfit)),
			),
			'balance' => array(
				array('Non-current assets', epc_free_tools_round($nonCurrentAssets)),
				array('Current assets', epc_free_tools_round($currentAssets)),
				array('Total assets', epc_free_tools_round($totalAssets)),
				array('Equity', epc_free_tools_round($equity)),
				array('Non-current liabilities', epc_free_tools_round($nonCurrentLiab)),
				array('Current liabilities', epc_free_tools_round($currentLiab)),
				array('Total equity & liabilities', epc_free_tools_round($totalEL)),
			),
			'net' => epc_free_tools_round($operatingProfit),
			'net_label' => 'Operating profit',
			'balanced' => $balanced,
			'note' => $balanced ? 'Balance sheet balances.' : 'Assets do not equal equity + liabilities — check inputs.',
		);
	}
}

if (!function_exists('epc_free_tools_einvoice_scheme')) {
	/**
	 * Country-driven electronic-invoicing scheme: the authority scheme name
	 * (from the country profile), the delivery network and the electronic
	 * address scheme id used on the network endpoint. Generic Peppol fallback.
	 *
	 * @return array{scheme:string,network:string,eas:string,endpoint_label:string}
	 */
	function epc_free_tools_einvoice_scheme(array $prof): array
	{
		$c = strtoupper((string) ($prof['country'] ?? ''));
		$schemeName = (string) ($prof['einvoice'] ?? '');
		$map = array(
			'AE' => array('FTA (PINT-AE)', 'Peppol (PINT-AE)', '0235', 'Tax Registration Number (TRN)'),
			'SA' => array('ZATCA (Fatoora)', 'ZATCA clearance/reporting', 'SA:VAT', 'VAT registration number'),
			'GB' => array('HMRC / Peppol', 'Peppol (BIS Billing 3.0)', '0209', 'GLN / company number'),
			'IN' => array('GST IRP (e-Invoice)', 'GSTN IRP (IRN + QR)', 'IN:GSTIN', 'GSTIN'),
			'EG' => array('ETA e-invoice', 'Egypt ETA platform', 'EG:TIN', 'Tax ID'),
		);
		// EU member states + many others default to Peppol BIS Billing 3.0.
		$peppolEas = array('IT' => '0211', 'DE' => '0204', 'FR' => '0009', 'NL' => '0106', 'BE' => '0208', 'ES' => '0210', 'NO' => '0192', 'SE' => '0007', 'DK' => '0184', 'FI' => '0216', 'AU' => '0151', 'NZ' => '0088', 'SG' => '0195');
		if (isset($map[$c])) {
			$m = $map[$c];
			return array('scheme' => $schemeName !== '' ? $schemeName : $m[0], 'network' => $m[1], 'eas' => $m[2], 'endpoint_label' => $m[3]);
		}
		if (isset($peppolEas[$c])) {
			return array('scheme' => $schemeName !== '' ? $schemeName : 'Peppol BIS Billing 3.0', 'network' => 'Peppol (BIS Billing 3.0)', 'eas' => $peppolEas[$c], 'endpoint_label' => 'VAT / company identifier');
		}
		return array(
			'scheme' => $schemeName !== '' ? $schemeName : 'No mandated e-invoice scheme for this country',
			'network' => 'Peppol (generic)',
			'eas' => '0088',
			'endpoint_label' => 'Tax / company identifier',
		);
	}
}

if (!function_exists('epc_free_tools_compute_einvoice')) {
	function epc_free_tools_compute_einvoice(array $prof, array $in): array
	{
		$rate = (float) $prof['tax_rate'];
		$lines = isset($in['lines']) && is_array($in['lines']) ? $in['lines'] : array();
		$subtotal = 0.0;
		$rows = array();
		foreach ($lines as $ln) {
			$desc = trim((string) ($ln['desc'] ?? ''));
			$qty = (float) ($ln['qty'] ?? 0);
			$price = (float) ($ln['price'] ?? 0);
			if ($desc === '' && $qty == 0 && $price == 0) {
				continue;
			}
			$amt = $qty * $price;
			$subtotal += $amt;
			$rows[] = array('desc' => $desc, 'qty' => $qty, 'price' => epc_free_tools_round($price), 'amount' => epc_free_tools_round($amt));
		}
		$tax = $subtotal * $rate / 100.0;
		$total = $subtotal + $tax;
		$number = strtoupper((string) ($in['number'] ?? ('INV-' . date('Ymd') . '-' . substr(strtoupper(bin2hex(random_bytes(2))), 0, 4))));
		$uuid = sprintf('%08x-%04x-%04x-%04x-%012x', random_int(0, 0xffffffff), random_int(0, 0xffff), random_int(0, 0xffff), random_int(0, 0xffff), random_int(0, 0xffffffffffff));

		$sch = epc_free_tools_einvoice_scheme($prof);
		$sellerTrn = trim((string) ($in['seller_trn'] ?? ''));
		$buyerTrn = trim((string) ($in['buyer_trn'] ?? ''));
		// Network endpoint id, reusing the ERP Peppol helper when available (sync rule).
		$endpoint = '';
		if ($sellerTrn !== '') {
			if (function_exists('epc_einvoice_peppol_endpoint')) {
				$endpoint = epc_einvoice_peppol_endpoint($sellerTrn, $sch['eas']);
			} else {
				$endpoint = $sch['eas'] . ':' . $sellerTrn;
			}
		}

		$findings = array();
		if (count($rows) === 0) {
			$findings[] = epc_free_tools_finding('fail', 'No invoice lines — add at least one line item.');
		}
		if ($rate > 0 && $sellerTrn === '') {
			$findings[] = epc_free_tools_finding('warn', 'Seller tax registration number is required on a ' . $prof['tax_label'] . ' tax invoice in ' . $prof['name'] . '.');
		}
		if (trim((string) ($in['seller'] ?? '')) === '') {
			$findings[] = epc_free_tools_finding('warn', 'Seller legal name is missing.');
		}
		if (!empty($endpoint)) {
			$findings[] = epc_free_tools_finding('pass', 'Network endpoint built for ' . $sch['network'] . ': ' . $endpoint . '.');
		}
		$findings[] = epc_free_tools_finding('pass', 'Invoice formatted for the ' . $sch['scheme'] . ' scheme (' . $prof['name'] . ').');

		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'rate' => $rate,
			'label' => $prof['tax_label'],
			'scheme' => $sch['scheme'],
			'network' => $sch['network'],
			'endpoint' => $endpoint,
			'endpoint_label' => $sch['endpoint_label'],
			'number' => $number,
			'uuid' => $uuid,
			'seller' => trim((string) ($in['seller'] ?? '')),
			'seller_trn' => $sellerTrn,
			'buyer' => trim((string) ($in['buyer'] ?? '')),
			'buyer_trn' => $buyerTrn,
			'date' => date('Y-m-d'),
			'lines' => $rows,
			'subtotal' => epc_free_tools_round($subtotal),
			'tax' => epc_free_tools_round($tax),
			'net' => epc_free_tools_round($total),
			'net_label' => 'Invoice total',
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_extreport')) {
	/**
	 * External Reporting filing pack: reuses the VAT and CT compute (sync rule)
	 * and wraps them with the country's authority + return names, so the output
	 * is a filing-ready pack for the registered country.
	 */
	function epc_free_tools_compute_extreport(array $prof, array $in): array
	{
		$auth = epc_free_tools_authority($prof['country']);
		$vat = epc_free_tools_compute_vat($prof, $in);
		$ct = epc_free_tools_compute_ct($prof, $in);
		$findings = array_merge(
			isset($vat['compliance']) ? $vat['compliance'] : array(),
			isset($ct['compliance']) ? $ct['compliance'] : array()
		);
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'authority' => $auth['authority'],
			'vat_return_name' => $auth['vat_return'],
			'ct_return_name' => $auth['ct_return'],
			'vat' => $vat,
			'ct' => $ct,
			'net' => $vat['net'],
			'net_label' => $auth['vat_return'] . ' net ' . $prof['tax_label'],
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_customs')) {
	/**
	 * Customs & Logistics: CIF, duty, import VAT and landed cost per line.
	 * Reuses the ERP customs engine epc_cust_compute() (sync rule) when present;
	 * otherwise a self-contained fallback keeps the tool working. Lines come
	 * from a CSV (hs_code/hs, qty, unit_value/value) or manual fields.
	 */
	function epc_free_tools_compute_customs(array $prof, array $in): array
	{
		$country = (string) $prof['country'];
		$freight = epc_free_tools_num($in['freight'] ?? 0);
		$insurance = epc_free_tools_num($in['insurance'] ?? 0);
		$fx = epc_free_tools_num($in['fx_rate'] ?? 1) ?: 1.0;
		$other = epc_free_tools_num($in['other'] ?? 0);
		$findings = array();
		$lines = array();
		$csv = trim((string) ($in['csv'] ?? ''));
		if ($csv !== '') {
			$rows = epc_free_tools_parse_csv($csv);
			$missingHs = 0;
			$zeroVal = 0;
			foreach ($rows as $r) {
				$hs = trim((string) ($r['hs_code'] ?? ($r['hs'] ?? ($r['hscode'] ?? ''))));
				$qty = epc_free_tools_num($r['qty'] ?? ($r['quantity'] ?? 1)) ?: 1.0;
				$uv = epc_free_tools_num($r['unit_value'] ?? ($r['value'] ?? ($r['price'] ?? 0)));
				if ($hs === '') {
					$missingHs++;
				}
				if ($uv <= 0) {
					$zeroVal++;
				}
				$lines[] = array('hs_code' => $hs, 'qty' => $qty, 'unit_value' => $uv);
			}
			if ($missingHs > 0) {
				$findings[] = epc_free_tools_finding('warn', $missingHs . ' line(s) have no HS code — duty defaults to the country standard rate.');
			}
			if ($zeroVal > 0) {
				$findings[] = epc_free_tools_finding('warn', $zeroVal . ' line(s) have a zero/blank value — customs may reject undervalued goods.');
			}
		} else {
			$lines[] = array(
				'hs_code' => trim((string) ($in['hs_code'] ?? '')),
				'qty' => epc_free_tools_num($in['qty'] ?? 1) ?: 1.0,
				'unit_value' => epc_free_tools_num($in['unit_value'] ?? 0),
			);
		}
		$hdr = array('country' => $country, 'freight' => $freight, 'insurance' => $insurance, 'fx_rate' => $fx, 'regime' => (string) ($in['regime'] ?? 'import_for_home'));

		if (function_exists('epc_cust_compute')) {
			$res = epc_cust_compute($hdr, $lines);
			$packLabel = function_exists('epc_cust_country_pack') ? (string) (epc_cust_country_pack($country)['label'] ?? '') : '';
		} else {
			// Fallback (engine absent): flat country VAT rate, 5% default duty.
			$rate = (float) $prof['tax_rate'];
			$goods = 0.0;
			foreach ($lines as $ln) {
				$goods += $ln['qty'] * $ln['unit_value'] * $fx;
			}
			$cif = $goods + $freight * $fx + $insurance * $fx;
			$duty = $cif * 5.0 / 100.0;
			$vat = ($cif + $duty) * $rate / 100.0;
			$res = array('goods_value' => $goods, 'freight' => $freight * $fx, 'insurance' => $insurance * $fx, 'cif_value' => $cif, 'duty_total' => $duty, 'vat_total' => $vat, 'total_payable' => $duty + $vat, 'lines' => $lines);
			$packLabel = '';
		}
		$landed = (float) $res['cif_value'] + (float) $res['duty_total'] + $other * $fx;
		$findings[] = epc_free_tools_finding('pass', 'CIF, duty and import ' . $prof['tax_label'] . ' computed for ' . $prof['name'] . '.');
		$rows = array(
			array('Goods value (CIF basis)', epc_free_tools_round($res['goods_value'])),
			array('Freight', epc_free_tools_round($res['freight'])),
			array('Insurance', epc_free_tools_round($res['insurance'])),
			array('CIF value', epc_free_tools_round($res['cif_value'])),
			array('Customs duty', epc_free_tools_round($res['duty_total'])),
			array('Import ' . $prof['tax_label'], epc_free_tools_round($res['vat_total'])),
			array('Other landed costs', epc_free_tools_round($other * $fx)),
			array('Total landed cost', epc_free_tools_round($landed)),
		);
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'pack' => $packLabel,
			'rows' => $rows,
			'lines' => $res['lines'],
			'net' => epc_free_tools_round($res['total_payable']),
			'net_label' => 'Duty + import ' . $prof['tax_label'] . ' payable',
			'landed_cost' => epc_free_tools_round($landed),
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_insurance')) {
	/**
	 * Insurance: country-driven compulsory/recommended cover (reuses
	 * epc_ins_country_recommended + epc_ins_class_label), plus a single-policy
	 * premium/renewal helper that reuses the doc-expiry status engine.
	 */
	function epc_free_tools_compute_insurance(array $prof, array $in): array
	{
		$country = (string) $prof['country'];
		$recommended = array();
		if (function_exists('epc_ins_country_recommended')) {
			foreach (epc_ins_country_recommended($country) as $r) {
				$label = function_exists('epc_ins_class_label') ? epc_ins_class_label((string) $r['class']) : ucfirst(str_replace('_', ' ', (string) $r['class']));
				$recommended[] = array('class' => $label, 'basis' => (string) $r['basis']);
			}
		}
		$sumInsured = epc_free_tools_num($in['sum_insured'] ?? 0);
		$ratePct = epc_free_tools_num($in['rate'] ?? 0);
		$premium = epc_free_tools_num($in['premium'] ?? 0);
		if ($premium <= 0 && $sumInsured > 0 && $ratePct > 0) {
			$premium = $sumInsured * $ratePct / 100.0;
		}
		$findings = array();
		$rows = array();
		if ($sumInsured > 0) {
			$rows[] = array('Sum insured', epc_free_tools_round($sumInsured));
		}
		if ($ratePct > 0) {
			$rows[] = array('Premium rate (%)', round($ratePct, 4));
		}
		if ($premium > 0) {
			$rows[] = array('Annual premium', epc_free_tools_round($premium));
		}
		// Renewal / expiry status reusing the doc-expiry engine.
		$expiryStr = trim((string) ($in['expiry'] ?? ''));
		$daysLeft = null;
		$status = '';
		if ($expiryStr !== '') {
			$ts = strtotime($expiryStr);
			if ($ts !== false) {
				$daysLeft = function_exists('epc_docx_days_left') ? epc_docx_days_left($ts) : (int) floor(($ts - time()) / 86400);
				$status = function_exists('epc_docx_status') ? epc_docx_status($ts) : ($daysLeft < 0 ? 'expired' : ($daysLeft <= 30 ? 'expiring' : 'valid'));
				$rows[] = array('Policy expiry', date('Y-m-d', $ts));
				$rows[] = array('Days to renewal', $daysLeft);
				if ($status === 'expired') {
					$findings[] = epc_free_tools_finding('fail', 'Policy has EXPIRED — arrange cover immediately; a gap in cover is a serious risk.');
				} elseif ($status === 'expiring') {
					$findings[] = epc_free_tools_finding('warn', 'Policy renews within 30 days — start the renewal now.');
				} else {
					$findings[] = epc_free_tools_finding('pass', 'Policy is valid for ' . $daysLeft . ' more day(s).');
				}
			}
		}
		$findings[] = epc_free_tools_finding('pass', count($recommended) . ' compulsory/recommended cover(s) listed for ' . $prof['name'] . '.');
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'recommended' => $recommended,
			'rows' => $rows,
			'status' => $status,
			'days_left' => $daysLeft,
			'net' => epc_free_tools_round($premium),
			'net_label' => 'Annual premium',
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_docexpiry')) {
	/**
	 * Document Expiry: days-left, status and the next reminder threshold per
	 * document, reusing the doc-expiry engine (sync rule). Documents come from
	 * a CSV (title, expiry, reminder_days) or a single manual entry.
	 */
	function epc_free_tools_compute_docexpiry(array $prof, array $in): array
	{
		$defaultReminders = trim((string) ($in['reminder_days'] ?? '90,60,30,7'));
		$items = array();
		$csv = trim((string) ($in['csv'] ?? ''));
		if ($csv !== '') {
			foreach (epc_free_tools_parse_csv($csv) as $r) {
				$items[] = array(
					'title' => trim((string) ($r['title'] ?? ($r['document'] ?? ($r['name'] ?? 'Document')))),
					'expiry' => trim((string) ($r['expiry'] ?? ($r['expiry_date'] ?? ($r['expires'] ?? '')))),
					'reminder_days' => trim((string) ($r['reminder_days'] ?? $defaultReminders)),
				);
			}
		} else {
			$items[] = array(
				'title' => trim((string) ($in['title'] ?? 'Document')),
				'expiry' => trim((string) ($in['expiry'] ?? '')),
				'reminder_days' => $defaultReminders,
			);
		}
		$rows = array();
		$findings = array();
		$expired = 0;
		$expiring = 0;
		foreach ($items as $it) {
			$ts = $it['expiry'] !== '' ? strtotime($it['expiry']) : false;
			if ($ts === false) {
				$rows[] = array('title' => $it['title'], 'expiry' => '—', 'days_left' => '—', 'status' => 'no date', 'next_reminder' => '—');
				continue;
			}
			$daysLeft = function_exists('epc_docx_days_left') ? epc_docx_days_left($ts) : (int) floor(($ts - time()) / 86400);
			$status = function_exists('epc_docx_status') ? epc_docx_status($ts) : ($daysLeft < 0 ? 'expired' : ($daysLeft <= 30 ? 'expiring' : 'valid'));
			$reminders = function_exists('epc_docx_parse_reminder_days') ? epc_docx_parse_reminder_days($it['reminder_days']) : array(90, 60, 30, 7);
			$due = function_exists('epc_docx_due_thresholds') ? epc_docx_due_thresholds($ts, $reminders) : array();
			$next = '';
			if ($due) {
				$next = 'due now (' . $due[0] . 'd)';
			} else {
				// Next future threshold above days-left.
				$future = array();
				foreach ($reminders as $d) {
					if ($daysLeft > $d) {
						$future[] = $d;
					}
				}
				$next = $future ? ('at ' . max($future) . 'd') : '—';
			}
			if ($status === 'expired') {
				$expired++;
			} elseif ($status === 'expiring') {
				$expiring++;
			}
			$rows[] = array('title' => $it['title'], 'expiry' => date('Y-m-d', $ts), 'days_left' => $daysLeft, 'status' => $status, 'next_reminder' => $next);
		}
		if ($expired > 0) {
			$findings[] = epc_free_tools_finding('fail', $expired . ' document(s) already EXPIRED — act now.');
		}
		if ($expiring > 0) {
			$findings[] = epc_free_tools_finding('warn', $expiring . ' document(s) expiring within 30 days.');
		}
		$findings[] = epc_free_tools_finding('pass', count($rows) . ' document(s) tracked with reminder lead days ' . $defaultReminders . '.');
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'doc_rows' => $rows,
			'net' => count($rows),
			'net_label' => 'Documents tracked',
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_valuation')) {
	/**
	 * Business valuation: DCF (Gordon growth on FCF), EBITDA-multiple and
	 * revenue-multiple, with an equity-value bridge (EV − net debt). Country
	 * currency from the profile. Can read revenue/EBITDA from a P&L CSV.
	 */
	function epc_free_tools_compute_valuation(array $prof, array $in): array
	{
		$findings = array();
		$revenue = epc_free_tools_num($in['revenue'] ?? 0);
		$ebitda = epc_free_tools_num($in['ebitda'] ?? 0);
		$csv = trim((string) ($in['csv'] ?? ''));
		if ($csv !== '') {
			$agg = epc_free_tools_ct_from_csv(epc_free_tools_parse_csv($csv));
			if ($revenue <= 0) {
				$revenue = $agg['revenue'];
			}
			if ($ebitda <= 0) {
				$ebitda = $agg['revenue'] - $agg['expenses'];
			}
			$findings = array_merge($findings, $agg['findings']);
		}
		$netDebt = epc_free_tools_num($in['net_debt'] ?? 0);
		$growth = epc_free_tools_num($in['growth'] ?? 5);          // % long-term
		$discount = epc_free_tools_num($in['discount'] ?? 15) ?: 15.0; // % WACC
		$ebitdaMult = epc_free_tools_num($in['ebitda_multiple'] ?? 6) ?: 6.0;
		$revMult = epc_free_tools_num($in['revenue_multiple'] ?? 1.5) ?: 1.5;
		$taxRate = epc_free_tools_num($in['tax_rate'] ?? 0);

		// Simple DCF: FCF proxy = EBITDA * (1 - tax). Gordon growth perpetuity.
		$fcf = $ebitda * (1 - $taxRate / 100.0);
		$g = $growth / 100.0;
		$d = $discount / 100.0;
		$dcfEv = ($d > $g) ? ($fcf * (1 + $g) / ($d - $g)) : 0.0;
		if ($d <= $g) {
			$findings[] = epc_free_tools_finding('warn', 'Discount rate must exceed the growth rate for a finite DCF — DCF set to 0.');
		}
		$ebitdaEv = $ebitda * $ebitdaMult;
		$revEv = $revenue * $revMult;
		$avgEv = ($dcfEv + $ebitdaEv + $revEv) / 3.0;
		$equity = $avgEv - $netDebt;

		if ($ebitda <= 0) {
			$findings[] = epc_free_tools_finding('warn', 'EBITDA is zero/negative — multiple and DCF methods are unreliable; rely on revenue multiple.');
		} else {
			$findings[] = epc_free_tools_finding('pass', 'Three valuation methods computed and averaged.');
		}
		$rows = array(
			array('DCF enterprise value', epc_free_tools_round($dcfEv)),
			array('EBITDA-multiple EV (' . round($ebitdaMult, 2) . 'x)', epc_free_tools_round($ebitdaEv)),
			array('Revenue-multiple EV (' . round($revMult, 2) . 'x)', epc_free_tools_round($revEv)),
			array('Average enterprise value', epc_free_tools_round($avgEv)),
			array('Less: net debt', epc_free_tools_round(-$netDebt)),
			array('Equity value', epc_free_tools_round($equity)),
		);
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'rows' => $rows,
			'net' => epc_free_tools_round($equity),
			'net_label' => 'Estimated equity value',
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_finmodel')) {
	/**
	 * Financial model: multi-year P&L projection from a base revenue, growth %,
	 * gross-margin %, opex % of revenue and a horizon. Country currency applied.
	 */
	function epc_free_tools_compute_finmodel(array $prof, array $in): array
	{
		$revenue = epc_free_tools_num($in['revenue'] ?? 0);
		$csv = trim((string) ($in['csv'] ?? ''));
		if ($csv !== '' && $revenue <= 0) {
			$agg = epc_free_tools_ct_from_csv(epc_free_tools_parse_csv($csv));
			$revenue = $agg['revenue'];
		}
		$growth = epc_free_tools_num($in['growth'] ?? 10) / 100.0;
		$gm = epc_free_tools_num($in['gross_margin'] ?? 40) / 100.0;
		$opexPct = epc_free_tools_num($in['opex_pct'] ?? 25) / 100.0;
		$taxRate = epc_free_tools_num($in['tax_rate'] ?? 0) / 100.0;
		$years = (int) epc_free_tools_num($in['years'] ?? 5);
		if ($years < 1) {
			$years = 3;
		}
		if ($years > 10) {
			$years = 10;
		}
		$projection = array();
		$rev = $revenue;
		$baseYear = (int) date('Y');
		for ($i = 0; $i < $years; $i++) {
			$yrRev = $i === 0 ? $rev : $rev * (1 + $growth);
			$rev = $yrRev;
			$gross = $yrRev * $gm;
			$opexAmt = $yrRev * $opexPct;
			$ebitda = $gross - $opexAmt;
			$net = $ebitda * (1 - $taxRate);
			$projection[] = array(
				'year' => $baseYear + $i,
				'revenue' => epc_free_tools_round($yrRev),
				'gross_profit' => epc_free_tools_round($gross),
				'ebitda' => epc_free_tools_round($ebitda),
				'net_profit' => epc_free_tools_round($net),
			);
		}
		$findings = array(epc_free_tools_finding('pass', $years . '-year projection built at ' . round($growth * 100, 1) . '% growth, ' . round($gm * 100, 1) . '% gross margin.'));
		if ($revenue <= 0) {
			$findings[] = epc_free_tools_finding('warn', 'Base revenue is zero — enter a starting revenue (or upload a P&L CSV).');
		}
		$lastNet = $projection ? $projection[count($projection) - 1]['net_profit'] : 0.0;
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'projection' => $projection,
			'net' => $lastNet,
			'net_label' => 'Year ' . ($baseYear + $years - 1) . ' net profit',
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_taxkit')) {
	/**
	 * Tax Worldwide Kit: a country tax snapshot reusing the localization
	 * profile, the corporate-tax band and the authority registry (sync rule).
	 */
	function epc_free_tools_compute_taxkit(array $prof, array $in): array
	{
		$cit = epc_free_tools_cit_rate($prof['country']);
		$auth = epc_free_tools_authority($prof['country']);
		$sch = epc_free_tools_einvoice_scheme($prof);
		$fyStart = (int) ($prof['fiscal_year_start_month'] ?? 1);
		$months = array(1 => 'January', 2 => 'February', 3 => 'March', 4 => 'April', 5 => 'May', 6 => 'June', 7 => 'July', 8 => 'August', 9 => 'September', 10 => 'October', 11 => 'November', 12 => 'December');
		$rows = array(
			array('Country', $prof['name'] . ' (' . $prof['country'] . ')'),
			array('Currency', $prof['currency']),
			array($prof['tax_label'] . ' standard rate', $prof['tax_rate'] . '%'),
			array('Corporate tax rate', $cit['rate'] . '%'),
			array('Small-business / relief threshold', $cit['threshold'] > 0 ? ($prof['currency'] . ' ' . number_format((float) $cit['threshold'], 2)) : 'None'),
			array('E-invoice scheme', $sch['scheme']),
			array('E-invoice network', $sch['network']),
			array('Filing authority', $auth['authority']),
			array('VAT/GST return', $auth['vat_return']),
			array('Corporate tax return', $auth['ct_return']),
			array('Fiscal year starts', $months[$fyStart] ?? 'January'),
		);
		$findings = array(
			epc_free_tools_finding('pass', 'Tax profile resolved for ' . $prof['name'] . ' from your registered country.'),
			epc_free_tools_finding($prof['tax_rate'] > 0 ? 'pass' : 'warn', $prof['tax_rate'] > 0 ? ($prof['tax_label'] . ' applies at ' . $prof['tax_rate'] . '%.') : ('No general ' . $prof['tax_label'] . ' in this country.')),
		);
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'rows_text' => $rows,
			'net' => $prof['tax_rate'],
			'net_label' => $prof['tax_label'] . ' rate (%)',
			'note' => $cit['note'],
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_hrcompliance')) {
	/**
	 * HR Compliance Worldwide: the country labour-law card + (optional)
	 * per-employee compliance check + end-of-service liability. Reuses the ERP
	 * labour-law engine (epc_hr_law_profile / epc_hr_compliance_check /
	 * epc_hr_gratuity) so it stays in sync. Generic fallback if engine absent.
	 */
	function epc_free_tools_compute_hrcompliance(array $prof, array $in): array
	{
		$country = (string) ($prof['hr_country'] ?? $prof['country']);
		$findings = array();
		$rows = array();
		$authority = '';
		if (function_exists('epc_hr_law_profile')) {
			$p = epc_hr_law_profile($country);
			$authority = (string) ($p['authority_url'] ?? '');
			$rows = array(
				array('Country', (string) ($p['name'] ?? $country)),
				array('Standard weekly hours', (string) ($p['weekly_hours'] ?? '—')),
				array('Work week', (string) ($p['workweek'] ?? '—')),
				array('Overtime', (string) ($p['overtime'] ?? '—')),
				array('Max probation', (string) ($p['probation_max_months'] ?? '—') . ' months'),
				array('Notice period', (string) ($p['notice_days'] ?? '—') . ' days'),
				array('Annual leave', (string) ($p['annual_leave_days'] ?? '—') . ' days/yr'),
				array('Sick leave', (string) ($p['sick_leave'] ?? '—')),
				array('Maternity', (string) ($p['maternity'] ?? '—')),
				array('Paternity', (string) ($p['paternity'] ?? '—')),
				array('Public holidays', (string) ($p['public_holidays'] ?? '—')),
				array('End-of-service', (string) ($p['eos'] ?? '—')),
				array('Wage protection', (string) ($p['wage_protection'] ?? '—')),
				array('Statutory basis', (string) ($p['authority'] ?? '—')),
			);
			$findings[] = epc_free_tools_finding('pass', 'Labour-law card resolved for ' . (string) ($p['name'] ?? $country) . '.');
		} else {
			$findings[] = epc_free_tools_finding('warn', 'Labour-law engine unavailable — showing generic guidance.');
		}

		// Optional per-employee check.
		$basic = epc_free_tools_num($in['basic_salary'] ?? 0);
		$hireStr = trim((string) ($in['hire_date'] ?? ''));
		$empRows = array();
		$flags = array();
		$eos = 0.0;
		if ($basic > 0 && $hireStr !== '') {
			$hireTs = strtotime($hireStr);
			if ($hireTs !== false) {
				$years = max(0.0, (time() - $hireTs) / (365.25 * 86400));
				if (function_exists('epc_hr_compliance_check')) {
					$chk = epc_hr_compliance_check($country, array('hire_date' => $hireTs, 'basic_salary' => $basic, 'leave_balance_days' => epc_free_tools_num($in['leave_balance'] ?? 0), 'name' => 'Employee'));
					$eos = (float) ($chk['eos_liability'] ?? 0);
					foreach ((array) ($chk['flags'] ?? array()) as $f) {
						$lvl = ((string) ($f['severity'] ?? 'info') === 'high') ? 'fail' : 'warn';
						$flags[] = epc_free_tools_finding($lvl, (string) ($f['message'] ?? '') . (isset($f['basis']) ? (' (' . $f['basis'] . ')') : ''));
					}
					$empRows[] = array('Service years', round((float) ($chk['service_years'] ?? $years), 2));
					$empRows[] = array('In probation', !empty($chk['in_probation']) ? 'Yes' : 'No');
					$empRows[] = array('End-of-service liability', epc_free_tools_round($eos));
				} elseif (function_exists('epc_hr_gratuity')) {
					$g = epc_hr_gratuity($country, $basic, $years);
					$eos = (float) $g['amount'];
					$empRows[] = array('Service years', round($years, 2));
					$empRows[] = array('End-of-service liability', epc_free_tools_round($eos));
				}
				$findings = array_merge($findings, $flags);
				if (!$flags) {
					$findings[] = epc_free_tools_finding('pass', 'No labour-law compliance flags for this employee.');
				}
			}
		}

		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'rows_text' => $rows,
			'emp_rows' => $empRows,
			'authority_url' => $authority,
			'net' => epc_free_tools_round($eos),
			'net_label' => 'End-of-service liability',
			'compliance' => $findings,
		);
	}
}

if (!function_exists('epc_free_tools_compute_workflow')) {
	function epc_free_tools_compute_workflow(array $prof, array $in): array
	{
		$cur = $prof['currency'];
		$t1 = (float) ($in['tier1'] ?? 5000);
		$t2 = (float) ($in['tier2'] ?? 50000);
		$a0 = trim((string) ($in['approver0'] ?? 'Line manager'));
		$a1 = trim((string) ($in['approver1'] ?? 'Department head'));
		$a2 = trim((string) ($in['approver2'] ?? 'Finance director'));
		$steps = array(
			array('range' => 'Up to ' . $cur . ' ' . number_format($t1, 2), 'approver' => $a0, 'sla' => '1 business day'),
			array('range' => $cur . ' ' . number_format($t1, 2) . ' – ' . number_format($t2, 2), 'approver' => $a0 . ' → ' . $a1, 'sla' => '2 business days'),
			array('range' => 'Above ' . $cur . ' ' . number_format($t2, 2), 'approver' => $a0 . ' → ' . $a1 . ' → ' . $a2, 'sla' => '3 business days'),
		);
		return array(
			'ok' => true,
			'currency' => $cur,
			'steps' => $steps,
			'net' => 3,
			'net_label' => 'Approval tiers configured',
			'note' => 'Adopt this as your purchase-requisition approval matrix. The full ECOM AE platform enforces it automatically on every requisition and PO.',
		);
	}
}

/* ------------------------------------------------------------------ *
 *  Free account storage (the only DB-backed part)                     *
 * ------------------------------------------------------------------ */

if (!function_exists('epc_free_tools_db')) {
	function epc_free_tools_db(): ?PDO
	{
		static $pdo = null;
		if ($pdo !== null) {
			return $pdo ?: null;
		}
		try {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
			$cfg = new DP_Config();
			$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
			$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		} catch (Throwable $e) {
			$pdo = false;
			return null;
		}
		return $pdo;
	}
}

if (!function_exists('epc_free_tools_ensure_schema')) {
	function epc_free_tools_ensure_schema(PDO $db): void
	{
		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_free_tool_accounts` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`token` varchar(64) NOT NULL,
				`email` varchar(190) NOT NULL,
				`company` varchar(190) DEFAULT NULL,
				`country` varchar(4) DEFAULT NULL,
				`time_created` int(11) DEFAULT NULL,
				`time_last_seen` int(11) DEFAULT NULL,
				`use_count` int(11) DEFAULT 0,
				PRIMARY KEY (`id`),
				UNIQUE KEY `token` (`token`),
				KEY `email` (`email`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Free Tools tier accounts'"
		);
		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_free_tool_saves` (
				`id` int(11) NOT NULL AUTO_INCREMENT,
				`account_id` int(11) NOT NULL,
				`tool` varchar(32) NOT NULL,
				`country` varchar(4) DEFAULT NULL,
				`title` varchar(190) DEFAULT NULL,
				`payload` mediumtext,
				`time_created` int(11) DEFAULT NULL,
				PRIMARY KEY (`id`),
				KEY `account_id` (`account_id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Free Tools saved results'"
		);
	}
}

if (!function_exists('epc_free_tools_register')) {
	/**
	 * Create or refresh a free-tools account, returning the token.
	 *
	 * @return array{ok:bool,token?:string,message?:string,account?:array}
	 */
	function epc_free_tools_register(string $email, string $company, string $country): array
	{
		$email = strtolower(trim($email));
		if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
			return array('ok' => false, 'message' => 'Enter a valid email address.');
		}
		$db = epc_free_tools_db();
		if ($db === null) {
			return array('ok' => false, 'message' => 'Service unavailable, please try again.');
		}
		epc_free_tools_ensure_schema($db);
		$country = strtoupper(preg_replace('/[^A-Za-z]/', '', $country)) ?: 'XX';
		$now = time();
		$st = $db->prepare("SELECT * FROM `epc_free_tool_accounts` WHERE `email`=? LIMIT 1");
		$st->execute(array($email));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		$returning = false;
		if ($row) {
			// One email = one account. Never create a duplicate — recognise the
			// existing account and sign them straight back in (saved results kept).
			$returning = true;
			$token = (string) $row['token'];
			$company = $company !== '' ? $company : (string) $row['company'];
			$country = $country !== 'XX' ? $country : (string) $row['country'];
			$db->prepare("UPDATE `epc_free_tool_accounts` SET `company`=?,`country`=?,`time_last_seen`=?,`use_count`=`use_count`+1 WHERE `id`=?")
				->execute(array($company, $country, $now, (int) $row['id']));
		} else {
			$token = bin2hex(random_bytes(24));
			$db->prepare("INSERT INTO `epc_free_tool_accounts` (`token`,`email`,`company`,`country`,`time_created`,`time_last_seen`,`use_count`) VALUES (?,?,?,?,?,?,1)")
				->execute(array($token, $email, $company, $country, $now, $now));
		}
		return array(
			'ok' => true,
			'token' => $token,
			'returning' => $returning,
			'message' => $returning ? 'Welcome back — your free account is already set up; all tools are unlocked.' : 'Account created — all free tools unlocked.',
			'account' => array('email' => $email, 'company' => $company, 'country' => $country),
		);
	}
}

if (!function_exists('epc_free_tools_account_by_token')) {
	function epc_free_tools_account_by_token(string $token): ?array
	{
		$token = preg_replace('/[^a-f0-9]/', '', strtolower($token));
		if ($token === '') {
			return null;
		}
		$db = epc_free_tools_db();
		if ($db === null) {
			return null;
		}
		epc_free_tools_ensure_schema($db);
		$st = $db->prepare("SELECT * FROM `epc_free_tool_accounts` WHERE `token`=? LIMIT 1");
		$st->execute(array($token));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		return $row ?: null;
	}
}

if (!function_exists('epc_free_tools_save')) {
	function epc_free_tools_save(int $accountId, string $tool, string $country, string $title, array $payload): array
	{
		$db = epc_free_tools_db();
		if ($db === null) {
			return array('ok' => false, 'message' => 'Service unavailable.');
		}
		epc_free_tools_ensure_schema($db);
		$db->prepare("INSERT INTO `epc_free_tool_saves` (`account_id`,`tool`,`country`,`title`,`payload`,`time_created`) VALUES (?,?,?,?,?,?)")
			->execute(array($accountId, $tool, $country, $title, json_encode($payload), time()));
		return array('ok' => true, 'id' => (int) $db->lastInsertId());
	}
}

if (!function_exists('epc_free_tools_list_saves')) {
	function epc_free_tools_list_saves(int $accountId, int $limit = 50): array
	{
		$db = epc_free_tools_db();
		if ($db === null) {
			return array();
		}
		epc_free_tools_ensure_schema($db);
		$st = $db->prepare("SELECT `id`,`tool`,`country`,`title`,`time_created` FROM `epc_free_tool_saves` WHERE `account_id`=? ORDER BY `id` DESC LIMIT ?");
		$st->bindValue(1, $accountId, PDO::PARAM_INT);
		$st->bindValue(2, $limit, PDO::PARAM_INT);
		$st->execute();
		return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}
}

/* ------------------------------------------------------------------ *
 *  Page renderer                                                      *
 * ------------------------------------------------------------------ */

if (!function_exists('epc_ecomae_platform_page_free_tools')) {
	function epc_ecomae_platform_page_free_tools(array $params = array())
	{
		$base = function_exists('epc_ecomae_platform_base_url') ? epc_ecomae_platform_base_url() : 'https://www.ecomae.com/';
		$catalog = epc_free_tools_catalog();
		$tool = isset($params['tool']) ? preg_replace('/[^a-z]/', '', (string) $params['tool']) : '';
		ob_start();
		echo epc_free_tools_styles();
		if ($tool !== '' && isset($catalog[$tool])) {
			echo epc_free_tools_render_tool($tool, $catalog[$tool], $base);
		} else {
			echo epc_free_tools_render_hub($catalog, $base);
		}
		echo epc_free_tools_scripts($base);
		return ob_get_clean();
	}
}

if (!function_exists('epc_free_tools_render_hub')) {
	function epc_free_tools_render_hub(array $catalog, string $base): string
	{
		ob_start();
		?>
<div class="epm-wrap eft-wrap">
	<div class="eft-hero">
		<div class="epm-badge"><i class="fa fa-globe"></i> Worldwide &middot; free &middot; no payment</div>
		<h1>Free business tools for your company</h1>
		<p class="lead"><strong>Worldwide tools that work to your registered country's rules.</strong> Run VAT/GST &amp; corporate tax returns, IFRS financials, payroll &amp; gratuity, electronic invoicing (UAE FTA, Saudi ZATCA, EU Peppol&hellip;), customs &amp; landed cost, insurance, document expiry, business valuation, financial models and approval workflows — all free. Every tool auto-localises to your currency, tax rate, e-invoice scheme and labour law from the country you register with.</p>
		<p class="eft-rules"><i class="fa fa-check-circle"></i> <strong>Register once</strong> with your email &mdash; that single free account unlocks <strong>every tool</strong>. One email = one account (you can't register the same email twice; we just sign you back in). Many tools accept a <strong>CSV upload</strong> for instant compliance checks and reports. <i class="fa fa-book"></i> Open any tool to read its <strong>guide</strong> (what it does, what you get and the CSV format) before you register.</p>
	</div>
	<div class="eft-grid">
		<?php foreach ($catalog as $key => $t): ?>
		<a class="eft-card" href="<?php echo epc_free_tools_h($base); ?>platform/free-tools/<?php echo epc_free_tools_h($key); ?>">
			<span class="eft-card__tag"><?php echo epc_free_tools_h($t['tag']); ?></span>
			<i class="fa <?php echo epc_free_tools_h($t['icon']); ?>" aria-hidden="true"></i>
			<h3><?php echo epc_free_tools_h($t['name']); ?></h3>
			<p><?php echo epc_free_tools_h($t['blurb']); ?></p>
			<span class="eft-card__cta">Open tool <i class="fa fa-arrow-right"></i></span>
		</a>
		<?php endforeach; ?>
	</div>
	<div class="eft-upsell">
		<h3>Ready for the full picture?</h3>
		<p>These tools are a taste of the ECOM AE Business Operating System — full ERP, commerce, compliance and CRM, with your data carried across when you upgrade.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_free_tools_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> Start a demo</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_free_tools_h($base); ?>platform/pricing"><i class="fa fa-tags"></i> See pricing</a>
		</div>
	</div>
</div>
		<?php
		return ob_get_clean();
	}
}

if (!function_exists('epc_free_tools_guide')) {
	/**
	 * Pre-registration guide for a tool: what it is, who it's for, what you
	 * get, how it works (country-driven), and the CSV format where relevant.
	 * Lets a visitor understand the tool before registering.
	 *
	 * @return array{what:string,who:string,get:array<int,string>,how:string,csv?:string}
	 */
	function epc_free_tools_guide(string $key): array
	{
		$guides = array(
			'vat' => array(
				'what' => 'Prepare a VAT/GST return for one period. Enter your standard, zero-rated and exempt sales plus recoverable purchases — or upload a sales/purchase ledger CSV — and get the net VAT payable or refundable, filing-ready.',
				'who' => 'Any VAT/GST-registered business that wants to check its return before filing with the tax authority.',
				'get' => array('Output tax, recoverable input tax and net VAT payable/refundable', 'A compliance check that flags missing TRN, rate mismatches and negative lines', 'Downloadable CSV + print/PDF of the return', 'Save the result to your free account'),
				'how' => 'The VAT rate, label (VAT/GST) and currency come automatically from the country you register with — UAE 5%, Saudi 15%, and so on. No manual setup.',
				'csv' => 'type,category,amount,trn — e.g. sale,standard,1000,100123456700003 / purchase,standard,400,',
			),
			'ct' => array(
				'what' => 'Estimate corporate income tax for a year. Enter revenue, deductible expenses and adjustments — or upload a P&L/trial-balance CSV — and get taxable profit and tax after the small-business relief band.',
				'who' => 'Companies that want a quick corporate-tax estimate before preparing the formal return.',
				'get' => array('Taxable profit, relief band applied and tax due', 'Loss / under-threshold handling (no tax when below relief)', 'Compliance notes on adjustments', 'Downloadable CSV + print/PDF, save to your account'),
				'how' => 'The corporate-tax rate and relief threshold are driven by your registered country (e.g. UAE 0% on the first AED 375,000 then 9%).',
				'csv' => 'type,amount — e.g. revenue,1000000 / expense,400000 / adjustment,25000',
			),
			'payroll' => array(
				'what' => 'Calculate monthly net pay and end-of-service gratuity for an employee from basic salary, allowances, deductions and years of service.',
				'who' => 'Employers and HR/payroll staff who need a quick net-pay and gratuity figure.',
				'get' => array('Gross pay, deductions and net pay', 'Statutory end-of-service gratuity for the years served', 'Country currency applied', 'Save / print the calculation'),
				'how' => 'Gratuity rules and currency follow your registered country\'s labour law (e.g. UAE 21/30 days per year banding).',
			),
			'ifrs' => array(
				'what' => 'Build an IFRS-style income statement and balance sheet. Type the figures or upload a trial-balance CSV — the tool maps accounts, computes profit and runs a balance check.',
				'who' => 'Owners, accountants and founders who want quick IFRS-format financial statements.',
				'get' => array('Income statement (revenue → operating profit)', 'Balance sheet with an assets = equity + liabilities check', 'Trial-balance totals and completeness flags', 'Downloadable CSV + print/PDF'),
				'how' => 'Currency comes from your registered country; the IFRS structure is the same worldwide.',
				'csv' => 'account,debit,credit,classification — e.g. Sales,0,500000,revenue / COGS,300000,0,cogs',
			),
			'einvoice' => array(
				'what' => 'Generate a compliant electronic invoice with the correct tax and a network endpoint (Peppol/PINT) for your country. Add line items and get a tax invoice with a UUID.',
				'who' => 'Businesses moving to mandatory e-invoicing (UAE FTA / PINT-AE, Saudi ZATCA/FATOORA, EU Peppol…).',
				'get' => array('Line totals, tax and grand total', 'Country e-invoice scheme, network and endpoint', 'Invoice number + UUID', 'Print/PDF and save'),
				'how' => 'The e-invoice scheme and network are selected automatically from your registered country — UAE PINT-AE, Saudi ZATCA, EU Peppol, with a generic fallback elsewhere.',
			),
			'extreport' => array(
				'what' => 'Produce an external filing pack — the VAT/GST return and the corporate-tax return together — formatted to your country\'s authority and return names.',
				'who' => 'Finance teams preparing the periodic submissions they send to the tax authority.',
				'get' => array('VAT return pack (e.g. UAE VAT 201) and corporate-tax return pack', 'The official filing authority and return names for your country', 'Compliance checks on both packs', 'Downloadable CSV + print/PDF'),
				'how' => 'Authority, scheme and return names resolve from your registered country (UAE → FTA / VAT 201; Saudi → ZATCA).',
				'csv' => 'VAT rows (type,category,amount,trn) and/or CT rows (type,amount) — both are read.',
			),
			'customs' => array(
				'what' => 'Work out landed cost for an import: CIF (goods + freight + insurance), customs duty by HS code, and import VAT. Enter one line or upload a multi-line CSV.',
				'who' => 'Importers, traders and logistics teams estimating duty and total landed cost.',
				'get' => array('CIF value, duty (HS-driven) and import VAT', 'Total payable and landed cost', 'Compliance checks for missing/unknown HS codes and zero-value lines', 'Downloadable CSV + print/PDF'),
				'how' => 'Duty rates, HS overrides and import-VAT rate come from your registered country\'s customs pack (e.g. UAE/Dubai Customs Mirsal 2).',
				'csv' => 'hs_code,qty,unit_value — e.g. 8516,10,250 (freight/insurance taken from the fields above).',
			),
			'insurance' => array(
				'what' => 'Estimate an insurance premium and see the cover that is compulsory or recommended for your country, with a renewal/expiry status.',
				'who' => 'Businesses budgeting cover and tracking when policies must be renewed.',
				'get' => array('Premium from sum insured × rate (or a flat premium)', 'Compulsory / recommended cover for your country', 'Renewal status with an imminent-expiry warning', 'Save / print the summary'),
				'how' => 'The recommended-cover list and currency follow your registered country.',
			),
			'docexpiry' => array(
				'what' => 'Track licences, permits, IDs and contracts by expiry date. Get days-left, a status (valid/expiring/expired) and the next reminder date. Upload many documents via CSV.',
				'who' => 'Operations, HR and admin teams managing trade licences, visas, passports and contracts.',
				'get' => array('Days-left and status per document', 'Next reminder date from your lead-day thresholds', 'Compliance flags for expired and soon-to-expire documents', 'Downloadable CSV + print/PDF'),
				'how' => 'Default reminder lead days follow your country; you can override them per document.',
				'csv' => 'title,expiry,reminder_days — e.g. Trade Licence,2026-03-31,90,60,30',
			),
			'valuation' => array(
				'what' => 'Value your company three ways — discounted cash flow, EBITDA multiple and revenue multiple — and bridge enterprise value to equity value by removing net debt.',
				'who' => 'Founders, buyers/sellers and advisors needing a fast, defensible valuation range.',
				'get' => array('DCF, EBITDA-multiple and revenue-multiple valuations', 'Enterprise value → equity value bridge (EV − net debt)', 'A blended equity value', 'Downloadable CSV + print/PDF'),
				'how' => 'Currency comes from your registered country; you control growth, discount rate and multiples.',
				'csv' => 'Optional P&L CSV: type,amount (revenue / expense) to derive revenue and EBITDA.',
			),
			'finmodel' => array(
				'what' => 'Project a multi-year P&L — revenue, gross profit, EBITDA and net profit — from a base revenue, growth rate and margin assumptions, then export it.',
				'who' => 'Anyone building a business plan, budget or fundraising model.',
				'get' => array('Year-by-year revenue, gross profit, EBITDA and net profit', 'Up to 10 years of projection', 'Downloadable CSV of the full model', 'Save / print'),
				'how' => 'Currency is set by your registered country; all drivers (growth, margins, tax) are yours to set.',
				'csv' => 'Optional P&L CSV: type,amount (revenue) to set the base-year revenue.',
			),
			'taxkit' => array(
				'what' => 'A one-screen tax snapshot for your country: VAT/GST rate, corporate-tax rate and relief threshold, e-invoice scheme and network, filing authority, return names and fiscal-year start.',
				'who' => 'Anyone wanting a quick reference of the tax and compliance landscape for a country.',
				'get' => array('11 key tax & compliance facts at a glance', 'E-invoice scheme + network for the country', 'Authority and VAT/CT return names', 'Save / print the snapshot'),
				'how' => 'Reads your registered country automatically; use “change” to preview another country for comparison.',
			),
			'hrcompliance' => array(
				'what' => 'A country labour-law card — end-of-service, leave, notice, probation, working hours, wage protection and the official authority link — plus an optional per-employee compliance check and end-of-service liability.',
				'who' => 'Employers and HR teams that need to stay compliant with local labour law worldwide.',
				'get' => array('Statutory labour-law card for your country', 'Official government authority link', 'Optional per-employee compliance check + end-of-service liability', 'Save / print'),
				'how' => 'Uses the same labour-law engine as the full ERP, driven by your registered country (UAE → MOHRE/WPS, UK → GOV.UK, Saudi → HRSD).',
			),
			'workflow' => array(
				'what' => 'Design a spend-approval matrix — who approves what at each value tier — with SLA targets per step.',
				'who' => 'Finance and operations teams setting up purchase/expense approval rules.',
				'get' => array('A tiered approval chain with limits', 'Approver and SLA per tier', 'Country currency on the limits', 'Save / print the matrix'),
				'how' => 'Currency on the limits follows your registered country.',
			),
		);
		return $guides[$key] ?? array('what' => '', 'who' => '', 'get' => array(), 'how' => '');
	}
}

if (!function_exists('epc_free_tools_guide_html')) {
	/** Render the pre-registration guide card for a tool. */
	function epc_free_tools_guide_html(string $key, array $meta): string
	{
		$g = epc_free_tools_guide($key);
		if (($g['what'] ?? '') === '') {
			return '';
		}
		$h = '<div class="eft-guide" id="eft_guide">';
		$h .= '<h3><i class="fa fa-book"></i> Guide: ' . epc_free_tools_h($meta['name']) . '</h3>';
		$h .= '<div class="eft-guide__grid">';
		$h .= '<div class="eft-guide__col"><h4>What it does</h4><p>' . epc_free_tools_h($g['what']) . '</p>';
		if (!empty($g['who'])) {
			$h .= '<h4>Who it\'s for</h4><p>' . epc_free_tools_h($g['who']) . '</p>';
		}
		$h .= '</div>';
		$h .= '<div class="eft-guide__col"><h4>What you get</h4><ul class="eft-guide__list">';
		foreach ($g['get'] as $item) {
			$h .= '<li><i class="fa fa-check"></i> ' . epc_free_tools_h($item) . '</li>';
		}
		$h .= '</ul>';
		$h .= '<h4>How it works</h4><p class="eft-guide__how"><i class="fa fa-globe"></i> ' . epc_free_tools_h($g['how']) . '</p>';
		if (!empty($g['csv'])) {
			$h .= '<h4>CSV format</h4><p class="eft-guide__csv"><code>' . epc_free_tools_h($g['csv']) . '</code></p>';
		}
		$h .= '</div></div></div>';
		return $h;
	}
}

if (!function_exists('epc_free_tools_render_tool')) {
	function epc_free_tools_render_tool(string $key, array $meta, string $base): string
	{
		$countries = epc_free_tools_countries();
		ob_start();
		?>
<div class="epm-wrap eft-wrap" data-eft-tool="<?php echo epc_free_tools_h($key); ?>">
	<a class="eft-back" href="<?php echo epc_free_tools_h($base); ?>platform/free-tools"><i class="fa fa-arrow-left"></i> All free tools</a>
	<div class="eft-tool-head">
		<i class="fa <?php echo epc_free_tools_h($meta['icon']); ?>" aria-hidden="true"></i>
		<div>
			<h1><?php echo epc_free_tools_h($meta['name']); ?></h1>
			<p><?php echo epc_free_tools_h($meta['blurb']); ?></p>
		</div>
	</div>

	<div class="eft-gate" id="eft_gate">
		<h3><i class="fa fa-unlock-alt"></i> Register free to use this tool</h3>
		<p>Enter your details once — it's free, no card. We localise the tool to your country and let you save results.</p>
		<div class="eft-form-row">
			<label>Work email<input type="email" id="eft_email" placeholder="you@company.com" autocomplete="email" /></label>
			<label>Company<input type="text" id="eft_company" placeholder="Your company name" autocomplete="organization" /></label>
			<label>Country
				<select id="eft_country">
					<?php foreach ($countries as $cc => $cn): ?>
					<option value="<?php echo epc_free_tools_h($cc); ?>"<?php echo $cc === 'AE' ? ' selected' : ''; ?>><?php echo epc_free_tools_h($cn); ?></option>
					<?php endforeach; ?>
				</select>
			</label>
		</div>
		<button type="button" class="epm-btn epm-btn--primary" id="eft_register"><i class="fa fa-check"></i> Start using free</button>
		<p class="eft-msg" id="eft_gate_msg"></p>
	</div>

	<?php echo epc_free_tools_guide_html($key, $meta); ?>

	<div class="eft-app" id="eft_app" hidden>
		<div class="eft-app__bar">
			<span id="eft_who"></span>
			<span class="eft-country-pill">Country: <strong id="eft_country_label"></strong> · <button type="button" class="eft-link" id="eft_change_country">change</button></span>
		</div>
		<div class="eft-split">
			<form class="eft-inputs" id="eft_form" onsubmit="return false;">
				<?php echo epc_free_tools_fields_html($key); ?>
				<button type="button" class="epm-btn epm-btn--primary" id="eft_compute"><i class="fa fa-calculator"></i> Calculate</button>
			</form>
			<div class="eft-result" id="eft_result">
				<div class="eft-result__empty">Fill in the form and press <strong>Calculate</strong>.</div>
			</div>
		</div>
	</div>
</div>
		<?php
		return ob_get_clean();
	}
}

if (!function_exists('epc_free_tools_fields_html')) {
	/** Per-tool input fields. */
	function epc_free_tools_fields_html(string $key): string
	{
		$num = function ($id, $label, $val = '') {
			return '<label>' . epc_free_tools_h($label) . '<input type="number" step="0.01" data-eft-field="' . epc_free_tools_h($id) . '" value="' . epc_free_tools_h($val) . '" /></label>';
		};
		$txt = function ($id, $label, $val = '') {
			return '<label>' . epc_free_tools_h($label) . '<input type="text" data-eft-field="' . epc_free_tools_h($id) . '" value="' . epc_free_tools_h($val) . '" /></label>';
		};
		$csv = function ($hint) {
			return '<div class="eft-fieldset">Or upload a CSV</div>'
				. '<label class="eft-csv">Upload CSV file'
				. '<input type="file" accept=".csv,text/csv" data-eft-csv="1" /></label>'
				. '<label>CSV data (auto-fills when you choose a file; or paste here)'
				. '<textarea rows="5" data-eft-field="csv" placeholder="' . epc_free_tools_h($hint) . '"></textarea></label>'
				. '<p class="eft-hint"><i class="fa fa-info-circle"></i> ' . epc_free_tools_h($hint) . '</p>';
		};
		switch ($key) {
			case 'vat':
				return $num('standard_sales', 'Standard-rated sales (ex-tax)')
					. $num('zero_sales', 'Zero-rated sales')
					. $num('exempt_sales', 'Exempt sales')
					. $num('standard_purchases', 'Standard-rated purchases (ex-tax)')
					. $num('import_vat', 'Import tax paid')
					. $csv('Header: type,category,amount,trn — e.g. sale,standard,1000,100123456700003');
			case 'ct':
				return $num('revenue', 'Revenue / turnover')
					. $num('expenses', 'Deductible expenses')
					. $num('adjustments', 'Tax adjustments (+/-)')
					. $csv('Header: type,amount — e.g. revenue,500000 / expense,300000 / adjustment,20000');
			case 'payroll':
				return $num('basic', 'Basic salary (monthly)')
					. $num('allowances', 'Allowances (monthly)')
					. $num('deductions', 'Deductions (monthly)')
					. $num('years', 'Years of service (for gratuity)');
			case 'ifrs':
				return '<div class="eft-fieldset">Income statement</div>'
					. $num('revenue', 'Revenue')
					. $num('cogs', 'Cost of sales')
					. $num('opex', 'Operating expenses')
					. $num('other_income', 'Other income')
					. '<div class="eft-fieldset">Balance sheet</div>'
					. $num('non_current_assets', 'Non-current assets')
					. $num('current_assets', 'Current assets')
					. $num('equity', 'Equity')
					. $num('non_current_liabilities', 'Non-current liabilities')
					. $num('current_liabilities', 'Current liabilities')
					. $csv('Header: account,debit,credit,classification — e.g. Sales,0,500000,revenue');
			case 'einvoice':
				return $txt('number', 'Invoice number (blank = auto)')
					. $txt('seller', 'Seller / your company')
					. $txt('seller_trn', 'Seller tax reg. no.')
					. $txt('buyer', 'Buyer / customer')
					. $txt('buyer_trn', 'Buyer tax reg. no.')
					. '<div class="eft-fieldset">Lines</div>'
					. '<div id="eft_lines">'
					. epc_free_tools_einvoice_line_html()
					. epc_free_tools_einvoice_line_html()
					. '</div>'
					. '<button type="button" class="eft-link" id="eft_add_line"><i class="fa fa-plus"></i> Add line</button>';
			case 'extreport':
				return '<div class="eft-fieldset">VAT / GST return</div>'
					. $num('standard_sales', 'Standard-rated sales (ex-tax)')
					. $num('zero_sales', 'Zero-rated sales')
					. $num('exempt_sales', 'Exempt sales')
					. $num('standard_purchases', 'Standard-rated purchases (ex-tax)')
					. $num('import_vat', 'Import tax paid')
					. '<div class="eft-fieldset">Corporate tax return</div>'
					. $num('revenue', 'Revenue / turnover')
					. $num('expenses', 'Deductible expenses')
					. $num('adjustments', 'Tax adjustments (+/-)')
					. $csv('VAT rows (type,category,amount,trn) or CT rows (type,amount) — both are read.');
			case 'customs':
				return $txt('hs_code', 'HS code (single line)')
					. $num('qty', 'Quantity', '1')
					. $num('unit_value', 'Unit value (FOB)')
					. $num('freight', 'Freight')
					. $num('insurance', 'Insurance')
					. $num('other', 'Other landed costs (clearing, handling)')
					. $num('fx_rate', 'FX rate to ' . epc_free_tools_h('local currency'), '1')
					. $csv('Header: hs_code,qty,unit_value — e.g. 8516,10,250 (freight/insurance from the fields above).');
			case 'insurance':
				return $num('sum_insured', 'Sum insured')
					. $num('rate', 'Premium rate (%) — optional')
					. $num('premium', 'Annual premium — optional (overrides rate)')
					. $txt('expiry', 'Policy expiry date (YYYY-MM-DD)');
			case 'docexpiry':
				return $txt('title', 'Document title (single)')
					. $txt('expiry', 'Expiry date (YYYY-MM-DD)')
					. $txt('reminder_days', 'Reminder lead days', '90,60,30,7')
					. $csv('Header: title,expiry,reminder_days — e.g. Trade Licence,2026-03-31,90,60,30');
			case 'valuation':
				return $num('revenue', 'Annual revenue')
					. $num('ebitda', 'EBITDA')
					. $num('net_debt', 'Net debt (cash negative)')
					. $num('growth', 'Long-term growth %', '5')
					. $num('discount', 'Discount rate / WACC %', '15')
					. $num('ebitda_multiple', 'EBITDA multiple (x)', '6')
					. $num('revenue_multiple', 'Revenue multiple (x)', '1.5')
					. $num('tax_rate', 'Tax rate % (for DCF)', '0')
					. $csv('Optional P&L CSV: type,amount (revenue / expense) to derive revenue & EBITDA.');
			case 'finmodel':
				return $num('revenue', 'Base-year revenue')
					. $num('growth', 'Annual growth %', '10')
					. $num('gross_margin', 'Gross margin %', '40')
					. $num('opex_pct', 'Opex as % of revenue', '25')
					. $num('tax_rate', 'Tax rate %', '0')
					. $num('years', 'Years to project (1-10)', '5')
					. $csv('Optional P&L CSV: type,amount (revenue) to set base-year revenue.');
			case 'taxkit':
				return '<p class="eft-hint"><i class="fa fa-info-circle"></i> This kit reads your registered country automatically. Use “change” above to preview another country.</p>';
			case 'hrcompliance':
				return '<p class="eft-hint"><i class="fa fa-info-circle"></i> The labour-law card uses your registered country. Add an employee below for an optional compliance check + end-of-service liability.</p>'
					. '<div class="eft-fieldset">Employee check (optional)</div>'
					. $num('basic_salary', 'Basic salary (monthly)')
					. $txt('hire_date', 'Hire date (YYYY-MM-DD)')
					. $num('leave_balance', 'Leave balance (days)');
			case 'workflow':
				return $txt('approver0', 'Tier 1 approver', 'Line manager')
					. $txt('approver1', 'Tier 2 approver', 'Department head')
					. $txt('approver2', 'Tier 3 approver', 'Finance director')
					. $num('tier1', 'Tier 1 limit', '5000')
					. $num('tier2', 'Tier 2 limit', '50000');
		}
		return '';
	}
}

if (!function_exists('epc_free_tools_einvoice_line_html')) {
	function epc_free_tools_einvoice_line_html(): string
	{
		return '<div class="eft-line">'
			. '<input type="text" class="eft-ln-desc" placeholder="Description" />'
			. '<input type="number" step="0.01" class="eft-ln-qty" placeholder="Qty" />'
			. '<input type="number" step="0.01" class="eft-ln-price" placeholder="Unit price" />'
			. '</div>';
	}
}

if (!function_exists('epc_free_tools_styles')) {
	function epc_free_tools_styles(): string
	{
		return '<style>
.eft-wrap{padding-top:24px}
.eft-hero{max-width:820px;margin:0 auto 28px;text-align:center}
.eft-hero h1{font-family:Syne,sans-serif;font-size:2.1rem;margin:10px 0}
.eft-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:18px}
.eft-card{display:flex;flex-direction:column;gap:8px;padding:20px;border:1px solid rgba(148,163,184,.25);border-radius:14px;background:linear-gradient(160deg,rgba(15,23,42,.6),rgba(30,41,59,.4));text-decoration:none;color:inherit;transition:transform .15s,border-color .15s}
.eft-card:hover{transform:translateY(-3px);border-color:var(--epm-cyan,#22d3ee)}
.eft-card>i{font-size:26px;color:var(--epm-cyan,#22d3ee)}
.eft-card h3{margin:2px 0;font-size:1.15rem}
.eft-card p{color:var(--epm-muted,#94a3b8);font-size:.92rem;margin:0;flex:1}
.eft-card__tag{align-self:flex-start;font-size:.7rem;text-transform:uppercase;letter-spacing:.06em;padding:3px 8px;border-radius:999px;background:rgba(34,211,238,.14);color:var(--epm-cyan,#22d3ee)}
.eft-card__cta{font-weight:700;color:var(--epm-cyan,#22d3ee);font-size:.9rem}
.eft-upsell{margin:34px auto 0;max-width:760px;text-align:center;padding:24px;border:1px dashed rgba(148,163,184,.3);border-radius:14px}
.eft-back{display:inline-block;margin-bottom:14px;color:var(--epm-muted,#94a3b8);text-decoration:none;font-weight:600;font-size:.9rem}
.eft-tool-head{display:flex;gap:16px;align-items:flex-start;margin-bottom:18px}
.eft-tool-head>i{font-size:30px;color:var(--epm-cyan,#22d3ee);margin-top:6px}
.eft-tool-head h1{font-family:Syne,sans-serif;margin:0 0 4px;font-size:1.7rem}
.eft-tool-head p{color:var(--epm-muted,#94a3b8);margin:0;max-width:640px}
.eft-gate,.eft-app{border:1px solid rgba(148,163,184,.25);border-radius:14px;padding:22px;background:rgba(15,23,42,.5)}
.eft-form-row{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px;margin:12px 0}
.eft-wrap label{display:flex;flex-direction:column;gap:5px;font-size:.85rem;font-weight:600;color:var(--epm-muted,#94a3b8)}
.eft-wrap input,.eft-wrap select{padding:9px 11px;border-radius:9px;border:1px solid rgba(148,163,184,.3);background:rgba(2,6,23,.6);color:#e2e8f0;font:inherit}
.eft-split{display:grid;grid-template-columns:1fr 1fr;gap:20px}
.eft-inputs{display:flex;flex-direction:column;gap:12px}
.eft-fieldset{font-weight:800;color:#e2e8f0;margin-top:6px;border-bottom:1px solid rgba(148,163,184,.2);padding-bottom:4px}
.eft-line{display:grid;grid-template-columns:2fr 1fr 1fr;gap:8px;margin-bottom:8px}
.eft-result{border:1px solid rgba(148,163,184,.2);border-radius:12px;padding:18px;min-height:160px;background:rgba(2,6,23,.4)}
.eft-result__empty{color:var(--epm-muted,#94a3b8);text-align:center;padding:40px 0}
.eft-result table{width:100%;border-collapse:collapse;font-size:.92rem}
.eft-result td{padding:7px 4px;border-bottom:1px solid rgba(148,163,184,.14)}
.eft-result td:last-child{text-align:right;font-variant-numeric:tabular-nums}
.eft-result .eft-total{font-weight:800;font-size:1.05rem;color:var(--epm-cyan,#22d3ee)}
.eft-app__bar{display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px;margin-bottom:14px;font-size:.85rem;color:var(--epm-muted,#94a3b8)}
.eft-link{background:transparent;border:0;color:var(--epm-cyan,#22d3ee);cursor:pointer;font:inherit;font-weight:600;padding:0}
.eft-msg{font-size:.85rem;margin-top:8px;min-height:18px}
.eft-msg.is-err{color:#f87171}.eft-msg.is-ok{color:#34d399}
.eft-result__actions{display:flex;gap:8px;margin-top:14px;flex-wrap:wrap}
.eft-result__actions button{font-size:.82rem;padding:7px 12px}
.eft-rules{max-width:740px;margin:8px auto 0;font-size:.9rem;color:var(--epm-muted,#94a3b8);background:rgba(34,211,238,.06);border:1px solid rgba(34,211,238,.18);border-radius:10px;padding:10px 14px}
.eft-rules i{color:var(--epm-cyan,#22d3ee)}
.eft-hint{font-size:.8rem;color:var(--epm-muted,#94a3b8);margin:2px 0 0;font-weight:500}
.eft-wrap textarea{padding:9px 11px;border-radius:9px;border:1px solid rgba(148,163,184,.3);background:rgba(2,6,23,.6);color:#e2e8f0;font:inherit;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:.82rem;resize:vertical}
.eft-csv input[type=file]{padding:7px;background:rgba(2,6,23,.4)}
.eft-find{list-style:none;padding:0;margin:6px 0 0}
.eft-find li{display:flex;gap:8px;align-items:flex-start;padding:6px 8px;border-radius:8px;margin-bottom:5px;font-size:.86rem;font-weight:500}
.eft-find li.is-pass{background:rgba(52,211,153,.1);color:#bbf7d0}
.eft-find li.is-warn{background:rgba(251,191,36,.1);color:#fde68a}
.eft-find li.is-fail{background:rgba(248,113,113,.12);color:#fecaca}
.eft-find li i{margin-top:2px}
.eft-result h4{margin:14px 0 4px;font-size:.95rem;color:#e2e8f0}
.eft-result a{color:var(--epm-cyan,#22d3ee)}
.eft-prev-gallery{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:14px;margin:8px 0 4px}
.eft-prev-gallery figure{margin:0;border:1px solid rgba(148,163,184,.25);border-radius:12px;overflow:hidden;background:rgba(2,6,23,.4)}
.eft-prev-gallery img{width:100%;display:block;height:150px;object-fit:cover;object-position:top}
.eft-prev-gallery figcaption{padding:8px 10px;font-size:.8rem;color:var(--epm-muted,#94a3b8)}
.eft-prev{margin:6px 0 18px;border:1px solid rgba(148,163,184,.25);border-radius:12px;overflow:hidden;max-width:520px}
.eft-prev img{width:100%;display:block}
.eft-prev figcaption{padding:8px 12px;font-size:.82rem;color:var(--epm-muted,#94a3b8);background:rgba(2,6,23,.4)}
.eft-guide{margin:18px 0;padding:18px 20px;border:1px solid rgba(148,163,184,.22);border-radius:14px;background:linear-gradient(180deg,rgba(15,23,42,.5),rgba(2,6,23,.3))}
.eft-guide>h3{margin:0 0 12px;font-size:1.05rem;color:#e2e8f0;display:flex;align-items:center;gap:8px}
.eft-guide>h3 i{color:var(--epm-cyan,#22d3ee)}
.eft-guide__grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}
.eft-guide__col h4{margin:12px 0 4px;font-size:.82rem;text-transform:uppercase;letter-spacing:.04em;color:var(--epm-cyan,#22d3ee)}
.eft-guide__col h4:first-child{margin-top:0}
.eft-guide__col p{margin:0;font-size:.9rem;line-height:1.55;color:#cbd5e1}
.eft-guide__list{list-style:none;padding:0;margin:0}
.eft-guide__list li{font-size:.9rem;color:#cbd5e1;padding:3px 0;display:flex;gap:8px;align-items:flex-start}
.eft-guide__list li i{color:#34d399;margin-top:3px}
.eft-guide__how i{color:var(--epm-cyan,#22d3ee)}
.eft-guide__csv code{display:block;font-size:.8rem;background:rgba(2,6,23,.6);border:1px solid rgba(148,163,184,.2);border-radius:8px;padding:8px 10px;color:#a5f3fc;overflow-x:auto}
@media(max-width:760px){.eft-split{grid-template-columns:1fr}.eft-guide__grid{grid-template-columns:1fr;gap:8px}}
</style>';
	}
}

if (!function_exists('epc_free_tools_scripts')) {
	function epc_free_tools_scripts(string $base): string
	{
		$endpoint = $base . 'content/general_pages/ajax_epc_free_tools.php';
		$js = <<<'JS'
(function(){
	var wrap=document.querySelector('[data-eft-tool]');
	if(!wrap)return;
	var tool=wrap.getAttribute('data-eft-tool');
	var ENDPOINT=__ENDPOINT__;
	var tokenKey='eft_token';
	function post(action,data){
		data.action=action;
		return fetch(ENDPOINT,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)}).then(function(r){return r.json();});
	}
	function el(id){return document.getElementById(id);}
	function showApp(acc){
		el('eft_gate').hidden=true;el('eft_app').hidden=false;
		el('eft_who').textContent=acc.company?('Signed in: '+acc.company+' ('+acc.email+')'):('Signed in: '+acc.email);
		el('eft_country_label').textContent=acc.country;
		wrap.setAttribute('data-eft-country',acc.country);
	}
	var saved=localStorage.getItem(tokenKey);
	if(saved){
		post('whoami',{token:saved}).then(function(r){if(r&&r.ok){showApp(r.account);}});
	}
	var reg=el('eft_register');
	if(reg)reg.addEventListener('click',function(){
		var msg=el('eft_gate_msg');msg.className='eft-msg';msg.textContent='Working…';
		post('register',{email:el('eft_email').value,company:el('eft_company').value,country:el('eft_country').value}).then(function(r){
			if(r&&r.ok){localStorage.setItem(tokenKey,r.token);msg.className='eft-msg is-ok';msg.textContent=r.message||'';showApp(r.account);}
			else{msg.className='eft-msg is-err';msg.textContent=(r&&r.message)||'Could not register.';}
		}).catch(function(){msg.className='eft-msg is-err';msg.textContent='Network error.';});
	});
	var chg=el('eft_change_country');
	if(chg)chg.addEventListener('click',function(){el('eft_app').hidden=true;el('eft_gate').hidden=false;});
	var addLine=el('eft_add_line');
	if(addLine)addLine.addEventListener('click',function(){
		var d=document.createElement('div');d.className='eft-line';
		d.innerHTML='<input type="text" class="eft-ln-desc" placeholder="Description" /><input type="number" step="0.01" class="eft-ln-qty" placeholder="Qty" /><input type="number" step="0.01" class="eft-ln-price" placeholder="Unit price" />';
		el('eft_lines').appendChild(d);
	});
	// CSV file -> textarea (so the same field is posted whether typed or uploaded).
	wrap.querySelectorAll('[data-eft-csv]').forEach(function(fi){
		fi.addEventListener('change',function(){
			var f=fi.files&&fi.files[0];if(!f)return;
			var rd=new FileReader();
			rd.onload=function(){
				var ta=wrap.querySelector('textarea[data-eft-field="csv"]');
				if(ta){ta.value=String(rd.result||'');}
			};
			rd.readAsText(f);
		});
	});
	function csvEscape(v){v=String(v==null?'':v);return /[",\n]/.test(v)?('"'+v.replace(/"/g,'""')+'"'):v;}
	function download(name,text){
		var blob=new Blob([text],{type:'text/csv'});
		var a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=name;
		document.body.appendChild(a);a.click();document.body.removeChild(a);
	}
	function resultToCsv(r){
		var lines=[];
		function kv(rows){rows.forEach(function(row){lines.push(csvEscape(row[0])+','+csvEscape(row[1]));});}
		if(r.rows)kv(r.rows);
		if(r.rows_text)kv(r.rows_text);
		if(r.income){lines.push('Income statement');kv(r.income);}
		if(r.balance){lines.push('Balance sheet');kv(r.balance);}
		if(r.projection){lines.push('Year,Revenue,Gross profit,EBITDA,Net profit');r.projection.forEach(function(p){lines.push([p.year,p.revenue,p.gross_profit,p.ebitda,p.net_profit].map(csvEscape).join(','));});}
		if(r.doc_rows){lines.push('Document,Expiry,Days left,Status,Next reminder');r.doc_rows.forEach(function(d){lines.push([d.title,d.expiry,d.days_left,d.status,d.next_reminder].map(csvEscape).join(','));});}
		if(r.recommended){lines.push('Cover,Basis');r.recommended.forEach(function(x){lines.push(csvEscape(x.class)+','+csvEscape(x.basis));});}
		if(r.compliance){lines.push('');lines.push('Compliance');r.compliance.forEach(function(f){lines.push(csvEscape(f.level)+','+csvEscape(f.message));});}
		if(typeof r.net!=='undefined')lines.push(csvEscape(r.net_label||'Total')+','+csvEscape(r.net));
		return lines.join('\n');
	}
	function collect(){
		var data={};
		wrap.querySelectorAll('[data-eft-field]').forEach(function(i){data[i.getAttribute('data-eft-field')]=i.value;});
		if(tool==='einvoice'){
			var lines=[];
			wrap.querySelectorAll('.eft-line').forEach(function(l){
				lines.push({desc:l.querySelector('.eft-ln-desc').value,qty:l.querySelector('.eft-ln-qty').value,price:l.querySelector('.eft-ln-price').value});
			});
			data.lines=lines;
		}
		return data;
	}
	function money(cur,v){return cur+' '+Number(v).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2});}
	function render(r){
		var box=el('eft_result');
		if(!r||!r.ok){box.innerHTML='<div class="eft-result__empty">'+((r&&r.message)||'Could not calculate.')+'</div>';return;}
		var cur=r.currency||'';var h='';
		function esc(s){return String(s==null?'':s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
		function cell(v){return (typeof v==='number')?money(cur,v):esc(v);}
		function kvtable(rows){var t='<table>';rows.forEach(function(row){t+='<tr><td>'+esc(row[0])+'</td><td>'+cell(row[1])+'</td></tr>';});return t+'</table>';}
		function findings(list){if(!list||!list.length)return '';var t='<h4>Compliance check</h4><ul class="eft-find">';list.forEach(function(f){var ic=f.level==='pass'?'fa-check-circle':(f.level==='fail'?'fa-times-circle':'fa-exclamation-triangle');t+='<li class="is-'+esc(f.level)+'"><i class="fa '+ic+'"></i><span>'+esc(f.message)+'</span></li>';});return t+'</ul>';}
		// External Reporting: nested VAT + CT packs.
		if(r.vat||r.ct){
			h+='<p><small>Filing authority: <strong>'+esc(r.authority)+'</strong></small></p>';
			if(r.vat){h+='<h4>'+esc(r.vat_return_name)+'</h4>'+kvtable(r.vat.rows||[]);}
			if(r.ct){h+='<h4>'+esc(r.ct_return_name)+'</h4>'+kvtable(r.ct.rows||[]);}
		} else {
			if(r.rows)h+=kvtable(r.rows);
			if(r.rows_text)h+=kvtable(r.rows_text);
		}
		if(r.recommended&&r.recommended.length){h+='<h4>Compulsory / recommended cover</h4><table><tr><td><b>Cover</b></td><td><b>Basis</b></td></tr>';r.recommended.forEach(function(x){h+='<tr><td>'+esc(x.class)+'</td><td>'+esc(x.basis)+'</td></tr>';});h+='</table>';}
		if(r.emp_rows&&r.emp_rows.length){h+='<h4>Employee check</h4>'+kvtable(r.emp_rows);}
		if(r.doc_rows){h+='<h4>Documents</h4><table><tr><td><b>Document</b></td><td><b>Expiry</b></td><td><b>Days</b></td><td><b>Status</b></td><td><b>Next reminder</b></td></tr>';r.doc_rows.forEach(function(d){h+='<tr><td>'+esc(d.title)+'</td><td>'+esc(d.expiry)+'</td><td>'+esc(d.days_left)+'</td><td>'+esc(d.status)+'</td><td>'+esc(d.next_reminder)+'</td></tr>';});h+='</table>';}
		if(r.projection){h+='<h4>Projection</h4><table><tr><td><b>Year</b></td><td><b>Revenue</b></td><td><b>Gross</b></td><td><b>EBITDA</b></td><td><b>Net</b></td></tr>';r.projection.forEach(function(p){h+='<tr><td>'+esc(p.year)+'</td><td>'+money(cur,p.revenue)+'</td><td>'+money(cur,p.gross_profit)+'</td><td>'+money(cur,p.ebitda)+'</td><td>'+money(cur,p.net_profit)+'</td></tr>';});h+='</table>';}
		if(r.income){h+='<h4>Income statement</h4>'+kvtable(r.income);}
		if(r.balance){h+='<h4>Balance sheet</h4>'+kvtable(r.balance);}
		if(r.steps){h+='<table><tr><td><b>Spend range</b></td><td><b>Approver(s)</b></td></tr>';r.steps.forEach(function(s){h+='<tr><td>'+esc(s.range)+'</td><td>'+esc(s.approver)+' <small>('+esc(s.sla)+')</small></td></tr>';});h+='</table>';}
		if(r.lines&&(tool==='einvoice')){h+='<table><tr><td><b>Item</b></td><td><b>Amount</b></td></tr>';r.lines.forEach(function(l){h+='<tr><td>'+esc(l.desc||'')+' ×'+esc(l.qty)+'</td><td>'+money(cur,l.amount)+'</td></tr>';});h+='</table>';h+='<table><tr><td>Subtotal</td><td>'+money(cur,r.subtotal)+'</td></tr><tr><td>'+esc(r.label)+' ('+esc(r.rate)+'%)</td><td>'+money(cur,r.tax)+'</td></tr></table>';h+='<p><small>Invoice '+esc(r.number)+' · '+esc(r.date)+' · '+esc(r.scheme)+'<br/>Network: '+esc(r.network||'')+(r.endpoint?(' · Endpoint '+esc(r.endpoint)):'')+'<br/>UUID '+esc(r.uuid)+'</small></p>';}
		if(typeof r.net!=='undefined'&&!r.steps){h+='<table><tr class="eft-total"><td>'+esc(r.net_label||'Total')+'</td><td>'+((tool==='workflow'||tool==='taxkit'||tool==='docexpiry')?esc(r.net):money(cur,r.net))+'</td></tr></table>';}
		if(r.net_label&&r.steps){h+='<p class="eft-total">'+esc(r.net)+' '+esc(r.net_label)+'</p>';}
		if(r.authority_url){h+='<p><small><i class="fa fa-external-link"></i> Official authority: <a href="'+esc(r.authority_url)+'" target="_blank" rel="noopener">'+esc(r.authority_url)+'</a></small></p>';}
		if(r.note){h+='<p><small>'+esc(r.note)+'</small></p>';}
		h+=findings(r.compliance);
		h+='<div class="eft-result__actions"><button type="button" class="epm-btn epm-btn--ghost" id="eft_save"><i class="fa fa-save"></i> Save</button><button type="button" class="epm-btn epm-btn--ghost" id="eft_csv"><i class="fa fa-download"></i> Download CSV</button><button type="button" class="epm-btn epm-btn--ghost" onclick="window.print()"><i class="fa fa-print"></i> Print / PDF</button></div><p class="eft-msg" id="eft_save_msg"></p>';
		box.innerHTML=h;
		var dl=el('eft_csv');
		if(dl)dl.addEventListener('click',function(){download(tool+'-'+new Date().toISOString().slice(0,10)+'.csv',resultToCsv(r));});
		var sv=el('eft_save');
		if(sv)sv.addEventListener('click',function(){
			var m=el('eft_save_msg');m.className='eft-msg';m.textContent='Saving…';
			post('save',{token:localStorage.getItem(tokenKey),tool:tool,country:wrap.getAttribute('data-eft-country'),title:tool+' '+new Date().toISOString().slice(0,10),payload:r}).then(function(x){
				m.className='eft-msg '+(x&&x.ok?'is-ok':'is-err');m.textContent=(x&&x.ok)?'Saved to your free account.':((x&&x.message)||'Could not save.');
			});
		});
	}
	var comp=el('eft_compute');
	if(comp)comp.addEventListener('click',function(){
		post('compute',{token:localStorage.getItem(tokenKey),tool:tool,country:wrap.getAttribute('data-eft-country'),inputs:collect()}).then(render).catch(function(){el('eft_result').innerHTML='<div class="eft-result__empty">Network error.</div>';});
	});
})();
JS;
		$js = str_replace('__ENDPOINT__', json_encode($endpoint), $js);
		return '<script>' . $js . '</script>';
	}
}
