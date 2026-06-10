<?php
/**
 * Admin + CRM notification recipients (admin always receives all).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';

/** Primary admin inbox for all storefront alerts. */
function epc_admin_notify_email(): string
{
	global $DP_Config;
	if (is_object($DP_Config)) {
		$ctx = epc_site_context($DP_Config);
		if (!empty($ctx['admin_email'])) {
			return (string) $ctx['admin_email'];
		}
	}
	$email = '';
	if (is_object($DP_Config) && !empty($DP_Config->from_email)) {
		$email = trim((string) $DP_Config->from_email);
	}
	if ($email === '' || stripos($email, 'noreply') !== false) {
		$email = 'admin@' . preg_replace('/^www\./', '', epc_portal_host());
	}
	return $email;
}

/**
 * @return array<int, array<string, mixed>>
 */
function epc_admin_notify_persons_direct(): array
{
	return array(
		array(
			'type' => 'direct_contact',
			'contacts' => array(
				'email' => array('value' => epc_admin_notify_email()),
			),
		),
	);
}

/**
 * Assigned relationship manager (internal user) for a customer.
 */
function epc_crm_user_id_for_customer(int $customer_id): int
{
	global $db_link;
	if ($customer_id <= 0 || !isset($db_link) || !$db_link) {
		return 0;
	}
	$keys = array('epc_crm_user_id', 'epc_relationship_manager', 'relationship_manager_id', 'account_manager_id');
	$placeholders = implode(',', array_fill(0, count($keys), '?'));
	try {
		$orderField = "FIELD(`data_key`, '" . implode("','", array_map('addslashes', $keys)) . "')";
		$stmt = $db_link->prepare(
			'SELECT `data_value` FROM `users_profiles`
			 WHERE `user_id` = ? AND `data_key` IN (' . $placeholders . ')
			 ORDER BY ' . $orderField . ' LIMIT 1'
		);
		$stmt->execute(array_merge(array($customer_id), $keys));
		$val = (int)$stmt->fetchColumn();
		return $val > 0 ? $val : 0;
	} catch (Throwable $e) {
		return 0;
	}
}

/**
 * @return array<int, array<string, mixed>>
 */
function epc_crm_persons_for_customer(int $customer_id): array
{
	$crm_id = epc_crm_user_id_for_customer($customer_id);
	if ($crm_id <= 0) {
		return array();
	}
	return array(array('type' => 'user_id', 'user_id' => $crm_id));
}

/**
 * @return array<int, array<string, mixed>>
 */
function epc_office_manager_persons(int $office_id): array
{
	global $db_link;
	$persons = array();
	if ($office_id <= 0 || !isset($db_link) || !$db_link) {
		return $persons;
	}
	if (!class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	}
	try {
		$stmt = $db_link->prepare('SELECT `users` FROM `shop_offices` WHERE `id` = ? LIMIT 1');
		$stmt->execute(array($office_id));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if (!$row || empty($row['users'])) {
			return $persons;
		}
		$list = json_decode((string)$row['users'], true);
		if (!is_array($list)) {
			return $persons;
		}
		foreach ($list as $uid) {
			$uid = (int)$uid;
			if ($uid <= 0) {
				continue;
			}
			if (method_exists('DP_User', 'isBackendGroupById') && !DP_User::isBackendGroupById($uid)) {
				continue;
			}
			$persons[] = array('type' => 'user_id', 'user_id' => $uid);
		}
	} catch (Throwable $e) {
	}
	return $persons;
}

/**
 * Merge recipient lists; admin direct email is always included.
 *
 * @param array<int, array<string, mixed>> ...$lists
 * @return array<int, array<string, mixed>>
 */
function epc_notify_merge_persons(...$lists): array
{
	$out = array();
	$seen_users = array();
	$admin_email = strtolower(epc_admin_notify_email());

	foreach (epc_admin_notify_persons_direct() as $p) {
		$out[] = $p;
	}

	foreach ($lists as $list) {
		if (!is_array($list)) {
			continue;
		}
		foreach ($list as $p) {
			if (!is_array($p) || empty($p['type'])) {
				continue;
			}
			if ($p['type'] === 'user_id') {
				$uid = (int)($p['user_id'] ?? 0);
				if ($uid <= 0 || isset($seen_users[$uid])) {
					continue;
				}
				$seen_users[$uid] = true;
				$out[] = $p;
				continue;
			}
			if ($p['type'] === 'direct_contact' && !empty($p['contacts']['email']['value'])) {
				$em = strtolower(trim((string)$p['contacts']['email']['value']));
				if ($em !== '' && $em !== $admin_email) {
					$out[] = $p;
				}
			}
		}
	}

	return $out;
}

