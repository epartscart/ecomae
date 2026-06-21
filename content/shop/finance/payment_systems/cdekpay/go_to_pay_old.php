<?php
/**
 * Скрипт перехода на страницу оплаты
 */
// Соединение с БД
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
$operation_description = "Пополнение баланса";
$flag_pay_orders = false;
if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$flag_pay_orders = true;
	$operation_description = "Оплата заказа корзины № ". $operation["pay_orders"];
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
//Чек
$positions = array();

$receipt_details = array();

if ($flag_pay_orders == false) {
    $operation_description = "Пополнение баланса клиента № " . $user_id;
    $receipt_details[] = array(
        "name" => $operation_description,
        "price" => $operation["amount"],
        "quantity" => 1,
        "sum" => $operation["amount"],
    );
} else {
    $order_id = (int)$operation["pay_orders"];
    $operation_description = "Оплата заказа № $order_id в интернет-магазине автозапчастей " . $DP_Config->site_name;

    // Позиции заказа
    $order_items_query = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = :order_id ' . $WHERE_COUNT_STATUS);
    $order_items_query->bindParam(':order_id', $order_id, PDO::PARAM_INT);
    $order_items_query->execute();

    while ($order_items_record = $order_items_query->fetch(PDO::FETCH_ASSOC)) {
        // Получаем наименование товара
        $name = '';

        if ($order_items_record['product_type'] == 1) {
            $product_id = (int)$order_items_record['product_id'];
            $product_query = $db_link->prepare('SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = :product_id');
            $product_query->bindParam(':product_id', $product_id, PDO::PARAM_INT);
            $product_query->execute();
            $caption_record = $product_query->fetch(PDO::FETCH_ASSOC);
            $caption = $caption_record['caption'];
            $name = mb_substr($caption, 0, 49, 'UTF-8');
        } else {
            $name = $order_items_record['t2_manufacturer'] . ' - ' . $order_items_record['t2_article'];
        }

        if ($name == '') {
            $name = 'Автозапчасть';
        }

        $receipt_details[] = array(
            'name' => $name,
            'price' => $order_items_record["price"],
            'quantity' => $order_items_record['count_need'],
            'sum' => $order_items_record["price"] * $order_items_record['count_need'],
        );
    }
}

$receipt_items = json_encode($receipt_details, JSON_UNESCAPED_UNICODE);
$formatted_receipt_details = array();


// Общий скрипт получения настроек платежной системы.
require_once($_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/get_pay_system_parameters.php');
if($paysystem_parameters["test_mode"] == 1)
{
  $api_url = "https://secure.cdekfin.ru/test_merchant_api/payment_orders";
}
else
{
  $api_url = "https://secure.cdekfin.ru/merchant_api/payment_orders";
  echo "string";
}
$payment_order = array(
    "pay_for" => $operation_description,
    "pay_amount" => $operation["amount"],
    "currency" => "RUR",
    "user_phone" => $phone,
    "user_email" => $email,
    "qr_life_time" => 7,
);

foreach ($receipt_details as $index => $item) {
    $formatted_receipt_details["receipt_details.$index.name"] = $item['name'];
    $formatted_receipt_details["receipt_details.$index.price"] = $item['price'];
    $formatted_receipt_details["receipt_details.$index.quantity"] = $item['quantity'];
    $formatted_receipt_details["receipt_details.$index.payment_object"] = $item['payment_object'];
    $formatted_receipt_details["receipt_details.$index.sum"] = $item['sum'];
}

$payment_order = array_merge($payment_order, $formatted_receipt_details);
// Отсортировать ключи и получить значения
ksort($payment_order);
$values = array_values($payment_order);

// Добавить api_key в конец
$values[] = $paysystem_parameters["cd_apikey"];

// Конкатенировать значения через символ "|"
$string_to_hash = implode("|", $values);
// Вычислить SHA256 хэш и привести к верхнему регистру
$signature = strtoupper(hash("sha256", $string_to_hash));
// Данные для запроса
$request_data = array(
    "login" => $paysystem_parameters["cd_login"],
    "signature" => $signature,
    "payment_order" => array(
        "pay_for" => $operation_description,
        "pay_amount" => $operation["amount"],
        "currency" => "RUR",
        "user_phone" => $phone, // Используйте значение номера телефона
        "user_email" => $email, // Используйте значение почты
        "return_url_success" => $DP_Config->domain_path."content/shop/finance/payment_systems/cdek/success.php",
        "return_url_fail" => $DP_Config->domain_path,
        "receipt_details" => json_decode($receipt_items_no_replace, true) // Используйте данные чека
    )
);

// Преобразование данных в JSON
$request_json = json_encode($request_data);

// Отправка POST-запроса к API CDEK Pay
$ch = curl_init($api_url);
curl_setopt($ch, CURLOPT_POST, 1);
curl_setopt($ch, CURLOPT_POSTFIELDS, $request_json);
curl_setopt($ch, CURLOPT_HTTPHEADER, array('Content-Type: application/json'));
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);

$response = curl_exec($ch);
curl_close($ch);

// Парсинг ответа
$response_data = json_decode($response, true);
//var_dump($response);
if (isset($response_data['result']) && $response_data['result'] == "OK") {
    $payment_url = $response_data['url'];
    echo "Ссылка на платежную форму: $payment_url";
} else {
    echo "Ошибка при создании платежной формы: " . $response;
}
