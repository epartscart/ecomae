<?php
header('Content-Type: application/json;charset=utf-8;');
/*Серверный скрипт для получения информации по заказам - для модуля инликации*/
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $result["status"] = false;
	$result["message"] = "DB connect error";
	$result["code"] = 502;
	exit(json_encode($result));
}
$db_link->query("SET NAMES utf8;");

// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Проверяем право менеджера
if( ! DP_User::isAdmin())
{
	$result["status"] = false;
	$result["message"] = "Forbidden";
	$result["code"] = 501;
	exit(json_encode($result));//Вообще не является администратором бэкенда
}


$request_object = json_decode($_POST["request_object"], true);
$user_id = $request_object["user_id"];

$not_viewed_count_query = $db_link->prepare("SELECT COUNT(`id`) AS `count` FROM `shop_orders_viewed` WHERE `user_id` = ? AND `viewed_flag` = 0;");
$not_viewed_count_query->execute( array($user_id) );
$not_viewed_count_record = $not_viewed_count_query->fetch();

$result["status"] = true;
$result["message"] = $not_viewed_count_record["count"];
exit(json_encode($result));//Вообще не является администратором бэкенда
?>