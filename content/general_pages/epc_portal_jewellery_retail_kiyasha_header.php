<?php
/**
 * Kiyasha-style jewellery luxury storefront header (utility bar, dept tabs, search, mega nav).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_logo.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$epc_jrk_home_url = epc_jewellery_retail_kiyasha_public_url();
$utility = epc_jewellery_retail_kiyasha_utility_links();
$departments = epc_jewellery_retail_kiyasha_departments();
$collectionTabs = epc_jewellery_retail_kiyasha_collection_tabs();
$megaNav = epc_jewellery_retail_kiyasha_mega_nav();
$searchPlaceholder = 'Search rings, gold, diamonds, brands…';
$searchValue = isset($value_for_input_search_string) ? $value_for_input_search_string : '';

function epc_jrk_header_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<header class="epc-jrk-header hidden-xs" id="epc_jrk_header">
	<div class="epc-jrk-utility">
		<div class="container">
			<nav class="epc-jrk-utility__nav" aria-label="Utility">
				<?php foreach ($utility as $link) { ?>
				<a class="epc-jrk-utility__link" href="<?php echo epc_jrk_header_href($lang, $link['href']); ?>">
					<i class="fa <?php echo htmlspecialchars($link['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i>
					<?php echo htmlspecialchars($link['label'], ENT_QUOTES, 'UTF-8'); ?>
				</a>
				<?php } ?>
			</nav>
			<div class="epc-jrk-utility__actions">
				<span class="epc-jrk-utility__promo">UAE Â· Free insured delivery over AED 500</span>
				<div class="epc-jrk-utility__lang">
					<?php require $_SERVER['DOCUMENT_ROOT'] . '/modules/lang/module.php'; ?>
				</div>
				<?php if (!empty($epc_currency_records)) { ?>
				<div class="epc-jrk-utility__currency epc-currency-switcher">
					<select id="epc_currency_select" aria-label="Currency"<?php if (!empty($epc_currency_locked_for_user)) { ?> disabled="disabled"<?php } ?>>
						<?php foreach ($epc_currency_records as $epc_currency_iso => $epc_currency_row) { ?>
						<option value="<?php echo htmlspecialchars($epc_currency_iso, ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($epc_currency_iso === $epc_selected_currency_iso) ? ' selected' : ''; ?>>
							<?php echo htmlspecialchars($epc_currency_row['caption_short'], ENT_QUOTES, 'UTF-8'); ?>
						</option>
						<?php } ?>
					</select>
				</div>
				<?php } ?>
				<a class="epc-jrk-utility__link" href="<?php echo epc_jrk_header_href($lang, '/shop/zakladki'); ?>">
					<i class="fa fa-heart-o" aria-hidden="true"></i> Wishlist
				</a>
				<?php if ((int) DP_User::getUserId() > 0) { ?>
				<a class="epc-jrk-utility__link" href="<?php echo epc_jrk_header_href($lang, '/shop/orders'); ?>">
					<i class="fa fa-user" aria-hidden="true"></i> Account
				</a>
				<?php } else {
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_auth_links.php';
					echo epc_storefront_auth_links_html($multilang_params, 'epc-jrk-utility__link epc-auth-header-links');
					echo epc_storefront_auth_links_styles();
				} ?>
			</div>
		</div>
	</div>

	<div class="epc-jrk-logo-row">
		<div class="container">
			<div class="epc-jrk-logo-row__grid">
				<a class="epc-jrk-logo" href="<?php echo htmlspecialchars($epc_jrk_home_url, ENT_QUOTES, 'UTF-8'); ?>" aria-label="Home">
					<?php echo epc_portal_storefront_logo_markup(); ?>
				</a>
				<div class="epc-jrk-search">
					<form action="<?php echo epc_jrk_header_href($lang, '/shop/search'); ?>" method="GET" class="epc-jrk-search__form" role="search">
						<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
						<input type="search" class="epc-jrk-search__input form-control" name="search_string" value="<?php echo htmlspecialchars($searchValue, ENT_QUOTES, 'UTF-8'); ?>" placeholder="<?php echo htmlspecialchars($searchPlaceholder, ENT_QUOTES, 'UTF-8'); ?>" autocomplete="off" />
						<button class="epc-jrk-search__btn btn btn-ar btn-primary" type="submit" aria-label="Search">
							<i class="fa fa-search" aria-hidden="true"></i>
						</button>
					</form>
				</div>
				<div class="epc-jrk-logo-row__icons">
					<a class="epc-jrk-icon-btn" href="<?php echo epc_jrk_header_href($lang, '/shop/orders'); ?>" title="My orders">
						<i class="fa fa-list-alt" aria-hidden="true"></i>
					</a>
					<a class="epc-jrk-icon-btn epc-jrk-icon-btn--cart" href="<?php echo epc_jrk_header_href($lang, '/shop/cart'); ?>" title="Bag">
						<i class="fa fa-shopping-bag" aria-hidden="true"></i>
						<span class="epc-jrk-icon-btn__count" id="header_cart_items_count"></span>
					</a>
				</div>
			</div>
		</div>
	</div>

	<nav class="epc-jrk-dept" aria-label="Departments">
		<div class="container">
			<ul class="epc-jrk-dept__list">
				<?php foreach ($departments as $dept) {
					$cls = !empty($dept['active']) ? ' is-active' : '';
				?>
				<li class="epc-jrk-dept__item<?php echo $cls; ?>">
					<a href="<?php echo epc_jrk_header_href($lang, $dept['href']); ?>"><?php echo htmlspecialchars($dept['label'], ENT_QUOTES, 'UTF-8'); ?></a>
				</li>
				<?php } ?>
			</ul>
		</div>
	</nav>

	<nav class="epc-jrk-beauty-tabs" aria-label="Collections">
		<div class="container">
			<ul class="epc-jrk-beauty-tabs__list">
				<?php foreach ($collectionTabs as $tab) {
					$cls = !empty($tab['active']) ? ' is-active' : '';
				?>
				<li class="epc-jrk-beauty-tabs__item<?php echo $cls; ?>">
					<a href="<?php echo epc_jrk_header_href($lang, $tab['href']); ?>"><?php echo htmlspecialchars($tab['label'], ENT_QUOTES, 'UTF-8'); ?></a>
				</li>
				<?php } ?>
			</ul>
		</div>
	</nav>

	<nav class="epc-jrk-mega" aria-label="Main">
		<div class="container">
			<button type="button" class="epc-jrk-mega__all" onclick="if(typeof showCatalogMenu==='function'){showCatalogMenu();}">
				<i class="fa fa-bars" aria-hidden="true"></i> All
			</button>
			<ul class="epc-jrk-mega__list">
				<?php foreach ($megaNav as $item) {
					$cls = !empty($item['highlight']) ? ' epc-jrk-mega__item--deal' : '';
				?>
				<li class="epc-jrk-mega__item<?php echo $cls; ?>">
					<a href="<?php echo epc_jrk_header_href($lang, $item['href']); ?>"><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?></a>
				</li>
				<?php } ?>
			</ul>
		</div>
		<div id="dp_menu" class="epc-jrk-mega__panel" style="display:none;">
			<div class="container">
				<div class="vertical-tabs-right">
					<?php include $_SERVER['DOCUMENT_ROOT'] . '/modules/shop/catalogue/dp_menu.php'; ?>
				</div>
			</div>
		</div>
	</nav>
</header>

<header class="epc-jrk-header-mobile hidden-sm hidden-md hidden-lg" id="epc_jrk_header_mobile">
	<div class="epc-jrk-mobile-bar">
		<button type="button" class="epc-jrk-mobile-bar__toggle navbar-toggle" data-toggle="collapse" data-target="#epc_jrk_mobile_nav" aria-label="Menu">
			<i class="fa fa-bars"></i>
		</button>
		<a class="epc-jrk-mobile-bar__logo" href="<?php echo htmlspecialchars($epc_jrk_home_url, ENT_QUOTES, 'UTF-8'); ?>">
			<?php echo epc_portal_storefront_logo_markup(); ?>
		</a>
		<a class="epc-jrk-mobile-bar__cart" href="<?php echo epc_jrk_header_href($lang, '/shop/cart'); ?>">
			<i class="fa fa-shopping-bag"></i>
			<span id="header_cart_items_count_mobile"></span>
		</a>
	</div>
	<div class="epc-jrk-mobile-dept">
		<div class="epc-jrk-mobile-dept__scroll">
			<?php foreach ($departments as $dept) {
				$cls = !empty($dept['active']) ? ' is-active' : '';
			?>
			<a class="epc-jrk-mobile-dept__chip<?php echo $cls; ?>" href="<?php echo epc_jrk_header_href($lang, $dept['href']); ?>"><?php echo htmlspecialchars($dept['label'], ENT_QUOTES, 'UTF-8'); ?></a>
			<?php } ?>
		</div>
	</div>
	<div class="epc-jrk-mobile-search container">
		<form action="<?php echo epc_jrk_header_href($lang, '/shop/search'); ?>" method="GET">
			<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
			<div class="input-group">
				<input type="search" class="form-control" name="search_string" placeholder="<?php echo htmlspecialchars($searchPlaceholder, ENT_QUOTES, 'UTF-8'); ?>" />
				<span class="input-group-btn">
					<button class="btn btn-ar btn-primary" type="submit"><i class="fa fa-search"></i></button>
				</span>
			</div>
		</form>
	</div>
	<div class="collapse" id="epc_jrk_mobile_nav">
		<ul class="epc-jrk-mobile-nav">
			<?php foreach ($collectionTabs as $tab) { ?>
			<li><a href="<?php echo epc_jrk_header_href($lang, $tab['href']); ?>"><?php echo htmlspecialchars($tab['label'], ENT_QUOTES, 'UTF-8'); ?></a></li>
			<?php } ?>
			<?php foreach ($megaNav as $item) { ?>
			<li><a href="<?php echo epc_jrk_header_href($lang, $item['href']); ?>"><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?></a></li>
			<?php } ?>
			<li><a href="<?php echo epc_jrk_header_href($lang, '/kontakty'); ?>">Help</a></li>
		</ul>
	</div>
</header>

