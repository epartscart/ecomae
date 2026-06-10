<?php

set_time_limit(600);

// ini_set('error_reporting', E_ALL);
// ini_set('display_errors', 1);
// ini_set('display_startup_errors', 1);

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
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


$answer = array('status'=>false, 'message' => translate_str_by_key('1711375224_1_5f735d1486aa51eb9a61df1cd635a0fb'));

/********************************************************************************************************************** */
/********************************************************************************************************************** */

//Сначала выставляем всем товарам начальные значения
$ITEMS_UPDATE_SQL = "
UPDATE
`shop_catalogue_products`
SET
	`min_limit_status` = 0
WHERE
	`min_limit_status` != '0'
;";

$items_update_query = $db_link->prepare($ITEMS_UPDATE_SQL);
$items_update_query->execute();


/********************************************************************************************************************** */
/********************************************************************************************************************** */

$SQL = "SELECT 
`shop_catalogue_products`.`id` AS `product_id`,
`shop_catalogue_products`.`category_id` AS `product_category_id`,
`shop_catalogue_products`.`caption`,
`shop_catalogue_products`.`min_limit`,
`shop_catalogue_products`.`min_limit_enable`,
`shop_storages_data`.`storage_id`,
`shop_storages_data`.`product_id` AS `storage_product_id`,
`shop_storages_data`.`category_id`,
`shop_storages_data`.`price`,
`shop_storages_data`.`exist`,
`shop_storages_data`.`reserved`,
`shop_storages_data`.`issued`
FROM `shop_catalogue_products` LEFT JOIN `shop_storages_data` 
ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` 
WHERE `shop_catalogue_products`.`min_limit_enable` = '1'
;";

$items_query = $db_link->prepare($SQL);
$items_query->execute();

$all_products = array();
$all_limited = array();
while($row = $items_query->fetch(PDO::FETCH_ASSOC)) {

	$product_id          = $row["product_id"];
	$category_id         = $row["category_id"];
	$product_category_id = $row["product_category_id"];
	$storage_id          = $row["storage_id"];

	if($product_category_id !== $category_id) continue;

	if(isset($all_products[$product_id]['storages'][$storage_id])) {
		$all_products[$product_id]['storages'][$storage_id] += $row['exist'];
	}
	else
	{
		$all_products[$product_id]['storages'][$storage_id] = $row['exist'];
	}

	$all_products[$product_id]['min_limit'] = $row['min_limit'];
}


// echo "<pre>";
// print_r($all_products);
// echo "</pre>";


//Ищем товары, где наличие ниже установленного лимита
if(!empty($all_products)) {
	foreach ($all_products as $product_id => $product) {
		
		$min_limit = $product['min_limit'];
		$total_exist = 0;

		if(!empty($product['storages'])) {
			foreach ($product['storages'] as $storage_id => $storage_exist) {
				$total_exist += (int) $storage_exist;
			}
		}

		if((int)$total_exist < (int)$min_limit) $all_limited[] = $product_id;

	}
}


// echo "<pre>";
// print_r($all_limited);
// echo "</pre>";


/********************************************************************************************************************** */
/********************************************************************************************************************** */

//Обновляем состояние всех перешедших лимит товаров
$products_ids_str = implode(',', $all_limited);
$ITEMS_UPDATE_SQL = "
UPDATE
`shop_catalogue_products`
SET
	`min_limit_status` = 1
WHERE
	`id` IN ($products_ids_str)
;";

$items_update_query = $db_link->prepare($ITEMS_UPDATE_SQL);

if($items_update_query->execute() == true) {

	//Обновляем время в файле
	$f = fopen('cron_update_products_limit_log.txt', 'w');
	fwrite($f, time()."\n\n");

}


?>