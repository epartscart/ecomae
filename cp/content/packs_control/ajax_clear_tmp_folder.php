<?php
/**
 * Скрипт очистки временного каталога
*/
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");

//0. ПОДКЛЮЧЕНИЕ К БД
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	exit(json_encode("No DB connect"));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('id'=>241, 'url'=>'packs');//Корневой раздел "Пакеты"
$pages_to_check[] = array('id'=>242, 'url'=>'packs/packs_manager');//Менеджер пакетов
$pages_to_check[] = array('id'=>247, 'url'=>'packs/setup');//Установить пакет
$pages_to_check[] = array('id'=>248, 'url'=>'packs/pack_control');//Управление пакетом
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/check_admin_access/check_admin_access.php");
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------------------


//1. ПРОВЕРКА ПРАВ НА ЗАПУСК СКРИПТА
$check_authentication_query = $db_link->prepare( "SELECT COUNT(*) FROM `sessions` WHERE `session`= ? AND `type` = ?;" );
$check_authentication_query->execute( array($_COOKIE["admin_session"], 1) );

if( $check_authentication_query->fetchColumn() == 0)
{
    exit("No access");
}
else if($check_authentication_query->fetchColumn() != 1)
{
    exit("Session duplication");
}

// - проверка пройдена

$uploaddir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/pack_setup/";//Очищаемый каталог
clear_dir($uploaddir, true);//Функция очистки каталога (true - очистить, а сам каталог оставить)

echo json_encode("Success");
exit();



// -----------------------------------------------------------------------------------------------


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