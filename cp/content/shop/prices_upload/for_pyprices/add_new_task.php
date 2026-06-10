<?php
//Единый скрипт добавления нового задания в shop_docpart_pyprices_tasks
// -------------------------------------------------------------------------------
header('Content-Type: application/json; charset=utf-8');
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
	$answer["message"] = 'No DB Connect';
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");
// -------------------------------------------------------------------------------
//Скрипт может запускаться как пользователем со страницы "Менеджер прайс-листов", так и cron-скриптом при выполнении заданий по расписанию
if( !(isset( $_POST['key'] ) && $_POST['key'] == $DP_Config->tech_key) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	//Для работы с пользователями
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
	// -------------------------------------------------------------------------------
	//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
	$pages_to_check = array();
	$pages_to_check[] = array('url'=>'shop/prices', 'is_frontend' => 0);
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
	// -------------------------------------------------------------------------------
}
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
//Добавляем задание в таблицу
if( $db_link->prepare("INSERT INTO `shop_docpart_pyprices_tasks` (`time_created`,`price_id`) VALUES (?,?);")->execute( array( time(), $_POST['price_id'] ) ) )
{
	//Клиенту нужно вернуть ID созданного задания
	$task_id = $db_link->lastInsertId();
	if( !$task_id )
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = 'Error getting task ID';
		exit(json_encode($answer));
	}
	else
	{
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_price_upload_history.php");
		$priceChannel = trim((string)($_POST['price_channel'] ?? ''));
		epc_price_history_begin_pyprices_task($db_link, (int)$_POST['price_id'], (int)$task_id, $priceChannel);

		$answer = array();
		$answer["task_id"] = $task_id;
		$answer["status"] = true;
		$answer["message"] = 'Ok';
		exit(json_encode($answer));
	}
}
else
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'SQL error';
	exit(json_encode($answer));
}
?>