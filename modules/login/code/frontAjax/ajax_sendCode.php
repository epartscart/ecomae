<?php
	ini_set('display_errors', 0);

    require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");

    //Соединение с основной БД
    $DP_Config = new DP_Config;//Конфигурация CMS
    $epcPortalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
    if (is_file($epcPortalFile)) {
        require_once $epcPortalFile;
        if (function_exists('epc_portal_apply_config')) {
            epc_portal_apply_config($DP_Config);
        }
    }


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

    require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
    $user_session = DP_User::getUserSession();

    //Для отправки уведомлений
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php");


    $_POST = json_decode(file_get_contents("php://input"), true);


    // -------------------------------------------------------------------------------
    //Защита от CSRF-атак
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
    // -------------------------------------------------------------------------------


    $selectCodeExistResult = json_decode($user_session["data"], true);
    if (isset($selectCodeExistResult["timeSendFaCode"]))
	{
        if ((time() - $selectCodeExistResult["timeSendFaCode"]) < 30)
        {
            $time = (30 - (time() - $selectCodeExistResult["timeSendFaCode"]));
            exit(json_encode(["status" => 501, "message" => translate_str_by_id(5656)." " . $time . " ".translate_str_by_id(5647)]));
        }
	}
	
	
	$reg_contact = $_POST["contact"];
	$notify_name = 'verification_code';//Тип уведомления - "Код проверки"
	$verification_code = rand(100000,999999);


	if ($_POST["method"] == 'sms')
	{
		$reg_contact_type = "phone";
		
		//Массив получателей в соответствии с API скрипта уведомлений
		$persons = array(
			array(
				'type'=>'direct_contact',
				'contacts'=>array(
					'phone'=>array('value'=>$reg_contact)
				)
			)
		);
	}
	else if ($_POST["method"] == 'smtp')
	{
		$reg_contact_type = "email";
		
		//Массив получателей в соответствии с API скрипта уведомлений
		$persons = array(
			array(
				'type'=>'direct_contact',
				'contacts'=>array(
					'email'=>array('value'=>$reg_contact)
				)
			)
		);
	}
	else
	{
		exit(json_encode(["status" => 403, "message" => translate_str_by_id(5648)]));
	}
    
	
	//Проверка соответствия рег. выражению
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
        exit(json_encode(["status" => 501, "message" => translate_str_by_id(5649)]));
    }
	

    //Переменные для шаблонов уведомления
    $notify_vars = array();
    $notify_vars["verification_code"] = $verification_code;


    //Отправляем уведомление
    $curl_result = send_notify($notify_name, $notify_vars, $persons, true);
    if($curl_result["status"] == false)
    {
        //Причина - некорректные данные, нет прав на запуск и т.д.
        exit(json_encode(["status" => 501, "message" => translate_str_by_id(4697)]));
    }
    else
    {
        //Скрипт отправки выдал статус true, теперь НЕОБХОДИМО проверить статус отправки по конкретному контакту
        //Мы указывали единственный контакт, поэтому его и проверяем.
        if( ! $curl_result["persons"][0]['contacts'][$reg_contact_type]['status'] ) //Изменить условие на !
        {
            exit(json_encode(["status" => 501, "message" => translate_str_by_id(4698)]));
        }
        //Всё отработало корректно, сохраняем код проверки в поле 2fa по сессии в таблицу sessions
        else
        {
            $data = [
                "timeSendFaCode" => time(),
                "expireFaCode" => time() + 300,
                "type" => $_POST["type"],
                "method" => $_POST["method"],
                "contact_string" => $_POST["contact_string"],
                "contact" => $_POST["contact"]
            ];
            $update_code = $db_link->prepare("UPDATE `sessions` SET `2fa_code` = ?, `data` = ?, `2fa_attempts` = ? WHERE `session` = ?;");
            if ($update_code->execute([$verification_code, json_encode($data), 3, $user_session["session"]]) != true)
                exit(json_encode(["status" => 501, "message" => translate_str_by_id(5650)]));

            //Всё отработало корректно
        }
    }

    //Всё отработало корректно
    exit(json_encode(["status" => 200]));

?>