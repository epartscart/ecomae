<?php
/**
 * Industry-specific landing blocks for non auto-parts sites.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_brand.php';

$site = epc_portal_site_profile();
$industry = epc_portal_industry();
$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$system = htmlspecialchars(isset($site['system_name']) ? $site['system_name'] : 'Portal', ENT_QUOTES, 'UTF-8');
$tagline = htmlspecialchars(isset($site['tagline']) ? $site['tagline'] : '', ENT_QUOTES, 'UTF-8');
$cp_url = '/' . $DP_Config->backend_dir;
$icon = isset($industry['icon']) ? $industry['icon'] : 'fa-briefcase';
$industry_name = htmlspecialchars(isset($industry['name']) ? $industry['name'] : 'Business', ENT_QUOTES, 'UTF-8');

$erpHref = function_exists('epc_portal_erp_url')
	? epc_portal_erp_url((string) $lang)
	: ($lang . '/shop/erp');
$contactHref = $lang . '/kontakty';

$services = array();
if ($site['industry'] === 'tax_advisory') {
	$services = array(
		array('title' => 'Corporate tax & VAT', 'text' => 'Registration, filing, compliance reviews and advisory for UAE and international structures.'),
		array('title' => 'Business advisory', 'text' => 'Entity setup, bookkeeping oversight, payroll coordination and management reporting.'),
		array('title' => 'E-invoicing & ERP', 'text' => 'Integrated invoicing, customer records and finance workflows on one portal.'),
	);
} else {
	$services = array(
		array('title' => 'Customer management', 'text' => 'Central CRM, orders and communications for your team.'),
		array('title' => 'Finance & ERP', 'text' => 'Invoices, payments, accounting and operational dashboards.'),
		array('title' => 'Multi-channel sales', 'text' => 'Catalogue, marketing tools and order fulfilment in one place.'),
	);
}
?>
<?php echo epc_portal_tenant_brand_hero_block(); ?>
<div class="epc-portal-hero col-lg-12">
	<div class="epc-portal-hero__shell">
		<div class="epc-portal-hero__badge"><i class="fa <?php echo $icon; ?>"></i> <?php echo $industry_name; ?></div>
		<h1 class="epc-portal-hero__title"><?php echo $system; ?></h1>
		<p class="epc-portal-hero__lead"><?php echo $tagline; ?></p>
		<div class="epc-portal-hero__actions">
			<?php if ($site['industry'] === 'tax_advisory') { ?>
			<a class="btn btn-primary btn-lg" href="<?php echo htmlspecialchars($erpHref, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-line-chart"></i> Client ERP portal</a>
			<a class="btn btn-default btn-lg" href="<?php echo htmlspecialchars($contactHref, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-envelope"></i> Contact us</a>
			<?php } ?>
			<a class="btn <?php echo $site['industry'] === 'tax_advisory' ? 'btn-default' : 'btn-primary'; ?> btn-lg" href="<?php echo htmlspecialchars($cp_url, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-th-large"></i> <?php echo $site['industry'] === 'tax_advisory' ? 'Staff control panel' : 'Open control panel'; ?></a>
			<a class="btn btn-default btn-lg" href="<?php echo htmlspecialchars($lang . '/users/login', ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-sign-in"></i> Client login</a>
		</div>
	</div>
	<div class="epc-portal-hero__grid">
		<?php foreach ($services as $svc) { ?>
		<div class="epc-portal-hero__card">
			<h3><?php echo htmlspecialchars($svc['title'], ENT_QUOTES, 'UTF-8'); ?></h3>
			<p><?php echo htmlspecialchars($svc['text'], ENT_QUOTES, 'UTF-8'); ?></p>
		</div>
		<?php } ?>
	</div>
</div>
<style>
.epc-portal-hero { margin: 0 0 24px; }
.epc-portal-hero__shell {
	background: linear-gradient(135deg, var(--epc-portal-hero-from, #042f2e), var(--epc-portal-hero-to, #115e59));
	border-radius: 22px;
	color: #fff;
	padding: 42px 36px;
}
.epc-portal-hero__badge {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	background: rgba(255,255,255,.12);
	border: 1px solid rgba(255,255,255,.16);
	border-radius: 999px;
	font-size: 12px;
	font-weight: 700;
	letter-spacing: .06em;
	margin-bottom: 16px;
	padding: 8px 14px;
	text-transform: uppercase;
}
.epc-portal-hero__title { color: #fff; font-size: 42px; font-weight: 800; margin: 0 0 12px; }
.epc-portal-hero__lead { color: rgba(255,255,255,.88); font-size: 18px; max-width: 760px; }
.epc-portal-hero__actions { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 24px; }
.epc-portal-hero__grid {
	display: grid;
	gap: 16px;
	grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
	margin-top: 22px;
}
.epc-portal-hero__card {
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 16px;
	box-shadow: 0 10px 30px rgba(15,23,42,.06);
	padding: 22px;
}
.epc-portal-hero__card h3 { font-size: 18px; font-weight: 700; margin: 0 0 8px; }
.epc-portal-hero__card p { color: #475569; margin: 0; }
</style>
