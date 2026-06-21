<?php
/*
Обязательные поля:
eshopId - Номер сайта участника
recipientCurrency - Валюта (USD, RUR, EUR, UAH)
sekret - Секретное слово

Поля формы для оповещения:
successUrl 
failUrl


Настройки на сайте платежной системы:
1. Скрипт notification.php
2. Секретное слово
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

//Почтовый обработчик
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer.php");


$operation_id = (int)$_POST["orderId"];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$eshopId = $paysystem_parameters["eshopId"];// ID магазина
$recipientCurrency = $paysystem_parameters["recipientCurrency"];// Валюта
$sekret = trim($paysystem_parameters["sekret"]);// секретное слово



// HTTP parameters:
$eshopId_post = $_POST["eshopId"];
$orderId = (int)$_POST["orderId"];
$serviceName_post = $_POST["serviceName"];
$recipientAmount_post = $_POST["recipientAmount"];
$eshopAccount_post = $_POST["eshopAccount"];
$recipientCurrency_post = $_POST["recipientCurrency"];
$paymentStatus_post = $_POST["paymentStatus"];
$userName_post = $_POST["userName"];
$userEmail_post = $_POST["userEmail"];
$paymentData_post = $_POST["paymentData"];

$hash_post = $_POST["hash"];

//Проверяем операцию (существование и цену)
$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active`=0;');
$operation_query->execute( array($orderId) );
$record = $operation_query->fetch();
if($record == false)
{
    exit("No operation");
}


//Формируем hash
$amount = number_format($record['amount'], 2, '.', '');
$hash = $eshopId .'::'. $orderId .'::'. $serviceName_post .'::'. $eshopAccount_post .'::'. $amount .'::'. $recipientCurrency .'::'. $paymentStatus_post .'::'. $userName_post .'::'. $userEmail_post .'::'. $paymentData_post .'::'. $sekret;

$hash_str = $hash;
$hash = md5($hash);
$hash = strtolower($hash);

//Проверяем подпись
if ($hash_post !== $hash)
{
    exit("hash error");
}



//Активируем операцию:
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
$update_query->execute( array($orderId) );


// -----
//Уведомление менеджерам магазинов
$operation_id = $orderId;
$amount = $recipientAmount_post;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
// -----


// -----
//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
$operation_id = $orderId;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
// -----


header('Status: 200 Ok');
?>