<?php
//Скрипт для получения настроек платежной системы. Встраивается в скрипты платежных систем go_to_pay.php или notification.php

//Нельзя обратиться прямо
if( !isset($DP_Config) )
{
	exit;
}


//Необходимый параметр - $operation_id, который должен быть задан перед этим скриптом
// Optional: $EPC_PAY_HANDLER to load credentials for a specific enabled gateway (multi-gateway checkout)



if( isset( $DP_Config->wholesaler ) )
{
	$paysystem_parameters_query = $db_link->prepare('SELECT `pay_system_parameters` FROM `shop_offices` WHERE `id` = (SELECT `office_id` FROM `shop_users_accounting` WHERE `id` = ?);');
	$paysystem_parameters_query->execute( array($operation_id) );
	$paysystem_parameters_record = $paysystem_parameters_query->fetch();
	$paysystem_parameters = json_decode($paysystem_parameters_record["pay_system_parameters"], true);
}
else
{
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
	if (!is_array($paysystem_parameters)) {
		$paysystem_parameters = array();
	}
}
?>
