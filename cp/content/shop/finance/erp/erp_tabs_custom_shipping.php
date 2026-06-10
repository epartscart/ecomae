<?php
/**
 * ERP â€” Custom & Shipping tab (dashboard, declaration list, create/view forms).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_declaration_pdf_import.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$csrf = isset($csrf) ? (string) $csrf : '';

epc_cs_ensure_schema($db_link);
epc_cs_ensure_box_schema($db_link);

$boxDefs = epc_cs_declaration_box_definitions();
$boxGroups = array('header' => 'Header (boxes 1â€“21)', 'footer' => 'Clearance & financials (boxes 38â€“59)');

$csView = isset($_GET['cs_view']) ? (string) $_GET['cs_view'] : 'dashboard';
$csCategory = isset($_GET['cs_category']) ? (string) $_GET['cs_category'] : '';
$csType = isset($_GET['cs_type']) ? (string) $_GET['cs_type'] : '';
$csId = isset($_GET['cs_id']) ? (int) $_GET['cs_id'] : 0;
$csReportKey = isset($_GET['cs_report']) ? (string) $_GET['cs_report'] : '';
$csReportFilters = epc_cs_report_filters_from_request($_GET);

if (isset($_GET['cs_export']) && (string) $_GET['cs_export'] === 'csv' && $csView === 'reports' && $csReportKey !== '') {
	epc_cs_handle_report_csv_export($db_link, $csReportKey, $csReportFilters);
}

$categories = epc_cs_categories_config();
$typeRegistry = epc_cs_declaration_types_registry();
$counts = epc_cs_dashboard_counts($db_link, $date_from, $date_to);
$reports = epc_cs_report_stubs();
$csUrls = epc_cs_configure_urls();
$recentRows = ($csView === 'dashboard')
	? epc_cs_list_declarations($db_link, array('from' => $date_from_str, 'to' => $date_to_str), 15)
	: array();
$lineItemsInitial = array();
$unitOptions = epc_cs_line_item_unit_options();
$volumeUnitOptions = epc_cs_line_item_volume_unit_options();

$baseCsUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str);
$csReportsUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'reports', 'cs_report' => 'search_results'));
$csReportsHubUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'reports'));
$newCustomDeclUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_category' => 'import'));
$guideUrlCs = $csUrls['csGuideUrl'];
if (function_exists('epc_erp_shell_append_query')) {
	$guideUrlCs = epc_erp_shell_append_query($guideUrlCs);
}

$csFormAssetVer = '20260609cs1';
if (!isset($GLOBALS['epc_cp_page_assets']) || !is_array($GLOBALS['epc_cp_page_assets'])) {
	$GLOBALS['epc_cp_page_assets'] = array('css' => array(), 'js' => array());
}
$GLOBALS['epc_cp_page_assets']['js']['/content/shop/finance/epc_custom_shipping_form.js?v=' . rawurlencode($csFormAssetVer)] = true;

?>
<style>
.epc-cs-grid { display: grid; gap: 14px; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); margin-bottom: 18px; }
.epc-cs-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; box-shadow: 0 8px 22px rgba(15,23,42,.06); padding: 16px; text-decoration: none !important; color: #1f2937; display: block; transition: transform .15s ease, box-shadow .15s ease; }
.epc-cs-card:hover { transform: translateY(-1px); box-shadow: 0 12px 28px rgba(37,99,235,.1); border-color: #2563eb; color: #111827; }
.epc-cs-card .cnt { font-size: 28px; font-weight: 700; line-height: 1.1; }
.epc-cs-card .lbl { font-size: 13px; color: #64748b; margin-top: 4px; }
.epc-cs-card .ico { float: right; font-size: 22px; opacity: .85; }
.epc-cs-reports { display: flex; flex-wrap: wrap; gap: 8px; margin: 12px 0 18px; }
.epc-cs-reports .label { font-size: 12px; padding: 6px 10px; }
.epc-cs-form-section { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px 16px; margin-bottom: 14px; }
.epc-cs-form-section h5 { margin: 0 0 12px; font-weight: 700; }
.epc-cs-items-table { font-size: 12px; margin-bottom: 8px; }
.epc-cs-items-table th { white-space: nowrap; background: #f1f5f9; }
.epc-cs-items-table input, .epc-cs-items-table select { min-width: 0; }
.epc-cs-items-table .form-control { padding: 4px 6px; height: 28px; font-size: 12px; }
.epc-cs-items-table tfoot td { font-weight: 700; background: #eef2ff; }
.epc-cs-items-table .col-line { width: 42px; text-align: center; }
.epc-cs-items-table .col-rm { width: 36px; text-align: center; }
.epc-cs-autofill { background: #ecfdf5 !important; border-color: #6ee7b7 !important; }
.epc-cs-autofill-label::after { content: ' auto'; font-size: 10px; color: #059669; font-weight: normal; }
.epc-cs-pdf-panel { background: #eff6ff; border: 1px dashed #93c5fd; border-radius: 8px; padding: 14px 16px; margin-bottom: 14px; }
.epc-cs-box-grid .form-control { font-size: 12px; }
.epc-cs-multi-lines .epc-cs-multi-row { display: flex; gap: 8px; margin-bottom: 6px; align-items: center; }
.epc-cs-multi-lines .epc-cs-multi-row input { flex: 1; }
.epc-cs-line-ext { font-size: 11px; color: #64748b; }
.epc-cs-pdf-viewer { background: #fff; border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 14px; overflow: hidden; }
.epc-cs-pdf-viewer iframe { width: 100%; height: 520px; border: 0; display: block; background: #f8fafc; }
.epc-cs-pdf-viewer-head { padding: 8px 12px; background: #f1f5f9; border-bottom: 1px solid #e2e8f0; font-size: 12px; }
.epc-cs-autofill-panel { border: 1px solid #6ee7b7; background: #f0fdf4; border-radius: 8px; padding: 14px 16px; margin-bottom: 14px; }
.epc-cs-autofill-panel.is-empty { opacity: .65; }
.epc-cs-form-actions { position: sticky; top: 0; z-index: 20; background: #fff; border: 1px solid #e5e7eb; border-radius: 8px; padding: 10px 14px; margin-bottom: 14px; box-shadow: 0 2px 8px rgba(15,23,42,.06); }
.epc-cs-save-bar {
	position: sticky;
	top: 0;
	z-index: 1050;
	background: linear-gradient(135deg, #1e40af 0%, #2563eb 55%, #3b82f6 100%);
	border-radius: 10px;
	padding: 14px 18px;
	margin: 0 0 16px;
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	justify-content: space-between;
	gap: 12px;
	box-shadow: 0 6px 24px rgba(37, 99, 235, .35);
	color: #fff;
}
.epc-cs-save-bar-label { font-size: 15px; font-weight: 700; margin: 0; line-height: 1.3; }
.epc-cs-save-bar-label small { display: block; font-size: 12px; font-weight: 500; opacity: .9; margin-top: 2px; }
.epc-cs-save-bar-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.epc-cs-save-bar .btn-save-custom {
	font-size: 16px;
	font-weight: 700;
	padding: 10px 26px;
	background: #fff;
	color: #1d4ed8;
	border: 0;
	border-radius: 8px;
	box-shadow: 0 2px 8px rgba(15, 23, 42, .15);
}
.epc-cs-save-bar .btn-save-custom:hover,
.epc-cs-save-bar .btn-save-custom:focus { background: #eff6ff; color: #1e3a8a; }
.epc-cs-save-bar .btn-cancel-light { color: #fff; border-color: rgba(255,255,255,.55); background: transparent; }
.epc-cs-save-bar .btn-cancel-light:hover { background: rgba(255,255,255,.12); color: #fff; }
.epc-cs-dashboard-cta {
	background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
	border: 2px solid #93c5fd;
	border-radius: 12px;
	padding: 18px 20px;
	margin-bottom: 18px;
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	justify-content: space-between;
	gap: 14px;
}
.epc-cs-dashboard-cta p { margin: 0; font-size: 14px; color: #1e3a8a; max-width: 560px; }
.epc-cs-list-cta { margin: 12px 0 16px; }
.epc-cs-row-actions .btn { min-width: 28px; }
.epc-cs-pdf-modal { display: none; position: fixed; inset: 0; z-index: 10050; background: rgba(15,23,42,.55); padding: 24px; align-items: center; justify-content: center; }
.epc-cs-pdf-modal.is-open { display: flex !important; align-items: center; justify-content: center; }
.epc-cs-pdf-modal__dialog { width: min(960px, 100%); max-height: calc(100vh - 48px); background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 24px 48px rgba(15,23,42,.25); display: flex; flex-direction: column; margin: 0 auto; }
.epc-cs-pdf-modal__head { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 16px; background: #f1f5f9; border-bottom: 1px solid #e2e8f0; }
.epc-cs-pdf-modal__body { flex: 1; min-height: 0; }
.epc-cs-pdf-modal__body iframe { width: 100%; height: min(72vh, 640px); border: 0; display: block; background: #f8fafc; }
.epc-cs-pdf-modal__empty { padding: 48px 24px; text-align: center; color: #64748b; font-size: 14px; }
</style>

<div class="epc-erp-section">
	<div class="epc-erp-hero">
		<h3><i class="fa fa-ship"></i> <?php echo epc_cs_h('Custom & Shipping'); ?></h3>
		<p>Customs declarations and logistics documentation â€” mapped from the C&amp;L Excel format (Phase 1 registry + Phase 2 reports).</p>
		<p class="text-muted" style="margin-bottom:0;">
			<a class="btn btn-primary btn-xs" href="<?php echo epc_cs_h($guideUrlCs); ?>"><i class="fa fa-book"></i> Operator guide</a>
			<a class="btn btn-default btn-xs" href="<?php echo epc_cs_h($baseCsUrl); ?>"><i class="fa fa-dashboard"></i> Dashboard</a>
			<a class="btn btn-default btn-xs" href="<?php echo epc_cs_h($csReportsUrl); ?>"><i class="fa fa-bar-chart"></i> Reports</a>
		</p>
	</div>

<?php if ($csView === 'dashboard'): ?>

	<div class="epc-cs-dashboard-cta">
		<p><strong>Need to save a customs declaration?</strong> Open the declaration form â€” the blue <strong>Save custom declaration</strong> bar stays pinned at the top while you fill fields or upload a PDF.</p>
		<a class="btn btn-primary btn-lg" href="<?php echo epc_cs_h($newCustomDeclUrl); ?>"><i class="fa fa-plus-circle"></i> New custom declaration</a>
	</div>

	<div class="epc-erp-kpi" style="margin-bottom:16px;">
		<div class="kpi"><div class="lbl">Total declarations</div><div class="val"><?php echo (int) $counts['total']; ?></div></div>
		<div class="kpi"><div class="lbl">Draft</div><div class="val"><?php echo (int) $counts['draft']; ?></div></div>
		<div class="kpi"><div class="lbl">Submitted</div><div class="val"><?php echo (int) $counts['submitted']; ?></div></div>
		<div class="kpi"><div class="lbl">Cleared</div><div class="val"><?php echo (int) $counts['cleared']; ?></div></div>
	</div>

	<div class="panel panel-default">
		<div class="panel-heading"><strong><i class="fa fa-folder-open-o"></i> Declaration categories</strong></div>
		<div class="panel-body">
	<p class="text-muted">Pick a category to list declarations or create a new entry. <?php echo array_sum(array_map('count', $typeRegistry)); ?> declaration types registered from Excel.</p>
	<div class="epc-cs-grid">
		<?php foreach ($categories as $catKey => $catMeta): ?>
			<?php $cnt = (int) ($counts['by_category'][$catKey] ?? 0); ?>
			<a class="epc-cs-card" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'list', 'cs_category' => $catKey))); ?>">
				<i class="fa <?php echo epc_cs_h($catMeta['icon']); ?> ico" style="color:<?php echo epc_cs_h($catMeta['color']); ?>;"></i>
				<div class="cnt"><?php echo $cnt; ?></div>
				<div class="lbl"><?php echo epc_cs_h($catMeta['label']); ?></div>
				<small class="text-muted"><?php echo count($typeRegistry[$catKey] ?? array()); ?> types</small>
			</a>
		<?php endforeach; ?>
	</div>
		</div>
	</div>

	<div class="panel panel-default">
		<div class="panel-heading"><strong><i class="fa fa-bolt"></i> Quick actions</strong></div>
		<div class="panel-body">
	<p>
		<a class="btn btn-primary btn-sm" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_category' => 'import', 'cs_type' => 'Import to Local from ROW'))); ?>"><i class="fa fa-plus"></i> New import (Local from ROW)</a>
		<a class="btn btn-success btn-sm" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_category' => 'export', 'cs_type' => 'Export from Local to ROW'))); ?>"><i class="fa fa-plus"></i> New export (Local to ROW)</a>
		<a class="btn btn-info btn-sm" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_category' => 'transit', 'cs_type' => 'FZ transit in'))); ?>"><i class="fa fa-plus"></i> New FZ transit in</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_category' => 'lgp'))); ?>"><i class="fa fa-plus"></i> New LGP entry</a>
	</p>
		</div>
	</div>

	<div class="panel panel-default">
		<div class="panel-heading"><strong><i class="fa fa-clock-o"></i> Recent declarations</strong></div>
		<div class="panel-body">
	<?php if (empty($recentRows)): ?>
		<p class="text-muted">No declarations in this date range yet. Use a category card or quick action above to create one.</p>
	<?php else: ?>
		<table class="table table-striped table-bordered table-condensed">
			<thead><tr>
				<th>ID</th><th>Category</th><th>Type</th><th>Company</th><th>Entry date</th><th>Items</th><th>Status</th><th>Actions</th>
			</tr></thead>
			<tbody>
			<?php foreach ($recentRows as $r): ?>
				<?php $catKey = (string) ($r['category'] ?? 'import'); $catLbl = $categories[$catKey]['label'] ?? $catKey; ?>
				<tr>
					<td><?php echo (int) $r['id']; ?></td>
					<td><?php echo epc_cs_h($catLbl); ?></td>
					<td><?php echo epc_cs_h($r['declaration_type']); ?></td>
					<td><?php echo epc_cs_h($r['company']); ?></td>
					<td><?php echo epc_cs_h($r['entry_date'] ?: 'â€”'); ?></td>
					<td><?php echo (int) ($r['item_count'] ?? 0); ?></td>
					<td><span class="label label-<?php echo $r['status'] === 'cleared' ? 'success' : ($r['status'] === 'submitted' ? 'info' : 'default'); ?>"><?php echo epc_cs_h($r['status']); ?></span></td>
					<td><?php echo epc_cs_declaration_row_actions_html($erpUrl, $date_from_str, $date_to_str, $r); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	<?php endif; ?>
		</div>
	</div>

	<div class="panel panel-default">
		<div class="panel-heading"><strong><i class="fa fa-bar-chart"></i> Reports</strong></div>
		<div class="panel-body">
			<p class="text-muted">Declaration search, cost summary, duty stub, re-export tracking, and document expiry â€” filter, print, and export CSV.</p>
			<div class="epc-erp-report-grid">
				<?php foreach ($reports as $repKey => $rep): ?>
					<div class="epc-erp-report-tile">
						<h5><i class="fa <?php echo epc_cs_h($rep['icon']); ?>"></i> <?php echo epc_cs_h($rep['label']); ?></h5>
						<p class="text-muted" style="font-size:12px;"><?php echo epc_cs_h($rep['desc'] ?? ''); ?></p>
						<a class="btn btn-primary btn-xs" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'reports', 'cs_report' => $repKey))); ?>">Open</a>
					</div>
				<?php endforeach; ?>
			</div>
			<p style="margin:8px 0 0;"><a class="btn btn-primary btn-sm" href="<?php echo epc_cs_h($csReportsUrl); ?>"><i class="fa fa-list"></i> Declaration registry</a> <a class="btn btn-default btn-sm" href="<?php echo epc_cs_h($csReportsHubUrl); ?>"><i class="fa fa-th"></i> All reports</a></p>
		</div>
	</div>

<?php elseif ($csView === 'reports'): ?>

	<?php require __DIR__ . '/custom_shipping/custom_shipping_reports.php'; ?>

<?php elseif ($csView === 'list'): ?>

	<?php
	if (!isset($categories[$csCategory])) {
		$csCategory = 'import';
	}
	$catMeta = $categories[$csCategory];
	$rows = epc_cs_list_declarations($db_link, array(
		'category' => $csCategory,
		'from' => $date_from_str,
		'to' => $date_to_str,
	), 200);
	?>
	<p><a href="<?php echo epc_cs_h($baseCsUrl); ?>">&larr; Dashboard</a></p>
	<h4><i class="fa <?php echo epc_cs_h($catMeta['icon']); ?>"></i> <?php echo epc_cs_h($catMeta['label']); ?> declarations</h4>
	<div class="epc-cs-list-cta">
		<a class="btn btn-primary btn-lg" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_category' => $csCategory))); ?>"><i class="fa fa-plus-circle"></i> New custom declaration</a>
		<span class="text-muted" style="margin-left:10px;font-size:13px;">Opens the form with <strong>Save custom declaration</strong> at the top</span>
	</div>
	<table class="table table-striped table-bordered table-condensed">
		<thead><tr>
			<th>ID</th><th>Type</th><th>Company</th><th>Emirate</th><th>Entry date</th><th>Decl. #</th><th>Items</th><th>Status</th><th>Amount AED</th><th>Actions</th>
		</tr></thead>
		<tbody>
		<?php if (empty($rows)): ?>
			<tr><td colspan="10" class="text-muted text-center">No declarations yet in this category.</td></tr>
		<?php else: ?>
			<?php foreach ($rows as $r): ?>
				<tr>
					<td><?php echo (int) $r['id']; ?></td>
					<td><?php echo epc_cs_h($r['declaration_type']); ?></td>
					<td><?php echo epc_cs_h($r['company']); ?></td>
					<td><?php echo epc_cs_h($r['customs_emirate']); ?></td>
					<td><?php echo epc_cs_h($r['entry_date'] ?: 'â€”'); ?></td>
					<td><?php echo epc_cs_h($r['declaration_number'] ?: 'â€”'); ?></td>
					<td><?php echo (int) ($r['item_count'] ?? 0); ?></td>
					<td><span class="label label-<?php echo $r['status'] === 'cleared' ? 'success' : ($r['status'] === 'submitted' ? 'info' : 'default'); ?>"><?php echo epc_cs_h($r['status']); ?></span></td>
					<td><?php echo epc_cs_h(number_format((float) $r['invoice_amount_aed'], 2)); ?></td>
					<td><?php echo epc_cs_declaration_row_actions_html($erpUrl, $date_from_str, $date_to_str, $r); ?></td>
				</tr>
			<?php endforeach; ?>
		<?php endif; ?>
		</tbody>
	</table>

	<h5>Declaration types in this category</h5>
	<ul class="list-inline">
		<?php foreach ($typeRegistry[$csCategory] ?? array() as $t): ?>
			<li><a href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_category' => $csCategory, 'cs_type' => $t))); ?>"><?php echo epc_cs_h($t); ?></a></li>
		<?php endforeach; ?>
	</ul>

<?php elseif ($csView === 'form' || $csView === 'view'): ?>

	<?php
	$edit = null;
	if ($csId > 0) {
		$edit = epc_cs_get_declaration($db_link, $csId);
		if ($edit) {
			$csCategory = (string) $edit['category'];
			$csType = (string) $edit['declaration_type'];
		}
	}
	if (!isset($categories[$csCategory])) {
		$csCategory = 'import';
	}
	$fieldDefs = epc_cs_field_definitions($csCategory);
	$typesForCat = $typeRegistry[$csCategory] ?? array();
	$isView = ($csView === 'view' && $edit);
	$title = $isView ? 'View declaration #' . (int) $edit['id'] : ($edit ? 'Edit declaration #' . (int) $edit['id'] : 'New declaration');
	if ($edit && !empty($edit['line_items'])) {
		$lineItemsInitial = $edit['line_items'];
	}
	$boxDataInitial = is_array($edit['box_data'] ?? null) ? $edit['box_data'] : array();
	$boxValues = is_array($boxDataInitial['boxes'] ?? null) ? $boxDataInitial['boxes'] : array();
	$box45Lines = is_array($boxDataInitial['box_45_lines'] ?? null) ? $boxDataInitial['box_45_lines'] : array();
	$box54Lines = is_array($boxDataInitial['box_54_lines'] ?? null) ? $boxDataInitial['box_54_lines'] : array();
	if (empty($box45Lines) && !empty($boxValues['box_45'])) {
		$box45Lines = array($boxValues['box_45']);
	}
	if (empty($box54Lines) && !empty($boxValues['box_54'])) {
		$box54Lines = preg_split('/\s*\|\s*/', (string) $boxValues['box_54']);
	}
	$pdfAutofillKeys = is_array($edit['pdf_autofill_keys'] ?? null) ? $edit['pdf_autofill_keys'] : array();
	$fieldGroups = epc_cs_form_field_groups($csCategory);
	$manualFieldDefs = $fieldGroups['manual'];
	$workflowFieldDefs = $fieldGroups['workflow'];
	$skipBoxKeys = epc_cs_form_skip_box_keys();
	$pdfViewerUrl = ($edit && !empty($edit['pdf_file_path'])) ? epc_cs_pdf_public_url($edit['pdf_file_path']) : '';
	$pdfViewerName = ($edit && !empty($edit['pdf_file_name'])) ? (string) $edit['pdf_file_name'] : 'Declaration PDF';

	function epc_cs_render_field_row($key, $meta, $edit, $csCategory, $csType, $typesForCat, $pdfAutofillKeys, $sectionClass = '')
	{
		$val = '';
		if ($edit) {
			$val = isset($edit[$key]) ? $edit[$key] : ($edit['field_data'][$key] ?? '');
		} elseif ($key === 'declaration_type' && $csType !== '') {
			$val = $csType;
		} elseif ($key === 'entry_date' || $key === 'declaration_date') {
			$val = date('Y-m-d');
		} elseif ($key === 'customs_emirate') {
			$val = 'DUBAI';
		}
		$col = ($meta['type'] === 'textarea') ? 'col-sm-12' : 'col-sm-6';
		$req = !empty($meta['required']) ? ' required' : '';
		$isAutofill = in_array($key, $pdfAutofillKeys, true);
		$manualOnly = in_array($key, epc_cs_pdf_manual_only_fields(), true);
		$fieldClass = 'form-control input-sm epc-cs-core-field' . ($isAutofill ? ' epc-cs-autofill' : '');
		?>
		<div class="<?php echo $col; ?> epc-cs-field-wrap<?php echo $sectionClass !== '' ? ' ' . epc_cs_h($sectionClass) : ''; ?>" style="margin-bottom:10px;" data-field-key="<?php echo epc_cs_h($key); ?>">
			<label class="<?php echo $isAutofill ? 'epc-cs-autofill-label' : ''; ?>"><?php echo epc_cs_h($meta['label']); ?><?php if (!empty($meta['required'])): ?> <span class="text-danger">*</span><?php endif; ?><?php if ($manualOnly): ?> <span class="text-muted" style="font-size:10px;">(manual)</span><?php endif; ?></label>
			<?php if ($meta['type'] === 'select' && $key === 'declaration_type'): ?>
				<select name="<?php echo epc_cs_h($key); ?>" class="<?php echo $fieldClass; ?>"<?php echo $req; ?> id="epc_cs_field_declaration_type">
					<option value="">â€” select â€”</option>
					<?php foreach ($typesForCat as $t): ?>
						<option value="<?php echo epc_cs_h($t); ?>"<?php echo ($val === $t) ? ' selected' : ''; ?>><?php echo epc_cs_h($t); ?></option>
					<?php endforeach; ?>
				</select>
			<?php elseif ($meta['type'] === 'select'): ?>
				<select name="<?php echo epc_cs_h($key); ?>" class="<?php echo $fieldClass; ?>"<?php echo $req; ?>>
					<?php foreach (($meta['options'] ?? array()) as $opt): ?>
						<option value="<?php echo epc_cs_h($opt); ?>"<?php echo ($val === $opt) ? ' selected' : ''; ?>><?php echo epc_cs_h($opt ?: 'â€”'); ?></option>
					<?php endforeach; ?>
				</select>
			<?php elseif ($meta['type'] === 'textarea'): ?>
				<textarea name="<?php echo epc_cs_h($key); ?>" class="<?php echo $fieldClass; ?>" rows="2"<?php echo $req; ?>><?php echo epc_cs_h($val); ?></textarea>
			<?php else: ?>
				<input type="<?php echo epc_cs_h($meta['type']); ?>" name="<?php echo epc_cs_h($key); ?>" class="<?php echo $fieldClass; ?>" value="<?php echo epc_cs_h($val); ?>"<?php echo $req; ?>>
			<?php endif; ?>
		</div>
		<?php
	}
	?>
	<p><a href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'list', 'cs_category' => $csCategory))); ?>">&larr; <?php echo epc_cs_h($categories[$csCategory]['label']); ?> list</a></p>
	<h4><?php echo epc_cs_h($title); ?></h4>

	<?php if ($csId > 0 && !$edit): ?>
		<div class="alert alert-warning">Declaration #<?php echo (int) $csId; ?> was not found. <a href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'list', 'cs_category' => $csCategory))); ?>">Back to list</a></div>
	<?php elseif ($isView): ?>
		<?php if ($pdfViewerUrl !== ''): ?>
		<div class="epc-cs-pdf-viewer">
			<div class="epc-cs-pdf-viewer-head">
				<strong><i class="fa fa-file-pdf-o"></i> Declaration copy</strong> â€” <?php echo epc_cs_h($pdfViewerName); ?>
				<a class="btn btn-default btn-xs pull-right" href="<?php echo epc_cs_h($pdfViewerUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open PDF</a>
			</div>
			<iframe src="<?php echo epc_cs_h($pdfViewerUrl); ?>#toolbar=1" title="Declaration PDF"></iframe>
		</div>
		<?php else: ?>
		<p class="text-muted"><i class="fa fa-file-pdf-o"></i> No PDF attached yet. Use <strong>Edit</strong> to upload and parse a declaration copy.</p>
		<?php endif; ?>
		<table class="table table-bordered table-condensed">
			<?php foreach ($fieldDefs as $key => $meta): ?>
				<?php
				$val = isset($edit[$key]) ? $edit[$key] : ($edit['field_data'][$key] ?? '');
				if ($val === '' || $val === null) {
					continue;
				}
				?>
				<tr><th style="width:220px;"><?php echo epc_cs_h($meta['label']); ?></th><td><?php echo epc_cs_h($val); ?></td></tr>
			<?php endforeach; ?>
			<tr><th>Status</th><td><?php echo epc_cs_h($edit['status']); ?></td></tr>
		</table>
		<?php if (!empty($lineItemsInitial)): ?>
			<h5 style="margin-top:16px;"><i class="fa fa-list-ol"></i> Declaration line items (<?php echo count($lineItemsInitial); ?>)</h5>
			<table class="table table-bordered table-condensed epc-cs-items-table">
				<thead><tr>
					<th>#</th><th>HS code</th><th>Origin</th><th>Description</th><th>Qty</th><th>Unit</th><th>Volume</th><th>Amount</th><th>Weight</th>
				</tr></thead>
				<tbody>
				<?php
				$sumQty = 0;
				$sumVol = 0;
				$sumAmt = 0;
				foreach ($lineItemsInitial as $li):
					$sumQty += (float) $li['quantity'];
					$sumVol += (float) $li['volume'];
					$sumAmt += (float) $li['amount'];
				?>
					<tr>
						<td><?php echo (int) $li['line_number']; ?></td>
						<td><?php echo epc_cs_h($li['hs_code']); ?></td>
						<td><?php echo epc_cs_h($li['country_of_origin']); ?></td>
						<td><?php echo epc_cs_h($li['description'] ?: 'â€”'); ?></td>
						<td><?php echo epc_cs_h($li['quantity']); ?></td>
						<td><?php echo epc_cs_h($li['unit']); ?></td>
						<td><?php echo epc_cs_h($li['volume'] . ' ' . $li['volume_unit']); ?></td>
						<td><?php echo epc_cs_h(number_format((float) $li['amount'], 2)); ?></td>
						<td><?php echo (float) $li['weight'] > 0 ? epc_cs_h($li['weight']) : 'â€”'; ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
				<tfoot><tr>
					<td colspan="4" class="text-right">Totals</td>
					<td><?php echo epc_cs_h($sumQty); ?></td>
					<td></td>
					<td><?php echo epc_cs_h($sumVol); ?></td>
					<td><?php echo epc_cs_h(number_format($sumAmt, 2)); ?></td>
					<td></td>
				</tr></tfoot>
			</table>
		<?php endif; ?>
		<p>
			<a class="btn btn-default btn-sm" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_id' => (int) $edit['id']))); ?>"><i class="fa fa-pencil"></i> Edit</a>
			<a class="btn btn-info btn-sm" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'form', 'cs_id' => (int) $edit['id']))); ?>#epc_cs_pdf_panel"><i class="fa fa-upload"></i> Re-import PDF</a>
			<?php if ($pdfViewerUrl !== ''): ?>
				<a class="btn btn-default btn-sm" href="<?php echo epc_cs_h($pdfViewerUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-file-pdf-o"></i> Open PDF</a>
			<?php endif; ?>
			<button type="button" class="btn btn-danger btn-sm epc-cs-delete-btn" data-id="<?php echo (int) $edit['id']; ?>" data-category="<?php echo epc_cs_h($csCategory); ?>"><i class="fa fa-trash"></i> Delete</button>
			<?php if ($edit['status'] === 'draft'): ?>
				<button type="button" class="btn btn-primary btn-sm" id="epc_cs_submit_btn" data-id="<?php echo (int) $edit['id']; ?>"><i class="fa fa-send"></i> Submit</button>
			<?php endif; ?>
		</p>
	<?php else: ?>
		<div class="epc-cs-save-bar" id="epc_cs_save_bar">
			<div>
				<p class="epc-cs-save-bar-label"><i class="fa fa-save"></i> Save custom declaration
					<small><?php echo $edit ? 'Edit #' . (int) $edit['id'] : 'New entry'; ?> â€” <?php echo epc_cs_h($categories[$csCategory]['label']); ?></small>
				</p>
			</div>
			<div class="epc-cs-save-bar-actions">
				<button type="submit" form="epc_cs_declaration_form" class="btn btn-save-custom" id="epc_cs_save_btn_top"><i class="fa fa-save"></i> Save custom declaration</button>
				<a class="btn btn-default btn-cancel-light" href="<?php echo epc_cs_h($baseCsUrl); ?>">Cancel</a>
				<?php if ($edit): ?>
					<button type="button" class="btn btn-danger epc-cs-delete-btn" data-id="<?php echo (int) $edit['id']; ?>" data-category="<?php echo epc_cs_h($csCategory); ?>"><i class="fa fa-trash"></i> Delete</button>
				<?php endif; ?>
			</div>
		</div>

		<div class="epc-cs-pdf-panel" id="epc_cs_pdf_panel">
			<h5 style="margin:0 0 8px;"><i class="fa fa-file-pdf-o"></i> <?php echo $edit ? 'Re-upload declaration PDF' : 'Upload declaration PDF'; ?></h5>
			<p class="text-muted" style="margin:0 0 10px;font-size:12px;"><?php echo $edit ? 'Choose a new PDF to replace the stored copy and re-parse boxes 1â€“59.' : 'Upload the UAE customs declaration copy â€” the system reads boxes 1â€“59 and fills matching fields.'; ?> Supplier details and other ERP fields stay editable.</p>
			<div class="row" style="margin-bottom:8px;">
				<div class="col-sm-4">
					<label class="small">Declaration type (optional hint)</label>
					<select id="epc_cs_pdf_type_hint" class="form-control input-sm">
						<option value="">â€” auto-detect â€”</option>
						<?php foreach ($typesForCat as $t): ?>
							<option value="<?php echo epc_cs_h($t); ?>"<?php echo ($csType === $t) ? ' selected' : ''; ?>><?php echo epc_cs_h($t); ?></option>
						<?php endforeach; ?>
					</select>
				</div>
				<div class="col-sm-5">
					<label class="small">Declaration PDF</label>
					<input type="file" id="epc_cs_pdf_file" accept="application/pdf,.pdf" class="form-control input-sm">
				</div>
				<div class="col-sm-3" style="padding-top:22px;">
					<button type="button" class="btn btn-info btn-sm btn-block" id="epc_cs_pdf_upload_btn"><i class="fa fa-upload"></i> Parse PDF</button>
				</div>
			</div>
			<div id="epc_cs_pdf_status" class="small text-muted" style="display:none;"></div>
			<div id="epc_cs_pdf_error" class="alert alert-warning" style="display:none;margin-top:8px;">
				<p id="epc_cs_pdf_error_msg" style="margin:0 0 8px;"></p>
				<p id="epc_cs_pdf_server_diag" class="small" style="margin:0 0 8px;display:none;"></p>
				<button type="button" class="btn btn-default btn-xs" id="epc_cs_pdf_manual_btn"><i class="fa fa-pencil"></i> Continue filling form manually</button>
			</div>
		</div>

		<form id="epc_cs_declaration_form" class="form-horizontal">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cs_h($csrf); ?>">
			<input type="hidden" name="action" value="cs_save_declaration">
			<input type="hidden" name="category" value="<?php echo epc_cs_h($csCategory); ?>">
			<input type="hidden" name="from" value="<?php echo epc_cs_h($date_from_str); ?>">
			<input type="hidden" name="to" value="<?php echo epc_cs_h($date_to_str); ?>">
			<input type="hidden" name="pdf_token" id="epc_cs_pdf_token" value="">
			<input type="hidden" name="pdf_file_name" id="epc_cs_pdf_file_name" value="">
			<?php if ($edit): ?><input type="hidden" name="id" value="<?php echo (int) $edit['id']; ?>"><?php endif; ?>

			<div class="epc-cs-form-section">
				<h5><i class="fa fa-pencil"></i> Manual ERP fields <small class="text-muted">â€” supplier, PO, LC, SRV, D365 refs (never auto-filled from PDF)</small></h5>
				<div class="row">
					<?php foreach ($manualFieldDefs as $key => $meta): ?>
						<?php epc_cs_render_field_row($key, $meta, $edit, $csCategory, $csType, $typesForCat, $pdfAutofillKeys, 'epc-cs-manual-field'); ?>
					<?php endforeach; ?>
				</div>
			</div>

			<div id="epc_cs_pdf_viewer_wrap" class="epc-cs-pdf-viewer" style="<?php echo $pdfViewerUrl !== '' ? '' : 'display:none;'; ?>">
				<div class="epc-cs-pdf-viewer-head">
					<strong><i class="fa fa-file-pdf-o"></i> Declaration copy</strong>
					<span id="epc_cs_pdf_viewer_label"><?php echo epc_cs_h($pdfViewerName); ?></span>
					<a class="btn btn-default btn-xs pull-right" id="epc_cs_pdf_open_link" href="<?php echo epc_cs_h($pdfViewerUrl); ?>" target="_blank" rel="noopener" style="<?php echo $pdfViewerUrl !== '' ? '' : 'display:none;'; ?>"><i class="fa fa-external-link"></i> Open PDF</a>
				</div>
				<iframe id="epc_cs_pdf_viewer_frame" src="<?php echo epc_cs_h($pdfViewerUrl !== '' ? $pdfViewerUrl . '#toolbar=1' : ''); ?>" title="Declaration PDF"></iframe>
			</div>

			<div class="epc-cs-autofill-panel<?php echo empty($pdfAutofillKeys) ? ' is-empty' : ''; ?>" id="epc_cs_autofill_panel">
				<h5 style="margin-top:0;"><i class="fa fa-magic"></i> Auto-filled from PDF <small class="text-muted">â€” green fields below (header + boxes 1â€“59 + line items)</small></h5>
				<p class="text-muted" style="margin:0 0 12px;font-size:12px;">Declaration #, B/L, weights, ports, and amounts appear here only â€” not duplicated in manual fields above.</p>

			<?php if (!empty($workflowFieldDefs)): ?>
			<div class="epc-cs-form-section" style="background:transparent;border:none;padding:0 0 8px;margin-bottom:8px;">
				<h5><i class="fa fa-check-square-o"></i> Declaration header <?php if ($csCategory !== 'lgp'): ?><small class="text-muted">â€” company, emirate, type, dates</small><?php endif; ?></h5>
				<div class="row">
					<?php foreach ($workflowFieldDefs as $key => $meta): ?>
						<?php epc_cs_render_field_row($key, $meta, $edit, $csCategory, $csType, $typesForCat, $pdfAutofillKeys, 'epc-cs-workflow-field'); ?>
					<?php endforeach; ?>
				</div>
			</div>
			<?php endif; ?>

			<?php foreach ($boxGroups as $groupKey => $groupLabel): ?>
			<div class="epc-cs-form-section epc-cs-box-grid">
				<h5><i class="fa fa-th-list"></i> <?php echo epc_cs_h($groupLabel); ?></h5>
				<div class="row">
					<?php foreach ($boxDefs as $bKey => $bMeta): ?>
						<?php if (($bMeta['group'] ?? '') !== $groupKey || !empty($bMeta['multi']) || in_array($bKey, $skipBoxKeys, true)) continue;
						$bVal = $boxValues[$bKey] ?? '';
						$bAuto = in_array($bKey, $pdfAutofillKeys, true);
						$bCol = (($bMeta['type'] ?? '') === 'textarea') ? 'col-sm-12' : 'col-sm-4';
						?>
						<div class="<?php echo $bCol; ?>" style="margin-bottom:8px;" data-box-key="<?php echo epc_cs_h($bKey); ?>">
							<label class="small<?php echo $bAuto ? ' epc-cs-autofill-label' : ''; ?>">Box <?php echo epc_cs_h($bMeta['num']); ?> â€” <?php echo epc_cs_h($bMeta['label']); ?></label>
							<?php if (($bMeta['type'] ?? '') === 'textarea'): ?>
								<textarea name="boxes[<?php echo epc_cs_h($bKey); ?>]" class="form-control input-sm epc-cs-box-field<?php echo $bAuto ? ' epc-cs-autofill' : ''; ?>" rows="2"><?php echo epc_cs_h($bVal); ?></textarea>
							<?php else: ?>
								<input type="text" name="boxes[<?php echo epc_cs_h($bKey); ?>]" class="form-control input-sm epc-cs-box-field<?php echo $bAuto ? ' epc-cs-autofill' : ''; ?>" value="<?php echo epc_cs_h($bVal); ?>">
							<?php endif; ?>
						</div>
					<?php endforeach; ?>
				</div>
			</div>
			<?php endforeach; ?>

			<div class="epc-cs-form-section">
				<h5>Box 45 â€” Other remarks <small class="text-muted">(multi-line)</small></h5>
				<div class="epc-cs-multi-lines" id="epc_cs_box45_lines">
					<?php if (empty($box45Lines)): $box45Lines = array(''); endif; ?>
					<?php foreach ($box45Lines as $i => $ln): ?>
					<div class="epc-cs-multi-row">
						<input type="text" name="box_45_lines[]" class="form-control input-sm epc-cs-box45-input<?php echo ($i === 0 && in_array('box_45', $pdfAutofillKeys, true)) ? ' epc-cs-autofill' : ''; ?>" value="<?php echo epc_cs_h($ln); ?>" placeholder="e.g. [FOB] FRT: INS:">
						<button type="button" class="btn btn-link btn-xs text-danger epc-cs-rm-multi" title="Remove"><i class="fa fa-times"></i></button>
					</div>
					<?php endforeach; ?>
				</div>
				<button type="button" class="btn btn-default btn-xs" id="epc_cs_add_box45"><i class="fa fa-plus"></i> Add line</button>
			</div>

			<div class="epc-cs-form-section">
				<h5>Box 54 â€” Payment No. <small class="text-muted">(multi-column / multi-line)</small></h5>
				<div class="epc-cs-multi-lines" id="epc_cs_box54_lines">
					<?php if (empty($box54Lines)): $box54Lines = array(''); endif; ?>
					<?php foreach ($box54Lines as $i => $ln): ?>
					<div class="epc-cs-multi-row">
						<input type="text" name="box_54_lines[]" class="form-control input-sm epc-cs-box54-input<?php echo ($i === 0 && in_array('box_54', $pdfAutofillKeys, true)) ? ' epc-cs-autofill' : ''; ?>" value="<?php echo epc_cs_h($ln); ?>" placeholder="e.g. DEPO 6768.00 [4779319] SG-2000144">
						<button type="button" class="btn btn-link btn-xs text-danger epc-cs-rm-multi" title="Remove"><i class="fa fa-times"></i></button>
					</div>
					<?php endforeach; ?>
				</div>
				<button type="button" class="btn btn-default btn-xs" id="epc_cs_add_box54"><i class="fa fa-plus"></i> Add payment line</button>
			</div>

			<div class="epc-cs-form-section" id="epc_cs_line_items_section">
				<h5><i class="fa fa-list-ol"></i> Line items â€” boxes 22â€“37B <small class="text-muted">(HS codes, weights, duty per line)</small></h5>
				<p class="text-muted" style="margin:0 0 10px;font-size:12px;">Add one row per HS code line on the customs declaration. HS code, country of origin, and quantity are required per row.</p>
				<input type="hidden" name="line_items_json" id="epc_cs_line_items_json" value="">
				<div class="table-responsive">
					<table class="table table-bordered table-condensed epc-cs-items-table" id="epc_cs_line_items_table">
						<thead><tr>
							<th class="col-line">#</th>
							<th>B22 HS <span class="text-danger">*</span></th>
							<th>B23 Desc</th>
							<th>B24 Origin <span class="text-danger">*</span></th>
							<th>B34 Qty <span class="text-danger">*</span></th>
							<th>B35 Unit</th>
							<th>B25 Fgn val</th>
							<th>B26 Cur</th>
							<th>B28 CIF AED</th>
							<th>B29 D.Rate</th>
							<th>B36 Net kg</th>
							<th>B37 Gross kg</th>
							<th>B32 Pkgs</th>
							<th>B30 Inc</th>
							<th class="col-rm"></th>
						</tr></thead>
						<tbody id="epc_cs_line_items_body"></tbody>
						<tfoot><tr>
							<td colspan="4" class="text-right">Totals</td>
							<td id="epc_cs_tot_qty">0</td>
							<td colspan="4"></td>
							<td id="epc_cs_tot_amt">0.00</td>
							<td colspan="4"></td>
						</tr></tfoot>
					</table>
				</div>
				<button type="button" class="btn btn-default btn-sm" id="epc_cs_add_line_item"><i class="fa fa-plus"></i> Add item</button>
			</div>

			</div><!-- .epc-cs-autofill-panel -->

			<div class="epc-cs-form-actions">
				<button type="submit" class="btn btn-primary btn-lg" id="epc_cs_save_btn_bottom"><i class="fa fa-save"></i> Save custom declaration</button>
				<a class="btn btn-default" href="<?php echo epc_cs_h($baseCsUrl); ?>">Cancel</a>
			</div>
		</form>
		<p class="text-muted" style="margin-top:12px;"><small><?php echo (int) epc_cs_declaration_box_count(); ?> declaration box fields in the PDF section. Green = auto-filled. Manual supplier/PO fields stay at the top.</small></p>
	<?php endif; ?>

