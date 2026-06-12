<?php
/**
 * ecomae.com homepage marketing sections (after animated hub hero).
 * Source: ecomae_complete.html — adapted to platform theme.
 */
defined('_ASTEXE_') or die('No access');

function epc_ecomae_home_sections_img_base()
{
	return 'https://skyagent-artifacts.skywork.ai/router/agent/2026-06-08/prod_agent_49d6676e-ad03-4cf8-a857-2305c7b5c262';
}

function epc_ecomae_home_sections_enqueue()
{
	static $done = false;
	if ($done) {
		return '';
	}
	$done = true;
	return '<link rel="preconnect" href="https://fonts.googleapis.com" />' . "\n"
		. '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />' . "\n"
		. '<link href="https://fonts.googleapis.com/css2?family=Syne:wght@400;600;700;800&family=DM+Sans:opsz,wght@9..40,300;9..40,400;9..40,500;9..40,600;9..40,700&display=swap" rel="stylesheet" />' . "\n"
		. '<link rel="stylesheet" href="/epc-static.php?f=content/general_pages/epc_ecomae_home_sections.css&v=20260608b" />' . "\n";
}

/**
 * @return string HTML for all post-hero marketing sections
 */
function epc_ecomae_home_sections_render($base = null, $superCp = null, $demoDays = 3)
{
	if ($base === null) {
		$base = epc_ecomae_platform_base_url();
	}
	if ($superCp === null) {
		$superCp = epc_ecomae_platform_super_cp_url();
	}
	$demoDays = (int) $demoDays;
	$demoUrl = $base . 'platform/demo';
	$platformUrl = $base . 'platform';
	$industriesUrl = $base . 'platform/industries';
	$img = epc_ecomae_home_sections_img_base();
	$h = 'epc_ecomae_h';

	$themeTag = '';
	$demoBlock = '';
	$demoDir = dirname(__DIR__) . '/shop/finance';
	if (is_file($demoDir . '/epc_erp_theme.php')) {
		require_once $demoDir . '/epc_erp_theme.php';
		if (function_exists('epc_theme_style_tag_for_surface')) {
			$themeTag = epc_theme_style_tag_for_surface('marketing');
		}
	}
	if (is_file($demoDir . '/epc_demo_portal.php')) {
		if (is_file($demoDir . '/epc_erp_demo.php')) {
			require_once $demoDir . '/epc_erp_demo.php';
		}
		require_once $demoDir . '/epc_demo_portal.php';
		if (function_exists('epc_demo_portal_html')) {
			$demoBlock = epc_demo_portal_html(rtrim($base, '/'));
		}
	}

	$cpScreens = array(
		array('id' => 'cp0', 'tab' => 'Dashboard', 'img' => 'cp_01_dashboard_30cbbb4714994afc9968d85c50f433cd.png', 'alt' => 'Super CP Dashboard', 'cap' => '<strong>Operator Console Dashboard.</strong> Live tenants, CP modules, and sandbox demos — quick-action tiles for every cross-tenant workflow.'),
		array('id' => 'cp1', 'tab' => 'Tenant Hub', 'img' => 'cp_02_tenant_hub_9b5e91773a67434fbf300181dbfec704.png', 'alt' => 'Tenant Hub', 'cap' => '<strong>Tenant Hub &amp; DNS.</strong> Platform IP, DNS counts, client intro form, A-record guide, SSL issuance — all operator-driven.'),
		array('id' => 'cp2', 'tab' => 'Onboarding', 'img' => 'cp_04_onboarding_642e2167134c416a8555f67e28bf7569.png', 'alt' => 'Client Onboarding', 'cap' => '<strong>Client Onboarding Form.</strong> One form provisions storefront, ERP, and CRM on isolated MySQL — form to stack in 24 hours.'),
		array('id' => 'cp3', 'tab' => 'Health Check', 'img' => 'cp_03_platform_health_13f2374391ca4c73b0491bf25740c1cd.png', 'alt' => 'Platform Health', 'cap' => '<strong>Platform Health Checkup.</strong> Probes across tenants — URL response, SSL expiry, DB isolation, backup age. Export CSV for SLA reporting.'),
		array('id' => 'cp4', 'tab' => 'Industry', 'img' => 'cp_05_industry_0a31163a2d594abaa1fc6364904e626e.png', 'alt' => 'Industry Settings', 'cap' => '<strong>Industry &amp; Module Settings.</strong> Select vertical, visual theme, company names — deploy the full module pack in one step.'),
		array('id' => 'cp5', 'tab' => 'Tenant Control', 'img' => 'cp_06_tenant_control_fa554969a8444633bfaf793f94980b38.png', 'alt' => 'Tenant Control', 'cap' => '<strong>Tenant Control Center.</strong> Set credentials per tenant, mark demos, manage access — drill from stats to individual tenants.'),
		array('id' => 'cp6', 'tab' => 'Visual Editor', 'img' => 'cp_07_visual_editor_9c6d17340ece4513bff2baa3bac58a51.png', 'alt' => 'Visual Editor', 'cap' => '<strong>Visual Page Editor.</strong> Brand colours, headlines, live preview — real-time storefront design from the operator console.'),
	);

	ob_start();
	echo epc_ecomae_home_sections_enqueue();
	?>
<!-- ECOMAE-HOME-SECTIONS-v1 -->
<?php echo $themeTag; ?>
<div class="ehm-home" id="ehm-home-sections">
	<section id="trust" class="ehm-trust" aria-label="Platform trust signals">
		<div class="ehm-wrap">
			<div class="ehm-trust-row">
				<div class="ehm-ti"><span class="ehm-ti-ico">⚡</span><span>Go live in <strong>24 hours</strong></span></div>
				<div class="ehm-ti"><span class="ehm-ti-ico">🔒</span><span>Isolated <strong>MySQL per tenant</strong></span></div>
				<div class="ehm-ti"><span class="ehm-ti-ico">📋</span><span><strong>UAE e-invoice</strong> (PINT-AE) built-in</span></div>
				<div class="ehm-ti"><span class="ehm-ti-ico">🏭</span><span><strong>19+ industry</strong> packs ready</span></div>
				<div class="ehm-ti"><span class="ehm-ti-ico">🌍</span><span><strong>195+</strong> tax jurisdictions</span></div>
				<div class="ehm-ti"><span class="ehm-ti-ico">🤖</span><span><strong>AI advisor</strong> &amp; forecasting</span></div>
			</div>
		</div>
	</section>
	<div class="ehm-divider"></div>
	<?php echo $demoBlock; ?>

	<section id="platform" class="ehm-sec">
		<div class="ehm-wrap">
			<div class="ehm-plat-grid">
				<div class="ehm-rev">
					<div class="ehm-tag"><span class="ehm-dot"></span>Platform Architecture</div>
					<h2 style="margin:14px 0">One unified<br><span class="ehm-glow">data layer.</span></h2>
					<p style="font-size:14px;color:var(--ehm-mid);line-height:1.7;max-width:460px;margin:0">Stop reconciling across disconnected tools. ECOM AE keeps storefront orders, GL entries, and VAT posting in one database — zero manual sync.</p>
					<div class="ehm-layer-stack">
						<div class="ehm-layer"><div class="ehm-l-num">1</div><div class="ehm-l-body"><h4>Commerce Layer</h4><p>Storefront, catalogue, checkout, B2B portal, multi-currency payments, WhatsApp.</p></div></div>
						<div class="ehm-layer"><div class="ehm-l-num">2</div><div class="ehm-l-body"><h4>Operations Layer</h4><p>Inventory ledger with lot, serial and barcode traceability, warehouses, procurement, RMA, delivery workflows.</p></div></div>
						<div class="ehm-layer"><div class="ehm-l-num">3</div><div class="ehm-l-body"><h4>Finance &amp; Compliance</h4><p>GL with immutable posting and period locking, AR/AP, bank reconciliation, multi-currency revaluation, multi-company consolidation, UAE VAT and Peppol e-invoicing.</p></div></div>
						<div class="ehm-layer"><div class="ehm-l-num">4</div><div class="ehm-l-body"><h4>Intelligence Layer</h4><p>Intelligent BOS AI advisor — revenue and cash-flow forecasting, predictive inventory, KPI recommendations — plus CRM pipeline and commerce AI.</p></div></div>
					</div>
				</div>
				<div class="ehm-rev ehm-rev--2">
					<div class="ehm-eco">
						<div class="ehm-eco-node"><div class="ehm-eco-l"><span class="ehm-eco-ico">🏢</span><div><div class="ehm-eco-name">Super CP</div><div class="ehm-eco-sub">Operator console · All tenants</div></div></div><span class="ehm-eco-badge ehm-eco-badge--c">OPERATOR</span></div>
						<div class="ehm-eco-con"></div>
						<div class="ehm-eco-node ehm-eco-node--core"><div class="ehm-eco-l"><span class="ehm-eco-ico">☁️</span><div><div class="ehm-eco-name">ECOM AE Cloud</div><div class="ehm-eco-sub">One cloud · Isolated tenants</div></div></div><span class="ehm-eco-badge ehm-eco-badge--g">CORE</span></div>
						<div class="ehm-eco-con"></div>
						<div class="ehm-eco-node"><div class="ehm-eco-l"><span class="ehm-eco-ico">🛒</span><div><div class="ehm-eco-name">Storefront</div><div class="ehm-eco-sub">www.client.com</div></div></div><span class="ehm-eco-badge ehm-eco-badge--c">TENANT</span></div>
						<div class="ehm-eco-con"></div>
						<div class="ehm-eco-node"><div class="ehm-eco-l"><span class="ehm-eco-ico">⚙️</span><div><div class="ehm-eco-name">Control Panel</div><div class="ehm-eco-sub">client.com/cp</div></div></div><span class="ehm-eco-badge ehm-eco-badge--c">TENANT</span></div>
						<div class="ehm-eco-con"></div>
						<div class="ehm-eco-node"><div class="ehm-eco-l"><span class="ehm-eco-ico">📊</span><div><div class="ehm-eco-name">ERP + Finance</div><div class="ehm-eco-sub">client.com/erp</div></div></div><span class="ehm-eco-badge ehm-eco-badge--c">TENANT</span></div>
					</div>
					<p style="font-size:11px;color:var(--ehm-mt);text-align:center;margin:12px 0 0">Multi-tenant nginx · Isolated MySQL per tenant · One codebase</p>
				</div>
			</div>
		</div>
	</section>

	<section id="features" class="ehm-sec ehm-sec--alt">
		<div class="ehm-wrap">
			<div class="ehm-sec-head ehm-rev">
				<div class="ehm-tag"><span class="ehm-dot"></span>One Business Operating System</div>
				<h2>Everything your business<br><span class="ehm-glow">runs on.</span></h2>
				<p>The capability pillars of the ECOM AE BOS — ERP, commerce, compliance, workflows and industry intelligence — enterprise-grade, sharing one database, nothing needs to sync.</p>
			</div>
			<div class="ehm-feat-grid">
				<div class="ehm-fc ehm-rev"><div class="ehm-fc-ico">🛒</div><h4>E-Commerce &amp; Catalogue</h4><p>SKU management, variants, bulk CSV, B2B pricing, promo codes, multi-currency checkout.</p><div class="ehm-pills"><span class="ehm-pill">Storefront</span><span class="ehm-pill">B2B Portal</span><span class="ehm-pill">Multi-Currency</span></div></div>
				<div class="ehm-fc ehm-rev ehm-rev--1"><div class="ehm-fc-ico">📦</div><h4>Inventory &amp; Logistics</h4><p>Multi-warehouse stock with a full inventory ledger, lot/serial/batch and barcode traceability, pick/pack/ship, RMA.</p><div class="ehm-pills"><span class="ehm-pill">Inventory Ledger</span><span class="ehm-pill">Lot / Serial</span><span class="ehm-pill">Barcode</span></div></div>
				<div class="ehm-fc ehm-rev ehm-rev--2"><div class="ehm-fc-ico">📊</div><h4>ERP &amp; Finance</h4><p>Double-entry GL with immutable posting &amp; period locking, AR/AP, bank reconciliation, multi-currency revaluation, multi-company consolidation, UAE VAT.</p><div class="ehm-pills"><span class="ehm-pill">Immutable Posting</span><span class="ehm-pill">Period Locking</span><span class="ehm-pill">Consolidation</span></div></div>
				<div class="ehm-fc ehm-rev"><div class="ehm-fc-ico">🤝</div><h4>CRM &amp; Customers</h4><p>Kanban pipeline, lead capture, 360° customer view, segments, approval workflows.</p><div class="ehm-pills"><span class="ehm-pill">Kanban</span><span class="ehm-pill">Leads</span><span class="ehm-pill">360° View</span></div></div>
				<div class="ehm-fc ehm-rev ehm-rev--1"><div class="ehm-fc-ico">🔧</div><h4>Procurement</h4><p>Supplier directory, purchase requisitions, RFQ &amp; vendor comparison, purchase orders, goods receipt, 3-way match.</p><div class="ehm-pills"><span class="ehm-pill">Requisition</span><span class="ehm-pill">RFQ</span><span class="ehm-pill">Vendor Compare</span></div></div>
				<div class="ehm-fc ehm-rev ehm-rev--2"><div class="ehm-fc-ico">🛡️</div><h4>Compliance &amp; Governance</h4><p>Tax regimes, e-invoicing, filing calendar, database-level integrity, and an enriched audit trail (old/new value, user, IP, device).</p><div class="ehm-pills"><span class="ehm-pill">Audit Trail</span><span class="ehm-pill">Data Integrity</span><span class="ehm-pill">e-Invoice</span></div></div>
				<div class="ehm-fc ehm-rev"><div class="ehm-fc-ico">⚡</div><h4>Workflow Automation</h4><p>Approval chains, spend thresholds, multi-step routing and triggers across orders, POs, journals and vouchers.</p><div class="ehm-pills"><span class="ehm-pill">Approvals</span><span class="ehm-pill">Thresholds</span><span class="ehm-pill">Triggers</span></div></div>
				<div class="ehm-fc ehm-rev ehm-rev--1"><div class="ehm-fc-ico">🧠</div><h4>Intelligent BOS &amp; AI</h4><p>The AI advisor that runs and advises the business — revenue &amp; cash-flow forecasting, predictive inventory, KPI recommendations, plus per-industry dashboards.</p><div class="ehm-pills"><span class="ehm-pill">AI Advisor</span><span class="ehm-pill">Forecasting</span><span class="ehm-pill">Recommendations</span></div></div>
				<div class="ehm-fc ehm-rev ehm-rev--2"><div class="ehm-fc-ico">🏢</div><h4>Multi-Tenant SaaS</h4><p>Super CP for agencies — onboard tenants, push templates, monitor health, deploy industry packs.</p><div class="ehm-pills"><span class="ehm-pill">Super CP</span><span class="ehm-pill">Health Monitor</span><span class="ehm-pill">Templates</span></div></div>
			</div>
		</div>
	</section>

	<section id="industries" class="ehm-sec">
		<div class="ehm-wrap">
			<div class="ehm-sec-head ehm-rev">
				<div class="ehm-tag"><span class="ehm-dot"></span>Industry Solutions</div>
				<h2>Built for your<br><span class="ehm-glow">industry.</span></h2>
				<p>Pre-built module packs, catalogue structure, and workflows tuned to each vertical.</p>
			</div>
			<div class="ehm-ind-grid ehm-rev">
				<a class="ehm-ind-card" href="<?php echo $h($industriesUrl); ?>/industry/auto_parts" style="text-decoration:none;color:inherit"><span class="ehm-ind-emo">🔧</span><div class="ehm-ind-name">Auto Parts</div><div class="ehm-ind-sub">VIN · OEM · B2B</div><span class="ehm-live-tag">● Live</span></a>
				<a class="ehm-ind-card" href="<?php echo $h($industriesUrl); ?>/industry/fashion" style="text-decoration:none;color:inherit"><span class="ehm-ind-emo">👗</span><div class="ehm-ind-name">Fashion &amp; Beauty</div><div class="ehm-ind-sub">Variants · Lookbooks</div><span class="ehm-live-tag">● Live</span></a>
				<a class="ehm-ind-card" href="<?php echo $h($industriesUrl); ?>/industry/electronics" style="text-decoration:none;color:inherit"><span class="ehm-ind-emo">📱</span><div class="ehm-ind-name">Electronics</div><div class="ehm-ind-sub">SKU-heavy · RMA</div><span class="ehm-live-tag">● Live</span></a>
				<a class="ehm-ind-card" href="<?php echo $h($industriesUrl); ?>/industry/jewellery" style="text-decoration:none;color:inherit"><span class="ehm-ind-emo">💎</span><div class="ehm-ind-name">Jewellery</div><div class="ehm-ind-sub">Gallery · Certs</div><span class="ehm-live-tag">● Live</span></a>
				<div class="ehm-ind-card"><span class="ehm-ind-emo">🏥</span><div class="ehm-ind-name">Medical</div><div class="ehm-ind-sub">Batch · Expiry</div><span class="ehm-avail-tag">Available</span></div>
				<a class="ehm-ind-card" href="<?php echo $h($industriesUrl); ?>/industry/tax_advisory" style="text-decoration:none;color:inherit"><span class="ehm-ind-emo">📋</span><div class="ehm-ind-name">Tax &amp; Advisory</div><div class="ehm-ind-sub">CRM · VAT · Docs</div><span class="ehm-live-tag">● Live</span></a>
				<div class="ehm-ind-card"><span class="ehm-ind-emo">🏗️</span><div class="ehm-ind-name">Consultancy</div><div class="ehm-ind-sub">Projects · Billing</div><span class="ehm-avail-tag">Available</span></div>
				<div class="ehm-ind-card"><span class="ehm-ind-emo">🔑</span><div class="ehm-ind-name">Rental Ops</div><div class="ehm-ind-sub">Daily/Monthly</div><span class="ehm-avail-tag">Available</span></div>
			</div>
			<p style="text-align:center;margin-top:20px"><a class="ehm-btn ehm-btn--g" href="<?php echo $h($industriesUrl); ?>">Browse all industries →</a></p>
		</div>
	</section>

	<section id="ai" class="ehm-sec ehm-sec--alt">
		<div class="ehm-wrap">
			<div class="ehm-ai-grid">
				<div class="ehm-rev">
					<div class="ehm-tag" style="background:rgba(0,255,178,.07);border-color:rgba(0,255,178,.2);color:var(--ehm-m)"><span class="ehm-dot"></span>Intelligent BOS · AI Layer</div>
					<h2 style="margin:14px 0 12px">Runs and advises<br>the business.</h2>
					<p style="font-size:14px;color:var(--ehm-mid);line-height:1.7;margin:0 0 24px">The Intelligent BOS reads your live data and tells you what to do next — forecasting, predictive inventory and recommendations woven into the stack, not a chatbot bolted on.</p>
					<div class="ehm-ai-cards">
						<div class="ehm-ai-card"><div class="ehm-ai-ico">🧠</div><div><h4>AI Advisor</h4><p>Ask the BOS in plain English — revenue, cash, stock, receivables — answered from your live data.</p></div></div>
						<div class="ehm-ai-card"><div class="ehm-ai-ico">📈</div><div><h4>Forecasting</h4><p>Revenue and cash-flow forecasts from your ledger — trend, confidence and liquidity alerts.</p></div></div>
						<div class="ehm-ai-card"><div class="ehm-ai-ico">📦</div><div><h4>Predictive Inventory</h4><p>Consumption rate → days of cover → recommended reorder quantity and value, per item.</p></div></div>
						<div class="ehm-ai-card"><div class="ehm-ai-ico">✅</div><div><h4>Automated Recommendations</h4><p>KPI- and forecast-driven actions, prioritised by severity — decision support, not dashboards alone.</p></div></div>
					</div>
				</div>
				<div class="ehm-rev ehm-rev--2">
					<div class="ehm-layla-card">
						<div class="ehm-layla-badge">🤖 Meet Layla</div>
						<div class="ehm-layla-av">L</div>
						<h3 style="color:#fff;margin:0 0 10px">Your AI Demo Guide</h3>
						<p class="ehm-layla-desc">Layla walks you through a live <?php echo (int) $demoDays; ?>-day sandbox — commerce or ERP-only. She adapts the tour to your industry. No sales rep. No waiting.</p>
						<ul class="ehm-layla-li">
							<li>Guided tour of your industry solution</li>
							<li>ERP setup walkthrough in real time</li>
							<li>UAE compliance feature overview</li>
							<li>Live isolated MySQL sandbox environment</li>
							<li>Voice-enabled on supported browsers</li>
						</ul>
						<a href="<?php echo $h($demoUrl); ?>" class="ehm-btn ehm-btn--p" style="margin-top:22px;width:100%;justify-content:center">Try AI Demo — <?php echo (int) $demoDays; ?> Days Free</a>
					</div>
				</div>
			</div>
		</div>
	</section>

	<section id="compliance" class="ehm-sec">
		<div class="ehm-wrap">
			<div class="ehm-comp-banner ehm-rev">
				<div class="ehm-comp-banner__flag" aria-hidden="true">🇦🇪</div>
				<div class="ehm-tag" style="margin-bottom:14px"><span class="ehm-dot"></span>UAE Compliance</div>
				<h2 style="margin:0">Built for UAE.<br><span class="ehm-glow">From day one.</span></h2>
				<p style="font-size:14px;color:var(--ehm-mid);max-width:520px;margin:12px auto 0;line-height:1.7">Designed with UAE tax law, FTA requirements, and Peppol e-invoicing at its core.</p>
			</div>
			<div class="ehm-comp-grid">
				<div class="ehm-comp-card ehm-rev"><div class="ehm-comp-ico">📋</div><h3 class="ehm-comp-title">Peppol / PINT-AE E-Invoicing</h3><p class="ehm-comp-desc">Generate, validate, and export Peppol-ready e-invoices aligned with UAE PINT-AE.</p><ul class="ehm-comp-li"><li>PINT-AE e-invoice generation</li><li>FTA XML &amp; JSON export</li><li>Validate before submission</li></ul></div>
				<div class="ehm-comp-card ehm-rev ehm-rev--1"><div class="ehm-comp-ico">💸</div><h3 class="ehm-comp-title">UAE VAT &amp; TRN Invoicing</h3><p class="ehm-comp-desc">Full VAT lifecycle from storefront through FTA return preparation with TRN-aware invoices.</p><ul class="ehm-comp-li"><li>VAT return data preparation</li><li>TRN on all documents</li><li>Tax-inclusive pricing</li></ul></div>
				<div class="ehm-comp-card ehm-rev ehm-rev--2"><div class="ehm-comp-ico">🌍</div><h3 class="ehm-comp-title">Worldwide Tax Toolkit</h3><p class="ehm-comp-desc">Business taxation for 195+ countries — VAT/GST, CIT, customs, withholding.</p><ul class="ehm-comp-li"><li>195+ tax jurisdictions</li><li>One-click FTA legislation update</li><li>GCC cross-border ready</li></ul></div>
			</div>
		</div>
	</section>

	<section id="supercp" class="ehm-sec ehm-sec--alt">
		<div class="ehm-wrap">
			<div class="ehm-cp-intro">
				<div class="ehm-rev">
					<div class="ehm-tag"><span class="ehm-dot"></span>Super CP — Operator Console</div>
					<h2 style="margin:14px 0">One console.<br><span class="ehm-glow">Every tenant.</span></h2>
					<p style="font-size:14px;color:var(--ehm-mid);line-height:1.7;max-width:480px;margin:0">Agencies and platform operators onboard clients, deploy templates, monitor health, and govern every site from one workspace.</p>
					<div class="ehm-cp-stats">
						<div class="ehm-cp-stat"><div class="ehm-cp-stat-n">6</div><div class="ehm-cp-stat-l">Live tenants</div></div>
						<div class="ehm-cp-stat"><div class="ehm-cp-stat-n">112</div><div class="ehm-cp-stat-l">CP modules</div></div>
						<div class="ehm-cp-stat"><div class="ehm-cp-stat-n">17+</div><div class="ehm-cp-stat-l">Quick actions</div></div>
						<div class="ehm-cp-stat"><div class="ehm-cp-stat-n">24h</div><div class="ehm-cp-stat-l">Client go-live</div></div>
					</div>
					<p style="margin-top:18px"><a class="ehm-btn ehm-btn--p" href="<?php echo $h($superCp); ?>"><i class="fa fa-cloud"></i> Open Super CP</a></p>
				</div>
				<div class="ehm-rev ehm-rev--2">
					<div class="ehm-cp-feat-grid">
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">🏢</div><div class="ehm-cp-feat-name">Tenant Hub</div><div class="ehm-cp-feat-sub">Registry, DNS, SSL</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">📝</div><div class="ehm-cp-feat-name">Client Onboarding</div><div class="ehm-cp-feat-sub">Form → stack in 24h</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">❤️</div><div class="ehm-cp-feat-name">Platform Health</div><div class="ehm-cp-feat-sub">Probes · SSL · Backup</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">🏪</div><div class="ehm-cp-feat-name">Industry Settings</div><div class="ehm-cp-feat-sub">8 verticals · module packs</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">🔑</div><div class="ehm-cp-feat-name">Tenant Control</div><div class="ehm-cp-feat-sub">Access · credentials</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">🎨</div><div class="ehm-cp-feat-name">Visual Editor</div><div class="ehm-cp-feat-sub">Live branding &amp; preview</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">👥</div><div class="ehm-cp-feat-name">Customer Board</div><div class="ehm-cp-feat-sub">Cross-tenant CRM links</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">💲</div><div class="ehm-cp-feat-name">Price Configs</div><div class="ehm-cp-feat-sub">API markup &amp; catalogue rules</div></div>
						<div class="ehm-cp-feat"><div class="ehm-cp-feat-ico">⚖️</div><div class="ehm-cp-feat-name">Governance</div><div class="ehm-cp-feat-sub">Platform-wide compliance</div></div>
					</div>
				</div>
			</div>
			<div class="ehm-rev">
				<div class="ehm-cp-tabs" id="ehmCpTabs" role="tablist">
					<?php foreach ($cpScreens as $i => $scr) { ?>
					<button type="button" class="ehm-cp-tab<?php echo $i === 0 ? ' is-on' : ''; ?>" role="tab" data-target="<?php echo $h($scr['id']); ?>" aria-selected="<?php echo $i === 0 ? 'true' : 'false'; ?>"><?php echo $h($scr['tab']); ?></button>
					<?php } ?>
				</div>
				<div class="ehm-cp-screens">
					<?php foreach ($cpScreens as $i => $scr) { ?>
					<div class="ehm-cp-screen<?php echo $i === 0 ? ' is-on' : ''; ?>" id="<?php echo $h($scr['id']); ?>" role="tabpanel">
						<div class="ehm-ss">
							<img src="<?php echo $h($img . '/' . $scr['img']); ?>" alt="<?php echo $h($scr['alt']); ?>" loading="lazy" />
							<div class="ehm-ss-cap"><div class="ehm-ss-cap-dot"></div><p class="ehm-ss-cap-txt"><?php echo $scr['cap']; ?></p></div>
						</div>
					</div>
					<?php } ?>
				</div>
			</div>
		</div>
	</section>

	<section id="storefront" class="ehm-sec">
		<div class="ehm-wrap">
			<div class="ehm-sec-head ehm-rev">
				<div class="ehm-tag" style="background:rgba(0,255,178,.07);border-color:rgba(0,255,178,.2);color:var(--ehm-m)"><span class="ehm-dot"></span>See The Result — eParts Cart Live</div>
				<h2>This is what<br><span class="ehm-glow-a">client gets.</span></h2>
				<p>eParts Cart is a live ECOM AE tenant — auto spare parts B2B/B2C store running Storefront + CP + ERP on one database.</p>
			</div>
			<div class="ehm-badges ehm-rev">
				<span class="ehm-badge-pill ehm-live-tag">● Live on ecomae.com</span>
				<span class="ehm-badge-pill ehm-avail-tag">Auto Spare Parts</span>
				<span class="ehm-badge-pill" style="background:rgba(255,184,48,.07);border:1px solid rgba(255,184,48,.18);color:var(--ehm-a)">Dubai, UAE · AED / USD / EUR</span>
			</div>
			<div class="ehm-ss ehm-rev" style="margin-bottom:22px">
				<img src="<?php echo $h($img . '/ep_01_home_clean_002d71635c0b47ac83319c7f292601ad.png'); ?>" alt="eParts Cart Homepage" loading="lazy" />
				<div class="ehm-ss-cap"><div class="ehm-ss-cap-dot"></div><p class="ehm-ss-cap-txt"><strong>eParts Cart — Live Homepage.</strong> Multi-tab search, AI Parts Expert chat, WhatsApp, Excel bulk upload, multi-currency — all on ECOM AE.</p></div>
			</div>
			<div class="ehm-sf-pair ehm-rev">
				<div class="ehm-ss">
					<img src="<?php echo $h($img . '/ep_02_parts_0b611ca02fc2409bb49698c824b493f7.png'); ?>" alt="Parts Search" loading="lazy" />
					<div class="ehm-ss-cap"><div class="ehm-ss-cap-dot"></div><p class="ehm-ss-cap-txt"><strong>Parts Search.</strong> OE code lookup — live pricing, availability, add-to-cart, brand filters.</p></div>
				</div>
				<div class="ehm-ss">
					<img src="<?php echo $h($img . '/ep_03_catalog_97ad7d47f81d449d81c7b068733d26a8.png'); ?>" alt="Vehicle Catalog" loading="lazy" />
					<div class="ehm-ss-cap"><div class="ehm-ss-cap-dot"></div><p class="ehm-ss-cap-txt"><strong>Vehicle Catalog.</strong> 708 brands, VIN search, A-Z manufacturer index.</p></div>
				</div>
			</div>
			<div class="ehm-sf-feat-grid ehm-rev">
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">🔍</div><div class="ehm-sf-feat-name">Part No. Search</div><div class="ehm-sf-feat-sub">OE &amp; cross-ref lookup</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">🚗</div><div class="ehm-sf-feat-name">VIN Decode</div><div class="ehm-sf-feat-sub">Exact vehicle &amp; parts</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">📖</div><div class="ehm-sf-feat-name">708 Brands</div><div class="ehm-sf-feat-sub">Passenger / Commercial</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">🤖</div><div class="ehm-sf-feat-name">AI Parts Expert</div><div class="ehm-sf-feat-sub">24/7 chat assistant</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">💱</div><div class="ehm-sf-feat-name">Multi-Currency</div><div class="ehm-sf-feat-sub">AED · USD · EUR</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">💬</div><div class="ehm-sf-feat-name">WhatsApp</div><div class="ehm-sf-feat-sub">Direct line + callback</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">📊</div><div class="ehm-sf-feat-name">Excel Bulk Upload</div><div class="ehm-sf-feat-sub">B2B order sheets</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">🏦</div><div class="ehm-sf-feat-name">B2B Balance</div><div class="ehm-sf-feat-sub">Trade accounts</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">🔖</div><div class="ehm-sf-feat-name">Quotes &amp; Saves</div><div class="ehm-sf-feat-sub">Quote requests</div></div>
				<div class="ehm-sf-feat"><div class="ehm-sf-feat-ico">🌍</div><div class="ehm-sf-feat-name">3 Languages</div><div class="ehm-sf-feat-sub">EN · AR · RU</div></div>
			</div>
			<div class="ehm-pipeline ehm-rev">
				<p class="ehm-pipeline-label">The complete loop — Super CP to live tenant</p>
				<div class="ehm-pipe-row">
					<div class="ehm-pipe-step"><div class="ehm-pipe-ico">🏢</div><div class="ehm-pipe-name ehm-pipe-name--c">Super CP</div><div class="ehm-pipe-sub">Operator onboards</div></div>
					<div class="ehm-pipe-arr">→</div>
					<div class="ehm-pipe-step"><div class="ehm-pipe-ico">⚙️</div><div class="ehm-pipe-name ehm-pipe-name--c">Industry Pack</div><div class="ehm-pipe-sub">Auto Parts deployed</div></div>
					<div class="ehm-pipe-arr">→</div>
					<div class="ehm-pipe-step"><div class="ehm-pipe-ico">🌐</div><div class="ehm-pipe-name ehm-pipe-name--c">DNS + SSL</div><div class="ehm-pipe-sub">Domain goes live</div></div>
					<div class="ehm-pipe-arr">→</div>
					<div class="ehm-pipe-step"><div class="ehm-pipe-ico">🛒</div><div class="ehm-pipe-name ehm-pipe-name--m">Storefront</div><div class="ehm-pipe-sub">epartscart.com</div></div>
					<div class="ehm-pipe-arr">→</div>
					<div class="ehm-pipe-step"><div class="ehm-pipe-ico">📦</div><div class="ehm-pipe-name ehm-pipe-name--m">Inventory</div><div class="ehm-pipe-sub">Stock managed</div></div>
					<div class="ehm-pipe-arr">→</div>
					<div class="ehm-pipe-step"><div class="ehm-pipe-ico">📊</div><div class="ehm-pipe-name ehm-pipe-name--m">ERP + CRM</div><div class="ehm-pipe-sub">Orders → finance</div></div>
				</div>
				<p class="ehm-pipeline-note">All above runs on a <strong style="color:var(--ehm-wh)">single isolated MySQL database per tenant</strong> — orders, inventory, ERP journals, VAT entries, and CRM pipeline sharing one source of truth, managed from the Super CP.</p>
			</div>
			<p style="text-align:center;margin-top:24px" class="ehm-rev">
				<a href="https://www.epartscart.com/" class="ehm-btn ehm-btn--g" target="_blank" rel="noopener" style="margin-right:10px">Visit epartscart.com</a>
				<a href="<?php echo $h($demoUrl); ?>" class="ehm-btn ehm-btn--p">Start Your Own Store</a>
			</p>
		</div>
	</section>

	<section id="how" class="ehm-sec ehm-sec--alt">
		<div class="ehm-wrap">
			<div class="ehm-sec-head ehm-rev">
				<div class="ehm-tag"><span class="ehm-dot"></span>Getting Started</div>
				<h2>Three steps to<br><span class="ehm-glow">go live.</span></h2>
				<p>From first contact to live store — most clients running in under 24 hours.</p>
			</div>
			<div class="ehm-steps">
				<div class="ehm-step ehm-rev"><div class="ehm-step-n">1</div><h3>Start Your Free Demo</h3><p>Meet Layla — <?php echo (int) $demoDays; ?>-day sandbox tailored to your industry. No credit card.</p></div>
				<div class="ehm-step ehm-rev ehm-rev--2"><div class="ehm-step-n">2</div><h3>Configure Your Stack</h3><p>Pick industry template, domain, UAE compliance. Team handles DNS, SSL, and health checks.</p></div>
				<div class="ehm-step ehm-rev ehm-rev--3"><div class="ehm-step-n">3</div><h3>Go Live &amp; Scale</h3><p>Storefront, ERP, and CRM on your domain — one database. Add staff, integrate payments, sell.</p></div>
			</div>
		</div>
	</section>

	<section class="ehm-sec" style="padding-top:0" aria-label="Live tenants">
		<div class="ehm-wrap">
			<p style="text-align:center;font-size:11px;font-weight:700;letter-spacing:1.8px;text-transform:uppercase;color:var(--ehm-mt);margin:0 0 22px">Live Tenants on ECOM AE</p>
			<div class="ehm-tenant-grid ehm-rev">
				<a class="ehm-tenant-card" href="https://www.epartscart.com/" target="_blank" rel="noopener" style="text-decoration:none;color:inherit"><div class="ehm-tenant-emo">🔧</div><div class="ehm-tenant-name">eParts Cart</div><div class="ehm-tenant-url">epartscart.com</div><span class="ehm-live-tag">Auto Parts</span></a>
				<a class="ehm-tenant-card" href="https://www.thejewellerytrend.com/" target="_blank" rel="noopener" style="text-decoration:none;color:inherit"><div class="ehm-tenant-emo">💎</div><div class="ehm-tenant-name">Jewellery Trend</div><div class="ehm-tenant-url">thejewellerytrend.com</div><span class="ehm-live-tag">Jewellery</span></a>
				<a class="ehm-tenant-card" href="https://www.electronicae.com/" target="_blank" rel="noopener" style="text-decoration:none;color:inherit"><div class="ehm-tenant-emo">📱</div><div class="ehm-tenant-name">Electronicae</div><div class="ehm-tenant-url">electronicae.com</div><span class="ehm-live-tag">Electronics</span></a>
				<a class="ehm-tenant-card" href="https://www.taxofinca.com/" target="_blank" rel="noopener" style="text-decoration:none;color:inherit"><div class="ehm-tenant-emo">📋</div><div class="ehm-tenant-name">Taxofinca</div><div class="ehm-tenant-url">taxofinca.com</div><span class="ehm-live-tag">Tax Advisory</span></a>
				<a class="ehm-tenant-card" href="https://www.stylenlook.com/" target="_blank" rel="noopener" style="text-decoration:none;color:inherit"><div class="ehm-tenant-emo">👗</div><div class="ehm-tenant-name">Style N Look</div><div class="ehm-tenant-url">stylenlook.com</div><span class="ehm-live-tag">Fashion</span></a>
			</div>
		</div>
	</section>

	<section id="cta" class="ehm-sec ehm-sec--alt">
		<div class="ehm-wrap">
			<div class="ehm-cta-box ehm-rev">
				<div class="ehm-cta-badge">🎁 <?php echo (int) $demoDays; ?>-Day Free Demo · No Credit Card</div>
				<h2 style="margin:0">Ready to run your entire<br><span class="ehm-glow">business on one cloud?</span></h2>
				<p style="font-size:15px;color:var(--ehm-mid);max-width:480px;margin:12px auto 28px;line-height:1.7">Start your free sandbox today. Layla guides you through every feature relevant to your industry.</p>
				<div style="display:flex;align-items:center;justify-content:center;gap:12px;flex-wrap:wrap">
					<a href="<?php echo $h($demoUrl); ?>" class="ehm-btn ehm-btn--p">Start Free Demo</a>
					<a href="<?php echo $h($platformUrl); ?>" class="ehm-btn ehm-btn--g">View Full Platform</a>
				</div>
				<div class="ehm-cta-check-row">
					<span class="ehm-cc"><?php echo (int) $demoDays; ?>-day free sandbox</span>
					<span class="ehm-cc">No credit card</span>
					<span class="ehm-cc">Go live in 24 hours</span>
					<span class="ehm-cc">UAE compliance built-in</span>
					<span class="ehm-cc">112 CP modules</span>
					<span class="ehm-cc">19+ industry packs</span>
				</div>
			</div>
		</div>
	</section>
</div>
<?php echo epc_ecomae_home_sections_scripts(); ?>
	<?php
	return ob_get_clean();
}

