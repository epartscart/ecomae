<?php
//Подключаемый скрипт для записи файла crontab
defined('_PYPRICES_CRONTAB_') or die('No access');//Защита от запуска извне
// -----------------------------------------------------------------------------------------
//Если нет такой функции - определяем (str_contains() - начиная с PHP 8)
if( ! function_exists('str_contains') ) 
{
    function str_contains($haystack, $needle)
	{
        return $needle !== '' && mb_strpos($haystack, $needle) !== false;
    }
}
// -----------------------------------------------------------------------------------------
/*
Алгоритм такой:
- получаем текущие задания в crontab (это могут быть не только наши)
- записываем их в массив (кроме наших)
- получаем из таблицы заданий наши актуальные и добавляем их в массив
- стираем текущее состояние файла crontab
- перезаписываем его заново
cd <site_home_dir> && php <backend_dir>/content/shop/prices_upload/for_pyprices/for_cron/cron_task_executor.php <tech_key> <crontab_task_id>
*/
// -----------------------------------------------------------------------------------------
$crontab_tasks = array();//Сюда пишем те задания, которые должны попасть в обновленный файл crontab
//Получаем текущие задания из crontab (строки)
$cronfiles = exec('crontab -l', $output);
//$output = explode("\n", $output);
//Наши задания - фильтруем
for( $i = 0 ; $i < count($output) ; $i++ )
{
	//Если эта строка из текущего crontab содержит имя нашего скрипта - ее выкидываем
	if( str_contains($output[$i], "cron_task_executor.php") )
	{
		continue;
	}
	
	//Эта строка не относится к нашему скрипту - ее оставляем
	$crontab_tasks[] = $output[$i];
}
// -----------------------------------------------------------------------------------------
//Теперь получаем текущие задания по расписанию из таблицы
$crontab_tasks_query = $db_link->prepare("SELECT * FROM `shop_docpart_pyprices_crontab` WHERE `active` = ?;");
$crontab_tasks_query->execute( array(1) );
while( $crontab_task = $crontab_tasks_query->fetch() )
{
	$crontab_tasks[] = $crontab_task['minute']." ".$crontab_task['hour']." ".$crontab_task['day_month']." ".$crontab_task['month']." ".$crontab_task['day_week']." cd ".$_SERVER["DOCUMENT_ROOT"]." && php ".$DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/for_cron/cron_task_executor.php ".$DP_Config->tech_key." ".$crontab_task['id'];
}
// -----------------------------------------------------------------------------------------
//Перезыписываем файл crontab
exec('crontab -r');//Удалили все текущие задания из crontab
for( $i = 0 ; $i < count($crontab_tasks) ; $i++ )
{
	exec('crontab -l | { cat; echo "'.$crontab_tasks[$i].'"; } | crontab -');
}
// -----------------------------------------------------------------------------------------
?>