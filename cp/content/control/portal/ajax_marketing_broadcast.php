<?php
/**
 * Marketing Broadcast — AJAX (recipient count, template preview).
 * Self-bootstrapping for /content/general_pages/ajax_epc_marketing_broadcast.php proxy.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
if (!headers_sent()) {
	header('Content-Type: application/json; charset=utf-8');
}

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $docRoot . '/content/general_pages/epc_portal.php';
require_once $docRoot . '/content/general_pages/epc_portal_db.php';
require_once $docRoot . '/content/general_pages/epc_portal_tenant.php';
epc_portal_apply_config($DP_Config);

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'message' => 'DB unavailable'));
	exit;
}

require_once $docRoot . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'message' => 'Forbidden'));
	exit;
}

require_once $docRoot . '/content/shop/marketing/epc_marketing_broadcast_helpers.php';

$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	echo json_encode(array('ok' => false, 'message' => 'DB unavailable'));
	exit;
}

$action = (string) ($_GET['action'] ?? $_POST['action'] ?? '');

if ($action === 'count_recipients') {
	$mode = (string) ($_GET['audience_mode'] ?? 'all');
	$meta = (string) ($_GET['audience_meta'] ?? '');
	$channel = (string) ($_GET['channel'] ?? 'email');
	$count = epc_mb_count_recipients($pdo, $mode, $meta, $channel);
	echo json_encode(array('ok' => true, 'count' => $count));
	exit;
}

if ($action === 'template_preview') {
	$channel = (string) ($_GET['channel'] ?? 'email');
	$key = (string) ($_GET['template_key'] ?? 'blank');
	if ($channel === 'whatsapp') {
		$tpls = epc_mb_whatsapp_templates();
		$tpl = $tpls[$key] ?? $tpls['blank'];
		echo json_encode(array('ok' => true, 'body_text' => (string) ($tpl['body'] ?? '')));
		exit;
	}
	$tpls = epc_mb_email_templates();
	$tpl = $tpls[$key] ?? $tpls['blank'];
	echo json_encode(array(
		'ok' => true,
		'subject' => (string) ($tpl['subject'] ?? ''),
		'preview' => (string) ($tpl['preview'] ?? ''),
		'body_html' => (string) ($tpl['html'] ?? ''),
	));
	exit;
}

echo json_encode(array('ok' => false, 'message' => 'Unknown action'));
