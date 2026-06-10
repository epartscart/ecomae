<?php
/**
 * Shared go_to_pay for UAE gateway demo stubs.
 * Set $EPC_PAY_HANDLER before including.
 */
if (!isset($EPC_PAY_HANDLER) || $EPC_PAY_HANDLER === '') {
	exit('No handler');
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
	exit(json_encode(array('result' => false)));
}
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$user_id = DP_User::getUserId();
$operation_id = (int)($_GET['operation'] ?? 0);
$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0 AND `user_id` = ?;');
$operation_query->execute(array($operation_id, $user_id));
$operation = $operation_query->fetch(PDO::FETCH_ASSOC);
if (!$operation) {
	exit(json_encode(array('result' => false, 'code' => 2)));
}

$operation_description = translate_str_by_id(4338);
if ($operation['pay_orders'] !== '' && $operation['pay_orders'] !== null) {
	$operation_description = translate_str_by_id(4350);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/get_pay_system_parameters.php';

$demoMode = !empty($paysystem_parameters['demo_mode']);
$currency = !empty($paysystem_parameters['currency']) ? (string)$paysystem_parameters['currency'] : 'AED';
$sum = (float)$operation['amount'];
$handler = preg_replace('/[^a-z0-9_]/', '', (string)$EPC_PAY_HANDLER);

if (!$demoMode) {
	?>
	<!DOCTYPE html><html><head><meta charset="utf-8"><title>Configure gateway</title>
	<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css"></head>
	<body style="padding:40px"><div class="container"><div class="alert alert-warning">
	<strong><?php echo htmlspecialchars(ucfirst(str_replace('_', ' ', $handler)), ENT_QUOTES, 'UTF-8'); ?></strong> is set to live mode but live API integration is not wired yet.
	Enable <em>Demo mode</em> in CP → Payment gateways, or add real API keys and implement the live redirect.
	<br><a href="<?php echo $multilang_params['lang_href']; ?>/shop/balans" class="btn btn-default btn-sm" style="margin-top:12px">Back</a>
	</div></div></body></html>
	<?php
	exit;
}
?>
<form name="pay_form" style="display:none" method="post" action="/content/shop/finance/payment_systems/epc_demo/pay_page.php">
	<input type="hidden" name="EPC_PAY_HANDLER" value="<?php echo htmlspecialchars($handler, ENT_QUOTES, 'UTF-8'); ?>">
	<input type="hidden" name="operation_id" value="<?php echo (int)$operation_id; ?>">
	<input type="hidden" name="sum" value="<?php echo htmlspecialchars((string)$sum, ENT_QUOTES, 'UTF-8'); ?>">
	<input type="hidden" name="operation_description" value="<?php echo htmlspecialchars($operation_description, ENT_QUOTES, 'UTF-8'); ?>">
	<input type="hidden" name="user_id" value="<?php echo (int)$user_id; ?>">
	<input type="hidden" name="currency" value="<?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>">
</form>
<script>document.forms['pay_form'].submit();</script>
