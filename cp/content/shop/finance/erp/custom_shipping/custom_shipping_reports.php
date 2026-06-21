<?php
/**
 * Custom & Shipping — Phase 2 reports (search, cost summary, duty stub, re-export, document expiry).
 */
defined('_ASTEXE_') or die('No access');

$csReportKey = isset($csReportKey) ? (string) $csReportKey : '';
$csReportFilters = isset($csReportFilters) && is_array($csReportFilters) ? $csReportFilters : epc_cs_report_filters_from_request($_GET);
$reportDefs = epc_cs_report_definitions();
$categories = epc_cs_categories_config();
$typeRegistry = epc_cs_declaration_types_registry();
$emirates = epc_cs_field_definitions('import')['customs_emirate']['options'] ?? array();

if ($csReportKey === '' || !isset($reportDefs[$csReportKey])) {
	$registryUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'reports', 'cs_report' => 'search_results'));
	?>
	<p><a href="<?php echo epc_cs_h($baseCsUrl); ?>">&larr; Dashboard</a></p>
	<div class="epc-scp-panel__hero" style="margin-bottom:14px;">
		<h4 style="margin:0 0 4px;font-size:18px;font-weight:700;color:#fff;"><i class="fa fa-bar-chart"></i> Custom &amp; Shipping reports</h4>
		<p style="margin:0;font-size:13px;opacity:.92;">Declaration registry, cost summary, duty, re-export tracking, and document expiry.</p>
	</div>
	<p style="margin-bottom:14px;">
		<a class="btn btn-primary btn-sm" href="<?php echo epc_cs_h($registryUrl); ?>"><i class="fa fa-list"></i> Open declaration registry</a>
		<span class="text-muted" style="margin-left:8px;font-size:12px;">Main management hub — edit, delete, view PDF copies</span>
	</p>
	<div class="epc-erp-report-grid">
		<?php foreach ($reportDefs as $key => $meta): ?>
			<?php
			$badge = ($meta['status'] ?? '') === 'live' ? 'success' : 'warning';
			$badgeLbl = ($meta['status'] ?? '') === 'live' ? 'Live' : 'Partial';
			?>
			<div class="epc-erp-report-tile">
				<h5><i class="fa <?php echo epc_cs_h($meta['icon']); ?>"></i> <?php echo epc_cs_h($meta['label']); ?>
					<span class="label label-<?php echo epc_cs_h($badge); ?>" style="font-size:10px;vertical-align:middle;"><?php echo epc_cs_h($badgeLbl); ?></span>
				</h5>
				<p class="text-muted"><?php echo epc_cs_h($meta['desc']); ?></p>
				<a class="btn btn-primary btn-sm" href="<?php echo epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'reports', 'cs_report' => $key))); ?>"><i class="fa fa-table"></i> Open report</a>
			</div>
		<?php endforeach; ?>
	</div>
	<div class="alert alert-info" style="margin-top:16px;">
		<strong>Excel parity:</strong> Cost Summary LC/bank/charge breakdown, duty paid/payable calculations, VAT document tracking, and supplier/shipment search macros remain Phase 3.
	</div>
	<?php
	return;
}

