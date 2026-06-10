<?php
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
	$result = array();
	$result["status"] = false;
	$result["message"] = "No DB connect";
	exit(json_encode($result));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

////////////////////////////////////////////

//Если в браузере есть информация о статистике запросов не авторизованного пользователя, а сейчас пользователь авторизован, тогда привяжим их к пользователю
if( isset($_COOKIE["shop_stat"]) && $user_id > 0)
{
	$where = '';
	$binding_values = array();
	//Формируем строку условий запроса из id записей статистики
	$shop_stat = explode('_', $_COOKIE["shop_stat"]);
	if(is_array($shop_stat)){
		$str = '';
		foreach($shop_stat as $item){
			if($str != ''){
				$str .= ',';
			}
			$str .= '?';
			array_push($binding_values, (int)$item);
		}
		if($str != ''){
			$where = '`id` IN('.$str.')';
		}
	}
	//Если строка условий сформирована
	if($where !== ''){
		array_unshift($binding_values, $user_id);//Добавляем user_id в запрос
		$sql = "UPDATE `shop_stat_article_queries` SET `user_id` = ? WHERE ".$where;
		//Привязываем записи статистики к user_id
		$query = $db_link->prepare($sql);
		if($query->execute($binding_values) == true)
		{
			//Удалим ненужную информацию
			setcookie('shop_stat', '', time()-86400, '/');
			unset($_COOKIE["shop_stat"]);
		}
	}
}

////////////////////////////////////////////
$request_object = json_decode($_POST['request_object'], true);
$value = $request_object['value'];
$cnt_list = 50;//Максимальное количество строк отображения
$list = array();
$dump_list = array();//Список дампов для проверки на уникальность
$standart_list = array( 
						array('article'=>'C110', 'manufacturer'=>'DOLZ', 'name'=>translate_str_by_id(4194)),
						array('article'=>'12345', 'manufacturer'=>'FEBI', 'name'=>translate_str_by_id(4195)),
						array('article'=>'S56545', 'manufacturer'=>'BREMBO', 'name'=>translate_str_by_id(4196))
				 );



//Получаем последние запросы
$where = '';
$binding_values = array();
if($value == ''){
	//Если в браузере есть информация о статистике запросов не авторизованного пользователя
	if( isset($_COOKIE["shop_stat"]))
	{
		//Формируем строку условий запроса из id записей статистики
		$shop_stat = explode('_', $_COOKIE["shop_stat"]);
		if(is_array($shop_stat)){
			$str = '';
			foreach($shop_stat as $item){
				if($str != ''){
					$str .= ',';
				}
				$str .= '?';
				array_push($binding_values, (int)$item);
			}
			if($str != ''){
				$where = '`id` IN('.$str.')';
			}
		}
	}
	if($user_id > 0){
		if($where != ''){
			$where .= ' AND '; 
		}
		$where .= '`user_id` = ?';
		array_push($binding_values, $user_id);
	}
}
if($value != ''){
	if($where != ''){
		$where .= ' AND '; 
	}
	$where .= '(`article` LIKE ? OR `name` LIKE ?)';
	array_push($binding_values, $value.'%');
	array_push($binding_values, '%'.$value.'%');
}
if($where != ''){
	$sql = 'SELECT * FROM `shop_stat_article_queries` WHERE `id` IN(SELECT MAX(`id`) FROM `shop_stat_article_queries` WHERE '.$where.' GROUP BY `article`, `manufacturer`) ORDER BY `id` DESC LIMIT '.$cnt_list;
	$query = $db_link->prepare($sql);
	$query->execute($binding_values);
		while($record = $query->fetch()){
		$dump = md5($record['article'].$record['manufacturer']);
		if( array_search($dump, $dump_list) === false )
		{
			array_push($dump_list, $dump);//Вносим дамп в список уникальных дампов
			$list[] = array('article'=>$record['article'], 'manufacturer'=>$record['manufacturer'], 'name'=>$record['name']);
		}
	}
}

