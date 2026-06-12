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
epc_erp_pm_inline_assets();

epc_erp_inventory_ensure_schema($db_link);

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'all';
$subs = array(
	'devkit' => 'Product dev kit',
	'all' => 'All products',
	'released' => 'Release product',
	'fields' => 'Field setup',
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
$activePack = epc_erp_platform_setting_get($db_link, 'active_industry_pack', '');
if ($activePack !== '' && epc_erp_industry_pack($activePack) === null) {
	$activePack = '';
}
$activePackDef = $activePack !== '' ? epc_erp_industry_pack($activePack) : null;
$fieldDefs = epc_erp_inv_field_defs_all($db_link);
$invFields = array_filter($fieldDefs, function ($f) { return ($f['field_role'] ?? 'inventory') === 'inventory'; });
$nonFields = array_filter($fieldDefs, function ($f) { return ($f['field_role'] ?? 'inventory') === 'non_inventory'; });

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-cube"></i> Product Information System</h3>';
echo '<p class="text-muted">Develop products, browse the catalogue, release products as active, and configure which product fields are inventory vs non-inventory. Per-tenant; specialized fields come from the industry pack.</p></div>';

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
