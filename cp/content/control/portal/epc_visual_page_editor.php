<?php
/**
 * Super CP / tenant CP — ECOM Visual Page Editor (Elementor-style MVP).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_visual_page_editor.php';

if (!epc_vpe_guard_admin()) {
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : (function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null);
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

$allowed = epc_vpe_allowed_site_keys($pdo);
$siteKey = epc_vpe_normalize_site_key((string) ($_GET['site_key'] ?? ''));
if ($siteKey === '' || !in_array($siteKey, $allowed, true)) {
	$siteKey = $allowed[0] ?? 'platform';
}

$layout = epc_vpe_layout_load($pdo, $siteKey);
$targets = epc_vpe_target_options($pdo);
$previewUrl = (string) ($_GET['preview'] ?? '');
if ($previewUrl === '') {
	$previewUrl = epc_vpe_resolve_preview_url($pdo, $siteKey);
}
$previewUrl = preg_match('#^https?://#i', $previewUrl) ? $previewUrl : 'https://www.ecomae.com/en/';
$backend = epc_scp_backend();
$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$pageUrl = '/' . $backend . '/control/portal/epc_visual_page_editor';
$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_visual_page_editor.php';
$lib = epc_vpe_block_library();
$brand = $layout['brand'];

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
$vpeHeroActions = $isSuper ? array(
	array('label' => 'Info blocks', 'icon' => 'fa-th-large', 'url' => '/' . $backend . '/control/portal/epc_super_cp_info_blocks'),
	array('label' => 'Industry settings', 'icon' => 'fa-cog', 'url' => '/' . $backend . '/control/portal/industry_settings'),
) : array();
epc_cp_page_frame_open(array(
	'class' => 'epc-vpe-workspace epc-scp-panel',
	'hero' => array(
		'badge' => $isSuper ? 'Super CP' : 'Control panel',
		'title' => 'Visual page editor',
		'sub' => 'Drag blocks, edit text and brand colours, preview the live storefront, then publish to homepage.',
		'actions' => $vpeHeroActions,
	),
));
?>

<div id="epc-vpe-app">

<div class="epc-vpe-toolbar">
	<?php if ($isSuper && count($targets) > 1) { ?>
	<label for="epc-vpe-tenant">Tenant</label>
	<select id="epc-vpe-tenant" class="form-control input-sm" style="width:auto;max-width:280px">
		<?php foreach ($targets as $t) { ?>
		<option value="<?php echo epc_vpe_h($t['site_key']); ?>" data-url="<?php echo epc_vpe_h($t['preview_url']); ?>"<?php echo $siteKey === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_vpe_h($t['label']); ?></option>
		<?php } ?>
	</select>
	<?php } else { ?>
	<span class="text-muted"><i class="fa fa-globe"></i> Editing: <strong><?php echo epc_vpe_h($siteKey); ?></strong></span>
	<?php } ?>
	<span class="epc-vpe-status" id="epc-vpe-status"><?php echo $layout['is_published'] ? 'Published' : 'Draft'; ?></span>
	<div style="margin-left:auto;display:flex;gap:8px">
		<button type="button" class="btn btn-default btn-sm" id="epc-vpe-refresh-preview"><i class="fa fa-refresh"></i> Refresh preview</button>
		<button type="button" class="btn btn-default btn-sm" id="epc-vpe-save"><i class="fa fa-save"></i> Save draft</button>
		<button type="button" class="btn btn-primary btn-sm" id="epc-vpe-publish"><i class="fa fa-check"></i> Save &amp; publish</button>
	</div>
</div>

<div class="epc-vpe-split">
	<div class="epc-vpe-panel">
		<div class="epc-vpe-panel__head"><h4><i class="fa fa-paint-brush"></i> Brand &amp; blocks</h4></div>
		<div class="epc-vpe-panel__body">
			<p class="text-muted" style="font-size:12px;margin-top:0">CSS variables map to <code>site_settings.theme</code> (primary, accent, background). Logo/tagline/footer sync on publish.</p>
			<div class="epc-vpe-brand-grid">
				<div><label for="epc-vpe-brand-primary">Primary</label><input type="color" id="epc-vpe-brand-primary" value="<?php echo epc_vpe_h($brand['primary']); ?>" /></div>
				<div><label for="epc-vpe-brand-accent">Accent</label><input type="color" id="epc-vpe-brand-accent" value="<?php echo epc_vpe_h($brand['accent']); ?>" /></div>
				<div><label for="epc-vpe-brand-background">Background</label><input type="color" id="epc-vpe-brand-background" value="<?php echo epc_vpe_h($brand['background']); ?>" /></div>
			</div>
			<div class="epc-vpe-field"><label for="epc-vpe-brand-logo_url">Logo URL</label><input class="form-control input-sm" type="text" id="epc-vpe-brand-logo_url" value="<?php echo epc_vpe_h($brand['logo_url']); ?>" placeholder="/content/files/…" /></div>
			<div class="epc-vpe-field"><label for="epc-vpe-brand-hero_headline">Hero headline (system name)</label><input class="form-control input-sm" type="text" id="epc-vpe-brand-hero_headline" value="<?php echo epc_vpe_h($brand['hero_headline']); ?>" /></div>
			<div class="epc-vpe-field"><label for="epc-vpe-brand-hero_subheadline">Hero subheadline</label><input class="form-control input-sm" type="text" id="epc-vpe-brand-hero_subheadline" value="<?php echo epc_vpe_h($brand['hero_subheadline']); ?>" /></div>
			<div class="epc-vpe-field"><label for="epc-vpe-brand-tagline">Tagline</label><input class="form-control input-sm" type="text" id="epc-vpe-brand-tagline" value="<?php echo epc_vpe_h($brand['tagline']); ?>" /></div>
			<div class="epc-vpe-field"><label for="epc-vpe-brand-footer_text">Footer text</label><input class="form-control input-sm" type="text" id="epc-vpe-brand-footer_text" value="<?php echo epc_vpe_h($brand['footer_text']); ?>" /></div>

			<h5 style="margin:16px 0 8px;font-weight:700"><i class="fa fa-th-list"></i> Homepage blocks <small class="text-muted">(drag to reorder)</small></h5>
			<ul class="epc-vpe-blocks" id="epc-vpe-blocks"></ul>
			<div class="epc-vpe-add-blocks">
				<?php foreach ($lib as $type => $meta) { ?>
				<button type="button" class="btn btn-default btn-sm epc-vpe-add" data-type="<?php echo epc_vpe_h($type); ?>"><i class="fa <?php echo epc_vpe_h($meta['icon']); ?>"></i> <?php echo epc_vpe_h($meta['label']); ?></button>
				<?php } ?>
			</div>
		</div>
	</div>

	<div class="epc-vpe-panel epc-vpe-preview">
		<div class="epc-vpe-panel__head">
			<h4><i class="fa fa-desktop"></i> Live preview</h4>
			<a class="btn btn-xs btn-default" href="<?php echo epc_vpe_h($previewUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open storefront</a>
		</div>
		<div class="epc-vpe-panel__body">
			<iframe id="epc-vpe-preview-frame" title="Storefront preview" data-base="<?php echo epc_vpe_h($previewUrl); ?>" src="<?php echo epc_vpe_h($previewUrl); ?>?_=<?php echo time(); ?>"></iframe>
		</div>
	</div>
</div>
</div>
<?php epc_cp_page_frame_close(); ?>