$repMeta = $reportDefs[$csReportKey];
$reportData = epc_cs_report_run($db_link, $csReportKey, $csReportFilters);
$exportUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array_merge(
	array('cs_view' => 'reports', 'cs_report' => $csReportKey, 'cs_export' => 'csv'),
	array_filter(array(
		'cs_category' => $csReportFilters['category'] ?? '',
		'cs_status' => $csReportFilters['status'] ?? '',
		'cs_type' => $csReportFilters['declaration_type'] ?? '',
		'cs_company' => $csReportFilters['company'] ?? '',
		'cs_emirate' => $csReportFilters['customs_emirate'] ?? '',
		'cs_q' => $csReportFilters['q'] ?? '',
		'cs_expiry_days' => !empty($csReportFilters['expiry_days']) ? (int) $csReportFilters['expiry_days'] : '',
	))
));
$reportsHubUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'reports'));
$managementUrl = epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'reports', 'cs_report' => 'search_results'));
$allTypes = array();
foreach ($typeRegistry as $catTypes) {
	foreach ($catTypes as $t) {
		$allTypes[] = $t;
	}
}
sort($allTypes);
?>
<style>
@media print {
	.epc-erp-sidebar, .epc-erp-content-toolbar, .epc-erp-filter-bar, .epc-cs-report-actions, .epc-erp-context-banner, .epc-cs-report-filters form button, .epc-cs-pdf-modal { display: none !important; }
	.epc-erp-content-body { padding: 0; }
}
.epc-cs-report-actions { margin: 10px 0 14px; display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.epc-cs-report-count { font-size: 12px; color: #64748b; margin-left: auto; }
.epc-cs-pdf-modal { display: none; position: fixed; inset: 0; z-index: 10050; background: rgba(15, 23, 42, 0.55); padding: 24px; align-items: center; justify-content: center; }
.epc-cs-pdf-modal.is-open { display: flex !important; align-items: center; justify-content: center; }
.epc-cs-pdf-modal__empty { padding: 48px 24px; text-align: center; color: #64748b; font-size: 14px; }
.epc-cs-pdf-modal__dialog { width: min(960px, 100%); max-height: calc(100vh - 48px); background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 24px 48px rgba(15, 23, 42, 0.25); display: flex; flex-direction: column; }
.epc-cs-pdf-modal__head { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 16px; background: #f1f5f9; border-bottom: 1px solid #e2e8f0; }
.epc-cs-pdf-modal__head strong { font-size: 14px; color: #0f172a; }
.epc-cs-pdf-modal__body { flex: 1; min-height: 0; }
.epc-cs-pdf-modal__body iframe { width: 100%; height: min(72vh, 640px); border: 0; display: block; background: #f8fafc; }
</style>

<p><a href="<?php echo epc_cs_h($reportsHubUrl); ?>">&larr; All reports</a> · <a href="<?php echo epc_cs_h($baseCsUrl); ?>">Dashboard</a></p>
<div class="epc-scp-panel__hero" style="margin-bottom:14px;">
	<h4 style="margin:0 0 4px;font-size:18px;font-weight:700;color:#fff;"><i class="fa <?php echo epc_cs_h($repMeta['icon']); ?>"></i> <?php echo epc_cs_h($repMeta['label']); ?></h4>
	<p style="margin:0;font-size:13px;opacity:.92;"><?php echo epc_cs_h($repMeta['desc']); ?></p>
</div>

<div class="epc-scp-filter-bar epc-cs-report-filters">
	<form method="get" class="form-inline">
		<input type="hidden" name="area" value="custom_shipping">
		<input type="hidden" name="tab" value="custom_shipping">
		<input type="hidden" name="cs_view" value="reports">
		<input type="hidden" name="cs_report" value="<?php echo epc_cs_h($csReportKey); ?>">
		<?php if (!empty($_GET['epc_erp_shell'])): ?><input type="hidden" name="epc_erp_shell" value="1"><?php endif; ?>
		<label>From</label>
		<input type="date" name="from" class="form-control input-sm" value="<?php echo epc_cs_h($date_from_str); ?>">
		<label>To</label>
		<input type="date" name="to" class="form-control input-sm" value="<?php echo epc_cs_h($date_to_str); ?>">
		<?php if ($csReportKey !== 'reexport_tracking'): ?>
		<label>Category</label>
		<select name="cs_category" class="form-control input-sm">
			<option value="">All</option>
			<?php foreach ($categories as $ck => $cm): ?>
				<option value="<?php echo epc_cs_h($ck); ?>"<?php echo (($csReportFilters['category'] ?? '') === $ck) ? ' selected' : ''; ?>><?php echo epc_cs_h($cm['label']); ?></option>
			<?php endforeach; ?>
		</select>
		<?php endif; ?>
		<label>Status</label>
		<select name="cs_status" class="form-control input-sm">
			<option value="">All</option>
			<?php foreach (array('draft', 'submitted', 'cleared') as $st): ?>
				<option value="<?php echo epc_cs_h($st); ?>"<?php echo (($csReportFilters['status'] ?? '') === $st) ? ' selected' : ''; ?>><?php echo epc_cs_h(ucfirst($st)); ?></option>
			<?php endforeach; ?>
		</select>
		<label>Emirate</label>
		<select name="cs_emirate" class="form-control input-sm">
			<option value="">All</option>
			<?php foreach ($emirates as $em): ?>
				<option value="<?php echo epc_cs_h($em); ?>"<?php echo (($csReportFilters['customs_emirate'] ?? '') === $em) ? ' selected' : ''; ?>><?php echo epc_cs_h($em); ?></option>
			<?php endforeach; ?>
		</select>
		<label>Company</label>
		<input type="text" name="cs_company" class="form-control input-sm" value="<?php echo epc_cs_h($csReportFilters['company'] ?? ''); ?>" placeholder="Company name">
		<?php if ($csReportKey === 'search_results'): ?>
			<label>Search</label>
			<input type="text" name="cs_q" class="form-control input-sm" value="<?php echo epc_cs_h($csReportFilters['q'] ?? ''); ?>" placeholder="Decl #, supplier, SRV, B/L">
			<label>Type</label>
			<select name="cs_type" class="form-control input-sm">
				<option value="">All types</option>
				<?php foreach ($allTypes as $t): ?>
					<option value="<?php echo epc_cs_h($t); ?>"<?php echo (($csReportFilters['declaration_type'] ?? '') === $t) ? ' selected' : ''; ?>><?php echo epc_cs_h($t); ?></option>
				<?php endforeach; ?>
			</select>
		<?php endif; ?>
		<?php if ($csReportKey === 'document_expiry'): ?>
			<label>Within days</label>
			<input type="number" name="cs_expiry_days" class="form-control input-sm" value="<?php echo (int) ($csReportFilters['expiry_days'] ?? 30); ?>" min="1" max="365" style="width:70px;">
		<?php endif; ?>
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-filter"></i> Apply filters</button>
	</form>
</div>

<div class="epc-cs-report-actions">
	<a class="btn btn-default btn-sm" href="<?php echo epc_cs_h($exportUrl); ?>"><i class="fa fa-download"></i> Export CSV</a>
	<button type="button" class="btn btn-default btn-sm" onclick="window.print();"><i class="fa fa-print"></i> Print</button>
	<?php if ($csReportKey !== 'search_results'): ?>
		<a class="btn btn-info btn-sm" href="<?php echo epc_cs_h($managementUrl); ?>"><i class="fa fa-list"></i> Declaration registry</a>
	<?php endif; ?>
</div>

<?php if ($csReportKey === 'search_results'): ?>
	<?php $rows = is_array($reportData) ? $reportData : array(); ?>
	<div class="epc-scp-table-card">
		<div style="display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:12px;">
			<h4 style="margin:0;"><i class="fa fa-list-alt"></i> Saved declarations</h4>
			<span class="epc-cs-report-count"><?php echo count($rows); ?> record<?php echo count($rows) === 1 ? '' : 's'; ?></span>
		</div>
		<div class="table-responsive">
	<table class="table table-striped table-bordered table-condensed epc-scp-data-table epc-cs-report-table">
		<thead><tr>
			<th>ID</th><th>Category</th><th>Type</th><th>Company</th><th>Emirate</th><th>Entry</th><th>Decl #</th><th>SRV #</th><th>Status</th><th>Invoice AED</th><th>Items</th><th></th>
		</tr></thead>
		<tbody>
		<?php if (empty($rows)): ?>
			<tr><td colspan="12" class="text-muted text-center">No declarations match filters.</td></tr>
		<?php else: ?>
			<?php foreach ($rows as $r): ?>
				<?php $catLbl = $categories[$r['category'] ?? '']['label'] ?? ($r['category'] ?? ''); ?>
				<tr>
					<td><strong>#<?php echo (int) $r['id']; ?></strong></td>
					<td><?php echo epc_cs_h($catLbl); ?></td>
					<td><span class="text-muted small"><?php echo epc_cs_h($r['declaration_type']); ?></span></td>
					<td><?php echo epc_cs_h($r['company']); ?></td>
					<td><?php echo epc_cs_h($r['customs_emirate']); ?></td>
					<td><?php echo epc_cs_h($r['entry_date'] ?: '—'); ?></td>
					<td><strong><?php echo epc_cs_h($r['declaration_number'] ?: '—'); ?></strong></td>
					<td><?php echo epc_cs_h($r['srv_number'] ?: '—'); ?></td>
					<td><?php echo epc_cs_status_badge_html($r['status'] ?? 'draft'); ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['invoice_amount_aed'], 2)); ?></td>
					<td><?php echo (int) ($r['item_count'] ?? 0); ?></td>
					<td class="text-right"><?php echo epc_cs_declaration_row_actions_html($erpUrl, $date_from_str, $date_to_str, $r); ?></td>
				</tr>
			<?php endforeach; ?>
		<?php endif; ?>
		</tbody>
	</table>
		</div>
	</div>
	<p class="text-muted"><small>Management hub — edit, delete, or view PDF copies. Excel: Search data by declaration #, supplier, shipment no. (SRV).</small></p>

	<div class="epc-cs-pdf-modal" id="epc_cs_pdf_modal" aria-hidden="true">
		<div class="epc-cs-pdf-modal__dialog" role="dialog" aria-labelledby="epc_cs_pdf_modal_title">
			<div class="epc-cs-pdf-modal__head">
				<strong id="epc_cs_pdf_modal_title"><i class="fa fa-file-pdf-o"></i> Declaration PDF</strong>
				<div>
					<a class="btn btn-default btn-xs" id="epc_cs_pdf_modal_open" href="#" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open</a>
					<button type="button" class="btn btn-default btn-xs" id="epc_cs_pdf_modal_close"><i class="fa fa-times"></i> Close</button>
				</div>
			</div>
			<div class="epc-cs-pdf-modal__body">
				<div class="epc-cs-pdf-modal__empty" id="epc_cs_pdf_modal_empty" style="display:none;"><i class="fa fa-file-pdf-o"></i> No PDF attached</div>
				<iframe id="epc_cs_pdf_modal_frame" title="Declaration PDF preview"></iframe>
			</div>
		</div>
	</div>

<?php elseif ($csReportKey === 'cost_summary'): ?>
	<?php $sumRows = $reportData['rows'] ?? array(); $tot = $reportData['totals'] ?? array(); ?>
	<div class="epc-scp-table-card">
	<div class="table-responsive">
	<table class="table table-striped table-bordered table-condensed epc-scp-data-table epc-cs-report-table">
		<thead><tr>
			<th>Category</th><th>Company</th><th>Emirate</th><th>Declarations</th><th>Invoice AED</th><th>Total cost AED</th><th>Line items AED</th><th>Line qty</th>
		</tr></thead>
		<tbody>
		<?php if (empty($sumRows)): ?>
			<tr><td colspan="8" class="text-muted text-center">No data in this period.</td></tr>
		<?php else: ?>
			<?php foreach ($sumRows as $r): ?>
				<?php $catLbl = $categories[$r['category'] ?? '']['label'] ?? ($r['category'] ?? ''); ?>
				<tr>
					<td><?php echo epc_cs_h($catLbl); ?></td>
					<td><?php echo epc_cs_h($r['company']); ?></td>
					<td><?php echo epc_cs_h($r['customs_emirate']); ?></td>
					<td><?php echo (int) $r['decl_count']; ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['sum_invoice_aed'], 2)); ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['sum_total_cost_aed'], 2)); ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['sum_line_amount_aed'], 2)); ?></td>
					<td><?php echo epc_cs_h($r['sum_line_qty']); ?></td>
				</tr>
			<?php endforeach; ?>
		<?php endif; ?>
		</tbody>
		<?php if (!empty($sumRows)): ?>
		<tfoot><tr style="font-weight:700;background:#eef2ff;">
			<td colspan="3" class="text-right">Totals</td>
			<td><?php echo (int) ($tot['decl_count'] ?? 0); ?></td>
			<td><?php echo epc_cs_h(number_format((float) ($tot['sum_invoice_aed'] ?? 0), 2)); ?></td>
			<td><?php echo epc_cs_h(number_format((float) ($tot['sum_total_cost_aed'] ?? 0), 2)); ?></td>
			<td><?php echo epc_cs_h(number_format((float) ($tot['sum_line_amount_aed'] ?? 0), 2)); ?></td>
			<td></td>
		</tr></tfoot>
		<?php endif; ?>
	</table>
	</div>
	</div>
	<p class="text-muted"><small>Partial Excel Cost Summary: company, SRV, supplier, invoice and total cost. LC/bank charges, marine insurance, and 18-line cost breakdown deferred to Phase 3.</small></p>

