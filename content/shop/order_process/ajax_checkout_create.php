<?php
/**
 * Сервеный скрипт для создания заказа
 * 
 * Алгоритм:
 * 
 * 1. Создать учетную запись заказа
 * 2. Создать записи позиций заказа (по сути перенос записей корзины)
 * 3. Создать детальные записи заказа (по сути перенос детальных записей корзины)
 * 4. Если пользователь не авторизован - у него куки - их нужно очистить
 * 
 * 
 * 
 * !!! ЗАКАЗ ПРИВЯЗЫВАЕТСЯ К ОДНОМУ ОФИСУ:
 * 1. ЕСЛИ СПОСОБ ПОЛУЧЕНИЯ САМОВЫВОЗ - ТО, ОФИС - ТОТ, ЧТО ВЫБРАЛ ПОКУПАТЕЛЬ
 * 2. ЕСЛИ СПОСОБ ПОЛУЧЕНИЯ ДОСТАВКА - ТО, ОФИС - САМЫЙ ПЕРВЫЙ ИЗ ДЕТАЛЬНЫХ ЗАПИСЕЙ
 * 
*/
header('Content-Type: application/json;charset=utf-8;');

//Рекурвиная функция. Обрабатывает все значения древовидного массива через htmlentities
function prepare_json_htmlentities($how_get)
{
	foreach($how_get AS $key=>$value)
	{
		if( is_array($value) )
		{
			$how_get[$key] = prepare_json_htmlentities($value);
		}
		else
		{
			$how_get[$key] = htmlentities($value);
		}
	}
	
	return $how_get;
}



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


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------



//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
if ($user_id > 0 && !epc_trade_can_place_order($db_link, (int)$user_id)) {
	$result = array();
	$result['status'] = false;
	$result['code'] = 'trade_not_approved';
	$result['message'] = epc_trade_checkout_block_message($db_link, (int)$user_id);
	exit(json_encode($result));
}
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );



