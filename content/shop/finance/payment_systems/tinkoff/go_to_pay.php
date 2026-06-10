<?php
require_once("include.php"); //Необходимые библиотеки

// TerminalKey	String(20)	Да	Идентификатор терминала, выдается Продавцу Банком
// Amount	Number(10)	Да	Сумма в копейках
// OrderId	String(50)	Да	Номер заказа в 

$user_id = DP_User::getUserId();

// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Получаем данные операции
$operation_id = (int)$_GET["operation"];
$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0 AND `user_id` = ?;');
$operation_query->execute( array($operation_id, $user_id) );
$operation = $operation_query->fetch();
if($operation == false)
{
    $answer = array();
	$answer["result"] = false;
	$answer["code"] = 2;
	exit(json_encode($answer));
}
$operation_description = translate_str_by_id(4338);
if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$operation_description = translate_str_by_id(4350);
}

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


$terminal_id = $paysystem_parameters["terminal_id"];
?>
<head>
	<meta charset="utf-8">
	<script src="https://code.jquery.com/jquery-1.12.4.min.js" integrity="sha256-ZosEbRLbNQzLpnKIkEdrPv7lOy9C27hHQ+Xp8a4MxAQ=" crossorigin="anonymous"></script>
	<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css" integrity="sha384-BVYiiSIFeK1dGmJRAkycuHAHRg32OmUcww7on3RYdg4Va+PmSTsz/K68vbdEjh4u" crossorigin="anonymous" />

	<script src="https://securepay.tinkoff.ru/html/payForm/js/tinkoff.js"></script>
</head>
<body>
	<div class="container">
		<div class="row">
			<div class="col-lg-6"  style="margin: 10% 0 0 10%">
			
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					
				</div>
				
				<div class="panel-body">
					<form class="form" name="TinkoffPayForm" onsubmit="pay(this); return false;">
						<input class="" type="hidden" name="terminalkey" value="<?=$terminal_id?>">
						<input class="" type="hidden" name="frame" value="false">
						<input class="" type="hidden" name="language" value="ru">
						<input class="" type="hidden" placeholder="<?php echo translate_str_by_id(3244); ?>" name="order" value="<?=$operation["id"];?>">
						<input class="" type="hidden" placeholder="<?php echo translate_str_by_id(4396); ?>" name="phone">
						
						<div class="form-group">
							<label class="control-label" for="amount"><?php echo translate_str_by_id(3516); ?></label>
							<input class="form-control" disabled type="text" placeholder="<?php echo translate_str_by_id(3516); ?>" id="amount" name="amount" value="<?=$operation["amount"];?>" requred>	
						</div>
						
						<div class="form-group">
							<label class="contol-label" for="description" ><?php echo translate_str_by_id(4397); ?></label>
							<input class="form-control" type="text" placeholder="<?php echo translate_str_by_id(4397); ?>" id="description" name="description" value="<?=$operation_description;?>">
						</div>
						<div class="form-group">
							<label for="name" class="control-label"><?php echo translate_str_by_id(4398); ?></label>
							<input class="form-control" type="text" placeholder="<?php echo translate_str_by_id(4398); ?>" id="name" name="name">
						</div>
						
						<div class="form-group">
							<label for="email" class="conrol-label">E-mail</label>
							<input class="form-control" type="text" id="email" placeholder="E-mail" name="email">
						</div>
						<input class="btn btn-success" type="submit" value="<?php echo translate_str_by_id(4399); ?>">
					</form>
				</div>
				<div class="panel-body"></div>
				
			</div>
			</div>
		</div>
	</div>
</body>