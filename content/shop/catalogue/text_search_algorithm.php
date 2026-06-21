<?php
/**
 * Шаблон скрипта для подбора товаров по текстовой строке
 * 
 * Данный скрипт заполняет массив $products_list номерами товаров, которые удовлетворяют поисковому запросу.
 * 
 * Этот скрипт нужно включать в соответствующие места скриптов, где должен использоваться этот массив
 * 
*/
//Подключаем строки с подзапросами, с учетом мультиязычности
require_once( $_SERVER['DOCUMENT_ROOT']."/content/shop/catalogue/cat_lang_general.php" );


$searsch_str = $search_string;
$like = '1 = 1';
$like_2 = '1 = 1';
$bind_array = array();
$iter = 0;
if(!empty($searsch_str))
{
	$searsch_str = trim($searsch_str);
	$searsch_str = explode(' ',$searsch_str);
	if(!empty($searsch_str))
	{
		$tmp_str = '';
		$tmp_str_2 = '';
		foreach($searsch_str as $item_str)
		{
			$item_str = trim($item_str);
			if(mb_strlen($item_str, 'utf-8') < 2)
			{
				continue;
			}
			
			$param_name = ':name'.$iter;
			$iter++;
			array_push($bind_array, array('value' => '%'.$item_str.'%', 'name'=>$param_name) );
			
			// Поиск по названию
			if($tmp_str != '')
			{
				$tmp_str .= ' AND ';
			}
			$tmp_str .= '((`value` LIKE '.$param_name.')';
			//$tmp_str .= '((`caption` LIKE \'%OZ%\')';
			
			// Поиск по описанию
			if($tmp_str_2 != '')
			{
				$tmp_str_2 .= ' AND ';
			}
			$tmp_str_2 .= '((`value` LIKE '.$param_name.')';
			
			
			
			$tmp_str .= ')';
			$tmp_str_2 .= ')';
		}
		$like = '('.$tmp_str.')';
		$like_2 = '('.$tmp_str_2.')';
	}
}


$products_list = array();


//Поиск по наименованию
$sql = "SELECT `id` FROM `shop_catalogue_products` WHERE `caption` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE ".$like.");";
$products_list_query = $db_link->prepare($sql);
for( $i=0; $i < count($bind_array); $i++ )
{
	$products_list_query->bindValue($bind_array[$i]['name'], $bind_array[$i]['value'], PDO::PARAM_STR);
}
$products_list_query->execute();
while( $product_record = $products_list_query->fetch() )
{
    array_push($products_list, (int)$product_record["id"]);
}


//Поиск по текстовому описанию
$sql = "SELECT `product_id` FROM `shop_products_text` WHERE `content` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE ".$like_2.");";
$products_list_query = $db_link->prepare($sql);
for( $i=0; $i < count($bind_array); $i++ )
{
	$products_list_query->bindValue($bind_array[$i]['name'], $bind_array[$i]['value'], PDO::PARAM_STR);
}
$products_list_query->execute();
while( $product_record = $products_list_query->fetch() )
{
    array_push($products_list, (int)$product_record["product_id"]);
}


//Поиск по артикулу
$article_norm = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $search_string), "UTF-8");
$sql = "SELECT `product_id` FROM `shop_properties_values_text` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `value` IN ".$article_lang." AND `property_type_id` = 3) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND value = ? );";
$products_list_query = $db_link->prepare($sql);
$products_list_query->execute( array( $multilang_params['lang'], $article_norm ) );
while( $product_record = $products_list_query->fetch() )
{
    array_push($products_list, (int)$product_record["product_id"]);
}

//Поиск по alias (brand/partnumber CHPU) и brand_article_key
if ($article_norm !== '') {
	$aliasStmt = $db_link->prepare(
		"SELECT `id` FROM `shop_catalogue_products`
		 WHERE `published_flag` = 1 AND (`alias` LIKE ? OR `alias` LIKE ? OR `alias` = ?)"
	);
	$aliasStmt->execute(array('%/' . $article_norm, $article_norm . '%', strtolower($article_norm)));
	while ($product_record = $aliasStmt->fetch()) {
		array_push($products_list, (int) $product_record['id']);
	}
}
$search_lower = strtolower(trim($search_string));
if ($search_lower !== '') {
	try {
		$baStmt = $db_link->prepare(
			"SELECT DISTINCT q.`product_id`
			 FROM `epc_product_discovery_queue` q
			 WHERE q.`status` = 'imported' AND q.`product_id` > 0
			   AND (q.`brand_article_key` LIKE ? OR q.`meta_json` LIKE ?)"
		);
		$baStmt->execute(array('%' . $search_lower . '%', '%"brand_article_key":"%' . $search_lower . '%'));
		while ($product_record = $baStmt->fetch()) {
			array_push($products_list, (int) $product_record['product_id']);
		}
	} catch (Throwable $e) {
	}
}
?>