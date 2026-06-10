<?php
/**
 * Taxofinca consultancy storefront: portal settings, branding strings, probes.
 * https://www.epartscart.com/epc-taxofinca-consultancy-theme.php?token=epartscart-deploy-2026&apply=1
 * On tenant vhost: https://www.taxofinca.com/epc-taxofinca-consultancy-theme.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$hostname = 'www.taxofinca.com';
$bare = 'taxofinca.com';

function epc_tfc_sync_consultancy_files(string $platformRoot): array
{
	$relFiles = array(
		'index.php',
		'templates/nero/desktop.php',
		'content/general_pages/epc_portal.php',
		'content/general_pages/epc_portal_db.php',
		'content/general_pages/epc_portal_industry_home.php',
		'content/general_pages/animated_epartscart_logo.php',
		'content/general_pages/epc_branding.css',
		'content/shop/finance/epc_erp_portal_router.php',
		'epc-taxofinca-consultancy-theme.php',
	);
	$targets = array(
		'/home/epartscart/htdocs/www.epartscart.com',
		'/home/epartscart/htdocs/www.taxofinca.com',
		'/home/taxofinca/htdocs/www.taxofinca.com',
		'/home/taxofinca/htdocs/www.taxofinca.com/public',
		'/home/ecomae/htdocs/www.taxofinca.com',
	);
	exec('grep -R "server_name.*taxofinca" /etc/nginx/ 2>/dev/null | head -40', $ngLines);
	foreach ($ngLines as $line) {
		if (preg_match('/root\s+([^;]+);/', $line, $m)) {
			$targets[] = trim($m[1]);
		}
	}
	exec('find /home -maxdepth 6 -type d -name "www.taxofinca.com" 2>/dev/null | head -10', $findDirs);
	foreach ($findDirs as $dir) {
		$targets[] = trim($dir);
	}
	exec('find /home -type f -path "*/templates/nero/desktop.php" 2>/dev/null | head -20', $desktopPaths);
	foreach ($desktopPaths as $desk) {
		$targets[] = dirname(dirname(dirname(trim($desk))));
	}
	$log = array();
	$seen = array();
	foreach ($targets as $destRoot) {
		$destRoot = rtrim($destRoot, '/');
		if ($destRoot === '' || isset($seen[$destRoot]) || !is_dir($destRoot)) {
			if ($destRoot !== '' && !isset($seen[$destRoot])) {
				$log[] = "skip missing: {$destRoot}";
			}
			continue;
		}
		$seen[$destRoot] = true;
		if (realpath($destRoot) === realpath($platformRoot)) {
			$log[] = "skip platform root: {$destRoot}";
			continue;
		}
		$log[] = "sync -> {$destRoot}";
		foreach ($relFiles as $rel) {
			$src = $platformRoot . '/' . $rel;
			if (!is_file($src)) {
				continue;
			}
			$dest = $destRoot . '/' . $rel;
			$dir = dirname($dest);
			if (!is_dir($dir)) {
				mkdir($dir, 0755, true);
			}
			$ok = @copy($src, $dest);
			$log[] = '  ' . $rel . ': ' . ($ok ? 'ok' : 'FAIL');
		}
	}
	return $log;
}

