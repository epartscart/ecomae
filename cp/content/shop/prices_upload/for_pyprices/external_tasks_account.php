<?php
//Скрипт для учета внешних заданий - предназначен для страницы "Менеджер прайс-листов" для учета заданий, которые запускаются другими контроллерами (с другого экземпляра страницы "Менеджер прайс-листов" или из cron-скрипта) и продолжают выполняться в момент запроса к этому скрипту
// -------------------------------------------------------------------------------
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/../epc_prices_ajax_init.php';
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
//Технический запрос для простановки passed для тех запусков pyprices, которые не завершились корректно (которые упали, либо, произошел таймаут)
//Получаем все запуски с passed==0
$not_passed_launches_query = $db_link->prepare("SELECT * FROM `shop_docpart_pyprices_launches` WHERE `passed` = ? ORDER BY `id` DESC LIMIT 40;");
$not_passed_launches_query->execute( array(0) );
while( $launch = $not_passed_launches_query->fetch() )
{
	//Смотрим, если такой процесс. Пробуем несколькими способами. Если ни один из них не показал процесс - считаем, что процесс уже не выполняется
	
	$launch['pid'] = (int)$launch['pid'];
	
	if(
		!file_exists("/proc/".$launch['pid']) && 
		!posix_getpgid($launch['pid'])
	)
	{
		//Считаем, что процесс не выполняется. Проставляем passed
		$db_link->prepare("UPDATE `shop_docpart_pyprices_launches` SET `passed` = ? WHERE `id` = ?;")->execute( array(1, $launch['id']) );
	}
}
// -------------------------------------------------------------------------------
//Исходные данные
$tasks_not_to_account = json_decode($_POST['tasks_not_to_account'], true);//Задания, которые не интересуют клиента (это те, которые он сам контролирует)
$tasks_exeternal_to_account = json_decode($_POST['tasks_exeternal_to_account'], true);//Внешние задания, которые клиент УЖЕ отслеживает и которые его интересуют
$prices_to_account = json_decode($_POST['prices_to_account'], true);//Список прайс-листов, по которым клиента могут интересовать задания
// -------------------------------------------------------------------------------
//Проверки исходных данных
if( !is_array($tasks_not_to_account) ||
!is_array($tasks_exeternal_to_account) ||
!is_array($prices_to_account)
)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Incorrect data';
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
//Получаем обстановку по тем заданиям, которые интересуют клиента (завершились ли они, или еще нет)
$tasks_exeternal_to_account_results = array();//Для отправки клиенту
if( count($tasks_exeternal_to_account) > 0 )
{
	$SQL = "SELECT
			`shop_docpart_pyprices_launches`.`passed` AS `passed`,
			`shop_docpart_pyprices_launches`.`normal_exit_status` AS `normal_exit_status`,
			`shop_docpart_pyprices_launches`.`answer` AS `answer`,
			`shop_docpart_pyprices_launches`.`is_normal_exit` AS `is_normal_exit`,
			`shop_docpart_pyprices_launches`.`list_to_handle` AS `list_to_handle`,
			`shop_docpart_pyprices_launches`.`time_end` AS `time_end`,
			`shop_docpart_pyprices_launches`.`time_start` AS `time_start`,
			`shop_docpart_pyprices_launches`.`id` AS `launch_id`,
			`shop_docpart_pyprices_tasks`.`price_id` AS `price_id`,
			`shop_docpart_pyprices_tasks`.`id` AS `client_task_id`
		FROM
			`shop_docpart_pyprices_tasks`
		INNER JOIN `shop_docpart_pyprices_launches` ON `shop_docpart_pyprices_tasks`.`pyprices_launche_id` = `shop_docpart_pyprices_launches`.`id`
		WHERE
			`shop_docpart_pyprices_tasks`.`id` IN (?".str_repeat(",?", count($tasks_exeternal_to_account)-1 ).");";
	
	$tasks_exeternal_to_account_query = $db_link->prepare($SQL);
	$tasks_exeternal_to_account_query->execute( $tasks_exeternal_to_account );
	while( $item = $tasks_exeternal_to_account_query->fetch() )
	{	
		$task = null;//Переменная для записи описания текущего состояния задания
		
		//Задание все еще выполняется
		if( !$item['passed'] )
		{
			//Объект в ответ (задание продолжает выполняться). Отвечаем просто - чтобы клиент понимал, что это задание еще выполняется
			$task = array();
			$task['client_task_id'] = $item['client_task_id'];
			$task['passed'] = false;
		}
		else
		{
			//Задание больше не выполняется. Здесь нужно понять статус. Если запуск pyprices завершился штатно, читаем объект задания из колонки answer. Если не штатно - читаем объект задания из колонки list_to_handle. Задача в том, чтобы клиент мог при возможности, индицировать результат обоаботки этого задания.
			if( $item['is_normal_exit'] )
			{
				//Нужно найти объект задания в ответе pyprices
				$pyprices_answer = json_decode($item['answer'], true);
				
				//print_r($pyprices_answer);
				
				//Ищем сначала объект в массиве list_to_handle
				for( $i=0 ; $i < count($pyprices_answer['list_to_handle']) ; $i++)
				{
					if( $pyprices_answer['list_to_handle'][$i]['client_task_id'] == $item['client_task_id'] )
					{
						$task = $pyprices_answer['list_to_handle'][$i];
						break;
					}
				}
				
				//Если не нашли, то, ищем в массиве list_to_handle_incorrect
				if( !$task )
				{
					for( $i=0 ; $i < count($pyprices_answer['list_to_handle_incorrect']) ; $i++)
					{
						if( $pyprices_answer['list_to_handle_incorrect'][$i]['client_task_id'] == $item['client_task_id'] )
						{
							$task = $pyprices_answer['list_to_handle_incorrect'][$i];
							break;
						}
					}
				}
			}
			else
			{
				//pyprices с этим заданием не смог завершиться корректно. Поэтому, объект задания берем из колонки list_to_handle (в ней задания в исходном виде - без сообщений) и инициализируем поле с описанием ошибки.
				$pyprices_list_to_handle = json_decode($item['list_to_handle'], true);
				
				for( $i=0 ; $i < count($pyprices_list_to_handle) ; $i++)
				{
					if( $pyprices_list_to_handle[$i]['client_task_id'] == $item['client_task_id'] )
					{
						$task = $pyprices_list_to_handle[$i];
						
						//Результат работы установить не возможно. Поэтому просто заполняем поле other_error - для индикации ошибки
						$task['other_error'] = translate_str_by_id(5349);
						break;
					}
				}
			}
			
			$task['passed'] = true;

			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_price_upload_history.php");
			epc_price_history_log_pyprices_task($db_link, $task, (int)$item['price_id']);
		}
		
		//Добавляем объект с описанием текущего состояния задания в массив для ответа
		$tasks_exeternal_to_account_results[] = $task;
	}
}
// -------------------------------------------------------------------------------
//Получаем новые задания, которые в данные момент выполняются
/*
Фильтр:
- перечень заданий, которые нас не интересуют (это те задания, которыми клиент управляет сам)
- перечень заданий, которые интересуют (они уже не новые)
- прайс-листы, которые не входят в перень прайсов из запроса (т.е. интересут только те задания, которые имеют price_id из списка от клиента)
*/
$new_external_tasks = array();
//Если есть прайс-листы в списке для отслеживания
if( count($prices_to_account) > 0 )
{
	$SQL = "
	SELECT
		`shop_docpart_pyprices_tasks`.`id` AS `client_task_id`,
		`shop_docpart_pyprices_tasks`.`price_id` AS `price_id`
	FROM
		`shop_docpart_pyprices_launches`
	INNER JOIN `shop_docpart_pyprices_tasks` ON `shop_docpart_pyprices_tasks`.`pyprices_launche_id` = `shop_docpart_pyprices_launches`.`id`
	WHERE
		`shop_docpart_pyprices_launches`.`passed` = ? AND
		`shop_docpart_pyprices_tasks`.`id` NOT IN (?".str_repeat(",?", count( array_merge($tasks_not_to_account, $tasks_exeternal_to_account) ) ).") AND
		`shop_docpart_pyprices_tasks`.`price_id` IN (?".str_repeat(",?", count($prices_to_account)-1 ).");
	";


	//passed==0 (которые выполняются); 0 - на случай, если два следующих массива не заполнеы; которые не входят в  $tasks_not_to_account; которые не входят в $tasks_exeternal_to_account; прайсы которых входят в $prices_to_account
	$binding_values = array_merge( array(0,0), $tasks_not_to_account, $tasks_exeternal_to_account, $prices_to_account);
	$new_external_tasks_query = $db_link->prepare($SQL);
	$new_external_tasks_query->execute( $binding_values );
	while( $item = $new_external_tasks_query->fetch() )
	{
		$item['completed'] = false;
		$new_external_tasks[] = $item;
	}
}
// -------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
//Ответ клиенту:
$answer = array();
$answer["status"] = true;
$answer["message"] = 'OK';

//Массивы с нужно инфой
$answer['tasks_exeternal_to_account_results'] = $tasks_exeternal_to_account_results;//Текущая обстановка по интересующим клиента заданиям
$answer['new_external_tasks'] = $new_external_tasks;//Новые внешние задание, которые сейчас выполняются

exit(json_encode($answer));
?>