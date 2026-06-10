<?php
/*
Скрипт для работы с контактами пользователя. Работает ТОЛЬКО с контактами авторизованного пользователя.
Выполняет функции:
- указание контака (если контакт не указан)
- смена контакта (если контакт указан)
- отправка кода подтверждения контакта (если контакт указан и не подтвержден)



Подробно по функциям
1. Указание контакта (set)
Сработает, если:
- пользователь авторизован
- контакт указанного типа не заполнен в основном поле (т.е. у пользователя никогда не был указан данный контакт)


2. Смена контакта (change)
Сработает, если:
- пользователь авторизован
- контакт указанного типа заполнен в основном поле
- для контакта указанного типа нет ограничений не отправку новых запросов (текущее время больше значения в колонке _code_send_lock_expired)


3. Подтверждение контакта (confirm)
Т.е. нужно просто отправить код подтверждения для ранее указанного контакта.
Ссылка/код подтверждения контакта отправится при условии:
- пользователь авторизован
- контакт указанного типа заполнен в основном поле
- контакт указанного типа НЕ подтвержден
//- для контакта указанного типа НЕТ незавершенных запросов (т.е. _code и _code_expired не заполнены)
- для контакта указанного типа нет ограничений не отправку новых запросов (текущее время больше значения в колонке _code_send_lock_expired)



Входные данные:
- тип контакта (email/phone)
- действия (set/change/confirm)
- контакт (Т.е. конректный телефон или email. Заполняетс при действиях - Указание или Смена)



Проверка контакта (при указании или смене)
- проверить по регулярному выражению соответствующего типа
- проверить уникальность по двум полям (основное и _new)
*/
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
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


