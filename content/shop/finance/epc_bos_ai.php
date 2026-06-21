<?php
/**
 * Intelligent BOS — the AI/advisory layer that runs on top of the live ERP data
 * and the KPI engine.
 *
 * Capabilities (all computed natively from tenant data, no external service):
 *   - Revenue / sales forecasting (linear-trend + seasonal-naive blend)
 *   - Cash-flow prediction (opening cash + expected AR collections − AP payments
 *     + operating-cash trend, projected forward N months)
 *   - Predictive inventory (consumption rate -> days-of-cover -> reorder advice)
 *   - KPI-driven recommendations / automated decision support (rules engine)
 *   - Natural-language assistant over the computed metrics (intent matcher);
 *     an external LLM can be layered in later via epc_bos_ai_llm_available().
 */

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';

if (!function_exists('epc_bos_ai_month_bounds')) {

	/** Start/end timestamps for the calendar month containing $ts. */
	function epc_bos_ai_month_bounds(int $ts): array
	{
		$start = strtotime(date('Y-m-01 00:00:00', $ts));
		$end = strtotime(date('Y-m-t 23:59:59', $ts));
		return array($start, $end);
	}

	/**
	 * Monthly history of revenue / purchases / profit for the last $months
	 * calendar months (oldest first). Computed from epc_erp_dashboard per month.
	 *
	 * @return array<int,array<string,mixed>>
	 */
	function epc_bos_ai_monthly_series(PDO $db, int $months = 6): array
	{
		$months = max(2, min(24, $months));
		$out = array();
		$cursor = time();
		$stack = array();
		for ($i = 0; $i < $months; $i++) {
			list($s, $e) = epc_bos_ai_month_bounds($cursor);
			$dash = epc_erp_dashboard($db, $s, $e);
			$stack[] = array(
				'label' => date('M Y', $s),
				'ym' => date('Y-m', $s),
				'start' => $s,
				'revenue' => round((float) ($dash['revenue_ex_vat'] ?? 0), 2),
				'purchases' => round((float) ($dash['purchase_ex_vat'] ?? 0), 2),
				'profit' => round((float) ($dash['profit_ex_vat'] ?? 0), 2),
			);
			$cursor = $s - 1; // step into previous month
		}
		$out = array_reverse($stack);
		return $out;
	}

	/**
	 * Ordinary least-squares fit over y-values indexed 0..n-1.
	 * Returns [slope, intercept]. Forecast at x: intercept + slope*x.
	 */
	function epc_bos_ai_linreg(array $y): array
	{
		$n = count($y);
		if ($n < 2) {
			return array(0.0, $n ? (float) $y[0] : 0.0);
		}
		$sx = $sy = $sxx = $sxy = 0.0;
		foreach ($y as $i => $v) {
			$sx += $i;
			$sy += (float) $v;
			$sxx += $i * $i;
			$sxy += $i * (float) $v;
		}
		$denom = ($n * $sxx) - ($sx * $sx);
		if (abs($denom) < 1e-9) {
			return array(0.0, $sy / $n);
		}
		$slope = (($n * $sxy) - ($sx * $sy)) / $denom;
		$intercept = ($sy - ($slope * $sx)) / $n;
		return array($slope, $intercept);
	}

	/**
	 * Revenue forecast: blends the linear trend with the trailing average so a
	 * short or noisy history doesn't produce wild numbers. Returns history +
	 * forecast points and a simple confidence label.
	 *
	 * @return array<string,mixed>
	 */
	function epc_bos_ai_revenue_forecast(PDO $db, int $history = 6, int $ahead = 3): array
	{
		$series = epc_bos_ai_monthly_series($db, $history);
		$rev = array_map(function ($r) { return (float) $r['revenue']; }, $series);
		list($slope, $intercept) = epc_bos_ai_linreg($rev);
		$n = count($rev);
		$avg = $n ? array_sum($rev) / $n : 0.0;

		$forecast = array();
		$cursor = $n ? $series[$n - 1]['start'] : time();
		for ($k = 1; $k <= $ahead; $k++) {
			$trend = $intercept + ($slope * ($n - 1 + $k));
			// Blend trend with mean (70/30) and floor at zero.
			$val = max(0.0, ($trend * 0.7) + ($avg * 0.3));
			$cursor = strtotime('+1 month', $cursor);
			$forecast[] = array(
				'label' => date('M Y', $cursor),
				'value' => round($val, 2),
			);
		}

		// Confidence from coefficient of variation of history.
		$conf = 'low';
		if ($n >= 3) {
			$mean = $avg ?: 1;
			$var = 0.0;
			foreach ($rev as $v) { $var += pow($v - $avg, 2); }
			$cv = sqrt($var / $n) / abs($mean);
			$conf = $cv < 0.15 ? 'high' : ($cv < 0.4 ? 'medium' : 'low');
		}

		return array(
			'series' => $series,
			'forecast' => $forecast,
			'trend_per_month' => round($slope, 2),
			'confidence' => $conf,
		);
	}

	/**
	 * Cash-flow projection for the next $ahead months.
	 * Baseline = current cash. Each month adds the trailing average operating
	 * cash movement (profit as a proxy for operating cash), plus a one-off
	 * expected collection of current AR in month 1 and expected payment of
	 * current AP in month 1 (working-capital unwind).
	 *
	 * @return array<string,mixed>
	 */
	function epc_bos_ai_cashflow_forecast(PDO $db, int $ahead = 3): array
	{
		$series = epc_bos_ai_monthly_series($db, 6);
		list($s, $e) = epc_bos_ai_month_bounds(time());
		$dash = epc_erp_dashboard($db, strtotime('-6 month', $s), $e);
		$cash = (float) ($dash['cash_bank_total'] ?? 0);
		$ar = (float) ($dash['customer_ledger_balance'] ?? 0);
		$ap = (float) ($dash['payable_balance'] ?? 0);

		$profits = array_map(function ($r) { return (float) $r['profit']; }, $series);
		$avgOp = $profits ? array_sum($profits) / count($profits) : 0.0;

		$points = array();
		$running = $cash;
		$cursor = time();
		for ($k = 1; $k <= $ahead; $k++) {
			$cursor = strtotime('+1 month', $cursor);
			$collections = $k === 1 ? ($ar * 0.6) : ($k === 2 ? ($ar * 0.3) : 0.0);
			$payments = $k === 1 ? ($ap * 0.6) : ($k === 2 ? ($ap * 0.3) : 0.0);
			$net = $avgOp + $collections - $payments;
			$running += $net;
			$points[] = array(
				'label' => date('M Y', $cursor),
				'net' => round($net, 2),
				'expected_collections' => round($collections, 2),
				'expected_payments' => round($payments, 2),
				'projected_cash' => round($running, 2),
			);
		}
		$minCash = $points ? min(array_map(function ($p) { return $p['projected_cash']; }, $points)) : $cash;
		return array(
			'opening_cash' => round($cash, 2),
			'ar' => round($ar, 2),
			'ap' => round($ap, 2),
			'avg_operating_cash' => round($avgOp, 2),
			'points' => $points,
			'min_projected_cash' => round($minCash, 2),
			'liquidity_alert' => $minCash < 0,
		);
	}

	/**
	 * Predictive inventory: for each stocked item compute the daily consumption
	 * rate from outbound movements over the trailing window, the days of cover at
	 * current on-hand, and a reorder recommendation.
	 *
	 * @return array<int,array<string,mixed>>
	 */
	function epc_bos_ai_inventory_predictions(PDO $db, int $windowDays = 90, int $coverTargetDays = 30): array
	{
		if (!function_exists('epc_erp_inventory_ensure_schema')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
		}
		epc_erp_inventory_ensure_schema($db);
		$since = time() - ($windowDays * 86400);
		$outTypes = "('sale_out','transfer_out','return_out')";
		// Consumption per item over the window.
		$sql = "SELECT m.`item_id`, SUM(m.`qty`) AS used_qty
			 FROM `epc_erp_inv_movements` m
			 WHERE m.`active` = 1 AND m.`movement_type` IN $outTypes AND m.`movement_date` >= ?
			 GROUP BY m.`item_id`";
		$st = $db->prepare($sql);
		$st->execute(array($since));
		$consumption = array();
		foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
			$consumption[(int) $r['item_id']] = (float) $r['used_qty'];
		}

		// Current on-hand by item.
		$onHand = array();
		$avgCost = array();
		$st2 = $db->query("SELECT `item_id`, SUM(`qty_on_hand`) AS qty, AVG(`avg_unit_cost`) AS cost
			 FROM `epc_erp_inv_stock` GROUP BY `item_id`");
		foreach ($st2->fetchAll(PDO::FETCH_ASSOC) as $r) {
			$onHand[(int) $r['item_id']] = (float) $r['qty'];
			$avgCost[(int) $r['item_id']] = (float) $r['cost'];
		}

		$items = $db->query("SELECT `id`, `sku`, `name`, `unit`,
			" . (epc_erp_inventory_has_column($db, 'epc_erp_inv_items', 'reorder_level') ? '`reorder_level`' : '0 AS reorder_level') . "
			 FROM `epc_erp_inv_items` WHERE `active` = 1")->fetchAll(PDO::FETCH_ASSOC);

		$out = array();
		foreach ($items as $it) {
			$id = (int) $it['id'];
			$used = $consumption[$id] ?? 0.0;
			if ($used <= 0) {
				continue; // no demand signal — skip from predictions
			}
			$daily = $used / max(1, $windowDays);
			$have = $onHand[$id] ?? 0.0;
			$cover = $daily > 0 ? ($have / $daily) : 9999;
			$targetQty = $daily * $coverTargetDays;
			$recommend = max(0.0, $targetQty - $have);
			$status = $cover <= 7 ? 'critical' : ($cover <= $coverTargetDays ? 'reorder' : 'ok');
			$out[] = array(
				'item_id' => $id,
				'sku' => (string) $it['sku'],
				'name' => (string) $it['name'],
				'unit' => (string) ($it['unit'] ?? 'pcs'),
				'on_hand' => round($have, 3),
				'daily_use' => round($daily, 3),
				'days_cover' => round($cover, 1),
				'recommend_qty' => round($recommend, 2),
				'reorder_value' => round($recommend * ($avgCost[$id] ?? 0), 2),
				'status' => $status,
			);
		}
		// Critical first, then by least cover.
		usort($out, function ($a, $b) {
			$rank = array('critical' => 0, 'reorder' => 1, 'ok' => 2);
			if ($rank[$a['status']] !== $rank[$b['status']]) {
				return $rank[$a['status']] <=> $rank[$b['status']];
			}
			return $a['days_cover'] <=> $b['days_cover'];
		});
		return $out;
	}

	/**
	 * Automated decision support: turn the KPI set + forecasts into a prioritised
	 * list of recommendations with severity and a concrete suggested action.
	 *
	 * @return array<int,array<string,string>>
	 */
	function epc_bos_ai_recommendations(PDO $db, int $dateFrom, int $dateTo): array
	{
		$kpis = epc_bos_intel_kpis($db, $dateFrom, $dateTo);
		$by = array();
		foreach ($kpis as $k) { $by[$k['key']] = $k; }
		$rec = array();
		$add = function ($sev, $title, $action) use (&$rec) {
			$rec[] = array('severity' => $sev, 'title' => $title, 'action' => $action);
		};

		$dso = (float) ($by['dso']['value'] ?? 0);
		$dpo = (float) ($by['dpo']['value'] ?? 0);
		$gm = (float) ($by['gross_margin']['value'] ?? 0);
		$cr = (float) ($by['current_ratio']['value'] ?? 0);
		$invT = (float) ($by['inv_turnover']['value'] ?? 0);
		$cash = (float) ($by['cash']['value'] ?? 0);
		$ar = (float) ($by['ar']['value'] ?? 0);

		if ($dso > 60) {
			$add('high', 'Receivables are slow (DSO ' . round($dso) . ' days)',
				'Prioritise collections on the oldest AR; consider deposits or early-payment discounts.');
		} elseif ($dso > 45) {
			$add('medium', 'Collection cycle creeping up (DSO ' . round($dso) . ' days)',
				'Send statements and chase invoices over 45 days.');
		}
		if ($gm > 0 && $gm < 15) {
			$add('high', 'Gross margin is thin (' . round($gm, 1) . '%)',
				'Review pricing and supplier costs on low-margin lines.');
		}
		if ($cr > 0 && $cr < 1.0) {
			$add('high', 'Liquidity is tight (current ratio ' . round($cr, 2) . ')',
				'Defer discretionary spend; accelerate collections and arrange a buffer facility.');
		}
		if ($invT > 0 && $invT < 2) {
			$add('medium', 'Inventory is turning slowly (' . round($invT, 1) . 'x)',
				'Identify slow movers; run a promotion or reduce reorder quantities.');
		}
		if ($dpo > 0 && $dpo < 20) {
			$add('low', 'Paying suppliers very fast (DPO ' . round($dpo) . ' days)',
				'Negotiate longer terms to keep cash in the business.');
		}
		if ($cash < 0) {
			$add('high', 'Cash & bank position is negative',
				'Review the cash-flow forecast and prioritise inflows this week.');
		}

		// Forecast-driven signals.
		$fc = epc_bos_ai_revenue_forecast($db, 6, 3);
		if ($fc['trend_per_month'] < 0) {
			$add('medium', 'Revenue trend is declining (' . epc_erp_money($fc['trend_per_month']) . '/mo)',
				'Review pipeline and marketing; the 3-month forecast is trending down.');
		}
		$cff = epc_bos_ai_cashflow_forecast($db, 3);
		if (!empty($cff['liquidity_alert'])) {
			$add('high', 'Projected cash dips below zero within 3 months',
				'Build a collections plan and stagger supplier payments to stay liquid.');
		}

		$inv = epc_bos_ai_inventory_predictions($db);
		$crit = array_filter($inv, function ($i) { return $i['status'] === 'critical'; });
		if ($crit) {
			$add('high', count($crit) . ' item(s) will stock out within ~7 days',
				'Raise purchase orders now for the critical items in the predictive list.');
		}

		if (!$rec) {
			$add('low', 'No red flags detected', 'Core KPIs are within healthy ranges for this period.');
		}
		// Order by severity.
		usort($rec, function ($a, $b) {
			$rank = array('high' => 0, 'medium' => 1, 'low' => 2);
			return $rank[$a['severity']] <=> $rank[$b['severity']];
		});
		return $rec;
	}

	/** Whether an external LLM is wired (for full free-text NL chat). */
	function epc_bos_ai_llm_available(): bool
	{
		$key = getenv('OPENAI_API_KEY');
		return is_string($key) && $key !== '';
	}

	/**
	 * Natural-language assistant. Intent-matches the question against the live
	 * metrics and returns a direct answer. Works with no external dependency;
	 * if an LLM key is present a richer free-form path can be added later.
	 *
	 * @return array<string,mixed>
	 */
	function epc_bos_ai_answer(PDO $db, string $question, int $dateFrom, int $dateTo): array
	{
		$q = strtolower(trim($question));
		if ($q === '') {
			return array('answer' => 'Ask me about revenue, cash, receivables, payables, margin, forecasts, or what to reorder.', 'kind' => 'help');
		}
		$kpis = epc_bos_intel_kpis($db, $dateFrom, $dateTo);
		$by = array();
		foreach ($kpis as $k) { $by[$k['key']] = $k; }
		$money = function ($v) { return epc_erp_money($v) . ' AED'; };

		$has = function (array $words) use ($q) {
			foreach ($words as $w) { if (strpos($q, $w) !== false) { return true; } }
			return false;
		};

		if ($has(array('forecast', 'predict', 'next month', 'projection')) && $has(array('cash', 'liquid'))) {
			$cf = epc_bos_ai_cashflow_forecast($db, 3);
			$parts = array();
			foreach ($cf['points'] as $p) { $parts[] = $p['label'] . ': ' . $money($p['projected_cash']); }
			return array('answer' => 'Projected cash — ' . implode('; ', $parts) . '.'
				. ($cf['liquidity_alert'] ? ' ⚠ Cash is projected to go negative.' : ''), 'kind' => 'cashflow_forecast', 'data' => $cf);
		}
		if ($has(array('forecast', 'predict', 'next month', 'projection', 'trend')) && $has(array('revenue', 'sales'))) {
			$fc = epc_bos_ai_revenue_forecast($db, 6, 3);
			$parts = array();
			foreach ($fc['forecast'] as $p) { $parts[] = $p['label'] . ': ' . $money($p['value']); }
			return array('answer' => 'Revenue forecast (' . $fc['confidence'] . ' confidence) — ' . implode('; ', $parts)
				. '. Trend ' . $money($fc['trend_per_month']) . '/month.', 'kind' => 'revenue_forecast', 'data' => $fc);
		}
		if ($has(array('reorder', 'restock', 'stock out', 'stockout', 'run out', 'buy', 'purchase'))) {
			$inv = epc_bos_ai_inventory_predictions($db);
			$top = array_slice($inv, 0, 5);
			if (!$top) { return array('answer' => 'No items show enough demand to recommend a reorder yet.', 'kind' => 'inventory'); }
			$parts = array();
			foreach ($top as $i) { $parts[] = $i['sku'] . ' (' . $i['days_cover'] . 'd cover, order ' . $i['recommend_qty'] . ' ' . $i['unit'] . ')'; }
			return array('answer' => 'Reorder priorities — ' . implode('; ', $parts) . '.', 'kind' => 'inventory', 'data' => $top);
		}
		if ($has(array('recommend', 'advice', 'advise', 'what should', 'insight', 'decision', 'help me'))) {
			$rec = epc_bos_ai_recommendations($db, $dateFrom, $dateTo);
			$parts = array();
			foreach (array_slice($rec, 0, 4) as $r) { $parts[] = '[' . strtoupper($r['severity']) . '] ' . $r['title'] . ' → ' . $r['action']; }
			return array('answer' => implode(' ', $parts), 'kind' => 'recommendations', 'data' => $rec);
		}
		if ($has(array('revenue', 'sales', 'turnover'))) {
			return array('answer' => 'Revenue this period is ' . $money($by['revenue']['value'] ?? 0) . '.', 'kind' => 'kpi');
		}
		if ($has(array('profit', 'margin'))) {
			return array('answer' => 'Gross margin is ' . round((float) ($by['gross_margin']['value'] ?? 0), 1) . '%.', 'kind' => 'kpi');
		}
		if ($has(array('receivable', 'owe me', 'ar ', 'debtor', 'collect'))) {
			return array('answer' => 'Receivables outstanding: ' . $money($by['ar']['value'] ?? 0) . ' (DSO ' . round((float) ($by['dso']['value'] ?? 0)) . ' days).', 'kind' => 'kpi');
		}
		if ($has(array('payable', 'i owe', 'ap ', 'creditor', 'supplier'))) {
			return array('answer' => 'Payables outstanding: ' . $money($by['ap']['value'] ?? 0) . ' (DPO ' . round((float) ($by['dpo']['value'] ?? 0)) . ' days).', 'kind' => 'kpi');
		}
		if ($has(array('cash', 'bank', 'liquid'))) {
			return array('answer' => 'Cash & bank position: ' . $money($by['cash']['value'] ?? 0) . '.', 'kind' => 'kpi');
		}
		if ($has(array('inventory', 'stock', 'warehouse'))) {
			return array('answer' => 'Inventory value: ' . $money($by['inventory']['value'] ?? 0) . ' (turnover ' . round((float) ($by['inv_turnover']['value'] ?? 0), 1) . 'x).', 'kind' => 'kpi');
		}

		return array('answer' => "I can answer questions about revenue, margin, cash, receivables, payables, inventory, forecasts (revenue/cash), reorder priorities, and recommendations. Try: \"what should I do?\" or \"forecast cash flow\".", 'kind' => 'help');
	}
}
