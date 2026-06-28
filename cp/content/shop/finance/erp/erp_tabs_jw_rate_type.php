<?php
/**
 * Jewellery ERP — Rate Type Master.
 * GMS, GOZ, KB, TTB etc. with conversion factors and margin settings.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$rates = epc_jewel_rate_type_list($db_link, $companyId);

erp_page_header('<i class="fa fa-line-chart"></i> Rate Type Master', 'Metal rate types with conversion factors.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Rate type master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Rate Type Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_rt_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<?php if (empty($rates)): ?>
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" style="display:inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_rate_type_seed">
			<button type="submit" class="btn btn-success btn-xs"><i class="fa fa-database"></i> Seed defaults</button>
		</form>
		<?php endif; ?>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr><th>No.</th><th>Metal</th><th>Rate Type</th><th>Conv. Factor</th><th>Conv. Factor OZ</th><th>Currency</th><th>Status</th></tr></thead>
			<tbody>
			<?php if (empty($rates)): ?><tr><td colspan="7" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($rates as $r): ?>
			<tr>
				<td><?php echo $n++; ?></td>
				<td><?php echo epc_erp_h($r['metal']); ?></td>
				<td><strong><?php echo epc_erp_h($r['rate_type']); ?></strong></td>
				<td><?php echo number_format((float)$r['conv_factor'], 4); ?></td>
				<td><?php echo number_format((float)$r['conv_factor_oz'], 4); ?></td>
				<td><?php echo epc_erp_h($r['currency']); ?></td>
				<td><?php echo epc_erp_h($r['status']); ?></td>
			</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_rt_form" style="display:none;margin-top:10px;">
			<div class="ef-section"><span class="ef-section-title">Rate Type Details</span>
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="action" value="jw_rate_type_save">
				<div class="ef-row">
					<div class="ef-field"><label>Metal</label><select name="metal"><option value="G">G</option><option value="S">S</option><option value="T">T</option></select></div>
					<div class="ef-field"><label>Rate Type</label><input name="rate_type" required placeholder="GMS"></div>
					<div class="ef-field"><label>Conv Fact(Gms)</label><input name="conv_factor" type="number" step="0.000001" value="1.000000"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Currency</label><input name="currency" value="AED" maxlength="5"></div>
					<div class="ef-field"><label>Curr Rate</label><input name="curr_rate" type="number" step="0.000001" value="1.000000"></div>
					<div class="ef-field"><label>Conv Fact OZ</label><input name="conv_factor_oz" type="number" step="0.0001" value="31.1035"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Rate Variance %</label><input name="rate_variance_pct" type="number" step="0.01" value="50.00"></div>
					<div class="ef-field"><label>POS Margin Min</label><input name="pos_margin_min" type="number" step="0.01" value="1.00"></div>
					<div class="ef-field"><label>POS Margin Max</label><input name="pos_margin_max" type="number" step="0.01" value="50.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Status</label><select name="status"><option value="MULTIPLY">MULTIPLY</option><option value="DIVIDE">DIVIDE</option></select></div>
					<div class="ef-field"><label><input type="checkbox" name="is_default" value="1"> Default Rate Type</label></div>
				</div>
				<div class="ef-actions">
					<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
					<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_rt_form').style.display='none'">Cancel</button>
				</div>
			</form>
			</div>
		</div>
	</div>
	<div class="ef-status"><span>Mode:=VIEW</span><span>Header New Record → Function Key (F5)</span></div>
</div>
