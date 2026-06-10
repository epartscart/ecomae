<?php
/**
 * Серверный скрипт для отправки уведомлений
 * 
 * Инициатор 4 - скрипт
*/
/*
ИНСТРУКЦИЯ К СКРИПТУ
Скрипт используется для отправки писем и sms, с использованием настраиваемых уведомлений в таблице notifications_settings.

ПРИНЦИП:
Скрипт - универсальный. Подходит как для отправки одиночного уведомления одному получателю на один его контакт (email или телефон), так и для отправки пакетных уведомлений нескольким получателям на произвольные контакты (email и телефон) с проверкой статуса отправки отдельно по каждому получателю и по каждому контакту каждого получателя.

Входные данные:
- check - ключ для запуска скрипта
- name - символьное обозначение уведомления из таблицы notifications_settings
- vars - JSON-массив со значениями переменных, подставляемыми в тексты уведомлений, в соответствии с описанием уведомления
- persons - массив получателей. Имеет сложную структуру, описанную ниже
- files - массив файлов для вставки в письмо отправляемое на Email

persons:
[
	[
		type => user_id/direct_contact (либо ID пользователя, либо прямые контакты), (Указывает инициатор)
		
		//Если type=='user_id', то, нужно указать поле user_id:
		user_id => ... (Указывает инициатор)
		
		
		//Поле для контактов:
		
		//Для type=='user_id'. Инициатором не указываются. Формируются полностью скриптом отправки и затем могут быть проверены инициатором.
		contacts =>[
			"email" => ["value"=>... (не указывается), "status"=>... (статус отправки), 'email_confirmed'=>..., 'tried_to_send'=>...],
			"phone" => ["value"=>... (не указывается), "status"=>... (статус отправки), 'phone_confirmed'=>..., 'tried_to_send'=>...]
		]
		// value - не указывается, чтобы не светить контакты пользователей в ответе, tried_to_send - пытались отправить (да/нет), email_confirmed/phone_confirmed - флаг "контакт подтвержден", status - статус отправки (true, если была успешная попытка отправки, в остальных случаях - false)
		
		
		
		//Для type=='direct_contact'. Начинается формироваться инициатором. Затем, скрипт отправки - дописывает статусы отправки по контактам, которые затем могут быть проверены инициатором из ответа.
		contacts =>[
			"email" => ["value"=>... (указывается инициатором), "status"=>... (статус отправки), 'email_confirmed'=>... (всегда false), 'tried_to_send'=>...],
			"phone" => ["value"=>... (указывается инициатором), "status"=>... (статус отправки), 'phone_confirmed'=>... (всегда false), 'tried_to_send'=>...]
		]
		//value - указывается самим инициатором, tried_to_send - пытались отправить (да/нет), status - статус отправки (true, если была успешная попытка отправки, в остальных случаях - false), email_confirmed/phone_confirmed - флаг "контакт подтвержден" (всегда false, т.к. при type=='direct_contact' не указывается user_id и не происходит никаких проверок подтверждения контактов в учетных записях пользователей. Хотя, позже можно будет реализовать проверку подтвержденности контакта по таблице users, если это потребуется )
		
	],
	...
]
Таким образом, в persons можно указывать нескольких получателей, с разными типами и по каждому получателю (и по каждому отдельному контакту получателя) можно узнавать статус отправки уведомления.

files:
[
	[
		url => ... Путь до файла который необходимо прикрепить к письму (относительный путь от корня сайта)
		name => ... Наименование файла которое будет передано в письме (указывать вместе с расширением файла)
	]
]
*/
header('Content-Type: application/json;charset=utf-8;');
// -------------------------------------------------------------------------------------------------

//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

// -------------------------------------------------------------------------------------------------

//Проверка прав на запуск скрипта
if( $_POST["check"] != $DP_Config->secret_succession )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit( json_encode($answer) );
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
	$answer["message"] = "Error";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");



// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------------------------

//Почтовый обработчик
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer.php");

// -------------------------------------------------------------------------------------------------

//Получаем входные данные:
if( !isset($_POST["name"]) && !isset($_POST["vars"]) && !isset($_POST["persons"]) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4072);
	exit( json_encode($answer) );
}

$name = $_POST["name"];
$vars = json_decode($_POST["vars"], true);
$persons = json_decode($_POST["persons"], true);
$files = json_decode($_POST["files"], true);

if( !is_array($vars) && !is_array($persons) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4073);
	exit( json_encode($answer) );
}

// -------------------------------------------------------------------------------------------------

