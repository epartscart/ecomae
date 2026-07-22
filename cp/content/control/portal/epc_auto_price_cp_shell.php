<?php
/**
 * Auto Price AI CP — lightweight shell (hero, tabs, lazy tab body).
 */
defined('_ASTEXE_') or die('No access');

function epc_apai_cp_load_shell_modules(): void
{
	$doc = $_SERVER['DOCUMENT_ROOT'];
	require_once $doc . '/content/shop/price_engine/epc_auto_price_engine.php';
	require_once $doc . '/content/shop/price_engine/epc_apai_country_sources.php';
	require_once $doc . '/content/shop/price_engine/epc_industry_taxonomy.php';
}

/**
 * @param array<string,mixed> $ctx
 */
function epc_apai_cp_render_shell(array $ctx): void
{
	global $DP_Config;

	$siteKey = (string) ($ctx['siteKey'] ?? '');
	$tab = (string) ($ctx['tab'] ?? 'discover');
	$pageBase = (string) ($ctx['pageBase'] ?? '');
	$backend = (string) ($ctx['backend'] ?? 'cp');
	$flash = (string) ($ctx['flash'] ?? '');
	$flashClass = (string) ($ctx['flashClass'] ?? 'info');
	$isSuperCp = !empty($ctx['isSuperCp']);
	$tenantOptions = (array) ($ctx['tenantOptions'] ?? array());
	$pdo = $ctx['pdo'] ?? null;

	$tenantCountryMeta = array('label' => 'UAE', 'tld' => 'ae');
	$industryLabel = '…';
	$industryKey = 'general_retail';
	if ($pdo instanceof PDO) {
		if (function_exists('epc_apai_tenant_country') && function_exists('epc_apai_country_meta')) {
			$tenantCountryMeta = epc_apai_country_meta(epc_apai_tenant_country($siteKey, $pdo));
		}
		if (function_exists('epc_apai_resolve_industry')) {
			$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
		}
		if (function_exists('epc_apai_industry_profiles')) {
			$profiles = epc_apai_industry_profiles();
			$industryLabel = (string) (($profiles[$industryKey]['label'] ?? ucfirst(str_replace('_', ' ', $industryKey))));
		}
	}

	$tabs = array(
		'discover' => 'Discover',
		'product_lines' => 'Product lines',
		'compare' => 'Compare',
		'uae_sources' => 'Market sources',
		'imports' => 'My imports',
		'rules' => 'Rules',
		'guide' => 'Guide',
	);

	$backendRaw = trim((string) ($ctx['backendRaw'] ?? $backend), '/');
	if ($backendRaw === '') {
		$backendRaw = 'cp';
	}
	$ajaxUrl = function_exists('epc_apai_ajax_url')
		? epc_apai_ajax_url($backendRaw)
		: ('/' . $backendRaw . '/control/portal/ajax_auto_price');
	$shellCfg = array(
		'active' => true,
		'ajaxUrl' => $ajaxUrl,
		'pageBase' => $pageBase !== '' ? $pageBase : ('/' . $backendRaw . '/control/portal/epc_auto_price_engine'),
		'backend' => $backendRaw,
		'siteKey' => $siteKey,
		'tab' => $tab,
	);
	if (!empty($ctx['discoverInlined'])) {
		$shellCfg['discoverInlined'] = true;
	}
	$openTabBodyOnly = !empty($ctx['openTabBodyOnly']);
	$inlineTabHtml = (string) ($ctx['inlineTabHtml'] ?? '');
	$ver = function_exists('epc_cp_page_asset_version') ? epc_cp_page_asset_version() : '20260606apai2';
	$GLOBALS['epc_cp_page_assets']['js']['/content/general_pages/epc_auto_price_shell_js.php?v=' . rawurlencode($ver)] = true;
	if (!function_exists('epc_cp_footer_scripts_append')) {
		$epcCpRelocate = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_script_relocate.php';
		if (is_file($epcCpRelocate)) {
			require_once $epcCpRelocate;
		}
	}
	?>
<div class="col-lg-12 epc-ape-panel epc-ape-panel--shell">
	<div class="epc-ape-hero">
		<h2><i class="fa fa-magic"></i> Auto Price AI</h2>
		<p class="epc-ape-hero__subtitle"><strong>Discover · Price · Import · Sell</strong> — Market: <strong><?php echo epc_ape_h($tenantCountryMeta['label']); ?></strong> (<?php echo epc_ape_h('.' . $tenantCountryMeta['tld']); ?> sources) for <?php echo epc_ape_h($industryLabel); ?>.</p>
		<div class="epc-ape-hero__actions">
			<?php
			$guideHref = ($pageBase !== '' ? $pageBase : ('/' . $backendRaw . '/control/portal/epc_auto_price_engine'))
				. '?site_key=' . urlencode($siteKey) . '&tab=guide&apai_sync=1';
			?>
			<a class="btn btn-sm btn-default" href="<?php echo epc_ape_h($guideHref); ?>" data-apai-tab="guide"><i class="fa fa-book"></i> Guide</a>
			<a class="btn btn-sm btn-default" href="/<?php echo epc_ape_h(trim($backend, '/')); ?>/shop/catalogue/products"><i class="fa fa-th-large"></i> Catalogue</a>
		</div>
	</div>

	<?php if ($flash !== '') { ?>
	<div class="alert alert-<?php echo epc_ape_h($flashClass); ?>"><?php echo $flash; ?></div>
	<?php } ?>

	<?php if ($isSuperCp && $tenantOptions) { ?>
	<form method="get" class="form-inline" style="margin-bottom:14px">
		<input type="hidden" name="tab" value="<?php echo epc_ape_h($tab); ?>" />
		<label>Tenant</label>
		<select name="site_key" class="form-control input-sm" onchange="this.form.submit()">
			<?php foreach ($tenantOptions as $t) { ?>
			<option value="<?php echo epc_ape_h($t['site_key']); ?>"<?php echo $siteKey === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_ape_h($t['label']); ?></option>
			<?php } ?>
			<option value="platform"<?php echo $siteKey === 'platform' ? ' selected' : ''; ?>>Platform</option>
		</select>
	</form>
	<?php } ?>

	<div class="epc-ape-kpi" id="epc-apai-kpi">
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-lightbulb-o"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Suggested</div>
				<div class="epc-ape-kpi__val" data-apai-kpi="suggested">…</div>
				<div class="epc-ape-kpi__hint">awaiting review</div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-industry"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Industry</div>
				<div class="epc-ape-kpi__text" data-apai-kpi="industry_label"><?php echo epc_ape_h($industryLabel); ?></div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-sitemap"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Product lines</div>
				<div class="epc-ape-kpi__val" data-apai-kpi="tax_count">…</div>
				<div class="epc-ape-kpi__hint">taxonomy nodes</div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-folder-open"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Categories</div>
				<div class="epc-ape-kpi__val" data-apai-kpi="category_map_count">…</div>
				<div class="epc-ape-kpi__hint">catalogue maps</div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-download"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Imported</div>
				<div class="epc-ape-kpi__val" data-apai-kpi="imported">…</div>
				<div class="epc-ape-kpi__hint">in catalogue</div>
			</div>
		</div>
	</div>

	<div class="epc-ape-tabs">
		<?php
		$shellPageBase = $pageBase !== '' ? $pageBase : ('/' . $backendRaw . '/control/portal/epc_auto_price_engine');
		foreach ($tabs as $k => $label) {
			$cls = $tab === $k ? 'btn-primary' : 'btn-default';
			$tabHref = $shellPageBase . '?site_key=' . urlencode($siteKey) . '&tab=' . $k;
			echo '<a class="btn btn-sm ' . $cls . '" href="' . epc_ape_h($tabHref) . '" data-apai-tab="' . epc_ape_h($k) . '">' . epc_ape_h($label) . '</a>';
		}
		?>
	</div>

	<div id="epc-apai-tab-body" class="epc-apai-tab-body" data-apai-tab="<?php echo epc_ape_h($tab); ?>"<?php echo !empty($ctx['discoverInlined']) ? ' data-apai-inlined="1"' : ''; ?>>
		<?php if ($inlineTabHtml !== '') { ?>
		<?php echo $inlineTabHtml; ?>
		<?php } elseif (!$openTabBodyOnly) { ?>
		<div class="epc-apai-tab-loading text-center" style="padding:48px 16px">
			<i class="fa fa-spinner fa-spin fa-2x text-muted"></i>
			<p class="text-muted" style="margin-top:12px">Loading <?php echo epc_ape_h($tabs[$tab] ?? $tab); ?>…</p>
		</div>
		<?php } ?>
	<?php if (!$openTabBodyOnly) { ?>
	</div>
</div>
<?php
	$shellScript = '<script>window.EPC_APAI_SHELL=' . json_encode($shellCfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';</script>';
	if (function_exists('epc_cp_footer_scripts_append')) {
		epc_cp_footer_scripts_append($shellScript);
	} else {
		echo $shellScript;
	}
	?>
	<?php } ?>
	<?php
}
