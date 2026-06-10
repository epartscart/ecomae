<?php
/**
 * CRM phases 2–6 — quotes, tickets, projects, contracts, expenses.
 */
defined('_ASTEXE_') or die('No access');

function epc_crm_quote_statuses()
{
	return array('draft' => 'Draft', 'sent' => 'Sent', 'accepted' => 'Accepted', 'rejected' => 'Rejected');
}

function epc_crm_ticket_statuses()
{
	return array('open' => 'Open', 'pending' => 'Pending', 'resolved' => 'Resolved', 'closed' => 'Closed');
}

function epc_crm_ticket_priorities()
{
	return array('low' => 'Low', 'normal' => 'Normal', 'high' => 'High', 'urgent' => 'Urgent');
}

function epc_crm_project_statuses()
{
	return array('planned' => 'Planned', 'active' => 'Active', 'on_hold' => 'On hold', 'done' => 'Done', 'cancelled' => 'Cancelled');
}

function epc_crm_contract_statuses()
{
	return array('draft' => 'Draft', 'active' => 'Active', 'paused' => 'Paused', 'ended' => 'Ended');
}

function epc_crm_expense_statuses()
{
	return array('draft' => 'Draft', 'submitted' => 'Submitted', 'approved' => 'Approved', 'rejected' => 'Rejected', 'paid' => 'Paid');
}

function epc_crm_next_quote_number(PDO $db)
{
	$n = (int)$db->query('SELECT COUNT(*) FROM `epc_crm_quotes`')->fetchColumn() + 1;
	return 'Q-' . date('Ym') . '-' . str_pad((string)$n, 4, '0', STR_PAD_LEFT);
}

