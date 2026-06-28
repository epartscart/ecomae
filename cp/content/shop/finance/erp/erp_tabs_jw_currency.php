<?php
/**
 * Jewellery ERP — Currency Master.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$currencies = epc_jewel_currency_list($db_link, $companyId);

erp_page_header('<i class="fa fa-money"></i> Currency Master', 'Multi-currency with conversion rates.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Currency master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Currency Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_curr_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<?php if (empty($currencies)): ?>
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" style="display:inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_currency_seed">
			<button type="submit" class="btn btn-success btn-xs"><i class="fa fa-database"></i> Seed defaults</button>
		</form>
		<?php endif; ?>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr><th>No.</th><th>Currency Code</th><th>Description</th><th>Conv.Rate</th><th>Fraction</th><th>Symbol</th><th>Status</th></tr></thead>
			<tbody>
			<?php if (empty($currencies)): ?><tr><td colspan="7" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($currencies as $c): ?>
			<tr>
				<td><?php echo $n++; ?></td>
				<td><strong><?php echo epc_erp_h($c['curr_code']); ?></strong></td>
				<td><?php echo epc_erp_h($c['description']); ?></td>
				<td><?php echo number_format((float)$c['conv_rate'], 6); ?></td>
				<td><?php echo epc_erp_h($c['fraction']); ?></td>
				<td><?php echo epc_erp_h($c['symbol']); ?></td>
				<td><?php echo epc_erp_h($c['status']); ?></td>
			</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div>
	<div class="ef-status"><span>Mode:=VIEW</span><span>Header New Record → Function Key (F5)</span></div>
</div>
