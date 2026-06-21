<?php
//Скрипт отправки SMS
exit;
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
$sms_api_query->execute( array('smsaero') );
$sms_api = $sms_api_query->fetch();
$parameters_values = json_decode($sms_api["parameters_values"], true);


//Данные для отправки
//$subject = urlencode($_POST["subject"]);
$body = urlencode($_POST["body"]);
$main_field = urlencode( str_replace("+","",$_POST['main_field']) );//+ из строки телефона удаляем
$login = urlencode($parameters_values["login"]);
$password = urlencode(md5($parameters_values["password"]));
$from = urlencode($parameters_values["from"]);
$testsend = (int)$parameters_values["testsend"];


//Вызов API оператора
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "https://gate.smsaero.ru/send/?user=$login&password=$password&to=$main_field&text=$body&from=$from&testsend=$testsend&answer=json");
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
$curl_result_str = curl_exec($curl);
$curl_result_str = str_replace(array(" ","	","\n","\t","\r"),"",$curl_result_str);


/*
$log = fopen("log.txt", "w");
fwrite($log, "https://gate.smsaero.ru/send/?user=$login&password=$password&to=$main_field&text=$body&from=$from&testsend=$testsend");
fclose($log);
*/

curl_close($curl);


//Обработка ответа
$curl_result = json_decode($curl_result_str, true);
if( empty($curl_result["result"]) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Формат ответа SMS оператора не соответствует описанию протокола. ".$curl_result_str;
	exit( json_encode($answer) );
}
else
{
	if($curl_result["result"] == "accepted")
	{
		$answer = array();
		$answer["status"] = true;
		$answer["message"] = "";
		exit( json_encode($answer) );
	}
	else
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = $curl_result["result"];
		exit( json_encode($answer) );
	}
}
?>