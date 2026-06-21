<?php
//Скрипт отправки SMS

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
$body = $_POST["body"];
$main_field = urlencode( str_replace("+","",$_POST['main_field']) );//+ из строки телефона удаляем
$login = $parameters_values["login"];
$password = $parameters_values["password"];
$from = urlencode($parameters_values["from"]);
$testsend = (int)$parameters_values["testsend"];


$curl = curl_init();
$text = curl_escape($curl, $body);
$sign = $from;
$number = $main_field;

curl_setopt_array($curl, array(
  CURLOPT_URL => 'https://gate.smsaero.ru/v2/sms/send?text=' . $text . '&sign=' . $sign . '&number=' . $number,
  CURLOPT_RETURNTRANSFER => true,
  CURLOPT_ENCODING => '',
  CURLOPT_MAXREDIRS => 10,
  CURLOPT_TIMEOUT => 0,
  CURLOPT_FOLLOWLOCATION => true,
  CURLOPT_HTTP_VERSION => CURL_HTTP_VERSION_1_1,
  CURLOPT_CUSTOMREQUEST => 'GET',
  CURLOPT_USERPWD =>  $login . ":" . $password  
));

$response = curl_exec($curl);

curl_close($curl);

  
//Обработка ответа
$curl_result = json_decode($response, true);

if($curl_result["success"])
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
	$answer["message"] = $curl_result["message"];
	exit( json_encode($answer) );
}

?>