<?php
header('Content-Type: text/html; charset=utf-8');
/**
 * Скрипт для автоуведомления менеджера об оплате и для изменения статуса счета
 * 
 * Этот скрипт вызывает Яндекс-Деньги после платежа
*/

//Соединение с БД
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

//Почтовый обработчик
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer.php");



$operation_id = $_POST['operation_id'];// id счета в системе Яндекс-Деньги


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$receiver = $paysystem_parameters["receiver"];// счет
$notification = $paysystem_parameters["notification"];// секретное слово


if(!empty($_POST))
{
	
	// Получаем данные от Яндекс-Деньги
	$notification_type  = $_POST['notification_type'];// Для кошелька — p2p-incoming. Для карты — card-incoming.
	$operation_id 		= $_POST['operation_id'];// id счета в системе Яндекс-Деньги
	$amount 			= $_POST['amount'];// сумма зачисленная на счет Яндекс-Деньги
	$currency 			= $_POST['currency'];// код валюты всегда 643 (рубль РФ согласно ISO 4217).
	$sender 			= $_POST['sender'];// номер счета отправителя
	$datetime 			= $_POST['datetime'];// номер счета отправителя
	$codepro 			= $_POST['codepro'];// перевод защищен кодом протекции.
	$label 				= $_POST['label'];// id счета в нашей базе
	$sha1_hash			= $_POST['sha1_hash'];// хеш проверочный
	
	// Формируем хеш для проверки
	// notification_type&operation_id&amount&currency&datetime&sender&codepro&notification_secret&label
	$hash = "$notification_type&$operation_id&$amount&$currency&$datetime&$sender&$codepro&$notification&$label";
	$hash = sha1($hash, true);
	$hash = bin2hex($hash);
	
	
	// Сравниваем Хеши
	if($sha1_hash === $hash)
	{
		$payment_id = (int) $label;// id счета в нашей базе

		// Меняем статус
		$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ? AND `active` = 0;');

		if( $update_query->execute( array($payment_id) ) != true)
		{
			//Ошибка
		}
		
		//Получаем сумму
		$amount_query = $db_link->prepare('SELECT `amount` FROM `shop_users_accounting` WHERE `id` = ?;');
		$amount_query->execute( array($payment_id) );
		$amount_record = $amount_query->fetch();
		$amount = $amount_record["amount"];
		
		// -----
		//Уведомление менеджерам магазинов
		$operation_id = $payment_id;
		$amount = $amount;
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
		// -----
		
		// -----
		//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
		$operation_id = $payment_id;
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
		// -----
		
		header('Status: 200 Ok');
	}
}
else
{
	exit;
}
?>