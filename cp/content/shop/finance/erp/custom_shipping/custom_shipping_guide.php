<?php
/**
 * Custom & Shipping — operator guide content.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';

$csUrls = epc_cs_configure_urls();
$categories = epc_cs_categories_config();
$typeRegistry = epc_cs_declaration_types_registry();
$totalTypes = array_sum(array_map('count', $typeRegistry));
$shellSuffix = '';
if (function_exists('epc_erp_shell_url_query')) {
	$q = epc_erp_shell_url_query();
	if ($q !== '') {
		$shellSuffix = '?' . $q;
	}
}
$csTabUrl = $csUrls['csTabUrl'] . $shellSuffix;
$csReportsUrl = $csUrls['csTabUrl'] . (strpos($csUrls['csTabUrl'], '?') !== false ? '&' : '?') . 'cs_view=reports' . ($shellSuffix !== '' ? '&' . ltrim($shellSuffix, '?') : '');
$erpGuideUrl = $csUrls['erpUrl'] . '/guide' . $shellSuffix;
$reportDefs = epc_cs_report_definitions();

function epc_csg_required_labels($category)
{
	$labels = array();
	foreach (epc_cs_field_definitions($category) as $meta) {
		if (!empty($meta['required'])) {
			$labels[] = (string) $meta['label'];
		}
	}
	return $labels;
}
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260529">
<style>
.epc-csg-intro { background: linear-gradient(135deg, #1e3a5f 0%, #0f766e 100%); color: #fff; border-radius: 8px; padding: 20px 22px; margin-bottom: 18px; }
.epc-csg-intro h3 { margin: 0 0 8px; color: #fff; }
.epc-csg-step { border-left: 4px solid #0f766e; padding: 12px 16px; margin: 14px 0; background: #f8fafc; border-radius: 0 6px 6px 0; }
.epc-csg-step h5 { margin: 0 0 8px; font-weight: 700; }
.epc-csg-cat { margin: 16px 0; padding: 12px 14px; border: 1px solid #e2e8f0; border-radius: 8px; background: #fff; }
.epc-csg-cat h5 { margin-top: 0; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Custom &amp; Shipping — step-by-step guide
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_cs_h($csTabUrl); ?>"><i class="fa fa-ship"></i> Open module</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_cs_h($erpGuideUrl); ?>"><i class="fa fa-briefcase"></i> ERP guide</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-csg-intro">
				<h3><i class="fa fa-book"></i> Customs &amp; logistics declarations</h3>
				<p style="margin:0;opacity:.92;">UAE customs declaration tracking — <?php echo (int) $totalTypes; ?> declaration types from the C&amp;L Excel workbook, dashboard KPIs, category lists, core field capture, and Phase 2 reports (search, cost summary, duty stub, re-export, document expiry).</p>
			</div>

			<div class="alert alert-info">
				<strong>Menu path:</strong> ERP Suite → <em>Custom &amp; Shipping</em> (sidebar group) · or Shop → Finance → ERP → area <code>custom_shipping</code>.
			</div>

			<div class="epc-csg-step">
				<h5>Step 1 — Open the dashboard</h5>
				<p>From CP sidebar: <strong>ERP Suite → Custom &amp; Shipping</strong>. The dashboard shows KPI tiles (total, draft, submitted, cleared), six category cards with live counts, quick-action buttons, and a recent-declarations table when records exist.</p>
			</div>

			<div class="epc-csg-step">
				<h5>Step 2 — Pick a declaration category</h5>
				<p>Click a category card to list declarations in that group, or use a quick action:</p>
				<ul>
					<li><strong>Import</strong> (<?php echo count($typeRegistry['import'] ?? array()); ?> types) — local, FZ, CW, courier, re-export intake</li>
					<li><strong>Export</strong> (<?php echo count($typeRegistry['export'] ?? array()); ?> types) — ROW, FZ, CW, re-export, courier</li>
					<li><strong>Transit</strong> (<?php echo count($typeRegistry['transit'] ?? array()); ?> types) — FZ transit in/out, ROW transit, courier</li>
					<li><strong>Temporary Admission</strong> (<?php echo count($typeRegistry['temp_admission'] ?? array()); ?> types) — ROW, FZ, CW to local</li>
					<li><strong>Transfer</strong> (<?php echo count($typeRegistry['transfer'] ?? array()); ?> types) — CW cargo transfer, FZ internal</li>
					<li><strong>LGP</strong> — warehouse intake form (dedicated fields, not in the 36-type registry)</li>
				</ul>
			</div>

			<div class="epc-csg-step">
				<h5>Step 3 — Choose declaration type</h5>
				<p>On the category list, click a type name to pre-fill the form, or use <strong>New declaration</strong> and select from the dropdown. Quick actions on the dashboard jump straight to common types: Import to Local from ROW, Export from Local to ROW, FZ transit in, and New LGP entry.</p>
			</div>

			<div class="epc-csg-step">
				<h5>Step 4 — Fill required core fields</h5>
				<p>For all customs categories (Import, Export, Transit, Temporary Admission, Transfer): <strong>Company</strong>, <strong>Customs emirate</strong>, <strong>Declaration type</strong>, <strong>Date</strong>, and <strong>Declaration date</strong> are mandatory. Also capture B/L #, SRV #, supplier detail, currency, invoice amount, weights, ports, and INCO terms when available from paperwork.</p>
			</div>

			<div class="epc-csg-step">
				<h5>Step 5 — Category-specific fields</h5>
				<?php foreach (array('import', 'export', 'transit', 'temp_admission', 'transfer', 'lgp') as $catKey): ?>
					<?php
					if ($catKey !== 'lgp' && empty($typeRegistry[$catKey])) {
						continue;
					}
					$catMeta = $categories[$catKey] ?? array('label' => strtoupper($catKey));
					$req = epc_csg_required_labels($catKey);
					?>
					<div class="epc-csg-cat">
						<h5><i class="fa <?php echo epc_cs_h($catMeta['icon'] ?? 'fa-folder'); ?>" style="color:<?php echo epc_cs_h($catMeta['color'] ?? '#64748b'); ?>;"></i> <?php echo epc_cs_h($catMeta['label']); ?></h5>
						<p><strong>Required:</strong> <?php echo epc_cs_h(implode(', ', $req)); ?></p>
						<?php if ($catKey === 'import'): ?>
							<p class="text-muted" style="margin:0;">Also capture supplier code (customs), custom inspection, and ERP PO reference. HS codes and origins go on declaration line items (see Step 6).</p>
						<?php elseif (in_array($catKey, array('export', 'transit', 'temp_admission', 'transfer'), true)): ?>
							<p class="text-muted" style="margin:0;">Also capture import (re-export) declaration ref #, document expiry date, ERP SO reference, customer ref, and customer country where applicable.</p>
						<?php elseif ($catKey === 'lgp'): ?>
							<p class="text-muted" style="margin:0;">LGP is the local gate pass / warehouse intake workflow — no customs declaration type dropdown.</p>
						<?php endif; ?>
					</div>
				<?php endforeach; ?>
			</div>

			<div class="epc-csg-step">
				<h5>Step 6 — Add declaration line items (multiple HS codes)</h5>
				<p>Below the core fields, use the <strong>Declaration line items</strong> grid when a shipment has more than one HS code, country of origin, quantity, volume, or line value — matching item-level columns in the C&amp;L Excel workbook.</p>
				<ul>
					<li>Click <strong>Add item</strong> for each additional line on the customs declaration.</li>
					<li>Per row (required): <strong>HS code</strong>, <strong>Country of origin</strong>, <strong>Quantity</strong>. Optional: description, unit (PCS, KG, etc.), volume (CBM), amount in declaration currency, weight.</li>
					<li>The totals row sums quantity, volume, and amount across all lines. Remove a row with the <i class="fa fa-times"></i> button (at least one row must remain).</li>
					<li>Category lists show an <strong>Items</strong> column with the line count. Open a saved declaration to review the read-only line-item table.</li>
				</ul>
			</div>

			<div class="epc-csg-step">
				<h5>Step 7 — Save draft</h5>
				<p>Click <strong>Save draft</strong> to store the record while paperwork is still in progress. The declaration appears in the category list with status <em>draft</em>. You can reopen and edit until submitted.</p>
			</div>

			<div class="epc-csg-step">
				<h5>Step 8 — Review and submit</h5>
				<p>Open the saved record from the list. Review all captured fields on the read-only view. When filed with UAE customs, click <strong>Submit</strong> to move status to <em>submitted</em>. Cleared status and duty reconciliation arrive in Phase 2.</p>
			</div>

			<h4><i class="fa fa-list"></i> Registered declaration types (<?php echo (int) $totalTypes; ?>)</h4>
			<?php foreach ($categories as $catKey => $catMeta): ?>
				<?php if ($catKey === 'lgp' || empty($typeRegistry[$catKey])) {
					continue;
				} ?>
				<h5><?php echo epc_cs_h($catMeta['label']); ?> (<?php echo count($typeRegistry[$catKey] ?? array()); ?>)</h5>
				<ul>
					<?php foreach ($typeRegistry[$catKey] ?? array() as $t): ?>
						<li><?php echo epc_cs_h($t); ?></li>
					<?php endforeach; ?>
				</ul>
			<?php endforeach; ?>

			<div class="epc-csg-step">
				<h5>Step 9 — Run declaration reports</h5>
				<p>Open <a href="<?php echo epc_cs_h($csReportsUrl); ?>"><strong>Reports</strong></a> from the dashboard panel or toolbar (<code>cs_view=reports</code>). Five reports map to the C&amp;L Excel workbook:</p>
				<table class="table table-bordered table-condensed" style="margin-top:10px;">
					<thead><tr><th>Report</th><th>Status</th><th>Use</th></tr></thead>
					<tbody>
					<?php foreach ($reportDefs as $repKey => $rep): ?>
						<?php
						$st = (string) ($rep['status'] ?? 'partial');
						$badge = $st === 'live' ? 'success' : 'warning';
						$lbl = $st === 'live' ? 'Live' : 'Partial';
						?>
						<tr>
							<td><i class="fa <?php echo epc_cs_h($rep['icon']); ?>"></i> <strong><?php echo epc_cs_h($rep['label']); ?></strong></td>
							<td><span class="label label-<?php echo epc_cs_h($badge); ?>"><?php echo epc_cs_h($lbl); ?></span></td>
							<td><small><?php echo epc_cs_h($rep['desc']); ?></small></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<p style="margin-top:10px;">Workflow: pick a report tile → set filters (date range, category, company, emirate, status, free-text search on declaration search) → <strong>Apply filters</strong> → review table → <strong>Export CSV</strong> or <strong>Print</strong>. Re-export tracking resolves import ref # to the linked declaration when the number is registered.</p>
			</div>

			<div class="alert alert-warning" style="margin-top:18px;">
				<strong>Phase 3 (deferred):</strong> Full Excel Cost Summary (LC/bank charges, marine insurance, 18-line cost breakdown), automated duty calculations, VAT document tracking, company/supplier master lists, ERP PO/SO sync, and Excel formula parity.
			</div>

		</div>
	</div>
</div>