/**
 * Staff recipients: admin (always) + CRM for customer + office managers.
 *
 * @return array<int, array<string, mixed>>
 */
function epc_staff_notify_persons(int $customer_id, int $office_id = 0, array $extra = array()): array
{
	return epc_notify_merge_persons(
		epc_crm_persons_for_customer($customer_id),
		epc_office_manager_persons($office_id),
		$extra
	);
}

/**
 * Send notification to staff (admin always included).
 */
function epc_staff_send_notify(string $notify_name, array $notify_vars, int $customer_id = 0, int $office_id = 0, array $extra_persons = array(), bool $wait = true): void
{
	if (!function_exists('send_notify')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/notify_helper.php';
	}
	$persons = epc_staff_notify_persons($customer_id, $office_id, $extra_persons);
	if (empty($persons)) {
		$persons = epc_admin_notify_persons_direct();
	}
	send_notify($notify_name, $notify_vars, $persons, $wait);
}

/**
 * @return array<string, mixed>|null
 */
function epc_notify_last_answer(): ?array
{
	global $epc_notify_last_answer;
	return is_array($epc_notify_last_answer ?? null) ? $epc_notify_last_answer : null;
}

function epc_notify_store_answer($answer): void
{
	global $epc_notify_last_answer;
	if (is_array($answer)) {
		$epc_notify_last_answer = $answer;
	}
}

function epc_notify_email_status(?array $answer, string $match = ''): ?bool
{
	if (!is_array($answer) || empty($answer['persons'])) {
		return null;
	}
	$match = strtolower(trim($match));
	foreach ($answer['persons'] as $person) {
		if (!is_array($person) || empty($person['contacts']['email']['tried_to_send'])) {
			continue;
		}
		if ($match !== '') {
			if (($person['type'] ?? '') === 'direct_contact') {
				$email = strtolower(trim((string)($person['contacts']['email']['value'] ?? '')));
				if ($email !== $match) {
					continue;
				}
			} elseif (($person['type'] ?? '') === 'user_id' && (string)($person['user_id'] ?? '') !== $match) {
				continue;
			}
		}
		return !empty($person['contacts']['email']['status']);
	}
	return null;
}

function epc_log_order_notification($db_link, int $order_id, string $text): void
{
	if ($order_id <= 0 || !isset($db_link) || !$db_link) {
		return;
	}
	try {
		$db_link->prepare(
			'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?, ?, ?, ?, ?, ?);'
		)->execute(array($order_id, time(), 0, 0, $text, 1));
	} catch (Throwable $e) {
	}
}

/**
 * Send new-order emails at checkout with retry + order log (admin + customer).
 */
