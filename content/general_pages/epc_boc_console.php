<?php
/**
 * BOC console shell — Business Operation Control chrome.
 * Default layout is ERP-style TOP mega-menu (red + white); optional left rail
 * via ctx['layout'] = 'rail'. Neutralises legacy CP theme chrome on BOC pages.
 *
 * Pure presentation: open/close emit markup from plain data, so they can be
 * rendered both inside the live CP and in an offline screenshot harness.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_boc_kernel.php';
if (is_file(__DIR__ . '/epc_cp_translate.php')) {
    require_once __DIR__ . '/epc_cp_translate.php';
}

if (!function_exists('epc_boc_console_css')) {
    /** The BOC design system. Scoped under .epc-boc / body.epc-boc-mode. */
    function epc_boc_console_css(): string
    {
        $path = dirname(__DIR__, 2) . '/cp/templates/bootstrap_admin/css/epc_boc_console.css';
        if (is_file($path)) {
            $css = file_get_contents($path);
            return is_string($css) ? $css : '';
        }
        return '';
    }
}

if (!function_exists('epc_boc_console_asset_ver')) {
    function epc_boc_console_asset_ver(): string
    {
        return '20260722boc1';
    }
}

if (!function_exists('epc_boc_console_enqueue_assets')) {
    /**
     * Load BOC CSS/JS outside the CP .row pane (avoids base-href "code flash").
     * Safe to call multiple times.
     */
    function epc_boc_console_enqueue_assets(): void
    {
        if (!empty($GLOBALS['epc_boc_assets_enqueued'])) {
            return;
        }
        $GLOBALS['epc_boc_assets_enqueued'] = true;
        $ver = rawurlencode(epc_boc_console_asset_ver());
        $cssUrl = '/content/general_pages/epc_boc_console_css.php?v=' . $ver;
        $jsUrl = '/content/general_pages/epc_boc_topnav_js.php?v=' . $ver;
        // Head already closed on normal CP requests — queue CSS for footer inject too.
        if (empty($GLOBALS['epc_boc_css_in_head'])) {
            if (!isset($GLOBALS['epc_cp_footer_styles']) || !is_array($GLOBALS['epc_cp_footer_styles'])) {
                $GLOBALS['epc_cp_footer_styles'] = array();
            }
            $GLOBALS['epc_cp_footer_styles'][] =
                '<link rel="stylesheet" href="' . htmlspecialchars($cssUrl, ENT_QUOTES, 'UTF-8') . '">';
        }
        if (function_exists('epc_cp_footer_scripts_append')) {
            epc_cp_footer_scripts_append('<script src="' . htmlspecialchars($jsUrl, ENT_QUOTES, 'UTF-8') . '"></script>');
        } else {
            if (!isset($GLOBALS['epc_cp_footer_scripts']) || !is_array($GLOBALS['epc_cp_footer_scripts'])) {
                $GLOBALS['epc_cp_footer_scripts'] = array();
            }
            $GLOBALS['epc_cp_footer_scripts'][] = '<script src="' . htmlspecialchars($jsUrl, ENT_QUOTES, 'UTF-8') . '"></script>';
        }
    }
}


