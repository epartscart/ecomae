<?php
/*
Серверный файл для загрузки файлов через TinyMCE
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



// -------------------------------------------------------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('id'=>256, 'url'=>'shop/catalogue/products/product');//Редактирование одного товара
$pages_to_check[] = array('id'=>215, 'url'=>'content/edit_content');//Редактирование одного материала
$pages_to_check[] = array('id'=>371, 'url'=>'content/content_manager/content');//Создание и редактирование одного материала
$pages_to_check[] = array('id'=>356, 'url'=>'content/dopolnitelnye-teksty/dopolnitelnyj-tekst');//Текст для URL
$pages_to_check[] = array('id'=>239, 'url'=>'modules/module');//Создание и управление одним модулем
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/check_admin_access/check_admin_access.php");
// -------------------------------------------------------------------------------



// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Проверяем доступ в панель управления
if( ! DP_User::isAdmin())
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Forbidden';
	exit(json_encode($answer));
}




/*******************************************************
* Only these origins will be allowed to upload images *
******************************************************/
$accepted_origins = array($DP_Config->domain_path);

/*********************************************
* Change this line to set the upload folder *
*********************************************/
$imageFolder = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/";

$image_url_path = "/content/files/images/";

reset ($_FILES);
$temp = current($_FILES);
if (is_uploaded_file($temp['tmp_name']))
{
	if (isset($_SERVER['HTTP_ORIGIN'])) 
	{
		// same-origin requests won't set an origin. If the origin is set, it must be valid.
		if ( ! in_array($_SERVER['HTTP_ORIGIN']."/", $accepted_origins)) 
		{
			$answer = array();
			$answer["status"] = false;
			$answer["message"] = "Origin Denied";
			exit(json_encode($answer));
		}
	}

	// Sanitize input
	if (preg_match("/([^\w\s\d\-_~,;:\[\]\(\).])|([\.]{2,})/", $temp['name'])) 
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = "Invalid file name";
		exit(json_encode($answer));
	}

	// Verify extension
	if (!in_array(strtolower(pathinfo($temp['name'], PATHINFO_EXTENSION)), array("gif", "jpg", "png"))) 
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = "Invalid extension";
		exit(json_encode($answer));
	}
	
	// Если в папке уже существует файл с таким же наименованием то добавляем к наименованию индекс что бы новый файл не заменил старый
	if(file_exists($imageFolder . $temp['name'])){
		$n = 1;
		while(file_exists($imageFolder . pathinfo($temp['name'], PATHINFO_FILENAME) .'_'. $n .'.'. pathinfo($temp['name'], PATHINFO_EXTENSION))){
			$n++;
		}
		$temp['name'] = pathinfo($temp['name'], PATHINFO_FILENAME) .'_'. $n .'.'. pathinfo($temp['name'], PATHINFO_EXTENSION);
	}

	// Accept upload if there was no origin, or if it is an accepted origin
	$filetowrite = $imageFolder . $temp['name'];
	move_uploaded_file($temp['tmp_name'], $filetowrite);
	
	$image_url_path = $image_url_path.$temp['name'];//Путь для URL
	
	//УСПЕШНОЕ ВЫПОЛНЕНИЕ
	$answer = array();
	$answer["status"] = true;
	$answer["url"] = $image_url_path;
	exit(json_encode($answer));
} 
else 
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Server Error";
	exit(json_encode($answer));
}
?>