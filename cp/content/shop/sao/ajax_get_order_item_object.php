<?php
/*
Серверный скрипт для получения текущей информации по позиции заказа.
Скрипт используется для перерисовки позиции заказа
*/
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS

//-----------------------------------------------------------------------------------------------

//Получаем исходные данные:
$order_item_id = $_GET["order_item_id"];
$key = $_GET["key"];

//-----------------------------------------------------------------------------------------------

//Проверяем право на запуск:
if( $key != $DP_Config->tech_key )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Wrong key";
	exit(json_encode($answer));
}

//-----------------------------------------------------------------------------------------------

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
$csrf_check_admin = 1;
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//-----------------------------------------------------------------------------------------------

//Получаем данные запрошенной позиции
$SQL_order_item = "SELECT *,
(SELECT `color` FROM `shop_orders_items_statuses_ref` WHERE `id` = `shop_orders_items`.`status`) AS `status_color`,
(SELECT `name` FROM `shop_orders_items_statuses_ref` WHERE `id` = `shop_orders_items`.`status`) AS `status_name`,
(SELECT `name` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state`) AS `sao_state_name`,
(SELECT `color_background` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state`) AS `sao_state_color_background`,
(SELECT `color_text` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state`) AS `sao_state_color_text`
 FROM `shop_orders_items` WHERE `id` = ?;";

$order_item_query = $db_link->prepare($SQL_order_item);
$order_item_query->execute( array($order_item_id) );
$order_item_record = $order_item_query->fetch();

//-----------------------------------------------------------------------------------------------
//Формируем и возвращаем ответ

$answer = array();
$answer["status"] = true;
$answer["order_item_id"] = $order_item_id;
$answer["item"] = array();
$answer["item"]["status_color"] = $order_item_record["status_color"];
$answer["item"]["status_name"] = $order_item_record["status_name"];
$answer["item"]["sao"] = array();
$answer["item"]["sao"]["state_name"] = $order_item_record["sao_state_name"];
$answer["item"]["sao"]["state_color_background"] = $order_item_record["sao_state_color_background"];
$answer["item"]["sao"]["state_color_text"] = $order_item_record["sao_state_color_text"];
$answer["item"]["sao"]["message"] = $order_item_record["sao_message"];

//Получаем доступные SAO-действия для данного SAO-состояния, данного поставщика
$answer["item"]["sao"]["actions"] = array();
$SQL_sao_actions = "SELECT * FROM `shop_sao_actions` WHERE `id` IN (SELECT `action_id` FROM `shop_sao_states_types_actions_link` WHERE `state_type_id` = (SELECT `id` FROM `shop_sao_states_types_link` WHERE `state_id` = ? AND `interface_type_id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?) ) );";
$sao_actions_query = $db_link->prepare($SQL_sao_actions);
$sao_actions_query->execute( array($order_item_record["sao_state"], $order_item_record["t2_storage_id"]) );
while( $sao_action = $sao_actions_query->fetch() )
{
	array_push($answer["item"]["sao"]["actions"], array("id"=>$sao_action["id"], "name"=>$sao_action["name"], "btn_class"=>$sao_action["btn_class"], "fontawesome"=>$sao_action["fontawesome"]) );
}

exit(json_encode($answer));
//-----------------------------------------------------------------------------------------------
?>