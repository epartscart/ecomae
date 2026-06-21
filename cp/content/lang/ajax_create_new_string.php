<?php
//Серверный скрипт создания новой строки

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
- description (описание создаваемой строки)
- same (является одинаковой для всех языков)
- is_error (является сообщением об ошибке)
*/

if( !isset($_POST['description']) || !isset($_POST['same']) || !isset($_POST['is_error']) || !isset($_POST['is_custom']) || !isset($_POST['used_found']) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Too few arguments";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Проверка is_error (либо 1, либо 0)
$_POST['is_error'] = (int)$_POST['is_error'];

if( $_POST['is_error'] != 1 && $_POST['is_error'] != 0 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Incorrect value of is_error";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Проверка same (либо 'no', либо существующий lang_code)
if( $_POST['same'] != 'no' )
{
	//Значит должен быть какой-то из языков
	
	$check_is_such_lang = $db_link->prepare("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?;");
	$check_is_such_lang->execute( array($_POST['same']) );
	if( $check_is_such_lang->fetchColumn() != 1 )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = "Incorrect value of same";
		exit(json_encode($answer));
	}
}
else
{
	$_POST['same'] = null;
}

// -------------------------------------------------------------------------------
//Проверка is_custom (либо 1, либо 0)
$_POST['is_custom'] = (int)$_POST['is_custom'];

if( $_POST['is_custom'] != 1 && $_POST['is_custom'] != 0 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Incorrect value of is_custom";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------
//Проверка used_found (допустимые значения 0, 1, 2)
$_POST['used_found'] = (int)$_POST['used_found'];

if( $_POST['used_found'] != 1 && $_POST['used_found'] != 0 && $_POST['used_found'] != 2 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Incorrect value of used_found";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Значение описания не допускается пустым
if( $_POST['description'] == '' )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Empty value does not acceptable";
	exit(json_encode($answer));
}

// -------------------------------------------------------------------------------

//Генерируем уникальный key:
$str_key = get_next_str_key();

// -------------------------------------------------------------------------------

//Добавляем строку
if( $db_link->prepare("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `str_key`, `used_found`, `is_custom`) VALUES (?, ?, ?, ?, ?, ?);")->execute( array( $_POST['description'], $_POST['same'], $_POST['is_error'],  $str_key, $_POST['used_found'], $_POST['is_custom']) ) )
{
	//Формируем объект строки
	$str = array();
	//$str['id'] = $db_link->lastInsertId();
	$str['str_key'] = $str_key;
	$str['description'] = htmlentities($_POST['description']);
	$str['current_lang_translation'] = '';//Перевода на текущий язык еще нет
	$str['same'] = $_POST['same'];
	$str['is_error'] = $_POST['is_error'];
	$str['used_found'] = $_POST['used_found'];
	$str['is_custom'] = $_POST['is_custom'];
	
	//Получаем языки платформы и заполняем флаги наличия переводов
	$filled_has_lang = false;
	$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
	$languages_query->execute();
	while( $language = $languages_query->fetch() )
	{
		$str[ 'has_'.$language['lang_code'] ] = 0;
		$filled_has_lang = true;
	}
	
	
	//Проверки
	/*
	if( $str['id'] <= 0 )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(2522);
		exit(json_encode($answer));
	}
	*/
	if( ! $filled_has_lang )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(2523);
		exit(json_encode($answer));
	}
	
	
	
	$answer = array();
	$answer['str'] = $str;
	$answer["status"] = true;
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
?>