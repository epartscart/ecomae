<?php
//Серверный скрипт получения текущего значения перевода определенной строки на определенном языке

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
*/
// -------------------------------------------------------------------------------

$translation_query = $db_link->prepare("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?;");
$translation_query->execute( array( $_POST['str_key'], $_POST['lang_code'] ) );
$translation_record = $translation_query->fetch();

$value = "";
if( $translation_record )
{
	$value = $translation_record['value'];
}


$answer = array();
$answer["status"] = true;
$answer["message"] = "OK";
$answer["value"] = $value;
exit(json_encode($answer));
?>