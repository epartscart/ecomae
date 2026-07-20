<?php
/**
 * ERP top-bar branding — tenant / platform aware logo + title.
 *
 * epartscart.com → eParts Cart logo & name
 * ecomae.com (platform) → ECOM AE logo & name
 * other tenants → tenant brand / company profile / trade name
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';

/**
 * @return array{
 *   mode:string,
 *   title:string,
 *   tagline:string,
 *   logo_url:string,
 *   logo_html:string,
 *   aria:string,
 *   host_label:string
 * }
 */
function epc_erp_topbar_brand_context(): array
{
	static $cached = null;
	if (is_array($cached)) {
		return $cached;
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';

	$brand = function_exists('epc_brand_cp_context') ? epc_brand_cp_context() : array();
	$host = function_exists('epc_portal_host') ? strtolower(trim((string) epc_portal_host())) : '';
	$host = preg_replace('/^www\./', '', $host);
	$isPlatform = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
	$isPlatformErp = function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active();
	$isEparts = function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname();
	$isSharedErp = !empty($brand['is_shared_erp_session']);

	$title = trim((string) ($brand['company_name'] ?? ''));
	if ($title === '') {
		$title = trim((string) ($brand['product_name'] ?? ''));
	}
	$tagline = trim((string) ($brand['hub_tagline'] ?? 'Finance & operations'));
	$mode = 'generic';
	$logoUrl = '';
	$logoHtml = '';
	$hostLabel = $host !== '' ? $host : 'tenant';

	// 1) Platform / ecomae operator shell (not a shared client ERP session).
	if (($isPlatform || $isPlatformErp) && !$isSharedErp) {
		$mode = 'ecomae';
		$title = $isPlatformErp ? 'ECOM AE Operations' : 'ECOM AE';
		$tagline = $isPlatformErp ? 'Platform ERP · ecomae registry' : 'Finance & operations · ecomae.com';
		$hostLabel = 'ecomae.com';
		$logoUrl = function_exists('epc_ecomae_platform_logo_url')
			? epc_ecomae_platform_logo_url()
			: '/content/general_pages/epc_ecomae_logo_svg.php';
		$logoHtml = epc_ecomae_static_logo('compact', array(
			'show_title' => false,
			'show_tagline' => false,
			'aria_label' => 'ECOM AE',
		));
		$cached = array(
			'mode' => $mode,
			'title' => $title,
			'tagline' => $tagline,
			'logo_url' => $logoUrl,
			'logo_html' => $logoHtml,
			'aria' => 'ECOM AE',
			'host_label' => $hostLabel,
		);
		return $cached;
	}

	// 2) eParts Cart tenant host — always tenant storefront brand (not hub/parent name).
	if ($isEparts) {
		$mode = 'epartscart';
		$title = 'eParts Cart';
		$tagline = 'epartscart.com · Finance & operations';
		$hostLabel = 'epartscart.com';
		$logoUrl = '/content/files/images/ecomae-platform/assets/epartscart.png';
		$logoHtml = epc_erp_topbar_brand_img($logoUrl, 'eParts Cart', 'epc-erp-topbar__tenant-logo epc-erp-topbar__tenant-logo--epartscart');
		$cached = array(
			'mode' => $mode,
			'title' => $title,
			'tagline' => $tagline,
			'logo_url' => $logoUrl,
			'logo_html' => $logoHtml,
			'aria' => 'eParts Cart',
			'host_label' => $hostLabel,
		);
		return $cached;
	}

	// 3) Known tenant brand pack (electronics, fashion, jewellery, tax…).
	$tenantBrandFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_brand.php';
	if (is_file($tenantBrandFile)) {
		require_once $tenantBrandFile;
		if (function_exists('epc_portal_tenant_brand_config')) {
			$tb = epc_portal_tenant_brand_config();
			if (is_array($tb) && !empty($tb['logo_url'])) {
				$mode = 'tenant_pack';
				$label = trim((string) ($tb['label'] ?? $title));
				if ($label !== '') {
					$title = $label;
				}
				$packTag = trim((string) ($tb['tagline'] ?? ''));
				if ($packTag !== '') {
					$tagline = $packTag;
				} else {
					$tagline = ($host !== '' ? $host : 'Tenant') . ' · Finance & operations';
				}
				$logoUrl = (string) $tb['logo_url'];
				$logoHtml = epc_erp_topbar_brand_img($logoUrl, $title, 'epc-erp-topbar__tenant-logo');
				$cached = array(
					'mode' => $mode,
					'title' => $title,
					'tagline' => $tagline,
					'logo_url' => $logoUrl,
					'logo_html' => $logoHtml,
					'aria' => $title,
					'host_label' => $hostLabel,
				);
				return $cached;
			}
		}
	}

	// 4) Company profile logo from ERP (per-tenant DB).
	$coLogo = '';
	$coName = '';
	global $db_link;
	$coFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
	if (is_file($coFile) && isset($db_link) && $db_link instanceof PDO) {
		require_once $coFile;
		if (function_exists('epc_co_profile_get')) {
			try {
				$co = epc_co_profile_get($db_link);
				$coLogo = trim((string) ($co['logo_url'] ?? ''));
				$coName = trim((string) (($co['trade_name'] ?? '') !== '' ? $co['trade_name'] : ($co['legal_name'] ?? '')));
			} catch (Throwable $e) {
			}
		}
	}
	if ($coName !== '') {
		$title = $coName;
	}
	if ($title === '' || stripos($title, 'e-world commerce system') !== false) {
		$trade = function_exists('epc_brand_trade_name') ? trim((string) epc_brand_trade_name()) : '';
		if ($trade !== '') {
			$title = $trade;
		}
	}
	if ($title === '') {
		$title = 'ERP';
	}
	$tagline = ($host !== '' ? $host : 'Tenant') . ' · Finance & operations';

	if ($coLogo !== '') {
		$mode = 'company';
		$logoUrl = $coLogo;
		$logoHtml = epc_erp_topbar_brand_img($logoUrl, $title, 'epc-erp-topbar__tenant-logo');
	} else {
		// Fallback: initials mark (never show ecomae logo on a tenant ERP).
		$mode = 'initials';
		$logoHtml = epc_erp_topbar_brand_initials($title);
	}

	$cached = array(
		'mode' => $mode,
		'title' => $title,
		'tagline' => $tagline,
		'logo_url' => $logoUrl,
		'logo_html' => $logoHtml,
		'aria' => $title,
		'host_label' => $hostLabel,
	);
	return $cached;
}

function epc_erp_topbar_brand_img(string $url, string $alt, string $class = 'epc-erp-topbar__tenant-logo'): string
{
	$url = trim($url);
	if ($url === '') {
		return epc_erp_topbar_brand_initials($alt);
	}
	return '<span class="' . htmlspecialchars($class, ENT_QUOTES, 'UTF-8') . '" role="img" aria-label="'
		. htmlspecialchars($alt, ENT_QUOTES, 'UTF-8') . '">'
		. '<img src="' . htmlspecialchars($url, ENT_QUOTES, 'UTF-8') . '" alt="" width="36" height="36" loading="eager" />'
		. '</span>';
}

function epc_erp_topbar_brand_initials(string $title): string
{
	$title = trim($title);
	$parts = preg_split('/\s+/', $title) ?: array();
	$ini = '';
	foreach ($parts as $p) {
		$p = trim((string) $p);
		if ($p === '') {
			continue;
		}
		$ini .= strtoupper(substr($p, 0, 1));
		if (strlen($ini) >= 2) {
			break;
		}
	}
	if ($ini === '') {
		$ini = 'ER';
	}
	return '<span class="epc-erp-topbar__initials" aria-hidden="true">' . htmlspecialchars($ini, ENT_QUOTES, 'UTF-8') . '</span>';
}

/**
 * Render logo + title/tagline block for the ERP top bar (inner brand link contents).
 */
function epc_erp_topbar_brand_markup(): string
{
	$ctx = epc_erp_topbar_brand_context();
	ob_start();
	?>
			<?php echo $ctx['logo_html']; ?>
			<span class="epc-erp-topbar__brand-text">
				<strong><?php echo htmlspecialchars($ctx['title'], ENT_QUOTES, 'UTF-8'); ?></strong>
				<small><?php echo htmlspecialchars($ctx['tagline'], ENT_QUOTES, 'UTF-8'); ?></small>
			</span>
	<?php
	return ob_get_clean();
}
