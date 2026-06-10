<?php
/**
 * Серверный скрипт для получения списка id продуктов ($products_list).
 * 
 * В зависимости от типа запроса, может быть:
 * - запрос товаров категории (покупатель, администратор каталога, кладовщик);
 * - запрос по строке поиска (покупатель)
*/
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


//Указатель валюты
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/general/get_currency_indicator.php");


//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$userProfile = DP_User::getUserProfile();
$group_id = $userProfile["groups"][0];//Берем первую группу пользователя


//Получить список магазинов покупателя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");


//Получаем объект запроса
$propucts_request = json_decode($_POST["propucts_request"], true);
$category_id = $propucts_request["category_id"];
$properties_list = $propucts_request["properties_list"];
$product_block_type = $propucts_request["product_block_type"];

$productsPerPage = $propucts_request["productsPerPage"];//Количество товаров на страницу
$needPagesCount = $propucts_request["needPagesCount"];//Требуемое количество страниц
$startFrom = $propucts_request["startFrom"];//С какой страницы начать
$page_style = $propucts_request["page_style"];//Стиль отображения
$product_block_type = $propucts_request["product_block_type"];//Тип страницы (1 - отображения для покупателя; 2 - для администратора каталога; 3 - для кладовщика; 4 - для покупателя при поиске через текстовую строку)

$product_from = $startFrom*$productsPerPage;//С какого продукта начать
$product_max_count = $needPagesCount*$productsPerPage;//До какого продукта показывать (НЕ включительно)


$main_class_of_block = "";//Главный класс блока
switch($page_style)
{
    case 1:
        $main_class_of_block = "product_div_tile col-xs-12 col-sm-4 col-md-4 col-lg-3";//Плитка
        break;
    case 2:
        $main_class_of_block = "product_div_list_photo col-lg-12";//Список с фото
        break;
    case 3:
        $main_class_of_block = "product_div_list col-lg-12";//Список без фото
        break;
}


//Сначала получаем список товаров, которые подходят по запросу:
$search_string = trim(htmlspecialchars(strip_tags($propucts_request["search_string"])));
if($search_string !== ''){
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/text_search_algorithm.php");//ЕДИНЫЙ АЛГОРИТМ ПОИСКА ТОВАРА ПО ТЕКСТОВОЙ СТРОКЕ
	
	//Составим строку с id товаров вида (1,2,3). $products_list - массив с id товаров, который заполнен в скрипте единого алгоритма
	$products_ids_str = "";
	for($i=0; $i < count($products_list); $i++)
	{
		$products_list[$i] = (int)$products_list[$i];
		
		if($products_ids_str != "") $products_ids_str = $products_ids_str.",";
		$products_ids_str = $products_ids_str.$products_list[$i];
	}
	
	if($products_ids_str === "")
	{
		$products_ids_str = "0";
	}
}
	

//Подключение построение запроса
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_all.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_show.php");


//Подключаем скрипт генерации объектов товаров единого формата ( $products_objects )
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/generate_products_objects_by_sql.php");
?>