<?php
/**
 * Per-industry visual style templates (same modules/layout; colours only).
 * Four looks per vertical — stable IDs (classic, modern, midnight, signature)
 * with industry-specific display names (e.g. Racing red, Ocean blue).
 *
 * Full storefront chrome (header/footer/home packages) is separate from colour slots —
 * see epc_portal_storefront_packages.php and epc_theme_presets/*.json.
 *
 * Implemented packages (registry):
 *   - automotive_spareparts_pro — auto_parts + classic (piston hero, legacy SVG logo; NOT tenant brand)
 *   - electronics_retail_virgin — electronics + midnight (Virgin Megastore chrome)
 *   - consulting_primeinvest — tax_advisory|consultancy + modern (Prime Invest chrome)
 * Placeholders: fashion_retail_editorial (implemented=false). Jewellery: jewellery_retail_kiyasha.
 *
 * Onboard a new spare-parts tenant:
 *   1. Super CP Tenant hub: industry auto_parts, theme_template classic.
 *   2. In site_settings contact JSON set storefront_package=automotive_spareparts_pro,
 *      use_animated_hub_logo=false, use_tenant_brand=false (or apply preset JSON).
 *   3. Override colours only: site_settings.theme { primary, primary_dark, accent, hero_from, hero_to }
 *      or pick another visual style template (modern/midnight/signature) — layout stays automotive pro.
 *   4. Optional: merge epc_theme_presets/automotive_spareparts_pro.json via Industry Settings save.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

/** Stable template keys stored in DB (theme_template column). */
function epc_portal_theme_template_slot_ids()
{
	return array('classic', 'modern', 'midnight', 'signature');
}

/**
 * Deterministic default visual style per industry.
 * Keep this as the single source of truth for first-look storefront themes.
 */
function epc_portal_default_theme_template_by_industry()
{
	return array(
		'auto_parts' => 'classic',
		'tax_advisory' => 'modern',
		'electronics' => 'midnight',
		'fashion' => 'signature',
		'jewellery' => 'classic',
		'medical' => 'modern',
		'health' => 'signature',
		'consultancy' => 'modern',
		'rental' => 'classic',
		'erp_standalone' => 'classic',
		'platform_host' => 'classic',
	);
}

function epc_portal_default_theme_template($industryCode)
{
	$code = preg_replace('/[^a-z0-9_]/', '', (string) $industryCode);
	$map = epc_portal_default_theme_template_by_industry();
	$candidate = isset($map[$code]) ? (string) $map[$code] : 'classic';
	return epc_portal_normalize_theme_template($code !== '' ? $code : 'auto_parts', $candidate);
}

function epc_portal_theme_palette($primary, $primary_dark, $accent, $sidebar_from, $sidebar_to, $hero_from, $hero_to)
{
	return array(
		'primary' => $primary,
		'primary_dark' => $primary_dark,
		'accent' => $accent,
		'sidebar_from' => $sidebar_from,
		'sidebar_to' => $sidebar_to,
		'hero_from' => $hero_from,
		'hero_to' => $hero_to,
	);
}

/** Shorthand for palette + metadata. */
function epc_portal_theme_style($id, $label, $desc, array $palette)
{
	return array(
		'id' => $id,
		'label' => $label,
		'desc' => $desc,
		'theme' => $palette,
	);
}

