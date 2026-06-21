<?php
/**
 * Скрипт перехода на страницу оплаты
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


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

$user_id = DP_User::getUserId();




//Получаем данные операции
$operation_id = (int)$_GET["operation"];
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

$operation_description = translate_str_by_id(4338);
if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$operation_description = translate_str_by_id(4350);
}



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

// Название сайта
$site_name = $DP_Config->domain_path;
$site_name = str_replace('https://','',$site_name);
$site_name = str_replace('http://','',$site_name);
$site_name = str_replace('/','',$site_name);
$site_name = trim($site_name);

if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$order_id = (int)$operation["pay_orders"];
	$operation_description = translate_str_by_id(4350)." id $order_id ".translate_str_by_id(4375)." ".$site_name;
	
	$operation_name = translate_str_by_id(4350)." id $order_id";// Наименование операции которое отображается на сайте платежной системы
	
	// Позиции заказа
	$order_items_query = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ".$WHERE_COUNT_STATUS);
	array_unshift($binding_args, $order_id);
	$order_items_query->execute($binding_args);
	
	while($order_items_record = $order_items_query->fetch() )
	{	
		// Получаем наименование товара
		$name = '';
		if($order_items_record['product_type'] == 1)
		{
			$product_id = (int)$order_items_record['product_id'];
			
			$product_query = $db_link->prepare("SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = ?;");
			$product_query->execute( array($product_id) );
			

			$caption = $product_query->fetch();
			$caption = $caption['caption'];
			$name = mb_substr($caption, 0, 49, 'UTF-8');
		}else{
			$name = $order_items_record['t2_manufacturer'] .' - '. $order_items_record['t2_article'];
		}
		
		if($name == ''){
			$name = translate_str_by_id(4376);
		}
		
		$cart[] = array('name' 		=> $name, 
						'price' 	=> $order_items_record["price"], 
						'quantity' 	=> $order_items_record['count_need'], 
						'sum' 		=> $order_items_record["price"] * $order_items_record['count_need'], 
						'tax' 		=> 'none'
						);
	}
}else{
	$cart[] = array('name' 		=> translate_str_by_id(4377), 
					'price' 	=> $operation["amount"], 
					'quantity' 	=> 1, 
					'sum' 		=> $operation["amount"], 
					'tax' 		=> 'none', 
					'payment_type' => 'advance'
					);
}

//////////////////////////////////////////////////////////////////////////////////////////



$email = '';
$phone = '';

if($operation["user_id"] > 0)
{
	// Клиент зарегистрирован
	$main_field_query = $db_link->prepare("SELECT `email`, `phone` FROM `users` WHERE `user_id` = ?;");
	$main_field_query->execute( array($operation["user_id"]) );
	$main_field_record = $main_field_query->fetch();
	
	$phone = '+7'.$main_field_record["phone"];
	$email = $main_field_record["email"];
	
}else{
	// Клиент без регистрации, возьмем данные для уведомления из заказа
	if(!empty($operation["pay_orders"]))
	{
		
		$order_id_tmp = (int) trim($operation["pay_orders"]);
		
		
		$order_data_query = $db_link->prepare("SELECT * FROM `shop_orders` WHERE `id` = ?;");
		$order_data_query->execute( array($order_id_tmp) );
		
		
		$order_data_record = $order_data_query->fetch();
		
		$phone = '+7'.$order_data_record["phone_not_auth"];
		$email = $order_data_record["email_not_auth"];
	}
}


//////////////////////////////////////////////////////////////////////////////////////////




$sum = $operation["amount"];


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$login = $paysystem_parameters["paykeeper_login"];
$password = $paysystem_parameters["paykeeper_password"];
$subdomain = $paysystem_parameters["paykeeper_subdomain"];



// инициализация сеанса
$payment_parameters = http_build_query(array( "clientid"=>$user_id,
			"orderid"=>$operation_id,
			"sum"=>$sum,
			"client_email"=>$email,
			"client_phone"=>$phone,
			"cart"=>json_encode($cart)));
$options = array("http"=>array(
			"method"=>"POST",
			"header"=>
			"Content-type: application/x-www-form-urlencoded",
			"content"=>$payment_parameters
			   ));
$context = stream_context_create($options);

header('Content-Type: text/html; charset=utf-8');
echo file_get_contents("https://".$subdomain.".paykeeper.ru/order/inline/",FALSE, $context);
?>