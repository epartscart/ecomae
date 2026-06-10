<?php
//Серверный скрипт для создания/редактирования задания по расписанию
// -------------------------------------------------------------------------------
define('_PYPRICES_CRONTAB_', 1);
header('Content-Type: application/json; charset=utf-8');
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
	$answer["message"] = 'No DB Connect';
	exit(json_encode($answer));
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
// -------------------------------------------------------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('url'=>'shop/prices', 'is_frontend' => 0);
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
//Получаем объект задания
$cron_task = json_decode($_POST['cron_task'], true);
// -------------------------------------------------------------------------------
//Добавляем задание в БД
try
{
	//Старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception(translate_str_by_id(2132));
	}
	
	
	//Валидация объекта
	//Прайс-листы
	if( !isset( $cron_task['prices'] ) || !is_array($cron_task['prices']) || count($cron_task['prices'])==0 )
	{
		throw new Exception("Validation error 1");
	}
	else
	{
		//Проверяем наличие в БД указанных прайс-листов
		$check_prices_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_docpart_prices` WHERE `id` IN (?'.str_repeat(',?', count($cron_task['prices'])-1 ).');');
		$check_prices_query->execute( $cron_task['prices'] );
		if( count($cron_task['prices']) != $check_prices_query->fetchColumn() )
		{
			throw new Exception("Validation error 2");
		}
	}
	//Включено/выключено
	if( !isset($cron_task['active']) || ($cron_task['active']!= 1 && $cron_task['active']!=0) )
	{
		throw new Exception("Validation error 3");
	}
	//Дни недели
	if( !isset($cron_task['days']) || !is_array($cron_task['days']) || count($cron_task['days']) == 0 || count($cron_task['days']) > 7 )
	{
		throw new Exception("Validation error 4");
	}
	else
	{
		for( $i=0 ; $i < count($cron_task['days']) ; $i++ )
		{
			if( array_search( $cron_task['days'][$i] , array('1', '2', '3', '4', '5', '6', '7') ) === false )
			{
				throw new Exception("Validation error 5");
			}
		}
	}
	//Время (часы и минуты)
	if( !isset( $cron_task['time'] ) )
	{
		throw new Exception("Validation error 6");
	}
	else
	{
		$time = explode(':', $cron_task['time']);
		if( count($time) != 2 )
		{
			throw new Exception("Validation error 7");
		}
		
		//Часы
		if( (int)$time[0] < 0 || (int)$time[0] > 23 )
		{
			throw new Exception("Validation error 8");
		}
		//Минуты
		if( (int)$time[1] < 0 || (int)$time[1] > 59 )
		{
			throw new Exception("Validation error 8");
		}
	}
	
	
	//Дни недели к строке
	$day_week = "";
	for( $i = 0 ; $i < count($cron_task['days']) ; $i++ )
	{
		if( $i > 0 )
		{
			$day_week .= ',';
		}
		$day_week .= $cron_task['days'][$i];
	}
	
	
	
	if( !isset($cron_task['id']) )
	{
		//Добавляем задание в БД
		if( ! $db_link->prepare('INSERT INTO `shop_docpart_pyprices_crontab` (`active`, `day_week`, `month`, `day_month`, `hour`, `minute`) VALUES (?,?,?,?,?,?);')->execute( array($cron_task['active'], $day_week, "*", "*", (int)$time[0], (int)$time[1]) ) )
		{
			throw new Exception("Error inserting task");
		}
		$cron_task_id = $db_link->lastInsertId();
		if( !$cron_task_id )
		{
			throw new Exception("Error getting task ID");
		}
		//Связываем задание с прайс-листами
		for( $i = 0 ; $i < count($cron_task['prices']) ; $i++ )
		{
			if( ! $db_link->prepare("INSERT INTO `shop_docpart_pyprices_crontab_prices` (`price_id`,`crontab_task_id`) VALUES (?,?);")->execute( array($cron_task['prices'][$i], $cron_task_id) ) )
			{
				throw new Exception("Error binding task with price-list");
			}
		}
		$message = translate_str_by_id(5350);
	}
	else
	{
		$cron_task_id = $cron_task['id'];
		
		//Checking if the task exists
		$check_cron_task_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab` WHERE `id` = ?;");
		$check_cron_task_query->execute( array($cron_task_id) );
		if( $check_cron_task_query->fetchColumn() == 0 )
		{
			throw new Exception("Error - cron task not found");
		}
		
		//Updating
		if( ! $db_link->prepare("UPDATE `shop_docpart_pyprices_crontab` SET `active` = ?, `day_week` = ?, `month` = ?, `day_month` = ?, `hour` = ?, `minute` = ? WHERE `id` = ?;")->execute( array($cron_task['active'], $day_week, "*", "*", (int)$time[0], (int)$time[1], $cron_task_id) ) )
		{
			throw new Exception("Error updating task");
		}
		
		//Очищаем старые записи связи задания с прайс-листами
		if( ! $db_link->prepare("DELETE FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?;")->execute( array($cron_task_id) ) )
		{
			throw new Exception("Error deleting old links of cron_task and price-lists");
		}
		
		//Adding new links beetwen the cron task and prices
		for( $i = 0 ; $i < count($cron_task['prices']) ; $i++ )
		{
			if( ! $db_link->prepare("INSERT INTO `shop_docpart_pyprices_crontab_prices` (`price_id`,`crontab_task_id`) VALUES (?,?);")->execute( array($cron_task['prices'][$i], $cron_task_id) ) )
			{
				throw new Exception("Error binding task with price-list");
			}
		}
		
		$message = translate_str_by_id(5351);
	}
}
catch (Exception $e)
{
	//Откатываем все изменения
	$db_link->rollBack();
	
	$result = array();
	$result["status"] = false;
	$result["message"] = $e->getMessage();
	exit(json_encode($result));
}

//Дошли до сюда, значит выполнено ОК
$db_link->commit();//Коммитим все изменения и закрываем транзакцию


//Перезапись файла crontab (единый скрипт)
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/for_cron/crontab_writer.php");



$result = array();
$result["status"] = true;
$result["cron_task_id"] = $cron_task_id;
$result["message"] = $message;
exit(json_encode($result));
?>