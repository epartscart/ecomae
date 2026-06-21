<?php
/**
 * Super CP — Custom & Shipping module operator guide.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';

function epc_csg_portal_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = isset($GLOBALS['DP_Config']->backend_dir) ? (string) $GLOBALS['DP_Config']->backend_dir : 'cp';
$typeRegistry = epc_cs_declaration_types_registry();
$totalTypes = array_sum(array_map('count', $typeRegistry));
$reportDefs = epc_cs_report_definitions();
$tenantCpExample = 'https://www.ecomae.com/' . $backend . '/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1';
$tenantReportsExample = $tenantCpExample . '&cs_view=reports';
$guideExample = 'https://www.ecomae.com/' . $backend . '/shop/finance/erp/custom-shipping-guide?epc_erp_shell=1';
$setupUrl = 'https://www.ecomae.com/epc-custom-shipping-setup.php?token=epartscart-deploy-2026';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array('class' => 'epc-csg-portal'));
?>
<div class="epc-csg-portal">
	<div class="hpanel">
		<div class="panel-heading">
			<h2><i class="fa fa-ship"></i> Custom &amp; Shipping (Phase 1 + reports)</h2>
			<p class="text-muted">Deploy and enable the customs declarations module for tenant CP. Based on the C&amp;L Excel format — <?php echo (int) $totalTypes; ?> declaration types plus LGP warehouse intake and five declaration reports.</p>
		</div>
		<div class="panel-body">

			<h3>Deploy module files</h3>
			<p>Push updated ERP files via platform fix, then run the setup endpoint on the tenant site:</p>
			<pre>python tools/push_one.py content/shop/finance/epc_custom_shipping.php
python tools/push_one.py cp/content/shop/finance/erp/erp_tabs_custom_shipping.php
python tools/push_one.py cp/content/shop/finance/erp/custom_shipping/custom_shipping_guide_page.php
python tools/push_one.py cp/content/shop/finance/erp/custom_shipping/custom_shipping_guide.php
python tools/push_one.py cp/content/shop/finance/erp/custom_shipping/custom_shipping_reports.php
python tools/push_one.py content/general_pages/epc_ecomae_platform_capability_guides.php
python tools/push_one.py epc-custom-shipping-setup.php

curl -sk "<?php echo epc_csg_portal_h($setupUrl); ?>"</pre>

			<h3>Operator workflow (tenant CP)</h3>
			<ol class="steps">
				<li><strong>Open ERP → Custom &amp; Shipping</strong> — tenant CP sidebar group or <code>area=custom_shipping</code> tab. Dashboard shows KPI tiles and six category cards.</li>
				<li><strong>Pick category or quick action</strong> — Import (12), Export (13), Transit (6), Temporary Admission (3), Transfer (2), or LGP warehouse intake.</li>
				<li><strong>Choose declaration type</strong> — click a type from the category list or use quick actions for Import ROW, Export ROW, FZ transit in, or LGP.</li>
				<li><strong>Fill required fields</strong> — customs categories need Company, Customs emirate, Declaration type, Date, and Declaration date; LGP uses warehouse intake fields (customer ref, warehouse, packing list, commercial invoice, etc.).</li>
				<li><strong>Declaration line items</strong> — add multiple HS code rows (origin, qty, unit, volume, amount, weight) via the line items grid; required per row: HS code, country of origin, quantity.</li>
				<li><strong>Save draft and submit</strong> — record stays editable with status <em>draft</em> until submitted to UAE customs (<em>submitted</em> → <em>cleared</em>).</li>
				<li><strong>Run reports</strong> — dashboard Reports panel or <code>cs_view=reports</code>: declaration search, cost summary, duty report (partial), re-export tracking, document expiry. Filter, print, export CSV per report.</li>
			</ol>

			<h3>Declaration reports</h3>
			<table class="table table-bordered table-condensed">
				<thead><tr><th>Report</th><th>Status</th><th>Description</th></tr></thead>
				<tbody>
				<?php foreach ($reportDefs as $rep): ?>
					<?php
					$st = (string) ($rep['status'] ?? 'partial');
					$badge = $st === 'live' ? 'success' : 'warning';
					?>
					<tr>
						<td><i class="fa <?php echo epc_csg_portal_h($rep['icon']); ?>"></i> <?php echo epc_csg_portal_h($rep['label']); ?></td>
						<td><span class="label label-<?php echo epc_csg_portal_h($badge); ?>"><?php echo epc_csg_portal_h($st === 'live' ? 'Live' : 'Partial'); ?></span></td>
						<td><small><?php echo epc_csg_portal_h($rep['desc']); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>

			<h3>Tenant URLs (example on ecomae.com)</h3>
			<ul>
				<li>Module dashboard: <a href="<?php echo epc_csg_portal_h($tenantCpExample); ?>" target="_blank" rel="noopener"><?php echo epc_csg_portal_h($tenantCpExample); ?></a></li>
				<li>Reports hub: <a href="<?php echo epc_csg_portal_h($tenantReportsExample); ?>" target="_blank" rel="noopener"><?php echo epc_csg_portal_h($tenantReportsExample); ?></a></li>
				<li>Operator guide: <a href="<?php echo epc_csg_portal_h($guideExample); ?>" target="_blank" rel="noopener"><?php echo epc_csg_portal_h($guideExample); ?></a></li>
			</ul>

			<h3>Marketing capability</h3>
			<p>Listed on <a href="https://www.ecomae.com/platform/capabilities" target="_blank" rel="noopener">ecomae.com/platform/capabilities</a> as <strong>Custom &amp; shipping declarations</strong> (<code>custom-shipping-declarations</code>) with modal guide steps including reports and screenshot <code>guide-custom-shipping.svg</code>.</p>

			<h3>Phase 3 backlog</h3>
			<p>Full Excel Cost Summary breakdown (LC/bank charges, marine insurance), automated duty calculations, VAT document tracking, company/supplier master data, D365 references, and Excel formula parity.</p>

		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
