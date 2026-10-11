<?php
// Dump CSS bodies and catalog JSON for PlanQ1Left C# consts.
// Usage: php dump_assets.php /workspace-wt/small-done /tmp/plan_q1l_assets
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace', '/');
$out = rtrim($argv[2] ?? '/tmp/plan_q1l_assets', '/');
@mkdir($out, 0777, true);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
$_SERVER['DOCUMENT_ROOT'] = $root;

$cssFiles = array(
	'inthub' => 'epc_integrations_hub_css.php',
	'inds' => 'epc_industry_settings_css.php',
	'mb' => 'epc_marketing_broadcast_css.php',
);
foreach ($cssFiles as $key => $php) {
	ob_start();
	require $root . '/content/general_pages/' . $php;
	file_put_contents($out . '/' . $key . '.css', ob_get_clean());
}

$brochure = require $root . '/content/general_pages/epc_cp_brochure_inventory.php';
file_put_contents($out . '/brochure.json', json_encode($brochure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
$guides = require $root . '/content/general_pages/epc_ecomae_platform_capability_guides.php';
file_put_contents($out . '/guides.json', json_encode($guides, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
echo "ok\n";