if (!function_exists('epc_boc_render_top_nav')) {
    /**
     * ERP-style top mega-menu from BOC area registry groups.
     *
     * @param array<int, array{group:array,areas:array}> $nav
     */
    function epc_boc_render_top_nav(array $nav, string $base, string $active, array $brand, string $actionsHtml = ''): void
    {
        $h = 'epc_boc_h';
        $home = $base . '/control';
        echo '<nav class="epc-boc__topnav" id="epc_boc_topnav" aria-label="Business Operation Control">';
        echo '<div class="epc-boc__topnav-inner">';
        echo '<a class="epc-boc__topnav-brand" href="' . $h($home) . '"><i class="fa fa-cubes" aria-hidden="true"></i><span>' . $h($brand['short'] ?? 'BOC') . '</span></a>';
        echo '<ul class="epc-boc__topnav-list" role="menubar">';
        foreach ($nav as $gid => $g) {
            if (empty($g['areas']) || !is_array($g['areas'])) {
                continue;
            }
            $gKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $gid)) ?: 'group';
            $gLabel = (string) ($g['group']['label'] ?? $gKey);
            $gIcon = (string) ($g['group']['icon'] ?? 'fa-th-large');
            $isActive = false;
            foreach ($g['areas'] as $id => $area) {
                if ((string) $id === $active) {
                    $isActive = true;
                    break;
                }
            }
            $first = reset($g['areas']);
            $firstUrl = $base . '/' . ltrim((string) ($first['path'] ?? 'control'), '/');
            echo '<li class="epc-boc__topnav-item' . ($isActive ? ' is-active' : '') . '" data-boc-group="' . $h($gKey) . '" role="none">';
            echo '<button type="button" class="epc-boc__topnav-btn" role="menuitem" aria-haspopup="true" aria-expanded="false" data-boc-topnav-toggle="' . $h($gKey) . '">';
            echo '<i class="fa ' . $h($gIcon) . '" aria-hidden="true"></i>';
            echo '<span class="epc-boc__topnav-label">' . $h($gLabel) . '</span>';
            echo '<i class="fa fa-angle-down epc-boc__topnav-caret" aria-hidden="true"></i>';
            echo '</button>';
            echo '<div class="epc-boc__topnav-panel" role="menu" hidden data-boc-topnav-panel="' . $h($gKey) . '">';
            echo '<div class="epc-boc__topnav-panel-hd">';
            echo '<div class="epc-boc__topnav-panel-title"><i class="fa ' . $h($gIcon) . '"></i> ' . $h($gLabel) . '</div>';
            echo '<a class="epc-boc__topnav-panel-hub" href="' . $h($firstUrl) . '">Open panel <i class="fa fa-arrow-right"></i></a>';
            echo '</div>';
            // Multi-column mega-menu so large CP/ERP groups show every item.
            $chunks = array_chunk($g['areas'], 7, true);
            $colN = count($chunks);
            $colTitles = array('Modules', 'More', 'Also', 'More tools', 'Extra');
            echo '<div class="epc-boc__topnav-cols">';
            foreach ($chunks as $ci => $chunk) {
                $colLabel = $colN > 1 ? ($colTitles[$ci] ?? ('Panel ' . ($ci + 1))) : 'Modules';
                echo '<div class="epc-boc__topnav-col">';
                echo '<div class="epc-boc__topnav-col-hd"><i class="fa fa-sitemap"></i> ' . $h($colLabel) . '</div>';
                echo '<ul class="epc-boc__topnav-links">';
                foreach ($chunk as $id => $area) {
                    $url = $base . '/' . ltrim((string) ($area['path'] ?? ''), '/');
                    $cls = ((string) $id === $active) ? ' class="is-active"' : '';
                    echo '<li' . $cls . '><a href="' . $h($url) . '" title="' . $h($area['hint'] ?? '') . '">';
                    echo '<i class="fa ' . $h($area['icon'] ?? 'fa-circle-o') . '"></i> ' . $h($area['label'] ?? $id);
                    echo '</a></li>';
                }
                echo '</ul></div>';
            }
            echo '</div></div>';
            echo '</li>';
        }
        echo '</ul>';
        if ($actionsHtml !== '') {
            echo '<div class="epc-boc__topnav-actions">' . $actionsHtml . '</div>';
        }
        echo '</div></nav>';
    }
}

