<?php
/*
Серверный скрипт для добавления комментрия в лог заказа
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
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
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


//Получаем данные:
$order_id = $_GET["order_id"];
$text = htmlentities($_GET["text"]);
$user_id = DP_User::getAdminId();




if( $db_link->prepare("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?,?,?,?,?);")->execute( array($order_id, time(), $user_id, 1, $text) ) != true)
{
	$result["status"] = false;
	$result["message"] = "SQL Error";
	$result["code"] = 401;
	exit(json_encode($result));
}
else
{
	$result["status"] = true;
	$result["message"] = "Ok";
	$result["code"] = 0;
	exit(json_encode($result));
}
?>