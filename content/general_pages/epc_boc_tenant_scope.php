<?php
/**
 * Super CP (BOC) tenant segregation.
 *
 * Mode A — Platform fleet (default on ecomae.com/cp): tenants, health, governance,
 *          fleet pricing AI. No per-shop OMS/catalogue mixed into the platform DB.
 * Mode B — Active tenant: Commerce/Catalogue/Logistics/ERP/Pro menus rewrite to that
 *          tenant's own CP host (epartscart.com/cp, taxofinca.com/cp, …).
 *
 * Switch: ?epc_boc_tenant={site_key}  Exit: ?epc_boc_exit_tenant=1
 */
defined('_ASTEXE_') or die('No access');

/** @return list<string> Platform-fleet groups (ecomae Super CP / overall CP). */
function epc_boc_platform_group_ids(): array
{
	return array(
		'command',
		'lifecycle',
		'reliability',
		'supply',
		'commerce', // fleet Pricing AI / API / POS overview
		'finance',
		'growth', // platform growth tools (tenant campaigns stripped separately)
		'identity',
		'platform',
		'knowledge',
	);
}

/** @return list<string> Tenant-CP module groups (require an active tenant). */
function epc_boc_tenant_group_ids(): array
{
	return array(
		'shop',
		'catalogue',
		'logistics',
		'erp',
		'professional',
	);
}

/**
 * Individual areas that live inside platform groups but must run on a tenant CP
 * (shop/ERP paths that hit the wrong DB when opened on ecomae).
 *
 * @return list<string>
 */
function epc_boc_tenant_area_ids(): array
{
	return array(
		'cp_marketing',
		'cp_seo',
		'erp_finance',
		'uae_tax',
		'insights_erp',
		'fulfillment_queue',
	);
}

/** Groups kept as a thin strip while operating in Tenant CP mode. */
function epc_boc_tenant_mode_platform_strip(): array
{
	return array('command', 'lifecycle', 'reliability');
}

function epc_boc_session_boot(): void
{
	if (session_status() === PHP_SESSION_NONE && !headers_sent()) {
		@session_start();
	}
}

/**
 * @return array{site_key:string,hostname:string,label:string,type:string,cp_url:string}|null
 */
function epc_boc_active_tenant(): ?array
{
	epc_boc_session_boot();
	$t = $_SESSION['epc_boc_active_tenant'] ?? null;
	if (!is_array($t)) {
		return null;
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($t['site_key'] ?? '')));
	$host = strtolower(trim((string) ($t['hostname'] ?? '')));
	if ($key === '' || $host === '') {
		return null;
	}
	$label = trim((string) ($t['label'] ?? $key));
	$type = (string) ($t['type'] ?? 'commerce');
	$cpUrl = trim((string) ($t['cp_url'] ?? ''));
	if ($cpUrl === '') {
		$cpUrl = 'https://www.' . $host . '/cp/';
	}
	return array(
		'site_key' => $key,
		'hostname' => $host,
		'label' => $label !== '' ? $label : $key,
		'type' => $type,
		'cp_url' => rtrim($cpUrl, '/') . '/',
	);
}

function epc_boc_set_active_tenant(array $tenant): void
{
	epc_boc_session_boot();
	$_SESSION['epc_boc_active_tenant'] = array(
		'site_key' => preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($tenant['site_key'] ?? ''))),
		'hostname' => strtolower(trim((string) ($tenant['hostname'] ?? ''))),
		'label' => trim((string) ($tenant['label'] ?? '')),
		'type' => (string) ($tenant['type'] ?? 'commerce'),
		'cp_url' => (string) ($tenant['cp_url'] ?? ''),
	);
}

function epc_boc_clear_active_tenant(): void
{
	epc_boc_session_boot();
	unset($_SESSION['epc_boc_active_tenant']);
}

/**
 * Build absolute CP URL on a tenant host (or demo/client-erp path on ecomae).
 */
function epc_boc_tenant_module_url(array $tenant, string $path): string
{
	$path = ltrim($path, '/');
	$type = (string) ($tenant['type'] ?? 'commerce');
	$key = (string) ($tenant['site_key'] ?? '');
	$host = (string) ($tenant['hostname'] ?? '');

	if ($type === 'demo' || strpos($key, 'demo_') === 0) {
		$base = rtrim((string) ($tenant['cp_url'] ?? ('/cp/demo/' . $key . '/')), '/') . '/';
		if (strpos($base, 'http') !== 0) {
			$base = 'https://www.ecomae.com' . (strpos($base, '/') === 0 ? $base : '/' . $base);
		}
		return $base . $path;
	}
	if ($type === 'erp_only') {
		return 'https://www.ecomae.com/cp/client-erp/' . rawurlencode($key) . '/' . $path;
	}
	if ($host === '') {
		return '/cp/' . $path;
	}
	return 'https://www.' . $host . '/cp/' . $path;
}

