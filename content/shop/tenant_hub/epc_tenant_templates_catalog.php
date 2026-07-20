<?php
/**
 * Super CP — industry template catalogue (show / apply / guide).
 *
 * Bridges /platform/industries demos with Tenant hub control:
 *   template_key → live hub + /cp/demo/{key} ERP → apply theme/package to a tenant.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../../general_pages/epc_industry_consolidation.php';
require_once __DIR__ . '/../../general_pages/epc_portal_storefront_packages.php';
require_once __DIR__ . '/../../general_pages/epc_portal_industry_live_bridge.php';
require_once __DIR__ . '/../../general_pages/epc_portal.php';

/**
 * Guide steps for Industry Templates control.
 *
 * @return list<array{title:string,body:string}>
 */
function epc_th_templates_guide_steps(): array
{
	return array(
		array(
			'title' => 'What is a template?',
			'body' => 'Each industry on <a href="https://www.ecomae.com/platform/industries" target="_blank" rel="noopener">/platform/industries</a> maps to a shared <strong>template key</strong> (e.g. <code>automotive</code>, <code>jewellery</code>). That key drives the public 3D hub (<code>{key}.ecomae.com</code>), the shared demo CP/ERP at <code>/cp/demo/{key}/</code>, and the default storefront chrome + ERP pack when you apply it to a client.',
		),
		array(
			'title' => 'Layers that get applied',
			'body' => '<ol style="margin:6px 0 0;padding-left:18px;">'
				. '<li><strong>Industry code</strong> — portal onboard code (auto_parts, jewellery, …)</li>'
				. '<li><strong>Theme + storefront package</strong> — colours, header/home, CP module packs</li>'
				. '<li><strong>ERP industry pack</strong> — costing, UOM, process flow (apply in client ERP → Setup after theme)</li>'
				. '<li><strong>Demo showcase</strong> — read-only shared demo; does not change client data until you Apply</li>'
				. '</ol>',
		),
		array(
			'title' => 'Preview before apply',
			'body' => 'Use <strong>Live hub</strong> for the marketing site, <strong>Demo CP</strong> for the control panel, and <strong>Demo ERP</strong> for the finance shell. Login for shared demos: <code>demo@ecomae.com</code> / <code>demo2026</code>.',
		),
		array(
			'title' => 'Apply to a tenant',
			'body' => 'On this Templates tab: pick a client from the list → <strong>Apply to tenant</strong>. That writes industry + theme + storefront package into portal site settings and (when live) syncs CP packs to the client database. Then open the client ERP → Setup → Industry pack and apply the suggested pack if fields/COA need seeding.',
		),
		array(
			'title' => 'New clients',
			'body' => 'Prefer <strong>Onboard client</strong> with the industry selected — that seeds the same template profile during registration. Use Templates when an existing tenant needs a restyle or industry switch.',
		),
		array(
			'title' => 'Where else to edit',
			'body' => 'Per-site fine tuning: Super CP → <em>Industry settings</em>. ERP field blueprints: client ERP → Tenant config / Setup. Fleet module packs (BOS): <code>/bos/</code> Industry packs. This Templates tab is the single <em>show + apply + guide</em> surface.',
		),
	);
}

/**
 * Portal industry codes that resolve to a given template_key via the live bridge.
 *
 * @return list<string>
 */
function epc_th_portal_codes_for_template(string $templateKey): array
{
	$tk = preg_replace('/[^a-z0-9_]/', '', strtolower($templateKey));
	$out = array();
	foreach (epc_portal_industry_live_defs() as $code => $def) {
		if (($def['template_key'] ?? '') === $tk) {
			$out[] = $code;
		}
	}
	return $out;
}

/**
 * Best default portal industry code for a consolidation template.
 */
