<?php
/**
 * Tenant CP home — simple daily workspace: KPIs + primary actions + optional more tools.
 */
defined('_ASTEXE_') or die('No access');

if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	return;
}

$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (is_file($portalFile)) {
	require_once $portalFile;
}
$brandFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
if (is_file($brandFile)) {
	require_once $brandFile;
}

$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
$base = '/' . $backend;

function epc_tcp_dash_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_tcp_dash_stats(PDO $db): array
{
	$compute = static function () use ($db): array {
		$stats = array(
			'orders_today' => 0,
			'products' => 0,
			'clients' => 0,
			'pending_tasks' => 0,
		);
		try {
			$stats['orders_today'] = (int) $db->query(
				'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE())'
			)->fetchColumn();
		} catch (Exception $e) {
		}
		try {
			$stats['products'] = (int) $db->query(
				'SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `published_flag` = 1'
			)->fetchColumn();
		} catch (Exception $e) {
		}
		try {
			// Storefront customers only — exclude users bound to backend/admin groups.
			$stats['clients'] = (int) $db->query(
				'SELECT COUNT(*) FROM `users` u
				 WHERE u.`user_id` > 0
				 AND NOT EXISTS (
					SELECT 1 FROM `users_groups_bind` b
					INNER JOIN `groups` g ON g.`id` = b.`group_id`
					WHERE b.`user_id` = u.`user_id` AND g.`for_backend` = 1
				 )'
			)->fetchColumn();
		} catch (Exception $e) {
			try {
				$stats['clients'] = (int) $db->query(
					'SELECT COUNT(*) FROM `users` WHERE `user_id` > 0'
				)->fetchColumn();
			} catch (Exception $e2) {
			}
		}
		try {
			$openStatuses = array();
			$q = $db->query("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1");
			while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
				$openStatuses[] = (int) $r['id'];
			}
			if (count($openStatuses) > 0) {
				$sp = implode(',', array_fill(0, count($openStatuses), '?'));
				$st = $db->prepare("SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `status` IN ($sp)");
				$st->execute($openStatuses);
				$stats['pending_tasks'] = (int) $st->fetchColumn();
			}
		} catch (Exception $e) {
		}
		return $stats;
	};

	$perfCache = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';
	if (is_file($perfCache)) {
		require_once $perfCache;
		if (function_exists('epc_perf_cache_remember')) {
			$dbName = 'default';
			try {
				$dbName = (string) $db->query('SELECT DATABASE()')->fetchColumn();
			} catch (Throwable $e) {
			}
			return epc_perf_cache_remember('epc_tcp_dash_stats:v3:' . $dbName, 180, $compute);
		}
	}
	return $compute();
}

$stats = array(
	'orders_today' => 0,
	'products' => 0,
	'clients' => 0,
	'pending_tasks' => 0,
);
global $db_link;
if (isset($db_link) && $db_link instanceof PDO) {
	$stats = epc_tcp_dash_stats($db_link);
}

$brand = function_exists('epc_brand_cp_context') ? epc_brand_cp_context() : array();
$tenantName = trim((string) ($brand['company_name'] ?? $brand['product_name'] ?? 'Control Panel'));
$industryCode = function_exists('epc_portal_cp_active_industry') ? epc_portal_cp_active_industry() : 'auto_parts';
$industry = function_exists('epc_portal_industry') ? epc_portal_industry($industryCode) : array('name' => 'Commerce', 'icon' => 'fa-cog');
$industryLabel = trim((string) ($industry['name'] ?? 'Commerce'));
$industryIcon = trim((string) ($industry['icon'] ?? 'fa-cog'));
$storefrontUrl = trim((string) ($GLOBALS['DP_Config']->domain_path ?? ''));
$settingsUrl = $base . '/control/portal/industry_settings';
$ordersUrl = $base . '/shop/orders/orders';
$catalogueUrl = $base . '/shop/catalogue/products';
$clientsUrl = $base . '/shop/customer_mgmt/customer_mgmt';