/**
 * @return list<array{site_key:string,hostname:string,label:string,type:string,cp_url:string,status:string}>
 */
function epc_boc_switcher_tenants(?PDO $pdo = null): array
{
	if (!($pdo instanceof PDO)) {
		global $db_link;
		$pdo = ($db_link instanceof PDO) ? $db_link : null;
	}
	if (!($pdo instanceof PDO)) {
		return array();
	}
	$out = array();
	try {
		if (is_file(__DIR__ . '/epc_bos_unified.php')) {
			require_once __DIR__ . '/epc_bos_unified.php';
		}
		if (function_exists('epc_bos_tenant_list')) {
			foreach (epc_bos_tenant_list($pdo) as $row) {
				$key = (string) ($row['site_key'] ?? '');
				$host = (string) ($row['hostname'] ?? '');
				if ($key === '' || $key === 'ecomae' || $host === '' || $host === 'ecomae.com' || $host === 'www.ecomae.com') {
					continue;
				}
				if ((string) ($row['type'] ?? '') === 'platform') {
					continue;
				}
				$type = (string) ($row['type'] ?? 'commerce');
				$label = trim((string) ($row['trade_name'] ?? ''));
				if ($label === '') {
					$label = trim((string) ($row['hub_name'] ?? ''));
				}
				if ($label === '') {
					$label = $key;
				}
				$cpUrl = 'https://www.' . preg_replace('/^www\./', '', $host) . '/cp/';
				if ($type === 'demo') {
					$cpUrl = 'https://www.ecomae.com/cp/demo/' . rawurlencode($key) . '/';
				} elseif ($type === 'erp_only') {
					$cpUrl = 'https://www.ecomae.com/cp/client-erp/' . rawurlencode($key) . '/';
				}
				$out[] = array(
					'site_key' => $key,
					'hostname' => preg_replace('/^www\./', '', $host),
					'label' => $label,
					'type' => $type,
					'cp_url' => $cpUrl,
					'status' => (string) ($row['status'] ?? ''),
				);
			}
			return $out;
		}
	} catch (Throwable $e) {
		// fall through
	}
	return $out;
}

/**
 * Apply ?epc_boc_tenant= / ?epc_boc_exit_tenant= once per request.
 */
function epc_boc_handle_tenant_switch(?PDO $pdo = null): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		return;
	}
	if (!empty($_GET['epc_boc_exit_tenant'])) {
		epc_boc_clear_active_tenant();
		return;
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['epc_boc_tenant'] ?? ''))));
	if ($key === '') {
		return;
	}
	foreach (epc_boc_switcher_tenants($pdo) as $t) {
		if ($t['site_key'] === $key) {
			epc_boc_set_active_tenant($t);
			return;
		}
	}
}

/**
 * Filter + rewrite BOC nav for platform vs active-tenant mode.
 *
 * @param array<string,mixed> $nav
 * @return array<string,mixed>
 */
