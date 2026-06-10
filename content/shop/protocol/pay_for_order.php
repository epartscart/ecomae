<?php
/**
 * Скрипт привязки платежей к заказу.
 * 
 * // Технически:
 * // - флаг Оплачен выставляется в true;
 * // - отправляется уведомление покупателю и менеджеру;
 * // - меняется статус заказа;
 * // - отпускаем со склада товар.
 * 
 * Скрипт вызывается, когда:
 * - менеджер того офиса, к которому относится заказ, добавляет платеж к заказу
 * - покупатель сам оплачивает свой заказ с баланса
 * - прямая оплата через сайт покупателем. В этом случае, скрипт вызывается платежной системой автоматически.
 * 
 * Инициаторы:
 * 1 - менеджер
 * 2 - покупатель
 * 3 - скрипт платежной системы
*/
header('Content-Type: application/json;charset=utf-8;');
//Проверки перед началом работы
if( !isset($_GET["initiator"]) || !isset($_GET["order_id"]) || !isset($_GET["pay_sum"]) || !isset($_GET["direct_pay"]) )
{
	exit();
}
//Откуда вызов:
if($_GET["initiator"] != 1 && $_GET["initiator"] != 2 && $_GET["initiator"] != 3)
{
	exit();
}
if( $_GET["direct_pay"] != 1 && $_GET["direct_pay"] != 0 )
{
	exit();
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
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



if( $_GET["initiator"] == 1 || $_GET["initiator"] == 2 )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
}



//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

//Для отправки уведомлений
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );



//Входные данные:
$initiator = $_GET["initiator"];//(1 - менеджер, 2 - покупатель, 3 - платежная система)
$order_id = $_GET["order_id"];//Номер заказа
$pay_sum = $_GET["pay_sum"];//Сумма платежа
$direct_pay = $_GET["direct_pay"];//Флаг "Прямая оплата". (если 0, значит оплата с текущего баланса клиента)




