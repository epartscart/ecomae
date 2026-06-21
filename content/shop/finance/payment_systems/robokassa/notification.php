<?php
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


$operation_id = $_POST["InvId"];


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$mrh_pass2 = $paysystem_parameters["robokassa_pswd2"];  // merchant pass2 here
$mrh_login = $paysystem_parameters["robokassa_shop_id"];  // merchant pass1 here


// HTTP parameters:
$out_summ = $_POST["OutSum"];
$inv_id = $_POST["InvId"];
$crc = $_POST["SignatureValue"];


$my_crc = strtoupper(md5("$out_summ:$inv_id:$mrh_pass2"));

//Проверяем подпись
if (strtoupper($my_crc) != strtoupper($crc))
{
    exit("Sign error");
}
//Проверяем операцию (существование и цену)
$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active`=0;');
$operation_query->execute( array($inv_id) );
$operation = $operation_query->fetch();
if($operation==false)
{
    exit("No operation");
}
//Сумма:
if($operation["amount"]/$out_summ > 1.1)
{
    exit("Wrong amount");
}


//Активируем операцию:
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
$update_query->execute( array($inv_id) );

// -----
//Уведомление менеджерам магазинов
$operation_id = $inv_id;
$amount = $out_summ;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
// -----



// -----
//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
$operation_id = $inv_id;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
// -----
?>