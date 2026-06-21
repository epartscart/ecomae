<?php
/*
Скрипт проверки работоспособности электронной почты
*/
exit;
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer.php");
$DP_Config = new DP_Config;


$docpartMailer = new DocpartMailer();//Объект обработчика
$docpartMailer->Subject = "Тест почты на сайте";//Тема письма
$docpartMailer->Body = "Тест почты на сайте";//Текст письма
$docpartMailer->CharSet="UTF-8";
$docpartMailer->addAddress($DP_Config->from_email, $DP_Config->from_email);// Добавляем адрес в список получателей
$docpartMailer->IsSMTP();
$docpartMailer->SMTPDebug = 1;
if(!$docpartMailer->Send())
{
	echo "Ошибка отправки ссылки активации. ".$docpartMailer->ErrorInfo;
}
else
{
	echo "Почта работает";
}
?>