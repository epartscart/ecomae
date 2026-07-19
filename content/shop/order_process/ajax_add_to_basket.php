<?php
/**
 * Серверный скрипт для добавления товара в корзину
 * 
 * 
 * Если покупатель авторизован, то добавляем товар Базу данных
 * 
 * Если покупатель не авторизован - добавляем товар в куки
 * 
*/
header('Content-Type: application/json;charset=utf-8;');
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


//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_product_hash.php");
$user_id = DP_User::getUserId();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_moq_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
if (epc_storefront_guest_commerce_blocked((int) $user_id)) {
	exit(json_encode(epc_storefront_guest_commerce_denied_payload(
		isset($multilang_params) && is_array($multilang_params) ? $multilang_params : null
	)));
}
if($user_id > 0)
{
	//Поля для авторизованного пользователя
	$session_id = 0;
}
else
{
	//Поля для НЕавторизованного пользователя
	$session_record = DP_User::getUserSession();
	if($session_record == false)
	{
		$result = array();
		$result["status"] = false;
		$result["code"] = "incorrect_session";
		$result["message"] = translate_str_by_id(4460);
		exit(json_encode($result));
	}
	
	$session_id = $session_record["id"];
}

$userProfile = DP_User::getUserProfile();//Профиль пользователя
$group_id = $userProfile["groups"][0];

//Получаем массив продуктов для добавления в корзину
$product_objects = json_decode($_POST["product_objects"], true);

if($product_objects == NULL)
{
    $result = array();
    $result["status"] = false;
    $result["code"] = "incorrect_data";
    $result["message"] = translate_str_by_id(4461);
    exit(json_encode($result));
}

//Список записей корзины
$products_in_cart = array();

$no_error = true;//Флаг - выполнено без ошибок