//Проверка, включена ли функции заказа без регистрации, если пользователь не авторизован
if( $user_id == 0 )
{
	if( isset( $DP_Config->order_without_auth ) )
	{
		if( $DP_Config->order_without_auth != 1 )
		{
			$result = array();
			$result["status"] = false;
			$result["message"] = translate_str_by_id(4470);
			exit(json_encode($result));
		}
	}
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



//СОЗДАНИЕ ЗАКАЗА ЧЕРЕЗ ТРАНЗАКЦИЮ
try
{
	//Меняем статус autocommit на FALSE. Т.е. старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception(translate_str_by_id(2132));
	}
	
	if( !isset($_COOKIE["users_agreement"]) )
	{
		throw new Exception(translate_str_by_id(4471));
	}
	if($_COOKIE["users_agreement"] != "yes")
	{
		throw new Exception(translate_str_by_id(4471));
	}

	$time = time();//Время создания заказа

	$office_executer = 0;//Офис - исполнитель

	//Сначала проверяем наличие товаров в корзине
	$check_already_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_carts` WHERE `user_id`=? AND `session_id` = ?;');
	$check_already_query->execute( array($user_id, $session_id) );
	if( $check_already_query->fetchColumn() == 0 )
	{
		throw new Exception(translate_str_by_id(4472));
	}
	
	
	//1.
	//Получаем статус заказа, который присваивается для вновь-оформленного
	$for_created_status_query = $db_link->prepare('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_created`=1;');
	$for_created_status_query->execute();
	$for_created_status_record = $for_created_status_query->fetch();
	$for_created_status = $for_created_status_record["id"];
	
	if( (int)$for_created_status <= 0 )
	{
		throw new Exception(translate_str_by_id(4473));
	}
	
	//Выясняем способ получения:
	if( !isset($_COOKIE["how_get"]) )
	{
		throw new Exception(translate_str_by_id(4474));
	}
	$how_get = json_decode($_COOKIE["how_get"], true);

	$how_get["mode"] = (int)$how_get["mode"];
	//Проверяем, не подменен ли $how_get["mode"]
	$check_how_get_mode_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_obtaining_modes` WHERE `id` = ?;");
	$check_how_get_mode_query->execute( array($how_get["mode"]) );
	if( $check_how_get_mode_query->fetchColumn() != 1 )
	{
		throw new Exception(translate_str_by_id(4475));
	}

	if($how_get["mode"] == 1)
	{
		$office_executer = (int)$how_get["office_id"];//Назначаем офис-исполнитель
		
		if( $office_executer <= 0 )
		{
			throw new Exception(translate_str_by_id(4476));
		}
	}
	
	//Контакты для неавторизованного покупателя - полная проверка, включая регулярное выражение
	$phone_not_auth = '';
	$email_not_auth = '';
	if( $user_id == 0 )
	{
		//Телефон - должен указываться обязательно
		if( !isset( $_POST['phone_not_auth'] ) )
		{
			throw new Exception(translate_str_by_id(4477));
		}
		$phone_not_auth = trim($_POST['phone_not_auth']);
		if( ! DP_User::check_contact_by_regexp($phone_not_auth, 'phone') )
		{
			throw new Exception(translate_str_by_id(4478));
		}
		
		
		//E-mail - может указываться по желанию
		if( isset( $_POST['email_not_auth'] ) )
		{
			$email_not_auth = trim($_POST['email_not_auth']);
			if( !empty( $email_not_auth ) )
			{
				if( ! DP_User::check_contact_by_regexp($email_not_auth, 'email') )
				{
					throw new Exception(translate_str_by_id(4479));
				}
			}
		}
	}
	
	
	//$_COOKIE["how_get"] может содержать информацию в JSON-виде. Поэтому, перед записью в БД - нужно пронать все его значения через htmlentities. Вложенность может быть любая, поэтому, делаем рекурсивно
	$how_get = prepare_json_htmlentities($how_get);


	if( $db_link->prepare('INSERT INTO `shop_orders` (`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`) VALUES (?,?,?,?,?,?,?,?,?,?);')->execute( array($user_id, $session_id, $time, 0, $for_created_status, 0, $how_get["mode"], json_encode($how_get), htmlentities($phone_not_auth), htmlentities($email_not_auth) ) ) != true)
	{
		throw new Exception(translate_str_by_id(4480));
	}
	//1.1. Получаем ID созданной записи
	$order_id = $db_link->lastInsertId();
	if( ! ($order_id > 0) )
	{
		throw new Exception(translate_str_by_id(4481));
	}
	
	
	//2.
	//Получаем статус позиции заказа, который присваивается для вновь-созданной позиции
	$for_created_status_query = $db_link->prepare('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_created`=1;');
	$for_created_status_query->execute();
	$for_created_status_record = $for_created_status_query->fetch();
	$for_created_status = $for_created_status_record["id"];
	
	if( $for_created_status <= 0 )
	{
		throw new Exception(translate_str_by_id(4482));
	}
	

	//2.1. Получаем перечень id записей корзины (позиции в корзине)
	$cart_records_ids = array();
	$cart_ids_query = $db_link->prepare('SELECT `id` FROM `shop_carts` WHERE `user_id`=? AND `session_id` = ?;');
	$cart_ids_query->execute( array($user_id, $session_id) );
	while($cart_id_record = $cart_ids_query->fetch() )
	{
		array_push($cart_records_ids, $cart_id_record["id"]);
	}
	$binding_values = array();
	$SQL_SELECT_CART = "SELECT * FROM `shop_carts` WHERE (";
	for($c=0; $c < count($cart_records_ids); $c++)
	{
		$cart_record_id = (int)$cart_records_ids[$c];
		
		if( $cart_record_id <= 0 )
		{
			throw new Exception(translate_str_by_id(4483));
		}
		
		if($c > 0) $SQL_SELECT_CART .= " OR ";
		$SQL_SELECT_CART .= "`id`=?";
		
		array_push($binding_values, $cart_record_id);
	}
	$SQL_SELECT_CART .= ") AND `checked_for_order` = 1;";
	//ПОЛУЧАЕМ ВСЕ ЗАПИСИ КОРЗИНЫ (ВСЕ ПОЗИЦИИ КОРЗИНЫ)
	$cart_items = array();//Список для записей корзины - чтобы потом их удалить
	$cart_details = array();//Список для детальных записей корзины - чтобы потом их удалить
	$cart_query = $db_link->prepare($SQL_SELECT_CART);
	$cart_query->execute($binding_values);
	$t2_office_id_first = 0;//Для получения офиса обслуживания для продукта типа 2. Чтобы гарантированно инициализировать офис обслуживания при способе получения "Доставка по адресу"
	while($cart_record = $cart_query->fetch() )//ПО ПОЗИЦИЯМ КОРЗИНЫ
	{
		$cart_record_id = $cart_record["id"];
		$product_type = $cart_record["product_type"];
		$product_id = $cart_record["product_id"];
		$price = $cart_record["price"];
		$count_need = $cart_record["count_need"];
		
		array_push($cart_items, $cart_record_id);
		
		//2.2 Добавляем запись позиции заказа (аналог записи из таблицы shop_carts). Логика зависит от типа продукта
		if($product_type == 1)
		{
			$t2_manufacturer = $cart_record["t2_manufacturer"];
			$t2_article = $cart_record["t2_article"];
			$t2_article_show = $cart_record["t2_article_show"];
			$t2_name = $cart_record["t2_name"];
			$t2_exist = $cart_record["t2_exist"];
			$t2_time_to_exe = $cart_record["t2_time_to_exe"];
			$t2_time_to_exe_guaranteed = $cart_record["t2_time_to_exe_guaranteed"];
			$t2_storage = $cart_record["t2_storage"];
			$t2_min_order = $cart_record["t2_min_order"];
			$t2_probability = $cart_record["t2_probability"];
			$t2_markup = $cart_record["t2_markup"];
			$t2_price_purchase = 0;
			$t2_office_id = $cart_record["t2_office_id"];
			$t2_office_id_first = $t2_office_id;
			$t2_storage_id = $cart_record["t2_storage_id"];
			$t2_product_json = $cart_record["t2_product_json"];
			$t2_json_params = (string) ($cart_record["t2_json_params"] ?? "");
			$sao_state = '?';
			$sao_robot = '?';
		}
		else if($product_type == 2)
		{
			$t2_manufacturer = $cart_record["t2_manufacturer"];
			$t2_article = $cart_record["t2_article"];
			$t2_article_show = $cart_record["t2_article_show"];
			$t2_name = $cart_record["t2_name"];
			$t2_exist = $cart_record["t2_exist"];
			$t2_time_to_exe = $cart_record["t2_time_to_exe"];
			$t2_time_to_exe_guaranteed = $cart_record["t2_time_to_exe_guaranteed"];
			$t2_storage = $cart_record["t2_storage"];
			$t2_min_order = $cart_record["t2_min_order"];
			$t2_probability = $cart_record["t2_probability"];
			$t2_markup = $cart_record["t2_markup"];
			$t2_price_purchase = $cart_record["t2_price_purchase"];
			$t2_office_id = $cart_record["t2_office_id"];
			$t2_office_id_first = $t2_office_id;
			$t2_storage_id = $cart_record["t2_storage_id"];
			$t2_json_params = $cart_record["t2_json_params"];
			$t2_product_json = '';
			
			//Получаем значения sao_state и sao_robot для данного поставщика
			$sao_state = "( SELECT IFNULL( (SELECT `state_id` FROM `shop_sao_states_types_link` WHERE `is_start` = 1 AND `interface_type_id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?)) ,0) AS `state_id` )";
			
			$sao_robot = "( SELECT IFNULL( (SELECT `action_id` FROM `shop_sao_states_types_actions_link` WHERE `is_start` = 1 AND `state_type_id` = (SELECT `id` FROM `shop_sao_states_types_link` WHERE `is_start` = 1 AND `interface_type_id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?) )) ,0) AS `action_id` )";
		}
		$SQL_INSERT_ORDER_ITEM = "INSERT INTO `shop_orders_items` 
		(`order_id`, 
		`product_type`, 
		`price`, 
		`count_need`, 
		`product_id`, 
		`status`,
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
		`sao_state`,
		`sao_robot`,
		`t2_json_params`
		) 
		VALUES 
		(";
		
		$binding_values = array($order_id,$product_type, $price, $count_need, $product_id, $for_created_status,$t2_manufacturer,$t2_article,$t2_article_show,$t2_name,$t2_exist,$t2_time_to_exe,$t2_time_to_exe_guaranteed,$t2_storage,$t2_min_order,$t2_probability,$t2_markup,$t2_price_purchase,$t2_office_id,$t2_storage_id,$t2_product_json);
		
		$SQL_INSERT_ORDER_ITEM = $SQL_INSERT_ORDER_ITEM.str_repeat('?,', count($binding_values) - 1) . '?,'.$sao_state.','.$sao_robot.',?);';
		
		//Добавим оставшиеся переменные
		array_push($binding_values, $t2_storage_id);//Из $sao_state
		array_push($binding_values, $t2_storage_id);//Из $sao_robot
		array_push($binding_values, $t2_json_params);

		if( $db_link->prepare($SQL_INSERT_ORDER_ITEM)->execute( $binding_values ) != true)
		{
			throw new Exception(translate_str_by_id(4484));
		}
		$order_item_id =$db_link->lastInsertId();//ID добавленной записи (позиция заказа)
		
		if( $order_item_id <= 0 )
		{
			throw new Exception(translate_str_by_id(4485));
		}
		
		//Для не трилаксных типов не создаем детальные записи
		if($product_type != 1)
		{
			continue;
		}
		
		//3.
		//3.1 Получаем детальные записи позиции корзины
		$cart_record_details_query = $db_link->prepare('SELECT * FROM `shop_carts_details` WHERE `cart_record_id` = ?;');
		$cart_record_details_query->execute( array($cart_record_id) );
		while( $cart_record_detail = $cart_record_details_query->fetch() )
		{
			$detail_id = $cart_record_detail["id"];
			$office_id = $cart_record_detail["office_id"];
			$storage_id = $cart_record_detail["storage_id"];
			$storage_record_id = $cart_record_detail["storage_record_id"];
			$count_reserved = $cart_record_detail["count_reserved"];
			$price_purchase = 0;//Цена закупа (берется с поставки склада)
			array_push($cart_details, $detail_id);
			
			//Получаем цену ЗАКУПА по данной поставке со склада:
			$SQL_currency_rate = "(SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`) )";
			
			$price_purchase_query = $db_link->prepare('SELECT `price`*'.$SQL_currency_rate.' AS `price`, `price_purchase`*'.$SQL_currency_rate.' AS `price_purchase` FROM `shop_storages_data` WHERE `id`= ?;');
			$price_purchase_query->execute( array($storage_record_id) );
			$price_purchase_record = $price_purchase_query->fetch();
			if($price_purchase_record["price_purchase"] > 0){
				$price_purchase = $price_purchase_record["price_purchase"];
			}else{
				$price_purchase = $price_purchase_record["price"];
			}

			//Если $office_executer не выставлен - значит способ получения - Доставка. Первый попавшийся офис - назначаем исполнителем заказа
			if($office_executer == 0)
			{
				$office_executer = $office_id;
			}
			
			//3.2 Создаем детальную запись позиции заказа
			if( $db_link->prepare('INSERT INTO `shop_orders_items_details` (`order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`) VALUES (?,?,?,?,?,?,?,?,?);')->execute( array($order_id, $order_item_id, $office_id, $storage_id, $storage_record_id, $count_reserved, 0, 0, $price_purchase) ) != true)
			{
				throw new Exception(translate_str_by_id(5682));
			}
		}//while() - по детальным записям одной позиции корзины
	}//~while() - по позициям корзины

	

	//При выполнении алгоритма - $office_executer не инициализировался - т.е. товар product_type = 2 (там другая система)
	if($office_executer == 0)
	{
		$office_executer = $t2_office_id_first;
	}
	if( $db_link->prepare('UPDATE `shop_orders` SET `successfully_created`=1, `office_id`=? WHERE `id` = ?;')->execute( array($office_executer, $order_id) ) != true)
	{
		throw new Exception(translate_str_by_id(4487));
	}



	//ОЧИЩАЕМ КОРЗИНУ
	//Удаляем записи позиций корзины
	$binding_values = array();
	$SQL_DELETE_CART_ITEMS = "DELETE FROM `shop_carts` WHERE (";
	for($i=0; $i < count($cart_items); $i++)
	{
		if($i>0)$SQL_DELETE_CART_ITEMS .= " OR ";
		
		$cart_id = (int)$cart_items[$i];
		
		$SQL_DELETE_CART_ITEMS .= "`id`=?";
		array_push($binding_values, $cart_id);
	}
	$SQL_DELETE_CART_ITEMS .= ") AND `checked_for_order` = 1;";
	
	if( $db_link->prepare($SQL_DELETE_CART_ITEMS)->execute( $binding_values ) != true )
	{
		throw new Exception(translate_str_by_id(4488));
	}
	
	//Удаляем детальные записи корзины
	if( count($cart_details) > 0 )
	{
		$binding_values = array();
		$SQL_DELETE_CART_DETAILS = "DELETE FROM `shop_carts_details` WHERE ";
		for($i=0; $i < count($cart_details); $i++)
		{
			if($i>0)$SQL_DELETE_CART_DETAILS .= " OR ";
			$cart_detail_id = (int)$cart_details[$i];
			$SQL_DELETE_CART_DETAILS .= "`id`=?";
			
			array_push($binding_values, $cart_detail_id);
		}
		$SQL_DELETE_CART_DETAILS .= ";";
		if( $db_link->prepare($SQL_DELETE_CART_DETAILS)->execute( $binding_values ) != true )
		{
			throw new Exception(translate_str_by_id(4489));
		}
	}
	
	
	
	//Записываем комментарий покупателя:
	if( !empty($_POST["buyer_po_number"]) )
	{
		$buyer_po = substr(trim((string)$_POST["buyer_po_number"]), 0, 64);
		$buyer_po = htmlentities($buyer_po, ENT_QUOTES, 'UTF-8');
		if( $buyer_po !== '' )
		{
			$db_link->prepare('INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`) VALUES (?,?,?,?);')->execute( array($order_id, 1, 'Purchase Order: '.$buyer_po, time()) );
			$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,?);')->execute( array($order_id, time(), $user_id, 0, 'Buyer PO: '.$buyer_po, 0) );
		}
	}
	if( !empty($_POST["order_message"]) )
	{
		$order_message = trim($_POST["order_message"]);
		$order_message = htmlentities($order_message);
		$order_message = str_replace("\r","",$order_message);
		$order_message = str_replace("\t","",$order_message);
		$order_message = str_replace("\n","<br/>",$order_message);
		
		$db_link->prepare('INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`) VALUES (?,?,?,?);')->execute( array($order_id, 1, $order_message, time()) );
	}
	
	
	
	//Пишем лог заказа
	if( $db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?,?,?,?,?);')->execute( array($order_id, time(), $user_id, 0, 'Заказ создан') ) != true )
	{
		throw new Exception("SQL-ошибка журнала заказа");
	}
	
	
	
	//Указываем, что заказ не просмотрен менеджером
	//Получаем список менеджеров данного офиса обслуживания
	$managers_query = $db_link->prepare('SELECT `users` FROM `shop_offices` WHERE `id` = ?;');
	$managers_query->execute( array($office_executer) );
	$managers_record = $managers_query->fetch();
	if( $managers_record != false )
	{
		//Список ID менеджеров
		$managers_list = json_decode($managers_record["users"], true);

		for($i=0; $i < count($managers_list); $i++)
		{
			//Проверяем что пользователь принадлежит группе с доступом в Бэкенд
			if( ! DP_User::isBackendGroupById($managers_list[$i]) ){
				continue;
			}
			
			//Ставим флаг "Не просмотрен" для данного менеджера
			if( $db_link->prepare('INSERT INTO `shop_orders_viewed` (`user_id`, `order_id`, `viewed_flag`) VALUES (?,?,?);')->execute( array((int)$managers_list[$i], $order_id, 0) ) != true )
			{
				throw new Exception(translate_str_by_id(4490));
			}
		}
	}
	
	
	
	//Очищаем куки
	//Если пользователь не авторизован - удаляем куки корзины:
	if($user_id == 0)
	{
		$cookietime = time()-99999;//Время в прошлом
		setcookie("products_in_cart", "", $cookietime, "/");
		
		//Ставим куки только что созданного заказа - пока не требуется
		$cookietime = time()+9999999;//на долго
		setcookie("created_order", $order_id, $cookietime, "/");
	}
}
catch (Exception $e)
{
	$db_link->rollBack();//Откатываем все изменения и закрываем транзакцию
	
	
	$result = array();
	$result["status"] = false;
	$result["message"] = translate_str_by_id(4492).". ".$e->getMessage();
	exit(json_encode($result));
}


