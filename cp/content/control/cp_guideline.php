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
		'/shop/price-management' => 'Supplier/warehouse margins (overall → brand → article), customer profiles, guest margin, brand & article rules, VAT. Live calculator.',
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
		'/users/customer_mgmt' => 'Customer directory — orders, invoices, advances, returns, e-invoice profile.',
		'/users/usermanager' => 'Search customers, edit profile, assign price profile group.',
		'/control/config' => 'Settings redesign: jump-nav groups, Frontend/Backend impact chips for storefront-facing values (contacts, currency, search, refunds).',
		'/control/notifications_settings' => 'E-mail and SMS notification templates for orders and system events.',
		'/control/communications' => 'Test e-mail / SMS delivery from the control panel.',
		'/control/cp-guideline' => 'This page — visual CP map, daily workflows, and Settings guide.',
		'/shop/finance/erp' => 'ERP: sales revenue, customer receivables, supplier payables, purchases, cash & bank entries.',
		'/shop/finance/erp/guide' => 'Step-by-step ERP guide — sales, AR, AP, cash/bank, COA, GL, P&L, balance sheet.',
		'/shop/document_control/document_control' => 'Company docs, letterheads, and document templates used on invoices and PDFs.',
		'/shop/logistics/whatsapp-guide' => 'WhatsApp logistics guide — share and notify flows for warehouse / delivery staff.',
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
		'sub' => 'A simple map of the left sidebar, daily workflows, and Settings. Open any card to jump straight into the task.',
		'html_sub' => false,
		'actions' => array(
			array('label' => 'CP home', 'icon' => 'fa-home', 'url' => $base_cp),
			array('label' => 'Settings', 'icon' => 'fa-sliders', 'url' => $base_cp . '/control/config', 'primary' => true),
			array('label' => 'Price management', 'icon' => 'fa-tags', 'url' => $base_cp . '/shop/price-management'),
			array('label' => 'Full CP brochure', 'icon' => 'fa-book', 'url' => $base_cp . '/control/cp_brochure'),
		),
	),
));
?>

<div class="epc-cpg-roles">
	<span class="epc-cpg-roles__label">Roles</span>
	<span class="epc-cpg-chip"><i class="fa fa-car"></i> epartscart.com — spare parts</span>
	<span class="epc-cpg-chip"><i class="fa fa-building"></i> ecomae.com/cp — platform</span>
	<span class="epc-cpg-chip"><i class="fa fa-bars"></i> Sidebar: SYSTEM · SHOP · CATALOG · USERS</span>
	<a class="epc-cpg-chip" href="/brochure" target="_blank" rel="noopener"><i class="fa fa-file-text-o"></i> Product brochure</a>
</div>

<div class="epc-cpg-quick">
	<a href="<?=epc_cpg_h($base_cp . '/control/config');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-sliders"></i></span>
		<strong>Settings</strong>
		<span>Jump-nav groups + Frontend / Backend impact chips for storefront values.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-percent"></i></span>
		<strong>Price profiles</strong>
		<span>Retail / Wholesale margins, guest %, brand &amp; article rules.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/prices/guide');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-upload"></i></span>
		<strong>Price upload</strong>
		<span>Import supplier CSV / multi-vendor Excel into warehouses.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/orders/oms-guide');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-list-alt"></i></span>
		<strong>OMS daily</strong>
		<span>Queue → items → pay → docs → status → messages → done.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/orders/guide');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-truck"></i></span>
		<strong>Fulfilment</strong>
		<span>Checkout → e-mails → supplier LPO → staff processing.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/shop/orders/whatsapp-guide');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-whatsapp"></i></span>
		<strong>WhatsApp</strong>
		<span>Quotes, cart share, staff order &amp; LPO messages (EN + AR).</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/users/customer_mgmt');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-users"></i></span>
		<strong>Customers</strong>
		<span>Directory, orders, invoices, advances, returns.</span>
	</a>
	<a href="<?=epc_cpg_h($base_cp . '/users/customer_approvals');?>">
		<span class="epc-cpg-quick__ico"><i class="fa fa-check-square-o"></i></span>
		<strong>Trade approvals</strong>
		<span>Approve Retail / Wholesale registrations &amp; currency.</span>
	</a>
</div>

