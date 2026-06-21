<?php
//Серверный скрипт для работы с шаблонами категорий (создание, редактирование, удаление, получение списка)
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
	$answer["message"] = "No DB connect";
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


//Проверяем право менеджера
if( ! DP_User::isAdmin())
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit( json_encode($answer) );
}

// ------------------------------------------------------------------------------




switch( $_POST['action'] )
{
	case 'create':
		
		//Получаем содержимое файла изображения
		$image_body = NULL;
		$image_name = NULL;
		if( $_POST['image_type'] == 'from_input' )
		{
			$FILE_POST = $_FILES["image"];
			
			if( $FILE_POST["size"] > 0 )
			{
				$image_body = file_get_contents($FILE_POST['tmp_name']);
				
				$image_name = $FILE_POST['name'];
			}
		}
		else if( $_POST['image_type'] == 'from_category' )
		{
			$category_object = json_decode($_POST['category_object'], true);
			if( file_exists( $_SERVER['DOCUMENT_ROOT'].'/content/files/images/catalogue_images/'.$category_object['image'] ) )
			{
				$image_body = file_get_contents( $_SERVER['DOCUMENT_ROOT'].'/content/files/images/catalogue_images/'.$category_object['image'] );
				
				$image_name = $category_object['image'];
			}
		}

		
		if( ! $db_link->prepare("INSERT INTO `shop_catalogue_categories_templates` (`caption`, `category_object`, `image`, `image_name`) VALUES (?,?,?,?);")->execute( array($_POST['caption'], $_POST['category_object'], $image_body, $image_name) ) )
		{
			$answer = array();
			$answer["status"] = false;
			$answer["message"] = translate_str_by_id(2829);
			exit( json_encode($answer) );
		}
		else
		{
			$answer = array();
			$answer["status"] = true;
			$answer["template_id"] = $db_link->lastInsertId();
			$answer["message"] = '';
			exit( json_encode($answer) );
		}
		break;
	case 'get_all':
		
		$templates = array();
		
		$all_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories_templates` ORDER BY `id` ASC;");
		$all_query->execute();
		
		while( $item = $all_query->fetch() )
		{
			$templates[] = array('id'=>$item['id'], 'value'=>$item['caption'], 'image'=>base64_encode( $item['image'] ), 'image_name'=>$item['image_name'], 'category_object'=>$item['category_object'] );
		}
		
		
		$answer = array();
		$answer["status"] = true;
		$answer["message"] = '';
		$answer["templates"] = $templates;
		exit( json_encode($answer) );
		
		
		break;
	case 'delete':
		
		if( ! $db_link->prepare('DELETE FROM `shop_catalogue_categories_templates` WHERE `id` = ?;')->execute( array( $_POST['template_id'] ) ) )
		{
			$answer = array();
			$answer["status"] = false;
			$answer["message"] = translate_str_by_id(2830);
			exit( json_encode($answer) );
		}
		else
		{
			$answer = array();
			$answer["status"] = true;
			$answer["message"] = '';
			exit( json_encode($answer) );
		}
		
		break;
	default:
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = "Forbidden";
		exit( json_encode($answer) );
}
?>