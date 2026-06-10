<?php
header('Content-Type: application/json;charset=utf-8;');

ini_set('max_execution_time', '100');
ini_set('memory_limit', '512M');

$time_start = microtime(true);

//--------------------------------------------------------


/*
$f = fopen('log_asynchron.txt', 'a');
fwrite($f, json_encode($_POST)."\n\n\n");
*/

//$_POST = json_decode('', true);


//--------------------------------------------------------

//Конфигурация
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
//Соединение с основной БД
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


// Заполняем информацию по пользователю, ее нужно будет передать в обработчик что бы правильно применились наценки

require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();//ID пользователя
$userProfile = DP_User::getUserProfile();//Профиль пользователя
$group_id = $userProfile["groups"][0];// Группа

//Получаем географический узел покупателя
$geo_id = NULL;
if( isset($_COOKIE["my_city"]) )
{
	$geo_id = $_COOKIE["my_city"];
}
//Куки не были еще выставлены - выводим для самого первого гео-узла, чтобы хоть что-то показать
if($geo_id == NULL)
{
	$min_geo_id_query = $db_link->prepare('SELECT MIN(`id`) AS `id` FROM `shop_geo`;');
	$min_geo_id_query->execute();
	$min_geo_id_record = $min_geo_id_query->fetch();
	$geo_id = $min_geo_id_record["id"];
}





// Получаем параметры запроса
$request_object = json_decode($_POST['request_object'], true);

/*
echo '<pre>';
var_dump($request_object);
exit;
*/

$action = $request_object['action'];// Тип запроса
$article = $request_object['article'];// Артикул
$office_storage_bunches = $request_object['storages'];// Массив с элементами office_storage_bunches





// Если запрос идет от API
if( !empty($request_object['user_id']) ){
	if($request_object['check'] === $DP_Config->tech_key){
		$user_id = (int) $request_object['user_id'];//ID пользователя
		$userProfile = DP_User::getUserProfileById($user_id);//Профиль пользователя
		$group_id = $userProfile["groups"][0];// Группа
		
		$geo_id = $request_object['geo_id'];
	}
}



// Определяем тип запроса

