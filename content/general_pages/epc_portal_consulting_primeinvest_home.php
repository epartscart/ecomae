<?php
/**
 * Prime Invest landing homepage — Qode demo parity, Taxofinca UAE copy.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_data.php';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_helpers.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_helpers.php';
}

$site = epc_portal_site_profile();
$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$tradeRaw = function_exists('epc_cpi_store_name') ? epc_cpi_store_name() : (isset($site['system_name']) ? $site['system_name'] : 'Taxofinca');
$trade = htmlspecialchars($tradeRaw, ENT_QUOTES, 'UTF-8');
$slides = epc_cpi_hero_slides();
$icons = epc_cpi_icon_boxes();
$services = epc_cpi_services();
$stats = epc_cpi_stats();
$steps = epc_cpi_process_steps();
$team = epc_cpi_team();
$testimonials = epc_cpi_testimonials();
$credentials = epc_cpi_credentials();
$packages = epc_cpi_service_packages();
$aboutImage = epc_cpi_about_image_url();
$partners = epc_cpi_partners();
$cp_url = '/' . $DP_Config->backend_dir;
$erpHref = function_exists('epc_portal_erp_url')
	? epc_portal_erp_url((string) $lang)
	: ($lang . '/shop/erp');

function epc_cpi_home_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<div class="epc-cpi-home col-lg-12">

	<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_hero_banner.php'; ?>

	<section class="epc-cpi-hero-slider" id="epc_cpi_hero" aria-label="Hero">
		<div class="epc-cpi-hero-slider__slides" id="epc_cpi_hero_slides">
			<?php foreach ($slides as $i => $slide) {
				$toneClass = isset($slide['tone']) && $slide['tone'] === 'green' ? 'epc-cpi-hero-slide--green' : 'epc-cpi-hero-slide--dark';
				$href = epc_cpi_home_href($lang, $slide['href']);
			?>
			<div class="epc-cpi-hero-slide <?php echo $toneClass; ?><?php echo $i === 0 ? ' is-active' : ''; ?>" data-index="<?php echo (int) $i; ?>">
				<div class="container">
					<div class="epc-cpi-hero-slide__grid">
						<div class="epc-cpi-hero-slide__copy">
							<div class="epc-cpi-hero-slide__eyebrow"><?php echo htmlspecialchars($slide['eyebrow'], ENT_QUOTES, 'UTF-8'); ?></div>
							<h1 class="epc-cpi-hero-slide__title"><?php echo htmlspecialchars($slide['title'], ENT_QUOTES, 'UTF-8'); ?></h1>
							<p class="epc-cpi-hero-slide__text"><?php echo htmlspecialchars($slide['text'], ENT_QUOTES, 'UTF-8'); ?></p>
							<div class="epc-cpi-hero-slide__actions">
								<a class="epc-cpi-btn epc-cpi-btn--primary" href="<?php echo $href; ?>"><?php echo htmlspecialchars($slide['cta'], ENT_QUOTES, 'UTF-8'); ?></a>
								<a class="epc-cpi-btn epc-cpi-btn--ghost" href="<?php echo htmlspecialchars($erpHref, ENT_QUOTES, 'UTF-8'); ?>">Client ERP portal</a>
							</div>
						</div>
						<div class="epc-cpi-hero-slide__card" aria-hidden="true">
							<div class="epc-cpi-hero-slide__card-label"><?php echo $trade; ?> at a glance</div>
							<p class="epc-cpi-hero-slide__card-stat"><?php echo htmlspecialchars($stats[0]['value'], ENT_QUOTES, 'UTF-8'); ?></p>
							<p style="color:rgba(255,255,255,.75);margin:0;font-size:15px;"><?php echo htmlspecialchars($stats[0]['label'], ENT_QUOTES, 'UTF-8'); ?></p>
						</div>
					</div>
				</div>
			</div>
			<?php } ?>
		</div>
		<div class="epc-cpi-hero-slider__arrows hidden-xs">
			<button type="button" class="epc-cpi-hero-slider__arrow" id="epc_cpi_hero_prev" aria-label="Previous slide"><i class="fa fa-chevron-left"></i></button>
			<button type="button" class="epc-cpi-hero-slider__arrow" id="epc_cpi_hero_next" aria-label="Next slide"><i class="fa fa-chevron-right"></i></button>
		</div>
		<div class="epc-cpi-hero-slider__dots" role="tablist" aria-label="Hero slides">
			<?php foreach ($slides as $i => $slide) { ?>
			<button type="button" class="epc-cpi-hero-slider__dot<?php echo $i === 0 ? ' is-active' : ''; ?>" data-goto="<?php echo (int) $i; ?>" aria-label="Slide <?php echo (int) ($i + 1); ?>"></button>
			<?php } ?>
		</div>
	</section>

	<section class="epc-cpi-icons epc-cpi-reveal">
		<div class="container">
			<div class="epc-cpi-icons__grid">
				<?php foreach ($icons as $box) { ?>
				<article class="epc-cpi-icon-box">
					<div class="epc-cpi-icon-box__icon"><i class="fa <?php echo htmlspecialchars($box['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i></div>
					<h3 class="epc-cpi-icon-box__title"><?php echo htmlspecialchars($box['title'], ENT_QUOTES, 'UTF-8'); ?></h3>
					<p class="epc-cpi-icon-box__text"><?php echo htmlspecialchars($box['text'], ENT_QUOTES, 'UTF-8'); ?></p>
				</article>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-trust epc-cpi-reveal" aria-label="Credentials">
		<div class="container">
			<div class="epc-cpi-trust__grid">
				<?php foreach ($credentials as $cred) { ?>
				<div class="epc-cpi-trust__item">
					<div class="epc-cpi-trust__icon"><i class="fa <?php echo htmlspecialchars($cred['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i></div>
					<div>
						<strong class="epc-cpi-trust__title"><?php echo htmlspecialchars($cred['title'], ENT_QUOTES, 'UTF-8'); ?></strong>
						<span class="epc-cpi-trust__text"><?php echo htmlspecialchars($cred['text'], ENT_QUOTES, 'UTF-8'); ?></span>
					</div>
				</div>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-section--gray epc-cpi-reveal" id="epc-cpi-packages">
		<div class="container">
			<div class="epc-cpi-section__head">
				<div class="epc-cpi-section__subtitle">Advisory packages</div>
				<h2 class="epc-cpi-section__title">Transparent service catalogue — prices in AED</h2>
				<p class="epc-cpi-section__lead">Fixed-scope packages for VAT, corporate tax, accounting and ERP onboarding. Each listing includes a reference SKU for proposals and client portal quotes.</p>
			</div>
			<div class="epc-cpi-packages epc-stagger">
				<?php foreach ($packages as $pkg) {
					$price = epc_cpi_format_aed($pkg['price']);
					$was = epc_cpi_format_aed(isset($pkg['was']) ? $pkg['was'] : 0);
				?>
				<article class="epc-cpi-package-card epc-card-lift">
					<a class="epc-cpi-package-card__link" href="<?php echo epc_cpi_home_href($lang, '/kontakty'); ?>">
						<div class="epc-cpi-package-card__img epc-img-zoom">
							<?php if (!empty($pkg['is_new'])) { ?><span class="epc-cpi-package-card__badge">New</span><?php } ?>
							<img src="<?php echo htmlspecialchars($pkg['image'], ENT_QUOTES, 'UTF-8'); ?>" alt="<?php echo htmlspecialchars($pkg['name'], ENT_QUOTES, 'UTF-8'); ?>" loading="lazy" decoding="async" width="320" height="200"/>
						</div>
						<div class="epc-cpi-package-card__cat"><?php echo htmlspecialchars($pkg['category'], ENT_QUOTES, 'UTF-8'); ?></div>
						<h3 class="epc-cpi-package-card__name"><?php echo htmlspecialchars($pkg['name'], ENT_QUOTES, 'UTF-8'); ?></h3>
						<div class="epc-cpi-package-card__sku">SKU <?php echo htmlspecialchars($pkg['sku'], ENT_QUOTES, 'UTF-8'); ?></div>
						<div class="epc-cpi-package-card__price">
							<strong><?php echo htmlspecialchars($price, ENT_QUOTES, 'UTF-8'); ?></strong>
							<?php if ($was !== '') { ?><s><?php echo htmlspecialchars($was, ENT_QUOTES, 'UTF-8'); ?></s><?php } ?>
						</div>
					</a>
				</article>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-reveal" id="epc-cpi-about">
		<div class="container">
			<div class="epc-cpi-split">
				<div class="epc-cpi-split__visual" style="background-image:url('<?php echo htmlspecialchars($aboutImage, ENT_QUOTES, 'UTF-8'); ?>')">
					<div class="epc-cpi-split__badge">
						<strong>UAE</strong>
						<span>Tax &amp; advisory specialists</span>
					</div>
				</div>
				<div>
					<div class="epc-cpi-section__head epc-cpi-section__head--left">
						<div class="epc-cpi-section__subtitle">About <?php echo $trade; ?></div>
						<h2 class="epc-cpi-section__title">Bridging the gap between regulation and your business goals</h2>
						<p class="epc-cpi-section__lead epc-cpi-section__lead--left">We combine corporate tax, VAT, accounting and ERP in one advisory relationship — so founders and finance teams stay ahead of UAE compliance, not behind it.</p>
					</div>
					<ul class="epc-cpi-split__list">
						<li>Corporate tax registration, returns and advisory</li>
						<li>VAT compliance, e-invoicing and FTA correspondence</li>
						<li>Management accounts and audit-ready bookkeeping</li>
						<li>Secure client ERP portal for documents and workflows</li>
					</ul>
					<p class="epc-cpi-about__cta">
						<a class="epc-cpi-btn epc-cpi-btn--primary" href="<?php echo epc_cpi_home_href($lang, '/kontakty'); ?>">Speak with an advisor</a>
					</p>
				</div>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-section--dark epc-cpi-reveal">
		<div class="container">
			<div class="epc-cpi-section__head">
				<div class="epc-cpi-section__subtitle">By the numbers</div>
				<h2 class="epc-cpi-section__title">Experience you can measure</h2>
			</div>
			<div class="epc-cpi-counters">
				<?php foreach ($stats as $stat) { ?>
				<div class="epc-cpi-counter">
					<div class="epc-cpi-counter__value"><em><?php echo htmlspecialchars($stat['value'], ENT_QUOTES, 'UTF-8'); ?></em></div>
					<div class="epc-cpi-counter__label"><?php echo htmlspecialchars($stat['label'], ENT_QUOTES, 'UTF-8'); ?></div>
					<div class="epc-cpi-counter__sub"><?php echo htmlspecialchars($stat['sub'], ENT_QUOTES, 'UTF-8'); ?></div>
				</div>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-section--gray epc-cpi-reveal" id="epc-cpi-services">
		<div class="container">
			<div class="epc-cpi-section__head">
				<div class="epc-cpi-section__subtitle">Our services</div>
				<h2 class="epc-cpi-section__title">Online advisory tailored to your entity</h2>
				<p class="epc-cpi-section__lead">Tax, accounting and business support for UAE companies — from corporate tax registration and VAT filing to audit-ready books and ERP integration.</p>
			</div>
			<div class="epc-cpi-services">
				<?php foreach ($services as $svc) { ?>
				<article class="epc-cpi-service">
					<div class="epc-cpi-service__num"><?php echo htmlspecialchars($svc['num'], ENT_QUOTES, 'UTF-8'); ?></div>
					<div class="epc-cpi-service__icon"><i class="fa <?php echo htmlspecialchars($svc['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i></div>
					<h3 class="epc-cpi-service__title"><?php echo htmlspecialchars($svc['title'], ENT_QUOTES, 'UTF-8'); ?></h3>
					<p class="epc-cpi-service__text"><?php echo htmlspecialchars($svc['text'], ENT_QUOTES, 'UTF-8'); ?></p>
				</article>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-reveal">
		<div class="container">
			<div class="epc-cpi-section__head">
				<div class="epc-cpi-section__subtitle">How we work</div>
				<h2 class="epc-cpi-section__title">Start off on the right foot</h2>
				<p class="epc-cpi-section__lead">A structured engagement from discovery through ongoing compliance — built for founders, finance teams and growing UAE entities.</p>
			</div>
			<div class="epc-cpi-process">
				<?php foreach ($steps as $step) { ?>
				<div class="epc-cpi-step">
					<div class="epc-cpi-step__num"><?php echo htmlspecialchars($step['num'], ENT_QUOTES, 'UTF-8'); ?></div>
					<h3 class="epc-cpi-step__title"><?php echo htmlspecialchars($step['title'], ENT_QUOTES, 'UTF-8'); ?></h3>
					<p class="epc-cpi-step__text"><?php echo htmlspecialchars($step['text'], ENT_QUOTES, 'UTF-8'); ?></p>
				</div>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-section--gray epc-cpi-reveal" id="epc-cpi-team">
		<div class="container">
			<div class="epc-cpi-section__head">
				<div class="epc-cpi-section__subtitle">Our team</div>
				<h2 class="epc-cpi-section__title">Advisors behind your compliance</h2>
			</div>
			<div class="epc-cpi-team">
				<?php foreach ($team as $member) { ?>
				<article class="epc-cpi-team-card">
					<div class="epc-cpi-team-card__avatar"><?php echo htmlspecialchars($member['initials'], ENT_QUOTES, 'UTF-8'); ?></div>
					<div class="epc-cpi-team-card__body">
						<h3 class="epc-cpi-team-card__name"><?php echo htmlspecialchars($member['name'], ENT_QUOTES, 'UTF-8'); ?></h3>
						<p class="epc-cpi-team-card__role"><?php echo htmlspecialchars($member['role'], ENT_QUOTES, 'UTF-8'); ?></p>
					</div>
				</article>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-reveal">
		<div class="container">
			<div class="epc-cpi-section__head">
				<div class="epc-cpi-section__subtitle">Testimonials</div>
				<h2 class="epc-cpi-section__title">See what our clients have to say</h2>
			</div>
			<div class="epc-cpi-testimonials">
				<?php foreach ($testimonials as $t) { ?>
				<blockquote class="epc-cpi-testimonial">
					<p class="epc-cpi-testimonial__quote"><?php echo htmlspecialchars($t['quote'], ENT_QUOTES, 'UTF-8'); ?></p>
					<p class="epc-cpi-testimonial__author"><?php echo htmlspecialchars($t['author'], ENT_QUOTES, 'UTF-8'); ?></p>
					<p class="epc-cpi-testimonial__company"><?php echo htmlspecialchars($t['company'], ENT_QUOTES, 'UTF-8'); ?></p>
				</blockquote>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-section epc-cpi-section--gray epc-cpi-reveal">
		<div class="container">
			<div class="epc-cpi-section__head">
				<div class="epc-cpi-section__subtitle">Trusted approach</div>
				<h2 class="epc-cpi-section__title">Our partners and standards</h2>
			</div>
			<div class="epc-cpi-partners">
				<?php foreach ($partners as $p) { ?>
				<span class="epc-cpi-partners__item"><?php echo htmlspecialchars($p, ENT_QUOTES, 'UTF-8'); ?></span>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-cpi-cta-band epc-cpi-reveal">
		<div class="container">
			<h2 class="epc-cpi-cta-band__title">Ready to simplify tax &amp; finance?</h2>
			<p class="epc-cpi-cta-band__text">Book a consultation or sign in to your client ERP portal — documents, invoices and compliance in one place.</p>
			<div class="epc-cpi-cta-band__actions">
				<a class="epc-cpi-btn epc-cpi-btn--ghost" href="<?php echo epc_cpi_home_href($lang, '/kontakty'); ?>">Book a consultation</a>
				<a class="epc-cpi-btn epc-cpi-btn--primary epc-cpi-btn--inverse" href="<?php echo htmlspecialchars($erpHref, ENT_QUOTES, 'UTF-8'); ?>">Open client portal</a>
			</div>
		</div>
	</section>
</div>
<script>
(function () {
	var slides = document.querySelectorAll('.epc-cpi-hero-slide');
	var dots = document.querySelectorAll('.epc-cpi-hero-slider__dot');
	var current = 0;
	var total = slides.length;
	if (!total) return;

	function goTo(index) {
		current = (index + total) % total;
		for (var i = 0; i < slides.length; i++) {
			slides[i].classList.toggle('is-active', i === current);
		}
		for (var j = 0; j < dots.length; j++) {
			dots[j].classList.toggle('is-active', j === current);
		}
	}
	var prev = document.getElementById('epc_cpi_hero_prev');
	var next = document.getElementById('epc_cpi_hero_next');
	if (prev) prev.addEventListener('click', function () { goTo(current - 1); });
	if (next) next.addEventListener('click', function () { goTo(current + 1); });
	for (var d = 0; d < dots.length; d++) {
		dots[d].addEventListener('click', function () {
			goTo(parseInt(this.getAttribute('data-goto'), 10));
		});
	}
	var reducedMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	if (!reducedMotion) {
		setInterval(function () { goTo(current + 1); }, 7000);
	}

	var reveals = document.querySelectorAll('.epc-cpi-reveal');
	if ('IntersectionObserver' in window && reveals.length) {
		if (reducedMotion) {
			reveals.forEach(function (el) { el.classList.add('is-visible'); });
		} else {
		var io = new IntersectionObserver(function (entries) {
			entries.forEach(function (e) {
				if (e.isIntersecting) {
					e.target.classList.add('is-visible');
					io.unobserve(e.target);
				}
			});
		}, { threshold: 0.12, rootMargin: '0px 0px -40px 0px' });
			reveals.forEach(function (el) { io.observe(el); });
		}
	} else {
		reveals.forEach(function (el) { el.classList.add('is-visible'); });
	}
})();
</script>
