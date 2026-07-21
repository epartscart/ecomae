<?php
/**
 * Module: Bank Account.
 * Sub-modules: Bank account details, Cheque printing, Bank parameters,
 * Bank transaction report.
 */
defined('_ASTEXE_') or die('No access');

$epcBankGuard = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_tenant_data_guard.php';
if (is_file($epcBankGuard)) {
	require_once $epcBankGuard;
	if (function_exists('epc_tenant_data_guard_active') && epc_tenant_data_guard_active()) {
		echo epc_tenant_data_guard_banner('bank');
		return;
	}
}

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'accounts';
$subs = array(
	'accounts' => 'Bank account details',
	'cheque' => 'Cheque printing',
	'parameters' => 'Bank parameters',
	'report' => 'Bank transaction report',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-university"></i> Bank Account</h3>';
echo '<p class="text-muted">Bank account details, cheque register &amp; printing, bank parameters and the bank transaction report. Per-tenant.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'bank_setup', 'finance', $date_from_str, $date_to_str, $subs, $view);

// Load bank accounts (existing engine).
$bankAccts = array();
try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_schema.php';
	if (function_exists('epc_erp_ensure_schema')) {
		epc_erp_ensure_schema($db_link);
	}
	$bankAccts = $db_link->query("SELECT * FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1 AND `account_type` = 'bank' ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) ?: array();
} catch (Exception $e) {
}

switch ($view) {
	case 'cheque':
		$chOpts = array();
		foreach ($bankAccts as $a) {
			$chOpts[(string) $a['id']] = $a['name'] . ($a['account_number'] ? ' · ' . $a['account_number'] : '');
		}
		echo '<div class="epc-erp-section pm-section"><h4><i class="fa fa-pencil-square-o"></i> Record / print cheque</h4>';
		echo '<form class="pm-form epc-erp-pm-cheque-form"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '"><div class="pm-fields">';
		echo '<div class="pm-field"><label>Bank account</label><select name="bank_account_id" class="form-control input-sm">';
		if (empty($chOpts)) {
			echo '<option value="0">— no bank account —</option>';
		}
		foreach ($chOpts as $v => $t) {
			echo '<option value="' . epc_erp_h((string) $v) . '">' . epc_erp_h((string) $t) . '</option>';
		}
		echo '</select></div>';
		echo '<div class="pm-field"><label>Cheque no</label><input type="text" name="cheque_no" class="form-control input-sm" required></div>';
		echo '<div class="pm-field"><label>Pay to</label><input type="text" name="pay_to" class="form-control input-sm" required></div>';
		echo '<div class="pm-field"><label>Amount</label><input type="number" step="any" name="amount" class="form-control input-sm" required></div>';
		echo '<div class="pm-field"><label>Date</label><input type="date" name="cheque_date" class="form-control input-sm" value="' . epc_erp_h(date('Y-m-d')) . '"></div>';
		echo '<div class="pm-field" style="flex:1 1 100%"><label>Memo</label><input type="text" name="memo" class="form-control input-sm"></div>';
		echo '<div class="pm-field pm-field--btn"><label>&nbsp;</label><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-print"></i> Record cheque</button></div>';
		echo '</div></form></div>';

		$cheques = epc_erp_pm_cheques_list($db_link);
		echo '<div class="epc-erp-section pm-section"><h4><i class="fa fa-list"></i> Cheque register <span class="badge">' . count($cheques) . '</span></h4>';
		if (empty($cheques)) {
			echo '<p class="text-muted">No cheques recorded yet.</p>';
		} else {
			echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Cheque no</th><th>Pay to</th><th>Amount</th><th>Date</th><th>Memo</th><th>Status</th></tr></thead><tbody>';
			foreach ($cheques as $c) {
				echo '<tr><td>' . epc_erp_h((string) $c['cheque_no']) . '</td><td>' . epc_erp_h((string) $c['pay_to']) . '</td><td>' . epc_erp_money((float) $c['amount']) . '</td><td>' . epc_erp_h(date('d M Y', (int) $c['cheque_date'])) . '</td><td>' . epc_erp_h((string) $c['memo']) . '</td><td><span class="label label-success">' . epc_erp_h(ucfirst((string) $c['status'])) . '</span></td></tr>';
			}
			echo '</tbody></table></div>';
		}
		echo '</div>';
		break;

	case 'parameters':
		echo '<div class="epc-erp-section"><h4><i class="fa fa-sliders"></i> Bank parameters</h4>';
		echo '<p class="text-muted">Defaults applied to bank documents. Methods of payment &amp; GL accounts are configured under AR/AP setup; base currency &amp; rounding under Setup &amp; Data → Accounting setup.</p>';
		$methods = array();
		try {
			$methods = epc_erp_pm_list($db_link, 'epc_erp_pm_pay_methods', true);
		} catch (Exception $e) {
		}
		echo '<table class="table table-bordered table-condensed" style="max-width:640px;">';
		echo '<tr><th style="width:40%;">Bank accounts configured</th><td>' . count($bankAccts) . '</td></tr>';
		echo '<tr><th>Bank payment methods</th><td>' . count(array_filter($methods, function ($m) {
			return in_array($m['method_type'], array('bank', 'cheque', 'card'), true);
		})) . '</td></tr>';
		echo '<tr><th>Cheque next number</th><td>auto-incremented in cheque register</td></tr>';
		echo '</table>';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'ap_setup', $date_from_str, $date_to_str, 'purchasing')) . '&amp;pm_view=methods">Configure payment methods</a>';
		echo '</div>';
		break;

	case 'report':
		$entries = array();
		try {
			$from = strtotime($date_from_str . ' 00:00:00');
			$to = strtotime($date_to_str . ' 23:59:59');
			$st = $db_link->prepare("SELECT e.*, a.`name` AS acct_name FROM `epc_erp_cash_bank_entries` e
				INNER JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = e.`account_id` AND a.`account_type` = 'bank'
				WHERE e.`active` = 1 AND e.`time` BETWEEN ? AND ? ORDER BY e.`time` DESC LIMIT 300");
			$st->execute(array($from, $to));
			$entries = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
		} catch (Exception $e) {
		}
		echo '<div class="epc-erp-section"><h4><i class="fa fa-exchange"></i> Bank transaction report <small class="text-muted">' . epc_erp_h($date_from_str) . ' → ' . epc_erp_h($date_to_str) . '</small></h4>';
		if (empty($entries)) {
			echo '<p class="text-muted">No bank transactions in this period.</p>';
		} else {
			echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Date</th><th>Account</th><th>Type</th><th>Reference</th><th style="text-align:right;">In</th><th style="text-align:right;">Out</th></tr></thead><tbody>';
			$tin = 0.0;
			$tout = 0.0;
			foreach ($entries as $e) {
				$amt = (float) $e['amount'];
				$isIn = (int) $e['direction'] === 1;
				if ($isIn) {
					$tin += $amt;
				} else {
					$tout += $amt;
				}
				echo '<tr><td>' . epc_erp_h(date('d M Y', (int) $e['time'])) . '</td><td>' . epc_erp_h((string) $e['acct_name']) . '</td><td>' . epc_erp_h((string) $e['entry_type']) . '</td><td>' . epc_erp_h((string) ($e['reference'] ?? '')) . '</td><td style="text-align:right;">' . ($isIn ? epc_erp_money($amt) : '') . '</td><td style="text-align:right;">' . (!$isIn ? epc_erp_money($amt) : '') . '</td></tr>';
			}
			echo '<tr><th colspan="4" style="text-align:right;">Totals</th><th style="text-align:right;">' . epc_erp_money($tin) . '</th><th style="text-align:right;">' . epc_erp_money($tout) . '</th></tr>';
			echo '</tbody></table></div>';
		}
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'cash_bank', $date_from_str, $date_to_str, 'finance')) . '">Open Cash &amp; Bank</a>';
		echo '</div>';
		break;

	case 'accounts':
	default:
		echo '<div class="epc-erp-section"><h4><i class="fa fa-university"></i> Bank account details <span class="badge">' . count($bankAccts) . '</span></h4>';
		if (empty($bankAccts)) {
			echo '<p class="text-muted">No bank accounts yet — add them under Finance → Cash &amp; Bank.</p>';
		} else {
			$buNameMapBS = array();
			try {
				foreach ($db_link->query("SELECT `id`, `name` FROM `epc_erp_pm_business_units`")->fetchAll(PDO::FETCH_ASSOC) as $bu) {
					$buNameMapBS[(int) $bu['id']] = (string) $bu['name'];
				}
			} catch (Exception $e) {
			}
			echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Name</th><th>Business unit</th><th>Bank</th><th>Branch</th><th>Account number</th><th>IBAN</th><th>SWIFT/BIC</th><th>Currency</th><th>Status</th><th style="text-align:right;">Opening balance</th></tr></thead><tbody>';
			foreach ($bankAccts as $a) {
				$buId = (int) ($a['business_unit_id'] ?? 0);
				$buName = $buId > 0 && isset($buNameMapBS[$buId]) ? $buNameMapBS[$buId] : '—';
				echo '<tr><td>' . epc_erp_h((string) $a['name']) . '</td>'
					. '<td>' . epc_erp_h($buName) . '</td>'
					. '<td>' . epc_erp_h((string) ($a['bank_name'] ?? '')) . '</td>'
					. '<td>' . epc_erp_h((string) ($a['bank_branch'] ?? '')) . '</td>'
					. '<td>' . epc_erp_h((string) ($a['account_number'] ?? '')) . '</td>'
					. '<td>' . epc_erp_h((string) ($a['iban'] ?? '')) . '</td>'
					. '<td>' . epc_erp_h((string) ($a['swift_bic'] ?? '')) . '</td>'
					. '<td>' . epc_erp_h((string) $a['currency_code']) . '</td>'
					. '<td>' . epc_erp_h(ucfirst((string) ($a['status'] ?? 'active'))) . '</td>'
					. '<td style="text-align:right;">' . epc_erp_money((float) $a['opening_balance']) . '</td></tr>';
			}
			echo '</tbody></table></div>';
		}
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'cash_bank', $date_from_str, $date_to_str, 'finance')) . '"><i class="fa fa-plus"></i> Manage in Cash &amp; Bank</a>';
		echo '</div>';
		break;
}
