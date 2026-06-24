<?php
/**
 * BOS — Business Operating System
 * Unified entry point: ecomae.com/bos/
 */
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }
if (!defined('EPC_BOS_ENTRY')) { define('EPC_BOS_ENTRY', true); }
if (PHP_SAPI !== 'cli') { @set_time_limit(30); }

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
if (!isset($DP_Config)) { $DP_Config = new DP_Config(); }

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bos_unified.php';

// Security headers (nginx ignores .htaccess so we set them in PHP)
header('X-Robots-Tag: noindex, nofollow, noarchive');
header('X-Content-Type-Options: nosniff');
header('X-Frame-Options: DENY');
header('Referrer-Policy: strict-origin-when-cross-origin');
header('Permissions-Policy: geolocation=(), microphone=(), camera=()');
header("Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com; font-src 'self' https://cdnjs.cloudflare.com; img-src 'self' data:; frame-src https:; frame-ancestors 'none'");

epc_bos_session_start();

$bosAction = isset($_GET['action']) ? trim($_GET['action']) : '';

if ($bosAction === 'ajax') {
    require_once __DIR__ . '/ajax_epc_bos.php';
    exit;
}

$ctx = epc_bos_context();
$isLoggedIn = $ctx['role'] !== 'guest' && $ctx['user_id'] > 0;

if ($bosAction === 'login' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    require_once __DIR__ . '/ajax_epc_bos.php';
    exit;
}

if ($bosAction === 'logout') {
    $_SESSION = array();
    if (ini_get('session.use_cookies')) {
        $p = session_get_cookie_params();
        setcookie(session_name(), '', time() - 42000, $p['path'], $p['domain'], $p['secure'], $p['httponly']);
    }
    session_destroy();
    header('Location: /bos/');
    exit;
}

$activeTenant = isset($_GET['t']) ? preg_replace('/[^a-z0-9_]/', '', strtolower(trim($_GET['t']))) : epc_bos_active_tenant_key();
$activeModule = isset($_GET['m']) ? trim($_GET['m']) : 'command_center';

$platformPdo = null;
$tenants = array();
$sections = array();
$tenantInfo = null;
$tenantCountryProfile = null;

if ($isLoggedIn) {
    try {
        $platformPdo = epc_portal_platform_operator_pdo();
    } catch (Exception $e) {
        $platformPdo = null;
    }
    if ($platformPdo) {
        if (epc_bos_is_provider()) {
            $tenants = epc_bos_tenant_list($platformPdo);
        }
        if ($activeTenant !== '') {
            epc_bos_set_context(array_merge($ctx, array('tenant_key' => $activeTenant)));
            $tenantSettings = epc_bos_load_tenant_settings($platformPdo, $activeTenant);
            if ($tenantSettings) {
                $tenantInfo = array(
                    'site_key' => $activeTenant,
                    'industry_code' => $tenantSettings['industry_code'] ?? '',
                    'trade_name' => $tenantSettings['trade_name'] ?? $activeTenant,
                    'hostname' => $tenantSettings['hostname'] ?? '',
                );
                $tenantConn = epc_bos_tenant_connect($platformPdo, $activeTenant);
                if ($tenantConn['pdo'] !== null) {
                    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';
                    $country = '';
                    try {
                        $coSt = $tenantConn['pdo']->query("SELECT `value` FROM `epc_co_profile` WHERE `field` = 'country' LIMIT 1");
                        $coRow = $coSt ? $coSt->fetch(PDO::FETCH_ASSOC) : null;
                        $country = $coRow ? (string) $coRow['value'] : '';
                    } catch (Exception $e) {
                        $country = '';
                    }
                    if ($country !== '') {
                        $tenantCountryProfile = epc_country_profile($country);
                    }
                }
            }
        }
        $sections = epc_bos_sidebar_sections($platformPdo, $activeTenant);
        if (!epc_bos_is_provider()) {
            $sections = array_filter($sections, function($s) { return empty($s['provider_only']); });
        }
    }
}

// Resolve active module metadata
$activeModuleInfo = null;
$isModuleView = false;
if ($activeModule !== '' && $activeModule !== 'command_center' && !empty($sections)) {
    $activeModuleInfo = epc_bos_resolve_module($sections, $activeModule);
    $isModuleView = ($activeModuleInfo !== null);
}

$tenantsJson = json_encode($tenants, JSON_UNESCAPED_UNICODE);
$sectionsJson = json_encode(array_values($sections), JSON_UNESCAPED_UNICODE);
$tenantInfoJson = json_encode($tenantInfo, JSON_UNESCAPED_UNICODE);
$countryProfileJson = json_encode($tenantCountryProfile, JSON_UNESCAPED_UNICODE);
$industries = epc_portal_industries();
$industriesJson = json_encode($industries, JSON_UNESCAPED_UNICODE);

?><!DOCTYPE html>
<html lang="en" dir="ltr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex, nofollow">
<title>BOS &mdash; Business Operating System</title>
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css" integrity="sha256-eZrrJcwDc/3uDhsdt61sL2oOBY362qM3lon1gyExkL0=" crossorigin="anonymous">
<link rel="stylesheet" href="/epc-static.php?f=bos/epc_bos_shell.css&v=<?php echo EPC_BOS_VERSION; ?>">
</head>
<body class="bos-body<?php echo !$isLoggedIn ? ' bos-body--login' : ''; ?><?php echo $tenantCountryProfile && ($tenantCountryProfile['dir'] ?? '') === 'rtl' ? ' bos-rtl' : ''; ?>">