function epc_checkout_send_order_notifications(
	$db_link,
	int $order_id,
	int $user_id,
	int $office_id,
	string $email_not_auth = '',
	string $phone_not_auth = ''
): void {
	if ($order_id <= 0) {
		return;
	}
	if (!function_exists('send_notify')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/notify_helper.php';
	}

	$order_text = '';
	include $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_manager.php';
	$manager_vars = array('order_id' => $order_id, 'order_text' => $order_text);

	$staff_answer = null;
	if (function_exists('epc_staff_notify_persons')) {
		$persons = epc_staff_notify_persons($user_id, $office_id);
		if (empty($persons)) {
			$persons = epc_admin_notify_persons_direct();
		}
		$staff_answer = send_notify('new_order_to_manager', $manager_vars, $persons, true);
		epc_notify_store_answer($staff_answer);
	} else {
		epc_staff_send_notify('new_order_to_manager', $manager_vars, $user_id, $office_id, array(), true);
		$staff_answer = epc_notify_last_answer();
	}

	$admin_email = strtolower(epc_admin_notify_email());
	$admin_ok = epc_notify_email_status($staff_answer, $admin_email);
	if ($admin_ok !== true) {
		$retry = send_notify('new_order_to_manager', $manager_vars, epc_admin_notify_persons_direct(), true);
		epc_notify_store_answer($retry);
		$admin_ok = epc_notify_email_status($retry, $admin_email);
		epc_log_order_notification(
			$db_link,
			$order_id,
			'Order email to admin ' . epc_admin_notify_email() . ': ' . ($admin_ok ? 'sent (retry)' : 'FAILED after retry')
		);
	} else {
		epc_log_order_notification($db_link, $order_id, 'Order email to admin ' . epc_admin_notify_email() . ': sent');
	}

	$customer_persons = array();
	if ($user_id > 0) {
		$customer_persons[] = array('type' => 'user_id', 'user_id' => $user_id);
	} else {
		$customer_persons[] = array(
			'type' => 'direct_contact',
			'contacts' => array(
				'email' => array('value' => htmlentities($email_not_auth)),
				'phone' => array('value' => htmlentities($phone_not_auth)),
			),
		);
	}

	$order_text = '';
	include $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_user.php';
	$msgQ = $db_link->prepare(
		'SELECT `text` FROM `shop_orders_messages` WHERE `order_id` = ? AND `is_customer` = 1 ORDER BY `id` ASC LIMIT 1'
	);
	$msgQ->execute(array($order_id));
	$order_message = $msgQ->fetch(PDO::FETCH_ASSOC);
	if (!empty($order_message['text'])) {
		$order_text .= '<h4>' . translate_str_by_id(4509) . '</h4>';
		$order_text .= '<div style="font-family: Calibri; font-size: 14px;">' . str_replace("\n", '<br/>', $order_message['text']) . '</div>';
	}
	$customer_vars = array('order_id' => $order_id, 'order_text' => $order_text);

	$customer_answer = send_notify('new_order_to_user', $customer_vars, $customer_persons, true);
	epc_notify_store_answer($customer_answer);
	if ($user_id > 0) {
		$customer_ok = epc_notify_email_status($customer_answer, (string)$user_id);
	} else {
		$customer_ok = epc_notify_email_status($customer_answer, strtolower(trim($email_not_auth)));
	}
	if ($customer_ok !== true) {
		$retry = send_notify('new_order_to_user', $customer_vars, $customer_persons, true);
		if ($user_id > 0) {
			$customer_ok = epc_notify_email_status($retry, (string)$user_id);
		} else {
			$customer_ok = epc_notify_email_status($retry, strtolower(trim($email_not_auth)));
		}
	}

	$customer_label = $user_id > 0 ? ('user #' . $user_id) : trim($email_not_auth);
	epc_log_order_notification(
		$db_link,
		$order_id,
		'Order email to customer (' . $customer_label . '): ' . ($customer_ok ? 'sent' : 'FAILED')
	);

	if (function_exists('epc_send_supplier_lpo_notifications')) {
		epc_send_supplier_lpo_notifications($db_link, $order_id);
	} else {
		$epc_supplier_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_supplier_notifications.php';
		if (is_readable($epc_supplier_path)) {
			require_once $epc_supplier_path;
			epc_send_supplier_lpo_notifications($db_link, $order_id);
		}
	}
}

/**
 * HTML block: customer profile for emails (Name, Company, TIN, etc.).
 */
