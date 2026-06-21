<?php
/**
 * Сюда идет переадресация от Авангарда при успешной оплате
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


$operation_id = $_POST["order_number"];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );




$shop_id = $paysystem_parameters["avangard_shop_id"];
$auth_code = $_POST["auth_code"];//Код авторизации
$av_sign = $paysystem_parameters["avangard_av_sign"];
$amount = $_POST["amount"];
$signature = $_POST["signature"];


//Проверяем подпись:
if(strtoupper(md5(strtoupper(md5($av_sign).md5($shop_id.$operation_id.$amount)))) != $signature)
{
    exit("No signature");
}

$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
$update_query->execute( array($operation_id) );	


// -----
//Уведомление менеджерам магазинов
$operation_id = $operation_id;
$amount = $amount;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
// -----


// -----
//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
$operation_id = $operation_id;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
// -----

header("HTTP/1.1 202 Accepted");
?>