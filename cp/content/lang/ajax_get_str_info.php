<?php
//Серверный скрипт получения информации о строке

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
*/

/*
Поля ответа:
- str_key
- id
- description
- same
- is_error
- is_custom
- used_found
- массив с флагами наличия переводом по языкам
*/

// -------------------------------------------------------------------------------
//Получем массив со всеми языками платформы
$languages = array();
$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
$languages_query->execute();
while( $language = $languages_query->fetch() )
{
	$languages[] = $language['lang_code'];
}

//Формируем запрос
$SQL = "SELECT *";

//Для каждого языка в платформе добавляем колонку has_<язык> (т.е. имеет ли строка перевод для данного языка)
for( $i=0 ; $i < count($languages); $i++ )
{
	//$languages[$i] - считаем безопасным, т.к. берется из таблицы lang_languages, которая никак пользователями не редактируется
	
	$SQL .= ", (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `lang_code` = '".$languages[$i]."' AND `str_key` = `lang_text_strings`.`str_key` ) AS `has_".$languages[$i]."` ";
}

$SQL .= "FROM `lang_text_strings` WHERE `str_key` = ?;";


$str_info_query = $db_link->prepare($SQL);
$str_info_query->execute( array($_POST['str_key']) );
$str_info = $str_info_query->fetch();

// -------------------------------------------------------------------------------

if($str_info)
{
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "OK";
	$answer["str_info"] = $str_info;
	exit(json_encode($answer));
}
else
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "SQL error getting string info";
	exit(json_encode($answer));
}
?>