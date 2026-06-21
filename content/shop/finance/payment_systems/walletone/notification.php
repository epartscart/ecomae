<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
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

$wmi_merchant_id = $_POST["WMI_MERCHANT_ID"];
$WMI_PAYMENT_AMOUNT = $_POST["WMI_PAYMENT_AMOUNT"];
$WMI_PAYMENT_NO = $_POST["WMI_PAYMENT_NO"];



$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active`=1 WHERE `amount` = ? AND `id` = ? AND `active` = 0;');
if( $update_query->execute( array($WMI_PAYMENT_AMOUNT, $WMI_PAYMENT_NO) ) == true )
{
	// -----
	//Уведомление менеджерам магазинов
	$operation_id = $WMI_PAYMENT_NO;
	$amount = $WMI_PAYMENT_AMOUNT;
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
	// -----
	
	
	// -----
	//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
	$operation_id = $WMI_PAYMENT_NO;
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
	// -----
	
	
	
	exit("OK");
}
else
{
	exit("RETRY");
}
?>