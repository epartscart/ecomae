<?php
// Сюда идет переадресация от Assist при успешной оплате

ini_set("display_errors",0);

header("Content-Type: text/xml");

//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["result"] = false;
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


$operation_id = (int)$_POST["ordernumber"];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );



/*
	$file = fopen("notification.txt", "w");

	ob_start();
				
	print_r($_POST);
	echo "После поста...";
	echo "<br>";

	print_r($operation_id);
	
	$notify_log = ob_get_contents();
	
	ob_end_clean();


	fwrite($file, $notify_log."\n\n");
*/

// Проверяем действительно ли был платеж
$merchant_id = (int)$_POST["merchant_id"];
$order_number = (int)$_POST["ordernumber"];
$order_amount = $_POST["amount"];
$order_currency = $_POST["currency"];// Валюта
$order_state = $_POST["orderstate"];// Статус заказа

$billnumber = $_POST["billnumber"];// Полный уникальный номер операции в платежной системе
$packetdate = $_POST["packetdate"];// Дата формирования запроса по Гринвичу (GMT)
$testmode = $_POST["testmode"];

$checkvalue = $_POST["checkvalue"];// проверочный хеш

// Формируем Checkvalue - проверочную строку
$str_x = $merchant_id . $order_number . $order_amount . $order_currency . $order_state;
$str_x = md5($str_x);
$salt  = md5($paysystem_parameters['salt']);
$hash  = mb_strtoupper($salt . $str_x, 'UTF-8');
$hash  = md5($hash);
$hash  = mb_strtoupper($hash, 'UTF-8');

fwrite($file, $hash .' = '. $checkvalue ."\n");

if($hash !== $checkvalue){
	// Ошибка хеша
	echo '<?xml version="1.0" encoding="utf-8"?>
<pushpaymentresult firstcode="9" secondcode="5">
</pushpaymentresult>';
}else{
	// Все хорошо
	$flag = false;
	
	if(!empty($operation_id) && ($order_state == 'Approved'))
	{
		$check_operation = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
		$check_operation->execute( array($operation_id) );
		if($check_operation->fetchColumn() > 0)
		{
			$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
			if( $update_query->execute( array($operation_id) ) != false)
			{
				$flag = true;
				
				// -----
				//Уведомление менеджерам магазинов
				$operation_id = $operation_id;
				$amount = $order_amount;
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
				// -----
				
				// -----
				//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
				$operation_id = $operation_id;
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
				// -----
			}
		}
	}
	
	if($flag)
	{
	// Все хорошо
	echo '<?xml version="1.0" encoding="utf-8"?>
<pushpaymentresult firstcode="0" secondcode="0">
<order>
<billnumber>'.$billnumber.'</billnumber>
<packetdate>'.$packetdate.'</packetdate>
</order>
</pushpaymentresult>';
	}
	else
	{
		// Ошибка
echo '<?xml version="1.0" encoding="utf-8"?>
<pushpaymentresult firstcode="1" secondcode="1">
</pushpaymentresult>';
	}
}
?>