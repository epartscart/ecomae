<?php
//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS

// Подключение к БД
try {
    $db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
    $answer = array();
    $answer["result"] = false;
    exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");

// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------

// Подключение файла для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"] . "/content/users/dp_user.php");

// Получение ID пользователя
$user_id = DP_User::getUserId();


header("Location: ".$DP_Config->domain_path."shop/balans?success_message=".translate_str_by_id(2722));
?>
