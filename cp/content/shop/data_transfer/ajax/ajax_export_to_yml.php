<?php
/**
*	Скрипт формирования YML-файла для последующей загрузки каталога товаров на Яндекс.Маркет
*/



// Тестирование принудительным вызовом скрипта (0 / 1)
$_Testing_flag = 0;



// -----------------------------------------------------------------------------------------------------

if($_Testing_flag){
	
	// ... /cp/content/shop/data_transfer/ajax/ajax_export_to_yml.php
	
	set_time_limit(600);
	ini_set('display_errors', 1);
	ini_set('memory_limit', '1024M');
	
	//phpinfo();
	//exit('d');
	
	/*
	$f = fopen('log.txt', 'w');
	fwrite($f, json_encode($_GET));
	exit;
	*/
	
	$_GET = json_decode('{"export_options":"{\"data_output_mode\":\"download_file\",\"offices\":[\"1\"],\"group_id\":\"2\",\"FBY_flag\":0,\"properties_all_flag\":\"0\",\"no_published_category_upload_flag\":\"0\",\"no_published_product_upload_flag\":\"0\",\"count_flag\":\"1\",\"currencyId_flag\":\"1\",\"delivery_flag\":\"0\",\"weight_property\":\"\",\"length_property\":\"\",\"height_property\":\"\",\"width_property\":\"\",\"save_flag\":\"1\",\"arr_category\":[62,63,64,65,66,74,76,78]}","csrf_guard_key":"b5fec33a322afcc8423f1ed5044f37e9ead8d77b"}', true);
	
	/*
	$export_options = json_decode($_GET["export_options"], true);
	echo '<pre>';
	var_dump($export_options);
	echo '</pre>';
	exit;
	*/
	
}else{
	
	header('Content-Type: application/json;charset=utf-8;');

	set_time_limit(0);
	ini_set('display_errors', 0);
	ini_set('memory_limit', '1024M');
}

// -----------------------------------------------------------------------------------------------------



// Настройки выполнения скрипта
$export_options = array();
$export_options["offices"] = array(1);// Список магазинов, от которых выводить предложения
$export_options["data_output_mode"] = "download_file";// Способ вывода строки (оставить файл на сервере/скачать файл)
$export_options["group_id"] = 2;
if( !empty($_GET["export_options"]) )
{
	$export_options = json_decode($_GET["export_options"], true);
}
$category = json_encode($export_options['arr_category']);
$category_list = str_replace(array('[',']','{','}'),'',$category);// Список ID категорий для выгрузки



// -----------------------------------------------------------------------------------------------------

// Если магазин работает по модели FBY или FBS
// В зависимости от флага FBY_flag формируются разные файлы YML
$FBY_flag = (int) trim($export_options["FBY_flag"]);

// Имя формируемого YML файла
if($FBY_flag === 1){
	if($export_options["data_output_mode"] === "download_file"){
		$file_name = "yml_dump_FBY_FBS_download.xml";
	}else{
		$file_name = "yml_dump_FBY_FBS.xml";
	}
}else{
	if($export_options["data_output_mode"] === "download_file"){
		$file_name = "yml_dump_DBS_download.xml";
	}else{
		$file_name = "yml_dump_DBS.xml";
	}
}

// -----------------------------------------------------------------------------------------------------



// Флаг выгрузки всех свойств товаров
$properties_all_flag = (int) trim($export_options["properties_all_flag"]);

// Флаг выгрузки всех категорий товаров, включая не опубликованные
$no_published_category_upload_flag = (int) trim($export_options["no_published_category_upload_flag"]);

// Флаг выгрузки всех товаров, включая не опубликованные
$no_published_product_upload_flag = (int) trim($export_options["no_published_product_upload_flag"]);

// Флаг доступна ли курьерская доставка
$delivery_flag = (int) trim($export_options["delivery_flag"]);

// Флаг выгружать тег count
$count_flag = (int) trim($export_options["count_flag"]);

// Флаг выгружать тег currencyId
$currencyId_flag = (int) trim($export_options["currencyId_flag"]);

// Запомнить текущие настройки после нажатия кнопки
$save_flag = (int) trim($export_options["save_flag"]);

