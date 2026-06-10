<?php
/**
 * Minimal Super CP hotfix (~5 KB) — upload to www.ecomae.com docroot, run once:
 * https://www.ecomae.com/ecomae-cp-hotfix-mini.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

$result = array('ok' => true, 'steps' => array());

function ecp_hotfix_patch_file(string $path, callable $patch): array
{
	if (!is_file($path)) {
		return array('ok' => false, 'message' => 'missing');
	}
	$content = (string) file_get_contents($path);
	$new = $patch($content);
	if ($new === null) {
		return array('ok' => true, 'message' => 'already patched');
	}
	if ($new === $content || file_put_contents($path, $new) === false) {
		return array('ok' => false, 'message' => 'write failed');
	}
	return array('ok' => true, 'message' => 'patched');
}

$indexPath = __DIR__ . '/index.php';
$result['steps']['index'] = ecp_hotfix_patch_file($indexPath, function (string $content) {
	$marker = 'Nginx try_files sends /cp/* subpaths here';
	if (strpos($content, $marker) !== false) {
		return null;
	}
	$needle = "epc_portal_apply_config(\$DP_Config);\n\nrequire_once \$_SERVER['DOCUMENT_ROOT']";
	$insert = <<<'PHP'
epc_portal_apply_config($DP_Config);

// Nginx try_files sends /cp/* subpaths here — hand off to backend CP (cp/index.php).
$epcBackendDir = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($epcBackendDir !== '' && isset($_SERVER['REQUEST_URI'])) {
	$epcPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if (!is_string($epcPath) || $epcPath === '') {
		$epcPath = '/';
	}
	$epcCpBase = '/' . $epcBackendDir;
	if ($epcPath === $epcCpBase || $epcPath === $epcCpBase . '/'
		|| (strlen($epcPath) > strlen($epcCpBase) && strpos($epcPath, $epcCpBase . '/') === 0)) {
		$cpEntry = $_SERVER['DOCUMENT_ROOT'] . '/' . $epcBackendDir . '/index.php';
		if (is_file($cpEntry)) {
			require $cpEntry;
			exit;
		}
	}
}

require_once $_SERVER['DOCUMENT_ROOT']
PHP;
	if (strpos($content, $needle) === false) {
		return $content;
	}
	return str_replace($needle, $insert, $content);
});

$portalPath = __DIR__ . '/content/general_pages/epc_portal.php';
$result['steps']['portal_visible'] = ecp_hotfix_patch_file($portalPath, function (string $content) {
	if (strpos($content, 'epc_portal_is_super_cp_host()) && epc_portal_is_super_cp_host())') !== false) {
		return null;
	}
	$needle = "function epc_portal_cp_item_visible(\$item_url)\n{\n\t\$url";
	$insert = "function epc_portal_cp_item_visible(\$item_url)\n{\n\tif (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {\n\t\treturn true;\n\t}\n\t\$url";
	if (strpos($content, $needle) === false) {
		return $content;
	}
	return str_replace($needle, $insert, $content);
});

if (strpos((string) @file_get_contents($portalPath), 'function epc_portal_is_super_cp_host') === false) {
	$append = <<<'PHP'

function epc_portal_is_super_cp_host()
{
	$host = function_exists('epc_portal_host') ? epc_portal_host() : '';
	return $host === 'cp.ecomae.com';
}
PHP;
	file_put_contents($portalPath, (string) file_get_contents($portalPath) . $append);
	$result['steps']['portal_super_fn'] = array('ok' => true, 'message' => 'appended');
}

$dbPath = __DIR__ . '/content/general_pages/epc_portal_db.php';
$result['steps']['portal_db'] = ecp_hotfix_patch_file($dbPath, function (string $content) {
	$old = "} elseif (\$industry_code === 'platform_host') {\n\t\t\$packs = array_values(array_filter(\$packs, function (\$p) {\n\t\t\treturn \$p !== 'super_platform';\n\t\t}));\n\t}";
	$new = "} elseif (\$industry_code === 'platform_host' && !(function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host())) {\n\t\t\$packs = array_values(array_filter(\$packs, function (\$p) {\n\t\t\treturn \$p !== 'super_platform';\n\t\t}));\n\t}";
	if (strpos($content, $new) !== false) {
		return null;
	}
	if (strpos($content, $old) !== false) {
		return str_replace($old, $new, $content);
	}
	$old2 = "} elseif ((\$settings['industry_code'] ?? '') === 'platform_host') {\n\t\t\$settings['enabled_packs'] = array_values(array_filter(\$settings['enabled_packs'], function (\$p) {\n\t\t\treturn \$p !== 'super_platform';\n\t\t}));\n\t}";
	$new2 = "} elseif ((\$settings['industry_code'] ?? '') === 'platform_host' && !(function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host())) {\n\t\t\$settings['enabled_packs'] = array_values(array_filter(\$settings['enabled_packs'], function (\$p) {\n\t\t\treturn \$p !== 'super_platform';\n\t\t}));\n\t}";
	if (strpos($content, $old2) !== false) {
		return str_replace($old2, $new2, $content);
	}
	return $content;
});

$menuPath = __DIR__ . '/cp/modules/left_cp_menu/left_cp_menu.php';
$result['steps']['left_menu'] = ecp_hotfix_patch_file($menuPath, function (string $content) {
	if (strpos($content, '$epcCpSuperHost') !== false) {
		return null;
	}
	$old = "\tif( is_anable(\$item) && epc_portal_cp_item_visible(\$item[\"url\"]) )";
	$new = "\t\$epcCpSuperHost = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();\n\tif( (is_anable(\$item) || (int)(isset(\$item['show_anyway']) ? \$item['show_anyway'] : 0) === 1 || \$epcCpSuperHost) && epc_portal_cp_item_visible(\$item[\"url\"]) )";
	if (strpos($content, $old) !== false) {
		return str_replace($old, $new, $content);
	}
	return $content;
});

$cpIndex = __DIR__ . '/cp/index.php';
$cpRedirect = <<<'PHP'
if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()
	&& ($_SERVER['REQUEST_METHOD'] ?? '') === 'GET' && isset($_SERVER['REQUEST_URI'])) {
	$cpPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	$cpBase = '/' . trim((string) $DP_Config->backend_dir, '/');
	if ($cpPath === $cpBase || $cpPath === $cpBase . '/') {
		header('Location: ' . $cpBase . '/shop/tenant_hub/tenant_hub?tab=onboard', true, 302);
		exit;
	}
}

PHP;
$result['steps']['cp_index'] = ecp_hotfix_patch_file($cpIndex, function (string $content) use ($cpRedirect) {
	if (strpos($content, 'tenant_hub/tenant_hub?tab=onboard') !== false) {
		return null;
	}
	$needle = "epc_portal_apply_config(\$DP_Config);\n\n\$isFrontMode";
	if (strpos($content, $needle) !== false) {
		return str_replace($needle, "epc_portal_apply_config(\$DP_Config);\n\n" . $cpRedirect . "\$isFrontMode", $content);
	}
	return $content;
});

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

if (is_file(__DIR__ . '/ecomae-super-cp-setup.php')) {
	require_once __DIR__ . '/ecomae-super-cp-setup.php';
	if (function_exists('ecomae_super_cp_materialize_files')) {
		$result['steps']['materialized'] = ecomae_super_cp_materialize_files(__DIR__);
	}
}

try {
	$pdo = epc_portal_platform_pdo();
	if (!$pdo instanceof PDO) {
		$cfg = new DP_Config();
		epc_portal_apply_config($cfg);
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	}
	epc_portal_db_ensure($pdo);
	require_once __DIR__ . '/epc_cp_mainstream_menu.php';
	$cpSettings = epc_portal_default_site_settings('cp.ecomae.com');
	$cpSettings['host'] = 'cp.ecomae.com';
	$cpSettings['enabled_packs'] = array_values(array_unique(array_merge(
		$cpSettings['enabled_packs'],
		array('core', 'commerce', 'professional', 'marketing', 'super_platform', 'erp', 'catalogue')
	)));
	epc_portal_save_site_settings($pdo, $cpSettings);
	$result['steps']['menu'] = epc_cp_super_platform_menu_apply($pdo);
	$result['steps']['cp_packs'] = $cpSettings['enabled_packs'];
} catch (Exception $e) {
	$result['steps']['db'] = array('ok' => false, 'message' => $e->getMessage());
}

$result['urls'] = array(
	'cp' => 'https://cp.ecomae.com/cp/',
	'tenant_hub' => 'https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard',
);

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