<?php if (!$isLoggedIn): ?>
<!-- ═══════════════════ LOGIN SCREEN ═══════════════════ -->
<div class="bos-login">
    <!-- Animated background -->
    <div class="bos-login__bg">
        <div class="bos-login__grid-lines"></div>
        <div class="bos-login__particles" id="bosParticles"></div>
        <div class="bos-login__glow bos-login__glow--1"></div>
        <div class="bos-login__glow bos-login__glow--2"></div>
        <div class="bos-login__glow bos-login__glow--3"></div>
    </div>

    <!-- Left side: branding & capabilities -->
    <div class="bos-login__hero">
        <div class="bos-login__hero-content">
            <div class="bos-login__brand-mark">
                <div class="bos-login__brand-icon">
                    <div class="bos-login__brand-icon-inner">
                        <i class="fa fa-th-large"></i>
                    </div>
                    <div class="bos-login__brand-ring"></div>
                    <div class="bos-login__brand-ring bos-login__brand-ring--2"></div>
                </div>
                <h1 class="bos-login__brand-title">BOS</h1>
                <p class="bos-login__brand-tagline">Business Operating System</p>
            </div>

            <div class="bos-login__capabilities">
                <div class="bos-login__cap-item bos-login__cap-item--1">
                    <div class="bos-login__cap-icon bos-login__cap-icon--erp">
                        <svg width="28" height="28" viewBox="0 0 28 28" fill="none"><rect x="3" y="6" width="22" height="16" rx="2" fill="url(#erpG)"/><rect x="6" y="9" width="6" height="4" rx="1" fill="rgba(255,255,255,.9)"/><rect x="14" y="9" width="8" height="2" rx="1" fill="rgba(255,255,255,.6)"/><rect x="14" y="13" width="5" height="2" rx="1" fill="rgba(255,255,255,.4)"/><rect x="6" y="15" width="16" height="2" rx="1" fill="rgba(255,255,255,.3)"/><rect x="6" y="19" width="10" height="2" rx="1" fill="rgba(255,255,255,.2)"/><defs><linearGradient id="erpG" x1="3" y1="6" x2="25" y2="22"><stop stop-color="#3b82f6"/><stop offset="1" stop-color="#1d4ed8"/></linearGradient></defs></svg>
                    </div>
                    <div class="bos-login__cap-text">
                        <strong>Enterprise ERP</strong>
                        <span>Finance, HR, Inventory, Production</span>
                    </div>
                </div>
                <div class="bos-login__cap-item bos-login__cap-item--2">
                    <div class="bos-login__cap-icon bos-login__cap-icon--ecom">
                        <svg width="28" height="28" viewBox="0 0 28 28" fill="none"><circle cx="14" cy="12" r="9" fill="url(#ecomG)"/><path d="M8 10h12l-2 7H10L8 10z" fill="rgba(255,255,255,.9)"/><circle cx="11" cy="20" r="1.5" fill="rgba(255,255,255,.8)"/><circle cx="17" cy="20" r="1.5" fill="rgba(255,255,255,.8)"/><path d="M7 8l-1-3" stroke="rgba(255,255,255,.6)" stroke-width="1.5" stroke-linecap="round"/><defs><linearGradient id="ecomG" x1="5" y1="3" x2="23" y2="21"><stop stop-color="#10b981"/><stop offset="1" stop-color="#059669"/></linearGradient></defs></svg>
                    </div>
                    <div class="bos-login__cap-text">
                        <strong>eCommerce Platform</strong>
                        <span>Multi-tenant, Multi-industry</span>
                    </div>
                </div>
                <div class="bos-login__cap-item bos-login__cap-item--3">
                    <div class="bos-login__cap-icon bos-login__cap-icon--compliance">
                        <svg width="28" height="28" viewBox="0 0 28 28" fill="none"><circle cx="14" cy="14" r="11" fill="url(#compG)" opacity=".9"/><ellipse cx="14" cy="14" rx="11" ry="5" stroke="rgba(255,255,255,.5)" stroke-width="1" fill="none"/><ellipse cx="14" cy="14" rx="5" ry="11" stroke="rgba(255,255,255,.4)" stroke-width="1" fill="none"/><circle cx="14" cy="14" r="2" fill="rgba(255,255,255,.9)"/><circle cx="14" cy="3" r="1.2" fill="#fbbf24"/><circle cx="14" cy="25" r="1.2" fill="#34d399"/><circle cx="3" cy="14" r="1.2" fill="#f472b6"/><circle cx="25" cy="14" r="1.2" fill="#60a5fa"/><defs><linearGradient id="compG" x1="3" y1="3" x2="25" y2="25"><stop stop-color="#8b5cf6"/><stop offset="1" stop-color="#6d28d9"/></linearGradient></defs></svg>
                    </div>
                    <div class="bos-login__cap-text">
                        <strong>Worldwide Compliance</strong>
                        <span>Auto-localized per country</span>
                    </div>
                </div>
                <div class="bos-login__cap-item bos-login__cap-item--4">
                    <div class="bos-login__cap-icon bos-login__cap-icon--ai">
                        <svg width="28" height="28" viewBox="0 0 28 28" fill="none"><rect x="4" y="4" width="20" height="20" rx="4" fill="url(#aiG)"/><circle cx="10" cy="11" r="2" fill="rgba(255,255,255,.8)"/><circle cx="18" cy="11" r="2" fill="rgba(255,255,255,.8)"/><path d="M10 17q4 3 8 0" stroke="rgba(255,255,255,.7)" stroke-width="1.5" stroke-linecap="round" fill="none"/><circle cx="14" cy="2" r="1.5" fill="#fbbf24"/><line x1="14" y1="2" x2="14" y2="4" stroke="#fbbf24" stroke-width="1"/><circle cx="4" cy="8" r="1" fill="rgba(255,255,255,.4)"/><circle cx="24" cy="8" r="1" fill="rgba(255,255,255,.4)"/><line x1="4" y1="8" x2="4" y2="10" stroke="rgba(255,255,255,.3)" stroke-width="1"/><line x1="24" y1="8" x2="24" y2="10" stroke="rgba(255,255,255,.3)" stroke-width="1"/><defs><linearGradient id="aiG" x1="4" y1="4" x2="24" y2="24"><stop stop-color="#f59e0b"/><stop offset="1" stop-color="#d97706"/></linearGradient></defs></svg>
                    </div>
                    <div class="bos-login__cap-text">
                        <strong>AI-Powered Operations</strong>
                        <span>Price engine, CRM, Automation</span>
                    </div>
                </div>
            </div>

            <div class="bos-login__stats-bar">
                <div class="bos-login__stat">
                    <span class="bos-login__stat-num" data-count="225">0</span>
                    <span class="bos-login__stat-label">Tenants</span>
                </div>
                <div class="bos-login__stat-divider"></div>
                <div class="bos-login__stat">
                    <span class="bos-login__stat-num" data-count="11">0</span>
                    <span class="bos-login__stat-label">Industries</span>
                </div>
                <div class="bos-login__stat-divider"></div>
                <div class="bos-login__stat">
                    <span class="bos-login__stat-num" data-count="95">0</span>
                    <span class="bos-login__stat-label">ERP Modules</span>
                </div>
                <div class="bos-login__stat-divider"></div>
                <div class="bos-login__stat">
                    <span class="bos-login__stat-num" data-count="15">0</span>
                    <span class="bos-login__stat-label">Countries</span>
                </div>
            </div>
        </div>
    </div>

    <!-- Right side: login forms -->
    <div class="bos-login__panel">
        <div class="bos-login__panel-inner">
            <!-- Role selector tabs -->
            <div class="bos-login__role-tabs">
                <button class="bos-login__role-tab bos-login__role-tab--active" data-role="provider" id="bosTabProvider">
                    <i class="fa fa-shield"></i>
                    <span>Platform Operator</span>
                </button>
                <button class="bos-login__role-tab" data-role="erp" id="bosTabErp">
                    <i class="fa fa-university"></i>
                    <span>ERP Customer</span>
                </button>
            </div>

            <!-- Provider login form -->
            <div class="bos-login__form-wrap bos-login__form-wrap--active" id="bosFormProvider">
                <div class="bos-login__form-header">
                    <h2>Operator Access</h2>
                    <p>Manage your entire tenant fleet from one dashboard</p>
                </div>
                <form class="bos-login__form" id="bosLoginForm" method="post" action="/bos/?action=login">
                    <input type="hidden" name="login_role" value="provider">
                    <div class="bos-login__field">
                        <label for="bosEmail"><i class="fa fa-envelope-o"></i> Email</label>
                        <input type="email" id="bosEmail" name="email" required autocomplete="email" placeholder="operator@ecomae.com">
                        <div class="bos-login__field-line"></div>
                    </div>
                    <div class="bos-login__field">
                        <label for="bosPassword"><i class="fa fa-lock"></i> Password</label>
                        <input type="password" id="bosPassword" name="password" required autocomplete="current-password" placeholder="Enter secure password">
                        <div class="bos-login__field-line"></div>
                    </div>
                    <div class="bos-login__error" id="bosLoginError"></div>
                    <button type="submit" class="bos-login__btn">
                        <span class="bos-login__btn-text"><i class="fa fa-sign-in"></i> Sign In to BOS</span>
                        <span class="bos-login__btn-loading"><i class="fa fa-circle-o-notch fa-spin"></i> Authenticating...</span>
                    </button>
                </form>
                <div class="bos-login__secure-badge">
                    <i class="fa fa-lock"></i> 256-bit encrypted &middot; Session isolated &middot; Audit logged
                </div>
            </div>

            <!-- ERP Customer login form -->
            <div class="bos-login__form-wrap" id="bosFormErp">
                <div class="bos-login__form-header">
                    <h2>ERP Access</h2>
                    <p>Access your enterprise resource planning system</p>
                </div>
                <form class="bos-login__form" id="bosLoginFormErp" method="post" action="/bos/?action=login">
                    <input type="hidden" name="login_role" value="erp">
                    <div class="bos-login__field">
                        <label for="bosErpEmail"><i class="fa fa-envelope-o"></i> Business Email</label>
                        <input type="email" id="bosErpEmail" name="email" required autocomplete="email" placeholder="finance@yourcompany.com">
                        <div class="bos-login__field-line"></div>
                    </div>
                    <div class="bos-login__field">
                        <label for="bosErpPassword"><i class="fa fa-lock"></i> Password</label>
                        <input type="password" id="bosErpPassword" name="password" required autocomplete="current-password" placeholder="Enter your password">
                        <div class="bos-login__field-line"></div>
                    </div>
                    <div class="bos-login__error" id="bosLoginErrorErp"></div>
                    <button type="submit" class="bos-login__btn bos-login__btn--erp">
                        <span class="bos-login__btn-text"><i class="fa fa-university"></i> Access ERP System</span>
                        <span class="bos-login__btn-loading"><i class="fa fa-circle-o-notch fa-spin"></i> Connecting...</span>
                    </button>
                </form>
                <div class="bos-login__erp-features">
                    <div class="bos-login__erp-feat"><i class="fa fa-check-circle"></i> General Ledger & Financial Reporting</div>
                    <div class="bos-login__erp-feat"><i class="fa fa-check-circle"></i> Accounts Payable & Receivable</div>
                    <div class="bos-login__erp-feat"><i class="fa fa-check-circle"></i> HR, Payroll & Workforce Management</div>
                    <div class="bos-login__erp-feat"><i class="fa fa-check-circle"></i> Inventory, Production & Warehouse</div>
                    <div class="bos-login__erp-feat"><i class="fa fa-check-circle"></i> Tax Compliance & E-Invoicing</div>
                </div>
                <div class="bos-login__secure-badge">
                    <i class="fa fa-shield"></i> Database isolated &middot; Country-compliant &middot; Enterprise grade
                </div>
            </div>

            <div class="bos-login__footer">
                <div class="bos-login__footer-brand">
                    <span class="bos-login__footer-logo">ECOM<span>AE</span></span>
                    <span class="bos-login__footer-sep">&middot;</span>
                    <span>Business Operating System v<?php echo EPC_BOS_VERSION; ?></span>
                </div>
            </div>
        </div>
    </div>
