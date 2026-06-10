<?php
/**
 * Retail / wholesale registration, manager approval, fixed dealing currency.
 */
defined('_ASTEXE_') or die('No access');

function epc_trade_customer_types(): array
{
	return array('retail', 'wholesale');
}

function epc_trade_profile_get($db_link, int $user_id, string $key, string $default = ''): string
{
	if ($user_id <= 0 || !isset($db_link) || !$db_link) {
		return $default;
	}
	try {
		$stmt = $db_link->prepare('SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1;');
		$stmt->execute(array($user_id, $key));
		$val = $stmt->fetchColumn();
		return ($val === false) ? $default : trim((string)$val);
	} catch (Throwable $e) {
		return $default;
	}
}

function epc_trade_profile_set($db_link, int $user_id, string $key, string $value): void
{
	if ($user_id <= 0 || !isset($db_link) || !$db_link || $key === '') {
		return;
	}
	try {
		$db_link->prepare('DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?;')->execute(array($user_id, $key));
		$db_link->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?);')->execute(array($user_id, $key, $value));
	} catch (Throwable $e) {
	}
}

function epc_trade_approval_status($db_link, int $user_id): string
{
	$status = epc_trade_profile_get($db_link, $user_id, 'epc_trade_approval_status', '');
	if ($status === '') {
		return 'approved';
	}
	return $status;
}

function epc_trade_is_pending($db_link, int $user_id): bool
{
	return epc_trade_approval_status($db_link, $user_id) === 'pending';
}

function epc_trade_is_approved($db_link, int $user_id): bool
{
	return epc_trade_approval_status($db_link, $user_id) === 'approved';
}

function epc_trade_can_place_order($db_link, int $user_id): bool
{
	if ($user_id <= 0) {
		return true;
	}
	return epc_trade_is_approved($db_link, $user_id);
}

function epc_trade_customer_type_label(string $type): string
{
	$type = strtolower(trim($type));
	if ($type === 'wholesale') {
		return 'Wholesale';
	}
	if ($type === 'retail') {
		return 'Retail';
	}
	return ucfirst($type);
}

function epc_trade_normalize_customer_type(string $type): string
{
	$type = strtolower(trim($type));
	return in_array($type, epc_trade_customer_types(), true) ? $type : '';
}

function epc_trade_default_retail_currency_iso($db_link): string
{
	if (!isset($db_link) || !$db_link) {
		return '';
	}
	$iso = '784';
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';
		epc_currency_ensure_supported($db_link);
		$stmt = $db_link->prepare('SELECT COUNT(*) FROM `shop_currencies` WHERE `iso_code` = ? AND `available` = 1;');
		$stmt->execute(array($iso));
		if ((int)$stmt->fetchColumn() > 0) {
			return $iso;
		}
	} catch (Throwable $e) {
	}
	return '';
}

function epc_trade_save_registration($db_link, int $user_id, string $customer_type): void
{
	$customer_type = epc_trade_normalize_customer_type($customer_type);
	if ($customer_type === '') {
		$customer_type = 'retail';
	}
	epc_trade_profile_set($db_link, $user_id, 'epc_customer_type', $customer_type);
	epc_trade_profile_set($db_link, $user_id, 'epc_trade_registered_at', (string)time());
	epc_trade_profile_delete($db_link, $user_id, 'epc_currency_change_requested');

	if ($customer_type === 'retail') {
		$currency = epc_trade_default_retail_currency_iso($db_link);
		if ($currency !== '' && epc_trade_approve_customer($db_link, $user_id, $currency, 'retail', 0)) {
			return;
		}
		epc_trade_profile_set($db_link, $user_id, 'epc_trade_approval_status', 'approved');
		epc_trade_assign_price_profile($db_link, $user_id, 'retail');
		if ($currency !== '') {
			epc_trade_profile_set($db_link, $user_id, 'epc_dealing_currency', $currency);
		}
		epc_trade_profile_set($db_link, $user_id, 'epc_trade_approved_at', (string)time());
		epc_trade_profile_delete($db_link, $user_id, 'epc_trade_rejection_note');
		return;
	}

	epc_trade_profile_set($db_link, $user_id, 'epc_trade_approval_status', 'pending');
	epc_trade_profile_delete($db_link, $user_id, 'epc_dealing_currency');
	epc_trade_profile_delete($db_link, $user_id, 'epc_trade_approved_at');
	epc_trade_profile_delete($db_link, $user_id, 'epc_trade_approved_by');
}

