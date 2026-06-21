<?php
/**
 * Скрипт проверки уникальности и корректности контакта (при регистрации или замене)
*/
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация Docpart
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
	$answer["result"] = "undefined";
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");




// -------------------------------------------------------------------------------
// based on original work from the PHP Laravel framework
if (!function_exists('str_contains')) 
{
    function str_contains($haystack, $needle) {
        return $needle !== '' && mb_strpos($haystack, $needle) !== false;
    }
}
// -------------------------------------------------------------------------------



// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//Входящие данные:
$reg_contact = $_POST["reg_contact"];
$reg_contact_type = $_POST["reg_contact_type"];



//Имя колонки, в которой ищем контакт:
$col_name = 'email';//По-умолчанию - email
$col_caption = "E-mail";
if( $reg_contact_type == 'phone' )
{
	$col_name = 'phone';//Будем искать в колонке "Телефон"
	$col_caption = translate_str_by_id(1312);
}
else if( $reg_contact_type != 'email' )
{
	exit();//Значит было передано некорректное значение reg_contact_type
}



//Запрещаем использовать адреса разработчиков CMS - чтобы сайты клиентов не заваливали различными уведомлениями
if( $reg_contact_type == 'email' )
{
	if( str_contains($reg_contact, '@intask.pro') || str_contains($reg_contact, '@docpart.ru') || str_contains($reg_contact, '@docpart.net') )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(5641);
		exit( json_encode($answer) );
	}
}




//Проверяем корректность контакта
//Получаем регулярное выражение для контакта
$regexp_query = $db_link->prepare("SELECT `regexp` FROM `reg_fields` WHERE `name` = ?;");
$regexp_query->execute( array($reg_contact_type) );
$regexp = $regexp_query->fetchColumn();
preg_match("/".$regexp."/", $reg_contact, $matches);
$regexp_ok = true;
if($regexp != '') {
	if( count($matches) == 1 )
	{
		if( $matches[0] != $reg_contact )
		{
			$regexp_ok = false;
		}
	}
	else
	{
		$regexp_ok = false;
	}
}
if( !$regexp_ok )
{
	//Значение контакта не соответствует регулярному выражению
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4699)." ".$col_caption;
	exit( json_encode($answer) );
}



//Проверяем уникальность контакта
$contact_check_query = $db_link->prepare('SELECT COUNT(*) FROM `users` WHERE `'.$col_name.'`= ?;');//У col_name - безопасное значение
$contact_check_query->execute( array(htmlentities($reg_contact)) );

$contact_count_rows = $contact_check_query->fetchColumn();

if( $contact_count_rows != 0)
{
	//Такое поле уже есть - использовать нельзя
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4700)." ".$col_caption." ".translate_str_by_id(4701);
	exit( json_encode($answer) );
}
else if($contact_count_rows == 0)
{
	//Такого поля еще нет - можно использовать
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "Ok";
	exit( json_encode($answer) );
}
?>