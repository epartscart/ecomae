<?php
defined('_ASTEXE_') or die('No access');


//Получить список магазинов покупателя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");


//Указатель валюты
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/general/get_currency_indicator.php");


//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$userProfile = DP_User::getUserProfile();
$group_id = $userProfile["groups"][0];//Берем первую группу пользователя


//Подключаем скрипт с общей функцией вывода блока товара ( printProductBlock(product) )
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/helper.php");


//СТИЛЬ БЛОКА ДЛЯ ТОВАРА
$main_class_of_block = "product_div_tile col-xs-12 col-sm-4 col-md-3 col-lg-1-5";


//ТИП БЛОКА (1,2,3,4,5)
$product_block_type = 5;//ТИП ДЛЯ БЛОКОВ ТОВАРОВ НА ГЛАВНОЙ


//Исходные данные:
$groups_query = $db_link->prepare('SELECT * FROM `shop_main_page_groups` WHERE `active` = 1 ORDER BY `order`;');
$groups_query->execute();
while( $group_record = $groups_query->fetch() )//Цикл по группам товаров
{
	//Получаем товары группы
	$products_list = array();
	$products_list_comma = "";
	
	$products_query = $db_link->prepare('SELECT `product_id` FROM `shop_main_page_products` WHERE `group_id` = :group_id ORDER BY `order`;');
	$products_query->bindValue(':group_id', $group_record["id"]);
	$products_query->execute();
	while( $product_record = $products_query->fetch())
	{
		$product_record["product_id"] = (int)$product_record["product_id"];
		
		array_push($products_list, $product_record["product_id"]);
		
		
		if($products_list_comma != "")
		{
			$products_list_comma = $products_list_comma . ",";
		}
		$products_list_comma = $products_list_comma . $product_record["product_id"];
	}
	
	// Подстрока для запроса товаров
	$products_ids_str = $products_list_comma;
	
	//Подключение построение запроса
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_all.php");
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_show.php");
	
	//Подключаем скрипт генерации объектов товаров единого формата ( $products_objects )
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/generate_products_objects_by_sql.php");
	
	if( count($products_objects) != 0)//Если объекты товаров
	{
		?>
		<div class="col-xs-12 col-sm-12 col-md-12 col-lg-12">
		<?php
		
		if( $group_record["show_caption"] == true )//Если нужно показать название блока
		{
			?>
			<h2 class="section-title"><?php echo translate_str_by_id($group_record["caption"]); ?></h2>
			<?php
		}
		
		foreach( $products_objects AS $product_id => $product )
		{
			printProductBlock($product);
		}
		?>
		</div>
		<?php
	}
}

//Функция добавления в корзину
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/common_add_to_basket.php");
?>