function epc_trade_profile_delete($db_link, int $user_id, string $key): void
{
	if ($user_id <= 0 || $key === '') {
		return;
	}
	try {
		$db_link->prepare('DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?;')->execute(array($user_id, $key));
	} catch (Throwable $e) {
	}
}

function epc_trade_user_currency_iso($db_link, int $user_id): string
{
	if ($user_id <= 0 || !epc_trade_is_approved($db_link, $user_id)) {
		return '';
	}
	$iso = preg_replace('/[^0-9]/', '', epc_trade_profile_get($db_link, $user_id, 'epc_dealing_currency', ''));
	return $iso !== '' ? $iso : '';
}

function epc_trade_currency_locked($db_link, int $user_id): bool
{
	return epc_trade_user_currency_iso($db_link, $user_id) !== '';
}

function epc_trade_price_profile_group_id($db_link, string $profile_code): int
{
	$profile_code = strtolower(trim($profile_code));
	if ($profile_code === '' || !isset($db_link) || !$db_link) {
		return 0;
	}
	try {
		$stmt = $db_link->prepare('SELECT `group_id` FROM `epc_price_profiles` WHERE `code` = ? LIMIT 1;');
		$stmt->execute(array($profile_code));
		return (int)$stmt->fetchColumn();
	} catch (Throwable $e) {
		return 0;
	}
}

function epc_trade_assign_price_profile($db_link, int $user_id, string $profile_code): bool
{
	$group_id = epc_trade_price_profile_group_id($db_link, $profile_code);
	if ($user_id <= 0 || $group_id <= 0) {
		return false;
	}
	try {
		$profile_groups = $db_link->query('SELECT `group_id` FROM `epc_price_profiles`;')->fetchAll(PDO::FETCH_COLUMN);
		if (!empty($profile_groups)) {
			$placeholders = implode(',', array_fill(0, count($profile_groups), '?'));
			$args = array_merge(array($user_id), $profile_groups);
			$db_link->prepare('DELETE FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN (' . $placeholders . ');')->execute($args);
		}
		$db_link->prepare('INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?);')->execute(array($user_id, $group_id));
		return true;
	} catch (Throwable $e) {
		return false;
	}
}

function epc_trade_approve_customer($db_link, int $user_id, string $currency_iso, string $profile_code, int $admin_id = 0): bool
{
	$currency_iso = preg_replace('/[^0-9]/', '', $currency_iso);
	$profile_code = epc_trade_normalize_customer_type($profile_code) !== '' ? epc_trade_normalize_customer_type($profile_code) : strtolower(trim($profile_code));
	if ($user_id <= 0 || $currency_iso === '' || $profile_code === '') {
		return false;
	}
	if (!epc_trade_assign_price_profile($db_link, $user_id, $profile_code)) {
		return false;
	}
	epc_trade_profile_set($db_link, $user_id, 'epc_trade_approval_status', 'approved');
	epc_trade_profile_set($db_link, $user_id, 'epc_dealing_currency', $currency_iso);
	epc_trade_profile_set($db_link, $user_id, 'epc_trade_approved_at', (string)time());
	epc_trade_profile_set($db_link, $user_id, 'epc_trade_approved_by', (string)$admin_id);
	epc_trade_profile_delete($db_link, $user_id, 'epc_currency_change_requested');
	epc_trade_profile_delete($db_link, $user_id, 'epc_currency_change_requested_iso');
	epc_trade_profile_delete($db_link, $user_id, 'epc_trade_rejection_note');
	return true;
}