// Определим наличие скрипта водяного знака
$watermark_flag = (int) file_exists($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/watermark.php");
if($FBY_flag === 1){
	$watermark_flag = 0;// Для модели FBY запрещено загружать картинки с водяным знаком
}

// Константы
$property_types_tables = array("1"=>"int", "2"=>"float", "3"=>"text", "4"=>"bool", "5"=>"list", "6"=>"tree_list");// Постфиксы таблиц значений свойств - зависят от типа свойства
$products_images_dir = "content/files/images/products_images/";// Директория к изображениям товаров



// -----------------------------------------------------------------------------------------------------

require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;// Конфигурация CMS
// Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");

// -----------------------------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



// -------------------------------------------------------------------------------

//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('id'=>343, 'url'=>'shop/perenos-dannyx/vygruzka-na-yandeksmarket');//Выгрузка на Яндекс.Маркет
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/check_admin_access/check_admin_access.php");

// -------------------------------------------------------------------------------



// -------------------------------------------------------------------------------

if( ! $_Testing_flag ){
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
}

// -------------------------------------------------------------------------------



// -----------------------------------------------------------------------------------------------------



// Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

// Проверяем доступ в панель управления
if( ! DP_User::isAdmin() )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Forbidden';
	exit(json_encode($answer));
}

// Библиотека для XML
require_once('Array2XML.php');



// -----------------------------------------------------------------------------------------------------

// Счетчики
$_COUNT_products_categoryes_all = 0;// Количество товаров в указанных категориях
$_COUNT_products_read_all = 0;// Количество зачитанных и добавленных в YML товаров
$_COUNT_products_blocked_no_storage_record = 0;// Количество заблокированных товаров по причине отсутствия складской записи (цены или количества)
$_COUNT_products_blocked_yandex = 0;// Количество заблокированных товаров по причинам не удовлетворяющим обязательным условиям Яндекс (наличие таких свойств как Производитель, Изображение, Габариты, Вес)
$_COUNT_products_blocked_no_published = 0;// Количество пропущенных НЕ опубликованных товаров
$_Erors_arr = array();// Массив различных ошибок

// -----------------------------------------------------------------------------------------------------



// Сохраняем текущие выставленные настройки
if($save_flag === 1){
	if( ! file_exists('settings_yml.php') ){
		$f = fopen('settings_yml.php', 'w');
		fclose($f);
	}
	if( file_exists('settings_yml.php') ){
		$f = fopen('settings_yml.php', 'w');
		fwrite($f, json_encode($_GET));
		fclose($f);
	}else{
		$_Erors_arr[] = "Ошибка сохранения настроек. Проверьте права доступа на сервере, для записи файлов в папке.";
	}
}
if($save_flag === 3){
	// Сбросить настройки
	$f = fopen('settings_yml.php', 'w');
	fclose($f);
}



// Формируем массив со свойствами товаров, используется при формировании SQL-кода
$shop_categories_properties_map = array();
$sql = "SELECT * FROM `shop_categories_properties_map` ORDER BY `order`";
$query = $db_link->prepare($sql);
$query->execute();
while($record = $query->fetch())
{
	$shop_categories_properties_map[$record['category_id']][] = $record;
}



// -----------------------------------------------------------------------------------------------------

// Для каждого магазина получить список складов
$offices_all = array();
for($o=0; $o < count($export_options["offices"]); $o++)
{
	$office_id = $export_options["offices"][$o];// ID точки выдачи
	
	$storages_query = $db_link->prepare("SELECT DISTINCT(`storage_id`) AS `storage_id`, `additional_time`, (SELECT `iso_name` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_offices_storages_map`.`storage_id`)) AS `currency_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` IN(SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1);");
	$storages_query->execute( array($office_id) );
	while($storage = $storages_query->fetch() )
	{
		if($storage['currency_id'] == 'RUB'){
			$storage['currency_id'] = 'RUR';
		}
		$offices_all[$office_id][$storage['storage_id']] = array(
											"storage_id" => $storage['storage_id'],
											"additional_time" => $storage['additional_time'],
											"currency_id" => $storage['currency_id']
		);
	}
}

// Информация по первому магазину
$customer_office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
$customer_office_query->execute(array($export_options["offices"][0]));
$customer_office_info = $customer_office_query->fetch(PDO::FETCH_ASSOC);

// -----------------------------------------------------------------------------------------------------



// ФОРМИРОВАНИЕ ОБЪЕКТА SHOP
$shop = array(
	"#date"=>date("Y-m-d H:i", time() ),
	"shop"=>array(
		"name"=>translate_str_by_id($DP_Config->site_name),
		"company"=>translate_str_by_id($DP_Config->site_name),
		"url"=>$DP_Config->domain_path,
		"platform"=>"Eparts System",
		"version"=>"1",
		"agency"=>$customer_office_info['email']
	)
);



