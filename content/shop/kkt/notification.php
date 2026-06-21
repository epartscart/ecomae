<?php
//Этот скрипт вызывает касса после фактического создания чека
define('_DOCPART_KKT_', 1);


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
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


//Проверяем подключена ли касса к сайту
$kkt_query = $db_link->prepare("SELECT * FROM `shop_kkt_devices` WHERE `handler` != '';");
$kkt_query->execute();
$kkt_devices = $kkt_query->fetch();


//Проверка прав на запуск
if( !empty($kkt_devices['handler']) )
{
	$handler = $kkt_devices['handler'];
}
else
{
	exit;
}


require_once( $_SERVER['DOCUMENT_ROOT']."/".$DP_Config->backend_dir."/content/shop/kkt/handlers/".$handler."/notification.php" );
?>