function epc_tfc_probe(string $url): string
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
		if (stripos($body, 'eParts Cart') !== false || stripos($body, 'Autoparts') !== false) {
			$hint = ' [autoparts-branding]';
		} elseif (stripos($body, 'Taxofin') !== false) {
			$hint = ' [taxofinca]';
		} elseif (stripos($body, 'Corporate tax') !== false || stripos($body, 'epc-portal-hero') !== false) {
			$hint = ' [consultancy-hero]';
		} elseif (stripos($body, '/en/shiny') !== false || stripos($body, 'Search parts') !== false) {
			$hint = ' [catalog-chrome]';
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "=== Taxofinca consultancy theme ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

echo "Before:\n";
echo '  /en/: ' . epc_tfc_probe('https://' . $hostname . '/en/') . "\n";
echo '  /erp: ' . epc_tfc_probe('https://' . $hostname . '/erp') . "\n";
echo '  /en/shop/erp: ' . epc_tfc_probe('https://' . $hostname . '/en/shop/erp') . "\n";
echo '  /cp/: ' . epc_tfc_probe('https://' . $hostname . '/cp/') . "\n\n";

$_SERVER['HTTP_HOST'] = $hostname;
$_SERVER['SERVER_NAME'] = $hostname;

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_theme_templates.php';

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

$tenant = epc_portal_load_tenant_by_host($hostname);
if ($tenant !== null) {
	echo "Tenant registry: site_key={$tenant['site_key']} registry_db=" . ($tenant['db'] ?? '') . "\n";
} else {
	echo "Tenant registry: not found\n";
}
// Model C: taxofinca vhost shares epartscart docroot + docpart DB; site_settings live in storefront DB.
$overrideDb = trim((string) ($_GET['tenant_db'] ?? ''));
if ($overrideDb !== '') {
	$dbName = $overrideDb;
	echo "Using tenant_db override: {$dbName}\n";
} else {
	echo "Storefront DB (site_settings): {$dbName}\n";
}

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
	$ind = epc_portal_industry('tax_advisory');
	$packs = array('core', 'erp', 'professional', 'tax_advisory');
	$savePayload = array(
		'industry_code' => 'tax_advisory',
		'access_mode' => 'consultancy',
		'theme_template' => 'classic',
		'system_name' => 'Taxofin',
		'hub_name' => 'Taxofinca',
		'tagline' => 'Tax & advisory services — UAE corporate tax, VAT and business compliance',
		'domain_path' => 'https://' . $hostname . '/',
		'contact' => epc_portal_default_contact(array(
			'trade_name' => 'Taxofinca',
			'hub_name' => 'Taxofinca',
			'from_email' => 'info@taxofinca.com',
		)),
		'enabled_packs' => $packs,
		'theme' => epc_portal_style_template_theme('tax_advisory', 'classic'),
	);
	foreach (array($hostname, $bare) as $hostKey) {
		$savePayload['host'] = $hostKey;
		epc_portal_save_site_settings($pdo, $savePayload);
	}
	echo "epc_portal_site_settings saved for {$hostname} + {$bare} (access_mode=consultancy)\n";

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
	echo "Lang branding strings updated (site_name → Taxofinca)\n";

	$syncLog = epc_tfc_sync_consultancy_files(rtrim(__DIR__, '/\\'));
	foreach ($syncLog as $line) {
		echo $line . "\n";
	}
}

$settings = epc_portal_load_site_settings_for_host($pdo, $hostname);
epc_portal_apply_config($cfg);
echo "\nRuntime after apply:\n";
echo '  access_mode=' . (function_exists('epc_portal_access_mode') ? epc_portal_access_mode() : '?') . "\n";
echo '  commerce_storefront=' . (function_exists('epc_portal_commerce_storefront_enabled') && epc_portal_commerce_storefront_enabled() ? 'yes' : 'no') . "\n";
echo '  industry=' . ($settings['industry_code'] ?? '?') . "\n";
echo '  domain_path=' . $cfg->domain_path . "\n";
echo '  settings_row=' . json_encode(array(
	'industry_code' => $settings['industry_code'] ?? '',
	'access_mode' => $settings['access_mode'] ?? '',
	'enabled_packs' => $settings['enabled_packs'] ?? array(),
)) . "\n";

echo "\nAfter:\n";
echo '  /en/: ' . epc_tfc_probe('https://' . $hostname . '/en/') . "\n";
echo '  /erp: ' . epc_tfc_probe('https://' . $hostname . '/erp') . "\n";
echo '  /en/shop/erp: ' . epc_tfc_probe('https://' . $hostname . '/en/shop/erp') . "\n";
echo '  /en/shop/cart: ' . epc_tfc_probe('https://' . $hostname . '/en/shop/cart') . "\n";
echo '  /cp/: ' . epc_tfc_probe('https://' . $hostname . '/cp/') . "\n";
echo "\nDone.\n";