//Получаем настройки уведомления
$notify_query = $db_link->prepare("SELECT * FROM `notifications_settings` WHERE `name` = ?;");
$notify_query->execute( array($name) );
$notify = $notify_query->fetch();
if( $notify == false )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(4074);
	exit( json_encode($answer) );
}

//Описание переменных уведомления переводим в PHP-массив
$notify["vars"] = json_decode($notify["vars"], true);

// -------------------------------------------------------------------------------------------------

//Формируем текст и заголовок письма, и, текст sms-сообщения
$email_subject = translate_str_by_id($notify["email_subject"]);
$email_body = translate_str_by_id($notify["email_body"]);
$sms_body = translate_str_by_id($notify["sms_body"]);

//Подставляем значения переменных
//Цикл по описанию переменных
for( $i = 0 ; $i < count($notify["vars"]) ; $i++ )
{
	//Для параметра типа "Текст"
	if( $notify["vars"][$i]["type"] == "text" )
	{
		//В шаблонах заголовка и текста письма, а также в тексте sms, находим переменную по ключу %<имя_переменной>% и заменяем ее на значение, полученное от вызывающего скрипта.
		$email_subject = str_replace("%".$notify["vars"][$i]["name"]."%" , $vars[ $notify["vars"][$i]["name"] ] , $email_subject);
		$email_body = str_replace("%".$notify["vars"][$i]["name"]."%" , $vars[ $notify["vars"][$i]["name"] ] , $email_body);
		$sms_body = str_replace("%".$notify["vars"][$i]["name"]."%" , $vars[ $notify["vars"][$i]["name"] ] , $sms_body);
	}
	//Параметры других типов можно будет реализовать потом, если они вообще будут нужны...
}

//Тексты для уведомлений подготовлены
// -------------------------------------------------------------------------------------------------

//Доработка: замена текста уведомления
require_once($_SERVER["DOCUMENT_ROOT"]."/content/notifications/template.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/notifications/epc_whatsapp_notify.php");

// -------------------------------------------------------------------------------------------------
//Рассылаем уведомление


//Получаем активного оператора, который будет использоваться для отправки SMS-сообщений
$sms_operator_query = $db_link->prepare('SELECT * FROM `sms_api` WHERE `active` = ?;');
$sms_operator_query->execute( array(1) );
$sms_api = $sms_operator_query->fetch();


