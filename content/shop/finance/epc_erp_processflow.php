<?php
/**
 * Process Flow — chained task routing / lightweight BPM for the BOS.
 *
 * Lets an organisation define a named process as an ordered chain of steps,
 * each routed to a specific person, a department head, or "anyone in a
 * department". A running "case" walks the chain automatically: when the current
 * assignee approves/completes their step, the case hands off to the next step's
 * assignee, until the chain finishes. A live monitor shows exactly which step
 * every case has reached, who holds it now, and whether it has breached SLA.
 *
 * Tables (auto-provisioned per tenant, empty until used):
 *   epc_pf_processes   — process templates
 *   epc_pf_steps       — ordered step definitions per process
 *   epc_pf_dept_heads  — department_code -> head user_id
 *   epc_pf_cases       — running / closed work items
 *   epc_pf_case_steps  — per-case step history (the "where it reached" trail)
 */

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';

function epc_pf_admin_id(): int
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	return class_exists('DP_User') ? (int) DP_User::getAdminId() : 0;
}

function epc_pf_user_id(): int
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	return class_exists('DP_User') ? (int) DP_User::getUserId() : 0;
}

function epc_pf_assign_types(): array
{
	return array(
		'dept_head' => 'Department head',
		'user' => 'Specific person',
		'department' => 'Anyone in department',
		'initiator' => 'Back to initiator',
	);
}

