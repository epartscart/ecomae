<?php
/**
 * Prime Invest header — top bar, sticky main nav, Taxofinca animated logo.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_logo.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_brand.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$nav = epc_cpi_nav_links();
$contact = epc_cpi_header_contact();
$homeUrl = rtrim((string) $DP_Config->domain_path, '/');
$erpHref = function_exists('epc_portal_erp_url')
	? epc_portal_erp_url((string) $lang)
	: ($lang . '/shop/erp');

function epc_cpi_header_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<div class="epc-cpi-header-top hidden-xs">
	<div class="container">
		<div class="epc-cpi-header-top__meta">
			<span><i class="fa fa-phone" aria-hidden="true"></i> <?php echo htmlspecialchars($contact['phone'], ENT_QUOTES, 'UTF-8'); ?></span>
			<span><i class="fa fa-envelope-o" aria-hidden="true"></i> <?php echo htmlspecialchars($contact['email'], ENT_QUOTES, 'UTF-8'); ?></span>
			<span><i class="fa fa-clock-o" aria-hidden="true"></i> <?php echo htmlspecialchars($contact['hours'], ENT_QUOTES, 'UTF-8'); ?></span>
		</div>
		<div class="epc-cpi-header-top__actions">
			<a class="epc-cpi-header-top__link" href="<?php echo epc_cpi_header_href($lang, '/erp'); ?>"><i class="fa fa-line-chart"></i> Client ERP</a>
			<a class="epc-cpi-header-top__link" href="<?php echo epc_cpi_header_href($lang, '/cp/'); ?>"><i class="fa fa-th-large"></i> Staff CP</a>
			<div class="epc-cpi-header-top__lang">
				<?php require $_SERVER['DOCUMENT_ROOT'] . '/modules/lang/module.php'; ?>
			</div>
		</div>
	</div>
</div>

<div class="epc-cpi-header-wrap" id="epc_cpi_header_wrap">
	<header class="epc-cpi-header hidden-xs" id="epc_cpi_header">
		<div class="container">
			<a class="epc-cpi-logo" href="<?php echo htmlspecialchars($homeUrl, ENT_QUOTES, 'UTF-8'); ?>" aria-label="Home">
				<?php echo epc_portal_storefront_logo_markup(); ?>
			</a>
			<nav class="epc-cpi-nav" aria-label="Main navigation">
				<?php foreach ($nav as $item) { ?>
				<a class="epc-cpi-nav__link" href="<?php echo epc_cpi_header_href($lang, $item['href']); ?>">
					<?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?>
				</a>
				<?php } ?>
			</nav>
			<div class="epc-cpi-header__cta">
				<a class="epc-cpi-btn epc-cpi-btn--outline-dark epc-cpi-btn--sm" href="<?php echo epc_cpi_header_href($lang, '/kontakty'); ?>">Contact</a>
				<a class="epc-cpi-btn epc-cpi-btn--primary epc-cpi-btn--sm" href="<?php echo htmlspecialchars($erpHref, ENT_QUOTES, 'UTF-8'); ?>">
					Client portal
				</a>
			</div>
		</div>
	</header>
</div>

<header class="epc-cpi-header-mobile hidden-sm hidden-md hidden-lg" id="epc_cpi_header_mobile">
	<div class="container">
		<a class="epc-cpi-logo" href="<?php echo htmlspecialchars($homeUrl, ENT_QUOTES, 'UTF-8'); ?>">
			<?php
			if (function_exists('epc_portal_tenant_brand_enabled') && epc_portal_tenant_brand_enabled()) {
				echo epc_portal_tenant_brand_markup('compact');
			} else {
				echo epc_portal_storefront_logo_markup();
			}
			?>
		</a>
		<button type="button" class="epc-cpi-header-mobile__toggle" id="epc_cpi_mobile_toggle" aria-expanded="false" aria-controls="epc_cpi_mobile_panel">
			<i class="fa fa-bars" aria-hidden="true"></i>
		</button>
	</div>
	<div class="epc-cpi-header-mobile__panel" id="epc_cpi_mobile_panel">
		<div class="container">
			<?php foreach ($nav as $item) { ?>
			<a href="<?php echo epc_cpi_header_href($lang, $item['href']); ?>"><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?></a>
			<?php } ?>
			<a href="<?php echo htmlspecialchars($erpHref, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-line-chart"></i> Client portal</a>
			<a href="<?php echo epc_cpi_header_href($lang, '/kontakty'); ?>">Contact</a>
		</div>
	</div>
</header>
<script>
(function () {
	var wrap = document.getElementById('epc_cpi_header_wrap');
	if (wrap) {
		var onScroll = function () {
			wrap.classList.toggle('is-scrolled', window.scrollY > 12);
		};
		onScroll();
		window.addEventListener('scroll', onScroll, { passive: true });
	}
	var btn = document.getElementById('epc_cpi_mobile_toggle');
	var panel = document.getElementById('epc_cpi_mobile_panel');
	if (btn && panel) {
		btn.addEventListener('click', function () {
			var open = panel.classList.toggle('is-open');
			btn.setAttribute('aria-expanded', open ? 'true' : 'false');
		});
	}
})();
</script>
