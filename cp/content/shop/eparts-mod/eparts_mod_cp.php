<?php
/**
 * CP — EParts Mod (vehicle picker catalog) control panel.
 * Route: /cp/shop/eparts-mod
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cata_config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_partsapi_config.php';

function epc_emod_cp_h($v): string
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
	$action = (string) ($_POST['epc_emod_action'] ?? '');
	if ($csrf === '' || !hash_equals($csrf, $postedCsrf)) {
		$flash = 'Security check failed. Refresh and try again.';
	} elseif ($action === 'save_presentation') {
		$cfg = epc_cata_presentation_config();
		$cfg['storefront_primary'] = 'eparts-mod';
		$cfg['enable_vin_search'] = !empty($_POST['enable_vin_search']);
		$cfg['enable_plate_search'] = !empty($_POST['enable_plate_search']);
		$cfg['default_section'] = (string) ($_POST['default_section'] ?? $cfg['default_section']);
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
			$flash = 'EParts Mod presentation saved.';
			$flashOk = true;
		} else {
			$flash = 'Could not save settings.';
		}
	}
}

$presentation = epc_cata_presentation_config();
$configured = function_exists('epc_partsapi_credentials_configured') && epc_partsapi_credentials_configured();
$enabled = function_exists('epc_partsapi_enabled_for_request') ? epc_partsapi_enabled_for_request() : true;
$storefrontMod = rtrim((string) ($GLOBALS['DP_Config']->domain_path ?? ''), '/') . '/en/eparts-mod';
$cataCpUrl = $base . '/shop/eparts-cata';
$synUrl = $base . '/shop/manufacturers_synonyms';

epc_cp_page_frame_open(array(
	'class' => 'epc-emod-cp',
	'hero' => array(
		'badge' => 'EParts Mod',
		'title' => 'Vehicle picker catalog',
		'sub' => 'Make → model → engine flow used on the public storefront for category parts.',
		'actions' => array(
			array('url' => $storefrontMod, 'label' => 'Open storefront', 'icon' => 'fa-external-link', 'primary' => true),
			array('url' => $cataCpUrl, 'label' => 'EParts CATA CP', 'icon' => 'fa-cubes'),
			array('url' => $synUrl, 'label' => 'Manufacturer synonyms', 'icon' => 'fa-exchange'),
		),
	),
));
?>
<style>
.epc-emod-cp .epc-emod-flash{margin:0 0 14px;padding:10px 14px;border-radius:8px;font-weight:600}
.epc-emod-cp .epc-emod-flash.ok{background:#ecfdf5;border:1px solid #a7f3d0;color:#065f46}
.epc-emod-cp .epc-emod-flash.bad{background:#fef2f2;border:1px solid #fecaca;color:#991b1b}
.epc-emod-cp .epc-emod-check{display:flex;align-items:center;gap:8px;margin:6px 0}
.epc-emod-cp .epc-emod-status{display:flex;gap:10px;flex-wrap:wrap;margin:0 0 16px}
.epc-emod-cp .epc-emod-pill{display:inline-flex;align-items:center;gap:6px;padding:6px 12px;border-radius:999px;font-size:12px;font-weight:700;border:1px solid #e2e8f0;background:#fff}
.epc-emod-cp .epc-emod-pill.ok{background:#ecfdf5;border-color:#a7f3d0;color:#065f46}
.epc-emod-cp .epc-emod-pill.warn{background:#fffbeb;border-color:#fcd34d;color:#92400e}
</style>

<?php if ($flash !== '') { ?>
<div class="epc-emod-flash <?php echo $flashOk ? 'ok' : 'bad'; ?>"><?php echo epc_emod_cp_h($flash); ?></div>
<?php } ?>

<div class="epc-emod-status">
	<span class="epc-emod-pill <?php echo $enabled ? 'ok' : 'warn'; ?>">
		<i class="fa <?php echo $enabled ? 'fa-check' : 'fa-exclamation-triangle'; ?>"></i>
		Mod catalog <?php echo $enabled ? 'available' : 'disabled for this site'; ?>
	</span>
	<span class="epc-emod-pill <?php echo $configured ? 'ok' : 'warn'; ?>">
		<i class="fa <?php echo $configured ? 'fa-key' : 'fa-key'; ?>"></i>
		API credentials <?php echo $configured ? 'configured' : 'missing — set config.epc-partsapi.php'; ?>
	</span>
</div>

<div class="row">
	<div class="col-md-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt"><i class="fa fa-sliders"></i> Mod presentation</div>
			<div class="panel-body">
				<form method="post" action="">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_emod_cp_h($csrf); ?>">
					<input type="hidden" name="epc_emod_action" value="save_presentation">
					<div class="form-group">
						<label>Default vehicle section</label>
						<select class="form-control" name="default_section">
							<?php foreach (array('passenger' => 'Passenger', 'commercial' => 'Commercial', 'motorbike' => 'Motorbike') as $k => $lab) { ?>
							<option value="<?php echo epc_emod_cp_h($k); ?>"<?php echo ($presentation['default_section'] ?? '') === $k ? ' selected' : ''; ?>><?php echo epc_emod_cp_h($lab); ?></option>
							<?php } ?>
						</select>
					</div>
					<label class="epc-emod-check"><input type="checkbox" name="enable_vin_search" value="1"<?php echo !empty($presentation['enable_vin_search']) ? ' checked' : ''; ?>> Enable VIN search</label>
					<label class="epc-emod-check"><input type="checkbox" name="enable_plate_search" value="1"<?php echo !empty($presentation['enable_plate_search']) ? ' checked' : ''; ?>> Enable plate search</label>
					<hr>
					<strong>Sections enabled</strong>
					<?php
					$sec = is_array($presentation['sections_enabled'] ?? null) ? $presentation['sections_enabled'] : array();
					foreach (array('passenger' => 'Passenger cars', 'commercial' => 'Commercial', 'motorbike' => 'Motorbike') as $k => $lab) {
						?>
					<label class="epc-emod-check"><input type="checkbox" name="sections_enabled[<?php echo epc_emod_cp_h($k); ?>]" value="1"<?php echo !empty($sec[$k]) ? ' checked' : ''; ?>> <?php echo epc_emod_cp_h($lab); ?></label>
					<?php } ?>
					<div style="margin-top:14px">
						<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save</button>
						<a class="btn btn-default" href="<?php echo epc_emod_cp_h($storefrontMod); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open Mod storefront</a>
					</div>
				</form>
			</div>
		</div>
	</div>
	<div class="col-md-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt"><i class="fa fa-info-circle"></i> How it works</div>
			<div class="panel-body">
				<ol style="margin:0;padding-left:18px;line-height:1.55">
					<li>Customer opens <code>/eparts-mod</code> and picks Make → Model → Engine.</li>
					<li>Category cards open parts for that vehicle (linked from product-family home).</li>
					<li>Use <a href="<?php echo epc_emod_cp_h($cataCpUrl); ?>">EParts CATA CP</a> for category targets, icons, and sync status.</li>
					<li>Manufacturer aliases for search live under <a href="<?php echo epc_emod_cp_h($synUrl); ?>">Manufacturer synonyms</a>.</li>
				</ol>
			</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
