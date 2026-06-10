<?php
/*
Серверный скрипт для получения списка отзывов о товаре
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

$evaluation_query = json_decode($_POST["evaluation_query"], true);//Объект запроса

//Формируем SQL-запрос
$SQL = "SELECT *, (SELECT `data_value` FROM `users_profiles` WHERE `user_id` = `shop_products_evaluations`.`user_id` AND `data_key`='name') AS `user_name`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ".(int)$evaluation_query["product_id"]." ) AS `count_total` FROM `shop_products_evaluations` WHERE `product_id` = ".(int)$evaluation_query["product_id"];

//Фильтр по оценкам
if($evaluation_query["mark"] != 0)
{
	$SQL = $SQL . " AND `mark` = ".(int)$evaluation_query["mark"];
}

//Сортировка
if($evaluation_query["asc_desc"] == "asc")
{
	$SQL = $SQL . " ORDER BY `id` ASC";
}
else if($evaluation_query["asc_desc"] == "desc")
{
	$SQL = $SQL . " ORDER BY `id` DESC";
}
else
{
	exit();
}



//Страница
$start_from  = $DP_Config->list_page_limit*(int)$evaluation_query["page"];
$limit = $DP_Config->list_page_limit;
$SQL = $SQL . " LIMIT ".(int)$start_from.", ".(int)$limit;


//Запрос
$evaluations = array();//Список для отзывов
$pages_total = 0;//Количество страниц
$evaluations_query = $db_link->prepare($SQL);
$evaluations_query->execute();
while( $evaluation = $evaluations_query->fetch(PDO::FETCH_ASSOC) )
{
	$user_name = translate_str_by_id(4091);
	if($evaluation["hide_user_data"] != 1)
	{
		$user_name = $evaluation["user_name"];
	}
	
	
	//Количество страниц:
	if( $pages_total == 0)
	{
		$pages_total = (int)($evaluation["count_total"] / $DP_Config->list_page_limit);
		if( $evaluation["count_total"] % $DP_Config->list_page_limit != 0 )
		{
			$pages_total++;
		}
	}
	
	
	array_push( $evaluations, array("id"=>$evaluation["id"], "product_id"=>$evaluation["product_id"], "mark"=>$evaluation["mark"], "text_plus"=>$evaluation["text_plus"], "text_minus"=>$evaluation["text_minus"], "text"=>$evaluation["text"], "time"=> date("d.m.Y", $evaluation["time"])." ".date("G:i", $evaluation["time"]), "user_name" => $user_name) );
}





$anser = array();
$anser["status"] = true;
$anser["evaluations"] = $evaluations;
$anser["pages_total"] = $pages_total;
exit( json_encode($anser) );
?>