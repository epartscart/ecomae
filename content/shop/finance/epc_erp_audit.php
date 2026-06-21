<?php
/**
 * ERP audit trail — key actions (purchases, GL, CRM stage, RFQ, bank recon).
 *
 * Enriched (Phase-1 data-governance): every entry can carry who/when plus the
 * request IP and device (user-agent), and field-level before/after values for
 * change tracking on financial transactions and master records.
 */
defined('_ASTEXE_') or die('No access');

/** Add a column to a table only if it does not already exist (portable). */
function epc_erp_audit_add_column(PDO $db, $table, $column, $definition)
{
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM information_schema.COLUMNS
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?'
		);
		$st->execute(array($table, $column));
		if ((int)$st->fetchColumn() === 0) {
			$db->exec('ALTER TABLE `' . $table . '` ADD COLUMN `' . $column . '` ' . $definition);
		}
	} catch (Exception $e) {
		// best-effort; never block the underlying write because of audit DDL
	}
}

function epc_erp_audit_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_audit_log` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`time` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`action` varchar(64) NOT NULL,
		`entity_type` varchar(32) NOT NULL DEFAULT '',
		`entity_id` int(11) NOT NULL DEFAULT 0,
		`summary` varchar(512) DEFAULT NULL,
		`detail_json` text,
		`old_json` text,
		`new_json` text,
		`ip_address` varchar(45) DEFAULT NULL,
		`user_agent` varchar(255) DEFAULT NULL,
		PRIMARY KEY (`id`),
		KEY `x_time` (`time`),
		KEY `x_action` (`action`),
		KEY `x_entity` (`entity_type`, `entity_id`),
		KEY `x_admin` (`admin_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP audit trail';");

	// Bring older tables up to the enriched shape without losing data.
	epc_erp_audit_add_column($db, 'epc_erp_audit_log', 'old_json', 'text NULL');
	epc_erp_audit_add_column($db, 'epc_erp_audit_log', 'new_json', 'text NULL');
	epc_erp_audit_add_column($db, 'epc_erp_audit_log', 'ip_address', "varchar(45) DEFAULT NULL");
	epc_erp_audit_add_column($db, 'epc_erp_audit_log', 'user_agent', "varchar(255) DEFAULT NULL");
}

/** Best-effort client IP, honouring common proxy headers. */
function epc_erp_audit_client_ip()
{
	foreach (array('HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'HTTP_X_REAL_IP', 'REMOTE_ADDR') as $k) {
		if (!empty($_SERVER[$k])) {
			$ip = trim(explode(',', (string)$_SERVER[$k])[0]);
			if (filter_var($ip, FILTER_VALIDATE_IP)) {
				return $ip;
			}
		}
	}
	return isset($_SERVER['REMOTE_ADDR']) ? (string)$_SERVER['REMOTE_ADDR'] : '';
}

/** Device / client string from the user agent. */
function epc_erp_audit_user_agent()
{
	return isset($_SERVER['HTTP_USER_AGENT']) ? mb_substr((string)$_SERVER['HTTP_USER_AGENT'], 0, 255) : '';
}

function epc_erp_audit_log(PDO $db, $action, $entityType = '', $entityId = 0, $summary = '', array $detail = array(), array $old = array(), array $new = array())
{
	if (!function_exists('epc_erp_admin_id')) {
		require_once __DIR__ . '/epc_erp_helpers.php';
	}
	epc_erp_audit_ensure_schema($db);
	$db->prepare(
		'INSERT INTO `epc_erp_audit_log`
		 (`time`, `admin_id`, `action`, `entity_type`, `entity_id`, `summary`, `detail_json`, `old_json`, `new_json`, `ip_address`, `user_agent`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		time(),
		epc_erp_admin_id(),
		mb_substr((string)$action, 0, 64),
		mb_substr((string)$entityType, 0, 32),
		(int)$entityId,
		mb_substr((string)$summary, 0, 512),
		$detail ? json_encode($detail, JSON_UNESCAPED_UNICODE) : null,
		$old ? json_encode($old, JSON_UNESCAPED_UNICODE) : null,
		$new ? json_encode($new, JSON_UNESCAPED_UNICODE) : null,
		epc_erp_audit_client_ip(),
		epc_erp_audit_user_agent(),
	));
}

/**
 * Change-tracking helper: record only the fields that actually changed between
 * an old and new associative row. Use for master-record and financial edits.
 *
 * @param array $only  optional whitelist of keys to compare (defaults to all)
 */
function epc_erp_audit_change(PDO $db, $action, $entityType, $entityId, array $oldRow, array $newRow, $summary = '', array $only = array())
{
	$keys = $only ? $only : array_keys($newRow + $oldRow);
	$before = array();
	$after = array();
	foreach ($keys as $k) {
		$o = array_key_exists($k, $oldRow) ? $oldRow[$k] : null;
		$n = array_key_exists($k, $newRow) ? $newRow[$k] : null;
		if ((string)$o !== (string)$n) {
			$before[$k] = $o;
			$after[$k] = $n;
		}
	}
	if (!$before && !$after) {
		return false; // nothing changed → no noise in the trail
	}
	if ($summary === '') {
		$summary = ucfirst((string)$action) . ' ' . (string)$entityType . ' #' . (int)$entityId
			. ' (' . count($after) . ' field' . (count($after) === 1 ? '' : 's') . ' changed)';
	}
	epc_erp_audit_log($db, $action, $entityType, $entityId, $summary, array(), $before, $after);
	return true;
}

function epc_erp_audit_list(PDO $db, $action = '', $limit = 150)
{
	epc_erp_audit_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_erp_audit_log` WHERE 1=1';
	$params = array();
	if ($action !== '') {
		$sql .= ' AND `action` = ?';
		$params[] = $action;
	}
	$sql .= ' ORDER BY `time` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}
