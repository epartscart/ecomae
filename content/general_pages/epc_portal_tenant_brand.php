<?php
/**
 * Tenant storefront brand — animated logo + tagline for DNS-only client sites.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

function epc_portal_tenant_brand_catalog(): array
{
	$base = '/content/files/images/ecomae-platform/assets/';
	return array(
		'electronicae' => array(
			'label' => 'Electronicae',
			'logo_url' => $base . 'electronicae.png',
			'tagline' => 'TECH • GAMING • UAE',
			'accent' => '#e10a0a',
			'glow' => 'rgba(225,10,10,.24)',
			'animation' => 'circuit',
		),
		'stylenlook' => array(
			'label' => 'Stylenlook',
			'logo_url' => $base . 'stylenlook.png',
			'tagline' => 'FASHION & BEAUTY',
			'accent' => '#ec4899',
			'glow' => 'rgba(236,72,153,.26)',
			'animation' => 'fashion',
		),
		'thejewellerytrend' => array(
			'label' => 'The Jewellery Trend',
			'logo_url' => $base . 'thejewellerytrend.png',
			'tagline' => 'STYLE • SPARKLE • SHINE',
			'accent' => '#d97706',
			'glow' => 'rgba(217,119,6,.32)',
			'animation' => 'sparkle',
		),
		'taxofinca' => array(
			'label' => 'TaxoFinca',
			'logo_url' => $base . 'taxofinca.png',
			'tagline' => 'TAX & ACCOUNTING SOLUTIONS',
			'accent' => '#227a40',
			'glow' => 'rgba(34,122,64,.24)',
			'animation' => 'advisory',
		),
	);
}

function epc_portal_tenant_brand_site_key(): ?string
{
	static $key = null;
	static $resolved = false;
	if ($resolved) {
		return $key;
	}
	$resolved = true;
	$catalog = epc_portal_tenant_brand_catalog();
	$site = epc_portal_site_profile();
	if (!empty($site['site_key'])) {
		$candidate = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $site['site_key']));
		if ($candidate !== '' && isset($catalog[$candidate])) {
			$key = $candidate;
			return $key;
		}
	}
	$host = epc_portal_host();
	$host = preg_replace('/^www\./', '', strtolower(trim($host)));
	foreach (array_keys($catalog) as $candidate) {
		if ($host === $candidate . '.com' || $host === $candidate) {
			$key = $candidate;
			return $key;
		}
	}
	return null;
}

function epc_portal_tenant_brand_config(): ?array
{
	$siteKey = epc_portal_tenant_brand_site_key();
	if ($siteKey === null) {
		return null;
	}
	$catalog = epc_portal_tenant_brand_catalog();
	return isset($catalog[$siteKey]) ? $catalog[$siteKey] : null;
}

function epc_portal_tenant_brand_enabled(): bool
{
	if (!function_exists('epc_portal_is_client_hostname') || !epc_portal_is_client_hostname()) {
		return false;
	}
	$site = function_exists('epc_portal_site_profile') ? epc_portal_site_profile() : array();
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($site['site_key'] ?? '')));
	if ($siteKey === 'epartscart') {
		return false;
	}
	if (isset($site['industry']) && $site['industry'] === 'auto_parts') {
		return false;
	}
	return epc_portal_tenant_brand_config() !== null;
}

function epc_portal_tenant_brand_css_href(): string
{
	return '/content/general_pages/epc_portal_tenant_brand.css';
}

function epc_portal_tenant_brand_css_version(): string
{
	return '20260528c';
}

function epc_portal_tenant_brand_enqueue(): void
{
	static $done = false;
	if ($done || !epc_portal_tenant_brand_enabled()) {
		return;
	}
	$done = true;
	$href = htmlspecialchars(epc_portal_tenant_brand_css_href(), ENT_QUOTES, 'UTF-8');
	$ver = htmlspecialchars(epc_portal_tenant_brand_css_version(), ENT_QUOTES, 'UTF-8');
	echo '<link rel="stylesheet" href="' . $href . '?v=' . $ver . '" />' . "\n";
}

/**
 * @param string $variant header|hero|compact
 */
function epc_portal_tenant_brand_markup(string $variant = 'header'): string
{
	$brand = epc_portal_tenant_brand_config();
	if ($brand === null) {
		return '';
	}
	$variant = in_array($variant, array('header', 'hero', 'compact'), true) ? $variant : 'header';
	$label = (string) ($brand['label'] ?? 'Store');
	$tagline = (string) ($brand['tagline'] ?? '');
	$logo = (string) ($brand['logo_url'] ?? '');
	$accent = (string) ($brand['accent'] ?? '#2563eb');
	$glow = (string) ($brand['glow'] ?? 'rgba(37,99,235,.2)');
	$animation = preg_replace('/[^a-z0-9_-]/', '', (string) ($brand['animation'] ?? 'default'));
	$showTagline = $variant !== 'compact';
	$aria = $label . ($tagline !== '' ? ' — ' . $tagline : '');

	ob_start();
	?>
<span class="epc-tenant-brand epc-tenant-brand--<?php echo htmlspecialchars($variant, ENT_QUOTES, 'UTF-8'); ?> epc-tenant-brand--<?php echo htmlspecialchars($animation, ENT_QUOTES, 'UTF-8'); ?>" role="img" aria-label="<?php echo htmlspecialchars($aria, ENT_QUOTES, 'UTF-8'); ?>" style="--etb-accent:<?php echo htmlspecialchars($accent, ENT_QUOTES, 'UTF-8'); ?>;--etb-glow:<?php echo htmlspecialchars($glow, ENT_QUOTES, 'UTF-8'); ?>">
	<span class="epc-tenant-brand__halo" aria-hidden="true"></span>
	<span class="epc-tenant-brand__logo-wrap">
		<img class="epc-tenant-brand__logo" src="<?php echo htmlspecialchars($logo, ENT_QUOTES, 'UTF-8'); ?>" alt="" width="320" height="96" loading="<?php echo $variant === 'hero' ? 'eager' : 'lazy'; ?>" />
	</span>
	<?php if ($showTagline && $tagline !== '') { ?>
	<span class="epc-tenant-brand__tagline"><?php echo htmlspecialchars($tagline, ENT_QUOTES, 'UTF-8'); ?></span>
	<?php } ?>
</span>
	<?php
	return ob_get_clean();
}

function epc_portal_tenant_brand_hero_block(): string
{
	if (!epc_portal_tenant_brand_enabled()) {
		return '';
	}
	epc_portal_tenant_brand_enqueue();
	ob_start();
	?>
<section class="epc-tenant-brand-hero col-lg-12" aria-label="Brand presentation">
	<div class="epc-tenant-brand-hero__inner">
		<?php echo epc_portal_tenant_brand_markup('hero'); ?>
	</div>
</section>
	<?php
	return ob_get_clean();
}
