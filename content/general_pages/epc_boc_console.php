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

if (!function_exists('epc_boc_console_css')) {
    /** The BOC design system. Scoped under .epc-boc / body.epc-boc-mode. */
    function epc_boc_console_css(): string
    {
        return <<<CSS
:root{--boc-bg:#0b1220;--boc-rail:#0e1830;--boc-rail-2:#0b1426;--boc-line:#1c2942;--boc-ink:#e6edf7;--boc-mut:#8aa0c2;--boc-accent:#22d3ee;--boc-accent-2:#3b82f6;--boc-canvas:#f1f5f9;--boc-card:#ffffff;--boc-card-line:#e2e8f0;--boc-green:#16a34a;--boc-amber:#d97706;--boc-red:#dc2626;}
/* Neutralise legacy CP chrome on BOC pages */
body.epc-boc-mode #menu,body.epc-boc-mode .navbar-static-side,body.epc-boc-mode nav.navbar-default,body.epc-boc-mode #top-navigation,body.epc-boc-mode .epc-cp-topbar{display:none!important;}
body.epc-boc-mode #wrapper,body.epc-boc-mode.epc-cp-shell #wrapper{margin-left:0!important;width:100%!important;max-width:100%!important;background:var(--boc-canvas)!important;}
body.epc-boc-mode .content,body.epc-boc-mode #wrapper .content{padding:0!important;margin:0!important;background:var(--boc-canvas)!important;}
body.epc-boc-mode{background:var(--boc-canvas)!important;}
.epc-boc{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;color:#0f172a;display:flex;min-height:100vh;}
.epc-boc *{box-sizing:border-box;}
/* Left rail */
.epc-boc__rail{width:250px;flex:0 0 250px;background:linear-gradient(180deg,var(--boc-rail),var(--boc-rail-2));color:var(--boc-ink);position:sticky;top:0;align-self:flex-start;height:100vh;overflow-y:auto;border-right:1px solid var(--boc-line);}
.epc-boc__brand{display:flex;align-items:center;gap:10px;padding:18px 18px 14px;border-bottom:1px solid var(--boc-line);}
.epc-boc__brand-badge{width:34px;height:34px;border-radius:9px;background:linear-gradient(135deg,var(--boc-accent),var(--boc-accent-2));display:flex;align-items:center;justify-content:center;font-weight:800;color:#04121f;font-size:14px;letter-spacing:.5px;}
.epc-boc__brand-name{font-weight:700;font-size:15px;line-height:1.1;}
.epc-boc__brand-sub{font-size:10px;color:var(--boc-mut);letter-spacing:1.5px;text-transform:uppercase;}
.epc-boc__group{padding:14px 12px 2px;}
.epc-boc__group-h{font-size:10px;letter-spacing:1.3px;text-transform:uppercase;color:var(--boc-mut);padding:4px 10px;display:flex;align-items:center;gap:7px;}
.epc-boc__nav{display:flex;flex-direction:column;gap:1px;margin-top:3px;}
.epc-boc__nav a{display:flex;align-items:center;gap:10px;padding:8px 11px;border-radius:8px;color:#c7d4ea;text-decoration:none;font-size:13px;transition:all .12s;}
.epc-boc__nav a .fa{width:16px;text-align:center;color:var(--boc-mut);font-size:13px;}
.epc-boc__nav a:hover{background:rgba(59,130,246,.12);color:#fff;}
.epc-boc__nav a.is-active{background:linear-gradient(90deg,rgba(34,211,238,.16),rgba(59,130,246,.10));color:#fff;box-shadow:inset 3px 0 0 var(--boc-accent);}
.epc-boc__nav a.is-active .fa{color:var(--boc-accent);}
/* Main */
.epc-boc__main{flex:1;min-width:0;display:flex;flex-direction:column;}
.epc-boc__topbar{position:sticky;top:0;z-index:20;display:flex;align-items:center;gap:14px;background:#fff;border-bottom:1px solid var(--boc-card-line);padding:11px 22px;box-shadow:0 1px 3px rgba(15,23,42,.05);}
.epc-boc__crumb{font-weight:700;font-size:15px;color:#0f172a;}
.epc-boc__crumb small{display:block;font-weight:400;font-size:11px;color:#64748b;}
.epc-boc__search{flex:1;max-width:460px;position:relative;}
.epc-boc__search input{width:100%;border:1px solid var(--boc-card-line);background:#f8fafc;border-radius:9px;padding:8px 12px 8px 34px;font-size:13px;outline:none;}
.epc-boc__search input:focus{border-color:var(--boc-accent-2);background:#fff;box-shadow:0 0 0 3px rgba(59,130,246,.12);}
.epc-boc__search .fa{position:absolute;left:12px;top:9px;color:#94a3b8;font-size:13px;}
.epc-boc__topbar-actions{margin-left:auto;display:flex;align-items:center;gap:8px;}
.epc-boc__env{font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:#0369a1;background:#e0f2fe;border:1px solid #bae6fd;border-radius:20px;padding:3px 10px;}
.epc-boc__btn{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--boc-card-line);background:#fff;color:#334155;border-radius:8px;padding:7px 12px;font-size:12px;font-weight:600;cursor:pointer;text-decoration:none;}
.epc-boc__btn:hover{background:#f1f5f9;color:#0f172a;}
.epc-boc__btn--ai{background:linear-gradient(135deg,var(--boc-accent),var(--boc-accent-2));color:#04121f;border:none;}
.epc-boc__btn--ai:hover{filter:brightness(1.05);color:#04121f;}
.epc-boc__avatar{width:32px;height:32px;border-radius:50%;background:linear-gradient(135deg,#334155,#0f172a);color:#fff;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;}
.epc-boc__canvas{padding:22px;flex:1;}
/* Cards / tiles */
.epc-boc__hero{background:linear-gradient(135deg,#0c4a6e,#0369a1);color:#fff;border-radius:14px;padding:22px 24px;margin-bottom:18px;display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;}
.epc-boc__hero h2{margin:6px 0 4px;font-size:21px;}
.epc-boc__hero p{margin:0;opacity:.9;font-size:13px;max-width:640px;}
.epc-boc__tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:14px;margin-bottom:18px;}
.epc-boc__tile{background:var(--boc-card);border:1px solid var(--boc-card-line);border-radius:12px;padding:15px 16px;position:relative;overflow:hidden;}
.epc-boc__tile-label{font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:.6px;}
.epc-boc__tile-val{font-size:26px;font-weight:800;color:#0f172a;line-height:1.1;margin-top:4px;}
.epc-boc__tile-hint{font-size:11px;color:#94a3b8;margin-top:2px;}
.epc-boc__tile--green{box-shadow:inset 4px 0 0 var(--boc-green);} .epc-boc__tile--green .epc-boc__tile-val{color:var(--boc-green);}
.epc-boc__tile--amber{box-shadow:inset 4px 0 0 var(--boc-amber);} .epc-boc__tile--amber .epc-boc__tile-val{color:var(--boc-amber);}
.epc-boc__tile--red{box-shadow:inset 4px 0 0 var(--boc-red);} .epc-boc__tile--red .epc-boc__tile-val{color:var(--boc-red);}
.epc-boc__panel{background:var(--boc-card);border:1px solid var(--boc-card-line);border-radius:12px;padding:18px;margin-bottom:18px;}
.epc-boc__panel-h{display:flex;align-items:center;gap:9px;font-size:14px;font-weight:700;color:#0f172a;margin:0 0 12px;padding-bottom:10px;border-bottom:1px solid var(--boc-card-line);}
.epc-boc__panel-h .fa{color:var(--boc-accent-2);}
.epc-boc table{width:100%;border-collapse:collapse;font-size:13px;}
.epc-boc table th{text-align:left;font-size:11px;text-transform:uppercase;letter-spacing:.5px;color:#64748b;padding:8px 10px;border-bottom:2px solid var(--boc-card-line);}
.epc-boc table td{padding:9px 10px;border-bottom:1px solid #eef2f7;vertical-align:middle;}
.epc-boc table tr:hover td{background:#f8fafc;}
.epc-boc__chip{display:inline-block;font-size:10px;font-weight:700;letter-spacing:.5px;color:#fff;border-radius:20px;padding:2px 9px;}
.epc-boc__chip--green{background:var(--boc-green);} .epc-boc__chip--amber{background:var(--boc-amber);} .epc-boc__chip--red{background:var(--boc-red);}
.epc-boc__chip--type{background:#334155;}
.epc-boc__mini{display:inline-flex;align-items:center;justify-content:center;width:26px;height:24px;border:1px solid var(--boc-card-line);border-radius:6px;color:#475569;text-decoration:none;margin-right:2px;}
.epc-boc__mini:hover{background:#f1f5f9;color:#0369a1;}
.epc-boc__sectitle{font-size:13px;font-weight:700;color:#0369a1;margin:6px 0 8px;display:flex;align-items:center;gap:8px;}
.epc-boc__code{font-family:ui-monospace,Menlo,monospace;font-size:11px;color:#475569;}
@media(max-width:880px){.epc-boc__rail{display:none;}}
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
        $nav = epc_boc_nav();
        $h = 'epc_boc_h';
        $initials = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', $operator) ?: 'OP', 0, 2));
        echo "<style>" . epc_boc_console_css() . "</style>\n";
        echo "<script>(function(){try{document.body.classList.add('epc-boc-mode');}catch(e){}})();</script>\n";
        echo '<div class="epc-boc">';
        // Rail
        echo '<aside class="epc-boc__rail">';
        echo '<div class="epc-boc__brand"><div class="epc-boc__brand-badge">' . $h($brand['short']) . '</div><div><div class="epc-boc__brand-name">' . $h($brand['short']) . '</div><div class="epc-boc__brand-sub">Control</div></div></div>';
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
        echo '<div class="epc-boc__main">';
        echo '<div class="epc-boc__topbar">';
        echo '<div class="epc-boc__crumb">' . $h($title) . '<small>' . $h($brand['name']) . '</small></div>';
        echo '<div class="epc-boc__search"><i class="fa fa-search"></i><input type="search" id="epc-boc-search" placeholder="Search tenants, orders, settings… (global)"></div>';
        echo '<div class="epc-boc__topbar-actions">';
        echo '<span class="epc-boc__env"><i class="fa fa-circle" style="font-size:7px;vertical-align:middle;color:#16a34a"></i> ' . $h($env) . '</span>';
        echo '<a class="epc-boc__btn epc-boc__btn--ai" href="#" id="epc-boc-copilot"><i class="fa fa-magic"></i> AI Copilot</a>';
        echo '<a class="epc-boc__btn" href="#" title="Notifications"><i class="fa fa-bell"></i></a>';
        echo '<span class="epc-boc__avatar" title="' . $h($operator) . '">' . $h($initials) . '</span>';
        echo '</div></div>';
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
