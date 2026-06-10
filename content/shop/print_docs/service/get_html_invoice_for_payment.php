<?php
//Скрипт для формирования HTML счета на оплату
defined('_INTASK_') or die('No access');


$order_id = (int)$_GET["order_id"];


//Получаем запись заказа и проверяем права на печать
if(DP_User::isAdmin())
{
	//Вызов со стороны админа - поэтому, не проверяем принадлежность заказа пользователю
	$order_query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ?;');
	$order_query->execute( array($order_id) );
	$order_record = $order_query->fetch();
}
else
{
	//Вызов со стороны обычноного пользователя
	if($user_id == 0)
	{
		//Не авторизованные не могут печатать
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = "Not authorized";
		exit(json_encode($answer));
	}
	
	
	$order_query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `user_id` = ? AND `id` = ?;');
	$order_query->execute( array($user_id, $order_id) );
	$order_record = $order_query->fetch();
}
if($order_record == false)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "No such order";
    exit(json_encode($answer));
}






//Формируем HTML
ob_start();
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 5.0 Transitional//EN">
<HTML>
<HEAD>
<META HTTP-EQUIV="Content-Type" CONTENT="text/html; CHARSET=utf-8">
<TITLE></TITLE>
<STYLE TYPE="text/css">
body { background: #ffffff; margin: 0; font-family: DejaVu Sans, sans-serif; font-size: 8pt; font-style: normal; }
tr.R0{ height: 15px; }
tr.R0 td.R3C1{ border-left: #000000 1px solid; border-bottom: #000000 1px solid; border-right: #000000 1px solid; }
tr.R0 td.R7C1{ font-family: DejaVu Sans, sans-serif; font-size: 8pt; font-style: normal; text-align: left; vertical-align: middle; border-left: #000000 1px solid; border-top: #ffffff 0px none; border-bottom: #000000 1px solid; }
tr.R0 td.R9C1{ font-family: DejaVu Sans, sans-serif; font-size: 14pt; font-style: normal; font-weight: bold; vertical-align: middle; }
tr.R1{ height: 17px; }
tr.R1 td.R19C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; vertical-align: top; }
tr.R1 td.R19C5{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; vertical-align: top; }
tr.R1 td.R1C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; border-right: #000000 1px solid; }
tr.R1 td.R1C19{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; vertical-align: middle; border-left: #000000 1px solid; border-top: #000000 1px solid; border-bottom: #000000 1px solid; border-right: #000000 1px solid; }
tr.R1 td.R1C22{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; text-align: left; vertical-align: middle; border-left: #000000 1px solid; border-top: #000000 1px solid; border-bottom: #ffffff 0px none; border-right: #000000 1px solid; }
tr.R1 td.R21C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; text-align: center; vertical-align: middle; border-left: #000000 2px solid; border-top: #000000 2px solid; }
tr.R1 td.R21C2{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; text-align: center; vertical-align: middle; border-left: #000000 1px solid; border-top: #000000 2px solid; }
tr.R1 td.R21C6{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; text-align: center; vertical-align: middle; border-left: #000000 1px solid; border-top: #000000 2px solid; border-right: #000000 2px solid; }
tr.R1 td.R24C6{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; text-align: right; vertical-align: top; }
tr.R1 td.R27C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; }
tr.R1 td.R36C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; }
tr.R1 td.R36C19{ text-align: right; border-bottom: #000000 1px solid; }
tr.R1 td.R36C7{ border-bottom: #000000 1px solid; }
tr.R1 td.R4C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; text-align: left; vertical-align: middle; border-left: #000000 1px solid; border-top: #000000 1px solid; }
tr.R1 td.R4C19{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; border-bottom: #000000 1px solid; border-right: #000000 1px solid; }
tr.R1 td.R4C22{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; text-align: left; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; border-bottom: #000000 1px solid; border-right: #000000 1px solid; }
tr.R1 td.R4C3{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; text-align: left; vertical-align: middle; border-top: #000000 1px solid; border-right: #000000 1px solid; }
tr.R1 td.R5C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; text-align: left; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; }
tr.R11{ height: 9px; }
tr.R11 td.R11C1{ border-bottom: #000000 2px solid; }
tr.R11 td.R23C1{ border-top: #000000 2px solid; }
tr.R13{ height: 34px; }
tr.R13 td.R13C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; vertical-align: top; }
tr.R13 td.R13C5{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; vertical-align: top; }
tr.R16{ height: 18px; }
tr.R16 td.R16C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; vertical-align: top; }
tr.R16 td.R16C5{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; font-weight: bold; vertical-align: top; }
tr.R2{ height: 16px; }
tr.R2 td.R2C19{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; border-bottom: #000000 1px solid; border-right: #000000 1px solid; }
tr.R2 td.R2C22{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; text-align: left; vertical-align: top; border-left: #000000 1px solid; border-top: #ffffff 0px none; border-bottom: #000000 1px solid; border-right: #000000 1px solid; }
tr.R2 td.R30C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; }
tr.R22{ height: 29px; }
tr.R22 td.R22C1{ text-align: center; vertical-align: top; overflow: hidden;border-left: #000000 2px solid; border-top: #000000 1px solid; }
tr.R22 td.R22C2{ text-align: left; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; }
tr.R22 td.R22C3{ text-align: right; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; }
tr.R22 td.R22C4{ text-align: left; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; }
tr.R22 td.R22C6{ text-align: right; vertical-align: top; border-left: #000000 1px solid; border-top: #000000 1px solid; border-right: #000000 2px solid; }
tr.R29{ height: 10px; }
tr.R29 td.R29C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; }
tr.R31{ height: 31px; }
tr.R31 td.R31C1{ font-family: DejaVu Sans, sans-serif; font-size: 9pt; font-style: normal; text-align: left; }
table {table-layout: fixed; padding: 0px; padding-left: 2px; vertical-align:bottom; border-collapse:collapse;width: 100%; font-family: DejaVu Sans, sans-serif; font-size: 8pt; font-style: normal; }
td { padding: 0px; padding-left: 2px; overflow:hidden; }
</STYLE>
</HEAD>
<BODY STYLE="background: #ffffff; margin: 0; font-family: DejaVu Sans, sans-serif; font-size: 8pt; font-style: normal; ">
<TABLE style="width:100%; height:0px; " CELLSPACING=0>
<COL WIDTH=7>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=26>
<COL WIDTH=15>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=17>
<COL WIDTH=16>
<COL WIDTH=16>
<COL WIDTH=17>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=23>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=12>
<COL WIDTH=13>
<COL>
<TR CLASS=R0>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD>&nbsp;</TD>
</TR>
<TR CLASS=R1>
<TD><SPAN></SPAN></TD>
<TD CLASS="R1C1" COLSPAN=18 ROWSPAN=2><?php echo $parameters_values["bank"]; ?></TD>
<TD CLASS="R1C19" COLSPAN=3><SPAN STYLE="white-space:nowrap;max-width:0px;">БИК</SPAN></TD>
<TD CLASS="R1C22" COLSPAN=11><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $parameters_values["bik"]; ?></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R2>
<TD><SPAN></SPAN></TD>
<TD CLASS="R2C19" COLSPAN=3 ROWSPAN=2><SPAN STYLE="white-space:nowrap;max-width:0px;">К/С&nbsp;№</SPAN></TD>
<TD CLASS="R2C22" COLSPAN=11 ROWSPAN=2><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $parameters_values["k_schet"]; ?></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R0>
<TD><SPAN></SPAN></TD>
<TD CLASS="R3C1" COLSPAN=18><SPAN STYLE="white-space:nowrap;max-width:0px;">Банк&nbsp;получателя</SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R1>
<TD><SPAN></SPAN></TD>
<TD CLASS="R4C1" COLSPAN=2><SPAN STYLE="white-space:nowrap;max-width:0px;">ИНН</SPAN></TD>
<TD CLASS="R4C3" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $parameters_values["inn"]; ?></SPAN></TD>
<TD CLASS="R4C1" COLSPAN=2><SPAN STYLE="white-space:nowrap;max-width:0px;">КПП&nbsp;&nbsp;</SPAN></TD>
<TD CLASS="R4C3" COLSPAN=7><SPAN><?php echo $parameters_values["kpp"]; ?></SPAN></TD>
<TD CLASS="R4C19" COLSPAN=3 ROWSPAN=4><SPAN STYLE="white-space:nowrap;max-width:0px;">Р/С&nbsp;№</SPAN></TD>
<TD CLASS="R4C22" COLSPAN=11 ROWSPAN=4><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $parameters_values["r_schet"]; ?></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R1>
<TD><SPAN></SPAN></TD>
<TD CLASS="R5C1" COLSPAN=18 ROWSPAN=2><?php echo $parameters_values["payment_receiver"]; ?></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R2>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD>&nbsp;</TD>
</TR>
<TR CLASS=R0>
<TD><SPAN></SPAN></TD>
<TD CLASS="R7C1" COLSPAN=18><SPAN STYLE="white-space:nowrap;max-width:0px;">Получатель</SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD>&nbsp;</TD>
</TR>
<TR CLASS=R0>
<TD><SPAN></SPAN></TD>
<TD CLASS="R9C1" COLSPAN=32 ROWSPAN=2>Счет на оплату № <?php echo $_GET["order_id"]; ?> от <?php echo get_date($order_record["time"]); ?> г.</TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R0>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD>&nbsp;</TD>
</TR>
<TR CLASS=R11>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1" COLSPAN=32><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:9px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>
<TR CLASS=R11>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:9px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>
<TR CLASS=R13>
<TD><SPAN></SPAN></TD>
<TD CLASS="R13C1" COLSPAN=4 ROWSPAN=2><SPAN STYLE="white-space:nowrap;max-width:0px;">Продавец<BR>(Исполнитель):</SPAN></TD>
<TD CLASS="R13C5" COLSPAN=28 ROWSPAN=2><?php echo $parameters_values["seller"]; ?></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R0>
<TD><DIV STYLE="position:relative; height:15px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:15px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:15px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:15px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>
<TR CLASS=R11>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:9px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>
<TR CLASS=R16>
<TD><SPAN></SPAN></TD>
<TD CLASS="R16C1" COLSPAN=4 ROWSPAN=2><SPAN STYLE="white-space:nowrap;max-width:0px;">Покупатель<BR>(Заказчик):</SPAN></TD>
<TD CLASS="R16C5" COLSPAN=28 ROWSPAN=2><?php echo get_user_str_by_user_profile_json_builder($order_record["user_id"], $parameters_values["customer"]); ?></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R0>
<TD><DIV STYLE="position:relative; height:15px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:15px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:15px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:15px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>
<TR CLASS=R11>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:9px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>



<?php
if( $parameters_values["basis_of_payment"] != "" )
{
	?>
	<TR CLASS=R1>
	<TD><SPAN></SPAN></TD>
	<TD CLASS="R19C1" COLSPAN=4><SPAN STYLE="white-space:nowrap;max-width:0px;">Основание:</SPAN></TD>
	<TD CLASS="R19C5" COLSPAN=28><?php echo $parameters_values["basis_of_payment"]; ?></TD>
	<TD><SPAN></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	<?php
}
?>



<TR CLASS=R11>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:9px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>
</TABLE>
<TABLE style="width:100%; height:0px; " CELLSPACING=0>
<COL WIDTH=7>
<COL WIDTH=32>
<COL WIDTH=328>
<COL WIDTH=54>
<COL WIDTH=42>
<COL WIDTH=87>
<COL WIDTH=99>
<COL>
<TR CLASS=R1>
<TD><SPAN></SPAN></TD>
<TD CLASS="R21C1"><SPAN STYLE="white-space:nowrap;max-width:0px;">№</SPAN></TD>
<TD CLASS="R21C2"><SPAN STYLE="white-space:nowrap;max-width:0px;">Товары&nbsp;(работы,&nbsp;услуги)</SPAN></TD>
<TD CLASS="R21C2"><SPAN STYLE="white-space:nowrap;max-width:0px;">Кол-во</SPAN></TD>
<TD CLASS="R21C2"><SPAN STYLE="white-space:nowrap;max-width:0px;">Ед.</SPAN></TD>
<TD CLASS="R21C2"><SPAN STYLE="white-space:nowrap;max-width:0px;">Цена</SPAN></TD>
<TD CLASS="R21C6"><SPAN STYLE="white-space:nowrap;max-width:0px;">Сумма</SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>

<?php
//Получаем товарные позиции заказа, которые участвуют при ценовых расчетах (по статусу позиции)
$WHERE_CONDITIONS = "";
if(isset($_GET['order_items'])){
	$order_items = json_decode($_GET['order_items'], true);
	if(is_array($order_items)){
		$item_id_str = "";
		foreach($order_items as $item_id){
			if($item_id_str != ''){
				$item_id_str .= ', ';
			}
			$item_id_str .= (int) $item_id;
		}
		if($item_id_str != ''){
			$WHERE_CONDITIONS = " AND `shop_orders_items`.`id` IN(".$item_id_str.")";
		}
	}
}
//Наименования
$SELECT_product_name = "(CONCAT(`t2_manufacturer`, ' ', `t2_article`, ' ', `t2_name`))";
//Сумма позиции
$SELECT_item_price_sum = "`price`*`count_need`";
//ВЛОЖЕННЫЙ ЗАПРОС
$SELECT_ORDER_ITEMS = "SELECT *, $SELECT_product_name AS `product_name`, $SELECT_item_price_sum AS `price_sum` FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = ?)$WHERE_CONDITIONS;";

$order_items_query = $db_link->prepare($SELECT_ORDER_ITEMS);
$order_items_query->execute( array($order_id, 1) );
$count_items = 0;//Счетчик позиций
$price_sum_total_num = 0;//Сумма ИТОГО (цифра без форматирования)
while( $order_item = $order_items_query->fetch() )
{
	$count_items++;//Счетчик позиций
	
	$price_sum_total_num = $price_sum_total_num + $order_item["price_sum"];//Сумма ИТОГО
	
	?>
	<TR CLASS=R22>
	<TD><SPAN></SPAN></TD>
	<TD CLASS="R22C1"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $count_items; ?></SPAN></TD>
	<TD CLASS="R22C2"><?php echo trim($order_item["product_name"]); ?></TD>
	<TD CLASS="R22C3"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $order_item["count_need"]; ?></SPAN></TD>
	<TD CLASS="R22C4"><SPAN STYLE="white-space:nowrap;max-width:0px;">шт</SPAN></TD>
	<TD CLASS="R22C3"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo number_format($order_item["price"], 2, '.', ' '); ?></SPAN></TD>
	<TD CLASS="R22C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo number_format($order_item["price_sum"], 2, '.', ' '); ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	<?php
}
$price_sum_total = number_format($price_sum_total_num, 2, '.', ' ');
?>

</TABLE>
<TABLE style="width:100%; height:0px; " CELLSPACING=0>
<COL WIDTH=7>
<COL WIDTH=32>
<COL WIDTH=75>
<COL WIDTH=190>
<COL WIDTH=54>
<COL WIDTH=42>
<COL WIDTH=151>
<COL WIDTH=98>
<COL>
<TR CLASS=R11>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R23C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R23C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R23C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R23C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R23C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R23C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R23C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:9px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>


<?php
//Обработка НДС / UAE VAT
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';
$uae_vat_totals = epc_uae_vat_invoice_totals($db_link, $price_sum_total_num);
$invoice_total_payable = $price_sum_total_num;

if ($uae_vat_totals['exclusive_mode'] && $uae_vat_totals['vat_rate'] > 0)
{
	$invoice_total_payable = (float)$uae_vat_totals['total_incl_vat'];
	$uae_vat_amount = (float)$uae_vat_totals['vat_amount'];
	$uae_vat_rate = (float)$uae_vat_totals['vat_rate'];
	?>
	<TR CLASS=R1>
	<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">Subtotal (ex VAT):</SPAN></TD>
	<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $price_sum_total; ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>

	<TR CLASS=R1>
	<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">VAT <?php echo number_format($uae_vat_rate, 0); ?>% (output):</SPAN></TD>
	<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo number_format($uae_vat_amount, 2, '.', ' '); ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	<?php
}
elseif( $parameters_values["nds"] == "" )
{
	?>
	<TR CLASS=R1>
	<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">Итого:</SPAN></TD>
	<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $price_sum_total; ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	
	
	<TR CLASS=R1>
	<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">Без&nbsp;налога&nbsp;(НДС)</SPAN></TD>
	<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;">-</SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	<?php
}
else
{
	$parameters_values["nds"] = (int)$parameters_values["nds"];
	
	//Получаем значение НДС и сумму без НДС, включенного в сумму:
	$price_sum_total_without_NDS = $price_sum_total_num/(1 + $parameters_values["nds"]/100);
	$NDS = $price_sum_total_num - $price_sum_total_without_NDS;
	?>
	<TR CLASS=R1>
	<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">Сумма с НДС:</SPAN></TD>
	<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $price_sum_total; ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	
	<TR CLASS=R1>
	<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">Сумма без НДС:</SPAN></TD>
	<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo number_format($price_sum_total_without_NDS, 2, '.', ' '); ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	
	
	<TR CLASS=R1>
	<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">НДС <?php echo $parameters_values["nds"]; ?>%:</SPAN></TD>
	<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo number_format($NDS, 2, '.', ' '); ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	<?php
}
?>


<TR CLASS=R1>
<TD CLASS="R24C6" COLSPAN=7><SPAN STYLE="white-space:nowrap;max-width:0px;">Всего&nbsp;к&nbsp;оплате:</SPAN></TD>
<TD CLASS="R24C6"><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo number_format($invoice_total_payable, 2, '.', ' '); ?></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>


</TABLE>
<TABLE style="width:100%; height:0px; " CELLSPACING=0>
<COL WIDTH=7>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=26>
<COL WIDTH=15>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=17>
<COL WIDTH=16>
<COL WIDTH=16>
<COL WIDTH=17>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=23>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=21>
<COL WIDTH=12>
<COL WIDTH=13>
<COL>
<TR CLASS=R1>
<TD><SPAN></SPAN></TD>
<TD CLASS="R27C1" COLSPAN=32><SPAN STYLE="white-space:nowrap;max-width:0px;">Всего&nbsp;наименований&nbsp;<?php echo $count_items; ?>,&nbsp;на&nbsp;сумму&nbsp;<?php echo number_format($invoice_total_payable, 2, '.', ' '); ?>&nbsp;<?php echo $currency["caption_short"]; ?>.</SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R1>
<TD><SPAN></SPAN></TD>
<TD CLASS="R19C5" COLSPAN=31><?php echo num2str($invoice_total_payable); ?></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
<TR CLASS=R29>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R29C1"><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:10px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:10px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>


<?php
if( $parameters_values["pay_before"] != "" )
{
	$pay_before = (int)$parameters_values["pay_before"];
	$pay_before = $order_record["time"] + $pay_before*86400;
	?>
	<TR CLASS=R2>
	<TD><SPAN></SPAN></TD>
	<TD CLASS="R30C1" COLSPAN=32><SPAN STYLE="white-space:nowrap;max-width:0px;">Оплатить&nbsp;не&nbsp;позднее&nbsp;<?php echo date("d.m.Y", $pay_before); ?></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	<?php
}
?>



<?php
if( $parameters_values["explanation"] != "" )
{
	
	?>
	<TR CLASS=R31>
	<TD><SPAN></SPAN></TD>
	<TD CLASS="R31C1" COLSPAN=32><?php echo $parameters_values["explanation"]; ?></TD>
	<TD><SPAN></SPAN></TD>
	<TD><SPAN></SPAN></TD>
	<TD></TD>
	</TR>
	<?php
}
?>




<TR CLASS=R11>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD CLASS="R11C1"><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="position:relative; height:9px;width: 100%; overflow:hidden;"><SPAN></SPAN></DIV></TD>
<TD><DIV STYLE="width:100%;height:9px;overflow:hidden;">&nbsp;</DIV></TD>
</TR>
<TR>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD>&nbsp;</TD>
</TR>
<TR>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD>&nbsp;</TD>
</TR>
<TR CLASS=R1>
<TD><SPAN></SPAN></TD>
<TD CLASS="R36C1" COLSPAN=6><SPAN STYLE="white-space:nowrap;max-width:0px;"><?php echo $parameters_values["dl_name"]; ?></SPAN></TD>
<TD CLASS="R36C7"><SPAN></SPAN></TD>
<TD CLASS="R36C7"><SPAN></SPAN></TD>
<TD CLASS="R36C7"><SPAN></SPAN></TD>
<TD CLASS="R36C7" COLSPAN=9>
	<SPAN>
		<?php
		if( $parameters_values["signature_scan"] != "" )
		{
			if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/images/".$parameters_values["signature_scan"]) )
			{
				?>
				<img src="/content/files/images/<?php echo $parameters_values["signature_scan"]; ?>" style="max-width:150px;max-height:100px;" />
				<?php
			}
		}
		?>
	</SPAN>
</TD>
<TD CLASS="R36C19" COLSPAN=14><?php echo $parameters_values["dl_fio"]; ?></TD>
<TD><SPAN></SPAN></TD>
<TD><SPAN></SPAN></TD>
<TD></TD>
</TR>
</TABLE>

<br><br><br>
<?php
if( $parameters_values["stamp_scan"] != "" )
{
	if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/images/".$parameters_values["stamp_scan"]) )
	{
		?>
		<img src="/content/files/images/<?php echo $parameters_values["stamp_scan"]; ?>" style="width:180px;height:180px;margin-left:50px;" />
		<?php
	}
}
?>
</BODY>
</HTML>
<?php
$HTML = ob_get_contents();
ob_end_clean();
?>