<?php
//Серверный скрипт для создания csv для функции простановки цен
header('Content-Type: application/json;charset=utf-8;');
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
	$answer["message"] = 'No DB connect';
	exit(json_encode($answer));
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


// -------------------------------------------------------------------------------------------

//Проверка наличия необходимых полей
if( !isset( $_POST['price_id'] ) || !isset( $_POST['type'] ) )
{
	exit;
}

//Подстрока для статуса простановки цен
$reviewed_SQL = "";
if( $_POST['type'] == 1 )
{
	//Все позиции
}
else if( $_POST['type'] == 2 )
{
	//Только проставленные
	$reviewed_SQL = " AND `reviewed` = 1 ";
}
else if( $_POST['type'] == 3 )
{
	//Только НЕ проставленные
	$reviewed_SQL = " AND `reviewed` = 0 ";
}
else
{
	exit;
}

//ID прайс-листа
$price_id = (int)$_POST['price_id'];

//Тип (все, проставленные, не проставленные)
$type = (int)$_POST['type'];

//Запрос в БД
$price_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices_data` WHERE `price_id` = ? ".$reviewed_SQL.";");
$price_query->execute( array($price_id) );


$csv_name = $price_id."_".$type."_review.csv";//Имя файла
$csv_path_rel = "/".$DP_Config->backend_dir."/tmp/prices_upload_files/".$csv_name;//Относительный путь к файлу
$csv_path = $_SERVER["DOCUMENT_ROOT"].$csv_path_rel;//Полный путь к файлу

//Открываем файл на запись
$csv = @fopen( $csv_path , "w" );
if( ! $csv )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(3729);
	$answer["type"] = $type;
	exit(json_encode($answer));
}

//Заголовки для колонок
fwrite($csv, iconv('UTF-8', 'windows-1251', translate_str_by_id(2070).";".translate_str_by_id(2071).";".translate_str_by_id(2102).";".translate_str_by_id(2752).";".translate_str_by_id(2751).";".translate_str_by_id(3730)."")."\r\n");

//Пишем позиции в файл
while( $item = $price_query->fetch() )
{
	$yes_no = translate_str_by_id(2456);
	if( $item["reviewed"] == 0 )
	{
		$yes_no = translate_str_by_id(2457);
	}
	
	fwrite($csv, iconv('UTF-8', 'windows-1251', $item["manufacturer"].";".$item["article"].";".$item["name"].";".$item["exist"].";".$item["price"].";".$yes_no)."\r\n");
}
fclose($csv);


$answer = array();
$answer["status"] = true;
$answer["csv_path_rel"] = $csv_path_rel;
$answer["csv_name"] = $csv_name;
$answer["type"] = $type;
exit( json_encode($answer) );
?>