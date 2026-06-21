<?php
/**
Скрипт робота SAO
*/
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS

$key = $_GET["key"];


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

//-----------------------------------------------------------------------------------------------

//Получаем позиции заказов, у которых есть задание роботу
$SQL_orders_items_for_robot = "SELECT `id`, `sao_robot` FROM `shop_orders_items` WHERE `sao_robot` > 0;";
$orders_items_query = $db_link->prepare($SQL_orders_items_for_robot);
$orders_items_query->execute();
while( $item = $orders_items_query->fetch() )
{
	$ch = curl_init();
	curl_setopt($ch, CURLOPT_URL, $DP_Config->domain_path.$DP_Config->backend_dir."/content/shop/sao/ajax_exec_action.php?order_item_id=".$item["id"]."&sao_action_id=".$item["sao_robot"]."&key=".urlencode($DP_Config->tech_key) );
	curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
	curl_setopt($ch, CURLOPT_HEADER, 0);
	curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
	$curl_result = curl_exec($ch);
	curl_close($ch);
	
	//Можем обработать результат аналогично страничному процессу.
	/*
	$curl_result = json_decode($curl_result, true);
	if($curl_result["status"] == true)
	{
		//Успех
	}
	else
	{
		//Ошибка
		//$curl_result["sao_action_message"] - можем записать сообщение куда-нибудь
	}
	*/
}
?>