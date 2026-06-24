<?php
/**
 * Design Tokens — tenant-scoped CSS custom properties for branded portal UX.
 *
 * Each tenant can store branding overrides in the registry (epc_settings) or
 * the platform hub (epc_portal_tenants). This file resolves the effective
 * token set and emits CSS vars on :root for the login, CP shell, and ERP.
 *
 * Token hierarchy: tenant DB setting → platform registry → static catalog → defaults
 */
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

/* ─────────────────── Default Token Set ─────────────────── */

function epc_design_tokens_defaults(): array
{
	return array(
		'--epc-brand-primary'    => '#0d6efd',
		'--epc-brand-secondary'  => '#6c757d',
		'--epc-brand-accent'     => '#0dcaf0',
		'--epc-brand-success'    => '#198754',
		'--epc-brand-warning'    => '#ffc107',
		'--epc-brand-danger'     => '#dc3545',
		'--epc-brand-bg'         => '#ffffff',
		'--epc-brand-bg-dark'    => '#0f172a',
		'--epc-brand-text'       => '#212529',
		'--epc-brand-text-muted' => '#6c757d',
		'--epc-brand-border'     => '#dee2e6',
		'--epc-brand-radius'     => '0.375rem',
		'--epc-brand-font'       => "'Inter', 'Segoe UI', system-ui, sans-serif",
		'--epc-brand-logo-url'   => '',
		'--epc-brand-logo-height' => '48px',
		'--epc-brand-login-bg'   => 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
		'--epc-brand-login-card' => 'rgba(255,255,255,0.05)',
		'--epc-brand-sidebar-bg' => '#1e293b',
		'--epc-brand-sidebar-text' => '#e2e8f0',
		'--epc-brand-header-bg'  => '#ffffff',
		'--epc-brand-header-text' => '#1e293b',
	);
}

/* ─────────────────── Industry Presets ─────────────────── */

function epc_design_tokens_industry_presets(): array
{
	return array(
		'auto_parts' => array(
			'--epc-brand-primary'   => '#dc2626',
			'--epc-brand-accent'    => '#f59e0b',
			'--epc-brand-bg-dark'   => '#18181b',
			'--epc-brand-login-bg'  => 'linear-gradient(135deg, #18181b 0%, #27272a 50%, #dc2626 200%)',
		),
		'fashion' => array(
			'--epc-brand-primary'   => '#ec4899',
			'--epc-brand-accent'    => '#a855f7',
			'--epc-brand-bg-dark'   => '#1a0a1e',
			'--epc-brand-login-bg'  => 'linear-gradient(135deg, #1a0a1e 0%, #2d1b3d 50%, #ec4899 200%)',
		),
		'jewellery' => array(
			'--epc-brand-primary'   => '#d97706',
			'--epc-brand-accent'    => '#f59e0b',
			'--epc-brand-bg-dark'   => '#1c1917',
			'--epc-brand-login-bg'  => 'linear-gradient(135deg, #1c1917 0%, #292524 50%, #d97706 200%)',
		),
		'electronics' => array(
			'--epc-brand-primary'   => '#e10a0a',
			'--epc-brand-accent'    => '#3b82f6',
			'--epc-brand-bg-dark'   => '#0c0a09',
			'--epc-brand-login-bg'  => 'linear-gradient(135deg, #0c0a09 0%, #1c1917 50%, #e10a0a 200%)',
		),
		'accounting' => array(
			'--epc-brand-primary'   => '#227a40',
			'--epc-brand-accent'    => '#10b981',
			'--epc-brand-bg-dark'   => '#0f1f17',
			'--epc-brand-login-bg'  => 'linear-gradient(135deg, #0f1f17 0%, #1a3a2a 50%, #227a40 200%)',
		),
		'real_estate' => array(
			'--epc-brand-primary'   => '#2563eb',
			'--epc-brand-accent'    => '#0ea5e9',
			'--epc-brand-bg-dark'   => '#0c1929',
			'--epc-brand-login-bg'  => 'linear-gradient(135deg, #0c1929 0%, #1e3a5f 50%, #2563eb 200%)',
		),
		'hr_only' => array(
			'--epc-brand-primary'   => '#7c3aed',
			'--epc-brand-accent'    => '#a78bfa',
			'--epc-brand-bg-dark'   => '#1a0f2e',
			'--epc-brand-login-bg'  => 'linear-gradient(135deg, #1a0f2e 0%, #2d1b69 50%, #7c3aed 200%)',
		),
	);
}

