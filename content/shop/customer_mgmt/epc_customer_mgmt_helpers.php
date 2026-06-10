<?php
/**
 * Customer management — orders, invoices, advances, returns, e-invoice profile.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../finance/epc_einvoice.php';

function epc_cm_h($v)
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_cm_money($n)
{
	return number_format((float)$n, 2, '.', ',');
}

function epc_cm_dashboard(PDO $db): array
{
	$customers = (int)$db->query('SELECT COUNT(*) FROM `users` WHERE `user_id` > 0')->fetchColumn();
	$orders30 = (int)$db->query(
		'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ' . (int)strtotime('-30 days')
	)->fetchColumn();
	$openOrders = (int)$db->query(
		'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `paid` != 1'
	)->fetchColumn();
	$buyersWithTrn = (int)$db->query(
		'SELECT COUNT(*) FROM `epc_einvoice_buyer_profiles` WHERE `trn` != "" AND `trn` IS NOT NULL'
	)->fetchColumn();
	$einvoices = (int)$db->query('SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `active` = 1')->fetchColumn();
	$returns = 0;
	try {
		$returns = (int)$db->query('SELECT COUNT(*) FROM `shop_orders_returns`')->fetchColumn();
	} catch (Exception $e) {
	}
	$advances = (float)$db->query(
		'SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1'
	)->fetchColumn();
	return array(
		'customers' => $customers,
		'orders_30d' => $orders30,
		'open_orders' => $openOrders,
		'buyers_with_trn' => $buyersWithTrn,
		'einvoices' => $einvoices,
		'returns' => $returns,
		'customer_ledger_balance' => $advances,
	);
}

function epc_cm_count_customers(PDO $db, $search = ''): int
{
	if ($search === '') {
		return (int)$db->query('SELECT COUNT(*) FROM `users` WHERE `user_id` > 0')->fetchColumn();
	}
	$sql = 'SELECT COUNT(DISTINCT u.`user_id`) FROM `users` u
		LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`
		WHERE u.`user_id` > 0
		AND (u.`email` LIKE ? OR u.`phone` LIKE ? OR up.`data_value` LIKE ?)';
	$q = '%' . $search . '%';
	$st = $db->prepare($sql);
	$st->execute(array($q, $q, $q));
	return (int)$st->fetchColumn();
}

function epc_cm_list_customers(PDO $db, $search = '', $limit = 50, $offset = 0): array
{
	$limit = max(1, min(200, (int)$limit));
	$offset = max(0, (int)$offset);
	$sql = "SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_reg`,
		MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname,
		MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname,
		MAX(CASE WHEN up.`data_key` = 'company' THEN up.`data_value` END) AS company,
		b.`trn`, b.`peppol_endpoint`, b.`buyer_onboarded`
		FROM `users` u
		LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`
		LEFT JOIN `epc_einvoice_buyer_profiles` b ON b.`user_id` = u.`user_id`
		WHERE u.`user_id` > 0";
	$params = array();
	if ($search !== '') {
		$sql .= ' AND (u.`email` LIKE ? OR u.`phone` LIKE ? OR up.`data_value` LIKE ?)';
		$q = '%' . $search . '%';
		$params = array($q, $q, $q);
	}
	$sql .= ' GROUP BY u.`user_id` ORDER BY u.`user_id` DESC LIMIT ' . $limit . ' OFFSET ' . $offset;
	$st = $db->prepare($sql);
	$st->execute($params);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$row) {
		$row['order_count'] = null;
	}
	unset($row);
	if (!$rows) {
		return $rows;
	}
	$ids = array();
	foreach ($rows as $r) {
		$ids[] = (int)$r['user_id'];
	}
	$ph = implode(',', array_fill(0, count($ids), '?'));
	$oc = $db->prepare(
		'SELECT `user_id`, COUNT(*) AS c FROM `shop_orders`
		WHERE `successfully_created` = 1 AND `user_id` IN (' . $ph . ')
		GROUP BY `user_id`'
	);
	$oc->execute($ids);
	$counts = array();
	while ($c = $oc->fetch(PDO::FETCH_ASSOC)) {
		$counts[(int)$c['user_id']] = (int)$c['c'];
	}
	foreach ($rows as &$row) {
		$uid = (int)$row['user_id'];
		$row['order_count'] = $counts[$uid] ?? 0;
	}
	unset($row);
	return $rows;
}

function epc_cm_customer_orders(PDO $db, int $user_id, $limit = 50): array
{
	if ($user_id <= 0) {
		return array();
	}
	$st = $db->prepare(
		'SELECT o.*,
			(SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = o.`id`) AS sale_ex
		FROM `shop_orders` o
		WHERE o.`user_id` = ? AND o.`successfully_created` = 1
		ORDER BY o.`time` DESC LIMIT ' . (int)$limit
	);
	$st->execute(array($user_id));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_cm_customer_advances(PDO $db, int $user_id, $limit = 50): array
{
	if ($user_id <= 0) {
		return array();
	}
	$st = $db->prepare(
		'SELECT * FROM `shop_users_accounting`
		WHERE `user_id` = ? AND `active` = 1
		ORDER BY `time` DESC LIMIT ' . (int)$limit
	);
	$st->execute(array($user_id));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_cm_customer_einvoices(PDO $db, int $user_id, $limit = 30): array
{
	if ($user_id <= 0) {
		return array();
	}
	$st = $db->prepare(
		'SELECT * FROM `epc_einvoice_documents`
		WHERE `user_id` = ? AND `active` = 1
		ORDER BY `issue_date` DESC LIMIT ' . (int)$limit
	);
	$st->execute(array($user_id));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_cm_recent_returns(PDO $db, int $user_id = 0, $limit = 30): array
{
	try {
		$sql = 'SELECT r.*, o.`user_id` FROM `shop_orders_returns` r
			LEFT JOIN `shop_orders` o ON o.`id` = r.`order_id`
			WHERE 1=1';
		$params = array();
		if ($user_id > 0) {
			$sql .= ' AND o.`user_id` = ?';
			$params[] = $user_id;
		}
		$sql .= ' ORDER BY r.`id` DESC LIMIT ' . (int)$limit;
		$st = $db->prepare($sql);
		$st->execute($params);
		return $st->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return array();
	}
}

function epc_cm_save_customer_profile(PDO $db, array $data): void
{
	$user_id = (int)($data['user_id'] ?? 0);
	if ($user_id <= 0) {
		throw new Exception('Invalid customer');
	}
	epc_einvoice_save_buyer_profile($db, $data);
	if (isset($data['country_code'])) {
		require_once __DIR__ . '/../pricing/epc_customer_trade.php';
		epc_trade_profile_set($db, $user_id, 'epc_reg_country', strtoupper(trim((string)$data['country_code'])));
	}
	if (!empty($data['trn'])) {
		require_once __DIR__ . '/../pricing/epc_customer_trade.php';
		epc_trade_profile_set($db, $user_id, 'epc_reg_trn', preg_replace('/\D/', '', (string)$data['trn']));
	}
	$vatFile = __DIR__ . '/../finance/epc_uae_customer_vat.php';
	if (is_readable($vatFile)) {
		require_once $vatFile;
		epc_uae_customer_vat_sync($db, $user_id);
	}
	$fields = array('company' => 'company', 'address' => 'address_line1', 'city' => 'city', 'phone' => 'phone');
	foreach ($fields as $profileKey => $postKey) {
		if (!isset($data[$postKey]) && !isset($data[$profileKey])) {
			continue;
		}
		$val = trim((string)($data[$postKey] ?? $data[$profileKey] ?? ''));
		$ex = $db->prepare('SELECT `id` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1');
		$ex->execute(array($user_id, $profileKey));
		$id = (int)$ex->fetchColumn();
		if ($id > 0) {
			$db->prepare('UPDATE `users_profiles` SET `data_value` = ? WHERE `id` = ?')->execute(array($val, $id));
		} elseif ($val !== '') {
			$db->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')->execute(array($user_id, $profileKey, $val));
		}
	}
}

function epc_cm_get_customer(PDO $db, int $user_id): ?array
{
	if ($user_id <= 0) {
		return null;
	}
	$rows = epc_cm_list_customers($db, (string)$user_id, 1);
	foreach ($rows as $r) {
		if ((int)$r['user_id'] === $user_id) {
			$st = $db->prepare('SELECT * FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1');
			$st->execute(array($user_id));
			$profile = $st->fetch(PDO::FETCH_ASSOC);
			return array_merge($r, $profile ?: array());
		}
	}
	$st = $db->prepare('SELECT u.* FROM `users` u WHERE u.`user_id` = ? LIMIT 1');
	$st->execute(array($user_id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_cm_recent_orders(PDO $db, $limit = 50): array
{
	$st = $db->query(
		'SELECT o.*, u.`email`,
			(SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = o.`id`) AS sale_ex
		FROM `shop_orders` o
		LEFT JOIN `users` u ON u.`user_id` = o.`user_id`
		WHERE o.`successfully_created` = 1
		ORDER BY o.`time` DESC LIMIT ' . (int)$limit
	);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_cm_tab_url($base, $tab, $extra = '')
{
	$url = rtrim($base, '?') . '?tab=' . urlencode($tab);
	if ($extra !== '') {
		$url .= '&' . ltrim($extra, '&');
	}
	return $url;
}
