<?php
/**
 * Prices Send AJAX — generate / email customer price lists.
 */
set_time_limit(600);
ini_set('display_errors', '0');
header('Content-Type: application/json; charset=utf-8');
if (ob_get_level()) {
	@ob_end_clean();
}

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	$dbHost = trim((string) $DP_Config->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
	$db_link->query('SET NAMES utf8mb4');
} catch (Throwable $e) {
	http_response_code(503);
	exit(json_encode(array('status' => false, 'message' => 'DB connect error')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'forbidden')));
}

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
	if (function_exists('multilang_init')) {
		multilang_init();
	}
} catch (Throwable $e) {
}

$csrf_check_admin = true;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

try {
	$db_link->exec('SET SESSION SQL_BIG_SELECTS = 1');
} catch (Throwable $e) {
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/prices_send/prices_send_helper.php';

$answer = array('status' => false);
$raw = (string) ($_POST['request_object'] ?? '');
$request_object = json_decode($raw, true);
if (!is_array($request_object)) {
	$request_object = json_decode(urldecode($raw), true);
}
if (!is_array($request_object)) {
	exit(json_encode(array('status' => false, 'message' => 'bad_request')));
}

$action = (string) ($request_object['action'] ?? '');

try {
switch ($action) {
	case 'list_brands':
		$limit = min(50, max(1, (int) ($request_object['limit'] ?? 30)));
		$rows = array();
		$st = $db_link->query(
			'SELECT `manufacturer` AS `brand`, COUNT(*) AS `cnt`
			 FROM `shop_docpart_prices_data`
			 WHERE `manufacturer` <> ""
			 GROUP BY `manufacturer`
			 ORDER BY `cnt` DESC
			 LIMIT ' . (int) $limit
		);
		while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
			$rows[] = array('brand' => (string) $r['brand'], 'count' => (int) $r['cnt']);
		}
		$answer = array('status' => true, 'brands' => $rows);
		break;

	case 'ensure_office_storage_links':
		$office_id = (int) ($request_object['offices'] ?? 0);
		$arr_storages = isset($request_object['arr_storages']) && is_array($request_object['arr_storages']) ? $request_object['arr_storages'] : array();
		$group_ids = isset($request_object['group_ids']) && is_array($request_object['group_ids']) ? $request_object['group_ids'] : array();
		if ($office_id < 1 || empty($arr_storages)) {
			$answer = array('status' => false, 'message' => 'Select shop and storages');
			break;
		}
		if (empty($group_ids)) {
			// Common customer markup profiles + guests
			$group_ids = array(2, 4, 5, 6, 7);
		}
		$ins = $db_link->prepare(
			'INSERT INTO `shop_offices_storages_map`
			 (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`)
			 SELECT ?, ?, ?, 0, 999999999, 0, 0 FROM DUAL
			 WHERE NOT EXISTS (
			 	SELECT 1 FROM `shop_offices_storages_map`
			 	WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ?
			 	AND `min_point` = 0 AND `max_point` = 999999999
			 )'
		);
		$linked = 0;
		foreach ($arr_storages as $sid) {
			$sid = (int) $sid;
			if ($sid < 1) { continue; }
			foreach ($group_ids as $gid) {
				$gid = (int) $gid;
				if ($gid < 1) { continue; }
				$ins->execute(array($office_id, $sid, $gid, $office_id, $sid, $gid));
				$linked += (int) $ins->rowCount();
			}
		}
		$answer = array('status' => true, 'linked' => $linked, 'message' => 'Linked ' . $linked . ' markup map row(s)');
		break;

	case 'send_prices':
		$send_result = true;
		require_once $_SERVER['DOCUMENT_ROOT'] . '/lib/DocpartMailer/docpart_mailer.php';
		$subject = translate_str_by_key($DP_Config->site_name) . ' ' . translate_str_by_key('3660');
		$body = '<p>' . translate_str_by_key('3660') . ' ' . translate_str_by_key($DP_Config->site_name) . ' ' . translate_str_by_key('1711373666_1_5f735d1486aa51eb9a61df1cd635a0fb') . ' ' . date('d-m-Y', time()) . '</p>';
		$new_name_file = 'prices_' . date('d_m_Y', time()) . '.csv';
		$users_list = isset($request_object['users_list']) ? $request_object['users_list'] : array();
		$emails_list = explode(',', (string) ($request_object['emails_list'] ?? ''));
		$group_id_my_list_emails = (int) ($request_object['group_id_my_list_emails'] ?? 0);
		$sent = 0;

		if (is_array($users_list) && !empty($users_list)) {
			foreach ($users_list as $user) {
				$sql = 'SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? LIMIT 1;';
				$query = $db_link->prepare($sql);
				$query->execute(array($user));
				$rov = $query->fetch();
				$group_id = $rov ? (int) $rov['group_id'] : 0;
				$sql = 'SELECT `user_id`, `email` AS `email` FROM `users` WHERE `user_id` = ?';
				$query = $db_link->prepare($sql);
				$query->execute(array($user));
				while ($rov = $query->fetch()) {
					$email = trim((string) $rov['email']);
					if (!empty($group_id) && $email !== '') {
						$file = $_SERVER['DOCUMENT_ROOT'] . '/content/files/Documents/prices_tmp/prices_' . $group_id . '.csv';
						if (is_file($file)) {
							$docpartMailer = new DocpartMailer();
							$docpartMailer->Subject = $subject;
							$docpartMailer->Body = $body;
							$docpartMailer->CharSet = 'UTF-8';
							$docpartMailer->addAddress($email, $email);
							$docpartMailer->addAttachment($file, $new_name_file);
							$docpartMailer->IsSMTP();
							$docpartMailer->IsHTML(true);
							if (!$docpartMailer->Send()) {
								$send_result = false;
							} else {
								$sent++;
							}
						}
					}
				}
			}
		}

		if (!empty($emails_list)) {
			foreach ($emails_list as $email) {
				$email = trim($email);
				$group_id = $group_id_my_list_emails;
				if (!empty($group_id) && $email !== '') {
					$file = $_SERVER['DOCUMENT_ROOT'] . '/content/files/Documents/prices_tmp/prices_' . $group_id . '.csv';
					if (is_file($file)) {
						$docpartMailer = new DocpartMailer();
						$docpartMailer->Subject = $subject;
						$docpartMailer->Body = $body;
						$docpartMailer->CharSet = 'UTF-8';
						$docpartMailer->addAddress($email, $email);
						$docpartMailer->addAttachment($file, $new_name_file);
						$docpartMailer->IsSMTP();
						$docpartMailer->IsHTML(true);
						if (!$docpartMailer->Send()) {
							$send_result = false;
						} else {
							$sent++;
						}
					}
				}
			}
		}

		$answer = array('status' => (bool) $send_result, 'sent' => $sent);
		if (!$send_result) {
			$answer['message'] = 'Some emails failed to send';
		}
		break;

	case 'check_office_storages_map':
		$offices = (int) ($request_object['offices'] ?? 0);
		$arr_storages = isset($request_object['arr_storages']) && is_array($request_object['arr_storages']) ? $request_object['arr_storages'] : array();
		$storages_not_linked = array();
		foreach ($arr_storages as $storage_id) {
			$storage_id = (int) $storage_id;
			$check = $db_link->prepare('SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?;');
			$check->execute(array($offices, $storage_id));
			if ((int) $check->fetchColumn() === 0) {
				$name_q = $db_link->prepare('SELECT `name` FROM `shop_storages` WHERE `id` = ?;');
				$name_q->execute(array($storage_id));
				$rec = $name_q->fetch();
				$storages_not_linked[] = $rec ? (string) $rec['name'] : ('ID ' . $storage_id);
			}
		}
		if (empty($storages_not_linked)) {
			$answer = array('status' => true);
		} else {
			$answer = array('status' => false, 'message' => implode(', ', $storages_not_linked), 'can_link' => true);
		}
		break;

	case 'create_prices':
		$check_result = generate_price($request_object);
		if ($check_result) {
			$answer['status'] = true;
			if (empty($answer['message'])) {
				$rows = isset($answer['rows_total']) ? (int) $answer['rows_total'] : 0;
				$files = isset($answer['files']) && is_array($answer['files']) ? count($answer['files']) : 0;
				$answer['message'] = 'Generated ' . $files . ' file(s), ' . number_format($rows) . ' row(s).';
			}
		} else {
			$answer['status'] = false;
			$answer['message'] = 'No markup profile selected (choose customers, emails+group, or a profile group).';
		}
		break;

	default:
		$answer = array('status' => false, 'message' => 'Unknown action');
}
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('status' => false, 'message' => 'query_failed', 'error' => $e->getMessage())));
}

exit(json_encode($answer, JSON_UNESCAPED_UNICODE));
