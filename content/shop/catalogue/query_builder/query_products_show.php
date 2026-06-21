<?php
//Построение запроса товаров с группировкой и пагинацией

if(empty($product_max_count)){
	$product_from = 0;//С какого продукта начать
	$product_max_count = 100;//До какого продукта показывать (НЕ включительно)
}

// Сортировка конечного результата страницы
if( isset($propucts_request["products_sort_mode"]["asc_desc"]) && strtolower($propucts_request["products_sort_mode"]["asc_desc"]) == "desc" )
{
	$ASC_DESC_DIR = "DESC";
}
else
{
	$ASC_DESC_DIR = "ASC";
}


if( isset($propucts_request["products_sort_mode"]["field"]) )
{
	switch($propucts_request["products_sort_mode"]["field"])
	{
		case "price":
			$ASC_DESC_FIELD = "`customer_price` $ASC_DESC_DIR";
			break;
		case "name":
			$ASC_DESC_FIELD = "`caption_translation` $ASC_DESC_DIR, `customer_price` ASC";
			break;
		case "random":
			$ASC_DESC_FIELD = "RAND(), `customer_price` ASC";
			break;
		default:
			$ASC_DESC_FIELD = "`customer_price` $ASC_DESC_DIR";
	}
}
else
{
	$ASC_DESC_FIELD = "`customer_price` $ASC_DESC_DIR";
}




$SQL_ORDER_BY = " ORDER BY CASE WHEN (`customer_price` > 0) THEN 0 WHEN (`customer_price` IS NULL) THEN 2 ELSE 1 END, $ASC_DESC_FIELD, `id` ASC";

// В $SQL полученном в файле query_products_all.php в данный момент находится выборка всех ID товаров с конечной продажной ценой
// В зависимости от направления сортировки нужно отсеить только уникальные ID товаров в нужной сортировке

$MIN_MAX = 'MIN(IFNULL(`customer_price`, 10000000000))';

if($ASC_DESC_DIR === "DESC"){
	$MIN_MAX = 'MAX(IFNULL(`customer_price`, 0))';
}

$caption_sql = "";
if( isset($propucts_request["products_sort_mode"]["field"]) && $propucts_request["products_sort_mode"]["field"] == "name"){
	//$caption_sql = "`caption`,";
	$caption_sql = "`caption_translation`,";
	//$caption_sql = "(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = `shop_catalogue_products`.`caption` AND `lang_code` = '".$multilang_params['lang']."') AS `caption_translation`,";
}

$SQL_limit = "
SELECT `id` FROM (
	SELECT * FROM (
		SELECT `id`, $caption_sql $MIN_MAX AS `customer_price` FROM ( $SQL ) AS `products_group` GROUP BY `id`
	) AS `products_id_sort` 
	
	$SQL_ORDER_BY 

	LIMIT $product_from, $product_max_count
	
) AS `products_id_limit`
";

//ОКОНЧАТЕЛЬНЫЙ ЗАПРОС ВСЕХ ДАННЫХ ------------------------------------------------------------------------------------------

$SQL = "";//Единственный запрос для получения всех нужных товаров
$cnt_args = count($sql_args_array);//Для клонирования при наличии нескольких магазинов

