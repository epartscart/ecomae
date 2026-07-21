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
        return <<<CSS
:root{--boc-bg:#000000;--boc-rail:#000000;--boc-rail-2:#0a0a0a;--boc-line:#1f1f1f;--boc-ink:#f5f5f5;--boc-mut:#a3a3a3;--boc-accent:#c62828;--boc-accent-2:#e57373;--boc-canvas:#fff7f7;--boc-card:#ffffff;--boc-card-line:#f0d4d4;--boc-green:#0a7d3c;--boc-amber:#b45309;--boc-red:#c62828;--boc-red-soft:#ef5350;--boc-rose:#fff5f5;--boc-rose-mid:#fde8e8;--boc-rose-line:#f5c6c6;--boc-ink-1:#1f2937;--boc-ink-2:#404040;--boc-ink-3:#6b7280;}
/* Neutralise legacy CP chrome on BOC pages (incl. top mega-menu that sat above a transparent rail) */
body.epc-boc-mode #menu,body.epc-boc-mode .navbar-static-side,body.epc-boc-mode nav.navbar-default,body.epc-boc-mode #top-navigation,body.epc-boc-mode .epc-cp-topbar,body.epc-boc-mode .epc-cp-topnav,body.epc-boc-mode .epc-cp-topnav-panel,body.epc-boc-mode #header,body.epc-boc-mode .left_cp_menu,body.epc-boc-mode .left_menu{display:none!important;visibility:hidden!important;}
body.epc-boc-mode #wrapper,body.epc-boc-mode.epc-cp-shell #wrapper,body.epc-boc-mode.epc-cp-topnav-only.fixed-navbar #wrapper{margin:0!important;margin-top:0!important;margin-left:0!important;width:100%!important;max-width:100%!important;height:100vh!important;max-height:100vh!important;background:var(--boc-canvas)!important;}
body.epc-boc-mode .content,body.epc-boc-mode #wrapper .content{padding:0!important;margin:0!important;background:var(--boc-canvas)!important;}
body.epc-boc-mode{background:var(--boc-canvas)!important;overflow:hidden!important;}
.epc-boc{font-family:"Sora","Segoe UI",Helvetica,Arial,sans-serif;color:var(--boc-ink-1);display:flex;min-height:100vh;position:fixed;inset:0;z-index:4000;overflow:auto;background:var(--boc-canvas);-webkit-font-smoothing:antialiased;font-feature-settings:"tnum";isolation:isolate;}
/* :has() fallback: hide legacy CP chrome when a BOC console is present */
body:has(.epc-boc) #header,body:has(.epc-boc) #menu,body:has(.epc-boc) #right-sidebar,body:has(.epc-boc) .footer,body:has(.epc-boc) .epc-cp-topnav,body:has(.epc-boc) .epc-cp-topnav-panel,body:has(.epc-boc) .left_cp_menu,body:has(.epc-boc) .left_menu{display:none!important;visibility:hidden!important;}
body:has(.epc-boc) #wrapper,body:has(.epc-boc).epc-cp-topnav-only.fixed-navbar #wrapper{margin:0!important;margin-top:0!important;padding:0!important;width:100%!important;height:100vh!important;max-height:100vh!important;}
body:has(.epc-boc){overflow:hidden!important;}
/* Beat legacy .epc-cp-shell rules that force .content>.row>div to display:block */
body.epc-cp-shell .content .epc-boc,body.epc-cp .content .epc-boc,.epc-cp-shell .epc-boc{display:flex!important;overflow:auto!important;width:100%!important;float:none!important;position:fixed!important;inset:0!important;z-index:4000!important;background:var(--boc-canvas)!important;}
body.epc-cp-shell .content .epc-boc__main,.epc-cp-shell .epc-boc__main{display:flex!important;flex-direction:column!important;flex:1 1 auto!important;min-width:0!important;width:auto!important;background:var(--boc-canvas)!important;}
body.epc-cp-shell .content .epc-boc__rail,.epc-cp-shell .epc-boc__rail{flex:0 0 260px!important;width:260px!important;background:#000000!important;}
.epc-boc *{box-sizing:border-box;}
.epc-boc a{text-decoration:none;}
/* Optional left rail (layout=rail only) */
.epc-boc__rail{width:260px;flex:0 0 260px;background:#000000!important;color:#f5f5f5;position:sticky;top:0;align-self:flex-start;height:100vh;overflow-y:auto;border-right:1px solid #1f1f1f;scrollbar-width:thin;scrollbar-color:#404040 #000;opacity:1!important;}
.epc-boc__rail::-webkit-scrollbar{width:8px;}.epc-boc__rail::-webkit-scrollbar-thumb{background:#404040;border-radius:8px;}
.epc-boc__brand{display:flex;align-items:center;gap:11px;padding:16px 18px;border-bottom:3px solid #dc2626;position:sticky;top:0;background:#000000!important;z-index:2;opacity:1!important;-webkit-backdrop-filter:none!important;backdrop-filter:none!important;}
.epc-boc__brand-badge{width:36px;height:36px;border-radius:8px;background:#dc2626;display:flex;align-items:center;justify-content:center;font-weight:800;color:#fff;font-size:13px;letter-spacing:.3px;box-shadow:none;}
.epc-boc__brand-name{font-weight:800;font-size:15px;line-height:1.05;letter-spacing:.04em;color:#fff;text-transform:uppercase;}
.epc-boc__brand-sub{font-size:9.5px;color:#a3a3a3;letter-spacing:2px;text-transform:uppercase;margin-top:2px;}
.epc-boc__group{padding:12px 12px 2px;}
.epc-boc__group-h{font-size:10px;letter-spacing:.08em;text-transform:uppercase;color:#a3a3a3;padding:4px 10px;display:flex;align-items:center;gap:8px;font-weight:700;border-bottom:1px solid #262626;margin-bottom:4px;padding-bottom:8px;}
.epc-boc__group-h .fa{color:#f87171;}
.epc-boc__nav{display:flex;flex-direction:column;gap:1px;margin-top:2px;}
.epc-boc__nav a{display:flex;align-items:center;gap:11px;padding:8px 11px;border-radius:6px;color:#e5e5e5;font-size:12.5px;font-weight:500;transition:background .12s,color .12s;}
.epc-boc__nav a .fa{width:16px;text-align:center;color:#f87171;font-size:13px;}
.epc-boc__nav a:hover{background:rgba(220,38,38,.28);color:#fff;}
.epc-boc__nav a:hover .fa{color:#fecaca;}
.epc-boc__nav a.is-active{background:#dc2626;color:#fff;font-weight:700;box-shadow:none;}
.epc-boc__nav a.is-active .fa{color:#fff;}
/* ERP-style TOP mega-menu — light professional red fill + multi-column panels */
.epc-boc--topnav{flex-direction:column!important;}
body.epc-cp-shell .content .epc-boc--topnav,.epc-cp-shell .epc-boc--topnav{flex-direction:column!important;}
.epc-boc--topnav .epc-boc__rail{display:none!important;}
.epc-boc{background:linear-gradient(180deg,#fff7f7 0%,#ffffff 45%,#fef2f2 100%)!important;}
.epc-boc__topnav{position:sticky;top:0;z-index:40;background:linear-gradient(180deg,#ef5350 0%,#e53935 100%);color:#fff;border-bottom:1px solid #c62828;box-shadow:0 6px 18px rgba(198,40,40,.14);}
.epc-boc__topnav-inner{display:flex;align-items:stretch;gap:2px;padding:0 10px;min-height:50px;max-width:100%;}
.epc-boc__topnav-brand{display:inline-flex;align-items:center;gap:8px;padding:0 12px 0 4px;color:#fff!important;font-weight:800;font-size:12px;letter-spacing:.04em;text-transform:uppercase;white-space:nowrap;border-right:1px solid rgba(255,255,255,.28);margin-right:4px;}
.epc-boc__topnav-brand .fa{color:#ffebee;}
.epc-boc__topnav-list{list-style:none;margin:0;padding:0;display:flex;align-items:stretch;flex:1 1 auto;min-width:0;overflow-x:auto;scrollbar-width:thin;scrollbar-color:rgba(255,255,255,.45) transparent;}
.epc-boc__topnav-item{position:static;display:flex;align-items:stretch;flex:0 0 auto;}
.epc-boc__topnav-btn{appearance:none;border:0;background:transparent;color:#fff;display:inline-flex;align-items:center;gap:5px;padding:0 9px;font:inherit;font-size:11.5px;font-weight:700;cursor:pointer;white-space:nowrap;border-bottom:3px solid transparent;}
.epc-boc__topnav-btn .fa{color:#ffebee;font-size:12px;}
.epc-boc__topnav-btn .epc-boc__topnav-caret{font-size:10px;opacity:.85;margin-left:1px;}
.epc-boc__topnav-btn:hover,.epc-boc__topnav-item.is-open .epc-boc__topnav-btn,.epc-boc__topnav-item.is-active .epc-boc__topnav-btn{background:rgba(255,255,255,.18);border-bottom-color:#fff;}
.epc-boc__topnav-actions{display:inline-flex;align-items:center;gap:6px;margin-left:auto;padding-left:8px;border-left:1px solid rgba(255,255,255,.28);flex:0 0 auto;}
.epc-boc__topnav-actions .epc-boc__scope,.epc-boc__topnav-actions .epc-boc__env{color:#fff;background:rgba(255,255,255,.16);border-color:rgba(255,255,255,.28);}
.epc-boc__topnav-actions .epc-boc__btn{background:rgba(255,255,255,.16);border-color:rgba(255,255,255,.28);color:#fff;}
.epc-boc__topnav-actions .epc-boc__btn--ai{background:#b71c1c;border-color:#b71c1c;}
.epc-boc__topnav-actions .epc-boc__lang select,.epc-boc__topnav-actions .epc-boc__lang .form-control{min-height:28px;padding:2px 8px;font-size:12px;border-radius:6px;border:1px solid rgba(255,255,255,.35);background:rgba(255,255,255,.14);color:#fff;}
.epc-boc--topnav .epc-boc__topbar{display:none!important;}
.epc-boc__page-title{display:flex;align-items:baseline;justify-content:space-between;gap:12px;margin:0 0 14px;padding:0;}
.epc-boc__page-title h1{margin:0;font-size:20px;font-weight:800;letter-spacing:-.02em;color:var(--boc-ink-1);}
.epc-boc__page-title small{color:var(--boc-ink-3);font-size:12px;font-weight:600;}
.epc-boc__topnav-panel{position:fixed;z-index:50;min-width:320px;max-width:min(1100px,calc(100vw - 16px));background:#fff;color:#1f2937;border:1px solid var(--boc-rose-line);border-radius:0 0 12px 12px;box-shadow:0 16px 40px rgba(198,40,40,.14);padding:0;overflow:auto;}
.epc-boc__topnav-panel-hd{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:12px 16px;border-bottom:1px solid var(--boc-rose-mid);background:linear-gradient(180deg,#ffffff 0%,#fff7f7 100%);}
.epc-boc__topnav-panel-title{font-weight:800;font-size:13px;color:#1f2937;display:flex;align-items:center;gap:8px;}
.epc-boc__topnav-panel-title .fa{color:var(--boc-red);}
.epc-boc__topnav-panel-hub{font-size:12px;font-weight:700;color:var(--boc-red);}
.epc-boc__topnav-cols{display:flex;flex-wrap:wrap;gap:4px 18px;padding:12px 16px 16px;}
.epc-boc__topnav-col{min-width:170px;flex:1 1 170px;max-width:240px;}
.epc-boc__topnav-col-hd{font-size:10px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;color:var(--boc-red);margin:4px 0 8px;display:flex;align-items:center;gap:6px;}
.epc-boc__topnav-links{list-style:none;margin:0;padding:0;}
.epc-boc__topnav-links a{display:flex;align-items:center;gap:8px;padding:7px 8px;border-radius:8px;color:#374151;font-size:12.5px;font-weight:600;}
.epc-boc__topnav-links a .fa{width:14px;text-align:center;color:var(--boc-red-soft);}
.epc-boc__topnav-links a:hover{background:var(--boc-rose);color:#7f1d1d;}
.epc-boc__topnav-links li.is-active a{background:var(--boc-rose-mid);color:#b71c1c;box-shadow:inset 3px 0 0 var(--boc-red);}
.epc-boc__topnav-links li.is-active a .fa{color:var(--boc-red);}
body.epc-boc-topnav-open{overflow:hidden;}
/* Main */
.epc-boc__main{flex:1;min-width:0;display:flex;flex-direction:column;background:var(--boc-canvas);}
.epc-boc--topnav .epc-boc__main{width:100%!important;flex:1 1 auto!important;}
.epc-boc__topbar{position:sticky;top:0;z-index:20;display:flex;align-items:center;gap:14px;background:#ffffff!important;border-bottom:1px solid var(--boc-card-line);padding:11px 24px;box-shadow:0 2px 10px rgba(0,0,0,.06);opacity:1!important;-webkit-backdrop-filter:none!important;backdrop-filter:none!important;}
.epc-boc--topnav .epc-boc__topbar{top:0;z-index:30;}
.epc-boc__crumb{font-weight:700;font-size:15px;color:var(--boc-ink-1);letter-spacing:.2px;}
.epc-boc__crumb small{display:block;font-weight:500;font-size:10.5px;color:var(--boc-ink-3);letter-spacing:.3px;margin-top:1px;}
.epc-boc__search{flex:1;max-width:480px;position:relative;}
.epc-boc__search input{width:100%;border:1px solid var(--boc-card-line);background:#fafafa;border-radius:8px;padding:8px 12px 8px 34px;font-size:13px;outline:none;transition:box-shadow .12s,border-color .12s,background .12s;}
.epc-boc__search input:focus{border-color:#dc2626;background:#fff;box-shadow:0 0 0 3px rgba(220,38,38,.14);}
.epc-boc__search .fa{position:absolute;left:12px;top:9px;color:#737373;font-size:13px;}
.epc-boc__topbar-actions{margin-left:auto;display:flex;align-items:center;gap:9px;}
.epc-boc__lang{display:inline-flex;align-items:center;}
.epc-boc__lang>.epc-cp-translate-nav,.epc-boc__lang>.epc-cp-translate{list-style:none;margin:0;padding:0;display:inline-flex;align-items:center;}
.epc-boc__lang select.epc-cp-translate__select{height:32px;font-size:12px;min-width:118px;}
.epc-boc__env{font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:#0a7d3c;background:#e7f6ec;border:1px solid #bfe6cd;border-radius:20px;padding:3px 10px;}
.epc-boc__scope{font-size:11px;font-weight:700;color:var(--boc-ink-2);background:#f5f5f5;border:1px solid #e5e5e5;border-radius:20px;padding:3px 11px;display:inline-flex;align-items:center;gap:6px;}
.epc-boc__scope .fa{color:#dc2626;font-size:11px;}
.epc-boc__btn{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--boc-card-line);background:#fff;color:var(--boc-ink-2);border-radius:8px;padding:7px 12px;font-size:12px;font-weight:600;cursor:pointer;}
.epc-boc__btn:hover{background:#fafafa;color:var(--boc-ink-1);border-color:#dc2626;}
.epc-boc__btn--ai{background:#0a0a0a;color:#fff;border:1px solid #0a0a0a;box-shadow:none;}
.epc-boc__btn--ai:hover{background:#dc2626;border-color:#b91c1c;color:#fff;filter:none;}
.epc-boc__avatar{width:32px;height:32px;border-radius:50%;background:#0a0a0a;color:#fff;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;letter-spacing:.3px;}
.epc-boc__canvas{padding:18px 20px 32px;flex:1;max-width:none;width:100%;position:relative;}
.epc-boc__canvas::before{content:"";position:absolute;inset:0;pointer-events:none;z-index:0;opacity:.95;background-color:#fff7f7;background-image:
 linear-gradient(rgba(198,40,40,.04) 1px,transparent 1px),
 linear-gradient(90deg,rgba(198,40,40,.04) 1px,transparent 1px),
 radial-gradient(ellipse 55% 42% at 0% 0%,rgba(239,83,80,.12),transparent 55%),
 linear-gradient(180deg,#fffafa 0%,#fff5f5 70%,#fde8e8 100%);
 background-size:32px 32px,32px 32px,auto,auto;mask-image:linear-gradient(180deg,#000 0%,#000 58%,transparent 100%);}
.epc-boc__canvas>*{position:relative;z-index:1;}
@keyframes bocRise{from{opacity:0;transform:translateY(14px);}to{opacity:1;transform:none;}}
@keyframes bocPulse{0%,100%{box-shadow:0 0 0 0 rgba(10,125,60,.35);}50%{box-shadow:0 0 0 10px rgba(10,125,60,0);}}
@keyframes bocBarFill{from{transform:scaleX(0);}to{transform:scaleX(1);}}
/* Hero */
.epc-boc__hero{background:linear-gradient(118deg,#0a0a0a 0%,#171717 42%,#7f1d1d 100%);color:#fff;border-radius:16px;padding:26px 28px;margin-bottom:18px;display:grid;grid-template-columns:1.4fr auto;align-items:center;gap:20px;position:relative;overflow:hidden;animation:bocRise .5s ease both;}
.epc-boc__hero::before{content:"";position:absolute;inset:0;background:repeating-linear-gradient(-45deg,transparent,transparent 10px,rgba(255,255,255,.02) 10px,rgba(255,255,255,.02) 11px);pointer-events:none;}
.epc-boc__hero::after{content:"";position:absolute;right:-40px;top:-80px;width:280px;height:280px;border-radius:50%;background:radial-gradient(circle,rgba(220,38,38,.35),transparent 68%);pointer-events:none;}
.epc-boc__hero>div{position:relative;z-index:1;}
.epc-boc__hero-brand{display:inline-flex;align-items:center;gap:8px;font-size:11px;font-weight:800;letter-spacing:.16em;text-transform:uppercase;color:#fecaca;margin:0 0 10px;}
.epc-boc__hero-brand span{display:inline-flex;align-items:center;justify-content:center;width:28px;height:28px;border-radius:8px;background:#dc2626;color:#fff;font-size:11px;letter-spacing:0;}
.epc-boc__hero h2{margin:0 0 8px;font-family:"Fraunces","Times New Roman",serif;font-size:28px;font-weight:700;letter-spacing:-.02em;line-height:1.15;color:#fff!important;}
.epc-boc__hero p{margin:0;opacity:.88;font-size:13.5px;max-width:62ch;line-height:1.55;color:#f5f5f5;}
.epc-boc__hero-actions{display:flex;flex-wrap:wrap;gap:8px;justify-content:flex-end;position:relative;z-index:1;}
.epc-boc__hero .epc-boc__btn{backdrop-filter:none;}
.epc-boc__hero .epc-boc__btn--solid{background:#fff;color:#0a0a0a;border-color:#fff;font-weight:700;}
.epc-boc__hero .epc-boc__btn--solid:hover{background:#fecaca;border-color:#fecaca;color:#7f1d1d;}
.epc-boc__hero .epc-boc__btn--ghost{background:rgba(255,255,255,.1);color:#fff;border:1px solid rgba(255,255,255,.35);}
.epc-boc__hero .epc-boc__btn--ghost:hover{background:rgba(255,255,255,.2);color:#fff;border-color:#fff;}
/* Analytical KPI cards */
.epc-boc__tiles{display:grid!important;grid-template-columns:repeat(auto-fit,minmax(150px,1fr))!important;gap:12px!important;margin-bottom:18px;animation:bocRise .55s ease .05s both;}
.epc-boc__tile{background:var(--boc-card)!important;border:1px solid var(--boc-card-line)!important;border-radius:14px!important;padding:16px 16px 14px!important;position:relative;overflow:hidden;box-shadow:0 1px 2px rgba(13,22,38,.04);transition:box-shadow .18s,transform .18s;display:flex!important;flex-direction:column!important;gap:2px;}
.epc-boc__tile:hover{box-shadow:0 8px 22px rgba(13,22,38,.1);transform:translateY(-2px);}
.epc-boc__tile-ico{width:34px;height:34px;border-radius:10px;display:flex;align-items:center;justify-content:center;background:#f5f5f5;color:#404040;font-size:15px;margin-bottom:8px;}
.epc-boc__tile-label{font-size:10.5px;color:var(--boc-ink-3);text-transform:uppercase;letter-spacing:.7px;font-weight:700;}
.epc-boc__tile-val{font-size:30px;font-weight:800;color:var(--boc-ink-1);line-height:1.05;margin-top:4px;font-variant-numeric:tabular-nums;letter-spacing:-.6px;}
.epc-boc__tile-hint{font-size:11px;color:#94a3b8;margin-top:2px;}
.epc-boc__tile-bar{height:4px;border-radius:99px;background:#f1f5f9;margin-top:10px;overflow:hidden;}
.epc-boc__tile-bar>i{display:block;height:100%;width:var(--boc-bar,40%);border-radius:99px;background:#0a0a0a;transform-origin:left center;animation:bocBarFill .8s ease .2s both;}
.epc-boc__tile--green{box-shadow:inset 4px 0 0 var(--boc-green)!important;} .epc-boc__tile--green .epc-boc__tile-val{color:var(--boc-green);} .epc-boc__tile--green .epc-boc__tile-ico{background:#e7f6ec;color:var(--boc-green);} .epc-boc__tile--green .epc-boc__tile-bar>i{background:var(--boc-green);}
.epc-boc__tile--amber{box-shadow:inset 4px 0 0 var(--boc-amber)!important;} .epc-boc__tile--amber .epc-boc__tile-val{color:var(--boc-amber);} .epc-boc__tile--amber .epc-boc__tile-ico{background:#fff7ed;color:var(--boc-amber);} .epc-boc__tile--amber .epc-boc__tile-bar>i{background:var(--boc-amber);}
.epc-boc__tile--red{box-shadow:inset 4px 0 0 var(--boc-red)!important;} .epc-boc__tile--red .epc-boc__tile-val{color:var(--boc-red);} .epc-boc__tile--red .epc-boc__tile-ico{background:#fef2f2;color:var(--boc-red);} .epc-boc__tile--red .epc-boc__tile-bar>i{background:var(--boc-red);}
.epc-boc__tile--ink .epc-boc__tile-ico{background:#0a0a0a;color:#fff;}
/* Command-center layout blocks */
.epc-boc__cc-row{display:grid!important;grid-template-columns:1.15fr .85fr;gap:14px;margin-bottom:18px;animation:bocRise .6s ease .08s both;}
.epc-boc__pulse{background:#fff;border:1px solid var(--boc-card-line);border-radius:14px;padding:18px 20px;display:flex;gap:22px;align-items:center;box-shadow:0 1px 2px rgba(13,22,38,.04);}
.epc-boc__ring{--pct:100;width:118px;height:118px;flex:0 0 118px;border-radius:50%;background:conic-gradient(var(--boc-green) calc(var(--pct) * 1%),#e5e5e5 0);display:flex;align-items:center;justify-content:center;position:relative;animation:bocPulse 2.4s ease-in-out infinite;}
.epc-boc__ring::after{content:"";position:absolute;inset:12px;border-radius:50%;background:#fff;}
.epc-boc__ring-inner{position:relative;z-index:1;text-align:center;}
.epc-boc__ring-inner strong{display:block;font-size:26px;font-weight:800;letter-spacing:-.5px;line-height:1;color:var(--boc-ink-1);}
.epc-boc__ring-inner span{font-size:10px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--boc-ink-3);}
.epc-boc__pulse-meta{flex:1;min-width:0;}
.epc-boc__pulse-meta h3{margin:0 0 6px;font-size:15px;font-weight:750;color:var(--boc-ink-1);}
.epc-boc__pulse-meta p{margin:0 0 12px;font-size:12.5px;color:var(--boc-ink-3);line-height:1.45;}
.epc-boc__stack{display:flex;height:12px;border-radius:99px;overflow:hidden;background:#f1f5f9;margin-bottom:10px;}
.epc-boc__stack>i{display:block;height:100%;}
.epc-boc__stack>i.g{background:var(--boc-green);} .epc-boc__stack>i.a{background:var(--boc-amber);} .epc-boc__stack>i.r{background:var(--boc-red);}
.epc-boc__legend{display:flex;flex-wrap:wrap;gap:10px 14px;font-size:11.5px;color:var(--boc-ink-2);font-weight:600;}
.epc-boc__legend b{font-variant-numeric:tabular-nums;}
.epc-boc__dot{display:inline-block;width:8px;height:8px;border-radius:50%;margin-right:5px;vertical-align:middle;}
.epc-boc__dot.g{background:var(--boc-green);} .epc-boc__dot.a{background:var(--boc-amber);} .epc-boc__dot.r{background:var(--boc-red);}
.epc-boc__qa-port{background:#fff;border:1px solid var(--boc-card-line);border-radius:14px;padding:16px 18px;margin:0 0 18px;box-shadow:0 1px 2px rgba(13,22,38,.04);animation:bocRise .55s ease .05s both;}
.epc-boc__qa-port .eds-wrap{margin:0;}
.epc-boc__qa-port .eds-head h4{font-size:14px;}
.epc-boc__ops{background:#fff;border:1px solid var(--boc-card-line);border-radius:14px;padding:16px 18px;box-shadow:0 1px 2px rgba(13,22,38,.04);}
.epc-boc__ops h3{margin:0 0 12px;font-size:13px;font-weight:750;color:var(--boc-ink-1);display:flex;align-items:center;gap:8px;}
.epc-boc__ops h3 .fa{color:var(--boc-accent);}
.epc-boc__ops-grid{display:grid;grid-template-columns:1fr 1fr;gap:8px;}
.epc-boc__ops a{display:flex;align-items:center;gap:10px;padding:11px 12px;border:1px solid var(--boc-card-line);border-radius:10px;background:#fafafa;color:var(--boc-ink-1);font-size:12px;font-weight:600;transition:background .15s,border-color .15s,transform .15s;}
.epc-boc__ops a:hover{background:#fff;border-color:#dc2626;transform:translateY(-1px);color:#0a0a0a;}
.epc-boc__ops a .fa{width:28px;height:28px;border-radius:8px;background:#0a0a0a;color:#fff;display:flex;align-items:center;justify-content:center;font-size:12px;flex:0 0 28px;}
.epc-boc__ops a small{display:block;font-weight:500;font-size:10.5px;color:var(--boc-ink-3);margin-top:1px;}
/* Fleet tenant cards */
.epc-boc__fleet-sec{margin-bottom:18px;animation:bocRise .65s ease .1s both;}
.epc-boc__fleet-h{display:flex;align-items:center;justify-content:space-between;gap:12px;margin:0 0 12px;}
.epc-boc__fleet-h h3{margin:0;font-size:15px;font-weight:750;color:var(--boc-ink-1);display:flex;align-items:center;gap:8px;}
.epc-boc__fleet-h h3 .fa{color:var(--boc-accent);}
.epc-boc__fleet-h .epc-boc__count{font-size:11px;font-weight:700;letter-spacing:.04em;color:var(--boc-ink-3);background:#fff;border:1px solid var(--boc-card-line);border-radius:8px;padding:3px 9px;}
.epc-boc__fleet-grid{display:grid!important;grid-template-columns:repeat(auto-fill,minmax(260px,1fr))!important;gap:12px!important;}
.epc-boc__tcard{background:#fff!important;border:1px solid var(--boc-card-line)!important;border-radius:14px!important;padding:14px 15px!important;display:flex!important;flex-direction:column!important;gap:10px;box-shadow:0 1px 2px rgba(13,22,38,.04);transition:box-shadow .18s,transform .18s,border-color .18s;position:relative;overflow:hidden;}
.epc-boc__tcard::before{content:"";position:absolute;left:0;top:0;bottom:0;width:4px;background:var(--boc-health,#0a7d3c);}
.epc-boc__tcard:hover{box-shadow:0 10px 24px rgba(13,22,38,.1);transform:translateY(-2px);border-color:#d4d4d4;}
.epc-boc__tcard-top{display:flex;align-items:flex-start;justify-content:space-between;gap:10px;}
.epc-boc__tcard-name{font-size:14px;font-weight:750;color:var(--boc-ink-1);line-height:1.25;margin:0;}
.epc-boc__tcard-key{font-family:ui-monospace,"SFMono-Regular",Menlo,monospace;font-size:10.5px;color:#737373;margin-top:3px;}
.epc-boc__tcard-meta{display:flex;flex-wrap:wrap;gap:6px;align-items:center;}
.epc-boc__pill{display:inline-flex;align-items:center;gap:5px;font-size:10.5px;font-weight:700;letter-spacing:.02em;padding:3px 8px;border-radius:7px;background:#f5f5f5;color:var(--boc-ink-2);border:1px solid #e5e5e5;}
.epc-boc__pill--live{background:#e7f6ec;color:var(--boc-green);border-color:#bfe6cd;}
.epc-boc__tcard-actions{display:flex;gap:6px;margin-top:auto;padding-top:4px;}
.epc-boc__tcard-actions a{flex:1;display:inline-flex;align-items:center;justify-content:center;gap:6px;padding:8px 8px;border-radius:8px;border:1px solid var(--boc-card-line);background:#fafafa;color:var(--boc-ink-2);font-size:11.5px;font-weight:650;transition:background .12s,color .12s,border-color .12s;}
.epc-boc__tcard-actions a:hover{background:#0a0a0a;color:#fff;border-color:#0a0a0a;}
.epc-boc__tcard-actions a.primary{background:#dc2626;border-color:#dc2626;color:#fff;}
.epc-boc__tcard-actions a.primary:hover{background:#b91c1c;border-color:#b91c1c;}
.epc-boc__empty{background:#fff;border:1px dashed #d4d4d4;border-radius:14px;padding:28px;text-align:center;color:#737373;}
/* Panels */
.epc-boc__panel{background:var(--boc-card);border:1px solid var(--boc-card-line);border-radius:14px;padding:0;margin-bottom:18px;overflow:hidden;box-shadow:0 1px 2px rgba(13,22,38,.04);animation:bocRise .7s ease .12s both;}
.epc-boc__panel-h{display:flex;align-items:center;gap:9px;font-size:13.5px;font-weight:700;color:var(--boc-ink-1);margin:0;padding:14px 18px;border-bottom:1px solid var(--boc-card-line);background:#fafafa;}
.epc-boc__panel-h .fa{color:var(--boc-accent-2);}
.epc-boc__panel>:not(.epc-boc__panel-h){padding-left:18px;padding-right:18px;}
.epc-boc__panel>table,.epc-boc__panel>.epc-boc__timeline{padding:0;}
.epc-boc__panel>p{padding-top:14px;padding-bottom:14px;}
.epc-boc__panel>p+p,.epc-boc__panel .epc-boc__btn{margin:0 18px 16px;}
/* Enterprise data grid */
.epc-boc table{width:100%;border-collapse:collapse;font-size:12.5px;}
.epc-boc table th{text-align:left;font-size:10.5px;text-transform:uppercase;letter-spacing:.6px;color:var(--boc-ink-3);font-weight:700;padding:10px 14px;background:#f6f8fb;border-bottom:1px solid var(--boc-card-line);position:sticky;top:0;z-index:1;}
.epc-boc table td{padding:11px 14px;border-bottom:1px solid #eef2f7;vertical-align:middle;color:var(--boc-ink-2);}
.epc-boc table tbody tr:nth-child(even) td{background:#fcfdfe;}
.epc-boc table tbody tr:hover td{background:#f5f5f5;}
.epc-boc table td strong{color:var(--boc-ink-1);font-weight:650;}
.epc-boc__grid--metric th:nth-child(n+3),.epc-boc__grid--metric td:nth-child(n+3){text-align:right;font-variant-numeric:tabular-nums;}
.epc-boc__chip{display:inline-flex;align-items:center;gap:5px;font-size:10px;font-weight:700;letter-spacing:.4px;color:#fff;border-radius:7px;padding:3px 9px;text-transform:uppercase;}
.epc-boc__chip::before{content:"";width:6px;height:6px;border-radius:50%;background:rgba(255,255,255,.9);}
.epc-boc__chip--green{background:var(--boc-green);} .epc-boc__chip--amber{background:var(--boc-amber);} .epc-boc__chip--red{background:var(--boc-red);}
.epc-boc__chip--type{background:#404040;}
.epc-boc__mini{display:inline-flex;align-items:center;justify-content:center;width:27px;height:25px;border:1px solid var(--boc-card-line);border-radius:7px;color:#475569;margin-right:3px;}
.epc-boc__mini:hover{background:#0a0a0a;color:#fff;border-color:#0a0a0a;}
.epc-boc__sectitle{font-size:13px;font-weight:700;color:var(--boc-ink-1);margin:6px 0 8px;display:flex;align-items:center;gap:8px;}
.epc-boc__code{font-family:ui-monospace,"SFMono-Regular",Menlo,monospace;font-size:11px;color:#64748b;}
.epc-boc__timeline{list-style:none;margin:0;padding:4px 18px 16px!important;}
.epc-boc__timeline li{display:grid;grid-template-columns:16px 1fr;gap:12px;padding:12px 0;border-bottom:1px solid #f0f0f0;position:relative;}
.epc-boc__timeline li:last-child{border-bottom:0;}
.epc-boc__timeline-dot{width:10px;height:10px;border-radius:50%;background:#dc2626;margin-top:4px;box-shadow:0 0 0 3px rgba(220,38,38,.15);}
.epc-boc__timeline-body strong{display:block;font-size:12.5px;color:var(--boc-ink-1);}
.epc-boc__timeline-body span{font-size:11.5px;color:var(--boc-ink-3);}
.epc-boc__timeline-body em{font-style:normal;font-size:11px;color:#a3a3a3;float:right;}
@media(max-width:1100px){.epc-boc__cc-row{grid-template-columns:1fr;}.epc-boc__ops-grid{grid-template-columns:1fr 1fr;}}
@media(max-width:980px){.epc-boc__rail{display:none;}.epc-boc__canvas{padding:16px;}.epc-boc__hero{grid-template-columns:1fr;}.epc-boc__hero-actions{justify-content:flex-start;}.epc-boc__topnav-label{max-width:9ch;overflow:hidden;text-overflow:ellipsis;}}
@media(max-width:640px){.epc-boc__ops-grid{grid-template-columns:1fr;}.epc-boc__pulse{flex-direction:column;align-items:flex-start;}.epc-boc__topnav-brand span{display:none;}}
CSS;
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
        echo '<script>(function(){function bind(){var root=document.getElementById("epc_boc_topnav");if(!root||root._bocTopBound){return;}root._bocTopBound=true;var items=root.querySelectorAll(".epc-boc__topnav-item");function closeAll(ex){items.forEach(function(it){if(ex&&it===ex){return;}it.classList.remove("is-open");var b=it.querySelector("[data-boc-topnav-toggle]");var p=it.querySelector("[data-boc-topnav-panel]");if(b){b.setAttribute("aria-expanded","false");}if(p){p.hidden=true;}});document.body.classList.remove("epc-boc-topnav-open");}function place(it,p){var rr=root.getBoundingClientRect();var btn=it.querySelector(".epc-boc__topnav-btn");var br=btn?btn.getBoundingClientRect():rr;var top=Math.round(rr.bottom);p.style.top=top+"px";p.style.maxHeight=Math.max(180,Math.floor(window.innerHeight-top-12))+"px";p.hidden=false;var w=p.offsetWidth||640;var left=Math.round(br.left);var maxLeft=Math.max(8,window.innerWidth-w-8);if(left>maxLeft){left=maxLeft;}if(left<8){left=8;}p.style.left=left+"px";p.style.right="auto";}function open(it){closeAll(it);it.classList.add("is-open");var b=it.querySelector("[data-boc-topnav-toggle]");var p=it.querySelector("[data-boc-topnav-panel]");if(b){b.setAttribute("aria-expanded","true");}if(p){place(it,p);}document.body.classList.add("epc-boc-topnav-open");}items.forEach(function(it){var b=it.querySelector("[data-boc-topnav-toggle]");if(!b){return;}b.addEventListener("click",function(e){e.preventDefault();e.stopPropagation();if(it.classList.contains("is-open")){closeAll();}else{open(it);}});});document.addEventListener("click",function(e){if(!root.contains(e.target)){closeAll();}});document.addEventListener("keydown",function(e){if(e.key==="Escape"){closeAll();}});window.addEventListener("resize",function(){items.forEach(function(it){if(!it.classList.contains("is-open")){return;}var p=it.querySelector("[data-boc-topnav-panel]");if(p){place(it,p);}});});}if(document.readyState!=="loading"){bind();}else{document.addEventListener("DOMContentLoaded",bind);}})();</script>';
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
        echo '<link rel="preconnect" href="https://fonts.googleapis.com">'
            . '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>'
            . '<link href="https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,600;700&family=Sora:wght@400;500;600;700;800&display=swap" rel="stylesheet">' . "\n";
        echo "<style>" . epc_boc_console_css() . "</style>\n";
        echo "<script>(function(){try{document.body.classList.add('epc-boc-mode');document.body.classList.add('epc-cp-bos-host');}catch(e){}})();</script>\n";
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
        // Relocate legacy CP language switcher into the BOS chrome slot.
        echo '<script>(function(){function go(){var s=document.getElementById("epc-boc-lang-slot");if(!s){return;}if(s.querySelector("select")){return;}var w=document.querySelector(".epc-cp-translate-nav,.epc-cp-translate");if(w){s.appendChild(w);}}if(document.readyState!=="loading"){go();}else{document.addEventListener("DOMContentLoaded",go);}setTimeout(go,900);})();</script>';
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
