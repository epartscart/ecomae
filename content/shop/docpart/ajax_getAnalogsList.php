<?php
/**
 * Серверный скрипт для получения списка аналогов по артикулу или артикулу-производителю
*/
header('Content-Type: application/json;charset=utf-8;');
ini_set("memory_limit", "512M");
set_time_limit(40);

// Функция очистки артикула
function prepareString($string)
{
	$string = str_replace(array(" ", "-", "_", "`", "/", "'", '"', "\\", ".", ",", "#", "\r\n", "\r", "\n", "\t"), '',$string);
	$string = trim($string);
	return $string;
}

// Функция запроса
function multi_request($postdata){
	
	$curly = array();
	$result = array();
	$mh = curl_multi_init();

	foreach($postdata as $id => $postdata_item){
		$curly[$id] = curl_init();
		
		curl_setopt($curly[$id], CURLOPT_URL, $postdata_item['url']);
		curl_setopt($curly[$id], CURLOPT_HEADER, 0);
		curl_setopt($curly[$id], CURLOPT_RETURNTRANSFER, 1);
		curl_setopt($curly[$id], CURLOPT_CONNECTTIMEOUT, 10);
		curl_setopt($curly[$id], CURLOPT_TIMEOUT, 10);
		curl_setopt($curly[$id], CURLOPT_FOLLOWLOCATION, true);
		curl_setopt($curly[$id], CURLOPT_SSL_VERIFYPEER, 0);
		curl_setopt($curly[$id], CURLOPT_SSL_VERIFYHOST, 0);
		
		if(!empty($postdata_item['query'])){
			curl_setopt($curly[$id], CURLOPT_POST, true);
			curl_setopt($curly[$id], CURLOPT_POSTFIELDS, $postdata_item['query']);
		}
		
		curl_multi_add_handle($mh, $curly[$id]);
	}

	$running = null;
	
	do {
		curl_multi_exec($mh, $running);
		usleep(100);
	} while($running > 0);

	foreach($curly as $id => $c){
		$current_result = json_decode(curl_multi_getcontent($c), true );
		if(!empty($current_result)){
			$result[$id] = $current_result;
		}
		curl_multi_remove_handle($mh, $c);
	}
	
	curl_multi_close($mh);
	return $result;
}



$time_start = microtime(true);// Начальное время выполнения скрипта

//Конфигурация Treelax
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS



// Данные запроса
$search_object = json_decode($_POST["search_object"], true);
$article = mb_strtoupper(prepareString($search_object["article"]), 'UTF-8');// Артикул
$manufacturers  = $search_object["manufacturers"];// Список всех производителей с учетом синонимов

if(empty($article)){
	$answer = array();
	$answer["result"] = 0;
	$answer["check"] = 1;
	$answer["analogs"] = array();
	exit(json_encode($answer));
}

$analogs = array();//Список аналогов
$postdata = array();// Массив запросов для асинхронного опроса
$hashes = array();// Массив хешей найденных кроссов для фильтрации повторяющихся


// <------------------------------------------------------------------------------------------------------