<?php elseif ($csReportKey === 'duty_report'): ?>
	<?php $rows = is_array($reportData) ? $reportData : array(); ?>
	<div class="alert alert-warning"><strong>Phase 2 stub:</strong> Duty paid / payable / date columns show stored values when present; automated duty calculation from CIF and HS tariff is Phase 3.</div>
	<div class="epc-scp-table-card">
	<div class="table-responsive">
	<table class="table table-striped table-bordered table-condensed epc-scp-data-table epc-cs-report-table">
		<thead><tr>
			<th>Decl #</th><th>Company</th><th>Type</th><th>Entry</th><th>Line</th><th>HS code</th><th>Origin</th><th>Qty</th><th>Line AED</th><th>Invoice AED</th><th>Total cost AED</th><th>Duty paid</th><th>Duty payable</th><th>Payable date</th>
		</tr></thead>
		<tbody>
		<?php if (empty($rows)): ?>
			<tr><td colspan="14" class="text-muted text-center">No line items in this period.</td></tr>
		<?php else: ?>
			<?php foreach ($rows as $r): ?>
				<tr>
					<td><?php echo epc_cs_h($r['declaration_number'] ?: ('#' . (int) $r['id'])); ?></td>
					<td><?php echo epc_cs_h($r['company']); ?></td>
					<td><?php echo epc_cs_h($r['declaration_type']); ?></td>
					<td><?php echo epc_cs_h($r['entry_date'] ?: '—'); ?></td>
					<td><?php echo (int) $r['line_number']; ?></td>
					<td><?php echo epc_cs_h($r['hs_code']); ?></td>
					<td><?php echo epc_cs_h($r['country_of_origin']); ?></td>
					<td><?php echo epc_cs_h($r['quantity']); ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['line_amount'], 2)); ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['invoice_amount_aed'], 2)); ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['total_cost_aed'], 2)); ?></td>
					<td><?php echo epc_cs_h(epc_cs_field_data_val($r, 'custom_duty_paid', '—')); ?></td>
					<td><?php echo epc_cs_h(epc_cs_field_data_val($r, 'custom_duty_payable', '—')); ?></td>
					<td><?php echo epc_cs_h(epc_cs_field_data_val($r, 'duty_payable_date', '—')); ?></td>
				</tr>
			<?php endforeach; ?>
		<?php endif; ?>
		</tbody>
	</table>
	</div>
	</div>
	<p class="text-muted"><small>Excel: Duty paid report, Duty payable report, Duty payable list with date.</small></p>