/* ─────────────────── Tenant Token Catalog (static) ─────────────────── */

function epc_design_tokens_tenant_catalog(): array
{
	return array(
		'epartscart' => array(
			'--epc-brand-primary'     => '#dc2626',
			'--epc-brand-accent'      => '#f59e0b',
			'--epc-brand-logo-url'    => '/content/files/images/ecomae-platform/assets/epartscart.png',
			'--epc-brand-login-bg'    => 'linear-gradient(135deg, #18181b 0%, #27272a 50%, #dc2626 200%)',
			'_trade_name'             => 'eParts Cart',
			'_tagline'                => 'Auto Parts UAE',
			'_industry'               => 'auto_parts',
		),
		'taxofinca' => array(
			'--epc-brand-primary'     => '#227a40',
			'--epc-brand-accent'      => '#10b981',
			'--epc-brand-logo-url'    => '/content/files/images/ecomae-platform/assets/taxofinca.png',
			'--epc-brand-login-bg'    => 'linear-gradient(135deg, #0f1f17 0%, #1a3a2a 50%, #227a40 200%)',
			'_trade_name'             => 'TaxoFinca',
			'_tagline'                => 'Tax & Accounting Solutions',
			'_industry'               => 'accounting',
		),
		'electronicae' => array(
			'--epc-brand-primary'     => '#e10a0a',
			'--epc-brand-accent'      => '#3b82f6',
			'--epc-brand-logo-url'    => '/content/files/images/ecomae-platform/assets/electronicae.png',
			'--epc-brand-login-bg'    => 'linear-gradient(135deg, #0c0a09 0%, #1c1917 50%, #e10a0a 200%)',
			'_trade_name'             => 'Electronicae',
			'_tagline'                => 'Tech Gaming UAE',
			'_industry'               => 'electronics',
		),
		'stylenlook' => array(
			'--epc-brand-primary'     => '#ec4899',
			'--epc-brand-accent'      => '#a855f7',
			'--epc-brand-logo-url'    => '/content/files/images/ecomae-platform/assets/stylenlook.png',
			'--epc-brand-login-bg'    => 'linear-gradient(135deg, #1a0a1e 0%, #2d1b3d 50%, #ec4899 200%)',
			'_trade_name'             => 'Stylenlook',
			'_tagline'                => 'Fashion & Beauty',
			'_industry'               => 'fashion',
		),
		'thejewellerytrend' => array(
			'--epc-brand-primary'     => '#d97706',
			'--epc-brand-accent'      => '#f59e0b',
			'--epc-brand-logo-url'    => '/content/files/images/ecomae-platform/assets/thejewellerytrend.png',
			'--epc-brand-login-bg'    => 'linear-gradient(135deg, #1c1917 0%, #292524 50%, #d97706 200%)',
			'_trade_name'             => 'The Jewellery Trend',
			'_tagline'                => 'Style Sparkle Shine',
			'_industry'               => 'jewellery',
		),
		'spare247' => array(
			'--epc-brand-primary'     => '#dc2626',
			'--epc-brand-accent'      => '#ef4444',
			'_trade_name'             => 'Spare247',
			'_tagline'                => 'Auto Parts Express',
			'_industry'               => 'auto_parts',
		),
		'asapcustom' => array(
			'--epc-brand-primary'     => '#0d6efd',
			'--epc-brand-accent'      => '#0dcaf0',
			'_trade_name'             => 'ASAP Custom',
			'_tagline'                => 'Custom Auto Parts',
			'_industry'               => 'auto_parts',
		),
	);
}

