<?php
/**
 * 		Скрипт перехода на страницу оплаты alfabank
 
 
Для работы в "Личном кабинете" используйте логин с суффиксом -operator
Адрес "Личного кабинета" на тестовой среде:
https://test.paymentgate.ru/mportal/#login

Для использования API используйте логин с суффиксом -api

Руководство по подключению и описание "Личного кабинета" можно скачать по ссылке: 
https://pay.alfabank.ru/ecommerce/

Перед проведением интеграционных работ, во избежания возможных ошибок, настоятельно рекомендуем ознакомиться с FAQ
https://pay.alfabank.ru/ecommerce/_build/html/index.html

По завершению интеграции, вам необходмо выполснить несколько тестов:
https://pay.alfabank.ru/ecommerce/_build/html/testing_integration.html#id2

По всем техническим вопросам, возникшим в процессе интеграции, обращайтесь на адрес - ers@alfabank.ru
 
 
**/
header('Content-Type: text/html; charset=utf-8');

// Подключаем класс работы с api банка
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/payment_systems/alfabank/SoapClient.php");


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



//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );



// -------------------------------------------------------------------------------------------------


//Регистрируем нашу операцию в системе alfabank
//ПОСЫЛАЕМ ЗАПРОС В alfabank:

// Авторизационные данные
$login = $paysystem_parameters["login"];
$password = $paysystem_parameters["password"];

// Валюта - по умолчанию российские рубли. В документации указан код 810 - ISO 4217
// Проблема в том что это устаревший код. Сейчас код 643
// Поэтому мы проверяем код и если он 810 или 643 то удаляем настройку что бы не ошибиться так как по умолчанию если не передан параметр будут рубли.
$currency = $paysystem_parameters["currency"];

if(!empty($currency)){
	if($currency == 810 || $currency == 643){
		$currency = null;
	}// российский рубль (ISO 4217)
}

if($paysystem_parameters["test_mode"] == 1)
{
    $wsdl = "https://alfa.rbsuat.com/payment/webservices/merchant-ws?wsdl";
}
else
{
    $wsdl = "https://payment.alfabank.ru/payment/webservices/merchant-ws?wsdl";
}


$amount = $operation["amount"] * 100;

// Создаем соединение с банком
$client = new Gateway($wsdl);
$client->login = $login; 
$client->password = $password;
$data = array('orderParams' => array(
'returnUrl' => $DP_Config->domain_path .$multilang_params['lang_href_no_slash']. '/content/shop/finance/payment_systems/alfabank/notification.php',
'merchantOrderNumber' => $operation["id"],
'amount' => $amount,
'description' => $operation_description
));

// Валюта если указана
if(!empty($currency)){
	$data['orderParams']['currency'] = $currency;
}

$response = $client->__call('registerOrder', $data);

// Проверяем результат регистрации операции
if($response->errorCode != 0)
{
	// Есть ошибка:
	echo 'Ошибка: #' . $response->errorCode . ': ' . $response->errorMessage;
}
else
{
	//Оперция успешно зарегистрирована
	$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `tech_value_text` = ? WHERE `id` = ?;');
	if(! $update_query->execute(array($response->orderId, $operation_id)) )
	{
		header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".urlencode(translate_str_by_id(4351)) );
	}
	else
	{
		// Перенаправляем клиента на страницу оплаты
		header('Location: ' . $response->formUrl);
	}
}
?>