//Дошли сюда - значит все запросы выполнены без ошибок
$db_link->commit();//Коммитим все изменения и закрываем транзакцию


// -------------------------------------------------------------------------------------------

//Связь заказа с автомобилем гаража
if($user_id > 0)
{
	//Автомобиль
	$cars_query = $db_link->prepare('SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND `active` = 1 LIMIT 1;');
	$cars_query->execute( array($user_id) );
	$car = $cars_query->fetch();
	
	if($car['id'] > 0)
	{
		$garage_orders_query = $db_link->prepare('INSERT INTO `shop_docpart_garage_orders`(`id`, `garage_id`, `order_id`) VALUES (NULL,?,?);');
		$garage_orders_query->execute( array($car['id'], $order_id) );
	}
}

// -------------------------------------------------------------------------------------------


// Staff + customer order emails (admin always, with retry + order log)
$epc_notify_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
if (is_readable($epc_notify_path)) {
	require_once $epc_notify_path;
	if (function_exists('epc_checkout_send_order_notifications')) {
		epc_checkout_send_order_notifications(
			$db_link,
			(int)$order_id,
			(int)$user_id,
			(int)$office_executer,
			isset($email_not_auth) ? (string)$email_not_auth : '',
			isset($phone_not_auth) ? (string)$phone_not_auth : ''
		);
	} else {
		$order_text = '';
		include $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_manager.php';
		$notify_vars = array('order_id' => $order_id, 'order_text' => $order_text);
		epc_staff_send_notify('new_order_to_manager', $notify_vars, (int)$user_id, (int)$office_executer, array(), true);
		$persons = array();
		if ($user_id > 0) {
			$persons[] = array('type' => 'user_id', 'user_id' => $user_id);
		} else {
			$persons[] = array(
				'type' => 'direct_contact',
				'contacts' => array(
					'email' => array('value' => htmlentities($email_not_auth)),
					'phone' => array('value' => htmlentities($phone_not_auth)),
				),
			);
		}
		$order_text = '';
		include $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_user.php';
		$order_message_query = $db_link->prepare('SELECT `text` FROM `shop_orders_messages` WHERE `order_id` = ? AND `is_customer` = 1 ORDER BY `id` ASC LIMIT 1;');
		$order_message_query->execute(array($order_id));
		$order_message = $order_message_query->fetch();
		if (!empty($order_message['text'])) {
			$order_text .= '<h4>' . translate_str_by_id(4509) . '</h4>';
			$order_text .= '<div style="font-family: Calibri; font-size: 14px;">' . str_replace("\n", '<br/>', $order_message['text']) . '</div>';
		}
		$notify_vars = array('order_id' => $order_id, 'order_text' => $order_text);
		send_notify('new_order_to_user', $notify_vars, $persons, true);
	}
} else {
	$managers_query = $db_link->prepare('SELECT `users` FROM `shop_offices` WHERE `id` = ?;');
	$managers_query->execute(array($office_executer));
	$managers_record = $managers_query->fetch();
	if ($managers_record != false) {
		$managers_list = json_decode($managers_record['users'], true);
		$persons = array();
		if (is_array($managers_list)) {
			for ($i = 0; $i < count($managers_list); $i++) {
				if (!DP_User::isBackendGroupById($managers_list[$i])) {
					continue;
				}
				$persons[] = array('type' => 'user_id', 'user_id' => (int)$managers_list[$i]);
			}
		}
		if (!empty($persons)) {
			$order_text = '';
			include $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_manager.php';
			$notify_vars = array('order_id' => $order_id, 'order_text' => $order_text);
			send_notify('new_order_to_manager', $notify_vars, $persons, true);
		}
	}
	$persons = array();
	if ($user_id > 0) {
		$persons[] = array('type' => 'user_id', 'user_id' => $user_id);
	} else {
		$persons[] = array(
			'type' => 'direct_contact',
			'contacts' => array(
				'email' => array('value' => htmlentities($email_not_auth)),
				'phone' => array('value' => htmlentities($phone_not_auth)),
			),
		);
	}
	$order_text = '';
	include $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_user.php';
	$order_message_query = $db_link->prepare('SELECT `text` FROM `shop_orders_messages` WHERE `order_id` = ? AND `is_customer` = 1 ORDER BY `id` ASC LIMIT 1;');
	$order_message_query->execute(array($order_id));
	$order_message = $order_message_query->fetch();
	if (!empty($order_message['text'])) {
		$order_text .= '<h4>' . translate_str_by_id(4509) . '</h4>';
		$order_text .= '<div style="font-family: Calibri; font-size: 14px;">' . str_replace("\n", '<br/>', $order_message['text']) . '</div>';
	}
	$notify_vars = array('order_id' => $order_id, 'order_text' => $order_text);
	send_notify('new_order_to_user', $notify_vars, $persons, true);
}


// -------------------------------------------------------------------------------------------

// ERP order fulfillment — open sales order + draft POs per supplier (non-blocking)
$epc_of_file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
if (is_readable($epc_of_file) && (int)$user_id > 0) {
	require_once $epc_of_file;
	try {
		epc_erp_order_fulfillment_bootstrap($db_link, (int)$order_id);
	} catch (Throwable $epc_of_err) {
		$db_link->prepare(
			'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, 0, 1, ?, 1);'
		)->execute(array(
			(int)$order_id,
			time(),
			'ERP fulfillment bootstrap skipped: ' . $epc_of_err->getMessage(),
		));
	}
}

// -------------------------------------------------------------------------------------------


//Возвращаем результат - УСПЕШНО
$result = array();
$result["status"] = true;
$result["message"] = translate_str_by_id(4493).": ".$order_id;
$result["order_id"] = $order_id;
exit(json_encode($result));
?>