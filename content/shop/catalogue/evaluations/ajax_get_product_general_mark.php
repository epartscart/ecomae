<?php
/*
Серверный скрипт для получения средней оценки товара
*/
header('Content-Type: application/json;charset=utf-8;');
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


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

$product_id = (int)$_POST["product_id"];

$mark_query = $db_link->prepare('SELECT COUNT(`id`) AS `marks_count`, ROUND(SUM(`mark`)/COUNT(`id`)) AS `general_mark`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = '.$product_id.' AND `mark`=1) AS `mark_1_count`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = '.$product_id.' AND `mark`=2) AS `mark_2_count`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = '.$product_id.' AND `mark`=3) AS `mark_3_count`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = '.$product_id.' AND `mark`=4) AS `mark_4_count`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = '.$product_id.' AND `mark`=5) AS `mark_5_count` FROM `shop_products_evaluations` WHERE `product_id` = '.$product_id.';');
$mark_query->execute();
$mark_record = $mark_query->fetch(PDO::FETCH_ASSOC);

$answer = array();
$answer["status"] = true;
$answer["marks_count"] = $mark_record["marks_count"];
$answer["general_mark"] = $mark_record["general_mark"];

$answer["mark_1_count"] = $mark_record["mark_1_count"];
$answer["mark_2_count"] = $mark_record["mark_2_count"];
$answer["mark_3_count"] = $mark_record["mark_3_count"];
$answer["mark_4_count"] = $mark_record["mark_4_count"];
$answer["mark_5_count"] = $mark_record["mark_5_count"];

exit(json_encode($answer));
?>