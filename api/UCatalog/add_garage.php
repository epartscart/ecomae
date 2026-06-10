<?php
// Добавление автомобиля в гараж
defined('_UCatalog_') or die('No access');



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
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();



// Проверяем наличе соответствующего поля в базе данных
$flag = false;
$query = $db_link->prepare('SHOW COLUMNS FROM `shop_docpart_garage`;');
$query->execute();
while($record = $query->fetch())
{
	if($record['Field'] === 'UCatalog_json'){
		$flag = true;
		break;
	}
}

// Если поля нет тогда создаем
if($flag === false){
	$query = $db_link->prepare("ALTER TABLE `shop_docpart_garage` ADD `UCatalog_json` TEXT CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL DEFAULT '' COMMENT 'JSON-запись привязки к каталогу UCatalog' AFTER `car_tree_list_json`;");
	$query->execute();
}

// Добавляем автомобиль в гараж
$query = $db_link->prepare("SELECT `id` FROM `shop_docpart_garage` WHERE `UCatalog_json` != '' AND `caption` = ? AND `user_id` = ?;");
$query->execute(array($request_object['request_object']['caption'], $user_id));
$record = $query->fetch();
if(empty($record['id'])){
	$request_object['request_object']['breadcrumbs'] = $request_object['request_object']['breadcrumbs'];
	$query = $db_link->prepare("INSERT INTO `shop_docpart_garage` (`caption`,`UCatalog_json`,`user_id`) VALUES (?,?,?);");
	$query->execute(array($request_object['request_object']['caption'], json_encode($request_object['request_object']), $user_id));
	$id = $db_link->lastInsertId();
}

$answer["status"] = true;
$answer["html"] = '<span class="btn_primary"><i class="fa fa-check" aria-hidden="true"></i> '.translate_str_by_id(2062).'</span>';
$answer["tag"] = 'UCatalog_breadcrumbs_right_btn';
?>