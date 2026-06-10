<?php
/**
 * Скрипт перехода на страницу оплаты
 */
// Соединение с БД
// ini_set('display_errors', 1);
// ini_set('display_startup_errors', 1);
// error_reporting(E_ALL);

require_once($_SERVER["DOCUMENT_ROOT"] . "/config.php");
$DP_Config = new DP_Config; // Конфигурация CMS
// Подключение к БД
try {
    $db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
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
// Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"] . "/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

// Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"] . "/content/users/dp_user.php");

$user_id = DP_User::getUserId();

// Получаем данные операции
$operation_id = (int)$_GET["operation"];
$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0 AND `user_id` = ?;');
$operation_query->execute(array($operation_id, $user_id));
$operation = $operation_query->fetch();
if ($operation == false) {
    $answer = array();
    $answer["result"] = false;
    $answer["code"] = 2;
    exit(json_encode($answer));
}
$operation_description = translate_str_by_key('4338');
$flag_pay_orders = false;
if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$flag_pay_orders = true;
	$operation_description = translate_str_by_key('5679')." № ". $operation["pay_orders"];
}
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


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );

if($paysystem_parameters["test_mode"] == 1)
{
  $api_url = "https://secure.cdekfin.ru/test_merchant_api/payment_orders";
}
else
{
  $api_url = "https://secure.cdekfin.ru/merchant_api/payment_orders";
}

//Чек
$positions = array();

if ($flag_pay_orders == false) {
    $positions[] = array(
        "name" => translate_str_by_key('4378')." № $user_id",
        "price" => $operation["amount"] * 100, // сумма заказа в копейках
        "quantity" => 1,
        "sum" => $operation["amount"] * 100,
        "payment_object" => 4,
    );
} else {
    $order_items_query = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `order_id` = ?;");
    $order_items_query->execute(array($operation["pay_orders"]));
    $positions = array();
    $count_need_total = 0; // Итого количество
    $price_sum_total = 0; // Итого сумма
    $positions[] = array(
        "name" => $operation_description,
        "price" => $operation["amount"] * 100, // сумма заказа в копейках
        "quantity" => 1,
        "payment_object" => 1,
        "sum" => $operation["amount"] * 100,
    );
}
$success_url = $DP_Config->domain_path."content/shop/finance/payment_systems/cdekpay/success.php?account=".$operation_id; // Адрес для переадресации плательщика в случае успешной оплаты
// Подготовить positions[] к виду для отправки receipt_details.0
$positions_tmp = array();
foreach ($positions as $key => $value) {
    $positions_tmp[] = array(
        "name" => $value["name"],
        "price" => $value["price"],
        "quantity" => $value["quantity"],
        "payment_object" => $value["payment_object"],
        "sum" => $value["sum"],
    );
}


$post = [
    'login' => $paysystem_parameters['cd_login'],
    'payment_order' => [
        'pay_for' => $operation_description,
        'pay_amount' => $operation["amount"] * 100, // сумма заказа в копейках
        'currency' => "RUR",
        'user_phone' => $phone,
        'user_email' => $email,
        "return_url_success" => $success_url,
        
    ],
];
foreach ($positions as $key => $value) {
    $post['payment_order']["receipt_details.$key.name"] = $value["name"];
    $post['payment_order']["receipt_details.$key.price"] = $value["price"];
    $post['payment_order']["receipt_details.$key.quantity"] = $value["quantity"];
    $post['payment_order']["receipt_details.$key.payment_object"] = $value["payment_object"];
    $post['payment_order']["receipt_details.$key.sum"] = $value["sum"];
}
$post['payment_order']["pay_for_details.payment_id"] = $operation["id"];
$post['payment_order']["pay_for_details.payer"] = translate_str_by_key('3245')." №".$user_id;

//print_r($post);

// Отсортируем массив
ksort($post['payment_order']);
$post['payment_order'] = array_change_key_case($post['payment_order'], CASE_LOWER);

$signed = implode("|", $post['payment_order']) . "|" . $paysystem_parameters['cd_apikey'];

// Приведем подпись к верхнему регистру
$signed = strtoupper(hash("sha256", $signed));



$post = [
    'login' => $paysystem_parameters['cd_login'],
    'signature' => $signed,
    'payment_order' => [
        'pay_for' => $operation_description,
        'pay_amount' => $operation["amount"] * 100, // сумма заказа в копейках
        'currency' => "RUR",
        'user_phone' => $phone,
        'user_email' => $email,
        "return_url_success" => $success_url,
        "receipt_details"  => $positions,
        "pay_for_details" => [
            "payment_id" => $operation["id"],
            "payer" => translate_str_by_key('3245')." №".$user_id,
        ],
        
    ],
];

$post = json_encode($post, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);

//print_r("<br><br>".$post);
// Установим заголовок Content-Type
$headers = [
    'Content-Type: application/json',
];

$ch = curl_init($api_url);
curl_setopt($ch, CURLOPT_POSTFIELDS, $post);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);

// Обработка ошибок CURL
if (curl_errno($ch)) {
    echo 'Curl error: ' . curl_error($ch);
} else {
    $response_json = curl_exec($ch);
    $http_code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    $response = json_decode($response_json, true);

   // Сделаем редирект на страницу оплаты
    if($http_code == 200){
    	// Успешно
    	header('Location: '.$response["link"]);
    }else{
        echo 'HTTP Code: ' . $http_code . '<br>';
        echo 'Response: ' . $response_json;
    }
}

