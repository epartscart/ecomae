<?php
/*
Серверный скрипт для сохранения комментария о пользователе
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
$user_id = $_POST["user_id"];
$comment = $_POST["comment"];


$user_query = $db_link->prepare('UPDATE `users` SET `comment` = ? WHERE `user_id` = ?');
if($user_query->execute( array($comment, $user_id) )){
	$result["status"] = true;
	$result["message"] = "Ok";
	$result["code"] = 0;
	exit(json_encode($result));
}else{
	$result["status"] = false;
	$result["message"] = "SQL Error";
	$result["code"] = 401;
	exit(json_encode($result));
}


?>