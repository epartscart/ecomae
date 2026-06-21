<?php
//Серверный скрипт для простановки цен
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
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'No DB connect';
	exit(json_encode($answer));
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

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Проверяем доступ в панель управления
if( ! DP_User::isAdmin())
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Forbidden';
	exit(json_encode($answer));
}


// -------------------------------------------------------------------------------------------

//Проверяем наличие аргументов
if( !isset( $_POST["price_id"] ) || !isset( $_POST["start"] ) || !isset( $_POST["base_mark"] ) || !isset( $_POST["plus_minus"] ) || !isset( $_POST["percent"] ) || !isset( $_POST["prices"] ) || !isset( $_POST["from"] ) || !isset( $_POST["items_per_time"] ) || !isset($_POST["end"]) )
{
	exit;
}

//Если это первая порция, выставляем флаг reviewed в 0
if( $_POST["start"] == 1 )
{
	if( ! $db_link->prepare("UPDATE `shop_docpart_prices_data` SET `reviewed` = ? WHERE `price_id` = ?;")->execute( array(0, $_POST["price_id"]) ) )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(3731);
		exit(json_encode($answer));
	}
}


//Список прайс-листов, откуда брать цены
$prices = json_decode($_POST["prices"], true);



//Функция для MySQL:
$price_func = "";
if( $_POST["base_mark"] == "min" )
{
	$price_func = "MIN";
}
else if( $_POST["base_mark"] == "max" )
{
	$price_func = "MAX";
}
else if( $_POST["base_mark"] == "middle" )
{
	$price_func = "AVG";
}
else
{
	exit;
}



//Подготовленный запрос для оптимизации
$review_query = $db_link->prepare("UPDATE `shop_docpart_prices_data` SET `price` = ?, `reviewed` = ? WHERE `id` = ?;");



//Получаем набор позиций прайс-листа для текущего вызова
$items_query = $db_link->prepare("SELECT `id`, `article`, `manufacturer` FROM `shop_docpart_prices_data` WHERE `price_id` = :price_id ORDER BY `id` LIMIT :items_per_time OFFSET :from;");

$price_id = (int) $_POST["price_id"];
$from = (int) $_POST["from"];
$items_per_time = (int) $_POST["items_per_time"];

$items_query->bindParam(':price_id', $price_id, PDO::PARAM_INT);
$items_query->bindParam(':from', $from, PDO::PARAM_INT);
$items_query->bindParam(':items_per_time', $items_per_time, PDO::PARAM_INT);
$items_query->execute();

while( $item = $items_query->fetch() )
{	
	//Получаем варианты написания производителя
	$manufacturers = array();
	$manufacturers[] = $item["manufacturer"];
	$manufacturers_query = $db_link->prepare("SELECT * FROM `shop_docpart_manufacturers` WHERE `name` = ?;");
	$manufacturers_query->execute( array($item["manufacturer"]) );
	$manufacturer_record = $manufacturers_query->fetch();
	if( $manufacturer_record != false )
	{
		$manufacturer_id = $manufacturer_record["id"];
		
		//Это основной производитель. Получаем его синонимы
		$manufacturers_synonyms_query = $db_link->prepare("SELECT * FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?;");
		$manufacturers_synonyms_query->execute( array($manufacturer_id) );
		while( $synonym = $manufacturers_synonyms_query->fetch() )
		{
			$manufacturers[] = $synonym["synonym"];
		}
	}
	else
	{
		//Пробуем найти в таблице синонимов
		$manufacturers_synonyms_query = $db_link->prepare("SELECT * FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ?;");
		$manufacturers_synonyms_query->execute( array($item["manufacturer"]) );
		$manufacturers_synonyms_record = $manufacturers_synonyms_query->fetch();
		if( $manufacturers_synonyms_record != false )
		{
			$manufacturer_id = $manufacturers_synonyms_record["manufacturer_id"];
		
			$manufacturers_synonyms_query = $db_link->prepare("SELECT * FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?;");
			$manufacturers_synonyms_query->execute( array($manufacturer_id) );
			while( $synonym = $manufacturers_synonyms_query->fetch() )
			{
				$manufacturers[] = $synonym["synonym"];
			}
		}
	}
	
	//Получаем цену по функции из других прайс-листов
	$the_same_query = $db_link->prepare( "SELECT ".$price_func."(`price`) AS `price` FROM `shop_docpart_prices_data` WHERE `price_id` IN (".str_repeat('?,', count($prices)-1 )."?) AND `article` = ? AND `manufacturer` IN (".str_repeat('?,', count($manufacturers)-1 )."?) AND `price` > ?;" );
	$the_same_query->execute( array_merge($prices, array($item['article']), $manufacturers, array(0) ) );
	$the_same_record = $the_same_query->fetch();
	
	if( $the_same_record != false )
	{
		if( $the_same_record["price"] != NULL )
		{
			if( $_POST["percent"] > 0 )
			{
				$percent_value = $the_same_record["price"]*$_POST["percent"]/100;
				
				
				if( $_POST["plus_minus"] == "plus" )
				{
					$price_to_item = $the_same_record["price"] + $percent_value;
				}
				else if( $_POST["plus_minus"] == "minus" )
				{
					$price_to_item = $the_same_record["price"] - $percent_value;
				}
				
				$the_same_record["price"] = $price_to_item;
			}
			
			
			if( $the_same_record["price"] > 0 )
			{
				if( ! $review_query->execute( array($the_same_record["price"], 1, $item["id"]) ) )
				{
					$answer = array();
					$answer["status"] = false;
					$answer["message"] = translate_str_by_id(3732);
					exit(json_encode($answer));
				}
			}
		}
	}
}


//Если это была последняя партия, то, нужно получить результат по всему процессу
if( $_POST["end"] == 1 )
{
	$result_query = $db_link->prepare("SELECT COUNT(*) AS `items_count`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id`=? AND `reviewed` =? ) AS `reviewed_yes`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id`=? AND `reviewed` =? ) AS `reviewed_no` FROM `shop_docpart_prices_data` WHERE `price_id`=?;");
	$result_query->execute( array( $_POST["price_id"], 1, $_POST["price_id"], 0, $_POST["price_id"] ) );
	
	
	$answer = array();
	$answer["status"] = true;
	$answer["result"] = $result_query->fetch();
	exit(json_encode($answer));
}


//Не последняя партия, выдаем простой ответ
$answer = array();
$answer["status"] = true;
exit(json_encode($answer));
?>