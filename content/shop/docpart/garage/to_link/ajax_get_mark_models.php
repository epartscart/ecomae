<?php
header('Content-Type: application/json;charset=utf-8;');
//Скрипт для асинхронного получения списка моделей выбранной марки
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS


//Получаем список моделей выбранной марки через веб-сервис каталога
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "http://ucats.ru/ucats/to/get_car_models.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&car_id=".(int)$_POST["to_mark"]);
curl_setopt($curl, CURLOPT_HEADER, 0);
curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
$curl_result = curl_exec($curl);
curl_close($curl);
$curl_result = json_decode($curl_result, true);

if($curl_result["status"] == "ok")
{
	$answer = array();
	$answer["status"] = true;
	$answer["list"] = $curl_result["list"];
	exit( json_encode($answer) );
}
else
{
	$answer = array();
	$answer["status"] = false;
	exit( json_encode($answer) );
}
?>