function epc_th_default_industry_for_template(string $templateKey, array $group = array()): string
{
	$codes = epc_th_portal_codes_for_template($templateKey);
	// Prefer hub_root codes.
	$defs = epc_portal_industry_live_defs();
	foreach ($codes as $code) {
		if (($defs[$code]['mode'] ?? '') === 'hub_root') {
			return $code;
		}
	}
	if ($codes) {
		return $codes[0];
	}
	// Fallback from known map
	$map = array(
		'automotive' => 'auto_parts',
		'electronics' => 'electronics',
		'fashion' => 'fashion',
		'jewellery' => 'jewellery',
		'food_beverage' => 'food_beverage',
		'healthcare' => 'medical',
		'professional' => 'consultancy',
		'retail' => 'grocery_retail',
		'hospitality' => 'hospitality_travel',
		'education' => 'education_training',
		'logistics' => 'logistics_freight',
		'finance' => 'financial_services',
		'energy' => 'energy_utilities',
		'manufacturing' => 'manufacturing_industrial',
		'construction' => 'construction_contracting',
		'rental' => 'rental',
	);
	$tk = preg_replace('/[^a-z0-9_]/', '', strtolower($templateKey));
	return $map[$tk] ?? 'auto_parts';
}

/**
 * Full catalogue of industry templates for Super CP.
 *
 * @return list<array<string,mixed>>
 */
function epc_th_industry_templates_catalog(): array
{
	require_once __DIR__ . '/../../general_pages/epc_industry_seo.php';
	$groups = epc_industry_groups();
	$packages = epc_portal_storefront_package_registry();
	$portalIndustries = epc_portal_industries();
	$erpPacks = array();
	$erpFile = __DIR__ . '/../finance/epc_erp_industry_packs.php';
	if (is_file($erpFile)) {
		require_once $erpFile;
		if (function_exists('epc_erp_industry_packs')) {
			$erpPacks = epc_erp_industry_packs();
		}
	}

	$out = array();
	foreach ($groups as $gid => $g) {
		$tk = (string) ($g['template_key'] ?? $gid);
		$industryCode = epc_th_default_industry_for_template($tk, $g);
		$portalCodes = epc_th_portal_codes_for_template($tk);
		$pkgId = epc_portal_storefront_package_for_industry($industryCode);
		$pkg = $pkgId !== '' ? ($packages[$pkgId] ?? null) : null;
		$erpBase = (string) ($g['erp_base'] ?? '');
		$erpLabel = $erpBase !== '' && isset($erpPacks[$erpBase])
			? (string) ($erpPacks[$erpBase]['label'] ?? $erpBase)
			: $erpBase;

		$host = function_exists('epc_industry_seo_primary_host')
			? epc_industry_seo_primary_host($tk)
			: ($tk . '.ecomae.com');
		$liveUrl = 'https://' . $host . '/';
		$demoCp = 'https://www.ecomae.com/cp/demo/' . rawurlencode($tk) . '/';
		$demoErp = $demoCp . 'shop/finance/erp?epc_erp_shell=1';
		$platformUrl = 'https://www.ecomae.com/platform/industry/' . rawurlencode(str_replace('_', '-', $industryCode));

		$portalNames = array();
		foreach ($portalCodes as $code) {
			if (isset($portalIndustries[$code]['name'])) {
				$portalNames[] = (string) $portalIndustries[$code]['name'];
			} elseif (isset($portalIndustries[$code]['label'])) {
				$portalNames[] = (string) $portalIndustries[$code]['label'];
			} else {
				$portalNames[] = $code;
			}
		}

		$colors = isset($g['color_scheme']) && is_array($g['color_scheme']) ? $g['color_scheme'] : array();
		$out[] = array(
			'id' => $gid,
			'template_key' => $tk,
			'label' => (string) ($g['label'] ?? $tk),
			'description' => (string) ($g['description'] ?? ''),
			'icon' => (string) ($g['icon'] ?? 'fa-industry'),
			'primary' => (string) ($colors['primary'] ?? '#0f766e'),
			'accent' => (string) ($colors['accent'] ?? '#14b8a6'),
			'industry_code' => $industryCode,
			'portal_codes' => $portalCodes,
			'portal_names' => $portalNames,
			'storefront_package' => $pkgId,
			'storefront_label' => $pkg ? (string) ($pkg['label'] ?? $pkgId) : '',
			'theme_template' => $pkg ? (string) ($pkg['theme_template'] ?? 'classic') : 'classic',
			'erp_pack' => $erpBase,
			'erp_pack_label' => $erpLabel,
			'sub_areas' => isset($g['available_sub_areas']) && is_array($g['available_sub_areas'])
				? $g['available_sub_areas'] : array(),
			'live_url' => $liveUrl,
			'demo_cp_url' => $demoCp,
			'demo_erp_url' => $demoErp,
			'platform_url' => $platformUrl,
			'has_storefront_package' => $pkgId !== '',
		);
	}

	usort($out, function ($a, $b) {
		return strcasecmp((string) $a['label'], (string) $b['label']);
	});
	return $out;
}

