<?php
defined('_ASTEXE_') or die('No access');

/**
 * BOS Workflow pillar — generic approval & automation engine.
 *
 * A reusable, config-driven approval state machine that any entity (purchase
 * order, sales order, invoice, payment, journal, expense, ...) can route
 * through. Rules are per-tenant: an admin defines threshold rules
 * ("PO amount >= 10,000 needs Manager approval") and the engine raises an
 * approval request, walks it through ordered steps, and writes an immutable
 * audit log for every transition.
 *
 * Distinct from the department task board (erp_tabs_workflow): that is a
 * to-do board; this is a governance / approval-of-record engine.
 */

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';

function epc_bos_wf_admin_id(): int
{
	return function_exists('epc_erp_admin_id') ? (int) epc_erp_admin_id() : 0;
}

/** Entities the approval engine can govern. Drives the rule-builder dropdown. */
function epc_bos_wf_entity_types(): array
{
	return array(
		'purchase_order' => 'Purchase order',
		'sales_order' => 'Sales order',
		'purchase_invoice' => 'Purchase invoice / bill',
		'sales_invoice' => 'Sales invoice',
		'payment_voucher' => 'Payment voucher',
		'receipt_voucher' => 'Receipt voucher',
		'gl_journal' => 'GL journal',
		'expense' => 'Expense claim',
		'rfq' => 'RFQ',
	);
}

function epc_bos_wf_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_bos_approval_rules` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`name` varchar(160) NOT NULL,
		`entity_type` varchar(48) NOT NULL,
		`operator` enum('>=','>','<=','any') NOT NULL DEFAULT '>=',
		`threshold_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
		`steps_json` text,
		`priority` int(11) NOT NULL DEFAULT 100,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_entity` (`entity_type`,`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS approval rules';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_bos_approval_requests` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`rule_id` int(11) NOT NULL DEFAULT 0,
		`entity_type` varchar(48) NOT NULL,
		`entity_ref` varchar(96) NOT NULL DEFAULT '',
		`entity_id` int(11) NOT NULL DEFAULT 0,
		`amount` decimal(16,2) NOT NULL DEFAULT 0.00,
		`title` varchar(200) DEFAULT NULL,
		`steps_json` text,
		`current_step` int(11) NOT NULL DEFAULT 0,
		`status` enum('pending','approved','rejected','cancelled') NOT NULL DEFAULT 'pending',
		`requested_by` int(11) NOT NULL DEFAULT 0,
		`created_at` int(11) NOT NULL DEFAULT 0,
		`decided_at` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_status` (`status`),
		KEY `x_entity` (`entity_type`,`entity_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS approval requests';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_bos_approval_log` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`request_id` int(11) NOT NULL DEFAULT 0,
		`step_index` int(11) NOT NULL DEFAULT 0,
		`action` varchar(32) NOT NULL DEFAULT '',
		`actor_id` int(11) NOT NULL DEFAULT 0,
		`actor_name` varchar(120) DEFAULT NULL,
		`comment` text,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_req` (`request_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS approval audit log';");
}

/** Seed one example rule the first time so the pillar is not empty (idempotent). */
function epc_bos_wf_seed(PDO $db): void
{
	epc_bos_wf_ensure_schema($db);
	$has = (int) $db->query("SELECT COUNT(*) FROM `epc_bos_approval_rules`")->fetchColumn();
	if ($has > 0) {
		return;
	}
	$steps = json_encode(array(
		array('role' => 'Manager', 'label' => 'Manager approval'),
		array('role' => 'Finance', 'label' => 'Finance sign-off'),
	));
	$st = $db->prepare(
		'INSERT INTO `epc_bos_approval_rules`
			(`name`,`entity_type`,`operator`,`threshold_amount`,`steps_json`,`priority`,`active`,`admin_id`,`time`)
		 VALUES (?,?,?,?,?,?,1,?,?)'
	);
	$st->execute(array(
		'High-value PO approval', 'purchase_order', '>=', 10000.00, $steps, 100,
		epc_bos_wf_admin_id(), time(),
	));
}

