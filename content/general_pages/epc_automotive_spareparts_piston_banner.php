<?php
/**
 * Piston + crankshaft hero visual (legacy eParts Cart homepage module).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_automotive_spareparts_data.php';

$lang = epc_asp_home_lang();
$eyebrow = epc_asp_hero_eyebrow();
$title = epc_asp_hero_title();
$copy = epc_asp_hero_copy();
$actions = epc_asp_hero_actions($lang);
$stats = epc_asp_hero_stats();
?>
<section class="epc-home-pro epc-asp-piston-banner">
	<div class="container">
		<div class="epc-home-pro__grid">
			<div>
				<div class="epc-home-pro__eyebrow"><i class="fa fa-bolt"></i>&nbsp; <?php echo htmlspecialchars($eyebrow, ENT_QUOTES, 'UTF-8'); ?></div>
				<h2><?php echo htmlspecialchars($title, ENT_QUOTES, 'UTF-8'); ?></h2>
				<p class="epc-home-pro__copy"><?php echo htmlspecialchars($copy, ENT_QUOTES, 'UTF-8'); ?></p>
				<div class="epc-home-pro__actions">
					<?php foreach ($actions as $act) {
						$cls = 'epc-home-pro__btn' . (!empty($act['primary']) ? ' epc-home-pro__btn--primary' : ' epc-home-pro__btn--ghost');
						?>
					<a class="<?php echo $cls; ?>" href="<?php echo htmlspecialchars($act['href'], ENT_QUOTES, 'UTF-8'); ?>">
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
			<div class="epc-home-pro__visual" aria-label="Animated piston moving inside an engine">
				<div class="epc-engine-animation" role="img" aria-label="Animated piston moving inside an engine">
					<div class="epc-engine-block">
						<div class="epc-engine-cylinder epc-engine-cylinder--one"><span class="epc-engine-piston"></span></div>
						<div class="epc-engine-cylinder epc-engine-cylinder--two"><span class="epc-engine-piston"></span></div>
						<div class="epc-engine-cylinder epc-engine-cylinder--three"><span class="epc-engine-piston"></span></div>
						<div class="epc-engine-crank"></div>
					</div>
					<div class="epc-engine-exhaust"></div>
					<div class="epc-engine-spark"></div>
					<div class="epc-engine-part epc-engine-ring" aria-hidden="true"></div>
					<div class="epc-engine-part epc-engine-gasket" aria-hidden="true"></div>
					<div class="epc-engine-part epc-engine-liner" aria-hidden="true"></div>
					<div class="epc-engine-part epc-engine-suspension" aria-hidden="true"></div>
					<div class="epc-engine-label">Engine + parts power loop</div>
				</div>
				<div class="epc-home-pro__video-card">
					<span class="epc-home-pro__play"><i class="fa fa-play"></i></span>
					<strong>Piston power animation</strong>
					<p>A moving piston and crankshaft loop shows engine power in a professional workshop style.</p>
				</div>
			</div>
		</div>
	</div>
</section>
