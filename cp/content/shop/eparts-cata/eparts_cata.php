<?php
/**
 * CP — EParts CATA (unified catalog) control panel.
 * Route: /cp/shop/eparts-cata  (also /cp/shop/eparts_cata)
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cata_config.php';

function epc_cata_cp_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$session = DP_User::getAdminSession();
$csrf = is_array($session) ? (string) ($session['csrf_guard_key'] ?? '') : '';
$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}
$base = '/' . $backend;
$flash = '';
$flashOk = false;

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
	$postedCsrf = (string) ($_POST['csrf_guard_key'] ?? '');
	$action = (string) ($_POST['epc_cata_action'] ?? '');
	if ($csrf === '' || !hash_equals($csrf, $postedCsrf)) {
		$flash = 'Security check failed. Refresh and try again.';
	} elseif ($action === 'save_presentation') {
		$cfg = epc_cata_presentation_config();
		$cfg['default_article_view'] = (string) ($_POST['default_article_view'] ?? $cfg['default_article_view']);
		$cfg['default_section'] = (string) ($_POST['default_section'] ?? $cfg['default_section']);
		$cfg['storefront_primary'] = epc_cata_normalize_catalog_target((string) ($_POST['storefront_primary'] ?? $cfg['storefront_primary']));
		$cfg['show_category_icons'] = !empty($_POST['show_category_icons']);
		$cfg['warehouse_only_prices'] = !empty($_POST['warehouse_only_prices']);
		$cfg['enable_vin_search'] = !empty($_POST['enable_vin_search']);
		$cfg['enable_plate_search'] = !empty($_POST['enable_plate_search']);
		$sections = array('passenger' => 0, 'commercial' => 0, 'motorbike' => 0);
		$postedSections = isset($_POST['sections_enabled']) && is_array($_POST['sections_enabled'])
			? $_POST['sections_enabled']
			: array();
		foreach ($sections as $key => $_) {
			$sections[$key] = !empty($postedSections[$key]) ? 1 : 0;
		}
		if (!array_filter($sections)) {
			$sections['passenger'] = 1;
		}
		$cfg['sections_enabled'] = $sections;
		if (epc_cata_setting_set('presentation', $cfg)) {
			$flash = 'Storefront presentation settings saved.';
			$flashOk = true;
		} else {
			$flash = 'Could not save presentation settings.';
		}
	} elseif ($action === 'save_categories') {
		$rows = epc_cata_category_config_rows_for_cp();
		$enabledMap = isset($_POST['cat_enabled']) && is_array($_POST['cat_enabled'])
			? $_POST['cat_enabled']
			: array();
		$targetMap = isset($_POST['cat_target']) && is_array($_POST['cat_target'])
			? $_POST['cat_target']
			: array();
		$orderMap = isset($_POST['cat_order']) && is_array($_POST['cat_order'])
			? $_POST['cat_order']
			: array();
		foreach ($rows as $idx => $row) {
			$strId = (string) (int) ($row['STR_ID'] ?? 0);
			if ($strId === '0') {
				continue;
			}
			$rows[$idx]['enabled'] = !empty($enabledMap[$strId]);
			if (isset($targetMap[$strId])) {
				$rows[$idx]['catalog_target'] = epc_cata_normalize_catalog_target((string) $targetMap[$strId]);
			}
			if (isset($orderMap[$strId]) && is_numeric($orderMap[$strId])) {
				$rows[$idx]['ORDER'] = (int) $orderMap[$strId];
			}
		}
		if (epc_cata_category_config_save_bulk($rows)) {
			$flash = 'Category visibility and targets saved.';
			$flashOk = true;
		} else {
			$flash = 'Could not save category settings.';
		}
	} elseif ($action === 'reset_categories') {
		if (epc_cata_category_config_reset()) {
			$flash = 'Category overrides reset to defaults.';
			$flashOk = true;
		} else {
			$flash = 'Could not reset categories.';
		}
	} elseif ($action === 'refresh_status') {
		epc_cata_cp_status_cached(1, true);
		epc_cata_cp_import_monitor(true);
		$flash = 'Catalog status cache refreshed.';
		$flashOk = true;
	}
}

$status = epc_cata_cp_status_shell();
$monitor = epc_cata_cp_import_monitor();
$presentation = epc_cata_presentation_config();
$categories = epc_cata_category_config_rows_for_cp();
$catalogTargets = epc_cata_catalog_targets();
$totals = is_array($status['totals'] ?? null) ? $status['totals'] : array();
$providers = is_array($status['providers'] ?? null) ? $status['providers'] : array();
$storefrontCata = rtrim((string) ($GLOBALS['DP_Config']->domain_path ?? ''), '/') . '/en/eparts-cata';
$storefrontMod = rtrim((string) ($GLOBALS['DP_Config']->domain_path ?? ''), '/') . '/en/eparts-mod';
$modCpUrl = $base . '/shop/eparts-mod';
$synUrl = $base . '/shop/manufacturers_synonyms';

epc_cp_page_frame_open(array(
	'class' => 'epc-cata-cp',
	'hero' => array(
		'badge' => 'EParts CATA',
		'title' => 'Unified parts catalog',
		'sub' => 'Configure storefront vehicle catalog, category cards, and provider sync status.',
		'actions' => array(
			array('url' => $storefrontCata, 'label' => 'Open storefront', 'icon' => 'fa-external-link', 'primary' => true),
			array('url' => $modCpUrl, 'label' => 'EParts Mod CP', 'icon' => 'fa-car'),
			array('url' => $synUrl, 'label' => 'Manufacturer synonyms', 'icon' => 'fa-exchange'),
		),
	),
));
?>
<style>
.epc-cata-cp .epc-cata-metrics{display:grid;grid-template-columns:repeat(auto-fill,minmax(140px,1fr));gap:10px;margin:0 0 16px}
.epc-cata-cp .epc-cata-metric{background:#fff;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;border-left:4px solid #dc2626}
.epc-cata-cp .epc-cata-metric__l{font-size:11px;font-weight:700;letter-spacing:.04em;text-transform:uppercase;color:#64748b}
.epc-cata-cp .epc-cata-metric__v{font-size:20px;font-weight:800;color:#0f172a;margin-top:4px}
.epc-cata-cp .epc-cata-flash{margin:0 0 14px;padding:10px 14px;border-radius:8px;font-weight:600}
.epc-cata-cp .epc-cata-flash.ok{background:#ecfdf5;border:1px solid #a7f3d0;color:#065f46}
.epc-cata-cp .epc-cata-flash.bad{background:#fef2f2;border:1px solid #fecaca;color:#991b1b}
.epc-cata-cp .epc-cata-form-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}
.epc-cata-cp .epc-cata-check{display:flex;align-items:center;gap:8px;margin:6px 0}
.epc-cata-cp .table>thead>tr>th{background:#fef2f2;color:#7f1d1d;font-size:11px;text-transform:uppercase;letter-spacing:.04em}
@media (max-width:767px){.epc-cata-cp .epc-cata-form-grid{grid-template-columns:1fr}}
</style>

<?php if ($flash !== '') { ?>
<div class="epc-cata-flash <?php echo $flashOk ? 'ok' : 'bad'; ?>"><?php echo epc_cata_cp_h($flash); ?></div>
<?php } ?>

<div class="epc-cata-metrics">
	<div class="epc-cata-metric">
		<div class="epc-cata-metric__l">Version</div>
		<div class="epc-cata-metric__v" style="font-size:14px"><?php echo epc_cata_cp_h($status['version'] ?? EPC_CATA_VERSION); ?></div>
	</div>
	<div class="epc-cata-metric">
		<div class="epc-cata-metric__l">Manufacturers</div>
		<div class="epc-cata-metric__v"><?php echo number_format((int) ($totals['manufacturers'] ?? 0)); ?></div>
	</div>
	<div class="epc-cata-metric">
		<div class="epc-cata-metric__l">Models</div>
		<div class="epc-cata-metric__v"><?php echo number_format((int) ($totals['models'] ?? 0)); ?></div>
	</div>
	<div class="epc-cata-metric">
		<div class="epc-cata-metric__l">Modifications</div>
		<div class="epc-cata-metric__v"><?php echo number_format((int) ($totals['modifications'] ?? 0)); ?></div>
	</div>
	<div class="epc-cata-metric">
		<div class="epc-cata-metric__l">Categories</div>
		<div class="epc-cata-metric__v"><?php echo number_format((int) ($totals['categories'] ?? count($categories))); ?></div>
	</div>
	<div class="epc-cata-metric">
		<div class="epc-cata-metric__l">Articles</div>
		<div class="epc-cata-metric__v"><?php echo number_format((int) ($totals['articles'] ?? 0)); ?></div>
	</div>
</div>

<div class="row">
	<div class="col-md-5">
		<div class="hpanel">
			<div class="panel-heading hbuilt"><i class="fa fa-sliders"></i> Storefront presentation</div>
			<div class="panel-body">
				<form method="post" action="">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cata_cp_h($csrf); ?>">
					<input type="hidden" name="epc_cata_action" value="save_presentation">
					<div class="epc-cata-form-grid">
						<div class="form-group">
							<label>Default article view</label>
							<select class="form-control" name="default_article_view">
								<?php foreach (array('list' => 'List', 'card' => 'Card', 'compact' => 'Compact') as $k => $lab) { ?>
								<option value="<?php echo epc_cata_cp_h($k); ?>"<?php echo ($presentation['default_article_view'] ?? '') === $k ? ' selected' : ''; ?>><?php echo epc_cata_cp_h($lab); ?></option>
								<?php } ?>
							</select>
						</div>
						<div class="form-group">
							<label>Default vehicle section</label>
							<select class="form-control" name="default_section">
								<?php foreach (array('passenger' => 'Passenger', 'commercial' => 'Commercial', 'motorbike' => 'Motorbike') as $k => $lab) { ?>
								<option value="<?php echo epc_cata_cp_h($k); ?>"<?php echo ($presentation['default_section'] ?? '') === $k ? ' selected' : ''; ?>><?php echo epc_cata_cp_h($lab); ?></option>
								<?php } ?>
							</select>
						</div>
						<div class="form-group" style="grid-column:1 / -1">
							<label>Primary storefront catalog</label>
							<select class="form-control" name="storefront_primary">
								<?php foreach ($catalogTargets as $k => $lab) { ?>
								<option value="<?php echo epc_cata_cp_h($k); ?>"<?php echo ($presentation['storefront_primary'] ?? '') === $k ? ' selected' : ''; ?>><?php echo epc_cata_cp_h($lab); ?></option>
								<?php } ?>
							</select>
						</div>
					</div>
					<label class="epc-cata-check"><input type="checkbox" name="show_category_icons" value="1"<?php echo !empty($presentation['show_category_icons']) ? ' checked' : ''; ?>> Show category icons</label>
					<label class="epc-cata-check"><input type="checkbox" name="warehouse_only_prices" value="1"<?php echo !empty($presentation['warehouse_only_prices']) ? ' checked' : ''; ?>> Warehouse-only prices on catalog</label>
					<label class="epc-cata-check"><input type="checkbox" name="enable_vin_search" value="1"<?php echo !empty($presentation['enable_vin_search']) ? ' checked' : ''; ?>> Enable VIN search</label>
					<label class="epc-cata-check"><input type="checkbox" name="enable_plate_search" value="1"<?php echo !empty($presentation['enable_plate_search']) ? ' checked' : ''; ?>> Enable plate search</label>
					<hr>
					<strong>Sections enabled</strong>
					<?php
					$sec = is_array($presentation['sections_enabled'] ?? null) ? $presentation['sections_enabled'] : array();
					foreach (array('passenger' => 'Passenger cars', 'commercial' => 'Commercial', 'motorbike' => 'Motorbike') as $k => $lab) {
						?>
					<label class="epc-cata-check"><input type="checkbox" name="sections_enabled[<?php echo epc_cata_cp_h($k); ?>]" value="1"<?php echo !empty($sec[$k]) ? ' checked' : ''; ?>> <?php echo epc_cata_cp_h($lab); ?></label>
					<?php } ?>
					<div style="margin-top:14px">
						<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save presentation</button>
						<a class="btn btn-default" href="<?php echo epc_cata_cp_h($storefrontMod); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Preview Mod</a>
					</div>
				</form>
			</div>
		</div>

		<div class="hpanel">
			<div class="panel-heading hbuilt"><i class="fa fa-database"></i> Providers &amp; sync</div>
			<div class="panel-body">
				<?php if (!$providers) { ?>
				<p class="text-muted" style="margin:0">No live provider snapshot yet. Status uses cached sync metadata when available.</p>
				<?php } else { ?>
				<ul class="list-unstyled" style="margin:0">
					<?php foreach ($providers as $pkey => $prow) {
						$plabel = is_array($prow) ? (string) ($prow['label'] ?? $pkey) : (string) $pkey;
						$pen = is_array($prow) ? !empty($prow['enabled']) : true;
						?>
					<li style="padding:6px 0;border-bottom:1px solid #f1f5f9">
						<strong><?php echo epc_cata_cp_h($plabel); ?></strong>
						<span class="label label-<?php echo $pen ? 'success' : 'default'; ?>"><?php echo $pen ? 'enabled' : 'off'; ?></span>
					</li>
					<?php } ?>
				</ul>
				<?php } ?>
				<form method="post" action="" style="margin-top:12px">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cata_cp_h($csrf); ?>">
					<input type="hidden" name="epc_cata_action" value="refresh_status">
					<button type="submit" class="btn btn-default btn-sm"><i class="fa fa-refresh"></i> Refresh status cache</button>
				</form>
				<?php if (!empty($monitor) && is_array($monitor)) { ?>
				<p class="text-muted" style="margin:12px 0 0;font-size:12px">
					Import monitor: <?php echo epc_cata_cp_h(json_encode(array_intersect_key($monitor, array_flip(array('ok', 'phase', 'progress', 'message', 'updated_at'))), JSON_UNESCAPED_UNICODE)); ?>
				</p>
				<?php } ?>
			</div>
		</div>
	</div>

	<div class="col-md-7">
		<div class="hpanel">
			<div class="panel-heading hbuilt"><i class="fa fa-th-large"></i> Category cards</div>
			<div class="panel-body" style="padding:0">
				<form method="post" action="">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cata_cp_h($csrf); ?>">
					<input type="hidden" name="epc_cata_action" value="save_categories">
					<div class="table-responsive">
						<table class="table table-striped" style="margin:0">
							<thead>
								<tr>
									<th style="width:70px">On</th>
									<th style="width:70px">Order</th>
									<th>Category</th>
									<th>Storefront target</th>
								</tr>
							</thead>
							<tbody>
								<?php foreach ($categories as $row) {
									$strId = (string) (int) ($row['STR_ID'] ?? 0);
									$name = (string) ($row['CATEGORY_NAME'] ?? ($row['display_name'] ?? ($row['default_name'] ?? ('#' . $strId))));
									$target = epc_cata_normalize_catalog_target((string) ($row['catalog_target'] ?? 'eparts-mod'));
									?>
								<tr>
									<td>
										<input type="checkbox" name="cat_enabled[<?php echo epc_cata_cp_h($strId); ?>]" value="1"<?php echo !empty($row['enabled']) ? ' checked' : ''; ?>>
									</td>
									<td>
										<input class="form-control input-sm" type="number" name="cat_order[<?php echo epc_cata_cp_h($strId); ?>]" value="<?php echo (int) ($row['ORDER'] ?? 0); ?>" style="width:70px">
									</td>
									<td>
										<strong><?php echo epc_cata_cp_h($name); ?></strong>
										<div class="text-muted" style="font-size:11px">ID <?php echo epc_cata_cp_h($strId); ?></div>
									</td>
									<td>
										<select class="form-control input-sm" name="cat_target[<?php echo epc_cata_cp_h($strId); ?>]">
											<?php foreach ($catalogTargets as $k => $lab) { ?>
											<option value="<?php echo epc_cata_cp_h($k); ?>"<?php echo $target === $k ? ' selected' : ''; ?>><?php echo epc_cata_cp_h($lab); ?></option>
											<?php } ?>
										</select>
									</td>
								</tr>
								<?php } ?>
							</tbody>
						</table>
					</div>
					<div style="padding:12px 14px;border-top:1px solid #e2e8f0;display:flex;gap:8px;flex-wrap:wrap">
						<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save categories</button>
					</div>
				</form>
				<form method="post" action="" style="padding:0 14px 14px" onsubmit="return confirm('Reset all category overrides to defaults?');">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_cata_cp_h($csrf); ?>">
					<input type="hidden" name="epc_cata_action" value="reset_categories">
					<button type="submit" class="btn btn-default btn-sm"><i class="fa fa-undo"></i> Reset to defaults</button>
				</form>
			</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
