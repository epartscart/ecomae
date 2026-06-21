<?php
/*
Серверный скрипт для получения информации о количестве не просмотренных сообщений в заказах и возвратах - для модуля инликации
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
$user_id = DP_User::getUserId();


if(isset($_GET['returns'])){
	$not_viewed_count_query = $db_link->prepare("SELECT COUNT(*) AS 'count' FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `return_id` IN(SELECT `id` FROM `shop_orders_returns` WHERE `user_id` = ?);");
}else{
	$not_viewed_count_query = $db_link->prepare("SELECT COUNT(*) AS 'count' FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `order_id` IN(SELECT `id` FROM `shop_orders` WHERE `user_id` = ?);");
}

$not_viewed_count_query->execute(array($user_id));
$not_viewed_count_record = $not_viewed_count_query->fetch();

$result["status"] = true;
$result["count"] = $not_viewed_count_record["count"];
exit(json_encode($result));
?>