<?php
/**
 * Скрипт перехода на страницу оплаты
 * https://kassa.yandex.ru/developers
 * https://kassa.yandex.ru/developers/api
 *
 * https://yookassa.ru/developers/payments/payment-process
*/

//[{"name":"shopId","type":"text","caption":"Идентификатор магазина"},{"name":"shopKey","type":"text","caption":"Секретный ключ"},{"name":"currency","type":"text","caption":"Код валюты (например: RUB)<br><br><br>Для работы платежной системы к сайту обязательно должен быть подключен<br>SSL сертификат и сайт должен работать<br>по https протоколу.<br>В личном кабинете яндекс-кассы в разделе Интеграция — HTTP-уведомления укажите<br>URL для уведомлений:<br>https://ВАШ_ДОМЕН_САЙТА/content/shop/finance/payment_systems/yandex_money/notification.php"}]

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
$user_id = (int)DP_User::getUserId();



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
$flag_pay_orders = false;
if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$flag_pay_orders = true;
	$operation_description = translate_str_by_id(4350)." id ". $operation["pay_orders"];
}



//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );



$shopId = trim($paysystem_parameters["shopId"]);//Идентификатор магазина
$shopKey = trim($paysystem_parameters["shopKey"]);//Секретный ключ
$currency = trim($paysystem_parameters["currency"]);//Валюта

if(empty($currency)){
	$currency = 'RUB';
}



//Сумма (должна быть с копейками)
$amount = (float)$operation["amount"];
$amount = number_format($amount, 2, '.', '');



//*************************************************************************
//*************************************************************************
//*************************************************************************

// ПОЛУЧАЕМ ПОЧТУ ИЛИ ТЕЛЕФОН КЛИЕНТА НА КОТОРУЮ БУДЕТ ОТПРАВЛЕН ЭЛЕКТРОННЫЙ ЧЕК
$email = '';
$phone = '';
if(empty($email)){
	if($operation["user_id"] > 0){
		// Клиент зарегистрирован
		$main_field_query = $db_link->prepare("SELECT * FROM `users` WHERE `user_id` = ".$operation["user_id"]);
		$main_field_query->execute();
		$main_field_record = $main_field_query->fetch();
		$email = trim($main_field_record["email"]);
		$phone = trim($main_field_record["phone"]);
		if(!empty($phone)){
			$phone = str_replace(array(' ', '+7', '(', ')', '-', '_'), '', $phone);
			if(strlen($phone) == 11){
				$phone = substr($phone, 1);
			}
			$phone = '+7'.$phone;
		}
	}else{
		// Клиент без регистрации, возьмем данные для уведомления из заказа
		if(!empty($operation["pay_orders"])){
			
			$order_id_tmp = (int) trim($operation["pay_orders"]);
			
			$order_data_query = $db_link->prepare("SELECT * FROM `shop_orders` WHERE `id` = $order_id_tmp;");
			$order_data_query->execute();
			$order_data_record = $order_data_query->fetch();
			$email = trim($order_data_record["email_not_auth"]);
			$phone = trim($order_data_record["phone_not_auth"]);
			if(!empty($phone)){
				$phone = str_replace(array(' ', '+7', '(', ')', '-', '_'), '', $phone);
				if(strlen($phone) == 11){
					$phone = substr($phone, 1);
				}
				$phone = '+7'.$phone;
			}
		}
	}
}



// ДАННЫЕ ДЛЯ ФОРМИРОВАНИЯ ЧЕКА
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");
$receipt = array();

