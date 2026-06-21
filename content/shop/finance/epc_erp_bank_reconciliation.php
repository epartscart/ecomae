<?php
/**
 * D365-style Bank Reconciliation module.
 *
 * Bank statement import, auto-matching rules, reconciliation engine,
 * unmatched items management, reconciliation reporting.
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_bank_recon_ensure_schema(PDO $db)
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_bank_recon_sessions` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`account_id`      INT UNSIGNED NOT NULL DEFAULT 0,
		`statement_date`  DATE NOT NULL,
		`statement_balance` DECIMAL(16,2) NOT NULL DEFAULT 0,
		`book_balance`    DECIMAL(16,2) NOT NULL DEFAULT 0,
		`adjusted_balance` DECIMAL(16,2) NOT NULL DEFAULT 0,
		`difference`      DECIMAL(16,2) NOT NULL DEFAULT 0,
		`status`          ENUM("draft","in_progress","reconciled","cancelled") NOT NULL DEFAULT "draft",
		`reconciled_by`   INT UNSIGNED NOT NULL DEFAULT 0,
		`reconciled_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		`created_at`      INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_account` (`account_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_bank_recon_statement_lines` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`session_id`      INT UNSIGNED NOT NULL DEFAULT 0,
		`line_date`       DATE NOT NULL,
		`description`     VARCHAR(500) NOT NULL DEFAULT "",
		`reference`       VARCHAR(120) NOT NULL DEFAULT "",
		`debit`           DECIMAL(16,2) NOT NULL DEFAULT 0,
		`credit`          DECIMAL(16,2) NOT NULL DEFAULT 0,
		`matched`         TINYINT(1) NOT NULL DEFAULT 0,
		`matched_entry_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`match_rule_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_session` (`session_id`),
		INDEX `idx_matched` (`matched`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_bank_recon_rules` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`name`            VARCHAR(200) NOT NULL DEFAULT "",
		`match_type`      ENUM("exact_amount","amount_tolerance","reference","description_contains") NOT NULL DEFAULT "exact_amount",
		`match_value`     VARCHAR(200) NOT NULL DEFAULT "",
		`tolerance_pct`   DECIMAL(5,2) NOT NULL DEFAULT 0,
		`date_range_days` SMALLINT UNSIGNED NOT NULL DEFAULT 3,
		`auto_match`      TINYINT(1) NOT NULL DEFAULT 1,
		`priority`        TINYINT UNSIGNED NOT NULL DEFAULT 5,
		`active`          TINYINT(1) NOT NULL DEFAULT 1,
		`created_at`      INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_active` (`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_erp_bank_recon_create_session(PDO $db, array $data)
{
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_bank_recon_sessions` (`account_id`,`statement_date`,`statement_balance`,`book_balance`,`status`,`created_at`) VALUES (?,?,?,?,?,?)'
	)->execute(array(
		(int) ($data['account_id'] ?? 0), $data['statement_date'] ?? date('Y-m-d'),
		$data['statement_balance'] ?? 0, $data['book_balance'] ?? 0, 'draft', $now,
	));
	return (int) $db->lastInsertId();
}

function epc_erp_bank_recon_add_statement_line(PDO $db, int $sessionId, array $data)
{
	$db->prepare(
		'INSERT INTO `epc_erp_bank_recon_statement_lines` (`session_id`,`line_date`,`description`,`reference`,`debit`,`credit`) VALUES (?,?,?,?,?,?)'
	)->execute(array(
		$sessionId, $data['line_date'] ?? date('Y-m-d'),
		$data['description'] ?? '', $data['reference'] ?? '',
		$data['debit'] ?? 0, $data['credit'] ?? 0,
	));
	return (int) $db->lastInsertId();
}

function epc_erp_bank_recon_rule_save(PDO $db, array $data)
{
	$now = time();
	$id = isset($data['id']) ? (int) $data['id'] : 0;
	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_erp_bank_recon_rules` SET `name`=?, `match_type`=?, `match_value`=?, `tolerance_pct`=?, `date_range_days`=?, `auto_match`=?, `priority`=?, `active`=? WHERE `id`=?'
		)->execute(array(
			$data['name'] ?? '', $data['match_type'] ?? 'exact_amount',
			$data['match_value'] ?? '', $data['tolerance_pct'] ?? 0,
			(int) ($data['date_range_days'] ?? 3),
			isset($data['auto_match']) ? (int) $data['auto_match'] : 1,
			(int) ($data['priority'] ?? 5),
			isset($data['active']) ? (int) $data['active'] : 1, $id,
		));
		return $id;
	}
	$db->prepare(
		'INSERT INTO `epc_erp_bank_recon_rules` (`name`,`match_type`,`match_value`,`tolerance_pct`,`date_range_days`,`auto_match`,`priority`,`active`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$data['name'] ?? '', $data['match_type'] ?? 'exact_amount',
		$data['match_value'] ?? '', $data['tolerance_pct'] ?? 0,
		(int) ($data['date_range_days'] ?? 3),
		isset($data['auto_match']) ? (int) $data['auto_match'] : 1,
		(int) ($data['priority'] ?? 5),
		isset($data['active']) ? (int) $data['active'] : 1, $now,
	));
	return (int) $db->lastInsertId();
}

function epc_erp_bank_recon_auto_match(PDO $db, int $sessionId)
{
	$session = $db->prepare('SELECT * FROM `epc_erp_bank_recon_sessions` WHERE `id` = ?');
	$session->execute(array($sessionId));
	$sess = $session->fetch(PDO::FETCH_ASSOC);
	if (!$sess) {
		return array('matched' => 0);
	}

	$accountId = (int) $sess['account_id'];
	$rules = $db->query('SELECT * FROM `epc_erp_bank_recon_rules` WHERE `active` = 1 AND `auto_match` = 1 ORDER BY `priority` ASC')->fetchAll(PDO::FETCH_ASSOC);

	$unmatched = $db->prepare(
		'SELECT * FROM `epc_erp_bank_recon_statement_lines` WHERE `session_id` = ? AND `matched` = 0 ORDER BY `line_date`'
	);
	$unmatched->execute(array($sessionId));
	$lines = $unmatched->fetchAll(PDO::FETCH_ASSOC);

	$matched = 0;
	foreach ($lines as $line) {
		$lineAmt = (float) $line['debit'] - (float) $line['credit'];
		$lineRef = (string) $line['reference'];
		$lineDesc = (string) $line['description'];
		$lineDate = (string) $line['line_date'];

		foreach ($rules as $rule) {
			$ruleId = (int) $rule['id'];
			$dateRange = (int) $rule['date_range_days'];

			$dateFrom = date('Y-m-d', strtotime($lineDate) - $dateRange * 86400);
			$dateTo = date('Y-m-d', strtotime($lineDate) + $dateRange * 86400);

			$matchSql = 'SELECT `id`, (`amount` * IF(`direction` = 1, 1, -1)) AS signed_amount, `reference`
			             FROM `epc_erp_cash_bank_entries`
			             WHERE `account_id` = ? AND DATE(FROM_UNIXTIME(`time`)) BETWEEN ? AND ?
			               AND `id` NOT IN (SELECT `matched_entry_id` FROM `epc_erp_bank_recon_statement_lines` WHERE `session_id` = ? AND `matched` = 1)';

			if ($rule['match_type'] === 'exact_amount') {
				$matchSql .= ' AND ABS((`amount` * IF(`direction` = 1, 1, -1)) - ?) < 0.01';
				$st = $db->prepare($matchSql . ' LIMIT 1');
				$st->execute(array($accountId, $dateFrom, $dateTo, $sessionId, $lineAmt));
			} elseif ($rule['match_type'] === 'amount_tolerance') {
				$tol = abs($lineAmt) * (float) $rule['tolerance_pct'] / 100;
				$matchSql .= ' AND ABS((`amount` * IF(`direction` = 1, 1, -1)) - ?) <= ?';
				$st = $db->prepare($matchSql . ' LIMIT 1');
				$st->execute(array($accountId, $dateFrom, $dateTo, $sessionId, $lineAmt, $tol));
			} elseif ($rule['match_type'] === 'reference') {
				if ($lineRef === '') {
					continue;
				}
				$matchSql .= ' AND `reference` = ?';
				$st = $db->prepare($matchSql . ' LIMIT 1');
				$st->execute(array($accountId, $dateFrom, $dateTo, $sessionId, $lineRef));
			} elseif ($rule['match_type'] === 'description_contains') {
				$keyword = (string) $rule['match_value'];
				if ($keyword === '' || stripos($lineDesc, $keyword) === false) {
					continue;
				}
				$st = $db->prepare($matchSql . ' LIMIT 1');
				$st->execute(array($accountId, $dateFrom, $dateTo, $sessionId));
			} else {
				continue;
			}

			$entry = $st->fetch(PDO::FETCH_ASSOC);
			if ($entry) {
				$db->prepare(
					'UPDATE `epc_erp_bank_recon_statement_lines` SET `matched` = 1, `matched_entry_id` = ?, `match_rule_id` = ? WHERE `id` = ?'
				)->execute(array((int) $entry['id'], $ruleId, (int) $line['id']));
				$matched++;
				break;
			}
		}
	}

	$db->prepare(
		'UPDATE `epc_erp_bank_recon_sessions` SET `status` = "in_progress" WHERE `id` = ? AND `status` = "draft"'
	)->execute(array($sessionId));

	return array('matched' => $matched, 'total_lines' => count($lines));
}

function epc_erp_bank_recon_finalize(PDO $db, int $sessionId, int $userId)
{
	$now = time();
	$st = $db->prepare('SELECT COUNT(*) FROM `epc_erp_bank_recon_statement_lines` WHERE `session_id` = ? AND `matched` = 0');
	$st->execute(array($sessionId));
	$unmatchedCount = (int) $st->fetchColumn();

	$session = $db->prepare('SELECT * FROM `epc_erp_bank_recon_sessions` WHERE `id` = ?');
	$session->execute(array($sessionId));
	$sess = $session->fetch(PDO::FETCH_ASSOC);

	$stmtBal = (float) ($sess['statement_balance'] ?? 0);

	$matchedSt = $db->prepare(
		'SELECT COALESCE(SUM(`debit`), 0) - COALESCE(SUM(`credit`), 0) AS net FROM `epc_erp_bank_recon_statement_lines` WHERE `session_id` = ? AND `matched` = 1'
	);
	$matchedSt->execute(array($sessionId));
	$matchedNet = (float) $matchedSt->fetchColumn();

	$adjusted = $stmtBal - $matchedNet;
	$diff = round($adjusted - (float) ($sess['book_balance'] ?? 0), 2);

	$db->prepare(
		'UPDATE `epc_erp_bank_recon_sessions` SET `adjusted_balance` = ?, `difference` = ?, `status` = "reconciled", `reconciled_by` = ?, `reconciled_at` = ? WHERE `id` = ?'
	)->execute(array($adjusted, $diff, $userId, $now, $sessionId));

	return array(
		'session_id' => $sessionId,
		'statement_balance' => $stmtBal,
		'adjusted_balance' => $adjusted,
		'difference' => $diff,
		'unmatched_count' => $unmatchedCount,
	);
}

function epc_erp_bank_recon_session_list(PDO $db, int $accountId = 0)
{
	$sql = 'SELECT * FROM `epc_erp_bank_recon_sessions`';
	if ($accountId > 0) {
		$sql .= ' WHERE `account_id` = ' . (int) $accountId;
	}
	$sql .= ' ORDER BY `statement_date` DESC';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}
