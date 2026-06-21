<?php
//Серверный скрипт сохранения ОПИСАНИЯ строки

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
if( ! DP_User::isAdmin() )
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
- value
*/

if( !isset($_POST['str_key']) || !isset($_POST['value']) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Too few arguments";
	exit(json_encode($answer));
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

//Значение не допускается пустым
if( $_POST['value'] == '' )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Empty value does not acceptable";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Обновляем описание

if( $db_link->prepare("UPDATE `lang_text_strings` SET `description` = ? WHERE `str_key` = ?;")->execute( array( $_POST['value'], $_POST['str_key'] ) ) )
{
	$answer = array();
	$answer["status"] = true;
	$answer["str_key"] = $_POST['str_key'];
	$answer["message"] = "OK";
	$answer["new_value"] = htmlentities($_POST['value']);
	exit(json_encode($answer));
}
else
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2525);
	exit(json_encode($answer));
}
?>