<?php
/**
 * Integrations operator guide — every catalog module in one CP page.
 * URL: /cp/control/portal/epc_integrations_guide
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	echo '<div class="alert alert-warning">Admin login required.</div>';
	return;
}

$backend = epc_int_backend();
$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$hubUrl = '/' . $backend . '/control/portal/epc_integrations_hub';
$catalog = epc_integrations_catalog();
$categories = epc_integrations_categories();

/** @var array<string, array{summary:string,steps:array<int,string>,tips:array<int,string>,links:array<int,array{label:string,url:string}>}> */
$sections = array(
	'email_smtp' => array(
		'summary' => 'Deliver order confirmations, OTP codes, and staff alerts through SMTP.',
		'steps' => array(
			'Open Email / SMTP settings (tenant page for shops; Super CP auth settings for platform defaults).',
			'Enter host, port, encryption, username, and password. Save.',
			'Use Send test email — confirm delivery before go-live.',
			'Place a test order and verify the customer receipt arrives.',
		),
		'tips' => array(
			'Prefer a dedicated mailbox (orders@…) with SPF/DKIM aligned to your domain.',
			'If tests fail, check firewall allow-lists for outbound 465/587.',
		),
		'links' => array(
			array('label' => 'Tenant SMTP', 'url' => '/' . $backend . '/control/portal/epc_tenant_email_settings'),
			array('label' => 'Auth settings', 'url' => '/' . $backend . '/control/portal/epc_cp_auth_settings'),
		),
	),
	'oauth' => array(
		'summary' => 'Google / Microsoft (and related) OAuth for CP or storefront login — Super CP configures app credentials.',
		'steps' => array(
			'Create OAuth clients in Google Cloud / Microsoft Entra with redirect URIs for your hosts.',
			'Paste Client ID / Secret under Super CP → Auth settings.',
			'Enable the providers you want, then test login in an incognito window.',
			'Toggle the oauth feature per tenant under Tenant features if a shop should not use it.',
		),
		'tips' => array(
			'Redirect URI mismatches are the #1 failure — copy exact https://host/… callback paths.',
		),
		'links' => array(
			array('label' => 'Auth settings', 'url' => '/' . $backend . '/control/portal/epc_cp_auth_settings'),
			array('label' => 'Tenant features', 'url' => '/' . $backend . '/control/portal/epc_tenant_features'),
		),
	),
	'registration_enhanced' => array(
		'summary' => 'Stronger signup and verification policies controlled from Super CP auth settings.',
		'steps' => array(
			'Review registration fields and verification requirements under Auth settings.',
			'Ensure SMTP works so verification emails send.',
			'Create a test customer account on the tenant storefront.',
			'Confirm the account appears in Users and can place an order.',
		),
		'tips' => array(
			'Keep OTP / email verification on for B2B tenants handling trade accounts.',
		),
		'links' => array(
			array('label' => 'Auth settings', 'url' => '/' . $backend . '/control/portal/epc_cp_auth_settings'),
		),
	),
	'whatsapp' => array(
		'summary' => 'Phase 1 wa.me sharing — staff open WhatsApp with bilingual EN/AR prefilled order text.',
		'steps' => array(
			'Open the WhatsApp sharing guide and confirm sales display name / phone.',
			'From Orders, use Share on WhatsApp on a sample order.',
			'Verify the message opens with English + Arabic text and correct order link.',
			'Train the desk to send from phone or desktop WhatsApp (no Business API required yet).',
		),
		'tips' => array(
			'For broadcast campaigns, use Marketing broadcast instead of one-off order shares.',
		),
		'links' => array(
			array('label' => 'WhatsApp guide', 'url' => '/' . $backend . '/shop/orders/whatsapp-guide'),
			array('label' => 'Orders', 'url' => '/' . $backend . '/shop/orders/orders'),
		),
	),
	'payment_gateways' => array(
		'summary' => 'Checkout rails: Telr, GCC BNPL (Tabby, Tamara, MyFatoorah, Tap, HyperPay…), Pakistan wallets, crypto, and settlement accounts.',
		'steps' => array(
			'Open Shop → Payments and enable the methods you need.',
			'Enter merchant keys / store IDs; save each gateway.',
			'Optional: Payments → Accounts to set who receives funds (platform / office / vendor).',
			'Run a small live or sandbox purchase and confirm notify / settlement rows.',
		),
		'tips' => array(
			'Keep at least one card gateway + one local method for GCC shoppers.',
			'Crypto (NOWPayments) needs a live IPN URL reachable from the internet.',
		),
		'links' => array(
			array('label' => 'Payments', 'url' => '/' . $backend . '/shop/payments/payments'),
			array('label' => 'Accounts tab', 'url' => '/' . $backend . '/shop/payments/payments?tab=accounts'),
		),
	),
	'pos' => array(
		'summary' => 'Counter terminal for walk-in sales linked to ERP stock and receipts.',
		'steps' => array(
			'Confirm the POS feature is enabled for the tenant.',
			'Open POS Terminal, select storage / cashier context.',
			'Ring a test sale (cash or card tender) and print/preview the receipt.',
			'Verify the order and stock movement in ERP / Orders.',
		),
		'tips' => array(
			'Use a dedicated browser profile on the counter PC to avoid session mix-ups.',
		),
		'links' => array(
			array('label' => 'POS Terminal', 'url' => '/' . $backend . '/shop/pos/terminal'),
		),
	),
	'tax_toolkit' => array(
		'summary' => 'Market tax / VAT profiles driven by the tenant country registration (Super CP configures toolkit).',
		'steps' => array(
			'Confirm the tenant market/country under Tenant hub / country profile.',
			'Super CP: review Tax Toolkit manage for available profiles.',
			'In ERP, verify tax lines on a sample invoice match the market rate.',
			'Document any exemptions for B2B / free-zone customers.',
		),
		'tips' => array(
			'Tax follows market — ask Super CP if the registered country must change.',
		),
		'links' => array(
			array('label' => 'ERP', 'url' => '/' . $backend . '/shop/finance/erp'),
		),
	),
	'custom_shipping' => array(
		'summary' => 'Customs declarations, LGP warehouse intake, and declaration reports inside ERP.',
		'steps' => array(
			'Read the Custom & Shipping operator guide for declaration types.',
			'Open ERP → Custom & Shipping and create a sample declaration.',
			'Run one of the built-in reports to confirm filters and export.',
			'Train ops on which declaration type maps to each shipment lane.',
		),
		'tips' => array(
			'Keep HS codes and consignee details accurate — reports inherit declaration fields.',
		),
		'links' => array(
			array('label' => 'Shipping guide', 'url' => '/' . $backend . '/control/portal/epc_custom_shipping_guide'),
			array('label' => 'ERP module', 'url' => '/' . $backend . '/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1'),
		),
	),
	'social_media_hub' => array(
		'summary' => 'Plan, generate, and publish social content with account links and calendars.',
		'steps' => array(
			'Open Social media hub and connect / list your brand accounts.',
			'Read the in-hub Guide tab for posting specs per network.',
			'Create a draft post, review AI suggestions, schedule or copy out.',
			'Track which posts shipped and update weekly.',
		),
		'tips' => array(
			'Reuse Marketing broadcast for WhatsApp/email blasts; keep Social hub for public networks.',
		),
		'links' => array(
			array('label' => 'Social hub', 'url' => '/' . $backend . '/control/portal/epc_social_media_hub'),
			array('label' => 'Guide tab', 'url' => '/' . $backend . '/control/portal/epc_social_media_hub?tab=guide'),
		),
	),
	'marketing_broadcast' => array(
		'summary' => 'Bulk email and WhatsApp campaigns with audience segments.',
		'steps' => array(
			'Open Marketing broadcast and complete the Guide tab once.',
			'Build or select an audience segment.',
			'Compose a campaign, send a test to yourself, then launch.',
			'Review delivery stats before the next blast.',
		),
		'tips' => array(
			'SMTP must be healthy before email campaigns; WhatsApp uses the sharing / API path configured for the tenant.',
		),
		'links' => array(
			array('label' => 'Broadcast hub', 'url' => '/' . $backend . '/control/portal/epc_marketing_broadcast'),
			array('label' => 'Guide', 'url' => '/' . $backend . '/control/portal/epc_marketing_broadcast?tab=guide'),
		),
	),
	'web_tracker' => array(
		'summary' => 'Wire GA4, Meta, TikTok (and similar) pixels / tags into the storefront.',
		'steps' => array(
			'Open Web tracker and paste measurement IDs / pixel IDs.',
			'Save and hard-refresh the storefront.',
			'Use browser tag assistants / debug views to confirm page_view fires.',
			'Place a test add-to-cart / purchase if conversion events are enabled.',
		),
		'tips' => array(
			'Never put secrets in page HTML — only public pixel IDs belong in the tracker UI.',
		),
		'links' => array(
			array('label' => 'Web tracker', 'url' => '/' . $backend . '/control/portal/epc_web_tracker'),
		),
	),
	'visual_page_editor' => array(
		'summary' => 'Compose landing and content blocks for the storefront without deploying PHP.',
		'steps' => array(
			'Open Visual page editor and pick the page / template to edit.',
			'Add blocks, set images and CTAs, preview on mobile width.',
			'Publish and verify the live URL.',
			'Keep one owner for homepage changes to avoid overwrite conflicts.',
		),
		'tips' => array(
			'Prefer one hero composition — avoid stacking promo cards in the first viewport.',
		),
		'links' => array(
			array('label' => 'Visual editor', 'url' => '/' . $backend . '/control/portal/epc_visual_page_editor'),
		),
	),
	'auto_price_ai' => array(
		'summary' => 'Discover competitive parts prices, compare lines, and import into your catalog.',
		'steps' => array(
			'Open Auto Price AI and complete the CP guide once.',
			'Run discovery for your market / product lines.',
			'Compare results, mark winners, and import selected rows.',
			'Re-check margins in ERP before publishing price changes.',
		),
		'tips' => array(
			'Discovery sources follow the tenant country profile — wrong market = wrong comps.',
		),
		'links' => array(
			array('label' => 'Auto Price engine', 'url' => '/' . $backend . '/control/portal/epc_auto_price_engine'),
			array('label' => 'Auto Price guide', 'url' => '/' . $backend . '/control/portal/epc_auto_price_guide'),
		),
	),
	'parts_agent' => array(
		'summary' => 'AI parts expert chats for staff (and optional storefront assist).',
		'steps' => array(
			'Confirm the feature is enabled under Tenant features.',
			'Open Parts agent chats and ask for a known OEM / aftermarket part.',
			'Validate suggested OEMs / cross refs against your catalog.',
			'Document which staff roles may use the agent with customers.',
		),
		'tips' => array(
			'Treat suggestions as assistive — always confirm fitment before quoting.',
		),
		'links' => array(
			array('label' => 'Parts agent', 'url' => '/' . $backend . '/shop/parts_agent_chats'),
		),
	),
	'api_integrations' => array(
		'summary' => 'Catalog & Price PRO client keys and tenant-scoped REST API access.',
		'steps' => array(
			'Open API clients manage to issue or rotate Catalog & Price PRO keys.',
			'Super CP: follow the API documentation guide for /epc-api/v1 scopes.',
			'Call /epc-api/v1/health then a scoped endpoint with X-API-Key.',
			'Store plain keys in a password manager — hashes only live in DB.',
		),
		'tips' => array(
			'Power BI keys need read:bi (or read:erp / read:*).',
		),
		'links' => array_values(array_filter(array(
			array('label' => 'API clients', 'url' => '/' . $backend . '/control/portal/epc_api_clients_manage'),
			$isSuper ? array('label' => 'API docs guide', 'url' => '/' . $backend . '/control/portal/epc_api_documentation_guide') : null,
		))),
	),
	'power_bi' => array(
		'summary' => 'Pull JSON/CSV datasets into Power BI Desktop; optional workspace embed in CP.',
		'steps' => array(
			'Create an API key with read:bi (or read:erp / read:*).',
			'Follow Portal → Power BI guide — load KPIs CSV first.',
			'Publish the report and confirm scheduled refresh once.',
			'Optional: save workspace / report IDs under Power BI settings for embed preview.',
		),
		'tips' => array(
			'Use HTTPS + header X-API-Key; never put keys in the report URL query string for production.',
		),
		'links' => array(
			array('label' => 'Power BI settings', 'url' => '/' . $backend . '/control/portal/epc_power_bi'),
			array('label' => 'Power BI guide', 'url' => '/' . $backend . '/control/portal/epc_power_bi_guide'),
		),
	),
	'mobile_apps' => array(
		'summary' => 'Installable PWA today; Capacitor native shells for CP, ERP, and storefront.',
		'steps' => array(
			'Open Mobile apps and confirm store URLs / package IDs.',
			'On a phone: open CP → Add to Home Screen; verify offline shell loads.',
			'For native builds, follow the Level 1 mobile guide (Capacitor targets).',
			'Test push / deep links only after PWA baseline works.',
		),
		'tips' => array(
			'PWA uses the real responsive CP — no separate mobile UI to maintain.',
		),
		'links' => array(
			array('label' => 'Mobile apps', 'url' => '/' . $backend . '/control/portal/epc_mobile_apps'),
		),
	),
	'tenant_registry' => array(
		'summary' => 'Super CP registry of live tenants, hosts, DB credentials, and feature flags.',
		'steps' => array(
			'Open Tenant hub and locate the shop by site_key / hostname.',
			'Confirm status is live and credentials resolve.',
			'Use Tenant features to enable/disable integrations for that shop.',
			'Smoke-test the tenant CP Integrations hub after flag changes.',
		),
		'tips' => array(
			'Never paste production DB passwords into tickets — use the hub + vault.',
		),
		'links' => array(
			array('label' => 'Tenant hub', 'url' => '/' . $backend . '/shop/tenant_hub/tenant_hub'),
			array('label' => 'Tenant features', 'url' => '/' . $backend . '/control/portal/epc_tenant_features'),
		),
	),
);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-intguide epc-inthub',
));
?>

