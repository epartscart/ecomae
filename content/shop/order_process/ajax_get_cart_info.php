<?php
/*
Серверный скрипт для получения данных по своей корзине
*/
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//Указатель валюты
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/general/get_currency_indicator.php");

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();
if($user_id > 0)
{
	//Поля для авторизованного пользователя
	$session_id = 0;
}
else
{
	//Поля для НЕавторизованного пользователя
	$session_record = DP_User::getUserSession();
	if($session_record == false)
	{
		$result = array();
		$result["status"] = false;
		$result["code"] = "incorrect_session";
		$result["message"] = translate_str_by_id(4460);
		exit(json_encode($result));
	}
	
	$session_id = $session_record["id"];
}

$cart_items_count = 0;
$cart_items_sum = 0;

//Получаем содержимое его корзины из базы данных
$cart_records_query = $db_link->prepare('SELECT COUNT(`id`) AS `count`, IFNULL(SUM(`price`*`count_need`), 0) AS `sum` FROM `shop_carts` WHERE `user_id` = ? AND `session_id`=?;');
$cart_records_query->execute( array($user_id,$session_id) );
$cart_record = $cart_records_query->fetch();
if( $cart_record != false )
{
	$cart_items_count = $cart_record["count"];
	$cart_items_sum = $cart_record["sum"];
}
$cart_items_sum = number_format($cart_items_sum, 2, '.', '');


//Данные по корзине получены

//Обработка строки с ценой
if($cart_items_sum > 0)
{
	if($DP_Config->currency_show_mode == "sign_before")
	{
		$cart_items_sum = $currency_indicator." ".$cart_items_sum;
	}
	else
	{
		$cart_items_sum = $cart_items_sum." ".$currency_indicator;
	}
}
else
{
	$cart_items_sum = translate_str_by_id(4494);
}


$answer = array();
$answer["cart_items_sum"] = $cart_items_sum;
$answer["cart_items_count"] = $cart_items_count;
exit( json_encode($answer) );
?>