<?php
require_once($_SERVER["DOCUMENT_ROOT"] ."/config.php");
require_once($_SERVER["DOCUMENT_ROOT"] ."/content/users/dp_user.php");
require_once("genHmacStr.php");
//------------------------------------------------------------------------------------------------//

$DP_Config = new DP_Config;//Конфигурация CMS
//Соединение с БД
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

$opration_id_return	= $_POST["ORDER"];//Получаем ID операции
$p_sing_return 		= $_POST["P_SIGN"];//Хеш банка

if($_POST["RESULT"] != 0) //Если не ноль, ошибка проведения операции.
{
	$answer = array();
	$answer["result"] = false;
	$answer["code"] = $_POST["RESULT"];
	
	exit(json_encode($answer));
}

$operation_id = preg_replace("/^[0]{1,5}/", "", $opration_id_return); 

$operation_id = (int)$operation_id;

$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
$operation_query->execute( array($operation_id) );
$operation = $operation_query->fetch();
if($operation == false)
{
    $answer = array();
	$answer["result"] = false;
	$answer["code"] = 2;
	exit(json_encode($answer));
}



//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );

$merchant	= $paysystem_parameters["merchant"];
$terminal	= $paysystem_parameters["terminal"];
$secret_key	= $paysystem_parameters["secret_key"];


//Порядок формирования строк для хеша
// AMOUNT,CURRENCY,ORDER,MERCH_NAME,MERCHANT,TERMINAL,EMAIL,TRTYPE,TIMESTAMP,NONCE,BACKREF, RESULT, RC, RCTEXT, AUTHCODE, RRN, INT_REF

$verification = array();

$verification["AMOUNT"] 		= $operation["amount"];
$verification["CURRENCY"] 	= $_POST["CURRENCY"];
$verification["ORDER"] 		= "00000".$operation["id"];
$verification["MERCH_NAME"] 	= $_POST["MERCH_NAME"];
$verification["MERCHANT"] 	= $merchant;
$verification["TERMINAL"] 	= $terminal;
$verification["EMAIL"] 		= $_POST["EMAIL"];
$verification["TRTYPE"] 		= $_POST["TRTYPE"];
$verification["TIMESTAMP"]	= $_POST["TIMESTAMP"];
$verification["NONCE"] 		= $_POST["NONCE"];
$verification["BACKREF"] 		= $_POST["BACKREF"];
$verification["RESULT"] 		= $_POST["RESULT"];
$verification["RC"] 			= $_POST["RC"];
$verification["RCTEXT"] 		= $_POST["RCTEXT"];
$verification["AUTHCODE"] 	= $_POST["AUTHCODE"];
$verification["RRN"] 			= $_POST["RRN"];
$verification["INT_REF"] 		= $_POST["INT_REF"];

$p_sign = genHmacStr($verification, $secret_key); //Получаем хеш
$p_sign = strtoupper($p_sign);
if($p_sign != $p_sing_return) //Сравниваем
{
    $answer = array();
	$answer["result"] = false;
	$answer["code"] = 2;

	exit(json_encode($answer));
}

//Активируем операцию:
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ? AND `active` = 0;');
$update_query->execute( array($operation["id"]) );	

//Количество затронутых строк
$elements_count_rows = $update_query->rowCount();

if($elements_count_rows == 0)
{
    $answer = array();
	$answer["result"] = false;
	$answer["code"] = 3;

	exit(json_encode($answer));
}
else
{
	// -----
	//Уведомление менеджерам магазинов
	$operation_id = $operation["id"];
	$amount = $operation["amount"];
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");

	// -----
	//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
	$operation_id = $operation["id"];
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
}
?>