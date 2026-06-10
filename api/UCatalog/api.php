<?php
/*
API UCatalog
*/

// Проверка робота
if((int)$_COOKIE["UCatalog"] !== 1){
	exit('Forbidden 403');
}

// Константа доступа
define("_UCatalog_", "1");


$host = 'https://api.ucats.ru/';
$login = '';
$password = '';
$domain = '';


$UCatalog_active = true;// Отображать каталог

$type_show_cars = true;// Отображать легковые
$type_show_trucks = true;// Отображать грузовые
$type_show_moto = true;// Отображать мотоциклы

$popular_cars = array();// Популярные легковые
$popular_trucks = array();// Популярные грузовые
$popular_moto = array();// Популярные мотоциклы

$filter_cars = array();// Скрытые марки легковые
$filter_trucks = array();// Скрытые марки грузовые
$filter_moto = array();// Скрытые марки мотоциклы


// Конфигурация сайта
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
if(!empty($DP_Config)){
	if(isset($DP_Config->api_ucats_url)){
		$host = trim($DP_Config->api_ucats_url);
	}
	
	$login = trim($DP_Config->ucats_login);
	$password = trim($DP_Config->ucats_password);
	$domain = trim($DP_Config->domain_path);
	
	// Отображать каталог
	if(isset($DP_Config->UCatalog_active)){
		$UCatalog_active = (int) $DP_Config->UCatalog_active;
	}
	
	// Отображаемые разделы
	if(isset($DP_Config->type_show_cars)){
		$type_show_cars = (int) $DP_Config->type_show_cars;
	}
	if(isset($DP_Config->type_show_trucks)){
		$type_show_trucks = (int) $DP_Config->type_show_trucks;
	}
	if(isset($DP_Config->type_show_moto)){
		$type_show_moto = (int) $DP_Config->type_show_moto;
	}
	
	// Популярные марки
	if(isset($DP_Config->popular_cars)){
		$popular_cars = explode(',', $DP_Config->popular_cars);
		foreach($popular_cars as $k => $v){
			$popular_cars[$k] = trim(mb_strtoupper($v, "UTF-8"));
		}
	}
	if(isset($DP_Config->popular_trucks)){
		$popular_trucks = explode(',', $DP_Config->popular_trucks);
		foreach($popular_trucks as $k => $v){
			$popular_trucks[$k] = trim(mb_strtoupper($v, "UTF-8"));
		}
	}
	if(isset($DP_Config->popular_moto)){
		$popular_moto = explode(',', $DP_Config->popular_moto);
		foreach($popular_moto as $k => $v){
			$popular_moto[$k] = trim(mb_strtoupper($v, "UTF-8"));
		}
	}
	
	// Скрытые марки
	if(isset($DP_Config->filter_cars)){
		$filter_cars = explode(',', $DP_Config->filter_cars);
		foreach($filter_cars as $k => $v){
			$filter_cars[$k] = trim(mb_strtoupper($v, "UTF-8"));
		}
	}
	if(isset($DP_Config->filter_trucks)){
		$filter_trucks = explode(',', $DP_Config->filter_trucks);
		foreach($filter_trucks as $k => $v){
			$filter_trucks[$k] = trim(mb_strtoupper($v, "UTF-8"));
		}
	}
	if(isset($DP_Config->filter_moto)){
		$filter_moto = explode(',', $DP_Config->filter_moto);
		foreach($filter_moto as $k => $v){
			$filter_moto[$k] = trim(mb_strtoupper($v, "UTF-8"));
		}
	}
}


// Проверка доступа
if(strpos($_SERVER['HTTP_REFERER'], $domain) !== 0){
	exit('Forbidden 403');
}


// Формируем массив ответа
$answer = array();
$answer["status"] = false;// Статус выполнения запроса
$answer["message"] = '';// Сообщение об ошибке
$answer["html"] = '';// HTML представление страницы
$answer["tag"] = '';// ID HTML-тега в который будет вставлен код HTML 
$answer["key"] = '';// ID товара для которого выполняется запрос информации
$answer["json"] = '';// JSON данные с информацией о товаре
$answer["breadcrumbs"] = '';// Хлебные крошки


