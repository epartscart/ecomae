<?php
/**
 * ERP tab — BOS VAT refund / tourist tax-free register (country-aware).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_vat_refund.php';

$vrScheme = epc_bos_vat_refund_active_scheme($db_link);
$vrCountry = strtoupper((string) epc_bos_vat_refund_company_country($db_link));
$vrFrom = isset($date_from) ? (int) $date_from : 0;
$vrTo = isset($date_to) ? (int) $date_to : 0;
$vrList = epc_bos_vat_refund_list($db_link, $vrFrom, $vrTo);
$vrSum = epc_bos_vat_refund_summary($db_link, $vrFrom, $vrTo);
$csrfLocal = isset($csrf) ? $csrf : '';
$vrCur = (string) ($vrScheme['currency'] ?? '');

$vrStatusLabel = array(
	'recorded' => array('default', 'Recorded'),
	'validated' => array('info', 'Validated'),
	'exported' => array('primary', 'Exported'),
	'refunded' => array('success', 'Refunded'),
	'void' => array('danger', 'Void'),
);
?>

<div class="epc-erp-section">
	<div class="alert alert-info" style="margin-bottom:14px;">
		<strong><i class="fa fa-plane"></i> Tax-free / Tourist VAT refunds</strong> — record retail sales to overseas tourists under your country's VAT refund scheme. Scheme is resolved from your tax area.
		<br>Tax area / country: <strong><?php echo epc_erp_h($vrCountry); ?></strong> ·
		Scheme: <strong><?php echo epc_erp_h((string) $vrScheme['name']); ?></strong>
		<?php if (!empty($vrScheme['operator'])): ?> · Operator: <strong><?php echo epc_erp_h((string) $vrScheme['operator']); ?></strong><?php endif; ?>
		<?php if (!empty($vrScheme['authority'])): ?> · <?php echo epc_erp_h((string) $vrScheme['authority']); ?><?php endif; ?>
		<?php if (empty($vrScheme['enabled'])): ?> <span class="label label-warning">no country scheme configured</span><?php endif; ?>
		<div class="text-muted" style="font-size:12px;margin-top:6px;"><?php echo epc_erp_h((string) $vrScheme['note']); ?></div>
	</div>

	<div class="row" style="margin-bottom:16px;">
		<div class="col-sm-2 col-xs-4"><div class="epc-erp-kpi" style="border-left:4px solid #34495e;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:22px;font-weight:700;"><?php echo (int) $vrSum['count']; ?></div><div class="text-muted">Records</div></div></div>
		<div class="col-sm-2 col-xs-4"><div class="epc-erp-kpi" style="border-left:4px solid #2980b9;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:22px;font-weight:700;"><?php echo number_format((float) $vrSum['sales'], 2); ?></div><div class="text-muted">Tax-free sales</div></div></div>
		<div class="col-sm-2 col-xs-4"><div class="epc-erp-kpi" style="border-left:4px solid #8e44ad;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:22px;font-weight:700;"><?php echo number_format((float) $vrSum['vat'], 2); ?></div><div class="text-muted">VAT charged</div></div></div>
		<div class="col-sm-2 col-xs-4"><div class="epc-erp-kpi" style="border-left:4px solid #27ae60;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:22px;font-weight:700;"><?php echo number_format((float) $vrSum['refund'], 2); ?></div><div class="text-muted">Refund to tourists</div></div></div>
		<div class="col-sm-2 col-xs-4"><div class="epc-erp-kpi" style="border-left:4px solid #e67e22;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:22px;font-weight:700;"><?php echo number_format((float) $vrSum['fee'], 2); ?></div><div class="text-muted">Operator fees</div></div></div>
		<div class="col-sm-2 col-xs-4"><div class="epc-erp-kpi" style="border-left:4px solid #16a085;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:22px;font-weight:700;"><?php echo number_format((float) $vrSum['retained'], 2); ?></div><div class="text-muted">Retained (VAT−refund)</div></div></div>
	</div>

	<div class="panel panel-default">
		<div class="panel-heading"><strong>Record a tax-free sale</strong></div>
		<div class="panel-body">
			<form data-bos-action="bos_vat_refund_save" id="epc_vr_form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="form-group col-sm-3"><label>Tax-free tag / ref</label><input type="text" name="tag_ref" class="form-control input-sm" placeholder="Planet tag no."></div>
					<div class="form-group col-sm-3"><label>Invoice ref</label><input type="text" name="invoice_ref" class="form-control input-sm" placeholder="SI-..."></div>
					<div class="form-group col-sm-3"><label>Sale date</label><input type="date" name="sale_date" class="form-control input-sm" value="<?php echo epc_erp_h(date('Y-m-d')); ?>"></div>
					<div class="form-group col-sm-3"><label>Status</label>
						<select name="status" class="form-control input-sm">
							<option value="recorded">Recorded</option>
							<option value="validated">Validated</option>
							<option value="exported">Exported</option>
							<option value="refunded">Refunded</option>
						</select>
					</div>
				</div>
				<div class="row">
					<div class="form-group col-sm-3"><label>Tourist name</label><input type="text" name="customer_name" class="form-control input-sm" placeholder="Customer name"></div>
					<div class="form-group col-sm-3"><label>Passport no.</label><input type="text" name="passport_no" class="form-control input-sm"></div>
					<div class="form-group col-sm-3"><label>Nationality</label><input type="text" name="nationality" class="form-control input-sm"></div>
				</div>
				<div class="row">
					<div class="form-group col-sm-3"><label>Sale amount (ex VAT)</label><input type="number" step="0.01" name="sale_amount" id="epc_vr_sale" class="form-control input-sm" value="0"></div>
					<div class="form-group col-sm-3"><label>VAT amount <small class="text-muted">(auto at <?php echo number_format((float) ($vrScheme['vat_rate'] ?? 0), 1); ?>% if blank)</small></label><input type="number" step="0.01" name="vat_amount" id="epc_vr_vat" class="form-control input-sm" placeholder="auto"></div>
					<div class="form-group col-sm-6"><label>Computed refund</label>
						<p class="form-control-static" style="margin:0;">
							Refund to tourist: <strong id="epc_vr_refund" style="color:#27ae60;">0.00</strong> <?php echo epc_erp_h($vrCur); ?>
							· fee <strong id="epc_vr_fee">0.00</strong>
							· retained <strong id="epc_vr_ret">0.00</strong>
						</p>
					</div>
				</div>
				<div class="row"><div class="form-group col-sm-9"><label>Notes</label><input type="text" name="notes" class="form-control input-sm"></div>
					<div class="form-group col-sm-3" style="padding-top:24px;"><button class="btn btn-sm btn-primary" type="submit"><i class="fa fa-save"></i> Save record</button></div>
				</div>
			</form>
		</div>
	</div>

	<table class="table table-bordered table-condensed">
		<thead><tr><th>Date</th><th>Tag / Invoice</th><th>Tourist</th><th>Nationality</th><th class="text-right">Sale</th><th class="text-right">VAT</th><th class="text-right">Refund</th><th class="text-right">Fee</th><th>Status</th><th></th></tr></thead>
		<tbody>
		<?php if (empty($vrList)): ?>
			<tr><td colspan="10" class="text-muted text-center" style="padding:18px;">No tax-free sales recorded in this period yet.</td></tr>
		<?php else: foreach ($vrList as $r): $sl = $vrStatusLabel[$r['status']] ?? array('default', $r['status']); ?>
			<tr>
				<td><?php echo epc_erp_h((int) $r['sale_date'] > 0 ? date('d M Y', (int) $r['sale_date']) : '—'); ?></td>
				<td><strong><?php echo epc_erp_h((string) $r['tag_ref']); ?></strong><?php if (!empty($r['invoice_ref'])): ?><br><small class="text-muted"><?php echo epc_erp_h((string) $r['invoice_ref']); ?></small><?php endif; ?></td>
				<td><?php echo epc_erp_h((string) $r['customer_name']); ?><?php if (!empty($r['passport_no'])): ?><br><small class="text-muted"><?php echo epc_erp_h((string) $r['passport_no']); ?></small><?php endif; ?></td>
				<td><?php echo epc_erp_h((string) $r['nationality']); ?></td>
				<td class="text-right"><?php echo number_format((float) $r['sale_amount'], 2); ?></td>
				<td class="text-right"><?php echo number_format((float) $r['vat_amount'], 2); ?></td>
				<td class="text-right"><strong style="color:#27ae60;"><?php echo number_format((float) $r['refund_amount'], 2); ?></strong></td>
				<td class="text-right"><?php echo number_format((float) $r['fee_amount'], 2); ?></td>
				<td><span class="label label-<?php echo epc_erp_h($sl[0]); ?>"><?php echo epc_erp_h($sl[1]); ?></span></td>
				<td>
					<?php if ($r['status'] !== 'refunded' && $r['status'] !== 'void'): ?>
					<form data-bos-action="bos_vat_refund_status" style="display:inline;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="id" value="<?php echo (int) $r['id']; ?>">
						<input type="hidden" name="status" value="refunded">
						<button class="btn btn-xs btn-success" type="submit" title="Mark refunded">Mark refunded</button>
					</form>
					<?php endif; ?>
				</td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
</div>

<script>
(function(){
	var rate = <?php echo json_encode((float) ($vrScheme['refund_rate'] ?? 1)); ?>;
	var fee = <?php echo json_encode((float) ($vrScheme['fee_per_tag'] ?? 0)); ?>;
	var vatRate = <?php echo json_encode((float) ($vrScheme['vat_rate'] ?? 0)); ?>;
	var sale = document.getElementById('epc_vr_sale');
	var vat = document.getElementById('epc_vr_vat');
	function calc(){
		var v = parseFloat(vat.value);
		if (isNaN(v) || v <= 0) { v = (parseFloat(sale.value) || 0) * (vatRate/100); }
		if (isNaN(v) || v < 0) v = 0;
		var refund = (v * rate) - fee; if (refund < 0) refund = 0;
		var f = (v > 0) ? fee : 0;
		document.getElementById('epc_vr_refund').textContent = refund.toFixed(2);
		document.getElementById('epc_vr_fee').textContent = f.toFixed(2);
		document.getElementById('epc_vr_ret').textContent = (v - refund).toFixed(2);
	}
	if (sale && vat) { sale.addEventListener('input', calc); vat.addEventListener('input', calc); calc(); }
})();
</script>
