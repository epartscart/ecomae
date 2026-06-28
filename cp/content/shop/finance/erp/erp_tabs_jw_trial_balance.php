<?php
/**
 * Dual Trial Balance — Weight + Value reporting for jewellery tenants.
 * Suntech ef-window style with tabs for Weight TB, Value TB, Combined.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
include __DIR__ . '/erp_entry_form_css.php';
epc_jw_ensure_integration_schema($db_link);

erp_page_header(
	'<i class="fa fa-balance-scale"></i> Dual trial balance',
	'Jewellery industry: Weight-based + Value-based trial balance side by side.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'General ledger'),
		array('label' => 'Dual trial balance'),
	)
);

$wtTB = epc_jw_weight_trial_balance($db_link, $date_from, $date_to);
$valTB = epc_jw_value_trial_balance($db_link, $date_from, $date_to);
$activeView = isset($_GET['tb_view']) ? (string)$_GET['tb_view'] : 'weight';
?>
<div class="ef-window">
	<div class="ef-title"><i class="fa fa-balance-scale"></i> Dual Trial Balance</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs<?php echo $activeView === 'weight' ? ' active' : ''; ?>" onclick="jwTbView('weight')"><i class="fa fa-balance-scale"></i> Weight TB</button>
		<button class="btn btn-default btn-xs<?php echo $activeView === 'value' ? ' active' : ''; ?>" onclick="jwTbView('value')"><i class="fa fa-money"></i> Value TB</button>
		<button class="btn btn-default btn-xs<?php echo $activeView === 'combined' ? ' active' : ''; ?>" onclick="jwTbView('combined')"><i class="fa fa-columns"></i> Combined</button>
		<span style="flex:1"></span>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
	</div>
	<div class="ef-body">

		<!-- Weight Trial Balance -->
		<div id="jw_tb_weight" style="<?php echo $activeView !== 'weight' ? 'display:none' : ''; ?>">
			<div class="ef-section">
				<span class="ef-section-title">Weight Trial Balance (grams)</span>
				<?php if (empty($wtTB)): ?>
					<p style="text-align:center;color:#999;padding:20px 0;">No weight ledger entries yet. Transactions will appear after purchase/sale postings.</p>
				<?php else:
					$totalIn = $totalOut = $totalBal = 0;
				?>
				<table class="ef-grid">
					<thead><tr>
						<th>Account</th><th>Metal</th><th>Karat</th>
						<th style="text-align:right">Weight In (g)</th>
						<th style="text-align:right">Weight Out (g)</th>
						<th style="text-align:right">Balance (g)</th>
					</tr></thead>
					<tbody>
					<?php foreach ($wtTB as $row):
						$totalIn += (float)$row['total_weight_in'];
						$totalOut += (float)$row['total_weight_out'];
						$totalBal += (float)$row['weight_balance'];
						$bal = (float)$row['weight_balance'];
					?>
						<tr>
							<td><strong><?php echo epc_erp_h($row['account_code']); ?></strong> <?php echo epc_erp_h($row['account_name']); ?></td>
							<td><?php echo epc_erp_h($row['metal_type'] ?: '—'); ?></td>
							<td><?php echo epc_erp_h($row['karat'] ?: '—'); ?></td>
							<td style="text-align:right"><?php echo number_format((float)$row['total_weight_in'], 3); ?></td>
							<td style="text-align:right"><?php echo number_format((float)$row['total_weight_out'], 3); ?></td>
							<td style="text-align:right;font-weight:bold;color:<?php echo $bal >= 0 ? '#2e7d32' : '#c62828'; ?>"><?php echo number_format($bal, 3); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
					<tfoot>
						<tr><td colspan="3"><strong>Totals</strong></td>
							<td style="text-align:right"><strong><?php echo number_format($totalIn, 3); ?></strong></td>
							<td style="text-align:right"><strong><?php echo number_format($totalOut, 3); ?></strong></td>
							<td style="text-align:right"><strong><?php echo number_format($totalBal, 3); ?></strong></td>
						</tr>
					</tfoot>
				</table>
				<?php endif; ?>
			</div>
		</div>

		<!-- Value Trial Balance -->
		<div id="jw_tb_value" style="<?php echo $activeView !== 'value' ? 'display:none' : ''; ?>">
			<div class="ef-section">
				<span class="ef-section-title">Value Trial Balance (AED)</span>
				<?php if (empty($valTB)): ?>
					<p style="text-align:center;color:#999;padding:20px 0;">No value ledger entries yet.</p>
				<?php else:
					$totalDr = $totalCr = $totalBal2 = 0;
				?>
				<table class="ef-grid">
					<thead><tr>
						<th>Account</th>
						<th style="text-align:right">Debit (AED)</th>
						<th style="text-align:right">Credit (AED)</th>
						<th style="text-align:right">Balance (AED)</th>
					</tr></thead>
					<tbody>
					<?php foreach ($valTB as $row):
						$totalDr += (float)$row['total_debit'];
						$totalCr += (float)$row['total_credit'];
						$totalBal2 += (float)$row['balance'];
						$bal2 = (float)$row['balance'];
					?>
						<tr>
							<td><strong><?php echo epc_erp_h($row['account_code']); ?></strong> <?php echo epc_erp_h($row['account_name']); ?></td>
							<td style="text-align:right"><?php echo epc_erp_money($row['total_debit']); ?></td>
							<td style="text-align:right"><?php echo epc_erp_money($row['total_credit']); ?></td>
							<td style="text-align:right;font-weight:bold;color:<?php echo $bal2 >= 0 ? '#2e7d32' : '#c62828'; ?>"><?php echo epc_erp_money($bal2); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
					<tfoot>
						<tr><td><strong>Totals</strong></td>
							<td style="text-align:right"><strong><?php echo epc_erp_money($totalDr); ?></strong></td>
							<td style="text-align:right"><strong><?php echo epc_erp_money($totalCr); ?></strong></td>
							<td style="text-align:right"><strong><?php echo epc_erp_money($totalBal2); ?></strong></td>
						</tr>
					</tfoot>
				</table>
				<?php endif; ?>
			</div>
		</div>

		<!-- Combined View -->
		<div id="jw_tb_combined" style="<?php echo $activeView !== 'combined' ? 'display:none' : ''; ?>">
			<div class="ef-section">
				<span class="ef-section-title">Combined Dual Trial Balance</span>
				<?php if (empty($wtTB)): ?>
					<p style="text-align:center;color:#999;padding:20px 0;">No dual ledger entries yet.</p>
				<?php else: ?>
				<table class="ef-grid">
					<thead><tr>
						<th>Account</th><th>Metal</th><th>Karat</th>
						<th style="text-align:right">Wt In (g)</th><th style="text-align:right">Wt Out (g)</th><th style="text-align:right">Wt Bal (g)</th>
						<th style="text-align:right">Dr (AED)</th><th style="text-align:right">Cr (AED)</th><th style="text-align:right">Val Bal (AED)</th>
					</tr></thead>
					<tbody>
					<?php foreach ($wtTB as $row):
						$wb = (float)$row['weight_balance'];
						$vb = (float)$row['value_balance'];
					?>
						<tr>
							<td><strong><?php echo epc_erp_h($row['account_code']); ?></strong> <?php echo epc_erp_h($row['account_name']); ?></td>
							<td><?php echo epc_erp_h($row['metal_type'] ?: '—'); ?></td>
							<td><?php echo epc_erp_h($row['karat'] ?: '—'); ?></td>
							<td style="text-align:right"><?php echo number_format((float)$row['total_weight_in'], 3); ?></td>
							<td style="text-align:right"><?php echo number_format((float)$row['total_weight_out'], 3); ?></td>
							<td style="text-align:right;font-weight:bold;color:<?php echo $wb >= 0 ? '#2e7d32' : '#c62828'; ?>"><?php echo number_format($wb, 3); ?></td>
							<td style="text-align:right"><?php echo epc_erp_money($row['total_debit']); ?></td>
							<td style="text-align:right"><?php echo epc_erp_money($row['total_credit']); ?></td>
							<td style="text-align:right;font-weight:bold;color:<?php echo $vb >= 0 ? '#2e7d32' : '#c62828'; ?>"><?php echo epc_erp_money($vb); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<?php endif; ?>
			</div>
		</div>

	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Period: <?php echo epc_erp_h($date_from_str); ?> to <?php echo epc_erp_h($date_to_str); ?></span>
	</div>
</div>
<script>
function jwTbView(pane){
	['weight','value','combined'].forEach(function(p){
		document.getElementById('jw_tb_'+p).style.display=(p===pane)?'block':'none';
	});
	document.querySelectorAll('.ef-toolbar .btn').forEach(function(b){b.classList.remove('active');});
	event.target.closest('.btn').classList.add('active');
}
</script>
