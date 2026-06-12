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
		$count = count($rows);
		echo '<div class="epc-erp-section pm-section epc-d365-form" data-pm-section style="margin-bottom:22px;">';

		// D365 Action Pane (tab strip + command toolbar)
		echo '<div class="epc-d365-actionpane">';
		echo '<div class="epc-d365-ap-tabstrip"><span class="epc-d365-ap-tab is-active">' . epc_erp_h($title) . '</span></div>';
		echo '<div class="epc-d365-ap-toolbar">';
		echo '<button type="button" class="epc-d365-cmd epc-d365-cmd--primary" data-pm-new><i class="fa fa-plus-circle"></i><span>New</span></button>';
		echo '<button type="button" class="epc-d365-cmd" data-pm-save><i class="fa fa-save"></i><span>Save</span></button>';
		echo '<span class="epc-d365-ap-count"><i class="fa ' . epc_erp_h($icon) . '"></i> ' . $count . ' record' . ($count === 1 ? '' : 's') . '</span>';
		echo '</div></div>';

		// New-record panel rendered as a D365 FastTab (hidden until "New")
		echo '<form class="pm-form epc-erp-pm-form epc-d365-newpanel" data-pm-table="' . epc_erp_h($table) . '">';
		echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
		echo '<input type="hidden" name="pm_table" value="' . epc_erp_h($table) . '">';
		echo '<div class="epc-d365-fasttab is-open" data-fasttab>';
		echo '<button type="button" class="epc-d365-fasttab-hd" data-fasttab-toggle><i class="fa fa-caret-down epc-d365-fasttab-caret"></i> <span>General</span> <small class="epc-d365-fasttab-sum">New record details</small></button>';
		echo '<div class="epc-d365-fasttab-bd"><div class="pm-fields">';
		foreach ($fields as $f) {
			echo epc_erp_pm_field_html($f);
		}
		echo '</div>';
		echo '<div class="epc-d365-newpanel-actions"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-check"></i> Save record</button> <button type="button" class="btn btn-default btn-sm" data-pm-cancel>Cancel</button></div>';
		echo '</div></div></form>';

		// D365 grid list page
		if (empty($rows)) {
			echo '<div class="epc-d365-grid-empty"><i class="fa ' . epc_erp_h($icon) . '"></i><p>No records yet — choose <strong>New</strong> to add the first one.</p></div>';
		} else {
			echo '<div class="table-responsive epc-d365-grid-wrap"><table class="table table-condensed epc-d365-grid">';
			echo '<thead><tr>';
			foreach ($columns as $c) {
				echo '<th>' . epc_erp_h($c['label']) . '</th>';
			}
			echo '<th>Status</th></tr></thead><tbody>';
			foreach ($rows as $r) {
				echo '<tr>';
				$first = true;
				foreach ($columns as $c) {
					$v = $r[$c['key']] ?? '';
					$cls = $first ? ' class="epc-d365-grid-link"' : '';
					echo '<td' . $cls . '>' . epc_erp_h((string) $v) . '</td>';
					$first = false;
				}
				$active = (int) ($r['active'] ?? 1) === 1;
				echo '<td>' . ($active ? '<span class="epc-d365-pill epc-d365-pill--on">Active</span>' : '<span class="epc-d365-pill">Inactive</span>') . '</td>';
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
/* ── Dynamics 365 Finance & Operations form styling ── */
/* Sub-module strip = D365 form tab pages */
.pm-module-tabs{display:flex;flex-wrap:wrap;gap:0;margin:0 0 18px;border-bottom:1px solid #c8c6c4;padding:0}
.pm-module-tabs a{padding:9px 16px;font-size:13px;font-weight:600;color:#605e5c;text-decoration:none;border:0;border-bottom:2px solid transparent;margin-bottom:-1px;background:transparent}
.pm-module-tabs a.active{color:#0f6cbd;border-bottom-color:#0f6cbd}
.pm-module-tabs a:hover{color:#0f6cbd;background:#f3f2f1}

/* Action Pane (top command bar) */
.epc-d365-form{background:#fff;border:1px solid #e1dfdd;border-radius:2px;box-shadow:0 1px 2px rgba(0,0,0,.05)}
.epc-d365-actionpane{border-bottom:1px solid #edebe9;background:#faf9f8}
.epc-d365-ap-tabstrip{display:flex;gap:0;padding:0 8px;border-bottom:1px solid #edebe9}
.epc-d365-ap-tab{padding:8px 14px 7px;font-size:13px;font-weight:600;color:#201f1e;border-bottom:2px solid #0f6cbd}
.epc-d365-ap-toolbar{display:flex;align-items:center;gap:2px;padding:5px 8px;flex-wrap:wrap}
.epc-d365-cmd{display:inline-flex;align-items:center;gap:6px;background:transparent;border:1px solid transparent;border-radius:2px;padding:5px 11px;font-size:13px;color:#201f1e;cursor:pointer;line-height:1.2}
.epc-d365-cmd .fa{font-size:14px;color:#0f6cbd}
.epc-d365-cmd:hover{background:#f3f2f1;border-color:#edebe9}
.epc-d365-cmd--primary .fa{color:#107c10}
.epc-d365-ap-count{margin-left:auto;font-size:12px;color:#605e5c;padding:0 8px}
.epc-d365-ap-count .fa{margin-right:5px;color:#8a8886}

/* FastTab (collapsible section) */
.epc-d365-newpanel{display:none;border-bottom:1px solid #edebe9}
.epc-d365-form.is-creating .epc-d365-newpanel{display:block}
.epc-d365-fasttab{border-top:1px solid #f3f2f1}
.epc-d365-fasttab-hd{width:100%;text-align:left;background:#fff;border:0;border-bottom:1px solid #edebe9;padding:10px 16px;font-size:14px;font-weight:600;color:#201f1e;cursor:pointer}
.epc-d365-fasttab-hd:hover{background:#faf9f8}
.epc-d365-fasttab-caret{color:#605e5c;transition:transform .15s ease;margin-right:6px}
.epc-d365-fasttab:not(.is-open) .epc-d365-fasttab-caret{transform:rotate(-90deg)}
.epc-d365-fasttab-sum{font-weight:400;color:#a19f9d;margin-left:10px;font-size:12px}
.epc-d365-fasttab-bd{display:none;padding:14px 16px 16px}
.epc-d365-fasttab.is-open .epc-d365-fasttab-bd{display:block}
.epc-d365-newpanel-actions{margin-top:12px}

/* Form fields (D365 single-column-ish grid of labelled inputs) */
.pm-fields{display:flex;flex-wrap:wrap;gap:14px 18px;align-items:flex-end;background:transparent;border:0;border-radius:0;padding:0}
.pm-field{display:flex;flex-direction:column;gap:4px;min-width:200px;flex:0 1 240px}
.pm-field label{font-size:12px;font-weight:600;text-transform:none;letter-spacing:0;color:#323130;margin:0}
.pm-field .form-control{border-radius:2px;border:1px solid #8a8886;box-shadow:none}
.pm-field .form-control:focus{border-color:#0f6cbd;box-shadow:0 0 0 1px #0f6cbd}
.pm-field--btn{flex:0 0 auto;min-width:auto}

/* Grid list page */
.epc-d365-grid-wrap{margin:0}
.epc-d365-grid{margin:0;font-size:13px;border:0}
.epc-d365-grid thead th{background:#faf9f8;color:#605e5c;font-weight:600;font-size:12px;border-top:0;border-bottom:1px solid #c8c6c4 !important;padding:8px 12px;white-space:nowrap}
.epc-d365-grid tbody td{border-top:0;border-bottom:1px solid #f3f2f1;padding:8px 12px;vertical-align:middle}
.epc-d365-grid tbody tr:hover{background:#f3f9fd}
.epc-d365-grid-link{color:#0f6cbd;font-weight:600;cursor:pointer}
.epc-d365-grid-link:hover{text-decoration:underline}
.epc-d365-pill{display:inline-block;font-size:11px;font-weight:600;color:#605e5c;background:#f3f2f1;border-radius:2px;padding:2px 8px}
.epc-d365-pill--on{color:#0b6a0b;background:#dff6dd}
.epc-d365-grid-empty{text-align:center;color:#605e5c;padding:34px 18px}
.epc-d365-grid-empty .fa{font-size:30px;color:#c8c6c4;display:block;margin-bottom:10px}
.epc-d365-grid-empty p{margin:0}
</style>
<script>
(function(){
	if(window.__epcD365PmBound){return;}
	window.__epcD365PmBound=true;
	document.addEventListener('click',function(ev){
		var n=ev.target.closest('[data-pm-new]');
		if(n){ev.preventDefault();var s=n.closest('[data-pm-section]');if(s){var open=s.classList.toggle('is-creating');if(open){var i=s.querySelector('.pm-fields input:not([type=hidden]),.pm-fields select');if(i){try{i.focus();}catch(e){}}}}return;}
		var c=ev.target.closest('[data-pm-cancel]');
		if(c){ev.preventDefault();var s2=c.closest('[data-pm-section]');if(s2){s2.classList.remove('is-creating');}return;}
		var sv=ev.target.closest('[data-pm-save]');
		if(sv){ev.preventDefault();var s3=sv.closest('[data-pm-section]');if(s3){s3.classList.add('is-creating');var f=s3.querySelector('form.pm-form');if(f){if(f.requestSubmit){f.requestSubmit();}else{var b=f.querySelector('[type=submit]');if(b){b.click();}}}}return;}
		var t=ev.target.closest('[data-fasttab-toggle]');
		if(t){var tb=t.closest('[data-fasttab]');if(tb){tb.classList.toggle('is-open');}return;}
	});
})();
</script>
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
