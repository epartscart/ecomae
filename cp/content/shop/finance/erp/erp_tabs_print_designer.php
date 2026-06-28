<?php
/**
 * ERP tab — Print Designer (tenant-configurable document print templates).
 *
 * Allows tenants to customise print layouts for vouchers, invoices, POs,
 * delivery notes, receipt/payment vouchers, and reports.
 */
defined('_ASTEXE_') or die('No access');
ini_set('display_errors', '1');
error_reporting(E_ALL);
register_shutdown_function(function () {
	$e = error_get_last();
	if ($e !== null && in_array($e['type'], array(E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR))) {
		echo '<div style="padding:20px;background:#ffcdd2;border:2px solid red;margin:20px;font-family:monospace">';
		echo '<h4 style="color:red">FATAL ERROR in Print Designer</h4>';
		echo '<p><strong>Type:</strong> ' . (int)$e['type'] . '</p>';
		echo '<p><strong>Message:</strong> ' . htmlspecialchars((string)$e['message']) . '</p>';
		echo '<p><strong>File:</strong> ' . htmlspecialchars((string)$e['file']) . '</p>';
		echo '<p><strong>Line:</strong> ' . (int)$e['line'] . '</p>';
		echo '</div>';
	}
});
echo '<p style="color:green;font-size:10px">PD-CHECKPOINT-1</p>';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
echo '<p style="color:green;font-size:10px">PD-CHECKPOINT-2</p>';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
echo '<p style="color:green;font-size:10px">PD-CHECKPOINT-3</p>';

/* ── Load backend ── */
$_pdOk = false;
$_pdErr = '';
$_pdFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_print_designer.php';
if (is_file($_pdFile)) {
	try { require_once $_pdFile; $_pdOk = true; }
	catch (\Throwable $e) { $_pdErr = $e->getMessage(); }
}

/* ── Schema + seed ── */
if ($_pdOk && isset($db_link) && $db_link instanceof PDO) {
	try {
		if (function_exists('epc_erp_print_designer_ensure_schema')) { epc_erp_print_designer_ensure_schema($db_link); }
		if (function_exists('epc_erp_print_designer_seed_defaults'))  { epc_erp_print_designer_seed_defaults($db_link); }
	} catch (\Throwable $e) { /* schema/seed failed — continue */ }
}
echo '<p style="color:green;font-size:10px">PD-CHECKPOINT-4 (schema done)</p>';

/* ── Request params ── */
$pdAction  = isset($_GET['pd_action']) ? (string)$_GET['pd_action'] : 'list';
$pdId      = isset($_GET['pd_id'])     ? (int)$_GET['pd_id']       : 0;
$pdDocType = isset($_GET['pd_doctype'])? (string)$_GET['pd_doctype']: '';
$csrfLocal = isset($csrf) ? $csrf : '';
$pdBase    = epc_erp_tab_url($erpUrl, 'print_designer', $date_from_str, $date_to_str, 'setup');
$docTypes  = function_exists('epc_erp_print_doc_types')   ? epc_erp_print_doc_types()   : array();
$mergeFields = function_exists('epc_erp_print_merge_fields') ? epc_erp_print_merge_fields() : array();
echo '<p style="color:green;font-size:10px">PD-CHECKPOINT-5 (vars done)</p>';

/* ── Page header ── */
erp_page_header(
	'<i class="fa fa-paint-brush"></i> Print designer',
	'Customise voucher, invoice, PO, and report print layouts &mdash; logo, columns, colours, terms, signatures.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'System administration'),
		array('label' => 'Print designer'),
	),
	array(
		array('label' => 'New template', 'url' => $pdBase . '&pd_action=edit', 'class' => 'btn-primary', 'icon' => 'fa-plus'),
	)
);

echo '<p style="color:green;font-size:10px">PD-CHECKPOINT-6 (page header done)</p>';

/* ── Backend warning ── */
if ($_pdErr !== '') {
	echo '<div class="alert alert-warning" style="margin:10px"><i class="fa fa-exclamation-triangle"></i> Print designer backend: ' . epc_erp_h($_pdErr) . '</div>';
}

/* ============================================================
 *  EDIT VIEW
 * ============================================================ */
