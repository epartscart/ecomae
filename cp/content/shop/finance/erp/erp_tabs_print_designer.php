<?php
/**
 * ERP tab — Print Designer (tenant-configurable document print templates).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_print_designer.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

try {
	epc_erp_print_designer_ensure_schema($db_link);
	epc_erp_print_designer_seed_defaults($db_link);
} catch (Throwable $e) {
	// Schema creation may fail on first run — continue with empty list
}

$pdAction = isset($_GET['pd_action']) ? (string)$_GET['pd_action'] : 'list';
$pdId = isset($_GET['pd_id']) ? (int)$_GET['pd_id'] : 0;
$pdDocType = isset($_GET['pd_doctype']) ? (string)$_GET['pd_doctype'] : '';
$csrfLocal = isset($csrf) ? $csrf : '';
$pdBase = epc_erp_tab_url($erpUrl, 'print_designer', $date_from_str, $date_to_str, 'setup');
$docTypes = epc_erp_print_doc_types();
$mergeFields = epc_erp_print_merge_fields();

erp_page_header(
	'<i class="fa fa-paint-brush"></i> Print designer',
	'Customise voucher, invoice, PO, and report print layouts — logo, columns, colours, terms, signatures.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'System administration'),
		array('label' => 'Print designer'),
	),
	array(
		array('label' => 'New template', 'url' => $pdBase . '&pd_action=edit', 'class' => 'btn-primary', 'icon' => 'fa-plus'),
	)
);

if ($pdAction === 'edit'):
	$tpl = $pdId > 0 ? epc_erp_print_template_get($db_link, $pdId) : null;
?>
<div class="ef-window">
	<div class="ef-title"><i class="fa fa-paint-brush"></i> <?php echo $tpl ? 'Edit' : 'New'; ?> Print Template</div>
	<div class="ef-body">
		<form method="post" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" class="epc-erp-form">
			<input type="hidden" name="action" value="print_designer_save">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="id" value="<?php echo (int)($tpl['id'] ?? 0); ?>">

			<div class="row">
				<div class="col-sm-6">
					<div class="ef-section">
						<span class="ef-section-title"><i class="fa fa-cog"></i> General</span>
						<table class="ef-grid">
							<tr><td style="width:140px"><label>Document type</label></td><td>
								<select name="doc_type" class="form-control input-sm" required>
									<?php foreach ($docTypes as $k => $v): ?>
									<option value="<?php echo epc_erp_h($k); ?>"<?php echo ($tpl['doc_type'] ?? '') === $k ? ' selected' : ''; ?>><?php echo epc_erp_h($v); ?></option>
									<?php endforeach; ?>
								</select>
							</td></tr>
							<tr><td><label>Template name</label></td><td><input type="text" name="name" class="form-control input-sm" value="<?php echo epc_erp_h($tpl['name'] ?? ''); ?>" required></td></tr>
							<tr><td><label>Set as default</label></td><td><label><input type="checkbox" name="is_default" value="1"<?php echo !empty($tpl['is_default']) ? ' checked' : ''; ?>> Default for this doc type</label></td></tr>
						</table>
					</div>
					<div class="ef-section" style="margin-top:8px">
						<span class="ef-section-title"><i class="fa fa-file-o"></i> Page setup</span>
						<table class="ef-grid">
							<tr><td style="width:140px"><label>Page size</label></td><td>
								<select name="page_size" class="form-control input-sm">
									<?php foreach (array('A4','A5','Letter','Legal') as $ps): ?>
									<option<?php echo ($tpl['page_size'] ?? 'A4') === $ps ? ' selected' : ''; ?>><?php echo $ps; ?></option>
									<?php endforeach; ?>
								</select>
							</td></tr>
							<tr><td><label>Orientation</label></td><td>
								<select name="orientation" class="form-control input-sm">
									<option value="portrait"<?php echo ($tpl['orientation'] ?? 'portrait') === 'portrait' ? ' selected' : ''; ?>>Portrait</option>
									<option value="landscape"<?php echo ($tpl['orientation'] ?? '') === 'landscape' ? ' selected' : ''; ?>>Landscape</option>
								</select>
							</td></tr>
							<tr><td><label>Margins (mm)</label></td><td>
								T:<input type="number" name="margin_top" class="form-control input-sm" style="width:50px;display:inline" value="<?php echo (int)($tpl['margin_top'] ?? 15); ?>">
								B:<input type="number" name="margin_bottom" class="form-control input-sm" style="width:50px;display:inline" value="<?php echo (int)($tpl['margin_bottom'] ?? 15); ?>">
								L:<input type="number" name="margin_left" class="form-control input-sm" style="width:50px;display:inline" value="<?php echo (int)($tpl['margin_left'] ?? 10); ?>">
								R:<input type="number" name="margin_right" class="form-control input-sm" style="width:50px;display:inline" value="<?php echo (int)($tpl['margin_right'] ?? 10); ?>">
							</td></tr>
						</table>
					</div>
				</div>
				<div class="col-sm-6">
					<div class="ef-section">
						<span class="ef-section-title"><i class="fa fa-font"></i> Typography & colours</span>
						<table class="ef-grid">
							<tr><td style="width:140px"><label>Font</label></td><td><input type="text" name="font_family" class="form-control input-sm" value="<?php echo epc_erp_h($tpl['font_family'] ?? 'Arial, sans-serif'); ?>"></td></tr>
							<tr><td><label>Font size (pt)</label></td><td><input type="number" name="font_size" class="form-control input-sm" style="width:70px" value="<?php echo (int)($tpl['font_size'] ?? 11); ?>"></td></tr>
							<tr><td><label>Primary colour</label></td><td><input type="color" name="primary_color" value="<?php echo epc_erp_h($tpl['primary_color'] ?? '#1565c0'); ?>" style="width:50px;height:28px;border:1px solid #ccc;"> <small class="text-muted">Headers, totals</small></td></tr>
							<tr><td><label>Secondary colour</label></td><td><input type="color" name="secondary_color" value="<?php echo epc_erp_h($tpl['secondary_color'] ?? '#4a6a7a'); ?>" style="width:50px;height:28px;border:1px solid #ccc;"> <small class="text-muted">Labels, muted text</small></td></tr>
						</table>
					</div>
					<div class="ef-section" style="margin-top:8px">
						<span class="ef-section-title"><i class="fa fa-picture-o"></i> Logo</span>
						<table class="ef-grid">
							<tr><td style="width:140px"><label>Position</label></td><td>
								<select name="logo_position" class="form-control input-sm">
									<?php foreach (array('left'=>'Left','center'=>'Center','right'=>'Right','none'=>'No logo') as $lk=>$lv): ?>
									<option value="<?php echo $lk; ?>"<?php echo ($tpl['logo_position'] ?? 'left') === $lk ? ' selected' : ''; ?>><?php echo $lv; ?></option>
									<?php endforeach; ?>
								</select>
							</td></tr>
							<tr><td><label>Max height (px)</label></td><td><input type="number" name="logo_max_height" class="form-control input-sm" style="width:70px" value="<?php echo (int)($tpl['logo_max_height'] ?? 60); ?>"></td></tr>
						</table>
					</div>
				</div>
			</div>

			<div class="ef-section" style="margin-top:8px">
				<span class="ef-section-title"><i class="fa fa-code"></i> Header HTML</span>
				<textarea name="header_html" class="form-control" rows="4" placeholder="Header template with merge fields..."><?php echo epc_erp_h($tpl['header_html'] ?? ''); ?></textarea>
				<small class="text-muted">Merge fields: <?php echo implode(', ', array_keys($mergeFields)); ?></small>
			</div>

			<div class="ef-section" style="margin-top:8px">
				<span class="ef-section-title"><i class="fa fa-columns"></i> Body columns (JSON)</span>
				<textarea name="body_columns" class="form-control" rows="4" placeholder='[{"key":"line_no","label":"#","width":"5%","align":"center"}]'><?php echo epc_erp_h($tpl['body_columns'] ?? ''); ?></textarea>
				<small class="text-muted">JSON array. Each column: {"key": "field_name", "label": "Header", "width": "15%", "align": "left|center|right"}</small>
			</div>

			<div class="row">
				<div class="col-sm-6">
					<div class="ef-section" style="margin-top:8px">
						<span class="ef-section-title"><i class="fa fa-file-text-o"></i> Footer HTML</span>
						<textarea name="footer_html" class="form-control" rows="3"><?php echo epc_erp_h($tpl['footer_html'] ?? ''); ?></textarea>
					</div>
				</div>
				<div class="col-sm-6">
					<div class="ef-section" style="margin-top:8px">
						<span class="ef-section-title"><i class="fa fa-css3"></i> Custom CSS</span>
						<textarea name="custom_css" class="form-control" rows="3" placeholder="Additional CSS overrides..."><?php echo epc_erp_h($tpl['custom_css'] ?? ''); ?></textarea>
					</div>
				</div>
			</div>

			<div class="row">
				<div class="col-sm-6">
					<div class="ef-section" style="margin-top:8px">
						<span class="ef-section-title"><i class="fa fa-legal"></i> Terms & conditions</span>
						<label><input type="checkbox" name="show_terms" value="1"<?php echo !empty($tpl['show_terms']) || $tpl === null ? ' checked' : ''; ?>> Show terms</label>
						<textarea name="terms_html" class="form-control" rows="3" placeholder="Terms and conditions text..."><?php echo epc_erp_h($tpl['terms_html'] ?? ''); ?></textarea>
					</div>
				</div>
				<div class="col-sm-6">
					<div class="ef-section" style="margin-top:8px">
						<span class="ef-section-title"><i class="fa fa-university"></i> Bank details</span>
						<label><input type="checkbox" name="show_bank_details" value="1"<?php echo !empty($tpl['show_bank_details']) || $tpl === null ? ' checked' : ''; ?>> Show bank details</label>
						<textarea name="bank_details_html" class="form-control" rows="3" placeholder="Bank name, IBAN, SWIFT..."><?php echo epc_erp_h($tpl['bank_details_html'] ?? ''); ?></textarea>
					</div>
				</div>
			</div>

			<div class="ef-section" style="margin-top:8px">
				<span class="ef-section-title"><i class="fa fa-pencil-square-o"></i> Signatures</span>
				<label><input type="checkbox" name="show_signature_line" value="1"<?php echo !empty($tpl['show_signature_line']) || $tpl === null ? ' checked' : ''; ?>> Show signature lines</label>
				<input type="text" name="signature_labels" class="form-control input-sm" value="<?php echo epc_erp_h($tpl['signature_labels'] ?? 'Prepared by,Approved by,Received by'); ?>" placeholder="Comma-separated labels">
				<small class="text-muted">Comma-separated labels for signature boxes, e.g. "Prepared by, Approved by, Received by"</small>
			</div>

			<div class="ef-section" style="margin-top:8px">
				<span class="ef-section-title"><i class="fa fa-qrcode"></i> Extras</span>
				<label><input type="checkbox" name="show_qr_code" value="1"<?php echo !empty($tpl['show_qr_code']) ? ' checked' : ''; ?>> Show QR code</label>
				&nbsp;&nbsp;
				<label><input type="checkbox" name="show_barcode" value="1"<?php echo !empty($tpl['show_barcode']) ? ' checked' : ''; ?>> Show barcode</label>
			</div>

			<div class="ef-actions" style="margin-top:12px">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save template</button>
				<a href="<?php echo epc_erp_h($pdBase); ?>" class="btn btn-default btn-sm">Cancel</a>
			</div>
		</form>
	</div>
</div>

<?php else: // list view
	$templates = array();
	try {
		$templates = epc_erp_print_templates_list($db_link, $pdDocType);
	} catch (Throwable $e) {
		// Table may not exist yet
	}
	$grouped = array();
	foreach ($templates as $t) {
		$grouped[$t['doc_type']][] = $t;
	}
?>
<div class="ef-window">
	<div class="ef-title"><i class="fa fa-paint-brush"></i> Print Templates</div>
	<div class="ef-toolbar">
		<a href="<?php echo epc_erp_h($pdBase . '&pd_action=edit'); ?>" class="btn btn-primary btn-xs"><i class="fa fa-plus"></i> New template</a>
		<select onchange="if(this.value)location='<?php echo epc_erp_h($pdBase); ?>&pd_doctype='+this.value;else location='<?php echo epc_erp_h($pdBase); ?>';" class="form-control input-sm" style="width:auto;display:inline-block">
			<option value="">All doc types</option>
			<?php foreach ($docTypes as $k => $v): ?>
			<option value="<?php echo epc_erp_h($k); ?>"<?php echo $pdDocType === $k ? ' selected' : ''; ?>><?php echo epc_erp_h($v); ?></option>
			<?php endforeach; ?>
		</select>
	</div>
	<div class="ef-body">
		<?php if (empty($templates)): ?>
			<div style="padding:20px;text-align:center;color:#999;"><i class="fa fa-paint-brush fa-3x"></i><br>No templates found. <a href="<?php echo epc_erp_h($pdBase . '&pd_action=edit'); ?>">Create one</a>.</div>
		<?php else: ?>
			<?php foreach ($grouped as $dtype => $tpls): ?>
			<div class="ef-section" style="margin-bottom:10px">
				<span class="ef-section-title"><i class="fa fa-file-text-o"></i> <?php echo epc_erp_h($docTypes[$dtype] ?? $dtype); ?></span>
				<table class="ef-grid">
					<thead><tr><th>Name</th><th>Page</th><th>Default</th><th>Last updated</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($tpls as $t): ?>
						<tr>
							<td><strong><?php echo epc_erp_h($t['name']); ?></strong></td>
							<td><?php echo epc_erp_h($t['page_size'] . ' / ' . $t['orientation']); ?></td>
							<td><?php echo $t['is_default'] ? '<span class="label label-success">Default</span>' : ''; ?></td>
							<td><small><?php echo $t['time_updated'] > 0 ? date('d M Y', (int)$t['time_updated']) : '—'; ?></small></td>
							<td>
								<a href="<?php echo epc_erp_h($pdBase . '&pd_action=edit&pd_id=' . (int)$t['id']); ?>" class="btn btn-default btn-xs"><i class="fa fa-pencil"></i></a>
							</td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
			<?php endforeach; ?>
		<?php endif; ?>
	</div>
	<div class="ef-status">
		<span>Templates: <?php echo count($templates); ?></span>
		<span>Types: <?php echo count($grouped); ?></span>
	</div>
</div>
<?php endif; ?>