if($value == ''){
	//Добавляем стандартные строки если позволяет количество
	for($i=0; (count($list) < $cnt_list && count($standart_list) > $i); $i++){
		$dump = md5($standart_list[$i]['article'].$standart_list[$i]['manufacturer']);
		if( array_search($dump, $dump_list) === false )
		{
			array_push($dump_list, $dump);//Вносим дамп в список уникальных дампов
			$list[] = $standart_list[$i];
		}
	}
}else{
	for($i=0; count($standart_list) > $i; $i++){
		if(strpos($standart_list[$i]['article'], mb_strtoupper(trim($value), "UTF-8")) === 0){
			$dump = md5($standart_list[$i]['article'].$standart_list[$i]['manufacturer']);
			if( array_search($dump, $dump_list) === false )
			{
				array_push($dump_list, $dump);//Вносим дамп в список уникальных дампов
				$list[] = $standart_list[$i];
			}
		}
	}
}


//Из прайс-листов по артикулу
if($value != ''){
	$sql = 'SELECT * FROM `shop_docpart_prices_data` WHERE `article` LIKE ? LIMIT '.$cnt_list;
	$query = $db_link->prepare($sql);
	$query->execute(array($value.'%'));
		while($record = $query->fetch()){
		$dump = md5($record['article'].$record['manufacturer']);
		if( array_search($dump, $dump_list) === false )
		{
			array_push($dump_list, $dump);//Вносим дамп в список уникальных дампов
			$list[] = array('article'=>$record['article'], 'manufacturer'=>$record['manufacturer'], 'name'=>$record['name']);
		}
	}
}

//Из прайс-листов по наименованию
if($value != ''){
	$SQL_searsch_str = '';
	$binding_values = array();
	$values_arr = explode(' ',$value);
	if(!empty($values_arr))
	{
		foreach($values_arr as $item_str)
		{
			$item_str = trim($item_str);
			if(mb_strlen($item_str, 'utf-8') < 3)
			{
				continue;// Короткие слова пропускаем
			}
			if($SQL_searsch_str != '')
			{
				$SQL_searsch_str .= " AND ";
			}
			$SQL_searsch_str .= "(`name` LIKE ?)";
			array_push($binding_values, '%'.$item_str.'%');
		}
	}
	$sql = 'SELECT * FROM `shop_docpart_prices_data` LIMIT '.$cnt_list;

	if($SQL_searsch_str != '')
	{
		$sql = 'SELECT * FROM `shop_docpart_prices_data` WHERE '.$SQL_searsch_str.' LIMIT '.$cnt_list;
	}


	$query = $db_link->prepare($sql);
	$query->execute($binding_values);
		while($record = $query->fetch()){
		$dump = md5($record['article'].$record['manufacturer']);
		if( array_search($dump, $dump_list) === false )
		{
			array_push($dump_list, $dump);//Вносим дамп в список уникальных дампов
			$list[] = array('article'=>$record['article'], 'manufacturer'=>$record['manufacturer'], 'name'=>$record['name']);
		}
	}
}


//Из каталога товаров по артикулу
if($value != ''){
	$sql_article = "SELECT `product_id`, `value` FROM `shop_properties_values_text` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `lang_code` = 'ru' AND `value` = 'Артикул') AND `property_type_id` = 3) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` LIKE ?) LIMIT ".$cnt_list;
	$query_article = $db_link->prepare($sql_article);
	$query_article->execute(array($value.'%'));
	while($record_article = $query_article->fetch()){
		$product_id = $record_article['product_id'];
		
		$sql_manufacturer = "SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = ? AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `lang_code` = 'ru' AND `value` = 'Производитель') AND `property_type_id` = 5))";
		$query_manufacturer = $db_link->prepare($sql_manufacturer);
		$query_manufacturer->execute(array($product_id, $product_id));
		$record_manufacturer = $query_manufacturer->fetch();
		
		$sql_product = 'SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = ?';
		$query_product = $db_link->prepare($sql_product);
		$query_product->execute(array($product_id));
		$record_product = $query_product->fetch();
		
		$record = array();
		$record['article'] = translate_str_by_id($record_article['value']);
		$record['manufacturer'] = translate_str_by_id($record_manufacturer['value']);
		$record['name'] = translate_str_by_id($record_product['caption']);
		
		$dump = md5($record['article'].$record['manufacturer']);
		if( array_search($dump, $dump_list) === false )
		{
			array_push($dump_list, $dump);//Вносим дамп в список уникальных дампов
			$list[] = array('article'=>$record['article'], 'manufacturer'=>$record['manufacturer'], 'name'=>$record['name']);
		}
	}
}




