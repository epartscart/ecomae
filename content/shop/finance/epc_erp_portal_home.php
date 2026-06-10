<?php
/**
 * Platform host (ecomae.com) — ERP portal marketing landing above sign-in.
 */
defined('_ASTEXE_') or die('No access');

global $DP_Config;
$base = rtrim((string) ($DP_Config->domain_path ?? ''), '/');
$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
$superCp = $base . '/' . $backend . '/shop/tenant_hub/tenant_hub';
$platformUrl = $base . '/platform';
$erpGuide = isset($portal_home) ? $portal_home . '/guide' : $base . '/erp/guide';
$logo = '/content/files/images/ecomae-logo.png';
?>
<section class="epc-erp-home-hero" aria-label="ECOM AE ERP portal">
	<div class="epc-erp-home-hero__inner">
		<div class="epc-erp-home-hero__copy">
			<img class="epc-erp-home-hero__logo" src="<?php echo htmlspecialchars($logo, ENT_QUOTES, 'UTF-8'); ?>" alt="ECOM AE" width="72" height="72" />
			<p class="epc-erp-home-hero__eyebrow">E-Commerce Arab Emirates</p>
			<h1 class="epc-erp-home-hero__title">Unified ERP &amp; commerce cloud</h1>
			<p class="epc-erp-home-hero__lead">
				Finance, inventory, VAT, CRM, and operations for your team — on the same tenant data as the control panel.
				Host clients on isolated databases with Super CP, or sign in below for department access.
			</p>
			<div class="epc-erp-home-hero__cta">
				<a class="btn btn-primary btn-lg" href="#sign-in"><i class="fa fa-sign-in"></i> Department sign-in</a>
				<a class="btn btn-default btn-lg" href="<?php echo htmlspecialchars($superCp, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-cloud"></i> Super CP</a>
				<a class="btn btn-default btn-lg" href="<?php echo htmlspecialchars($platformUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-globe"></i> Platform overview</a>
			</div>
		</div>
		<div class="epc-erp-home-hero__stats">
			<div class="epc-erp-home-stat"><strong>Storefront</strong><span>Industry templates &amp; checkout</span></div>
			<div class="epc-erp-home-stat"><strong>Control panel</strong><span>Roles, orders, catalogue</span></div>
			<div class="epc-erp-home-stat"><strong>ERP Finance</strong><span>GL, VAT, payables, stock</span></div>
			<div class="epc-erp-home-stat"><strong>Super CP</strong><span>Tenant hub &amp; onboarding</span></div>
		</div>
	</div>
</section>

<section class="epc-erp-home-grid" aria-label="Product areas">
	<div class="row">
		<div class="col-md-4">
			<div class="epc-erp-home-card">
				<h3><i class="fa fa-line-chart text-primary"></i> ERP Finance</h3>
				<p>Sales, purchases, general ledger, VAT returns, inventory, fixed assets, and department dashboards.</p>
				<a href="<?php echo htmlspecialchars($erpGuide, ENT_QUOTES, 'UTF-8'); ?>">ERP guide <i class="fa fa-angle-right"></i></a>
			</div>
		</div>
		<div class="col-md-4">
			<div class="epc-erp-home-card epc-erp-home-card--accent">
				<h3><i class="fa fa-cloud text-primary"></i> Super CP</h3>
				<p>Onboard tenants, push industry packs, manage DNS-only clients, and open finance modules per site.</p>
				<a href="<?php echo htmlspecialchars($superCp, ENT_QUOTES, 'UTF-8'); ?>">Open tenant hub <i class="fa fa-angle-right"></i></a>
			</div>
		</div>
		<div class="col-md-4">
			<div class="epc-erp-home-card">
				<h3><i class="fa fa-sitemap text-primary"></i> Platform marketing</h3>
				<p>Industries, pricing, demos, and how ECOM AE delivers storefront + CP + ERP for each vertical.</p>
				<a href="<?php echo htmlspecialchars($platformUrl, ENT_QUOTES, 'UTF-8'); ?>">Explore platform <i class="fa fa-angle-right"></i></a>
			</div>
		</div>
	</div>
</section>
