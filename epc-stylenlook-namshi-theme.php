<?php
/**
 * Stylenlook Namshi-style fashion & beauty theme: settings + probe.
 * https://www.ecomae.com/epc-stylenlook-namshi-theme.php?token=epartscart-deploy-2026&apply=1
 * On tenant: https://www.stylenlook.com/epc-stylenlook-namshi-theme.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$hostname = 'www.stylenlook.com';

function epc_stylenlook_probe(string $url): string
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
		if (stripos($body, 'epc-frn-header') !== false) {
			$hint = ' [namshi-header]';
		}
		if (stripos($body, 'epc-frn-footer') !== false) {
			$hint .= ' [namshi-footer]';
		}
		if (stripos($body, 'epc-frn-home') !== false) {
			$hint .= ' [namshi-retail-home]';
		}
		if (stripos($body, 'epc-frn-hero-banner') !== false || stripos($body, 'epc-frn-hero-anim') !== false) {
			$hint .= ' [animated-fashion-hero]';
		} elseif (stripos($body, 'epc-frn-chips') !== false) {
			$hint = ' [namshi-chips]';
		} elseif (stripos($body, 'epc-portal-hero') !== false) {
			$hint = ' [legacy-industry-hero]';
		}
		if (preg_match('/data-epc-storefront="([^"]+)"/', $body, $m)) {
			$hint .= ' storefront=' . $m[1];
		}
		if (stripos($body, 'epc-sf-logo--fashion') !== false) {
			$hint .= ' [animated-svg-logo]';
		} elseif (stripos($body, 'epc-tenant-brand--fashion') !== false) {
			$hint .= ' [stylenlook-logo]';
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

echo "=== Stylenlook Namshi fashion retail theme ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

echo "Before:\n";
echo '  /en/: ' . epc_stylenlook_probe('https://' . $hostname . '/en/') . "\n\n";

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

function epc_stylenlook_pdo_candidates(): array
{
	$out = array();
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
	$out[] = array('label' => 'platform', 'db' => (string) $cfg->db, 'user' => (string) $cfg->user, 'pass' => (string) $cfg->password);
	if (function_exists('epc_portal_resolve_tenant_db_credentials')) {
		$tenantCreds = epc_portal_resolve_tenant_db_credentials();
		if (!empty($tenantCreds['password'])) {
			$out[] = array(
				'label' => 'docpart',
				'db' => (string) ($tenantCreds['db'] ?? 'docpart'),
				'user' => (string) ($tenantCreds['user'] ?? 'docpart'),
				'pass' => (string) $tenantCreds['password'],
			);
		}
	}
	$seen = array();
	$unique = array();
	foreach ($out as $row) {
		$key = strtolower($row['db'] . '@' . $row['user']);
		if ($row['pass'] === '' || isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$unique[] = $row;
	}
	return $unique;
}

function epc_stylenlook_connect(array $row): ?PDO
{
	try {
		return new PDO(
			'mysql:host=127.0.0.1;dbname=' . $row['db'] . ';charset=utf8',
			$row['user'],
			$row['pass'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return null;
	}
}

$pdoTargets = epc_stylenlook_pdo_candidates();
if ($pdoTargets === array()) {
	exit("No DB targets resolved\n");
}

echo 'DB targets: ' . implode(', ', array_map(function ($r) {
	return $r['label'] . '=' . $r['user'] . '@' . $r['db'];
}, $pdoTargets)) . "\n";

if ($apply) {
	$themeTemplate = 'signature';
	$namshiTheme = epc_portal_style_template_theme('fashion', $themeTemplate);
	$packs = array('core', 'commerce', 'catalogue');
	$savePayload = array(
		'host' => $hostname,
		'industry_code' => 'fashion',
		'access_mode' => 'full',
		'theme_template' => $themeTemplate,
		'system_name' => 'Stylenlook',
		'hub_name' => 'Stylenlook',
		'tagline' => 'Fashion, beauty & lifestyle — UAE delivery, prices in AED',
		'domain_path' => 'https://' . $hostname . '/',
		'enabled_packs' => $packs,
		'contact' => epc_portal_default_contact(array(
			'trade_name' => 'Stylenlook',
			'hub_name' => 'Stylenlook',
			'from_email' => 'hello@stylenlook.com',
			'use_animated_hub_logo' => false,
			'storefront_package' => 'fashion_retail_namshi',
		)),
		'theme' => $namshiTheme,
	);
	$bareHost = preg_replace('/^www\./', '', $hostname);
	$savePayloadBare = $savePayload;
	$savePayloadBare['host'] = $bareHost;
	$savePayloadBare['domain_path'] = 'https://' . $bareHost . '/';

	foreach ($pdoTargets as $target) {
		$pdo = epc_stylenlook_connect($target);
		if (!$pdo instanceof PDO) {
			echo $target['label'] . ': connect FAIL' . "\n";
			continue;
		}
		epc_portal_db_ensure($pdo);
		epc_portal_save_site_settings($pdo, $savePayload);
		epc_portal_save_site_settings($pdo, $savePayloadBare);
		echo $target['label'] . ': site_settings saved (signature + fashion_retail_namshi)' . "\n";

		$tenantSave = epc_portal_save_tenant($pdo, array(
			'site_key' => 'stylenlook',
			'hostname' => $hostname,
			'industry_code' => 'fashion',
			'status' => 'live',
			'trade_name' => 'Stylenlook',
			'hub_name' => 'Stylenlook',
			'from_email' => 'hello@stylenlook.com',
			'db_name' => $target['db'],
			'db_user' => $target['user'],
			'db_password' => $target['pass'],
			'notes' => 'epc-stylenlook-namshi-theme.php',
		));
		echo $target['label'] . ' tenant registry: ' . ($tenantSave['ok'] ? 'OK' : 'FAIL') . "\n";

		$verify = $pdo->prepare('SELECT `theme_template`, `system_name`, `contact_json` FROM `epc_portal_site_settings` WHERE `host` = ? OR `host` = ? LIMIT 1');
		$verify->execute(array($hostname, $bareHost));
		$row = $verify->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$contact = json_decode((string) ($row['contact_json'] ?? ''), true);
			$pkg = is_array($contact) && !empty($contact['storefront_package']) ? $contact['storefront_package'] : '?';
			echo $target['label'] . ' verify: theme=' . $row['theme_template'] . ' name=' . $row['system_name'] . ' pkg=' . $pkg . "\n";
		}
	}

	$seoTitle = 'Stylenlook — Fashion & Beauty UAE';
	$seoDesc = 'Shop Stylenlook for women, men, beauty & accessories. Fast UAE delivery. MAC, Dior, Charlotte Tilbury & more. Prices in AED.';
	$seoKeys = 'fashion UAE, beauty Dubai, makeup, skincare, dresses, shoes, AED, Stylenlook';
	foreach ($pdoTargets as $target) {
		$pdo = epc_stylenlook_connect($target);
		if (!$pdo instanceof PDO) {
			continue;
		}
		try {
			$mainUp = $pdo->prepare(
				'UPDATE `content` SET `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?, `value` = ?
				 WHERE `main_flag` = 1 LIMIT 5'
			);
			$mainUp->execute(array($seoTitle, $seoDesc, $seoKeys, 'Home'));
			echo $target['label'] . ' content SEO rows: ' . $mainUp->rowCount() . "\n";
		} catch (Exception $e) {
			echo $target['label'] . ' content SEO skipped: ' . $e->getMessage() . "\n";
		}
	}
} else {
	echo "Dry run — add apply=1 to write site_settings.\n";
}

echo "\nAfter:\n";
echo '  /en/: ' . epc_stylenlook_probe('https://' . $hostname . '/en/') . "\n";
echo "\nDone.\n";
