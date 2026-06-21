<?php
/**
 * Плагин аутентификации администраторов бэкэнда
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");//Класс пользователь
require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_shared_erp.php");
$epc_rate_limit_file = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_login_rate_limit.php';
if (is_file($epc_rate_limit_file)) { require_once $epc_rate_limit_file; }
$epc_pwd_upgrade_file = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_password_upgrade.php';
if (is_file($epc_pwd_upgrade_file)) { require_once $epc_pwd_upgrade_file; }
$clientErpRouterFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
if (is_file($clientErpRouterFile)) {
	require_once $clientErpRouterFile;
}
$platformErpRouterFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
if (is_file($platformErpRouterFile)) {
	require_once $platformErpRouterFile;
}

function epc_cp_auth_plugin_file($relative)
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	return $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/' . ltrim((string) $relative, '/');
}




//Авторизован ли пользователь, как администратор
if(DP_User::isAdmin() == 0)//Если не авторизован...
{
    if(!empty($_POST["authentication"]))//...но пытается авторизоваться
    {
        if( !empty($_POST["auth_contact"]) && !empty($_POST["password"]) && !empty($_POST["auth_contact_select"]) )
        {
			$auth_contact = $_POST["auth_contact"];
			$contact_type = $_POST["auth_contact_select"];
			$password = $_POST["password"];
            $auth_result = false;//Результат аутентификации
            $sharedLoginMessage = '';
            $demoAuthDetail = '';

			// Rate limiting — block brute-force attempts
			if (function_exists('epc_login_rate_limit_check')) {
				$rlResult = epc_login_rate_limit_check($db_link, $auth_contact);
				if (!empty($rlResult['blocked'])) {
					$auth_result = false;
					$retryMin = max(1, (int) ceil(($rlResult['retry_after'] ?? 900) / 60));
					$sharedLoginMessage = 'Too many failed attempts. Please wait ' . $retryMin . ' minutes before trying again.';
					goto epc_auth_result_check;
				}
			}
            
			
			//$contact_type используется в SQL-запросах. Проверяем значение
			if( $contact_type != 'email' && $contact_type != 'phone' )
			{
				exit;
			}
			
			
            //Если логин и пароль не правильны или этот пользователь не входит в группу Администраторы, то выводим сообщение "Не правильные логин и пароль"
            //Проверка логина и пароль
			$demoCpLogin = function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context();
			$demoAuthDetail = '';
			// Try both legacy MD5 and bcrypt hashing
			$check_user_query = $db_link->prepare('SELECT * FROM `users` WHERE `'.$contact_type.'`=? AND `'.$contact_type.'_confirmed`=? AND `unlocked`=?;');
			$check_user_query->execute( array(htmlentities($auth_contact), 1, 1) );
			$user_record = $check_user_query->fetch();
			if ($user_record) {
				$storedPw = (string) ($user_record['password'] ?? '');
				$pwOk = false;
				if (password_verify($password, $storedPw)) {
					$pwOk = true;
				} elseif (md5($password . $DP_Config->secret_succession) === $storedPw) {
					$pwOk = true;
					// Transparent upgrade to bcrypt
					if (function_exists('epc_password_upgrade_if_needed')) {
						epc_password_upgrade_if_needed($db_link, (int) $user_record['user_id'], $password, $storedPw);
					}
				}
				if (!$pwOk) {
					$user_record = false;
				}
			}
            if($user_record == false)
            {
                $auth_result = false;
				if ($demoCpLogin) {
					$probe = $db_link->prepare('SELECT `user_id`, `email_confirmed`, `unlocked` FROM `users` WHERE `'.$contact_type.'`=? LIMIT 1;');
					$probe->execute(array(htmlentities($auth_contact)));
					$probeRow = $probe->fetch();
					if (!$probeRow) {
						$demoAuthDetail = 'No demo CP account for that email. Use the credentials from your demo email or ask the operator to reset login.';
					} elseif ((int) ($probeRow['email_confirmed'] ?? 0) !== 1) {
						$demoAuthDetail = 'Email is not confirmed on this demo account.';
					} elseif ((int) ($probeRow['unlocked'] ?? 0) !== 1) {
						$demoAuthDetail = 'This demo account is locked.';
					} else {
						$demoAuthDetail = 'Incorrect password. Use the temp password from your demo email or Tenant control center.';
					}
				}
            }
            else//Логин и пароль есть такие, теперь нужно проверить группу
            {
                $user_id = $user_record["user_id"];
				if ($demoCpLogin && function_exists('epc_portal_demo_ensure_cp_user_backend_groups')) {
					epc_portal_demo_ensure_cp_user_backend_groups($db_link, (int) $user_id);
				}
                
                //Получаем список групп, допущенных к управлению бэкэндом
                $backend_groups_list = array();//Список групп, допущенных до бэкэнда
                $backend_groups_list = getBackendGroups(NULL, $backend_groups_list);//ПОЛУЧАЕМ СПИСОК ГРУПП, ДОПУЩЕННЫХ К БЭКЭНДУ
                
                //Получаем список групп, к котором относится данный пользователь
                $user_groups_list = array();
				
				$user_groups_list_query = $db_link->prepare("SELECT * FROM `users_groups_bind` WHERE `user_id` = ?;");
				$user_groups_list_query->execute( array($user_id) );
                while($user_group_record = $user_groups_list_query->fetch() )
                {
                    array_push($user_groups_list, $user_group_record["group_id"]);
                }
                
                //Теперь ищем первое совпадение элементов списка backend_groups_list и user_groups_list
                //Если есть совпадение - есть допуск, если совпадения нет $auth_result = false;
                $access_denied = true;//Доступ запрещен
                for($i = 0 ; $i < count($backend_groups_list); $i++)
                {
                    for($j = 0 ; $j < count($user_groups_list) ; $j++)
                    {
                        if($backend_groups_list[$i] == $user_groups_list[$j])
                        {
                            //!!!Есть допуск!
                            $access_denied = false;
                            break;
                        }
                    }
                    if(!$access_denied)break;
                }//~for($i)
                
                //После всех проверок ставим результат аутентификации
                if($access_denied)
                {
                    $auth_result = false;
					if ($demoCpLogin) {
						$demoAuthDetail = 'Demo CP account lacks backend permissions. Login was repaired — try again, or run epc-demo-cp-reset-login.php.';
					}
                }
                else
                {
                    $auth_result = true;
                }
            }
            
			
            
            $sharedPick = isset($_POST['epc_erp_tenant_pick']) ? (string) $_POST['epc_erp_tenant_pick'] : '';
            $sharedLogin = array();
            $sharedLoginMessage = '';
            $clientErpLogin = function_exists('epc_client_erp_is_active') && epc_client_erp_is_active();
            $platformErpLogin = function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active();
            if ($clientErpLogin && function_exists('epc_portal_shared_erp_complete_login')) {
                $forcedSiteKey = function_exists('epc_client_erp_site_key') ? epc_client_erp_site_key() : '';
                if ($sharedPick === '' && $forcedSiteKey !== '') {
                    $sharedPick = $forcedSiteKey;
                }
                $sharedLogin = epc_portal_shared_erp_complete_login($auth_contact, $password, $contact_type, $sharedPick);
                if (!empty($sharedLogin['ok']) && !empty($sharedLogin['redirect'])) {
                    header('Location: ' . $sharedLogin['redirect']);
                    exit;
                }
                if (!empty($sharedLogin['pick']) && is_array($sharedLogin['pick'])) {
                    $auth_result = 'pick';
                } elseif (!empty($sharedLogin['message'])) {
                    $sharedLoginMessage = (string) $sharedLogin['message'];
                }
            } elseif ($platformErpLogin
                && function_exists('epc_portal_shared_erp_email_is_tenant_only')
                && epc_portal_shared_erp_email_is_tenant_only($auth_contact, $contact_type)) {
                $auth_result = false;
                $siteKey = function_exists('epc_portal_shared_erp_site_key_for_contact')
                    ? epc_portal_shared_erp_site_key_for_contact($auth_contact, $contact_type)
                    : '';
                if ($siteKey !== '' && function_exists('epc_client_erp_login_url')) {
                    $sharedLoginMessage = 'Company ERP accounts must sign in at '
                        . epc_client_erp_login_url($siteKey)
                        . ' — not Platform ERP at /cp/platform-erp/.';
                } else {
                    $sharedLoginMessage = 'Use your company client ERP login URL.';
                }
            } elseif (!$demoCpLogin
                && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()
                && !$platformErpLogin
                && function_exists('epc_portal_shared_erp_email_is_tenant_only')
                && epc_portal_shared_erp_email_is_tenant_only($auth_contact, $contact_type)) {
                $auth_result = false;
                $siteKey = function_exists('epc_portal_shared_erp_site_key_for_contact')
                    ? epc_portal_shared_erp_site_key_for_contact($auth_contact, $contact_type)
                    : '';
                if ($siteKey !== '' && function_exists('epc_client_erp_login_url')) {
                    $sharedLoginMessage = 'Company ERP accounts must sign in at '
                        . epc_client_erp_login_url($siteKey)
                        . ' — not the Super CP login at /cp/.';
                } else {
                    $sharedLoginMessage = 'Use your company ERP login URL — not Super CP /cp/.';
                }
            }

            if ($auth_result === true
                && !$demoCpLogin
                && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()
                && !$clientErpLogin
                && !$platformErpLogin
                && function_exists('epc_portal_shared_erp_email_is_tenant_only')
                && epc_portal_shared_erp_email_is_tenant_only($auth_contact, $contact_type)) {
                $auth_result = false;
                if ($sharedLoginMessage === '') {
                    $sharedLoginMessage = 'Use your company ERP login URL — not Super CP /cp/.';
                }
            }

            epc_auth_result_check:
            //ЗДЕСЬ ИДЕТ ПРОВЕРКА ФЛАГА $auth_result...
            if($auth_result == false && $auth_result !== 'pick')
            {
                if (!empty($sharedLogin['pick']) && is_array($sharedLogin['pick'])) {
                    $auth_result = 'pick';
                } elseif ($sharedLoginMessage !== '') {
                    $auth_result = false;
                } elseif (!empty($sharedLogin['message'])) {
                    $sharedLoginMessage = (string) $sharedLogin['message'];
                }
            }

            if($auth_result == false)
            {
                // Record failed attempt for rate limiting
                if (function_exists('epc_login_rate_limit_record')) {
                    epc_login_rate_limit_record($db_link, $auth_contact, false);
                }
                //ЗАПРЕЩЕНО!!!
                //ДИНАМИЧЕСКИ МЕНЯЕМ ШАБЛОН СТРАНИЦЫ - ФОРМА ВХОДА С СООБЩЕНИЕ О НЕПРАВИЛНЫХ УЧЕТНЫХ ДАННЫХ
                //Путь с файлу шаблона
                $tpl_file_path = epc_cp_auth_plugin_file('plugins/authentication/login_form/template.php');
                $tpl_file = fopen($tpl_file_path, "r");
                $tpl_file_string = fread($tpl_file, filesize($tpl_file_path));//Строка с html/php кодом страницы шаблона
                fclose($tpl_file);
                $DP_Template->id = 0;//ID шаблона ставим равным 0 !ОБЯЗАТЕЛЬНО, Т.К. ПЛАГИН phone_tablet учитывает это значение
                $DP_Template->html = $tpl_file_string;//Присваиваем содержимое шаблона в HTML-код страницы
                $DP_Template->positions = json_decode("[{\"type\":\"head\",\"name\":\"head\",\"caption\":\"head\"},{\"type\":\"main\",\"name\":\"main\",\"caption\":\"main\"}]", true);//Список позиций шаблона
                
                //ДИНАМИЧЕСКИЙ МЕНЯМ ОСНОВНОЕ СОДЕРЖИМОЕ СТРАНИЦЫ
                //ПЕРЕИнициализируем поля объекта DP_Content
                $DP_Content->content_id = 0;
                $DP_Content->content_type = "";
                $DP_Content->title_tag = translate_str_by_id(4015);
                $DP_Content->description_tag = "";
                $DP_Content->keywords_tag = "";
                $DP_Content->author_tag = "";
                //Путь с файлу содержимого
                $form_file_path = epc_cp_auth_plugin_file('plugins/authentication/login_form/form.php');
                $form_file = fopen($form_file_path, "r");
                $form_file_string = fread($form_file, filesize($form_file_path));//Строка с html/php кодом формы
                fclose($form_file);
                $authMsg = $sharedLoginMessage !== ''
                    ? htmlspecialchars($sharedLoginMessage, ENT_QUOTES, 'UTF-8')
                    : ($demoAuthDetail !== ''
                        ? htmlspecialchars($demoAuthDetail, ENT_QUOTES, 'UTF-8')
                        : translate_str_by_id(4020));
                $DP_Content->content = $form_file_string."\n<script>document.getElementById(\"wrong_authentication\").innerHTML = \"".$authMsg."\";</script>";
                $DP_Content->css_js = "";
                $DP_Content->modules_array = array();//Очищаем список модулей
                if ($sharedLoginMessage === '') {
                    $DP_Template->html = str_replace("<div class=\"wrong_authentication\" id=\"wrong_authentication\"></div>","".translate_str_by_id(4021)."",$DP_Template->html);
                }
            }
            else if($auth_result === 'pick')
            {
                $tpl_file_path = epc_cp_auth_plugin_file('plugins/authentication/login_form/template.php');
                $tpl_file = fopen($tpl_file_path, "r");
                $tpl_file_string = fread($tpl_file, filesize($tpl_file_path));
                fclose($tpl_file);
                $DP_Template->id = 0;
                $DP_Template->html = $tpl_file_string;
                $DP_Template->positions = json_decode("[{\"type\":\"head\",\"name\":\"head\",\"caption\":\"head\"},{\"type\":\"main\",\"name\":\"main\",\"caption\":\"main\"}]", true);
                $DP_Content->content_id = 0;
                $DP_Content->content_type = "";
                $DP_Content->title_tag = translate_str_by_id(4015);
                $DP_Content->description_tag = "";
                $DP_Content->keywords_tag = "";
                $DP_Content->author_tag = "";
                $form_file_path = epc_cp_auth_plugin_file('plugins/authentication/login_form/form.php');
                $form_file = fopen($form_file_path, "r");
                $form_file_string = fread($form_file, filesize($form_file_path));
                fclose($form_file);
                $picker = function_exists('epc_portal_shared_erp_login_picker_html')
                    ? epc_portal_shared_erp_login_picker_html($sharedLogin['pick'])
                    : '';
                $DP_Content->content = $form_file_string . $picker;
                $DP_Content->css_js = "";
                $DP_Content->modules_array = array();
            }
            else if($auth_result == true)//Успешная аутентификация
            {
                // Clear rate limit counter on successful login
                if (function_exists('epc_login_rate_limit_clear')) {
                    epc_login_rate_limit_clear($db_link, $auth_contact);
                }
                if (function_exists('epc_login_rate_limit_record')) {
                    epc_login_rate_limit_record($db_link, $auth_contact, true);
                }

                $time = time();
                
                $session_succession = md5($auth_contact.$time.$DP_Config->secret_succession);//Код сессии - собираем его из логина, текущего дампа времени и секретной последовательности
                
				
				//Сначала очищаем старые неактивные сессии данного пользователя
				$last_activiti_time_to_del = time()-2592000;//До этого времени - удалять (30 суток)
				//Пользовательские настройки
				$db_link->prepare("DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?);")->execute( array($user_id, $last_activiti_time_to_del) );
				//Сами сессии
				$db_link->prepare("DELETE FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?;")->execute( array($user_id, $last_activiti_time_to_del) );
				
				
				//Ключ защиты от CSRF-атак:
				$csrf_guard_key = sha1( $DP_Config->secret_succession . $session_succession . $_SERVER["REMOTE_ADDR"] . $_SERVER["HTTP_USER_AGENT"] );
				
                //Записываем сеcсию в БД
                $db_link->prepare("INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`) VALUES (?,?,?,?,?,?,?);")->execute( array($session_succession, $user_id, $time, '', 1, $contact_type,$csrf_guard_key) );
				
                
                //Записываем сессию в куки:
                if(!empty($_POST["rememberme"]))
                {
                    $cookietime = time()+9999999;//Запоминаем пользователя на долго
                }
                else
                {
                    $cookietime = 0; // На время работы браузера
                }
                setcookie("admin_session", $session_succession, $cookietime, "/", '',false,true);
                setcookie("admin_u_id", $user_id, $cookietime, "/", '',false,true);
                
                if ($db_link instanceof PDO && function_exists('epc_cp_menu_cache')) {
                    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';
                    epc_cp_menu_cache($db_link);
                }
                
                if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
                    epc_portal_shared_erp_clear_tenant_cookie();
                }
                if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()
                    && function_exists('epc_platform_erp_shell_url')) {
                    if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
                        epc_portal_shared_erp_clear_tenant_cookie();
                    }
                    if (function_exists('epc_platform_erp_set_cookie')) {
                        epc_platform_erp_set_cookie();
                    }
                    header('Location: ' . epc_platform_erp_shell_url());
                    exit;
                }
                if (function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
                    $backend = trim((string) $DP_Config->backend_dir, '/');
                    if ($backend === '') {
                        $backend = 'cp';
                    }
                    $dest = function_exists('epc_cp_control_url') ? epc_cp_control_url($backend) : ('/' . $backend . '/control');
                    header('Location: ' . $dest);
                    exit;
                } else                if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()
                    && function_exists('epc_portal_demo_cp_site_key')) {
                    $key = epc_portal_demo_cp_site_key();
                    if ($key !== '') {
                        if (function_exists('epc_portal_demo_cp_post_login_url')) {
                            header('Location: ' . epc_portal_demo_cp_post_login_url($key));
                        } elseif (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
                            && function_exists('epc_portal_demo_erp_shell_url')) {
                            header('Location: ' . epc_portal_demo_erp_shell_url($key));
                        } elseif (function_exists('epc_portal_demo_cp_login_url')) {
                            header('Location: ' . epc_portal_demo_cp_login_url($key));
                        }
                        exit;
                    }
                } elseif (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()
                    && function_exists('epc_client_erp_shell_url') && function_exists('epc_client_erp_site_key')) {
                    $key = epc_client_erp_site_key();
                    if ($key !== '') {
                        header('Location: ' . epc_client_erp_shell_url($key));
                        exit;
                    }
                } elseif (function_exists('epc_portal_is_shared_erp_cp_session') && epc_portal_is_shared_erp_cp_session()) {
                    header('Location: ' . epc_portal_shared_erp_shell_url());
                    exit;
                } elseif (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
                    $backend = trim((string) $DP_Config->backend_dir, '/');
                    if ($backend === '') {
                        $backend = 'cp';
                    }
                    $dest = function_exists('epc_cp_control_url') ? epc_cp_control_url($backend) : ('/' . $backend . '/control');
                    header('Location: ' . $dest);
                    exit;
                } else {
                    $dest = function_exists('epc_cp_control_url')
                        ? epc_cp_control_url((string) $DP_Config->backend_dir)
                        : getPageUrl();
                    header('Location: ' . $dest);
                    exit;
                }
            }
        }
        else
        {
            exit();//Нет логина и пароля
        }
    }//if(!empty($_POST["authentication"])) - попытка аутентификации
    else//...Пользователь не авторизован и не пытается авторизоваться - выводим форму входа
    {
        //ДИНАМИЧЕСКИ МЕНЯЕМ ШАБЛОН СТРАНИЦЫ - ОБЫЧНАЯ ФОРМА ВХОДА
        //Путь с файлу шаблона
        $tpl_file_path = epc_cp_auth_plugin_file('plugins/authentication/login_form/template.php');
        $tpl_file = fopen($tpl_file_path, "r");
        $tpl_file_string = fread($tpl_file, filesize($tpl_file_path));//Строка с html/php кодом страницы шаблона
        fclose($tpl_file);
        $DP_Template->id = 0;//ID шаблона ставим равным 0 !ОБЯЗАТЕЛЬНО, Т.К. ПЛАГИН phone_tablet учитывает это значение
        $DP_Template->html = $tpl_file_string;//Присваиваем содержимое шаблона в HTML-код страницы
        $DP_Template->positions = json_decode("[{\"type\":\"head\",\"name\":\"head\",\"caption\":\"head\"},{\"type\":\"main\",\"name\":\"main\",\"caption\":\"main\"}]", true);//Список позиций шаблона
        
        //ДИНАМИЧЕСКИЙ МЕНЯМ ОСНОВНОЕ СОДЕРЖИМОЕ СТРАНИЦЫ
        //ПЕРЕИнициализируем поля объекта DP_Content
        $DP_Content->content_id = 0;
        $DP_Content->content_type = "";
        $DP_Content->title_tag = translate_str_by_id(4015);
        $DP_Content->description_tag = "";
        $DP_Content->keywords_tag = "";
        $DP_Content->author_tag = "";
        //Путь с файлу содержимого
        $form_file_path = epc_cp_auth_plugin_file('plugins/authentication/login_form/form.php');
        $form_file = fopen($form_file_path, "r");
        $form_file_string = fread($form_file, filesize($form_file_path));//Строка с html/php кодом формы
        fclose($form_file);
        $DP_Content->content = $form_file_string;
        $DP_Content->css_js = "";
        $DP_Content->modules_array = array();//Очищаем список модулей данного материала
        $DP_Module_array = array();//Очищаем список объектов "Модуль"
    }//else
}//if(DP_Admin::getAdminId() == 0)
else//Если авторизован...
{
    //...и пытается выйти
    if(!empty($_POST["logout"]))
    {
		// -------------------------------------------------------------------------------
		//Защита от CSRF-атак
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
		// -------------------------------------------------------------------------------
		
		if($_POST["logout"] == "logout")
		{
			if (function_exists('epc_cp_perform_logout')) {
				epc_cp_perform_logout($db_link instanceof PDO ? $db_link : null);
				$dest = function_exists('epc_cp_logout_redirect_url') ? epc_cp_logout_redirect_url() : getPageUrl();
				header('Location: ' . $dest);
				exit;
			}
			$db_link->prepare("DELETE FROM `sessions` WHERE `session`=? AND `user_id`=?;")->execute( array($_COOKIE["admin_session"], $_COOKIE["admin_u_id"]) );
		
			setcookie("admin_session", '', time() - 10000, "/", '',false,true);
			setcookie("admin_u_id", '', time() - 10000, "/", '',false,true);
			if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
				epc_portal_shared_erp_clear_tenant_cookie();
			}
			
			header("Location: ".getPageUrl());
		}
		else
		{
			exit;
		}
    }
	else
	{
		//Авторизован и не пытается выйти - ставим время активности сессии и время последнего визита
		//В учетную запись пользователя (старый вариант)
		$stmt = $db_link->prepare('UPDATE `users` SET `time_last_visit`= ? WHERE `user_id`=?;')->execute( array(time(), DP_User::getAdminId()) );
		
		//В учетную запись сессии (на разных устройствах у пользователя разные сессии). Время последней активности для сессии нужно для функции очистки старых неактивных сессий
		$db_link->prepare("UPDATE `sessions` SET `last_activiti_time` = ? WHERE `session` = ? AND `user_id` = ?;")->execute( array(time(), $_COOKIE["admin_session"], DP_User::getAdminId()) );
	}
}




//ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ
//Рекурсивная функция получения линейного списка групп для бэкэнда
function getBackendGroups($parent_group_id, $backend_groups_list)
{
    global $db_link;
    global $DP_Config;
    
    //Первый вызов метода - получаем верхнюю группу бэкэнда
    if($parent_group_id == NULL)
    {
		$group_for_backend_query = $db_link->prepare("SELECT * FROM `groups` WHERE `for_backend` = 1;");
		$group_for_backend_query->execute();
        $group_for_backend_record = $group_for_backend_query->fetch();
        array_push($backend_groups_list, $group_for_backend_record["id"]);//Добавляем основную группу для бэкэнда
        
        if($group_for_backend_record["count"] == 0)
        {
            return $backend_groups_list;
        }
        else
        {
            return getBackendGroups($group_for_backend_record["id"], $backend_groups_list);//Рекурсивный вызов для вложенных
        }
    }
    else//Был рекурсивный вызов - добавляем влоеженные группы
    {
		$groups_query = $db_link->prepare("SELECT * FROM `groups` WHERE `parent` = ?;");
		$groups_query->execute( array($parent_group_id) );
        while(  $group_record = $groups_query->fetch() )
        {
            array_push($backend_groups_list, $group_record["id"]);//Добавляем вложенную группу
            
            if($group_record["count"] > 0)
            {
                $backend_groups_list = getBackendGroups($group_record["id"], $backend_groups_list);//Рекурсивный вызов для вложенных
            }
        }
    }
    
    return $backend_groups_list;//Возвращаем рекурсивно заполненный список групп
}
?>