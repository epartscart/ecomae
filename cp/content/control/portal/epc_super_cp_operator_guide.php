<?php
/**
 * Super CP — Operator workspace guide (platform operators only).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_super_cp_platform.php';

if (!epc_scp_guard_super_admin()) {
	return;
}

$backend = epc_scp_backend();
$base = '/' . $backend;
$modules = array(
	array(
		'icon' => 'fa-th-large',
		'title' => 'Super CP Fleet Dashboard',
		'url' => $base . '/control/portal/epc_super_cp_fleet_dashboard',
		'summary' => 'View all CP instances across all 28 industries. Search tenants, access any CP/ERP directly.',
		'who' => 'Platform administrators needing fleet-wide visibility into all tenant CPs.',
		'workflow' => array(
			'View all industry groups with their tenant counts.',
			'Click any tenant card to open its CP, ERP, or storefront.',
			'Use search to find specific tenants by name, domain, or industry.',
		),
	),
	array(
		'icon' => 'fa-calculator',
		'title' => 'Super ERP Fleet Dashboard',
		'url' => $base . '/control/portal/epc_super_erp_fleet_dashboard',
		'summary' => 'All ERP instances with module status, BOS control, and fleet-wide operations.',
		'who' => 'Platform operators managing ERP modules, monitoring tenant health, and BOS administration.',
		'workflow' => array(
			'View all ERP instances (live + demo) across industries.',
			'Check module activation status for each tenant.',
			'Access BOS for full fleet control (tenants, billing, security, deployment).',
		),
	),
	array(
		'icon' => 'fa-users',
		'title' => 'Customer board',
		'url' => $base . '/control/portal/epc_super_cp_customer_board',
		'summary' => 'Cross-tenant customer search across the platform registry and every live tenant MySQL database.',
		'who' => 'Support, onboarding, and billing operators who need one search instead of opening each client CP.',
		'workflow' => array(
			'Enter email, phone, name, or company in the search box.',
			'Filter by platform-only or a specific tenant from the registry.',
			'Open CRM, ERP, or tenant CP from Quick actions for the matching user row.',
		),
	),
	array(
		'icon' => 'fa-tags',
		'title' => 'Price configs',
		'url' => $base . '/control/portal/epc_super_cp_price_configs',
		'summary' => 'Markup rules applied to built-in catalogue, price lists, and API or channel pricing.',
		'who' => 'Commercial operators defining platform defaults or per-tenant overrides before go-live.',
		'workflow' => array(
			'Create a platform default rule (scope = Platform default).',
			'Add tenant overrides with higher priority when a client needs different margins.',
			'Set client type (catalogue, API, channel, price list) and optional client ref.',
			'Verify live prices under Shop → Prices on the tenant or demo storefront.',
		),
	),
	array(
		'icon' => 'fa-th-large',
		'title' => 'Info blocks',
		'url' => $base . '/control/portal/epc_super_cp_info_blocks',
		'summary' => 'CMS-style HTML blocks for marketing pages, storefront banners, checkout sidebars, and CP notices.',
		'who' => 'Content and launch operators publishing promos, compliance notices, or maintenance banners.',
		'workflow' => array(
			'Choose placement (homepage, footer, checkout, CP notice, etc.).',
			'Set scope to Platform or Tenant and pick site_key when tenant-specific.',
			'Use a stable block_key (lowercase, underscores) for theme hooks.',
			'Preview on the tenant storefront after DNS is live.',
		),
	),
	array(
		'icon' => 'fa-envelope',
		'title' => 'Communication',
		'url' => $base . '/control/portal/epc_super_cp_communication',
		'summary' => 'Platform email notification policy, SMTP diagnostics, and internal operator tasks.',
		'who' => 'Platform operators coordinating onboarding, DNS go-live, demos, and internal follow-ups.',
		'workflow' => array(
			'Review SMTP row (host/from) — transport lives in config.epc-smtp.php / Modern auth.',
			'Toggle which events send mail (onboard, DNS live, demo expiry, task assigned, digest).',
			'Create internal tasks with assignee, tenant link, due date, and priority.',
			'Filter tasks by status for open onboarding or support queues.',
		),
	),
);
?>
<div class="col-lg-12 epc-scp-panel epc-scp-operator-guide">
<?php
epc_scp_render_hero(
	'Super CP',
	'Operator workspace guide',
	'Platform operator tools on www.ecomae.com — not for end-customers or tenant shop staff.',
	array(
		array('label' => 'Customer board', 'icon' => 'fa-users', 'url' => $base . '/control/portal/epc_super_cp_customer_board', 'primary' => true),
		array('label' => 'Tenant hub', 'icon' => 'fa-cloud', 'url' => $base . '/shop/tenant_hub/tenant_hub'),
	)
);
?>

<div class="epc-scp-guide-callout epc-scp-guide-callout--role">
	<h4><i class="fa fa-shield"></i> Who is an &ldquo;Operator&rdquo;?</h4>
	<p>
		<strong>Super CP Operator</strong> means an ECOM AE staff account on the platform host
		(<code>www.ecomae.com/cp</code>) with a session in the <strong>platform registry database</strong> (ecomae),
		not a client company admin on <code>client-domain/cp</code>.
	</p>
	<ul>
		<li>Use Operator tools to run <em>all tenants</em> from one console.</li>
		<li>Do not share Super CP credentials with clients — use Tenant control center for per-tenant operator passwords.</li>
		<li>Platform ERP and Tenant hub cover finance/registry; Operator covers cross-tenant CRM, pricing, CMS, and comms.</li>
	</ul>
</div>

<div class="epc-scp-guide-callout epc-scp-guide-callout--tenant">
	<h4><i class="fa fa-store"></i> Tenant CP — no Operator sidebar group</h4>
	<p>
		The <strong>Operator</strong> menu group is registered only on <strong>Super CP</strong>
		(<code>epc_cp_super_cp_operator_menu_apply</code>). Client tenant control panels do not show it.
	</p>
	<p class="epc-scp-guide-callout__equiv">
		<strong>Tenant equivalent:</strong> day-to-day shop work lives under <em>Shop</em> (orders, catalogue, prices, clients).
		Store admins manage their own customers in <strong>Shop → Clients &amp; CRM</strong> and storefront content in theme/settings —
		not via Super CP Operator modules.
	</p>
</div>

<h3 class="epc-scp-section-title"><i class="fa fa-th-large"></i> Modules</h3>
<div class="epc-scp-guide-modules">
	<?php foreach ($modules as $mod) { ?>
	<div class="epc-scp-guide-module">
		<div class="epc-scp-guide-module__head">
			<span class="epc-scp-guide-module__icon"><i class="fa <?php echo epc_scp_h($mod['icon']); ?>"></i></span>
			<div>
				<h4><?php echo epc_scp_h($mod['title']); ?></h4>
				<p><?php echo epc_scp_h($mod['summary']); ?></p>
			</div>
			<a class="btn btn-sm btn-primary" href="<?php echo epc_scp_h($mod['url']); ?>">Open</a>
		</div>
		<p class="text-muted small" style="margin:8px 0 6px"><strong>Who should use it:</strong> <?php echo epc_scp_h($mod['who']); ?></p>
		<ol class="epc-scp-guide-module__steps">
			<?php foreach ($mod['workflow'] as $step) { ?>
			<li><?php echo epc_scp_h($step); ?></li>
			<?php } ?>
		</ol>
	</div>
	<?php } ?>
</div>

<h3 class="epc-scp-section-title"><i class="fa fa-random"></i> Typical operator day</h3>
<div class="epc-scp-guide-flow">
	<div class="epc-scp-guide-flow__step"><span>1</span> Tenant hub — onboard client, DNS, industry pack</div>
	<div class="epc-scp-guide-flow__step"><span>2</span> Customer board — verify admin user, open CRM/ERP</div>
	<div class="epc-scp-guide-flow__step"><span>3</span> Price configs — seed markup before catalogue import</div>
	<div class="epc-scp-guide-flow__step"><span>4</span> Info blocks — launch banner or CP notice</div>
	<div class="epc-scp-guide-flow__step"><span>5</span> Communication — tasks + notification toggles for go-live</div>
</div>

<div class="alert alert-info" style="margin-top:18px">
	<strong>Menu location:</strong> Sidebar group <em>Operator</em> (below Tenant hub on Super CP).
	Re-run <code>epc-super-cp-abcp-setup.php?apply=1</code> after deploy if a module link returns 404.
</div>
</div>
