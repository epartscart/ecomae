<?php
/*
Серверный скрипт для отправки запросов на сервер обновлений
*/
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
	$answer["status"] = "ERROR";
	$answer["message"] = "No DB connect";
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------



//Формируем строку запроса
$url = $DP_Config->update_server;//Сервер обновлений
$args_url = "";//Строка для аргументов
$args = json_decode($_POST["args"], true);//Объект запроса от страницы
foreach($args as $key => $value)
{
	if($args_url == "")
	{
		$args_url .= "?";
	}
	else
	{
		$args_url .= "&";
	}
	$args_url .= $key."=".urlencode($value);
}


//Делаем запрос
$ch = curl_init();
curl_setopt($ch, CURLOPT_URL, $url.$args_url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_HEADER, 0);
$curl_result = curl_exec($ch);
curl_close($ch);

//Возвращаем результат странице
exit($curl_result);
?>