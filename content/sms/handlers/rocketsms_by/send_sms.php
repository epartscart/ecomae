<?php
//Скрипт отправки SMS

/*

http://cab.rocketsms.by/

INSERT INTO `sms_api` (`id`, `name`, `parameters`, `parameters_values`, `description`, `active`, `handler`)VALUES (NULL , 'rocketsms.by', '[{"name":"login","type":"text","caption":"Логин"},{"name":"pass","type":"text","caption":"Пароль"}]', '', '', '0', 'rocketsms_by');

*/


// Записываем лог запроса
$f = fopen('log_'.date("m_Y", time()).'.txt', 'a');
fwrite($f, date("d-m-Y H:i:s", time())."\n");
fwrite($f, json_encode($_POST)."\n\n");


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
$sms_api_query->execute( array('rocketsms_by') );
$sms_api = $sms_api_query->fetch();
$parameters_values = json_decode($sms_api["parameters_values"], true);


//Данные для доступа
$login = $parameters_values["login"];
$pass = $parameters_values["pass"];


//Данные для отправки
//$subject = urlencode($_POST["subject"]);
$body = urlencode($_POST["body"]);
$main_field = urlencode( str_replace("+","",$_POST['main_field']) );//+ из строки телефона удаляем


//Вызов API оператора
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "http://api.rocketsms.by/json/send");
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, false);
curl_setopt($curl, CURLOPT_POST, true);
curl_setopt($curl, CURLOPT_POSTFIELDS, "username=$login&password=$pass&phone=$main_field&text=$body");
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
$result = curl_exec($curl);
curl_close($curl);


// Записываем лог запроса
fwrite($f, json_encode($result)."\n\n__________________________________________________\n\n");


// Обрабатываем результат
$result = @json_decode($result, true);


if($result && isset($result['id'])){
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "";
	exit( json_encode($answer) );
}elseif($result && isset($result['error'])){
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4680)." ".$result['error'];
	exit( json_encode($answer) );
}else{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4681).". Service error";
	exit( json_encode($answer) );
}
?>