<div class="epc-inthub-brand">
	<div>
		<div class="epc-inthub-brand__mark">Operator handbook</div>
		<h2>Integrations Guide</h2>
		<p>Step-by-step enable → configure → test for every module listed in the Integrations Hub. Dedicated guides are linked where they already exist.</p>
	</div>
	<div class="epc-inthub-brand__actions">
		<a class="btn btn-sm btn-primary" href="<?php echo epc_int_h($hubUrl); ?>"><i class="fa fa-plug"></i> Back to hub</a>
		<?php if ($isSuper) { ?>
		<a class="btn btn-sm btn-default" href="/<?php echo epc_int_h($backend); ?>/control/portal/epc_tenant_features"><i class="fa fa-sliders"></i> Tenant features</a>
		<?php } ?>
	</div>
</div>

<div class="epc-intguide-layout">
	<nav class="epc-intguide-toc" aria-label="Guide contents">
		<h4>Contents</h4>
		<?php foreach ($catalog as $key => $meta) {
			if (!$isSuper && !empty($meta['super_only_config']) && empty($meta['tenant_url'])) {
				continue;
			}
			?>
		<a href="#<?php echo epc_int_h($key); ?>"><?php echo epc_int_h($meta['label']); ?></a>
		<?php } ?>
	</nav>

	<div>
		<?php
		foreach ($categories as $catKey => $catMeta) {
			$keysInCat = array();
			foreach ($catalog as $key => $meta) {
				if (($meta['category'] ?? '') !== $catKey) {
					continue;
				}
				if (!$isSuper && !empty($meta['super_only_config']) && empty($meta['tenant_url'])) {
					continue;
				}
				$keysInCat[] = $key;
			}
			if (!$keysInCat) {
				continue;
			}
			foreach ($keysInCat as $key) {
				$meta = $catalog[$key];
				$sec = $sections[$key] ?? array(
					'summary' => (string) ($meta['blurb'] ?? ''),
					'steps' => array('Open Configure from the Integrations hub.', 'Save settings.', 'Run any built-in test action.'),
					'tips' => array(),
					'links' => array(),
				);
				$configUrl = $isSuper
					? (string) ($meta['super_url'] ?? '')
					: (string) ($meta['tenant_url'] ?? $meta['super_url'] ?? '');
				?>
		<section class="epc-intguide-section" id="<?php echo epc_int_h($key); ?>">
			<h3><i class="fa <?php echo epc_int_h($meta['icon'] ?? 'fa-plug'); ?>"></i><?php echo epc_int_h($meta['label']); ?></h3>
			<p class="lead"><?php echo epc_int_h($sec['summary']); ?></p>
			<strong>Activate</strong>
			<ol>
				<?php foreach ($sec['steps'] as $step) { ?>
				<li><?php echo epc_int_h($step); ?></li>
				<?php } ?>
			</ol>
			<?php if (!empty($sec['tips'])) { ?>
			<strong>Tips</strong>
			<ul>
				<?php foreach ($sec['tips'] as $tip) { ?>
				<li><?php echo epc_int_h($tip); ?></li>
				<?php } ?>
			</ul>
			<?php } ?>
			<div class="epc-intguide-actions">
				<?php if ($configUrl !== '' && (empty($meta['super_only_config']) || $isSuper)) { ?>
				<a class="btn btn-sm btn-primary" href="<?php echo epc_int_h($configUrl); ?>"><i class="fa fa-cog"></i> Configure</a>
				<?php } ?>
				<?php
				$extraGuide = epc_integrations_resolve_guide((string) ($meta['guide'] ?? ''), $key);
				if ($extraGuide !== '' && strpos($extraGuide, 'epc_integrations_guide') === false) {
					?>
				<a class="btn btn-sm btn-default" href="<?php echo epc_int_h($extraGuide); ?>"><i class="fa fa-book"></i> Dedicated guide</a>
				<?php } ?>
				<?php foreach ($sec['links'] as $link) {
					if (($link['url'] ?? '') === '' || ($link['url'] ?? '') === $configUrl) {
						continue;
					}
					?>
				<a class="btn btn-sm btn-default" href="<?php echo epc_int_h($link['url']); ?>"><?php echo epc_int_h($link['label']); ?></a>
				<?php } ?>
			</div>
		</section>
				<?php
			}
		}
		?>
	</div>
</div>

<?php epc_cp_page_frame_close(); ?>