switch($action){
	case 'get_manufacturers':
	
		// Формируем запросы на получение списка производителей
		$postdata_manufacturers = array();
		if(!empty($office_storage_bunches)){
			foreach($office_storage_bunches as $item){
				if($item['protocol_version'] === 2){
					// API - поставщик
					$postdata_manufacturers[] = array('url'=>$DP_Config->domain_path.'content/shop/docpart/ajax_getManufacturersList.php', 'query'=>'geo_id='.$geo_id.'&office_id='.$item['office_id'].'&storage_id='.$item['storage_id'].'&query='.urlencode(json_encode(array('article' => $article))));
				}else if($item['protocol_version'] === 3){
					// Прайс листы
					$postdata_manufacturers[] = array('url'=>$DP_Config->domain_path.'content/shop/docpart/ajax_getManufacturersListFromPrices.php', 'query'=>'office_storage_bunches='.urlencode(json_encode($item['office_storage_bunches'])).'&query='.urlencode(json_encode(array('article' => $article))));
				}else if($item['protocol_version'] === 'server'){
					// Сервер кроссов
					$postdata_manufacturers[] = array('url'=>$DP_Config->domain_path.'content/shop/docpart/ajax_getManufacturersListFromCrossServer.php', 'query'=>'query='.urlencode(json_encode(array('article' => $article))));
				}
			}
		}
		
		// Получаем список брендов
		if(!empty($postdata_manufacturers)){
			
			$result = multi_request($postdata_manufacturers);
			
			//СИНОНИМЫ
			$synonyms = array();
			$synonym_query = $db_link->prepare("SELECT `synonym`, (SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = `shop_docpart_manufacturers_synonyms`.`manufacturer_id`) AS 'name' FROM `shop_docpart_manufacturers_synonyms`;");
			$synonym_query->execute();
			while($synonym_record = $synonym_query->fetch()){
				$synonyms[mb_strtoupper(str_replace('"',"'",$synonym_record["synonym"]), 'UTF-8')] = mb_strtoupper(str_replace('"',"'",$synonym_record["name"]), 'UTF-8');
			}
			
			if(is_array($result))
			{
				foreach($result as &$item)
				{
					if(is_array($item['ProductsManufacturers']))
					{
						foreach($item['ProductsManufacturers'] as &$manufacturer)
						{
							$synonym = null;
							
							if( isset($synonyms[mb_strtoupper(str_replace('"',"'",$manufacturer["manufacturer"]), 'UTF-8')]) )
							{
								$synonym = $synonyms[mb_strtoupper(str_replace('"',"'",$manufacturer["manufacturer"]), 'UTF-8')];
							}
							else
							{
								$synonym = "";
							}
							if(!empty($synonym))
							{
								$manufacturer['manufacturer_show'] = mb_strtoupper($synonym, 'UTF-8');
							}
						}
					}
				}
			}
			
			//echo '<pre>';
			//var_dump($result);
			
		}else{
			$answer = array();
			$answer["result"] = 0;
			$answer["msg"] = translate_str_by_id(4192);
			exit(json_encode($answer));
		}
	break;
	case 'get_articles':
		
		// Формируем запросы на получение списка позиций
		$postdata = array();
		if(!empty($office_storage_bunches)){
			foreach($office_storage_bunches as $item){
				// Если в запросе склада указано несколько производителей то делаем отдельные запросы по каждому
				if(false){
				// Пока это отключаем. При запросах по каждому производителю проценка работает медленнее и присутствуют дубли позиций
				//if(count($item['search_object']['manufacturers']) > 1){
					foreach($item['search_object']['manufacturers'] as $item_manufacturer){
						$search_object_tmp = $item['search_object'];
						$search_object_tmp['manufacturers'] = array($item_manufacturer);
						
						$postdata[] = array('url'=>$DP_Config->domain_path.'content/shop/docpart/ajax_getProductsOfBunch.php', 'query'=>'geo_id='.$geo_id.'&async=1&tech_key='.urlencode($DP_Config->tech_key).'user_id='.$user_id.'&group_id='.$group_id.'&office_id='.$item['office_id'].'&storage_id='.$item['storage_id'].'&query='.urlencode(json_encode( $search_object_tmp )), 'search_object'=>$search_object_tmp);
					}
				}else{
					$postdata[] = array('url'=>$DP_Config->domain_path.'content/shop/docpart/ajax_getProductsOfBunch.php', 'query'=>'geo_id='.$geo_id.'&async=1&tech_key='.urlencode($DP_Config->tech_key).'&user_id='.$user_id.'&group_id='.$group_id.'&office_id='.$item['office_id'].'&storage_id='.$item['storage_id'].'&query='.urlencode(json_encode( $item['search_object'] )), 'search_object'=>$item['search_object']);
				}
			}
		}
		
		/*
		echo '<pre>';
		var_dump($postdata);
		echo '</pre>';
		exit;
		*/
		
		// Получаем список позиций
		if(!empty($postdata)){
			$result = multi_request($postdata);
		}else{
			$answer = array();
			$answer["result"] = 0;
			$answer["msg"] = translate_str_by_id(4192);
			exit(json_encode($answer));
		}
		
	break;
	default:
		$answer = array();
		$answer["result"] = 0;
		$answer["msg"] = translate_str_by_id(4193);
		exit(json_encode($answer));
	break;
}





/*
echo '<pre>';
var_dump($result);
exit;*/






// Функция запроса
function multi_request($postdata) {
	
	global $DP_Config;
	
	$curly = array();
	$result = array();
	$mh = curl_multi_init();

	foreach ($postdata as $id => $postdata_item) {
		
		$curly[$id] = curl_init();
		curl_setopt($curly[$id], CURLOPT_URL, $postdata_item['url']);
		curl_setopt($curly[$id], CURLOPT_HEADER, 0);
		curl_setopt($curly[$id], CURLOPT_RETURNTRANSFER, 1);
		curl_setopt($curly[$id], CURLOPT_CONNECTTIMEOUT, (int) $DP_Config->max_time_answer_storage); 
		curl_setopt($curly[$id], CURLOPT_TIMEOUT, (int) $DP_Config->max_time_answer_storage);
		curl_setopt($curly[$id], CURLOPT_FOLLOWLOCATION, true);
		curl_setopt($curly[$id], CURLOPT_SSL_VERIFYPEER, 0);
		curl_setopt($curly[$id], CURLOPT_SSL_VERIFYHOST, 0);

		curl_setopt($curly[$id], CURLOPT_POST, true);
		curl_setopt($curly[$id], CURLOPT_POSTFIELDS, $postdata_item['query']);

		curl_multi_add_handle($mh, $curly[$id]);
		
	}

	$running = null;
	
	do {
		
		curl_multi_exec($mh, $running);
		usleep(100);
		
	} while($running > 0);

	foreach($curly as $id => $c) {
		
		$current_result = json_decode(curl_multi_getcontent($c), true );
		
		//echo '<pre>';
		//var_dump(curl_multi_getcontent($c));
		
		//echo '<pre>';
		//var_dump($current_result);
		
		if(!empty($current_result)){
			$result[$id] = $current_result;
		}else{
			$errmsg  = curl_error($curly[$id]);
			$result[$id] = array('result' => 0, 'message' => $errmsg, 'Products' => array(), 'ProductsManufacturers' => array());
		}
		
		curl_multi_remove_handle($mh, $c);
		
	}
	
	
	curl_multi_close($mh);
	return $result;
	
}

