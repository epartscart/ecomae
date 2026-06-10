<?php
// Скрипт отправки SMS
// https://lk.smstraffic.ru/doc/SMSTraffic_API.pdf

header('Content-Type: text/html; charset=utf-8');

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
	$answer["message"] = "Error";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


//Проверка прав на запуск скрипта
if( $_POST["check"] != $DP_Config->secret_succession )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit( json_encode($answer) );
}

//Получаем настройки SMS-оператора
$sms_api_query = $db_link->prepare('SELECT * FROM `sms_api` WHERE `handler` = ?;');
$sms_api_query->execute( array('smstraffic') );
$sms_api = $sms_api_query->fetch();
$parameters_values = json_decode($sms_api["parameters_values"], true);


//Данные для отправки
$body = urlencode($_POST["body"]);
$main_field = urlencode( str_replace("+","",$_POST['main_field']) );//+ из строки телефона удаляем
$login = urlencode($parameters_values["login"]);
$password = urlencode($parameters_values["password"]);


//Данные для отправки
//$body = 'test';
//$main_field = '';



//Вызов API оператора
$params = "login={$login}&password={$password}&want_sms_ids=1&phones={$main_field}&message={$body}&max_parts=5&rus=5";

$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "https://api.smstraffic.ru/multi.php");
curl_setopt($curl, CURLOPT_FOLLOWLOCATION, 1);
curl_setopt($curl, CURLOPT_POST, 1);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($curl, CURLOPT_USERAGENT, "sms.php class 1.0 (curl https)");
curl_setopt($curl, CURLOPT_TIMEOUT, 5); // 5 seconds
curl_setopt($curl, CURLOPT_POSTFIELDS, $params);

ob_start();
$bSuccess = curl_exec($curl);
$response = ob_get_contents();
ob_end_clean();
$http_result_code = curl_getinfo($curl, CURLINFO_HTTP_CODE);

curl_close($curl);

$response = ($bSuccess && $http_result_code == 200) ? $response : null;

// interpret response
if (strpos($response, '<result>OK</result>')){
	if (preg_match('|<sms_id>(\d+)</sms_id>|s', $response, $regs)){
		// Запрос выполнился
    	$answer = array();
    	$answer["status"] = true;
    	$answer["message"] = "";
    	exit( json_encode($answer) );
	}
	else // impossible
		// Запрос не выполнился (возможно ошибка авторизации, параметрах, итд...)
    	$answer = array();
    	$answer["status"] = false;
    	$answer["message"] = translate_str_by_id(4684);
    	exit( json_encode($answer) );
}
elseif (preg_match('|<description>(.+?)</description>|s', $response, $regs)){
	$error = $regs[1];
	// Запрос не выполнился (возможно ошибка авторизации, параметрах, итд...)
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = $error;
	exit( json_encode($answer) );
}
else
	// Запрос не выполнился (возможно ошибка авторизации, параметрах, итд...)
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4685);
	exit( json_encode($answer) );


?>