<?php
/**
 * Серверный скрипт для изменения количества требуемого количества товара в КОРЗИНЕ
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
    $result = array();
	$result["status"] = false;
	$result["message"] = "No DB Connect";
	$result["code"] = "no_db_connect";
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



//Объект запроса
$request_object = json_decode($_POST["request_object"], true);
$count_need = (int)$request_object["count_need"];
$cart_record_id = (int)$request_object["id"];


//В зависимости от инициатора (пользователь или API)
if( !isset($request_object["tech_key"]) )
{
	//Инициатор - пользователь - со страницы корзины
	
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	$user_id = DP_User::getUserId();
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
	
	$userProfile = DP_User::getUserProfile();
	$group_id = $userProfile["groups"][0];//Берем первую группу пользователя
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
	$userProfile = DP_User::getUserProfileById($user_id);
	$group_id = $userProfile["groups"][0];//Берем первую группу пользователя
}



//Для получения списка точек выдачи для этого покупателя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");


//Удостоверяемся, что запрос идет именно от владельца корзины
$check_user_cart_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_carts` WHERE `id` = ? AND `session_id` = ? AND `user_id` = ?;");
$check_user_cart_query->execute( array($cart_record_id, $session_id, $user_id) );
if($check_user_cart_query->fetchColumn() != 1)
{
	$result = array();
	$result["status"] = false;
	$result["message"] = translate_str_by_id(4466);
	$result["code"] = "cart_item_not_found";
	exit(json_encode($result));
}






//ЛОГИКА ЗАВИСИТ ОТ ТИПА ПРОДУКТА. ПОЭТОМУ СНАЧАЛА ПОЛУЧАЕМ ТИП ПРОДУКТА
$product_type_query = $db_link->prepare('SELECT `product_type` FROM `shop_carts` WHERE `id` = ?;');
$product_type_query->execute( array($cart_record_id) );
$product_type_record = $product_type_query->fetch();

$product_type = $product_type_record["product_type"];

switch($product_type)
{
    case 1:
        changeCountType1();
        break;
    case 2:
        changeCountType2();
        break;
};






// **********************************************************************************************************************************************
// ***********************************************************************************************************************************************
// ************************************************     Опредениея функций для каждого типа продукта     ******************************************
// ***********************************************************************************************************************************************
// **********************************************************************************************************************************************

// ------------------------------------------------------------------------------------------------------------------------------------------
//Функция для изменения количества товара для типа 1
function changeCountType1()
{
    global $db_link;
    global $DP_Config;
    global $cart_record_id;
    global $count_need;
    global $customer_offices;
	global $group_id;
    
    //Информация из записи корзины
	$cart_record_query = $db_link->prepare('SELECT `count_need`, `product_id`, `price` FROM `shop_carts` WHERE `id` = ?;');
	$cart_record_query->execute( array($cart_record_id) );
    $cart_record = $cart_record_query->fetch();
    $current_count_need = $cart_record["count_need"];//Текущее количество
    $price =  $cart_record["price"];//Цена на товар
    $product_id = $cart_record["product_id"];//Каталожный ID продукта
    $time = time();//Текущее время
    
    
    if($current_count_need == $count_need)
    {
		$result = array();
		$result["status"] = false;
		$result["message"] = translate_str_by_id(4467);
		$result["code"] = "the_same_count";
		exit(json_encode($result));
		
        //exit("The same count...");
    }
    else if($count_need > $current_count_need)//Прибавление количества
    {
        $to_plus_total_need = $count_need - $current_count_need;//Требуется добавить всего
        $to_plus_left = $to_plus_total_need;//Сколько осталось добавить
        
        
        //1. РЕЗЕРВИРУЕМ ТОВАР ИЗ ТЕХ ЖЕ ПОСТАВОК, ИЗ КОТОРЫХ УЖЕ ЧАСТЬ ЗАРЕЗЕРВИРОВАНА
        //Получаем детализацию записи
		$details_query = $db_link->prepare('SELECT * FROM `shop_carts_details` WHERE `cart_record_id` = ?;');
		$details_query->execute( array($cart_record_id) );
        while($detail = $details_query->fetch() )
        {
            $detail_id = $detail["id"];
            $office_id = $detail["office_id"];
            $storage_id = $detail["storage_id"];
            $storage_record_id = $detail["storage_record_id"];
            
            //Запрашиваем доступное количество на складе с данной поставки
			$storage_product_exist_query = $db_link->prepare('SELECT `exist` FROM `shop_storages_data` WHERE `id` = ?;');
			$storage_product_exist_query->execute( array($storage_record_id) );
			$storage_product_exist_record = $storage_product_exist_query->fetch();
			$exist = $storage_product_exist_record["exist"];
		
			if($exist != 0)
			{
				if($to_plus_left <= $exist)//Наличия на складе полностью покрывает потребность
				{
					//Резервируем товар на складе
					$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`- ?, `reserved`= `reserved` + ? WHERE `id` = ?;')->execute( array($to_plus_left, $to_plus_left, $storage_record_id) );
					
					//Обновляем свою детальную запись
					$db_link->prepare('UPDATE `shop_carts_details` SET `count_reserved` = `count_reserved` + ? WHERE `id`=?;')->execute( array($to_plus_left, $detail_id) );
					
					
					//Обновляем запись корзины
					$db_link->prepare('UPDATE `shop_carts` SET `count_need` = `count_need`+? WHERE `id` = ?;')->execute( array($to_plus_left, $cart_record_id) );
					
					//ПОЛНОСТЬЮ ПОКРЫЛИ - ВЫХОД
					$result = array();
					$result["status"] = true;
					$result["count_need"] = $count_need;
					$result["id"] = $cart_record_id;
					exit(json_encode($result));
				}
				else//Наличие покрывает потребность лишь частично - резервируем ВСЕ в этой поставке
				{
					$can_plus = $exist;//Можно зарезервировать на данном складе (ВСЕ, ЧТО ОСТАЛОСЬ)
					
					//Резервируем товар на складе
					$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`- ?, `reserved`=`reserved`+? WHERE `id` = ?;')->execute( array($can_plus, $can_plus, $storage_record_id) );
					
					
					//Обновляем свою детальную запись
					$db_link->prepare('UPDATE `shop_carts_details` SET `count_reserved` = `count_reserved` + ? WHERE `id`=?;')->execute( array($can_plus, $detail_id) );
					
					//Обновляем запись корзины
					$db_link->prepare('UPDATE `shop_carts` SET `count_need` = `count_need`+? WHERE `id` = ?;')->execute( array($can_plus, $cart_record_id) );
					
					$to_plus_left = $to_plus_left - $can_plus;//Осталось добавить
				}
			}
        }// while() По уже существующим записям таблицы shop_carts_details
        
        
        //2. ТЕПЕРЬ РЕЗЕРВИРУЕМ ТОВАР ПО ОСТАЛЬНЫМ ТОЧКАМ И СКЛАДАМ
        //2.1. START СНАЧАЛА РЕЗЕРВИРУЕМ ТО, ЧТО УЖЕ ЕСТЬ В НАЛИЧИИ
        //Для каждого магазина получить список складов и опросить каждый склад по ДАННОМУ товару
        for($i=0; $i < count($customer_offices); $i++)
        {
        	$office_id = $customer_offices[$i];//ID точки выдачи
        	
			$storages_query = $db_link->prepare('SELECT DISTINCT(`storage_id`) AS storage_id, `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ?;');
			$storages_query->execute( array($office_id) );
        	while($storage = $storages_query->fetch())
        	{
        		$storage_id = $storage["storage_id"];
        		$additional_time = $storage["additional_time"];
        		
        		//Время доставки больше нуля - значит товар еще требуется перевозить
        		if($additional_time > 0)continue;
        		
				
				//Получаем записи поставок, в которых цена (с учетом наценки), как в корзине и есть наличие
				$SQL_SUPPLIES = "SELECT * FROM ( SELECT `id`, `exist`, `price` AS `price_purchase`, `price` + `price`*(SELECT markup/100 AS markup FROM shop_offices_storages_map WHERE office_id = ? AND storage_id = ? AND group_id = ? AND min_point <= shop_storages_data.price AND max_point > shop_storages_data.price ) AS customer_price FROM `shop_storages_data` WHERE `product_id` = ? AND `arrival_time` < ? AND `exist` > 0 AND storage_id = ? ) AS storage_data WHERE customer_price = ?";
				$storage_product_records_query = $db_link->prepare($SQL_SUPPLIES);
				$storage_product_records_query->execute( array($office_id,$storage_id,$group_id,$product_id,$time,$storage_id,$price) );
				while($storage_product_record = $storage_product_records_query->fetch())
				{
					//1. Данные товара со склада:
					$storage_record_id = $storage_product_record["id"];
					$exist = $storage_product_record["exist"];
					$price_purchase = $storage_product_record["price_purchase"];
					
					if($to_plus_left <= $exist)//Наличия на складе полностью покрывает потребность
					{
						//Резервируем товар на складе
						$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`- ?, `reserved`= `reserved` + ? WHERE `id` = ?;')->execute( array($to_plus_left, $to_plus_left, $storage_record_id) );
						
						//Добавляем детальную запись корзины
						$db_link->prepare('INSERT INTO `shop_carts_details` (`cart_record_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `price_purchase`) VALUES (?,?,?,?,?,?);')->execute( array($cart_record_id, $office_id, $storage_id, $storage_record_id, $to_plus_left, $price_purchase) );
						
						//Обновляем запись корзины
						$db_link->prepare('UPDATE `shop_carts` SET `count_need` = `count_need`+? WHERE `id` = ?;')->execute( array($to_plus_left, $cart_record_id) );
						
						//ПОЛНОСТЬЮ ПОКРЫЛИ - ВЫХОД
						$result = array();
						$result["status"] = true;
						$result["count_need"] = $count_need;
						$result["id"] = $cart_record_id;
						exit(json_encode($result));
					}
					else//Наличие покрывает потребность лишь частично (резервируем все, что есть)
					{
						$can_plus = $exist;//Можно зарезервировать на данном складе
						
						//Резервируем товар на складе
						$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`- ?, `reserved`=`reserved`+? WHERE `id` = ?;')->execute( array($can_plus, $can_plus, $storage_record_id) );
						
						//Добавляем детальную запись корзины
						$db_link->prepare('INSERT INTO `shop_carts_details` (`cart_record_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `price_purchase`) VALUES (?,?,?,?,?,?);')->execute( array($cart_record_id, $office_id, $storage_id, $storage_record_id, $can_plus, $price_purchase) );
						
						//Обновляем запись корзины
						$db_link->prepare('UPDATE `shop_carts` SET `count_need` = `count_need`+? WHERE `id` = ?;')->execute( array($can_plus, $cart_record_id) );
						
						$to_plus_left = $to_plus_left - $can_plus;//Осталось добавить
					}
				}//while() - по учетным записям товара с данного склада
        	}//По складам
        }//По магазинам - опрашиваем для данного товара
        //2.1. ~ END СНАЧАЛА РЕЗЕРВИРУЕМ ТО, ЧТО УЖЕ ЕСТЬ В НАЛИЧИИ
        
        //2.2. START ТЕПЕРЬ РЕЗЕРВИРУЕМ ТО, ЧТО ТОЛЬКО ОЖИДАЕТСЯ ПОЛУЧИТЬ
        //Для каждого магазина получить список складов и опросить каждый склад по ДАННОМУ товару
        for($i=0; $i < count($customer_offices); $i++)
        {
        	$office_id = $customer_offices[$i];//ID точки выдачи
			
			$storages_query = $db_link->prepare('SELECT DISTINCT(`storage_id`), `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ?;');
			$storages_query->execute( array($office_id) );
        	while($storage = $storages_query->fetch() )
        	{
        		$storage_id = $storage["storage_id"];
        		
        		//Получаем соединение со складом	
				//Получаем записи поставок, в которых цена (с учетом наценки), как в корзине и есть наличие. Время здесь уже не учитываем, т.к. все-равно остались только будущие поставки
				
				$SQL_SUPPLIES = "SELECT * FROM ( SELECT `id`, `exist`, `price` AS `price_purchase`, `price` + `price`*(SELECT markup/100 AS markup FROM shop_offices_storages_map WHERE office_id = ? AND storage_id = ? AND group_id = ? AND min_point <= shop_storages_data.price AND max_point > shop_storages_data.price ) AS customer_price FROM `shop_storages_data` WHERE `product_id` = ? AND `exist` > 0 AND storage_id = ? ) AS storage_data WHERE customer_price = ?";
				$storage_product_records_query = $db_link->prepare($SQL_SUPPLIES);
				$storage_product_records_query->execute( array($office_id, $storage_id, $group_id, $product_id, $storage_id, $price) );
				while($storage_product_record = $storage_product_records_query->fetch() )
				{
					//1. Данные товара со склада:
					$storage_record_id = $storage_product_record["id"];
					$exist = $storage_product_record["exist"];
					$price_purchase = $storage_product_record["price_purchase"];
					
					if($to_plus_left <= $exist)//Наличие на складе полностью покрывает потребность
					{
						//Резервируем товар на складе
						$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`- ?, `reserved`= `reserved` + ? WHERE `id` = ?;')->execute( array($to_plus_left, $to_plus_left, $storage_record_id) );
						
						//Добавляем детальную запись корзины
						$db_link->prepare('INSERT INTO `shop_carts_details` (`cart_record_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `price_purchase`) VALUES (?,?,?,?,?,?);')->execute( array($cart_record_id, $office_id, $storage_id, $storage_record_id, $to_plus_left, $price_purchase) );
						
						//Обновляем запись корзины
						$db_link->prepare('UPDATE `shop_carts` SET `count_need` = `count_need`+? WHERE `id` = ?;')->execute( array($to_plus_left,$cart_record_id) );
						
						//ПОЛНОСТЬЮ ПОКРЫЛИ - ВЫХОД
						$result = array();
						$result["status"] = true;
						$result["count_need"] = $count_need;
						$result["id"] = $cart_record_id;
						exit(json_encode($result));
					}
					else//Наличие покрывает потребность лишь частично
					{
						$can_plus = $exist;//Можно зарезервировать на данном складе (резервируем все, что есть)
						
						//Резервируем товар на складе
						$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`- ?, `reserved`=`reserved`+? WHERE `id` = ?;')->execute( array($can_plus, $can_plus, $storage_record_id) );
						
						//Добавляем детальную запись корзины
						$db_link->prepare('INSERT INTO `shop_carts_details` (`cart_record_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `price_purchase`) VALUES (?,?,?,?,?,?);')->execute( array($cart_record_id, $office_id, $storage_id, $storage_record_id, $can_plus, $price_purchase) );
						
						//Обновляем запись корзины
						$db_link->prepare('UPDATE `shop_carts` SET `count_need` = `count_need`+? WHERE `id` = ?;')->execute( array($can_plus, $cart_record_id) );
						
						$to_plus_left = $to_plus_left - $can_plus;//Осталось добавить
					}
				}//while() - по учетным записям товара с данного склада
        	}//По складам
        }//По магазинам - опрашиваем для данного товара
        //2.2. ~ END ТЕПЕРЬ РЕЗЕРВИРУЕМ ТО, ЧТО ТОЛЬКО ОЖИДАЕТСЯ ПОЛУЧИТЬ
        
        
        //Если долши до сюда, значит не достаточно наличия - выдаем ответ
        $count_need_result_query = $db_link->prepare('SELECT `count_need` FROM `shop_carts` WHERE `id` = ?;');
		$count_need_result_query->execute( array($cart_record_id) );
        $count_need_result_record = $count_need_result_query->fetch();
        $count_need_result = $count_need_result_record["count_need"];
        $result = array();
        $result["status"] = false;
        $result["code"] = "not_enough";
		$result["message"] = translate_str_by_id(4468);
        $result["count_need"] = $count_need_result;
        $result["id"] = $cart_record_id;
        exit(json_encode($result));
    }//~else if($count_need > $current_count_need)//Прибавление количества
    else//Уменьшение количества
    {
        $to_minus_total_need = $current_count_need - $count_need;//Требуется вычесть всего
        $to_minus_left = $to_minus_total_need;//Сколько осталось вычесть
        
        //Получаем детальные записи корзины в обратном порядке, чтобы уменьшать количество в соответствии с приоритетами
        $details_query = $db_link->prepare('SELECT * FROM `shop_carts_details` WHERE `cart_record_id` = ? ORDER BY `id` DESC;');
		$details_query->execute( array($cart_record_id) );
        while( $detail = $details_query->fetch() )
        {
            $detail_id = $detail["id"];
            $office_id = $detail["office_id"];
            $storage_id = $detail["storage_id"];
            $storage_record_id = $detail["storage_record_id"];
            $count_reserved = $detail["count_reserved"];//Сколько зарезервировано товара по данной поставке
            
            $delete_this_record = false;//Детальную запись удалить после отказа от товара
            
            //Определяем количество товара, который мы сможем вернуть по данной поставке
            if($count_reserved >= $to_minus_left)
            {
                if($count_reserved == $to_minus_left)$delete_this_record = true;//В данной поставке зарезервировано все, что мы сейчас вернем - удаляем детальную запись корзины
                $can_cancel_count = $to_minus_left;//Сколько сможем вернуть ВСЕ
                $to_minus_left = 0;//Останется вычесть после (вернули, сколько и нужно было)
            }
            else
            {
                $can_cancel_count = $count_reserved;//Сколько сможем вернуть - сколько здесь зарезервировано
                $to_minus_left = $to_minus_left - $can_cancel_count;//Останется вычесть после
            }
            


			//Возвращает товар на склад
			
			$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist`+ ?, `reserved`= `reserved` - ? WHERE `id` = ?;')->execute( array($can_cancel_count, $can_cancel_count, $storage_record_id) );
			
			//Обновляем свою детальную запись
			if($delete_this_record)
			{
				//Удаляем детальную запись, т.к. в ней больше не осталось товара
				$db_link->prepare('DELETE FROM `shop_carts_details` WHERE `id`=?;')->execute( array($detail_id) );
			}
			else
			{
				$db_link->prepare('UPDATE `shop_carts_details` SET `count_reserved` = `count_reserved` - ? WHERE `id`=?;')->execute( array($can_cancel_count, $detail_id) );
			}
			
			//Обновляем запись корзины
			$db_link->prepare('UPDATE `shop_carts` SET `count_need` = `count_need`-? WHERE `id` = ?;')->execute( array($can_cancel_count, $cart_record_id) );
            
            //Если уменьшили количество до требуемого
            if($to_minus_left == 0)
            {
                $result = array();
                $result["status"] = true;
                $result["count_need"] = $count_need;
                $result["id"] = $cart_record_id;
                exit(json_encode($result));
            }
            
        }// while() По уже существующим записям таблицы shop_carts_details
    }//~else//Уменьшение количества
}//~function changeCountType1()
// ------------------------------------------------------------------------------------------------------------------------------------------
//Функция изменения количества товара для записей с product_type 2
function changeCountType2__old()
{
    global $db_link;
    global $DP_Config;
    global $cart_record_id;
    global $count_need;
    
    
    //Информация из записи корзины
	$cart_record_query = $db_link->prepare('SELECT `count_need`, `t2_exist`, `t2_min_order` FROM `shop_carts` WHERE `id` = ?;');
	$cart_record_query->execute( array($cart_record_id) );
    $cart_record = $cart_record_query->fetch();
    $current_count_need = $cart_record["count_need"];//Текущее количество
    $t2_exist = $cart_record["t2_exist"];//Всего доступно у поставщика
	$t2_min_order = (int)$cart_record["t2_min_order"];//Всего доступно у поставщика

    
    if($current_count_need == $count_need)
    {
		$result = array();
		$result["status"] = false;
		$result["code"] = "the_same_count";
		$result["message"] = translate_str_by_id(4467);
		exit(json_encode($result));
		
        //exit("The same count...");
    }
    else if($count_need > $current_count_need)//Прибавление количества
    {
        if($count_need > $t2_exist)//Если товара у поставщика не достаточно для увеличения количества
        {
            //Увеличиваем количество товара до максимально возможного
            $db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($t2_exist, $cart_record_id) );
            $result = array();
            $result["status"] = false;
            $result["code"] = "not_enough";
			$result["message"] = translate_str_by_id(4468);
            $result["count_need"] = $t2_exist;
            $result["id"] = $cart_record_id;
            exit(json_encode($result));
        }
        else//Товара достаточно - увеличиваем количество
        {
			$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($count_need, $cart_record_id) );
            $result = array();
            $result["status"] = true;
            $result["count_need"] = $count_need;
            $result["id"] = $cart_record_id;
            exit(json_encode($result));
        }
    }
    else//Уменьшение количества
    {
		if($count_need < $t2_min_order){
			$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($t2_min_order, $cart_record_id) );
			$result = array();
			$result["status"] = true;
			$result["count_need"] = $t2_min_order;
			$result["id"] = $cart_record_id;
			exit(json_encode($result));
		}else{
			$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($count_need, $cart_record_id) );
			$result = array();
			$result["status"] = true;
			$result["count_need"] = $count_need;
			$result["id"] = $cart_record_id;
			exit(json_encode($result));
		}
    }
}
function changeCountType2()
{
    global $db_link;
    global $DP_Config;
    global $cart_record_id;
    global $count_need;
    
    
    //Информация из записи корзины
	$cart_record_query = $db_link->prepare("SELECT `count_need`, `t2_exist`, `t2_min_order` FROM `shop_carts` WHERE `id` = ?;");
	$cart_record_query->execute( array($cart_record_id) );
    $cart_record = $cart_record_query->fetch();
    $current_count_need = $cart_record["count_need"];//Текущее количество
    $t2_exist = $cart_record["t2_exist"];//Всего доступно у поставщика
    $t2_min_order = $cart_record["t2_min_order"];//Всего доступно у поставщика
	
	
	if($count_need < $t2_exist){
	$flag = false;
	for($i = $t2_min_order; $i <= $t2_exist; $i+=$t2_min_order){
		if($i == $count_need){
			$flag = true;
			break;
		}
	}
	if($flag == false){
		$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($t2_min_order, $cart_record_id) );
		$result = array();
		$result["status"] = false;
		$result["code"] = "error";
		$result["i"] = $i;
		$result["count_need"] = $count_need;
		$result["t2_min_order"] = $t2_min_order;
		$result["message"] = translate_str_by_id(4469);
		$result["count_need"] = $t2_min_order;
		$result["t2_exist"] = $t2_exist;
		$result["id"] = $cart_record_id;
		exit(json_encode($result));
	}
	}
    
    if($current_count_need == $count_need)
    {
		$result = array();
		$result["status"] = false;
		$result["code"] = "the_same_count";
		$result["message"] = translate_str_by_id(4467);
		exit(json_encode($result));
		
        //exit("The same count...");
    }
    else if($count_need > $current_count_need)//Прибавление количества
    {
        if($count_need > $t2_exist)//Если товара у поставщика не достаточно для увеличения количества
        {
            //Увеличиваем количество товара до максимально возможного (Пока выключии, включим при необходимости)
			//$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($t2_exist, $cart_record_id) );
            $result = array();
            $result["status"] = false;
            $result["code"] = "not_enough";
			$result["message"] = translate_str_by_id(4468);
            //$result["count_need"] = $t2_exist;
            $result["count_need"] = $current_count_need;
            $result["id"] = $cart_record_id;
            exit(json_encode($result));
        }
        else//Товара достаточно - увеличиваем количество
        {
			$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($count_need, $cart_record_id) );
            $result = array();
            $result["status"] = true;
            $result["count_need"] = $count_need;
            $result["id"] = $cart_record_id;
            exit(json_encode($result));
        }
    }
    else//Уменьшение количества
    {
        if($t2_min_order > $count_need){
			$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($t2_min_order, $cart_record_id) );
            $result = array();
            $result["status"] = false;
            $result["code"] = "not_enough";
			$result["message"] = translate_str_by_id(4469);
            $result["count_need"] = $t2_min_order;
            $result["id"] = $cart_record_id;
            exit(json_encode($result));
		}else{
			$db_link->prepare('UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?;')->execute( array($count_need, $cart_record_id) );
			$result = array();
			$result["status"] = true;
			$result["count_need"] = $count_need;
			$result["id"] = $cart_record_id;
			exit(json_encode($result));
		}
    }
}
// ------------------------------------------------------------------------------------------------------------------------------------------
?>