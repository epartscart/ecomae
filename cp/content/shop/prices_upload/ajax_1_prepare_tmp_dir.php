<?php
/**
 * Скрипт шага 1 "Предварительное создание или очистка временного каталога"
*/

//Конфигурация Treelax
require_once __DIR__ . '/epc_prices_ajax_init.php';


if($DP_Config->tech_key !== $_GET['key'])
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------

	//Для работы с пользователями
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
	//Проверяем доступ в панель управления
	if( ! DP_User::isAdmin())
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
}





$uploaddir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/pack_setup/";//Очищаемый каталог
clear_dir($uploaddir, true);//Функция очистки каталога (true - очистить, а сам каталог оставить)




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
?>
