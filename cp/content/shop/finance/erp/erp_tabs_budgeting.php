<?php
/**
 * Module: Budgeting.
 * Sub-modules: Budget account-wise, Budget monthly, Master budget.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-pie-chart"></i> Budgeting</h3>';
echo '<p class="text-muted">Create budgets per business unit / fiscal year, enter account-wise monthly figures, and flag a master (consolidated) budget. Per-tenant and configurable.</p></div>';

try {
	$budgets = epc_erp_pm_budgets_list($db_link);
} catch (Exception $e) {
	$budgets = array();
}
$buOpts = array('0' => '— all / group —');
try {
	foreach (epc_erp_pm_list($db_link, 'epc_erp_pm_business_units', true) as $bu) {
		$buOpts[(string) $bu['id']] = $bu['code'] . ' · ' . $bu['name'];
	}
} catch (Exception $e) {
}

$selBudget = (int) ($_GET['budget_id'] ?? 0);
$months = array(1 => 'Jan', 2 => 'Feb', 3 => 'Mar', 4 => 'Apr', 5 => 'May', 6 => 'Jun', 7 => 'Jul', 8 => 'Aug', 9 => 'Sep', 10 => 'Oct', 11 => 'Nov', 12 => 'Dec');

// New budget form
echo '<div class="epc-erp-section pm-section"><h4><i class="fa fa-plus"></i> New budget</h4>';
echo '<form class="pm-form epc-erp-pm-budget-form"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '"><div class="pm-fields">';
echo '<div class="pm-field"><label>Code</label><input type="text" name="code" class="form-control input-sm" required placeholder="BUD-2026"></div>';
echo '<div class="pm-field"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>';
echo '<div class="pm-field"><label>Fiscal year</label><input type="text" name="fiscal_year" class="form-control input-sm" value="' . epc_erp_h(date('Y')) . '"></div>';
echo '<div class="pm-field"><label>Business unit</label><select name="business_unit_id" class="form-control input-sm">';
foreach ($buOpts as $v => $t) {
	echo '<option value="' . epc_erp_h((string) $v) . '">' . epc_erp_h((string) $t) . '</option>';
}
echo '</select></div>';
echo '<div class="pm-field"><label>Master budget</label><select name="is_master" class="form-control input-sm"><option value="0">No</option><option value="1">Yes (consolidated)</option></select></div>';
echo '<div class="pm-field pm-field--btn"><label>&nbsp;</label><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Create</button></div>';
echo '</div></form></div>';

// Budgets list
echo '<div class="epc-erp-section pm-section"><h4><i class="fa fa-list"></i> Budgets <span class="badge">' . count($budgets) . '</span></h4>';
if (empty($budgets)) {
	echo '<p class="text-muted">No budgets yet.</p>';
} else {
	echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Code</th><th>Name</th><th>FY</th><th>Business unit</th><th>Master</th><th>Total budgeted</th><th></th></tr></thead><tbody>';
	foreach ($budgets as $b) {
		$url = epc_erp_tab_url($erpUrl, 'budgeting', $date_from_str, $date_to_str, 'enterprise') . '&budget_id=' . (int) $b['id'];
		echo '<tr><td>' . epc_erp_h((string) $b['code']) . '</td><td>' . epc_erp_h((string) $b['name']) . '</td><td>' . epc_erp_h((string) $b['fiscal_year']) . '</td><td>' . epc_erp_h((string) ($b['bu_name'] ?? '—')) . '</td><td>' . ((int) $b['is_master'] ? '<span class="label label-info">Master</span>' : '—') . '</td><td>' . epc_erp_money((float) $b['total_amount']) . '</td><td><a class="btn btn-xs btn-default" href="' . epc_erp_h($url) . '">Open lines</a></td></tr>';
	}
	echo '</tbody></table></div>';
}
echo '</div>';

// Selected budget — account-wise monthly lines
if ($selBudget > 0) {
	$hdr = null;
	foreach ($budgets as $b) {
		if ((int) $b['id'] === $selBudget) {
			$hdr = $b;
			break;
		}
	}
	if ($hdr) {
		$lines = epc_erp_pm_budget_lines($db_link, $selBudget);
		echo '<div class="epc-erp-section pm-section"><h4><i class="fa fa-table"></i> ' . epc_erp_h((string) $hdr['name']) . ' — account-wise monthly budget</h4>';
		// add line
		echo '<form class="pm-form epc-erp-pm-budgetline-form"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '"><input type="hidden" name="budget_id" value="' . $selBudget . '"><div class="pm-fields">';
		echo '<div class="pm-field"><label>Account code</label><input type="text" name="account_code" class="form-control input-sm" required placeholder="5000"></div>';
		echo '<div class="pm-field"><label>Account name</label><input type="text" name="account_name" class="form-control input-sm" placeholder="Salaries"></div>';
		echo '<div class="pm-field"><label>Month</label><select name="month_no" class="form-control input-sm">';
		foreach ($months as $mn => $ml) {
			echo '<option value="' . $mn . '">' . $ml . '</option>';
		}
		echo '</select></div>';
		echo '<div class="pm-field"><label>Amount</label><input type="number" step="any" name="amount" class="form-control input-sm" required></div>';
		echo '<div class="pm-field pm-field--btn"><label>&nbsp;</label><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Add line</button></div>';
		echo '</div></form>';

		// pivot account × month
		if (empty($lines)) {
			echo '<p class="text-muted" style="margin-top:8px;">No budget lines yet.</p>';
		} else {
			$pivot = array();
			$names = array();
			$colTot = array_fill(1, 12, 0.0);
			foreach ($lines as $l) {
				$ac = (string) $l['account_code'];
				$names[$ac] = (string) $l['account_name'];
				if (!isset($pivot[$ac])) {
					$pivot[$ac] = array_fill(1, 12, 0.0);
				}
				$pivot[$ac][(int) $l['month_no']] += (float) $l['amount'];
				$colTot[(int) $l['month_no']] += (float) $l['amount'];
			}
			echo '<div class="table-responsive" style="margin-top:10px;"><table class="table table-bordered table-condensed" style="font-size:12px;"><thead><tr><th>Account</th>';
			foreach ($months as $ml) {
				echo '<th style="text-align:right;">' . $ml . '</th>';
			}
			echo '<th style="text-align:right;">Total</th></tr></thead><tbody>';
			$grand = 0.0;
			foreach ($pivot as $ac => $cells) {
				$rowTot = array_sum($cells);
				$grand += $rowTot;
				echo '<tr><td>' . epc_erp_h($ac . ' · ' . ($names[$ac] ?? '')) . '</td>';
				foreach ($months as $mn => $ml) {
					echo '<td style="text-align:right;">' . ($cells[$mn] != 0 ? epc_erp_h(number_format($cells[$mn], 0)) : '<span class="text-muted">—</span>') . '</td>';
				}
				echo '<td style="text-align:right;"><strong>' . epc_erp_h(number_format($rowTot, 0)) . '</strong></td></tr>';
			}
			echo '<tr><th>Total</th>';
			foreach ($months as $mn => $ml) {
				echo '<th style="text-align:right;">' . epc_erp_h(number_format($colTot[$mn], 0)) . '</th>';
			}
			echo '<th style="text-align:right;">' . epc_erp_h(number_format($grand, 0)) . '</th></tr>';
			echo '</tbody></table></div>';
		}
		echo '</div>';
	}
}
