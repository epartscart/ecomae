<?php
/**
 * eParts Cart — complete Control Panel guideline.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$epc_cp_login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $epc_cp_login
		. '">log in to the control panel</a> to view this guide.</div>';
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/control_helper.php';

function epc_cpg_h($value)
{
	return htmlspecialchars((string)$value, ENT_QUOTES, 'UTF-8');
}

function epc_cpg_item_label($caption)
{
	if (function_exists('translate_str_by_key') && preg_match('/^[A-Za-z0-9_]+$/', (string)$caption)) {
		return translate_str_by_key($caption);
	}
	if (is_numeric($caption) && function_exists('translate_str_by_id')) {
		return translate_str_by_id((int)$caption);
	}
	return (string)$caption;
}

function epc_cpg_page_hints()
{
	return array(
		'/shop/price-management' => 'Customer profiles (Retail, Wholesale…), guest margin, brand & article pricing rules, VAT. Includes live demo calculator.',
		'/shop/prices/guide' => 'Step-by-step: upload CSV price lists, multi-vendor Excel, map columns, import to warehouse stock.',
		'/shop/prices/prices_edit' => 'Browse imported price rows, preview site price per customer profile, edit lines.',
		'/shop/prices' => 'Price list manager — upload, schedule, and monitor supplier price files.',
		'/shop/orders/oms-guide' => 'Daily OMS: open queue → console areas (items, pay, docs, status, messages) → complete.',
		'/shop/orders/guide' => 'Full order workflow: registration → checkout → emails → supplier LPO → CP processing.',
		'/shop/orders/whatsapp-guide' => 'WhatsApp Phase 1: wa.me share buttons — customer quotes, staff order/LPO messages (EN+AR).',
		'/shop/marketing/marketing' => 'Marketing & growth — 10 strategies (SEO, ads, marketplaces, WhatsApp, trust, international, email, B2B) with follow/review/KPI tracking.',
		'/shop/orders/orders' => 'OMS one-page console: open orders, manage items/payment/docs/status/messages.',
		'/shop/parts_agent_chats' => 'Review AI Parts Expert chat sessions — customer details, country, full transcript.',
		'/shop/logistics' => 'Hub for warehouses, pickup points, delivery modes, and stock management.',
		'/shop/logistics/storages' => 'Warehouses — address, LPO e-mail for supplier purchase orders.',
		'/shop/logistics/stock' => 'Stock quantities by category and product.',
		'/shop/crosses' => 'Cross-reference (interchange) management for part numbers.',
		'/shop/demand_countries' => 'Export demand countries and vehicle tags for market intelligence.',
		'/users/customer_approvals' => 'Approve new Retail / Wholesale registrations and assign currency.',
		'/users/usermanager' => 'Search customers, edit profile, assign price profile group.',
		'/control/config' => 'Site settings: shop, e-mail, SMS, payment, agent toggle, contacts.',
		'/control/notifications_settings' => 'E-mail and SMS notification templates for orders and system events.',
		'/control/communications' => 'Test e-mail / SMS delivery from the control panel.',
		'/control/cp-guideline' => 'This page — complete CP map and workflows.',
		'/shop/finance/erp' => 'ERP: sales revenue, customer receivables, supplier payables, purchases, cash & bank entries.',
		'/shop/finance/erp/guide' => 'Step-by-step ERP guide — sales, AR, AP, cash/bank, COA, GL, P&L, balance sheet.',
	);
}

function epc_cpg_hint_for_url($url, array $hints)
{
	$url = (string)$url;
	foreach ($hints as $pattern => $hint) {
		if (strpos($url, $pattern) !== false) {
			return $hint;
		}
	}
	return 'Open this section from the left menu. Access depends on your admin group.';
}

function epc_cpg_load_menu_tabs($db_link, $backend)
{
	$tabs = array();
	$groups_query = $db_link->prepare('SELECT * FROM `control_groups` ORDER BY `order` ASC;');
	$groups_query->execute();
	while ($group = $groups_query->fetch(PDO::FETCH_ASSOC)) {
		$tabs[(string)$group['id']] = array(
			'caption' => epc_cpg_item_label($group['caption']),
			'items' => array(),
		);
	}

	$items_query = $db_link->prepare('SELECT * FROM `control_items` ORDER BY `order` ASC;');
	$items_query->execute();
	while ($item = $items_query->fetch(PDO::FETCH_ASSOC)) {
		$item['url'] = str_replace(array('<backend>'), $backend, $item['url']);
		if (!is_anable($item) && (int)(isset($item['show_anyway']) ? $item['show_anyway'] : 0) !== 1) {
			continue;
		}
		$gid = (string)$item['items_group'];
		if (!isset($tabs[$gid])) {
			$tabs[$gid] = array('caption' => 'Other', 'items' => array());
		}
		$tabs[$gid]['items'][] = $item;
	}

	return $tabs;
}

$backend = $DP_Config->backend_dir;
$hints = epc_cpg_page_hints();
$menu_tabs = epc_cpg_load_menu_tabs($db_link, $backend);
$base_cp = '/' . $backend;
$isSuperCp = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-cpg-dashboard',
	'hero' => array(
		'badge' => $isSuperCp ? 'Super CP' : 'Control panel',
		'title' => 'Control Panel guideline',
		'sub' => 'Use the <strong>left sidebar</strong> (SYSTEM · SHOP · CATALOG · CONTENT · USERS · EXTENSIONS) to open any task. This page maps every menu item, daily workflows, and links to detailed sub-guides.',
		'html_sub' => true,
		'actions' => array(
			array('label' => 'CP home', 'icon' => 'fa-home', 'url' => $base_cp),
			array('label' => 'Price management', 'icon' => 'fa-tags', 'url' => $base_cp . '/shop/price-management', 'primary' => true),
		),
	),
));
?>

<p class="text-muted" style="margin:0 0 14px;max-width:52rem;line-height:1.45;">
	<strong>Platform roles:</strong> <code>epartscart.com</code> is spare-parts (auto_parts).
	<code>ecomae.com/cp</code> is overall platform control. Common OMS / commerce CP packs stay in sync across tenants; industry tools (vehicle catalog, etc.) stay scoped.
</p>
<div class="epc-cpg-quick">
	<a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">
		<strong>Price profiles &amp; margins</strong>
		<span>Retail / Wholesale profiles, guest margin, brand &amp; article rules — with live demo.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/prices/guide');?>">
		<strong>Price upload guide</strong>
		<span>Import supplier CSV price lists into warehouse stock.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/orders/oms-guide');?>">
		<strong>OMS daily guide</strong>
		<span>Step-by-step areas: open queue → items → payment → docs → status → messages → complete.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/orders/guide');?>">
		<strong>Order fulfilment guide</strong>
		<span>Checkout → customer e-mail → supplier LPO → staff processing.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/orders/whatsapp-guide');?>">
		<strong>WhatsApp sharing guide</strong>
		<span>Customer quotes, cart share, staff order &amp; LPO messages (bilingual EN/AR).</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/parts_agent_chats');?>">
		<strong>AI Parts Expert chats</strong>
		<span>Review storefront agent conversations and customer details.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/users/customer_approvals');?>">
		<strong>Customer trade approvals</strong>
		<span>Approve Retail / Wholesale registrations.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/control/config');?>">
		<strong>Site configuration</strong>
		<span>Shop settings, e-mail, SMS, payment, agent on/off.</span>
	</a>
</div>

<div class="hpanel">
	<div class="panel-heading hbuilt">Daily workflows — step by step</div>
	<div class="panel-body">
		<div class="panel-group" id="epc_cpg_workflows">
			<div class="panel panel-default epc-cpg-section">
				<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#wf_prices">1. Prices &amp; stock</a></h5></div>
				<div id="wf_prices" class="panel-collapse collapse in">
					<div class="panel-body epc-cpg-flow">
						<span class="epc-cpg-badge">SHOP</span>
						<ol>
							<li><strong>Upload price list</strong> — <a href="<?=epc_cpg_h($base_cp . '/shop/prices/guide');?>">Price upload guide</a> or <a href="<?=epc_cpg_h($base_cp . '/shop/prices');?>">Price manager</a>.</li>
							<li><strong>Verify rows</strong> — <a href="<?=epc_cpg_h($base_cp . '/shop/prices/prices_edit');?>">Edit price list rows</a> — check site preview per profile.</li>
							<li><strong>Set customer margins</strong> — <a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">Price management</a>: profile overall %, brand rules, article rules, guest margin.</li>
							<li><strong>Assign customer to profile</strong> — same page → Assign customer (e.g. YAWER → Retail or Wholesale).</li>
						</ol>
					</div>
				</div>
			</div>
			<div class="panel panel-default epc-cpg-section">
				<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#wf_orders" class="collapsed">2. Orders &amp; fulfilment</a></h5></div>
				<div id="wf_orders" class="panel-collapse collapse">
					<div class="panel-body epc-cpg-flow">
						<span class="epc-cpg-badge">SHOP</span>
						<ol>
							<li>Customer registers → <a href="<?=epc_cpg_h($base_cp . '/users/customer_approvals');?>">Customer approvals</a> (if trade account).</li>
							<li>Customer searches parts on storefront → adds to cart → checkout.</li>
							<li>System sends e-mails (customer, staff, supplier LPO) — see <a href="<?=epc_cpg_h($base_cp . '/shop/orders/guide');?>">Order fulfilment guide</a>.</li>
							<li>Staff opens <a href="<?=epc_cpg_h($base_cp . '/shop/orders/orders');?>">Orders</a> → update status, edit lines, print documents, <a href="<?=epc_cpg_h($base_cp . '/shop/orders/whatsapp-guide');?>">WhatsApp share</a>.</li>
							<li>Configure warehouses &amp; LPO e-mails: <a href="<?=epc_cpg_h($base_cp . '/shop/logistics/storages');?>">Warehouses</a>.</li>
						</ol>
					</div>
				</div>
			</div>
			<div class="panel panel-default epc-cpg-section">
				<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#wf_customers" class="collapsed">3. Customers &amp; profiles</a></h5></div>
				<div id="wf_customers" class="panel-collapse collapse">
					<div class="panel-body epc-cpg-flow">
						<span class="epc-cpg-badge">USERS</span>
						<ol>
							<li><a href="<?=epc_cpg_h($base_cp . '/users/usermanager');?>">User manager</a> — find customer by e-mail or ID.</li>
							<li><a href="<?=epc_cpg_h($base_cp . '/users/customer_approvals');?>">Customer approvals</a> — approve Retail / Wholesale, set currency.</li>
							<li><a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">Price management</a> — assign profile group (Retail, Wholesale, CIS, GCC).</li>
							<li>Customer logs in → sees prices with their profile margins on the storefront.</li>
						</ol>
					</div>
				</div>
			</div>
			<div class="panel panel-default epc-cpg-section">
				<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#wf_agent" class="collapsed">4. AI Parts Expert</a></h5></div>
				<div id="wf_agent" class="panel-collapse collapse">
					<div class="panel-body epc-cpg-flow">
						<span class="epc-cpg-badge">SHOP</span>
						<ol>
							<li>Enable agent: <a href="<?=epc_cpg_h($base_cp . '/control/config');?>">Configuration</a> → epc_parts_agent_enabled.</li>
							<li>Customers use <strong>AI PARTS EXPERT</strong> widget on the storefront.</li>
							<li>Review chats: <a href="<?=epc_cpg_h($base_cp . '/shop/parts_agent_chats');?>">AI agent chats</a> — customer, country, transcript.</li>
						</ol>
					</div>
				</div>
			</div>
			<div class="panel panel-default epc-cpg-section">
				<div class="panel-heading"><h5 class="panel-title"><a data-toggle="collapse" href="#wf_system" class="collapsed">5. System &amp; notifications</a></h5></div>
				<div id="wf_system" class="panel-collapse collapse">
					<div class="panel-body epc-cpg-flow">
						<span class="epc-cpg-badge">SYSTEM</span>
						<ol>
							<li><a href="<?=epc_cpg_h($base_cp . '/control/config');?>">Configuration</a> — shop, SMTP, SMS, payment gateways.</li>
							<li><a href="<?=epc_cpg_h($base_cp . '/control/notifications_settings');?>">Notification settings</a> — order e-mail templates.</li>
							<li><a href="<?=epc_cpg_h($base_cp . '/control/communications');?>">Communications test</a> — send test e-mail / SMS.</li>
							<li>If banners show “Email/SMS not working” — fix SMTP/IMAP in Configuration first.</li>
						</ol>
					</div>
				</div>
			</div>
		</div>
	</div>
</div>

<?php foreach ($menu_tabs as $tab) { ?>
	<?php if (empty($tab['items'])) { continue; } ?>
	<div class="hpanel">
		<div class="panel-heading hbuilt"><?=epc_cpg_h($tab['caption']);?> — menu map</div>
		<div class="panel-body">
			<div class="table-responsive">
				<table class="table table-striped epc-cpg-menu-table">
					<thead><tr><th style="width:28%;">Page</th><th>What it does</th><th style="width:100px;"></th></tr></thead>
					<tbody>
						<?php foreach ($tab['items'] as $item) {
							$label = epc_cpg_item_label($item['caption']);
							$url = (string)$item['url'];
							$hint = epc_cpg_hint_for_url($url, $hints);
							?>
						<tr>
							<td class="epc-cpg-link">
								<?php if (!empty($item['fontawesome_class'])) { ?><i class="<?=epc_cpg_h($item['fontawesome_class']);?>"></i> <?php } ?>
								<?=epc_cpg_h($label);?>
							</td>
							<td class="epc-cpg-hint"><?=epc_cpg_h($hint);?></td>
							<td><a class="btn btn-primary btn-xs" href="<?=epc_cpg_h($url);?>">Open</a></td>
						</tr>
						<?php } ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
<?php } ?>

<div class="hpanel">
	<div class="panel-heading hbuilt">Margin levels (price management summary)</div>
	<div class="panel-body">
		<p>Full guide with live demo: <a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>"><strong>Price management</strong></a></p>
		<table class="table table-bordered epc-cpg-margin-table">
			<thead><tr><th>Level</th><th>Where</th><th>Example</th></tr></thead>
			<tbody>
				<tr><td>Guest overall</td><td>Price management → Guest margin</td><td>+40% for visitors not logged in</td></tr>
				<tr><td>Profile overall</td><td>Profile table → Overall margin %</td><td>Wholesale +5% on all brands</td></tr>
				<tr><td>Brand</td><td>Add / update brand rule</td><td>Retail MAZDA +15%</td></tr>
				<tr><td>Article</td><td>Add / update article rule</td><td>Retail TOYOTA 1140051020 +20%</td></tr>
			</tbody>
		</table>
		<p class="text-muted" style="font-size:12px;">Applied in order: profile → brand → article → guest. Each stacks on the previous price.</p>
	</div>
</div>

<div class="hpanel">
	<div class="panel-heading hbuilt">Troubleshooting</div>
	<div class="panel-body">
		<ul style="line-height:1.65;">
			<li><strong>Cannot open a CP page / “privileges” error</strong> — your admin group needs access in Content → access rights, or run the relevant setup script (e.g. epc-parts-agent-cp-access-fix.php).</li>
			<li><strong>Prices wrong on storefront</strong> — check customer profile assignment and rules in <a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">Price management</a>; preview in <a href="<?=epc_cpg_h($base_cp . '/shop/prices/prices_edit');?>">Prices edit</a>.</li>
			<li><strong>Order e-mails not sent</strong> — <a href="<?=epc_cpg_h($base_cp . '/control/communications');?>">Communications test</a> + SMTP in <a href="<?=epc_cpg_h($base_cp . '/control/config');?>">Configuration</a>.</li>
			<li><strong>AI agent not on site</strong> — Configuration → epc_parts_agent_enabled = 1; clear cache / hard refresh storefront.</li>
			<li><strong>Red banner “Email/SMS not working”</strong> — fix SMTP and SMS operator settings under SYSTEM → Configuration / Communications.</li>
		</ul>
	</div>
</div>

<p class="text-muted" style="font-size:12px;margin-top:8px;">
	Last updated: <?=epc_cpg_h(date('Y-m-d'));?> · eParts Cart CP guideline ·
	<a href="<?=epc_cpg_h($base_cp);?>">Control panel home</a>
</p>
<?php epc_cp_page_frame_close(); ?>
