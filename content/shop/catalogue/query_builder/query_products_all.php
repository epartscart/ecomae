<?php
//Построение подзапроса товаров без группировки и пагинации

// ФОРМИРУЕМ СТРОКИ УСЛОВИЙ ДЛЯ СВОЙСТВ: ------------------------------------------------------------------------------------------

// Флаг символизирующий открытие блока похожих товаров в карточке продукта
if(empty($block_of_similar_products)){
	
	$SQL_PROPERTIES_CONDITIONS = "";
	$SQL_PROPERTIES_CONDITIONS_HAVING = "";
	$sql_args_array = array();

	// Если это страница категории
	if(!empty($category_id) && empty($products_ids_str)){
		if($SQL_PROPERTIES_CONDITIONS != ""){
			$SQL_PROPERTIES_CONDITIONS .= " AND ";
		}
		if (function_exists('epc_electronicae_storefront_active') && epc_electronicae_storefront_active()) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';
			$SQL_PROPERTIES_CONDITIONS .= '(`shop_catalogue_products`.`category_id` IN (' . epc_electronicae_category_sql_in($db_link, (int) $category_id) . '))';
		} else {
			$SQL_PROPERTIES_CONDITIONS .= "(`shop_catalogue_products`.`category_id` = $category_id)";
		}
	}

	// Для клиентской стороны скрываем не опубликованные товары
	if(  isset($product_block_type) &&  ($product_block_type == 1 || $product_block_type == 4)  )
	{
		if($SQL_PROPERTIES_CONDITIONS != ""){
			$SQL_PROPERTIES_CONDITIONS .= " AND ";
		}
		$SQL_PROPERTIES_CONDITIONS .= "(`shop_catalogue_products`.`published_flag` = 1)";
	}

	//	Поиск по наименованию
	if(isset($propucts_request["products_ids_str"]) && $propucts_request["products_ids_str"] !== ''){
		$products_ids_str = $propucts_request["products_ids_str"];
	}
	if(isset($products_ids_str)){
		if($SQL_PROPERTIES_CONDITIONS != ""){
			$SQL_PROPERTIES_CONDITIONS .= " AND ";
		}
		$SQL_PROPERTIES_CONDITIONS .= "(`shop_catalogue_products`.`id` IN($products_ids_str))";
	}

	// Фильтр свойств
	if(isset($properties_list)){
	for($i=0; $i < count($properties_list); $i++)
	{
		$property_type_id = null;
		if( isset($properties_list[$i]["property_type_id"]) )
		{
			$property_type_id = $properties_list[$i]["property_type_id"];
		}
		
		$property_id = null;
		if( isset($properties_list[$i]["property_id"]) )
		{
			$property_id = $properties_list[$i]["property_id"];
		}
		
		//Цена
		if($property_id == 'price')
		{
			$price_object = $properties_list[$i];
			if(($price_object["min_need"] > $price_object["min_value"]) || ($price_object["max_need"] < $price_object["max_value"]))
			{
				$min_price = floatval($price_object["min_need"]);
				$max_price = floatval($price_object["max_need"])+1;
				
				if($SQL_PROPERTIES_CONDITIONS_HAVING != ""){
					$SQL_PROPERTIES_CONDITIONS_HAVING .= " AND ";
				}
				$SQL_PROPERTIES_CONDITIONS_HAVING .= "(`customer_price` >= $min_price AND `customer_price` < $max_price)";
			}
			continue;
		}
		
		//Для свойств типа int и float
		if($property_type_id == 1 || $property_type_id == 2)
		{
			//Если указаны крайние значения, то это свойство не учитываем
			if($properties_list[$i]["min_need"] == $properties_list[$i]["min_value"] && $properties_list[$i]["max_need"] == $properties_list[$i]["max_value"])
			{
				continue;
			}
		}
		
		// Свойства по типам
		switch($property_type_id)
		{
			case 1:
				if($SQL_PROPERTIES_CONDITIONS != ""){
					$SQL_PROPERTIES_CONDITIONS .= " AND ";
				}
				$SQL_PROPERTIES_CONDITIONS .= '( (SELECT `value` FROM shop_properties_values_int WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) >= ? AND (SELECT `value` FROM shop_properties_values_int WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) <= ? )';
				
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $properties_list[$i]["min_need"]);
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $properties_list[$i]["max_need"]);
				
				break;
			case 2:
				if($SQL_PROPERTIES_CONDITIONS != ""){
					$SQL_PROPERTIES_CONDITIONS .= " AND ";
				}
				$SQL_PROPERTIES_CONDITIONS .= '( (SELECT `value` FROM shop_properties_values_float WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) >= ? AND (SELECT `value` FROM shop_properties_values_float WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) <= ? )';
				
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $properties_list[$i]["min_need"]);
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $properties_list[$i]["max_need"]);
				
				break;
			case 4:
				//Если оба варианта (ДА/НЕТ) не отмечены - означает, что данное свойство не учитывается
				//Если оба варианта (ДА/НЕТ) отмечны - это равносильно тому, что нужно будет высести все товары, поэтому, тоже это свойство не будет учтено
				if( !( ($properties_list[$i]["true_checked"] == false && $properties_list[$i]["false_checked"] == false) ||
					($properties_list[$i]["true_checked"] == true && $properties_list[$i]["false_checked"] == true) ) )
				{
					//if сработал, потому, что одно из них выставлено - выясняем, какое:
					if($properties_list[$i]["true_checked"] == true)
					{
						$need_value = 1;
					}
					else
					{
						$need_value = 0;
					}
					if($SQL_PROPERTIES_CONDITIONS != ""){
						$SQL_PROPERTIES_CONDITIONS .= " AND ";
					}
					$SQL_PROPERTIES_CONDITIONS .= '(SELECT `value` FROM shop_properties_values_bool WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) = ?';
					
					array_push($sql_args_array, $property_id);
					array_push($sql_args_array, $need_value);
				}
				break;
			case 5:
				
				$list_options = $properties_list[$i]["list_options"];
				$list_type = $properties_list[$i]["list_type"];//Тип списка
				
				if($list_type == 1)
				{
					$OR_AND = "OR";
				}
				else if($list_type == 2)
				{
					$OR_AND = "AND";
				}
				
				$SQL_VALUES_COND = "";//Подстрока с условиями для поля value
				for($o=0; $o < count($list_options); $o++)
				{
					if( isset($list_options[$o]["value"]) )
					{
						if($list_options[$o]["value"] == true)
						{
							if($SQL_VALUES_COND != "")$SQL_VALUES_COND .= ' '.$OR_AND.' ';
							$SQL_VALUES_COND = $SQL_VALUES_COND . ' (`shop_catalogue_products`.id IN(SELECT `product_id` FROM shop_properties_values_list WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ? AND value = ?)) ';
							
							array_push($sql_args_array, $property_id);
							array_push($sql_args_array, $list_options[$o]["id"]);
						}
					}
				}
				if($SQL_VALUES_COND != "")//Если строка заполнена, то по меньше мере одно значение отмечено, значит добавляем строку
				{
					if($SQL_PROPERTIES_CONDITIONS != ""){
						$SQL_PROPERTIES_CONDITIONS .= " AND ";
					}
					$SQL_PROPERTIES_CONDITIONS .= '('.$SQL_VALUES_COND.')';
				}
				break;//case 5
			case 6:
				//Свойство данного типа НЕ учитывается если выбранно значение "Все" на первом уровне
				if( ! ( $properties_list[$i]["current_level"] == 1 && $properties_list[$i]["current_value"] == 0 ) )
				{
					$current_value = $properties_list[$i]["current_value"];
					
					if($SQL_PROPERTIES_CONDITIONS != ""){
						$SQL_PROPERTIES_CONDITIONS .= " AND ";
					}
					$SQL_PROPERTIES_CONDITIONS .= '( SELECT `value` FROM `shop_properties_values_tree_list` WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ? AND value = ? LIMIT 1) ';
					
					array_push($sql_args_array, $property_id);
					array_push($sql_args_array, $current_value);
				}
				break;
		}//switch($property_type_id)
	}
	}

	// Завершение формирования строки
	if($SQL_PROPERTIES_CONDITIONS != ""){
		$SQL_PROPERTIES_CONDITIONS = "WHERE " . $SQL_PROPERTIES_CONDITIONS;
	}
	if($SQL_PROPERTIES_CONDITIONS_HAVING != ""){
		$SQL_PROPERTIES_CONDITIONS_HAVING = " HAVING " . $SQL_PROPERTIES_CONDITIONS_HAVING;
	}
}

