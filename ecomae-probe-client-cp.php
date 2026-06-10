<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

echo "DB={$DP_Config->db}\n";

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('pdo fail: ' . $e->getMessage() . "\n");
}

require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
$settings = epc_portal_load_site_settings($pdo);
echo 'enabled_packs=' . json_encode($settings['enabled_packs'] ?? array()) . "\n";
echo 'industry=' . ($settings['industry_code'] ?? '') . "\n";

$ci = (int) $pdo->query('SELECT COUNT(*) FROM `control_items`')->fetchColumn();
$cg = (int) $pdo->query('SELECT COUNT(*) FROM `control_groups`')->fetchColumn();
echo "control_items={$ci} control_groups={$cg}\n";

$ac = $pdo->query("SELECT `name`, `activated` FROM `plugins` WHERE `name` LIKE '%access%' OR `name` LIKE '%control%'")->fetchAll(PDO::FETCH_ASSOC);
echo 'plugins=' . json_encode($ac) . "\n";

$row = $pdo->query("SELECT `id`, `url`, `content_type`, `content`, `published_flag` FROM `content` WHERE `url` = 'control/portal/industry_settings' AND `is_frontend` = 0 LIMIT 1")->fetch(PDO::FETCH_ASSOC);
echo 'industry_content=' . json_encode($row) . "\n";

$phpPath = __DIR__ . '/cp/content/control/portal/industry_settings.php';
echo 'php_file=' . (is_file($phpPath) ? 'yes' : 'no') . "\n";

// Simulate is_anable dependency
echo 'getAllowedGroups=' . (function_exists('getAllowedGroups') ? 'defined' : 'MISSING') . "\n";

// Boot CP page fragment
$_SERVER['REQUEST_URI'] = '/cp/control/portal/industry_settings';
$_SERVER['REQUEST_METHOD'] = 'GET';
ob_start();
try {
	require __DIR__ . '/cp/content/control/portal/industry_settings.php';
	$out = ob_get_clean();
	echo 'industry_render_len=' . strlen($out) . "\n";
	echo 'industry_has_form=' . (strpos($out, 'epc-portal-settings-form') !== false ? 'yes' : 'no') . "\n";
} catch (Throwable $e) {
	ob_end_clean();
	echo 'industry_render_error=' . $e->getMessage() . "\n";
}
