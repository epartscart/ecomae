<?php 
if($_GET["initiator"] != 1 && $_GET["initiator"] != 2)
{
	exit();
}

/**
 * Серверный скрипт для выставления статуса отдельной позиции
 * 
 * 
 * Инициаторы:
 * 1 - менеджер
 * 2 - Скрипт, например SAO, робот
 * 
 * 
*/
header('Content-Type: application/json;charset=utf-8;');

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



//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

//Для отправки уведомлений
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );


$result = array();//Результат работы



//Входные данные:
$initiator = $_GET["initiator"];
$orders_items = json_decode($_GET["orders_items"], true);
$status = $_GET["status"];
$key = null;
if( isset($_GET["key"]) )
{
	$key = $_GET["key"];
}



//ПРОВЕРКА ПРАВ
if($initiator == 1)
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    //Проверяем право менеджера
    if( ! DP_User::isAdmin())
    {
        $result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));//Вообще не является администратором бэкенда
    }
}
if($initiator == 2)
{
	if( $key != $DP_Config->tech_key )
	{
		$result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));
	}
}



// -----------------------------------------------------------------------------------------------------------

// Разбиение / Возврат позиции 

// ТРАНЗАКЦИЯ
try
{
	//Меняем статус autocommit на FALSE. Т.е. старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception(translate_str_by_id(2132));
	}
	
	if(((int)$_GET["retun"]) === 1){
		
		// Массив статусов позиций
		$items_statuses = array();
		$query = $db_link->prepare('SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order`;');
		$query->execute();
		while($record = $query->fetch()){
			$items_statuses[$record['id']] = $record;
		}
		
		$time = time();
		
		// Массив id позиций, должна быть 1 позиция
		$orders_items = json_decode($_GET["orders_items"], true);
		if(count($orders_items) > 1){
			throw new Exception(translate_str_by_id(5630));
		}
		$item_id = $orders_items[0];
		
		// Информация по позиции
		$sql = "SELECT * FROM `shop_orders_items` WHERE `id` = ?;";
		$query = $db_link->prepare($sql);
		$query->execute(array($item_id));
		$item_info = $query->fetch();
		
		$order_id = $item_info['order_id'];
		
		// Информация по заказу
		$query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ?;');
		$query->execute(array($order_id));
		$order_info = $query->fetch();
		
		// Переданное количество для разделения позиции недопустимо
		if( (((int)$item_info['count_need']) < ((int)$_GET["count"])) || (((int)$_GET["count"]) <= 0) ){
			throw new Exception(translate_str_by_id(5631));
		}
		
		// Разбивается ли позиция на части
		if( ((int)$item_info['count_need']) > ((int)$_GET["count"]) ){
			// Клонируем запись во временную таблицу
			$sql = "CREATE TEMPORARY TABLE `tmp` SELECT * FROM `shop_orders_items` WHERE `id` = ?;";
			$query = $db_link->prepare($sql);
			$query->execute(array($item_id));
			
			// Удаляем id клонируемой позиции
			$sql = "UPDATE `tmp` SET `id` = NULL, `count_need` = ?;";
			$query = $db_link->prepare($sql);
			$query->execute(array((int)$_GET["count"]));

			// Клонируем запись в таблицу позиций
			$sql = "INSERT INTO `shop_orders_items` SELECT * FROM `tmp`;";
			$query = $db_link->prepare($sql);
			if($query->execute()){
				// ID добавленной записи (позиция заказа)
				$new_order_item_id = $db_link->lastInsertId();
				
				// Добавляем детальные записи по товарам из каталога
				if($item_info['product_type'] == 1){
					$sql = "INSERT INTO `shop_orders_items_details` 
					(`id`, `order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`) 
					SELECT 
					NULL, `order_id`, $new_order_item_id, `office_id`, `storage_id`, `storage_record_id`, ".((int)$_GET["count"]).", `count_issued`, `count_canceled`, `price_purchase` FROM `shop_orders_items_details` WHERE `order_item_id` = $item_id";
					$query = $db_link->prepare($sql);
					if(!$query->execute()){
						throw new Exception(translate_str_by_id(5632));
					}
				}
				
				// Пишем лог
				if( $db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?, ?, ?, ?, ?);')->execute( array($order_id, $time, DP_User::getAdminId(), 1,'Дублированна позиция ID '.$new_order_item_id.' от позиции ID '.$item_id.' с указанием количества '.((int)$_GET["count"]).' шт.') ) != true )
				{
					throw new Exception(translate_str_by_id(5633));
				}
				
				// Уменьшаем количество у исходной позиции
				$sql = "UPDATE `shop_orders_items` SET `count_need` = ? WHERE `id` = ?;";
				$query = $db_link->prepare($sql);
				if( $query->execute(array( (((int)$item_info['count_need']) - ((int)$_GET["count"])), $item_id )) != true )
				{
					throw new Exception(translate_str_by_id(5634));
				}
				
				// Уменьшаем количество у исходной позиции детальной записи по товарам из каталога
				if($item_info['product_type'] == 1){
					$sql = "UPDATE `shop_orders_items_details` SET `count_reserved` = ? WHERE `order_item_id` = ?;";
					$query = $db_link->prepare($sql);
					if( $query->execute(array( (((int)$item_info['count_need']) - ((int)$_GET["count"])), $item_id )) != true )
					{
						throw new Exception(translate_str_by_id(5635));
					}
				}
				
				// Пишем лог
				if( $db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?, ?, ?, ?, ?);')->execute( array($order_id, $time, DP_User::getAdminId(), 1, translate_str_by_id(5636).' '.$new_order_item_id.' '.translate_str_by_id(5637).'  ID '.$item_id.'. '.translate_str_by_id(5638).' '.$item_info['count_need'].' '.translate_str_by_id(5639).' '.(((int)$item_info['count_need']) - ((int)$_GET["count"]))) ) != true )
				{
					throw new Exception(translate_str_by_id(5633)." 2");
				}
				
				// Изменяем id позиции в исходном массиве что бы произвести дальнейшие действия над созданной позицией
				$orders_items = array($new_order_item_id);
				
			}else{
				throw new Exception(translate_str_by_id(5640));
			}
		}
	}
}
catch (Exception $e)
{
	$db_link->rollBack();//Откатываем все изменения и закрываем транзакцию
	
	$result = array();
	$result["status"] = false;
	$result["message"] = translate_str_by_id(2122).". ".$e->getMessage();
	exit(json_encode($result));
}

