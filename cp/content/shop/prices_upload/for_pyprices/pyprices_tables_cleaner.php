<?php
//Скрипт очистики технологических таблиц, относящихся к запускам обновления прайс-листов (запуски pyprices, cron-скрипта), а также с учетными записями объектов заданий для pyprices. Очищаются записи, которым уже больше недели - считаем их уже не актуальными.
defined('_PYPRICES_CRONTAB_') or die('No access');//Защита от запуска извне

/*
Перечень таблиц для очистки:
- shop_docpart_prices_cron_executor_launches (запуски php-скрипта по cron)
- shop_docpart_pyprices_launches (запуски pyprices)
- shop_docpart_pyprices_tasks (объекты заданий для pyprices)
*/


/*
Скрипт подключен в:
- cron_task_executor.php (скрипт, запускаемый по cron)
- prices_manager.php (скрипт страницы Менеджера прайс-листов)
Т.е. технологические таблицы очищаются при выполнении выше указанных скриптов.
*/


//UNIX-время неделю назад
$time_week_ago = time() - 604800;

//shop_docpart_prices_cron_executor_launches
$db_link->prepare("DELETE FROM `shop_docpart_prices_cron_executor_launches` WHERE `time_start` < ?;")->execute( array($time_week_ago) );

//shop_docpart_pyprices_launches
$db_link->prepare("DELETE FROM `shop_docpart_pyprices_launches` WHERE `time_start` < ?;")->execute( array($time_week_ago) );

//shop_docpart_pyprices_tasks
$db_link->prepare("DELETE FROM `shop_docpart_pyprices_tasks` WHERE `time_created` < ?;")->execute( array($time_week_ago) );
?>