function epc_portal_theme_palette_definitions()
{
	return array(
		'auto_parts' => array(
			epc_portal_theme_style('classic', 'Racing red', 'Bold motorsport red, charcoal nav, amber highlights', epc_portal_theme_palette('#dc2626', '#991b1b', '#f97316', '#0f172a', '#1e293b', '#0b1220', '#1e3a5f')),
			epc_portal_theme_style('modern', 'Carbon silver', 'Clean gunmetal grey with orange call-to-action', epc_portal_theme_palette('#ea580c', '#c2410c', '#fbbf24', '#1e293b', '#475569', '#0f172a', '#334155')),
			epc_portal_theme_style('midnight', 'Night track', 'Dark garage UI with neon red and cyan accents', epc_portal_theme_palette('#ef4444', '#b91c1c', '#22d3ee', '#020617', '#0f172a', '#020617', '#1e293b')),
			epc_portal_theme_style('signature', 'Torch amber', 'High-contrast amber primary for parts counters', epc_portal_theme_palette('#f59e0b', '#d97706', '#ef4444', '#292524', '#44403c', '#1c1917', '#78350f')),
		),
		'tax_advisory' => array(
			epc_portal_theme_style('classic', 'Trust teal', 'Professional teal — default for tax & advisory firms', epc_portal_theme_palette('#0d9488', '#0f766e', '#14b8a6', '#042f2e', '#134e4a', '#042f2e', '#115e59')),
			epc_portal_theme_style('modern', 'Ocean blue', 'Bright coastal blue, calm client-portal feel', epc_portal_theme_palette('#0891b2', '#0e7490', '#67e8f9', '#164e63', '#334155', '#0c4a6e', '#155e75')),
			epc_portal_theme_style('midnight', 'Midnight counsel', 'Deep green-black with gold trust accents', epc_portal_theme_palette('#2dd4bf', '#0d9488', '#fbbf24', '#022c22', '#064e3b', '#022c22', '#134e4a')),
			epc_portal_theme_style('signature', 'Sandstone gold', 'Warm stone neutrals with teal CTAs', epc_portal_theme_palette('#0f766e', '#115e59', '#d4a574', '#44403c', '#57534e', '#292524', '#134e4a')),
		),
		'fashion' => array(
			epc_portal_theme_style('classic', 'Rose couture', 'Magenta editorial — boutique fashion default', epc_portal_theme_palette('#be185d', '#9d174d', '#ec4899', '#1f1020', '#4a1942', '#1f1020', '#701a75')),
			epc_portal_theme_style('modern', 'Blush studio', 'Soft pink on cool grey — lookbook ready', epc_portal_theme_palette('#db2777', '#be185d', '#f9a8d4', '#374151', '#4b5563', '#1f1020', '#9d174d')),
			epc_portal_theme_style('midnight', 'Noir runway', 'Dark luxe with neon pink accent', epc_portal_theme_palette('#f472b6', '#db2777', '#e879f9', '#18181b', '#3f3f46', '#09090b', '#4a044e')),
			epc_portal_theme_style('signature', 'Lavender dusk', 'Muted violet hero with rose gold accent', epc_portal_theme_palette('#a855f7', '#7e22ce', '#f9a8d4', '#3b0764', '#581c87', '#1e1b4b', '#6b21a8')),
		),
		'electronics' => array(
			epc_portal_theme_style('classic', 'Circuit blue', 'Tech retail blue — trusted electronics look', epc_portal_theme_palette('#2563eb', '#1d4ed8', '#38bdf8', '#0f172a', '#1e3a8a', '#0f172a', '#1e40af')),
			epc_portal_theme_style('modern', 'Arctic steel', 'Light grey chrome with sky-blue highlights', epc_portal_theme_palette('#3b82f6', '#2563eb', '#7dd3fc', '#1e293b', '#475569', '#0f172a', '#1d4ed8')),
			epc_portal_theme_style('midnight', 'Virgin red', 'Bold retail red, black nav, white panels — megastore look', epc_portal_theme_palette('#e10a0a', '#b00808', '#000000', '#000000', '#1a1a1a', '#000000', '#2d2d2d')),
			epc_portal_theme_style('signature', 'Volt cyan', 'Cyan-forward gaming & gadget energy', epc_portal_theme_palette('#06b6d4', '#0891b2', '#a78bfa', '#083344', '#164e63', '#020617', '#312e81')),
		),
		'medical' => array(
			epc_portal_theme_style('classic', 'Clinical sky', 'Hospital-grade blue — clean and reassuring', epc_portal_theme_palette('#0284c7', '#0369a1', '#22d3ee', '#0c4a6e', '#164e63', '#0c4a6e', '#155e75')),
			epc_portal_theme_style('modern', 'Care blue', 'Soft sky panels for patient-friendly portals', epc_portal_theme_palette('#0ea5e9', '#0284c7', '#7dd3fc', '#1e3a8a', '#334155', '#0c4a6e', '#0369a1')),
			epc_portal_theme_style('midnight', 'ICU navy', 'Deep navy with mint safety accent', epc_portal_theme_palette('#38bdf8', '#0284c7', '#34d399', '#082f49', '#0c4a6e', '#042f2e', '#164e63')),
			epc_portal_theme_style('signature', 'Healing mint', 'Mint green primary for wellness suppliers', epc_portal_theme_palette('#14b8a6', '#0d9488', '#38bdf8', '#134e4a', '#115e59', '#042f2e', '#0e7490')),
		),
		'health' => array(
			epc_portal_theme_style('classic', 'Forest wellness', 'Natural green — supplements & lifestyle', epc_portal_theme_palette('#16a34a', '#15803d', '#4ade80', '#14532d', '#166534', '#14532d', '#15803d')),
			epc_portal_theme_style('modern', 'Fresh meadow', 'Bright leaf green on soft sage backgrounds', epc_portal_theme_palette('#22c55e', '#16a34a', '#86efac', '#1e3a2f', '#365314', '#14532d', '#166534')),
			epc_portal_theme_style('midnight', 'Deep zen', 'Dark forest UI with lime highlights', epc_portal_theme_palette('#4ade80', '#16a34a', '#a3e635', '#052e16', '#14532d', '#022c22', '#166534')),
			epc_portal_theme_style('signature', 'Citrus vitality', 'Lime & orange zest for active brands', epc_portal_theme_palette('#84cc16', '#65a30d', '#fb923c', '#365314', '#3f6212', '#1a2e05', '#15803d')),
		),
		'consultancy' => array(
			epc_portal_theme_style('classic', 'Royal purple', 'Consulting purple — authority and clarity', epc_portal_theme_palette('#7c3aed', '#6d28d9', '#a78bfa', '#2e1065', '#4c1d95', '#2e1065', '#5b21b6')),
			epc_portal_theme_style('modern', 'Slate advisory', 'Cool violet on professional grey', epc_portal_theme_palette('#8b5cf6', '#7c3aed', '#c4b5fd', '#312e81', '#4c1d95', '#1e1b4b', '#6d28d9')),
			epc_portal_theme_style('midnight', 'Executive noir', 'Dark boardroom with violet edge light', epc_portal_theme_palette('#a78bfa', '#7c3aed', '#f472b6', '#1e1b4b', '#312e81', '#0f0a1a', '#4c1d95')),
			epc_portal_theme_style('signature', 'Rose quartz', 'Soft pink accent for boutique consultancies', epc_portal_theme_palette('#db2777', '#be185d', '#a78bfa', '#4a044e', '#701a75', '#1e1b4b', '#6d28d9')),
		),
		'rental' => array(
			epc_portal_theme_style('classic', 'Fleet gold', 'Equipment rental — amber fleet identity', epc_portal_theme_palette('#ca8a04', '#a16207', '#facc15', '#422006', '#713f12', '#422006', '#854d0e')),
			epc_portal_theme_style('modern', 'Sandstone lease', 'Warm stone neutrals for property & assets', epc_portal_theme_palette('#eab308', '#ca8a04', '#fde047', '#44403c', '#57534e', '#292524', '#a16207')),
			epc_portal_theme_style('midnight', 'Charcoal fleet', 'Dark logistics with gold status chips', epc_portal_theme_palette('#facc15', '#ca8a04', '#fb923c', '#1c1917', '#422006', '#0c0a09', '#713f12')),
			epc_portal_theme_style('signature', 'Sunset hire', 'Orange sunset gradient heroes for bookings', epc_portal_theme_palette('#f97316', '#ea580c', '#facc15', '#7c2d12', '#9a3412', '#431407', '#c2410c')),
		),
		'jewellery' => array(
			epc_portal_theme_style('classic', 'Champagne gold', 'Luxury gold on black — jewellery default', epc_portal_theme_palette('#b45309', '#92400e', '#fbbf24', '#1c1917', '#44403c', '#1c1917', '#78350f')),
			epc_portal_theme_style('modern', 'Ivory luxe', 'Cream storefront with rich amber CTAs', epc_portal_theme_palette('#d97706', '#b45309', '#fcd34d', '#292524', '#44403c', '#1c1917', '#92400e')),
			epc_portal_theme_style('midnight', 'Onyx gallery', 'Pure black gallery with gold foil accent', epc_portal_theme_palette('#fbbf24', '#d97706', '#fef3c7', '#0c0a09', '#292524', '#000000', '#78350f')),
			epc_portal_theme_style('signature', 'Rose gold', 'Blush metal tone for fine jewellery houses', epc_portal_theme_palette('#e11d48', '#be123c', '#fbbf24', '#4c0519', '#881337', '#1c1917', '#9f1239')),
		),
		'platform_host' => array(
			epc_portal_theme_style('classic', 'Cloud cyan', 'ECOM AE operator cyan — platform default', epc_portal_theme_palette('#0ea5e9', '#0284c7', '#38bdf8', '#0c4a6e', '#075985', '#082f49', '#0c4a6e')),
			epc_portal_theme_style('modern', 'Horizon blue', 'Light SaaS blue for marketing & demos', epc_portal_theme_palette('#06b6d4', '#0891b2', '#67e8f9', '#164e63', '#0e7490', '#083344', '#155e75')),
			epc_portal_theme_style('midnight', 'Operator dark', 'Super CP dark shell with cyan glow', epc_portal_theme_palette('#22d3ee', '#0ea5e9', '#a5f3fc', '#020617', '#0c4a6e', '#020617', '#075985')),
			epc_portal_theme_style('signature', 'Aurora mint', 'Mint + cyan aurora for ecomae branding', epc_portal_theme_palette('#2dd4bf', '#14b8a6', '#38bdf8', '#042f2e', '#0f766e', '#020617', '#164e63')),
		),
	);
}