try
{
	// -----------------------------------------------------------------------------------
	//Старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception(translate_str_by_id(2132));
	}
	// -----------------------------------------------------------------------------------
	//Проверяем, авторизован ли пользователь
	if( DP_User::getUserId() == 0 )
	{
		throw new Exception(translate_str_by_id(4689));
	}
	$user_id = DP_User::getUserId();
	$user_profile = DP_User::getUserProfile();
	// -----------------------------------------------------------------------------------
	//Пользователь авторизован
	//Проверяем входные данные
	
	//Наличие полей, которые точно должны быть
	if( !isset($_POST['type']) || !isset($_POST['action']) || !isset($_POST['csrf_guard_key']) )
	{
		throw new Exception(translate_str_by_id(4690));
	}
	
	
	//Защита от CSRF-атак
	$user_session = DP_User::getUserSession();
	if( !isset( $_POST["csrf_guard_key"] ) || $user_session["csrf_guard_key"] != $_POST["csrf_guard_key"] )
	{
		throw new Exception(translate_str_by_id(4691));
	}
	
	
	//Тип контакта
	$type = $_POST['type'];
	//$type используется в SQL-запросах, поэтому проверяем значение
	if( $type != 'email' && $type != 'phone' )
	{
		throw new Exception(translate_str_by_id(4690));
	}
	
	//Действие с контактом
	$action = $_POST['action'];
	if( array_search($action, array('set', 'change', 'confirm') ) === false )
	{
		throw new Exception(translate_str_by_id(4690));
	}
	
	
	//Проверяем контакт (для указания и смены) - на корректность и уникальность
	if( array_search($action, array('set', 'change') ) !== false )
	{
		if( !isset($_POST['contact']) )
		{
			throw new Exception(translate_str_by_id(4690));
		}
		$contact = $_POST['contact'];
		
		
		//Соответствие регулярному выражению
		//Получаем регулярное выражение для контакта
		$regexp_query = $db_link->prepare("SELECT `regexp` FROM `reg_fields` WHERE `name` = ?;");
		$regexp_query->execute( array($type) );
		$regexp = $regexp_query->fetchColumn();
		preg_match("/".$regexp."/", $contact, $matches);
		$regexp_ok = true;
		if(!empty($regexp))
		{
			if( count($matches) == 1 )
			{
				if( $matches[0] != $contact )
				{
					$regexp_ok = false;
				}
			}
			else
			{
				$regexp_ok = false;
			}
		}
		if( !$regexp_ok )
		{
			throw new Exception(translate_str_by_id(4692));
		}
		
		
		//Проверяем уникальность контакта. Такой контакт не должен быть у других пользователей, причем, как в основном поле, так и в поле _new
		$contact_check_query = $db_link->prepare('SELECT COUNT(*) FROM `users` WHERE (`'.$type.'` = ? OR `'.$type.'_new` = ?) AND `user_id` != ?;');
		$contact_check_query->execute( array($contact, $contact, $user_id) );
		if( $contact_check_query->fetchColumn() > 0 )
		{
			throw new Exception(translate_str_by_id(4693));
		}
	}
	
	// -----------------------------------------------------------------------------------
	//Проверка условий по действиям
	
	//Для действия "Указать контакт"
	if( $action == 'set' )
	{
		if( !empty( $user_profile[$type] ) )
		{
			throw new Exception(translate_str_by_id(4690));
		}
		
		//$contact - задан
		$contact_confirmed = 0;
		$contact_new = '';
		//$contact_code - задан
		//$contact_code_expired - задан
		$contact_code_attempts = 0;
	}
	
	
	//Для действия "Сменить контакт"
	if( $action == 'change' )
	{
		if( empty($user_profile[$type]) )
		{
			throw new Exception(translate_str_by_id(3342));
		}
		
		if( $user_profile[$type."_code_send_lock_expired"] > time() )
		{
			throw new Exception(translate_str_by_id(4694));
		}
		
		$contact = $user_profile[$type];//Пока остается старый
		$contact_confirmed = $user_profile[$type.'_confirmed'];//Пока остается старый
		$contact_new = $_POST['contact'];//Выше проверен
		//$contact_code - задан
		//$contact_code_expired - задан
		$contact_code_attempts = 0;
	}
	
	
	//Для действия "Подтвердить контакт"
	if( $action == 'confirm' )
	{
		//Контакт берется из учетки пользователя
		$contact = $user_profile[$type];
		
		//Должен быть указан
		if( empty($contact) )
		{
			throw new Exception(translate_str_by_id(4695));
		}
		//Должен быть не подтвержден
		if( $user_profile[$type.'_confirmed'] != 0 )
		{
			throw new Exception(translate_str_by_id(3342));
		}
		
		if( $user_profile[$type."_code_send_lock_expired"] > time() )
		{
			throw new Exception(translate_str_by_id(4694));
		}
		
		
		//$contact - задан
		$contact_confirmed = 0;
		$contact_new = '';
		//$contact_code - задан
		//$contact_code_expired - задан
		$contact_code_attempts = 0;
	}
	
	// -----------------------------------------------------------------------------------
	//Пользователь авторизован, входные данные проверены.
	
	//Для любого действия нужно будет отправить код. Формируем код.
	if( $type == "email" )
	{
		$contact_code = md5(md5($contact.$contact_new.rand(100000,999999)).md5($DP_Config->secret_succession));//Код активации
	}
	else
	{
		$contact_code = rand(100000,999999);
	}
	$contact_code_expired = time() + 1800;//30 минут, как при регистрации
	// -----------------------------------------------------------------------------------

	//Записываем в БД соответсвующие поля
	
	//Ставим на 5 минут блокировку отправки новых кодов подтверждения (например, чтобы предотвратить чрезмерное расходование баланса у SMS-оператора)
	$code_send_lock_expired = time()+300;
	
	if( !$db_link->prepare('UPDATE `users` SET `'.$type.'` = ?, `'.$type.'_confirmed` = ?, `'.$type.'_new` = ?, `'.$type.'_code` = ?, `'.$type.'_code_expired` = ?, `'.$type.'_code_attempts` = ?, `'.$type.'_code_send_lock_expired` = ? WHERE `user_id` = ?;')->execute( array($contact, $contact_confirmed, $contact_new, $contact_code, $contact_code_expired, $contact_code_attempts, $code_send_lock_expired, $user_id) ) )
	{
		throw new Exception(translate_str_by_id(2429));
	}
	
	
	// -----------------------------------------------------------------------------------
	//Дошли до сюда, значит, в базе записали необходимые поля для указания/смены/подтверждения контакта. Теперь необходимо отправить пользователю код подтверждения
	//Поля для уведомления:
	$notify_name = $type.'_confirm_other';//Универсальное уведомление для подтверждения контакта (для указания/смены/подтверждения). Отдельное для email и для телефона
	
	
	//Массив получателей в соответствии с API скрипта уведомлений
	$email = '';
	$phone = '';
	if($type == 'email')
	{
		if( $action == 'change' )
		{
			$email = $contact_new;//Отправим на новый контакт
		}
		else
		{
			$email = $contact;
		}
	}
	else
	{
		if( $action == 'change' )
		{
			$phone = $contact_new;//Отправим на новый контакт
		}
		else
		{
			$phone = $contact;
		}
	}
	$persons = array(
		array(
			'type'=>'direct_contact',
			'contacts'=>array(
					'email'=>array('value'=>$email),
					'phone'=>array('value'=>$phone)
				)
			) 
	);
	
	//Переменные для шаблонов уведомления. Уведомления email_confirm_other и phone_confirm_other не конфликтуют в именах переменных, поэтому, вне зависимости от $type заполним массив значениями для всех возможных переменных для этих двух уведомлений.
	$notify_vars = array();
	$notify_vars["site_name"] = translate_str_by_id($DP_Config->site_name);//Название сайта 
	$notify_vars["email_confirm_href"] = "<a target='_blank' href='".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/users/confirm_contact?code=$contact_code&u_id=$user_id&type=email'>".translate_str_by_id(4696)."</a>";//Ссылка для подтверждения E-mail
	$notify_vars["phone_confirm_code"] = $contact_code;
	
	
	
	//Отправка уведомления по общий интерфейс
	$postdata = http_build_query(
		array(
			'check' => $DP_Config->secret_succession,
			'name' => $notify_name,
			'vars' => json_encode($notify_vars),
			'persons' => json_encode($persons),
			'multilang_params' => json_encode($multilang_params)
		)
	);
	$curl = curl_init();
	curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/notifications/send_notify.php");
	curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
	curl_setopt($curl, CURLOPT_POST, true);
	curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
	curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
	curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
	$curl_result = curl_exec($curl);
	curl_close($curl);
	$curl_result = json_decode($curl_result, true);
	if($curl_result["status"] == false)
	{
		//Причина - некорректные данные, нет прав на запуск и т.д.
		throw new Exception(translate_str_by_id(4697));
	}
	else
	{
		//Скрипт отправки выдал статус true, теперь НЕОБХОДИМО проверить статус отправки по конкретному контакту
		//Мы указывали единственный контакт, поэтому его и проверяем.
		if( ! $curl_result["persons"][0]['contacts'][$type]['status'] )
		{
			throw new Exception(translate_str_by_id(4698));
		}
	}
	
	
	
	// -----------------------------------------------------------------------------------
	//Дошли до сюда, значит выполнено ОК
	$db_link->commit();//Коммитим все изменения и закрываем транзакцию
	
	
	
	$answer = array();
	$answer['status'] = true;
	$answer['message'] = '';
	$answer['type'] = $type;
	$answer['action'] = $action;
	$answer['contact'] = $contact;
	exit( json_encode($answer) );
}
catch (Exception $e)
{
	//Откатываем все изменения БД
	$db_link->rollBack();
	
	
	$answer = array();
	$answer['status'] = false;
	$answer['message'] = $e->getMessage();
	exit( json_encode($answer) );
}
?>