//Дошли сюда - значит все запросы выполнены без ошибок
$db_link->commit();//Коммитим все изменения и закрываем транзакцию

// -----------------------------------------------------------------------------------------------------------



//ДАЛЕЕ САМ АЛГОРИТМ
// -----------------------------------------------------------------------------------------------------------
//0 Получаем список заказов по данным позициям:
$SQL_IN = "";
$binding_values = array();
for($i=0; $i < count($orders_items); $i++)
{
    if($i > 0) $SQL_IN .= ",";
    $SQL_IN .= '?';
	
	array_push($binding_values, $orders_items[$i]);
}
$SQL_IN = '('.$SQL_IN.')';
$orders_query = $db_link->prepare('SELECT DISTINCT(`order_id`), `id` FROM `shop_orders_items` WHERE `id` IN '.$SQL_IN.';');
$orders_query->execute($binding_values);
$orders = array();
while( $order = $orders_query->fetch() )
{
    array_push($orders, $order["order_id"]);
}
// -----------------------------------------------------------------------------------------------------------
//Проверка состояния оплаты заказов. Если заказ имеет состояние "Оплачен" или "Частично оплачен", то его позициям нельзя устанавливать статус, исключающий эти позиции из подсчета суммы заказов.
//Если идет установка статуса позиций, исключающего их из суммы заказа
if( array_search($status, $orders_items_statuses_not_count) !== false )
{
	//Проверка наличия заказов, по которым есть платежи
	$check_paid_query = $db_link->prepare( 'SELECT COUNT(*) FROM `shop_orders` WHERE `paid` != ? AND `id` IN ('.str_repeat('?,', count($orders)-1).'?);' );
	$check_paid_query->execute( array_merge( array(0), $orders ) );
	if( $check_paid_query->fetchColumn() > 0 )
	{	
		
		//===========================================================================================================================================================
		//===========================================================================================================================================================
			
			for($o=0; $o < count($orders); $o++)
			{
				// Позволяем сменить статус если сумма платежей по заказу меньше либо равна сумме заказа после отмены позиции
				
				$order_id = $orders[$o];
				
				//Подстрока с условиями фильтрования статусов позиций, которые не участвуют в ценовых расчетах
				$WHERE_statuses_not_count = "";
				for($i=0; $i<count($orders_items_statuses_not_count); $i++)
				{
					$WHERE_statuses_not_count .= " AND `status` != ".(int)$orders_items_statuses_not_count[$i];
				}
				
				//Для подсчета суммы оплаты по заказу
				$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)";
				$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
				
				
				//Для определения текущего баланса клиента
				$sub_balance_SQL = "";
				if( isset( $DP_Config->wholesaler ) )
				{
					$sub_balance_SQL = " AND `office_id` = `shop_orders`.`office_id` ";
				}
				$INCOME_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `user_id` = `shop_orders`.`user_id` ".$sub_balance_SQL." ), 0)";
				$ISSUE_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `user_id` = `shop_orders`.`user_id` ".$sub_balance_SQL." ),0)";
				
				
				//Получаем данные заказа
				$order_query = $db_link->prepare("SELECT *, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`, (SELECT `handler` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_handler`, CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) AS DECIMAL(10,2)) AS `price_sum`, CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(10,2) ) AS `paid_sum`, CAST( ($INCOME_USER_SQL - $ISSUE_USER_SQL) AS DECIMAL(10,2) ) AS `customer_balance`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) - ($ISSUE_SQL - $INCOME_SQL) ) AS DECIMAL(10,2) )  AS `paid_left` FROM `shop_orders` WHERE `id` = ?;");
				$order_query->execute( array($order_id, $order_id, $order_id, $order_id, $order_id) );
				$order = $order_query->fetch();
				
				if( $offices_list[$order["office_id"]] == NULL )
				{
					$result = array();
					$result["status"] = false;
					$result["message"] = translate_str_by_id(3504);
					exit(json_encode($result));
				}
				
				
				// Получаем сумму отменяемых позиций по конкретному заказу
				$SQL_IN_items = "";
				$binding_values_items = array();
				for($itm=0; $itm < count($orders_items); $itm++)
				{
					if($itm > 0) $SQL_IN_items .= ",";
					$SQL_IN_items .= '?';
					
					array_push($binding_values_items, $orders_items[$itm]);
				}
				
				array_push($binding_values_items, $order_id);
				
				$orders_query_items = $db_link->prepare('SELECT SUM(`price`*`count_need`) AS `price_otmena` FROM `shop_orders_items` WHERE `id` IN('.$SQL_IN_items.') AND `order_id` = ?');
				$orders_query_items->execute($binding_values_items);
				$record_items = $orders_query_items->fetch();
				
				// Из текущей общей суммы заказа вычитаем сумму отменяемых позиций и проверяем что бы полученное значение не было меньше уплаченной суммы платежей по заказу
				if( ($order["price_sum"] - $record_items['price_otmena']) < $order["paid_sum"]){
					
					$price_otmena = $record_items['price_otmena'];
					if($record_items['price_otmena'] > $order["paid_sum"]){
						$price_otmena = $order["paid_sum"];
						if( ($order["paid_sum"] - ($order["price_sum"] - $record_items['price_otmena'])) > 0 ){
							$price_otmena = ($order["paid_sum"] - ($order["price_sum"] - $record_items['price_otmena']));
						}
					}
					
					$direct_refund = 0;
					if( $order['user_id'] == 0 )
					{
						$direct_refund = 1;
					}
					
					//Делаем возврат (добавляем приходную операцию с заданным order_id)
					if( ! $db_link->prepare('INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`) VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) );')->execute( array($order['user_id'], time(), 1, $price_otmena, '5_refund_from_order_to_balance', 1, $order['id'], $order['id']) ) )
					{
						throw new Exception(translate_str_by_id(3488));
					}
					
					
					//Меняем paid в заказе на 0 (Не оплачен)
					if( ! $db_link->prepare('UPDATE `shop_orders` SET `paid`=? WHERE `id` = ?;')->execute( array(0, $order_id) ) )
					{
						throw new Exception(translate_str_by_id(3489));
					}
					
					//Если direct_refund==1, то добавляем расходную операцию на баланс клиента (выдача денег с баланса)
					if( $direct_refund )
					{
						if( ! $db_link->prepare('INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`) VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) );')->execute( array($order['user_id'], time(), 0, $price_otmena, '6_refund_from_balance', 1, 0, $order['id']) ) )
						{
							throw new Exception(translate_str_by_id(3490));
						}
					}
					
					//Пишем лог заказа (произведен возврат)
					$log_text = translate_str_by_id(3492).' <b>'.$price_otmena.'</b> ('.translate_str_by_id(4642).')';
					if( $direct_refund )
					{
						$log_text = translate_str_by_id(3492).' <b>'.$price_otmena.'</b> ('.translate_str_by_id(3493).')';
					}
					if( !$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order['id'], time(), 0, 0, $log_text, 1) ) )
					{
						throw new Exception(translate_str_by_id(3494));
					}
					
				}
				
				//Получаем данные заказа
				$order_query = $db_link->prepare("SELECT *, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`, (SELECT `handler` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_handler`, CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) AS DECIMAL(10,2)) AS `price_sum`, CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(10,2) ) AS `paid_sum`, CAST( ($INCOME_USER_SQL - $ISSUE_USER_SQL) AS DECIMAL(10,2) ) AS `customer_balance`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) - ($ISSUE_SQL - $INCOME_SQL) ) AS DECIMAL(10,2) )  AS `paid_left` FROM `shop_orders` WHERE `id` = ?;");
				$order_query->execute( array($order_id, $order_id, $order_id, $order_id, $order_id) );
				$order = $order_query->fetch();
				
				// Если остаток задолженности по заказу равен 0 то заказ оплачен
				if( (($order["price_sum"] - $record_items['price_otmena'] - $order["paid_sum"]) == 0) && ($order["paid_sum"] > 0) ){
					//Записываем статус оплаты в заказ
					if( ! $db_link->prepare('UPDATE `shop_orders` SET `paid`=? WHERE `id` = ?;')->execute( array(1, $order_id) ) )
					{
						throw new Exception(translate_str_by_id(3489));
					}
				}else{
					// Если есть платежи по заказу тогда он частично оплачен
					if($order["paid_sum"] > 0){
						//Записываем статус оплаты в заказ
						if( ! $db_link->prepare('UPDATE `shop_orders` SET `paid`=? WHERE `id` = ?;')->execute( array(2, $order_id) ) )
						{
							throw new Exception(translate_str_by_id(3489));
						}
					}
				}
				
			}
			
		//===========================================================================================================================================================
		//===========================================================================================================================================================
		
		/*
		$result = array();
		$result["status"] = false;
		$result["message"] = "Данный статус нельзя назначать позициям заказов, которые Оплачены, либо Частично оплачены";
		exit(json_encode($result));
		*/
	}
}
// -----------------------------------------------------------------------------------------------------------
//1. Массив покупателей и менеджеров по заказам
$orders_data = array();//Ассоциативный массив Заказ=>[покупатель, офис]
for($i=0; $i < count($orders); $i++)
{
    //1. Получаем информацию по заказам:
	$order_query = $db_link->prepare('SELECT `user_id` AS `customer`, `status`, `paid`, `email_not_auth`, `phone_not_auth`, (SELECT `users` FROM `shop_offices` WHERE `id`=`shop_orders`.`office_id`) AS `managers` FROM `shop_orders` WHERE `id`= ?;');
	$order_query->execute( array($orders[$i]) );
    $order_record = $order_query->fetch();
    $orders_data[$orders[$i]] = array("customer"=>$order_record["customer"], "status"=>$order_record["status"], "paid"=>$order_record["paid"], "managers"=>json_decode($order_record["managers"], true), "email_not_auth"=>$order_record["email_not_auth"], "phone_not_auth"=>$order_record["phone_not_auth"]); 
	
	//2. Список конкретных позициий данного заказа для которых был изменен статус
	$orders_items_query = $db_link->prepare('SELECT `id` FROM `shop_orders_items` WHERE `id` IN '.$SQL_IN.' AND `order_id` = '.$orders[$i].';');
	$orders_items_query->execute($binding_values);
	$items = '';
	while( $order_item_record = $orders_items_query->fetch() )
	{
		if($items != ''){
			$items .= ', ';
		}
		$items .= $order_item_record['id'];
	}
	$orders_data[$orders[$i]]['item_id'] = $items;
}