</div>

<?php else: ?>
<!-- ═══════════════════ MAIN BOS SHELL ═══════════════════ -->

<!-- Top bar -->
<header class="bos-topbar">
    <div class="bos-topbar__left">
        <button class="bos-topbar__toggle" id="bosSidebarToggle" title="Toggle sidebar">
            <i class="fa fa-bars"></i>
        </button>
        <a href="/bos/" class="bos-topbar__brand">
            <span class="bos-topbar__brand-icon"><i class="fa fa-th-large"></i></span>
            <span class="bos-topbar__brand-text">BOS</span>
        </a>
    </div>

    <div class="bos-topbar__center">
        <?php if (epc_bos_is_provider()): ?>
        <div class="bos-tenant-switcher" id="bosTenantSwitcher">
            <button class="bos-tenant-switcher__btn" id="bosTenantBtn">
                <?php if ($tenantInfo): ?>
                    <i class="fa <?php echo epc_bos_h($industries[$tenantInfo['industry_code']]['icon'] ?? 'fa-building'); ?>"></i>
                    <span class="bos-tenant-switcher__name"><?php echo epc_bos_h($tenantInfo['trade_name'] ?: $activeTenant); ?></span>
                    <span class="bos-tenant-switcher__industry"><?php echo epc_bos_h($tenantInfo['industry_code']); ?></span>
                <?php else: ?>
                    <i class="fa fa-building"></i>
                    <span class="bos-tenant-switcher__name">Select Tenant</span>
                <?php endif; ?>
                <i class="fa fa-chevron-down bos-tenant-switcher__arrow"></i>
            </button>
            <div class="bos-tenant-switcher__dropdown" id="bosTenantDropdown">
                <div class="bos-tenant-switcher__search">
                    <i class="fa fa-search"></i>
                    <input type="text" id="bosTenantSearch" placeholder="Search tenants..." autocomplete="off">
                </div>
                <div class="bos-tenant-switcher__filters" id="bosTenantFilters">
                    <button class="bos-filter-btn bos-filter-btn--active" data-filter="all">All</button>
                    <button class="bos-filter-btn" data-filter="commerce">Commerce</button>
                    <button class="bos-filter-btn" data-filter="erp_only">ERP Only</button>
                    <button class="bos-filter-btn" data-filter="demo">Demo</button>
                </div>
                <div class="bos-tenant-switcher__list" id="bosTenantList">
                    <!-- Populated by JS -->
                </div>
            </div>
        </div>
        <?php endif; ?>
    </div>

    <div class="bos-topbar__right">
        <?php if ($tenantCountryProfile): ?>
        <div class="bos-topbar__locale" title="Tenant locale">
            <span class="bos-topbar__flag"><?php echo epc_bos_h($tenantCountryProfile['country']); ?></span>
            <span class="bos-topbar__currency"><?php echo epc_bos_h($tenantCountryProfile['currency']); ?></span>
            <span class="bos-topbar__lang"><?php echo epc_bos_h(strtoupper($tenantCountryProfile['language'])); ?></span>
        </div>
        <?php endif; ?>
        <div class="bos-topbar__user">
            <i class="fa fa-user-circle"></i>
            <span><?php echo epc_bos_h($ctx['email'] ?? 'Operator'); ?></span>
        </div>
        <a href="/bos/?action=logout" class="bos-topbar__logout" title="Sign out">
            <i class="fa fa-sign-out"></i>
        </a>
    </div>
