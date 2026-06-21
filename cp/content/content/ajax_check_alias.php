<?php
header('Content-Type: application/json;charset=utf-8;');
//Скрипт проверки уникальности поля Алиас в рамках одного уровня одной ветви

//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;


if( $_GET["code"] != $DP_Config->secret_succession )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit( json_encode($answer) );
}


//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");




//Исходные данные
$alias = $_GET["alias"];
$content_id = $_GET["content_id"];
$parent = $_GET["parent"];
$is_frontend = $_GET["is_frontend"];


$check_query = $db_link->prepare("SELECT COUNT(*) FROM `content` WHERE `alias` = ? AND `parent` = ? AND `is_frontend` = ? AND `id` != ?;");
if( ! $check_query->execute( array($alias, $parent, $is_frontend, $content_id) ) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "SQL-error";
	exit( json_encode($answer) );
}
if( $check_query->fetchColumn() > 0 )
{
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "duplicated";
	$answer["result_code"] = "duplicated";
	exit( json_encode($answer) );
}
else
{
	$answer = array();
	$answer["status"] = true;
	$answer["message"] = "ok";
	$answer["result_code"] = "ok";
	exit( json_encode($answer) );
}
?>