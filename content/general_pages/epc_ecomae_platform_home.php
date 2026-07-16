<?php
/**
 * ecomae.com — platform marketing homepage (Model C hybrid — public face).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';

$site = epc_portal_site_profile();
$industries = epc_portal_industries();
$cp_marketing = '/' . $GLOBALS['DP_Config']->backend_dir;
$super_cp = 'https://cp.ecomae.com/' . $GLOBALS['DP_Config']->backend_dir;
$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';

function epc_ecomae_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}
?>
<style>
.epc-ecomae-hero{padding:48px 0 32px;color:#fff;background:linear-gradient(135deg,#082f49 0%,#0ea5e9 100%);border-radius:20px;margin-bottom:24px;overflow:hidden;position:relative}
.epc-ecomae-hero:before{content:"";position:absolute;inset:0;background:radial-gradient(circle at 80% 20%,rgba(56,189,248,.35),transparent 40%);pointer-events:none}
.epc-ecomae-hero .inner{position:relative;z-index:1;padding:0 28px}
.epc-ecomae-hero h1{font-size:42px;font-weight:800;margin:0 0 12px;color:#fff}
.epc-ecomae-hero .lead{font-size:18px;opacity:.92;max-width:720px;line-height:1.6}
.epc-ecomae-badge{display:inline-block;background:rgba(255,255,255,.15);border:1px solid rgba(255,255,255,.25);padding:6px 14px;border-radius:999px;font-size:12px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;margin-bottom:16px}
.epc-ecomae-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:16px;margin:28px 0}
.epc-ecomae-card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:20px;box-shadow:0 4px 20px rgba(15,23,42,.06)}
.epc-ecomae-card h4{margin:0 0 8px;color:#0f172a;font-weight:700}
.epc-ecomae-card p{margin:0;color:#64748b;font-size:14px;line-height:1.55}
.epc-ecomae-steps{counter-reset:step;margin:20px 0;padding:0;list-style:none}
.epc-ecomae-steps li{counter-increment:step;padding:12px 0 12px 48px;position:relative;border-bottom:1px solid #e2e8f0;color:#334155}
.epc-ecomae-steps li:before{content:counter(step);position:absolute;left:0;top:12px;width:32px;height:32px;background:#0ea5e9;color:#fff;border-radius:50%;text-align:center;line-height:32px;font-weight:700;font-size:14px}
.epc-ecomae-pricing{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px;margin-top:20px}
.epc-ecomae-price{border:2px solid #e2e8f0;border-radius:12px;padding:20px;text-align:center}
.epc-ecomae-price.featured{border-color:#0ea5e9;background:#f0f9ff}
.epc-ecomae-cta{margin-top:24px;display:flex;flex-wrap:wrap;gap:12px}
</style>

<div class="col-lg-12">
	<div class="epc-ecomae-hero">
		<div class="inner">
			<div class="epc-ecomae-badge"><i class="fa fa-globe"></i> E-Commerce · Arab Emirates</div>
			<h1><?php echo epc_ecomae_h(epc_brand_system_name()); ?></h1>
			<p class="lead"><strong>ecomae</strong> — a multi-tenant <strong>Blockchain BOS Enterprise System</strong> combining ERP, commerce, compliance, workflows, industry intelligence and cryptographic proof for organizations worldwide. Your client keeps <strong>only their domain</strong>; we run one unified operating system on our platform.</p>
			<div class="epc-ecomae-cta">
				<a class="btn btn-primary btn-lg" href="#contact"><i class="fa fa-envelope"></i> Request demo</a>
				<a class="btn btn-default btn-lg" href="<?php echo epc_ecomae_h($super_cp); ?>" style="color:#0f172a"><i class="fa fa-th-large"></i> Platform login</a>
			</div>
			<p style="margin-top:16px;opacity:.85;font-size:13px">Designed by <?php echo epc_ecomae_h(epc_brand_hub_name()); ?> · Electronic World Group</p>
		</div>
	</div>

	<div class="epc-ecomae-grid">
		<div class="epc-ecomae-card">
			<h4><i class="fa fa-link text-primary"></i> Domain only</h4>
			<p>Clients point DNS to ecomae infrastructure. No separate hosting, DB, or server management for them.</p>
		</div>
		<div class="epc-ecomae-card">
			<h4><i class="fa fa-database text-primary"></i> Isolated databases</h4>
			<p>Separate MySQL database per tenant — secure custody, easy backup and export.</p>
		</div>
		<div class="epc-ecomae-card">
			<h4><i class="fa fa-industry text-primary"></i> Multi-industry</h4>
			<p>Auto parts, tax advisory, fashion, medical, consultancy — modular CP packs per vertical.</p>
		</div>
		<div class="epc-ecomae-card">
			<h4><i class="fa fa-university text-primary"></i> ERP &amp; Compliance</h4>
			<p>Finance, VAT, e-invoicing, filing calendar, document control, customer hub — Blockchain BOS Enterprise modules included.</p>
		</div>
	</div>

	<h3 style="margin-top:32px"><i class="fa fa-cubes"></i> Industries supported</h3>
	<div class="epc-ecomae-grid">
		<?php foreach ($industries as $ind) {
			if ($ind['code'] === 'platform_host') {
				continue;
			} ?>
		<div class="epc-ecomae-card">
			<h4><i class="fa <?php echo epc_ecomae_h($ind['icon']); ?>"></i> <?php echo epc_ecomae_h($ind['name']); ?></h4>
			<p>Pre-configured CP modules, theme, and homepage for <?php echo epc_ecomae_h(strtolower($ind['name'])); ?> businesses.</p>
		</div>
		<?php } ?>
	</div>

	<h3 style="margin-top:32px"><i class="fa fa-road"></i> How it works</h3>
	<ol class="epc-ecomae-steps">
		<li>Client keeps domain at GoDaddy (e.g. <code>www.theirbrand.com</code>)</li>
		<li>You register tenant in Super CP — draft until ready</li>
		<li>GoDaddy A record → our platform IP — no separate hosting package</li>
		<li>One codebase on ecomae — SSL + storefront go live on their domain</li>
		<li>Client uses <code>www.theirbrand.com/cp</code>; you manage at <strong>cp.ecomae.com</strong></li>
	</ol>

	<h3 id="pricing" style="margin-top:32px"><i class="fa fa-tags"></i> Typical plans</h3>
	<div class="epc-ecomae-pricing">
		<div class="epc-ecomae-price">
			<h4>Launch — AED 399/mo</h4>
			<p>Storefront + CP + ERP-lite<br>Dedicated DB · SSL · unlimited users</p>
		</div>
		<div class="epc-ecomae-price featured">
			<h4>Growth — AED 999/mo</h4>
			<p>+ Full ERP · country-driven VAT<br>Multi-warehouse/vendor · POS · CRM</p>
		</div>
		<div class="epc-ecomae-price">
			<h4>Scale / Enterprise</h4>
			<p>+ Multichannel OMS · AI · API · Blockchain proofs<br>Blockchain BOS operator fleet · custom · SLA</p>
		</div>
	</div>

	<div class="epc-ecomae-card" id="contact" style="margin-top:32px">
		<h4><i class="fa fa-envelope"></i> Contact</h4>
		<p>Email: <a href="mailto:hello@ecomae.com">hello@ecomae.com</a> · Platform operator login: <a href="<?php echo epc_ecomae_h($super_cp); ?>">cp.ecomae.com</a></p>
		<p class="text-muted" style="margin-top:12px">ecomae — E-Commerce Arab Emirates. Hosted commerce platform by Electronic World Group.</p>
	</div>
</div>
