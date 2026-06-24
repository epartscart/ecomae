<?php
/**
 * GL Period Close — month-level period management with soft-close, lock, and checklist.
 *
 * Periods: open → soft_close → locked
 *   open:       Normal posting allowed
 *   soft_close: Warning shown, only super_role can post
 *   locked:     No posting allowed (integrates with epc_erp_fiscal_periods.php)
 *
 * Checklist: pre-close validation of outstanding items (draft SO, open PO, unposted journals)
 */
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

require_once __DIR__ . '/epc_erp_fiscal_periods.php';

/* ─────────────────── Schema ─────────────────── */

function epc_erp_period_close_ensure_schema(PDO $db): void
{
	static $done = false;
	if ($done) { return; }
	$done = true;

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_erp_periods` (
			`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`year_month` CHAR(7) NOT NULL,
			`year` SMALLINT NOT NULL,
			`month` TINYINT NOT NULL,
			`status` ENUM(\'open\',\'soft_close\',\'locked\') NOT NULL DEFAULT \'open\',
			`closed_by` INT NOT NULL DEFAULT 0,
			`closed_at` INT NOT NULL DEFAULT 0,
			`locked_by` INT NOT NULL DEFAULT 0,
			`locked_at` INT NOT NULL DEFAULT 0,
			`note` VARCHAR(512) NOT NULL DEFAULT \'\',
			`checklist_json` TEXT,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `idx_year_month` (`year_month`),
			INDEX `idx_status` (`status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_erp_period_close_log` (
			`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`year_month` CHAR(7) NOT NULL,
			`action` VARCHAR(32) NOT NULL,
			`old_status` VARCHAR(16) NOT NULL DEFAULT \'\',
			`new_status` VARCHAR(16) NOT NULL DEFAULT \'\',
			`admin_id` INT NOT NULL DEFAULT 0,
			`note` VARCHAR(512) NOT NULL DEFAULT \'\',
			`created_at` INT NOT NULL DEFAULT 0,
			INDEX `idx_period` (`year_month`, `created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);
}

/* ─────────────────── Period CRUD ─────────────────── */

function epc_erp_period_get(PDO $db, string $yearMonth): array
{
	epc_erp_period_close_ensure_schema($db);

	$st = $db->prepare('SELECT * FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1');
	$st->execute(array($yearMonth));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$row['checklist'] = json_decode($row['checklist_json'] ?? '[]', true) ?: array();
		return $row;
	}

	// Auto-create if not exists
	return epc_erp_period_ensure($db, $yearMonth);
}

function epc_erp_period_ensure(PDO $db, string $yearMonth): array
{
	epc_erp_period_close_ensure_schema($db);

	if (!preg_match('/^\d{4}-\d{2}$/', $yearMonth)) {
		throw new \Exception('Invalid year_month format: ' . $yearMonth);
	}

	$parts = explode('-', $yearMonth);
	$year = (int) $parts[0];
	$month = (int) $parts[1];
	$now = time();

	try {
		$db->prepare(
			'INSERT INTO `epc_erp_periods` (`year_month`, `year`, `month`, `status`, `created_at`, `updated_at`)
			 VALUES (?, ?, ?, \'open\', ?, ?)
			 ON DUPLICATE KEY UPDATE `year_month` = `year_month`'
		)->execute(array($yearMonth, $year, $month, $now, $now));
	} catch (\PDOException $e) {
		// Ignore duplicate
	}

	$st = $db->prepare('SELECT * FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1');
	$st->execute(array($yearMonth));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$row['checklist'] = json_decode($row['checklist_json'] ?? '[]', true) ?: array();
	return $row;
}

function epc_erp_period_list(PDO $db, int $limit = 24): array
{
	epc_erp_period_close_ensure_schema($db);

	// Ensure current and recent months exist
	$current = date('Y-m');
	for ($i = 0; $i < 3; $i++) {
		$ym = date('Y-m', strtotime("-$i months"));
		epc_erp_period_ensure($db, $ym);
	}

	$st = $db->prepare(
		'SELECT * FROM `epc_erp_periods` ORDER BY `year_month` DESC LIMIT ?'
	);
	$st->execute(array($limit));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$r) {
		$r['checklist'] = json_decode($r['checklist_json'] ?? '[]', true) ?: array();
	}
	return $rows;
}

/* ─────────────────── Status Transitions ─────────────────── */

function epc_erp_period_soft_close(PDO $db, string $yearMonth, int $adminId = 0, string $note = ''): array
{
	$period = epc_erp_period_get($db, $yearMonth);
	if ($period['status'] === 'locked') {
		return array('ok' => false, 'error' => 'Period is already locked. Reopen first to modify.');
	}
	if ($period['status'] === 'soft_close') {
		return array('ok' => false, 'error' => 'Period is already in soft-close state.');
	}

	$now = time();
	$db->prepare(
		'UPDATE `epc_erp_periods` SET `status` = \'soft_close\', `closed_by` = ?, `closed_at` = ?, `note` = ?, `updated_at` = ? WHERE `year_month` = ?'
	)->execute(array($adminId, $now, $note, $now, $yearMonth));

	epc_erp_period_close_log($db, $yearMonth, 'soft_close', $period['status'], 'soft_close', $adminId, $note);

	return array('ok' => true, 'status' => 'soft_close', 'year_month' => $yearMonth);
}

function epc_erp_period_lock(PDO $db, string $yearMonth, int $adminId = 0, string $note = ''): array
{
	$period = epc_erp_period_get($db, $yearMonth);
	if ($period['status'] === 'locked') {
		return array('ok' => false, 'error' => 'Period is already locked.');
	}

	$now = time();

	// Run pre-close checklist
	$checklist = epc_erp_period_checklist($db, $yearMonth);
	$hasBlockers = false;
	foreach ($checklist as $item) {
		if ($item['severity'] === 'blocker' && $item['count'] > 0) {
			$hasBlockers = true;
			break;
		}
	}
	if ($hasBlockers) {
		return array(
			'ok' => false,
			'error' => 'Cannot lock period — resolve blockers first.',
			'checklist' => $checklist,
		);
	}

	// Set fiscal lock date to last day of the month
	$parts = explode('-', $yearMonth);
	$lastDay = mktime(23, 59, 59, (int) $parts[1], (int) date('t', mktime(0, 0, 0, (int) $parts[1], 1, (int) $parts[0])), (int) $parts[0]);
	epc_erp_fiscal_set_lock($db, $lastDay, 'Period lock: ' . $yearMonth);

	$db->prepare(
		'UPDATE `epc_erp_periods` SET `status` = \'locked\', `locked_by` = ?, `locked_at` = ?,
		 `note` = ?, `checklist_json` = ?, `updated_at` = ? WHERE `year_month` = ?'
	)->execute(array($adminId, $now, $note, json_encode($checklist), $now, $yearMonth));

	epc_erp_period_close_log($db, $yearMonth, 'lock', $period['status'], 'locked', $adminId, $note);

	return array('ok' => true, 'status' => 'locked', 'year_month' => $yearMonth, 'checklist' => $checklist);
}

function epc_erp_period_reopen(PDO $db, string $yearMonth, int $adminId = 0, string $note = ''): array
{
	$period = epc_erp_period_get($db, $yearMonth);
	if ($period['status'] === 'open') {
		return array('ok' => false, 'error' => 'Period is already open.');
	}

	$now = time();

	// Clear fiscal lock if this is the current lock date
	$parts = explode('-', $yearMonth);
	$lastDay = mktime(23, 59, 59, (int) $parts[1], (int) date('t', mktime(0, 0, 0, (int) $parts[1], 1, (int) $parts[0])), (int) $parts[0]);
	$currentLock = epc_erp_fiscal_lock_date($db);
	if ($currentLock > 0 && $currentLock <= $lastDay) {
		// Find the previous locked period
		$prevSt = $db->prepare(
			'SELECT `year_month` FROM `epc_erp_periods` WHERE `status` = \'locked\' AND `year_month` < ? ORDER BY `year_month` DESC LIMIT 1'
		);
		$prevSt->execute(array($yearMonth));
		$prevLocked = $prevSt->fetchColumn();

		if ($prevLocked) {
			$pp = explode('-', $prevLocked);
			$prevLastDay = mktime(23, 59, 59, (int) $pp[1], (int) date('t', mktime(0, 0, 0, (int) $pp[1], 1, (int) $pp[0])), (int) $pp[0]);
			epc_erp_fiscal_set_lock($db, $prevLastDay, 'Reopened ' . $yearMonth . '; lock reverted to ' . $prevLocked);
		} else {
			epc_erp_fiscal_set_lock($db, 0, 'Reopened ' . $yearMonth . '; all periods now open');
		}
	}

	$db->prepare(
		'UPDATE `epc_erp_periods` SET `status` = \'open\', `note` = ?, `updated_at` = ? WHERE `year_month` = ?'
	)->execute(array($note, $now, $yearMonth));

	epc_erp_period_close_log($db, $yearMonth, 'reopen', $period['status'], 'open', $adminId, $note);

	return array('ok' => true, 'status' => 'open', 'year_month' => $yearMonth);
}

/* ─────────────────── Posting Guard ─────────────────── */

function epc_erp_period_posting_allowed(PDO $db, int $journalDate, string $userRole = ''): array
{
	$yearMonth = date('Y-m', $journalDate);
	$period = epc_erp_period_get($db, $yearMonth);

	$superRoles = array('super_admin', 'finance_admin', 'finance_controller');

	switch ($period['status']) {
		case 'open':
			return array('allowed' => true, 'warning' => '');

		case 'soft_close':
			if (in_array($userRole, $superRoles, true)) {
				return array('allowed' => true, 'warning' => 'Period ' . $yearMonth . ' is in soft-close. Posting allowed for your role.');
			}
			return array('allowed' => false, 'warning' => 'Period ' . $yearMonth . ' is soft-closed. Only finance controllers can post.');

		case 'locked':
			return array('allowed' => false, 'warning' => 'Period ' . $yearMonth . ' is locked. No posting allowed.');
	}

	return array('allowed' => true, 'warning' => '');
}

/* ─────────────────── Pre-Close Checklist ─────────────────── */

function epc_erp_period_checklist(PDO $db, string $yearMonth): array
{
	epc_erp_period_close_ensure_schema($db);

	$parts = explode('-', $yearMonth);
	$year = (int) $parts[0];
	$month = (int) $parts[1];
	$startTs = mktime(0, 0, 0, $month, 1, $year);
	$endTs = mktime(23, 59, 59, $month, (int) date('t', $startTs), $year);

	$items = array();

	// 1. Draft Sales Orders
	$draftSO = 0;
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = \'draft\' AND `time_created` >= ? AND `time_created` <= ?'
		);
		$st->execute(array($startTs, $endTs));
		$draftSO = (int) $st->fetchColumn();
	} catch (\PDOException $e) { /* table may not exist */ }

	$items[] = array(
		'id'       => 'draft_so',
		'label'    => 'Draft Sales Orders',
		'count'    => $draftSO,
		'severity' => 'warning',
		'help'     => 'Confirm or cancel draft sales orders before closing the period.',
	);

	// 2. Open Purchase Orders
	$openPO = 0;
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `status` IN (\'draft\',\'pending\') AND `order_date` >= ? AND `order_date` <= ?'
		);
		$st->execute(array($startTs, $endTs));
		$openPO = (int) $st->fetchColumn();
	} catch (\PDOException $e) { /* table may not exist */ }

	$items[] = array(
		'id'       => 'open_po',
		'label'    => 'Open Purchase Orders',
		'count'    => $openPO,
		'severity' => 'warning',
		'help'     => 'Receive or cancel open purchase orders before closing.',
	);

	// 3. Unposted GL Journals (draft/unbalanced)
	$unpostedJournals = 0;
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `status` = \'draft\' AND `active` = 1 AND `journal_date` >= ? AND `journal_date` <= ?'
		);
		$st->execute(array($startTs, $endTs));
		$unpostedJournals = (int) $st->fetchColumn();
	} catch (\PDOException $e) { /* table may not exist */ }

	$items[] = array(
		'id'       => 'unposted_journals',
		'label'    => 'Unposted GL Journals',
		'count'    => $unpostedJournals,
		'severity' => 'blocker',
		'help'     => 'Post or delete draft journals. Unposted journals prevent period lock.',
	);

	// 4. Unreconciled Bank Entries
	$unreconciledBank = 0;
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `epc_erp_cash_bank_entries` WHERE `reconciled` = 0 AND `entry_date` >= ? AND `entry_date` <= ?'
		);
		$st->execute(array($startTs, $endTs));
		$unreconciledBank = (int) $st->fetchColumn();
	} catch (\PDOException $e) { /* table may not exist */ }

	$items[] = array(
		'id'       => 'unreconciled_bank',
		'label'    => 'Unreconciled Bank Entries',
		'count'    => $unreconciledBank,
		'severity' => 'warning',
		'help'     => 'Reconcile bank entries for accurate reporting.',
	);

	// 5. Unpaid Sales Invoices
	$unpaidInvoices = 0;
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `epc_erp_sales_invoices` WHERE `status` = \'unpaid\' AND `invoice_date` >= ? AND `invoice_date` <= ?'
		);
		$st->execute(array($startTs, $endTs));
		$unpaidInvoices = (int) $st->fetchColumn();
	} catch (\PDOException $e) { /* table may not exist */ }

	$items[] = array(
		'id'       => 'unpaid_invoices',
		'label'    => 'Unpaid Sales Invoices',
		'count'    => $unpaidInvoices,
		'severity' => 'info',
		'help'     => 'Outstanding invoices — OK to close but flagged for awareness.',
	);

	// 6. E-invoice Submissions Pending
	$pendingEinvoice = 0;
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `status` IN (\'draft\',\'queued\') AND `issue_date` >= ? AND `issue_date` <= ?'
		);
		$st->execute(array($startTs, $endTs));
		$pendingEinvoice = (int) $st->fetchColumn();
	} catch (\PDOException $e) { /* table may not exist */ }

	$items[] = array(
		'id'       => 'pending_einvoice',
		'label'    => 'Pending E-Invoice Submissions',
		'count'    => $pendingEinvoice,
		'severity' => 'warning',
		'help'     => 'Submit e-invoices to ASP before closing for FTA compliance.',
	);

	// 7. Inventory Period Closing (snapshot)
	$invClosed = false;
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `epc_erp_inv_closing_snapshots` WHERE `period` = ? LIMIT 1'
		);
		$st->execute(array($yearMonth));
		$invClosed = (int) $st->fetchColumn() > 0;
	} catch (\PDOException $e) { /* table may not exist — OK */ }

	$items[] = array(
		'id'       => 'inv_snapshot',
		'label'    => 'Inventory Period Snapshot',
		'count'    => $invClosed ? 0 : 1,
		'severity' => 'warning',
		'help'     => $invClosed ? 'Inventory snapshot exists for this period.' : 'Run inventory period close to create a valuation snapshot.',
	);

	return $items;
}