if($flag_pay_orders == false){
	$positions[] = array(
		"description" => translate_str_by_id(4378)." id $user_id",
		"quantity" => 1,
		"amount" => array('value'=>$amount, 'currency'=>$currency),
		"vat_code" => 1,
		"payment_subject" => 'payment',
		"payment_mode" => 'full_payment'
	);
	
}else{
	
	$order_items_query = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `order_id` = ?;");
	$order_items_query->execute( array($operation["pay_orders"]) );
	$positions = array();
	$count_need_total = 0;//Итого количество
	$price_sum_total = 0;//Итого сумма
	while($order_items_record = $order_items_query->fetch()){
		
		$item_status 		= $order_items_record["status"];
		$item_count_need    = $order_items_record["count_need"];
		$item_price         = $order_items_record["price"];
		
		//Считаем поля ИТОГО ПО ЗАКАЗУ (если статус позиции позволяет)
		if( array_search($item_status, $orders_items_statuses_not_count) === false)
		{
			$count_need_total += $item_count_need;
			$price_sum_total  += $item_price * $item_count_need;
		}else{
			// Если статус позиции - отмена то пропускаем позицию
			continue;
		}
		
		// Получаем наименование товара
		$name = '';
		if($order_items_record['product_type'] == 1){
			$product_id = (int)$order_items_record['product_id'];
			$product_query = $db_link->prepare("SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = ?;");
			$product_query->execute( array($product_id) );
			$caption = $product_query->fetch();
			$caption = $caption['caption'];
			$name = mb_substr($caption, 0, 100, 'UTF-8');
		}else{
			$name = $order_items_record['t2_manufacturer'] .' - '. $order_items_record['t2_article'] .' - '. $order_items_record['t2_name'];
		}
		
		if($name == ''){
			$name = translate_str_by_id(4376);
		}
		
		$positions[] = array(
			"description" => $name,
			"quantity" => $order_items_record['count_need'],
			"amount" => array('value'=>number_format($order_items_record['price'], 2, '.', ''), 'currency'=>$currency),
			"vat_code" => 1,
			"payment_subject" => 'commodity',
			"payment_mode" => 'full_payment'
		);
	}
}

if(!empty($positions)){
	$customer = array();
	if(!empty($email)){
		$customer['email'] = $email;
	}
	if(!empty($phone)){
		$customer['phone'] = $phone;
	}
	$receipt = array(
		'customer' => $customer,
		'items' => $positions
	);
}

//*************************************************************************
//*************************************************************************
//*************************************************************************



$postdata = array(
	'amount' => array('value'=>$amount, 'currency'=>$currency),
	'capture' => true,
	'confirmation' => array('type'=>'redirect', 'return_url'=>$DP_Config->domain_path.'content/shop/finance/payment_systems/yookassa/notification.php?operation_id='.$operation_id),
	'description' => $operation_description,
	'metadata' => array('operation_id'=>$operation_id),
	'receipt' => $receipt
);

$data_string = json_encode($postdata, JSON_UNESCAPED_UNICODE);

$curl = curl_init();
//curl_setopt($curl, CURLOPT_URL, "https://payment.yandex.net/api/v3/payments");
curl_setopt($curl, CURLOPT_URL, "https://api.yookassa.ru/v3/payments");
curl_setopt($curl, CURLOPT_HEADER, 0);
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_POST, true);
curl_setopt($curl, CURLOPT_POSTFIELDS, $data_string);
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_HTTPHEADER, array(
   'Authorization: Basic '.base64_encode("$shopId:$shopKey"),
   'Idempotence-Key: '.$operation_id,
   'Content-Type: application/json'
   )
);
curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 30); 
curl_setopt($curl, CURLOPT_TIMEOUT, 30);
curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
$curl_result = curl_exec($curl);



/*
echo '<pre>';
var_dump($curl_result);
echo '</pre>';
*/



$result = json_decode($curl_result, true);



/*
echo '<pre>';
var_dump($result);
echo '</pre>';
*/



$f = fopen('z_go_to_pay'.date("m_Y", time()).'.txt', 'a');
fwrite($f, "\n\n\n\n***\n\n\n\n". date("d-m-Y H:i:s", time()) ."\n");
fwrite($f, "postdata:\n". json_encode($postdata, JSON_UNESCAPED_UNICODE) ."\n\n");
fwrite($f, "curl_result:\n". $curl_result ."\n");



curl_close($curl);



if($result['status'] === 'pending'){
	if( (((float)$result['amount']['value']) === ((float)$amount)) && (((int)$result['metadata']['operation_id']) === ((int)$operation_id)) && (!empty($result['confirmation']['confirmation_url']))){
		
		$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `tech_value_text` = ? WHERE `id` = ?;');
		if(! $update_query->execute( array($result["id"], $operation_id ) ) )
		{
			header( "Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".urlencode(translate_str_by_id(4351)) );
			exit;
		}
		else
		{
			$confirmation_url = $result['confirmation']['confirmation_url'];
			header('Location: '.$confirmation_url);
			exit;
		}
		
	}else{
		echo '
		<h3>'.translate_str_by_id(4405).'.</h3>
		<p>'.translate_str_by_id(4406).'</p>
		';
	}
}else{
	echo '
	<h3>'.translate_str_by_id(4407).'.</h3>
	<p>'.translate_str_by_id(4406).'.</p>
	';
}
?>