<?php
header('Content-Type: text/html; charset=utf-8');
/**
 * Скрипт для автоуведомления менеджера об оплате и для изменения статуса заказа
 * 
 * Этот скрипт вызывает PayMaster после платежа
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


//Проверяем доступ вызывающей стороны (Это обязательно должен быть PayMaster)
/*
Должны быть правильными:
LMI_MERCHANT_ID - Идентификатор сайта в системе PayMaster
LMI_SYS_PAYMENT_ID - Номер платежа в системе PayMaster
LMI_PAYMENT_AMOUNT - Сумма платежа, заказанная продавцом
LMI_PAYMENT_NO - номер заказа в нашей БД
*/


$operation_id = $_POST["LMI_PAYMENT_NO"];


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$login = $paysystem_parameters["paymaster_login"];
$nonce = time();
$paymentid = $_POST["LMI_SYS_PAYMENT_ID"];
$password = $paysystem_parameters["paymaster_password"];

$hash = "$login;$password;$nonce;$paymentid";
$hash = base64_encode(sha1($hash, true));


//Делаем запрос в Paymaster для получения информации по транзакции
$ch = curl_init();
curl_setopt($ch, CURLOPT_URL, "https://paymaster.ru/partners/rest/getpayment?login=$login&nonce=$nonce&paymentid=$paymentid&hash=$hash");
curl_setopt($ch, CURLOPT_HEADER, 0);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
$result  = curl_exec($ch);
curl_close($ch);
$result = json_decode($result, true);

//Статус есть
if($result["ErrorCode"] == 0)
{
    if($result["Payment"]["State"] == "COMPLETE")
    {
		//Проверяем заказ - он должен быть в статусе 1
		$check_operation = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
		$check_operation->execute( array($result["Payment"]["SiteInvoiceID"]) );
		if($check_operation->fetchColumn() > 0)
		{
			$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
			$update_query->execute( array($result["Payment"]["SiteInvoiceID"]) );	
			
			// -----
			//Уведомление менеджерам магазинов
			$operation_id = $result["Payment"]["SiteInvoiceID"];
			$amount = $_POST["LMI_PAYMENT_AMOUNT"];
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
			// -----
			
			// -----
			//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
			$operation_id = $result["Payment"]["SiteInvoiceID"];
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
			// -----
		}
    }
}
?>