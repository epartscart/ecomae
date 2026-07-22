<?php
/**
 * Shared notification for UAE gateway demo stubs.
 */
if (!isset($EPC_PAY_HANDLER) || $EPC_PAY_HANDLER === '') {
	exit('No handler');
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
	exit(json_encode(array('result' => false)));
}
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$operation_id = (int)($_POST['operation_id'] ?? $_GET['operation_id'] ?? 0);
$sum = (float)($_POST['sum'] ?? 0);
$demoToken = (string)($_POST['demo_token'] ?? '');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/get_pay_system_parameters.php';

if (empty($paysystem_parameters['demo_mode']) && $demoToken !== 'epc-demo-ok') {
	exit('Forbidden');
}

$check = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
$check->execute(array($operation_id));
if ((int)$check->fetchColumn() !== 1) {
	header('Location: ' . $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] . '/shop/balans?success_message=' . urlencode(translate_str_by_id(4355)));
	exit;
}

$db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;')->execute(array($operation_id));

$amount = $sum;
$operation_id = $operation_id;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/pay_notify.php';

$operation_id = (int)$_POST['operation_id'];
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/pay_for_order.php';

// Attribute funds to individual payment account (office / vendor)
try {
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_accounts.php';
	epc_pay_accounts_ensure_schema($db_link);
	$opQ = $db_link->prepare('SELECT `pay_orders`, `amount` FROM `shop_users_accounting` WHERE `id` = ? LIMIT 1');
	$opQ->execute(array($operation_id));
	$op = $opQ->fetch(PDO::FETCH_ASSOC) ?: array();
	$orderId = (int)($op['pay_orders'] ?? 0);
	$account = null;
	$accId = 0;
	try {
		$aq = $db_link->prepare('SELECT `epc_payment_account_id` FROM `shop_users_accounting` WHERE `id` = ? LIMIT 1');
		$aq->execute(array($operation_id));
		$accId = (int)$aq->fetchColumn();
	} catch (Throwable $e) {
		$accId = 0;
	}
	if ($accId > 0) {
		$account = epc_pay_accounts_get($db_link, $accId);
	}
	if (!$account) {
		$account = epc_pay_accounts_resolve_for_order($db_link, $orderId);
	}
	$gross = $sum > 0 ? $sum : (float)($op['amount'] ?? 0);
	$currency = !empty($paysystem_parameters['currency']) ? (string)$paysystem_parameters['currency'] : 'AED';
	if (is_array($account)) {
		$splits = $orderId > 0 ? epc_pay_accounts_order_splits($db_link, $orderId) : array();
		$hasVendorSplits = false;
		foreach ($splits as $sp) {
			if (!empty($sp['account']) && (int)($sp['vendor_id'] ?? 0) > 0) {
				$hasVendorSplits = true;
				epc_pay_accounts_create_settlement($db_link, array(
					'operation_id' => $operation_id,
					'order_id' => $orderId,
					'account_id' => (int)$sp['account']['id'],
					'owner_type' => 'vendor',
					'owner_id' => (int)$sp['vendor_id'],
					'handler' => (string)($sp['account']['handler'] ?? $EPC_PAY_HANDLER),
					'gross_amount' => (float)$sp['amount'],
					'platform_fee_pct' => (float)($sp['account']['platform_fee_pct'] ?? 0),
					'currency' => $currency,
					'status' => 'credited',
					'note' => 'Auto-settlement to vendor account',
				));
			}
		}
		if (!$hasVendorSplits) {
			epc_pay_accounts_create_settlement($db_link, array(
				'operation_id' => $operation_id,
				'order_id' => $orderId,
				'account_id' => (int)($account['id'] ?? 0),
				'owner_type' => (string)($account['owner_type'] ?? 'platform'),
				'owner_id' => (int)($account['owner_id'] ?? 0),
				'handler' => (string)($account['handler'] ?? $EPC_PAY_HANDLER),
				'gross_amount' => $gross,
				'platform_fee_pct' => (float)($account['platform_fee_pct'] ?? 0),
				'currency' => $currency,
				'status' => 'credited',
				'note' => 'Payment credited to individual account',
			));
		}
	}
} catch (Throwable $e) {
}

header('Location: ' . $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] . '/shop/balans?success_message=' . urlencode(translate_str_by_id(4355)));
exit;