//По всем доступным магазинам
for($i = 0; $i < count($customer_offices); $i++)
{
	if($i > 0)
	{
		$SQL = $SQL . "UNION";
		
		//Клонируем параметры фильтра для нового магазина
		for( $j=0; $j < $cnt_args; $j++ )
		{
			array_push($sql_args_array, $sql_args_array[$j]);
		}
	}
	
	//----------------------------
	
	//Получаем список ID складов которые подключены к данному магазину, что бы не выбирать цены не подключенных складов
	
	$storages_id_in_office = "";
	$sql_storages = "SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` IN(SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1);";
	$stmt = $db_link->prepare($sql_storages);
	$stmt->execute(array($customer_offices[$i]));
	while( $record = $stmt->fetch() ){
		if($storages_id_in_office != ''){
			$storages_id_in_office .= ',';
		}
		$storages_id_in_office .= $record['storage_id'];
	}
	if($storages_id_in_office == ""){
		$storages_id_in_office = "0";//Если к магазину не подключен не один склад то поставим 0 что бы не было ошибки в запросе, отобразим товары без цен
	}
	
	//----------------------------
	
	//Определяем курс валюты для подключенных к данному магазину складов
	
	$currency_sql = "";
	$stmt = $db_link->prepare("SELECT `id`, (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = `currency`) AS `rate` FROM `shop_storages` WHERE `id` IN($storages_id_in_office);");
	$stmt->execute();
	while( $record = $stmt->fetch() ){
		$currency_sql .= "WHEN `shop_storages_data`.`storage_id` = ".$record['id']." THEN ".$record['rate']." ";
	}
	if($currency_sql != ""){
		$currency_sql = "(CASE ".$currency_sql."ELSE 1 END)";
	}else{
		$currency_sql = "1";//Склады не подключены, поставим 1 что бы не было ошибки в запросе
	}

	//----------------------------
	
	//Формируем ПРОДАЖНУЮ цену с наценкой
	
	$customer_price_sql = "";
	$stmt = $db_link->prepare("SELECT * FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `group_id` = ? AND `storage_id` IN($storages_id_in_office)");
	$stmt->execute(array($customer_offices[$i], $group_id));
	while( $record = $stmt->fetch() ){
		$customer_price_sql .= "
			WHEN
				`shop_storages_data`.`storage_id` = ".$record['storage_id']." AND `shop_storages_data`.`price` * $currency_sql >= ".floatval($record['min_point'])." AND `shop_storages_data`.`price` * $currency_sql < ".floatval($record['max_point'])."
			THEN
				`price` * $currency_sql + `price` * $currency_sql * (".floatval($record['markup'])." / 100)
		";
	}
	if($customer_price_sql != ""){
		$customer_price_sql = "CASE".$customer_price_sql."	ELSE
				`price` * $currency_sql + `price` * $currency_sql * (0 / 100)
		END AS `customer_price`
		";
	}else{
		$customer_price_sql = "`price` * $currency_sql AS `customer_price`
		";
	}
	
	//----------------------------
	
	//Формируем ЗАЧЕРКНУТУЮ цену с наценкой
	
	$price_crossed_out_sql = "";
	$stmt = $db_link->prepare("SELECT * FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `group_id` = ? AND `storage_id` IN($storages_id_in_office)");
	$stmt->execute(array($customer_offices[$i], $group_id));
	while( $record = $stmt->fetch() ){
		$price_crossed_out_sql .= "
			WHEN
				`shop_storages_data`.`storage_id` = ".$record['storage_id']." AND `shop_storages_data`.`price_crossed_out` * $currency_sql >= ".floatval($record['min_point'])." AND `shop_storages_data`.`price_crossed_out` * $currency_sql < ".floatval($record['max_point'])."
			THEN
				`price_crossed_out` * $currency_sql + `price_crossed_out` * $currency_sql * (".floatval($record['markup'])." / 100)
		";
	}
	if($price_crossed_out_sql != ""){
		$price_crossed_out_sql = "CASE".$price_crossed_out_sql."	ELSE
				`price_crossed_out` * $currency_sql + `price_crossed_out` * $currency_sql * (0 / 100)
		END AS `price_crossed_out`
		";
	}else{
		$price_crossed_out_sql = "`price_crossed_out` * $currency_sql AS `price_crossed_out`
		";
	}
	
	//----------------------------
	
	//Если отображение для панели управления, цены не важны
	if( isset($product_block_type) && $product_block_type == 2){
		$customer_price_sql = "`price` AS `customer_price`";
		$price_crossed_out_sql = "`price_crossed_out` AS `price_crossed_out`";
	}
	
	$SQL .= "
	SELECT 
		`shop_catalogue_products`.`id` AS `id`, 
		`shop_catalogue_products`.`caption` AS `caption`, 
		(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = `shop_catalogue_products`.`caption` AND `lang_code` = '".$multilang_params['lang']."') AS `caption_translation`, 
		`shop_catalogue_products`.`alias` AS `alias`, 
		`shop_catalogue_products`.`category_id` AS `category_id`, 
		
		".$customer_offices[$i]." AS `office_id`,
		
		`shop_storages_data`.`id` AS `storage_record_id`, 
		`shop_storages_data`.`storage_id` AS `storage_id`, 
		
		".$customer_price_sql.", 
		
		CAST(`shop_storages_data`.`price` * ".$currency_sql." AS decimal(20,2)) AS `price`, 
		".$price_crossed_out_sql.", 
		CAST(`shop_storages_data`.`price_purchase` * ".$currency_sql." AS decimal(20,2)) AS `price_purchase`, 
		
		`shop_storages_data`.`arrival_time` AS `arrival_time`, 
		`shop_storages_data`.`time_to_exe` AS `time_to_exe`, 
		`shop_storages_data`.`exist` AS `exist`, 
		`shop_storages_data`.`reserved` AS `reserved`, 
		`shop_storages_data`.`issued` AS `issued`, 
		
		(SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = `shop_catalogue_products`.`id` LIMIT 1) AS `file_name`, 
		(SELECT `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ".$customer_offices[$i]." AND `storage_id` = `shop_storages_data`.`storage_id` LIMIT 1) AS `additional_time`, 
		(SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = `shop_catalogue_products`.`category_id` LIMIT 1) AS `category_url`, 
		(SELECT `value` FROM `shop_properties_values_text` WHERE `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = `shop_catalogue_products`.`category_id` AND `value` IN (SELECT `id` FROM `lang_text_strings` WHERE `id` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` = 'Артикул' AND `lang_code` = 'ru')) AND `property_type_id` = 3) AND `product_id` = `shop_catalogue_products`.`id` LIMIT 1) AS article, 
		(SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = `shop_catalogue_products`.`category_id` AND `value` IN (SELECT `id` FROM `lang_text_strings` WHERE `id` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` = 'Производитель' AND `lang_code` = 'ru')) AND `property_type_id` = 5))) AS manufacturer,
		(SELECT ROUND(SUM(`mark`)/COUNT(`id`)) FROM `shop_products_evaluations` WHERE `product_id` = `shop_catalogue_products`.`id`) AS `mark`, 
		(SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = `shop_catalogue_products`.`id` AND `mark`=1) AS `mark_1`, 
		(SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = `shop_catalogue_products`.`id` AND `mark`=2) AS `mark_2`, 
		(SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = `shop_catalogue_products`.`id` AND `mark`=3) AS `mark_3`, 
		(SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = `shop_catalogue_products`.`id` AND `mark`=4) AS `mark_4`, 
		(SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = `shop_catalogue_products`.`id` AND `mark`=5) AS `mark_5`, 
		(SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = `shop_catalogue_products`.`id`) AS `marks_count`, 
		
		`stickers_t`.`value` AS `sticker_value`, 
		`stickers_t`.`id` AS `sticker_id`, 
		`stickers_t`.`color_text` AS `sticker_color_text`, 
		`stickers_t`.`color_background` AS `sticker_color_background`, 
		`stickers_t`.`href` AS `sticker_href`, 
		`stickers_t`.`class_css` AS `sticker_class_css`, 
		`stickers_t`.`description` AS `sticker_description` 
		
	FROM

		`shop_catalogue_products`

	LEFT OUTER JOIN `shop_storages_data` ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` AND `shop_storages_data`.`storage_id` IN($storages_id_in_office) AND `exist` > 0
	LEFT JOIN shop_products_stickers AS stickers_t ON shop_catalogue_products.id = stickers_t.product_id

	WHERE `shop_catalogue_products`.`id` IN( $SQL_limit )
	
	".$SQL_PROPERTIES_CONDITIONS_HAVING."
	
	";
	
	//Если отображение для панели управления, цены не важны, поэтому выходим из цикла по магазинам
	if( isset($product_block_type) && $product_block_type == 2){
		break;
	}
}

//Если выборка идет из нескольких магазинов подключаемых через UNION тогда сортировку можно производить только по объединенной выборке, значит нужно добавить промежуточную таблицу что бы не было ошибки в запросе, но это увеличит время выполнения, поэтому если магазин 1 тогда не будем добавлять промежуточную таблицу
if(count($customer_offices) > 1){
	$SQL = "
	SELECT * FROM (
		$SQL
	) AS `ALL_UNION` 
	";
}

// Сортировка конечного результата страницы
$SQL = $SQL . $SQL_ORDER_BY;

/*$log = fopen( 'log.txt' , 'w' );
fwrite($log, $SQL);
fclose($log);*/
?>