//Добавляем записи:
for($i=0; $i < count($product_objects); $i++)
{
    $product_object = $product_objects[$i];
    $product_type = $product_object["product_type"];
    
    //В зависимости от типа продукта
    if( $product_type == 1 )//Каталожный
    {
        $product_id = (int)$product_object["product_id"];
        $office_id = (int)$product_object["office_id"];
        $storage_id = (int)$product_object["storage_id"];
        $storage_record_id = $product_object["storage_record_id"];
        $price = $product_object["price"];
        $time_to_exe = $product_object["time_to_exe"];
        $exist = $product_object["exist"];
        
		if(!empty($product_object["count_need"]))
		{
			$count_need = (int)$product_object["count_need"];
			if( $count_need <= 0 )
			{
				exit;
			}
		}
		else
		{
			$count_need = 1;//Изначально всегда добавляем одну запись
		}
		
        $time = time();//Время добавления записи
        
        
		//Проверяем хеш, защищающий от подмены данных злоумышлненниками через JavaScript
		$check_hash = md5($product_id.$office_id.$storage_id.$storage_record_id.$price.$DP_Config->tech_key);
		if( $check_hash != $product_object["check_hash"] )
		{
			$result = array();
			$result["status"] = false;
			$result["code"] = "35";
			$result["message"] = translate_str_by_id(4462);
			exit(json_encode($result));
		}
		
        
        //Проверяем наличие такого же товара в корзине
        $check_already_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_carts` WHERE `product_id`=? AND `price`= ? AND `user_id`=? AND `session_id` = ?;');
		$check_already_query->execute( array($product_id, $price, $user_id, $session_id) );
		if( $check_already_query->fetchColumn() > 0)
		{
			//Такой товар уже есть в корзине, но проверим ID складской записии, это могут быть разные склады, в этом случае можно позволить положить товар в корзину
			$check_already_query = $db_link->prepare('SELECT * FROM `shop_carts_details` WHERE `cart_record_id` IN(SELECT `id` FROM `shop_carts` WHERE `product_id` = ? AND `price`= ? AND `user_id` = ? AND `session_id` = ?) AND `storage_record_id` = ?;');
			$check_already_query->execute( array($product_id, $price, $user_id, $session_id, $storage_record_id) );
			if( $check_already_query->fetchColumn() > 0)
			{
				$result = array();
				$result["status"] = false;
				$result["code"] = "already";
				exit(json_encode($result));
			}
		}
		
		
		//Получаем данные по товару: Наименование, Артикул и Производитель
		
		//Подключаем строки с подзапросами, с учетом мультиязычности
		require_once( $_SERVER['DOCUMENT_ROOT']."/content/shop/catalogue/cat_lang_general.php" );
		
		$SQL_query = 'SELECT `caption` AS `name`, 
		
		(SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = ? AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1) AND `value` IN '.$manufacturer_lang.' AND `property_type_id` = 5 LIMIT 1) LIMIT 1) LIMIT 1) AS `manufacturer`, 

		(SELECT `value` FROM `shop_properties_values_text` WHERE `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1) AND `value` IN '.$article_lang.' AND `property_type_id` = 3 LIMIT 1) AND `product_id` = ?) AS `article`

		FROM `shop_catalogue_products` WHERE `id` = ?';
		$query = $db_link->prepare($SQL_query);
		$query->execute( array($product_id, $product_id, $product_id, $product_id, $product_id) );
		$record = $query->fetch();
		$product_object["article"] = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", translate_str_by_id($record["article"])), "UTF-8");
		$product_object["article_show"] = trim(translate_str_by_id($record["article"]));
		$product_object["manufacturer"] = mb_strtoupper(trim(translate_str_by_id($record["manufacturer"])), "UTF-8");
		$product_object["name"] =  trim(translate_str_by_id($record["name"]));
		
		
		//Получаем наценку:
		$markup_query = $db_link->prepare('SELECT `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= ? AND `max_point` > ?;');
		$markup_query_args = array($office_id, $storage_id, $group_id, $price, $price);
		$markup_query->execute($markup_query_args);
		$markup_record = $markup_query->fetch();
		$product_object["markup"] = $markup_record["markup"];//Наценка
		
		
		//Узнаем отпускную цену склада.
		$price_purchase = 0;//Цена закупа склада
		$price_not_markup = 0;//Цена склада без наценки
		//Подстрока для умножение цены на курс валюты склада
		$SQL_currency_rate = "(SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`) )";
		$price_purchase_query = $db_link->prepare('SELECT `price`*'.$SQL_currency_rate.' AS `price`, `price` AS `price_not_markup`, `price_purchase`*'.$SQL_currency_rate.' AS `price_purchase` FROM `shop_storages_data` WHERE `id`= ?;');
		$price_purchase_query->execute( array($storage_record_id) );
		$price_purchase_record = $price_purchase_query->fetch();
		if($price_purchase_record["price_purchase"] > 0){
			$price_purchase = $price_purchase_record["price_purchase"];
		}else{
			$price_purchase = $price_purchase_record["price"];
		}
		$price_not_markup = $price_purchase_record["price_not_markup"];
		$product_object["price_purchase"] = $price_purchase;
		$product_object["price_not_markup"] = $price_not_markup;
		
		
        $t2_manufacturer = $product_object["manufacturer"];
        $t2_article = $product_object["article"];
        $t2_article_show = $product_object["article_show"];
        $t2_name = $product_object["name"];
        $t2_exist = $product_object["exist"];
        $t2_time_to_exe = $product_object["time_to_exe"];
        $t2_time_to_exe_guaranteed = $product_object["time_to_exe_guaranteed"];
        $t2_storage = $product_object["storage_id"];
        $t2_min_order = 1;
        $t2_probability = 100;
		$t2_markup = $product_object["markup"];
        $t2_price_purchase = 0;// Записываем 0 что бы не было ошибки в расчете суммы закупа, для товаров каталога эта сумма берется из таблицы shop_carts_details
        $t2_office_id = $product_object["office_id"];
        $t2_storage_id = $product_object["storage_id"];
		$t2_product_json = json_encode($product_object);
		$t2_json_params = '';
		$apaiFulfillmentFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_fulfillment.php';
		if (is_file($apaiFulfillmentFile)) {
			require_once $apaiFulfillmentFile;
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
			$apaiSiteKey = epc_portal_site_key_from_hostname((string) ($_SERVER['HTTP_HOST'] ?? ''));
			if ($apaiSiteKey !== '' && function_exists('epc_apai_product_fulfillment_meta')) {
				$apaiMeta = epc_apai_product_fulfillment_meta($db_link, $apaiSiteKey, (int) $product_id);
				if (is_array($apaiMeta)) {
					$t2_json_params = epc_apai_order_item_json_params($apaiMeta);
				}
			}
		}
       
	   
        if( $db_link->prepare('INSERT INTO `shop_carts` (`product_type`, `product_id`, `price`, `count_need`, `user_id`, `time`, `session_id`, 
		`t2_manufacturer`,
		`t2_article`,
		`t2_article_show`,
		`t2_name`,
		`t2_exist`,
		`t2_time_to_exe`,
		`t2_time_to_exe_guaranteed`,
		`t2_storage`,
		`t2_min_order`,
		`t2_probability`,
		`t2_markup`,
		`t2_price_purchase`,
		`t2_office_id`,
		`t2_storage_id`,
		`t2_product_json`,
		`t2_json_params`
		) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);')->execute( array($product_type, $product_id, $price, $count_need, $user_id, $time, $session_id, 
		$t2_manufacturer, 
		$t2_article, 
		$t2_article_show, 
		$t2_name, 
		$t2_exist, 
		$t2_time_to_exe, 
		$t2_time_to_exe_guaranteed, 
		$t2_storage, 
		$t2_min_order, 
		$t2_probability, 
		$t2_markup, 
		$t2_price_purchase, 
		$t2_office_id, 
		$t2_storage_id, 
		$t2_product_json,
		$t2_json_params
		) ) )
        {
            //1. Получаем ID последней всталенной записи
            $cart_record_id = $db_link->lastInsertId();

            //2. Добавляем в список записей корзины
            array_push($products_in_cart, $cart_record_id);
            
            //3. Резервируем товар на складе (И УЗНАЕМ ЗАКУПОЧНУЮ ЦЕНУ СКЛАДА)
            
			
			//3.1.Резервируем товар на складе
			$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = (`exist`-'.$count_need.'), `reserved` = (`reserved`+'.$count_need.') WHERE `id`= ?;')->execute( array($storage_record_id) );
			
			
            //3.2 Вносим детализированную запись корзины
            if($db_link->prepare('INSERT INTO `shop_carts_details` (`cart_record_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `price`, `price_purchase`) VALUES (?,?,?,?,?,?,?);')->execute( array($cart_record_id, $office_id, $storage_id, $storage_record_id, $count_need, $price_not_markup, $price_purchase) ) != true)
            {
				$db_link->prepare('DELETE FROM `shop_carts` WHERE `id` = ?;')->execute( array($cart_record_id) );
                $no_error = false;
            }
        }
        else
        {
            $no_error = false;
        }
    }//if( $product_type == 1 )//Каталожный
    else if( $product_type == 2 )//Автозапчасть Docpart
    {
        // **************************************************************************************************************
        // ***************************************************************************************************************
        // ****************************************************************************************************************
        // ***************************************************************************************************************
        // **************************************************************************************************************
        //Получаем поля продукта
        $t2_manufacturer = $product_object["manufacturer"];
        $t2_article = $product_object["article"];
        $t2_article_show = $product_object["article_show"];
        $t2_name = $product_object["name"];
        $t2_exist = $product_object["exist"];
        $t2_time_to_exe = $product_object["time_to_exe"];
        $t2_time_to_exe_guaranteed = $product_object["time_to_exe_guaranteed"];
        $t2_storage = $product_object["storage"].'';
        $t2_min_order = $product_object["min_order"];
        $t2_min_order = epc_moq_effective($db_link, (int)$user_id, (int)$t2_min_order);
        $t2_probability = $product_object["probability"];
        $price = $product_object["price"];
        $t2_price_purchase = $product_object["price_purchase"];
        $t2_markup = $product_object["markup"];
        $t2_office_id = $product_object["office_id"];
        $t2_storage_id = $product_object["storage_id"];
		$t2_json_params = $product_object["json_params"].'';
        
		if(!empty($product_object["count_need"])){
			$count_need = (int)$product_object["count_need"];
		}else{
			$count_need = max(1, (int)$t2_min_order);
		}
		if ($count_need < (int)$t2_min_order) {
			$count_need = (int)$t2_min_order;
		}
		
        $time = time();//Время добавления записи
        
		
		
		//Проверяем хеш, защищающий от подмены данных злоумышлненниками через JavaScript
		$computed_hash = docpart_type2_cart_check_hash($product_object, $price, $DP_Config->tech_key);
		$client_hash = isset($product_object["check_hash"]) ? trim((string) $product_object["check_hash"]) : '';
		// Guest price redaction used to set check_hash=0; treat placeholder zeros as missing.
		if ($client_hash === '0' || strtolower($client_hash) === 'null' || strtolower($client_hash) === 'undefined') {
			$client_hash = '';
		}
		if ($client_hash !== '' && !hash_equals($computed_hash, $client_hash))
		{
			$result = array();
			$result["status"] = false;
			$result["code"] = "35.2";
			$hash_message = translate_str_by_id(4463);
			if ($hash_message === false || $hash_message === null || $hash_message === '' || strpos($hash_message, 'ERROR STR_KEY') === 0)
			{
				$hash_message = 'Unable to add to cart: price data expired. Refresh the page and try again.';
			}
			$result["message"] = $hash_message;
			exit(json_encode($result));
		}
        
		
		//Определяем Б\У
		$by_flag = false;
		$t2_json_params_array = json_decode($t2_json_params, true);
		if( !empty($t2_json_params_array) && ((int) $t2_json_params_array['used'] === 1) ){
			$by_flag = true;
		}
		
        
        //Проверяем наличие такого же товара в корзине, если он не б\у
		if($by_flag === false){
			$check_already_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_carts` WHERE 
					`product_type`=2 AND 
					`user_id`=? AND 
					`session_id`=? AND 
					`t2_manufacturer` = ? AND 
					`t2_article` = ? AND 
					`t2_exist` = ? AND 
					`t2_time_to_exe` = ? AND 
					`t2_time_to_exe_guaranteed` = ? AND 
					`t2_probability` = ? AND 
					`t2_office_id` = ? AND 
					`t2_storage_id` = ? AND 
					CAST(`price` AS DECIMAL) = CAST(? AS DECIMAL);');
			$check_already_query->execute( array($user_id, $session_id, $t2_manufacturer, $t2_article, $t2_exist, $t2_time_to_exe, $t2_time_to_exe_guaranteed, $t2_probability, $t2_office_id, $t2_storage_id, $price) );
			if( $check_already_query->fetchColumn() > 0 )
			{
				$result = array();
				$result["status"] = false;
				$result["code"] = "already";
				exit(json_encode($result));
			}
		}
        
        
        
        //Убедились, что такого товара еще нет в корзине - формируем запрос на добавление
        $SQL_INSERT = "INSERT INTO `shop_carts` (
            `product_type`,
            `price`,
            `count_need`,
            `time`,
            `user_id`,
			`session_id`,
            `t2_manufacturer`,
            `t2_article`,
            `t2_article_show`,
            `t2_name`,
            `t2_exist`,
            `t2_time_to_exe`,
            `t2_time_to_exe_guaranteed`,
            `t2_storage`,
            `t2_min_order`,
            `t2_probability`,
            `t2_markup`,
            `t2_price_purchase`,
            `t2_office_id`,
            `t2_storage_id`,
            `t2_product_json`,
			`t2_json_params`
                ) VALUES (";
				

		$binding_values = array(2,$price,$count_need,$time,$user_id,$session_id,$t2_manufacturer,$t2_article,$t2_article_show,$t2_name,$t2_exist,$t2_time_to_exe,$t2_time_to_exe_guaranteed,(string)$t2_storage,$t2_min_order,$t2_probability,$t2_markup,$t2_price_purchase,$t2_office_id,$t2_storage_id,json_encode($product_object), $t2_json_params );
		
		$SQL_INSERT = $SQL_INSERT.str_repeat('?,', count($binding_values) - 1).'?)';
		
		
        //Добавляем запись в таблицу корзины
        if( $db_link->prepare($SQL_INSERT)->execute( $binding_values ) )
        {
            //1. Получаем ID новой записи
            $inserted_record_id = $db_link->lastInsertId();
            
            //2. Добавляем в список записей корзины
            array_push($products_in_cart, $inserted_record_id);
        }
        else
        {
            $no_error = false;
        }
        // ****************************************************************************************************************
        // ***************************************************************************************************************
        // **************************************************************************************************************
        // ***************************************************************************************************************
        // ****************************************************************************************************************
    }//~else if( $product_type == 2 )//Автозапчасть Docpart
    else
    {
        $result = array();
        $result["status"] = false;
        $result["code"] = "unknown_product_type";
        $result["message"] = translate_str_by_id(4464);
        exit(json_encode($result));
    }
    
}//for(по каждому объекту)









//Объект ответа
$result = array();



if($no_error)
{
    $result["status"] = true;
}
else
{
    $result["status"] = false;
}


exit(json_encode($result));
?>