function epc_ecomae_home_sections_scripts()
{
	static $done = false;
	if ($done) {
		return '';
	}
	$done = true;
	ob_start();
	?>
<script defer>
(function(){
	var rev=document.querySelectorAll('.ehm-home .ehm-rev');
	if(rev.length&&'IntersectionObserver' in window){
		var ro=new IntersectionObserver(function(entries){
			entries.forEach(function(e){if(e.isIntersecting){e.target.classList.add('is-vis');}});
		},{threshold:0.07,rootMargin:'0px 0px -30px 0px'});
		rev.forEach(function(el){ro.observe(el);});
	}else{rev.forEach(function(el){el.classList.add('is-vis');});}
	var tabs=document.querySelectorAll('.ehm-home .ehm-cp-tab');
	tabs.forEach(function(tab){
		tab.addEventListener('click',function(){
			var id=tab.getAttribute('data-target');
			tabs.forEach(function(t){t.classList.remove('is-on');t.setAttribute('aria-selected','false');});
			document.querySelectorAll('.ehm-home .ehm-cp-screen').forEach(function(s){s.classList.remove('is-on');});
			tab.classList.add('is-on');
			tab.setAttribute('aria-selected','true');
			var panel=document.getElementById(id);
			if(panel)panel.classList.add('is-on');
		});
	});
})();
</script>
	<?php
	return ob_get_clean();
}
