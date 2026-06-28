<?php
/**
 * Jewellery ERP — Currency Master.
 * Ref: Suntech Currency Master screenshot.
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

erp_page_header('<i class="fa fa-money"></i> Currency Master', 'Multi-currency with conversion rates and ranges.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Currency master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Currency Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" id="jw_curr_new_btn" onclick="document.getElementById('jw_curr_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
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
			<thead><tr>
				<th>No.</th><th>Currency Code</th><th>Description</th><th>Conv Rate</th>
			</tr></thead>
			<tbody>
			<?php if (empty($currencies)): ?>
				<tr><td colspan="4" style="text-align:center;color:#999">No records — click Seed defaults to populate</td></tr>
			<?php else: $n=1; foreach ($currencies as $c): ?>
				<tr class="ef-grid-row" data-id="<?php echo (int)$c['id']; ?>"
					onclick="jwCurrSelect(this)" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($c['curr_code']); ?></strong></td>
					<td><?php echo epc_erp_h($c['description']); ?></td>
					<td><?php echo number_format((float)$c['conv_rate'], 6); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_curr_form" style="display:none;margin-top:12px;">
			<div class="ef-section">
				<span class="ef-section-title">Currency Details</span>
				<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="action" value="jw_currency_save">
					<div class="ef-row">
						<div class="ef-field"><label>Curr. Code</label><input name="curr_code" maxlength="5" required placeholder="AED" style="width:80px"></div>
						<div class="ef-field ef-field-wide"><label>Description</label><input name="description" maxlength="60" placeholder="Arab Emirates Dirham"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Fraction</label><input name="fraction" maxlength="20" placeholder="FILLS" style="width:100px"></div>
						<div class="ef-field"><label>Symbol</label><input name="symbol" maxlength="5" placeholder="د.إ" style="width:60px"></div>
						<div class="ef-field"><label>Conv Rate</label><input name="conv_rate" type="number" step="0.000001" value="1.000000"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Min Conv Rate</label><input name="min_conv_rate" type="number" step="0.000001" value="1.000000"></div>
						<div class="ef-field"><label>Max Conv Rate</label><input name="max_conv_rate" type="number" step="0.000001" value="1.000000"></div>
						<div class="ef-field"><label>Status</label>
							<select name="status"><option value="MULTIPLY">MULTIPLY</option><option value="DIVIDE">DIVIDE</option></select>
						</div>
					</div>
					<div class="ef-actions">
						<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
						<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_curr_form').style.display='none'">Cancel</button>
					</div>
				</form>
			</div>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
<script>
function jwCurrSelect(row){
	document.querySelectorAll('.ef-grid-row').forEach(function(r){r.style.background='';});
	row.style.background='#d0e8ff';
}
</script>