function epc_boc_nav_apply_tenant_scope(array $nav, ?array $tenant = null): array
{
	if ($tenant === null) {
		$tenant = epc_boc_active_tenant();
	}
	$tenantGroupIds = array_flip(epc_boc_tenant_group_ids());
	$tenantAreaIds = array_flip(epc_boc_tenant_area_ids());
	$stripIds = array_flip(epc_boc_tenant_mode_platform_strip());
	$out = array();

	foreach ($nav as $gid => $g) {
		$gid = (string) $gid;
		$areas = (array) ($g['areas'] ?? array());

		if ($tenant === null) {
			// Platform fleet: hide tenant shop groups; strip tenant-only areas from platform groups.
			if (isset($tenantGroupIds[$gid])) {
				continue;
			}
			foreach ($areas as $id => $area) {
				if (isset($tenantAreaIds[(string) $id])) {
					unset($areas[$id]);
				}
			}
			if ($areas === []) {
				continue;
			}
			$g['areas'] = $areas;
			$out[$gid] = $g;
			continue;
		}

		// Tenant mode: thin platform strip + tenant CP groups (URLs on tenant host).
		if (!isset($tenantGroupIds[$gid]) && !isset($stripIds[$gid])) {
			// Keep mixed platform groups only when they still have tenant-area links to rewrite.
			$keep = array();
			foreach ($areas as $id => $area) {
				if (isset($tenantAreaIds[(string) $id])) {
					$path = (string) ($area['path'] ?? '');
					$area['url_override'] = epc_boc_tenant_module_url($tenant, $path);
					$area['hint'] = trim((string) ($area['hint'] ?? '') . ' · ' . $tenant['label']);
					$keep[$id] = $area;
				}
			}
			if ($keep === []) {
				continue;
			}
			$g['areas'] = $keep;
			$g['group']['label'] = (string) (($g['group']['label'] ?? $gid) . ' · ' . $tenant['label']);
			$out[$gid] = $g;
			continue;
		}

		if (isset($tenantGroupIds[$gid])) {
			foreach ($areas as $id => $area) {
				$path = (string) ($area['path'] ?? '');
				$area['url_override'] = epc_boc_tenant_module_url($tenant, $path);
				$area['hint'] = trim((string) ($area['hint'] ?? '') . ' · ' . $tenant['label']);
				$areas[$id] = $area;
			}
			$g['areas'] = $areas;
			$label = (string) ($g['group']['label'] ?? $gid);
			$g['group']['label'] = $label . ' · ' . $tenant['label'];
		}
		$out[$gid] = $g;
	}

	return $out;
}

function epc_boc_scope_label(?array $tenant = null): string
{
	if ($tenant === null) {
		$tenant = epc_boc_active_tenant();
	}
	if ($tenant === null) {
		return 'Platform · All tenants';
	}
	return 'Tenant · ' . $tenant['label'];
}

/** Resolve href for a BOC area (honours tenant url_override). */
function epc_boc_area_href(array $area, string $base): string
{
	if (!empty($area['url_override'])) {
		return (string) $area['url_override'];
	}
	return rtrim($base, '/') . '/' . ltrim((string) ($area['path'] ?? ''), '/');
}

/**
 * HTML for tenant switcher (topnav actions).
 */
function epc_boc_render_tenant_switcher_html(?PDO $pdo = null): string
{
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		return '';
	}
	epc_boc_handle_tenant_switch($pdo);
	$active = epc_boc_active_tenant();
	$tenants = epc_boc_switcher_tenants($pdo);
	if ($tenants === []) {
		return '';
	}
	$h = static function ($v): string {
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	};
	$reqPath = (string) (parse_url((string) ($_SERVER['REQUEST_URI'] ?? '/cp/control'), PHP_URL_PATH) ?? '/cp/control');
	$self = $reqPath !== '' ? $reqPath : '/cp/control';

	$html = '<div class="epc-boc__tenant-switch" data-epc-boc-tenant-switch="1">';
	$html .= '<button type="button" class="epc-boc__tenant-switch-btn" data-boc-tenant-toggle="1" aria-expanded="false">';
	$html .= '<i class="fa fa-building"></i> ';
	if ($active) {
		$html .= '<span class="epc-boc__tenant-switch-label">' . $h($active['label']) . '</span>';
	} else {
		$html .= '<span class="epc-boc__tenant-switch-label">Select tenant CP</span>';
	}
	$html .= ' <i class="fa fa-caret-down"></i></button>';
	$html .= '<div class="epc-boc__tenant-switch-panel" hidden>';
	$html .= '<div class="epc-boc__tenant-switch-head">Operate as tenant CP</div>';
	$html .= '<a class="epc-boc__tenant-switch-item' . ($active ? '' : ' is-active') . '" href="' . $h($self . '?epc_boc_exit_tenant=1') . '"><i class="fa fa-globe"></i> Platform fleet (ecomae)</a>';
	foreach ($tenants as $t) {
		$isOn = $active && $active['site_key'] === $t['site_key'];
		$href = $self . '?epc_boc_tenant=' . rawurlencode($t['site_key']);
		$html .= '<a class="epc-boc__tenant-switch-item' . ($isOn ? ' is-active' : '') . '" href="' . $h($href) . '">';
		$html .= '<i class="fa fa-external-link"></i> ' . $h($t['label']);
		$html .= '<small>' . $h($t['hostname']) . ' · ' . $h($t['type']) . '</small>';
		$html .= '</a>';
		if ($isOn) {
			$html .= '<a class="epc-boc__tenant-switch-open" href="' . $h($t['cp_url']) . '" target="_blank" rel="noopener">Open full tenant CP ↗</a>';
		}
	}
	$html .= '</div></div>';
	return $html;
}
