<?php
/*
Серверный скрипт авторизации от имени пользователя.
*/
header('Content-Type: application/json;charset=utf-8;');
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
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");

// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Скрипт могут запускать только администраторы
if(! DP_User::isAdmin() )
{
	$result = array();
	$result["status"] = false;
	$result["message"] = "Forbidden";
	exit(json_encode($result));//Вообще не является администратором бэкенда
}


$user_id = $_POST["user_id"];
$time = time();
$session_succession = md5($user_id.$time.$DP_Config->secret_succession."ok".$_SERVER["REMOTE_ADDR"]);//Код сессии - собираем его из логина, текущего дампа времени и секретной последовательности


//Ключ защиты от CSRF-атак:
$csrf_guard_key = sha1( $DP_Config->secret_succession . $session_succession . $_SERVER["REMOTE_ADDR"] . $_SERVER["HTTP_USER_AGENT"] );


//Записываем сеcсию в БД
$db_link->prepare("INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `csrf_guard_key`) VALUES (?, ?, ?, ?, ?);")->execute( array($session_succession, $user_id, $time, '', $csrf_guard_key) );

//Записываем сессию в куки:
$cookietime = time()+9999999;//Запоминаем пользователя на долго
setcookie("session", $session_succession, $cookietime, "/", '',false,true);
setcookie("u_id", $user_id, $cookietime, "/", '',false,true);



$result = array();
$result["status"] = true;
$result["message"] = "Ok";
exit(json_encode($result));//Вообще не является администратором бэкенда
?>