echo '<p style="color:green;font-size:10px">PD-CHECKPOINT-7 (entering view: ' . epc_erp_h($pdAction) . ')</p>';
if ($pdAction === 'edit') {
	$tpl = null;
	if ($pdId > 0 && $_pdOk && isset($db_link) && $db_link instanceof PDO && function_exists('epc_erp_print_template_get')) {
		try { $tpl = epc_erp_print_template_get($db_link, $pdId); } catch (\Throwable $e) { $tpl = null; }
	}

	$_f = function ($key, $default = '') use ($tpl) {
		return isset($tpl[$key]) ? $tpl[$key] : $default;
	};

	echo '<div class="ef-window">';
	echo '<div class="ef-title"><i class="fa fa-paint-brush"></i> ' . ($tpl ? 'Edit' : 'New') . ' Print Template</div>';
	echo '<div class="ef-body">';
	echo '<form method="post" action="' . epc_erp_h(isset($erpAjaxUrl) ? $erpAjaxUrl : '') . '" class="epc-erp-form">';
	echo '<input type="hidden" name="action" value="print_designer_save">';
	echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrfLocal) . '">';
	echo '<input type="hidden" name="id" value="' . (int)$_f('id', 0) . '">';

	// General + Page setup row
	echo '<div class="row"><div class="col-sm-6">';
	echo '<div class="ef-section"><span class="ef-section-title"><i class="fa fa-cog"></i> General</span>';
	echo '<table class="ef-grid">';
	echo '<tr><td style="width:140px"><label>Document type</label></td><td><select name="doc_type" class="form-control input-sm" required>';
	foreach ($docTypes as $k => $v) {
		$sel = ($_f('doc_type') === $k) ? ' selected' : '';
		echo '<option value="' . epc_erp_h($k) . '"' . $sel . '>' . epc_erp_h($v) . '</option>';
	}
	echo '</select></td></tr>';
	echo '<tr><td><label>Template name</label></td><td><input type="text" name="name" class="form-control input-sm" value="' . epc_erp_h($_f('name')) . '" required></td></tr>';
	echo '<tr><td><label>Set as default</label></td><td><label><input type="checkbox" name="is_default" value="1"' . (!empty($tpl['is_default']) ? ' checked' : '') . '> Default for this doc type</label></td></tr>';
	echo '</table></div>';

	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-file-o"></i> Page setup</span>';
	echo '<table class="ef-grid">';
	echo '<tr><td style="width:140px"><label>Page size</label></td><td><select name="page_size" class="form-control input-sm">';
	foreach (array('A4','A5','Letter','Legal') as $ps) {
		$sel = ($_f('page_size', 'A4') === $ps) ? ' selected' : '';
		echo '<option' . $sel . '>' . $ps . '</option>';
	}
	echo '</select></td></tr>';
	echo '<tr><td><label>Orientation</label></td><td><select name="orientation" class="form-control input-sm">';
	echo '<option value="portrait"' . ($_f('orientation', 'portrait') === 'portrait' ? ' selected' : '') . '>Portrait</option>';
	echo '<option value="landscape"' . ($_f('orientation') === 'landscape' ? ' selected' : '') . '>Landscape</option>';
	echo '</select></td></tr>';
	echo '<tr><td><label>Margins (mm)</label></td><td>';
	echo 'T:<input type="number" name="margin_top" class="form-control input-sm" style="width:50px;display:inline" value="' . (int)$_f('margin_top', 15) . '">';
	echo ' B:<input type="number" name="margin_bottom" class="form-control input-sm" style="width:50px;display:inline" value="' . (int)$_f('margin_bottom', 15) . '">';
	echo ' L:<input type="number" name="margin_left" class="form-control input-sm" style="width:50px;display:inline" value="' . (int)$_f('margin_left', 10) . '">';
	echo ' R:<input type="number" name="margin_right" class="form-control input-sm" style="width:50px;display:inline" value="' . (int)$_f('margin_right', 10) . '">';
	echo '</td></tr>';
	echo '</table></div>';
	echo '</div>'; // col-sm-6

	// Typography + Logo column
	echo '<div class="col-sm-6">';
	echo '<div class="ef-section"><span class="ef-section-title"><i class="fa fa-font"></i> Typography &amp; colours</span>';
	echo '<table class="ef-grid">';
	echo '<tr><td style="width:140px"><label>Font</label></td><td><input type="text" name="font_family" class="form-control input-sm" value="' . epc_erp_h($_f('font_family', 'Arial, sans-serif')) . '"></td></tr>';
	echo '<tr><td><label>Font size (pt)</label></td><td><input type="number" name="font_size" class="form-control input-sm" style="width:70px" value="' . (int)$_f('font_size', 11) . '"></td></tr>';
	echo '<tr><td><label>Primary colour</label></td><td><input type="color" name="primary_color" value="' . epc_erp_h($_f('primary_color', '#1565c0')) . '" style="width:50px;height:28px;border:1px solid #ccc"> <small class="text-muted">Headers, totals</small></td></tr>';
	echo '<tr><td><label>Secondary colour</label></td><td><input type="color" name="secondary_color" value="' . epc_erp_h($_f('secondary_color', '#4a6a7a')) . '" style="width:50px;height:28px;border:1px solid #ccc"> <small class="text-muted">Labels, muted text</small></td></tr>';
	echo '</table></div>';

	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-picture-o"></i> Logo</span>';
	echo '<table class="ef-grid">';
	echo '<tr><td style="width:140px"><label>Position</label></td><td><select name="logo_position" class="form-control input-sm">';
	foreach (array('left' => 'Left', 'center' => 'Center', 'right' => 'Right', 'none' => 'No logo') as $lk => $lv) {
		$sel = ($_f('logo_position', 'left') === $lk) ? ' selected' : '';
		echo '<option value="' . $lk . '"' . $sel . '>' . $lv . '</option>';
	}
	echo '</select></td></tr>';
	echo '<tr><td><label>Max height (px)</label></td><td><input type="number" name="logo_max_height" class="form-control input-sm" style="width:70px" value="' . (int)$_f('logo_max_height', 60) . '"></td></tr>';
	echo '</table></div>';
	echo '</div></div>'; // col-sm-6, row

	// Header HTML
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-code"></i> Header HTML</span>';
	echo '<textarea name="header_html" class="form-control" rows="4" placeholder="Header template with merge fields...">' . epc_erp_h($_f('header_html')) . '</textarea>';
	echo '<small class="text-muted">Merge fields: ' . implode(', ', array_keys($mergeFields)) . '</small></div>';

	// Body columns
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-columns"></i> Body columns (JSON)</span>';
	echo '<textarea name="body_columns" class="form-control" rows="4" placeholder=\'[{"key":"line_no","label":"#","width":"5%","align":"center"}]\'>' . epc_erp_h($_f('body_columns')) . '</textarea>';
	echo '<small class="text-muted">JSON array. Each column: {"key": "field_name", "label": "Header", "width": "15%", "align": "left|center|right"}</small></div>';

	// Footer + Custom CSS row
	echo '<div class="row"><div class="col-sm-6">';
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-file-text-o"></i> Footer HTML</span>';
	echo '<textarea name="footer_html" class="form-control" rows="3">' . epc_erp_h($_f('footer_html')) . '</textarea></div>';
	echo '</div><div class="col-sm-6">';
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-css3"></i> Custom CSS</span>';
	echo '<textarea name="custom_css" class="form-control" rows="3" placeholder="Additional CSS overrides...">' . epc_erp_h($_f('custom_css')) . '</textarea></div>';
	echo '</div></div>';

	// Terms + Bank row
	echo '<div class="row"><div class="col-sm-6">';
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-legal"></i> Terms &amp; conditions</span>';
	echo '<label><input type="checkbox" name="show_terms" value="1"' . (!empty($tpl['show_terms']) || $tpl === null ? ' checked' : '') . '> Show terms</label>';
	echo '<textarea name="terms_html" class="form-control" rows="3" placeholder="Terms and conditions text...">' . epc_erp_h($_f('terms_html')) . '</textarea></div>';
	echo '</div><div class="col-sm-6">';
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-university"></i> Bank details</span>';
	echo '<label><input type="checkbox" name="show_bank_details" value="1"' . (!empty($tpl['show_bank_details']) || $tpl === null ? ' checked' : '') . '> Show bank details</label>';
	echo '<textarea name="bank_details_html" class="form-control" rows="3" placeholder="Bank name, IBAN, SWIFT...">' . epc_erp_h($_f('bank_details_html')) . '</textarea></div>';
	echo '</div></div>';

	// Signatures
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-pencil-square-o"></i> Signatures</span>';
	echo '<label><input type="checkbox" name="show_signature_line" value="1"' . (!empty($tpl['show_signature_line']) || $tpl === null ? ' checked' : '') . '> Show signature lines</label>';
	echo '<input type="text" name="signature_labels" class="form-control input-sm" value="' . epc_erp_h($_f('signature_labels', 'Prepared by,Approved by,Received by')) . '" placeholder="Comma-separated labels">';
	echo '<small class="text-muted">Comma-separated labels for signature boxes</small></div>';

	// Extras
	echo '<div class="ef-section" style="margin-top:8px"><span class="ef-section-title"><i class="fa fa-qrcode"></i> Extras</span>';
	echo '<label><input type="checkbox" name="show_qr_code" value="1"' . (!empty($tpl['show_qr_code']) ? ' checked' : '') . '> Show QR code</label>';
	echo '&nbsp;&nbsp;<label><input type="checkbox" name="show_barcode" value="1"' . (!empty($tpl['show_barcode']) ? ' checked' : '') . '> Show barcode</label></div>';

	// Actions
	echo '<div class="ef-actions" style="margin-top:12px">';
	echo '<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save template</button>';
	echo ' <a href="' . epc_erp_h($pdBase) . '" class="btn btn-default btn-sm">Cancel</a>';
	echo '</div>';

	echo '</form></div></div>'; // form, ef-body, ef-window

