<?php
/**
 * Fashion animated hero — dress sway, runway chips, pink palette (Namshi retail).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_fashion_retail_namshi_data.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$eyebrow = epc_frn_pro_hero_eyebrow();
$title = epc_frn_pro_hero_title();
$copy = epc_frn_pro_hero_copy();
$actions = epc_frn_pro_hero_actions($lang);
$stats = epc_frn_pro_hero_stats();

function epc_frn_hero_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<section class="epc-home-pro epc-frn-hero-banner" style="position:relative;overflow:hidden">
	<div class="epc-particles" data-color="rgba(192,38,211,.2)" data-count="16"></div>
	<div class="container">
		<div class="epc-home-pro__grid">
			<div>
				<div class="epc-home-pro__eyebrow"><i class="fa fa-heart"></i>&nbsp; <?php echo htmlspecialchars($eyebrow, ENT_QUOTES, 'UTF-8'); ?></div>
				<h2><?php echo htmlspecialchars($title, ENT_QUOTES, 'UTF-8'); ?></h2>
				<p class="epc-home-pro__copy"><?php echo htmlspecialchars($copy, ENT_QUOTES, 'UTF-8'); ?></p>
				<div class="epc-home-pro__actions">
					<?php foreach ($actions as $act) {
						$cls = 'epc-home-pro__btn' . (!empty($act['primary']) ? ' epc-home-pro__btn--primary' : ' epc-home-pro__btn--ghost');
						?>
					<a class="<?php echo $cls; ?>" href="<?php echo epc_frn_hero_href($lang, $act['href']); ?>">
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
			<div class="epc-home-pro__visual" aria-label="Animated fashion runway visual">
				<div class="epc-frn-hero-anim" role="img" aria-label="Dress sway with floating beauty chips">
					<span class="epc-frn-hero-anim__chip">New in</span>
					<span class="epc-frn-hero-anim__dress" aria-hidden="true"></span>
					<span class="epc-frn-hero-anim__runway" aria-hidden="true"></span>
					<span class="epc-frn-hero-anim__chip epc-frn-hero-anim__chip--alt">Beauty</span>
					<div class="epc-frn-hero-anim__label">Runway &amp; beauty motion</div>
				</div>
				<div class="epc-home-pro__video-card">
					<span class="epc-home-pro__play"><i class="fa fa-play"></i></span>
					<strong>Fashion runway loop</strong>
					<p>Dress sway, floating chips and smooth CSS motion — Namshi-inspired retail energy.</p>
				</div>
			</div>
		</div>
	</div>
</section>
