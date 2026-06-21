<?php
/**
 * Module: Product Information System.
 * Sub-modules: Product dev kit (drafts), All products, Release product (active),
 * Field setup (per-tenant inventory / non-inventory field classification).
 * Backed by the per-tenant inventory item master + field definitions.
 *
 * The active industry pack "releases" a set of product fields into this screen;
 * the client decides which fields are inventory attributes (stock-tracked, part
 * of the item master & valuation) vs non-inventory (descriptive / catalogue).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_industry_packs.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_product_structure.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
$pimFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pim_custom_fields.php';
if (is_file($pimFile)) {
	require_once $pimFile;
}
epc_erp_pm_inline_assets();

epc_erp_inventory_ensure_schema($db_link);
epc_erp_prod_structure_ensure_schema($db_link);
epc_erp_company_context_ensure($db_link);
if (function_exists('epc_pim_ensure_schema')) {
	epc_pim_ensure_schema($db_link);
}

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'all';
$subs = array(
	'devkit' => 'Product dev kit',
	'all' => 'All products',
	'released' => 'Release product',
	'dimensions' => 'Dimensions &amp; variants',
	'fields' => 'Field setup',
	'pim_attrs' => 'PIM attributes',
);

$pinfoMsg = '';
$pinfoErr = '';

// ----- POST: field classification + add custom field -----------------------
if (!empty($_POST['pinfo_action'])) {
	$postedCsrf = isset($_POST['csrf_guard_key']) ? (string) $_POST['csrf_guard_key'] : '';
	if ($csrf !== '' && !hash_equals($csrf, $postedCsrf)) {
		$pinfoErr = 'Security token mismatch — please reload and try again.';
	} else {
		try {
			$act = (string) $_POST['pinfo_action'];
			if ($act === 'save_field_roles') {
				$roles = isset($_POST['field_role']) && is_array($_POST['field_role']) ? $_POST['field_role'] : array();
				$actives = isset($_POST['field_active']) && is_array($_POST['field_active']) ? $_POST['field_active'] : array();
				$n = 0;
				foreach ($roles as $fkey => $role) {
					epc_erp_inv_field_set_role($db_link, (string) $fkey, (string) $role);
					epc_erp_inv_field_set_active($db_link, (string) $fkey, !empty($actives[$fkey]) ? 1 : 0);
					$n++;
				}
				$pinfoMsg = $n . ' field(s) updated. Inventory / non-inventory classification saved for this tenant.';
			} elseif ($act === 'save_dim_group') {
				$cats = epc_erp_prod_dimension_catalog();
				$grp = array(
					'code' => (string) ($_POST['dg_code'] ?? 'STD'),
					'name' => (string) ($_POST['dg_name'] ?? 'Standard'),
					'product' => isset($_POST['dim_product']) && is_array($_POST['dim_product']) ? array_map('strval', array_keys($_POST['dim_product'])) : array(),
					'storage' => isset($_POST['dim_storage']) && is_array($_POST['dim_storage']) ? array_map('strval', array_keys($_POST['dim_storage'])) : array(),
					'tracking' => isset($_POST['dim_tracking']) && is_array($_POST['dim_tracking']) ? array_map('strval', array_keys($_POST['dim_tracking'])) : array(),
				);
				epc_erp_prod_dim_group_save($db_link, $grp);
				$pinfoMsg = 'Dimension group saved. Active product dimensions now drive variant generation.';
			} elseif ($act === 'add_dim_value') {
				$dt = (string) ($_POST['dim_type'] ?? '');
				$vals = preg_split('/[\r\n,]+/', (string) ($_POST['dim_values'] ?? ''));
				$added = 0;
				foreach ((array) $vals as $vv) {
					if (epc_erp_prod_dim_value_add($db_link, $dt, (string) $vv)) {
						$added++;
					}
				}
				$pinfoMsg = $added > 0 ? ($added . ' value(s) added to ' . epc_erp_prod_dimension_label('product', $dt) . '.') : 'No values added — pick a product dimension and enter at least one value.';
				if ($added === 0) {
					$pinfoErr = 'Enter one or more values for a valid product dimension (Size / Colour / Style / Configuration / Version).';
				}
			} elseif ($act === 'delete_dim_value') {
				epc_erp_prod_dim_value_delete($db_link, (int) ($_POST['value_id'] ?? 0));
				$pinfoMsg = 'Dimension value removed.';
			} elseif ($act === 'generate_variants') {
				$iid = (int) ($_POST['item_id'] ?? 0);
				$bsku = (string) ($_POST['base_sku'] ?? '');
				if ($iid > 0 && $bsku !== '') {
					$gres = epc_erp_prod_variants_generate($db_link, $iid, $bsku);
					$pinfoMsg = 'Variants for ' . epc_erp_h($bsku) . ': ' . (int) $gres['generated'] . ' generated, ' . (int) $gres['existing'] . ' already existed (' . count($gres['variants']) . ' total).';
				} else {
					$pinfoErr = 'Pick a product and ensure it has a SKU before generating variants.';
				}
			} elseif ($act === 'pim_create_field') {
				if (function_exists('epc_pim_field_save')) {
					$fName = trim((string) ($_POST['pim_name'] ?? ''));
					$fType = (string) ($_POST['pim_type'] ?? 'text');
					if ($fName === '') {
						$pinfoErr = 'Field name is required.';
					} else {
						$fId = epc_pim_field_save($db_link, array(
							'name' => $fName,
							'field_type' => $fType,
							'description' => trim((string) ($_POST['pim_desc'] ?? '')),
							'required' => !empty($_POST['pim_required']) ? 1 : 0,
							'show_inventory' => !empty($_POST['pim_show_inventory']) ? 1 : 0,
							'show_sales' => !empty($_POST['pim_show_sales']) ? 1 : 0,
							'show_purchase' => !empty($_POST['pim_show_purchase']) ? 1 : 0,
						));
						if ($fId > 0 && in_array($fType, array('single_option', 'multi_option'), true)) {
							$rawOpts = (string) ($_POST['pim_options'] ?? '');
							$pos = 0;
							foreach (preg_split('/[\r\n,]+/', $rawOpts) as $o) {
								$o = trim($o);
								if ($o !== '') {
									epc_pim_field_option_save($db_link, $fId, $o, $o, $pos++);
								}
							}
						}
						$pinfoMsg = 'PIM attribute "' . $fName . '" created (type: ' . $fType . ').';
					}
				} else {
					$pinfoErr = 'PIM module not available — file epc_erp_pim_custom_fields.php missing.';
				}
			} elseif ($act === 'pim_delete_field') {
				if (function_exists('epc_pim_field_delete')) {
					epc_pim_field_delete($db_link, (int) ($_POST['pim_field_id'] ?? 0));
					$pinfoMsg = 'PIM attribute deactivated.';
				}
			} elseif ($act === 'pim_add_option') {
				if (function_exists('epc_pim_field_option_save')) {
					$optLabel = trim((string) ($_POST['opt_label'] ?? ''));
					if ($optLabel !== '') {
						epc_pim_field_option_save($db_link, (int) ($_POST['pim_field_id'] ?? 0), $optLabel);
						$pinfoMsg = 'Option "' . $optLabel . '" added.';
					}
				}
			} elseif ($act === 'pim_delete_option') {
				if (function_exists('epc_pim_field_option_delete')) {
					epc_pim_field_option_delete($db_link, (int) ($_POST['opt_id'] ?? 0));
					$pinfoMsg = 'Option removed.';
				}
			} elseif ($act === 'add_field') {
				$opts = array();
				if (((string) ($_POST['new_type'] ?? '')) === 'select') {
					$raw = (string) ($_POST['new_options'] ?? '');
					foreach (preg_split('/[\r\n,]+/', $raw) as $o) {
						$o = trim($o);
						if ($o !== '') {
							$opts[] = $o;
						}
					}
				}
				$ok = epc_erp_inv_field_upsert($db_link, array(
					'field_key' => (string) ($_POST['new_key'] ?? ''),
					'label' => (string) ($_POST['new_label'] ?? ''),
					'field_type' => (string) ($_POST['new_type'] ?? 'text'),
					'options' => $opts,
					'field_role' => (string) ($_POST['new_role'] ?? 'inventory'),
					'sort_order' => 1000,
				));
				$pinfoMsg = $ok ? 'Custom field added.' : 'Could not add field — a valid key (letters/numbers/underscore) is required.';
				if (!$ok) {
					$pinfoErr = 'Provide a field key using lowercase letters, numbers or underscores.';
				}
			}
		} catch (Exception $e) {
			$pinfoErr = 'Could not save: ' . $e->getMessage();
		}
	}
}

// ----- Active pack + field-classification summary --------------------------
$activePack = epc_erp_company_industry_pack($db_link, epc_erp_active_company_id($db_link));
if ($activePack !== '' && epc_erp_industry_pack($activePack) === null) {
	$activePack = '';
}
$activePackDef = $activePack !== '' ? epc_erp_industry_pack($activePack) : null;
$fieldDefs = epc_erp_inv_field_defs_all($db_link);
$invFields = array_filter($fieldDefs, function ($f) { return ($f['field_role'] ?? 'inventory') === 'inventory'; });
$nonFields = array_filter($fieldDefs, function ($f) { return ($f['field_role'] ?? 'inventory') === 'non_inventory'; });

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-cube"></i> Product Information System</h3>';
echo '<p class="text-muted">Develop products, browse the catalogue, release products as active, and configure which product fields are inventory vs non-inventory. Per-tenant; specialized fields come from the industry pack.</p></div>';

// Active product-structure summary (dimension group + variants).
$dimGroup = epc_erp_prod_dim_group_get($db_link);
$dimProdActive = (array) $dimGroup['product'];
$variantCount = (int) $db_link->query('SELECT COUNT(*) FROM `epc_erp_prod_variants` WHERE `active`=1')->fetchColumn();
$dimsUrl = epc_erp_tab_url($erpUrl, 'product_info', $date_from_str, $date_to_str, 'operations') . '&pm_view=dimensions';

// Industry-pack banner (shown on every sub-view).
echo '<div class="epc-erp-section" style="background:#f4f8fc;border:1px solid #dbe6f1;border-radius:8px;padding:12px 14px;margin-bottom:14px;">';
if ($activePackDef !== null) {
	echo '<div><i class="fa fa-industry"></i> <strong>Industry pack:</strong> ' . epc_erp_h((string) $activePackDef['label'])
		. ' &middot; <span class="label label-success">' . count($invFields) . ' inventory field(s)</span> '
		. '<span class="label label-default">' . count($nonFields) . ' non-inventory field(s)</span></div>';
	echo '<div class="text-muted" style="margin-top:4px;font-size:12px;">Specialized units: ';
	foreach ((array) ($activePackDef['uoms'] ?? array()) as $u) {
		echo '<span class="label label-info" style="margin-right:3px;">' . epc_erp_h((string) $u) . '</span>';
	}
	echo '</div>';
} else {
	$setupUrl = epc_erp_tab_url($erpUrl, 'erp_setup', $date_from_str, $date_to_str, 'overview');
	echo '<div><i class="fa fa-industry"></i> <strong>No industry pack applied</strong> — running generic fields. '
		. '<a href="' . epc_erp_h($setupUrl) . '">Apply an industry pack</a> to load specialized product fields.</div>';
}
// Product structure (dimension group) line — always shown.
echo '<div style="margin-top:6px;padding-top:6px;border-top:1px dashed #cdddee;">'
	. '<i class="fa fa-object-group"></i> <strong>Product structure:</strong> '
	. '<span class="label label-primary">' . epc_erp_h((string) $dimGroup['code']) . ' · ' . epc_erp_h((string) $dimGroup['name']) . '</span> ';
if ($dimProdActive) {
	echo '<span class="text-muted" style="font-size:12px;">dimensions: ';
	foreach ($dimProdActive as $dk) {
		echo '<span class="label label-info" style="margin-right:3px;">' . epc_erp_h(epc_erp_prod_dimension_label('product', (string) $dk)) . '</span>';
	}
	echo '</span> <span class="label label-success">' . $variantCount . ' variant(s)</span>';
} else {
	echo '<span class="text-muted" style="font-size:12px;">no product dimensions active</span>';
}
echo ' &middot; <a href="' . epc_erp_h($dimsUrl) . '">Configure dimensions &amp; variants</a></div>';
echo '</div>';

if ($pinfoMsg !== '') {
	echo '<div class="alert alert-success" style="margin-bottom:12px;"><i class="fa fa-check-circle"></i> ' . epc_erp_h($pinfoMsg) . '</div>';
}
if ($pinfoErr !== '') {
	echo '<div class="alert alert-danger" style="margin-bottom:12px;"><i class="fa fa-exclamation-triangle"></i> ' . epc_erp_h($pinfoErr) . '</div>';
}

epc_erp_pm_module_tabs($erpUrl, 'product_info', 'operations', $date_from_str, $date_to_str, $subs, $view);

$items = array();
try {
	$items = $db_link->query("SELECT i.`id`, i.`sku`, i.`name`, i.`item_type`, i.`unit`, i.`active`,
			(SELECT COALESCE(SUM(s.`qty_on_hand`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS qty
		FROM `epc_erp_inv_items` i ORDER BY i.`sku` LIMIT 400")->fetchAll(PDO::FETCH_ASSOC) ?: array();
} catch (Exception $e) {
}

$newItemUrl = epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str, 'operations');

if ($view === 'fields') {
	$typeLabels = array('text' => 'Text', 'number' => 'Number', 'date' => 'Date', 'select' => 'Choice list');
	echo '<div class="epc-erp-section"><h4><i class="fa fa-sliders"></i> Field setup — inventory vs non-inventory</h4>';
	echo '<p class="text-muted">Choose which product fields are part of the <strong>inventory</strong> item master (stock-tracked, valued, part of accounting) and which are <strong>non-inventory</strong> (descriptive / catalogue only). Defaults come from the industry pack and are fully editable per tenant.</p>';
	echo '<form method="post" action="">';
	echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
	echo '<input type="hidden" name="pinfo_action" value="save_field_roles">';
	echo '<div class="table-responsive"><table class="table table-bordered table-condensed table-striped" style="background:#fff;"><thead><tr>'
		. '<th>Field</th><th>Key</th><th>Type</th><th>Classification</th><th>Active</th><th>Source</th></tr></thead><tbody>';
	if (empty($fieldDefs)) {
		echo '<tr><td colspan="6" class="text-muted">No fields defined yet — apply an industry pack or add a custom field below.</td></tr>';
	}
	foreach ($fieldDefs as $f) {
		$fk = (string) $f['field_key'];
		$role = (string) ($f['field_role'] ?? 'inventory');
		$src = (string) ($f['source_pack'] ?? '');
		$srcLabel = $src !== '' ? (string) (epc_erp_industry_pack($src)['label'] ?? $src) : 'Custom / base';
		echo '<tr>';
		echo '<td><strong>' . epc_erp_h((string) $f['label']) . '</strong></td>';
		echo '<td><code>' . epc_erp_h($fk) . '</code></td>';
		echo '<td>' . epc_erp_h($typeLabels[(string) $f['field_type']] ?? (string) $f['field_type']) . '</td>';
		echo '<td><select name="field_role[' . epc_erp_h($fk) . ']" class="form-control input-sm" style="min-width:160px;">'
			. '<option value="inventory"' . ($role === 'inventory' ? ' selected' : '') . '>Inventory (stock + accounting)</option>'
			. '<option value="non_inventory"' . ($role === 'non_inventory' ? ' selected' : '') . '>Non-inventory (info only)</option>'
			. '</select></td>';
		echo '<td style="text-align:center;"><input type="checkbox" name="field_active[' . epc_erp_h($fk) . ']" value="1"' . ((int) $f['active'] === 1 ? ' checked' : '') . '></td>';
		echo '<td class="text-muted" style="font-size:12px;">' . epc_erp_h($srcLabel) . '</td>';
		echo '</tr>';
	}
	echo '</tbody></table></div>';
	echo '<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save classification</button>';
	echo '</form>';

	// Add custom field.
	echo '<hr><h4><i class="fa fa-plus-circle"></i> Add custom field</h4>';
	echo '<form method="post" action="" class="form-inline" style="flex-wrap:wrap;gap:6px;">';
	echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
	echo '<input type="hidden" name="pinfo_action" value="add_field">';
	echo '<input type="text" name="new_key" class="form-control input-sm" placeholder="field_key" style="width:140px;margin-right:6px;" maxlength="32"> ';
	echo '<input type="text" name="new_label" class="form-control input-sm" placeholder="Label" style="width:180px;margin-right:6px;" maxlength="120"> ';
	echo '<select name="new_type" class="form-control input-sm" style="margin-right:6px;"><option value="text">Text</option><option value="number">Number</option><option value="date">Date</option><option value="select">Choice list</option></select> ';
	echo '<select name="new_role" class="form-control input-sm" style="margin-right:6px;"><option value="inventory">Inventory</option><option value="non_inventory">Non-inventory</option></select> ';
	echo '<input type="text" name="new_options" class="form-control input-sm" placeholder="Choices (comma separated, for choice list)" style="width:260px;margin-right:6px;"> ';
	echo '<button type="submit" class="btn btn-default btn-sm"><i class="fa fa-plus"></i> Add field</button>';
	echo '</form>';
	echo '<p class="text-muted" style="margin-top:8px;">Custom fields are stored in this tenant\'s database only and become available on the inventory item master.</p>';
	echo '</div>';
} elseif ($view === 'dimensions') {
	$cat = epc_erp_prod_dimension_catalog();
	$group = epc_erp_prod_dim_group_get($db_link);
	$activeProd = (array) $group['product'];

	echo '<div class="epc-erp-section"><h4><i class="fa fa-object-group"></i> Product structure — dimensions &amp; variants</h4>';
	echo '<p class="text-muted">Advanced product structure. A <strong>dimension group</strong> selects which dimensions an item uses. <strong>Product dimensions</strong> (Configuration / Size / Colour / Style / Version), crossed with their registered values, generate the item\'s <strong>variants</strong>. Storage and tracking dimensions describe where/how stock is held and do not create variants.</p>';

	// 1) Dimension group configuration.
	echo '<form method="post" action="">';
	echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
	echo '<input type="hidden" name="pinfo_action" value="save_dim_group">';
	echo '<div class="row">';
	echo '<div class="form-group col-sm-3" style="padding:6px;"><label>Group code</label><input type="text" name="dg_code" class="form-control input-sm" value="' . epc_erp_h((string) $group['code']) . '" maxlength="32"></div>';
	echo '<div class="form-group col-sm-5" style="padding:6px;"><label>Group name</label><input type="text" name="dg_name" class="form-control input-sm" value="' . epc_erp_h((string) $group['name']) . '" maxlength="120"></div>';
	echo '</div>';
	echo '<div class="row">';
	foreach (array('product' => 'dim_product', 'storage' => 'dim_storage', 'tracking' => 'dim_tracking') as $catKey => $field) {
		$activeCat = (array) $group[$catKey];
		echo '<div class="col-sm-4" style="padding:6px;"><div style="border:1px solid #e2e8f0;border-radius:6px;padding:10px;">';
		echo '<strong>' . epc_erp_h((string) $cat[$catKey]['label']) . '</strong>';
		echo '<div class="text-muted" style="font-size:11px;margin-bottom:6px;">' . epc_erp_h((string) $cat[$catKey]['note']) . '</div>';
		foreach ((array) $cat[$catKey]['dims'] as $dk => $dl) {
			$chk = in_array($dk, $activeCat, true) ? ' checked' : '';
			echo '<label style="display:block;font-weight:normal;margin:2px 0;"><input type="checkbox" name="' . $field . '[' . epc_erp_h($dk) . ']" value="1"' . $chk . '> ' . epc_erp_h((string) $dl) . '</label>';
		}
		echo '</div></div>';
	}
	echo '</div>';
	echo '<button type="submit" class="btn btn-primary btn-sm" style="margin-top:6px;"><i class="fa fa-save"></i> Save dimension group</button>';
	echo '</form>';

	// 2) Dimension values (only for the active product dimensions).
	echo '<hr><h4><i class="fa fa-list"></i> Product dimension values</h4>';
	if (empty($activeProd)) {
		echo '<p class="text-muted">No product dimensions are active. Tick at least one product dimension above (e.g. Size, Colour) to register its values.</p>';
	} else {
		echo '<p class="text-muted">Register the allowed values for each active product dimension. These are crossed to build variants.</p>';
		echo '<div class="row">';
		foreach ($activeProd as $dk) {
			$vals = epc_erp_prod_dim_values_list($db_link, (string) $dk);
			echo '<div class="col-sm-4" style="padding:6px;"><div style="border:1px solid #e2e8f0;border-radius:6px;padding:10px;">';
			echo '<strong>' . epc_erp_h(epc_erp_prod_dimension_label('product', (string) $dk)) . '</strong> <span class="badge">' . count($vals) . '</span>';
			echo '<div style="margin:6px 0;">';
			if (empty($vals)) {
				echo '<span class="text-muted" style="font-size:12px;">No values yet.</span>';
			}
			foreach ($vals as $v) {
				echo '<span class="label label-info" style="margin:2px;display:inline-block;">' . epc_erp_h((string) $v['value'])
					. ' <a href="#" onclick="document.getElementById(\'delv-' . (int) $v['id'] . '\').submit();return false;" style="color:#fff;opacity:.8;">&times;</a></span>';
				echo '<form id="delv-' . (int) $v['id'] . '" method="post" action="" style="display:none;"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '"><input type="hidden" name="pinfo_action" value="delete_dim_value"><input type="hidden" name="value_id" value="' . (int) $v['id'] . '"></form>';
			}
			echo '</div>';
			echo '<form method="post" action="" class="form-inline"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '"><input type="hidden" name="pinfo_action" value="add_dim_value"><input type="hidden" name="dim_type" value="' . epc_erp_h((string) $dk) . '">';
			echo '<input type="text" name="dim_values" class="form-control input-sm" placeholder="Value(s), comma separated" style="width:64%;"> <button type="submit" class="btn btn-default btn-sm"><i class="fa fa-plus"></i></button></form>';
			echo '</div></div>';
		}
		echo '</div>';
	}

	// 3) Variant generation per item + preview.
	echo '<hr><h4><i class="fa fa-th"></i> Generate variants</h4>';
	if (empty($activeProd)) {
		echo '<p class="text-muted">Activate product dimensions and register values first.</p>';
	} else {
		$valuesByDim = array();
		$totalCombos = 1;
		foreach ($activeProd as $dk) {
			$c = count(epc_erp_prod_dim_values_list($db_link, (string) $dk));
			$valuesByDim[(string) $dk] = $c;
			$totalCombos *= max(1, $c);
		}
		$haveAnyValues = array_sum($valuesByDim) > 0;
		echo '<p class="text-muted">Pick a released product; its base SKU is crossed with the active product dimensions to generate one variant per combination'
			. ($haveAnyValues ? ' (<strong>' . (int) $totalCombos . '</strong> per product with current values)' : '') . '.</p>';
		echo '<form method="post" action="" class="form-inline" style="margin-bottom:10px;">';
		echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
		echo '<input type="hidden" name="pinfo_action" value="generate_variants">';
		echo '<select name="item_pick" id="varItemPick" class="form-control input-sm" style="min-width:280px;margin-right:6px;" onchange="var o=this.options[this.selectedIndex];document.getElementById(\'varItemId\').value=o.getAttribute(\'data-id\')||\'\';document.getElementById(\'varBaseSku\').value=o.getAttribute(\'data-sku\')||\'\';">';
		echo '<option value="">— select product —</option>';
		foreach ($items as $r) {
			echo '<option value="' . (int) $r['id'] . '" data-id="' . (int) $r['id'] . '" data-sku="' . epc_erp_h((string) $r['sku']) . '">' . epc_erp_h((string) $r['sku']) . ' — ' . epc_erp_h((string) $r['name']) . '</option>';
		}
		echo '</select>';
		echo '<input type="hidden" name="item_id" id="varItemId" value="">';
		echo '<input type="hidden" name="base_sku" id="varBaseSku" value="">';
		echo '<button type="submit" class="btn btn-primary btn-sm"' . ($haveAnyValues ? '' : ' disabled') . '><i class="fa fa-cogs"></i> Generate variants</button>';
		echo '</form>';

		// Existing variants table (all items).
		$allVariants = $db_link->query('SELECT * FROM `epc_erp_prod_variants` WHERE `active`=1 ORDER BY `base_sku`,`variant_sku` LIMIT 500')->fetchAll(PDO::FETCH_ASSOC);
		echo '<div class="table-responsive"><table class="table table-bordered table-condensed table-striped" style="background:#fff;"><thead><tr><th>Base SKU</th><th>Variant SKU</th><th>Variant</th></tr></thead><tbody>';
		if (empty($allVariants)) {
			echo '<tr><td colspan="3" class="text-muted">No variants generated yet.</td></tr>';
		}
		foreach ($allVariants as $v) {
			echo '<tr><td>' . epc_erp_h((string) $v['base_sku']) . '</td><td><code>' . epc_erp_h((string) $v['variant_sku']) . '</code></td><td>' . epc_erp_h((string) $v['variant_label']) . '</td></tr>';
		}
		echo '</tbody></table></div>';
	}
	echo '</div>';
} elseif ($view === 'pim_attrs') {
	if (!function_exists('epc_pim_field_list')) {
		echo '<div class="epc-erp-section"><div class="alert alert-warning"><i class="fa fa-exclamation-triangle"></i> PIM module file missing — <code>epc_erp_pim_custom_fields.php</code> not found. Deploy the latest code.</div></div>';
	} else {
		$pimFields = epc_pim_field_list($db_link);
		$pimTypeLabels = array(
			'text' => 'Text', 'number' => 'Number', 'date' => 'Date',
			'boolean' => 'Yes/No', 'single_option' => 'Dropdown (single)',
			'multi_option' => 'Checkboxes (multi)',
		);

		echo '<div class="epc-erp-section">';
		echo '<h4><i class="fa fa-tags"></i> PIM custom attributes <span class="badge">' . count($pimFields) . '</span></h4>';
		echo '<p class="text-muted">Create unlimited custom product attributes with any field type. Each attribute can be shown on <strong>Inventory</strong>, <strong>Sales</strong>, and/or <strong>Purchase</strong> module forms. Options are managed per field for dropdown and checkbox types.</p>';

		// --- Create new field form ---
		echo '<div style="background:#f4f8fc;border:1px solid #dbe6f1;border-radius:8px;padding:14px;margin-bottom:16px;">';
		echo '<h5 style="margin-top:0;"><i class="fa fa-plus-circle"></i> Create new attribute</h5>';
		echo '<form method="post" action="">';
		echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
		echo '<input type="hidden" name="pinfo_action" value="pim_create_field">';
		echo '<div class="row">';
		echo '<div class="form-group col-sm-4" style="padding:6px;"><label>Field name <span style="color:red">*</span></label><input type="text" name="pim_name" class="form-control input-sm" placeholder="e.g. Inventory Type" required></div>';
		echo '<div class="form-group col-sm-3" style="padding:6px;"><label>Type</label>';
		echo '<select name="pim_type" class="form-control input-sm" onchange="document.getElementById(\'pim_opts_row\').style.display=(this.value===\'single_option\'||this.value===\'multi_option\')?\'block\':\'none\';">';
		foreach ($pimTypeLabels as $tk => $tl) {
			echo '<option value="' . $tk . '">' . epc_erp_h($tl) . '</option>';
		}
		echo '</select></div>';
		echo '<div class="form-group col-sm-5" style="padding:6px;"><label>Description</label><input type="text" name="pim_desc" class="form-control input-sm" placeholder="Optional help text"></div>';
		echo '</div>';
		echo '<div class="row">';
		echo '<div class="col-sm-4" style="padding:6px;"><label>Show on modules</label><div>';
		echo '<label style="display:inline-block;margin-right:10px;font-weight:normal;"><input type="checkbox" name="pim_show_inventory" value="1" checked> Inventory</label>';
		echo '<label style="display:inline-block;margin-right:10px;font-weight:normal;"><input type="checkbox" name="pim_show_sales" value="1" checked> Sales</label>';
		echo '<label style="display:inline-block;font-weight:normal;"><input type="checkbox" name="pim_show_purchase" value="1" checked> Purchase</label>';
		echo '</div></div>';
		echo '<div class="col-sm-2" style="padding:6px;"><label>&nbsp;</label><div><label style="font-weight:normal;"><input type="checkbox" name="pim_required" value="1"> Required</label></div></div>';
		echo '</div>';
		echo '<div id="pim_opts_row" style="display:none;padding:6px;">';
		echo '<label>Options (comma-separated for dropdown/checkbox fields)</label>';
		echo '<textarea name="pim_options" class="form-control input-sm" rows="2" placeholder="e.g. Inventory, Non-Inventory, Service"></textarea>';
		echo '</div>';
		echo '<div style="padding:6px;"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Create attribute</button></div>';
		echo '</form></div>';

		// --- Existing fields table ---
		echo '<div class="table-responsive"><table class="table table-bordered table-condensed table-striped" style="background:#fff;">';
		echo '<thead><tr><th>Name</th><th>Code</th><th>Type</th><th>Inventory</th><th>Sales</th><th>Purchase</th><th>Required</th><th>Options</th><th>Actions</th></tr></thead><tbody>';
		if (empty($pimFields)) {
			echo '<tr><td colspan="9" class="text-muted">No PIM attributes yet. Use the form above to create one.</td></tr>';
		}
		foreach ($pimFields as $pf) {
			$pfId = (int) $pf['id'];
			$opts = epc_pim_field_options($db_link, $pfId);
			echo '<tr>';
			echo '<td><strong>' . epc_erp_h((string) $pf['name']) . '</strong>';
			if ((string) $pf['description'] !== '') {
				echo '<br><small class="text-muted">' . epc_erp_h((string) $pf['description']) . '</small>';
			}
			echo '</td>';
			echo '<td><code>' . epc_erp_h((string) $pf['code']) . '</code></td>';
			echo '<td>' . epc_erp_h($pimTypeLabels[(string) $pf['field_type']] ?? (string) $pf['field_type']) . '</td>';
			echo '<td style="text-align:center;">' . ((int) $pf['show_inventory'] ? '<i class="fa fa-check text-success"></i>' : '<i class="fa fa-minus text-muted"></i>') . '</td>';
			echo '<td style="text-align:center;">' . ((int) $pf['show_sales'] ? '<i class="fa fa-check text-success"></i>' : '<i class="fa fa-minus text-muted"></i>') . '</td>';
			echo '<td style="text-align:center;">' . ((int) $pf['show_purchase'] ? '<i class="fa fa-check text-success"></i>' : '<i class="fa fa-minus text-muted"></i>') . '</td>';
			echo '<td style="text-align:center;">' . ((int) $pf['required'] ? 'Yes' : 'No') . '</td>';
			echo '<td>';
			if (!empty($opts)) {
				foreach ($opts as $oi => $o) {
					echo '<span class="label label-info" style="margin:1px;display:inline-block;">' . epc_erp_h((string) $o['label']);
					echo ' <a href="#" onclick="document.getElementById(\'pim_delopt_' . (int) $o['id'] . '\').submit();return false;" style="color:#fff;opacity:.7;" title="Remove">&times;</a>';
					echo '</span>';
					echo '<form id="pim_delopt_' . (int) $o['id'] . '" method="post" style="display:none;"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '"><input type="hidden" name="pinfo_action" value="pim_delete_option"><input type="hidden" name="opt_id" value="' . (int) $o['id'] . '"></form>';
				}
			}
			if (in_array((string) $pf['field_type'], array('single_option', 'multi_option'), true)) {
				echo '<form method="post" class="form-inline" style="margin-top:4px;display:inline-flex;gap:2px;">';
				echo '<input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
				echo '<input type="hidden" name="pinfo_action" value="pim_add_option">';
				echo '<input type="hidden" name="pim_field_id" value="' . $pfId . '">';
				echo '<input type="text" name="opt_label" class="form-control input-sm" placeholder="Add option" style="width:100px;">';
				echo '<button type="submit" class="btn btn-default btn-xs"><i class="fa fa-plus"></i></button>';
				echo '</form>';
			} else {
				echo '<span class="text-muted">-</span>';
			}
			echo '</td>';
			echo '<td>';
			echo '<form method="post" style="display:inline;"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '"><input type="hidden" name="pinfo_action" value="pim_delete_field"><input type="hidden" name="pim_field_id" value="' . $pfId . '">';
			echo '<button type="submit" class="btn btn-danger btn-xs" onclick="return confirm(\'Deactivate this attribute?\');"><i class="fa fa-trash"></i></button></form>';
			echo '</td>';
			echo '</tr>';
		}
		echo '</tbody></table></div>';
		echo '</div>';
	}
} elseif ($view === 'devkit') {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-flask"></i> Product dev kit</h4>';
	echo '<p class="text-muted">Design new products: define SKU, type, unit and specialized attributes before releasing. Items with no stock movement yet are shown as in-development.</p>';
	$draft = array_filter($items, function ($r) {
		return (float) $r['qty'] == 0.0;
	});
	echo '<a class="btn btn-primary btn-sm" href="' . epc_erp_h($newItemUrl) . '"><i class="fa fa-plus"></i> New product (inventory item)</a>';
	echo '<div class="table-responsive" style="margin-top:10px;"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Name</th><th>Type</th><th>Unit</th><th>Stage</th></tr></thead><tbody>';
	if (empty($draft)) {
		echo '<tr><td colspan="5" class="text-muted">No in-development products.</td></tr>';
	}
	foreach ($draft as $r) {
		echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['item_type']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td><span class="label label-warning">In development</span></td></tr>';
	}
	echo '</tbody></table></div></div>';
} elseif ($view === 'released') {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-check-circle"></i> Released products (active)</h4>';
	$rel = array_filter($items, function ($r) {
		return (int) $r['active'] === 1;
	});
	echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Name</th><th>Type</th><th>Unit</th><th>Qty on hand</th><th>Status</th></tr></thead><tbody>';
	if (empty($rel)) {
		echo '<tr><td colspan="6" class="text-muted">No released products.</td></tr>';
	}
	foreach ($rel as $r) {
		echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['item_type']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td>' . epc_erp_h(number_format((float) $r['qty'], 2)) . '</td><td><span class="label label-success">Released</span></td></tr>';
	}
	echo '</tbody></table></div></div>';
} else {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-cubes"></i> All products <span class="badge">' . count($items) . '</span></h4>';
	echo '<a class="btn btn-primary btn-sm" href="' . epc_erp_h($newItemUrl) . '"><i class="fa fa-plus"></i> New product</a>';
	echo '<div class="table-responsive" style="margin-top:10px;"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>SKU</th><th>Name</th><th>Type</th><th>Unit</th><th>Qty on hand</th><th>Status</th></tr></thead><tbody>';
	if (empty($items)) {
		echo '<tr><td colspan="6" class="text-muted">No products yet.</td></tr>';
	}
	foreach ($items as $r) {
		$active = (int) $r['active'] === 1;
		$badge = $active ? '<span class="label label-success">Released</span>' : '<span class="label label-default">Inactive</span>';
		echo '<tr><td>' . epc_erp_h((string) $r['sku']) . '</td><td>' . epc_erp_h((string) $r['name']) . '</td><td>' . epc_erp_h((string) $r['item_type']) . '</td><td>' . epc_erp_h((string) $r['unit']) . '</td><td>' . epc_erp_h(number_format((float) $r['qty'], 2)) . '</td><td>' . $badge . '</td></tr>';
	}
	echo '</tbody></table></div></div>';
}
