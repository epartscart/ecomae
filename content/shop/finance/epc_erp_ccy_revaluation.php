<?php
/**
 * BOS multi-currency — period-end revaluation of open monetary balances.
 *
 * IFRS/IAS-21: monetary items denominated in a foreign currency are retranslated
 * at the closing rate at each reporting date; the difference vs. the previously
 * booked base value is an unrealised FX gain/loss recognised in P&L.
 *
 * This engine scans open foreign-currency receivables (and payables when the
 * source stores a currency), values each at the as-of/closing rate, compares to
 * the rate on the document date, and posts a single balanced GL adjustment with
 * an optional auto-reversal in the next period (the standard revaluation cycle).
 *
 * Additive: one run-history table. Existing documents/postings are never edited.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_currency.php';
require_once __DIR__ . '/epc_erp_gl.php';

if (!function_exists('epc_erp_fx_reval_ensure_schema')) {

	function epc_erp_fx_reval_ensure_schema(PDO $db): void
	{
		$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_fx_revaluations` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`as_of` int(11) NOT NULL DEFAULT 0,
			`base_ccy` varchar(8) NOT NULL DEFAULT 'AED',
			`total_unrealised` decimal(16,2) NOT NULL DEFAULT 0.00,
			`gl_journal_id` int(11) NOT NULL DEFAULT 0,
			`reverse_journal_id` int(11) NOT NULL DEFAULT 0,
			`detail_json` mediumtext,
			`admin_id` int(11) NOT NULL DEFAULT 0,
			`time_created` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `x_asof` (`as_of`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS FX revaluation runs';");
	}

	/** Ensure the unrealised-FX P&L account exists; return its coa_id. */
	function epc_erp_fx_reval_account(PDO $db): int
	{
		$acc = epc_erp_gl_coa_by_code($db, '6900');
		if ($acc) {
			return (int) $acc['id'];
		}
		return epc_erp_gl_create_coa($db, array(
			'code' => '6900',
			'name' => 'Foreign exchange gain/loss (unrealised)',
			'account_type' => 'expense',
			'normal_side' => 'debit',
			'description' => 'IAS-21 period-end retranslation of monetary items',
		));
	}

	/**
	 * Open foreign-currency receivables with their unrealised FX position.
	 *
	 * @return array<int,array<string,mixed>>
	 */
	function epc_erp_fx_open_foreign_ar(PDO $db, string $base, int $asOf): array
	{
		$out = array();
		$has = $db->query("SHOW TABLES LIKE 'epc_einvoice_documents'")->fetchColumn();
		if (!$has) {
			return $out;
		}
		$st = $db->prepare(
			"SELECT `id`, `invoice_number`, `issue_date`, `currency_code`,
				ROUND(`total_incl_vat` - `paid_amount`, 2) AS outstanding_fc
			 FROM `epc_einvoice_documents`
			 WHERE `active` = 1 AND `status` <> 'cancelled'
			   AND `doc_category` IN ('tax_invoice','commercial_invoice')
			   AND UPPER(`currency_code`) <> ?
			   AND ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005
			   AND `issue_date` <= ?
			 ORDER BY `currency_code` ASC, `id` ASC"
		);
		$st->execute(array(strtoupper($base), $asOf));
		foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
			$ccy = strtoupper((string) $r['currency_code']);
			$outstandingFc = (float) $r['outstanding_fc'];
			$bookedRate = epc_ccy_get_rate($db, $ccy, $base, (int) $r['issue_date']);
			$currentRate = epc_ccy_get_rate($db, $ccy, $base, $asOf);
			if ($bookedRate === null || $currentRate === null) {
				continue; // no rate → cannot revalue this document
			}
			$bookedBase = epc_ccy_round($outstandingFc * $bookedRate, $base);
			$currentBase = epc_ccy_round($outstandingFc * $currentRate, $base);
			$out[] = array(
				'doc_type' => 'ar',
				'doc_id' => (int) $r['id'],
				'ref' => (string) $r['invoice_number'],
				'currency' => $ccy,
				'outstanding_fc' => $outstandingFc,
				'booked_rate' => $bookedRate,
				'current_rate' => $currentRate,
				'booked_base' => $bookedBase,
				'current_base' => $currentBase,
				'unrealised' => epc_ccy_round($currentBase - $bookedBase, $base),
			);
		}
		return $out;
	}

	/**
	 * Preview a revaluation as of a date: per-document detail, per-currency
	 * rollup and the net unrealised position (no posting).
	 */
	function epc_erp_fx_revaluation_preview(PDO $db, int $asOf = 0): array
	{
		epc_erp_fx_reval_ensure_schema($db);
		$asOf = $asOf > 0 ? $asOf : time();
		$cfg = epc_ccy_get_config($db);
		$base = (string) $cfg['base_currency'];
		$lines = epc_erp_fx_open_foreign_ar($db, $base, $asOf);

		$byCcy = array();
		$total = 0.0;
		foreach ($lines as $l) {
			$c = $l['currency'];
			if (!isset($byCcy[$c])) {
				$byCcy[$c] = array('currency' => $c, 'outstanding_fc' => 0.0, 'booked_base' => 0.0, 'current_base' => 0.0, 'unrealised' => 0.0, 'count' => 0);
			}
			$byCcy[$c]['outstanding_fc'] += $l['outstanding_fc'];
			$byCcy[$c]['booked_base'] += $l['booked_base'];
			$byCcy[$c]['current_base'] += $l['current_base'];
			$byCcy[$c]['unrealised'] += $l['unrealised'];
			$byCcy[$c]['count']++;
			$total += $l['unrealised'];
		}
		return array(
			'as_of' => $asOf,
			'base' => $base,
			'lines' => $lines,
			'by_currency' => array_values($byCcy),
			'total_unrealised' => epc_ccy_round($total, $base),
		);
	}

	/**
	 * Post a period-end revaluation: a balanced GL adjustment between the AR
	 * control account (1100) and the unrealised-FX account (6900), plus an
	 * optional auto-reversal dated the day after the as-of date.
	 *
	 * @return array{status:bool,message:string,journal_id:int,reverse_journal_id:int,total_unrealised:float}
	 */
	function epc_erp_fx_post_revaluation(PDO $db, int $asOf = 0, bool $autoReverse = true): array
	{
		epc_erp_fx_reval_ensure_schema($db);
		$preview = epc_erp_fx_revaluation_preview($db, $asOf);
		$asOf = (int) $preview['as_of'];
		$base = (string) $preview['base'];
		$total = (float) $preview['total_unrealised'];

		if (abs($total) < 0.005) {
			return array('status' => false, 'message' => 'No revaluation needed — net unrealised FX is zero.', 'journal_id' => 0, 'reverse_journal_id' => 0, 'total_unrealised' => 0.0);
		}

		$arAcc = epc_erp_gl_coa_by_code($db, '1100');
		if (!$arAcc) {
			return array('status' => false, 'message' => 'AR control account (1100) not found.', 'journal_id' => 0, 'reverse_journal_id' => 0, 'total_unrealised' => $total);
		}
		$arId = (int) $arAcc['id'];
		$fxId = epc_erp_fx_reval_account($db);

		// total > 0 → receivables worth more in base → unrealised GAIN:
		//   Dr AR control (asset up) / Cr FX gain. Loss is the mirror.
		$amount = abs($total);
		if ($total > 0) {
			$lines = array(
				array('coa_id' => $arId, 'debit' => $amount, 'credit' => 0, 'line_note' => 'FX revaluation (gain) of open AR'),
				array('coa_id' => $fxId, 'debit' => 0, 'credit' => $amount, 'line_note' => 'Unrealised FX gain'),
			);
		} else {
			$lines = array(
				array('coa_id' => $fxId, 'debit' => $amount, 'credit' => 0, 'line_note' => 'Unrealised FX loss'),
				array('coa_id' => $arId, 'debit' => 0, 'credit' => $amount, 'line_note' => 'FX revaluation (loss) of open AR'),
			);
		}

		$jid = epc_erp_gl_post_journal($db, array(
			'journal_date' => $asOf,
			'reference' => 'FX-REVAL ' . date('Y-m-d', $asOf),
			'description' => 'Period-end FX revaluation of open foreign-currency receivables (IAS-21)',
			'source_type' => 'adjustment',
		), $lines);

		$revId = 0;
		if ($autoReverse) {
			// Standard cycle: reverse on the first day of the next period so only
			// the realised difference remains when the item actually settles.
			$revId = epc_erp_gl_reverse_journal($db, $jid, $asOf + 86400, 'Auto-reversal of FX revaluation ' . date('Y-m-d', $asOf));
		}

		$ins = $db->prepare(
			'INSERT INTO `epc_erp_fx_revaluations`
			 (`as_of`,`base_ccy`,`total_unrealised`,`gl_journal_id`,`reverse_journal_id`,`detail_json`,`admin_id`,`time_created`)
			 VALUES (?,?,?,?,?,?,?,?)'
		);
		$ins->execute(array(
			$asOf,
			$base,
			$total,
			$jid,
			$revId,
			json_encode($preview['by_currency'], JSON_UNESCAPED_UNICODE),
			function_exists('epc_erp_admin_id') ? epc_erp_admin_id() : 0,
			time(),
		));

		if (function_exists('epc_erp_audit_log')) {
			epc_erp_audit_log($db, 'fx_revaluation', 'gl_journal', $jid,
				'FX revaluation ' . date('Y-m-d', $asOf) . ' net ' . number_format($total, 2) . ' ' . $base,
				array('as_of' => date('Y-m-d', $asOf), 'total_unrealised' => $total, 'reverse_journal_id' => $revId));
		}

		$dir = $total > 0 ? 'gain' : 'loss';
		return array(
			'status' => true,
			'message' => 'FX revaluation posted: unrealised ' . $dir . ' ' . number_format(abs($total), 2) . ' ' . $base
				. ' (journal #' . $jid . ($revId ? ', auto-reversal #' . $revId : '') . ')',
			'journal_id' => $jid,
			'reverse_journal_id' => $revId,
			'total_unrealised' => $total,
		);
	}

	/** Recent revaluation runs for the history panel. */
	function epc_erp_fx_revaluation_history(PDO $db, int $limit = 24): array
	{
		epc_erp_fx_reval_ensure_schema($db);
		$st = $db->prepare('SELECT * FROM `epc_erp_fx_revaluations` ORDER BY `id` DESC LIMIT ' . (int) $limit);
		$st->execute();
		return $st->fetchAll(PDO::FETCH_ASSOC);
	}
}
