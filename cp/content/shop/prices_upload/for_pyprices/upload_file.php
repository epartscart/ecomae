<?php
//Скрипт загрузки файла с ПК для обновления прайс-листа
// -------------------------------------------------------------------------------
//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
// -------------------------------------------------------------------------------
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'No DB Connect';
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");
// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------
//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
// -------------------------------------------------------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('url'=>'shop/prices', 'is_frontend' => 0);
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
// -------------------------------------------------------------------------------
//Исходные данные:
$price_id = $_POST['price_id'];//ID прайс-листа, для которого загружаем файл
$file_input_name = "file_".$price_id;//Имя инпута с файлов
// -------------------------------------------------------------------------------
//Проверяем наличие прайс-листа
$check_price_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_docpart_prices` WHERE `id` = ?;");
$check_price_query->execute( array($price_id) );
if( $check_price_query->fetchColumn() != 1 )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'No such price';
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
//Допустимые типы файлов
$file_type_allowed = array('csv', 'txt', 'xls', 'xlsx', 'rar', 'zip', '7z', 'tar');
// -------------------------------------------------------------------------------
//Проверяем расширение
$file_format = explode('.', $_FILES[$file_input_name]['name']);
if( count($file_format) == 1 )
{
	//Не было расширения
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'File has no extension';
	exit(json_encode($answer));
}
$file_format = $file_format[ count($file_format) - 1 ];
if( array_search( strtolower($file_format) , $file_type_allowed ) === false )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'File has incompatible type';
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
//Проверяем наличие временного каталога для загрузки (который основной, не удаляемый). ПРИ НЕОБХОДИМОСТИ СОЗДАЕМ
$tmp_full_dir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir.$DP_Config->tmp_dir_prices_upload;//Путь к каталогу для загрузки файлов прайс-листов
if( !is_dir($tmp_full_dir) )
{
	//Если нет, пробуем создать
	if(!mkdir($tmp_full_dir))
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = 'Could not create tmp dir. Error 1';
		exit(json_encode($answer));
	}
}
// -------------------------------------------------------------------------------
//Еще проверка - если нет директории после попытки ее создать
if( !is_dir($tmp_full_dir) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Could not create tmp dir. Error 2';
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
//Генерим уникальное имя папки, так, чтобы файлы с одинаковыми именами не перетирались бы при загрузке.
$tmp_folder_name = 'price_manual_upload_'.$price_id."_".time().'_'.rand(1, 5000);
// -------------------------------------------------------------------------------
//Создаем временную папку с уникальным именем
if(!mkdir($tmp_full_dir."/".$tmp_folder_name))
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Could not create unique tmp dir';
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
//Загружаем файл
$uploaddir = $tmp_full_dir."/".$tmp_folder_name."/";
setlocale(LC_ALL,'ru_RU.UTF-8');
$uploadfile = $uploaddir . basename($_FILES[$file_input_name]['name']);
if (! move_uploaded_file($_FILES[$file_input_name]['tmp_name'], $uploadfile)) 
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Could upload the file';
	exit(json_encode($answer));
}
// -------------------------------------------------------------------------------
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_price_upload_history.php");
$price_name_q = $db_link->prepare("SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;");
$price_name_q->execute(array($price_id));
$price_name_row = (string)$price_name_q->fetchColumn();
$orig_upload_name = basename($_FILES[$file_input_name]['name']);
$stored_relpath = epc_price_history_archive_file($uploadfile, (int)$price_id, $orig_upload_name);
$history_id = epc_price_history_save($db_link, array(
	'price_id' => (int)$price_id,
	'price_name' => $price_name_row,
	'upload_source' => 'pyprices_upload',
	'source_ref' => $tmp_folder_name,
	'original_filename' => $orig_upload_name,
	'stored_relpath' => $stored_relpath,
	'file_size' => is_file($uploadfile) ? (int)filesize($uploadfile) : 0,
	'status' => 'pending',
));
// -------------------------------------------------------------------------------
//Загрузили файл, выдаем результат
$answer = array();
$answer['status'] = true;
$answer['price_id'] = $price_id;
$answer['upload_id'] = (int)$_POST['upload_id'];
$answer['file_dir'] = $uploadfile;//Полный путь к файлу от корня сайта (будет передан затем в pyprices)
$answer['tmp_folder_name'] = $tmp_folder_name;//Имя временной папки под этот файл (после выполнения задания модулем pyprices, ее нужно будет удалить)
$answer['history_id'] = $history_id;
exit(json_encode($answer));
// -------------------------------------------------------------------------------
?>