/* ============================================================
 *  LIST VIEW
 * ============================================================ */
} else {
	$templates = array();
	if ($_pdOk && isset($db_link) && $db_link instanceof PDO && function_exists('epc_erp_print_templates_list')) {
		try { $templates = epc_erp_print_templates_list($db_link, $pdDocType); } catch (\Throwable $e) { $templates = array(); }
	}
	$grouped = array();
	foreach ($templates as $t) {
		$dt = isset($t['doc_type']) ? $t['doc_type'] : 'other';
		$grouped[$dt][] = $t;
	}

	echo '<div class="ef-window">';
	echo '<div class="ef-title"><i class="fa fa-paint-brush"></i> Print Templates</div>';
	echo '<div class="ef-toolbar">';
	echo '<a href="' . epc_erp_h($pdBase . '&pd_action=edit') . '" class="btn btn-primary btn-xs"><i class="fa fa-plus"></i> New template</a> ';
	echo '<select onchange="if(this.value)location=\'' . epc_erp_h($pdBase) . '&pd_doctype=\'+this.value;else location=\'' . epc_erp_h($pdBase) . '\';" class="form-control input-sm" style="width:auto;display:inline-block">';
	echo '<option value="">All doc types</option>';
	foreach ($docTypes as $k => $v) {
		echo '<option value="' . epc_erp_h($k) . '"' . ($pdDocType === $k ? ' selected' : '') . '>' . epc_erp_h($v) . '</option>';
	}
	echo '</select></div>';

	echo '<div class="ef-body">';
	if (empty($templates)) {
		echo '<div style="padding:20px;text-align:center;color:#999"><i class="fa fa-paint-brush fa-3x"></i><br>No templates found. <a href="' . epc_erp_h($pdBase . '&pd_action=edit') . '">Create one</a>.</div>';
	} else {
		foreach ($grouped as $dtype => $tpls) {
			echo '<div class="ef-section" style="margin-bottom:10px">';
			echo '<span class="ef-section-title"><i class="fa fa-file-text-o"></i> ' . epc_erp_h(isset($docTypes[$dtype]) ? $docTypes[$dtype] : $dtype) . '</span>';
			echo '<table class="ef-grid"><thead><tr><th>Name</th><th>Page</th><th>Default</th><th>Last updated</th><th></th></tr></thead><tbody>';
			foreach ($tpls as $t) {
				echo '<tr>';
				echo '<td><strong>' . epc_erp_h($t['name']) . '</strong></td>';
				echo '<td>' . epc_erp_h($t['page_size'] . ' / ' . $t['orientation']) . '</td>';
				echo '<td>' . ($t['is_default'] ? '<span class="label label-success">Default</span>' : '') . '</td>';
				echo '<td><small>' . (isset($t['time_updated']) && $t['time_updated'] > 0 ? date('d M Y', (int)$t['time_updated']) : '&mdash;') . '</small></td>';
				echo '<td><a href="' . epc_erp_h($pdBase . '&pd_action=edit&pd_id=' . (int)$t['id']) . '" class="btn btn-default btn-xs"><i class="fa fa-pencil"></i></a></td>';
				echo '</tr>';
			}
			echo '</tbody></table></div>';
		}
	}
	echo '</div>'; // ef-body

	echo '<div class="ef-status"><span>Templates: ' . count($templates) . '</span> <span>Types: ' . count($grouped) . '</span></div>';
	echo '</div>'; // ef-window
}