/** @return array<int,array<string,mixed>> */
function epc_bos_wf_rules(PDO $db, string $entityType = ''): array
{
	epc_bos_wf_ensure_schema($db);
	if ($entityType !== '') {
		$st = $db->prepare("SELECT * FROM `epc_bos_approval_rules` WHERE `active` = 1 AND `entity_type` = ? ORDER BY `priority`, `threshold_amount` DESC");
		$st->execute(array($entityType));
		return $st->fetchAll();
	}
	$rows = array();
	foreach ($db->query("SELECT * FROM `epc_bos_approval_rules` ORDER BY `entity_type`, `priority`") as $r) {
		$rows[] = $r;
	}
	return $rows;
}

function epc_bos_wf_decode_steps($json): array
{
	$steps = json_decode((string) $json, true);
	if (!is_array($steps) || empty($steps)) {
		return array(array('role' => 'Manager', 'label' => 'Approval'));
	}
	return array_values($steps);
}

function epc_bos_wf_save_rule(PDO $db, array $post): int
{
	epc_bos_wf_ensure_schema($db);
	$name = trim((string) ($post['name'] ?? ''));
	$entity = (string) ($post['entity_type'] ?? '');
	if ($name === '' || !array_key_exists($entity, epc_bos_wf_entity_types())) {
		return 0;
	}
	$op = (string) ($post['operator'] ?? '>=');
	$op = in_array($op, array('>=', '>', '<=', 'any'), true) ? $op : '>=';
	// Steps: accept either steps_json or role lines (step_role[]).
	$steps = array();
	if (!empty($post['step_role']) && is_array($post['step_role'])) {
		foreach ($post['step_role'] as $i => $role) {
			$role = trim((string) $role);
			if ($role === '') {
				continue;
			}
			$label = trim((string) ($post['step_label'][$i] ?? ($role . ' approval')));
			$steps[] = array('role' => $role, 'label' => $label !== '' ? $label : ($role . ' approval'));
		}
	}
	if (empty($steps)) {
		$steps = array(array('role' => trim((string) ($post['approver_role'] ?? 'Manager')) ?: 'Manager', 'label' => 'Approval'));
	}
	$id = (int) ($post['rule_id'] ?? 0);
	if ($id > 0) {
		$st = $db->prepare(
			'UPDATE `epc_bos_approval_rules` SET `name` = ?, `entity_type` = ?, `operator` = ?,
				`threshold_amount` = ?, `steps_json` = ?, `priority` = ?, `active` = ? WHERE `id` = ?'
		);
		$st->execute(array(
			$name, $entity, $op, (float) ($post['threshold_amount'] ?? 0),
			json_encode($steps), (int) ($post['priority'] ?? 100),
			empty($post['disable']) ? 1 : 0, $id,
		));
		return $id;
	}
	$st = $db->prepare(
		'INSERT INTO `epc_bos_approval_rules`
			(`name`,`entity_type`,`operator`,`threshold_amount`,`steps_json`,`priority`,`active`,`admin_id`,`time`)
		 VALUES (?,?,?,?,?,?,1,?,?)'
	);
	$st->execute(array(
		$name, $entity, $op, (float) ($post['threshold_amount'] ?? 0),
		json_encode($steps), (int) ($post['priority'] ?? 100),
		epc_bos_wf_admin_id(), time(),
	));
	return (int) $db->lastInsertId();
}

function epc_bos_wf_disable_rule(PDO $db, int $id): void
{
	$st = $db->prepare("UPDATE `epc_bos_approval_rules` SET `active` = 0 WHERE `id` = ?");
	$st->execute(array($id));
}

/**
 * Find the matching rule for an entity+amount (highest priority, then highest
 * threshold). Returns null when no rule applies (i.e. no approval needed).
 */
