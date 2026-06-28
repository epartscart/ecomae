<?php
/**
 * Jewellery ERP — Metal Stock Balance Report.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$divisions = epc_jewel_divisions();
$items = epc_jewel_metal_stock_list($db_link, $companyId);

erp_page_header('<i class="fa fa-bar-chart"></i> Metal Stock Balance', 'Current stock by metal, karat, division with values.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Stock balance'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Stock Balance Report</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-refresh"></i> Refresh</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-download"></i> Export Excel</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<div style="margin-left:auto;display:flex;gap:6px;align-items:center;font-size:11px;">
			<label>As Of</label><input type="date" value="<?php echo date('Y-m-d'); ?>" style="font-size:11px">
			<label>Division</label><select style="font-size:11px"><option value="">All</option><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($c); ?></option><?php endforeach; ?></select>
			<label>Branch</label><select style="font-size:11px"><option value="">All</option><option>HO</option></select>
		</div>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>Item Code</th><th>Description</th><th>Metal</th><th>Karat</th><th>Purity</th>
				<th>Stock Pcs</th><th>Stock Gms</th><th>Pure Wt</th><th>Rate/Gm</th><th>Stock Value</th>
			</tr></thead>
			<tbody>
			<?php
			$totalPcs = 0; $totalGms = 0; $totalValue = 0;
			if (empty($items)):
			?>
				<tr><td colspan="10" style="text-align:center;color:#999;padding:20px">No stock records.</td></tr>
			<?php else: foreach ($items as $i):
				$pcs = (int)$i['stock_pcs']; $gms = (float)$i['stock_gms']; $val = (float)$i['stock_value'];
				$totalPcs += $pcs; $totalGms += $gms; $totalValue += $val;
			?>
			<tr>
				<td><strong><?php echo epc_erp_h($i['item_code']); ?></strong></td>
				<td><?php echo epc_erp_h($i['description']); ?></td>
				<td><?php echo epc_erp_h($i['metal']); ?></td>
				<td><?php echo epc_erp_h($i['karat']); ?></td>
				<td><?php echo number_format((float)$i['purity'], 6); ?></td>
				<td style="text-align:right"><?php echo $pcs; ?></td>
				<td style="text-align:right"><?php echo number_format($gms, 4); ?></td>
				<td style="text-align:right"><?php echo number_format($gms * (float)$i['purity'], 4); ?></td>
				<td style="text-align:right"><?php echo epc_erp_money((float)($i['sale_price_gms'] ?? 0), 2); ?></td>
				<td style="text-align:right"><?php echo epc_erp_money($val, 2); ?></td>
			</tr>
			<?php endforeach; endif; ?>
			</tbody>
			<tfoot><tr style="font-weight:700">
				<td colspan="5" style="text-align:right">Totals:</td>
				<td style="text-align:right"><?php echo $totalPcs ?? 0; ?></td>
				<td style="text-align:right"><?php echo number_format($totalGms ?? 0, 4); ?></td>
				<td colspan="2"></td>
				<td style="text-align:right"><?php echo epc_erp_money($totalValue ?? 0, 2); ?></td>
			</tr></tfoot>
		</table>
	</div>
	<div class="ef-status"><span>Report view</span><span>As of: <?php echo date('d/m/Y'); ?></span></div>
</div>
