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


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Почтовый обработчик
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer.php");


$operation_id = (int)$_POST["pg_order_id"];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$apiKEY = $paysystem_parameters["apiKEY"];// Ключ

// HTTP parameters:
$pg_result = (int)$_POST["pg_result"];
$orderId = (int)$_POST["pg_order_id"];
$amount = (float)$_POST["pg_amount"];


//Проверяем операцию (существование и цену)
if(empty($pg_result)){
	 exit("No operation");
}
$operation_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `amount` = ? AND `active`=0;');
$operation_query->execute( array($orderId, $amount) );
if($operation_query->fetchColumn() == 0)
{
    exit("No operation");
}

//Активируем операцию:
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
$update_query->execute( array($orderId) );


// -----
//Уведомление менеджерам магазинов
$operation_id = $orderId;
$amount = $amount;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
// -----


// -----
//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
$operation_id = $orderId;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
// -----



// Ответ платежной системе:
$pg_salt = 'bdbsg4g4r'.time().'fn166hd4h';
$pg_status = 'ok';
$pg_description = translate_str_by_id(4374);
// Формируем массив с данными что бы затем его отсортировать в алфовитном порядке так как это требуется для формирования подписи
$arr = array(
		'pg_salt' 				=> $pg_salt,
		'pg_status' 			=> $pg_status,
		'pg_description' 		=> $pg_description
);

ksort($arr);

// Формирование подписи
$str = '';
foreach($arr as $v){
	$str .= $v .';';
}
$name_script = '';
$pg_sig = md5($name_script .';'. $str . $apiKEY);
echo '
<?xml version="1.0" encoding="utf-8"?>
<response>
<pg_salt>'. $pg_salt .'</pg_salt>
<pg_status>'. $pg_status .'</pg_status>
<pg_description>'. $pg_description .'</pg_description>
<pg_sig>'. $pg_sig .'</pg_sig>
</response>';
?>