function epc_crm_recalc_quote_subtotal(PDO $db, $quoteId)
{
	$st = $db->prepare('SELECT IFNULL(SUM(`qty` * `unit_price`), 0) FROM `epc_crm_quote_lines` WHERE `quote_id` = ?');
	$st->execute(array((int)$quoteId));
	$sum = (float)$st->fetchColumn();
	$db->prepare('UPDATE `epc_crm_quotes` SET `subtotal` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($sum, time(), (int)$quoteId));
	return $sum;
}

function epc_crm_list_quotes(PDO $db, $limit = 100)
{
	epc_crm_ensure_schema($db);
	$st = $db->query(
		'SELECT q.*, o.`title` AS opp_title FROM `epc_crm_quotes` q
		 LEFT JOIN `epc_crm_opportunities` o ON o.`id` = q.`opportunity_id`
		 WHERE q.`active` = 1 ORDER BY q.`time_updated` DESC LIMIT ' . (int)$limit
	);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_get_quote(PDO $db, $id)
{
	$st = $db->prepare('SELECT * FROM `epc_crm_quotes` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array((int)$id));
	$q = $st->fetch(PDO::FETCH_ASSOC);
	if (!$q) {
		return null;
	}
	$ln = $db->prepare('SELECT * FROM `epc_crm_quote_lines` WHERE `quote_id` = ? ORDER BY `sort_order`, `id`');
	$ln->execute(array((int)$id));
	$q['lines'] = $ln->fetchAll(PDO::FETCH_ASSOC);
	return $q;
}

function epc_crm_save_quote(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$statuses = array_keys(epc_crm_quote_statuses());
	$status = in_array($data['status'] ?? '', $statuses, true) ? $data['status'] : 'draft';
	$oppId = (int)($data['opportunity_id'] ?? 0);
	$leadId = (int)($data['lead_id'] ?? 0);
	$custId = (int)($data['customer_user_id'] ?? 0);
	if ($oppId > 0) {
		$opp = epc_crm_get_opportunity($db, $oppId);
		if ($opp) {
			if ($leadId <= 0) {
				$leadId = (int)$opp['lead_id'];
			}
			if ($custId <= 0) {
				$custId = (int)$opp['linked_user_id'];
			}
		}
	}
	$num = trim((string)($data['quote_number'] ?? ''));
	if ($num === '' && (int)$id <= 0) {
		$num = epc_crm_next_quote_number($db);
	}
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_quotes` SET `opportunity_id`=?, `lead_id`=?, `customer_user_id`=?, `quote_number`=?, `status`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array($oppId, $leadId, $custId, mb_substr($num, 0, 32), $status, trim((string)($data['notes'] ?? '')), $now, (int)$id));
		$quoteId = (int)$id;
	} else {
		$db->prepare(
			'INSERT INTO `epc_crm_quotes` (`opportunity_id`, `lead_id`, `customer_user_id`, `quote_number`, `status`, `subtotal`, `notes`, `time_created`, `time_updated`)
			 VALUES (?, ?, ?, ?, ?, 0, ?, ?, ?)'
		)->execute(array($oppId, $leadId, $custId, mb_substr($num, 0, 32), $status, trim((string)($data['notes'] ?? '')), $now, $now));
		$quoteId = (int)$db->lastInsertId();
	}
	if (!empty($data['line_description'])) {
		$mx = $db->prepare('SELECT IFNULL(MAX(`sort_order`), 0) + 1 FROM `epc_crm_quote_lines` WHERE `quote_id` = ?');
		$mx->execute(array($quoteId));
		$sort = (int)$mx->fetchColumn();
		$db->prepare(
			'INSERT INTO `epc_crm_quote_lines` (`quote_id`, `description`, `qty`, `unit_price`, `sort_order`)
			 VALUES (?, ?, ?, ?, ?)'
		)->execute(array(
			$quoteId,
			mb_substr(trim((string)$data['line_description']), 0, 512),
			max(0.001, (float)($data['line_qty'] ?? 1)),
			max(0, (float)($data['line_unit_price'] ?? 0)),
			$sort,
		));
	}
	epc_crm_recalc_quote_subtotal($db, $quoteId);
	return $quoteId;
}

function epc_crm_accept_quote(PDO $db, $quoteId)
{
	$quote = epc_crm_get_quote($db, $quoteId);
	if (!$quote) {
		throw new Exception('Quote not found');
	}
	if ($quote['status'] === 'accepted' && (int)$quote['shop_order_id'] > 0) {
		return array('quote_id' => (int)$quoteId, 'order_id' => (int)$quote['shop_order_id']);
	}
	$userId = (int)$quote['customer_user_id'];
	if ($userId <= 0 && (int)$quote['opportunity_id'] > 0) {
		$opp = epc_crm_get_opportunity($db, (int)$quote['opportunity_id']);
		if ($opp) {
			$userId = (int)$opp['linked_user_id'];
			if ($userId <= 0 && (int)$opp['lead_id'] > 0) {
				$lead = epc_crm_get_lead($db, (int)$opp['lead_id']);
				if ($lead && $lead['email'] !== '') {
					$st = $db->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
					$st->execute(array($lead['email']));
					$userId = (int)$st->fetchColumn();
				}
			}
		}
	}
	$orderId = epc_crm_create_order_stub_from_quote($db, $quote, $userId);
	$db->prepare('UPDATE `epc_crm_quotes` SET `status` = \'accepted\', `shop_order_id` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($orderId, time(), (int)$quoteId));
	return array('quote_id' => (int)$quoteId, 'order_id' => $orderId);
}

function epc_crm_create_order_stub_from_quote(PDO $db, array $quote, $userId = 0)
{
	$statusQ = $db->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_created` = 1 ORDER BY `order` ASC LIMIT 1');
	$orderStatus = (int)$statusQ->fetchColumn();
	if ($orderStatus <= 0) {
		throw new Exception('No order status configured');
	}
	$itemStatus = (int)$db->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_created` = 1 ORDER BY `order` ASC LIMIT 1')->fetchColumn();
	$officeId = (int)$db->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
	$storageId = (int)$db->query('SELECT `id` FROM `shop_storages` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
	$db->prepare(
		'INSERT INTO `shop_orders`
		(`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`, `office_id`)
		VALUES (?, 0, ?, 1, ?, 0, 1, \'{}\', \'\', \'\', ?)'
	)->execute(array(max(0, $userId), time(), $orderStatus, $officeId));
	$orderId = (int)$db->lastInsertId();
	$lines = !empty($quote['lines']) ? $quote['lines'] : array();
	if (empty($lines)) {
		$lines = array(array('description' => 'Quote ' . $quote['quote_number'], 'qty' => 1, 'unit_price' => $quote['subtotal']));
	}
	$sort = 0;
	foreach ($lines as $ln) {
		$sort++;
		$price = (float)$ln['unit_price'];
		$qty = max(1, (float)$ln['qty']);
		$desc = mb_substr((string)$ln['description'], 0, 255);
		$article = 'CRM-Q' . (int)$quote['id'] . '-' . $sort;
		$db->prepare(
			'INSERT INTO `shop_orders_items`
			(`order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`,
			 `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`, `t2_time_to_exe`, `t2_time_to_exe_guaranteed`,
			 `t2_storage`, `t2_min_order`, `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`, `sao_state`, `sao_robot`, `t2_json_params`)
			VALUES (?, 2, ?, ?, 0, ?, \'CRM\', ?, ?, ?, 10, 1, 1, \'\', 1, 100, 0, 0, ?, ?, \'\', 0, \'\')'
		)->execute(array($orderId, $price, (int)$qty, $itemStatus, $article, $article, $desc, $officeId, $storageId));
		$itemId = (int)$db->lastInsertId();
		if ($storageId > 0) {
			$db->prepare(
				'INSERT INTO `shop_orders_items_details`
				(`order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`)
				VALUES (?, ?, ?, ?, 0, 0, 0, 0, 0)'
			)->execute(array($orderId, $itemId, $officeId, $storageId));
		}
	}
	$db->prepare('INSERT INTO `shop_orders_logs` (`order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (?, ?, 0, 1, ?, 1)')
		->execute(array($orderId, time(), 'CRM quote #' . $quote['quote_number'] . ' accepted — draft order stub.'));
	return $orderId;
}

function epc_crm_list_tickets(PDO $db, $limit = 100)
{
	epc_crm_ensure_schema($db);
	return $db->query(
		'SELECT t.*, u.`email` AS customer_email FROM `epc_crm_tickets` t
		 LEFT JOIN `users` u ON u.`user_id` = t.`customer_user_id`
		 WHERE t.`active` = 1 ORDER BY FIELD(t.`status`, \'open\', \'pending\', \'resolved\', \'closed\'), t.`time_updated` DESC LIMIT ' . (int)$limit
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_get_ticket(PDO $db, $id)
{
	$st = $db->prepare('SELECT * FROM `epc_crm_tickets` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array((int)$id));
	$t = $st->fetch(PDO::FETCH_ASSOC);
	if (!$t) {
		return null;
	}
	$ms = $db->prepare('SELECT * FROM `epc_crm_ticket_messages` WHERE `ticket_id` = ? ORDER BY `time_created` ASC');
	$ms->execute(array((int)$id));
	$t['messages'] = $ms->fetchAll(PDO::FETCH_ASSOC);
	return $t;
}

function epc_crm_save_ticket(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$status = isset(epc_crm_ticket_statuses()[$data['status'] ?? '']) ? $data['status'] : 'open';
	$priority = isset(epc_crm_ticket_priorities()[$data['priority'] ?? '']) ? $data['priority'] : 'normal';
	$row = array(
		(int)($data['customer_user_id'] ?? 0),
		(int)($data['order_id'] ?? 0),
		mb_substr(trim((string)($data['subject'] ?? 'Support request')), 0, 255),
		$status,
		$priority,
		(int)($data['assigned_user_id'] ?? epc_crm_admin_id()),
	);
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_tickets` SET `customer_user_id`=?, `order_id`=?, `subject`=?, `status`=?, `priority`=?, `assigned_user_id`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge($row, array($now, (int)$id)));
		$ticketId = (int)$id;
	} else {
		$db->prepare(
			'INSERT INTO `epc_crm_tickets` (`customer_user_id`, `order_id`, `subject`, `status`, `priority`, `assigned_user_id`, `time_created`, `time_updated`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
		)->execute(array_merge($row, array($now, $now)));
		$ticketId = (int)$db->lastInsertId();
	}
	$body = trim((string)($data['message'] ?? ''));
	if ($body !== '') {
		$db->prepare(
			'INSERT INTO `epc_crm_ticket_messages` (`ticket_id`, `author_user_id`, `is_staff`, `body`, `time_created`)
			 VALUES (?, ?, 1, ?, ?)'
		)->execute(array($ticketId, epc_crm_admin_id(), $body, $now));
	}
	return $ticketId;
}

function epc_crm_list_projects(PDO $db, $limit = 100)
{
	epc_crm_ensure_schema($db);
	return $db->query(
		'SELECT p.*, o.`title` AS opp_title FROM `epc_crm_projects` p
		 LEFT JOIN `epc_crm_opportunities` o ON o.`id` = p.`opportunity_id`
		 WHERE p.`active` = 1 ORDER BY p.`time_updated` DESC LIMIT ' . (int)$limit
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_get_project(PDO $db, $id)
{
	$st = $db->prepare('SELECT * FROM `epc_crm_projects` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array((int)$id));
	$p = $st->fetch(PDO::FETCH_ASSOC);
	if (!$p) {
		return null;
	}
	$ts = $db->prepare('SELECT * FROM `epc_crm_project_tasks` WHERE `project_id` = ? ORDER BY `sort_order`, `id`');
	$ts->execute(array((int)$id));
	$p['tasks'] = $ts->fetchAll(PDO::FETCH_ASSOC);
	return $p;
}

function epc_crm_save_project(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$status = isset(epc_crm_project_statuses()[$data['status'] ?? '']) ? $data['status'] : 'planned';
	$start = !empty($data['start_date']) ? strtotime((string)$data['start_date'] . ' 12:00:00') : 0;
	$end = !empty($data['end_date']) ? strtotime((string)$data['end_date'] . ' 12:00:00') : 0;
	$row = array(
		mb_substr(trim((string)($data['name'] ?? 'Project')), 0, 255),
		(int)($data['opportunity_id'] ?? 0),
		(int)($data['order_id'] ?? 0),
		$status,
		max(0, min(100, (int)($data['progress_pct'] ?? 0))),
		$start ?: 0,
		$end ?: 0,
		(int)($data['owner_user_id'] ?? epc_crm_admin_id()),
		trim((string)($data['notes'] ?? '')),
	);
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_projects` SET `name`=?, `opportunity_id`=?, `order_id`=?, `status`=?, `progress_pct`=?, `start_date`=?, `end_date`=?, `owner_user_id`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge($row, array($now, (int)$id)));
		return (int)$id;
	}
	$db->prepare(
		'INSERT INTO `epc_crm_projects` (`name`, `opportunity_id`, `order_id`, `status`, `progress_pct`, `start_date`, `end_date`, `owner_user_id`, `notes`, `time_created`, `time_updated`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array_merge($row, array($now, $now)));
	return (int)$db->lastInsertId();
}

function epc_crm_save_project_task(PDO $db, array $data)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$pid = (int)($data['project_id'] ?? 0);
	$status = in_array($data['status'] ?? '', array('todo', 'doing', 'done'), true) ? $data['status'] : 'todo';
	$mx = $db->prepare('SELECT IFNULL(MAX(`sort_order`), 0) + 1 FROM `epc_crm_project_tasks` WHERE `project_id` = ?');
	$mx->execute(array($pid));
	$sort = (int)$mx->fetchColumn();
	$db->prepare(
		'INSERT INTO `epc_crm_project_tasks` (`project_id`, `title`, `status`, `progress_pct`, `hours_est`, `due_date`, `sort_order`, `time_updated`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$pid,
		mb_substr(trim((string)($data['title'] ?? 'Task')), 0, 255),
		$status,
		max(0, min(100, (int)($data['progress_pct'] ?? 0))),
		max(0, (float)($data['hours_est'] ?? 0)),
		!empty($data['due_date']) ? strtotime((string)$data['due_date'] . ' 12:00:00') : 0,
		$sort,
		$now,
	));
	epc_crm_recalc_project_progress($db, $pid);
	return (int)$db->lastInsertId();
}

function epc_crm_recalc_project_progress(PDO $db, $projectId)
{
	$st = $db->prepare('SELECT AVG(`progress_pct`) FROM `epc_crm_project_tasks` WHERE `project_id` = ?');
	$st->execute(array((int)$projectId));
	$pct = (int)round((float)$st->fetchColumn());
	$db->prepare('UPDATE `epc_crm_projects` SET `progress_pct` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($pct, time(), (int)$projectId));
}

function epc_crm_list_contracts(PDO $db, $limit = 100)
{
	epc_crm_ensure_schema($db);
	return $db->query(
		'SELECT c.*, u.`email` AS customer_email FROM `epc_crm_contracts` c
		 LEFT JOIN `users` u ON u.`user_id` = c.`customer_user_id`
		 WHERE c.`active` = 1 ORDER BY c.`next_billing_date` ASC LIMIT ' . (int)$limit
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_save_contract(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$intervals = array('monthly', 'quarterly', 'yearly', 'once');
	$interval = in_array($data['billing_interval'] ?? '', $intervals, true) ? $data['billing_interval'] : 'monthly';
	$status = isset(epc_crm_contract_statuses()[$data['status'] ?? '']) ? $data['status'] : 'draft';
	$next = !empty($data['next_billing_date']) ? strtotime((string)$data['next_billing_date'] . ' 12:00:00') : strtotime('+1 month');
	$row = array(
		(int)($data['customer_user_id'] ?? 0),
		mb_substr(trim((string)($data['title'] ?? 'Contract')), 0, 255),
		max(0, (float)($data['amount'] ?? 0)),
		$interval,
		$next ?: 0,
		$status,
		trim((string)($data['notes'] ?? '')),
	);
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_contracts` SET `customer_user_id`=?, `title`=?, `amount`=?, `billing_interval`=?, `next_billing_date`=?, `status`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge($row, array($now, (int)$id)));
		return (int)$id;
	}
	$db->prepare(
		'INSERT INTO `epc_crm_contracts` (`customer_user_id`, `title`, `amount`, `billing_interval`, `next_billing_date`, `status`, `notes`, `time_created`, `time_updated`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array_merge($row, array($now, $now)));
	return (int)$db->lastInsertId();
}

function epc_crm_contracts_due_schedule(PDO $db, $daysAhead = 90)
{
	epc_crm_ensure_schema($db);
	$until = time() + (int)$daysAhead * 86400;
	$st = $db->prepare(
		'SELECT * FROM `epc_crm_contracts` WHERE `active` = 1 AND `status` = \'active\'
		 AND `next_billing_date` > 0 AND `next_billing_date` <= ? ORDER BY `next_billing_date` ASC'
	);
	$st->execute(array($until));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_list_expenses(PDO $db, $limit = 100)
{
	epc_crm_ensure_schema($db);
	return $db->query(
		'SELECT * FROM `epc_crm_expenses` WHERE `active` = 1 ORDER BY `time_updated` DESC LIMIT ' . (int)$limit
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_save_expense(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$status = isset(epc_crm_expense_statuses()[$data['status'] ?? '']) ? $data['status'] : 'draft';
	$row = array(
		(int)($data['employee_user_id'] ?? epc_crm_admin_id()),
		max(0, (float)($data['amount'] ?? 0)),
		mb_substr(trim((string)($data['category'] ?? 'travel')), 0, 64),
		$status,
		mb_substr(trim((string)($data['receipt_note'] ?? '')), 0, 512),
	);
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_expenses` SET `employee_user_id`=?, `amount`=?, `category`=?, `status`=?, `receipt_note`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge($row, array($now, (int)$id)));
		return (int)$id;
	}
	$db->prepare(
		'INSERT INTO `epc_crm_expenses` (`employee_user_id`, `amount`, `category`, `status`, `receipt_note`, `time_created`, `time_updated`)
		 VALUES (?, ?, ?, ?, ?, ?, ?)'
	)->execute(array_merge($row, array($now, $now)));
	return (int)$db->lastInsertId();
}

function epc_crm_approve_expense(PDO $db, $expenseId, $postToCash = true)
{
	$st = $db->prepare('SELECT * FROM `epc_crm_expenses` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array((int)$expenseId));
	$exp = $st->fetch(PDO::FETCH_ASSOC);
	if (!$exp) {
		throw new Exception('Expense not found');
	}
	$cashId = 0;
	if ($postToCash && (float)$exp['amount'] > 0) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
		epc_erp_full_ensure_schema($db);
		$acctId = (int)$db->query('SELECT `id` FROM `epc_erp_cash_accounts` WHERE `active` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
		if ($acctId > 0) {
			$cashId = epc_erp_cash_entry($db, array(
				'cash_account_id' => $acctId,
				'direction' => 0,
				'amount' => (float)$exp['amount'],
				'entry_type' => 'expense',
				'reference' => 'CRM-EXP-' . (int)$expenseId,
				'note' => 'Expense: ' . $exp['category'] . ' — ' . $exp['receipt_note'],
			));
		}
	}
	$db->prepare('UPDATE `epc_crm_expenses` SET `status` = \'approved\', `cash_entry_id` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($cashId, time(), (int)$expenseId));
	return array('expense_id' => (int)$expenseId, 'cash_entry_id' => $cashId);
}

function epc_crm_dashboard_extended(PDO $db)
{
	$base = epc_crm_dashboard($db);
	epc_crm_ensure_schema($db);
	$base['quotes_open'] = (int)$db->query("SELECT COUNT(*) FROM `epc_crm_quotes` WHERE `active` = 1 AND `status` IN ('draft','sent')")->fetchColumn();
	$base['tickets_open'] = (int)$db->query("SELECT COUNT(*) FROM `epc_crm_tickets` WHERE `active` = 1 AND `status` IN ('open','pending')")->fetchColumn();
	$base['projects_active'] = (int)$db->query("SELECT COUNT(*) FROM `epc_crm_projects` WHERE `active` = 1 AND `status` = 'active'")->fetchColumn();
	$st = $db->prepare("SELECT COUNT(*) FROM `epc_crm_contracts` WHERE `active` = 1 AND `status` = 'active' AND `next_billing_date` <= ?");
	$st->execute(array(time() + 86400 * 30));
	$base['contracts_due_30d'] = (int)$st->fetchColumn();
	$base['expenses_pending'] = (int)$db->query("SELECT COUNT(*) FROM `epc_crm_expenses` WHERE `active` = 1 AND `status` IN ('submitted','approved')")->fetchColumn();
	return $base;
}

function epc_crm_quote_pdf_path(PDO $db, $quoteId)
{
	$quote = epc_crm_get_quote($db, (int)$quoteId);
	if (!$quote) {
		return '';
	}
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_crm_quotes';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	$file = $dir . '/quote_' . preg_replace('/[^a-zA-Z0-9_-]/', '_', $quote['quote_number']) . '.html';
	$linesHtml = '';
	foreach ($quote['lines'] as $ln) {
		$lineTotal = round((float)$ln['qty'] * (float)$ln['unit_price'], 2);
		$linesHtml .= '<tr><td>' . htmlspecialchars($ln['description']) . '</td><td>' . epc_crm_money($ln['qty']) . '</td>';
		$linesHtml .= '<td>' . epc_crm_money($ln['unit_price']) . '</td><td>' . epc_crm_money($lineTotal) . '</td></tr>';
	}
	$html = '<!DOCTYPE html><html><head><meta charset="utf-8"><title>Quote ' . htmlspecialchars($quote['quote_number']) . '</title>
	<style>body{font-family:Arial,sans-serif;margin:40px;color:#0f172a;} h1{font-size:22px;} table{border-collapse:collapse;width:100%;margin:16px 0;}
	th,td{border:1px solid #cbd5e1;padding:8px;text-align:left;} th{background:#f1f5f9;}</style></head><body>';
	$html .= '<h1>Commercial proposal ' . htmlspecialchars($quote['quote_number']) . '</h1>';
	$html .= '<p><strong>Status:</strong> ' . htmlspecialchars($quote['status']) . '<br>';
	$html .= '<strong>Total (ex VAT):</strong> ' . epc_crm_money($quote['subtotal']) . ' AED</p>';
	if (!empty($quote['notes'])) {
		$html .= '<p>' . nl2br(htmlspecialchars($quote['notes'])) . '</p>';
	}
	$html .= '<table><thead><tr><th>Description</th><th>Qty</th><th>Unit AED</th><th>Line total</th></tr></thead><tbody>' . $linesHtml . '</tbody></table>';
	$html .= '<p style="margin-top:32px;font-size:12px;color:#64748b;">Generated ' . date('Y-m-d H:i') . ' — ECOM AE ERP CRM</p></body></html>';
	file_put_contents($file, $html);
	return '/content/files/epc_crm_quotes/' . basename($file);
}

function epc_crm_quote_email_stub(PDO $db, $quoteId, $toEmail = '')
{
	$quote = epc_crm_get_quote($db, (int)$quoteId);
	if (!$quote) {
		throw new Exception('Quote not found');
	}
	$pdf = epc_crm_quote_pdf_path($db, (int)$quoteId);
	$to = trim((string)$toEmail);
	if ($to === '' && (int)$quote['customer_user_id'] > 0) {
		$st = $db->prepare('SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1');
		$st->execute(array((int)$quote['customer_user_id']));
		$to = (string)$st->fetchColumn();
	}
	$logDir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_crm_quote_emails';
	if (!is_dir($logDir)) {
		@mkdir($logDir, 0755, true);
	}
	$stub = array(
		'to' => $to ?: 'customer@example.com',
		'subject' => 'Proposal ' . $quote['quote_number'],
		'body' => 'Please find your commercial proposal attached.',
		'attachment' => $pdf,
		'time' => date('c'),
	);
	file_put_contents($logDir . '/stub_' . (int)$quoteId . '_' . time() . '.json', json_encode($stub, JSON_PRETTY_PRINT));
	if ($quote['status'] === 'draft') {
		$db->prepare('UPDATE `epc_crm_quotes` SET `status` = \'sent\', `time_updated` = ? WHERE `id` = ?')->execute(array(time(), (int)$quoteId));
	}
	return array('queued' => true, 'to' => $stub['to'], 'preview_path' => $pdf);
}
