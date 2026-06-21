<?php
/**
 * ERP — opening balances (COA, inventory, fixed assets) as of a specific date.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';
require_once __DIR__ . '/epc_erp_inventory.php';
require_once __DIR__ . '/epc_erp_fixed_assets.php';

function epc_erp_opening_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_opening_batches` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`module` enum('coa','cash_bank','inventory','fixed_assets','combined') NOT NULL DEFAULT 'combined',
		`as_of_date` date NOT NULL,
		`reference` varchar(128) DEFAULT NULL,
		`status` enum('draft','posted','void') NOT NULL DEFAULT 'draft',
		`note` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_posted` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_date` (`as_of_date`,`module`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_opening_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`batch_id` int(11) NOT NULL,
		`line_type` varchar(32) NOT NULL,
		`entity_id` int(11) NOT NULL DEFAULT 0,
		`entity_ref` varchar(64) DEFAULT NULL,
		`debit` decimal(14,2) NOT NULL DEFAULT 0.00,
		`credit` decimal(14,2) NOT NULL DEFAULT 0.00,
		`qty` decimal(14,3) NOT NULL DEFAULT 0.000,
		`unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`meta_json` text,
		PRIMARY KEY (`id`),
		KEY `x_batch` (`batch_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");
}

function epc_erp_opening_create_batch(PDO $db, array $data)
{
	$module = (string) ($data['module'] ?? 'combined');
	$allowed = array('coa', 'cash_bank', 'inventory', 'fixed_assets', 'combined');
	if (!in_array($module, $allowed, true)) {
		$module = 'combined';
	}
	$asOf = !empty($data['as_of_date']) ? date('Y-m-d', strtotime((string) $data['as_of_date'])) : date('Y-m-d');
	require_once __DIR__ . '/epc_erp_inventory.php';
	$db->prepare(
		'INSERT INTO `epc_erp_opening_batches` (`module`,`as_of_date`,`reference`,`status`,`note`,`admin_id`,`time_created`) VALUES (?,?,?,?,?,?,?)'
	)->execute(array(
		$module, $asOf, trim((string) ($data['reference'] ?? '')),
		'draft', trim((string) ($data['note'] ?? '')),
		epc_erp_admin_id(), time(),
	));
	return (int) $db->lastInsertId();
}

function epc_erp_opening_add_line(PDO $db, $batchId, array $line)
{
	$db->prepare(
		'INSERT INTO `epc_erp_opening_lines` (`batch_id`,`line_type`,`entity_id`,`entity_ref`,`debit`,`credit`,`qty`,`unit_cost`,`meta_json`) VALUES (?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		(int) $batchId,
		substr((string) ($line['line_type'] ?? 'misc'), 0, 32),
		(int) ($line['entity_id'] ?? 0),
		substr((string) ($line['entity_ref'] ?? ''), 0, 64),
		round((float) ($line['debit'] ?? 0), 2),
		round((float) ($line['credit'] ?? 0), 2),
		(float) ($line['qty'] ?? 0),
		(float) ($line['unit_cost'] ?? 0),
		!empty($line['meta']) ? json_encode($line['meta']) : null,
	));
}

function epc_erp_opening_post_batch(PDO $db, $batchId)
{
	$st = $db->prepare('SELECT * FROM `epc_erp_opening_batches` WHERE `id` = ? AND `status` = ? LIMIT 1');
	$st->execute(array((int) $batchId, 'draft'));
	$batch = $st->fetch(PDO::FETCH_ASSOC);
	if (!$batch) {
		throw new Exception('Opening batch not found or already posted');
	}
	$lines = $db->prepare('SELECT * FROM `epc_erp_opening_lines` WHERE `batch_id` = ?');
	$lines->execute(array((int) $batchId));
	$rows = $lines->fetchAll(PDO::FETCH_ASSOC);
	$asOf = (string) $batch['as_of_date'];
	$db->beginTransaction();
	try {
		foreach ($rows as $ln) {
			$type = (string) $ln['line_type'];
			$meta = !empty($ln['meta_json']) ? json_decode($ln['meta_json'], true) : array();
			if (!is_array($meta)) {
				$meta = array();
			}
			if ($type === 'coa' && (int) $ln['entity_id'] > 0) {
				$bal = round((float) $ln['debit'] - (float) $ln['credit'], 2);
				$db->prepare('UPDATE `epc_erp_coa_accounts` SET `opening_balance` = ? WHERE `id` = ?')
					->execute(array($bal, (int) $ln['entity_id']));
			}
			if ($type === 'cash_bank' && (int) $ln['entity_id'] > 0) {
				$bal = round((float) $ln['debit'] - (float) $ln['credit'], 2);
				if ($db->query("SHOW COLUMNS FROM `epc_erp_cash_bank_accounts` LIKE 'opening_balance'")->fetch()) {
					$db->prepare('UPDATE `epc_erp_cash_bank_accounts` SET `opening_balance` = ? WHERE `id` = ?')
						->execute(array($bal, (int) $ln['entity_id']));
				}
			}
			if ($type === 'inventory' && (int) $ln['entity_id'] > 0) {
				$wh = (int) ($meta['warehouse_id'] ?? 0);
				if ($wh <= 0) {
					continue;
				}
				epc_erp_inventory_record_movement($db, array(
					'movement_type' => 'opening',
					'warehouse_id' => $wh,
					'item_id' => (int) $ln['entity_id'],
					'qty' => (float) $ln['qty'],
					'unit_cost' => (float) $ln['unit_cost'],
					'batch_no' => (string) ($meta['batch_no'] ?? ''),
					'expiry_date' => !empty($meta['expiry_date']) ? $meta['expiry_date'] : null,
					'movement_date' => $asOf,
					'reference' => (string) ($batch['reference'] ?? 'Opening'),
					'opening_batch_id' => (int) $batchId,
				));
			}
			if ($type === 'fixed_asset' && (int) $ln['entity_id'] > 0) {
				$db->prepare(
					'UPDATE `epc_erp_fa_assets` SET `accumulated_depreciation` = ?, `opening_batch_id` = ? WHERE `id` = ?'
				)->execute(array(round((float) $ln['credit'], 2), (int) $batchId, (int) $ln['entity_id']));
				epc_erp_fa_refresh_book_value($db, (int) $ln['entity_id']);
			}
			if ($type === 'fixed_asset_new') {
				$meta['opening_batch_id'] = (int) $batchId;
				$meta['accumulated_depreciation'] = round((float) $ln['credit'], 2);
				epc_erp_fa_create_asset($db, $meta);
			}
		}
		$db->prepare('UPDATE `epc_erp_opening_batches` SET `status` = ?, `time_posted` = ? WHERE `id` = ?')
			->execute(array('posted', time(), (int) $batchId));
		$db->commit();
		return true;
	} catch (Exception $e) {
		$db->rollBack();
		throw $e;
	}
}

function epc_erp_opening_list_batches(PDO $db, $limit = 50)
{
	$limit = max(1, min(200, (int) $limit));
	return $db->query('SELECT * FROM `epc_erp_opening_batches` ORDER BY `as_of_date` DESC, `id` DESC LIMIT ' . $limit)
		->fetchAll(PDO::FETCH_ASSOC);
}
