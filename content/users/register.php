<?php
/**
 * Страничный и технический скрип
 * 
 * Сюда пользователя попадает сразу после формы регистрации
 * 
 * Действия:
 * 1. Создание учетной записи пользователя
 * 2. Создание профиля пользователя
 * 3. Привязка пользователя к группе регистрации
 * 4. Отправка ссылки активации
 * 5. Вывод страницы с сообщением
*/
defined('_ASTEXE_') or die('No access');

// GET /users/register is the POST target only — show the form on direct visits.
if (empty($_POST) && strtoupper((string) ($_SERVER['REQUEST_METHOD'] ?? 'GET')) !== 'POST') {
	$lang = '/en';
	if (!empty($multilang_params['lang_href']) && is_string($multilang_params['lang_href'])) {
		$lang = rtrim((string) $multilang_params['lang_href'], '/');
	}
	header('Location: ' . $lang . '/users/registration', true, 302);
	exit;
}

//Класс пользователя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


//Для отправки уведомлений
require_once($_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php");

$registration_confirm_email_failed = false;

//Все делаем через транзакцию
try
{
	// -------------------------------------------------------------------------------------------
	
	//Проверка, что пользователь не авторизован
	if(DP_User::getUserId() != 0)
	{
		throw new Exception(translate_str_by_id(4740));
	}
	
	// -------------------------------------------------------------------------------------------
	
	//Старт транзакции
	if( ! $db_link->beginTransaction()  )
	{
		throw new Exception(translate_str_by_id(2132));
	}
	
	// -------------------------------------------------------------------------------------------
	
	//1. CAPTCHA
	if( md5( $_POST['capcha_input'] ) != $_COOKIE["captcha"] && !isset($_POST["simple_register"]))
	{
		throw new Exception(translate_str_by_id(4041));
	}
	
	// -------------------------------------------------------------------------------------------
	
	//2. Пользовательское соглашение
	if( $_COOKIE["users_agreement"] != "yes"  && !isset($_POST["simple_register"]))
	{
		throw new Exception(translate_str_by_id(4745));
	}
	
	// -------------------------------------------------------------------------------------------

	//3. ПРОВЕРКА reg_contact (уникальность и корректность)
	//Входные данные
	$_POST["reg_contact"] = trim($_POST["reg_contact"]);
	$reg_contact = $_POST["reg_contact"];
	$reg_contact_type = $_POST["reg_contact_type"];
	//Имя колонки, в которой ищем контакт:
	$col_name = 'email';//По-умолчанию - email
	if( $reg_contact_type == 'phone' )
	{
		$col_name = 'phone';//Будем искать в колонке "Телефон"
	}
	else if( $reg_contact_type != 'email' )
	{
		//Значит было передано некорректное значение reg_contact_type
		throw new Exception(translate_str_by_id(2122)." 1.1");
	}
	//Проверяем корректность контакта
	//Получаем регулярное выражение для контакта
	$regexp_query = $db_link->prepare("SELECT `regexp` FROM `reg_fields` WHERE `name` = ?;");
	$regexp_query->execute( array($reg_contact_type) );
	$regexp = $regexp_query->fetchColumn();
	preg_match("/".$regexp."/", $reg_contact, $matches);
	$regexp_ok = true;
	if($regexp != '') {
		if( count($matches) == 1 )
		{
			if( $matches[0] != $reg_contact )
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
		throw new Exception(translate_str_by_id(2122)." 1.2");
	}
	//Проверяем уникальность контакта
	$contact_check_query = $db_link->prepare('SELECT COUNT(*) FROM `users` WHERE `'.$col_name.'`= ?;');//У col_name - безопасное значение
	$contact_check_query->execute( array(htmlentities($reg_contact)) );
	$contact_count_rows = $contact_check_query->fetchColumn();
	if( $contact_count_rows != 0)
	{
        if (isset($_POST["simple_register"]))
        {
            ?>
            <form id="formAuthenticate" action="/" method="post">
                <input type="hidden" name="authentication" value="true">
                <input type="hidden" name="auth_contact" value="<?php echo $_POST["reg_contact"]; ?>">
                <input type="hidden" name="auth_contact_type" value="<?php echo $_POST["reg_contact_type"]; ?>">
                <input type="hidden" name="code" value="<?php echo $_POST["code"]; ?>">
                <input type="hidden" name="csrf_guard_key" value="<?php echo $_POST["csrf_guard_key"]; ?>">
            </form>
            <script>
                document.querySelector('#formAuthenticate').submit();
            </script>
            <?php
			exit();//Останавливаем работу скрипта так как произошел переход на страницу авторизации, что бы не увеличивался счетчик ID пользователей в базе
        }
        else
		{
		    throw new Exception(translate_str_by_id(2122)." 1.3");
		}
	}

	// -------------------------------------------------------------------------------------------

	//4. Проверка IP-адреса - предотвращения регистраций роботами и хулиганами - блокируем ну сутки
	$ip = $_SERVER["REMOTE_ADDR"];
	if($ip == "" || $ip == NULL)
	{
		throw new Exception(translate_str_by_id(2122)." 2.1");
	}
	$time_day = time() - 86400;//Сутки назад

	$ip_query = $db_link->prepare('SELECT COUNT(*) FROM `users` WHERE `ip_address` = ? AND `time_registered` > ? AND `email_confirmed` = ? AND `phone_confirmed` = ?;');
	$ip_query->execute( array($ip, $time_day, 0, 0) );
	if( $ip_query->fetchColumn() > 0 )
	{
		throw new Exception(translate_str_by_id(4746));
	}
	// -------------------------------------------------------------------------------------------

	$epc_reg_enhanced_path = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_enhanced.php';
	if (is_readable($epc_reg_enhanced_path)) {
		require_once $epc_reg_enhanced_path;
		$epc_pre_reg_type = isset($_POST['epc_customer_type']) ? (string)$_POST['epc_customer_type'] : 'retail';
		epc_reg_validate_trade_uae($_POST, $epc_pre_reg_type);
		epc_reg_validate_uae_fields($_POST);
	}

	//5. ДОБАВЛЕНИЕ ЗАПИСИ В ТАБЛИЦУ users
	if( $reg_contact_type == "email" )
	{
		$activation_code = md5(md5($reg_contact).md5($DP_Config->secret_succession));//Код активации
	}
	else
	{
		$activation_code = rand(100000,999999);
	}

	//Проверка подмены reg_variant
	$check_reg_variant = $db_link->prepare("SELECT COUNT(*) FROM `reg_variants` WHERE `id` = ?;");
	$check_reg_variant->execute( array($_POST["reg_variant"]) );
	if($check_reg_variant->fetchColumn() != 1)
	{
		throw new Exception(translate_str_by_id(2122)." 5.1");
	}

	if (!isset($_POST["simple_register"]))
    {
        if( $db_link->prepare('INSERT INTO `users` (`'.$col_name.'`, `reg_variant`, `password`, `'.$col_name.'_code`, `time_registered`, `'.$col_name.'_code_expired`, `unlocked`) VALUES (?, ?, ?, ?, ?, ?, ?);')->execute( array(htmlentities($reg_contact), $_POST["reg_variant"], md5($_POST['password'].$DP_Config->secret_succession), $activation_code, time(), time()+1800, 1 ) ) != true)
        {
            throw new Exception(translate_str_by_id(3912));
        }
        else//Запись добавлена - узнаем user_id добавленного пользователя
        {
            $user_id = $db_link->lastInsertId();
        }
    }
	else
    {
        require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
		$user_session = DP_User::getUserSession();
		$session_data = json_decode($user_session["data"], true);
		$faCode = $user_session["2fa_code"];
		$faAttempts = (int) $user_session["2fa_attempts"];
		if($faAttempts < 1)
		{
			$error_message = translate_str_by_id(4003);
			throw new Exception($error_message);
		}
		else if($faCode === $_POST["code"])
		{
			if($session_data["expireFaCode"] < time())
			{
				$error_message = translate_str_by_id(5642);
				throw new Exception($error_message);
			}
			else
			{
				//OK
				
				$_POST['password'] = md5(rand(1000, 1000000000).$DP_Config->secret_succession);//Так как регистрация была по коду то генерируем пароль автоматически
				
				if( $db_link->prepare('INSERT INTO `users` (`'.$col_name.'`, `reg_variant`, `password`, `'.$col_name.'_confirmed`, `time_registered`, `unlocked`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array(htmlentities($reg_contact), $_POST["reg_variant"], md5($_POST['password'].$DP_Config->secret_succession), 1, time(), 1 ) ) != true)
				{
					throw new Exception(translate_str_by_id(3912));
				}
				else//Запись добавлена - узнаем user_id добавленного пользователя
				{
					$user_id = $db_link->lastInsertId();
				}
			}
		}
		else
		{
			$faAttempts = $faAttempts - 1;
			$sqlUpdateAttempt = $db_link->prepare("UPDATE `sessions` SET `2fa_attempts` = ? WHERE `session` = ?;");
			$sqlUpdateAttempt->execute([$faAttempts, $user_session["session"]]);
			
			$error_message = translate_str_by_id(5643).": " . $faAttempts . ".";
			throw new Exception($error_message);
		}
    }
	
	// -------------------------------------------------------------------------------------------

    if (!isset($_POST["simple_register"])) {
        //6. ДОБАВЛЕНИЕ ЗАПИСЕЙ В ТАБЛИЦУ users_profiles
        //Получаем дополнительные регистрационные поля
        $reg_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag` = 0;');
        $reg_fields_query->execute();
        $epc_reg_customer_type_early = isset($_POST['epc_customer_type']) ? strtolower(trim((string) $_POST['epc_customer_type'])) : 'retail';
        $epc_enhanced_active = is_readable($_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_enhanced.php');
        while ($reg_field_record = $reg_fields_query->fetch()) {
            $show_for = json_decode($reg_field_record["show_for"], true);
            if (!is_array($show_for)) {
                $show_for = array();
            }

            //Есть ли данное поле в этом Регистрационном Варианте показано
            if (array_search($_POST["reg_variant"], $show_for) === false && array_search((int) $_POST["reg_variant"], $show_for) === false) {
                continue;
            }

            $field_name = (string) $reg_field_record["name"];
            $widget = (string) ($reg_field_record["widget_type"] ?? 'text');
            $category = (string) ($reg_field_record["field_category"] ?? '');

            // File uploads are stored later via epc_reg_store_kyc_upload (wholesale only).
            if ($widget === 'file') {
                continue;
            }

            // Enhanced registration: compliance / KYC / business "additional information" is wholesale-only.
            if ($epc_enhanced_active && $epc_reg_customer_type_early !== 'wholesale') {
                if (in_array($category, array('business', 'einvoice', 'kyc_aml', 'documents', 'identity'), true)
                    || strpos($field_name, 'epc_') === 0
                    || $field_name === 'company_name'
                    || $field_name === 'patronymic'
                ) {
                    continue;
                }
            }

            $raw_val = isset($_POST[$field_name]) ? (string) $_POST[$field_name] : '';
            if ($db_link->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?);')->execute(array(
                $user_id,
                $field_name,
                htmlentities($raw_val),
            )) != true) {
                throw new Exception(translate_str_by_id(3913));
            }
        }
    }
    else
    {
        //6. ДОБАВЛЕНИЕ ЗАПИСЕЙ В ТАБЛИЦУ users_profiles
        //Получаем дополнительные регистрационные поля
        $reg_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag` = 0;');
        $reg_fields_query->execute();
        while ($reg_field_record = $reg_fields_query->fetch()) {
            $show_for = json_decode($reg_field_record["show_for"], true);

            //Есть ли данное поле в этом Регистрационном Варианте показано
            if (array_search($_POST["reg_variant"], $show_for) !== false) {
                if ($db_link->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?);')->execute(array($user_id,
                        $reg_field_record["name"], translate_str_by_key($reg_field_record["caption"]) )) != true) {
                    throw new Exception(translate_str_by_id(3913));
                }
            }
        }
    }
	// -------------------------------------------------------------------------------------------


	//7. ПРИВЯЗКА ПОЛЬЗОВАТЕЛЯ К ГРУППЕ РЕГИСТРАЦИИ
	$for_registrated_group_query = $db_link->prepare('SELECT * FROM `groups` WHERE `for_registrated` = 1;');
	$for_registrated_group_query->execute();
	$for_registrated_group_record = $for_registrated_group_query->fetch();
	if( $db_link->prepare('INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?);')->execute( array($user_id, $for_registrated_group_record["id"]) ) != true)
	{
		throw new Exception(translate_str_by_id(4747));
	}

	// -------------------------------------------------------------------------------------------

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
	$epc_reg_customer_type = isset($_POST['epc_customer_type']) ? (string)$_POST['epc_customer_type'] : 'retail';
	epc_trade_save_registration($db_link, (int)$user_id, $epc_reg_customer_type);

	$epc_reg_enhanced_path = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_enhanced.php';
	if (is_readable($epc_reg_enhanced_path)) {
		require_once $epc_reg_enhanced_path;
		epc_reg_save_uae_buyer_profile($db_link, (int)$user_id, $_POST, $reg_contact, $reg_contact_type);
	}

	// -------------------------------------------------------------------------------------------
	
	//8. ОТПРАВКА ССЫЛКИ/КОДА АКТИВАЦИИ КЛИЕНТУ

	if( $reg_contact_type == "email" )
	{
		$notify_name = 'reg_email_confirm';//Тип уведомления - "Подтверждение e-mail"
		
		//Массив получателей в соответствии с API скрипта уведомлений
		$persons = array(
			array(
				'type'=>'direct_contact',
				'contacts'=>array(
						'email'=>array('value'=>$reg_contact),
						'phone'=>array('value'=>'')
					)
				) 
		);
		
		// Professional confirmation email (brand name — never raw multilang hash keys).
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_confirm.php';
		if (is_readable($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
		}
		if (is_readable($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';
		}
		epc_reg_confirm_ensure_notify_template($db_link);
		$epc_reg_brand = epc_reg_confirm_brand_name($DP_Config);
		$epc_reg_confirm_url = $DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/users/confirm_contact?code=$activation_code&u_id=$user_id&type=email";
		$epc_reg_home_url = rtrim((string) $DP_Config->domain_path, '/') . '/' . ltrim((string) ($multilang_params['lang_href'] ?? 'en/'), '/');

		$notify_vars = array();
		$notify_vars["site_name"] = $epc_reg_brand;
		$notify_vars["customer_email"] = $reg_contact;
		$notify_vars["email_confirm_href"] = epc_reg_confirm_button_html($epc_reg_confirm_url, 'Confirm my email');
		$notify_vars["confirm_html"] = epc_reg_confirm_email_body_html(array(
			'brand' => $epc_reg_brand,
			'email' => $reg_contact,
			'confirm_url' => $epc_reg_confirm_url,
			'customer_type' => isset($epc_reg_customer_type) ? (string) $epc_reg_customer_type : 'retail',
			'home_url' => $epc_reg_home_url,
		));
	}
	else
	{
		$notify_name = 'reg_phone_confirm';//Тип уведомления - "Подтверждение телефона"
		
		//Массив получателей в соответствии с API скрипта уведомлений
		$persons = array(
			array(
				'type'=>'direct_contact',
				'contacts'=>array(
						'email'=>array('value'=>''),
						'phone'=>array('value'=>$reg_contact)
					)
				) 
		);
		
		//Переменные для шаблонов уведомления
		$notify_vars = array();
		$notify_vars["phone_confirm_code"] = $activation_code;
	}

    if (!isset($_POST["simple_register"])) {
        $curl_result = send_notify($notify_name, $notify_vars, $persons, true);
        $contact_sent = false;
        if (!empty($curl_result["status"]) && $curl_result["status"] !== false) {
            if (!empty($curl_result["persons"][0]['contacts'][$reg_contact_type]['status'])) {
                $contact_sent = true;
            }
        }
        if (!$contact_sent) {
            $allow_skip = !empty($DP_Config->registration_continue_if_confirm_email_fails)
                && (string) $DP_Config->registration_continue_if_confirm_email_fails === '1'
                && $reg_contact_type === 'email';
            if ($allow_skip) {
                $registration_confirm_email_failed = true;
            } else {
                if (empty($curl_result["status"]) || $curl_result["status"] == false) {
                    throw new Exception(translate_str_by_id(4697));
                }
                throw new Exception(translate_str_by_id(4698));
            }
        }
    }
	
}
catch (Exception $e)
{
	//Откатываем все изменения
	$db_link->rollBack();
	
	//Текст ошибки
	$error_message = $e->getMessage();
	$err_base = (!empty($multilang_params) && !empty($multilang_params['lang_href'])) ? $multilang_params['lang_href'] : '';
	?>
	<script>
		location="<?php echo $err_base; ?>/?error_message=<?php echo urlencode($error_message); ?>";
	</script>
	<?php
	exit();
}

// -------------------------------------------------------------------------------------------

//Дошли до сюда, значит выполнено ОК
$db_link->commit();//Коммитим все изменения и закрываем транзакцию

// -------------------------------------------------------------------------------------------

//9. ОТПРАВКА УВЕДОМЛЕНИЯ МЕНЕДЖЕРУ

//Настройки шаблона
$templates = array();
$templates_query = $db_link->prepare('SELECT * FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1;');
$templates_query->execute();
$templates = $templates_query->fetch();
$templates = json_decode($templates['data_value'], true);

// Формируем таблицу с данными профиля пользователя
$userProfile = DP_User::getUserProfileById($user_id);//Профиль пользователя

$table_html = '<h4>'.translate_str_by_id(4748).'</h4>';
$table_html .= "<table cellspacing='0' style='border-collapse: collapse; margin-top: 15px;'>";

$reg_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC;');
$reg_fields_query->execute();
while( $reg_field_record = $reg_fields_query->fetch() )
{
	if(isset($userProfile[$reg_field_record["name"]])){
		$table_html .= '<tr><td>'.$reg_field_record["caption"].'</td><td>'.$userProfile[$reg_field_record["name"]].'</td></tr>';
	}
}

if(!empty($userProfile['email'])){
	$table_html .= '<tr><td>E-mail</td><td>'.$userProfile['email'].'</td></tr>';
}

if(!empty($userProfile['phone'])){
	$table_html .= '<tr><td>'.translate_str_by_id(1312).'</td><td>'.$userProfile['phone'].'</td></tr>';
}

$reg_group_query = $db_link->prepare('SELECT * FROM `groups`;');
$reg_group_query->execute();
while( $reg_group_record = $reg_group_query->fetch() )
{
	if($userProfile['groups'][0] == $reg_group_record["id"]){
		$table_html .= '<tr><td>'.translate_str_by_id(3664).'</td><td>'.$reg_group_record["value"].'</td></tr>';
	}
}

$table_html .= '</table>';

// Ссылка на профиль пользователя
$background_user_link = "#799658";
if(!empty($templates['main_color'])){
	$background_user_link = $templates['main_color'];
}
$user_link = "<a style=\"background: ".$background_user_link."; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;\" href='". $DP_Config->domain_path . $DP_Config->backend_dir ."/users/usermanager/user?user_id=". $user_id ."'>".translate_str_by_id(3539)."</a>";

$persons = array();//Массив получателей

$backend_group = array();

//Группа администраторов:
$parent_ids = '';
do{
	if($parent_ids == ''){
		$group_backend_query = $db_link->prepare('SELECT * FROM `groups` WHERE `for_backend` = 1;');
		$group_backend_query->execute();
	}else{
		$group_backend_query = $db_link->prepare('SELECT * FROM `groups` WHERE `parent` IN('.$parent_ids.');');
		$group_backend_query->execute();
		$parent_ids = '';
	}
	while($group_backend_record = $group_backend_query->fetch()) {
		$backend_group[] = $group_backend_record['id'];
		if($parent_ids != ''){
			$parent_ids .= ',';
		}
		$parent_ids .= $group_backend_record['id'];
	}
}while($parent_ids != '');

$backend_group_str = implode(",", $backend_group);

$SQL_ids_admins = "SELECT DISTINCT `user_id` FROM `users_groups_bind` WHERE `group_id` IN ($backend_group_str);";
$ids_admins_query = $db_link->prepare($SQL_ids_admins);
$ids_admins_query->execute();
while( $id_admin_rec = $ids_admins_query->fetch() )
{
	$persons[] = array( 'type'=>'user_id', 'user_id'=>$id_admin_rec["user_id"] );
}
//Массив с переменными для уведомления:
$notify_vars = array();
$notify_vars["user_id"] = $user_id;//ID зарегистрированного пользователя
$notify_vars["user_profile"] = $table_html;//Таблица с информацией о профиле зарегистрированного пользователя
$notify_vars["user_link"] = $user_link;//Кнопка на профиль зарегистрированного пользователя

$reg_contact_label = !empty($userProfile['email']) ? (string)$userProfile['email'] : (isset($reg_contact) ? (string)$reg_contact : '');
$epc_notify_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
if (is_readable($epc_notify_path)) {
	require_once $epc_notify_path;
	$notify_vars['event_html'] = epc_build_auth_event_html('New customer registration', (int)$user_id, $reg_contact_label, $table_html);
	$notify_vars['user_profile'] = $table_html . $notify_vars['event_html'];
	$notify_vars['user_profile'] .= '<p><strong>Requested account type:</strong> ' . htmlspecialchars(epc_trade_customer_type_label($epc_reg_customer_type), ENT_QUOTES, 'UTF-8') . '</p>';
	if ($epc_reg_customer_type === 'retail' && epc_trade_is_approved($db_link, (int)$user_id)) {
		$notify_vars['user_profile'] .= '<p><strong>Trade approval:</strong> Auto-approved (retail).</p>';
	} else {
		$notify_vars['user_profile'] .= '<p><strong>Trade approval:</strong> Pending — review in Control Panel → Users → Customer approvals.</p>';
	}
	if (!empty($_POST['epc_uae_company']) && (string)$_POST['epc_uae_company'] !== '0') {
		$notify_vars['user_profile'] .= '<p><strong>UAE e-invoice:</strong> Buyer TRN/profile captured at registration.</p>';
	}
	$persons = epc_staff_notify_persons((int)$user_id, 0, $persons);
}
send_notify('reg_notify_admin', $notify_vars, $persons, false);

// -------------------------------------------------------------------------------------------

if (!isset($_POST["simple_register"]))
{
    // Professional confirmation message for the customer after registration.
    if( $reg_contact_type == "email" )
    {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_confirm.php';
        if (is_readable($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php')) {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
        }
        if (is_readable($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php')) {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';
        }
        $epc_reg_brand = isset($epc_reg_brand) ? (string) $epc_reg_brand : epc_reg_confirm_brand_name($DP_Config);
        $confirm_url = $DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/users/confirm_contact?code=".urlencode($activation_code)."&u_id=".(int)$user_id."&type=email";
        $login_home = rtrim((string) ($multilang_params['lang_href'] ?? '/en/'), '/') . '/';
        echo epc_reg_confirm_frontend_html(array(
            'brand' => $epc_reg_brand,
            'email' => isset($reg_contact) ? (string) $reg_contact : '',
            'customer_type' => isset($epc_reg_customer_type) ? (string) $epc_reg_customer_type : 'retail',
            'email_failed' => !empty($registration_confirm_email_failed),
            'confirm_url' => $confirm_url,
            'login_url' => $login_home,
        ));
    }
    else
    {
        ?>
        <?php echo translate_str_by_id(4750); ?>:
        <form method="GET" action="/users/confirm_contact">
            <input type="hidden" name="u_id" value="<?php echo $user_id; ?>" />
            <input type="hidden" name="type" value="phone" />
            <div class="form-group">
                <label for="" class="col-sm-2 control-label"><?php echo translate_str_by_id(4708); ?></label>
                <div class="col-sm-6" style="padding:5px;">
                  <input type="text" class="form-control" name="code" id="code" value="" placeholder="<?php echo translate_str_by_id(4708); ?>">
                </div>
                <div class="col-sm-4" style="padding:5px;">
                    <button type="submit"><?php echo translate_str_by_id(4521); ?></button>
                </div>
            </div>
        </form>
        <?php
    }
}
else
{
    ?>
    <form id="formAuthenticate" action="/" method="post">
        <input type="hidden" name="authentication" value="true">
        <input type="hidden" name="auth_contact" value="<?php echo $_POST["reg_contact"]; ?>">
        <input type="hidden" name="auth_contact_type" value="<?php echo $_POST["reg_contact_type"]; ?>">
        <input type="hidden" name="code" value="<?php echo $_POST["code"]; ?>">
        <input type="hidden" name="csrf_guard_key" value="<?php echo $_POST["csrf_guard_key"]; ?>">
    </form>
    <script>
        document.querySelector('#formAuthenticate').submit();
    </script>
    <?php
}
?>