function epc_build_customer_profile_html(int $customer_id, ?array $order = null): string
{
	global $db_link;
	if (!class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	}

	$profile = ($customer_id > 0) ? DP_User::getUserProfileById($customer_id) : array();
	if ($customer_id === 0 && is_array($order)) {
		$profile['email'] = $order['email_not_auth'] ?? '';
		$profile['phone'] = $order['phone_not_auth'] ?? '';
	}

	$labelMap = array(
		'name' => 'Name',
		'surname' => 'Surname',
		'company' => 'Company',
		'inn' => 'TIN',
		'tin' => 'TIN',
		'vat' => 'TIN',
		'city' => 'Client city',
		'address' => 'Address',
	);

	$rows = array();
	$push = static function ($label, $value) use (&$rows) {
		$value = trim((string)$value);
		if ($value === '') {
			return;
		}
		$rows[] = array('label' => $label, 'value' => $value);
	};

	$name = trim((string)($profile['name'] ?? '') . ' ' . (string)($profile['surname'] ?? ''));
	if ($name === '' && !empty($profile['company'])) {
		$name = (string)$profile['company'];
	}
	$push('Name', $name);
	$push('Company', $profile['company'] ?? '');

	foreach (array('inn', 'tin', 'vat', 'tax_id', 'tax_number') as $tinKey) {
		if (!empty($profile[$tinKey])) {
			$push('TIN', $profile[$tinKey]);
			break;
		}
	}

	$email = $profile['email'] ?? '';
	if ($email !== '') {
		$rows[] = array(
			'label' => 'Email',
			'value' => '<a href="mailto:' . htmlspecialchars($email, ENT_QUOTES, 'UTF-8') . '">' . htmlspecialchars($email, ENT_QUOTES, 'UTF-8') . '</a>',
		);
	}

	if (!empty($profile['groups'][0]) && isset($db_link)) {
		try {
			$gq = $db_link->prepare('SELECT `value` FROM `groups` WHERE `id` = ? LIMIT 1');
			$gq->execute(array((int)$profile['groups'][0]));
			$gval = (string)$gq->fetchColumn();
			if ($gval !== '') {
				$push('Profile', function_exists('translate_str_by_id') ? translate_str_by_id($gval) : $gval);
			}
		} catch (Throwable $e) {
		}
	}

	$push('Client city', $profile['city'] ?? '');

	if (isset($db_link)) {
		try {
			$rf = $db_link->query('SELECT `name`, `caption` FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC');
			while ($rf && ($f = $rf->fetch(PDO::FETCH_ASSOC))) {
				$key = (string)($f['name'] ?? '');
				if ($key === '' || isset($labelMap[$key])) {
					continue;
				}
				if (!empty($profile[$key])) {
					$cap = function_exists('translate_str_by_id') ? translate_str_by_id($f['caption']) : $key;
					$push($cap, $profile[$key]);
				}
			}
		} catch (Throwable $e) {
		}
	}

	if (!empty($profile['phone'])) {
		$push('Phone', $profile['phone']);
	}

	$html = '<h4 style="font-family:Calibri,Arial,sans-serif;margin:18px 0 8px;">Information on the client who placed the order:</h4>';
	$html .= '<table style="font-family:Calibri,Arial,sans-serif;font-size:14px;border-collapse:collapse;width:100%;max-width:640px;">';
	foreach ($rows as $r) {
		$html .= '<tr><td style="padding:4px 12px 4px 0;font-weight:bold;vertical-align:top;width:180px;">'
			. htmlspecialchars($r['label'], ENT_QUOTES, 'UTF-8') . ':</td><td style="padding:4px 0;">' . $r['value'] . '</td></tr>';
	}
	if ($customer_id > 0) {
		global $DP_Config;
		$backend = is_object($DP_Config) ? $DP_Config->backend_dir : 'cp';
		$domain = is_object($DP_Config) ? rtrim($DP_Config->domain_path, '/') : '';
		$html .= '<tr><td colspan="2" style="padding-top:10px;"><a style="background:#2b78d6;color:#fff;text-decoration:none;padding:6px 12px;border-radius:4px;display:inline-block;" href="'
			. htmlspecialchars($domain . '/' . $backend . '/users/usermanager/user?user_id=' . $customer_id, ENT_QUOTES, 'UTF-8')
			. '">Open customer in Control Panel</a></td></tr>';
	}
	$html .= '</table>';
	return $html;
}

/**
 * Login / registration alert body.
 */
function epc_build_auth_event_html(string $event, int $user_id, string $contact = '', string $extra = ''): string
{
	$ip = isset($_SERVER['REMOTE_ADDR']) ? (string)$_SERVER['REMOTE_ADDR'] : '';
	$ua = isset($_SERVER['HTTP_USER_AGENT']) ? (string)$_SERVER['HTTP_USER_AGENT'] : '';
	$html = '<div style="font-family:Calibri,Arial,sans-serif;font-size:14px;">';
	$html .= '<p><strong>' . htmlspecialchars($event, ENT_QUOTES, 'UTF-8') . '</strong></p>';
	if ($contact !== '') {
		$html .= '<p>Contact: ' . htmlspecialchars($contact, ENT_QUOTES, 'UTF-8') . '</p>';
	}
	$html .= '<p>User ID: ' . (int)$user_id . '</p>';
	$html .= '<p>Time: ' . date('d.m.Y H:i') . ' (server)</p>';
	if ($ip !== '') {
		$html .= '<p>IP: ' . htmlspecialchars($ip, ENT_QUOTES, 'UTF-8') . '</p>';
	}
	if ($ua !== '') {
		$html .= '<p style="font-size:12px;color:#666;">Browser: ' . htmlspecialchars($ua, ENT_QUOTES, 'UTF-8') . '</p>';
	}
	if ($extra !== '') {
		$html .= $extra;
	}
	if ($user_id > 0) {
		$html .= epc_build_customer_profile_html($user_id);
	}
	$html .= '</div>';
	return $html;
}
