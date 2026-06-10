<?php
// Скрипт отправки SMS
// https://new.smsgorod.ru/panel/apiinfo
// Сообщения приходят только на номер string(11)

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
$sms_api_query->execute( array('smsgorod_ru') );
$sms_api = $sms_api_query->fetch();
$parameters_values = json_decode($sms_api["parameters_values"], true);


//Данные для отправки
$body = trim($_POST["body"]);
$main_field = $_POST['main_field'];
$api_key = trim($parameters_values["api-key"]);
$channel = trim($parameters_values["channel"]);
$sender = trim($parameters_values["sender"]);
$domain = (int) trim($parameters_values["domain"]);


if($channel != 'digit' && $channel != 'char'){
	$channel = 'digit';
}


if($domain){
	$body = str_replace(array('https://','http://','/'), '', $DP_Config->domain_path) .': '. $body;
}


$phone = str_replace(array(' ', '+7', '(', ')', '-', '_'), '', $main_field);
if(strlen($phone) == 11){
	$phone = substr($phone, 1);
}
$phone = '7'.$phone;
$main_field = $phone;


$sms =  array();
$sms['phone'] = $main_field;
$sms['text'] = $body;
$sms['channel'] = $channel;
if($sms['channel'] === 'char'){
	$sms['sender'] = $sender;
}


//Вызов API оператора
$postdata = json_encode(
	array(
		'apiKey' => $api_key,
		'sms' => array($sms)
	)
);


$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "https://new.smsgorod.ru/apiSms/create");
curl_setopt($curl, CURLOPT_HTTPHEADER, array('Content-Type: application/json', 'accept: application/json'));
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_POST, true);
curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 20); 
curl_setopt($curl, CURLOPT_TIMEOUT, 20);
curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($curl, CURLOPT_FOLLOWLOCATION, 1);
$curl_result = curl_exec($curl);
curl_close($curl);


$log = fopen("log.txt", "w");
fwrite($log, "https://new.smsgorod.ru/apiSms/create"."\n".json_encode($postdata)."\n".$curl_result."\n".curl_error($curl));
fclose($log);


$curl_result = json_decode($curl_result, true);


//Обработка ответа
if($curl_result['status'] == 'success')
{
	if($curl_result['data'][0]['status'] == 'sent'){
		// Запрос выполнился
		$answer = array();
		$answer["status"] = true;
		$answer["message"] = "";
		exit( json_encode($answer) );
	}else{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = $curl_result['data'][0]['errorDescription'];
		exit( json_encode($answer) );
	}
} 
else 
{
	// Запрос не выполнился (возможно ошибка авторизации, параметрах, итд...)
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4681).".";
	exit( json_encode($answer) );
}
?>