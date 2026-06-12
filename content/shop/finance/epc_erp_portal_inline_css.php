<?php
/**
 * Inline professional stylesheet for the standalone ERP portal (topbar, home,
 * login and footer). Emitted into the portal <head> so the sign-in page renders
 * cleanly even when the external CSS files are not served as static assets on
 * the marketing host. Uses the --erp-* theme tokens (Blue & White) with safe
 * fallbacks.
 */
defined('_ASTEXE_') or die('No access');
?>
<style id="epc-erp-portal-inline">
:root{
  --ep-bg:var(--erp-bg-0,#f3f7fd);
  --ep-card:var(--erp-card,#ffffff);
  --ep-brd:var(--erp-card-brd,#e3e9f2);
  --ep-accent:var(--erp-accent,#1a56db);
  --ep-accent-2:var(--erp-accent-2,#2a6df4);
  --ep-text:var(--erp-text,#0d1b2a);
  --ep-muted:var(--erp-muted,#5b6b82);
}
body.epc-erp-standalone{
  background:var(--ep-bg);
  color:var(--ep-text);
  font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
  margin:0;
  -webkit-font-smoothing:antialiased;
}
body.epc-erp-standalone a{color:var(--ep-accent);}
body.epc-erp-standalone a:hover,body.epc-erp-standalone a:focus{color:var(--ep-accent-2);text-decoration:none;}

/* Top bar */
.epc-erp-topbar{background:var(--ep-card);border-bottom:1px solid var(--ep-brd);box-shadow:0 1px 3px rgba(13,27,42,.05);}
.epc-erp-topbar__inner{max-width:1180px;margin:0 auto;padding:7px 22px;display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;}
.epc-erp-topbar__brand{display:flex;align-items:center;gap:11px;color:var(--ep-text)!important;text-decoration:none;}
.epc-erp-topbar__brand .ech-static__logo,.epc-erp-topbar__brand img{width:32px;height:32px;border-radius:8px;display:block;}
.epc-erp-topbar__brand-text strong{display:block;font-size:16px;font-weight:700;line-height:1.1;letter-spacing:.2px;}
.epc-erp-topbar__brand-text small{display:block;font-size:11.5px;color:var(--ep-muted);}
.epc-erp-topbar__nav{display:flex;align-items:center;gap:4px;flex-wrap:wrap;}
.epc-erp-topbar__nav a{color:var(--ep-muted)!important;font-size:13.5px;font-weight:600;padding:7px 12px;border-radius:8px;text-decoration:none;transition:.15s;}
.epc-erp-topbar__nav a:hover{background:rgba(26,86,219,.08);color:var(--ep-accent)!important;}

/* Main / footer */
.epc-erp-main{max-width:1180px;margin:0 auto;padding:26px 22px 40px;}
.epc-erp-foot{max-width:1180px;margin:0 auto;padding:18px 22px 34px;color:var(--ep-muted);font-size:12.5px;border-top:1px solid var(--ep-brd);}

/* When the ERP application shell (sidebar + content) is on the page, let it use
   the full screen width — only the marketing/login pages stay centred. */
body.epc-erp-standalone:has(.epc-erp-shell--layout) .epc-erp-main{max-width:none;padding:0;}
body.epc-erp-standalone:has(.epc-erp-shell--layout) .epc-erp-topbar__inner{max-width:none;}
body.epc-erp-standalone:has(.epc-erp-shell--layout) .epc-erp-foot{max-width:none;}

/* Home hero */
.epc-erp-home-hero{background:linear-gradient(135deg,#ffffff 0%,#eef4fe 100%);border:1px solid var(--ep-brd);border-radius:18px;padding:30px 32px;margin-bottom:22px;}
.epc-erp-home-hero__inner{display:flex;gap:30px;flex-wrap:wrap;align-items:flex-start;justify-content:space-between;}
.epc-erp-home-hero__copy{flex:1 1 460px;min-width:300px;}
.epc-erp-home-hero__logo{width:62px;height:62px;border-radius:14px;display:block;margin-bottom:12px;box-shadow:0 6px 18px rgba(26,86,219,.25);}
.epc-erp-home-hero__eyebrow{text-transform:uppercase;letter-spacing:1.4px;font-size:11.5px;font-weight:700;color:var(--ep-accent);margin:0 0 6px;}
.epc-erp-home-hero__title{font-size:30px;line-height:1.15;font-weight:800;margin:0 0 12px;color:var(--ep-text);}
.epc-erp-home-hero__lead{font-size:15px;color:var(--ep-muted);max-width:620px;margin:0 0 18px;line-height:1.55;}
.epc-erp-home-hero__cta{display:flex;gap:10px;flex-wrap:wrap;}
.epc-erp-home-hero__stats{flex:0 0 280px;display:grid;grid-template-columns:1fr 1fr;gap:10px;}
.epc-erp-home-stat{background:var(--ep-card);border:1px solid var(--ep-brd);border-radius:12px;padding:12px 14px;}
.epc-erp-home-stat strong{display:block;font-size:13.5px;color:var(--ep-text);margin-bottom:2px;}
.epc-erp-home-stat span{font-size:11.5px;color:var(--ep-muted);line-height:1.35;}

/* Home product cards */
.epc-erp-home-grid{margin-bottom:8px;}
.epc-erp-home-card{background:var(--ep-card);border:1px solid var(--ep-brd);border-radius:14px;padding:20px 20px 16px;height:100%;box-shadow:0 1px 3px rgba(13,27,42,.04);transition:.15s;}
.epc-erp-home-card:hover{box-shadow:0 8px 24px rgba(13,27,42,.08);transform:translateY(-2px);}
.epc-erp-home-card--accent{border-color:var(--ep-accent);box-shadow:0 6px 20px rgba(26,86,219,.10);}
.epc-erp-home-card h3{font-size:17px;font-weight:700;margin:0 0 8px;color:var(--ep-text);}
.epc-erp-home-card p{font-size:13.5px;color:var(--ep-muted);line-height:1.5;margin:0 0 10px;}
.epc-erp-home-card .text-primary{color:var(--ep-accent)!important;}

/* Buttons */
.epc-erp-standalone .btn{border-radius:9px;font-weight:600;padding:9px 16px;font-size:14px;border:1px solid transparent;transition:.15s;}
.epc-erp-standalone .btn-lg{padding:11px 20px;font-size:15px;}
.epc-erp-standalone .btn-primary{background:var(--ep-accent);border-color:var(--ep-accent);color:#fff;}
.epc-erp-standalone .btn-primary:hover,.epc-erp-standalone .btn-primary:focus{background:var(--ep-accent-2);border-color:var(--ep-accent-2);color:#fff;}
.epc-erp-standalone .btn-default{background:#fff;border-color:var(--ep-brd);color:var(--ep-text);}
.epc-erp-standalone .btn-default:hover{background:#f4f8ff;border-color:var(--ep-accent);color:var(--ep-accent);}

/* Login split panel */
.epc-erp-login-panel--split{background:var(--ep-card);border:1px solid var(--ep-brd);border-radius:18px;padding:0;margin-top:6px;overflow:hidden;box-shadow:0 10px 34px rgba(13,27,42,.07);}
.epc-erp-login-panel--split .row{margin:0;display:flex;flex-wrap:wrap;}
.epc-erp-login-panel--split .col-md-5,.epc-erp-login-panel--split .col-md-7{padding:0;float:none;}
.epc-erp-login-panel--split .col-md-5{width:42%;}
.epc-erp-login-panel--split .col-md-7{width:58%;}
.epc-erp-login-panel__brand{background:linear-gradient(150deg,var(--ep-accent) 0%,#0f3da8 100%);color:#fff;padding:36px 34px;}
.epc-erp-login-panel__brand .ech-static__logo,.epc-erp-login-panel__brand img{width:64px;height:64px;border-radius:15px;background:rgba(255,255,255,.12);padding:4px;margin-bottom:14px;display:block;}
.epc-erp-login-panel__brand .ech-static__title,.epc-erp-login-panel__brand .ech-static__tagline{color:#fff;display:block;}
.epc-erp-login-panel__brand .ech-static__title{font-size:18px;font-weight:700;}
.epc-erp-login-panel__brand .ech-static__tagline{font-size:12.5px;opacity:.85;margin-bottom:6px;}
.epc-erp-login-panel__brand h1{font-size:24px;font-weight:800;margin:14px 0 10px;color:#fff;}
.epc-erp-login-lead{font-size:14px;line-height:1.6;color:rgba(255,255,255,.9);margin-bottom:18px;}
.epc-erp-login-features{list-style:none;padding:0;margin:0;}
.epc-erp-login-features li{font-size:13.5px;margin-bottom:9px;color:rgba(255,255,255,.95);}
.epc-erp-login-features li .fa{margin-right:8px;color:#bfe0ff;}

/* Login form column */
.epc-erp-login-panel--split .col-md-7{padding:34px 34px 28px;}
.epc-erp-login-panel--split .hpanel{border:none;box-shadow:none;background:transparent;}
.epc-erp-login-panel--split .panel-heading{font-size:19px;font-weight:700;color:var(--ep-text);background:transparent;border:none;padding:0 0 14px;}
.epc-erp-login-panel--split .panel-body{padding:0;background:transparent;}
.epc-erp-login-panel--split .login_form{background:transparent;}
.epc-erp-login-panel--split .login_form .panel-heading{display:none;}
.epc-erp-login-panel--split .login_form .panel-body{padding:0;}

/* Form fields */
.epc-erp-login-panel--split .login_form input[type="email"],
.epc-erp-login-panel--split .login_form input[type="password"],
.epc-erp-login-panel--split .login_form input[type="text"],
.epc-erp-login-panel--split .login_form .form-control{
  width:100%;height:auto;padding:11px 13px;font-size:14px;color:var(--ep-text);
  background:#fff;border:1px solid var(--ep-brd);border-radius:9px;box-shadow:none;
}
.epc-erp-login-panel--split .login_form input:focus,
.epc-erp-login-panel--split .login_form .form-control:focus{
  border-color:var(--ep-accent);outline:none;box-shadow:0 0 0 3px rgba(26,86,219,.15);
}
.epc-erp-login-panel--split .login_form .input-group{display:block;margin-bottom:12px;width:100%;border:none;background:none;}
.epc-erp-login-panel--split .login_form .input-group .input-group-addon{display:none;}
.epc-erp-login-panel--split .login_form .nav-tabs{border-bottom:1px solid var(--ep-brd);margin-bottom:16px;}
.epc-erp-login-panel--split .login_form .nav-tabs>li>a{color:var(--ep-muted);font-weight:600;border:none;font-size:13.5px;}
.epc-erp-login-panel--split .login_form .nav-tabs>li.active>a{color:var(--ep-accent);border:none;border-bottom:2px solid var(--ep-accent);background:transparent;}
.epc-erp-login-panel--split .login_form .checkbox{font-size:13px;color:var(--ep-muted);}

/* Primary login button + de-emphasise the "forgot password" button */
.epc-erp-login-panel--split .login_form .btn-success,
.epc-erp-login-panel--split .login_form button[type="submit"],
.epc-erp-login-panel--split .login_form .btn_enter{
  background:var(--ep-accent)!important;border-color:var(--ep-accent)!important;color:#fff!important;
  width:100%;padding:11px 16px;font-weight:700;border-radius:9px;font-size:14.5px;
}
.epc-erp-login-panel--split .login_form .btn-success:hover,
.epc-erp-login-panel--split .login_form button[type="submit"]:hover{background:var(--ep-accent-2)!important;border-color:var(--ep-accent-2)!important;}
.epc-erp-login-panel--split .login_form .btn-warning,
.epc-erp-login-panel--split .login_form .btn_forgot_password{
  background:transparent!important;border:none!important;color:var(--ep-muted)!important;
  box-shadow:none!important;text-decoration:underline;font-weight:500;font-size:13px;padding:8px 0!important;width:auto;
}
.epc-erp-login-panel--split .login_form .btn-warning:hover{color:var(--ep-accent)!important;}
.epc-erp-login-footnote a{font-weight:600;}

/* Access-denied / alerts */
.epc-erp-standalone .alert{border-radius:10px;border:1px solid var(--ep-brd);}

@media (max-width:860px){
  .epc-erp-login-panel--split .col-md-5,.epc-erp-login-panel--split .col-md-7{width:100%;}
  .epc-erp-home-hero__stats{flex-basis:100%;}
}
</style>