//По массиву получателей
for( $i=0 ; $i < count($persons) ; $i++ )
{
	//Создаем массив contacts, если тип получателя user_id
	if( $persons[$i]['type'] == 'user_id' )
	{
		$persons[$i]['contacts'] = array();
		$persons[$i]['contacts']['phone'] = array();
		$persons[$i]['contacts']['email'] = array();
	}
	
	
	//Инициализируем поля значениями по-умолчанию
	//Для email
	$persons[$i]['contacts']['email']['tried_to_send'] = false;
	$persons[$i]['contacts']['email']['email_confirmed'] = false;
	$persons[$i]['contacts']['email']['status'] = false;
	//Для телефона
	$persons[$i]['contacts']['phone']['tried_to_send'] = false;
	$persons[$i]['contacts']['phone']['phone_confirmed'] = false;
	$persons[$i]['contacts']['phone']['status'] = false;
	
	
	
	//Вспомогательные паременные
	//Для email
	$email = '';//Контакт
	$email_confirmed = false;//Контакт подтвержден
	$email_to_send = false;//На него нужно отправить уведомление
	//Для телефона
	$phone = '';//Контакт
	$phone_confirmed = false;//Контакт подтвержден
	$phone_to_send = false;//На него нужно отправить уведомление
	
	
	
	//Если тип получателя - user_id
	if( $persons[$i]['type'] == 'user_id' )
	{
		$user_query = $db_link->prepare("SELECT * FROM `users` WHERE `user_id` = ?;");
		$user_query->execute( array( $persons[$i]['user_id'] ) );
		$user = $user_query->fetch();
		
		if( $user != false )
		{
			//Если у пользователя указан E-mail
			if( !empty($user["email"]) )
			{
				//Добавляем его, если E-mail подтвержден, либо, если в уведомлении выставлен флаг "Отправлять неподтвержденным"
				if( $user["email_confirmed"] || $notify["send_for_not_confirmed"] )
				{
					$email = $user["email"];//Сам E-mail
					$email_confirmed = $user["email_confirmed"];//Статус "Подтвержден/Не подтвержден"
					$email_to_send = true;//Нужно отправить
				}
			}
			
			
			//Если у пользователя указан Телефон
			if( !empty($user["phone"]) )
			{
				//Добавляем его, если Телефон подтвержден, либо, если в уведомлении выставлен флаг "Отправлять неподтвержденным"
				if( $user["phone_confirmed"] || $notify["send_for_not_confirmed"] )
				{
					$phone = $user["phone"];//Сам телефон
					$phone_confirmed = $user["phone_confirmed"];//Статус "Подтвержден/Не подтвержден"
					$phone_to_send = true;//Нужно отправить
				}
			}
		}
	}
	//Для получателя с типом direct_contact
	else if( $persons[$i]['type'] == 'direct_contact' )
	{
		//Будем отправлять, только если указан его email и для уведомления выставлен флаг "Отправлять не подтвержденным"
		if( !empty($persons[$i]['contacts']['email']['value']) && $notify["send_for_not_confirmed"] )
		{
			$email = $persons[$i]['contacts']['email']['value'];
			$email_confirmed = false;
			$email_to_send = true;//Нужно отправить
		}
		
		//Будем отправлять, только если указан его телефон и для уведомления выставлен флаг "Отправлять не подтвержденным"
		if( !empty($persons[$i]['contacts']['phone']['value']) && $notify["send_for_not_confirmed"] )
		{
			$phone = $persons[$i]['contacts']['phone']['value'];
			$phone_confirmed = false;
			$phone_to_send = true;//Нужно отправить
		}
	}
	
	
	
	//Учет настроек отправки на email и телефон на уровне уведомления
	if( $notify['email_on'] == 0 )
	{
		$email_to_send = false;
	}
	if( $notify['sms_on'] == 0 )
	{
		$phone_to_send = false;
	}
	
	
	
	//Учет настроек отправки уведомлений по статусам заказов и статусам позиций заказов
	if( isset( $DP_Config->orders_statuses_notifications_settings ) )
	{
		//Статус заказа менеджеру
		if( $notify['name'] == 'order_status_to_manager' )
		{
			if( $vars['status_ref']['to_manager_email'] == 0 )
			{
				$email_to_send = false;
			}
			if( $vars['status_ref']['to_manager_sms'] == 0 )
			{
				$phone_to_send = false;
			}
		}
		//Статус заказа клиенту
		if( $notify['name'] == 'order_status_to_customer' )
		{
			if( $vars['status_ref']['to_customer_email'] == 0 )
			{
				$email_to_send = false;
			}
			if( $vars['status_ref']['to_customer_sms'] == 0 )
			{
				$phone_to_send = false;
			}
		}
		//Статус позиции заказа менеджеру
		if( $notify['name'] == 'order_item_status_to_manager' )
		{
			if( $vars['status_ref']['to_manager_email'] == 0 )
			{
				$email_to_send = false;
			}
			if( $vars['status_ref']['to_manager_sms'] == 0 )
			{
				$phone_to_send = false;
			}
		}
		//Статус позиции заказа клиенту
		if( $notify['name'] == 'order_item_status_to_customer' )
		{
			if( $vars['status_ref']['to_customer_email'] == 0 )
			{
				$email_to_send = false;
			}
			if( $vars['status_ref']['to_customer_sms'] == 0 )
			{
				$phone_to_send = false;
			}
		}
	}
	
	
	
	//Отправки
	//На E-mail
	if( $email_to_send )
	{
		//Указываем инициатору, чтобы пробуем отправить по email
		$persons[$i]['contacts']['email']['tried_to_send'] = true;
		$persons[$i]['contacts']['email']['email_confirmed'] = $email_confirmed;
		
		//$email = '';
		
		//Отправка
		$docpartMailer = new DocpartMailer();//Объект обработчика
		$docpartMailer->Subject = $email_subject;//Тема письма
		$docpartMailer->Body = $email_body;//Текст письма
		$docpartMailer->CharSet="UTF-8";
		$docpartMailer->addAddress($email, $email);// Добавляем адрес в список получателей
		$docpartMailer->IsSMTP();
		$docpartMailer->IsHTML(true);
		$docpartMailer->SMTPDebug = 1;
		
		if( !empty($files) )
		{
			for($i=0; $i<count($files); $i++){ 
				$docpartMailer->addAttachment($files[$i]['url'], $files[$i]['name']);//Файл
			};
		}
		
		ob_start();
		if( $docpartMailer->Send() )
		{
			//Отправлено без ошибок
			$persons[$i]['contacts']['email']['status'] = true;
		}
		else
		{
			$persons[$i]['contacts']['email']['status'] = false;
		}
		$docpartMailer_debug = ob_get_contents();
		ob_end_clean();
		
		
		//Для записи результата отправки в таблицу debug_results, которая используется для текущего контроля настроек
		$check_record_query = $db_link->prepare("SELECT * FROM `debug_results` WHERE `name` = ?;");
		$check_record_query->execute( array('email') );
		$check_record = $check_record_query->fetch();
		if( $check_record == false )
		{
			$db_link->prepare('INSERT INTO `debug_results` (`name`, `status`, `debug_result`, `time`) VALUES (?,?,?,?);')->execute( array('email', $persons[$i]['contacts']['email']['status'], $docpartMailer_debug, time()) );
		}
		else
		{
			$db_link->prepare("UPDATE `debug_results` SET `status`=?, `debug_result`=?, `time`=? WHERE `name` = ?;")->execute( array($persons[$i]['contacts']['email']['status'], $docpartMailer_debug, time(), 'email') );
		}
	}
	
	//На телефон
	if( $phone_to_send )
	{
		//Указываем инициатору, чтобы пробуем отправить на телефон
		$persons[$i]['contacts']['phone']['tried_to_send'] = true;
		$persons[$i]['contacts']['phone']['phone_confirmed'] = $phone_confirmed;
		
		$phone = str_replace(array(' ', '+7', '(', ')', '-', '_', '+'), '', $phone);
		
		//Отправка
		$for_debug_result = '';
		if( $sms_api != false )
		{
			//Отправка sms через общий интерфейс
			$postdata = http_build_query(
				array(
					'check' => $DP_Config->secret_succession,
					'body' => $sms_body,
					'main_field' => $phone,
					'parameters_values' => $sms_api["parameters_values"]
				)
			);
			$curl = curl_init();
			curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/sms/handlers/".$sms_api["handler"]."/send_sms.php");
			curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
			curl_setopt($curl, CURLOPT_POST, true);
			curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
			curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
			curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
			$curl_result = curl_exec($curl);
			curl_close($curl);
			$curl_result = json_decode($curl_result, true);
			if( $curl_result["status"] == true )
			{
				//Отправлено без ошибок
				$persons[$i]['contacts']['phone']['status'] = true;
				
				//Для записи результатов отладки
				$for_debug_result = translate_str_by_id(4075);
			}
			else
			{
				//Ошибка отправки (Причина - некорректные настройки оператора, или нет денег на балансе у оператора)
				$persons[$i]['contacts']['phone']['status'] = false;
				
				//Для записи результатов отладки
				if( isset($curl_result["message"]) )
				{
					$for_debug_result = $curl_result["message"];
				}
				else
				{
					$for_debug_result = translate_str_by_id(4076);
				}
			}
		}
		else
		{
			//Ошибка отправки (не подключен SMS-оператор)
			$persons[$i]['contacts']['phone']['status'] = false;
			
			$for_debug_result = translate_str_by_id(4077);
		}
		
		
		//Для записи результата отправки в таблицу debug_results, которая используется для текущего контроля настроек
		$check_record_query = $db_link->prepare("SELECT * FROM `debug_results` WHERE `name` = ?;");
		$check_record_query->execute( array('sms') );
		$check_record = $check_record_query->fetch();
		if( $check_record == false )
		{
			$db_link->prepare('INSERT INTO `debug_results` (`name`, `status`, `debug_result`, `time`) VALUES (?,?,?,?);')->execute( array('sms', $persons[$i]['contacts']['phone']['status'], $for_debug_result, time()) );
		}
		else
		{
			$db_link->prepare("UPDATE `debug_results` SET `status`=?, `debug_result`=?, `time`=? WHERE `name` = ?;")->execute( array($persons[$i]['contacts']['phone']['status'], $for_debug_result, time(), 'sms') );
		}
		
	}

	if( !isset($persons[$i]['contacts']['whatsapp']) )
	{
		$persons[$i]['contacts']['whatsapp'] = array('tried_to_send' => false, 'status' => false, 'error' => '');
	}
	if( $phone != '' && function_exists('epc_wa_dispatch_for_person') )
	{
		$wa = epc_wa_dispatch_for_person($db_link, $DP_Config, $notify, $vars, $sms_body, $email_body, $phone, true);
		$persons[$i]['contacts']['whatsapp'] = array(
			'tried_to_send' => $wa['tried_to_send'],
			'status' => $wa['status'],
			'error' => $wa['error'],
		);
	}
		
}
// -------------------------------------------------------------------------------------------------
//Выдаем результат
$answer = array();
$answer["status"] = true;//Означает, что блок отправки отработал. Результаты по каждому получателю (и по каждому контакту каждого получателя) смотри в persons
$answer["message"] = "";
$answer["persons"] = $persons;
exit( json_encode($answer) );
// -------------------------------------------------------------------------------------------------
?>