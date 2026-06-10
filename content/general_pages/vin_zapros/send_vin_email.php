<?php

	ini_set('error_reporting', ~E_ALL);
	ini_set('display_errors', 0);
	ini_set('display_startup_errors', 0);

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

	// -------------------------------------------------------------------------------
	// // Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------

	//Для отправки уведомлений
	require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );

	$result = array(
		'status' => true,
		'message' => '',
		'inputs' => array()
	);

	try
    {
		// Проверка регулярных выражений
		// Получаем данные полей из формы 
		$dataForm = $_POST;
		
		//1. CAPTCHA
		//Проверям правильность ввода captcha, чтобы исключить вероятность обращения напрямую
		//Получаем значение от пользователя и сразу переводим его в md5:
		$user_captcha = md5($_POST['capcha_input']);
		//Правильная captcha из Куки, которая уже в md5:
		$cookie_captcha = $_COOKIE["captcha"];

		if($user_captcha != $cookie_captcha)
		{
			throw new Exception(translate_str_by_id(4041));
		}

		// Получаем картинки
		$client_img = $_FILES['client_img'];
		$data_img = array();
		if(isset($client_img['name'])) {
			foreach($client_img['name'] as $client_img_key => $client_img_name) {
				if (!empty($client_img_name)) {
					// Собираем картинки все в один массив
					$data_img[$client_img_key]['name'] = $client_img_name;
					$data_img[$client_img_key]['tmp'] = $client_img['tmp_name'][$client_img_key];
					$data_img[$client_img_key]['size'] = $client_img['size'][$client_img_key];
					$data_img[$client_img_key]['type'] = $client_img['type'][$client_img_key];
				}
			}
		}

		// Получаем данные о "regexp" из БД по "name"
		$regexp_query =  $db_link->prepare("SELECT `regexp`, `caption`, `required`, `name` FROM `vin_fields` WHERE `name` = ?;");

		// Создание массива для хранения всех ошибок
		$error_form_message = '';

		// Сравниваем данные из БД с данными из POST
		foreach ($dataForm as $key => $value) { 

			$regexp_query->execute(array($key));
			$fetchQuery = $regexp_query->fetch();

			$regexp = $fetchQuery['regexp'];
			$inputName = translate_str_by_id($fetchQuery['caption']); 
			$input = $fetchQuery['name']; 
			$requiredInput = $fetchQuery['required']; 

			if(($requiredInput == 1) || ($requiredInput == 0 && $value != '' )) {

				if (!preg_match("/".$regexp."/", $value, $matches)) {
					// Имеются ошибки, запрещаем отправку mail 
					$error_form_message = translate_str_by_id(3885).' "'.$inputName.'" '.translate_str_by_id(5600).'. ';
					$result['inputs'] = $input;
				}
			}	
		}
		
		// Добавляем переменные для облегчения сравнивания данных картинки
		$total_file_size = 15728640;
		$total_size = 0;
		$available_format = ['image/png', 'image/jpeg', 'image/jpg','image/bmp'];

		// Проверяем картинки на параметры (размер, расширение)
		if (!empty($data_img)) {
			foreach ($data_img as $types) {

				// Если формат не тот
				if (!in_array($types['type'], $available_format)) {
					throw new Exception(translate_str_by_id(4575));
				}
				// Если одна картинк больше 5мб
				if (!in_array($types['size']) > 5242880) {
					throw new Exception(translate_str_by_id(4576));
				}
				// Если все фото превышают 15мб
				$total_size += $types['size'];
				if ($total_size > $total_file_size) {
					throw new Exception(translate_str_by_id(4577));
				}	
			} 
		}

		if(!empty($result['inputs'])) {
			throw new Exception($error_form_message);
		}
		
		// Формируем eMail и отправляем на почту
		$send_result = true;//Накопительный результат отправки

		//Получаем список менеджеров (кому отправлять)
		$managers_list = array();//Список для контроля уникальности получателей
		$persons = array();
		$managers_query = $db_link->prepare('SELECT `users` FROM `shop_offices`;');
		$managers_query->execute();
		while( $managers = $managers_query->fetch() )
		{
			$managers = json_decode($managers["users"], true);
			for($i=0; $i < count($managers); $i++)
			{
				if( array_search((integer)$managers[$i], $managers_list) === false )
				{
					array_push($managers_list, (integer)$managers[$i]);
					
					$persons[] = array('type'=>'user_id', 'user_id' =>(integer)$managers[$i]);
				}
			}
		}

		// Получаем поля из базы данных только те которые видны и доступны для ввода (`show` = '1')
		$getFields =  $db_link->prepare("SELECT `name`, `caption`, `required` FROM `vin_fields` WHERE `show` = '1'");
		$getFields->execute();

		//Формируем текст письма:
		$textMail = '';
		$textMail.= '<table class="table" style="width:100%;border-spacing: 0px;">';
		$textMail.= '<thead style="text-align:left;"><tr><th>'.translate_str_by_id(3893).'</th><th>'.translate_str_by_id(5601).'</th></tr></thead>';
		$textMail.= "<tbody>";

		while( $field = $getFields->fetch() ) {
			$field_name = $field['name'];
			$textMail.= "<tr><td>".translate_str_by_id($field['caption'])."</td><td>".$dataForm[$field_name]."</td></tr>";
		}

		$textMail.= "<tr><td>".translate_str_by_id(4043)."</td><td>".htmlentities($_POST["client_parts"])."</td></tr>";
		$textMail.= "</tbody>";
		$textMail.= '</table>';
		
		//Значение переменных для уведомления
		$notify_vars = array();
		$notify_vars['vin_zapros_text'] = $textMail;
		$notify_vars['files'] = $data_img;
		
		//Формируем массив файлов для отправки
		$files = array();
		if(!empty($data_img))
		{
			for($i=0; $i < count($data_img); $i++)
			{ 
				$files[] = array('url' => $data_img[$i]['tmp'], 'name' => basename($data_img[$i]['name']));
			}
		}
		
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
		$customer_uid = 0;
		try {
			$customer_uid = (int)DP_User::getUserId();
		} catch (Throwable $e) {
		}
		$persons = epc_staff_notify_persons($customer_uid, 0, $persons);
		$notify_result = send_notify('vin_zapros', $notify_vars, $persons, false, $files);
		if( $notify_result['status'] != true )
		{
			$send_result = false;
		}
		else
		{
			//Будем считать, что отправка успешна, если хотя бы один продавец получил письмо на E-mail
			$minimum_one_success = false;
			for( $i=0 ; $i<count($notify_result["persons"]) ; $i++)
			{
				if( $notify_result["persons"][$i]['contacts']['email']['status'] == true )
				{
					$minimum_one_success = true;
				}
			}
			if( !$minimum_one_success )
			{
				$send_result = false;
			}
		}
		
		//////////////////////////////////////////////////////////////////////////////
		
		//Записываем запрос в базу
		$SQL = "INSERT INTO `users_vin` (
		`id`, 
		`text`, 
		`user_id`,
		`time`,
		`viewed`,
		`viewed_customer`
		) VALUES ( ?, ?, ?, ?, ?, ? )";
		
		if( $db_link->prepare($SQL)->execute( array(
													NULL, 
													'',
													DP_User::getUserId(), 
													time(), 
													0, 
													1
													) ) == true )
		{
			//Получаем ID созданной записи
			$vin_id = $db_link->lastInsertId();
			
			//Сохраняем файлы
			if (!empty($data_img)) {
				$textMail .= '<div class="vin-file-box">';
					for($i=0; $i < count($data_img); $i++)
					{ 
						$uploaddir = $_SERVER["DOCUMENT_ROOT"]."/content/files/vin/";
						$new_name = 'vin_'.$vin_id.'_'.basename($data_img[$i]['name']);
						$new_name = str_replace(array(' ', '-'), '_', $new_name);
						$uploadfile = $uploaddir . $new_name;
						
						move_uploaded_file($data_img[$i]['tmp'], $uploadfile);//Сохраняем файл
						
						//Дополняем html запроса ссылками на прикрепленные файлы
						$textMail .= '
						<a class="vin-file-list-img" href="/content/files/vin/'.$new_name.'" rel="lightbox-vin-img">
							<div style="background-image: url(\'/content/files/vin/'.$new_name.'\');"></div>
						</a>
						';
					}
				$textMail .= '</div>';
			}
			
			//Сохраняем html
			$query = $db_link->prepare('UPDATE `users_vin` SET `text` = ? WHERE `id` = ?;');
			$query->execute(array($textMail, $vin_id));
			
			//Сохраняем сообщение клиента
			if(DP_User::getUserId() > 0){
				$query = $db_link->prepare('INSERT INTO `users_vin_messages`(`id`, `vin_id`, `is_customer`, `text`, `time`) VALUES (NULL, ?, ?, ?, ?);');
				$query->execute(array($vin_id, 1, trim(htmlentities($_POST["client_parts"])), time()));
			}
		}
		else
		{
			throw new Exception(translate_str_by_id(5602));	
		}
		
		//////////////////////////////////////////////////////////////////////////////

	}
	catch (Exception $e) {

		$result['status'] = false;
		$result['message'] = $e->getMessage();
	}

	exit(json_encode($result));

?>