// Запрос кроссов от поставщика api
if(0)
{
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
	
	
	$storage_id_array = array(19);// ID складов через запятую
	foreach($storage_id_array as $storage_id){
		if($storage_id > 0)
		{
			// 1. Получаем данные склада: настройки подключения и имя каталога, в котором находится скрипт-обработчик
			$storage_query = $db_link->prepare('SELECT
				`shop_storages`.`connection_options` AS `connection_options`,
				`shop_storages_interfaces_types`.`handler_folder` AS `handler_folder`
				FROM
				`shop_storages`
				INNER JOIN `shop_storages_interfaces_types` ON `shop_storages`.`interface_type` = `shop_storages_interfaces_types`.`id`
				WHERE
				`shop_storages`.`id` = ?;');
			$storage_query->execute( array($storage_id) );
			$storage_record = $storage_query->fetch();
			
			if(!empty($storage_record)){
				$handler_folder = $storage_record["handler_folder"];
				$storage_options = json_decode($storage_record["connection_options"], true);//Настройки для обработчика поставщика
				$storage_options["markups"] = array(0);
				$storage_options["office_id"] = 0;
				$storage_options["storage_id"] = $storage_id;
				$storage_options["additional_time"] = 0;
				$storage_options["office_caption"] = "";
				$storage_options["storage_caption"] = "";

				// 2. Получаем список производителей для данного артикула и данного склада
				$manufacturers_in_storage = array();
				if(is_array($manufacturers) && !empty($manufacturers)){
					foreach($manufacturers as $manufacturer){
						// Проверяем что текущий производитель пришел от нужного склада
						if($manufacturer['storage_id'] != $storage_id){
							continue;
						}
						$manufacturers_in_storage[] = $manufacturer;
					}
				}

				// 3. Формируем запрос к поставщику
				// Определяем версию протокола
				$protocol_script = "common_interface.php";// Версия 1
				if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/suppliers_handlers/".$handler_folder."/get_supplies.php") )
				{
					$protocol_script = "get_supplies.php";// Версия 2
				}
				$postdata[] = array('url'	=>	$DP_Config->domain_path."content/shop/docpart/suppliers_handlers/".$handler_folder."/".$protocol_script, 
									'query'	=>	http_build_query(
													array(
														'article' => $article,// Артикул
														'manufacturers' => json_encode($manufacturers_in_storage),// Массив производителей для API-поставщика
														'storage_options' => json_encode($storage_options)// Настройки подключения
													)
												)
				);
			}
		}
	}
}


// <------------------------------------------------------------------------------------------------------


// Запрос кроссов с ucats
if($DP_Config->ucats_crosses)
{
	if(is_array($manufacturers) && !empty($manufacturers)){
		foreach($manufacturers as $manufacturer){
			
			if( !isset($manufacturer['params']) || $manufacturer['params'] == null )
			{
				$manufacturer['params'] = array();
				$manufacturer['params']['type'] = null;
			}
			
			// Проверяем что текущий производитель пришел от сервера кроссов а не от поставщика api
			if($manufacturer['params']['type'] !== 'server'){
				continue;
			}
			$manufacturer = strtoupper($manufacturer['manufacturer']);// Наименование как на сервере
			$postdata[] = array('url'=>"http://ucats.ru/ucats/crosses/get_crosses_by_article_and_brand.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&article=".urlencode($article)."&manufacturer=".urlencode($manufacturer));
		}
	}
}


// <------------------------------------------------------------------------------------------------------


// Если есть данные для запроса кроссов с сервера или от поставщика
if(!empty($postdata)){
	$result = multi_request($postdata);// Асинхронный запрос
	
	if(is_array($result)){
		foreach($result as $curl_result){
			
			// Данные от сервера
			if( ! empty($curl_result["crosses"]) )
			{
				$crosses = $curl_result["crosses"];
				if(is_array($crosses) && !empty($crosses)) {
					foreach($crosses as $cross) {
						
						$article_analog = mb_strtoupper(prepareString($cross["article"]), 'UTF-8');
						$manufacturer_analog = mb_strtoupper(trim($cross["manufacturer"]), 'UTF-8');
						
						$hash = md5($article_analog . $manufacturer_analog);
						//Проверяем его наличие в массиве
						if ( ! isset($hashes[$hash]) ) {
							$hashes[$hash] = true;
							array_push($analogs, array("article"=>$article_analog, "manufacturer"=>$manufacturer_analog, "type"=>"server"));
						}
					}
				}
			}
			
			// Данные от API поставщика
			if( ! empty($curl_result["Products"]) )
			{
				$Products = $curl_result["Products"];
				
				//Формируем список аналогов
				for($i=0; $i < count($Products); $i++) {
					
					$article_analog = mb_strtoupper(prepareString($Products[$i]["article"]), 'UTF-8');
					$manufacturer_analog = mb_strtoupper(trim($Products[$i]["manufacturer"]), 'UTF-8');;
					
					//Если артикул равен запрашиваемому - пропускаем
					if( $article_analog == $article ) {
						continue;
					}

					$hash = md5($article_analog . $manufacturer_analog);
					//Проверяем его наличие в массиве
					if ( ! isset($hashes[$hash]) ) {
						$hashes[$hash] = true;
						array_push($analogs, array("article"=>$article_analog, "manufacturer"=>$manufacturer_analog, "type"=>"storage id ". $Products[$i]["storage_id"]));
					}
				}
			}
			
		}
	}
	unset($result);// Освобождаем память
}


// <------------------------------------------------------------------------------------------------------


// Запрос кроссов из таблицы сайта
if($DP_Config->local_crosses){
	if(empty($db_link))
	{
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
	}

	$analogs_tmp = array();
	$hashes_tmp = array();
	
	function get_analogs($article, $manufacturer, $level)
	{
		global $DP_Config, $db_link, $analogs_tmp, $hashes_tmp;
		
		$level = $level + 1;
		
		if($manufacturer == '')
		{
			$analogs_query = $db_link->prepare('SELECT * FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ?');
			$analogs_query->execute( array($article, $article) );
			while( $analog_record = $analogs_query->fetch() )
			{
				if($article === $analog_record['article'])
				{
					$manufacturer = $analog_record["manufacturer_article"];
					break;
				}
				else if($article === $analog_record['analog']){
					$manufacturer = $analog_record["manufacturer_analog"];
					break;
				}
			}
		}

		if(!empty($manufacturer))
		{
			$analogs_query = $db_link->prepare('SELECT * FROM `shop_docpart_articles_analogs_list` WHERE (`article` = ? AND `manufacturer_article` = ?) OR (`analog` = ? AND `manufacturer_analog` = ?)');
			$analogs_query->execute( array($article, $manufacturer, $article, $manufacturer) );
		}
		else
		{
			$analogs_query = $db_link->prepare('SELECT * FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ?;');
			$analogs_query->execute( array($article, $article) );
		}

		while( $analog_record = $analogs_query->fetch() )
		{
			$to_list = prepareString($analog_record["article"]);
			$to_list_manufacturer = trim($analog_record["manufacturer_article"]);
			if($to_list === $article)
			{
				$to_list = prepareString($analog_record["analog"]);
				$to_list_manufacturer = trim($analog_record["manufacturer_analog"]);
			}

			// Если артикула нет в аналогах то сохраним его там и найдем его аналоги, сделаем рекурсию
			$hash = md5($to_list . $to_list_manufacturer);
			
			if ( ! isset($hashes_tmp[$hash]) ) {
				$hashes_tmp[$hash] = true;
				array_push($analogs_tmp, array("article"=>$to_list, "manufacturer"=>$to_list_manufacturer));
				
				if($level <= 2){
					get_analogs($to_list, $to_list_manufacturer, $level);
				}
			}
		}
	}


	if(is_array($manufacturers) && !empty($manufacturers))
	{
		foreach($manufacturers as $manufacturer)
		{
			$manufacturer = $manufacturer['manufacturer'];// Наименование как на сервере
			get_analogs($article, $manufacturer, 0);
		}
	}
	else
	{
		get_analogs($article, '', 0);
	}
	
	unset($hashes_tmp);// Освобождаем память
	
	if(!empty($analogs_tmp)){
		foreach($analogs_tmp as $analog){
			$hash = md5($analog["article"] . $analog["manufacturer"]);
			if ( ! isset($hashes[$hash]) ) {
				$hashes[$hash] = true;
				array_push($analogs, array("article"=>$analog["article"], "manufacturer"=>$analog["manufacturer"], "type"=>"table"));
			}
		}
	}
}





// Ответ со списком аналогов
$time_end = microtime(true);// Время завершения работы скрипта
$answer = array();
$answer["result"] = 1;
$answer["check"] = 1;
$answer["analogs"] = $analogs;
$answer["time"] = number_format(($time_end - $time_start), 3, '.', '');// Время работы скрипта
exit(json_encode($answer));
?>