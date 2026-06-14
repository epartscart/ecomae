<?php
/**
 * BOC console shell — the professional "control-room" chrome for Business
 * Operation Control. Renders a fixed top command bar + grouped left rail
 * (from the area registry) + a clean content canvas, and neutralises the
 * legacy CP theme chrome on BOC pages so BOC has its own complete look.
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
        return <<<CSS
:root{--boc-bg:#0b1220;--boc-rail:#101b33;--boc-rail-2:#0a1326;--boc-line:#1e2c48;--boc-ink:#eaf0fb;--boc-mut:#8295b5;--boc-accent:#2ad4c4;--boc-accent-2:#2f6df6;--boc-canvas:#eef1f5;--boc-card:#ffffff;--boc-card-line:#e3e8ef;--boc-green:#0a7d3c;--boc-amber:#b45309;--boc-red:#c01829;--boc-ink-1:#0d1626;--boc-ink-2:#475569;--boc-ink-3:#6b7a90;}
/* Neutralise legacy CP chrome on BOC pages */
body.epc-boc-mode #menu,body.epc-boc-mode .navbar-static-side,body.epc-boc-mode nav.navbar-default,body.epc-boc-mode #top-navigation,body.epc-boc-mode .epc-cp-topbar{display:none!important;}
body.epc-boc-mode #wrapper,body.epc-boc-mode.epc-cp-shell #wrapper{margin-left:0!important;width:100%!important;max-width:100%!important;background:var(--boc-canvas)!important;}
body.epc-boc-mode .content,body.epc-boc-mode #wrapper .content{padding:0!important;margin:0!important;background:var(--boc-canvas)!important;}
body.epc-boc-mode{background:var(--boc-canvas)!important;}
.epc-boc{font-family:"Inter","Segoe UI",-apple-system,BlinkMacSystemFont,Roboto,Helvetica,Arial,sans-serif;color:var(--boc-ink-1);display:flex;min-height:100vh;position:fixed;inset:0;z-index:3000;overflow:auto;background:var(--boc-canvas);-webkit-font-smoothing:antialiased;font-feature-settings:"cv02","cv03","cv04","tnum";}
/* :has() fallback: hide legacy CP chrome when a BOC console is present (no JS / body-class dependency) */
body:has(.epc-boc) #header,body:has(.epc-boc) #menu,body:has(.epc-boc) #right-sidebar,body:has(.epc-boc) .footer{display:none!important;}
body:has(.epc-boc) #wrapper{margin:0!important;padding:0!important;width:100%!important;}
body:has(.epc-boc){overflow:hidden!important;}
/* Beat legacy .epc-cp-shell rules that force .content>.row>div to display:block */
body.epc-cp-shell .content .epc-boc,body.epc-cp .content .epc-boc,.epc-cp-shell .epc-boc{display:flex!important;overflow:auto!important;width:100%!important;float:none!important;}
body.epc-cp-shell .content .epc-boc__main,.epc-cp-shell .epc-boc__main{display:flex!important;flex-direction:column!important;flex:1 1 auto!important;min-width:0!important;width:auto!important;}
body.epc-cp-shell .content .epc-boc__rail,.epc-cp-shell .epc-boc__rail{flex:0 0 260px!important;width:260px!important;}
.epc-boc *{box-sizing:border-box;}
.epc-boc a{text-decoration:none;}
/* Left rail (shell sidebar) */
.epc-boc__rail{width:260px;flex:0 0 260px;background:linear-gradient(180deg,var(--boc-rail),var(--boc-rail-2));color:var(--boc-ink);position:sticky;top:0;align-self:flex-start;height:100vh;overflow-y:auto;border-right:1px solid var(--boc-line);scrollbar-width:thin;scrollbar-color:#2a3a5c transparent;}
.epc-boc__rail::-webkit-scrollbar{width:8px;}.epc-boc__rail::-webkit-scrollbar-thumb{background:#2a3a5c;border-radius:8px;}
.epc-boc__brand{display:flex;align-items:center;gap:11px;padding:18px 18px 16px;border-bottom:1px solid var(--boc-line);position:sticky;top:0;background:linear-gradient(180deg,var(--boc-rail),rgba(16,27,51,.92));backdrop-filter:blur(6px);z-index:2;}
.epc-boc__brand-badge{width:36px;height:36px;border-radius:10px;background:linear-gradient(135deg,var(--boc-accent),var(--boc-accent-2));display:flex;align-items:center;justify-content:center;font-weight:800;color:#04121f;font-size:14px;letter-spacing:.3px;box-shadow:0 4px 14px rgba(47,109,246,.35);}
.epc-boc__brand-name{font-weight:750;font-size:16px;line-height:1.05;letter-spacing:.2px;}
.epc-boc__brand-sub{font-size:9.5px;color:var(--boc-mut);letter-spacing:2px;text-transform:uppercase;margin-top:2px;}
.epc-boc__group{padding:12px 12px 2px;}
.epc-boc__group-h{font-size:9.5px;letter-spacing:1.4px;text-transform:uppercase;color:var(--boc-mut);padding:4px 10px;display:flex;align-items:center;gap:8px;font-weight:700;}
.epc-boc__nav{display:flex;flex-direction:column;gap:1px;margin-top:2px;}
.epc-boc__nav a{display:flex;align-items:center;gap:11px;padding:8px 11px;border-radius:8px;color:#bccbe6;font-size:12.5px;font-weight:500;transition:background .12s,color .12s;}
.epc-boc__nav a .fa{width:16px;text-align:center;color:var(--boc-mut);font-size:13px;}
.epc-boc__nav a:hover{background:rgba(47,109,246,.14);color:#fff;}
.epc-boc__nav a:hover .fa{color:#bcd0ff;}
.epc-boc__nav a.is-active{background:linear-gradient(90deg,rgba(42,212,196,.18),rgba(47,109,246,.10));color:#fff;font-weight:600;box-shadow:inset 3px 0 0 var(--boc-accent);}
.epc-boc__nav a.is-active .fa{color:var(--boc-accent);}
/* Main */
.epc-boc__main{flex:1;min-width:0;display:flex;flex-direction:column;}
.epc-boc__topbar{position:sticky;top:0;z-index:20;display:flex;align-items:center;gap:14px;background:rgba(255,255,255,.92);backdrop-filter:saturate(1.4) blur(8px);border-bottom:1px solid var(--boc-card-line);padding:11px 24px;box-shadow:0 1px 2px rgba(13,22,38,.04);}
.epc-boc__crumb{font-weight:700;font-size:15px;color:var(--boc-ink-1);letter-spacing:.2px;}
.epc-boc__crumb small{display:block;font-weight:500;font-size:10.5px;color:var(--boc-ink-3);letter-spacing:.3px;margin-top:1px;}
.epc-boc__search{flex:1;max-width:480px;position:relative;}
.epc-boc__search input{width:100%;border:1px solid var(--boc-card-line);background:#f4f6fa;border-radius:9px;padding:8px 12px 8px 34px;font-size:13px;outline:none;transition:box-shadow .12s,border-color .12s,background .12s;}
.epc-boc__search input:focus{border-color:var(--boc-accent-2);background:#fff;box-shadow:0 0 0 3px rgba(47,109,246,.14);}
.epc-boc__search .fa{position:absolute;left:12px;top:9px;color:#94a3b8;font-size:13px;}
.epc-boc__topbar-actions{margin-left:auto;display:flex;align-items:center;gap:9px;}
.epc-boc__lang{display:inline-flex;align-items:center;}
.epc-boc__lang>.epc-cp-translate-nav,.epc-boc__lang>.epc-cp-translate{list-style:none;margin:0;padding:0;display:inline-flex;align-items:center;}
.epc-boc__lang select.epc-cp-translate__select{height:32px;font-size:12px;min-width:118px;}
.epc-boc__env{font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:#0a7d3c;background:#e7f6ec;border:1px solid #bfe6cd;border-radius:20px;padding:3px 10px;}
.epc-boc__scope{font-size:11px;font-weight:700;color:var(--boc-ink-2);background:#eef2f7;border:1px solid #dde4ee;border-radius:20px;padding:3px 11px;display:inline-flex;align-items:center;gap:6px;}
.epc-boc__scope .fa{color:var(--boc-accent-2);font-size:11px;}
.epc-boc__btn{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--boc-card-line);background:#fff;color:var(--boc-ink-2);border-radius:8px;padding:7px 12px;font-size:12px;font-weight:600;cursor:pointer;}
.epc-boc__btn:hover{background:#f4f6fa;color:var(--boc-ink-1);}
.epc-boc__btn--ai{background:linear-gradient(135deg,var(--boc-accent),var(--boc-accent-2));color:#04121f;border:none;box-shadow:0 3px 10px rgba(47,109,246,.28);}
.epc-boc__btn--ai:hover{filter:brightness(1.05);color:#04121f;}
.epc-boc__avatar{width:32px;height:32px;border-radius:50%;background:linear-gradient(135deg,#334155,#0d1626);color:#fff;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;letter-spacing:.3px;}
.epc-boc__canvas{padding:24px 26px 30px;flex:1;max-width:1640px;width:100%;}
/* Hero */
.epc-boc__hero{background:linear-gradient(120deg,#0d1c39 0%,#11346b 55%,#0a5f73 100%);color:#fff;border-radius:14px;padding:22px 26px;margin-bottom:20px;display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;position:relative;overflow:hidden;box-shadow:0 10px 30px rgba(13,28,57,.18);}
.epc-boc__hero::after{content:"";position:absolute;right:-60px;top:-60px;width:240px;height:240px;border-radius:50%;background:radial-gradient(circle,rgba(42,212,196,.22),transparent 70%);pointer-events:none;z-index:0;}
.epc-boc__hero>div{position:relative;z-index:1;}
.epc-boc__hero h2{margin:8px 0 5px;font-size:22px;font-weight:750;letter-spacing:.2px;color:#fff!important;}
.epc-boc__hero p{margin:0;opacity:.86;font-size:13px;max-width:680px;line-height:1.5;color:#fff;}
/* Analytical KPI cards */
.epc-boc__tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(168px,1fr));gap:14px;margin-bottom:20px;}
.epc-boc__tile{background:var(--boc-card);border:1px solid var(--boc-card-line);border-radius:12px;padding:15px 17px;position:relative;overflow:hidden;box-shadow:0 1px 2px rgba(13,22,38,.04);transition:box-shadow .15s,transform .15s;}
.epc-boc__tile:hover{box-shadow:0 6px 18px rgba(13,22,38,.09);transform:translateY(-1px);}
.epc-boc__tile-label{font-size:10.5px;color:var(--boc-ink-3);text-transform:uppercase;letter-spacing:.7px;font-weight:600;}
.epc-boc__tile-val{font-size:27px;font-weight:800;color:var(--boc-ink-1);line-height:1.05;margin-top:6px;font-variant-numeric:tabular-nums;letter-spacing:-.5px;}
.epc-boc__tile-hint{font-size:11px;color:#94a3b8;margin-top:3px;}
.epc-boc__tile--green{box-shadow:inset 4px 0 0 var(--boc-green);} .epc-boc__tile--green .epc-boc__tile-val{color:var(--boc-green);}
.epc-boc__tile--amber{box-shadow:inset 4px 0 0 var(--boc-amber);} .epc-boc__tile--amber .epc-boc__tile-val{color:var(--boc-amber);}
.epc-boc__tile--red{box-shadow:inset 4px 0 0 var(--boc-red);} .epc-boc__tile--red .epc-boc__tile-val{color:var(--boc-red);}
/* Panels */
.epc-boc__panel{background:var(--boc-card);border:1px solid var(--boc-card-line);border-radius:12px;padding:0;margin-bottom:18px;overflow:hidden;box-shadow:0 1px 2px rgba(13,22,38,.04);}
.epc-boc__panel-h{display:flex;align-items:center;gap:9px;font-size:13.5px;font-weight:700;color:var(--boc-ink-1);margin:0;padding:14px 18px;border-bottom:1px solid var(--boc-card-line);background:#fafbfd;}
.epc-boc__panel-h .fa{color:var(--boc-accent-2);}
.epc-boc__panel>:not(.epc-boc__panel-h){padding-left:18px;padding-right:18px;}
.epc-boc__panel>table{padding:0;}
/* Enterprise data grid */
.epc-boc table{width:100%;border-collapse:collapse;font-size:12.5px;}
.epc-boc table th{text-align:left;font-size:10.5px;text-transform:uppercase;letter-spacing:.6px;color:var(--boc-ink-3);font-weight:700;padding:10px 14px;background:#f6f8fb;border-bottom:1px solid var(--boc-card-line);position:sticky;top:0;z-index:1;}
.epc-boc table td{padding:11px 14px;border-bottom:1px solid #eef2f7;vertical-align:middle;color:var(--boc-ink-2);}
.epc-boc table tbody tr:nth-child(even) td{background:#fcfdfe;}
.epc-boc table tbody tr:hover td{background:#eff5ff;}
.epc-boc table td strong{color:var(--boc-ink-1);font-weight:650;}
/* Numeric grids (supply control rooms): right-align metric columns (3+) with tabular figures */
.epc-boc__grid--metric th:nth-child(n+3),.epc-boc__grid--metric td:nth-child(n+3){text-align:right;font-variant-numeric:tabular-nums;}
.epc-boc__chip{display:inline-block;font-size:10px;font-weight:700;letter-spacing:.4px;color:#fff;border-radius:20px;padding:2px 9px;text-transform:uppercase;}
.epc-boc__chip--green{background:var(--boc-green);} .epc-boc__chip--amber{background:var(--boc-amber);} .epc-boc__chip--red{background:var(--boc-red);}
.epc-boc__chip--type{background:#475569;}
.epc-boc__mini{display:inline-flex;align-items:center;justify-content:center;width:27px;height:25px;border:1px solid var(--boc-card-line);border-radius:7px;color:#475569;margin-right:3px;}
.epc-boc__mini:hover{background:#eff5ff;color:var(--boc-accent-2);border-color:#bcd0ff;}
.epc-boc__sectitle{font-size:13px;font-weight:700;color:var(--boc-ink-1);margin:6px 0 8px;display:flex;align-items:center;gap:8px;}
.epc-boc__code{font-family:ui-monospace,"SFMono-Regular",Menlo,monospace;font-size:11px;color:#64748b;}
@media(max-width:980px){.epc-boc__rail{display:none;}.epc-boc__canvas{padding:16px;}}
CSS;
    }
}

if (!function_exists('epc_boc_console_open')) {
    /**
     * Open the BOC console chrome and the content canvas.
     *
     * @param array{active?:string,title?:string,subtitle?:string,base?:string,operator?:string,env?:string} $ctx
     */
    function epc_boc_console_open(array $ctx = array()): void
    {
        $brand = epc_boc_brand();
        $active = (string) ($ctx['active'] ?? '');
        $title = (string) ($ctx['title'] ?? 'Command Center');
        $subtitle = (string) ($ctx['subtitle'] ?? $brand['tagline']);
        $base = rtrim((string) ($ctx['base'] ?? '/cp'), '/');
        $operator = (string) ($ctx['operator'] ?? 'Operator');
        $env = (string) ($ctx['env'] ?? 'Production');
        $scope = (string) ($ctx['scope'] ?? 'All units · Fleet');
        $brandSub = (string) ($ctx['brand_sub'] ?? ($brand['control'] ?? 'Control'));
        $nav = (isset($ctx['nav']) && is_array($ctx['nav'])) ? $ctx['nav'] : epc_boc_nav();
        $h = 'epc_boc_h';
        $initials = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', $operator) ?: 'OP', 0, 2));
        // Flag the page as a BOC console so the CP template can hide the legacy
        // chrome before first paint (eliminates the blue-flash before the dark shell).
        $GLOBALS['epc_cp_boc_page'] = true;
        echo "<style>" . epc_boc_console_css() . "</style>\n";
        echo "<script>(function(){try{document.body.classList.add('epc-boc-mode');}catch(e){}})();</script>\n";
        echo '<div class="epc-boc" style="display:flex!important;position:fixed!important;inset:0!important;z-index:3000!important;overflow:auto!important;width:100%!important;float:none!important;">';
        // Rail
        echo '<aside class="epc-boc__rail" style="flex:0 0 250px!important;width:250px!important;float:none!important;">';
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
        // Main + topbar
        echo '<div class="epc-boc__main" style="display:flex!important;flex-direction:column!important;flex:1 1 auto!important;min-width:0!important;width:auto!important;float:none!important;">';
        echo '<div class="epc-boc__topbar">';
        echo '<div class="epc-boc__crumb">' . $h($title) . '<small>' . $h($brand['name']) . '</small></div>';
        echo '<div class="epc-boc__search"><i class="fa fa-search"></i><input type="search" id="epc-boc-search" placeholder="Search tenants, orders, settings… (global)"></div>';
        echo '<div class="epc-boc__topbar-actions">';
        $langSwitcher = function_exists('epc_cp_translate_render') ? epc_cp_translate_render('erp') : '';
        echo '<span class="epc-boc__lang" id="epc-boc-lang-slot" title="Change language">' . $langSwitcher . '</span>';
        echo '<span class="epc-boc__scope" title="What this login can see"><i class="fa fa-globe"></i> ' . $h($scope) . '</span>';
        echo '<span class="epc-boc__env"><i class="fa fa-circle" style="font-size:7px;vertical-align:middle;color:#16a34a"></i> ' . $h($env) . '</span>';
        echo '<a class="epc-boc__btn epc-boc__btn--ai" href="#" id="epc-boc-copilot"><i class="fa fa-magic"></i> AI Copilot</a>';
        echo '<a class="epc-boc__btn" href="#" title="Notifications"><i class="fa fa-bell"></i></a>';
        echo '<span class="epc-boc__avatar" title="' . $h($operator) . '">' . $h($initials) . '</span>';
        echo '</div></div>';
        // The legacy CP language switcher renders (hidden) inside #header on live
        // CP pages; relocate it into the BOS topbar so language can be changed.
        echo '<script>(function(){function go(){var s=document.getElementById("epc-boc-lang-slot");if(!s){return;}if(s.querySelector("select")){return;}var w=document.querySelector(".epc-cp-translate-nav,.epc-cp-translate");if(w){s.appendChild(w);}}if(document.readyState!=="loading"){go();}else{document.addEventListener("DOMContentLoaded",go);}setTimeout(go,900);})();</script>';
        echo '<div class="epc-boc__canvas">';
    }
}

if (!function_exists('epc_boc_console_close')) {
    function epc_boc_console_close(): void
    {
        echo '</div></div></div>';
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
        echo '<div class="epc-boc__hero"><div><span class="epc-boc__env" style="background:rgba(255,255,255,.15);color:#fff;border-color:rgba(255,255,255,.25)">' . $h($brand['short']) . '</span><h2><i class="fa fa-tachometer"></i> Operations Command Center</h2><p>' . $h($brand['tagline']) . '</p></div>';
        echo '<div><a class="epc-boc__btn" style="background:#fff" href="' . $h($base . '/shop/tenant_hub/tenant_hub?tab=onboard') . '"><i class="fa fa-rocket"></i> Onboard client</a></div></div>';
        // Tiles
        echo '<div class="epc-boc__tiles">';
        echo '<div class="epc-boc__tile"><div class="epc-boc__tile-label">Total units</div><div class="epc-boc__tile-val">' . (int) $summary['total'] . '</div><div class="epc-boc__tile-hint">all tenant types</div></div>';
        echo '<div class="epc-boc__tile"><div class="epc-boc__tile-label">Commerce</div><div class="epc-boc__tile-val">' . (int) $summary['by_type']['commerce'] . '</div><div class="epc-boc__tile-hint">storefront + ERP</div></div>';
        echo '<div class="epc-boc__tile"><div class="epc-boc__tile-label">ERP-only</div><div class="epc-boc__tile-val">' . (int) $summary['by_type']['erp_only'] . '</div><div class="epc-boc__tile-hint">finance clients</div></div>';
        echo '<div class="epc-boc__tile"><div class="epc-boc__tile-label">Demo</div><div class="epc-boc__tile-val">' . (int) $summary['by_type']['demo'] . '</div><div class="epc-boc__tile-hint">sandboxes</div></div>';
        echo '<div class="epc-boc__tile epc-boc__tile--green"><div class="epc-boc__tile-label">Healthy</div><div class="epc-boc__tile-val">' . (int) $summary['by_health']['green'] . '</div></div>';
        echo '<div class="epc-boc__tile epc-boc__tile--amber"><div class="epc-boc__tile-label">Attention</div><div class="epc-boc__tile-val">' . (int) $summary['by_health']['amber'] . '</div></div>';
        echo '<div class="epc-boc__tile epc-boc__tile--red"><div class="epc-boc__tile-label">Critical</div><div class="epc-boc__tile-val">' . (int) $summary['by_health']['red'] . '</div></div>';
        echo '</div>';
        // Fleet by type
        $typeOrder = array('commerce', 'erp_only', 'demo');
        foreach ($typeOrder as $type) {
            $rows = array_values(array_filter($fleet, static function ($f) use ($type) { return ($f['type'] ?? '') === $type; }));
            if (empty($rows)) { continue; }
            echo '<div class="epc-boc__panel"><div class="epc-boc__sectitle"><i class="fa fa-cube"></i> ' . $h(epc_boc_type_label($type)) . ' <span style="color:#94a3b8;font-weight:400">(' . count($rows) . ')</span></div>';
            echo '<table><thead><tr><th>Tenant</th><th>Industry</th><th>Status</th><th>Health</th><th>Notes</th><th>Open</th></tr></thead><tbody>';
            foreach ($rows as $f) {
                echo '<tr><td><strong>' . $h($f['label']) . '</strong><br><span class="epc-boc__code">' . $h($f['site_key']) . '</span></td>';
                echo '<td style="color:#64748b">' . $h($f['industry']) . '</td>';
                echo '<td>' . $h($f['status'] !== '' ? $f['status'] : '—') . '</td>';
                echo '<td><span class="epc-boc__chip epc-boc__chip--' . $h($f['health']) . '">' . $h(strtoupper($f['health'])) . '</span></td>';
                echo '<td style="color:#94a3b8;font-size:11px">' . ($f['reasons'] ? $h(implode('; ', $f['reasons'])) : '—') . '</td><td>';
                if (!empty($f['urls']['cp'])) { echo '<a class="epc-boc__mini" href="' . $h($f['urls']['cp']) . '" target="_blank" title="Control panel"><i class="fa fa-cog"></i></a>'; }
                if (!empty($f['urls']['erp'])) { echo '<a class="epc-boc__mini" href="' . $h($f['urls']['erp']) . '" target="_blank" title="ERP"><i class="fa fa-university"></i></a>'; }
                if (!empty($f['urls']['storefront'])) { echo '<a class="epc-boc__mini" href="' . $h($f['urls']['storefront']) . '" target="_blank" title="Storefront"><i class="fa fa-shopping-cart"></i></a>'; }
                echo '</td></tr>';
            }
            echo '</tbody></table></div>';
        }
        if (empty($fleet)) {
            echo '<div class="epc-boc__panel"><p style="color:#64748b;margin:0">No tenants in the registry yet. Onboard one from the <a href="' . $h($base . '/shop/tenant_hub/tenant_hub?tab=onboard') . '">Tenant hub</a>.</p></div>';
        }
        // Recent activity
        echo '<div class="epc-boc__panel"><div class="epc-boc__panel-h"><i class="fa fa-history"></i> Recent operator activity</div>';
        if (empty($audit)) {
            echo '<p style="color:#64748b;margin:0">No audited actions yet. Privileged operator actions (credential reveals, tenant toggles, governance edits) appear here.</p>';
        } else {
            echo '<table><thead><tr><th>When</th><th>Operator</th><th>Area</th><th>Action</th><th>Target</th></tr></thead><tbody>';
            foreach ($audit as $row) {
                echo '<tr><td style="color:#94a3b8;white-space:nowrap">' . $h(date('Y-m-d H:i', (int) ($row['ts'] ?? 0))) . '</td>';
                echo '<td>' . $h($row['actor'] !== '' ? $row['actor'] : ('#' . (int) ($row['user_id'] ?? 0))) . '</td>';
                echo '<td><span class="epc-boc__code">' . $h($row['area'] ?? '') . '</span></td>';
                echo '<td>' . $h($row['action'] ?? '') . '</td><td style="color:#64748b">' . $h($row['target'] ?? '') . '</td></tr>';
            }
            echo '</tbody></table><p style="margin:12px 0 0"><a class="epc-boc__btn" href="' . $h($base . '/control/portal/epc_boc_audit_log') . '"><i class="fa fa-list"></i> Full audit log</a></p>';
        }
        echo '</div>';
    }
}