</header>

<!-- Sidebar -->
<aside class="bos-sidebar" id="bosSidebar">
    <nav class="bos-sidebar__nav" id="bosSidebarNav">
        <?php foreach ($sections as $section): ?>
        <div class="bos-sidebar__section" data-section="<?php echo epc_bos_h($section['id']); ?>">
            <div class="bos-sidebar__section-header">
                <i class="fa <?php echo epc_bos_h($section['icon']); ?>"></i>
                <span><?php echo epc_bos_h($section['label']); ?></span>
                <i class="fa fa-angle-down bos-sidebar__section-arrow"></i>
            </div>
            <ul class="bos-sidebar__items">
                <?php foreach ($section['items'] as $item): ?>
                <li>
                    <a href="/bos/?<?php echo $activeTenant ? 't=' . epc_bos_h($activeTenant) . '&' : ''; ?>m=<?php echo epc_bos_h($item['id']); ?>"
                       class="bos-sidebar__link<?php echo $activeModule === $item['id'] ? ' bos-sidebar__link--active' : ''; ?>"
                       data-module="<?php echo epc_bos_h($item['id']); ?>"
                       data-path="<?php echo epc_bos_h($item['path']); ?>">
                        <i class="fa <?php echo epc_bos_h($item['icon']); ?>"></i>
                        <span><?php echo epc_bos_h($item['label']); ?></span>
                    </a>
                </li>
                <?php endforeach; ?>
            </ul>
        </div>
        <?php endforeach; ?>
    </nav>

    <div class="bos-sidebar__footer">
        <div class="bos-sidebar__version">BOS v<?php echo EPC_BOS_VERSION; ?></div>
    </div>
</aside>

