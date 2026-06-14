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
				'name' => 'E-Invoice Generator',
				'icon' => 'fa-qrcode',
				'tag' => 'Compliance',
				'blurb' => 'Create a compliant tax invoice with your country VAT and e-invoice scheme reference.',
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
			case 'workflow':
				return epc_free_tools_compute_workflow($prof, $in);
		}
		return array('ok' => false, 'message' => 'Unknown tool');
	}
}

if (!function_exists('epc_free_tools_compute_vat')) {
	function epc_free_tools_compute_vat(array $prof, array $in): array
	{
		$rate = (float) $prof['tax_rate'];
		$standardSales = (float) ($in['standard_sales'] ?? 0);
		$zeroSales = (float) ($in['zero_sales'] ?? 0);
		$exemptSales = (float) ($in['exempt_sales'] ?? 0);
		$standardPurch = (float) ($in['standard_purchases'] ?? 0);
		$importVat = (float) ($in['import_vat'] ?? 0);
		$outputTax = $standardSales * $rate / 100.0;
		$inputTax = $standardPurch * $rate / 100.0 + $importVat;
		$net = $outputTax - $inputTax;
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
		);
	}
}

if (!function_exists('epc_free_tools_compute_ct')) {
	function epc_free_tools_compute_ct(array $prof, array $in): array
	{
		$cit = epc_free_tools_cit_rate($prof['country']);
		$revenue = (float) ($in['revenue'] ?? 0);
		$expenses = (float) ($in['expenses'] ?? 0);
		$adjustments = (float) ($in['adjustments'] ?? 0);
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

if (!function_exists('epc_free_tools_compute_ifrs')) {
	function epc_free_tools_compute_ifrs(array $prof, array $in): array
	{
		$g = function ($k) use ($in) { return (float) ($in[$k] ?? 0); };
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
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
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
		return array(
			'ok' => true,
			'currency' => $prof['currency'],
			'rate' => $rate,
			'label' => $prof['tax_label'],
			'scheme' => $prof['einvoice'] !== '' ? $prof['einvoice'] : 'No mandated e-invoice scheme for this country',
			'number' => $number,
			'uuid' => $uuid,
			'seller' => trim((string) ($in['seller'] ?? '')),
			'seller_trn' => trim((string) ($in['seller_trn'] ?? '')),
			'buyer' => trim((string) ($in['buyer'] ?? '')),
			'buyer_trn' => trim((string) ($in['buyer_trn'] ?? '')),
			'date' => date('Y-m-d'),
			'lines' => $rows,
			'subtotal' => epc_free_tools_round($subtotal),
			'tax' => epc_free_tools_round($tax),
			'net' => epc_free_tools_round($total),
			'net_label' => 'Invoice total',
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
		if ($row) {
			$token = (string) $row['token'];
			$db->prepare("UPDATE `epc_free_tool_accounts` SET `company`=?,`country`=?,`time_last_seen`=?,`use_count`=`use_count`+1 WHERE `id`=?")
				->execute(array($company, $country, $now, (int) $row['id']));
		} else {
			$token = bin2hex(random_bytes(24));
			$db->prepare("INSERT INTO `epc_free_tool_accounts` (`token`,`email`,`company`,`country`,`time_created`,`time_last_seen`,`use_count`) VALUES (?,?,?,?,?,?,1)")
				->execute(array($token, $email, $company, $country, $now, $now));
		}
		return array('ok' => true, 'token' => $token, 'account' => array('email' => $email, 'company' => $company, 'country' => $country));
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
		<div class="epm-badge"><i class="fa fa-magic"></i> Free tools — no payment</div>
		<h1>Free business tools for your company</h1>
		<p class="lead">Run your <strong>VAT/GST return</strong>, <strong>corporate tax</strong>, <strong>payroll &amp; gratuity</strong>, <strong>IFRS financials</strong>, <strong>e-invoice</strong> and <strong>approval workflow</strong> — free. Pick your country and every tool localises to your currency, tax rate and e-invoice scheme. Register once with your email to use and save results.</p>
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
		switch ($key) {
			case 'vat':
				return $num('standard_sales', 'Standard-rated sales (ex-tax)')
					. $num('zero_sales', 'Zero-rated sales')
					. $num('exempt_sales', 'Exempt sales')
					. $num('standard_purchases', 'Standard-rated purchases (ex-tax)')
					. $num('import_vat', 'Import tax paid');
			case 'ct':
				return $num('revenue', 'Revenue / turnover')
					. $num('expenses', 'Deductible expenses')
					. $num('adjustments', 'Tax adjustments (+/-)');
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
					. $num('current_liabilities', 'Current liabilities');
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
.eft-result__actions{display:flex;gap:8px;margin-top:14px}
.eft-result__actions button{font-size:.82rem;padding:7px 12px}
@media(max-width:760px){.eft-split{grid-template-columns:1fr}}
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
			if(r&&r.ok){localStorage.setItem(tokenKey,r.token);msg.className='eft-msg is-ok';msg.textContent='';showApp(r.account);}
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
		if(r.rows){h+='<table>';r.rows.forEach(function(row){h+='<tr><td>'+row[0]+'</td><td>'+money(cur,row[1])+'</td></tr>';});h+='</table>';}
		if(r.income){h+='<h4>Income statement</h4><table>';r.income.forEach(function(row){h+='<tr><td>'+row[0]+'</td><td>'+money(cur,row[1])+'</td></tr>';});h+='</table>';}
		if(r.balance){h+='<h4>Balance sheet</h4><table>';r.balance.forEach(function(row){h+='<tr><td>'+row[0]+'</td><td>'+money(cur,row[1])+'</td></tr>';});h+='</table>';}
		if(r.steps){h+='<table><tr><td><b>Spend range</b></td><td><b>Approver(s)</b></td></tr>';r.steps.forEach(function(s){h+='<tr><td>'+s.range+'</td><td>'+s.approver+' <small>('+s.sla+')</small></td></tr>';});h+='</table>';}
		if(r.lines){h+='<table><tr><td><b>Item</b></td><td><b>Amount</b></td></tr>';r.lines.forEach(function(l){h+='<tr><td>'+(l.desc||'')+' ×'+l.qty+'</td><td>'+money(cur,l.amount)+'</td></tr>';});h+='</table>';h+='<table><tr><td>Subtotal</td><td>'+money(cur,r.subtotal)+'</td></tr><tr><td>'+r.label+' ('+r.rate+'%)</td><td>'+money(cur,r.tax)+'</td></tr></table>';h+='<p><small>Invoice '+r.number+' · '+r.date+' · '+r.scheme+'<br/>UUID '+r.uuid+'</small></p>';}
		if(typeof r.net!=='undefined'&&!r.steps){h+='<table><tr class="eft-total"><td>'+(r.net_label||'Total')+'</td><td>'+(tool==='workflow'?r.net:money(cur,r.net))+'</td></tr></table>';}
		if(r.net_label&&r.steps){h+='<p class="eft-total">'+r.net+' '+r.net_label+'</p>';}
		if(r.note){h+='<p><small>'+r.note+'</small></p>';}
		h+='<div class="eft-result__actions"><button type="button" class="epm-btn epm-btn--ghost" id="eft_save"><i class="fa fa-save"></i> Save</button><button type="button" class="epm-btn epm-btn--ghost" onclick="window.print()"><i class="fa fa-print"></i> Print / PDF</button></div><p class="eft-msg" id="eft_save_msg"></p>';
		box.innerHTML=h;
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
