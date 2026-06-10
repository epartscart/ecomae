<?php
/**
 * Promote Channels, ERP Finance, and AI Agent to top-level CP sidebar groups (not under Shop).
 * Run: https://www.epartscart.com/epc-cp-mainstream-menu-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$result = epc_cp_mainstream_menu_apply($pdo);

$groups = array();
$gq = $pdo->query('SELECT `id`, `caption`, `order` FROM `control_groups` ORDER BY `order` ASC');
while ($g = $gq->fetch(PDO::FETCH_ASSOC)) {
	$groups[] = array(
		'id' => (int)$g['id'],
		'caption' => $g['caption'],
		'order' => (int)$g['order'],
	);
}

$base = rtrim($cfg->domain_path, '/');
$be = $cfg->backend_dir;

echo json_encode(array(
	'status' => true,
	'message' => 'Top-level CP menu groups configured (Channels, ERP, AI Agent)',
	'groups' => $result,
	'all_control_groups' => $groups,
	'urls' => array(
		'channels' => $base . '/' . $be . '/shop/channels/channels',
		'channels_guide' => $base . '/' . $be . '/shop/channels/guide',
		'erp' => $base . '/' . $be . '/shop/finance/erp',
		'erp_guide' => $base . '/' . $be . '/shop/finance/erp/guide',
		'ai_agent' => $base . '/' . $be . '/shop/parts_agent_chats',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
