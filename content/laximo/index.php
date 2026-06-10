<?php

// ini_set('error_reporting', E_ALL);
// ini_set('display_errors', 1);
// ini_set('display_startup_errors', 1);

//Автозагрузка классов
set_include_path(get_include_path().PATH_SEPARATOR.$_SERVER["DOCUMENT_ROOT"]."/content/laximo/");
spl_autoload_register(function($class){
	$path = $_SERVER["DOCUMENT_ROOT"]."/content/laximo/";
	$file = preg_replace('/guayaquil/', 'com_guayaquil', $class);
	$file = preg_replace('/\\\\/', '/', $file);
	require_once($path . $file . '.php');
});



require_once($_SERVER["DOCUMENT_ROOT"] . '/content/laximo/vendor/autoload.php');
require_once($_SERVER["DOCUMENT_ROOT"] . '/content/laximo/com_guayaquil/index.php');

?>