/* ─────────────────── Audit Log ─────────────────── */

function epc_erp_period_close_log(PDO $db, string $yearMonth, string $action, string $oldStatus, string $newStatus, int $adminId = 0, string $note = ''): void
{
	epc_erp_period_close_ensure_schema($db);
	$db->prepare(
		'INSERT INTO `epc_erp_period_close_log` (`year_month`, `action`, `old_status`, `new_status`, `admin_id`, `note`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?)'
	)->execute(array($yearMonth, $action, $oldStatus, $newStatus, $adminId, $note, time()));
}

function epc_erp_period_close_log_list(PDO $db, string $yearMonth = '', int $limit = 50): array
{
	epc_erp_period_close_ensure_schema($db);
	if ($yearMonth !== '') {
		$st = $db->prepare('SELECT * FROM `epc_erp_period_close_log` WHERE `year_month` = ? ORDER BY `created_at` DESC LIMIT ?');
		$st->execute(array($yearMonth, $limit));
	} else {
		$st = $db->prepare('SELECT * FROM `epc_erp_period_close_log` ORDER BY `created_at` DESC LIMIT ?');
		$st->execute(array($limit));
	}
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

/* ─────────────────── Dashboard Summary ─────────────────── */

function epc_erp_period_close_summary(PDO $db): array
{
	epc_erp_period_close_ensure_schema($db);

	$current = date('Y-m');

	$st = $db->query(
		'SELECT `status`, COUNT(*) AS `count` FROM `epc_erp_periods` GROUP BY `status`'
	);
	$statusCounts = $st->fetchAll(PDO::FETCH_KEY_PAIR);

	$currentPeriod = epc_erp_period_get($db, $current);
	$checklist = epc_erp_period_checklist($db, $current);

	$blockersCount = 0;
	$warningsCount = 0;
	foreach ($checklist as $item) {
		if ($item['count'] > 0) {
			if ($item['severity'] === 'blocker') { $blockersCount++; }
			elseif ($item['severity'] === 'warning') { $warningsCount++; }
		}
	}

	return array(
		'current_period'  => $current,
		'current_status'  => $currentPeriod['status'],
		'fiscal_lock_date' => epc_erp_fiscal_lock_date($db),
		'open_periods'    => (int) ($statusCounts['open'] ?? 0),
		'soft_close_periods' => (int) ($statusCounts['soft_close'] ?? 0),
		'locked_periods'  => (int) ($statusCounts['locked'] ?? 0),
		'checklist_blockers' => $blockersCount,
		'checklist_warnings' => $warningsCount,
		'checklist'       => $checklist,
	);
}

/* ─────────────────── Period Lock API ─────────────────── */

/**
 * Assert that a posting date falls within an open period.
 * Prevents posting to locked or soft-closed periods.
 *
 * @throws \RuntimeException If period is locked
 */
function epc_erp_gl_assert_period_open(PDO $db, int $postingTimestamp, string $callerRole = ''): bool
{
	$yearMonth = date('Y-m', $postingTimestamp);
	$period = epc_erp_period_get($db, $yearMonth);

	if ($period['status'] === 'locked') {
		throw new \RuntimeException(
			'Period ' . $yearMonth . ' is locked. Cannot post transactions to a locked period.'
		);
	}

	if ($period['status'] === 'soft_close') {
		$superRoles = array('super_admin', 'finance_director', 'cfo');
		if (!in_array($callerRole, $superRoles, true)) {
			throw new \RuntimeException(
				'Period ' . $yearMonth . ' is soft-closed. Only finance directors can post.'
			);
		}
	}

	return true;
}

/**
 * Lock a period — no further posting allowed.
 */
function epc_erp_gl_lock_period(PDO $db, string $yearMonth, int $adminId = 0, string $note = ''): array
{
	$period = epc_erp_period_get($db, $yearMonth);
	$oldStatus = $period['status'];

	if ($oldStatus === 'locked') {
		return array('ok' => true, 'message' => 'Period already locked', 'status' => 'locked');
	}

	if ($oldStatus === 'open') {
		$checklist = epc_erp_period_checklist($db, $yearMonth);
		$blockers = 0;
		foreach ($checklist as $item) {
			if ($item['count'] > 0 && $item['severity'] === 'blocker') {
				$blockers++;
			}
		}
		if ($blockers > 0) {
			return array(
				'ok' => false,
				'message' => 'Cannot lock: ' . $blockers . ' blocker(s) outstanding. Soft-close first.',
				'blockers' => $blockers,
				'checklist' => $checklist,
			);
		}
	}

	$db->prepare(
		'UPDATE `epc_erp_periods` SET `status` = \'locked\', `locked_by` = ?, `locked_at` = ?, `updated_at` = ?
		 WHERE `year_month` = ?'
	)->execute(array($adminId, time(), time(), $yearMonth));

	epc_erp_period_close_log($db, $yearMonth, 'lock', $oldStatus, 'locked', $adminId, $note);

	return array('ok' => true, 'message' => 'Period ' . $yearMonth . ' locked', 'status' => 'locked');
}

/**
 * Unlock a period (emergency override).
 */
function epc_erp_gl_unlock_period(PDO $db, string $yearMonth, int $adminId = 0, string $reason = ''): array
{
	$period = epc_erp_period_get($db, $yearMonth);
	if ($period['status'] !== 'locked') {
		return array('ok' => false, 'message' => 'Period is not locked');
	}

	$db->prepare(
		'UPDATE `epc_erp_periods` SET `status` = \'open\', `updated_at` = ?
		 WHERE `year_month` = ?'
	)->execute(array(time(), $yearMonth));

	epc_erp_period_close_log($db, $yearMonth, 'unlock', 'locked', 'open', $adminId, 'EMERGENCY UNLOCK: ' . $reason);

	return array('ok' => true, 'message' => 'Period ' . $yearMonth . ' unlocked (emergency)', 'status' => 'open');
}

/**
 * Bulk lock all periods before a given month.
 */
function epc_erp_gl_bulk_lock_before(PDO $db, string $beforeYearMonth, int $adminId = 0): array
{
	epc_erp_period_close_ensure_schema($db);
	$st = $db->prepare(
		'SELECT `year_month` FROM `epc_erp_periods` WHERE `status` != \'locked\' AND `year_month` < ?'
	);
	$st->execute(array($beforeYearMonth));
	$periods = $st->fetchAll(PDO::FETCH_COLUMN);

	$locked = array();
	$failed = array();
	foreach ($periods as $ym) {
		$result = epc_erp_gl_lock_period($db, $ym, $adminId, 'Bulk lock before ' . $beforeYearMonth);
		if ($result['ok']) {
			$locked[] = $ym;
		} else {
			$failed[] = array('year_month' => $ym, 'reason' => $result['message']);
		}
	}
	return array('locked' => $locked, 'failed' => $failed);
}
