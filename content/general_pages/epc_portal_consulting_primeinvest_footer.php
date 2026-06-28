<?php
/**
 * Prime Invest footer — Qode 4-column + newsletter.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_brand.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';

$lang = isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '' ? $multilang_params['lang_href'] : '/en';
$columns = epc_cpi_footer_columns();
$tradeName = function_exists('epc_brand_trade_name') ? epc_brand_trade_name() : 'Taxofinca';
$year = date('Y');
$contact = epc_cpi_header_contact();

function epc_cpi_footer_href($lang, $path)
{
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return htmlspecialchars(rtrim((string) $lang, '/') . $path, ENT_QUOTES, 'UTF-8');
	}
	return htmlspecialchars($path, ENT_QUOTES, 'UTF-8');
}
?>
<footer class="epc-cpi-footer" id="epc_cpi_footer">
	<div class="epc-cpi-footer__top">
		<div class="container">
			<div class="epc-cpi-footer__grid">
				<div class="epc-cpi-footer__brand">
					<?php
					if (function_exists('epc_portal_tenant_brand_enabled') && epc_portal_tenant_brand_enabled()) {
						echo epc_portal_tenant_brand_markup('compact');
					}
					?>
					<p><?php echo htmlspecialchars($tradeName, ENT_QUOTES, 'UTF-8'); ?> — tax, accounting and business advisory for UAE entities. Corporate tax, VAT and client ERP in one relationship.</p>
					<div class="epc-cpi-footer__newsletter">
						<label for="epc_cpi_newsletter">Stay informed on tax updates</label>
						<div class="epc-cpi-footer__newsletter-row">
							<input type="email" id="epc_cpi_newsletter" placeholder="Your email" autocomplete="email" />
							<button type="button" onclick="window.location.href='<?php echo epc_cpi_footer_href($lang, '/kontakty'); ?>'">Subscribe</button>
						</div>
					</div>
				</div>
				<?php foreach ($columns as $col) { ?>
				<div class="epc-cpi-footer__col">
					<h3 class="epc-cpi-footer__title"><?php echo htmlspecialchars($col['title'], ENT_QUOTES, 'UTF-8'); ?></h3>
					<ul class="epc-cpi-footer__links">
						<?php foreach ($col['links'] as $link) { ?>
						<li>
							<a href="<?php echo epc_cpi_footer_href($lang, $link['href']); ?>">
								<?php echo htmlspecialchars($link['label'], ENT_QUOTES, 'UTF-8'); ?>
							</a>
						</li>
						<?php } ?>
					</ul>
				</div>
				<?php } ?>
			</div>
		</div>
	</div>
	<div class="epc-cpi-footer__bar">
		<div class="container" style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;">
			<p class="epc-cpi-footer__copy">
				&copy; <?php echo (int) $year; ?> <?php echo htmlspecialchars($tradeName, ENT_QUOTES, 'UTF-8'); ?>.
				<?php echo htmlspecialchars($contact['email'], ENT_QUOTES, 'UTF-8'); ?> &middot;
				<?php echo htmlspecialchars($contact['phone'], ENT_QUOTES, 'UTF-8'); ?>
				<?php echo function_exists('epc_brand_hosted_by_html') ? ' &middot; ' . epc_brand_hosted_by_html() : ''; ?>
			</p>
			<?php
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_worldclass.php';
			$epc_cpi_social = epc_storefront_social_links_data();
			if ($epc_cpi_social) {
				echo '<div class="epc-cpi-footer__social" style="display:flex;gap:12px;">';
				foreach ($epc_cpi_social as $s) {
					echo '<a href="' . htmlspecialchars($s['url'], ENT_QUOTES, 'UTF-8') . '" target="_blank" rel="noopener" title="' . htmlspecialchars($s['label'], ENT_QUOTES, 'UTF-8') . '" style="color:#94a3b8;font-size:18px;"><i class="fa ' . htmlspecialchars($s['icon'], ENT_QUOTES, 'UTF-8') . '"></i></a>';
				}
				echo '</div>';
			}
			?>
		</div>
	</div>
</footer>
