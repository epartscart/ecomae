<?php
/**
 * Очередной шаг общего алгоритма загрузки прайс-листов "Обработка файлов csv" (удаление кавычек и т.д.)
*/
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
		$answer["message"] = 'Forbibben';
		exit(json_encode($answer));
	}
}


//Временный каталог
$work_dir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir.$DP_Config->tmp_dir_prices_upload;

$price_id = (int) $_GET['price_id'];

$separator = ';';
$encoding = 'UTF-8';
$query = $db_link->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ?;');
$query->execute(array($price_id));
$record = $query->fetch();
if(!empty($record['separator'])){
	$separator = $record['separator'];
}
if(!empty($record['encoding'])){
	$encoding = $record['encoding'];
}

$status = 0;

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
		if(strripos(strtolower($obj), ".csv") != false || strripos(strtolower($obj), ".txt") != false)
		{
			if(strlen(strtolower($obj))-strlen(".csv") == strripos(strtolower($obj), ".csv") || strlen(strtolower($obj))-strlen(".txt") == strripos(strtolower($obj), ".txt"))
			{
			    //ОБРАБАТЫВАЕМ ФАЙЛ
				
				$status = 1;
				
                $f = file_get_contents($work_dir."/".$obj);
				
				if($encoding != 'UTF-8'){
					if($encoding == 'Windows-1251 (ANSI)'){
						$f = iconv("WINDOWS-1251", "UTF-8", $f);
					}
				}
				
				// Если в файле разделители табуляция то переводим их в точку с запятой
				if($separator != ';'){
					$f = str_replace(';',',',$f);
					if('\t' == $separator){
						$f = str_replace("\t",';',$f);
					}else{
						$f = str_replace($separator,';',$f);
					}
				}
				
                file_put_contents($work_dir."/".$obj, $f);
			}
			else
			{
				continue;
			}
		}
		else
		{
			continue;
		}
	}//else 1
}//~while 1
closedir($dh);//Закрываем каталог



$answer = array();//Объект ответа
$answer["result"] = 1;
exit(json_encode($answer));







// Далее определения методов:
// ***************************************************************************************************************
// ---------------------------------------------------- 
//Функция для определения кодировки
if ( !function_exists('mb_detect_encoding') ) 
{
// ---------------------------------------------------------------- 
    function mb_detect_encoding ($string, $enc=null, $ret=null) 
    { 
        static $enclist = array( 
            'UTF-8', 'ASCII', 
            'ISO-8859-1', 'ISO-8859-2', 'ISO-8859-3', 'ISO-8859-4', 'ISO-8859-5', 
            'ISO-8859-6', 'ISO-8859-7', 'ISO-8859-8', 'ISO-8859-9', 'ISO-8859-10', 
            'ISO-8859-13', 'ISO-8859-14', 'ISO-8859-15', 'ISO-8859-16', 
            'Windows-1251', 'Windows-1252', 'Windows-1254', 
            );
        
        $result = false; 
        
        foreach ($enclist as $item) 
        { 
            $sample = iconv($item, $item, $string); 
            if (md5($sample) == md5($string)) 
            { 
                if ($ret === NULL) 
                { 
                    $result = $item; 
                } 
                else 
                { 
                    $result = true; 
                } 
                break; 
            }
        }
            
        return $result; 
    } 
// ---------------------------------------------------------------- 
}
// *********************************************************************************
// ------------------------------------------------------ 
//Конвертирование в UTF-8
function str_to_utf8 ($str) 
{
    if (mb_detect_encoding($str, 'UTF-8', true) === false) 
    { 
        //Если кодировка не UTF-8, то перегоняем строку в UTF-8
        
        $str = iconv("CP1251", "UTF-8", $str);
        
        //$str = utf8_encode($str); 
    }

    return $str;
}
// ------------------------------------------------------ 
?>