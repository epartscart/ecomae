<?php
/*Костыльный запускатор заданий по расписанию - для тех хостингов, где у пользователя нет доступа к crontab через командную строку, но, есть возможность настроить задания по расписанию через панель хостинга.

Для запуска скрипта нужно добавить команду, выполняемую каждую минуту:
wget -O /dev/null -q 'https://<домен>/<backend>/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key=<tech_key>'
*/
// -------------------------------------------------------------------------------------------------
//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
// -------------------------------------------------------------------------------------------------
if( !isset( $_GET['key'] ) || $_GET['key'] != $DP_Config->tech_key )
{
	exit('No access');
}
// -------------------------------------------------------------------------------------------------
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'No DB connect';
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");
// -------------------------------------------------------------------------------------------------
/*
Получем список заданий по расписанию, которые:
- включены
- в данный момент не запущены
- должны выполняться сейчас (день, час, минута)
*/

/*
$crontab_query = $db_link->prepare("SELECT * FROM `shop_docpart_pyprices_crontab` WHERE `active` = ? AND (SELECT COUNT(*) FROM `shop_docpart_prices_cron_executor_launches` WHERE `crontab_task_id` = `shop_docpart_pyprices_crontab`.`id` AND ISNULL(`time_end`) ) = ? AND `day_week` LIKE ? AND `hour` = ? AND `minute` = ?;");
$crontab_query->execute( array(1, 0, '%'.date("N", time()).'%', date("G", time()), (int)date("i", time()) ) );
*/

$crontab_query = $db_link->prepare("SELECT * FROM `shop_docpart_pyprices_crontab` WHERE `active` = ? AND (SELECT COUNT(*) FROM `shop_docpart_prices_cron_executor_launches` WHERE `crontab_task_id` = `shop_docpart_pyprices_crontab`.`id` AND ISNULL(`time_end`) AND ? < (`time_start` + 1800) ) = ? AND `day_week` LIKE ? AND `hour` = ? AND `minute` = ?;");
$crontab_query->execute( array(1, time(), 0, '%'.date("N", time()).'%', date("G", time()), (int)date("i", time()) ) );

while( $crontab_task = $crontab_query->fetch() )
{
	//Для данного задания запускаем скрипт отсюда. Амперсант в конце - чтобы не ждать окончания выполнения.
	exec( "cd ".$_SERVER["DOCUMENT_ROOT"]." && php ".$DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/for_cron/cron_task_executor.php ".$DP_Config->tech_key." ".$crontab_task['id']." &" );
}

// -------------------------------------------------------------------------------------------------
// Nightly live FX rates (shop currencies) — safe to call every minute; runs once when due.
try {
	$fxHelper = $_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/epc_currency_live_rates.php";
	if (is_file($fxHelper)) {
		require_once $fxHelper;
		if (function_exists('epc_currency_live_schedule_tick')) {
			epc_currency_live_schedule_tick($db_link, $DP_Config, false);
		}
	}
} catch (Throwable $e) {
	// Non-fatal: pyprices cron must keep working even if FX tick fails.
}
?>