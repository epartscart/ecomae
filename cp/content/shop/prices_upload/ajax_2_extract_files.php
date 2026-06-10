<?php
/**
 * Третий шаг общего алгоритма сессии загрузки файлов "Извлечение архивов"
 * 
 * Скрипт пробегает по всем файлам во временно каталоге и распаковывает найденные архивы. Распакованные архивы удаляются
*/
header('Content-Type: application/json;charset=utf-8;');
//-------Подключаем библиотеку для работы с архивами---------
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/PclZip/pclzip.lib.php");
//-----------------------------------------------------------

//Конфигурация Treelax
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
	$answer["message"] = 'No DB connect';
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");



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
$packs_count = 0;//Всего найдено архивов
$packs_successfully_extracted = 0;//Архивов успешно распаковано
$packs_error = 0;//Архивов не распаковано (ошибки)
//Пробегаем по содержимому временного каталога
while (false !== ($obj = readdir($dh)))
{
	if($obj=='.' || $obj=='..') 
	{
		continue;
	}
	else
	{
		if(strripos($obj, ".zip") != false)//Если содержит .zip
		{
			if(strlen($obj)-strlen(".zip") == strripos($obj, ".zip"))//и при этом .zip - конец имени файла, то это zip-архив
			{
			    $packs_count++;//Счетчик архивов
			    
				$arhive_name_zip = $work_dir."/".$obj;//Полный путь к архиву, в т.ч. имя файла 
				$path_dir_zip = $work_dir."/";//Куда распаковывать - в сам временный каталог
				
				$archive = new PclZip($arhive_name_zip);//Объект PclZip
                $result = $archive->extract(PCLZIP_OPT_PATH, $path_dir_zip);//Распаковка архива
                if($result == 0) 
                {
                    //Обработать ошибку
                    $packs_error++;
                }
                else
                {
                    //Обработать успех
                    $packs_successfully_extracted++;
                }
                unlink($arhive_name_zip);//Удаляем архив
			}
		}
		if(strripos($obj, ".rar") != false)//Если содержит .rar
		{
			if(strlen($obj)-strlen(".rar") == strripos($obj, ".rar"))//и при этом .rar - конец имени файла, то это rar-архив
			{
			    $packs_count++;//Счетчик архивов
			    
				$arhive_name_rar = $work_dir."/".$obj;//Полный путь к архиву, в т.ч. имя файла 
				$path_dir_rar = $work_dir."/";//Куда распаковывать - в сам временный каталог
				
				$archive = rar_open($arhive_name_rar);
				$list = rar_list($archive);

				foreach($list as $file) {
					$entry = rar_entry_get($archive, $file->getName());
					$result = $entry->extract($path_dir_rar); // extract to the current dir
					if($result == 0) 
                    {
                        //Обработать ошибку
                        $packs_error++;
                    }
                    else
                    {
                        //Обработать успех
                        $packs_successfully_extracted++;
                    }
				}
				rar_close($archive);
				
                unlink($arhive_name_rar);//Удаляем архив
			}
		}
	}//else 1
}//~while 1
closedir($dh);//Закрываем каталог



$answer = array();//Объект ответа
$answer["status"] = true;
$answer["packs_count"] = $packs_count;
$answer["packs_error"] = $packs_error;
$answer["packs_successfully_extracted"] = $packs_successfully_extracted;

exit(json_encode($answer));
?>