/**
 * Standalone storefront packages (premium layouts) for the catalogue sidebar.
 *
 * @return list<array<string,mixed>>
 */
function epc_th_storefront_packages_catalog(): array
{
	$out = array();
	foreach (epc_portal_storefront_package_registry() as $id => $pkg) {
		$ind = !empty($pkg['industry_codes'][0]) ? (string) $pkg['industry_codes'][0] : '';
		$out[] = array(
			'id' => $id,
			'label' => (string) ($pkg['label'] ?? $id),
			'desc' => (string) ($pkg['desc'] ?? ''),
			'industry_code' => $ind,
			'theme_template' => (string) ($pkg['theme_template'] ?? 'classic'),
			'implemented' => !isset($pkg['implemented']) || $pkg['implemented'] !== false,
		);
	}
	return $out;
}

/**
 * Apply a catalogue template to a registered tenant (theme + package + industry).
 *
 * @return array{ok:bool,message:string,site_key?:string}
 */
function epc_th_apply_industry_template(PDO $db, string $siteKey, string $templateKey, array $opts = array()): array
{
	require_once __DIR__ . '/epc_tenant_hub_helpers.php';
	$tk = preg_replace('/[^a-z0-9_]/', '', strtolower($templateKey));
	if ($tk === '') {
		return array('ok' => false, 'message' => 'Invalid template key');
	}
	$catalog = epc_th_industry_templates_catalog();
	$card = null;
	foreach ($catalog as $row) {
		if ($row['template_key'] === $tk || $row['id'] === $tk) {
			$card = $row;
			break;
		}
	}
	if ($card === null) {
		return array('ok' => false, 'message' => 'Template not found in catalogue');
	}

	$industryCode = preg_replace('/[^a-z0-9_]/', '', (string) ($opts['industry_code'] ?? $card['industry_code']));
	if ($industryCode === '') {
		$industryCode = $card['industry_code'];
	}

	$applyOpts = array(
		'industry_code' => $industryCode,
		'push_client' => !isset($opts['push_client']) || !empty($opts['push_client']),
	);
	if (!empty($opts['storefront_package']) || !empty($card['storefront_package'])) {
		$applyOpts['storefront_package'] = (string) ($opts['storefront_package'] ?? $card['storefront_package']);
	}
	if (!empty($opts['theme_template']) || !empty($card['theme_template'])) {
		$applyOpts['theme_template'] = (string) ($opts['theme_template'] ?? $card['theme_template']);
	}

	$result = epc_th_apply_industry_theme($db, $siteKey, $applyOpts);
	if (empty($result['ok'])) {
		return $result;
	}

	// Persist ERP pack hint on portal site settings for operators / future sync.
	try {
		require_once __DIR__ . '/../../general_pages/epc_portal_db.php';
		$row = epc_portal_tenant_get($db, preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey)));
		if ($row && !empty($card['erp_pack'])) {
			$host = (string) ($row['hostname'] ?? '');
			if ($host !== '') {
				$saveHost = (strpos($host, 'www.') !== 0 && strpos($host, '.') !== false)
					? 'www.' . preg_replace('/^www\./', '', $host) : $host;
				$settings = epc_portal_load_site_settings_for_host($db, $saveHost);
				if (!is_array($settings)) {
					$settings = array();
				}
				$settings['erp_industry_pack'] = $card['erp_pack'];
				$settings['industry_template_key'] = $card['template_key'];
				$contact = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
				$contact['erp_industry_pack'] = $card['erp_pack'];
				$contact['industry_template_key'] = $card['template_key'];
				$settings['contact'] = $contact;
				epc_portal_save_site_settings($db, $settings);
			}
		}
	} catch (Throwable $e) {
		// non-fatal
	}

	$extra = '';
	if (!empty($card['erp_pack_label'])) {
		$extra = ' · Next: client ERP → Setup → apply pack “' . $card['erp_pack_label'] . '”';
	}
	$result['message'] = (string) ($result['message'] ?? 'Applied') . $extra;
	$result['template_key'] = $card['template_key'];
	$result['erp_pack'] = $card['erp_pack'];
	return $result;
}
