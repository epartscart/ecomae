<?php
/**
 * Virgin Megastore–style electronics retail homepage.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$storeName = epc_electronics_retail_store_name();
$trustBadges = epc_electronics_retail_trust_badges();

global $db_link;
$epc_el_pdo = ($db_link instanceof PDO) ? $db_link : null;
$epc_el_site_key = $epc_el_pdo ? epc_electronicae_site_key($epc_el_pdo) : 'electronicae';
$epc_el_use_catalogue = ($epc_el_pdo instanceof PDO);

$promos = epc_electronics_retail_promo_strip();
if ($epc_el_use_catalogue) {
	$lineTiles = epc_electronicae_product_line_tiles($epc_el_pdo, $epc_el_site_key, 12);
	$heroes = epc_electronicae_hero_slides($epc_el_pdo, $epc_el_site_key);
	$dealSections = epc_electronicae_home_product_sections($epc_el_pdo, $epc_el_site_key, 3, 6);
	$categories = array();
	foreach (array_slice($lineTiles, 0, 9) as $tile) {
		$categories[] = array(
			'name' => (string) ($tile['name'] ?? ''),
			'href' => (string) ($tile['href'] ?? '/'),
			'image' => (string) ($tile['image'] ?? ''),
			'alt' => (string) ($tile['name'] ?? ''),
			'icon' => (string) ($tile['icon'] ?? 'fa-microchip'),
		);
	}
	$featured = array();
	foreach (array_slice($lineTiles, 0, 8) as $tile) {
		$featured[] = array(
			'name' => (string) ($tile['name'] ?? ''),
			'href' => (string) ($tile['href'] ?? '/'),
			'image' => (string) ($tile['image'] ?? ''),
			'alt' => (string) ($tile['name'] ?? ''),
		);
	}
	$brands = array();
	foreach ($dealSections as $sec) {
		foreach ((array) ($sec['products'] ?? array()) as $p) {
			$b = trim((string) ($p['brand'] ?? ''));
			if ($b !== '' && !in_array($b, $brands, true)) {
				$brands[] = $b;
			}
		}
	}
	if (!$brands) {
		$brands = epc_electronics_retail_brands();
	}
	if ($lineTiles) {
		$first = $lineTiles[0];
		array_unshift($promos, array(
			'label' => 'Browse ' . (string) ($first['name'] ?? 'tech'),
			'href' => (string) ($first['href'] ?? '/'),
		));
	}
} else {
	$heroes = epc_electronics_retail_hero_slides();
	$categories = epc_electronics_retail_category_tiles();
	$dealSections = epc_electronics_retail_deal_sections();
	$brands = epc_electronics_retail_brands();
	$featured = epc_electronics_retail_featured_categories();
}

function epc_er_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<div class="epc-er-home col-lg-12">
	<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_virgin_hero_banner.php'; ?>
	<div class="epc-er-promo-strip epc-er-reveal">
		<div class="container">
			<div class="epc-er-promo-strip__track">
				<?php foreach ($promos as $promo) { ?>
				<a class="epc-er-promo-strip__item" href="<?php echo epc_er_href($lang, $promo['href']); ?>">
					<?php echo htmlspecialchars($promo['label'], ENT_QUOTES, 'UTF-8'); ?> <span aria-hidden="true">→</span>
				</a>
				<?php } ?>
			</div>
		</div>
	</div>

	<div class="epc-er-hero epc-er-reveal">
		<div class="container">
			<div class="epc-er-hero__carousel" id="epc_er_hero_carousel">
				<div class="epc-er-hero__slides">
					<?php foreach ($heroes as $i => $slide) { ?>
					<div class="epc-er-hero__slide<?php echo $i === 0 ? ' is-active' : ''; ?><?php echo $slide['tone'] === 'light' ? ' epc-er-hero__slide--light' : ''; ?><?php echo empty($slide['image']) ? ' epc-el-hero-icon-slide' : ''; ?>" data-index="<?php echo (int) $i; ?>">
						<div class="epc-er-hero__copy">
							<h1><?php echo htmlspecialchars($slide['title'], ENT_QUOTES, 'UTF-8'); ?></h1>
							<p><?php echo htmlspecialchars($slide['sub'], ENT_QUOTES, 'UTF-8'); ?></p>
							<a class="epc-er-btn epc-er-btn--primary" href="<?php echo epc_er_href($lang, $slide['href']); ?>">
								<?php echo htmlspecialchars($slide['cta'], ENT_QUOTES, 'UTF-8'); ?>
							</a>
						</div>
						<div class="epc-er-hero__media">
							<div class="epc-er-hero__media-frame">
								<?php if (!empty($slide['image'])) { ?>
								<img class="epc-er-hero__photo" src="<?php echo htmlspecialchars($slide['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($slide['alt'] ?? $slide['title'], ENT_QUOTES, 'UTF-8'); ?>" loading="<?php echo $i === 0 ? 'eager' : 'lazy'; ?>" decoding="async" width="720" height="480"/>
								<?php } else { ?>
								<span class="epc-el-hero-fallback" aria-hidden="true"><i class="fa <?php echo htmlspecialchars($slide['icon'] ?? 'fa-microchip', ENT_QUOTES, 'UTF-8'); ?>"></i></span>
								<?php } ?>
							</div>
						</div>
					</div>
					<?php } ?>
				</div>
				<div class="epc-er-hero__dots" role="tablist" aria-label="Hero banners">
					<?php foreach ($heroes as $i => $slide) { ?>
					<button type="button" class="epc-er-hero__dot<?php echo $i === 0 ? ' is-active' : ''; ?>" data-goto="<?php echo (int) $i; ?>" aria-label="Slide <?php echo (int) ($i + 1); ?>"></button>
					<?php } ?>
				</div>
			</div>
		</div>
	</div>

	<section class="epc-er-trust epc-er-reveal" aria-label="Shopping guarantees">
		<div class="container">
			<div class="epc-er-trust__grid">
				<?php foreach ($trustBadges as $badge) { ?>
				<div class="epc-er-trust__item">
					<i class="fa <?php echo htmlspecialchars($badge['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i>
					<div>
						<strong><?php echo htmlspecialchars($badge['title'], ENT_QUOTES, 'UTF-8'); ?></strong>
						<span><?php echo htmlspecialchars($badge['text'], ENT_QUOTES, 'UTF-8'); ?></span>
					</div>
				</div>
				<?php } ?>
			</div>
		</div>
	</section>

	<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_home_product_lines.php'; ?>

	<?php foreach ($dealSections as $section) { ?>
	<section class="epc-er-section epc-er-section--deals epc-er-reveal">
		<div class="container">
			<div class="epc-er-section__head">
				<h2 class="epc-er-section__title"><?php echo htmlspecialchars($section['title'], ENT_QUOTES, 'UTF-8'); ?></h2>
				<a class="epc-er-section__link" href="<?php echo epc_er_href($lang, !empty($section['href']) ? $section['href'] : '/smartphones'); ?>">View all</a>
			</div>
			<div class="epc-er-product-row epc-stagger">
				<?php foreach ($section['products'] as $product) {
					if (empty($product['image']) && !empty($product['key'])) {
						$product = epc_electronics_retail_resolve_product_images($product);
					}
					$price = epc_electronics_retail_format_aed($product['price']);
					$was = epc_electronics_retail_format_aed($product['was'] ?? 0);
					$badge = epc_electronics_retail_product_badge($product);
					$imgAlt = isset($product['alt']) ? $product['alt'] : $product['name'];
					$productHref = !empty($product['href']) ? $product['href'] : '/smartphones';
				?>
				<article class="epc-er-product-card epc-card-lift">
					<a class="epc-er-product-card__link" href="<?php echo epc_er_href($lang, $productHref); ?>">
						<div class="epc-er-product-card__img epc-img-zoom">
							<?php if ($badge !== '') { ?>
							<span class="epc-er-badge epc-er-badge--<?php echo htmlspecialchars($badge, ENT_QUOTES, 'UTF-8'); ?>"><?php echo $badge === 'sale' ? 'Sale' : 'New'; ?></span>
							<?php } ?>
							<img src="<?php echo htmlspecialchars($product['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($imgAlt, ENT_QUOTES, 'UTF-8'); ?>" loading="lazy" decoding="async" width="240" height="240"/>
						</div>
						<?php if (!empty($product['brand'])) { ?><div class="epc-er-product-card__brand"><?php echo htmlspecialchars($product['brand'], ENT_QUOTES, 'UTF-8'); ?></div><?php } ?>
						<?php if (!empty($product['sku'])) { ?><div class="epc-er-product-card__sku">SKU <?php echo htmlspecialchars($product['sku'], ENT_QUOTES, 'UTF-8'); ?></div><?php } ?>
						<h3 class="epc-er-product-card__name"><?php echo htmlspecialchars($product['name'], ENT_QUOTES, 'UTF-8'); ?></h3>
						<div class="epc-er-product-card__price">
							<strong><?php echo htmlspecialchars($price, ENT_QUOTES, 'UTF-8'); ?></strong>
							<?php if ($was !== '') { ?><s><?php echo htmlspecialchars($was, ENT_QUOTES, 'UTF-8'); ?></s><?php } ?>
						</div>
					</a>
				</article>
				<?php } ?>
			</div>
		</div>
	</section>
	<?php } ?>

	<?php if (!$dealSections) { ?>
	<section class="epc-er-section epc-er-section--deals epc-er-reveal epc-el-demo-fallback">
		<div class="container">
			<p class="epc-er-section__lead">Featured products will appear here as new lines are imported.</p>
		</div>
	</section>
	<?php } ?>

	<section class="epc-er-section epc-er-section--brands epc-er-reveal">
		<div class="container">
			<h2 class="epc-er-section__title">Popular brands</h2>
			<div class="epc-er-brand-strip">
				<?php foreach ($brands as $brand) { ?>
				<a class="epc-er-brand-pill" href="<?php echo epc_er_href($lang, '/smartphones'); ?>">
					<?php echo htmlspecialchars($brand, ENT_QUOTES, 'UTF-8'); ?>
				</a>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-er-section epc-er-section--featured epc-er-reveal">
		<div class="container">
			<h2 class="epc-er-section__title">Featured departments</h2>
			<div class="epc-er-featured-grid">
				<?php foreach ($featured as $feat) { ?>
				<a class="epc-er-featured-card" href="<?php echo epc_er_href($lang, $feat['href']); ?>">
					<img src="<?php echo htmlspecialchars($feat['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($feat['alt'] ?? $feat['name'], ENT_QUOTES, 'UTF-8'); ?>" loading="lazy" decoding="async" width="320" height="200"/>
					<span class="epc-er-featured-card__label"><?php echo htmlspecialchars($feat['name'], ENT_QUOTES, 'UTF-8'); ?></span>
				</a>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-er-section epc-er-section--services epc-er-reveal">
		<div class="container">
			<div class="epc-er-services">
				<a class="epc-er-service-card" href="<?php echo epc_er_href($lang, '/kontakty'); ?>">
					<i class="fa fa-gift"></i>
					<strong>Join our loyalty program</strong>
					<span>Earn points on every AED you spend</span>
				</a>
				<a class="epc-er-service-card" href="<?php echo epc_er_href($lang, '/gaming'); ?>">
					<i class="fa fa-ticket"></i>
					<strong>Events &amp; experiences</strong>
					<span>Tickets, cinema &amp; live shows in the UAE</span>
				</a>
				<a class="epc-er-service-card" href="<?php echo epc_er_href($lang, '/smartphones'); ?>">
					<i class="fa fa-bolt"></i>
					<strong>Online exclusives</strong>
					<span>Web-only tech deals — updated weekly</span>
				</a>
			</div>
		</div>
	</section>
</div>
<script>
(function () {
	var root = document.getElementById('epc_er_hero_carousel');
	if (root) {
		var slides = root.querySelectorAll('.epc-er-hero__slide');
		var dots = root.querySelectorAll('.epc-er-hero__dot');
		var idx = 0;
		var timer;
		var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
		function show(n) {
			idx = (n + slides.length) % slides.length;
			for (var i = 0; i < slides.length; i++) {
				slides[i].classList.toggle('is-active', i === idx);
				if (dots[i]) { dots[i].classList.toggle('is-active', i === idx); }
			}
		}
		function next() { show(idx + 1); }
		function start() { if (!reduced) { timer = setInterval(next, 6000); } }
		function stop() { if (timer) { clearInterval(timer); timer = null; } }
		for (var d = 0; d < dots.length; d++) {
			(function (dot, n) {
				dot.addEventListener('click', function () { stop(); show(n); start(); });
			})(dots[d], d);
		}
		root.addEventListener('mouseenter', stop);
		root.addEventListener('mouseleave', start);
		if ('IntersectionObserver' in window) {
			new IntersectionObserver(function (es) {
				if (es[0] && es[0].isIntersecting) { start(); } else { stop(); }
			}, { threshold: 0.15 }).observe(root);
		} else {
			start();
		}
	}

	if (!('IntersectionObserver' in window)) {
		var fallback = document.querySelectorAll('.epc-er-reveal');
		for (var f = 0; f < fallback.length; f++) { fallback[f].classList.add('is-visible'); }
		return;
	}
	var reducedMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	var reveals = document.querySelectorAll('.epc-er-reveal');
	if (reducedMotion) {
		for (var r = 0; r < reveals.length; r++) { reveals[r].classList.add('is-visible'); }
		return;
	}
	var io = new IntersectionObserver(function (entries) {
		entries.forEach(function (entry) {
			if (entry.isIntersecting) {
				entry.target.classList.add('is-visible');
				io.unobserve(entry.target);
			}
		});
	}, { rootMargin: '0px 0px -40px 0px', threshold: 0.08 });
	for (var j = 0; j < reveals.length; j++) { io.observe(reveals[j]); }
})();
</script>
