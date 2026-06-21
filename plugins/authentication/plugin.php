<?php
/**
 * Плагин аутентификации
 * Этот плагин также указывает время последнего визита пользователя
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


if( !isset($_SERVER['HTTP_USER_AGENT']) )
{
	$_SERVER['HTTP_USER_AGENT'] = '';
}



//Функция проверки робота (является ли посетитель роботом)
function isBot()
{
	$bots = array(
	'rambler','googlebot','aport','yahoo','msnbot','turtle','mail.ru','omsktele',
	'yetibot','picsearch','sape.bot','sape_context','gigabot','snapbot','alexa.com',
	'megadownload.net','askpeter.info','igde.ru','ask.com','qwartabot','yanga.co.uk',
	'scoutjet','similarpages','oozbot','shrinktheweb.com','aboutusbot','followsite.com',
	'dataparksearch','google-sitemaps','appEngine-google','feedfetcher-google',
	'liveinternet.ru','xml-sitemaps.com','agama','metadatalabs.com','h1.hrn.ru',
	'googlealert.com','seo-rus.com','yaDirectBot','yandeG','yandex',
	'yandexSomething','Copyscape.com','AdsBot-Google','domaintools.com',
	'Nigma.ru','bing.com','dotnetdotcom','SiteAnalyzerbot','bot'
	);
	
	foreach($bots as $bot)
	{
		if( stripos( strtolower($_SERVER['HTTP_USER_AGENT']), strtolower($bot) ) !== false )
		{
			return true;
		}
	}
	return false;
}





/*
Оптимизируем нагрузку на БД - исключаем обработку сессий для роботов.
*/
if( ! isBot() )
{
	//Сессия для неавторизованных. Проверяем, если у пользователя нет сессии (т.е. он неавторизован - добавляем ему сессию неавторизованного пользователя)
	$to_create_session = false;
	if(DP_User::getUserId() == 0)
	{
		if( ! isset($_COOKIE["session"]) || ! isset($_COOKIE["u_id"]) )
		{
			//Сессии точно нет - нужно создать
			$to_create_session = true;
		}
		else//Обе куки выставлены - проверяем актуальность
		{
			$check_session_query = $db_link->prepare("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `user_id` = ?;");
			$check_session_query->execute( array($_COOKIE["session"], $_COOKIE["u_id"]) );
			if( $check_session_query->fetchColumn() != 1 )
			{
				//Сессии точно нет - нужно создать
				$to_create_session = true;
			}
			else
			{
				//Сессия есть - ставим время последней активности
				$db_link->prepare("UPDATE `sessions` SET `last_activiti_time` = ? WHERE `session` = ? AND `user_id` = ?;")->execute( array(time(), $_COOKIE["session"], $_COOKIE["u_id"]) );
			}
		}
	}
	if($to_create_session)
	{
		//Здесь создаем сессию для неавторизованного пользователя
		$cookietime = time()+9999999;//Куки на долго
		$session_succession = md5(time().rand(1000, 1000000000).$DP_Config->secret_succession.$_SERVER["REMOTE_ADDR"]);//Сессия
		
		
		//Ключ защиты от CSRF-атак:
		$csrf_guard_key_1 = sha1( $DP_Config->secret_succession . $session_succession . $_SERVER["REMOTE_ADDR"] . $_SERVER["HTTP_USER_AGENT"] );
		
		
		if( $db_link->prepare("INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `last_activiti_time`, `csrf_guard_key`) VALUES (?, ?, ?, ?, ?, ?);")->execute( array($session_succession, 0, time(), '', time(), $csrf_guard_key_1) ) )
		{
			setcookie("session", $session_succession, $cookietime, "/", '',false,true);//Сессия
			setcookie("u_id", "0", $cookietime, "/", '',false,true);//ID пользователя (0)
			
			
			$_COOKIE["session"] = $session_succession;
			$_COOKIE["u_id"] = "0";
			
			//header("Location: ".getPageUrl());
		}
		else
		{
			exit();
		}
	}
	if(true)
	{
		//Очистка старых сессий для user_id = 0 (у которых последняя активность была более месяца назад)
		$last_activiti_time_to_del = time()-2592000;//До этого времени - удалять
		
		//Пользовательские настройки
		$db_link->prepare("DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?);")->execute( array(0, $last_activiti_time_to_del) );
		
		//Сами сессии
		$db_link->prepare("DELETE FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?;")->execute( array(0, $last_activiti_time_to_del) );
	}





	//Сначала проверяем, авторизован ли пользователь. Если да - ставим время время визита
	if(DP_User::getUserId() > 0)
	{
		//В учетную запись пользователя
		$db_link->prepare('UPDATE `users` SET `time_last_visit`= ? WHERE `user_id`=?;')->execute( array(time(), DP_User::getUserId()) );
		
		//В учетную запись сессии (на разных устройствах у пользователя разные сессии). Время последней активности для сессии нужно для функции очистки старых неактивных сессий
		$db_link->prepare("UPDATE `sessions` SET `last_activiti_time` = ? WHERE `session` = ? AND `user_id` = ?;")->execute( array(time(), $_COOKIE["session"], DP_User::getUserId()) );
	}


	if(!empty($_POST["authentication"]))
	{
		// -------------------------------------------------------------------------------
		//Защита от CSRF-атак
		require($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
		// -------------------------------------------------------------------------------
		
		if( !empty($_POST["auth_contact"]) && !empty($_POST["auth_contact_type"]) )
		{
			$auth_contact = $_POST["auth_contact"];
			$password = $_POST["password"];
			$auth_contact_type = $_POST["auth_contact_type"];
			//$auth_contact_type подставляется в SQL-запрос, поэтому провряем его значение
			if( $auth_contact_type != 'email' && $auth_contact_type != 'phone' )
			{
				exit;
			}
			
			$auth_record = false;
			
			//В зависимости от типа авторизации (по паролю, по коду)
			if( isset($_POST["code"]) )
			{
				require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
				$user_session = DP_User::getUserSession();
				$session_data = json_decode($user_session["data"], true);
				$faCode = $user_session["2fa_code"];
				$faAttempts = (int) $user_session["2fa_attempts"];
				if($faAttempts < 1)
				{
					$error_message = translate_str_by_id(4003);
				}
				else if($faCode === $_POST["code"])
				{
					if($session_data["expireFaCode"] < time())
					{
						$error_message = translate_str_by_id(5642);
					}
					else
					{
						//OK
						//Код подтвержден
						$auth_query = $db_link->prepare('SELECT * FROM `users` WHERE `'.$auth_contact_type.'`=? AND `'.$auth_contact_type.'_confirmed` = ? AND `unlocked` =?;');
						$auth_query->execute( array($auth_contact, 1, 1) );
						$auth_record = $auth_query->fetch();
					}
				}
				else
				{
					$faAttempts = $faAttempts - 1;
					$sqlUpdateAttempt = $db_link->prepare("UPDATE `sessions` SET `2fa_attempts` = ? WHERE `session` = ?;");
					$sqlUpdateAttempt->execute([$faAttempts, $user_session["session"]]);
					
					$error_message = translate_str_by_id(5652);
				}
			}
			else
			{
				/*
				Ищем по указанному типу контакта (email/phone) пользователя, у которого:
				- найден указанный контакт
				- указанный контакт подтвержден
				- пользователь не заблокирован админом
				- пароль от учетной записи указан верно
				*/
				// Dual auth: bcrypt first, fall back to legacy MD5 with transparent upgrade
				$auth_query = $db_link->prepare('SELECT * FROM `users` WHERE `'.$auth_contact_type.'`=? AND `'.$auth_contact_type.'_confirmed` = ? AND `unlocked` =?;');
				$auth_query->execute( array($auth_contact, 1, 1) );
				$auth_record = $auth_query->fetch();
				if ($auth_record) {
					$_storedPw = (string) ($auth_record['password'] ?? '');
					$_pwOk = false;
					if (password_verify($password, $_storedPw)) {
						$_pwOk = true;
					} elseif (md5($password.$DP_Config->secret_succession) === $_storedPw) {
						$_pwOk = true;
						$_upgFile = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_password_upgrade.php';
						if (is_file($_upgFile)) {
							require_once $_upgFile;
							if (function_exists('epc_password_upgrade_if_needed') && function_exists('epc_password_is_legacy_md5') && epc_password_is_legacy_md5($_storedPw)) {
								epc_password_upgrade_if_needed($db_link, (int) $auth_record['user_id'], $password, $_storedPw);
							}
						}
					}
					if (!$_pwOk) {
						$auth_record = false;
					}
				}
			}
			
			if( $auth_record == false )
			{
				//Аутентификация не проходит
				//Добавляем к HTML-коду скрипт для сообщения
				if(!empty($_POST["wrong_authentication_tag"]))//Есть имя тега, куда выводить сообщение об ошибке аутентификации
				{
					$DP_Template->html = $DP_Template->html."\n<script>document.getElementById(\"".$_POST["wrong_authentication_tag"]."\").innerHTML = \"".translate_str_by_id(4787)."\";</script>";
				}
				else//Конкретный тег не передан - выводим сообщение в стандартный
				{
					$DP_Template->html = $DP_Template->html."\n<script>alert(\"".translate_str_by_id(4787)."\");</script>";
				}
			}
			else//Успешная аутентификация
			{
				//Определяем id пользователя:
				$user_id = $auth_record["user_id"];
				$time = time();
				
				
				//Сначала очищаем устаревшие сессии данного пользователя
				//Пользовательские настройки
				$db_link->prepare("DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?);")->execute( array($user_id, $last_activiti_time_to_del) );
				//Сами сессии
				$db_link->prepare("DELETE FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?;")->execute( array($user_id, $last_activiti_time_to_del) );
				
				
				$session_succession = md5($auth_contact.$user_id.$time.$DP_Config->secret_succession);//Код сессии
				
				//Ключ защиты от CSRF-атак:
				$csrf_guard_key_1 = sha1( $DP_Config->secret_succession . $session_succession . $_SERVER["REMOTE_ADDR"] . $_SERVER["HTTP_USER_AGENT"] );
				
				//Записываем сеcсию в БД
				$db_link->prepare('INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `csrf_guard_key`) VALUES (?, ?, ?, ?, ?);')->execute( array($session_succession, $user_id, $time, '', $csrf_guard_key_1) );

				
				//Записываем сессию в куки:
				if(!empty($_POST["rememberme"]))
				{
					$cookietime = time()+9999999;//Запоминаем пользователя на долго
				}
				else
				{
					$cookietime = 0; // На время работы браузера
				}
				setcookie("session", $session_succession, $cookietime, "/", '',false,true);
				setcookie("u_id", $user_id, $cookietime, "/", '',false,true);
				
				//В куки есть сессия неавторизованного пользователя, переместим товары в корзину авторизованного пользователя
				if( isset( $_COOKIE["session"] ))
				{
					//Проверим есть ли товары в корзине этого пользователя
					$user_cart_query = $db_link->prepare("SELECT `id` FROM `shop_carts` WHERE `user_id` = 0 AND `session_id` = (SELECT `id` FROM `sessions` WHERE `session` = ?);");
					$user_cart_query->execute( array(str_replace(' ','',$_COOKIE["session"])) );
					
					while($shop_carts_id = $user_cart_query->fetch())
					{
						if( (int) $shop_carts_id['id'] > 0 )
						{
							$db_link->prepare('UPDATE `shop_carts` SET `user_id` = ?, `session_id` = 0 WHERE `id` = ?;')->execute( array($user_id, $shop_carts_id['id']) );
						}
					}
				}
				
				$epc_vat_login_file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
				if (is_readable($epc_vat_login_file) && $db_link instanceof PDO) {
					require_once $epc_vat_login_file;
					epc_uae_customer_vat_sync($db_link, (int)$user_id);
				}

				if (is_readable($_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php')) {
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
					$login_html = epc_build_auth_event_html('Customer signed in', (int)$user_id, (string)$auth_contact, '');
					epc_staff_send_notify('epc_customer_login', array(
						'user_id' => (int)$user_id,
						'login_contact' => (string)$auth_contact,
						'event_html' => $login_html,
					), (int)$user_id, 0, array(), false);
				}

				if( isset($_POST["target"]) )
				{
					require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
					$safeTarget = epc_redirect_safe_target((string)$_POST["target"]);
					header('Location: ' . rtrim($DP_Config->domain_path, '/') . $safeTarget);
				}
				else
				{
					header("Location: ".getPageUrl());//Переадресация на туже страницу
				}
			}
		}
	}
	else if( isset($_POST["logout"]) )
	{
		// -------------------------------------------------------------------------------
		//Защита от CSRF-атак
		require($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
		// -------------------------------------------------------------------------------
		
		if( $_POST["logout"] == "true" )
		{
			if( DP_User::getUserId() > 0 )
			{
				$db_link->prepare('DELETE FROM `sessions` WHERE `session`=? AND `user_id` = ?;')->execute( array($_COOKIE["session"], DP_User::getUserId()) );
				
				//При этом его пользовательские настройки (из таблицы users_options) не затронутся (при очередной авторизации - они будут действовать)
				
				setcookie("session", '', time() - 10000, "/", '',false,true);
				setcookie("u_id", '', time() - 10000, "/", '',false,true);
				header("Location: ".getPageUrl());
			}
			else
			{
				exit;
			}
		}
		else
		{
			exit;
		}
	}
}
?>