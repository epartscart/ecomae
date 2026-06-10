<?php
header('Content-Type: application/json;charset=utf-8;');
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8', $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}
$db_link->query('SET NAMES utf8;');
$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_parts_agent.php';

$cp_page_url = 'shop/parts_agent_chats';
$content_id = 0;
try {
	$content_stmt = $db_link->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$content_stmt->execute(array($cp_page_url));
	$content_id = (int)$content_stmt->fetchColumn();
} catch (Exception $e) {
}

if ($content_id <= 0) {
	exit(epc_agent_cp_json_encode(array('status' => false, 'message' => 'CP page not registered. Run epc-parts-agent-cp-setup.php')));
}

$pages_to_check = array();
$pages_to_check[] = array('id' => $content_id, 'url' => $cp_page_url);
require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/check_admin_access/check_admin_access.php';
$csrf_check_admin = true;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
if (!DP_User::isAdmin()) {
	exit(epc_agent_cp_json_encode(array('status' => false, 'message' => 'Access denied')));
}

$action = isset($_REQUEST['action']) ? (string)$_REQUEST['action'] : 'list';

try {
	if ($action === 'sync') {
		$synced = epc_agent_cp_sync_file_sessions($db_link, 120);
		exit(epc_agent_cp_json_encode(array('status' => true, 'synced' => $synced)));
	}

	if ($action === 'stats') {
		$stats = epc_agent_cp_stats($db_link);
		exit(epc_agent_cp_json_encode(array('status' => true, 'stats' => $stats)));
	}

	if ($action === 'get_config') {
		$config = epc_agent_load_config($DP_Config, $db_link);
		$branding = epc_agent_widget_branding($DP_Config);
		exit(epc_agent_cp_json_encode(array(
			'status' => true,
			'config' => $config,
			'storefront_toggle_operator_note' => 'Temporary price list toggles affect what the agent can quote to customers. The agent will not disclose disabled lists or admin controls.',
			'storefront_toggle_internal_prompt' => epc_agent_storefront_toggle_internal_prompt(),
			'defaults' => array(
				'agent_name' => (string) ($branding['agent_name'] ?? ''),
				'subtitle' => (string) ($branding['subtitle'] ?? ''),
				'greeting' => (string) ($branding['greeting'] ?? ''),
				'teaser_text' => (string) ($branding['teaser_text'] ?? ''),
				'placeholder' => (string) ($branding['placeholder'] ?? ''),
				'logo_url' => (string) ($branding['logo_url'] ?? ''),
				'domain' => (string) ($branding['domain'] ?? ''),
			),
		)));
	}

	if ($action === 'save_config') {
		$config = epc_agent_save_config($db_link, array(
			'enabled' => isset($_REQUEST['enabled']) ? (int) $_REQUEST['enabled'] : 0,
			'agent_name' => isset($_REQUEST['agent_name']) ? (string) $_REQUEST['agent_name'] : '',
			'subtitle' => isset($_REQUEST['subtitle']) ? (string) $_REQUEST['subtitle'] : '',
			'greeting' => isset($_REQUEST['greeting']) ? (string) $_REQUEST['greeting'] : '',
			'system_prompt' => isset($_REQUEST['system_prompt']) ? (string) $_REQUEST['system_prompt'] : '',
			'teaser_text' => isset($_REQUEST['teaser_text']) ? (string) $_REQUEST['teaser_text'] : '',
			'placeholder' => isset($_REQUEST['placeholder']) ? (string) $_REQUEST['placeholder'] : '',
			'logo_url' => isset($_REQUEST['logo_url']) ? (string) $_REQUEST['logo_url'] : '',
			'domain' => isset($_REQUEST['domain']) ? (string) $_REQUEST['domain'] : '',
		));
		exit(epc_agent_cp_json_encode(array('status' => true, 'config' => $config)));
	}

	if ($action === 'detail') {
		$session_id = isset($_REQUEST['session_id']) ? (string)$_REQUEST['session_id'] : '';
		$detail = epc_agent_cp_get_session($db_link, $session_id);
		if (!$detail['session']) {
			exit(epc_agent_cp_json_encode(array('status' => false, 'message' => 'Session not found')));
		}
		exit(epc_agent_cp_json_encode(array('status' => true, 'detail' => $detail)));
	}

	$filters = array();
	if (!empty($_REQUEST['q'])) {
		$filters['q'] = trim((string)$_REQUEST['q']);
	}
	if (!empty($_REQUEST['date_from'])) {
		$ts = strtotime((string)$_REQUEST['date_from'] . ' 00:00:00');
		if ($ts) {
			$filters['date_from'] = $ts;
		}
	}
	if (!empty($_REQUEST['date_to'])) {
		$ts = strtotime((string)$_REQUEST['date_to'] . ' 23:59:59');
		if ($ts) {
			$filters['date_to'] = $ts;
		}
	}

	$limit = isset($_REQUEST['limit']) ? (int)$_REQUEST['limit'] : 50;
	$offset = isset($_REQUEST['offset']) ? (int)$_REQUEST['offset'] : 0;

	if ($action === 'list') {
		$result = epc_agent_cp_list_sessions($db_link, $filters, $limit, $offset);
		exit(epc_agent_cp_json_encode(array(
			'status' => true,
			'sessions' => $result['sessions'],
			'total' => $result['total'],
			'limit' => max(1, min(200, $limit)),
			'offset' => max(0, $offset),
		)));
	}

	exit(epc_agent_cp_json_encode(array('status' => false, 'message' => 'Unknown action')));
} catch (Throwable $e) {
	exit(epc_agent_cp_json_encode(array(
		'status' => false,
		'message' => 'Server error: ' . $e->getMessage(),
	)));
}
