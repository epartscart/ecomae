<?php
/**
 * Virgin Megastore–style footer (columns, social, payment icons).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$columns = epc_electronics_retail_footer_columns();
$social = epc_electronics_retail_social_links();
$payments = epc_electronics_retail_payment_methods();
$tradeName = function_exists('epc_brand_trade_name') ? epc_brand_trade_name() : 'Store';
$year = date('Y');

function epc_er_footer_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<footer class="epc-er-footer" id="epc_er_footer">
	<div class="epc-er-footer__main">
		<div class="container">
			<div class="epc-er-footer__grid">
				<?php foreach ($columns as $col) { ?>
				<div class="epc-er-footer__col">
					<h3 class="epc-er-footer__title"><?php echo htmlspecialchars($col['title'], ENT_QUOTES, 'UTF-8'); ?></h3>
					<ul class="epc-er-footer__links">
						<?php foreach ($col['links'] as $link) { ?>
						<li>
							<a href="<?php echo epc_er_footer_href($lang, $link['href']); ?>">
								<?php echo htmlspecialchars($link['label'], ENT_QUOTES, 'UTF-8'); ?>
							</a>
						</li>
						<?php } ?>
					</ul>
				</div>
				<?php } ?>
			</div>
			<div class="epc-er-footer__social-row">
				<div class="epc-er-footer__social">
					<span class="epc-er-footer__social-label">Follow us</span>
					<?php foreach ($social as $s) { ?>
					<a class="epc-er-footer__social-icon" href="<?php echo htmlspecialchars($s['href'], ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener noreferrer" title="<?php echo htmlspecialchars($s['label'], ENT_QUOTES, 'UTF-8'); ?>">
						<i class="fa <?php echo htmlspecialchars($s['icon'], ENT_QUOTES, 'UTF-8'); ?>" aria-hidden="true"></i>
					</a>
					<?php } ?>
				</div>
				<div class="epc-er-footer__payments">
					<span class="epc-er-footer__social-label">We accept</span>
					<div class="epc-er-footer__pay-icons">
						<?php foreach ($payments as $pay) { ?>
						<span class="epc-er-footer__pay" title="<?php echo htmlspecialchars(ucfirst($pay), ENT_QUOTES, 'UTF-8'); ?>">
							<img src="/content/files/images/icons/pay/<?php echo htmlspecialchars($pay, ENT_QUOTES, 'UTF-8'); ?>.jpg" alt="" width="48" height="32" loading="lazy" />
						</span>
						<?php } ?>
					</div>
				</div>
			</div>
		</div>
	</div>
	<div class="epc-er-footer__bar">
		<div class="container">
			<p class="epc-er-footer__copy">
				&copy; <?php echo (int) $year; ?> <?php echo htmlspecialchars($tradeName, ENT_QUOTES, 'UTF-8'); ?>.
				All rights reserved.
				<?php echo function_exists('epc_brand_hosted_by_html') ? epc_brand_hosted_by_html() : ''; ?>
			</p>
		</div>
	</div>
</footer>
