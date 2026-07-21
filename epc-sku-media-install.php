<?php
/**
 * One-shot installer for SKU photos & specs CP route.
 * Usage: /epc-sku-media-install.php?token=...&apply=1
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/catalogue/epc_sku_media_cp_install.php';

$apply = !empty($_GET['apply']);
/** @var PDO $db_link */
global $db_link, $DP_Config;
$backend = isset($DP_Config) && is_object($DP_Config) ? (string) ($DP_Config->backend_dir ?? 'cp') : 'cp';
epc_sku_media_ensure_schema($db_link);
$result = epc_sku_media_cp_install($db_link, $backend, $apply);
echo "schema=ok\n";
echo 'apply=' . ($apply ? '1' : '0') . "\n";
echo 'content_id=' . (int) $result['content_id'] . "\n";
echo 'menu_item_id=' . (int) $result['menu_item_id'] . "\n";
if ($apply) {
	echo "Installed CP route: /{$backend}/shop/catalogue/sku_media\n";
} else {
	echo "Pass apply=1 to register CP content + menu.\n";
}
