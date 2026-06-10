<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$enabled = epc_erp_multi_entity_enabled($db_link);

erp_page_header(
	'<i class="fa fa-sitemap"></i> Multi-entity',
	'Platform placeholder for inter-company and consolidated reporting (Super CP only).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Multi-entity'),
	)
);
?>
<div class="epc-erp-section-card">
	<div class="epc-erp-section-card-bd">
		<div class="alert alert-info">
			<strong>Inter-company / multi-entity</strong> is a platform-level feature for operators on
			<a href="https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub" target="_blank" rel="noopener">ecomae Super CP</a>.
			Tenant sites run as a single legal entity by default.
		</div>
		<form id="epc_erp_form_multi_entity" class="form-horizontal" style="max-width:560px;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-group">
				<label class="col-sm-5">Enable multi-entity mode (stub)</label>
				<div class="col-sm-7">
					<label class="checkbox-inline"><input type="checkbox" name="enabled" value="1"<?php echo $enabled ? ' checked' : ''; ?>> Placeholder toggle</label>
				</div>
			</div>
			<div class="form-group"><div class="col-sm-offset-5 col-sm-7"><button type="submit" class="btn btn-default btn-sm">Save preference</button></div></div>
		</form>
		<div class="epc-erp-coming-soon" style="margin-top:20px;padding:28px;">
			<i class="fa fa-building-o"></i>
			<h4>Consolidation UI shell</h4>
			<p class="text-muted">Entity switcher, inter-company eliminations, and group P&amp;L will appear here when enabled for your tenant pack.</p>
		</div>
	</div>
</div>
