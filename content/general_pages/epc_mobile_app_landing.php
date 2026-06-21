<?php
/**
 * eParts Cart mobile app — Phase 1 landing snippet (storefront / CP notes).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';

function epc_mobile_app_phase1_enabled(): bool
{
	return function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname();
}

function epc_mobile_app_landing_html(): string
{
	if (!epc_mobile_app_phase1_enabled()) {
		return '';
	}
	$home = '/en/';
	ob_start();
	?>
	<section class="epc-mobile-app-landing" style="margin:1.5rem 0;padding:1.25rem 1.5rem;border:1px solid #fecaca;border-radius:12px;background:linear-gradient(135deg,#fff5f5,#ffffff);max-width:720px;">
		<h2 style="margin:0 0 .5rem;font-size:1.15rem;color:#991b1b;">eParts Cart mobile app — Phase 1</h2>
		<p style="margin:0 0 .75rem;color:#374151;line-height:1.5;">
			Install the native shell (Android APK / iOS build) or use <strong>Add to Home Screen</strong> in Chrome or Safari for a full-screen storefront at
			<a href="<?php echo htmlspecialchars($home, ENT_QUOTES, 'UTF-8'); ?>">eParts Cart</a>.
		</p>
		<ul style="margin:0;padding-left:1.25rem;color:#4b5563;line-height:1.6;font-size:.95rem;">
			<li>Capacitor app loads <code>www.epartscart.com</code> — same catalog and checkout as the website.</li>
			<li>PWA manifest and service worker enable offline notice when the network drops.</li>
			<li>Phase 2: push notifications, barcode scan, white-label tenant apps.</li>
		</ul>
	</section>
	<?php
	return (string) ob_get_clean();
}
