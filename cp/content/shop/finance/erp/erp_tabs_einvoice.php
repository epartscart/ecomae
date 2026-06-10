<?php
/**
 * ERP tab — UAE Electronic Invoicing (PINT-AE / MoF Feb 2026).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';

$einvSection = isset($_GET['einv_section']) ? (string)$_GET['einv_section'] : 'dashboard';
$viewDocId = isset($_GET['einv_doc']) ? (int)$_GET['einv_doc'] : 0;
$viewOrderId = isset($_GET['einv_order']) ? (int)$_GET['einv_order'] : 0;
$viewUserId = isset($_GET['einv_user']) ? (int)$_GET['einv_user'] : 0;

$einvDash = epc_einvoice_dashboard($db_link, $date_from, $date_to);
$readiness = epc_einvoice_readiness_checklist($db_link);
$seller = epc_einvoice_seller_profile($db_link);
$const = epc_einvoice_constants();
$flags = epc_einvoice_transaction_flags();
$taxCats = epc_einvoice_tax_categories();

$einvBase = epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str);
if (!isset($DP_Config) && isset($GLOBALS['DP_Config'])) {
	$DP_Config = $GLOBALS['DP_Config'];
}
$einvAjaxUrl = '/' . (isset($DP_Config->backend_dir) ? (string)$DP_Config->backend_dir : 'cp')
	. '/content/shop/finance/erp/ajax_erp_endpoint.php';
function epc_einv_url($base, $section, $extra = '')
{
	$u = $base . '&einv_section=' . rawurlencode($section);
	return $extra !== '' ? ($u . '&' . $extra) : $u;
}
?>

<div class="epc-erp-section epc-einvoice-panel">
	<div class="alert alert-info" style="border-left:4px solid #1d4ed8;">
		<strong><i class="fa fa-file-code-o"></i> UAE Electronic Invoicing</strong> — 5-corner Peppol model (Supplier → ASP → Buyer ASP → Buyer + FTA reporting).
		Guidelines <strong>V1.0 · 23 Feb 2026</strong> · PINT-AE XML · mandatory fields enforced before ASP submission.
		Voluntary from <strong>1 Jul 2026</strong> · Mandatory phased from <strong>1 Jan 2027</strong> (revenue ≥ AED 50M).
	</div>

	<ul class="nav nav-pills epc-einvoice-nav" style="margin-bottom:20px;flex-wrap:wrap;">
		<li class="<?php echo $einvSection === 'dashboard' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'dashboard')); ?>"><i class="fa fa-dashboard"></i> Dashboard</a></li>
		<li class="<?php echo $einvSection === 'invoices' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'invoices')); ?>"><i class="fa fa-list"></i> Invoices</a></li>
		<li class="<?php echo $einvSection === 'create' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'create')); ?>"><i class="fa fa-plus-circle"></i> Generate</a></li>
		<li class="<?php echo $einvSection === 'seller' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'seller')); ?>"><i class="fa fa-building"></i> Seller profile</a></li>
		<li class="<?php echo $einvSection === 'buyers' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'buyers')); ?>"><i class="fa fa-users"></i> Buyer Peppol</a></li>
		<li class="<?php echo $einvSection === 'asp' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'asp')); ?>"><i class="fa fa-cloud-upload"></i> ASP &amp; FTA</a></li>
		<li class="<?php echo $einvSection === 'guide' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'guide')); ?>"><i class="fa fa-book"></i> Guide</a></li>
	</ul>

	<?php if ($einvSection === 'dashboard'): ?>
		<div class="epc-erp-kpi">
			<div class="kpi"><div class="lbl">E-invoices (period)</div><div class="val"><?php echo (int)$einvDash['total']; ?></div></div>
			<div class="kpi"><div class="lbl">Validated</div><div class="val green"><?php echo (int)$einvDash['validated']; ?></div></div>
			<div class="kpi"><div class="lbl">Submitted / queued</div><div class="val"><?php echo (int)$einvDash['submitted']; ?></div></div>
			<div class="kpi"><div class="lbl">Accepted by ASP</div><div class="val green"><?php echo (int)$einvDash['accepted']; ?></div></div>
			<div class="kpi"><div class="lbl">Rejected</div><div class="val red"><?php echo (int)$einvDash['rejected']; ?></div></div>
			<div class="kpi"><div class="lbl">Total incl. VAT</div><div class="val"><?php echo epc_erp_money($einvDash['amount_incl_vat']); ?> AED</div></div>
		</div>

		<div class="row">
			<div class="col-md-6">
				<h4><i class="fa fa-check-square-o"></i> Readiness checklist</h4>
				<div class="progress" style="height:22px;margin-bottom:12px;">
					<div class="progress-bar progress-bar-success" style="width:<?php echo (int)$readiness['percent']; ?>%;line-height:22px;"><?php echo (int)$readiness['percent']; ?>%</div>
				</div>
				<ul class="list-group">
					<?php foreach ($readiness['items'] as $it): ?>
						<li class="list-group-item">
							<i class="fa fa-<?php echo $it['done'] ? 'check-circle text-success' : 'circle-o text-muted'; ?>"></i>
							<?php echo epc_erp_h($it['label']); ?>
						</li>
					<?php endforeach; ?>
				</ul>
			</div>
			<div class="col-md-6">
				<h4><i class="fa fa-info-circle"></i> Seller status</h4>
				<table class="table table-condensed table-bordered">
					<tr><td>Legal name</td><td><strong><?php echo epc_erp_h($seller['seller_name'] ?: '—'); ?></strong></td></tr>
					<tr><td>TRN</td><td><?php echo epc_erp_h($seller['seller_trn'] ?: '— configure in Seller profile'); ?></td></tr>
					<tr><td>Peppol endpoint</td><td><code><?php echo epc_erp_h($seller['seller_peppol_endpoint'] ?: '0235:__________'); ?></code></td></tr>
					<tr><td>ASP</td><td><?php echo epc_erp_h($einvDash['asp_name'] ?: '— not selected'); ?></td></tr>
					<tr><td>Specification</td><td><small><code><?php echo epc_erp_h($const['specification_id']); ?></code></small></td></tr>
				</table>
				<p class="text-muted">Onboard with your ASP via <a href="https://tax.gov.ae" target="_blank" rel="noopener">EmaraTax</a> → E-Invoicing tile.</p>
			</div>
		</div>

	<?php elseif ($einvSection === 'invoices' || ($einvSection === 'view' && $viewDocId > 0)): ?>
		<?php
		if ($viewDocId > 0):
			$doc = epc_einvoice_get_document($db_link, $viewDocId);
			if (!$doc):
				echo '<div class="alert alert-danger">Document not found.</div>';
			else:
				$sellerD = $doc['seller'];
				$buyerD = $doc['buyer'];
		?>
			<p><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'invoices')); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> Back to list</a>
			<button type="button" class="btn btn-primary btn-sm" onclick="epcEinvDownloadXml(<?php echo (int)$doc['id']; ?>)"><i class="fa fa-download"></i> Download PINT-AE XML</button>
			<?php if ($doc['validation_ok'] && !in_array($doc['status'], array('submitted', 'accepted', 'queued'), true)): ?>
				<button type="button" class="btn btn-success btn-sm" onclick="epcEinvSubmit(<?php echo (int)$doc['id']; ?>)"><i class="fa fa-cloud-upload"></i> Submit to ASP</button>
			<?php endif; ?>
			</p>

			<?php if (!$doc['validation_ok']): ?>
				<div class="alert alert-warning"><strong>Validation errors:</strong>
					<ul style="margin:8px 0 0;"><?php foreach ($doc['validation_errors'] as $err): ?><li><?php echo epc_erp_h($err); ?></li><?php endforeach; ?></ul>
				</div>
			<?php endif; ?>

			<div class="epc-einvoice-preview well" style="background:#fff;padding:24px;border:1px solid #cbd5e1;">
				<h3 style="text-align:center;margin-top:0;">Tax Invoice</h3>
				<p style="text-align:center;color:#64748b;font-size:12px;">
					<code><?php echo epc_erp_h($const['business_process']); ?></code> ·
					<code><?php echo epc_erp_h($const['specification_id']); ?></code>
				</p>
				<div class="row">
					<div class="col-sm-6">
						<h5><strong>SELLER</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Name</td><td><?php echo epc_erp_h($sellerD['seller_name'] ?? ''); ?></td></tr>
							<tr><td>TRN</td><td><?php echo epc_erp_h($sellerD['seller_trn'] ?? ''); ?></td></tr>
							<tr><td>Legal reg.</td><td><?php echo epc_erp_h($sellerD['seller_legal_reg_no'] ?? ''); ?> (<?php echo epc_erp_h($sellerD['seller_legal_reg_type'] ?? 'TL'); ?>)</td></tr>
							<tr><td>Authority</td><td><?php echo epc_erp_h($sellerD['seller_authority_name'] ?? ''); ?></td></tr>
							<tr><td>Address</td><td><?php echo epc_erp_h(($sellerD['seller_address_line1'] ?? '') . ', ' . ($sellerD['seller_city'] ?? '') . ', UAE'); ?></td></tr>
							<tr><td>Electronic address</td><td><code><?php echo epc_erp_h($sellerD['seller_peppol_endpoint'] ?? ''); ?></code></td></tr>
						</table>
					</div>
					<div class="col-sm-6">
						<h5><strong>INVOICE METADATA</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Invoice number</td><td><strong><?php echo epc_erp_h($doc['invoice_number']); ?></strong></td></tr>
							<tr><td>Issue date</td><td><?php echo epc_erp_h(date('Y-m-d', (int)$doc['issue_date'])); ?></td></tr>
							<tr><td>Type code</td><td><?php echo epc_erp_h($doc['invoice_type_code']); ?></td></tr>
							<tr><td>Currency</td><td><?php echo epc_erp_h($doc['currency_code']); ?></td></tr>
							<tr><td>Due date</td><td><?php echo epc_erp_h(date('Y-m-d', (int)$doc['payment_due_date'])); ?></td></tr>
							<tr><td>Transaction code</td><td><code><?php echo epc_erp_h($doc['transaction_type_code']); ?></code></td></tr>
							<tr><td>UUID</td><td><small><code><?php echo epc_erp_h($doc['uuid']); ?></code></small></td></tr>
							<tr><td>Status</td><td><span class="label label-<?php echo $doc['status'] === 'accepted' ? 'success' : ($doc['status'] === 'rejected' ? 'danger' : 'info'); ?>"><?php echo epc_erp_h(strtoupper($doc['status'])); ?></span></td></tr>
						</table>
					</div>
				</div>
				<div class="row">
					<div class="col-sm-6">
						<h5><strong>BUYER</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Name</td><td><?php echo epc_erp_h($buyerD['buyer_name'] ?? ''); ?></td></tr>
							<tr><td>TRN</td><td><?php echo epc_erp_h($buyerD['buyer_trn'] ?? ''); ?></td></tr>
							<tr><td>Address</td><td><?php echo epc_erp_h(($buyerD['buyer_address_line1'] ?? '') . ', ' . ($buyerD['buyer_city'] ?? '')); ?></td></tr>
							<tr><td>Electronic address</td><td><code><?php echo epc_erp_h($buyerD['buyer_peppol_endpoint'] ?? ''); ?></code></td></tr>
						</table>
					</div>
					<div class="col-sm-6">
						<h5><strong>PAYMENT</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Mode</td><td>Bank transfer (<?php echo epc_erp_h($doc['payment_means_code']); ?>)</td></tr>
							<tr><td>Bank account</td><td><?php echo epc_erp_h($doc['bank_account'] ?: '—'); ?></td></tr>
							<tr><td>Terms</td><td><?php echo epc_erp_h($doc['payment_terms'] ?: '—'); ?></td></tr>
							<tr><td>Order</td><td><a href="/<?php echo epc_erp_h($GLOBALS['DP_Config']->backend_dir); ?>/shop/orders/order?id=<?php echo (int)$doc['order_id']; ?>">#<?php echo (int)$doc['order_id']; ?></a></td></tr>
						</table>
					</div>
				</div>

				<table class="table table-bordered table-condensed" style="font-size:12px;margin-top:16px;">
					<thead><tr>
						<th>No.</th><th>Item</th><th>Description</th><th>Qty</th><th>UoM</th><th>Unit AED</th>
						<th>Subtotal</th><th>Tax type</th><th>Rate</th><th>VAT AED</th><th>Gross AED</th>
					</tr></thead>
					<tbody>
					<?php foreach ($doc['lines'] as $ln): ?>
						<tr>
							<td><?php echo (int)$ln['line_no']; ?></td>
							<td><?php echo epc_erp_h($ln['item_name']); ?></td>
							<td><?php echo epc_erp_h($ln['item_description']); ?></td>
							<td><?php echo epc_erp_h(number_format((float)$ln['quantity'], 0)); ?></td>
							<td><?php echo epc_erp_h($ln['uom_code']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($ln['unit_price']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($ln['line_net']); ?></td>
							<td><?php echo epc_erp_h($taxCats[$ln['tax_category']]['label'] ?? $ln['tax_category']); ?></td>
							<td><?php echo epc_erp_h(number_format((float)$ln['tax_rate'], 2)); ?>%</td>
							<td class="text-right"><?php echo epc_erp_money($ln['vat_line_aed']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($ln['gross_amount']); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>

				<div class="row">
					<div class="col-sm-6">
						<h5>VAT breakdown</h5>
						<table class="table table-condensed table-bordered">
							<thead><tr><th>Tax type</th><th>Taxable</th><th>Rate</th><th>VAT</th></tr></thead>
							<tbody>
							<?php foreach ($doc['tax_breakdown'] as $tb): ?>
								<tr>
									<td><?php echo epc_erp_h($tb['label'] ?? $tb['tax_category']); ?></td>
									<td class="text-right"><?php echo epc_erp_money($tb['taxable_amount']); ?></td>
									<td><?php echo epc_erp_h(number_format((float)$tb['tax_rate'], 2)); ?>%</td>
									<td class="text-right"><?php echo epc_erp_money($tb['tax_amount']); ?></td>
								</tr>
							<?php endforeach; ?>
							</tbody>
						</table>
					</div>
					<div class="col-sm-6">
						<table class="table" style="font-size:14px;">
							<tr><td>Total net amount</td><td class="text-right"><strong><?php echo epc_erp_money($doc['subtotal_ex_vat']); ?></strong></td></tr>
							<tr><td>Total excluding VAT</td><td class="text-right"><?php echo epc_erp_money($doc['subtotal_ex_vat']); ?></td></tr>
							<tr><td>Total VAT amount</td><td class="text-right"><?php echo epc_erp_money($doc['total_vat']); ?></td></tr>
							<tr><td>Total including VAT</td><td class="text-right"><strong><?php echo epc_erp_money($doc['total_incl_vat']); ?></strong></td></tr>
							<tr><td>(Less) Paid amount</td><td class="text-right"><?php echo epc_erp_money($doc['paid_amount']); ?></td></tr>
							<tr class="active"><td><strong>Total payable</strong></td><td class="text-right"><strong><?php echo epc_erp_money($doc['amount_due']); ?> AED</strong></td></tr>
						</table>
					</div>
				</div>

				<?php if (!empty($doc['events'])): ?>
					<h5><i class="fa fa-history"></i> Transmission log</h5>
					<table class="table table-condensed table-striped">
						<thead><tr><th>Time</th><th>Event</th><th>Status</th><th>Message</th></tr></thead>
						<tbody>
						<?php foreach ($doc['events'] as $ev): ?>
							<tr>
								<td><?php echo epc_erp_h(date('Y-m-d H:i', (int)$ev['time_created'])); ?></td>
								<td><?php echo epc_erp_h($ev['event_type']); ?></td>
								<td><?php echo epc_erp_h($ev['status']); ?></td>
								<td><?php echo epc_erp_h($ev['message']); ?></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
				<?php endif; ?>
			</div>
		<?php endif; else: ?>
			<h4><i class="fa fa-list"></i> Electronic invoices</h4>
			<?php $docs = epc_einvoice_list_documents($db_link, $date_from, $date_to, 150); ?>
			<table class="table table-striped table-bordered table-condensed">
				<thead><tr>
					<th>Invoice</th><th>Date</th><th>Order</th><th>Customer</th><th>Ex VAT</th><th>VAT</th><th>Incl VAT</th><th>Due</th><th>Status</th><th></th>
				</tr></thead>
				<tbody>
				<?php foreach ($docs as $d): ?>
					<tr>
						<td><strong><?php echo epc_erp_h($d['invoice_number']); ?></strong><br><small class="text-muted"><code><?php echo epc_erp_h(substr($d['uuid'], 0, 8)); ?>…</code></small></td>
						<td><?php echo epc_erp_h(date('Y-m-d', (int)$d['issue_date'])); ?></td>
						<td><?php echo (int)$d['order_id'] ? ('#' . (int)$d['order_id']) : '—'; ?></td>
						<td><?php echo (int)$d['user_id'] ? ('ID ' . (int)$d['user_id']) : 'Guest'; ?></td>
						<td><?php echo epc_erp_money($d['subtotal_ex_vat']); ?></td>
						<td><?php echo epc_erp_money($d['total_vat']); ?></td>
						<td><?php echo epc_erp_money($d['total_incl_vat']); ?></td>
						<td><?php echo epc_erp_money($d['amount_due']); ?></td>
						<td><span class="label label-default"><?php echo epc_erp_h($d['status']); ?></span></td>
						<td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'view', 'einv_doc=' . (int)$d['id'])); ?>">View</a></td>
					</tr>
				<?php endforeach; ?>
				<?php if (!$docs): ?><tr><td colspan="10" class="text-muted text-center">No e-invoices in this period. Generate from a completed order.</td></tr><?php endif; ?>
				</tbody>
			</table>
		<?php endif; ?>

	<?php elseif ($einvSection === 'create'): ?>
		<h4><i class="fa fa-plus-circle"></i> Generate electronic Tax Invoice from order</h4>
		<p class="text-muted">Builds PINT-AE XML with all mandatory fields. Buyer Peppol ID defaults to <code>0235:9900000098</code> if customer not onboarded.</p>
		<form id="epc_einv_form_create" class="form-horizontal well">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-group">
				<label class="col-sm-3 control-label">Order ID</label>
				<div class="col-sm-4">
					<input type="number" name="order_id" class="form-control" value="<?php echo $viewOrderId > 0 ? (int)$viewOrderId : ''; ?>" required placeholder="e.g. 1234">
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Transaction scenarios</label>
				<div class="col-sm-8">
					<?php foreach ($flags as $key => $label): ?>
						<label class="checkbox-inline"><input type="checkbox" name="flag_<?php echo epc_erp_h($key); ?>" value="1"> <?php echo epc_erp_h($label); ?></label><br>
					<?php endforeach; ?>
					<p class="help-block">8-digit transaction type code built from checked flags (MoF mandatory field #5).</p>
				</div>
			</div>
			<div class="form-group">
				<div class="col-sm-offset-3 col-sm-8">
					<button type="submit" class="btn btn-primary"><i class="fa fa-magic"></i> Generate &amp; validate</button>
				</div>
			</div>
		</form>

	<?php elseif ($einvSection === 'seller'): ?>
		<h4><i class="fa fa-building"></i> Seller profile (mandatory fields 10–20)</h4>
		<form id="epc_einv_form_seller" class="form-horizontal">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<?php
			$sfields = array(
				'seller_name' => 'Legal name',
				'seller_trn' => 'TRN (15 digits)',
				'seller_tin' => 'TIN (10 digits — auto from TRN if blank)',
				'seller_legal_reg_no' => 'Trade license / legal registration no.',
				'seller_legal_reg_type' => 'Registration type (TL/EID/PAS/CD)',
				'seller_authority_name' => 'Authority name (e.g. Dubai Economy and Tourism)',
				'seller_address_line1' => 'Address line 1',
				'seller_city' => 'City',
				'seller_emirate' => 'Emirate / subdivision',
				'seller_country_code' => 'Country code',
				'seller_phone' => 'Phone',
				'seller_email' => 'Email',
				'seller_bank_account' => 'Bank account (payment)',
				'payment_terms' => 'Payment terms',
			);
			foreach ($sfields as $k => $lbl):
				$val = epc_einvoice_get_setting($db_link, $k, $seller[$k] ?? '');
			?>
			<div class="form-group">
				<label class="col-sm-3 control-label"><?php echo epc_erp_h($lbl); ?></label>
				<div class="col-sm-6"><input type="text" name="<?php echo epc_erp_h($k); ?>" class="form-control" value="<?php echo epc_erp_h($val); ?>"></div>
			</div>
			<?php endforeach; ?>
			<div class="form-group">
				<label class="col-sm-3 control-label">VAT registered (FTA)</label>
				<div class="col-sm-6">
					<?php
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';
					$coProf = epc_uae_company_profile($db_link);
					$vatRegOn = !empty($coProf['vat_registered']);
					?>
					<label class="checkbox-inline">
						<input type="checkbox" name="company_vat_registered" value="1" <?php echo $vatRegOn ? 'checked' : ''; ?> />
						Company is VAT-registered in UAE (required for output / input VAT)
					</label>
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Peppol endpoint</label>
				<div class="col-sm-6">
					<p class="form-control-static"><code><?php echo epc_erp_h($seller['seller_peppol_endpoint'] ?: '0235:' . epc_einvoice_tin_from_trn($seller['seller_trn'])); ?></code></p>
					<p class="help-block">Auto: 0235 + TIN (first 10 digits of TRN). Country must stay <strong>AE</strong> and TRN 15 digits for FTA tax invoices.</p>
				</div>
			</div>
			<div class="form-group"><div class="col-sm-offset-3 col-sm-6"><button type="submit" class="btn btn-primary">Save seller profile</button></div></div>
		</form>

	<?php elseif ($einvSection === 'buyers'): ?>
		<h4><i class="fa fa-users"></i> Buyer Peppol / TRN profiles</h4>
		<p class="text-muted">B2B buyers need TRN and Peppol endpoint (<code>0235:TIN</code>). If not onboarded, system uses <code><?php echo epc_erp_h($const['endpoint_not_onboarded']); ?></code>.</p>
		<?php
		$buyerEdit = $viewUserId > 0 ? epc_einvoice_buyer_profile($db_link, $viewUserId) : null;
		$buyerList = $db_link->query(
			'SELECT b.*, u.`email` FROM `epc_einvoice_buyer_profiles` b
			LEFT JOIN `users` u ON u.`user_id` = b.`user_id`
			ORDER BY b.`time_updated` DESC LIMIT 100'
		)->fetchAll(PDO::FETCH_ASSOC);
		?>
		<form id="epc_einv_form_buyer" class="form-horizontal well">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-group">
				<label class="col-sm-2 control-label">Customer user ID</label>
				<div class="col-sm-3"><input type="number" name="user_id" class="form-control" value="<?php echo $buyerEdit ? (int)$buyerEdit['user_id'] : ''; ?>" required></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Buyer name</label>
				<div class="col-sm-4"><input type="text" name="buyer_name" class="form-control" value="<?php echo epc_erp_h($buyerEdit['buyer_name'] ?? ''); ?>"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">TRN</label>
				<div class="col-sm-4"><input type="text" name="trn" class="form-control" value="<?php echo epc_erp_h($buyerEdit['trn'] ?? ''); ?>" placeholder="15-digit TRN"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Legal reg. / type</label>
				<div class="col-sm-3"><input type="text" name="legal_reg_no" class="form-control" value="<?php echo epc_erp_h($buyerEdit['legal_reg_no'] ?? ''); ?>"></div>
				<div class="col-sm-2"><select name="legal_reg_type" class="form-control">
					<?php foreach (array('TL', 'EID', 'PAS', 'CD') as $t): ?>
						<option value="<?php echo $t; ?>" <?php echo ($buyerEdit['legal_reg_type'] ?? 'TL') === $t ? 'selected' : ''; ?>><?php echo $t; ?></option>
					<?php endforeach; ?>
				</select></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Address / city</label>
				<div class="col-sm-4"><input type="text" name="address_line1" class="form-control" value="<?php echo epc_erp_h($buyerEdit['address_line1'] ?? ''); ?>"></div>
				<div class="col-sm-2"><input type="text" name="city" class="form-control" value="<?php echo epc_erp_h($buyerEdit['city'] ?? 'Dubai'); ?>"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Peppol endpoint</label>
				<div class="col-sm-4"><input type="text" name="peppol_endpoint" class="form-control" value="<?php echo epc_erp_h($buyerEdit['peppol_endpoint'] ?? ''); ?>" placeholder="0235:1245780912"></div>
				<div class="col-sm-3"><label class="checkbox-inline"><input type="checkbox" name="buyer_onboarded" value="1" <?php echo !empty($buyerEdit['buyer_onboarded']) ? 'checked' : ''; ?>> Onboarded on Peppol</label></div>
			</div>
			<div class="form-group"><div class="col-sm-offset-2"><button type="submit" class="btn btn-primary">Save buyer profile</button></div></div>
		</form>
		<table class="table table-condensed table-bordered">
			<thead><tr><th>User</th><th>Name</th><th>TRN</th><th>Peppol</th><th>Onboarded</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($buyerList as $b): ?>
				<tr>
					<td><?php echo (int)$b['user_id']; ?></td>
					<td><?php echo epc_erp_h($b['buyer_name']); ?></td>
					<td><?php echo epc_erp_h($b['trn'] ?: '—'); ?></td>
					<td><code><?php echo epc_erp_h($b['peppol_endpoint'] ?: '—'); ?></code></td>
					<td><?php echo (int)$b['buyer_onboarded'] ? 'Yes' : 'No'; ?></td>
					<td><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'buyers', 'einv_user=' . (int)$b['user_id'])); ?>">Edit</a></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>

	<?php elseif ($einvSection === 'asp'): ?>
		<h4><i class="fa fa-cloud-upload"></i> Accredited Service Provider (ASP) &amp; FTA reporting</h4>
		<div class="alert alert-warning">
			<strong>5-corner model:</strong> Your ASP validates XML, transmits to buyer's ASP (Corner 3), and reports Tax Data to FTA (Corner 5) in parallel.
			Compliance obligation remains with you as supplier — select one ASP for send + receive via <a href="https://tax.gov.ae" target="_blank" rel="noopener">EmaraTax</a>.
		</div>
		<form id="epc_einv_form_asp" class="form-horizontal well">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-group">
				<label class="col-sm-3 control-label">ASP name</label>
				<div class="col-sm-5"><input type="text" name="asp_name" class="form-control" value="<?php echo epc_erp_h(epc_einvoice_get_setting($db_link, 'asp_name', '')); ?>" placeholder="Your accredited ASP"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Transmission mode</label>
				<div class="col-sm-4">
					<select name="asp_api_mode" class="form-control">
						<option value="manual" <?php echo epc_einvoice_get_setting($db_link, 'asp_api_mode') === 'manual' ? 'selected' : ''; ?>>Manual — download XML, upload via ASP portal</option>
						<option value="api" <?php echo epc_einvoice_get_setting($db_link, 'asp_api_mode') === 'api' ? 'selected' : ''; ?>>API — automated queue (configure URL)</option>
					</select>
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">ASP API URL</label>
				<div class="col-sm-6"><input type="url" name="asp_api_url" class="form-control" value="<?php echo epc_erp_h(epc_einvoice_get_setting($db_link, 'asp_api_url', '')); ?>" placeholder="https://asp.example/api/invoices"></div>
			</div>
			<div class="form-group"><div class="col-sm-offset-3"><button type="submit" class="btn btn-primary">Save ASP settings</button></div></div>
		</form>
		<h5>Predefined Peppol endpoints (MoF scenarios)</h5>
		<table class="table table-condensed table-bordered">
			<tr><td>Buyer not onboarded</td><td><code><?php echo epc_erp_h($const['endpoint_not_onboarded']); ?></code></td></tr>
			<tr><td>Deemed supply</td><td><code><?php echo epc_erp_h($const['endpoint_deemed_supply']); ?></code></td></tr>
			<tr><td>Exports (buyer no Peppol ID)</td><td><code><?php echo epc_erp_h($const['endpoint_exports']); ?></code></td></tr>
		</table>

	<?php elseif ($einvSection === 'guide'): ?>
		<h4><i class="fa fa-book"></i> UAE e-Invoicing guide (ERP implementation)</h4>
		<div class="row">
			<div class="col-md-6">
				<h5>Implementation timeline (MD 244/2025)</h5>
				<table class="table table-bordered table-condensed">
					<tr><th>Phase</th><th>Date</th></tr>
					<tr><td>Pilot programme</td><td>From 1 Jul 2026 (by invitation)</td></tr>
					<tr><td>Voluntary e-invoicing</td><td>From 1 Jul 2026 (all businesses)</td></tr>
					<tr><td>Mandatory — revenue ≥ AED 50M</td><td>ASP by 31 Jul 2026 · live 1 Jan 2027</td></tr>
					<tr><td>Mandatory — revenue &lt; AED 50M</td><td>ASP by 31 Mar 2027 · live 1 Jul 2027</td></tr>
					<tr><td>Government entities</td><td>Live 1 Oct 2027</td></tr>
				</table>
				<h5>Document categories</h5>
				<ul>
					<li><strong>Electronic Tax Invoice</strong> (type 380) — taxable supplies, TRN required</li>
					<li><strong>Electronic Tax Credit Note</strong> (381) — output tax reductions</li>
					<li><strong>Commercial Invoice</strong> — non-taxable / non-VAT registered supplies</li>
				</ul>
			</div>
			<div class="col-md-6">
				<h5>Getting ready (4 steps)</h5>
				<ol>
					<li><strong>Understand requirements</strong> — this ERP panel + MoF guidelines</li>
					<li><strong>Select ASP</strong> — contract + onboard via EmaraTax → obtain Peppol ID</li>
					<li><strong>Test</strong> — generate XML here, transmit via ASP, confirm FTA reporting</li>
					<li><strong>Go live</strong> — submit every B2B / B2G invoice through ASP</li>
				</ol>
				<h5>Tax categories (field #37)</h5>
				<table class="table table-condensed table-bordered">
					<?php foreach ($taxCats as $code => $tc): ?>
						<tr><td><code><?php echo epc_erp_h($code); ?></code></td><td><?php echo epc_erp_h($tc['label']); ?></td></tr>
					<?php endforeach; ?>
				</table>
				<h5>Data retention</h5>
				<p class="text-muted">5 years (7 for real estate). Must be retrievable for FTA on request. XML + transmission log stored in ERP.</p>
				<p><a href="https://www.mof.gov.ae/eInvoicing" target="_blank" rel="noopener">mof.gov.ae/eInvoicing</a> · Peppol PINT-AE specifications</p>
			</div>
		</div>
		<h5>Mandatory fields checklist (51 fields — Tax Invoice)</h5>
		<p class="text-muted">ERP validates all fields before ASP submission. See MoF mandatory fields document V1.0 (23 Feb 2026).</p>
		<div class="row">
			<?php
			$mandatory = epc_einvoice_mandatory_field_map();
			$chunks = array_chunk($mandatory, (int)ceil(count($mandatory) / 2), true);
			foreach ($chunks as $chunk):
			?>
			<div class="col-md-6">
				<ul style="font-size:13px;">
					<?php foreach ($chunk as $k => $lbl): ?><li><?php echo epc_erp_h($lbl); ?></li><?php endforeach; ?>
				</ul>
			</div>
			<?php endforeach; ?>
		</div>
	<?php endif; ?>
</div>

<script>
(function(){
	var erpPostUrl = <?php echo json_encode($einvAjaxUrl); ?>;
	var erpAjaxUrl = erpPostUrl;
	function parseJsonResponse(r) {
		return r.text().then(function(t) {
			var trimmed = (t || '').trim();
			if (trimmed.charAt(0) === '<') {
				throw new Error('Server returned HTML instead of JSON — hard-refresh the page (Ctrl+F5) and try again.');
			}
			try { return JSON.parse(trimmed); }
			catch (e) {
				throw new Error('Server returned invalid JSON (HTTP ' + r.status + '). ' + trimmed.substring(0, 120));
			}
		});
	}
	function post(action, data, cb) {
		var fd = new FormData();
		fd.append('action', action);
		for (var k in data) { if (data.hasOwnProperty(k)) fd.append(k, data[k]); }
		fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(cb).catch(function(e){ alert(e.message || 'Request failed'); });
	}
	function bindForm(id, action, extra) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(ev) {
			ev.preventDefault();
			var data = extra ? extra(f) : {};
			new FormData(f).forEach(function(v,k){ data[k]=v; });
			post(action, data, function(res) {
				alert(res.message || (res.status ? 'OK' : 'Error'));
				if (res.status && res.redirect) location.href = res.redirect;
				else if (res.status) location.reload();
			});
		});
	}
	bindForm('epc_einv_form_create', 'einvoice_create', function(f) {
		var flags = {};
		f.querySelectorAll('input[type=checkbox]').forEach(function(cb) {
			if (cb.name.indexOf('flag_') === 0 && cb.checked) flags[cb.name.replace('flag_','')] = '1';
		});
		return { transaction_flags: JSON.stringify(flags) };
	});
	bindForm('epc_einv_form_seller', 'einvoice_save_seller');
	bindForm('epc_einv_form_buyer', 'einvoice_save_buyer');
	bindForm('epc_einv_form_asp', 'einvoice_save_asp');
	window.epcEinvSubmit = function(id) {
		if (!confirm('Submit this e-invoice to your ASP for exchange and FTA reporting?')) return;
		post('einvoice_submit', { document_id: id, csrf_guard_key: <?php echo json_encode($csrf); ?> }, function(res) {
			alert(res.message || '');
			if (res.status) location.reload();
		});
	};
	window.epcEinvDownloadXml = function(id) {
		window.open(erpAjaxUrl + '?action=einvoice_download_xml&document_id=' + id + '&csrf_guard_key=' + encodeURIComponent(<?php echo json_encode($csrf); ?>), '_blank');
	};
})();
</script>
