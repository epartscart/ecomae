<?php
/**
 * СКРИПТ ДЛЯ УДАЛЕНИЯ ПРОДУКТОВ ИЗ СПРАВОЧНИКА ТОВАРОВ
 * 
 * '_sub' в названии скрипта говорит о том, что этот скрипт работает не самостоятельно. Его нужно подключать через require_once
 * 
 * Параметры, которые требуются скрипту:
 * 
 * - $products_to_delete - список ID продуктов
*/
defined('_ASTEXE_') or die('No access');

if(DP_User::getAdminId() <= 0)
{
	throw new Exception("Нет доступа");
}


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//АЛГОРИТМ УДАЛЕНИЯ ПРОДУКТА ИЗ СПРАВОЧНИКА
//1. Удаляем учетную запись продукта из таблицы shop_catalogue_products
//2. Удаляем изображения продукта из таблицы shop_products_images
//3. Удаляем текстовые описания продуктов из таблицы shop_products_texts
//4. Удаляем значения свойств продуктов из 5 таблиц значений свойств


//Составляем строку в формате '(ID1, ID2, ..., IDN)'
$sub_SQL_PRODUCTS_LIST = "";
$binding_values = array();

if(empty($products_to_delete) && !empty($category_id)){
	$sub_SQL_PRODUCTS_LIST = "(SELECT `id` FROM `shop_catalogue_products` WHERE `category_id` = $category_id)";
}else{
	for($i=0; $i < count($products_to_delete); $i++)
	{
		if($i > 0)
		{
			$sub_SQL_PRODUCTS_LIST .= ",";
		}
		$sub_SQL_PRODUCTS_LIST .= "?";
		
		array_push($binding_values, $products_to_delete[$i]);
	}
	$sub_SQL_PRODUCTS_LIST = "(".$sub_SQL_PRODUCTS_LIST.")";
}

$sub_SQL_PRODUCTS_LIST .= ' LIMIT 5000';


//Формируем SQL-запросы:

//Удаляем изображения продукта из таблицы shop_products_images
$SQL_DELETE_PRODUCTS_IMAGES = "DELETE FROM `shop_products_images` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
//Удаляем текстовые описания продуктов из таблицы shop_products_text
$SQL_DELETE_PRODUCTS_TEXTS = "DELETE FROM `shop_products_text` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
//Удаляем значения свойств продуктов из 6 таблиц значений свойств
$SQL_DELETE_PRODUCTS_PROPERTIES_INT = "DELETE FROM `shop_properties_values_int` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
$SQL_DELETE_PRODUCTS_PROPERTIES_FLOAT = "DELETE FROM `shop_properties_values_float` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
$SQL_DELETE_PRODUCTS_PROPERTIES_TEXT = "DELETE FROM `shop_properties_values_text` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
$SQL_DELETE_PRODUCTS_PROPERTIES_BOOL = "DELETE FROM `shop_properties_values_bool` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
$SQL_DELETE_PRODUCTS_PROPERTIES_LIST = "DELETE FROM `shop_properties_values_list` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
$SQL_DELETE_PRODUCTS_TREE_LIST = "DELETE FROM `shop_properties_values_tree_list` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
//Удаляем товары на главной
$SQL_DELETE_PRODUCTS_MAIN_PAGE = "DELETE FROM `shop_main_page_products` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
//Удаляем стикеры
$SQL_DELETE_PRODUCTS_PRODUCTS_STICKERS = "DELETE FROM `shop_products_stickers` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
//Удаляем комментарии
$SQL_DELETE_PRODUCTS_EVALUATIONS = "DELETE FROM `shop_products_evaluations` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
//Удаляем сопутствующие товары
$SQL_DELETE_PRODUCTS_RELATED_PRODUCTS = "DELETE FROM `shop_related_products` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";
$SQL_DELETE_PRODUCTS_RELATED_PRODUCTS_2 = "DELETE FROM `shop_related_products` WHERE `product_id_related` IN $sub_SQL_PRODUCTS_LIST;";
//Удаляем складские записи
$SQL_DELETE_PRODUCTS_STORAGES_DATA = "DELETE FROM `shop_storages_data` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;";

if(empty($products_to_delete) && !empty($category_id)){
	//Удаляем учетную запись продукта из таблицы shop_catalogue_products
	$SQL_DELETE_PRODUCTS_RECORDS = "DELETE FROM `shop_catalogue_products` WHERE `category_id` = $category_id LIMIT 5000;";
}else{
	//Удаляем учетную запись продукта из таблицы shop_catalogue_products
	$SQL_DELETE_PRODUCTS_RECORDS = "DELETE FROM `shop_catalogue_products` WHERE `id` IN $sub_SQL_PRODUCTS_LIST;";
}

/*
//Удаляем файлы изображений
$images_query = $db_link->prepare("SELECT * FROM `shop_products_images` WHERE `product_id` IN $sub_SQL_PRODUCTS_LIST;");
$images_query->execute($binding_values);
while($image = $images_query->fetch())
{
	//Проверяем, используется этот файл в учетных записях изображений тех товаров, которых нет в списке на удаение
	$check_file_use_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_products_images` WHERE `file_name` = '".$image["file_name"]."' AND `product_id` NOT IN $sub_SQL_PRODUCTS_LIST;");
	$check_file_use_query->execute($binding_values);
	//Файл удаляем только, если он не ипользуется учетных записях товаров, которых нет в списке на удаление
	if( $check_file_use_query->fetchColumn() == 0 )
	{
		unlink($_SERVER["DOCUMENT_ROOT"]."/content/files/images/products_images/".$image["file_name"]);
	}
}
*/





//ВЫПОЛНЯЕМ ЗАПРОСЫ:

$query = $db_link->prepare($SQL_DELETE_PRODUCTS_IMAGES);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2858));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_TEXTS);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2859));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_PROPERTIES_INT);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2860));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_PROPERTIES_FLOAT);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2861));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_PROPERTIES_TEXT);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2862));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_PROPERTIES_BOOL);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2863));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_PROPERTIES_LIST);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2864));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_TREE_LIST);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2865));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_MAIN_PAGE);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2866));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_PRODUCTS_STICKERS);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2867));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_EVALUATIONS);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2868));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_RELATED_PRODUCTS);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2869));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_RELATED_PRODUCTS_2);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2870));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_STORAGES_DATA);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2871));
	}
}while($query->rowCount() > 0);


$query = $db_link->prepare($SQL_DELETE_PRODUCTS_RECORDS);
do{
	if($query->execute($binding_values) != true){
		throw new Exception(translate_str_by_id(2872));
	}
}while($query->rowCount() > 0);





//ПРОИЗВОДИМ ОПТИМИЗАЦИЮ ТАБЛИЦ ЧТО БЫ ОЧИСТИТЬ МУСОРНЫЕ ДАННЫЕ В БАЗЕ

$db_link->prepare("OPTIMIZE TABLE `shop_products_images`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_products_text`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_properties_values_int`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_properties_values_float`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_properties_values_text`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_properties_values_bool`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_properties_values_list`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_properties_values_tree_list`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_main_page_products`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_products_stickers`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_products_evaluations`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_related_products`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_storages_data`")->execute();
$db_link->prepare("OPTIMIZE TABLE `shop_catalogue_products`")->execute();

?>