<?php
header('Content-Type: application/json;charset=utf-8;');
//Серверный скрипт для получения списка комплектаций модели
//Скрипт для асинхронного получения списка моделей выбранной марки
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS


//Получаем данные:
$model_id_to = (int)$_POST["to_model"];

//Получаем список моделей выбранной марки через веб-сервис каталога
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, "http://ucats.ru/ucats/to/get_types.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&model_id=$model_id_to");
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