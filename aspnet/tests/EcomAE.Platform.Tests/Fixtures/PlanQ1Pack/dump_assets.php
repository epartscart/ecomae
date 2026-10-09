<?php
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace-wt/small-done', '/');
$out = rtrim($argv[2] ?? '/tmp/plan_q1p_assets', '/');
@mkdir($out, 0777, true);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require $root . '/content/general_pages/epc_ecomae_faq_data.php';
file_put_contents($out . '/faq.json', json_encode(epc_ecomae_faq_modules(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
require $root . '/content/general_pages/epc_ecomae_legal_content.php';
file_put_contents($out . '/legal.json', json_encode(array(
	'date' => epc_ecomae_legal_effective_date(),
	'catalog' => epc_ecomae_legal_catalog(),
	'aliases' => epc_ecomae_legal_top_level_aliases(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
require $root . '/content/general_pages/epc_ded_activity_mapping.php';
file_put_contents($out . '/ded.json', json_encode(array(
	'divisions' => epc_ded_divisions(),
	'registries' => epc_worldwide_business_registries(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
echo "ok\n";