if (!function_exists('epc_boc_console_open')) {
    /**
     * Open the BOC console chrome and the content canvas.
     *
     * @param array{active?:string,title?:string,subtitle?:string,base?:string,operator?:string,env?:string,layout?:string} $ctx
     */
    function epc_boc_console_open(array $ctx = array()): void
    {
        // Idempotent: desktop auto-shell or page_frame may already have opened BOC.
        if (!empty($GLOBALS['epc_cp_boc_page'])) {
            return;
        }
        $brand = epc_boc_brand();
        $active = (string) ($ctx['active'] ?? '');
        $title = (string) ($ctx['title'] ?? 'Command Center');
        $subtitle = (string) ($ctx['subtitle'] ?? $brand['tagline']);
        $base = rtrim((string) ($ctx['base'] ?? '/cp'), '/');
        $operator = (string) ($ctx['operator'] ?? 'Operator');
        $env = (string) ($ctx['env'] ?? 'Production');
        $scope = (string) ($ctx['scope'] ?? 'All units · Fleet');
        $brandSub = (string) ($ctx['brand_sub'] ?? ($brand['control'] ?? 'Control'));
        $layout = strtolower(trim((string) ($ctx['layout'] ?? 'top')));
        if ($layout !== 'rail') {
            $layout = 'top';
        }
        $nav = (isset($ctx['nav']) && is_array($ctx['nav'])) ? $ctx['nav'] : epc_boc_nav();
        $h = 'epc_boc_h';
        $initials = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', $operator) ?: 'OP', 0, 2));
        // Flag the page as a BOC console so the CP template can hide the legacy
        // chrome before first paint (eliminates the blue-flash before the dark shell).
        $GLOBALS['epc_cp_boc_page'] = true;
        $GLOBALS['epc_boc_layout'] = $layout;
        // CSS/JS load from <head>/footer — never inline in .row (base-href code flash).
        epc_boc_console_enqueue_assets();
        $bocClass = 'epc-boc' . ($layout === 'top' ? ' epc-boc--topnav' : '');
        $bocStyle = $layout === 'top'
            ? 'display:flex!important;flex-direction:column!important;position:fixed!important;inset:0!important;z-index:4000!important;overflow:auto!important;width:100%!important;float:none!important;background:#fff7f7!important;isolation:isolate!important;'
            : 'display:flex!important;position:fixed!important;inset:0!important;z-index:4000!important;overflow:auto!important;width:100%!important;float:none!important;background:#fff7f7!important;isolation:isolate!important;';
        echo '<div class="' . $bocClass . '" style="' . $bocStyle . '">';

        $langSwitcher = '';
        if (function_exists('epc_cp_translate_render')) {
            try {
                $langSwitcher = (string) epc_cp_translate_render('erp');
            } catch (Throwable $e) {
                $langSwitcher = '';
            }
        }
        $actionsHtml = '';
        $actionsHtml .= '<span class="epc-boc__lang" id="epc-boc-lang-slot" title="Change language">' . $langSwitcher . '</span>';
        $actionsHtml .= '<span class="epc-boc__scope" title="What this login can see"><i class="fa fa-globe"></i> ' . $h($scope) . '</span>';
        $actionsHtml .= '<span class="epc-boc__env"><i class="fa fa-circle" style="font-size:7px;vertical-align:middle;color:#16a34a"></i> ' . $h($env) . '</span>';
        $actionsHtml .= '<span class="epc-boc__avatar" title="' . $h($operator) . '">' . $h($initials) . '</span>';

        if ($layout === 'top') {
            // One red top menu only — module flyouts + operator actions (no second bar).
            epc_boc_render_top_nav($nav, $base, $active, $brand, $actionsHtml);
        } else {
            echo '<aside class="epc-boc__rail" style="flex:0 0 260px!important;width:260px!important;float:none!important;background:#000000!important;opacity:1!important;">';
            echo '<div class="epc-boc__brand"><div class="epc-boc__brand-badge">' . $h($brand['short']) . '</div><div><div class="epc-boc__brand-name">' . $h($brand['short']) . '</div><div class="epc-boc__brand-sub">' . $h($brandSub) . '</div></div></div>';
            foreach ($nav as $g) {
                if (empty($g['areas'])) { continue; }
                echo '<div class="epc-boc__group"><div class="epc-boc__group-h"><i class="fa ' . $h($g['group']['icon']) . '"></i> ' . $h($g['group']['label']) . '</div><div class="epc-boc__nav">';
                foreach ($g['areas'] as $id => $area) {
                    $url = $base . '/' . ltrim((string) $area['path'], '/');
                    $cls = $id === $active ? ' class="is-active"' : '';
                    echo '<a href="' . $h($url) . '"' . $cls . ' title="' . $h($area['hint']) . '"><i class="fa ' . $h($area['icon']) . '"></i> ' . $h($area['label']) . '</a>';
                }
                echo '</div></div>';
            }
            echo '</aside>';
        }

        // Main + optional rail topbar (top layout uses single red menu only).
        echo '<div class="epc-boc__main" style="display:flex!important;flex-direction:column!important;flex:1 1 auto!important;min-width:0!important;width:auto!important;float:none!important;">';
        if ($layout !== 'top') {
            echo '<div class="epc-boc__topbar">';
            echo '<div class="epc-boc__crumb">' . $h($title) . '<small>' . $h($brand['name']) . '</small></div>';
            echo '<div class="epc-boc__search"><i class="fa fa-search"></i><input type="search" id="epc-boc-search" placeholder="Search tenants, orders, settings… (global)"></div>';
            echo '<div class="epc-boc__topbar-actions">' . $actionsHtml . '</div></div>';
        }
        // Language switcher relocate runs from epc_boc_topnav.js
        echo '<div class="epc-boc__canvas">';
        if ($layout === 'top' && $title !== '' && $active !== 'command_center') {
            echo '<div class="epc-boc__page-title"><h1>' . $h($title) . '</h1><small>' . $h($brand['name']) . '</small></div>';
        }
    }
}

