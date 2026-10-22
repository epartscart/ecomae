<?php
/**
 * PHP-served brand mark for ECOM AE / ECOM GLOBAL FZE LLC.
 *
 * Static assets under some paths are not reliably served on the marketing
 * host, whereas .php endpoints always are — so this streams the committed
 * glossy square logo mark (content/general_pages/ecomae_fze_mark.png) with an
 * image/png content type. epc_ecomae_platform_logo_url() points here, fixing
 * the logo across the marketing site, CP login, ERP and the hub animation.
 */

$file = __DIR__ . '/ecomae_fze_mark.png';
if (!is_file($file)) {
    header('HTTP/1.1 404 Not Found');
    exit;
}

header('Content-Type: image/png');
header('Cache-Control: public, max-age=86400');
header('Content-Length: ' . filesize($file));
readfile($file);