//Из каталога товаров по наименованию
if($value != ''){
	$SQL_searsch_str = '';
	$binding_values = array();
	$values_arr = explode(' ',$value);
	if(!empty($values_arr))
	{
		foreach($values_arr as $item_str)
		{
			$item_str = trim($item_str);
			if(mb_strlen($item_str, 'utf-8') < 3)
			{
				continue;// Короткие слова пропускаем
			}
			if($SQL_searsch_str != '')
			{
				$SQL_searsch_str .= " AND ";
			}
			$SQL_searsch_str .= "(`caption` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `value` LIKE ?) )";
			array_push($binding_values, $multilang_params['lang']);
			array_push($binding_values, '%'.$item_str.'%');
		}
	}
	
	$sql = 'SELECT `id`, `caption` FROM `shop_catalogue_products` LIMIT '.$cnt_list;

	if($SQL_searsch_str != '')
	{
		$sql = 'SELECT `id`, `caption` FROM `shop_catalogue_products` WHERE '.$SQL_searsch_str.' LIMIT '.$cnt_list;
	}

	$query = $db_link->prepare($sql);
	$query->execute($binding_values);
	while($record_product = $query->fetch()){
		$product_id = $record_product['id'];
		
		$sql_article = "SELECT `value` FROM `shop_properties_values_text` WHERE `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `lang_code` = 'ru' AND `value` = 'Артикул') AND `property_type_id` = 3) AND `product_id` = ?";
		$query_article = $db_link->prepare($sql_article);
		$query_article->execute(array($product_id, $product_id));
		$record_article = $query_article->fetch();
		
		if(empty($record_article['value'])){
			continue;
		}
		
		$sql_manufacturer = "SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = ? AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `lang_code` = 'ru' AND `value` = 'Производитель') AND `property_type_id` = 5))";
		$query_manufacturer = $db_link->prepare($sql_manufacturer);
		$query_manufacturer->execute(array($product_id, $product_id));
		$record_manufacturer = $query_manufacturer->fetch();
		
		$record = array();
		$record['article'] = translate_str_by_id($record_article['value']);
		$record['manufacturer'] = translate_str_by_id($record_manufacturer['value']);
		$record['name'] = translate_str_by_id($record_product['caption']);
			
		$dump = md5($record['article'].$record['manufacturer']);
		if( array_search($dump, $dump_list) === false )
		{
			array_push($dump_list, $dump);//Вносим дамп в список уникальных дампов
			$list[] = array('article'=>$record['article'], 'manufacturer'=>$record['manufacturer'], 'name'=>$record['name']);
		}
	}
}


/*
//ДАЛЕЕ ДЛЯ ОТЛАДКИ
//Функция замены первого вхождения строки
function str_replace_once($search, $replace, $text) 
{ 
   $pos = strpos($text, $search); 
   return $pos!==false ? substr_replace($text, "'".$replace."'", $pos, strlen($search)) : $text; 
}

//Боевой SQL-запрос присваем в $SQL_bebug, чтобы боевой остался без изменений, т.к. он будет далее использоваться в скрипте
$SQL_bebug = $sql;
//Цикл по массиву значений, которые нужно биндить
for( $i=0 ; $i < count($binding_values) ; $i++ )
{
	$SQL_bebug = str_replace_once('?', $binding_values[$i], $SQL_bebug);
}
*/


$result = array();
$result['list'] = $list;
//$result['sql'] = $SQL_bebug;
$json = json_encode($result, JSON_UNESCAPED_UNICODE);
exit($json);
?>