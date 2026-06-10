<?php
// Добавление товара в блокнот
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



if(empty($user_id)){
	$answer["html"] = '<span><i style="font-size: 30px; color: red; position: relative; top: 4px;" class="fa fa-times"></i> '.translate_str_by_id(2063).'</span>';
}else{
	$garage_id = $request_object['id_notepad'];
	
	if($garage_id > 0){
		$query = $db_link->prepare('SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND `id` = ?;');
		$query->execute( array($user_id, $garage_id) );
		
		if($query->fetchColumn() > 0){
			$flag = true;
		}else{
			$answer["html"] = '<span><i style="font-size: 30px; color: red; position: relative; top: 4px;" class="fa fa-times"></i> '.translate_str_by_id(2064).'</span>';
			$flag = false;
		}
	}else{
		$flag = true;
	}
	
	if($flag == true){
		$brend = trim(htmlspecialchars(strip_tags($request_object['manufacturer'])));
		$article = trim(htmlspecialchars(strip_tags($request_object['article'])));
		$name = trim(htmlspecialchars(strip_tags($request_object['name'])));
		$exist = 0;
		$price = 0;
		$comment = translate_str_by_id(2065).' '. date("d-m-Y H:i", time());
		
		if(!empty($article)){
			if($db_link->prepare('INSERT INTO `shop_docpart_garage_notepad` (`user_id`, `garage_id`, `brend`, `article`, `name`, `exist`, `price`, `comment`) VALUES (?,?,?,?,?,?,?,?);')->execute( array($user_id, $garage_id, $brend, $article, $name, $exist, $price, $comment) ))
			{
				$answer["html"] = '<span><i style="font-size: 30px; color: green;" class="fa fa-check"></i> '.translate_str_by_id(2066).'</span>';
			}else{
				$answer["html"] = '<span><i style="font-size: 30px; color: red; position: relative; top: 4px;" class="fa fa-times"></i> '.translate_str_by_id(2067).'</span>';
			}
		}else{
			$answer["html"] = '<span><i style="font-size: 30px; color: red; position: relative; top: 4px;" class="fa fa-times"></i> '.translate_str_by_id(2068).'</span>';
		}
	}
}

$answer["status"] = true;
$answer["tag"] = 'UCatalog_add_bloknot_msg';
?>