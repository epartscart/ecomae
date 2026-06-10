<?php
header('Content-Type: text/html; charset=utf-8');

/*
$f = fopen('log.txt', 'w');
fwrite($f, json_encode($_POST));
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


$operation_id = (int) $_POST['orderid'];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$login = $paysystem_parameters["paykeeper_login"];
$password = $paysystem_parameters["paykeeper_password"];
$subdomain = $paysystem_parameters["paykeeper_subdomain"];
$secret = $paysystem_parameters["paykeeper_secret"];



//Принято от paykeeper
$secret_seed = $secret;
$id = $_POST['id'];
$sum = (float) $_POST['sum'];
$clientid = (int) $_POST['clientid'];
$orderid = (int) $_POST['orderid'];
$key = $_POST['key'];

if ($key != md5 ($id . sprintf ("%.2lf", $sum).$clientid.$orderid.$secret_seed))
{
    echo "Error! Hash mismatch";
    exit;
}

$active_query = $db_link->prepare('SELECT `active` FROM `shop_users_accounting` WHERE `id` = ?;');
$active_query->execute( array($orderid) );
$active_record = $active_query->fetch();
if(empty($active_record)){
	echo "Error! accounting no";
    exit;
}
if((int)$active_record['active'] === 1){
	echo "OK ".md5($id.$secret_seed);
    exit;
}

//Активируем операцию:
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active`=1 WHERE `id` = ? AND `user_id`=? AND `amount`=?;');
$update_query->execute( array($orderid, $clientid, $sum) );	


// -----
//Уведомление менеджерам магазинов
$operation_id = $orderid;
$amount = $sum;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
// -----


// -----
//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
$operation_id = $orderid;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
// -----


echo "OK ".md5($id.$secret_seed);
?>