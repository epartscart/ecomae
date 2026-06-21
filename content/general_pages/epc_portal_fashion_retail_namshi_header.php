<?php
/**
 * Namshi-style fashion & beauty storefront header (utility bar, dept tabs, search, mega nav).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_fashion_retail_namshi_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_fashion_retail_namshi_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_logo.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$epc_frn_home_url = epc_fashion_retail_namshi_public_url();
$utility = epc_fashion_retail_namshi_utility_links();
$departments = epc_fashion_retail_namshi_departments();
$beautyTabs = epc_fashion_retail_namshi_beauty_tabs();
$megaNav = epc_fashion_retail_namshi_mega_nav();
$searchPlaceholder = 'Search fashion, beauty, brands…';
$searchValue = isset($value_for_input_search_string) ? $value_for_input_search_string : '';

function epc_frn_header_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<header class="epc-frn-header hidden-xs" id="epc_frn_header">
	<div class="epc-frn-utility">
		<div class="container">
			<nav class="epc-frn-utility__nav" aria-label="Utility">
				<?php foreach ($utility as $link) { ?>
				<a class="epc-frn-utility__link" href="<?php echo epc_frn_header_href($lang, $link['href']); ?>">
					<i class="fa <?php echo htmlspecialchars($link['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i>
					<?php echo htmlspecialchars($link['label'], ENT_QUOTES, 'UTF-8'); ?>
				</a>
				<?php } ?>
			</nav>
			<div class="epc-frn-utility__actions">
				<span class="epc-frn-utility__promo">UAE · Free delivery over AED 150</span>
				<div class="epc-frn-utility__lang">
					<?php require $_SERVER['DOCUMENT_ROOT'] . '/modules/lang/module.php'; ?>
				</div>
				<?php if (!empty($epc_currency_records)) { ?>
				<div class="epc-frn-utility__currency epc-currency-switcher">
					<select id="epc_currency_select" aria-label="Currency"<?php if (!empty($epc_currency_locked_for_user)) { ?> disabled="disabled"<?php } ?>>
						<?php foreach ($epc_currency_records as $epc_currency_iso => $epc_currency_row) { ?>
						<option value="<?php echo htmlspecialchars($epc_currency_iso, ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($epc_currency_iso === $epc_selected_currency_iso) ? ' selected' : ''; ?>>
							<?php echo htmlspecialchars($epc_currency_row['caption_short'], ENT_QUOTES, 'UTF-8'); ?>
						</option>
						<?php } ?>
					</select>
				</div>
				<?php } ?>
				<a class="epc-frn-utility__link" href="<?php echo epc_frn_header_href($lang, '/shop/zakladki'); ?>">
					<i class="fa fa-heart-o" aria-hidden="true"></i> Wishlist
				</a>
				<?php if ((int) DP_User::getUserId() > 0) { ?>
				<a class="epc-frn-utility__link" href="<?php echo epc_frn_header_href($lang, '/shop/orders'); ?>">
					<i class="fa fa-user" aria-hidden="true"></i> Account
				</a>
				<?php } else {
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_auth_links.php';
					echo epc_storefront_auth_links_html($multilang_params, 'epc-frn-utility__link epc-auth-header-links');
					echo epc_storefront_auth_links_styles();
				} ?>
			</div>
		</div>
	</div>

	<div class="epc-frn-logo-row">
		<div class="container">
			<div class="epc-frn-logo-row__grid">
				<a class="epc-frn-logo" href="<?php echo htmlspecialchars($epc_frn_home_url, ENT_QUOTES, 'UTF-8'); ?>" aria-label="Home">
					<?php echo epc_portal_storefront_logo_markup(); ?>
				</a>
				<div class="epc-frn-search">
					<form action="<?php echo epc_frn_header_href($lang, '/shop/search'); ?>" method="GET" class="epc-frn-search__form" role="search">
						<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
						<input type="search" class="epc-frn-search__input form-control" name="search_string" value="<?php echo htmlspecialchars($searchValue, ENT_QUOTES, 'UTF-8'); ?>" placeholder="<?php echo htmlspecialchars($searchPlaceholder, ENT_QUOTES, 'UTF-8'); ?>" autocomplete="off" />
						<button class="epc-frn-search__btn btn btn-ar btn-primary" type="submit" aria-label="Search">
							<i class="fa fa-search" aria-hidden="true"></i>
						</button>
					</form>
				</div>
				<div class="epc-frn-logo-row__icons">
					<a class="epc-frn-icon-btn" href="<?php echo epc_frn_header_href($lang, '/shop/orders'); ?>" title="My orders">
						<i class="fa fa-list-alt" aria-hidden="true"></i>
					</a>
					<a class="epc-frn-icon-btn epc-frn-icon-btn--cart" href="<?php echo epc_frn_header_href($lang, '/shop/cart'); ?>" title="Bag">
						<i class="fa fa-shopping-bag" aria-hidden="true"></i>
						<span class="epc-frn-icon-btn__count" id="header_cart_items_count"></span>
					</a>
				</div>
			</div>
		</div>
	</div>

	<nav class="epc-frn-dept" aria-label="Departments">
		<div class="container">
			<ul class="epc-frn-dept__list">
				<?php foreach ($departments as $dept) {
					$cls = !empty($dept['active']) ? ' is-active' : '';
				?>
				<li class="epc-frn-dept__item<?php echo $cls; ?>">
					<a href="<?php echo epc_frn_header_href($lang, $dept['href']); ?>"><?php echo htmlspecialchars($dept['label'], ENT_QUOTES, 'UTF-8'); ?></a>
				</li>
				<?php } ?>
			</ul>
		</div>
	</nav>

	<nav class="epc-frn-beauty-tabs" aria-label="Beauty">
		<div class="container">
			<ul class="epc-frn-beauty-tabs__list">
				<?php foreach ($beautyTabs as $tab) {
					$cls = !empty($tab['active']) ? ' is-active' : '';
				?>
				<li class="epc-frn-beauty-tabs__item<?php echo $cls; ?>">
					<a href="<?php echo epc_frn_header_href($lang, $tab['href']); ?>"><?php echo htmlspecialchars($tab['label'], ENT_QUOTES, 'UTF-8'); ?></a>
				</li>
				<?php } ?>
			</ul>
		</div>
	</nav>

	<nav class="epc-frn-mega" aria-label="Main">
		<div class="container">
			<button type="button" class="epc-frn-mega__all" onclick="if(typeof showCatalogMenu==='function'){showCatalogMenu();}">
				<i class="fa fa-bars" aria-hidden="true"></i> All
			</button>
			<ul class="epc-frn-mega__list">
				<?php foreach ($megaNav as $item) {
					$cls = !empty($item['highlight']) ? ' epc-frn-mega__item--deal' : '';
				?>
				<li class="epc-frn-mega__item<?php echo $cls; ?>">
					<a href="<?php echo epc_frn_header_href($lang, $item['href']); ?>"><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?></a>
				</li>
				<?php } ?>
			</ul>
		</div>
		<div id="dp_menu" class="epc-frn-mega__panel epc-frn-mega__panel--fashion" style="display:none;">
			<div class="container">
				<?php require __DIR__ . '/epc_fashion_retail_namshi_mega_menu.php'; ?>
			</div>
		</div>
	</nav>
</header>

<header class="epc-frn-header-mobile hidden-sm hidden-md hidden-lg" id="epc_frn_header_mobile">
	<div class="epc-frn-mobile-bar">
		<button type="button" class="epc-frn-mobile-bar__toggle navbar-toggle" data-toggle="collapse" data-target="#epc_frn_mobile_nav" aria-label="Menu">
			<i class="fa fa-bars"></i>
		</button>
		<a class="epc-frn-mobile-bar__logo" href="<?php echo htmlspecialchars($epc_frn_home_url, ENT_QUOTES, 'UTF-8'); ?>">
			<?php echo epc_portal_storefront_logo_markup(); ?>
		</a>
		<a class="epc-frn-mobile-bar__cart" href="<?php echo epc_frn_header_href($lang, '/shop/cart'); ?>">
			<i class="fa fa-shopping-bag"></i>
			<span id="header_cart_items_count_mobile"></span>
		</a>
	</div>
	<div class="epc-frn-mobile-dept">
		<div class="epc-frn-mobile-dept__scroll">
			<?php foreach ($departments as $dept) {
				$cls = !empty($dept['active']) ? ' is-active' : '';
			?>
			<a class="epc-frn-mobile-dept__chip<?php echo $cls; ?>" href="<?php echo epc_frn_header_href($lang, $dept['href']); ?>"><?php echo htmlspecialchars($dept['label'], ENT_QUOTES, 'UTF-8'); ?></a>
			<?php } ?>
		</div>
	</div>
	<div class="epc-frn-mobile-search container">
		<form action="<?php echo epc_frn_header_href($lang, '/shop/search'); ?>" method="GET">
			<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
			<div class="input-group">
				<input type="search" class="form-control" name="search_string" placeholder="<?php echo htmlspecialchars($searchPlaceholder, ENT_QUOTES, 'UTF-8'); ?>" />
				<span class="input-group-btn">
					<button class="btn btn-ar btn-primary" type="submit"><i class="fa fa-search"></i></button>
				</span>
			</div>
		</form>
	</div>
	<div class="collapse" id="epc_frn_mobile_nav">
		<ul class="epc-frn-mobile-nav">
			<?php foreach ($beautyTabs as $tab) { ?>
			<li><a href="<?php echo epc_frn_header_href($lang, $tab['href']); ?>"><?php echo htmlspecialchars($tab['label'], ENT_QUOTES, 'UTF-8'); ?></a></li>
			<?php } ?>
			<?php foreach ($megaNav as $item) { ?>
			<li><a href="<?php echo epc_frn_header_href($lang, $item['href']); ?>"><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?></a></li>
			<?php } ?>
			<li><a href="<?php echo epc_frn_header_href($lang, '/kontakty'); ?>">Help</a></li>
		</ul>
	</div>
</header>
