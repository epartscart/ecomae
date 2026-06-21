<?php
/**
 * Storefront — render published visual page builder blocks.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_visual_page_editor.php';

$pdo = null;
if (function_exists('epc_portal_platform_pdo')) {
	$pdo = epc_portal_platform_pdo();
}
if (!$pdo instanceof PDO && isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
	$pdo = $GLOBALS['db_link'];
}
if (!$pdo instanceof PDO) {
	return;
}

echo epc_vpe_render_storefront($pdo);
