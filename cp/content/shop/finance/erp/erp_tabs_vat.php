<?php
/**
 * ERP tab — UAE VAT return (output − input = payable / recoverable).
 *
 * Operational summary aligned to FTA VAT 201 under Federal Decree-Law 8/2017
 * (as amended) + Executive Regulations. Informational — file on EmaraTax.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$vat = epc_uae_vat_return_report($db_link, $date_from, $date_to);
$rate = (float) $vat['vat_rate_percent'];
$netLabel = ($vat['net_status'] === 'recoverable_from_fta') ? 'Recoverable from FTA' : 'Payable to FTA';
$netClass = ($vat['net_vat_payable'] >= 0) ? 'text-danger' : 'text-success';
$law = is_array($vat['law'] ?? null) ? $vat['law'] : epc_uae_vat_law_pack($date_to);
$filing = is_array($vat['filing'] ?? null) ? $vat['filing'] : epc_uae_vat_filing_deadline($date_to);
$co = is_array($vat['company'] ?? null) ? $vat['company'] : epc_uae_company_profile($db_link);
$creditLive = !empty($law['credit_limit_live']);
$extVatBase = epc_erp_tab_url($erpUrl, 'ext_reports', $date_from_str, $date_to_str, 'regrep');
$extVatUrl = $extVatBase . ((strpos($extVatBase, '?') !== false) ? '&' : '?') . 'cat=tax&rep=tax__vat_return';

erp_page_header(
	'<i class="fa fa-percent"></i> UAE VAT return',
	'Operational VAT 201 summary — output − input for the selected tax period, under current FTA law.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Tax'),
		array('label' => 'VAT return'),
	)
);
?>

<div class="epc-erp-section" style="margin-bottom:14px;background:#f3fafd;border:1px solid #0b6e99;border-radius:8px;padding:12px 16px;">
	<div style="font-weight:800;color:#0b6e99;font-size:14px;">
		<i class="fa fa-balance-scale"></i> UAE VAT law pack — <?php echo epc_erp_h((string) ($law['pack_label'] ?? 'FDL 8/2017')); ?>
	</div>
	<div class="text-muted" style="font-size:12px;margin-top:6px;line-height:1.55;">
		<strong>Governing law:</strong> <?php echo epc_erp_h((string) ($law['law'] ?? '')); ?>.
		&nbsp;·&nbsp; <strong>Form:</strong> <?php echo epc_erp_h((string) ($law['form'] ?? 'FTA VAT 201')); ?>
		&nbsp;·&nbsp; <strong>Filing:</strong> <?php echo epc_erp_h((string) ($filing['rule'] ?? 'Within 28 days of period end')); ?>
		— due <strong><?php echo epc_erp_h((string) ($filing['due_label'] ?? '—')); ?></strong>
		<?php if (!empty($filing['overdue'])): ?>
			<span class="label label-danger" style="margin-left:4px;">Overdue</span>
		<?php elseif (isset($filing['days_left']) && (int) $filing['days_left'] <= 7): ?>
			<span class="label label-warning" style="margin-left:4px;"><?php echo (int) $filing['days_left']; ?> day(s) left</span>
		<?php endif; ?>
		<?php if ($creditLive): ?>
			&nbsp;·&nbsp; <strong>Art. 74 / Procedures Art. 38:</strong> excess recoverable tax &amp; credit-balance refunds limited to <strong>5 years</strong> from period end (from 1 Jan 2026); transitional claims for aged credits until <strong>31 Dec 2026</strong>.
		<?php endif; ?>
	</div>
	<div style="margin-top:8px;">
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h((string) ($law['authority_url'] ?? 'https://tax.gov.ae')); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-external-link"></i> FTA — tax.gov.ae</a>
		<a class="btn btn-primary btn-sm" href="<?php echo epc_erp_h((string) ($law['portal_url'] ?? 'https://eservices.tax.gov.ae')); ?>" target="_blank" rel="noopener noreferrer"><i class="fa fa-upload"></i> EmaraTax filing portal</a>
		<?php if ($extVatUrl !== ''): ?>
			<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($extVatUrl); ?>"><i class="fa fa-file-text-o"></i> Full VAT 201 (External Reporting)</a>
		<?php endif; ?>
		<a class="btn btn-link btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, 'finance')); ?>">Tax compliance guide</a>
	</div>
</div>

<?php echo epc_uae_fta_erp_banner_html($db_link, epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str)); ?>

<div class="epc-erp-section">
	<div class="alert alert-info" style="margin-bottom:12px;">
		<strong>UAE VAT model (<?php echo epc_erp_h(number_format($rate, 2)); ?>% standard rate):</strong>
		Sales to UAE customers → <strong>output VAT</strong> on tax invoice (CP prices are <em>ex VAT</em>).
		<strong>Advance payments</strong> → output VAT when received; credited on the final tax invoice (Art. 65).
		Purchases from <strong>UAE VAT-registered suppliers</strong> (AE + valid TRN) → <strong>input VAT</strong> recoverable.
		<strong>Net = Output − Input</strong> → Box 14 payable to / recoverable from the FTA.
		Bad-debt output adjustments: <strong>Art. 64</strong> (write-off + &gt;6 months + notify customer — VATP024).
	</div>

	<p class="text-muted">
		Period: <?php echo epc_erp_h(date('d M Y', $date_from)); ?> — <?php echo epc_erp_h(date('d M Y', $date_to)); ?>
		· Completed orders only for sales output
		<?php if (!empty($co['trn'])): ?> · TRN: <code><?php echo epc_erp_h((string) $co['trn_display']); ?></code><?php endif; ?>
		· Pack as of <?php echo epc_erp_h(date('d M Y', (int) ($law['as_of'] ?? $date_to))); ?>
	</p>

	<div class="epc-erp-kpi">
		<div class="kpi"><div class="lbl">Sales (ex VAT)</div><div class="val"><?php echo epc_erp_money($vat['sales_ex_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Output VAT (2100)</div><div class="val red"><?php echo epc_erp_money($vat['output_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Sales incl. VAT</div><div class="val"><?php echo epc_erp_money($vat['sales_incl_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Purchases (ex VAT)</div><div class="val"><?php echo epc_erp_money($vat['purchase_ex_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Input VAT (1150)</div><div class="val green"><?php echo epc_erp_money($vat['input_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Net VAT (Box 14)</div><div class="val <?php echo epc_erp_h($netClass); ?>"><?php echo epc_erp_money($vat['net_vat_payable']); ?></div></div>
	</div>

	<div class="well well-sm" style="font-size:15px;">
		<strong><?php echo epc_erp_h($netLabel); ?>:</strong>
		<span class="<?php echo epc_erp_h($netClass); ?>" style="font-size:18px;font-weight:700;">
			<?php echo epc_erp_money(abs($vat['net_vat_payable'])); ?> AED
		</span>
		<?php if ($vat['net_vat_payable'] >= 0): ?>
			— remit via EmaraTax by <strong><?php echo epc_erp_h((string) ($filing['due_label'] ?? 'the filing deadline')); ?></strong> (Art. 72–73).
		<?php else: ?>
			— input exceeds output; carry forward or claim refund under Art. 74
			<?php if ($creditLive): ?>
				<strong>within 5 years</strong> of this period’s end (transitional aged credits: file by 31 Dec 2026).
			<?php else: ?>
				per FTA rules.
			<?php endif; ?>
		<?php endif; ?>
	</div>

	<h4><i class="fa fa-calculator"></i> Calculation</h4>
	<table class="table table-bordered table-condensed">
		<tbody>
			<tr><td>Output VAT on sales (<?php echo epc_erp_h(number_format($rate, 2)); ?>% × sales ex VAT)</td><td class="text-right"><strong><?php echo epc_erp_money($vat['output_vat']); ?></strong></td></tr>
			<tr><td>Less: Input VAT on UAE supplier purchases</td><td class="text-right"><strong>− <?php echo epc_erp_money($vat['input_vat']); ?></strong></td></tr>
			<tr class="active"><td><strong>Net VAT <?php echo epc_erp_h($netLabel); ?> (Box 14)</strong></td><td class="text-right"><strong><?php echo epc_erp_money($vat['net_vat_payable']); ?></strong></td></tr>
		</tbody>
	</table>

	<h4><i class="fa fa-money"></i> Advance payment VAT (FTA)</h4>
	<table class="table table-bordered table-condensed">
		<tbody>
			<tr><td>Output VAT on advances received (period)</td><td class="text-right"><?php echo epc_erp_money($vat['output_vat_on_advances'] ?? 0); ?></td></tr>
			<tr><td>Credited against tax invoices issued</td><td class="text-right">− <?php echo epc_erp_money($vat['advance_vat_credited_on_invoices'] ?? 0); ?></td></tr>
			<tr><td>Unadjusted advance VAT (awaiting invoice)</td><td class="text-right"><strong><?php echo epc_erp_money($vat['unadjusted_advance_vat'] ?? 0); ?></strong></td></tr>
			<tr><td>Advance payment rows</td><td class="text-right"><?php echo (int) ($vat['advance_payment_count'] ?? 0); ?></td></tr>
		</tbody>
	</table>
	<p class="text-muted">Operational sales output VAT above is on <strong>completed orders</strong>; advance VAT follows cash received — reconcile both on the VAT 201 before filing.</p>

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

	<h4><i class="fa fa-gavel"></i> Key law references (this return)</h4>
	<div class="row">
		<?php
		$artCards = array(
			array('Art. 64', (string) ($law['articles']['bad_debt'] ?? 'Bad-debt relief')),
			array('Art. 65', (string) ($law['articles']['tax_invoice'] ?? 'Tax invoices')),
			array('Art. 72–73', (string) ($law['articles']['payment'] ?? 'Returns & payment')),
			array('Art. 74', (string) ($law['articles']['excess'] ?? 'Excess recoverable tax')),
		);
		foreach ($artCards as $ac):
		?>
			<div class="col-sm-6" style="margin-bottom:10px;">
				<div class="well well-sm" style="margin-bottom:0;min-height:72px;">
					<div style="font-weight:700;color:#0b6e99;"><?php echo epc_erp_h($ac[0]); ?></div>
					<div class="text-muted" style="font-size:12px;margin-top:3px;"><?php echo epc_erp_h($ac[1]); ?></div>
				</div>
			</div>
		<?php endforeach; ?>
	</div>
	<?php if ($creditLive): ?>
		<div class="alert alert-warning" style="margin-top:4px;">
			<strong>2026 credit-limit reminder:</strong> <?php echo epc_erp_h((string) ($law['articles']['refund_limit'] ?? '')); ?>
			Review aged recoverable balances before the transitional window closes on 31 Dec 2026.
		</div>
	<?php endif; ?>

	<h4>Filing checklist</h4>
	<ol>
		<li>Confirm company <strong>TRN</strong> (15 digits) and VAT registration — seller profile / E-invoicing.</li>
		<li>Ensure suppliers have correct <strong>country AE</strong> and TRN on Payables — input VAT only for VAT-registered UAE suppliers.</li>
		<li>Record supplier invoices on <strong>Purchases</strong> with amount <em>ex VAT</em>; review blocked vs recoverable expense VAT.</li>
		<li>Reconcile advance VAT and any <strong>Art. 64</strong> bad-debt adjustments before filing.</li>
		<li>Generate the full <strong>VAT 201</strong> (External Reporting) for Emirate boxes / schedules, then file &amp; pay on EmaraTax by <strong><?php echo epc_erp_h((string) ($filing['due_label'] ?? 'the deadline')); ?></strong>.</li>
		<li><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, 'finance')); ?>">Tax compliance knowledge base</a> — invoice format, excise, CT filing.</li>
<?php if (!empty($epc_erp_cp_links)): ?>		<li>Default VAT rate: CP → <a href="/<?php echo epc_erp_h($GLOBALS['DP_Config']->backend_dir); ?>/shop/price-management">Price management</a>.</li><?php endif; ?>
	</ol>
	<p class="text-muted" style="font-size:11px;margin-top:12px;">
		Informational aid generated <?php echo date('d M Y H:i'); ?> from posted ERP data. Verify figures and the latest EmaraTax form against the official FTA source before filing.
		Procedures law: <?php echo epc_erp_h((string) ($law['procedures_law'] ?? '')); ?>.
	</p>
</div>