function epc_bos_wf_match_rule(PDO $db, string $entityType, float $amount): ?array
{
	foreach (epc_bos_wf_rules($db, $entityType) as $rule) {
		$th = (float) $rule['threshold_amount'];
		$op = (string) $rule['operator'];
		$hit = false;
		switch ($op) {
			case 'any': $hit = true; break;
			case '>': $hit = $amount > $th; break;
			case '<=': $hit = $amount <= $th; break;
			default: $hit = $amount >= $th; break;
		}
		if ($hit) {
			return $rule;
		}
	}
	return null;
}

/**
 * Raise an approval request for an entity if a rule matches. Idempotent per
 * entity: will not duplicate a pending request for the same entity instance.
 *
 * @return int request id (0 = no approval required)
 */
function epc_bos_wf_raise(PDO $db, string $entityType, int $entityId, string $entityRef, float $amount, string $title = ''): int
{
	$rule = epc_bos_wf_match_rule($db, $entityType, $amount);
	if ($rule === null) {
		return 0;
	}
	epc_bos_wf_ensure_schema($db);
	$chk = $db->prepare("SELECT `id` FROM `epc_bos_approval_requests` WHERE `entity_type` = ? AND `entity_id` = ? AND `status` = 'pending' LIMIT 1");
	$chk->execute(array($entityType, $entityId));
	$existing = (int) $chk->fetchColumn();
	if ($existing > 0) {
		return $existing;
	}
	$steps = epc_bos_wf_decode_steps($rule['steps_json']);
	$now = time();
	$st = $db->prepare(
		'INSERT INTO `epc_bos_approval_requests`
			(`rule_id`,`entity_type`,`entity_ref`,`entity_id`,`amount`,`title`,`steps_json`,`current_step`,`status`,`requested_by`,`created_at`)
		 VALUES (?,?,?,?,?,?,?,0,?,?,?)'
	);
	$st->execute(array(
		(int) $rule['id'], $entityType, substr($entityRef, 0, 96), $entityId, $amount,
		substr($title !== '' ? $title : $entityRef, 0, 200), json_encode($steps),
		'pending', epc_bos_wf_admin_id(), $now,
	));
	$reqId = (int) $db->lastInsertId();
	epc_bos_wf_log($db, $reqId, 0, 'raised', 'Auto-raised by rule "' . $rule['name'] . '" (' . $rule['operator'] . ' ' . number_format((float) $rule['threshold_amount'], 2) . ')');
	return $reqId;
}

function epc_bos_wf_log(PDO $db, int $requestId, int $stepIndex, string $action, string $comment = ''): void
{
	$name = '';
	if (function_exists('epc_erp_admin_name')) {
		$name = (string) epc_erp_admin_name();
	}
	$st = $db->prepare(
		'INSERT INTO `epc_bos_approval_log` (`request_id`,`step_index`,`action`,`actor_id`,`actor_name`,`comment`,`time`)
		 VALUES (?,?,?,?,?,?,?)'
	);
	$st->execute(array($requestId, $stepIndex, $action, epc_bos_wf_admin_id(), $name, $comment, time()));
}

/** Approve current step; advances to next step or marks request approved. */
function epc_bos_wf_decide(PDO $db, int $requestId, string $decision, string $comment = ''): array
{
	epc_bos_wf_ensure_schema($db);
	$st = $db->prepare("SELECT * FROM `epc_bos_approval_requests` WHERE `id` = ? LIMIT 1");
	$st->execute(array($requestId));
	$req = $st->fetch();
	if (!$req || $req['status'] !== 'pending') {
		return array('status' => false, 'message' => 'Request not pending');
	}
	$steps = epc_bos_wf_decode_steps($req['steps_json']);
	$cur = (int) $req['current_step'];
	$now = time();
	if ($decision === 'reject') {
		$db->prepare("UPDATE `epc_bos_approval_requests` SET `status` = 'rejected', `decided_at` = ? WHERE `id` = ?")
			->execute(array($now, $requestId));
		epc_bos_wf_log($db, $requestId, $cur, 'rejected', $comment);
		return array('status' => true, 'message' => 'Rejected', 'final' => 'rejected');
	}
	// approve
	epc_bos_wf_log($db, $requestId, $cur, 'approved', $comment);
	if ($cur + 1 >= count($steps)) {
		$db->prepare("UPDATE `epc_bos_approval_requests` SET `status` = 'approved', `current_step` = ?, `decided_at` = ? WHERE `id` = ?")
			->execute(array($cur, $now, $requestId));
		return array('status' => true, 'message' => 'Approved (final)', 'final' => 'approved');
	}
	$db->prepare("UPDATE `epc_bos_approval_requests` SET `current_step` = ? WHERE `id` = ?")
		->execute(array($cur + 1, $requestId));
	return array('status' => true, 'message' => 'Approved — advanced to next step', 'final' => 'pending');
}

