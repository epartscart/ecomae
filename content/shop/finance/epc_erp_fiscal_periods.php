<?php
/**
 * Fiscal-period locking for the BOS GL.
 *
 * A lock records a cut-off date; any GL journal dated on or before the active
 * lock date is rejected at posting time. This implements the "fiscal period
 * locking" accounting control (no back-dated posting into closed periods).
 *
 * The control is opt-in: with no lock set, posting behaves exactly as before.
 * Per-tenant, like every other BOS table.
 */

if (!function_exists('epc_erp_fiscal_ensure_schema')) {

	function epc_erp_fiscal_ensure_schema(PDO $db)
	{
		$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_fiscal_locks` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`lock_date` int(11) NOT NULL,
			`note` varchar(255) DEFAULT NULL,
			`admin_id` int(11) NOT NULL DEFAULT 0,
			`active` tinyint(1) NOT NULL DEFAULT 1,
			`time_created` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `x_active_date` (`active`,`lock_date`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS fiscal-period locks';");
	}

	/** Current effective lock date (0 = no lock / periods all open). */
	function epc_erp_fiscal_lock_date(PDO $db)
	{
		epc_erp_fiscal_ensure_schema($db);
		try {
			$v = $db->query('SELECT MAX(`lock_date`) FROM `epc_erp_fiscal_locks` WHERE `active` = 1')->fetchColumn();
			return (int) $v;
		} catch (Exception $e) {
			return 0;
		}
	}

	/** True if a journal dated $journalDate would fall in a closed period. */
	function epc_erp_fiscal_is_locked(PDO $db, $journalDate)
	{
		$lock = epc_erp_fiscal_lock_date($db);
		return $lock > 0 && (int) $journalDate <= $lock;
	}

	/**
	 * Set (or move) the fiscal lock to a cut-off date. Passing 0 clears the
	 * lock (re-opens all periods).
	 */
	function epc_erp_fiscal_set_lock(PDO $db, $lockDate, $note = '')
	{
		epc_erp_fiscal_ensure_schema($db);
		$admin = function_exists('epc_erp_admin_id') ? epc_erp_admin_id() : 0;
		$db->exec('UPDATE `epc_erp_fiscal_locks` SET `active` = 0 WHERE `active` = 1');
		if ((int) $lockDate > 0) {
			$st = $db->prepare(
				'INSERT INTO `epc_erp_fiscal_locks` (`lock_date`, `note`, `admin_id`, `active`, `time_created`) VALUES (?, ?, ?, 1, ?)'
			);
			$st->execute(array((int) $lockDate, mb_substr(trim((string) $note), 0, 255), $admin, time()));
		}
		if (function_exists('epc_erp_audit_log')) {
			epc_erp_audit_log($db, 'fiscal_lock', 'fiscal_period', (int) $lockDate, (int) $lockDate > 0 ? ('Locked up to ' . date('Y-m-d', (int) $lockDate)) : 'Cleared fiscal lock');
		}
		return true;
	}
}