<!-- Main content area -->
<main class="bos-main" id="bosMain">
    <div class="bos-main__header" id="bosMainHeader">
        <div class="bos-main__breadcrumb">
            <span>BOS</span>
            <?php if ($tenantInfo): ?>
                <i class="fa fa-angle-right"></i>
                <span><?php echo epc_bos_h($tenantInfo['trade_name'] ?: $activeTenant); ?></span>
            <?php endif; ?>
            <i class="fa fa-angle-right"></i>
            <span id="bosActiveModuleLabel"><?php echo epc_bos_h(ucwords(str_replace('_', ' ', $activeModule))); ?></span>
        </div>
        <?php if ($tenantCountryProfile): ?>
        <div class="bos-main__tenant-bar">
            <span class="bos-tag bos-tag--country"><i class="fa fa-globe"></i> <?php echo epc_bos_h($tenantCountryProfile['name']); ?></span>
            <span class="bos-tag bos-tag--currency"><i class="fa fa-money"></i> <?php echo epc_bos_h($tenantCountryProfile['currency']); ?></span>
            <span class="bos-tag bos-tag--tax"><i class="fa fa-percent"></i> <?php echo epc_bos_h($tenantCountryProfile['tax_label'] . ' ' . $tenantCountryProfile['tax_rate'] . '%'); ?></span>
            <span class="bos-tag bos-tag--lang"><i class="fa fa-language"></i> <?php echo epc_bos_h(strtoupper($tenantCountryProfile['language'])); ?> / <?php echo epc_bos_h(strtoupper($tenantCountryProfile['dir'])); ?></span>
            <?php if ($tenantCountryProfile['einvoice']): ?>
            <span class="bos-tag bos-tag--einvoice"><i class="fa fa-file-text-o"></i> <?php echo epc_bos_h($tenantCountryProfile['einvoice']); ?></span>
            <?php endif; ?>
        </div>
        <?php endif; ?>
    </div>

    <div class="bos-main__content" id="bosContent">
        <?php if ($isModuleView && $activeModuleInfo): ?>
        <!-- Module Content View (works for both provider and tenant context) -->
        <?php
            $modColor = epc_bos_module_color($activeModuleInfo['section']);
            $isErpModule = strpos($activeModuleInfo['path'], 'shop/finance/erp') === 0;
            $tenantHostname = is_array($tenantInfo) ? ($tenantInfo['hostname'] ?? '') : '';
            $tenantName = is_array($tenantInfo) ? ($tenantInfo['trade_name'] ?? $activeTenant) : '';
            $cpBaseUrl = $tenantHostname ? 'https://' . $tenantHostname . '/cp/' : '';
            $erpBaseUrl = $tenantHostname ? 'https://' . $tenantHostname . '/cp/client-erp/asap/' : '';
            $backUrl = $activeTenant ? '/bos/?t=' . epc_bos_h($activeTenant) : '/bos/';
            $contextLabel = $activeTenant ? ($tenantName ?: $activeTenant) : 'Platform';
            $isPlatformModule = !$activeTenant;

            $moduleDescriptions = array(
                'command_center'   => 'Monitor all tenants, system health, and platform metrics in real time.',
                'platform_health'  => 'Health checks, uptime monitoring, and infrastructure diagnostics.',
                'governance'       => 'Compliance rules, audit policies, and platform governance settings.',
                'audit_log'        => 'Complete audit trail of all administrative actions across the platform.',
                'failover'         => 'Disaster recovery procedures, failover runbook, and backup status.',
                'isolation_audit'   => 'Commerce data isolation audit: site_key enforcement, price_id scoping, cross-tenant leak detection.',
                'mfa_management'    => 'Multi-Factor Authentication policy: TOTP enrollment, role enforcement, backup codes, audit log.',
                'event_bus'         => 'Platform event bus: order.placed, invoice.posted, stock.below — structured events for webhooks and integrations.',
                'webhooks'          => 'Webhook management: HMAC-signed HTTP POST delivery, retry with exponential backoff, dead-letter queue.',
                'readiness_score'  => 'Enterprise readiness score: per-tenant health check (isolation, MFA, backup, e-invoice, performance, compliance).',
                'notifications'    => 'Notification center: in-app alerts, email digest, webhook notifications, per-category preferences.',
                'db_migrations'    => 'Database migration versioning: track, apply, rollback schema changes across platform and tenant databases.',
                'cp_role_home' => 'Role-based CP home: per-role dashboards (admin, finance, warehouse), KPI tiles, permissions.',
                'credit_limit' => 'Credit limit engine: per-customer credit, auto-hold on exceeded, aging analysis.',
                'order_erp_pipeline' => 'Order-to-ERP pipeline: auto GL journal, AR invoice, inventory deduct, tax posting.',
                'po_approval' => 'PO approval workflow: 3-tier (manager, finance, director), threshold rules, audit trail.',
                'rest_api_v2' => 'REST API v2: versioned endpoints, rate limiting, sandbox keys, OpenAPI spec.',
                'fulfillment_queue' => 'Fulfillment queue: pick/pack/ship, wave picking, SLA tracking.',
                'bi_metrics' => 'BI metrics engine: KPI snapshots, trends, cross-tenant comparison.',
                'ai_classification' => 'AI classification service: HS code lookup, category auto-tag, batch processing.',
                'tenant_config' => 'Tenant self-service config: 7 groups, change history, export/import.',
                'workflow_builder' => 'Workflow builder: trigger → condition → action chains, templates.',
                'inventory_forecast' => 'Inventory forecasting: demand planning, reorder points, EOQ, ABC analysis.',
                'multi_currency_gl' => 'Multi-currency GL: FX rates, revaluation, unrealized gain/loss, exposure report.',
                'sso_saml' => 'SSO / SAML 2.0: IdP config, SP metadata, JIT provisioning, session management.',
                'wps_payroll' => 'WPS / payroll: SIF file generation, salary management, payroll runs.',
                'collections_dunning' => 'Collections & dunning: 7-step sequences, aging buckets, payment tracking.',
                'warranty_rma' => 'Warranty / RMA: registration, claims, state machine, core returns.',
                'dealer_portal' => 'Dealer / distributor portal: tiered pricing, auto-tier promotion, B2B.',
                'ai_copilot' => 'AI copilot: NL query → SQL → chart, intent parsing, query history.',
                'nl_reporting' => 'NL reporting: template library, scheduled reports, export.',
                'industry_packs' => 'Industry packs: auto-parts, fashion, electronics, jewellery verticals.',
                'multi_entity' => 'Multi-entity consolidation: group GL, intercompany elimination, reporting.',
                'promotions_engine' => 'Promotions engine: percentage, fixed, BOGO, free shipping, bundle, tiered.',
                'config_sandbox' => 'Config sandbox: snapshot, diff, promote/discard before go-live.',
                'import_orchestrator' => 'Import orchestrator: bulk CSV/XML, schema validation, dry-run, chunk processing.',
                'document_vault' => 'Document vault: versioned file storage, folders, access control.',
                'subscription_billing' => 'Subscription billing: plans, trials, invoicing cycles, MRR tracking, payment, cancellation.',
                'soc2_compliance' => 'SOC 2 compliance: 21 controls (Trust Service Criteria), evidence, policies, gap analysis.',
                'promotions_engine' => 'Promotions engine: percentage, fixed, BOGO, free shipping, bundle, tiered.',
                'data_policy'      => 'Tenant data processing policy, classification, retention, and compliance.',
                'tenant_hub'       => 'Centralized tenant management: create, configure, and monitor tenants.',
                'tenant_control'   => 'Per-tenant settings: features, packs, limits, and configurations.',
                'tenant_features'  => 'Feature matrix showing capabilities enabled for each tenant.',
                'demo_tenants'     => 'Manage demo/trial tenants for sales and onboarding.',
                'industry_packs'   => 'Configure industry-specific feature packs and ERP module bundles.',
                'customer_board'   => 'Customer relationship overview across all tenants.',
                'integrations'     => 'Third-party integrations, API keys, and webhook management.',
                'design_tokens'    => 'Tenant branding: CSS design tokens, logo, colors, white-label login per tenant.',
                'orders'           => 'View, manage, and process customer orders. Track fulfillment status.',
                'customers'        => 'Customer database, profiles, purchase history, and segmentation.',
                'payments'         => 'Payment processing, transaction history, and settlement tracking.',
                'returns'          => 'Return requests, refund processing, and RMA management.',
                'quotes'           => 'Quote requests from customers, pricing negotiation, and approvals.',
                'channels'         => 'Multi-channel sales: marketplace integrations and channel management.',
                'pos'              => 'Point of Sale terminal for in-store transactions.',
                'statistics'       => 'Sales analytics, conversion rates, revenue reports, and trends.',
                'products'         => 'Product catalog management: items, categories, and attributes.',
                'prices_edit'      => 'Edit prices directly for individual products and price lists.',
                'prices_upload'    => 'Bulk price upload via CSV/Excel for warehouses and suppliers.',
                'prices_send'      => 'Send price updates to channels, marketplaces, and partners.',
                'pricing'          => 'Pricing rules: markups, discounts, customer-specific pricing.',
                'logistics'        => 'Shipping, delivery management, and carrier integrations.',
                'procurement'      => 'Purchase orders, supplier management, and procurement workflows.',
                'marketing'        => 'Marketing campaigns, promotions, and discount code management.',
                'broadcast'        => 'Email/SMS broadcast to customers and leads.',
                'social'           => 'Social media integration and management tools.',
                'seo'              => 'Search engine optimization settings, meta tags, and sitemaps.',
                'crm'              => 'Customer relationship management: leads, deals, and pipeline.',
                'documents'        => 'Document management and control for business processes.',
                'parts_agent'      => 'AI-powered parts inquiry chat agent for customer support.',
                'erp_home'         => 'ERP overview dashboard with key financial metrics and KPIs.',
                'erp_gl'           => 'General Ledger: chart of accounts, journal entries, and trial balance.',
                'erp_ap'           => 'Accounts Payable: vendor invoices, payments, and aging reports.',
                'erp_ar'           => 'Accounts Receivable: customer invoices, collections, and aging.',
                'erp_cash'         => 'Cash and bank management: bank accounts, reconciliation, and cash flow.',
                'erp_tax'          => 'Tax compliance: VAT/GST returns, tax codes, and filing preparation.',
                'erp_sales'        => 'Sales management: quotations, sales orders, and CRM integration.',
                'erp_purchasing'   => 'Purchase management: purchase orders, vendor selection, and RFQs.',
                'erp_inventory'    => 'Inventory management: stock levels, warehouses, and stock movements.',
                'erp_hr'           => 'Human resources: employee records, leave, attendance, and HR law.',
                'erp_payroll'      => 'Payroll processing: salary calculation, deductions, and payslips.',
                'erp_production'   => 'Production planning: BOMs, work orders, and manufacturing.',
                'erp_projects'     => 'Project management: tasks, milestones, and project accounting.',
                'erp_warehouse'    => 'Warehouse management: locations, bin management, and picking.',
                'erp_fixed_assets' => 'Fixed asset register: depreciation, disposal, and asset tracking.',
                'erp_budgeting'    => 'Budget planning: departmental budgets, variance analysis, and forecasts.',
                'crosses'          => 'Part cross-references: OEM/aftermarket number matching.',
                'demand'           => 'Demand analysis by country: popular parts and regional trends.',
                'auto_price'       => 'AI-powered automatic pricing engine for auto parts.',
                'synonyms'         => 'Brand synonym mapping for search and matching.',
                'tax_toolkit'      => 'Tax calculation toolkit for advisory clients.',
                'free_tools'       => 'Public free tools administration and configuration.',
                'portal_settings'  => 'Portal configuration: branding, domains, and global settings.',
                'modern_auth'      => 'Authentication settings: password policies and session management.',
                'communication'    => 'Communication tools: notifications, messages, and announcements.',
                'api_docs'         => 'API documentation and developer guide.',
                'operator_guide'   => 'Platform operator guide and administration manual.',
            );
            $modDesc = $moduleDescriptions[$activeModuleInfo['id']] ?? 'Module for ' . $activeModuleInfo['section'] . ' operations.';
            $moduleType = $isPlatformModule ? 'Platform Module' : ($isErpModule ? 'ERP Module' : 'CP Module');
            $moduleTypeIcon = $isPlatformModule ? 'fa-server' : ($isErpModule ? 'fa-university' : 'fa-cog');
        ?>
        <div class="bos-module-view" id="bosModuleView">
            <div class="bos-module-view__header">
                <div class="bos-module-view__title">
                    <a href="<?php echo $backUrl; ?>" class="bos-module-view__back" title="Back">
                        <i class="fa fa-arrow-left"></i>
                    </a>
                    <div class="bos-module-view__icon" style="background: <?php echo $modColor; ?>;">
                        <i class="fa <?php echo epc_bos_h($activeModuleInfo['icon']); ?>"></i>
                    </div>
                    <div>
                        <h2><?php echo epc_bos_h($activeModuleInfo['label']); ?></h2>
                        <span class="bos-module-view__section">
                            <i class="fa <?php echo epc_bos_h($activeModuleInfo['section_icon']); ?>"></i>
                            <?php echo epc_bos_h($activeModuleInfo['section']); ?>
                        </span>
                    </div>
                </div>
                <?php if ($tenantHostname): ?>
                <div class="bos-module-view__actions">
                    <?php if ($isErpModule): ?>
                    <a href="<?php echo $erpBaseUrl; ?>" target="_blank" class="bos-btn bos-btn--primary">
                        <i class="fa fa-external-link"></i> Open ERP
                    </a>
                    <?php else: ?>
                    <a href="<?php echo $cpBaseUrl; ?>" target="_blank" class="bos-btn bos-btn--primary">
                        <i class="fa fa-external-link"></i> Open in CP
                    </a>
                    <?php endif; ?>
                </div>
                <?php endif; ?>
            </div>
            <div class="bos-module-view__body">
                <div class="bos-module-view__content">
                    <div class="bos-module-view__hero" style="--mod-color: <?php echo $modColor; ?>;">
                        <div class="bos-module-view__hero-icon">
                            <i class="fa <?php echo epc_bos_h($activeModuleInfo['icon']); ?>"></i>
                        </div>
                        <div class="bos-module-view__hero-info">
                            <h3><?php echo epc_bos_h($activeModuleInfo['label']); ?></h3>
                            <p><?php echo epc_bos_h($modDesc); ?></p>
                        </div>
                    </div>

                    <div class="bos-module-view__details">
                        <div class="bos-module-view__detail-card">
                            <div class="bos-module-view__detail-label">Section</div>
                            <div class="bos-module-view__detail-value">
                                <i class="fa <?php echo epc_bos_h($activeModuleInfo['section_icon']); ?>" style="color: <?php echo $modColor; ?>;"></i>
                                <?php echo epc_bos_h($activeModuleInfo['section']); ?>
                            </div>
                        </div>
                        <div class="bos-module-view__detail-card">
                            <div class="bos-module-view__detail-label"><?php echo $isPlatformModule ? 'Context' : 'Tenant'; ?></div>
                            <div class="bos-module-view__detail-value">
                                <i class="fa <?php echo $isPlatformModule ? 'fa-server' : 'fa-building'; ?>" style="color: var(--bos-primary);"></i>
                                <?php echo epc_bos_h($contextLabel); ?>
                            </div>
                        </div>
                        <?php if ($tenantHostname): ?>
                        <div class="bos-module-view__detail-card">
                            <div class="bos-module-view__detail-label">Access</div>
                            <div class="bos-module-view__detail-value">
                                <i class="fa fa-globe" style="color: var(--bos-success);"></i>
                                <?php echo epc_bos_h($tenantHostname); ?>
                            </div>
                        </div>
                        <?php endif; ?>
                        <div class="bos-module-view__detail-card">
                            <div class="bos-module-view__detail-label">Type</div>
                            <div class="bos-module-view__detail-value">
                                <i class="fa <?php echo $moduleTypeIcon; ?>" style="color: <?php echo $modColor; ?>;"></i>
                                <?php echo $moduleType; ?>
                            </div>
                        </div>
                    </div>

                    <?php if ($tenantHostname): ?>
                    <div class="bos-module-view__launch">
                        <?php if ($isErpModule): ?>
                        <a href="<?php echo $erpBaseUrl; ?>" target="_blank" class="bos-module-view__launch-btn" style="--mod-color: <?php echo $modColor; ?>;">
                            <i class="fa fa-rocket"></i>
                            <span>Launch <?php echo epc_bos_h($activeModuleInfo['label']); ?> in ERP</span>
                            <i class="fa fa-arrow-right"></i>
                        </a>
                        <?php else: ?>
                        <a href="<?php echo $cpBaseUrl; ?>" target="_blank" class="bos-module-view__launch-btn" style="--mod-color: <?php echo $modColor; ?>;">
                            <i class="fa fa-rocket"></i>
                            <span>Launch <?php echo epc_bos_h($activeModuleInfo['label']); ?> in Control Panel</span>
                            <i class="fa fa-arrow-right"></i>
                        </a>
                        <?php endif; ?>
                    </div>
                    <?php endif; ?>
                </div>
            </div>
        </div>

        <?php elseif (!$activeTenant && epc_bos_is_provider()): ?>
        <!-- Fleet Dashboard -->
        <div class="bos-dashboard">
            <h2 class="bos-dashboard__title"><i class="fa fa-tachometer"></i> Fleet Command Center</h2>
            <div class="bos-dashboard__stats">
                <?php
                    $countByType = array('commerce' => 0, 'erp_only' => 0, 'demo' => 0, 'platform' => 0);
                    foreach ($tenants as $t) {
                        $type = $t['type'] ?? 'commerce';
                        $countByType[$type] = ($countByType[$type] ?? 0) + 1;
                    }
                ?>
                <div class="bos-stat-card bos-stat-card--total">
                    <div class="bos-stat-card__number"><?php echo count($tenants); ?></div>
                    <div class="bos-stat-card__label">Total Tenants</div>
                    <div class="bos-stat-card__icon"><i class="fa fa-th-large"></i></div>
                </div>
                <div class="bos-stat-card bos-stat-card--commerce">
                    <div class="bos-stat-card__number"><?php echo $countByType['commerce']; ?></div>
                    <div class="bos-stat-card__label">Commerce</div>
                    <div class="bos-stat-card__icon"><i class="fa fa-shopping-cart"></i></div>
                </div>
                <div class="bos-stat-card bos-stat-card--erp">
                    <div class="bos-stat-card__number"><?php echo $countByType['erp_only']; ?></div>
                    <div class="bos-stat-card__label">ERP Only</div>
                    <div class="bos-stat-card__icon"><i class="fa fa-university"></i></div>
                </div>
                <div class="bos-stat-card bos-stat-card--demo">
                    <div class="bos-stat-card__number"><?php echo $countByType['demo']; ?></div>
                    <div class="bos-stat-card__label">Demo</div>
                    <div class="bos-stat-card__icon"><i class="fa fa-flask"></i></div>
                </div>
            </div>

            <h3 class="bos-dashboard__subtitle"><i class="fa fa-list"></i> Tenant Fleet</h3>
            <div class="bos-tenant-grid" id="bosTenantGrid">
                <?php foreach ($tenants as $t):
                    $tColor = epc_bos_industry_color($t['industry']);
                ?>
                <a href="/bos/?t=<?php echo epc_bos_h($t['site_key']); ?>" class="bos-tenant-card" data-type="<?php echo epc_bos_h($t['type']); ?>" style="--tenant-color: <?php echo $tColor; ?>">
                    <div class="bos-tenant-card__header">
                        <span class="bos-tenant-card__icon"><i class="fa <?php echo epc_bos_h($t['industry_icon']); ?>"></i></span>
                        <span class="bos-badge <?php echo epc_bos_tenant_badge_class($t['type']); ?>"><?php echo epc_bos_h(epc_bos_tenant_type_label($t['type'])); ?></span>
                    </div>
                    <div class="bos-tenant-card__name"><?php echo epc_bos_h($t['trade_name'] ?: $t['site_key']); ?></div>
                    <div class="bos-tenant-card__meta">
                        <span><i class="fa fa-industry"></i> <?php echo epc_bos_h($t['industry_name']); ?></span>
                        <?php if ($t['hostname']): ?>
                        <span><i class="fa fa-globe"></i> <?php echo epc_bos_h($t['hostname']); ?></span>
                        <?php endif; ?>
                    </div>
                    <div class="bos-tenant-card__status">
                        <span class="bos-status-dot bos-status-dot--<?php echo $t['is_active'] ? 'active' : 'inactive'; ?>"></span>
                        <?php echo epc_bos_h($t['status']); ?>
                    </div>
                </a>
                <?php endforeach; ?>
            </div>

            <h3 class="bos-dashboard__subtitle"><i class="fa fa-cubes"></i> Industry Coverage</h3>
            <div class="bos-industry-grid">
                <?php
                $indCounts = array();
                foreach ($tenants as $t) {
                    $ic = $t['industry'];
                    $indCounts[$ic] = ($indCounts[$ic] ?? 0) + 1;
                }
                foreach ($industries as $code => $ind):
                    $count = $indCounts[$code] ?? 0;
                ?>
                <div class="bos-industry-card<?php echo $count > 0 ? ' bos-industry-card--active' : ''; ?>" style="--industry-color: <?php echo epc_bos_industry_color($code); ?>">
                    <i class="fa <?php echo epc_bos_h($ind['icon']); ?>" style="color: <?php echo epc_bos_industry_color($code); ?>"></i>
                    <span class="bos-industry-card__name"><?php echo epc_bos_h($ind['name']); ?></span>
                    <span class="bos-industry-card__count"><?php echo $count; ?></span>
                </div>
                <?php endforeach; ?>
            </div>
        </div>

        <?php elseif ($activeTenant): ?>
        <!-- Tenant Home — Module Grid -->
        <div class="bos-tenant-home" id="bosTenantHome">
            <div class="bos-tenant-home__header">
                <div class="bos-tenant-home__info">
                    <h2>
                        <i class="fa <?php echo epc_bos_h($industries[$tenantInfo['industry_code']]['icon'] ?? 'fa-building'); ?>"></i>
                        <?php echo epc_bos_h($tenantInfo['trade_name'] ?: $activeTenant); ?>
                    </h2>
                    <div class="bos-tenant-home__tags">
                        <span class="bos-tag"><i class="fa fa-industry"></i> <?php echo epc_bos_h($tenantInfo['industry_code']); ?></span>
                        <?php if ($tenantInfo['hostname']): ?>
                        <span class="bos-tag"><i class="fa fa-globe"></i> <?php echo epc_bos_h($tenantInfo['hostname']); ?></span>
                        <?php endif; ?>
                    </div>
                </div>
                <?php if ($tenantInfo['hostname']): ?>
                <div class="bos-tenant-home__actions">
                    <a href="https://<?php echo epc_bos_h($tenantInfo['hostname']); ?>" target="_blank" class="bos-btn bos-btn--outline">
                        <i class="fa fa-external-link"></i> Storefront
                    </a>
                    <a href="https://<?php echo epc_bos_h($tenantInfo['hostname']); ?>/cp/" target="_blank" class="bos-btn bos-btn--outline">
                        <i class="fa fa-cog"></i> Tenant CP
                    </a>
                </div>
                <?php endif; ?>
            </div>

            <?php if ($tenantCountryProfile): ?>
            <div class="bos-tenant-home__compliance">
                <h3><i class="fa fa-globe"></i> Country Compliance Profile</h3>
                <div class="bos-compliance-grid">
                    <div class="bos-compliance-item">
                        <div class="bos-compliance-item__label">Country</div>
                        <div class="bos-compliance-item__value"><?php echo epc_bos_h($tenantCountryProfile['name']); ?> (<?php echo epc_bos_h($tenantCountryProfile['country']); ?>)</div>
                    </div>
                    <div class="bos-compliance-item">
                        <div class="bos-compliance-item__label">Currency</div>
                        <div class="bos-compliance-item__value"><?php echo epc_bos_h($tenantCountryProfile['currency']); ?></div>
                    </div>
                    <div class="bos-compliance-item">
                        <div class="bos-compliance-item__label">Tax</div>
                        <div class="bos-compliance-item__value"><?php echo epc_bos_h($tenantCountryProfile['tax_label'] . ' ' . $tenantCountryProfile['tax_rate'] . '%'); ?></div>
                    </div>
                    <div class="bos-compliance-item">
                        <div class="bos-compliance-item__label">Language / Dir</div>
                        <div class="bos-compliance-item__value"><?php echo epc_bos_h(strtoupper($tenantCountryProfile['language']) . ' / ' . strtoupper($tenantCountryProfile['dir'])); ?></div>
                    </div>
                    <div class="bos-compliance-item">
                        <div class="bos-compliance-item__label">E-Invoice</div>
                        <div class="bos-compliance-item__value"><?php echo epc_bos_h($tenantCountryProfile['einvoice'] ?: 'Not required'); ?></div>
                    </div>
                    <div class="bos-compliance-item">
                        <div class="bos-compliance-item__label">Fiscal Year</div>
                        <div class="bos-compliance-item__value">Starts month <?php echo (int) $tenantCountryProfile['fiscal_year_start_month']; ?></div>
                    </div>
                    <div class="bos-compliance-item">
                        <div class="bos-compliance-item__label">Date Format</div>
                        <div class="bos-compliance-item__value"><?php echo epc_bos_h($tenantCountryProfile['date_format']); ?></div>
                    </div>
                </div>
            </div>
            <?php endif; ?>

            <div class="bos-tenant-home__modules">
                <h3><i class="fa fa-th"></i> Available Modules</h3>
                <div class="bos-module-grid">
                    <?php
                    $sectionColors = array(
                        'Fleet Command'      => array('bg' => '#0ea5e9', 'gradient' => 'linear-gradient(135deg, #0ea5e9, #0284c7)'),
                        'Tenant Operations'  => array('bg' => '#8b5cf6', 'gradient' => 'linear-gradient(135deg, #8b5cf6, #7c3aed)'),
                        'Commerce'           => array('bg' => '#10b981', 'gradient' => 'linear-gradient(135deg, #10b981, #059669)'),
                        'Catalogue'          => array('bg' => '#f59e0b', 'gradient' => 'linear-gradient(135deg, #f59e0b, #d97706)'),
                        'Logistics'          => array('bg' => '#6366f1', 'gradient' => 'linear-gradient(135deg, #6366f1, #4f46e5)'),
                        'Marketing'          => array('bg' => '#ec4899', 'gradient' => 'linear-gradient(135deg, #ec4899, #db2777)'),
                        'Professional'       => array('bg' => '#14b8a6', 'gradient' => 'linear-gradient(135deg, #14b8a6, #0d9488)'),
                        'ERP Finance'        => array('bg' => '#3b82f6', 'gradient' => 'linear-gradient(135deg, #3b82f6, #2563eb)'),
                        'Auto Parts'         => array('bg' => '#ef4444', 'gradient' => 'linear-gradient(135deg, #ef4444, #dc2626)'),
                        'Tax & Advisory'     => array('bg' => '#f97316', 'gradient' => 'linear-gradient(135deg, #f97316, #ea580c)'),
                        'Platform'           => array('bg' => '#64748b', 'gradient' => 'linear-gradient(135deg, #64748b, #475569)'),
                    );
                    $sIdx = 0;
                    ?>
                    <?php foreach ($sections as $section): ?>
                    <?php if (!empty($section['provider_only']) && !epc_bos_is_provider()) continue; ?>
                    <?php $sColor = $sectionColors[$section['label']] ?? array('bg' => '#64748b', 'gradient' => 'linear-gradient(135deg, #64748b, #475569)'); ?>
                    <div class="bos-module-group" style="--module-color: <?php echo $sColor['bg']; ?>; --module-gradient: <?php echo $sColor['gradient']; ?>;">
                        <div class="bos-module-group__header">
                            <div class="bos-module-group__header-icon" style="background: <?php echo $sColor['gradient']; ?>;">
                                <i class="fa <?php echo epc_bos_h($section['icon']); ?>"></i>
                            </div>
                            <span class="bos-module-group__header-label"><?php echo epc_bos_h($section['label']); ?></span>
                            <span class="bos-module-group__count"><?php echo count($section['items']); ?></span>
                        </div>
                        <div class="bos-module-group__items">
                            <?php foreach ($section['items'] as $item): ?>
                            <a href="/bos/?t=<?php echo epc_bos_h($activeTenant); ?>&m=<?php echo epc_bos_h($item['id']); ?>"
                               class="bos-module-tile" data-module="<?php echo epc_bos_h($item['id']); ?>">
                                <i class="fa <?php echo epc_bos_h($item['icon']); ?>" style="color: <?php echo $sColor['bg']; ?>;"></i>
                                <span><?php echo epc_bos_h($item['label']); ?></span>
                            </a>
                            <?php endforeach; ?>
                        </div>
                    </div>
                    <?php $sIdx++; endforeach; ?>
                </div>
            </div>
        </div>
        <?php endif; ?>
    </div>
</main>

<?php endif; ?>

<script>
window.BOS = {
    version: <?php echo json_encode(EPC_BOS_VERSION); ?>,
    isProvider: <?php echo epc_bos_is_provider() ? 'true' : 'false'; ?>,
    activeTenant: <?php echo json_encode($activeTenant); ?>,
    activeModule: <?php echo json_encode($activeModule); ?>,
    tenants: <?php echo $tenantsJson; ?>,
    sections: <?php echo $sectionsJson; ?>,
    tenantInfo: <?php echo $tenantInfoJson; ?>,
    countryProfile: <?php echo $countryProfileJson; ?>,
    industries: <?php echo $industriesJson; ?>
};
</script>
<script src="/epc-static.php?f=bos/epc_bos_shell.js&v=<?php echo EPC_BOS_VERSION; ?>"></script>
</body>
</html>
