<?php
/**
 * D365-style Intercompany Accounting module.
 *
 * Legal entities, intercompany transactions, due-to/due-from balances,
 * intercompany settlement, elimination entries for consolidation.
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_intercompany_ensure_schema(PDO $db)
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_legal_entities` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`entity_code`    VARCHAR(20) NOT NULL DEFAULT "",
		`name`           VARCHAR(200) NOT NULL DEFAULT "",
		`country_code`   CHAR(2) NOT NULL DEFAULT "",
		`currency_code`  CHAR(3) NOT NULL DEFAULT "AED",
		`registration_no` VARCHAR(60) NOT NULL DEFAULT "",
		`tax_id`         VARCHAR(40) NOT NULL DEFAULT "",
		`parent_entity_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`active`         TINYINT(1) NOT NULL DEFAULT 1,
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_code` (`entity_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ic_transactions` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`from_entity_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`to_entity_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`transaction_date` INT UNSIGNED NOT NULL DEFAULT 0,
		`transaction_type` ENUM("sale","purchase","transfer","service","loan","dividend") NOT NULL DEFAULT "sale",
		`amount`          DECIMAL(16,2) NOT NULL DEFAULT 0,
		`currency_code`   CHAR(3) NOT NULL DEFAULT "AED",
		`reference`       VARCHAR(120) NOT NULL DEFAULT "",
		`description`     VARCHAR(500) NOT NULL DEFAULT "",
		`status`          ENUM("draft","posted","settled","eliminated") NOT NULL DEFAULT "draft",
		`from_journal_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`to_journal_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`created_at`      INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_from` (`from_entity_id`),
		INDEX `idx_to` (`to_entity_id`),
		INDEX `idx_status` (`status`),
		INDEX `idx_date` (`transaction_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ic_balances` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`entity_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`counterparty_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`due_to`        DECIMAL(16,2) NOT NULL DEFAULT 0,
		`due_from`      DECIMAL(16,2) NOT NULL DEFAULT 0,
		`net_balance`   DECIMAL(16,2) NOT NULL DEFAULT 0,
		`as_of_date`    INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_pair` (`entity_id`, `counterparty_id`),
		INDEX `idx_entity` (`entity_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ic_settlements` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`from_entity_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`to_entity_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`settlement_date` INT UNSIGNED NOT NULL DEFAULT 0,
		`amount`          DECIMAL(16,2) NOT NULL DEFAULT 0,
		`reference`       VARCHAR(120) NOT NULL DEFAULT "",
		`status`          ENUM("pending","approved","completed") NOT NULL DEFAULT "pending",
		`created_at`      INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_from` (`from_entity_id`),
		INDEX `idx_to` (`to_entity_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ic_elimination_entries` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`consolidation_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`transaction_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`debit_coa_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`credit_coa_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`amount`          DECIMAL(16,2) NOT NULL DEFAULT 0,
		`description`     VARCHAR(500) NOT NULL DEFAULT "",
		`created_at`      INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_consolidation` (`consolidation_id`),
		INDEX `idx_transaction` (`transaction_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_erp_legal_entity_save(PDO $db, array $data)
{
	$now = time();
	$id = isset($data['id']) ? (int) $data['id'] : 0;
	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_erp_legal_entities` SET `entity_code`=?, `name`=?, `country_code`=?, `currency_code`=?, `registration_no`=?, `tax_id`=?, `parent_entity_id`=?, `active`=? WHERE `id`=?'
		)->execute(array(
			$data['entity_code'] ?? '', $data['name'] ?? '',
			$data['country_code'] ?? '', $data['currency_code'] ?? 'AED',
			$data['registration_no'] ?? '', $data['tax_id'] ?? '',
			(int) ($data['parent_entity_id'] ?? 0),
			isset($data['active']) ? (int) $data['active'] : 1, $id,
		));
		return $id;
	}
	$db->prepare(
		'INSERT INTO `epc_erp_legal_entities` (`entity_code`,`name`,`country_code`,`currency_code`,`registration_no`,`tax_id`,`parent_entity_id`,`active`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$data['entity_code'] ?? '', $data['name'] ?? '',
		$data['country_code'] ?? '', $data['currency_code'] ?? 'AED',
		$data['registration_no'] ?? '', $data['tax_id'] ?? '',
		(int) ($data['parent_entity_id'] ?? 0),
		isset($data['active']) ? (int) $data['active'] : 1, $now,
	));
	return (int) $db->lastInsertId();
}

function epc_erp_legal_entity_list(PDO $db)
{
	return $db->query('SELECT * FROM `epc_erp_legal_entities` WHERE `active` = 1 ORDER BY `entity_code`')->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_ic_transaction_post(PDO $db, array $data)
{
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_ic_transactions` (`from_entity_id`,`to_entity_id`,`transaction_date`,`transaction_type`,`amount`,`currency_code`,`reference`,`description`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		(int) ($data['from_entity_id'] ?? 0), (int) ($data['to_entity_id'] ?? 0),
		(int) ($data['transaction_date'] ?? $now), $data['transaction_type'] ?? 'sale',
		$data['amount'] ?? 0, $data['currency_code'] ?? 'AED',
		$data['reference'] ?? '', $data['description'] ?? '', 'posted', $now,
	));
	$txId = (int) $db->lastInsertId();

	$fromId = (int) ($data['from_entity_id'] ?? 0);
	$toId = (int) ($data['to_entity_id'] ?? 0);
	$amount = (float) ($data['amount'] ?? 0);

	$db->prepare(
		'INSERT INTO `epc_erp_ic_balances` (`entity_id`,`counterparty_id`,`due_from`,`net_balance`,`as_of_date`)
		 VALUES (?,?,?,?,?)
		 ON DUPLICATE KEY UPDATE `due_from` = `due_from` + VALUES(`due_from`), `net_balance` = `due_from` - `due_to`, `as_of_date` = VALUES(`as_of_date`)'
	)->execute(array($fromId, $toId, $amount, $amount, $now));

	$db->prepare(
		'INSERT INTO `epc_erp_ic_balances` (`entity_id`,`counterparty_id`,`due_to`,`net_balance`,`as_of_date`)
		 VALUES (?,?,?,?,?)
		 ON DUPLICATE KEY UPDATE `due_to` = `due_to` + VALUES(`due_to`), `net_balance` = `due_from` - `due_to`, `as_of_date` = VALUES(`as_of_date`)'
	)->execute(array($toId, $fromId, $amount, -$amount, $now));

	return $txId;
}

function epc_erp_ic_balance_report(PDO $db, int $entityId = 0)
{
	$sql = 'SELECT b.*, e1.`entity_code` AS entity_code, e1.`name` AS entity_name,
	               e2.`entity_code` AS counterparty_code, e2.`name` AS counterparty_name
	        FROM `epc_erp_ic_balances` b
	        LEFT JOIN `epc_erp_legal_entities` e1 ON e1.`id` = b.`entity_id`
	        LEFT JOIN `epc_erp_legal_entities` e2 ON e2.`id` = b.`counterparty_id`';
	if ($entityId > 0) {
		$sql .= ' WHERE b.`entity_id` = ' . (int) $entityId;
	}
	$sql .= ' ORDER BY e1.`entity_code`, e2.`entity_code`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_ic_settle(PDO $db, int $fromEntityId, int $toEntityId, float $amount)
{
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_ic_settlements` (`from_entity_id`,`to_entity_id`,`settlement_date`,`amount`,`status`,`created_at`) VALUES (?,?,?,?,?,?)'
	)->execute(array($fromEntityId, $toEntityId, $now, $amount, 'completed', $now));

	$db->prepare(
		'UPDATE `epc_erp_ic_balances` SET `due_from` = GREATEST(`due_from` - ?, 0), `net_balance` = `due_from` - `due_to`, `as_of_date` = ?
		 WHERE `entity_id` = ? AND `counterparty_id` = ?'
	)->execute(array($amount, $now, $fromEntityId, $toEntityId));

	$db->prepare(
		'UPDATE `epc_erp_ic_balances` SET `due_to` = GREATEST(`due_to` - ?, 0), `net_balance` = `due_from` - `due_to`, `as_of_date` = ?
		 WHERE `entity_id` = ? AND `counterparty_id` = ?'
	)->execute(array($amount, $now, $toEntityId, $fromEntityId));

	return (int) $db->lastInsertId();
}
