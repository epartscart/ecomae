<?php
/**
 * Native CRM — helpers (CRUD, pipeline, conversions).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_crm_schema.php';
require_once __DIR__ . '/epc_crm_modules.php';

function epc_crm_h($s)
{
	return htmlspecialchars((string)$s, ENT_QUOTES, 'UTF-8');
}

function epc_crm_money($n)
{
	return number_format((float)$n, 2, '.', ',');
}

function epc_crm_lead_statuses()
{
	return array(
		'new' => 'New',
		'contacted' => 'Contacted',
		'qualified' => 'Qualified',
		'unqualified' => 'Unqualified',
		'converted' => 'Converted',
	);
}

function epc_crm_opportunity_stages()
{
	return array(
		'prospect' => 'Prospect',
		'qualified' => 'Qualified',
		'proposal' => 'Proposal',
		'negotiation' => 'Negotiation',
		'won' => 'Won',
		'lost' => 'Lost',
	);
}

function epc_crm_activity_types()
{
	return array(
		'call' => 'Call',
		'email' => 'Email',
		'meeting' => 'Meeting',
		'note' => 'Note',
		'task' => 'Task',
	);
}

function epc_crm_admin_id()
{
	if (class_exists('DP_User')) {
		$aid = (int)DP_User::getAdminId();
		if ($aid > 0) {
			return $aid;
		}
		return (int)DP_User::getUserId();
	}
	return 0;
}

function epc_crm_list_leads(PDO $db, $status = '', $limit = 200)
{
	epc_crm_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_crm_leads` WHERE `active` = 1';
	$params = array();
	if ($status !== '') {
		$sql .= ' AND `status` = ?';
		$params[] = $status;
	}
	$sql .= ' ORDER BY `time_updated` DESC, `id` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_get_lead(PDO $db, $id)
{
	$st = $db->prepare('SELECT * FROM `epc_crm_leads` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array((int)$id));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_crm_save_lead(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$statuses = array_keys(epc_crm_lead_statuses());
	$status = in_array($data['status'] ?? '', $statuses, true) ? $data['status'] : 'new';
	$row = array(
		mb_substr(trim((string)($data['company'] ?? '')), 0, 255),
		mb_substr(trim((string)($data['contact_name'] ?? '')), 0, 255),
		mb_substr(trim((string)($data['email'] ?? '')), 0, 255),
		mb_substr(trim((string)($data['phone'] ?? '')), 0, 64),
		mb_substr(trim((string)($data['source'] ?? 'web')), 0, 64),
		$status,
		(int)($data['owner_user_id'] ?? epc_crm_admin_id()),
		max(0, (float)($data['expected_value'] ?? 0)),
		trim((string)($data['notes'] ?? '')),
	);
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_leads` SET `company`=?, `contact_name`=?, `email`=?, `phone`=?, `source`=?, `status`=?, `owner_user_id`=?, `expected_value`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge($row, array($now, (int)$id)));
		return (int)$id;
	}
	$db->prepare(
		'INSERT INTO `epc_crm_leads`
		(`company`, `contact_name`, `email`, `phone`, `source`, `status`, `owner_user_id`, `expected_value`, `notes`, `time_created`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array_merge($row, array($now, $now)));
	return (int)$db->lastInsertId();
}

function epc_crm_delete_lead(PDO $db, $id)
{
	$db->prepare('UPDATE `epc_crm_leads` SET `active` = 0, `time_updated` = ? WHERE `id` = ?')->execute(array(time(), (int)$id));
}

function epc_crm_list_opportunities(PDO $db, $stage = '', $limit = 200)
{
	epc_crm_ensure_schema($db);
	$sql = 'SELECT o.*, l.`company` AS lead_company
		FROM `epc_crm_opportunities` o
		LEFT JOIN `epc_crm_leads` l ON l.`id` = o.`lead_id`
		WHERE o.`active` = 1';
	$params = array();
	if ($stage !== '') {
		$sql .= ' AND o.`stage` = ?';
		$params[] = $stage;
	}
	$sql .= ' ORDER BY FIELD(o.`stage`, \'negotiation\', \'proposal\', \'qualified\', \'prospect\', \'won\', \'lost\'), o.`close_date` ASC, o.`id` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_get_opportunity(PDO $db, $id)
{
	$st = $db->prepare(
		'SELECT o.*, l.`company` AS lead_company FROM `epc_crm_opportunities` o
		 LEFT JOIN `epc_crm_leads` l ON l.`id` = o.`lead_id`
		 WHERE o.`id` = ? AND o.`active` = 1 LIMIT 1'
	);
	$st->execute(array((int)$id));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_crm_save_opportunity(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$stages = array_keys(epc_crm_opportunity_stages());
	$stage = in_array($data['stage'] ?? '', $stages, true) ? $data['stage'] : 'prospect';
	$prob = max(0, min(100, (int)($data['probability'] ?? 10)));
	$close = !empty($data['close_date']) ? strtotime((string)$data['close_date'] . ' 12:00:00') : 0;
	if ($close === false) {
		$close = 0;
	}
	$row = array(
		(int)($data['lead_id'] ?? 0),
		mb_substr(trim((string)($data['title'] ?? 'Opportunity')), 0, 255),
		$stage,
		max(0, (float)($data['amount'] ?? 0)),
		$prob,
		$close,
		(int)($data['owner_user_id'] ?? epc_crm_admin_id()),
		(int)($data['linked_user_id'] ?? 0),
		trim((string)($data['notes'] ?? '')),
	);
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_opportunities` SET `lead_id`=?, `title`=?, `stage`=?, `amount`=?, `probability`=?, `close_date`=?, `owner_user_id`=?, `linked_user_id`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge($row, array($now, (int)$id)));
		return (int)$id;
	}
	$db->prepare(
		'INSERT INTO `epc_crm_opportunities`
		(`lead_id`, `title`, `stage`, `amount`, `probability`, `close_date`, `owner_user_id`, `linked_user_id`, `notes`, `time_created`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array_merge($row, array($now, $now)));
	return (int)$db->lastInsertId();
}

function epc_crm_update_opportunity_stage(PDO $db, $id, $stage)
{
	if (!isset(epc_crm_opportunity_stages()[$stage])) {
		throw new Exception('Invalid stage');
	}
	$db->prepare('UPDATE `epc_crm_opportunities` SET `stage` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($stage, time(), (int)$id));
}

function epc_crm_list_activities(PDO $db, $relatedType = '', $relatedId = 0, $limit = 100)
{
	epc_crm_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_crm_activities` WHERE `active` = 1';
	$params = array();
	if ($relatedType !== '' && $relatedId > 0) {
		$sql .= ' AND `related_type` = ? AND `related_id` = ?';
		$params[] = $relatedType;
		$params[] = (int)$relatedId;
	}
	$sql .= ' ORDER BY `done` ASC, `due_date` ASC, `id` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_save_activity(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$types = array_keys(epc_crm_activity_types());
	$relTypes = array('lead', 'opportunity', 'user');
	$type = in_array($data['activity_type'] ?? '', $types, true) ? $data['activity_type'] : 'task';
	$relType = in_array($data['related_type'] ?? '', $relTypes, true) ? $data['related_type'] : 'lead';
	$due = !empty($data['due_date']) ? strtotime((string)$data['due_date'] . ' 12:00:00') : time();
	if ($due === false) {
		$due = time();
	}
	$now = time();
	$row = array(
		$type,
		$relType,
		(int)($data['related_id'] ?? 0),
		$due,
		!empty($data['done']) ? 1 : 0,
		(int)($data['owner_user_id'] ?? epc_crm_admin_id()),
		trim((string)($data['notes'] ?? '')),
	);
	if ((int)$id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_activities` SET `activity_type`=?, `related_type`=?, `related_id`=?, `due_date`=?, `done`=?, `owner_user_id`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge($row, array($now, (int)$id)));
		return (int)$id;
	}
	$db->prepare(
		'INSERT INTO `epc_crm_activities`
		(`activity_type`, `related_type`, `related_id`, `due_date`, `done`, `owner_user_id`, `notes`, `time_created`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array_merge($row, array($now, $now)));
	return (int)$db->lastInsertId();
}

function epc_crm_toggle_activity_done(PDO $db, $id, $done)
{
	$db->prepare('UPDATE `epc_crm_activities` SET `done` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($done ? 1 : 0, time(), (int)$id));
}

function epc_crm_pipeline_board(PDO $db)
{
	epc_crm_ensure_schema($db);
	$board = array();
	foreach (array_keys(epc_crm_opportunity_stages()) as $stage) {
		$board[$stage] = array();
	}
	$rows = epc_crm_list_opportunities($db, '', 500);
	foreach ($rows as $r) {
		$st = $r['stage'];
		if (!isset($board[$st])) {
			$board[$st] = array();
		}
		$board[$st][] = $r;
	}
	return $board;
}

function epc_crm_dashboard(PDO $db)
{
	epc_crm_ensure_schema($db);
	$openStages = array('prospect', 'qualified', 'proposal', 'negotiation');
	$in = "'" . implode("','", $openStages) . "'";
	$pipelineValue = (float)$db->query(
		"SELECT IFNULL(SUM(`amount` * `probability` / 100), 0) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` IN ({$in})"
	)->fetchColumn();
	$wonMonth = (float)$db->query(
		"SELECT IFNULL(SUM(`amount`), 0) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` = 'won' AND `time_updated` >= " . (int)strtotime(date('Y-m-01 00:00:00'))
	)->fetchColumn();
	$byStage = array();
	$st = $db->query(
		"SELECT `stage`, COUNT(*) AS cnt, SUM(`amount`) AS total FROM `epc_crm_opportunities` WHERE `active` = 1 GROUP BY `stage`"
	);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$byStage[$r['stage']] = array('count' => (int)$r['cnt'], 'total' => (float)$r['total']);
	}
	return array(
		'leads_total' => (int)$db->query("SELECT COUNT(*) FROM `epc_crm_leads` WHERE `active` = 1")->fetchColumn(),
		'leads_new' => (int)$db->query("SELECT COUNT(*) FROM `epc_crm_leads` WHERE `active` = 1 AND `status` = 'new'")->fetchColumn(),
		'opportunities_open' => (int)$db->query("SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` IN ({$in})")->fetchColumn(),
		'pipeline_weighted' => $pipelineValue,
		'won_mtd' => $wonMonth,
		'activities_due' => (int)$db->query("SELECT COUNT(*) FROM `epc_crm_activities` WHERE `active` = 1 AND `done` = 0 AND `due_date` <= " . (time() + 86400 * 7))->fetchColumn(),
		'by_stage' => $byStage,
	);
}

function epc_crm_convert_lead_to_opportunity(PDO $db, $leadId, array $extra = array())
{
	$lead = epc_crm_get_lead($db, $leadId);
	if (!$lead) {
		throw new Exception('Lead not found');
	}
	$title = isset($extra['title']) ? $extra['title'] : ('Opportunity — ' . $lead['company']);
	$amount = isset($extra['amount']) ? (float)$extra['amount'] : (float)$lead['expected_value'];
	$oppId = epc_crm_save_opportunity($db, array(
		'lead_id' => (int)$leadId,
		'title' => $title,
		'stage' => isset($extra['stage']) ? $extra['stage'] : 'qualified',
		'amount' => $amount,
		'probability' => isset($extra['probability']) ? $extra['probability'] : 30,
		'close_date' => isset($extra['close_date']) ? $extra['close_date'] : date('Y-m-d', time() + 86400 * 30),
		'notes' => isset($extra['notes']) ? $extra['notes'] : 'Converted from lead #' . (int)$leadId,
	));
	$db->prepare('UPDATE `epc_crm_leads` SET `status` = \'converted\', `time_updated` = ? WHERE `id` = ?')
		->execute(array(time(), (int)$leadId));
	return array('opportunity_id' => $oppId, 'lead_id' => (int)$leadId);
}

function epc_crm_opportunity_won_order_hint(PDO $db, $opportunityId)
{
	$opp = epc_crm_get_opportunity($db, $opportunityId);
	if (!$opp || $opp['stage'] !== 'won') {
		return array('hint' => '', 'linked_user_id' => 0);
	}
	$uid = (int)$opp['linked_user_id'];
	$hint = 'Create a shop order for this customer';
	if ($uid > 0) {
		$hint .= ' (user #' . $uid . ')';
	} elseif ((int)$opp['lead_id'] > 0) {
		$lead = epc_crm_get_lead($db, (int)$opp['lead_id']);
		if ($lead && $lead['email'] !== '') {
			$st = $db->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
			$st->execute(array($lead['email']));
			$uid = (int)$st->fetchColumn();
			if ($uid > 0) {
				$hint .= ' — match found: user #' . $uid;
			} else {
				$hint .= ' — register customer with email ' . $lead['email'];
			}
		}
	}
	$hint .= '. Amount: ' . epc_crm_money($opp['amount']) . ' AED.';
	return array(
		'hint' => $hint,
		'linked_user_id' => $uid,
		'amount' => (float)$opp['amount'],
		'title' => $opp['title'],
	);
}

function epc_crm_handle_ajax_action(PDO $db, $action, array $post)
{
	if (!epc_crm_pack_enabled() || !epc_crm_user_can_access($db)) {
		throw new Exception('Access denied');
	}
	epc_crm_ensure_schema($db);
	switch ($action) {
		case 'crm_save_lead':
		case 'save_lead':
			return array('id' => epc_crm_save_lead($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Lead saved');
		case 'crm_delete_lead':
		case 'delete_lead':
			epc_crm_delete_lead($db, (int)($post['id'] ?? 0));
			return array('message' => 'Lead removed');
		case 'crm_save_opportunity':
		case 'save_opportunity':
			return array('id' => epc_crm_save_opportunity($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Opportunity saved');
		case 'crm_update_stage':
		case 'update_stage':
			epc_crm_update_opportunity_stage($db, (int)($post['id'] ?? 0), (string)($post['stage'] ?? ''));
			return array('message' => 'Stage updated');
		case 'crm_convert_lead':
		case 'convert_lead':
			$r = epc_crm_convert_lead_to_opportunity($db, (int)($post['lead_id'] ?? 0), $post);
			return array_merge($r, array('message' => 'Lead converted to opportunity #' . (int)$r['opportunity_id']));
		case 'crm_won_hint':
		case 'won_hint':
			$h = epc_crm_opportunity_won_order_hint($db, (int)($post['opportunity_id'] ?? 0));
			return array_merge($h, array('message' => $h['hint']));
		case 'crm_save_activity':
		case 'save_activity':
			return array('id' => epc_crm_save_activity($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Activity saved');
		case 'crm_toggle_activity':
		case 'toggle_activity':
			epc_crm_toggle_activity_done($db, (int)($post['id'] ?? 0), !empty($post['done']));
			return array('message' => 'Activity updated');
		case 'crm_dashboard':
		case 'dashboard':
			return array('data' => epc_crm_dashboard_extended($db), 'message' => 'OK');
		case 'crm_pipeline':
		case 'pipeline':
			return array('board' => epc_crm_pipeline_board($db), 'message' => 'OK');
		case 'crm_save_quote':
			return array('id' => epc_crm_save_quote($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Quote saved');
		case 'crm_accept_quote':
			$r = epc_crm_accept_quote($db, (int)($post['quote_id'] ?? 0));
			return array_merge($r, array('message' => 'Quote accepted — order #' . (int)$r['order_id']));
		case 'crm_quote_preview':
			require_once __DIR__ . '/epc_crm_modules.php';
			$path = epc_crm_quote_pdf_path($db, (int)($post['quote_id'] ?? 0));
			if ($path === '') {
				throw new Exception('Could not build preview');
			}
			return array('preview_url' => $path, 'message' => 'Preview ready');
		case 'crm_quote_email':
			require_once __DIR__ . '/epc_crm_modules.php';
			$r = epc_crm_quote_email_stub($db, (int)($post['quote_id'] ?? 0), (string)($post['email'] ?? ''));
			return array_merge($r, array('message' => 'Email queued to ' . $r['to'] . ' (stub log saved)'));
		case 'crm_save_ticket':
			return array('id' => epc_crm_save_ticket($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Ticket saved');
		case 'crm_save_project':
			return array('id' => epc_crm_save_project($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Project saved');
		case 'crm_save_project_task':
			return array('id' => epc_crm_save_project_task($db, $post), 'message' => 'Task added');
		case 'crm_save_contract':
			return array('id' => epc_crm_save_contract($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Contract saved');
		case 'crm_save_expense':
			return array('id' => epc_crm_save_expense($db, $post, (int)($post['id'] ?? 0)), 'message' => 'Expense saved');
		case 'crm_approve_expense':
			$r = epc_crm_approve_expense($db, (int)($post['expense_id'] ?? 0), !empty($post['post_cash']));
			return array_merge($r, array('message' => 'Expense approved' . ($r['cash_entry_id'] ? ' (cash entry #' . $r['cash_entry_id'] . ')' : '')));
		default:
			throw new Exception('Unknown CRM action');
	}
}

function epc_crm_configure_urls($embedInErp = null)
{
	global $DP_Config;
	if ($embedInErp === null) {
		$embedInErp = !empty($GLOBALS['epc_crm_embed_in_erp']);
	}
	$backend = '/' . (isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp');
	$from = isset($_GET['from']) ? (string) $_GET['from'] : date('Y-m-01');
	$to = isset($_GET['to']) ? (string) $_GET['to'] : date('Y-m-d');
	$erpBase = $backend . '/shop/finance/erp';
	$ordersUrl = $backend . '/shop/orders/orders';
	$crmAjax = $embedInErp
		? $backend . '/content/shop/finance/erp/ajax_erp_endpoint.php'
		: $backend . '/content/shop/crm/ajax_crm_endpoint.php';

	// Keep CRM links + AJAX inside whichever ERP door the user opened. ERP-only
	// tenants run on the standalone /erp/ door (portal=frontend) and must never
	// be bounced to the /cp control panel; full tenants stay on /cp. Hardcoding
	// the CP backend here is what dropped ERP-only users out to the control panel.
	$portal = (isset($GLOBALS['epc_erp_portal']) && $GLOBALS['epc_erp_portal'] === 'frontend') ? 'frontend' : 'cp';
	if ($portal === 'frontend' && function_exists('epc_erp_configure_portal_urls')) {
		$resolved = epc_erp_configure_portal_urls('frontend');
		if (!empty($resolved['erpUrl'])) {
			$erpBase = (string) $resolved['erpUrl'];
		}
		if (!empty($resolved['erpAjaxEndpoint'])) {
			$crmAjax = (string) $resolved['erpAjaxEndpoint'];
		}
		$ordersUrl = isset($resolved['ordersUrl']) ? (string) $resolved['ordersUrl'] : '';
	}

	if ($embedInErp) {
		$sep = (strpos($erpBase, '?') !== false) ? '&' : '?';
		$crmUrl = $erpBase . $sep . 'tab=crm&from=' . rawurlencode($from) . '&to=' . rawurlencode($to);
	} else {
		$sep = (strpos($erpBase, '?') !== false) ? '&' : '?';
		$crmUrl = $erpBase . $sep . 'tab=crm';
	}

	return array(
		'crmUrl' => $crmUrl,
		'crmAjax' => $crmAjax,
		'erpUrl' => $erpBase,
		'ordersUrl' => $ordersUrl,
	);
}
