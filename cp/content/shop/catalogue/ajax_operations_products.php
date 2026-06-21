<?php
/**
 * Скрипт для обработки различных операций над таблицей
*/


//Подключаем строки с подзапросами, с учетом мультиязычности
require_once( $_SERVER['DOCUMENT_ROOT']."/content/shop/catalogue/cat_lang_general.php" );


//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
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


//Проверяем право менеджера
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$admin_id = DP_User::getAdminId();

// формируем пагинацию
// $all 		= количество постов в категории (определяем количество постов в базе данных)
// $lim 		= количество постов, размещаемых на одной странице
// $prev 		= количество отображаемых ссылок до и после номера текущей страницы
// $curr_link 	= номер текущей страницы (получаем из URL)
// $curr_css 	= css-стиль для ссылки на "текущую (активную)" страницу
// $link 		= часть адреса, используемый для формирования линков на другие страницы
function pagination($all, $lim, $prev, $curr_link, $curr_css, $link)
{
    $html = '';
	// осуществляем проверку, чтобы выводимые первая и последняя страницы
    // не вышли за границы нумерации
    $first = $curr_link - $prev;
    if ($first < 1) $first = 1;
    $last = $curr_link + $prev;
    if ($last > ceil($all/$lim)) $last = ceil($all/$lim);
 
    // начало вывода нумерации
    // выводим первую страницу
    $y = 1;
    if ($first > 1) $html .= "<li class='paginate_button'><a onclick='go_to_page({$y})'>1</a></li>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<li class='paginate_button'><a onclick='go_to_page({$y})'>...</a></li>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<li class='paginate_button'><a onclick='go_to_page({$y})'>$i</a></li>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= '<li class="paginate_button"><a class="'.$curr_css.'">'. $i .'</a></li>';
        } else {
            $alink = "<li class='paginate_button'><a onclick='go_to_page(";
            if($i != 1) $alink .= "{$i}";
            $alink .= ")'>$i</a></li>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < ceil($all / $lim) && ceil($all / $lim) - $last > 2) $html .=  "<li class='paginate_button'><a onclick='go_to_page({$y})'>...</a></li>";
    // выводим последнюю страницу
    $e = ceil($all / $lim);
    if ($last < ceil($all / $lim)) $html .=  "<li class='paginate_button'><a onclick='go_to_page({$e})'>$e</a></li>";
	
	return $html;
}


// Формирование информации по вложенности товара в категории
function get_breadcrumbs_category($category_id)
{
	global $db_link;
	global $multilang_params;
	
	$breadcrumbs = '';
	
	$res = $db_link->prepare("SELECT `alias`, `url`, `value`, `parent` FROM `shop_catalogue_categories` WHERE `id` = ?");
	$res->execute(array($category_id));
	$row = $res->fetch();
	
	if($row['parent'] > 0){
		$breadcrumbs .= get_breadcrumbs_category($row['parent']);
	}
	
	if($breadcrumbs != ''){
		$breadcrumbs .= ' <i class="fa fa-arrow-right" aria-hidden="true"></i> ';
	}
	$breadcrumbs .= '<a target="_blank" href="'.$multilang_params['lang_href'].'/'.$row['url'].'">'.translate_str_by_id($row['value']).'</a>';
	
	return $breadcrumbs;
}



