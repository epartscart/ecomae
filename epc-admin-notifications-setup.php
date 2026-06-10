<?php
/**
 * Register epc_customer_login notification + document CRM profile key.
 * https://www.epartscart.com/epc-admin-notifications-setup.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

try {
	$cfg = new DP_Config();
	$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

	$name = 'epc_customer_login';
	$exists = $db->prepare('SELECT `id` FROM `notifications_settings` WHERE `name` = ? LIMIT 1');
	$exists->execute(array($name));
	$id = (int)$exists->fetchColumn();

	$vars = '[{"name":"user_id","caption":"User ID","type":"text"},{"name":"login_contact","caption":"Login","type":"text"},{"name":"event_html","caption":"Details","type":"text"}]';
	$email_subject = 'Customer sign-in - %site_name%';
	$email_body = '<div style="font-family:Calibri,Arial,sans-serif;">%event_html%</div>';

	if ($id > 0) {
		$db->prepare(
			'UPDATE `notifications_settings` SET `email_subject` = ?, `email_body` = ?, `vars` = ?, `email_on` = 1 WHERE `id` = ?'
		)->execute(array($email_subject, $email_body, $vars, $id));
	} else {
		$db->prepare(
			'INSERT INTO `notifications_settings`
			(`name`, `caption`, `description`, `event`, `email_subject`, `email_body`, `sms_body`, `email_on`, `sms_on`, `vars`, `send_for_not_confirmed`, `foreseen_email`, `foreseen_sms`, `default_email_subject`, `default_email_body`, `default_sms_body`)
			VALUES (?, ?, ?, ?, ?, ?, ?, 1, 0, ?, 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$name,
			'Customer login alert',
			'eParts Cart - notify staff when a customer signs in',
			$name,
			$email_subject,
			$email_body,
			'',
			$vars,
			$email_subject,
			$email_body,
			'',
		));
		$id = (int)$db->lastInsertId();
	}

	echo "Notification epc_customer_login id={$id}\n";

	$db->prepare(
		'UPDATE `notifications_settings`
		 SET `send_for_not_confirmed` = 1, `email_on` = 1
		 WHERE `name` IN (\'new_order_to_manager\', \'new_order_to_user\')'
	)->execute();
	echo "Order notifications: send_for_not_confirmed=1 for new_order_to_manager + new_order_to_user\n";

	echo "Admin inbox: epartscart@gmail.com (via epc_admin_notify_email)\n";
	echo "Assign CRM: users_profiles.data_key = epc_crm_user_id, data_value = <backend user id>\n";
	echo "Done.\n";
} catch (Throwable $e) {
	echo 'FAIL ' . $e->getMessage() . "\n";
}