function epc_trade_reject_customer($db_link, int $user_id, string $note = '', int $admin_id = 0): void
{
	epc_trade_profile_set($db_link, $user_id, 'epc_trade_approval_status', 'rejected');
	epc_trade_profile_set($db_link, $user_id, 'epc_trade_rejection_note', trim($note));
	epc_trade_profile_set($db_link, $user_id, 'epc_trade_approved_by', (string)$admin_id);
	epc_trade_profile_delete($db_link, $user_id, 'epc_dealing_currency');
}

function epc_trade_request_currency_change($db_link, int $user_id, string $requested_iso = '', string $note = ''): void
{
	if ($user_id <= 0 || !epc_trade_is_approved($db_link, $user_id)) {
		return;
	}
	$requested_iso = preg_replace('/[^0-9]/', '', $requested_iso);
	epc_trade_profile_set($db_link, $user_id, 'epc_currency_change_requested', '1');
	if ($requested_iso !== '') {
		epc_trade_profile_set($db_link, $user_id, 'epc_currency_change_requested_iso', $requested_iso);
	}
	if ($note !== '') {
		epc_trade_profile_set($db_link, $user_id, 'epc_currency_change_note', trim($note));
	}
}

function epc_trade_pending_customers($db_link, int $limit = 200): array
{
	$rows = array();
	if (!isset($db_link) || !$db_link) {
		return $rows;
	}
	try {
		$sql = "SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_registered`, u.`email_confirmed`,
			MAX(CASE WHEN p.`data_key` = 'epc_customer_type' THEN p.`data_value` END) AS `customer_type`,
			MAX(CASE WHEN p.`data_key` = 'name' THEN p.`data_value` END) AS `name`,
			MAX(CASE WHEN p.`data_key` = 'surname' THEN p.`data_value` END) AS `surname`,
			MAX(CASE WHEN p.`data_key` = 'company' THEN p.`data_value` END) AS `company`
			FROM `users` u
			INNER JOIN `users_profiles` ps ON ps.`user_id` = u.`user_id` AND ps.`data_key` = 'epc_trade_approval_status' AND ps.`data_value` = 'pending'
			LEFT JOIN `users_profiles` p ON p.`user_id` = u.`user_id`
			GROUP BY u.`user_id`
			ORDER BY u.`time_registered` DESC
			LIMIT " . (int)$limit;
		$q = $db_link->query($sql);
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$rows[] = $row;
		}
	} catch (Throwable $e) {
	}
	return $rows;
}

function epc_trade_currency_options($db_link, $DP_Config): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';
	return epc_currency_records($db_link, $DP_Config);
}

function epc_trade_notify_customer($notify_name, int $user_id, array $extra_vars = array()): void
{
	if ($user_id <= 0 || !function_exists('send_notify')) {
		return;
	}
	$vars = array_merge(array('order_id' => 0, 'order_text' => ''), $extra_vars);
	$persons = array(array('type' => 'user_id', 'user_id' => $user_id));
	send_notify($notify_name, $vars, $persons, true);
}

function epc_trade_checkout_block_message($db_link, int $user_id): string
{
	if ($user_id <= 0 || epc_trade_can_place_order($db_link, $user_id)) {
		return '';
	}
	$status = epc_trade_approval_status($db_link, $user_id);
	if ($status === 'pending') {
		return 'Your account is registered. You can browse and add items to the cart, but checkout is available only after a manager approves your retail/wholesale profile and dealing currency.';
	}
	if ($status === 'rejected') {
		$note = epc_trade_profile_get($db_link, $user_id, 'epc_trade_rejection_note', '');
		$msg = 'Your trade account registration was not approved. Please contact us if you need assistance.';
		if ($note !== '') {
			$msg .= ' Note: ' . $note;
		}
		return $msg;
	}
	return '';
}
