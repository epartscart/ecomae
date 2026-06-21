<?php
/**
 * Auto Price AI — shared guide content (CP operator + ecomae marketing).
 * Call epc_apai_guide_render($opts) after optional context setup.
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<string,array<string,string>>
 */
function epc_apai_guide_industries(): array
{
	return array(
		'auto_parts' => array(
			'label' => 'Auto spare parts',
			'icon' => 'fa-car',
			'profile' => 'warehouse_supplier',
			'buy' => 'Wholesale B2B (spare247, autoparts.ae) + market reference',
			'sell' => 'Your catalogue, storefront, and Noon/Amazon gaps',
			'example' => 'Buy Toyota filter 1310154101 from spare247 — sell from warehouse or list on Noon where missing.',
		),
		'electronics' => array(
			'label' => 'Electronics retail',
			'icon' => 'fa-mobile',
			'profile' => 'marketplace_arbitrage',
			'buy' => 'Retailers (Sharaf DG, Jumbo, Virgin, Microless)',
			'sell' => 'Noon, Amazon.ae, eBay only',
			'example' => 'Buy a phone case from Sharaf DG — list on Noon where the SKU is missing or undercut.',
		),
		'fashion' => array(
			'label' => 'Fashion & beauty',
			'icon' => 'fa-female',
			'profile' => 'marketplace_arbitrage',
			'buy' => 'Brand retailers and department stores in your country pack',
			'sell' => 'Marketplaces + your own storefront catalogue',
			'example' => 'Track seasonal SKU prices across retailers — import winners with margin before peak demand.',
		),
		'jewellery' => array(
			'label' => 'Jewellery & luxury',
			'icon' => 'fa-diamond',
			'profile' => 'marketplace_arbitrage',
			'buy' => 'Benchmark listings and authorised dealers',
			'sell' => 'Your boutique catalogue + marketplace cross-list',
			'example' => 'Compare gold-piece pricing across sources — import with photos and specs intact.',
		),
		'tax_advisory' => array(
			'label' => 'Tax & professional services',
			'icon' => 'fa-briefcase',
			'profile' => 'professional_services',
			'buy' => 'Competitor consultancies, Big4 benchmarks, FTA references',
			'sell' => 'Your service catalogue and client quotes (not Noon/Amazon)',
			'example' => 'Benchmark VAT filing packages against mid-tier firms — import winning packages to your catalogue.',
		),
		'general_retail' => array(
			'label' => 'General retail',
			'icon' => 'fa-shopping-bag',
			'profile' => 'marketplace_arbitrage',
			'buy' => 'Country pack retailers + your custom vendors',
			'sell' => 'Marketplaces configured on Rules + storefront',
			'example' => 'Discover margin on everyday SKUs — one-click import with cost and sell targets set.',
		),
	);
}

/**
 * @param array<string,mixed> $opts
 */
