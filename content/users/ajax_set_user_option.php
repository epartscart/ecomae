<?php
//Серверный скрипт для асинхронной записи настроек пользователя
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

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

//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Фильтр. Не стоит записывать всё-попало. Поэтому, добавляем сюда только те ключи, которые могут использоваться в функциях сайта
$key_filter = array();//Фильтр ключей
$key_filter[] = "propucts_request_0";//Этот ключ используется для каталога при поиске товаров по наименованию
//Получаем список категорий встроенного каталога - для него могут быть настройки вида "propucts_request_<category_id>"
$key_filter[] = "selected_manufacturer";//Для ЧПУ-проценки
$catalogue_categories_query = $db_link->prepare("SELECT `id` FROM `shop_catalogue_categories`");
$catalogue_categories_query->execute();
while( $category = $catalogue_categories_query->fetch() )
{
	$key_filter[] = "propucts_request_".$category["id"];
}
//Фильтруем
if( array_search($_POST["key"], $key_filter) === false )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit(json_encode($answer));
}


//Дошли досюда - записываем настройку в БД
if(DP_User::set_user_option($_POST["key"], $_POST["value"]))
{
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "Ok";
	exit(json_encode($answer));
}
else
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Error";
	exit(json_encode($answer));
}
?>