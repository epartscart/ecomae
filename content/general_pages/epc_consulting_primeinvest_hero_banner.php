<?php
/**
 * Consultancy animated hero — growing charts, blue/gold (Prime Invest).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_data.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$eyebrow = epc_cpi_pro_hero_eyebrow();
$title = epc_cpi_pro_hero_title();
$copy = epc_cpi_pro_hero_copy();
$actions = epc_cpi_pro_hero_actions($lang);
$stats = epc_cpi_pro_hero_stats();

function epc_cpi_hero_banner_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<section class="epc-home-pro epc-cpi-hero-banner">
	<div class="container">
		<div class="epc-home-pro__grid">
			<div>
				<div class="epc-home-pro__eyebrow"><i class="fa fa-line-chart"></i>&nbsp; <?php echo htmlspecialchars($eyebrow, ENT_QUOTES, 'UTF-8'); ?></div>
				<h2><?php echo htmlspecialchars($title, ENT_QUOTES, 'UTF-8'); ?></h2>
				<p class="epc-home-pro__copy"><?php echo htmlspecialchars($copy, ENT_QUOTES, 'UTF-8'); ?></p>
				<div class="epc-home-pro__actions">
					<?php foreach ($actions as $act) {
						$cls = 'epc-home-pro__btn' . (!empty($act['primary']) ? ' epc-home-pro__btn--primary' : ' epc-home-pro__btn--ghost');
						?>
					<a class="<?php echo $cls; ?>" href="<?php echo epc_cpi_hero_banner_href($lang, $act['href']); ?>">
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
			<div class="epc-home-pro__visual" aria-label="Animated advisory charts">
				<div class="epc-cpi-hero-anim" role="img" aria-label="Growing performance charts">
					<span class="epc-cpi-hero-anim__bar" aria-hidden="true"></span>
					<span class="epc-cpi-hero-anim__bar" aria-hidden="true"></span>
					<span class="epc-cpi-hero-anim__bar" aria-hidden="true"></span>
					<span class="epc-cpi-hero-anim__bar" aria-hidden="true"></span>
					<div class="epc-cpi-hero-anim__label">Compliance &amp; growth charts</div>
				</div>
				<div class="epc-home-pro__video-card">
					<span class="epc-home-pro__play"><i class="fa fa-play"></i></span>
					<strong>Advisory growth loop</strong>
					<p>Animated blue &amp; gold performance bars — Prime Invest professional motion.</p>
				</div>
			</div>
		</div>
	</div>
</section>
