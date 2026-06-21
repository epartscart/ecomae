<?php
/**
 * Platform governance — policies, rules, protocols (Super CP / ecomae platform DB).
 */
defined('_ASTEXE_') or die('No access');

function epc_platform_governance_categories(): array
{
	return array(
		'auth' => array('label' => 'Authentication', 'icon' => 'fa-shield-alt'),
		'tenant' => array('label' => 'Tenants', 'icon' => 'fa-building'),
		'api' => array('label' => 'Public API', 'icon' => 'fa-plug'),
		'erp' => array('label' => 'ERP', 'icon' => 'fa-university'),
		'demo' => array('label' => 'Demos', 'icon' => 'fa-flask'),
		'branding' => array('label' => 'Branding', 'icon' => 'fa-palette'),
		'tax' => array('label' => 'Tax & compliance', 'icon' => 'fa-balance-scale'),
		'catalog' => array('label' => 'Catalog & commerce', 'icon' => 'fa-th-large'),
		'protocol' => array('label' => 'System protocol', 'icon' => 'fa-heartbeat'),
	);
}

function epc_platform_governance_enforcement_levels(): array
{
	return array('advisory', 'required', 'blocked');
}

function epc_platform_governance_db_ensure(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_platform_governance_rules` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`rule_key` VARCHAR(64) NOT NULL,
			`category` VARCHAR(24) NOT NULL DEFAULT \'tenant\',
			`title` VARCHAR(160) NOT NULL DEFAULT \'\',
			`description` TEXT NULL,
			`enforcement` VARCHAR(16) NOT NULL DEFAULT \'required\',
			`scope` VARCHAR(32) NOT NULL DEFAULT \'all_tenants\',
			`config_json` TEXT NULL,
			`module_link` VARCHAR(255) NOT NULL DEFAULT \'\',
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`time_updated` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `rule_key` (`rule_key`),
			KEY `category` (`category`),
			KEY `scope` (`scope`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

/** Default platform policies (seeded once per rule_key). */
function epc_platform_governance_default_rules(): array
{
	return array(
		array(
			'rule_key' => 'auth_tenant_email_otp_google',
			'category' => 'auth',
			'title' => 'Tenant CP login: email OTP + Google OAuth',
			'description' => 'Client CP and storefront admin login use email OTP where SMTP is configured, and Google OAuth when client credentials are set. Super CP may use operator password + OTP tools.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('otp' => true, 'google_oauth' => 'when_configured'),
			'module_link' => '/cp/control/portal/epc_cp_auth_settings',
		),
		array(
			'rule_key' => 'branding_no_vendor_labels',
			'category' => 'branding',
			'title' => 'No vendor branding in customer UI',
			'description' => 'Customer-facing CP, storefront, and invoices must not show umapi, docpart, or crossbase vendor names. Use trade name and ECOM AE platform branding only.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('blocked_tokens' => array('umapi', 'docpart', 'crossbase')),
			'module_link' => '/epc-neutralize-vendor-db.php',
		),
		array(
			'rule_key' => 'tax_vat_fta_legislation_source',
			'category' => 'tax',
			'title' => 'VAT compliance: FTA legislation.aspx source',
			'description' => 'UAE VAT guidance and ERP tax compliance modules must reference Federal Tax Authority legislation at tax.gov.ae/en/legislation.aspx as the authoritative source.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('fta_legislation_url' => 'https://tax.gov.ae/en/legislation.aspx'),
			'module_link' => '/cp/shop/finance/erp',
		),
		array(
			'rule_key' => 'api_clients_require_key',
			'category' => 'api',
			'title' => 'API clients require X-API-Key',
			'description' => 'REST API at /epc-api/v1/ requires a valid tenant X-API-Key. Storefront catalog and public HTML pages do not require an API key.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('header' => 'X-API-Key', 'public_catalog_no_key' => true),
			'module_link' => '/cp/control/portal/epc_api_documentation_guide',
		),
		array(
			'rule_key' => 'demo_expiry_enforced',
			'category' => 'demo',
			'title' => 'Demo tenants expire on schedule',
			'description' => 'Active demo storefronts and CP accounts must respect expiry dates in epc_portal_demo_requests. Expired demos return 410 or redirect to marketing.',
			'enforcement' => 'required',
			'scope' => 'demo',
			'config_json' => array('max_active' => 12),
			'module_link' => '/cp/control/portal/epc_demo_tenants_manage',
		),
		array(
			'rule_key' => 'erp_only_no_storefront_redirect',
			'category' => 'erp',
			'title' => 'ERP-only: no storefront redirect',
			'description' => 'Tenants with access_mode erp_only must land on ERP home (/cp/shop/finance/erp) and must not redirect anonymous visitors to a commerce storefront.',
			'enforcement' => 'required',
			'scope' => 'erp_only',
			'config_json' => array('access_mode' => 'erp_only'),
			'module_link' => '/cp/control/portal/epc_erp_only_onboard_guide',
		),
		array(
			'rule_key' => 'registration_retail_auto_wholesale_cp',
			'category' => 'tenant',
			'title' => 'Registration: retail auto-approve, wholesale CP approval',
			'description' => 'B2C retail customer registration may auto-approve when industry settings allow. B2B wholesale accounts require CP operator approval workflow.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('retail' => 'auto', 'wholesale' => 'cp_approval'),
			'module_link' => '/cp/control/portal/epc_customer_approval_guide',
		),
		array(
			'rule_key' => 'invoice_uae_trn_b2b',
			'category' => 'tax',
			'title' => 'UAE TRN on B2B tax invoices',
			'description' => 'B2B tax invoices must include supplier and customer TRN where applicable, per FTA PINT-AE format. ERP invoice templates enforce TRN fields.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('trn_required_b2b' => true),
			'module_link' => '/cp/shop/finance/erp',
		),
		array(
			'rule_key' => 'uae_country_registration_required',
			'category' => 'tax',
			'title' => 'UAE country registration for FTA VAT',
			'description' => 'UAE ERP tenants must register company country AE, VAT registration flag, and 15-digit TRN (seller profile) before FTA output VAT on sales, input VAT on purchases, and tax invoices.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('country_code' => 'AE', 'trn_digits' => 15, 'settings' => array('company_country_code', 'company_trn', 'company_vat_registered')),
			'module_link' => '/cp/shop/finance/erp?tab=einvoice&einv_section=seller',
		),
		array(
			'rule_key' => 'protocol_cp_login_reachable',
			'category' => 'protocol',
			'title' => 'CP login endpoint reachable',
			'description' => 'Startup check: GET /cp/ returns 200 or 302 to auth plugin, not 404/525.',
			'enforcement' => 'required',
			'scope' => 'all_tenants',
			'config_json' => array('probe_path' => '/cp/'),
			'module_link' => '',
		),
		array(
			'rule_key' => 'protocol_umapi_proxy_json',
			'category' => 'protocol',
			'title' => 'UMAPI proxy returns JSON',
			'description' => 'Parts search proxy must respond with application/json, not HTML error pages, for catalog integrations.',
			'enforcement' => 'advisory',
			'scope' => 'all_tenants',
			'config_json' => array(),
			'module_link' => '',
		),
		array(
			'rule_key' => 'protocol_fta_legislation_fetch',
			'category' => 'protocol',
			'title' => 'FTA legislation fetch',
			'description' => 'Health probe: tax.gov.ae/en/legislation.aspx is reachable for compliance module updates.',
			'enforcement' => 'advisory',
			'scope' => 'all_tenants',
			'config_json' => array('url' => 'https://tax.gov.ae/en/legislation.aspx'),
			'module_link' => '',
		),
	);
}

function epc_platform_governance_seed(PDO $pdo): int
{
	epc_platform_governance_db_ensure($pdo);
	$now = time();
	$count = 0;
	$stmt = $pdo->prepare(
		'INSERT INTO `epc_platform_governance_rules`
		 (`rule_key`, `category`, `title`, `description`, `enforcement`, `scope`, `config_json`, `module_link`, `active`, `time_updated`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?)
		 ON DUPLICATE KEY UPDATE
		 `title` = IF(`title` = \'\', VALUES(`title`), `title`),
		 `description` = IF(`description` IS NULL OR `description` = \'\', VALUES(`description`), `description`),
		 `module_link` = IF(`module_link` = \'\', VALUES(`module_link`), `module_link`),
		 `time_updated` = VALUES(`time_updated`)'
	);
	foreach (epc_platform_governance_default_rules() as $row) {
		$config = isset($row['config_json']) && is_array($row['config_json'])
			? json_encode($row['config_json'], JSON_UNESCAPED_UNICODE)
			: '{}';
		$stmt->execute(array(
			$row['rule_key'],
			$row['category'],
			$row['title'],
			$row['description'],
			$row['enforcement'],
			$row['scope'],
			$config,
			$row['module_link'] ?? '',
			$now,
		));
		$count++;
	}
	return $count;
}

function epc_platform_governance_list_rules(PDO $pdo, ?string $category = null): array
{
	epc_platform_governance_db_ensure($pdo);
	$sql = 'SELECT * FROM `epc_platform_governance_rules`';
	$args = array();
	if ($category !== null && $category !== '') {
		$sql .= ' WHERE `category` = ?';
		$args[] = $category;
	}
	$sql .= ' ORDER BY `category` ASC, `rule_key` ASC';
	$st = $pdo->prepare($sql);
	$st->execute($args);
	$rows = array();
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$r['config'] = json_decode((string) ($r['config_json'] ?? '{}'), true);
		if (!is_array($r['config'])) {
			$r['config'] = array();
		}
		$rows[] = $r;
	}
	return $rows;
}

function epc_platform_governance_update_rule(PDO $pdo, string $ruleKey, array $patch): bool
{
	epc_platform_governance_db_ensure($pdo);
	$allowed = array('active', 'enforcement', 'title', 'description');
	$sets = array();
	$vals = array();
	foreach ($allowed as $col) {
		if (!array_key_exists($col, $patch)) {
			continue;
		}
		if ($col === 'active') {
			$sets[] = '`active` = ?';
			$vals[] = !empty($patch['active']) ? 1 : 0;
		} elseif ($col === 'enforcement') {
			$enf = (string) $patch['enforcement'];
			if (!in_array($enf, epc_platform_governance_enforcement_levels(), true)) {
				continue;
			}
			$sets[] = '`enforcement` = ?';
			$vals[] = $enf;
		} else {
			$sets[] = '`' . $col . '` = ?';
			$vals[] = (string) $patch[$col];
		}
	}
	if ($sets === array()) {
		return false;
	}
	$sets[] = '`time_updated` = ?';
	$vals[] = time();
	$vals[] = $ruleKey;
	$pdo->prepare('UPDATE `epc_platform_governance_rules` SET ' . implode(', ', $sets) . ' WHERE `rule_key` = ? LIMIT 1')
		->execute($vals);
	return true;
}

function epc_platform_governance_rule_applies(array $rule, array $context): bool
{
	$scope = (string) ($rule['scope'] ?? 'all_tenants');
	if ($scope === 'all_tenants') {
		return true;
	}
	if ($scope === 'demo' && !empty($context['is_demo'])) {
		return true;
	}
	if ($scope === 'erp_only' && (($context['access_mode'] ?? '') === 'erp_only')) {
		return true;
	}
	if ($scope === 'tenant_key') {
		$key = (string) ($context['site_key'] ?? '');
		$target = (string) ($rule['config']['tenant_key'] ?? $rule['config']['site_key'] ?? '');
		return $target === '' || ($key !== '' && $key === $target);
	}
	if (strpos($scope, 'tenant:') === 0) {
		$key = substr($scope, 7);
		return $key !== '' && $key === (string) ($context['site_key'] ?? '');
	}
	return false;
}

function epc_platform_governance_active_rules(PDO $pdo, array $context = array()): array
{
	$out = array();
	foreach (epc_platform_governance_list_rules($pdo) as $rule) {
		if (empty($rule['active'])) {
			continue;
		}
		if (epc_platform_governance_rule_applies($rule, $context)) {
			$out[] = $rule;
		}
	}
	return $out;
}

function epc_platform_governance_blocked_for_branding(string $html): bool
{
	$lower = strtolower($html);
	foreach (array('umapi', 'docpart', 'crossbase') as $token) {
		if (strpos($lower, $token) !== false) {
			return true;
		}
	}
	return false;
}
