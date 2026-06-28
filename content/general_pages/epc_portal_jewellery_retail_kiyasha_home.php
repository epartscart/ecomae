<?php
/**
 * Kiyasha-style jewellery luxury retail homepage â€” Curated sections, chips, product grids.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_helpers.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$storeName = epc_jewellery_retail_kiyasha_store_name();
$promos = epc_jewellery_retail_kiyasha_promo_strip();
$heroes = epc_jewellery_retail_kiyasha_hero_slides();
$chips = epc_jewellery_retail_kiyasha_category_chips();
$brands = epc_jewellery_retail_kiyasha_brand_filters();
$categories = epc_jewellery_retail_kiyasha_category_tiles();
$sections = epc_jewellery_retail_kiyasha_product_sections();
$trustBadges = epc_jewellery_retail_kiyasha_trust_badges();

function epc_jrk_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<div class="epc-jrk-home col-lg-12">
	<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_hero_banner.php'; ?>
	<div class="epc-jrk-promo-strip epc-jrk-reveal">
		<div class="container">
			<div class="epc-jrk-promo-strip__track">
				<?php foreach ($promos as $promo) { ?>
				<a class="epc-jrk-promo-strip__item" href="<?php echo epc_jrk_href($lang, $promo['href']); ?>">
					<?php echo htmlspecialchars($promo['label'], ENT_QUOTES, 'UTF-8'); ?> <span aria-hidden="true">â†’</span>
				</a>
				<?php } ?>
			</div>
		</div>
	</div>

	<div class="epc-jrk-chips epc-jrk-reveal" aria-label="Category filters">
		<div class="container">
			<div class="epc-jrk-chips__scroll">
				<?php foreach ($chips as $chip) {
					$cls = !empty($chip['highlight']) ? ' epc-jrk-chips__chip--sale' : '';
				?>
				<a class="epc-jrk-chips__chip<?php echo $cls; ?>" href="<?php echo epc_jrk_href($lang, $chip['href']); ?>">
					<?php if (!empty($chip['icon'])) { ?><i class="fa <?php echo htmlspecialchars($chip['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i><?php } ?>
					<?php echo htmlspecialchars($chip['label'], ENT_QUOTES, 'UTF-8'); ?>
				</a>
				<?php } ?>
			</div>
		</div>
	</div>

	<div class="epc-jrk-hero epc-jrk-reveal">
		<div class="container">
			<div class="epc-jrk-hero__carousel" id="epc_jrk_hero_carousel">
				<div class="epc-jrk-hero__slides">
					<?php foreach ($heroes as $i => $slide) { ?>
					<div class="epc-jrk-hero__slide<?php echo $i === 0 ? ' is-active' : ''; ?><?php echo $slide['tone'] === 'light' ? ' epc-jrk-hero__slide--light' : ''; ?>" data-index="<?php echo (int) $i; ?>">
						<div class="epc-jrk-hero__copy">
							<span class="epc-jrk-hero__eyebrow">Luxury Edit</span>
							<h1><?php echo htmlspecialchars($slide['title'], ENT_QUOTES, 'UTF-8'); ?></h1>
							<p><?php echo htmlspecialchars($slide['sub'], ENT_QUOTES, 'UTF-8'); ?></p>
							<a class="epc-jrk-btn epc-jrk-btn--primary" href="<?php echo epc_jrk_href($lang, $slide['href']); ?>">
								<?php echo htmlspecialchars($slide['cta'], ENT_QUOTES, 'UTF-8'); ?>
							</a>
						</div>
						<div class="epc-jrk-hero__media">
							<img class="epc-jrk-hero__photo" src="<?php echo htmlspecialchars($slide['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($slide['alt'] ?? $slide['title'], ENT_QUOTES, 'UTF-8'); ?>" loading="<?php echo $i === 0 ? 'eager' : 'lazy'; ?>" decoding="async" width="720" height="480"/>
						</div>
					</div>
					<?php } ?>
				</div>
				<div class="epc-jrk-hero__dots" role="tablist" aria-label="Hero banners">
					<?php foreach ($heroes as $i => $slide) { ?>
					<button type="button" class="epc-jrk-hero__dot<?php echo $i === 0 ? ' is-active' : ''; ?>" data-goto="<?php echo (int) $i; ?>" aria-label="Slide <?php echo (int) ($i + 1); ?>"></button>
					<?php } ?>
				</div>
			</div>
		</div>
	</div>

	<section class="epc-jrk-trust epc-jrk-reveal" aria-label="Shopping guarantees">
		<div class="container">
			<div class="epc-jrk-trust__grid">
				<?php foreach ($trustBadges as $badge) { ?>
				<div class="epc-jrk-trust__item">
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

	<section class="epc-jrk-section epc-jrk-reveal">
		<div class="container">
			<h2 class="epc-jrk-section__title">Shop by category</h2>
			<div class="epc-jrk-cat-scroll epc-stagger">
				<?php foreach ($categories as $cat) { ?>
				<a class="epc-jrk-cat-tile epc-card-lift epc-img-zoom" href="<?php echo epc_jrk_href($lang, $cat['href']); ?>">
					<span class="epc-jrk-cat-tile__img"><img src="<?php echo htmlspecialchars($cat['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($cat['alt'] ?? $cat['name'], ENT_QUOTES, 'UTF-8'); ?>" loading="lazy" decoding="async" width="160" height="160"/></span>
					<span class="epc-jrk-cat-tile__name"><?php echo htmlspecialchars($cat['name'], ENT_QUOTES, 'UTF-8'); ?></span>
				</a>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-jrk-section epc-jrk-section--brands epc-jrk-reveal">
		<div class="container">
			<h2 class="epc-jrk-section__title">Shop by designer</h2>
			<div class="epc-jrk-brand-scroll">
				<?php foreach ($brands as $brand) { ?>
				<a class="epc-jrk-brand-chip" href="<?php echo epc_jrk_href($lang, '/gold'); ?>">
					<?php echo htmlspecialchars($brand, ENT_QUOTES, 'UTF-8'); ?>
				</a>
				<?php } ?>
			</div>
		</div>
	</section>

	<?php foreach ($sections as $section) { ?>
	<section class="epc-jrk-section epc-jrk-section--products epc-jrk-reveal">
		<div class="container">
			<div class="epc-jrk-section__head">
				<div>
					<h2 class="epc-jrk-section__title"><?php echo htmlspecialchars($section['title'], ENT_QUOTES, 'UTF-8'); ?></h2>
					<?php if (!empty($section['sub'])) { ?>
					<p class="epc-jrk-section__sub"><?php echo htmlspecialchars($section['sub'], ENT_QUOTES, 'UTF-8'); ?></p>
					<?php } ?>
				</div>
				<a class="epc-jrk-section__link" href="<?php echo epc_jrk_href($lang, '/gold'); ?>">View all</a>
			</div>
			<div class="epc-jrk-product-grid epc-stagger">
				<?php foreach ($section['products'] as $product) {
					$product = epc_jewellery_retail_kiyasha_resolve_product_images($product);
					$price = epc_jewellery_retail_kiyasha_format_aed($product['price']);
					$was = epc_jewellery_retail_kiyasha_format_aed($product['was']);
					$badge = epc_jewellery_retail_kiyasha_product_badge($product);
					$imgAlt = isset($product['alt']) ? $product['alt'] : $product['name'];
					$discount = '';
					if (!empty($product['was']) && !empty($product['price']) && (float) $product['was'] > (float) $product['price']) {
						$discount = '-' . (int) round(100 - ((float) $product['price'] / (float) $product['was']) * 100) . '%';
					}
				?>
				<article class="epc-jrk-product-card epc-card-lift">
					<a class="epc-jrk-product-card__link" href="<?php echo epc_jrk_href($lang, '/gold'); ?>">
						<div class="epc-jrk-product-card__img epc-img-zoom">
							<?php if ($badge !== '') { ?>
							<span class="epc-jrk-badge epc-jrk-badge--<?php echo htmlspecialchars($badge, ENT_QUOTES, 'UTF-8'); ?>">
								<?php echo $badge === 'sale' ? ($discount !== '' ? $discount : 'Sale') : 'New'; ?>
							</span>
							<?php } ?>
							<img src="<?php echo htmlspecialchars($product['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($imgAlt, ENT_QUOTES, 'UTF-8'); ?>" loading="lazy" decoding="async" width="240" height="240"/>
						</div>
						<div class="epc-jrk-product-card__brand"><?php echo htmlspecialchars($product['brand'], ENT_QUOTES, 'UTF-8'); ?></div>
						<?php if (!empty($product['sku'])) { ?><div class="epc-jrk-product-card__sku">SKU <?php echo htmlspecialchars($product['sku'], ENT_QUOTES, 'UTF-8'); ?></div><?php } ?>
						<h3 class="epc-jrk-product-card__name"><?php echo htmlspecialchars($product['name'], ENT_QUOTES, 'UTF-8'); ?></h3>
						<div class="epc-jrk-product-card__price">
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
</div>
<script>
(function () {
	var root = document.getElementById('epc_jrk_hero_carousel');
	if (root) {
		var slides = root.querySelectorAll('.epc-jrk-hero__slide');
		var dots = root.querySelectorAll('.epc-jrk-hero__dot');
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
		function start() { if (!reduced) { timer = setInterval(next, 5500); } }
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
		var fallback = document.querySelectorAll('.epc-jrk-reveal');
		for (var f = 0; f < fallback.length; f++) { fallback[f].classList.add('is-visible'); }
		return;
	}
	var reducedMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	var reveals = document.querySelectorAll('.epc-jrk-reveal');
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

