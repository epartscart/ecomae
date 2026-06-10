<?php
set_time_limit(600);
header('Content-Type: application/json;charset=utf-8;');
//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $result["status"] = false;
	$result["message"] = "DB connect error";
	$result["code"] = 502;
	exit(json_encode($result));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


$sql = "SET SESSION SQL_BIG_SELECTS = 1";
$query = $db_link->prepare($sql);
$query->execute();

//Проверяем право менеджера
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
if( ! DP_User::isAdmin())
{
	$answer = array('status'=>false);
	exit(json_encode($answer));
}

/*
$f = fopen('log.txt', 'w');
fwrite($f, $_POST['request_object']);
*/
/*
$_POST['request_object'] = '{"group_id_my_list_emails":"1","offices":"2","arr_storages":[17],"arr_category":[86,120,116,113,112,109,108,100,99,87,90,91,97,121,110,107,106,104,98,103,114,119,117,115,62,63,64,65,66,80,122,81,83,82,84,85,74,111,105,101,73,78,102],"action":"create_prices"}';
*/

require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/prices_send/prices_send_helper.php");

$answer = array('status'=>false);
$request_object = json_decode($_POST['request_object'], true);

switch($request_object['action'])
{
	case 'send_prices':
		
		$send_result = true;
		
		//Почтовый обработчик
		//require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer_distribution.php");
		require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer.php");
		
		$subject = translate_str_by_key($DP_Config->site_name)." ".translate_str_by_key('3660');
		$body = "<p>".translate_str_by_key('3660')." ".translate_str_by_key($DP_Config->site_name)." ".translate_str_by_key('1711373666_1_5f735d1486aa51eb9a61df1cd635a0fb')." ".date('d-m-Y', time())."</p>";
		
		//$body .= '<p>Отказаться от рассылке можно в <a href="http://yamato.kg/users/editform" target="_blank">личном кабинете</a></p>';
		
		$new_name_file = "prices_".date("d_m_Y", time()).".csv";//Имя файла, которое будет указано в письме
		
		
		
		$users_list = $request_object['users_list'];
		$emails_list = explode(',', $request_object['emails_list']);
		$group_id_my_list_emails = (int)$request_object['group_id_my_list_emails'];
		
		if(is_array($users_list) && !empty($users_list))
		{
			foreach($users_list as $user)
			{
				$sql = "SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? LIMIT 1;";
				$query = $db_link->prepare($sql);
				$query->execute( array($user) );
				$rov = $query->fetch();
				$group_id = $rov['group_id'];
				
				$sql = "SELECT `user_id`, `email` AS `email` FROM `users` WHERE `user_id` = ?";
				
				$query = $db_link->prepare($sql);
				
				$query->execute( array($user) );
				while($rov = $query->fetch() )
				{
					$user_id = (int)$rov['user_id'];
					$email = trim($rov['email']);
					
					if(!empty($group_id) && !empty($email))
					{
						$file = $_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/prices_$group_id.csv";
						
						if(file_exists($file))
						{
							$docpartMailer = new DocpartMailer();//Объект обработчика
							$docpartMailer->Subject = $subject;//Тема письма
							$docpartMailer->Body = $body;//Текст письма
							$docpartMailer->CharSet="UTF-8";
							$docpartMailer->addAddress($email, $email);// Добавляем адрес в список получателей

							$docpartMailer->addAttachment($file, $new_name_file);// файл
							
							$docpartMailer->IsSMTP();
							$docpartMailer->IsHTML(true);
							if(!$docpartMailer->Send())
							{
								//Обработать ошибку отправки
								$send_result = false;
							}
						}
					}
				}
			}
		}
		
		
		if(!empty($emails_list))
		{
			foreach($emails_list as $email)
			{
				$email = trim($email);
				$group_id = $group_id_my_list_emails;
				
				if(!empty($group_id) && !empty($email))
				{
					$file = $_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/prices_$group_id.csv";
					
					if(file_exists($file))
					{
						$docpartMailer = new DocpartMailer();//Объект обработчика
						$docpartMailer->Subject = $subject;//Тема письма
						$docpartMailer->Body = $body;//Текст письма
						$docpartMailer->CharSet="UTF-8";
						$docpartMailer->addAddress($email, $email);// Добавляем адрес в список получателей

						$docpartMailer->addAttachment($file, $new_name_file);// файл
						
						$docpartMailer->IsSMTP();
						$docpartMailer->IsHTML(true);
						if(!$docpartMailer->Send())
						{
							//Обработать ошибку отправки
							$send_result = false;
						}
					}
				}
			}
		}
		
		if($send_result)
		{
			$answer = array('status'=>true);
		}
		else
		{
			$answer = array('status'=>false);
		}
		
		break;
	case 'check_office_storages_map':
		$offices = (int)$request_object['offices'];
		$arr_storages = $request_object['arr_storages'];
		
		$storages_not_linked_str = "";
		
		foreach( $arr_storages AS $storage_id )
		{
			$check_office_storages_map_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?;");
			$check_office_storages_map_query->execute( array($offices, $storage_id) );
			if( $check_office_storages_map_query->fetchColumn() == 0 )
			{
				$storage_name_query = $db_link->prepare("SELECT `name` FROM `shop_storages` WHERE `id` = ?;");
				$storage_name_query->execute( array($storage_id) );
				$storage_name_record = $storage_name_query->fetch();
				
				if($storages_not_linked_str != "")
				{
					$storages_not_linked_str = $storages_not_linked_str.", ";
				}
				
				$storages_not_linked_str = $storages_not_linked_str.$storage_name_record["name"];
			}
		}
		
		if($storages_not_linked_str == "")
		{
			$answer = array('status'=>true);
		}
		else
		{
			$answer = array('status'=>false, "message"=>$storages_not_linked_str);
		}
		
		break;
	case 'create_prices':
		
		//Используется общая функция для генерации прайс-листов. Для mailing_price по cron
		$check_result = generate_price($request_object);

		//Если не было ошибок
		if( $check_result )
		{
			$answer['status'] = true;
		}

		break;
}
exit(json_encode($answer));
?>