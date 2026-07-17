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

// Keep denormalized QTY in sync so the CP listing never shows 0 for stocked lists.
try {
	$cntQ = $db_link->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
	$cntQ->execute(array((int) $price_id));
	$rowsInList = (int) $cntQ->fetchColumn();
	$db_link->prepare('UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?')
		->execute(array($rowsInList, (int) $price_id));
} catch (Throwable $e) {
	// Column may be absent on some tenants — listing has a live-count fallback.
}

// New/updated supplier stock must re-enter Google sitemaps after warm.
$whSeo = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_sitemap_warehouse.php';
if (is_file($whSeo)) {
	require_once $whSeo;
	if (function_exists('epc_sitemap_warehouse_mark_stale')) {
		epc_sitemap_warehouse_mark_stale('price_upload_complete', (int) $price_id);
	}
}

$answer = array();
$answer["result"] = 1;
exit(json_encode($answer));
?>