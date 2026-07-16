<?php
/**
 * Скрипт для включения индексов в таблице прайс-листов. Этот скрипт необходимо выполнить, если импорт CSV не успел выполниться (timeout php)
*/
header('Content-Type: application/json;charset=utf-8;');

//Конфигурация Treelax
require_once __DIR__ . '/epc_prices_ajax_init.php';


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


if($DP_Config->tech_key !== $_GET['key'])
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------

	//Для работы с пользователями
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
	
	//Проверяем доступ в панель управления
	if( ! DP_User::isAdmin())
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
}






if( $db_link->prepare("ALTER TABLE `shop_docpart_prices_data` ENABLE KEYS;")->execute() != true)
{
    $answer = array();
    $answer["result"] = 0;
    $answer["message"] = translate_str_by_id(3112);
    exit(json_encode($answer));
}





$answer = array();
$answer["result"] = 1;
exit(json_encode($answer));
?>