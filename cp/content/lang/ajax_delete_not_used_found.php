<?php
/*
Серверный скрипт удаления текстовых строк, у которых:
- used_found выставлен в 2 (использование не найдено)
- is_custom выставлен в 1 (строка является кастомной, т.е. пользовательской)

Таким образом, удаляются только те строки, которые являются пользовательскими и использование которых не найдено. Кроме самих строк, также удаляются и их переводы.
*/

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

//Работаем через транзакцию
try
{
	//Старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception("Could not start the transaction");
	}
	
	//Сначала удаляем переводы неиспользуемых строк
	if( ! $db_link->prepare("DELETE FROM `lang_text_strings_translation` WHERE `str_key` IN (SELECT `str_key` FROM `lang_text_strings` WHERE `is_custom` = ? AND `used_found` = ?);")->execute( array(1, 2) ) )
	{
		throw new Exception("Error deleting translations");
	}
	
	//Теперь удаляем сами неиспользуемые строки
	if( ! $db_link->prepare("DELETE FROM `lang_text_strings` WHERE `is_custom` = ? AND `used_found` = ?;")->execute( array(1, 2) ) )
	{
		throw new Exception("Error deleting strings");
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