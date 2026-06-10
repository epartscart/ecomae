<?php
//Скрипт для разворачивания pyprices
/*
1. Определяем наличие Python подходящей версии
2. Разворачиваем виртуальное окружение в pyprices
3. Для api.py присваиваем права на запуск
4. Пробуем обратиться к api.py через CURL
*/
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
/*
//START - Для отладки
sleep(3);
$answer = array();
$answer["status"] = true;
$answer["message"] = 'Debug message';
exit(json_encode($answer));
//END - Для отладки
*/
// -------------------------------------------------------------------------------
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
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------

//Первым делом нужно определить, что есть Python версии 3.6 и выше.

$python_cli_pref = "";//Сюда запишем префикс python для командной строки (один из массива $python_cli_pref_try)
$python_available = false;//Флаг - подходящий Python доступен

//Массив с вариантами префиксов, которые будем проверять
$python_cli_pref_try = array("", "3", "3.6", "3.7", "3.8", "3.9", "3.10", "3.11");

//Ищем первую попавшуюся подходяшую версию Python используя разные префиксы командной строки
for( $i=0 ; $i < count($python_cli_pref_try) ; $i++ )
{
	$output=null;
	$retval=null;
	
	exec('python'.$python_cli_pref_try[$i].' -V', $output, $retval);
	
	//Если код возарата 0, значит exec отработала без ошибки
	if( !$retval )
	{
		if( is_array($output) )
		{
			if( isset($output[0]) )
			{
				//Пробуем получить вывод в виде "Python 3.8.5"
				$python_V_output = $output[0];
				
				//Из этой строки пробуем получить версию Python
				$python_V_output = explode(" ", $python_V_output);
				
				if( isset($python_V_output[1]) )
				{
					$python_V_output = explode(".", $python_V_output[1]);
					
					if( count($python_V_output) >= 2 && count($python_V_output) <=3 )
					{
						if( $python_V_output[0] == 3 && $python_V_output[1] >= 6 )
						{
							//Есть подходящая версия Python
							$python_cli_pref = $python_cli_pref_try[$i];//Префикс командной строки для нее
							$python_available = true;//Флаг - Есть подходящая версия Python
							break;
						}
					}
				}
			}
		}
	}
}



if( ! $python_available )
{
	$answer = array();
	$answer['status'] = false;
	$answer['message'] = translate_str_by_id(5387);
	exit( json_encode($answer) );
}



//Дошли до сюда, далее пробуем развернуть виртуальное окружение в pyprices и установить необходимые модули. Для этого нужно выполнить ряд команд в консоли. Результат выполнения команд никак здесь не проверяем.


$cli_commands = array();
$cli_commands[] = 'python'.$python_cli_pref.' -m venv pyprices';//Развернуть виртуальное окружение
$cli_commands[] = 'python'.$python_cli_pref.' -m pip install --upgrade pip setuptools wheel';//Обновление инструментов pip
//Далее - команды на установку модулей
$cli_commands[] = 'pip'.$python_cli_pref.' install mysql-connector-python==8.0.28';
$cli_commands[] = 'pip'.$python_cli_pref.' install imap-tools';
$cli_commands[] = 'pip'.$python_cli_pref.' install patool';
$cli_commands[] = 'pip'.$python_cli_pref.' install rarfile';
$cli_commands[] = 'pip'.$python_cli_pref.' install py7zr';
$cli_commands[] = 'pip'.$python_cli_pref.' install price_parser';
$cli_commands[] = 'pip'.$python_cli_pref.' install openpyxl';
$cli_commands[] = 'pip'.$python_cli_pref.' install xlrd==1.2.0';



//Последовательно выполняем эти команды. Для выполнения каждой команды переходим в папку с pyprices и активируем виртуальное окружение (начиная со второй команды)
for( $i = 0 ; $i < count($cli_commands) ; $i++)
{
	$output=null;
	$retval=null;
	
	//Активировать виртуальное окружение - отдельная команда, которая нужна уже после создания самого виртуального окружения (после первой команды)
	$cli_command_activate = '&& source pyprices/bin/activate';
	if( $i == 0 )
	{
		$cli_command_activate = '';
	}
	
	exec('cd '.$_SERVER['DOCUMENT_ROOT'].'/pyprices '.$cli_command_activate.' && '.$cli_commands[$i], $output, $retval);
}



//Виртуальное окружение должно быть развернуто. Далее выставляем права на api.py
$output=null;
$retval=null;
exec('cd '.$_SERVER['DOCUMENT_ROOT'].'/pyprices && chmod 755 api.py', $output, $retval);



//Теперь всё должно работать. Проверяем
$curl_result = file_get_contents( $DP_Config->domain_path.'pyprices/api.py' );
$curl_result = json_decode(trim($curl_result), true);
//Если pyprices работает, то, в ответе должны быть поля
if( isset($curl_result['status']) && isset($curl_result['list_to_handle']) )
{
	$answer = array();
	$answer['status'] = true;
	$answer['message'] = "OK";
	exit( json_encode($answer) );
}
else
{
	$answer = array();
	$answer['status'] = false;
	$answer['message'] = translate_str_by_id(5388);
	exit( json_encode($answer) );
}
?>