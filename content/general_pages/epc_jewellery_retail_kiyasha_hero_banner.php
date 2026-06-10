<?php
/**
 * Luxury jewellery animated hero — ring glow + sparkle (Kiyasha retail).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_data.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$eyebrow = epc_jrk_pro_hero_eyebrow();
$title = epc_jrk_pro_hero_title();
$copy = epc_jrk_pro_hero_copy();
$actions = epc_jrk_pro_hero_actions($lang);
$stats = epc_jrk_pro_hero_stats();

function epc_jrk_hero_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<section class="epc-home-pro epc-jrk-hero-banner">
	<div class="container">
		<div class="epc-home-pro__grid">
			<div>
				<div class="epc-home-pro__eyebrow"><i class="fa fa-diamond"></i>&nbsp; <?php echo htmlspecialchars($eyebrow, ENT_QUOTES, 'UTF-8'); ?></div>
				<h2><?php echo htmlspecialchars($title, ENT_QUOTES, 'UTF-8'); ?></h2>
				<p class="epc-home-pro__copy"><?php echo htmlspecialchars($copy, ENT_QUOTES, 'UTF-8'); ?></p>
				<div class="epc-home-pro__actions">
					<?php foreach ($actions as $act) {
						$cls = 'epc-home-pro__btn' . (!empty($act['primary']) ? ' epc-home-pro__btn--primary' : ' epc-home-pro__btn--ghost');
						?>
					<a class="<?php echo $cls; ?>" href="<?php echo epc_jrk_hero_href($lang, $act['href']); ?>">
						<i class="fa <?php echo htmlspecialchars($act['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i>
						<?php echo htmlspecialchars($act['label'], ENT_QUOTES, 'UTF-8'); ?>
					</a>
					<?php } ?>
				</div>
				<div class="epc-home-pro__stats">
					<?php foreach ($stats as $stat) { ?>
					<div class="epc-home-pro__stat">
						<strong><?php echo htmlspecialchars($stat['value'], ENT_QUOTES, 'UTF-8'); ?></strong>
						<span><?php echo htmlspecialchars($stat['label'], ENT_QUOTES, 'UTF-8'); ?></span>
					</div>
					<?php } ?>
				</div>
			</div>
			<div class="epc-home-pro__visual" aria-label="Animated jewellery ring visual">
				<div class="epc-jrk-hero-anim" role="img" aria-label="Glowing gold ring with sparkle">
					<span class="epc-jrk-hero-anim__ring" aria-hidden="true"></span>
					<span class="epc-jrk-hero-anim__gem" aria-hidden="true"></span>
					<span class="epc-jrk-hero-anim__spark epc-jrk-hero-anim__spark--one" aria-hidden="true"></span>
					<span class="epc-jrk-hero-anim__spark epc-jrk-hero-anim__spark--two" aria-hidden="true"></span>
					<div class="epc-jrk-hero-anim__label">Gold ring glow loop</div>
				</div>
				<div class="epc-home-pro__video-card">
					<span class="epc-home-pro__play"><i class="fa fa-play"></i></span>
					<strong>Luxury sparkle loop</strong>
					<p>Glowing gold ring with diamond sparkles — Kiyasha fine jewellery motion.</p>
				</div>
			</div>
		</div>
	</div>
</section>
