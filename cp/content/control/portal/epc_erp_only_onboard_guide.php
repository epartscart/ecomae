<?php
/**
 * Super CP — ERP-only tenant onboarding guide (shared ecomae.com model).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';

$isSuper = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
if (!$isSuper) {
	echo '<div class="alert alert-warning">This guide is available on <strong>Super CP</strong> (www.ecomae.com) only.</div>';
	return;
}

function epc_eog_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
$hubUrl = '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=onboard';
$steps = epc_portal_erp_only_onboard_steps();
$accessDoc = '/docs/ECOM-ERP-SHARED-ACCESS.md';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array('class' => 'epc-eog'));
?>
<div class="epc-portal-settings epc-eog">
	<div class="hero">
		<h2><i class="fa fa-university"></i> ERP-only deployment (shared ecomae.com)</h2>
		<p style="margin:0;opacity:.92">Clients who need <strong>only ERP</strong> — no storefront, <strong>no client domain</strong>. All companies log in at <code>www.ecomae.com/cp/</code>; each company has its own MySQL database.</p>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Model — multi-company on one host</h4></div>
		<div class="panel-body">
			<ul>
				<li><code>hosted_on=platform</code> + <code>erp_only_shared=1</code> in tenant registry</li>
				<li>Hostname always <strong>www.ecomae.com</strong> — no DNS, no nginx alias per client</li>
				<li>Login email maps to tenant DB via platform registry; optional company picker if email exists in multiple DBs</li>
				<li><code>access_mode=erp_only</code> — commerce hidden; redirect to ERP shell after login</li>
				<li>Granular <strong>ERP modules</strong> per company (Full ERP, Custom &amp; Shipping, etc.)</li>
				<li>Separate tenant DB per company (<code>asap</code>, <code>company2</code>, …) — not multi-entity unless you enable it inside one DB</li>
			</ul>
			<p class="text-muted">Optional custom domain for ERP-only is deprecated for new clients — use shared ecomae.com only.</p>
			<p class="alert alert-info" style="margin-top:12px"><strong>URL separation (May 2026):</strong> Super CP → <code>/cp/</code> (tenant hub). ECOM AE company ERP → <code>/cp/platform-erp/</code> (ecomae DB). Client staff → <code>/cp/client-erp/{site_key}/</code> (tenant DB).</p>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Onboarding checklist</h4></div>
		<div class="panel-body">
			<ol class="steps">
				<?php foreach ($steps as $step): ?>
				<li><strong><?php echo epc_eog_h($step['title']); ?></strong><br><?php echo $step['body']; ?></li>
				<?php endforeach; ?>
				<li><strong>Set Live &amp; sync</strong><br>Tenant hub → status <strong>Live</strong> pushes <code>access_mode</code>, <code>erp_modules</code>, and CP packs to the company MySQL DB.</li>
				<li><strong>Create users &amp; hand off</strong><br>CP → Users on www.ecomae.com (tenant context after login). Share <code>https://www.ecomae.com/cp/</code> — see <?php echo epc_eog_h($accessDoc); ?> on the server repo.</li>
			</ol>
			<p><a class="btn btn-primary" href="<?php echo epc_eog_h($hubUrl); ?>"><i class="fa fa-rocket"></i> Open onboard form</a></p>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Login URL (ERP-only companies)</h4></div>
		<div class="panel-body">
			<pre>Super CP operator (tenant hub):
https://www.ecomae.com/cp/

Platform ERP (ECOM AE company, ecomae DB):
https://www.ecomae.com/cp/platform-erp/
https://www.ecomae.com/cp/platform-erp/shop/finance/erp?epc_erp_shell=1

ASAP client ERP (example site_key=asap):
https://www.ecomae.com/cp/client-erp/asap/
https://www.ecomae.com/cp/client-erp/asap/shop/finance/erp?epc_erp_shell=1</pre>
			<p class="text-muted">Super CP → tenant hub. Platform operators → platform-erp for company ledger. Client staff → client-erp only. Legacy <code>/cp/shop/finance/erp?epc_erp_shell=1</code> redirects or blocks.</p>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>ASAP (reference tenant)</h4></div>
		<div class="panel-body">
			<p>First shared ERP company: site key <code>asap</code>, Full ERP modules. Provision with <code>epc-asap-erp-onboard.php</code> on the server.</p>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
