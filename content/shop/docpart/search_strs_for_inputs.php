<?php
//Переменные для подстановки в input строк поиска. Скрипт подключается в desktop.php
defined('_ASTEXE_') or die('No access');

$value_for_input_search_string = "";//Значение, которое подставляется в input при загрузке страницы
$value_for_input_search = "";//Значение, которое подставляется в input при загрузке страницы
//Для артикула
if( isset($_GET["article"]) )
{
	$_GET["article"] = preg_replace("/[^A-Za-z0-9А-Яа-яёЁ]/ui", '', $_GET["article"]);//Очищаем артикул от лишних знаков
	$value_for_input_search = $_GET["article"];
}
else if( isset($DP_Content->service_data["article"]) )
{
	$value_for_input_search = preg_replace("/[^A-Za-z0-9А-Яа-яёЁ]/ui", '', $DP_Content->service_data["article"]);//Очищаем артикул от лишних знаков
}
//Для строки поиска по наименованию
if( isset($_GET["search_string"]) )
{
	$_GET["search_string"] = htmlentities($_GET["search_string"]);
	$value_for_input_search_string = $_GET["search_string"];
}
?>