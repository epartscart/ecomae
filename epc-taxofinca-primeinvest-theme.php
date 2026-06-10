<?php
/**
 * Taxofinca Prime Invest consulting theme: settings + probe.
 * https://www.ecomae.com/epc-taxofinca-primeinvest-theme.php?token=epartscart-deploy-2026&apply=1
 * On tenant: https://www.taxofinca.com/epc-taxofinca-primeinvest-theme.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$hostname = 'www.taxofinca.com';
$bare = 'taxofinca.com';

function epc_tfc_pi_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$hint = '';
	if (is_string($body) && $body !== '') {
		if (stripos($body, 'epc-cpi-header') !== false) {
			$hint = ' [cpi-header]';
		}
		if (stripos($body, 'epc-cpi-footer') !== false) {
			$hint .= ' [cpi-footer]';
		}
		if (stripos($body, 'epc-cpi-home') !== false) {
			$hint .= ' [cpi-home]';
		} elseif (stripos($body, 'epc-cpi-hero') !== false) {
			$hint .= ' [cpi-hero]';
		}
		if (preg_match('/data-epc-storefront="([^"]+)"/', $body, $m)) {
			$hint .= ' storefront=' . $m[1];
		}
		if (preg_match('/data-epc-style="([^"]+)"/', $body, $m)) {
			$hint .= ' style=' . $m[1];
		}
		if (stripos($body, 'epc_consulting_primeinvest.css') !== false) {
			$hint .= ' [cpi-css]';
		}
		if (stripos($body, '--epc-cpi-green:#227a40') !== false || stripos($body, '--epc-portal-primary:#227a40') !== false) {
			$hint .= ' [prime-green]';
		}
		if (stripos($body, 'taxofinca.png') !== false || stripos($body, 'epc-tenant-brand__logo') !== false) {
			$hint .= ' [tenant-logo]';
		}
		$bad = array('eParts Cart', 'Autoparts', 'autoparts', 'auto parts', 'spare parts', 'Part number', 'VIN');
		foreach ($bad as $b) {
			if (stripos($body, $b) !== false) {
				$hint .= ' [WARN:' . $b . ']';
			}
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "=== Taxofinca Prime Invest consulting theme ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

echo "Before:\n";
echo '  /en/: ' . epc_tfc_pi_probe('https://' . $hostname . '/en/') . "\n\n";

$_SERVER['HTTP_HOST'] = $hostname;
$_SERVER['SERVER_NAME'] = $hostname;

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_theme_templates.php';
require_once __DIR__ . '/content/general_pages/epc_consulting_primeinvest_data.php';

$cfg = epc_portal_docpart_config();
if (is_file(__DIR__ . '/config.local.php')) {
	$epc_config_local = null;
	require __DIR__ . '/config.local.php';
	if (isset($epc_config_local) && is_array($epc_config_local)) {
		foreach ($epc_config_local as $key => $value) {
			if (property_exists($cfg, $key)) {
				$cfg->$key = $value;
			}
		}
	}
}

$dbPass = (string) $cfg->password;
$dbUser = (string) $cfg->user;
$dbName = (string) $cfg->db;

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('DB connect failed: ' . $e->getMessage() . "\n");
}

echo "DB: {$dbUser}@{$dbName}\n";

if ($apply) {
	epc_portal_db_ensure($pdo);
	$themeTemplate = 'modern';
	$packs = array('core', 'erp', 'professional', 'tax_advisory');
	$cpiTheme = epc_cpi_theme_palette();
	$savePayload = array(
		'industry_code' => 'tax_advisory',
		'access_mode' => 'consultancy',
		'theme_template' => $themeTemplate,
		'system_name' => 'Taxofin',
		'hub_name' => 'Taxofinca',
		'tagline' => 'Tax & advisory services — UAE corporate tax, VAT and business compliance',
		'domain_path' => 'https://' . $hostname . '/',
		'enabled_packs' => $packs,
		'contact' => epc_portal_default_contact(array(
			'trade_name' => 'Taxofinca',
			'hub_name' => 'Taxofinca',
			'from_email' => 'info@taxofinca.com',
			'storefront_package' => 'consulting_primeinvest',
		)),
		'theme' => $cpiTheme,
	);
	foreach (array($hostname, $bare) as $hostKey) {
		$savePayload['host'] = $hostKey;
		epc_portal_save_site_settings($pdo, $savePayload);
	}
	echo "site_settings saved: theme={$themeTemplate} storefront=consulting_primeinvest\n";

	$brandStrings = array(
		3997 => array('en' => 'Taxofinca', 'ru' => 'Taxofinca', 'ar' => 'Taxofinca'),
		3998 => array('en' => 'Tax & advisory services', 'ru' => 'Налоговое и консультационное обслуживание'),
		3999 => array('en' => 'Tax advisory portal by Taxofinca', 'ru' => 'Портал налогового консалтинга Taxofinca'),
		4000 => array('en' => 'Client portal by Taxofinca', 'ru' => 'Клиентский портал Taxofinca'),
		4035 => array('en' => 'Taxofinca', 'ru' => 'Taxofinca'),
	);
	$ins = $pdo->prepare(
		'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`)
		VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
	);
	foreach ($brandStrings as $strId => $langs) {
		$keyRow = $pdo->prepare('SELECT `str_key` FROM `lang_text_strings` WHERE `id` = ? LIMIT 1');
		$keyRow->execute(array((int) $strId));
		$strKey = (string) $keyRow->fetchColumn();
		if ($strKey === '') {
			$strKey = 'epc_taxofinca_' . (int) $strId;
		}
		foreach ($langs as $lang => $value) {
			$ins->execute(array($strKey, $lang, $value));
		}
	}
	echo "Lang branding strings updated\n";

	$seoTitle = 'Taxofinca — Tax, Accounting & Advisory UAE';
	$seoDesc = 'Corporate tax, VAT compliance, bookkeeping and business advisory. Client ERP portal and professional support in the UAE.';
	$seoKeys = 'tax advisory UAE, corporate tax, VAT, accounting, Taxofinca, business compliance';
	try {
		$mainUp = $pdo->prepare(
			'UPDATE `content` SET `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?, `value` = ?
			 WHERE `main_flag` = 1 LIMIT 5'
		);
		$mainUp->execute(array($seoTitle, $seoDesc, $seoKeys, 'Home'));
		echo 'content main_flag SEO rows updated: ' . $mainUp->rowCount() . "\n";
	} catch (Exception $e) {
		echo 'content SEO update skipped: ' . $e->getMessage() . "\n";
	}

	$tenantSave = epc_portal_save_tenant($pdo, array(
		'site_key' => 'taxofinca',
		'hostname' => $hostname,
		'industry_code' => 'tax_advisory',
		'status' => 'live',
		'trade_name' => 'Taxofinca',
		'hub_name' => 'Taxofinca',
		'from_email' => 'info@taxofinca.com',
		'db_name' => $dbName,
		'db_user' => $dbUser,
		'db_password' => $dbPass,
		'notes' => 'epc-taxofinca-primeinvest-theme.php',
	));
	echo 'Tenant registry: ' . ($tenantSave['ok'] ? 'OK' : 'FAIL') . ' — ' . ($tenantSave['message'] ?? '') . "\n";

	$verify = $pdo->prepare('SELECT `theme_template`, `system_name`, `tagline` FROM `epc_portal_site_settings` WHERE `host` = ? OR `host` = ? LIMIT 1');
	$verify->execute(array($hostname, $bare));
	$row = $verify->fetch(PDO::FETCH_ASSOC);
	echo 'DB verify: ' . ($row ? json_encode($row) : 'NO ROW') . "\n";
} else {
	echo "Dry run — add apply=1 to write site_settings.\n";
}

echo "\nAfter:\n";
echo '  /en/: ' . epc_tfc_pi_probe('https://' . $hostname . '/en/') . "\n";
echo "\nDone.\n";
