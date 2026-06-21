<?php
header('Content-Type: text/html; charset=utf-8');
/**
 * Скрипт для автоуведомления менеджера об оплате и для изменения статуса заказа
 * 
 * chronopay вызывает этот скрипт после платежа
*/

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


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

$operation_id = $_POST["order_id"];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$product_id = $paysystem_parameters["product_id"];
$shared_sec = $paysystem_parameters["shared_sec"];



$customer_id = $_POST['customer_id'];
$transaction_id = $_POST['transaction_id'];
$transaction_type = $_POST['transaction_type'];
$total = $_POST['total'];

$sign = md5($shared_sec . $customer_id . $transaction_id . $transaction_type . $total);

if('185.30.16.166' === $_SERVER["REMOTE_ADDR"])
{
	if($sign === $_POST['sign'])
	{
		// ЗДЕСЬ МЕНЯМ СТАТУС ОПЕРАЦИИ (active=1)
		//Проверяем заказ
		$check_operation = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
		$check_operation->execute( array($_POST["order_id"]) );
		if( $check_operation->fetchColumn() == 1 )
		{
			$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
			$update_query->execute( array($_POST["order_id"]) );	
			
			// -----
			//Уведомление менеджерам магазинов
			$operation_id = $_POST["order_id"];
			$amount = $total;
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
			// -----
			
			
			// -----
			//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
			$operation_id = $_POST["order_id"];
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
			// -----
		}
	}
}
?>