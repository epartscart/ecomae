<?php
/**
 * Скрипт проверки captcha по синхронному запросу из Javascript
*/

//Если значение не передано - ничего не делаем
if(!isset($_REQUEST['captcha_check']))
{
	exit;
}

//Получаем значение от пользователя и сразу переводим его в md5:
$user_captcha = md5($_REQUEST['captcha_check']);
//Правильная captcha из Куки, которая уже в md5:
$cookie_captcha = $_COOKIE["captcha"];

if($user_captcha == $cookie_captcha)
{
	echo "true";
}
else
{
	echo "false";
}
?>