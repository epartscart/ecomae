<?php
/**
 * Virgin Megastore–style storefront header (utility bar, logo row, search, mega nav).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_logo.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$epc_er_home_url = epc_electronics_retail_public_url();
$utility = epc_electronics_retail_utility_links();
global $db_link;
$megaNav = ($db_link instanceof PDO)
	? epc_electronicae_mega_nav($db_link)
	: epc_electronics_retail_mega_nav();
if (!$megaNav) {
	$megaNav = epc_electronics_retail_mega_nav();
}
$searchPlaceholder = 'Search for products, brands and more…';
$searchValue = isset($value_for_input_search_string) ? $value_for_input_search_string : '';

function epc_er_header_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<header class="epc-er-header hidden-xs" id="epc_er_header">
	<div class="epc-er-utility">
		<div class="container">
			<nav class="epc-er-utility__nav" aria-label="Utility">
				<?php foreach ($utility as $link) { ?>
				<a class="epc-er-utility__link" href="<?php echo epc_er_header_href($lang, $link['href']); ?>">
					<i class="fa <?php echo htmlspecialchars($link['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i>
					<?php echo htmlspecialchars($link['label'], ENT_QUOTES, 'UTF-8'); ?>
				</a>
				<?php } ?>
			</nav>
			<div class="epc-er-utility__actions">
				<div class="epc-er-utility__lang">
					<?php require $_SERVER['DOCUMENT_ROOT'] . '/modules/lang/module.php'; ?>
				</div>
				<?php if (!empty($epc_currency_records)) { ?>
				<div class="epc-er-utility__currency epc-currency-switcher">
					<select id="epc_currency_select" aria-label="Currency"<?php if (!empty($epc_currency_locked_for_user)) { ?> disabled="disabled"<?php } ?>>
						<?php foreach ($epc_currency_records as $epc_currency_iso => $epc_currency_row) { ?>
						<option value="<?php echo htmlspecialchars($epc_currency_iso, ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($epc_currency_iso === $epc_selected_currency_iso) ? ' selected' : ''; ?>>
							<?php echo htmlspecialchars($epc_currency_row['caption_short'], ENT_QUOTES, 'UTF-8'); ?>
						</option>
						<?php } ?>
					</select>
				</div>
				<?php } ?>
				<a class="epc-er-utility__link" href="<?php echo epc_er_header_href($lang, '/shop/zakladki'); ?>">
					<i class="fa fa-heart-o" aria-hidden="true"></i> Wishlist
				</a>
				<?php if ((int) DP_User::getUserId() > 0) { ?>
				<a class="epc-er-utility__link" href="<?php echo epc_er_header_href($lang, '/shop/orders'); ?>">
					<i class="fa fa-user" aria-hidden="true"></i> My Account
				</a>
				<?php } else {
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_auth_links.php';
					echo epc_storefront_auth_links_html($multilang_params, 'epc-er-utility__link epc-auth-header-links');
					echo epc_storefront_auth_links_styles();
				} ?>
			</div>
		</div>
	</div>

	<div class="epc-er-logo-row">
		<div class="container">
			<div class="epc-er-logo-row__grid">
				<a class="epc-er-logo" href="<?php echo htmlspecialchars($epc_er_home_url, ENT_QUOTES, 'UTF-8'); ?>" aria-label="Home">
					<?php echo epc_portal_storefront_logo_markup(); ?>
				</a>
				<div class="epc-er-search">
					<form action="<?php echo epc_er_header_href($lang, '/shop/search'); ?>" method="GET" class="epc-er-search__form" role="search">
						<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
						<input type="search" class="epc-er-search__input form-control" name="search_string" value="<?php echo htmlspecialchars($searchValue, ENT_QUOTES, 'UTF-8'); ?>" placeholder="<?php echo htmlspecialchars($searchPlaceholder, ENT_QUOTES, 'UTF-8'); ?>" autocomplete="off" />
						<button class="epc-er-search__btn btn btn-ar btn-primary" type="submit" aria-label="Search">
							<i class="fa fa-search" aria-hidden="true"></i>
						</button>
					</form>
				</div>
				<div class="epc-er-logo-row__icons">
					<a class="epc-er-icon-btn" href="<?php echo epc_er_header_href($lang, '/shop/orders'); ?>" title="My orders">
						<i class="fa fa-list-alt" aria-hidden="true"></i>
					</a>
					<a class="epc-er-icon-btn epc-er-icon-btn--cart" href="<?php echo epc_er_header_href($lang, '/shop/cart'); ?>" title="Cart">
						<i class="fa fa-shopping-cart" aria-hidden="true"></i>
						<span class="epc-er-icon-btn__count" id="header_cart_items_count"></span>
					</a>
				</div>
			</div>
		</div>
	</div>

	<nav class="epc-er-mega" aria-label="Main">
		<div class="container">
			<button type="button" class="epc-er-mega__all" onclick="if(typeof showCatalogMenu==='function'){showCatalogMenu();}">
				<i class="fa fa-bars" aria-hidden="true"></i> All Categories
			</button>
			<ul class="epc-er-mega__list">
				<?php foreach ($megaNav as $item) {
					$cls = !empty($item['highlight']) ? ' epc-er-mega__item--deal' : '';
				?>
				<li class="epc-er-mega__item<?php echo $cls; ?>">
					<a href="<?php echo epc_er_header_href($lang, $item['href']); ?>"><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?><?php if (!empty($item['count'])) { ?><span class="epc-er-mega__item-count"><?php echo (int) $item['count']; ?></span><?php } ?></a>
				</li>
				<?php } ?>
			</ul>
		</div>
		<div id="dp_menu" class="epc-er-mega__panel" style="display:none;">
			<div class="container">
				<div class="vertical-tabs-right">
					<?php include $_SERVER['DOCUMENT_ROOT'] . '/modules/shop/catalogue/dp_menu.php'; ?>
				</div>
			</div>
		</div>
	</nav>
</header>

<header class="epc-er-header-mobile hidden-sm hidden-md hidden-lg" id="epc_er_header_mobile">
	<div class="epc-er-mobile-bar">
		<button type="button" class="epc-er-mobile-bar__toggle navbar-toggle" data-toggle="collapse" data-target="#epc_er_mobile_nav" aria-label="Menu">
			<i class="fa fa-bars"></i>
		</button>
		<a class="epc-er-mobile-bar__logo" href="<?php echo htmlspecialchars($epc_er_home_url, ENT_QUOTES, 'UTF-8'); ?>">
			<?php echo epc_portal_storefront_logo_markup(); ?>
		</a>
		<a class="epc-er-mobile-bar__cart" href="<?php echo epc_er_header_href($lang, '/shop/cart'); ?>">
			<i class="fa fa-shopping-cart"></i>
			<span id="header_cart_items_count_mobile"></span>
		</a>
	</div>
	<div class="epc-er-mobile-search container">
		<form action="<?php echo epc_er_header_href($lang, '/shop/search'); ?>" method="GET">
			<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
			<div class="input-group">
				<input type="search" class="form-control" name="search_string" placeholder="<?php echo htmlspecialchars($searchPlaceholder, ENT_QUOTES, 'UTF-8'); ?>" />
				<span class="input-group-btn">
					<button class="btn btn-ar btn-primary" type="submit"><i class="fa fa-search"></i></button>
				</span>
			</div>
		</form>
	</div>
	<div class="collapse" id="epc_er_mobile_nav">
		<ul class="epc-er-mobile-nav">
			<?php foreach ($megaNav as $item) { ?>
			<li><a href="<?php echo epc_er_header_href($lang, $item['href']); ?>"><?php echo htmlspecialchars($item['label'], ENT_QUOTES, 'UTF-8'); ?></a></li>
			<?php } ?>
			<li><a href="<?php echo epc_er_header_href($lang, '/kontakty'); ?>">Help &amp; Contact</a></li>
		</ul>
	</div>
</header>
