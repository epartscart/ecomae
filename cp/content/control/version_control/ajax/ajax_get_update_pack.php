<?php
/**
Серверный скрипт - получает пакет обновления и распаковывает его в каталог tmp/updates
*/
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/PclZip/pclzip.lib.php");//Библиотека для работы с zip


$update_id = $_GET["update_id"];//ID требуемого пакета обновления

//Конфигурация
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
	$answer["status"] = "ERROR";
	$answer["message"] = "No DB connect";
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------



// ------------------------------------------------------------------------------------------------
//0. ПРЕДВАРИТЕЛЬНАЯ ОЧИСТКА КАТАЛОГА
$uploaddir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/updates/";//Очищаемый каталог
clear_dir($uploaddir, true);//Функция очистки каталога (true - очистить, а сам каталог оставить)
// ------------------------------------------------------------------------------------------------
//0. Проверка каталога загрузки обновлений (он должен быть пустым)
if ($handle = opendir($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/updates")) 
{
    while (false !== ($entry = readdir($handle))) 
	{
        if ($entry != "." && $entry != "..") 
		{
			closedir($handle);
            $answer = array();
			$answer["status"] = "ERROR";
			$answer["message"] = translate_str_by_id(2496);
			exit( json_encode($answer) );
        }
    }
    closedir($handle);
}
// ------------------------------------------------------------------------------------------------
//1. ПОЛУЧЕНИЕ АДРЕСА ПАКЕТА

//Формируем строку запроса к серверу обновлений для получения адреса пакета
$url = $DP_Config->update_server."?query=get_pack_url&update_id=".$update_id;

//Делаем запрос
$ch = curl_init();
curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_HEADER, 0);
$curl_result = curl_exec($ch);
curl_close($ch);

$curl_result = json_decode($curl_result, true);
if($curl_result["status"] != "OK")
{
	$answer = array();
	$answer["status"] = "ERROR";
	$answer["message"] = translate_str_by_id(2497);
	exit( json_encode($answer) );
}

$pack_url = $curl_result["pack_url"];
$pack_file_name = $curl_result["pack_file_name"];
// ------------------------------------------------------------------------------------------------
//2. СКАЧИВАНИЕ ПАКЕТА
if(file_put_contents($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/updates/".$pack_file_name, fopen($pack_url, 'r')) == 0)
{
	$answer = array();
	$answer["status"] = "ERROR";
	$answer["message"] = translate_str_by_id(2498);
	exit( json_encode($answer) );
}
if( ! file_exists($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/updates/".$pack_file_name) )
{
	$answer = array();
	$answer["status"] = "ERROR";
	$answer["message"] = translate_str_by_id(2498);
	exit( json_encode($answer) );
}
// ------------------------------------------------------------------------------------------------
//3. РАСПАКОВКА ПАКЕТА
$uploaddir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/updates/";//Директория для распаковки (где и сам архив)
$archive = new PclZip($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/updates/".$pack_file_name);
if($archive->extract(PCLZIP_OPT_PATH, $uploaddir) == 0)
{
    $answer = array();
	$answer["status"] = "ERROR";
	$answer["message"] = translate_str_by_id(2499);
	exit( json_encode($answer) );
}
// ------------------------------------------------------------------------------------------------
//УСПЕШНОЕ ВЫПОЛНЕНИЕ
$answer = array();
$answer["status"] = "OK";
exit( json_encode($answer) );
// ------------------------------------------------------------------------------------------------




// ------------------------------------------------------------------------------------------------
//Функция очистки каталога ($clear_only: true - только очистить, false - удалить и сам каталог)
function clear_dir($dir, $clear_only) 
{
	foreach(glob($dir . '/*') as $file) 
	{
		if(is_dir($file))
			clear_dir($file, false);
		else
			unlink($file);
	}
	if(!$clear_only)
	{
		rmdir($dir);
	}
}
?>