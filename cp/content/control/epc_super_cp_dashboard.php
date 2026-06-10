<?php
/**
 * Super CP home dashboard — KPI row + quick links (ABCP-inspired layout, ECOM AE branding).
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
	return;
}

$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (is_file($portalFile)) {
	require_once $portalFile;
}

$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
$base = '/' . $backend;

function epc_scp_dash_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$stats = array(
	'tenants_total' => 0,
	'tenants_live' => 0,
	'tenants_dns_pending' => 0,
	'demo_active' => 0,
	'menu_modules' => 0,
);
global $db_link;
if (isset($db_link) && $db_link instanceof PDO) {
	$helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';
	if (is_file($helpers)) {
		require_once $helpers;
		if (function_exists('epc_th_platform_stats')) {
			$stats = array_merge($stats, epc_th_platform_stats($db_link));
		}
	}
	try {
		$stats['menu_modules'] = (int) $db_link->query('SELECT COUNT(*) FROM `control_items`')->fetchColumn();
	} catch (Exception $e) {
	}
	$demoFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	if (is_file($demoFile)) {
		require_once $demoFile;
		if (function_exists('epc_portal_demo_count_active')) {
			$stats['demo_active'] = (int) epc_portal_demo_count_active($db_link);
		}
	}
}

$operatorGuideUrl = $base . '/control/portal/epc_super_cp_operator_guide';
$quickLinks = array(
	array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => $operatorGuideUrl, 'tone' => 'platform', 'hint' => 'Who uses Operator tools & workflows'),
	array('label' => 'Customer orders', 'icon' => 'fa-shopping-cart', 'url' => $base . '/shop/orders/orders', 'tone' => 'orders'),
	array('label' => 'Clients & CRM', 'icon' => 'fa-address-book', 'url' => $base . '/shop/customer_mgmt/customer_mgmt', 'tone' => 'clients'),
	array('label' => 'Catalogue', 'icon' => 'fa-th-large', 'url' => $base . '/shop/catalogue/products', 'tone' => 'catalog'),
	array('label' => 'Prices', 'icon' => 'fa-tags', 'url' => $base . '/shop/prices', 'tone' => 'prices'),
	array('label' => 'Procurement', 'icon' => 'fa-truck-loading', 'url' => $base . '/shop/procurement/procurement', 'tone' => 'warehouse'),
	array('label' => 'POS Terminal', 'icon' => 'fa-credit-card', 'url' => $base . '/shop/pos/terminal', 'tone' => 'orders'),
	array('label' => 'POS overview', 'icon' => 'fa-building-o', 'url' => $base . '/control/portal/epc_pos_tenant_manage', 'tone' => 'platform'),
	array('label' => 'ERP & finance', 'icon' => 'fa-university', 'url' => $base . '/shop/finance/erp?epc_erp_shell=1', 'tone' => 'finance'),
	array('label' => 'Tenant hub', 'icon' => 'fa-cloud', 'url' => $base . '/shop/tenant_hub/tenant_hub', 'tone' => 'platform'),
	array('label' => 'Customer board', 'icon' => 'fa-users', 'url' => $base . '/control/portal/epc_super_cp_customer_board', 'tone' => 'clients', 'hint' => 'Cross-tenant search & CRM/ERP links'),
	array('label' => 'Price configs', 'icon' => 'fa-percent', 'url' => $base . '/control/portal/epc_super_cp_price_configs', 'tone' => 'prices', 'hint' => 'Catalogue & API markup rules'),
	array('label' => 'Info blocks', 'icon' => 'fa-newspaper-o', 'url' => $base . '/control/portal/epc_super_cp_info_blocks', 'tone' => 'docs', 'hint' => 'Storefront & CP CMS blocks'),
	array('label' => 'Visual editor', 'icon' => 'fa-magic', 'url' => $base . '/control/portal/epc_visual_page_editor', 'tone' => 'platform', 'hint' => 'Block layout, colours, live preview'),
	array('label' => 'Communication', 'icon' => 'fa-envelope', 'url' => $base . '/control/portal/epc_super_cp_communication', 'tone' => 'auth', 'hint' => 'Email policy & internal tasks'),
	array('label' => 'Social media hub', 'icon' => 'fa-share-alt', 'url' => $base . '/control/portal/epc_social_media_hub', 'tone' => 'platform', 'hint' => 'Content pack, TikTok/IG, AI advisor'),
	array('label' => 'Tenant control', 'icon' => 'fa-sliders', 'url' => $base . '/control/portal/epc_tenant_control_center', 'tone' => 'platform'),
	array('label' => 'Governance', 'icon' => 'fa-gavel', 'url' => $base . '/control/portal/epc_platform_governance', 'tone' => 'governance'),
	array('label' => 'Tax Toolkit', 'icon' => 'fa-balance-scale', 'url' => $base . '/control/portal/epc_tax_toolkit_manage', 'tone' => 'finance', 'hint' => 'Jurisdiction VAT/GST kits & tenant jurisdiction'),
	array('label' => 'Auto Price Engine', 'icon' => 'fa-chart-line', 'url' => $base . '/control/portal/epc_auto_price_engine', 'tone' => 'prices', 'hint' => 'Multi-source compare, product wizard, eBay cross-list'),
	array('label' => 'Modern auth', 'icon' => 'fa-sign-in', 'url' => $base . '/control/portal/epc_cp_auth_settings', 'tone' => 'auth'),
	array('label' => 'Documents', 'icon' => 'fa-file-text-o', 'url' => $base . '/shop/document_control/document_control', 'tone' => 'docs'),
	array('label' => 'Platform health', 'icon' => 'fa-heartbeat', 'url' => $base . '/control/portal/epc_platform_health_checkup', 'tone' => 'health'),
	array('label' => 'Platform FAQ', 'icon' => 'fa-question-circle', 'url' => 'https://www.ecomae.com/platform/faq', 'tone' => 'docs', 'hint' => '105 tenant capability answers'),
);
?>
<div class="col-lg-12 epc-scp-dashboard">
	<div class="epc-scp-dashboard__hero">
		<div>
			<span class="epc-scp-dashboard__badge">Super CP</span>
			<h2 class="epc-scp-dashboard__title">Operator console</h2>
			<p class="epc-scp-dashboard__sub">Tenants, commerce, ERP, and platform governance — one workspace on www.ecomae.com. <a href="<?php echo epc_scp_dash_h($operatorGuideUrl); ?>">Operator guide</a> explains cross-tenant tools.</p>
		</div>
		<div class="epc-scp-dashboard__hero-actions">
			<a class="btn btn-sm btn-default" href="<?php echo epc_scp_dash_h($operatorGuideUrl); ?>"><i class="fa fa-book"></i> Operator guide</a>
			<a class="btn btn-sm btn-primary epc-cp-page-header__pill--primary" href="<?php echo epc_scp_dash_h($base . '/shop/tenant_hub/tenant_hub?tab=onboard'); ?>"><i class="fa fa-rocket"></i> Onboard client</a>
			<a class="btn btn-sm btn-default" href="<?php echo epc_scp_dash_h($base . '/control/portal/industry_settings'); ?>"><i class="fa fa-cog"></i> Industry settings</a>
			<a class="btn btn-sm btn-default" href="<?php echo epc_scp_dash_h($base . '/platform-erp/'); ?>"><i class="fa fa-chart-line"></i> Platform ERP</a>
		</div>
	</div>

	<div class="epc-scp-kpi">
		<div class="epc-scp-kpi__card epc-cp-card epc-cp-stat">
			<div class="epc-scp-kpi__label">Tenants</div>
			<div class="epc-scp-kpi__val" data-epc-stat><?php echo (int) $stats['tenants_total']; ?></div>
			<div class="epc-scp-kpi__hint"><?php echo (int) $stats['tenants_live']; ?> live</div>
		</div>
		<div class="epc-scp-kpi__card epc-cp-card epc-cp-stat">
			<div class="epc-scp-kpi__label">Awaiting DNS</div>
			<div class="epc-scp-kpi__val" data-epc-stat><?php echo (int) $stats['tenants_dns_pending']; ?></div>
			<div class="epc-scp-kpi__hint">GoDaddy A-record</div>
		</div>
		<div class="epc-scp-kpi__card epc-cp-card epc-cp-stat">
			<div class="epc-scp-kpi__label">Active demos</div>
			<div class="epc-scp-kpi__val" data-epc-stat><?php echo (int) $stats['demo_active']; ?></div>
			<div class="epc-scp-kpi__hint">Sandbox tenants</div>
		</div>
		<div class="epc-scp-kpi__card epc-cp-card epc-cp-stat">
			<div class="epc-scp-kpi__label">CP modules</div>
			<div class="epc-scp-kpi__val" data-epc-stat><?php echo (int) $stats['menu_modules']; ?></div>
			<div class="epc-scp-kpi__hint">Sidebar entries</div>
		</div>
	</div>

	<h3 class="epc-scp-section-title"><i class="fa fa-bolt"></i> Quick actions</h3>
	<div class="epc-scp-quick-grid">
		<?php foreach ($quickLinks as $link) { ?>
		<a class="epc-scp-quick-card epc-cp-card epc-scp-quick-card--<?php echo epc_scp_dash_h($link['tone']); ?>" href="<?php echo epc_scp_dash_h($link['url']); ?>"<?php if (!empty($link['hint'])) { ?> title="<?php echo epc_scp_dash_h($link['hint']); ?>"<?php } ?>>
			<span class="epc-scp-quick-card__icon"><i class="fa <?php echo epc_scp_dash_h($link['icon']); ?>"></i></span>
			<span class="epc-scp-quick-card__label"><?php echo epc_scp_dash_h($link['label']); ?></span>
			<?php if (!empty($link['hint'])) { ?><span class="epc-scp-quick-card__hint"><?php echo epc_scp_dash_h($link['hint']); ?></span><?php } ?>
		</a>
		<?php } ?>
	</div>
</div>
