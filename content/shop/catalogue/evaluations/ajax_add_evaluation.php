<?php
/*
Серверный скрипт добавления отзыва о товаре
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
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------



//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();


//ОБЪЕКТ ОТЗЫВА:
$evaluation_object = json_decode($_POST["evaluation_object"], true);




//Проверяем авторизацию
if($user_id == 0)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4088);
	exit(json_encode($answer));
}


//Проверяем наличие отзыва от данного пользователя о данном товаре
$check_allready_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_products_evaluations` WHERE `user_id` = :user_id AND `product_id` = :product_id;');
$check_allready_query->bindValue(':user_id', $user_id);
$check_allready_query->bindValue(':product_id', $evaluation_object["product_id"]);
$check_allready_query->execute();

if( $check_allready_query->fetchColumn() > 0)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4089);
	exit(json_encode($answer));
}


$product_id = (int)$evaluation_object["product_id"];
$mark = (int)$evaluation_object["mark"];
$text_plus = htmlentities($evaluation_object["text_plus"]);
$text_minus = htmlentities($evaluation_object["text_minus"]);
$text = htmlentities($evaluation_object["text"]);
$hide_user_data = (int)$evaluation_object["hide_user_data"];

//Добавляем отзыв
$sql_result = $db_link->prepare('INSERT INTO `shop_products_evaluations` (`product_id`, `mark`, `text_plus`, `text_minus`, `text`, `user_id`, `time`, `hide_user_data`) VALUES (:product_id, :mark, :text_plus, :text_minus, :text, :user_id, :time, :hide_user_data);');
$sql_result->bindValue(':product_id', $product_id);
$sql_result->bindValue(':mark', $mark);
$sql_result->bindValue(':text_plus', $text_plus);
$sql_result->bindValue(':text_minus', $text_minus);
$sql_result->bindValue(':text', $text);
$sql_result->bindValue(':user_id', $user_id);
$sql_result->bindValue(':time', time());
$sql_result->bindValue(':hide_user_data', $hide_user_data);



if($sql_result->execute())
{
	$answer = array();
	$answer["status"] = true;
	exit(json_encode($answer));
}
else
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4090);
	exit(json_encode($answer));
}
?>