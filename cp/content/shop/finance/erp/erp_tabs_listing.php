<?php
/**
 * Module: Listing.
 * Sub-modules: prepare a resource listing and attach it to a voucher.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-list-alt"></i> Listing</h3>';
echo '<p class="text-muted">Prepare a resource listing (item / service / asset line) and attach it to a voucher reference (PO, SO, GRN, journal). Per-tenant and configurable.</p></div>';

try {
	$listings = epc_erp_pm_listings_list($db_link);
} catch (Exception $e) {
	$listings = array();
}

echo '<div class="epc-erp-section pm-section"><h4><i class="fa fa-plus"></i> Prepare listing</h4>';
echo '<form class="pm-form epc-erp-pm-listing-form">';
echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
echo '<div class="pm-fields">';
echo '<div class="pm-field"><label>Resource type</label><select name="resource_type" class="form-control input-sm"><option value="item">Item</option><option value="service">Service</option><option value="asset">Asset</option><option value="labour">Labour</option></select></div>';
echo '<div class="pm-field"><label>Title</label><input type="text" name="title" class="form-control input-sm" required></div>';
echo '<div class="pm-field"><label>Qty</label><input type="number" step="any" name="qty" class="form-control input-sm" value="1"></div>';
echo '<div class="pm-field"><label>Rate</label><input type="number" step="any" name="rate" class="form-control input-sm" value="0"></div>';
echo '<div class="pm-field"><label>Voucher ref (optional)</label><input type="text" name="voucher_ref" class="form-control input-sm" placeholder="PO-2026-00001"></div>';
echo '<div class="pm-field" style="flex:1 1 100%"><label>Description</label><input type="text" name="description" class="form-control input-sm"></div>';
echo '<div class="pm-field pm-field--btn"><label>&nbsp;</label><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Add listing</button></div>';
echo '</div></form></div>';

echo '<div class="epc-erp-section pm-section"><h4><i class="fa fa-list"></i> Listings <span class="badge">' . count($listings) . '</span></h4>';
if (empty($listings)) {
	echo '<p class="text-muted">No listings yet.</p>';
} else {
	echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Ref</th><th>Type</th><th>Title</th><th>Qty</th><th>Rate</th><th>Amount</th><th>Voucher</th><th>Status</th></tr></thead><tbody>';
	foreach ($listings as $r) {
		echo '<tr>';
		echo '<td>' . epc_erp_h((string) $r['ref_no']) . '</td>';
		echo '<td>' . epc_erp_h((string) $r['resource_type']) . '</td>';
		echo '<td>' . epc_erp_h((string) $r['title']) . '</td>';
		echo '<td>' . epc_erp_h(rtrim(rtrim(number_format((float) $r['qty'], 3), '0'), '.')) . '</td>';
		echo '<td>' . epc_erp_money((float) $r['rate']) . '</td>';
		echo '<td>' . epc_erp_money((float) $r['amount']) . '</td>';
		$vref = (string) $r['voucher_ref'];
		echo '<td>' . ($vref !== '' ? epc_erp_h($vref) : '<span class="text-muted">—</span>') . '</td>';
		$st = (string) $r['status'];
		$lbl = $st === 'attached' ? 'label-success' : ($st === 'draft' ? 'label-default' : 'label-info');
		echo '<td><span class="label ' . $lbl . '">' . epc_erp_h(ucfirst($st)) . '</span></td>';
		echo '</tr>';
	}
	echo '</tbody></table></div>';
}
echo '</div>';
