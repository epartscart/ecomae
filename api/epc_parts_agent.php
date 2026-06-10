<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once dirname(__DIR__) . '/config.php';
require_once dirname(__DIR__) . '/content/shop/docpart/epc_parts_agent.php';

$DP_Config = new DP_Config();

if (!epc_agent_enabled($DP_Config)) {
	echo json_encode(array('ok' => false, 'message' => 'Agent disabled'), JSON_UNESCAPED_UNICODE);
	exit;
}

$action = isset($_REQUEST['action']) ? trim((string)$_REQUEST['action']) : 'chat';

if ($action === 'bootstrap') {
	echo json_encode(epc_agent_bootstrap($DP_Config), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password
	);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE);
	exit;
}

$session_id = isset($_REQUEST['session_id']) ? trim((string)$_REQUEST['session_id']) : '';
if ($session_id === '') {
	$session_id = 's' . bin2hex(random_bytes(12));
}

$session = epc_agent_session_load($session_id);

if ($action === 'history') {
	$req_session = isset($_REQUEST['session_id']) ? trim((string)$_REQUEST['session_id']) : '';
	if ($req_session === '') {
		echo json_encode(array('ok' => false, 'message' => 'Missing session_id'), JSON_UNESCAPED_UNICODE);
		exit;
	}
	$history = epc_agent_get_session_history($db, $req_session);
	echo json_encode(array_merge(array('ok' => $history['ok']), $history), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

if ($action === 'chat') {
	$message = isset($_POST['message']) ? trim((string)$_POST['message']) : '';
	if ($message === '' && isset($_REQUEST['message'])) {
		$message = trim((string)$_REQUEST['message']);
	}
	$quick = isset($_REQUEST['quick']) ? trim((string)$_REQUEST['quick']) : '';
	if ($message === '' && $quick !== '') {
		$message = epc_agent_quick_action_message($quick);
	}

	if (count($session['messages']) > 80) {
		echo json_encode(array('ok' => false, 'message' => 'Session limit reached. Please refresh the page.'), JSON_UNESCAPED_UNICODE);
		exit;
	}

	$reply = epc_agent_handle_message($db, $DP_Config, $message, $session);

	if ($message !== '') {
		$session['messages'][] = array('role' => 'user', 'text' => $message, 't' => time());
	}
	$agent_msg = array('role' => 'agent', 'text' => $reply['text'], 't' => time());
	if (!empty($reply['links']) && is_array($reply['links'])) {
		$agent_msg['links'] = $reply['links'];
	}
	$session['messages'][] = $agent_msg;

	$meta = array(
		'ip_hash' => epc_agent_request_ip_hash(),
		'client_ip' => epc_agent_request_client_ip(),
		'user_agent' => isset($_SERVER['HTTP_USER_AGENT']) ? (string)$_SERVER['HTTP_USER_AGENT'] : '',
	);
	$geo = epc_agent_request_ip_geo();
	if (!empty($_REQUEST['client_country_code'])) {
		$geo['code'] = strtoupper(substr(trim((string)$_REQUEST['client_country_code']), 0, 8));
		$geo['name'] = epc_agent_iso_country_name($geo['code']);
	}
	if (!empty($_REQUEST['client_country_name'])) {
		$geo['name'] = trim((string)$_REQUEST['client_country_name']);
	}
	$meta['ip_country_code'] = (string)$geo['code'];
	$meta['ip_country_name'] = (string)$geo['name'];
	if ($meta['ip_country_code'] === '' && $meta['client_ip'] !== '') {
		$lookup = epc_agent_lookup_ip_country($meta['client_ip']);
		$meta['ip_country_code'] = (string)$lookup['code'];
		$meta['ip_country_name'] = (string)$lookup['name'];
	}
	if ($meta['client_ip'] !== '') {
		$session['client_ip'] = $meta['client_ip'];
	}
	if ($meta['ip_country_code'] !== '') {
		$session['ip_country_code'] = $meta['ip_country_code'];
	}
	if ($meta['ip_country_name'] !== '') {
		$session['ip_country_name'] = $meta['ip_country_name'];
	}
	epc_agent_session_save($session_id, $session);
	if (is_file(dirname(__DIR__) . '/content/users/dp_user.php')) {
		require_once dirname(__DIR__) . '/content/users/dp_user.php';
		if (class_exists('DP_User')) {
			$meta['user_id'] = (int)DP_User::getUserId();
		}
	}
	try {
		epc_agent_persist_turn($db, $session_id, $session, $message, $reply, $meta);
	} catch (Throwable $e) {
		// Chat must succeed even if audit logging fails.
	}

	echo json_encode(array(
		'ok' => true,
		'session_id' => $session_id,
		'reply' => $reply,
		'country_code' => (string)($session['country_code'] ?? ''),
		'country_name' => (string)($session['country_name'] ?? ''),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

echo json_encode(array('ok' => false, 'message' => 'Unknown action'), JSON_UNESCAPED_UNICODE);
