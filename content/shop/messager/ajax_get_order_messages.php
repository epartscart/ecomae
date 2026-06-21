<?php
/*
Серверный срипт для получения сообщений по заказу
*/
error_reporting(0);
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
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Входные данные:
$order_id = $_GET["order_id"];


//Проверяем права на запуск
if( !empty($_GET["manager"]) )//Запрос от менеджера
{
    //Проверяем право менеджера
    if( ! DP_User::isAdmin())
    {
        $result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));//Вообще не является администратором бэкенда
    }


    // -------------------------------------------------------------------------------
    //Защита от CSRF-атак
    $csrf_check_admin = 1;
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
    // -------------------------------------------------------------------------------
}
else//Запрос от пользователя
{
    $user_id = DP_User::getUserId();

    if (isset($_GET["return_id"]))
    {
        $check_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_orders_returns` WHERE `user_id` = ? AND `id` = ?;');
        $check_query->execute( array($user_id, $_GET["return_id"]) );
    }
    else
    {
        $check_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_orders` WHERE `user_id` = ? AND `id` = ?;');
        $check_query->execute( array($user_id, $order_id) );
    }

    if( $check_query->fetchColumn() == 0)
    {
        $result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));
    }

    // -------------------------------------------------------------------------------
    //Защита от CSRF-атак
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
    // -------------------------------------------------------------------------------
}

//Разрешено ...


$messages = array();//Массив с сообщениями

if (isset($_GET["return_id"]))
{
    $messages_query = $db_link->prepare('SELECT * FROM `shop_orders_messages` WHERE `return_id` = ?;');
    $messages_query->execute( array($_GET["return_id"]) );
}
else
{
    $messages_query = $db_link->prepare('SELECT * FROM `shop_orders_messages` WHERE `order_id` = ?;');
    $messages_query->execute( array($order_id) );
}
while($message = $messages_query->fetch() )
{
    array_push($messages, array("time"=>date("d.m.Y H:i:s", $message["time"]), "is_customer"=>(boolean)$message["is_customer"], "text"=>$message["text"]) );
}


exit(json_encode($messages));
?>