/**
 * @return array<string, array<string, array{label:string,desc:string,theme:array}>>
 */
function epc_portal_industry_style_templates()
{
	static $registry = null;
	if ($registry !== null) {
		return $registry;
	}

	require_once __DIR__ . '/epc_portal.php';
	$definitions = epc_portal_theme_palette_definitions();
	$industries = epc_portal_industries();
	$registry = array();

	foreach ($industries as $code => $ind) {
		$styles = isset($definitions[$code]) ? $definitions[$code] : epc_portal_quartet_from_base_theme(isset($ind['theme']) ? $ind['theme'] : array(), $ind['name']);
		$registry[$code] = array();
		foreach ($styles as $row) {
			$id = $row['id'];
			$registry[$code][$id] = array(
				'label' => $row['label'],
				'desc' => $row['desc'],
				'theme' => $row['theme'],
			);
		}
	}
	return $registry;
}

/** Four auto-generated looks when an industry has no hand-tuned palettes. */
function epc_portal_quartet_from_base_theme(array $base, $industryName = '')
{
	$p = isset($base['primary']) ? $base['primary'] : '#2563eb';
	$pd = isset($base['primary_dark']) ? $base['primary_dark'] : '#1d4ed8';
	$a = isset($base['accent']) ? $base['accent'] : '#38bdf8';
	$sf = isset($base['sidebar_from']) ? $base['sidebar_from'] : '#0f172a';
	$st = isset($base['sidebar_to']) ? $base['sidebar_to'] : '#1e293b';
	$hf = isset($base['hero_from']) ? $base['hero_from'] : '#0b1220';
	$ht = isset($base['hero_to']) ? $base['hero_to'] : '#1e3a5f';
	$name = trim((string) $industryName);
	$prefix = $name !== '' ? $name . ' — ' : '';

	return array(
		epc_portal_theme_style('classic', $prefix . 'Classic', 'Default brand colours for this industry', $base),
		epc_portal_theme_style('modern', $prefix . 'Bright', 'Lighter panels and stronger contrast', epc_portal_theme_palette($p, $pd, $a, '#334155', '#475569', $hf, $ht)),
		epc_portal_theme_style('midnight', $prefix . 'Midnight', 'Dark navigation with vivid accents', epc_portal_theme_palette($a, $p, $p, '#020617', $sf, '#020617', $st)),
		epc_portal_theme_style('signature', $prefix . 'Accent', 'Accent-led hero and sidebar highlights', epc_portal_theme_palette($a, $pd, $p, $sf, $st, $hf, $ht)),
	);
}

