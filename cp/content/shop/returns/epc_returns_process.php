<?php
/**
 * Shared returns process helpers for CP + storefront.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Ensure item-status flags for the return workflow.
 * Issued → customer may request return; dedicated approve/reject statuses exist.
 */
function epc_returns_ensure_automation(PDO $db_link): array
{
	$report = array();

	// Status 5 = Issued (for_finish + issue_flag): allow customer return requests.
	$upd = $db_link->prepare('UPDATE `shop_orders_items_statuses_ref` SET `check_for_return` = 1 WHERE `id` = 5 AND `check_for_return` = 0');
	$upd->execute();
	if ($upd->rowCount() > 0) {
		$report[] = 'Enabled return requests on Issued status';
	}

	// Keep status 7 as "in return process".
	$db_link->prepare('UPDATE `shop_orders_items_statuses_ref` SET `for_return` = 1 WHERE `id` = 7 AND `for_return` = 0')->execute();

	$completeId = (int) $db_link->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `complete_return` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
	$rejectId = (int) $db_link->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `reject_return` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();

	if ($completeId < 1) {
		$maxOrder = (int) $db_link->query('SELECT MAX(`order`) FROM `shop_orders_items_statuses_ref`')->fetchColumn();
		$nameKey = epc_returns_ensure_lang_string($db_link, 'epc_return_item_approved', 'Return approved');
		$db_link->prepare('INSERT INTO `shop_orders_items_statuses_ref` (`name`,`color`,`for_created`,`for_finish`,`order`,`count_flag`,`issue_flag`,`to_manager_email`,`to_manager_sms`,`to_customer_email`,`to_customer_sms`,`for_return`,`check_for_return`,`complete_return`,`reject_return`) VALUES (?,?,0,0,?,1,0,1,1,1,1,0,0,1,0)')
			->execute(array($nameKey, '#c8f7c5', $maxOrder + 1));
		$completeId = (int) $db_link->lastInsertId();
		$report[] = 'Created item status: Return approved';
	}

	if ($rejectId < 1) {
		$maxOrder = (int) $db_link->query('SELECT MAX(`order`) FROM `shop_orders_items_statuses_ref`')->fetchColumn();
		$nameKey = epc_returns_ensure_lang_string($db_link, 'epc_return_item_rejected', 'Return rejected');
		$db_link->prepare('INSERT INTO `shop_orders_items_statuses_ref` (`name`,`color`,`for_created`,`for_finish`,`order`,`count_flag`,`issue_flag`,`to_manager_email`,`to_manager_sms`,`to_customer_email`,`to_customer_sms`,`for_return`,`check_for_return`,`complete_return`,`reject_return`) VALUES (?,?,0,0,?,0,0,1,1,1,1,0,0,0,1)')
			->execute(array($nameKey, '#ffd0d0', $maxOrder + 1));
		$rejectId = (int) $db_link->lastInsertId();
		$report[] = 'Created item status: Return rejected';
	}

	// Ensure return request statuses exist with readable captions.
	$needStatuses = array(
		array('Created', '#dae1dd'),
		array('Under consideration', '#f5f3cc'),
		array('Closed', '#26ad5f'),
	);
	$countStatuses = (int) $db_link->query('SELECT COUNT(*) FROM `shop_orders_returns_statuses`')->fetchColumn();
	if ($countStatuses < 1) {
		foreach ($needStatuses as $st) {
			$key = epc_returns_ensure_lang_string($db_link, 'epc_ret_st_'.preg_replace('/[^a-z0-9]+/i', '_', strtolower($st[0])), $st[0]);
			$db_link->prepare('INSERT INTO `shop_orders_returns_statuses` (`color`,`caption`) VALUES (?,?)')->execute(array($st[1], $key));
		}
		$report[] = 'Seeded return request statuses';
	}

	$reasons = (int) $db_link->query('SELECT COUNT(*) FROM `shop_orders_returns_reasons`')->fetchColumn();
	if ($reasons < 1) {
		foreach (array('The product did not fit', 'Manufacturing defects') as $cap) {
			$key = epc_returns_ensure_lang_string($db_link, 'epc_ret_rs_'.preg_replace('/[^a-z0-9]+/i', '_', strtolower($cap)), $cap);
			$db_link->prepare('INSERT INTO `shop_orders_returns_reasons` (`caption`) VALUES (?)')->execute(array($key));
		}
		$report[] = 'Seeded return reasons';
	}

	return array(
		'check_status_id' => 5,
		'for_return_status_id' => 7,
		'complete_status_id' => $completeId,
		'reject_status_id' => $rejectId,
		'report' => $report,
	);
}

function epc_returns_ensure_lang_string(PDO $db_link, string $strKey, string $enValue): string
{
	$chk = $db_link->prepare('SELECT `str_key` FROM `lang_text_strings` WHERE `str_key` = ? LIMIT 1');
	$chk->execute(array($strKey));
	if (!$chk->fetchColumn()) {
		$db_link->prepare('INSERT INTO `lang_text_strings` (`str_key`,`description`,`same`,`is_error`,`is_custom`,`used_found`) VALUES (?,?,0,0,1,1)')
			->execute(array($strKey, $enValue));
	}
	$tr = $db_link->prepare('SELECT `id` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1');
	$tr->execute(array($strKey, 'en'));
	if (!$tr->fetchColumn()) {
		$db_link->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`,`lang_code`,`value`) VALUES (?,?,?)')
			->execute(array($strKey, 'en', $enValue));
	}
	return $strKey;
}

/** Resolve linked order id(s) for a return request. */
function epc_returns_order_ids(PDO $db_link, int $returnId): array
{
	$q = $db_link->prepare(
		'SELECT DISTINCT oi.`order_id`
		 FROM `shop_orders_returns_items` ri
		 INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
		 WHERE ri.`return_id` = ? AND oi.`order_id` > 0
		 ORDER BY oi.`order_id` ASC'
	);
	$q->execute(array($returnId));
	$ids = array();
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$ids[] = (int) $row['order_id'];
	}
	return $ids;
}

function epc_returns_primary_order_id(PDO $db_link, int $returnId): int
{
	$ids = epc_returns_order_ids($db_link, $returnId);
	return $ids ? $ids[0] : 0;
}

function epc_returns_status_id_by_caption_keys(PDO $db_link, array $keys): int
{
	foreach ($keys as $key) {
		$q = $db_link->prepare('SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = ? LIMIT 1');
		$q->execute(array($key));
		$id = (int) $q->fetchColumn();
		if ($id > 0) {
			return $id;
		}
	}
	// Fallback: first status
	return (int) $db_link->query('SELECT `id` FROM `shop_orders_returns_statuses` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
}

function epc_returns_open_status_id(PDO $db_link): int
{
	// Prefer Under consideration → Created → first
	return epc_returns_status_id_by_caption_keys($db_link, array('3806', '3796', 'epc_ret_st_under_consideration', 'epc_ret_st_created'));
}

function epc_returns_closed_status_id(PDO $db_link): int
{
	return epc_returns_status_id_by_caption_keys($db_link, array('3798', 'epc_ret_st_closed'));
}

function epc_returns_label(string $keyOrId): string
{
	if (function_exists('translate_str_by_id')) {
		$t = translate_str_by_id($keyOrId);
		if (is_string($t) && $t !== '' && strpos($t, 'ERROR STR_KEY') === false && $t !== '==Empty string==') {
			return $t;
		}
	}
	return (string) $keyOrId;
}
