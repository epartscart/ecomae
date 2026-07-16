<?php
/**
 * Platform host (ecomae.com) — ERP portal BOS-style animated hero above sign-in.
 */
defined('_ASTEXE_') or die('No access');

global $DP_Config;
$base = rtrim((string) ($DP_Config->domain_path ?? ''), '/');
$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
$superCp = $base . '/' . $backend . '/shop/tenant_hub/tenant_hub';
$platformUrl = $base . '/platform';
$erpGuide = isset($portal_home) ? $portal_home . '/guide' : $base . '/erp/guide';
?>
<div class="epc-erp-bos-hero">
	<div class="epc-erp-bos-hero__content">
		<div class="epc-erp-bos-hero__brand-mark">
			<div class="epc-erp-bos-hero__brand-icon">
				<div class="epc-erp-bos-hero__brand-icon-inner">
					<i class="fa fa-line-chart"></i>
				</div>
				<div class="epc-erp-bos-hero__brand-ring"></div>
				<div class="epc-erp-bos-hero__brand-ring epc-erp-bos-hero__brand-ring--2"></div>
			</div>
			<h1 class="epc-erp-bos-hero__title">ERP Finance</h1>
			<p class="epc-erp-bos-hero__tagline">Unified Blockchain BOS Enterprise System</p>
		</div>

		<div class="epc-erp-bos-hero__capabilities">
			<div class="epc-erp-bos-hero__cap-item epc-erp-bos-hero__cap-item--1">
				<div class="epc-erp-bos-hero__cap-icon"><i class="fa fa-calculator"></i></div>
				<div class="epc-erp-bos-hero__cap-text">
					<strong>Financial Accounting</strong>
					<span>GL, AP, AR, Bank, Fixed Assets</span>
				</div>
			</div>
			<div class="epc-erp-bos-hero__cap-item epc-erp-bos-hero__cap-item--2">
				<div class="epc-erp-bos-hero__cap-icon"><i class="fa fa-shopping-cart"></i></div>
				<div class="epc-erp-bos-hero__cap-text">
					<strong>Sales &amp; Purchase</strong>
					<span>Orders, Invoices, Quotations</span>
				</div>
			</div>
			<div class="epc-erp-bos-hero__cap-item epc-erp-bos-hero__cap-item--3">
				<div class="epc-erp-bos-hero__cap-icon"><i class="fa fa-cubes"></i></div>
				<div class="epc-erp-bos-hero__cap-text">
					<strong>Inventory &amp; Logistics</strong>
					<span>WMS, Barcode, Stock, Shipping</span>
				</div>
			</div>
			<div class="epc-erp-bos-hero__cap-item epc-erp-bos-hero__cap-item--4">
				<div class="epc-erp-bos-hero__cap-icon"><i class="fa fa-shield"></i></div>
				<div class="epc-erp-bos-hero__cap-text">
					<strong>Compliance &amp; AI</strong>
					<span>VAT, AML, E-invoice, AI Advisor</span>
				</div>
			</div>
		</div>

		<div class="epc-erp-bos-hero__stats-bar">
			<div class="epc-erp-bos-hero__stat"><span class="epc-erp-bos-hero__stat-num" data-count="95">0</span><span class="epc-erp-bos-hero__stat-label">Modules</span></div>
			<div class="epc-erp-bos-hero__stat-divider"></div>
			<div class="epc-erp-bos-hero__stat"><span class="epc-erp-bos-hero__stat-num" data-count="11">0</span><span class="epc-erp-bos-hero__stat-label">Industries</span></div>
			<div class="epc-erp-bos-hero__stat-divider"></div>
			<div class="epc-erp-bos-hero__stat"><span class="epc-erp-bos-hero__stat-num" data-count="65">0</span><span class="epc-erp-bos-hero__stat-label">Countries</span></div>
			<div class="epc-erp-bos-hero__stat-divider"></div>
			<div class="epc-erp-bos-hero__stat"><span class="epc-erp-bos-hero__stat-num" data-count="225">0</span><span class="epc-erp-bos-hero__stat-label">Tenants</span></div>
		</div>
	</div>
</div>