function epc_pf_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pf_processes` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`name` varchar(160) NOT NULL,
		`description` text,
		`category` varchar(64) NOT NULL DEFAULT 'general',
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_active` (`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Process flow templates'");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pf_steps` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`process_id` int(11) NOT NULL,
		`step_no` int(11) NOT NULL DEFAULT 1,
		`name` varchar(160) NOT NULL,
		`assign_type` varchar(16) NOT NULL DEFAULT 'dept_head',
		`assign_user_id` int(11) NOT NULL DEFAULT 0,
		`assign_department` varchar(32) NOT NULL DEFAULT '',
		`sla_hours` int(11) NOT NULL DEFAULT 24,
		`instructions` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_proc` (`process_id`,`step_no`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Process step definitions'");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pf_dept_heads` (
		`department_code` varchar(32) NOT NULL,
		`head_user_id` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`department_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Department heads'");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pf_cases` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`process_id` int(11) NOT NULL,
		`title` varchar(255) NOT NULL,
		`reference` varchar(120) NOT NULL DEFAULT '',
		`priority` enum('low','normal','high','urgent') NOT NULL DEFAULT 'normal',
		`status` enum('open','done','cancelled','rejected') NOT NULL DEFAULT 'open',
		`current_step_no` int(11) NOT NULL DEFAULT 1,
		`current_assignee_id` int(11) NOT NULL DEFAULT 0,
		`current_department` varchar(32) NOT NULL DEFAULT '',
		`initiator_id` int(11) NOT NULL DEFAULT 0,
		`subject_type` varchar(40) NOT NULL DEFAULT '',
		`subject_id` int(11) NOT NULL DEFAULT 0,
		`started_at` int(11) NOT NULL DEFAULT 0,
		`due_at` int(11) NOT NULL DEFAULT 0,
		`completed_at` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_status` (`status`),
		KEY `x_assignee` (`current_assignee_id`,`status`),
		KEY `x_proc` (`process_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Process flow cases'");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pf_case_steps` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`case_id` int(11) NOT NULL,
		`step_no` int(11) NOT NULL,
		`name` varchar(160) NOT NULL,
		`assign_type` varchar(16) NOT NULL DEFAULT 'dept_head',
		`department` varchar(32) NOT NULL DEFAULT '',
		`assignee_id` int(11) NOT NULL DEFAULT 0,
		`status` enum('pending','active','approved','rejected','skipped') NOT NULL DEFAULT 'pending',
		`comment` text,
		`sla_due_at` int(11) NOT NULL DEFAULT 0,
		`activated_at` int(11) NOT NULL DEFAULT 0,
		`completed_at` int(11) NOT NULL DEFAULT 0,
		`acted_by` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_case` (`case_id`,`step_no`),
		KEY `x_assignee` (`assignee_id`,`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-case step trail'");

	// location columns (GPS-style tracking across branches/sites)
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_schema.php';
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_staff_profiles', 'location', "varchar(80) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_staff_profiles', 'photo_url', "varchar(255) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_staff_profiles', 'business_unit', "varchar(80) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_staff_profiles', 'legal_entity', "varchar(120) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_pf_cases', 'current_location', "varchar(80) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_pf_case_steps', 'location', "varchar(80) NOT NULL DEFAULT ''");
}

/** Photo/avatar URL for a person: stored profile photo if present, else a deterministic generated face. */
function epc_pf_avatar_url(string $name, string $photo = ''): string
{
	$photo = trim($photo);
	if ($photo !== '') { return $photo; }
	$seed = rawurlencode($name !== '' ? $name : 'staff');
	return 'https://api.dicebear.com/7.x/avataaars/svg?radius=50&backgroundType=gradientLinear&seed=' . $seed;
}

/** Photo URL for a user id (looks up their staff profile). */
function epc_pf_user_photo(PDO $db, int $userId): string
{
	if ($userId <= 0) { return ''; }
	static $cache = array();
	if (array_key_exists($userId, $cache)) { return $cache[$userId]; }
	$photo = '';
	try {
		$st = $db->prepare("SELECT `photo_url` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1");
		$st->execute(array($userId));
		$photo = (string) $st->fetchColumn();
	} catch (Exception $e) {
	}
	$cache[$userId] = $photo;
	return $photo;
}

/**
 * Business unit for a department (a functional division that cuts across branches).
 * Used as a fallback when a staff profile has no explicit business_unit set.
 */
function epc_pf_bu_for_dept(string $deptCode): string
{
	$map = array(
		'sales' => 'Retail BU', 'marketing' => 'Retail BU',
		'purchase' => 'Distribution BU', 'logistics' => 'Distribution BU',
		'finance' => 'Corporate BU', 'accounts' => 'Corporate BU',
		'hr' => 'Corporate BU', 'it' => 'Corporate BU',
	);
	$deptCode = strtolower(trim($deptCode));
	return $map[$deptCode] ?? 'Corporate BU';
}

/** Legal entity for a location/branch. Fallback when no explicit legal_entity is set. */
function epc_pf_legal_entity_for_location(string $location): string
{
	$fze = array('Sharjah Branch', 'Jebel Ali Warehouse');
	if (in_array($location, $fze, true)) { return 'ECOM AE FZE'; }
	return 'ECOM AE Trading LLC';
}

/** Distinct work locations/branches, drawn from seeded/real staff. */
function epc_pf_locations(PDO $db): array
{
	epc_pf_ensure_schema($db);
	$out = array();
	try {
		foreach ($db->query("SELECT DISTINCT `location` FROM `epc_erp_staff_profiles` WHERE `location` <> '' ORDER BY `location`")->fetchAll(PDO::FETCH_COLUMN) as $l) {
			$out[] = (string) $l;
		}
	} catch (Exception $e) {
	}
	return $out;
}

/** Work location of a user (from their staff profile). */
function epc_pf_user_location(PDO $db, int $userId): string
{
	if ($userId <= 0) {
		return '';
	}
	static $cache = array();
	if (isset($cache[$userId])) {
		return $cache[$userId];
	}
	$loc = '';
	try {
		$st = $db->prepare("SELECT `location` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1");
		$st->execute(array($userId));
		$loc = (string) $st->fetchColumn();
	} catch (Exception $e) {
	}
	$cache[$userId] = $loc;
	return $loc;
}

/* ---------- people / departments ---------- */

/** Map a user id to a readable name (staff profile first, then HR, then users). */
function epc_pf_user_name(PDO $db, int $userId): string
{
	if ($userId <= 0) {
		return '—';
	}
	static $cache = array();
	if (isset($cache[$userId])) {
		return $cache[$userId];
	}
	$name = '';
	try {
		$st = $db->prepare("SELECT `display_name` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1");
		$st->execute(array($userId));
		$name = (string) $st->fetchColumn();
	} catch (Exception $e) {
	}
	if ($name === '') {
		try {
			$st = $db->prepare("SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1");
			$st->execute(array($userId));
			$name = (string) $st->fetchColumn();
		} catch (Exception $e) {
		}
	}
	if ($name === '') {
		$name = 'User #' . $userId;
	}
	$cache[$userId] = $name;
	return $name;
}

function epc_pf_dept_heads(PDO $db): array
{
	epc_pf_ensure_schema($db);
	$out = array();
	foreach ($db->query("SELECT `department_code`, `head_user_id` FROM `epc_pf_dept_heads`")->fetchAll(PDO::FETCH_ASSOC) as $r) {
		$out[(string) $r['department_code']] = (int) $r['head_user_id'];
	}
	return $out;
}

function epc_pf_set_dept_head(PDO $db, string $deptCode, int $userId): void
{
	epc_pf_ensure_schema($db);
	$db->prepare("INSERT INTO `epc_pf_dept_heads` (`department_code`,`head_user_id`,`time_updated`) VALUES (?,?,?)
		ON DUPLICATE KEY UPDATE `head_user_id` = VALUES(`head_user_id`), `time_updated` = VALUES(`time_updated`)")
		->execute(array($deptCode, $userId, time()));
}

/** Resolve the user a step should be assigned to. Falls back so a case never stalls. */
function epc_pf_resolve_assignee(PDO $db, array $step, array $case, array $deptHeads): int
{
	$type = (string) ($step['assign_type'] ?? 'dept_head');
	$dept = (string) ($step['assign_department'] ?? '');
	if ($type === 'user' && (int) $step['assign_user_id'] > 0) {
		return (int) $step['assign_user_id'];
	}
	if ($type === 'initiator' && (int) ($case['initiator_id'] ?? 0) > 0) {
		return (int) $case['initiator_id'];
	}
	if ($type === 'dept_head' && $dept !== '' && !empty($deptHeads[$dept])) {
		return (int) $deptHeads[$dept];
	}
	if ($type === 'department' && $dept !== '') {
		try {
			// pick any available person in the department (varied so work spreads
			// across people and locations rather than always the same desk)
			$st = $db->prepare("SELECT `user_id` FROM `epc_erp_staff_profiles` WHERE `department_code` = ? AND `active` = 1 ORDER BY RAND() LIMIT 1");
			$st->execute(array($dept));
			$uid = (int) $st->fetchColumn();
			if ($uid > 0) {
				return $uid;
			}
		} catch (Exception $e) {
		}
	}
	// fallbacks: department head of the named dept, then initiator, then current admin
	if ($dept !== '' && !empty($deptHeads[$dept])) {
		return (int) $deptHeads[$dept];
	}
	if ((int) ($case['initiator_id'] ?? 0) > 0) {
		return (int) $case['initiator_id'];
	}
	return epc_pf_user_id();
}

/* ---------- process templates ---------- */

function epc_pf_process_save(PDO $db, array $data): int
{
	epc_pf_ensure_schema($db);
	$id = (int) ($data['id'] ?? 0);
	$name = mb_substr(trim((string) ($data['name'] ?? '')), 0, 160);
	if ($name === '') {
		throw new Exception('Process name is required');
	}
	$desc = (string) ($data['description'] ?? '');
	$cat = mb_substr((string) ($data['category'] ?? 'general'), 0, 64);
	$active = !empty($data['active']) ? 1 : (isset($data['active']) ? 0 : 1);
	if ($id > 0) {
		$db->prepare("UPDATE `epc_pf_processes` SET `name`=?, `description`=?, `category`=?, `active`=? WHERE `id`=?")
			->execute(array($name, $desc, $cat, $active, $id));
		return $id;
	}
	$db->prepare("INSERT INTO `epc_pf_processes` (`name`,`description`,`category`,`active`,`time_created`) VALUES (?,?,?,?,?)")
		->execute(array($name, $desc, $cat, 1, time()));
	return (int) $db->lastInsertId();
}

function epc_pf_processes(PDO $db, bool $activeOnly = false): array
{
	epc_pf_ensure_schema($db);
	$sql = "SELECT p.*, (SELECT COUNT(*) FROM `epc_pf_steps` s WHERE s.`process_id` = p.`id`) AS step_count,
		(SELECT COUNT(*) FROM `epc_pf_cases` c WHERE c.`process_id` = p.`id` AND c.`status` = 'open') AS open_cases
		FROM `epc_pf_processes` p";
	if ($activeOnly) {
		$sql .= " WHERE p.`active` = 1";
	}
	$sql .= " ORDER BY p.`name`";
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pf_process_steps(PDO $db, int $processId): array
{
	epc_pf_ensure_schema($db);
	$st = $db->prepare("SELECT * FROM `epc_pf_steps` WHERE `process_id` = ? ORDER BY `step_no`, `id`");
	$st->execute(array($processId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pf_step_save(PDO $db, array $data): int
{
	epc_pf_ensure_schema($db);
	$processId = (int) ($data['process_id'] ?? 0);
	if ($processId <= 0) {
		throw new Exception('Process is required');
	}
	$name = mb_substr(trim((string) ($data['name'] ?? '')), 0, 160);
	if ($name === '') {
		throw new Exception('Step name is required');
	}
	$type = (string) ($data['assign_type'] ?? 'dept_head');
	if (!array_key_exists($type, epc_pf_assign_types())) {
		$type = 'dept_head';
	}
	$stepNo = (int) ($data['step_no'] ?? 0);
	if ($stepNo <= 0) {
		$max = (int) $db->query("SELECT COALESCE(MAX(`step_no`),0) FROM `epc_pf_steps` WHERE `process_id` = " . $processId)->fetchColumn();
		$stepNo = $max + 1;
	}
	$db->prepare("INSERT INTO `epc_pf_steps`
		(`process_id`,`step_no`,`name`,`assign_type`,`assign_user_id`,`assign_department`,`sla_hours`,`instructions`,`time_created`)
		VALUES (?,?,?,?,?,?,?,?,?)")
		->execute(array(
			$processId, $stepNo, $name, $type,
			(int) ($data['assign_user_id'] ?? 0),
			mb_substr((string) ($data['assign_department'] ?? ''), 0, 32),
			max(0, (int) ($data['sla_hours'] ?? 24)),
			(string) ($data['instructions'] ?? ''),
			time(),
		));
	return (int) $db->lastInsertId();
}

function epc_pf_step_delete(PDO $db, int $stepId): void
{
	$db->prepare("DELETE FROM `epc_pf_steps` WHERE `id` = ?")->execute(array($stepId));
}

/* ---------- cases (running work items) ---------- */

/**
 * Start a case from a process: materialise every step as a pending row, activate
 * step 1 and route it to its assignee. Returns the new case id.
 */
function epc_pf_case_start(PDO $db, array $data): int
{
	epc_pf_ensure_schema($db);
	$processId = (int) ($data['process_id'] ?? 0);
	$steps = epc_pf_process_steps($db, $processId);
	if (empty($steps)) {
		throw new Exception('This process has no steps defined yet');
	}
	$title = mb_substr(trim((string) ($data['title'] ?? '')), 0, 255);
	if ($title === '') {
		throw new Exception('Case title is required');
	}
	$priority = in_array($data['priority'] ?? '', array('low', 'normal', 'high', 'urgent'), true) ? $data['priority'] : 'normal';
	$initiator = (int) ($data['initiator_id'] ?? epc_pf_user_id());
	$now = time();

	$case = array('initiator_id' => $initiator);
	$deptHeads = epc_pf_dept_heads($db);

	$db->beginTransaction();
	try {
		$db->prepare("INSERT INTO `epc_pf_cases`
			(`process_id`,`title`,`reference`,`priority`,`status`,`current_step_no`,`initiator_id`,`subject_type`,`subject_id`,`started_at`,`time_created`,`time_updated`)
			VALUES (?,?,?,?,'open',?,?,?,?,?,?,?)")
			->execute(array(
				$processId, $title,
				mb_substr((string) ($data['reference'] ?? ''), 0, 120),
				$priority,
				(int) $steps[0]['step_no'],
				$initiator,
				mb_substr((string) ($data['subject_type'] ?? ''), 0, 40),
				(int) ($data['subject_id'] ?? 0),
				$now, $now, $now,
			));
		$caseId = (int) $db->lastInsertId();

		$firstAssignee = 0;
		$firstDept = '';
		$firstDue = 0;
		$firstLoc = '';
		foreach ($steps as $i => $s) {
			$isFirst = ($i === 0);
			$assignee = $isFirst ? epc_pf_resolve_assignee($db, $s, $case, $deptHeads) : 0;
			$loc = $isFirst ? epc_pf_user_location($db, $assignee) : '';
			$slaDue = ($isFirst && (int) $s['sla_hours'] > 0) ? ($now + (int) $s['sla_hours'] * 3600) : 0;
			$db->prepare("INSERT INTO `epc_pf_case_steps`
				(`case_id`,`step_no`,`name`,`assign_type`,`department`,`assignee_id`,`location`,`status`,`sla_due_at`,`activated_at`,`time_created`)
				VALUES (?,?,?,?,?,?,?,?,?,?,?)")
				->execute(array(
					$caseId, (int) $s['step_no'], (string) $s['name'], (string) $s['assign_type'],
					(string) $s['assign_department'], $assignee, $loc,
					$isFirst ? 'active' : 'pending',
					$slaDue, $isFirst ? $now : 0, $now,
				));
			if ($isFirst) {
				$firstAssignee = $assignee;
				$firstDept = (string) $s['assign_department'];
				$firstDue = $slaDue;
				$firstLoc = $loc;
			}
		}
		$db->prepare("UPDATE `epc_pf_cases` SET `current_assignee_id`=?, `current_department`=?, `current_location`=?, `due_at`=? WHERE `id`=?")
			->execute(array($firstAssignee, $firstDept, $firstLoc, $firstDue, $caseId));
		$db->commit();
		return $caseId;
	} catch (Exception $e) {
		if ($db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
}

/**
 * Act on a case's current step. decision = approve|reject. On approve the case
 * hands off to the next step (auto-routing to its assignee) or completes; on
 * reject the case is marked rejected and stops. Returns a status array.
 *
 * @return array{ok:bool,status:string,message:string,next_assignee:int}
 */
function epc_pf_case_act(PDO $db, int $caseId, string $decision, string $comment = '', int $actorId = 0): array
{
	epc_pf_ensure_schema($db);
	if ($actorId <= 0) {
		$actorId = epc_pf_user_id();
	}
	$cst = $db->prepare("SELECT * FROM `epc_pf_cases` WHERE `id` = ?");
	$cst->execute(array($caseId));
	$case = $cst->fetch(PDO::FETCH_ASSOC);
	if (!$case) {
		throw new Exception('Case not found');
	}
	if ($case['status'] !== 'open') {
		throw new Exception('This case is already ' . $case['status']);
	}
	$now = time();
	$curNo = (int) $case['current_step_no'];
	$findCur = $db->prepare("SELECT * FROM `epc_pf_case_steps` WHERE `case_id` = ? AND `step_no` = ? AND `status` = 'active' LIMIT 1");
	$findCur->execute(array($caseId, $curNo));
	$curStep = $findCur->fetch(PDO::FETCH_ASSOC);
	if (!$curStep) {
		throw new Exception('No active step on this case');
	}

	if ($decision === 'reject') {
		$db->prepare("UPDATE `epc_pf_case_steps` SET `status`='rejected', `comment`=?, `completed_at`=?, `acted_by`=? WHERE `id`=?")
			->execute(array($comment, $now, $actorId, (int) $curStep['id']));
		$db->prepare("UPDATE `epc_pf_cases` SET `status`='rejected', `completed_at`=?, `time_updated`=? WHERE `id`=?")
			->execute(array($now, $now, $caseId));
		return array('ok' => true, 'status' => 'rejected', 'message' => 'Case rejected at step ' . $curNo, 'next_assignee' => 0);
	}

	// approve / complete current step
	$db->prepare("UPDATE `epc_pf_case_steps` SET `status`='approved', `comment`=?, `completed_at`=?, `acted_by`=? WHERE `id`=?")
		->execute(array($comment, $now, $actorId, (int) $curStep['id']));

	$nxt = $db->prepare("SELECT * FROM `epc_pf_case_steps` WHERE `case_id` = ? AND `step_no` > ? ORDER BY `step_no` LIMIT 1");
	$nxt->execute(array($caseId, $curNo));
	$nextStep = $nxt->fetch(PDO::FETCH_ASSOC);

	if (!$nextStep) {
		$db->prepare("UPDATE `epc_pf_cases` SET `status`='done', `completed_at`=?, `time_updated`=? WHERE `id`=?")
			->execute(array($now, $now, $caseId));
		return array('ok' => true, 'status' => 'done', 'message' => 'Final step approved — case complete', 'next_assignee' => 0);
	}

	// resolve + activate next step
	$deptHeads = epc_pf_dept_heads($db);
	$stepDef = array(
		'assign_type' => $nextStep['assign_type'],
		'assign_user_id' => $nextStep['assignee_id'],
		'assign_department' => $nextStep['department'],
	);
	// pull the template values (assign_user_id is on the definition, not the case step)
	$tpl = $db->prepare("SELECT * FROM `epc_pf_steps` WHERE `process_id` = ? AND `step_no` = ? LIMIT 1");
	$tpl->execute(array((int) $case['process_id'], (int) $nextStep['step_no']));
	$tplRow = $tpl->fetch(PDO::FETCH_ASSOC);
	if ($tplRow) {
		$stepDef['assign_type'] = $tplRow['assign_type'];
		$stepDef['assign_user_id'] = $tplRow['assign_user_id'];
		$stepDef['assign_department'] = $tplRow['assign_department'];
	}
	$assignee = epc_pf_resolve_assignee($db, $stepDef, $case, $deptHeads);
	$assigneeLoc = epc_pf_user_location($db, $assignee);
	$slaHours = $tplRow ? (int) $tplRow['sla_hours'] : 24;
	$slaDue = $slaHours > 0 ? ($now + $slaHours * 3600) : 0;

	$db->prepare("UPDATE `epc_pf_case_steps` SET `status`='active', `assignee_id`=?, `location`=?, `sla_due_at`=?, `activated_at`=? WHERE `id`=?")
		->execute(array($assignee, $assigneeLoc, $slaDue, $now, (int) $nextStep['id']));
	$db->prepare("UPDATE `epc_pf_cases` SET `current_step_no`=?, `current_assignee_id`=?, `current_department`=?, `current_location`=?, `due_at`=?, `time_updated`=? WHERE `id`=?")
		->execute(array((int) $nextStep['step_no'], $assignee, (string) $stepDef['assign_department'], $assigneeLoc, $slaDue, $now, $caseId));

	return array('ok' => true, 'status' => 'open', 'message' => 'Approved — routed to ' . epc_pf_user_name($db, $assignee) . ' (step ' . (int) $nextStep['step_no'] . ')', 'next_assignee' => $assignee);
}

function epc_pf_case_reassign(PDO $db, int $caseId, int $newUserId): void
{
	epc_pf_ensure_schema($db);
	$cst = $db->prepare("SELECT `current_step_no`, `status` FROM `epc_pf_cases` WHERE `id` = ?");
	$cst->execute(array($caseId));
	$case = $cst->fetch(PDO::FETCH_ASSOC);
	if (!$case || $case['status'] !== 'open') {
		throw new Exception('Case is not open');
	}
	$db->prepare("UPDATE `epc_pf_case_steps` SET `assignee_id`=? WHERE `case_id`=? AND `step_no`=? AND `status`='active'")
		->execute(array($newUserId, $caseId, (int) $case['current_step_no']));
	$db->prepare("UPDATE `epc_pf_cases` SET `current_assignee_id`=?, `time_updated`=? WHERE `id`=?")
		->execute(array($newUserId, time(), $caseId));
}

function epc_pf_case_cancel(PDO $db, int $caseId): void
{
	$db->prepare("UPDATE `epc_pf_cases` SET `status`='cancelled', `completed_at`=?, `time_updated`=? WHERE `id`=? AND `status`='open'")
		->execute(array(time(), time(), $caseId));
}

/* ---------- automatic order-to-delivery process ---------- */

/** Stable category tag for the built-in order lifecycle process. */
const EPC_PF_ORDER_CATEGORY = 'order_lifecycle';

/**
 * Ensure the built-in "Customer Order → Delivery" process exists (idempotent).
 * Every tenant gets it automatically; it drives the auto-created case per order.
 * Returns the process id.
 */
function epc_pf_ensure_order_process(PDO $db): int
{
	epc_pf_ensure_schema($db);
	$id = (int) $db->query("SELECT `id` FROM `epc_pf_processes` WHERE `category` = '" . EPC_PF_ORDER_CATEGORY . "' ORDER BY `id` LIMIT 1")->fetchColumn();
	if ($id > 0) {
		return $id;
	}
	$pid = epc_pf_process_save($db, array(
		'name' => 'Customer Order → Delivery',
		'description' => 'Runs automatically for every customer order (online portal or manual). The case is created when the order arrives and advances by itself as the order is quoted, paid, procured, dispatched, delivered and invoiced.',
		'category' => EPC_PF_ORDER_CATEGORY,
	));
	$steps = array(
		array('Order received', 'department', 'sales', 4),
		array('Quotation confirmed', 'dept_head', 'sales', 12),
		array('Payment received', 'dept_head', 'finance', 24),
		array('Goods ready / procured', 'dept_head', 'purchase', 48),
		array('Out for delivery', 'dept_head', 'logistics', 12),
		array('Delivered', 'department', 'logistics', 24),
		array('Invoiced & closed', 'dept_head', 'accounts', 12),
	);
	$no = 1;
	foreach ($steps as $s) {
		epc_pf_step_save($db, array(
			'process_id' => $pid,
			'step_no' => $no++,
			'name' => $s[0],
			'assign_type' => $s[1],
			'assign_department' => $s[2],
			'sla_hours' => $s[3],
		));
	}
	return $pid;
}

/**
 * Gather the lifecycle signals for one commerce order from the ERP
 * (sales order, fulfilment status, completion, sales invoice). Best-effort and
 * never throws — missing signals simply leave that stage un-reached.
 */
function epc_pf_order_facts(PDO $db, int $orderId): array
{
	$f = array(
		'exists' => false, 'so' => false, 'paid' => false, 'procured' => false,
		'fulfilled' => false, 'delivered' => false, 'invoiced' => false,
		'customer' => '', 'so_id' => 0, 'invoice_id' => 0,
	);
	if ($orderId <= 0) {
		return $f;
	}
	try {
		$o = $db->prepare("SELECT * FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1");
		$o->execute(array($orderId));
		$row = $o->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$f['exists'] = true;
			$cust = trim((string) (($row['name'] ?? '') . ' ' . ($row['surname'] ?? '')));
			if ($cust === '') { $cust = trim((string) ($row['email'] ?? '')); }
			$f['customer'] = $cust;
		}
	} catch (Exception $e) {
	}
	if (!$f['exists']) {
		return $f;
	}
	try {
		$ff = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
		if (is_file($ff)) {
			require_once $ff;
			if (function_exists('epc_erp_order_fulfillment_find_sales_order')) {
				$so = epc_erp_order_fulfillment_find_sales_order($db, $orderId);
				if ($so) {
					$f['so'] = true;
					$f['so_id'] = (int) ($so['id'] ?? 0);
					$f['invoice_id'] = (int) ($so['sales_invoice_id'] ?? 0);
					if ($f['invoice_id'] > 0 || (string) ($so['status'] ?? '') === 'invoiced') {
						$f['invoiced'] = true;
					}
					$fs = (string) ($so['fulfillment_status'] ?? 'open');
					if (in_array($fs, array('partial', 'fulfilled'), true)) { $f['procured'] = true; }
					if ($fs === 'fulfilled') { $f['fulfilled'] = true; }
				}
			}
		}
	} catch (Exception $e) {
	}
	try {
		if (function_exists('epc_erp_order_is_complete') && epc_erp_order_is_complete($db, $orderId)) {
			$f['delivered'] = true;
			$f['fulfilled'] = true;
			$f['procured'] = true;
		}
	} catch (Exception $e) {
	}
	// payment is implied once the order has progressed past procurement
	if ($f['procured'] || $f['fulfilled'] || $f['delivered'] || $f['invoiced']) {
		$f['paid'] = true;
	}
	return $f;
}

/** Furthest step number (1..7) reached for an order + whether it is fully closed. */
function epc_pf_order_target_stage(array $f): array
{
	$step = 1;
	if (!empty($f['so'])) { $step = 2; }
	if (!empty($f['paid'])) { $step = max($step, 3); }
	if (!empty($f['procured'])) { $step = max($step, 4); }
	if (!empty($f['fulfilled'])) { $step = max($step, 5); }
	if (!empty($f['delivered'])) { $step = max($step, 6); }
	$done = false;
	if (!empty($f['invoiced'])) { $step = 7; $done = true; }
	return array('step' => $step, 'done' => $done);
}

/**
 * Create (if missing) and auto-advance the process case for a commerce order so
 * it mirrors the order's real status. Each auto-advanced step is credited to the
 * employee who held it, so it counts toward their task tally. Idempotent and
 * fully guarded — never breaks the calling order/invoice flow. Returns case id.
 */
function epc_pf_sync_order_case(PDO $db, int $orderId): int
{
	if ($orderId <= 0) {
		return 0;
	}
	try {
		epc_pf_ensure_schema($db);
		$f = epc_pf_order_facts($db, $orderId);
		if (empty($f['exists'])) {
			return 0;
		}
		$pid = epc_pf_ensure_order_process($db);
		$cst = $db->prepare("SELECT `id`, `status` FROM `epc_pf_cases` WHERE `subject_type` = 'shop_order' AND `subject_id` = ? AND `process_id` = ? ORDER BY `id` LIMIT 1");
		$cst->execute(array($orderId, $pid));
		$case = $cst->fetch(PDO::FETCH_ASSOC);
		if (!$case) {
			$title = 'Customer order #' . $orderId . ($f['customer'] !== '' ? ' — ' . $f['customer'] : '');
			$caseId = epc_pf_case_start($db, array(
				'process_id' => $pid,
				'title' => $title,
				'reference' => 'Order #' . $orderId,
				'priority' => 'normal',
				'subject_type' => 'shop_order',
				'subject_id' => $orderId,
				'initiator_id' => epc_pf_admin_id(),
			));
		} else {
			$caseId = (int) $case['id'];
			if ((string) $case['status'] !== 'open') {
				return $caseId; // already closed/cancelled/rejected
			}
		}
		$target = epc_pf_order_target_stage($f);
		$goal = $target['done'] ? 99 : (int) $target['step'];
		$guard = 0;
		while ($guard++ < 12) {
			$c = $db->prepare("SELECT `current_step_no`, `status` FROM `epc_pf_cases` WHERE `id` = ?");
			$c->execute(array($caseId));
			$cur = $c->fetch(PDO::FETCH_ASSOC);
			if (!$cur || (string) $cur['status'] !== 'open') {
				break;
			}
			$curNo = (int) $cur['current_step_no'];
			if ($curNo >= $goal) {
				break;
			}
			$as = $db->prepare("SELECT `assignee_id` FROM `epc_pf_case_steps` WHERE `case_id` = ? AND `step_no` = ? AND `status` = 'active' LIMIT 1");
			$as->execute(array($caseId, $curNo));
			$actor = (int) $as->fetchColumn();
			if ($actor <= 0) { $actor = epc_pf_admin_id(); }
			$res = epc_pf_case_act($db, $caseId, 'approve', 'Auto-advanced from order status', $actor);
			if ((string) $res['status'] !== 'open') {
				break;
			}
		}
		return $caseId;
	} catch (Exception $e) {
		return 0;
	}
}

/**
 * Backfill / refresh order cases for recent commerce orders (used by the manual
 * "Sync order cases" button and first-time backfill). Returns count synced.
 */
function epc_pf_sync_all_order_cases(PDO $db, int $limit = 200): int
{
	$n = 0;
	try {
		$ids = $db->query("SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 ORDER BY `id` DESC LIMIT " . max(1, (int) $limit))->fetchAll(PDO::FETCH_COLUMN);
		foreach ($ids as $oid) {
			if (epc_pf_sync_order_case($db, (int) $oid) > 0) { $n++; }
		}
	} catch (Exception $e) {
	}
	return $n;
}

/**
 * Document links for a case, derived from its subject. For order cases this
 * resolves the Fulfilment, Sales order and Invoice ERP pages so the real
 * document opens straight from the process flow.
 *
 * @return array<int,array{label:string,url:string,icon:string}>
 */
function epc_pf_case_documents(PDO $db, array $case): array
{
	$docs = array();
	$type = (string) ($case['subject_type'] ?? '');
	$sid = (int) ($case['subject_id'] ?? 0);
	if ($type !== 'shop_order' || $sid <= 0) {
		return $docs;
	}
	$base = '';
	if (function_exists('epc_erp_cp_redirect_url') && isset($GLOBALS['cfg']) && isset($GLOBALS['cfg']->backend_dir)) {
		$base = epc_erp_cp_redirect_url('/' . $GLOBALS['cfg']->backend_dir . '/shop/finance/erp');
	} else {
		$base = '/shop/finance/erp';
	}
	$f = epc_pf_order_facts($db, $sid);
	$docs[] = array('label' => 'Fulfilment (order #' . $sid . ')', 'url' => $base . '?area=sales&tab=fulfilment&order_id=' . $sid, 'icon' => 'fa-random');
	if (!empty($f['so_id'])) {
		$docs[] = array('label' => 'Sales order', 'url' => $base . '?area=sales&tab=sales_orders&so_id=' . (int) $f['so_id'], 'icon' => 'fa-shopping-cart');
	}
	if (!empty($f['invoice_id'])) {
		$docs[] = array('label' => 'Sales invoice', 'url' => $base . '?area=sales&tab=invoices&inv_id=' . (int) $f['invoice_id'], 'icon' => 'fa-file-text-o');
	}
	return $docs;
}

/** List cases for the monitor, with process name + current step name + step count. */
function epc_pf_cases(PDO $db, array $filters = array()): array
{
	epc_pf_ensure_schema($db);
	$sql = "SELECT c.*, p.`name` AS process_name,
		(SELECT COUNT(*) FROM `epc_pf_case_steps` cs WHERE cs.`case_id` = c.`id`) AS step_count,
		(SELECT cs2.`name` FROM `epc_pf_case_steps` cs2 WHERE cs2.`case_id` = c.`id` AND cs2.`step_no` = c.`current_step_no` LIMIT 1) AS current_step_name
		FROM `epc_pf_cases` c
		LEFT JOIN `epc_pf_processes` p ON p.`id` = c.`process_id`
		WHERE 1=1";
	$params = array();
	if (!empty($filters['status'])) {
		$sql .= " AND c.`status` = ?";
		$params[] = $filters['status'];
	}
	if (!empty($filters['process_id'])) {
		$sql .= " AND c.`process_id` = ?";
		$params[] = (int) $filters['process_id'];
	}
	if (!empty($filters['department'])) {
		$sql .= " AND c.`current_department` = ?";
		$params[] = $filters['department'];
	}
	if (!empty($filters['assignee_id'])) {
		$sql .= " AND c.`current_assignee_id` = ?";
		$params[] = (int) $filters['assignee_id'];
	}
	if (!empty($filters['mine_open'])) {
		$sql .= " AND c.`current_assignee_id` = ? AND c.`status` = 'open'";
		$params[] = (int) $filters['mine_open'];
	}
	$sql .= " ORDER BY FIELD(c.`status`,'open','rejected','done','cancelled'), FIELD(c.`priority`,'urgent','high','normal','low'), c.`due_at` ASC, c.`id` DESC";
	$sql .= " LIMIT " . (int) ($filters['limit'] ?? 200);
	$st = $db->prepare($sql);
	$st->execute($params);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	$now = time();
	foreach ($rows as &$r) {
		$r['assignee_name'] = epc_pf_user_name($db, (int) $r['current_assignee_id']);
		$r['initiator_name'] = epc_pf_user_name($db, (int) $r['initiator_id']);
		$r['overdue'] = ($r['status'] === 'open' && (int) $r['due_at'] > 0 && (int) $r['due_at'] < $now);
	}
	unset($r);
	return $rows;
}

/**
 * Bird's-eye data for the Organization Process Map (Verizon Reveal-style live map):
 * every process with its ordered steps + all open cases positioned at their current step.
 * The view renders departments/employees/tasks as nodes with live counts and animated arrows.
 */
function epc_pf_orgmap_data(PDO $db): array
{
	epc_pf_ensure_schema($db);
	$deptCfg = function_exists('epc_erp_departments_config') ? epc_erp_departments_config() : array();
	$deptName = function ($code) use ($deptCfg) {
		$code = (string) $code;
		if ($code === '') { return 'Unassigned'; }
		if (isset($deptCfg[$code]['name'])) { return $deptCfg[$code]['name']; }
		return function_exists('epc_erp_staff_department_name') ? (epc_erp_staff_department_name($code) ?: ucfirst($code)) : ucfirst($code);
	};

	$procs = array();
	foreach (epc_pf_processes($db) as $p) {
		$pid = (int) $p['id'];
		$steps = array();
		foreach (epc_pf_process_steps($db, $pid) as $s) {
			$steps[] = array(
				'no' => (int) $s['step_no'],
				'name' => (string) $s['name'],
				'dept' => (string) $s['assign_department'],
				'deptName' => $deptName((string) $s['assign_department']),
			);
		}
		$procs[$pid] = array(
			'id' => $pid,
			'name' => (string) $p['name'],
			'steps' => $steps,
			'cases' => array(),
		);
	}

	$now = time();
	$openCases = epc_pf_cases($db, array('status' => 'open', 'limit' => 500));
	foreach ($openCases as $c) {
		$pid = (int) $c['process_id'];
		if (!isset($procs[$pid])) { continue; }
		$procs[$pid]['cases'][] = array(
			'id' => (int) $c['id'],
			'ref' => (string) $c['reference'],
			'title' => (string) $c['title'],
			'priority' => (string) $c['priority'],
			'stepNo' => (int) $c['current_step_no'],
			'stepName' => (string) ($c['current_step_name'] ?? ''),
			'dept' => (string) $c['current_department'],
			'deptName' => $deptName((string) $c['current_department']),
			'location' => (string) ($c['current_location'] ?? ''),
			'bu' => epc_pf_bu_for_dept((string) $c['current_department']),
			'legalEntity' => epc_pf_legal_entity_for_location((string) ($c['current_location'] ?? '')),
			'assigneeId' => (int) $c['current_assignee_id'],
			'assignee' => (string) ($c['assignee_name'] ?? ''),
			'avatar' => epc_pf_avatar_url((string) ($c['assignee_name'] ?? ''), epc_pf_user_photo($db, (int) $c['current_assignee_id'])),
			'overdue' => (bool) $c['overdue'],
			'started' => (int) $c['started_at'],
		);
	}

	// drop processes with neither steps nor open cases to keep the map tidy
	$out = array();
	foreach ($procs as $p) {
		if (!empty($p['steps']) || !empty($p['cases'])) { $out[] = $p; }
	}
	return array(
		'processes' => $out,
		'generated' => $now,
	);
}

/**
 * Whole-workforce snapshot: every active employee with their department, location,
 * and live workload (how many open cases they hold and on which task). Powers the
 * "see all ~200 staff in one view — who's busy on what" workforce board.
 */
function epc_pf_workforce_data(PDO $db): array
{
	epc_pf_ensure_schema($db);
	$deptCfg = function_exists('epc_erp_departments_config') ? epc_erp_departments_config() : array();
	$deptName = function ($code) use ($deptCfg) {
		$code = (string) $code;
		if ($code === '') { return 'Unassigned'; }
		if (isset($deptCfg[$code]['name'])) { return $deptCfg[$code]['name']; }
		return function_exists('epc_erp_staff_department_name') ? (epc_erp_staff_department_name($code) ?: ucfirst($code)) : ucfirst($code);
	};

	// open workload keyed by assignee
	$work = array();
	foreach (epc_pf_cases($db, array('status' => 'open', 'limit' => 1000)) as $c) {
		$aid = (int) $c['current_assignee_id'];
		if ($aid <= 0) { continue; }
		if (!isset($work[$aid])) { $work[$aid] = array(); }
		$work[$aid][] = array(
			'id' => (int) $c['id'],
			'title' => (string) $c['title'],
			'step' => (string) ($c['current_step_name'] ?? ''),
			'ref' => (string) $c['reference'],
			'priority' => (string) $c['priority'],
			'overdue' => (bool) $c['overdue'],
		);
	}

	// performance: tasks completed (steps this person approved/acted on)
	$done = array();
	try {
		foreach ($db->query("SELECT `acted_by` AS u, COUNT(*) AS c FROM `epc_pf_case_steps` WHERE `status` = 'approved' AND `acted_by` > 0 GROUP BY `acted_by`")->fetchAll(PDO::FETCH_ASSOC) as $r) {
			$done[(int) $r['u']] = (int) $r['c'];
		}
	} catch (Exception $e) {
	}

	$staff = array();
	try {
		$rows = $db->query("SELECT `user_id`,`display_name`,`department_code`,`job_title`,`location`,`photo_url`,`business_unit`,`legal_entity` FROM `epc_erp_staff_profiles` WHERE `active` = 1 ORDER BY `department_code`, `display_name`")->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		$rows = array();
	}
	$busy = 0;
	$doneTotal = 0;
	foreach ($rows as $r) {
		$uid = (int) $r['user_id'];
		$tasks = isset($work[$uid]) ? $work[$uid] : array();
		if (!empty($tasks)) { $busy++; }
		$tasksDone = isset($done[$uid]) ? (int) $done[$uid] : 0;
		$doneTotal += $tasksDone;
		$staff[] = array(
			'uid' => $uid,
			'name' => (string) $r['display_name'],
			'dept' => (string) $r['department_code'],
			'deptName' => $deptName((string) $r['department_code']),
			'title' => (string) $r['job_title'],
			'location' => (string) $r['location'],
			'bu' => ((string) ($r['business_unit'] ?? '') !== '') ? (string) $r['business_unit'] : epc_pf_bu_for_dept((string) $r['department_code']),
			'legalEntity' => ((string) ($r['legal_entity'] ?? '') !== '') ? (string) $r['legal_entity'] : epc_pf_legal_entity_for_location((string) $r['location']),
			'avatar' => epc_pf_avatar_url((string) $r['display_name'], (string) ($r['photo_url'] ?? '')),
			'busy' => count($tasks),
			'done' => $tasksDone,
			'tasks' => $tasks,
		);
	}

	return array(
		'staff' => $staff,
		'total' => count($staff),
		'busy' => $busy,
		'idle' => count($staff) - $busy,
		'doneTotal' => $doneTotal,
	);
}

function epc_pf_case_get(PDO $db, int $caseId): ?array
{
	epc_pf_ensure_schema($db);
	$st = $db->prepare("SELECT c.*, p.`name` AS process_name FROM `epc_pf_cases` c LEFT JOIN `epc_pf_processes` p ON p.`id` = c.`process_id` WHERE c.`id` = ?");
	$st->execute(array($caseId));
	$case = $st->fetch(PDO::FETCH_ASSOC);
	if (!$case) {
		return null;
	}
	$case['assignee_name'] = epc_pf_user_name($db, (int) $case['current_assignee_id']);
	$case['initiator_name'] = epc_pf_user_name($db, (int) $case['initiator_id']);
	return $case;
}

/** The "where has it reached" trail for one case. */
function epc_pf_case_timeline(PDO $db, int $caseId): array
{
	epc_pf_ensure_schema($db);
	$st = $db->prepare("SELECT * FROM `epc_pf_case_steps` WHERE `case_id` = ? ORDER BY `step_no`, `id`");
	$st->execute(array($caseId));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$r) {
		$r['assignee_name'] = epc_pf_user_name($db, (int) $r['assignee_id']);
		$r['acted_by_name'] = (int) $r['acted_by'] > 0 ? epc_pf_user_name($db, (int) $r['acted_by']) : '';
	}
	unset($r);
	return $rows;
}

/** Counts for the monitor dashboard. */
function epc_pf_monitor_summary(PDO $db): array
{
	epc_pf_ensure_schema($db);
	$now = time();
	$open = (int) $db->query("SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status`='open'")->fetchColumn();
	$done = (int) $db->query("SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status`='done'")->fetchColumn();
	$rejected = (int) $db->query("SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status`='rejected'")->fetchColumn();
	$overdue = (int) $db->query("SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status`='open' AND `due_at` > 0 AND `due_at` < " . $now)->fetchColumn();
	// average cycle time (hours) for completed cases
	$avg = $db->query("SELECT AVG(`completed_at` - `started_at`) FROM `epc_pf_cases` WHERE `status`='done' AND `completed_at` > 0")->fetchColumn();
	$avgHours = $avg !== null ? round(((float) $avg) / 3600, 1) : 0.0;
	// by department (open cases)
	$byDept = array();
	foreach ($db->query("SELECT `current_department` AS d, COUNT(*) AS c FROM `epc_pf_cases` WHERE `status`='open' GROUP BY `current_department`")->fetchAll(PDO::FETCH_ASSOC) as $r) {
		$byDept[(string) $r['d']] = (int) $r['c'];
	}
	// by current location (open cases) — drives the GPS-style site map
	$byLoc = array();
	foreach ($db->query("SELECT `current_location` AS l, COUNT(*) AS c FROM `epc_pf_cases` WHERE `status`='open' AND `current_location` <> '' GROUP BY `current_location`")->fetchAll(PDO::FETCH_ASSOC) as $r) {
		$byLoc[(string) $r['l']] = (int) $r['c'];
	}
	$headcount = 0;
	try {
		$headcount = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_staff_profiles` WHERE `active` = 1")->fetchColumn();
	} catch (Exception $e) {
	}
	return array('open' => $open, 'done' => $done, 'rejected' => $rejected, 'overdue' => $overdue, 'avg_cycle_hours' => $avgHours, 'by_department' => $byDept, 'by_location' => $byLoc, 'headcount' => $headcount);
}

/* ---------- demo seed / clear ---------- */

/** Demo work locations/branches used by the sample employee population. */
function epc_pf_demo_locations(): array
{
	return array('Dubai HQ', 'Abu Dhabi Branch', 'Sharjah Branch', 'Jebel Ali Warehouse', 'Al Ain Branch');
}

const EPC_PF_DEMO_EMAIL = '@pf-demo.local';
const EPC_PF_DEMO_UID_BASE = 700000;

/**
 * Seed a realistic employee population (~200) spread across every department and
 * multiple physical locations, so workflow cases visibly route between people,
 * departments and sites. One head per department is placed at a rotating
 * location. Idempotent — demo staff are tagged by the @pf-demo.local email.
 *
 * @return array{count:int,heads:array<string,int>,locations:array<int,string>}
 */
function epc_pf_seed_employees(PDO $db): array
{
	epc_pf_ensure_schema($db);
	// clear previous demo staff first
	$db->exec("DELETE FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" . EPC_PF_DEMO_EMAIL . "'");

	$deptCfg = epc_erp_departments_config();
	$deptCodes = array_values(array_filter(array_keys($deptCfg), function ($c) { return $c !== 'admin'; }));
	$locations = epc_pf_demo_locations();

	$first = array('Ahmed', 'Mohammed', 'Fatima', 'Sara', 'Omar', 'Layla', 'Yusuf', 'Aisha', 'Khalid', 'Noura', 'Hassan', 'Mariam', 'Ali', 'Huda', 'Rashid', 'Salma', 'Tariq', 'Reem', 'Faisal', 'Mona', 'Bilal', 'Zainab', 'Imran', 'Dana', 'Saeed');
	$last = array('Al Mansoori', 'Khan', 'Hussain', 'Al Marri', 'Sharma', 'Patel', 'Al Naqbi', 'Rahman', 'Mehta', 'Al Suwaidi', 'Iqbal', 'Nair', 'Al Hashimi', 'Farooq', 'Das', 'Al Balushi', 'Siddiqui', 'Kapoor', 'Al Zaabi', 'Joseph');

	$titlesByDept = array(
		'sales' => array('Sales Executive', 'Account Manager', 'Sales Officer', 'Business Dev Exec'),
		'logistics' => array('Logistics Officer', 'Warehouse Supervisor', 'Dispatch Coordinator', 'Fleet Officer'),
		'marketing' => array('Marketing Executive', 'Content Specialist', 'Brand Officer', 'Digital Marketer'),
		'finance' => array('Accountant', 'Finance Officer', 'AP/AR Specialist', 'Treasury Officer'),
		'hr' => array('HR Officer', 'Recruiter', 'Payroll Officer', 'HR Coordinator'),
		'it' => array('IT Support', 'System Admin', 'Developer', 'Network Officer'),
		'purchase' => array('Buyer', 'Procurement Officer', 'Sourcing Specialist', 'Vendor Coordinator'),
		'accounts' => array('Accounts Officer', 'Bookkeeper', 'GL Accountant', 'Audit Assistant'),
	);

	$uid = EPC_PF_DEMO_UID_BASE;
	$seq = 0;
	$heads = array();
	$count = 0;
	$now = time();
	$ins = $db->prepare("INSERT INTO `epc_erp_staff_profiles`
		(`user_id`,`department_code`,`display_name`,`job_title`,`email`,`phone`,`location`,`business_unit`,`legal_entity`,`active`,`time_created`)
		VALUES (?,?,?,?,?,?,?,?,?,1,?)");

	foreach ($deptCodes as $d => $code) {
		$deptName = isset($deptCfg[$code]['name']) ? $deptCfg[$code]['name'] : ucfirst($code);
		$titles = $titlesByDept[$code] ?? array('Officer', 'Specialist', 'Coordinator', 'Associate');
		$headLoc = $locations[$d % count($locations)];

		// department head
		$uid++; $seq++;
		$hName = $first[$seq % count($first)] . ' ' . $last[$seq % count($last)];
		$hEmail = strtolower(str_replace(' ', '.', $hName)) . '.' . $seq . EPC_PF_DEMO_EMAIL;
		$ins->execute(array($uid, $code, $hName, 'Head of ' . $deptName, $hEmail, '+9715' . (1000000 + $seq), $headLoc, epc_pf_bu_for_dept($code), epc_pf_legal_entity_for_location($headLoc), $now));
		$heads[$code] = $uid;
		$count++;

		// staff across all locations
		foreach ($locations as $loc) {
			$perLoc = 5;
			for ($k = 0; $k < $perLoc; $k++) {
				$uid++; $seq++;
				$name = $first[$seq % count($first)] . ' ' . $last[($seq * 3) % count($last)];
				$email = strtolower(str_replace(' ', '.', $name)) . '.' . $seq . EPC_PF_DEMO_EMAIL;
				$title = $titles[$k % count($titles)];
				$ins->execute(array($uid, $code, $name, $title, $email, '+9715' . (1000000 + $seq), $loc, epc_pf_bu_for_dept($code), epc_pf_legal_entity_for_location($loc), $now));
				$count++;
			}
		}
	}

	return array('count' => $count, 'heads' => $heads, 'locations' => $locations);
}

function epc_pf_clear_demo(PDO $db): int
{
	epc_pf_ensure_schema($db);
	$ids = $db->query("SELECT `id` FROM `epc_pf_cases` WHERE `reference` LIKE 'DEMO-PF%'")->fetchAll(PDO::FETCH_COLUMN);
	$n = 0;
	foreach ($ids as $cid) {
		$db->prepare("DELETE FROM `epc_pf_case_steps` WHERE `case_id` = ?")->execute(array((int) $cid));
		$db->prepare("DELETE FROM `epc_pf_cases` WHERE `id` = ?")->execute(array((int) $cid));
		$n++;
	}
	$pids = $db->query("SELECT `id` FROM `epc_pf_processes` WHERE `category` = 'demo'")->fetchAll(PDO::FETCH_COLUMN);
	foreach ($pids as $pid) {
		$db->prepare("DELETE FROM `epc_pf_steps` WHERE `process_id` = ?")->execute(array((int) $pid));
		$db->prepare("DELETE FROM `epc_pf_processes` WHERE `id` = ?")->execute(array((int) $pid));
	}
	// demo employees + their dept-head pointers
	try {
		$demoUids = $db->query("SELECT `user_id` FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" . EPC_PF_DEMO_EMAIL . "'")->fetchAll(PDO::FETCH_COLUMN);
		if (!empty($demoUids)) {
			$in = implode(',', array_map('intval', $demoUids));
			$db->exec("DELETE FROM `epc_pf_dept_heads` WHERE `head_user_id` IN ($in)");
		}
		$db->exec("DELETE FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" . EPC_PF_DEMO_EMAIL . "'");
	} catch (Exception $e) {
	}
	return $n;
}

/**
 * Seed a few realistic multi-department processes and running cases at various
 * stages so the monitor shows a meaningful chain. Idempotent (clears first).
 */
function epc_pf_seed_demo(PDO $db): array
{
	epc_pf_ensure_schema($db);
	epc_pf_clear_demo($db);

	// seed the employee population (multi-department, multi-location)
	$emp = epc_pf_seed_employees($db);
	$deptCfg = epc_erp_departments_config();
	$adminUser = epc_pf_user_id();
	foreach (array_keys($deptCfg) as $code) {
		$uid = isset($emp['heads'][$code]) ? (int) $emp['heads'][$code] : 0;
		if ($uid <= 0) {
			$uid = $adminUser;
		}
		epc_pf_set_dept_head($db, $code, $uid);
	}

	$blueprints = array(
		array(
			'name' => 'Purchase requisition approval',
			'category' => 'demo',
			'description' => 'Requisition raised by a department, reviewed by its head, sourced by Purchase, signed off by Finance.',
			'steps' => array(
				array('name' => 'Raise requisition', 'assign_type' => 'department', 'assign_department' => 'sales', 'sla_hours' => 8),
				array('name' => 'Department head review', 'assign_type' => 'dept_head', 'assign_department' => 'sales', 'sla_hours' => 24),
				array('name' => 'Source & raise PO', 'assign_type' => 'dept_head', 'assign_department' => 'purchase', 'sla_hours' => 48),
				array('name' => 'Finance sign-off', 'assign_type' => 'dept_head', 'assign_department' => 'finance', 'sla_hours' => 24),
			),
		),
		array(
			'name' => 'Customer complaint resolution',
			'category' => 'demo',
			'description' => 'Complaint logged, triaged by Sales head, resolved by Logistics, closed by Sales.',
			'steps' => array(
				array('name' => 'Log complaint', 'assign_type' => 'department', 'assign_department' => 'sales', 'sla_hours' => 4),
				array('name' => 'Sales head triage', 'assign_type' => 'dept_head', 'assign_department' => 'sales', 'sla_hours' => 12),
				array('name' => 'Logistics resolution', 'assign_type' => 'dept_head', 'assign_department' => 'logistics', 'sla_hours' => 48),
				array('name' => 'Confirm & close', 'assign_type' => 'initiator', 'assign_department' => 'sales', 'sla_hours' => 12),
			),
		),
		array(
			'name' => 'New staff onboarding',
			'category' => 'demo',
			'description' => 'HR initiates, department head confirms role, IT grants access, Finance sets up payroll.',
			'steps' => array(
				array('name' => 'HR initiate onboarding', 'assign_type' => 'dept_head', 'assign_department' => 'hr', 'sla_hours' => 24),
				array('name' => 'Department head confirm role', 'assign_type' => 'dept_head', 'assign_department' => 'sales', 'sla_hours' => 24),
				array('name' => 'IT grant ERP/CP access', 'assign_type' => 'dept_head', 'assign_department' => 'it', 'sla_hours' => 24),
				array('name' => 'Finance set up payroll', 'assign_type' => 'dept_head', 'assign_department' => 'finance', 'sla_hours' => 24),
			),
		),
		array(
			'name' => 'Customer credit request',
			'category' => 'demo',
			'description' => 'Customer asks for a credit limit. Sales logs it, Sales head reviews, Finance runs a credit check, Finance head approves the limit, Sales confirms back to the customer.',
			'steps' => array(
				array('name' => 'Sales log credit request', 'assign_type' => 'department', 'assign_department' => 'sales', 'sla_hours' => 4),
				array('name' => 'Sales head review', 'assign_type' => 'dept_head', 'assign_department' => 'sales', 'sla_hours' => 12),
				array('name' => 'Finance credit check', 'assign_type' => 'department', 'assign_department' => 'finance', 'sla_hours' => 24),
				array('name' => 'Finance head approve limit', 'assign_type' => 'dept_head', 'assign_department' => 'finance', 'sla_hours' => 24),
				array('name' => 'Sales confirm to customer', 'assign_type' => 'initiator', 'assign_department' => 'sales', 'sla_hours' => 8),
			),
		),
		array(
			'name' => 'Goods delivery to customer',
			'category' => 'demo',
			'description' => 'Sales raises the delivery, Logistics arranges it, a driver dispatches and delivers, goods are unloaded, the customer confirms, and Sales records the signed delivery-note receipt.',
			'steps' => array(
				array('name' => 'Sales raise delivery order', 'assign_type' => 'department', 'assign_department' => 'sales', 'sla_hours' => 6),
				array('name' => 'Logistics arrange delivery', 'assign_type' => 'dept_head', 'assign_department' => 'logistics', 'sla_hours' => 12),
				array('name' => 'Driver dispatch & drive', 'assign_type' => 'department', 'assign_department' => 'logistics', 'sla_hours' => 24),
				array('name' => 'Unload goods at customer', 'assign_type' => 'department', 'assign_department' => 'logistics', 'sla_hours' => 6),
				array('name' => 'Customer confirms receipt', 'assign_type' => 'initiator', 'assign_department' => 'sales', 'sla_hours' => 12),
				array('name' => 'Sales record delivery-note receipt', 'assign_type' => 'dept_head', 'assign_department' => 'sales', 'sla_hours' => 8),
			),
		),
	);

	$procIds = array();
	foreach ($blueprints as $bp) {
		$pid = epc_pf_process_save($db, array('name' => $bp['name'], 'description' => $bp['description'], 'category' => 'demo'));
		$no = 1;
		foreach ($bp['steps'] as $s) {
			epc_pf_step_save($db, array(
				'process_id' => $pid,
				'step_no' => $no++,
				'name' => $s['name'],
				'assign_type' => $s['assign_type'],
				'assign_department' => $s['assign_department'],
				'sla_hours' => $s['sla_hours'],
			));
		}
		$procIds[] = $pid;
	}

	// pool of demo employees to act as initiators (gives located start points)
	$demoEmps = $db->query("SELECT `user_id` FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" . EPC_PF_DEMO_EMAIL . "' ORDER BY RAND() LIMIT 40")->fetchAll(PDO::FETCH_COLUMN);
	$pickEmp = function () use ($demoEmps, $adminUser) {
		if (empty($demoEmps)) { return $adminUser; }
		return (int) $demoEmps[array_rand($demoEmps)];
	};

	// start cases at various stages so the monitor + tracking views are alive.
	// each row: title, priority, process index (into $procIds), steps to advance
	$titles = array(
		array('Restock packaging materials', 'high', 0, 0),
		array('Office laptops requisition', 'normal', 0, 1),
		array('Damaged shipment — order #10231', 'urgent', 1, 2),
		array('Late delivery complaint — order #10244', 'high', 1, 3),
		array('Onboard A. Khan (Sales exec)', 'normal', 2, 0),
		array('Onboard R. Mehta (Warehouse)', 'normal', 2, 1),
		array('Spare parts purchase — Abu Dhabi', 'high', 0, 2),
		array('Wrong item delivered — order #10310', 'urgent', 1, 3),
		array('Onboard S. Patel (Finance)', 'normal', 2, 0),
		array('Forklift maintenance requisition', 'normal', 0, 1),
		array('Refund dispute — order #10355', 'high', 1, 2),
		array('Onboard D. Joseph (IT support)', 'low', 2, 3),
		array('Credit request — Gulf Spare Parts LLC', 'high', 3, 2),
		array('Credit request — Al Noor Motors', 'normal', 3, 4),
		array('Delivery — order #10410 to Sharjah', 'urgent', 4, 3),
		array('Delivery — order #10422 to Al Ain', 'high', 4, 5),
	);
	$cases = 0;
	foreach ($titles as $i => $t) {
		$pid = $procIds[$t[2]] ?? $procIds[$i % count($procIds)];
		$cid = epc_pf_case_start($db, array(
			'process_id' => $pid,
			'title' => $t[0],
			'priority' => $t[1],
			'reference' => 'DEMO-PF-' . ($i + 1),
			'initiator_id' => $pickEmp(),
		));
		$cases++;
		// advance the configured number of steps to spread cases across the chain.
		// credit each completed step to the employee who held it so the Workforce
		// performance leaderboard reflects real task counts.
		$advance = (int) $t[3];
		for ($a = 0; $a < $advance; $a++) {
			$curC = $db->query("SELECT `current_step_no` FROM `epc_pf_cases` WHERE `id` = " . (int) $cid)->fetchColumn();
			$asq = $db->prepare("SELECT `assignee_id` FROM `epc_pf_case_steps` WHERE `case_id` = ? AND `step_no` = ? AND `status` = 'active' LIMIT 1");
			$asq->execute(array($cid, (int) $curC));
			$actor = (int) $asq->fetchColumn();
			if ($actor <= 0) { $actor = $adminUser; }
			$res = epc_pf_case_act($db, $cid, 'approve', 'Reviewed and approved (sample data)', $actor);
			if ($res['status'] !== 'open') {
				break;
			}
		}
	}

	return array('processes' => count($procIds), 'cases' => $cases, 'employees' => (int) $emp['count']);
}
