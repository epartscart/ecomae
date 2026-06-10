<?php
/**
 * Серверный скрипт для удаления записи из корзины
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
$user_id = DP_User::getUserId();
$is_admin = DP_User::isAdmin();


// ---------------------------------------------------------------------------------------------------
//ОБЪЕКТ ЗАПРОСА
$request_object = json_decode($_POST["request_object"], true);
$records_to_del = $request_object["records_to_del"];
//$cart_record_id = $request_object["id"];

//Если вызов был от скрипта
$tech_key = "";
if( !empty($request_object["tech_key"]) )
{
	$tech_key = $request_object["tech_key"];
	
	if($tech_key != $DP_Config->tech_key)
	{
		$result = array();
		$result["status"] = false;
		$result["code"] = "forbidden";
		$result["message"] = "Forbidden";
		exit(json_encode($result));
	}
}
else
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
}

// ---------------------------------------------------------------------------------------------------
//ПРОВЕРКА ПРАВ НА ЗАПУСК СКРИПТА
//Если пользователь не является администратором или если вызов не от API:
if( ! $is_admin && $tech_key == "")
{
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
	
	
	for($i=0; $i < count($records_to_del); $i++)
	{
		$check_cart_record_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_carts` WHERE `user_id` = ? AND `id` = ? AND `session_id` = ?;');
		$check_cart_record_query->execute( array($user_id, $records_to_del[$i], $session_id) );
		if( $check_cart_record_query->fetchColumn() == 0 )
		{
			//Чужая корзина
			$result = array();
			$result["status"] = false;
			$result["code"] = "alien_cart";
			$result["message"] = "Alien cart";
			exit(json_encode($result));
		}
	}
}
// ---------------------------------------------------------------------------------------------------

//АЛГОРИТМ УДАЛЕНИЯ ЗАПИСЕЙ
$no_error = true;//Накопительный результат выполнения функций удаления записей. Т.е. если хотя бы одну запись корзины удалить не удастся, то $no_error будет установлен в false
for($i=0; $i < count($records_to_del); $i++)
{
	//ЛОГИКА ЗАВИСИТ ОТ ТИПА ПРОДУКТА. ПОЭТОМУ СНАЧАЛА ПОЛУЧАЕМ ТИП ПРОДУКТА
	$product_type_query = $db_link->prepare('SELECT `product_type` FROM `shop_carts` WHERE `id` = ?;');
	$product_type_query->execute( array($records_to_del[$i]) );
	$product_type_record = $product_type_query->fetch();
	$product_type = $product_type_record["product_type"];

	switch($product_type)
	{
		case 1:
			if(deleteRecordType1($records_to_del[$i]) != true)
			{
				$no_error = false;
			}
			break;
		case 2:
			if(deleteRecordType2($records_to_del[$i]) != true)
			{
				$no_error = false;
			}
			break;
	};
}

//Возвращаем результат инициатору
$result = array();
if($no_error)
{
	$result["status"] = true;
	$result["code"] = "ok";
	$result["message"] = "Ok";
	$result["records_to_del"] = $records_to_del;//В виде массива - также указываем список удаленных записей
}
else
{
	$result["code"] = "sql_error";
	$result["message"] = "SQL Error";
	$result["status"] = false;
}
echo json_encode($result);









// **********************************************************************************************************************************************
// ***********************************************************************************************************************************************
// ************************************************     Опредениея функций для каждого типа продукта     ******************************************
// ***********************************************************************************************************************************************
// **********************************************************************************************************************************************

// ---------------------------------------------------------------------------------------------------------------
//Удаление записи из корзины для типа 1
function deleteRecordType1($cart_record_id)
{
    global $DP_Config;
    global $db_link;
    
    //1. Получаем все детальные записи корзины по данному товару
    //2. Снимаем с резервирования все товары на складах
    //3. Удаляем детальные записи корзины
    //4. Удаляем запись корзины
    
    //1.
	$details_query = $db_link->prepare('SELECT * FROM `shop_carts_details` WHERE `cart_record_id` = ?;');
	$details_query->execute( array($cart_record_id) );
    while( $detail = $details_query->fetch() )
    {
        $detail_id = $detail["id"];
        $office_id = $detail["office_id"];
        $storage_id = $detail["storage_id"];
        $storage_record_id = $detail["storage_record_id"];
        $count_reserved = $detail["count_reserved"];
        
        
        //2.
    	//Освобождаем товар на складе
		$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`+?, `reserved`= `reserved`-? WHERE `id` = ?;')->execute( array($count_reserved, $count_reserved, $storage_record_id) );
		
    	//3.
		$db_link->prepare('DELETE FROM `shop_carts_details` WHERE `id` = ?;')->execute( array($detail_id) );
    }//~while() - по детальным записям корзины
    
    //4.
    //Запрос - единый для зарегистрированных и не зарегистрированных
    if( $db_link->prepare('DELETE FROM `shop_carts` WHERE `id` = ?;')->execute( array($cart_record_id) ) == true)
    {
        return true;
    }
    else
    {
        return false;
    }
}//~function deleteRecordType1()
// ---------------------------------------------------------------------------------------------------------------
//Удаление записи из корзины для типа 2
function deleteRecordType2($cart_record_id)
{
    global $DP_Config;
    global $db_link;
    
    //1. Запрос - единый для зарегистрированных и не зарегистрированных
    if( $db_link->prepare('DELETE FROM `shop_carts` WHERE `id` = ?;')->execute( array($cart_record_id) ) == true)
    {
        return true;
    }
    else
    {
        return false;
    }
}//~function deleteRecordType2()
// ---------------------------------------------------------------------------------------------------------------
?>