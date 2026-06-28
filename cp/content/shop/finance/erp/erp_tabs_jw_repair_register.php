<?php
/**
 * Jewellery ERP — Repair Register.
 * Ref: Suntech — master list of all repair jobs with status tracking.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$repairs = epc_jewel_repair_list($db_link, $companyId, date('Y-01-01'), date('Y-m-d'), '');

erp_page_header('<i class="fa fa-list-alt"></i> Repair Register', 'Complete register of all repair jobs with status tracking.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair register'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Register</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print Register</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-file-excel-o"></i> Export</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Filter</span>
			<div class="ef-row">
				<div class="ef-field"><label>From Date</label><input id="rr_from" type="date" value="<?php echo date('Y-m-01'); ?>"></div>
				<div class="ef-field"><label>To Date</label><input id="rr_to" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Status</label>
					<select id="rr_status"><option value="">All</option><option value="Received">Received</option><option value="In Progress">In Progress</option><option value="Workshop">Workshop</option><option value="Ready">Ready</option><option value="Delivered">Delivered</option></select>
				</div>
				<div class="ef-field"><label>Branch</label>
					<select id="rr_branch"><option value="">All</option><option value="HO">HO</option></select>
				</div>
			</div>
		</div>

		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Job No</th><th>Date</th><th>Customer</th><th>Mobile</th>
				<th>Item</th><th>Repair Type</th><th>Metal</th><th>Gross Wt</th>
				<th>Est. Charge</th><th>Advance</th><th>Promise Date</th><th>Status</th>
			</tr></thead>
			<tbody>
			<?php if (empty($repairs)): ?>
				<tr><td colspan="13" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($repairs as $r): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($r['job_no']); ?></strong></td>
					<td><?php echo epc_erp_h($r['receipt_date']); ?></td>
					<td><?php echo epc_erp_h($r['customer_name']); ?></td>
					<td><?php echo epc_erp_h($r['customer_mobile']); ?></td>
					<td><?php echo epc_erp_h($r['item_description']); ?></td>
					<td><?php echo epc_erp_h($r['repair_type']); ?></td>
					<td><?php echo epc_erp_h($r['metal']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$r['gross_wt'], 3); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$r['est_charge'], 2); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$r['advance_amount'], 2); ?></td>
					<td><?php echo epc_erp_h($r['promise_date']); ?></td>
					<td><?php
						$st = $r['status'] ?? 'Received';
						$clr = ($st === 'Delivered') ? 'green' : (($st === 'Ready') ? 'blue' : (($st === 'Workshop') ? 'orange' : '#333'));
						echo '<span style="color:'.$clr.';font-weight:bold">' . epc_erp_h($st) . '</span>';
					?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div class="ef-totals">
			<div class="ef-row">
				<div class="ef-field"><label>Total Jobs</label><input value="<?php echo count($repairs); ?>" readonly></div>
				<div class="ef-field"><label>Received</label><input value="<?php echo count(array_filter($repairs, function($r){ return ($r['status'] ?? '') === 'Received'; })); ?>" readonly></div>
				<div class="ef-field"><label>In Progress</label><input value="<?php echo count(array_filter($repairs, function($r){ return ($r['status'] ?? '') === 'In Progress'; })); ?>" readonly></div>
				<div class="ef-field"><label>Ready</label><input value="<?php echo count(array_filter($repairs, function($r){ return ($r['status'] ?? '') === 'Ready'; })); ?>" readonly></div>
				<div class="ef-field"><label>Delivered</label><input value="<?php echo count(array_filter($repairs, function($r){ return ($r['status'] ?? '') === 'Delivered'; })); ?>" readonly></div>
			</div>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=REGISTER</span>
		<span>Repair Register &mdash; All Jobs</span>
	</div>
</div>
