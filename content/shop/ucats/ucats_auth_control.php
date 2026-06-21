<?php
//Скрипт для контроля доступа к каталогам Ucats

/*
Данный скрипт предназначен для ограничения нагрузки на сервер Ucats
Внимание! Удаление этого скрипта может привести к блокировке доступа к Ucats
*/


//Получаем адрес страницы
$page_url  = @( $_SERVER["HTTPS"] != 'on' ) ? 'http://'.$_SERVER["SERVER_NAME"] :  'https://'.$_SERVER["SERVER_NAME"];
$page_url .= ( $_SERVER["SERVER_PORT"] != 80 ) ? ":".$_SERVER["SERVER_PORT"] : "";
$page_url .= $_SERVER["REQUEST_URI"];


//Если  страница от Ucats
if( mb_stristr($page_url, "ucats") != false )
{
	//START - БЛОКИРУЕМ БОТОВ
	$client_ip = $_SERVER["REMOTE_ADDR"];
	$bots_ips_query = mysqli_query($db_link, "SELECT * FROM `bot_ips`");
	while( $bot_ips_range = mysqli_fetch_array($bots_ips_query) )
	{
		if( checkIP($client_ip, $bot_ips_range["from"], $bot_ips_range["to"]) )
		{
			exit('Forbidden');
		}
	}
	//END - БЛОКИРУЕМ БОТОВ
	
	$user_id_ucats = 0;//ID пользователя (Пока не учитываем)
	
	
	//Проверяем наличие запросов от данного IP за последние сутки
	$check_query = $db_link->prepare('SELECT * FROM `shop_ucats_auth_control` WHERE `ip` = ? AND `time` > ?-86400');
	$check_query->execute( array($_SERVER["REMOTE_ADDR"], time()) );
	$check_query_record = $check_query->fetch();
	
	if( $check_query_record != false)//Запросы от него уже были сегодня
	{
		
		
		if($check_query_record["queries_count"] > 100)//Количество превышено - блокируем
		{
			exit('Forbidden');
		}
		else//Количество не превышено - прибавляем счетчик
		{
			$db_link->prepare('UPDATE `shop_ucats_auth_control` SET `queries_count` = `queries_count`+1 WHERE `id` = ?')->execute( array($check_query_record["id"]) );
		}
		
	}
	else//Запрос от данного IP за последние сутки еще не было - добавляем запись
	{
		$db_link->prepare('INSERT INTO `shop_ucats_auth_control` (`time`, `ip`, `user_id`, `queries_count`) VALUES (?, ?, ?, ?);')->execute( array(time(), $_SERVER["REMOTE_ADDR"], $user_id_ucats, 1) );
	}
}





//Функция проверки вхождения IP-адреса в диапазон
function checkIP ($user_ip, $ip_begin, $ip_end) 
{
	return (ip2long($user_ip)>=ip2long($ip_begin) && ip2long($user_ip)<=ip2long($ip_end));
}
?>