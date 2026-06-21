<?php
/**
 * Industry storefront animated SVG logos (live tenant headers).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_portal.php';
require_once __DIR__ . '/epc_branding.php';

function epc_storefront_animated_logo_enqueue(): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	echo '<link rel="stylesheet" href="/content/general_pages/epc_storefront_animated_logos.css?v=20260529h" />' . "\n";
}

function epc_storefront_animated_logo_markup(string $packageId = ''): string
{
	if ($packageId === '' && function_exists('epc_portal_active_storefront_package')) {
		$packageId = (string) epc_portal_active_storefront_package();
	}
	$packageId = preg_replace('/[^a-z0-9_]/', '', $packageId);
	if ($packageId === '') {
		return '';
	}
	$label = epc_brand_trade_name();
	if ($label === '' || stripos($label, 'epart') !== false) {
		$defaults = array(
			'fashion_retail_namshi' => 'Stylenlook',
			'electronics_retail_virgin' => 'Electronicae',
			'consulting_primeinvest' => 'TaxoFinca',
			'jewellery_retail_kiyasha' => 'The Jewellery Trend',
		);
		if (isset($defaults[$packageId])) {
			$label = $defaults[$packageId];
		}
	}
	epc_storefront_animated_logo_enqueue();
	switch ($packageId) {
		case 'fashion_retail_namshi':
			return epc_storefront_animated_logo_fashion($label);
		case 'electronics_retail_virgin':
			return epc_storefront_animated_logo_electronics($label);
		case 'consulting_primeinvest':
			return epc_storefront_animated_logo_consulting($label);
		case 'jewellery_retail_kiyasha':
			return epc_storefront_animated_logo_jewellery($label);
		default:
			return '';
	}
}

function epc_storefront_animated_logo_fashion(string $label): string
{
	ob_start();
	?>
<span class="epc-sf-logo epc-sf-logo--fashion" aria-label="<?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?>">
	<svg class="epc-sf-logo__mark" viewBox="0 0 36 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
		<path class="epc-sf-logo__hanger-hook" d="M18 4c-2 0-3 1.5-3 3.5S16 11 18 11s3-1.5 3-3.5S20 4 18 4z" fill="#f9a8d4"/>
		<path class="epc-sf-logo__dress" d="M18 11 L6 28 L30 28 Z" fill="#ec4899" opacity=".92"/>
	</svg>
	<span class="epc-sf-logo__text"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></span>
</span>
	<?php
	return ob_get_clean();
}

function epc_storefront_animated_logo_electronics(string $label): string
{
	ob_start();
	?>
<span class="epc-sf-logo epc-sf-logo--electronics" aria-label="<?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?>">
	<svg class="epc-sf-logo__mark" viewBox="0 0 40 36" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
		<rect x="6" y="8" width="28" height="20" rx="4" fill="#111" stroke="#e10a0a" stroke-width="2"/>
		<circle class="epc-sf-logo__pulse" cx="20" cy="18" r="5" fill="#e10a0a"/>
	</svg>
	<span class="epc-sf-logo__text"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></span>
</span>
	<?php
	return ob_get_clean();
}

function epc_storefront_animated_logo_consulting(string $label): string
{
	ob_start();
	?>
<span class="epc-sf-logo epc-sf-logo--consulting" aria-label="<?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?>">
	<svg class="epc-sf-logo__mark" viewBox="0 0 48 36" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
		<rect class="epc-sf-logo__bar" x="4" y="18" width="8" height="14" rx="2" fill="#d4af37"/>
		<rect class="epc-sf-logo__bar" x="18" y="10" width="8" height="22" rx="2" fill="#1e40af"/>
		<rect class="epc-sf-logo__bar" x="32" y="14" width="8" height="18" rx="2" fill="#d4af37"/>
	</svg>
	<span class="epc-sf-logo__text"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></span>
</span>
	<?php
	return ob_get_clean();
}

function epc_storefront_animated_logo_jewellery(string $label): string
{
	ob_start();
	?>
<span class="epc-sf-logo epc-sf-logo--jewellery" aria-label="<?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?>">
	<svg class="epc-sf-logo__mark" viewBox="0 0 44 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
		<circle cx="22" cy="22" r="14" fill="none" stroke="#d4af37" stroke-width="4"/>
		<path class="epc-sf-logo__spark" d="M22 6 L24 12 L30 12 L25 16 L27 22 L22 18 L17 22 L19 16 L14 12 L20 12 Z" fill="#fbbf24"/>
	</svg>
	<span class="epc-sf-logo__text"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></span>
</span>
	<?php
	return ob_get_clean();
}