/** @deprecated Use epc_portal_quartet_from_base_theme */
function epc_portal_triplet_from_base_theme(array $base)
{
	$q = epc_portal_quartet_from_base_theme($base);
	return array(
		'classic' => $q[0]['theme'],
		'modern' => $q[1]['theme'],
		'midnight' => $q[2]['theme'],
	);
}

function epc_portal_style_templates_for_industry($industryCode)
{
	$code = preg_replace('/[^a-z0-9_]/', '', (string) $industryCode);
	$all = epc_portal_industry_style_templates();
	if (isset($all[$code])) {
		return $all[$code];
	}
	require_once __DIR__ . '/epc_portal.php';
	$ind = epc_portal_industry($code);
	$base = isset($ind['theme']) ? $ind['theme'] : array();
	$styles = epc_portal_quartet_from_base_theme($base, isset($ind['name']) ? $ind['name'] : '');
	$out = array();
	foreach ($styles as $row) {
		$out[$row['id']] = array(
			'label' => $row['label'],
			'desc' => $row['desc'],
			'theme' => $row['theme'],
		);
	}
	return $out;
}

function epc_portal_style_template_ids($industryCode)
{
	return array_keys(epc_portal_style_templates_for_industry($industryCode));
}

/** Legacy DB values → current slot id. */
function epc_portal_theme_template_aliases()
{
	return array(
		'default' => 'classic',
		'standard' => 'classic',
		'light' => 'modern',
		'dark' => 'midnight',
	);
}