// -----------------------------------------------------------------------------------------------------------



//2.1 Операции с товаром (возврат на склад / списание со склада) ТОЛЬКО ДЛЯ ТОВАРОВ С ТИПОМ 1
//Сначала определяем - нужно ли делать выдачу товара или отмену позиции
$status_flags_query = $db_link->prepare('SELECT `count_flag`, `issue_flag` FROM `shop_orders_items_statuses_ref` WHERE `id` = ?;');
$status_flags_query->execute( array($status) );
$status_flags_record = $status_flags_query->fetch();
$count_flag = $status_flags_record["count_flag"];//Флаг - нужно ли учитывать товар при расчете суммы заказа
$issue_flag = $status_flags_record["issue_flag"];//Флаг - товар выдать покупателю

//Далее проверяем тип товаров в нужных позициях.
for($i=0; $i < count($orders_items); $i++)
{
	//Получаем данные по позиции
	$product_type_query = $db_link->prepare('SELECT `product_type`, (SELECT `count_flag` FROM `shop_orders_items_statuses_ref` WHERE `id` = `shop_orders_items`.`status`) AS `count_flag_current`, (SELECT SUM(`count_issued`) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`) AS `previously_issued` FROM `shop_orders_items` WHERE `id` = ?;');
	$product_type_query->execute( array($orders_items[$i]) );
	$product_type_record = $product_type_query->fetch();
	
	
	//Актуально только для типа продукта = 1
	if( $product_type_record["product_type"] == 1 )
	{
		//Определяем флаг "Позиция была отмене ранее"
		if($product_type_record["count_flag_current"] == 1)
		{
			$previously_canceled = 0;//Позицию не отменяли
		}
		else
		{
			$previously_canceled = 1;//Позиция уже отменена
		}
		
		//Определяем флаг "Товар уже был выдан покупателю" (точне не флаг, а количество товара, которое уже было отпущено)
		$previously_issued = $product_type_record["previously_issued"];
		
		
		//Получаем перечень детализированных записей позиции
		$details_records = array();
		$details_records_query = $db_link->prepare('SELECT `id` FROM `shop_orders_items_details` WHERE `order_item_id` = ?;');
		$details_records_query->execute( array($orders_items[$i]) );
		while( $detail_record = $details_records_query->fetch() )
		{
			array_push($details_records, $detail_record["id"]);
		}
		
		
		//Далее идут действия:
		
		//Нужно выполнить действие - Выдать товар покупателю со склада
		if($issue_flag)
		{
			//Позиция не была ранее отменена
			if( ! $previously_canceled )
			{
				//Товар еще не был выдан (OK - GOOD)
				if($previously_issued == 0) 
				{
					//ВЫДАЕМ
					//Склады - количество из "Зарезервировано" перетекает в "Отпущено"
					for( $d=0; $d < count($details_records); $d++ )
					{
						$db_link->prepare('UPDATE `shop_storages_data` SET `issued` = `issued` + (SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `id`=?), `reserved` = `reserved` - (SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `id`=?) WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `id`=?)')->execute( array($details_records[$d], $details_records[$d],$details_records[$d]) );
					}
					//Детальные записи заказа - из "Зарезервировано" перетекает в "Отпущено"
					for( $d=0; $d < count($details_records); $d++ )
					{
						$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_issued` = `count_reserved` WHERE `id` = ?')->execute( array($details_records[$d]) );
						
						$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_reserved` = 0 WHERE `id` = ?')->execute( array($details_records[$d]) );
					}
				}
				else//Товар уже выдан
				{
					//Ничего не делаем
				}
			}
			else//Позиция была отменена (OK - GOOD)
			{
				//ВЫДАЕМ
				//Склады - количество из "Наличие" перетекает в "Отпущено"
				for( $d=0; $d < count($details_records); $d++ )
				{
					$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist` - (SELECT `count_canceled` FROM `shop_orders_items_details` WHERE `id`=?), `issued` = `issued` + (SELECT `count_canceled` FROM `shop_orders_items_details` WHERE `id`=?) WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `id`=?)')->execute( array($details_records[$d], $details_records[$d], $details_records[$d]) );
				}
				//Детальные записи заказа - колонка "Отпущено" инициализируется количеством, а количество "Отменено" становится равным 0
				for( $d=0; $d < count($details_records); $d++ )
				{
					$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_issued` = `count_canceled` WHERE `id` = ?')->execute( array($details_records[$d]) );
					
					$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_canceled` = 0 WHERE `id` = ?')->execute( array($details_records[$d]) );
				}
			}
		}
		//Нужно выполнить действие - Вернуть товар на склад
		else if( ! $count_flag )
		{
			//Позиция не была ранее отменена
			if( ! $previously_canceled )
			{
				//Товар еще не был выдан (OK - GOOD)
				if($previously_issued == 0)
				{
					//Возвращаем товар на склад (снимаем с резервирования)
					//Склады - количество из "Зарезервировано" перетекает в "Наличие"
					for( $d=0; $d < count($details_records); $d++ )
					{
						$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist` + (SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `id`=?), `reserved` = `reserved` - (SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `id`=?) WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `id`=?)')->execute( array($details_records[$d], $details_records[$d], $details_records[$d]) );
					}
					//Детальные записи заказа - "Зарезервировано" ставим 0, а "Отменено" - указываем количество
					for( $d=0; $d < count($details_records); $d++ )
					{
						$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_canceled` = `count_reserved` WHERE `id` = ?')->execute( array($details_records[$d]) );

						$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_reserved` = 0 WHERE `id` = ?')->execute( array($details_records[$d]) );
					}
				}
				else//Товар был выдан (OK - GOOD)
				{
					//Возвращаем товар на склад
					//Склады - количество из "Отпущено" перетекает в "Наличие"
					for( $d=0; $d < count($details_records); $d++ )
					{
						$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist` + (SELECT `count_issued` FROM `shop_orders_items_details` WHERE `id`=?), `issued` = `issued` - (SELECT `count_issued` FROM `shop_orders_items_details` WHERE `id`=?) WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `id`=?)')->execute( array($details_records[$d], $details_records[$d],$details_records[$d]) );
					}
					//Детальные записи заказа - "Отпущено" ставим 0, а "Отменено" - указываем количество
					for( $d=0; $d < count($details_records); $d++ )
					{
						$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_canceled` = `count_issued` WHERE `id` = ?')->execute( array($details_records[$d]) );
						
						$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_issued` = 0 WHERE `id` = ?')->execute( array($details_records[$d]) );
					}
				}
			}
			else//Позиция уже была отменена
			{
				//Ничего не делаем
			}
		}
		else if( $count_flag )//Менеджер установил статус, при котором товар должен быть зарезервирован
		{
			//Этот блок выполняется только в том случае, если позиция в данный момент числится, как отмененная
			if( $previously_canceled )
			{
				//Склады - количество из "Наличие" перетекает в "Зарезервирован"
				for( $d=0; $d < count($details_records); $d++ )
				{
					$db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist` - (SELECT `count_canceled` FROM `shop_orders_items_details` WHERE `id`=?), `reserved` = `reserved` + (SELECT `count_canceled` FROM `shop_orders_items_details` WHERE `id`=?) WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `id`=?)')->execute( array($details_records[$d],$details_records[$d],$details_records[$d]) );
				}
				//Детальные записи заказа - "Отменено" ставим 0, а "Зарезервирован" - указываем количество
				for( $d=0; $d < count($details_records); $d++ )
				{
					$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_reserved` = `count_canceled` WHERE `id` = ?')->execute( array($details_records[$d]) );
					
					$db_link->prepare('UPDATE `shop_orders_items_details` SET `count_canceled` = 0 WHERE `id` = ?')->execute( array($details_records[$d]) );
				}
			}
		}
	}
}

// -----------------------------------------------------------------------------------------------------------

//3. Меняем статус позиции

array_unshift($binding_values, $status);
if( $db_link->prepare("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` IN $SQL_IN;")->execute( $binding_values ) != true)
{
    $result["status"] = false;
    $result["message"] = "SQL error";
    $result["code"] = 701;
    exit(json_encode($result));
}

// -----------------------------------------------------------------------------------------------------------

//==============================================================================================================================================
//==============================================================================================================================================

// Если заказ оплачен, проверяем после семены статуса сумму задолженности по заказу и если она есть то сменяем статус на частично оплачен
foreach($orders_data as $order_id=>$data)
{
	//Подстрока с условиями фильтрования статусов позиций, которые не участвуют в ценовых расчетах
	$WHERE_statuses_not_count = "";
	for($i=0; $i<count($orders_items_statuses_not_count); $i++)
	{
		$WHERE_statuses_not_count .= " AND `status` != ".(int)$orders_items_statuses_not_count[$i];
	}
	//Получаем данные заказа
	//Сколько уже оплачено по заказу
	$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)";
	$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
	//Баланс клиента
	$office_SQL = "";
	$office_SQL_values = array();
	if( isset( $DP_Config->wholesaler ) )
	{
		$office_SQL = " AND `office_id` = (SELECT `office_id` FROM `shop_orders` WHERE `id` = ?) ";
		$office_SQL_values = array($order_id, $order_id);
	}
	$INCOME_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `user_id` = `shop_orders`.`user_id` ".$office_SQL." ), 0)";
	$ISSUE_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `user_id` = `shop_orders`.`user_id` ".$office_SQL." ),0)";
	$order_query = $db_link->prepare("SELECT *, CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(10,2) ) AS `paid_sum`, CAST( ($INCOME_USER_SQL - $ISSUE_USER_SQL) AS DECIMAL(10,2) ) AS `customer_balance`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) - ($ISSUE_SQL - $INCOME_SQL) ) AS DECIMAL(10,2) )  AS `paid_left` FROM `shop_orders` WHERE `id` = ?;");
	$order_query->execute( array_merge( array($order_id, $order_id, $order_id, $order_id, $order_id), $office_SQL_values)  );
	$order = $order_query->fetch();
	if( $order == false )
	{
		throw new Exception("Forbidden");
	}
	
	// Заказ оплачен но образовалась задолженность, тогда снимаем оплату
	if( $order['paid'] == 1 && $order['paid_left'] > 0 )
	{
		if( $order['paid_sum'] == 0 )
		{
			$new_paid_status = 0;//Теперь заказ не оплачен
		}
		else if( $order['paid_sum'] > 0 )
		{
			$new_paid_status = 2;//Заказ частично оплачен
		}
		
		//Записываем статус оплаты в заказ
		if( ! $db_link->prepare('UPDATE `shop_orders` SET `paid`=? WHERE `id` = ?;')->execute( array($new_paid_status, $order_id) ) )
		{
			throw new Exception(translate_str_by_id(3489));
		}
	}
}

