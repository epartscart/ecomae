<?php
/**
 * CRM module — helpers (leads, opportunities, activities, pipeline).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_crm_schema.php';

function epc_crm_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_crm_stages()
{
	return array('prospect', 'qualified', 'proposal', 'negotiation', 'won', 'lost');
}

function epc_crm_lead_statuses()
{
	return array('new', 'contacted', 'qualified', 'disqualified', 'converted');
}

function epc_crm_activity_types()
{
	return array('call', 'email', 'meeting', 'note', 'task');
}

function epc_crm_admin_id()
{
	return class_exists('DP_User') ? (int) DP_User::getAdminId() : 0;
}

function epc_crm_dashboard(PDO $db)
{
	epc_crm_ensure_schema($db);
	$leads = (int) $db->query("SELECT COUNT(*) FROM `epc_crm_leads`")->fetchColumn();
	$openLeads = (int) $db->query("SELECT COUNT(*) FROM `epc_crm_leads` WHERE `status` NOT IN ('disqualified','converted')")->fetchColumn();
	$opps = (int) $db->query("SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `stage` NOT IN ('won','lost')")->fetchColumn();
	$won = (int) $db->query("SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `stage` = 'won'")->fetchColumn();
	$pipeline = (float) $db->query("SELECT COALESCE(SUM(`amount`),0) FROM `epc_crm_opportunities` WHERE `stage` NOT IN ('won','lost')")->fetchColumn();
	$wonVal = (float) $db->query("SELECT COALESCE(SUM(`amount`),0) FROM `epc_crm_opportunities` WHERE `stage` = 'won'")->fetchColumn();
	$due = (int) $db->query("SELECT COUNT(*) FROM `epc_crm_activities` WHERE `done` = 0 AND `due_date` > 0 AND `due_date` <= " . (int) (time() + 86400 * 7))->fetchColumn();
	return array(
		'leads_total' => $leads,
		'leads_open' => $openLeads,
		'opportunities_open' => $opps,
		'opportunities_won' => $won,
		'pipeline_value' => $pipeline,
		'won_value' => $wonVal,
		'activities_due_week' => $due,
	);
}

function epc_crm_list_leads(PDO $db, $status = '')
{
	epc_crm_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_crm_leads` WHERE 1=1';
	$params = array();
	if ($status !== '') {
		$sql .= ' AND `status` = ?';
		$params[] = $status;
	}
	$sql .= ' ORDER BY `time_updated` DESC, `id` DESC LIMIT 200';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_get_lead(PDO $db, $id)
{
	$st = $db->prepare('SELECT * FROM `epc_crm_leads` WHERE `id` = ? LIMIT 1');
	$st->execute(array((int) $id));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_crm_save_lead(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$fields = array(
		'company' => trim((string) ($data['company'] ?? '')),
		'contact_name' => trim((string) ($data['contact_name'] ?? '')),
		'email' => trim((string) ($data['email'] ?? '')),
		'phone' => trim((string) ($data['phone'] ?? '')),
		'source' => trim((string) ($data['source'] ?? 'web')),
		'status' => in_array($data['status'] ?? '', epc_crm_lead_statuses(), true) ? $data['status'] : 'new',
		'owner_user_id' => (int) ($data['owner_user_id'] ?? epc_crm_admin_id()),
		'expected_value' => (float) ($data['expected_value'] ?? 0),
		'notes' => trim((string) ($data['notes'] ?? '')),
		'time_updated' => $now,
	);
	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_crm_leads` SET `company`=?, `contact_name`=?, `email`=?, `phone`=?, `source`=?, `status`=?, `owner_user_id`=?, `expected_value`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array(
			$fields['company'], $fields['contact_name'], $fields['email'], $fields['phone'],
			$fields['source'], $fields['status'], $fields['owner_user_id'], $fields['expected_value'],
			$fields['notes'], $fields['time_updated'], (int) $id,
		));
		return (int) $id;
	}
	$fields['time_created'] = $now;
	$db->prepare(
		'INSERT INTO `epc_crm_leads` (`company`,`contact_name`,`email`,`phone`,`source`,`status`,`owner_user_id`,`expected_value`,`notes`,`time_created`,`time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$fields['company'], $fields['contact_name'], $fields['email'], $fields['phone'],
		$fields['source'], $fields['status'], $fields['owner_user_id'], $fields['expected_value'],
		$fields['notes'], $fields['time_created'], $fields['time_updated'],
	));
	return (int) $db->lastInsertId();
}

function epc_crm_list_opportunities(PDO $db, $stage = '')
{
	epc_crm_ensure_schema($db);
	$sql = 'SELECT o.*, l.`company` AS lead_company FROM `epc_crm_opportunities` o
		LEFT JOIN `epc_crm_leads` l ON l.`id` = o.`lead_id` WHERE 1=1';
	$params = array();
	if ($stage !== '') {
		$sql .= ' AND o.`stage` = ?';
		$params[] = $stage;
	}
	$sql .= ' ORDER BY o.`time_updated` DESC LIMIT 300';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_pipeline_board(PDO $db)
{
	$board = array();
	foreach (epc_crm_stages() as $stage) {
		$board[$stage] = array();
	}
	foreach (epc_crm_list_opportunities($db) as $row) {
		$s = $row['stage'];
		if (!isset($board[$s])) {
			$board[$s] = array();
		}
		$board[$s][] = $row;
	}
	return $board;
}

function epc_crm_save_opportunity(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$now = time();
	$stage = in_array($data['stage'] ?? '', epc_crm_stages(), true) ? $data['stage'] : 'prospect';
	$close = !empty($data['close_date']) ? strtotime((string) $data['close_date']) : 0;
	$params = array(
		(int) ($data['lead_id'] ?? 0),
		trim((string) ($data['title'] ?? 'Opportunity')),
		$stage,
		(float) ($data['amount'] ?? 0),
		max(0, min(100, (int) ($data['probability'] ?? 10))),
		$close ?: 0,
		(int) ($data['owner_user_id'] ?? epc_crm_admin_id()),
		(int) ($data['linked_user_id'] ?? 0),
		trim((string) ($data['notes'] ?? '')),
		$now,
	);
	if ($id > 0) {
		$params[] = (int) $id;
		$db->prepare(
			'UPDATE `epc_crm_opportunities` SET `lead_id`=?, `title`=?, `stage`=?, `amount`=?, `probability`=?, `close_date`=?, `owner_user_id`=?, `linked_user_id`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute($params);
		return (int) $id;
	}
	$params[] = $now;
	$db->prepare(
		'INSERT INTO `epc_crm_opportunities` (`lead_id`,`title`,`stage`,`amount`,`probability`,`close_date`,`owner_user_id`,`linked_user_id`,`notes`,`time_created`,`time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	)->execute($params);
	return (int) $db->lastInsertId();
}

function epc_crm_convert_lead_to_opportunity(PDO $db, $leadId)
{
	$lead = epc_crm_get_lead($db, $leadId);
	if (!$lead) {
		throw new Exception('Lead not found');
	}
	$oppId = epc_crm_save_opportunity($db, array(
		'lead_id' => (int) $lead['id'],
		'title' => $lead['company'] !== '' ? $lead['company'] : $lead['contact_name'],
		'amount' => $lead['expected_value'],
		'stage' => 'qualified',
		'notes' => 'Converted from lead #' . (int) $lead['id'],
	));
	$db->prepare('UPDATE `epc_crm_leads` SET `status` = \'converted\', `time_updated` = ? WHERE `id` = ?')
		->execute(array(time(), (int) $lead['id']));
	return $oppId;
}

function epc_crm_list_activities(PDO $db, $openOnly = false)
{
	epc_crm_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_crm_activities` WHERE 1=1';
	if ($openOnly) {
		$sql .= ' AND `done` = 0';
	}
	$sql .= ' ORDER BY `due_date` ASC, `id` DESC LIMIT 150';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_crm_save_activity(PDO $db, array $data, $id = 0)
{
	epc_crm_ensure_schema($db);
	$type = in_array($data['activity_type'] ?? '', epc_crm_activity_types(), true) ? $data['activity_type'] : 'note';
	$relType = in_array($data['related_type'] ?? '', array('lead', 'opportunity', 'user'), true) ? $data['related_type'] : 'lead';
	$due = !empty($data['due_date']) ? strtotime((string) $data['due_date']) : 0;
	$params = array(
		$type,
		$relType,
		(int) ($data['related_id'] ?? 0),
		$due ?: 0,
		!empty($data['done']) ? 1 : 0,
		(int) ($data['owner_user_id'] ?? epc_crm_admin_id()),
		trim((string) ($data['notes'] ?? '')),
		time(),
	);
	if ($id > 0) {
		$params[] = (int) $id;
		$db->prepare(
			'UPDATE `epc_crm_activities` SET `activity_type`=?, `related_type`=?, `related_id`=?, `due_date`=?, `done`=?, `owner_user_id`=?, `notes`=? WHERE `id`=?'
		)->execute($params);
		return (int) $id;
	}
	$db->prepare(
		'INSERT INTO `epc_crm_activities` (`activity_type`,`related_type`,`related_id`,`due_date`,`done`,`owner_user_id`,`notes`,`time_created`) VALUES (?,?,?,?,?,?,?,?)'
	)->execute($params);
	return (int) $db->lastInsertId();
}

function epc_crm_won_order_hint(PDO $db, $oppId)
{
	$st = $db->prepare('SELECT `title`, `amount`, `linked_user_id`, `stage` FROM `epc_crm_opportunities` WHERE `id` = ? LIMIT 1');
	$st->execute(array((int) $oppId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row || $row['stage'] !== 'won') {
		return 'Mark opportunity as Won first.';
	}
	$hint = 'Create a shop order for customer';
	if ((int) $row['linked_user_id'] > 0) {
		$hint .= ' user_id=' . (int) $row['linked_user_id'];
	}
	$hint .= ' — deal value ' . number_format((float) $row['amount'], 2) . ' AED (' . epc_crm_h($row['title']) . ').';
	return $hint;
}

function epc_crm_tab_url($base, $tab)
{
	return $base . '?tab=' . urlencode($tab);
}

function epc_crm_money($v)
{
	return number_format((float) $v, 2);
}
