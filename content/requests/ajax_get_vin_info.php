<?php
/*
Серверный скрипт для получения количества не просмотренных запросов
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
$user_id = DP_User::getUserId();



if($user_id <= 0)
{
	$result["status"] = false;
	$result["message"] = "Forbidden";
	$result["code"] = 501;
	exit(json_encode($result));
}



$not_viewed_count_query = $db_link->prepare("SELECT COUNT(*) AS 'count' FROM `users_vin` WHERE `user_id` = ? AND `viewed_customer` = 0;");
$not_viewed_count_query->execute( array($user_id) );
$not_viewed_count_record = $not_viewed_count_query->fetch();



$result["status"] = true;
$result["count"] = (int) $not_viewed_count_record["count"];
exit(json_encode($result));
?>