<div class="epc-cpg-block">
	<div class="epc-cpg-block__head">
		<div>
			<h3>Daily workflows</h3>
			<p>Follow the arrows left → right. Each step links into the live CP page.</p>
		</div>
	</div>
	<div class="epc-cpg-flows">
		<div class="epc-cpg-flow">
			<div class="epc-cpg-flow__top">
				<span class="epc-cpg-flow__num">1</span>
				<h4>Prices &amp; stock</h4>
				<span class="epc-cpg-flow__badge">Shop</span>
			</div>
			<div class="epc-cpg-steps">
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 1</span>
					<strong>Upload list</strong>
					<a href="<?=epc_cpg_h($base_cp . '/shop/prices/guide');?>">Price upload guide</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 2</span>
					<strong>Verify rows</strong>
					<a href="<?=epc_cpg_h($base_cp . '/shop/prices/prices_edit');?>">Prices edit preview</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 3</span>
					<strong>Set margins</strong>
					<a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">Price management</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 4</span>
					<strong>Assign profile</strong>
					<span>Retail / Wholesale / guest</span>
				</div>
			</div>
		</div>

		<div class="epc-cpg-flow">
			<div class="epc-cpg-flow__top">
				<span class="epc-cpg-flow__num">2</span>
				<h4>Orders &amp; fulfilment</h4>
				<span class="epc-cpg-flow__badge">Shop</span>
			</div>
			<div class="epc-cpg-steps">
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 1</span>
					<strong>Approve trade</strong>
					<a href="<?=epc_cpg_h($base_cp . '/users/customer_approvals');?>">Customer approvals</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 2</span>
					<strong>Customer orders</strong>
					<span>Search → cart → checkout</span>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 3</span>
					<strong>Process in OMS</strong>
					<a href="<?=epc_cpg_h($base_cp . '/shop/orders/orders');?>">Orders console</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 4</span>
					<strong>Share / LPO</strong>
					<a href="<?=epc_cpg_h($base_cp . '/shop/orders/whatsapp-guide');?>">WhatsApp guide</a>
				</div>
			</div>
		</div>

		<div class="epc-cpg-flow">
			<div class="epc-cpg-flow__top">
				<span class="epc-cpg-flow__num">3</span>
				<h4>Customers &amp; profiles</h4>
				<span class="epc-cpg-flow__badge">Users</span>
			</div>
			<div class="epc-cpg-steps">
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 1</span>
					<strong>Find customer</strong>
					<a href="<?=epc_cpg_h($base_cp . '/users/customer_mgmt');?>">Customer management</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 2</span>
					<strong>Approve account</strong>
					<a href="<?=epc_cpg_h($base_cp . '/users/customer_approvals');?>">Trade approvals</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 3</span>
					<strong>Assign margin</strong>
					<a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">Price profiles</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 4</span>
					<strong>Storefront prices</strong>
					<span>Customer sees their profile %</span>
				</div>
			</div>
		</div>

		<div class="epc-cpg-flow">
			<div class="epc-cpg-flow__top">
				<span class="epc-cpg-flow__num">4</span>
				<h4>AI Parts Expert</h4>
				<span class="epc-cpg-flow__badge">Shop</span>
			</div>
			<div class="epc-cpg-steps">
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 1</span>
					<strong>Enable agent</strong>
					<a href="<?=epc_cpg_h($base_cp . '/control/config');?>">Settings → agent toggle</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 2</span>
					<strong>Customers chat</strong>
					<span>Storefront AI widget</span>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 3</span>
					<strong>Review chats</strong>
					<a href="<?=epc_cpg_h($base_cp . '/shop/parts_agent_chats');?>">AI agent chats</a>
				</div>
			</div>
		</div>

		<div class="epc-cpg-flow">
			<div class="epc-cpg-flow__top">
				<span class="epc-cpg-flow__num">5</span>
				<h4>System &amp; notifications</h4>
				<span class="epc-cpg-flow__badge">System</span>
			</div>
			<div class="epc-cpg-steps">
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 1</span>
					<strong>Site Settings</strong>
					<a href="<?=epc_cpg_h($base_cp . '/control/config');?>">Contacts, SMTP, shop</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 2</span>
					<strong>Templates</strong>
					<a href="<?=epc_cpg_h($base_cp . '/control/notifications_settings');?>">Notification settings</a>
				</div>
				<div class="epc-cpg-step">
					<span class="epc-cpg-step__n">Step 3</span>
					<strong>Send a test</strong>
					<a href="<?=epc_cpg_h($base_cp . '/control/communications');?>">Communications test</a>
				</div>
			</div>
		</div>
	</div>
</div>

<div class="epc-cpg-block">
	<div class="epc-cpg-block__head">
		<div>
			<h3>Settings page — what changed</h3>
			<p>Open <a href="<?=epc_cpg_h($base_cp . '/control/config');?>">Settings</a>. Blue chips mark values that affect the public storefront.</p>
		</div>
	</div>
	<div class="epc-cpg-settings">
		<div class="epc-cpg-settings__visual">
			<h4><i class="fa fa-desktop"></i> Frontend vs Backend</h4>
			<p>Use the left jump nav to open a group. Read the blue “Effect on frontend” line before you save.</p>
			<div class="epc-cpg-settings__pills">
				<span>Frontend — customers see it</span>
				<span>Backend — CP / integrations only</span>
			</div>
		</div>
		<ul class="epc-cpg-settings__list">
			<li><i class="fa fa-phone"></i><span><strong>Contacts &amp; footer</strong> — phone, WhatsApp, offices appear on the public site.</span></li>
			<li><i class="fa fa-shopping-cart"></i><span><strong>Online store</strong> — currency, rounding, guest checkout, partial payment.</span></li>
			<li><i class="fa fa-search"></i><span><strong>Article search</strong> — results table, filters, async search layout.</span></li>
			<li><i class="fa fa-undo"></i><span><strong>Refunds</strong> — customer return requests and withholding text.</span></li>
			<li><i class="fa fa-envelope"></i><span><strong>E-mail / updates mailbox</strong> — mostly backend; customers only feel SMTP “from” name.</span></li>
		</ul>
	</div>
