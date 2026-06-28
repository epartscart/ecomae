<?php
/**
 * Jewellery ERP — Karat Master.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$karats = epc_jewel_karat_list($db_link, $companyId);
$divisions = epc_jewel_divisions();

erp_page_header('<i class="fa fa-tachometer"></i> Karat Master', 'Karat codes with standard purity, ranges and gravity.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Karat master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Karat Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_karat_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<?php if (empty($karats)): ?>
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" style="display:inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_karat_seed">
			<button type="submit" class="btn btn-success btn-xs"><i class="fa fa-database"></i> Seed defaults</button>
		</form>
		<?php endif; ?>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Karat Code</th><th>Desc</th><th>Std. Purity</th>
				<th>Gravity</th><th>Division</th>
			</tr></thead>
			<tbody>
			<?php if (empty($karats)): ?>
				<tr><td colspan="6" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($karats as $k): ?>
				<tr>
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($k['karat_code']); ?></strong></td>
					<td><?php echo epc_erp_h($k['description']); ?></td>
					<td><?php echo number_format((float) $k['std_purity'], 6); ?></td>
					<td><?php echo number_format((float) $k['sp_gravity'], 4); ?></td>
					<td><?php echo epc_erp_h($divisions[$k['division']] ?? $k['division']); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_karat_form" style="display:none;margin-top:10px;">
			<div class="ef-section">
				<span class="ef-section-title">Add / Edit Karat</span>
				<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="action" value="jw_karat_save">
					<div class="ef-row">
						<div class="ef-field"><label>Karat Code</label><input name="karat_code" required placeholder="22"></div>
						<div class="ef-field ef-field-wide"><label>Description</label><input name="description" placeholder="22 Karat"></div>
						<div class="ef-field"><label>Division</label>
							<select name="division"><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($c); ?></option><?php endforeach; ?></select>
						</div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>STD. Purity</label><input name="std_purity" type="number" step="0.000001" value="0.917000"></div>
						<div class="ef-field"><label>Range From</label><input name="range_from" type="number" step="0.000001" value="0.916000"></div>
						<div class="ef-field"><label>Range To</label><input name="range_to" type="number" step="0.000001" value="0.918000"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Sp Gravity</label><input name="sp_gravity" type="number" step="0.0001" value="0"></div>
						<div class="ef-field"><label>POS Rate Min/Max Amt</label><input name="pos_rate_min_max" type="number" step="0.01" value="0"></div>
					</div>
					<div class="ef-actions">
						<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
						<button type="button" class="btn btn-default btn-sm" onclick="this.closest('#jw_karat_form').style.display='none'">Cancel</button>
					</div>
				</form>
			</div>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record → Function Key (F5)</span>
	</div>
</div>
