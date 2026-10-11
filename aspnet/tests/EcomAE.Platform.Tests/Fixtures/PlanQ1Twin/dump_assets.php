<?php
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace', '/');
$out = rtrim($argv[2] ?? '/tmp/plan_q1t_assets', '/');
@mkdir($out, 0777, true);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require $root . '/content/general_pages/epc_import_orchestrator.php';
require $root . '/content/general_pages/epc_bi_metrics.php';
require $root . '/content/general_pages/epc_notifications.php';
file_put_contents($out . '/import.json', json_encode(array(
	'schemas' => epc_import_entity_schemas(),
	'formats' => epc_import_supported_formats(),
	'sources' => epc_import_supported_sources(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
file_put_contents($out . '/bi.json', json_encode(epc_bi_builtin_metrics(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
file_put_contents($out . '/ntf.json', json_encode(epc_notification_categories(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
echo "ok\n";