</div>

<div class="epc-cpg-block">
	<div class="epc-cpg-block__head">
		<div>
			<h3>Menu map</h3>
			<p>Every item from your left sidebar, grouped, with a short “what it does”.</p>
		</div>
	</div>
	<div class="epc-cpg-menu-grid">
		<?php foreach ($menu_tabs as $tab) {
			if (empty($tab['items'])) {
				continue;
			}
			$itemCount = count($tab['items']);
			?>
		<div class="epc-cpg-menu-card">
			<div class="epc-cpg-menu-card__head">
				<?=epc_cpg_h($tab['caption']);?>
				<small><?= (int) $itemCount; ?> pages</small>
			</div>
			<?php foreach ($tab['items'] as $item) {
				$label = epc_cpg_item_label($item['caption']);
				$url = (string) $item['url'];
				$hint = epc_cpg_hint_for_url($url, $hints);
				?>
			<div class="epc-cpg-menu-row">
				<div>
					<div class="epc-cpg-menu-row__title">
						<?php if (!empty($item['fontawesome_class'])) { ?><i class="<?=epc_cpg_h($item['fontawesome_class']);?>"></i><?php } ?>
						<?=epc_cpg_h($label);?>
					</div>
					<span class="epc-cpg-menu-row__hint"><?=epc_cpg_h($hint);?></span>
				</div>
				<a class="btn btn-primary btn-xs" href="<?=epc_cpg_h($url);?>">Open</a>
			</div>
			<?php } ?>
		</div>
		<?php } ?>
	</div>
</div>

<div class="epc-cpg-block">
	<div class="epc-cpg-block__head">
		<div>
			<h3>Margin stack</h3>
			<p>Applied in order on the storefront. Full demo: <a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">Price management</a>.</p>
		</div>
	</div>
	<div class="epc-cpg-margin">
		<div class="epc-cpg-margin__item">
			<strong>1 · Profile</strong>
			<span>Wholesale +5% on all brands</span>
		</div>
		<div class="epc-cpg-margin__item">
			<strong>2 · Brand</strong>
			<span>Retail MAZDA +15%</span>
		</div>
		<div class="epc-cpg-margin__item">
			<strong>3 · Article</strong>
			<span>TOYOTA 1140051020 +20%</span>
		</div>
		<div class="epc-cpg-margin__item">
			<strong>4 · Guest</strong>
			<span>+40% when not logged in</span>
		</div>
	</div>
</div>

<div class="epc-cpg-block">
	<div class="epc-cpg-block__head">
		<div>
			<h3>Troubleshooting</h3>
			<p>Quick fixes for the most common CP issues.</p>
		</div>
	</div>
	<div class="epc-cpg-trouble">
		<div class="epc-cpg-trouble__item">
			<strong>Cannot open a page / privileges</strong>
			Your admin group needs access under Content → access rights.
		</div>
		<div class="epc-cpg-trouble__item">
			<strong>Prices wrong on storefront</strong>
			Check profile rules in <a href="<?=epc_cpg_h($base_cp . '/shop/price-management');?>">Price management</a>; preview in Prices edit.
		</div>
		<div class="epc-cpg-trouble__item">
			<strong>Order e-mails not sent</strong>
			<a href="<?=epc_cpg_h($base_cp . '/control/communications');?>">Communications test</a> + SMTP under <a href="<?=epc_cpg_h($base_cp . '/control/config');?>">Settings</a> → E-mail.
		</div>
		<div class="epc-cpg-trouble__item">
			<strong>AI agent missing on site</strong>
			Settings → enable <code>epc_parts_agent_enabled</code>, then hard-refresh the storefront.
		</div>
		<div class="epc-cpg-trouble__item">
			<strong>Red “Email/SMS not working” banner</strong>
			Fix SMTP / SMS under Settings and Communications before other work.
		</div>
		<div class="epc-cpg-trouble__item">
			<strong>Changed a contact but site unchanged</strong>
			In Settings, confirm the blue Frontend chip, Save, then reload the public page.
		</div>
	</div>
</div>

<p class="epc-cpg-foot">
	Last updated: <?=epc_cpg_h(date('Y-m-d'));?> · eParts Cart CP guideline ·
	<a href="<?=epc_cpg_h($base_cp);?>">Control panel home</a>
</p>
<?php epc_cp_page_frame_close(); ?>
