<?php
//Серверный скрипт для выставления параметра used_found "Использование строки найдено" (0 - не определено, 1 - использование найдено, 2 - использование не найдено)

/*
// -------------------------------------------------------------------------------
//Тестовая ошибка
$answer = array();
$answer["status"] = false;
$answer["message"] = "Test error";
exit(json_encode($answer));
// -------------------------------------------------------------------------------
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

/*
Параметры для запроса:
- str_key
- used_found
*/

if( !isset($_POST['used_found']) || !isset($_POST['str_key']) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Too few arguments";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Проверка used_found (либо 1, либо 0, либо 2)
$_POST['used_found'] = (int)$_POST['used_found'];

if( $_POST['used_found'] != 1 && $_POST['used_found'] != 0 && $_POST['used_found'] != 2 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Incorrect value of used_found";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Проверка наличия строки
$str_check_query = $db_link->prepare("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?;");
$str_check_query->execute( array($_POST["str_key"]) );
if( $str_check_query->fetchColumn() != 1 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "No such string";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Указываем нужное значение

if( $db_link->prepare("UPDATE `lang_text_strings` SET `used_found` = ? WHERE `str_key` = ?;")->execute( array($_POST['used_found'], $_POST['str_key']) ) )
{
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "";
	$answer['str_key'] = $_POST['str_key'];
	$answer['used_found'] = $_POST['used_found'];
	exit(json_encode($answer));
}
else
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "SQL error";
	exit(json_encode($answer));
}
?>