//==============================================================================================================================================
//==============================================================================================================================================

// -----------------------------------------------------------------------------------------------------------

//4. Уведомления
foreach($orders_data as $order_id=>$data)
{
    //4.1 ДЛЯ МЕНЕДЖЕРОВ
	$persons = array();
    for($i=0; $i < count($data["managers"]); $i++)
    {
		//Проверяем что пользователь являеться менеджером
		if( ! DP_User::isBackendGroupById($data["managers"][$i]) ){
			continue;
		}
		$persons[] = array('type'=>'user_id', 'user_id'=>$data["managers"][$i]);
    }
	
	//Формируем полный текст позиций заказа
	$order_text = '';
	include( $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_manager.php' );
	
	//Переменные для уведомления
	$notify_vars = array();
	$notify_vars['order_id'] = $order_id;
	$notify_vars['item_id'] = $data['item_id'];
	$notify_vars['status_name'] = translate_str_by_id($orders_items_statuses[$status]["name"]);
	$notify_vars['status_ref'] = $orders_items_statuses[$status];//Этой переменной нет в спецификации уведомления. Но, она используется для учета настроек отправки по разным статусам
	$notify_vars['order_text'] = $order_text;
	
	//Отправляем уведомление (БЕЗ обработки результата)
	send_notify('order_item_status_to_manager', $notify_vars, $persons, false);
		
    
	
    //4.2 ДЛЯ ПОКУПАТЕЛЯ
	$persons = array();
    if( $data["customer"] > 0 )
    {
        $persons[] = array( 'type'=>'user_id', 'user_id'=>$data["customer"] );
    }
	else
	{
		$persons[] = array(
			'type'=>'direct_contact',
			'contacts'=>array(
					'email'=>array('value'=>$data["email_not_auth"]),
					'phone'=>array('value'=>$data["phone_not_auth"])
				)
			);
	}
	
	//Формируем полный текст позиций заказа
	$order_text = '';
	include( $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_user.php' );
	
	//Переменные для уведомления
	$notify_vars = array();
	$notify_vars['order_id'] = $order_id;
	$notify_vars['item_id'] = $data['item_id'];
	$notify_vars['status_name'] = translate_str_by_id($orders_items_statuses[$status]["name"]);
	$notify_vars['status_ref'] = $orders_items_statuses[$status];//Этой переменной нет в спецификации уведомления. Но, она используется для учета настроек отправки по разным статусам
	$notify_vars['order_text'] = $order_text;
	
	//Отправляем уведомление (БЕЗ обработки результата)
	send_notify('order_item_status_to_customer', $notify_vars, $persons, false);
}

// -----------------------------------------------------------------------------------------------------------

//ЗАПИСЬ ИСТОРИИ ДЕЙСТВИЙ С ЗАКАЗАМИ
if($initiator == 2)
{
	$is_manager = 0;
	$user_id = 0;
	$is_robot = 1;
}
else if($initiator == 1)
{
	$is_manager = 1;
	$is_robot = 0;
	$user_id = DP_User::getAdminId();
}
else 
{
	$is_manager = 0;
	$is_robot = 0;
	$user_id = DP_User::getUserId();
}
$orders_items_to_order_id_array = array();
for($i=0; $i < count($orders_items); $i++)
{
	$order_id_query = $db_link->prepare('SELECT `order_id` FROM `shop_orders_items` WHERE `id`= ?;');
	$order_id_query->execute( array($orders_items[$i]) );
	$order_id_record = $order_id_query->fetch();
	
	$order_id = $order_id_record["order_id"];
	
	//Пишем лог заказа
	$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order_id,time(),$user_id,$is_manager, translate_str_by_id(4569).' '.$orders_items[$i].' '.translate_str_by_id(4570).' <b>'.translate_str_by_id($orders_items_statuses[$status]["name"]).'</b>',$is_robot) );
}


