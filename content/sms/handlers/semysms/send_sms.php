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
$sms_api_query->execute( array('semysms') );
$sms_api = $sms_api_query->fetch();
$parameters_values = json_decode($sms_api["parameters_values"], true);

$phone = $_POST['main_field'];

// Удалить символ '+' если он присутствует в начале номера
if (substr($phone, 0, 1) === '+') {
    $phone = substr($phone, 1);
}

// Проверить количество символов в номере
if (strlen($phone) === 10) {
    // Если номер состоит из 10 символов, добавить '7' в начало
    $phone = '7' . $phone;
} elseif (substr($phone, 0, 1) === '8') {
    // Если номер начинается с '8', заменить '8' на '7'
    $phone = '7' . substr($phone, 1);
}

$options = http_build_query(
    array(
        "token" => $parameters_values["token"],
        "device" => $parameters_values["device"],
        "phone" => $phone,
        "msg" => $_POST["body"]
    )
);

//Вызов API оператора
$curl = curl_init();

curl_setopt_array($curl, array(
  CURLOPT_URL => 'https://semysms.net/api/3/sms.php?' . $options,
  CURLOPT_RETURNTRANSFER => true,
  CURLOPT_ENCODING => '',
  CURLOPT_MAXREDIRS => 10,
  CURLOPT_TIMEOUT => 0,
  CURLOPT_FOLLOWLOCATION => true,
  CURLOPT_HTTP_VERSION => CURL_HTTP_VERSION_1_1,
  CURLOPT_CUSTOMREQUEST => 'GET',
));

$response = curl_exec($curl);
/*
$log = fopen("log.txt", "w");
fwrite($log, "https://sms.ru/sms/send?api_id=$api_id&to=$main_field&msg=$body&json=1&translit=$translit"."\n".$curl_result_str.curl_error($curl));
fclose($log);
*/

curl_close($curl);

//Обработка ответа
$json = json_decode($response, true);
if ($json) 
{
	// Получен ответ от сервера
    if ($json["code"] == 0) 
	{ 
		// Сообщение отправлено
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
		$answer["message"] = translate_str_by_id(4680)." ".$json["code"].", ".$json["error"];
		exit( json_encode($answer) );
    }
}
else 
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4682)." ".$curl_result_str;
	exit( json_encode($answer) );
}
?>