<?php
require_once($_SERVER["DOCUMENT_ROOT"] ."/config.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

function genHash( $array )
{
	$str = "";
	ksort($array);
	
	if($array['Success'] === true){
		$array['Success'] = 'true';
	}
	
	foreach($array as $key => $v)
	{
		if( $key=="Receipt" || $key == "DATA" )
			continue;
		$str .= $v;
	}
	
	$str_HASH = hash("sha256", $str);
	
	return $str_HASH;
}


$DP_Config = new DP_Config();

try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
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
?>