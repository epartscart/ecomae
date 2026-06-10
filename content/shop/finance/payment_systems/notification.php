<?php
/**
 * Shared notification for UAE gateway demo stubs.
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
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$operation_id = (int)($_POST['operation_id'] ?? $_GET['operation_id'] ?? 0);
$sum = (float)($_POST['sum'] ?? 0);
$demoToken = (string)($_POST['demo_token'] ?? '');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/get_pay_system_parameters.php';

if (empty($paysystem_parameters['demo_mode']) && $demoToken !== 'epc-demo-ok') {
	exit('Forbidden');
}

$check = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
$check->execute(array($operation_id));
if ((int)$check->fetchColumn() !== 1) {
	header('Location: ' . $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] . '/shop/balans?success_message=' . urlencode(translate_str_by_id(4355)));
	exit;
}

$db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;')->execute(array($operation_id));

$amount = $sum;
$operation_id = $operation_id;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/pay_notify.php';

$operation_id = (int)$_POST['operation_id'];
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/pay_for_order.php';

header('Location: ' . $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] . '/shop/balans?success_message=' . urlencode(translate_str_by_id(4355)));
exit;
