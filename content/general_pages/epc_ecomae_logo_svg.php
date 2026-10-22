<?php
/**
 * PHP-served brand mark for ECOM AE / ECOM GLOBAL FZE LLC.
 *
 * Static assets under /content are not reliably deployed to the docroot, so
 * this endpoint redirects to the glossy square logo mark hosted by the
 * ASP.NET platform asset route (always live on every host).
 * epc_ecomae_platform_logo_url() points here, fixing the logo across the
 * marketing site, CP login, ERP and the hub animation.
 */

header('Location: /platform-assets/login-brand/ecomae-fze-mark.png', true, 302);
header('Cache-Control: public, max-age=86400');
