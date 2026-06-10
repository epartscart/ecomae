<?php
/*
Сервеный скрипт выставления статуса просмотра заказов
*/
header('Content-Type: application/json;charset=utf-8;');
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



//Получаем исходные данные
$request_object = json_decode($_POST["request_object"], true);

$orders = $request_object["orders"];
$orders_str = "";
$binding_values = array();
for($i=0;$i<count($orders);$i++)
{
	if( $i > 0 )
	{
		$orders_str = $orders_str.",";
	}
	$orders_str = $orders_str."?";
	
	array_push($binding_values, $orders[$i]);
}




array_unshift($binding_values, $request_object["viewed_flag"]);

$SQL = "UPDATE `shop_orders_viewed` SET `viewed_flag` = ? WHERE `order_id` IN ($orders_str);";

if( ! $db_link->prepare($SQL)->execute($binding_values) )
{
	$result["status"] = false;
	$result["message"] = "SQL error";
	exit(json_encode($result));
}
else
{
	$result["status"] = true;
	$result["message"] = $SQL;
	exit(json_encode($result));
}
?>