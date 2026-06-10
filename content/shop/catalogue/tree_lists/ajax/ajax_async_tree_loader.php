<?php
/*
Скрипт для асинхронной загрузки ветви дерева
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


//Входные параметры:
$tree_list_id = $_GET["tree_list_id"];
$parent_id = $_GET["parent_id"];

//Возвращаемый объект
$items = array("parent"=>$parent_id, "data"=>array());


//Делаем запрос и формируем возвращаемый объект
$items_query = $db_link->prepare('SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id AND `parent` = :parent ORDER BY `order`;');
$items_query->bindValue(':tree_list_id', $tree_list_id);
$items_query->bindValue(':parent', $parent_id);
$items_query->execute();
while( $item = $items_query->fetch() )
{
	if( $item["count"] > 0 )
	{
		array_push($items["data"], array("id"=>$item["id"], "value"=>translate_str_by_id($item["value"]), "webix_kids"=>$item["count"] ));
	}
	else
	{
		array_push($items["data"], array("id"=>$item["id"], "value"=>translate_str_by_id($item["value"]) ));
	}
	
}

exit( json_encode($items) );
?>