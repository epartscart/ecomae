<?php
/**
 * BOS-style dark animated CSS for the standalone ERP portal.
 * Matrix particles, glowing orbs, glass-morphism login panel.
 */
defined('_ASTEXE_') or die('No access');
?>
<style id="epc-erp-portal-inline">
:root{
  --ep-bg:#020617;
  --ep-card:rgba(15,23,42,.85);
  --ep-brd:rgba(99,102,241,.25);
  --ep-accent:#0ea5e9;
  --ep-accent-2:#38bdf8;
  --ep-text:#f1f5f9;
  --ep-muted:rgba(203,213,225,.85);
  --ep-glass:rgba(15,23,42,.6);
}
body.epc-erp-standalone{
  background:linear-gradient(155deg, #020617 0%, #0f172a 38%, #1e3a8a 72%, #0c4a6e 100%);
  color:var(--ep-text);
  font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
  margin:0;min-height:100vh;
  -webkit-font-smoothing:antialiased;
  overflow-x:hidden;
}
body.epc-erp-standalone a{color:var(--ep-accent-2);}
body.epc-erp-standalone a:hover,body.epc-erp-standalone a:focus{color:#7dd3fc;text-decoration:none;}

/* Animated background */
.epc-erp-portal-bg{position:fixed;inset:0;z-index:0;overflow:hidden;pointer-events:none;}
.epc-erp-portal-bg__grid{position:absolute;inset:0;background-image:linear-gradient(rgba(14,165,233,.07) 1px,transparent 1px),linear-gradient(90deg,rgba(14,165,233,.07) 1px,transparent 1px);background-size:60px 60px;animation:erpGridDrift 20s linear infinite;}
@keyframes erpGridDrift{0%{transform:translate(0,0);}100%{transform:translate(60px,60px);}}
.epc-erp-portal-bg__particles{position:absolute;inset:0;}
.epc-erp-portal-bg__particle{position:absolute;border-radius:50%;will-change:transform;}
.epc-erp-portal-bg__glow{position:absolute;border-radius:50%;filter:blur(80px);opacity:.35;}
.epc-erp-portal-bg__glow--1{width:420px;height:420px;background:radial-gradient(circle,rgba(14,165,233,.4),transparent 70%);top:-120px;left:8%;animation:erpGlow1 8s ease-in-out infinite alternate;}
.epc-erp-portal-bg__glow--2{width:380px;height:380px;background:radial-gradient(circle,rgba(99,102,241,.35),transparent 70%);bottom:-100px;right:12%;animation:erpGlow2 10s ease-in-out infinite alternate;}
.epc-erp-portal-bg__glow--3{width:300px;height:300px;background:radial-gradient(circle,rgba(168,85,247,.3),transparent 70%);top:40%;left:50%;animation:erpGlow3 12s ease-in-out infinite alternate;}
@keyframes erpGlow1{0%{transform:translate(0,0) scale(1);}100%{transform:translate(40px,30px) scale(1.15);}}
@keyframes erpGlow2{0%{transform:translate(0,0) scale(1);}100%{transform:translate(-30px,-40px) scale(1.2);}}
@keyframes erpGlow3{0%{transform:translate(0,0) scale(.9);}100%{transform:translate(20px,-20px) scale(1.1);}}
@keyframes erpFloat{0%{transform:translateY(-10vh);opacity:0;}5%{opacity:1;}50%{opacity:.9;}100%{transform:translateY(105vh);opacity:0;}}
@keyframes erpFloatDrift{0%{transform:translateY(-10vh) translateX(0);opacity:0;}8%{opacity:1;}25%{transform:translateY(20vh) translateX(15px);}50%{transform:translateY(50vh) translateX(-10px);opacity:.8;}75%{transform:translateY(75vh) translateX(20px);}100%{transform:translateY(110vh) translateX(-5px);opacity:0;}}
@keyframes erpFloatStreak{0%{transform:translateY(-5vh) scaleY(1);opacity:0;}5%{opacity:1;}50%{transform:translateY(50vh) scaleY(3);opacity:.7;}100%{transform:translateY(110vh) scaleY(1);opacity:0;}}
@media(prefers-reduced-motion:reduce){.epc-erp-portal-bg__particle,.epc-erp-portal-bg__grid,.epc-erp-portal-bg__glow{animation:none!important;}}

/* Top bar — dark glass */
.epc-erp-topbar{background:rgba(2,6,23,.7);backdrop-filter:blur(12px);-webkit-backdrop-filter:blur(12px);border-bottom:1px solid rgba(99,102,241,.15);box-shadow:0 2px 12px rgba(0,0,0,.3);position:relative;z-index:10;}
.epc-erp-topbar__inner{max-width:1180px;margin:0 auto;padding:7px 22px;display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;}
.epc-erp-topbar__brand{display:flex;align-items:center;gap:11px;color:#fff!important;text-decoration:none;}
.epc-erp-topbar__brand .ech-static__logo,.epc-erp-topbar__brand img{width:32px;height:32px;border-radius:8px;display:block;object-fit:contain;}
.epc-erp-topbar__tenant-logo{display:inline-flex;align-items:center;justify-content:center;width:36px;height:36px;border-radius:9px;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.25);flex-shrink:0;overflow:hidden;}
.epc-erp-topbar__tenant-logo img{width:100%;height:100%;object-fit:contain;border-radius:0;display:block;background:#fff;}
.epc-erp-topbar__tenant-logo--epartscart{background:#fff;padding:3px;}
.epc-erp-topbar__initials{display:inline-flex;align-items:center;justify-content:center;width:36px;height:36px;border-radius:9px;background:linear-gradient(135deg,#2563eb,#0ea5e9);color:#fff;font-size:13px;font-weight:800;letter-spacing:.02em;flex-shrink:0;}
.epc-erp-topbar__brand-text strong{display:block;font-size:16px;font-weight:700;line-height:1.1;letter-spacing:.2px;color:#fff;}
.epc-erp-topbar__brand-text small{display:block;font-size:11.5px;color:var(--ep-muted);}
.epc-erp-login-tenant-brand{display:flex;flex-direction:column;align-items:flex-start;gap:10px;margin:0 0 14px;}
.epc-erp-login-tenant-brand .epc-erp-topbar__tenant-logo{width:56px;height:56px;border-radius:12px;}
.epc-erp-login-tenant-brand__title{font-size:22px;font-weight:800;color:#fff;letter-spacing:-.02em;line-height:1.15;}
.epc-erp-login-tenant-brand__tag{font-size:12.5px;color:rgba(226,232,240,.85);font-weight:600;}
.epc-erp-topbar__nav{display:flex;align-items:center;gap:4px;flex-wrap:wrap;}
.epc-erp-topbar__nav a{color:var(--ep-muted)!important;font-size:13.5px;font-weight:600;padding:7px 12px;border-radius:8px;text-decoration:none;transition:.15s;}
.epc-erp-topbar__nav a:hover{background:rgba(14,165,233,.12);color:var(--ep-accent-2)!important;}

/* Main container */
.epc-erp-main{max-width:1180px;margin:0 auto;padding:26px 22px 40px;position:relative;z-index:1;}
.epc-erp-foot{max-width:1180px;margin:0 auto;padding:18px 22px 34px;color:var(--ep-muted);font-size:12.5px;border-top:1px solid rgba(99,102,241,.15);position:relative;z-index:1;}
body.epc-erp-standalone:has(.epc-erp-shell--layout) .epc-erp-main{max-width:none;padding:0;}
body.epc-erp-standalone:has(.epc-erp-shell--layout) .epc-erp-topbar__inner{max-width:none;}
body.epc-erp-standalone:has(.epc-erp-shell--layout) .epc-erp-foot{max-width:none;}

/* ═══ BOS-style Hero (left branding) ═══ */
.epc-erp-bos-hero{margin-bottom:24px;}
.epc-erp-bos-hero__content{text-align:center;padding:32px 20px 24px;}
.epc-erp-bos-hero__brand-mark{margin-bottom:28px;}
.epc-erp-bos-hero__brand-icon{position:relative;display:inline-block;margin-bottom:14px;}
.epc-erp-bos-hero__brand-icon-inner{width:72px;height:72px;border-radius:20px;background:linear-gradient(135deg,#0ea5e9 0%,#6366f1 100%);display:flex;align-items:center;justify-content:center;font-size:28px;color:#fff;box-shadow:0 8px 32px rgba(14,165,233,.4),0 0 60px rgba(99,102,241,.2);position:relative;z-index:1;}
.epc-erp-bos-hero__brand-ring{position:absolute;inset:-8px;border-radius:24px;border:2px solid rgba(14,165,233,.3);animation:erpRingPulse 3s ease-in-out infinite;}
.epc-erp-bos-hero__brand-ring--2{inset:-16px;border-radius:28px;border-color:rgba(99,102,241,.15);animation-delay:1.5s;}
@keyframes erpRingPulse{0%,100%{opacity:.4;transform:scale(1);}50%{opacity:.8;transform:scale(1.05);}}
.epc-erp-bos-hero__title{font-size:clamp(26px,5vw,36px);font-weight:800;color:#fff;margin:0 0 6px;letter-spacing:-.03em;}
.epc-erp-bos-hero__tagline{font-size:15px;color:rgba(203,213,225,.9);margin:0;font-weight:400;}

/* Capability items */
.epc-erp-bos-hero__capabilities{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:14px;max-width:700px;margin:0 auto 28px;}
.epc-erp-bos-hero__cap-item{display:flex;align-items:center;gap:12px;padding:14px 16px;background:rgba(15,23,42,.5);border:1px solid rgba(99,102,241,.2);border-radius:14px;backdrop-filter:blur(8px);-webkit-backdrop-filter:blur(8px);animation:erpCapFade .6s ease both;}
.epc-erp-bos-hero__cap-item--1{animation-delay:.1s;}
.epc-erp-bos-hero__cap-item--2{animation-delay:.2s;}
.epc-erp-bos-hero__cap-item--3{animation-delay:.3s;}
.epc-erp-bos-hero__cap-item--4{animation-delay:.4s;}
@keyframes erpCapFade{from{opacity:0;transform:translateY(12px);}to{opacity:1;transform:translateY(0);}}
.epc-erp-bos-hero__cap-icon{width:40px;height:40px;border-radius:12px;display:flex;align-items:center;justify-content:center;font-size:16px;flex-shrink:0;}
.epc-erp-bos-hero__cap-item--1 .epc-erp-bos-hero__cap-icon{background:rgba(59,130,246,.2);color:#60a5fa;}
.epc-erp-bos-hero__cap-item--2 .epc-erp-bos-hero__cap-icon{background:rgba(16,185,129,.2);color:#34d399;}
.epc-erp-bos-hero__cap-item--3 .epc-erp-bos-hero__cap-icon{background:rgba(245,158,11,.2);color:#fbbf24;}
.epc-erp-bos-hero__cap-item--4 .epc-erp-bos-hero__cap-icon{background:rgba(168,85,247,.2);color:#c084fc;}
.epc-erp-bos-hero__cap-text{text-align:left;}
.epc-erp-bos-hero__cap-text strong{display:block;font-size:13.5px;font-weight:700;color:#fff;margin-bottom:2px;}
.epc-erp-bos-hero__cap-text span{font-size:12px;color:rgba(203,213,225,.75);}

/* Stats bar */
.epc-erp-bos-hero__stats-bar{display:flex;align-items:center;justify-content:center;gap:20px;flex-wrap:wrap;}
.epc-erp-bos-hero__stat{text-align:center;}
.epc-erp-bos-hero__stat-num{display:block;font-size:24px;font-weight:800;color:#fff;line-height:1;}
.epc-erp-bos-hero__stat-label{display:block;font-size:11.5px;color:rgba(203,213,225,.7);margin-top:3px;text-transform:uppercase;letter-spacing:.5px;}
.epc-erp-bos-hero__stat-divider{width:1px;height:28px;background:rgba(99,102,241,.3);}

/* ═══ Login panel — glass morphism ═══ */
.epc-erp-login-panel--standalone{background:transparent!important;border:none!important;padding:0!important;margin-top:0!important;overflow:visible!important;box-shadow:none!important;}
.epc-erp-login-panel--split{background:var(--ep-glass)!important;backdrop-filter:blur(16px)!important;-webkit-backdrop-filter:blur(16px)!important;border:1px solid rgba(99,102,241,.25)!important;border-radius:20px!important;padding:0!important;margin-top:0!important;overflow:hidden!important;box-shadow:0 24px 48px rgba(2,6,23,.5),0 0 0 1px rgba(255,255,255,.04)!important;}
.epc-erp-login-panel--split .row{margin:0;display:flex;flex-wrap:wrap;}
.epc-erp-login-panel--split .col-md-5,.epc-erp-login-panel--split .col-md-7{padding:0;float:none;}
.epc-erp-login-panel--split .col-md-5{width:42%;}
.epc-erp-login-panel--split .col-md-7{width:58%;}
.epc-erp-login-panel__brand{background:linear-gradient(150deg,rgba(14,165,233,.15) 0%,rgba(99,102,241,.15) 100%)!important;color:#fff;padding:36px 34px;border-right:1px solid rgba(99,102,241,.15);}
.epc-erp-login-panel__brand .ech-static__logo,.epc-erp-login-panel__brand img{width:64px;height:64px;border-radius:15px;background:rgba(255,255,255,.08);padding:4px;margin-bottom:14px;display:block;box-shadow:0 8px 24px rgba(14,165,233,.3);}
.epc-erp-login-panel__brand .ech-static__title,.epc-erp-login-panel__brand .ech-static__tagline{color:#fff;display:block;}
.epc-erp-login-panel__brand .ech-static__title{font-size:18px;font-weight:700;}
.epc-erp-login-panel__brand .ech-static__tagline{font-size:12.5px;opacity:.85;margin-bottom:6px;}
.epc-erp-login-panel__brand h1{font-size:24px;font-weight:800;margin:14px 0 10px;color:#fff;}
.epc-erp-login-lead{font-size:14px;line-height:1.6;color:rgba(203,213,225,.9);margin-bottom:18px;}
.epc-erp-login-features{list-style:none;padding:0;margin:0;}
.epc-erp-login-features li{font-size:13.5px;margin-bottom:9px;color:rgba(226,232,240,.9);}
.epc-erp-login-features li .fa{margin-right:8px;color:#38bdf8;}

/* Login form column */
.epc-erp-login-panel--split .col-md-7{padding:34px 34px 28px;}
.epc-erp-login-panel--split .hpanel{border:none;box-shadow:none;background:transparent;}
.epc-erp-login-panel--split .panel-heading{font-size:19px;font-weight:700;color:#fff;background:transparent;border:none;padding:0 0 14px;}
.epc-erp-login-panel--split .panel-body{padding:0;background:transparent;}
.epc-erp-login-panel--split .login_form{background:transparent;}
.epc-erp-login-panel--split .login_form .panel-heading{display:none;}
.epc-erp-login-panel--split .login_form .panel-body{padding:0;}

/* Form fields — dark glass style */
.epc-erp-login-panel--split .login_form input[type="email"],
.epc-erp-login-panel--split .login_form input[type="password"],
.epc-erp-login-panel--split .login_form input[type="text"],
.epc-erp-login-panel--split .login_form .form-control{
  width:100%;height:auto;padding:12px 14px;font-size:14px;color:#f1f5f9;
  background:rgba(15,23,42,.6);border:1px solid rgba(99,102,241,.3);border-radius:10px;box-shadow:none;
  transition:border-color .2s,box-shadow .2s;
}
.epc-erp-login-panel--split .login_form input:focus,
.epc-erp-login-panel--split .login_form .form-control:focus{
  border-color:var(--ep-accent);outline:none;box-shadow:0 0 0 3px rgba(14,165,233,.2);background:rgba(15,23,42,.8);
}
.epc-erp-login-panel--split .login_form input::placeholder{color:rgba(148,163,184,.6);}
.epc-erp-login-panel--split .login_form .input-group{display:block;margin-bottom:12px;width:100%;border:none;background:none;}
.epc-erp-login-panel--split .login_form .input-group .input-group-addon{display:none;}
.epc-erp-login-panel--split .login_form .nav-tabs{border-bottom:1px solid rgba(99,102,241,.2);margin-bottom:16px;}
.epc-erp-login-panel--split .login_form .nav-tabs>li>a{color:var(--ep-muted);font-weight:600;border:none;font-size:13.5px;}
.epc-erp-login-panel--split .login_form .nav-tabs>li.active>a{color:var(--ep-accent-2);border:none;border-bottom:2px solid var(--ep-accent);background:transparent;}
.epc-erp-login-panel--split .login_form .checkbox{font-size:13px;color:var(--ep-muted);}
.epc-erp-login-panel--split .login_form label{color:var(--ep-muted);}

/* Primary login button */
.epc-erp-login-panel--split .login_form .btn-success,
.epc-erp-login-panel--split .login_form button[type="submit"],
.epc-erp-login-panel--split .login_form .btn_enter{
  background:linear-gradient(135deg,#0ea5e9 0%,#2563eb 100%)!important;border:none!important;color:#fff!important;
  width:100%;padding:12px 16px;font-weight:700;border-radius:10px;font-size:14.5px;
  box-shadow:0 4px 14px rgba(14,165,233,.35);transition:.2s;
}
.epc-erp-login-panel--split .login_form .btn-success:hover,
.epc-erp-login-panel--split .login_form button[type="submit"]:hover{background:linear-gradient(135deg,#38bdf8 0%,#0ea5e9 100%)!important;box-shadow:0 6px 20px rgba(14,165,233,.45);}
.epc-erp-login-panel--split .login_form .btn-warning,
.epc-erp-login-panel--split .login_form .btn_forgot_password{
  background:transparent!important;border:none!important;color:var(--ep-muted)!important;
  box-shadow:none!important;text-decoration:underline;font-weight:500;font-size:13px;padding:8px 0!important;width:auto;
}
.epc-erp-login-panel--split .login_form .btn-warning:hover{color:var(--ep-accent-2)!important;}
.epc-erp-login-footnote{color:var(--ep-muted);}
.epc-erp-login-footnote a{font-weight:600;color:var(--ep-accent-2);}

/* Buttons */
.epc-erp-standalone .btn{border-radius:9px;font-weight:600;padding:9px 16px;font-size:14px;border:1px solid transparent;transition:.15s;}
.epc-erp-standalone .btn-lg{padding:11px 20px;font-size:15px;}
.epc-erp-standalone .btn-primary{background:linear-gradient(135deg,#0ea5e9 0%,#2563eb 100%);border:none;color:#fff;box-shadow:0 4px 14px rgba(14,165,233,.3);}
.epc-erp-standalone .btn-primary:hover,.epc-erp-standalone .btn-primary:focus{background:linear-gradient(135deg,#38bdf8 0%,#0ea5e9 100%);color:#fff;}
.epc-erp-standalone .btn-default{background:rgba(15,23,42,.5);border-color:rgba(99,102,241,.3);color:#e2e8f0;backdrop-filter:blur(4px);}
.epc-erp-standalone .btn-default:hover{background:rgba(14,165,233,.15);border-color:var(--ep-accent);color:var(--ep-accent-2);}

/* Access-denied / alerts */
.epc-erp-standalone .alert{border-radius:10px;border:1px solid rgba(99,102,241,.3);background:rgba(15,23,42,.7);color:#e2e8f0;}

/* Workspace content sits on a light panel — never inherit the dark-portal
   body ink (#f1f5f9) into tables/forms/report rows (near-invisible on white). */
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body{
  color:#1e293b;
}
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body .text-muted,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body .help-block{
  color:#475569!important;
}
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body td,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body th,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body p,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body li,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body label,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body .form-control,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body .epc-box-drill,
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content-body summary{
  color:#1e293b;
}
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content .btn-default{
  background:#fff;border-color:#cbd5e1;color:#334155;
}
body.epc-erp-standalone .epc-erp-shell--layout .epc-erp-content .alert{
  background:#eff6ff;border-color:#bfdbfe;color:#1e293b;
}

/* /erp/guide — white reading surface; dark portal ink must not leak into content */
body.epc-erp-standalone--guide .epc-erp-main{
  max-width:1100px;
}
body.epc-erp-standalone--guide .epc-erp-portal-wrap,
body.epc-erp-standalone--guide .epc-erp-workspace{
  background:#ffffff!important;
  color:#1e293b!important;
  border:1px solid #e2e8f0;
  border-radius:14px;
  box-shadow:0 12px 36px rgba(2,6,23,.28);
  padding:18px 20px 28px;
  margin:0 0 24px;
}
body.epc-erp-standalone--guide .epc-erp-workspace,
body.epc-erp-standalone--guide .epc-erp-workspace *:not(.fa):not(i):not(.btn):not(.btn *):not(a):not(code):not(pre){
  color:#1e293b;
}
body.epc-erp-standalone--guide .epc-erp-workspace a{color:#1d4ed8!important;font-weight:600;}
body.epc-erp-standalone--guide .epc-erp-workspace a:hover{color:#1e3a8a!important;}
body.epc-erp-standalone--guide .epc-erp-workspace .text-muted,
body.epc-erp-standalone--guide .epc-erp-workspace .help-block,
body.epc-erp-standalone--guide .epc-erp-workspace small{
  color:#475569!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .hpanel,
body.epc-erp-standalone--guide .epc-erp-workspace .panel,
body.epc-erp-standalone--guide .epc-erp-workspace .panel-body,
body.epc-erp-standalone--guide .epc-erp-workspace .well,
body.epc-erp-standalone--guide .epc-erp-workspace .epc-erp-guide-step,
body.epc-erp-standalone--guide .epc-erp-workspace .epc-erp-flow,
body.epc-erp-standalone--guide .epc-erp-workspace .table,
body.epc-erp-standalone--guide .epc-erp-workspace .table td,
body.epc-erp-standalone--guide .epc-erp-workspace .table th,
body.epc-erp-standalone--guide .epc-erp-workspace p,
body.epc-erp-standalone--guide .epc-erp-workspace li,
body.epc-erp-standalone--guide .epc-erp-workspace dd,
body.epc-erp-standalone--guide .epc-erp-workspace dt,
body.epc-erp-standalone--guide .epc-erp-workspace h3,
body.epc-erp-standalone--guide .epc-erp-workspace h4,
body.epc-erp-standalone--guide .epc-erp-workspace h5{
  color:#1e293b!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .hpanel,
body.epc-erp-standalone--guide .epc-erp-workspace .panel{
  background:#fff!important;
  border:1px solid #e2e8f0!important;
  box-shadow:none!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .panel-heading,
body.epc-erp-standalone--guide .epc-erp-workspace .panel-heading.hbuilt{
  background:#f8fafc!important;
  color:#0f172a!important;
  border-bottom:1px solid #e2e8f0!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .panel-body,
body.epc-erp-standalone--guide .epc-erp-workspace .well,
body.epc-erp-standalone--guide .epc-erp-workspace .well-sm{
  background:#fff!important;
  color:#1e293b!important;
  border-color:#e2e8f0!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .alert{
  background:#eff6ff!important;
  border-color:#bfdbfe!important;
  color:#1e293b!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .alert-info{
  background:#eff6ff!important;
  border-color:#93c5fd!important;
  color:#1e3a8a!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .alert-warning{
  background:#fffbeb!important;
  border-color:#fcd34d!important;
  color:#92400e!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .table{
  background:#fff!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .table>thead>tr>th,
body.epc-erp-standalone--guide .epc-erp-workspace .table>tbody>tr>th,
body.epc-erp-standalone--guide .epc-erp-workspace .table>tbody>tr>td{
  background:#fff!important;
  color:#1e293b!important;
  border-color:#e2e8f0!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .table-striped>tbody>tr:nth-of-type(odd)>td{
  background:#f8fafc!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace code,
body.epc-erp-standalone--guide .epc-erp-workspace pre{
  color:#0f172a!important;
  background:#f1f5f9!important;
  border:1px solid #e2e8f0;
  border-radius:4px;
  padding:1px 6px;
  font-size:12.5px;
}
body.epc-erp-standalone--guide .epc-erp-workspace .epc-erp-guide-step{
  background:#f8fafc!important;
  border-left:4px solid #2563eb!important;
  color:#1e293b!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .epc-erp-guide-intro{
  background:linear-gradient(135deg,#1e3a8a 0%,#1d4ed8 55%,#0ea5e9 100%)!important;
  color:#fff!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .epc-erp-guide-intro,
body.epc-erp-standalone--guide .epc-erp-workspace .epc-erp-guide-intro *{
  color:#fff!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .epc-erp-guide-intro a{
  color:#dbeafe!important;
  text-decoration:underline;
}
body.epc-erp-standalone--guide .epc-erp-workspace .btn-default{
  background:#fff!important;
  border-color:#cbd5e1!important;
  color:#334155!important;
}
body.epc-erp-standalone--guide .epc-erp-workspace .btn-primary{
  color:#fff!important;
}

@media (max-width:860px){
  .epc-erp-login-panel--split .col-md-5,.epc-erp-login-panel--split .col-md-7{width:100%;}
  .epc-erp-bos-hero__capabilities{grid-template-columns:1fr;}
}
</style>