//Все действия делаем через транзакцию
try
{
	//Старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception(translate_str_by_id(2132));
	}
	
	if( $pay_sum <= 0 )
	{
		throw new Exception("Forbidden");
	}
	
	
	
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
	$order_query = $db_link->prepare("SELECT *, CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(8,2) ) AS `paid_sum`, CAST( ($INCOME_USER_SQL - $ISSUE_USER_SQL) AS DECIMAL(8,2) ) AS `customer_balance`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) - ($ISSUE_SQL - $INCOME_SQL) ) AS DECIMAL(8,2) )  AS `paid_left` FROM `shop_orders` WHERE `id` = ?;");
	$order_query->execute( array_merge( array($order_id, $order_id, $order_id, $order_id, $order_id), $office_SQL_values)  );
	$order = $order_query->fetch();
	if( $order == false )
	{
		throw new Exception("Forbidden");
	}
	
	
	//Получаем сумму заказа:
	$binding_values = array();
	array_push($binding_values, $order_id);
	$WHERE_COUNT_STATUS = "";
	for($i=0; $i < count($orders_items_statuses_not_count); $i++)
	{
		$WHERE_COUNT_STATUS .= " AND `status` != ?";
		
		array_push($binding_values, $orders_items_statuses_not_count[$i]);
	}
	$order_sum_query = $db_link->prepare('SELECT CAST( SUM(`price`*`count_need`) AS DECIMAL(8,2) ) AS `order_sum` FROM `shop_orders_items` WHERE `order_id` = ? '.$WHERE_COUNT_STATUS.';');
	$order_sum_query->execute( $binding_values );
	$order_sum_record = $order_sum_query->fetch();
	if( $order_sum_record == false )
	{
		throw new Exception("Forbidden");
	}
	$order_sum = $order_sum_record["order_sum"];
	
	
	//Проверки:
	//Заказ не должен иметь состояние "Полностью оплачен" (paid 1)
	if( $order['paid'] == 1 )
	{
		throw new Exception("Forbidden");
	}
	//Сумма создаваемого платежа не должна превышать сумму долга по заказу
	if( $pay_sum > $order['paid_left'] )
	{
		throw new Exception("Forbidden");
	}
	
	//Далее - проверки в зависимости от инициатора
	if($initiator == 1)
	{
		//Проверяем право менеджера
		if( ! DP_User::isAdmin())
		{
			throw new Exception("Forbidden");
		}
		//Теперь проверяем право работать с заказами данного офиса обслуживания
		$manager_id = DP_User::getAdminId();
		$office_id = $order["office_id"];
		$office_access_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ?;');
		$office_access_query->execute( array($office_id, '%'.$manager_id.'%') );
		if( $office_access_query->fetchColumn() == 0)
		{
			throw new Exception("Forbidden");
		}
		
		//Если оплата с баланса клиента (direct_pay == 0), проверяем, что клиент авторизован
		if( $direct_pay == 0 && $order['user_id'] == 0 )
		{
			throw new Exception("Forbidden");
		}
	}
	//Инициатор - клиент (оплачивает со своего баланса)
	else if($initiator == 2)
	{
		$customer_id = DP_User::getUserId();
		
		if( $customer_id == 0 )
		{
			throw new Exception("Forbidden");
		}
		if( $order['user_id'] != $customer_id )
		{
			throw new Exception("Forbidden");
		}
		
		//Если платеж - частичный
		if( $pay_sum < $order['paid_left'] )
		{
			//Проверяем, что функция частичной оплаты включена
			if( $DP_Config->partial_payment != true )
			{
				throw new Exception("Forbidden");
			}
			
			//Платеж должен быть не ниже минимально-допустимой оплаты в % от заказа
			if( $pay_sum < $order_sum*($DP_Config->partial_payment_min_percent/100) )
			{
				throw new Exception("Forbidden");
			}
		}
		
		
		//Если денег на балансе клиента не достаточно, то проверяем настройки овердрафта
		if( $pay_sum > $order['customer_balance'] )
		{
			//Включен ли овердрафт
			if( $DP_Config->client_overdraft != true )
			{
				throw new Exception("Forbidden");
			}
			
			//Размер допустимого овердрафта
			if( ($pay_sum - $order['customer_balance']) > (int)$DP_Config->client_overdraft_value )
			{
				if( (int)$DP_Config->client_overdraft_value > 0 )
				{
					throw new Exception("Forbidden");
				}
			}
		}
	}
	//Вызов из скрипта платежной системы
	else if($initiator == 3)
	{
		//Проверям ключ запуска скриптов
		if($_GET["code"] != $DP_Config->tech_key)
		{
			throw new Exception("Forbidden");
		}
		
		//Другие проверки в этом случае не требуются, т.к. они уже были ранее в других скриптах в процессе работы платежной системы.
	}

	
	
	//ПРОВЕРКИ - OK. ДАЛЕЕ - ЗАПИСЫВАЕМ ОПЛАТУ.
	
	//Если инициатор - продавец и идет прямая оплата заказа, то, необходимо сначала добавить приход на баланс клиента
	if( $initiator == 1 && $direct_pay )
	{
		if( ! $db_link->prepare('INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `pay_orders`, `office_id`) VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?,  (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) );')->execute( array($order['user_id'], time(), 1, $pay_sum, '2_income_for_direct_pay', 1, $order['id'], $order['id']) ) )
		{
			throw new Exception(translate_str_by_id(3488));
		}
	}
	
	
	//Добавляем расходную операцию на оплату заказа
	if( ! $db_link->prepare('INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`) VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) );')->execute( array($order['user_id'], time(), 0, $pay_sum, '1_pay_for_order', 1, $order['id'], $order['id']) ) )
	{
		throw new Exception(translate_str_by_id(3490));
	}
	$pay_ledger_id = (int)$db_link->lastInsertId();
	$uae_vat_file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
	if ($pay_ledger_id > 0 && is_file($uae_vat_file)) {
		require_once $uae_vat_file;
		epc_uae_vat_record_advance_on_payment($db_link, $pay_ledger_id, (int)$order['id'], (float)$pay_sum, time());
	}

	
	//Получаем остаток задолженности по заказу теперь.
	$paid_left_new = $order['paid_left'] - $pay_sum;
	if( $paid_left_new == 0 )
	{
		$new_paid_status = 1;//Теперь заказ полностью оплачен
		$new_paid_status_text = translate_str_by_id(3584);
	}
	else if( $paid_left_new > 0 )
	{
		$new_paid_status = 2;//Заказ частично оплачен
		$new_paid_status_text = translate_str_by_id(3515);
	}
	else
	{
		//Что-то пошло не так
		throw new Exception(translate_str_by_id(4565));
	}
	//Записываем статус оплаты в заказ
	if( ! $db_link->prepare('UPDATE `shop_orders` SET `paid`=? WHERE `id` = ?;')->execute( array($new_paid_status, $order_id) ) )
	{
		throw new Exception(translate_str_by_id(3489));
	}
	
	//Пишем лог заказа
	$log_user_id = 0;
	$log_is_manager = 0;
	$log_is_robot = 0;
	switch( $initiator )
	{
		case 1:
			$log_user_id = $manager_id;
			$log_is_manager = 1;
			$log_is_robot = 0;
			break;
		case 2:
			$log_user_id = $customer_id;
			$log_is_manager = 0;
			$log_is_robot = 0;
			break;
		default:
			$log_user_id = 0;
			$log_is_manager = 0;
			$log_is_robot = 1;
	}
	$log_text = translate_str_by_id(1316).'. '.translate_str_by_id(4366).' <b>'.$pay_sum.'</b><br/>'.translate_str_by_id(4529).': <b>'.$new_paid_status_text.'</b>';
	if( !$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order['id'], time(), $log_user_id, $log_is_manager, $log_text, $log_is_robot) ) )
	{
		throw new Exception(translate_str_by_id(3494));
	}
	
	
	//Здесь можем коммитит и закрывать транзакцию
	$db_link->commit();
	
	
	// --------------------------------------------------------------------------------------------------------------
	
	// Получаем список способов оплаты:
	$shop_orders_paid_type = array();
	$query = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;');
	$query->execute();
	while($rov = $query->fetch()){
		$shop_orders_paid_type[$rov['id']] = translate_str_by_id($rov['name']);
	}
	
	// Выставляем способ оплаты
	if($initiator == 1 && $direct_pay == 1){
		// Наличными в магазине (была нажата кнопка оплаты менеджером в панели управления без списания с баланса)
		$query = $db_link->prepare("UPDATE `shop_orders` SET `paid_type` = 1 WHERE `id` = ?;");
		$query->execute( array($order_id) );
		//Пишем лог заказа
		$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order_id, time(), $log_user_id, $log_is_manager, translate_str_by_id(4645).': <b>'.$shop_orders_paid_type[1].'</b>', $log_is_robot) );
	}else if($initiator == 3){
		// Онлайн по карте
		$query = $db_link->prepare("UPDATE `shop_orders` SET `paid_type` = 3 WHERE `id` = ?;");
		$query->execute( array($order_id) );
		//Пишем лог заказа
		$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order_id, time(), $log_user_id, $log_is_manager, translate_str_by_id(4645).': <b>'.$shop_orders_paid_type[3].'</b>', $log_is_robot) );
	}else{
		// С баланса
		$query = $db_link->prepare("UPDATE `shop_orders` SET `paid_type` = 2 WHERE `id` = ?;");
		$query->execute( array($order_id) );
		//Пишем лог заказа
		$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order_id, time(), $log_user_id, $log_is_manager, translate_str_by_id(4645).': <b>'.$shop_orders_paid_type[2].'</b>', $log_is_robot) );
	}
	
	// --------------------------------------------------------------------------------------------------------------
	//Далее - уведомления
	$office_id = $order["office_id"];
	
	//Настройки шаблона
	$templates = array();
	$templates_query = $db_link->prepare('SELECT * FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1;');
	$templates_query->execute();
	$templates = $templates_query->fetch();
	$templates = json_decode($templates['data_value'], true);
	$background_order_link = "#799658";
	if(!empty($templates['main_color'])){
		$background_order_link = $templates['main_color'];
	}
	
	//Для менеджера
	//Получаем список менеджеров офиса
	$managers_query = $db_link->prepare('SELECT `users` FROM `shop_offices` WHERE `id` = ?;');
	$managers_query->execute( array($office_id) );
	$managers_record = $managers_query->fetch();
	$managers = json_decode($managers_record["users"], true);
	$persons = array();
	for($i=0; $i < count($managers); $i++)
	{
		$persons[] = array('type'=>'user_id', 'user_id'=>(int)$managers[$i]);
	}
	//Значение переменных для уведомления
	$notify_vars = array();
	$notify_vars['order_id'] = $order_id;
	$notify_vars['pay_value'] = $pay_sum;
	$notify_vars['paid'] = $new_paid_status_text;
	$notify_vars['order_sum'] = $order_sum;
	$notify_vars['paid_sum'] = $order_sum - $paid_left_new;
	$notify_vars['paid_left'] = $paid_left_new;
	
	//Ссылка на страницу заказа
	$order_link = '<div style="margin-top:10px;"><a style="background: '.$background_order_link.'; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;" target="_blank" href="'. $DP_Config->domain_path . $DP_Config->backend_dir .'/shop/orders/order?order_id='. $order_id .'">'.translate_str_by_id(4643).'</a></div>';
	$notify_vars['order_link'] = $order_link;
	
	//Отправляем уведомление (БЕЗ обработки результата)
	send_notify('order_pay_to_manager', $notify_vars, $persons, false);


	//Для покупателя
	//Значение переменных для уведомления
	$notify_vars = array();
	$notify_vars['order_id'] = $order_id;
	$notify_vars['pay_value'] = $pay_sum;
	$notify_vars['paid'] = $new_paid_status_text;
	$notify_vars['order_sum'] = $order_sum;
	$notify_vars['paid_sum'] = $order_sum - $paid_left_new;
	$notify_vars['paid_left'] = $paid_left_new;
	
	if($user_id > 0){
		$order_link = '<div style="margin-top:10px;"><a style="background: '.$background_order_link.'; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;" target="_blank" href="'. $DP_Config->domain_path .'shop/orders/order?order_id='. $order_id .'">'.translate_str_by_id(4643).'</a></div>';
	}else{
		$order_link = '<div style="margin-top:10px;"><a style="background: '.$background_order_link.'; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;" target="_blank" href="'. $DP_Config->domain_path .'shop/orders/zakaz-bez-registracii?order_id='. $order_id .'">'.translate_str_by_id(4643).'</a></div>';
	}
	$notify_vars['order_link'] = $order_link;
	
	//Получатель
	$persons = array();
	if( $order["user_id"] > 0 )
	{
		$persons[] = array('type'=>'user_id', 'user_id'=>$order["user_id"]);
	}
	else
	{
		$persons[] = array(
			'type'=>'direct_contact',
			'contacts'=>array(
					'email'=>array('value'=>$order["email_not_auth"]),
					'phone'=>array('value'=>$order["phone_not_auth"])
				)
			);
	}
	//Отправляем уведомление (БЕЗ обработки результата)
	send_notify('order_pay_to_customer', $notify_vars, $persons, false);
	// --------------------------------------------------------------------------------------------------------------

	//Меняем статус заказа (только если заказ теперь полностью оплачен и если выставлены соответствующие настройки)
	if( $new_paid_status == 1 )
	{
		$for_paid_status_query = $db_link->prepare('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1;');
		$for_paid_status_query->execute();
		$for_paid_status_record = $for_paid_status_query->fetch();
		if( $for_paid_status_record != false )
		{
			$for_paid_status = $for_paid_status_record["id"];
			
			//ВЫЗЫВАЕМ СКРИПТ ДЛЯ ИЗМЕНЕНИЯ СТАТУСА ЗАКАЗА
			//Если требуется авторизация
			$username = '';
			$password = '';
			if( isset($DP_Config->http_login) )
			{
				$username = $DP_Config->http_login;
			}
			if( isset($DP_Config->http_password) )
			{
				$password = $DP_Config->http_password;
			}
			$context = stream_context_create(array(
				'http' => array(
					'header'  => "Authorization: Basic " . base64_encode("$username:$password")
				)
			));
			$set_order_status_result = file_get_contents($DP_Config->domain_path."content/shop/protocol/set_order_status.php?initiator=4&orders=[$order_id]&status=$for_paid_status&key=".urlencode($DP_Config->tech_key), false, $context);
			$set_order_status_result = json_decode($set_order_status_result, true);
			if($set_order_status_result['status'] == false )
			{
				//Ошибку смены статуса заказа не обрабатываем
			}
		}
	}
}
catch (Exception $e)
{
	//Откатываем все изменения
	$db_link->rollBack();
	
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = $e->getMessage();
	exit( json_encode($answer) );
}

//Дошли до сюда, значит выполнено ОК

$answer = array();
$answer["status"] = true;
$answer["message"] = '';
exit( json_encode($answer) );
?>