/** @return array<int,array<string,mixed>> */
function epc_bos_wf_requests(PDO $db, string $status = '', int $limit = 100): array
{
	epc_bos_wf_ensure_schema($db);
	$limit = max(1, min(500, $limit));
	if ($status !== '') {
		$st = $db->prepare("SELECT * FROM `epc_bos_approval_requests` WHERE `status` = ? ORDER BY `created_at` DESC LIMIT $limit");
		$st->execute(array($status));
		return $st->fetchAll();
	}
	$rows = array();
	foreach ($db->query("SELECT * FROM `epc_bos_approval_requests` ORDER BY `created_at` DESC LIMIT $limit") as $r) {
		$rows[] = $r;
	}
	return $rows;
}

/** @return array<int,array<string,mixed>> */
function epc_bos_wf_request_log(PDO $db, int $requestId): array
{
	epc_bos_wf_ensure_schema($db);
	$st = $db->prepare("SELECT * FROM `epc_bos_approval_log` WHERE `request_id` = ? ORDER BY `id` ASC");
	$st->execute(array($requestId));
	return $st->fetchAll();
}

/** Best-effort extraction of a transaction amount from a POST payload. */
function epc_bos_wf_amount_from_post(array $post): float
{
	foreach (array('approval_amount', 'grand_total', 'total_incl_vat', 'total_amount', 'total', 'amount', 'net_total') as $k) {
		if (isset($post[$k]) && is_numeric($post[$k])) {
			return (float) $post[$k];
		}
	}
	// Fall back to summing line totals when present.
	if (!empty($post['line_total']) && is_array($post['line_total'])) {
		$sum = 0.0;
		foreach ($post['line_total'] as $v) {
			$sum += (float) $v;
		}
		if ($sum > 0) {
			return $sum;
		}
	}
	return 0.0;
}

/**
 * Convenience hook for transaction handlers: raise an approval if a rule matches
 * the entity+amount. Returns a human message fragment (empty when none raised).
 */
function epc_bos_wf_maybe_raise(PDO $db, string $entityType, int $entityId, string $ref, array $post, string $title = ''): string
{
	$amount = epc_bos_wf_amount_from_post($post);
	if ($amount <= 0) {
		return '';
	}
	$reqId = epc_bos_wf_raise($db, $entityType, $entityId, $ref, $amount, $title !== '' ? $title : $ref);
	return $reqId > 0 ? ' — approval required (#' . $reqId . ')' : '';
}

/** @return array<string,int> */
function epc_bos_wf_summary(PDO $db): array
{
	epc_bos_wf_ensure_schema($db);
	$out = array('pending' => 0, 'approved' => 0, 'rejected' => 0, 'rules' => 0);
	foreach ($db->query("SELECT `status`, COUNT(*) c FROM `epc_bos_approval_requests` GROUP BY `status`") as $r) {
		if (isset($out[$r['status']])) {
			$out[$r['status']] = (int) $r['c'];
		}
	}
	$out['rules'] = (int) $db->query("SELECT COUNT(*) FROM `epc_bos_approval_rules` WHERE `active` = 1")->fetchColumn();
	return $out;
}
