<?php
/**
 * Серверный скрипт для выставления флага просмотра vin запроса менеджером
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
if( ! DP_User::isAdmin() )
{
	$result["status"] = false;
	$result["message"] = "Forbidden";
	$result["code"] = 501;
	exit(json_encode($result));//Вообще не является администратором бэкенда
}





//Получаем исходные данные
$request_object = json_decode($_POST["request_object"], true);

$SQL = "UPDATE `users_vin` SET `viewed` = ". (int) $request_object["viewed_flag"] ." WHERE `id` IN (". str_replace(array('[',']'), '', $request_object["vins"]) .");";
$query = $db_link->prepare($SQL);

if( $query->execute() != true )
{
	$result["status"] = false;
	$result["message"] = "SQL error";
	exit(json_encode($result));
}
else
{
	$result["status"] = true;
	$result["message"] = '';
	exit(json_encode($result));
}
?>