if ($industryCode === 'auto_parts') {
	$primaryLinks = array(
		array('label' => 'OMS', 'icon' => 'fa-shopping-cart', 'url' => $ordersUrl, 'tone' => 'orders', 'hint' => 'Orders, fulfilment & messaging'),
		array('label' => 'Warehouses', 'icon' => 'fa-building', 'url' => $base . '/shop/logistics/storages', 'tone' => 'warehouse', 'hint' => 'Storages & price-list links'),
		array('label' => 'Prices', 'icon' => 'fa-tags', 'url' => $base . '/shop/prices', 'tone' => 'prices', 'hint' => 'Price lists & storefront toggles'),
		array('label' => 'Multivendor', 'icon' => 'fa-handshake-o', 'url' => $base . '/shop/prices/multivendor', 'tone' => 'prices', 'hint' => 'Supplier price pipelines'),
		array('label' => 'Crosses', 'icon' => 'fa-exchange', 'url' => $base . '/shop/crosses', 'tone' => 'catalog', 'hint' => 'OEM / aftermarket crosses'),
		array('label' => 'AI chats', 'icon' => 'fa-comments', 'url' => $base . '/shop/parts_agent_chats', 'tone' => 'platform', 'hint' => 'Parts agent conversations'),
		array('label' => 'Clients', 'icon' => 'fa-address-book', 'url' => $clientsUrl, 'tone' => 'clients', 'hint' => 'Customers & CRM'),
		array('label' => 'ERP & finance', 'icon' => 'fa-university', 'url' => $base . '/shop/finance/erp?epc_erp_shell=1', 'tone' => 'finance', 'hint' => 'Ledger, VAT & reports'),
		array('label' => 'Settings', 'icon' => 'fa-cog', 'url' => $settingsUrl, 'tone' => 'governance', 'hint' => 'Branding & modules'),
	);
	$moreLinks = array(
		array('label' => 'Catalogue', 'icon' => 'fa-th-large', 'url' => $catalogueUrl, 'tone' => 'catalog', 'hint' => 'Products & categories'),
		array('label' => 'CP brochure', 'icon' => 'fa-book', 'url' => $base . '/control/cp_brochure', 'tone' => 'docs', 'hint' => 'Every CP function — share / print'),
		array('label' => 'Accessories', 'icon' => 'fa-puzzle-piece', 'url' => $base . '/shop/accessories', 'tone' => 'catalog', 'hint' => 'Marketplace listings'),
		array('label' => 'Auto Price AI', 'icon' => 'fa-chart-line', 'url' => $base . '/control/portal/epc_auto_price_engine', 'tone' => 'prices', 'hint' => 'Multi-source pricing'),
		array('label' => 'Procurement', 'icon' => 'fa-truck', 'url' => $base . '/shop/procurement/procurement', 'tone' => 'warehouse', 'hint' => 'Purchasing & suppliers'),
		array('label' => 'Documents', 'icon' => 'fa-file-text-o', 'url' => $base . '/shop/document_control/document_control', 'tone' => 'docs', 'hint' => 'Invoices & PDFs'),
		array('label' => 'POS Terminal', 'icon' => 'fa-credit-card', 'url' => $base . '/shop/pos/terminal', 'tone' => 'orders', 'hint' => 'In-store checkout'),
		array('label' => 'Visual editor', 'icon' => 'fa-magic', 'url' => $base . '/control/portal/epc_visual_page_editor', 'tone' => 'platform', 'hint' => 'Storefront layout'),
		array('label' => 'Tax Toolkit', 'icon' => 'fa-balance-scale', 'url' => $base . '/control/portal/epc_tax_toolkit_manage', 'tone' => 'finance', 'hint' => 'VAT / GST kits'),
		array('label' => 'Social media', 'icon' => 'fa-share-alt', 'url' => $base . '/control/portal/epc_social_media_hub', 'tone' => 'platform', 'hint' => 'Captions & accounts'),
	);
} else {
	$primaryLinks = array(
		array('label' => 'Orders', 'icon' => 'fa-shopping-cart', 'url' => $ordersUrl, 'tone' => 'orders', 'hint' => 'Process customer orders'),
		array('label' => 'Catalogue', 'icon' => 'fa-th-large', 'url' => $catalogueUrl, 'tone' => 'catalog', 'hint' => 'Products & categories'),
		array('label' => 'Prices', 'icon' => 'fa-tags', 'url' => $base . '/shop/prices', 'tone' => 'prices', 'hint' => 'Price lists & markups'),
		array('label' => 'Clients', 'icon' => 'fa-address-book', 'url' => $clientsUrl, 'tone' => 'clients', 'hint' => 'Customers & CRM'),
		array('label' => 'Accessories', 'icon' => 'fa-puzzle-piece', 'url' => $base . '/shop/accessories', 'tone' => 'catalog', 'hint' => 'Marketplace listings'),
		array('label' => 'ERP & finance', 'icon' => 'fa-university', 'url' => $base . '/shop/finance/erp?epc_erp_shell=1', 'tone' => 'finance', 'hint' => 'Ledger, VAT & reports'),
		array('label' => 'Settings', 'icon' => 'fa-cog', 'url' => $settingsUrl, 'tone' => 'governance', 'hint' => 'Branding & modules'),
	);
	$moreLinks = array(
		array('label' => 'CP brochure', 'icon' => 'fa-book', 'url' => $base . '/control/cp_brochure', 'tone' => 'docs', 'hint' => 'Every CP function — share / print'),
		array('label' => 'Auto Price AI', 'icon' => 'fa-chart-line', 'url' => $base . '/control/portal/epc_auto_price_engine', 'tone' => 'prices', 'hint' => 'Multi-source pricing'),
		array('label' => 'Procurement', 'icon' => 'fa-truck', 'url' => $base . '/shop/procurement/procurement', 'tone' => 'warehouse', 'hint' => 'Purchasing & suppliers'),
		array('label' => 'Documents', 'icon' => 'fa-file-text-o', 'url' => $base . '/shop/document_control/document_control', 'tone' => 'docs', 'hint' => 'Invoices & PDFs'),
		array('label' => 'POS Terminal', 'icon' => 'fa-credit-card', 'url' => $base . '/shop/pos/terminal', 'tone' => 'orders', 'hint' => 'In-store checkout'),
		array('label' => 'Visual editor', 'icon' => 'fa-magic', 'url' => $base . '/control/portal/epc_visual_page_editor', 'tone' => 'platform', 'hint' => 'Storefront layout'),
		array('label' => 'Tax Toolkit', 'icon' => 'fa-balance-scale', 'url' => $base . '/control/portal/epc_tax_toolkit_manage', 'tone' => 'finance', 'hint' => 'VAT / GST kits'),
		array('label' => 'Social media', 'icon' => 'fa-share-alt', 'url' => $base . '/control/portal/epc_social_media_hub', 'tone' => 'platform', 'hint' => 'Captions & accounts'),
	);
}

