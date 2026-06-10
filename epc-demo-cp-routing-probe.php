<?php
/**
 * Probe demo CP bootstrap — GET ?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	echo "forbidden\n";
	exit;
}

if (isset($_GET['grep'])) {
	$needle = (string) $_GET['grep'];
	$files = array(
		'/cp/index.php',
		'/index.php',
		'/content/general_pages/epc_portal_demo.php',
		'/cp/plugins/authentication/plugin.php',
		'/cp/epc_cp_auth_gate.php',
	);
	foreach ($files as $rel) {
		$f = $_SERVER['DOCUMENT_ROOT'] . $rel;
		if (!is_file($f)) {
			echo $rel . " missing\n";
			continue;
		}
		$c = (string) file_get_contents($f);
		echo $rel . ' count=' . substr_count($c, $needle) . "\n";
		if (!empty($_GET['context']) && strpos($c, $needle) !== false) {
			$pos = strpos($c, $needle);
			echo substr($c, max(0, $pos - 120), 280) . "\n---\n";
		}
	}
	exit;
}

define('_ASTEXE_', 1);
$probeKey = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['key'] ?? 'demo_260601_ap_1'));
$_SERVER['REQUEST_URI'] = '/cp/demo/' . $probeKey . '/';

if (!empty($_GET['registry'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	$pdo = epc_portal_demo_platform_pdo();
	if (!$pdo instanceof PDO) {
		echo "platform_pdo=no\n";
		exit;
	}
	epc_portal_demo_ensure_schema($pdo);
	$st = $pdo->prepare('SELECT `site_key`,`status`,`is_demo`,`db_name`,`db_user`,`db_password`,`demo_expires_at`,`demo_contact_email`,`trade_name` FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
	$st->execute(array($probeKey));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		echo "registry=missing site_key={$probeKey}\n";
		exit;
	}
	echo 'registry=' . json_encode($row, JSON_UNESCAPED_UNICODE) . "\n";
	$exp = (int) ($row['demo_expires_at'] ?? 0);
	echo 'expired=' . ($exp > 0 && $exp < time() ? 'yes' : 'no') . "\n";
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	echo 'tenant_pdo=' . ($tenantPdo instanceof PDO ? 'ok' : 'fail') . "\n";
	if ($tenantPdo instanceof PDO) {
		try {
			$tpl = $tenantPdo->query('SELECT COUNT(*) FROM `templates` WHERE `current` = 1 AND `is_frontend` = 0')->fetchColumn();
			echo 'cp_template_rows=' . (int) $tpl . "\n";
		} catch (Exception $e) {
			echo 'cp_template_err=' . $e->getMessage() . "\n";
		}
	}
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
$boot = epc_portal_demo_try_bootstrap_cp($DP_Config);
epc_portal_apply_config($DP_Config);
if (function_exists('epc_portal_demo_reapply_cp_config')) {
	epc_portal_demo_reapply_cp_config($DP_Config);
}
echo 'boot=' . ($boot ? 'yes' : 'no') . "\n";
echo 'demo_ctx=' . (!empty($GLOBALS['epc_demo_cp_context']) ? 'yes' : 'no') . "\n";
echo 'db=' . ($DP_Config->db ?? '') . "\n";
echo 'domain_path=' . ($DP_Config->domain_path ?? '') . "\n";
echo 'uri_after=' . ($_SERVER['REQUEST_URI'] ?? '') . "\n";
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_professional_shell.php';
echo 'login_ctx=' . (epc_cp_login_context()['type'] ?? '') . "\n";

if (isset($_GET['plugins']) && $_GET['plugins'] === '1') {
	try {
		$pdo = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$rows = $pdo->query('SELECT `id`,`name`,`source`,`order` FROM `plugins` WHERE `activated`=1 AND `is_frontend`=0 ORDER BY `order`')->fetchAll(PDO::FETCH_ASSOC);
		echo 'plugins=' . json_encode($rows) . "\n";
	} catch (Exception $e) {
		echo 'plugins_err=' . $e->getMessage() . "\n";
	}
}