/* ─────────────────── Resolve Tokens ─────────────────── */

function epc_design_tokens_resolve(string $siteKey = '', string $industry = ''): array
{
	$tokens = epc_design_tokens_defaults();

	// Layer 1: Industry preset
	if ($industry !== '') {
		$presets = epc_design_tokens_industry_presets();
		if (isset($presets[$industry])) {
			foreach ($presets[$industry] as $k => $v) {
				$tokens[$k] = $v;
			}
		}
	}

	// Layer 2: Static tenant catalog
	if ($siteKey !== '') {
		$catalog = epc_design_tokens_tenant_catalog();
		if (isset($catalog[$siteKey])) {
			foreach ($catalog[$siteKey] as $k => $v) {
				if (strpos($k, '--') === 0) {
					$tokens[$k] = $v;
				}
			}
		}
	}

	// Layer 3: DB overrides (tenant settings)
	$dbOverrides = epc_design_tokens_db_overrides($siteKey);
	foreach ($dbOverrides as $k => $v) {
		if (strpos($k, '--') === 0 && $v !== '') {
			$tokens[$k] = $v;
		}
	}

	return $tokens;
}

function epc_design_tokens_db_overrides(string $siteKey): array
{
	if ($siteKey === '') {
		return array();
	}

	try {
		if (!function_exists('epc_portal_platform_pdo')) {
			return array();
		}
		$pdo = epc_portal_platform_pdo();
		if (!$pdo) { return array(); }

		$st = $pdo->prepare(
			'SELECT `setting_key`, `setting_value` FROM `epc_settings`
			 WHERE `site_key` = ? AND `setting_key` LIKE \'brand_%\' AND `setting_value` != \'\''
		);
		$st->execute(array($siteKey));
		$rows = $st->fetchAll(PDO::FETCH_KEY_PAIR);

		$map = array(
			'brand_primary'    => '--epc-brand-primary',
			'brand_secondary'  => '--epc-brand-secondary',
			'brand_accent'     => '--epc-brand-accent',
			'brand_bg_dark'    => '--epc-brand-bg-dark',
			'brand_logo_url'   => '--epc-brand-logo-url',
			'brand_login_bg'   => '--epc-brand-login-bg',
			'brand_font'       => '--epc-brand-font',
			'brand_radius'     => '--epc-brand-radius',
			'brand_sidebar_bg' => '--epc-brand-sidebar-bg',
			'brand_header_bg'  => '--epc-brand-header-bg',
		);

		$overrides = array();
		foreach ($rows as $key => $val) {
			if (isset($map[$key])) {
				$overrides[$map[$key]] = $val;
			}
		}
		return $overrides;
	} catch (\Exception $e) {
		return array();
	}
}

/* ─────────────────── Tenant Metadata ─────────────────── */

function epc_design_tokens_tenant_meta(string $siteKey): array
{
	$catalog = epc_design_tokens_tenant_catalog();
	$entry = $catalog[$siteKey] ?? array();
	return array(
		'trade_name' => (string) ($entry['_trade_name'] ?? ucfirst($siteKey)),
		'tagline'    => (string) ($entry['_tagline'] ?? ''),
		'industry'   => (string) ($entry['_industry'] ?? ''),
		'logo_url'   => (string) ($entry['--epc-brand-logo-url'] ?? ''),
	);
}

/* ─────────────────── CSS Output ─────────────────── */

function epc_design_tokens_css(string $siteKey = '', string $industry = ''): string
{
	$tokens = epc_design_tokens_resolve($siteKey, $industry);
	$lines = array(':root {');
	foreach ($tokens as $prop => $val) {
		if (strpos($prop, '--') !== 0) { continue; }
		$lines[] = '  ' . $prop . ': ' . $val . ';';
	}
	$lines[] = '}';
	return implode("\n", $lines);
}

