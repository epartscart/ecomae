<?php
/**
 * The Jewellery Trend Kiyasha-style theme: settings + probe.
 * https://www.ecomae.com/epc-thejewellerytrend-kiyasha-theme.php?token=epartscart-deploy-2026&apply=1
 * On tenant: https://www.thejewellerytrend.com/epc-thejewellerytrend-kiyasha-theme.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$hostname = 'www.thejewellerytrend.com';

function epc_thejewellerytrend_probe(string $url): string
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
		if (stripos($body, 'epc-jrk-header') !== false) {
			$hint = ' [kiyasha-header]';
		}
		if (stripos($body, 'epc-jrk-footer') !== false) {
			$hint .= ' [kiyasha-footer]';
		}
		if (stripos($body, 'epc-jrk-home') !== false) {
			$hint .= ' [kiyasha-retail-home]';
		} elseif (stripos($body, 'epc-jrk-chips') !== false) {
			$hint = ' [kiyasha-chips]';
		} elseif (stripos($body, 'epc-portal-hero') !== false) {
			$hint = ' [legacy-industry-hero]';
		}
		if (preg_match('/data-epc-storefront="([^"]+)"/', $body, $m)) {
			$hint .= ' storefront=' . $m[1];
		}
		if (stripos($body, 'thejewellerytrend.png') !== false || stripos($body, 'epc-tenant-brand--header') !== false) {
			$hint .= ' [tenant-logo]';
		}
		$bad = array('eParts Cart', 'Autoparts', 'autoparts', 'auto parts', 'spare parts', 'epartscart.com', 'Part number', 'VIN');
		foreach ($bad as $b) {
			if (stripos($body, $b) !== false) {
				$hint .= ' [WARN:' . $b . ']';
			}
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "=== The Jewellery Trend Kiyasha retail theme ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

echo "Before:\n";
echo '  /en/: ' . epc_thejewellerytrend_probe('https://' . $hostname . '/en/') . "\n\n";

$_SERVER['HTTP_HOST'] = $hostname;
$_SERVER['SERVER_NAME'] = $hostname;

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_theme_templates.php';

$cfg = new DP_Config();
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
$overrideFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($overrideFile)) {
	$epc_tenant_host_db = null;
	require $overrideFile;
	if (isset($epc_tenant_host_db[$hostname]) && is_array($epc_tenant_host_db[$hostname])) {
		foreach (array('db', 'user', 'password') as $tk) {
			if (!empty($epc_tenant_host_db[$hostname][$tk])) {
				$cfg->$tk = $epc_tenant_host_db[$hostname][$tk];
			}
		}
	}
}
$bare = preg_replace('/^www\./', '', $hostname);
if (is_file($overrideFile) && isset($epc_tenant_host_db[$bare]) && is_array($epc_tenant_host_db[$bare])) {
	foreach (array('db', 'user', 'password') as $tk) {
		if (!empty($epc_tenant_host_db[$bare][$tk])) {
			$cfg->$tk = $epc_tenant_host_db[$bare][$tk];
		}
	}
}
if (function_exists('epc_portal_runtime_host_db')) {
	$runtimeDb = epc_portal_runtime_host_db($hostname);
	if ($runtimeDb === null && $bare !== $hostname) {
		$runtimeDb = epc_portal_runtime_host_db($bare);
	}
	if (is_array($runtimeDb)) {
		$cfg->db = $runtimeDb['db'];
		$cfg->user = $runtimeDb['user'];
		$cfg->password = $runtimeDb['password'];
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
	$themeTemplate = 'classic';
	$kiyashaTheme = epc_portal_style_template_theme('jewellery', $themeTemplate);
	$packs = array('core', 'commerce', 'catalogue');
	$savePayload = array(
		'host' => $hostname,
		'industry_code' => 'jewellery',
		'access_mode' => 'full',
		'theme_template' => $themeTemplate,
		'system_name' => 'The Jewellery Trend',
		'hub_name' => 'The Jewellery Trend',
		'tagline' => 'Fine gold, diamonds & bridal jewellery — UAE delivery, prices in AED',
		'domain_path' => 'https://' . $hostname . '/',
		'enabled_packs' => $packs,
		'contact' => epc_portal_default_contact(array(
			'trade_name' => 'The Jewellery Trend',
			'hub_name' => 'The Jewellery Trend',
			'from_email' => 'hello@thejewellerytrend.com',
			'use_animated_hub_logo' => false,
			'storefront_package' => 'jewellery_retail_kiyasha',
		)),
		'theme' => $kiyashaTheme,
	);
	epc_portal_save_site_settings($pdo, $savePayload);
	echo "site_settings saved: theme={$themeTemplate} system_name=The Jewellery Trend storefront=jewellery_retail_kiyasha\n";

	$seoTitle = 'The Jewellery Trend — Fine Jewellery UAE';
	$seoDesc = 'Shop The Jewellery Trend for gold, diamond rings, necklaces & bridal collections. Insured UAE delivery. Prices in AED.';
	$seoKeys = 'jewellery UAE, gold Dubai, diamond rings, necklaces, earrings, bridal, AED, The Jewellery Trend';
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
		'site_key' => 'thejewellerytrend',
		'hostname' => $hostname,
		'industry_code' => 'jewellery',
		'status' => 'live',
		'trade_name' => 'The Jewellery Trend',
		'hub_name' => 'The Jewellery Trend',
		'from_email' => 'hello@thejewellerytrend.com',
		'db_name' => $dbName,
		'db_user' => $dbUser,
		'db_password' => $dbPass,
		'notes' => 'epc-thejewellerytrend-kiyasha-theme.php',
	));
	echo 'Tenant registry: ' . ($tenantSave['ok'] ? 'OK' : 'FAIL') . ' — ' . ($tenantSave['message'] ?? '') . "\n";

	$verify = $pdo->prepare('SELECT `theme_template`, `system_name`, `tagline` FROM `epc_portal_site_settings` WHERE `host` = ? OR `host` = ? LIMIT 1');
	$verify->execute(array($hostname, 'thejewellerytrend.com'));
	$row = $verify->fetch(PDO::FETCH_ASSOC);
	echo 'DB verify: ' . ($row ? json_encode($row) : 'NO ROW') . "\n";
} else {
	echo "Dry run — add apply=1 to write site_settings.\n";
}

echo "\nAfter:\n";
echo '  /en/: ' . epc_thejewellerytrend_probe('https://' . $hostname . '/en/') . "\n";
echo "\nDone.\n";
