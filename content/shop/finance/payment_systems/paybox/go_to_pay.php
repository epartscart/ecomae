<?php
/**
 * Скрипт перехода на страницу оплаты
*/
header('Content-Type: text/html; charset=utf-8');
error_reporting(E_ALL & ~E_NOTICE & ~E_WARNING);

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

$user_profile = DP_User::getUserProfile();

$userPhone = isset($user_profile['main_field']) ? $user_profile['main_field'] : '';

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


$eshopId = $paysystem_parameters["eshopId"];// ID магазина
$currency = $paysystem_parameters["currency"];// Валюта
$apiKEY = $paysystem_parameters["apiKEY"];// Ключь

$out_summ = $operation["amount"];// Сумма


// инициализация сеанса
$pg_merchant_id = $eshopId;
$pg_order_id = $operation_id;
$pg_amount = $out_summ;
$pg_currency = $currency;
$pg_description = $operation_description;

$pg_result_url = $DP_Config->domain_path . 'content/shop/finance/payment_systems/paybox/notification.php';
$pg_request_method = 'POST';

$pg_success_url = $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] . '/shop/balans?success_message='.translate_str_by_id(2300);
$pg_success_url_method = 'GET';
$pg_failure_url = $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] . '/shop/balans?error_message='.translate_str_by_id(2122);
$pg_failure_url_method = 'GET';
$pg_site_url = $DP_Config->domain_path;

$pg_salt = time() . 'jsdhfsjsvsfisis';

$name_script = 'init_payment.php';

// Формируем массив с данными что бы затем его отсортировать в алфовитном порядке так как это требуется для формирования подписи
$arr = array(
		'pg_merchant_id' 		=> $eshopId,
		'pg_order_id' 			=> $operation_id,
		'pg_amount' 			=> $out_summ,
		'pg_currency' 			=> $currency,
		'pg_description' 		=> $operation_description,
		'pg_result_url' 		=> $pg_result_url,
		'pg_request_method' 	=> $pg_request_method,
		'pg_success_url' 		=> $pg_success_url,
		'pg_success_url_method' => $pg_success_url_method,
		'pg_failure_url' 		=> $pg_failure_url,
		'pg_failure_url_method' => $pg_failure_url_method,
		'pg_site_url' 			=> $pg_site_url,
		'pg_salt' 				=> $pg_salt
);

//$arr['pg_testing_mode'] = 1;
//$arr['pg_payment_system'] = 'TESTCARD';

ksort($arr);

//var_dump($arr);
//exit;

// Формирование подписи
$str = '';
foreach($arr as $v){
	$str .= $v .';';
}

//var_dump($name_script .';'. $str . $apiKEY)

$pg_sig = md5($name_script .';'. $str . $apiKEY);

// Флормируем запрос
$GET_STRING = "";
foreach($arr as $k => $v){
	$GET_STRING .= $k .'='. $v .'&';
}
$GET_STRING .= "pg_sig=$pg_sig";

$arr['pg_sig'] = $pg_sig;

$queryData = http_build_query($arr);

/*
echo '<pre>';
echo 'Масив данных отсортированный в алфавитном порядке:<br/>';
print_r($arr);
echo '<br/><br/>';
echo 'Секретный ключ:<br/>';
echo $apiKEY;
echo '<br/><br/>';
echo 'Формирование подписи:<br/>';
echo 'md5('.$name_script .';'. $str . $apiKEY.');';
echo '<br/><br/>';
echo 'результат формирования подписи: '.$pg_sig;
echo '<br/><br/>';
echo 'GET - Запрос:<br/>';
echo "https://www.paybox.kz/payment.php?".$GET_STRING;
exit;
*/

// header("Location: https://api.paybox.money/init_payment.php?" . $GET_STRING);

$ch = curl_init();
curl_setopt($ch, CURLOPT_URL, "https://api.paybox.money/init_payment.php");
curl_setopt ($ch, CURLOPT_FOLLOWLOCATION, true);
curl_setopt($ch, CURLOPT_HEADER, true);
curl_setopt($ch, CURLOPT_HTTPHEADER, array('Content-Type: application/x-www-form-urlencoded'));
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_POST, 1);
curl_setopt($ch, CURLOPT_POSTFIELDS, $queryData);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);

$result_str = curl_exec($ch);
curl_close($ch);

if(strpos($result_str, '<pg_status>ok</pg_status>') !== false) {
	if(preg_match('|<pg_redirect_url>(.*)</pg_redirect_url>|', $result_str, $response))
	{
		if(isset($response[1])) {
			$url = $response[1];
			header("Location: {$url}");
		}
	}
}


// header("Location: ".$DP_Config->domain_path."shop/balans?error_message=Ошибка+регистрации+операции");
echo translate_str_by_id(2122);


?>