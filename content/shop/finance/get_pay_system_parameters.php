<?php
//Скрипт для получения настроек платежной системы. Встраивается в скрипты платежных систем go_to_pay.php или notification.php

//Нельзя обратиться прямо
if( !isset($DP_Config) )
{
	exit;
}


//Необходимый параметр - $operation_id, который должен быть задан перед этим скриптом
// Optional: $EPC_PAY_HANDLER to load credentials for a specific enabled gateway (multi-gateway checkout)
// Individual accounts: credentials from epc_payment_accounts when linked to the operation



if( isset( $DP_Config->wholesaler ) )
{
	$paysystem_parameters_query = $db_link->prepare('SELECT `pay_system_parameters` FROM `shop_offices` WHERE `id` = (SELECT `office_id` FROM `shop_users_accounting` WHERE `id` = ?);');
	$paysystem_parameters_query->execute( array($operation_id) );
	$paysystem_parameters_record = $paysystem_parameters_query->fetch();
	$paysystem_parameters = json_decode($paysystem_parameters_record["pay_system_parameters"], true);
}
else
{
	$paysystem_parameters = array();
	$loadedFromAccount = false;

	// 1) Individual payment account linked to this operation
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_accounts.php';
		$accId = 0;
		try {
			$aq = $db_link->prepare('SELECT `epc_payment_account_id`, `pay_orders`, `office_id` FROM `shop_users_accounting` WHERE `id` = ? LIMIT 1');
			$aq->execute(array($operation_id));
			$opRow = $aq->fetch(PDO::FETCH_ASSOC);
			if ($opRow) {
				$accId = (int)($opRow['epc_payment_account_id'] ?? 0);
				if ($accId <= 0) {
					$orderId = (int)($opRow['pay_orders'] ?? 0);
					$resolved = epc_pay_accounts_resolve_for_order($db_link, $orderId);
					if (is_array($resolved) && (int)($resolved['id'] ?? 0) > 0) {
						$accId = (int)$resolved['id'];
					} elseif (is_array($resolved)) {
						$paysystem_parameters = epc_pay_accounts_credentials_array($resolved);
						$loadedFromAccount = !empty($paysystem_parameters);
						$GLOBALS['EPC_PAY_ACCOUNT'] = $resolved;
					}
				}
			}
		} catch (Throwable $e) {
			$accId = 0;
		}
		if ($accId > 0) {
			$account = epc_pay_accounts_get($db_link, $accId);
			if ($account && ($account['status'] ?? '') === 'active') {
				$paysystem_parameters = epc_pay_accounts_credentials_array($account);
				$loadedFromAccount = true;
				$GLOBALS['EPC_PAY_ACCOUNT'] = $account;
				if (!isset($EPC_PAY_HANDLER) || $EPC_PAY_HANDLER === '') {
					$EPC_PAY_HANDLER = (string)$account['handler'];
				}
			}
		}
	} catch (Throwable $e) {
	}

	if (!$loadedFromAccount) {
		$handlerHint = '';
		if (isset($EPC_PAY_HANDLER) && (string)$EPC_PAY_HANDLER !== '') {
			$handlerHint = preg_replace('/[^a-z0-9_]/', '', (string)$EPC_PAY_HANDLER);
		}
		if ($handlerHint !== '') {
			$paysystem_parameters_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `handler` = ? AND `anable` = 1 LIMIT 1;');
			$paysystem_parameters_query->execute(array($handlerHint));
			$paysystem_parameters_record = $paysystem_parameters_query->fetch();
		} else {
			$paysystem_parameters_record = false;
		}
		if ($paysystem_parameters_record == false) {
			$paysystem_parameters_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `active`= ?;');
			$paysystem_parameters_query->execute( array(1) );
			$paysystem_parameters_record = $paysystem_parameters_query->fetch();
		}
		$paysystem_parameters = json_decode($paysystem_parameters_record["parameters_values"] ?? '{}', true);
	}
	if (!is_array($paysystem_parameters)) {
		$paysystem_parameters = array();
	}
}
?>
