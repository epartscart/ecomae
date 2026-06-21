<?php
// Скрипт отправки SMS
// https://www.smsvizitka.com/integration
// Сообщения приходят только на номер с 8

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
$sms_api_query->execute( array('smsvizitka_com') );
$sms_api = $sms_api_query->fetch();
$parameters_values = json_decode($sms_api["parameters_values"], true);


//Данные для отправки
$body = urlencode($_POST["body"]);
$main_field = urlencode( str_replace("+","",$_POST['main_field']) );//+ из строки телефона удаляем
$api_key = urlencode($parameters_values["api_key"]);



//Данные для отправки
//$body = 'test';
//$main_field = '';



//Вызов API оператора
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "http://crm.smsvizitka.com/api/send-sms?api_key=$api_key&to=$main_field&text=$body");
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($curl, CURLOPT_FOLLOWLOCATION, 1);
curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($curl, CURLOPT_HEADER , 1);
$curl_result_str = curl_exec($curl);
//$curl_result_str = str_replace(array(" ","	","\n","\t","\r"),"",$curl_result_str);

$sent_headers = curl_getinfo($curl, CURLINFO_HEADER_OUT);

/*$log = fopen("log.txt", "w");
fwrite($log, "http://crm.smsvizitka.com/api/send-sms?api_key=$api_key&to=$main_field&text=$body"."\n".$curl_result_str."\n".$curl_result_str.curl_error($curl));
fclose($log);*/

curl_close($curl);


//echo '<pre>';
//var_dump($curl_result_str);





//Обработка ответа
if(strpos($curl_result_str, '200 OK'))
{
	// Запрос выполнился
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "";
	exit( json_encode($answer) );
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