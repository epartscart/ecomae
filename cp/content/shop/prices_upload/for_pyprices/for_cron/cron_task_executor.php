<?php
//Скрипт запускаемый через cron - для выполнения заданий по раписанию для обновления прайс-листов. Выполняется через php-cli (по умолчанию max_execution_time = 0, т.е. не ограничено, что и требуется)
// -------------------------------------------------------------------------------
/*
Здесь нет POST-аргументов, т.к. скрипт работает через командную строку:
php <путь к скрипту> <tech_key> <crontab_task_id>
*/
// -------------------------------------------------------------------------------
/*
//Конфигурация CMS (ВНИМАНИЕ! Подключение config.php корректно, если запуск идет из корневой папки сайта. Поэтому, при записи заданий в crontab нужно формировать команду таким образом:
cd <site_home_dir> && php <backend_dir>/content/shop/prices_upload/for_pyprices/for_cron/cron_task_executor.php <tech_key> <crontab_task_id>
)
*/
// -------------------------------------------------------------------------------
//Конфигурация CMS
require_once("config.php");
$DP_Config = new DP_Config;
// -------------------------------------------------------------------------------
//0 - пусть к этому скрипту. 1 - ключ. 2 - ID задания по расписанию
if( !isset($argv[0]) || !isset($argv[1]) || !isset($argv[2]) )
{
	exit("Too few arguments");
}
// -------------------------------------------------------------------------------
//Защита от постороннего доступа
if( $DP_Config->tech_key != $argv[1] )
{
	exit("Authorization failed");
}
// -------------------------------------------------------------------------------
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	exit("No DB Connect");
}
$db_link->query("SET NAMES utf8;");
// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
//Очистка технологических таблиц
define('_PYPRICES_CRONTAB_', 1);
require_once($DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/pyprices_tables_cleaner.php");
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
//Добавляем учетную запись запуска в shop_docpart_prices_cron_executor_launches
if( ! $db_link->prepare("INSERT INTO `shop_docpart_prices_cron_executor_launches` (`crontab_task_id`, `time_start`, `have_pyprices_query`) VALUES (?,?,?);")->execute( array( $argv[2], time(), 0 ) ) )
{
	exit("Error inserting launch record");
}
$executor_launch_id = $db_link->lastInsertId();
if( !$executor_launch_id  )
{
	exit("Error getting launch id");
}
$error_messages = array();//Для сообщений ошибок
// -------------------------------------------------------------------------------
//Формируем объект для запроса к pyprices
$list_to_handle = array();
//Получаем сразу перечень прайс-листов, которые привязаны к данном заданию по раписанию. Получаем сразу с найстроками. И добавляем объекты заданий в массив для запроса к pyprices
$prices_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `load_mode` != ? AND `id` IN (SELECT `price_id` FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?);");
$prices_query->execute( array(1, $argv[2]) );
while( $price = $prices_query->fetch() )
{
	//Формируем объект задания для pyprices для данного прайс-листа
	$pyprices_task = array();
	
	$pyprices_task['price_id'] = $price['id'];
	$pyprices_task['price_name'] = $price['name'];
	
	
	//Общие настройки задания
	$pyprices_task['file_name_substring'] = $price['file_name_substring'];
	$pyprices_task['file_name_substring_arch'] = $price['file_name_substring_arch'];
	$pyprices_task['file_encoding'] = $price['encoding'];
	$pyprices_task['cols_delimiter'] = str_replace('\t', "\t", $price['separator']);
	$pyprices_task['clear_old_records'] = $price['clean_before'];
	$pyprices_task['rows_per_query'] = 1000;
	
	
	//Структура файла
	$pyprices_task['col_name'] = $price['name_col'];
	$pyprices_task['col_article'] = $price['article_col'];
	$pyprices_task['col_manufacturer'] = $price['manufacturer_col'];
	$pyprices_task['col_price'] = $price['price_col'];
	$pyprices_task['col_exist'] = $price['exist_col'];
	$pyprices_task['col_storage'] = $price['storage_col'];
	$pyprices_task['col_min_order'] = $price['min_order_col'];
	$pyprices_task['col_time_to_exe'] = $price['time_to_exe_col'];
	$pyprices_task['cols_to_left'] = $price['strings_to_left'];
	
	
	//Источник
	switch( $price['load_mode'] )
	{
		case 2:
			$pyprices_task['source'] = 'ftp';
			$pyprices_task['ftp_host'] = $price['ftp_host'];
			$pyprices_task['ftp_username'] = $price['ftp_user'];
			$pyprices_task['ftp_password'] = $price['ftp_password'];
			$pyprices_task['ftp_folder'] = $price['ftp_folder'];
			break;
		case 3:
			$pyprices_task['source'] = 'email';
			$pyprices_task['email_price_sender'] = $price['sender_email'];
			$pyprices_task['email_message_header_substring'] = $price['message_header_substring'];
			$pyprices_task['not_mark_seen_email_messages'] = $price['not_mark_seen_email_messages'];//Проверить учет этой настройки со строны pyprices
			break;
		case 4:
			$pyprices_task['source'] = 'url';
			$pyprices_task['url'] = $price['link'];
			break;
	}
	
	//Устанавливаем флаг - "Обработка не завершена" (возможно, это не обязательно для cron)
	$pyprices_task['completed'] = false;
	
	
	//Необходимо указать поле client_task_id. Это уникальный ID задания. Необходим для идикации выполняемого процесса. Без него это задание в pyprices не пройдет валидацию
	if( $db_link->prepare("INSERT INTO `shop_docpart_pyprices_tasks` (`time_created`, `price_id`) VALUES (?,?);")->execute( array(time(), $price['id']) ) )
	{
		$client_task_id = $db_link->lastInsertId();
		if(!$client_task_id)
		{
			//Тут тоже можно записать лог ошибки
			$error_messages[] = translate_str_by_id(5352).". price_id ".$price['id'];
			continue;
		}
		
		$pyprices_task['client_task_id'] = $client_task_id;

		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_price_upload_history.php");
		epc_price_history_begin_pyprices_task($db_link, (int)$price['id'], (int)$client_task_id, (string)($pyprices_task['source'] ?? ''));
	}
	else
	{
		//Тут можно записать лог - ошибка добавления задания в таблицу
		$error_messages[] = translate_str_by_id(5353).". price_id ".$price['id'];
		continue;
	}
	
	
	//Дошли до сюда, значит свормирован правильный объект задания для pyprices. Добавляем его в массив
	$list_to_handle[] = $pyprices_task;
}
// -------------------------------------------------------------------------------
//Перед вызовом pyprices
$have_pyprices_query = 0;
$pyprices_answer = NULL;
// -------------------------------------------------------------------------------
//Если не оказалось корректных объектов заданий для pyprices
if( count($list_to_handle) == 0 )
{
	$error_messages[] = "No tasks for pyprices. Query will not executed";
}
else
{
	//Есть корректные объекты задания для pyprice. Выполняем запрос к pyprices
	$have_pyprices_query = 1;
	
	//Аргументы запроса
	$postdata_array = array( "key"=>$DP_Config->tech_key, "list_to_handle"=>json_encode($list_to_handle) );
	$postdata = http_build_query($postdata_array);


	$curl = curl_init();
	curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."pyprices/api.py");
	curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
	curl_setopt($curl, CURLOPT_POST, true);
	curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
	curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
	curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
	$pyprices_answer = curl_exec($curl);
	curl_close($curl);

	if ($pyprices_answer !== false && $pyprices_answer !== '' && $pyprices_answer !== null) {
		$pyprices_decoded = json_decode($pyprices_answer, true);
		if (is_array($pyprices_decoded)) {
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_price_upload_history.php");
			foreach (array('list_to_handle', 'list_to_handle_incorrect') as $listKey) {
				if (empty($pyprices_decoded[$listKey]) || !is_array($pyprices_decoded[$listKey])) {
					continue;
				}
				foreach ($pyprices_decoded[$listKey] as $finishedTask) {
					if (!is_array($finishedTask)) {
						continue;
					}
					$pid = (int)($finishedTask['price_id'] ?? 0);
					if ($pid > 0) {
						epc_price_history_log_pyprices_task($db_link, $finishedTask, $pid);
					}
				}
			}
		}
	}
}
// -------------------------------------------------------------------------------
//Завершаем выполнение
if( ! $db_link->prepare("UPDATE `shop_docpart_prices_cron_executor_launches` SET `time_end` = ?, `list_to_handle` = ?, `error_messages` = ?, `have_pyprices_query` = ?, `pyprices_answer` = ? WHERE `id` = ?;")->execute( array( time(), json_encode($list_to_handle), json_encode($error_messages), $have_pyprices_query, $pyprices_answer, $executor_launch_id ) ) )
{
	exit("Error updating executor launch record");
}
else
{
	exit(0);
}
// -------------------------------------------------------------------------------
?>