function epc_portal_normalize_theme_template($industryCode, $templateId)
{
	$tid = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $templateId));
	$aliases = epc_portal_theme_template_aliases();
	if (isset($aliases[$tid])) {
		$tid = $aliases[$tid];
	}
	$allowed = epc_portal_style_template_ids($industryCode);
	if ($tid === '' || !in_array($tid, $allowed, true)) {
		return 'classic';
	}
	return $tid;
}

function epc_portal_style_template_theme($industryCode, $templateId)
{
	$tid = epc_portal_normalize_theme_template($industryCode, $templateId);
	$templates = epc_portal_style_templates_for_industry($industryCode);
	return isset($templates[$tid]['theme']) ? $templates[$tid]['theme'] : array();
}

function epc_portal_style_template_meta($industryCode, $templateId)
{
	$tid = epc_portal_normalize_theme_template($industryCode, $templateId);
	$templates = epc_portal_style_templates_for_industry($industryCode);
	if (!isset($templates[$tid])) {
		return array('label' => 'Classic', 'desc' => '');
	}
	return array(
		'label' => $templates[$tid]['label'],
		'desc' => $templates[$tid]['desc'],
	);
}

function epc_portal_style_templates_for_js()
{
	$out = array();
	foreach (epc_portal_industry_style_templates() as $industry => $templates) {
		$out[$industry] = array();
		foreach ($templates as $id => $row) {
			$out[$industry][$id] = array(
				'label' => $row['label'],
				'desc' => $row['desc'],
				'theme' => $row['theme'],
			);
		}
	}
	return $out;
}

function epc_portal_resolve_site_theme(array $settings)
{
	$code = isset($settings['industry_code']) ? $settings['industry_code'] : 'auto_parts';
	$tid = isset($settings['theme_template']) ? $settings['theme_template'] : 'classic';
	return epc_portal_style_template_theme($code, $tid);
}
