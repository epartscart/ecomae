<?php
//Серверный скрипт для выставления поля "Отмечен для заказа" в корзине
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
    $result = array();
	$result["status"] = false;
	$result["message"] = "No DB connect";
	$result["code"] = "no_db_connect";
	exit(json_encode($result));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



$request_object = json_decode($_POST["request_object"], true);
$records = $request_object["records"];



//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//В зависимости от инициатора (пользователь или API)
if( !isset($request_object["tech_key"]) )
{
	//Инициатор - пользователь - со страницы корзины
	
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	$user_id = DP_User::getUserId();
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
}
else//Инициатор - API (пользователь точно авторизован)
{
	if( $request_object["tech_key"] != $DP_Config->tech_key )
	{
		$result = array();
		$result["status"] = false;
		$result["message"] = translate_str_by_id(4465);
		$result["code"] = "forbidden";
		exit(json_encode($result));
	}
	
	//Данные о пользователе - передаются прямо в объекте
	$user_id = $request_object["user_id"];
	$session_id = 0;//Записи корзины для авторизованого пользователя имеют session_id = 0
}





//Удостоверяемся, что запрос идет именно от владельца корзины
for($i=0; $i < count($records); $i++)
{
	$check_user_cart_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_carts` WHERE `id` = ? AND `session_id` = ? AND `user_id` = ?;");
	$check_user_cart_query->execute( array($records[$i], $session_id, $user_id) );
	if($check_user_cart_query->fetchColumn() != 1)
	{
		$result = array();
		$result["status"] = false;
		$result["message"] = translate_str_by_id(4466);
		$result["code"] = "cart_item_not_found";
		exit(json_encode($result));
	}
}








//Массив для ответа
$answer_records = array();

for($i=0; $i < count($records); $i++)
{
	$record_info_query = $db_link->prepare('SELECT `checked_for_order` FROM `shop_carts` WHERE `id` = ?;');
	$record_info_query->execute( array($records[$i]) );
	$record_info_record = $record_info_query->fetch();
	
	if($record_info_record["checked_for_order"] == 1)
	{
		$checked = 0;
	}
	else
	{
		$checked = 1;
	}
	
	$db_link->prepare('UPDATE `shop_carts` SET `checked_for_order` = ? WHERE `id` = ?;')->execute( array($checked, $records[$i]) );
	
	array_push($answer_records, array("cart_record_id" => (int)$records[$i], "checked_for_order"=>$checked));
}

$result = array();
$result["status"] = true;
$result["message"] = "Ok";
$result["code"] = "ok";
$result["records"] = $answer_records;
exit(json_encode($result));
?>