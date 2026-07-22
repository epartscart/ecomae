<?php
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
// CSRF optional for standalone endpoint (session already admin-checked there)
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php') && empty($GLOBALS['epc_pay_skip_csrf'])) {
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
	} catch (Throwable $e) {
	}
}

header('Content-Type: application/json; charset=utf-8');

function epc_pay_json($ok, $message, $extra = array())
{
	echo json_encode(array_merge(array('status' => (bool)$ok, 'message' => (string)$message), $extra));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	epc_pay_json(false, 'No database');
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_accounts.php';

$action = (string)($_POST['action'] ?? '');

try {
	switch ($action) {
		case 'seed_dummy':
			epc_payment_seed_all_gateways($db_link);
			epc_payment_enable_legacy($db_link);
			epc_pay_accounts_seed_platform($db_link);
			epc_pay_json(true, 'Gateways + platform payment account seeded');

		case 'activate':
			$handler = preg_replace('/[^a-z0-9_]/', '', (string)($_POST['handler'] ?? ''));
			if ($handler === '') {
				throw new Exception('Handler required');
			}
			$ex = $db_link->prepare('SELECT `id` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1');
			$ex->execute(array($handler));
			if ((int)$ex->fetchColumn() <= 0) {
				throw new Exception('Gateway not found');
			}
			epc_payment_set_active($db_link, $handler);
			epc_pay_json(true, 'Activated: ' . epc_payment_handler_title($handler));

		case 'save_config':
			$systemId = (int)($_POST['system_id'] ?? 0);
			$paramsJson = (string)($_POST['parameters_values'] ?? '{}');
			json_decode($paramsJson);
			if (json_last_error() !== JSON_ERROR_NONE) {
				throw new Exception('Invalid parameters JSON');
			}
			if ($systemId <= 0) {
				$db_link->exec('UPDATE `shop_payment_systems` SET `active` = 0');
				epc_pay_json(true, 'All payment gateways disabled');
			}
			$db_link->exec('UPDATE `shop_payment_systems` SET `active` = 0');
			$db_link->prepare('UPDATE `shop_payment_systems` SET `active` = 1, `parameters_values` = ? WHERE `id` = ?')
				->execute(array($paramsJson, $systemId));
			epc_pay_json(true, 'Payment gateway saved and activated');

		case 'save_account':
			$credsJson = (string)($_POST['credentials_json'] ?? '{}');
			$creds = json_decode($credsJson, true);
			if (!is_array($creds)) {
				throw new Exception('Invalid credentials JSON');
			}
			$id = epc_pay_accounts_save($db_link, array(
				'id' => (int)($_POST['id'] ?? 0),
				'owner_type' => (string)($_POST['owner_type'] ?? 'platform'),
				'owner_id' => (int)($_POST['owner_id'] ?? 0),
				'title' => (string)($_POST['title'] ?? ''),
				'handler' => (string)($_POST['handler'] ?? ''),
				'mode' => (string)($_POST['mode'] ?? 'direct'),
				'connected_account_id' => (string)($_POST['connected_account_id'] ?? ''),
				'payout_iban' => (string)($_POST['payout_iban'] ?? ''),
				'payout_bank' => (string)($_POST['payout_bank'] ?? ''),
				'payout_name' => (string)($_POST['payout_name'] ?? ''),
				'platform_fee_pct' => (float)($_POST['platform_fee_pct'] ?? 0),
				'status' => (string)($_POST['status'] ?? 'active'),
				'demo_mode' => !empty($_POST['demo_mode']) ? 1 : 0,
				'is_default' => !empty($_POST['is_default']) ? 1 : 0,
				'credentials' => $creds,
			));
			epc_pay_json(true, 'Payment account saved', array('account_id' => $id));

		case 'disable_account':
			$aid = (int)($_POST['id'] ?? 0);
			if ($aid <= 0) {
				throw new Exception('Account id required');
			}
			$row = epc_pay_accounts_get($db_link, $aid);
			if (!$row) {
				throw new Exception('Account not found');
			}
			epc_pay_accounts_save($db_link, array_merge($row, array(
				'id' => $aid,
				'status' => 'disabled',
				'credentials' => epc_pay_accounts_decode_credentials($row['credentials']),
				'is_default' => 0,
			)));
			epc_pay_json(true, 'Account disabled');

		case 'seed_platform_account':
			epc_pay_accounts_seed_platform($db_link);
			epc_pay_json(true, 'Platform payment account ready');

		case 'mark_settlement':
			$sid = (int)($_POST['id'] ?? 0);
			$status = preg_replace('/[^a-z_]/', '', (string)($_POST['status'] ?? 'paid_out'));
			if ($sid <= 0) {
				throw new Exception('Settlement id required');
			}
			epc_pay_accounts_mark_settlement($db_link, $sid, $status !== '' ? $status : 'paid_out');
			epc_pay_json(true, 'Settlement updated');

		default:
			epc_pay_json(false, 'Unknown action');
	}
} catch (Exception $e) {
	epc_pay_json(false, $e->getMessage());
}
