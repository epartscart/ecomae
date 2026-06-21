<?php
//Скрипт для выполнения протокола оплаты заказа при зачислении платежа на баланс клиента. Скрипт подключается в notification.php

$order_id_query = $db_link->prepare('SELECT `pay_orders` FROM `shop_users_accounting` WHERE `id` = ?;');
$order_id_query->execute( array($operation_id) );
$order_id_record = $order_id_query->fetch();
$pay_orders = $order_id_record["pay_orders"];

if( $pay_orders != "" )
{
	$order_id = $pay_orders;
	
	//$amount - объявлен в notification.php из папки платежной системы
	
	//Выполняем протокол
	$curl = curl_init();
	curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/protocol/pay_for_order.php?order_id=".$order_id."&initiator=3&code=".urlencode($DP_Config->tech_key)."&pay_sum=".$amount."&direct_pay=0");
	curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
	curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
	$curl_result = curl_exec($curl);
	curl_close($curl);
	$curl_result = json_decode($curl_result, true);
	
	if( $curl_result["status"] == true )
	{
		//Заказ успешно оплачен с баланса
	}
	else
	{
		//Ошибка оплаты
	}
}

// Создание и печать чека ККТ после онлайн платежа
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/kkt/inc_create_check_for_online_pay.php");
?>