$time_end = microtime(true);

///////////////////////////////////////////////////////

//Пишем статистку запросов
if($request_object['action'] === 'get_articles'){
	//Артикул
	$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/", "", $request_object['article']), "UTF-8");
	$manufacturer = '';
	$name = '';
	
	//Производитель и  Наименование
	if(is_array($request_object['storages'])){
		foreach($request_object['storages'] as $storage_object){
			if(is_array($storage_object['search_object']['manufacturers'])){
				foreach($storage_object['search_object']['manufacturers'] as $manufacturer_object){
					if(!empty($manufacturer_object['manufacturer_show'])){
						$manufacturer = htmlentities(mb_strtoupper(trim($manufacturer_object['manufacturer_show']), "UTF-8"), ENT_QUOTES, "UTF-8");
						$name = htmlentities(str_replace(array("\"", "\\", "'", "\n", "\r", "\t", "#", ">", "<"), "", $manufacturer_object['name']), ENT_QUOTES, "UTF-8");
						break 2;
					}
				}
			}
		}
	}

	//Если пользователь авторизован удаляем все записи запросов данного артикула пользователем за последние 24 часа, что бы не было дублей, для более корректной статистики
	if($user_id > 0){
		$where = '';
		$where = '`user_id` = ? AND `time` > ? AND `article` = ? AND `manufacturer` = ?';
		$binding_values = array();
		array_push($binding_values, $user_id);
		array_push($binding_values, (time() - 86400));
		array_push($binding_values, $article);
		array_push($binding_values, $manufacturer);
		
		$sql = "DELETE FROM `shop_stat_article_queries` WHERE ".$where;
		$query = $db_link->prepare($sql);
		$query->execute($binding_values);
	}
	
	//Пишем в таблицу статистики запрос данного артикула с текущей датой
	$where = '';
	$binding_values = array();
	array_push($binding_values, $article);
	array_push($binding_values, $manufacturer);
	array_push($binding_values, $name);
	array_push($binding_values, '');
	array_push($binding_values, $_SERVER['REMOTE_ADDR']);
	array_push($binding_values, $user_id);
	array_push($binding_values, time());
	
	$sql = "INSERT INTO `shop_stat_article_queries`(`id`, `article`, `manufacturer`, `name`, `search_string`, `ip`, `user_id`, `time`) VALUES (NULL,?,?,?,?,?,?,?)";
	$query = $db_link->prepare($sql);
	$query->execute($binding_values);
	
	//Если пользователь не авторизован, запишем в браузер id записи из таблицы статистики, что бы при авторизации привязать запись к user_id
	if($user_id <= 0){
		$shop_stat = array();
		$id = $db_link->lastInsertId();
		if($id > 0){
			$shop_stat[] = $id;
		}
		if( isset($_COOKIE["shop_stat"]) )
		{
			$shop_stat_COOKIE = explode('_', $_COOKIE["shop_stat"]);
			for($i=0, $cnt=count($shop_stat_COOKIE); $i<$cnt; $i++){
				if($i >= 19){
					break;//Для не авторизованного пользователя храним только 20 последних запросов
				}
				$shop_stat[] = (int) $shop_stat_COOKIE[$i];
			}
		}
		setcookie('shop_stat', implode('_',$shop_stat), time()+2592000, '/');
	}
}

///////////////////////////////////////////////////////

// Отдаем результат
$answer = array();
$answer["result"] = 1;
$answer["data"] = $result;
$answer["time"] = number_format(($time_end - $time_start), 3, '.', '');
exit(json_encode($answer));
?>