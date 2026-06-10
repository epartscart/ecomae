<?php
/**
 * Patch root index.php so /cp/* subpaths load cp/index.php (backend CP).
 * https://www.ecomae.com/ecomae-fix-cp-delegate.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$indexPath = __DIR__ . '/index.php';
if (!is_file($indexPath)) {
	echo json_encode(array('ok' => false, 'message' => 'index.php missing'), JSON_PRETTY_PRINT);
	exit;
}

$marker = 'Nginx try_files sends /cp/* subpaths here';
$content = (string) file_get_contents($indexPath);
if (strpos($content, $marker) !== false) {
	echo json_encode(array('ok' => true, 'message' => 'Already patched', 'index' => $indexPath), JSON_PRETTY_PRINT);
	exit;
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
	echo json_encode(array('ok' => false, 'message' => 'index.php anchor not found — upload stage/index.php manually'), JSON_PRETTY_PRINT);
	exit;
}

$newContent = str_replace($needle, $insert, $content);
if ($newContent === $content || file_put_contents($indexPath, $newContent) === false) {
	echo json_encode(array('ok' => false, 'message' => 'Patch write failed'), JSON_PRETTY_PRINT);
	exit;
}

echo json_encode(array(
	'ok' => true,
	'message' => 'index.php patched — /cp/* now delegates to backend CP',
	'verify' => array(
		'tenant_hub' => 'https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard',
		'super_cp' => 'https://cp.ecomae.com/cp/',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
