<?php
/**
 * Очередной шаг общего алгоритма загрузки прайс-листа "Конвертирование файлов Excel в CSV"
*/

//-------Подключаем библиотеку для работы с файлами Excel---------
//require_once($_SERVER["DOCUMENT_ROOT"]."/lib/PHPExcel/PHPExcel.php");
//-----------------------------------------------------------
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация Treelax
require_once __DIR__ . '/epc_prices_ajax_init.php';


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


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

//Временный каталог
$work_dir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir.$DP_Config->tmp_dir_prices_upload;



$dh = opendir($work_dir);//Открываем временный каталог
//Пробегаем по содержимому временного каталога
while (false !== ($obj = readdir($dh)))
{
	if($obj=='.' || $obj=='..') 
	{
		continue;
	}
	else
	{
		$excel_note = translate_str_by_id(3484)." <a href=\"https://intask.pro/\" target=\"_blank\" style=\"text-decoration:underline;font-weight:bold;color:#33C;\">intask.pro</a> ".translate_str_by_id(3685);
		
		if(strripos($obj, ".xls") != false)//Если содержит .xls
		{
			if(strlen($obj)-strlen(".xls") == strripos($obj, ".xls"))
			{
			    $answer = array();//Объект ответа
                $answer["result"] = 0;
                $answer["message"] = $excel_note;
                closedir($dh);//Закрываем каталог
                exit(json_encode($answer));
			}
		}
		if(strripos($obj, ".xlsx") != false)//Если содержит .xlsx
		{
			if(strlen($obj)-strlen(".xlsx") == strripos($obj, ".xlsx"))
			{
			    $answer = array();//Объект ответа
                $answer["result"] = 0;
                $answer["message"] = $excel_note;
                closedir($dh);//Закрываем каталог
                exit(json_encode($answer));
			}
		}
	}//else 1
}//~while 1
closedir($dh);//Закрываем каталог



$answer = array();//Объект ответа
$answer["result"] = 1;
exit(json_encode($answer));
?>