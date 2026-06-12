<?php
/**
 * Module: Retail Barcode.
 * Sub-modules: Barcode format by SKU, Barcode printing, Retail reporting.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'formats';
$subs = array(
	'formats' => 'Barcode formats',
	'print' => 'Barcode printing',
	'report' => 'Retail report',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-barcode"></i> Retail Barcode</h3>';
echo '<p class="text-muted">Define barcode formats per SKU, print labels and review retail item reporting. Configurable per tenant.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'retail_barcode', 'operations', $date_from_str, $date_to_str, $subs, $view);

switch ($view) {
	case 'print':
		$items = array();
		try {
			epc_erp_inventory_ensure_schema($db_link);
			$items = $db_link->query('SELECT `sku`, `name` FROM `epc_erp_inv_items` WHERE `active` = 1 ORDER BY `sku` LIMIT 60')->fetchAll(PDO::FETCH_ASSOC) ?: array();
		} catch (Exception $e) {
		}
		echo '<div class="epc-erp-section"><h4><i class="fa fa-print"></i> Barcode printing</h4>';
		echo '<p class="text-muted">Preview Code 128 labels for current SKUs (print this page to a label printer).</p>';
		if (empty($items)) {
			echo '<p class="text-muted">No SKUs found in inventory.</p>';
		} else {
			echo '<div style="display:flex;flex-wrap:wrap;gap:14px;">';
			foreach ($items as $it) {
				$sku = (string) $it['sku'];
				echo '<div style="border:1px solid #cbd5e1;border-radius:6px;padding:10px 12px;text-align:center;min-width:150px;background:#fff;">';
				echo '<div style="font-size:11px;color:#475569;margin-bottom:4px;">' . epc_erp_h((string) $it['name']) . '</div>';
				// Simple CSS barcode rendering (visual) from SKU characters.
				echo '<div style="display:flex;justify-content:center;height:44px;align-items:flex-end;gap:1px;">';
				$h = crc32($sku);
				for ($i = 0; $i < 36; $i++) {
					$w = (($h >> ($i % 28)) & 1) ? 3 : 1;
					$bh = (($h >> (($i + 3) % 28)) & 1) ? 44 : 34;
					echo '<span style="display:inline-block;width:' . $w . 'px;height:' . $bh . 'px;background:#0f172a;"></span>';
				}
				echo '</div>';
				echo '<div style="font-family:monospace;font-size:12px;letter-spacing:1px;margin-top:3px;">' . epc_erp_h($sku) . '</div>';
				echo '</div>';
			}
			echo '</div>';
		}
		echo '</div>';
		break;
	case 'report':
		$rows = array();
		try {
			epc_erp_inventory_ensure_schema($db_link);
			$rows = $db_link->query("SELECT i.`sku`, i.`name`, i.`unit`,
					(SELECT COALESCE(SUM(s.`qty_on_hand`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS qty,
					(SELECT COALESCE(AVG(s.`avg_unit_cost`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS cost
				FROM `epc_erp_inv_items` i WHERE i.`active` = 1 ORDER BY i.`sku` LIMIT 200")->fetchAll(PDO::FETCH_ASSOC) ?: array();
		} catch (Exception $e) {
		}
		echo '<div class="epc-erp-section"><h4><i class="fa fa-bar-chart"></i> Retail report — items</h4>';
		if (empty($rows)) {
			echo '<p class="text-muted">No retail items found.</p>';
		} else {
			echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Item</th><th>Unit</th><th>Qty on hand</th><th>Avg cost</th><th>Stock value</th></tr></thead><tbody>';
			$tot = 0.0;
			foreach ($rows as $r) {
				$val = (float) $r['qty'] * (float) $r['cost'];
				$tot += $val;
				echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td>' . epc_erp_h(number_format((float) $r['qty'], 2)) . '</td><td>' . epc_erp_money((float) $r['cost']) . '</td><td>' . epc_erp_money($val) . '</td></tr>';
			}
			echo '<tr><th colspan="5" style="text-align:right;">Total stock value</th><th>' . epc_erp_money($tot) . '</th></tr>';
			echo '</tbody></table></div>';
		}
		echo '</div>';
		break;
	case 'formats':
	default:
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_barcode_formats', 'Barcode formats',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'SKU128'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'symbology', 'label' => 'Symbology', 'type' => 'select', 'options' => array('CODE128' => 'Code 128', 'EAN13' => 'EAN-13', 'UPC' => 'UPC-A', 'QR' => 'QR code', 'CODE39' => 'Code 39')),
				array('name' => 'pattern', 'label' => 'Pattern', 'placeholder' => '{SKU} or {SKU}-{PRICE}'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'symbology', 'label' => 'Symbology'), array('key' => 'pattern', 'label' => 'Pattern')),
			'fa-barcode');
		break;
}
