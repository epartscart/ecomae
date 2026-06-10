<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_erp_inventory_ensure_schema($db_link);
$bomItems = epc_erp_bom_from_inventory($db_link, 150);

erp_page_header(
	'<i class="fa fa-cogs"></i> Manufacturing &amp; BOM',
	'Read-only bill of materials from inventory SKUs — full MRP planned later.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Manufacturing'),
	)
);
erp_stat_cards(array(
	array('label' => 'SKUs (BOM source)', 'value' => (string) count($bomItems)),
	array('label' => 'With stock', 'value' => (string) count(array_filter($bomItems, function ($i) {
		return (float) ($i['qty_on_hand'] ?? 0) > 0;
	}))),
));
ob_start();
if (empty($bomItems)) {
	erp_empty_state('No inventory items yet. Add SKUs under Inventory to use as BOM components.', 'fa-cubes');
} else {
	erp_table_open(array('SKU', 'Name', 'Unit', 'On hand', 'Reorder'));
	foreach ($bomItems as $i) {
		echo '<tr><td>' . epc_erp_h($i['sku']) . '</td><td>' . epc_erp_h($i['name']) . '</td>';
		echo '<td>' . epc_erp_h($i['unit'] ?: 'ea') . '</td>';
		echo '<td>' . epc_erp_h(number_format((float) ($i['qty_on_hand'] ?? 0), 3)) . '</td>';
		echo '<td>' . epc_erp_h(number_format((float) ($i['reorder_level'] ?? 0), 3)) . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Component list (from inventory)', ob_get_clean(), array('icon' => 'fa-list'));
?>
<div class="epc-erp-coming-soon" style="margin-top:16px;padding:28px;">
	<i class="fa fa-industry"></i>
	<h4>Work orders &amp; multi-level BOM</h4>
	<p class="text-muted">Production orders, component issue/receipt, and routing will extend this view in a future release.</p>
</div>
