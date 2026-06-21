<?php
/**
 * BOS — Business Operating System
 * Unified entry point: ecomae.com/bos/
 */
define('_ASTEXE_', 1);
define('EPC_BOS_ENTRY', true);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bos_unified.php';

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
<link rel="stylesheet" href="/bos/epc_bos_shell.css?v=<?php echo EPC_BOS_VERSION; ?>">
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
                    <div class="bos-login__cap-icon"><i class="fa fa-university"></i></div>
                    <div class="bos-login__cap-text">
                        <strong>Enterprise ERP</strong>
                        <span>Finance, HR, Inventory, Production</span>
                    </div>
                </div>
                <div class="bos-login__cap-item bos-login__cap-item--2">
                    <div class="bos-login__cap-icon"><i class="fa fa-shopping-cart"></i></div>
                    <div class="bos-login__cap-text">
                        <strong>eCommerce Platform</strong>
                        <span>Multi-tenant, Multi-industry</span>
                    </div>
                </div>
                <div class="bos-login__cap-item bos-login__cap-item--3">
                    <div class="bos-login__cap-icon"><i class="fa fa-globe"></i></div>
                    <div class="bos-login__cap-text">
                        <strong>Worldwide Compliance</strong>
                        <span>Auto-localized per country</span>
                    </div>
                </div>
                <div class="bos-login__cap-item bos-login__cap-item--4">
                    <div class="bos-login__cap-icon"><i class="fa fa-magic"></i></div>
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
        <?php if (!$activeTenant && epc_bos_is_provider()): ?>
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
                    <i class="fa fa-th-large bos-stat-card__icon"></i>
                </div>
                <div class="bos-stat-card bos-stat-card--commerce">
                    <div class="bos-stat-card__number"><?php echo $countByType['commerce']; ?></div>
                    <div class="bos-stat-card__label">Commerce</div>
                    <i class="fa fa-shopping-cart bos-stat-card__icon"></i>
                </div>
                <div class="bos-stat-card bos-stat-card--erp">
                    <div class="bos-stat-card__number"><?php echo $countByType['erp_only']; ?></div>
                    <div class="bos-stat-card__label">ERP Only</div>
                    <i class="fa fa-university bos-stat-card__icon"></i>
                </div>
                <div class="bos-stat-card bos-stat-card--demo">
                    <div class="bos-stat-card__number"><?php echo $countByType['demo']; ?></div>
                    <div class="bos-stat-card__label">Demo</div>
                    <i class="fa fa-flask bos-stat-card__icon"></i>
                </div>
            </div>

            <h3 class="bos-dashboard__subtitle"><i class="fa fa-list"></i> Tenant Fleet</h3>
            <div class="bos-tenant-grid" id="bosTenantGrid">
                <?php foreach ($tenants as $t): ?>
                <a href="/bos/?t=<?php echo epc_bos_h($t['site_key']); ?>" class="bos-tenant-card" data-type="<?php echo epc_bos_h($t['type']); ?>">
                    <div class="bos-tenant-card__header">
                        <i class="fa <?php echo epc_bos_h($t['industry_icon']); ?> bos-tenant-card__icon"></i>
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
                <div class="bos-industry-card<?php echo $count > 0 ? ' bos-industry-card--active' : ''; ?>">
                    <i class="fa <?php echo epc_bos_h($ind['icon']); ?>"></i>
                    <span class="bos-industry-card__name"><?php echo epc_bos_h($ind['name']); ?></span>
                    <span class="bos-industry-card__count"><?php echo $count; ?></span>
                </div>
                <?php endforeach; ?>
            </div>
        </div>

        <?php elseif ($activeTenant): ?>
        <!-- Tenant Context Active -->
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
                    <?php foreach ($sections as $section): ?>
                    <?php if (!empty($section['provider_only']) && !epc_bos_is_provider()) continue; ?>
                    <div class="bos-module-group">
                        <div class="bos-module-group__header">
                            <i class="fa <?php echo epc_bos_h($section['icon']); ?>"></i>
                            <?php echo epc_bos_h($section['label']); ?>
                            <span class="bos-module-group__count"><?php echo count($section['items']); ?></span>
                        </div>
                        <div class="bos-module-group__items">
                            <?php foreach ($section['items'] as $item): ?>
                            <a href="/bos/?t=<?php echo epc_bos_h($activeTenant); ?>&m=<?php echo epc_bos_h($item['id']); ?>"
                               class="bos-module-tile" data-module="<?php echo epc_bos_h($item['id']); ?>">
                                <i class="fa <?php echo epc_bos_h($item['icon']); ?>"></i>
                                <span><?php echo epc_bos_h($item['label']); ?></span>
                            </a>
                            <?php endforeach; ?>
                        </div>
                    </div>
                    <?php endforeach; ?>
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
<script src="/bos/epc_bos_shell.js?v=<?php echo EPC_BOS_VERSION; ?>"></script>
</body>
</html>