// -----------------------------------------------------------------------------------------------------

// Заполняем категории товаров
$category_no_published = array();// Массив для добавления ID категорий которые не опубликованы вместе с их вложенными подкатегориями что бы отфильтровать их
$category_list_arr = array();// Массив для добавления ID категорий что бы ниже по ним отфильтровать товары, так как изначально список ID может содержать не опубликованные категории
$categories_names = array();// Массив для добавления названий категорий в товары
$categories_url = array();// Массив для добавления URL категорий в товары
$shop["shop"]["categories"] = array();
$categories_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories` WHERE `id` IN($category_list) ORDER BY `order`, `level`;");
$categories_query->execute();
while( $category_record = $categories_query->fetch() )
{
	if($no_published_category_upload_flag === 0){
		if($category_record["published_flag"] != 1 || in_array($category_record["parent"], $category_no_published) !== false){
			$category_no_published[] = $category_record["id"];
			continue;
		}
	}
	
	$category = array(
		"category"=>array(
			"#id"=>$category_record["id"]
		)
	);
	if($category_record["parent"] > 0)
	{
		$category["category"]["#parentId"] = $category_record["parent"];
	}
	// Заполняем название категории - как индексный элемент массива
	$category["category"][] = array(trim(translate_str_by_id($category_record["value"])));
	array_push($shop["shop"]["categories"], $category);
	
	if($category_record["count"] == 0){
		array_push($category_list_arr, $category_record["id"]);// Добавляем в массив ID по которым будет фильтровать товары
		$categories_names[$category_record["id"]] = translate_str_by_id($category_record["value"]);
		$categories_url[$category_record["id"]] = $category_record["url"];
	}
}

// Определяем количество товаров в указанных категориях
$cnt_products_query = $db_link->prepare("SELECT COUNT(*) AS 'cnt' FROM `shop_catalogue_products` WHERE `category_id` IN($category_list);");
$cnt_products_query->execute();
$cnt_products_record = $cnt_products_query->fetch();
$_COUNT_products_categoryes_all = (int) $cnt_products_record['cnt'];

// -----------------------------------------------------------------------------------------------------



// Заполняем предложения магазина
$shop["shop"]["offers"] = array();

$converter = new Array2XML();
$converter->rootName = "yml_catalog";
$export_str = $converter->convert($shop);
$export_str = str_replace('<offers/></shop></yml_catalog>','<offers>',$export_str);



// -----------------------------------------------------------------------------------------------------

// Начинаем формировать файл
$export_file = fopen($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/".$file_name, "w");
fwrite($export_file, trim($export_str));

// -----------------------------------------------------------------------------------------------------



////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// Функция обработки одного товара
function data_processing($product_record){
	
	global $db_link, $DP_Config, $properties_all_flag, $shop_categories_properties_map, $property_types_tables, $offices_all, $FBY_flag, $categories_names, $categories_url, $export_file, $watermark_flag, $products_images_dir, $delivery_flag, $export_options, $_COUNT_products_categoryes_all, $_COUNT_products_read_all, $_COUNT_products_blocked_no_storage_record, $_COUNT_products_blocked_yandex, $_COUNT_products_blocked_no_published, $_Erors_arr, $currencyId_flag, $count_flag;
	
	
	
	// Изображения товара
	$picture = $product_record["file_name"];
	
	// Получаем текстовое описание товара
	$description = $product_record["content"];
	if(empty($description)){
		$description = $product_record['caption'];
	}
	
	// Получаем значения свойств товара
	$vendor = '';// Производитель - обязательный элемент для яндекса
	$vendorCode = '';// Артикул
	
	$weight = '';// Вес
	$length = '';// Длина
	$height = '';// Высота
	$width  = '';//  Ширина
	
	$dimensions = '';// Габариты
	
	if($FBY_flag == 1){
		$weight_property_value = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["weight_property"]));// Вес
		$length_property_value = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["length_property"]));// Длина
		$height_property_value = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["height_property"]));// Высота
		$width_property_value  = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["width_property"]));//  Ширина
	}
	
	$params = array();
	
	// Свойства товара
	$category_properties = $shop_categories_properties_map[$product_record["category_id"]];
	if(!empty($category_properties)){
		foreach($category_properties as $property_record)
		{
			
			$property_record["value"] = translate_str_by_id($property_record["value"]);
			
			if(!empty($product_record["property_".$property_record["id"]])){
				$param = array(
					"param"=>array(
						"#name"=>$property_record["value"]
					)
				);
				$param["param"][] = array($product_record["property_".$property_record["id"]]);
				array_push($params, $param);
				
				// Производитель
				if( array_search( $property_record["value"], array('Производитель', 'Manufacturer') ) !== false && $property_record["property_type_id"] == 5){
					$vendor = $product_record["property_".$property_record["id"]];
				}
				
				// Артикул
				if(  array_search( $property_record["value"], array('Артикул', 'Article') ) !== false && $property_record["property_type_id"] == 3){
					$vendorCode = $product_record["property_".$property_record["id"]];
				}
				
				if($FBY_flag == 1){
					// Вес
					if(!empty($weight_property_value) && $weight_property_value === $property_record["value"]){
						$weight = $product_record["property_".$property_record["id"]];
					}
					// Длина
					if(!empty($length_property_value) && $length_property_value === $property_record["value"]){
						$length = $product_record["property_".$property_record["id"]];
					}
					// Высота
					if(!empty($height_property_value) && $height_property_value === $property_record["value"]){
						$height = $product_record["property_".$property_record["id"]];
					}
					// Ширина
					if(!empty($width_property_value) && $width_property_value === $property_record["value"]){
						$width = $product_record["property_".$property_record["id"]];
					}
				}
			}
		}
	}
	
	
	
	// Округление цены
	$work_price = $product_record["customer_price"];
	if($DP_Config->price_rounding == '1')//Без копеечной части
	{
		if($work_price > (int)$work_price)
		{
			$work_price = (int)$work_price+1;
		}
		else
		{
			$work_price = (int)$work_price;
		}
	}
	else if($DP_Config->price_rounding == '2')//До 5 руб
	{
		$work_price = (integer)$work_price;
		$price_str = (string)$work_price;
		$price_str_last_char = (integer)$price_str[strlen($price_str)-1];
		if($price_str_last_char > 0 && $price_str_last_char < 5)
		{
			$work_price = $work_price + (5 - $price_str_last_char);
		}
		else if($price_str_last_char > 5 && $price_str_last_char <= 9)
		{
			$work_price = $work_price + (10 - $price_str_last_char);
		}
	}
	else if($DP_Config->price_rounding == '3')//До 10 руб
	{
		$work_price = (integer)$work_price;
		$price_str = (string)$work_price;
		$price_str_last_char = (integer)$price_str[strlen($price_str)-1];
		if($price_str_last_char != 0)
		{
			$work_price = $work_price + (10 - $price_str_last_char);
		}
	}
	$work_price = (float) number_format($work_price, 2, '.', '');
	$product_record["customer_price"] = $work_price;
	
	
	
	// Формируем offer_id
	$uniqid = '';
	if(!empty($vendorCode) && !empty($vendor)){
		$uniqid = $vendorCode.'/'.$vendor.'/'.$product_record["product_id"];
	}else{
		$uniqid = $product_record["product_id"];
	}
	$uniqid = str_replace(' ', '_', $uniqid);
	
	
	
	// Формируем габариты для модели FBY или FBS
	if($FBY_flag == 1){
		if(!empty($length) && !empty($width) && !empty($height)){
			$dimensions = $length .'/'. $width .'/'. $height;
		}
	}
	
	
	
	// Последняя проверка обязательных полей
	if(empty($product_record["customer_price"]) || empty($product_record["exist"])){
		$_COUNT_products_blocked_no_storage_record++;// Количество заблокированных товаров по причине отсутствия складской записи (цены или количества)
		return false;
	}
	if(empty($vendor) || empty($product_record["caption"]) || empty($picture) || empty($description)){
		$_COUNT_products_blocked_yandex++;// Количество заблокированных товаров по причинам не удовлетворяющим обязательным условиям Яндекс (наличие таких свойств как Производитель, Изображение, Габариты, Вес)
		return false;
	}
	if($FBY_flag == 1){
		if(empty($dimensions) || empty($weight)){
			$_COUNT_products_blocked_yandex++;// Количество заблокированных товаров по причинам не удовлетворяющим обязательным условиям Яндекс (наличие таких свойств как Производитель, Изображение, Габариты, Вес)
			return false;
		}
	}
	
	
	
	// ФОРМИРУЕТСЯ ЭЛЕМЕНТ OFFER
	if($FBY_flag === 1){
		
		// ОБРАБАТЫВАЕМ СРОК ПОСТАВКИ
		$time_to_exe = 0;
		$additional_time = (int) $offices_all[$product_record["office_id"]][$product_record["storage_id"]]["additional_time"];//Дополнительный срок поставки склада в часах
		if(time() < $product_record["arrival_time"]){
			$time_to_exe = (int)((($product_record["arrival_time"] + ($additional_time * 3600)) - time()) / 86400);
		}else{
			if($product_record["time_to_exe"] > 0){
				$time_to_exe = $product_record["time_to_exe"] + ((int)($additional_time / 24));
			}else{
				$time_to_exe = ((int)($additional_time / 24));
			}
		}
		
		//URL товара
        if($DP_Config->product_url == "id")
        {
            $product_url = $DP_Config->domain_path.$categories_url[$product_record["category_id"]]."/".$product_record["product_id"];
        }
        else
        {
            $product_url = $DP_Config->domain_path.$categories_url[$product_record["category_id"]]."/".$product_record["alias"];
        }
		
		$offer = array(
			"offer"=>array(
				"#id"=>$uniqid,
				"#available"=>"true",
				
				"name"=>translate_str_by_id($product_record["caption"]),
				"url"=>$product_url,
				"price"=>$product_record["customer_price"],
				
				"count"=>$product_record["exist"],
				
				"min-quantity"=>1,
				"categoryId"=>$product_record["category_id"],
				"typePrefix"=>$categories_names[$product_record["category_id"]]
			)
		);
		
		// Габариты
		if($dimensions != "" && $dimensions != NULL)
		{
			$offer["offer"]["dimensions"] = $dimensions;
		}
		
		// Вес
		if($weight != "" && $weight != NULL)
		{
			$offer["offer"]["weight"] = $weight;
		}
		
		// Курьерская доставка
		if($delivery_flag === 1){
			$offer["offer"]["delivery"] = 'true';
			$offer["offer"]["delivery-options"] = array("option"=>array("#cost"=>"1", "#days"=>$time_to_exe));
		}
		
		// Самовывоз
		$offer["offer"]["pickup"] = 'true';
		$offer["offer"]["pickup-options"] = array("option"=>array("#cost"=>"1", "#days"=>$time_to_exe));
		
	}else{
		
		//URL товара
        if($DP_Config->product_url == "id")
        {
            $product_url = $DP_Config->domain_path.$categories_url[$product_record["category_id"]]."/".$product_record["product_id"];
        }
        else
        {
            $product_url = $DP_Config->domain_path.$categories_url[$product_record["category_id"]]."/".$product_record["alias"];
        }
		
		$offer = array(
			"offer"=>array(
				"#id"=>$uniqid,
				"#available"=>"true",
				
				"name"=>translate_str_by_id($product_record["caption"]),
				"url"=>$product_url,
				"price"=>$product_record["customer_price"],
				"min-quantity"=>1,
				"categoryId"=>$product_record["category_id"],
				"typePrefix"=>$categories_names[$product_record["category_id"]]
			)
		);
		
		// Добавляем поле count
		if($count_flag === 1)
		{
			$offer["offer"]["count"] = $product_record["exist"];
		}
		
		// Добавляем поле currencyId
		if($currencyId_flag === 1)
		{
			$offer["offer"]["currencyId"] = $offices_all[$product_record["office_id"]][$product_record["storage_id"]]["currency_id"];
		}
	}
	
	// Добавляем поле Производитель
	if($vendor != "" && $vendor != NULL)
	{
		$offer["offer"]["vendor"] = $vendor;
	}
	
	// Добавляем поле Артикул
	if($vendorCode != "" && $vendorCode != NULL)
	{
		$offer["offer"]["vendorCode"] = $vendorCode;
	}
	
	//Добавляем изображение
	if(!empty($picture))
	{
		if($watermark_flag === 1){
			$offer["offer"]["picture"] = $DP_Config->domain_path."content/shop/catalogue/watermark.php?image_id=".$picture;
		}else{
			if(strpos($picture,'http') === 0){
				$offer["offer"]["picture"] = $picture;
			}else{
				$offer["offer"]["picture"] = $DP_Config->domain_path.$products_images_dir.$picture;
			}
		}
	}
	
	//Добавляем текстовое описание
	if($description != "")
	{
		$offer["offer"]["description"] = $description;
	}
	
	//Параметры добавляем только если они есть
	if(count($params) > 0)
	{
		for($param = 0; $param < count($params); $param++)
		{
			array_push($offer["offer"], $params[$param]);
		}
	}
	
	// Преобразовываем массив с товаром в XML
	$converter = new Array2XML();
	$converter->rootName = "root";
	$export_str = $converter->convert($offer);
	$export_str = str_replace('<?xml version="1.0" encoding="UTF-8"?>','',$export_str);
	$export_str = str_replace('<root>','',$export_str);
	$export_str = str_replace('</root>','',$export_str);
	$export_str = str_replace('<description>',"\n<description>\n<![CDATA[\n",$export_str);
	$export_str = str_replace('</description>',"\n]]>\n</description>\n",$export_str);
	$export_str = $export_str ."\n";
	
	// Записываем в YML файл объект товара
	fwrite($export_file, $export_str);
	$_COUNT_products_read_all++;// Количество зачитанных и добавленных в YML товаров
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



// -----------------------------------------------------------------------------------------------------

$t1 = time();// Время начала обработки товаров
$the_best_product = null;// Товар с наилучшим предложением, только он добавляется в YML

// Цикл по категориям
foreach($category_list_arr as $category_id)
{
	$SQL = "";// Единственный запрос для получения всех нужных товаров

	// По всем доступным магазинам
	for($office_iterator = 0; $office_iterator < count($export_options["offices"]); $office_iterator++)
	{
		$office_id = (int) trim($export_options["offices"][$office_iterator]);
		
		if($office_iterator > 0)
		{
			$SQL = $SQL . "UNION";
		}
		
		//----------------------------
		
		// Получаем список ID складов которые подключены к данному магазину, что бы не выбирать цены не подключенных складов
		
		$storages_id_in_office = "";
		$sql_storages = "SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` IN(SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1);";
		$stmt = $db_link->prepare($sql_storages);
		$stmt->execute(array($office_id));
		while( $record = $stmt->fetch() ){
			// Проверяем что склад был указан в настройках выгрузки
			if( ! in_array($record['storage_id'], $export_options["storages"]) ){
				continue;// Пропускаем склад
			}
			if($storages_id_in_office != ''){
				$storages_id_in_office .= ',';
			}
			$storages_id_in_office .= $record['storage_id'];
		}
		if($storages_id_in_office == ""){
			$storages_id_in_office = "0";// Если к магазину не подключен не один склад то поставим 0 что бы не было ошибки в запросе, отобразим товары без цен
		}
		
		//----------------------------
		
		// Определяем курс валюты для подключенных к данному магазину складов
		
		$currency_sql = "";
		$stmt = $db_link->prepare("SELECT `id`, (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = `currency`) AS `rate` FROM `shop_storages` WHERE `id` IN($storages_id_in_office);");
		$stmt->execute();
		while( $record = $stmt->fetch() ){
			$currency_sql .= "WHEN `shop_storages_data`.`storage_id` = ".$record['id']." THEN ".$record['rate']." ";
		}
		if($currency_sql != ""){
			$currency_sql = "(CASE ".$currency_sql."ELSE 1 END)";
		}else{
			$currency_sql = "1";// Склады не подключены, поставим 1 что бы не было ошибки в запросе
		}

		//----------------------------
		
		// Формируем ПРОДАЖНУЮ цену с наценкой
		
		$customer_price_sql = "";
		$stmt = $db_link->prepare("SELECT * FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `group_id` = ? AND `storage_id` IN($storages_id_in_office)");
		$stmt->execute(array($office_id, $export_options["group_id"]));
		while( $record = $stmt->fetch() ){
			$customer_price_sql .= "
				WHEN
					`shop_storages_data`.`storage_id` = ".$record['storage_id']." AND `shop_storages_data`.`price` * $currency_sql >= ".$record['min_point']." AND `shop_storages_data`.`price` * $currency_sql < ".$record['max_point']."
				THEN
					`price` * $currency_sql + `price` * $currency_sql * (".$record['markup']." / 100)
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
		
		// Формируем запрос картинки
		
		$products_images_sql = "(SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = `shop_storages_data`.`product_id` LIMIT 1) AS file_name,";
		if($watermark_flag === 1){
			$products_images_sql = "(SELECT `id` FROM `shop_products_images` WHERE `product_id` = `shop_storages_data`.`product_id` LIMIT 1) AS file_name,";
		}
		
		//----------------------------
		
		// Свойства товара
		
		$properties_all_sql = '';
		$category_properties = null;
		if(!empty($shop_categories_properties_map[$category_id])){
			$category_properties = $shop_categories_properties_map[$category_id];
		}
		if(!empty($category_properties)){
			foreach($category_properties as $property_record)
			{
				if($properties_all_flag === 0){
					// Пропускаем все свойства кроме обязательных для яндекса
					if($FBY_flag == 1){
						$weight_property_value = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["weight_property"]));// Вес
						$length_property_value = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["length_property"]));// Длина
						$height_property_value = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["height_property"]));// Высота
						$width_property_value  = str_replace(array("'", '"', "`", "#", "--"), '', trim($export_options["width_property"]));//  Ширина
						if($property_record["value"] != $weight_property_value && $property_record["value"] != $length_property_value && $property_record["value"] != $height_property_value && $property_record["value"] != $width_property_value && array_search($property_record["value"], array('Артикул', 'Article') ) === false && array_search($property_record["value"], array('Производитель', 'Manufacturer') ) === false ){
							continue;
						}
					}else{
						if( array_search($property_record["value"], array('Артикул', 'Article') ) === false && array_search($property_record["value"], array('Производитель', 'Manufacturer') ) ){
							continue;
						}
					}
				}
				
				// Получаем значение данного свойства для товара:
				$table_postfix = $property_types_tables[(string)$property_record["property_type_id"]];// Постфикс таблицы
				
				switch($property_record["property_type_id"])
				{
					case 1:
					case 2:
						$properties_all_sql .= "
						(SELECT `value` FROM `shop_properties_values_$table_postfix` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ".$property_record["id"]." LIMIT 1) AS 'property_".$property_record["id"]."', 
						";
						break;
					case 3:
						$properties_all_sql .= "
						(SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = '".$multilang_params['lang']."' AND `str_id` = (SELECT `value` FROM `shop_properties_values_$table_postfix` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ".$property_record["id"]." LIMIT 1) ) AS 'property_".$property_record["id"]."', 
						";
						break;
					case 4:
						$properties_all_sql .= "
						(SELECT `value` FROM `shop_properties_values_$table_postfix` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ".$property_record["id"]." LIMIT 1) AS 'property_".$property_record["id"]."', 
						";
						break;
					case 5:
						$properties_all_sql .= "
						( SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = '".$multilang_params['lang']."' AND `str_id` = (SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ".$property_record["id"]." LIMIT 1)) ) AS 'property_".$property_record["id"]."', 
						";
						break;
				}
			}
		}
		
		//----------------------------
		
		// Выбор всех товаров с конечной ценой покупателя удовлетворяющих фильтру
		
		$SQL = $SQL . "
		SELECT  
			`shop_catalogue_products`.`id` AS `product_id`, 
			`shop_catalogue_products`.`published_flag` AS `published_flag`, 
			`shop_catalogue_products`.`category_id` AS `category_id`, 
			`shop_catalogue_products`.`caption` AS `caption`, 
			`shop_catalogue_products`.`alias` AS `alias`, 
			
			(SELECT `content` FROM `shop_products_text` WHERE `product_id` = `shop_catalogue_products`.`id` LIMIT 1) AS `content`, 
			
			$products_images_sql
			$properties_all_sql
			
			'$office_id' AS 'office_id', 
			`shop_storages_data`.`storage_id` AS 'storage_id', 
			`shop_storages_data`.`id` AS `shop_storages_data_id`,
			`shop_storages_data`.`arrival_time` AS `arrival_time`,
			`shop_storages_data`.`time_to_exe` AS `time_to_exe`,
			`shop_storages_data`.`exist`,
			".$customer_price_sql."
		FROM `shop_catalogue_products` 

			LEFT OUTER JOIN `shop_storages_data` ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` AND `shop_storages_data`.`storage_id` IN($storages_id_in_office) AND `exist` > 0 AND `price` > 0
		
		WHERE `shop_catalogue_products`.`category_id` = $category_id
		
		ORDER BY `product_id` ASC
		";
	}
	
	//----------------------------
	
	if($_Testing_flag){
		/*
		echo '<pre>';
		echo $SQL;
		echo '</pre>';
		echo '<br>';
		echo '<br>';
		echo '=========================================================';
		echo '<br>';
		echo '<br>';
		//exit;
		*/
	}
	
	//----------------------------
	
	// Выполняем выборку из базы товаров
	$products_query = $db_link->prepare($SQL);
	$products_query->execute();
	while( $product_record = $products_query->fetch() )
	{
		$t2 = time();
		$t3 = $t2 - $t1;// Время прошедшее с начала обработки товаров
		if($t3 > 600){
			$_Erors_arr[] = "Error Time: Процесс привысил допустимое время выполнения и был остановлен принудительно. Рекомендации: уменьшите число выгружаемых категорий или товаров, сократите количество свойств товаров.";
			break(2);
		}
		
		if($_Testing_flag){
			if($_COUNT_products_read_all > 10){
				//break(2);
			}
		}
		
		// Если не указан флаг выгрузки всех товаров то пропускаем не опубликованные
		if( $no_published_product_upload_flag === 0 && $product_record["published_flag"] == 0 ){
			$_COUNT_products_blocked_no_published++;// Количество пропущенных НЕ опубликованных товаров
			continue;
		}
		
		// Если указан фильтр по цене
		if( ((int)$export_options["price_min_property"] > 0) ){
			if( ((int)$export_options["price_min_property"] > $product_record["customer_price"]) ){
				$_COUNT_products_blocked_yandex++;// Количество заблокированных товаров по причинам не удовлетворяющим обязательным условиям Яндекс (наличие таких свойств как Производитель, Изображение, Габариты, Вес)
				continue;
			}
		}
		if( ((int)$export_options["price_max_property"] > 0) ){
			if( ((int)$export_options["price_max_property"] < $product_record["customer_price"]) ){
				$_COUNT_products_blocked_yandex++;// Количество заблокированных товаров по причинам не удовлетворяющим обязательным условиям Яндекс (наличие таких свойств как Производитель, Изображение, Габариты, Вес)
				continue;
			}
		}
		
		// Добавляем товар в YML
		if($the_best_product != null && $the_best_product["product_id"] != $product_record["product_id"]){
			data_processing($the_best_product);
			$the_best_product = null;
		}
		
		// Определяем наилучший товар
		if( ($product_record["customer_price"] < $the_best_product["customer_price"]) || ($product_record["customer_price"] == $the_best_product["customer_price"] && $product_record["exist"] > $the_best_product["exist"]) || ($the_best_product == null) ){
			$the_best_product = $product_record;
		}
	}//while( $product_record = $products_query->fetch() )

	// Добавляем последний товар в YML
	if($the_best_product != null){
		data_processing($the_best_product);
		$the_best_product = null;
	}
	
}

// -----------------------------------------------------------------------------------------------------





//Завершаем формирование файла
$export_str = '</offers></shop></yml_catalog>';
fwrite($export_file, $export_str);
fclose($export_file);





// -----------------------------------------------------------------------------------------------------

// Результат
$answer = array();
$answer["status"] = true;
$answer["filename"] = $file_name;// Имя сформированного YML файла
$answer["_COUNT_products_categoryes_all"] = $_COUNT_products_categoryes_all;// Количество товаров в указанных категориях
$answer["_COUNT_products_read_all"] = $_COUNT_products_read_all;// Количество зачитанных и добавленных в YML товаров
$answer["_COUNT_products_blocked_no_storage_record"] = $_COUNT_products_blocked_no_storage_record;// Количество заблокированных товаров по причине отсутствия складской записи (цены или количества)
$answer["_COUNT_products_blocked_yandex"] = $_COUNT_products_blocked_yandex;// Количество заблокированных товаров по причинам не удовлетворяющим обязательным условиям Яндекс (наличие таких свойств как Производитель, Изображение, Габариты, Вес)
$answer["_COUNT_products_blocked_no_published"] = $_COUNT_products_blocked_no_published;// Количество пропущенных НЕ опубликованных товаров
$answer["_Erors_arr"] = $_Erors_arr;// Массив различных ошибок
$answer["time"] = $t3;// Общее время выполнения SQL запросов к базе данных

// -----------------------------------------------------------------------------------------------------



if($_Testing_flag){
	echo '<pre>';
	var_dump($answer);
	echo '</pre>';
	echo '<br/>';
	echo '<br/>';
	echo '<br/>';
}



exit(json_encode($answer));
?>