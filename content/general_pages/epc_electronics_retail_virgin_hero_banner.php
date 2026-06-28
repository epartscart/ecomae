<?php
/**
 * Electronics animated hero — chip scan + audio wave (Virgin retail).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$eyebrow = epc_er_pro_hero_eyebrow();
$title = epc_er_pro_hero_title();
$copy = epc_er_pro_hero_copy();
$stats = epc_er_pro_hero_stats();
global $db_link;
$actions = ($db_link instanceof PDO)
	? array_map(function ($tile) {
		return array(
			'label' => 'Shop ' . (string) ($tile['name'] ?? 'tech'),
			'href' => (string) ($tile['href'] ?? '/'),
			'icon' => (string) ($tile['icon'] ?? 'fa-microchip'),
			'primary' => false,
		);
	}, array_slice(epc_electronicae_product_line_tiles($db_link, '', 3), 0, 3))
	: epc_er_pro_hero_actions($lang);
if ($actions) {
	$actions[0]['primary'] = true;
}

function epc_er_hero_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<section class="epc-home-pro epc-er-hero-banner" style="position:relative;overflow:hidden">
	<div class="epc-particles" data-color="rgba(225,10,10,.25)" data-count="18"></div>
	<div class="container">
		<div class="epc-home-pro__grid">
			<div>
				<div class="epc-home-pro__eyebrow"><i class="fa fa-microchip"></i>&nbsp; <?php echo htmlspecialchars($eyebrow, ENT_QUOTES, 'UTF-8'); ?></div>
				<h2><?php echo htmlspecialchars($title, ENT_QUOTES, 'UTF-8'); ?></h2>
				<p class="epc-home-pro__copy"><?php echo htmlspecialchars($copy, ENT_QUOTES, 'UTF-8'); ?></p>
				<div class="epc-home-pro__actions">
					<?php foreach ($actions as $act) {
						$cls = 'epc-home-pro__btn' . (!empty($act['primary']) ? ' epc-home-pro__btn--primary' : ' epc-home-pro__btn--ghost');
						?>
					<a class="<?php echo $cls; ?>" href="<?php echo epc_er_hero_href($lang, $act['href']); ?>">
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
			<div class="epc-home-pro__visual" aria-label="Animated tech chip visual">
				<div class="epc-er-hero-anim" role="img" aria-label="Scanning chip with audio wave">
					<span class="epc-er-hero-anim__chip" aria-hidden="true"></span>
					<span class="epc-er-hero-anim__wave" aria-hidden="true">
						<span></span><span></span><span></span><span></span>
					</span>
					<div class="epc-er-hero-anim__label">Tech scan + wave loop</div>
				</div>
				<div class="epc-home-pro__video-card">
					<span class="epc-home-pro__play"><i class="fa fa-play"></i></span>
					<strong>Tech power animation</strong>
					<p>Scanning microchip with pulsing audio wave — Virgin Megastore retail motion.</p>
				</div>
			</div>
		</div>
	</div>
</section>
