<?php
//Серверный скрипт сохранения. Сохраняет перевод определенной строки на определенном языке. Если перевода нет, то, он добавится

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

/*
Параметры для запроса:
- str_key
- lang_code
- value
*/

if( !isset($_POST['str_key']) || !isset($_POST['lang_code']) || !isset($_POST['value']) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Too few arguments";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Для ограниченного режима работы редактора. Запрещаем редактировать переводы для языков у которых выставлен соответствующий флаг
if( $DP_Config->multilang_editor_restricted_mode )
{
	$restrict_edit_query = $db_link->prepare("SELECT `restrict_edit` FROM `lang_languages` WHERE `lang_code` = ?;");
	$restrict_edit_query->execute( array($_POST['lang_code']) );
	if( $restrict_edit_query->fetchColumn() == 1 )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = "Editor works in restricted mode. Action was canceled";
		exit( json_encode($answer) );
	}
}

// -------------------------------------------------------------------------------

//Сначала проверяем наличие самой строки
$check_str_exist_query = $db_link->prepare("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?;");
$check_str_exist_query->execute( array($_POST['str_key']) );
if( $check_str_exist_query->fetchColumn() != 1 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "String not found";
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------

//Теперь проверяем наличие языка
$check_lang_exist_query = $db_link->prepare("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?;");
$check_lang_exist_query->execute( array($_POST['lang_code']) );
if( $check_lang_exist_query->fetchColumn() != 1 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Language not found";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Значение перевода не допускается пустым
if( $_POST['value'] == '' )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Empty value does not acceptable";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Проверяем, если уже перевод этой строки на указанный языке
$translation_exist_query = $db_link->prepare("SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?;");
$translation_exist_query->execute( array( $_POST['str_key'], $_POST['lang_code'] ) );
if( $translation_exist_query->fetchColumn() == 1 )
{
	//Перевод есть - UPDATE
	if( $db_link->prepare("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?;")->execute( array( $_POST['value'], $_POST['str_key'], $_POST['lang_code'] ) ) )
	{
		$answer = array();
		$answer["status"] = true;
		$answer["str_key"] = $_POST['str_key'];
		$answer["lang_code"] = htmlentities($_POST['lang_code']);
		$answer["value"] = htmlentities($_POST['value']);
		$answer["message"] = "OK";
		exit(json_encode($answer));
	}
	else
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(2525);
		exit(json_encode($answer));
	}
}
else
{
	//Перевода нет - INSERT
	if( $db_link->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`,`lang_code`,`value`) VALUES (?,?,?);")->execute( array( $_POST['str_key'], $_POST['lang_code'], $_POST['value'] ) ) )
	{
		$answer = array();
		$answer["status"] = true;
		$answer["str_key"] = $_POST['str_key'];
		$answer["lang_code"] = htmlentities($_POST['lang_code']);
		$answer["value"] = htmlentities($_POST['value']);
		$answer["message"] = "OK";
		exit(json_encode($answer));
	}
	else
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(2524);
		exit(json_encode($answer));
	}
}
?>