function epc_apai_guide_render(array $opts = array()): void
{
	$mode = (string) ($opts['mode'] ?? 'operator');
	$isMarketing = ($mode === 'marketing');
	$wrap = $isMarketing ? 'epm-apai' : 'epc-ape-guide';
	$ctx = (array) ($opts['ctx'] ?? array());
	$siteKey = (string) ($opts['site_key'] ?? ($ctx['site_key'] ?? 'electronicae'));
	$profile = (string) ($opts['profile'] ?? ($ctx['profile'] ?? 'marketplace_arbitrage'));
	$profileLabel = (string) ($opts['profile_label'] ?? '');
	if ($profileLabel === '' && function_exists('epc_ape_profiles')) {
		$profiles = epc_ape_profiles();
		$profileLabel = (string) ($profiles[$profile]['label'] ?? $profile);
	}
	$industryKey = (string) ($opts['industry_key'] ?? ($ctx['industry_key'] ?? 'electronics'));
	$industryLabel = (string) ($opts['industry_label'] ?? ($ctx['industry_label'] ?? 'Universal'));
	$tenantLabel = (string) ($opts['tenant_label'] ?? ($ctx['tenant_label'] ?? ucfirst($siteKey)));
	$tenantCountryCode = (string) ($opts['country_code'] ?? 'AE');
	$tenantCountryLabel = (string) ($opts['country_label'] ?? 'United Arab Emirates');
	$marginPct = (string) ($opts['margin_pct'] ?? '12');
	$isSuperCp = !empty($opts['is_super_cp']);
	$isWarehouse = ($profile === 'warehouse_supplier');
	$isArbitrage = ($profile === 'marketplace_arbitrage');
	$isProfessional = ($profile === 'professional_services' || $industryKey === 'tax_advisory');
	$urls = (array) ($opts['urls'] ?? ($ctx['urls'] ?? array()));
	$discKpi = (array) ($opts['disc_kpi'] ?? array());
	$demo = (array) ($opts['demo'] ?? ($ctx['demo'] ?? array()));
	$engineUrl = (string) ($opts['engine_url'] ?? ($urls['engine_super'] ?? $urls['engine_tenant'] ?? ''));
	$discoverUrl = $engineUrl !== '' ? $engineUrl . (strpos($engineUrl, '?') !== false ? '&' : '?') . 'tab=discover' : '';
	$sourcesUrl = $engineUrl !== '' ? preg_replace('/tab=[^&]*/', 'tab=uae_sources', $discoverUrl) : '';
	if ($sourcesUrl === $discoverUrl && $engineUrl !== '') {
		$sourcesUrl = $engineUrl . (strpos($engineUrl, '?') !== false ? '&' : '?') . 'tab=uae_sources';
	}
	$rulesUrl = $engineUrl !== '' ? $engineUrl . (strpos($engineUrl, '?') !== false ? '&' : '?') . 'tab=rules' : '';
	$compareUrl = (string) ($opts['compare_url'] ?? ($urls['compare_super'] ?? $urls['compare_tenant'] ?? ''));
	$importsUrl = $engineUrl !== '' ? $engineUrl . (strpos($engineUrl, '?') !== false ? '&' : '?') . 'tab=imports' : '';
	$productLinesUrl = $engineUrl !== '' ? $engineUrl . (strpos($engineUrl, '?') !== false ? '&' : '?') . 'tab=product_lines' : '';
	$tenantFeaturesUrl = (string) ($opts['tenant_features_url'] ?? '');
	$runCrawlUrl = (string) ($opts['run_crawl_url'] ?? '');
	$backend = (string) ($opts['backend'] ?? 'cp');
	$h = function_exists('epc_ape_h') ? 'epc_ape_h' : 'htmlspecialchars';
	$base = function_exists('epc_ecomae_platform_base_url') ? epc_ecomae_platform_base_url() : '/';
	$demoDays = 3;
	if (function_exists('epc_ecomae_platform_demo_package')) {
		$demoDays = (int) (epc_ecomae_platform_demo_package()['days'] ?? 3);
	}
	// Operator panel mixes PHP and HTML — capture via ob_start before this function emits HTML (CP eval-safe).
	$operatorHtml = '';
	if (!$isMarketing) {
		$operatorPath = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_guide_operator.php';
		if (is_file($operatorPath)) {
			ob_start();
			try {
				include $operatorPath;
				$operatorHtml = (string) ob_get_clean();
			} catch (Throwable $operatorErr) {
				if (ob_get_level() > 0) {
					ob_end_clean();
				}
				$operatorHtml = '<div class="alert alert-danger">Guide operator panel failed: '
					. htmlspecialchars($operatorErr->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
			}
		} else {
			$operatorHtml = '<div class="alert alert-danger">Guide operator file missing: '
				. htmlspecialchars($operatorPath, ENT_QUOTES, 'UTF-8') . '</div>';
		}
	}
	?>
<div class="<?php echo $h($wrap); ?><?php echo $isMarketing ? ' epm-apai--marketing' : ''; ?>">
	<?php if (!$isMarketing && $isSuperCp) { ?>
	<div class="epc-ape-guide__platform">
		<h4><i class="fa fa-cloud"></i> Super CP — platform operator view</h4>
		<p>You are viewing tenant <strong><?php echo $h($tenantLabel); ?></strong> from Super CP. Use the tenant selector above to switch sites. This guide adapts to each tenant&rsquo;s industry profile.</p>
		<p>Full platform documentation: <code>docs/guides/AUTO_PRICE_AI.md</code> in the deploy repo — architecture, cron, country packs, AJAX API, and deploy checklist.</p>
		<p><a href="<?php echo $h($engineUrl); ?>">Open engine for <?php echo $h($tenantLabel); ?></a>
			<?php if ($siteKey !== 'epartscart') { ?> · <a href="<?php echo $h(($urls['engine_super'] ?? '') . (strpos((string) ($urls['engine_super'] ?? ''), '?') !== false ? '&' : '?') . 'site_key=epartscart&tab=guide'); ?>">eParts Cart guide</a><?php } ?>
			<?php if ($siteKey !== 'electronicae') { ?> · <a href="<?php echo $h(($urls['engine_super'] ?? '') . (strpos((string) ($urls['engine_super'] ?? ''), '?') !== false ? '&' : '?') . 'site_key=electronicae&tab=guide'); ?>">Electronicae guide</a><?php } ?>
		</p>
	</div>
	<?php } ?>

	<?php if (!$isMarketing) { ?>
	<div class="epc-ape-guide__hero">
		<h3><i class="fa fa-book"></i> Auto Price AI — <?php echo $h($tenantLabel); ?></h3>
		<p><strong>Discover · Price · Import · Sell</strong> — industry: <?php echo $h($industryLabel); ?> · profile: <?php echo $h($profileLabel); ?>. Compare the same product across market sources, find real margin, import to your catalogue, and list on your sell marketplaces.</p>
		<p style="margin-top:10px;font-size:12px;opacity:.9"><strong>Sources grow automatically:</strong> platform pack updates + daily sync + your hourly crawl (when enabled). <strong>Marketplaces:</strong> global (eBay, Amazon.com) + your country (Noon, Amazon.<?php echo $h(strtolower($tenantCountryCode)); ?>, etc.). Scheduled crons and Quick crawl refresh prices — not infinite live scraping 24/7.</p>
		<p style="margin-top:8px;font-size:12px"><a href="https://www.ecomae.com/platform/auto-price-ai" target="_blank" rel="noopener">Public overview for prospects →</a></p>
	</div>
	<?php } ?>

	<?php if (!$isMarketing && !empty($discKpi)) { ?>
	<div class="epc-ape-guide__cards">
		<div class="epc-ape-guide__card">
			<h4><i class="fa fa-compass"></i> Discover queue</h4>
			<p><?php echo (int) ($discKpi['suggested'] ?? 0); ?> suggested · <?php echo (int) ($discKpi['imported'] ?? 0); ?> imported · <?php echo (int) ($discKpi['sources'] ?? 0); ?> sources</p>
			<?php if ($discoverUrl !== '') { ?><p><a href="<?php echo $h($discoverUrl); ?>">Open Discover tab</a></p><?php } ?>
		</div>
		<div class="epc-ape-guide__card">
			<h4><i class="fa fa-sitemap"></i> Product lines</h4>
			<p><?php echo (int) ($discKpi['taxonomy_nodes'] ?? 0); ?> taxonomy nodes for <?php echo $h($industryLabel); ?>.</p>
			<?php if ($productLinesUrl !== '') { ?><p><a href="<?php echo $h($productLinesUrl); ?>">Product lines tab</a></p><?php } ?>
		</div>
		<div class="epc-ape-guide__card">
			<h4><i class="fa fa-table"></i> Compare matrix</h4>
			<p><?php echo !empty($demo['in_matrix']) ? 'Live demo row seeded — buy sources vs marketplace sell + margin.' : 'Import products or run crawl to populate matrix.'; ?></p>
			<?php if ($compareUrl !== '') { ?><p><a href="<?php echo $h($compareUrl); ?>">Compare tab</a></p><?php } ?>
		</div>
		<div class="epc-ape-guide__card">
			<h4><i class="fa fa-percent"></i> Margin rule</h4>
			<p>Min margin <?php echo $h($marginPct); ?>% · margin = marketplace sell − buy lowest.</p>
			<?php if ($rulesUrl !== '') { ?><p><a href="<?php echo $h($rulesUrl); ?>">Rules tab</a></p><?php } ?>
		</div>
	</div>
	<?php } ?>

	<?php if ($isMarketing) { ?>
	<h2 class="epm-section-title">What Auto Price AI does for your business</h2>
	<p class="epm-section-lead">Multi-source price intelligence built into ECOM AE — discover products across retailers and wholesalers, compare buy cost vs marketplace sell price, import winners to your catalogue, and list on Noon, Amazon, or eBay. Your tenant profile controls how margin and opportunities are interpreted.</p>
	<?php } ?>

	<div class="<?php echo $isMarketing ? 'epm-grid epm-apai__profiles' : 'epc-ape-guide__ref'; ?>" style="<?php echo $isMarketing ? '' : 'margin-bottom:18px'; ?>">
		<?php if ($isMarketing) { ?>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-industry text-primary"></i> Warehouse supplier</h4>
			<p><strong>Typical:</strong> auto parts distributors (eParts Cart). Buy wholesale, hold stock, sell from your catalogue and storefront. Also finds marketplace gaps on Noon/Amazon.</p>
			<p style="font-size:13px;color:var(--epm-muted);margin:0"><strong>Buy from:</strong> B2B portals (spare247) · <strong>Sell on:</strong> your shop + marketplaces</p>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-tags text-primary"></i> Marketplace arbitrage</h4>
			<p><strong>Typical:</strong> electronics retailers (Electronicae). Buy from retail stores when price is low, list only on marketplaces where the product is missing or undercut.</p>
			<p style="font-size:13px;color:var(--epm-muted);margin:0"><strong>Buy from:</strong> Sharaf DG, Jumbo · <strong>Sell on:</strong> Noon, Amazon, eBay only</p>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-briefcase text-primary"></i> Professional services</h4>
			<p><strong>Typical:</strong> tax advisory (Taxofinca). Benchmark service packages against competitors and FTA guidance — not retail arbitrage.</p>
			<p style="font-size:13px;color:var(--epm-muted);margin:0"><strong>Buy from:</strong> competitor rates · <strong>Sell on:</strong> your service catalogue</p>
		</div>
		<?php } else { ?>
		<h4 style="margin:0 0 10px;font-size:14px;color:#0f766e"><i class="fa fa-briefcase"></i> Your business model</h4>
		<table class="table table-condensed table-bordered" style="font-size:12px;margin:0">
			<thead><tr><th>Profile</th><th>Typical tenant</th><th>Buy from</th><th>Sell on</th></tr></thead>
			<tbody>
				<tr<?php echo $isWarehouse ? ' style="background:#ecfdf5"' : ''; ?>>
					<td><strong>warehouse_supplier</strong></td>
					<td>eParts Cart</td>
					<td>Wholesale / B2B (spare247, autoparts.ae)</td>
					<td>Catalogue &amp; storefront; Noon/Amazon gaps</td>
				</tr>
				<tr<?php echo $isArbitrage ? ' style="background:#ecfdf5"' : ''; ?>>
					<td><strong>marketplace_arbitrage</strong></td>
					<td>Electronicae</td>
					<td>Retailers (Sharaf DG, Jumbo, Virgin)</td>
					<td><strong>Only</strong> Noon, Amazon.ae, eBay</td>
				</tr>
				<tr<?php echo $isProfessional ? ' style="background:#ecfdf5"' : ''; ?>>
					<td><strong>professional_services</strong></td>
					<td>Taxofinca</td>
					<td>Competitor consultancies, FTA benchmarks</td>
					<td>Service catalogue (not Noon/Amazon)</td>
				</tr>
			</tbody>
		</table>
		<p style="font-size:12px;margin:10px 0 0">Current profile: <strong><?php echo $h($profileLabel); ?></strong></p>
		<?php } ?>
	</div>

	<?php if ($isMarketing) { ?>
	<h2 class="epm-section-title">How it works — three steps</h2>
	<p class="epm-section-lead">From market scan to listed SKU — every import requires your approval. Nothing auto-publishes.</p>
	<div class="epm-grid epm-apai__steps">
		<div class="epm-card">
			<span class="epm-apai__step-num">1</span>
			<h4><i class="fa fa-compass text-primary"></i> Discover</h4>
			<p>Crawl your country&rsquo;s buy sources and sell marketplaces. Cards show buy lowest, marketplace sell target, and margin — green when above your rule.</p>
		</div>
		<div class="epm-card">
			<span class="epm-apai__step-num">2</span>
			<h4><i class="fa fa-download text-primary"></i> Import</h4>
			<p>One click adds the product to your catalogue with cost = lowest buy and sell = marketplace target. Photos and specs copy across.</p>
		</div>
		<div class="epm-card">
			<span class="epm-apai__step-num">3</span>
			<h4><i class="fa fa-shopping-cart text-primary"></i> Sell</h4>
			<p>Publish on your storefront and cross-list to Noon, Amazon, or eBay. Compare tab tracks margins on imported SKUs over time.</p>
		</div>
	</div>
	<?php } ?>

	<div class="epc-ape-guide__flow" aria-label="Auto Price AI workflow" style="<?php echo $isMarketing ? 'margin:24px 0' : ''; ?>">
		<?php
		$flowSteps = $isMarketing
			? array(
				array('num' => '1', 'icon' => 'fa-compass', 'label' => 'Discover', 'hint' => 'Scan market sources'),
				array('num' => '2', 'icon' => 'fa-download', 'label' => 'Import', 'hint' => 'Add to catalogue'),
				array('num' => '3', 'icon' => 'fa-shopping-cart', 'label' => 'Sell', 'hint' => 'List on marketplaces'),
			)
			: array(
				array('num' => '1', 'icon' => 'fa-toggle-on', 'label' => 'Enable', 'hint' => 'Turn on module'),
				array('num' => '2', 'icon' => 'fa-globe', 'label' => 'Add sources', 'hint' => $industryKey === 'auto_parts' ? 'spare247 login' : 'Review seeded sites'),
				array('num' => '3', 'icon' => 'fa-refresh', 'label' => 'Crawl', 'hint' => 'Quick or full run'),
				array('num' => '4', 'icon' => 'fa-search', 'label' => 'Review', 'hint' => 'Check margin badges'),
				array('num' => '5', 'icon' => 'fa-download', 'label' => 'Import', 'hint' => 'Add to catalogue'),
				array('num' => '6', 'icon' => 'fa-shopping-cart', 'label' => 'List', 'hint' => 'Noon · Amazon · eBay'),
			);
		foreach ($flowSteps as $step) {
			?>
		<div class="epc-ape-guide__flow-step">
			<span class="epc-ape-guide__flow-num"><?php echo $h($step['num']); ?></span>
			<i class="fa <?php echo $h($step['icon']); ?> epc-ape-guide__flow-icon"></i>
			<span class="epc-ape-guide__flow-label"><?php echo $h($step['label']); ?></span>
			<span class="epc-ape-guide__flow-hint"><?php echo $h($step['hint']); ?></span>
		</div>
			<?php
		}
		?>
	</div>

	<?php
	$diagramClass = 'epc-ape-guide__diagram--arbitrage';
	$diagramIcon = 'fa-mobile';
	$diagramTitle = 'Marketplace arbitrage flow';
	if ($isWarehouse || $industryKey === 'auto_parts') {
		$diagramClass = 'epc-ape-guide__diagram--warehouse';
		$diagramIcon = 'fa-car';
		$diagramTitle = 'Warehouse & parts flow';
	} elseif ($isProfessional) {
		$diagramClass = 'epc-ape-guide__diagram--professional';
		$diagramIcon = 'fa-briefcase';
		$diagramTitle = 'Service benchmarking flow';
	}
	?>
	<div class="epc-ape-guide__diagram <?php echo $h($diagramClass); ?>">
		<h4><i class="fa <?php echo $h($diagramIcon); ?>"></i> <?php echo $isMarketing ? 'Critical rule — buy sources vs sell marketplaces' : $h($diagramTitle . ' — ' . $tenantLabel); ?></h4>
		<?php if ($isArbitrage || ($isMarketing && !$isWarehouse && !$isProfessional)) { ?>
		<div class="epc-ape-guide__diagram-track">
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--buy"><i class="fa fa-shopping-bag"></i> Buy sources<br><small>Sharaf DG · Jumbo · Virgin</small></div>
			<span class="epc-ape-guide__diagram-arrow">→</span>
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--catalogue"><i class="fa fa-archive"></i> Your catalogue<br><small>Cost = lowest buy</small></div>
			<span class="epc-ape-guide__diagram-arrow">→</span>
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--sell"><i class="fa fa-tags"></i> Sell marketplaces<br><small>Noon · Amazon · eBay</small></div>
		</div>
		<p class="epc-ape-guide__diagram-plain"><strong>Plain example:</strong> You buy an iPhone case from Sharaf DG for 45 AED — you list it on Noon for 79 AED. Margin = 34 AED. Sharaf DG is <em>never</em> where you sell — it is a buy source only.</p>
		<?php } elseif ($isWarehouse || $industryKey === 'auto_parts') { ?>
		<div class="epc-ape-guide__diagram-track">
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--buy"><i class="fa fa-industry"></i> Wholesale buy<br><small>spare247 · autoparts.ae</small></div>
			<span class="epc-ape-guide__diagram-arrow">→</span>
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--catalogue"><i class="fa fa-cubes"></i> Warehouse stock<br><small>Brand + article match</small></div>
			<span class="epc-ape-guide__diagram-arrow">→</span>
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--sell"><i class="fa fa-store"></i> Sell<br><small>Storefront + Noon/Amazon</small></div>
		</div>
		<p class="epc-ape-guide__diagram-plain"><strong>Plain example:</strong> Buy Toyota filter <code>1310154101</code> from spare247 for 120 AED — sell from warehouse at 165 AED, or list on Noon where missing. Always search <strong>brand + part number</strong>.</p>
		<?php } else { ?>
		<div class="epc-ape-guide__diagram-track">
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--buy"><i class="fa fa-line-chart"></i> Benchmark sources<br><small>PwC · EY · tax.gov.ae</small></div>
			<span class="epc-ape-guide__diagram-arrow">→</span>
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--service"><i class="fa fa-file-text-o"></i> Service catalogue<br><small>Fixed-fee packages</small></div>
			<span class="epc-ape-guide__diagram-arrow">→</span>
			<div class="epc-ape-guide__diagram-node epc-ape-guide__diagram-node--catalogue"><i class="fa fa-users"></i> Client quotes<br><small>Storefront booking</small></div>
		</div>
		<p class="epc-ape-guide__diagram-plain"><strong>Plain example:</strong> Compare VAT filing packages against mid-tier firms — import a winning package to your catalogue. No Noon/Amazon arbitrage for tax advisory.</p>
		<?php } ?>
	</div>

	<?php if ($isMarketing) { ?>
	<h2 class="epm-section-title">Country-aware markets</h2>
	<p class="epm-section-lead">Your tenant country drives buy sources and local sell marketplaces. UAE tenants get Noon and Amazon.ae; Oman gets Lulu and Extra; Pakistan gets Daraz — global channels (eBay, Amazon.com) install for all tenants.</p>
	<?php } ?>
	<div class="<?php echo $isMarketing ? 'epm-split' : 'epc-ape-guide__section-body'; ?>" style="<?php echo $isMarketing ? '' : 'padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;margin-bottom:18px'; ?>">
		<?php if (!$isMarketing) { ?><h4 style="margin:0 0 8px;font-size:14px;color:#0f766e"><i class="fa fa-globe"></i> Your market is your country</h4><?php } ?>
		<p style="<?php echo $isMarketing ? '' : 'font-size:12px;margin:0 0 8px'; ?>"><?php if (!$isMarketing) { ?>Current country: <strong><?php echo $h($tenantCountryLabel); ?></strong> (<code><?php echo $h($tenantCountryCode); ?></code>). <?php } ?>Buy sources come from your country pack + industry overlay. Sell marketplaces = global (eBay, Amazon.com) + local (Noon, Amazon.<?php echo $h(strtolower($tenantCountryCode)); ?>&hellip;).</p>
		<?php if ($isMarketing) { ?>
		<div class="epm-card">
			<h4><i class="fa fa-map-marker text-primary"></i> Example — UAE (AE)</h4>
			<p style="margin:0;font-size:14px">Buy: Sharaf DG, Jumbo, spare247, noon.com · Sell: Noon, Amazon.ae, eBay, Dubizzle</p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-map-marker text-primary"></i> Scales to OM, PK, IN&hellip;</h4>
			<p style="margin:0;font-size:14px">New tenants get the correct country pack on first open. Custom vendors you add are preserved across platform syncs.</p>
		</div>
		<?php } else { ?>
		<ul style="font-size:12px;margin:8px 0 0;padding-left:18px">
			<li><strong>Sources grow automatically</strong> — platform taxonomy updates → daily source expand → hourly crawl (when enabled).</li>
			<li><strong>New tenant onboarding</strong> — first APAI open installs country pack + sell marketplaces.</li>
		</ul>
		<?php } ?>
	</div>

	<?php if ($isMarketing) { ?>
	<h2 class="epm-section-title">Continuous growth — sources &amp; marketplaces expand</h2>
	<p class="epm-section-lead">Auto Price AI is not a one-time scrape. Platform pack updates, daily sync crons, and optional hourly Quick crawl keep prices fresh. Your custom domains on Market sources are never overwritten.</p>
	<ul class="epm-feature-list">
		<li><strong>Platform pack updates</strong> — new retailers and marketplaces added weekly</li>
		<li><strong>Daily source expand</strong> — merges latest country + industry sources per tenant</li>
		<li><strong>Hourly Quick crawl</strong> — refreshes prices on enabled sources (when auto-crawl is on)</li>
		<li><strong>Discover queue grows</strong> — new product suggestions after each crawl pass</li>
	</ul>
	<?php } ?>

	<h2 class="<?php echo $isMarketing ? 'epm-section-title' : 'epc-ape-guide__mock-wrap'; ?>" style="<?php echo $isMarketing ? '' : 'margin:0 0 10px;font-size:14px;color:#0f766e'; ?>">
		<?php if ($isMarketing) { ?>Industries supported<?php } else { ?><i class="fa fa-th"></i> Industries supported<?php } ?>
	</h2>
	<?php if ($isMarketing) { ?><p class="epm-section-lead">One module adapts to your vertical — taxonomy, sources, and profile preset install on first engine load.</p><?php } ?>
	<div class="epc-ape-guide__legend" style="margin-bottom:18px">
		<?php foreach (epc_apai_guide_industries() as $iKey => $ind) { ?>
		<div class="epc-ape-guide__legend-card epc-ape-guide__legend-card--green" style="<?php echo ($iKey === $industryKey && !$isMarketing) ? 'box-shadow:0 0 0 2px #0d9488' : ''; ?>">
			<h5><i class="fa <?php echo $h($ind['icon']); ?>"></i> <?php echo $h($ind['label']); ?></h5>
			<p><?php echo $h($ind['example']); ?></p>
			<?php if ($isMarketing) { ?>
			<p style="font-size:10px;margin:4px 0 0;color:#64748b"><strong>Profile:</strong> <?php echo $h(str_replace('_', ' ', $ind['profile'])); ?></p>
			<?php } ?>
		</div>
		<?php } ?>
	</div>

	<?php if (!$isMarketing && $operatorHtml !== '') {
		echo $operatorHtml;
	} ?>
</div>
	<?php
}
