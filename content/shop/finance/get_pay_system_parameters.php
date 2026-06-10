<?php
//Скрипт для получения настроек платежной системы. Встраивается в скрипты платежных систем go_to_pay.php или notification.php

//Нельзя обратиться прямо
if( !isset($DP_Config) )
{
	exit;
}


//Необходимый параметр - $operation_id, который должен быть задан перед этим скриптом



if( isset( $DP_Config->wholesaler ) )
{
	$paysystem_parameters_query = $db_link->prepare('SELECT `pay_system_parameters` FROM `shop_offices` WHERE `id` = (SELECT `office_id` FROM `shop_users_accounting` WHERE `id` = ?);');
	$paysystem_parameters_query->execute( array($operation_id) );
	$paysystem_parameters_record = $paysystem_parameters_query->fetch();
	$paysystem_parameters = json_decode($paysystem_parameters_record["pay_system_parameters"], true);
}
else
{
	$paysystem_parameters_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `active`= ?;');
	$paysystem_parameters_query->execute( array(1) );
	$paysystem_parameters_record = $paysystem_parameters_query->fetch();
	$paysystem_parameters = json_decode($paysystem_parameters_record["parameters_values"], true);
}
?>