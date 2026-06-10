<?php
//Скрипт для удаления временной папки с уникальным именем, в которую был предварительно загружен файл с прайс-листом
// -------------------------------------------------------------------------------
header('Content-Type: application/json; charset=utf-8');
//Функция очистки каталога ($clear_only: true - только очистить, false - удалить и сам каталог)
function clear_dir($dir, $clear_only) 
{
	foreach(glob($dir . '/*') as $file) 
	{
		if(is_dir($file))
		{
			clear_dir($file, false);
		}
		else
		{
			$file_name = explode("/", $file);
			$file_name = $file_name[ count($file_name) - 1 ];
			if( $file_name != "index.html" )
			{
				unlink($file);
			}
		}
	}
	if(!$clear_only)
	{
		rmdir($dir);
	}
}
// -------------------------------------------------------------------------------
//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
// -------------------------------------------------------------------------------
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'No DB Connect';
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
//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
// -------------------------------------------------------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('url'=>'shop/prices', 'is_frontend' => 0);
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
// -------------------------------------------------------------------------------
//Исходные данные
$tmp_folder_name_posted = $_POST['tmp_folder_name'];//Имя папки, которую нужно удалить
// -------------------------------------------------------------------------------
//Имя не должно содержать точек и других недопустимых символов. Только буквы в ниженем регистре, цифры и знак нижнего подчеркивания
$tmp_folder_name = preg_replace( "/[^a-z_0-9\s]/", '', $tmp_folder_name_posted );
// -------------------------------------------------------------------------------
if( $tmp_folder_name != $tmp_folder_name_posted )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Incorrect name';
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
//Полный путь к папке от корня
$tmp_folder_name = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir.$DP_Config->tmp_dir_prices_upload.'/'.$tmp_folder_name;
// -------------------------------------------------------------------------------
//Проверяем наличие папки
if( is_dir($tmp_folder_name) )
{
	clear_dir($tmp_folder_name, false);
	
	
	$answer = array();
	$answer["status"] = true;
	$answer["tmp_folder_name"] = $tmp_folder_name;
	$answer["message"] = translate_str_by_id(5347);
	exit(json_encode($answer));
}
else
{
	$answer = array();
	$answer["status"] = false;
	$answer["tmp_folder_name"] = $tmp_folder_name;
	$answer["message"] = translate_str_by_id(5348);
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
?>