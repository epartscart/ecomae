<?php
/**
 * Серверный срипт для получения сообщений запроса пользователя
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
    $result = array();
	$result["status"] = false;
	$result["message"] = "No DB Connect";
	$result["code"] = "no_db_connect";
	exit(json_encode($result));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Входные данные:
$vin_id = (int) $_GET["vin_id"];


//Проверяем права на запуск
if( !empty($_GET["manager"]) )//Запрос от менеджера
{
	//Проверяем право менеджера
    if( ! DP_User::isAdmin() )
    {
        $result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));//Вообще не является администратором бэкенда
    }
}
else//Запрос от пользователя
{
	$user_id = DP_User::getUserId();
	
	if( $user_id <= 0 )
    {
        $result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));
    }
	
	$vin_list_query = $db_link->prepare("SELECT `id` FROM `users_vin` WHERE `id` = ? AND `user_id` = ?;");
	$vin_list_query->execute( array($vin_id, $user_id) );
	$vin_list_array = $vin_list_query->fetch();
	
	if( empty($vin_list_array) )
	{
		$result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));
	}
}


//Разрешено ...


$messages = array();//Массив с сообщениями

$messages_query = $db_link->prepare("SELECT * FROM `users_vin_messages` WHERE `vin_id` = ?;");
$messages_query->execute( array($vin_id) );
while($message = $messages_query->fetch())
{
	array_push($messages, array("time"=>date("d.m.Y H:i:s", $message["time"]), "is_customer"=>(boolean)$message["is_customer"], "text"=>$message["text"]) );
}



exit(json_encode($messages));
?>