// Запрос к API
function u_curl($postdata, $sub_url='cars_parts'){
	global $host, $login, $password;
	
	$postdata['login'] = $login;
	$postdata['password'] = $password;
	$postdata = http_build_query($postdata);

	$curl = curl_init();
	curl_setopt($curl, CURLOPT_URL, $host.$sub_url."/");
	curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
	curl_setopt($curl, CURLOPT_POST, true);
	curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 10); 
	curl_setopt($curl, CURLOPT_TIMEOUT, 10);
	curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
	curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
	$curl_result = curl_exec($curl);
	curl_close($curl);
	
	return $curl_result;
}


// Получаем параметры запроса
if($UCatalog_active && !empty($_POST['request_object'])){
	$request_object = json_decode($_POST['request_object'], true);
	
	if($request_object['action'] == 'get_garage'){
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
		
		//Для работы с пользователем
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
		$user_id = DP_User::getUserId();

		$query = $db_link->prepare("SELECT `UCatalog_json` FROM `shop_docpart_garage` WHERE `id` = ?;");
		$query->execute(array($request_object['id']));
		$record = $query->fetch();
		if(!empty($record['UCatalog_json'])){
			$request_object = json_decode($record['UCatalog_json'], true);
		}
	}
	
	//Нужно для мультиязычности
	if( !isset($db_link) )
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
	
	// -------------------------------------------------------------------------------
	//Подключение мультиязычности
	require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
	$multilang_params = multilang_init();
	// -------------------------------------------------------------------------------
	
	
	// Подключаем обработчик метода
	$request_object['action'] = str_replace(array('.','\\','/'), '', $request_object['action']);
	if(file_exists($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/".$request_object['action'].".php")){
		$data_array = array();// Основной массив данных полученный от API
		require_once($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/".$request_object['action'].".php");
	}
}


// Breadcrumbs
if( !isset($request_object) || $request_object == null )
{
	$request_object = array('action'=>null);
}

if($request_object['action'] != 'get_info' && $request_object['action'] != 'get_info_html' && $request_object['action'] != 'add_garage' && $request_object['action'] != 'get_notepad' && $request_object['action'] != 'add_notepad'){
	$breadcrumbs = array();
	if($request_object['action'] != 'get_types'){
		if( isset($request_object['action']) && $request_object['action'] == 'get_marks'){
			$request_object['action'] = 'get_types';
		}
		
		if( !isset($request_object['breadcrumbs']) || !is_array($request_object['breadcrumbs']) )
		{
			$request_object['breadcrumbs'] = array();
		}
		foreach($request_object['breadcrumbs'] as $k => $v){
			if($v['action'] != $request_object['action']){
				$breadcrumbs[] = $v;
			}else{
				break;
			}
		}
		
		if(empty($request_object['parent_id'])){
			if( isset($request_object['breadcrumbs']) ) { unset($request_object['breadcrumbs']); }
			if( !isset($request_object) || $request_object == null ) { $request_object = array(); }
			$breadcrumbs[] = $request_object;
			if($request_object['action'] != 'get_types'){
				$breadcrumbs_html = '';
				foreach($breadcrumbs as $item){
					if($breadcrumbs_html != ''){
						$breadcrumbs_html .= '<i style="margin: 0px 10px;" class="fa fa-angle-double-right" aria-hidden="true"></i>';
					}
					if( !isset($item) || $item == null )
					{
						$item = array();
					}
					$breadcrumbs_html .= '<div onClick="UCatalog_loading(\''.$item['action'].'\', \''.$item['caption'].'\', \''.$item['type'].'\', \''.$item['mark_id'].'\', \''.$item['model_id'].'\', \''.$item['modification_id'].'\', \'0\', \'\', \'\');">'.$item['caption'].'</div>';
				}
				if($breadcrumbs_html != ''){
					if( ( isset($request_object['action']) && $request_object['action'] == 'get_tree') && ((int)$_COOKIE["u_id"] > 0) ){
						$breadcrumbs_html .= '<span id="UCatalog_breadcrumbs_right_btn"><span onClick="UCatalog_add_garage();" class="btn_primary"><i class="fa fa-car" aria-hidden="true"></i> Добавить в гараж</span></span>';
					}
					$breadcrumbs_html = '<div class="row"><div class="col-md-12"><div class="UCatalog_breadcrumbs">'.$breadcrumbs_html.'</div></div></div>';
					$answer["html"] = $breadcrumbs_html . $answer["html"];
				}
			}
		}
	}
	$answer["breadcrumbs"] = $breadcrumbs;
	$request_object["breadcrumbs"] = $breadcrumbs;
}


$answer['request_object'] = $request_object;
exit(json_encode($answer));
?>