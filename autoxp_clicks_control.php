<?php
/**
 * Счетчик контроля каталога AutoXP
*/

require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");

$DP_Config = new DP_Config;//Конфигурация CMS
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


$month = date("n", time());//Текущий номер месяца
$year = date("Y", time());//Текущий номер месяца

//Проверяем его наличие в БД
$record_exist_query = $db_link->prepare('SELECT `id`, `clicks_count` FROM `shop_docpart_autoxp_clicks` WHERE `month` = ? AND `year` = ?;');
$record_exist_query->execute( array($month, $year) );
$record = $record_exist_query->fetch();
if( $record != false )//Запись есть
{
    if($record["clicks_count"] >= 2000)
    {
        exit(json_encode(0));
    }
    
    //Накручиваем счетчик и разрешаем пользоваться каталогом
	$db_link->prepare('UPDATE `shop_docpart_autoxp_clicks` SET `clicks_count` = `clicks_count`+1 WHERE `month` = ? AND `year` = ?;')->execute( array($month, $year) );
	
    exit(json_encode(1));
}
else//Записей в текущем месяце еще не было - создаем новую и разрешаем пользоваться каталогом
{	
	$db_link->prepare('INSERT INTO `shop_docpart_autoxp_clicks` (`month`, `year`, `clicks_count`) VALUES (?, ?, ?);')->execute( array($month, $year, 1) );
	
    exit(json_encode(1));
}
?>