$GLOBALS['epc_tenant_cp_dashboard_shown'] = true;
?>
<div class="col-lg-12 epc-scp-dashboard epc-tcp-dashboard epc-tcp-dashboard--easy">
	<div class="epc-scp-dashboard__hero epc-tcp-dashboard__hero">
		<div>
			<span class="epc-scp-dashboard__badge"><i class="fa <?php echo epc_tcp_dash_h($industryIcon); ?>"></i> <?php echo epc_tcp_dash_h($industryLabel); ?></span>
			<h2 class="epc-scp-dashboard__title"><?php echo epc_tcp_dash_h($tenantName); ?></h2>
			<p class="epc-scp-dashboard__sub"><?php echo $industryCode === 'auto_parts'
				? 'Spare-parts workspace — OMS, warehouses, prices, crosses, and AI chats for today’s trading.'
				: 'Your workspace for today’s trading — clear actions, clean screens, fast answers. Start with orders, catalogue, prices, or clients.'; ?></p>
		</div>
		<div class="epc-scp-dashboard__hero-actions">
			<a class="btn btn-sm btn-primary epc-cp-page-header__pill--primary" href="<?php echo epc_tcp_dash_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
			<a class="btn btn-sm btn-default" href="<?php echo epc_tcp_dash_h($catalogueUrl); ?>"><i class="fa fa-th-large"></i> Catalogue</a>
			<?php if ($storefrontUrl !== '') { ?>
			<a class="btn btn-sm btn-default" href="<?php echo epc_tcp_dash_h($storefrontUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> View site</a>
			<?php } ?>
			<a class="btn btn-sm btn-default" href="<?php echo epc_tcp_dash_h($base . '/control/cp_brochure'); ?>" target="_blank" rel="noopener"><i class="fa fa-book"></i> CP brochure</a>
			<a class="btn btn-sm btn-default" href="<?php echo epc_tcp_dash_h($settingsUrl); ?>"><i class="fa fa-cog"></i> Settings</a>
		</div>
	</div>

	<div class="epc-scp-kpi">
		<a class="epc-scp-kpi__card epc-cp-card epc-cp-stat" href="<?php echo epc_tcp_dash_h($ordersUrl); ?>">
			<div class="epc-scp-kpi__label">Orders today</div>
			<div class="epc-scp-kpi__val" data-epc-stat><?php echo (int) $stats['orders_today']; ?></div>
			<div class="epc-scp-kpi__hint">Open orders list</div>
		</a>
		<a class="epc-scp-kpi__card epc-cp-card epc-cp-stat" href="<?php echo epc_tcp_dash_h($catalogueUrl); ?>">
			<div class="epc-scp-kpi__label">Products</div>
			<div class="epc-scp-kpi__val" data-epc-stat><?php echo (int) $stats['products']; ?></div>
			<div class="epc-scp-kpi__hint">Published catalogue</div>
		</a>
		<a class="epc-scp-kpi__card epc-cp-card epc-cp-stat" href="<?php echo epc_tcp_dash_h($clientsUrl); ?>">
			<div class="epc-scp-kpi__label">Clients</div>
			<div class="epc-scp-kpi__val" data-epc-stat><?php echo (int) $stats['clients']; ?></div>
			<div class="epc-scp-kpi__hint">Storefront customers</div>
		</a>
		<a class="epc-scp-kpi__card epc-cp-card epc-cp-stat" href="<?php echo epc_tcp_dash_h($ordersUrl); ?>">
			<div class="epc-scp-kpi__label">Open orders</div>
			<div class="epc-scp-kpi__val epc-scp-kpi__val--warn" data-epc-stat><?php echo (int) $stats['pending_tasks']; ?></div>
			<div class="epc-scp-kpi__hint">Need fulfilment</div>
		</a>
	</div>

	<h3 class="epc-scp-section-title"><i class="fa fa-bolt"></i> Daily work</h3>
	<div class="epc-scp-quick-grid epc-tcp-primary-grid">
		<?php foreach ($primaryLinks as $link) { ?>
		<a class="epc-scp-quick-card epc-cp-card epc-scp-quick-card--<?php echo epc_tcp_dash_h($link['tone']); ?>" href="<?php echo epc_tcp_dash_h($link['url']); ?>" title="<?php echo epc_tcp_dash_h($link['hint']); ?>">
			<span class="epc-scp-quick-card__icon"><i class="fa <?php echo epc_tcp_dash_h($link['icon']); ?>"></i></span>
			<span class="epc-scp-quick-card__label"><?php echo epc_tcp_dash_h($link['label']); ?></span>
			<span class="epc-scp-quick-card__hint"><?php echo epc_tcp_dash_h($link['hint']); ?></span>
		</a>
		<?php } ?>
	</div>

	<details class="epc-tcp-more">
		<summary>
			<span><i class="fa fa-ellipsis-h"></i> More tools</span>
			<span class="epc-tcp-more__hint">POS, documents, pricing AI, accessories…</span>
		</summary>
		<div class="epc-scp-quick-grid epc-tcp-more-grid">
			<?php foreach ($moreLinks as $link) { ?>
			<a class="epc-scp-quick-card epc-cp-card epc-scp-quick-card--<?php echo epc_tcp_dash_h($link['tone']); ?>" href="<?php echo epc_tcp_dash_h($link['url']); ?>" title="<?php echo epc_tcp_dash_h($link['hint']); ?>">
				<span class="epc-scp-quick-card__icon"><i class="fa <?php echo epc_tcp_dash_h($link['icon']); ?>"></i></span>
				<span class="epc-scp-quick-card__label"><?php echo epc_tcp_dash_h($link['label']); ?></span>
				<span class="epc-scp-quick-card__hint"><?php echo epc_tcp_dash_h($link['hint']); ?></span>
			</a>
			<?php } ?>
		</div>
	</details>

	<p class="epc-tcp-help">
		Tip: use the left menu search to jump to any page. Share the
		<a href="/brochure" target="_blank" rel="noopener">product brochure</a>,
		the full
		<a href="<?php echo epc_tcp_dash_h($base . '/control/cp_brochure'); ?>" target="_blank" rel="noopener">CP brochure</a>
		(every function), or the
		<a href="https://www.ecomae.com/platform/faq" target="_blank" rel="noopener">Platform FAQ</a>.
	</p>
</div>
