<?php
//Скрипт для получения JSON-объектов учетных записей пользователей для autocomplete-полей
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
	$answer['status'] = false;
	$answer['message'] = "No DB connect";
    exit( json_encode($answer) );
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


//Скрипт могут запускать только администраторы
if(! DP_User::isAdmin() )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit(json_encode($answer));//Вообще не является администратором бэкенда
}


//Далее можем работать
$input_str = "%".$_POST['input_str']."%";



//Формируем часть SQL-запроса для покупателя (отдельно, чтобы можно было использовать и для WHERE)
//Формируем подзапрос для значений профиля пользователя (только для тех полей, которые выводятся в таблицу пользователей в менеджер пользователей колонками)
$users_profile_SQL = "";
$users_profile_fields_query = $db_link->prepare("SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1;");
$users_profile_fields_query->execute();
while( $users_profile_field = $users_profile_fields_query->fetch() )
{
	if( $users_profile_SQL != "" )
	{
		$users_profile_SQL = $users_profile_SQL.",";
	}
	
	//Допустимы только буквы и знаки нижнего подчеркивания
	$field_name = str_replace( array(' ', '#', '-', "'", '"'), '', $users_profile_field["name"] );
	
	$users_profile_SQL = $users_profile_SQL." IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `users`.`user_id`), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `users`.`user_id`)),'') ";
}
if( $users_profile_SQL != "" )
{
	$users_profile_SQL = ",".$users_profile_SQL;
}
//SQL-подзапрос компонует строку с данными пользователя
$SQL_SELECT_CUSTOMER = " IF( `user_id` = 0, 'ID 0, ".translate_str_by_id(3233)."', CONCAT( 'ID ', `user_id`, ', E-mail: ', IF(`email`!='', `email`, 'Не указан'), ', Телефон: ', IF(`phone`!='', `phone`, 'Не указан') ".$users_profile_SQL." ) )";



//Массив найденных вариантов
$users = array();
$user = array();
$user['user_id'] = 0;
$user['user_info'] = 'ID 0, '.translate_str_by_id(3233);
$users[] = $user;



$users_query = $db_link->prepare("SELECT *, $SQL_SELECT_CUSTOMER AS `customer` FROM `users` WHERE $SQL_SELECT_CUSTOMER LIKE ?;");
$users_query->execute( array($input_str) );
while( $user_record = $users_query->fetch() )
{
	$user = array();
	$user['user_id'] = $user_record['user_id'];
	$user['user_info'] = $user_record['customer'];
	
	$users[] = $user;
}


$answer = array();
$answer["status"] = true;
$answer["message"] = "";
$answer["vars"] = $users;
exit(json_encode($answer));
?>