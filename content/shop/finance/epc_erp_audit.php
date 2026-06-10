<?php
/**
 * ERP audit trail — key actions (purchases, GL, CRM stage, RFQ, bank recon).
 */
defined('_ASTEXE_') or die('No access');

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
		PRIMARY KEY (`id`),
		KEY `x_time` (`time`),
		KEY `x_action` (`action`),
		KEY `x_entity` (`entity_type`, `entity_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP audit trail';");
}

function epc_erp_audit_log(PDO $db, $action, $entityType = '', $entityId = 0, $summary = '', array $detail = array())
{
	if (!function_exists('epc_erp_admin_id')) {
		require_once __DIR__ . '/epc_erp_helpers.php';
	}
	epc_erp_audit_ensure_schema($db);
	$db->prepare(
		'INSERT INTO `epc_erp_audit_log` (`time`, `admin_id`, `action`, `entity_type`, `entity_id`, `summary`, `detail_json`)
		 VALUES (?,?,?,?,?,?,?)'
	)->execute(array(
		time(),
		epc_erp_admin_id(),
		mb_substr((string)$action, 0, 64),
		mb_substr((string)$entityType, 0, 32),
		(int)$entityId,
		mb_substr((string)$summary, 0, 512),
		$detail ? json_encode($detail, JSON_UNESCAPED_UNICODE) : null,
	));
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
