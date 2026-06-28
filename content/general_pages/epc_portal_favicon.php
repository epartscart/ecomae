<?php
/**
 * Per-tenant dynamic favicon system.
 *
 * Returns an inline SVG favicon based on the tenant's industry.
 * Called from the <head> section of desktop.php instead of the
 * hardcoded /favicon.svg path.
 *
 * Usage:
 *   require_once __DIR__ . '/epc_portal_favicon.php';
 *   $faviconSvg = epc_portal_favicon_svg($industryCode);
 *   // then in <head>:
 *   echo epc_portal_favicon_link_tags($industryCode);
 */
defined('_ASTEXE_') or die('No access');

function epc_portal_favicon_svg(string $industry): string
{
	switch ($industry) {
		case 'electronics':
			return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'
				. '<rect width="64" height="64" rx="14" fill="#1e40af"/>'
				. '<rect x="16" y="10" width="32" height="22" rx="3" fill="none" stroke="#fff" stroke-width="3"/>'
				. '<line x1="32" y1="32" x2="32" y2="38" stroke="#fff" stroke-width="3" stroke-linecap="round"/>'
				. '<line x1="24" y1="38" x2="40" y2="38" stroke="#fff" stroke-width="3" stroke-linecap="round"/>'
				. '<circle cx="32" cy="21" r="5" fill="none" stroke="#60a5fa" stroke-width="2"/>'
				. '<circle cx="32" cy="21" r="1.5" fill="#60a5fa"/>'
				. '<rect x="14" y="44" width="10" height="10" rx="2" fill="#3b82f6"/>'
				. '<rect x="27" y="44" width="10" height="10" rx="2" fill="#3b82f6"/>'
				. '<rect x="40" y="44" width="10" height="10" rx="2" fill="#3b82f6"/>'
				. '<text x="19" y="52" font-size="7" fill="#fff" font-family="sans-serif" font-weight="bold">E</text>'
				. '<text x="30" y="52" font-size="7" fill="#fff" font-family="sans-serif" font-weight="bold">A</text>'
				. '<text x="43" y="52" font-size="7" fill="#fff" font-family="sans-serif" font-weight="bold">E</text>'
				. '</svg>';

		case 'fashion':
			return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'
				. '<rect width="64" height="64" rx="14" fill="#7c3aed"/>'
				. '<path d="M32 8 L22 20 L26 20 L24 56 L40 56 L38 20 L42 20 Z" fill="none" stroke="#fff" stroke-width="2.5" stroke-linejoin="round"/>'
				. '<path d="M26 20 Q32 28 38 20" fill="none" stroke="#c4b5fd" stroke-width="2"/>'
				. '<circle cx="32" cy="12" r="2" fill="#c4b5fd"/>'
				. '<text x="18" y="61" font-size="7" fill="#e9d5ff" font-family="sans-serif" letter-spacing="1">SNL</text>'
				. '</svg>';

		case 'jewellery':
			return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'
				. '<rect width="64" height="64" rx="14" fill="#78350f"/>'
				. '<polygon points="32,8 20,24 12,24 32,56 52,24 44,24" fill="none" stroke="#fbbf24" stroke-width="2.5" stroke-linejoin="round"/>'
				. '<polygon points="32,8 26,24 38,24" fill="#fbbf24" opacity="0.3"/>'
				. '<line x1="12" y1="24" x2="52" y2="24" stroke="#fbbf24" stroke-width="2"/>'
				. '<line x1="26" y1="24" x2="32" y2="56" stroke="#fbbf24" stroke-width="1.5" opacity="0.5"/>'
				. '<line x1="38" y1="24" x2="32" y2="56" stroke="#fbbf24" stroke-width="1.5" opacity="0.5"/>'
				. '<text x="15" y="62" font-size="5.5" fill="#fde68a" font-family="serif" letter-spacing="0.5">TJT</text>'
				. '</svg>';

		case 'tax_advisory':
		case 'consultancy':
			return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'
				. '<rect width="64" height="64" rx="14" fill="#0f766e"/>'
				. '<rect x="14" y="12" width="36" height="44" rx="3" fill="none" stroke="#fff" stroke-width="2.5"/>'
				. '<line x1="22" y1="22" x2="42" y2="22" stroke="#5eead4" stroke-width="2" stroke-linecap="round"/>'
				. '<line x1="22" y1="29" x2="42" y2="29" stroke="#5eead4" stroke-width="2" stroke-linecap="round"/>'
				. '<line x1="22" y1="36" x2="36" y2="36" stroke="#5eead4" stroke-width="2" stroke-linecap="round"/>'
				. '<path d="M34 42 L38 46 L46 36" fill="none" stroke="#2dd4bf" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>'
				. '<text x="14" y="62" font-size="5.5" fill="#99f6e4" font-family="sans-serif" letter-spacing="0.5">TCA</text>'
				. '</svg>';

		case 'auto_parts':
		default:
			return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'
				. '<rect width="64" height="64" rx="14" fill="#fff"/>'
				. '<path d="M18 16h33c2.8 0 4.7 2.6 3.9 5.3L50 39H25L18 16Z" fill="none" stroke="#dc2626" stroke-linejoin="round" stroke-width="5"/>'
				. '<path d="M18 16h-7l-4-5" fill="none" stroke="#dc2626" stroke-linecap="round" stroke-linejoin="round" stroke-width="5"/>'
				. '<path d="M25 25h22l-3 9H28Z" fill="#dc2626"/>'
				. '<circle cx="27" cy="48" r="7" fill="#dc2626"/><circle cx="27" cy="48" r="4" fill="#fff"/>'
				. '<circle cx="50" cy="48" r="7" fill="#dc2626"/><circle cx="50" cy="48" r="4" fill="#fff"/>'
				. '</svg>';
	}
}

function epc_portal_favicon_link_tags(string $industry): string
{
	$svg = epc_portal_favicon_svg($industry);
	$encoded = 'data:image/svg+xml,' . rawurlencode($svg);
	return '<link rel="icon" type="image/svg+xml" href="' . $encoded . '"/>' . "\n"
		. '    <link rel="alternate icon" href="/favicon.ico?v=20260621"/>';
}
