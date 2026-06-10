<?php
/**
 * Скрипт перехода на страницу оплаты (https://docs.webpay.by)
*/
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
    $answer = array();
	$answer["result"] = false;
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();



//Получаем данные операции
$operation_id = (int) $_GET["operation"];
$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0 AND `user_id` = ?;');
$operation_query->execute( array($operation_id, $user_id) );
$operation = $operation_query->fetch();
if($operation == false)
{
    $answer = array();
	$answer["result"] = false;
	$answer["code"] = 2;
	exit(json_encode($answer));
}



// -------------------------------------------------------------------------------------------------


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


// -------------------------------------------------------------------------------------------------



//Регистрируем нашу операцию в системе банка
if($paysystem_parameters["wsb_test"] == 1)
{
	$url = "https://securesandbox.webpay.by/api/v1/payment";
}
else
{
	$url = "https://payment.webpay.by/api/v1/payment";
}



$request_body = array();
$request_body["wsb_storeid"] = $paysystem_parameters["wsb_storeid"];//Идентификатор магазина в системе WEBPAY
$request_body["wsb_order_num"] = $operation_id;//Уникальный идентификатор заказа
$request_body["wsb_currency_id"] = $paysystem_parameters["wsb_currency_id"];//Идентификатор валюты: "BYN" "USD" "EUR" "RUB"
$request_body["wsb_version"] = 2;//Версия формы оплаты. Текущий номер версии: 2
$request_body["wsb_seed"] = rand(10000, 100000);//Случайная последовательность символов, участвующих в формировании электронной подписи заказа
$request_body["wsb_test"] = (int) $paysystem_parameters["wsb_test"];//Поле, указывающее на проведение тестовой оплаты.
$request_body["wsb_invoice_item_name"] = array();//Массив - Наименование единицы товара.
$request_body["wsb_invoice_item_quantity"] = array();//Массив - Количество единиц товара.
$request_body["wsb_invoice_item_price"] = array();//Массив - Цена единицы товара.



//////////////////////////////////////////////////////////////////////////////////////////
//Общая информация по заказам
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

// Отмененные статусы
$binding_args = array();
$WHERE_COUNT_STATUS = "";
for($i=0; $i < count($orders_items_statuses_not_count); $i++)
{
    $WHERE_COUNT_STATUS .= " AND `status` != ?";
	
	$binding_args[] = $orders_items_statuses_not_count[$i];
}

if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	// Позиции заказа
	$order_items_query = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ".$WHERE_COUNT_STATUS);
	array_unshift($binding_args, $operation["pay_orders"]);
	$order_items_query->execute($binding_args);
	
	while($order_items_record = $order_items_query->fetch() )
	{	
		$name = $order_items_record['t2_manufacturer'] .' - '. $order_items_record['t2_article'];
		
		if($name == ''){
			$name = 'Автозапчасть';
		}
		
		$request_body["wsb_invoice_item_name"][] = $name;
		$request_body["wsb_invoice_item_quantity"][] = $order_items_record['count_need'];
		$request_body["wsb_invoice_item_price"][] = $order_items_record["price"] * $order_items_record['count_need'];
		
		$request_body["wsb_cancel_return_url"] = $DP_Config->domain_path."shop/orders/order?order_id=".$operation["pay_orders"]."&error_message=%D0%9E%D1%88%D0%B8%D0%B1%D0%BA%D0%B0";
	}
}else{
	$request_body["wsb_invoice_item_name"][] = translate_str_by_id(4338);
	$request_body["wsb_invoice_item_quantity"][] = 1;
	$request_body["wsb_invoice_item_price"][] = $operation["amount"];
	
	$request_body["wsb_cancel_return_url"] = $DP_Config->domain_path."shop/balans?error_message=%D0%9E%D1%88%D0%B8%D0%B1%D0%BA%D0%B0";
}

//////////////////////////////////////////////////////////////////////////////////////////



$request_body["wsb_total"] = $operation["amount"];//Сумма оплаты заказа.
$request_body["wsb_signature"] = SHA1($request_body["wsb_seed"] . $request_body["wsb_storeid"] . $request_body["wsb_order_num"] . $request_body["wsb_test"] . $request_body["wsb_currency_id"] . $request_body["wsb_total"] . $paysystem_parameters["secret_key"]);//Электронная подпись
$request_body["wsb_notify_url"] = $DP_Config->domain_path."content/shop/finance/payment_systems/webpay_by/notification.php";
$request_body["wsb_return_url"] = $DP_Config->domain_path."content/shop/finance/payment_systems/webpay_by/notification.php";



$data_string = json_encode($request_body, JSON_UNESCAPED_UNICODE);

$ch = curl_init();
curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_HEADER, 0);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($ch, CURLOPT_POSTFIELDS, $data_string);
curl_setopt($ch, CURLOPT_HTTPHEADER, array(
   'Content-Type: application/json',
   'Content-Length: ' . strlen($data_string))
);
$result = curl_exec($ch);
curl_close($ch);

$result = json_decode($result, true);

/*
echo '<pre>';
var_dump($request_body);
echo '</pre>';
exit;
*/

if(!empty($result['data']['redirectUrl'])){
	header("Location: ".$result['data']['redirectUrl']);
	exit;
}else{
	echo '<h3>'.translate_str_by_id(2122).':</h3>';
	echo '<pre>';
	echo $result['error']['message'];
	echo '</pre>';
	exit;
}
?>