// Функция формирования WHERE строки для запроса
function get_where($where_object)
{
	global $binding_values;
	global $show_limited;
	
	global $article_lang;
	global $manufacturer_lang;
	
	$where = '';
	
	if(!empty($where_object))
	{
		$category_id = (int) $where_object['category_id'];
		$search_text = trim(urldecode($where_object['search_text']));
		$storage_id = (int) $where_object['storage_id'];
		$article = trim($where_object['article']);
		$manufacturer = trim($where_object['manufacturer']);
		$no_article = (int) $where_object['no_article'];
		$no_manufacturer = (int) $where_object['no_manufacturer'];
		
		$article = strip_tags(mb_strtoupper(trim(urldecode($article)), 'UTF-8'));
		$manufacturer = strip_tags(mb_strtoupper(trim(urldecode($manufacturer)), 'UTF-8'));
		
		$article = strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/", "", $article));
		$manufacturer = htmlentities(mb_strtoupper(trim($manufacturer), "UTF-8"), ENT_QUOTES, "UTF-8");
		
		if(!empty($category_id))
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "(`shop_catalogue_products`.`category_id` IN(?))";
			array_push($binding_values, $category_id);
		}
		
		if(!empty($storage_id))
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "(`shop_storages_data`.`storage_id` IN(?))";
			array_push($binding_values, $storage_id);
		}
		
		if($article !== '')
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "( `shop_catalogue_products`.`id` IN (SELECT DISTINCT `product_id` FROM `shop_properties_values_text` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 3 AND `value` IN ".$article_lang." ) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` = ?) ) )";
			array_push($binding_values, $article);
		}
		
		if($manufacturer !== '')
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "( `shop_catalogue_products`.`id` IN (SELECT DISTINCT `product_id` FROM `shop_properties_values_list` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `value` IN ".$manufacturer_lang." ) AND `value` IN (SELECT `id` FROM `shop_line_lists_items` WHERE `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` = ? ) )))";
			array_push($binding_values, $manufacturer);
		}
		
		if(!empty($no_article))
		{
			if($where != ''){$where .= ' AND ';}
			//$where .= "(`shop_catalogue_products`.`id` NOT IN(SELECT DISTINCT `product_id` FROM `shop_properties_values_text` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 3 AND `value` = 'Артикул') AND `value` != ?))";
			
			
			
			$where .= "( `shop_catalogue_products`.`id` NOT IN (SELECT DISTINCT `product_id` FROM `shop_properties_values_text` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 3 AND `value` IN ".$article_lang." ) AND (`value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` != ?) AND `value` != 0 ) ) )";
			
			
			array_push($binding_values, '');
		}
		
		if(!empty($no_manufacturer))
		{
			if($where != ''){$where .= ' AND ';}
			//$where .= "(`shop_catalogue_products`.`id` NOT IN (SELECT DISTINCT `product_id` FROM `shop_properties_values_list` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `value` = 'Производитель') AND `value` IN(SELECT `id` FROM `shop_line_lists_items` WHERE `value` != ?)))";
			
			
			$where .= "( `shop_catalogue_products`.`id` NOT IN (SELECT DISTINCT `product_id` FROM `shop_properties_values_list` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `value` IN ".$manufacturer_lang." ) AND (`value` IN (SELECT `id` FROM `shop_line_lists_items` WHERE `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` != ? ) ) AND `value` != 0 ) ) )";
			
			
			
			array_push($binding_values, '');
		}
		
		////////////////////////////////////////////////////////////////////////////////////
		
		if(!empty($search_text))
		{
			$search_text_arr = explode(' ', $search_text);
			$where_tmp = '';
			
			
			$where_tmp_2 = '';
			if($where_tmp != ''){$where_tmp .= ' OR ';}
			foreach($search_text_arr as $search_text_item){
				if(mb_strlen($search_text_item, 'UTF-8') >= 3){
					if($where_tmp_2 != ''){$where_tmp_2 .= ' AND ';}
					$where_tmp_2 .= "(`shop_catalogue_products`.`id` IN (SELECT DISTINCT `product_id` FROM `shop_properties_values_text` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 3 AND `value` IN ".$article_lang." ) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` LIKE ?)))";
					array_push($binding_values, '%'.$search_text_item.'%');
				}
			}
			$where_tmp .= $where_tmp_2;
			if($where_tmp != ''){$where_tmp = '('. $where_tmp .')';}
			
			
			$where_tmp_2 = '';
			if($where_tmp != ''){$where_tmp .= ' OR ';}
			foreach($search_text_arr as $search_text_item){
				if(mb_strlen($search_text_item, 'UTF-8') >= 3){
					if($where_tmp_2 != ''){$where_tmp_2 .= ' AND ';}
					$where_tmp_2 .= " (`shop_catalogue_products`.`id` IN (SELECT DISTINCT `product_id` FROM `shop_properties_values_list` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `value` IN ".$manufacturer_lang.") AND `value` IN (SELECT `id` FROM `shop_line_lists_items` WHERE `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` LIKE ?))) )";
					array_push($binding_values, '%'.$search_text_item.'%');
				}
			}
			$where_tmp .= $where_tmp_2;
			if($where_tmp != ''){$where_tmp = '('. $where_tmp .')';}
			
			
			$where_tmp_2 = '';
			if($where_tmp != ''){$where_tmp .= ' OR ';}
			foreach($search_text_arr as $search_text_item){
				if(mb_strlen($search_text_item, 'UTF-8') >= 3){
					if($where_tmp_2 != ''){$where_tmp_2 .= ' AND ';}
					$where_tmp_2 .= "(`shop_catalogue_products`.`caption` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `value` LIKE ? ) )";
					array_push($binding_values, '%'.$search_text_item.'%');
				}
			}
			$where_tmp .= $where_tmp_2;
			if($where_tmp != ''){$where_tmp = '('. $where_tmp .')';}
			
			
			if($where_tmp != ''){
				if($where != ''){$where .= ' AND ';}
				$where .= '('. $where_tmp .')';
			}
		}
		
		////////////////////////////////////////////////////////////////////////////////////
		
		if($show_limited) {
			if($where != ''){$where .= ' AND ';}
			$where .= "(`shop_catalogue_products`.`min_limit_status` = '1')";
			array_push($binding_values, '');
		}

		if(!empty($where)){
			$where = 'WHERE ' . $where;
		}
	}
	else
	{
		if($show_limited) {
			if($where != ''){$where .= ' AND ';}
			$where .= "(`shop_catalogue_products`.`min_limit_status` = '1')";
			array_push($binding_values, '');
		}
		
		if(!empty($where)){
			$where = 'WHERE ' . $where;
		}
	}
	
	return $where;
}

// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//Проверяем право менеджера
if( ! DP_User::isAdmin())
{
	$answer = array('status'=>false);
	exit(json_encode($answer));
}



$answer = array('status'=>false);
$request_object = json_decode($_POST['request_object'], true);



switch($request_object['action'])
{
	case 'save_product_status_limit':
		$product_id = $request_object['product_id'];
		$status = $request_object['status'];
	
		$pattern_query = $db_link->prepare("UPDATE `shop_catalogue_products` SET `min_limit_enable` = ? WHERE `id` = ?;");

		if($pattern_query->execute( array($status,$product_id)) != true ) {
			
			$error = translate_str_by_key('1711368522_1_5f735d1486aa51eb9a61df1cd635a0fb').'. ' . $db_link->errorInfo();
			$answer = array('status'=>false, "message"=>$error);
			
		} else {
			
			$answer = array('status'=>true);
		}
		break;
	case 'save_product_value_limit':
			$product_id = $request_object['product_id'];
			$value = $request_object['value'];
		
			$pattern_query = $db_link->prepare("UPDATE `shop_catalogue_products` SET `min_limit` = ? WHERE `id` = ?;");
	
			if($pattern_query->execute( array($value,$product_id)) != true ) {
				
				$error = translate_str_by_key('1711368522_1_5f735d1486aa51eb9a61df1cd635a0fb').'. ' . $db_link->errorInfo();
				$answer = array('status'=>false, "message"=>$error);
				
			} else {
				
				$answer = array('status'=>true);
			}
	break;
	case 'get_table':
		/*
		$kol - количество записей для вывода
		$art - с какой записи выводить
		$total - всего записей
		$page - текущая страница
		$str_pag - количество страниц для пагинации
		*/
		// Текущая страница
		$page = (int)$request_object['page'];
		if(empty($page))
		{
			$page = 1;
		}
		
		$kol = 50;//количество записей для вывода
		$art = ($page * $kol) - $kol;//с какой записи выводить
		
		$binding_values = array();
		$show_limited = (isset($request_object['limited']) && $request_object['limited']) ? true : false;


		$where = get_where($request_object['where_object']);
		
		// Определяем количество записей в таблице
		if(empty($where)){
			$res = $db_link->prepare("SELECT COUNT(*) AS `count` FROM `shop_catalogue_products`");
		}else{
			$res = $db_link->prepare("SELECT COUNT(*) AS `count` FROM (
				SELECT DISTINCT `all`.`id` FROM(
				SELECT `shop_catalogue_products`.`id` FROM `shop_catalogue_products` LEFT JOIN `shop_storages_data` ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` $where) AS `all`) AS `all_count`"
			);
		}
		
		$res->execute($binding_values);
		$row = $res->fetch();
		$total = (int)$row['count']; // всего записей	
		
		// Количество страниц для пагинации
		$str_pag = ceil($total / $kol);
		
		// Сортировка товаров
		$sort_field = "exist";
		$sort_asc_desc = "asc";
		if( isset($_COOKIE["stock_sort"]) )
		{
			$stock_sort = $_COOKIE["stock_sort"];
		}
		if($stock_sort != NULL)
		{
			$stock_sort = json_decode($stock_sort, true);
			$sort_field = $stock_sort["field"];
			$sort_asc_desc = $stock_sort["asc_desc"];
		}
		if($sort_asc_desc == "asc"){
			$ASC_DESC = "ASC";
		}else{
			$ASC_DESC = "DESC";
		}
		switch($sort_field)
		{
			case "caption":
				$SORT = "`caption_lang` $ASC_DESC, `shop_storages_data`.`exist` $ASC_DESC";
				$SORT_2 = "`shop_storages_data`.`exist` $ASC_DESC";
				break;
			case "id":
			case "category_id":
			case "published_flag":
				$SORT = "`shop_catalogue_products`.`".$sort_field."` $ASC_DESC, `shop_storages_data`.`exist` $ASC_DESC";
				$SORT_2 = "`shop_storages_data`.`exist` $ASC_DESC";
				break;
			case "exist":
				$SORT = "`shop_storages_data`.`".$sort_field."` $ASC_DESC, `shop_storages_data`.`price` $ASC_DESC";
				$SORT_2 = "`shop_storages_data`.`".$sort_field."` $ASC_DESC, `shop_storages_data`.`price` $ASC_DESC";
				break;
			case "price":
			case "storage_id":
			case "reserved":
			case "issued":
				$SORT = "`shop_storages_data`.`".$sort_field."` $ASC_DESC, `shop_storages_data`.`exist` $ASC_DESC";
				$SORT_2 = "`shop_storages_data`.`".$sort_field."` $ASC_DESC, `shop_storages_data`.`exist` $ASC_DESC";
				break;
			case "min_limit":
				$SORT = "`shop_catalogue_products`.`".$sort_field."` $ASC_DESC, `shop_storages_data`.`exist` $ASC_DESC";
				$SORT_2 = "`shop_storages_data`.`exist` $ASC_DESC";
				break;
			case "min_limit_enable":
				$SORT = "`shop_catalogue_products`.`".$sort_field."` $ASC_DESC, `shop_storages_data`.`exist` $ASC_DESC";
				$SORT_2 = "`shop_storages_data`.`exist` $ASC_DESC";
				break;
			default:
				$sort_field = "exist";
				$sort_asc_desc = "asc";
				$SORT = "`shop_storages_data`.`exist` $ASC_DESC, `shop_storages_data`.`price` $ASC_DESC";
				$SORT_2 = "`shop_storages_data`.`exist` $ASC_DESC, `shop_storages_data`.`price` $ASC_DESC";
		}
		$sort_field_img = $sort_field . '_img';
		$$sort_field_img = ' <img src="/content/files/images/sort_'.$sort_asc_desc.'.png" style="width:15px; position: relative; top: -3px;"/>';
		
		
		
		if(!empty($where)){
			$sql = "
					SELECT DISTINCT `all`.`id`, @i := @i + 1 AS `row_number` FROM(
					SELECT `shop_catalogue_products`.`id` , (SELECT `value` FROM `lang_text_strings_translation` WHERE `str_id` = `shop_catalogue_products`.`caption` AND `lang_code` = '".$multilang_params['lang']."' ) AS `caption_lang`
					FROM `shop_catalogue_products` LEFT JOIN `shop_storages_data` ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` $where ORDER BY $SORT) AS `all` LIMIT $art, $kol
			";
		}else{
			$sql = "
					SELECT DISTINCT `all`.`id`, @i := @i + 1 AS `row_number` FROM(
					SELECT `shop_catalogue_products`.`id`, (SELECT `value` FROM `lang_text_strings_translation` WHERE `str_id` = `shop_catalogue_products`.`caption` AND `lang_code` = '".$multilang_params['lang']."' ) AS `caption_lang` FROM `shop_catalogue_products` LEFT JOIN `shop_storages_data` ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` ORDER BY $SORT) AS `all` LIMIT $art, $kol
			";
		}
		
		//*****************************************************************************************
		//*****************************************************************************************
		
		//ДАЛЕЕ ДЛЯ ОТЛАДКИ
		//Функция замены первого вхождения строки
		function str_replace_once($search, $replace, $text) 
		{ 
		   $pos = strpos($text, $search); 
		   return $pos!==false ? substr_replace($text, $replace, $pos, strlen($search)) : $text; 
		}

		//Боевой SQL-запрос присваем в $SQL_bebug, чтобы боевой остался без изменений, т.к. он будет далее использоваться в скрипте
		$SQL_bebug = $sql;
		
		//Цикл по массиву значений, которые нужно биндить
		for( $i=0 ; $i < count($binding_values) ; $i++ )
		{
			$SQL_bebug = str_replace_once('?', "'".$binding_values[$i]."'", $SQL_bebug);
		}
		
		if($_SERVER['REMOTE_ADDR'] == '80.82.46.136'){
			//echo $SQL_bebug;
		}
		
		//*****************************************************************************************
		//*****************************************************************************************
		
		$query = $db_link->prepare($sql);
		$query->execute($binding_values);
		
		/////////////////////////////

		// Склады
		$storages = array();
		$query_storages = $db_link->prepare("SELECT `id`, `name` FROM `shop_storages` WHERE `interface_type` = 1;");
		$query_storages->execute();
		while($row_storages = $query_storages->fetch())
		{
			$storages[$row_storages['id']] = $row_storages['name'];
		}
		
		// Категории
		$categories = array();
		$query_categories = $db_link->prepare("SELECT `id`, `url`, `value` FROM `shop_catalogue_categories`");
		$query_categories->execute();
		while($row_categories = $query_categories->fetch())
		{
			$row_categories['breadcrumbs'] = get_breadcrumbs_category($row_categories['id']);
			$categories[$row_categories['id']] = $row_categories;
		}
		
		/////////////////////////////
		
		$html = '';
		$query_product = $db_link->prepare("SELECT * FROM `shop_catalogue_products` WHERE `shop_catalogue_products`.`id` = ?");
		while($rov = $query->fetch() )
		{

			$query_product->execute(array($rov['id']));
			$rov = $query_product->fetch();
			
			
			//////////////////////////
			
			$shop_storages_data = array();
			$SQL_storages_data = "SELECT * FROM `shop_storages_data` WHERE `product_id` = ? ";
			if(!empty($request_object['where_object']['storage_id']))
			{ 
				$SQL_storages_data .= " AND `storage_id` IN(".$request_object['where_object']['storage_id'].")";
			}
			else
			{
				$SQL_storages_data .= " AND `storage_id` IN(SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1 AND `users` LIKE '%$admin_id%')";
			}
			$SQL_storages_data .= "ORDER BY $SORT_2";

			$query_shop_storages_data = $db_link->prepare($SQL_storages_data);
			$query_shop_storages_data->execute(array($rov['id']));
			while($row_shop_storages_data = $query_shop_storages_data->fetch() )
			{
				$shop_storages_data[] = $row_shop_storages_data;
			}
			
			//////////////////////////
			
			$query_article = $db_link->prepare("(SELECT `value` FROM `shop_properties_values_text` WHERE `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?) AND `value` IN ".$article_lang." AND `property_type_id` = 3) AND `product_id` = ?)");
			$query_article->execute(array($rov['id'], $rov['id']));
			$row_article = $query_article->fetch();
			$article = $row_article['value'];
			
			$query_manufacturer = $db_link->prepare("(SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = ? AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?) AND `value` IN ".$manufacturer_lang." AND `property_type_id` = 5)))");
			$query_manufacturer->execute(array($rov['id'], $rov['id']));
			$row_manufacturer = $query_manufacturer->fetch();
			$manufacturer = $row_manufacturer['value'];
			
			
			//$SELECT_type1_manufacturer = "(SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = `".$DP_Config->dbprefix."shop_orders_items`.`product_id` AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = `".$DP_Config->dbprefix."shop_orders_items`.`product_id`) AND `value` = 'Производитель' AND `property_type_id` = 5)))";
	
			//////////////////////////
			
			$background_exist = '#f3f3f3';
			if($shop_storages_data[0]['exist'] === null){
				$background_exist = '#d1471c';
			}
			if($shop_storages_data[0]['exist'] === '0'){
				$background_exist = '#ffe500';
			}
			
			
			$html .= '<tbody>';
			$class = '';
			$published_flag = '';
			$min_limit_checkbox = $rov['min_limit_enable'] == 1 ? 'checked' : '';
			if($rov['published_flag'] == 0){
				$class = 'no_published_flag';
				$published_flag = '<i title="'.translate_str_by_id(2747).'" class="fa fa-ban" aria-hidden="true"></i>';
			}
			$html .= '
			<tr class="'.$class.'" id="show_line_'. $rov['id'] .'" data-product-id="'. $rov['id'] .'">
				<td>'. $rov['id'] .'</td>
				<td>'. translate_str_by_id($article) .'</td>
				<td>'. translate_str_by_id($manufacturer) .'</td>
				<td style="white-space: normal;">'. translate_str_by_id($rov['caption']) .'</td>
				<td style="white-space: normal;">'. translate_str_by_id($categories[$rov['category_id']]['value']) .'<div class="breadcrumbs_category">'. $categories[$rov['category_id']]['breadcrumbs'] .'</div></td>
				<td>'. $shop_storages_data[0]['storage_id'] .' - '. $storages[$shop_storages_data[0]['storage_id']] .'</td>
				<td style="text-align:right;">'. number_format($shop_storages_data[0]['price'],2,'.',' ') .'</td>
				<td style="text-align:center; background:'.$background_exist.';">'. $shop_storages_data[0]['exist'] .'</td>
				<td style="text-align:center;">'. $shop_storages_data[0]['issued'] .'</td>
				<td style="text-align:center;">'. $shop_storages_data[0]['reserved'] .'</td>
				<td style="text-align:center;"><input name="product_min_limit_value" type="number" class="js-value_limit_input form-control" data-product-id="'. $rov['id'] .'" value="'. $rov['min_limit'] .'" /></td>
				<td style="text-align:center;"><input type="checkbox" '. $min_limit_checkbox .' name="product_min_limit" class="js-status_limit_input" data-product-id="'. $rov['id'] .'" id="product_min_limit_'. $rov['id'] .'" /></td>
				<td style="text-align:center;">'. $published_flag .'</td>
				<td>
					<a target="_blank" href="/'. $DP_Config->backend_dir .'/shop/catalogue/products/product?category_id='. $rov['category_id'] .'&product_id='. $rov['id'] .'" class="btn btn-sm btn-primary" title="'.translate_str_by_id(2748).'"><i class="fas fa-pencil-alt"></i></a>
					<a target="_blank" href="/'. $DP_Config->backend_dir .'/shop/logistics/stock/product?product_id='. $rov['id'] .'"class="btn btn-sm btn-primary" title="'.translate_str_by_id(764).'"><i class="fas fa-plus"></i></a>
				</td>
			</tr>
			';
			
			
			for($i=1; $i<count($shop_storages_data); $i++){
				$background_exist = '#f3f3f3';
				if($shop_storages_data[$i]['exist'] <= 0){
					$background_exist = '#ffe500';
				}
				$html .= '
				<tr class="shop_storages_data">
					<td></td>
					<td></td>
					<td></td>
					<td></td>
					<td></td>
					<td>'. $shop_storages_data[$i]['storage_id'] .' - '. $storages[$shop_storages_data[$i]['storage_id']] .'</td>
					<td style="text-align:right;">'. number_format($shop_storages_data[$i]['price'],2,'.',' ') .'</td>
					<td style="text-align:center; background:'.$background_exist.';">'. $shop_storages_data[$i]['exist'] .'</td>
					<td style="text-align:center;">'. $shop_storages_data[$i]['reserved'] .'</td>
					<td style="text-align:center;">'. $shop_storages_data[$i]['issued'] .'</td>
					<td style="text-align:center;"></td>
					<td style="text-align:center;"></td>
					<td style="text-align:center;"></td>
					<td style="text-align:center;"></td>
				</tr>
				';
			}
			$html .= '</tbody>';
		}
		
		if($html != ''){
			$html = '
			<table class="table table-bordered table-hover">
				<thead>
				<tr>
						<th><a onClick="sort(\'id\')">ID'.${'id_img'}.'</a></th>
						<th><a style="cursor: default;">'.translate_str_by_id(2071).'</a></th>
						<th><a style="cursor: default;" title="'.translate_str_by_id(2070).'">'.translate_str_by_key('4276').'</a></th>
						<th><a onClick="sort(\'caption\')">'.translate_str_by_key('2102').''.${'caption_img'}.'</a></th>
						<th><a onClick="sort(\'category_id\')">'.translate_str_by_key('2749').''.${'category_id_img'}.'</a></th>
						<th><a onClick="sort(\'storage_id\')">'.translate_str_by_key('2750').''.${'storage_id_img'}.'</a></th>
						<th style="text-align:right;"><a onClick="sort(\'price\')">'.translate_str_by_key('2751').''.${'price_img'}.'</a></th>
						<th style="text-align:center;"><a onClick="sort(\'exist\')">'.translate_str_by_key('4526').''.${'exist_img'}.'</a></th>
						<th style="text-align:center;"><a onClick="sort(\'issued\')">'.translate_str_by_key('2754').''.${'issued_img'}.'</a></th>
						<th style="text-align:center;" title="'.translate_str_by_key('2753').'"><a onClick="sort(\'reserved\')">'.translate_str_by_key('1711289765_1_5f735d1486aa51eb9a61df1cd635a0fb').''.${'reserved_img'}.'</a></th>
						<th style="text-align:center;"><a onClick="sort(\'min_limit\')">'.translate_str_by_key('1711289801_1_5f735d1486aa51eb9a61df1cd635a0fb').'</a></th>
						<th style="text-align:center;" title="'.translate_str_by_key('1711289832_1_5f735d1486aa51eb9a61df1cd635a0fb').'"><a onClick="sort(\'min_limit_enable\')"><i class="far fa-bell"></i></a></th>
						<th style="text-align:center;"><a onClick="sort(\'published_flag\')"><i title="'.translate_str_by_key('2747').'" class="fa fa-ban" aria-hidden="true"></i>'.${'published_flag_img'}.'</a></th>
						<th width="130px"><a style="cursor: default;">'.translate_str_by_key('2755').'</a></th>
				</tr>
				</thead>
				'.$html.'
			</table>';
			
			// формируем пагинацию
			$pagination = pagination($total, $kol, 3, $page, 'pagination_active', '');
			if($pagination != '<a class="pagination_active">1</a>'){
				$pagination = '<div class="panel-footer text-center"><div class="pagination">'.$pagination.'</div></div>';
			}else{
				$pagination = '';
			}
			
			$html = '<div class="panel-body">'.$html.'</div>'.$pagination;
		}else{
			$html = '<div class="panel-body">'.translate_str_by_id(2756).'</div>';
		}
		
		exit($html);
		break;
}
exit(json_encode($answer));
?>