<?php
//Серверный скрипт для поиска использования текстовых строк

// -------------------------------------------------------------------------------

//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

// -------------------------------------------------------------------------------

//Ограниченный режим редактора. Действие не выполняем
if( $DP_Config->multilang_editor_restricted_mode )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Editor works in restricted mode. Action was canceled";
	exit( json_encode($answer) );
}

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
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
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

//Проверяем право менеджера
if( ! DP_User::isAdmin())
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit(json_encode($answer));//Вообще не является администратором бэкенда
}


// ------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('url'=>'lang/editor', 'is_frontend' => 0);//Редактор переводов текстовых строк
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
// ------------------------------


// -------------------------------------------------------------------------------
//Поиск использования строк пока осуществляется только в таблицах БД. Для поиска используем массив с описанием: таблицы, колонки и тип (равно или LIKE). LIKE - используется для тех колонок, в которых хранятся структуры в JSON.

//Подключаем массив с описанием
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/lang/lang_tabs_cols.php");

// -------------------------------------------------------------------------------

//Работаем через транзакцию
try
{
	//Старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception("Could not start the transaction");
	}
	
	
	//Предварительно везде устанавливаем used_found = 0 (использование сроки не определено)
	if( !$db_link->prepare("UPDATE `lang_text_strings` SET `used_found` = ? WHERE ?;")->execute( array(0, 1) ) )
	{
		throw new Exception("Could preset used_found to 0");
	}
	
	
	
	//Цикл по каждой текстовой строке
	$all_strings_query = $db_link->prepare("SELECT `str_key` FROM `lang_text_strings`;");
	$all_strings_query->execute();
	while( $string = $all_strings_query->fetch() )
	{
		$str_key = $string['str_key'];
		
		$used_found = 0;//Флаг - использование найдено (0 - не нашли, 1 - нашли)
		
		//Цикл по массиву $lang_tabs_cols - ищем использование данной строки. Если наши - ставим 1, если нигде не нашли - ставим - 2
		
		//По таблицам
		foreach( $lang_tabs_cols AS $table_name => $cols)
		{
			//По колонкам таблиц
			foreach( $cols AS $col_name => $value_type )
			{
				/*
				По безопасности SQL-запросов:
				- $table_name хранится в php-скрипте
				- $col_name - хранится в php-скрипте
				- $str_key - генерируется функцией
				Таким образом, пользователь без достаточных привелегий не сможет подставить опасные значения
				*/
				
				//В зависимости от типа значения (text или json)
				if( $value_type == 'text' )
				{
					$search_str_query = $db_link->prepare("SELECT * FROM `".$table_name."` WHERE `".$col_name."` = '".$str_key."' LIMIT 1;");
				}
				else
				{
					$search_str_query = $db_link->prepare("SELECT * FROM `".$table_name."` WHERE `".$col_name."` LIKE '%\"".$str_key."\"%' LIMIT 1;");
				}
				
				//Делаем запрос
				if( ! $search_str_query->execute() )
				{
					throw new Exception("Error searching string");
				}
				
				if( $search_str_query->fetch() )
				{
					//Использование найдено
					$used_found = 1;
					break;//Из цикла по колонкам таблицы
				}
			}//~foreach($col)
			
			
			if( $used_found == 1 )
			{
				break;//Из цикла по таблицам
			}
		}//~foreach($lang_tabs_cols)
		
		
		
		//В колонке used_found возможны три значения. 0 - не определено (т.е. не искали), 1 - нашли использование, 2 - не нашли использование
		if( $used_found == 0 )
		{
			$used_found = 2;
		}
		
		//Выставляем значение
		if( ! $db_link->prepare("UPDATE `lang_text_strings` SET `used_found` = ? WHERE `str_key` = ?;")->execute( array($used_found, $str_key) ) )
		{
			throw new Exception("Error updating used_found for str_key ".$str_key);
		}
	}
}
catch (Exception $e)
{
	//Откатываем все изменения
	$db_link->rollBack();
	
	
	//Если возникла какая-либо ошибка, сразу выходим и предупреждаем пользователя, что не стоит запускать процесс удаления, т.к. не известно точно, используется ли та или иная строка
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Error: ".$e->getMessage();
	exit(json_encode($answer));
}

//Дошли до сюда, значит выполнено ОК
$db_link->commit();//Коммитим все изменения и закрываем транзакцию

$answer = array();
$answer["status"] = true;
$answer["message"] = "OK";
exit(json_encode($answer));
?>