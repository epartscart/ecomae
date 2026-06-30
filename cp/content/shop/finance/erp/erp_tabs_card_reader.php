<?php
/**
 * ID Card / Photo Reader — capture customer official ID during invoicing for AML compliance.
 * Reads Emirates ID, Passport, National ID via camera or file upload.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-id-card"></i> Card / ID Reader',
	'Capture customer identification documents for AML compliance — Emirates ID, passport, or national ID card reader/photo upload.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Card Reader'),
	),
	array()
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-camera"></i> Capture customer ID</h4>
	<p class="text-muted">For AML compliance, capture the customer's official ID when the transaction exceeds the reporting threshold. Supports camera capture or file upload.</p>
	<div class="row">
		<div class="col-md-6">
			<div class="panel panel-default">
				<div class="panel-heading"><strong><i class="fa fa-camera"></i> Camera capture</strong></div>
				<div class="panel-body text-center" style="min-height:200px;background:#f8fafc;border:2px dashed #cbd5e1;border-radius:8px;">
					<i class="fa fa-id-card-o fa-3x text-muted" style="margin:40px 0 12px;"></i>
					<p class="text-muted">Position ID card in view</p>
					<button class="btn btn-primary btn-sm"><i class="fa fa-camera"></i> Start camera</button>
				</div>
			</div>
		</div>
		<div class="col-md-6">
			<div class="panel panel-default">
				<div class="panel-heading"><strong><i class="fa fa-upload"></i> File upload</strong></div>
				<div class="panel-body text-center" style="min-height:200px;background:#f8fafc;border:2px dashed #cbd5e1;border-radius:8px;">
					<i class="fa fa-cloud-upload fa-3x text-muted" style="margin:40px 0 12px;"></i>
					<p class="text-muted">Drop image or PDF here</p>
					<button class="btn btn-default btn-sm"><i class="fa fa-folder-open"></i> Browse files</button>
				</div>
			</div>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-list"></i> Recent ID captures</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Date</th><th>Customer</th><th>ID type</th><th>ID number</th><th>Transaction ref</th><th>Amount</th><th>Captured by</th><th></th></tr></thead>
		<tbody>
			<tr><td>2026-06-20</td><td>Ahmed Al Rashid</td><td><span class="label label-info">Emirates ID</span></td><td>784-****-*****-1</td><td>INV-2026-0045</td><td>52,000 AED</td><td>Sales-01</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-19</td><td>John Williams</td><td><span class="label label-primary">Passport</span></td><td>GB****567</td><td>INV-2026-0044</td><td>28,000 AED</td><td>Sales-02</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-18</td><td>Fatima Hassan</td><td><span class="label label-info">Emirates ID</span></td><td>784-****-*****-5</td><td>INV-2026-0043</td><td>15,500 AED</td><td>Sales-01</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> AML ID capture settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Threshold for ID capture</label><input type="number" class="form-control input-sm" value="15000" id="cr_threshold"></div>
		<div class="pm-field"><label>Accepted ID types</label>
			<select class="form-control input-sm" multiple size="4">
				<option selected>Emirates ID</option><option selected>Passport</option><option selected>National ID</option><option>Driving License</option>
			</select>
		</div>
		<div class="pm-field"><label>OCR extraction</label>
			<select class="form-control input-sm"><option value="1">Enabled — auto-extract name &amp; number</option><option value="0">Disabled — manual entry</option></select>
		</div>
		<div class="pm-field"><label>Retention period</label>
			<select class="form-control input-sm"><option>5 years (UAE AML)</option><option>7 years</option><option>10 years</option><option>Indefinite</option></select>
		</div>
	</div>
</div>
<?php
erp_section_card('Card / ID Reader', ob_get_clean(), array('icon' => 'fa-id-card'));