// END: ------------------------------------------------------------------------------------------



$SQL = "";//Единственный запрос для получения всех нужных товаров
$cnt_args = 0;
if(isset($sql_args_array)){
	$cnt_args = count($sql_args_array);//Для клонирования при наличии нескольких магазинов
}

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
	
	//Выбор всех товаров с конечной ценой покупателя удовлетворяющих фильтру
	
	//Если отображение для панели управления, цены не важны
	if( isset($product_block_type) && $product_block_type == 2){
		$propucts_request["products_sort_mode"]["field"] = "name";
		$customer_price_sql = "`price` AS `customer_price`";
	}
	
	//Если сортировка по наименованию тогда добавим вывод наименования в запрос
	$caption_sql = "";
	if( isset($propucts_request["products_sort_mode"]["field"]) && $propucts_request["products_sort_mode"]["field"] == "name"){
		//$caption_sql = "`shop_catalogue_products`.`caption`,";
		$caption_sql = "(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = `shop_catalogue_products`.`caption` AND `lang_code` = '".$multilang_params['lang']."') AS `caption_translation`,";
	}
	
	$SQL = $SQL . "
	SELECT  
		`shop_catalogue_products`.`id`, 
		".$caption_sql."
		".$customer_price_sql."
	FROM `shop_catalogue_products` 

		LEFT OUTER JOIN `shop_storages_data` ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` AND `shop_storages_data`.`storage_id` IN($storages_id_in_office) AND `exist` > 0 AND `price` > 0
	
	".$SQL_PROPERTIES_CONDITIONS." ".$SQL_PROPERTIES_CONDITIONS_HAVING."

	";
	
	//Если отображение для панели управления, цены не важны, поэтому выходим из цикла по магазинам
	if( isset($product_block_type) && $product_block_type == 2){
		break;
	}
}
?>