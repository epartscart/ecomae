<?php
/**
 * ERP tab — UAE VAT return (output − input = payable / recoverable).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';

$vat = epc_uae_vat_return_report($db_link, $date_from, $date_to);
$rate = (float)$vat['vat_rate_percent'];
$netLabel = ($vat['net_status'] === 'recoverable_from_fta') ? 'Recoverable from FTA' : 'Payable to FTA';
$netClass = ($vat['net_vat_payable'] >= 0) ? 'text-danger' : 'text-success';
?>

<div class="epc-erp-section">
	<div class="alert alert-info">
		<strong>UAE VAT model:</strong> Sales to UAE customers → <strong><?php echo epc_erp_h(number_format($rate, 2)); ?>% output VAT</strong> on invoice (prices in CP are <em>ex VAT</em>).
		<strong>Advance payments</strong> → output VAT when received; <strong>credited</strong> on final tax invoice.
		Purchases from <strong>UAE suppliers</strong> (country AE, VAT registered) → <?php echo epc_erp_h(number_format($rate, 2)); ?>% <strong>input VAT</strong> (recoverable).
		<strong>Net = Output − Input</strong> → pay Federal Tax Authority or claim refund.
		<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, 'finance')); ?>">Compliance guide</a>
	</div>

	<p class="text-muted">Period: <?php echo epc_erp_h(date('d M Y', $date_from)); ?> — <?php echo epc_erp_h(date('d M Y', $date_to)); ?> · Completed orders only for sales output.</p>

	<div class="epc-erp-kpi">
		<div class="kpi"><div class="lbl">Sales (ex VAT)</div><div class="val"><?php echo epc_erp_money($vat['sales_ex_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Output VAT (2100)</div><div class="val red"><?php echo epc_erp_money($vat['output_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Sales incl. VAT</div><div class="val"><?php echo epc_erp_money($vat['sales_incl_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Purchases (ex VAT)</div><div class="val"><?php echo epc_erp_money($vat['purchase_ex_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Input VAT (1150)</div><div class="val green"><?php echo epc_erp_money($vat['input_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Net VAT</div><div class="val <?php echo epc_erp_h($netClass); ?>"><?php echo epc_erp_money($vat['net_vat_payable']); ?></div></div>
	</div>

	<div class="well well-sm" style="font-size:15px;">
		<strong><?php echo epc_erp_h($netLabel); ?>:</strong>
		<span class="<?php echo epc_erp_h($netClass); ?>" style="font-size:18px;font-weight:700;">
			<?php echo epc_erp_money(abs($vat['net_vat_payable'])); ?> AED
		</span>
		<?php if ($vat['net_vat_payable'] >= 0): ?>
			— remit this amount to FTA for the tax period (after filing VAT return).
		<?php else: ?>
			— input VAT exceeds output; carry forward / claim per FTA rules.
		<?php endif; ?>
	</div>

	<h4><i class="fa fa-calculator"></i> Calculation</h4>
	<table class="table table-bordered table-condensed">
		<tbody>
			<tr><td>Output VAT on sales (<?php echo epc_erp_h(number_format($rate, 2)); ?>% × sales ex VAT)</td><td class="text-right"><strong><?php echo epc_erp_money($vat['output_vat']); ?></strong></td></tr>
			<tr><td>Less: Input VAT on UAE supplier purchases</td><td class="text-right"><strong>− <?php echo epc_erp_money($vat['input_vat']); ?></strong></td></tr>
			<tr class="active"><td><strong>Net VAT <?php echo epc_erp_h($netLabel); ?></strong></td><td class="text-right"><strong><?php echo epc_erp_money($vat['net_vat_payable']); ?></strong></td></tr>
		</tbody>
	</table>

	<h4><i class="fa fa-money"></i> Advance payment VAT (FTA)</h4>
	<table class="table table-bordered table-condensed">
		<tbody>
			<tr><td>Output VAT on advances received (period)</td><td class="text-right"><?php echo epc_erp_money($vat['output_vat_on_advances'] ?? 0); ?></td></tr>
			<tr><td>Credited against tax invoices issued</td><td class="text-right">− <?php echo epc_erp_money($vat['advance_vat_credited_on_invoices'] ?? 0); ?></td></tr>
			<tr><td>Unadjusted advance VAT (awaiting invoice)</td><td class="text-right"><strong><?php echo epc_erp_money($vat['unadjusted_advance_vat'] ?? 0); ?></strong></td></tr>
			<tr><td>Advance payment rows</td><td class="text-right"><?php echo (int)($vat['advance_payment_count'] ?? 0); ?></td></tr>
		</tbody>
	</table>
	<p class="text-muted">Operational sales output VAT above is on <strong>completed orders</strong>; advance VAT follows cash received — reconcile both in your VAT return.</p>

	<?php if (!empty($vat['input_vat_expense_lines'])): ?>
	<h4><i class="fa fa-credit-card"></i> Input VAT by expense type</h4>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Type</th><th class="text-right">Recoverable VAT</th><th class="text-right">Blocked VAT</th></tr></thead>
		<tbody>
		<?php foreach ($vat['input_vat_expense_lines'] as $ln): ?>
			<tr><td><?php echo epc_erp_h($ln['label']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($ln['recoverable_vat']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($ln['blocked_vat']); ?></td></tr>
		<?php endforeach; ?>
			<tr class="active"><td><strong>Total</strong></td>
				<td class="text-right"><strong><?php echo epc_erp_money($vat['input_vat_recoverable_expenses'] ?? 0); ?></strong></td>
				<td class="text-right"><strong><?php echo epc_erp_money($vat['input_vat_blocked_expenses'] ?? 0); ?></strong></td></tr>
		</tbody>
	</table>
	<?php endif; ?>

	<h4><i class="fa fa-book"></i> GL cross-check (same period)</h4>
	<table class="table table-striped table-condensed table-bordered">
		<thead><tr><th>Account</th><th>GL movement</th><th>Operational</th></tr></thead>
		<tbody>
			<tr>
				<td>2100 VAT output (payable)</td>
				<td class="text-right"><?php echo epc_erp_money($vat['gl_output_vat_period']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($vat['output_vat']); ?></td>
			</tr>
			<tr>
				<td>1150 VAT input (recoverable)</td>
				<td class="text-right"><?php echo epc_erp_money($vat['gl_input_vat_period']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($vat['input_vat']); ?></td>
			</tr>
		</tbody>
	</table>
	<p class="text-muted">Post sales and purchases to GL (General ledger tab) so COA balances match operational VAT.</p>

	<h4>Checklist</h4>
	<ol>
		<li>Ensure all suppliers have correct <strong>country</strong> (AE = UAE) and TRN on Payables tab.</li>
		<li>Record supplier invoices on <strong>Purchases</strong> with amount <em>ex VAT</em> — VAT auto-calculated for UAE suppliers.</li>
		<li>Customer order prices are ex VAT; invoice / due = ex + <?php echo epc_erp_h(number_format($rate, 2)); ?>%.</li>
		<li>File VAT return on FTA portal; pay net amount from this report.</li>
		<li><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, 'finance')); ?>">Tax compliance knowledge base</a> — invoice format, excise, CT filing.</li>
		<li>Default VAT rate: CP → <a href="/<?php echo epc_erp_h($GLOBALS['DP_Config']->backend_dir); ?>/shop/price-management">Price management</a>.</li>
	</ol>
</div>
