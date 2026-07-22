<?php
/**
Серверный скрипт создания новой финансовой операции (только при оплате через интернет-эквайринг)
*/
header('Content-Type: application/json;charset=utf-8;');
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
	$answer["result"] = false;
	$answer["message"] = 'No DB Connect';
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

//Магазины пользователя
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/order_process/get_customer_offices.php' );

//Получаем объект запроса на создание операции
$request_object = json_decode($_POST["request_object"], true);

$pay_order = "";//Строка с номером заказа, который нужно оплатить


//Варианты работы: пополнение баланса (только для зарегистрированных клиентов) и платеж по заказу, полный, либо частичный (может выполнять любой пользователь, в т.ч. не зарегистрированный).
//Сумма в request_object передается в любом случае - при пополнении баланса и при прямой оплате заказа.
//Параметр order_id передается только при прямом платеже по заказу (полная оплата, либо, частичная оплата).
if( isset($request_object["amount"]) && !isset($request_object["order_id"]) )
{
	//Пополнение баланса
	//Пополнять баланс могут только зарегистрированные пользователи
	if($user_id == 0)
	{
		$answer = array();
		$answer["result"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
	
	//Сумма операции берется из объекта запроса
	$amount = $request_object["amount"];
	
	
	if( $amount <= 0 )
	{
		$answer = array();
		$answer["result"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
	
	
	//Ключ для типа системной операции - "Самостоятельное пополнение баланса клиентом через сайт"
	$operation_key = '3_income_by_customer';
}
else if( isset($request_object["amount"]) && isset($request_object["order_id"]) )
{
	//Платеж по заказу
	$amount = $request_object["amount"];
	$order_id = $request_object["order_id"];
	$pay_order = $order_id;
	
	
	//Проверка соответствия заказа пользователю
	$query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ?');
	$query->execute(array($order_id));
	$record = $query->fetch();
	if($record['user_id'] != $user_id){
		$answer = array();
		$answer["result"] = false;
		$answer["user"] = false;
		exit(json_encode($answer));
	}
	
	
	if( $amount <= 0 )
	{
		$answer = array();
		$answer["result"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
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
	$order_query = $db_link->prepare("SELECT *, CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(8,2) ) AS `paid_sum`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) - ($ISSUE_SQL - $INCOME_SQL) ) AS DECIMAL(8,2) )  AS `paid_left` FROM `shop_orders` WHERE `id` = ?;");
	$order_query->execute( array($order_id, $order_id, $order_id, $order_id, $order_id) );
	$order = $order_query->fetch();
	if( $order == false )
	{
		$answer = array();
		$answer["result"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
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
		$answer = array();
		$answer["result"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
	$order_sum = $order_sum_record["order_sum"];
	
	
	//Далее нужно определить, допустима ли такая операция. Эти проверки есть в скрипте привязки платежей к заказу, но, здесь они тоже нужны.
	//Заказ не должен иметь состояние "Полностью оплачен" (paid 1)
	if( $order['paid'] == 1 )
	{
		$answer = array();
		$answer["result"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
	
	//Сумма создаваемого платежа не должна превышать сумму долга по заказу
	if( $amount > ($order_sum - $order['paid_sum']) )
	{
		$answer = array();
		$answer["result"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
	
	//Если платеж - частичный (сумма создаваемого платежа меньше суммы долга по заказу), то:
	if( $amount < $order['paid_left'] )
	{
		//Проверяем, что функция частичной оплаты включена
		if( $DP_Config->partial_payment != true )
		{
			$answer = array();
			$answer["result"] = false;
			$answer["message"] = 'Forbidden';
			exit(json_encode($answer));
		}
		
		//Платеж должен быть не ниже минимально-допустимой оплаты в % от заказа
		if( $amount < $order_sum*($DP_Config->partial_payment_min_percent/100) )
		{
			$answer = array();
			$answer["result"] = false;
			$answer["message"] = 'Forbidden';
			exit(json_encode($answer));
		}
	}
	
	//Ключ для типа системной операции - "Зачисление на баланс перед платежом по заказу (при оплате клиентом через сайт)"
	$operation_key = '4_income_for_direct_pay';
}
else
{
	exit;
}





if( isset($DP_Config->wholesaler) )
{
	if( isset($request_object["order_id"]) )
	{
		//Если идет оплата заказа ($request_object["order_id"] - проверен выше)
		$office_id_query = $db_link->prepare("SELECT `office_id` FROM `shop_orders` WHERE `id` = ?;");
		$office_id_query->execute( array($request_object["order_id"]) );
		$office_id = $office_id_query->fetchColumn();
	}
	else
	{
		//Если идет пополнение баланса
		$office_id = (int)$request_object['office_id'];
	
		if( array_search($office_id, $customer_offices) === false )
		{
			exit();
		}
	}

	
	$active_payment_system_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `id` = (SELECT `pay_system_id` FROM `shop_offices` WHERE `id` = ?);');
	$active_payment_system_query->execute( array($office_id) );
	$active_system = $active_payment_system_query->fetch();
	if( $active_system != false )
	{
		$active_system = $active_system["handler"];
	}
}
else
{
	$office_id = 0;

	// Optional customer-selected gateway (must be enabled / anable=1)
	$requested_handler = '';
	if (!empty($request_object['pay_handler'])) {
		$requested_handler = preg_replace('/[^a-z0-9_]/', '', (string)$request_object['pay_handler']);
	}
	$active_system = false;
	if ($requested_handler !== '') {
		$pick_q = $db_link->prepare('SELECT `handler` FROM `shop_payment_systems` WHERE `handler` = ? AND `anable` = 1 LIMIT 1;');
		$pick_q->execute(array($requested_handler));
		$picked = $pick_q->fetchColumn();
		if ($picked) {
			$active_system = (string)$picked;
		}
	}
	if ($active_system === false) {
		$active_payment_system_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `active` = 1;');
		$active_payment_system_query->execute();
		$active_system_row = $active_payment_system_query->fetch();
		if ($active_system_row != false) {
			$active_system = $active_system_row["handler"];
		}
	}
}

// Individual payment account (office / vendor) — money attributed to that account
$epc_payment_account_id = 0;
$epc_pay_account = null;
try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_accounts.php';
	$orderForAccount = isset($request_object['order_id']) ? (int)$request_object['order_id'] : 0;
	$epc_pay_account = epc_pay_accounts_resolve_for_order($db_link, $orderForAccount);
	if (is_array($epc_pay_account)) {
		$epc_payment_account_id = (int)($epc_pay_account['id'] ?? 0);
		$accHandler = preg_replace('/[^a-z0-9_]/', '', (string)($epc_pay_account['handler'] ?? ''));
		// Prefer account's own merchant handler unless customer explicitly picked another enabled gateway
		if ($accHandler !== '' && empty($request_object['pay_handler'])) {
			$active_system = $accHandler;
		}
		if (($epc_pay_account['owner_type'] ?? '') === 'office' && (int)($epc_pay_account['owner_id'] ?? 0) > 0 && (int)$office_id === 0) {
			$office_id = (int)$epc_pay_account['owner_id'];
		}
	}
} catch (Throwable $e) {
	$epc_payment_account_id = 0;
}

$create_result = $db_link->prepare('INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `pay_orders`, `office_id`) VALUES (?, ?, ?, ?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) , ?, ?, ?);');
if( $create_result->execute( array($user_id, time(), 1, $amount, $operation_key, 0, $pay_order, $office_id) ) == true)
{
	$newOpId = (int)$db_link->lastInsertId();
	if ($epc_payment_account_id > 0) {
		try {
			$db_link->prepare('UPDATE `shop_users_accounting` SET `epc_payment_account_id` = ? WHERE `id` = ?')
				->execute(array($epc_payment_account_id, $newOpId));
		} catch (Throwable $e) {
		}
	}
	$answer = array();
	$answer["result"] = true;
	$answer["operation"] = $newOpId;
	$answer["pay_system"] = $active_system;
	$answer["payment_account_id"] = $epc_payment_account_id;
	if (is_array($epc_pay_account)) {
		$answer["payment_account"] = array(
			'id' => $epc_payment_account_id,
			'title' => (string)($epc_pay_account['title'] ?? ''),
			'owner_type' => (string)($epc_pay_account['owner_type'] ?? ''),
			'owner_id' => (int)($epc_pay_account['owner_id'] ?? 0),
			'handler' => (string)($epc_pay_account['handler'] ?? ''),
		);
	}
	exit(json_encode($answer));
}
else
{
	$answer = array();
	$answer["result"] = false;
	exit(json_encode($answer));
}


$answer = array();
$answer["result"] = false;
exit(json_encode($answer));
?>