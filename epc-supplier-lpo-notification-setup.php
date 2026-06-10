<?php
/**
 * Register supplier LPO notification + warehouse order_email field.
 * https://www.epartscart.com/epc-supplier-lpo-notification-setup.php?token=epartscart-deploy-2026
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

	$name = 'lpo_to_supplier';
	$exists = $db->prepare('SELECT `id` FROM `notifications_settings` WHERE `name` = ? LIMIT 1');
	$exists->execute(array($name));
	$id = (int)$exists->fetchColumn();

	$vars = '[{"name":"order_id","caption":"Customer order ID","type":"text"},{"name":"lpo_number","caption":"LPO number","type":"text"},{"name":"storage_name","caption":"Warehouse","type":"text"},{"name":"order_text","caption":"LPO body","type":"text"}]';
	$email_subject = 'LPO #%lpo_number% — please supply parts (%storage_name%)';
	$email_body = '%order_text%';

	if ($id > 0) {
		$db->prepare(
			'UPDATE `notifications_settings` SET `caption` = ?, `description` = ?, `email_subject` = ?, `email_body` = ?, `vars` = ?, `email_on` = 1, `send_for_not_confirmed` = 1 WHERE `id` = ?'
		)->execute(array(
			'Supplier LPO (purchase order)',
			'eParts Cart — e-mail supplier to ship goods. LPO number = customer order number.',
			$email_subject,
			$email_body,
			$vars,
			$id,
		));
	} else {
		$db->prepare(
			'INSERT INTO `notifications_settings`
			(`name`, `caption`, `description`, `event`, `email_subject`, `email_body`, `sms_body`, `email_on`, `sms_on`, `vars`, `send_for_not_confirmed`, `foreseen_email`, `foreseen_sms`, `default_email_subject`, `default_email_body`, `default_sms_body`)
			VALUES (?, ?, ?, ?, ?, ?, ?, 1, 0, ?, 1, 1, 0, ?, ?, ?)'
		)->execute(array(
			$name,
			'Supplier LPO (purchase order)',
			'eParts Cart — e-mail supplier to ship goods. LPO number = customer order number.',
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
	echo "Notification lpo_to_supplier id={$id}\n";

	$newField = array(
		'caption' => 'Supplier order email (LPO)',
		'name' => 'order_email',
		'type' => 'text',
		'hint' => 'E-mail for purchase orders. LPO number = customer order number. Falls back to price list sender e-mail if empty.',
	);

	foreach (array(1, 2) as $interfaceTypeId) {
		$row = $db->prepare('SELECT `connection_options` FROM `shop_storages_interfaces_types` WHERE `id` = ? LIMIT 1');
		$row->execute(array($interfaceTypeId));
		$json = (string)$row->fetchColumn();
		$opts = json_decode($json, true);
		if (!is_array($opts)) {
			$opts = array();
		}
		$has = false;
		foreach ($opts as $opt) {
			if (is_array($opt) && ($opt['name'] ?? '') === 'order_email') {
				$has = true;
				break;
			}
		}
		if (!$has) {
			$opts[] = $newField;
			$db->prepare('UPDATE `shop_storages_interfaces_types` SET `connection_options` = ? WHERE `id` = ?')
				->execute(array(json_encode($opts, JSON_UNESCAPED_UNICODE), $interfaceTypeId));
			echo "Added order_email field to interface type {$interfaceTypeId}\n";
		} else {
			echo "Interface type {$interfaceTypeId} already has order_email\n";
		}
	}

	echo "\nConfigure each warehouse: CP → Shop → Logistics → Warehouses → edit → Supplier order email (LPO)\n";
	echo "Or rely on Price list → Sender e-mail (for price-list warehouses).\n";
	echo "CP → Control panel → Notifications → lpo_to_supplier — edit subject/body if needed.\n";
	echo "Done.\n";
} catch (Throwable $e) {
	echo 'FAIL ' . $e->getMessage() . "\n";
}