<?php else: ?>
	<div class="alert alert-warning">Unknown view.</div>
<?php endif; ?>

</div><!-- .epc-erp-section -->

<div class="epc-cs-pdf-modal" id="epc_cs_pdf_modal_global" aria-hidden="true">
	<div class="epc-cs-pdf-modal__dialog" role="dialog" aria-labelledby="epc_cs_pdf_modal_global_title">
		<div class="epc-cs-pdf-modal__head">
			<strong id="epc_cs_pdf_modal_global_title"><i class="fa fa-file-pdf-o"></i> Declaration PDF</strong>
			<div>
				<a class="btn btn-default btn-xs" id="epc_cs_pdf_modal_global_open" href="#" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open</a>
				<button type="button" class="btn btn-default btn-xs" id="epc_cs_pdf_modal_global_close"><i class="fa fa-times"></i> Close</button>
			</div>
		</div>
		<div class="epc-cs-pdf-modal__body">
			<div class="epc-cs-pdf-modal__empty" id="epc_cs_pdf_modal_global_empty" style="display:none;"><i class="fa fa-file-pdf-o"></i> No PDF attached</div>
			<iframe id="epc_cs_pdf_modal_global_frame" title="Declaration PDF preview"></iframe>
		</div>
	</div>
</div>

<?php
$csFormBoot = array(
	'erpPostUrl' => isset($erpAjaxEndpoint) ? (string) $erpAjaxEndpoint : '',
	'csrf' => isset($csrf) ? (string) $csrf : '',
	'csFrom' => $date_from_str,
	'csTo' => $date_to_str,
	'csDefaultCategory' => $csCategory,
	'csReportsUrl' => $csReportsUrl,
	'lineItemsInitial' => $lineItemsInitial,
	'unitOptions' => $unitOptions,
	'volumeUnitOptions' => $volumeUnitOptions,
);
?>
<script type="application/json" id="epc_cs_form_boot"><?php echo json_encode($csFormBoot, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?></script>
<?php /* epc_custom_shipping_form.js loaded via epc_cp_page_assets (footer, eval-safe) */ ?>