// -------------------------------------------------------------------------------------------

//АВТОМАТИЧЕСКАЯ СМЕНА СТАТУСОВ ПО УСЛОВИЯМ
if($initiator == 1)// Только когда идет прямой запрос от менеджера
{
	//Если все позиции заказа имеют статус "Отмена" или "Выдано", при этом должна быть хотябы одна позиция в статусе "Выдано" - ставим статус заказа "Выдан" - то есть работа с заказом завершена
	foreach($orders_data as $order_id=>$data)
	{
		//Получаем список статусов с флагом "for_finish" - заказ выполнен
		$orders_statuses_for_finish = array();
		$orders_statuses_query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1;");
		$orders_statuses_query->execute();
		while($status_row = $orders_statuses_query->fetch() )
		{
			$orders_statuses_for_finish[] = $status_row["id"];
		}
		
		//Проверяем что бы заказ уже не был в этом статусе
		if( !empty($orders_statuses_for_finish) && !in_array($data['status'], $orders_statuses_for_finish) ){
			
			//Получаем количественные показатели по заказу
			$SQL = "SELECT 
			
			COUNT(*) AS 'all', 
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN(SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1)) AS 'finish', 
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN(SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0)) AS 'inverse' 
			
			FROM `shop_orders_items` WHERE `order_id` = ?;";
			
			$query = $db_link->prepare($SQL);
			$query->execute( array($order_id, $order_id, $order_id) );
			$row = $query->fetch(PDO::FETCH_ASSOC);
			
			if( ($row['finish'] > 0) && ($row['all'] == ($row['finish'] + $row['inverse'])) ){
				//Меняем статус заказа - только при условии что он полностью оплачен
				if($data['paid'] == 1){
					$curl = curl_init();
					curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/protocol/set_order_status.php?initiator=4&key=".urlencode($DP_Config->tech_key)."&status=".$orders_statuses_for_finish[0]."&orders=".json_encode(array($order_id)));
					curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
					curl_setopt($curl, CURLOPT_POST, false);
					curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
					curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
					curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 1); 
					curl_setopt($curl, CURLOPT_TIMEOUT, 1);
					curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
					$curl_result = curl_exec($curl);
					curl_close($curl);
				}
			}
			
		}
	}

	// ...........

	//Если все позиции заказа имеют статус "Отмена" - ставим статус заказа "Отмена" - то есть заказ полностью отменен
	foreach($orders_data as $order_id=>$data)
	{
		//Получаем список статусов заказа с флагом "for_inverse" - заказ отменен
		$orders_statuses_for_inverse = array();
		$orders_statuses_query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1;");
		$orders_statuses_query->execute();
		while($status_row = $orders_statuses_query->fetch() )
		{
			$orders_statuses_for_inverse[] = $status_row["id"];
		}
		
		//Проверяем что бы заказ уже не был в этом статусе
		if( !empty($orders_statuses_for_inverse) && !in_array($data['status'], $orders_statuses_for_inverse) ){
			
			//Получаем количественные показатели по заказу
			$SQL = "SELECT 
			
			COUNT(*) AS 'all', 
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN(SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0)) AS 'inverse' 
			
			FROM `shop_orders_items` WHERE `order_id` = ?;";
			
			$query = $db_link->prepare($SQL);
			$query->execute( array($order_id, $order_id) );
			$row = $query->fetch(PDO::FETCH_ASSOC);
			
			if( ($row['all'] > 0) && ($row['all'] == $row['inverse']) ){
				//Меняем статус заказа
				$curl = curl_init();
				curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/protocol/set_order_status.php?initiator=4&key=".urlencode($DP_Config->tech_key)."&status=".$orders_statuses_for_inverse[0]."&orders=".json_encode(array($order_id)));
				curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
				curl_setopt($curl, CURLOPT_POST, false);
				curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
				curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
				curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 1); 
				curl_setopt($curl, CURLOPT_TIMEOUT, 1);
				curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
				$curl_result = curl_exec($curl);
				curl_close($curl);
			}
			
		}
	}

	// ...........

	//Если заказ имеет статус "Отменен" или "Выполнен", при этом существует позиция в любом другом статусе кроме "Выдана" и "Отменена" - ставим статус заказа "Комплектуется" - то есть в работе, значит он еще не завершен и не отменен до конца
	foreach($orders_data as $order_id=>$data)
	{
		//Получаем список статусов заказа с флагом "for_finish" - заказ выполнен
		$orders_statuses_for_finish = array();
		$orders_statuses_query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1;");
		$orders_statuses_query->execute();
		while($status_row = $orders_statuses_query->fetch() )
		{
			$orders_statuses_for_finish[] = $status_row["id"];
		}
		
		//Получаем список статусов заказа с флагом "for_inverse" - заказ отменен
		$orders_statuses_for_inverse = array();
		$orders_statuses_query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1;");
		$orders_statuses_query->execute();
		while($status_row = $orders_statuses_query->fetch() )
		{
			$orders_statuses_for_inverse[] = $status_row["id"];
		}
		
		//Получаем список статусов заказа с флагом "for_paid" - заказ в работе
		$orders_statuses_for_paid = array();
		$orders_statuses_query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1;");
		$orders_statuses_query->execute();
		while($status_row = $orders_statuses_query->fetch() )
		{
			$orders_statuses_for_paid[] = $status_row["id"];
		}
		
		//Проверяем что бы заказ уже не был в этом статусе
		if( !empty($orders_statuses_for_paid) && !in_array($data['status'], $orders_statuses_for_paid) ){
			
			//Проверяем что заказ находится в статусе "Отменен" или "Выполнен"
			if( in_array($data['status'], $orders_statuses_for_finish) || in_array($data['status'], $orders_statuses_for_inverse) ){
					
				//Получаем количественные показатели по заказу
				$SQL = "SELECT 
				
				COUNT(*) AS 'all', 
				(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN(SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1)) AS 'finish', 
				(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN(SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0)) AS 'inverse' 
				
				FROM `shop_orders_items` WHERE `order_id` = ?;";
				
				$query = $db_link->prepare($SQL);
				$query->execute( array($order_id, $order_id, $order_id) );
				$row = $query->fetch(PDO::FETCH_ASSOC);
				
				
				if( ($row['all'] > 0) && ($row['all'] != ($row['finish'] + $row['inverse'])) ){
					//Меняем статус заказа
					$curl = curl_init();
					curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/protocol/set_order_status.php?initiator=4&key=".urlencode($DP_Config->tech_key)."&status=".$orders_statuses_for_paid[0]."&orders=".json_encode(array($order_id)));
					curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
					curl_setopt($curl, CURLOPT_POST, false);
					curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
					curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
					curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 1); 
					curl_setopt($curl, CURLOPT_TIMEOUT, 1);
					curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
					$curl_result = curl_exec($curl);
					curl_close($curl);
				}
				
			}
		}
	}
}

// -------------------------------------------------------------------------------------------

//5. Выдаем ответ (JSON)
$result["status"] = true;
exit(json_encode($result));
?>