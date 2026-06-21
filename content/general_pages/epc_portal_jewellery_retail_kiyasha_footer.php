<?php
/**
 * Kiyasha-style jewellery luxury footer.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$columns = epc_jewellery_retail_kiyasha_footer_columns();
$social = epc_jewellery_retail_kiyasha_social_links();
$payments = epc_jewellery_retail_kiyasha_payment_methods();
$tradeName = function_exists('epc_brand_trade_name') ? epc_brand_trade_name() : 'Store';
$year = date('Y');

function epc_jrk_footer_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<footer class="epc-jrk-footer" id="epc_jrk_footer">
	<div class="epc-jrk-footer__main">
		<div class="container">
			<div class="epc-jrk-footer__grid">
				<?php foreach ($columns as $col) { ?>
				<div class="epc-jrk-footer__col">
					<h3 class="epc-jrk-footer__title"><?php echo htmlspecialchars($col['title'], ENT_QUOTES, 'UTF-8'); ?></h3>
					<ul class="epc-jrk-footer__links">
						<?php foreach ($col['links'] as $link) { ?>
						<li>
							<a href="<?php echo epc_jrk_footer_href($lang, $link['href']); ?>">
								<?php echo htmlspecialchars($link['label'], ENT_QUOTES, 'UTF-8'); ?>
							</a>
						</li>
						<?php } ?>
					</ul>
				</div>
				<?php } ?>
			</div>
			<div class="epc-jrk-footer__social-row">
				<div class="epc-jrk-footer__social">
					<span class="epc-jrk-footer__social-label">Follow us</span>
					<?php foreach ($social as $s) { ?>
					<a class="epc-jrk-footer__social-icon" href="<?php echo htmlspecialchars($s['href'], ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener noreferrer" title="<?php echo htmlspecialchars($s['label'], ENT_QUOTES, 'UTF-8'); ?>">
						<i class="fa <?php echo htmlspecialchars($s['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i>
					</a>
					<?php } ?>
				</div>
				<div class="epc-jrk-footer__payments">
					<span class="epc-jrk-footer__social-label">We accept</span>
					<div class="epc-jrk-footer__pay-icons">
						<?php foreach ($payments as $pay) { ?>
						<span class="epc-jrk-footer__pay" title="<?php echo htmlspecialchars(ucfirst($pay), ENT_QUOTES, 'UTF-8'); ?>">
							<img src="/content/files/images/icons/pay/<?php echo htmlspecialchars($pay, ENT_QUOTES, 'UTF-8'); ?>.jpg" alt="" width="48" height="32" loading="lazy" />
						</span>
						<?php } ?>
					</div>
				</div>
			</div>
		</div>
	</div>
	<div class="epc-jrk-footer__bar">
		<div class="container">
			<p class="epc-jrk-footer__copy">
				&copy; <?php echo (int) $year; ?> <?php echo htmlspecialchars($tradeName, ENT_QUOTES, 'UTF-8'); ?>.
				All rights reserved.
				<?php echo function_exists('epc_brand_hosted_by_html') ? epc_brand_hosted_by_html() : ''; ?>
			</p>
		</div>
	</div>
</footer>