<?php elseif ($csReportKey === 'reexport_tracking'): ?>
	<?php $rows = is_array($reportData) ? $reportData : array(); ?>
	<div class="epc-scp-table-card">
	<div class="table-responsive">
	<table class="table table-striped table-bordered table-condensed epc-scp-data-table epc-cs-report-table">
		<thead><tr>
			<th>ID</th><th>Flow</th><th>Type</th><th>Company</th><th>Entry</th><th>Decl #</th><th>Import ref #</th><th>Linked import</th><th>Expiry</th><th>Status</th><th>Invoice AED</th><th></th>
		</tr></thead>
		<tbody>
		<?php if (empty($rows)): ?>
			<tr><td colspan="12" class="text-muted text-center">No re-export or import-for-re-export declarations in this period.</td></tr>
		<?php else: ?>
			<?php foreach ($rows as $r): ?>
				<tr>
					<td><?php echo (int) $r['id']; ?></td>
					<td><?php echo !empty($r['is_reexport']) ? '<span class="label label-success">Re-export</span>' : '<span class="label label-info">Import for re-export</span>'; ?></td>
					<td><?php echo epc_cs_h($r['declaration_type']); ?></td>
					<td><?php echo epc_cs_h($r['company']); ?></td>
					<td><?php echo epc_cs_h($r['entry_date'] ?: '—'); ?></td>
					<td><?php echo epc_cs_h($r['declaration_number'] ?: '—'); ?></td>
					<td><?php echo epc_cs_h($r['import_ref'] ?: '—'); ?></td>
					<td><?php
						if (!empty($r['import_link']['id'])) {
							echo '<a href="' . epc_cs_h(epc_cs_tab_url($erpUrl, $date_from_str, $date_to_str, array('cs_view' => 'view', 'cs_id' => (int) $r['import_link']['id']))) . '">#' . (int) $r['import_link']['id'] . '</a>';
						} else {
							echo epc_cs_h($r['import_ref'] ? 'Not in registry' : '—');
						}
					?></td>
					<td><?php echo epc_cs_h($r['document_expiry_date'] ?: '—'); ?></td>
					<td><?php echo epc_cs_status_badge_html($r['status'] ?? 'draft'); ?></td>
					<td><?php echo epc_cs_h(number_format((float) $r['invoice_amount_aed'], 2)); ?></td>
					<td class="text-right"><?php echo epc_cs_declaration_row_actions_html($erpUrl, $date_from_str, $date_to_str, $r); ?></td>
				</tr>
			<?php endforeach; ?>
		<?php endif; ?>
		</tbody>
	</table>
	</div>
	</div>
	<p class="text-muted"><small>Excel: Re-export report, Re-export pending with expiry date.</small></p>

