<?php
/**
 * Module: Business Unit (enterprise style).
 * Sub-modules: Business units, Class units, Legal entities, Financial
 * dimensions (+ values), Cost centres, Listing reference.
 * All per-tenant, config-driven via epc_erp_pm_* masters.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_costing.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'business_units';
$subs = array(
	'business_units' => 'Business units',
	'class_units' => 'Class units',
	'legal_entities' => 'Legal entities',
	'dimensions' => 'Financial dimensions',
	'cost_centres' => 'Cost centres',
);

echo '<div class="epc-erp-section">';
echo '<h3 style="margin-top:0;"><i class="fa fa-sitemap"></i> Business Unit</h3>';
echo '<p class="text-muted">Organisational backbone — legal entities, business units, class units, financial dimensions and cost centres. Every record is stored in this tenant\'s own database and is fully configurable.</p>';
echo '</div>';

epc_erp_pm_module_tabs($erpUrl, 'business_units', 'enterprise', $date_from_str, $date_to_str, $subs, $view);

// Build legal-entity options for the BU form.
$leOpts = array('0' => '— none —');
try {
	foreach (epc_erp_pm_list($db_link, 'epc_erp_pm_legal_entities', true) as $le) {
		$leOpts[(string) $le['id']] = $le['code'] . ' · ' . $le['name'];
	}
} catch (Exception $e) {
}

switch ($view) {
	case 'class_units':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_class_units', 'Class units',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'e.g. DIV-A'),
				array('name' => 'name', 'label' => 'Name', 'required' => true, 'placeholder' => 'Division / class name'),
				array('name' => 'class_type', 'label' => 'Class type', 'type' => 'select', 'options' => array('division' => 'Division', 'segment' => 'Segment', 'region' => 'Region', 'channel' => 'Channel')),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'class_type', 'label' => 'Type'), array('key' => 'note', 'label' => 'Note')),
			'fa-object-group');
		break;

	case 'legal_entities':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_legal_entities', 'Legal entities',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'e.g. LE-AE'),
				array('name' => 'name', 'label' => 'Legal name', 'required' => true),
				array('name' => 'country_code', 'label' => 'Country', 'placeholder' => 'AE'),
				array('name' => 'currency_code', 'label' => 'Currency', 'placeholder' => 'AED'),
				array('name' => 'trn', 'label' => 'TRN / Tax ID'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Legal name'), array('key' => 'country_code', 'label' => 'Country'), array('key' => 'currency_code', 'label' => 'Currency'), array('key' => 'trn', 'label' => 'TRN')),
			'fa-bank');
		break;

	case 'dimensions':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_dimensions', 'Financial dimensions',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'e.g. DEPT'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'dim_type', 'label' => 'Type', 'type' => 'select', 'options' => array('department' => 'Department', 'project' => 'Project', 'cost_center' => 'Cost centre', 'region' => 'Region', 'custom' => 'Custom')),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'dim_type', 'label' => 'Type')),
			'fa-tags');

		// Dimension values, keyed by dimension.
		$dimOpts = array();
		foreach (epc_erp_pm_list($db_link, 'epc_erp_pm_dimensions', true) as $d) {
			$dimOpts[(string) $d['id']] = $d['code'] . ' · ' . $d['name'];
		}
		echo '<hr>';
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_dimension_values', 'Dimension values',
			array(
				array('name' => 'dimension_id', 'label' => 'Dimension', 'type' => 'select', 'options' => $dimOpts, 'required' => true),
				array('name' => 'code', 'label' => 'Value code', 'required' => true),
				array('name' => 'name', 'label' => 'Value name', 'required' => true),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'dimension_id', 'label' => 'Dim #'), array('key' => 'code', 'label' => 'Value code'), array('key' => 'name', 'label' => 'Value name')),
			'fa-tag');
		break;

	case 'cost_centres':
		// Cost centres use the existing tested epc_cc_* engine.
		try {
			epc_cc_ensure_schema($db_link);
			$ccRows = $db_link->query('SELECT * FROM `epc_cc_centers` ORDER BY `code`')->fetchAll(PDO::FETCH_ASSOC) ?: array();
		} catch (Exception $e) {
			$ccRows = array();
		}
		echo '<div class="epc-erp-section pm-section">';
		echo '<h4><i class="fa fa-building"></i> Cost centres <span class="badge">' . count($ccRows) . '</span></h4>';
		echo '<form class="pm-form epc-erp-pm-form" data-pm-table="">';
		echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
		echo '<input type="hidden" name="action" value="cc_save_disabled">';
		echo '</form>';
		echo '<p class="text-muted">Cost centres allocate shared/overhead cost across drivers (headcount, area, usage). Manage allocation runs in Finance → Cost accounting.</p>';
		if (!empty($ccRows)) {
			echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Code</th><th>Name</th><th>Driver</th></tr></thead><tbody>';
			foreach ($ccRows as $r) {
				echo '<tr><td>' . epc_erp_h((string) ($r['code'] ?? '')) . '</td><td>' . epc_erp_h((string) ($r['name'] ?? '')) . '</td><td>' . epc_erp_h((string) ($r['driver'] ?? ($r['driver_type'] ?? ''))) . '</td></tr>';
			}
			echo '</tbody></table></div>';
		} else {
			echo '<p class="text-muted">No cost centres yet.</p>';
		}
		echo '</div>';
		break;

	case 'business_units':
	default:
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_business_units', 'Business units',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'e.g. BU-RETAIL'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'legal_entity_id', 'label' => 'Legal entity', 'type' => 'select', 'options' => $leOpts),
				array('name' => 'manager', 'label' => 'Manager'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'manager', 'label' => 'Manager')),
			'fa-sitemap');
		break;
}
