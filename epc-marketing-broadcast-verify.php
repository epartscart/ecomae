<?php
/**
 * Marketing Broadcast — post-deploy verification probe.
 * https://www.ecomae.com/epc-marketing-broadcast-verify.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/marketing/epc_marketing_broadcast_helpers.php';
require_once __DIR__ . '/content/shop/marketing/epc_marketing_broadcast_templates.php';

$cfg = new DP_Config();
$checks = array();

$files = array(
	'content/shop/marketing/epc_marketing_broadcast_helpers.php',
	'content/shop/marketing/epc_marketing_broadcast_templates.php',
	'content/general_pages/epc_marketing_broadcast_css.php',
	'content/general_pages/epc_marketing_broadcast_js.php',
	'content/general_pages/epc_marketing_broadcast_config.php',
	'cp/content/control/portal/epc_marketing_broadcast.php',
	'cp/content/control/portal/epc_marketing_broadcast_panel.php',
	'cp/content/control/portal/epc_marketing_broadcast.js',
	'cp/content/control/portal/ajax_marketing_broadcast.php',
	'content/general_pages/ajax_epc_marketing_broadcast.php',
	'epc-marketing-broadcast-setup-all.php',
	'epc-marketing-broadcast-render-probe.php',
);
foreach ($files as $f) {
	$checks['file:' . $f] = is_file(__DIR__ . '/' . $f);
}

$checks['fn:epc_mb_email_templates'] = function_exists('epc_mb_email_templates') && count(epc_mb_email_templates()) >= 3;
$checks['fn:epc_mb_whatsapp_templates'] = function_exists('epc_mb_whatsapp_templates') && count(epc_mb_whatsapp_templates()) >= 3;

try {
	$host = trim((string) $cfg->host);
	if ($host === '' || strtolower($host) === 'localhost') {
		$host = '127.0.0.1';
	}
	$pdo = new PDO(
		'mysql:host=' . $host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_mb_ensure_schema($pdo);
	$checks['schema_campaigns'] = (bool) $pdo->query("SHOW TABLES LIKE 'epc_marketing_broadcast_campaigns'")->fetch();
	$checks['schema_log'] = (bool) $pdo->query("SHOW TABLES LIKE 'epc_marketing_broadcast_log'")->fetch();
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array('control/portal/epc_marketing_broadcast'));
	$checks['cp_content_route'] = (int) $st->fetchColumn() > 0;
	$gst = $pdo->prepare('SELECT `id` FROM `control_groups` WHERE `caption` = ? LIMIT 1');
	$gst->execute(array('epc_cp_group_portal'));
	$portalGroupId = (int) $gst->fetchColumn();
	$menuSt = $pdo->prepare(
		'SELECT `id` FROM `control_items` WHERE `items_group` = ? AND `caption` = ? LIMIT 1'
	);
	$menuSt->execute(array($portalGroupId, 'epc_marketing_broadcast_cp'));
	$checks['cp_menu_item'] = (int) $menuSt->fetchColumn() > 0;
} catch (Throwable $e) {
	$checks['db_error'] = false;
}

$configPath = __DIR__ . '/content/general_pages/epc_marketing_broadcast_config.php';
$configSrc = (string) @file_get_contents($configPath);
$checks['config_bootstrap'] = strpos($configSrc, "define('_ASTEXE_', 1)") !== false
	&& strpos($configSrc, "die('No access')") === false
	&& strpos($configSrc, '/content/general_pages/ajax_epc_marketing_broadcast.php') !== false;
$checks['ajax_proxy_on_disk'] = is_file(__DIR__ . '/content/general_pages/ajax_epc_marketing_broadcast.php')
	&& filesize(__DIR__ . '/content/general_pages/ajax_epc_marketing_broadcast.php') > 0;

$host = function_exists('epc_portal_host') ? (string) epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');
if ($host === '') {
	$host = 'www.ecomae.com';
}
$probeCtx = stream_context_create(array('http' => array('timeout' => 15, 'ignore_errors' => true)));
$configProbeUrl = 'https://' . $host . '/content/general_pages/epc_marketing_broadcast_config.php';
$configProbeRaw = @file_get_contents($configProbeUrl, false, $probeCtx);
$checks['config_proxy_http'] = is_string($configProbeRaw)
	&& strpos($configProbeRaw, 'window.EPC_MB') !== false
	&& strpos($configProbeRaw, 'No access') === false
	&& strpos($configProbeRaw, '/content/general_pages/ajax_epc_marketing_broadcast.php') !== false;

$ajaxProbeHeaders = @get_headers('https://' . $host . '/content/general_pages/ajax_epc_marketing_broadcast.php');
$ajaxProbeHttp = 0;
if (is_array($ajaxProbeHeaders) && isset($ajaxProbeHeaders[0]) && preg_match('/\s(\d{3})\s/', (string) $ajaxProbeHeaders[0], $m)) {
	$ajaxProbeHttp = (int) $m[1];
}
$checks['ajax_proxy_http'] = in_array($ajaxProbeHttp, array(200, 403), true);

$pass = 0;
$fail = 0;
echo "=== Marketing Broadcast Verify ===\n\n";
foreach ($checks as $key => $ok) {
	$status = $ok ? 'PASS' : 'FAIL';
	if ($ok) {
		$pass++;
	} else {
		$fail++;
	}
	echo sprintf("%-40s %s\n", $key, $status);
}

echo "\nSummary: {$pass} passed, {$fail} failed\n";
echo "Hub URL: /cp/control/portal/epc_marketing_broadcast\n";
echo "Guide:   /cp/control/portal/epc_marketing_broadcast?tab=guide\n";
echo "Menu:    Portal → Marketing broadcast\n";

if ($fail > 0) {
	echo "\nRun setup: epc-marketing-broadcast-setup-all.php?token=...&apply=1\n";
	exit(1);
}
exit(0);
