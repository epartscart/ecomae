<?php
/**
 * Shared renderer for the D365-style master-data modules.
 *
 * epc_erp_pm_section() renders one "code + name + extra fields" master as a
 * create form + live list, posting through the generic `pm_save` AJAX action.
 * Tab files compose a module page from several of these sections so all the
 * sub-modules are visible without duplicating markup.
 *
 * Expected in scope: $db_link (PDO), $csrf (string).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';

if (!function_exists('epc_erp_pm_field_html')) {
	/**
	 * Render one form field.
	 * @param array{name:string,label:string,type?:string,options?:array,placeholder?:string,required?:bool} $f
	 */
	function epc_erp_pm_field_html(array $f): string
	{
		$name = $f['name'];
		$label = epc_erp_h($f['label']);
		$type = $f['type'] ?? 'text';
		$req = !empty($f['required']) ? ' required' : '';
		$ph = isset($f['placeholder']) ? ' placeholder="' . epc_erp_h($f['placeholder']) . '"' : '';
		$out = '<div class="pm-field"><label>' . $label . '</label>';
		if ($type === 'select') {
			$out .= '<select name="' . epc_erp_h($name) . '" class="form-control input-sm"' . $req . '>';
			foreach (($f['options'] ?? array()) as $val => $txt) {
				$out .= '<option value="' . epc_erp_h((string) $val) . '">' . epc_erp_h((string) $txt) . '</option>';
			}
			$out .= '</select>';
		} elseif ($type === 'number') {
			$out .= '<input type="number" step="any" name="' . epc_erp_h($name) . '" class="form-control input-sm"' . $ph . $req . '>';
		} else {
			$out .= '<input type="text" name="' . epc_erp_h($name) . '" class="form-control input-sm"' . $ph . $req . '>';
		}
		$out .= '</div>';
		return $out;
	}
}

if (!function_exists('epc_erp_pm_section')) {
	/**
	 * Render a master CRUD section.
	 *
	 * @param PDO    $db
	 * @param string $csrf
	 * @param string $table    registered pm table
	 * @param string $title    section heading
	 * @param array  $fields   form fields (each: name,label,type,options,...)
	 * @param array  $columns  list columns: [ ['key'=>'code','label'=>'Code'], ... ]
	 * @param string $icon     fa icon
	 */
	function epc_erp_pm_section(PDO $db, string $csrf, string $table, string $title, array $fields, array $columns, string $icon = 'fa-list')
	{
		try {
			$rows = epc_erp_pm_list($db, $table);
		} catch (Exception $e) {
			$rows = array();
		}
		echo '<div class="epc-erp-section pm-section" style="margin-bottom:22px;">';
		echo '<h4><i class="fa ' . epc_erp_h($icon) . '"></i> ' . epc_erp_h($title) . ' <span class="badge">' . count($rows) . '</span></h4>';

		// form
		echo '<form class="pm-form epc-erp-pm-form" data-pm-table="' . epc_erp_h($table) . '">';
		echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
		echo '<input type="hidden" name="pm_table" value="' . epc_erp_h($table) . '">';
		echo '<div class="pm-fields">';
		foreach ($fields as $f) {
			echo epc_erp_pm_field_html($f);
		}
		echo '<div class="pm-field pm-field--btn"><label>&nbsp;</label><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Add</button></div>';
		echo '</div></form>';

		// list
		if (empty($rows)) {
			echo '<p class="text-muted" style="margin-top:8px;">No records yet — add the first one above.</p>';
		} else {
			echo '<div class="table-responsive" style="margin-top:10px;"><table class="table table-striped table-bordered table-condensed">';
			echo '<thead><tr>';
			foreach ($columns as $c) {
				echo '<th>' . epc_erp_h($c['label']) . '</th>';
			}
			echo '<th>Status</th></tr></thead><tbody>';
			foreach ($rows as $r) {
				echo '<tr>';
				foreach ($columns as $c) {
					$v = $r[$c['key']] ?? '';
					echo '<td>' . epc_erp_h((string) $v) . '</td>';
				}
				$active = (int) ($r['active'] ?? 1) === 1;
				echo '<td>' . ($active ? '<span class="label label-success">Active</span>' : '<span class="label label-default">Inactive</span>') . '</td>';
				echo '</tr>';
			}
			echo '</tbody></table></div>';
		}
		echo '</div>';
	}
}

if (!function_exists('epc_erp_pm_inline_assets')) {
	/** Emit the shared CSS + JS for pm forms once per page. */
	function epc_erp_pm_inline_assets()
	{
		static $done = false;
		if ($done) {
			return;
		}
		$done = true;
		?>
<style>
.pm-section h4 .badge{background:#64748b;margin-left:6px}
.pm-fields{display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:12px}
.pm-field{display:flex;flex-direction:column;gap:3px;min-width:150px;flex:1 1 150px}
.pm-field label{font-size:11px;text-transform:uppercase;letter-spacing:.03em;color:#64748b;margin:0}
.pm-field--btn{flex:0 0 auto;min-width:auto}
.pm-module-tabs{display:flex;flex-wrap:wrap;gap:6px;margin:0 0 16px;border-bottom:2px solid #e2e8f0;padding-bottom:0}
.pm-module-tabs a{padding:8px 14px;font-size:13px;font-weight:600;color:#475569;text-decoration:none;border-radius:6px 6px 0 0;border:1px solid transparent;border-bottom:none;margin-bottom:-2px}
.pm-module-tabs a.active{color:#1d4ed8;background:#fff;border-color:#e2e8f0;border-bottom:2px solid #fff}
.pm-module-tabs a:hover{color:#1d4ed8}
</style>
		<?php
	}
}

if (!function_exists('epc_erp_pm_module_tabs')) {
	/** Render sub-module tab strip for a module page. $items: [key=>label], $active key, builds ?...&pm_view=key */
	function epc_erp_pm_module_tabs(string $erpUrl, string $tab, string $area, string $from, string $to, array $items, string $active)
	{
		echo '<div class="pm-module-tabs">';
		foreach ($items as $key => $label) {
			$url = epc_erp_tab_url($erpUrl, $tab, $from, $to, $area) . '&pm_view=' . urlencode($key);
			$cls = $key === $active ? ' class="active"' : '';
			echo '<a href="' . epc_erp_h($url) . '"' . $cls . '>' . epc_erp_h($label) . '</a>';
		}
		echo '</div>';
	}
}
