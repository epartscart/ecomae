<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

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

$action = (string)($_POST['action'] ?? '');

try {
	switch ($action) {
		case 'seed_dummy':
			epc_payment_seed_uae_gateways($db_link);
			epc_payment_enable_legacy($db_link);
			epc_pay_json(true, 'Dummy credentials refreshed for all gateways');

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

		default:
			epc_pay_json(false, 'Unknown action');
	}
} catch (Exception $e) {
	epc_pay_json(false, $e->getMessage());
}