if (!function_exists('epc_boc_console_close')) {
    function epc_boc_console_close(): void
    {
        if (empty($GLOBALS['epc_cp_boc_page'])) {
            return;
        }
        echo '</div></div></div>';
        $GLOBALS['epc_cp_boc_page'] = false;
        $GLOBALS['epc_boc_page_shell_open'] = false;
    }
}

if (!function_exists('epc_boc_render_command_center')) {
    /**
     * Render the Command Center canvas body (hero + tiles + fleet by type +
     * recent audit). Assumes the console is already open. Reusable by the BOC
     * home dashboard and the command-center route.
     */
    function epc_boc_render_command_center(PDO $db, string $base): void
    {
        $h = 'epc_boc_h';
        $brand = epc_boc_brand();
        $fleet = epc_boc_fleet($db);
        $summary = epc_boc_fleet_summary($fleet);
        $audit = epc_boc_audit_recent($db, 10);
        $base = rtrim($base, '/');

        $total = max(1, (int) $summary['total']);
        $healthy = (int) $summary['by_health']['green'];
        $attention = (int) $summary['by_health']['amber'];
        $critical = (int) $summary['by_health']['red'];
        $healthPct = (int) round(($healthy / $total) * 100);
        $gPct = (int) round(($healthy / $total) * 100);
        $aPct = (int) round(($attention / $total) * 100);
        $rPct = max(0, 100 - $gPct - $aPct);
        $commerce = (int) $summary['by_type']['commerce'];
        $erpOnly = (int) $summary['by_type']['erp_only'];
        $demo = (int) $summary['by_type']['demo'];

        // Hero
        echo '<div class="epc-boc__hero">';
        echo '<div><div class="epc-boc__hero-brand"><span>' . $h($brand['short']) . '</span> Business Operation System</div>';
        echo '<h2>Operations Command Center</h2>';
        echo '<p>' . $h($brand['tagline']) . '</p></div>';
        echo '<div class="epc-boc__hero-actions">';
        echo '<a class="epc-boc__btn epc-boc__btn--solid" href="' . $h($base . '/shop/tenant_hub/tenant_hub?tab=onboard') . '"><i class="fa fa-rocket"></i> Onboard client</a>';
        echo '<a class="epc-boc__btn epc-boc__btn--ghost" href="' . $h($base . '/control/portal/epc_platform_health_checkup') . '"><i class="fa fa-heartbeat"></i> Health checkup</a>';
        echo '<a class="epc-boc__btn epc-boc__btn--ghost" href="' . $h($base . '/control/cp_brochure') . '" target="_blank" rel="noopener"><i class="fa fa-book"></i> CP brochure</a>';
        echo '</div></div>';

        // KPI tiles
        $bar = static function (int $n, int $den): string {
            $pct = $den > 0 ? (int) round(($n / $den) * 100) : 0;
            return (string) max(4, min(100, $pct));
        };
        echo '<div class="epc-boc__tiles">';
        echo '<div class="epc-boc__tile epc-boc__tile--ink"><div class="epc-boc__tile-ico"><i class="fa fa-cubes"></i></div><div class="epc-boc__tile-label">Total units</div><div class="epc-boc__tile-val" data-boc-count="' . (int) $summary['total'] . '">0</div><div class="epc-boc__tile-hint">all tenant types</div><div class="epc-boc__tile-bar" style="--boc-bar:100%"><i></i></div></div>';
        echo '<div class="epc-boc__tile"><div class="epc-boc__tile-ico"><i class="fa fa-shopping-bag"></i></div><div class="epc-boc__tile-label">Commerce</div><div class="epc-boc__tile-val" data-boc-count="' . $commerce . '">0</div><div class="epc-boc__tile-hint">storefront + ERP</div><div class="epc-boc__tile-bar" style="--boc-bar:' . $bar($commerce, (int) $summary['total']) . '%"><i></i></div></div>';
        echo '<div class="epc-boc__tile"><div class="epc-boc__tile-ico"><i class="fa fa-university"></i></div><div class="epc-boc__tile-label">ERP-only</div><div class="epc-boc__tile-val" data-boc-count="' . $erpOnly . '">0</div><div class="epc-boc__tile-hint">finance clients</div><div class="epc-boc__tile-bar" style="--boc-bar:' . $bar($erpOnly, (int) $summary['total']) . '%"><i></i></div></div>';
        echo '<div class="epc-boc__tile"><div class="epc-boc__tile-ico"><i class="fa fa-flask"></i></div><div class="epc-boc__tile-label">Demo</div><div class="epc-boc__tile-val" data-boc-count="' . $demo . '">0</div><div class="epc-boc__tile-hint">sandboxes</div><div class="epc-boc__tile-bar" style="--boc-bar:' . $bar($demo, (int) $summary['total']) . '%"><i></i></div></div>';
        echo '<div class="epc-boc__tile epc-boc__tile--green"><div class="epc-boc__tile-ico"><i class="fa fa-check"></i></div><div class="epc-boc__tile-label">Healthy</div><div class="epc-boc__tile-val" data-boc-count="' . $healthy . '">0</div><div class="epc-boc__tile-hint">' . $healthPct . '% of fleet</div><div class="epc-boc__tile-bar" style="--boc-bar:' . $healthPct . '%"><i></i></div></div>';
        echo '<div class="epc-boc__tile epc-boc__tile--amber"><div class="epc-boc__tile-ico"><i class="fa fa-exclamation"></i></div><div class="epc-boc__tile-label">Attention</div><div class="epc-boc__tile-val" data-boc-count="' . $attention . '">0</div><div class="epc-boc__tile-bar" style="--boc-bar:' . $bar($attention, (int) $summary['total']) . '%"><i></i></div></div>';
        echo '<div class="epc-boc__tile epc-boc__tile--red"><div class="epc-boc__tile-ico"><i class="fa fa-bolt"></i></div><div class="epc-boc__tile-label">Critical</div><div class="epc-boc__tile-val" data-boc-count="' . $critical . '">0</div><div class="epc-boc__tile-bar" style="--boc-bar:' . $bar($critical, (int) $summary['total']) . '%"><i></i></div></div>';
        echo '</div>';

        // Graphical Quick actions (same Edit shortcuts builder as ERP / tenant CP)
        $bocShortcutsHtml = '';
        $shortcutUi = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_dash_shortcuts_ui.php';
        $shortcutLib = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_shortcut_icons.php';
        $helpersLib = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
        if (is_file($shortcutLib) && is_file($shortcutUi)) {
            require_once $shortcutLib;
            require_once $shortcutUi;
            if (is_file($helpersLib)) {
                require_once $helpersLib;
            }
            $bocAjax = $base . '/content/shop/finance/erp/ajax_erp.php';
            if (function_exists('epc_erp_configure_portal_urls')) {
                $bocUrls = epc_erp_configure_portal_urls('cp');
                if (!empty($bocUrls['erpAjaxUrl'])) {
                    $bocAjax = (string) $bocUrls['erpAjaxUrl'];
                }
            }
            $bocCsrf = '';
            if (class_exists('DP_User') && method_exists('DP_User', 'getAdminSession')) {
                $bocSess = DP_User::getAdminSession();
                if (is_array($bocSess) && !empty($bocSess['csrf_guard_key'])) {
                    $bocCsrf = (string) $bocSess['csrf_guard_key'];
                }
            }
            $bocCatalog = function_exists('epc_shortcuts_catalog_boc')
                ? epc_shortcuts_catalog_boc($base)
                : array();
            $bocDefaults = array('tenant_hub', 'health', 'channels', 'governance', 'audit', 'industry', 'onboard', 'brochure');
            $bocUid = function_exists('epc_shortcuts_user_id') ? epc_shortcuts_user_id() : 0;
            $bocItems = array();
            if ($bocUid > 0 && function_exists('epc_shortcuts_seed_defaults')) {
                epc_shortcuts_seed_defaults($db, $bocUid, 'cp', $bocDefaults, $bocCatalog);
                $bocItems = epc_shortcuts_as_tiles(epc_shortcuts_list_for_surface($db, $bocUid, 'cp'));
            } else {
                foreach ($bocDefaults as $dk) {
                    if (!isset($bocCatalog[$dk])) {
                        continue;
                    }
                    $c = $bocCatalog[$dk];
                    $bocItems[] = array(
                        'id' => 0,
                        'key' => $dk,
                        'label' => $c['label'],
                        'icon' => preg_replace('/^fa\s+/', '', (string) $c['icon']),
                        'color' => $c['color'],
                        'url' => $c['url'],
                        'tone' => $c['tone'] ?? 'red',
                    );
                }
            }
            if (function_exists('epc_dash_shortcuts_render')) {
                $bocShortcutsHtml = epc_dash_shortcuts_render(array(
                    'surface' => 'cp',
                    'variant' => 'cp',
                    'title' => 'Quick actions',
                    'ajax_url' => $bocAjax,
                    'csrf' => $bocCsrf,
                    'catalog' => $bocCatalog,
                    'items' => $bocItems,
                ));
            }
        }
        if ($bocShortcutsHtml !== '') {
            echo '<div class="epc-boc__qa-port">' . $bocShortcutsHtml . '</div>';
        }

        // Fleet pulse
        echo '<div class="epc-boc__cc-row" style="grid-template-columns:1fr;margin-bottom:18px">';
        echo '<div class="epc-boc__pulse">';
        echo '<div class="epc-boc__ring" style="--pct:' . $healthPct . '"><div class="epc-boc__ring-inner"><strong>' . $healthPct . '%</strong><span>Healthy</span></div></div>';
        echo '<div class="epc-boc__pulse-meta"><h3>Fleet health pulse</h3>';
        echo '<p>Live RAG across ' . (int) $summary['total'] . ' registered units. Green means storefront/ERP probes are clear; amber and red need operator attention.</p>';
        echo '<div class="epc-boc__stack"><i class="g" style="width:' . $gPct . '%"></i><i class="a" style="width:' . $aPct . '%"></i><i class="r" style="width:' . $rPct . '%"></i></div>';
        echo '<div class="epc-boc__legend">';
        echo '<span><i class="epc-boc__dot g"></i>Healthy <b>' . $healthy . '</b></span>';
        echo '<span><i class="epc-boc__dot a"></i>Attention <b>' . $attention . '</b></span>';
        echo '<span><i class="epc-boc__dot r"></i>Critical <b>' . $critical . '</b></span>';
        echo '</div></div></div>';
        echo '</div>';

        // Fleet cards by type
        $typeOrder = array('commerce', 'erp_only', 'demo');
        $typeIcons = array('commerce' => 'fa-shopping-bag', 'erp_only' => 'fa-university', 'demo' => 'fa-flask');
        $healthColor = array('green' => '#0a7d3c', 'amber' => '#b45309', 'red' => '#dc2626');
        foreach ($typeOrder as $type) {
            $rows = array_values(array_filter($fleet, static function ($f) use ($type) {
                return ($f['type'] ?? '') === $type;
            }));
            if (empty($rows)) {
                continue;
            }
            echo '<div class="epc-boc__fleet-sec"><div class="epc-boc__fleet-h">';
            echo '<h3><i class="fa ' . $h($typeIcons[$type] ?? 'fa-cube') . '"></i> ' . $h(epc_boc_type_label($type)) . '</h3>';
            echo '<span class="epc-boc__count">' . count($rows) . ' unit' . (count($rows) === 1 ? '' : 's') . '</span></div>';
            echo '<div class="epc-boc__fleet-grid">';
            foreach ($rows as $f) {
                $rag = (string) ($f['health'] ?? 'green');
                $hc = $healthColor[$rag] ?? '#0a7d3c';
                $status = (string) ($f['status'] ?? '');
                $statusLive = strtolower($status) === 'live';
                echo '<article class="epc-boc__tcard" style="--boc-health:' . $h($hc) . '">';
                echo '<div class="epc-boc__tcard-top"><div><p class="epc-boc__tcard-name">' . $h($f['label']) . '</p>';
                echo '<div class="epc-boc__tcard-key">' . $h($f['site_key']) . '</div></div>';
                echo '<span class="epc-boc__chip epc-boc__chip--' . $h($rag) . '">' . $h(strtoupper($rag)) . '</span></div>';
                echo '<div class="epc-boc__tcard-meta">';
                if ($status !== '') {
                    echo '<span class="epc-boc__pill' . ($statusLive ? ' epc-boc__pill--live' : '') . '">' . $h($status) . '</span>';
                }
                if (($f['industry'] ?? '') !== '') {
                    echo '<span class="epc-boc__pill"><i class="fa fa-tag"></i> ' . $h($f['industry']) . '</span>';
                }
                if (!empty($f['reasons'])) {
                    echo '<span class="epc-boc__pill" title="' . $h(implode('; ', $f['reasons'])) . '"><i class="fa fa-info-circle"></i> Notes</span>';
                }
                echo '</div><div class="epc-boc__tcard-actions">';
                if (!empty($f['urls']['cp'])) {
                    echo '<a class="primary" href="' . $h($f['urls']['cp']) . '" target="_blank" rel="noopener"><i class="fa fa-cog"></i> CP</a>';
                }
                if (!empty($f['urls']['erp'])) {
                    echo '<a href="' . $h($f['urls']['erp']) . '" target="_blank" rel="noopener"><i class="fa fa-university"></i> ERP</a>';
                }
                if (!empty($f['urls']['storefront'])) {
                    echo '<a href="' . $h($f['urls']['storefront']) . '" target="_blank" rel="noopener"><i class="fa fa-globe"></i> Site</a>';
                }
                echo '</div></article>';
            }
            echo '</div></div>';
        }
        if (empty($fleet)) {
            echo '<div class="epc-boc__empty"><p style="margin:0 0 12px">No tenants in the registry yet.</p>';
            echo '<a class="epc-boc__btn epc-boc__btn--ai" href="' . $h($base . '/shop/tenant_hub/tenant_hub?tab=onboard') . '"><i class="fa fa-rocket"></i> Onboard first client</a></div>';
        }

        // Recent activity timeline
        echo '<div class="epc-boc__panel"><div class="epc-boc__panel-h"><i class="fa fa-history"></i> Recent operator activity</div>';
        if (empty($audit)) {
            echo '<p style="color:#64748b;margin:0">No audited actions yet. Privileged operator actions (credential reveals, tenant toggles, governance edits) appear here.</p>';
        } else {
            echo '<ul class="epc-boc__timeline">';
            foreach ($audit as $row) {
                $when = date('Y-m-d H:i', (int) ($row['ts'] ?? 0));
                $actor = ($row['actor'] ?? '') !== '' ? (string) $row['actor'] : ('#' . (int) ($row['user_id'] ?? 0));
                $action = trim((string) ($row['action'] ?? ''));
                $area = (string) ($row['area'] ?? '');
                $target = (string) ($row['target'] ?? '');
                echo '<li><span class="epc-boc__timeline-dot"></span><div class="epc-boc__timeline-body">';
                echo '<em>' . $h($when) . '</em><strong>' . $h($action !== '' ? $action : 'Action') . '</strong>';
                echo '<span>' . $h($actor) . ' · <span class="epc-boc__code">' . $h($area) . '</span>';
                if ($target !== '') {
                    echo ' · ' . $h($target);
                }
                echo '</span></div></li>';
            }
            echo '</ul>';
            echo '<p style="margin:0 0 16px"><a class="epc-boc__btn" href="' . $h($base . '/control/portal/epc_boc_audit_log') . '"><i class="fa fa-list"></i> Full audit log</a></p>';
        }
        echo '</div>';

        // Count-up motion for KPI values
        echo '<script>(function(){var nodes=document.querySelectorAll("[data-boc-count]");if(!nodes.length)return;nodes.forEach(function(el){var target=parseInt(el.getAttribute("data-boc-count"),10)||0;var start=null;var dur=700;function step(ts){if(!start)start=ts;var p=Math.min((ts-start)/dur,1);var eased=1-Math.pow(1-p,3);el.textContent=Math.round(eased*target);if(p<1)requestAnimationFrame(step);}requestAnimationFrame(step);});})();</script>';
    }
}