function epc_design_tokens_style_tag(string $siteKey = '', string $industry = ''): string
{
	$css = epc_design_tokens_css($siteKey, $industry);
	return '<style id="epc-design-tokens">' . "\n" . $css . "\n" . '</style>';
}

/* ─────────────────── Branded Login Markup ─────────────────── */

function epc_design_tokens_login_brand(string $siteKey): string
{
	$meta = epc_design_tokens_tenant_meta($siteKey);
	$tokens = epc_design_tokens_resolve($siteKey, $meta['industry']);

	$logoUrl = $tokens['--epc-brand-logo-url'] ?? '';
	$tradeName = $meta['trade_name'];
	$tagline = $meta['tagline'];
	$primary = $tokens['--epc-brand-primary'] ?? '#0d6efd';

	ob_start();
	?>
	<div class="epc-login-brand" style="text-align:center;padding:2rem 1rem 1rem;">
		<?php if ($logoUrl !== '') { ?>
		<img src="<?php echo htmlspecialchars($logoUrl, ENT_QUOTES, 'UTF-8'); ?>"
			 alt="<?php echo htmlspecialchars($tradeName, ENT_QUOTES, 'UTF-8'); ?>"
			 style="max-width:280px;max-height:80px;margin-bottom:1rem;" />
		<?php } else { ?>
		<h2 style="color:<?php echo htmlspecialchars($primary, ENT_QUOTES, 'UTF-8'); ?>;font-size:1.5rem;margin-bottom:0.25rem;">
			<?php echo htmlspecialchars($tradeName, ENT_QUOTES, 'UTF-8'); ?>
		</h2>
		<?php } ?>
		<?php if ($tagline !== '') { ?>
		<p style="color:rgba(255,255,255,0.6);font-size:0.85rem;margin:0.25rem 0 0;">
			<?php echo htmlspecialchars($tagline, ENT_QUOTES, 'UTF-8'); ?>
		</p>
		<?php } ?>
	</div>
	<?php
	return ob_get_clean();
}

/* ─────────────────── "Hosted by" Control ─────────────────── */

function epc_design_tokens_white_label_enabled(string $siteKey): bool
{
	try {
		if (!function_exists('epc_portal_platform_pdo')) { return false; }
		$pdo = epc_portal_platform_pdo();
		if (!$pdo) { return false; }
		$st = $pdo->prepare(
			'SELECT `setting_value` FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'white_label_login\' LIMIT 1'
		);
		$st->execute(array($siteKey));
		return (string) $st->fetchColumn() === '1';
	} catch (\Exception $e) {
		return false;
	}
}

/* ─────────────────── API: Save Brand Setting ─────────────────── */

function epc_design_tokens_save(PDO $pdo, string $siteKey, string $settingKey, string $value): bool
{
	$allowed = array(
		'brand_primary', 'brand_secondary', 'brand_accent', 'brand_bg_dark',
		'brand_logo_url', 'brand_login_bg', 'brand_font', 'brand_radius',
		'brand_sidebar_bg', 'brand_header_bg', 'white_label_login',
	);
	if (!in_array($settingKey, $allowed, true)) {
		return false;
	}

	$st = $pdo->prepare(
		'INSERT INTO `epc_settings` (`site_key`, `setting_key`, `setting_value`, `updated_at`)
		 VALUES (?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `updated_at` = VALUES(`updated_at`)'
	);
	return $st->execute(array($siteKey, $settingKey, $value, time()));
}

function epc_design_tokens_list(PDO $pdo, string $siteKey): array
{
	$st = $pdo->prepare(
		'SELECT `setting_key`, `setting_value` FROM `epc_settings`
		 WHERE `site_key` = ? AND (`setting_key` LIKE \'brand_%\' OR `setting_key` = \'white_label_login\')'
	);
	$st->execute(array($siteKey));
	return $st->fetchAll(PDO::FETCH_KEY_PAIR);
}
