<?php
/**
 * Automotive spare parts package homepage (piston hero + quick-link banners).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_automotive_spareparts_data.php';

$lang = epc_asp_home_lang();
$banners = epc_asp_home_banners($lang);

require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_automotive_spareparts_piston_banner.php';
?>
<section class="epc-home-banners epc-asp-home-banners">
	<div class="container">
		<div class="epc-home-banners__grid">
			<?php foreach ($banners as $banner) {
				$tone = preg_replace('/[^a-z]/', '', (string) ($banner['tone'] ?? 'red'));
				?>
			<a class="epc-home-banner epc-home-banner--<?php echo htmlspecialchars($tone, ENT_QUOTES, 'UTF-8'); ?>" href="<?php echo htmlspecialchars($banner['href'], ENT_QUOTES, 'UTF-8'); ?>">
				<span class="epc-home-banner__icon"><i class="fa <?php echo htmlspecialchars($banner['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i></span>
				<strong><?php echo htmlspecialchars($banner['title'], ENT_QUOTES, 'UTF-8'); ?></strong>
				<span><?php echo htmlspecialchars($banner['text'], ENT_QUOTES, 'UTF-8'); ?></span>
			</a>
			<?php } ?>
		</div>
	</div>
</section>
