<?php
/**
 * Restore epartscart legacy animated SVG logo + piston homepage (undo generic tenant brand).
 * https://www.ecomae.com/epc-epartscart-legacy-restore.php?token=epartscart-deploy-2026&apply=1
 * https://www.epartscart.com/epc-epartscart-legacy-restore.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$hostname = 'www.epartscart.com';

function epc_epartscart_restore_probe(string $url): string
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
		if (stripos($body, 'epc-animated-logo') !== false) {
			$hint = ' [svg-logo]';
		} elseif (stripos($body, 'epc-tenant-brand') !== false) {
			$hint = ' [tenant-brand]';
		} elseif (stripos($body, 'ech-hub') !== false || stripos($body, 'epc-storefront-logo') !== false) {
			$hint = ' [hub-logo]';
		}
		if (stripos($body, 'epc-engine-animation') !== false) {
			$hint .= ' [piston-banner]';
		}
		if (stripos($body, 'epc-tenant-brand-hero') !== false) {
			$hint .= ' [generic-hero]';
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "=== eParts Cart legacy storefront restore ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

echo "Before:\n";
echo '  /en/: ' . epc_epartscart_restore_probe('https://' . $hostname . '/en/') . "\n\n";

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
$bare = preg_replace('/^www\./', '', $hostname);
if (is_file($overrideFile)) {
	$epc_tenant_host_db = null;
	require $overrideFile;
	foreach (array($hostname, $bare) as $hk) {
		if (isset($epc_tenant_host_db[$hk]) && is_array($epc_tenant_host_db[$hk])) {
			foreach (array('db', 'user', 'password') as $tk) {
				if (!empty($epc_tenant_host_db[$hk][$tk])) {
					$cfg->$tk = $epc_tenant_host_db[$hk][$tk];
				}
			}
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

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('DB connect failed: ' . $e->getMessage() . "\n");
}

echo "DB: {$cfg->user}@{$cfg->db}\n";

if ($apply) {
	epc_portal_db_ensure($pdo);
	$existing = epc_portal_load_site_settings($pdo);
	$contact = isset($existing['contact']) && is_array($existing['contact']) ? $existing['contact'] : array();
	$contact['use_animated_hub_logo'] = false;
	$contact['use_tenant_brand'] = false;
	$contact['storefront_package'] = 'automotive_spareparts_pro';
	$savePayload = array(
		'host' => $hostname,
		'industry_code' => 'auto_parts',
		'access_mode' => isset($existing['access_mode']) ? $existing['access_mode'] : 'full',
		'theme_template' => isset($existing['theme_template']) ? $existing['theme_template'] : 'classic',
		'system_name' => isset($existing['system_name']) ? $existing['system_name'] : 'eParts Cart',
		'hub_name' => isset($existing['hub_name']) ? $existing['hub_name'] : 'Electronic World Group',
		'tagline' => isset($existing['tagline']) ? $existing['tagline'] : 'Auto parts & commerce',
		'domain_path' => isset($existing['domain_path']) ? $existing['domain_path'] : 'https://' . $hostname . '/',
		'enabled_packs' => isset($existing['enabled_packs']) ? $existing['enabled_packs'] : array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing'),
		'contact' => array_merge(epc_portal_default_contact(array(
			'trade_name' => 'eParts Cart',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'partsdoc2025@gmail.com',
		)), $contact),
		'theme' => isset($existing['theme']) && is_array($existing['theme']) ? $existing['theme'] : epc_portal_style_template_theme('auto_parts', 'classic'),
		'cp_menu' => isset($existing['cp_menu']) ? $existing['cp_menu'] : array('hidden_groups' => array(), 'hidden_items' => array()),
	);
	epc_portal_save_site_settings($pdo, $savePayload);
	echo "site_settings saved: storefront=automotive_spareparts_pro use_animated_hub_logo=false use_tenant_brand=false\n";

	try {
		$ecomaePass = '';
		if (is_file(__DIR__ . '/config.local.php')) {
			$epc_config_local = null;
			require __DIR__ . '/config.local.php';
			if (isset($epc_config_local['password'])) {
				$ecomaePass = (string) $epc_config_local['password'];
			}
		}
		if ($ecomaePass !== '') {
			$platformPdo = new PDO(
				'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
				'ecomae',
				$ecomaePass,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			epc_portal_db_ensure($platformPdo);
			$tpl = epc_portal_tenant_templates()['epartscart'] ?? array();
			epc_portal_save_tenant($platformPdo, array(
				'site_key' => 'epartscart',
				'hostname' => $hostname,
				'industry_code' => 'auto_parts',
				'status' => 'live',
				'trade_name' => $tpl['trade_name'] ?? 'eParts Cart',
				'hub_name' => $tpl['hub_name'] ?? 'Electronic World Group',
				'from_email' => $tpl['from_email'] ?? 'partsdoc2025@gmail.com',
				'db_name' => (string) $cfg->db,
				'db_user' => (string) $cfg->user,
				'db_password' => (string) $cfg->password,
				'notes' => 'epc-epartscart-legacy-restore.php',
			));
			echo "Super CP tenant registry updated (epartscart / auto_parts)\n";
		}
	} catch (Exception $e) {
		echo 'Super CP registry: WARN — ' . $e->getMessage() . "\n";
	}
} else {
	echo "Dry run — pass apply=1 to update site_settings.\n";
}

echo "\nAfter:\n";
echo '  /en/: ' . epc_epartscart_restore_probe('https://' . $hostname . '/en/') . "\n";
echo "\nDone.\n";
