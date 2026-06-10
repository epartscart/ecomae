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
//Получаем настройки SMS-оператора
$sms_api_query = $db_link->prepare('SELECT * FROM `sms_api` WHERE `handler` = ?;');
$sms_api_query->execute( array('sms_ru') );
$sms_api = $sms_api_query->fetch();
$parameters_values = json_decode($sms_api["parameters_values"], true);



//Данные для отправки
//$subject = urlencode($_POST["subject"]);
$body = urlencode($_POST["body"]);
$main_field = urlencode( str_replace("+","",$_POST['main_field']) );//+ из строки телефона удаляем
$api_id = urlencode($parameters_values["api_id"]);
$translit = $parameters_values["translit"];


//Вызов API оператора
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "https://sms.ru/sms/send?api_id=$api_id&to=$main_field&msg=$body&json=1&translit=$translit");
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
$curl_result_str = curl_exec($curl);
$curl_result_str = str_replace(array(" ","	","\n","\t","\r"),"",$curl_result_str);

/*
$log = fopen("log.txt", "w");
fwrite($log, "https://sms.ru/sms/send?api_id=$api_id&to=$main_field&msg=$body&json=1&translit=$translit"."\n".$curl_result_str.curl_error($curl));
fclose($log);
*/




curl_close($curl);








//Обработка ответа
$json = json_decode($curl_result_str, true);
if ($json) 
{
	// Получен ответ от сервера
    if ($json["status"] == "OK") 
	{ 
		// Запрос выполнился
        foreach ($json["sms"] as $phone => $data) 
		{
			// Перебираем массив СМС сообщений
            if ($data["status"] == "OK") 
			{
				// Сообщение отправлено
                $answer = array();
				$answer["status"] = true;
				$answer["message"] = "";
				exit( json_encode($answer) );
            } 
			else 
			{
				// Ошибка в отправке
				$answer = array();
				$answer["status"] = false;
				$answer["message"] = translate_str_by_id(4683)." ".$json["status_code"].", ".$json["status_text"];
				exit( json_encode($answer) );
            }
        }
    } 
	else 
	{
		// Запрос не выполнился (возможно ошибка авторизации, параметрах, итд...)
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(4680)." ".$json["status_code"].", ".$json["status_text"];
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