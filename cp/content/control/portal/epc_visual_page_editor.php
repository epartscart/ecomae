<?php
/**
 * Super CP / tenant CP — ECOM Visual Page Editor (level-first workspace).
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
$pageKey = epc_vpe_normalize_page_key((string) ($_GET['page_key'] ?? 'homepage'));

$layout = epc_vpe_layout_load($pdo, $siteKey, $pageKey);
$level = epc_vpe_level_meta($pageKey);
$levels = epc_vpe_frontend_levels();
$targets = epc_vpe_target_options($pdo);
$previewUrl = (string) ($_GET['preview'] ?? '');
if ($previewUrl === '') {
	$previewUrl = epc_vpe_resolve_preview_url($pdo, $siteKey, $pageKey);
}
$previewUrl = preg_match('#^https?://#i', $previewUrl) ? $previewUrl : 'https://www.ecomae.com/en/';
$backend = epc_scp_backend();
$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$pageUrl = '/' . $backend . '/control/portal/epc_visual_page_editor';
$lib = epc_vpe_block_library();
$brand = $layout['brand'];
$mode = (string) ($layout['mode'] ?? 'layout');
$isBrandOnly = ($mode === 'brand_only');

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
		'sub' => 'Pick a frontend level, edit blocks or brand, preview, then save draft or publish.',
		'actions' => $vpeHeroActions,
	),
));
?>

<div id="epc-vpe-app"
	data-page-key="<?php echo epc_vpe_h($pageKey); ?>"
	data-level-id="<?php echo epc_vpe_h((string) ($level['id'] ?? $pageKey)); ?>"
	data-mode="<?php echo epc_vpe_h($mode); ?>">

	<header class="epc-vpe-brandbar">
		<div class="epc-vpe-brandbar__mark"><i class="fa fa-magic" aria-hidden="true"></i></div>
		<div class="epc-vpe-brandbar__text">
			<div class="epc-vpe-brandbar__name">Visual page editor</div>
			<div class="epc-vpe-brandbar__sub">Each frontend level has its own layout — switch left, edit centre, preview right</div>
		</div>
		<div class="epc-vpe-brandbar__meta">
			<span class="epc-vpe-pill" id="epc-vpe-level-pill"><i class="fa <?php echo epc_vpe_h((string) ($level['icon'] ?? 'fa-home')); ?>"></i> <span id="epc-vpe-level-label"><?php echo epc_vpe_h((string) ($level['label'] ?? 'Homepage')); ?></span></span>
			<span class="epc-vpe-status" id="epc-vpe-status"><?php echo !empty($layout['is_published']) ? 'Published' : 'Draft'; ?></span>
		</div>
	</header>

	<div class="epc-vpe-toolbar">
		<?php if ($isSuper && count($targets) > 1) { ?>
		<label for="epc-vpe-tenant">Tenant</label>
		<select id="epc-vpe-tenant" class="form-control input-sm">
			<?php foreach ($targets as $t) { ?>
			<option value="<?php echo epc_vpe_h($t['site_key']); ?>" data-url="<?php echo epc_vpe_h($t['preview_url']); ?>"<?php echo $siteKey === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_vpe_h($t['label']); ?></option>
			<?php } ?>
		</select>
		<?php } else { ?>
		<span class="epc-vpe-tenant-fixed"><i class="fa fa-globe"></i> <?php echo epc_vpe_h($siteKey); ?></span>
		<?php } ?>

		<div class="epc-vpe-toolbar__hint">
			<span><strong>Draft</strong> saves privately</span>
			<span><strong>Publish</strong> pushes to the live storefront placement</span>
		</div>

		<div class="epc-vpe-toolbar__actions">
			<button type="button" class="btn btn-default btn-sm" id="epc-vpe-refresh-preview"><i class="fa fa-refresh"></i> Refresh</button>
			<button type="button" class="btn btn-default btn-sm" id="epc-vpe-save"><i class="fa fa-save"></i> Save draft</button>
			<button type="button" class="btn btn-primary btn-sm" id="epc-vpe-publish"><i class="fa fa-check"></i> Save &amp; publish</button>
		</div>
	</div>

	<div class="epc-vpe-workspace-grid">
		<aside class="epc-vpe-levels" aria-label="Frontend levels">
			<div class="epc-vpe-levels__head">Frontend levels</div>
			<p class="epc-vpe-levels__intro">Choose what part of the storefront you are editing.</p>
			<nav class="epc-vpe-level-list" id="epc-vpe-level-list">
				<?php foreach ($levels as $lid => $lv) {
					$active = ((string) ($level['id'] ?? '') === $lid) || ((string) ($lv['page_key'] ?? '') === $pageKey);
					?>
				<button type="button"
					class="epc-vpe-level<?php echo $active ? ' is-active' : ''; ?>"
					data-level="<?php echo epc_vpe_h($lid); ?>"
					data-page-key="<?php echo epc_vpe_h((string) ($lv['page_key'] ?? $lid)); ?>"
					data-mode="<?php echo epc_vpe_h((string) ($lv['mode'] ?? 'layout')); ?>">
					<span class="epc-vpe-level__icon"><i class="fa <?php echo epc_vpe_h((string) ($lv['icon'] ?? 'fa-file-o')); ?>"></i></span>
					<span class="epc-vpe-level__copy">
						<span class="epc-vpe-level__label"><?php echo epc_vpe_h((string) ($lv['label'] ?? $lid)); ?></span>
						<span class="epc-vpe-level__hint"><?php echo epc_vpe_h((string) ($lv['hint'] ?? '')); ?></span>
					</span>
				</button>
				<?php } ?>
			</nav>
		</aside>

		<section class="epc-vpe-panel epc-vpe-inspector" aria-label="Editor">
			<div class="epc-vpe-panel__head">
				<h4 id="epc-vpe-inspector-title"><i class="fa fa-sliders"></i> <span><?php echo $isBrandOnly ? 'Brand settings' : 'Blocks &amp; content'; ?></span></h4>
				<span class="epc-vpe-panel__count" id="epc-vpe-block-count"><?php echo count((array) $layout['blocks']); ?> blocks</span>
			</div>
			<div class="epc-vpe-panel__body">
				<div id="epc-vpe-brand-section" class="epc-vpe-section<?php echo $isBrandOnly ? '' : ' is-collapsible'; ?>" <?php echo $isBrandOnly ? '' : 'data-collapsed="1"'; ?>>
					<button type="button" class="epc-vpe-section__toggle" id="epc-vpe-brand-toggle" <?php echo $isBrandOnly ? 'style="display:none"' : ''; ?>>
						<span><i class="fa fa-paint-brush"></i> Brand colours &amp; identity</span>
						<i class="fa fa-chevron-down"></i>
					</button>
					<div class="epc-vpe-section__body" id="epc-vpe-brand-body" <?php echo $isBrandOnly ? '' : 'hidden'; ?>>
						<p class="epc-vpe-help">Maps to <code>site_settings.theme</code> on publish. Logo, tagline, and footer sync to contact settings.</p>
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
					</div>
				</div>

				<div id="epc-vpe-blocks-section" class="epc-vpe-section" <?php echo $isBrandOnly ? 'hidden' : ''; ?>>
					<div class="epc-vpe-section__head">
						<strong><i class="fa fa-th-list"></i> Content blocks</strong>
						<span class="text-muted">Drag to reorder · click a card to edit</span>
					</div>
					<ul class="epc-vpe-blocks" id="epc-vpe-blocks"></ul>
					<div class="epc-vpe-add-blocks" id="epc-vpe-add-blocks">
						<div class="epc-vpe-add-blocks__label">Add block</div>
						<?php foreach ($lib as $type => $meta) { ?>
						<button type="button" class="epc-vpe-add" data-type="<?php echo epc_vpe_h($type); ?>" title="<?php echo epc_vpe_h((string) ($meta['hint'] ?? $meta['label'])); ?>">
							<i class="fa <?php echo epc_vpe_h($meta['icon']); ?>"></i>
							<span><?php echo epc_vpe_h($meta['label']); ?></span>
						</button>
						<?php } ?>
					</div>
				</div>

				<div id="epc-vpe-brand-only-note" class="epc-vpe-empty" <?php echo $isBrandOnly ? '' : 'hidden'; ?>>
					<div class="epc-vpe-empty__icon"><i class="fa fa-paint-brush"></i></div>
					<div class="epc-vpe-empty__title">Brand global</div>
					<p>Edit colours and identity here. Publish applies theme / logo / tagline to the selected tenant storefront.</p>
				</div>
			</div>
		</section>

		<section class="epc-vpe-panel epc-vpe-preview" aria-label="Live preview">
			<div class="epc-vpe-panel__head">
				<h4><i class="fa fa-desktop"></i> Live preview</h4>
				<div class="epc-vpe-device" id="epc-vpe-device" role="group" aria-label="Preview width">
					<button type="button" class="is-active" data-device="desktop" title="Desktop"><i class="fa fa-desktop"></i></button>
					<button type="button" data-device="tablet" title="Tablet"><i class="fa fa-tablet"></i></button>
					<button type="button" data-device="mobile" title="Mobile"><i class="fa fa-mobile"></i></button>
				</div>
				<a class="btn btn-xs btn-default" id="epc-vpe-open-storefront" href="<?php echo epc_vpe_h($previewUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open</a>
			</div>
			<div class="epc-vpe-panel__body">
				<div class="epc-vpe-preview-frame-wrap is-desktop" id="epc-vpe-preview-wrap">
					<iframe id="epc-vpe-preview-frame" title="Storefront preview" data-base="<?php echo epc_vpe_h($previewUrl); ?>" src="<?php echo epc_vpe_h($previewUrl); ?>?_=<?php echo time(); ?>"></iframe>
				</div>
			</div>
		</section>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
