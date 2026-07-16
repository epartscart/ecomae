<?php
/**
 * Скрипт для очередного шага общего алгоритма загрузки прайс-листа "Завершение работы"
*/
header('Content-Type: application/json;charset=utf-8;');



//Конфигурация Treelax
require_once __DIR__ . '/epc_prices_ajax_init.php';

// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


if($DP_Config->tech_key !== $_GET['key']){
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





//Получаем конфигурацию прайс-листа
$price_id = $_GET["price_id"];




if( $db_link->prepare("UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?;")->execute( array(time(), $price_id) ) != true)
{
    $answer = array();
    $answer["result"] = 0;
    $answer["message"] = translate_str_by_id(3688);
    exit(json_encode($answer));
}





$answer = array();
$answer["result"] = 1;
exit(json_encode($answer));
?>