<?php elseif ($csReportKey === 'document_expiry'): ?>
	<?php $rows = is_array($reportData) ? $reportData : array(); ?>
	<div class="epc-scp-table-card">
	<div class="table-responsive">
	<table class="table table-striped table-bordered table-condensed epc-scp-data-table epc-cs-report-table">
		<thead><tr>
			<th>ID</th><th>Category</th><th>Company</th><th>Type</th><th>Decl #</th><th>Expiry date</th><th>Days</th><th>Status</th><th>Emirate</th><th>Actions</th>
		</tr></thead>
		<tbody>
		<?php if (empty($rows)): ?>
			<tr><td colspan="10" class="text-muted text-center">No overdue or upcoming document expiries (within <?php echo (int) ($csReportFilters['expiry_days'] ?? 30); ?> days).</td></tr>
		<?php else: ?>
			<?php foreach ($rows as $r): ?>
				<?php $catLbl = $categories[$r['category'] ?? '']['label'] ?? ($r['category'] ?? ''); ?>
				<tr class="<?php echo ($r['expiry_status'] ?? '') === 'overdue' ? 'danger' : 'warning'; ?>">
					<td><?php echo (int) $r['id']; ?></td>
					<td><?php echo epc_cs_h($catLbl); ?></td>
					<td><?php echo epc_cs_h($r['company']); ?></td>
					<td><?php echo epc_cs_h($r['declaration_type']); ?></td>
					<td><?php echo epc_cs_h($r['declaration_number'] ?: '—'); ?></td>
					<td><?php echo epc_cs_h($r['document_expiry_date']); ?></td>
					<td><?php echo (int) ($r['days_until_expiry'] ?? 0); ?></td>
					<td><span class="label label-<?php echo ($r['expiry_status'] ?? '') === 'overdue' ? 'danger' : 'warning'; ?>"><?php echo epc_cs_h($r['expiry_status']); ?></span></td>
					<td><?php echo epc_cs_h($r['customs_emirate']); ?></td>
					<td class="text-right"><?php echo epc_cs_declaration_row_actions_html($erpUrl, $date_from_str, $date_to_str, $r); ?></td>
				</tr>
			<?php endforeach; ?>
		<?php endif; ?>
		</tbody>
	</table>
	</div>
	</div>
	<p class="text-muted"><small>Excel: Document submission expiry, VAT document tracking (VAT stamping fields Phase 3).</small></p>
<?php endif; ?>
