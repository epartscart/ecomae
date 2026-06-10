<?php
header('Content-Type: application/json;charset=utf-8;');
/*
Скрипт для асинхронной загрузки ветви дерева в виде простого массива
*/
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


//Входные параметры:
$tree_list_id = null;
if( isset( $_GET["tree_list_id"] ) )
{
	$tree_list_id = (int)$_GET["tree_list_id"];
}
$parent_id = null;
if( isset($_GET["parent_id"]) )
{
	$parent_id = (int)$_GET["parent_id"];
}
$int_1 = null;
if( isset($_GET["int_1"]) )
{
	$int_1 = (int)$_GET["int_1"];//Используется для асинхронных запросов, чтобы понимать, от какого запроса пришел ответ
}
$int_2 = null;
if( isset($_GET["int_2"]) )
{
	$int_2 = (int)$_GET["int_2"];
}
$int_3 = null;
if( isset( $_GET["int_3"] ) )
{
	$int_3 = (int)$_GET["int_3"];
}


//Возвращаемый объект
$items = array("data"=>array(), "int_1"=>$int_1, "int_2"=>$int_2, "int_3"=>$int_3);


//Делаем запрос и формируем возвращаемый объект
$items_query = $db_link->prepare('SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id AND `parent` = :parent ORDER BY `order`;');
$items_query->bindValue(':tree_list_id', $tree_list_id);
$items_query->bindValue(':parent', $parent_id);
$items_query->execute();
while( $item = $items_query->fetch() )
{
	array_push($items["data"], array("id"=>$item["id"], "value"=>translate_str_by_id($item["value"]), "webix_kids"=>$item["count"], "data"=>array() ));
}

exit( json_encode($items) );
?>