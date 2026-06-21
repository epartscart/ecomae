<?php
//Серверный скрипт для тестирования настроек E-mail и Телефона
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация CMS
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
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Проверяем право менеджера
if( ! DP_User::isAdmin())
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit( json_encode($answer) );
}

// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//Для отправки уведомлений
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );




//Проверка наличия необходимых входных данных
if( !isset($_POST['contact']) || !isset($_POST['type']) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "No params";
	exit( json_encode($answer) );
}


//Проверка значения $_POST['type']
if( $_POST['type'] != 'email' && $_POST['type'] != 'phone' )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Incorrect type";
	exit( json_encode($answer) );
}


//Проверяем соответствие контакта формату
$contact = trim($_POST['contact']);
$type = $_POST['type'];
if( !DP_User::check_contact_by_regexp($contact, $type) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2389);
	exit( json_encode($answer) );
}


//Для массив получателей
if( $type == 'email' )
{
	$email = $contact;
	$phone = '';
}
else
{
	$email = '';
	$phone = $contact;
}



//Получатель:
$persons = array();
$persons[] = array(
	'type'=>'direct_contact',
	'contacts'=>array(
			'email'=>array('value'=>$email),
			'phone'=>array('value'=>$phone)
		)
	);



//Отправка
$send_result = send_notify('test_'.$type, array(), $persons);



if($send_result["status"] == false)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2390);
	exit( json_encode($answer) );
}
else
{
	//Скрипт отправки выдал статус true, теперь НЕОБХОДИМО проверить статус отправки по конкретному контакту
	//Мы указывали единственный контакт, поэтому его и проверяем.
	if( ! $send_result["persons"][0]['contacts'][$type]['status'] )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = translate_str_by_id(2391);
		exit( json_encode($answer) );
	}
	else
	{
		$answer = array();
		$answer["status"] = true;
		$answer["message"] = translate_str_by_id(2392);
		exit( json_encode($answer) );
	}
}
?>