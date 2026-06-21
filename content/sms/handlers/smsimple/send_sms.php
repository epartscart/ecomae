<?php
error_reporting( 0 );

require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");

require_once( "smsimple.class.php" );


$DP_Config = new DP_Config();

try
{
	$db_link = new PDO(
		'mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, 
		$DP_Config->user, 
		$DP_Config->password
	);
	
} catch ( PDOException $e ) {
	
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "Error";
	exit( json_encode($answer) );
	
}

$db_link->query("SET NAMES utf8;");

//Проверка прав на запуск скрипта
if( $_POST["check"] != $DP_Config->secret_succession ) {
	
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit( json_encode($answer) );
	
} 

//Получаем настройки SMS-оператора
$sms_api_query = $db_link->prepare('SELECT * FROM `sms_api` WHERE `handler` = ?;');
$sms_api_query->execute( array('smsimple') );
$sms_api = $sms_api_query->fetch( PDO::FETCH_ASSOC );

$parameters_values	= json_decode( $sms_api["parameters_values"], true );

$serivce 				= "http://api.smsimple.ru/";
$signature_id 			= $parameters_values['signature_id']; //ID подписи

//Тело сообщения, адресаты
//$subject 		= $_POST["subject"];
$body 			= $_POST["body"];
$main_field 	= "7" . $_POST['main_field'];

//Инициализируем обработчик
$params_api = array (
	'url' => $serivce,
	'username' => $parameters_values['login'],
	'password' => $parameters_values['password']
);

$api = new SMSimple( $params_api );

try {
	
	$api->connect();
	$message_id = $api->send( $signature_id, $main_field, $body ); 
	
} 
catch ( SMSimpleException $e ) {
	
	$answer = array (
		'status' => false,
		'message' => $e->getMessage()
	);
	
	exit( json_encode( $answer ) );
	
}

$answer = array( 'status' => true );

exit( json_encode( $answer ) );
?>